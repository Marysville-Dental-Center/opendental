using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenDental {
	public partial class FormTimeCard {
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
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormTimeCard));
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.timerUpdateBreak = new System.Windows.Forms.Timer(this.components);
			this.groupBox1 = new OpenDental.UI.GroupBox();
			this.labelNote = new System.Windows.Forms.Label();
			this.textNote = new System.Windows.Forms.TextBox();
			this.textDatePaycheck = new System.Windows.Forms.TextBox();
			this.textDateStop = new System.Windows.Forms.TextBox();
			this.textDateStart = new System.Windows.Forms.TextBox();
			this.butRight = new OpenDental.UI.Button();
			this.butLeft = new OpenDental.UI.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.groupBox2 = new OpenDental.UI.GroupBox();
			this.radioBreaks = new System.Windows.Forms.RadioButton();
			this.radioTimeCard = new System.Windows.Forms.RadioButton();
			this.gridMain = new OpenDental.UI.GridOD();
			this.butCalcDaily = new OpenDental.UI.Button();
			this.butPrint = new OpenDental.UI.Button();
			this.butCalcWeekOT = new OpenDental.UI.Button();
			this.butAdj = new OpenDental.UI.Button();
			this.butSave = new OpenDental.UI.Button();
			this.groupEmployee = new OpenDental.UI.GroupBox();
			this.butPrevEmp = new OpenDental.UI.Button();
			this.butNextEmp = new OpenDental.UI.Button();
			this.gridTotals = new OpenDental.UI.GridOD();
			this.groupTotalsFormat = new OpenDental.UI.GroupBox();
			this.radioTotalsDecimal = new System.Windows.Forms.RadioButton();
			this.radioTotalsColon = new System.Windows.Forms.RadioButton();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupEmployee.SuspendLayout();
			this.groupTotalsFormat.SuspendLayout();
			this.SuspendLayout();
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(107, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(73, 18);
			this.label2.TabIndex = 6;
			this.label2.Text = "Start Date";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(110, 28);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(70, 18);
			this.label3.TabIndex = 8;
			this.label3.Text = "End Date";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// timerUpdateBreak
			// 
			this.timerUpdateBreak.Enabled = true;
			this.timerUpdateBreak.Interval = 1000;
			this.timerUpdateBreak.Tick += new System.EventHandler(this.timerUpdateBreak_Tick);
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this.labelNote);
			this.groupBox1.Controls.Add(this.textNote);
			this.groupBox1.Controls.Add(this.textDatePaycheck);
			this.groupBox1.Controls.Add(this.textDateStop);
			this.groupBox1.Controls.Add(this.textDateStart);
			this.groupBox1.Controls.Add(this.butRight);
			this.groupBox1.Controls.Add(this.butLeft);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Location = new System.Drawing.Point(18, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(646, 71);
			this.groupBox1.TabIndex = 14;
			this.groupBox1.Text = "Pay Period";
			// 
			// labelNote
			// 
			this.labelNote.Location = new System.Drawing.Point(284, 28);
			this.labelNote.Name = "labelNote";
			this.labelNote.Size = new System.Drawing.Size(46, 18);
			this.labelNote.TabIndex = 16;
			this.labelNote.Text = "Note";
			this.labelNote.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textNote
			// 
			this.textNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textNote.Location = new System.Drawing.Point(331, 28);
			this.textNote.Multiline = true;
			this.textNote.Name = "textNote";
			this.textNote.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textNote.Size = new System.Drawing.Size(309, 40);
			this.textNote.TabIndex = 15;
			// 
			// textDatePaycheck
			// 
			this.textDatePaycheck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.textDatePaycheck.Location = new System.Drawing.Point(540, 8);
			this.textDatePaycheck.Name = "textDatePaycheck";
			this.textDatePaycheck.ReadOnly = true;
			this.textDatePaycheck.Size = new System.Drawing.Size(100, 20);
			this.textDatePaycheck.TabIndex = 14;
			// 
			// textDateStop
			// 
			this.textDateStop.Location = new System.Drawing.Point(181, 28);
			this.textDateStop.Name = "textDateStop";
			this.textDateStop.ReadOnly = true;
			this.textDateStop.Size = new System.Drawing.Size(100, 20);
			this.textDateStop.TabIndex = 13;
			// 
			// textDateStart
			// 
			this.textDateStart.Location = new System.Drawing.Point(181, 8);
			this.textDateStart.Name = "textDateStart";
			this.textDateStart.ReadOnly = true;
			this.textDateStart.Size = new System.Drawing.Size(100, 20);
			this.textDateStart.TabIndex = 12;
			// 
			// butRight
			// 
			this.butRight.Image = ((System.Drawing.Image)(resources.GetObject("butRight.Image")));
			this.butRight.Location = new System.Drawing.Point(63, 18);
			this.butRight.Name = "butRight";
			this.butRight.Size = new System.Drawing.Size(39, 24);
			this.butRight.TabIndex = 11;
			this.butRight.Click += new System.EventHandler(this.butRight_Click);
			// 
			// butLeft
			// 
			this.butLeft.Image = ((System.Drawing.Image)(resources.GetObject("butLeft.Image")));
			this.butLeft.Location = new System.Drawing.Point(13, 18);
			this.butLeft.Name = "butLeft";
			this.butLeft.Size = new System.Drawing.Size(39, 24);
			this.butLeft.TabIndex = 10;
			this.butLeft.Click += new System.EventHandler(this.butLeft_Click);
			// 
			// label4
			// 
			this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.label4.Location = new System.Drawing.Point(422, 8);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(117, 18);
			this.label4.TabIndex = 9;
			this.label4.Text = "Paycheck Date";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.radioBreaks);
			this.groupBox2.Controls.Add(this.radioTimeCard);
			this.groupBox2.Location = new System.Drawing.Point(816, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(140, 71);
			this.groupBox2.TabIndex = 20;
			// 
			// radioBreaks
			// 
			this.radioBreaks.Location = new System.Drawing.Point(34, 27);
			this.radioBreaks.Name = "radioBreaks";
			this.radioBreaks.Size = new System.Drawing.Size(97, 19);
			this.radioBreaks.TabIndex = 1;
			this.radioBreaks.Text = "Breaks";
			this.radioBreaks.UseVisualStyleBackColor = true;
			this.radioBreaks.Click += new System.EventHandler(this.radioBreaks_Click);
			// 
			// radioTimeCard
			// 
			this.radioTimeCard.Checked = true;
			this.radioTimeCard.Location = new System.Drawing.Point(34, 10);
			this.radioTimeCard.Name = "radioTimeCard";
			this.radioTimeCard.Size = new System.Drawing.Size(97, 19);
			this.radioTimeCard.TabIndex = 0;
			this.radioTimeCard.TabStop = true;
			this.radioTimeCard.Text = "Time Card";
			this.radioTimeCard.UseVisualStyleBackColor = true;
			this.radioTimeCard.Click += new System.EventHandler(this.radioTimeCard_Click);
			// 
			// gridMain
			// 
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(18, 80);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(939, 488);
			this.gridMain.TabIndex = 13;
			this.gridMain.Title = "Time Card";
			this.gridMain.TranslationName = "TableTimeCard";
			this.gridMain.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			// 
			// butCalcDaily
			// 
			this.butCalcDaily.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butCalcDaily.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butCalcDaily.Location = new System.Drawing.Point(139, 633);
			this.butCalcDaily.Name = "butCalcDaily";
			this.butCalcDaily.Size = new System.Drawing.Size(90, 24);
			this.butCalcDaily.TabIndex = 23;
			this.butCalcDaily.Text = "Calc Daily";
			this.butCalcDaily.Click += new System.EventHandler(this.butCalcDaily_Click);
			// 
			// butPrint
			// 
			this.butPrint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butPrint.Image = ((System.Drawing.Image)(resources.GetObject("butPrint.Image")));
			this.butPrint.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butPrint.Location = new System.Drawing.Point(779, 662);
			this.butPrint.Name = "butPrint";
			this.butPrint.Size = new System.Drawing.Size(86, 24);
			this.butPrint.TabIndex = 19;
			this.butPrint.Text = "Print";
			this.butPrint.Click += new System.EventHandler(this.butPrint_Click);
			// 
			// butCalcWeekOT
			// 
			this.butCalcWeekOT.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butCalcWeekOT.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butCalcWeekOT.Location = new System.Drawing.Point(139, 662);
			this.butCalcWeekOT.Name = "butCalcWeekOT";
			this.butCalcWeekOT.Size = new System.Drawing.Size(90, 24);
			this.butCalcWeekOT.TabIndex = 18;
			this.butCalcWeekOT.Text = "Calc Week OT";
			this.butCalcWeekOT.Click += new System.EventHandler(this.butCalcWeekOT_Click);
			// 
			// butAdj
			// 
			this.butAdj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdj.Icon = OpenDental.UI.EnumIcons.Add;
			this.butAdj.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdj.Location = new System.Drawing.Point(18, 662);
			this.butAdj.Name = "butAdj";
			this.butAdj.Size = new System.Drawing.Size(115, 24);
			this.butAdj.TabIndex = 15;
			this.butAdj.Text = "Add Adjustment";
			this.butAdj.Click += new System.EventHandler(this.butAdj_Click);
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.butSave.Location = new System.Drawing.Point(882, 662);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 0;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// groupEmployee
			// 
			this.groupEmployee.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.groupEmployee.Controls.Add(this.butPrevEmp);
			this.groupEmployee.Controls.Add(this.butNextEmp);
			this.groupEmployee.Location = new System.Drawing.Point(670, 3);
			this.groupEmployee.Name = "groupEmployee";
			this.groupEmployee.Size = new System.Drawing.Size(140, 71);
			this.groupEmployee.TabIndex = 121;
			this.groupEmployee.Text = "Employee";
			this.groupEmployee.Visible = false;
			// 
			// butPrevEmp
			// 
			this.butPrevEmp.Image = ((System.Drawing.Image)(resources.GetObject("butPrevEmp.Image")));
			this.butPrevEmp.Location = new System.Drawing.Point(23, 18);
			this.butPrevEmp.Name = "butPrevEmp";
			this.butPrevEmp.Size = new System.Drawing.Size(39, 24);
			this.butPrevEmp.TabIndex = 127;
			this.butPrevEmp.Click += new System.EventHandler(this.butBrowseEmp_Click);
			// 
			// butNextEmp
			// 
			this.butNextEmp.Image = ((System.Drawing.Image)(resources.GetObject("butNextEmp.Image")));
			this.butNextEmp.Location = new System.Drawing.Point(74, 18);
			this.butNextEmp.Name = "butNextEmp";
			this.butNextEmp.Size = new System.Drawing.Size(39, 24);
			this.butNextEmp.TabIndex = 128;
			this.butNextEmp.Click += new System.EventHandler(this.butBrowseEmp_Click);
			//
			// gridTotals
			//
			this.gridTotals.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.gridTotals.Location = new System.Drawing.Point(18, 572);
			this.gridTotals.Name = "gridTotals";
			this.gridTotals.SelectionMode = OpenDental.UI.GridSelectionMode.None;
			this.gridTotals.Size = new System.Drawing.Size(630, 58);
			this.gridTotals.TabIndex = 131;
			this.gridTotals.Title = "Totals for Period";
			this.gridTotals.TranslationName = "TableTimeCardTotals";
			//
			// groupTotalsFormat
			//
			this.groupTotalsFormat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.groupTotalsFormat.Controls.Add(this.radioTotalsDecimal);
			this.groupTotalsFormat.Controls.Add(this.radioTotalsColon);
			this.groupTotalsFormat.Location = new System.Drawing.Point(654, 572);
			this.groupTotalsFormat.Name = "groupTotalsFormat";
			this.groupTotalsFormat.Size = new System.Drawing.Size(120, 58);
			this.groupTotalsFormat.TabIndex = 132;
			//
			// radioTotalsDecimal
			//
			this.radioTotalsDecimal.Location = new System.Drawing.Point(10, 12);
			this.radioTotalsDecimal.Name = "radioTotalsDecimal";
			this.radioTotalsDecimal.Size = new System.Drawing.Size(105, 19);
			this.radioTotalsDecimal.TabIndex = 0;
			this.radioTotalsDecimal.Text = "Decimal Format";
			this.radioTotalsDecimal.UseVisualStyleBackColor = true;
			this.radioTotalsDecimal.Click += new System.EventHandler(this.radioTotalsFormat_Click);
			//
			// radioTotalsColon
			//
			this.radioTotalsColon.Location = new System.Drawing.Point(10, 32);
			this.radioTotalsColon.Name = "radioTotalsColon";
			this.radioTotalsColon.Size = new System.Drawing.Size(105, 19);
			this.radioTotalsColon.TabIndex = 1;
			this.radioTotalsColon.Text = "Colon Format";
			this.radioTotalsColon.UseVisualStyleBackColor = true;
			this.radioTotalsColon.Click += new System.EventHandler(this.radioTotalsFormat_Click);
			//
			// FormTimeCard
			//
			this.CancelButton = this.butSave;
			this.ClientSize = new System.Drawing.Size(979, 696);
			this.Controls.Add(this.groupTotalsFormat);
			this.Controls.Add(this.gridTotals);
			this.Controls.Add(this.groupEmployee);
			this.Controls.Add(this.butCalcDaily);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.butPrint);
			this.Controls.Add(this.butCalcWeekOT);
			this.Controls.Add(this.butAdj);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butSave);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormTimeCard";
			this.ShowInTaskbar = false;
			this.Text = "Time Card";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormTimeCard_FormClosing);
			this.Load += new System.EventHandler(this.FormTimeCard_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupEmployee.ResumeLayout(false);
			this.groupTotalsFormat.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private OpenDental.UI.Button butSave;
		private System.Windows.Forms.Timer timerUpdateBreak;
		private OpenDental.UI.GridOD gridMain;
		private OpenDental.UI.GroupBox groupBox1;
		private OpenDental.UI.Button butRight;
		private OpenDental.UI.Button butLeft;
		private Label label4;
		private TextBox textDateStart;
		private TextBox textDatePaycheck;
		private TextBox textDateStop;
		private OpenDental.UI.Button butAdj;
		private OpenDental.UI.Button butCalcWeekOT;
		private OpenDental.UI.Button butPrint;
		private OpenDental.UI.GroupBox groupBox2;
		private RadioButton radioBreaks;
		private RadioButton radioTimeCard;
		private OpenDental.UI.Button butCalcDaily;
		private OpenDental.UI.GroupBox groupEmployee;
		private UI.Button butPrevEmp;
		private UI.Button butNextEmp;
		private Label labelNote;
		private TextBox textNote;
		private OpenDental.UI.GridOD gridTotals;
		private OpenDental.UI.GroupBox groupTotalsFormat;
		private RadioButton radioTotalsDecimal;
		private RadioButton radioTotalsColon;
	}
}
