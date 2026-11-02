namespace OpenPmsDental{
	partial class FormRpEraAutoProcessed {
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
			this.dateRangePicker = new OpenPmsDental.UI.ODDateRangePicker();
			this.checkShowAcknowledged = new OpenPmsDental.UI.CheckBox();
			this.textCarrier = new OpenPmsDental.ODtextBox();
			this.textCheckTrace = new OpenPmsDental.ODtextBox();
			this.labelCheckTrace = new System.Windows.Forms.Label();
			this.labelCarrier = new System.Windows.Forms.Label();
			this.butRefresh = new OpenPmsDental.UI.Button();
			this.gridEras = new OpenPmsDental.UI.GridOD();
			this.gridClaims = new OpenPmsDental.UI.GridOD();
			this.butAcknowledge = new OpenPmsDental.UI.Button();
			this.comboClinics = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.listAutoProcessStatuses = new OpenPmsDental.UI.ListBox();
			this.labelAutoProcessStatus = new System.Windows.Forms.Label();
			this.butSelectEob = new OpenPmsDental.UI.Button();
			this.gridClaimProcedureAdjustments = new OpenPmsDental.UI.GridOD();
			this.checkShowPreAuths = new OpenPmsDental.UI.CheckBox();
			this.checkShowZeroPayments = new OpenPmsDental.UI.CheckBox();
			this.SuspendLayout();
			// 
			// dateRangePicker
			// 
			this.dateRangePicker.BackColor = System.Drawing.Color.Transparent;
			this.dateRangePicker.Location = new System.Drawing.Point(12, 13);
			this.dateRangePicker.MinimumSize = new System.Drawing.Size(453, 22);
			this.dateRangePicker.Name = "dateRangePicker";
			this.dateRangePicker.Size = new System.Drawing.Size(453, 24);
			this.dateRangePicker.TabIndex = 0;
			// 
			// checkShowAcknowledged
			// 
			this.checkShowAcknowledged.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowAcknowledged.Location = new System.Drawing.Point(901, 64);
			this.checkShowAcknowledged.Name = "checkShowAcknowledged";
			this.checkShowAcknowledged.Size = new System.Drawing.Size(162, 17);
			this.checkShowAcknowledged.TabIndex = 5;
			this.checkShowAcknowledged.Text = "Show Acknowledged";
			// 
			// textCarrier
			// 
			this.textCarrier.AcceptsTab = true;
			this.textCarrier.BackColor = System.Drawing.SystemColors.Window;
			this.textCarrier.DetectLinksEnabled = false;
			this.textCarrier.DetectUrls = false;
			this.textCarrier.Location = new System.Drawing.Point(599, 5);
			this.textCarrier.Multiline = false;
			this.textCarrier.Name = "textCarrier";
			this.textCarrier.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.InsPlan;
			this.textCarrier.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textCarrier.Size = new System.Drawing.Size(161, 21);
			this.textCarrier.TabIndex = 1;
			this.textCarrier.Text = "";
			// 
			// textCheckTrace
			// 
			this.textCheckTrace.AcceptsTab = true;
			this.textCheckTrace.BackColor = System.Drawing.SystemColors.Window;
			this.textCheckTrace.DetectLinksEnabled = false;
			this.textCheckTrace.DetectUrls = false;
			this.textCheckTrace.Location = new System.Drawing.Point(599, 26);
			this.textCheckTrace.Multiline = false;
			this.textCheckTrace.Name = "textCheckTrace";
			this.textCheckTrace.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.FinancialNotes;
			this.textCheckTrace.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textCheckTrace.Size = new System.Drawing.Size(161, 21);
			this.textCheckTrace.TabIndex = 2;
			this.textCheckTrace.Text = "";
			// 
			// labelCheckTrace
			// 
			this.labelCheckTrace.Location = new System.Drawing.Point(469, 28);
			this.labelCheckTrace.Name = "labelCheckTrace";
			this.labelCheckTrace.Size = new System.Drawing.Size(130, 16);
			this.labelCheckTrace.TabIndex = 275;
			this.labelCheckTrace.Text = "Check# or EFT Trace#";
			this.labelCheckTrace.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelCarrier
			// 
			this.labelCarrier.Location = new System.Drawing.Point(544, 5);
			this.labelCarrier.Name = "labelCarrier";
			this.labelCarrier.Size = new System.Drawing.Size(55, 16);
			this.labelCarrier.TabIndex = 274;
			this.labelCarrier.Text = "Carrier";
			this.labelCarrier.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// butRefresh
			// 
			this.butRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butRefresh.Location = new System.Drawing.Point(1137, 57);
			this.butRefresh.Name = "butRefresh";
			this.butRefresh.Size = new System.Drawing.Size(75, 24);
			this.butRefresh.TabIndex = 8;
			this.butRefresh.Text = "Refresh";
			this.butRefresh.UseVisualStyleBackColor = true;
			this.butRefresh.Click += new System.EventHandler(this.butRefresh_Click);
			// 
			// gridEras
			// 
			this.gridEras.AllowSortingByColumn = true;
			this.gridEras.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridEras.Location = new System.Drawing.Point(12, 87);
			this.gridEras.Name = "gridEras";
			this.gridEras.SelectionMode = OpenPmsDental.UI.GridSelectionMode.MultiExtended;
			this.gridEras.Size = new System.Drawing.Size(1200, 149);
			this.gridEras.TabIndex = 9;
			this.gridEras.Title = "ERAs";
			this.gridEras.TranslationName = "TableEtrans835s";
			this.gridEras.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridEras_CellDoubleClick);
			this.gridEras.MouseUp += new System.Windows.Forms.MouseEventHandler(this.gridEras_MouseUp);
			// 
			// gridClaims
			// 
			this.gridClaims.AllowSortingByColumn = true;
			this.gridClaims.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridClaims.Location = new System.Drawing.Point(12, 242);
			this.gridClaims.Name = "gridClaims";
			this.gridClaims.Size = new System.Drawing.Size(1200, 288);
			this.gridClaims.TabIndex = 10;
			this.gridClaims.Title = "Claims";
			this.gridClaims.TranslationName = "TableEtrans835s";
			this.gridClaims.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridClaims_CellDoubleClick);
			this.gridClaims.SelectionCommitted += new System.EventHandler(this.gridClaims_SelectionCommitted);
			// 
			// butAcknowledge
			// 
			this.butAcknowledge.Location = new System.Drawing.Point(12, 57);
			this.butAcknowledge.Name = "butAcknowledge";
			this.butAcknowledge.Size = new System.Drawing.Size(144, 24);
			this.butAcknowledge.TabIndex = 6;
			this.butAcknowledge.Text = "Acknowledge ERA(s)";
			this.butAcknowledge.UseVisualStyleBackColor = true;
			this.butAcknowledge.Click += new System.EventHandler(this.butAcknowledge_Click);
			// 
			// comboClinics
			// 
			this.comboClinics.IncludeAll = true;
			this.comboClinics.IncludeUnassigned = true;
			this.comboClinics.Location = new System.Drawing.Point(562, 47);
			this.comboClinics.Name = "comboClinics";
			this.comboClinics.IsMultiSelect = true;
			this.comboClinics.Size = new System.Drawing.Size(198, 21);
			this.comboClinics.TabIndex = 3;
			// 
			// listAutoProcessStatuses
			// 
			this.listAutoProcessStatuses.Location = new System.Drawing.Point(910, 5);
			this.listAutoProcessStatuses.Name = "listAutoProcessStatuses";
			this.listAutoProcessStatuses.SelectionMode = OpenPmsDental.UI.SelectionMode.MultiExtended;
			this.listAutoProcessStatuses.Size = new System.Drawing.Size(153, 56);
			this.listAutoProcessStatuses.TabIndex = 4;
			// 
			// labelAutoProcessStatus
			// 
			this.labelAutoProcessStatus.Location = new System.Drawing.Point(781, 5);
			this.labelAutoProcessStatus.Name = "labelAutoProcessStatus";
			this.labelAutoProcessStatus.Size = new System.Drawing.Size(128, 16);
			this.labelAutoProcessStatus.TabIndex = 283;
			this.labelAutoProcessStatus.Text = "Auto Processed Result";
			this.labelAutoProcessStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// butSelectEob
			// 
			this.butSelectEob.Location = new System.Drawing.Point(162, 57);
			this.butSelectEob.Name = "butSelectEob";
			this.butSelectEob.Size = new System.Drawing.Size(90, 24);
			this.butSelectEob.TabIndex = 7;
			this.butSelectEob.Text = "Select EOB";
			this.butSelectEob.UseVisualStyleBackColor = true;
			this.butSelectEob.Visible = false;
			this.butSelectEob.Click += new System.EventHandler(this.butSelectEob_Click);
			// 
			// gridClaimProcedureAdjustments
			// 
			this.gridClaimProcedureAdjustments.AllowSortingByColumn = true;
			this.gridClaimProcedureAdjustments.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridClaimProcedureAdjustments.Location = new System.Drawing.Point(12, 536);
			this.gridClaimProcedureAdjustments.Name = "gridClaimProcedureAdjustments";
			this.gridClaimProcedureAdjustments.Size = new System.Drawing.Size(1200, 154);
			this.gridClaimProcedureAdjustments.TabIndex = 11;
			this.gridClaimProcedureAdjustments.Title = "Claim Procedure Adjustments";
			this.gridClaimProcedureAdjustments.TranslationName = "TableEtrans835s";
			//
			// checkShowPreAuths
			//
			this.checkShowPreAuths.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowPreAuths.Checked = true;
			this.checkShowPreAuths.Location = new System.Drawing.Point(1070, 7);
			this.checkShowPreAuths.Name = "checkShowPreAuths";
			this.checkShowPreAuths.Size = new System.Drawing.Size(142, 17);
			this.checkShowPreAuths.TabIndex = 12;
			this.checkShowPreAuths.Text = "Show PreAuths";
			//
			// checkShowZeroPayments
			//
			this.checkShowZeroPayments.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkShowZeroPayments.Checked = true;
			this.checkShowZeroPayments.Location = new System.Drawing.Point(1070, 28);
			this.checkShowZeroPayments.Name = "checkShowZeroPayments";
			this.checkShowZeroPayments.Size = new System.Drawing.Size(142, 17);
			this.checkShowZeroPayments.TabIndex = 13;
			this.checkShowZeroPayments.Text = "Show $0 Payments";
			//
			// FormRpEraAutoProcessed
			//
			this.ClientSize = new System.Drawing.Size(1224, 696);
			this.Controls.Add(this.checkShowZeroPayments);
			this.Controls.Add(this.checkShowPreAuths);
			this.Controls.Add(this.gridClaimProcedureAdjustments);
			this.Controls.Add(this.butSelectEob);
			this.Controls.Add(this.listAutoProcessStatuses);
			this.Controls.Add(this.labelAutoProcessStatus);
			this.Controls.Add(this.comboClinics);
			this.Controls.Add(this.butAcknowledge);
			this.Controls.Add(this.gridClaims);
			this.Controls.Add(this.gridEras);
			this.Controls.Add(this.butRefresh);
			this.Controls.Add(this.textCarrier);
			this.Controls.Add(this.textCheckTrace);
			this.Controls.Add(this.labelCheckTrace);
			this.Controls.Add(this.labelCarrier);
			this.Controls.Add(this.checkShowAcknowledged);
			this.Controls.Add(this.dateRangePicker);
			this.Name = "FormRpEraAutoProcessed";
			this.Text = "ERAs Automatically Processed";
			this.Load += new System.EventHandler(this.FormRpEraAutoProcessed_Load);
			this.ResumeLayout(false);

		}

		#endregion
		private UI.ODDateRangePicker dateRangePicker;
		private OpenPmsDental.UI.CheckBox checkShowAcknowledged;
		private ODtextBox textCarrier;
		private ODtextBox textCheckTrace;
		private System.Windows.Forms.Label labelCheckTrace;
		private System.Windows.Forms.Label labelCarrier;
		private UI.Button butRefresh;
		private UI.GridOD gridEras;
		private UI.GridOD gridClaims;
		private UI.Button butAcknowledge;
		private UI.ComboBoxClinicPicker comboClinics;
		private UI.ListBox listAutoProcessStatuses;
		private System.Windows.Forms.Label labelAutoProcessStatus;
		private UI.Button butSelectEob;
		private UI.GridOD gridClaimProcedureAdjustments;
		private OpenPmsDental.UI.CheckBox checkShowPreAuths;
		private OpenPmsDental.UI.CheckBox checkShowZeroPayments;
	}
}