using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormInsPlansMergeWarning {
		private System.ComponentModel.IContainer components = null;// Required designer variable.

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
			this.labelWarning = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.butCombine = new OpenPmsDental.UI.Button();
			this.butCancel = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelWarning
			//
			this.labelWarning.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelWarning.Location = new System.Drawing.Point(12,9);
			this.labelWarning.Name = "labelWarning";
			this.labelWarning.Size = new System.Drawing.Size(935,56);
			this.labelWarning.TabIndex = 20;
			this.labelWarning.Text = "The plans below have fields that do not match the plan you selected.";
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(11,68);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(936,442);
			this.gridMain.TabIndex = 19;
			this.gridMain.Title = "Mismatched Fields";
			this.gridMain.TranslationName = "TableInsPlansMergeWarning";
			//
			// butCombine
			//
			this.butCombine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butCombine.Location = new System.Drawing.Point(785,520);
			this.butCombine.Name = "butCombine";
			this.butCombine.Size = new System.Drawing.Size(78,26);
			this.butCombine.TabIndex = 4;
			this.butCombine.Text = "C&ombine";
			this.butCombine.Click += new System.EventHandler(this.butCombine_Click);
			//
			// butCancel
			//
			this.butCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butCancel.Location = new System.Drawing.Point(869,520);
			this.butCancel.Name = "butCancel";
			this.butCancel.Size = new System.Drawing.Size(78,26);
			this.butCancel.TabIndex = 5;
			this.butCancel.Text = "&Cancel";
			this.butCancel.Click += new System.EventHandler(this.butCancel_Click);
			//
			// FormInsPlansMergeWarning
			//
			this.AutoScaleBaseSize = new System.Drawing.Size(5,13);
			this.ClientSize = new System.Drawing.Size(962,556);
			this.Controls.Add(this.butCancel);
			this.Controls.Add(this.butCombine);
			this.Controls.Add(this.labelWarning);
			this.Controls.Add(this.gridMain);
			this.Name = "FormInsPlansMergeWarning";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Combine Insurance Plans - Mismatched Fields";
			this.Load += new System.EventHandler(this.FormInsPlansMergeWarning_Load);
			this.ResumeLayout(false);

		}
		#endregion
		private OpenPmsDental.UI.Button butCombine;
		private OpenPmsDental.UI.Button butCancel;
		private OpenPmsDental.UI.GridOD gridMain;
		private Label labelWarning;
	}
}
