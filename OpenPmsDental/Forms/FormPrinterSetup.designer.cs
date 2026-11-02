using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormPrinterSetup {
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

		private void InitializeComponent(){
			this.label1 = new System.Windows.Forms.Label();
			this.butSave = new OpenPmsDental.UI.Button();
			this.checkSimple = new OpenPmsDental.UI.CheckBox();
			this.gridPrinters = new OpenPmsDental.UI.GridOD();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(19, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(438, 18);
			this.label1.TabIndex = 2;
			this.label1.Text = "These settings only apply to this workstation";
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(854, 368);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 2;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// checkSimple
			// 
			this.checkSimple.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.checkSimple.Location = new System.Drawing.Point(12, 367);
			this.checkSimple.Name = "checkSimple";
			this.checkSimple.Size = new System.Drawing.Size(440, 24);
			this.checkSimple.TabIndex = 33;
			this.checkSimple.Text = "This is too complicated.  Show me the simple interface.";
			this.checkSimple.Click += new System.EventHandler(this.checkSimple_Click);
			// 
			// gridPrinters
			// 
			this.gridPrinters.Location = new System.Drawing.Point(12, 27);
			this.gridPrinters.Name = "gridPrinters";
			this.gridPrinters.Size = new System.Drawing.Size(917, 334);
			this.gridPrinters.TabIndex = 42;
			this.gridPrinters.Title = "Print Situations";
			this.gridPrinters.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridPrinters_CellDoubleClick);
			// 
			// FormPrinterSetup
			// 
			this.AcceptButton = this.butSave;
			this.ClientSize = new System.Drawing.Size(941, 404);
			this.Controls.Add(this.gridPrinters);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.checkSimple);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormPrinterSetup";
			this.ShowInTaskbar = false;
			this.Text = "Printer Setup";
			this.Load += new System.EventHandler(this.FormPrinterSetup_Load);
			this.ResumeLayout(false);

		}
		#endregion

		private System.Windows.Forms.Label label1;
		private OpenPmsDental.UI.Button butSave;
		private OpenPmsDental.UI.CheckBox checkSimple;
		private UI.GridOD gridPrinters;
	}
}
