using MetroFramework.Controls; // Spinner용
using MetroFramework.Forms;
using PocTrace.Core.Models; // EquipmentTraceSummary 사용
using PocTrace.Core.Utils;
using PocTrace.UI.Helpers; // UserSettings 사용
using PocTrace.UI.ViewModels;
using Serilog;
using Serilog.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel; // PropertyChanged 사용
using System.Drawing; // Point 사용
using System.Linq; // LINQ 사용
using System.Windows.Forms;

namespace PocTrace.UI
{
    public partial class MainForm : MetroForm
    {
        // ViewModel은 상태와 데이터 로직을 담당
        private readonly MainViewModel _viewModel;
        private readonly ILogger _logger;

        private BindingSource _traceBindingSource; // 바인딩 소스 추가

        // [UI Control] 스피너와 메인 컨테이너 참조 변수
        //private MetroProgressSpinner _spinner;
        //private Control _mainContainer; // 버튼, 그리드 등이 포함된 패널

        private ContextMenuStrip _ctxMenu; // [신규] 우클릭 메뉴

        // [신규] 최근 검색어 저장용
        private List<string> _recentSearches = new List<string>();
        private const int MaxRecentSearches = 10;

        // [신규] 사용자 설정 관리
        private UserSettings _userSettings;

        public MainForm(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            // ---------------------------------------------------------
            // [신규] 사용자 설정 로드
            // ---------------------------------------------------------
            LoadUserSettings();

            // ---------------------------------------------------------
            // 1. UI 컨트롤 참조 확보
            // ---------------------------------------------------------
            // 디자이너에 추가한 Spinner 찾기
            //var spinners = this.Controls.Find("spinnerLoading", true);
            //if (spinners.Length > 0) _spinner = spinners[0] as MetroProgressSpinner;

            // 메인 컨테이너 찾기 (보통 MetroPanel이나 폼 자체)
            // 폼 전체를 잠그려면 'this'를 써도 되지만, 최소화/닫기는 되어야 하므로 
            // 내부 컨트롤들이 담긴 Panel이 있다면 그것을 지정하는 것이 좋음.
            // 여기서는 편의상 폼 내부의 특정 패널(pnlContainer라 가정) 혹은 
            // 주요 버튼들이 있는 영역을 제어한다고 가정합니다.
            // 만약 Panel이 없다면, 버튼들을 직접 제어해야 합니다.

            // (예시: pnlMain이라는 패널 안에 모든 버튼/그리드가 있다고 가정)
            // _mainContainer = this.Controls.Find("pnlMain", true).FirstOrDefault() as Control; 

            // 만약 패널이 없다면 폼 자체를 제어하되, Spinner는 폼의 자식이므로 
            // Spinner가 Enabled=false 되어 멈추지 않도록 주의해야 합니다.
            // 가장 확실한 방법: 이벤트 핸들러에서 개별 주요 컨트롤 제어.

            // ---------------------------------------------------------
            // 2. ViewModel 이벤트 / 바인딩은 AttachEvents·BindControls에서만 연결
            //    (여기서 PropertyChanged를 구독하면 AttachEvents와 이중 구독됨)
            // ---------------------------------------------------------

            // 3. 기존 바인딩 및 Grid 초기화
            //txtSearch.DataBindings.Add("Text", _viewModel, "SearchText", true, DataSourceUpdateMode.OnPropertyChanged);
            //lblStatus.DataBindings.Add("Text", _viewModel, "StatusMessage");
            InitializeGrid();
            InitializeContextMenu(); // [신규] 메뉴 초기화
            // 2. 데이터 바인딩 (수동 바인딩 방식 권장 for WinForms)
            BindControls();

            // 3. 이벤트 연결
            AttachEvents();

            // [신규] 단축키 등록
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // [신규] Search 텍스트박스 Enter 키 이벤트
            txtSearch.KeyDown += TxtSearch_KeyDown;

            // [Fix] txtSearch 자동완성 설정 (최근 검색어 표시용)
            txtSearch.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            txtSearch.AutoCompleteSource = AutoCompleteSource.CustomSource;

            // [신규] 폼 이벤트 연결 (설정 저장용)
            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;
            this.Move += MainForm_Move;

            // [신규] 사용자 설정 적용
            ApplyUserSettings();

            // [Fix] cboPropList Controls.Find로 안전하게 찾기
            var cboProp = this.Controls.Find("cboPropList", true).FirstOrDefault() as ComboBox;
            if (cboProp != null && cboProp.Items.Count > 0)
            {
                cboProp.SelectedIndex = 0;
            }

            // [초기화] 버튼 상태 설정
            UpdateButtonStates(false);

            //btnLoadData_Click(null, null);
        }

