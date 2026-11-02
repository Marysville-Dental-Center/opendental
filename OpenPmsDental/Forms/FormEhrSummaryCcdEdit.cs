using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormEhrSummaryCcdEdit:FormODBase {
		public string StrXmlFilePath;
		public bool DidPrint;
		///<summary>Will be set to the patient that this CCD message is indicated for.  Can be null if not meant for incorporating into a patient's account.</summary>
		private Patient _patCur;
		///<summary>The line shown above the document, for example that the stylesheet that came with it was not run. Can be empty.</summary>
		private string _note;

		///<summary>Patient can be null.  If null, or if PatNum is 0, reconciles will not be available. note is shown above the document.</summary>
		public FormEhrSummaryCcdEdit(string strXmlFilePath,Patient patCur,string note) {
			InitializeComponent();
			InitializeLayoutManager();
			StrXmlFilePath=strXmlFilePath;
			_patCur=patCur;
			_note=note;
		}

		private void FormEhrSummaryCcdEdit_Load(object sender,EventArgs e) {
			if(_patCur==null || _patCur.PatNum==0) {//No patient is currently selected.  Do not show reconcile UI.
				labelReconcile.Visible=false;
				butReconcileAllergies.Visible=false;
				butReconcileMedications.Visible=false;
				butReconcileProblems.Visible=false;
			}
			Cursor=Cursors.WaitCursor;
			string html;
			try {
				//The document is shown as XML, encoded, so patient data is never markup. No stylesheet runs in the program: one that came with an email
				//could recurse without end, and the stack overflow would end the program whatever catches it, or loop for ever.
				html=HtmlPage.BuildXml(File.ReadAllText(StrXmlFilePath),_note);
			}
			catch(Exception ex) {
				html=HtmlPage.BuildText(Lan.g(this,"The document could not be shown.")+"\r\n"+ex.Message);
			}
			webViewMain.LinkClicked+=webViewMain_LinkClicked;
			try {
				//The document can come with a received email: the page loads no file but its own and nothing from the web.
				webViewMain.NavigateToHtml(html);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"The document could not be shown."),ex);
			}
			Cursor=Cursors.Default;
		}

		///<summary>A link in the document opens in the default browser (http, https and mailto only), never in this window.</summary>
		private void webViewMain_LinkClicked(object sender,CodeBase.Controls.ODWebView2LinkEventArgs e) {
			CodeBase.Controls.WebView2Policy.OpenInSystemBrowser(e.GetSystemBrowserUrl());
		}

		///<summary>Can only be called if IsReconcile is true.  This function is for EHR module b.4.</summary>
		private void butReconcileMedications_Click(object sender,EventArgs e) {
			XmlDocument xmlDocCcd=new XmlDocument();
			try {
				string strXmlText=File.ReadAllText(StrXmlFilePath);
				xmlDocCcd.LoadXml(strXmlText);				
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error reading file")+": "+ex.Message);
				return;
			}
			using FormReconcileMedication formRM=new FormReconcileMedication(_patCur);
			formRM.ListMedicationPatsNew=new List<MedicationPat>();
			EhrCCD.GetListMedicationPats(xmlDocCcd,formRM.ListMedicationPatsNew);
			formRM.ShowDialog();
		}

		///<summary>Can only be called if IsReconcile is true.  This function is for EHR module b.4.</summary>
		private void butReconcileProblems_Click(object sender,EventArgs e) {
			XmlDocument xmlDocCcd=new XmlDocument();
			try {
				string strXmlText=File.ReadAllText(StrXmlFilePath);
				xmlDocCcd.LoadXml(strXmlText);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error reading file")+": "+ex.Message);
				return;
			}
			using FormReconcileProblem formRP=new FormReconcileProblem(_patCur);
			formRP.ListDiseasesNew=new List<Disease>();
			formRP.ListDiseaseDefsNew=new List<DiseaseDef>();
			EhrCCD.GetListDiseases(xmlDocCcd,formRP.ListDiseasesNew,formRP.ListDiseaseDefsNew);
			formRP.ShowDialog();
		}

		///<summary>Can only be called if IsReconcile is true.  This function is for EHR module b.4.</summary>
		private void butReconcileAllergies_Click(object sender,EventArgs e) {
			XmlDocument xmlDocCcd=new XmlDocument();
			try {
				string strXmlText=File.ReadAllText(StrXmlFilePath);
				xmlDocCcd.LoadXml(strXmlText);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error reading file")+": "+ex.Message);
				return;
			}
			using FormReconcileAllergy formRA=new FormReconcileAllergy(_patCur);
			formRA.ListAllergiesNew=new List<Allergy>();
			formRA.ListAllergyDefsNew=new List<AllergyDef>();
			EhrCCD.GetListAllergies(xmlDocCcd,formRA.ListAllergiesNew,formRA.ListAllergyDefsNew);
			formRA.ShowDialog();
		}

		private void butShowXml_Click(object sender,EventArgs e) {
			string strCcd=File.ReadAllText(StrXmlFilePath);
			//Reformat to add newlines after each element to make more readable.
			strCcd=strCcd.Replace("\r\n","").Replace("\n","").Replace("\r","");//Remove existsing newlines.
			strCcd=strCcd.Replace(">",">\r\n");
			using MsgBoxCopyPaste msgbox=new MsgBoxCopyPaste(strCcd);
			msgbox.ShowDialog();
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(webViewMain.CoreWebView2==null) {
				return;
			}
			//Windows' print dialog, which also allows user to choose printer (WebView2's print API)
			webViewMain.ShowPrintDialog();
			DidPrint = true;
		}

	}
}