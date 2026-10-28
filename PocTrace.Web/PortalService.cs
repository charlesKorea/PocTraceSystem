using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JLic;
using PocTrace.Core.Composition;
using PocTrace.Core.Engine.Reporting;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Licensing;
using PocTrace.Core.Models;
using PocTrace.Core.Models.ReverseTrace;
using PocTrace.Demolition;
using PocTrace.Demolition.Export;
using PocTrace.Demolition.Input;
using PocTrace.Demolition.Jobs;
using PocTrace.NozzleTrace.Input;
using PocTrace.NozzleTrace.Jobs;
using Newtonsoft.Json;
using Serilog;

namespace PocTrace.Web
{
    /// <summary>
    /// Viewer MainViewModel의 로드 / 단건 Trace / 배치 / Excel을 HTTP 작업으로 옮긴 것.
    /// 캐시 파일은 읽기만 하고, Excel은 사용자 PC의 LocalAppData에 둔다.
    /// </summary>
    internal sealed class PortalService
    {
        public const int MaxBatchTags = 300;
        public const int MaxDemolitionRows = 2000;
        public const int MaxSummaryRows = 500;
        public const int MaxIssueRows = 200;

        private readonly PocTraceAppServices _services;
        private readonly LicenseResult _license;
        private readonly object _gate = new object();
        private readonly Dictionary<string, JobRecord> _jobs = new Dictionary<string, JobRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DemolitionDraft> _drafts = new Dictionary<string, DemolitionDraft>(StringComparer.OrdinalIgnoreCase);

        private string _loadedDb;
        private DateTime _loadedSavedAt;

        public PortalService(PocTraceAppServices services, LicenseResult license)
        {
            _services = services;
            _license = license ?? LicenseResult.Fail(LicenseErrorCode.FileMissing, "라이선스가 없습니다.");
            Directory.CreateDirectory(WorkRoot);
        }

        public bool HasLicense
        {
            get { return _license != null && _license.Success; }
        }

        public bool CanUseExtraTools
        {
            get { return PocTraceLicense.CanUseExtraTools(_license); }
        }

        public object GetLicenseView()
        {
            return PocTraceLicense.ClientView(_license);
        }

        public object GetPrefs()
        {
            return WebLocalStore.ReadPrefs();
        }

        public void SavePrefs(string lang, string theme)
        {
            WebLocalStore.WritePrefs(lang, theme);
        }

        public object GetHistory()
        {
            string user = _license == null ? "" : (_license.UserName ?? "");
            return WebLocalStore.Recent(user);
        }

        private void RememberInspection(string method, string line, IEnumerable<string> targets)
        {
            try
            {
                string user = _license == null ? "" : (_license.UserName ?? "");
                WebLocalStore.Append(user, method, line, targets);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Web] inspection history");
            }
        }

        public static string WorkRoot =>
            PocTraceLocalPaths.AppDirectory(PocTraceLocalPaths.Web);

        public object GetLines()
        {
            var gate = CacheFreshnessGuard.EvaluateViewerStartup();
            var lines = new List<object>();
            foreach (string db in GlobalConfig.TargetDatabases)
            {
                DateTime savedAt;
                int version;
                string source;
                bool found = CacheFreshnessGuard.TryGetSavedAt(db, out savedAt, out version, out source);
                double age = found ? (DateTime.Now - savedAt).TotalHours : -1;
                lines.Add(new
                {
                    db,
                    found,
                    savedAt = found ? savedAt.ToString("yyyy-MM-dd HH:mm") : null,
                    ageHours = found ? Math.Round(age, 1) : (double?)null,
                    version = found ? version : (int?)null,
                    allowed = found && age <= GlobalConfig.CacheMaxAgeHours,
                    loaded = string.Equals(_loadedDb, db, StringComparison.OrdinalIgnoreCase)
                });
            }

            return new
            {
                allowed = gate.Allowed,
                message = gate.Message,
                sharedCachePath = GlobalConfig.SharedCachePath,
                dailyReportPath = GlobalConfig.DailyReportPath,
                cacheMaxAgeHours = GlobalConfig.CacheMaxAgeHours,
                loadedDb = _loadedDb,
                lines,
                details = gate.Details,
                limits = new
                {
                    maxBatchTags = MaxBatchTags,
                    maxNozzleKeys = MaxBatchTags,
                    maxDemolitionRows = MaxDemolitionRows,
                    maxSummaryRows = MaxSummaryRows,
                    maxIssueRows = MaxIssueRows
                }
            };
        }

        public string StartCacheLoad(string db)
        {
            db = RequireDb(db);
            DateTime savedAt;
            int version;
            string source;
            bool fresh = CacheFreshnessGuard.TryGetSavedAt(db, out savedAt, out version, out source);
            bool ready = fresh
                && string.Equals(_loadedDb, db, StringComparison.OrdinalIgnoreCase)
                && _loadedSavedAt == savedAt
                && _services.CacheManager.IsInitialized;
            JobRecord job = BeginJob("load", db, db);
            if (ready)
            {
                lock (_gate)
                {
                    job.State = "done";
                    job.Percent = 100;
                    job.Message = Msg(
                        db + " 캐시가 이미 메모리에 있습니다. 태그를 입력하세요.",
                        db + " 缓存已在内存中。请输入标签。");
                }
                return job.Id;
            }

            Task.Run(() => RunCacheLoad(job));
            return job.Id;
        }

        public string StartTrace(string db, string tag)
        {
            db = RequireDb(db);
            tag = (tag ?? "").Trim();
            if (tag.Length == 0)
                throw new PortalInputException(Msg("태그를 입력하세요.", "请输入标签。"));
            if (tag.Length > 200)
                throw new PortalInputException(Msg("태그가 너무 깁니다.", "标签过长。"));

            JobRecord job = BeginJob("trace", db, tag);
            Task.Run(() => RunSingle(job, tag));
            return job.Id;
        }

