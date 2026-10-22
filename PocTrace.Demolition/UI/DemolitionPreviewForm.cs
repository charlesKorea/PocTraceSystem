using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PocTrace.Core.Infrastructure;
using PocTrace.Demolition.Export;
using PocTrace.Demolition.Jobs;
using Serilog;

namespace PocTrace.Demolition.UI
{
  /// <summary>
  /// 철거 검수 결과 Preview (MLinkExportPreviewForm 패턴).
  /// 종합 그리드 + 상세 탭, Excel 저장 / HTML 요약.
  /// </summary>
  public sealed class DemolitionPreviewForm : Form
  {
    private static readonly Color HeaderBack = Color.FromArgb(28, 42, 58);
    private static readonly Color PanelBack = Color.FromArgb(245, 247, 250);

    private readonly DemolitionJobResult _job;
    private readonly string _defaultDir;
    private readonly DataGridView _summaryGrid;
    private readonly TextBox _searchBox;
    private readonly Label _summaryLabel;
    private readonly TabControl _tabs;
    private List<object[]> _summaryDataRows;

    public string LastExcelPath { get; private set; }
    public string LastHtmlPath { get; private set; }

    public DemolitionPreviewForm(DemolitionJobResult job, string excelDefaultDirectory = null)
    {
      _job = job ?? new DemolitionJobResult();
      _defaultDir = ResolveDir(excelDefaultDirectory);

      Text = DemolitionUiText.PreviewTitle;
      FormBorderStyle = FormBorderStyle.Sizable;
      StartPosition = FormStartPosition.CenterParent;
      MinimizeBox = false;
      MaximizeBox = true;
      ClientSize = new Size(1200, 700);
      MinimumSize = new Size(960, 520);
      BackColor = PanelBack;
      Font = new Font("Segoe UI", 9f);

      try
      {
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
      }
      catch
      {
        // ignore
      }

      var header = new Panel
      {
        Dock = DockStyle.Top,
        Height = 88,
        BackColor = HeaderBack,
        Padding = new Padding(16, 10, 16, 8)
      };

      var title = new Label
      {
        Dock = DockStyle.Top,
        Height = 28,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 13f, FontStyle.Bold),
        Text = DemolitionUiText.PreviewHeader
      };

      _summaryLabel = new Label
      {
        Dock = DockStyle.Top,
        Height = 40,
        ForeColor = Color.FromArgb(200, 220, 235),
        Text = BuildSummaryText()
      };

      header.Controls.Add(_summaryLabel);
      header.Controls.Add(title);

      _tabs = new TabControl { Dock = DockStyle.Fill };

      // —— 종합 tab ——
      var sumHost = new Panel { Dock = DockStyle.Fill };
      var searchBar = new Panel
      {
        Dock = DockStyle.Top,
        Height = 36,
        Padding = new Padding(8, 4, 8, 4)
      };
      var searchLabel = new Label { Text = "Search EQPID:", AutoSize = true, Left = 8, Top = 8 };
      _searchBox = new TextBox { Left = 100, Top = 4, Width = 320, Height = 24 };
      _searchBox.TextChanged += (s, e) => ApplySummaryFilter();
      searchBar.Controls.Add(searchLabel);
      searchBar.Controls.Add(_searchBox);
      searchBar.Resize += (s, e) =>
      {
        _searchBox.Width = Math.Max(180, searchBar.ClientSize.Width - 120);
      };

      _summaryGrid = CreateGrid();
      sumHost.Controls.Add(_summaryGrid);
      sumHost.Controls.Add(searchBar);
      var sumPage = new TabPage(DemolitionExcelExporter.SummarySheetName);
      sumPage.Controls.Add(sumHost);
      _tabs.TabPages.Add(sumPage);

      AddDetailTab(EquipmentInfoProjector.SheetName, EquipmentInfoProjector.Headers, _job.EquipmentInfoRows);
      AddDetailTab(StatusEmpProjector.SheetName, StatusEmpProjector.Headers, _job.StatusEmpRows);
      AddDetailTab(FullListNozzleProjector.SheetName, FullListNozzleProjector.Headers, _job.NozzleRows);
      AddDetailTab(FullListStickProjector.SheetName, FullListStickProjector.Headers, _job.StickRows);
      AddDetailTab(FullListTakeOffProjector.SheetName, FullListTakeOffProjector.Headers, _job.TakeOffRows);
      AddDetailTab(HookupNumberProjector.SheetName, HookupNumberProjector.Headers, _job.HookupRows);
      AddDetailTab(BimGroupEqpProjector.SheetName, BimGroupEqpProjector.Headers, _job.BimGroupEqpRows);

      var footer = new Panel
      {
        Dock = DockStyle.Bottom,
        Height = 52,
        Padding = new Padding(12, 8, 12, 8)
      };

      var btnExcel = new Button
      {
        Text = DemolitionUiText.BtnExcelSave,
        Width = 130,
        Height = 32,
        Left = 12,
        Top = 8,
        BackColor = Color.FromArgb(0, 120, 215),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
      };
      var btnHtml = new Button
      {
        Text = DemolitionUiText.BtnHtmlSummary,
        Width = 130,
        Height = 32,
        Left = 150,
        Top = 8
      };
      var btnClose = new Button
      {
        Text = DemolitionUiText.BtnClose,
        Width = 100,
        Height = 32,
        Anchor = AnchorStyles.Top | AnchorStyles.Right
      };

      void LayoutFooter(object s, EventArgs e)
      {
        btnClose.Left = footer.ClientSize.Width - btnClose.Width - 12;
      }

