namespace OpenPmsDental.User_Controls {
	partial class UserControlReportSetup {
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

		#region Component Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.labelInstructions = new System.Windows.Forms.Label();
			this.labelAllowedCount = new System.Windows.Forms.Label();
			this.labelUserGroup = new System.Windows.Forms.Label();
			this.comboUserGroup = new System.Windows.Forms.ComboBox();
			this.gridReports = new OpenPmsDental.UI.GridOD();
			this.butDown = new OpenPmsDental.UI.Button();
			this.butUp = new OpenPmsDental.UI.Button();
			this.butSetAll = new OpenPmsDental.UI.Button();
			this.butSetNone = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelInstructions
			//
			this.labelInstructions.Location = new System.Drawing.Point(3, 3);
			this.labelInstructions.Name = "labelInstructions";
			this.labelInstructions.Size = new System.Drawing.Size(380, 28);
			this.labelInstructions.TabIndex = 221;
			this.labelInstructions.Text = "Click a display name to rename the report, or a box to mark it.\r\nUp and Down move the selected report within its category.";
			this.labelInstructions.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelAllowedCount
			//
			this.labelAllowedCount.Location = new System.Drawing.Point(376, 5);
			this.labelAllowedCount.Name = "labelAllowedCount";
			this.labelAllowedCount.Size = new System.Drawing.Size(146, 24);
			this.labelAllowedCount.TabIndex = 222;
			this.labelAllowedCount.Text = "Reports allowed: 0 / 0";
			this.labelAllowedCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// labelUserGroup
			//
			this.labelUserGroup.Location = new System.Drawing.Point(6, 5);
			this.labelUserGroup.Name = "labelUserGroup";
			this.labelUserGroup.Size = new System.Drawing.Size(73, 23);
			this.labelUserGroup.TabIndex = 224;
			this.labelUserGroup.Text = "User Group";
			this.labelUserGroup.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboUserGroup
			//
			this.comboUserGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboUserGroup.Location = new System.Drawing.Point(80, 6);
			this.comboUserGroup.Name = "comboUserGroup";
			this.comboUserGroup.Size = new System.Drawing.Size(147, 21);
			this.comboUserGroup.TabIndex = 225;
			this.comboUserGroup.SelectionChangeCommitted += new System.EventHandler(this.comboUserGroup_SelectionChangeCommitted);
			//
			// gridReports
			//
			this.gridReports.Location = new System.Drawing.Point(3, 35);
			this.gridReports.Name = "gridReports";
			this.gridReports.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;
			this.gridReports.Size = new System.Drawing.Size(519, 569);
			this.gridReports.TabIndex = 214;
			this.gridReports.Title = "Reports";
			this.gridReports.TranslationName = "TableReportSetup";
			this.gridReports.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridReports_CellClick);
			this.gridReports.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridReports_CellLeave);
			//
			// butDown
			//
			this.butDown.AdjustImageLocation = new System.Drawing.Point(0, 1);
			this.butDown.Image = global::OpenPmsDental.Properties.Resources.down;
			this.butDown.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDown.Location = new System.Drawing.Point(459, 5);
			this.butDown.Name = "butDown";
			this.butDown.Size = new System.Drawing.Size(63, 24);
			this.butDown.TabIndex = 220;
			this.butDown.Text = "Down";
			this.butDown.Click += new System.EventHandler(this.butDown_Click);
			//
			// butUp
			//
			this.butUp.AdjustImageLocation = new System.Drawing.Point(0, 1);
			this.butUp.Image = global::OpenPmsDental.Properties.Resources.up;
			this.butUp.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butUp.Location = new System.Drawing.Point(392, 5);
			this.butUp.Name = "butUp";
			this.butUp.Size = new System.Drawing.Size(63, 24);
			this.butUp.TabIndex = 219;
			this.butUp.Text = "Up";
			this.butUp.Click += new System.EventHandler(this.butUp_Click);
			//
			// butSetAll
			//
			this.butSetAll.Location = new System.Drawing.Point(236, 5);
			this.butSetAll.Name = "butSetAll";
			this.butSetAll.Size = new System.Drawing.Size(66, 24);
			this.butSetAll.TabIndex = 223;
			this.butSetAll.Text = "Set All";
			this.butSetAll.Click += new System.EventHandler(this.butSetAll_Click);
			//
			// butSetNone
			//
			this.butSetNone.Location = new System.Drawing.Point(306, 5);
			this.butSetNone.Name = "butSetNone";
			this.butSetNone.Size = new System.Drawing.Size(66, 24);
			this.butSetNone.TabIndex = 226;
			this.butSetNone.Text = "Set None";
			this.butSetNone.Click += new System.EventHandler(this.butSetNone_Click);
			//
			// UserControlReportSetup
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.Controls.Add(this.butSetNone);
			this.Controls.Add(this.comboUserGroup);
			this.Controls.Add(this.labelUserGroup);
			this.Controls.Add(this.gridReports);
			this.Controls.Add(this.labelAllowedCount);
			this.Controls.Add(this.labelInstructions);
			this.Controls.Add(this.butDown);
			this.Controls.Add(this.butUp);
			this.Controls.Add(this.butSetAll);
			this.Name = "UserControlReportSetup";
			this.Size = new System.Drawing.Size(525, 607);
			this.ResumeLayout(false);

		}

		#endregion

		private UI.Button butUp;
		private UI.Button butDown;
		private System.Windows.Forms.Label labelInstructions;
		private System.Windows.Forms.Label labelAllowedCount;
		private UI.GridOD gridReports;
		private UI.Button butSetAll;
		private System.Windows.Forms.Label labelUserGroup;
		private System.Windows.Forms.ComboBox comboUserGroup;
		private UI.Button butSetNone;
	}
}
