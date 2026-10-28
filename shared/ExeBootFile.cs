using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PocTrace.Startup
{
    /// <summary>
    /// Serilog나 PocTrace.Core를 열기 전에 exe 폴더 log에 시작 실패를 남긴다.
    /// DLL이 없으면 Main 안의 catch까지 도달하지 못하므로 이 코드는 그 참조를 쓰지 않는다.
    /// </summary>
    public static class ExeBootFile
    {
        public static string Start(string appName)
        {
            try
            {
                string path = PathFor(appName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(
                    path,
                    Line("START") + " base=" + AppDomain.CurrentDomain.BaseDirectory + "\r\n",
                    Encoding.UTF8);
                return path;
            }
            catch
            {
                return null;
            }
        }

        public static void Fail(string path, Exception ex)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    path = PathFor("startup");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var text = new StringBuilder();
                text.Append(Line("FAIL"));
                text.AppendLine("DLL이 exe와 다른 빌드이거나 빠져 있으면 창이 뜨기 전에 죽는다.");
                text.AppendLine("같은 빌드의 PocTrace.Core.dll 만 exe 옆에 둔다. JLic와 Serilog는 exe 안에 있다.");
                text.AppendLine("폴더에 남은 예전 DLL은 exe 안쪽 복사본보다 먼저 열린다.");
                text.AppendLine(ex == null ? "exception=null" : ex.ToString());
                File.AppendAllText(path, text.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string PathFor(string appName)
        {
            string name = string.IsNullOrWhiteSpace(appName) ? "startup" : appName.Trim();
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", name + "-startup.log");
        }

        private static string Line(string stage)
        {
            return DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz")
                + " " + stage
                + " pid=" + Process.GetCurrentProcess().Id;
        }
    }
}
