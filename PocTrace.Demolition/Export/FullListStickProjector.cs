using System;
using System.Collections.Generic;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Demolition.Input;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "Stick": GraphCache에서 Stick POC 역매칭(Info⊇stick, ReferenceTag↔EQPID),
  /// S:는 설비 시작 POC 형태. Full List Excel 미사용.
  /// </summary>
  public static class FullListStickProjector
  {
    public const string SheetName = "Stick";

    public static readonly string[] Headers =
    {
      "Status",
      "S:BIM ID",
      "S:Instance ID",
      "S:Name",
      "S:GUID",
      "S:Connector GUID",
      "S:Tag",
      "T:Connection Tag",
      "T:Connector Info"
    };

    public static List<object[]> Collect(
      IGraphCache cache,
      IEnumerable<string> eqpIds,
      EqpSheetCounter counter = null)
    {
      var rows = new List<object[]>();
      if (cache == null) return rows;
      var eqpList = EquipmentInfoProjector.NormalizeEqp(eqpIds);
      if (eqpList.Count == 0) return rows;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var poc in cache.EnumeratePocs())
      {
        if (poc == null) continue;
        if (string.IsNullOrWhiteSpace(poc.Information)) continue;
        if (poc.Information.IndexOf("stick", StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        if (string.IsNullOrWhiteSpace(poc.ReferenceTag)) continue;

        NodeInfo owner;
        if (!cache.TryGetNode(poc.OwnerId, out owner) || owner == null)
          continue;

        var matched = FullListNozzleProjector.MatchingEqps(poc.ReferenceTag, eqpList);
        if (matched.Count == 0) continue;

        foreach (var eqp in matched)
        {
          string key = poc.InstanceId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          NodeInfo startNode;
          PocData startPoc;
          if (!CacheEqpStartResolver.TryResolve(cache, eqp, out startNode, out startPoc))
          {
            startNode = owner;
            startPoc = poc;
          }

          rows.Add(new object[]
          {
            "EQP",
            startNode.BimId ?? string.Empty,
            CacheEqpStartResolver.InstanceId(startNode),
            startNode.Name ?? string.Empty,
            startNode.InstanceId.ToString(),
            startPoc.InstanceId.ToString(),
            eqp,
            poc.ReferenceTag ?? string.Empty,
            poc.Information ?? string.Empty
          });

          if (counter != null) counter.Add(eqp, 3);
        }
      }

      return rows;
    }

    public static List<object[]> Collect(
      IList<FullTraceListLoader.FullTraceRow> fullRows,
      IEnumerable<string> eqpIds,
      EqpSheetCounter counter = null)
    {
      var rows = new List<object[]>();
      if (fullRows == null || fullRows.Count == 0) return rows;
      var eqpList = EquipmentInfoProjector.NormalizeEqp(eqpIds);
      if (eqpList.Count == 0) return rows;

      foreach (var r in fullRows)
      {
        if (r == null) continue;
        if (string.IsNullOrWhiteSpace(r.SConnectorInformation)) continue;
        if (r.SConnectorInformation.IndexOf("stick", StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        if (string.IsNullOrWhiteSpace(r.SConnectionTag)) continue;

        var matched = FullListNozzleProjector.MatchingEqps(r.SConnectionTag, eqpList);
        if (matched.Count == 0) continue;

        rows.Add(new object[]
        {
          r.Status ?? string.Empty,
          r.SBimId ?? string.Empty,
          r.SInstanceId ?? string.Empty,
          string.Empty, // S:Name — Full List 레거시 경로에 Name 컬럼 없음
          r.SGuid ?? string.Empty,
          r.SConnectorGuid ?? string.Empty,
          r.STag ?? string.Empty,
          r.TConnectionTag ?? string.Empty,
          r.TConnectorInformation ?? string.Empty
        });

        if (counter != null)
        {
          foreach (var eqp in matched)
            counter.Add(eqp, 3);
        }
      }

      return rows;
    }
  }
}
