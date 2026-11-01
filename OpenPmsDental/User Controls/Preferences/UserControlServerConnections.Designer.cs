
namespace OpenPmsDental {
	partial class UserControlServerConnections {
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
			this.checkUseReadOnlyServer = new OpenPmsDental.UI.CheckBox();
			this.groupBoxReadOnlyServerSetup = new OpenPmsDental.UI.GroupBox();
			this.radioReadOnlyServerDirect = new System.Windows.Forms.RadioButton();
			this.groupConnectionSettings = new OpenPmsDental.UI.GroupBox();
			this.labelSslCaPem = new System.Windows.Forms.Label();
			this.textSslCaPem = new System.Windows.Forms.TextBox();
			this.comboServerName = new System.Windows.Forms.ComboBox();
			this.textMysqlPass = new System.Windows.Forms.TextBox();
			this.comboDatabase = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textMysqlUser = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.radioReadOnlyServerMiddleTier = new System.Windows.Forms.RadioButton();
			this.groupMiddleTier = new OpenPmsDental.UI.GroupBox();
			this.textMiddleTierURI = new System.Windows.Forms.TextBox();
			this.labelURI = new System.Windows.Forms.Label();
			this.groupReplication = new OpenPmsDental.UI.GroupBox();
			this.labelReplicationReadDelay = new System.Windows.Forms.Label();
			this.textReplicationReadDelayMs = new OpenPmsDental.ValidNum();
			this.labelReplicationSignalLookback = new System.Windows.Forms.Label();
			this.textReplicationSignalLookbackMs = new OpenPmsDental.ValidNum();
			this.labelReplicationInfo = new System.Windows.Forms.Label();
			this.groupBoxReadOnlyServerSetup.SuspendLayout();
			this.groupConnectionSettings.SuspendLayout();
			this.groupMiddleTier.SuspendLayout();
			this.groupReplication.SuspendLayout();
			this.SuspendLayout();
			// 
			// checkUseReadOnlyServer
			// 
			this.checkUseReadOnlyServer.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkUseReadOnlyServer.Location = new System.Drawing.Point(60, 10);
			this.checkUseReadOnlyServer.Name = "checkUseReadOnlyServer";
			this.checkUseReadOnlyServer.Size = new System.Drawing.Size(400, 17);
			this.checkUseReadOnlyServer.TabIndex = 0;
			this.checkUseReadOnlyServer.Text = "Use Separate Read-Only Server\r\n";
			this.checkUseReadOnlyServer.CheckedChanged += new System.EventHandler(this.checkReadOnlyServer_CheckChanged);
			// 
			// groupBoxReadOnlyServerSetup
			// 
			this.groupBoxReadOnlyServerSetup.Controls.Add(this.radioReadOnlyServerDirect);
			this.groupBoxReadOnlyServerSetup.Controls.Add(this.groupConnectionSettings);
			this.groupBoxReadOnlyServerSetup.Controls.Add(this.radioReadOnlyServerMiddleTier);
			this.groupBoxReadOnlyServerSetup.Controls.Add(this.groupMiddleTier);
			this.groupBoxReadOnlyServerSetup.Location = new System.Drawing.Point(20, 30);
			this.groupBoxReadOnlyServerSetup.Name = "groupBoxReadOnlyServerSetup";
			this.groupBoxReadOnlyServerSetup.Size = new System.Drawing.Size(450, 323);
			this.groupBoxReadOnlyServerSetup.TabIndex = 99;
			this.groupBoxReadOnlyServerSetup.Text = "Read-Only Server Setup";
			// 
			// radioReadOnlyServerDirect
			// 
			this.radioReadOnlyServerDirect.Location = new System.Drawing.Point(9, 29);
			this.radioReadOnlyServerDirect.Name = "radioReadOnlyServerDirect";
			this.radioReadOnlyServerDirect.Size = new System.Drawing.Size(157, 18);
			this.radioReadOnlyServerDirect.TabIndex = 3;
			this.radioReadOnlyServerDirect.TabStop = true;
			this.radioReadOnlyServerDirect.Text = "Direct Connection";
			this.radioReadOnlyServerDirect.UseVisualStyleBackColor = true;
			this.radioReadOnlyServerDirect.CheckedChanged += new System.EventHandler(this.checkReadOnlyServer_CheckChanged);
			// 
			// groupConnectionSettings
			// 
			this.groupConnectionSettings.Controls.Add(this.textSslCaPem);
			this.groupConnectionSettings.Controls.Add(this.comboServerName);
			this.groupConnectionSettings.Controls.Add(this.textMysqlPass);
			this.groupConnectionSettings.Controls.Add(this.comboDatabase);
			this.groupConnectionSettings.Controls.Add(this.textMysqlUser);
			this.groupConnectionSettings.Controls.Add(this.labelSslCaPem);
			this.groupConnectionSettings.Controls.Add(this.label1);
			this.groupConnectionSettings.Controls.Add(this.label2);
			this.groupConnectionSettings.Controls.Add(this.label3);
			this.groupConnectionSettings.Controls.Add(this.label4);
			this.groupConnectionSettings.Location = new System.Drawing.Point(9, 54);
			this.groupConnectionSettings.Name = "groupConnectionSettings";
			this.groupConnectionSettings.Size = new System.Drawing.Size(438, 174);
			this.groupConnectionSettings.TabIndex = 100;
			this.groupConnectionSettings.Text = "Connection Settings";
			// 
			// labelSslCaPem
			// 
			this.labelSslCaPem.Location = new System.Drawing.Point(6, 140);
			this.labelSslCaPem.Name = "labelSslCaPem";
			this.labelSslCaPem.Size = new System.Drawing.Size(132, 28);
			this.labelSslCaPem.TabIndex = 105;
			this.labelSslCaPem.Text = "SSL CA certificate (.pem) path";
			this.labelSslCaPem.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textSslCaPem
			// 
			this.textSslCaPem.Location = new System.Drawing.Point(139, 144);
			this.textSslCaPem.Name = "textSslCaPem";
			this.textSslCaPem.Size = new System.Drawing.Size(292, 20);
			this.textSslCaPem.TabIndex = 105;
			// 
			// comboServerName
			// 
			this.comboServerName.FormattingEnabled = true;
			this.comboServerName.Location = new System.Drawing.Point(139, 19);
			this.comboServerName.Name = "comboServerName";
			this.comboServerName.Size = new System.Drawing.Size(292, 21);
			this.comboServerName.TabIndex = 4;
			this.comboServerName.DropDown += new System.EventHandler(this.comboComputers_DropDown);
			// 
			// textMysqlPass
			// 
			this.textMysqlPass.Location = new System.Drawing.Point(139, 112);
			this.textMysqlPass.Name = "textMysqlPass";
			this.textMysqlPass.Size = new System.Drawing.Size(292, 20);
			this.textMysqlPass.TabIndex = 7;
			// 
			// comboDatabase
			// 
			this.comboDatabase.FormattingEnabled = true;
			this.comboDatabase.Location = new System.Drawing.Point(139, 50);
			this.comboDatabase.Name = "comboDatabase";
			this.comboDatabase.Size = new System.Drawing.Size(292, 21);
			this.comboDatabase.TabIndex = 5;
			this.comboDatabase.DropDown += new System.EventHandler(this.comboDatabase_DropDown);
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(16, 115);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(122, 17);
			this.label1.TabIndex = 104;
			this.label1.Text = "MySQL Password";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textMysqlUser
			// 
			this.textMysqlUser.Location = new System.Drawing.Point(139, 81);
			this.textMysqlUser.Name = "textMysqlUser";
			this.textMysqlUser.Size = new System.Drawing.Size(292, 20);
			this.textMysqlUser.TabIndex = 6;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(16, 22);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(122, 17);
			this.label2.TabIndex = 101;
			this.label2.Text = "Server Name";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(16, 84);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(122, 17);
			this.label3.TabIndex = 103;
			this.label3.Text = "MySQL User";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(16, 53);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(122, 17);
			this.label4.TabIndex = 102;
			this.label4.Text = "Database";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// radioReadOnlyServerMiddleTier
			// 
			this.radioReadOnlyServerMiddleTier.Location = new System.Drawing.Point(9, 241);
			this.radioReadOnlyServerMiddleTier.Name = "radioReadOnlyServerMiddleTier";
			this.radioReadOnlyServerMiddleTier.Size = new System.Drawing.Size(137, 18);
			this.radioReadOnlyServerMiddleTier.TabIndex = 8;
			this.radioReadOnlyServerMiddleTier.TabStop = true;
			this.radioReadOnlyServerMiddleTier.Text = "Middle Tier";
			this.radioReadOnlyServerMiddleTier.UseVisualStyleBackColor = true;
			this.radioReadOnlyServerMiddleTier.CheckedChanged += new System.EventHandler(this.checkReadOnlyServer_CheckChanged);
			// 
			// groupMiddleTier
			// 
			this.groupMiddleTier.Controls.Add(this.textMiddleTierURI);
			this.groupMiddleTier.Controls.Add(this.labelURI);
			this.groupMiddleTier.Location = new System.Drawing.Point(9, 266);
			this.groupMiddleTier.Name = "groupMiddleTier";
			this.groupMiddleTier.Size = new System.Drawing.Size(438, 36);
			this.groupMiddleTier.TabIndex = 105;
			this.groupMiddleTier.Text = "";
			// 
			// textMiddleTierURI
			// 
			this.textMiddleTierURI.Location = new System.Drawing.Point(139, 8);
			this.textMiddleTierURI.Name = "textMiddleTierURI";
			this.textMiddleTierURI.Size = new System.Drawing.Size(292, 20);
			this.textMiddleTierURI.TabIndex = 9;
			// 
			// labelURI
			// 
			this.labelURI.Location = new System.Drawing.Point(16, 11);
			this.labelURI.Name = "labelURI";
			this.labelURI.Size = new System.Drawing.Size(122, 17);
			this.labelURI.TabIndex = 106;
			this.labelURI.Text = "URI";
			this.labelURI.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupReplication
			// 
			this.groupReplication.Controls.Add(this.labelReplicationReadDelay);
			this.groupReplication.Controls.Add(this.textReplicationReadDelayMs);
			this.groupReplication.Controls.Add(this.labelReplicationSignalLookback);
			this.groupReplication.Controls.Add(this.textReplicationSignalLookbackMs);
			this.groupReplication.Controls.Add(this.labelReplicationInfo);
			this.groupReplication.Location = new System.Drawing.Point(20, 365);
			this.groupReplication.Name = "groupReplication";
			this.groupReplication.Size = new System.Drawing.Size(450, 156);
			this.groupReplication.TabIndex = 110;
			this.groupReplication.Text = "Replication";
			// 
			// labelReplicationReadDelay
			// 
			this.labelReplicationReadDelay.Location = new System.Drawing.Point(16, 21);
			this.labelReplicationReadDelay.Name = "labelReplicationReadDelay";
			this.labelReplicationReadDelay.Size = new System.Drawing.Size(320, 17);
			this.labelReplicationReadDelay.TabIndex = 111;
			this.labelReplicationReadDelay.Text = "Read delay after write (ms)";
			this.labelReplicationReadDelay.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textReplicationReadDelayMs
			// 
			this.textReplicationReadDelayMs.Location = new System.Drawing.Point(339, 19);
			this.textReplicationReadDelayMs.MaxVal = 60000;
			this.textReplicationReadDelayMs.MinVal = 0;
			this.textReplicationReadDelayMs.Name = "textReplicationReadDelayMs";
			this.textReplicationReadDelayMs.Size = new System.Drawing.Size(60, 20);
			this.textReplicationReadDelayMs.TabIndex = 112;
			this.textReplicationReadDelayMs.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// labelReplicationSignalLookback
			// 
			this.labelReplicationSignalLookback.Location = new System.Drawing.Point(16, 47);
			this.labelReplicationSignalLookback.Name = "labelReplicationSignalLookback";
			this.labelReplicationSignalLookback.Size = new System.Drawing.Size(320, 17);
			this.labelReplicationSignalLookback.TabIndex = 113;
			this.labelReplicationSignalLookback.Text = "Write delay for signals (ms)";
			this.labelReplicationSignalLookback.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textReplicationSignalLookbackMs
			// 
			this.textReplicationSignalLookbackMs.Location = new System.Drawing.Point(339, 45);
			this.textReplicationSignalLookbackMs.MaxVal = 60000;
			this.textReplicationSignalLookbackMs.MinVal = 0;
			this.textReplicationSignalLookbackMs.Name = "textReplicationSignalLookbackMs";
			this.textReplicationSignalLookbackMs.Size = new System.Drawing.Size(60, 20);
			this.textReplicationSignalLookbackMs.TabIndex = 114;
			this.textReplicationSignalLookbackMs.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// labelReplicationInfo
			// 
			this.labelReplicationInfo.Location = new System.Drawing.Point(6, 72);
			this.labelReplicationInfo.Name = "labelReplicationInfo";
			this.labelReplicationInfo.Size = new System.Drawing.Size(438, 78);
			this.labelReplicationInfo.TabIndex = 115;
			this.labelReplicationInfo.Text = "Read delay: after this workstation saves, reads meant for the read-only server use the main server for this long. Write delay: signal checks look back this much further, so signals that reach a replica late are not missed. 0 (off) to 60000. Used only with a read-only server or replication servers.";
			// 
			// UserControlServerConnections
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			this.Controls.Add(this.checkUseReadOnlyServer);
			this.Controls.Add(this.groupBoxReadOnlyServerSetup);
			this.Controls.Add(this.groupReplication);
			this.Name = "UserControlServerConnections";
			this.Size = new System.Drawing.Size(494, 660);
			this.groupBoxReadOnlyServerSetup.ResumeLayout(false);
			this.groupConnectionSettings.ResumeLayout(false);
			this.groupConnectionSettings.PerformLayout();
			this.groupMiddleTier.ResumeLayout(false);
			this.groupMiddleTier.PerformLayout();
			this.groupReplication.ResumeLayout(false);
			this.groupReplication.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.TextBox textMysqlPass;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textMysqlUser;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private UI.GroupBox groupConnectionSettings;
		private OpenPmsDental.UI.CheckBox checkUseReadOnlyServer;
		private System.Windows.Forms.RadioButton radioReadOnlyServerMiddleTier;
		private System.Windows.Forms.RadioButton radioReadOnlyServerDirect;
		private System.Windows.Forms.TextBox textMiddleTierURI;
		private System.Windows.Forms.Label labelURI;
		private UI.GroupBox groupMiddleTier;
		private UI.GroupBox groupBoxReadOnlyServerSetup;
		private System.Windows.Forms.ComboBox comboServerName;
		private System.Windows.Forms.ComboBox comboDatabase;
		private System.Windows.Forms.Label labelSslCaPem;
		private System.Windows.Forms.TextBox textSslCaPem;
		private UI.GroupBox groupReplication;
		private System.Windows.Forms.Label labelReplicationReadDelay;
		private ValidNum textReplicationReadDelayMs;
		private System.Windows.Forms.Label labelReplicationSignalLookback;
		private ValidNum textReplicationSignalLookbackMs;
		private System.Windows.Forms.Label labelReplicationInfo;
	}
}
