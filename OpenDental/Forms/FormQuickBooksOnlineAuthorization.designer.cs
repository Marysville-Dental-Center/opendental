namespace OpenDental{
	partial class FormQuickBooksOnlineAuthorization {
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
			if(disposing) {
				_cancellationTokenSource.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormQuickBooksOnlineAuthorization));
			this.butOK = new OpenDental.UI.Button();
			this.labelWaiting = new System.Windows.Forms.Label();
			this.textAddress = new System.Windows.Forms.TextBox();
			this.groupSeparate = new OpenDental.UI.GroupBox();
			this.textAuthCode = new System.Windows.Forms.TextBox();
			this.labelAuthCode = new System.Windows.Forms.Label();
			this.labelRealmId = new System.Windows.Forms.Label();
			this.textRealmId = new System.Windows.Forms.TextBox();
			this.groupSeparate.SuspendLayout();
			this.SuspendLayout();
			//
			// butOK
			//
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(446, 239);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 3;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			//
			// labelWaiting
			//
			this.labelWaiting.Location = new System.Drawing.Point(12, 9);
			this.labelWaiting.Name = "labelWaiting";
			this.labelWaiting.Size = new System.Drawing.Size(509, 44);
			this.labelWaiting.TabIndex = 130;
			this.labelWaiting.Text = "After approving, copy the full address from your browser\'s address bar and paste it here.";
			this.labelWaiting.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textAddress
			//
			this.textAddress.Location = new System.Drawing.Point(12, 56);
			this.textAddress.MaxLength = 99999;
			this.textAddress.Name = "textAddress";
			this.textAddress.Size = new System.Drawing.Size(509, 20);
			this.textAddress.TabIndex = 0;
			//
			// groupSeparate
			//
			this.groupSeparate.Controls.Add(this.textAuthCode);
			this.groupSeparate.Controls.Add(this.labelAuthCode);
			this.groupSeparate.Controls.Add(this.labelRealmId);
			this.groupSeparate.Controls.Add(this.textRealmId);
			this.groupSeparate.Location = new System.Drawing.Point(12, 92);
			this.groupSeparate.Name = "groupSeparate";
			this.groupSeparate.Size = new System.Drawing.Size(509, 136);
			this.groupSeparate.TabIndex = 1;
			this.groupSeparate.Text = "Enter separately (the connection attempt can\'t be checked)";
			//
			// textAuthCode
			//
			this.textAuthCode.Location = new System.Drawing.Point(10, 50);
			this.textAuthCode.MaxLength = 99999;
			this.textAuthCode.Name = "textAuthCode";
			this.textAuthCode.Size = new System.Drawing.Size(489, 20);
			this.textAuthCode.TabIndex = 0;
			//
			// labelAuthCode
			//
			this.labelAuthCode.Location = new System.Drawing.Point(10, 22);
			this.labelAuthCode.Name = "labelAuthCode";
			this.labelAuthCode.Size = new System.Drawing.Size(489, 25);
			this.labelAuthCode.TabIndex = 130;
			this.labelAuthCode.Text = "Authorization Code (the code value in the address)";
			this.labelAuthCode.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// labelRealmId
			//
			this.labelRealmId.Location = new System.Drawing.Point(10, 74);
			this.labelRealmId.Name = "labelRealmId";
			this.labelRealmId.Size = new System.Drawing.Size(489, 25);
			this.labelRealmId.TabIndex = 131;
			this.labelRealmId.Text = "Realm ID (the realmId value in the address)";
			this.labelRealmId.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textRealmId
			//
			this.textRealmId.Location = new System.Drawing.Point(10, 102);
			this.textRealmId.MaxLength = 99999;
			this.textRealmId.Name = "textRealmId";
			this.textRealmId.Size = new System.Drawing.Size(489, 20);
			this.textRealmId.TabIndex = 1;
			//
			// FormQuickBooksOnlineAuthorization
			//
			this.ClientSize = new System.Drawing.Size(533, 275);
			this.Controls.Add(this.groupSeparate);
			this.Controls.Add(this.textAddress);
			this.Controls.Add(this.labelWaiting);
			this.Controls.Add(this.butOK);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormQuickBooksOnlineAuthorization";
			this.Text = "QuickBooks Online Authorization";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormQuickBooksOnlineAuthorization_FormClosing);
			this.Load += new System.EventHandler(this.FormQuickBooksOnlineAuthorization_Load);
			this.groupSeparate.ResumeLayout(false);
			this.groupSeparate.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenDental.UI.Button butOK;
		private System.Windows.Forms.Label labelWaiting;
		private System.Windows.Forms.TextBox textAddress;
		private OpenDental.UI.GroupBox groupSeparate;
		private System.Windows.Forms.TextBox textAuthCode;
		private System.Windows.Forms.Label labelAuthCode;
		private System.Windows.Forms.Label labelRealmId;
		private System.Windows.Forms.TextBox textRealmId;
	}
}
