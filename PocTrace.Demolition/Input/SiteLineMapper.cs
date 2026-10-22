using System;
using System.Collections.Generic;

namespace PocTrace.Demolition.Input
{
  /// <summary>
  /// Input Site 문자열 → BinaryCache DB 키 (x1 / x2 / x2_2).
  /// A1에서 Input 검증에 사용. A0에서 골격만 제공.
  /// </summary>
  public static class SiteLineMapper
  {
    private static readonly Dictionary<string, string> Aliases =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        { "X1", "x1" },
        { "X1L", "x1" },
        { "X2", "x2" },
        { "X2-PH1", "x2" },
        { "X2PH1", "x2" },
        { "X2_PH1", "x2" },
        { "X2-2", "x2_2" },
        { "X2-PH2", "x2_2" },
        { "X2PH2", "x2_2" },
        { "X2_2", "x2_2" },
        { "X2_PH2", "x2_2" },
      };

    public static bool TryMap(string siteRaw, out string dbKey)
    {
      dbKey = null;
      if (string.IsNullOrWhiteSpace(siteRaw))
        return false;

      var key = siteRaw.Trim();
      if (Aliases.TryGetValue(key, out dbKey))
        return true;

      // 이미 캐시 키 형태면 통과
      if (string.Equals(key, "x1", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "x2", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "x2_2", StringComparison.OrdinalIgnoreCase))
      {
        dbKey = key.ToLowerInvariant();
        return true;
      }

      return false;
    }
  }
}
