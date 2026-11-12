using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace PocTrace.DdwTraceBench.Data
{
    /// <summary>
    /// PocTrace.UI(Viewer) 실행 폴더의 exe.config 와 config\db.secret 를 읽는다.
    /// 암호 파일 형식은 PocTrace.Core.Infrastructure.SecretConfigStore 와 같다.
    /// 읽기만 한다. exe.config 의 비밀번호를 지우거나 암호 파일을 만들지 않는다.
    /// </summary>
    public sealed class UiSqlConfig
    {
        private static readonly string[] PreferredConfigs =
        {
            "PocTrace.Viewer.exe.config",
            "PocTrace.Admin.exe.config",
            "PocTrace.DeletedPipeLength.exe.config",
            "PocTrace.Demolition.exe.config",
            "PocTrace.NozzleTrace.exe.config",
            "PocTrace.Web.exe.config"
        };

        private static readonly string[] PreferredNames = { "SourceDb", "FrmConnectionString", "PocTraceDB" };

        public string Folder;
        public string ConfigFile;
        public string SecretFile;
        public string Notice;
        public Dictionary<string, string> Connections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string GuessFolder()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 4 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "PocTrace.Viewer.exe.config"))
                    || File.Exists(Path.Combine(dir, "PocTrace.Viewer.exe")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                dir = parent == null ? null : parent.FullName;
            }
            return string.Empty;
        }

        public static UiSqlConfig Load(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new InvalidOperationException("PocTrace.UI 실행 폴더를 지정하세요. PocTrace.Viewer.exe.config 가 있는 폴더입니다.");
            string full = Path.GetFullPath(folder.Trim());
            if (!Directory.Exists(full))
                throw new InvalidOperationException("폴더가 없습니다: " + full);

            var result = new UiSqlConfig { Folder = full };
            result.ConfigFile = FindConfig(full);
            if (result.ConfigFile == null)
                throw new InvalidOperationException("exe.config 가 없습니다. PocTrace.Viewer.exe.config 가 있는 폴더를 지정하세요: " + full);

            ReadExeConfig(result.ConfigFile, result.Connections);

            string secret = Path.Combine(full, "config", "db.secret");
            if (File.Exists(secret))
            {
                result.SecretFile = secret;
                OverlaySecret(secret, result.Connections);
                result.Notice = "연결은 " + Path.GetFileName(result.ConfigFile) + " 와 config\\db.secret 에서 읽었다.";
            }
            else
            {
                result.Notice = "config\\db.secret 이 없다. " + Path.GetFileName(result.ConfigFile) + " 의 연결 문자열만 쓴다. 비밀번호는 그 파일이나 db.secret 에 있어야 한다.";
            }

            if (result.Connections.Count == 0)
                throw new InvalidOperationException("연결 문자열이 없습니다. SourceDb 또는 FrmConnectionString 을 확인하세요. " + result.ConfigFile);
            return result;
        }

        public static string PreferredName(IDictionary<string, string> connections)
        {
            foreach (string name in PreferredNames)
            {
                string value;
                if (connections.TryGetValue(name, out value) && !string.IsNullOrWhiteSpace(value))
                    return name;
            }
            foreach (var pair in connections)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    return pair.Key;
            }
            return null;
        }

        public static string Describe(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return "(비어 있음)";
            SqlConnectionStringBuilder builder;
            try
            {
                builder = new SqlConnectionStringBuilder(connectionString);
            }
            catch (Exception ex)
            {
                return "연결 문자열을 해석하지 못했다: " + ex.Message;
            }
            string catalog = string.IsNullOrEmpty(builder.InitialCatalog) ? "(서버 기본 DB)" : builder.InitialCatalog;
            string auth = builder.IntegratedSecurity ? "Windows 인증" : "SQL 로그인 " + builder.UserID;
            return builder.DataSource + " / " + catalog + " / " + auth;
        }

        private static string FindConfig(string folder)
        {
            foreach (string name in PreferredConfigs)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path))
                    return path;
            }
            string[] any = Directory.GetFiles(folder, "*.exe.config");
            return any.Length == 0 ? null : any[0];
        }

        private static void ReadExeConfig(string path, Dictionary<string, string> connections)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            XmlNodeList nodes = doc.SelectNodes("/configuration/connectionStrings/add");
            if (nodes != null)
            {
                foreach (XmlElement add in nodes)
                {
                    string name = add.GetAttribute("name");
                    string value = add.GetAttribute("connectionString");
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                        connections[name.Trim()] = value.Trim();
                }
            }
            XmlNodeList settings = doc.SelectNodes("/configuration/appSettings/add");
            if (settings == null)
                return;
            foreach (XmlElement add in settings)
            {
                if (!string.Equals(add.GetAttribute("key"), "FrmConnectionString", StringComparison.OrdinalIgnoreCase))
                    continue;
                string value = add.GetAttribute("value");
                if (!string.IsNullOrWhiteSpace(value))
                    connections["FrmConnectionString"] = value.Trim();
            }
        }

        private static void OverlaySecret(string path, Dictionary<string, string> connections)
        {
            byte[] plain = SharedDbSecret.Decrypt(File.ReadAllBytes(path));
            var doc = new XmlDocument();
            using (var stream = new MemoryStream(plain))
                doc.Load(stream);
            ApplyAdds(doc, "/secrets/connectionStrings/add", "name", "connectionString", connections);
            ApplyAdds(doc, "/secrets/appSettings/add", "key", "value", connections);
        }

        private static void ApplyAdds(XmlDocument doc, string xpath, string keyAttr, string valueAttr, Dictionary<string, string> connections)
        {
            XmlNodeList nodes = doc.SelectNodes(xpath);
            if (nodes == null)
                return;
            foreach (XmlElement add in nodes)
            {
                string key = add.GetAttribute(keyAttr);
                string value = add.GetAttribute(valueAttr);
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;
                if (string.Equals(key, "FrmConnectionString", StringComparison.OrdinalIgnoreCase)
                    || xpath.IndexOf("connectionStrings", StringComparison.OrdinalIgnoreCase) >= 0)
                    connections[key.Trim()] = value.Trim();
            }
        }
    }

    /// <summary>
    /// PocTrace.Core 의 DbSecretCipher 와 같은 형식. 키 유도 문구를 바꾸면 기존 db.secret 을 읽지 못한다.
    /// </summary>
    internal static class SharedDbSecret
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
