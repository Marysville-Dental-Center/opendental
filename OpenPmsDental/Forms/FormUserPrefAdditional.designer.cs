namespace OpenPmsDental {
	partial class FormUserPrefAdditional {
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
			this.butOK = new OpenPmsDental.UI.Button();
			this.gridUserProperties = new OpenPmsDental.UI.GridOD();
			this.SuspendLayout();
			// 
			// butOK
			// 
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(186, 422);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 3;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			// 
			// gridUserProperties
			// 
			this.gridUserProperties.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridUserProperties.Location = new System.Drawing.Point(12, 12);
			this.gridUserProperties.Name = "gridUserProperties";
			this.gridUserProperties.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;
			this.gridUserProperties.Size = new System.Drawing.Size(249, 397);
			this.gridUserProperties.TabIndex = 4;
			this.gridUserProperties.Title = "Additional User Properties";
			this.gridUserProperties.TranslationName = "TableUserPrefProperties";
			this.gridUserProperties.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridProvProperties_CellLeave);
			// 
			// FormUserPrefAdditional
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(273, 458);
			this.Controls.Add(this.gridUserProperties);
			this.Controls.Add(this.butOK);
			this.Name = "FormUserPrefAdditional";
			this.Text = "Additional User Properties";
			this.Load += new System.EventHandler(this.FormProvAdditional_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenPmsDental.UI.Button butOK;
		private UI.GridOD gridUserProperties;
	}
}