namespace OpenPmsDental {
	partial class FormRpUdsPlus {
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
			this.labelDates = new System.Windows.Forms.Label();
			this.dateRangePicker = new OpenPmsDental.UI.ODDateRangePicker();
			this.comboClinics = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.labelProv = new System.Windows.Forms.Label();
			this.checkAllProv = new OpenPmsDental.UI.CheckBox();
			this.listProv = new OpenPmsDental.UI.ListBox();
			this.labelLines = new System.Windows.Forms.Label();
			this.butSetup = new OpenPmsDental.UI.Button();
			this.labelExport = new System.Windows.Forms.Label();
			this.butReport = new OpenPmsDental.UI.Button();
			this.butExport = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelDates
			//
			this.labelDates.Location = new System.Drawing.Point(12, 9);
			this.labelDates.Name = "labelDates";
			this.labelDates.Size = new System.Drawing.Size(400, 18);
			this.labelDates.TabIndex = 0;
			this.labelDates.Text = "Completed procedures dated";
			this.labelDates.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// dateRangePicker
			//
			this.dateRangePicker.BackColor = System.Drawing.Color.Transparent;
			this.dateRangePicker.EnableWeekButtons = false;
			this.dateRangePicker.Location = new System.Drawing.Point(12, 29);
			this.dateRangePicker.MaximumSize = new System.Drawing.Size(0, 185);
			this.dateRangePicker.MinimumSize = new System.Drawing.Size(400, 22);
			this.dateRangePicker.Name = "dateRangePicker";
			this.dateRangePicker.Size = new System.Drawing.Size(400, 22);
			this.dateRangePicker.TabIndex = 1;
			//
			// comboClinics
			//
			this.comboClinics.IncludeAll = true;
			this.comboClinics.IncludeHiddenInAll = true;
			this.comboClinics.IncludeUnassigned = true;
			this.comboClinics.IsMultiSelect = true;
			this.comboClinics.Location = new System.Drawing.Point(12, 61);
			this.comboClinics.Name = "comboClinics";
			this.comboClinics.Size = new System.Drawing.Size(230, 21);
			this.comboClinics.TabIndex = 2;
			//
			// labelProv
			//
			this.labelProv.Location = new System.Drawing.Point(12, 92);
			this.labelProv.Name = "labelProv";
			this.labelProv.Size = new System.Drawing.Size(120, 18);
			this.labelProv.TabIndex = 3;
			this.labelProv.Text = "Providers";
			this.labelProv.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// checkAllProv
			//
			this.checkAllProv.Checked = true;
			this.checkAllProv.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkAllProv.Location = new System.Drawing.Point(12, 112);
			this.checkAllProv.Name = "checkAllProv";
			this.checkAllProv.Size = new System.Drawing.Size(95, 16);
			this.checkAllProv.TabIndex = 4;
			this.checkAllProv.Text = "All";
			this.checkAllProv.Click += new System.EventHandler(this.checkAllProv_Click);
			//
			// listProv
			//
			this.listProv.Location = new System.Drawing.Point(12, 131);
			this.listProv.Name = "listProv";
			this.listProv.SelectionMode = OpenPmsDental.UI.SelectionMode.MultiExtended;
			this.listProv.Size = new System.Drawing.Size(230, 160);
			this.listProv.TabIndex = 5;
			this.listProv.Click += new System.EventHandler(this.listProv_Click);
			//
			// labelLines
			//
			this.labelLines.Location = new System.Drawing.Point(260, 92);
			this.labelLines.Name = "labelLines";
			this.labelLines.Size = new System.Drawing.Size(288, 60);
			this.labelLines.TabIndex = 6;
			this.labelLines.Text = "UDS lines set up:";
			//
			// butSetup
			//
			this.butSetup.Location = new System.Drawing.Point(260, 155);
			this.butSetup.Name = "butSetup";
			this.butSetup.Size = new System.Drawing.Size(90, 24);
			this.butSetup.TabIndex = 7;
			this.butSetup.Text = "&Setup";
			this.butSetup.Click += new System.EventHandler(this.butSetup_Click);
			//
			// labelExport
			//
			this.labelExport.Location = new System.Drawing.Point(260, 195);
			this.labelExport.Name = "labelExport";
			this.labelExport.Size = new System.Drawing.Size(288, 96);
			this.labelExport.TabIndex = 8;
			this.labelExport.Text = "Report shows the visits and patients of each UDS line. Save File writes the patient-level file (one row per patient, visit and line) for the center\'s own UDS+ tools. Nothing is sent anywhere.";
			//
			// butReport
			//
			this.butReport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butReport.Location = new System.Drawing.Point(258, 304);
			this.butReport.Name = "butReport";
			this.butReport.Size = new System.Drawing.Size(95, 24);
			this.butReport.TabIndex = 9;
			this.butReport.Text = "&Report";
			this.butReport.Click += new System.EventHandler(this.butReport_Click);
			//
			// butExport
			//
			this.butExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butExport.Location = new System.Drawing.Point(358, 304);
			this.butExport.Name = "butExport";
			this.butExport.Size = new System.Drawing.Size(105, 24);
			this.butExport.TabIndex = 10;
			this.butExport.Text = "Save &File...";
			this.butExport.Click += new System.EventHandler(this.butExport_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(473, 304);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 11;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormRpUdsPlus
			//
			this.ClientSize = new System.Drawing.Size(560, 340);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butExport);
			this.Controls.Add(this.butReport);
			this.Controls.Add(this.labelExport);
			this.Controls.Add(this.butSetup);
			this.Controls.Add(this.labelLines);
			this.Controls.Add(this.listProv);
			this.Controls.Add(this.checkAllProv);
			this.Controls.Add(this.labelProv);
			this.Controls.Add(this.comboClinics);
			this.Controls.Add(this.dateRangePicker);
			this.Controls.Add(this.labelDates);
			this.Name = "FormRpUdsPlus";
			this.Text = "UDS+ Report for FQHC Dental Services";
			this.Load += new System.EventHandler(this.FormRpUdsPlus_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelDates;
		private OpenPmsDental.UI.ODDateRangePicker dateRangePicker;
		private OpenPmsDental.UI.ComboBoxClinicPicker comboClinics;
		private System.Windows.Forms.Label labelProv;
		private OpenPmsDental.UI.CheckBox checkAllProv;
		private OpenPmsDental.UI.ListBox listProv;
		private System.Windows.Forms.Label labelLines;
		private OpenPmsDental.UI.Button butSetup;
		private System.Windows.Forms.Label labelExport;
		private OpenPmsDental.UI.Button butReport;
		private OpenPmsDental.UI.Button butExport;
		private OpenPmsDental.UI.Button butClose;
	}
}
