using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "설비정보": GraphCache Equipment + 철거 전용 3-% Template Equipment.
  /// POC Trace 없음. 행 = GUID 단위.
  /// </summary>
  public static class EquipmentInfoProjector
  {
    public static readonly string SheetName = DemolitionUiText.SheetEquipmentInfo;

    public static readonly string[] Headers =
    {
      "Instance ID",
      "Instance Name",
      "Tag",
      "Construction Code",
      "Division",
      "SubDivision1",
      "SubDivision2",
      "GUID"
    };

    public static List<object[]> Collect(
      IGraphCache cache,
      IEnumerable<string> eqpIds,
      EqpSheetCounter counter = null,
      IEnumerable<NodeInfo> templateNodes = null)
    {
      var rows = new List<object[]>();
      if (eqpIds == null) return rows;

      var eqpList = NormalizeEqp(eqpIds);
      if (eqpList.Count == 0) return rows;

      var seenGuid = new HashSet<Guid>();

      if (cache != null)
      {
        foreach (var node in cache.EnumerateNodes())
          TryAdd(node, eqpList, seenGuid, rows, counter);
      }

      // 철거 전용 sidecar: Subdivision1 LIKE '3-%' (GraphCache에 없음)
      if (templateNodes != null)
      {
        foreach (var node in templateNodes)
          TryAdd(node, eqpList, seenGuid, rows, counter);
      }

      return rows;
    }

    private static void TryAdd(
      NodeInfo node,
      List<string> eqpList,
      HashSet<Guid> seenGuid,
      List<object[]> rows,
      EqpSheetCounter counter)
    {
      if (node == null || node.InstanceId == Guid.Empty) return;
      if (node.Type != NodeType.Equipment) return;
      if (string.IsNullOrWhiteSpace(node.Tag)) return;
      if (!seenGuid.Add(node.InstanceId)) return;

      bool any = false;
      foreach (var eqp in eqpList)
      {
        if (EqpTextMatcher.IsExactMatch(node.Tag, eqp))
        {
          any = true;
          if (counter != null) counter.Add(eqp, 0);
        }
      }
      if (!any) return;

      long instanceId = node.InstanceNumericId != 0
        ? node.InstanceNumericId
        : DotNetGuidHash.FromGuid(node.InstanceId);

      string division = node.Division == 0 ? "Equipment"
        : node.Division == 3 ? "PlumbingComponent"
        : node.Division.ToString();

      rows.Add(new object[]
      {
        instanceId,
        node.Name ?? string.Empty,
        node.Tag ?? string.Empty,
        node.ConstructionCode ?? string.Empty,
        division,
        node.Sub1Name ?? string.Empty,
        node.Sub2Name ?? string.Empty,
        node.InstanceId.ToString()
      });
    }

    internal static List<string> NormalizeEqp(IEnumerable<string> eqpIds)
    {
      return EqpTextMatcher.NormalizeEqps(eqpIds);
    }
  }
}
