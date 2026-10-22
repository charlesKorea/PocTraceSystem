using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>MainName 코호트 → median / IQR 베이스라인.</summary>
  public static class DesignCohortService
  {
    public static Dictionary<string, CohortBaseline> BuildBaselines(
      IList<EquipmentFootprint> footprints,
      DesignProgressOptions options,
      IReadOnlyDictionary<string, FrmEquipmentRow> frmByTag)
    {
      options = options ?? new DesignProgressOptions();
      frmByTag = frmByTag ?? new Dictionary<string, FrmEquipmentRow>(StringComparer.OrdinalIgnoreCase);

      var eligible = footprints
        .Where(f => IsCohortEligible(f, options, frmByTag))
        .Where(f => !string.IsNullOrWhiteSpace(f.MainName))
        .ToList();

      var map = new Dictionary<string, CohortBaseline>(StringComparer.OrdinalIgnoreCase);
      foreach (var g in eligible.GroupBy(f => f.MainName.Trim(), StringComparer.OrdinalIgnoreCase))
      {
        var rows = g.ToList();
        var bl = new CohortBaseline
        {
          MainName = g.Key,
          N = rows.Count,
          LengthMedian = Median(rows.Select(r => r.TotalLength)),
          LengthP25 = Percentile(rows.Select(r => r.TotalLength), 0.25),
          LengthP75 = Percentile(rows.Select(r => r.TotalLength), 0.75),
          BomMedian = Median(rows.Select(r => (double)r.BomCount)),
          BomP25 = Percentile(rows.Select(r => (double)r.BomCount), 0.25),
          BomP75 = Percentile(rows.Select(r => (double)r.BomCount), 0.75)
        };

        var utilKeys = rows.SelectMany(r => r.ByUtility.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var ut in utilKeys)
        {
          var slices = rows
            .Where(r => r.ByUtility.ContainsKey(ut))
            .Select(r => r.ByUtility[ut])
            .ToList();
          if (slices.Count == 0) continue;
          bl.ByUtility[ut] = new UtilityBaseline
          {
            N = slices.Count,
            LengthMedian = Median(slices.Select(s => s.Length)),
            BomMedian = Median(slices.Select(s => (double)s.BomCount))
          };
        }

        map[g.Key] = bl;
      }
      return map;
    }

    public static bool IsCohortEligible(
      EquipmentFootprint f,
      DesignProgressOptions options,
      IReadOnlyDictionary<string, FrmEquipmentRow> frmByTag)
    {
      if (f == null) return false;
      if (frmByTag != null && frmByTag.TryGetValue(f.Tag ?? string.Empty, out var frm)
          && !string.IsNullOrWhiteSpace(frm.OfficialStatus)
          && frm.OfficialStatus.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

      if (!f.LastPipeDesignAt.HasValue) return false;
      var asOf = options.AsOfDate.Date;
      // 진행중이면 코호트 제외
      if (f.LastPipeDesignAt.Value.Date >= asOf.AddDays(-options.InProgressDays))
        return false;
      // IdleStableDays 동안 갱신 없음
      return f.LastPipeDesignAt.Value.Date <= asOf.AddDays(-options.IdleStableDays);
    }

    public static double Median(IEnumerable<double> values)
    {
      var arr = values.OrderBy(v => v).ToList();
      if (arr.Count == 0) return 0;
      int mid = arr.Count / 2;
      if (arr.Count % 2 == 1) return arr[mid];
      return (arr[mid - 1] + arr[mid]) / 2.0;
    }

    public static double Percentile(IEnumerable<double> values, double p)
    {
      var arr = values.OrderBy(v => v).ToList();
      if (arr.Count == 0) return 0;
      if (arr.Count == 1) return arr[0];
      double idx = p * (arr.Count - 1);
      int lo = (int)Math.Floor(idx);
      int hi = (int)Math.Ceiling(idx);
      if (lo == hi) return arr[lo];
      double w = idx - lo;
      return arr[lo] * (1 - w) + arr[hi] * w;
    }
  }
}
