using PocTrace.Core.Composition;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Licensing;
using PocTrace.UI;
using PocTrace.UI.Services;
using PocTrace.UI.ViewModels;
using Serilog;
using System;
using System.IO;
using System.Windows.Forms;

namespace PocTrace
{
  static class Program
  {
    [STAThread]
    static int Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.Viewer);
      CleanUpOldLogs(logDirectory);

      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .Enrich.WithProperty("ThreadId", "")
        .WriteTo.File(Path.Combine(logDirectory, "poctrace-.txt"),
          outputTemplate:
          "[{Timestamp:HH:mm:ss} {Level:u3}] [Thread-{ThreadId}] {Message:lj}{NewLine}{Exception}",
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
        Log.Information("License ok user={User} group={Group}", lic.UserName, lic.Group);

        var cacheGate = CacheFreshnessGuard.EvaluateViewerStartup();
        foreach (var line in cacheGate.Details)
          Log.Information("[CacheGate] {Line}", line);

        if (!cacheGate.Allowed)
        {
          Log.Error("Viewer startup blocked by cache freshness gate.\n{Msg}", cacheGate.Message);
          MessageBox.Show(
            cacheGate.Message,
            "PocTrace Viewer — 캐시 만료/없음",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
          return 3;
        }

        // [Refactor / Goal 3] 경량(무외부의존) 컴포지션 루트에서 객체 그래프를 조립한다.
        var services = PocTraceAppServices.Create(Log.Logger, GlobalConfig.AllowDatabaseFallback);

        var mainViewModel = new MainViewModel(
          services.GraphLoader,
          services.CacheManager,
          services.CreateTraceEngine(),
          services.EngineFactory,
          services.ExcelReport,
          Log.Logger,
          new WinFormsDialogService());

        Application.Run(new MainForm(mainViewModel));
        return 0;
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Viewer startup failed");
        MessageBox.Show($"실행 오류: {ex.Message}\n{ex.StackTrace}");
        return 1;
      }
      finally
      {
        Log.CloseAndFlush();
      }
    }

    static void CleanUpOldLogs(string logPath)
    {
      try
      {
        if (!Directory.Exists(logPath))
          return;

        var cutoff = DateTime.Now.AddDays(-14);
        foreach (var file in Directory.GetFiles(logPath, "*.txt"))
        {
          if (File.GetLastWriteTime(file) < cutoff)
            File.Delete(file);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"로그 파일 삭제 중 오류 발생 (무시됨): {ex.Message}");
      }
    }
  }
}
