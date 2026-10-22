using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Demolition.Export;
using PocTrace.Demolition.Input;
using PocTrace.Demolition.Jobs;
using PocTrace.Demolition.UI;
using Serilog;

namespace PocTrace.Demolition
{
  public partial class MainForm : Form
  {
    private List<DemolitionInputRow> _inputRows = new List<DemolitionInputRow>();
    private DemolitionJobResult _lastJobResult;
    private string _lastExportPath;
    private bool _running;

    public MainForm()
    {
      InitializeComponent();
      Text = DemolitionUiText.AppTitle;
      btnRun.Text = DemolitionUiText.BtnRun;
      try
      {
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
      }
      catch
      {
        // exe 아이콘 미설정 시 기본값 유지
      }
      txtInputPath.Text = string.Empty;
      lblStatus.Text = DemolitionUiText.StatusBrowseHint;
      ConfigureGridColumns();
      btnRun.Enabled = false;
    }

    /// <summary>A3 Job 결과 (A4/A5에서 사용).</summary>
    public DemolitionJobResult LastJobResult => _lastJobResult;

    /// <summary>A4 Excel 출력 경로.</summary>
    public string LastExportPath => _lastExportPath;

    public IReadOnlyList<DemolitionInputRow> LoadedRows => _inputRows;

    public IReadOnlyList<DemolitionInputRow> ValidRows =>
      _inputRows.Where(r => r.IsValid).ToList();

