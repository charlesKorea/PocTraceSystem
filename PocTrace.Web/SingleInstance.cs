using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;

namespace PocTrace.Web
{
    /// <summary>
    /// 같은 Windows 세션(사용자)에서 Web.exe 를 두 번 띄우지 않게 한다.
    /// Local\ 이름 공간이라 SVDI 에서 다른 사용자 세션과는 서로 막지 않는다.
    /// </summary>
    internal sealed class SingleInstance : IDisposable
    {
        private const string MutexName = @"Local\PocTrace.Web.SingleInstance";
        private const uint MbOk = 0x0;
        private const uint MbIconInformation = 0x40;
        private const uint MbTopmost = 0x40000;

        private Mutex _mutex;
        private readonly string _infoPath;

        private SingleInstance(Mutex mutex)
        {
            _mutex = mutex;
            _infoPath = InfoPath();
        }

        public static bool TryAcquire(out SingleInstance instance)
        {
            instance = null;
            bool created;
            Mutex mutex;
            try
            {
                mutex = new Mutex(true, MutexName, out created);
            }
            catch
            {
                return true;
            }

            if (!created)
            {
                mutex.Dispose();
                return false;
            }

            instance = new SingleInstance(mutex);
            return true;
        }

        public void Publish(string url)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_infoPath));
                File.WriteAllText(_infoPath, JsonConvert.SerializeObject(new InstanceInfo
                {
                    Pid = Process.GetCurrentProcess().Id,
                    Url = url,
                    StartedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }));
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try { if (File.Exists(_infoPath)) File.Delete(_infoPath); }
            catch { }
            try
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
            catch { }
            _mutex = null;
        }

        /// <summary>이미 떠 있는 Web.exe 주소. 프로세스가 죽었으면 null.</summary>
        public static string RunningUrl()
        {
            try
            {
                string path = InfoPath();
                if (!File.Exists(path))
                    return null;
                InstanceInfo info = JsonConvert.DeserializeObject<InstanceInfo>(File.ReadAllText(path));
                if (info == null || string.IsNullOrEmpty(info.Url))
                    return null;
                try { Process.GetProcessById(info.Pid); }
                catch (ArgumentException) { return null; }
                return info.Url;
            }
            catch
            {
                return null;
            }
        }

        public static void NotifyDuplicate(string url, bool openBrowser)
        {
            string text =
                "PocTrace.Web 이 이미 실행 중입니다. 새로 띄우지 않고 기존 화면을 엽니다.\r\n" +
                "PocTrace.Web 已在运行。不会重复启动，将打开现有页面。\r\n\r\n" +
                (url == null ? "" : url + "\r\n\r\n") +
                "화면이 보이지 않으면 작업 표시줄의 Web.exe 창을 확인하세요.\r\n" +
                "如果看不到页面，请查看任务栏中的 Web.exe 窗口。";
            Console.Error.WriteLine(text);

            if (openBrowser && url != null)
            {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
                catch { }
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try { MessageBoxW(IntPtr.Zero, text, "PocTrace.Web", MbOk | MbIconInformation | MbTopmost); }
                catch { }
            }
        }

        private static string InfoPath()
        {
            return Path.Combine(PortalService.WorkRoot, "web.instance.json");
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        private sealed class InstanceInfo
        {
            public int Pid { get; set; }
            public string Url { get; set; }
            public string StartedUtc { get; set; }
        }
    }
}
