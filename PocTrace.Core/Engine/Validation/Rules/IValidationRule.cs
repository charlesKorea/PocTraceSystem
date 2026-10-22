namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// 단일 검수 규칙. 새 VOC/예외는 이 인터페이스 구현체로 추가한다.
  /// TraceValidator 오케스트레이터가 순서대로 실행한다.
  /// </summary>
  public interface IValidationRule
  {
    /// <summary>리포트/로그용 규칙 식별자 (예: "설비정보상이").</summary>
    string RuleId { get; }

    /// <summary>Rules 페이지·리포트용 한 줄 설명.</summary>
    string Description { get; }

    /// <summary>
    /// true면 이슈로 기록.
    /// </summary>
    bool TryApply(ValidationContext context, out string issueTitle, out string feedback);
  }
}
