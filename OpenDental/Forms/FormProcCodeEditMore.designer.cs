namespace OpenDental{
	partial class FormProcCodeEditMore {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormProcCodeEditMore));
			this.gridMain = new OpenDental.UI.GridOD();
			this.labelProvider = new System.Windows.Forms.Label();
			this.comboProvider = new OpenDental.UI.ComboBox();
			this.comboClinic = new OpenDental.UI.ComboBoxClinicPicker();
			this.SuspendLayout();
			// 
			// labelProvider
			// 
			this.labelProvider.Location = new System.Drawing.Point(9, 13);
			this.labelProvider.Name = "labelProvider";
			this.labelProvider.Size = new System.Drawing.Size(55, 17);
			this.labelProvider.TabIndex = 5;
			this.labelProvider.Text = "Provider";
			this.labelProvider.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboProvider
			// 
			this.comboProvider.Location = new System.Drawing.Point(66, 12);
			this.comboProvider.Name = "comboProvider";
			this.comboProvider.Size = new System.Drawing.Size(160, 21);
			this.comboProvider.TabIndex = 0;
			this.comboProvider.SelectionChangeCommitted += new System.EventHandler(this.comboFilter_SelectionChangeCommitted);
			// 
			// comboClinic
			// 
			this.comboClinic.HqDescription = "Default";
			this.comboClinic.IncludeAll = true;
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(240, 12);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(308, 21);
			this.comboClinic.TabIndex = 1;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboFilter_SelectionChangeCommitted);
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 42);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(536, 452);
			this.gridMain.TabIndex = 4;
			this.gridMain.Title = "Fees";
			this.gridMain.TranslationName = "TableFees";
			this.gridMain.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			// 
			// FormProcCodeEditMore
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(560, 506);
			this.Controls.Add(this.comboClinic);
			this.Controls.Add(this.comboProvider);
			this.Controls.Add(this.labelProvider);
			this.Controls.Add(this.gridMain);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormProcCodeEditMore";
			this.Text = "More Fees";
			this.Load += new System.EventHandler(this.FormProcCodeEditMore_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private UI.GridOD gridMain;
		private System.Windows.Forms.Label labelProvider;
		private UI.ComboBox comboProvider;
		private UI.ComboBoxClinicPicker comboClinic;
	}
}