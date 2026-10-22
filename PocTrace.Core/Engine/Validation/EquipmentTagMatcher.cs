using System;

using System.Collections.Generic;

using System.Linq;



namespace PocTrace.Core.Engine.Validation

{

  /// <summary>

  /// 설비 Tag 비교 유틸.

  /// Scrubber 등 Tag가 "a,b" 형태(콤마 구분 다중 토큰)인 경우,

  /// 검색/검수 시 단일 토큰("a")만 들어와도 동일 설비로 인식한다.

  /// Damper[a,b] 형태(TOF EndName/T.Tag)는 대괄호 안 내용을 정규화해 비교한다.

  /// </summary>

  public static class EquipmentTagMatcher

  {

    private static readonly char[] TokenSeparators = { ',', ';' };



    /// <summary>

    /// 두 Tag가 동일 설비(또는 상호 포함)로 간주되면 true.

    /// - 정확 일치

    /// - Damper[a,b] 등 대괄호 unwrap

    /// - T.Tag가 S.Tag를 포함(또는 반대)하면 정상

    /// - 콤마 토큰 교집합(scrubber: "a,b" ↔ "a" / "b")

    /// - 레거시 양방향 substring(MAIN/SUB 접미사 등 기존 검수 호환)

    /// </summary>

    public static bool AreRelated(string tagA, string tagB)

    {

      if (string.IsNullOrWhiteSpace(tagA) || string.IsNullOrWhiteSpace(tagB))

        return false;



      var aRaw = tagA.Trim();

      var bRaw = tagB.Trim();



      if (string.Equals(aRaw, bRaw, StringComparison.OrdinalIgnoreCase))

        return true;



      // 예) T.Tag=Damper[설비a, 설비b], S.Tag=설비a, 설비b → T가 S를 포함하면 정상

      if (aRaw.IndexOf(bRaw, StringComparison.OrdinalIgnoreCase) >= 0

          || bRaw.IndexOf(aRaw, StringComparison.OrdinalIgnoreCase) >= 0)

        return true;



      var a = UnwrapBracketContent(aRaw);

      var b = UnwrapBracketContent(bRaw);



      if (!string.Equals(a, aRaw, StringComparison.OrdinalIgnoreCase)

          || !string.Equals(b, bRaw, StringComparison.OrdinalIgnoreCase))

      {

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))

          return true;

        if (a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0

            || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)

          return true;

        // unwrap 결과가 raw 반대편에 포함되는 경우 (Damper[...] vs 설비a,설비b)

        if (aRaw.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0

            || bRaw.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)

          return true;

      }



      var tokensA = SplitTokens(a);

      var tokensB = SplitTokens(b);



      // Scrubber 등 다중 토큰: 토큰 교집합이 있으면 동일 계열로 본다.

      if (tokensA.Count > 1 || tokensB.Count > 1)

      {

        foreach (var ta in tokensA)

        {

          foreach (var tb in tokensB)

          {

            if (string.Equals(ta, tb, StringComparison.OrdinalIgnoreCase))

              return true;

            // Damper[설비a] 잔여 토큰 vs 설비a

            if (ta.IndexOf(tb, StringComparison.OrdinalIgnoreCase) >= 0

                || tb.IndexOf(ta, StringComparison.OrdinalIgnoreCase) >= 0)

              return true;

          }

        }



        // 한쪽의 모든 토큰이 상대 raw/정규 문자열에 포함되면 정상

        // (S=설비a,설비b / T=Damper[설비a, 설비b])

        if (tokensA.Count > 0 && tokensA.All(t =>

              bRaw.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0

              || b.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))

          return true;

        if (tokensB.Count > 0 && tokensB.All(t =>

              aRaw.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0

              || a.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))

          return true;



        return false;

      }



      // 단일 토큰 레거시: 양방향 포함

      return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0

          || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0;

    }



    /// <summary>태그가 서로 다르면(관련 없으면) true — 검수 이슈 판정용.</summary>

    public static bool AreMismatched(string tagA, string tagB)

    {

      if (string.IsNullOrWhiteSpace(tagA) || string.IsNullOrWhiteSpace(tagB))

        return false;

      return !AreRelated(tagA, tagB);

    }



    /// <summary>콤마/세미콜론으로 분리한 토큰 목록(공백 trim, 빈값 제거).</summary>

    public static IReadOnlyList<string> SplitTokens(string tag)

    {

      if (string.IsNullOrWhiteSpace(tag))

        return Array.Empty<string>();



      return tag

          .Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)

          .Select(t => t.Trim())

          .Where(t => t.Length > 0)

          .ToList();

    }



    /// <summary>

    /// Damper[a,b] / Prefix[content] → content. 대괄호가 없으면 원문.

    /// </summary>

    public static string UnwrapBracketContent(string tag)

    {

      if (string.IsNullOrWhiteSpace(tag))

        return string.Empty;



      var s = tag.Trim();

      int l = s.IndexOf('[');

      int r = s.LastIndexOf(']');

      if (l >= 0 && r > l)

        return s.Substring(l + 1, r - l - 1).Trim();

      return s;

    }



    /// <summary>Subdivision2에 scrubber가 포함되면 true.</summary>

    public static bool IsScrubberSubdivision(string subDivision2)

    {

      return !string.IsNullOrEmpty(subDivision2)

          && subDivision2.IndexOf("scrubber", StringComparison.OrdinalIgnoreCase) >= 0;

    }

  }

}


