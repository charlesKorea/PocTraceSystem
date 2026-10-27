using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// SVDI에서 exe를 누가 열어 두었는지 본다. LocalAppData가 아니라 exe 폴더의 log에 남긴다.
    /// </summary>
    public static class ExeUseLog
    {
        public static ExeUseSession Open(string appName)
        {
            return new ExeUseSession(appName);
        }
    }

    public sealed class ExeUseSession : IDisposable
    {
        private readonly string _appName;
        private readonly string _logPath;
        private readonly string _markerPath;
        private readonly string _winUser;
        private readonly string _machine;
        private readonly int _pid;
        private readonly int _sessionId;
        private readonly string _exePath;
        private string _licenseUser = "";
        private int _closed;

        internal ExeUseSession(string appName)
        {
            _appName = string.IsNullOrWhiteSpace(appName) ? "App" : appName.Trim();
            _winUser = WindowsUser();
            _machine = Environment.MachineName ?? "";
            _pid = Process.GetCurrentProcess().Id;
            _sessionId = Process.GetCurrentProcess().SessionId;
            _exePath = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                ProcessModule module = Process.GetCurrentProcess().MainModule;
                if (module != null && !string.IsNullOrEmpty(module.FileName))
                    _exePath = module.FileName;
            }
            catch
            {
            }

            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            Directory.CreateDirectory(root);
            _logPath = Path.Combine(root, Safe(_appName) + ".log");
            string openDir = Path.Combine(root, "open");
            Directory.CreateDirectory(openDir);
            _markerPath = Path.Combine(openDir, Safe(_appName) + "-" + Safe(_machine) + "-p" + _pid + ".txt");
            WriteMarker();
            Append("OPEN");
            AppDomain.CurrentDomain.ProcessExit += delegate { Dispose(); };
        }

        public void SetLicenseUser(string licenseUser)
        {
            _licenseUser = licenseUser ?? "";
            WriteMarker();
            Append("LICENSE");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;
            try
            {
                Append("CLOSE");
            }
            catch
            {
            }
            try
            {
                if (File.Exists(_markerPath))
                    File.Delete(_markerPath);
            }
            catch
            {
            }
        }

        private void WriteMarker()
        {
            var text = new StringBuilder();
            text.Append("app=").AppendLine(_appName);
            text.Append("licUser=").AppendLine(_licenseUser);
            text.Append("winUser=").AppendLine(_winUser);
            text.Append("machine=").AppendLine(_machine);
            text.Append("pid=").AppendLine(_pid.ToString());
            text.Append("session=").AppendLine(_sessionId.ToString());
            text.Append("exe=").AppendLine(_exePath);
            text.Append("started=").AppendLine(Stamp());
            File.WriteAllText(_markerPath, text.ToString(), new UTF8Encoding(false));
        }

        private void Append(string action)
        {
            string line = Stamp()
                + " " + action
                + " app=" + _appName
                + " licUser=" + _licenseUser
                + " winUser=" + _winUser
                + " machine=" + _machine
                + " pid=" + _pid
                + " session=" + _sessionId
                + " exe=" + _exePath;
            AppendLine(_logPath, line);
        }

        private static void AppendLine(string path, string line)
        {
            Mutex mutex = null;
            bool owned = false;
            try
            {
                mutex = new Mutex(false, @"Global\PocTraceExeUseLog");
                try
                {
                    owned = mutex.WaitOne(1500);
                }
                catch (AbandonedMutexException)
                {
                    owned = true;
                }
            }
            catch
            {
                mutex = null;
            }

            try
            {
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine(line);
                }
            }
            finally
            {
                if (mutex != null)
                {
                    try
                    {
                        if (owned)
                            mutex.ReleaseMutex();
                    }
                    catch
                    {
                    }
                    mutex.Dispose();
                }
            }
        }

        private static string WindowsUser()
        {
            string domain = Environment.UserDomainName ?? "";
            string name = Environment.UserName ?? "";
            if (domain.Length == 0)
                return name;
            return domain + "\\" + name;
        }

        private static string Stamp()
        {
            return DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "x";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
