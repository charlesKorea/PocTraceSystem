using System;
using System.IO;
using System.Windows.Forms;

using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Licensing;
using PocTrace.Startup;
using Serilog;
using Serilog.Events;

namespace PocTrace.Admin
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static int Main()
        {
            string boot = null;
            try
            {
                boot = ExeBootFile.Start(PocTraceLocalPaths.Admin);
                return RunCore();
            }
            catch (Exception ex)
            {
                ExeBootFile.Fail(boot, ex);
                try
                {
                    MessageBox.Show(
                        "시작에 실패했습니다. 같은 빌드의 PocTrace.Core.dll 만 exe 옆에 두세요.\r\nJLic와 Serilog는 exe 안에 있습니다. 폴더에 예전 DLL이 남아 있으면 그 파일이 먼저 열립니다.\r\n" + boot + "\r\n\r\n" + ex,
                        "PocTrace Admin",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch { }
                return 1;
            }
        }

        static int RunCore()
        {
            ExeUseSession use = null;
            try { use = ExeUseLog.Open(PocTraceLocalPaths.Admin); }
            catch { }

            // 1. 로그 설정 (사용자별 LocalAppData — 공유 폴더에 쓰지 않음)
            string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.Admin);
            MemoryLoadLog.Initialize(logDirectory);
            string logPath = Path.Combine(logDirectory, "admin_log-.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                // Quartz 스케줄러 폴링 노이즈 제거 (예: "Batch acquisition of 0 triggers")
                .MinimumLevel.Override("Quartz", LogEventLevel.Warning)
                .MinimumLevel.Override("Quartz.Core", LogEventLevel.Warning)
                .Filter.ByExcluding(e =>
                {
                    var text = e.MessageTemplate.Text;
                    return text != null
                           && text.IndexOf("Batch acquisition of 0 triggers", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("==================================================");
                Log.Information(">>> PocTrace.Admin Scheduler Started");
                Log.Information($"Execution Path: {AppDomain.CurrentDomain.BaseDirectory}");

                var lic = PocTraceLicense.VerifyAdmin();
                if (!lic.Success)
                {
                    Log.Error("License failed: {Code} {Error}", lic.ErrorCode, lic.Error);
                    MessageBox.Show(lic.Error, "PocTrace 라이선스", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 2;
                }
                Log.Information("License ok user={User} group={Group}", lic.UserName, lic.Group);
                if (use != null)
                    use.SetLicenseUser(lic.UserName);

                // Admin regenerates cache — do not hard-block on age; log freshness only.
                var freshness = PocTrace.Core.Infrastructure.CacheFreshnessGuard.EvaluateViewerStartup();
                foreach (var line in freshness.Details)
                    Log.Information("[CacheGate] {Line}", line);
                if (!freshness.Allowed)
                    Log.Warning("Shared cache is stale/missing. Run DailyTraceJob to refresh.\n{Msg}", freshness.Message);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                // 프로그램 시작 중 치명적 오류 발생 시
                Log.Fatal(ex, ">>> Application Crushed Unexpectedly");
                MessageBox.Show($"Critical Error: {ex.Message}", "Error");
                return 1;
            }
            finally
            {
                try { if (use != null) use.Dispose(); }
                catch { }
                Log.Information(">>> PocTrace.Admin Scheduler Stopped");
                Log.CloseAndFlush();
            }
        }
    }
}
