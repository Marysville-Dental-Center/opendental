namespace OpenPmsDental{
	partial class FormRecallsPat {
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
			this.butPerio = new OpenPmsDental.UI.Button();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.SuspendLayout();
			// 
			// butPerio
			// 
			this.butPerio.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butPerio.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butPerio.Location = new System.Drawing.Point(267, 396);
			this.butPerio.Name = "butPerio";
			this.butPerio.Size = new System.Drawing.Size(83, 24);
			this.butPerio.TabIndex = 35;
			this.butPerio.Text = "Set Perio";
			this.butPerio.Click += new System.EventHandler(this.butPerio_Click);
			// 
			// butAdd
			// 
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(12, 396);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(75, 24);
			this.butAdd.TabIndex = 34;
			this.butAdd.Text = "Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 12);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.None;
			this.gridMain.Size = new System.Drawing.Size(811, 370);
			this.gridMain.TabIndex = 33;
			this.gridMain.Title = "Recalls";
			this.gridMain.TranslationName = "TableRecallsPat";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			// 
			// FormRecallsPat
			// 
			this.ClientSize = new System.Drawing.Size(848, 439);
			this.Controls.Add(this.butPerio);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.gridMain);
			this.Name = "FormRecallsPat";
			this.Text = "Recalls for Patient";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormRecallsPat_FormClosing);
			this.Load += new System.EventHandler(this.FormRecallsPat_Load);
			this.ResumeLayout(false);

		}

		#endregion
		private OpenPmsDental.UI.GridOD gridMain;
		private OpenPmsDental.UI.Button butAdd;
		private OpenPmsDental.UI.Button butPerio;
	}
}