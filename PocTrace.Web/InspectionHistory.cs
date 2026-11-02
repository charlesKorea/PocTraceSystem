using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace PocTrace.Web
{
    internal sealed class HistoryEntry
    {
        public string At { get; set; }
        public string UserName { get; set; }
        public string Method { get; set; }
        public string Line { get; set; }
        public List<string> Targets { get; set; }
        public string Machine { get; set; }
    }

    internal sealed class HistoryQuery
    {
        public string User = "";
        public string Method = "";
        public string Line = "";
        public string Text = "";
        public DateTime FromUtc = DateTime.MinValue;
        public DateTime ToUtc = DateTime.MaxValue;
    }

    internal sealed class HistoryRow
    {
        public string At;
        public DateTime AtUtc;
        public string UserName;
        public string Method;
        public string Line;
        public string Target;
    }

    internal sealed class HistoryResult
    {
        public List<HistoryRow> Rows = new List<HistoryRow>();
        public bool Truncated;
        public bool SharedAvailable;
        public string SharedPath = "";
        public string SharedError = "";
        public List<string> Users = new List<string>();
        public List<string> Methods = new List<string>();
        public List<string> Lines = new List<string>();
    }

    /// <summary>
    /// 공용 폴더(기본 Y:\sds\999. data)에 모든 사용자의 검수 기록을 남기고 읽는다.
    /// 사용자·월마다 jsonl 파일을 따로 두어 여러 PC가 같은 파일을 동시에 열지 않게 한다.
    /// 공용 폴더를 쓸 수 없어도 개인 로컬 기록은 그대로 남는다.
    /// </summary>
    internal static class SharedHistoryStore
    {
        public const string DefaultRoot = @"Y:\sds\999. data";
        public const string FolderName = "PocTraceWebHistory";
        public const int MaxRows = 5000;

        private const int ProbeSeconds = 3;
        private const int ProbeCacheSeconds = 20;

        private static readonly object Gate = new object();
        private static DateTime _probeAt = DateTime.MinValue;
        private static bool _probeOk;
        private static string _probeError = "";

        public static string RootPath
        {
            get
            {
                string value = ConfigurationManager.AppSettings["SharedHistoryPath"];
                return string.IsNullOrWhiteSpace(value) ? DefaultRoot : value.Trim();
            }
        }

        public static string FolderPath
        {
            get { return Path.Combine(RootPath, FolderName); }
        }

        /// <summary>끊어진 네트워크 드라이브에서 오래 멈추지 않도록 별도 작업에서 확인한다.</summary>
        public static bool IsAvailable(out string error)
        {
            lock (Gate)
            {
                if ((DateTime.UtcNow - _probeAt).TotalSeconds < ProbeCacheSeconds)
                {
                    error = _probeError;
                    return _probeOk;
                }
            }

            bool ok = false;
            string message = "";
            try
            {
                string root = RootPath;
                Task<bool> probe = Task.Run(() => Directory.Exists(root));
                if (!probe.Wait(TimeSpan.FromSeconds(ProbeSeconds)))
                    message = "응답이 없습니다 / 无响应: " + root;
                else if (!probe.Result)
                    message = "폴더가 없습니다 / 文件夹不存在: " + root;
                else
                    ok = true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            lock (Gate)
            {
                _probeAt = DateTime.UtcNow;
                _probeOk = ok;
                _probeError = message;
            }
            error = message;
            return ok;
        }

        public static void Append(HistoryEntry entry)
        {
            string error;
            if (!IsAvailable(out error))
                throw new IOException(error);

            Directory.CreateDirectory(FolderPath);
            DateTime when = ParseUtc(entry.At);
            if (when == DateTime.MinValue)
                when = DateTime.UtcNow;
            string path = Path.Combine(
                FolderPath,
                when.ToString("yyyyMM", CultureInfo.InvariantCulture) + "_" + SafeName(entry.UserName) + ".jsonl");
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(entry) + "\r\n");

            Exception last = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        stream.Write(bytes, 0, bytes.Length);
                    return;
                }
                catch (IOException ex)
                {
                    last = ex;
                    Thread.Sleep(120 * (attempt + 1));
                }
            }
            throw last ?? new IOException(path);
        }

        public static List<HistoryEntry> ReadAll(HistoryQuery query)
        {
            var list = new List<HistoryEntry>();
            string error;
            if (!IsAvailable(out error) || !Directory.Exists(FolderPath))
                return list;

            foreach (string file in Directory.GetFiles(FolderPath, "*.jsonl"))
            {
                if (!MonthMayMatch(file, query))
                    continue;
                try
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string text;
                        while ((text = reader.ReadLine()) != null)
                        {
                            if (text.Length == 0)
                                continue;
                            try
                            {
                                HistoryEntry entry = JsonConvert.DeserializeObject<HistoryEntry>(text);
                                if (entry != null)
                                    list.Add(entry);
                            }
                            catch (JsonException)
                            {
                                /* 쓰는 도중 잘린 줄은 건너뛴다 */
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[Web] 공용 검수 기록을 읽지 못했습니다: {File}", file);
                }
            }
            return list;
        }

        public static DateTime ParseUtc(string value)
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

        private static bool MonthMayMatch(string file, HistoryQuery query)
        {
            string name = Path.GetFileNameWithoutExtension(file) ?? "";
            if (name.Length < 6)
                return true;
            DateTime month;
            if (!DateTime.TryParseExact(name.Substring(0, 6), "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month))
                return true;
            DateTime start = DateTime.SpecifyKind(month, DateTimeKind.Utc);
            DateTime end = start.AddMonths(1);
            // 시간대 차이를 하루 여유로 보되, MinValue/MaxValue 경계에서 넘치지 않게 한다.
            DateTime from = query.FromUtc > DateTime.MinValue.AddDays(2) ? query.FromUtc.AddDays(-1) : DateTime.MinValue;
            DateTime to = query.ToUtc < DateTime.MaxValue.AddDays(-2) ? query.ToUtc.AddDays(1) : DateTime.MaxValue;
            return end > from && start <= to;
        }

        private static string SafeName(string user)
        {
            string name = string.IsNullOrWhiteSpace(user) ? "unknown" : user.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }
    }

    internal static class InspectionHistory
    {
        public static void Record(string userName, string method, string line, IEnumerable<string> targets)
        {
            var clean = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targets != null)
            {
                foreach (string target in targets)
                {
                    if (string.IsNullOrWhiteSpace(target))
                        continue;
                    string trimmed = target.Trim();
                    if (seen.Add(trimmed))
                        clean.Add(trimmed);
                }
            }

            var entry = new HistoryEntry
            {
                At = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                UserName = userName ?? "",
                Method = method ?? "",
                Line = line ?? "",
                Targets = clean,
                Machine = Environment.MachineName
            };

            try
            {
                WebLocalStore.Append(entry);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Web] 개인 검수 기록을 쓰지 못했습니다.");
            }

            try
            {
                SharedHistoryStore.Append(entry);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Web] 공용 검수 기록을 쓰지 못했습니다: {Path}", SharedHistoryStore.FolderPath);
            }
        }

        public static HistoryResult Query(HistoryQuery query, string currentUser)
        {
            if (query == null)
                query = new HistoryQuery();

            var result = new HistoryResult();
            string error;
            result.SharedAvailable = SharedHistoryStore.IsAvailable(out error);
            result.SharedPath = SharedHistoryStore.FolderPath;
            result.SharedError = error ?? "";

            var entries = new List<HistoryEntry>();
            entries.AddRange(WebLocalStore.LoadAll());
            if (result.SharedAvailable)
                entries.AddRange(SharedHistoryStore.ReadAll(query));

            var all = new List<HistoryRow>();
            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HistoryEntry entry in entries)
            {
                DateTime at = SharedHistoryStore.ParseUtc(entry.At);
                if (at == DateTime.MinValue)
                    continue;
                string user = string.IsNullOrWhiteSpace(entry.UserName) ? (currentUser ?? "") : entry.UserName;
                var targets = entry.Targets == null || entry.Targets.Count == 0
                    ? new List<string> { "" }
                    : entry.Targets;

                string key = entry.At + "|" + user + "|" + entry.Method + "|" + entry.Line + "|" + string.Join(",", targets);
                if (!dedupe.Add(key))
                    continue;

                foreach (string target in targets)
                {
                    all.Add(new HistoryRow
                    {
                        At = entry.At,
                        AtUtc = at,
                        UserName = user,
                        Method = entry.Method ?? "",
                        Line = entry.Line ?? "",
                        Target = target ?? ""
                    });
                }
            }

            result.Users = all.Select(r => r.UserName).Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            result.Methods = all.Select(r => r.Method).Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            result.Lines = all.Select(r => r.Line).Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

            IEnumerable<HistoryRow> filtered = all.Where(r => r.AtUtc >= query.FromUtc && r.AtUtc <= query.ToUtc);
            if (!string.IsNullOrWhiteSpace(query.User))
                filtered = filtered.Where(r => string.Equals(r.UserName, query.User, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.Method))
                filtered = filtered.Where(r => string.Equals(r.Method, query.Method, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.Line))
                filtered = filtered.Where(r => string.Equals(r.Line, query.Line, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.Text))
            {
                string needle = query.Text.Trim();
                filtered = filtered.Where(r => r.Target.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            List<HistoryRow> ordered = filtered.OrderByDescending(r => r.AtUtc).ToList();
            if (ordered.Count > SharedHistoryStore.MaxRows)
            {
                result.Truncated = true;
                ordered = ordered.Take(SharedHistoryStore.MaxRows).ToList();
            }
            result.Rows = ordered;
            return result;
        }
    }
}
