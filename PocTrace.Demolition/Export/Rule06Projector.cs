using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Rule 6: 전체 배관 Hookup Number 중 업로드 EQPID 포함 건.
  /// 헤더 = Rule05(Sheet3 계열) + Hookup Number 열.
  /// </summary>
  public static class Rule06Projector
  {
    public const string SheetName = "Rule06";
    public const string RuleId = "Rule06";
    public const string RuleTitle = "Pipe Hookup Number ↔ EQPID 매칭";

    public static readonly string[] Headers =
      Rule05Projector.Headers.Concat(new[] { "Hookup Number" }).ToArray();

    public static List<object[]> Collect(IGraphCache cache, IEnumerable<string> eqpIds)
    {
      var rows = new List<object[]>();
      if (cache == null || eqpIds == null) return rows;

      var eqpList = EqpTextMatcher.NormalizeEqps(eqpIds);
      if (eqpList.Count == 0) return rows;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var pipe in cache.EnumeratePipes())
      {
        if (pipe == null || pipe.PipeId == Guid.Empty) continue;

        string hookup = cache.GetHookupNumber(pipe.PipeId) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(hookup)) continue;

        foreach (var eqp in eqpList)
        {
          if (!EqpTextMatcher.IsExactMatch(hookup, eqp))
            continue;

          string key = pipe.PipeId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          rows.Add(ToRow(pipe, hookup));
        }
      }

      return rows;
    }

    public static object[] ToRow(PipeData pipe, string hookupNumber)
    {
      long instanceId = DotNetGuidHash.FromGuid(pipe.PipeId);
      string name = string.IsNullOrWhiteSpace(pipe.UtilityName)
        ? "Pipe"
        : "Pipe(" + pipe.UtilityName + ")";

      return new object[]
      {
        instanceId,
        string.Empty, // Bim Id
        name,         // Instance Name
        pipe.PipeId.ToString(),
        string.Empty, // Connection Tag
        "MEP",        // Division (배관)
        string.Empty, // Subdivision1
        string.Empty, // Subdivision2
        hookupNumber ?? string.Empty
      };
    }
  }
}
