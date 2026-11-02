namespace OpenPmsDental{
	partial class FormSecurity {
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
			this.userControlSecurityUserGroup = new OpenPmsDental.UserControlSecurityUserGroup();
			this.menuMain = new OpenPmsDental.UI.MenuOD();
			this.SuspendLayout();
			// 
			// userControlSecurityUserGroup
			// 
			this.userControlSecurityUserGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.userControlSecurityUserGroup.IsForCEMT = false;
			this.userControlSecurityUserGroup.Location = new System.Drawing.Point(3, 26);
			this.userControlSecurityUserGroup.Name = "userControlSecurityUserGroup";
			this.userControlSecurityUserGroup.SelectedUser = null;
			this.userControlSecurityUserGroup.SelectedUserGroup = null;
			this.userControlSecurityUserGroup.Size = new System.Drawing.Size(969, 484);
			this.userControlSecurityUserGroup.TabIndex = 252;
			this.userControlSecurityUserGroup.AddUserClick += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTabsEventHandler(this.userControlSecurityTabs_AddUserClick);
			this.userControlSecurityUserGroup.CopyUserClick += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTabsEventHandler(this.UserControlSecurityTabs_CopyUserClick);
			this.userControlSecurityUserGroup.EditUserClick += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTabsEventHandler(this.userControlSecurityTabs_EditUserClick);
			this.userControlSecurityUserGroup.AddUserGroupClick += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTabsEventHandler(this.userControlSecurityTabs_AddUserGroupClick);
			this.userControlSecurityUserGroup.EditUserGroupClick += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTabsEventHandler(this.userControlSecurityTabs_EditUserGroupClick);
			this.userControlSecurityUserGroup.ReportPermissionChecked += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTreeEventHandler(this.userControlSecurityTabs_ReportPermissionChecked);
			this.userControlSecurityUserGroup.GroupPermissionChecked += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTreeEventHandler(this.userControlSecurityTabs_GroupPermissionChecked);
			this.userControlSecurityUserGroup.AdjustmentTypeDenyPermissionChecked += new OpenPmsDental.UserControlSecurityUserGroup.SecurityTreeEventHandler(this.userControlSecurityTabs_AdjustmentTypeDenyPermissionChecked);
			// 
			// menuMain
			// 
			this.menuMain.Dock = System.Windows.Forms.DockStyle.Top;
			this.menuMain.Location = new System.Drawing.Point(0, 0);
			this.menuMain.Name = "menuMain";
			this.menuMain.Size = new System.Drawing.Size(984, 24);
			this.menuMain.TabIndex = 253;
			// 
			// FormSecurity
			// 
			this.ClientSize = new System.Drawing.Size(984, 696);
			this.Controls.Add(this.userControlSecurityUserGroup);
			this.Controls.Add(this.menuMain);
			this.Name = "FormSecurity";
			this.Text = "Security";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormSecurityEdit_FormClosing);
			this.Load += new System.EventHandler(this.FormSecurityEdit_Load);
			this.ResumeLayout(false);

		}

		#endregion
		private UserControlSecurityUserGroup userControlSecurityUserGroup;
		private UI.MenuOD menuMain;
	}
}