using System;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// Testbench의 ddw id. uniqueidentifier 문자열을 대문자로 적는다.
    /// </summary>
    public static class DdwId
    {
        public static string Text(Guid id)
        {
            if (id == Guid.Empty)
                return "";
            return id.ToString("D").ToUpperInvariant();
        }
    }
}
