using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using Serilog;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>
  /// Design Progress 일배치 엔트리. Trace pathResults → footprint → cohort → report DTO.
  /// </summary>
  public sealed class DesignProgressEngine
  {
    private readonly ILogger _log;

    public DesignProgressEngine(ILogger log = null)
    {
      _log = log ?? Log.Logger;
    }

    public ProgressReportDto Run(
      string dbName,
      IGraphCache cache,
      IList<TraceResult> pathResults,
      DesignProgressOptions options = null,
      IFrmEquipmentMaster frmMaster = null,
      IPipeDesignDateSource pipeDateSource = null)
    {
      options = options ?? DesignProgressOptionsFactory.FromGlobalConfig();
      if (!options.Enabled)
      {
        _log.Information("[DesignProgress] Disabled. Skipping {Db}.", dbName);
        return new ProgressReportDto { DbName = dbName, GenDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
      }

      frmMaster = frmMaster ?? FrmEquipmentMasterRepository.CreateFromConfig(_log);
      var frmRows = frmMaster.Load(dbName) ?? new List<FrmEquipmentRow>();
      var frmByTag = frmRows
        .Where(r => !string.IsNullOrWhiteSpace(r.Tag))
        .GroupBy(r => r.Tag.Trim(), System.StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);

      int totalResults = pathResults?.Count ?? 0;
      int withSteps = pathResults == null
        ? 0
        : pathResults.Count(r => r.Steps != null && r.Steps.Count > 0);
      if (totalResults > 0 && withSteps == 0)
      {
        _log.Warning(
          "[DesignProgress] {Db}: TraceResult.Steps empty for all {Count} results. LastPipeDesignAt/IsInProgress will be 0. Run before ReleaseTraceStepMemory.",
          dbName, totalResults);
      }

      // FRM ModelKey로 MainName 보강 (비어 있을 때)
      // 최근 설계일은 배관 길이가 바뀐 기록 기준. 이력을 못 읽으면 Pipe.OpTime으로 대신한다.
      pipeDateSource = pipeDateSource ?? PipeDesignDateRepository.CreateFromConfig(_log);
      var pipeIds = DesignFootprintBuilder.CollectPipeIds(pathResults);
      var pipeDates = pipeIds.Count > 0 ? pipeDateSource.Load(dbName, pipeIds) : null;
      if (pipeDates == null && pipeIds.Count > 0)
        _log.Information("[DesignProgress] {Db}: 길이 변경 이력 없음 — Pipe.OpTime 기준", dbName);

      var builder = new DesignFootprintBuilder(cache);
      var footprints = builder.Build(pathResults, pipeDates);
      foreach (var fp in footprints)
      {
        if (string.IsNullOrWhiteSpace(fp.MainName) && frmByTag.TryGetValue(fp.Tag, out var frm)
            && !string.IsNullOrWhiteSpace(frm.ModelKey))
          fp.MainName = frm.ModelKey.Trim();
      }

      var report = DesignProgressCalculator.Calculate(dbName, footprints, options, frmByTag);
      _log.Information(
        "[DesignProgress] {Db}: Equip={Eq}, InProgress={Ip}, LowConf={Lc}, AvgPct={Avg}",
        dbName, report.TotalEquip, report.InProgressCount, report.LowConfidenceCount, report.AvgProgressPct);
      return report;
    }
  }

  public static class DesignProgressOptionsFactory
  {
    public static DesignProgressOptions FromGlobalConfig()
    {
      return new DesignProgressOptions
      {
        Enabled = Infrastructure.GlobalConfig.DesignProgressEnabled,
        InProgressDays = Infrastructure.GlobalConfig.DesignProgressInProgressDays,
        IdleStableDays = Infrastructure.GlobalConfig.DesignProgressIdleStableDays,
        MinCohortSize = Infrastructure.GlobalConfig.DesignProgressMinCohortSize,
        LengthWeight = Infrastructure.GlobalConfig.DesignProgressLengthWeight,
        AsOfDate = System.DateTime.Today
      };
    }
  }
}
