using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormEhrSummaryCcdEdit:FormODBase {
		public bool DidPrint;
		///<summary>The text of the CCD (or CCR), kept in memory only: the document is never written to disk while it is shown, and a crash leaves
		///nothing behind. Shown, reconciled and shown as raw XML from here.</summary>
		private string _xmlCcd;
		///<summary>Will be set to the patient that this CCD message is indicated for.  Can be null if not meant for incorporating into a patient's account.</summary>
		private Patient _patCur;
		///<summary>The page the program's own stylesheet made of the document (EhrStylesheetStarter, in a child process), or null to show the XML.</summary>
		private string _htmlRendered;
		///<summary>The line shown above the document, for example that the stylesheet that came with it was not run. Can be empty.</summary>
		private string _note;

		///<summary>xmlCcd is the text of the CCD (or CCR), not a file. htmlRendered is the page the program's own stylesheet made of it, or null:
		///the window then shows the XML. Patient can be null.  If null, or if PatNum is 0, reconciles will not be available.
		///note is shown above the document.</summary>
		public FormEhrSummaryCcdEdit(string xmlCcd,string htmlRendered,Patient patCur,string note) {
			InitializeComponent();
			InitializeLayoutManager();
			_xmlCcd=xmlCcd??"";
			_htmlRendered=htmlRendered;
			_patCur=patCur;
			_note=note;
		}

		///<summary>The longest part of a document over the stylesheet child's limits that the window shows (1 MB of text).</summary>
		private const int MAX_CHARS_SHOWN_OVER_LIMITS=1024*1024;

		///<summary>The page the window shows. The rendered page with the note as its first line; without one, the XML, indented and encoded, below
		///the note. A document over the child's limits (EhrStylesheetStarter.GetLimitError: too large, too deep, a DTD) is shown as its text, encoded,
		///not indented (the indentation grows with the depth squared) and cut after 1 MB, so the program stays up.</summary>
		public static string BuildPage(string xmlCcd,string htmlRendered,string note) {
			string html;
			if(!string.IsNullOrEmpty(htmlRendered)) {
				html=InsertNote(htmlRendered,note);
			}
			else if(EhrStylesheetStarter.GetLimitError(xmlCcd)!=null && !string.IsNullOrEmpty(xmlCcd)) {
				string text=xmlCcd;
				string noteShown=note;
				if(text.Length>MAX_CHARS_SHOWN_OVER_LIMITS) {
					text=text.Substring(0,MAX_CHARS_SHOWN_OVER_LIMITS);
					noteShown+="\r\n"+Lan.g("FormEhrSummaryCcdEdit","Only the first part of the document's text is shown.");
				}
				html=HtmlPage.Build("<p>"+HtmlPage.EncodeWithLineBreaks(noteShown)+"</p><pre style=\"white-space:pre-wrap;word-wrap:break-word\">"
					+HtmlPage.Encode(text)+"</pre>","","font-family:sans-serif");
			}
			else {
				//The document is shown as XML, encoded, so patient data is never markup.
				html=HtmlPage.BuildXml(xmlCcd,note);
			}
			return html;
		}

		///<summary>The page with the note, encoded, as a paragraph at the start of its body. A page without a body tag gets it at the start.</summary>
		public static string InsertNote(string html,string note) {
			if(string.IsNullOrEmpty(note)) {
				return html;
			}
			string paragraph="<p style=\"font-family:sans-serif;font-weight:bold\">"+HtmlPage.EncodeWithLineBreaks(note)+"</p>";
			Match match=Regex.Match(html,@"<body\b[^>]*>",RegexOptions.IgnoreCase);
			if(!match.Success) {
				return paragraph+html;
			}
			return html.Insert(match.Index+match.Length,paragraph);
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
				//No stylesheet runs in the program: the page was made by the stylesheet child (EhrStylesheetStarter), or the document is shown as XML.
				//A stylesheet that came with an email never runs: it could recurse without end, and the stack overflow would end the program whatever
				//catches it, or loop for ever.
				html=BuildPage(_xmlCcd,_htmlRendered,_note);
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
				xmlDocCcd.LoadXml(_xmlCcd);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error reading the document")+": "+ex.Message);
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
				xmlDocCcd.LoadXml(_xmlCcd);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error reading the document")+": "+ex.Message);
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
				xmlDocCcd.LoadXml(_xmlCcd);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error reading the document")+": "+ex.Message);
				return;
			}
			using FormReconcileAllergy formRA=new FormReconcileAllergy(_patCur);
			formRA.ListAllergiesNew=new List<Allergy>();
			formRA.ListAllergyDefsNew=new List<AllergyDef>();
			EhrCCD.GetListAllergies(xmlDocCcd,formRA.ListAllergiesNew,formRA.ListAllergyDefsNew);
			formRA.ShowDialog();
		}

		private void butShowXml_Click(object sender,EventArgs e) {
			string strCcd=_xmlCcd;
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