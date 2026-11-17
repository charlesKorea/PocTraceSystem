using System;
using System.IO;

namespace PocTrace.Admin
{
    /// <summary>
    /// Admin 로그는 exe 실행 폴더의 log 에 둔다. LocalAppData 에 쓰지 않는다.
    /// 시작 사용자 기록(Admin.log, Admin-startup.log, log\open)은 Admin을 혼자 실행하므로 남기지 않고 지운다.
    /// </summary>
    internal static class AdminLog
    {
        public static string DirectoryPath()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void DeleteStartupRecords()
        {
            string dir = DirectoryPath();
            TryDelete(Path.Combine(dir, "Admin-startup.log"));
            TryDelete(Path.Combine(dir, "Admin.log"));
            string open = Path.Combine(dir, "open");
            if (!Directory.Exists(open))
                return;
            string[] files;
            try
            {
                files = Directory.GetFiles(open, "Admin-*");
            }
            catch
            {
                return;
            }
            for (int i = 0; i < files.Length; i++)
                TryDelete(files[i]);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
