using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormClaimForms {
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
			this.groupBox2 = new OpenPmsDental.UI.GroupBox();
			this.comboReassign = new OpenPmsDental.UI.ComboBox();
			this.butReassign = new OpenPmsDental.UI.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.butDuplicate = new OpenPmsDental.UI.Button();
			this.butExport = new OpenPmsDental.UI.Button();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butImport = new OpenPmsDental.UI.Button();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.gridCustom = new OpenPmsDental.UI.GridOD();
			this.gridInternal = new OpenPmsDental.UI.GridOD();
			this.butCopy = new OpenPmsDental.UI.Button();
			this.butDefault = new OpenPmsDental.UI.Button();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.comboReassign);
			this.groupBox2.Controls.Add(this.butReassign);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Location = new System.Drawing.Point(540, 71);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(232, 122);
			this.groupBox2.TabIndex = 12;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Reassign";
			// 
			// comboReassign
			// 
			this.comboReassign.Location = new System.Drawing.Point(15, 64);
			this.comboReassign.Name = "comboReassign";
			this.comboReassign.Size = new System.Drawing.Size(169, 21);
			this.comboReassign.TabIndex = 15;
			// 
			// butReassign
			// 
			this.butReassign.Location = new System.Drawing.Point(15, 87);
			this.butReassign.Name = "butReassign";
			this.butReassign.Size = new System.Drawing.Size(87, 23);
			this.butReassign.TabIndex = 13;
			this.butReassign.Text = "Reassign";
			this.butReassign.Click += new System.EventHandler(this.butReassign_Click);
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(12, 16);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(203, 47);
			this.label2.TabIndex = 14;
			this.label2.Text = "Reassign all insurance plans that use the selected claim form at the left to the " +
    "claim form below";
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this.butDuplicate);
			this.groupBox1.Controls.Add(this.butExport);
			this.groupBox1.Controls.Add(this.butAdd);
			this.groupBox1.Controls.Add(this.butImport);
			this.groupBox1.Controls.Add(this.butDelete);
			this.groupBox1.Location = new System.Drawing.Point(540, 258);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(232, 122);
			this.groupBox1.TabIndex = 11;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Custom Claim Form Options";
			// 
			// butDuplicate
			// 
			this.butDuplicate.Location = new System.Drawing.Point(15, 87);
			this.butDuplicate.Name = "butDuplicate";
			this.butDuplicate.Size = new System.Drawing.Size(75, 23);
			this.butDuplicate.TabIndex = 4;
			this.butDuplicate.Text = "Duplicate";
			this.butDuplicate.Click += new System.EventHandler(this.butDuplicate_Click);
			// 
			// butExport
			// 
			this.butExport.Location = new System.Drawing.Point(141, 25);
			this.butExport.Name = "butExport";
			this.butExport.Size = new System.Drawing.Size(75, 23);
			this.butExport.TabIndex = 9;
			this.butExport.Text = "Export";
			this.butExport.Click += new System.EventHandler(this.butExport_Click);
			// 
			// butAdd
			// 
			this.butAdd.Location = new System.Drawing.Point(15, 25);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(75, 23);
			this.butAdd.TabIndex = 3;
			this.butAdd.Text = "&Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			// 
			// butImport
			// 
			this.butImport.Location = new System.Drawing.Point(141, 56);
			this.butImport.Name = "butImport";
			this.butImport.Size = new System.Drawing.Size(75, 23);
			this.butImport.TabIndex = 10;
			this.butImport.Text = "Import";
			this.butImport.Click += new System.EventHandler(this.butImport_Click);
			// 
			// butDelete
			// 
			this.butDelete.Location = new System.Drawing.Point(15, 56);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(75, 23);
			this.butDelete.TabIndex = 5;
			this.butDelete.Text = "Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(5, 7);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(263, 28);
			this.label1.TabIndex = 19;
			this.label1.Text = "Internal forms cannot be edited or printed. \r\nCopy the form over to a custom form" +
    " first.";
			// 
			// gridCustom
			// 
			this.gridCustom.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridCustom.Location = new System.Drawing.Point(274, 38);
			this.gridCustom.Name = "gridCustom";
			this.gridCustom.Size = new System.Drawing.Size(260, 372);
			this.gridCustom.TabIndex = 17;
			this.gridCustom.Title = "Custom Claim Forms";
			this.gridCustom.TranslationName = "TableClaimFormsCustom";
			this.gridCustom.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridCustom_CellDoubleClick);
			// 
			// gridInternal
			// 
			this.gridInternal.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridInternal.Location = new System.Drawing.Point(8, 38);
			this.gridInternal.Name = "gridInternal";
			this.gridInternal.Size = new System.Drawing.Size(186, 372);
			this.gridInternal.TabIndex = 16;
			this.gridInternal.Title = "Internal Claim Forms";
			this.gridInternal.TranslationName = "TableClaimFormsInternal";
			this.gridInternal.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridInternal_CellDoubleClick);
			// 
			// butCopy
			// 
			this.butCopy.Image = global::OpenPmsDental.Properties.Resources.Right;
			this.butCopy.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butCopy.Location = new System.Drawing.Point(200, 221);
			this.butCopy.Name = "butCopy";
			this.butCopy.Size = new System.Drawing.Size(68, 23);
			this.butCopy.TabIndex = 18;
			this.butCopy.Text = "Copy";
			this.butCopy.Click += new System.EventHandler(this.butCopy_Click);
			// 
			// butDefault
			// 
			this.butDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butDefault.Location = new System.Drawing.Point(540, 38);
			this.butDefault.Name = "butDefault";
			this.butDefault.Size = new System.Drawing.Size(87, 23);
			this.butDefault.TabIndex = 12;
			this.butDefault.Text = "Set Default";
			this.butDefault.Click += new System.EventHandler(this.butDefault_Click);
			// 
			// FormClaimForms
			// 
			this.ClientSize = new System.Drawing.Size(785, 424);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.butCopy);
			this.Controls.Add(this.gridCustom);
			this.Controls.Add(this.gridInternal);
			this.Controls.Add(this.butDefault);
			this.Controls.Add(this.groupBox1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormClaimForms";
			this.ShowInTaskbar = false;
			this.Text = "Claim Forms";
			this.Closing += new System.ComponentModel.CancelEventHandler(this.FormClaimForms_Closing);
			this.Load += new System.EventHandler(this.FormClaimForms_Load);
			this.groupBox2.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		private OpenPmsDental.UI.Button butAdd;
		private OpenPmsDental.UI.Button butDuplicate;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.UI.Button butExport;
		private OpenPmsDental.UI.Button butImport;
		private OpenPmsDental.UI.GroupBox groupBox1;
		private OpenPmsDental.UI.Button butDefault;
		private OpenPmsDental.UI.Button butReassign;
		private Label label2;
		private OpenPmsDental.UI.ComboBox comboReassign;
		private UI.GridOD gridInternal;
		private UI.GridOD gridCustom;
		private UI.Button butCopy;
		private OpenPmsDental.UI.GroupBox groupBox2;
		private Label label1;
	}
}