        public string StartBatch(string db, string text)
        {
            db = RequireDb(db);
            var tags = SplitTags(text);
            if (tags.Count == 0)
                throw new PortalInputException(Msg("배치 태그를 입력하세요.", "请输入批量标签。"));
            if (tags.Count > MaxBatchTags)
                throw new PortalInputException(Msg(
                    "배치는 한 번에 " + MaxBatchTags + "개까지입니다. 지금은 " + tags.Count + "개입니다.",
                    "批量一次最多 " + MaxBatchTags + " 个。当前 " + tags.Count + " 个。"));

            JobRecord job = BeginJob("batch", db, tags.Count + " tags");
            Task.Run(() => RunBatch(job, tags));
            return job.Id;
        }

        public JobRecord FindJob(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            lock (_gate)
            {
                JobRecord job;
                return _jobs.TryGetValue(id.Trim(), out job) ? job : null;
            }
        }

        private void RunSingle(JobRecord job, string tag)
        {
            try
            {
                var token = TokenOf(job);
                EnsureLoaded(job.Db, job);
                token.ThrowIfCancellationRequested();
                job.Message = Msg("Trace 중: ", "追踪中: ") + tag;
                job.Percent = 40;

                var engine = _services.CreateTraceEngine();
                var results = engine.RunTrace(tag, 0, GlobalConfig.EnableDetailedDebug, token, includePartialTagMatch: true)
                    ?? new List<TraceResult>();

                token.ThrowIfCancellationRequested();
                job.Message = Msg("Excel 저장 중", "正在保存 Excel");
                job.Percent = 80;
                var summaries = EquipmentSummaryBuilder.Build(results);
                var issues = new List<TraceIssueView>();
                bool truncated = false;
                AppendIssues(results, issues, ref truncated);
                string path = Path.Combine(JobDirectory(job), SafeFile(job.Db + "_" + tag) + ".xlsx");
                _services.ExcelReport.SaveSingleFileReport(path, results, _services.CacheManager);

                lock (_gate)
                {
                    job.Summaries = summaries;
                    job.Issues = issues;
                    job.IssuesTruncated = truncated;
                    if (truncated)
                        job.Warnings.Add(IssueCapWarning());
                    job.ExcelPath = path;
                    job.ResultCount = results.Count;
                    job.Percent = 100;
                    job.State = "done";
                    job.Message = Msg(
                        "완료. 설비 " + summaries.Count + "건, 결과 " + results.Count + "행",
                        "完成。设备 " + summaries.Count + " 台，结果 " + results.Count + " 行");
                }
                RememberInspection("trace", job.Db, new[] { tag });
            }
            catch (Exception ex) when (IsCancelled(ex))
            {
                MarkCancelled(job);
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
        }

        private void RunBatch(JobRecord job, List<string> tags)
        {
            string outputDir = null;
            var summaries = new List<EquipmentTraceSummary>();
            var issues = new List<TraceIssueView>();
            bool truncated = false;
            try
            {
                var token = TokenOf(job);
                EnsureLoaded(job.Db, job);
                outputDir = JobDirectory(job);
                int done = 0;

                foreach (string tag in tags)
                {
                    token.ThrowIfCancellationRequested();
                    job.Message = Msg("Trace ", "追踪 ") + (done + 1) + "/" + tags.Count + " " + tag;
                    job.Percent = 10 + (int)(70.0 * done / tags.Count);
                    try
                    {
                        var engine = _services.CreateTraceEngine();
                        var results = engine.RunTrace(tag, 0, GlobalConfig.EnableDetailedDebug, token, includePartialTagMatch: true);
                        if (results == null || results.Count == 0)
                        {
                            Log.Warning("[WebBatch] 결과 없음: {Tag}", tag);
                        }
                        else
                        {
                            AppendIssues(results, issues, ref truncated);
                            var part = EquipmentSummaryBuilder.Build(results);
                            summaries.AddRange(part);
                            var first = part.FirstOrDefault();
                            string name = SafeFile(job.Db + "_" + tag
                                + "_" + (first == null ? 0 : first.EquipmentCount)
                                + "_" + (first == null ? 0 : first.TotalPocCount));
                            _services.ExcelReport.SaveSingleFileReport(
                                Path.Combine(outputDir, name + ".xlsx"),
                                results,
                                _services.CacheManager);
                        }
                    }
                    catch (Exception ex) when (IsCancelled(ex))
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[WebBatch] {Tag}", tag);
                        job.Warnings.Add(tag + ": " + ex.Message);
                    }

                    done++;
                }

                token.ThrowIfCancellationRequested();
                string summaryPath = null;
                if (summaries.Count > 0)
                {
                    job.Message = Msg("요약 Excel 저장 중", "正在保存汇总 Excel");
                    summaryPath = Path.Combine(outputDir, "Summary.xlsx");
                    _services.ExcelReport.SaveBatchSummaryReport(summaryPath, summaries);
                }

                lock (_gate)
                {
                    job.Summaries = summaries.OrderBy(s => s.EquipmentTag).ToList();
                    job.Issues = issues;
                    job.IssuesTruncated = truncated;
                    if (truncated)
                        job.Warnings.Add(IssueCapWarning());
                    job.ExcelPath = summaryPath;
                    job.OutputDirectory = outputDir;
                    job.ResultCount = summaries.Count;
                    job.Percent = 100;
                    job.State = "done";
                    job.Message = Msg(
                        "배치 완료. 요약 " + summaries.Count + "건",
                        "批量完成。摘要 " + summaries.Count + " 项");
                }
                RememberInspection("batch", job.Db, tags);
            }
            catch (Exception ex) when (IsCancelled(ex))
            {
                lock (_gate)
                {
                    job.Summaries = summaries.OrderBy(s => s.EquipmentTag).ToList();
                    job.Issues = issues;
                    job.IssuesTruncated = truncated;
                    job.OutputDirectory = outputDir;
                    job.ResultCount = summaries.Count;
                    job.State = "cancelled";
                    job.Error = null;
                    job.Message = Msg("작업을 취소했습니다.", "已取消作业。");
                }
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
        }

        private void EnsureLoaded(string db, JobRecord job)
        {
            string block = FreshnessBlock(db);
            if (block != null)
                throw new InvalidOperationException(block);

            DateTime savedAt;
            int version;
            string source;
            if (!CacheFreshnessGuard.TryGetSavedAt(db, out savedAt, out version, out source))
                throw new InvalidOperationException(Msg("캐시 시각을 읽지 못했습니다: ", "无法读取缓存时间: ") + db);

            bool same = string.Equals(_loadedDb, db, StringComparison.OrdinalIgnoreCase)
                && _services.CacheManager.IsInitialized
                && _loadedSavedAt == savedAt;
            if (same)
                return;

            job.Message = Msg(db + " 캐시를 메모리에 올리는 중", "正在把 " + db + " 缓存载入内存");
            job.Percent = 10;
            var progress = new Progress<string>(msg => job.Message = msg);
            var loaded = _services.GraphLoader.LoadGraphDataAsync(db, progress).GetAwaiter().GetResult();
            if (loaded.Nodes == null)
                throw new InvalidOperationException(Msg(
                    db + " 캐시를 읽지 못했습니다. Admin 캐시 갱신 여부를 확인하세요.",
                    "未能读取 " + db + " 缓存。请确认 Admin 是否已更新缓存。"));

            var utMaps = _services.GraphLoader.LoadUtMapping();
            var nodes = loaded.Nodes;
            var pocs = loaded.Pocs;
            var pipes = loaded.Pipes;
            var takeoffs = loaded.Takeoffs;
            var dyn = loaded.HookupNumbers;
            loaded = default;
            _services.CacheManager.Initialize(
                db, nodes, pocs, pipes, takeoffs, dyn, utMaps, buildReverseGraphIndex: false, progress: progress);
            nodes = null;
            pocs = null;
            pipes = null;
            takeoffs = null;
            dyn = null;
            _services.CacheManager.BuildReverseIndexAfterInputsReleased(progress);

            _loadedDb = db;
            _loadedSavedAt = savedAt;
            Log.Information("[Web] cache loaded {Db} savedAt={SavedAt:yyyy-MM-dd HH:mm} v{Version}", db, savedAt, version);
        }

        private void RunCacheLoad(JobRecord job)
        {
            try
            {
                EnsureLoaded(job.Db, job);
                TokenOf(job).ThrowIfCancellationRequested();
                lock (_gate)
                {
                    job.State = "done";
                    job.Percent = 100;
                    job.ResultCount = 0;
                    job.Message = Msg(
                        job.Db + " 캐시를 메모리에 올렸습니다. 이제 태그를 입력하세요.",
                        "已将 " + job.Db + " 缓存载入内存。现在可以输入标签。");
                }
            }
            catch (Exception ex) when (IsCancelled(ex))
            {
                MarkCancelled(job);
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
        }

        public object PreviewDemolitionText(string text)
        {
            var rows = ParseDemolition(text);
            if (rows.Count == 0)
                throw new PortalInputException(DemolitionUiText.Kz(
                    "철거 대상을 입력하세요. 예: X1,EQP-001",
                    "请输入拆除对象。例: X1,EQP-001"));
            return StoreDraft(rows);
        }

        public object PreviewDemolitionUpload(byte[] bytes, string fileName)
        {
            if (bytes == null || bytes.Length == 0)
                throw new PortalInputException(DemolitionUiText.Kz("파일이 비어 있습니다.", "文件为空。"));

            string ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xlsm")
                throw new PortalInputException(DemolitionUiText.Kz(
                    "Excel 파일만 올릴 수 있습니다. (xlsx, xlsm)",
                    "只能上传 Excel 文件。(xlsx, xlsm)"));

            string dir = Path.Combine(WorkRoot, "uploads");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
            File.WriteAllBytes(path, bytes);
            try
            {
                var loaded = DemolitionExcelReader.Load(path);
                if (!string.IsNullOrEmpty(loaded.ErrorMessage) && (loaded.Rows == null || loaded.Rows.Count == 0))
                    throw new PortalInputException(loaded.ErrorMessage);
                return StoreDraft(loaded.Rows ?? new List<DemolitionInputRow>());
            }
            finally
            {
                try { File.Delete(path); } catch { /* 임시 파일 */ }
            }
        }

        public string StartDemolition(string draftId, string text)
        {
            List<DemolitionInputRow> source;
            if (!string.IsNullOrWhiteSpace(draftId))
            {
                source = CopyDraft(draftId.Trim());
            }
            else
            {
                source = ParseDemolition(text);
                if (source.Count == 0)
                    throw new PortalInputException(DemolitionUiText.Kz(
                        "철거 대상을 입력하세요. 예: X1,EQP-001",
                        "请输入拆除对象。例: X1,EQP-001"));
                EnsureRowCap(source);
            }

            var rows = source.Select(CloneRow).ToList();
            var valid = rows.Where(r => r.IsValid).ToList();
            if (valid.Count == 0)
                throw new PortalInputException(DemolitionUiText.MsgNoValidEqp);

            string line = valid[0].MappedLine;
            JobRecord job = BeginJob("demolition", line, valid.Count + " eqp");
            job.InputRows = SnapshotRows(rows);
            Task.Run(() => RunDemolition(job, rows));
            return job.Id;
        }

        public bool CancelJob(string id)
        {
            lock (_gate)
            {
                JobRecord job;
                if (string.IsNullOrWhiteSpace(id) || !_jobs.TryGetValue(id.Trim(), out job))
                    return false;
                if (job.State != "running")
                    return true;
                if (job.CancelSource == null)
                    throw new PortalInputException(DemolitionUiText.Kz(
                        "이 작업은 취소할 수 없습니다.",
                        "此作业无法取消。"));
                job.Message = DemolitionUiText.Kz("취소 요청됨", "已请求取消");
                job.CancelSource.Cancel();
                return true;
            }
        }

        public object ClientView(string id)
        {
            lock (_gate)
            {
                JobRecord job;
                if (string.IsNullOrWhiteSpace(id) || !_jobs.TryGetValue(id.Trim(), out job))
                    return null;

                return new
                {
                    id = job.Id,
                    kind = job.Kind,
                    db = job.Db,
                    title = job.Title,
                    state = job.State,
                    percent = job.Percent,
                    message = job.Message,
                    error = job.Error,
                    resultCount = job.ResultCount,
                    excelReady = !string.IsNullOrEmpty(job.ExcelPath) && File.Exists(job.ExcelPath),
                    htmlReady = !string.IsNullOrEmpty(job.HtmlPath) && File.Exists(job.HtmlPath),
                    outputDirectory = job.OutputDirectory,
                    warnings = job.Warnings.ToList(),
                    summaries = job.Summaries,
                    preview = job.Preview.ToList(),
                    etaSeconds = job.EtaSeconds,
                    linesDone = job.LinesDone,
                    linesTotal = job.LinesTotal,
                    eqpDone = job.EqpDone,
                    eqpTotal = job.EqpTotal,
                    inputRows = job.InputRows == null ? null : job.InputRows.ToList(),
                    summaryTable = job.SummaryTable,
                    summaryTruncated = job.SummaryTruncated,
                    sheetNames = job.SheetNames,
                    sheetTotals = job.SheetTotals,
                    eqpTraced = job.EqpTraced,
                    eqpMissing = job.EqpMissing,
                    eqpNoResult = job.EqpNoResult,
                    eqpErrorCount = job.EqpErrorCount,
                    elapsedSeconds = job.ElapsedSeconds,
                    cancellable = job.CancelSource != null && job.State == "running",
                    issues = job.Issues ?? new List<TraceIssueView>(),
                    issuesTruncated = job.IssuesTruncated,
                    nozzleRows = job.NozzleRows ?? new List<NozzleRowView>()
                };
            }
        }

        public object ListJobs()
        {
            lock (_gate)
            {
                DropOldJobs();
                var jobs = _jobs.Values
                    .OrderByDescending(j => j.CreatedUtc)
                    .Take(12)
                    .Select(j => new
                    {
                        id = j.Id,
                        kind = j.Kind,
                        db = j.Db,
                        title = j.Title,
                        state = j.State,
                        created = j.CreatedUtc.ToString("yyyy-MM-dd HH:mm"),
                        message = j.Message,
                        percent = j.Percent,
                        excelReady = !string.IsNullOrEmpty(j.ExcelPath) && File.Exists(j.ExcelPath),
                        htmlReady = !string.IsNullOrEmpty(j.HtmlPath) && File.Exists(j.HtmlPath)
                    })
                    .ToList();
                return new { jobs };
            }
        }

        public string StartNozzle(string db, string keyKind, string text)
        {
            db = RequireDb(db);
            var kind = ParseNozzleKind(keyKind);
            var keys = SplitTags(text);
            if (keys.Count == 0)
                throw new PortalInputException(Msg("노즐 키를 입력하세요.", "请输入喷嘴键。"));
            if (keys.Count > MaxBatchTags)
                throw new PortalInputException(Msg(
                    "한 번에 " + MaxBatchTags + "개까지입니다.",
                    "一次最多 " + MaxBatchTags + " 个。"));

            var rows = new List<NozzleTraceInputRow>();
            int n = 1;
            foreach (string key in keys)
            {
                rows.Add(new NozzleTraceInputRow
                {
                    RowNumber = n++,
                    DbName = db,
                    KeyKind = kind,
                    KeyValue = key,
                    LoadStatus = "OK"
                });
            }

            JobRecord job = BeginJob("nozzle", db, keys.Count + " keys");
            Task.Run(() => RunNozzle(job, rows));
            return job.Id;
        }

        private void RunDemolition(JobRecord job, List<DemolitionInputRow> rows)
        {
            var valid = rows.Where(r => r.IsValid).ToList();
            try
            {
                var progress = new Progress<DemolitionJobProgress>(p => ApplyDemolitionProgress(job, p, rows));
                var result = new DemolitionInspectionJob()
                    .RunAsync(valid, progress, job.CancelSource.Token)
                    .GetAwaiter()
                    .GetResult();
                if (result == null || !result.Success)
                    throw new InvalidOperationException(result == null
                        ? Msg("철거 검수 결과가 없습니다.", "没有拆除检查结果。")
                        : (result.ErrorMessage ?? Msg("철거 검수 실패", "拆除检查失败")));

                string dir = JobDirectory(job);
                string path = Path.Combine(dir, "Demolition.xlsx");
                var exported = DemolitionExcelExporter.Export(path, result);
                if (exported == null || !exported.Success)
                    throw new InvalidOperationException(exported == null
                        ? Msg("Excel 저장 실패", "Excel 保存失败")
                        : (exported.ErrorMessage ?? Msg("Excel 저장 실패", "Excel 保存失败")));

                string htmlPath = Path.Combine(dir, "Demolition.html");
                DemolitionHtmlSummaryWriter.Write(htmlPath, result);

                var table = new List<string[]>();
                var src = result.SummaryRows ?? new List<object[]>();
                int take = Math.Min(src.Count, MaxSummaryRows + 1);
                for (int i = 0; i < take; i++)
                {
                    var cells = src[i] ?? new object[0];
                    table.Add(cells.Select(c => c == null ? "" : Convert.ToString(c)).ToArray());
                }

                int[] totals = new int[EqpSheetCounter.SheetCount];
                if (result.SummaryCounter != null)
                {
                    for (int i = 0; i < totals.Length; i++)
                        totals[i] = result.SummaryCounter.TotalForSheet(i);
                }

                lock (_gate)
                {
                    job.ExcelPath = exported.OutputPath;
                    job.HtmlPath = htmlPath;
                    job.ResultCount = result.EqpAttempted;
                    job.Warnings.AddRange(result.Warnings ?? new List<string>());
                    if (src.Count > MaxSummaryRows + 1)
                    {
                        job.SummaryTruncated = true;
                        job.Warnings.Add(DemolitionUiText.Kz(
                            "종합 표는 화면에서 " + MaxSummaryRows + "행까지 보여 줍니다. 전체는 Excel과 HTML에 있습니다.",
                            "综合表在画面上最多显示 " + MaxSummaryRows + " 行。完整内容在 Excel 和 HTML 中。"));
                    }
                    job.SummaryTable = table;
                    job.SheetNames = EqpSheetCounter.DetailSheetNames.ToArray();
                    job.SheetTotals = totals;
                    job.EqpTraced = result.EqpTraced;
                    job.EqpMissing = result.EqpMissingTag;
                    job.EqpNoResult = result.EqpNoResult;
                    job.EqpErrorCount = result.EqpError;
                    job.ElapsedSeconds = Math.Round(result.Elapsed.TotalSeconds, 1);
                    job.InputRows = SnapshotRows(rows);
                    job.Percent = 100;
                    job.EtaSeconds = 0;
                    job.State = "done";
                    job.Message = DemolitionUiText.Kz(
                        "철거 검수 완료. 설비 " + result.EqpAttempted + "건",
                        "拆除检查完成。设备 " + result.EqpAttempted + " 台");
                }
                RememberInspection(
                    "demolition",
                    job.Db,
                    rows.Where(r => r != null && r.IsValid).Select(r => r.EqpId));
            }
            catch (Exception ex) when (IsCancelled(ex))
            {
                lock (_gate)
                {
                    job.State = "cancelled";
                    job.Error = null;
                    job.InputRows = SnapshotRows(rows);
                    job.Message = DemolitionUiText.Kz("철거 검수를 취소했습니다.", "已取消拆除检查。");
                }
            }
            catch (Exception ex)
            {
                Fail(job, ex);
                lock (_gate)
                {
                    job.InputRows = SnapshotRows(rows);
                }
            }
        }

        private void ApplyDemolitionProgress(JobRecord job, DemolitionJobProgress progress, List<DemolitionInputRow> rows)
        {
            if (progress == null)
                return;
            var snap = SnapshotRows(rows);
            lock (_gate)
            {
                job.Percent = progress.Percent;
                if (!string.IsNullOrEmpty(progress.Message))
                    job.Message = progress.Message;
                job.EtaSeconds = progress.EtaSeconds;
                job.LinesDone = progress.LinesDone;
                job.LinesTotal = progress.LinesTotal;
                job.EqpDone = progress.EqpDone;
                job.EqpTotal = progress.EqpTotal;
                job.InputRows = snap;
            }
        }

        private void RunNozzle(JobRecord job, List<NozzleTraceInputRow> rows)
        {
            try
            {
                var token = TokenOf(job);
                var progress = new Progress<NozzleTraceJobProgress>(p =>
                {
                    if (p == null) return;
                    job.Percent = (int)Math.Round(Math.Max(0, Math.Min(1, p.Fraction)) * 100);
                    job.Message = p.Message;
                });
                var result = new NozzleTraceJob().RunAsync(rows, progress, token).GetAwaiter().GetResult();
                if (result == null)
                    throw new InvalidOperationException(Msg("노즐 추적 결과가 없습니다.", "没有喷嘴追踪结果。"));
                if (!result.Success && string.IsNullOrEmpty(result.OutputExcelPath))
                    throw new InvalidOperationException(result.ErrorMessage ?? Msg("노즐 추적 실패", "喷嘴追踪失败"));

                string excel = result.OutputExcelPath;
                if (!string.IsNullOrEmpty(excel) && File.Exists(excel))
                {
                    string dest = Path.Combine(JobDirectory(job), Path.GetFileName(excel));
                    if (!string.Equals(Path.GetFullPath(excel), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                        File.Copy(excel, dest, true);
                    excel = dest;
                }

                var nozzleRows = BuildNozzleRows(rows, result);
                lock (_gate)
                {
                    job.ExcelPath = excel;
                    job.ResultCount = result.TracePathCount;
                    job.NozzleRows = nozzleRows;
                    job.Warnings.AddRange(result.Warnings ?? new List<string>());
                    job.Percent = 100;
                    job.State = result.Success ? "done" : "error";
                    job.Message = result.Success
                        ? Msg(
                            "노즐 추적 완료. 경로 " + result.TracePathCount + "건",
                            "喷嘴追踪完成。路径 " + result.TracePathCount + " 条")
                        : (result.ErrorMessage ?? Msg("노즐 추적 실패", "喷嘴追踪失败"));
                    job.Error = result.Success ? null : job.Message;
                }
                if (result.Success)
                {
                    RememberInspection(
                        "nozzle",
                        job.Db,
                        rows.Where(r => r != null).Select(r => r.KeyValue));
                }
            }
            catch (Exception ex) when (IsCancelled(ex))
            {
                lock (_gate)
                {
                    job.NozzleRows = BuildNozzleRows(rows, null);
                }
                MarkCancelled(job);
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
        }

        private static List<DemolitionInputRow> ParseDemolition(string text)
        {
            var rows = new List<DemolitionInputRow>();
            int n = 1;
            bool headerChecked = false;
            foreach (string raw in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string[] parts = line.IndexOf(',') >= 0 || line.IndexOf('\t') >= 0
                    ? line.Split(new[] { ',', '\t' })
                    : line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = (parts[i] ?? "").Trim();
                int end = parts.Length;
                while (end > 0 && parts[end - 1].Length == 0)
                    end--;
                if (end != parts.Length)
                {
                    var trimmed = new string[end];
                    Array.Copy(parts, trimmed, end);
                    parts = trimmed;
                }

                if (!headerChecked)
                {
                    headerChecked = true;
                    if (IsDemolitionHeader(parts))
                        continue;
                }

                var row = new DemolitionInputRow
                {
                    RowNumber = n++,
                    TraceStatus = "-"
                };

                if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    row.SiteRaw = line;
                    row.LoadStatus = "InvalidFormat";
                    rows.Add(row);
                    continue;
                }

                string site = parts[0];
                string eqp = parts[parts.Length - 1];
                string process = parts.Length >= 3 ? parts[1] : "";
                string code = parts.Length >= 4 ? parts[2] : "";
                row.SiteRaw = site;
                row.Process = process;
                row.ConstructionCode = code;
                row.EqpId = eqp;

                string mapped;
                if (string.IsNullOrWhiteSpace(eqp))
                {
                    row.LoadStatus = "InvalidEqpId";
                }
                else if (!SiteLineMapper.TryMap(site, out mapped))
                {
                    row.MappedLine = "";
                    row.LoadStatus = "InvalidSite";
                }
                else
                {
                    row.MappedLine = mapped;
                    row.LoadStatus = "OK";
                    row.TraceStatus = "Pending";
                }

                rows.Add(row);
            }

            EnsureRowCap(rows);
            return rows;
        }

        private static bool IsDemolitionHeader(string[] parts)
        {
            if (parts == null || parts.Length < 2)
                return false;
            string site = NormHeader(parts[0]);
            string eqp = NormHeader(parts[parts.Length - 1]);
            bool siteOk = site == "site" || site == "라인" || site == "line" || site == "db" || site == "project";
            bool eqpOk = eqp == "eqpid" || eqp == "eqp" || eqp == "tag" || eqp == "설비" || eqp == "설비id";
            return siteOk && eqpOk;
        }

        private static string NormHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return new string(value.Trim().ToLowerInvariant()
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-' && ch != ':')
                .ToArray());
        }

        private static void EnsureRowCap(List<DemolitionInputRow> rows)
        {
            if (rows != null && rows.Count > MaxDemolitionRows)
                throw new PortalInputException(DemolitionUiText.Kz(
                    "한 번에 " + MaxDemolitionRows + "행까지입니다. 지금은 " + rows.Count + "행입니다.",
                    "一次最多 " + MaxDemolitionRows + " 行。当前 " + rows.Count + " 行。"));
        }

        private object StoreDraft(List<DemolitionInputRow> rows)
        {
            rows = rows ?? new List<DemolitionInputRow>();
            EnsureRowCap(rows);
            var draft = new DemolitionDraft
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                Rows = rows,
                CreatedUtc = DateTime.UtcNow
            };
            lock (_gate)
            {
                var dead = _drafts.Values
                    .Where(d => d.CreatedUtc < DateTime.UtcNow.AddHours(-2))
                    .Select(d => d.Id)
                    .ToList();
                foreach (string id in dead)
                    _drafts.Remove(id);
                _drafts[draft.Id] = draft;
            }
            return DescribeDraft(draft);
        }

        private List<DemolitionInputRow> CopyDraft(string id)
        {
            lock (_gate)
            {
                DemolitionDraft draft;
                if (!_drafts.TryGetValue(id, out draft))
                    throw new PortalInputException(DemolitionUiText.Kz(
                        "검증 결과가 없습니다. 다시 검증하세요.",
                        "没有验证结果。请重新验证。"));
                return draft.Rows.ToList();
            }
        }

        private static object DescribeDraft(DemolitionDraft draft)
        {
            var rows = draft.Rows ?? new List<DemolitionInputRow>();
            var valid = rows.Where(r => r.IsValid).ToList();
            int distinct = valid
                .Select(r => ((r.MappedLine ?? "") + "|" + (r.EqpId ?? "")).ToUpperInvariant())
                .Distinct()
                .Count();
            var byLine = valid
                .GroupBy(r => r.MappedLine ?? "", StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    line = g.Key,
                    count = g.Select(x => x.EqpId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                })
                .ToList();

            return new
            {
                draftId = draft.Id,
                total = rows.Count,
                valid = valid.Count,
                invalid = rows.Count - valid.Count,
                distinctEqp = distinct,
                byLine,
                caches = byLine.Select(x => CacheInfo(x.line)).ToList(),
                rows = rows.Select(ToRowView).ToList()
            };
        }

        private static object CacheInfo(string db)
        {
            DateTime savedAt;
            int version;
            string source;
            bool found = CacheFreshnessGuard.TryGetSavedAt(db, out savedAt, out version, out source);
            double age = found ? (DateTime.Now - savedAt).TotalHours : -1;
            return new
            {
                db,
                found,
                savedAt = found ? savedAt.ToString("yyyy-MM-dd HH:mm") : null,
                ageHours = found ? Math.Round(age, 1) : (double?)null,
                allowed = found && age <= GlobalConfig.CacheMaxAgeHours
            };
        }

        private static DemolitionInputRow CloneRow(DemolitionInputRow row)
        {
            return new DemolitionInputRow
            {
                RowNumber = row.RowNumber,
                SiteRaw = row.SiteRaw,
                MappedLine = row.MappedLine,
                Process = row.Process,
                ConstructionCode = row.ConstructionCode,
                EqpId = row.EqpId,
                LoadStatus = row.LoadStatus,
                TraceStatus = row.IsValid ? "Pending" : (row.TraceStatus ?? "-")
            };
        }

        private static List<DemolitionRowView> SnapshotRows(List<DemolitionInputRow> rows)
        {
            return (rows ?? new List<DemolitionInputRow>()).Select(ToRowView).ToList();
        }

        private static DemolitionRowView ToRowView(DemolitionInputRow row)
        {
            return new DemolitionRowView
            {
                Row = row.RowNumber,
                Site = row.SiteRaw ?? "",
                Line = row.MappedLine ?? "",
                Process = row.Process ?? "",
                Code = row.ConstructionCode ?? "",
                Eqp = row.EqpId ?? "",
                Load = row.LoadStatus ?? "",
                Trace = row.TraceStatus ?? "",
                Valid = row.IsValid,
                Reason = RowReason(row)
            };
        }

        private static bool IsCancelled(Exception ex)
        {
            if (ex is OperationCanceledException)
                return true;
            var agg = ex as AggregateException;
            if (agg == null)
                return false;
            var flat = agg.Flatten().InnerExceptions;
            return flat.Count > 0 && flat.All(e => e is OperationCanceledException);
        }

        private static NozzleLookupKeyKind ParseNozzleKind(string keyKind)
        {
            string key = (keyKind ?? "").Trim();
            if (string.Equals(key, "instance", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "InstanceNumericId", StringComparison.OrdinalIgnoreCase))
                return NozzleLookupKeyKind.InstanceNumericId;
            if (string.Equals(key, "guid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "NodeGuid", StringComparison.OrdinalIgnoreCase))
                return NozzleLookupKeyKind.NodeGuid;
            return NozzleLookupKeyKind.BimId;
        }

        private JobRecord BeginJob(string kind, string db, string title)
        {
            lock (_gate)
            {
                DropOldJobs();
                if (_jobs.Values.Any(j => j.State == "running"))
                    throw new PortalBusyException(Msg(
                        "이미 실행 중인 작업이 있습니다. 끝난 뒤, 또는 취소 뒤에 다시 실행하세요.",
                        "已有作业在运行。请等待结束，或取消后再执行。"));

                var job = new JobRecord
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                    Kind = kind,
                    Db = db,
                    Title = title,
                    State = "running",
                    Percent = 1,
                    Message = Msg("시작", "开始"),
                    CreatedUtc = DateTime.UtcNow,
                    CancelSource = new CancellationTokenSource()
                };
                _jobs[job.Id] = job;
                return job;
            }
        }

        private void Fail(JobRecord job, Exception ex)
        {
            Log.Error(ex, "[Web] job {Id} failed", job.Id);
            lock (_gate)
            {
                job.State = "error";
                job.Percent = 100;
                job.Error = ex.Message;
                job.Message = ex.Message;
            }
        }

        private void DropOldJobs()
        {
            var dead = _jobs.Values
                .Where(j => j.State != "running" && j.CreatedUtc < DateTime.UtcNow.AddHours(-12))
                .Select(j => j.Id)
                .ToList();
            foreach (string id in dead)
                _jobs.Remove(id);
        }

        private static string JobDirectory(JobRecord job)
        {
            string path = Path.Combine(WorkRoot, "jobs", job.Id);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string RequireDb(string db)
        {
            db = (db ?? "").Trim();
            bool known = GlobalConfig.TargetDatabases.Any(d => string.Equals(d, db, StringComparison.OrdinalIgnoreCase));
            if (!known)
                throw new PortalInputException(Msg("대상 라인이 아닙니다: ", "不是目标线路: ") + db);
            string block = FreshnessBlock(db);
            if (block != null)
                throw new PortalInputException(block);
            return db;
        }

        private static string FreshnessBlock(string db)
        {
            DateTime savedAt;
            int version;
            string source;
            if (!CacheFreshnessGuard.TryGetSavedAt(db, out savedAt, out version, out source))
                return Msg(
                    db + " 캐시가 없습니다. Admin이 캐시를 만든 뒤 다시 실행하세요. 경로: " + GlobalConfig.GetCacheDirectory(db),
                    db + " 没有缓存。请让 Admin 生成缓存后再执行。路径: " + GlobalConfig.GetCacheDirectory(db));

            double age = (DateTime.Now - savedAt).TotalHours;
            if (age > GlobalConfig.CacheMaxAgeHours)
                return Msg(
                    db + " 캐시가 " + age.ToString("F1") + "시간 전 것입니다. 허용 " + GlobalConfig.CacheMaxAgeHours.ToString("0") + "시간. Admin CacheOnly 또는 Full 이후 다시 실행하세요.",
                    db + " 缓存是 " + age.ToString("F1") + " 小时前的。允许 " + GlobalConfig.CacheMaxAgeHours.ToString("0") + " 小时。请在 Admin CacheOnly 或 Full 之后再执行。");
            return null;
        }

        private static List<string> SplitTags(string text)
        {
            return (text ?? "")
                .Split(new[] { ',', '\r', '\n', ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string Msg(string korean, string chinese)
        {
            return DemolitionUiText.Kz(korean, chinese);
        }

        private static CancellationToken TokenOf(JobRecord job)
        {
            return job != null && job.CancelSource != null
                ? job.CancelSource.Token
                : CancellationToken.None;
        }

        private void MarkCancelled(JobRecord job)
        {
            lock (_gate)
            {
                job.State = "cancelled";
                job.Error = null;
                job.Message = Msg("작업을 취소했습니다.", "已取消作业。");
            }
        }

        private static string IssueCapWarning()
        {
            return Msg(
                "실패 사유는 화면에서 " + MaxIssueRows + "건까지입니다. 전체는 Excel에 있습니다.",
                "画面上最多显示 " + MaxIssueRows + " 条失败原因。完整内容在 Excel 中。");
        }

        private static string TrimText(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value ?? "";
            return value.Substring(0, max);
        }

        private static void AppendIssues(IEnumerable<TraceResult> results, List<TraceIssueView> sink, ref bool truncated)
        {
            if (results == null || sink == null || truncated)
                return;
            foreach (TraceResult result in results)
            {
                if (result == null)
                    continue;
                bool failed = result.FailCount > 0
                    || (result.IssueList != null && result.IssueList.Count > 0);
                if (!failed)
                    continue;
                if (sink.Count >= MaxIssueRows)
                {
                    truncated = true;
                    return;
                }

                string issues = "";
                if (result.IssueList != null)
                {
                    issues = string.Join(", ", result.IssueList.Where(s => !string.IsNullOrWhiteSpace(s)).Take(12));
                    issues = TrimText(issues, 400);
                }

                sink.Add(new TraceIssueView
                {
                    Tag = result.StartEquipmentTag ?? "",
                    Status = result.STATUS ?? "",
                    Poc = result.StartPocId == Guid.Empty ? "" : result.StartPocId.ToString(),
                    Issues = issues,
                    Feedback = TrimText(result.feedback, 240)
                });
            }
        }

        private static List<NozzleRowView> BuildNozzleRows(List<NozzleTraceInputRow> rows, NozzleTraceJobResult result)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (result != null && result.Runs != null)
            {
                foreach (var run in result.Runs)
                {
                    string key = run == null || run.Input == null ? "" : (run.Input.KeyValue ?? "");
                    if (key.Length == 0)
                        continue;
                    int add = run.Traces == null ? 0 : run.Traces.Count;
                    int cur;
                    counts.TryGetValue(key, out cur);
                    counts[key] = cur + add;
                }
            }

            var list = new List<NozzleRowView>();
            foreach (var row in rows ?? new List<NozzleTraceInputRow>())
            {
                if (row == null)
                    continue;
                int paths;
                counts.TryGetValue(row.KeyValue ?? "", out paths);
                list.Add(new NozzleRowView
                {
                    Kind = row.KeyKind.ToString(),
                    Key = row.KeyValue ?? "",
                    Status = row.TraceStatus ?? "",
                    Paths = paths
                });
            }
            return list;
        }

        private static string RowReason(DemolitionInputRow row)
        {
            if (row == null)
                return "";
            switch (row.LoadStatus ?? "")
            {
                case "InvalidFormat":
                    return Msg("형식 오류. Site와 EQPID가 필요합니다.", "格式错误。需要 Site 和 EQPID。");
                case "InvalidEqpId":
                    return Msg("EQPID가 비어 있습니다.", "EQPID 为空。");
                case "InvalidSite":
                    return Msg("Site를 라인으로 맞출 수 없습니다.", "无法把 Site 对应到线路。");
            }

            string trace = row.TraceStatus ?? "";
            if (trace.Length == 0 || trace == "-" || trace == "Pending" || trace == "Traced")
                return "";
            return trace;
        }

        private static string SafeFile(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            if (name.Length > 80)
                name = name.Substring(0, 80);
            return name;
        }
    }

    internal sealed class JobRecord
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Db { get; set; }
        public string Title { get; set; }
        public string State { get; set; }
        public int Percent { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public int ResultCount { get; set; }
        public string ExcelPath { get; set; }
        public string HtmlPath { get; set; }
        public string OutputDirectory { get; set; }
        public DateTime CreatedUtc { get; set; }
        public int? EtaSeconds { get; set; }
        public int LinesDone { get; set; }
        public int LinesTotal { get; set; }
        public int EqpDone { get; set; }
        public int EqpTotal { get; set; }
        public int EqpTraced { get; set; }
        public int EqpMissing { get; set; }
        public int EqpNoResult { get; set; }
        public int EqpErrorCount { get; set; }
        public double ElapsedSeconds { get; set; }
        public bool SummaryTruncated { get; set; }
        public string[] SheetNames { get; set; }
        public int[] SheetTotals { get; set; }
        public List<string[]> SummaryTable { get; set; }
        public List<DemolitionRowView> InputRows { get; set; }
        public CancellationTokenSource CancelSource { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Preview { get; } = new List<string>();
        public List<EquipmentTraceSummary> Summaries { get; set; } = new List<EquipmentTraceSummary>();
        public List<TraceIssueView> Issues { get; set; } = new List<TraceIssueView>();
        public bool IssuesTruncated { get; set; }
        public List<NozzleRowView> NozzleRows { get; set; } = new List<NozzleRowView>();
    }

    internal sealed class DemolitionDraft
    {
        public string Id { get; set; }
        public List<DemolitionInputRow> Rows { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    internal sealed class DemolitionRowView
    {
        [JsonProperty("row")] public int Row { get; set; }
        [JsonProperty("site")] public string Site { get; set; }
        [JsonProperty("line")] public string Line { get; set; }
        [JsonProperty("process")] public string Process { get; set; }
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("eqp")] public string Eqp { get; set; }
        [JsonProperty("load")] public string Load { get; set; }
        [JsonProperty("trace")] public string Trace { get; set; }
        [JsonProperty("valid")] public bool Valid { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
    }

    internal sealed class TraceIssueView
    {
        [JsonProperty("tag")] public string Tag { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("poc")] public string Poc { get; set; }
        [JsonProperty("issues")] public string Issues { get; set; }
        [JsonProperty("feedback")] public string Feedback { get; set; }
    }

    internal sealed class NozzleRowView
    {
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("paths")] public int Paths { get; set; }
    }

    internal sealed class PortalInputException : Exception
    {
        public PortalInputException(string message) : base(message) { }
    }

    internal sealed class PortalBusyException : Exception
    {
        public PortalBusyException(string message) : base(message) { }
    }
}
