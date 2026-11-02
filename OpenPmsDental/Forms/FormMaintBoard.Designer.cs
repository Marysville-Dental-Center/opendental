namespace OpenPmsDental {
	partial class FormMaintBoard {
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
			this.labelAssignee = new System.Windows.Forms.Label();
			this.comboAssignee = new OpenPmsDental.UI.ComboBox();
			this.checkMine = new OpenPmsDental.UI.CheckBox();
			this.checkShowHidden = new OpenPmsDental.UI.CheckBox();
			this.butRefresh = new OpenPmsDental.UI.Button();
			this.labelSummary = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.butRecord = new OpenPmsDental.UI.Button();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butEdit = new OpenPmsDental.UI.Button();
			this.butHistory = new OpenPmsDental.UI.Button();
			this.butHistoryAll = new OpenPmsDental.UI.Button();
			this.butPrint = new OpenPmsDental.UI.Button();
			this.butExport = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelAssignee
			//
			this.labelAssignee.Location = new System.Drawing.Point(12, 12);
			this.labelAssignee.Name = "labelAssignee";
			this.labelAssignee.Size = new System.Drawing.Size(80, 20);
			this.labelAssignee.TabIndex = 0;
			this.labelAssignee.Text = "Assigned to";
			this.labelAssignee.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboAssignee
			//
			this.comboAssignee.Location = new System.Drawing.Point(95, 12);
			this.comboAssignee.Name = "comboAssignee";
			this.comboAssignee.Size = new System.Drawing.Size(200, 21);
			this.comboAssignee.TabIndex = 1;
			this.comboAssignee.SelectionChangeCommitted += new System.EventHandler(this.comboAssignee_SelectionChangeCommitted);
			//
			// checkMine
			//
			this.checkMine.Location = new System.Drawing.Point(310, 13);
			this.checkMine.Name = "checkMine";
			this.checkMine.Size = new System.Drawing.Size(100, 20);
			this.checkMine.TabIndex = 2;
			this.checkMine.Text = "Mine only";
			this.checkMine.Click += new System.EventHandler(this.checkMine_Click);
			//
			// checkShowHidden
			//
			this.checkShowHidden.Location = new System.Drawing.Point(415, 13);
			this.checkShowHidden.Name = "checkShowHidden";
			this.checkShowHidden.Size = new System.Drawing.Size(110, 20);
			this.checkShowHidden.TabIndex = 3;
			this.checkShowHidden.Text = "Show hidden";
			this.checkShowHidden.Click += new System.EventHandler(this.checkShowHidden_Click);
			//
			// butRefresh
			//
			this.butRefresh.Location = new System.Drawing.Point(535, 10);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 24);
			this.butRefresh.TabIndex = 4;
			this.butRefresh.Text = "Refresh";
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			//
			// labelSummary
			//
			this.labelSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelSummary.Location = new System.Drawing.Point(625, 12);
			this.labelSummary.Name = "labelSummary";
			this.labelSummary.Size = new System.Drawing.Size(463, 20);
			this.labelSummary.TabIndex = 5;
			this.labelSummary.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 42);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(1076, 546);
			this.gridMain.TabIndex = 6;
			this.gridMain.Title = "Maintenance";
			this.gridMain.TranslationName = "TableMaintBoard";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			//
			// butRecord
			//
			this.butRecord.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRecord.Location = new System.Drawing.Point(12, 600);
			this.butRecord.Name = "butRecord";
			this.butRecord.Size = new System.Drawing.Size(80, 24);
			this.butRecord.TabIndex = 7;
			this.butRecord.Text = "Record";
			this.butRecord.Click += new System.EventHandler(this.butRecord_Click);
			//
			// butAdd
			//
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.Location = new System.Drawing.Point(120, 600);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(85, 24);
			this.butAdd.TabIndex = 8;
			this.butAdd.Text = "Add Item";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			//
			// butEdit
			//
			this.butEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butEdit.Location = new System.Drawing.Point(211, 600);
			this.butEdit.Name = "butEdit";
			this.butEdit.Size = new System.Drawing.Size(85, 24);
			this.butEdit.TabIndex = 9;
			this.butEdit.Text = "Edit Item";
			this.butEdit.Click += new System.EventHandler(this.butEdit_Click);
			//
			// butHistory
			//
			this.butHistory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butHistory.Location = new System.Drawing.Point(324, 600);
			this.butHistory.Name = "butHistory";
			this.butHistory.Size = new System.Drawing.Size(80, 24);
			this.butHistory.TabIndex = 10;
			this.butHistory.Text = "History";
			this.butHistory.Click += new System.EventHandler(this.butHistory_Click);
			//
			// butHistoryAll
			//
			this.butHistoryAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butHistoryAll.Location = new System.Drawing.Point(410, 600);
			this.butHistoryAll.Name = "butHistoryAll";
			this.butHistoryAll.Size = new System.Drawing.Size(85, 24);
			this.butHistoryAll.TabIndex = 11;
			this.butHistoryAll.Text = "All History";
			this.butHistoryAll.Click += new System.EventHandler(this.butHistoryAll_Click);
			//
			// butPrint
			//
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butPrint.Image = global::OpenPmsDental.Properties.Resources.butPrint;
			this.butPrint.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butPrint.Location = new System.Drawing.Point(523, 600);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(75, 24);
			this.butPrint.TabIndex = 12;
			this.butPrint.Text = "Print";
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			//
			// butExport
			//
			this.butExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butExport.Location = new System.Drawing.Point(604, 600);
			this.butExport.Name = "butExport";
			this.butExport.Size = new System.Drawing.Size(75, 24);
			this.butExport.TabIndex = 13;
			this.butExport.Text = "Export";
			this.butExport.Click += new System.EventHandler(this.butExport_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(1013, 600);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 14;
			this.butClose.Text = "Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormMaintBoard
			//
			this.ClientSize = new System.Drawing.Size(1100, 636);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butExport);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.butHistoryAll);
			this.Controls.Add(this.butHistory);
			this.Controls.Add(this.butEdit);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.butRecord);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelSummary);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.checkShowHidden);
			this.Controls.Add(this.checkMine);
			this.Controls.Add(this.comboAssignee);
			this.Controls.Add(this.labelAssignee);
			this.Name = "FormMaintBoard";
			this.Text = "Maintenance";
			this.Load += new System.EventHandler(this.FormMaintBoard_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelAssignee;
		private OpenPmsDental.UI.ComboBox comboAssignee;
		private OpenPmsDental.UI.CheckBox checkMine;
		private OpenPmsDental.UI.CheckBox checkShowHidden;
		private OpenPmsDental.UI.Button butRefresh;
		private System.Windows.Forms.Label labelSummary;
		private OpenPmsDental.UI.GridOD gridMain;
		private OpenPmsDental.UI.Button butRecord;
		private OpenPmsDental.UI.Button butAdd;
		private OpenPmsDental.UI.Button butEdit;
		private OpenPmsDental.UI.Button butHistory;
		private OpenPmsDental.UI.Button butHistoryAll;
		private OpenPmsDental.UI.Button butPrint;
		private OpenPmsDental.UI.Button butExport;
		private OpenPmsDental.UI.Button butClose;
	}
}
