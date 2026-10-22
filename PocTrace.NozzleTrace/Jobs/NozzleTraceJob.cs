using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using PocTrace.Core.Composition;

using PocTrace.Core.Infrastructure;

using PocTrace.Core.Infrastructure.Diagnostics;

using PocTrace.Core.Infrastructure.Repositories;

using PocTrace.Core.Models;

using PocTrace.Core.Models.ReverseTrace;

using PocTrace.NozzleTrace.Export;

using PocTrace.NozzleTrace.Input;

using PocTrace.NozzleTrace;

using Serilog;



namespace PocTrace.NozzleTrace.Jobs

{

  public sealed class NozzleTraceJob

  {

    /// <summary>Viewer LoadGraph와 동일 — 라인 캐시 로드 후 ID 입력·역추적.</summary>

    public async Task<NozzleLineCacheLoadResult> LoadLineCacheAsync(

      string dbName,

      IProgress<NozzleTraceJobProgress> progress = null,

      CancellationToken cancellationToken = default(CancellationToken))

    {

      var result = new NozzleLineCacheLoadResult();

      if (string.IsNullOrWhiteSpace(dbName))

      {

        result.ErrorMessage = "DB를 선택하세요.";

        return result;

      }



      string line = dbName.Trim();

      Report(progress, NozzleTraceUserMessages.Step(1, 2, NozzleTraceUserMessages.Both("라인 캐시 로드: ", "正在加载线路缓存: ") + line), line, 0, 1, 0.1);



      var services = PocTraceAppServices.Create(Log.Logger, GlobalConfig.AllowDatabaseFallback);

      var binaryRepo = new BinaryCacheRepository();

      int cacheVersion = 0;

      DateTime savedAt = DateTime.MinValue;

      if (binaryRepo.TryPeekManifest(line, out cacheVersion, out savedAt))

      {

        Log.Information("[NozzleTrace] manifest peek — {Db} v{Ver} saved {At:yyyy-MM-dd HH:mm}",

          line, cacheVersion, savedAt);

      }



      try

      {

        var loadProgress = new Progress<string>(msg =>

          Report(progress, "[" + line + "] " + msg, line, 0, 1, 0.3));



        var tuple = await services.GraphLoader.LoadGraphDataAsync(line, loadProgress)

          .ConfigureAwait(false);



        bool isStale = tuple.Item1;

        savedAt = tuple.Item2;

        var nodes = tuple.Item3;

        var pocs = tuple.Item4;

        var pipes = tuple.Item5;

        var takeoffs = tuple.Item6;

        var hookupNumbers = tuple.Item7;



        if (nodes == null)

          throw new InvalidOperationException(NozzleTraceUserMessages.Both("캐시에서 노드 데이터를 읽지 못했습니다.", "未能从缓存读取节点数据。"));



        if (!binaryRepo.TryPeekManifest(line, out cacheVersion, out savedAt))
          cacheVersion = 0;

        cancellationToken.ThrowIfCancellationRequested();

        Report(progress, NozzleTraceUserMessages.Step(2, 2, NozzleTraceUserMessages.Both("그래프 초기화", "正在初始化图")), line, 0, 1, 0.6);



        await Task.Run(() =>

            services.CacheManager.Initialize(

              line,

              nodes,

              pocs,

              pipes,

              takeoffs,

              hookupNumbers,

              new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase)),

            cancellationToken).ConfigureAwait(false);



        var nodeList = nodes as IList<NodeInfo> ?? nodes.ToList();

        var bimStats = BimIdDiagnostics.AnalyzeAndLog("NozzleTraceLoad", line, cacheVersion, nodeList);



        if (isStale)

        {

          string warn = string.Format("[{0}] 캐시 stale (저장 {1:yyyy-MM-dd HH:mm}) — Admin 재생성 권장", line, savedAt);

          Log.Warning(warn);

        }



        result.Session = new NozzleLineCacheSession(services, line, savedAt, cacheVersion, isStale, bimStats);

        result.Success = true;

        Report(progress, NozzleTraceUserMessages.Both("캐시 준비 완료", "缓存已就绪") + " — " + BimIdDiagnostics.FormatUserSummary(bimStats, savedAt), line, 1, 1, 1.0);

      }

