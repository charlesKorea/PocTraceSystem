using System;
using System.IO;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Licensing;
using PocTrace.Startup;
using Serilog;

namespace PocTrace.DeletedPipeLength
{
    static class Program
    {
        [STAThread]
        static int Main()
        {
            string boot = null;
            try
            {
                boot = ExeBootFile.Start(PocTraceLocalPaths.DeletedPipeLength);
                return RunCore();
            }
            catch (Exception ex)
            {
                ExeBootFile.Fail(boot, ex);
                try
                {
                    MessageBox.Show(
                        "시작에 실패했습니다. 창이 없이 끝난 경우는 DLL이 빠진 경우가 많습니다.\r\n"
                        + "같은 빌드 출력의 PocTrace.Core.dll, JLic.Core.dll, Serilog.dll, Serilog.Sinks.File.dll 을 exe와 같이 두세요.\r\n"
                        + boot + "\r\n\r\n" + ex,
                        "PocTrace.DeletedPipeLength",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                }
                return 1;
            }
        }

        static int RunCore()
        {
            ExeUseSession use = null;
            try
            {
                use = ExeUseLog.Open(PocTraceLocalPaths.DeletedPipeLength);
            }
            catch
            {
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.DeletedPipeLength);
                string logFile = Path.Combine(logDirectory, "deleted-pipe-" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        Path.Combine(logDirectory, "deleted-pipe-.txt"),
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        flushToDiskInterval: TimeSpan.FromSeconds(1),
                        shared: true)
                    .CreateLogger();
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deleted-pipe-log.path.txt"), logFile);
                }
                catch (Exception pointerEx)
                {
                    Log.Warning(pointerEx, "Could not write deleted-pipe-log.path.txt");
                }
                Log.Information("Log file {Path}", logFile);
                if (!string.IsNullOrEmpty(SecretConfigStore.Notice))
                    Log.Information("{Notice}", SecretConfigStore.Notice);

                var lic = PocTraceLicense.Verify();
                if (!lic.Success)
                {
                    Log.Error("License failed: {Code} {Error}", lic.ErrorCode, lic.Error);
                    MessageBox.Show(lic.Error, "PocTrace 라이선스", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 2;
                }
                if (use != null)
                    use.SetLicenseUser(lic.UserName);
                if (!PocTraceLicense.CanUseExtraTools(lic))
                {
                    MessageBox.Show(
                        "삭제 배관 길이 집계는 2차 권한에서 사용할 수 없습니다.",
                        "PocTrace 라이선스",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 2;
                }
                if (GlobalConfig.ActiveSchema != SchemaKind.S5d)
                {
                    MessageBox.Show(
                        "이 도구는 S5D만 읽습니다. App.config의 SchemaKind를 S5d로 두세요.",
                        "PocTrace.DeletedPipeLength",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 4;
                }

                Log.Information("PocTrace.DeletedPipeLength starting. user={User}", lic.UserName);
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Fatal(ex, "DeletedPipeLength startup failed");
                }
                catch
                {
                }
                ExeBootFile.Fail(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", "DeletedPipeLength-startup.log"), ex);
                MessageBox.Show(ex.ToString(), "PocTrace.DeletedPipeLength", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                try
                {
                    if (use != null)
                        use.Dispose();
                }
                catch
                {
                }
                Log.CloseAndFlush();
            }
        }
    }
}
