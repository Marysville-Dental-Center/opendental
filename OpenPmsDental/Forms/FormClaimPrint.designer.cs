using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormClaimPrint {
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
			this.Preview2 = new OpenPmsDental.UI.ODPrintPreviewControl();
			this.butPrint = new OpenPmsDental.UI.Button();
			this.labelTotPages = new System.Windows.Forms.Label();
			this.butBack = new OpenPmsDental.UI.Button();
			this.butFwd = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			// 
			// Preview2
			// 
			this.Preview2.AutoZoom = false;
			this.Preview2.Location = new System.Drawing.Point(0,0);
			this.Preview2.Name = "Preview2";
			this.Preview2.Size = new System.Drawing.Size(738,798);
			this.Preview2.TabIndex = 1;
			this.Preview2.Zoom = 1;
			// 
			// butPrint
			// 
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPrint.Location = new System.Drawing.Point(766,773);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(75,25);
			this.butPrint.TabIndex = 2;
			this.butPrint.Text = "&Print";
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			// 
			// labelTotPages
			// 
			this.labelTotPages.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.labelTotPages.Font = new System.Drawing.Font("Microsoft Sans Serif",9F,System.Drawing.FontStyle.Bold,System.Drawing.GraphicsUnit.Point,((byte)(0)));
			this.labelTotPages.Location = new System.Drawing.Point(776,724);
			this.labelTotPages.Name = "labelTotPages";
			this.labelTotPages.Size = new System.Drawing.Size(54,18);
			this.labelTotPages.TabIndex = 22;
			this.labelTotPages.Text = "1 / 2";
			this.labelTotPages.TextAlign = System.Drawing.ContentAlignment.TopCenter;
			// 
			// butBack
			// 
			this.butBack.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butBack.Image = global::OpenPmsDental.Properties.Resources.Left;
			this.butBack.Location = new System.Drawing.Point(754,721);
			this.butBack.Name = "butBack";
			this.butBack.Size = new System.Drawing.Size(18,23);
			this.butBack.TabIndex = 23;
			this.butBack.Click += new System.EventHandler(this.butBack_Click);
			// 
			// butFwd
			// 
			this.butFwd.AdjustImageLocation = new System.Drawing.Point(1,0);
			this.butFwd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butFwd.Image = global::OpenPmsDental.Properties.Resources.Right;
			this.butFwd.Location = new System.Drawing.Point(832,721);
			this.butFwd.Name = "butFwd";
			this.butFwd.Size = new System.Drawing.Size(18,23);
			this.butFwd.TabIndex = 24;
			this.butFwd.Click += new System.EventHandler(this.butFwd_Click);
			// 
			// FormClaimPrint
			// 
			this.AcceptButton = this.butPrint;
			this.AutoScaleBaseSize = new System.Drawing.Size(5,13);
			this.ClientSize = new System.Drawing.Size(860,816);
			this.Controls.Add(this.labelTotPages);
			this.Controls.Add(this.butBack);
			this.Controls.Add(this.butFwd);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.Preview2);
			this.Name = "FormClaimPrint";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Print Claim";
			this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
			this.Load += new System.EventHandler(this.FormClaimPrint_Load);
			this.Layout += new System.Windows.Forms.LayoutEventHandler(this.FormClaimPrint_Layout);
			this.ResumeLayout(false);

		}
		#endregion
		private OpenPmsDental.UI.ODPrintPreviewControl Preview2;
		private OpenPmsDental.UI.Button butPrint;
		private System.Windows.Forms.Label labelTotPages;
		private OpenPmsDental.UI.Button butBack;
		private OpenPmsDental.UI.Button butFwd;
	}
}
