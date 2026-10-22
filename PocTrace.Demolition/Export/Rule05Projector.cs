using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Rule 5: Takeoff — S:Tag(Damper[eqpid] 형태)에 업로드 EQPID 포함.
  /// 헤더는 Rule03/04와 동일. 채울 수 없는 열(Bim Id, Connection Tag, Division/Sub*)은 공란.
  /// </summary>
  public static class Rule05Projector
  {
    public const string SheetName = "Rule05";
    public const string RuleId = "Rule05";
    public const string RuleTitle = "Takeoff Tag Damper[EQPID] 매칭";

    public static readonly string[] Headers = Rule03Projector.Headers;

    public static List<object[]> Collect(IGraphCache cache, IEnumerable<string> eqpIds)
    {
      var rows = new List<object[]>();
      if (cache == null || eqpIds == null) return rows;

      var eqpList = EqpTextMatcher.NormalizeEqps(eqpIds);
      if (eqpList.Count == 0) return rows;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var takeoff in cache.EnumerateTakeoffs())
      {
        if (takeoff == null) continue;
        if (string.IsNullOrWhiteSpace(takeoff.Tag)) continue;

        string tag = takeoff.Tag.Trim();
        foreach (var eqp in eqpList)
        {
          if (!EqpTextMatcher.IsExactMatch(tag, eqp))
            continue;

          string key = takeoff.TakeoffId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          rows.Add(ToRow(takeoff));
        }
      }

      return rows;
    }

    public static object[] ToRow(TakeoffData takeoff)
    {
      // Instance Id: TakeoffId 해시 (Pipe와 동일 계열, 공란 금지)
      long instanceId = DotNetGuidHash.FromGuid(takeoff.TakeoffId);
      string name = takeoff.Tag ?? string.Empty;
      // DB Tag가 eqpid만인 경우 Display는 Damper[tag]인 경우가 많음 — 저장된 Tag 그대로 사용

      return new object[]
      {
        instanceId,
        string.Empty, // Bim Id — Takeoff에 없음
        name,         // Instance Name = S: Tag
        takeoff.TakeoffId.ToString(),
        string.Empty, // Connection Tag — Takeoff에 없음
        string.Empty, // Division
        string.Empty, // Subdivision1
        string.Empty  // Subdivision2
      };
    }
  }
}
