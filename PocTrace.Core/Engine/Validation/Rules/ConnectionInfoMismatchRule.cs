using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// 연결정보 상이 (TOF/NOZ/EQP + MainLine VOC-1 + Scrubber + GCS STICK).
  /// 신규 VOC는 이 클래스에 분기 추가하거나 별도 IValidationRule로 분리.
  /// </summary>
  public sealed class ConnectionInfoMismatchRule : IValidationRule
  {
    private readonly Util _util = new Util();

    /// <summary>T:Module Name "ch (a)" → 기대 Connector Tag "ch a"</summary>
    private static readonly Regex ChParenPattern = new Regex(
      @"ch\s*\(\s*([^)]+?)\s*\)",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string RuleId => "연결정보상이";
    public string Description => "TOF/NOZ/EQP 연결정보 불일치(MainLine VOC·Scrubber·GCS STICK S/T Module/Tag VOC 포함)를 검출합니다.";

    public bool TryApply(ValidationContext context, out string issueTitle, out string feedback)
    {
      issueTitle = null;
      feedback = null;

      var r = context?.Result;
      var startPoc = context?.StartPoc;
      var bd = context?.SamePocBranchResults ?? new List<TraceResult>();
      if (r == null) return false;

      if (MainLineTraceHelper.IsMainLineTofChaseResult(r, startPoc))
      {
        if (!_util.IsEmptyValue(r.StartEquipmentTag) && !_util.IsEmptyValue(r.TargetNodeTag) &&
            EquipmentTagMatcher.AreMismatched(r.StartEquipmentTag, r.TargetNodeTag))
        {
          issueTitle = " 연결정보 상이 ";
          feedback = $" [EQP/MainLine] T.Tag:{r.TargetNodeTag} != S.Tag:{r.StartEquipmentTag} (S.UT:{startPoc?.UtilityName}) ";
          return true;
        }
      }

      if (string.Equals(r.STATUS, "TOF"))
      {
        if (!_util.IsEmptyValue(r.StartEquipmentTag) && !_util.IsEmptyValue(r.TargetNodeTag) &&
            EquipmentTagMatcher.AreMismatched(r.StartEquipmentTag, r.TargetNodeTag))
        {
          issueTitle = " 연결정보 상이 ";
          feedback = _util.IsStringContains(startPoc?.UtilityGroupName, "WATER")
              ? $" [{r.STATUS}] {startPoc.UtilityGroupName} Takeoff"
              : $" [{r.STATUS}] T.Tag:{r.TargetNodeTag} != S.Tag:{r.StartEquipmentTag} ";
          return true;
        }
        return false;
      }

      if (string.Equals(r.STATUS, "NOZ"))
      {
        if (!_util.IsEmptyValue(r.StartEquipmentTag) && !_util.IsEmptyValue(r.TargetPocConnectionTag) &&
            EquipmentTagMatcher.AreMismatched(r.StartEquipmentTag, r.TargetPocConnectionTag))
        {
          issueTitle = " 연결정보 상이 ";
          feedback = $" [{r.STATUS}] T.ConnectionTag:{r.TargetPocConnectionTag} != S.Tag:{r.StartEquipmentTag} ";
          return true;
        }
        return false;
      }

      if (!string.Equals(r.STATUS, "EQP"))
        return false;

      // VOC: GCS + ConnectorInfo In → S/T Connection Tag·Connector Tag 무시 (연결정보상이 제외)
      if (GcsInConnectionTagExemption.Applies(r))
        return false;

      // IN/OUT POC는 연결정보 상이 제외
      if (r.StartPocInformation == "IN" || r.TargetConnectorInformation == "IN" ||
          r.StartPocInformation == "OUT" || r.TargetConnectorInformation == "IN")
        return false;

      if (EquipmentTagMatcher.IsScrubberSubdivision(r.StartNodeSubDivision2)
          || EquipmentTagMatcher.IsScrubberSubdivision(r.TargetNodeSubDivision2)
          || (r.StartNodeTag != null && r.StartNodeTag.IndexOf(',') >= 0)
          || (r.TargetNodeTag != null && r.TargetNodeTag.IndexOf(',') >= 0))
      {
        if (EquipmentTagMatcher.AreMismatched(r.StartEquipmentTag, r.TargetNodeTag))
        {
          issueTitle = " 연결정보 상이 ";
          feedback = $"[{r.STATUS}] Scrubber S.Tag:{r.StartNodeTag}!=T.Tag:{r.TargetNodeTag} ";
          return true;
        }
        return false;
      }

      // 공통: S:Subdivision1 에 6-gcs + S:Connector Information = stick
      if (IsGcsStickStart(r))
      {
        // 기존 VOC #02-1
        if (_util.IsStringContains(r.StartPocConnectionTag, r.TargetNodeTag) && r.StartPocConnectionTag != r.TargetNodeTag)
        {
          issueTitle = " 연결정보 상이 ";
          feedback = $"[{r.STATUS}] Start GCS S.Connection Tag:{r.StartPocConnectionTag}!=T.Tag:{r.TargetNodeTag} ";
          return true;
        }

        // VOC: GCS Stick Module/Tag 연결정보상이 (조건 1~10 OR)
        string vocFeedback;
        if (TryGcsStickModuleTagMismatch(r, bd, out vocFeedback))
        {
          issueTitle = " 연결정보 상이 ";
          feedback = vocFeedback;
          return true;
        }

        return false;
      }

      // 공통: T:Subdivision1 에 6-gcs + T:Connector Information = stick (S측 GCS STICK의 대칭)
      if (IsGcsStickTarget(r))
      {
        // 기존 VOC #02-2
        if (_util.IsStringContains(r.TargetPocConnectionTag, r.StartNodeTag) && r.TargetPocConnectionTag != r.StartNodeTag)
        {
          issueTitle = " 연결정보 상이 ";
          feedback = $"[{r.STATUS}] Target GCS T.Connection Tag:{r.TargetPocConnectionTag}!=S.Tag:{r.StartNodeTag} ";
          return true;
        }

        string vocFeedbackT;
        if (TryGcsStickTargetModuleTagMismatch(r, bd, out vocFeedbackT))
        {
          issueTitle = " 연결정보 상이 ";
          feedback = vocFeedbackT;
          return true;
        }

        return false;
      }

      if (!IsMisMatchedTag(r))
        return false;

      if (bd.Count == 1)
      {
        issueTitle = " 연결정보 상이 ";
        feedback = string.Empty;
        return true;
      }

      var hasNozOrTof = bd.Any(a => a.STATUS.Equals("NOZ") || a.STATUS.Equals("TOF"));
      if (!hasNozOrTof)
        return false;

      var nozOk = bd.Find(a => a.STATUS.Equals("NOZ") && EquipmentTagMatcher.AreRelated(a.TargetPocConnectionTag, r.StartEquipmentTag));
      var tofOk = bd.Find(a => a.STATUS.Equals("TOF") && EquipmentTagMatcher.AreRelated(a.TargetNodeTag, r.StartEquipmentTag));
      if (nozOk == null && tofOk == null)
      {
        issueTitle = " 연결정보 상이 ";
        feedback = $"[{r.STATUS}] [] Scrubber S.Tag:{r.StartNodeTag}!=T.Tag:{r.TargetNodeTag} ";
        return true;
      }

      return false;
    }

    private bool IsGcsStickStart(TraceResult r)
    {
      return _util.IsStringContains(r.StartNodeSubDivision1, "6-gcs")
          && _util.IsStringContains(r.StartPocInformation, "stick");
    }

    private bool IsGcsStickTarget(TraceResult r)
    {
      return _util.IsStringContains(r.TargetNodeSubDivision1, "6-gcs")
          && _util.IsStringContains(r.TargetConnectorInformation, "stick");
    }

    /// <summary>
    /// GCS Stick VOC: 공통(6-gcs+stick) + 개별조건 1~10 중 하나라도 만족 시 연결정보상이.
    /// S:Connector Tag = StartPocTag, T:Module Name = TargetPocModuleName.
    /// 분기배관수 = SamePocBranchResults.Count (= BranchedPipes).
    /// </summary>
    private bool TryGcsStickModuleTagMismatch(
      TraceResult r,
      List<TraceResult> bd,
      out string feedback)
    {
      feedback = null;
      if (r == null) return false;

      int branchCount = bd != null && bd.Count > 0 ? bd.Count : Math.Max(1, r.BranchedPipes);
      int distinctTargetGuids = CountDistinctTargetGuids(bd, r);
      string connectorTag = (r.StartPocTag ?? string.Empty).Trim();
      string moduleName = (r.TargetPocModuleName ?? string.Empty).Trim();
      string tSub1 = r.TargetNodeSubDivision1 ?? string.Empty;
      string tSub2 = r.TargetNodeSubDivision2 ?? string.Empty;

      // --- 조건 1~9: 분기=1 + T:GUID 유일 ---
      if (branchCount == 1 && distinctTargetGuids == 1)
      {
        // 1) 1-main + Module(비공백) <> ConnectorTag  — 공백 Module은 #5
        if (_util.IsStringContains(tSub1, "1-main")
            && !_util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(moduleName, connectorTag))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#1] 1-main Module:{moduleName}<>S.ConnectorTag:{connectorTag}";
          return true;
        }

        // 2) 2-sub + Module(비공백) <> ConnectorTag  — 공백 Module은 #6
        if (_util.IsStringContains(tSub1, "2-sub")
            && !_util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(moduleName, connectorTag))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#2] 2-sub Module:{moduleName}<>S.ConnectorTag:{connectorTag}";
          return true;
        }

        // 3) 7- + Module 공백 + ConnectorTag <> T:Sub2
        if (_util.IsStringContains(tSub1, "7-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, tSub2))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#3] 7- Module빈값 S.ConnectorTag:{connectorTag}<>T.Sub2:{tSub2}";
          return true;
        }

        // 4) 4- + Module 공백 + ConnectorTag <> T:Sub2
        if (_util.IsStringContains(tSub1, "4-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, tSub2))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#4] 4- Module빈값 S.ConnectorTag:{connectorTag}<>T.Sub2:{tSub2}";
          return true;
        }

        // 5) 1- + Module 공백 + ConnectorTag <> main tool
        if (_util.IsStringContains(tSub1, "1-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, "main tool"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#5] 1- Module빈값 S.ConnectorTag:{connectorTag}<>main tool";
          return true;
        }

        // 6) 2- + Module 공백 + ConnectorTag <> sub tool
        if (_util.IsStringContains(tSub1, "2-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, "sub tool"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#6] 2- Module빈값 S.ConnectorTag:{connectorTag}<>sub tool";
          return true;
        }

        // 7) 1- + Module ch(n) + ConnectorTag <> 괄호제외값(ch a)
        //    (기대값과 불일치 시 연결정보상이 — 8·9번 <> 패턴과 동일)
        string expectedCh;
        if (_util.IsStringContains(tSub1, "1-")
            && TryExtractChParenExpected(moduleName, out expectedCh)
            && !IsEqualIgnoreCase(connectorTag, expectedCh))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#7] 1- Module:{moduleName} 기대:{expectedCh}<>S.ConnectorTag:{connectorTag}";
          return true;
        }

        // 8) 1- + Module pht1/cor1 + ConnectorTag <> pm1
        if (_util.IsStringContains(tSub1, "1-")
            && ContainsPhtOrCor(moduleName, 1)
            && !IsEqualIgnoreCase(connectorTag, "pm1"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#8] 1- Module:{moduleName} S.ConnectorTag:{connectorTag}<>pm1";
          return true;
        }

        // 9) 1- + Module pht2/cor2 + ConnectorTag <> pm2
        if (_util.IsStringContains(tSub1, "1-")
            && ContainsPhtOrCor(moduleName, 2)
            && !IsEqualIgnoreCase(connectorTag, "pm2"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK#9] 1- Module:{moduleName} S.ConnectorTag:{connectorTag}<>pm2";
          return true;
        }
      }

      // --- 조건 10: 분기>1 + T:GUID 중복(>1) + Module이 ConnectorTag에 미포함 ---
      if (branchCount > 1
          && distinctTargetGuids > 1
          && !_util.IsEmptyValue(moduleName)
          && !_util.IsStringContains(connectorTag, moduleName))
      {
        feedback = $"[{r.STATUS}/GCS-STICK#10] 분기:{branchCount} T.GUID중복:{distinctTargetGuids} Module:{moduleName} not in S.ConnectorTag:{connectorTag}";
        return true;
      }

      return false;
    }

    /// <summary>
    /// T측 GCS Stick VOC: 공통(T:6-gcs + T:stick) + 개별조건 1~10 OR.
    /// S측 TryGcsStickModuleTagMismatch의 S/T 대칭.
    /// T:Connector Tag = TargetPocTag, S:Module Name = StartPocModuleName.
    /// 1~9: 분기=1, T:Connector Name 유일, T:GUID 유일, S:GUID 유일.
    /// </summary>
    private bool TryGcsStickTargetModuleTagMismatch(
      TraceResult r,
      List<TraceResult> bd,
      out string feedback)
    {
      feedback = null;
      if (r == null) return false;

      int branchCount = bd != null && bd.Count > 0 ? bd.Count : Math.Max(1, r.BranchedPipes);
      int distinctTargetGuids = CountDistinctTargetGuids(bd, r);
      int distinctStartGuids = CountDistinctStartGuids(bd, r);
      int distinctTConnectorNames = CountDistinctTargetConnectorNames(bd, r);
      string connectorTag = (r.TargetPocTag ?? string.Empty).Trim();
      string moduleName = (r.StartPocModuleName ?? string.Empty).Trim();
      string sSub1 = r.StartNodeSubDivision1 ?? string.Empty;
      string sSub2 = r.StartNodeSubDivision2 ?? string.Empty;

      bool uniqueSingle = branchCount == 1
          && distinctTConnectorNames == 1
          && distinctTargetGuids == 1
          && distinctStartGuids == 1;

      if (uniqueSingle)
      {
        // 1) S:1-main + Module(비공백) <> T:ConnectorTag
        if (_util.IsStringContains(sSub1, "1-main")
            && !_util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(moduleName, connectorTag))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#1] 1-main S.Module:{moduleName}<>T.ConnectorTag:{connectorTag}";
          return true;
        }

        // 2) S:2-sub + Module(비공백) <> T:ConnectorTag
        if (_util.IsStringContains(sSub1, "2-sub")
            && !_util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(moduleName, connectorTag))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#2] 2-sub S.Module:{moduleName}<>T.ConnectorTag:{connectorTag}";
          return true;
        }

        // 3) S:7- + Module 공백 + T:ConnectorTag <> S:Sub2
        if (_util.IsStringContains(sSub1, "7-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, sSub2))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#3] 7- S.Module빈값 T.ConnectorTag:{connectorTag}<>S.Sub2:{sSub2}";
          return true;
        }

        // 4) S:4- + Module 공백 + T:ConnectorTag <> S:Sub2
        if (_util.IsStringContains(sSub1, "4-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, sSub2))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#4] 4- S.Module빈값 T.ConnectorTag:{connectorTag}<>S.Sub2:{sSub2}";
          return true;
        }

        // 5) S:1- + Module 공백 또는 MAIN + T:ConnectorTag <> main tool
        if (_util.IsStringContains(sSub1, "1-")
            && IsEmptyOrMainModule(moduleName)
            && !IsEqualIgnoreCase(connectorTag, "main tool"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#5] 1- S.Module:{moduleName} T.ConnectorTag:{connectorTag}<>main tool";
          return true;
        }

        // 6) S:2- + Module 공백 + T:ConnectorTag <> sub tool
        if (_util.IsStringContains(sSub1, "2-")
            && _util.IsEmptyValue(moduleName)
            && !IsEqualIgnoreCase(connectorTag, "sub tool"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#6] 2- S.Module빈값 T.ConnectorTag:{connectorTag}<>sub tool";
          return true;
        }

        // 7) S:1- + Module ch(n) + T:ConnectorTag <> 괄호제외값(ch a)
        string expectedCh;
        if (_util.IsStringContains(sSub1, "1-")
            && TryExtractChParenExpected(moduleName, out expectedCh)
            && !IsEqualIgnoreCase(connectorTag, expectedCh))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#7] 1- S.Module:{moduleName} 기대:{expectedCh}<>T.ConnectorTag:{connectorTag}";
          return true;
        }

        // 8) S:1- + Module pht1/cor1 + T:ConnectorTag <> pm1
        if (_util.IsStringContains(sSub1, "1-")
            && ContainsPhtOrCor(moduleName, 1)
            && !IsEqualIgnoreCase(connectorTag, "pm1"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#8] 1- S.Module:{moduleName} T.ConnectorTag:{connectorTag}<>pm1";
          return true;
        }

        // 9) S:1- + Module pht2/cor2 + T:ConnectorTag <> pm2
        if (_util.IsStringContains(sSub1, "1-")
            && ContainsPhtOrCor(moduleName, 2)
            && !IsEqualIgnoreCase(connectorTag, "pm2"))
        {
          feedback = $"[{r.STATUS}/GCS-STICK-T#9] 1- S.Module:{moduleName} T.ConnectorTag:{connectorTag}<>pm2";
          return true;
        }
      }

      // 10) 분기>1 + T:Connector Name / S:GUID / T:GUID 각각 2+ + S:Module이 T:ConnectorTag에 없거나 불일치
      if (branchCount > 1
          && distinctTConnectorNames >= 2
          && distinctStartGuids >= 2
          && distinctTargetGuids >= 2
          && !_util.IsEmptyValue(moduleName)
          && (!_util.IsStringContains(connectorTag, moduleName)
              || !IsEqualIgnoreCase(moduleName, connectorTag)))
      {
        feedback = $"[{r.STATUS}/GCS-STICK-T#10] 분기:{branchCount} T.Name중복:{distinctTConnectorNames} S.GUID:{distinctStartGuids} T.GUID:{distinctTargetGuids} S.Module:{moduleName} vs T.ConnectorTag:{connectorTag}";
        return true;
      }

      return false;
    }

    private static int CountDistinctTargetGuids(List<TraceResult> bd, TraceResult current)
    {
      if (bd == null || bd.Count == 0)
        return current != null && current.TargetNodeGUID != Guid.Empty ? 1 : 0;

      return bd
        .Where(x => x != null && x.TargetNodeGUID != Guid.Empty)
        .Select(x => x.TargetNodeGUID)
        .Distinct()
        .Count();
    }

    private static int CountDistinctStartGuids(List<TraceResult> bd, TraceResult current)
    {
      if (bd == null || bd.Count == 0)
        return current != null && current.StartNodeGUID != Guid.Empty ? 1 : 0;

      return bd
        .Where(x => x != null && x.StartNodeGUID != Guid.Empty)
        .Select(x => x.StartNodeGUID)
        .Distinct()
        .Count();
    }

    private static int CountDistinctTargetConnectorNames(List<TraceResult> bd, TraceResult current)
    {
      IEnumerable<TraceResult> rows = (bd != null && bd.Count > 0)
        ? bd.Where(x => x != null)
        : (current != null ? new[] { current } : Array.Empty<TraceResult>());

      return rows
        .Select(x => (x.TargetPocName ?? string.Empty).Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    }

    private bool IsEmptyOrMainModule(string moduleName)
    {
      return _util.IsEmptyValue(moduleName) || IsEqualIgnoreCase(moduleName, "MAIN");
    }

    private static bool IsEqualIgnoreCase(string a, string b)
    {
      return string.Equals(
        (a ?? string.Empty).Trim(),
        (b ?? string.Empty).Trim(),
        StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractChParenExpected(string moduleName, out string expected)
    {
      expected = null;
      if (string.IsNullOrWhiteSpace(moduleName)) return false;

      var m = ChParenPattern.Match(moduleName);
      if (!m.Success || m.Groups.Count < 2) return false;

      string inner = m.Groups[1].Value.Trim();
      if (string.IsNullOrEmpty(inner)) return false;

      expected = "ch " + inner;
      return true;
    }

    private bool ContainsPhtOrCor(string moduleName, int n)
    {
      if (string.IsNullOrWhiteSpace(moduleName)) return false;
      string spaced = "pht " + n;
      string compact = "pht" + n;
      string corSpaced = "cor " + n;
      string corCompact = "cor" + n;
      return _util.IsStringContains(moduleName, spaced)
          || _util.IsStringContains(moduleName, compact)
          || _util.IsStringContains(moduleName, corSpaced)
          || _util.IsStringContains(moduleName, corCompact);
    }

    private bool IsMisMatchedTag(TraceResult r)
    {
      if (_util.IsStringContains(r.TargetNodeDivision, "equipment") &&
          _util.IsStringContainsOneOfList(r.TargetNodeSubDivision1, new List<string> { "MAIN", "SUB" }))
      {
        if (EquipmentTagMatcher.AreMismatched(r.TargetNodeTag, r.StartEquipmentTag))
          return true;
      }

      if (_util.IsStringContains(r.TargetNodeDivision, "equipment") &&
          _util.IsStringContainsOneOfList(r.TargetNodeSubDivision1, new List<string> { "PCS" }))
      {
        if (EquipmentTagMatcher.AreMismatched(r.StartEquipmentTag, r.TargetNodeTag))
          return true;
      }

      if (_util.IsStringContains(r.TargetNodeSubDivision1, "6-gcs-s.gas") ||
          _util.IsStringContains(r.TargetNodeSubDivision1, "6-gcs-chemical"))
      {
        if (!EquipmentTagMatcher.AreRelated(r.StartEquipmentTag, r.TargetPocConnectionTag))
          return true;
      }

      return false;
    }
  }
}
