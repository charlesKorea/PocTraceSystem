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
            get
            {
                Validate(LinkedServer, "Linked server 이름");
                Validate(Catalog, "카탈로그 이름");
                return "[" + LinkedServer + "].[" + Catalog + "].[dbo].";
            }
        }

        public string InformationSchema
        {
            get
            {
                Validate(LinkedServer, "Linked server 이름");
                Validate(Catalog, "카탈로그 이름");
                return "[" + LinkedServer + "].[" + Catalog + "].INFORMATION_SCHEMA.COLUMNS";
            }
        }

        private static void Validate(string value, string label)
        {
            if (string.IsNullOrEmpty(value) || !NamePattern.IsMatch(value))
                throw new System.ArgumentException(label + "에는 영문, 숫자, _, - 만 쓸 수 있습니다: " + value);
        }
    }
}
