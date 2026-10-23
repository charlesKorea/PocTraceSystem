using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PocTrace.Core.Composition;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Licensing;
using Serilog;

namespace PocTrace.Web
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool openBrowser = !args.Any(a => string.Equals(a, "--no-browser", StringComparison.OrdinalIgnoreCase));
            string logDirectory = Path.Combine(PortalService.WorkRoot, "logs");
            Directory.CreateDirectory(logDirectory);
            MemoryLoadLog.Initialize(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logDirectory, "poctrace-web-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                var lic = PocTraceLicense.Verify();
                if (!lic.Success)
                {
                    Log.Error("License failed: {Code} {Error}", lic.ErrorCode, lic.Error);
                    Console.Error.WriteLine("라이선스 오류: " + lic.Error);
                    Console.Error.WriteLine("앱 폴더에 JLic.Core.dll 과 Lic\\jlab.lic 를 두고 AccessApps에 PT 또는 AL 을 넣으세요.");
                    return 2;
                }
                Log.Information("License ok user={User} group={Group} extra={Extra}",
                    lic.UserName, lic.Group, PocTraceLicense.CanUseExtraTools(lic));

                if (GlobalConfig.AllowDatabaseFallback)
                    Log.Warning("AllowDatabaseFallback=true 입니다. 웹 포털은 공유 캐시만 읽는 구성이 맞습니다.");

                var services = PocTraceAppServices.Create(Log.Logger, GlobalConfig.AllowDatabaseFallback);
                var portal = new PortalService(services, lic);
                var server = new PortalServer(portal);

                string host = Setting("ListenHost", "127.0.0.1");
                int port = SettingInt("ListenPort", 5088);
                string prefix = server.Start(host, port);

                Log.Information("PocTrace.Web listening at {Url}", prefix);
                Log.Information("SharedCachePath={Path} TargetDatabases={Dbs}",
                    GlobalConfig.SharedCachePath,
                    string.Join(",", GlobalConfig.TargetDatabases));

                bool wantBrowser = openBrowser && SettingBool("OpenBrowser", true);
                if (wantBrowser)
                    OpenBrowser(prefix);

                Console.WriteLine("PocTrace.Web " + prefix);
                Console.WriteLine("이 창을 닫으면 웹 화면도 멈춥니다. Viewer exe는 그대로 사용할 수 있습니다.");
                server.RunUntilEnter();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PocTrace.Web failed to start");
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "브라우저를 열지 못했습니다. 주소창에 {Url} 를 입력하세요.", url);
            }
        }

        private static string Setting(string key, string fallback)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int SettingInt(string key, int fallback)
        {
            int parsed;
            return int.TryParse(Setting(key, ""), out parsed) && parsed > 0 ? parsed : fallback;
        }

        private static bool SettingBool(string key, bool fallback)
        {
            bool parsed;
            return bool.TryParse(Setting(key, ""), out parsed) ? parsed : fallback;
        }
    }
}
