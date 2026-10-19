namespace OpenDental{
	partial class FormQuickBooksOnlineSetup {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormQuickBooksOnlineSetup));
			this.butSave = new OpenDental.UI.Button();
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.labelClassesInOD = new System.Windows.Forms.Label();
			this.labelIncomeAccountsInOD = new System.Windows.Forms.Label();
			this.labelDepositAccountsInOD = new System.Windows.Forms.Label();
			this.groupConnection = new OpenDental.UI.GroupBox();
			this.labelDescription = new System.Windows.Forms.Label();
			this.labelEnvironment = new System.Windows.Forms.Label();
			this.comboEnvironment = new OpenDental.UI.ComboBox();
			this.labelClientId = new System.Windows.Forms.Label();
			this.textClientId = new System.Windows.Forms.TextBox();
			this.labelClientSecret = new System.Windows.Forms.Label();
			this.textClientSecret = new System.Windows.Forms.TextBox();
			this.butChangeSecret = new OpenDental.UI.Button();
			this.labelRedirectUri = new System.Windows.Forms.Label();
			this.textRedirectUri = new System.Windows.Forms.TextBox();
			this.labelRelayPort = new System.Windows.Forms.Label();
			this.textRelayPort = new System.Windows.Forms.TextBox();
			this.labelRelayPortHint = new System.Windows.Forms.Label();
			this.labelStatus = new System.Windows.Forms.Label();
			this.labelStatusValue = new System.Windows.Forms.Label();
			this.butConnect = new OpenDental.UI.Button();
			this.butDisconnect = new OpenDental.UI.Button();
			this.butAddDepositAccount = new OpenDental.UI.Button();
			this.butRemoveDepositAccount = new OpenDental.UI.Button();
			this.labelDepositAccountsAvailable = new System.Windows.Forms.Label();
			this.labelIncomeAccountsAvailable = new System.Windows.Forms.Label();
			this.labelClassesAvailable = new System.Windows.Forms.Label();
			this.butRemoveIncomeAccount = new OpenDental.UI.Button();
			this.butAddIncomeAccount = new OpenDental.UI.Button();
			this.butRemoveClass = new OpenDental.UI.Button();
			this.butAddClass = new OpenDental.UI.Button();
			this.listBoxDepositAccountsInOD = new OpenDental.UI.ListBox();
			this.listBoxDepositAccountsAvailable = new OpenDental.UI.ListBox();
			this.listBoxIncomeAccountsInOD = new OpenDental.UI.ListBox();
			this.listBoxIncomeAccountsAvailable = new OpenDental.UI.ListBox();
			this.listBoxClassRefsInOD = new OpenDental.UI.ListBox();
			this.listBoxClassRefsAvailable = new OpenDental.UI.ListBox();
			this.groupConnection.SuspendLayout();
			this.SuspendLayout();
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(725, 683);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 14;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// checkEnabled
			//
			this.checkEnabled.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkEnabled.Location = new System.Drawing.Point(40, 20);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(82, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled";
			this.checkEnabled.Click += new System.EventHandler(this.checkEnabled_Click);
			//
			// labelClassesInOD
			//
			this.labelClassesInOD.Location = new System.Drawing.Point(10, 559);
			this.labelClassesInOD.Name = "labelClassesInOD";
			this.labelClassesInOD.Size = new System.Drawing.Size(141, 71);
			this.labelClassesInOD.TabIndex = 85;
			this.labelClassesInOD.Text = "User will get to pick from this list of classes.";
			this.labelClassesInOD.TextAlign = System.Drawing.ContentAlignment.TopRight;
			//
			// labelIncomeAccountsInOD
			//
			this.labelIncomeAccountsInOD.Location = new System.Drawing.Point(7, 435);
			this.labelIncomeAccountsInOD.Name = "labelIncomeAccountsInOD";
			this.labelIncomeAccountsInOD.Size = new System.Drawing.Size(144, 68);
			this.labelIncomeAccountsInOD.TabIndex = 83;
			this.labelIncomeAccountsInOD.Text = "User will get to pick from this list of income accounts.";
			this.labelIncomeAccountsInOD.TextAlign = System.Drawing.ContentAlignment.TopRight;
			//
			// labelDepositAccountsInOD
			//
			this.labelDepositAccountsInOD.Location = new System.Drawing.Point(4, 311);
			this.labelDepositAccountsInOD.Name = "labelDepositAccountsInOD";
			this.labelDepositAccountsInOD.Size = new System.Drawing.Size(147, 70);
			this.labelDepositAccountsInOD.TabIndex = 80;
			this.labelDepositAccountsInOD.Text = "User will get to pick from this list of accounts to deposit to.";
			this.labelDepositAccountsInOD.TextAlign = System.Drawing.ContentAlignment.TopRight;
			//
			// groupConnection
			//
			this.groupConnection.Controls.Add(this.checkEnabled);
			this.groupConnection.Controls.Add(this.labelDescription);
			this.groupConnection.Controls.Add(this.labelEnvironment);
			this.groupConnection.Controls.Add(this.comboEnvironment);
			this.groupConnection.Controls.Add(this.labelClientId);
			this.groupConnection.Controls.Add(this.textClientId);
			this.groupConnection.Controls.Add(this.labelClientSecret);
			this.groupConnection.Controls.Add(this.textClientSecret);
			this.groupConnection.Controls.Add(this.butChangeSecret);
			this.groupConnection.Controls.Add(this.labelRedirectUri);
			this.groupConnection.Controls.Add(this.textRedirectUri);
			this.groupConnection.Controls.Add(this.labelRelayPort);
			this.groupConnection.Controls.Add(this.textRelayPort);
			this.groupConnection.Controls.Add(this.labelRelayPortHint);
			this.groupConnection.Controls.Add(this.labelStatus);
			this.groupConnection.Controls.Add(this.labelStatusValue);
			this.groupConnection.Controls.Add(this.butConnect);
			this.groupConnection.Controls.Add(this.butDisconnect);
			this.groupConnection.Location = new System.Drawing.Point(12, 6);
			this.groupConnection.Name = "groupConnection";
			this.groupConnection.Size = new System.Drawing.Size(790, 288);
			this.groupConnection.TabIndex = 1;
			this.groupConnection.Text = "Connection";
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(130, 18);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(650, 44);
			this.labelDescription.TabIndex = 101;
			this.labelDescription.Text = "Sends deposit slips to a QuickBooks Online company using your own Intuit developer app. An internet connection is requ" +
    "ired. The deposit memo, amounts and date are sent to Intuit, so don\'t put patient names in the memo.";
			//
			// labelEnvironment
			//
			this.labelEnvironment.Location = new System.Drawing.Point(6, 70);
			this.labelEnvironment.Name = "labelEnvironment";
			this.labelEnvironment.Size = new System.Drawing.Size(120, 20);
			this.labelEnvironment.TabIndex = 102;
			this.labelEnvironment.Text = "Environment";
			this.labelEnvironment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboEnvironment
			//
			this.comboEnvironment.Location = new System.Drawing.Point(128, 70);
			this.comboEnvironment.Name = "comboEnvironment";
			this.comboEnvironment.Size = new System.Drawing.Size(150, 21);
			this.comboEnvironment.TabIndex = 1;
			//
			// labelClientId
			//
			this.labelClientId.Location = new System.Drawing.Point(6, 97);
			this.labelClientId.Name = "labelClientId";
			this.labelClientId.Size = new System.Drawing.Size(120, 20);
			this.labelClientId.TabIndex = 103;
			this.labelClientId.Text = "Client ID";
			this.labelClientId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textClientId
			//
			this.textClientId.Location = new System.Drawing.Point(128, 97);
			this.textClientId.Name = "textClientId";
			this.textClientId.Size = new System.Drawing.Size(450, 20);
			this.textClientId.TabIndex = 2;
			//
			// labelClientSecret
			//
			this.labelClientSecret.Location = new System.Drawing.Point(6, 123);
			this.labelClientSecret.Name = "labelClientSecret";
			this.labelClientSecret.Size = new System.Drawing.Size(120, 20);
			this.labelClientSecret.TabIndex = 104;
			this.labelClientSecret.Text = "Client Secret";
			this.labelClientSecret.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textClientSecret
			//
			this.textClientSecret.Location = new System.Drawing.Point(128, 123);
			this.textClientSecret.Name = "textClientSecret";
			this.textClientSecret.ReadOnly = true;
			this.textClientSecret.Size = new System.Drawing.Size(300, 20);
			this.textClientSecret.TabIndex = 3;
			this.textClientSecret.UseSystemPasswordChar = true;
			//
			// butChangeSecret
			//
			this.butChangeSecret.Location = new System.Drawing.Point(434, 121);
			this.butChangeSecret.Name = "butChangeSecret";
			this.butChangeSecret.Size = new System.Drawing.Size(75, 24);
			this.butChangeSecret.TabIndex = 4;
			this.butChangeSecret.Text = "Change...";
			this.butChangeSecret.Click += new System.EventHandler(this.butChangeSecret_Click);
			//
			// labelRedirectUri
			//
			this.labelRedirectUri.Location = new System.Drawing.Point(6, 151);
			this.labelRedirectUri.Name = "labelRedirectUri";
			this.labelRedirectUri.Size = new System.Drawing.Size(120, 20);
			this.labelRedirectUri.TabIndex = 105;
			this.labelRedirectUri.Text = "Redirect URI";
			this.labelRedirectUri.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textRedirectUri
			//
			this.textRedirectUri.Location = new System.Drawing.Point(128, 151);
			this.textRedirectUri.Name = "textRedirectUri";
			this.textRedirectUri.Size = new System.Drawing.Size(450, 20);
			this.textRedirectUri.TabIndex = 5;
			//
			// labelRelayPort
			//
			this.labelRelayPort.Location = new System.Drawing.Point(6, 177);
			this.labelRelayPort.Name = "labelRelayPort";
			this.labelRelayPort.Size = new System.Drawing.Size(120, 20);
			this.labelRelayPort.TabIndex = 106;
			this.labelRelayPort.Text = "Relay Port";
			this.labelRelayPort.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textRelayPort
			//
			this.textRelayPort.Location = new System.Drawing.Point(128, 177);
			this.textRelayPort.Name = "textRelayPort";
			this.textRelayPort.Size = new System.Drawing.Size(60, 20);
			this.textRelayPort.TabIndex = 6;
			//
			// labelRelayPortHint
			//
			this.labelRelayPortHint.Location = new System.Drawing.Point(194, 177);
			this.labelRelayPortHint.Name = "labelRelayPortHint";
			this.labelRelayPortHint.Size = new System.Drawing.Size(586, 34);
			this.labelRelayPortHint.TabIndex = 107;
			this.labelRelayPortHint.Text = "Optional. Only when your HTTPS redirect address hosts the relay page with the same port. Production keys need an HTTPS r" +
    "edirect address; sandbox keys can use http://localhost:<port>/callback.";
			//
			// labelStatus
			//
			this.labelStatus.Location = new System.Drawing.Point(6, 222);
			this.labelStatus.Name = "labelStatus";
			this.labelStatus.Size = new System.Drawing.Size(120, 20);
			this.labelStatus.TabIndex = 108;
			this.labelStatus.Text = "Status";
			this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// labelStatusValue
			//
			this.labelStatusValue.Location = new System.Drawing.Point(128, 222);
			this.labelStatusValue.Name = "labelStatusValue";
			this.labelStatusValue.Size = new System.Drawing.Size(450, 54);
			this.labelStatusValue.TabIndex = 109;
			this.labelStatusValue.Text = "Not connected";
			//
			// butConnect
			//
			this.butConnect.Location = new System.Drawing.Point(590, 220);
			this.butConnect.Name = "butConnect";
			this.butConnect.Size = new System.Drawing.Size(90, 24);
			this.butConnect.TabIndex = 7;
			this.butConnect.Text = "Connect...";
			this.butConnect.Click += new System.EventHandler(this.butConnect_Click);
			//
			// butDisconnect
			//
			this.butDisconnect.Location = new System.Drawing.Point(686, 220);
			this.butDisconnect.Name = "butDisconnect";
			this.butDisconnect.Size = new System.Drawing.Size(90, 24);
			this.butDisconnect.TabIndex = 8;
			this.butDisconnect.Text = "Disconnect";
			this.butDisconnect.Click += new System.EventHandler(this.butDisconnect_Click);
			//
			// butAddDepositAccount
			//
			this.butAddDepositAccount.AdjustImageLocation = new System.Drawing.Point(-1, 0);
			this.butAddDepositAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butAddDepositAccount.Image = global::OpenDental.Properties.Resources.Left;
			this.butAddDepositAccount.Location = new System.Drawing.Point(390, 333);
			this.butAddDepositAccount.Name = "butAddDepositAccount";
			this.butAddDepositAccount.Size = new System.Drawing.Size(35, 24);
			this.butAddDepositAccount.TabIndex = 3;
			this.butAddDepositAccount.Click += new System.EventHandler(this.butAddDepositAccount_Click);
			//
			// butRemoveDepositAccount
			//
			this.butRemoveDepositAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRemoveDepositAccount.Image = global::OpenDental.Properties.Resources.Right;
			this.butRemoveDepositAccount.Location = new System.Drawing.Point(390, 373);
			this.butRemoveDepositAccount.Name = "butRemoveDepositAccount";
			this.butRemoveDepositAccount.Size = new System.Drawing.Size(35, 24);
			this.butRemoveDepositAccount.TabIndex = 4;
			this.butRemoveDepositAccount.Click += new System.EventHandler(this.butRemoveDepositAccount_Click);
			//
			// labelDepositAccountsAvailable
			//
			this.labelDepositAccountsAvailable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelDepositAccountsAvailable.Location = new System.Drawing.Point(664, 311);
			this.labelDepositAccountsAvailable.Name = "labelDepositAccountsAvailable";
			this.labelDepositAccountsAvailable.Size = new System.Drawing.Size(136, 70);
			this.labelDepositAccountsAvailable.TabIndex = 98;
			this.labelDepositAccountsAvailable.Text = "Deposit accounts available in QuickBooks Online.";
			//
			// labelIncomeAccountsAvailable
			//
			this.labelIncomeAccountsAvailable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelIncomeAccountsAvailable.Location = new System.Drawing.Point(664, 435);
			this.labelIncomeAccountsAvailable.Name = "labelIncomeAccountsAvailable";
			this.labelIncomeAccountsAvailable.Size = new System.Drawing.Size(136, 68);
			this.labelIncomeAccountsAvailable.TabIndex = 99;
			this.labelIncomeAccountsAvailable.Text = "Income accounts available in QuickBooks Online.";
			//
			// labelClassesAvailable
			//
			this.labelClassesAvailable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelClassesAvailable.Location = new System.Drawing.Point(664, 559);
			this.labelClassesAvailable.Name = "labelClassesAvailable";
			this.labelClassesAvailable.Size = new System.Drawing.Size(136, 71);
			this.labelClassesAvailable.TabIndex = 100;
			this.labelClassesAvailable.Text = "Classes available in QuickBooks Online.";
			//
			// butRemoveIncomeAccount
			//
			this.butRemoveIncomeAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRemoveIncomeAccount.Image = global::OpenDental.Properties.Resources.Right;
			this.butRemoveIncomeAccount.Location = new System.Drawing.Point(390, 497);
			this.butRemoveIncomeAccount.Name = "butRemoveIncomeAccount";
			this.butRemoveIncomeAccount.Size = new System.Drawing.Size(35, 24);
			this.butRemoveIncomeAccount.TabIndex = 8;
			this.butRemoveIncomeAccount.Click += new System.EventHandler(this.butRemoveIncomeAccount_Click);
			//
			// butAddIncomeAccount
			//
			this.butAddIncomeAccount.AdjustImageLocation = new System.Drawing.Point(-1, 0);
			this.butAddIncomeAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butAddIncomeAccount.Image = global::OpenDental.Properties.Resources.Left;
			this.butAddIncomeAccount.Location = new System.Drawing.Point(390, 457);
			this.butAddIncomeAccount.Name = "butAddIncomeAccount";
			this.butAddIncomeAccount.Size = new System.Drawing.Size(35, 24);
			this.butAddIncomeAccount.TabIndex = 7;
			this.butAddIncomeAccount.Click += new System.EventHandler(this.butAddIncomeAccount_Click);
			//
			// butRemoveClass
			//
			this.butRemoveClass.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRemoveClass.Image = global::OpenDental.Properties.Resources.Right;
			this.butRemoveClass.Location = new System.Drawing.Point(390, 621);
			this.butRemoveClass.Name = "butRemoveClass";
			this.butRemoveClass.Size = new System.Drawing.Size(35, 24);
			this.butRemoveClass.TabIndex = 12;
			this.butRemoveClass.Click += new System.EventHandler(this.butRemoveClass_Click);
			//
			// butAddClass
			//
			this.butAddClass.AdjustImageLocation = new System.Drawing.Point(-1, 0);
			this.butAddClass.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butAddClass.Image = global::OpenDental.Properties.Resources.Left;
			this.butAddClass.Location = new System.Drawing.Point(390, 581);
			this.butAddClass.Name = "butAddClass";
			this.butAddClass.Size = new System.Drawing.Size(35, 24);
			this.butAddClass.TabIndex = 11;
			this.butAddClass.Click += new System.EventHandler(this.butAddClass_Click);
			//
			// listBoxDepositAccountsInOD
			//
			this.listBoxDepositAccountsInOD.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxDepositAccountsInOD.Location = new System.Drawing.Point(152, 311);
			this.listBoxDepositAccountsInOD.Name = "listBoxDepositAccountsInOD";
			this.listBoxDepositAccountsInOD.Size = new System.Drawing.Size(230, 108);
			this.listBoxDepositAccountsInOD.TabIndex = 2;
			//
			// listBoxDepositAccountsAvailable
			//
			this.listBoxDepositAccountsAvailable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxDepositAccountsAvailable.Location = new System.Drawing.Point(433, 311);
			this.listBoxDepositAccountsAvailable.Name = "listBoxDepositAccountsAvailable";
			this.listBoxDepositAccountsAvailable.Size = new System.Drawing.Size(230, 108);
			this.listBoxDepositAccountsAvailable.TabIndex = 5;
			//
			// listBoxIncomeAccountsInOD
			//
			this.listBoxIncomeAccountsInOD.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxIncomeAccountsInOD.Location = new System.Drawing.Point(152, 435);
			this.listBoxIncomeAccountsInOD.Name = "listBoxIncomeAccountsInOD";
			this.listBoxIncomeAccountsInOD.Size = new System.Drawing.Size(230, 108);
			this.listBoxIncomeAccountsInOD.TabIndex = 6;
			//
			// listBoxIncomeAccountsAvailable
			//
			this.listBoxIncomeAccountsAvailable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxIncomeAccountsAvailable.Location = new System.Drawing.Point(433, 435);
			this.listBoxIncomeAccountsAvailable.Name = "listBoxIncomeAccountsAvailable";
			this.listBoxIncomeAccountsAvailable.Size = new System.Drawing.Size(230, 108);
			this.listBoxIncomeAccountsAvailable.TabIndex = 9;
			//
			// listBoxClassRefsInOD
			//
			this.listBoxClassRefsInOD.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxClassRefsInOD.Location = new System.Drawing.Point(152, 559);
			this.listBoxClassRefsInOD.Name = "listBoxClassRefsInOD";
			this.listBoxClassRefsInOD.Size = new System.Drawing.Size(230, 108);
			this.listBoxClassRefsInOD.TabIndex = 10;
			//
			// listBoxClassRefsAvailable
			//
			this.listBoxClassRefsAvailable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.listBoxClassRefsAvailable.Location = new System.Drawing.Point(433, 559);
			this.listBoxClassRefsAvailable.Name = "listBoxClassRefsAvailable";
			this.listBoxClassRefsAvailable.Size = new System.Drawing.Size(230, 108);
			this.listBoxClassRefsAvailable.TabIndex = 13;
			//
			// FormQuickBooksOnlineSetup
			//
			this.ClientSize = new System.Drawing.Size(814, 719);
			this.Controls.Add(this.listBoxClassRefsAvailable);
			this.Controls.Add(this.listBoxClassRefsInOD);
			this.Controls.Add(this.listBoxIncomeAccountsAvailable);
			this.Controls.Add(this.listBoxIncomeAccountsInOD);
			this.Controls.Add(this.listBoxDepositAccountsAvailable);
			this.Controls.Add(this.listBoxDepositAccountsInOD);
			this.Controls.Add(this.butRemoveClass);
			this.Controls.Add(this.butAddClass);
			this.Controls.Add(this.butRemoveIncomeAccount);
			this.Controls.Add(this.butAddIncomeAccount);
			this.Controls.Add(this.labelClassesAvailable);
			this.Controls.Add(this.labelIncomeAccountsAvailable);
			this.Controls.Add(this.labelDepositAccountsAvailable);
			this.Controls.Add(this.butRemoveDepositAccount);
			this.Controls.Add(this.butAddDepositAccount);
			this.Controls.Add(this.groupConnection);
			this.Controls.Add(this.labelClassesInOD);
			this.Controls.Add(this.labelIncomeAccountsInOD);
			this.Controls.Add(this.labelDepositAccountsInOD);
			this.Controls.Add(this.butSave);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormQuickBooksOnlineSetup";
			this.Text = "QuickBooks Online Setup";
			this.Load += new System.EventHandler(this.FormQuickBooksOnlineSetup_Load);
			this.groupConnection.ResumeLayout(false);
			this.groupConnection.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private OpenDental.UI.Button butSave;
		private OpenDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelClassesInOD;
		private System.Windows.Forms.Label labelIncomeAccountsInOD;
		private System.Windows.Forms.Label labelDepositAccountsInOD;
		private OpenDental.UI.GroupBox groupConnection;
		private System.Windows.Forms.Label labelDescription;
		private System.Windows.Forms.Label labelEnvironment;
		private OpenDental.UI.ComboBox comboEnvironment;
		private System.Windows.Forms.Label labelClientId;
		private System.Windows.Forms.TextBox textClientId;
		private System.Windows.Forms.Label labelClientSecret;
		private System.Windows.Forms.TextBox textClientSecret;
		private UI.Button butChangeSecret;
		private System.Windows.Forms.Label labelRedirectUri;
		private System.Windows.Forms.TextBox textRedirectUri;
		private System.Windows.Forms.Label labelRelayPort;
		private System.Windows.Forms.TextBox textRelayPort;
		private System.Windows.Forms.Label labelRelayPortHint;
		private System.Windows.Forms.Label labelStatus;
		private System.Windows.Forms.Label labelStatusValue;
		private UI.Button butConnect;
		private UI.Button butDisconnect;
		private UI.Button butAddDepositAccount;
		private UI.Button butRemoveDepositAccount;
		private System.Windows.Forms.Label labelDepositAccountsAvailable;
		private System.Windows.Forms.Label labelIncomeAccountsAvailable;
		private System.Windows.Forms.Label labelClassesAvailable;
		private UI.Button butRemoveIncomeAccount;
		private UI.Button butAddIncomeAccount;
		private UI.Button butRemoveClass;
		private UI.Button butAddClass;
		private UI.ListBox listBoxDepositAccountsInOD;
		private UI.ListBox listBoxDepositAccountsAvailable;
		private UI.ListBox listBoxIncomeAccountsInOD;
		private UI.ListBox listBoxIncomeAccountsAvailable;
		private UI.ListBox listBoxClassRefsInOD;
		private UI.ListBox listBoxClassRefsAvailable;
	}
}
