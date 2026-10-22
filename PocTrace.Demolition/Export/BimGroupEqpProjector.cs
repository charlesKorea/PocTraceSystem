using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "Group" (7번째): ConfigurationGroupLog Name=EQPID + ParentID 경로.
  /// Path = parentName\...\eqpId. 미존재 → Path "x", 종합 수량 0.
  /// </summary>
  public static class BimGroupEqpProjector
  {
    public const string SheetName = "Group";

    public static readonly string[] Headers =
    {
      "EQPID",
      "Path"
    };

    public static List<object[]> Collect(
      BimGroupNameIndex index,
      IEnumerable<string> eqpIds,
      EqpSheetCounter counter = null)
    {
      var rows = new List<object[]>();
      if (index == null) return rows;

      foreach (var item in index.BuildRows(eqpIds ?? Enumerable.Empty<string>(), includeUnmatchedAsX: true))
      {
        rows.Add(new object[] { item.EqpId, item.Path ?? "x" });
        if (counter != null && item.Matched)
          counter.Add(item.EqpId, 6);
      }

      return rows;
    }
  }
}
