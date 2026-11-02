namespace OpenPmsDental {
	partial class FormEhrEduBrowser {
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
			this.webViewMain = new CodeBase.Controls.ODWebView2();
			this.butPrint = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// webViewMain
			// 
			this.webViewMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.webViewMain.Location = new System.Drawing.Point(1, 1);
			this.webViewMain.Name = "webViewMain";
			this.webViewMain.Size = new System.Drawing.Size(882, 603);
			this.webViewMain.TabIndex = 1;
			// 
			// butPrint
			// 
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPrint.Location = new System.Drawing.Point(718, 610);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(75, 23);
			this.butPrint.TabIndex = 2;
			this.butPrint.Text = "Print";
			this.butPrint.UseVisualStyleBackColor = true;
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			// 
			// FormEhrEduBrowser
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(884, 639);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.webViewMain);
			this.Name = "FormEhrEduBrowser";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Educational Resources";
			this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
			this.Load += new System.EventHandler(this.FormEduBrowser_Load);
			this.ResumeLayout(false);

		}

		#endregion
		private CodeBase.Controls.ODWebView2 webViewMain;
		private System.Windows.Forms.Button butPrint;
	}
}