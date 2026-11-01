namespace OpenDentalImaging{
	partial class FormImagingDevices {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormImagingDevices));
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.textComputer = new System.Windows.Forms.TextBox();
			this.butDown = new OpenPmsDental.UI.Button();
			this.butUp = new OpenPmsDental.UI.Button();
			this.butUnassigned = new OpenPmsDental.UI.Button();
			this.butStandardTemplates = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(32, 35);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(1008, 516);
			this.gridMain.TabIndex = 4;
			this.gridMain.Title = "Imaging Devices";
			this.gridMain.TranslationName = "TableImagingDevices";
			this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			// 
			// butAdd
			// 
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(1067, 35);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(75, 24);
			this.butAdd.TabIndex = 5;
			this.butAdd.Text = "Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(32, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(134, 18);
			this.label2.TabIndex = 10;
			this.label2.Text = "This Computer Name";
			this.label2.TextAlign = System.Drawing.ContentAlignment.BottomRight;
			// 
			// textComputer
			// 
			this.textComputer.Location = new System.Drawing.Point(167, 9);
			this.textComputer.Name = "textComputer";
			this.textComputer.ReadOnly = true;
			this.textComputer.Size = new System.Drawing.Size(182, 20);
			this.textComputer.TabIndex = 9;
			// 
			// butDown
			// 
			this.butDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butDown.Image = global::OpenPmsDental.Properties.Resources.down;
			this.butDown.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDown.Location = new System.Drawing.Point(1067, 188);
			this.butDown.Name = "butDown";
			this.butDown.Size = new System.Drawing.Size(75, 26);
			this.butDown.TabIndex = 38;
			this.butDown.Text = "&Down";
			this.butDown.Click += new System.EventHandler(this.butDown_Click);
			// 
			// butUp
			// 
			this.butUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butUp.Image = global::OpenPmsDental.Properties.Resources.up;
			this.butUp.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butUp.Location = new System.Drawing.Point(1067, 156);
			this.butUp.Name = "butUp";
			this.butUp.Size = new System.Drawing.Size(75, 26);
			this.butUp.TabIndex = 39;
			this.butUp.Text = "&Up";
			this.butUp.Click += new System.EventHandler(this.butUp_Click);
			// 
			// butUnassigned
			// 
			this.butUnassigned.Location = new System.Drawing.Point(372, 6);
			this.butUnassigned.Name = "butUnassigned";
			this.butUnassigned.Size = new System.Drawing.Size(140, 24);
			this.butUnassigned.TabIndex = 40;
			this.butUnassigned.Text = "Unassigned captures";
			this.butUnassigned.Click += new System.EventHandler(this.butUnassigned_Click);
			// 
			// butStandardTemplates
			// 
			this.butStandardTemplates.Location = new System.Drawing.Point(518, 6);
			this.butStandardTemplates.Name = "butStandardTemplates";
			this.butStandardTemplates.Size = new System.Drawing.Size(150, 24);
			this.butStandardTemplates.TabIndex = 41;
			this.butStandardTemplates.Text = "Add standard templates";
			this.butStandardTemplates.Click += new System.EventHandler(this.butStandardTemplates_Click);
			// 
			// FormImagingDevices
			// 
			this.ClientSize = new System.Drawing.Size(1154, 563);
			this.Controls.Add(this.butStandardTemplates);
			this.Controls.Add(this.butUnassigned);
			this.Controls.Add(this.butDown);
			this.Controls.Add(this.butUp);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.textComputer);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.gridMain);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormImagingDevices";
			this.Text = "Imaging Devices";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormImagingDevices_FormClosing);
			this.Load += new System.EventHandler(this.FormImagingDevices_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private OpenPmsDental.UI.GridOD gridMain;
		private OpenPmsDental.UI.Button butAdd;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox textComputer;
		private OpenPmsDental.UI.Button butDown;
		private OpenPmsDental.UI.Button butUp;
		private OpenPmsDental.UI.Button butUnassigned;
		private OpenPmsDental.UI.Button butStandardTemplates;
	}
}