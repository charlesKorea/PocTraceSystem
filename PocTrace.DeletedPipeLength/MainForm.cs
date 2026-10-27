using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PocTrace.Core.Engine.DeletedPipe;
using PocTrace.Core.Infrastructure;
using PocTrace.DeletedPipeLength.Export;
using PocTrace.DeletedPipeLength.Input;
using Serilog;

namespace PocTrace.DeletedPipeLength
{
    public sealed class MainForm : Form
    {
        private readonly ComboBox _db = new ComboBox();
        private readonly TextBox _excel = new TextBox();
        private readonly DateTimePicker _cutover = new DateTimePicker();
        private readonly Button _browse = new Button();
        private readonly Button _run = new Button();
        private readonly Button _cancel = new Button();
        private readonly TextBox _log = new TextBox();
        private CancellationTokenSource _runCts;

        public MainForm()
        {
            Text = "삭제 배관 길이 / BOM";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 920;
            Height = 680;

            Controls.Add(LabelAt("DB", 16, 18));
            _db.Left = 120;
            _db.Top = 14;
            _db.Width = 220;
            _db.DropDownStyle = ComboBoxStyle.DropDownList;
            var databases = GlobalConfig.TargetDatabases ?? new string[0];
            for (int i = 0; i < databases.Length; i++)
                _db.Items.Add(databases[i]);
            if (_db.Items.Count > 0)
                _db.SelectedIndex = 0;
            Controls.Add(_db);

            Controls.Add(LabelAt("EQPID 엑셀", 16, 50));
            _excel.Left = 120;
            _excel.Top = 46;
            _excel.Width = 640;
            _excel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(_excel);
            _browse.Text = "찾기";
            _browse.Left = 770;
            _browse.Top = 44;
            _browse.Width = 100;
            _browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _browse.Click += OnBrowse;
            Controls.Add(_browse);

            Controls.Add(LabelAt("기준일", 16, 82));
            _cutover.Left = 120;
            _cutover.Top = 78;
            _cutover.Width = 160;
            _cutover.Format = DateTimePickerFormat.Custom;
            _cutover.CustomFormat = "yyyy-MM-dd";
            _cutover.Value = new DateTime(2026, 1, 1);
            Controls.Add(_cutover);

            var note = LabelAt("EQPID 기준. Op2(기준일 이후)와 Op1+건설코드 Delete를 삭제 설비로 봅니다. BOM은 피팅 이름·QCode 수량입니다.", 16, 112);
            note.MaximumSize = new System.Drawing.Size(860, 0);
            Controls.Add(note);

            _run.Text = "실행";
            _run.Left = 120;
            _run.Top = 148;
            _run.Width = 100;
            _run.Click += OnRun;
            Controls.Add(_run);

            _cancel.Text = "취소";
            _cancel.Left = 230;
            _cancel.Top = 148;
            _cancel.Width = 100;
            _cancel.Enabled = false;
            _cancel.Click += delegate { _runCts?.Cancel(); };
            Controls.Add(_cancel);

            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Both;
            _log.Left = 12;
            _log.Top = 188;
            _log.Width = 880;
            _log.Height = 434;
            _log.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            _log.Font = new System.Drawing.Font("Consolas", 9f);
            Controls.Add(_log);
        }

        private static Label LabelAt(string text, int left, int top)
        {
            return new Label { Text = text, Left = left, Top = top, AutoSize = true };
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _excel.Text = dialog.FileName;
            }
        }

        private async void OnRun(object sender, EventArgs e)
        {
            string fallback = _db.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(fallback))
            {
                MessageBox.Show(this, "DB를 선택하세요.");
                return;
            }

            var source = ConfigurationManager.ConnectionStrings["SourceDb"];
            string connectionString = source == null ? null : source.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString) ||
                connectionString.IndexOf("YOUR_SERVER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(this, "App.config의 SourceDb 연결 문자열을 채우세요.");
                return;
            }

            var loaded = DeletedPipeExcelReader.Load(_excel.Text, fallback);
            if (!loaded.Success)
            {
                MessageBox.Show(this, loaded.ErrorMessage);
                return;
            }

            string outputPath;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                dialog.FileName = "deleted-pipe-length-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                outputPath = dialog.FileName;
            }

            _run.Enabled = false;
            _cancel.Enabled = true;
            _runCts = new CancellationTokenSource();
            var progress = new Progress<string>(WriteLog);
            try
            {
                var reports = await Task.Run(
                    () => RunAll(connectionString, loaded.Rows, _cutover.Value.Date, progress, _runCts.Token),
                    _runCts.Token).ConfigureAwait(true);

                DeletedPipeExcelWriter.Write(outputPath, reports);
                WriteLog("저장: " + outputPath);
                Log.Information("Deleted pipe report saved. {Path}", outputPath);
            }
            catch (OperationCanceledException)
            {
                WriteLog("취소했습니다.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Deleted pipe run failed");
                WriteLog(ex.Message);
                MessageBox.Show(this, ex.Message, "삭제 배관 길이", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _run.Enabled = true;
                _cancel.Enabled = false;
                _runCts.Dispose();
                _runCts = null;
            }
        }

        private static List<DeletedPipeReport> RunAll(
            string connectionString,
            IList<DeletedPipeInputRow> rows,
            DateTime cutover,
            IProgress<string> progress,
            CancellationToken cancellation)
        {
            var reports = new List<DeletedPipeReport>();
            var invalid = new DeletedPipeReport { DbName = string.Empty, Cutover = cutover };
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.IsValid)
                {
                    invalid.Unmatched.Add(new DeletedPipeUnmatchedRow
                    {
                        DbName = row.SiteRaw,
                        EqpId = row.EqpId,
                        Reason = row.LoadStatus == "InvalidSite"
                            ? "DB 라인을 해석하지 못했습니다."
                            : "DB 라인이 없습니다."
                    });
                    continue;
                }

                List<string> eqpIds;
                if (!groups.TryGetValue(row.MappedLine, out eqpIds))
                {
                    eqpIds = new List<string>();
                    groups.Add(row.MappedLine, eqpIds);
                }
                eqpIds.Add(row.EqpId);
            }

            var service = new DeletedPipeLengthService();
            foreach (var pair in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellation.ThrowIfCancellationRequested();
                var report = service.Run(connectionString, pair.Key, pair.Value, cutover, progress, cancellation);
                reports.Add(report);
                for (int w = 0; w < report.Warnings.Count; w++)
                    progress?.Report(report.Warnings[w]);
            }

            if (invalid.Unmatched.Count > 0)
                reports.Add(invalid);
            return reports;
        }

        private void WriteLog(string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(WriteLog), message);
                return;
            }
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }
    }
}
