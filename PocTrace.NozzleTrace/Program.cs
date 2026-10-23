using System;
using System.IO;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Licensing;
using Serilog;

namespace PocTrace.NozzleTrace
{
  static class Program
  {
    [STAThread]
    static int Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.NozzleTrace);
      MemoryLoadLog.Initialize(logDirectory);

      Serilog.Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(
          System.IO.Path.Combine(logDirectory, "nozzletrace-.txt"),
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
          MessageBox.Show("노즐추적은 2차 권한에서 사용할 수 없습니다.", "PocTrace 라이선스",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
          return 2;
        }

        var cacheGate = CacheFreshnessGuard.EvaluateViewerStartup();
        foreach (var line in cacheGate.Details)
          Log.Information("[CacheGate] {Line}", line);
        if (!cacheGate.Allowed)
        {
          Log.Error("NozzleTrace startup blocked by cache freshness gate.\n{Msg}", cacheGate.Message);
          MessageBox.Show(cacheGate.Message, "PocTrace NozzleTrace — 캐시 만료/없음",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
          return 3;
        }

        Log.Information("PocTrace.NozzleTrace starting. user={User}", lic.UserName);
        Application.Run(new MainForm());
        return 0;
      }
      catch (Exception ex)
      {
        Serilog.Log.Fatal(ex, "NozzleTrace startup failed");
        MessageBox.Show(ex.Message, "PocTrace.NozzleTrace", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
      }
      finally
      {
        Log.CloseAndFlush();
      }
    }
  }
}
