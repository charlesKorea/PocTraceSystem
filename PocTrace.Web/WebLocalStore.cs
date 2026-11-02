using System;
using System.Collections.Generic;
using System.IO;
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

        public static void Append(HistoryEntry entry)
        {
            lock (Gate)
            {
                List<HistoryEntry> list = LoadHistory();
                DateTime cutoff = DateTime.UtcNow.AddDays(-40);
                list.RemoveAll(item => SharedHistoryStore.ParseUtc(item.At) < cutoff);
                list.Add(entry);
                File.WriteAllText(HistoryPath(), JsonConvert.SerializeObject(list));
            }
        }

        public static void RemoveRows(HashSet<string> keys, string currentUser)
        {
            lock (Gate)
            {
                List<HistoryEntry> list = LoadHistory();
                var next = new List<HistoryEntry>();
                bool changed = false;
                foreach (HistoryEntry entry in list)
                {
                    HistoryEntry kept;
                    int removed = InspectionHistory.Prune(entry, keys, currentUser, out kept);
                    if (removed > 0)
                        changed = true;
                    if (kept != null)
                        next.Add(kept);
                }
                if (changed)
                    File.WriteAllText(HistoryPath(), JsonConvert.SerializeObject(next));
            }
        }

        public static List<HistoryEntry> LoadAll()
        {
            lock (Gate)
            {
                return LoadHistory();
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
    }
}
