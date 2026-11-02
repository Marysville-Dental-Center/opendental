using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormBenefitEdit {
		private System.ComponentModel.IContainer components = null;// Required designer variable.

		///<summary></summary>
		protected override void Dispose( bool disposing ){
			if( disposing ){
				if(components != null){
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		private void InitializeComponent(){
			this.labelAmount = new System.Windows.Forms.Label();
			this.listCategory = new OpenPmsDental.UI.ComboBox();
			this.checkPat = new OpenPmsDental.UI.CheckBox();
			this.textProcCode = new System.Windows.Forms.TextBox();
			this.listBenefitType = new OpenPmsDental.UI.ComboBox();
			this.labelType = new System.Windows.Forms.Label();
			this.labelPercent = new System.Windows.Forms.Label();
			this.listTimePeriod = new OpenPmsDental.UI.ComboBox();
			this.label4 = new System.Windows.Forms.Label();
			this.listQuantityQualifier = new OpenPmsDental.UI.ComboBox();
			this.label8 = new System.Windows.Forms.Label();
			this.groupQuantity = new OpenPmsDental.UI.GroupBox();
			this.textQuantity = new OpenPmsDental.ValidNum();
			this.listCoverageLevel = new OpenPmsDental.UI.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textAmount = new OpenPmsDental.ValidDouble();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.textPercent = new OpenPmsDental.ValidNum();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.radioCategory = new System.Windows.Forms.RadioButton();
			this.radioProcCode = new System.Windows.Forms.RadioButton();
			this.radioCodeGroup = new System.Windows.Forms.RadioButton();
			this.listTreatArea = new OpenPmsDental.UI.ComboBox();
			this.labelTreatmentArea = new System.Windows.Forms.Label();
			this.comboCodeGroup = new OpenPmsDental.UI.ComboBox();
			this.labelTeeth = new System.Windows.Forms.Label();
			this.textTeeth = new System.Windows.Forms.TextBox();
			this.labelTeethHint = new System.Windows.Forms.Label();
			this.labelPreview = new System.Windows.Forms.Label();
			this.groupQuantity.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			//
			// labelAmount
			//
			this.labelAmount.Location = new System.Drawing.Point(12, 184);
			this.labelAmount.Name = "labelAmount";
			this.labelAmount.Size = new System.Drawing.Size(110, 16);
			this.labelAmount.TabIndex = 4;
			this.labelAmount.Text = "Amount";
			this.labelAmount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// listCategory
			//
			this.listCategory.Location = new System.Drawing.Point(125, 19);
			this.listCategory.Name = "listCategory";
			this.listCategory.Size = new System.Drawing.Size(200, 21);
			this.listCategory.TabIndex = 1;
			//
			// checkPat
			//
			this.checkPat.Location = new System.Drawing.Point(12, 8);
			this.checkPat.Name = "checkPat";
			this.checkPat.Size = new System.Drawing.Size(536, 20);
			this.checkPat.TabIndex = 0;
			this.checkPat.Text = "Patient Override (Rare. Usually if percentages are different for family members)";
			//
			// textProcCode
			//
			this.textProcCode.Location = new System.Drawing.Point(125, 42);
			this.textProcCode.Name = "textProcCode";
			this.textProcCode.Size = new System.Drawing.Size(100, 20);
			this.textProcCode.TabIndex = 3;
			//
			// listBenefitType
			//
			this.listBenefitType.Location = new System.Drawing.Point(125, 134);
			this.listBenefitType.Name = "listBenefitType";
			this.listBenefitType.Size = new System.Drawing.Size(160, 21);
			this.listBenefitType.TabIndex = 6;
			this.listBenefitType.SelectedIndexChanged += new System.EventHandler(this.listBenefitType_SelectedIndexChanged);
			//
			// labelType
			//
			this.labelType.Location = new System.Drawing.Point(12, 136);
			this.labelType.Name = "labelType";
			this.labelType.Size = new System.Drawing.Size(110, 16);
			this.labelType.TabIndex = 26;
			this.labelType.Text = "Type";
			this.labelType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// labelPercent
			//
			this.labelPercent.Location = new System.Drawing.Point(12, 160);
			this.labelPercent.Name = "labelPercent";
			this.labelPercent.Size = new System.Drawing.Size(110, 16);
			this.labelPercent.TabIndex = 27;
			this.labelPercent.Text = "Percent";
			this.labelPercent.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// listTimePeriod
			//
			this.listTimePeriod.Location = new System.Drawing.Point(125, 206);
			this.listTimePeriod.Name = "listTimePeriod";
			this.listTimePeriod.Size = new System.Drawing.Size(160, 21);
			this.listTimePeriod.TabIndex = 9;
			//
			// label4
			//
			this.label4.Location = new System.Drawing.Point(12, 208);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(110, 16);
			this.label4.TabIndex = 30;
			this.label4.Text = "Time Period";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// listQuantityQualifier
			//
			this.listQuantityQualifier.Location = new System.Drawing.Point(243, 16);
			this.listQuantityQualifier.Name = "listQuantityQualifier";
			this.listQuantityQualifier.Size = new System.Drawing.Size(160, 21);
			this.listQuantityQualifier.TabIndex = 1;
			this.listQuantityQualifier.SelectedIndexChanged += new System.EventHandler(this.listQuantityQualifier_SelectedIndexChanged);
			//
			// label8
			//
			this.label8.Location = new System.Drawing.Point(178, 18);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(62, 16);
			this.label8.TabIndex = 34;
			this.label8.Text = "Qualifier";
			this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// groupQuantity
			//
			this.groupQuantity.Controls.Add(this.textQuantity);
			this.groupQuantity.Controls.Add(this.listQuantityQualifier);
			this.groupQuantity.Controls.Add(this.label8);
			this.groupQuantity.Location = new System.Drawing.Point(12, 230);
			this.groupQuantity.Name = "groupQuantity";
			this.groupQuantity.Size = new System.Drawing.Size(536, 46);
			this.groupQuantity.TabIndex = 10;
			this.groupQuantity.Text = "Quantity";
			//
			// textQuantity
			//
			this.textQuantity.Location = new System.Drawing.Point(113, 16);
			this.textQuantity.MaxVal = 100;
			this.textQuantity.Name = "textQuantity";
			this.textQuantity.Size = new System.Drawing.Size(60, 20);
			this.textQuantity.TabIndex = 0;
			//
			// listCoverageLevel
			//
			this.listCoverageLevel.Location = new System.Drawing.Point(125, 286);
			this.listCoverageLevel.Name = "listCoverageLevel";
			this.listCoverageLevel.Size = new System.Drawing.Size(160, 21);
			this.listCoverageLevel.TabIndex = 11;
			//
			// label1
			//
			this.label1.Location = new System.Drawing.Point(12, 288);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(110, 16);
			this.label1.TabIndex = 32;
			this.label1.Text = "Coverage Level";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAmount
			//
			this.textAmount.Location = new System.Drawing.Point(125, 182);
			this.textAmount.MaxVal = 100000000D;
			this.textAmount.MinVal = -100000000D;
			this.textAmount.Name = "textAmount";
			this.textAmount.Size = new System.Drawing.Size(80, 20);
			this.textAmount.TabIndex = 8;
			//
			// butDelete
			//
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(12, 430);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(75, 26);
			this.butDelete.TabIndex = 17;
			this.butDelete.Text = "&Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(473, 430);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 26);
			this.butSave.TabIndex = 16;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// textPercent
			//
			this.textPercent.Location = new System.Drawing.Point(125, 158);
			this.textPercent.MaxVal = 100;
			this.textPercent.Name = "textPercent";
			this.textPercent.ShowZero = false;
			this.textPercent.Size = new System.Drawing.Size(60, 20);
			this.textPercent.TabIndex = 7;
			//
			// groupBox1
			//
			this.groupBox1.Controls.Add(this.radioCategory);
			this.groupBox1.Controls.Add(this.listCategory);
			this.groupBox1.Controls.Add(this.radioProcCode);
			this.groupBox1.Controls.Add(this.textProcCode);
			this.groupBox1.Controls.Add(this.radioCodeGroup);
			this.groupBox1.Controls.Add(this.comboCodeGroup);
			this.groupBox1.Location = new System.Drawing.Point(12, 32);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(536, 94);
			this.groupBox1.TabIndex = 1;
			this.groupBox1.Text = "Applies to";
			//
			// radioCategory
			//
			this.radioCategory.Location = new System.Drawing.Point(12, 19);
			this.radioCategory.Name = "radioCategory";
			this.radioCategory.Size = new System.Drawing.Size(110, 20);
			this.radioCategory.TabIndex = 0;
			this.radioCategory.TabStop = true;
			this.radioCategory.Text = "Category";
			this.radioCategory.UseVisualStyleBackColor = true;
			//
			// radioProcCode
			//
			this.radioProcCode.Location = new System.Drawing.Point(12, 42);
			this.radioProcCode.Name = "radioProcCode";
			this.radioProcCode.Size = new System.Drawing.Size(110, 20);
			this.radioProcCode.TabIndex = 2;
			this.radioProcCode.Text = "Procedure code";
			this.radioProcCode.UseVisualStyleBackColor = true;
			//
			// radioCodeGroup
			//
			this.radioCodeGroup.Location = new System.Drawing.Point(12, 65);
			this.radioCodeGroup.Name = "radioCodeGroup";
			this.radioCodeGroup.Size = new System.Drawing.Size(110, 20);
			this.radioCodeGroup.TabIndex = 4;
			this.radioCodeGroup.Text = "Code group";
			this.radioCodeGroup.UseVisualStyleBackColor = true;
			//
			// listTreatArea
			//
			this.listTreatArea.Location = new System.Drawing.Point(125, 310);
			this.listTreatArea.Name = "listTreatArea";
			this.listTreatArea.Size = new System.Drawing.Size(160, 21);
			this.listTreatArea.TabIndex = 12;
			//
			// labelTreatmentArea
			//
			this.labelTreatmentArea.Location = new System.Drawing.Point(12, 312);
			this.labelTreatmentArea.Name = "labelTreatmentArea";
			this.labelTreatmentArea.Size = new System.Drawing.Size(110, 16);
			this.labelTreatmentArea.TabIndex = 38;
			this.labelTreatmentArea.Text = "Treatment Area";
			this.labelTreatmentArea.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboCodeGroup
			//
			this.comboCodeGroup.Location = new System.Drawing.Point(125, 65);
			this.comboCodeGroup.Name = "comboCodeGroup";
			this.comboCodeGroup.Size = new System.Drawing.Size(200, 21);
			this.comboCodeGroup.TabIndex = 5;
			this.comboCodeGroup.Text = "comboBox1";
			//
			// labelTeeth
			//
			this.labelTeeth.Location = new System.Drawing.Point(12, 336);
			this.labelTeeth.Name = "labelTeeth";
			this.labelTeeth.Size = new System.Drawing.Size(110, 16);
			this.labelTeeth.TabIndex = 39;
			this.labelTeeth.Text = "Teeth";
			this.labelTeeth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textTeeth
			//
			this.textTeeth.Location = new System.Drawing.Point(125, 334);
			this.textTeeth.Name = "textTeeth";
			this.textTeeth.Size = new System.Drawing.Size(160, 20);
			this.textTeeth.TabIndex = 13;
			//
			// labelTeethHint
			//
			this.labelTeethHint.Location = new System.Drawing.Point(290, 336);
			this.labelTeethHint.Name = "labelTeethHint";
			this.labelTeethHint.Size = new System.Drawing.Size(258, 30);
			this.labelTeethHint.TabIndex = 40;
			this.labelTeethHint.Text = "Exclusions and age limits only. Blank for every tooth. Example: 2,15 or 23-26";
			//
			// labelPreview
			//
			this.labelPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.labelPreview.Location = new System.Drawing.Point(12, 374);
			this.labelPreview.Name = "labelPreview";
			this.labelPreview.Size = new System.Drawing.Size(536, 40);
			this.labelPreview.TabIndex = 41;
			this.labelPreview.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// FormBenefitEdit
			//
			this.ClientSize = new System.Drawing.Size(560, 468);
			this.Controls.Add(this.labelPreview);
			this.Controls.Add(this.labelTeethHint);
			this.Controls.Add(this.textTeeth);
			this.Controls.Add(this.labelTeeth);
			this.Controls.Add(this.labelTreatmentArea);
			this.Controls.Add(this.listTreatArea);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.textPercent);
			this.Controls.Add(this.listTimePeriod);
			this.Controls.Add(this.listCoverageLevel);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.groupQuantity);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.labelPercent);
			this.Controls.Add(this.listBenefitType);
			this.Controls.Add(this.labelType);
			this.Controls.Add(this.checkPat);
			this.Controls.Add(this.textAmount);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelAmount);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormBenefitEdit";
			this.ShowInTaskbar = false;
			this.Text = "Edit Benefit";
			this.Load += new System.EventHandler(this.FormBenefitEdit_Load);
			this.groupQuantity.ResumeLayout(false);
			this.groupQuantity.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		private System.Windows.Forms.Label labelAmount;
		private OpenPmsDental.UI.Button butSave;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.ValidDouble textAmount;
		private OpenPmsDental.UI.ComboBox listCategory;
		private TextBox textProcCode;
		private OpenPmsDental.UI.ComboBox listBenefitType;
		private Label labelTeeth;
		private TextBox textTeeth;
		private Label labelTeethHint;
		private Label labelType;
		private Label labelPercent;
		private OpenPmsDental.UI.ComboBox listTimePeriod;
		private Label label4;
		private ValidNum textQuantity;
		private OpenPmsDental.UI.ComboBox listQuantityQualifier;
		private Label label8;
		private OpenPmsDental.UI.GroupBox groupQuantity;
		private OpenPmsDental.UI.CheckBox checkPat;
		private OpenPmsDental.UI.ComboBox listCoverageLevel;
		private Label label1;
		private ValidNum textPercent;
		private UI.GroupBox groupBox1;
		private RadioButton radioCategory;
		private RadioButton radioProcCode;
		private RadioButton radioCodeGroup;
		private UI.ComboBox listTreatArea;
		private Label labelTreatmentArea;
		private UI.ComboBox comboCodeGroup;
		private Label labelPreview;
	}
}
