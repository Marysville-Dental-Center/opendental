using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenDental {
	public partial class FormRpProcedures {
		private System.ComponentModel.IContainer components = null;

		///<summary></summary>
		protected override void Dispose(bool disposing) {
			if(disposing) {
				if(components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormRpProcedures));
			this.groupFilter = new OpenDental.UI.GroupBox();
			this.labelCode = new System.Windows.Forms.Label();
			this.textCode = new System.Windows.Forms.TextBox();
			this.labelProv = new System.Windows.Forms.Label();
			this.comboProvs = new OpenDental.UI.ComboBox();
			this.comboClinics = new OpenDental.UI.ComboBoxClinicPicker();
			this.dateRangePicker = new OpenDental.UI.ODDateRangePicker();
			this.butRefresh = new OpenDental.UI.Button();
			this.gridMain = new OpenDental.UI.GridOD();
			this.contextMenuGrid = new System.Windows.Forms.ContextMenu();
			this.menuItemGoToPatient = new System.Windows.Forms.MenuItem();
			this.labelTotal = new System.Windows.Forms.Label();
			this.butPrint = new OpenDental.UI.Button();
			this.butExport = new OpenDental.UI.Button();
			this.butClose = new OpenDental.UI.Button();
			this.groupFilter.SuspendLayout();
			this.SuspendLayout();
			//
			// groupFilter
			//
			this.groupFilter.Controls.Add(this.labelCode);
			this.groupFilter.Controls.Add(this.textCode);
			this.groupFilter.Controls.Add(this.labelProv);
			this.groupFilter.Controls.Add(this.comboProvs);
			this.groupFilter.Controls.Add(this.comboClinics);
			this.groupFilter.Controls.Add(this.dateRangePicker);
			this.groupFilter.Location = new System.Drawing.Point(12, 4);
			this.groupFilter.Name = "groupFilter";
			this.groupFilter.Size = new System.Drawing.Size(890, 64);
			this.groupFilter.TabIndex = 0;
			this.groupFilter.Text = "Filters";
			//
			// labelCode
			//
			this.labelCode.Location = new System.Drawing.Point(642, 15);
			this.labelCode.Name = "labelCode";
			this.labelCode.Size = new System.Drawing.Size(240, 16);
			this.labelCode.TabIndex = 6;
			this.labelCode.Text = "Only for procedure codes similar to:";
			this.labelCode.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textCode
			//
			this.textCode.Location = new System.Drawing.Point(645, 34);
			this.textCode.Name = "textCode";
			this.textCode.Size = new System.Drawing.Size(100, 20);
			this.textCode.TabIndex = 4;
			//
			// labelProv
			//
			this.labelProv.Location = new System.Drawing.Point(412, 15);
			this.labelProv.Name = "labelProv";
			this.labelProv.Size = new System.Drawing.Size(55, 16);
			this.labelProv.TabIndex = 5;
			this.labelProv.Text = "Providers";
			this.labelProv.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboProvs
			//
			this.comboProvs.BackColor = System.Drawing.SystemColors.Window;
			this.comboProvs.Location = new System.Drawing.Point(469, 12);
			this.comboProvs.Name = "comboProvs";
			this.comboProvs.SelectionModeMulti = true;
			this.comboProvs.Size = new System.Drawing.Size(160, 21);
			this.comboProvs.TabIndex = 2;
			//
			// comboClinics
			//
			this.comboClinics.IncludeAll = true;
			this.comboClinics.IncludeHiddenInAll = true;
			this.comboClinics.IncludeUnassigned = true;
			this.comboClinics.IsMultiSelect = true;
			this.comboClinics.Location = new System.Drawing.Point(432, 36);
			this.comboClinics.Name = "comboClinics";
			this.comboClinics.Size = new System.Drawing.Size(197, 21);
			this.comboClinics.TabIndex = 3;
			//
			// dateRangePicker
			//
			this.dateRangePicker.BackColor = System.Drawing.Color.Transparent;
			this.dateRangePicker.EnableWeekButtons = false;
			this.dateRangePicker.Location = new System.Drawing.Point(5, 12);
			this.dateRangePicker.MaximumSize = new System.Drawing.Size(0, 185);
			this.dateRangePicker.MinimumSize = new System.Drawing.Size(400, 22);
			this.dateRangePicker.Name = "dateRangePicker";
			this.dateRangePicker.Size = new System.Drawing.Size(400, 22);
			this.dateRangePicker.TabIndex = 1;
			//
			// butRefresh
			//
			this.butRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRefresh.Location = new System.Drawing.Point(913, 40);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 26);
			this.butRefresh.TabIndex = 1;
			this.butRefresh.Text = "&Refresh";
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			//
			// gridMain
			//
			this.gridMain.AllowSortingByColumn = true;
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 74);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(976, 504);
			this.gridMain.TabIndex = 2;
			this.gridMain.Title = "Procedures";
			this.gridMain.TranslationName = "TableProcedures";
			this.gridMain.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			//
			// contextMenuGrid
			//
			this.contextMenuGrid.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemGoToPatient});
			//
			// menuItemGoToPatient
			//
			this.menuItemGoToPatient.Index = 0;
			this.menuItemGoToPatient.Text = "Go to Patient";
			this.menuItemGoToPatient.Click += new System.EventHandler(this.menuItemGoToPatient_Click);
			//
			// labelTotal
			//
			this.labelTotal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.labelTotal.Location = new System.Drawing.Point(588, 582);
			this.labelTotal.Name = "labelTotal";
			this.labelTotal.Size = new System.Drawing.Size(400, 18);
			this.labelTotal.TabIndex = 6;
			this.labelTotal.Text = "Total";
			this.labelTotal.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// butPrint
			//
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butPrint.Image = global::OpenDental.Properties.Resources.butPrintSmall;
			this.butPrint.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butPrint.Location = new System.Drawing.Point(12, 606);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(79, 24);
			this.butPrint.TabIndex = 3;
			this.butPrint.Text = "&Print";
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			//
			// butExport
			//
			this.butExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butExport.Image = global::OpenDental.Properties.Resources.butExport;
			this.butExport.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butExport.Location = new System.Drawing.Point(97, 606);
			this.butExport.Name = "butExport";
			this.butExport.Size = new System.Drawing.Size(79, 24);
			this.butExport.TabIndex = 4;
			this.butExport.Text = "&Export";
			this.butExport.Click += new System.EventHandler(this.butExport_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(913, 606);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 5;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormRpProcedures
			//
			this.ClientSize = new System.Drawing.Size(1000, 642);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butExport);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.labelTotal);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.groupFilter);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormRpProcedures";
			this.Text = "Procedures Report";
			this.Load += new System.EventHandler(this.FormRpProcedures_Load);
			this.groupFilter.ResumeLayout(false);
			this.groupFilter.PerformLayout();
			this.ResumeLayout(false);

		}
		#endregion

		private OpenDental.UI.GroupBox groupFilter;
		private System.Windows.Forms.Label labelCode;
		private System.Windows.Forms.TextBox textCode;
		private System.Windows.Forms.Label labelProv;
		private OpenDental.UI.ComboBox comboProvs;
		private OpenDental.UI.ComboBoxClinicPicker comboClinics;
		private OpenDental.UI.ODDateRangePicker dateRangePicker;
		private OpenDental.UI.Button butRefresh;
		private OpenDental.UI.GridOD gridMain;
		private System.Windows.Forms.ContextMenu contextMenuGrid;
		private System.Windows.Forms.MenuItem menuItemGoToPatient;
		private System.Windows.Forms.Label labelTotal;
		private OpenDental.UI.Button butPrint;
		private OpenDental.UI.Button butExport;
		private OpenDental.UI.Button butClose;
	}
}
