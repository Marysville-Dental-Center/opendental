namespace OpenPmsDental {
	partial class FormSupplyUsageReport {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.labelFrom = new System.Windows.Forms.Label();
			this.textDateFrom = new OpenPmsDental.ValidDate();
			this.labelTo = new System.Windows.Forms.Label();
			this.textDateTo = new OpenPmsDental.ValidDate();
			this.radioByItem = new System.Windows.Forms.RadioButton();
			this.radioByCode = new System.Windows.Forms.RadioButton();
			this.butRefresh = new OpenPmsDental.UI.Button();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.labelTotal = new System.Windows.Forms.Label();
			this.butPrint = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelFrom
			//
			this.labelFrom.Location = new System.Drawing.Point(12, 12);
			this.labelFrom.Name = "labelFrom";
			this.labelFrom.Size = new System.Drawing.Size(40, 18);
			this.labelFrom.TabIndex = 0;
			this.labelFrom.Text = "From";
			this.labelFrom.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateFrom
			//
			this.textDateFrom.Location = new System.Drawing.Point(54, 11);
			this.textDateFrom.Name = "textDateFrom";
			this.textDateFrom.Size = new System.Drawing.Size(80, 20);
			this.textDateFrom.TabIndex = 1;
			//
			// labelTo
			//
			this.labelTo.Location = new System.Drawing.Point(138, 12);
			this.labelTo.Name = "labelTo";
			this.labelTo.Size = new System.Drawing.Size(24, 18);
			this.labelTo.TabIndex = 2;
			this.labelTo.Text = "To";
			this.labelTo.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateTo
			//
			this.textDateTo.Location = new System.Drawing.Point(164, 11);
			this.textDateTo.Name = "textDateTo";
			this.textDateTo.Size = new System.Drawing.Size(80, 20);
			this.textDateTo.TabIndex = 3;
			//
			// radioByItem
			//
			this.radioByItem.Location = new System.Drawing.Point(262, 12);
			this.radioByItem.Name = "radioByItem";
			this.radioByItem.Size = new System.Drawing.Size(80, 18);
			this.radioByItem.TabIndex = 4;
			this.radioByItem.Text = "By item";
			this.radioByItem.Click += new System.EventHandler(this.radio_Click);
			//
			// radioByCode
			//
			this.radioByCode.Location = new System.Drawing.Point(346, 12);
			this.radioByCode.Name = "radioByCode";
			this.radioByCode.Size = new System.Drawing.Size(140, 18);
			this.radioByCode.TabIndex = 5;
			this.radioByCode.Text = "By procedure code";
			this.radioByCode.Click += new System.EventHandler(this.radio_Click);
			//
			// butRefresh
			//
			this.butRefresh.Location = new System.Drawing.Point(492, 9);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 24);
			this.butRefresh.TabIndex = 6;
			this.butRefresh.Text = "&Refresh";
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 40);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(696, 420);
			this.gridMain.TabIndex = 7;
			this.gridMain.Title = "Supply Usage";
			this.gridMain.TranslationName = "TableSupplyUsage";
			//
			// labelTotal
			//
			this.labelTotal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelTotal.Location = new System.Drawing.Point(12, 468);
			this.labelTotal.Name = "labelTotal";
			this.labelTotal.Size = new System.Drawing.Size(300, 18);
			this.labelTotal.TabIndex = 8;
			this.labelTotal.Text = "Total cost";
			this.labelTotal.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butPrint
			//
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPrint.Image = global::OpenPmsDental.Properties.Resources.butPrint;
			this.butPrint.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butPrint.Location = new System.Drawing.Point(552, 466);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(75, 24);
			this.butPrint.TabIndex = 9;
			this.butPrint.Text = "Print";
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(633, 466);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 10;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSupplyUsageReport
			//
			this.ClientSize = new System.Drawing.Size(720, 500);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.labelTotal);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.radioByCode);
			this.Controls.Add(this.radioByItem);
			this.Controls.Add(this.textDateTo);
			this.Controls.Add(this.labelTo);
			this.Controls.Add(this.textDateFrom);
			this.Controls.Add(this.labelFrom);
			this.Name = "FormSupplyUsageReport";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Supply Usage Report";
			this.Load += new System.EventHandler(this.FormSupplyUsageReport_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelFrom;
		private ValidDate textDateFrom;
		private System.Windows.Forms.Label labelTo;
		private ValidDate textDateTo;
		private System.Windows.Forms.RadioButton radioByItem;
		private System.Windows.Forms.RadioButton radioByCode;
		private UI.Button butRefresh;
		private UI.GridOD gridMain;
		private System.Windows.Forms.Label labelTotal;
		private UI.Button butPrint;
		private UI.Button butClose;
	}
}
