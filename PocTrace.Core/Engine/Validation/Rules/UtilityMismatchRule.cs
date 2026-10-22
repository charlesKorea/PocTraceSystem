using System;
using System.Collections.Generic;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// UT 상이 (NOZ / TOF / EQP). UtMapping.csv 기반 그룹 매핑 + VOC 예외.
  /// 로직은 TraceValidator.UT상이체크에서 이관(동작 보존).
  /// </summary>
  public sealed class UtilityMismatchRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "UT상이";
    public string Description => "NOZ/TOF/EQP의 Connected UT 불일치(UtMapping 그룹·VOC 예외 포함)를 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;

      var r = context?.Result;
      if (r == null) return false;

      var utMap = context.UtilityGroupMap;
      string result = string.Empty;
      string fb = string.Empty;
      Evaluate(r, utMap, ref result, ref fb);

      if (string.IsNullOrEmpty(result))
        return false;

      issueTitle = result;
      feedback = fb;
      return true;
    }

    private void Evaluate(
      TraceResult r,
      Dictionary<string, Dictionary<string, string>> utMapList,
      ref string result,
      ref string feedback)
    {
      if (_util.예외대상이름여부(r.StartPocUtilityName) ||
          _util.예외대상이름여부(r.StartPocName) ||
          _util.예외대상이름여부(r.StartPocUtilityGroup))
        return;

      if (string.Equals(r.STATUS, "NOZ"))
      {
        EvaluateNoz(r, ref result, ref feedback);
        return;
      }

      if (string.Equals(r.STATUS, "TOF") || string.Equals(r.STATUS, "EQP"))
        EvaluateTofOrEqp(r, utMapList, ref result, ref feedback);
    }

    private void EvaluateNoz(TraceResult r, ref string result, ref string feedback)
    {
      if (r.StartPocUtilityName == "IWW1" && r.TargetPocConnectedUtility == "FWW") return;
      if (r.StartPocUtilityName == "IWW2" && r.TargetPocConnectedUtility == "NFW") return;
      if (r.StartNodeSubDivision2 == "CMP" &&
          _util.IsStringContains(r.StartPocUtilityGroup, "exhaust") &&
          _util.IsStringContains(r.TargetNodeSubDivision2, "폐수")) return;
      if (_util.IsStringContains(r.StartNodeSubDivision1, "GCS") &&
          r.StartPocUtilityName == "SN2" &&
          r.TargetPocConnectedUtility == "GN2") return;
      if (r.StartPocUtilityName == "GN2" && r.TargetPocConnectedUtility == "PN2") return;

      // CMP Main + Drain → Nozzle 분기 PASS
      if (_util.IsStringContains(r.StartNodeSubDivision1, "main") &&
          _util.IsStringContains(r.StartNodeSubDivision2, "cmp") &&
          _util.IsStringContains(r.StartPocUtilityGroup, "drain") &&
          _util.IsStringContains(r.TargetNodeSubDivision2, "nozzle"))
        return;

      if (_util.IsEmptyValue(r.StartPocUtilityName))
      {
        result = " UT 상이 (공백)";
        feedback += $" S.UT: {r.StartPocUtilityName} ";
      }
    }

    private void EvaluateTofOrEqp(
      TraceResult r,
      Dictionary<string, Dictionary<string, string>> utMapList,
      ref string result,
      ref string feedback)
    {
      if (string.Equals(r.STATUS, "TOF"))
      {
        // DIFF Pump + GN2 → Exhaust TOF PASS
        if (_util.IsStringContains(r.StartNodeSubDivision2, "pump") &&
            _util.IsStringContains(r.StartNodeName, "diff") &&
            _util.IsStringContains(r.StartPocUtilityName, "gn2") &&
            _util.IsStringContains(r.StartPocTag, "exhaust"))
          return;

        // VOC03-02: IMP + GN2 + T.UT not Exhaust
        if (_util.IsStringContains(r.StartNodeSubDivision2, "imp") &&
            _util.IsStringContains(r.StartPocUtilityName, "gn2") &&
            !_util.IsStringContains(r.TargetPocUtilityName, "exhaust"))
        {
          result = " UT 상이";
          feedback += $" {r.STATUS} IMP-GN2-T.UT {r.TargetPocUtilityName} ";
        }
      }

      // VOC4-1 FORE-LINE / By Pass (EQP)
      if (string.Equals(r.STATUS, "EQP"))
      {
        if (_util.IsStringContains(r.StartPocUtilityName, "fore") &&
            _util.IsStringContainsOneOfList(r.StartPocName, new List<string> { "PS", "By Pass" }) &&
            _util.IsStringContainsOneOfList(r.TargetPocUtilityName, new List<string> { "fore", "vac", "pfc" }))
        {
          if (!_util.IsStringContainsOneOfList(r.StartPocConnectedUtility, new List<string> { "vac", "exhaust", "pfc", "fore-line" }) ||
              !_util.IsStringContainsOneOfList(r.TargetPocConnectedUtility, new List<string> { "vac", "exhaust", "pfc", "fore-line" }))
          {
            result = " UT 상이";
            feedback += $" {r.STATUS} S.Connected UT {r.StartPocConnectedUtility} / T.Connected UT {r.TargetPocConnectedUtility} ";
          }
          else
            return;
        }

        if (_util.IsStringContains(r.TargetPocUtilityName, "fore") &&
            _util.IsStringContainsOneOfList(r.TargetPocName, new List<string> { "PS", "By Pass" }) &&
            _util.IsStringContainsOneOfList(r.StartPocConnectedUtility, new List<string> { "fore", "vac", "pfc" }))
        {
          if (!_util.IsStringContainsOneOfList(r.StartPocConnectedUtility, new List<string> { "vac", "exhaust", "pfc", "fore-line" }) ||
              !_util.IsStringContainsOneOfList(r.TargetPocConnectedUtility, new List<string> { "vac", "exhaust", "pfc", "fore-line" }))
          {
            result = " UT 상이";
            feedback += $" {r.STATUS} S.Connected UT {r.StartPocConnectedUtility} / T.Connected UT {r.TargetPocConnectedUtility} ";
          }
          else
            return;
        }
      }

      if (_util.IsEmptyValue(r.StartPocUtilityName) || _util.IsEmptyValue(r.TargetPocUtilityName))
      {
        result = " UT 상이 (공백)";
        if (_util.IsEmptyValue(r.StartPocUtilityName)) feedback += $" S.UT: {r.StartPocUtilityName} ";
        if (_util.IsEmptyValue(r.TargetPocUtilityName)) feedback += $" T.UT: {r.TargetPocUtilityName} ";
      }
      else if (IsUtilityMismatched(r, utMapList))
      {
        result = " UT 상이 ";
        feedback += $" S:ConnectedUT({r.StartPocConnectedUtility}) != T:ConnectedUT({r.TargetPocConnectedUtility})";
      }
    }

    /// <summary>Connected UT 상호 불일치 + UtMapping 그룹 동의어. (기존 TraceValidator 로직 보존)</summary>
    private bool IsUtilityMismatched(TraceResult r, Dictionary<string, Dictionary<string, string>> dbGroupMap)
    {
      string sConUT = r.StartPocConnectedUtility;
      string tConUT = r.TargetPocConnectedUtility;

      if (string.IsNullOrEmpty(sConUT) || string.IsNullOrEmpty(tConUT)) return false;

      if (string.Equals(sConUT, tConUT, StringComparison.OrdinalIgnoreCase)) return false;

      if (_util.IsStringContainsOneOfList(sConUT, new List<string> { "VENT", "FORE", "VAC", "DIVERT VAC" }) &&
          _util.IsStringContainsOneOfList(tConUT, new List<string> { "VENT", "FORE", "VAC", "DIVERT VAC" }))
        return false;

      string currentGroup = r.StartPocUtilityGroup;
      if (string.IsNullOrEmpty(currentGroup))
      {
        // 기존 동작: IssueList에 직접 추가 후 mismatch=false
        r.IssueList.Add($"Utility Mismatch ({sConUT} vs {tConUT}) - No Group Context");
        return false;
      }

      if (dbGroupMap != null && dbGroupMap.TryGetValue(currentGroup, out var groupMap))
      {
        if (groupMap.TryGetValue(r.StartPocUtilityName, out string mapped1) &&
            string.Equals(mapped1, tConUT, StringComparison.OrdinalIgnoreCase)) return false;

        if (groupMap.TryGetValue(r.StartPocUtilityName, out string mapped2) &&
            string.Equals(mapped2, sConUT, StringComparison.OrdinalIgnoreCase)) return false;
      }

      return true;
    }
  }
}
