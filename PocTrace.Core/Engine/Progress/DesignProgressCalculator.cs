using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>Freshness + Length/BOM % + Utility rollup.</summary>
  public static class DesignProgressCalculator
  {
    public static ProgressReportDto Calculate(
      string dbName,
      IList<EquipmentFootprint> footprints,
      DesignProgressOptions options,
      IReadOnlyDictionary<string, FrmEquipmentRow> frmByTag)
    {
      options = options ?? new DesignProgressOptions();
      frmByTag = frmByTag ?? new Dictionary<string, FrmEquipmentRow>(StringComparer.OrdinalIgnoreCase);
      footprints = footprints ?? new List<EquipmentFootprint>();

      var baselines = DesignCohortService.BuildBaselines(footprints, options, frmByTag);
      double wLen = Math.Max(0, Math.Min(1, options.LengthWeight));
      double wBom = 1.0 - wLen;
      var asOf = options.AsOfDate.Date;

      var equipDtos = new List<EquipmentProgressDto>();
      foreach (var fp in footprints.OrderBy(f => f.Tag, StringComparer.OrdinalIgnoreCase))
      {
        frmByTag.TryGetValue(fp.Tag ?? string.Empty, out var frm);

        bool inProgress = fp.LastPipeDesignAt.HasValue
          && fp.LastPipeDesignAt.Value.Date >= asOf.AddDays(-options.InProgressDays);

        // FRM OfficialStatus 우선
        if (frm != null && !string.IsNullOrWhiteSpace(frm.OfficialStatus))
        {
          var st = frm.OfficialStatus;
          if (st.IndexOf("inprogress", StringComparison.OrdinalIgnoreCase) >= 0
              || st.IndexOf("in progress", StringComparison.OrdinalIgnoreCase) >= 0
              || st.IndexOf("진행", StringComparison.OrdinalIgnoreCase) >= 0)
            inProgress = true;
          if (st.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0
              || st.IndexOf("완료", StringComparison.OrdinalIgnoreCase) >= 0)
            inProgress = false;
        }

        baselines.TryGetValue(fp.MainName ?? string.Empty, out var bl);
        int cohortN = bl?.N ?? 0;
        bool lowConf = cohortN < options.MinCohortSize;

        double baseLen = 0;
        double baseBom = 0;
        if (frm?.PlannedLengthMm != null && frm.PlannedLengthMm.Value > 0)
          baseLen = frm.PlannedLengthMm.Value;
        else if (bl != null)
          baseLen = bl.LengthMedian;

        if (frm?.PlannedBomCount != null && frm.PlannedBomCount.Value > 0)
          baseBom = frm.PlannedBomCount.Value;
        else if (bl != null)
          baseBom = bl.BomMedian;

        double lengthPct = Pct(fp.TotalLength, baseLen);
        double bomPct = Pct(fp.BomCount, baseBom);
        bool overrun = (baseLen > 0 && fp.TotalLength > baseLen) || (baseBom > 0 && fp.BomCount > baseBom);
        double progressPct = Round1(wLen * lengthPct + wBom * bomPct);

        var dto = new EquipmentProgressDto
        {
          Tag = fp.Tag,
          Guid = fp.EquipGuid.ToString(),
          MainName = fp.MainName,
          Process = fp.Process,
          FirstDesignAt = fp.FirstPipeDesignAt?.ToString("yyyy-MM-dd") ?? string.Empty,
          LastDesignAt = fp.LastPipeDesignAt?.ToString("yyyy-MM-dd") ?? string.Empty,
          DesignDays = ComputeDesignDays(fp.FirstPipeDesignAt, fp.LastPipeDesignAt),
          IsInProgress = inProgress,
          IsCohortEligible = DesignCohortService.IsCohortEligible(fp, options, frmByTag),
          LowConfidence = lowConf || baseLen <= 0 && baseBom <= 0,
          Overrun = overrun,
          CurrentLength = Round1(fp.TotalLength),
          CurrentBomCount = fp.BomCount,
          BaselineLengthMedian = Round1(baseLen),
          BaselineBomMedian = Round1(baseBom),
          CohortN = cohortN,
          LengthPct = lengthPct,
          BomPct = bomPct,
          ProgressPct = progressPct,
          FrmStatus = frm?.OfficialStatus
        };

        // Utility deep-dive
        foreach (var kv in fp.ByUtility.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
          double uBaseLen = 0;
          double uBaseBom = 0;
          if (bl != null && bl.ByUtility.TryGetValue(kv.Key, out var ub))
          {
            uBaseLen = ub.LengthMedian;
            uBaseBom = ub.BomMedian;
          }
          double uLenPct = Pct(kv.Value.Length, uBaseLen);
          double uBomPct = Pct(kv.Value.BomCount, uBaseBom);
          dto.Utilities.Add(new UtilityProgressDto
          {
            UtilityName = kv.Key,
            CurrentLength = Round1(kv.Value.Length),
            CurrentBomCount = kv.Value.BomCount,
            BaselineLengthMedian = Round1(uBaseLen),
            BaselineBomMedian = Round1(uBaseBom),
            LengthPct = uLenPct,
            BomPct = uBomPct,
            ProgressPct = Round1(wLen * uLenPct + wBom * uBomPct),
            Overrun = (uBaseLen > 0 && kv.Value.Length > uBaseLen)
                      || (uBaseBom > 0 && kv.Value.BomCount > uBaseBom)
          });
        }

        equipDtos.Add(dto);
      }

      int inProg = equipDtos.Count(e => e.IsInProgress);
      int low = equipDtos.Count(e => e.LowConfidence);
      double avg = equipDtos.Count > 0
        ? Round1(equipDtos.Average(e => e.ProgressPct))
        : 0;

      return new ProgressReportDto
      {
        DbName = dbName,
        GenDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        TotalEquip = equipDtos.Count,
        InProgressCount = inProg,
        LowConfidenceCount = low,
        AvgProgressPct = avg,
        Equipments = equipDtos
      };
    }

    private static int? ComputeDesignDays(DateTime? first, DateTime? last)
    {
      if (!first.HasValue || !last.HasValue) return null;
      return (last.Value.Date - first.Value.Date).Days;
    }

    private static double Pct(double current, double baseline)
    {
      if (baseline <= 0) return 0;
      return Math.Min(100, Round1(current / baseline * 100.0));
    }

    private static double Round1(double v) => Math.Round(v, 1);
  }
}
