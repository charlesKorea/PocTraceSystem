using System;

namespace PocTrace.Core.Utils
{
  /// <summary>
  /// .NET Framework Guid.GetHashCode. WEBDB.WEBDEV5D.GetDotNetHashCode 와 같다.
  /// Guid의 _a, _b, _c, _f, _k 로 _a xor ((_b를 16비트 왼쪽) or (ushort)_c) xor ((_f를 24비트 왼쪽) or _k) 이다.
  /// 결과는 부호 있는 32비트를 long으로 넓힌 값이다. DDW에는 그 SQL 함수가 없어 캐시에서 계산한다.
  /// </summary>
  public static class DotNetGuidHash
  {
    public static long FromGuid(Guid id)
    {
      return id.GetHashCode();
    }
  }
}
