using System;
using System.Collections.Generic;
using System.Linq;

namespace JLic
{
    public static class LicenseCatalog
    {
        public static readonly string[] KnownGroups = { "Admin", "SDS", "SCS", "2차" };
        public static readonly string[] KnownApps = { "AL", "JP", "SP", "BAM", "PT" };

        public static IEnumerable<string> SplitApps(string accessApps)
        {
            if (string.IsNullOrWhiteSpace(accessApps))
                yield break;

            foreach (var token in accessApps.Split(new[] { '|', '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var value = token.Trim();
                if (value.Length > 0)
                    yield return value;
            }
        }

        public static string[] NormalizeAppArray(string accessApps)
        {
            return SplitApps(accessApps)
                .Select(x => x.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string NormalizeApps(string accessApps)
        {
            return string.Join("|", NormalizeAppArray(accessApps));
        }

        public static bool IsKnownGroup(string group)
        {
            return KnownGroups.Any(x => x.Equals(group, StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasGroup(string actual, params string[] groups)
        {
            if (string.IsNullOrWhiteSpace(actual) || groups == null)
                return false;
            return groups.Any(g => !string.IsNullOrWhiteSpace(g)
                && actual.Equals(g.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasAnyApp(IEnumerable<string> actual, params string[] required)
        {
            if (actual == null || required == null)
                return false;

            var owned = new HashSet<string>(
                actual.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var code in required)
            {
                if (!string.IsNullOrWhiteSpace(code) && owned.Contains(code.Trim()))
                    return true;
            }
            return false;
        }
    }
}
