namespace OpenPmsDental {
	partial class FormSupplyReceive {
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
			this.butAddLotRow = new OpenPmsDental.UI.Button();
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
			this.labelScanNote.Size = new System.Drawing.Size(500, 28);
			this.labelScanNote.TabIndex = 2;
			this.labelScanNote.Text = "Each scan adds one to the item. Enter the lot number and expiry date of supplies tracked by lot. For a second lot of" +
    " the same item, use Add Lot Row.";
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
			this.gridMain.Size = new System.Drawing.Size(736, 380);
			this.gridMain.TabIndex = 3;
			this.gridMain.Title = "Order Items";
			this.gridMain.TranslationName = "TableSupplyReceive";
			this.gridMain.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellLeave);
			//
			// butAddLotRow
			//
			this.butAddLotRow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAddLotRow.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAddLotRow.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAddLotRow.Location = new System.Drawing.Point(12, 428);
			this.butAddLotRow.Name = "butAddLotRow";
			this.butAddLotRow.Size = new System.Drawing.Size(100, 24);
			this.butAddLotRow.TabIndex = 4;
			this.butAddLotRow.Text = "Add Lot Row";
			this.butAddLotRow.Click += new System.EventHandler(this.butAddLotRow_Click);
			//
			// butPost
			//
			this.butPost.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPost.Location = new System.Drawing.Point(592, 428);
			this.butPost.Name = "butPost";
			this.butPost.Size = new System.Drawing.Size(75, 24);
			this.butPost.TabIndex = 5;
			this.butPost.Text = "&Post";
			this.butPost.Click += new System.EventHandler(this.butPost_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(673, 428);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 6;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSupplyReceive
			//
			this.ClientSize = new System.Drawing.Size(760, 462);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butPost);
			this.Controls.Add(this.butAddLotRow);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelScanNote);
			this.Controls.Add(this.textScan);
			this.Controls.Add(this.labelScan);
			this.Name = "FormSupplyReceive";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Receive Order";
			this.Load += new System.EventHandler(this.FormSupplyReceive_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelScan;
		private System.Windows.Forms.TextBox textScan;
		private System.Windows.Forms.Label labelScanNote;
		private UI.GridOD gridMain;
		private UI.Button butAddLotRow;
		private UI.Button butPost;
		private UI.Button butClose;
	}
}
