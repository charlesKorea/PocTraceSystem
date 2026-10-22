using System.Collections.Generic;
using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>STATUS=ERR (철거/Delete 등).</summary>
  public sealed class ErrStatusRule : IValidationRule
  {
    public string RuleId => "철거설비연결";
    public string Description => "STATUS=ERR(철거/Delete 등)인 연결을 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;
      var r = context?.Result;
      if (r == null || !string.Equals(r.STATUS, "ERR"))
        return false;

      issueTitle = " 철거설비연결 ";
      feedback = " 건설코드 Delete 포함 ";
      return true;
    }
  }

  /// <summary>ING + 예외 UT/이름이 아니면 모델링 미완료.</summary>
  public sealed class ModelingIncompleteRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "모델링미완료";
    public string Description => "STATUS=ING이며 예외 UT/이름이 아니면 모델링 미완료로 판정합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;
      var r = context?.Result;
      if (r == null || !string.Equals(r.STATUS, "ING"))
        return false;

      if (_util.예외대상이름여부(r.StartPocUtilityGroup) || _util.예외대상이름여부(r.StartPocName))
        return false;

      issueTitle = " 모델링 미완료 ";
      feedback = $"ING, T.Name: {r.EndName}";
      return true;
    }
  }

  /// <summary>EMP인데 UT에 미사용 미포함 → 모델링 없음.</summary>
  public sealed class ModelingAbsentRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "모델링없음";
    public string Description => "STATUS=EMP이고 UT에 '미사용'이 없으면 모델링 없음으로 판정합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;
      var r = context?.Result;
      if (r == null || !string.Equals(r.STATUS, "EMP"))
        return false;

      if (_util.IsStringContains(r.StartPocUtilityName, "미사용"))
        return false;

      issueTitle = " 모델링 없음 ";
      feedback = $"{r.STATUS}, ConnectorName:{r.StartPocName}";
      return true;
    }
  }

  /// <summary>NOZ POC UT 층별 GO2/PO2 ↔ O2 상이.</summary>
  public sealed class PocUtMismatchRule : IValidationRule
  {
    public string RuleId => "POCUT상이";
    public string Description => "NOZ POC의 층별 GO2/PO2 ↔ O2 UT 불일치를 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;
      var r = context?.Result;
      if (r == null || !string.Equals(r.STATUS, "NOZ"))
        return false;

      if (r.StartEquipmentPZ < 5400 && r.StartPocUtilityName == "GO2" && r.TargetPocConnectedUtility == "O2")
      {
        issueTitle = " 기준정보 이슈 ";
        feedback = $"[{r.STATUS}] POC UT 상이 1층 설비 S: Ut = GO2, T: Lateral Ut = O2 ";
        return true;
      }

      if (r.StartEquipmentPZ >= 5400 && r.StartPocUtilityName == "PO2" && r.TargetPocConnectedUtility == "O2")
      {
        issueTitle = " 기준정보 이슈 ";
        feedback = $"[{r.STATUS}] POC UT 상이 2,3층 설비 S: Ut = PO2, T: Lateral Ut = O2 ";
        return true;
      }

      return false;
    }
  }

  /// <summary>TOF/NOZ/EQP(GCS STICK) 연결정보 누락.</summary>
  public sealed class ConnectionMissingRule : IValidationRule
  {
    private readonly Util _util = new Util();

    public string RuleId => "연결정보누락";
    public string Description => "TOF/NOZ/EQP(GCS STICK)에서 Tag·ConnectionTag 누락을 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;
      var r = context?.Result;
      if (r == null) return false;

      if (string.Equals(r.STATUS, "TOF"))
      {
        if (_util.IsEmptyValue(r.TargetNodeTag))
        {
          issueTitle = " 연결정보 누락 ";
          feedback = $"[{r.STATUS}] T.Tag EMPTY ";
          return true;
        }
        return false;
      }

      if (string.Equals(r.STATUS, "NOZ"))
      {
        if (_util.IsEmptyValue(r.TargetPocConnectionTag))
        {
          issueTitle = " 연결정보 누락 ";
          feedback = $"[{r.STATUS}] T.ConnectionTag EMPTY ";
          return true;
        }
        return false;
      }

      if (string.Equals(r.STATUS, "EQP"))
      {
        // VOC: GCS + ConnectorInfo In → S/T Connection Tag·Connector Tag 무시 (연결정보누락 제외)
        if (GcsInConnectionTagExemption.Applies(r))
          return false;

        if (_util.IsStringContains(r.StartNodeSubDivision1, "gcs") && r.StartPocInformation == "STICK" && _util.IsEmptyValue(r.StartPocTag))
        {
          issueTitle = " 연결정보 누락 ";
          feedback = $"[{r.STATUS}] GCS S.Connector Tag EMPTY Module Name 미기입";
          return true;
        }
        if (_util.IsStringContains(r.TargetNodeSubDivision1, "gcs") && r.TargetConnectorInformation == "STICK" && _util.IsEmptyValue(r.TargetPocTag))
        {
          issueTitle = " 연결정보 누락 ";
          feedback = $"[{r.STATUS}] GCS T.Connector Tag EMPTY Module Name 미기입 ";
          return true;
        }
      }

      return false;
    }
  }
}
