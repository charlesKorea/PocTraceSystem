#if false
// =============================================================================
// VOC 규칙 추가 템플릿 (복사해서 사용)
// =============================================================================
// 1) 이 파일을 Voc{이슈번호}Rule.cs 로 복사 (예: Voc05ScrubberRule.cs)
// 2) #if false 제거
// 3) RuleId / TryApply 구현
// 4) ValidationPipeline.CreateDefault() 목록에 new VocXxxRule() 추가
// 5) PocTrace.Core.csproj <Compile Include="..."/> 추가 (구식 csproj)
// =============================================================================

using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// VOC-XX: (한 줄 설명 — SCS 요청 요약)
  /// </summary>
  public sealed class VocXxSampleRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "VOC-XX";
    public string Description => "VOC-XX: (한 줄 설명 — SCS 요청 요약)";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;

      var r = context?.Result;
      if (r == null) return false;

      // 예: 특정 STATUS만
      // if (!string.Equals(r.STATUS, "EQP")) return false;

      // 예: Tag 비교는 반드시 EquipmentTagMatcher 사용
      // if (EquipmentTagMatcher.AreRelated(r.StartEquipmentTag, r.TargetNodeTag)) return false;

      // 이슈 기록
      // issueTitle = " 연결정보 상이 ";
      // feedback = $" [VOC-XX] ... ";
      // return true;

      return false;
    }
  }
}
#endif
