using System;
using System.IO;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// Per-user writable paths. Shared network folders are read-only for clients.
    /// Layout: %LocalAppData%\PocTrace\{app}\
    /// </summary>
    public static class PocTraceLocalPaths
    {
        public const string Viewer = "Viewer";
        public const string NozzleTrace = "NozzleTrace";
        public const string Demolition = "Demolition";
        public const string Web = "Web";
        public const string Admin = "Admin";

        public static string Root =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PocTrace");

        public static string AppDirectory(string appName)
        {
            var dir = Path.Combine(Root, string.IsNullOrWhiteSpace(appName) ? "App" : appName.Trim());
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string LogsDirectory(string appName)
        {
            var dir = Path.Combine(AppDirectory(appName), "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string CombineApp(string appName, params string[] parts)
        {
            var dir = AppDirectory(appName);
            if (parts == null || parts.Length == 0)
                return dir;
            var all = new string[parts.Length + 1];
            all[0] = dir;
            Array.Copy(parts, 0, all, 1, parts.Length);
            return Path.Combine(all);
        }
    }
}
