using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;
using CodeBase;
using System.Diagnostics;
using Microsoft.VisualBasic;
using System.Collections.Generic;

namespace OpenPmsDental{
///<summary></summary>
	public partial class FormImagingSetup:FormODBase {
		private bool _showScanDocSelectSourceOld;
		//private ComputerPref computerPrefs;

		///<summary></summary>
		public FormImagingSetup(){
			InitializeComponent();
			InitializeLayoutManager();
			//too many labels to use Lan.F()
			Lan.C(this, new System.Windows.Forms.Control[]
			{
				this,
				this.groupBox1,
				this.groupBox2,
				this.groupBox3,
				this.label1,
				this.label5,
				this.label6,
				this.label7,
				this.label12,
				this.labelPanoBW,
				this.label37,
				this.butScanDocSelectSourceDetails
			});
			Lan.C("All", new System.Windows.Forms.Control[] {
				butSave,
			});
		}

		private void FormImagingSetup_Load(object sender, System.EventArgs e) {
			_showScanDocSelectSourceOld=ComputerPrefs.LocalComputer.ScanDocSelectSource;
			checkScanDocSelectSource.Checked=_showScanDocSelectSourceOld;
			if(ComputerPrefs.LocalComputer.ScanDocShowOptions) {
				radioScanDocShowOptions.Checked=true;
				radioScanDocUseOptionsBelow.Checked=false;
				groupScanningOptions.Enabled=false;
			}
			else {
				radioScanDocShowOptions.Checked=false;
				radioScanDocUseOptionsBelow.Checked=true;
				groupScanningOptions.Enabled=true;
			}
			checkScanDocDuplex.Checked=ComputerPrefs.LocalComputer.ScanDocDuplex;
			checkScanDocGrayscale.Checked=ComputerPrefs.LocalComputer.ScanDocGrayscale;
			textScanDocResolution.Text=ComputerPrefs.LocalComputer.ScanDocResolution.ToString();
			textScanDocQuality.Text=ComputerPrefs.LocalComputer.ScanDocQuality.ToString();
			//textScanDocQuality.Text=PrefC.GetLong(PrefName.ScannerCompression).ToString();
			slider.MinVal=PrefC.GetInt(PrefName.ImageWindowingMin);
			slider.MaxVal=PrefC.GetInt(PrefName.ImageWindowingMax);
			//The Suni sensor settings (exposure, port, type, binned) were for Open Dental's closed Suni capture driver, removed on 2026-11-01.
			//Open Dental 26.2 removed Suni support altogether (P262-LINKS). The computerpref columns stay in the database.
			//checkScanDocShowOptions.Checked=PrefC.GetBool(PrefName.ScannerSuppressDialog);
			//textScanDocResolution.Text=PrefC.GetString(PrefName.ScannerResolution);
			if(Clinics.IsMedicalPracticeOrClinic(Clinics.ClinicNum)) {
				labelPanoBW.Visible=false;
			}
		}

		private void radioScanDocShowOptions_CheckedChanged(object sender,EventArgs e) {
			groupScanningOptions.Enabled=true;
			if(radioScanDocShowOptions.Checked) {
				groupScanningOptions.Enabled=false;
			}
		}

		///<summary>Explains when to turn on Show Select Scanner Window. Only shows text: nothing here talks to a scanner.</summary>
		private void butScanDocSelectSourceDetails_Click(object sender,EventArgs e) {
			MsgBox.Show(this,"When this box is checked, the window for choosing a scanner opens every time a document is scanned on this computer.\r\n\r\n"
				+"Turn it on if this computer scans with a Canon scanner and, after scanning, either of these happens:\r\n"
				+"- some PDFs no longer open in the Imaging module;\r\n"
				+"- list boxes in other windows, such as Commlog, stop showing which row is selected.\r\n\r\n"
				+"Choosing the scanner before each scan avoids both problems. Otherwise it can stay off, and scans use the scanner picked with Set Default Scanner.");
		}

		private void butSetScanner_Click(object sender,EventArgs e) {
			try {
				ImagingDeviceManager.Twain.ActivateEZTwain();
			}
			catch {
				MsgBox.Show(this,"EzTwain4.dll not found.  Please run the setup file in your images folder.");
				return;
			}
			ImagingDeviceManager.EZTwain.SelectImageSource(this.Handle);
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(!textScanDocQuality.IsValid() || !textScanDocResolution.IsValid()) {
				MessageBox.Show(Lan.g(this,"Please fix data entry errors first."));
				return;
			}
			if(textScanDocQuality.Text=="100"
				|| (radioScanDocUseOptionsBelow.Checked && PIn.Int(textScanDocResolution.Text)>300)) 
			{
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"With the provided settings the file created may be extremely large.  Would you like to continue?")) {
					return;
				}
			}
			ComputerPrefs.LocalComputer.ScanDocSelectSource=checkScanDocSelectSource.Checked;
			ComputerPrefs.LocalComputer.ScanDocShowOptions=radioScanDocShowOptions.Checked;
			ComputerPrefs.LocalComputer.ScanDocDuplex=checkScanDocDuplex.Checked;
			ComputerPrefs.LocalComputer.ScanDocGrayscale=checkScanDocGrayscale.Checked;
			ComputerPrefs.LocalComputer.ScanDocResolution=PIn.Int(textScanDocResolution.Text);
			ComputerPrefs.LocalComputer.ScanDocQuality=PIn.Byte(textScanDocQuality.Text);
			//Prefs.UpdateLong(PrefName.ScannerCompression,PIn.Long(textScanDocQuality.Text));
			Prefs.UpdateLong(PrefName.ImageWindowingMin,slider.MinVal);
			Prefs.UpdateLong(PrefName.ImageWindowingMax,slider.MaxVal);
			//Prefs.UpdateBool(PrefName.ScannerSuppressDialog,checkScanDocShowOptions.Checked);
			//Prefs.UpdateLong(PrefName.ScannerResolution,PIn.Long(textScanDocResolution.Text));
			ComputerPrefs.Update(ComputerPrefs.LocalComputer);
			DataValid.SetInvalid(InvalidType.Prefs);
			if(_showScanDocSelectSourceOld!=checkScanDocSelectSource.Checked) {
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0
					,Lan.g(this,"Show Select Scanner Window option changed from")+" "
					+(_showScanDocSelectSourceOld?Lan.g(this,"true"):Lan.g(this,"false"))+" "
					+Lan.g(this,"to")+" "+(checkScanDocSelectSource.Checked?Lan.g(this,"true"):Lan.g(this,"false")));
			}
			DialogResult=DialogResult.OK;
		}

	}
}