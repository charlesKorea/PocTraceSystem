using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// EQP 도달 시 S.Tag ↔ T.Tag 설비정보 상이 검수.
  /// Scrubber("a,b")는 <see cref="EquipmentTagMatcher"/>로 토큰 교집합을 허용한다.
  /// GCS Subdivision1(Start/Target)은 기존과 같이 제외.
  /// </summary>
  public sealed class EquipmentInfoMismatchRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "설비정보상이";
    public string Description => "EQP 도달 시 S.Tag↔T.Tag 불일치(Scrubber 콤마 태그는 교집합 허용)를 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;

      var r = context?.Result;
      if (r == null || !string.Equals(r.STATUS, "EQP"))
        return false;

      // Start/Target 모두 GCS면 설비정보상이 대상 제외
      if (_util.IsStringContains(r.TargetNodeSubDivision1, "gcs")
          || _util.IsStringContains(r.StartNodeSubDivision1, "gcs"))
        return false;

      // 시작/도착 모두 scrubber이거나 콤마 태그인 경우 토큰 교집합으로 판정
      if (EquipmentTagMatcher.AreRelated(r.StartEquipmentTag, r.TargetNodeTag))
        return false;

      issueTitle = " 기준정보 이슈 ";
      feedback = $" 설비 정보 상이 S.Tag({r.StartEquipmentTag})!=T.Tag({r.TargetNodeTag})";
      return true;
    }
  }
}
