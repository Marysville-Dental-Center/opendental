namespace OpenPmsDental{
	partial class FormVideo {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing) {
				ReleaseCamera();
			}
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
			this.pictureBoxCamera = new System.Windows.Forms.PictureBox();
			this.butCapture = new OpenPmsDental.UI.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.comboCameras = new OpenPmsDental.UI.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.butRefresh = new OpenPmsDental.UI.Button();
			this.butSettings = new OpenPmsDental.UI.Button();
			this.labelStatus = new System.Windows.Forms.Label();
			this.labelInfo = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.pictureBoxCamera)).BeginInit();
			this.SuspendLayout();
			//
			// pictureBoxCamera
			//
			this.pictureBoxCamera.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pictureBoxCamera.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pictureBoxCamera.Location = new System.Drawing.Point(3, 29);
			this.pictureBoxCamera.Name = "pictureBoxCamera";
			this.pictureBoxCamera.Size = new System.Drawing.Size(514, 301);
			this.pictureBoxCamera.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBoxCamera.TabIndex = 6;
			this.pictureBoxCamera.TabStop = false;
			//
			// butCapture
			//
			this.butCapture.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butCapture.Location = new System.Drawing.Point(3, 333);
			this.butCapture.Name = "butCapture";
			this.butCapture.Size = new System.Drawing.Size(75, 24);
			this.butCapture.TabIndex = 1;
			this.butCapture.TabStop = false;
			this.butCapture.Text = "Capture";
			this.butCapture.Click += new System.EventHandler(this.butCapture_Click);
			//
			// label1
			//
			this.label1.Location = new System.Drawing.Point(4, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(59, 18);
			this.label1.TabIndex = 9;
			this.label1.Text = "Camera";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboCameras
			//
			this.comboCameras.Location = new System.Drawing.Point(64, 4);
			this.comboCameras.Name = "comboCameras";
			this.comboCameras.Size = new System.Drawing.Size(260, 21);
			this.comboCameras.TabIndex = 2;
			this.comboCameras.SelectionChangeCommitted += new System.EventHandler(this.comboCameras_SelectionChangeCommitted);
			//
			// label2
			//
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label2.Location = new System.Drawing.Point(82, 336);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(80, 18);
			this.label2.TabIndex = 10;
			this.label2.Text = "(or Space)";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butRefresh
			//
			this.butRefresh.Location = new System.Drawing.Point(330, 3);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 24);
			this.butRefresh.TabIndex = 3;
			this.butRefresh.TabStop = false;
			this.butRefresh.Text = "Refresh";
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			//
			// butSettings
			//
			this.butSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butSettings.Location = new System.Drawing.Point(442, 3);
			this.butSettings.Name = "butSettings";
			this.butSettings.Size = new System.Drawing.Size(75, 24);
			this.butSettings.TabIndex = 4;
			this.butSettings.TabStop = false;
			this.butSettings.Text = "Settings";
			this.butSettings.Click += new System.EventHandler(this.butSettings_Click);
			//
			// labelStatus
			//
			this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelStatus.BackColor = System.Drawing.SystemColors.Info;
			this.labelStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.labelStatus.ForeColor = System.Drawing.SystemColors.InfoText;
			this.labelStatus.Location = new System.Drawing.Point(3, 29);
			this.labelStatus.Name = "labelStatus";
			this.labelStatus.Size = new System.Drawing.Size(514, 44);
			this.labelStatus.TabIndex = 11;
			this.labelStatus.Text = "Starting the camera...";
			this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.labelStatus.Visible = false;
			//
			// labelInfo
			//
			this.labelInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelInfo.AutoEllipsis = true;
			this.labelInfo.Location = new System.Drawing.Point(166, 336);
			this.labelInfo.Name = "labelInfo";
			this.labelInfo.Size = new System.Drawing.Size(351, 18);
			this.labelInfo.TabIndex = 12;
			this.labelInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// FormVideo
			//
			this.ClientSize = new System.Drawing.Size(520, 360);
			this.Controls.Add(this.labelInfo);
			this.Controls.Add(this.labelStatus);
			this.Controls.Add(this.butSettings);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.comboCameras);
			this.Controls.Add(this.butCapture);
			this.Controls.Add(this.pictureBoxCamera);
			this.Name = "FormVideo";
			this.Text = "Video Capture";
			this.TopMost = true;
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormVideo_FormClosing);
			this.Load += new System.EventHandler(this.FormVideo_Load);
			this.Shown += new System.EventHandler(this.FormVideo_Shown);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormVideo_KeyDown);
			((System.ComponentModel.ISupportInitialize)(this.pictureBoxCamera)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.PictureBox pictureBoxCamera;
		private UI.Button butCapture;
		private System.Windows.Forms.Label label1;
		private OpenPmsDental.UI.ComboBox comboCameras;
		private System.Windows.Forms.Label label2;
		private UI.Button butRefresh;
		private UI.Button butSettings;
		private System.Windows.Forms.Label labelStatus;
		private System.Windows.Forms.Label labelInfo;
	}
}