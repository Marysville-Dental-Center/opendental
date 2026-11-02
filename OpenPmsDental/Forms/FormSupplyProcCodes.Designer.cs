namespace OpenPmsDental {
	partial class FormSupplyProcCodes {
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
			this.gridCodes = new OpenPmsDental.UI.GridOD();
			this.gridMaterials = new OpenPmsDental.UI.GridOD();
			this.butAddCode = new OpenPmsDental.UI.Button();
			this.butAddSupply = new OpenPmsDental.UI.Button();
			this.butRemoveSupply = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelInstructions
			//
			this.labelInstructions.Location = new System.Drawing.Point(12, 7);
			this.labelInstructions.Name = "labelInstructions";
			this.labelInstructions.Size = new System.Drawing.Size(760, 30);
			this.labelInstructions.TabIndex = 0;
			this.labelInstructions.Text = "When a procedure with one of these codes is completed, the Materials used window opens filled with its default materi" +
    "als. Changes are saved right away.";
			this.labelInstructions.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridCodes
			//
			this.gridCodes.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridCodes.Location = new System.Drawing.Point(12, 40);
			this.gridCodes.Name = "gridCodes";
			this.gridCodes.Size = new System.Drawing.Size(330, 400);
			this.gridCodes.TabIndex = 1;
			this.gridCodes.Title = "Procedure Codes";
			this.gridCodes.TranslationName = "TableSupplyProcCodes";
			this.gridCodes.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridCodes_CellClick);
			//
			// gridMaterials
			//
			this.gridMaterials.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMaterials.Location = new System.Drawing.Point(350, 40);
			this.gridMaterials.Name = "gridMaterials";
			this.gridMaterials.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;
			this.gridMaterials.Size = new System.Drawing.Size(422, 400);
			this.gridMaterials.TabIndex = 2;
			this.gridMaterials.Title = "Default Materials";
			this.gridMaterials.TranslationName = "TableSupplyProcCodeMaterials";
			this.gridMaterials.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMaterials_CellLeave);
			//
			// butAddCode
			//
			this.butAddCode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAddCode.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAddCode.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAddCode.Location = new System.Drawing.Point(12, 448);
			this.butAddCode.Name = "butAddCode";
			this.butAddCode.Size = new System.Drawing.Size(90, 24);
			this.butAddCode.TabIndex = 3;
			this.butAddCode.Text = "Add Code";
			this.butAddCode.Click += new System.EventHandler(this.butAddCode_Click);
			//
			// butAddSupply
			//
			this.butAddSupply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAddSupply.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAddSupply.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAddSupply.Location = new System.Drawing.Point(350, 448);
			this.butAddSupply.Name = "butAddSupply";
			this.butAddSupply.Size = new System.Drawing.Size(90, 24);
			this.butAddSupply.TabIndex = 4;
			this.butAddSupply.Text = "Add Supply";
			this.butAddSupply.Click += new System.EventHandler(this.butAddSupply_Click);
			//
			// butRemoveSupply
			//
			this.butRemoveSupply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRemoveSupply.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butRemoveSupply.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butRemoveSupply.Location = new System.Drawing.Point(446, 448);
			this.butRemoveSupply.Name = "butRemoveSupply";
			this.butRemoveSupply.Size = new System.Drawing.Size(90, 24);
			this.butRemoveSupply.TabIndex = 5;
			this.butRemoveSupply.Text = "Remove";
			this.butRemoveSupply.Click += new System.EventHandler(this.butRemoveSupply_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(697, 448);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 6;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSupplyProcCodes
			//
			this.ClientSize = new System.Drawing.Size(784, 482);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butRemoveSupply);
			this.Controls.Add(this.butAddSupply);
			this.Controls.Add(this.butAddCode);
			this.Controls.Add(this.gridMaterials);
			this.Controls.Add(this.gridCodes);
			this.Controls.Add(this.labelInstructions);
			this.Name = "FormSupplyProcCodes";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Default Materials of Procedure Codes";
			this.Load += new System.EventHandler(this.FormSupplyProcCodes_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelInstructions;
		private UI.GridOD gridCodes;
		private UI.GridOD gridMaterials;
		private UI.Button butAddCode;
		private UI.Button butAddSupply;
		private UI.Button butRemoveSupply;
		private UI.Button butClose;
	}
}
