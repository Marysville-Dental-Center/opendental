namespace OpenDental {
	partial class FormPdmpClientCertificate {
		///<summary>Required designer variable.</summary>
		private System.ComponentModel.IContainer components = null;

		///<summary>Clean up any resources being used.</summary>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		///<summary>Required method for Designer support - do not modify the contents of this method with the code editor.</summary>
		private void InitializeComponent() {
			this.labelIntro = new System.Windows.Forms.Label();
			this.labelClinic = new System.Windows.Forms.Label();
			this.labelCurrent = new System.Windows.Forms.Label();
			this.textCurrent = new System.Windows.Forms.TextBox();
			this.groupImport = new OpenDental.UI.GroupBox();
			this.labelFile = new System.Windows.Forms.Label();
			this.textFile = new System.Windows.Forms.TextBox();
			this.butBrowse = new OpenDental.UI.Button();
			this.labelPassword = new System.Windows.Forms.Label();
			this.textPassword = new System.Windows.Forms.TextBox();
			this.butImport = new OpenDental.UI.Button();
			this.butStore = new OpenDental.UI.Button();
			this.butRemove = new OpenDental.UI.Button();
			this.butClose = new OpenDental.UI.Button();
			this.groupImport.SuspendLayout();
			this.SuspendLayout();
			//
			// labelIntro
			//
			this.labelIntro.Location = new System.Drawing.Point(12, 9);
			this.labelIntro.Name = "labelIntro";
			this.labelIntro.Size = new System.Drawing.Size(496, 32);
			this.labelIntro.TabIndex = 0;
			this.labelIntro.Text = "The prescription monitoring service identifies this program by a client certificate. Import the certificate file issued for this software, or use one already in the Windows certificate store.";
			//
			// labelClinic
			//
			this.labelClinic.Location = new System.Drawing.Point(12, 44);
			this.labelClinic.Name = "labelClinic";
			this.labelClinic.Size = new System.Drawing.Size(496, 18);
			this.labelClinic.TabIndex = 1;
			this.labelClinic.Text = "Clinic: Default";
			//
			// labelCurrent
			//
			this.labelCurrent.Location = new System.Drawing.Point(12, 66);
			this.labelCurrent.Name = "labelCurrent";
			this.labelCurrent.Size = new System.Drawing.Size(200, 18);
			this.labelCurrent.TabIndex = 2;
			this.labelCurrent.Text = "Current certificate";
			this.labelCurrent.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textCurrent
			//
			this.textCurrent.Location = new System.Drawing.Point(12, 86);
			this.textCurrent.Multiline = true;
			this.textCurrent.Name = "textCurrent";
			this.textCurrent.ReadOnly = true;
			this.textCurrent.Size = new System.Drawing.Size(496, 48);
			this.textCurrent.TabIndex = 3;
			//
			// groupImport
			//
			this.groupImport.Controls.Add(this.labelFile);
			this.groupImport.Controls.Add(this.textFile);
			this.groupImport.Controls.Add(this.butBrowse);
			this.groupImport.Controls.Add(this.labelPassword);
			this.groupImport.Controls.Add(this.textPassword);
			this.groupImport.Controls.Add(this.butImport);
			this.groupImport.Location = new System.Drawing.Point(12, 144);
			this.groupImport.Name = "groupImport";
			this.groupImport.Size = new System.Drawing.Size(496, 98);
			this.groupImport.TabIndex = 4;
			this.groupImport.Text = "Import a .pfx or .p12 file";
			//
			// labelFile
			//
			this.labelFile.Location = new System.Drawing.Point(6, 22);
			this.labelFile.Name = "labelFile";
			this.labelFile.Size = new System.Drawing.Size(70, 18);
			this.labelFile.TabIndex = 0;
			this.labelFile.Text = "File";
			this.labelFile.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textFile
			//
			this.textFile.Location = new System.Drawing.Point(78, 21);
			this.textFile.Name = "textFile";
			this.textFile.ReadOnly = true;
			this.textFile.Size = new System.Drawing.Size(328, 20);
			this.textFile.TabIndex = 1;
			//
			// butBrowse
			//
			this.butBrowse.Location = new System.Drawing.Point(412, 18);
			this.butBrowse.Name = "butBrowse";
			this.butBrowse.Size = new System.Drawing.Size(75, 24);
			this.butBrowse.TabIndex = 2;
			this.butBrowse.Text = "Browse...";
			this.butBrowse.Click += new System.EventHandler(this.butBrowse_Click);
			//
			// labelPassword
			//
			this.labelPassword.Location = new System.Drawing.Point(6, 50);
			this.labelPassword.Name = "labelPassword";
			this.labelPassword.Size = new System.Drawing.Size(70, 18);
			this.labelPassword.TabIndex = 3;
			this.labelPassword.Text = "Password";
			this.labelPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textPassword
			//
			this.textPassword.Location = new System.Drawing.Point(78, 49);
			this.textPassword.Name = "textPassword";
			this.textPassword.Size = new System.Drawing.Size(200, 20);
			this.textPassword.TabIndex = 4;
			this.textPassword.UseSystemPasswordChar = true;
			//
			// butImport
			//
			this.butImport.Location = new System.Drawing.Point(412, 64);
			this.butImport.Name = "butImport";
			this.butImport.Size = new System.Drawing.Size(75, 24);
			this.butImport.TabIndex = 5;
			this.butImport.Text = "Import";
			this.butImport.Click += new System.EventHandler(this.butImport_Click);
			//
			// butStore
			//
			this.butStore.Location = new System.Drawing.Point(12, 252);
			this.butStore.Name = "butStore";
			this.butStore.Size = new System.Drawing.Size(220, 24);
			this.butStore.TabIndex = 5;
			this.butStore.Text = "Use a Windows store certificate...";
			this.butStore.Click += new System.EventHandler(this.butStore_Click);
			//
			// butRemove
			//
			this.butRemove.Location = new System.Drawing.Point(238, 252);
			this.butRemove.Name = "butRemove";
			this.butRemove.Size = new System.Drawing.Size(75, 24);
			this.butRemove.TabIndex = 6;
			this.butRemove.Text = "Remove";
			this.butRemove.Click += new System.EventHandler(this.butRemove_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(433, 293);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 7;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormPdmpClientCertificate
			//
			this.ClientSize = new System.Drawing.Size(520, 329);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butRemove);
			this.Controls.Add(this.butStore);
			this.Controls.Add(this.groupImport);
			this.Controls.Add(this.textCurrent);
			this.Controls.Add(this.labelCurrent);
			this.Controls.Add(this.labelClinic);
			this.Controls.Add(this.labelIntro);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormPdmpClientCertificate";
			this.ShowInTaskbar = false;
			this.Text = "Client Certificate";
			this.Load += new System.EventHandler(this.FormPdmpClientCertificate_Load);
			this.groupImport.ResumeLayout(false);
			this.groupImport.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelIntro;
		private System.Windows.Forms.Label labelClinic;
		private System.Windows.Forms.Label labelCurrent;
		private System.Windows.Forms.TextBox textCurrent;
		private OpenDental.UI.GroupBox groupImport;
		private System.Windows.Forms.Label labelFile;
		private System.Windows.Forms.TextBox textFile;
		private OpenDental.UI.Button butBrowse;
		private System.Windows.Forms.Label labelPassword;
		private System.Windows.Forms.TextBox textPassword;
		private OpenDental.UI.Button butImport;
		private OpenDental.UI.Button butStore;
		private OpenDental.UI.Button butRemove;
		private OpenDental.UI.Button butClose;
	}
}
