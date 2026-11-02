namespace OpenPmsDental{
	partial class FormWikiIncomingLinks {
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
			this.webViewWiki = new CodeBase.Controls.ODWebView2();
			this.SuspendLayout();
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridMain.Location = new System.Drawing.Point(12, 12);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(248, 621);
			this.gridMain.TabIndex = 6;
			this.gridMain.Title = "Incoming Links";
			this.gridMain.TranslationName = "TableIncomingLinks";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			this.gridMain.Click += new System.EventHandler(this.gridMain_Click);
			// 
			// webViewWiki
			// 
			this.webViewWiki.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.webViewWiki.AreBrowserKeysEnabled = true;
			this.webViewWiki.IsContextMenuEnabled = false;
			this.webViewWiki.Location = new System.Drawing.Point(266, 12);
			this.webViewWiki.Name = "webViewWiki";
			this.webViewWiki.Size = new System.Drawing.Size(634, 621);
			this.webViewWiki.TabIndex = 0;
			// 
			// FormWikiIncomingLinks
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(951, 645);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.webViewWiki);
			this.Name = "FormWikiIncomingLinks";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Incoming Links";
			this.Load += new System.EventHandler(this.FormWiki_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private CodeBase.Controls.ODWebView2 webViewWiki;
		private UI.GridOD gridMain;

	}
}