      catch (Exception ex)

      {

        Log.Error(ex, "[NozzleTrace] Cache load failed for {Line}", line);

        result.ErrorMessage = NozzleTraceUserMessages.Both("캐시 로드 실패: ", "缓存加载失败: ") + ex.Message;

        try { services.CacheManager?.ReleaseMemory(); } catch { }

      }



      return result;

    }



    public async Task<NozzleTraceJobResult> RunTracesAsync(

      NozzleLineCacheSession session,

      IReadOnlyList<NozzleTraceInputRow> rows,

      IProgress<NozzleTraceJobProgress> progress = null,

      CancellationToken cancellationToken = default(CancellationToken))

    {

      var result = new NozzleTraceJobResult();

      if (session == null)

      {

        result.Success = false;

        result.ErrorMessage = "먼저 라인 캐시를 로드하세요.";

        return result;

      }



      if (rows == null || rows.Count == 0)

      {

        result.Success = false;

        result.ErrorMessage = NozzleTraceUserMessages.Both("입력 행이 없습니다.", "没有输入行。");

        return result;

      }



      var validRows = rows

        .Where(r => r != null && r.IsValid)

        .Where(r => string.Equals(r.DbName?.Trim(), session.DbName, StringComparison.OrdinalIgnoreCase))

        .ToList();



      if (validRows.Count == 0)

      {

        result.Success = false;

        result.ErrorMessage = string.Format("유효한 입력이 없습니다. 로드된 DB는 '{0}'입니다.", session.DbName);

        return result;

      }



      if (session.BimIdStats != null && session.BimIdStats.UserWarnings.Count > 0)

        result.Warnings.AddRange(session.BimIdStats.UserWarnings);



      var traceService = session.Services.CreateNozzleTraceService(session.Services.CacheManager);

      int rowsDone = 0;

      int rowsTotal = validRows.Count;



      foreach (var row in validRows)

      {

        cancellationToken.ThrowIfCancellationRequested();

        row.LoadStatus = "OK";



        try

        {

          Report(progress,

            NozzleTraceUserMessages.Step(rowsDone + 1, rowsTotal, NozzleTraceUserMessages.Both("역추적: ", "反向追踪: ") + row.KeyValue),

            session.DbName, rowsDone, rowsTotal, (double)rowsDone / Math.Max(1, rowsTotal));



          var run = traceService.Run(row.ToInput());

          result.Runs.Add(run);

          result.TracePathCount += run.Traces?.Count ?? 0;



          if (run.Resolution != null && run.Resolution.Success)

          {

            row.TraceStatus = "Traced";

            Log.Information("[NozzleTrace] OK {Key} — paths={Count}", row.KeyValue, run.Traces?.Count ?? 0);

          }

          else

          {

            string code = run.Resolution?.ErrorCode ?? "Error";

            row.TraceStatus = code;

            string userMsg = NozzleTraceUserMessages.ForErrorCode(code, run.Resolution?.ErrorMessage);

            result.Warnings.Add(string.Format("[{0}] {1}: {2}", session.DbName, row.KeyValue, userMsg));

            Log.Warning("[NozzleTrace] {Key} — {Code}: {Msg}", row.KeyValue, code, userMsg);

          }

        }

        catch (Exception ex)

        {

          Log.Error(ex, "[NozzleTrace] Trace failed for {Key}", row.KeyValue);

          row.TraceStatus = "Error";

          result.Warnings.Add("[" + session.DbName + "] " + row.KeyValue + ": " + ex.Message);

          result.Runs.Add(new NozzleTraceRunResult

          {

            Input = row.ToInput(),

            Resolution = new NozzleStartResolution

            {

              ErrorCode = "EXCEPTION",

              ErrorMessage = ex.Message

            }

          });

        }



        rowsDone++;

      }



      string outDir = Path.Combine(

        GlobalConfig.DailyReportPath,

        "NozzleTrace",

        DateTime.Now.ToString("yyyyMMdd"));

      Directory.CreateDirectory(outDir);



      string outPath = Path.Combine(outDir,

        "NozzleTrace_" + DateTime.Now.ToString("HHmmss") + ".xlsx");



      Report(progress, NozzleTraceUserMessages.Both("Excel 저장 중…", "正在保存 Excel…"), session.DbName, rowsDone, rowsTotal, 0.95);

      await Task.Run(() => NozzleTraceExcelExporter.Export(outPath, result.Runs), cancellationToken)

        .ConfigureAwait(false);



      result.OutputExcelPath = outPath;

      result.InputsProcessed = validRows.Count;

      result.Success = true;



      Report(progress, NozzleTraceUserMessages.Both("완료 — 경로 ", "完成 — 路径 ") + result.TracePathCount + NozzleTraceUserMessages.Both("건, ", " 条, ") + Path.GetFileName(outPath),

        session.DbName, rowsDone, rowsTotal, 1.0);

      return result;

    }



