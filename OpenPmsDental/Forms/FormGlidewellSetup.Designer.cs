namespace OpenPmsDental {
	partial class FormGlidewellSetup {
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
			this.gridAccounts = new OpenPmsDental.UI.GridOD();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.labelTransport = new System.Windows.Forms.Label();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(12, 12);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(200, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled";
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 36);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(570, 60);
			this.labelDescription.TabIndex = 1;
			this.labelDescription.Text = "Sends lab cases from the Edit Lab Case window to Glidewell for a scan from an iTero or Medit scanner, and checks their status, directly with the office\'s own Glidewell account number and API key. Sent: the patient\'s name, patient number, gender and birthdate, the provider\'s number and license number, and the lab case\'s number, instructions and products.";
			//
			// gridAccounts
			//
			this.gridAccounts.Location = new System.Drawing.Point(12, 102);
			this.gridAccounts.Name = "gridAccounts";
			this.gridAccounts.Size = new System.Drawing.Size(470, 200);
			this.gridAccounts.TabIndex = 2;
			this.gridAccounts.Title = "Glidewell Accounts";
			this.gridAccounts.TranslationName = "TableGlidewellAccts";
			this.gridAccounts.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridAccounts_CellDoubleClick);
			//
			// butAdd
			//
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.Location = new System.Drawing.Point(490, 102);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(80, 24);
			this.butAdd.TabIndex = 3;
			this.butAdd.Text = "Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			//
			// labelTransport
			//
			this.labelTransport.ForeColor = System.Drawing.Color.Firebrick;
			this.labelTransport.Location = new System.Drawing.Point(12, 310);
			this.labelTransport.Name = "labelTransport";
			this.labelTransport.Size = new System.Drawing.Size(570, 60);
			this.labelTransport.TabIndex = 4;
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(507, 381);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 5;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormGlidewellSetup
			//
			this.ClientSize = new System.Drawing.Size(594, 417);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelTransport);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.gridAccounts);
			this.Controls.Add(this.labelDescription);
			this.Controls.Add(this.checkEnabled);
			this.Name = "FormGlidewellSetup";
			this.Text = "Glidewell Lab Cases Setup";
			this.Load += new System.EventHandler(this.FormGlidewellSetup_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenPmsDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelDescription;
		private OpenPmsDental.UI.GridOD gridAccounts;
		private OpenPmsDental.UI.Button butAdd;
		private System.Windows.Forms.Label labelTransport;
		private OpenPmsDental.UI.Button butSave;
	}
}
