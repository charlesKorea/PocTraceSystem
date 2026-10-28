using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PocTrace.Web
{
    /// <summary>
    /// 이 PC 사용자 폴더(%LocalAppData%\PocTrace\Web)에 화면 설정과 검수 기록을 둔다.
    /// </summary>
    internal static class WebLocalStore
    {
        private static readonly object Gate = new object();

        public static object ReadPrefs()
        {
            lock (Gate)
            {
                PrefsFile file = LoadPrefs();
                return new { lang = file.Lang, theme = file.Theme };
            }
        }

        public static void WritePrefs(string lang, string theme)
        {
            lock (Gate)
            {
                var file = new PrefsFile
                {
                    Lang = lang == "zh" ? "zh" : "ko",
                    Theme = theme == "dark" ? "dark" : "light"
                };
                File.WriteAllText(PrefsPath(), JsonConvert.SerializeObject(file));
            }
        }

        public static void Append(string userName, string method, string line, IEnumerable<string> targets)
        {
            var clean = new List<string>();
            if (targets != null)
            {
                foreach (string target in targets)
                {
                    if (string.IsNullOrWhiteSpace(target))
                        continue;
                    string trimmed = target.Trim();
                    bool exists = false;
                    for (int i = 0; i < clean.Count; i++)
                    {
                        if (string.Equals(clean[i], trimmed, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                        clean.Add(trimmed);
                }
            }

            lock (Gate)
            {
                List<HistoryEntry> list = LoadHistory();
                DateTime cutoff = DateTime.UtcNow.AddDays(-40);
                list.RemoveAll(item => ParseUtc(item.At) < cutoff);
                list.Add(new HistoryEntry
                {
                    At = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    UserName = userName ?? "",
                    Method = method ?? "",
                    Line = line ?? "",
                    Targets = clean
                });
                File.WriteAllText(HistoryPath(), JsonConvert.SerializeObject(list));
            }
        }

        public static object Recent(string currentUser)
        {
            lock (Gate)
            {
                List<HistoryEntry> list = LoadHistory();
                DateTime cutoff = DateTime.UtcNow.AddDays(-31);
                var rows = new List<object>();
                foreach (HistoryEntry entry in list.OrderByDescending(item => ParseUtc(item.At)))
                {
                    if (ParseUtc(entry.At) < cutoff)
                        continue;
                    var targets = entry.Targets == null || entry.Targets.Count == 0
                        ? new List<string> { "" }
                        : entry.Targets;
                    foreach (string target in targets)
                    {
                        rows.Add(new
                        {
                            at = entry.At,
                            userName = string.IsNullOrWhiteSpace(entry.UserName) ? (currentUser ?? "") : entry.UserName,
                            method = entry.Method ?? "",
                            line = entry.Line ?? "",
                            target = target ?? ""
                        });
                    }
                }
                return new
                {
                    days = 31,
                    userName = currentUser ?? "",
                    rows
                };
            }
        }

        private static string PrefsPath()
        {
            return Path.Combine(PortalService.WorkRoot, "ui-prefs.json");
        }

        private static string HistoryPath()
        {
            return Path.Combine(PortalService.WorkRoot, "inspection-history.json");
        }

        private static PrefsFile LoadPrefs()
        {
            try
            {
                if (!File.Exists(PrefsPath()))
                    return new PrefsFile();
                PrefsFile file = JsonConvert.DeserializeObject<PrefsFile>(File.ReadAllText(PrefsPath()));
                if (file == null)
                    return new PrefsFile();
                if (file.Lang != "zh")
                    file.Lang = "ko";
                if (file.Theme != "dark")
                    file.Theme = "light";
                return file;
            }
            catch
            {
                return new PrefsFile();
            }
        }

        private static List<HistoryEntry> LoadHistory()
        {
            try
            {
                if (!File.Exists(HistoryPath()))
                    return new List<HistoryEntry>();
                List<HistoryEntry> list = JsonConvert.DeserializeObject<List<HistoryEntry>>(File.ReadAllText(HistoryPath()));
                return list ?? new List<HistoryEntry>();
            }
            catch
            {
                return new List<HistoryEntry>();
            }
        }

        private static DateTime ParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed))
                return parsed.ToUniversalTime();
            return DateTime.MinValue;
        }

        private sealed class PrefsFile
        {
            public string Lang { get; set; }
            public string Theme { get; set; }

            public PrefsFile()
            {
                Lang = "ko";
                Theme = "light";
            }
        }

        private sealed class HistoryEntry
        {
            public string At { get; set; }
            public string UserName { get; set; }
            public string Method { get; set; }
            public string Line { get; set; }
            public List<string> Targets { get; set; }
        }
    }
}
