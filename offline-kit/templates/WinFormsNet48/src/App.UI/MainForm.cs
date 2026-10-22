using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Serilog;
using WinFormsOffline.Core.Jobs;
using WinFormsOffline.Core.Reporting;

namespace WinFormsOffline.UI
{
  public partial class MainForm : Form
  {
    public MainForm()
    {
      InitializeComponent();
    }

    private void BtnRun_Click(object sender, EventArgs e)
    {
      try
      {
        var job = SampleJob.Run(txtHint.Text);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "out");
        Directory.CreateDirectory(outDir);

        var xlsx = SampleExcelExporter.Export(Path.Combine(outDir, "sample_" + stamp + ".xlsx"), job);
        var html = LocalHtmlWriter.Write(Path.Combine(outDir, "sample_" + stamp + ".html"), job);

        lblStatus.Text = job.Message + Environment.NewLine + xlsx + Environment.NewLine + html;
        Log.Information("Run ok");
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Run failed");
        MessageBox.Show(ex.ToString(), "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }

    private void BtnOpenOut_Click(object sender, EventArgs e)
    {
      var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "out");
      Directory.CreateDirectory(outDir);
      Process.Start(new ProcessStartInfo
      {
        FileName = outDir,
        UseShellExecute = true
      });
    }
  }
}
