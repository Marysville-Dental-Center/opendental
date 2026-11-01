namespace OpenDental {
	partial class FormTreatPlanAddProcs {
		/// <summary>Required designer variable.</summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>Clean up any resources being used.</summary>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		private void InitializeComponent() {
			this.labelPlan = new System.Windows.Forms.Label();
			this.gridQuick = new OpenDental.UI.GridOD();
			this.labelCode = new System.Windows.Forms.Label();
			this.textCode = new System.Windows.Forms.TextBox();
			this.butAddCode = new OpenDental.UI.Button();
			this.butSearch = new OpenDental.UI.Button();
			this.gridMain = new OpenDental.UI.GridOD();
			this.butRemove = new OpenDental.UI.Button();
			this.labelPriority = new System.Windows.Forms.Label();
			this.comboPriority = new OpenDental.UI.ComboBox();
			this.labelHelp = new System.Windows.Forms.Label();
			this.butOK = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelPlan
			//
			this.labelPlan.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelPlan.Location = new System.Drawing.Point(12, 9);
			this.labelPlan.Name = "labelPlan";
			this.labelPlan.Size = new System.Drawing.Size(876, 18);
			this.labelPlan.TabIndex = 0;
			this.labelPlan.Text = "labelPlan";
			this.labelPlan.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridQuick
			//
			this.gridQuick.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridQuick.Location = new System.Drawing.Point(12, 32);
			this.gridQuick.Name = "gridQuick";
			this.gridQuick.Size = new System.Drawing.Size(200, 330);
			this.gridQuick.TabIndex = 1;
			this.gridQuick.Title = "Quick Buttons";
			this.gridQuick.TranslationName = "TableTreatPlanAddProcs";
			this.gridQuick.CellClick += new OpenDental.UI.ODGridClickEventHandler(this.gridQuick_CellClick);
			//
			// labelCode
			//
			this.labelCode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelCode.Location = new System.Drawing.Point(12, 370);
			this.labelCode.Name = "labelCode";
			this.labelCode.Size = new System.Drawing.Size(40, 20);
			this.labelCode.TabIndex = 2;
			this.labelCode.Text = "Code";
			this.labelCode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// textCode
			//
			this.textCode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.textCode.Location = new System.Drawing.Point(54, 370);
			this.textCode.Name = "textCode";
			this.textCode.Size = new System.Drawing.Size(88, 20);
			this.textCode.TabIndex = 3;
			this.textCode.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textCode_KeyDown);
			//
			// butAddCode
			//
			this.butAddCode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAddCode.Location = new System.Drawing.Point(148, 368);
			this.butAddCode.Name = "butAddCode";
			this.butAddCode.Size = new System.Drawing.Size(64, 24);
			this.butAddCode.TabIndex = 4;
			this.butAddCode.Text = "Add";
			this.butAddCode.Click += new System.EventHandler(this.butAddCode_Click);
			//
			// butSearch
			//
			this.butSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butSearch.Location = new System.Drawing.Point(12, 400);
			this.butSearch.Name = "butSearch";
			this.butSearch.Size = new System.Drawing.Size(200, 24);
			this.butSearch.TabIndex = 5;
			this.butSearch.Text = "Search Codes...";
			this.butSearch.Click += new System.EventHandler(this.butSearch_Click);
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.EditableEnterMovesDown = true;
			this.gridMain.Location = new System.Drawing.Point(222, 32);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenDental.UI.GridSelectionMode.OneCell;
			this.gridMain.Size = new System.Drawing.Size(666, 392);
			this.gridMain.TabIndex = 6;
			this.gridMain.Title = "Procedures to Add";
			this.gridMain.TranslationName = "TableTreatPlanAddProcs";
			this.gridMain.CellLeave += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellLeave);
			//
			// butRemove
			//
			this.butRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRemove.Location = new System.Drawing.Point(222, 432);
			this.butRemove.Name = "butRemove";
			this.butRemove.Size = new System.Drawing.Size(75, 24);
			this.butRemove.TabIndex = 7;
			this.butRemove.Text = "Remove";
			this.butRemove.Click += new System.EventHandler(this.butRemove_Click);
			//
			// labelPriority
			//
			this.labelPriority.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelPriority.Location = new System.Drawing.Point(310, 434);
			this.labelPriority.Name = "labelPriority";
			this.labelPriority.Size = new System.Drawing.Size(60, 20);
			this.labelPriority.TabIndex = 8;
			this.labelPriority.Text = "Priority";
			this.labelPriority.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboPriority
			//
			this.comboPriority.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.comboPriority.Location = new System.Drawing.Point(374, 434);
			this.comboPriority.Name = "comboPriority";
			this.comboPriority.Size = new System.Drawing.Size(150, 21);
			this.comboPriority.TabIndex = 9;
			//
			// labelHelp
			//
			this.labelHelp.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelHelp.Location = new System.Drawing.Point(12, 466);
			this.labelHelp.Name = "labelHelp";
			this.labelHelp.Size = new System.Drawing.Size(760, 48);
			this.labelHelp.TabIndex = 10;
			this.labelHelp.Text = "Tooth: one tooth, a list (3,5) or a range (3-5). Surf/Area: surfaces (MOD), a quadrant (UR, UL, LR, LL), an arch (U, L) or a sextant (1-6). A tooth or surface code typed for several teeth adds one procedure per tooth. The procedures are added as treatment planned with the fees and estimates of Chart entry.";
			//
			// butOK
			//
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(788, 520);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(100, 24);
			this.butOK.TabIndex = 11;
			this.butOK.Text = "&Add to Plan";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			//
			// FormTreatPlanAddProcs
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(900, 556);
			this.Controls.Add(this.labelPlan);
			this.Controls.Add(this.gridQuick);
			this.Controls.Add(this.labelCode);
			this.Controls.Add(this.textCode);
			this.Controls.Add(this.butAddCode);
			this.Controls.Add(this.butSearch);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butRemove);
			this.Controls.Add(this.labelPriority);
			this.Controls.Add(this.comboPriority);
			this.Controls.Add(this.labelHelp);
			this.Controls.Add(this.butOK);
			this.MinimizeBox = false;
			this.Name = "FormTreatPlanAddProcs";
			this.Text = "Add Procedures to Active Treatment Plan";
			this.Load += new System.EventHandler(this.FormTreatPlanAddProcs_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelPlan;
		private OpenDental.UI.GridOD gridQuick;
		private System.Windows.Forms.Label labelCode;
		private System.Windows.Forms.TextBox textCode;
		private OpenDental.UI.Button butAddCode;
		private OpenDental.UI.Button butSearch;
		private OpenDental.UI.GridOD gridMain;
		private OpenDental.UI.Button butRemove;
		private System.Windows.Forms.Label labelPriority;
		private OpenDental.UI.ComboBox comboPriority;
		private System.Windows.Forms.Label labelHelp;
		private OpenDental.UI.Button butOK;
	}
}
