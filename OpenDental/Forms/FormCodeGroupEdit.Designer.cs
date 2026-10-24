namespace OpenDental {
	partial class FormCodeGroupEdit {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormCodeGroupEdit));
			this.butSave = new OpenDental.UI.Button();
			this.textGroupName = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.textProcCodes = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.checkShowInFreq = new System.Windows.Forms.CheckBox();
			this.comboCodeGroupFixed = new OpenDental.UI.ComboBox();
			this.label3 = new System.Windows.Forms.Label();
			this.butProcCodesAdd = new OpenDental.UI.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.checkShowInAgeLim = new System.Windows.Forms.CheckBox();
			this.checkShowInOther = new System.Windows.Forms.CheckBox();
			this.checkHidden = new System.Windows.Forms.CheckBox();
			this.butDelete = new OpenDental.UI.Button();
			this.SuspendLayout();
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(584, 264);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 99;
			this.butSave.Text = "Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// textGroupName
			// 
			this.textGroupName.Location = new System.Drawing.Point(149, 77);
			this.textGroupName.MaxLength = 50;
			this.textGroupName.Name = "textGroupName";
			this.textGroupName.Size = new System.Drawing.Size(167, 20);
			this.textGroupName.TabIndex = 1;
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(12, 81);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(136, 14);
			this.label1.TabIndex = 0;
			this.label1.Text = "Group Name";
			this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// textProcCodes
			// 
			this.textProcCodes.Location = new System.Drawing.Point(149, 162);
			this.textProcCodes.Multiline = true;
			this.textProcCodes.Name = "textProcCodes";
			this.textProcCodes.Size = new System.Drawing.Size(410, 49);
			this.textProcCodes.TabIndex = 3;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(12, 166);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(136, 14);
			this.label2.TabIndex = 0;
			this.label2.Text = "Proc Codes";
			this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// checkShowInFreq
			// 
			this.checkShowInFreq.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowInFreq.Location = new System.Drawing.Point(29, 51);
			this.checkShowInFreq.Name = "checkShowInFreq";
			this.checkShowInFreq.Size = new System.Drawing.Size(134, 20);
			this.checkShowInFreq.TabIndex = 6;
			this.checkShowInFreq.Text = "Show In Frequencies";
			this.checkShowInFreq.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowInFreq.UseVisualStyleBackColor = true;
			// 
			// comboCodeGroupFixed
			// 
			this.comboCodeGroupFixed.Location = new System.Drawing.Point(149, 109);
			this.comboCodeGroupFixed.Name = "comboCodeGroupFixed";
			this.comboCodeGroupFixed.Size = new System.Drawing.Size(167, 21);
			this.comboCodeGroupFixed.TabIndex = 2;
			this.comboCodeGroupFixed.Text = "Code Group Fixed";
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(12, 113);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(136, 14);
			this.label3.TabIndex = 0;
			this.label3.Text = "Fixed Group";
			this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// butProcCodesAdd
			// 
			this.butProcCodesAdd.Icon = OpenDental.UI.EnumIcons.Add;
			this.butProcCodesAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butProcCodesAdd.Location = new System.Drawing.Point(565, 162);
			this.butProcCodesAdd.Name = "butProcCodesAdd";
			this.butProcCodesAdd.Size = new System.Drawing.Size(75, 24);
			this.butProcCodesAdd.TabIndex = 4;
			this.butProcCodesAdd.Text = "&Add";
			this.butProcCodesAdd.Click += new System.EventHandler(this.butProcCodesAdd_Click);
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(146, 214);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(410, 14);
			this.label4.TabIndex = 0;
			this.label4.Text = "Codes and ranges separated by commas (e.g. D2391-D2394,D2740)";
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(322, 97);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(325, 59);
			this.label5.TabIndex = 0;
			this.label5.Text = resources.GetString("label5.Text");
			// 
			// checkShowInAgeLim
			// 
			this.checkShowInAgeLim.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowInAgeLim.Location = new System.Drawing.Point(29, 28);
			this.checkShowInAgeLim.Name = "checkShowInAgeLim";
			this.checkShowInAgeLim.Size = new System.Drawing.Size(134, 20);
			this.checkShowInAgeLim.TabIndex = 5;
			this.checkShowInAgeLim.Text = "Show In Age Limits";
			this.checkShowInAgeLim.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowInAgeLim.UseVisualStyleBackColor = true;
			// 
			// checkShowInOther
			// 
			this.checkShowInOther.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowInOther.Location = new System.Drawing.Point(169, 28);
			this.checkShowInOther.Name = "checkShowInOther";
			this.checkShowInOther.Size = new System.Drawing.Size(160, 20);
			this.checkShowInOther.TabIndex = 7;
			this.checkShowInOther.Text = "Show In Other Benefits";
			this.checkShowInOther.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowInOther.UseVisualStyleBackColor = true;
			// 
			// checkHidden
			// 
			this.checkHidden.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkHidden.Location = new System.Drawing.Point(169, 51);
			this.checkHidden.Name = "checkHidden";
			this.checkHidden.Size = new System.Drawing.Size(160, 20);
			this.checkHidden.TabIndex = 8;
			this.checkHidden.Text = "Hidden (retired)";
			this.checkHidden.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkHidden.UseVisualStyleBackColor = true;
			// 
			// butDelete
			// 
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenDental.UI.EnumIcons.DeleteX;
			this.butDelete.Location = new System.Drawing.Point(12, 264);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(75, 24);
			this.butDelete.TabIndex = 98;
			this.butDelete.Text = "&Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			// 
			// FormCodeGroupEdit
			// 
			this.ClientSize = new System.Drawing.Size(671, 300);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.checkHidden);
			this.Controls.Add(this.checkShowInOther);
			this.Controls.Add(this.checkShowInAgeLim);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.butProcCodesAdd);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.comboCodeGroupFixed);
			this.Controls.Add(this.checkShowInFreq);
			this.Controls.Add(this.textProcCodes);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.textGroupName);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.butSave);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormCodeGroupEdit";
			this.Text = "Code Group Edit";
			this.Load += new System.EventHandler(this.FormCodeGroupEdit_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenDental.UI.Button butSave;
		private System.Windows.Forms.TextBox textGroupName;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textProcCodes;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.CheckBox checkShowInFreq;
		private UI.ComboBox comboCodeGroupFixed;
		private System.Windows.Forms.Label label3;
		private UI.Button butProcCodesAdd;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.CheckBox checkShowInAgeLim;
		private System.Windows.Forms.CheckBox checkShowInOther;
		private System.Windows.Forms.CheckBox checkHidden;
		private UI.Button butDelete;
	}
}