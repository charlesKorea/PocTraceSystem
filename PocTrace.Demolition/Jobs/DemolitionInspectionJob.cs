using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PocTrace.Core.Composition;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Repositories;
using PocTrace.Core.Models;
using PocTrace.Demolition.Export;
using PocTrace.Demolition.Input;
using Serilog;

namespace PocTrace.Demolition.Jobs
{
  /// <summary>
  /// MappedLine별 BinaryCache 로드 → 설비정보(+3-% Template) / POC Trace(Status EMP) /
  /// GraphCache 역매칭(Nozzle·Stick·TakeOff) / Hookup / Group(sidecar ParentID path).
  /// Full List Excel은 OOM 방지를 위해 사용하지 않음.
  /// </summary>
  public sealed class DemolitionInspectionJob
  {
    public const string TraceStatusTraced = "Traced";
    public const string TraceStatusMissingTag = "MissingTag";
    public const string TraceStatusNoResult = "NoResult";
    public const string TraceStatusError = "Error";
    public const string TraceStatusCacheFail = "CacheFail";

    private Stopwatch _etaClock;

    public async Task<DemolitionJobResult> RunAsync(
      IReadOnlyList<DemolitionInputRow> validRows,
      IProgress<DemolitionJobProgress> progress = null,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      var result = new DemolitionJobResult();
      var sw = Stopwatch.StartNew();
      _etaClock = Stopwatch.StartNew();

      if (validRows == null || validRows.Count == 0)
      {
        result.Success = false;
        result.ErrorMessage = "유효한 Input 행이 없습니다.";
        return result;
      }

      var lineGroups = validRows
        .Where(r => r != null && r.IsValid)
        .GroupBy(r => r.MappedLine, StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

      result.LinesProcessed = 0;
      int linesTotal = lineGroups.Count;
      int eqpTotal = lineGroups.Sum(g =>
        g.Select(r => r.EqpId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
      int eqpDone = 0;

      Report(progress,
        string.Format(DemolitionUiText.ProgStart, linesTotal, eqpTotal),
        null, 0, linesTotal, 0, eqpTotal, 0);

      var allUploadedEqp = validRows
        .Where(r => r != null && r.IsValid)
        .Select(r => r.EqpId)
        .ToList();
      var sheetCounter = new EqpSheetCounter(allUploadedEqp);

      var services = PocTraceAppServices.Create(Log.Logger, GlobalConfig.AllowDatabaseFallback);
      var allResults = new ConcurrentBag<TraceResult>();
      var allEquip = new ConcurrentBag<object[]>();
      var allStatusEmp = new ConcurrentBag<object[]>();
      var allNozzle = new ConcurrentBag<object[]>();
      var allStick = new ConcurrentBag<object[]>();
      var allTakeOff = new ConcurrentBag<object[]>();
      var allHookup = new ConcurrentBag<object[]>();
      var allBimGroup = new ConcurrentBag<object[]>();
      int maxParallel = Math.Max(1, GlobalConfig.MaxParallelTrace);
      bool detailed = GlobalConfig.EnableDetailedDebug;

      Dictionary<string, Dictionary<string, Dictionary<string, string>>> utMaps;
      try
      {
        utMaps = await Task.Run(() => services.GraphLoader.LoadUtMapping(), cancellationToken)
          .ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[Demolition] UtMapping load failed — continuing with empty map");
        utMaps = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(
          StringComparer.OrdinalIgnoreCase);
        result.Warnings.Add("UtMapping 로드 실패: " + ex.Message);
      }

      foreach (var lineGroup in lineGroups)
      {
        cancellationToken.ThrowIfCancellationRequested();
        string line = lineGroup.Key;
        var rowsForLine = lineGroup.ToList();
        var eqpIds = rowsForLine
          .Select(r => r.EqpId.Trim())
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
          .ToList();

        Report(progress,
          string.Format(DemolitionUiText.ProgCacheLoad, line, result.LinesProcessed + 1, linesTotal),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.02);

        try
        {
          var loadProgress = new Progress<string>(msg =>
            Report(progress, "[" + line + "] " + msg, line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal,
              MapCacheLoadPhase(msg)));

          var (isStale, savedAt, nodes, pocs, pipes, takeoffs, dyn) =
            await services.GraphLoader.LoadGraphDataAsync(line, loadProgress).ConfigureAwait(false);

          if (nodes == null)
            throw new InvalidOperationException("Cache returned null nodes for " + line);

          if (isStale)
          {
            string warn = string.Format(
              "[{0}] 캐시 stale/구버전 가능 (SavedAt={1:yyyy-MM-dd HH:mm})", line, savedAt);
            Log.Warning(warn);
            result.Warnings.Add(warn);
          }

          await Task.Run(() =>
            services.CacheManager.Initialize(line, nodes, pocs, pipes, takeoffs, dyn, utMaps),
            cancellationToken).ConfigureAwait(false);

          Log.Information(
            "[Demolition] Cache ready for {Line}: Eqp={Eqp}, SavedAt={At}, Stale={Stale}",
            line, eqpIds.Count, savedAt, isStale);
        }
        catch (Exception ex)
        {
          Log.Error(ex, "[Demolition] Cache load failed for {Line}", line);
          result.Warnings.Add("[" + line + "] 캐시 로드 실패: " + ex.Message);
          SetTraceStatus(rowsForLine, TraceStatusCacheFail);
          result.EqpError += eqpIds.Count;
          result.EqpAttempted += eqpIds.Count;
          eqpDone += eqpIds.Count;
          result.LinesProcessed++;
          Report(progress,
            string.Format("캐시 실패: {0}", line),
            line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 1.0);
          continue;
        }

        var cache = services.CacheManager;

        // --- Sheet 설비정보 (GraphCache Tag + 3-% Template sidecar) ---
        Report(progress,
          string.Format(DemolitionUiText.ProgEquipScan, line),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.28);

        IReadOnlyList<NodeInfo> templateNodes = null;
        var teLoad = new TemplateEquipmentCacheRepository().TryLoad(line);
        if (teLoad == null)
        {
          string warn = string.Format(
            "[{0}] TemplateEqpCache 없음 — 3-% 설비는 설비정보에 미포함 (Admin TemplateEqp 캐시 생성 필요)", line);
          Log.Warning(warn);
          result.Warnings.Add(warn);
        }
        else
        {
          if (teLoad.IsStale)
          {
            string warn = string.Format(
              "[{0}] TemplateEqpCache stale (SavedAt={1:yyyy-MM-dd HH:mm})", line, teLoad.SavedAt);
            result.Warnings.Add(warn);
          }
          templateNodes = teLoad.Nodes;
        }

        foreach (var row in EquipmentInfoProjector.Collect(cache, eqpIds, sheetCounter, templateNodes))
          allEquip.Add(row);

        // --- POC Trace → Status EMP ---
        Report(progress,
          string.Format(DemolitionUiText.ProgTrace, line, eqpIds.Count, maxParallel),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.30);

        var statusByEqp = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lineTraces = new ConcurrentBag<TraceResult>();
        int lineTraced = 0, lineMissing = 0, lineNoResult = 0, lineError = 0;

        Parallel.ForEach(eqpIds, new ParallelOptions
        {
          MaxDegreeOfParallelism = maxParallel,
          CancellationToken = cancellationToken
        }, eqpId =>
        {
          try
          {
            var startPocs = cache.FindStartPocs(eqpId, includePartialMatch: false)?.ToList();
            if (startPocs == null || startPocs.Count == 0)
            {
              statusByEqp[eqpId] = TraceStatusMissingTag;
              Interlocked.Increment(ref lineMissing);
              Log.Warning("[Demolition][{Line}] MissingTag: {Eqp}", line, eqpId);
              return;
            }

            var engine = services.CreateTraceEngine(cache);
            var traces = engine.RunTrace(eqpId, 0, detailed, cancellationToken, includePartialTagMatch: false);

            if (traces == null || traces.Count == 0)
            {
              statusByEqp[eqpId] = TraceStatusNoResult;
              Interlocked.Increment(ref lineNoResult);
              return;
            }

            foreach (var tr in traces)
            {
              tr.Line = line;
              if (string.IsNullOrWhiteSpace(tr.StartEquipmentTag))
                tr.StartEquipmentTag = eqpId;
              allResults.Add(tr);
              lineTraces.Add(tr);
            }

            statusByEqp[eqpId] = TraceStatusTraced;
            Interlocked.Increment(ref lineTraced);
          }
          catch (Exception ex)
          {
            statusByEqp[eqpId] = TraceStatusError + ": " + Truncate(ex.Message, 80);
            Interlocked.Increment(ref lineError);
            Log.Error(ex, "[Demolition][{Line}] Trace error: {Eqp}", line, eqpId);
          }
          finally
          {
            int done = Interlocked.Increment(ref eqpDone);
            if (done % 10 == 0 || done == eqpTotal)
            {
              double tracePhase = 0.30 + 0.45 * (eqpTotal <= 0 ? 1 : (double)done / eqpTotal);
              Report(progress,
                string.Format("Trace {0}: {1}/{2} EQPID", line, done, eqpTotal),
                line, result.LinesProcessed, linesTotal, done, eqpTotal, tracePhase);
            }
          }
        });

        foreach (var empRow in StatusEmpProjector.Collect(lineTraces, eqpIds, sheetCounter))
          allStatusEmp.Add(empRow);

        // --- Nozzle / Stick / TakeOff: GraphCache 역매칭 (Full List Excel 미사용 → OOM 회피) ---
        Report(progress,
          string.Format(DemolitionUiText.ProgNozStickTof, line),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.78);

        foreach (var row in FullListNozzleProjector.Collect(cache, eqpIds, sheetCounter))
          allNozzle.Add(row);
        Report(progress, string.Format(DemolitionUiText.ProgStick, line),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.82);
        foreach (var row in FullListStickProjector.Collect(cache, eqpIds, sheetCounter))
          allStick.Add(row);
        Report(progress, string.Format(DemolitionUiText.ProgTakeOff, line),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.86);
        foreach (var row in FullListTakeOffProjector.Collect(cache, eqpIds, sheetCounter))
          allTakeOff.Add(row);

        // --- Hookup ---
        Report(progress,
          string.Format(DemolitionUiText.ProgHookup, line),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.90);
        foreach (var hRow in HookupNumberProjector.Collect(cache, eqpIds, sheetCounter))
          allHookup.Add(hRow);

        // --- Group sidecar (ParentID path) ---
        Report(progress,
          string.Format(DemolitionUiText.ProgGroup, line),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 0.95);
        var bgLoad = new BimGroupCacheRepository().TryLoad(line);
        if (bgLoad == null)
        {
          string warn = string.Format(
            "[{0}] GroupCache 없음 — Sheet Group 비움 (Admin Group 캐시 생성 필요)", line);
          Log.Warning(warn);
          result.Warnings.Add(warn);
        }
        else
        {
          if (bgLoad.IsStale)
          {
            string warn = string.Format(
              "[{0}] GroupCache stale (SavedAt={1:yyyy-MM-dd HH:mm})", line, bgLoad.SavedAt);
            result.Warnings.Add(warn);
          }

          var index = bgLoad.Records != null && bgLoad.Records.Count > 0
            ? new BimGroupNameIndex(bgLoad.Records)
            : new BimGroupNameIndex(bgLoad.Names);
          foreach (var bRow in BimGroupEqpProjector.Collect(index, eqpIds, sheetCounter))
            allBimGroup.Add(bRow);
        }

        foreach (var row in rowsForLine)
        {
          string st;
          if (statusByEqp.TryGetValue(row.EqpId.Trim(), out st))
            row.TraceStatus = st;
          else
            row.TraceStatus = TraceStatusError + ": no status";
        }

        result.EqpAttempted += eqpIds.Count;
        result.EqpTraced += lineTraced;
        result.EqpMissingTag += lineMissing;
        result.EqpNoResult += lineNoResult;
        result.EqpError += lineError;
        result.LinesProcessed++;

        Report(progress,
          string.Format(DemolitionUiText.ProgLineDone,
            line, lineTraced, lineMissing, lineNoResult, lineError),
          line, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 1.0);
      }

      result.Results = allResults.ToList();
      result.EquipmentInfoRows = allEquip.ToList();
      result.StatusEmpRows = allStatusEmp.ToList();
      result.NozzleRows = allNozzle.ToList();
      result.StickRows = allStick.ToList();
      result.TakeOffRows = allTakeOff.ToList();
      result.HookupRows = allHookup.ToList();
      result.BimGroupEqpRows = allBimGroup.ToList();
      result.SummaryCounter = sheetCounter;
      result.SummaryRows = sheetCounter.ToSummaryRows();
      sw.Stop();
      result.Elapsed = sw.Elapsed;
      result.Success = true;

      Log.Information(
        "[Demolition] Job done: Lines={L}, Eqp={E}, Equip={E1} Emp={E2} Noz={E3} Stick={E4} TOF={E5} HU={E6} BG={E7}, Traced={T}, Missing={M}, NoResult={N}, Error={Err}, Elapsed={Ms}ms",
        result.LinesProcessed, result.EqpAttempted,
        result.EquipmentInfoRowCount, result.StatusEmpRowCount, result.NozzleRowCount,
        result.StickRowCount, result.TakeOffRowCount, result.HookupRowCount, result.BimGroupEqpRowCount,
        result.EqpTraced, result.EqpMissingTag, result.EqpNoResult, result.EqpError,
        (long)result.Elapsed.TotalMilliseconds);

      Report(progress,
        string.Format(DemolitionUiText.ProgDone,
          result.EquipmentInfoRowCount, result.StatusEmpRowCount, result.NozzleRowCount,
          result.StickRowCount, result.TakeOffRowCount, result.HookupRowCount, result.BimGroupEqpRowCount,
          result.EqpAttempted, result.Elapsed.TotalSeconds),
        null, result.LinesProcessed, linesTotal, eqpDone, eqpTotal, 1.0);

      return result;
    }

    private static void SetTraceStatus(IEnumerable<DemolitionInputRow> rows, string status)
    {
      foreach (var row in rows)
        row.TraceStatus = status;
    }

    private static string Truncate(string s, int max)
    {
      if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
      return s.Substring(0, max) + "…";
    }

    private static double MapCacheLoadPhase(string msg)
    {
      if (string.IsNullOrEmpty(msg)) return 0.05;
      if (msg.IndexOf("nodes", StringComparison.OrdinalIgnoreCase) >= 0) return 0.06;
      if (msg.IndexOf("pocs", StringComparison.OrdinalIgnoreCase) >= 0) return 0.12;
      if (msg.IndexOf("pipes", StringComparison.OrdinalIgnoreCase) >= 0) return 0.18;
      if (msg.IndexOf("takeoff", StringComparison.OrdinalIgnoreCase) >= 0) return 0.22;
      if (msg.IndexOf("dyn", StringComparison.OrdinalIgnoreCase) >= 0) return 0.24;
      if (msg.IndexOf("완료", StringComparison.OrdinalIgnoreCase) >= 0) return 0.26;
      return 0.08;
    }

    private void Report(
      IProgress<DemolitionJobProgress> progress,
      string message,
      string line,
      int linesDone,
      int linesTotal,
      int eqpDone,
      int eqpTotal,
      double linePhase01)
    {
      if (progress == null) return;

      if (linePhase01 < 0) linePhase01 = 0;
      if (linePhase01 > 1) linePhase01 = 1;

      double overall = 0;
      if (linesTotal > 0)
        overall = (linesDone + linePhase01) / linesTotal;
      if (overall > 1) overall = 1;
      int pct = (int)Math.Round(overall * 100.0);
      if (pct < 0) pct = 0;
      if (pct > 100) pct = 100;

      int? etaSec = null;
      if (_etaClock != null && pct >= 2)
      {
        double elapsed = _etaClock.Elapsed.TotalSeconds;
        double remain = elapsed * (100.0 - pct) / pct;
        etaSec = (int)Math.Max(0, Math.Round(remain));
      }

      progress.Report(new DemolitionJobProgress
      {
        Message = message,
        CurrentLine = line,
        LinesDone = linesDone,
        LinesTotal = linesTotal,
        EqpDone = eqpDone,
        EqpTotal = eqpTotal,
        Percent = pct,
        EtaSeconds = etaSec
      });
    }
  }
}
