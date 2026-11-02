using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormImagingSetup {
		private System.ComponentModel.IContainer components = null;

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
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.label12 = new System.Windows.Forms.Label();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.radioScanDocUseOptionsBelow = new System.Windows.Forms.RadioButton();
			this.radioScanDocShowOptions = new System.Windows.Forms.RadioButton();
			this.groupScanningOptions = new OpenPmsDental.UI.GroupBox();
			this.label8 = new System.Windows.Forms.Label();
			this.checkScanDocDuplex = new OpenPmsDental.UI.CheckBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.textScanDocResolution = new OpenPmsDental.ValidNum();
			this.checkScanDocGrayscale = new OpenPmsDental.UI.CheckBox();
			this.checkScanDocSelectSource = new OpenPmsDental.UI.CheckBox();
			this.butScanDocSelectSourceDetails = new OpenPmsDental.UI.Button();
			this.label7 = new System.Windows.Forms.Label();
			this.textScanDocQuality = new OpenPmsDental.ValidNum();
			this.groupBox2 = new OpenPmsDental.UI.GroupBox();
			this.label1 = new System.Windows.Forms.Label();
			this.labelPanoBW = new System.Windows.Forms.Label();
			this.slider = new OpenPmsDental.UI.WindowingSlider();
			this.groupBox3 = new OpenPmsDental.UI.GroupBox();
			this.label37 = new System.Windows.Forms.Label();
			this.butSetScanner = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.groupBox1.SuspendLayout();
			this.groupScanningOptions.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.SuspendLayout();
			// 
			// label12
			// 
			this.label12.Location = new System.Drawing.Point(17, 42);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(254, 15);
			this.label12.TabIndex = 14;
			this.label12.Text = "JPEG Compression - Quality After Scanning";
			this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.radioScanDocUseOptionsBelow);
			this.groupBox1.Controls.Add(this.radioScanDocShowOptions);
			this.groupBox1.Controls.Add(this.groupScanningOptions);
			this.groupBox1.Controls.Add(this.checkScanDocSelectSource);
			this.groupBox1.Controls.Add(this.butScanDocSelectSourceDetails);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textScanDocQuality);
			this.groupBox1.Controls.Add(this.label12);
			this.groupBox1.Location = new System.Drawing.Point(20, 52);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(622, 197);
			this.groupBox1.TabIndex = 17;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Documents - Settings apply only to this workstation";
			// 
			// radioScanDocUseOptionsBelow
			// 
			this.radioScanDocUseOptionsBelow.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.radioScanDocUseOptionsBelow.Location = new System.Drawing.Point(85, 81);
			this.radioScanDocUseOptionsBelow.Name = "radioScanDocUseOptionsBelow";
			this.radioScanDocUseOptionsBelow.Size = new System.Drawing.Size(202, 18);
			this.radioScanDocUseOptionsBelow.TabIndex = 26;
			this.radioScanDocUseOptionsBelow.TabStop = true;
			this.radioScanDocUseOptionsBelow.Text = "Use the Options Below";
			this.radioScanDocUseOptionsBelow.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.radioScanDocUseOptionsBelow.UseVisualStyleBackColor = true;
			// 
			// radioScanDocShowOptions
			// 
			this.radioScanDocShowOptions.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.radioScanDocShowOptions.Location = new System.Drawing.Point(67, 63);
			this.radioScanDocShowOptions.Name = "radioScanDocShowOptions";
			this.radioScanDocShowOptions.Size = new System.Drawing.Size(220, 18);
			this.radioScanDocShowOptions.TabIndex = 25;
			this.radioScanDocShowOptions.TabStop = true;
			this.radioScanDocShowOptions.Text = "Show Scanner Options Window";
			this.radioScanDocShowOptions.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.radioScanDocShowOptions.UseVisualStyleBackColor = true;
			this.radioScanDocShowOptions.CheckedChanged += new System.EventHandler(this.radioScanDocShowOptions_CheckedChanged);
			// 
			// groupScanningOptions
			// 
			this.groupScanningOptions.Controls.Add(this.label8);
			this.groupScanningOptions.Controls.Add(this.checkScanDocDuplex);
			this.groupScanningOptions.Controls.Add(this.label5);
			this.groupScanningOptions.Controls.Add(this.label6);
			this.groupScanningOptions.Controls.Add(this.textScanDocResolution);
			this.groupScanningOptions.Controls.Add(this.checkScanDocGrayscale);
			this.groupScanningOptions.Location = new System.Drawing.Point(32, 100);
			this.groupScanningOptions.Name = "groupScanningOptions";
			this.groupScanningOptions.Size = new System.Drawing.Size(584, 90);
			this.groupScanningOptions.TabIndex = 24;
			this.groupScanningOptions.TabStop = false;
			this.groupScanningOptions.Text = "Scanning Options";
			// 
			// label8
			// 
			this.label8.Location = new System.Drawing.Point(260, 9);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(260, 34);
			this.label8.TabIndex = 23;
			this.label8.Text = "If this setting causes your scanner to malfunction, use the \"Show Scanner Options" +
    " Window\" instead";
			this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// checkScanDocDuplex
			// 
			this.checkScanDocDuplex.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkScanDocDuplex.Location = new System.Drawing.Point(9, 19);
			this.checkScanDocDuplex.Name = "checkScanDocDuplex";
			this.checkScanDocDuplex.Size = new System.Drawing.Size(247, 18);
			this.checkScanDocDuplex.TabIndex = 21;
			this.checkScanDocDuplex.Text = "Multipage Scans Duplex (both sides)";
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(52, 66);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(186, 19);
			this.label5.TabIndex = 15;
			this.label5.Text = "Resolution";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label6
			// 
			this.label6.Location = new System.Drawing.Point(313, 65);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(180, 19);
			this.label6.TabIndex = 15;
			this.label6.Text = "> 50. Typical setting: 150 dpi";
			this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// textScanDocResolution
			// 
			this.textScanDocResolution.Location = new System.Drawing.Point(241, 65);
			this.textScanDocResolution.MaxVal = 1000;
			this.textScanDocResolution.MinVal = 51;
			this.textScanDocResolution.Name = "textScanDocResolution";
			this.textScanDocResolution.Size = new System.Drawing.Size(68, 20);
			this.textScanDocResolution.TabIndex = 20;
			// 
			// checkScanDocGrayscale
			// 
			this.checkScanDocGrayscale.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkScanDocGrayscale.Location = new System.Drawing.Point(110, 47);
			this.checkScanDocGrayscale.Name = "checkScanDocGrayscale";
			this.checkScanDocGrayscale.Size = new System.Drawing.Size(146, 18);
			this.checkScanDocGrayscale.TabIndex = 22;
			this.checkScanDocGrayscale.Text = "Grayscale";
			// 
			// checkScanDocSelectSource
			// 
			this.checkScanDocSelectSource.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkScanDocSelectSource.Location = new System.Drawing.Point(32, 19);
			this.checkScanDocSelectSource.Name = "checkScanDocSelectSource";
			this.checkScanDocSelectSource.Size = new System.Drawing.Size(256, 18);
			this.checkScanDocSelectSource.TabIndex = 23;
			this.checkScanDocSelectSource.Text = "Show Select Scanner Window";
			// 
			// butScanDocSelectSourceDetails
			// 
			this.butScanDocSelectSourceDetails.Location = new System.Drawing.Point(294, 17);
			this.butScanDocSelectSourceDetails.Name = "butScanDocSelectSourceDetails";
			this.butScanDocSelectSourceDetails.Size = new System.Drawing.Size(60, 22);
			this.butScanDocSelectSourceDetails.TabIndex = 27;
			this.butScanDocSelectSourceDetails.Text = "Details";
			this.butScanDocSelectSourceDetails.Click += new System.EventHandler(this.butScanDocSelectSourceDetails_Click);
			// 
			// label7
			// 
			this.label7.Location = new System.Drawing.Point(345, 42);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(274, 15);
			this.label7.TabIndex = 23;
			this.label7.Text = "0-100. 100=No compression.  Typical setting: 40";
			this.label7.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// textScanDocQuality
			// 
			this.textScanDocQuality.Location = new System.Drawing.Point(273, 40);
			this.textScanDocQuality.MaxVal = 100;
			this.textScanDocQuality.MinVal = 0;
			this.textScanDocQuality.Name = "textScanDocQuality";
			this.textScanDocQuality.Size = new System.Drawing.Size(68, 20);
			this.textScanDocQuality.TabIndex = 20;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this.labelPanoBW);
			this.groupBox2.Controls.Add(this.slider);
			this.groupBox2.Location = new System.Drawing.Point(20, 258);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(484, 96);
			this.groupBox2.TabIndex = 18;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Radiographs";
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(13, 14);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(425, 19);
			this.label1.TabIndex = 22;
			this.label1.Text = "Default pixel windowing for new radiographs, both scanned and digital";
			this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// labelPanoBW
			// 
			this.labelPanoBW.Location = new System.Drawing.Point(13, 65);
			this.labelPanoBW.Name = "labelPanoBW";
			this.labelPanoBW.Size = new System.Drawing.Size(465, 24);
			this.labelPanoBW.TabIndex = 15;
			this.labelPanoBW.Text = "Suggested setting for scanning panos is Greyscale, 300 dpi.  For BWs, 400dpi.";
			// 
			// slider
			// 
			this.slider.Location = new System.Drawing.Point(14, 38);
			this.slider.MaxVal = 128;
			this.slider.Name = "slider";
			this.slider.Size = new System.Drawing.Size(194, 14);
			this.slider.TabIndex = 21;
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.label37);
			this.groupBox3.Location = new System.Drawing.Point(20, 360);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(484, 43);
			this.groupBox3.TabIndex = 19;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Photos";
			// 
			// label37
			// 
			this.label37.Location = new System.Drawing.Point(14, 18);
			this.label37.Name = "label37";
			this.label37.Size = new System.Drawing.Size(464, 20);
			this.label37.TabIndex = 15;
			this.label37.Text = "Suggested setting for scanning photos is Color, 300 dpi.";
			// 
			// butSetScanner
			// 
			this.butSetScanner.Location = new System.Drawing.Point(21, 12);
			this.butSetScanner.Name = "butSetScanner";
			this.butSetScanner.Size = new System.Drawing.Size(110, 24);
			this.butSetScanner.TabIndex = 22;
			this.butSetScanner.Text = "Set Default Scanner";
			this.butSetScanner.Click += new System.EventHandler(this.butSetScanner_Click);
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(562, 519);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 2;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// FormImagingSetup
			// 
			this.ClientSize = new System.Drawing.Size(649, 555);
			this.Controls.Add(this.butSetScanner);
			this.Controls.Add(this.groupBox3);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.butSave);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormImagingSetup";
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.Text = "Scanning Setup";
			this.Load += new System.EventHandler(this.FormImagingSetup_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupScanningOptions.ResumeLayout(false);
			this.groupScanningOptions.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox3.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		private OpenPmsDental.UI.Button butSave;
		private System.Windows.Forms.Label label12;
		private OpenPmsDental.UI.GroupBox groupBox1;
		private OpenPmsDental.UI.GroupBox groupBox2;
		private System.Windows.Forms.Label labelPanoBW;
		private OpenPmsDental.UI.GroupBox groupBox3;
		private System.Windows.Forms.Label label37;
		private UI.WindowingSlider slider;
		private Label label1;
		private ValidNum textScanDocQuality;
		private ValidNum textScanDocResolution;
		private Label label6;
		private Label label5;
		private UI.Button butSetScanner;
		private OpenPmsDental.UI.CheckBox checkScanDocSelectSource;
		private OpenPmsDental.UI.Button butScanDocSelectSourceDetails;
		private OpenPmsDental.UI.CheckBox checkScanDocDuplex;
		private Label label7;
		private OpenPmsDental.UI.CheckBox checkScanDocGrayscale;
		private RadioButton radioScanDocUseOptionsBelow;
		private RadioButton radioScanDocShowOptions;
		private OpenPmsDental.UI.GroupBox groupScanningOptions;
		private Label label8;
	}
}
