namespace OpenPmsDental {
	partial class FormGlidewellAcctEdit {
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
			this.labelAccountNumber = new System.Windows.Forms.Label();
			this.textAccountNumber = new System.Windows.Forms.TextBox();
			this.labelApiKey = new System.Windows.Forms.Label();
			this.textApiKey = new System.Windows.Forms.TextBox();
			this.labelApiKeyStatus = new System.Windows.Forms.Label();
			this.labelLastSignIn = new System.Windows.Forms.Label();
			this.textLastSignIn = new System.Windows.Forms.TextBox();
			this.labelProviders = new System.Windows.Forms.Label();
			this.listProviders = new OpenPmsDental.UI.ListBox();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelAccountNumber
			//
			this.labelAccountNumber.Location = new System.Drawing.Point(12, 15);
			this.labelAccountNumber.Name = "labelAccountNumber";
			this.labelAccountNumber.Size = new System.Drawing.Size(130, 17);
			this.labelAccountNumber.TabIndex = 0;
			this.labelAccountNumber.Text = "Account Number";
			this.labelAccountNumber.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAccountNumber
			//
			this.textAccountNumber.Location = new System.Drawing.Point(144, 14);
			this.textAccountNumber.MaxLength = 255;
			this.textAccountNumber.Name = "textAccountNumber";
			this.textAccountNumber.Size = new System.Drawing.Size(200, 20);
			this.textAccountNumber.TabIndex = 1;
			//
			// labelApiKey
			//
			this.labelApiKey.Location = new System.Drawing.Point(12, 41);
			this.labelApiKey.Name = "labelApiKey";
			this.labelApiKey.Size = new System.Drawing.Size(130, 17);
			this.labelApiKey.TabIndex = 2;
			this.labelApiKey.Text = "API Key";
			this.labelApiKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textApiKey
			//
			this.textApiKey.Location = new System.Drawing.Point(144, 40);
			this.textApiKey.MaxLength = 200;
			this.textApiKey.Name = "textApiKey";
			this.textApiKey.Size = new System.Drawing.Size(200, 20);
			this.textApiKey.TabIndex = 3;
			this.textApiKey.UseSystemPasswordChar = true;
			//
			// labelApiKeyStatus
			//
			this.labelApiKeyStatus.Location = new System.Drawing.Point(144, 63);
			this.labelApiKeyStatus.Name = "labelApiKeyStatus";
			this.labelApiKeyStatus.Size = new System.Drawing.Size(270, 30);
			this.labelApiKeyStatus.TabIndex = 4;
			//
			// labelLastSignIn
			//
			this.labelLastSignIn.Location = new System.Drawing.Point(12, 97);
			this.labelLastSignIn.Name = "labelLastSignIn";
			this.labelLastSignIn.Size = new System.Drawing.Size(130, 17);
			this.labelLastSignIn.TabIndex = 5;
			this.labelLastSignIn.Text = "Last scanner sign-in";
			this.labelLastSignIn.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textLastSignIn
			//
			this.textLastSignIn.Location = new System.Drawing.Point(144, 96);
			this.textLastSignIn.Name = "textLastSignIn";
			this.textLastSignIn.ReadOnly = true;
			this.textLastSignIn.Size = new System.Drawing.Size(200, 20);
			this.textLastSignIn.TabIndex = 6;
			//
			// labelProviders
			//
			this.labelProviders.Location = new System.Drawing.Point(12, 126);
			this.labelProviders.Name = "labelProviders";
			this.labelProviders.Size = new System.Drawing.Size(400, 17);
			this.labelProviders.TabIndex = 7;
			this.labelProviders.Text = "Default account of these providers\' lab cases";
			this.labelProviders.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// listProviders
			//
			this.listProviders.Location = new System.Drawing.Point(12, 146);
			this.listProviders.Name = "listProviders";
			this.listProviders.SelectionMode = OpenPmsDental.UI.SelectionMode.MultiExtended;
			this.listProviders.Size = new System.Drawing.Size(400, 186);
			this.listProviders.TabIndex = 8;
			//
			// butDelete
			//
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(12, 349);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(81, 24);
			this.butDelete.TabIndex = 9;
			this.butDelete.Text = "Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(337, 349);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 10;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormGlidewellAcctEdit
			//
			this.ClientSize = new System.Drawing.Size(424, 385);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.listProviders);
			this.Controls.Add(this.labelProviders);
			this.Controls.Add(this.textLastSignIn);
			this.Controls.Add(this.labelLastSignIn);
			this.Controls.Add(this.labelApiKeyStatus);
			this.Controls.Add(this.textApiKey);
			this.Controls.Add(this.labelApiKey);
			this.Controls.Add(this.textAccountNumber);
			this.Controls.Add(this.labelAccountNumber);
			this.Name = "FormGlidewellAcctEdit";
			this.Text = "Glidewell Account";
			this.Load += new System.EventHandler(this.FormGlidewellAcctEdit_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelAccountNumber;
		private System.Windows.Forms.TextBox textAccountNumber;
		private System.Windows.Forms.Label labelApiKey;
		private System.Windows.Forms.TextBox textApiKey;
		private System.Windows.Forms.Label labelApiKeyStatus;
		private System.Windows.Forms.Label labelLastSignIn;
		private System.Windows.Forms.TextBox textLastSignIn;
		private System.Windows.Forms.Label labelProviders;
		private OpenPmsDental.UI.ListBox listProviders;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.UI.Button butSave;
	}
}