        // [신규] 단축키 처리
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+L: Data Load
            if (e.Control && e.KeyCode == Keys.L)
            {
                btnLoadData_Click(sender, e);
                e.SuppressKeyPress = true;
            }
            // Ctrl+E: Export
            else if (e.Control && e.KeyCode == Keys.E)
            {
                btnExport_Click(sender, e);
                e.SuppressKeyPress = true;
            }
            // Ctrl+F: Search 텍스트박스 포커스
            else if (e.Control && e.KeyCode == Keys.F)
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
                e.SuppressKeyPress = true;
            }
            // F5: 현재 검색 새로고침
            else if (e.KeyCode == Keys.F5)
            {
                if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    btnSearch_Click(sender, e);
                }
                e.SuppressKeyPress = true;
            }
        }

        // [신규] Search 텍스트박스 Enter 키 처리
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSearch_Click(sender, e);
                e.SuppressKeyPress = true;
            }
        }

        // [신규] 버튼 상태 관리 (우선순위 1)
        private void UpdateButtonStates(bool isBusy)
        {
            // 작업 중이면 버튼 비활성화, 완료되면 활성화
            btnLoadData.Enabled = !isBusy;
            btnSearch.Enabled = !isBusy;
            btnExport.Enabled = !isBusy;
            btnBatchTrace.Enabled = !isBusy;

            // [수정] Batch Trace 버튼 찾기 (BtnBatchTrace_Click 메서드 이름 확인)
            var btnBatch = this.Controls.Find("BtnBatchTrace", true).FirstOrDefault() as Button;
            if (btnBatch == null)
            {
                // 다른 이름으로도 찾아보기
                btnBatch = this.Controls.Find("btnBatchTrace", true).FirstOrDefault() as Button;
            }
            if (btnBatch != null)
            {
                btnBatch.Enabled = !isBusy;
            }

            // 텍스트박스 활성화/비활성화
            txtSearch.Enabled = !isBusy;

            var txtBatch = this.Controls.Find("txtBatchInput", true).FirstOrDefault() as TextBox;
            if (txtBatch != null)
            {
                txtBatch.Enabled = !isBusy;
            }

            // 커서 변경
            this.Cursor = isBusy ? Cursors.WaitCursor : Cursors.Default;

            // Grid 활성화/비활성화
            gridResults.Enabled = !isBusy;
        }

        

        private void BindControls()
        {
            // [New] DB 선택 콤보박스
            // 디자이너에 cboDbList가 없으면 생성 필요
            if (this.Controls.Find("cboDbList", true).Length > 0)
            {
                var cbo = this.Controls.Find("cboDbList", true)[0] as ComboBox; // 또는 MetroComboBox
                if (cbo != null)
                {
                    cbo.DataSource = _viewModel.DbList != null && _viewModel.DbList.Count > 0
                        ? _viewModel.DbList.ToList()
                        : new List<string> { "X1", "X2", "X2_2" };
                    cbo.DataBindings.Add("SelectedItem", _viewModel, "SelectedDb", true, DataSourceUpdateMode.OnPropertyChanged);
                    
                    // [중요] SelectedIndexChanged 이벤트 추가
                    // 사용자가 콤보박스를 변경할 때 ViewModel의 SelectedDb 속성이 업데이트되도록 함
                    cbo.SelectedIndexChanged += (s, e) =>
                    {
                        if (cbo.SelectedItem != null)
                        {
                            _viewModel.SelectedDb = cbo.SelectedItem.ToString();
                            System.Diagnostics.Debug.WriteLine($"DB changed to: {_viewModel.SelectedDb}");
                        }
                    };
                }
            }

            // [New] 캐시 상태 라벨
            if (this.Controls.Find("lblCacheStatus", true).Length > 0)
            {
                var lbl = this.Controls.Find("lblCacheStatus", true)[0] as Control;
                lbl.DataBindings.Add("Text", _viewModel, "CacheStatus");
            }

            // [New] 배치 입력 텍스트박스
            if (this.Controls.Find("txtBatchInput", true).Length > 0)
            {
                var txt = this.Controls.Find("txtBatchInput", true)[0] as Control;
                txt.DataBindings.Add("Text", _viewModel, "BatchInputText", true, DataSourceUpdateMode.OnPropertyChanged);
            }

            // 기존 컨트롤 바인딩
            txtSearch.DataBindings.Add("Text", _viewModel, "SearchText", true, DataSourceUpdateMode.OnPropertyChanged);
            lblStatus.DataBindings.Add("Text", _viewModel, "StatusMessage");

            // IsBusy 바인딩 (ViewModel_PropertyChanged에서 처리하므로 생략 가능하나 명시적 바인딩도 좋음)


        }

        private void AttachEvents()
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // btnSearch.Click은 Designer(btnSearch_Click)에서만 연결.
            // 여기서 SearchCommand를 또 붙이면 검색이 이중 실행된다.
        }


        // [신규] 컨텍스트 메뉴 설정
        private void InitializeContextMenu()
        {
            _ctxMenu = new ContextMenuStrip();
            var itemTrace = new ToolStripMenuItem("🔍 Trace Details (Log All Steps)");
            itemTrace.Click += ItemTrace_Click;
            _ctxMenu.Items.Add(itemTrace);
        }

        // [신규] 메뉴 클릭 이벤트 핸들러
        private async void ItemTrace_Click(object sender, EventArgs e)
        {
            if (gridResults.SelectedRows.Count == 0) return;

            // 선택된 행의 데이터 가져오기
            var selectedRow = gridResults.SelectedRows[0];
            var summary = selectedRow.DataBoundItem as EquipmentTraceSummary;

            if (summary != null)
            {
                // ViewModel에 요청
                await _viewModel.ReTraceSpecificAsync(summary.EquipmentGuid, summary.EquipmentTag);
                MessageBox.Show("상세 추적 완료!\n로그 파일(logs/log.txt)을 확인하세요.", "Trace Detail");
            }
        }

        // [핵심] IsBusy 상태에 따라 UI를 총괄 제어하는 메서드 (우선순위 1)
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 폼이 닫히는 중이면 무시 (안전장치)
            if (this.IsDisposed || this.Disposing) return;
            if (e.PropertyName == nameof(MainViewModel.IsBusy))
            {
                // [안전장치 1] 다른 스레드에서 호출된 경우 UI 스레드로 넘김
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => ViewModel_PropertyChanged(sender, e)));
                    return;
                }

                bool isBusy = _viewModel.IsBusy;

                // 버튼 상태 업데이트
                UpdateButtonStates(isBusy);

                // Spinner 제어 (있는 경우)
                var spinners = this.Controls.Find("spinnerLoading", true);
                if (spinners.Length > 0 && spinners[0] is MetroProgressSpinner spinner)
                {
                    spinner.Visible = isBusy;
                    spinner.Spinning = isBusy;
                    if (isBusy) spinner.BringToFront();
                }
            }
        }

        // [UI Design] Grid 컬럼 수동 정의 (Apple Style: Clean & Organized)
        private void InitializeGrid()
        {
            // [중요] 자동 생성 기능을 가장 먼저 끕니다.
            gridResults.AutoGenerateColumns = false;

            // 기존에 디자이너나 이전 로직에 의해 추가된 컬럼이 있다면 싹 지웁니다.
            gridResults.Columns.Clear();

            // BindingSource 초기화
            _traceBindingSource = new BindingSource();
            _traceBindingSource.DataSource = _viewModel.TraceSummaries;

            // [중요] 컬럼을 다 정의한 뒤에 DataSource를 연결하는 것이 안전할 때도 있지만,
            // AutoGenerateColumns = false가 되어 있다면 순서는 크게 상관없습니다.
            // 하지만 명시적으로 컬럼부터 정의하겠습니다.

            // [UI Design] 요청하신 6개 핵심 컬럼 정의

            // 1) 설비ID (Tag)
            AddColumn("EquipmentTag", "설비ID", 100);

            // 2) 검수결과 (PASS/FAIL)
            AddColumn("CheckResult", "검수결과", 80);

            // 3) Issue Count
            AddColumn("CountERR", "Issue Count", 80);

            // 4) 설비수량
            AddColumn("EquipmentCount", "설비수량", 70);

            // 5) POC수량
            AddColumn("TotalPocCount", "POC수량", 70);

            // 6) BreakDown
            AddColumn("DerivedBranchCount", "BreakDown", 80);

            // 나중에 쓸 수도 있으니 "ExcelFilePath"로 매핑해두고 헤더만 "비고"로 변경
            AddColumn("ExcelFilePath", "비고", 150);


            // ---------------------------------------------------------
            // [UI Design] 컬럼 수동 정의 (요청하신 8개 항목만)
            // ---------------------------------------------------------
            /*
		    AddColumn("EquipmentTag",       "설비TAG",   80);
		    AddColumn("EquipmentName",      "설비NAME",  120);
		    AddColumn("EquipmentGuid",      "설비GUID",  200);
		    AddColumn("TotalPocCount",      "POC수량",    60);
		    AddColumn("DerivedBranchCount", "파생수량",   60);
		    AddColumn("CountNOZ",           "NOZ",       50);
		    AddColumn("CountTOF",           "TOF",       50);
		    AddColumn("CountEQP",           "EQP",       50);
		    AddColumn("CountCAP",           "CAP",       50);
		    AddColumn("CountEMP",           "EMP",       50);
		    AddColumn("CountERR",           "ERR",       50);
		    AddColumn("CountING",           "ING",       50);
		    AddColumn("TotalHopCount",      "HOP",       60);
		    AddColumn("TotalLength",        "Length",    70);
			*/


            // 스타일 설정
            gridResults.AllowUserToAddRows = false;
            gridResults.ReadOnly = true;
            gridResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // [우선순위 4] DataGridView 기능 강화 - 열 정렬 가능
            foreach (DataGridViewColumn col in gridResults.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.Automatic;
            }

            // [우선순위 4] 더블 클릭 이벤트 추가
            gridResults.CellDoubleClick += GridResults_CellDoubleClick;

            // 마지막으로 데이터 소스 연결
            gridResults.DataSource = _traceBindingSource;

            // [신규] 우클릭 이벤트 연결
            gridResults.CellMouseDown += GridResults_CellMouseDown;
        }

        // [우선순위 4] DataGridView 더블 클릭 이벤트 핸들러
        private void GridResults_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < gridResults.Rows.Count)
            {
                var row = gridResults.Rows[e.RowIndex];
                var summary = row.DataBoundItem as EquipmentTraceSummary;

                if (summary != null)
                {
                    // 상세 정보를 팝업으로 표시
                    string detailInfo = $"설비ID: {summary.EquipmentTag}\n" +
                                        $"설비명: {summary.EquipmentName}\n" +
                                        $"검수결과: {summary.CheckResult}\n" +
                                        $"Issue Count: {summary.CountERR}\n" +
                                        $"설비수량: {summary.EquipmentCount}\n" +
                                        $"POC수량: {summary.TotalPocCount}\n" +
                                        $"BreakDown: {summary.DerivedBranchCount}";

                    MessageBox.Show(detailInfo, "상세 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void AddColumn(string dataPropertyName, string headerText, int width)
        {
            var col = new DataGridViewTextBoxColumn();
            col.DataPropertyName = dataPropertyName; // ViewModel(TraceResult)의 속성명
            col.HeaderText = headerText;             // 헤더에 표시될 텍스트
            col.Width = width;
            col.ReadOnly = true;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            // 숫자 컬럼은 가운데 정렬
            if (dataPropertyName != "EquipmentTag" && dataPropertyName != "ExcelFilePath")
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            gridResults.Columns.Add(col);
        }


        // === 이벤트 핸들러 (사용자 동작 처리) ===

        // [신규] 우클릭 시 행 선택 처리
        private void GridResults_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    // 마우스 위치의 행을 강제로 선택
                    gridResults.CurrentCell = gridResults.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    gridResults.Rows[e.RowIndex].Selected = true;

                    // 메뉴 표시
                    _ctxMenu.Show(Cursor.Position);
                }
            }
        }

        // [우선순위 4] 선택된 행/셀 복사 (Ctrl+C)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                if (gridResults.SelectedCells.Count > 0)
                {
                    // 선택된 셀의 값을 클립보드에 복사
                    var selectedCell = gridResults.SelectedCells[0];
                    if (selectedCell.Value != null)
                    {
                        Clipboard.SetText(selectedCell.Value.ToString());
                        return true;
                    }
                }
                else if (gridResults.SelectedRows.Count > 0)
                {
                    // 선택된 행 전체를 클립보드에 복사
                    var row = gridResults.SelectedRows[0];
                    var summary = row.DataBoundItem as EquipmentTraceSummary;
                    if (summary != null)
                    {
                        string rowData = $"{summary.EquipmentTag}\t{summary.CheckResult}\t{summary.CountERR}\t{summary.EquipmentCount}\t{summary.TotalPocCount}\t{summary.DerivedBranchCount}";
                        Clipboard.SetText(rowData);
                        return true;
                    }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }


        private async void btnLoadData_Click(object sender, EventArgs e)
        {
            // [우선순위 3] ProgressBar/Spinner 표시
            ShowLoading(true, "Loading data...");

            try
            {
                // 버튼을 굳이 비활성화하지 않아도 ViewModel 내부에서 IsBusy 체크를 하므로 안전함
                await _viewModel.LoadDataAsync();
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // [신규] 로딩 표시 관리 (우선순위 3)
        private void ShowLoading(bool show, string message = "")
        {
            // Spinner 찾기
            var spinners = this.Controls.Find("spinnerLoading", true);
            if (spinners.Length > 0 && spinners[0] is MetroProgressSpinner spinner)
            {
                // MetroProgressSpinner는 투명 배경을 지원하지 않음
                // 단순히 표시/숨기기만 수행
                spinner.Visible = show;
                spinner.Spinning = show;
                if (show)
                {
                    spinner.BringToFront();
                }
            }

            // 상태 메시지 업데이트
            if (!string.IsNullOrEmpty(message))
            {
                lblStatus.Text = message;
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            string searchText = txtSearch.Text?.Trim() ?? "";

            // 검색어 유효성 검사
            if (string.IsNullOrWhiteSpace(searchText))
            {
                MessageBox.Show("검색어를 입력해주세요.", "Input Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSearch.Focus();
                return;
            }

            try
            {
                // [안전장치 1] BindingSource 일시 중지
                _traceBindingSource.SuspendBinding();

                // [안전장치 2] DataGridView 데이터 소스 해제 (CurrencyManager 오류 방지)
                gridResults.DataSource = null;

                // ViewModel에 있는 검색 로직 호출
                await _viewModel.SearchAsync(searchText);

                // [안전장치 3] 데이터 소스 재연결
                _traceBindingSource.DataSource = _viewModel.TraceSummaries;
                gridResults.DataSource = _traceBindingSource;

                // [안전장치 4] BindingSource 재개 및 강제 갱신
                _traceBindingSource.ResumeBinding();
                _traceBindingSource.ResetBindings(false);

                // [신규] 최근 검색어 저장 (우선순위 2)
                AddRecentSearch(searchText);

                // 결과 없음 메시지
                if (_viewModel.TraceSummaries == null || _viewModel.TraceSummaries.Count == 0)
                {
                    MessageBox.Show($"'{searchText}'에 대한 검색 결과가 없습니다.", "No Results",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                // 예외 발생 시 복원 시도
                MessageBox.Show($"Search Error: {ex.Message}\n\n상세 로그를 확인해주세요.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 복원 시도
                try
                {
                    _traceBindingSource.ResumeBinding();
                    _traceBindingSource.DataSource = _viewModel.TraceSummaries;
                    gridResults.DataSource = _traceBindingSource;
                }
                catch { }
            }
        }

        // [신규] 최근 검색어 추가 (우선순위 2)
        private void AddRecentSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return;

            // UserSettings를 사용하여 최근 검색어 추가
            _userSettings.AddRecentSearch(searchText);

            // 내부 리스트도 업데이트
            _recentSearches.Remove(searchText);
            _recentSearches.Insert(0, searchText);

            // 최대 개수 제한
            if (_recentSearches.Count > MaxRecentSearches)
            {
                _recentSearches.RemoveAt(_recentSearches.Count - 1);
            }
        }

        // [신규] 최근 검색어 목록 가져오기
        private List<string> GetRecentSearches()
        {
            return _recentSearches.ToList();
        }

        private async void btnExport_Click(object sender, EventArgs e)
        {
            // 데이터가 없으면 경고
            if (_viewModel.TraceSummaries == null || _viewModel.TraceSummaries.Count == 0)
            {
                MessageBox.Show("No data to export.", "Warning");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                // [확인] 필터가 xlsx로 되어 있어야 함
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";

                // 파일명 기본값 설정
                // [수정 2] 파일명에 Timestamp 자동 삽입
                // 예: TraceResult_Merged_20251225_143005.xlsx
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string defaultName = _viewModel.TraceSummaries.Count > 1
                    ? $"TraceResult_Merged_{timestamp}.xlsx"
                    : $"TraceResult_{_viewModel.SelectedDb}_{_viewModel.TraceSummaries[0].EquipmentTag}_{timestamp}.xlsx";

                sfd.FileName = defaultName;

                // [중요] ShowDialog는 여기서 딱 한 번만 호출!
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // ViewModel에게는 "경로"만 전달 (ViewModel이 팝업 띄우지 않도록 주의)
                        await _viewModel.ExportToExcelAsync(sfd.FileName);
                        MessageBox.Show("Export Complete!", "Success");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Export Failed: {ex.Message}", "Error");
                    }
                    // ViewModel의 ExportToExcelAsync 호출
                    //await _viewModel.ExportToExcelAsync(sfd.FileName);
                    //MessageBox.Show("Export Complete!", "Success");
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            //btnLoadData_Click(null, e);
        }

        private async void BtnBatchTrace_Click(object sender, EventArgs e)
        {
            //await _viewModel.RunBatchTraceAsync(txtBatchInput.Text);

            // Grid 갱신 강제 (WinForms 특성상 안전장치)
            //_traceBindingSource.ResetBindings(false);

            try
            {
                string input = txtBatchInput.Text;

                // 1. [중요] 작업 전 연결 끊기 (그리드가 변경사항을 못 보게 함)
                _traceBindingSource.SuspendBinding();
                gridResults.DataSource = null; // 아예 끊어버림 (가장 확실)

                // 2. ViewModel 실행 (데이터 수집)
                await _viewModel.RunBatchTraceAsync(input);

                // 3. [중요] 작업 후 다시 연결
                _traceBindingSource.DataSource = _viewModel.TraceSummaries;
                gridResults.DataSource = _traceBindingSource;

                // 4. 강제 갱신
                _traceBindingSource.ResumeBinding();
                _traceBindingSource.ResetBindings(false);
            }
            catch (Exception ex)
            {
                try
                {
                    _traceBindingSource.DataSource = _viewModel.TraceSummaries;
                    gridResults.DataSource = _traceBindingSource;
                    _traceBindingSource.ResumeBinding();
                }
                catch { }
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        // ==========================================================
        // [신규] 사용자 설정 관련 메서드
        // ==========================================================

        /// <summary>
        /// 사용자 설정을 파일에서 불러옵니다.
        /// </summary>
        private void LoadUserSettings()
        {
            try
            {
                _userSettings = UserSettings.Load();
                System.Diagnostics.Debug.WriteLine($"User settings loaded from: {UserSettings.GetSettingsFilePath()}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
                _userSettings = new UserSettings();
            }
        }

        /// <summary>
        /// 사용자 설정을 UI에 적용합니다.
        /// </summary>
        private void ApplyUserSettings()
        {
            try
            {
                // DB 선택 콤보박스
                if (this.Controls.Find("cboDbList", true).Length > 0)
                {
                    var cbo = this.Controls.Find("cboDbList", true)[0] as ComboBox;
                    if (cbo != null && !string.IsNullOrEmpty(_userSettings.SelectedDb))
                    {
                        // 콤보박스에 해당 항목이 있는지 확인 후 선택
                        int index = cbo.Items.IndexOf(_userSettings.SelectedDb);
                        if (index >= 0)
                        {
                            cbo.SelectedIndex = index;
                            
                            // [중요] ViewModel의 SelectedDb 속성도 직접 설정
                            // SelectedIndex를 설정해도 바인딩이 자동으로 동작하지 않을 수 있음
                            _viewModel.SelectedDb = _userSettings.SelectedDb;
                            
                            System.Diagnostics.Debug.WriteLine($"Applied DB setting: {_userSettings.SelectedDb}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"DB '{_userSettings.SelectedDb}' not found in combo box");
                        }
                    }
                }

                // 검색 텍스트박스
                if (!string.IsNullOrEmpty(_userSettings.SearchText))
                {
                    txtSearch.Text = _userSettings.SearchText;
                }

                // 배치 입력 텍스트박스
                if (this.Controls.Find("txtBatchInput", true).Length > 0)
                {
                    var txt = this.Controls.Find("txtBatchInput", true)[0] as TextBoxBase;
                    if (txt != null && !string.IsNullOrEmpty(_userSettings.BatchInputText))
                    {
                        txt.Text = _userSettings.BatchInputText;
                    }
                }

                // 속성 리스트 콤보박스 (Controls.Find로 안전하게 찾기)
                var cboProp2 = this.Controls.Find("cboPropList", true).FirstOrDefault() as ComboBox;
                if (cboProp2 != null && _userSettings.SelectedPropListIndex >= 0 &&
                    _userSettings.SelectedPropListIndex < cboProp2.Items.Count)
                {
                    cboProp2.SelectedIndex = _userSettings.SelectedPropListIndex;
                    cboProp2.SelectedIndex = _userSettings.SelectedPropListIndex;
                }
                if (_userSettings.RecentSearches != null && _userSettings.RecentSearches.Count > 0)
                {
                    _recentSearches = new List<string>(_userSettings.RecentSearches);

                    var autoCompSource = new AutoCompleteStringCollection();
                    autoCompSource.AddRange(_recentSearches.ToArray());
                    txtSearch.AutoCompleteCustomSource = autoCompSource;
                }

                // 폼 크기 및 위치
                if (_userSettings.FormWidth > 0 && _userSettings.FormHeight > 0)
                {
                    this.Width = _userSettings.FormWidth;
                    this.Height = _userSettings.FormHeight;
                }

                if (_userSettings.FormLocationX >= 0 && _userSettings.FormLocationY >= 0)
                {
                    this.Location = new Point(_userSettings.FormLocationX, _userSettings.FormLocationY);
                }

                // 폼 최대화 상태
                if (_userSettings.FormMaximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }

                System.Diagnostics.Debug.WriteLine("User settings applied successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply user settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 UI 상태를 사용자 설정에 저장합니다.
        /// </summary>
        private void SaveUserSettings()
        {
            try
            {
                // DB 선택 콤보박스
                if (this.Controls.Find("cboDbList", true).Length > 0)
                {
                    var cbo = this.Controls.Find("cboDbList", true)[0] as ComboBox;
                    if (cbo != null && cbo.SelectedItem != null)
                    {
                        _userSettings.SelectedDb = cbo.SelectedItem.ToString();
                    }
                }

                // 검색 텍스트박스
                _userSettings.SearchText = txtSearch.Text ?? "";

                // 배치 입력 텍스트박스
                if (this.Controls.Find("txtBatchInput", true).Length > 0)
                {
                    var txt = this.Controls.Find("txtBatchInput", true)[0] as TextBoxBase;
                    if (txt != null)
                    {
                        _userSettings.BatchInputText = txt.Text ?? "";
                    }
                }

                // 속성 리스트 콤보박스 (Controls.Find로 안전하게 찾기)
                var cboProp3 = this.Controls.Find("cboPropList", true).FirstOrDefault() as ComboBox;
                if (cboProp3 != null)
                {
                    _userSettings.SelectedPropListIndex = cboProp3.SelectedIndex;
                }
                // 최근 검색어 목록
                _userSettings.RecentSearches = new List<string>(_recentSearches);

                // 폼 크기 및 위치 (최대화 상태가 아닐 때만 저장)
                if (this.WindowState != FormWindowState.Maximized)
                {
                    _userSettings.FormWidth = this.Width;
                    _userSettings.FormHeight = this.Height;
                    _userSettings.FormLocationX = this.Location.X;
                    _userSettings.FormLocationY = this.Location.Y;
                    _userSettings.FormMaximized = false;
                }
                else
                {
                    _userSettings.FormMaximized = true;
                }

                // 설정 파일에 저장
                _userSettings.Save();

                System.Diagnostics.Debug.WriteLine($"User settings saved to: {UserSettings.GetSettingsFilePath()}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save user settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 폼이 닫힐 때 설정을 저장합니다.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveUserSettings();
        }

        /// <summary>
        /// 폼 크기가 변경될 때 설정을 저장합니다.
        /// </summary>
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // 최대화/최소화 상태 변경 시 저장
            if (this.WindowState == FormWindowState.Normal)
            {
                _userSettings.FormWidth = this.Width;
                _userSettings.FormHeight = this.Height;
                _userSettings.FormMaximized = false;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                _userSettings.FormMaximized = true;
            }
        }

        /// <summary>
        /// 폼이 이동할 때 설정을 저장합니다.
        /// </summary>
        private void MainForm_Move(object sender, EventArgs e)
        {
            // 일반 상태일 때만 위치 저장
            if (this.WindowState == FormWindowState.Normal)
            {
                _userSettings.FormLocationX = this.Location.X;
                _userSettings.FormLocationY = this.Location.Y;
            }
        }
    }
}




