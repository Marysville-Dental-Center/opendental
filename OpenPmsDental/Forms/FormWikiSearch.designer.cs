namespace OpenPmsDental{
	partial class FormWikiSearch {
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
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.textSearch = new System.Windows.Forms.TextBox();
			this.checkIgnoreContent = new OpenPmsDental.UI.CheckBox();
			this.checkArchivedOnly = new OpenPmsDental.UI.CheckBox();
			this.webViewWiki = new CodeBase.Controls.ODWebView2();
			this.butOK = new OpenPmsDental.UI.Button();
			this.butRestore = new OpenPmsDental.UI.Button();
			this.checkBoxMatchWholeWord = new OpenPmsDental.UI.CheckBox();
			this.checkBoxShowMainPages = new OpenPmsDental.UI.CheckBox();
			this.butSearch = new OpenPmsDental.UI.Button();
			this.checkReportServer = new OpenPmsDental.UI.CheckBox();
			this.checkLimit = new OpenPmsDental.UI.CheckBox();
			this.textLimit = new OpenPmsDental.ValidNum();
			this.labelLimitInfo = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridMain.Location = new System.Drawing.Point(12, 62);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(248, 588);
			this.gridMain.TabIndex = 10;
			this.gridMain.Title = "Wiki Pages";
			this.gridMain.TranslationName = "TableWikiSearchPages";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			this.gridMain.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellClick);
			// 
			// textSearch
			// 
			this.textSearch.Location = new System.Drawing.Point(12, 12);
			this.textSearch.Name = "textSearch";
			this.textSearch.Size = new System.Drawing.Size(248, 20);
			this.textSearch.TabIndex = 0;
			this.textSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textSearch_KeyDown);
			// 
			// checkIgnoreContent
			// 
			this.checkIgnoreContent.Location = new System.Drawing.Point(414, 11);
			this.checkIgnoreContent.Name = "checkIgnoreContent";
			this.checkIgnoreContent.Size = new System.Drawing.Size(110, 22);
			this.checkIgnoreContent.TabIndex = 14;
			this.checkIgnoreContent.Text = "Ignore Content";
			this.checkIgnoreContent.Click += new System.EventHandler(this.checkIgnoreContent_Click);
			// 
			// checkArchivedOnly
			// 
			this.checkArchivedOnly.Location = new System.Drawing.Point(536, 11);
			this.checkArchivedOnly.Name = "checkArchivedOnly";
			this.checkArchivedOnly.Size = new System.Drawing.Size(108, 22);
			this.checkArchivedOnly.TabIndex = 15;
			this.checkArchivedOnly.Text = "Archived Only";
			this.checkArchivedOnly.CheckedChanged += new System.EventHandler(this.checkArchivedOnly_CheckedChanged);
			// 
			// webViewWiki
			// 
			this.webViewWiki.AllowExternalDrop = false;
			this.webViewWiki.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.webViewWiki.IsContextMenuEnabled = false;
			this.webViewWiki.Location = new System.Drawing.Point(266, 62);
			this.webViewWiki.Name = "webViewWiki";
			this.webViewWiki.Size = new System.Drawing.Size(825, 588);
			this.webViewWiki.TabIndex = 11;
			// 
			// butOK
			// 
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(1097, 626);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 3;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			// 
			// butRestore
			// 
			this.butRestore.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRestore.Enabled = false;
			this.butRestore.Location = new System.Drawing.Point(1097, 10);
			this.butRestore.Name = "butRestore";
			this.butRestore.Size = new System.Drawing.Size(75, 24);
			this.butRestore.TabIndex = 16;
			this.butRestore.Text = "Restore";
			this.butRestore.Click += new System.EventHandler(this.butRestore_Click);
			// 
			// checkBoxMatchWholeWord
			// 
			this.checkBoxMatchWholeWord.Location = new System.Drawing.Point(656, 14);
			this.checkBoxMatchWholeWord.Name = "checkBoxMatchWholeWord";
			this.checkBoxMatchWholeWord.Size = new System.Drawing.Size(102, 17);
			this.checkBoxMatchWholeWord.TabIndex = 18;
			this.checkBoxMatchWholeWord.Text = "Exact Match";
			this.checkBoxMatchWholeWord.CheckedChanged += new System.EventHandler(this.checkBoxMatchWholeWord_CheckedChanged);
			// 
			// checkBoxShowMainPages
			// 
			this.checkBoxShowMainPages.Checked = true;
			this.checkBoxShowMainPages.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxShowMainPages.Location = new System.Drawing.Point(770, 11);
			this.checkBoxShowMainPages.Name = "checkBoxShowMainPages";
			this.checkBoxShowMainPages.Size = new System.Drawing.Size(135, 22);
			this.checkBoxShowMainPages.TabIndex = 19;
			this.checkBoxShowMainPages.Text = "Show Main Pages";
			this.checkBoxShowMainPages.CheckedChanged += new System.EventHandler(this.checkBoxShowMainPages_CheckedChanged);
			// 
			// butSearch
			// 
			this.butSearch.Location = new System.Drawing.Point(266, 10);
			this.butSearch.Name = "butSearch";
			this.butSearch.Size = new System.Drawing.Size(75, 24);
			this.butSearch.TabIndex = 20;
			this.butSearch.Text = "Search";
			this.butSearch.Click += new System.EventHandler(this.butSearch_Click);
			// 
			// checkReportServer
			// 
			this.checkReportServer.Location = new System.Drawing.Point(911, 12);
			this.checkReportServer.Name = "checkReportServer";
			this.checkReportServer.Size = new System.Drawing.Size(145, 22);
			this.checkReportServer.TabIndex = 21;
			this.checkReportServer.Text = "Run on report server";
			// 
			// checkLimit
			// 
			this.checkLimit.Location = new System.Drawing.Point(12, 38);
			this.checkLimit.Name = "checkLimit";
			this.checkLimit.Size = new System.Drawing.Size(110, 20);
			this.checkLimit.TabIndex = 22;
			this.checkLimit.Text = "Limit results to";
			this.checkLimit.Click += new System.EventHandler(this.checkLimit_Click);
			// 
			// textLimit
			// 
			this.textLimit.Location = new System.Drawing.Point(124, 38);
			this.textLimit.MaxLength = 4;
			this.textLimit.MaxVal = 5000;
			this.textLimit.MinVal = 1;
			this.textLimit.Name = "textLimit";
			this.textLimit.ShowZero = false;
			this.textLimit.Size = new System.Drawing.Size(45, 20);
			this.textLimit.TabIndex = 23;
			this.textLimit.Text = "100";
			this.textLimit.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.textLimit.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textLimit_KeyDown);
			this.textLimit.Leave += new System.EventHandler(this.textLimit_Leave);
			// 
			// labelLimitInfo
			// 
			this.labelLimitInfo.Location = new System.Drawing.Point(266, 38);
			this.labelLimitInfo.Name = "labelLimitInfo";
			this.labelLimitInfo.Size = new System.Drawing.Size(300, 20);
			this.labelLimitInfo.TabIndex = 24;
			this.labelLimitInfo.Text = "Showing first 100 matches";
			this.labelLimitInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.labelLimitInfo.Visible = false;
			// 
			// FormWikiSearch
			// 
			this.ClientSize = new System.Drawing.Size(1184, 662);
			this.Controls.Add(this.labelLimitInfo);
			this.Controls.Add(this.textLimit);
			this.Controls.Add(this.checkLimit);
			this.Controls.Add(this.checkReportServer);
			this.Controls.Add(this.butSearch);
			this.Controls.Add(this.checkBoxShowMainPages);
			this.Controls.Add(this.checkBoxMatchWholeWord);
			this.Controls.Add(this.butRestore);
			this.Controls.Add(this.checkArchivedOnly);
			this.Controls.Add(this.checkIgnoreContent);
			this.Controls.Add(this.textSearch);
			this.Controls.Add(this.webViewWiki);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butOK);
			this.Name = "FormWikiSearch";
			this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			this.Text = "Wiki Search";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormWikiSearch_FormClosing);
			this.Load += new System.EventHandler(this.FormWikiSearch_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenPmsDental.UI.Button butOK;
		private CodeBase.Controls.ODWebView2 webViewWiki;
		private UI.GridOD gridMain;
		private System.Windows.Forms.TextBox textSearch;
		private OpenPmsDental.UI.CheckBox checkIgnoreContent;
		private OpenPmsDental.UI.CheckBox checkArchivedOnly;
		private UI.Button butRestore;
		private OpenPmsDental.UI.CheckBox checkBoxMatchWholeWord;
		private OpenPmsDental.UI.CheckBox checkBoxShowMainPages;
		private UI.Button butSearch;
		private OpenPmsDental.UI.CheckBox checkReportServer;
		private OpenPmsDental.UI.CheckBox checkLimit;
		private ValidNum textLimit;
		private System.Windows.Forms.Label labelLimitInfo;
	}
}