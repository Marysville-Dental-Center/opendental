namespace OpenDental{
	partial class FormCodeSystemsImport {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormCodeSystemsImport));
			this.gridMain = new OpenDental.UI.GridOD();
			this.butImport = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.EditableAcceptsCR = true;
			this.gridMain.Location = new System.Drawing.Point(12, 12);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(857, 347);
			this.gridMain.TabIndex = 27;
			this.gridMain.Title = "Code Systems";
			this.gridMain.TranslationName = "TableCodeSystems";
			//
			// butImport
			//
			this.butImport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butImport.Enabled = false;
			this.butImport.Location = new System.Drawing.Point(759, 370);
			this.butImport.Name = "butImport";
			this.butImport.Size = new System.Drawing.Size(110, 24);
			this.butImport.TabIndex = 3;
			this.butImport.Text = "&Import CPT File";
			this.butImport.Click += new System.EventHandler(this.butImport_Click);
			//
			// FormCodeSystemsImport
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(881, 406);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butImport);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormCodeSystemsImport";
			this.Text = "Import CPT Codes";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormCodeSystemsImport_FormClosing);
			this.Load += new System.EventHandler(this.FormCodeSystemsImport_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenDental.UI.Button butImport;
		private UI.GridOD gridMain;
	}
}
