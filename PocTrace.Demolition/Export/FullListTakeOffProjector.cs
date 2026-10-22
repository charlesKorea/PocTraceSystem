using System;
using System.Collections.Generic;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Demolition.Input;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "TakeOff": GraphCache TakeoffLog.Tag↔EQPID 역매칭,
  /// S:는 설비 시작 POC 형태. Full List Excel 미사용.
  /// </summary>
  public static class FullListTakeOffProjector
  {
    public const string SheetName = "TakeOff";

    public static readonly string[] Headers =
    {
      "Status",
      "S:BIM ID",
      "S:Instance ID",
      "S:Name",
      "S:GUID",
      "S:Connector GUID",
      "S:Tag",
      "T:Tag",
      "T:Instance Name",
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

      foreach (var takeoff in cache.EnumerateTakeoffs())
      {
        if (takeoff == null || string.IsNullOrWhiteSpace(takeoff.Tag)) continue;

        var matched = FullListNozzleProjector.MatchingEqps(takeoff.Tag, eqpList);
        if (matched.Count == 0) continue;

        foreach (var eqp in matched)
        {
          string key = takeoff.TakeoffId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          NodeInfo startNode;
          PocData startPoc;
          string sBim = string.Empty;
          long sInst = 0;
          string sName = string.Empty;
          string sGuid = string.Empty;
          string sConn = string.Empty;

          if (CacheEqpStartResolver.TryResolve(cache, eqp, out startNode, out startPoc))
          {
            sBim = startNode.BimId ?? string.Empty;
            sInst = CacheEqpStartResolver.InstanceId(startNode);
            sName = startNode.Name ?? string.Empty;
            sGuid = startNode.InstanceId.ToString();
            sConn = startPoc.InstanceId.ToString();
          }

          rows.Add(new object[]
          {
            "TOF",
            sBim,
            sInst,
            sName,
            sGuid,
            sConn,
            eqp,
            takeoff.Tag ?? string.Empty,
            takeoff.Tag ?? string.Empty,
            takeoff.TakeoffId.ToString()
          });

          if (counter != null) counter.Add(eqp, 4);
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
        if (!string.Equals(r.Status, "TOF", StringComparison.OrdinalIgnoreCase))
          continue;
        if (string.IsNullOrWhiteSpace(r.TTag)) continue;

        var matched = FullListNozzleProjector.MatchingEqps(r.TTag, eqpList);
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
          r.TTag ?? string.Empty,
          r.TName ?? string.Empty,
          r.TGuid ?? string.Empty
        });

        if (counter != null)
        {
          foreach (var eqp in matched)
            counter.Add(eqp, 4);
        }
      }

      return rows;
    }
  }
}
