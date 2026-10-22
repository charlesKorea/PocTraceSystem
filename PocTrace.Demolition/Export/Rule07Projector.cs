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
  /// [보류] Rule 7: BIM Group Name ↔ EQPID.
  /// ConfigurationLog OUTER APPLY가 캐시 생성 중 타임아웃으로 abort되어
  /// SQL·시트 출력을 일시 중지 (VOC 2026-08). 재개 시 GetSelectNodes OUTER APPLY + 이 Collect 복구.
  /// </summary>
  public static class Rule07Projector
  {
    public const string SheetName = "Rule07";
    public const string RuleId = "Rule07";
    public const string RuleTitle = "BIM Group Name ↔ EQPID 매칭 (보류)";

    public static readonly string[] Headers =
    {
      "Instance Id",
      "Bim Id",
      "Instance Name",
      "GUID",
      "Connection Tag",
      "Division",
      "Subdivision1",
      "Subdivision2",
      "BIM Group Name"
    };

    public static List<object[]> Collect(IGraphCache cache, IEnumerable<string> eqpIds)
    {
      // 보류: 빈 결과
      return new List<object[]>();
#if false
      var rows = new List<object[]>();
      if (cache == null || eqpIds == null) return rows;
      // ... prior BIM Group scan omitted ...
      return rows;
#endif
    }
  }
}
