namespace WinFormsOffline.UI
{
  partial class MainForm
  {
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Button btnRun;
    private System.Windows.Forms.Button btnOpenOut;
    private System.Windows.Forms.TextBox txtHint;
    private System.Windows.Forms.Label lblHint;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Label lblTitle;

    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
        components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.btnRun = new System.Windows.Forms.Button();
      this.btnOpenOut = new System.Windows.Forms.Button();
      this.txtHint = new System.Windows.Forms.TextBox();
      this.lblHint = new System.Windows.Forms.Label();
      this.lblStatus = new System.Windows.Forms.Label();
      this.lblTitle = new System.Windows.Forms.Label();
      this.SuspendLayout();
      //
      // lblTitle
      //
      this.lblTitle.AutoSize = true;
      this.lblTitle.Font = new System.Drawing.Font("Malgun Gothic", 12F, System.Drawing.FontStyle.Bold);
      this.lblTitle.Location = new System.Drawing.Point(16, 16);
      this.lblTitle.Name = "lblTitle";
      this.lblTitle.Size = new System.Drawing.Size(280, 21);
      this.lblTitle.Text = "WinForms 오프라인 템플릿 (.NET 4.8)";
      //
      // lblHint
      //
      this.lblHint.AutoSize = true;
      this.lblHint.Location = new System.Drawing.Point(18, 56);
      this.lblHint.Name = "lblHint";
      this.lblHint.Size = new System.Drawing.Size(41, 12);
      this.lblHint.Text = "메모";
      //
      // txtHint
      //
      this.txtHint.Location = new System.Drawing.Point(20, 76);
      this.txtHint.Name = "txtHint";
      this.txtHint.Size = new System.Drawing.Size(440, 21);
      this.txtHint.TabIndex = 0;
      //
      // btnRun
      //
      this.btnRun.Location = new System.Drawing.Point(20, 112);
      this.btnRun.Name = "btnRun";
      this.btnRun.Size = new System.Drawing.Size(140, 32);
      this.btnRun.TabIndex = 1;
      this.btnRun.Text = "샘플 작업 + Excel/HTML";
      this.btnRun.UseVisualStyleBackColor = true;
      this.btnRun.Click += new System.EventHandler(this.BtnRun_Click);
      //
      // btnOpenOut
      //
      this.btnOpenOut.Location = new System.Drawing.Point(176, 112);
      this.btnOpenOut.Name = "btnOpenOut";
      this.btnOpenOut.Size = new System.Drawing.Size(120, 32);
      this.btnOpenOut.TabIndex = 2;
      this.btnOpenOut.Text = "출력 폴더";
      this.btnOpenOut.UseVisualStyleBackColor = true;
      this.btnOpenOut.Click += new System.EventHandler(this.BtnOpenOut_Click);
      //
      // lblStatus
      //
      this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
      this.lblStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      this.lblStatus.Location = new System.Drawing.Point(20, 160);
      this.lblStatus.Name = "lblStatus";
      this.lblStatus.Padding = new System.Windows.Forms.Padding(8);
      this.lblStatus.Size = new System.Drawing.Size(540, 180);
      this.lblStatus.Text = "NuGet: C:\\OfflineDev\\nuget-packages\r\nWeb: C:\\OfflineDev\\web-assets\r\n가이드: offline-kit\\templates\\WinFormsNet48\\README.md";
      //
      // MainForm
      //
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(584, 361);
      this.Controls.Add(this.lblStatus);
      this.Controls.Add(this.btnOpenOut);
      this.Controls.Add(this.btnRun);
      this.Controls.Add(this.txtHint);
      this.Controls.Add(this.lblHint);
      this.Controls.Add(this.lblTitle);
      this.MinimumSize = new System.Drawing.Size(500, 320);
      this.Name = "MainForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "WinForms Offline Template";
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
