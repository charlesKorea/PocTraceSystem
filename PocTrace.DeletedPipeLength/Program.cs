using System;
using System.IO;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Licensing;
using Serilog;

namespace PocTrace.DeletedPipeLength
{
    static class Program
    {
        [STAThread]
        static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.DeletedPipeLength);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDirectory, "deleted-pipe-.txt"),
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                var lic = PocTraceLicense.Verify();
                if (!lic.Success)
                {
                    Log.Error("License failed: {Code} {Error}", lic.ErrorCode, lic.Error);
                    MessageBox.Show(lic.Error, "PocTrace 라이선스", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 2;
                }
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
                Log.Fatal(ex, "DeletedPipeLength startup failed");
                MessageBox.Show(ex.Message, "PocTrace.DeletedPipeLength", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
