using System.Reflection;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// UI가 컴파일 때 올린 x.xxx 버전. 엑셀 Summary V1과 폼 하단에 같은 문자열을 쓴다.
    /// MetroForm.ProductVersion(문자열)과 이름이 겹치지 않게 둔다.
    /// </summary>
    public static class TraceAppVersion
    {
        public static string Current { get; set; }

        public static string Display()
        {
            if (!string.IsNullOrWhiteSpace(Current))
                return Current.Trim();
            var asm = Assembly.GetEntryAssembly();
            if (asm != null)
            {
                var attrs = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                if (attrs != null && attrs.Length > 0)
                {
                    var text = ((AssemblyInformationalVersionAttribute)attrs[0]).InformationalVersion;
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
            return "1.001";
        }
    }
}
