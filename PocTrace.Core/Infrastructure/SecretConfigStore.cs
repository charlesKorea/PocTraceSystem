using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Xml;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// DB 계정은 exe.config가 아니라 별도 폴더의 secrets.config에 둔다.
    /// 기본 폴더는 C:\ProgramData\PocTrace\config 이고, 만들 때 숨김이다.
    /// </summary>
    public static class SecretConfigStore
    {
        public const string FileName = "secrets.config";

        private static readonly object Gate = new object();
        private static bool _loaded;
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
            ConnectionStringSettings setting = null;
            try
            {
                setting = ConfigurationManager.ConnectionStrings[name];
            }
            catch
            {
                setting = null;
            }
            return setting == null ? null : setting.ConnectionString;
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
                    Notice = "비밀 설정을 읽지 못했습니다. " + ex.Message;
                }
                _loaded = true;
            }
        }

        private static void Load()
        {
            DirectoryPath = ResolveDirectory();
            FilePath = Path.Combine(DirectoryPath, FileName);
            Directory.CreateDirectory(DirectoryPath);
            try
            {
                var dir = new DirectoryInfo(DirectoryPath);
                if ((dir.Attributes & FileAttributes.Hidden) == 0)
                    dir.Attributes |= FileAttributes.Hidden;
            }
            catch
            {
            }

            if (!File.Exists(FilePath))
                ImportFromExeConfig();
            if (File.Exists(FilePath))
                Read(FilePath);
            if (string.IsNullOrEmpty(Notice))
                Notice = "DB 계정 파일: " + FilePath;
            TryWritePointer();
        }

        private static string ResolveDirectory()
        {
            string configured = null;
            try
            {
                configured = ConfigurationManager.AppSettings["SecretConfigDirectory"];
            }
            catch
            {
                configured = null;
            }
            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim();
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PocTrace",
                "config");
        }

        private static void ImportFromExeConfig()
        {
            var connections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int copied = 0;
            try
            {
                foreach (ConnectionStringSettings item in ConfigurationManager.ConnectionStrings)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Name))
                        continue;
                    if (!HasRealPassword(item.ConnectionString))
                        continue;
                    connections[item.Name] = item.ConnectionString.Trim();
                    copied++;
                }
                string frm = ConfigurationManager.AppSettings["FrmConnectionString"];
                if (HasRealPassword(frm))
                {
                    settings["FrmConnectionString"] = frm.Trim();
                    copied++;
                }
            }
            catch (Exception ex)
            {
                Notice = "exe.config 연결 문자열을 읽지 못했습니다. " + ex.Message;
            }

            WriteFile(connections, settings);
            Notice = copied > 0
                ? "DB 계정을 " + FilePath + " 에 복사했습니다. 확인 후 exe.config의 비밀번호는 지우세요."
                : "DB 계정 파일: " + FilePath;
        }

        private static void WriteFile(Dictionary<string, string> connections, Dictionary<string, string> settings)
        {
            var doc = new XmlDocument();
            var decl = doc.CreateXmlDeclaration("1.0", "utf-8", null);
            doc.AppendChild(decl);
            var root = doc.CreateElement("secrets");
            doc.AppendChild(root);
            var comment = doc.CreateComment(" DB 계정만 둔다. exe.config에 비밀번호를 넣지 않는다. ");
            root.AppendChild(comment);
            var connectionNode = doc.CreateElement("connectionStrings");
            root.AppendChild(connectionNode);
            AppendKnown(doc, connectionNode, connections, "SourceDb");
            AppendKnown(doc, connectionNode, connections, "PocTraceDB");
            AppendKnown(doc, connectionNode, connections, "LocalDb");
            foreach (var pair in connections)
            {
                if (pair.Key.Equals("SourceDb", StringComparison.OrdinalIgnoreCase)
                    || pair.Key.Equals("PocTraceDB", StringComparison.OrdinalIgnoreCase)
                    || pair.Key.Equals("LocalDb", StringComparison.OrdinalIgnoreCase))
                    continue;
                AppendAdd(doc, connectionNode, "name", pair.Key, "connectionString", pair.Value);
            }
            var settingNode = doc.CreateElement("appSettings");
            root.AppendChild(settingNode);
            string frm;
            settings.TryGetValue("FrmConnectionString", out frm);
            AppendAdd(doc, settingNode, "key", "FrmConnectionString", "value", frm ?? "");

            var settingsXml = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (var writer = XmlWriter.Create(FilePath, settingsXml))
                doc.Save(writer);
            try
            {
                File.SetAttributes(FilePath, File.GetAttributes(FilePath) | FileAttributes.Hidden);
            }
            catch
            {
            }
        }

        private static void AppendKnown(XmlDocument doc, XmlElement parent, Dictionary<string, string> values, string name)
        {
            string value;
            values.TryGetValue(name, out value);
            AppendAdd(doc, parent, "name", name, "connectionString", value ?? "");
        }

        private static void AppendAdd(XmlDocument doc, XmlElement parent, string keyAttr, string key, string valueAttr, string value)
        {
            var add = doc.CreateElement("add");
            add.SetAttribute(keyAttr, key);
            add.SetAttribute(valueAttr, value ?? "");
            parent.AppendChild(add);
        }

        private static void Read(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            _connections = ReadAdds(doc, "/secrets/connectionStrings/add", "name", "connectionString");
            _settings = ReadAdds(doc, "/secrets/appSettings/add", "key", "value");
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

        private static void TryWritePointer()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secret-config.path.txt");
                File.WriteAllText(path, FilePath ?? "", new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
