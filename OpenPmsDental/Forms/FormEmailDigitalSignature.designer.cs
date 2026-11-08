namespace OpenPmsDental{
	partial class FormEmailDigitalSignature {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.label2 = new System.Windows.Forms.Label();
			this.textCertificateAuthority = new System.Windows.Forms.TextBox();
			this.textValidFrom = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textSignedBy = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textThumbprint = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textVersion = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.SuspendLayout();
			//
			// label2
			//
			this.label2.Location = new System.Drawing.Point(5, 49);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(100, 20);
			this.label2.TabIndex = 5;
			this.label2.Text = "Authority:";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textCertificateAuthority
			//
			this.textCertificateAuthority.Location = new System.Drawing.Point(106, 49);
			this.textCertificateAuthority.Multiline = true;
			this.textCertificateAuthority.Name = "textCertificateAuthority";
			this.textCertificateAuthority.ReadOnly = true;
			this.textCertificateAuthority.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textCertificateAuthority.Size = new System.Drawing.Size(332, 60);
			this.textCertificateAuthority.TabIndex = 6;
			//
			// textValidFrom
			//
			this.textValidFrom.Location = new System.Drawing.Point(106, 115);
			this.textValidFrom.Name = "textValidFrom";
			this.textValidFrom.ReadOnly = true;
			this.textValidFrom.Size = new System.Drawing.Size(332, 20);
			this.textValidFrom.TabIndex = 8;
			//
			// label3
			//
			this.label3.Location = new System.Drawing.Point(5, 115);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(100, 20);
			this.label3.TabIndex = 7;
			this.label3.Text = "Valid From:";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textSignedBy
			//
			this.textSignedBy.Location = new System.Drawing.Point(106, 23);
			this.textSignedBy.Name = "textSignedBy";
			this.textSignedBy.ReadOnly = true;
			this.textSignedBy.Size = new System.Drawing.Size(332, 20);
			this.textSignedBy.TabIndex = 12;
			//
			// label5
			//
			this.label5.Location = new System.Drawing.Point(5, 23);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(100, 20);
			this.label5.TabIndex = 11;
			this.label5.Text = "Signed By:";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textThumbprint
			//
			this.textThumbprint.Location = new System.Drawing.Point(106, 141);
			this.textThumbprint.Name = "textThumbprint";
			this.textThumbprint.ReadOnly = true;
			this.textThumbprint.Size = new System.Drawing.Size(332, 20);
			this.textThumbprint.TabIndex = 14;
			//
			// label6
			//
			this.label6.Location = new System.Drawing.Point(5, 141);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(100, 20);
			this.label6.TabIndex = 13;
			this.label6.Text = "Thumbprint:";
			this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textVersion
			//
			this.textVersion.Location = new System.Drawing.Point(106, 164);
			this.textVersion.Name = "textVersion";
			this.textVersion.ReadOnly = true;
			this.textVersion.Size = new System.Drawing.Size(332, 20);
			this.textVersion.TabIndex = 16;
			//
			// label7
			//
			this.label7.Location = new System.Drawing.Point(5, 164);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(100, 20);
			this.label7.TabIndex = 15;
			this.label7.Text = "Version:";
			this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// FormEmailDigitalSignature
			//
			this.ClientSize = new System.Drawing.Size(488, 208);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.Controls.Add(this.textVersion);
			this.Controls.Add(this.label7);
			this.Controls.Add(this.textThumbprint);
			this.Controls.Add(this.label6);
			this.Controls.Add(this.textSignedBy);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.textValidFrom);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.textCertificateAuthority);
			this.Controls.Add(this.label2);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Name = "FormEmailDigitalSignature";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Email Digital Signature";
			this.Load += new System.EventHandler(this.FormEmailDigitalSignature_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox textCertificateAuthority;
		private System.Windows.Forms.TextBox textValidFrom;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox textSignedBy;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.TextBox textThumbprint;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.TextBox textVersion;
		private System.Windows.Forms.Label label7;
	}
}
