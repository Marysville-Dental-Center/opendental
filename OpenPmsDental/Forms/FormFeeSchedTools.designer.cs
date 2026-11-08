using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormFeeSchedTools {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.checkShowGroups = new OpenPmsDental.UI.CheckBox();
			this.comboClinicTo = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.groupBox7 = new OpenPmsDental.UI.GroupBox();
			this.comboProvider = new OpenPmsDental.UI.ComboBox();
			this.comboGroup = new OpenPmsDental.UI.ComboBox();
			this.comboClinic = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.comboFeeSched = new OpenPmsDental.UI.ComboBox();
			this.butPickProv = new OpenPmsDental.UI.Button();
			this.label12 = new System.Windows.Forms.Label();
			this.butPickGroup = new OpenPmsDental.UI.Button();
			this.butPickSched = new OpenPmsDental.UI.Button();
			this.label8 = new System.Windows.Forms.Label();
			this.labelGroup = new System.Windows.Forms.Label();
			this.groupGlobalUpdateFees = new OpenPmsDental.UI.GroupBox();
			this.label9 = new System.Windows.Forms.Label();
			this.butUpdateWriteoffs = new OpenPmsDental.UI.Button();
			this.comboGlobalUpdateClinics = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.label4 = new System.Windows.Forms.Label();
			this.butUpdate = new OpenPmsDental.UI.Button();
			this.groupBox5 = new OpenPmsDental.UI.GroupBox();
			this.butImport = new OpenPmsDental.UI.Button();
			this.butExport = new OpenPmsDental.UI.Button();
			this.groupBox3 = new OpenPmsDental.UI.GroupBox();
			this.label5 = new System.Windows.Forms.Label();
			this.groupBox4 = new OpenPmsDental.UI.GroupBox();
			this.radioPenny = new System.Windows.Forms.RadioButton();
			this.radioDime = new System.Windows.Forms.RadioButton();
			this.radioDollar = new System.Windows.Forms.RadioButton();
			this.label3 = new System.Windows.Forms.Label();
			this.butIncrease = new OpenPmsDental.UI.Button();
			this.textPercent = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox2 = new OpenPmsDental.UI.GroupBox();
			this.label11 = new System.Windows.Forms.Label();
			this.butClear = new OpenPmsDental.UI.Button();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.butPickGroupTo = new OpenPmsDental.UI.Button();
			this.labelGroupTo = new System.Windows.Forms.Label();
			this.comboGroupTo = new OpenPmsDental.UI.ComboBox();
			this.comboProviderTo = new OpenPmsDental.UI.ComboBox();
			this.comboFeeSchedTo = new OpenPmsDental.UI.ComboBox();
			this.label7 = new System.Windows.Forms.Label();
			this.butPickProvTo = new OpenPmsDental.UI.Button();
			this.butPickSchedTo = new OpenPmsDental.UI.Button();
			this.butCopy = new OpenPmsDental.UI.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.labelDateAsOf = new System.Windows.Forms.Label();
			this.textDateAsOf = new OpenPmsDental.ValidDate();
			this.labelDateAsOfNote = new System.Windows.Forms.Label();
			this.labelDateEffectiveCopy = new System.Windows.Forms.Label();
			this.textDateEffectiveCopy = new OpenPmsDental.ValidDate();
			this.labelDateEffectiveCopyNote = new System.Windows.Forms.Label();
			this.checkExportFuture = new OpenPmsDental.UI.CheckBox();
			this.labelDateEffectiveImport = new System.Windows.Forms.Label();
			this.textDateEffectiveImport = new OpenPmsDental.ValidDate();
			this.labelDateEffectiveNew = new System.Windows.Forms.Label();
			this.textDateEffectiveNew = new OpenPmsDental.ValidDate();
			this.tabControlTools = new OpenPmsDental.UI.TabControl();
			this.tabPageCopy = new OpenPmsDental.UI.TabPage();
			this.tabPageIncrease = new OpenPmsDental.UI.TabPage();
			this.tabPageClear = new OpenPmsDental.UI.TabPage();
			this.tabPageExportImport = new OpenPmsDental.UI.TabPage();
			this.tabPageGlobalUpdates = new OpenPmsDental.UI.TabPage();
			this.tabPageCopyCode = new OpenPmsDental.UI.TabPage();
			this.groupBoxCopyCode = new OpenPmsDental.UI.GroupBox();
			this.labelCopyCodeExplain = new System.Windows.Forms.Label();
			this.labelCodeFrom = new System.Windows.Forms.Label();
			this.textCodeFrom = new System.Windows.Forms.TextBox();
			this.butPickCodeFrom = new OpenPmsDental.UI.Button();
			this.labelCodeTo = new System.Windows.Forms.Label();
			this.textCodeTo = new System.Windows.Forms.TextBox();
			this.butPickCodeTo = new OpenPmsDental.UI.Button();
			this.radioCopyCodeAllScheds = new System.Windows.Forms.RadioButton();
			this.radioCopyCodeOneSched = new System.Windows.Forms.RadioButton();
			this.butCopyCode = new OpenPmsDental.UI.Button();
			this.tabControlTools.SuspendLayout();
			this.tabPageCopy.SuspendLayout();
			this.tabPageIncrease.SuspendLayout();
			this.tabPageClear.SuspendLayout();
			this.tabPageExportImport.SuspendLayout();
			this.tabPageGlobalUpdates.SuspendLayout();
			this.tabPageCopyCode.SuspendLayout();
			this.groupBoxCopyCode.SuspendLayout();
			this.groupBox7.SuspendLayout();
			this.groupGlobalUpdateFees.SuspendLayout();
			this.groupBox5.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox4.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// checkShowGroups
			// 
			this.checkShowGroups.Location = new System.Drawing.Point(12, 8);
			this.checkShowGroups.Name = "checkShowGroups";
			this.checkShowGroups.Size = new System.Drawing.Size(173, 17);
			this.checkShowGroups.TabIndex = 40;
			this.checkShowGroups.Text = "Show Fee Schedule Groups";
			this.checkShowGroups.Visible = false;
			this.checkShowGroups.CheckedChanged += new System.EventHandler(this.checkShowGroups_CheckedChanged);
			// 
			// comboClinicTo
			// 
			this.comboClinicTo.HqDescription = "Default";
			this.comboClinicTo.IncludeUnassigned = true;
			this.comboClinicTo.IsMultiSelect = true;
			this.comboClinicTo.Location = new System.Drawing.Point(51, 46);
			this.comboClinicTo.Name = "comboClinicTo";
			this.comboClinicTo.Size = new System.Drawing.Size(301, 21);
			this.comboClinicTo.TabIndex = 2;
			// 
			// groupBox7
			// 
			this.groupBox7.Controls.Add(this.comboProvider);
			this.groupBox7.Controls.Add(this.comboGroup);
			this.groupBox7.Controls.Add(this.comboClinic);
			this.groupBox7.Controls.Add(this.comboFeeSched);
			this.groupBox7.Controls.Add(this.butPickProv);
			this.groupBox7.Controls.Add(this.label12);
			this.groupBox7.Controls.Add(this.butPickGroup);
			this.groupBox7.Controls.Add(this.butPickSched);
			this.groupBox7.Controls.Add(this.label8);
			this.groupBox7.Controls.Add(this.labelGroup);
			this.groupBox7.Location = new System.Drawing.Point(12, 31);
			this.groupBox7.Name = "groupBox7";
			this.groupBox7.Size = new System.Drawing.Size(391, 105);
			this.groupBox7.TabIndex = 0;
			this.groupBox7.TabStop = false;
			this.groupBox7.Text = "Select Fees";
			// 
			// comboProvider
			// 
			this.comboProvider.Location = new System.Drawing.Point(88, 73);
			this.comboProvider.Name = "comboProvider";
			this.comboProvider.Size = new System.Drawing.Size(264, 21);
			this.comboProvider.TabIndex = 4;
			// 
			// comboGroup
			// 
			this.comboGroup.Location = new System.Drawing.Point(-160, 43);
			this.comboGroup.Name = "comboGroup";
			this.comboGroup.Size = new System.Drawing.Size(174, 21);
			this.comboGroup.TabIndex = 41;
			this.comboGroup.Visible = false;
			// 
			// comboClinic
			// 
			this.comboClinic.HqDescription = "Default";
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(51, 46);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(301, 21);
			this.comboClinic.TabIndex = 2;
			// 
			// comboFeeSched
			// 
			this.comboFeeSched.Location = new System.Drawing.Point(88, 20);
			this.comboFeeSched.Name = "comboFeeSched";
			this.comboFeeSched.Size = new System.Drawing.Size(264, 21);
			this.comboFeeSched.TabIndex = 0;
			this.comboFeeSched.SelectionChangeCommitted += new System.EventHandler(this.comboFeeCombos_SelectionChangeCommitted);
			// 
			// butPickProv
			// 
			this.butPickProv.Location = new System.Drawing.Point(358, 73);
			this.butPickProv.Name = "butPickProv";
			this.butPickProv.Size = new System.Drawing.Size(23, 21);
			this.butPickProv.TabIndex = 5;
			this.butPickProv.Text = "...";
			this.butPickProv.Click += new System.EventHandler(this.butPickProvider_Click);
			// 
			// label12
			// 
			this.label12.Location = new System.Drawing.Point(3, 20);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(87, 17);
			this.label12.TabIndex = 35;
			this.label12.Text = "Fee Schedule";
			this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// butPickGroup
			// 
			this.butPickGroup.Location = new System.Drawing.Point(358, 47);
			this.butPickGroup.Name = "butPickGroup";
			this.butPickGroup.Size = new System.Drawing.Size(23, 21);
			this.butPickGroup.TabIndex = 3;
			this.butPickGroup.Text = "...";
			this.butPickGroup.Visible = false;
			this.butPickGroup.Click += new System.EventHandler(this.butPickGroup_Click);
			// 
			// butPickSched
			// 
			this.butPickSched.Location = new System.Drawing.Point(358, 20);
			this.butPickSched.Name = "butPickSched";
			this.butPickSched.Size = new System.Drawing.Size(23, 21);
			this.butPickSched.TabIndex = 1;
			this.butPickSched.Text = "...";
			this.butPickSched.Click += new System.EventHandler(this.butPickFeeSched_Click);
			// 
			// label8
			// 
			this.label8.Location = new System.Drawing.Point(22, 75);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(65, 17);
			this.label8.TabIndex = 33;
			this.label8.Text = "Provider";
			this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelGroup
			// 
			this.labelGroup.Location = new System.Drawing.Point(48, 47);
			this.labelGroup.Name = "labelGroup";
			this.labelGroup.Size = new System.Drawing.Size(36, 17);
			this.labelGroup.TabIndex = 34;
			this.labelGroup.Text = "Group";
			this.labelGroup.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.labelGroup.Visible = false;
			// 
			// groupGlobalUpdateFees
			// 
			this.groupGlobalUpdateFees.Controls.Add(this.label9);
			this.groupGlobalUpdateFees.Controls.Add(this.butUpdateWriteoffs);
			this.groupGlobalUpdateFees.Controls.Add(this.comboGlobalUpdateClinics);
			this.groupGlobalUpdateFees.Controls.Add(this.label4);
			this.groupGlobalUpdateFees.Controls.Add(this.butUpdate);
			this.groupGlobalUpdateFees.Location = new System.Drawing.Point(5, 6);
			this.groupGlobalUpdateFees.Name = "groupGlobalUpdateFees";
			this.groupGlobalUpdateFees.Size = new System.Drawing.Size(248, 230);
			this.groupGlobalUpdateFees.TabIndex = 5;
			this.groupGlobalUpdateFees.TabStop = false;
			this.groupGlobalUpdateFees.Text = "Global Updates";
			// 
			// label9
			// 
			this.label9.Location = new System.Drawing.Point(6, 114);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(239, 77);
			this.label9.TabIndex = 6;
			this.label9.Text = "Only for offices that run reports after updating fee schedules and before selecti" +
    "ng patients. Recalculates treatment planned estimates, write-offs, ortho cases, " +
    "and discount plan amounts.";
			this.label9.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// butUpdateWriteoffs
			// 
			this.butUpdateWriteoffs.Location = new System.Drawing.Point(6, 194);
			this.butUpdateWriteoffs.Name = "butUpdateWriteoffs";
			this.butUpdateWriteoffs.Size = new System.Drawing.Size(130, 24);
			this.butUpdateWriteoffs.TabIndex = 3;
			this.butUpdateWriteoffs.Text = "Update Estimates Only";
			this.butUpdateWriteoffs.Click += new System.EventHandler(this.butUpdateWriteoffs_Click);
			// 
			// comboGlobalUpdateClinics
			// 
			this.comboGlobalUpdateClinics.IncludeAll = true;
			this.comboGlobalUpdateClinics.IncludeUnassigned = true;
			this.comboGlobalUpdateClinics.IsMultiSelect = true;
			this.comboGlobalUpdateClinics.Location = new System.Drawing.Point(6, 26);
			this.comboGlobalUpdateClinics.Name = "comboGlobalUpdateClinics";
			this.comboGlobalUpdateClinics.Size = new System.Drawing.Size(231, 21);
			this.comboGlobalUpdateClinics.TabIndex = 0;
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(6, 60);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(208, 18);
			this.label4.TabIndex = 5;
			this.label4.Text = "Update fees for all patients";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// butUpdate
			// 
			this.butUpdate.Location = new System.Drawing.Point(6, 84);
			this.butUpdate.Name = "butUpdate";
			this.butUpdate.Size = new System.Drawing.Size(130, 24);
			this.butUpdate.TabIndex = 2;
			this.butUpdate.Text = "Update Proc Fees Only";
			this.butUpdate.Click += new System.EventHandler(this.butUpdateFees_Click);
			// 
			// groupBox5
			// 
			this.groupBox5.Controls.Add(this.butImport);
			this.groupBox5.Controls.Add(this.butExport);
			this.groupBox5.Controls.Add(this.checkExportFuture);
			this.groupBox5.Controls.Add(this.labelDateEffectiveImport);
			this.groupBox5.Controls.Add(this.textDateEffectiveImport);
			this.groupBox5.Location = new System.Drawing.Point(5, 6);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(391, 81);
			this.groupBox5.TabIndex = 3;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "Export/Import";
			// 
			// butImport
			// 
			this.butImport.Location = new System.Drawing.Point(88, 21);
			this.butImport.Name = "butImport";
			this.butImport.Size = new System.Drawing.Size(84, 24);
			this.butImport.TabIndex = 1;
			this.butImport.Text = "Import";
			this.butImport.Click += new System.EventHandler(this.butImport_Click);
			// 
			// butExport
			// 
			this.butExport.Location = new System.Drawing.Point(6, 21);
			this.butExport.Name = "butExport";
			this.butExport.Size = new System.Drawing.Size(75, 24);
			this.butExport.TabIndex = 0;
			this.butExport.Text = "Export";
			this.butExport.Click += new System.EventHandler(this.butExport_Click);
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.label5);
			this.groupBox3.Controls.Add(this.groupBox4);
			this.groupBox3.Controls.Add(this.label3);
			this.groupBox3.Controls.Add(this.butIncrease);
			this.groupBox3.Controls.Add(this.textPercent);
			this.groupBox3.Controls.Add(this.label2);
			this.groupBox3.Controls.Add(this.labelDateEffectiveNew);
			this.groupBox3.Controls.Add(this.textDateEffectiveNew);
			this.groupBox3.Controls.Add(this.labelDateAsOf);
			this.groupBox3.Controls.Add(this.textDateAsOf);
			this.groupBox3.Controls.Add(this.labelDateAsOfNote);
			this.groupBox3.Location = new System.Drawing.Point(5, 6);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(391, 222);
			this.groupBox3.TabIndex = 2;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Increase by %";
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(87, 190);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(90, 18);
			this.label5.TabIndex = 11;
			this.label5.Text = "(or decrease)";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// groupBox4
			// 
			this.groupBox4.Controls.Add(this.radioPenny);
			this.groupBox4.Controls.Add(this.radioDime);
			this.groupBox4.Controls.Add(this.radioDollar);
			this.groupBox4.Location = new System.Drawing.Point(6, 49);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(208, 75);
			this.groupBox4.TabIndex = 1;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "Round to nearest";
			// 
			// radioPenny
			// 
			this.radioPenny.Location = new System.Drawing.Point(14, 52);
			this.radioPenny.Name = "radioPenny";
			this.radioPenny.Size = new System.Drawing.Size(104, 17);
			this.radioPenny.TabIndex = 2;
			this.radioPenny.Text = "$.01";
			// 
			// radioDime
			// 
			this.radioDime.Location = new System.Drawing.Point(14, 35);
			this.radioDime.Name = "radioDime";
			this.radioDime.Size = new System.Drawing.Size(104, 17);
			this.radioDime.TabIndex = 1;
			this.radioDime.Text = "$.10";
			// 
			// radioDollar
			// 
			this.radioDollar.Checked = true;
			this.radioDollar.Location = new System.Drawing.Point(14, 18);
			this.radioDollar.Name = "radioDollar";
			this.radioDollar.Size = new System.Drawing.Size(104, 17);
			this.radioDollar.TabIndex = 0;
			this.radioDollar.TabStop = true;
			this.radioDollar.Text = "$1";
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(92, 24);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(109, 18);
			this.label3.TabIndex = 6;
			this.label3.Text = "for example: 5";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// butIncrease
			// 
			this.butIncrease.Location = new System.Drawing.Point(6, 187);
			this.butIncrease.Name = "butIncrease";
			this.butIncrease.Size = new System.Drawing.Size(75, 24);
			this.butIncrease.TabIndex = 2;
			this.butIncrease.Text = "Increase";
			this.butIncrease.Click += new System.EventHandler(this.butIncrease_Click);
			// 
			// textPercent
			// 
			this.textPercent.Location = new System.Drawing.Point(42, 23);
			this.textPercent.Name = "textPercent";
			this.textPercent.Size = new System.Drawing.Size(46, 20);
			this.textPercent.TabIndex = 0;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(3, 24);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(38, 18);
			this.label2.TabIndex = 5;
			this.label2.Text = "%";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.label11);
			this.groupBox2.Controls.Add(this.butClear);
			this.groupBox2.Location = new System.Drawing.Point(5, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(214, 79);
			this.groupBox2.TabIndex = 4;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Clear";
			// 
			// label11
			// 
			this.label11.Location = new System.Drawing.Point(6, 16);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(200, 30);
			this.label11.TabIndex = 7;
			this.label11.Text = "Clears all values from selected fee sched for selected prov and clinic";
			// 
			// butClear
			// 
			this.butClear.Location = new System.Drawing.Point(6, 49);
			this.butClear.Name = "butClear";
			this.butClear.Size = new System.Drawing.Size(75, 24);
			this.butClear.TabIndex = 0;
			this.butClear.Text = "Clear";
			this.butClear.Click += new System.EventHandler(this.butClear_Click);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.butPickGroupTo);
			this.groupBox1.Controls.Add(this.labelGroupTo);
			this.groupBox1.Controls.Add(this.comboGroupTo);
			this.groupBox1.Controls.Add(this.comboClinicTo);
			this.groupBox1.Controls.Add(this.comboProviderTo);
			this.groupBox1.Controls.Add(this.comboFeeSchedTo);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.butPickProvTo);
			this.groupBox1.Controls.Add(this.butPickSchedTo);
			this.groupBox1.Controls.Add(this.butCopy);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.labelDateEffectiveCopy);
			this.groupBox1.Controls.Add(this.textDateEffectiveCopy);
			this.groupBox1.Controls.Add(this.labelDateEffectiveCopyNote);
			this.groupBox1.Location = new System.Drawing.Point(5, 6);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(391, 167);
			this.groupBox1.TabIndex = 1;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Copy To";
			// 
			// butPickGroupTo
			// 
			this.butPickGroupTo.Location = new System.Drawing.Point(358, 46);
			this.butPickGroupTo.Name = "butPickGroupTo";
			this.butPickGroupTo.Size = new System.Drawing.Size(23, 21);
			this.butPickGroupTo.TabIndex = 43;
			this.butPickGroupTo.Text = "...";
			this.butPickGroupTo.Visible = false;
			this.butPickGroupTo.Click += new System.EventHandler(this.butPickGroupTo_Click);
			// 
			// labelGroupTo
			// 
			this.labelGroupTo.Location = new System.Drawing.Point(31, 46);
			this.labelGroupTo.Name = "labelGroupTo";
			this.labelGroupTo.Size = new System.Drawing.Size(56, 17);
			this.labelGroupTo.TabIndex = 42;
			this.labelGroupTo.Text = "Group";
			this.labelGroupTo.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.labelGroupTo.Visible = false;
			// 
			// comboGroupTo
			// 
			this.comboGroupTo.Location = new System.Drawing.Point(-160, 47);
			this.comboGroupTo.Name = "comboGroupTo";
			this.comboGroupTo.Size = new System.Drawing.Size(173, 21);
			this.comboGroupTo.TabIndex = 42;
			this.comboGroupTo.Visible = false;
			// 
			// comboProviderTo
			// 
			this.comboProviderTo.Location = new System.Drawing.Point(88, 73);
			this.comboProviderTo.Name = "comboProviderTo";
			this.comboProviderTo.Size = new System.Drawing.Size(264, 21);
			this.comboProviderTo.TabIndex = 4;
			// 
			// comboFeeSchedTo
			// 
			this.comboFeeSchedTo.Location = new System.Drawing.Point(88, 20);
			this.comboFeeSchedTo.Name = "comboFeeSchedTo";
			this.comboFeeSchedTo.Size = new System.Drawing.Size(264, 21);
			this.comboFeeSchedTo.TabIndex = 0;
			this.comboFeeSchedTo.SelectionChangeCommitted += new System.EventHandler(this.comboFeeCombos_SelectionChangeCommitted);
			// 
			// label7
			// 
			this.label7.Location = new System.Drawing.Point(25, 75);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(62, 16);
			this.label7.TabIndex = 39;
			this.label7.Text = "Provider";
			this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// butPickProvTo
			// 
			this.butPickProvTo.Location = new System.Drawing.Point(358, 72);
			this.butPickProvTo.Name = "butPickProvTo";
			this.butPickProvTo.Size = new System.Drawing.Size(23, 21);
			this.butPickProvTo.TabIndex = 5;
			this.butPickProvTo.Text = "...";
			this.butPickProvTo.Click += new System.EventHandler(this.butPickProvider_Click);
			// 
			// butPickSchedTo
			// 
			this.butPickSchedTo.Location = new System.Drawing.Point(358, 20);
			this.butPickSchedTo.Name = "butPickSchedTo";
			this.butPickSchedTo.Size = new System.Drawing.Size(23, 21);
			this.butPickSchedTo.TabIndex = 1;
			this.butPickSchedTo.Text = "...";
			this.butPickSchedTo.Click += new System.EventHandler(this.butPickFeeSched_Click);
			// 
			// butCopy
			// 
			this.butCopy.Location = new System.Drawing.Point(88, 129);
			this.butCopy.Name = "butCopy";
			this.butCopy.Size = new System.Drawing.Size(75, 24);
			this.butCopy.TabIndex = 6;
			this.butCopy.Text = "Copy";
			this.butCopy.Click += new System.EventHandler(this.butCopy_Click);
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(2, 20);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(88, 16);
			this.label1.TabIndex = 3;
			this.label1.Text = "Fee Schedule";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelDateAsOf
			//
			this.labelDateAsOf.Location = new System.Drawing.Point(3, 131);
			this.labelDateAsOf.Name = "labelDateAsOf";
			this.labelDateAsOf.Size = new System.Drawing.Size(122, 18);
			this.labelDateAsOf.TabIndex = 41;
			this.labelDateAsOf.Text = "Base Fees Date";
			this.labelDateAsOf.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateAsOf
			//
			this.textDateAsOf.Location = new System.Drawing.Point(127, 130);
			this.textDateAsOf.Name = "textDateAsOf";
			this.textDateAsOf.Size = new System.Drawing.Size(90, 20);
			this.textDateAsOf.TabIndex = 42;
			//
			// labelDateAsOfNote
			//
			this.labelDateAsOfNote.Location = new System.Drawing.Point(221, 131);
			this.labelDateAsOfNote.Name = "labelDateAsOfNote";
			this.labelDateAsOfNote.Size = new System.Drawing.Size(165, 17);
			this.labelDateAsOfNote.TabIndex = 43;
			this.labelDateAsOfNote.Text = "increases the fees in effect then";
			this.labelDateAsOfNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelDateEffectiveCopy
			//
			this.labelDateEffectiveCopy.Location = new System.Drawing.Point(3, 101);
			this.labelDateEffectiveCopy.Name = "labelDateEffectiveCopy";
			this.labelDateEffectiveCopy.Size = new System.Drawing.Size(84, 17);
			this.labelDateEffectiveCopy.TabIndex = 44;
			this.labelDateEffectiveCopy.Text = "Date Effective";
			this.labelDateEffectiveCopy.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateEffectiveCopy
			//
			this.textDateEffectiveCopy.Location = new System.Drawing.Point(88, 100);
			this.textDateEffectiveCopy.Name = "textDateEffectiveCopy";
			this.textDateEffectiveCopy.Size = new System.Drawing.Size(90, 20);
			this.textDateEffectiveCopy.TabIndex = 45;
			//
			// labelDateEffectiveCopyNote
			//
			this.labelDateEffectiveCopyNote.Location = new System.Drawing.Point(182, 101);
			this.labelDateEffectiveCopyNote.Name = "labelDateEffectiveCopyNote";
			this.labelDateEffectiveCopyNote.Size = new System.Drawing.Size(200, 17);
			this.labelDateEffectiveCopyNote.TabIndex = 46;
			this.labelDateEffectiveCopyNote.Text = "blank = always in effect";
			this.labelDateEffectiveCopyNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// checkExportFuture
			//
			this.checkExportFuture.Location = new System.Drawing.Point(6, 52);
			this.checkExportFuture.Name = "checkExportFuture";
			this.checkExportFuture.Size = new System.Drawing.Size(160, 17);
			this.checkExportFuture.TabIndex = 47;
			this.checkExportFuture.Text = "Export includes future fees";
			//
			// labelDateEffectiveImport
			//
			this.labelDateEffectiveImport.Location = new System.Drawing.Point(180, 23);
			this.labelDateEffectiveImport.Name = "labelDateEffectiveImport";
			this.labelDateEffectiveImport.Size = new System.Drawing.Size(110, 17);
			this.labelDateEffectiveImport.TabIndex = 48;
			this.labelDateEffectiveImport.Text = "Import Date Effective";
			this.labelDateEffectiveImport.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateEffectiveImport
			//
			this.textDateEffectiveImport.Location = new System.Drawing.Point(292, 22);
			this.textDateEffectiveImport.Name = "textDateEffectiveImport";
			this.textDateEffectiveImport.Size = new System.Drawing.Size(90, 20);
			this.textDateEffectiveImport.TabIndex = 49;
			//
			// labelDateEffectiveNew
			//
			this.labelDateEffectiveNew.Location = new System.Drawing.Point(3, 157);
			this.labelDateEffectiveNew.Name = "labelDateEffectiveNew";
			this.labelDateEffectiveNew.Size = new System.Drawing.Size(122, 18);
			this.labelDateEffectiveNew.TabIndex = 50;
			this.labelDateEffectiveNew.Text = "New Effective Date";
			this.labelDateEffectiveNew.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateEffectiveNew
			//
			this.textDateEffectiveNew.Location = new System.Drawing.Point(127, 156);
			this.textDateEffectiveNew.Name = "textDateEffectiveNew";
			this.textDateEffectiveNew.Size = new System.Drawing.Size(90, 20);
			this.textDateEffectiveNew.TabIndex = 51;
			//
			// tabControlTools
			//
			this.tabControlTools.Controls.Add(this.tabPageCopy);
			this.tabControlTools.Controls.Add(this.tabPageIncrease);
			this.tabControlTools.Controls.Add(this.tabPageClear);
			this.tabControlTools.Controls.Add(this.tabPageExportImport);
			this.tabControlTools.Controls.Add(this.tabPageGlobalUpdates);
			this.tabControlTools.Controls.Add(this.tabPageCopyCode);
			this.tabControlTools.Location = new System.Drawing.Point(12, 144);
			this.tabControlTools.Name = "tabControlTools";
			this.tabControlTools.Size = new System.Drawing.Size(405, 266);
			this.tabControlTools.TabIndex = 1;
			this.tabControlTools.SelectedIndexChanged += new System.EventHandler(this.tabControlTools_SelectedIndexChanged);
			//
			// tabPageCopy
			//
			this.tabPageCopy.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(254)))));
			this.tabPageCopy.Controls.Add(this.groupBox1);
			this.tabPageCopy.Location = new System.Drawing.Point(2, 21);
			this.tabPageCopy.Name = "tabPageCopy";
			this.tabPageCopy.Size = new System.Drawing.Size(401, 243);
			this.tabPageCopy.TabIndex = 0;
			this.tabPageCopy.Text = "Copy";
			//
			// tabPageIncrease
			//
			this.tabPageIncrease.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(254)))));
			this.tabPageIncrease.Controls.Add(this.groupBox3);
			this.tabPageIncrease.Location = new System.Drawing.Point(2, 21);
			this.tabPageIncrease.Name = "tabPageIncrease";
			this.tabPageIncrease.Size = new System.Drawing.Size(401, 243);
			this.tabPageIncrease.TabIndex = 1;
			this.tabPageIncrease.Text = "Increase";
			//
			// tabPageClear
			//
			this.tabPageClear.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(254)))));
			this.tabPageClear.Controls.Add(this.groupBox2);
			this.tabPageClear.Location = new System.Drawing.Point(2, 21);
			this.tabPageClear.Name = "tabPageClear";
			this.tabPageClear.Size = new System.Drawing.Size(401, 243);
			this.tabPageClear.TabIndex = 2;
			this.tabPageClear.Text = "Clear";
			//
			// tabPageExportImport
			//
			this.tabPageExportImport.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(254)))));
			this.tabPageExportImport.Controls.Add(this.groupBox5);
			this.tabPageExportImport.Location = new System.Drawing.Point(2, 21);
			this.tabPageExportImport.Name = "tabPageExportImport";
			this.tabPageExportImport.Size = new System.Drawing.Size(401, 243);
			this.tabPageExportImport.TabIndex = 3;
			this.tabPageExportImport.Text = "Export / Import";
			//
			// tabPageGlobalUpdates
			//
			this.tabPageGlobalUpdates.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(254)))));
			this.tabPageGlobalUpdates.Controls.Add(this.groupGlobalUpdateFees);
			this.tabPageGlobalUpdates.Location = new System.Drawing.Point(2, 21);
			this.tabPageGlobalUpdates.Name = "tabPageGlobalUpdates";
			this.tabPageGlobalUpdates.Size = new System.Drawing.Size(401, 243);
			this.tabPageGlobalUpdates.TabIndex = 4;
			this.tabPageGlobalUpdates.Text = "Global Updates";
			//
			// tabPageCopyCode
			//
			this.tabPageCopyCode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(254)))));
			this.tabPageCopyCode.Controls.Add(this.groupBoxCopyCode);
			this.tabPageCopyCode.Location = new System.Drawing.Point(2, 21);
			this.tabPageCopyCode.Name = "tabPageCopyCode";
			this.tabPageCopyCode.Size = new System.Drawing.Size(401, 243);
			this.tabPageCopyCode.TabIndex = 5;
			this.tabPageCopyCode.Text = "Copy Code";
			//
			// groupBoxCopyCode
			//
			this.groupBoxCopyCode.Controls.Add(this.labelCopyCodeExplain);
			this.groupBoxCopyCode.Controls.Add(this.labelCodeFrom);
			this.groupBoxCopyCode.Controls.Add(this.textCodeFrom);
			this.groupBoxCopyCode.Controls.Add(this.butPickCodeFrom);
			this.groupBoxCopyCode.Controls.Add(this.labelCodeTo);
			this.groupBoxCopyCode.Controls.Add(this.textCodeTo);
			this.groupBoxCopyCode.Controls.Add(this.butPickCodeTo);
			this.groupBoxCopyCode.Controls.Add(this.radioCopyCodeAllScheds);
			this.groupBoxCopyCode.Controls.Add(this.radioCopyCodeOneSched);
			this.groupBoxCopyCode.Controls.Add(this.butCopyCode);
			this.groupBoxCopyCode.Location = new System.Drawing.Point(5, 6);
			this.groupBoxCopyCode.Name = "groupBoxCopyCode";
			this.groupBoxCopyCode.Size = new System.Drawing.Size(391, 222);
			this.groupBoxCopyCode.TabIndex = 0;
			this.groupBoxCopyCode.TabStop = false;
			this.groupBoxCopyCode.Text = "Copy fees from one procedure code to another";
			//
			// labelCopyCodeExplain
			//
			this.labelCopyCodeExplain.Location = new System.Drawing.Point(8, 18);
			this.labelCopyCodeExplain.Name = "labelCopyCodeExplain";
			this.labelCopyCodeExplain.Size = new System.Drawing.Size(375, 58);
			this.labelCopyCodeExplain.TabIndex = 0;
			this.labelCopyCodeExplain.Text = "In each fee schedule where the From code has fees, the To code gets the same fees for every clinic, provider and effective date, and its other fees in that fee schedule are deleted. Fee schedules where the From code has no fee are not changed.";
			//
			// labelCodeFrom
			//
			this.labelCodeFrom.Location = new System.Drawing.Point(8, 84);
			this.labelCodeFrom.Name = "labelCodeFrom";
			this.labelCodeFrom.Size = new System.Drawing.Size(90, 18);
			this.labelCodeFrom.TabIndex = 1;
			this.labelCodeFrom.Text = "From code";
			this.labelCodeFrom.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textCodeFrom
			//
			this.textCodeFrom.Location = new System.Drawing.Point(100, 83);
			this.textCodeFrom.Name = "textCodeFrom";
			this.textCodeFrom.Size = new System.Drawing.Size(100, 20);
			this.textCodeFrom.TabIndex = 2;
			//
			// butPickCodeFrom
			//
			this.butPickCodeFrom.Location = new System.Drawing.Point(203, 82);
			this.butPickCodeFrom.Name = "butPickCodeFrom";
			this.butPickCodeFrom.Size = new System.Drawing.Size(23, 21);
			this.butPickCodeFrom.TabIndex = 3;
			this.butPickCodeFrom.Text = "...";
			this.butPickCodeFrom.Click += new System.EventHandler(this.butPickCode_Click);
			//
			// labelCodeTo
			//
			this.labelCodeTo.Location = new System.Drawing.Point(8, 110);
			this.labelCodeTo.Name = "labelCodeTo";
			this.labelCodeTo.Size = new System.Drawing.Size(90, 18);
			this.labelCodeTo.TabIndex = 4;
			this.labelCodeTo.Text = "To code";
			this.labelCodeTo.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textCodeTo
			//
			this.textCodeTo.Location = new System.Drawing.Point(100, 109);
			this.textCodeTo.Name = "textCodeTo";
			this.textCodeTo.Size = new System.Drawing.Size(100, 20);
			this.textCodeTo.TabIndex = 5;
			//
			// butPickCodeTo
			//
			this.butPickCodeTo.Location = new System.Drawing.Point(203, 108);
			this.butPickCodeTo.Name = "butPickCodeTo";
			this.butPickCodeTo.Size = new System.Drawing.Size(23, 21);
			this.butPickCodeTo.TabIndex = 6;
			this.butPickCodeTo.Text = "...";
			this.butPickCodeTo.Click += new System.EventHandler(this.butPickCode_Click);
			//
			// radioCopyCodeAllScheds
			//
			this.radioCopyCodeAllScheds.Checked = true;
			this.radioCopyCodeAllScheds.Location = new System.Drawing.Point(100, 138);
			this.radioCopyCodeAllScheds.Name = "radioCopyCodeAllScheds";
			this.radioCopyCodeAllScheds.Size = new System.Drawing.Size(280, 18);
			this.radioCopyCodeAllScheds.TabIndex = 7;
			this.radioCopyCodeAllScheds.TabStop = true;
			this.radioCopyCodeAllScheds.Text = "Every fee schedule";
			//
			// radioCopyCodeOneSched
			//
			this.radioCopyCodeOneSched.Location = new System.Drawing.Point(100, 158);
			this.radioCopyCodeOneSched.Name = "radioCopyCodeOneSched";
			this.radioCopyCodeOneSched.Size = new System.Drawing.Size(280, 18);
			this.radioCopyCodeOneSched.TabIndex = 8;
			this.radioCopyCodeOneSched.Text = "Only the fee schedule under Select Fees";
			//
			// butCopyCode
			//
			this.butCopyCode.Location = new System.Drawing.Point(100, 186);
			this.butCopyCode.Name = "butCopyCode";
			this.butCopyCode.Size = new System.Drawing.Size(75, 24);
			this.butCopyCode.TabIndex = 9;
			this.butCopyCode.Text = "Copy";
			this.butCopyCode.Click += new System.EventHandler(this.butCopyCode_Click);
			//
			// FormFeeSchedTools
			//
			this.ClientSize = new System.Drawing.Size(429, 422);
			this.Controls.Add(this.checkShowGroups);
			this.Controls.Add(this.groupBox7);
			this.Controls.Add(this.tabControlTools);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormFeeSchedTools";
			this.ShowInTaskbar = false;
			this.Text = "Fee Tools";
			this.Load += new System.EventHandler(this.FormFeeSchedTools_Load);
			this.tabControlTools.ResumeLayout(false);
			this.tabPageCopy.ResumeLayout(false);
			this.tabPageIncrease.ResumeLayout(false);
			this.tabPageClear.ResumeLayout(false);
			this.tabPageExportImport.ResumeLayout(false);
			this.tabPageGlobalUpdates.ResumeLayout(false);
			this.tabPageCopyCode.ResumeLayout(false);
			this.groupBoxCopyCode.ResumeLayout(false);
			this.groupBoxCopyCode.PerformLayout();
			this.groupBox7.ResumeLayout(false);
			this.groupGlobalUpdateFees.ResumeLayout(false);
			this.groupBox5.ResumeLayout(false);
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		private OpenPmsDental.UI.GroupBox groupBox1;
		private System.Windows.Forms.Label label1;
		private OpenPmsDental.UI.Button butCopy;
		private OpenPmsDental.UI.ComboBox comboFeeSchedTo;
		private OpenPmsDental.UI.GroupBox groupBox2;
		private OpenPmsDental.UI.Button butClear;
		private OpenPmsDental.UI.GroupBox groupBox3;
		private OpenPmsDental.UI.Button butIncrease;
		private System.Windows.Forms.TextBox textPercent;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private OpenPmsDental.UI.GroupBox groupBox4;
		private System.Windows.Forms.RadioButton radioDollar;
		private System.Windows.Forms.RadioButton radioDime;
		private System.Windows.Forms.RadioButton radioPenny;
		private OpenPmsDental.UI.GroupBox groupBox5;
		private OpenPmsDental.UI.Button butExport;
		private OpenPmsDental.UI.Button butImport;
		private OpenPmsDental.UI.GroupBox groupGlobalUpdateFees;
		private Label label4;
		private OpenPmsDental.UI.Button butUpdate;
		private Label label5;
		private OpenPmsDental.UI.GroupBox groupBox7;
		private OpenPmsDental.UI.ComboBox comboProvider;
		private OpenPmsDental.UI.ComboBoxClinicPicker comboClinic;
		private OpenPmsDental.UI.ComboBox comboFeeSched;
		private Label label12;
		private Label labelGroup;
		private Label label8;
		private UI.Button butPickProv;
		private UI.Button butPickSched;
		private Label label7;
		private UI.Button butPickProvTo;
		private OpenPmsDental.UI.ComboBox comboProviderTo;
		private UI.ComboBoxClinicPicker comboClinicTo;
		private UI.Button butPickSchedTo;
		private Label label9;
		private UI.Button butUpdateWriteoffs;
		private UI.ComboBoxClinicPicker comboGlobalUpdateClinics;
		private Label label11;
		private OpenPmsDental.UI.CheckBox checkShowGroups;
		private OpenPmsDental.UI.ComboBox comboGroup;
		private OpenPmsDental.UI.ComboBox comboGroupTo;
		private Label labelGroupTo;
		private UI.Button butPickGroupTo;
		private UI.Button butPickGroup;
		private Label labelDateAsOf;
		private ValidDate textDateAsOf;
		private Label labelDateAsOfNote;
		private Label labelDateEffectiveCopy;
		private ValidDate textDateEffectiveCopy;
		private Label labelDateEffectiveCopyNote;
		private OpenPmsDental.UI.CheckBox checkExportFuture;
		private Label labelDateEffectiveImport;
		private ValidDate textDateEffectiveImport;
		private Label labelDateEffectiveNew;
		private ValidDate textDateEffectiveNew;
		private OpenPmsDental.UI.TabControl tabControlTools;
		private OpenPmsDental.UI.TabPage tabPageCopy;
		private OpenPmsDental.UI.TabPage tabPageIncrease;
		private OpenPmsDental.UI.TabPage tabPageClear;
		private OpenPmsDental.UI.TabPage tabPageExportImport;
		private OpenPmsDental.UI.TabPage tabPageGlobalUpdates;
		private OpenPmsDental.UI.TabPage tabPageCopyCode;
		private OpenPmsDental.UI.GroupBox groupBoxCopyCode;
		private System.Windows.Forms.Label labelCopyCodeExplain;
		private System.Windows.Forms.Label labelCodeFrom;
		private System.Windows.Forms.TextBox textCodeFrom;
		private OpenPmsDental.UI.Button butPickCodeFrom;
		private System.Windows.Forms.Label labelCodeTo;
		private System.Windows.Forms.TextBox textCodeTo;
		private OpenPmsDental.UI.Button butPickCodeTo;
		private System.Windows.Forms.RadioButton radioCopyCodeAllScheds;
		private System.Windows.Forms.RadioButton radioCopyCodeOneSched;
		private OpenPmsDental.UI.Button butCopyCode;
	}
}
