using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormGraphics {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormGraphics));
			this.checkHardwareAccel = new OpenPmsDental.UI.CheckBox();
			this.checkDoubleBuffering = new OpenPmsDental.UI.CheckBox();
			this.group3DToothChart = new OpenPmsDental.UI.GroupBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textSelected = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3DChart = new System.Windows.Forms.Label();
			this.groupFilters = new OpenPmsDental.UI.GroupBox();
			this.checkAllFormats = new OpenPmsDental.UI.CheckBox();
			this.gridFormats = new OpenPmsDental.UI.GridOD();
			this.radioSimpleChart = new System.Windows.Forms.RadioButton();
			this.radioOpenGLChart = new System.Windows.Forms.RadioButton();
			this.radioDirectXChart = new System.Windows.Forms.RadioButton();
			this.butSave = new OpenPmsDental.UI.Button();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.group3DToothChart.SuspendLayout();
			this.groupFilters.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// checkHardwareAccel
			// 
			this.checkHardwareAccel.Location = new System.Drawing.Point(6, 15);
			this.checkHardwareAccel.Name = "checkHardwareAccel";
			this.checkHardwareAccel.Size = new System.Drawing.Size(282, 18);
			this.checkHardwareAccel.TabIndex = 2;
			this.checkHardwareAccel.Text = "Hardware Acceleration (checked by default)";
			this.checkHardwareAccel.Click += new System.EventHandler(this.checkHardwareAccel_Click);
			// 
			// checkDoubleBuffering
			// 
			this.checkDoubleBuffering.Location = new System.Drawing.Point(6, 32);
			this.checkDoubleBuffering.Name = "checkDoubleBuffering";
			this.checkDoubleBuffering.Size = new System.Drawing.Size(282, 17);
			this.checkDoubleBuffering.TabIndex = 4;
			this.checkDoubleBuffering.Text = "Use Double-Buffering";
			this.checkDoubleBuffering.Click += new System.EventHandler(this.checkDoubleBuffering_Click);
			// 
			// group3DToothChart
			// 
			this.group3DToothChart.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.group3DToothChart.Controls.Add(this.label4);
			this.group3DToothChart.Controls.Add(this.textSelected);
			this.group3DToothChart.Controls.Add(this.label3);
			this.group3DToothChart.Controls.Add(this.label2);
			this.group3DToothChart.Controls.Add(this.label3DChart);
			this.group3DToothChart.Controls.Add(this.groupFilters);
			this.group3DToothChart.Controls.Add(this.gridFormats);
			this.group3DToothChart.Location = new System.Drawing.Point(28, 288);
			this.group3DToothChart.Name = "group3DToothChart";
			this.group3DToothChart.Size = new System.Drawing.Size(833, 352);
			this.group3DToothChart.TabIndex = 5;
			this.group3DToothChart.TabStop = false;
			this.group3DToothChart.Text = "Options For Older Technology";
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(60, 164);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(608, 16);
			this.label4.TabIndex = 15;
			this.label4.Text = " Formats are listed from most recommended on top to least recommended on bottom.";
			// 
			// textSelected
			// 
			this.textSelected.Location = new System.Drawing.Point(6, 161);
			this.textSelected.Name = "textSelected";
			this.textSelected.ReadOnly = true;
			this.textSelected.Size = new System.Drawing.Size(53, 20);
			this.textSelected.TabIndex = 14;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(3, 143);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(159, 16);
			this.label3.TabIndex = 13;
			this.label3.Text = "Currently selected format number";
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(6, 111);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(818, 31);
			this.label2.TabIndex = 12;
			this.label2.Text = resources.GetString("label2.Text");
			// 
			// label3DChart
			// 
			this.label3DChart.Location = new System.Drawing.Point(9, 18);
			this.label3DChart.Name = "label3DChart";
			this.label3DChart.Size = new System.Drawing.Size(818, 20);
			this.label3DChart.TabIndex = 11;
			this.label3DChart.Text = "Most users will never need to change any of these options.  These are only used w" +
    "hen the 3D tooth chart is not working properly.";
			// 
			// groupFilters
			// 
			this.groupFilters.Controls.Add(this.checkHardwareAccel);
			this.groupFilters.Controls.Add(this.checkDoubleBuffering);
			this.groupFilters.Controls.Add(this.checkAllFormats);
			this.groupFilters.Location = new System.Drawing.Point(6, 39);
			this.groupFilters.Name = "groupFilters";
			this.groupFilters.Size = new System.Drawing.Size(295, 68);
			this.groupFilters.TabIndex = 10;
			this.groupFilters.TabStop = false;
			this.groupFilters.Text = "OpenGL filters for list below";
			// 
			// checkAllFormats
			// 
			this.checkAllFormats.Location = new System.Drawing.Point(6, 48);
			this.checkAllFormats.Name = "checkAllFormats";
			this.checkAllFormats.Size = new System.Drawing.Size(282, 17);
			this.checkAllFormats.TabIndex = 9;
			this.checkAllFormats.Text = "Show All Formats";
			this.checkAllFormats.Click += new System.EventHandler(this.checkAllFormats_Click);
			// 
			// gridFormats
			// 
			this.gridFormats.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.gridFormats.Location = new System.Drawing.Point(6, 184);
			this.gridFormats.Name = "gridFormats";
			this.gridFormats.Size = new System.Drawing.Size(821, 162);
			this.gridFormats.TabIndex = 8;
			this.gridFormats.Title = "Available Graphics Formats";
			this.gridFormats.TranslationName = "TaleFormats";
			this.gridFormats.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridFormats_CellClick);
			// 
			// radioSimpleChart
			// 
			this.radioSimpleChart.Location = new System.Drawing.Point(15, 35);
			this.radioSimpleChart.Name = "radioSimpleChart";
			this.radioSimpleChart.Size = new System.Drawing.Size(146, 19);
			this.radioSimpleChart.TabIndex = 6;
			this.radioSimpleChart.TabStop = true;
			this.radioSimpleChart.Text = "Simple 2D (pie shapes)";
			this.radioSimpleChart.UseVisualStyleBackColor = true;
			this.radioSimpleChart.Click += new System.EventHandler(this.radioSimpleChart_Click);
			// 
			// radioOpenGLChart
			// 
			this.radioOpenGLChart.Location = new System.Drawing.Point(15, 52);
			this.radioOpenGLChart.Name = "radioOpenGLChart";
			this.radioOpenGLChart.Size = new System.Drawing.Size(147, 19);
			this.radioOpenGLChart.TabIndex = 7;
			this.radioOpenGLChart.TabStop = true;
			this.radioOpenGLChart.Text = "OpenGL (no perio)";
			this.radioOpenGLChart.UseVisualStyleBackColor = true;
			this.radioOpenGLChart.Click += new System.EventHandler(this.radioOpenGLChart_Click);
			// 
			// radioDirectXChart
			// 
			this.radioDirectXChart.Location = new System.Drawing.Point(15, 17);
			this.radioDirectXChart.Name = "radioDirectXChart";
			this.radioDirectXChart.Size = new System.Drawing.Size(215, 19);
			this.radioDirectXChart.TabIndex = 8;
			this.radioDirectXChart.TabStop = true;
			this.radioDirectXChart.Text = "DirectX 9 (needs the DirectX End-User Runtime)";
			this.radioDirectXChart.UseVisualStyleBackColor = true;
			this.radioDirectXChart.Click += new System.EventHandler(this.radioDirectXChart_Click);
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(786, 646);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 26);
			this.butSave.TabIndex = 1;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.radioDirectXChart);
			this.groupBox1.Controls.Add(this.radioSimpleChart);
			this.groupBox1.Controls.Add(this.radioOpenGLChart);
			this.groupBox1.Location = new System.Drawing.Point(28, 209);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(257, 75);
			this.groupBox1.TabIndex = 11;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Tooth Chart Technology";
			// 
			// FormGraphics
			// 
			this.ClientSize = new System.Drawing.Size(892, 683);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.group3DToothChart);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormGraphics";
			this.ShowInTaskbar = false;
			this.Text = "Graphics Preferences";
			this.Load += new System.EventHandler(this.FormGraphics_Load);
			this.group3DToothChart.ResumeLayout(false);
			this.group3DToothChart.PerformLayout();
			this.groupFilters.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion
		private OpenPmsDental.UI.Button butSave;
		private OpenPmsDental.UI.CheckBox checkHardwareAccel;
		private OpenPmsDental.UI.CheckBox checkDoubleBuffering;
		private OpenPmsDental.UI.GroupBox group3DToothChart;
		private OpenPmsDental.UI.GridOD gridFormats;
		private OpenPmsDental.UI.CheckBox checkAllFormats;
		private RadioButton radioSimpleChart;
		private RadioButton radioOpenGLChart;
		private OpenPmsDental.UI.GroupBox groupFilters;
		private Label label3DChart;
		private Label label2;
		private Label label4;
		private TextBox textSelected;
		private Label label3;
		private RadioButton radioDirectXChart;
		private OpenPmsDental.UI.GroupBox groupBox1;
	}
}
