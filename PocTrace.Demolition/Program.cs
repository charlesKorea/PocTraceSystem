using System;
using System.IO;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Licensing;
using PocTrace.Startup;
using Serilog;

namespace PocTrace.Demolition
{
  static class Program
  {
    [STAThread]
    static int Main()
    {
      string boot = null;
      try
      {
        boot = ExeBootFile.Start(PocTraceLocalPaths.Demolition);
        return RunCore();
      }
      catch (Exception ex)
      {
        ExeBootFile.Fail(boot, ex);
        try
        {
          MessageBox.Show(
            "시작에 실패했습니다. 같은 빌드 출력의 DLL을 exe와 같이 두세요.\r\nPocTrace.Core.dll, JLic.Core.dll, Serilog.dll, Serilog.Sinks.File.dll\r\n" + boot + "\r\n\r\n" + ex,
            "PocTrace Demolition",
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
      try { use = ExeUseLog.Open(PocTraceLocalPaths.Demolition); }
      catch { }

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.Demolition);
      MemoryLoadLog.Initialize(logDirectory);

      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(
          Path.Combine(logDirectory, "demolition-.txt"),
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
          MessageBox.Show("철거검수는 2차 권한에서 사용할 수 없습니다.", "PocTrace 라이선스",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
          return 2;
        }

        var cacheGate = CacheFreshnessGuard.EvaluateViewerStartup();
        foreach (var line in cacheGate.Details)
          Log.Information("[CacheGate] {Line}", line);
        if (!cacheGate.Allowed)
        {
          Log.Error("Demolition startup blocked by cache freshness gate.\n{Msg}", cacheGate.Message);
          MessageBox.Show(cacheGate.Message, "PocTrace Demolition — 캐시 만료/없음",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
          return 3;
        }

        if (use != null)
          use.SetLicenseUser(lic.UserName);
        Log.Information("PocTrace.Demolition starting. Base={Base} user={User}",
          AppDomain.CurrentDomain.BaseDirectory, lic.UserName);
        Application.Run(new MainForm());
        return 0;
      }
      catch (Exception ex)
      {
        Log.Fatal(ex, "Demolition startup failed");
        MessageBox.Show(
          string.Format(DemolitionUiText.MsgStartupError, ex.Message, ex.StackTrace),
          DemolitionUiText.AppTitle,
          MessageBoxButtons.OK,
          MessageBoxIcon.Error);
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
