namespace OpenPmsDental {
	partial class FormSupplyProcPendings {
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
			this.butMaterials = new OpenPmsDental.UI.Button();
			this.butNoMaterials = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelInstructions
			//
			this.labelInstructions.Location = new System.Drawing.Point(12, 7);
			this.labelInstructions.Name = "labelInstructions";
			this.labelInstructions.Size = new System.Drawing.Size(736, 30);
			this.labelInstructions.TabIndex = 0;
			this.labelInstructions.Text = "Completed procedures whose materials were not confirmed: nothing was deducted for them yet. Select one or more and " +
    "click Materials to confirm what they used.";
			this.labelInstructions.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 40);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.MultiExtended;
			this.gridMain.Size = new System.Drawing.Size(736, 400);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "Awaiting Materials";
			this.gridMain.TranslationName = "TableSupplyProcPendings";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			//
			// butMaterials
			//
			this.butMaterials.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butMaterials.Location = new System.Drawing.Point(12, 448);
			this.butMaterials.Name = "butMaterials";
			this.butMaterials.Size = new System.Drawing.Size(90, 24);
			this.butMaterials.TabIndex = 2;
			this.butMaterials.Text = "&Materials";
			this.butMaterials.Click += new System.EventHandler(this.butMaterials_Click);
			//
			// butNoMaterials
			//
			this.butNoMaterials.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butNoMaterials.Location = new System.Drawing.Point(108, 448);
			this.butNoMaterials.Name = "butNoMaterials";
			this.butNoMaterials.Size = new System.Drawing.Size(100, 24);
			this.butNoMaterials.TabIndex = 3;
			this.butNoMaterials.Text = "&No Materials";
			this.butNoMaterials.Click += new System.EventHandler(this.butNoMaterials_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(673, 448);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 4;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSupplyProcPendings
			//
			this.ClientSize = new System.Drawing.Size(760, 482);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butNoMaterials);
			this.Controls.Add(this.butMaterials);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelInstructions);
			this.Name = "FormSupplyProcPendings";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Awaiting Materials";
			this.Load += new System.EventHandler(this.FormSupplyProcPendings_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelInstructions;
		private UI.GridOD gridMain;
		private UI.Button butMaterials;
		private UI.Button butNoMaterials;
		private UI.Button butClose;
	}
}
