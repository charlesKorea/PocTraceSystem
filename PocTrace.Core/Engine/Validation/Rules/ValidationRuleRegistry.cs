using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>리포트/Rules 페이지용 규칙 메타 + 배치 실행(이슈 발생) 횟수.</summary>
  public sealed class ValidationRuleInfo
  {
    public string RuleId { get; set; }
    public string Description { get; set; }
    public long HitCount { get; set; }
    public int Order { get; set; }
  }

  /// <summary>
  /// 등록된 Check Rules 카탈로그 및 배치 중 이슈 발생 횟수 집계.
  /// - IValidationRule: VOC 추가 시 CreateDefault()에 등록
  /// - HookupValidation: 항상 단일 고정 Rule (추가/삭제 없음)
  /// </summary>
  public static class ValidationRuleRegistry
  {
    public const string HookupRuleId = "HookupValidation";

    public const string HookupRuleDescription =
      "H/U Number 검증(단일 고정 Rule). Exhaust/Vac 대상 POC의 Connector Tag·Hookup 번호 일치/누락/불일치를 검사합니다. Rule이 추가될 일은 없습니다.";

    private static readonly ConcurrentDictionary<string, long> Hits =
      new ConcurrentDictionary<string, long>();

    public static void ResetHits()
    {
      Hits.Clear();
    }

    public static void RecordHit(string ruleId)
    {
      if (string.IsNullOrWhiteSpace(ruleId)) return;
      Hits.AddOrUpdate(ruleId.Trim(), 1, (_, n) => n + 1);
    }

    /// <summary>비정상 Hookup 결과 건수만큼 Hit 적립 (OK 제외).</summary>
    public static void RecordHookupHits(int nonOkCount)
    {
      if (nonOkCount <= 0) return;
      Hits.AddOrUpdate(HookupRuleId, nonOkCount, (_, n) => n + nonOkCount);
    }

    public static ValidationPipeline CreateDefaultPipeline() =>
      ValidationPipeline.CreateDefault();

    /// <summary>파이프라인 Rule 수 + Hookup 고정 1.</summary>
    public static int RegisteredCount => CreateDefaultPipeline().Rules.Count + 1;

    public static List<ValidationRuleInfo> GetCatalog(bool includeHits = true)
    {
      var pipeline = CreateDefaultPipeline();
      int order = 0;
      var list = pipeline.Rules.Select(r =>
      {
        long hits = 0;
        if (includeHits)
          Hits.TryGetValue(r.RuleId, out hits);
        return new ValidationRuleInfo
        {
          RuleId = r.RuleId,
          Description = r.Description,
          HitCount = hits,
          Order = ++order
        };
      }).ToList();

      long hookupHits = 0;
      if (includeHits)
        Hits.TryGetValue(HookupRuleId, out hookupHits);

      list.Add(new ValidationRuleInfo
      {
        RuleId = HookupRuleId,
        Description = HookupRuleDescription,
        HitCount = hookupHits,
        Order = ++order
      });

      return list;
    }

    public static Dictionary<string, long> SnapshotHits()
    {
      return Hits.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static long TotalHits()
    {
      long sum = 0;
      foreach (var kv in Hits) sum += kv.Value;
      return sum;
    }
  }
}
