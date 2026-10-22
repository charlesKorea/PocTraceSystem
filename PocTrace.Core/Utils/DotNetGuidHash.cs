using System;

namespace PocTrace.Core.Utils
{
  /// <summary>
  /// WEBDB.WEBDEV5D.GetDotNetHashCode(Guid)와 동일한 .NET Guid.GetHashCode 결과 (long 승격).
  /// Pipe 등 캐시에 InstanceNumericId가 없을 때 Instance Id 열 채움용.
  /// </summary>
  public static class DotNetGuidHash
  {
    public static long FromGuid(Guid id)
    {
      return id.GetHashCode();
    }
  }
}