    private void ConfigureGridColumns()
    {
      gridInput.AutoGenerateColumns = false;
      gridInput.Columns.Clear();
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.RowNumber),
        HeaderText = "Row",
        Width = 50,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.SiteRaw),
        HeaderText = "Site",
        Width = 90,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.MappedLine),
        HeaderText = "MappedLine",
        Width = 90,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.Process),
        HeaderText = DemolitionUiText.ColProcess,
        Width = 90,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.ConstructionCode),
        HeaderText = DemolitionUiText.ColConstructionCode,
        Width = 120,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.EqpId),
        HeaderText = "EQPID",
        Width = 120,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.LoadStatus),
        HeaderText = "LoadStatus",
        Width = 100,
        ReadOnly = true
      });
      gridInput.Columns.Add(new DataGridViewTextBoxColumn
      {
        DataPropertyName = nameof(DemolitionInputRow.TraceStatus),
        HeaderText = "TraceStatus",
        Width = 100,
        ReadOnly = true
      });
    }

    private void btnBrowse_Click(object sender, EventArgs e)
    {
      if (_running) return;

      using (var dlg = new OpenFileDialog())
      {
        dlg.Title = DemolitionUiText.DlgInputTitle;
        dlg.Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*";
        dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
        dlg.CheckFileExists = true;
        dlg.Multiselect = false;

        if (dlg.ShowDialog(this) != DialogResult.OK)
          return;

        LoadInputFile(dlg.FileName);
      }
    }

    private void LoadInputFile(string path)
    {
      try
      {
        Cursor = Cursors.WaitCursor;
        lblStatus.Text = string.Format(DemolitionUiText.StatusReading, Path.GetFileName(path));

        var loaded = DemolitionExcelReader.Load(path);
        if (!loaded.Success)
        {
          _inputRows = new List<DemolitionInputRow>();
          _lastJobResult = null;
          _lastExportPath = null;
          BindGrid();
          btnRun.Enabled = false;
          txtInputPath.Text = path;
          lblStatus.Text = string.Format(DemolitionUiText.StatusLoadFail, loaded.ErrorMessage);
          MessageBox.Show(this, loaded.ErrorMessage, DemolitionUiText.DlgLoadFail,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        _inputRows = loaded.Rows;
        _lastJobResult = null;
        _lastExportPath = null;
        txtInputPath.Text = path;
        BindGrid();
        gridInput.DataBindingComplete -= GridInput_DataBindingComplete;
        gridInput.DataBindingComplete += GridInput_DataBindingComplete;
        ColorInvalidRows();

        btnRun.Enabled = loaded.ValidCount > 0;
        lblStatus.Text = string.Format(
          DemolitionUiText.StatusLoadOk,
          loaded.Rows.Count, loaded.ValidCount, loaded.InvalidCount, loaded.DistinctEqpCount);

        if (loaded.InvalidCount > 0)
        {
          MessageBox.Show(this,
            string.Format(
              DemolitionUiText.MsgPartialInvalid,
              loaded.ValidCount, loaded.InvalidCount),
            DemolitionUiText.DlgInputValidate,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex, "LoadInputFile failed");
        MessageBox.Show(this, ex.Message, DemolitionUiText.DlgLoadError,
          MessageBoxButtons.OK, MessageBoxIcon.Error);
        lblStatus.Text = string.Format(DemolitionUiText.StatusLoadError, ex.Message);
        btnRun.Enabled = false;
      }
      finally
      {
        Cursor = Cursors.Default;
      }
    }

    private void BindGrid()
    {
      gridInput.DataSource = null;
      gridInput.DataSource = _inputRows;
    }

    private void GridInput_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
    {
      ColorInvalidRows();
    }

    private void ColorInvalidRows()
    {
      foreach (DataGridViewRow viewRow in gridInput.Rows)
      {
        var data = viewRow.DataBoundItem as DemolitionInputRow;
        if (data == null) continue;
        if (!data.IsValid)
        {
          viewRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
          viewRow.DefaultCellStyle.ForeColor = Color.FromArgb(120, 60, 0);
        }
        else
        {
          viewRow.DefaultCellStyle.BackColor = Color.White;
          viewRow.DefaultCellStyle.ForeColor = Color.Black;
        }
      }
    }

    private async void btnRun_Click(object sender, EventArgs e)
    {
      if (_running) return;

      var valid = ValidRows;
      if (valid.Count == 0)
      {
        MessageBox.Show(this, DemolitionUiText.MsgNoValidEqp, "Run",
          MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      var byLine = valid
        .GroupBy(r => r.MappedLine, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.Key + "=" + g.Select(x => x.EqpId).Distinct(StringComparer.OrdinalIgnoreCase).Count())
        .ToList();

      var confirm = MessageBox.Show(this,
        DemolitionUiText.MsgRunConfirm +
        DemolitionUiText.Kz("유효 행", "有效行") + ": " + valid.Count + "\n" +
        "Line EQPID(distinct): " + string.Join(", ", byLine) + "\n\n" +
        DemolitionUiText.MsgRunContinue,
        DemolitionUiText.DlgRunTitle,
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question);
      if (confirm != DialogResult.Yes)
        return;

      _running = true;
      btnRun.Enabled = false;
      btnBrowse.Enabled = false;
      Cursor = Cursors.WaitCursor;
      progressBar.Value = 0;
      lblPercent.Text = "0%";
      lblEta.Text = "";

      foreach (var row in valid)
        row.TraceStatus = "Pending";
      RefreshTraceStatusColumn();

      var progress = new Progress<DemolitionJobProgress>(p =>
      {
        if (p == null) return;
        if (!string.IsNullOrEmpty(p.Message))
          lblStatus.Text = p.Message;

        int pct = p.Percent;
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;
        if (pct != progressBar.Value)
          progressBar.Value = pct;
        lblPercent.Text = pct + "%";
        lblEta.Text = p.EtaSeconds.HasValue
          ? string.Format(DemolitionUiText.EtaSeconds, p.EtaSeconds.Value)
          : "";

        RefreshTraceStatusColumn();
      });

      try
      {
        var job = new DemolitionInspectionJob();
        _lastJobResult = await job.RunAsync(valid, progress).ConfigureAwait(true);

        RefreshTraceStatusColumn();
        ColorInvalidRows();

        if (_lastJobResult == null || !_lastJobResult.Success)
        {
          string err = _lastJobResult?.ErrorMessage ?? DemolitionUiText.MsgUnknownError;
          lblStatus.Text = string.Format(DemolitionUiText.StatusFail, err);
          MessageBox.Show(this, err, DemolitionUiText.DlgInspectFail, MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }

        lblStatus.Text = string.Format(
          DemolitionUiText.StatusInspectDone,
          _lastJobResult.EqpAttempted,
          _lastJobResult.Elapsed.TotalSeconds);

        string defaultDir = !string.IsNullOrWhiteSpace(txtInputPath.Text)
          ? Path.GetDirectoryName(txtInputPath.Text)
          : PocTraceLocalPaths.AppDirectory(PocTraceLocalPaths.Demolition);

        using (var preview = new DemolitionPreviewForm(_lastJobResult, defaultDir))
        {
          preview.ShowDialog(this);
          if (!string.IsNullOrEmpty(preview.LastExcelPath))
            _lastExportPath = preview.LastExcelPath;
        }

        if (!string.IsNullOrEmpty(_lastExportPath))
          lblStatus.Text = string.Format(DemolitionUiText.StatusDoneExcel, Path.GetFileName(_lastExportPath));
      }
      catch (Exception ex)
      {
        Log.Error(ex, "[Demolition] Run failed");
        lblStatus.Text = string.Format(DemolitionUiText.StatusError, ex.Message);
        MessageBox.Show(this, ex.Message, DemolitionUiText.DlgInspectError,
          MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        _running = false;
        btnBrowse.Enabled = true;
        btnRun.Enabled = _inputRows != null && _inputRows.Any(r => r != null && r.IsValid);
        Cursor = Cursors.Default;
        if (progressBar.Value < 100 && _lastJobResult != null && _lastJobResult.Success)
        {
          progressBar.Value = 100;
          lblPercent.Text = "100%";
          lblEta.Text = DemolitionUiText.ZeroSeconds;
        }
        RefreshTraceStatusColumn();
      }
    }

    private void RefreshTraceStatusColumn()
    {
      if (gridInput.DataSource == null) return;
      gridInput.Invalidate();
      gridInput.Refresh();
    }
  }
}
