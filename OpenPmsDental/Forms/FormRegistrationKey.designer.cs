using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormRegistrationKey {
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
			this.labelLicenseAgree = new System.Windows.Forms.Label();
			this.richTextAgreement = new System.Windows.Forms.RichTextBox();
			this.butOK = new OpenPmsDental.UI.Button();
			this.listBoxRegistration = new OpenPmsDental.UI.ListBox();
			this.SuspendLayout();
			//
			// labelLicenseAgree
			// 
			this.labelLicenseAgree.Location = new System.Drawing.Point(12, 26);
			this.labelLicenseAgree.Name = "labelLicenseAgree";
			this.labelLicenseAgree.Size = new System.Drawing.Size(150, 13);
			this.labelLicenseAgree.TabIndex = 6;
			this.labelLicenseAgree.Text = "License Agreement";
			this.labelLicenseAgree.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// richTextAgreement
			// 
			this.richTextAgreement.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.richTextAgreement.Location = new System.Drawing.Point(168, 45);
			this.richTextAgreement.Name = "richTextAgreement";
			this.richTextAgreement.Size = new System.Drawing.Size(583, 359);
			this.richTextAgreement.TabIndex = 8;
			this.richTextAgreement.Text = "";
			// 
			// butOK
			// 
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(676, 410);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 26);
			this.butOK.TabIndex = 4;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			// 
			// listBoxRegistration
			// 
			this.listBoxRegistration.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxRegistration.Location = new System.Drawing.Point(12, 45);
			this.listBoxRegistration.Name = "listBoxRegistration";
			this.listBoxRegistration.Size = new System.Drawing.Size(150, 358);
			this.listBoxRegistration.TabIndex = 9;
			this.listBoxRegistration.SelectedIndexChanged += new System.EventHandler(this.listRegistration_SelectedIndexChanged);
			// 
			// FormRegistrationKey
			// 
			this.ClientSize = new System.Drawing.Size(763, 442);
			this.Controls.Add(this.listBoxRegistration);
			this.Controls.Add(this.butOK);
			this.Controls.Add(this.richTextAgreement);
			this.Controls.Add(this.labelLicenseAgree);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormRegistrationKey";
			this.ShowInTaskbar = false;
			this.Text = "Registration Key";
			this.Load += new System.EventHandler(this.FormRegistrationKey_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion
		private OpenPmsDental.UI.Button butOK;
		private Label labelLicenseAgree;
		private OpenPmsDental.UI.ListBox listBoxRegistration;
		private RichTextBox richTextAgreement;
	}
}
