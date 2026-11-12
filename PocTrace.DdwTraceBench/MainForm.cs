using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PocTrace.DdwTraceBench.Data;
using PocTrace.DdwTraceBench.Export;
using PocTrace.DdwTraceBench.Trace;

namespace PocTrace.DdwTraceBench
{
    /// <summary>
    /// SVDI 시험용. 설비 Tag 하나를 받아 DDW tb_* 를 읽고 poc 추적 결과를 Excel 로 저장한다.
    /// 비밀번호는 저장하지 않는다.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly TextBox _uiFolder = new TextBox();
        private readonly ComboBox _connectionName = new ComboBox();
        private readonly Label _connectionInfo = new Label();
        private readonly TextBox _linked = new TextBox();
        private readonly TextBox _catalog = new TextBox();
        private UiSqlConfig _loadedConfig;

        private readonly TextBox _tag = new TextBox();
        private readonly NumericUpDown _maxObjects = new NumericUpDown();
        private readonly CheckBox _byModelType = new CheckBox();
        private readonly TextBox _modelTypes = new TextBox();
        private readonly CheckBox _byFamilyKey = new CheckBox();
        private readonly TextBox _familyKey = new TextBox();
        private readonly CheckBox _byBimZero = new CheckBox();
        private readonly TextBox _passThrough = new TextBox();
        private readonly CheckBox _forceStart = new CheckBox();
        private readonly TextBox _outDir = new TextBox();

        private readonly Button _test = new Button();
        private readonly Button _run = new Button();
        private readonly Button _cancel = new Button();
        private readonly Button _open = new Button();
        private readonly TextBox _log = new TextBox();

        private CancellationTokenSource _cts;
        private string _lastFile;

