namespace OpenPmsDental {
	partial class FormSshHostKeys {
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
			this.labelIntro = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.labelServer = new System.Windows.Forms.Label();
			this.textServer = new System.Windows.Forms.TextBox();
			this.labelStoredKey = new System.Windows.Forms.Label();
			this.textStoredKey = new System.Windows.Forms.TextBox();
			this.labelRefusedKey = new System.Windows.Forms.Label();
			this.textRefusedKey = new System.Windows.Forms.TextBox();
			this.butAccept = new OpenPmsDental.UI.Button();
			this.butRemove = new OpenPmsDental.UI.Button();
			this.butRefresh = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelIntro
			//
			this.labelIntro.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelIntro.Location = new System.Drawing.Point(12, 9);
			this.labelIntro.Name = "labelIntro";
			this.labelIntro.Size = new System.Drawing.Size(1076, 32);
			this.labelIntro.TabIndex = 0;
			this.labelIntro.Text = "The first successful connection to an SSH server stores its host key. A server that later presents another key is refused before the user name and password are sent, and the key it presented shows under Changed Key Refused.";
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 44);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(1076, 340);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "SSH Host Keys";
			this.gridMain.TranslationName = "TableSshHostKeys";
			this.gridMain.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellClick);
			//
			// labelServer
			//
			this.labelServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelServer.Location = new System.Drawing.Point(12, 394);
			this.labelServer.Name = "labelServer";
			this.labelServer.Size = new System.Drawing.Size(130, 20);
			this.labelServer.TabIndex = 2;
			this.labelServer.Text = "Server";
			this.labelServer.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textServer
			//
			this.textServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.textServer.Location = new System.Drawing.Point(145, 394);
			this.textServer.Name = "textServer";
			this.textServer.ReadOnly = true;
			this.textServer.Size = new System.Drawing.Size(300, 20);
			this.textServer.TabIndex = 3;
			//
			// labelStoredKey
			//
			this.labelStoredKey.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelStoredKey.Location = new System.Drawing.Point(12, 420);
			this.labelStoredKey.Name = "labelStoredKey";
			this.labelStoredKey.Size = new System.Drawing.Size(130, 20);
			this.labelStoredKey.TabIndex = 4;
			this.labelStoredKey.Text = "Stored key (old)";
			this.labelStoredKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textStoredKey
			//
			this.textStoredKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textStoredKey.Location = new System.Drawing.Point(145, 420);
			this.textStoredKey.Name = "textStoredKey";
			this.textStoredKey.ReadOnly = true;
			this.textStoredKey.Size = new System.Drawing.Size(690, 20);
			this.textStoredKey.TabIndex = 5;
			//
			// labelRefusedKey
			//
			this.labelRefusedKey.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelRefusedKey.Location = new System.Drawing.Point(12, 446);
			this.labelRefusedKey.Name = "labelRefusedKey";
			this.labelRefusedKey.Size = new System.Drawing.Size(130, 20);
			this.labelRefusedKey.TabIndex = 6;
			this.labelRefusedKey.Text = "Refused key (new)";
			this.labelRefusedKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textRefusedKey
			//
			this.textRefusedKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textRefusedKey.Location = new System.Drawing.Point(145, 446);
			this.textRefusedKey.Name = "textRefusedKey";
			this.textRefusedKey.ReadOnly = true;
			this.textRefusedKey.Size = new System.Drawing.Size(690, 20);
			this.textRefusedKey.TabIndex = 7;
			//
			// butAccept
			//
			this.butAccept.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butAccept.Location = new System.Drawing.Point(848, 444);
			this.butAccept.Name = "butAccept";
			this.butAccept.Size = new System.Drawing.Size(130, 24);
			this.butAccept.TabIndex = 8;
			this.butAccept.Text = "Accept Changed Key";
			this.butAccept.Click += new System.EventHandler(this.butAccept_Click);
			//
			// butRemove
			//
			this.butRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butRemove.Location = new System.Drawing.Point(988, 444);
			this.butRemove.Name = "butRemove";
			this.butRemove.Size = new System.Drawing.Size(100, 24);
			this.butRemove.TabIndex = 9;
			this.butRemove.Text = "Remove";
			this.butRemove.Click += new System.EventHandler(this.butRemove_Click);
			//
			// butRefresh
			//
			this.butRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRefresh.Location = new System.Drawing.Point(12, 520);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 24);
			this.butRefresh.TabIndex = 10;
			this.butRefresh.Text = "Refresh";
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(1013, 520);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 11;
			this.butClose.Text = "Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormSshHostKeys
			//
			this.ClientSize = new System.Drawing.Size(1100, 556);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.butRemove);
			this.Controls.Add(this.butAccept);
			this.Controls.Add(this.textRefusedKey);
			this.Controls.Add(this.labelRefusedKey);
			this.Controls.Add(this.textStoredKey);
			this.Controls.Add(this.labelStoredKey);
			this.Controls.Add(this.textServer);
			this.Controls.Add(this.labelServer);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelIntro);
			this.Name = "FormSshHostKeys";
			this.Text = "SSH Host Keys";
			this.Load += new System.EventHandler(this.FormSshHostKeys_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelIntro;
		private OpenPmsDental.UI.GridOD gridMain;
		private System.Windows.Forms.Label labelServer;
		private System.Windows.Forms.TextBox textServer;
		private System.Windows.Forms.Label labelStoredKey;
		private System.Windows.Forms.TextBox textStoredKey;
		private System.Windows.Forms.Label labelRefusedKey;
		private System.Windows.Forms.TextBox textRefusedKey;
		private OpenPmsDental.UI.Button butAccept;
		private OpenPmsDental.UI.Button butRemove;
		private OpenPmsDental.UI.Button butRefresh;
		private OpenPmsDental.UI.Button butClose;
	}
}
