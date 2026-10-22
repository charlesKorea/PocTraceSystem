namespace PocTrace.NozzleTrace
{
  partial class MainForm
  {
    private System.ComponentModel.IContainer components = null;
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
        components.Dispose();
      base.Dispose(disposing);
    }
    private void InitializeComponent()
    {
      this.cmbDb = new System.Windows.Forms.ComboBox();
      this.lblDb = new System.Windows.Forms.Label();
      this.btnLoadCache = new System.Windows.Forms.Button();
      this.lblCacheStatus = new System.Windows.Forms.Label();
      this.lblKey = new System.Windows.Forms.Label();
      this.txtKey = new System.Windows.Forms.TextBox();
      this.rbBimId = new System.Windows.Forms.RadioButton();
      this.rbNumericId = new System.Windows.Forms.RadioButton();
      this.rbNodeGuid = new System.Windows.Forms.RadioButton();
      this.btnRun = new System.Windows.Forms.Button();
      this.btnUpload = new System.Windows.Forms.Button();
      this.btnTemplate = new System.Windows.Forms.Button();
      this.btnOpenOutput = new System.Windows.Forms.Button();
      this.lblStatus = new System.Windows.Forms.Label();
      this.progressBar = new System.Windows.Forms.ProgressBar();
      this.txtLog = new System.Windows.Forms.TextBox();
      this.lblLog = new System.Windows.Forms.Label();
      this.SuspendLayout();
      //
      // lblDb
      //
      this.lblDb.AutoSize = true;
      this.lblDb.Location = new System.Drawing.Point(16, 18);
      this.lblDb.Name = "lblDb";
      this.lblDb.Size = new System.Drawing.Size(25, 15);
      this.lblDb.Text = "DB";
      //
      // cmbDb
      //
      this.cmbDb.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.cmbDb.Location = new System.Drawing.Point(56, 14);
      this.cmbDb.Name = "cmbDb";
      this.cmbDb.Size = new System.Drawing.Size(120, 23);
      this.cmbDb.SelectedIndexChanged += new System.EventHandler(this.cmbDb_SelectedIndexChanged);
      //
      // btnLoadCache
      //
      this.btnLoadCache.Location = new System.Drawing.Point(192, 12);
      this.btnLoadCache.Name = "btnLoadCache";
      this.btnLoadCache.Size = new System.Drawing.Size(120, 28);
      this.btnLoadCache.Text = "캐시 로드";
      this.btnLoadCache.UseVisualStyleBackColor = true;
      this.btnLoadCache.Click += new System.EventHandler(this.btnLoadCache_Click);
      //
      // lblCacheStatus
      //
      this.lblCacheStatus.AutoEllipsis = true;
      this.lblCacheStatus.Location = new System.Drawing.Point(16, 44);
      this.lblCacheStatus.Name = "lblCacheStatus";
      this.lblCacheStatus.Size = new System.Drawing.Size(400, 36);
      this.lblCacheStatus.Text = "캐시: 미로드";
      //
      // lblKey
      //
      this.lblKey.AutoSize = true;
      this.lblKey.Location = new System.Drawing.Point(16, 88);
      this.lblKey.Name = "lblKey";
      this.lblKey.Size = new System.Drawing.Size(28, 15);
      this.lblKey.Text = "Key";
      //
      // txtKey
      //
      this.txtKey.Enabled = false;
      this.txtKey.Location = new System.Drawing.Point(56, 84);
      this.txtKey.Name = "txtKey";
      this.txtKey.Size = new System.Drawing.Size(360, 23);
      //
      // rbBimId
      //
      this.rbBimId.AutoSize = true;
      this.rbBimId.Checked = true;
      this.rbBimId.Enabled = false;
      this.rbBimId.Location = new System.Drawing.Point(56, 116);
      this.rbBimId.Name = "rbBimId";
      this.rbBimId.Size = new System.Drawing.Size(58, 19);
      this.rbBimId.TabStop = true;
      this.rbBimId.Text = "BIM ID";
      //
      // rbNumericId
      //
      this.rbNumericId.AutoSize = true;
      this.rbNumericId.Enabled = false;
      this.rbNumericId.Location = new System.Drawing.Point(128, 116);
      this.rbNumericId.Name = "rbNumericId";
      this.rbNumericId.Size = new System.Drawing.Size(103, 19);
      this.rbNumericId.Text = "Instance ID";
      //
      // rbNodeGuid
      //
      this.rbNodeGuid.AutoSize = true;
      this.rbNodeGuid.Enabled = false;
      this.rbNodeGuid.Location = new System.Drawing.Point(248, 116);
      this.rbNodeGuid.Name = "rbNodeGuid";
      this.rbNodeGuid.Size = new System.Drawing.Size(86, 19);
      this.rbNodeGuid.Text = "Node GUID";
      //
      // btnRun
      //
      this.btnRun.Enabled = false;
      this.btnRun.Location = new System.Drawing.Point(16, 148);
      this.btnRun.Name = "btnRun";
      this.btnRun.Size = new System.Drawing.Size(100, 36);
      this.btnRun.Text = "단건 실행";
      this.btnRun.UseVisualStyleBackColor = true;
      this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
      //
      // btnUpload
      //
      this.btnUpload.Enabled = false;
      this.btnUpload.Location = new System.Drawing.Point(124, 148);
      this.btnUpload.Name = "btnUpload";
      this.btnUpload.Size = new System.Drawing.Size(100, 36);
      this.btnUpload.Text = "Excel 업로드";
      this.btnUpload.UseVisualStyleBackColor = true;
      this.btnUpload.Click += new System.EventHandler(this.btnUpload_Click);
      //
      // btnTemplate
      //
      this.btnTemplate.Location = new System.Drawing.Point(232, 148);
      this.btnTemplate.Name = "btnTemplate";
      this.btnTemplate.Size = new System.Drawing.Size(90, 36);
      this.btnTemplate.Text = "양식 받기";
      this.btnTemplate.UseVisualStyleBackColor = true;
      this.btnTemplate.Click += new System.EventHandler(this.btnTemplate_Click);
      //
      // btnOpenOutput
      //
      this.btnOpenOutput.Enabled = false;
      this.btnOpenOutput.Location = new System.Drawing.Point(330, 148);
      this.btnOpenOutput.Name = "btnOpenOutput";
      this.btnOpenOutput.Size = new System.Drawing.Size(86, 36);
      this.btnOpenOutput.Text = "결과 폴더";
      this.btnOpenOutput.UseVisualStyleBackColor = true;
      this.btnOpenOutput.Click += new System.EventHandler(this.btnOpenOutput_Click);
      //
      // progressBar
      //
      this.progressBar.Location = new System.Drawing.Point(16, 196);
      this.progressBar.Name = "progressBar";
      this.progressBar.Size = new System.Drawing.Size(400, 18);
      //
      // lblStatus
      //
      this.lblStatus.AutoEllipsis = true;
      this.lblStatus.Location = new System.Drawing.Point(16, 220);
      this.lblStatus.Name = "lblStatus";
      this.lblStatus.Size = new System.Drawing.Size(400, 32);
      this.lblStatus.Text = "캐시를 먼저 로드하세요.";
      //
      // lblLog
      //
      this.lblLog.AutoSize = true;
      this.lblLog.Location = new System.Drawing.Point(16, 258);
      this.lblLog.Name = "lblLog";
      this.lblLog.Size = new System.Drawing.Size(28, 15);
      this.lblLog.Text = "로그";
      //
      // txtLog
      //
      this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
      this.txtLog.BackColor = System.Drawing.SystemColors.Window;
      this.txtLog.Location = new System.Drawing.Point(16, 276);
      this.txtLog.Multiline = true;
      this.txtLog.Name = "txtLog";
      this.txtLog.ReadOnly = true;
      this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this.txtLog.Size = new System.Drawing.Size(400, 120);
      this.txtLog.TabStop = false;
      //
      // MainForm
      //
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(434, 412);
      this.Controls.Add(this.txtLog);
      this.Controls.Add(this.lblLog);
      this.Controls.Add(this.lblStatus);
      this.Controls.Add(this.progressBar);
      this.Controls.Add(this.btnOpenOutput);
      this.Controls.Add(this.btnTemplate);
      this.Controls.Add(this.btnUpload);
      this.Controls.Add(this.btnRun);
      this.Controls.Add(this.rbNodeGuid);
      this.Controls.Add(this.rbNumericId);
      this.Controls.Add(this.rbBimId);
      this.Controls.Add(this.txtKey);
      this.Controls.Add(this.lblKey);
      this.Controls.Add(this.lblCacheStatus);
      this.Controls.Add(this.btnLoadCache);
      this.Controls.Add(this.cmbDb);
      this.Controls.Add(this.lblDb);
      this.Font = new System.Drawing.Font("Segoe UI", 9F);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
      this.MaximizeBox = false;
      this.MinimumSize = new System.Drawing.Size(450, 450);
      this.Name = "MainForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "PocTrace Nozzle Trace";
      this.ResumeLayout(false);
      this.PerformLayout();
    }
    private System.Windows.Forms.ComboBox cmbDb;
    private System.Windows.Forms.Label lblDb;
    private System.Windows.Forms.Button btnLoadCache;
    private System.Windows.Forms.Label lblCacheStatus;
    private System.Windows.Forms.Label lblKey;
    private System.Windows.Forms.TextBox txtKey;
    private System.Windows.Forms.RadioButton rbBimId;
    private System.Windows.Forms.RadioButton rbNumericId;
    private System.Windows.Forms.RadioButton rbNodeGuid;
    private System.Windows.Forms.Button btnRun;
    private System.Windows.Forms.Button btnUpload;
    private System.Windows.Forms.Button btnTemplate;
    private System.Windows.Forms.Button btnOpenOutput;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.TextBox txtLog;
    private System.Windows.Forms.Label lblLog;
  }
}
