using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JLic
{
    public static class LicenseJson
    {
        public static string Write(LicenseDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var sb = new StringBuilder();
            sb.Append('{');
            WriteProp(sb, "format", document.Format, true);
            WriteProp(sb, "issuedAt", document.IssuedAt ?? "", false);
            WriteProp(sb, "issuer", document.Issuer ?? "JLicManager", false);
            sb.Append(",\"users\":[");
            var users = document.Users ?? new LicenseUserEntry[0];
            for (int i = 0; i < users.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                WriteUser(sb, users[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static LicenseDocument Read(string json)
        {
            var root = JsonMap.ParseObject(json);
            var doc = new LicenseDocument
            {
                Format = root.GetInt("format", 1),
                IssuedAt = root.GetString("issuedAt"),
                Issuer = root.GetString("issuer")
            };

            var users = new List<LicenseUserEntry>();
            foreach (var item in root.GetArray("users"))
            {
                var map = item as JsonMap;
                if (map == null)
                    continue;
                users.Add(new LicenseUserEntry
                {
                    MacHash = (map.GetString("macHash") ?? "").ToLowerInvariant(),
                    UserName = map.GetString("userName") ?? "",
                    Group = map.GetString("group") ?? "",
                    Apps = map.GetStringArray("apps"),
                    ExpireDate = map.GetString("expireDate")
                });
            }
            doc.Users = users.ToArray();
            return doc;
        }

        public static DateTime? ParseExpire(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            DateTime parsed;
            if (DateTime.TryParseExact(value.Trim(), new[] { "yyyyMMdd", "yyyy-MM-dd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;
            return null;
        }

        private static void WriteUser(StringBuilder sb, LicenseUserEntry user)
        {
            sb.Append('{');
            WriteProp(sb, "macHash", user.MacHash ?? "", true);
            WriteProp(sb, "userName", user.UserName ?? "", false);
            WriteProp(sb, "group", user.Group ?? "", false);
            sb.Append(",\"apps\":[");
            var apps = user.Apps ?? new string[0];
            for (int i = 0; i < apps.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append('"').Append(Escape(apps[i] ?? "")).Append('"');
            }
            sb.Append(']');
            if (!string.IsNullOrWhiteSpace(user.ExpireDate))
                WriteProp(sb, "expireDate", user.ExpireDate, false);
            else
                sb.Append(",\"expireDate\":null");
            sb.Append('}');
        }

        private static void WriteProp(StringBuilder sb, string name, int value, bool first)
        {
            if (!first)
                sb.Append(',');
            sb.Append('"').Append(name).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteProp(StringBuilder sb, string name, string value, bool first)
        {
            if (!first)
                sb.Append(',');
            sb.Append('"').Append(name).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"')
                    sb.Append("\\\"");
                else if (c == '\\')
                    sb.Append("\\\\");
                else if (c == '\n')
                    sb.Append("\\n");
                else if (c == '\r')
                    sb.Append("\\r");
                else if (c == '\t')
                    sb.Append("\\t");
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }

    internal sealed class JsonMap
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        public static JsonMap ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("라이선스 JSON이 비어 있습니다.");
            int i = 0;
            object value = ParseValue(json, ref i);
            JsonMap map = value as JsonMap;
            if (map == null)
                throw new FormatException("라이선스 JSON 루트가 객체가 아닙니다.");
            return map;
        }

        public int GetInt(string key, int fallback)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                return fallback;
            if (value is int)
                return (int)value;
            int parsed;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return fallback;
        }

        public string GetString(string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public IEnumerable<object> GetArray(string key)
        {
            object value;
            if (!values.TryGetValue(key, out value))
                return new object[0];
            var list = value as List<object>;
            return list ?? new List<object>();
        }

        public string[] GetStringArray(string key)
        {
            var items = new List<string>();
            foreach (var item in GetArray(key))
            {
                if (item != null)
                    items.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
            }
            return items.ToArray();
        }

        private static object ParseValue(string json, ref int i)
        {
            SkipWs(json, ref i);
            if (i >= json.Length)
                throw new FormatException("JSON이 중간에 끝났습니다.");
            char c = json[i];
            if (c == '{')
                return ParseObject(json, ref i);
            if (c == '[')
                return ParseArray(json, ref i);
            if (c == '"')
                return ParseString(json, ref i);
            if (c == 'n' && Match(json, ref i, "null"))
                return null;
            if (c == 't' && Match(json, ref i, "true"))
                return true;
            if (c == 'f' && Match(json, ref i, "false"))
                return false;
            return ParseNumber(json, ref i);
        }

        private static JsonMap ParseObject(string json, ref int i)
        {
            i++;
            var map = new JsonMap();
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == '}')
            {
                i++;
                return map;
            }
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                string key = ParseString(json, ref i);
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':')
                    throw new FormatException("JSON 객체에 ':'가 없습니다.");
                i++;
                map.values[key] = ParseValue(json, ref i);
                SkipWs(json, ref i);
                if (i >= json.Length)
                    break;
                if (json[i] == ',')
                {
                    i++;
                    continue;
                }
                if (json[i] == '}')
                {
                    i++;
                    return map;
                }
                throw new FormatException("JSON 객체 형식이 올바르지 않습니다.");
            }
            throw new FormatException("JSON 객체가 닫히지 않았습니다.");
        }

        private static List<object> ParseArray(string json, ref int i)
        {
            i++;
            var list = new List<object>();
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == ']')
            {
                i++;
                return list;
            }
            while (i < json.Length)
            {
                list.Add(ParseValue(json, ref i));
                SkipWs(json, ref i);
                if (i >= json.Length)
                    break;
                if (json[i] == ',')
                {
                    i++;
                    continue;
                }
                if (json[i] == ']')
                {
                    i++;
                    return list;
                }
                throw new FormatException("JSON 배열 형식이 올바르지 않습니다.");
            }
            throw new FormatException("JSON 배열이 닫히지 않았습니다.");
        }

        private static string ParseString(string json, ref int i)
        {
            if (json[i] != '"')
                throw new FormatException("JSON 문자열이 '\"'로 시작하지 않습니다.");
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= json.Length)
                    break;
                char esc = json[i++];
                if (esc == '"' || esc == '\\' || esc == '/')
                    sb.Append(esc);
                else if (esc == 'n')
                    sb.Append('\n');
                else if (esc == 'r')
                    sb.Append('\r');
                else if (esc == 't')
                    sb.Append('\t');
                else if (esc == 'u' && i + 4 <= json.Length)
                {
                    sb.Append((char)int.Parse(json.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                }
                else
                    sb.Append(esc);
            }
            throw new FormatException("JSON 문자열이 닫히지 않았습니다.");
        }

        private static object ParseNumber(string json, ref int i)
        {
            int start = i;
            if (json[i] == '-')
                i++;
            while (i < json.Length && char.IsDigit(json[i]))
                i++;
            string raw = json.Substring(start, i - start);
            int value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new FormatException("JSON 숫자를 읽지 못했습니다.");
            return value;
        }

        private static void SkipWs(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
        }

        private static bool Match(string json, ref int i, string token)
        {
            if (i + token.Length > json.Length || json.Substring(i, token.Length) != token)
                return false;
            i += token.Length;
            return true;
        }
    }
}
