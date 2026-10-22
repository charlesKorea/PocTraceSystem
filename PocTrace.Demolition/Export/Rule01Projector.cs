using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Models;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Rule 1 = Detail Trace 투영. Sheet1 헤더/행 생성.
  /// 선행열: Instance Id / Name / EQPID / Construction Code / Division / Sub1 / Sub2 / GUID
  /// + Check Result/Issue + Detail 잔여(T:*, UT* …). S: Name/Tag/Sub/GUID는 선행열로 이동했으므로 잔여에서 제외.
  /// </summary>
  public static class Rule01Projector
  {
    public const string SheetName = "Rule01";
    public const string RuleId = "Rule01";
    public const string RuleTitle = "EQPID Detail Trace 투영";

    /// <summary>Sheet1 전체 헤더 (선행 철거 스키마 + Check/Issue + Detail 잔여).</summary>
    public static readonly string[] Headers =
    {
      // 철거 선행 열
      "Instance Id",
      "Instance Name",
      "EQPID",
      "Construction Code",
      "Division",
      "Subdivision1",
      "Subdivision2",
      "GUID",
      // Check / Issue (Locked 포함)
      "Check Result",
      "Fail Count",
      "Issue Type",
      "Feedback",
      "Issue Owner",
      // Detail 잔여 (S: Name/Tag/Sub/GUID 제외 — 선행열로 이동)
      "Line",
      "S: Connector GUID",
      "분기배관수",
      "S: Connector Tag",
      "S: Connector Name",
      "S: Connector Information",
      "S: Connection Tag",
      "S: Module Name",
      "S: PocNo",
      "Status",
      "T: Name",
      "T: Tag",
      "T: GUID",
      "T: Connector GUID",
      "T: Connector Name",
      "T: Connector Information",
      "T: Connection Tag",
      "T: Module Name",
      "T: PocNo",
      "T: Division",
      "T: Subdivision1",
      "T: Subdivision2",
      "S: Utility Group",
      "S: Utility",
      "S: Connected UT",
      "T: Connected UT",
      "T: Utility",
      "S: Size"
    };

    public static object[] ToRow(TraceResult r)
    {
      if (r == null)
        return new object[Headers.Length];

      return new object[]
      {
        r.InstanceNumericId,
        r.StartNodeName ?? string.Empty,
        r.StartNodeTag ?? string.Empty,
        r.ConstructionCode ?? string.Empty,
        r.StartNodeDivision ?? string.Empty,
        r.StartNodeSubDivision1 ?? string.Empty,
        r.StartNodeSubDivision2 ?? string.Empty,
        r.StartNodeGUID.ToString(),
        r.CheckResult ?? string.Empty,
        r.FailCount,
        r.IssueType ?? string.Empty,
        r.feedback ?? string.Empty,
        r.IssueBelongTo ?? string.Empty,
        r.Line ?? string.Empty,
        r.StartPocGUID ?? string.Empty,
        r.BranchedPipes,
        r.StartPocTag ?? string.Empty,
        r.StartPocName ?? string.Empty,
        r.StartPocInformation ?? string.Empty,
        r.StartPocConnectionTag ?? string.Empty,
        r.StartPocModuleName ?? string.Empty,
        r.StartPocNo ?? string.Empty,
        r.Status ?? string.Empty,
        r.TargetNodeName ?? string.Empty,
        r.TargetNodeTag ?? string.Empty,
        r.TargetNodeGUID.ToString(),
        r.TargetPocGUID ?? string.Empty,
        r.TargetPocName ?? string.Empty,
        r.TargetConnectorInformation ?? string.Empty,
        r.TargetPocConnectionTag ?? string.Empty,
        r.TargetPocModuleName ?? string.Empty,
        r.TargetPocNo ?? string.Empty,
        r.TargetNodeDivision ?? string.Empty,
        r.TargetNodeSubDivision1 ?? string.Empty,
        r.TargetNodeSubDivision2 ?? string.Empty,
        r.StartPocUtilityGroup ?? string.Empty,
        r.StartPocUtilityName ?? string.Empty,
        r.StartPocConnectedUtility ?? string.Empty,
        r.TargetPocConnectedUtility ?? string.Empty,
        r.TargetPocUtilityName ?? string.Empty,
        r.StartPocSize ?? string.Empty
      };
    }

    public static IEnumerable<object[]> ToRows(IEnumerable<TraceResult> results)
    {
      if (results == null) yield break;
      foreach (var r in results)
        yield return ToRow(r);
    }

    public static int CountFail(IEnumerable<TraceResult> results)
    {
      if (results == null) return 0;
      return results.Count(r => r != null &&
        !string.Equals(r.CheckResult, "Pass", StringComparison.OrdinalIgnoreCase));
    }
  }
}
