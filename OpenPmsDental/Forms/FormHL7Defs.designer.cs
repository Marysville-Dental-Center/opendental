using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormHL7Defs {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components=null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components!=null)
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
			this.grid1 = new OpenPmsDental.UI.GridOD();
			this.grid2 = new OpenPmsDental.UI.GridOD();
			this.butDuplicate = new OpenPmsDental.UI.Button();
			this.butCopy = new OpenPmsDental.UI.Button();
			this.butHistory = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			// 
			// grid1
			// 
			this.grid1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.grid1.Location = new System.Drawing.Point(12, 38);
			this.grid1.Name = "grid1";
			this.grid1.Size = new System.Drawing.Size(470, 559);
			this.grid1.TabIndex = 14;
			this.grid1.Title = "Internal";
			this.grid1.TranslationName = "TableInternal";
			this.grid1.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.grid1_CellDoubleClick);
			// 
			// grid2
			// 
			this.grid2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.grid2.Location = new System.Drawing.Point(491, 38);
			this.grid2.Name = "grid2";
			this.grid2.Size = new System.Drawing.Size(470, 559);
			this.grid2.TabIndex = 12;
			this.grid2.Title = "Custom";
			this.grid2.TranslationName = "TableCustom";
			this.grid2.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.grid2_CellDoubleClick);
			// 
			// butDuplicate
			// 
			this.butDuplicate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butDuplicate.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butDuplicate.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDuplicate.Location = new System.Drawing.Point(737, 607);
			this.butDuplicate.Name = "butDuplicate";
			this.butDuplicate.Size = new System.Drawing.Size(89, 24);
			this.butDuplicate.TabIndex = 20;
			this.butDuplicate.Text = "Duplicate";
			this.butDuplicate.Click += new System.EventHandler(this.butDuplicate_Click);
			// 
			// butCopy
			// 
			this.butCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butCopy.Image = global::OpenPmsDental.Properties.Resources.Right;
			this.butCopy.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butCopy.Location = new System.Drawing.Point(333, 607);
			this.butCopy.Name = "butCopy";
			this.butCopy.Size = new System.Drawing.Size(75, 24);
			this.butCopy.TabIndex = 15;
			this.butCopy.Text = "Copy";
			this.butCopy.Click += new System.EventHandler(this.butCopy_Click);
			// 
			// butHistory
			// 
			this.butHistory.Location = new System.Drawing.Point(38, 8);
			this.butHistory.Name = "butHistory";
			this.butHistory.Size = new System.Drawing.Size(75, 24);
			this.butHistory.TabIndex = 21;
			this.butHistory.Text = "History";
			this.butHistory.Click += new System.EventHandler(this.butHistory_Click);
			// 
			// FormHL7Defs
			// 
			this.ClientSize = new System.Drawing.Size(974, 641);
			this.Controls.Add(this.butHistory);
			this.Controls.Add(this.butDuplicate);
			this.Controls.Add(this.butCopy);
			this.Controls.Add(this.grid1);
			this.Controls.Add(this.grid2);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormHL7Defs";
			this.ShowInTaskbar = false;
			this.Text = "HL7 Defs";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormHL7Defs_FormClosing);
			this.Load += new System.EventHandler(this.FormHL7Defs_Load);
			this.ResumeLayout(false);

		}
		#endregion
		private UI.Button butCopy;
		private UI.GridOD grid2;
		private UI.GridOD grid1;
		private UI.Button butDuplicate;
		private UI.Button butHistory;
	}
}
