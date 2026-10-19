namespace OpenDental {
	partial class FormCareCreditSetup {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormCareCreditSetup));
			this.butSave = new OpenDental.UI.Button();
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.groupClinicSettings = new OpenDental.UI.GroupBox();
			this.labelQSBatch = new System.Windows.Forms.Label();
			this.listBoxQSOptions = new OpenDental.UI.ListBox();
			this.labelMerchantNumberClinic = new System.Windows.Forms.Label();
			this.textMerchantNumberClinic = new System.Windows.Forms.TextBox();
			this.groupQSBatch = new OpenDental.UI.GroupBox();
			this.comboBoxDaysOut = new OpenDental.UI.ComboBox();
			this.labelQSDays = new System.Windows.Forms.Label();
			this.comboPaymentType = new OpenDental.UI.ComboBox();
			this.labelPaymentType = new System.Windows.Forms.Label();
			this.groupPromotions = new OpenDental.UI.GroupBox();
			this.butPromotions = new OpenDental.UI.Button();
			this.comboClinics = new OpenDental.UI.ComboBoxClinicPicker();
			this.comboPatFieldDef = new OpenDental.UI.ComboBox();
			this.labelPatFieldType = new System.Windows.Forms.Label();
			this.textMerchantNumberPractice = new System.Windows.Forms.TextBox();
			this.labelMerchantNumberPractice = new System.Windows.Forms.Label();
			this.checkMerchantNumByProv = new OpenDental.UI.CheckBox();
			this.labelMerchantClosedDescription = new System.Windows.Forms.Label();
			this.comboPatFieldPreApprvAmt = new OpenDental.UI.ComboBox();
			this.labelPreApprvAmt = new System.Windows.Forms.Label();
			this.comboPatFieldAvailableCredit = new OpenDental.UI.ComboBox();
			this.labelAvailableCredit = new System.Windows.Forms.Label();
			this.labelClientId = new System.Windows.Forms.Label();
			this.textClientId = new System.Windows.Forms.TextBox();
			this.labelClientSecret = new System.Windows.Forms.Label();
			this.textClientSecret = new System.Windows.Forms.TextBox();
			this.labelEnvironment = new System.Windows.Forms.Label();
			this.comboEnvironment = new OpenDental.UI.ComboBox();
			this.labelApiBaseUrl = new System.Windows.Forms.Label();
			this.textApiBaseUrl = new System.Windows.Forms.TextBox();
			this.labelIhsBaseUrl = new System.Windows.Forms.Label();
			this.textIhsBaseUrl = new System.Windows.Forms.TextBox();
			this.labelPartnerCode = new System.Windows.Forms.Label();
			this.textPartnerCode = new System.Windows.Forms.TextBox();
			this.labelTokenPath = new System.Windows.Forms.Label();
			this.textTokenPath = new System.Windows.Forms.TextBox();
			this.labelPrefillPath = new System.Windows.Forms.Label();
			this.textPrefillPath = new System.Windows.Forms.TextBox();
			this.labelPullbackPath = new System.Windows.Forms.Label();
			this.textPullbackPath = new System.Windows.Forms.TextBox();
			this.labelBatchPath = new System.Windows.Forms.Label();
			this.textBatchPath = new System.Windows.Forms.TextBox();
			this.labelSessionParam = new System.Windows.Forms.Label();
			this.textSessionParam = new System.Windows.Forms.TextBox();
			this.labelClientAuth = new System.Windows.Forms.Label();
			this.comboClientAuth = new OpenDental.UI.ComboBox();
			this.labelScope = new System.Windows.Forms.Label();
			this.textScope = new System.Windows.Forms.TextBox();
			this.groupConnection = new OpenDental.UI.GroupBox();
			this.butTestConnection = new OpenDental.UI.Button();
			this.labelConnectionStatus = new System.Windows.Forms.Label();
			this.groupBackground = new OpenDental.UI.GroupBox();
			this.labelJobComputer = new System.Windows.Forms.Label();
			this.butRunHere = new OpenDental.UI.Button();
			this.labelProcessTime = new System.Windows.Forms.Label();
			this.textProcessTime = new System.Windows.Forms.TextBox();
			this.labelPullbackTime = new System.Windows.Forms.Label();
			this.textPullbackTime = new System.Windows.Forms.TextBox();
			this.labelTimeFormat = new System.Windows.Forms.Label();
			this.groupClinicSettings.SuspendLayout();
			this.groupQSBatch.SuspendLayout();
			this.groupPromotions.SuspendLayout();
			this.groupConnection.SuspendLayout();
			this.groupBackground.SuspendLayout();
			this.SuspendLayout();
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(925, 568);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 7;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// checkEnabled
			// 
			this.checkEnabled.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkEnabled.Location = new System.Drawing.Point(72, 20);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(118, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled";
			// 
			// groupClinicSettings
			// 
			this.groupClinicSettings.Controls.Add(this.labelQSBatch);
			this.groupClinicSettings.Controls.Add(this.listBoxQSOptions);
			this.groupClinicSettings.Controls.Add(this.labelMerchantNumberClinic);
			this.groupClinicSettings.Controls.Add(this.textMerchantNumberClinic);
			this.groupClinicSettings.Controls.Add(this.groupQSBatch);
			this.groupClinicSettings.Controls.Add(this.comboPaymentType);
			this.groupClinicSettings.Controls.Add(this.labelPaymentType);
			this.groupClinicSettings.Controls.Add(this.groupPromotions);
			this.groupClinicSettings.Location = new System.Drawing.Point(24, 240);
			this.groupClinicSettings.Name = "groupClinicSettings";
			this.groupClinicSettings.Size = new System.Drawing.Size(566, 238);
			this.groupClinicSettings.TabIndex = 6;
			this.groupClinicSettings.Text = "Clinic Settings";
			// 
			// labelQSBatch
			// 
			this.labelQSBatch.Location = new System.Drawing.Point(6, 139);
			this.labelQSBatch.Name = "labelQSBatch";
			this.labelQSBatch.Size = new System.Drawing.Size(160, 16);
			this.labelQSBatch.TabIndex = 276;
			this.labelQSBatch.Text = "Quickscreen Feature";
			this.labelQSBatch.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// listBoxQSBatch
			// 
			this.listBoxQSOptions.Location = new System.Drawing.Point(9, 158);
			this.listBoxQSOptions.Name = "listBoxQSBatch";
			this.listBoxQSOptions.Size = new System.Drawing.Size(92, 64);
			this.listBoxQSOptions.TabIndex = 277;
			this.listBoxQSOptions.Text = "listQSBatch";
			this.listBoxQSOptions.MouseClick += new System.Windows.Forms.MouseEventHandler(this.listQSBatch_SelectionChanged);
			// 
			// labelMerchantNumberClinic
			// 
			this.labelMerchantNumberClinic.Location = new System.Drawing.Point(6, 106);
			this.labelMerchantNumberClinic.Name = "labelMerchantNumberClinic";
			this.labelMerchantNumberClinic.Size = new System.Drawing.Size(138, 16);
			this.labelMerchantNumberClinic.TabIndex = 276;
			this.labelMerchantNumberClinic.Text = "Merchant Number";
			this.labelMerchantNumberClinic.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textMerchantNumberClinic
			// 
			this.textMerchantNumberClinic.Location = new System.Drawing.Point(146, 105);
			this.textMerchantNumberClinic.Name = "textMerchantNumberClinic";
			this.textMerchantNumberClinic.Size = new System.Drawing.Size(220, 20);
			this.textMerchantNumberClinic.TabIndex = 275;
			this.textMerchantNumberClinic.Leave += new System.EventHandler(this.textMerchantNumberClinic_Leave);
			// 
			// groupQSBatch
			// 
			this.groupQSBatch.Controls.Add(this.comboBoxDaysOut);
			this.groupQSBatch.Controls.Add(this.labelQSDays);
			this.groupQSBatch.Location = new System.Drawing.Point(104, 158);
			this.groupQSBatch.Name = "groupQSBatch";
			this.groupQSBatch.Size = new System.Drawing.Size(459, 64);
			this.groupQSBatch.TabIndex = 6;
			this.groupQSBatch.Text = "Quickscreen Settings";
			// 
			// comboBoxDaysOut
			// 
			this.comboBoxDaysOut.Location = new System.Drawing.Point(8, 24);
			this.comboBoxDaysOut.Name = "comboBoxDaysOut";
			this.comboBoxDaysOut.Size = new System.Drawing.Size(38, 21);
			this.comboBoxDaysOut.TabIndex = 0;
			this.comboBoxDaysOut.Text = "comboBoxPlus1";
			// 
			// labelBatchDays
			// 
			this.labelQSDays.Location = new System.Drawing.Point(52, 26);
			this.labelQSDays.Name = "labelQSDays";
			this.labelQSDays.Size = new System.Drawing.Size(404, 19);
			this.labelQSDays.TabIndex = 208;
			this.labelQSDays.Text = "Number of days out to check for pre-approvals for future appointments.";
			this.labelQSDays.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// comboPaymentType
			// 
			this.comboPaymentType.Location = new System.Drawing.Point(146, 17);
			this.comboPaymentType.Name = "comboPaymentType";
			this.comboPaymentType.Size = new System.Drawing.Size(220, 21);
			this.comboPaymentType.TabIndex = 0;
			this.comboPaymentType.Text = "comboPaymentType";
			// 
			// labelPaymentType
			// 
			this.labelPaymentType.Location = new System.Drawing.Point(53, 18);
			this.labelPaymentType.Name = "labelPaymentType";
			this.labelPaymentType.Size = new System.Drawing.Size(90, 16);
			this.labelPaymentType.TabIndex = 18;
			this.labelPaymentType.Text = "Payment Type";
			this.labelPaymentType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupPromotions
			// 
			this.groupPromotions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.groupPromotions.Controls.Add(this.butPromotions);
			this.groupPromotions.Location = new System.Drawing.Point(146, 44);
			this.groupPromotions.Name = "groupPromotions";
			this.groupPromotions.Size = new System.Drawing.Size(220, 54);
			this.groupPromotions.TabIndex = 4;
			this.groupPromotions.Text = "CareCredit admin";
			// 
			// butPromotions
			// 
			this.butPromotions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPromotions.Location = new System.Drawing.Point(73, 19);
			this.butPromotions.Name = "butPromotions";
			this.butPromotions.Size = new System.Drawing.Size(75, 24);
			this.butPromotions.TabIndex = 0;
			this.butPromotions.Text = "Manage";
			this.butPromotions.Click += new System.EventHandler(this.butPromotions_Click);
			// 
			// comboClinics
			// 
			this.comboClinics.HqDescription = "Headquarters";
			this.comboClinics.IncludeUnassigned = true;
			this.comboClinics.Location = new System.Drawing.Point(139, 211);
			this.comboClinics.Name = "comboClinics";
			this.comboClinics.Size = new System.Drawing.Size(251, 21);
			this.comboClinics.TabIndex = 2;
			this.comboClinics.SelectionChangeCommitted += new System.EventHandler(this.comboClinics_SelectionChangeCommitted);
			// 
			// comboPatFieldDef
			// 
			this.comboPatFieldDef.Location = new System.Drawing.Point(177, 101);
			this.comboPatFieldDef.Name = "comboPatFieldDef";
			this.comboPatFieldDef.Size = new System.Drawing.Size(213, 21);
			this.comboPatFieldDef.TabIndex = 3;
			this.comboPatFieldDef.Text = "comboBoxPlus1";
			// 
			// labelPatFieldType
			// 
			this.labelPatFieldType.Location = new System.Drawing.Point(5, 102);
			this.labelPatFieldType.Name = "labelPatFieldType";
			this.labelPatFieldType.Size = new System.Drawing.Size(169, 16);
			this.labelPatFieldType.TabIndex = 270;
			this.labelPatFieldType.Text = "Pre-Approval Status Patient Field";
			this.labelPatFieldType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textMerchantNumberPractice
			// 
			this.textMerchantNumberPractice.Location = new System.Drawing.Point(177, 73);
			this.textMerchantNumberPractice.Name = "textMerchantNumberPractice";
			this.textMerchantNumberPractice.Size = new System.Drawing.Size(213, 20);
			this.textMerchantNumberPractice.TabIndex = 5;
			this.textMerchantNumberPractice.Leave += new System.EventHandler(this.textMerchantNumberPractice_Leave);
			// 
			// labelMerchantNumberPractice
			// 
			this.labelMerchantNumberPractice.Location = new System.Drawing.Point(36, 74);
			this.labelMerchantNumberPractice.Name = "labelMerchantNumberPractice";
			this.labelMerchantNumberPractice.Size = new System.Drawing.Size(138, 16);
			this.labelMerchantNumberPractice.TabIndex = 274;
			this.labelMerchantNumberPractice.Text = "Merchant Number";
			this.labelMerchantNumberPractice.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// checkMerchantNumByProv
			// 
			this.checkMerchantNumByProv.Location = new System.Drawing.Point(177, 185);
			this.checkMerchantNumByProv.Name = "checkMerchantNumByProv";
			this.checkMerchantNumByProv.Size = new System.Drawing.Size(213, 18);
			this.checkMerchantNumByProv.TabIndex = 4;
			this.checkMerchantNumByProv.Text = "Merchant number by provider";
			this.checkMerchantNumByProv.CheckedChanged += new System.EventHandler(this.checkMerchantNumByProv_CheckedChanged);
			// 
			// labelMerchantClosedDescription
			// 
			this.labelMerchantClosedDescription.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelMerchantClosedDescription.ForeColor = System.Drawing.Color.Red;
			this.labelMerchantClosedDescription.Location = new System.Drawing.Point(21, 558);
			this.labelMerchantClosedDescription.Name = "labelMerchantClosedDescription";
			this.labelMerchantClosedDescription.Size = new System.Drawing.Size(392, 31);
			this.labelMerchantClosedDescription.TabIndex = 275;
			this.labelMerchantClosedDescription.Text = "The selected Merchant Number has been closed.\r\nPlease enter a new one or refer to" +
    " the manual for more information.";
			this.labelMerchantClosedDescription.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// comboPatFieldPreApprvAmt
			// 
			this.comboPatFieldPreApprvAmt.Location = new System.Drawing.Point(177, 129);
			this.comboPatFieldPreApprvAmt.Name = "comboPatFieldPreApprvAmt";
			this.comboPatFieldPreApprvAmt.Size = new System.Drawing.Size(213, 21);
			this.comboPatFieldPreApprvAmt.TabIndex = 276;
			this.comboPatFieldPreApprvAmt.Text = "comboBoxPlus1";
			// 
			// labelPreApprvAmt
			// 
			this.labelPreApprvAmt.Location = new System.Drawing.Point(5, 130);
			this.labelPreApprvAmt.Name = "labelPreApprvAmt";
			this.labelPreApprvAmt.Size = new System.Drawing.Size(169, 16);
			this.labelPreApprvAmt.TabIndex = 277;
			this.labelPreApprvAmt.Text = "Pre-Approval Amount Patient Field";
			this.labelPreApprvAmt.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboPatFieldAvailableCredit
			// 
			this.comboPatFieldAvailableCredit.Location = new System.Drawing.Point(177, 157);
			this.comboPatFieldAvailableCredit.Name = "comboPatFieldAvailableCredit";
			this.comboPatFieldAvailableCredit.Size = new System.Drawing.Size(213, 21);
			this.comboPatFieldAvailableCredit.TabIndex = 278;
			this.comboPatFieldAvailableCredit.Text = "comboBoxPlus1";
			// 
			// labelAvailableCredit
			// 
			this.labelAvailableCredit.Location = new System.Drawing.Point(5, 158);
			this.labelAvailableCredit.Name = "labelAvailableCredit";
			this.labelAvailableCredit.Size = new System.Drawing.Size(169, 16);
			this.labelAvailableCredit.TabIndex = 279;
			this.labelAvailableCredit.Text = "Available Credit Patient Field";
			this.labelAvailableCredit.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// groupConnection
			//
			this.groupConnection.Controls.Add(this.labelClientId);
			this.groupConnection.Controls.Add(this.textClientId);
			this.groupConnection.Controls.Add(this.labelClientSecret);
			this.groupConnection.Controls.Add(this.textClientSecret);
			this.groupConnection.Controls.Add(this.labelEnvironment);
			this.groupConnection.Controls.Add(this.comboEnvironment);
			this.groupConnection.Controls.Add(this.labelApiBaseUrl);
			this.groupConnection.Controls.Add(this.textApiBaseUrl);
			this.groupConnection.Controls.Add(this.labelIhsBaseUrl);
			this.groupConnection.Controls.Add(this.textIhsBaseUrl);
			this.groupConnection.Controls.Add(this.labelPartnerCode);
			this.groupConnection.Controls.Add(this.textPartnerCode);
			this.groupConnection.Controls.Add(this.labelTokenPath);
			this.groupConnection.Controls.Add(this.textTokenPath);
			this.groupConnection.Controls.Add(this.labelPrefillPath);
			this.groupConnection.Controls.Add(this.textPrefillPath);
			this.groupConnection.Controls.Add(this.labelPullbackPath);
			this.groupConnection.Controls.Add(this.textPullbackPath);
			this.groupConnection.Controls.Add(this.labelBatchPath);
			this.groupConnection.Controls.Add(this.textBatchPath);
			this.groupConnection.Controls.Add(this.labelSessionParam);
			this.groupConnection.Controls.Add(this.textSessionParam);
			this.groupConnection.Controls.Add(this.labelClientAuth);
			this.groupConnection.Controls.Add(this.comboClientAuth);
			this.groupConnection.Controls.Add(this.labelScope);
			this.groupConnection.Controls.Add(this.textScope);
			this.groupConnection.Controls.Add(this.butTestConnection);
			this.groupConnection.Controls.Add(this.labelConnectionStatus);
			this.groupConnection.Location = new System.Drawing.Point(605, 12);
			this.groupConnection.Name = "groupConnection";
			this.groupConnection.Size = new System.Drawing.Size(395, 432);
			this.groupConnection.TabIndex = 8;
			this.groupConnection.Text = "Connection (values from Synchrony's implementation guide)";
			// 
			// labelClientId
			// 
			this.labelClientId.Location = new System.Drawing.Point(6, 21);
			this.labelClientId.Name = "labelClientId";
			this.labelClientId.Size = new System.Drawing.Size(150, 16);
			this.labelClientId.TabIndex = 100;
			this.labelClientId.Text = "Client ID";
			this.labelClientId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textClientId
			// 
			this.textClientId.Location = new System.Drawing.Point(160, 20);
			this.textClientId.Name = "textClientId";
			this.textClientId.Size = new System.Drawing.Size(225, 20);
			this.textClientId.TabIndex = 0;
			// 
			// labelClientSecret
			// 
			this.labelClientSecret.Location = new System.Drawing.Point(6, 47);
			this.labelClientSecret.Name = "labelClientSecret";
			this.labelClientSecret.Size = new System.Drawing.Size(150, 16);
			this.labelClientSecret.TabIndex = 101;
			this.labelClientSecret.Text = "Client secret";
			this.labelClientSecret.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textClientSecret
			// 
			this.textClientSecret.Location = new System.Drawing.Point(160, 46);
			this.textClientSecret.Name = "textClientSecret";
			this.textClientSecret.Size = new System.Drawing.Size(225, 20);
			this.textClientSecret.TabIndex = 1;
			this.textClientSecret.UseSystemPasswordChar = true;
			// 
			// labelEnvironment
			// 
			this.labelEnvironment.Location = new System.Drawing.Point(6, 73);
			this.labelEnvironment.Name = "labelEnvironment";
			this.labelEnvironment.Size = new System.Drawing.Size(150, 16);
			this.labelEnvironment.TabIndex = 102;
			this.labelEnvironment.Text = "Environment";
			this.labelEnvironment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboEnvironment
			// 
			this.comboEnvironment.Location = new System.Drawing.Point(160, 72);
			this.comboEnvironment.Name = "comboEnvironment";
			this.comboEnvironment.Size = new System.Drawing.Size(225, 21);
			this.comboEnvironment.TabIndex = 2;
			// 
			// labelApiBaseUrl
			// 
			this.labelApiBaseUrl.Location = new System.Drawing.Point(6, 99);
			this.labelApiBaseUrl.Name = "labelApiBaseUrl";
			this.labelApiBaseUrl.Size = new System.Drawing.Size(150, 16);
			this.labelApiBaseUrl.TabIndex = 103;
			this.labelApiBaseUrl.Text = "API base URL";
			this.labelApiBaseUrl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textApiBaseUrl
			// 
			this.textApiBaseUrl.Location = new System.Drawing.Point(160, 98);
			this.textApiBaseUrl.Name = "textApiBaseUrl";
			this.textApiBaseUrl.Size = new System.Drawing.Size(225, 20);
			this.textApiBaseUrl.TabIndex = 3;
			// 
			// labelIhsBaseUrl
			// 
			this.labelIhsBaseUrl.Location = new System.Drawing.Point(6, 125);
			this.labelIhsBaseUrl.Name = "labelIhsBaseUrl";
			this.labelIhsBaseUrl.Size = new System.Drawing.Size(150, 16);
			this.labelIhsBaseUrl.TabIndex = 104;
			this.labelIhsBaseUrl.Text = "Hosted-page base URL";
			this.labelIhsBaseUrl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textIhsBaseUrl
			// 
			this.textIhsBaseUrl.Location = new System.Drawing.Point(160, 124);
			this.textIhsBaseUrl.Name = "textIhsBaseUrl";
			this.textIhsBaseUrl.Size = new System.Drawing.Size(225, 20);
			this.textIhsBaseUrl.TabIndex = 4;
			// 
			// labelPartnerCode
			// 
			this.labelPartnerCode.Location = new System.Drawing.Point(6, 151);
			this.labelPartnerCode.Name = "labelPartnerCode";
			this.labelPartnerCode.Size = new System.Drawing.Size(150, 16);
			this.labelPartnerCode.TabIndex = 105;
			this.labelPartnerCode.Text = "Partner code";
			this.labelPartnerCode.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textPartnerCode
			// 
			this.textPartnerCode.Location = new System.Drawing.Point(160, 150);
			this.textPartnerCode.Name = "textPartnerCode";
			this.textPartnerCode.Size = new System.Drawing.Size(225, 20);
			this.textPartnerCode.TabIndex = 5;
			// 
			// labelTokenPath
			// 
			this.labelTokenPath.Location = new System.Drawing.Point(6, 177);
			this.labelTokenPath.Name = "labelTokenPath";
			this.labelTokenPath.Size = new System.Drawing.Size(150, 16);
			this.labelTokenPath.TabIndex = 106;
			this.labelTokenPath.Text = "Token path";
			this.labelTokenPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textTokenPath
			// 
			this.textTokenPath.Location = new System.Drawing.Point(160, 176);
			this.textTokenPath.Name = "textTokenPath";
			this.textTokenPath.Size = new System.Drawing.Size(225, 20);
			this.textTokenPath.TabIndex = 6;
			// 
			// labelPrefillPath
			// 
			this.labelPrefillPath.Location = new System.Drawing.Point(6, 203);
			this.labelPrefillPath.Name = "labelPrefillPath";
			this.labelPrefillPath.Size = new System.Drawing.Size(150, 16);
			this.labelPrefillPath.TabIndex = 107;
			this.labelPrefillPath.Text = "Prefill path";
			this.labelPrefillPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textPrefillPath
			// 
			this.textPrefillPath.Location = new System.Drawing.Point(160, 202);
			this.textPrefillPath.Name = "textPrefillPath";
			this.textPrefillPath.Size = new System.Drawing.Size(225, 20);
			this.textPrefillPath.TabIndex = 7;
			// 
			// labelPullbackPath
			// 
			this.labelPullbackPath.Location = new System.Drawing.Point(6, 229);
			this.labelPullbackPath.Name = "labelPullbackPath";
			this.labelPullbackPath.Size = new System.Drawing.Size(150, 16);
			this.labelPullbackPath.TabIndex = 108;
			this.labelPullbackPath.Text = "Pullback path";
			this.labelPullbackPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textPullbackPath
			// 
			this.textPullbackPath.Location = new System.Drawing.Point(160, 228);
			this.textPullbackPath.Name = "textPullbackPath";
			this.textPullbackPath.Size = new System.Drawing.Size(225, 20);
			this.textPullbackPath.TabIndex = 8;
			// 
			// labelBatchPath
			// 
			this.labelBatchPath.Location = new System.Drawing.Point(6, 255);
			this.labelBatchPath.Name = "labelBatchPath";
			this.labelBatchPath.Size = new System.Drawing.Size(150, 16);
			this.labelBatchPath.TabIndex = 109;
			this.labelBatchPath.Text = "Batch path";
			this.labelBatchPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textBatchPath
			// 
			this.textBatchPath.Location = new System.Drawing.Point(160, 254);
			this.textBatchPath.Name = "textBatchPath";
			this.textBatchPath.Size = new System.Drawing.Size(225, 20);
			this.textBatchPath.TabIndex = 9;
			// 
			// labelSessionParam
			// 
			this.labelSessionParam.Location = new System.Drawing.Point(6, 281);
			this.labelSessionParam.Name = "labelSessionParam";
			this.labelSessionParam.Size = new System.Drawing.Size(150, 16);
			this.labelSessionParam.TabIndex = 110;
			this.labelSessionParam.Text = "Session parameter";
			this.labelSessionParam.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textSessionParam
			// 
			this.textSessionParam.Location = new System.Drawing.Point(160, 280);
			this.textSessionParam.Name = "textSessionParam";
			this.textSessionParam.Size = new System.Drawing.Size(225, 20);
			this.textSessionParam.TabIndex = 10;
			// 
			// labelClientAuth
			// 
			this.labelClientAuth.Location = new System.Drawing.Point(6, 307);
			this.labelClientAuth.Name = "labelClientAuth";
			this.labelClientAuth.Size = new System.Drawing.Size(150, 16);
			this.labelClientAuth.TabIndex = 111;
			this.labelClientAuth.Text = "Client authentication";
			this.labelClientAuth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboClientAuth
			// 
			this.comboClientAuth.Location = new System.Drawing.Point(160, 306);
			this.comboClientAuth.Name = "comboClientAuth";
			this.comboClientAuth.Size = new System.Drawing.Size(225, 21);
			this.comboClientAuth.TabIndex = 11;
			// 
			// labelScope
			// 
			this.labelScope.Location = new System.Drawing.Point(6, 333);
			this.labelScope.Name = "labelScope";
			this.labelScope.Size = new System.Drawing.Size(150, 16);
			this.labelScope.TabIndex = 112;
			this.labelScope.Text = "Scope (optional)";
			this.labelScope.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textScope
			// 
			this.textScope.Location = new System.Drawing.Point(160, 332);
			this.textScope.Name = "textScope";
			this.textScope.Size = new System.Drawing.Size(225, 20);
			this.textScope.TabIndex = 12;
			//
			// butTestConnection
			//
			this.butTestConnection.Location = new System.Drawing.Point(160, 362);
			this.butTestConnection.Name = "butTestConnection";
			this.butTestConnection.Size = new System.Drawing.Size(110, 24);
			this.butTestConnection.TabIndex = 20;
			this.butTestConnection.Text = "Test connection";
			this.butTestConnection.Click += new System.EventHandler(this.butTestConnection_Click);
			//
			// labelConnectionStatus
			//
			this.labelConnectionStatus.Location = new System.Drawing.Point(6, 391);
			this.labelConnectionStatus.Name = "labelConnectionStatus";
			this.labelConnectionStatus.Size = new System.Drawing.Size(383, 36);
			this.labelConnectionStatus.TabIndex = 21;
			this.labelConnectionStatus.Text = "";
			this.labelConnectionStatus.TextAlign = System.Drawing.ContentAlignment.TopLeft;
			//
			// groupBackground
			//
			this.groupBackground.Controls.Add(this.labelJobComputer);
			this.groupBackground.Controls.Add(this.butRunHere);
			this.groupBackground.Controls.Add(this.labelProcessTime);
			this.groupBackground.Controls.Add(this.textProcessTime);
			this.groupBackground.Controls.Add(this.labelPullbackTime);
			this.groupBackground.Controls.Add(this.textPullbackTime);
			this.groupBackground.Controls.Add(this.labelTimeFormat);
			this.groupBackground.Location = new System.Drawing.Point(605, 452);
			this.groupBackground.Name = "groupBackground";
			this.groupBackground.Size = new System.Drawing.Size(395, 104);
			this.groupBackground.TabIndex = 9;
			this.groupBackground.Text = "Background tasks (batch QuickScreen and result checks)";
			//
			// labelJobComputer
			//
			this.labelJobComputer.Location = new System.Drawing.Point(6, 20);
			this.labelJobComputer.Name = "labelJobComputer";
			this.labelJobComputer.Size = new System.Drawing.Size(265, 30);
			this.labelJobComputer.TabIndex = 0;
			this.labelJobComputer.Text = "Run on: (not set)";
			this.labelJobComputer.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butRunHere
			//
			this.butRunHere.Location = new System.Drawing.Point(275, 23);
			this.butRunHere.Name = "butRunHere";
			this.butRunHere.Size = new System.Drawing.Size(114, 24);
			this.butRunHere.TabIndex = 1;
			this.butRunHere.Text = "Run on this computer";
			this.butRunHere.Click += new System.EventHandler(this.butRunHere_Click);
			//
			// labelProcessTime
			//
			this.labelProcessTime.Location = new System.Drawing.Point(6, 55);
			this.labelProcessTime.Name = "labelProcessTime";
			this.labelProcessTime.Size = new System.Drawing.Size(150, 16);
			this.labelProcessTime.TabIndex = 2;
			this.labelProcessTime.Text = "Batch process time";
			this.labelProcessTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textProcessTime
			//
			this.textProcessTime.Location = new System.Drawing.Point(160, 54);
			this.textProcessTime.Name = "textProcessTime";
			this.textProcessTime.Size = new System.Drawing.Size(60, 20);
			this.textProcessTime.TabIndex = 3;
			//
			// labelPullbackTime
			//
			this.labelPullbackTime.Location = new System.Drawing.Point(6, 79);
			this.labelPullbackTime.Name = "labelPullbackTime";
			this.labelPullbackTime.Size = new System.Drawing.Size(150, 16);
			this.labelPullbackTime.TabIndex = 4;
			this.labelPullbackTime.Text = "First batch result check";
			this.labelPullbackTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textPullbackTime
			//
			this.textPullbackTime.Location = new System.Drawing.Point(160, 78);
			this.textPullbackTime.Name = "textPullbackTime";
			this.textPullbackTime.Size = new System.Drawing.Size(60, 20);
			this.textPullbackTime.TabIndex = 5;
			//
			// labelTimeFormat
			//
			this.labelTimeFormat.Location = new System.Drawing.Point(226, 55);
			this.labelTimeFormat.Name = "labelTimeFormat";
			this.labelTimeFormat.Size = new System.Drawing.Size(163, 16);
			this.labelTimeFormat.TabIndex = 6;
			this.labelTimeFormat.Text = "24-hour time, e.g. 07:20";
			this.labelTimeFormat.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// FormCareCreditSetup
			//
			this.ClientSize = new System.Drawing.Size(1012, 604);
			this.Controls.Add(this.groupBackground);
			this.Controls.Add(this.groupConnection);
			this.Controls.Add(this.comboPatFieldAvailableCredit);
			this.Controls.Add(this.labelAvailableCredit);
			this.Controls.Add(this.comboPatFieldPreApprvAmt);
			this.Controls.Add(this.labelPreApprvAmt);
			this.Controls.Add(this.labelMerchantClosedDescription);
			this.Controls.Add(this.checkMerchantNumByProv);
			this.Controls.Add(this.labelMerchantNumberPractice);
			this.Controls.Add(this.textMerchantNumberPractice);
			this.Controls.Add(this.comboPatFieldDef);
			this.Controls.Add(this.labelPatFieldType);
			this.Controls.Add(this.comboClinics);
			this.Controls.Add(this.groupClinicSettings);
			this.Controls.Add(this.checkEnabled);
			this.Controls.Add(this.butSave);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormCareCreditSetup";
			this.Text = "CareCredit Setup";
			this.Load += new System.EventHandler(this.FormCareCreditSetup_Load);
			this.groupClinicSettings.ResumeLayout(false);
			this.groupClinicSettings.PerformLayout();
			this.groupQSBatch.ResumeLayout(false);
			this.groupPromotions.ResumeLayout(false);
			this.groupConnection.ResumeLayout(false);
			this.groupConnection.PerformLayout();
			this.groupBackground.ResumeLayout(false);
			this.groupBackground.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private UI.Button butSave;
		private OpenDental.UI.CheckBox checkEnabled;
		private OpenDental.UI.GroupBox groupClinicSettings;
		private UI.ComboBoxClinicPicker comboClinics;
		private System.Windows.Forms.Label labelPaymentType;
		private UI.ComboBox comboPaymentType;
		private OpenDental.UI.GroupBox groupPromotions;
		private UI.Button butPromotions;
		private OpenDental.UI.GroupBox groupQSBatch;
		private System.Windows.Forms.Label labelQSDays;
		private UI.ComboBox comboBoxDaysOut;
		private UI.ComboBox comboPatFieldDef;
		private System.Windows.Forms.Label labelPatFieldType;
		private System.Windows.Forms.TextBox textMerchantNumberPractice;
		private System.Windows.Forms.Label labelMerchantNumberPractice;
		private OpenDental.UI.CheckBox checkMerchantNumByProv;
		private System.Windows.Forms.Label labelMerchantNumberClinic;
		private System.Windows.Forms.TextBox textMerchantNumberClinic;
		private System.Windows.Forms.Label labelMerchantClosedDescription;
		private UI.ComboBox comboPatFieldPreApprvAmt;
		private System.Windows.Forms.Label labelPreApprvAmt;
		private UI.ComboBox comboPatFieldAvailableCredit;
		private System.Windows.Forms.Label labelAvailableCredit;
		private UI.ListBox listBoxQSOptions;
		private System.Windows.Forms.Label labelQSBatch;
		private OpenDental.UI.GroupBox groupConnection;
		private System.Windows.Forms.Label labelClientId;
		private System.Windows.Forms.TextBox textClientId;
		private System.Windows.Forms.Label labelClientSecret;
		private System.Windows.Forms.TextBox textClientSecret;
		private System.Windows.Forms.Label labelEnvironment;
		private UI.ComboBox comboEnvironment;
		private System.Windows.Forms.Label labelApiBaseUrl;
		private System.Windows.Forms.TextBox textApiBaseUrl;
		private System.Windows.Forms.Label labelIhsBaseUrl;
		private System.Windows.Forms.TextBox textIhsBaseUrl;
		private System.Windows.Forms.Label labelPartnerCode;
		private System.Windows.Forms.TextBox textPartnerCode;
		private System.Windows.Forms.Label labelTokenPath;
		private System.Windows.Forms.TextBox textTokenPath;
		private System.Windows.Forms.Label labelPrefillPath;
		private System.Windows.Forms.TextBox textPrefillPath;
		private System.Windows.Forms.Label labelPullbackPath;
		private System.Windows.Forms.TextBox textPullbackPath;
		private System.Windows.Forms.Label labelBatchPath;
		private System.Windows.Forms.TextBox textBatchPath;
		private System.Windows.Forms.Label labelSessionParam;
		private System.Windows.Forms.TextBox textSessionParam;
		private System.Windows.Forms.Label labelClientAuth;
		private UI.ComboBox comboClientAuth;
		private System.Windows.Forms.Label labelScope;
		private System.Windows.Forms.TextBox textScope;
		private UI.Button butTestConnection;
		private System.Windows.Forms.Label labelConnectionStatus;
		private OpenDental.UI.GroupBox groupBackground;
		private System.Windows.Forms.Label labelJobComputer;
		private UI.Button butRunHere;
		private System.Windows.Forms.Label labelProcessTime;
		private System.Windows.Forms.TextBox textProcessTime;
		private System.Windows.Forms.Label labelPullbackTime;
		private System.Windows.Forms.TextBox textPullbackTime;
		private System.Windows.Forms.Label labelTimeFormat;
	}
}