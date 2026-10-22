using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace JLic
{
    public static class LicensePaths
    {
        public const string DefaultFileName = "jlab.lic";
        public const string ClientKeyFileName = "JLic.client.keys";

        public static IEnumerable<string> LicenseCandidates(string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                yield return explicitPath;
                yield break;
            }

            foreach (var root in ProbeRoots())
            {
                yield return Path.Combine(root, "Lic", DefaultFileName);
                yield return Path.Combine(root, DefaultFileName);
                yield return Path.Combine(root, "output", DefaultFileName);
            }
        }

        public static IEnumerable<string> ClientKeyCandidates()
        {
            foreach (var root in ProbeRoots())
            {
                yield return Path.Combine(root, ClientKeyFileName);
                yield return Path.Combine(root, "Keys", ClientKeyFileName);
                yield return Path.Combine(root, "Embedded", "client.keys");
            }
        }

        public static string FindLicenseFile(string explicitPath)
        {
            foreach (var path in LicenseCandidates(explicitPath))
            {
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        private static IEnumerable<string> ProbeRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                SafeDir(Assembly.GetEntryAssembly()),
                SafeDir(typeof(LicensePaths).Assembly),
                Environment.CurrentDirectory
            })
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                string full = Path.GetFullPath(path);
                if (seen.Add(full))
                    yield return full;
            }
        }

        private static string SafeDir(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
                return null;
            try
            {
                string location = assembly.Location;
                return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }
    }
}
