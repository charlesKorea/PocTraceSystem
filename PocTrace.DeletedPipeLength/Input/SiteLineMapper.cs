using System;
using System.Collections.Generic;

namespace PocTrace.DeletedPipeLength.Input
{
    public static class SiteLineMapper
    {
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "X1", "x1" },
                { "X1L", "x1" },
                { "X2", "x2" },
                { "X2-PH1", "x2" },
                { "X2PH1", "x2" },
                { "X2_PH1", "x2" },
                { "X2-2", "x2_2" },
                { "X2-PH2", "x2_2" },
                { "X2PH2", "x2_2" },
                { "X2_2", "x2_2" },
                { "X2_PH2", "x2_2" }
            };

        public static bool TryMap(string siteRaw, out string dbKey)
        {
            dbKey = null;
            if (string.IsNullOrWhiteSpace(siteRaw))
                return false;

            string key = siteRaw.Trim();
            if (Aliases.TryGetValue(key, out dbKey))
                return true;

            var allowed = PocTrace.Core.Infrastructure.GlobalConfig.TargetDatabases;
            if (allowed == null)
                return false;
            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    dbKey = allowed[i];
                    return true;
                }
            }
            return false;
        }
    }
}
