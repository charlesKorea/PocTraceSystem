using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Rule 4: 설비(Equipment) · Subdivision1에 GCS 포함 · Connector Information = STICK ·
  /// Connection Tag에 업로드 EQPID 포함. Sheet 헤더는 Rule03과 동일.
  /// </summary>
  public static class Rule04Projector
  {
    public const string SheetName = "Rule04";
    public const string RuleId = "Rule04";
    public const string RuleTitle = "GCS STICK ConnectionTag ↔ EQPID 매칭";

    /// <summary>Sheet3(Rule03) 헤더 그대로.</summary>
    public static readonly string[] Headers = Rule03Projector.Headers;

    public static List<object[]> Collect(IGraphCache cache, IEnumerable<string> eqpIds)
    {
      var rows = new List<object[]>();
      if (cache == null || eqpIds == null) return rows;

      var eqpList = EqpTextMatcher.NormalizeEqps(eqpIds);
      if (eqpList.Count == 0) return rows;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var poc in cache.EnumeratePocs())
      {
        if (poc == null) continue;
        if (string.IsNullOrWhiteSpace(poc.Information)) continue;
        if (!string.Equals(poc.Information.Trim(), "stick", StringComparison.OrdinalIgnoreCase))
          continue;
        if (string.IsNullOrWhiteSpace(poc.ReferenceTag)) continue;

        NodeInfo node;
        if (!cache.TryGetNode(poc.OwnerId, out node) || node == null)
          continue;
        if (node.Type != NodeType.Equipment)
          continue;
        if (string.IsNullOrWhiteSpace(node.Sub1Name) ||
            node.Sub1Name.IndexOf("gcs", StringComparison.OrdinalIgnoreCase) < 0)
          continue;

        foreach (var eqp in eqpList)
        {
          if (!EqpTextMatcher.IsExactMatch(poc.ReferenceTag, eqp))
            continue;

          string key = node.InstanceId.ToString("N") + "|" + poc.InstanceId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          rows.Add(Rule03Projector.ToRow(node, poc));
        }
      }

      return rows;
    }
  }
}
