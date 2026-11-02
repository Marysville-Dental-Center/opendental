namespace OpenPmsDental{
	partial class FormPayConnect2iFrame {
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
			((System.ComponentModel.ISupportInitialize)(this.webViewMain)).BeginInit();
			this.SuspendLayout();
			//
			// webViewMain
			// 
			this.webViewMain.CreationProperties = null;
			this.webViewMain.DefaultBackgroundColor = System.Drawing.Color.White;
			this.webViewMain.Location = new System.Drawing.Point(12, 13);
			this.webViewMain.Name = "webViewMain";
			this.webViewMain.Size = new System.Drawing.Size(500, 400);
			this.webViewMain.TabIndex = 7;
			this.webViewMain.ZoomFactor = 1D;
			// 
			// FormPayConnect2iFrame
			// 
			this.ClientSize = new System.Drawing.Size(524, 432);
			this.Controls.Add(this.webViewMain);
			this.Name = "FormPayConnect2iFrame";
			this.Text = "PayConnect";
			this.Load += new System.EventHandler(this.FormPayConnect2iFrame_Load);
			((System.ComponentModel.ISupportInitialize)(this.webViewMain)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion
		private CodeBase.Controls.ODWebView2 webViewMain;
	}
}