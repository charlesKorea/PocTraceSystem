using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Core.Engine.Validation
{
  /// <summary>
  /// VOC-1 MainLine(Takeoff 경유 EQP) 판별. 엔진·검수 규칙이 공유.
  /// </summary>
  public static class MainLineTraceHelper
  {
    public static readonly string[] Utilities =
        { "vent", "vac", "fore line", "fore-line", "foreline" };

    public static bool IsMainLineTofChaseResult(TraceResult r, PocData startPoc)
    {
      if (r == null || !string.Equals(r.STATUS, "EQP", StringComparison.Ordinal))
        return false;

      var util = new Util();
      if (!util.IsStringContains(r.StartNodeSubDivision1, "MAIN"))
        return false;

      string ut = startPoc?.UtilityName ?? string.Empty;
      if (!Utilities.Any(u => util.IsStringContains(ut, u)))
        return false;

      return r.Steps != null &&
             r.Steps.Any(s => string.Equals(s.NodeType, "Takeoff", StringComparison.OrdinalIgnoreCase));
    }
  }
}
