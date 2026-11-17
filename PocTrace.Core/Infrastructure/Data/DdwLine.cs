using System;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>
    /// DDW 캐시 라인. 폴더 키는 x2_m 이고 S5D의 x1, x2, x2_2 와 섞지 않는다.
    /// SQL 카탈로그는 캐시 키와 다르다. 기본은 Linked Server remote_monami 의 monami 다.
    /// </summary>
    public static class DdwLine
    {
        public const string CacheKey = "x2_m";
        public const string ZeroTemplateId = "00000000-0000-0000-0000-000000000000";

        public static string Catalog
        {
            get { return Setting("DdwCatalog", "monami"); }
        }

        public static string LinkedServer
        {
            get { return Setting("DdwLinkedServer", "remote_monami"); }
        }

        /// <summary>
        /// [remote_monami].[monami] 또는 Linked Server 가 비면 [monami]. DdwSql 이 .dbo.테이블 을 붙인다.
        /// </summary>
        public static string CatalogPrefix()
        {
            string catalog = Bracket(Catalog);
            string linked = LinkedServer;
            if (string.IsNullOrWhiteSpace(linked))
                return catalog;
            return Bracket(linked) + "." + catalog;
        }

        private static string Setting(string key, string fallback)
        {
            string value = SecretConfigStore.AppSetting(key);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            return value.Trim();
        }

        private static string Bracket(string name)
        {
            if (!IsPlainName(name))
                throw new InvalidOperationException("DDW 식별자가 이름이 아닙니다: " + name);
            return "[" + name.Replace("]", "]]") + "]";
        }

        private static bool IsPlainName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// GCS 설비의 Information. IN 은 POC 이름에 _in_ 이 있고, OUT 은 stick_ 다음에 숫자가 있다.
    /// </summary>
    public static class DdwGcsEnd
    {
        public static string FromPocName(string pocName)
        {
            if (string.IsNullOrEmpty(pocName))
                return null;
            if (pocName.IndexOf("_in_", StringComparison.OrdinalIgnoreCase) >= 0)
                return "IN";
            int at = pocName.IndexOf("stick_", StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && at + 6 < pocName.Length && char.IsDigit(pocName[at + 6]))
                return "OUT";
            return null;
        }

        public static string Information(string memo, string pocName)
        {
            if (!string.IsNullOrEmpty(memo))
                return memo;
            return FromPocName(pocName) ?? "";
        }
    }
}
