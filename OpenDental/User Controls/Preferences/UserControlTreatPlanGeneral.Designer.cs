
namespace OpenDental {
	partial class UserControlTreatPlanGeneral {
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.label1 = new System.Windows.Forms.Label();
			this.groupTreatPlan = new OpenDental.UI.GroupBox();
			this.checkTreatPlanShowCompleted = new OpenDental.UI.CheckBox();
			this.checkTreatPlanItemized = new OpenDental.UI.CheckBox();
			this.checkTPSaveSigned = new OpenDental.UI.CheckBox();
			this.checkPromptSaveTP = new OpenDental.UI.CheckBox();
			this.groupBoxDiscounts = new OpenDental.UI.GroupBox();
			this.comboProcDiscountType = new OpenDental.UI.ComboBox();
			this.label19 = new System.Windows.Forms.Label();
			this.textDiscountPercentage = new System.Windows.Forms.TextBox();
			this.labelDiscountPercentage = new System.Windows.Forms.Label();
			this.groupBoxInsuranceHistory = new OpenDental.UI.GroupBox();
			this.labelInsHistCodeGroups = new System.Windows.Forms.Label();
			this.groupTreatPlanSort = new OpenDental.UI.GroupBox();
			this.radioTreatPlanSortTooth = new System.Windows.Forms.RadioButton();
			this.radioTreatPlanSortOrder = new System.Windows.Forms.RadioButton();
			this.textTreatNote = new OpenDental.ODtextBox();
			this.groupTreatPlan.SuspendLayout();
			this.groupBoxDiscounts.SuspendLayout();
			this.groupBoxInsuranceHistory.SuspendLayout();
			this.groupTreatPlanSort.SuspendLayout();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(38, 20);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(48, 52);
			this.label1.TabIndex = 242;
			this.label1.Text = "Default Note";
			this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// groupTreatPlan
			// 
			this.groupTreatPlan.Controls.Add(this.checkTreatPlanShowCompleted);
			this.groupTreatPlan.Controls.Add(this.checkTreatPlanItemized);
			this.groupTreatPlan.Controls.Add(this.checkTPSaveSigned);
			this.groupTreatPlan.Controls.Add(this.checkPromptSaveTP);
			this.groupTreatPlan.Location = new System.Drawing.Point(20, 80);
			this.groupTreatPlan.Name = "groupTreatPlan";
			this.groupTreatPlan.Size = new System.Drawing.Size(450, 94);
			this.groupTreatPlan.TabIndex = 367;
			this.groupTreatPlan.Text = "Treatment Plan";
			// 
			// checkTreatPlanShowCompleted
			// 
			this.checkTreatPlanShowCompleted.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkTreatPlanShowCompleted.Location = new System.Drawing.Point(90, 10);
			this.checkTreatPlanShowCompleted.Name = "checkTreatPlanShowCompleted";
			this.checkTreatPlanShowCompleted.Size = new System.Drawing.Size(350, 17);
			this.checkTreatPlanShowCompleted.TabIndex = 243;
			this.checkTreatPlanShowCompleted.Text = "Show completed work on graphical tooth chart";
			// 
			// checkTreatPlanItemized
			// 
			this.checkTreatPlanItemized.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkTreatPlanItemized.Location = new System.Drawing.Point(152, 29);
			this.checkTreatPlanItemized.Name = "checkTreatPlanItemized";
			this.checkTreatPlanItemized.Size = new System.Drawing.Size(288, 17);
			this.checkTreatPlanItemized.TabIndex = 246;
			this.checkTreatPlanItemized.Text = "Show itemized fees";
			// 
			// checkTPSaveSigned
			// 
			this.checkTPSaveSigned.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkTPSaveSigned.Location = new System.Drawing.Point(138, 48);
			this.checkTPSaveSigned.Name = "checkTPSaveSigned";
			this.checkTPSaveSigned.Size = new System.Drawing.Size(302, 17);
			this.checkTPSaveSigned.TabIndex = 247;
			this.checkTPSaveSigned.Text = "Save signed Treatment Plans to PDF";
			// 
			// checkPromptSaveTP
			// 
			this.checkPromptSaveTP.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkPromptSaveTP.Location = new System.Drawing.Point(62, 67);
			this.checkPromptSaveTP.Name = "checkPromptSaveTP";
			this.checkPromptSaveTP.Size = new System.Drawing.Size(378, 17);
			this.checkPromptSaveTP.TabIndex = 252;
			this.checkPromptSaveTP.Text = "When saving Treatment Plans, prompt for name change";
			// 
			// groupBoxDiscounts
			// 
			this.groupBoxDiscounts.Controls.Add(this.comboProcDiscountType);
			this.groupBoxDiscounts.Controls.Add(this.textDiscountPercentage);
			this.groupBoxDiscounts.Controls.Add(this.label19);
			this.groupBoxDiscounts.Controls.Add(this.labelDiscountPercentage);
			this.groupBoxDiscounts.Location = new System.Drawing.Point(20, 239);
			this.groupBoxDiscounts.Name = "groupBoxDiscounts";
			this.groupBoxDiscounts.Size = new System.Drawing.Size(450, 65);
			this.groupBoxDiscounts.TabIndex = 274;
			this.groupBoxDiscounts.Text = "Discounts";
			// 
			// comboProcDiscountType
			// 
			this.comboProcDiscountType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.comboProcDiscountType.Location = new System.Drawing.Point(277, 10);
			this.comboProcDiscountType.Name = "comboProcDiscountType";
			this.comboProcDiscountType.Size = new System.Drawing.Size(163, 21);
			this.comboProcDiscountType.TabIndex = 244;
			// 
			// label19
			// 
			this.label19.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label19.Location = new System.Drawing.Point(76, 13);
			this.label19.Margin = new System.Windows.Forms.Padding(0);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(200, 17);
			this.label19.TabIndex = 250;
			this.label19.Text = "Procedure discount adj type";
			this.label19.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textDiscountPercentage
			// 
			this.textDiscountPercentage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.textDiscountPercentage.Location = new System.Drawing.Point(387, 35);
			this.textDiscountPercentage.Name = "textDiscountPercentage";
			this.textDiscountPercentage.Size = new System.Drawing.Size(53, 20);
			this.textDiscountPercentage.TabIndex = 245;
			// 
			// labelDiscountPercentage
			// 
			this.labelDiscountPercentage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelDiscountPercentage.Location = new System.Drawing.Point(140, 38);
			this.labelDiscountPercentage.Margin = new System.Windows.Forms.Padding(0);
			this.labelDiscountPercentage.Name = "labelDiscountPercentage";
			this.labelDiscountPercentage.Size = new System.Drawing.Size(246, 17);
			this.labelDiscountPercentage.TabIndex = 251;
			this.labelDiscountPercentage.Text = "Default Procedure discount percentage";
			this.labelDiscountPercentage.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// groupBoxInsuranceHistory
			// 
			this.groupBoxInsuranceHistory.Controls.Add(this.labelInsHistCodeGroups);
			this.groupBoxInsuranceHistory.Location = new System.Drawing.Point(20, 309);
			this.groupBoxInsuranceHistory.Name = "groupBoxInsuranceHistory";
			this.groupBoxInsuranceHistory.Size = new System.Drawing.Size(450, 56);
			this.groupBoxInsuranceHistory.TabIndex = 273;
			this.groupBoxInsuranceHistory.Text = "Insurance History";
			// 
			// labelInsHistCodeGroups
			// 
			this.labelInsHistCodeGroups.Location = new System.Drawing.Point(10, 13);
			this.labelInsHistCodeGroups.Name = "labelInsHistCodeGroups";
			this.labelInsHistCodeGroups.Size = new System.Drawing.Size(430, 34);
			this.labelInsHistCodeGroups.TabIndex = 253;
			this.labelInsHistCodeGroups.Text = "Insurance History rows and their procedure codes are set in Setup, Code Groups: check Show In History on a code group and give it a History Proc Code.";
			// 
			// groupTreatPlanSort
			// 
			this.groupTreatPlanSort.BackColor = System.Drawing.Color.White;
			this.groupTreatPlanSort.Controls.Add(this.radioTreatPlanSortTooth);
			this.groupTreatPlanSort.Controls.Add(this.radioTreatPlanSortOrder);
			this.groupTreatPlanSort.Location = new System.Drawing.Point(20, 179);
			this.groupTreatPlanSort.Name = "groupTreatPlanSort";
			this.groupTreatPlanSort.Size = new System.Drawing.Size(450, 55);
			this.groupTreatPlanSort.TabIndex = 249;
			this.groupTreatPlanSort.Text = "Sort procedures by";
			// 
			// radioTreatPlanSortTooth
			// 
			this.radioTreatPlanSortTooth.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.radioTreatPlanSortTooth.Location = new System.Drawing.Point(283, 9);
			this.radioTreatPlanSortTooth.Name = "radioTreatPlanSortTooth";
			this.radioTreatPlanSortTooth.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
			this.radioTreatPlanSortTooth.Size = new System.Drawing.Size(157, 18);
			this.radioTreatPlanSortTooth.TabIndex = 54;
			this.radioTreatPlanSortTooth.Text = "Tooth";
			this.radioTreatPlanSortTooth.UseVisualStyleBackColor = true;
			this.radioTreatPlanSortTooth.Click += new System.EventHandler(this.radioTreatPlanSortTooth_Click);
			// 
			// radioTreatPlanSortOrder
			// 
			this.radioTreatPlanSortOrder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.radioTreatPlanSortOrder.Checked = true;
			this.radioTreatPlanSortOrder.Location = new System.Drawing.Point(283, 28);
			this.radioTreatPlanSortOrder.Name = "radioTreatPlanSortOrder";
			this.radioTreatPlanSortOrder.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
			this.radioTreatPlanSortOrder.Size = new System.Drawing.Size(157, 18);
			this.radioTreatPlanSortOrder.TabIndex = 53;
			this.radioTreatPlanSortOrder.TabStop = true;
			this.radioTreatPlanSortOrder.Text = "Order Entered";
			this.radioTreatPlanSortOrder.UseVisualStyleBackColor = true;
			this.radioTreatPlanSortOrder.Click += new System.EventHandler(this.radioTreatPlanSortOrder_Click);
			// 
			// textTreatNote
			// 
			this.textTreatNote.AcceptsTab = true;
			this.textTreatNote.BackColor = System.Drawing.SystemColors.Window;
			this.textTreatNote.DetectLinksEnabled = false;
			this.textTreatNote.DetectUrls = false;
			this.textTreatNote.Location = new System.Drawing.Point(97, 10);
			this.textTreatNote.MaxLength = 32767;
			this.textTreatNote.Name = "textTreatNote";
			this.textTreatNote.QuickPasteType = OpenDentBusiness.EnumQuickPasteType.TreatPlan;
			this.textTreatNote.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textTreatNote.Size = new System.Drawing.Size(363, 66);
			this.textTreatNote.TabIndex = 248;
			this.textTreatNote.Text = "";
			// 
			// UserControlTreatPlanGeneral
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			this.Controls.Add(this.groupTreatPlan);
			this.Controls.Add(this.groupBoxDiscounts);
			this.Controls.Add(this.groupBoxInsuranceHistory);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.groupTreatPlanSort);
			this.Controls.Add(this.textTreatNote);
			this.Name = "UserControlTreatPlanGeneral";
			this.Size = new System.Drawing.Size(494, 660);
			this.Load += new System.EventHandler(this.UserControlTreatPlanGeneral_Load);
			this.groupTreatPlan.ResumeLayout(false);
			this.groupBoxDiscounts.ResumeLayout(false);
			this.groupBoxDiscounts.PerformLayout();
			this.groupBoxInsuranceHistory.ResumeLayout(false);
			this.groupBoxInsuranceHistory.PerformLayout();
			this.groupTreatPlanSort.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenDental.UI.CheckBox checkPromptSaveTP;
		private System.Windows.Forms.Label labelDiscountPercentage;
		private System.Windows.Forms.Label label19;
		private System.Windows.Forms.Label label1;
		private UI.GroupBox groupTreatPlanSort;
		private System.Windows.Forms.RadioButton radioTreatPlanSortTooth;
		private System.Windows.Forms.RadioButton radioTreatPlanSortOrder;
		private ODtextBox textTreatNote;
		private OpenDental.UI.CheckBox checkTPSaveSigned;
		private UI.ComboBox comboProcDiscountType;
		private OpenDental.UI.CheckBox checkTreatPlanShowCompleted;
		private System.Windows.Forms.TextBox textDiscountPercentage;
		private OpenDental.UI.CheckBox checkTreatPlanItemized;
		private UI.GroupBox groupBoxInsuranceHistory;
		private System.Windows.Forms.Label labelInsHistCodeGroups;
		private UI.GroupBox groupBoxDiscounts;
		private UI.GroupBox groupTreatPlan;
	}
}
