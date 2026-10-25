namespace OpenDental {
	partial class FormClerriSetup {
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
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.labelDescription = new System.Windows.Forms.Label();
			this.labelProgName = new System.Windows.Forms.Label();
			this.textProgName = new System.Windows.Forms.TextBox();
			this.labelProgDesc = new System.Windows.Forms.Label();
			this.textProgDesc = new System.Windows.Forms.TextBox();
			this.checkShowToolbarButton = new OpenDental.UI.CheckBox();
			this.labelToolbarNote = new System.Windows.Forms.Label();
			this.labelDashboardAddress = new System.Windows.Forms.Label();
			this.textDashboardAddress = new System.Windows.Forms.TextBox();
			this.labelDashboardHint = new System.Windows.Forms.Label();
			this.labelClinic = new System.Windows.Forms.Label();
			this.comboClinic = new OpenDental.UI.ComboBoxClinicPicker();
			this.labelClientKey = new System.Windows.Forms.Label();
			this.textClientKey = new System.Windows.Forms.TextBox();
			this.butClearKey = new OpenDental.UI.Button();
			this.labelKeyStatus = new System.Windows.Forms.Label();
			this.butSave = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(150, 12);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(200, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled";
			this.checkEnabled.CheckedChanged += new System.EventHandler(this.checkEnabled_CheckedChanged);
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 36);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(510, 58);
			this.labelDescription.TabIndex = 1;
			this.labelDescription.Text = "Adds a Clerri button to the Family module that opens the office\'s Clerri dashboard in the default browser. Nothing is synced with Clerri. Only the Client Key and the selected patient\'s number are sent, and only where the dashboard address has [ClientKey] and [PatNum].";
			//
			// labelProgName
			//
			this.labelProgName.Location = new System.Drawing.Point(12, 102);
			this.labelProgName.Name = "labelProgName";
			this.labelProgName.Size = new System.Drawing.Size(136, 16);
			this.labelProgName.TabIndex = 2;
			this.labelProgName.Text = "Internal Name";
			this.labelProgName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textProgName
			//
			this.textProgName.Location = new System.Drawing.Point(150, 100);
			this.textProgName.Name = "textProgName";
			this.textProgName.ReadOnly = true;
			this.textProgName.Size = new System.Drawing.Size(250, 20);
			this.textProgName.TabIndex = 3;
			this.textProgName.TabStop = false;
			//
			// labelProgDesc
			//
			this.labelProgDesc.Location = new System.Drawing.Point(12, 128);
			this.labelProgDesc.Name = "labelProgDesc";
			this.labelProgDesc.Size = new System.Drawing.Size(136, 16);
			this.labelProgDesc.TabIndex = 4;
			this.labelProgDesc.Text = "Description";
			this.labelProgDesc.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textProgDesc
			//
			this.textProgDesc.Location = new System.Drawing.Point(150, 126);
			this.textProgDesc.Name = "textProgDesc";
			this.textProgDesc.ReadOnly = true;
			this.textProgDesc.Size = new System.Drawing.Size(250, 20);
			this.textProgDesc.TabIndex = 5;
			this.textProgDesc.TabStop = false;
			//
			// checkShowToolbarButton
			//
			this.checkShowToolbarButton.Location = new System.Drawing.Point(150, 154);
			this.checkShowToolbarButton.Name = "checkShowToolbarButton";
			this.checkShowToolbarButton.Size = new System.Drawing.Size(250, 18);
			this.checkShowToolbarButton.TabIndex = 6;
			this.checkShowToolbarButton.Text = "Show Toolbar Button";
			//
			// labelToolbarNote
			//
			this.labelToolbarNote.Location = new System.Drawing.Point(150, 174);
			this.labelToolbarNote.Name = "labelToolbarNote";
			this.labelToolbarNote.Size = new System.Drawing.Size(372, 16);
			this.labelToolbarNote.TabIndex = 7;
			this.labelToolbarNote.Text = "The Family module button. Can\'t be changed while Clerri is enabled.";
			//
			// labelDashboardAddress
			//
			this.labelDashboardAddress.Location = new System.Drawing.Point(12, 202);
			this.labelDashboardAddress.Name = "labelDashboardAddress";
			this.labelDashboardAddress.Size = new System.Drawing.Size(136, 16);
			this.labelDashboardAddress.TabIndex = 8;
			this.labelDashboardAddress.Text = "Dashboard Address";
			this.labelDashboardAddress.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDashboardAddress
			//
			this.textDashboardAddress.Location = new System.Drawing.Point(150, 200);
			this.textDashboardAddress.Name = "textDashboardAddress";
			this.textDashboardAddress.Size = new System.Drawing.Size(372, 20);
			this.textDashboardAddress.TabIndex = 9;
			//
			// labelDashboardHint
			//
			this.labelDashboardHint.Location = new System.Drawing.Point(150, 222);
			this.labelDashboardHint.Name = "labelDashboardHint";
			this.labelDashboardHint.Size = new System.Drawing.Size(372, 30);
			this.labelDashboardHint.TabIndex = 10;
			this.labelDashboardHint.Text = "From Clerri. Must start with https://. [ClientKey] and [PatNum] are replaced; nothing else is.";
			//
			// labelClinic
			//
			this.labelClinic.Location = new System.Drawing.Point(12, 262);
			this.labelClinic.Name = "labelClinic";
			this.labelClinic.Size = new System.Drawing.Size(136, 16);
			this.labelClinic.TabIndex = 11;
			this.labelClinic.Text = "Clinic";
			this.labelClinic.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboClinic
			//
			this.comboClinic.HqDescription = "Default";
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(150, 260);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(200, 21);
			this.comboClinic.TabIndex = 12;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboClinic_SelectionChangeCommitted);
			//
			// labelClientKey
			//
			this.labelClientKey.Location = new System.Drawing.Point(12, 289);
			this.labelClientKey.Name = "labelClientKey";
			this.labelClientKey.Size = new System.Drawing.Size(136, 16);
			this.labelClientKey.TabIndex = 13;
			this.labelClientKey.Text = "Client Key";
			this.labelClientKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textClientKey
			//
			this.textClientKey.Location = new System.Drawing.Point(150, 287);
			this.textClientKey.Name = "textClientKey";
			this.textClientKey.PasswordChar = '*';
			this.textClientKey.Size = new System.Drawing.Size(250, 20);
			this.textClientKey.TabIndex = 14;
			//
			// butClearKey
			//
			this.butClearKey.Location = new System.Drawing.Point(406, 285);
			this.butClearKey.Name = "butClearKey";
			this.butClearKey.Size = new System.Drawing.Size(75, 24);
			this.butClearKey.TabIndex = 15;
			this.butClearKey.Text = "Clear";
			this.butClearKey.Click += new System.EventHandler(this.butClearKey_Click);
			//
			// labelKeyStatus
			//
			this.labelKeyStatus.Location = new System.Drawing.Point(150, 312);
			this.labelKeyStatus.Name = "labelKeyStatus";
			this.labelKeyStatus.Size = new System.Drawing.Size(372, 30);
			this.labelKeyStatus.TabIndex = 16;
			this.labelKeyStatus.Text = "Not set.";
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(447, 384);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 17;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormClerriSetup
			//
			this.ClientSize = new System.Drawing.Size(534, 420);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelKeyStatus);
			this.Controls.Add(this.butClearKey);
			this.Controls.Add(this.textClientKey);
			this.Controls.Add(this.labelClientKey);
			this.Controls.Add(this.comboClinic);
			this.Controls.Add(this.labelClinic);
			this.Controls.Add(this.labelDashboardHint);
			this.Controls.Add(this.textDashboardAddress);
			this.Controls.Add(this.labelDashboardAddress);
			this.Controls.Add(this.labelToolbarNote);
			this.Controls.Add(this.checkShowToolbarButton);
			this.Controls.Add(this.textProgDesc);
			this.Controls.Add(this.labelProgDesc);
			this.Controls.Add(this.textProgName);
			this.Controls.Add(this.labelProgName);
			this.Controls.Add(this.labelDescription);
			this.Controls.Add(this.checkEnabled);
			this.Name = "FormClerriSetup";
			this.Text = "Clerri Setup";
			this.Load += new System.EventHandler(this.FormClerriSetup_Load);
			this.Shown += new System.EventHandler(this.FormClerriSetup_Shown);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelDescription;
		private System.Windows.Forms.Label labelProgName;
		private System.Windows.Forms.TextBox textProgName;
		private System.Windows.Forms.Label labelProgDesc;
		private System.Windows.Forms.TextBox textProgDesc;
		private OpenDental.UI.CheckBox checkShowToolbarButton;
		private System.Windows.Forms.Label labelToolbarNote;
		private System.Windows.Forms.Label labelDashboardAddress;
		private System.Windows.Forms.TextBox textDashboardAddress;
		private System.Windows.Forms.Label labelDashboardHint;
		private System.Windows.Forms.Label labelClinic;
		private OpenDental.UI.ComboBoxClinicPicker comboClinic;
		private System.Windows.Forms.Label labelClientKey;
		private System.Windows.Forms.TextBox textClientKey;
		private OpenDental.UI.Button butClearKey;
		private System.Windows.Forms.Label labelKeyStatus;
		private OpenDental.UI.Button butSave;
	}
}
