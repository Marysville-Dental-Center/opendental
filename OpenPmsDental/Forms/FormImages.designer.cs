namespace OpenPmsDental{
	partial class FormImages {
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
			this.components = new System.ComponentModel.Container();
			this.elementHostToolBarMain = new System.Windows.Forms.Integration.ElementHost();
			this.listFiles = new OpenPmsDental.UI.ListBox();
			this.contextMenuStripList = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.elementHostZoomSlider = new System.Windows.Forms.Integration.ElementHost();
			this.panelImage = new OpenPmsDental.UI.ControlDoubleBuffered();
			this.SuspendLayout();
			//
			// elementHostToolBarMain
			//
			this.elementHostToolBarMain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.elementHostToolBarMain.Location = new System.Drawing.Point(0, 0);
			this.elementHostToolBarMain.Name = "elementHostToolBarMain";
			this.elementHostToolBarMain.Size = new System.Drawing.Size(1166, 25);
			this.elementHostToolBarMain.TabIndex = 0;
			this.elementHostToolBarMain.Text = "elementHost1";
			this.elementHostToolBarMain.Child = null;
			//
			// listFiles
			//
			this.listFiles.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
			this.listFiles.ContextMenuStrip = this.contextMenuStripList;
			this.listFiles.Location = new System.Drawing.Point(2, 29);
			this.listFiles.Name = "listFiles";
			this.listFiles.Size = new System.Drawing.Size(226, 747);
			this.listFiles.TabIndex = 1;
			this.listFiles.SelectionChangeCommitted += new System.EventHandler(this.listFiles_SelectionChangeCommitted);
			this.listFiles.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listFiles_MouseDoubleClick);
			this.listFiles.MouseUp += new System.Windows.Forms.MouseEventHandler(this.listFiles_MouseUp);
			this.listFiles.MouseCaptureChanged += new System.EventHandler(this.listFiles_MouseCaptureChanged);
			//
			// contextMenuStripList
			//
			this.contextMenuStripList.Name = "contextMenuStripList";
			this.contextMenuStripList.Size = new System.Drawing.Size(61, 4);
			//
			// elementHostZoomSlider
			//
			this.elementHostZoomSlider.Location = new System.Drawing.Point(233, 26);
			this.elementHostZoomSlider.Name = "elementHostZoomSlider";
			this.elementHostZoomSlider.Size = new System.Drawing.Size(231, 25);
			this.elementHostZoomSlider.TabIndex = 2;
			this.elementHostZoomSlider.Text = "elementHost2";
			this.elementHostZoomSlider.Child = null;
			//
			// panelImage
			//
			this.panelImage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.panelImage.Location = new System.Drawing.Point(233, 53);
			this.panelImage.Name = "panelImage";
			this.panelImage.Size = new System.Drawing.Size(931, 723);
			this.panelImage.TabIndex = 3;
			this.panelImage.SizeChanged += new System.EventHandler(this.panelImage_SizeChanged);
			this.panelImage.Paint += new System.Windows.Forms.PaintEventHandler(this.panelImage_Paint);
			this.panelImage.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelImage_MouseDown);
			this.panelImage.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelImage_MouseMove);
			this.panelImage.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelImage_MouseUp);
			//
			// FormImages
			//
			this.ClientSize = new System.Drawing.Size(1166, 778);
			this.Controls.Add(this.panelImage);
			this.Controls.Add(this.elementHostZoomSlider);
			this.Controls.Add(this.listFiles);
			this.Controls.Add(this.elementHostToolBarMain);
			this.Name = "FormImages";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Images";
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormImages_FormClosed);
			this.Load += new System.EventHandler(this.FormImages_Load);
			this.Shown += new System.EventHandler(this.FormImages_Shown);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Integration.ElementHost elementHostToolBarMain;
		private UI.ListBox listFiles;
		private System.Windows.Forms.ContextMenuStrip contextMenuStripList;
		private System.Windows.Forms.Integration.ElementHost elementHostZoomSlider;
		private UI.ControlDoubleBuffered panelImage;
	}
}
