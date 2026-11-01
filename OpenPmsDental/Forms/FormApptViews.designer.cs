using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormApptViews {
		/// <summary>
		/// Required designer variable.
		/// </summary>
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormApptViews));
			this.label1 = new System.Windows.Forms.Label();
			this.listViews = new OpenPmsDental.UI.ListBox();
			this.butDown = new OpenPmsDental.UI.Button();
			this.butUp = new OpenPmsDental.UI.Button();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.radioFive = new System.Windows.Forms.RadioButton();
			this.radioFifteen = new System.Windows.Forms.RadioButton();
			this.radioTen = new System.Windows.Forms.RadioButton();
			this.checkTwoRows = new OpenPmsDental.UI.CheckBox();
			this.butProcColors = new OpenPmsDental.UI.Button();
			this.comboClinic = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.butCopy = new OpenPmsDental.UI.Button();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(57, 40);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(158, 18);
			this.label1.TabIndex = 1;
			this.label1.Text = "Views";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// listViews
			// 
			this.listViews.Location = new System.Drawing.Point(56, 60);
			this.listViews.Name = "listViews";
			this.listViews.Size = new System.Drawing.Size(183, 329);
			this.listViews.TabIndex = 2;
			this.listViews.DoubleClick += new System.EventHandler(this.listViews_DoubleClick);
			this.listViews.SelectedIndexChanged += new System.EventHandler(this.listViews_SelectedIndexChanged);
			// 
			// butDown
			// 
			this.butDown.Image = global::OpenPmsDental.Properties.Resources.down;
			this.butDown.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDown.Location = new System.Drawing.Point(151, 437);
			this.butDown.Name = "butDown";
			this.butDown.Size = new System.Drawing.Size(89, 24);
			this.butDown.TabIndex = 38;
			this.butDown.Text = "&Down";
			this.butDown.Click += new System.EventHandler(this.butDown_Click);
			// 
			// butUp
			// 
			this.butUp.Image = global::OpenPmsDental.Properties.Resources.up;
			this.butUp.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butUp.Location = new System.Drawing.Point(151, 399);
			this.butUp.Name = "butUp";
			this.butUp.Size = new System.Drawing.Size(89, 24);
			this.butUp.TabIndex = 39;
			this.butUp.Text = "&Up";
			this.butUp.Click += new System.EventHandler(this.butUp_Click);
			// 
			// butAdd
			// 
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(55, 399);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(89, 24);
			this.butAdd.TabIndex = 36;
			this.butAdd.Text = "&Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.radioFive);
			this.groupBox1.Controls.Add(this.radioFifteen);
			this.groupBox1.Controls.Add(this.radioTen);
			this.groupBox1.Location = new System.Drawing.Point(279, 54);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(169, 82);
			this.groupBox1.TabIndex = 40;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Time Increments";
			// 
			// radioFive
			// 
			this.radioFive.Location = new System.Drawing.Point(23, 19);
			this.radioFive.Name = "radioFive";
			this.radioFive.Size = new System.Drawing.Size(100, 18);
			this.radioFive.TabIndex = 2;
			this.radioFive.Text = "5 Min";
			// 
			// radioFifteen
			// 
			this.radioFifteen.Location = new System.Drawing.Point(23, 57);
			this.radioFifteen.Name = "radioFifteen";
			this.radioFifteen.Size = new System.Drawing.Size(100, 18);
			this.radioFifteen.TabIndex = 1;
			this.radioFifteen.Text = "15 Min";
			// 
			// radioTen
			// 
			this.radioTen.Location = new System.Drawing.Point(23, 38);
			this.radioTen.Name = "radioTen";
			this.radioTen.Size = new System.Drawing.Size(100, 18);
			this.radioTen.TabIndex = 0;
			this.radioTen.Text = "10 Min";
			// 
			// checkTwoRows
			// 
			this.checkTwoRows.Location = new System.Drawing.Point(0, 0);
			this.checkTwoRows.Name = "checkTwoRows";
			this.checkTwoRows.Size = new System.Drawing.Size(104, 24);
			this.checkTwoRows.TabIndex = 0;
			// 
			// butProcColors
			// 
			this.butProcColors.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.butProcColors.Location = new System.Drawing.Point(279, 159);
			this.butProcColors.Name = "butProcColors";
			this.butProcColors.Size = new System.Drawing.Size(82, 24);
			this.butProcColors.TabIndex = 41;
			this.butProcColors.Text = "Proc Colors";
			this.butProcColors.Click += new System.EventHandler(this.butProcColors_Click);
			// 
			// comboClinic
			// 
			this.comboClinic.HqDescription = "Headquarters";
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(55, 12);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(185, 21);
			this.comboClinic.TabIndex = 135;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboClinic_SelectionChangeCommitted);
			//
			// butCopy
			//
			this.butCopy.Enabled = false;
			this.butCopy.Location = new System.Drawing.Point(55, 437);
			this.butCopy.Name = "butCopy";
			this.butCopy.Size = new System.Drawing.Size(89, 24);
			this.butCopy.TabIndex = 37;
			this.butCopy.Text = "Copy View";
			this.butCopy.Click += new System.EventHandler(this.butCopy_Click);
			//
			// FormApptViews
			//
			this.ClientSize = new System.Drawing.Size(476,485);
			this.Controls.Add(this.butCopy);
			this.Controls.Add(this.comboClinic);
			this.Controls.Add(this.butProcColors);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.butDown);
			this.Controls.Add(this.butUp);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.listViews);
			this.Controls.Add(this.label1);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormApptViews";
			this.ShowInTaskbar = false;
			this.Text = "Appointment Views";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormApptViews_FormClosing);
			this.Load += new System.EventHandler(this.FormApptViews_Load);
			this.groupBox1.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		private System.Windows.Forms.Label label1;
		private OpenPmsDental.UI.Button butDown;
		private OpenPmsDental.UI.Button butUp;
		private OpenPmsDental.UI.Button butAdd;
		private OpenPmsDental.UI.ListBox listViews;
		private OpenPmsDental.UI.GroupBox groupBox1;
		private System.Windows.Forms.RadioButton radioTen;
		private System.Windows.Forms.RadioButton radioFifteen;
		private OpenPmsDental.UI.CheckBox checkTwoRows;
		private RadioButton radioFive;
		private OpenPmsDental.UI.Button butProcColors;
		private OpenPmsDental.UI.ComboBoxClinicPicker comboClinic;
		private OpenPmsDental.UI.Button butCopy;
	}
}
