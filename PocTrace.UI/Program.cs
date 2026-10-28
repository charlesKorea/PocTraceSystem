using PocTrace.Core.Composition;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Licensing;
using PocTrace.Startup;
using PocTrace.UI;
using PocTrace.UI.Services;
using PocTrace.UI.ViewModels;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PocTrace
{
  static class Program
  {
    [STAThread]
    static int Main()
    {
      string boot = null;
      try
      {
        boot = ExeBootFile.Start(PocTraceLocalPaths.Viewer);
        return RunCore();
      }
      catch (Exception ex)
      {
        ExeBootFile.Fail(boot, ex);
        try
        {
          MessageBox.Show(
            "시작에 실패했습니다. 같은 빌드의 PocTrace.Core.dll 만 exe 옆에 두세요.\r\nJLic와 Serilog는 exe 안에 있습니다. 폴더에 예전 DLL이 남아 있으면 그 파일이 먼저 열립니다.\r\n" + boot + "\r\n\r\n" + ex,
            "PocTrace Viewer",
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
      try { use = ExeUseLog.Open(PocTraceLocalPaths.Viewer); }
      catch { }

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      string logDirectory = PocTraceLocalPaths.LogsDirectory(PocTraceLocalPaths.Viewer);
      CleanUpOldLogs(logDirectory);
      MemoryLoadLog.Initialize(logDirectory);

      Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
      Application.ThreadException += OnUiThreadException;
      AppDomain.CurrentDomain.UnhandledException += OnDomainException;
      TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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
        if (use != null)
          use.SetLicenseUser(lic.UserName);
        Log.Information(
          "Memory load log: {Path} procBits={Bits} AllowDatabaseFallback={Fallback}",
          MemoryLoadLog.FilePath,
          Environment.Is64BitProcess ? 64 : 32,
          GlobalConfig.AllowDatabaseFallback);
        MemoryLoadLog.Stage(
          "Startup",
          "allowDatabaseFallback=" + GlobalConfig.AllowDatabaseFallback
          + " log=" + MemoryLoadLog.FilePath);

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
        MemoryLoadLog.Fail("Viewer.Startup", ex);
        try { Log.Error(ex, "Viewer startup failed"); }
        catch { }
        try { MessageBox.Show($"실행 오류: {ex.Message}\n{ex.StackTrace}"); }
        catch { }
        return 1;
      }
      finally
      {
        try { if (use != null) use.Dispose(); }
        catch { }
        Log.CloseAndFlush();
      }
    }

    static void OnUiThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
    {
      MemoryLoadLog.Fail("WinForms.ThreadException", e.Exception);
      try { Log.Fatal(e.Exception, "Unhandled UI exception"); }
      catch { }
      try
      {
        MessageBox.Show(
          e.Exception.GetType().Name + ": " + e.Exception.Message + "\r\n" + MemoryLoadLog.FilePath,
          "PocTrace",
          MessageBoxButtons.OK,
          MessageBoxIcon.Error);
      }
      catch { }
    }

    static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
      var ex = e.ExceptionObject as Exception;
      MemoryLoadLog.Fail("AppDomain.Unhandled", ex);
      try
      {
        Log.Fatal(ex, "Unhandled domain exception");
        Log.CloseAndFlush();
      }
      catch { }
    }

    static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
      MemoryLoadLog.Fail("Task.Unobserved", e.Exception);
      try { Log.Fatal(e.Exception, "Unobserved task exception"); }
      catch { }
      e.SetObserved();
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
