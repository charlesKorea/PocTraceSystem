using Dapper;
using Newtonsoft.Json;
using OfficeOpenXml;
using PocTrace.Core.Composition;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Progress;
using PocTrace.Core.Engine.Reporting;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Engine.Validation.Rules;
using PocTrace.Core.Enums;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Infrastructure.Repositories;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;
using Quartz;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PocTrace.Admin.Jobs
{
    [DisallowConcurrentExecution]
    public class DailyTraceJob : IJob
    {
        /// <summary>Quartz JobDataMap: Mode = "Full" | "CacheOnly"</summary>
        public const string JobDataModeKey = "Mode";
        public const string ModeFull = "Full";
        public const string ModeCacheOnly = "CacheOnly";

        private static int _executionGate;

        /// <summary>True while any DailyTraceJob instance (cron or manual) is inside Execute.</summary>
        public static bool IsRunning => System.Threading.Volatile.Read(ref _executionGate) != 0;

        public async Task Execute(IJobExecutionContext context)
        {
            string mode = context?.JobDetail?.JobDataMap?.GetString(JobDataModeKey) ?? ModeFull;
            if (System.Threading.Interlocked.CompareExchange(ref _executionGate, 1, 0) != 0)
            {
                Log.Warning(">>> [DailyTraceJob] Overlapping execution skipped. Mode={Mode}", mode);
                return;
            }

            try
            {
            Log.Information(">>> [DailyTraceJob] Started. Mode={Mode}", mode);
            AdminJobProgress.Report(mode == ModeCacheOnly ? "캐시 갱신 시작" : "전체 배치 시작", 0);

            try
            {
                // 1. 설정 로드
                string connString = SecretConfigStore.ConnectionString("PocTraceDB");
                if (string.IsNullOrEmpty(connString))
                    throw new Exception("PocTraceDB connection string is missing. " + SecretConfigStore.FilePath);

                // [New] 상세 디버그 로그 옵션 로드
                bool enableDetailedDebug = GlobalConfig.EnableDetailedDebug;
                Log.Information(">>> [DailyTraceJob] Detailed Debug Mode: {Enabled}", enableDetailedDebug);

                bool excelPerfCompareEnabled = bool.TryParse(
                    ConfigurationManager.AppSettings["ExcelPerformanceCompareEnabled"],
                    out var perfEnabled) && perfEnabled;
                int excelPerfCompareThreshold = int.TryParse(
                    ConfigurationManager.AppSettings["ExcelPerformanceCompareRowThreshold"],
                    out var perfThreshold) ? perfThreshold : 50000;
                Log.Information(">>> [DailyTraceJob] Excel Perf Compare: Enabled={Enabled}, Threshold={Threshold:N0}",
                    excelPerfCompareEnabled, excelPerfCompareThreshold);

                                // 라이선스 설정 (EPPlus)
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // [신규] 로그 파일 삭제 (매 실행 시 기존 로그 삭제)
                string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.Admin);
                if (Directory.Exists(logDirectory))
                {
                    try
                    {
                        var logFiles = Directory.GetFiles(logDirectory, "*.log");
                        foreach (var file in logFiles)
                        {
                            try { File.Delete(file); }
                            catch { /* 파일이 사용 중이면 무시 */ }
                        }
                        Log.Information($"[Log Cleanup] Deleted {logFiles.Length} old log files.");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Log Cleanup] Failed to delete old logs: {ex.Message}");
                    }
                }

                var targetDbs = GlobalConfig.TargetDatabases;
                if (targetDbs == null || targetDbs.Length == 0)
                    throw new Exception("TargetDatabases가 비어 있습니다.");

                if (string.Equals(mode, ModeCacheOnly, StringComparison.OrdinalIgnoreCase))
                {
                    await RunCacheOnlyAsync(connString, targetDbs).ConfigureAwait(false);
                    return;
                }

                string today = DateTime.Now.ToString("yyyyMMdd");
                string baseOutputFolder = GlobalConfig.DailyReportPath;
                if (!Directory.Exists(baseOutputFolder)) Directory.CreateDirectory(baseOutputFolder);

                var htmlGenerator = new HtmlReportGenerator(baseOutputFolder);
                AdminJobProgress.Report("HTML assets 배포", 2, baseOutputFolder);
                htmlGenerator.DeployAssets();

                // Check Rules 이슈 발생 횟수 집계 시작
                ValidationRuleRegistry.ResetHits();
                int checkRulesCount = ValidationRuleRegistry.RegisteredCount;
                Log.Information(">>> [DailyTraceJob] Check Rules registered: {Count}", checkRulesCount);

                var allLineHistories = new Dictionary<string, List<DailyStat>>();
                List<dynamic> summaryStats = new List<dynamic>(); // 통합 Summary용 데이터
                var progressReports = new List<ProgressReportDto>();
                var progressEngine = new DesignProgressEngine(Log.Logger);
                
                Stopwatch totalSw = Stopwatch.StartNew();
                int dbCount = targetDbs.Length;
                int dbIndex = 0;

                // ==========================================
                // DB별 반복 실행
                // ==========================================
                foreach (var dbName in targetDbs)
                {
                    GraphCacheManager manager = null;
                    int dbBase = 0;
                    int dbSpan = 100;
                    List<NodeInfo> nodes = null;
                    List<PocData> pocs = null;
                    List<PipeData> pipes = null;
                    List<TakeoffData> takeoffs = null;
                    List<DynamicPropertiesData> dynamicProperties = null;
                    try
                    {
                    dbIndex++;
                    dbBase = ((dbIndex - 1) * 100) / dbCount;
                    dbSpan = 100 / dbCount;
                    Stopwatch sw = Stopwatch.StartNew();

                    Log.Information($"\n🟢 >>> [Start Processing DB: {dbName}] ----------------");
                    AdminJobProgress.Report("DB Query (Graph)", dbBase + (int)(dbSpan * 0.05), dbName, null, dbName);

                    // A. 데이터 로드 (DB -> Memory)
                    var binaryRepo = new BinaryCacheRepository();

                    // [Fix 1] 쿼리 결과 매핑 변경 (String -> EquipmentInfo)
                    List<EquipmentInfo> allEquips;
                    using (IDbConnection conn = new SqlConnection(connString))
                    {
                        var bundle = await GraphQueryProviders.CreateFromConfig()
                            .LoadAsync(conn, dbName, null)
                            .ConfigureAwait(false);
                        nodes = bundle.Nodes;
                        pocs = bundle.Pocs;
                        pipes = bundle.Pipes;
                        takeoffs = bundle.Takeoffs;
                        dynamicProperties = bundle.HookupNumbers;

                        BimIdDiagnostics.AnalyzeAndLog("AdminDbLoad", dbName, CacheConstants.CurrentVersion, nodes);
                    }

                    // B. Binary Cache 저장 (Admin 전용 기능)
                    binaryRepo.SaveToCache(dbName, nodes, pocs, pipes, takeoffs, dynamicProperties);
                    Log.Information($"[{dbName}] Binary Cache Saved within {sw.Elapsed.TotalMilliseconds} msec");
                    AdminJobProgress.Report("Binary Cache 저장", dbBase + (int)(dbSpan * 0.12),
                        $"nodes={nodes.Count}, pocs={pocs.Count}", sw.Elapsed, dbName);
                    sw.Restart();

                    // B2. Group(BIM) sidecar 캐시 (GraphCache와 실패 격리)
                    await SaveBimGroupCacheSafeAsync(connString, dbName).ConfigureAwait(false);
                    AdminJobProgress.Report("Group Cache", dbBase + (int)(dbSpan * 0.14), dbName, sw.Elapsed, dbName);
                    sw.Restart();

                    // B3. 철거 전용 3-% Template Equipment sidecar
                    await SaveTemplateEquipmentCacheSafeAsync(connString, dbName).ConfigureAwait(false);
                    AdminJobProgress.Report("TemplateEqp Cache", dbBase + (int)(dbSpan * 0.15), dbName, sw.Elapsed, dbName);
                    sw.Restart();

                    // C. UT Mapping 로드 (CSV)
                    // GraphLoaderRepository를 임시로 사용하여 로드 (DB 연결 불필요)
                    var loaderRepo = new GraphLoaderRepository(null);
                    var utMaps = loaderRepo.LoadUtMapping();

                    // D. 엔진 초기화
                    manager = new GraphCacheManager();
                    manager.Initialize(dbName, nodes, pocs, pipes, takeoffs, dynamicProperties, utMaps,
                        GlobalConfig.AdminBuildReverseGraphIndex);

                    // SQL 적재 리스트는 manager에 흡수 후 즉시 해제 (x2 pipe index OOM 완화)
                    nodes = null;
                    pocs = null;
                    pipes = null;
                    takeoffs = null;
                    dynamicProperties = null;

                    // 명시적 인터페이스 캐스팅
                    IGraphCache cache = manager;

                    // E. 전체 설비 목록 조회
                    AdminJobProgress.Report("설비 Query", dbBase + (int)(dbSpan * 0.18), dbName, null, dbName);
                    using (IDbConnection conn = new SqlConnection(connString))
                    {
                        string query = SqlQueries.GetSelectAllEquipments(dbName);
                        allEquips = (await conn.QueryAsync<EquipmentInfo>(query)).ToList();
                    }
                    AdminJobProgress.Report("설비 Query 완료", dbBase + (int)(dbSpan * 0.22),
                        $"count={allEquips.Count}", sw.Elapsed, dbName);
                    sw.Restart();

                    // F. 배치 추적 실행 (Parallel)
                    AdminJobProgress.Report("Trace / Validation", dbBase + (int)(dbSpan * 0.25),
                        $"parallel={GlobalConfig.MaxParallelTrace}", null, dbName);
                    var allResults = new List<TraceResult>();
                    object lockObj = new object();
                    var tracedEquipmentTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var startTagMissingTags = new ConcurrentBag<string>();
                    var traceExceptionTags = new ConcurrentBag<string>();
                    var noResultAfterTraceTags = new ConcurrentBag<string>();
                    var ingOnlyTags = new ConcurrentBag<string>();

                    int maxParallel = Math.Max(1, GlobalConfig.MaxParallelTrace);
                    var appServices = PocTraceAppServices.Create(Log.Logger, GlobalConfig.AllowDatabaseFallback);

                    Log.Information("⚙️ >>> [DailyTraceJob] Starting parallel trace for {Count} equipments (MaxDegreeOfParallelism: {Max})",
                        allEquips.Count, maxParallel);
                    sw.Restart();

                    Parallel.ForEach(allEquips, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, eq =>
                    {
                        int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

                        try
                        {
                            var startPocs = cache.FindStartPocs(eq.Tag, includePartialMatch: false)?.ToList();
                            if (startPocs == null || startPocs.Count == 0)
                            {
                                startTagMissingTags.Add(eq.Tag);
                                Log.Warning("[TraceGap][{Db}] StartTag 미존재(시작 POC 없음): {Tag}", dbName, eq.Tag);
                                return;
                            }

                            var threadEngine = appServices.CreateTraceEngine(cache);
                            var results = threadEngine.RunTrace(eq.Tag, 0, enableDetailedDebug, System.Threading.CancellationToken.None);

                            if (results != null && results.Any())
                            {
                                foreach (var r in results)
                                {
                                    // NodeInfo(v6)에서 이미 채워진 경우 유지; 설비 쿼리 값이 있으면 덮어씀
                                    if (!string.IsNullOrWhiteSpace(eq.ConstructionCode))
                                        r.ConstructionCode = eq.ConstructionCode;
                                }
                                lock (lockObj)
                                {
                                    allResults.AddRange(results);
                                    tracedEquipmentTags.Add(eq.Tag);
                                }

                                if (results.All(r => string.Equals(r.STATUS, "ING", StringComparison.OrdinalIgnoreCase)))
                                {
                                    ingOnlyTags.Add(eq.Tag);
                                }
                            }
                            else
                            {
                                noResultAfterTraceTags.Add(eq.Tag);
                            }

                            Log.Information($"[Thread-{threadId}] Completed: {eq.Tag} ({results?.Count ?? 0} results) ");
                        }
                        catch (Exception ex)
                        {
                            traceExceptionTags.Add(eq.Tag);
                            Log.Error($"[Thread-{threadId}] Error processing {eq.Tag}: {ex.Message}");
                        }
                    });

                    Log.Information($"⚡ >>> [DailyTraceJob] Parallel Trace Completed. Total Results: {allResults.Count} within {sw.Elapsed.TotalMilliseconds} msec ({sw.Elapsed.TotalSeconds:F1} sec)");

                    var queriedTags = allEquips
                        .Select(e => e.Tag)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var noResultTags = queriedTags
                        .Where(t => !tracedEquipmentTags.Contains(t))
                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var startTagMissingSet = startTagMissingTags
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var traceExceptionSet = traceExceptionTags
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var noResultAfterTraceSet = noResultAfterTraceTags
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var ingOnlySet = ingOnlyTags
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var noResultUnclassified = noResultTags
                        .Where(t =>
                            !startTagMissingSet.Contains(t, StringComparer.OrdinalIgnoreCase) &&
                            !traceExceptionSet.Contains(t, StringComparer.OrdinalIgnoreCase) &&
                            !noResultAfterTraceSet.Contains(t, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    Log.Information(
                        "[TraceGap][{Db}] QueryCount={QueryCount}, TracedCount={TracedCount}, NoResultCount={NoResultCount}, TotalResults={TotalResults}",
                        dbName, queriedTags.Count, tracedEquipmentTags.Count, noResultTags.Count, allResults.Count);

                    Log.Information(
                        "[TraceGap][{Db}] Categories => StartTagMissing={StartTagMissing}, TraceException={TraceException}, NoResultAfterTrace={NoResultAfterTrace}, IngOnly={IngOnly}, NoResultUnclassified={NoResultUnclassified}",
                        dbName, startTagMissingSet.Count, traceExceptionSet.Count, noResultAfterTraceSet.Count, ingOnlySet.Count, noResultUnclassified.Count);

                    // -------------------------------------------------------------
                    // Trace Gap TXT (list txt/)
                    // 원인: 설비 쿼리 대비 Trace 결과가 없거나 ING-only인 태그 갭 진단
                    // Flow: Parallel Trace → NoResult/StartTagMissing/Exception/AfterTrace/ING 분류 → 파일 기록
                    // 활용: 캐시·Tag 매칭·스키마 이슈 수동 점검, 재실행 대상 리스트 (HTML/Excel 미포함)
                    // -------------------------------------------------------------
                    if (noResultTags.Count > 0)
                    {
                        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string listTxtDir = Path.Combine(baseOutputFolder, "list txt");
                        Directory.CreateDirectory(listTxtDir);

                        string noResultPath = Path.Combine(
                            listTxtDir,
                            $"Trace_NoResultTags_{dbName}_{stamp}.txt");
                        File.WriteAllLines(noResultPath, noResultTags);

                        string sample = string.Join(", ", noResultTags.Take(30));
                        Log.Warning(
                            "[TraceGap][{Db}] No-result sample(30): {Sample} / FullList={Path}",
                            dbName, sample, noResultPath);

                        string startTagMissingPath = Path.Combine(listTxtDir, $"Trace_NoResult_StartTagMissing_{dbName}_{stamp}.txt");
                        string traceExceptionPath = Path.Combine(listTxtDir, $"Trace_NoResult_TraceException_{dbName}_{stamp}.txt");
                        string noResultAfterTracePath = Path.Combine(listTxtDir, $"Trace_NoResult_AfterTrace_{dbName}_{stamp}.txt");
                        string noResultUnclassifiedPath = Path.Combine(listTxtDir, $"Trace_NoResult_Unclassified_{dbName}_{stamp}.txt");
                        string ingOnlyPath = Path.Combine(listTxtDir, $"Trace_ING_OnlyTags_{dbName}_{stamp}.txt");

                        File.WriteAllLines(startTagMissingPath, startTagMissingSet);
                        File.WriteAllLines(traceExceptionPath, traceExceptionSet);
                        File.WriteAllLines(noResultAfterTracePath, noResultAfterTraceSet);
                        File.WriteAllLines(noResultUnclassifiedPath, noResultUnclassified);
                        File.WriteAllLines(ingOnlyPath, ingOnlySet);

                        Log.Information(
                            "[TraceGap][{Db}] Category files => StartTagMissing={StartPath}, TraceException={ExceptionPath}, AfterTrace={AfterTracePath}, Unclassified={UnclassifiedPath}, IngOnly={IngPath}",
                            dbName, startTagMissingPath, traceExceptionPath, noResultAfterTracePath, noResultUnclassifiedPath, ingOnlyPath);
                    }

                    AdminJobProgress.Report("Trace / Validation 완료", dbBase + (int)(dbSpan * 0.55),
                        $"results={allResults.Count}", sw.Elapsed, dbName);
                    sw.Restart();

                    AdminJobProgress.Report("Report 집계", dbBase + (int)(dbSpan * 0.58), dbName, null, dbName);

                    // [중요] TraceResult(allResults) 확보 후
                    // -------------------------------------------------------------
                    // [Step 1] C# 메모리에서 데이터 다이어트 (Pre-aggregation)
                    // -------------------------------------------------------------
                    var reportDto = new ReportDto();

                    // 1. 설비별 그룹핑
                    var equipGroups = allResults.GroupBy(r => r.StartEquipmentTag).ToList();
                    foreach (var group in equipGroups)
                    {
                        var trs = group.ToList();
                        // [Fix 1] Main 설비 찾기 로직 (1-MAIN이 포함된 대상 우선, 없으면 첫번째)
                        var mainTr = trs.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.StartNodeSubDivision1) &&
                        p.StartNodeSubDivision1.Contains("1-MAIN")) ?? trs.First();

                        // [Fix 1] Length 값 가져오기 (PocData나 EquipmentInfo에 있다고 가정)
                        // 만약 TraceResult에 없다면, PocData에서 가져와서 TraceResult에 매핑해줘야 함
                        // 여기서는 임시로 TraceResult에 Length 속성이 있다고 가정하거나, 
                        // EquipmentInfo 리스트에서 Lookup 해야 함.

                        // 1. 안전한 합계 계산 (long으로 캐스팅 후 Sum)
                        // TraceResult의 TotalLength가 int라고 가정
                        double totalLen = trs.Sum(p => (long)p.TotalLength);
                        string lengthVal = totalLen.ToString();

                        // 이슈 리스트 (중복 제거하여 문자열로 압축)
                        var issues = trs.SelectMany(p => p.IssueList).Distinct().Take(5).ToList(); // Top 5만
                        string issueText = string.Join(", ", issues);
                        if (trs.SelectMany(p => p.IssueList).Distinct().Count() > 5) issueText += "...";

                        reportDto.Equipments.Add(new EquipmentSummaryDto
                        {
                            Guid = mainTr.StartNodeGUID.ToString(),
                            Tag = group.Key,
                            ConstCode = mainTr.ConstructionCode, // 앞서 매핑했다고 가정
                            MainName = mainTr.StartNodeName, // [New] Main 이름
                            Length = lengthVal,                    // [New] Length
                            Process = !string.IsNullOrEmpty(mainTr.StartNodeSubDivision2) ? mainTr.StartNodeSubDivision2 : "-",
                            Result = trs.Any(p => p.FailCount > 0) ? "FAIL" : "PASS",
                            IssueCount = trs.Sum(p => p.IssueList.Count),
                            EqpQty = trs.Select(p => p.StartNodeGUID).Distinct().Count(),
                            TotalPoc = trs.Count,
                            PassPoc = trs.Count(p => p.FailCount == 0),
                            FailPoc = trs.Count(p => p.FailCount > 0),
                            MainIssues = issueText // Raw Data 대신 텍스트만
                        });
                    }

                    // 2. 파이 차트용 (Error Stats)
                    var allIssues = allResults.SelectMany(r => r.IssueList);
                    reportDto.ErrorStats = allIssues
                        .GroupBy(i => i)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // 2. [New] Belong Stats 집계
                    // IssueBelongTo 값이 없으면 "Unknown" 처리
                    reportDto.BelongStats = allResults
                        .Where(r => r.FailCount > 0 && !string.IsNullOrEmpty(r.IssueBelongTo))
                        .GroupBy(r => r.IssueBelongTo)
                        .ToDictionary(g => g.Key, g => g.Count());

                                        // 3. 바 차트용 (Process Stats) — 설비 Pass/Fail + POC 수량
                    reportDto.ProcessStats = reportDto.Equipments
                        .GroupBy(e => e.Process ?? "-")
                        .ToDictionary(g => g.Key, g => new ProcessStatDto
                        {
                            Pass = g.Count(e => e.Result == "PASS"),
                            Fail = g.Count(e => e.Result == "FAIL"),
                            EqpCount = g.Count(),
                            PassPoc = g.Sum(e => e.PassPoc),
                            FailPoc = g.Sum(e => e.FailPoc),
                            TotalPoc = g.Sum(e => e.TotalPoc)
                        });

                    AdminJobProgress.Report("Hookup Validation", dbBase + (int)(dbSpan * 0.62), dbName, null, dbName);
                    var hookupValidationService = new HookupValidationService(Log.Logger);
                    var hookupValidationResults = hookupValidationService.Validate(allResults, cache);

                    reportDto.HookupStats = new HookupStatsDto
                    {
                        OK = hookupValidationResults.Count(x => x.Status == "OK"),
                        MISSING = hookupValidationResults.Count(x => x.Status == "MISSING"),
                        MISMATCH = hookupValidationResults.Count(x => x.Status == "MISMATCH"),
                        TAG_ERROR = hookupValidationResults.Count(x => x.Status == "TAG_ERROR"),
                        DATA_ERROR = hookupValidationResults.Count(x => x.Status == "DATA_ERROR"),
                        TotalEquip = hookupValidationResults.Select(x => x.EquipmentTag).Distinct().Count(),
                        TotalObjects = hookupValidationResults.Count
                    };

                    // 공정별 H/U Pass/Fail (설비 Tag → Process 매핑)
                    var processByTag = reportDto.Equipments
                        .Where(e => !string.IsNullOrWhiteSpace(e.Tag))
                        .GroupBy(e => e.Tag.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().Process ?? "-", StringComparer.OrdinalIgnoreCase);

                    foreach (var hv in hookupValidationResults)
                    {
                        string proc = "-";
                        if (!string.IsNullOrWhiteSpace(hv.EquipmentTag) &&
                            processByTag.TryGetValue(hv.EquipmentTag.Trim(), out var mapped))
                            proc = mapped;

                        ProcessStatDto ps;
                        if (!reportDto.ProcessStats.TryGetValue(proc, out ps))
                        {
                            ps = new ProcessStatDto();
                            reportDto.ProcessStats[proc] = ps;
                        }

                        if (string.Equals(hv.Status, "OK", StringComparison.OrdinalIgnoreCase))
                            ps.HookupPass++;
                        else
                            ps.HookupFail++;
                    }

                    // [디버그] HookupStats 로그
                    Log.Information($"[Hookup] Validation Results Count={hookupValidationResults.Count}, TotalObjects={reportDto.HookupStats.TotalObjects}, OK={reportDto.HookupStats.OK}, MISSING={reportDto.HookupStats.MISSING}, MISMATCH={reportDto.HookupStats.MISMATCH}");

                    // Check Rules 카탈로그: Hookup은 고정 단일 Rule — 비정상 건수를 Hit로 적립
                    int hookupNonOk = hookupValidationResults.Count(x => x.Status != "OK");
                    ValidationRuleRegistry.RecordHookupHits(hookupNonOk);

                    // -------------------------------------------------------------
                    // [Step 2] History 및 Summary 계산 (기존 로직 유지/보완)
                    // -------------------------------------------------------------
                    // ... (History 저장 로직은 reportDto.Equipments 기반으로 계산하면 더 빠름) ...
                    int totalEq = reportDto.Equipments.Count;
                    int passEq = reportDto.Equipments.Count(e => e.Result == "PASS");

                    //int totalEq = equipGroups.Count;
                    // int passEq = equipGroups.Count(g => g.All(r => r.FailCount == 0));
                    int failEq = totalEq - passEq;
                    double rateEq = totalEq > 0 ? (double)passEq / totalEq * 100.0 : 0;

                    // POC 기준 통계
                    int totalPoc = allResults.Count;
                    int failPoc = allResults.Count(r => r.FailCount > 0);
                    int passPoc = totalPoc - failPoc;
                    double ratePoc = totalPoc > 0 ? (double)passPoc / totalPoc * 100.0 : 0;

                                        // H. 시계열 데이터(History) 업데이트 — DailyReport/data/
                    string dataDir = Path.Combine(baseOutputFolder, "data");
                    Directory.CreateDirectory(dataDir);
                    string historyFile = Path.Combine(dataDir, $"Stats_History_{dbName}.json");
                    string legacyHistory = Path.Combine(baseOutputFolder, $"Stats_History_{dbName}.json");
                    if (!File.Exists(historyFile) && File.Exists(legacyHistory))
                    {
                        try { File.Copy(legacyHistory, historyFile, overwrite: false); }
                        catch { /* ignore */ }
                    }
                    
                    // [신규] Hookup Error Count (MISSING + MISMATCH)
                    int hookupErrorCount = (reportDto.HookupStats?.MISSING ?? 0) + (reportDto.HookupStats?.MISMATCH ?? 0);
                    double hookupPassRate = (reportDto.HookupStats?.TotalObjects ?? 0) > 0 
                        ? (double)(reportDto.HookupStats?.OK ?? 0) / (reportDto.HookupStats?.TotalObjects ?? 1) * 100.0 
                        : 0;
                    double avgPassRate = (reportDto.HookupStats?.TotalObjects ?? 0) > 0
                        ? Math.Max(0, Math.Min(100, (ratePoc + hookupPassRate) / 2))
                        : Math.Max(0, Math.Min(100, ratePoc));

                    var todayStat = new DailyStat
                    {
                        Date = DateTime.Now.ToString("yyyy-MM-dd"),
                        TotalEq = totalEq,
                        PassEq = passEq,
                        FailEq = failEq,
                        RateEq = Math.Round(rateEq, 1),
                        TotalPoc = totalPoc,
                        PassPoc = passPoc,
                        FailPoc = failPoc,
                        RatePoc = Math.Round(ratePoc, 1),
                        IssuePocCount = allResults.Sum(r => r.IssueList.Count),
                        HookupErrorCount = hookupErrorCount,
                        AvgPassRate = Math.Round(avgPassRate, 1),
                        CheckRulesCount = checkRulesCount
                    };

                    List<DailyStat> history = new List<DailyStat>();
                    if (File.Exists(historyFile))
                    {
                        try
                        {
                            string json = TextFileEncoding.ReadAllText(historyFile);
                            history = JsonConvert.DeserializeObject<List<DailyStat>>(json) ?? new List<DailyStat>();
                        }
                        catch (Exception historyEx)
                        {
                            string backup = historyFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                            Log.Error(historyEx, "Stats history unreadable for {Db}. Backup={Backup}", dbName, backup);
                            try { File.Copy(historyFile, backup, overwrite: true); }
                            catch (Exception copyEx)
                            {
                                Log.Warning(copyEx, "Could not backup corrupt history {File}", historyFile);
                            }
                            history = new List<DailyStat>();
                        }
                    }
                    history.RemoveAll(x => x.Date == todayStat.Date); // 오늘자 중복 제거
                    history.Add(todayStat);
                    history = history.OrderBy(x => x.Date).ToList(); // 날짜 정렬

                    // History 파일 저장 (다음 실행을 위해)
                    TextFileEncoding.WriteAllText(historyFile, JsonConvert.SerializeObject(history));


                    // -------------------------------------------------------------
                    // [Step 3] JS 생성 (가벼워진 reportDto 전달)
                    // -------------------------------------------------------------
                    // 기존: htmlGenerator.GenerateReport(dbName, allResults, history); -> 1.9GB
                    // 변경: htmlGenerator.GenerateReport(dbName, reportDto, history);  -> 5MB
                    AdminJobProgress.Report("HTML 생성", dbBase + (int)(dbSpan * 0.72), dbName, null, dbName);
                    htmlGenerator.GenerateReport(dbName, reportDto, history, checkRulesCount);

                    allLineHistories[dbName] = history;

                    Log.Information($"🌐 [{dbName}] HTML Report Generated. within {sw.Elapsed.TotalMilliseconds} msec ({sw.Elapsed.TotalSeconds:F1} sec)");
                    AdminJobProgress.Report("HTML 생성 완료", dbBase + (int)(dbSpan * 0.78), dbName, sw.Elapsed, dbName);
                    sw.Restart();

                    // Design Progress는 Pipe OpTime(TraceResult.Steps)이 필요하므로
                    // Excel OOM 완화를 위한 Steps 해제보다 먼저 실행한다.
                    // 순서가 바뀌면 LastPipeDesignAt이 전부 null → progress.html 진행중 설비가 0이 된다.
                    if (GlobalConfig.DesignProgressEnabled)
                    {
                        AdminJobProgress.Report("Design Progress", dbBase + (int)(dbSpan * 0.79), dbName, null, dbName);
                        var prog = progressEngine.Run(dbName, cache, allResults);
                        progressReports.Add(prog);
                        htmlGenerator.GenerateProgressReport(prog, baseOutputFolder);
                    }

                    AdminJobProgress.Report("Excel Export", dbBase + (int)(dbSpan * 0.80), dbName, null, dbName);

                    // Progress 완료 후 Steps는 Excel에 불필요 — 해제하여 Full_List OOM 완화
                    TraceResultExcelExporter.ReleaseTraceStepMemory(allResults);

                    // -------------------------------------------------------------
                    // [Step 4] Excel 저장 (Raw Data는 엑셀에만 존재)
                    // -------------------------------------------------------------
                                        // J. 엑셀 파일 저장 (요청 사항)
                    // [수정] Excel 폴더도 htmlOutputFolder 기준
                    string excelDir = Path.Combine(baseOutputFolder, "Excel");
                    if (!Directory.Exists(excelDir)) Directory.CreateDirectory(excelDir);

                    // J-1. 전체 리스트 — 대용량은 LargeXlsx 스트리밍 (EPPlus CellStore OOM 회피)
                    string fullExcelPath = Path.Combine(excelDir, $"{dbName}_Full_List.xlsx");
                    var fullExcelMetrics = TraceResultExcelExporter.Export(
                        fullExcelPath,
                        allResults,
                        excelPerfCompareEnabled,
                        excelPerfCompareThreshold,
                        TraceResultExcelExporter.TraceExcelWriterEngine.LargeXlsx);
                    LogExcelPerfMilestone(dbName, "Full_List", fullExcelMetrics);

                    var errorResults = allResults.Where(r => r.FailCount > 0).ToList();
                    if (errorResults.Any())
                    {
                        string errExcelPath = Path.Combine(excelDir, $"{dbName}_Error_Detail.xlsx");
                        var errorExcelMetrics = TraceResultExcelExporter.Export(
                            errExcelPath,
                            errorResults,
                            excelPerfCompareEnabled,
                            excelPerfCompareThreshold,
                            TraceResultExcelExporter.TraceExcelWriterEngine.EpPlus);
                        LogExcelPerfMilestone(dbName, "Error_Detail", errorExcelMetrics);
                    }

                                                                                // J-3. Hookup Validation (신규) - Excel만 생성
                    // [주의] HookupStats는 위에서 이미 reportDto에 할당됨
                    if (hookupValidationResults.Any())
                    {
                        var updateData = hookupValidationResults
                            .Where(x => x.Status != "OK" && 
                                        x.Status != "TAG_ERROR" &&
                                        x.Status != "DATA_ERROR" && 
                                        x.ExpectedHookup != "N/A(No ConnectorTag)")
                            .ToList();
                        string hookupExcelPath = Path.Combine(excelDir, $"{dbName}_Hookup_Validation.xlsx");
                        HookupExcelReportWriter.Export(hookupExcelPath, hookupValidationResults, updateData);
                    }

                    Log.Information($"📊 [{dbName}] Excel Report Generated. within {sw.Elapsed.TotalMilliseconds} msec ({sw.Elapsed.TotalSeconds:F1} sec)");
                    AdminJobProgress.Report("Excel Export 완료", dbBase + (int)(dbSpan * 0.95), dbName, sw.Elapsed, dbName);
                    sw.Restart();

                                        // K. Summary용 통계 수집
                    // [수정] Hookup 정보도 함께 수집
                    int hookupTotal = reportDto.HookupStats?.TotalObjects ?? 0;
                    int hookupOK = reportDto.HookupStats?.OK ?? 0;
                    hookupPassRate = hookupTotal > 0 ? (double)hookupOK / hookupTotal * 100.0 : 0;
                    
                    // POC Pass Rate (평균 계산용)
                    double pocPassRate = totalPoc > 0 ? (double)passPoc / totalPoc * 100.0 : 0;
                    // Quality Point: Hookup 데이터가 있을 때만 평균, 없으면 POC만 (0~100)
                    avgPassRate = hookupTotal > 0
                        ? (pocPassRate + hookupPassRate) / 2.0
                        : pocPassRate;
                    if (avgPassRate > 100) avgPassRate = 100;
                    if (avgPassRate < 0) avgPassRate = 0;

                    summaryStats.Add(new
                    {
                        DbName = dbName,
                        TotalEquip = totalEq,
                        PassEquipCount = passEq,
                        FailEquipCount = failEq,
                        TotalPoc = totalPoc,
                        PassPocCount = passPoc,
                        FailPocCount = failPoc,
                        TotalIssues = allResults.Sum(r => r.IssueList.Count),
                        HookupTotal = hookupTotal,
                        HookupPass = hookupOK,
                        HookupPassRate = Math.Round(hookupPassRate, 1),
                        AvgPassRate = Math.Round(avgPassRate, 1),
                        CheckRulesCount = checkRulesCount
                    });

                    }
                    catch (Exception dbEx)
                    {
                        Log.Error(dbEx, ">>> [DailyTraceJob] DB {Db} failed — {Action}", dbName,
                            GlobalConfig.AdminContinueOnDbFailure ? "continuing next line" : "aborting job");

                        AdminJobProgress.Report(
                            "DB 라인 실패",
                            Math.Min(99, dbBase + dbSpan),
                            dbEx.Message,
                            null,
                            dbName);

                        summaryStats.Add(new
                        {
                            DbName = dbName,
                            Failed = true,
                            Error = dbEx.Message
                        });

                        if (!GlobalConfig.AdminContinueOnDbFailure)
                            throw;
                    }
                    finally
                    {
                        manager?.ReleaseMemory();
                        nodes = null;
                        pocs = null;
                        pipes = null;
                        takeoffs = null;
                        dynamicProperties = null;

                        if (GlobalConfig.AdminGcBetweenDatabases)
                        {
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                            GC.WaitForPendingFinalizers();
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                            Log.Information("[Memory] GC after DB line: {Db}", dbName);
                        }
                    }

                } // End Loop (DB)

                AdminJobProgress.Report("Summary HTML", 96, "index.html", null, null);
                htmlGenerator.GenerateSummary(summaryStats, allLineHistories);

                // Rules 페이지: 카탈로그 + 일별 Rule 수/히트 히스토리
                AdminJobProgress.Report("Rules 페이지", 98, "rules.html", null, null);
                var rulesCatalog = ValidationRuleRegistry.GetCatalog(includeHits: true);
                htmlGenerator.GenerateRulesPage(rulesCatalog, baseOutputFolder);

                if (GlobalConfig.DesignProgressEnabled && progressReports.Count > 0)
                {
                    AdminJobProgress.Report("Progress 통합", 99, "progress.html", null, null);
                    htmlGenerator.GenerateProgressSummary(progressReports);
                }

                Log.Information($"✅ >>> [All Jobs Finished] Dashboard Updated at: {baseOutputFolder} within {totalSw.Elapsed.TotalMilliseconds} msec ({totalSw.Elapsed.TotalSeconds:F1} sec)");
                AdminJobProgress.Complete($"Dashboard: {baseOutputFolder}", totalSw.Elapsed);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ">>> [DailyTraceJob] Critical Failure.");
                AdminJobProgress.Fail("배치 실패", ex);
                throw new JobExecutionException(ex);
            }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _executionGate, 0);
            }
        }

        /// <summary>
        /// DB → BinaryCache(v5)만 갱신. Trace/Excel/HTML 없음 (Viewer 48h 게이트용).
        /// </summary>
        private static async Task RunCacheOnlyAsync(string connString, string[] targetDbs)
        {
            var binaryRepo = new BinaryCacheRepository();
            var totalSw = Stopwatch.StartNew();
            int dbCount = targetDbs.Length;
            int dbIndex = 0;

            AdminJobProgress.Report("캐시 갱신", 0, $"DB {dbCount}개", null, null);

            foreach (var dbName in targetDbs)
            {
                dbIndex++;
                int pctBase = ((dbIndex - 1) * 100) / dbCount;
                int pctSpan = 100 / dbCount;
                var sw = Stopwatch.StartNew();

                List<NodeInfo> nodes = null;
                List<PocData> pocs = null;
                List<PipeData> pipes = null;
                List<TakeoffData> takeoffs = null;
                List<DynamicPropertiesData> dynamicProperties = null;

                try
                {
                AdminJobProgress.Report("DB Query (Graph)", pctBase + pctSpan / 4, dbName, null, dbName);
                Log.Information("[CacheOnly] {Db} — loading graph from DB...", dbName);

                using (IDbConnection conn = new SqlConnection(connString))
                {
                    try
                    {
                        var bundle = await GraphQueryProviders.CreateFromConfig()
                            .LoadAsync(conn, dbName, null)
                            .ConfigureAwait(false);
                        nodes = bundle.Nodes;
                        pocs = bundle.Pocs;
                        pipes = bundle.Pipes;
                        takeoffs = bundle.Takeoffs;
                        dynamicProperties = bundle.HookupNumbers;

                        BimIdDiagnostics.AnalyzeAndLog("CacheOnly", dbName, CacheConstants.CurrentVersion, nodes);
                    }
                    catch (Exception dbEx)
                    {
                        Log.Error(dbEx, "[CacheOnly] DB {Db} failed", dbName);
                        AdminJobProgress.Report("DB Query 실패", pctBase, dbEx.Message, sw.Elapsed, dbName);
                        continue;
                    }
                }

                AdminJobProgress.Report("DB Query 완료", pctBase + pctSpan / 2,
                    $"nodes={nodes.Count}, pocs={pocs.Count}", sw.Elapsed, dbName);
                sw.Restart();

                AdminJobProgress.Report("Binary Cache 저장", pctBase + (pctSpan * 3 / 4), dbName, null, dbName);
                binaryRepo.SaveToCache(dbName, nodes, pocs, pipes, takeoffs, dynamicProperties);

                await SaveBimGroupCacheSafeAsync(connString, dbName).ConfigureAwait(false);
                await SaveTemplateEquipmentCacheSafeAsync(connString, dbName).ConfigureAwait(false);

                Log.Information("[CacheOnly] {Db} saved — {Ms} ms", dbName, sw.ElapsedMilliseconds);
                AdminJobProgress.Report("Binary Cache 저장 완료", pctBase + pctSpan,
                    GlobalConfig.GetCacheDirectory(dbName), sw.Elapsed, dbName);
                }
                catch (Exception dbEx)
                {
                    Log.Error(dbEx, "[CacheOnly] DB {Db} failed — continuing next line", dbName);
                    AdminJobProgress.Report("캐시 저장 실패", pctBase, dbEx.Message, sw.Elapsed, dbName);
                    if (!GlobalConfig.AdminContinueOnDbFailure)
                        throw;
                }
                finally
                {
                    nodes = null;
                    pocs = null;
                    pipes = null;
                    takeoffs = null;
                    dynamicProperties = null;
                    if (GlobalConfig.AdminGcBetweenDatabases)
                    {
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                        GC.WaitForPendingFinalizers();
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                    }
                }
            }

            AdminJobProgress.Complete(
                $"캐시 갱신 완료 ({dbCount} DB). Viewer는 GraphCache_* 폴더를 읽습니다.",
                totalSw.Elapsed);
        }

        /// <summary>
        /// ConfigurationGroupLog (InstanceID/Name/ParentID) → BimGroupCache_{db} v2.
        /// GraphCache 실패와 격리.
        /// </summary>
        private static async Task SaveBimGroupCacheSafeAsync(string connString, string dbName)
        {
            try
            {
                List<BimGroupRecord> records;
                using (IDbConnection conn = new SqlConnection(connString))
                {
                    var rows = await conn.QueryAsync<BimGroupRecord>(SqlQueries.GetSelectBimGroups(dbName))
                        .ConfigureAwait(false);
                    records = rows?.ToList() ?? new List<BimGroupRecord>();
                }

                new BimGroupCacheRepository().Save(dbName, records);
                Log.Information("[BimGroupCache][{Db}] Saved {Count} groups (v2 ParentID)", dbName, records.Count);
            }
            catch (Exception ex)
            {
                // GraphCache / Trace / Excel 계속 — Group sheet만 영향
                Log.Warning(ex, "[BimGroupCache][{Db}] Save failed (non-fatal)", dbName);
            }
        }

        /// <summary>
        /// Subdivision1 LIKE '3-%' Equipment → TemplateEqpCache_{db}.
        /// GraphCache에 넣지 않음 — 철거 설비정보 전용.
        /// </summary>
        private static async Task SaveTemplateEquipmentCacheSafeAsync(string connString, string dbName)
        {
            try
            {
                List<NodeInfo> nodes;
                using (IDbConnection conn = new SqlConnection(connString))
                {
                    var rows = await conn.QueryAsync<NodeInfo>(SqlQueries.GetSelectTemplateEquipments(dbName))
                        .ConfigureAwait(false);
                    nodes = rows?.ToList() ?? new List<NodeInfo>();
                }

                new TemplateEquipmentCacheRepository().Save(dbName, nodes);
                Log.Information("[TemplateEqpCache][{Db}] Saved {Count} 3-% nodes", dbName, nodes.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TemplateEqpCache][{Db}] Save failed (non-fatal)", dbName);
            }
        }

        private static void LogExcelPerfMilestone(
            string dbName,
            string exportName,
            TraceResultExcelExporter.ExcelExportMetrics metrics)
        {
            if (metrics == null || metrics.RowCount <= 0) return;

            string band = metrics.RowCount >= 100000
                ? "100K+"
                : metrics.RowCount >= 50000
                    ? "50K+"
                    : "<50K";

            if (metrics.LegacyElapsedMs.HasValue)
            {
                Log.Information(
                    "[ExcelPerf][{Band}][{Db}][{Name}] Engine={Engine}, Rows={Rows:N0}, TunedMs={TunedMs:N0}, LegacyMs={LegacyMs:N0}, Speedup={Speedup:0.00}x",
                    band, dbName, exportName, metrics.WriterEngine, metrics.RowCount, metrics.TunedElapsedMs,
                    metrics.LegacyElapsedMs.Value, metrics.SpeedupX ?? 0);
            }
            else
            {
                Log.Information(
                    "[ExcelPerf][{Band}][{Db}][{Name}] Engine={Engine}, Rows={Rows:N0}, TunedMs={TunedMs:N0}, TunedRps={Rps:N0}",
                    band, dbName, exportName, metrics.WriterEngine, metrics.RowCount, metrics.TunedElapsedMs, metrics.TunedRowsPerSec);
            }
        }

}

        // [Data Model 확장]
    public class DailyStat
    {
        public string Date { get; set; }
        // 설비 기준
        public int TotalEq { get; set; }
        public int PassEq { get; set; }
        public int FailEq { get; set; }
        public double RateEq { get; set; } // Eq Pass Rate
        // POC 기준
        public int TotalPoc { get; set; }
        public int PassPoc { get; set; }
        public int FailPoc { get; set; }
        public double RatePoc { get; set; } // POC Pass Rate
        // Issue Count
        public int IssuePocCount { get; set; }
        // [신규] Hookup 관련
        public int HookupErrorCount { get; set; }
        public double AvgPassRate { get; set; }
        /// <summary>등록된 Check Rules(IValidationRule) 수량.</summary>
        public int CheckRulesCount { get; set; }
    }

    // [New] 쿼리 매핑용 DTO 클래스
    public class EquipmentInfo
    {
        public string Tag { get; set; }
        public string ConstructionCode { get; set; } // a.constructioncode		
        public string MainName { get; set; } // [New] Main 설비 이름
        public string Length { get; set; }   // [New] Length (DB에서 가져온 값)
    }

                // 2. 숫자 포맷팅 확장 메서드 (Formatter Helper)
    public static class NumberFormatter
    {
        public static string ToMetric(this double value)
        {
            if (value >= 1000000) return (value / 1000000D).ToString("0.#") + "M";
            if (value >= 1000) return (value / 1000D).ToString("0.#") + "K";
            return value.ToString("#,0");
        }
    }

    
}
