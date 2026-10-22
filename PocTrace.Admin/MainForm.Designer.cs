namespace PocTrace.Admin

{

    partial class MainForm

    {

        private System.ComponentModel.IContainer components = null;



        protected override void Dispose(bool disposing)

        {

            if (disposing && (components != null))

            {

                components.Dispose();

            }

            base.Dispose(disposing);

        }



        #region Windows Form Designer generated code



        private void InitializeComponent()

        {

            this.btnStart = new System.Windows.Forms.Button();

            this.btnStop = new System.Windows.Forms.Button();

            this.btnRunNow = new System.Windows.Forms.Button();

            this.btnCacheOnly = new System.Windows.Forms.Button();

            this.btnSchemaProbe = new System.Windows.Forms.Button();

            this.lblStatus = new System.Windows.Forms.Label();

            this.lblStepTime = new System.Windows.Forms.Label();

            this.progressBarMain = new System.Windows.Forms.ProgressBar();

            this.txtLog = new System.Windows.Forms.TextBox();

            this.SuspendLayout();

            // 

            // btnStart

            // 

            this.btnStart.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.btnStart.ForeColor = System.Drawing.Color.ForestGreen;

            this.btnStart.Location = new System.Drawing.Point(16, 16);

            this.btnStart.Name = "btnStart";

            this.btnStart.Size = new System.Drawing.Size(140, 48);

            this.btnStart.TabIndex = 0;

            this.btnStart.Text = "Start (02:00)";

            this.btnStart.UseVisualStyleBackColor = true;

            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);

            // 

            // btnStop

            // 

            this.btnStop.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.btnStop.ForeColor = System.Drawing.Color.IndianRed;

            this.btnStop.Location = new System.Drawing.Point(164, 16);

            this.btnStop.Name = "btnStop";

            this.btnStop.Size = new System.Drawing.Size(120, 48);

            this.btnStop.TabIndex = 1;

            this.btnStop.Text = "Stop";

            this.btnStop.UseVisualStyleBackColor = true;

            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);

            // 

            // btnRunNow

            // 

            this.btnRunNow.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.btnRunNow.ForeColor = System.Drawing.Color.DarkCyan;

            this.btnRunNow.Location = new System.Drawing.Point(292, 16);

            this.btnRunNow.Name = "btnRunNow";

            this.btnRunNow.Size = new System.Drawing.Size(160, 48);

            this.btnRunNow.TabIndex = 2;

            this.btnRunNow.Text = "Run Full Batch";

            this.btnRunNow.UseVisualStyleBackColor = true;

            this.btnRunNow.Click += new System.EventHandler(this.BtnRunNow_Click);

            // 

            // btnCacheOnly

            // 

            this.btnCacheOnly.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.btnCacheOnly.ForeColor = System.Drawing.Color.DarkOrange;

            this.btnCacheOnly.Location = new System.Drawing.Point(460, 16);

            this.btnCacheOnly.Name = "btnCacheOnly";

            this.btnCacheOnly.Size = new System.Drawing.Size(180, 48);

            this.btnCacheOnly.TabIndex = 3;

            this.btnCacheOnly.Text = "Cache Only (DB→bin)";

            this.btnCacheOnly.UseVisualStyleBackColor = true;

            this.btnCacheOnly.Click += new System.EventHandler(this.BtnCacheOnly_Click);

            // 

            // btnSchemaProbe

            // 

            this.btnSchemaProbe.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.btnSchemaProbe.ForeColor = System.Drawing.Color.DarkSlateBlue;

            this.btnSchemaProbe.Location = new System.Drawing.Point(648, 16);

            this.btnSchemaProbe.Name = "btnSchemaProbe";

            this.btnSchemaProbe.Size = new System.Drawing.Size(220, 48);

            this.btnSchemaProbe.TabIndex = 8;

            this.btnSchemaProbe.Text = "사전점검 (엑셀)";

            this.btnSchemaProbe.UseVisualStyleBackColor = true;

            this.btnSchemaProbe.Click += new System.EventHandler(this.BtnSchemaProbe_Click);

            // 

            // lblStatus

            // 

            this.lblStatus.AutoSize = false;

            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.lblStatus.Location = new System.Drawing.Point(16, 76);

            this.lblStatus.Name = "lblStatus";

            this.lblStatus.Size = new System.Drawing.Size(868, 24);

            this.lblStatus.TabIndex = 4;

            this.lblStatus.Text = "대기 중";

            // 

            // progressBarMain

            // 

            this.progressBarMain.Location = new System.Drawing.Point(16, 104);

            this.progressBarMain.Name = "progressBarMain";

            this.progressBarMain.Size = new System.Drawing.Size(868, 24);

            this.progressBarMain.TabIndex = 5;

            // 

            // lblStepTime

            // 

            this.lblStepTime.AutoSize = false;

            this.lblStepTime.Font = new System.Drawing.Font("Segoe UI", 9F);

            this.lblStepTime.ForeColor = System.Drawing.SystemColors.GrayText;

            this.lblStepTime.Location = new System.Drawing.Point(16, 132);

            this.lblStepTime.Name = "lblStepTime";

            this.lblStepTime.Size = new System.Drawing.Size(868, 20);

            this.lblStepTime.TabIndex = 6;

            this.lblStepTime.Text = "단계 소요: —";

            // 

            // txtLog

            // 

            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)

            | System.Windows.Forms.AnchorStyles.Left)

            | System.Windows.Forms.AnchorStyles.Right)));

            this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);

            this.txtLog.Location = new System.Drawing.Point(16, 160);

            this.txtLog.Multiline = true;

            this.txtLog.Name = "txtLog";

            this.txtLog.ReadOnly = true;

            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;

            this.txtLog.Size = new System.Drawing.Size(868, 320);

            this.txtLog.TabIndex = 7;

            // 

            // MainForm

            // 

            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);

            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

            this.ClientSize = new System.Drawing.Size(884, 501);

            this.Controls.Add(this.txtLog);

            this.Controls.Add(this.lblStepTime);

            this.Controls.Add(this.progressBarMain);

            this.Controls.Add(this.lblStatus);

            this.Controls.Add(this.btnSchemaProbe);

            this.Controls.Add(this.btnCacheOnly);

            this.Controls.Add(this.btnRunNow);

            this.Controls.Add(this.btnStop);

            this.Controls.Add(this.btnStart);

            this.MinimumSize = new System.Drawing.Size(900, 540);

            this.Name = "MainForm";

            this.Text = "PocTrace Admin";

            this.ResumeLayout(false);

            this.PerformLayout();

        }



        #endregion



        private System.Windows.Forms.Button btnStart;

        private System.Windows.Forms.Button btnStop;

        private System.Windows.Forms.Button btnRunNow;

        private System.Windows.Forms.Button btnCacheOnly;

        private System.Windows.Forms.Button btnSchemaProbe;

        private System.Windows.Forms.Label lblStatus;

        private System.Windows.Forms.Label lblStepTime;

        private System.Windows.Forms.ProgressBar progressBarMain;

        private System.Windows.Forms.TextBox txtLog;

    }

}


