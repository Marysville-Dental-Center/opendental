using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormRxSetup {
		private System.ComponentModel.IContainer components = null;

		///<summary></summary>
		protected override void Dispose( bool disposing ){
			if( disposing ){
				if(components != null){
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
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butAdd2 = new OpenPmsDental.UI.Button();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.checkProcCodeRequired = new OpenPmsDental.UI.CheckBox();
			this.butCombine = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			// 
			// butAdd
			// 
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(106, 636);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(88, 26);
			this.butAdd.TabIndex = 14;
			this.butAdd.Text = "Add &New";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			// 
			// butAdd2
			// 
			this.butAdd2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd2.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd2.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd2.Location = new System.Drawing.Point(12, 636);
			this.butAdd2.Name = "butAdd2";
			this.butAdd2.Size = new System.Drawing.Size(88, 26);
			this.butAdd2.TabIndex = 16;
			this.butAdd2.Text = "Duplicate";
			this.butAdd2.Click += new System.EventHandler(this.butAdd2_Click);
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 37);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.MultiExtended;
			this.gridMain.Size = new System.Drawing.Size(913, 590);
			this.gridMain.TabIndex = 17;
			this.gridMain.Title = "Prescriptions";
			this.gridMain.TranslationName = "TableRxSetup";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			// 
			// checkProcCodeRequired
			// 
			this.checkProcCodeRequired.Location = new System.Drawing.Point(12, 12);
			this.checkProcCodeRequired.Name = "checkProcCodeRequired";
			this.checkProcCodeRequired.Size = new System.Drawing.Size(913, 24);
			this.checkProcCodeRequired.TabIndex = 18;
			this.checkProcCodeRequired.Text = "Procedure code required on some prescriptions";
			this.checkProcCodeRequired.Click += new System.EventHandler(this.checkProcCodeRequired_Click);
			// 
			// butCombine
			// 
			this.butCombine.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
			this.butCombine.Location = new System.Drawing.Point(426, 636);
			this.butCombine.Name = "butCombine";
			this.butCombine.Size = new System.Drawing.Size(90, 26);
			this.butCombine.TabIndex = 19;
			this.butCombine.Text = "Co&mbine";
			this.butCombine.Click += new System.EventHandler(this.butCombine_Click);
			// 
			// FormRxSetup
			// 
			this.ClientSize = new System.Drawing.Size(942, 674);
			this.Controls.Add(this.butCombine);
			this.Controls.Add(this.checkProcCodeRequired);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butAdd2);
			this.Controls.Add(this.butAdd);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormRxSetup";
			this.ShowInTaskbar = false;
			this.Text = "Rx Setup";
			this.Load += new System.EventHandler(this.FormRxSetup_Load);
			this.ResumeLayout(false);

		}
		#endregion

		private OpenPmsDental.UI.Button butAdd;
		private OpenPmsDental.UI.Button butAdd2;
		private UI.GridOD gridMain;// Required designer variable.
		private OpenPmsDental.UI.CheckBox checkProcCodeRequired;
		private UI.Button butCombine;
	}
}
