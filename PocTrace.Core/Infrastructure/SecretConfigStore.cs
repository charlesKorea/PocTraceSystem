using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// DB 비밀번호만 공유 실행 폴더의 config\db.secret 에 암호화해서 둔다.
    /// 다른 설정은 exe.config 평문이다. 개인 디스크에는 쓰지 않는다.
    /// </summary>
    public static class SecretConfigStore
    {
        public const string FileName = "db.secret";

        private static readonly object Gate = new object();
        private static bool _loaded;
        private static string _directoryNote;
        private static Dictionary<string, string> _connections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string DirectoryPath { get; private set; }
        public static string FilePath { get; private set; }
        public static string Notice { get; private set; }

        public static string ConnectionString(string name)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(name))
                return null;
            string external;
            if (_connections.TryGetValue(name, out external) && !string.IsNullOrWhiteSpace(external))
                return external.Trim();
            return ExeConnectionString(name);
        }

        public static string AppSetting(string key)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key))
                return null;
            string external;
            if (_settings.TryGetValue(key, out external) && !string.IsNullOrWhiteSpace(external))
                return external.Trim();
            try
            {
                return ConfigurationManager.AppSettings[key];
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            lock (Gate)
            {
                if (_loaded)
                    return;
                try
                {
                    Load();
                }
                catch (Exception ex)
                {
                    Notice = "DB 암호 파일을 읽지 못했습니다. " + ex.Message;
                }
                _loaded = true;
            }
        }

        private static void Load()
        {
            DirectoryPath = ResolveDirectory();
            FilePath = Path.Combine(DirectoryPath, FileName);
            Directory.CreateDirectory(DirectoryPath);

            if (File.Exists(FilePath))
            {
                ReadEncrypted(FilePath);
                AbsorbPlainPasswords();
                Notice = Note("DB 암호 파일: " + FilePath);
                return;
            }

            var connections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CollectFromExeConfig(connections, settings);
            CollectFromLegacyProgramData(connections, settings);
            if (connections.Count == 0 && settings.Count == 0)
            {
                Notice = Note("DB 암호 파일이 없습니다. exe.config에 비밀번호를 한 번 넣고 실행하면 " + FilePath + " 로 옮깁니다.");
                return;
            }

            WriteEncrypted(FilePath, connections, settings);
            _connections = connections;
            _settings = settings;
            bool scrubbed = ScrubExeConfig(connections, settings);
            TryDeleteLegacyProgramData();
            Notice = Note(scrubbed
                ? "DB 비밀번호를 " + FilePath + " 에 암호화했습니다. exe.config에서는 지웠습니다."
                : "DB 비밀번호를 " + FilePath + " 에 암호화했습니다. exe.config가 잠겨 있으면 비밀번호 줄을 직접 지우세요.");
        }

        private static void AbsorbPlainPasswords()
        {
            var fromExeConnections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fromExeSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CollectFromExeConfig(fromExeConnections, fromExeSettings);
            bool changed = ReplaceWith(fromExeConnections, _connections);
            changed = ReplaceWith(fromExeSettings, _settings) || changed;

            var legacyConnections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var legacySettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CollectFromLegacyProgramData(legacyConnections, legacySettings);
            changed = FillMissing(legacyConnections, _connections) || changed;
            changed = FillMissing(legacySettings, _settings) || changed;
            if (changed)
                WriteEncrypted(FilePath, _connections, _settings);
            ScrubExeConfig(_connections, _settings);
            TryDeleteLegacyProgramData();
        }

        private static bool ReplaceWith(Dictionary<string, string> source, Dictionary<string, string> target)
        {
            bool changed = false;
            foreach (var pair in source)
            {
                string existing;
                if (target.TryGetValue(pair.Key, out existing) && string.Equals(existing, pair.Value, StringComparison.Ordinal))
                    continue;
                target[pair.Key] = pair.Value;
                changed = true;
            }
            return changed;
        }

        private static bool FillMissing(Dictionary<string, string> source, Dictionary<string, string> target)
        {
            bool changed = false;
            foreach (var pair in source)
            {
                if (target.ContainsKey(pair.Key))
                    continue;
                target[pair.Key] = pair.Value;
                changed = true;
            }
            return changed;
        }

        private static string ResolveDirectory()
        {
            string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
            string configured = null;
            try
            {
                configured = ConfigurationManager.AppSettings["SecretConfigDirectory"];
            }
            catch
            {
                configured = null;
            }
            if (string.IsNullOrWhiteSpace(configured))
                return fallback;
            try
            {
                string trimmed = configured.Trim();
                string full = Path.IsPathRooted(trimmed)
                    ? Path.GetFullPath(trimmed)
                    : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trimmed));
                if (!IsUnderBase(full))
                {
                    _directoryNote = "SecretConfigDirectory가 실행 폴더 밖이라 무시했습니다.";
                    return fallback;
                }
                return full;
            }
            catch
            {
                _directoryNote = "SecretConfigDirectory를 읽지 못해 실행 폴더의 config를 씁니다.";
                return fallback;
            }
        }

        private static bool IsUnderBase(string full)
        {
            string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory ?? ".");
            string left = AppendSeparator(baseDir);
            string right = AppendSeparator(full);
            return right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Path.DirectorySeparatorChar.ToString();
            char sep = Path.DirectorySeparatorChar;
            if (path[path.Length - 1] == sep || path[path.Length - 1] == Path.AltDirectorySeparatorChar)
                return path;
            return path + sep;
        }

        private static string Note(string notice)
        {
            if (string.IsNullOrEmpty(_directoryNote))
                return notice;
            return _directoryNote + " " + notice;
        }

        private static void CollectFromExeConfig(Dictionary<string, string> connections, Dictionary<string, string> settings)
        {
            try
            {
                foreach (ConnectionStringSettings item in ConfigurationManager.ConnectionStrings)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Name) || !HasRealPassword(item.ConnectionString))
                        continue;
                    connections[item.Name] = item.ConnectionString.Trim();
                }
                string frm = ConfigurationManager.AppSettings["FrmConnectionString"];
                if (HasRealPassword(frm))
                    settings["FrmConnectionString"] = frm.Trim();
            }
            catch (Exception ex)
            {
                Notice = "exe.config 연결 문자열을 읽지 못했습니다. " + ex.Message;
            }
        }

        private static void CollectFromLegacyProgramData(Dictionary<string, string> connections, Dictionary<string, string> settings)
        {
            try
            {
                string legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "PocTrace",
                    "config",
                    "secrets.config");
                if (!File.Exists(legacy))
                    return;
                var doc = new XmlDocument();
                doc.Load(legacy);
                Merge(connections, ReadAdds(doc, "/secrets/connectionStrings/add", "name", "connectionString"));
                Merge(settings, ReadAdds(doc, "/secrets/appSettings/add", "key", "value"));
            }
            catch
            {
            }
        }

        private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || !HasRealPassword(pair.Value))
                    continue;
                if (!target.ContainsKey(pair.Key))
                    target[pair.Key] = pair.Value.Trim();
            }
        }

        private static void WriteEncrypted(string path, Dictionary<string, string> connections, Dictionary<string, string> settings)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("secrets");
            doc.AppendChild(root);
            var connectionNode = doc.CreateElement("connectionStrings");
            root.AppendChild(connectionNode);
            foreach (var pair in connections)
                AppendAdd(doc, connectionNode, "name", pair.Key, "connectionString", pair.Value);
            var settingNode = doc.CreateElement("appSettings");
            root.AppendChild(settingNode);
            foreach (var pair in settings)
                AppendAdd(doc, settingNode, "key", pair.Key, "value", pair.Value);
            using (var stream = new MemoryStream())
            {
                doc.Save(stream);
                File.WriteAllBytes(path, DbSecretCipher.Encrypt(stream.ToArray()));
            }
        }

        private static void ReadEncrypted(string path)
        {
            byte[] plain = DbSecretCipher.Decrypt(File.ReadAllBytes(path));
            var doc = new XmlDocument();
            using (var stream = new MemoryStream(plain))
                doc.Load(stream);
            _connections = ReadAdds(doc, "/secrets/connectionStrings/add", "name", "connectionString");
            _settings = ReadAdds(doc, "/secrets/appSettings/add", "key", "value");
        }

        private static void AppendAdd(XmlDocument doc, XmlElement parent, string keyAttr, string key, string valueAttr, string value)
        {
            var add = doc.CreateElement("add");
            add.SetAttribute(keyAttr, key);
            add.SetAttribute(valueAttr, value ?? "");
            parent.AppendChild(add);
        }

        private static Dictionary<string, string> ReadAdds(XmlDocument doc, string xpath, string keyAttr, string valueAttr)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var nodes = doc.SelectNodes(xpath);
            if (nodes == null)
                return map;
            foreach (XmlElement add in nodes)
            {
                string key = add.GetAttribute(keyAttr);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                map[key.Trim()] = add.GetAttribute(valueAttr) ?? "";
            }
            return map;
        }

        private static bool ScrubExeConfig(Dictionary<string, string> connections, Dictionary<string, string> settings)
        {
            try
            {
                string path = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;
                var doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.Load(path);
                bool changed = false;
                var nodes = doc.SelectNodes("/configuration/connectionStrings/add");
                if (nodes != null)
                {
                    foreach (XmlElement add in nodes)
                    {
                        string name = add.GetAttribute("name");
                        if (!connections.ContainsKey(name) || !HasRealPassword(add.GetAttribute("connectionString")))
                            continue;
                        add.SetAttribute("connectionString", "");
                        changed = true;
                    }
                }
                if (settings.ContainsKey("FrmConnectionString"))
                {
                    var appSettings = doc.SelectNodes("/configuration/appSettings/add");
                    if (appSettings != null)
                    {
                        foreach (XmlElement add in appSettings)
                        {
                            if (!string.Equals(add.GetAttribute("key"), "FrmConnectionString", StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (!HasRealPassword(add.GetAttribute("value")))
                                continue;
                            add.SetAttribute("value", "");
                            changed = true;
                        }
                    }
                }
                if (changed)
                    doc.Save(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteLegacyProgramData()
        {
            try
            {
                string legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "PocTrace",
                    "config",
                    "secrets.config");
                if (File.Exists(legacy))
                    File.Delete(legacy);
            }
            catch
            {
            }
        }

        private static string ExeConnectionString(string name)
        {
            try
            {
                ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings[name];
                return setting == null ? null : setting.ConnectionString;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasRealPassword(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (value.IndexOf("YOUR_PW", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("YOUR_ID", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("YOUR_SERVER", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return value.IndexOf("Password=", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Pwd=", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal static class DbSecretCipher
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PTSEC1");

        public static byte[] Encrypt(byte[] plain)
        {
            byte[] aesKey = Derive("aes");
            byte[] macKey = Derive("mac");
            byte[] iv = new byte[16];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(iv);
            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var transform = aes.CreateEncryptor())
                    cipher = transform.TransformFinalBlock(plain, 0, plain.Length);
            }
            byte[] mac = Sign(macKey, iv, cipher);
            var output = new byte[Magic.Length + iv.Length + mac.Length + cipher.Length];
            Buffer.BlockCopy(Magic, 0, output, 0, Magic.Length);
            Buffer.BlockCopy(iv, 0, output, Magic.Length, iv.Length);
            Buffer.BlockCopy(mac, 0, output, Magic.Length + iv.Length, mac.Length);
            Buffer.BlockCopy(cipher, 0, output, Magic.Length + iv.Length + mac.Length, cipher.Length);
            return output;
        }

        public static byte[] Decrypt(byte[] data)
        {
            int offset = Magic.Length + 16 + 32;
            if (data == null || data.Length <= offset || !StartsWithMagic(data))
                throw new InvalidDataException("db.secret 형식이 아닙니다.");
            var iv = new byte[16];
            var mac = new byte[32];
            var cipher = new byte[data.Length - offset];
            Buffer.BlockCopy(data, Magic.Length, iv, 0, 16);
            Buffer.BlockCopy(data, Magic.Length + 16, mac, 0, 32);
            Buffer.BlockCopy(data, offset, cipher, 0, cipher.Length);
            byte[] expected = Sign(Derive("mac"), iv, cipher);
            if (!FixedEquals(mac, expected))
                throw new InvalidDataException("db.secret 검증에 실패했습니다.");
            using (var aes = Aes.Create())
            {
                aes.Key = Derive("aes");
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var transform = aes.CreateDecryptor())
                    return transform.TransformFinalBlock(cipher, 0, cipher.Length);
            }
        }

        private static byte[] Sign(byte[] macKey, byte[] iv, byte[] cipher)
        {
            using (var hmac = new HMACSHA256(macKey))
            {
                hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                hmac.TransformFinalBlock(cipher, 0, cipher.Length);
                return hmac.Hash;
            }
        }

        private static byte[] Derive(string label)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("PocTrace.SharedDbSecret.v1")))
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(label));
        }

        private static bool StartsWithMagic(byte[] data)
        {
            if (data.Length < Magic.Length)
                return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    return false;
            }
            return true;
        }

        private static bool FixedEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
