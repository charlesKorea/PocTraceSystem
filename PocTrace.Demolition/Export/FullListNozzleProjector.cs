using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Demolition.Input;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "Nozzle": GraphCache에서 Nozzle POC를 역매칭(Info⊇nozzle, ReferenceTag↔EQPID),
  /// S: 열은 해당 EQPID 설비 시작 POC 형태로 채움 (Full List Excel 불필요 — OOM 회피).
  /// </summary>
  public static class FullListNozzleProjector
  {
    public const string SheetName = "Nozzle";

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
      "T:Connector Information",
      "T:Division",
      "T:GUID"
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
        if (poc.Information.IndexOf("nozzle", StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        if (string.IsNullOrWhiteSpace(poc.ReferenceTag)) continue;

        NodeInfo nozzleNode;
        if (!cache.TryGetNode(poc.OwnerId, out nozzleNode) || nozzleNode == null)
          continue;

        var matched = MatchingEqps(poc.ReferenceTag, eqpList);
        if (matched.Count == 0) continue;

        foreach (var eqp in matched)
        {
          string key = poc.InstanceId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          NodeInfo startNode;
          PocData startPoc;
          if (!CacheEqpStartResolver.TryResolve(cache, eqp, out startNode, out startPoc))
          {
            // 설비 시작점 없으면 노즐 소유 노드를 S:로 (최후)
            startNode = nozzleNode;
            startPoc = poc;
          }

          rows.Add(new object[]
          {
            "NOZ",
            startNode.BimId ?? string.Empty,
            CacheEqpStartResolver.InstanceId(startNode),
            startNode.Name ?? string.Empty,
            startNode.InstanceId.ToString(),
            startPoc.InstanceId.ToString(),
            eqp,
            poc.ReferenceTag ?? string.Empty,
            poc.Information ?? string.Empty,
            CacheEqpStartResolver.FormatDivision(nozzleNode),
            nozzleNode.InstanceId.ToString()
          });

          if (counter != null) counter.Add(eqp, 2);
        }
      }

      return rows;
    }

    /// <summary>레거시 Full List 행 경로 (미사용·호환용).</summary>
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
        if (string.IsNullOrWhiteSpace(r.TConnectorInformation)) continue;
        if (r.TConnectorInformation.IndexOf("nozzle", StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        if (string.IsNullOrWhiteSpace(r.TConnectionTag)) continue;

        var matched = MatchingEqps(r.TConnectionTag, eqpList);
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
          r.TConnectorInformation ?? string.Empty,
          r.TDivision ?? string.Empty,
          r.TGuid ?? string.Empty
        });

        if (counter != null)
        {
          foreach (var eqp in matched)
            counter.Add(eqp, 2);
        }
      }

      return rows;
    }

    internal static List<string> MatchingEqps(string field, List<string> eqpList)
    {
      return EqpTextMatcher.FindMatches(field, eqpList);
    }
  }
}
