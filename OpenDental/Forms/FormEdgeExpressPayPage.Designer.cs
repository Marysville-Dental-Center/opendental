namespace OpenDental {
	partial class FormEdgeExpressPayPage {
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
			this.components = new System.ComponentModel.Container();
			this.webViewMain = new CodeBase.Controls.ODWebView2();
			this.labelBrowserWait = new System.Windows.Forms.Label();
			this.butFinished = new OpenDental.UI.Button();
			this.butClose = new OpenDental.UI.Button();
			this.timerLifetime = new System.Windows.Forms.Timer(this.components);
			((System.ComponentModel.ISupportInitialize)(this.webViewMain)).BeginInit();
			this.SuspendLayout();
			//
			// webViewMain
			//
			this.webViewMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.webViewMain.CreationProperties = null;
			this.webViewMain.DefaultBackgroundColor = System.Drawing.Color.White;
			this.webViewMain.Location = new System.Drawing.Point(12, 12);
			this.webViewMain.Name = "webViewMain";
			this.webViewMain.Size = new System.Drawing.Size(660, 560);
			this.webViewMain.TabIndex = 0;
			this.webViewMain.ZoomFactor = 1D;
			//
			// labelBrowserWait
			//
			this.labelBrowserWait.Location = new System.Drawing.Point(12, 12);
			this.labelBrowserWait.Name = "labelBrowserWait";
			this.labelBrowserWait.Size = new System.Drawing.Size(420, 60);
			this.labelBrowserWait.TabIndex = 1;
			this.labelBrowserWait.Text = "Enter the card in your browser. This window closes when the card processor finishes.";
			this.labelBrowserWait.Visible = false;
			//
			// butFinished
			//
			this.butFinished.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butFinished.Location = new System.Drawing.Point(506, 582);
			this.butFinished.Name = "butFinished";
			this.butFinished.Size = new System.Drawing.Size(85, 24);
			this.butFinished.TabIndex = 2;
			this.butFinished.Text = "I\'m finished";
			this.butFinished.Visible = false;
			this.butFinished.Click += new System.EventHandler(this.butFinished_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(597, 582);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 3;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// timerLifetime
			//
			this.timerLifetime.Tick += new System.EventHandler(this.timerLifetime_Tick);
			//
			// FormEdgeExpressPayPage
			//
			this.ClientSize = new System.Drawing.Size(684, 618);
			this.Controls.Add(this.butClose);
			this.Controls.Add(this.butFinished);
			this.Controls.Add(this.labelBrowserWait);
			this.Controls.Add(this.webViewMain);
			this.Name = "FormEdgeExpressPayPage";
			this.Text = "Card Entry";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormEdgeExpressPayPage_FormClosing);
			this.Load += new System.EventHandler(this.FormEdgeExpressPayPage_Load);
			((System.ComponentModel.ISupportInitialize)(this.webViewMain)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private CodeBase.Controls.ODWebView2 webViewMain;
		private System.Windows.Forms.Label labelBrowserWait;
		private OpenDental.UI.Button butFinished;
		private OpenDental.UI.Button butClose;
		private System.Windows.Forms.Timer timerLifetime;
	}
}