      footer.Resize += LayoutFooter;
      btnExcel.Click += (s, e) => SaveExcel();
      btnHtml.Click += (s, e) => SaveHtml();
      btnClose.Click += (s, e) =>
      {
        DialogResult = DialogResult.OK;
        Close();
      };

      footer.Controls.Add(btnExcel);
      footer.Controls.Add(btnHtml);
      footer.Controls.Add(btnClose);

      Controls.Add(_tabs);
      Controls.Add(footer);
      Controls.Add(header);

      LoadSummaryGrid();
      LayoutFooter(null, EventArgs.Empty);
      CancelButton = btnClose;
    }

    private string BuildSummaryText()
    {
      var c = _job.SummaryCounter;
      string cards = c == null
        ? ""
        : string.Join(" · ",
          Enumerable.Range(0, EqpSheetCounter.SheetCount)
            .Select(i => EqpSheetCounter.DetailSheetNames[i] + "=" + c.TotalForSheet(i)));

      return string.Format(
        "Line {0} · EQPID {1} (Traced {2} / Missing {3} / NoResult {4} / Error {5}) · {6:0.0}s\n{7}",
        _job.LinesProcessed,
        _job.EqpAttempted,
        _job.EqpTraced,
        _job.EqpMissingTag,
        _job.EqpNoResult,
        _job.EqpError,
        _job.Elapsed.TotalSeconds,
        cards);
    }

    private void LoadSummaryGrid()
    {
      var rows = _job.SummaryRows;
      if (rows == null || rows.Count == 0)
        rows = (_job.SummaryCounter ?? new EqpSheetCounter(Enumerable.Empty<string>())).ToSummaryRows();

      _summaryGrid.Columns.Clear();
      _summaryGrid.Rows.Clear();
      if (rows.Count == 0) return;

      var header = rows[0];
      for (int c = 0; c < header.Length; c++)
      {
        _summaryGrid.Columns.Add("c" + c, Convert.ToString(header[c]));
        if (c > 0)
          _summaryGrid.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
      }
      _summaryGrid.Columns[0].Width = 160;

      _summaryDataRows = rows.Skip(1).ToList();
      ApplySummaryFilter();
    }

    private void ApplySummaryFilter()
    {
      _summaryGrid.Rows.Clear();
      if (_summaryDataRows == null) return;
      string q = (_searchBox.Text ?? string.Empty).Trim();
      foreach (var row in _summaryDataRows)
      {
        string eqp = Convert.ToString(row[0]) ?? "";
        if (q.Length > 0 && eqp.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        _summaryGrid.Rows.Add(row);
      }
    }

    private void AddDetailTab(string name, string[] headers, IList<object[]> data)
    {
      var grid = CreateGrid();
      grid.Columns.Clear();
      for (int c = 0; c < headers.Length; c++)
        grid.Columns.Add("c" + c, headers[c]);

      if (data != null)
      {
        foreach (var row in data)
          grid.Rows.Add(row);
      }

      var page = new TabPage(name + " (" + (data?.Count ?? 0) + ")");
      page.Controls.Add(grid);
      _tabs.TabPages.Add(page);
    }

    private static DataGridView CreateGrid()
    {
      return new DataGridView
      {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
          BackColor = Color.FromArgb(47, 84, 150),
          ForeColor = Color.White,
          Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        }
      };
    }

    private void SaveExcel()
    {
      using (var save = new SaveFileDialog())
      {
        save.Title = DemolitionUiText.DlgExcelSaveTitle;
        save.Filter = "Excel Files (*.xlsx)|*.xlsx";
        save.InitialDirectory = _defaultDir;
        save.FileName = Path.GetFileName(DemolitionExcelExporter.SuggestDefaultPath(_defaultDir));
        if (save.ShowDialog(this) != DialogResult.OK) return;

        var export = DemolitionExcelExporter.Export(save.FileName, _job);
        if (!export.Success)
        {
          MessageBox.Show(this, export.ErrorMessage, DemolitionUiText.DlgExcelSaveFail,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        LastExcelPath = export.OutputPath;
        MessageBox.Show(this,
          string.Format(DemolitionUiText.MsgExcelSavedHint, LastExcelPath),
          "Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);

        try
        {
          Process.Start(new ProcessStartInfo { FileName = LastExcelPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
          Log.Warning(ex, "Open Excel failed");
        }
      }
    }

    private void SaveHtml()
    {
      using (var save = new SaveFileDialog())
      {
        save.Title = DemolitionUiText.DlgHtmlSaveTitle;
        save.Filter = "HTML Files (*.html)|*.html";
        save.InitialDirectory = _defaultDir;
        save.FileName = Path.GetFileName(DemolitionExcelExporter.SuggestHtmlPath(_defaultDir));
        if (save.ShowDialog(this) != DialogResult.OK) return;

        try
        {
          LastHtmlPath = DemolitionHtmlSummaryWriter.Write(save.FileName, _job);
          MessageBox.Show(this, string.Format(DemolitionUiText.MsgHtmlSaved, LastHtmlPath), "HTML",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
          Process.Start(new ProcessStartInfo { FileName = LastHtmlPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
          Log.Error(ex, "HTML summary failed");
          MessageBox.Show(this, ex.Message, DemolitionUiText.DlgHtmlSaveFail,
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }

    private static string ResolveDir(string path)
    {
      if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        return path;
      if (!string.IsNullOrWhiteSpace(path))
      {
        string d = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
      }
      return PocTraceLocalPaths.AppDirectory(PocTraceLocalPaths.Demolition);
    }
  }
}
