namespace OpenPmsDental {
	partial class FormGlobalPaymentsV2Setup {
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
			this.checkEnabled = new OpenPmsDental.UI.CheckBox();
			this.labelDescription = new System.Windows.Forms.Label();
			this.checkUseSandbox = new OpenPmsDental.UI.CheckBox();
			this.labelClinic = new System.Windows.Forms.Label();
			this.comboClinic = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.groupPaySettings = new OpenPmsDental.UI.GroupBox();
			this.labelAppId = new System.Windows.Forms.Label();
			this.textAppId = new System.Windows.Forms.TextBox();
			this.labelAppKey = new System.Windows.Forms.Label();
			this.textAppKey = new System.Windows.Forms.TextBox();
			this.labelKeyStatus = new System.Windows.Forms.Label();
			this.labelAccountName = new System.Windows.Forms.Label();
			this.textAccountName = new System.Windows.Forms.TextBox();
			this.labelPaymentType = new System.Windows.Forms.Label();
			this.comboPaymentType = new OpenPmsDental.UI.ComboBox();
			this.checkPrintReceipt = new OpenPmsDental.UI.CheckBox();
			this.checkPreventSavingNewCC = new OpenPmsDental.UI.CheckBox();
			this.checkForceDuplicates = new OpenPmsDental.UI.CheckBox();
			this.labelClinicHint = new System.Windows.Forms.Label();
			this.butTest = new OpenPmsDental.UI.Button();
			this.labelTestResult = new System.Windows.Forms.Label();
			this.groupTerminals = new OpenPmsDental.UI.GroupBox();
			this.gridTerminals = new OpenPmsDental.UI.GridOD();
			this.butAddTerminal = new OpenPmsDental.UI.Button();
			this.butDeleteTerminal = new OpenPmsDental.UI.Button();
			this.butTestTerminal = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.groupPaySettings.SuspendLayout();
			this.groupTerminals.SuspendLayout();
			this.SuspendLayout();
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(12, 12);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(260, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled (affects all clinics)";
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 36);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(616, 44);
			this.labelDescription.TabIndex = 1;
			this.labelDescription.Text = "Takes card payments on Global Payments card terminals, and charges cards saved through them, by calling Global Payments directly with the office\'s own API app (App ID and App Key). Only one of X-Charge, Global Payments V1 and Global Payments V2 can be enabled at a time.";
			//
			// checkUseSandbox
			//
			this.checkUseSandbox.Location = new System.Drawing.Point(12, 84);
			this.checkUseSandbox.Name = "checkUseSandbox";
			this.checkUseSandbox.Size = new System.Drawing.Size(420, 18);
			this.checkUseSandbox.TabIndex = 2;
			this.checkUseSandbox.Text = "Use the sandbox (test mode, nothing is charged; affects all clinics)";
			//
			// labelClinic
			//
			this.labelClinic.Location = new System.Drawing.Point(12, 112);
			this.labelClinic.Name = "labelClinic";
			this.labelClinic.Size = new System.Drawing.Size(130, 16);
			this.labelClinic.TabIndex = 3;
			this.labelClinic.Text = "Clinic";
			this.labelClinic.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboClinic
			//
			this.comboClinic.HqDescription = "Headquarters";
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(146, 110);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(229, 21);
			this.comboClinic.TabIndex = 4;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboClinic_SelectionChangeCommitted);
			//
			// groupPaySettings
			//
			this.groupPaySettings.Controls.Add(this.labelAppId);
			this.groupPaySettings.Controls.Add(this.textAppId);
			this.groupPaySettings.Controls.Add(this.labelAppKey);
			this.groupPaySettings.Controls.Add(this.textAppKey);
			this.groupPaySettings.Controls.Add(this.labelKeyStatus);
			this.groupPaySettings.Controls.Add(this.labelAccountName);
			this.groupPaySettings.Controls.Add(this.textAccountName);
			this.groupPaySettings.Controls.Add(this.labelPaymentType);
			this.groupPaySettings.Controls.Add(this.comboPaymentType);
			this.groupPaySettings.Controls.Add(this.checkPrintReceipt);
			this.groupPaySettings.Controls.Add(this.checkPreventSavingNewCC);
			this.groupPaySettings.Controls.Add(this.checkForceDuplicates);
			this.groupPaySettings.Controls.Add(this.labelClinicHint);
			this.groupPaySettings.Controls.Add(this.butTest);
			this.groupPaySettings.Controls.Add(this.labelTestResult);
			this.groupPaySettings.Location = new System.Drawing.Point(12, 140);
			this.groupPaySettings.Name = "groupPaySettings";
			this.groupPaySettings.Size = new System.Drawing.Size(616, 254);
			this.groupPaySettings.TabIndex = 5;
			this.groupPaySettings.Text = "Payment Settings";
			//
			// labelAppId
			//
			this.labelAppId.Location = new System.Drawing.Point(6, 22);
			this.labelAppId.Name = "labelAppId";
			this.labelAppId.Size = new System.Drawing.Size(124, 16);
			this.labelAppId.TabIndex = 0;
			this.labelAppId.Text = "App ID";
			this.labelAppId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAppId
			//
			this.textAppId.Location = new System.Drawing.Point(134, 20);
			this.textAppId.Name = "textAppId";
			this.textAppId.Size = new System.Drawing.Size(280, 20);
			this.textAppId.TabIndex = 1;
			//
			// labelAppKey
			//
			this.labelAppKey.Location = new System.Drawing.Point(6, 48);
			this.labelAppKey.Name = "labelAppKey";
			this.labelAppKey.Size = new System.Drawing.Size(124, 16);
			this.labelAppKey.TabIndex = 2;
			this.labelAppKey.Text = "App Key";
			this.labelAppKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAppKey
			//
			this.textAppKey.Location = new System.Drawing.Point(134, 46);
			this.textAppKey.Name = "textAppKey";
			this.textAppKey.PasswordChar = '*';
			this.textAppKey.Size = new System.Drawing.Size(280, 20);
			this.textAppKey.TabIndex = 3;
			//
			// labelKeyStatus
			//
			this.labelKeyStatus.Location = new System.Drawing.Point(420, 44);
			this.labelKeyStatus.Name = "labelKeyStatus";
			this.labelKeyStatus.Size = new System.Drawing.Size(190, 30);
			this.labelKeyStatus.TabIndex = 4;
			this.labelKeyStatus.Text = "Not set.";
			//
			// labelAccountName
			//
			this.labelAccountName.Location = new System.Drawing.Point(6, 76);
			this.labelAccountName.Name = "labelAccountName";
			this.labelAccountName.Size = new System.Drawing.Size(124, 16);
			this.labelAccountName.TabIndex = 5;
			this.labelAccountName.Text = "Account name";
			this.labelAccountName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAccountName
			//
			this.textAccountName.Location = new System.Drawing.Point(134, 74);
			this.textAccountName.Name = "textAccountName";
			this.textAccountName.Size = new System.Drawing.Size(280, 20);
			this.textAccountName.TabIndex = 6;
			//
			// labelPaymentType
			//
			this.labelPaymentType.Location = new System.Drawing.Point(6, 104);
			this.labelPaymentType.Name = "labelPaymentType";
			this.labelPaymentType.Size = new System.Drawing.Size(124, 16);
			this.labelPaymentType.TabIndex = 7;
			this.labelPaymentType.Text = "Payment Type";
			this.labelPaymentType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboPaymentType
			//
			this.comboPaymentType.Location = new System.Drawing.Point(134, 102);
			this.comboPaymentType.Name = "comboPaymentType";
			this.comboPaymentType.Size = new System.Drawing.Size(200, 21);
			this.comboPaymentType.TabIndex = 8;
			//
			// checkPrintReceipt
			//
			this.checkPrintReceipt.Location = new System.Drawing.Point(134, 130);
			this.checkPrintReceipt.Name = "checkPrintReceipt";
			this.checkPrintReceipt.Size = new System.Drawing.Size(320, 18);
			this.checkPrintReceipt.TabIndex = 9;
			this.checkPrintReceipt.Text = "Print receipts by default";
			//
			// checkPreventSavingNewCC
			//
			this.checkPreventSavingNewCC.Location = new System.Drawing.Point(134, 152);
			this.checkPreventSavingNewCC.Name = "checkPreventSavingNewCC";
			this.checkPreventSavingNewCC.Size = new System.Drawing.Size(320, 18);
			this.checkPreventSavingNewCC.TabIndex = 10;
			this.checkPreventSavingNewCC.Text = "Prevent saving new cards";
			//
			// checkForceDuplicates
			//
			this.checkForceDuplicates.Location = new System.Drawing.Point(134, 174);
			this.checkForceDuplicates.Name = "checkForceDuplicates";
			this.checkForceDuplicates.Size = new System.Drawing.Size(320, 18);
			this.checkForceDuplicates.TabIndex = 11;
			this.checkForceDuplicates.Text = "Recurring charges force duplicates by default";
			//
			// labelClinicHint
			//
			this.labelClinicHint.Location = new System.Drawing.Point(134, 196);
			this.labelClinicHint.Name = "labelClinicHint";
			this.labelClinicHint.Size = new System.Drawing.Size(476, 18);
			this.labelClinicHint.TabIndex = 12;
			this.labelClinicHint.Text = "A clinic without its own App ID uses the Headquarters App ID, App Key and account name.";
			//
			// butTest
			//
			this.butTest.Location = new System.Drawing.Point(134, 220);
			this.butTest.Name = "butTest";
			this.butTest.Size = new System.Drawing.Size(110, 24);
			this.butTest.TabIndex = 13;
			this.butTest.Text = "Test connection";
			this.butTest.Click += new System.EventHandler(this.butTest_Click);
			//
			// labelTestResult
			//
			this.labelTestResult.Location = new System.Drawing.Point(250, 216);
			this.labelTestResult.Name = "labelTestResult";
			this.labelTestResult.Size = new System.Drawing.Size(360, 34);
			this.labelTestResult.TabIndex = 14;
			//
			// groupTerminals
			//
			this.groupTerminals.Controls.Add(this.gridTerminals);
			this.groupTerminals.Controls.Add(this.butAddTerminal);
			this.groupTerminals.Controls.Add(this.butDeleteTerminal);
			this.groupTerminals.Controls.Add(this.butTestTerminal);
			this.groupTerminals.Location = new System.Drawing.Point(12, 400);
			this.groupTerminals.Name = "groupTerminals";
			this.groupTerminals.Size = new System.Drawing.Size(616, 170);
			this.groupTerminals.TabIndex = 6;
			this.groupTerminals.Text = "Terminals";
			//
			// gridTerminals
			//
			this.gridTerminals.Location = new System.Drawing.Point(8, 20);
			this.gridTerminals.Name = "gridTerminals";
			this.gridTerminals.Size = new System.Drawing.Size(480, 140);
			this.gridTerminals.TabIndex = 0;
			this.gridTerminals.Title = "Terminals";
			this.gridTerminals.TranslationName = "TableGlobalPaymentsV2Terminals";
			this.gridTerminals.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridTerminals_CellDoubleClick);
			//
			// butAddTerminal
			//
			this.butAddTerminal.Location = new System.Drawing.Point(498, 20);
			this.butAddTerminal.Name = "butAddTerminal";
			this.butAddTerminal.Size = new System.Drawing.Size(108, 24);
			this.butAddTerminal.TabIndex = 1;
			this.butAddTerminal.Text = "Add Terminal";
			this.butAddTerminal.Click += new System.EventHandler(this.butAddTerminal_Click);
			//
			// butDeleteTerminal
			//
			this.butDeleteTerminal.Location = new System.Drawing.Point(498, 50);
			this.butDeleteTerminal.Name = "butDeleteTerminal";
			this.butDeleteTerminal.Size = new System.Drawing.Size(108, 24);
			this.butDeleteTerminal.TabIndex = 2;
			this.butDeleteTerminal.Text = "Delete";
			this.butDeleteTerminal.Click += new System.EventHandler(this.butDeleteTerminal_Click);
			//
			// butTestTerminal
			//
			this.butTestTerminal.Location = new System.Drawing.Point(498, 80);
			this.butTestTerminal.Name = "butTestTerminal";
			this.butTestTerminal.Size = new System.Drawing.Size(108, 24);
			this.butTestTerminal.TabIndex = 3;
			this.butTestTerminal.Text = "Test terminal";
			this.butTestTerminal.Click += new System.EventHandler(this.butTestTerminal_Click);
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(553, 580);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 7;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormGlobalPaymentsV2Setup
			//
			this.ClientSize = new System.Drawing.Size(640, 616);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.groupTerminals);
			this.Controls.Add(this.groupPaySettings);
			this.Controls.Add(this.comboClinic);
			this.Controls.Add(this.labelClinic);
			this.Controls.Add(this.checkUseSandbox);
			this.Controls.Add(this.labelDescription);
			this.Controls.Add(this.checkEnabled);
			this.Name = "FormGlobalPaymentsV2Setup";
			this.Text = "Global Payments V2 Setup";
			this.Load += new System.EventHandler(this.FormGlobalPaymentsV2Setup_Load);
			this.groupPaySettings.ResumeLayout(false);
			this.groupPaySettings.PerformLayout();
			this.groupTerminals.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenPmsDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelDescription;
		private OpenPmsDental.UI.CheckBox checkUseSandbox;
		private System.Windows.Forms.Label labelClinic;
		private OpenPmsDental.UI.ComboBoxClinicPicker comboClinic;
		private OpenPmsDental.UI.GroupBox groupPaySettings;
		private System.Windows.Forms.Label labelAppId;
		private System.Windows.Forms.TextBox textAppId;
		private System.Windows.Forms.Label labelAppKey;
		private System.Windows.Forms.TextBox textAppKey;
		private System.Windows.Forms.Label labelKeyStatus;
		private System.Windows.Forms.Label labelAccountName;
		private System.Windows.Forms.TextBox textAccountName;
		private System.Windows.Forms.Label labelPaymentType;
		private OpenPmsDental.UI.ComboBox comboPaymentType;
		private OpenPmsDental.UI.CheckBox checkPrintReceipt;
		private OpenPmsDental.UI.CheckBox checkPreventSavingNewCC;
		private OpenPmsDental.UI.CheckBox checkForceDuplicates;
		private System.Windows.Forms.Label labelClinicHint;
		private OpenPmsDental.UI.Button butTest;
		private System.Windows.Forms.Label labelTestResult;
		private OpenPmsDental.UI.GroupBox groupTerminals;
		private OpenPmsDental.UI.GridOD gridTerminals;
		private OpenPmsDental.UI.Button butAddTerminal;
		private OpenPmsDental.UI.Button butDeleteTerminal;
		private OpenPmsDental.UI.Button butTestTerminal;
		private OpenPmsDental.UI.Button butSave;
	}
}
