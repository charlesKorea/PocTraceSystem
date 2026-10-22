using System;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// VOC: STATUS=EQP이고 Subdivision1에 gcs, Connector Information이 In이면
  /// 연결정보상이·연결정보누락에서 Connection Tag / Connector Tag를 보지 않는다.
  /// Case1 = S측, Case2 = T측. 대소문자 무시.
  /// </summary>
  internal static class GcsInConnectionTagExemption
  {
    public static bool Applies(TraceResult r)
    {
      return AppliesToStart(r) || AppliesToTarget(r);
    }

    public static bool AppliesToStart(TraceResult r)
    {
      return IsEqp(r)
          && ContainsGcs(r.StartNodeSubDivision1)
          && IsIn(r.StartPocInformation);
    }

    public static bool AppliesToTarget(TraceResult r)
    {
      return IsEqp(r)
          && ContainsGcs(r.TargetNodeSubDivision1)
          && IsIn(r.TargetConnectorInformation);
    }

    private static bool IsEqp(TraceResult r)
    {
      return r != null
          && string.Equals(r.STATUS, "EQP", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsGcs(string value)
    {
      return !string.IsNullOrEmpty(value)
          && value.IndexOf("gcs", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsIn(string connectorInfo)
    {
      return string.Equals(
        (connectorInfo ?? string.Empty).Trim(),
        "IN",
        StringComparison.OrdinalIgnoreCase);
    }
  }
}
