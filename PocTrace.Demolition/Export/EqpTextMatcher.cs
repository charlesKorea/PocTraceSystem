using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Demolition.Export
{
  internal static class EqpTextMatcher
  {
    internal static List<string> NormalizeEqps(IEnumerable<string> eqpIds)
    {
      if (eqpIds == null) return new List<string>();
      return eqpIds
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .Select(e => e.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    internal static bool IsExactMatch(string source, string eqp)
    {
      if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(eqp)) return false;
      var text = source.Trim();
      var key = eqp.Trim();

      if (string.Equals(text, key, StringComparison.OrdinalIgnoreCase))
        return true;

      int start = 0;
      while (start < text.Length)
      {
        int idx = text.IndexOf(key, start, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        int left = idx - 1;
        int right = idx + key.Length;
        bool leftOk = left < 0 || !IsTagChar(text[left]);
        bool rightOk = right >= text.Length || !IsTagChar(text[right]);
        if (leftOk && rightOk) return true;

        start = idx + 1;
      }

      return false;
    }

    internal static List<string> FindMatches(string source, IEnumerable<string> eqpList)
    {
      var hits = new List<string>();
      if (string.IsNullOrWhiteSpace(source) || eqpList == null) return hits;
      foreach (var eqp in eqpList)
      {
        if (IsExactMatch(source, eqp))
          hits.Add(eqp);
      }
      return hits;
    }

    /// <summary>
    /// 태그 연속 문자. '_'는 Hookup(예: PCW_AAA01) 구분자로 쓰이므로 제외.
    /// 문자/숫자만 연속으로 보면 aaa01 ⊂ aaa012 오탐을 막는다.
    /// </summary>
    private static bool IsTagChar(char c)
    {
      return char.IsLetterOrDigit(c);
    }
  }
}
