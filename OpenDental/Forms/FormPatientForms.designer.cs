namespace OpenDental{
	partial class FormPatientForms {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormPatientForms));
			this.gridMain = new OpenDental.UI.GridOD();
			this.menuMain = new OpenDental.UI.MenuOD();
			this.toolBarMain = new OpenDental.UI.ToolBarOD();
			this.groupSortBy = new OpenDental.UI.GroupBox();
			this.radioSortByDateTime = new System.Windows.Forms.RadioButton();
			this.radioSortByDescDateT = new System.Windows.Forms.RadioButton();
			this.groupShow = new OpenDental.UI.GroupBox();
			this.radioShowBoth = new System.Windows.Forms.RadioButton();
			this.radioShowSheets = new System.Windows.Forms.RadioButton();
			this.radioShowEForms = new System.Windows.Forms.RadioButton();
			this.groupSortBy.SuspendLayout();
			this.groupShow.SuspendLayout();
			this.SuspendLayout();
			//
			// gridMain
			//
			this.gridMain.AllowSortingByColumn = true;
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 123);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(722, 563);
			this.gridMain.TabIndex = 4;
			this.gridMain.Title = "Patient Forms and Medical Histories";
			this.gridMain.TranslationName = "FormPatientForms";
			this.gridMain.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			this.gridMain.SelectionCommitted += new System.EventHandler(this.gridMain_SelectionCommitted);
			//
			// menuMain
			//
			this.menuMain.Dock = System.Windows.Forms.DockStyle.Top;
			this.menuMain.Location = new System.Drawing.Point(0, 0);
			this.menuMain.Name = "menuMain";
			this.menuMain.Size = new System.Drawing.Size(745, 24);
			this.menuMain.TabIndex = 12;
			//
			// toolBarMain
			//
			this.toolBarMain.Dock = System.Windows.Forms.DockStyle.Top;
			this.toolBarMain.Location = new System.Drawing.Point(0, 24);
			this.toolBarMain.Name = "toolBarMain";
			this.toolBarMain.Size = new System.Drawing.Size(745, 25);
			this.toolBarMain.TabIndex = 0;
			//
			// groupSortBy
			//
			this.groupSortBy.BackColor = System.Drawing.Color.White;
			this.groupSortBy.Controls.Add(this.radioSortByDateTime);
			this.groupSortBy.Controls.Add(this.radioSortByDescDateT);
			this.groupSortBy.Location = new System.Drawing.Point(12, 55);
			this.groupSortBy.Name = "groupSortBy";
			this.groupSortBy.Size = new System.Drawing.Size(290, 62);
			this.groupSortBy.TabIndex = 251;
			this.groupSortBy.Text = "Sort forms by";
			//
			// radioSortByDateTime
			//
			this.radioSortByDateTime.Checked = true;
			this.radioSortByDateTime.Location = new System.Drawing.Point(24, 18);
			this.radioSortByDateTime.Name = "radioSortByDateTime";
			this.radioSortByDateTime.Size = new System.Drawing.Size(184, 18);
			this.radioSortByDateTime.TabIndex = 252;
			this.radioSortByDateTime.TabStop = true;
			this.radioSortByDateTime.Text = "Date and Time";
			this.radioSortByDateTime.UseVisualStyleBackColor = true;
			this.radioSortByDateTime.Click += new System.EventHandler(this.radioSortByDateTime_Click);
			//
			// radioSortByDescDateT
			//
			this.radioSortByDescDateT.Location = new System.Drawing.Point(24, 38);
			this.radioSortByDescDateT.Name = "radioSortByDescDateT";
			this.radioSortByDescDateT.Size = new System.Drawing.Size(236, 18);
			this.radioSortByDescDateT.TabIndex = 251;
			this.radioSortByDescDateT.Text = "Description, then Date/Time";
			this.radioSortByDescDateT.UseVisualStyleBackColor = true;
			this.radioSortByDescDateT.Click += new System.EventHandler(this.radioSortByDescriptionDateTime_Click);
			//
			// groupShow
			//
			this.groupShow.BackColor = System.Drawing.Color.White;
			this.groupShow.Controls.Add(this.radioShowBoth);
			this.groupShow.Controls.Add(this.radioShowSheets);
			this.groupShow.Controls.Add(this.radioShowEForms);
			this.groupShow.Location = new System.Drawing.Point(312, 55);
			this.groupShow.Name = "groupShow";
			this.groupShow.Size = new System.Drawing.Size(250, 62);
			this.groupShow.TabIndex = 253;
			this.groupShow.Text = "Show";
			//
			// radioShowBoth
			//
			this.radioShowBoth.Checked = true;
			this.radioShowBoth.Location = new System.Drawing.Point(16, 18);
			this.radioShowBoth.Name = "radioShowBoth";
			this.radioShowBoth.Size = new System.Drawing.Size(110, 18);
			this.radioShowBoth.TabIndex = 254;
			this.radioShowBoth.TabStop = true;
			this.radioShowBoth.Text = "Both";
			this.radioShowBoth.UseVisualStyleBackColor = true;
			this.radioShowBoth.Click += new System.EventHandler(this.radioShow_Click);
			//
			// radioShowSheets
			//
			this.radioShowSheets.Location = new System.Drawing.Point(16, 38);
			this.radioShowSheets.Name = "radioShowSheets";
			this.radioShowSheets.Size = new System.Drawing.Size(110, 18);
			this.radioShowSheets.TabIndex = 255;
			this.radioShowSheets.Text = "Sheets";
			this.radioShowSheets.UseVisualStyleBackColor = true;
			this.radioShowSheets.Click += new System.EventHandler(this.radioShow_Click);
			//
			// radioShowEForms
			//
			this.radioShowEForms.Location = new System.Drawing.Point(130, 18);
			this.radioShowEForms.Name = "radioShowEForms";
			this.radioShowEForms.Size = new System.Drawing.Size(110, 18);
			this.radioShowEForms.TabIndex = 256;
			this.radioShowEForms.Text = "eForms";
			this.radioShowEForms.UseVisualStyleBackColor = true;
			this.radioShowEForms.Click += new System.EventHandler(this.radioShow_Click);
			//
			// FormPatientForms
			//
			this.ClientSize = new System.Drawing.Size(745, 696);
			this.Controls.Add(this.groupShow);
			this.Controls.Add(this.groupSortBy);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.toolBarMain);
			this.Controls.Add(this.menuMain);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormPatientForms";
			this.Text = "Patient Forms and Medical Histories";
			this.Load += new System.EventHandler(this.FormPatientForms_Load);
			this.groupSortBy.ResumeLayout(false);
			this.groupShow.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion
		private OpenDental.UI.GridOD gridMain;
		private UI.MenuOD menuMain;
		private OpenDental.UI.ToolBarOD toolBarMain;
		private UI.GroupBox groupSortBy;
		private System.Windows.Forms.RadioButton radioSortByDateTime;
		private System.Windows.Forms.RadioButton radioSortByDescDateT;
		private UI.GroupBox groupShow;
		private System.Windows.Forms.RadioButton radioShowBoth;
		private System.Windows.Forms.RadioButton radioShowSheets;
		private System.Windows.Forms.RadioButton radioShowEForms;
	}
}
