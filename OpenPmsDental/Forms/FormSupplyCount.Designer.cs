namespace OpenPmsDental {
	partial class FormSupplyCount {
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
			this.labelScan = new System.Windows.Forms.Label();
			this.textScan = new System.Windows.Forms.TextBox();
			this.labelScanNote = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.labelCount = new System.Windows.Forms.Label();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butNewLot = new OpenPmsDental.UI.Button();
			this.butRemove = new OpenPmsDental.UI.Button();
			this.butDiscard = new OpenPmsDental.UI.Button();
			this.labelReason = new System.Windows.Forms.Label();
			this.textReason = new System.Windows.Forms.TextBox();
			this.butPost = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelScan
			//
			this.labelScan.Location = new System.Drawing.Point(12, 12);
			this.labelScan.Name = "labelScan";
			this.labelScan.Size = new System.Drawing.Size(46, 18);
			this.labelScan.TabIndex = 0;
			this.labelScan.Text = "Scan";
			this.labelScan.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textScan
			//
			this.textScan.Location = new System.Drawing.Point(60, 11);
			this.textScan.Name = "textScan";
			this.textScan.Size = new System.Drawing.Size(180, 20);
			this.textScan.TabIndex = 1;
			this.textScan.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textScan_KeyDown);
			//
			// labelScanNote
			//
			this.labelScanNote.Location = new System.Drawing.Point(246, 8);
			this.labelScanNote.Name = "labelScanNote";
			this.labelScanNote.Size = new System.Drawing.Size(522, 28);
			this.labelScanNote.TabIndex = 2;
			this.labelScanNote.Text = "Each scan adds one to the counted quantity of the item (of the selected lot). Or type the counted quantity in the" +
    " grid. Every counted row is saved at once.";
			this.labelScanNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 40);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;
			this.gridMain.Size = new System.Drawing.Size(756, 380);
			this.gridMain.TabIndex = 3;
			this.gridMain.Title = "Stock Count";
			this.gridMain.TranslationName = "TableSupplyCount";
			this.gridMain.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellLeave);
			//
			// labelCount
			//
			this.labelCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelCount.Location = new System.Drawing.Point(12, 424);
			this.labelCount.Name = "labelCount";
			this.labelCount.Size = new System.Drawing.Size(756, 18);
			this.labelCount.TabIndex = 4;
			this.labelCount.Text = "0 rows counted";
			this.labelCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butAdd
			//
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(12, 446);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(80, 24);
			this.butAdd.TabIndex = 5;
			this.butAdd.Text = "Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			//
			// butNewLot
			//
			this.butNewLot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butNewLot.Location = new System.Drawing.Point(98, 446);
			this.butNewLot.Name = "butNewLot";
			this.butNewLot.Size = new System.Drawing.Size(80, 24);
			this.butNewLot.TabIndex = 6;
			this.butNewLot.Text = "Lot...";
			this.butNewLot.Click += new System.EventHandler(this.butNewLot_Click);
			//
			// butRemove
			//
			this.butRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRemove.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butRemove.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butRemove.Location = new System.Drawing.Point(184, 446);
			this.butRemove.Name = "butRemove";
			this.butRemove.Size = new System.Drawing.Size(80, 24);
			this.butRemove.TabIndex = 7;
			this.butRemove.Text = "Remove";
			this.butRemove.Click += new System.EventHandler(this.butRemove_Click);
			//
			// butDiscard
			//
			this.butDiscard.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDiscard.Location = new System.Drawing.Point(270, 446);
			this.butDiscard.Name = "butDiscard";
			this.butDiscard.Size = new System.Drawing.Size(80, 24);
			this.butDiscard.TabIndex = 8;
			this.butDiscard.Text = "Discard";
			this.butDiscard.Click += new System.EventHandler(this.butDiscard_Click);
			//
			// labelReason
			//
			this.labelReason.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelReason.Location = new System.Drawing.Point(356, 449);
			this.labelReason.Name = "labelReason";
			this.labelReason.Size = new System.Drawing.Size(56, 18);
			this.labelReason.TabIndex = 9;
			this.labelReason.Text = "Reason";
			this.labelReason.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textReason
			//
			this.textReason.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.textReason.Location = new System.Drawing.Point(414, 448);
			this.textReason.MaxLength = 255;
			this.textReason.Name = "textReason";
			this.textReason.Size = new System.Drawing.Size(190, 20);
			this.textReason.TabIndex = 10;
			//
			// butPost
			//
			this.butPost.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPost.Location = new System.Drawing.Point(612, 446);
			this.butPost.Name = "butPost";
			this.butPost.Size = new System.Drawing.Size(75, 24);
			this.butPost.TabIndex = 11;
			this.butPost.Text = "&Post";
			this.butPost.Click += new System.EventHandler(this.butPost_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(693, 446);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 12;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSupplyCount
			//
			this.ClientSize = new System.Drawing.Size(780, 480);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butPost);
			this.Controls.Add(this.textReason);
			this.Controls.Add(this.labelReason);
			this.Controls.Add(this.butDiscard);
			this.Controls.Add(this.butRemove);
			this.Controls.Add(this.butNewLot);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.labelCount);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelScanNote);
			this.Controls.Add(this.textScan);
			this.Controls.Add(this.labelScan);
			this.Name = "FormSupplyCount";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Stock Count";
			this.Load += new System.EventHandler(this.FormSupplyCount_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelScan;
		private System.Windows.Forms.TextBox textScan;
		private System.Windows.Forms.Label labelScanNote;
		private UI.GridOD gridMain;
		private System.Windows.Forms.Label labelCount;
		private UI.Button butAdd;
		private UI.Button butNewLot;
		private UI.Button butRemove;
		private UI.Button butDiscard;
		private System.Windows.Forms.Label labelReason;
		private System.Windows.Forms.TextBox textReason;
		private UI.Button butPost;
		private UI.Button butClose;
	}
}
