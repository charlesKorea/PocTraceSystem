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
  /// Rule 3: 시작점이 Nozzle — Connector Information에 "nozzle" 포함 +
  /// Connection Tag(ReferenceTag)에 업로드 EQPID 포함인 Nozzle POC/노드 추출.
  /// </summary>
  public static class Rule03Projector
  {
    public const string SheetName = "Rule03";
    public const string RuleId = "Rule03";
    public const string RuleTitle = "Nozzle ConnectionTag ↔ EQPID 매칭";

    public static readonly string[] Headers =
    {
      "Instance Id",
      "Bim Id",
      "Instance Name",
      "GUID",
      "Connection Tag",
      "Division",
      "Subdivision1",
      "Subdivision2"
    };

    /// <summary>
    /// 캐시에서 Nozzle 노드 POC를 스캔.
    /// Information ⊇ "nozzle"(무시 대소문자), ReferenceTag ⊇ 업로드 EQPID.
    /// </summary>
    public static List<object[]> Collect(IGraphCache cache, IEnumerable<string> eqpIds)
    {
      var rows = new List<object[]>();
      if (cache == null || eqpIds == null) return rows;

      var eqpList = EqpTextMatcher.NormalizeEqps(eqpIds);
      if (eqpList.Count == 0) return rows;

      // (nodeGuid, pocGuid, eqp) 중복 방지
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var poc in cache.EnumeratePocs())
      {
        if (poc == null) continue;
        if (string.IsNullOrWhiteSpace(poc.Information)) continue;
        if (poc.Information.IndexOf("nozzle", StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        if (string.IsNullOrWhiteSpace(poc.ReferenceTag)) continue;

        NodeInfo node;
        if (!cache.TryGetNode(poc.OwnerId, out node) || node == null)
          continue;
        if (node.Type != NodeType.Nozzle)
          continue;

        foreach (var eqp in eqpList)
        {
          if (!EqpTextMatcher.IsExactMatch(poc.ReferenceTag, eqp))
            continue;

          string key = node.InstanceId.ToString("N") + "|" + poc.InstanceId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          rows.Add(ToRow(node, poc));
        }
      }

      return rows;
    }

    public static object[] ToRow(NodeInfo node, PocData poc)
    {
      long instanceId = node.InstanceNumericId != 0
        ? node.InstanceNumericId
        : DotNetGuidHash.FromGuid(node.InstanceId);

      string division;
      if (node.Division == 0) division = "Equipment";
      else if (node.Division == 3) division = "PlumbingComponent";
      else if (node.Division == 1) division = "PlumbingComponent"; // Nozzle 계열
      else division = string.IsNullOrWhiteSpace(node.Division.ToString())
        ? string.Empty
        : node.Division.ToString();

      // GetDivision 스타일: Nozzle often maps differently — prefer cache helper names if Sub present
      if (node.Type == NodeType.Nozzle && node.Division != 0 && node.Division != 3)
        division = "PlumbingComponent";

      return new object[]
      {
        instanceId,
        node.BimId ?? string.Empty,
        node.Name ?? string.Empty,
        node.InstanceId.ToString(),
        poc.ReferenceTag ?? string.Empty,
        division,
        node.Sub1Name ?? string.Empty,
        node.Sub2Name ?? string.Empty
      };
    }
  }
}
