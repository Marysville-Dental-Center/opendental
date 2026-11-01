namespace OpenDental {
	partial class FormWaitingRoom {
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
			this.gridMain = new OpenDental.UI.GridOD();
			this.timerTick = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(8, 8);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(264, 305);
			this.gridMain.TabIndex = 0;
			this.gridMain.Title = "Waiting Room";
			this.gridMain.TranslationName = "TableApptWaiting";
			this.gridMain.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			//
			// timerTick
			//
			this.timerTick.Interval = 1000;
			this.timerTick.Tick += new System.EventHandler(this.timerTick_Tick);
			//
			// FormWaitingRoom
			//
			this.ClientSize = new System.Drawing.Size(280, 321);
			this.Controls.Add(this.gridMain);
			this.Name = "FormWaitingRoom";
			this.ShowInTaskbar = true;
			this.Text = "Waiting Room";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormWaitingRoom_FormClosing);
			this.Load += new System.EventHandler(this.FormWaitingRoom_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private UI.GridOD gridMain;
		private System.Windows.Forms.Timer timerTick;
	}
}
