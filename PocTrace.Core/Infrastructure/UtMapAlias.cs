using System.Collections.Generic;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// x2_m 행이 없으면 x2 의 UT 매핑을 쓴다. x2_m 행이 있으면 그 행만 쓴다.
    /// </summary>
    public static class UtMapAlias
    {
        public static void CopyX2WhenX2mMissing(
            Dictionary<string, Dictionary<string, Dictionary<string, string>>> map)
        {
            if (map == null)
                return;
            Dictionary<string, Dictionary<string, string>> own;
            if (map.TryGetValue(Data.DdwLine.CacheKey, out own) && own != null && own.Count > 0)
                return;
            Dictionary<string, Dictionary<string, string>> x2;
            if (!map.TryGetValue("x2", out x2) || x2 == null)
                return;
            map[Data.DdwLine.CacheKey] = x2;
        }

        public static Dictionary<string, Dictionary<string, string>> Resolve(
            Dictionary<string, Dictionary<string, Dictionary<string, string>>> maps,
            string dbName)
        {
            var empty = new Dictionary<string, Dictionary<string, string>>(System.StringComparer.OrdinalIgnoreCase);
            if (maps == null || string.IsNullOrWhiteSpace(dbName))
                return empty;
            Dictionary<string, Dictionary<string, string>> map;
            if (maps.TryGetValue(dbName, out map) && map != null && map.Count > 0)
                return map;
            if (Data.DdwLine.IsCacheKey(dbName) && maps.TryGetValue("x2", out map) && map != null)
                return map;
            return map ?? empty;
        }
    }
}
