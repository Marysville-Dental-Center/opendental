namespace OpenPmsDental {
	partial class FormMaterialsUsed {
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
			this.labelInstructions = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.labelScan = new System.Windows.Forms.Label();
			this.textScan = new System.Windows.Forms.TextBox();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butRemove = new OpenPmsDental.UI.Button();
			this.labelLot = new System.Windows.Forms.Label();
			this.comboLot = new OpenPmsDental.UI.ComboBox();
			this.butLotNumber = new OpenPmsDental.UI.Button();
			this.labelReadOnly = new System.Windows.Forms.Label();
			this.butOK = new OpenPmsDental.UI.Button();
			this.butCancel = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelInstructions
			//
			this.labelInstructions.Location = new System.Drawing.Point(12, 7);
			this.labelInstructions.Name = "labelInstructions";
			this.labelInstructions.Size = new System.Drawing.Size(736, 32);
			this.labelInstructions.TabIndex = 12;
			this.labelInstructions.Text = "Confirm the materials used: change a quantity, add items (Add, or scan a barcode), remove a row. OK deducts them, s" +
    "oonest-expiring lot first unless you pick a lot. An implant\'s lot is recorded with the procedure.";
			this.labelInstructions.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 44);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;
			this.gridMain.Size = new System.Drawing.Size(736, 300);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "Materials Used";
			this.gridMain.TranslationName = "TableMaterialsUsed";
			this.gridMain.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellClick);
			this.gridMain.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellLeave);
			//
			// labelScan
			//
			this.labelScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelScan.Location = new System.Drawing.Point(12, 354);
			this.labelScan.Name = "labelScan";
			this.labelScan.Size = new System.Drawing.Size(46, 18);
			this.labelScan.TabIndex = 2;
			this.labelScan.Text = "Scan";
			this.labelScan.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textScan
			//
			this.textScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.textScan.Location = new System.Drawing.Point(60, 353);
			this.textScan.Name = "textScan";
			this.textScan.Size = new System.Drawing.Size(160, 20);
			this.textScan.TabIndex = 0;
			this.textScan.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textScan_KeyDown);
			//
			// butAdd
			//
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(230, 351);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(80, 24);
			this.butAdd.TabIndex = 4;
			this.butAdd.Text = "Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			//
			// butRemove
			//
			this.butRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRemove.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butRemove.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butRemove.Location = new System.Drawing.Point(316, 351);
			this.butRemove.Name = "butRemove";
			this.butRemove.Size = new System.Drawing.Size(80, 24);
			this.butRemove.TabIndex = 5;
			this.butRemove.Text = "Remove";
			this.butRemove.Click += new System.EventHandler(this.butRemove_Click);
			//
			// labelLot
			//
			this.labelLot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelLot.Location = new System.Drawing.Point(410, 354);
			this.labelLot.Name = "labelLot";
			this.labelLot.Size = new System.Drawing.Size(60, 18);
			this.labelLot.TabIndex = 6;
			this.labelLot.Text = "Lot";
			this.labelLot.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboLot
			//
			this.comboLot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.comboLot.Location = new System.Drawing.Point(472, 353);
			this.comboLot.Name = "comboLot";
			this.comboLot.Size = new System.Drawing.Size(276, 21);
			this.comboLot.TabIndex = 7;
			this.comboLot.SelectionChangeCommitted += new System.EventHandler(this.comboLot_SelectionChangeCommitted);
			//
			// butLotNumber
			//
			this.butLotNumber.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butLotNumber.Location = new System.Drawing.Point(648, 381);
			this.butLotNumber.Name = "butLotNumber";
			this.butLotNumber.Size = new System.Drawing.Size(100, 24);
			this.butLotNumber.TabIndex = 11;
			this.butLotNumber.Text = "Lot Number...";
			this.butLotNumber.Click += new System.EventHandler(this.butLotNumber_Click);
			//
			// labelReadOnly
			//
			this.labelReadOnly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelReadOnly.ForeColor = System.Drawing.Color.DarkRed;
			this.labelReadOnly.Location = new System.Drawing.Point(12, 384);
			this.labelReadOnly.Name = "labelReadOnly";
			this.labelReadOnly.Size = new System.Drawing.Size(560, 18);
			this.labelReadOnly.TabIndex = 8;
			this.labelReadOnly.Text = "Read only. Changing confirmed materials needs Completed Procedure Edit Misc for the procedure\'s date.";
			this.labelReadOnly.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.labelReadOnly.Visible = false;
			//
			// butOK
			//
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(592, 412);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 9;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			//
			// butCancel
			//
			this.butCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butCancel.Location = new System.Drawing.Point(673, 412);
			this.butCancel.Name = "butCancel";
			this.butCancel.Size = new System.Drawing.Size(75, 24);
			this.butCancel.TabIndex = 10;
			this.butCancel.Text = "&Cancel";
			this.butCancel.Click += new System.EventHandler(this.butCancel_Click);
			//
			// FormMaterialsUsed
			//
			this.ClientSize = new System.Drawing.Size(760, 448);
			this.Controls.Add(this.butCancel);
			this.Controls.Add(this.butOK);
			this.Controls.Add(this.labelReadOnly);
			this.Controls.Add(this.butLotNumber);
			this.Controls.Add(this.comboLot);
			this.Controls.Add(this.labelLot);
			this.Controls.Add(this.butRemove);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.textScan);
			this.Controls.Add(this.labelScan);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelInstructions);
			this.Name = "FormMaterialsUsed";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Materials Used";
			this.Load += new System.EventHandler(this.FormMaterialsUsed_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelInstructions;
		private UI.GridOD gridMain;
		private System.Windows.Forms.Label labelScan;
		private System.Windows.Forms.TextBox textScan;
		private UI.Button butAdd;
		private UI.Button butRemove;
		private System.Windows.Forms.Label labelLot;
		private UI.ComboBox comboLot;
		private UI.Button butLotNumber;
		private System.Windows.Forms.Label labelReadOnly;
		private UI.Button butOK;
		private UI.Button butCancel;
	}
}
