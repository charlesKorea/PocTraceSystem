using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Models;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "Status EMP": POC Trace 결과 중 Status ≠ EMP 전부.
  /// Validation Fail 여부와 무관. 행 = Connector GUID 단위(TraceResult 1행 = 시작 POC).
  /// </summary>
  public static class StatusEmpProjector
  {
    public const string SheetName = "Status EMP";

    public static readonly string[] Headers =
    {
      "Status",
      "S:BIM ID",
      "S:Instance ID",
      "S:Name",
      "S:Tag",
      "S:Construction Code",
      "S:GUID",
      "S:Connector GUID"
    };

    public static List<object[]> Collect(
      IEnumerable<TraceResult> traces,
      IEnumerable<string> eqpIds = null,
      EqpSheetCounter counter = null)
    {
      var rows = new List<object[]>();
      if (traces == null) return rows;

      var eqpList = eqpIds != null
        ? EquipmentInfoProjector.NormalizeEqp(eqpIds)
        : null;

      var seenConn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var r in traces)
      {
        if (r == null) continue;
        if (string.Equals(r.STATUS, "EMP", StringComparison.OrdinalIgnoreCase))
          continue;

        string connGuid = r.StartPocGUID ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connGuid))
          connGuid = r.StartPocId == Guid.Empty ? string.Empty : r.StartPocId.ToString();

        string key = (connGuid ?? string.Empty) + "|" + (r.StartNodeGUID.ToString());
        if (!seenConn.Add(key)) continue;

        rows.Add(new object[]
        {
          r.STATUS ?? string.Empty,
          r.BimId ?? string.Empty,
          r.InstanceNumericId,
          r.StartNodeName ?? string.Empty,
          r.StartEquipmentTag ?? r.StartNodeTag ?? string.Empty,
          r.ConstructionCode ?? string.Empty,
          r.StartNodeGUID.ToString(),
          connGuid
        });

        if (counter != null && eqpList != null)
        {
          string tag = r.StartEquipmentTag ?? r.StartNodeTag ?? string.Empty;
          foreach (var eqp in eqpList)
          {
            if (EqpTextMatcher.IsExactMatch(tag, eqp))
            {
              counter.Add(eqp, 1);
            }
          }
        }
      }

      return rows;
    }
  }
}
