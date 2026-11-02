namespace OpenPmsDental {
	partial class FormSupplyStock {
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
			this.labelSupply = new System.Windows.Forms.Label();
			this.gridLots = new OpenPmsDental.UI.GridOD();
			this.checkShowEmptyLots = new OpenPmsDental.UI.CheckBox();
			this.gridLedger = new OpenPmsDental.UI.GridOD();
			this.butAdjust = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelSupply
			//
			this.labelSupply.Location = new System.Drawing.Point(12, 7);
			this.labelSupply.Name = "labelSupply";
			this.labelSupply.Size = new System.Drawing.Size(860, 20);
			this.labelSupply.TabIndex = 0;
			this.labelSupply.Text = "Supply";
			this.labelSupply.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridLots
			//
			this.gridLots.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridLots.Location = new System.Drawing.Point(12, 32);
			this.gridLots.Name = "gridLots";
			this.gridLots.Size = new System.Drawing.Size(300, 400);
			this.gridLots.TabIndex = 1;
			this.gridLots.Title = "Lots";
			this.gridLots.TranslationName = "TableSupplyLots";
			this.gridLots.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridLots_CellDoubleClick);
			//
			// checkShowEmptyLots
			//
			this.checkShowEmptyLots.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkShowEmptyLots.Location = new System.Drawing.Point(12, 438);
			this.checkShowEmptyLots.Name = "checkShowEmptyLots";
			this.checkShowEmptyLots.Size = new System.Drawing.Size(200, 18);
			this.checkShowEmptyLots.TabIndex = 2;
			this.checkShowEmptyLots.Text = "Show lots without stock";
			this.checkShowEmptyLots.Click += new System.EventHandler(this.checkShowEmptyLots_Click);
			//
			// gridLedger
			//
			this.gridLedger.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridLedger.Location = new System.Drawing.Point(320, 32);
			this.gridLedger.Name = "gridLedger";
			this.gridLedger.Size = new System.Drawing.Size(752, 400);
			this.gridLedger.TabIndex = 3;
			this.gridLedger.Title = "Stock Ledger";
			this.gridLedger.TranslationName = "TableSupplyLedger";
			//
			// butAdjust
			//
			this.butAdjust.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdjust.Location = new System.Drawing.Point(320, 438);
			this.butAdjust.Name = "butAdjust";
			this.butAdjust.Size = new System.Drawing.Size(80, 24);
			this.butAdjust.TabIndex = 4;
			this.butAdjust.Text = "Adjust";
			this.butAdjust.Click += new System.EventHandler(this.butAdjust_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(997, 438);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 5;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSupplyStock
			//
			this.ClientSize = new System.Drawing.Size(1084, 472);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butAdjust);
			this.Controls.Add(this.gridLedger);
			this.Controls.Add(this.checkShowEmptyLots);
			this.Controls.Add(this.gridLots);
			this.Controls.Add(this.labelSupply);
			this.Name = "FormSupplyStock";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Supply Stock";
			this.Load += new System.EventHandler(this.FormSupplyStock_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelSupply;
		private UI.GridOD gridLots;
		private UI.CheckBox checkShowEmptyLots;
		private UI.GridOD gridLedger;
		private UI.Button butAdjust;
		private UI.Button butClose;
	}
}
