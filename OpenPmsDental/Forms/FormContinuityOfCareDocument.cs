using System;
using System.IO;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormContinuityOfCareDocument:FormODBase {
		public Patient PatCur;

		public FormContinuityOfCareDocument() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void butShowAndPrint_Click(object sender,EventArgs e) {
			string continuityOfCareText="";
			try {
				continuityOfCareText=EhrCCD.GenerateClinicalSummary(PatCur,true,true,true,true,true,true,true,true,true,true,true,true,
					textInstructions.Text,DateTime.MinValue,out string warnings);
				if(!string.IsNullOrEmpty(warnings)) {
					if(MessageBox.Show(warnings,"Warnings",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
						return;
					}
				}
			} 
			catch(Exception ex) {
				MsgBox.Show(this,ex.Message);
				return;
			}
			bool didPrint=FormEhrSummaryOfCare.DisplayCCD(continuityOfCareText);
			if(didPrint) {
				//we are printing a ccd so add new measure event.					
				EhrMeasureEvent ehrMeasureEvent=new EhrMeasureEvent();
				ehrMeasureEvent.DateTEvent=DateTime.Now;
				ehrMeasureEvent.EventType=EhrMeasureEventType.ClinicalSummaryProvidedToPt;
				ehrMeasureEvent.PatNum=PatCur.PatNum;
				EhrMeasureEvents.Insert(ehrMeasureEvent);
			}
		}

		private void butExportToFile_Click(object sender,EventArgs e) {
			string continuityOfCareText="";
			try {
				continuityOfCareText=EhrCCD.GenerateClinicalSummary(PatCur,true,true,true,true,true,true,true,true,true,true,true,true,
					textInstructions.Text,DateTime.MinValue,out string warnings);
				if(!string.IsNullOrEmpty(warnings)) {
					if(MessageBox.Show(warnings,"Warnings",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
						return;
					}
				}
			}
			catch(Exception ex) {
				MsgBox.Show(this,ex.Message);
				return;
			}
			using FolderBrowserDialog formBrowserDialog=new FolderBrowserDialog();
			formBrowserDialog.SelectedPath=ImageStore.GetPatientFolder(PatCur,ImageStore.GetPreferredAtoZpath());//Default to patient image folder.
			DialogResult dialogResult=formBrowserDialog.ShowDialog();
			if(dialogResult!=DialogResult.OK) {
				return;
			}
			if(File.Exists(Path.Combine(formBrowserDialog.SelectedPath,"ccd.xml"))) {
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Overwrite existing ccd.xml file?")) {
					return;
				}
			}
			string reasonPage=FormEhrSummaryOfCare.WriteCcdFiles(formBrowserDialog.SelectedPath,continuityOfCareText);//ccd.xml, the stylesheet files, the licence text and ccd.html.
			EhrMeasureEvent ehrMeasureEvent=new EhrMeasureEvent();
			ehrMeasureEvent.DateTEvent=DateTime.Now;
			ehrMeasureEvent.EventType=EhrMeasureEventType.ClinicalSummaryProvidedToPt;
			ehrMeasureEvent.PatNum=PatCur.PatNum;
			EhrMeasureEvents.Insert(ehrMeasureEvent);
			MessageBox.Show(Lan.g(this,"File has been exported.")+FormEhrSummaryOfCare.GetPageNotWrittenMessage(reasonPage));
		}

	}
}