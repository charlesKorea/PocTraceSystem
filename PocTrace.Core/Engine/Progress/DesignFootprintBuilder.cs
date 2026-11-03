using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>
  /// Trace pathResults Steps에서 설비별 Pipe/Fitting union footprint 구축.
  /// 배관만 설계 날짜에 반영 (설비·노즐 node 무시). 날짜는 배관 길이가 바뀐 기록 기준(PipeDesignDates),
  /// 이력을 못 쓰면 Pipe.OpTime.
  /// </summary>
  public sealed class DesignFootprintBuilder
  {
    private readonly IGraphCache _cache;

    public DesignFootprintBuilder(IGraphCache cache)
    {
      _cache = cache;
    }

    /// <summary>Trace 경로에 나온 배관 Guid (설계 날짜 이력 조회용).</summary>
    public static List<Guid> CollectPipeIds(IEnumerable<TraceResult> pathResults)
    {
      var seen = new HashSet<Guid>();
      if (pathResults == null) return new List<Guid>();
      foreach (var tr in pathResults)
      {
        if (tr?.Steps == null) continue;
        foreach (var step in tr.Steps)
        {
          if (step == null || step.NodeId == Guid.Empty) continue;
          if (IsPipeStep(step.NodeType ?? string.Empty))
            seen.Add(step.NodeId);
        }
      }
      return seen.ToList();
    }

    /// <param name="pipeDates">
    /// 배관별 설계 날짜(길이가 바뀐 기록 기준). null이면 Pipe.OpTime을 쓴다.
    /// 값이 있으면 거기 없는 배관은 날짜 없음으로 둔다.
    /// </param>
    public List<EquipmentFootprint> Build(
      IEnumerable<TraceResult> pathResults,
      IReadOnlyDictionary<Guid, PipeDesignDates> pipeDates = null)
    {
      var list = new List<EquipmentFootprint>();
      if (pathResults == null) return list;

      foreach (var group in pathResults.GroupBy(r => r.StartEquipmentTag ?? string.Empty))
      {
        if (string.IsNullOrWhiteSpace(group.Key)) continue;
        var fp = BuildOne(group.Key, group.ToList(), pipeDates);
        if (fp != null) list.Add(fp);
      }
      return list;
    }

    private EquipmentFootprint BuildOne(
      string tag,
      List<TraceResult> results,
      IReadOnlyDictionary<Guid, PipeDesignDates> pipeDates)
    {
      var mainTr = results.FirstOrDefault(p =>
          !string.IsNullOrEmpty(p.StartNodeSubDivision1) &&
          p.StartNodeSubDivision1.IndexOf("1-MAIN", StringComparison.OrdinalIgnoreCase) >= 0)
        ?? results.First();

      var pipeSeen = new HashSet<Guid>();
      var fittingSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // NodeId|QCode
      double totalLen = 0;
      DateTime? firstPipe = null;
      DateTime? lastPipe = null;
      var byUt = new Dictionary<string, UtilityAccum>(StringComparer.OrdinalIgnoreCase);

      foreach (var tr in results)
      {
        if (tr?.Steps == null) continue;
        // 경로상 직전 Pipe Utility — Fitting BOM을 UNKNOWN에만 몰지 않도록 귀속
        string lastPipeUtil = "UNKNOWN";
        foreach (var step in tr.Steps)
        {
          if (step == null) continue;
          string nt = step.NodeType ?? string.Empty;

          if (IsPipeStep(nt))
          {
            if (step.NodeId == Guid.Empty || !pipeSeen.Add(step.NodeId))
              continue;

            double len = step.Length;
            DateTime? op = step.OpTime;
            string util = "UNKNOWN";

            if (_cache != null && _cache.TryGetPipe(step.NodeId, out var pipe) && pipe != null)
            {
              if (len <= 0) len = pipe.Length;
              if (!op.HasValue) op = pipe.OpTime;
              if (!string.IsNullOrWhiteSpace(pipe.UtilityName))
                util = pipe.UtilityName.Trim();
            }

            lastPipeUtil = util;
            totalLen += len;

            DateTime? opFirst = op;
            DateTime? opLast = op;
            if (pipeDates != null)
            {
              PipeDesignDates dates;
              if (pipeDates.TryGetValue(step.NodeId, out dates) && dates != null)
              {
                opFirst = dates.FirstAt;
                opLast = dates.LastLengthChangeAt;
              }
              else
              {
                opFirst = null;
                opLast = null;
              }
            }

            if (opFirst.HasValue && (!firstPipe.HasValue || opFirst.Value < firstPipe.Value))
              firstPipe = opFirst;
            if (opLast.HasValue && (!lastPipe.HasValue || opLast.Value > lastPipe.Value))
              lastPipe = opLast;

            if (!byUt.TryGetValue(util, out var acc))
            {
              acc = new UtilityAccum();
              byUt[util] = acc;
            }
            acc.Length += len;
            if (opFirst.HasValue && (!acc.First.HasValue || opFirst.Value < acc.First.Value))
              acc.First = opFirst;
            if (opLast.HasValue && (!acc.Last.HasValue || opLast.Value > acc.Last.Value))
              acc.Last = opLast;
          }
          else if (IsFittingStep(nt))
          {
            string key = step.NodeId + "|" + (step.QCode ?? string.Empty);
            if (!fittingSeen.Add(key)) continue;

            // Fitting/Damper BOM: 동일 Trace 경로에서 직전에 본 Pipe Utility에 귀속
            string util = lastPipeUtil;
            if (!byUt.TryGetValue(util, out var acc))
            {
              acc = new UtilityAccum();
              byUt[util] = acc;
            }
            acc.Bom++;
          }
        }
      }

      int bom = fittingSeen.Count;

      var fp = new EquipmentFootprint
      {
        Tag = tag,
        EquipGuid = mainTr.StartNodeGUID,
        MainName = (mainTr.StartNodeName ?? string.Empty).Trim(),
        Process = !string.IsNullOrEmpty(mainTr.StartNodeSubDivision2) ? mainTr.StartNodeSubDivision2 : "-",
        TotalLength = totalLen,
        BomCount = bom,
        FirstPipeDesignAt = firstPipe,
        LastPipeDesignAt = lastPipe
      };

      foreach (var kv in byUt)
      {
        fp.ByUtility[kv.Key] = new UtilityFootprintSlice
        {
          Length = kv.Value.Length,
          BomCount = kv.Value.Bom,
          FirstPipeDesignAt = kv.Value.First,
          LastPipeDesignAt = kv.Value.Last
        };
      }

      return fp;
    }

    private static bool IsPipeStep(string nodeType)
    {
      return nodeType.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsFittingStep(string nodeType)
    {
      return nodeType.IndexOf("Fitting", StringComparison.OrdinalIgnoreCase) >= 0
          || string.Equals(nodeType, "Damper", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UtilityAccum
    {
      public double Length;
      public int Bom;
      public DateTime? First;
      public DateTime? Last;
    }
  }
}
