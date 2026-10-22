using Serilog;
using System;
using System.IO;
using System.Windows.Forms;

namespace WinFormsOffline.UI
{
  internal static class Program
  {
    [STAThread]
    private static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
      Directory.CreateDirectory(logDir);

      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(
          Path.Combine(logDir, "app-.txt"),
          rollingInterval: RollingInterval.Day,
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

      try
      {
        Log.Information("App start (offline WinForms template)");
        Application.Run(new MainForm());
      }
      catch (Exception ex)
      {
        Log.Fatal(ex, "Unhandled");
        MessageBox.Show(ex.Message, "Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        Log.CloseAndFlush();
      }
    }
  }
}
