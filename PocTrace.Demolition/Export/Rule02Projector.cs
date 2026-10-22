using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Rule 2: Rule01 결과 중 FailCount≥1 &amp; STATUS≠EMP → Steps 경로 객체 전개.
  /// 헤더는 Rule01에서 Check Result / Fail Count / Issue Type 제외.
  /// EQPID = 시작 설비 Tag(업로드 EQPID). Instance Id: Node=캐시 NumericId, Pipe=PipeId 해시(공란 금지).
  /// </summary>
  public static class Rule02Projector
  {
    public const string SheetName = "Rule02";
    public const string RuleId = "Rule02";
    public const string RuleTitle = "Fail 경로 전체 객체 (Steps 전개)";

    private static readonly HashSet<string> ExcludedHeaders =
      new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        "Check Result",
        "Fail Count",
        "Issue Type"
      };

    public static readonly string[] Headers =
      Rule01Projector.Headers.Where(h => !ExcludedHeaders.Contains(h)).ToArray();

    public static bool IsRule02Source(TraceResult r)
    {
      if (r == null) return false;
      if (r.FailCount < 1) return false;
      if (string.Equals(r.STATUS, "EMP", StringComparison.OrdinalIgnoreCase))
        return false;
      return r.Steps != null && r.Steps.Count > 0;
    }

    /// <summary>캐시에 GUID가 있는 Steps만 행으로 전개 (제외 없음 — Node/Pipe/Takeoff/Poc).</summary>
    public static IEnumerable<object[]> Expand(TraceResult parent, IGraphCache cache)
    {
      if (!IsRule02Source(parent) || cache == null)
        yield break;

      string eqpId = parent.StartEquipmentTag ?? parent.StartNodeTag ?? string.Empty;

      foreach (var step in parent.Steps)
      {
        if (step == null || step.NodeId == Guid.Empty)
          continue;

        object[] row;
        if (!TryBuildRow(parent, step, cache, eqpId, out row))
          continue;
        yield return row;
      }
    }

    public static List<object[]> ExpandAll(IEnumerable<TraceResult> results, IGraphCache cache)
    {
      var list = new List<object[]>();
      if (results == null || cache == null) return list;
      foreach (var r in results)
        list.AddRange(Expand(r, cache));
      return list;
    }

    private static bool TryBuildRow(
      TraceResult parent,
      TraceStep step,
      IGraphCache cache,
      string eqpId,
      out object[] row)
    {
      row = null;
      long instanceNumericId;
      string instanceName = step.NodeName ?? string.Empty;
      string constructionCode = string.Empty;
      string division = step.Division ?? string.Empty;
      string sub1 = string.Empty;
      string sub2 = string.Empty;
      Guid guid = step.NodeId;
      bool inCache = false;

      NodeInfo node;
      if (cache.TryGetNode(step.NodeId, out node) && node != null)
      {
        inCache = true;
        instanceNumericId = node.InstanceNumericId != 0
          ? node.InstanceNumericId
          : DotNetGuidHash.FromGuid(node.InstanceId);
        instanceName = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : instanceName;
        constructionCode = node.ConstructionCode ?? string.Empty;
        division = FormatDivision(node.Division, division);
        sub1 = node.Sub1Name ?? string.Empty;
        sub2 = node.Sub2Name ?? string.Empty;
        guid = node.InstanceId;
      }
      else
      {
        PipeData pipe;
        if (cache.TryGetPipe(step.NodeId, out pipe) && pipe != null)
        {
          inCache = true;
          // Pipe류: PipeId → InstanceNumericId (GetDotNetHashCode와 동일 계열, 공란 금지)
          instanceNumericId = DotNetGuidHash.FromGuid(pipe.PipeId);
          if (string.IsNullOrWhiteSpace(instanceName))
            instanceName = string.IsNullOrWhiteSpace(pipe.UtilityName)
              ? "Pipe"
              : "Pipe(" + pipe.UtilityName + ")";
          guid = pipe.PipeId;
          if (string.IsNullOrWhiteSpace(division))
            division = "MEP";
        }
        else
        {
          TakeoffData takeoff;
          if (cache.TryGetTakeoff(step.NodeId, out takeoff) && takeoff != null)
          {
            inCache = true;
            instanceNumericId = DotNetGuidHash.FromGuid(takeoff.TakeoffId);
            if (string.IsNullOrWhiteSpace(instanceName))
              instanceName = string.IsNullOrWhiteSpace(takeoff.Tag)
                ? "Takeoff"
                : "Takeoff[" + takeoff.Tag + "]";
            guid = takeoff.TakeoffId;
          }
          else
          {
            PocData poc;
            if (cache.TryGetPoc(step.NodeId, out poc) && poc != null)
            {
              inCache = true;
              instanceNumericId = DotNetGuidHash.FromGuid(poc.InstanceId);
              if (string.IsNullOrWhiteSpace(instanceName))
                instanceName = poc.PocName ?? poc.Tag ?? "POC";
              guid = poc.InstanceId;
            }
            else
            {
              return false; // 캐시에 GUID 없음 → 스킵
            }
          }
        }
      }

      if (!inCache) return false;

      // Rule01 헤더 순서에서 Check Result / Fail Count / Issue Type만 제거한 값 배열
      var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
      {
        ["Instance Id"] = instanceNumericId,
        ["Instance Name"] = instanceName ?? string.Empty,
        ["EQPID"] = eqpId,
        ["Construction Code"] = constructionCode,
        ["Division"] = division ?? string.Empty,
        ["Subdivision1"] = sub1,
        ["Subdivision2"] = sub2,
        ["GUID"] = guid.ToString(),
        ["Feedback"] = string.Empty,
        ["Issue Owner"] = string.Empty,
        ["Line"] = parent.Line ?? string.Empty,
        ["S: Connector GUID"] = string.Empty,
        ["분기배관수"] = string.Empty,
        ["S: Connector Tag"] = string.Empty,
        ["S: Connector Name"] = string.Empty,
        ["S: Connector Information"] = string.Empty,
        ["S: Connection Tag"] = string.Empty,
        ["S: Module Name"] = string.Empty,
        ["S: PocNo"] = string.Empty,
        ["Status"] = parent.Status ?? string.Empty,
        ["T: Name"] = string.Empty,
        ["T: Tag"] = string.Empty,
        ["T: GUID"] = string.Empty,
        ["T: Connector GUID"] = string.Empty,
        ["T: Connector Name"] = string.Empty,
        ["T: Connector Information"] = string.Empty,
        ["T: Connection Tag"] = string.Empty,
        ["T: Module Name"] = string.Empty,
        ["T: PocNo"] = string.Empty,
        ["T: Division"] = string.Empty,
        ["T: Subdivision1"] = string.Empty,
        ["T: Subdivision2"] = string.Empty,
        ["S: Utility Group"] = string.Empty,
        ["S: Utility"] = string.Empty,
        ["S: Connected UT"] = string.Empty,
        ["T: Connected UT"] = string.Empty,
        ["T: Utility"] = string.Empty,
        ["S: Size"] = string.Empty
      };

      // 시작 설비 스텝이면 부모 S:* 일부를 채움 (동일 GUID)
      if (parent.StartNodeGUID != Guid.Empty && parent.StartNodeGUID == guid)
      {
        map["S: Connector GUID"] = parent.StartPocGUID ?? string.Empty;
        map["분기배관수"] = parent.BranchedPipes;
        map["S: Connector Tag"] = parent.StartPocTag ?? string.Empty;
        map["S: Connector Name"] = parent.StartPocName ?? string.Empty;
        map["S: Connector Information"] = parent.StartPocInformation ?? string.Empty;
        map["S: Connection Tag"] = parent.StartPocConnectionTag ?? string.Empty;
        map["S: Module Name"] = parent.StartPocModuleName ?? string.Empty;
        map["S: PocNo"] = parent.StartPocNo ?? string.Empty;
        map["S: Utility Group"] = parent.StartPocUtilityGroup ?? string.Empty;
        map["S: Utility"] = parent.StartPocUtilityName ?? string.Empty;
        map["S: Connected UT"] = parent.StartPocConnectedUtility ?? string.Empty;
        map["S: Size"] = parent.StartPocSize ?? string.Empty;
      }

      // 종료 노드 스텝이면 T:* 채움
      if (parent.TargetNodeGUID != Guid.Empty && parent.TargetNodeGUID == guid)
      {
        map["T: Name"] = parent.TargetNodeName ?? string.Empty;
        map["T: Tag"] = parent.TargetNodeTag ?? string.Empty;
        map["T: GUID"] = parent.TargetNodeGUID.ToString();
        map["T: Connector GUID"] = parent.TargetPocGUID ?? string.Empty;
        map["T: Connector Name"] = parent.TargetPocName ?? string.Empty;
        map["T: Connector Information"] = parent.TargetConnectorInformation ?? string.Empty;
        map["T: Connection Tag"] = parent.TargetPocConnectionTag ?? string.Empty;
        map["T: Module Name"] = parent.TargetPocModuleName ?? string.Empty;
        map["T: PocNo"] = parent.TargetPocNo ?? string.Empty;
        map["T: Division"] = parent.TargetNodeDivision ?? string.Empty;
        map["T: Subdivision1"] = parent.TargetNodeSubDivision1 ?? string.Empty;
        map["T: Subdivision2"] = parent.TargetNodeSubDivision2 ?? string.Empty;
        map["T: Connected UT"] = parent.TargetPocConnectedUtility ?? string.Empty;
        map["T: Utility"] = parent.TargetPocUtilityName ?? string.Empty;
      }

      row = new object[Headers.Length];
      for (int i = 0; i < Headers.Length; i++)
      {
        object v;
        row[i] = map.TryGetValue(Headers[i], out v) ? v : string.Empty;
      }
      return true;
    }

    private static string FormatDivision(int divisionCode, string fallback)
    {
      if (divisionCode == 0) return "Equipment";
      if (divisionCode == 3) return "PlumbingComponent";
      if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
      return divisionCode.ToString();
    }
  }
}