    /// <summary>배치/CLI — DB별 캐시 로드 후 즉시 역추적.</summary>

    public async Task<NozzleTraceJobResult> RunAsync(

      IReadOnlyList<NozzleTraceInputRow> rows,

      IProgress<NozzleTraceJobProgress> progress = null,

      CancellationToken cancellationToken = default(CancellationToken))

    {

      var result = new NozzleTraceJobResult();

      if (rows == null || rows.Count == 0)

      {

        result.Success = false;

        result.ErrorMessage = NozzleTraceUserMessages.Both("입력 행이 없습니다.", "没有输入行。");

        return result;

      }



      var validRows = rows.Where(r => r != null && r.IsValid).ToList();

      if (validRows.Count == 0)

      {

        result.Success = false;

        result.ErrorMessage = "유효한 입력 행이 없습니다.";

        return result;

      }



      var lineGroups = validRows

        .GroupBy(r => r.DbName.Trim(), StringComparer.OrdinalIgnoreCase)

        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)

        .ToList();



      foreach (var lineGroup in lineGroups)

      {

        cancellationToken.ThrowIfCancellationRequested();

        var load = await LoadLineCacheAsync(lineGroup.Key, progress, cancellationToken).ConfigureAwait(false);

        if (!load.Success || load.Session == null)
        {
          string fail = load.ErrorMessage ?? NozzleTraceUserMessages.Both("캐시 로드 실패: ", "缓存加载失败: ") + lineGroup.Key;
          result.Warnings.Add(fail);
          Report(progress, fail, lineGroup.Key, 0, validRows.Count, 0);
          foreach (var row in lineGroup)
          {
            row.LoadStatus = "CacheFail";
            row.TraceStatus = "Skipped";
          }
          continue;
        }

        using (load.Session)
        {
          var traceResult = await RunTracesAsync(load.Session, lineGroup.ToList(), progress, cancellationToken)
            .ConfigureAwait(false);

          result.Runs.AddRange(traceResult.Runs);
          result.TracePathCount += traceResult.TracePathCount;
          result.Warnings.AddRange(traceResult.Warnings);
          if (!string.IsNullOrWhiteSpace(traceResult.OutputExcelPath))
            result.OutputExcelPath = traceResult.OutputExcelPath;
        }
      }



      result.InputsProcessed = validRows.Count;

      result.Success = result.Runs.Count > 0 || result.TracePathCount > 0;

      if (!result.Success && result.Warnings.Count > 0)

        result.ErrorMessage = string.Join(Environment.NewLine, result.Warnings.Take(3));



      return result;

    }



    private static void Report(

      IProgress<NozzleTraceJobProgress> progress,

      string message,

      string dbName,

      int rowsDone,

      int rowsTotal,

      double fraction)

    {

      progress?.Report(new NozzleTraceJobProgress

      {

        Message = message,

        DbName = dbName,

        RowsDone = rowsDone,

        RowsTotal = rowsTotal,

        Fraction = fraction

      });

    }

  }

}


