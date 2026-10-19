namespace OpenDental {
	partial class FormGmailApiSetup {
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
			this.labelDescription = new System.Windows.Forms.Label();
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.labelClientId = new System.Windows.Forms.Label();
			this.textClientId = new System.Windows.Forms.TextBox();
			this.labelClientSecret = new System.Windows.Forms.Label();
			this.textClientSecret = new System.Windows.Forms.TextBox();
			this.labelSecretStatus = new System.Windows.Forms.Label();
			this.butSave = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 12);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(460, 32);
			this.labelDescription.TabIndex = 0;
			this.labelDescription.Text = "Send and receive email through the Gmail API using your own Google Cloud OAuth client.";
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(12, 52);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(200, 18);
			this.checkEnabled.TabIndex = 1;
			this.checkEnabled.Text = "Enabled";
			//
			// labelClientId
			//
			this.labelClientId.Location = new System.Drawing.Point(12, 84);
			this.labelClientId.Name = "labelClientId";
			this.labelClientId.Size = new System.Drawing.Size(100, 18);
			this.labelClientId.TabIndex = 2;
			this.labelClientId.Text = "Client ID";
			this.labelClientId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textClientId
			//
			this.textClientId.Location = new System.Drawing.Point(114, 84);
			this.textClientId.Name = "textClientId";
			this.textClientId.Size = new System.Drawing.Size(358, 20);
			this.textClientId.TabIndex = 3;
			//
			// labelClientSecret
			//
			this.labelClientSecret.Location = new System.Drawing.Point(12, 110);
			this.labelClientSecret.Name = "labelClientSecret";
			this.labelClientSecret.Size = new System.Drawing.Size(100, 18);
			this.labelClientSecret.TabIndex = 4;
			this.labelClientSecret.Text = "Client Secret";
			this.labelClientSecret.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textClientSecret
			//
			this.textClientSecret.Location = new System.Drawing.Point(114, 110);
			this.textClientSecret.Name = "textClientSecret";
			this.textClientSecret.Size = new System.Drawing.Size(358, 20);
			this.textClientSecret.TabIndex = 5;
			this.textClientSecret.UseSystemPasswordChar = true;
			//
			// labelSecretStatus
			//
			this.labelSecretStatus.Location = new System.Drawing.Point(114, 134);
			this.labelSecretStatus.Name = "labelSecretStatus";
			this.labelSecretStatus.Size = new System.Drawing.Size(358, 32);
			this.labelSecretStatus.TabIndex = 6;
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(397, 186);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 7;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormGmailApiSetup
			//
			this.ClientSize = new System.Drawing.Size(484, 222);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelSecretStatus);
			this.Controls.Add(this.textClientSecret);
			this.Controls.Add(this.labelClientSecret);
			this.Controls.Add(this.textClientId);
			this.Controls.Add(this.labelClientId);
			this.Controls.Add(this.checkEnabled);
			this.Controls.Add(this.labelDescription);
			this.Name = "FormGmailApiSetup";
			this.Text = "Gmail API Setup";
			this.Load += new System.EventHandler(this.FormGmailApiSetup_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelDescription;
		private OpenDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelClientId;
		private System.Windows.Forms.TextBox textClientId;
		private System.Windows.Forms.Label labelClientSecret;
		private System.Windows.Forms.TextBox textClientSecret;
		private System.Windows.Forms.Label labelSecretStatus;
		private OpenDental.UI.Button butSave;
	}
}
