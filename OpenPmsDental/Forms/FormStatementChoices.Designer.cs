namespace OpenPmsDental{
	partial class FormStatementChoices {
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
			this.butOK = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelInstructions
			//
			this.labelInstructions.Location = new System.Drawing.Point(12, 9);
			this.labelInstructions.Name = "labelInstructions";
			this.labelInstructions.Size = new System.Drawing.Size(560, 18);
			this.labelInstructions.TabIndex = 0;
			this.labelInstructions.Text = "Choose the kind of statement. Double-click a row, or select it and click OK.";
			this.labelInstructions.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 30);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(600, 262);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "Statement Options";
			this.gridMain.TranslationName = "TableStatementChoices";
			this.gridMain.WrapText = true;
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			//
			// butOK
			//
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(537, 302);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 2;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			//
			// FormStatementChoices
			//
			this.ClientSize = new System.Drawing.Size(624, 338);
			this.Controls.Add(this.butOK);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelInstructions);
			this.Name = "FormStatementChoices";
			this.Text = "Statement Options";
			this.Load += new System.EventHandler(this.FormStatementChoices_Load);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Label labelInstructions;
		private UI.GridOD gridMain;
		private UI.Button butOK;
	}
}
