using OfficeOpenXml; // EPPlus
using OfficeOpenXml.Style;
using PocTrace.Core.Composition;
using PocTrace.Core.Engine;
using PocTrace.Core.Engine.Reporting;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Enums;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Infrastructure.Repositories;
using PocTrace.Core.Models;
using Serilog;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PocTrace.UI.Services;

namespace PocTrace.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // 🌟 [필드 추가] 클래스 상단에 로거 필드를 정의합니다.
        private readonly ILogger _logger;
        private readonly IGraphLoader _loader;
        private readonly ITraceEngine _traceEngine;
        private readonly Func<ITraceEngine> _engineFactory; // 배치: 태그별(스레드별) 새 엔진 생성
        private readonly GraphCacheManager _cacheManager;
        private readonly TraceExcelReportService _excelReport;
        private readonly IDialogService _dialog;

        private IGraphCache CurrentCache => _cacheManager;

        public List<string> DbList { get; } = GlobalConfig.TargetDatabases
            .Select(d => d.ToUpperInvariant())
            .ToList();

        private string _selectedDb = "x1";
        public string SelectedDb
        {
            get => _selectedDb;
            set { _selectedDb = value; OnPropertyChanged(); }
        }

        // [New] 캐시 상태 메시지
        private string _cacheStatus = "Not Loaded";
        public string CacheStatus
        {
            get => _cacheStatus;
            set { _cacheStatus = value; OnPropertyChanged(); }
        }

        // Grid 바인딩용
        public ObservableCollection<EquipmentTraceSummary> TraceSummaries { get; set; }

        // 내부 데이터 보관용
        private List<TraceResult> _allTraceResults;

        // [New] Hookup Validation 결과 보관용
        private List<HookupValidationResult> _allHookupErrors;

        // Commands
        public RelayCommand LoadGraphCommand { get; private set; }
        public RelayCommand SearchCommand { get; private set; }
        public RelayCommand BatchSearchCommand { get; private set; }

        // Status
        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // Inputs
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        // [New] Batch Input
        private string _batchInputText;
        public string BatchInputText
        {
            get => _batchInputText;
            set { _batchInputText = value; OnPropertyChanged(); }
        }

        public int LimitCount { get; set; } = 0; // 0 = Unlimited

        // 생성자
        public MainViewModel(
            IGraphLoader loader,
            GraphCacheManager cacheManager,
            ITraceEngine engine,
            Func<ITraceEngine> engineFactory,
            TraceExcelReportService excelReport,
            ILogger logger,
            IDialogService dialog)
        {
            _loader = loader;
            _cacheManager = cacheManager;
            _traceEngine = engine;
            _engineFactory = engineFactory;
            _excelReport = excelReport;
            _logger = logger;
            _dialog = dialog ?? new WinFormsDialogService();

            TraceSummaries = new ObservableCollection<EquipmentTraceSummary>();
            _allTraceResults = new List<TraceResult>();
            _allHookupErrors = new List<HookupValidationResult>();

            LoadGraphCommand = new RelayCommand(async (o) => await LoadDataAsync().ConfigureAwait(true));
            SearchCommand = new RelayCommand(async (o) => await SearchAsync(SearchText).ConfigureAwait(true));
            BatchSearchCommand = new RelayCommand(async (o) => await RunBatchTraceAsync(BatchInputText).ConfigureAwait(true));
        }

        // [수정] DB 선택 반영하여 로드
        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                StatusMessage = $"Loading Data for {SelectedDb}...";

                var progress = new Progress<string>(msg => StatusMessage = msg);

                // [수정] 구조 분해 할당 (Deconstruction) 사용
                // 튜플을 개별 변수로 쪼개서 받습니다. CS1061 오류 완벽 해결.
                var (isStale, savedAt, nodes, pocs, pipes, takeoffs, dynamicProperties) =
                    await _loader.LoadGraphDataAsync(SelectedDb, progress).ConfigureAwait(true);

                // [체크] 데이터가 null인지 확인 (nodes 변수 사용)
                if (nodes == null)
                {
                    StatusMessage = "Load Failed (No Data).";
                    return;
                }

                // [New] 2. UtMapping.dat 로드
                // GraphLoaderRepository에 추가했던 LoadUtMapping 메서드 호출
                StatusMessage = "Loading Utility Mappings...";
                var utMaps = await Task.Run(() => _loader.LoadUtMapping()).ConfigureAwait(true);

                // ---------------------------------------------------------
                // [New] UT Mapping 로드 결과 검증 (디버깅용)
                // ---------------------------------------------------------
                if (utMaps != null && utMaps.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"✅ UT Mapping Loaded Successfully!");
                    sb.AppendLine($"---------------------------------------------");
                    sb.AppendLine($"Target DB: {SelectedDb}");

                    if (utMaps.TryGetValue(SelectedDb, out var groupMap))
                    {
                        sb.AppendLine($"Total Groups: {groupMap.Count}");
                        sb.AppendLine($"---------------------------------------------");

                        // 샘플 3개 그룹만 확인
                        int sampleCount = 0;
                        foreach (var group in groupMap)
                        {
                            if (sampleCount++ >= 3) break; // 3개만 샘플링
                            sb.AppendLine($"[Group: {group.Key}] -> Mapped Count: {group.Value.Count}");

                            // 그룹 내 매핑 샘플 2개
                            var firstTwo = group.Value.Take(2).Select(kv => $"{kv.Key}={kv.Value}");
                            sb.AppendLine($"   Ex: {string.Join(", ", firstTwo)} ...");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"⚠️ Warning: No mapping found for selected DB '{SelectedDb}'.");
                        sb.AppendLine($"Available DBs in CSV: {string.Join(", ", utMaps.Keys)}");
                    }

                    // 로그 기록
                    Log.Information(sb.ToString());

                    // [선택] 개발 중에만 팝업으로 확인 (배포 시 주석 처리 가능)
                    //MessageBox.Show(sb.ToString(), "UT Mapping Debug Info");
                }
                else
                {
                    Log.Warning("⚠️ UT Mapping is empty.");
                    _dialog.ShowWarning("UT Mapping file loaded but empty.");
                }
                // ---------------------------------------------------------

                StatusMessage = "Initializing Graph...";
                MemoryLoadLog.Stage(
                    "UI.BeforeInitialize",
                    "db=" + SelectedDb
                    + " nodes=" + CollectionCount(nodes)
                    + " pocs=" + CollectionCount(pocs)
                    + " pipes=" + CollectionCount(pipes)
                    + " takeoffs=" + CollectionCount(takeoffs)
                    + " dyn=" + CollectionCount(dynamicProperties)
                    + " allowDatabaseFallback=" + GlobalConfig.AllowDatabaseFallback);

                await Task.Run(() =>
                {
                    // 역인덱스는 원본 리스트를 놓은 뒤에 만든다.
                    // 리스트 + 사전 + 역인덱스가 한 프로세스에 동시에 있으면 주소 공간이 터진다.
                    _cacheManager.Initialize(
                        SelectedDb,
                        nodes,
                        pocs,
                        pipes,
                        takeoffs,
                        dynamicProperties,
                        utMaps,
                        buildReverseGraphIndex: false,
                        progress: progress);

                    nodes = null;
                    pocs = null;
                    pipes = null;
                    takeoffs = null;
                    dynamicProperties = null;

                    MemoryLoadLog.CompactForLargeAlloc("UI.BeforeReverseIndex");
                    progress.Report("Initializing Graph: reverse index");
                    _cacheManager.GetReverseGraphIndex();
                });

                string staleMsg = isStale ? "⚠️ [OLD DATA]" : "✅ [LATEST]";
                CacheStatus = $"{staleMsg} Date: {savedAt:yyyy-MM-dd HH:mm}";

                StatusMessage = $"Ready ({SelectedDb}).";
            }
            catch (Exception ex)
            {
                MemoryLoadLog.Fail("UI.LoadData", ex);
                StatusMessage = ex is OutOfMemoryException
                    ? "Error: out of memory while initializing graph."
                    : "Error Loading Data.";
                try { Log.Error(ex, "LoadDataAsync Failed"); }
                catch { }

                try
                {
                    string path = MemoryLoadLog.FilePath ?? "(memory-load.log 없음)";
                    string message = ex is OutOfMemoryException
                        ? "그래프 초기화 중 프로세스 메모리가 부족합니다.\r\n"
                          + "작업 관리자의 전체 메모리 %와 별개로, 32비트 한도(약 2~4GB)이거나\r\n"
                          + "한 배열이 2GB를 넘으면 이 예외가 납니다.\r\n"
                          + "AllowDatabaseFallback 은 이 단계에 영향이 없습니다.\r\n"
                          + path
                        : "Error: " + ex.Message + "\r\n" + path;
                    _dialog.ShowError(message, "Load Error");
                }
                catch
                {
                }
            }
            finally
            {
                IsBusy = false;
            }
        }


        public async Task SearchAsync(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Tracing tag: {tag}...";

                TraceSummaries.Clear();
                _allTraceResults.Clear();
                _allHookupErrors.Clear();

                if (!CurrentCache.IsInitialized)
                    throw new InvalidOperationException("Cache is not initialized. Please load data first.");

                // [VOC-2] Viewer 대화형 검색: 콤마 토큰 부분매칭 포함(스크러버 "tag1,tag2" 등도 대상)
                // EnableDetailedDebug는 PocTrace.UI.exe.config AppSettings를 따름 (TraceEngine이 algorithm에 전달)
                bool detailedLog = GlobalConfig.EnableDetailedDebug;
                var results = await _traceEngine.RunTraceAsync(tag, 0, detailedLog, includePartialTagMatch: true).ConfigureAwait(true);

                if (results != null && results.Count > 0)
                {
                    lock (_allTraceResults) { _allTraceResults.AddRange(results); }
                }

                UpdateSummaries(results ?? new List<TraceResult>());

                OnPropertyChanged(nameof(TraceSummaries));

                StatusMessage = $"Found {TraceSummaries.Count} Equipments.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error during trace: " + ex.Message;
                Log.Error(ex, "SearchAsync Failed");
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // [1] 공통 로직 추출 (Batch와 Single이 함께 쓸 함수)
        // =========================================================
        private static int CollectionCount(IEnumerable source)
        {
            var collection = source as ICollection;
            return collection == null ? -1 : collection.Count;
        }

        private List<EquipmentTraceSummary> GetGroupedSummaries(List<TraceResult> results)
        {
            // [Refactor] 집계 로직은 Core의 EquipmentSummaryBuilder로 단일화됨.
            // (이 경로에서 TotalLength는 화면/배치 엑셀에 표시되지 않으므로 단위 변화의 영향 없음)
            try
            {
                return EquipmentSummaryBuilder.Build(results);
            }
            catch (Exception ex)
            {
                Log.Information($"{ex.Source} {ex.Message} {ex.InnerException} {ex.StackTrace} {ex.HResult}");
                return new List<EquipmentTraceSummary>();
            }
        }

        // =========================================================
        // [2] 배치 추적 (RunBatchTraceAsync) 수정
        // =========================================================
        public async Task RunBatchTraceAsync(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                _dialog.ShowInfo("Please enter tags in the batch input box.");
                return;
            }
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                TraceSummaries.Clear();
                _allTraceResults.Clear();
                _allHookupErrors.Clear();

                var tags = inputText.Split(new[] { ',', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim()).Distinct().ToList();

                StatusMessage = $"🚀 Batch Tracing {tags.Count} tags...";

                Log.Information($"[Batch] 초기화 완료. 대상 태그 수: {tags.Count}");
                // 엑셀 저장 폴더 생성
                string outputDir = PocTraceLocalPaths.CombineApp(
                    PocTraceLocalPaths.Viewer,
                    "BatchTraceResult",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                var tempAllResults = new ConcurrentBag<TraceResult>();
                var tempSummaries = new ConcurrentBag<EquipmentTraceSummary>();
                var tempHookupErrors = new ConcurrentBag<HookupValidationResult>();
                var failedTags = new ConcurrentBag<string>();

                // [New] 캐시 확인
                var cache = _cacheManager as IGraphCache;
                if (cache == null || !cache.IsInitialized)
                {
                    throw new InvalidOperationException("Cache is not initialized. Please load data first.");
                }

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, GlobalConfig.MaxParallelTrace)
                };

                await Task.Run(() =>
                {
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                    Parallel.ForEach(tags, parallelOptions, tag =>
                    {
                        try
                        {
                            var engine = _engineFactory();
                            // [VOC-2] Viewer 배치도 대화형이므로 부분매칭 포함
                            bool detailedLog = GlobalConfig.EnableDetailedDebug;
                            var results = engine.RunTrace(tag, 0, detailedLog, System.Threading.CancellationToken.None, includePartialTagMatch: true);

                            if (results == null || results.Count == 0)
                            {
                                _logger.Warning("[Batch] No results for tag: {Tag}", tag);
                                return;
                            }

                            var summaryList = GetGroupedSummaries(results);
                            var firstSum = summaryList.First();
                            string fileName = $"{SelectedDb}_{tag}_{firstSum.EquipmentCount}_{firstSum.TotalPocCount}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                            string fullPath = Path.Combine(outputDir, fileName);

                            _excelReport.SaveSingleFileReport(fullPath, results, CurrentCache);

                            foreach (var r in results) tempAllResults.Add(r);
                            foreach (var s in summaryList) tempSummaries.Add(s);

                            Log.Information("[Batch] Completed: {Tag}", tag);
                        }
                        catch (Exception ex)
                        {
                            failedTags.Add(tag);
                            Log.Error(ex, "[Batch] Error processing tag: {Tag}", tag);
                        }
                    });
                }).ConfigureAwait(true);

                // Summary Excel 생성
                var summaryOnlyList = tempSummaries.ToList();
                if (summaryOnlyList.Any())
                {
                    int totalEqCount = summaryOnlyList.Sum(s => s.EquipmentCount);
                    int totalPocCount = summaryOnlyList.Sum(s => s.TotalPocCount);
                    long totalErrCount = summaryOnlyList.Sum(s => s.CountERR);
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    string summaryFileName = $"Summary_{timestamp}_Eq{totalEqCount}_Poc{totalPocCount}_Err{totalErrCount}.xlsx";
                    string summaryPath = Path.Combine(outputDir, summaryFileName);

                    StatusMessage = "Saving Batch Summary Excel...";
                    await Task.Run(() => _excelReport.SaveBatchSummaryReport(summaryPath, summaryOnlyList));
                }

                // UI 업데이트
                var sortedList = tempSummaries.OrderBy(s => s.EquipmentTag).ToList();
                foreach (var item in sortedList)
                {
                    TraceSummaries.Add(item);
                }

                _allTraceResults = tempAllResults.ToList();
                _allHookupErrors = tempHookupErrors.ToList();

                OnPropertyChanged(nameof(TraceSummaries));

                StatusMessage = failedTags.IsEmpty
                    ? $"Batch Complete. Saved to {outputDir} (Hookup Errors: {_allHookupErrors.Count})"
                    : $"Batch finished with {failedTags.Count} failed tag(s). Saved to {outputDir}";
                System.Diagnostics.Process.Start("explorer.exe", outputDir);
                if (!failedTags.IsEmpty)
                    throw new InvalidOperationException(
                        "배치 중 실패한 태그: " + string.Join(", ", failedTags.Take(8)));
            }
            catch (Exception ex)
            {
                StatusMessage = "Error during batch trace: " + ex.Message;
                Log.Error(ex, "Batch Failed");
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // [요청 2] 우클릭 상세 추적 (MainForm에서 호출)
        public async Task ReTraceSpecificAsync(string equipGuidStr, string equipTag)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusMessage = $"🔍 Deep Tracing for {equipTag} ({equipGuidStr})... Check Logs.";
                Log.Information("=================================================");
                Log.Information("🕵️ [Manual Deep Trace] Start for GUID: {Guid}", equipGuidStr);

                // 상세 모드(true)로 실행 -> TraceEngine이 모든 스텝을 로그에 남김
                // Tag로 전체를 다시 돌리지만, 로그에 상세 정보가 남으므로 목적 달성
                // (더 정교하게 하려면 Engine에 Guid 필터를 넣어야 하지만, 현재 구조상 Tag 재검색이 안전)
                await _traceEngine.RunTraceAsync(equipTag, 0, true, includePartialTagMatch: true);

                StatusMessage = "Deep Trace Completed. Logs updated.";
                Log.Information("=================================================");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Deep Trace Failed");
                StatusMessage = "Deep Trace Error.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // [Helper] Summary 집계 및 UI 바인딩
        private void UpdateSummaries(List<TraceResult> results)
        {
            // 이제 직접 로직을 짜지 않고 공통 함수 호출
            var list = GetGroupedSummaries(results);

            foreach (var item in list)
            {
                TraceSummaries.Add(item);
            }
        }

        // ---------------------------------------------------------------------
        // [핵심] 멀티 시트 엑셀 내보내기 (XML Spreadsheet 포맷)
        // ---------------------------------------------------------------------
        public async Task ExportToExcelAsync(string filePath)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Exporting to Excel...";

                // 라이선스 컨텍스트 (한 번 더 확인)
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                await Task.Run(() =>
                {
                    // 1. 현재 메모리에 있는 결과(_allTraceResults)를 바탕으로 Summary 재생성
                    var currentDetails = _allTraceResults;

                    // 2. Core 리포팅 서비스 호출
                    _excelReport.SaveSingleFileReport(filePath, currentDetails, CurrentCache);
                });

                StatusMessage = "Export Complete.";
                Log.Information("💾 Excel Saved to {Path}", filePath);
            }
            catch (Exception ex)
            {
                StatusMessage = "Export Failed.";
                Log.Error(ex, "Excel Export Failed");
                _dialog.ShowError($"Export Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}