        private static string SettingsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DdwTraceBench.settings.txt"); }
        }

        public MainForm()
        {
            Text = "PocTrace.DdwTraceBench - DDW 설비 POC 추적 시험";
            ClientSize = new Size(900, 800);
            MinimumSize = new Size(760, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("맑은 고딕", 9F);

            BuildConnectionGroup();
            BuildTraceGroup();
            BuildButtons();

            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Both;
            _log.WordWrap = false;
            _log.Font = new Font("Consolas", 9F);
            _log.SetBounds(12, 408, 876, 380);
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_log);

            LoadSettings();
            if (_uiFolder.Text.Trim().Length == 0)
                _uiFolder.Text = UiSqlConfig.GuessFolder();
            TryLoadConfig(false);
            Log("DDW 설비 POC 추적 시험. 모든 조회는 version_sequence < 1000000000 이다.");
            Log("SQL 연결은 PocTrace.UI 폴더의 exe.config 와 config\\db.secret 을 그대로 쓴다. 암호는 이 프로그램에 저장하지 않는다.");
        }

        private static Label Lbl(string text, int x, int y, int w)
        {
            return new Label { Text = text, Left = x, Top = y + 3, Width = w, AutoSize = false };
        }

        private void BuildConnectionGroup()
        {
            var g = new GroupBox { Text = "연결 (PocTrace.UI 와 같은 config, 같은 FRM 서버)", Left = 12, Top = 8, Width = 876, Height = 148 };
            g.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(g);

            g.Controls.Add(Lbl("UI 폴더", 10, 22, 55));
            _uiFolder.SetBounds(68, 22, 690, 23);
            _uiFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            g.Controls.Add(_uiFolder);
            var browse = new Button { Text = "찾아보기", Left = 766, Top = 21, Width = 96, Height = 25 };
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.Description = "PocTrace.Viewer.exe.config 가 있는 폴더";
                    if (Directory.Exists(_uiFolder.Text.Trim()))
                        d.SelectedPath = _uiFolder.Text.Trim();
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        _uiFolder.Text = d.SelectedPath;
                        TryLoadConfig(true);
                    }
                }
            };
            g.Controls.Add(browse);

            g.Controls.Add(Lbl("연결", 10, 52, 55));
            _connectionName.SetBounds(68, 52, 220, 23);
            _connectionName.DropDownStyle = ComboBoxStyle.DropDownList;
            _connectionName.SelectedIndexChanged += (s, e) => ShowSelectedConnection();
            g.Controls.Add(_connectionName);
            var reload = new Button { Text = "다시 읽기", Left = 296, Top = 51, Width = 90, Height = 25 };
            reload.Click += (s, e) => TryLoadConfig(true);
            g.Controls.Add(reload);
            _connectionInfo.SetBounds(396, 55, 466, 20);
            _connectionInfo.AutoSize = false;
            _connectionInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            g.Controls.Add(_connectionInfo);

            g.Controls.Add(Lbl("Linked server", 10, 86, 80));
            _linked.SetBounds(92, 86, 150, 23);
            _linked.Text = "remote_monami";
            g.Controls.Add(_linked);
            g.Controls.Add(Lbl("카탈로그", 256, 86, 60));
            _catalog.SetBounds(318, 86, 120, 23);
            _catalog.Text = "monami";
            g.Controls.Add(_catalog);

            var hint = new Label
            {
                Text = "기본은 SourceDb. 비밀번호는 UI 폴더의 config\\db.secret 이다. 같은 서버의 DB면 Linked server 를 비운다.",
                Left = 10,
                Top = 116,
                Width = 850,
                Height = 22,
                AutoSize = false
            };
            g.Controls.Add(hint);
        }

        private void BuildTraceGroup()
        {
            var g = new GroupBox { Text = "추적", Left = 12, Top = 162, Width = 876, Height = 196 };
            g.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(g);

            g.Controls.Add(Lbl("설비 Tag", 10, 22, 60));
            _tag.SetBounds(72, 22, 200, 23);
            _tag.Text = "rme010n03";
            g.Controls.Add(_tag);
            g.Controls.Add(Lbl("객체 한도", 290, 22, 60));
            _maxObjects.SetBounds(352, 22, 80, 23);
            _maxObjects.Minimum = 10;
            _maxObjects.Maximum = 200000;
            _maxObjects.Value = 3000;
            g.Controls.Add(_maxObjects);

            var rule = new Label { Text = "설비 판정(하나만 만족해도 설비). 시작 설비와 도착 객체 모두에 쓴다.", Left = 10, Top = 56, Width = 560, AutoSize = false };
            g.Controls.Add(rule);

            _byModelType.Text = "model_type =";
            _byModelType.Checked = true;
            _byModelType.SetBounds(10, 78, 100, 23);
            g.Controls.Add(_byModelType);
            _modelTypes.SetBounds(112, 78, 160, 23);
            _modelTypes.Text = "50594049";
            g.Controls.Add(_modelTypes);

            _byFamilyKey.Text = "family_key =";
            _byFamilyKey.Checked = true;
            _byFamilyKey.SetBounds(290, 78, 100, 23);
            g.Controls.Add(_byFamilyKey);
            _familyKey.SetBounds(392, 78, 120, 23);
            _familyKey.Text = "equipment";
            g.Controls.Add(_familyKey);

            _byBimZero.Text = "bim_yn = 0 (끝점 판정과 겹쳐 기본 꺼짐)";
            _byBimZero.Checked = false;
            _byBimZero.SetBounds(530, 78, 330, 23);
            g.Controls.Add(_byBimZero);

            g.Controls.Add(Lbl("통과 model_type", 10, 108, 100));
            _passThrough.SetBounds(112, 108, 160, 23);
            _passThrough.Text = "51380485";
            g.Controls.Add(_passThrough);
            var passNote = new Label { Text = "connector_pipe 등. 설비 판정에서 빼고 통과한다.", Left = 290, Top = 111, Width = 400, AutoSize = false };
            g.Controls.Add(passNote);

            _forceStart.Text = "tag 소유 객체가 설비 판정을 못 해도 그 객체에서 시작";
            _forceStart.SetBounds(10, 136, 420, 23);
            g.Controls.Add(_forceStart);

            g.Controls.Add(Lbl("저장 폴더", 10, 164, 60));
            _outDir.SetBounds(72, 164, 690, 23);
            _outDir.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            _outDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            g.Controls.Add(_outDir);
            var browse = new Button { Text = "찾아보기", Left = 770, Top = 163, Width = 90, Height = 25 };
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog())
                {
                    if (d.ShowDialog(this) == DialogResult.OK)
                        _outDir.Text = d.SelectedPath;
                }
            };
            g.Controls.Add(browse);
        }

        private void BuildButtons()
        {
            _test.Text = "연결·스키마 확인";
            _test.SetBounds(12, 368, 140, 30);
            _test.Click += async (s, e) => await OnTest();
            Controls.Add(_test);

            _run.Text = "추적 실행";
            _run.SetBounds(162, 368, 120, 30);
            _run.Click += async (s, e) => await OnRun();
            Controls.Add(_run);

            _cancel.Text = "취소";
            _cancel.SetBounds(292, 368, 80, 30);
            _cancel.Enabled = false;
            _cancel.Click += (s, e) =>
            {
                if (_cts != null)
                    _cts.Cancel();
            };
            Controls.Add(_cancel);

            _open.Text = "결과 폴더 열기";
            _open.SetBounds(382, 368, 120, 30);
            _open.Click += (s, e) => OpenFolder();
            Controls.Add(_open);
        }

        private void TryLoadConfig(bool tell)
        {
            try
            {
                string keep = (_connectionName.SelectedItem as string) ?? (_connectionName.Tag as string);
                _loadedConfig = UiSqlConfig.Load(_uiFolder.Text);
                _connectionName.Items.Clear();
                foreach (string name in _loadedConfig.Connections.Keys)
                    _connectionName.Items.Add(name);
                string pick = keep != null && _loadedConfig.Connections.ContainsKey(keep)
                    ? keep
                    : UiSqlConfig.PreferredName(_loadedConfig.Connections);
                if (pick != null)
                    _connectionName.SelectedItem = pick;
                ShowSelectedConnection();
                Log(_loadedConfig.Notice + " 연결 " + _connectionName.Items.Count.ToString(CultureInfo.InvariantCulture) + "개. 선택 " + (_connectionName.SelectedItem ?? ""));
            }
            catch (Exception ex)
            {
                _loadedConfig = null;
                _connectionName.Items.Clear();
                _connectionInfo.Text = "";
                if (tell || _uiFolder.Text.Trim().Length > 0)
                    Log("설정을 읽지 못했다: " + ex.Message);
            }
        }

        private void ShowSelectedConnection()
        {
            string name = _connectionName.SelectedItem as string;
            string value = null;
            if (_loadedConfig != null && name != null)
                _loadedConfig.Connections.TryGetValue(name, out value);
            _connectionInfo.Text = string.IsNullOrEmpty(name) ? "" : name + " = " + UiSqlConfig.Describe(value);
        }

        private void Log(string text)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(Log), text);
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + text + "\r\n");
        }

        private void SetBusy(bool busy)
        {
            _run.Enabled = !busy;
            _test.Enabled = !busy;
            _cancel.Enabled = busy;
        }

        private sealed class Request
        {
            public string ConnectionString;
            public string ConnectionInfo;
            public DdwSettings Settings;
            public TraceRunOptions Options;
            public string OutputDirectory;
        }

        private Request ReadRequest()
        {
            UiSqlConfig config = UiSqlConfig.Load(_uiFolder.Text);
            _loadedConfig = config;
            string name = _connectionName.SelectedItem as string;
            if (string.IsNullOrEmpty(name))
                name = UiSqlConfig.PreferredName(config.Connections);
            string connectionString;
            if (string.IsNullOrEmpty(name) || !config.Connections.TryGetValue(name, out connectionString) || string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("쓸 연결 문자열이 없습니다. UI 폴더의 SourceDb 를 확인하세요.");

            var settings = new DdwSettings
            {
                LinkedServer = _linked.Text.Trim(),
                Catalog = _catalog.Text.Trim()
            };
            if (string.IsNullOrEmpty(settings.Prefix))
                throw new InvalidOperationException("카탈로그 이름이 비었습니다.");

            var options = new TraceRunOptions
            {
                Tag = _tag.Text.Trim(),
                MaxObjects = (int)_maxObjects.Value,
                EquipByModelType = _byModelType.Checked,
                EquipmentModelTypes = ParseLongs(_modelTypes.Text, "model_type"),
                EquipByFamilyKey = _byFamilyKey.Checked,
                EquipmentFamilyKey = _familyKey.Text.Trim(),
                EquipByBimYnZero = _byBimZero.Checked,
                PassThroughModelTypes = ParseLongs(_passThrough.Text, "통과 model_type"),
                ForceStartWhenNotEquipment = _forceStart.Checked
            };

            return new Request
            {
                ConnectionString = connectionString,
                ConnectionInfo = name + " = " + UiSqlConfig.Describe(connectionString) + " / " + (string.IsNullOrEmpty(settings.LinkedServer) ? settings.Catalog : settings.LinkedServer + "." + settings.Catalog),
                Settings = settings,
                Options = options,
                OutputDirectory = _outDir.Text.Trim()
            };
        }

        private static HashSet<long> ParseLongs(string text, string label)
        {
            var set = new HashSet<long>();
            foreach (var part in (text ?? string.Empty).Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                long v;
                if (!long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                    throw new InvalidOperationException(label + " 값이 숫자가 아닙니다: " + part);
                set.Add(v);
            }
            return set;
        }

        private async Task OnTest()
        {
            Request req;
            try
            {
                req = ReadRequest();
                SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            _cts = new CancellationTokenSource();
            try
            {
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(req.ConnectionString))
                    {
                        conn.Open();
                        Log("연결됨: " + req.ConnectionInfo + " (SQL Server " + conn.ServerVersion + ")");
                        var data = new SqlDdwData(conn, req.Settings, Log);
                        data.Initialize();
                        foreach (var n in data.SchemaNotes)
                            Log("스키마: " + n);
                        Log("필수 열 확인 완료.");
                        var owners = data.FindTagOwners(req.Options.Tag);
                        Log("Tag '" + req.Options.Tag + "' 의 tag 속성 owner 수: " + owners.Count.ToString(CultureInfo.InvariantCulture)
                            + (owners.Count > 0 ? " (앞 10자: " + string.Join(", ", owners.Select(o => Hex.Short(o.OwnerId))) + ")" : string.Empty));
                    }
                });
            }
            catch (Exception ex)
            {
                Log("오류: " + Shorten(ex.Message));
                Log(ex.ToString());
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task OnRun()
        {
            Request req;
            try
            {
                req = ReadRequest();
                if (req.Options.Tag.Length == 0)
                    throw new InvalidOperationException("Tag를 입력하세요.");
                if (req.OutputDirectory.Length == 0)
                    throw new InvalidOperationException("저장 폴더를 입력하세요.");
                SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            try
            {
                string file = await Task.Run(() => RunTrace(req, ct), ct);
                _lastFile = file;
                Log("저장: " + file);
            }
            catch (OperationCanceledException)
            {
                Log("취소했다. Excel은 저장하지 않았다.");
            }
            catch (Exception ex)
            {
                Log("오류: " + Shorten(ex.Message));
                Log(ex.ToString());
                MessageBox.Show(this, Shorten(ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private string RunTrace(Request req, CancellationToken ct)
        {
            using (var conn = new SqlConnection(req.ConnectionString))
            {
                conn.Open();
                Log("연결됨: " + req.ConnectionInfo);
                var data = new SqlDdwData(conn, req.Settings, Log);
                data.Initialize();
                foreach (var n in data.SchemaNotes)
                    Log("스키마: " + n);

                var tracer = new DdwTracer(data, Log);
                var result = tracer.Run(req.Options, ct);

                foreach (var t in result.TagOwners)
                    Log("tag owner " + Hex.Short(t.Owner.OwnerId) + ": 설비=" + (t.IsEquipment ? "Y" : "N") + ", 추적=" + (t.Traced ? "Y" : "N") + (string.IsNullOrEmpty(t.Note) ? string.Empty : " (" + t.Note + ")"));
                Log("hop " + result.Hops.Count.ToString(CultureInfo.InvariantCulture)
                    + ", 배관 " + result.Pipes.Count.ToString(CultureInfo.InvariantCulture)
                    + ", 객체 " + result.Objects.Count.ToString(CultureInfo.InvariantCulture)
                    + ", POC " + result.Pocs.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var n in result.Notes)
                    Log("비고: " + n);

                Directory.CreateDirectory(req.OutputDirectory);
                string safeTag = new string(req.Options.Tag.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
                string path = Path.Combine(req.OutputDirectory, "ddw-trace-" + safeTag + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".xlsx");
                TraceExcelWriter.Write(path, result, data.SchemaNotes, req.ConnectionInfo);
                return path;
            }
        }

        private static string Shorten(string text)
        {
            if (text == null)
                return string.Empty;
            return text.Length <= 300 ? text : text.Substring(0, 300);
        }

        private void OpenFolder()
        {
            try
            {
                string dir = _outDir.Text.Trim();
                if (!string.IsNullOrEmpty(_lastFile) && File.Exists(_lastFile))
                    Process.Start("explorer.exe", "/select,\"" + _lastFile + "\"");
                else if (Directory.Exists(dir))
                    Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                Log("폴더를 열지 못했다: " + ex.Message);
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;
                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1);
                    switch (key)
                    {
                        case "uiFolder": _uiFolder.Text = val; break;
                        case "connectionName": _connectionName.Tag = val; break;
                        case "linked": _linked.Text = val; break;
                        case "catalog": _catalog.Text = val; break;
                        case "tag": _tag.Text = val; break;
                        case "outDir": _outDir.Text = val; break;
                        case "byModelType": _byModelType.Checked = val == "1"; break;
                        case "modelTypes": _modelTypes.Text = val; break;
                        case "byFamilyKey": _byFamilyKey.Checked = val == "1"; break;
                        case "familyKey": _familyKey.Text = val; break;
                        case "byBimZero": _byBimZero.Checked = val == "1"; break;
                        case "passThrough": _passThrough.Text = val; break;
                    }
                }
                string savedName = _connectionName.Tag as string;
                if (!string.IsNullOrEmpty(savedName))
                    _connectionName.Tag = savedName;
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("uiFolder=" + _uiFolder.Text);
                sb.AppendLine("connectionName=" + (_connectionName.SelectedItem as string ?? ""));
                sb.AppendLine("linked=" + _linked.Text);
                sb.AppendLine("catalog=" + _catalog.Text);
                sb.AppendLine("tag=" + _tag.Text);
                sb.AppendLine("outDir=" + _outDir.Text);
                sb.AppendLine("byModelType=" + (_byModelType.Checked ? "1" : "0"));
                sb.AppendLine("modelTypes=" + _modelTypes.Text);
                sb.AppendLine("byFamilyKey=" + (_byFamilyKey.Checked ? "1" : "0"));
                sb.AppendLine("familyKey=" + _familyKey.Text);
                sb.AppendLine("byBimZero=" + (_byBimZero.Checked ? "1" : "0"));
                sb.AppendLine("passThrough=" + _passThrough.Text);
                File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
