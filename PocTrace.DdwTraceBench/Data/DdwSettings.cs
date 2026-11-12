using System.Text.RegularExpressions;

namespace PocTrace.DdwTraceBench.Data
{
    public sealed class DdwSettings
    {
        public const long LiveLimit = 1000000000L;

        public string LinkedServer = "remote_monami";
        public string Catalog = "monami";
        public string TagAttributeName = "tag";
        public int CommandTimeoutSeconds = 600;

        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9_\\-]+$", RegexOptions.Compiled);

        public string Prefix
        {
            get { return Qualified("") ; }
        }

        public string InformationSchema
        {
            get { return Qualified("INFORMATION_SCHEMA.COLUMNS"); }
        }

        private string Qualified(string tail)
        {
            Validate(Catalog, "카탈로그 이름");
            string head;
            if (string.IsNullOrWhiteSpace(LinkedServer))
                head = "[" + Catalog.Trim() + "].";
            else
            {
                Validate(LinkedServer.Trim(), "Linked server 이름");
                head = "[" + LinkedServer.Trim() + "].[" + Catalog.Trim() + "].";
            }
            return tail.Length == 0 ? head + "[dbo]." : head + tail;
        }

        private static void Validate(string value, string label)
        {
            if (string.IsNullOrEmpty(value) || !NamePattern.IsMatch(value))
                throw new System.ArgumentException(label + "에는 영문, 숫자, _, - 만 쓸 수 있습니다: " + value);
        }
    }
}
