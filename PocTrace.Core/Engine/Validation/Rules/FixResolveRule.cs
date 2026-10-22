using System.Collections.Generic;
using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// Fix Resolve 필요.
  /// - EQP인데 T.Division이 Equipment가 아님
  /// - 단일 배관에서 S.GUID == T.GUID
  /// </summary>
  public sealed class FixResolveRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "FixResolve필요";
    public string Description => "EQP인데 Division≠Equipment이거나 단일 배관 S.GUID==T.GUID인 Fix Resolve 대상을 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;

      var r = context?.Result;
      if (r == null) return false;

      var bd = context.SamePocBranchResults ?? new List<Models.TraceResult>();

      if (string.Equals(r.STATUS, "EQP"))
      {
        if (!string.IsNullOrEmpty(r.TargetNodeDivision) &&
            !_util.IsStringContains(r.TargetNodeDivision, "Equipment"))
        {
          issueTitle = " Fix Resolve 필요 ";
          feedback = " EQP != Division ";
          return true;
        }
      }

      if (bd.Count == 1 &&
          string.Equals(r.STATUS, "EQP") &&
          r.StartNodeGUID == r.TargetNodeGUID)
      {
        issueTitle = " Fix Resolve 필요 ";
        feedback = " S.GUID == T.GUID ";
        return true;
      }

      return false;
    }
  }
}
