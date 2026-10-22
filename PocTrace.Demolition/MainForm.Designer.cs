namespace PocTrace.Demolition
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

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
      this.pnlTop = new System.Windows.Forms.Panel();
      this.btnRun = new System.Windows.Forms.Button();
      this.btnBrowse = new System.Windows.Forms.Button();
      this.txtInputPath = new System.Windows.Forms.TextBox();
      this.lblInput = new System.Windows.Forms.Label();
      this.gridInput = new System.Windows.Forms.DataGridView();
      this.pnlBottom = new System.Windows.Forms.Panel();
      this.progressBar = new System.Windows.Forms.ProgressBar();
      this.lblPercent = new System.Windows.Forms.Label();
      this.lblEta = new System.Windows.Forms.Label();
      this.lblStatus = new System.Windows.Forms.Label();
      this.pnlTop.SuspendLayout();
      this.pnlBottom.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.gridInput)).BeginInit();
      this.SuspendLayout();
      //
      // pnlTop
      //
      this.pnlTop.Controls.Add(this.btnRun);
      this.pnlTop.Controls.Add(this.btnBrowse);
      this.pnlTop.Controls.Add(this.txtInputPath);
      this.pnlTop.Controls.Add(this.lblInput);
      this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
      this.pnlTop.Location = new System.Drawing.Point(0, 0);
      this.pnlTop.Name = "pnlTop";
      this.pnlTop.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
      this.pnlTop.Size = new System.Drawing.Size(984, 72);
      this.pnlTop.TabIndex = 0;
      //
      // lblInput
      //
      this.lblInput.AutoSize = true;
      this.lblInput.Location = new System.Drawing.Point(12, 16);
      this.lblInput.Name = "lblInput";
      this.lblInput.Size = new System.Drawing.Size(89, 15);
      this.lblInput.TabIndex = 0;
      this.lblInput.Text = "Input Excel";
      //
      // txtInputPath
      //
      this.txtInputPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
      this.txtInputPath.Location = new System.Drawing.Point(107, 12);
      this.txtInputPath.Name = "txtInputPath";
      this.txtInputPath.ReadOnly = true;
      this.txtInputPath.Size = new System.Drawing.Size(620, 23);
      this.txtInputPath.TabIndex = 1;
      //
      // btnBrowse
      //
      this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.btnBrowse.Location = new System.Drawing.Point(733, 10);
      this.btnBrowse.Name = "btnBrowse";
      this.btnBrowse.Size = new System.Drawing.Size(100, 28);
      this.btnBrowse.TabIndex = 2;
      this.btnBrowse.Text = "Browse…";
      this.btnBrowse.UseVisualStyleBackColor = true;
      this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
      //
      // btnRun
      //
      this.btnRun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.btnRun.Location = new System.Drawing.Point(820, 10);
      this.btnRun.Name = "btnRun";
      this.btnRun.Size = new System.Drawing.Size(150, 28);
      this.btnRun.TabIndex = 3;
      this.btnRun.Text = "Run";
      this.btnRun.UseVisualStyleBackColor = true;
      this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
      //
      // gridInput
      //
      this.gridInput.AllowUserToAddRows = false;
      this.gridInput.AllowUserToDeleteRows = false;
      this.gridInput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
      this.gridInput.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this.gridInput.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this.gridInput.Location = new System.Drawing.Point(12, 84);
      this.gridInput.Name = "gridInput";
      this.gridInput.ReadOnly = true;
      this.gridInput.RowHeadersVisible = false;
      this.gridInput.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this.gridInput.Size = new System.Drawing.Size(960, 450);
      this.gridInput.TabIndex = 1;
      //
      // pnlBottom
      //
      this.pnlBottom.Controls.Add(this.lblStatus);
      this.pnlBottom.Controls.Add(this.lblEta);
      this.pnlBottom.Controls.Add(this.lblPercent);
      this.pnlBottom.Controls.Add(this.progressBar);
      this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
      this.pnlBottom.Location = new System.Drawing.Point(0, 545);
      this.pnlBottom.Name = "pnlBottom";
      this.pnlBottom.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
      this.pnlBottom.Size = new System.Drawing.Size(984, 66);
      this.pnlBottom.TabIndex = 2;
      //
      // progressBar
      //
      this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
      this.progressBar.Location = new System.Drawing.Point(12, 8);
      this.progressBar.Name = "progressBar";
      this.progressBar.Size = new System.Drawing.Size(820, 18);
      this.progressBar.TabIndex = 0;
      //
      // lblPercent
      //
      this.lblPercent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.lblPercent.Location = new System.Drawing.Point(838, 6);
      this.lblPercent.Name = "lblPercent";
      this.lblPercent.Size = new System.Drawing.Size(60, 22);
      this.lblPercent.TabIndex = 1;
      this.lblPercent.Text = "0%";
      this.lblPercent.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      //
      // lblEta
      //
      this.lblEta.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this.lblEta.Location = new System.Drawing.Point(900, 6);
      this.lblEta.Name = "lblEta";
      this.lblEta.Size = new System.Drawing.Size(72, 22);
      this.lblEta.TabIndex = 2;
      this.lblEta.Text = "";
      this.lblEta.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      //
      // lblStatus
      //
      this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
      this.lblStatus.Location = new System.Drawing.Point(12, 32);
      this.lblStatus.Name = "lblStatus";
      this.lblStatus.Size = new System.Drawing.Size(960, 26);
      this.lblStatus.TabIndex = 3;
      this.lblStatus.Text = "Ready";
      //
      // MainForm
      //
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(984, 611);
      this.Controls.Add(this.gridInput);
      this.Controls.Add(this.pnlBottom);
      this.Controls.Add(this.pnlTop);
      this.MinimumSize = new System.Drawing.Size(800, 500);
      this.Name = "MainForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "PocTrace Demolition";
      this.pnlTop.ResumeLayout(false);
      this.pnlTop.PerformLayout();
      this.pnlBottom.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.gridInput)).EndInit();
      this.ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Panel pnlTop;
    private System.Windows.Forms.Button btnRun;
    private System.Windows.Forms.Button btnBrowse;
    private System.Windows.Forms.TextBox txtInputPath;
    private System.Windows.Forms.Label lblInput;
    private System.Windows.Forms.DataGridView gridInput;
    private System.Windows.Forms.Panel pnlBottom;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblPercent;
    private System.Windows.Forms.Label lblEta;
    private System.Windows.Forms.Label lblStatus;
  }
}
