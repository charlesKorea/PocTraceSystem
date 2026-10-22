namespace PocTrace.UI
{
    partial class MainForm
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// [Refactor] Bunifu 의존을 제거하고 MetroFramework 컨트롤로 재작성한 코드-정의 레이아웃.
        /// 컨트롤 Name/위치/크기/이벤트 배선은 기존과 동일하게 유지하여 MainForm.cs는 무수정으로 동작한다.
        /// 참고: 컨트롤이 절대좌표 기반이라 기존 시각 배치를 최대한 보존했다. (VS 디자이너에서 최종 확인 권장)
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.panelInput = new MetroFramework.Controls.MetroPanel();
            this.cboDbList = new MetroFramework.Controls.MetroComboBox();
            this.cboPropList = new MetroFramework.Controls.MetroComboBox();
            this.btnLoadData = new MetroFramework.Controls.MetroButton();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.btnSearch = new MetroFramework.Controls.MetroButton();
            this.txtBatchInput = new System.Windows.Forms.RichTextBox();
            this.btnBatchTrace = new MetroFramework.Controls.MetroButton();
            this.btnExport = new MetroFramework.Controls.MetroButton();
            this.panelGrid = new MetroFramework.Controls.MetroPanel();
            this.gridResults = new MetroFramework.Controls.MetroGrid();
            this.spinnerLoading = new MetroFramework.Controls.MetroProgressSpinner();
            this.panelStatus = new MetroFramework.Controls.MetroPanel();
            this.lblStatus = new MetroFramework.Controls.MetroLabel();
            this.lblCacheStatus = new MetroFramework.Controls.MetroLabel();
            this.panelInput.SuspendLayout();
            this.panelGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridResults)).BeginInit();
            this.panelStatus.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelInput
            // 
            this.panelInput.Controls.Add(this.cboDbList);
            this.panelInput.Controls.Add(this.cboPropList);
            this.panelInput.Controls.Add(this.btnLoadData);
            this.panelInput.Controls.Add(this.txtSearch);
            this.panelInput.Controls.Add(this.btnSearch);
            this.panelInput.Controls.Add(this.txtBatchInput);
            this.panelInput.Controls.Add(this.btnBatchTrace);
            this.panelInput.Controls.Add(this.btnExport);
            this.panelInput.HorizontalScrollbarBarColor = true;
            this.panelInput.HorizontalScrollbarHighlightOnWheel = false;
            this.panelInput.HorizontalScrollbarSize = 10;
            this.panelInput.Location = new System.Drawing.Point(11, 48);
            this.panelInput.Name = "panelInput";
            this.panelInput.Size = new System.Drawing.Size(1025, 128);
            this.panelInput.TabIndex = 10;
            this.panelInput.VerticalScrollbarBarColor = true;
            this.panelInput.VerticalScrollbarHighlightOnWheel = false;
            this.panelInput.VerticalScrollbarSize = 10;
            // 
            // cboDbList
            // 
            this.cboDbList.FormattingEnabled = true;
            this.cboDbList.ItemHeight = 23;
            this.cboDbList.Items.AddRange(new object[] {
            "X1, X2, X2_2"});
            this.cboDbList.Location = new System.Drawing.Point(22, 27);
            this.cboDbList.Name = "cboDbList";
            this.cboDbList.Size = new System.Drawing.Size(96, 29);
            this.cboDbList.TabIndex = 0;
            this.cboDbList.UseSelectable = true;
            // 
            // cboPropList
            // 
            this.cboPropList.FormattingEnabled = true;
            this.cboPropList.ItemHeight = 23;
            this.cboPropList.Items.AddRange(new object[] {
            "EQPID"});
            this.cboPropList.Location = new System.Drawing.Point(22, 80);
            this.cboPropList.Name = "cboPropList";
            this.cboPropList.Size = new System.Drawing.Size(141, 29);
            this.cboPropList.TabIndex = 1;
            this.cboPropList.UseSelectable = true;
            // 
            // btnLoadData
            // 
            this.btnLoadData.Location = new System.Drawing.Point(129, 27);
            this.btnLoadData.Name = "btnLoadData";
            this.btnLoadData.Size = new System.Drawing.Size(190, 27);
            this.btnLoadData.Style = MetroFramework.MetroColorStyle.Blue;
            this.btnLoadData.TabIndex = 2;
            this.btnLoadData.Text = "📜 Load Data";
            this.btnLoadData.UseSelectable = true;
            this.btnLoadData.Click += new System.EventHandler(this.btnLoadData_Click);
            // 
            // txtSearch
            // 
            this.txtSearch.Location = new System.Drawing.Point(168, 82);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(168, 21);
            this.txtSearch.TabIndex = 3;
            // 
            // btnSearch
            // 
            this.btnSearch.Location = new System.Drawing.Point(342, 80);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(185, 27);
            this.btnSearch.Style = MetroFramework.MetroColorStyle.Blue;
            this.btnSearch.TabIndex = 4;
            this.btnSearch.Text = "🚀 Single Trace";
            this.btnSearch.UseSelectable = true;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // txtBatchInput
            // 
            this.txtBatchInput.Location = new System.Drawing.Point(543, 21);
            this.txtBatchInput.Name = "txtBatchInput";
            this.txtBatchInput.Size = new System.Drawing.Size(287, 97);
            this.txtBatchInput.TabIndex = 5;
            this.txtBatchInput.Text = "";
            // 
            // btnBatchTrace
            // 
            this.btnBatchTrace.Location = new System.Drawing.Point(846, 21);
            this.btnBatchTrace.Name = "btnBatchTrace";
            this.btnBatchTrace.Size = new System.Drawing.Size(168, 27);
            this.btnBatchTrace.Style = MetroFramework.MetroColorStyle.Blue;
            this.btnBatchTrace.TabIndex = 6;
            this.btnBatchTrace.Text = "🔥 Batch Trace";
            this.btnBatchTrace.UseSelectable = true;
            this.btnBatchTrace.Click += new System.EventHandler(this.BtnBatchTrace_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(846, 85);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(162, 27);
            this.btnExport.Style = MetroFramework.MetroColorStyle.Green;
            this.btnExport.TabIndex = 7;
            this.btnExport.Text = "💾 Export Excel";
            this.btnExport.UseSelectable = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // panelGrid
            // 
            this.panelGrid.Controls.Add(this.gridResults);
            this.panelGrid.Controls.Add(this.spinnerLoading);
            this.panelGrid.HorizontalScrollbarBarColor = true;
            this.panelGrid.HorizontalScrollbarHighlightOnWheel = false;
            this.panelGrid.HorizontalScrollbarSize = 10;
            this.panelGrid.Location = new System.Drawing.Point(11, 181);
            this.panelGrid.Name = "panelGrid";
            this.panelGrid.Size = new System.Drawing.Size(1025, 379);
            this.panelGrid.TabIndex = 8;
            this.panelGrid.VerticalScrollbarBarColor = true;
            this.panelGrid.VerticalScrollbarHighlightOnWheel = false;
            this.panelGrid.VerticalScrollbarSize = 10;
            // 
            // gridResults
            // 
            this.gridResults.AllowUserToAddRows = false;
            this.gridResults.AllowUserToDeleteRows = false;
            this.gridResults.AllowUserToResizeRows = false;
            this.gridResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridResults.BackgroundColor = System.Drawing.Color.White;
            this.gridResults.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gridResults.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            this.gridResults.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(174)))), ((int)(((byte)(219)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(198)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(17)))), ((int)(((byte)(17)))));
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridResults.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(136)))), ((int)(((byte)(136)))), ((int)(((byte)(136)))));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(198)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(17)))), ((int)(((byte)(17)))));
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridResults.DefaultCellStyle = dataGridViewCellStyle2;
            this.gridResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridResults.EnableHeadersVisualStyles = false;
            this.gridResults.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.gridResults.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(221)))), ((int)(((byte)(238)))), ((int)(((byte)(255)))));
            this.gridResults.Location = new System.Drawing.Point(0, 0);
            this.gridResults.Name = "gridResults";
            this.gridResults.ReadOnly = true;
            this.gridResults.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(174)))), ((int)(((byte)(219)))));
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(198)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(17)))), ((int)(((byte)(17)))));
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridResults.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.gridResults.RowHeadersVisible = false;
            this.gridResults.RowHeadersWidth = 51;
            this.gridResults.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.gridResults.RowTemplate.Height = 32;
            this.gridResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridResults.Size = new System.Drawing.Size(1025, 379);
            this.gridResults.TabIndex = 8;
            // 
            // spinnerLoading
            // 
            this.spinnerLoading.Location = new System.Drawing.Point(387, 113);
            this.spinnerLoading.Maximum = 100;
            this.spinnerLoading.Name = "spinnerLoading";
            this.spinnerLoading.Size = new System.Drawing.Size(182, 137);
            this.spinnerLoading.Spinning = false;
            this.spinnerLoading.TabIndex = 9;
            this.spinnerLoading.UseSelectable = true;
            this.spinnerLoading.Visible = false;
            // 
            // panelStatus
            // 
            this.panelStatus.Controls.Add(this.lblStatus);
            this.panelStatus.Controls.Add(this.lblCacheStatus);
            this.panelStatus.HorizontalScrollbarBarColor = true;
            this.panelStatus.HorizontalScrollbarHighlightOnWheel = false;
            this.panelStatus.HorizontalScrollbarSize = 10;
            this.panelStatus.Location = new System.Drawing.Point(11, 565);
            this.panelStatus.Name = "panelStatus";
            this.panelStatus.Size = new System.Drawing.Size(1025, 59);
            this.panelStatus.TabIndex = 11;
            this.panelStatus.VerticalScrollbarBarColor = true;
            this.panelStatus.VerticalScrollbarHighlightOnWheel = false;
            this.panelStatus.VerticalScrollbarSize = 10;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.FontSize = MetroFramework.MetroLabelSize.Tall;
            this.lblStatus.Location = new System.Drawing.Point(17, 19);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(119, 25);
            this.lblStatus.Style = MetroFramework.MetroColorStyle.Blue;
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Current Status";
            // 
            // lblCacheStatus
            // 
            this.lblCacheStatus.AutoSize = true;
            this.lblCacheStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblCacheStatus.Location = new System.Drawing.Point(300, 21);
            this.lblCacheStatus.Name = "lblCacheStatus";
            this.lblCacheStatus.Size = new System.Drawing.Size(122, 19);
            this.lblCacheStatus.TabIndex = 1;
            this.lblCacheStatus.Text = "Cache: Not Loaded";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1051, 635);
            this.Controls.Add(this.panelStatus);
            this.Controls.Add(this.panelInput);
            this.Controls.Add(this.panelGrid);
            this.Name = "MainForm";
            this.Padding = new System.Windows.Forms.Padding(14, 60, 14, 13);
            this.Text = "PocTrace Viewer";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.panelInput.ResumeLayout(false);
            this.panelInput.PerformLayout();
            this.panelGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridResults)).EndInit();
            this.panelStatus.ResumeLayout(false);
            this.panelStatus.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private MetroFramework.Controls.MetroPanel panelInput;
        private MetroFramework.Controls.MetroPanel panelGrid;
        private MetroFramework.Controls.MetroPanel panelStatus;
        private MetroFramework.Controls.MetroComboBox cboDbList;
        private MetroFramework.Controls.MetroComboBox cboPropList;
        private MetroFramework.Controls.MetroButton btnLoadData;
        private MetroFramework.Controls.MetroButton btnSearch;
        private MetroFramework.Controls.MetroButton btnBatchTrace;
        private MetroFramework.Controls.MetroButton btnExport;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.RichTextBox txtBatchInput;
        private MetroFramework.Controls.MetroGrid gridResults;
        private MetroFramework.Controls.MetroProgressSpinner spinnerLoading;
        private MetroFramework.Controls.MetroLabel lblStatus;
        private MetroFramework.Controls.MetroLabel lblCacheStatus;
    }
}
