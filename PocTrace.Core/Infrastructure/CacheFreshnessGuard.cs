using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// Startup / ops check for shared binary cache age (manifest SavedAt only — lightweight).
    /// </summary>
    public static class CacheFreshnessGuard
    {
        public sealed class Result
        {
            public bool Allowed { get; set; }
            public string Message { get; set; }
            public IList<string> Details { get; set; } = new List<string>();
        }

        /// <summary>
        /// Viewer hard gate: every TargetDatabase must have a readable cache whose SavedAt
        /// is within <see cref="GlobalConfig.CacheMaxAgeHours"/> of now.
        /// </summary>
        public static Result EvaluateViewerStartup(IEnumerable<string> databases = null)
        {
            var dbs = (databases ?? GlobalConfig.TargetDatabases)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (dbs.Length == 0)
            {
                return new Result
                {
                    Allowed = false,
                    Message = "TargetDatabases가 비어 있습니다. App.config를 확인하세요."
                };
            }

            var details = new List<string>();
            var maxAge = GlobalConfig.CacheMaxAgeHours;
            var now = DateTime.Now;
            bool anyFail = false;

            foreach (var db in dbs)
            {
                if (!TryGetSavedAt(db, out var savedAt, out var version, out var source))
                {
                    anyFail = true;
                    details.Add($"[{db}] 캐시 없음 또는 손상 ({GlobalConfig.GetCacheDirectory(db)})");
                    continue;
                }

                var ageHours = (now - savedAt).TotalHours;
                if (ageHours > maxAge)
                {
                    anyFail = true;
                    details.Add(
                        $"[{db}] 캐시 시각 {savedAt:yyyy-MM-dd HH:mm} (약 {ageHours:F1}시간 전, 허용 {maxAge:0}h, v{version}, {source})");
                }
                else
                {
                    details.Add(
                        $"[{db}] OK — {savedAt:yyyy-MM-dd HH:mm} (약 {ageHours:F1}시간 전, v{version}, {source})");
                }
            }

            if (!anyFail)
            {
                return new Result
                {
                    Allowed = true,
                    Message = "캐시 신선도 검사 통과",
                    Details = details
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine("공유 Binary Cache가 없거나 48시간(설정값)을 초과했습니다.");
            sb.AppendLine("PocTrace.Admin 일배치로 캐시를 갱신한 뒤 Viewer를 다시 실행하세요.");
            sb.AppendLine();
            sb.AppendLine($"SharedCachePath: {GlobalConfig.SharedCachePath}");
            sb.AppendLine($"CacheMaxAgeHours: {maxAge}");
            sb.AppendLine();
            foreach (var line in details)
                sb.AppendLine(line);

            return new Result
            {
                Allowed = false,
                Message = sb.ToString(),
                Details = details
            };
        }

        public static bool TryGetSavedAt(string dbName, out DateTime savedAt, out int version, out string source)
        {
            savedAt = default;
            version = 0;
            source = null;

            try
            {
                var manifestPath = GlobalConfig.GetManifestPath(dbName);
                if (File.Exists(manifestPath))
                {
                    using (var fs = CacheGenerationStore.OpenRead(manifestPath, 4 * 1024))
                    using (var r = new BinaryReader(fs, Encoding.UTF8))
                    {
                        int magic = r.ReadInt32();
                        if (magic != CacheConstants.ManifestMagic)
                        {
                            Log.Warning("[CacheGuard] Invalid manifest magic for {Db}", dbName);
                            return false;
                        }

                        version = r.ReadInt32();
                        savedAt = DateTime.FromBinary(r.ReadInt64());
                        source = "v5-manifest";
                        return true;
                    }
                }

                // Legacy v4 single file
                var legacyPath = GlobalConfig.GetLegacyCacheFilePath(dbName);
                if (File.Exists(legacyPath))
                {
                    using (var fs = CacheGenerationStore.OpenRead(legacyPath, 4 * 1024))
                    using (var r = new BinaryReader(fs, Encoding.UTF8))
                    {
                        version = r.ReadInt32();
                        savedAt = DateTime.FromBinary(r.ReadInt64());
                        source = "v4-legacy";
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[CacheGuard] Failed reading cache timestamp for {Db}", dbName);
            }

            return false;
        }
    }
}
