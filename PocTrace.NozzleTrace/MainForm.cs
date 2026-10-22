using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Models.ReverseTrace;
using PocTrace.NozzleTrace.Input;
using PocTrace.NozzleTrace.Jobs;
using Serilog;
namespace PocTrace.NozzleTrace
{
  public partial class MainForm : Form
  {
    private bool _busy;
    private string _lastOutputPath;
    private NozzleLineCacheSession _session;
    public MainForm()
    {
      InitializeComponent();
      try
      {
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
          this.Icon = new System.Drawing.Icon(iconPath);
        else
          this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
      }
      catch { }
      foreach (var db in GlobalConfig.TargetDatabases)
        cmbDb.Items.Add(db);
      if (cmbDb.Items.Count > 0)
        cmbDb.SelectedIndex = 0;
      SetTraceInputsEnabled(false);
      AppendLog("1) DB 선택 후 [캐시 로드] → 2) 단건 입력 또는 Excel 업로드 → 3) 결과 Summary/Path");
      AppendLog("업로드 양식: No | BimId | InstanceId | Guid (복수 시 Guid > BimId > InstanceId)");
    }
    private void SetTraceInputsEnabled(bool enabled)
    {
      txtKey.Enabled = enabled;
      rbBimId.Enabled = enabled;
      rbNumericId.Enabled = enabled;
      rbNodeGuid.Enabled = enabled;
      btnRun.Enabled = enabled;
      btnUpload.Enabled = enabled;
    }
    private void AppendLog(string line)
    {
      if (string.IsNullOrEmpty(line)) return;
      string stamp = DateTime.Now.ToString("HH:mm:ss");
      txtLog.AppendText("[" + stamp + "] " + line + Environment.NewLine);
      txtLog.SelectionStart = txtLog.TextLength;
      txtLog.ScrollToCaret();
      Log.Information("[UI] {Line}", line);
    }
    private void InvalidateSession(string reason)
    {
      _session?.Dispose();
      _session = null;
      SetTraceInputsEnabled(false);
      lblCacheStatus.Text = "캐시: 미로드";
      if (!string.IsNullOrEmpty(reason))
        AppendLog(reason);
    }
    private void cmbDb_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (_session != null)
        InvalidateSession("DB 변경 — 캐시를 다시 로드하세요.");
    }
    private async void btnLoadCache_Click(object sender, EventArgs e)
    {
      if (_busy) return;
      if (cmbDb.SelectedItem == null)
      {
        MessageBox.Show(this, "DB를 선택하세요.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }
      string dbName = cmbDb.SelectedItem.ToString();
      InvalidateSession(null);
      _busy = true;
      btnLoadCache.Enabled = false;
      btnRun.Enabled = false;
      btnUpload.Enabled = false;
      progressBar.Style = ProgressBarStyle.Continuous;
      progressBar.Value = 0;
      var progress = new Progress<NozzleTraceJobProgress>(p =>
      {
        lblStatus.Text = p.Message ?? string.Empty;
        progressBar.Value = Math.Max(0, Math.Min(100, (int)(p.Fraction * 100)));
      });
      try
      {
        AppendLog(NozzleTraceUserMessages.Step(1, 2, "캐시 로드 시작 — " + dbName));
        var job = new NozzleTraceJob();
        var load = await job.LoadLineCacheAsync(dbName, progress).ConfigureAwait(true);
        if (!load.Success || load.Session == null)
        {
          lblCacheStatus.Text = "캐시: 실패";
          AppendLog("캐시 로드 실패 — " + (load.ErrorMessage ?? "알 수 없음"));
          MessageBox.Show(this, load.ErrorMessage ?? "캐시 로드 실패", Text,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }
        _session = load.Session;
        lblCacheStatus.Text = BimIdDiagnostics.FormatUserSummary(_session.BimIdStats, _session.SavedAt);
        AppendLog("캐시 로드 완료 — " + lblCacheStatus.Text.Replace(Environment.NewLine, " / "));
        if (_session.IsStale)
          AppendLog("⚠ 캐시가 오래되었습니다. Admin에서 재생성을 권장합니다.");
        if (_session.BimIdStats != null)
        {
          foreach (var w in _session.BimIdStats.UserWarnings)
            AppendLog("⚠ " + w);
        }
        SetTraceInputsEnabled(true);
        lblStatus.Text = "캐시 준비 완료 — 단건 실행 또는 Excel 업로드";
        txtKey.Focus();
      }
      catch (Exception ex)
      {
        AppendLog("오류 — " + ex.Message);
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        _busy = false;
        btnLoadCache.Enabled = true;
        progressBar.Value = _session != null ? 100 : 0;
      }
    }
    private async void btnRun_Click(object sender, EventArgs e)
    {
      if (_busy) return;
      if (_session == null)
      {
        MessageBox.Show(this, "먼저 [캐시 로드]를 실행하세요.", Text,
          MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }
      string key = txtKey.Text?.Trim();
      if (string.IsNullOrWhiteSpace(key))
      {
        MessageBox.Show(this, "BIM ID, Instance ID 또는 Node GUID를 입력하세요.", Text,
          MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }
      var keyKind = SelectedKeyKind();
      if (keyKind == NozzleLookupKeyKind.NodeGuid)
      {
        Guid parsed;
        if (!Guid.TryParse(key, out parsed))
        {
          MessageBox.Show(this,
            "Node GUID 형식이 아닙니다.\n예: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
          txtKey.Focus();
          return;
        }
        key = parsed.ToString();
      }
      else if (keyKind == NozzleLookupKeyKind.InstanceNumericId)
      {
        long numericId;
        if (!long.TryParse(key, out numericId))
        {
          MessageBox.Show(this, "Instance ID는 숫자여야 합니다.", Text,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
          txtKey.Focus();
          return;
        }
      }
      var row = new NozzleTraceInputRow
      {
        RowNumber = 1,
        DbName = _session.DbName,
        KeyKind = keyKind,
        KeyValue = key
      };
      await RunRowsAsync(new List<NozzleTraceInputRow> { row },
        "단건 역추적 — " + keyKind + " " + key).ConfigureAwait(true);
    }
    private async void btnUpload_Click(object sender, EventArgs e)
    {
      if (_busy) return;
      if (_session == null)
      {
        MessageBox.Show(this, "먼저 [캐시 로드]를 실행하세요.", Text,
          MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }
      using (var dlg = new OpenFileDialog())
      {
        dlg.Title = "Nozzle Trace 대량 입력 Excel";
        dlg.Filter = "Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*";
        dlg.CheckFileExists = true;
        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;
        AppendLog("업로드 파일: " + Path.GetFileName(dlg.FileName));
        var loaded = NozzleBulkExcelReader.Load(dlg.FileName, _session.DbName);
        if (!loaded.Success)
        {
          AppendLog("업로드 실패 — " + (loaded.ErrorMessage ?? "알 수 없음"));
          MessageBox.Show(this, loaded.ErrorMessage ?? "업로드 실패", Text,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }
        AppendLog(string.Format(
          "업로드 파싱 — 유효 {0}행 (빈행 {1}, 무효 {2})",
          loaded.Rows.Count, loaded.SkippedEmpty, loaded.SkippedInvalid));
        foreach (var w in loaded.Warnings.Take(20))
          AppendLog("⚠ " + w);
        if (loaded.Warnings.Count > 20)
          AppendLog("⚠ … 경고 " + (loaded.Warnings.Count - 20) + "건 더 있음 (로그 파일 참고)");
        var confirm = MessageBox.Show(this,
          string.Format("유효 입력 {0}건을 역추적할까요?\n(결과: Summary + Path_Detail)", loaded.Rows.Count),
          "대량 역추적", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
          return;
        await RunRowsAsync(loaded.Rows,
          "대량 역추적 — " + loaded.Rows.Count + "건").ConfigureAwait(true);
      }
    }
    private void btnTemplate_Click(object sender, EventArgs e)
    {
      try
      {
        string dir = Path.Combine(
          GlobalConfig.DailyReportPath ?? AppDomain.CurrentDomain.BaseDirectory,
          "NozzleTrace",
          "Templates");
        string path = NozzleBulkExcelReader.SaveTemplate(dir);
        AppendLog("양식 저장: " + path);
        var open = MessageBox.Show(this,
          "업로드 양식을 저장했습니다.\n열까요?\n" + path,
          Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (open == DialogResult.Yes)
          Process.Start(path);
      }
      catch (Exception ex)
      {
        AppendLog("양식 저장 실패 — " + ex.Message);
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }
    private async Task RunRowsAsync(IReadOnlyList<NozzleTraceInputRow> rows, string logTitle)
    {
      if (_session == null || rows == null || rows.Count == 0)
        return;
      _busy = true;
      btnRun.Enabled = false;
      btnUpload.Enabled = false;
      btnLoadCache.Enabled = false;
      btnTemplate.Enabled = false;
      btnOpenOutput.Enabled = false;
      progressBar.Value = 0;
      var progress = new Progress<NozzleTraceJobProgress>(p =>
      {
        lblStatus.Text = p.Message ?? string.Empty;
        progressBar.Value = Math.Max(0, Math.Min(100, (int)(p.Fraction * 100)));
      });
      try
      {
        AppendLog(logTitle);
        var job = new NozzleTraceJob();
        var result = await job.RunTracesAsync(_session, rows, progress).ConfigureAwait(true);
        if (result.Success)
        {
          _lastOutputPath = result.OutputExcelPath;
          btnOpenOutput.Enabled = !string.IsNullOrWhiteSpace(_lastOutputPath);
          AppendLog(string.Format(
            "완료 — 입력 {0}건 / 경로 {1}건 / 파일: {2}",
            result.InputsProcessed,
            result.TracePathCount,
            Path.GetFileName(result.OutputExcelPath ?? string.Empty)));
          foreach (var w in result.Warnings.Take(30))
            AppendLog("⚠ " + w);
          if (!string.IsNullOrWhiteSpace(result.OutputExcelPath))
          {
            var open = MessageBox.Show(this,
              "Excel을 열까요?\n" + result.OutputExcelPath,
              Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (open == DialogResult.Yes)
              Process.Start(result.OutputExcelPath);
          }
        }
        else
        {
          AppendLog("실패 — " + (result.ErrorMessage ?? "알 수 없음"));
          MessageBox.Show(this, result.ErrorMessage ?? "실패", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
      catch (Exception ex)
      {
        AppendLog("오류 — " + ex.Message);
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        _busy = false;
        btnLoadCache.Enabled = true;
        btnTemplate.Enabled = true;
        SetTraceInputsEnabled(_session != null);
        progressBar.Value = 100;
      }
    }
    private NozzleLookupKeyKind SelectedKeyKind()
    {
      if (rbNodeGuid.Checked) return NozzleLookupKeyKind.NodeGuid;
      if (rbNumericId.Checked) return NozzleLookupKeyKind.InstanceNumericId;
      return NozzleLookupKeyKind.BimId;
    }
    private void btnOpenOutput_Click(object sender, EventArgs e)
    {
      if (string.IsNullOrWhiteSpace(_lastOutputPath)) return;
      string dir = Path.GetDirectoryName(_lastOutputPath);
      if (Directory.Exists(dir))
        Process.Start("explorer.exe", dir);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
      _session?.Dispose();
      base.OnFormClosed(e);
    }
  }
}
