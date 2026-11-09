namespace OpenPmsDental{
	partial class FormEmailCertRegister {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormEmailCertRegister));
			this.labelExplanation = new System.Windows.Forms.Label();
			this.labelEmailAddress = new System.Windows.Forms.Label();
			this.textEmailAddress = new System.Windows.Forms.TextBox();
			this.labelAddressNote = new System.Windows.Forms.Label();
			this.groupCertificate = new OpenPmsDental.UI.GroupBox();
			this.labelCertFilePath = new System.Windows.Forms.Label();
			this.textCertFilePath = new System.Windows.Forms.TextBox();
			this.butBrowse = new OpenPmsDental.UI.Button();
			this.labelPassword = new System.Windows.Forms.Label();
			this.textPassword = new System.Windows.Forms.TextBox();
			this.butCheck = new OpenPmsDental.UI.Button();
			this.labelDetails = new System.Windows.Forms.Label();
			this.textDetails = new System.Windows.Forms.TextBox();
			this.butImport = new OpenPmsDental.UI.Button();
			this.labelInStore = new System.Windows.Forms.Label();
			this.textInStore = new System.Windows.Forms.TextBox();
			this.groupAnchor = new OpenPmsDental.UI.GroupBox();
			this.labelAnchorExplanation = new System.Windows.Forms.Label();
			this.labelAnchorFilePath = new System.Windows.Forms.Label();
			this.textAnchorFilePath = new System.Windows.Forms.TextBox();
			this.butBrowseAnchor = new OpenPmsDental.UI.Button();
			this.butImportAnchor = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.openFileDialogCert = new System.Windows.Forms.OpenFileDialog();
			this.openFileDialogAnchor = new System.Windows.Forms.OpenFileDialog();
			this.groupCertificate.SuspendLayout();
			this.groupAnchor.SuspendLayout();
			this.SuspendLayout();
			//
			// labelExplanation
			//
			this.labelExplanation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelExplanation.Location = new System.Drawing.Point(12, 9);
			this.labelExplanation.Name = "labelExplanation";
			this.labelExplanation.Size = new System.Drawing.Size(596, 72);
			this.labelExplanation.TabIndex = 0;
			this.labelExplanation.Text = resources.GetString("labelExplanation.Text");
			//
			// labelEmailAddress
			//
			this.labelEmailAddress.Location = new System.Drawing.Point(12, 86);
			this.labelEmailAddress.Name = "labelEmailAddress";
			this.labelEmailAddress.Size = new System.Drawing.Size(128, 20);
			this.labelEmailAddress.TabIndex = 1;
			this.labelEmailAddress.Text = "Email Address";
			this.labelEmailAddress.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textEmailAddress
			//
			this.textEmailAddress.Location = new System.Drawing.Point(143, 86);
			this.textEmailAddress.Name = "textEmailAddress";
			this.textEmailAddress.ReadOnly = true;
			this.textEmailAddress.Size = new System.Drawing.Size(320, 20);
			this.textEmailAddress.TabIndex = 2;
			//
			// labelAddressNote
			//
			this.labelAddressNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelAddressNote.Location = new System.Drawing.Point(143, 109);
			this.labelAddressNote.Name = "labelAddressNote";
			this.labelAddressNote.Size = new System.Drawing.Size(465, 28);
			this.labelAddressNote.TabIndex = 8;
			this.labelAddressNote.Text = "The certificate is stored for this user name as typed in Edit Email Address. Sending from the address uses it once Edit Email Address is saved with this user name.";
			//
			// groupCertificate
			//
			this.groupCertificate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupCertificate.Controls.Add(this.labelCertFilePath);
			this.groupCertificate.Controls.Add(this.textCertFilePath);
			this.groupCertificate.Controls.Add(this.butBrowse);
			this.groupCertificate.Controls.Add(this.labelPassword);
			this.groupCertificate.Controls.Add(this.textPassword);
			this.groupCertificate.Controls.Add(this.butCheck);
			this.groupCertificate.Controls.Add(this.labelDetails);
			this.groupCertificate.Controls.Add(this.textDetails);
			this.groupCertificate.Controls.Add(this.butImport);
			this.groupCertificate.Location = new System.Drawing.Point(12, 144);
			this.groupCertificate.Name = "groupCertificate";
			this.groupCertificate.Size = new System.Drawing.Size(596, 236);
			this.groupCertificate.TabIndex = 3;
			this.groupCertificate.TabStop = false;
			this.groupCertificate.Text = "Certificate for this address, from your Direct provider (.pfx or .p12)";
			//
			// labelCertFilePath
			//
			this.labelCertFilePath.Location = new System.Drawing.Point(6, 22);
			this.labelCertFilePath.Name = "labelCertFilePath";
			this.labelCertFilePath.Size = new System.Drawing.Size(122, 20);
			this.labelCertFilePath.TabIndex = 0;
			this.labelCertFilePath.Text = "File";
			this.labelCertFilePath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textCertFilePath
			//
			this.textCertFilePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textCertFilePath.Location = new System.Drawing.Point(131, 22);
			this.textCertFilePath.Name = "textCertFilePath";
			this.textCertFilePath.Size = new System.Drawing.Size(420, 20);
			this.textCertFilePath.TabIndex = 1;
			this.textCertFilePath.TextChanged += new System.EventHandler(this.textCertFile_TextChanged);
			//
			// butBrowse
			//
			this.butBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butBrowse.Location = new System.Drawing.Point(557, 20);
			this.butBrowse.Name = "butBrowse";
			this.butBrowse.Size = new System.Drawing.Size(30, 24);
			this.butBrowse.TabIndex = 2;
			this.butBrowse.Text = "...";
			this.butBrowse.Click += new System.EventHandler(this.butBrowse_Click);
			//
			// labelPassword
			//
			this.labelPassword.Location = new System.Drawing.Point(6, 50);
			this.labelPassword.Name = "labelPassword";
			this.labelPassword.Size = new System.Drawing.Size(122, 20);
			this.labelPassword.TabIndex = 3;
			this.labelPassword.Text = "Password";
			this.labelPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textPassword
			//
			this.textPassword.Location = new System.Drawing.Point(131, 50);
			this.textPassword.Name = "textPassword";
			this.textPassword.PasswordChar = '*';
			this.textPassword.Size = new System.Drawing.Size(188, 20);
			this.textPassword.TabIndex = 4;
			this.textPassword.TextChanged += new System.EventHandler(this.textCertFile_TextChanged);
			//
			// butCheck
			//
			this.butCheck.Location = new System.Drawing.Point(325, 48);
			this.butCheck.Name = "butCheck";
			this.butCheck.Size = new System.Drawing.Size(75, 24);
			this.butCheck.TabIndex = 5;
			this.butCheck.Text = "Check";
			this.butCheck.Click += new System.EventHandler(this.butCheck_Click);
			//
			// labelDetails
			//
			this.labelDetails.Location = new System.Drawing.Point(6, 80);
			this.labelDetails.Name = "labelDetails";
			this.labelDetails.Size = new System.Drawing.Size(122, 20);
			this.labelDetails.TabIndex = 6;
			this.labelDetails.Text = "Certificate";
			this.labelDetails.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDetails
			//
			this.textDetails.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textDetails.Location = new System.Drawing.Point(131, 80);
			this.textDetails.Multiline = true;
			this.textDetails.Name = "textDetails";
			this.textDetails.ReadOnly = true;
			this.textDetails.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textDetails.Size = new System.Drawing.Size(456, 116);
			this.textDetails.TabIndex = 7;
			//
			// butImport
			//
			this.butImport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butImport.Enabled = false;
			this.butImport.Location = new System.Drawing.Point(512, 203);
			this.butImport.Name = "butImport";
			this.butImport.Size = new System.Drawing.Size(75, 24);
			this.butImport.TabIndex = 8;
			this.butImport.Text = "&Import";
			this.butImport.Click += new System.EventHandler(this.butImport_Click);
			//
			// labelInStore
			//
			this.labelInStore.Location = new System.Drawing.Point(12, 388);
			this.labelInStore.Name = "labelInStore";
			this.labelInStore.Size = new System.Drawing.Size(128, 32);
			this.labelInStore.TabIndex = 4;
			this.labelInStore.Text = "On this computer now";
			this.labelInStore.TextAlign = System.Drawing.ContentAlignment.TopRight;
			//
			// textInStore
			//
			this.textInStore.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textInStore.Location = new System.Drawing.Point(143, 388);
			this.textInStore.Multiline = true;
			this.textInStore.Name = "textInStore";
			this.textInStore.ReadOnly = true;
			this.textInStore.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textInStore.Size = new System.Drawing.Size(465, 60);
			this.textInStore.TabIndex = 5;
			//
			// groupAnchor
			//
			this.groupAnchor.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupAnchor.Controls.Add(this.labelAnchorExplanation);
			this.groupAnchor.Controls.Add(this.labelAnchorFilePath);
			this.groupAnchor.Controls.Add(this.textAnchorFilePath);
			this.groupAnchor.Controls.Add(this.butBrowseAnchor);
			this.groupAnchor.Controls.Add(this.butImportAnchor);
			this.groupAnchor.Location = new System.Drawing.Point(12, 458);
			this.groupAnchor.Name = "groupAnchor";
			this.groupAnchor.Size = new System.Drawing.Size(596, 108);
			this.groupAnchor.TabIndex = 6;
			this.groupAnchor.TabStop = false;
			this.groupAnchor.Text = "Trust anchor of your Direct provider (.cer, .crt, .der or .pem)";
			//
			// labelAnchorExplanation
			//
			this.labelAnchorExplanation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelAnchorExplanation.Location = new System.Drawing.Point(6, 18);
			this.labelAnchorExplanation.Name = "labelAnchorExplanation";
			this.labelAnchorExplanation.Size = new System.Drawing.Size(581, 32);
			this.labelAnchorExplanation.TabIndex = 0;
			this.labelAnchorExplanation.Text = "Direct messages are trusted when the sender\'s certificate was issued under a trust anchor of this computer. Your provider gives you its anchor as a public certificate file.";
			//
			// labelAnchorFilePath
			//
			this.labelAnchorFilePath.Location = new System.Drawing.Point(6, 54);
			this.labelAnchorFilePath.Name = "labelAnchorFilePath";
			this.labelAnchorFilePath.Size = new System.Drawing.Size(122, 20);
			this.labelAnchorFilePath.TabIndex = 1;
			this.labelAnchorFilePath.Text = "File";
			this.labelAnchorFilePath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAnchorFilePath
			//
			this.textAnchorFilePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textAnchorFilePath.Location = new System.Drawing.Point(131, 54);
			this.textAnchorFilePath.Name = "textAnchorFilePath";
			this.textAnchorFilePath.Size = new System.Drawing.Size(420, 20);
			this.textAnchorFilePath.TabIndex = 2;
			//
			// butBrowseAnchor
			//
			this.butBrowseAnchor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butBrowseAnchor.Location = new System.Drawing.Point(557, 52);
			this.butBrowseAnchor.Name = "butBrowseAnchor";
			this.butBrowseAnchor.Size = new System.Drawing.Size(30, 24);
			this.butBrowseAnchor.TabIndex = 3;
			this.butBrowseAnchor.Text = "...";
			this.butBrowseAnchor.Click += new System.EventHandler(this.butBrowseAnchor_Click);
			//
			// butImportAnchor
			//
			this.butImportAnchor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butImportAnchor.Location = new System.Drawing.Point(512, 80);
			this.butImportAnchor.Name = "butImportAnchor";
			this.butImportAnchor.Size = new System.Drawing.Size(75, 24);
			this.butImportAnchor.TabIndex = 4;
			this.butImportAnchor.Text = "Import...";
			this.butImportAnchor.Click += new System.EventHandler(this.butImportAnchor_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(533, 576);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 7;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// openFileDialogCert
			//
			this.openFileDialogCert.DefaultExt = "pfx";
			//
			// openFileDialogAnchor
			//
			this.openFileDialogAnchor.DefaultExt = "cer";
			//
			// FormEmailCertRegister
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(620, 612);
			this.Controls.Add(this.labelExplanation);
			this.Controls.Add(this.labelEmailAddress);
			this.Controls.Add(this.textEmailAddress);
			this.Controls.Add(this.labelAddressNote);
			this.Controls.Add(this.groupCertificate);
			this.Controls.Add(this.labelInStore);
			this.Controls.Add(this.textInStore);
			this.Controls.Add(this.groupAnchor);
			this.Controls.Add(this.butClose);
			this.Name = "FormEmailCertRegister";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Direct Certificate";
			this.Load += new System.EventHandler(this.FormEmailCertRegister_Load);
			this.groupCertificate.ResumeLayout(false);
			this.groupCertificate.PerformLayout();
			this.groupAnchor.ResumeLayout(false);
			this.groupAnchor.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelExplanation;
		private System.Windows.Forms.Label labelEmailAddress;
		private System.Windows.Forms.TextBox textEmailAddress;
		private System.Windows.Forms.Label labelAddressNote;
		private OpenPmsDental.UI.GroupBox groupCertificate;
		private System.Windows.Forms.Label labelCertFilePath;
		private System.Windows.Forms.TextBox textCertFilePath;
		private OpenPmsDental.UI.Button butBrowse;
		private System.Windows.Forms.Label labelPassword;
		private System.Windows.Forms.TextBox textPassword;
		private OpenPmsDental.UI.Button butCheck;
		private System.Windows.Forms.Label labelDetails;
		private System.Windows.Forms.TextBox textDetails;
		private OpenPmsDental.UI.Button butImport;
		private System.Windows.Forms.Label labelInStore;
		private System.Windows.Forms.TextBox textInStore;
		private OpenPmsDental.UI.GroupBox groupAnchor;
		private System.Windows.Forms.Label labelAnchorExplanation;
		private System.Windows.Forms.Label labelAnchorFilePath;
		private System.Windows.Forms.TextBox textAnchorFilePath;
		private OpenPmsDental.UI.Button butBrowseAnchor;
		private OpenPmsDental.UI.Button butImportAnchor;
		private OpenPmsDental.UI.Button butClose;
		private System.Windows.Forms.OpenFileDialog openFileDialogCert;
		private System.Windows.Forms.OpenFileDialog openFileDialogAnchor;
	}
}
