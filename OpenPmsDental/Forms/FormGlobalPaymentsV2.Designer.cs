namespace OpenPmsDental {
	partial class FormGlobalPaymentsV2 {
		///<summary>Required designer variable.</summary>
		private System.ComponentModel.IContainer components = null;

		///<summary>Clean up any resources being used.</summary>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		///<summary>Required method for Designer support - do not modify the contents of this method with the code editor.</summary>
		private void InitializeComponent() {
			this.groupTransType = new OpenPmsDental.UI.GroupBox();
			this.radioSale = new System.Windows.Forms.RadioButton();
			this.radioRefund = new System.Windows.Forms.RadioButton();
			this.radioVoid = new System.Windows.Forms.RadioButton();
			this.groupCard = new OpenPmsDental.UI.GroupBox();
			this.radioTerminal = new System.Windows.Forms.RadioButton();
			this.comboTerminal = new OpenPmsDental.UI.ComboBox();
			this.radioSavedCard = new System.Windows.Forms.RadioButton();
			this.labelAmount = new System.Windows.Forms.Label();
			this.textAmount = new System.Windows.Forms.TextBox();
			this.labelTransaction = new System.Windows.Forms.Label();
			this.textTransaction = new System.Windows.Forms.TextBox();
			this.checkSaveCard = new OpenPmsDental.UI.CheckBox();
			this.labelStatus = new System.Windows.Forms.Label();
			this.butRun = new OpenPmsDental.UI.Button();
			this.butCancel = new OpenPmsDental.UI.Button();
			this.groupTransType.SuspendLayout();
			this.groupCard.SuspendLayout();
			this.SuspendLayout();
			//
			// groupTransType
			//
			this.groupTransType.Controls.Add(this.radioSale);
			this.groupTransType.Controls.Add(this.radioRefund);
			this.groupTransType.Controls.Add(this.radioVoid);
			this.groupTransType.Location = new System.Drawing.Point(12, 10);
			this.groupTransType.Name = "groupTransType";
			this.groupTransType.Size = new System.Drawing.Size(416, 46);
			this.groupTransType.TabIndex = 0;
			this.groupTransType.Text = "Transaction Type";
			//
			// radioSale
			//
			this.radioSale.Checked = true;
			this.radioSale.Location = new System.Drawing.Point(10, 20);
			this.radioSale.Name = "radioSale";
			this.radioSale.Size = new System.Drawing.Size(80, 18);
			this.radioSale.TabIndex = 0;
			this.radioSale.TabStop = true;
			this.radioSale.Text = "Sale";
			this.radioSale.UseVisualStyleBackColor = true;
			this.radioSale.CheckedChanged += new System.EventHandler(this.radioTransType_CheckedChanged);
			//
			// radioRefund
			//
			this.radioRefund.Location = new System.Drawing.Point(100, 20);
			this.radioRefund.Name = "radioRefund";
			this.radioRefund.Size = new System.Drawing.Size(80, 18);
			this.radioRefund.TabIndex = 1;
			this.radioRefund.Text = "Refund";
			this.radioRefund.UseVisualStyleBackColor = true;
			this.radioRefund.CheckedChanged += new System.EventHandler(this.radioTransType_CheckedChanged);
			//
			// radioVoid
			//
			this.radioVoid.Location = new System.Drawing.Point(190, 20);
			this.radioVoid.Name = "radioVoid";
			this.radioVoid.Size = new System.Drawing.Size(80, 18);
			this.radioVoid.TabIndex = 2;
			this.radioVoid.Text = "Void";
			this.radioVoid.UseVisualStyleBackColor = true;
			this.radioVoid.CheckedChanged += new System.EventHandler(this.radioTransType_CheckedChanged);
			//
			// groupCard
			//
			this.groupCard.Controls.Add(this.radioTerminal);
			this.groupCard.Controls.Add(this.comboTerminal);
			this.groupCard.Controls.Add(this.radioSavedCard);
			this.groupCard.Location = new System.Drawing.Point(12, 62);
			this.groupCard.Name = "groupCard";
			this.groupCard.Size = new System.Drawing.Size(416, 74);
			this.groupCard.TabIndex = 1;
			this.groupCard.Text = "Card";
			//
			// radioTerminal
			//
			this.radioTerminal.Checked = true;
			this.radioTerminal.Location = new System.Drawing.Point(10, 20);
			this.radioTerminal.Name = "radioTerminal";
			this.radioTerminal.Size = new System.Drawing.Size(130, 18);
			this.radioTerminal.TabIndex = 0;
			this.radioTerminal.TabStop = true;
			this.radioTerminal.Text = "Card on terminal";
			this.radioTerminal.UseVisualStyleBackColor = true;
			this.radioTerminal.CheckedChanged += new System.EventHandler(this.radioCard_CheckedChanged);
			//
			// comboTerminal
			//
			this.comboTerminal.Location = new System.Drawing.Point(146, 18);
			this.comboTerminal.Name = "comboTerminal";
			this.comboTerminal.Size = new System.Drawing.Size(258, 21);
			this.comboTerminal.TabIndex = 1;
			//
			// radioSavedCard
			//
			this.radioSavedCard.Location = new System.Drawing.Point(10, 46);
			this.radioSavedCard.Name = "radioSavedCard";
			this.radioSavedCard.Size = new System.Drawing.Size(394, 18);
			this.radioSavedCard.TabIndex = 2;
			this.radioSavedCard.Text = "Saved card";
			this.radioSavedCard.UseVisualStyleBackColor = true;
			this.radioSavedCard.CheckedChanged += new System.EventHandler(this.radioCard_CheckedChanged);
			//
			// labelAmount
			//
			this.labelAmount.Location = new System.Drawing.Point(12, 147);
			this.labelAmount.Name = "labelAmount";
			this.labelAmount.Size = new System.Drawing.Size(120, 16);
			this.labelAmount.TabIndex = 2;
			this.labelAmount.Text = "Amount";
			this.labelAmount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAmount
			//
			this.textAmount.Location = new System.Drawing.Point(136, 145);
			this.textAmount.Name = "textAmount";
			this.textAmount.Size = new System.Drawing.Size(100, 20);
			this.textAmount.TabIndex = 3;
			//
			// labelTransaction
			//
			this.labelTransaction.Location = new System.Drawing.Point(12, 173);
			this.labelTransaction.Name = "labelTransaction";
			this.labelTransaction.Size = new System.Drawing.Size(120, 16);
			this.labelTransaction.TabIndex = 4;
			this.labelTransaction.Text = "Transaction to void";
			this.labelTransaction.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textTransaction
			//
			this.textTransaction.Location = new System.Drawing.Point(136, 171);
			this.textTransaction.Name = "textTransaction";
			this.textTransaction.ReadOnly = true;
			this.textTransaction.Size = new System.Drawing.Size(268, 20);
			this.textTransaction.TabIndex = 5;
			//
			// checkSaveCard
			//
			this.checkSaveCard.Location = new System.Drawing.Point(136, 197);
			this.checkSaveCard.Name = "checkSaveCard";
			this.checkSaveCard.Size = new System.Drawing.Size(268, 18);
			this.checkSaveCard.TabIndex = 6;
			this.checkSaveCard.Text = "Save card for later charges";
			//
			// labelStatus
			//
			this.labelStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.labelStatus.Location = new System.Drawing.Point(12, 224);
			this.labelStatus.Name = "labelStatus";
			this.labelStatus.Size = new System.Drawing.Size(416, 84);
			this.labelStatus.TabIndex = 7;
			//
			// butRun
			//
			this.butRun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butRun.Location = new System.Drawing.Point(262, 320);
			this.butRun.Name = "butRun";
			this.butRun.Size = new System.Drawing.Size(80, 24);
			this.butRun.TabIndex = 8;
			this.butRun.Text = "&Run";
			this.butRun.Click += new System.EventHandler(this.butRun_Click);
			//
			// butCancel
			//
			this.butCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butCancel.Location = new System.Drawing.Point(348, 320);
			this.butCancel.Name = "butCancel";
			this.butCancel.Size = new System.Drawing.Size(80, 24);
			this.butCancel.TabIndex = 9;
			this.butCancel.Text = "&Cancel";
			this.butCancel.Click += new System.EventHandler(this.butCancel_Click);
			//
			// FormGlobalPaymentsV2
			//
			this.ClientSize = new System.Drawing.Size(440, 356);
			this.Controls.Add(this.butCancel);
			this.Controls.Add(this.butRun);
			this.Controls.Add(this.labelStatus);
			this.Controls.Add(this.checkSaveCard);
			this.Controls.Add(this.textTransaction);
			this.Controls.Add(this.labelTransaction);
			this.Controls.Add(this.textAmount);
			this.Controls.Add(this.labelAmount);
			this.Controls.Add(this.groupCard);
			this.Controls.Add(this.groupTransType);
			this.Name = "FormGlobalPaymentsV2";
			this.Text = "Global Payments V2";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormGlobalPaymentsV2_FormClosing);
			this.Load += new System.EventHandler(this.FormGlobalPaymentsV2_Load);
			this.groupTransType.ResumeLayout(false);
			this.groupCard.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenPmsDental.UI.GroupBox groupTransType;
		private System.Windows.Forms.RadioButton radioSale;
		private System.Windows.Forms.RadioButton radioRefund;
		private System.Windows.Forms.RadioButton radioVoid;
		private OpenPmsDental.UI.GroupBox groupCard;
		private System.Windows.Forms.RadioButton radioTerminal;
		private OpenPmsDental.UI.ComboBox comboTerminal;
		private System.Windows.Forms.RadioButton radioSavedCard;
		private System.Windows.Forms.Label labelAmount;
		private System.Windows.Forms.TextBox textAmount;
		private System.Windows.Forms.Label labelTransaction;
		private System.Windows.Forms.TextBox textTransaction;
		private OpenPmsDental.UI.CheckBox checkSaveCard;
		private System.Windows.Forms.Label labelStatus;
		private OpenPmsDental.UI.Button butRun;
		private OpenPmsDental.UI.Button butCancel;
	}
}
