namespace OpenPmsDental {
	partial class FormMaintHistory {
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
			this.labelItem = new System.Windows.Forms.Label();
			this.labelDateFrom = new System.Windows.Forms.Label();
			this.textDateFrom = new OpenPmsDental.ValidDate();
			this.labelDateTo = new System.Windows.Forms.Label();
			this.textDateTo = new OpenPmsDental.ValidDate();
			this.butRefresh = new OpenPmsDental.UI.Button();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.gridValues = new OpenPmsDental.UI.GridOD();
			this.butOpenAttachment = new OpenPmsDental.UI.Button();
			this.butPrint = new OpenPmsDental.UI.Button();
			this.butExport = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelItem
			//
			this.labelItem.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelItem.Location = new System.Drawing.Point(12, 12);
			this.labelItem.Name = "labelItem";
			this.labelItem.Size = new System.Drawing.Size(450, 20);
			this.labelItem.TabIndex = 0;
			this.labelItem.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelDateFrom
			//
			this.labelDateFrom.Location = new System.Drawing.Point(470, 12);
			this.labelDateFrom.Name = "labelDateFrom";
			this.labelDateFrom.Size = new System.Drawing.Size(80, 20);
			this.labelDateFrom.TabIndex = 1;
			this.labelDateFrom.Text = "Done from";
			this.labelDateFrom.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateFrom
			//
			this.textDateFrom.Location = new System.Drawing.Point(553, 12);
			this.textDateFrom.Name = "textDateFrom";
			this.textDateFrom.Size = new System.Drawing.Size(90, 20);
			this.textDateFrom.TabIndex = 2;
			//
			// labelDateTo
			//
			this.labelDateTo.Location = new System.Drawing.Point(645, 12);
			this.labelDateTo.Name = "labelDateTo";
			this.labelDateTo.Size = new System.Drawing.Size(30, 20);
			this.labelDateTo.TabIndex = 3;
			this.labelDateTo.Text = "to";
			this.labelDateTo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			//
			// textDateTo
			//
			this.textDateTo.Location = new System.Drawing.Point(677, 12);
			this.textDateTo.Name = "textDateTo";
			this.textDateTo.Size = new System.Drawing.Size(90, 20);
			this.textDateTo.TabIndex = 4;
			//
			// butRefresh
			//
			this.butRefresh.Location = new System.Drawing.Point(778, 10);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 24);
			this.butRefresh.TabIndex = 5;
			this.butRefresh.Text = "Refresh";
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 42);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(1076, 360);
			this.gridMain.TabIndex = 6;
			this.gridMain.Title = "Maintenance History";
			this.gridMain.TranslationName = "TableMaintHistory";
			this.gridMain.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellClick);
			//
			// gridValues
			//
			this.gridValues.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridValues.Location = new System.Drawing.Point(12, 410);
			this.gridValues.Name = "gridValues";
			this.gridValues.Size = new System.Drawing.Size(990, 180);
			this.gridValues.TabIndex = 7;
			this.gridValues.Title = "Checklist and Results";
			this.gridValues.TranslationName = "TableMaintHistoryValues";
			this.gridValues.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridValues_CellDoubleClick);
			//
			// butOpenAttachment
			//
			this.butOpenAttachment.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOpenAttachment.Location = new System.Drawing.Point(1008, 410);
			this.butOpenAttachment.Name = "butOpenAttachment";
			this.butOpenAttachment.Size = new System.Drawing.Size(80, 40);
			this.butOpenAttachment.TabIndex = 8;
			this.butOpenAttachment.Text = "Open Attachment";
			this.butOpenAttachment.Click += new System.EventHandler(this.butOpenAttachment_Click);
			//
			// butPrint
			//
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butPrint.Image = global::OpenPmsDental.Properties.Resources.butPrint;
			this.butPrint.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butPrint.Location = new System.Drawing.Point(12, 600);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(75, 24);
			this.butPrint.TabIndex = 9;
			this.butPrint.Text = "Print";
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			//
			// butExport
			//
			this.butExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butExport.Location = new System.Drawing.Point(93, 600);
			this.butExport.Name = "butExport";
			this.butExport.Size = new System.Drawing.Size(75, 24);
			this.butExport.TabIndex = 10;
			this.butExport.Text = "Export";
			this.butExport.Click += new System.EventHandler(this.butExport_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(1013, 600);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 11;
			this.butClose.Text = "Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormMaintHistory
			//
			this.ClientSize = new System.Drawing.Size(1100, 636);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butExport);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.butOpenAttachment);
			this.Controls.Add(this.gridValues);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.textDateTo);
			this.Controls.Add(this.labelDateTo);
			this.Controls.Add(this.textDateFrom);
			this.Controls.Add(this.labelDateFrom);
			this.Controls.Add(this.labelItem);
			this.Name = "FormMaintHistory";
			this.Text = "Maintenance History";
			this.Load += new System.EventHandler(this.FormMaintHistory_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelItem;
		private System.Windows.Forms.Label labelDateFrom;
		private OpenPmsDental.ValidDate textDateFrom;
		private System.Windows.Forms.Label labelDateTo;
		private OpenPmsDental.ValidDate textDateTo;
		private OpenPmsDental.UI.Button butRefresh;
		private OpenPmsDental.UI.GridOD gridMain;
		private OpenPmsDental.UI.GridOD gridValues;
		private OpenPmsDental.UI.Button butOpenAttachment;
		private OpenPmsDental.UI.Button butPrint;
		private OpenPmsDental.UI.Button butExport;
		private OpenPmsDental.UI.Button butClose;
	}
}
