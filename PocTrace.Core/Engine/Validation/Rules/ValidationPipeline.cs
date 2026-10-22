using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// 검수 규칙 파이프라인.
  /// VOC 추가: VocRule.Sample.cs 복사 → IValidationRule 구현 → 아래 목록에 등록.
  /// </summary>
  public sealed class ValidationPipeline
  {
    private readonly List<IValidationRule> _rules;

    public ValidationPipeline(IEnumerable<IValidationRule> rules)
    {
      _rules = rules?.ToList() ?? new List<IValidationRule>();
    }

    public static ValidationPipeline CreateDefault()
    {
      return new ValidationPipeline(new IValidationRule[]
      {
        new ErrStatusRule(),
        new ModelingIncompleteRule(),
        new EquipmentInfoMismatchRule(),
        new PocUtMismatchRule(),
        new ModelingAbsentRule(),
        new ConnectionMissingRule(),
        new ConnectionInfoMismatchRule(),
        new FixResolveRule(),
        new UtilityMismatchRule(),
      });
    }

    public IReadOnlyList<IValidationRule> Rules => _rules;

    public void Execute(ValidationContext context, System.Action<string, string> onIssue)
    {
      if (context?.Result == null || onIssue == null) return;

      foreach (var rule in _rules)
      {
        if (rule.TryApply(context, out var title, out var feedback))
        {
          ValidationRuleRegistry.RecordHit(rule.RuleId);
          onIssue(title, feedback);
        }
      }
    }
  }
}
