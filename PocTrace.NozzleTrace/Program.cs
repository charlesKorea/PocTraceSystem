using System;
using System.IO;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Licensing;
using PocTrace.Startup;
using Serilog;

namespace PocTrace.NozzleTrace
{
  static class Program
  {
    [STAThread]
    static int Main()
    {
      string boot = null;
      try
      {
        boot = ExeBootFile.Start(PocTraceLocalPaths.NozzleTrace);
        return RunCore();
      }
      catch (Exception ex)
      {
        ExeBootFile.Fail(boot, ex);
        try
        {
          MessageBox.Show(
            "시작에 실패했습니다. 같은 빌드 출력의 DLL을 exe와 같이 두세요.\r\nPocTrace.Core.dll, JLic.Core.dll, Serilog.dll, Serilog.Sinks.File.dll\r\n" + boot + "\r\n\r\n" + ex,
            "PocTrace NozzleTrace",
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
      try { use = ExeUseLog.Open(PocTraceLocalPaths.NozzleTrace); }
      catch { }

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

        if (use != null)
          use.SetLicenseUser(lic.UserName);
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
        try { if (use != null) use.Dispose(); }
        catch { }
        Log.CloseAndFlush();
      }
    }
  }
}
