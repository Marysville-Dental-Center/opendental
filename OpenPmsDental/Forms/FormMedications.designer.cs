using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormMedications {
		/// <summary>Required designer variable.</summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMedications));
			this.butOK = new OpenPmsDental.UI.Button();
			this.butAddGeneric = new OpenPmsDental.UI.Button();
			this.butAddBrand = new OpenPmsDental.UI.Button();
			this.gridAllMedications = new OpenPmsDental.UI.GridOD();
			this.textSearch = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.butExportMedications = new OpenPmsDental.UI.Button();
			this.butImportMedications = new OpenPmsDental.UI.Button();
			this.checkShowHidden = new OpenPmsDental.UI.CheckBox();
			this.SuspendLayout();
			// 
			// butOK
			// 
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(858, 635);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 26);
			this.butOK.TabIndex = 1;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			// 
			// butAddGeneric
			// 
			this.butAddGeneric.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAddGeneric.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAddGeneric.Location = new System.Drawing.Point(15, 9);
			this.butAddGeneric.Name = "butAddGeneric";
			this.butAddGeneric.Size = new System.Drawing.Size(113, 26);
			this.butAddGeneric.TabIndex = 33;
			this.butAddGeneric.Text = "Add Generic";
			this.butAddGeneric.Click += new System.EventHandler(this.butAddGeneric_Click);
			// 
			// butAddBrand
			// 
			this.butAddBrand.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAddBrand.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAddBrand.Location = new System.Drawing.Point(134, 9);
			this.butAddBrand.Name = "butAddBrand";
			this.butAddBrand.Size = new System.Drawing.Size(113, 26);
			this.butAddBrand.TabIndex = 34;
			this.butAddBrand.Text = "Add Brand";
			this.butAddBrand.Click += new System.EventHandler(this.butAddBrand_Click);
			// 
			// gridAllMedications
			// 
			this.gridAllMedications.AllowSortingByColumn = true;
			this.gridAllMedications.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridAllMedications.Location = new System.Drawing.Point(14, 41);
			this.gridAllMedications.Name = "gridAllMedications";
			this.gridAllMedications.Size = new System.Drawing.Size(919, 588);
			this.gridAllMedications.TabIndex = 37;
			this.gridAllMedications.Title = "All Medications";
			this.gridAllMedications.TranslationName = "FormMedications";
			this.gridAllMedications.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridAllMedications_CellDoubleClick);
			this.gridAllMedications.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridAllMedications_CellClick);
			// 
			// textSearch
			// 
			this.textSearch.Location = new System.Drawing.Point(376, 12);
			this.textSearch.Name = "textSearch";
			this.textSearch.Size = new System.Drawing.Size(195, 20);
			this.textSearch.TabIndex = 0;
			this.textSearch.TextChanged += new System.EventHandler(this.textSearch_TextChanged);
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(248, 15);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(127, 17);
			this.label1.TabIndex = 39;
			this.label1.Text = "Search";
			this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// checkShowHidden
			// 
			this.checkShowHidden.Location = new System.Drawing.Point(587, 13);
			this.checkShowHidden.Name = "checkShowHidden";
			this.checkShowHidden.Size = new System.Drawing.Size(110, 20);
			this.checkShowHidden.TabIndex = 43;
			this.checkShowHidden.Text = "Show Hidden";
			this.checkShowHidden.Click += new System.EventHandler(this.checkShowHidden_Click);
			//
			// butExportMedications
			//
			this.butExportMedications.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butExportMedications.Location = new System.Drawing.Point(848, 9);
			this.butExportMedications.Name = "butExportMedications";
			this.butExportMedications.Size = new System.Drawing.Size(85, 26);
			this.butExportMedications.TabIndex = 42;
			this.butExportMedications.Text = "Export";
			this.butExportMedications.Click += new System.EventHandler(this.butExportMedications_Click);
			// 
			// butImportMedications
			// 
			this.butImportMedications.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butImportMedications.Location = new System.Drawing.Point(757, 9);
			this.butImportMedications.Name = "butImportMedications";
			this.butImportMedications.Size = new System.Drawing.Size(85, 26);
			this.butImportMedications.TabIndex = 41;
			this.butImportMedications.Text = "Import";
			this.butImportMedications.Click += new System.EventHandler(this.butImportMedications_Click);
			//
			// FormMedications
			//
			this.AcceptButton = this.butOK;
			this.ClientSize = new System.Drawing.Size(941, 671);
			this.Controls.Add(this.checkShowHidden);
			this.Controls.Add(this.butExportMedications);
			this.Controls.Add(this.butImportMedications);
			this.Controls.Add(this.gridAllMedications);
			this.Controls.Add(this.textSearch);
			this.Controls.Add(this.butAddGeneric);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.butAddBrand);
			this.Controls.Add(this.butOK);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormMedications";
			this.ShowInTaskbar = false;
			this.Text = "Medications";
			this.Load += new System.EventHandler(this.FormMedications_Load);
			this.Shown += new System.EventHandler(this.FormMedications_Shown);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion
		private OpenPmsDental.UI.Button butOK;
		private OpenPmsDental.UI.Button butAddGeneric;
		private OpenPmsDental.UI.Button butAddBrand;
		private OpenPmsDental.UI.GridOD gridAllMedications;
		public TextBox textSearch;
		private Label label1;
		private UI.Button butImportMedications;
		private UI.Button butExportMedications;
		private OpenPmsDental.UI.CheckBox checkShowHidden;
	}
}
