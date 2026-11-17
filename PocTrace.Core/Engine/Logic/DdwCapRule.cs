using System;

namespace PocTrace.Core.Engine.Logic
{
    /// <summary>
    /// DDW Cap. 이름 또는 division 세 칸에 cap 이 있으면 Cap이다.
    /// division 에 equipment 가 있으면 Cap이 아니다.
    /// 기타, vac, flange END, 미사용 같은 S5D 이름 목록은 쓰지 않는다.
    /// </summary>
    public static class DdwCapRule
    {
        public static bool IsCap(string name, string division, string sub1, string sub2)
        {
            if (Has(division, "equipment") || Has(sub1, "equipment") || Has(sub2, "equipment"))
                return false;
            return Has(name, "cap") || Has(division, "cap") || Has(sub1, "cap") || Has(sub2, "cap");
        }

        private static bool Has(string text, string token)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
