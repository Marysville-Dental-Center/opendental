using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using CodeBase;
using System.Xml;
using OpenPmsDental.UI;

namespace OpenPmsDental {
	public partial class FormEhrSummaryOfCare:FormODBase {
		public Patient PatCur;
		private List<EhrMeasureEvent> _listHistorySent;
		private List<EhrSummaryCcd> _listCcdRec;

		public FormEhrSummaryOfCare() {
			InitializeComponent();
			InitializeLayoutManager();
		}

		private void FormSummaryOfCare_Load(object sender,EventArgs e) {
			FillGridSent();
			FillGridRec();
		}

		private void FillGridSent() {
			List<RefAttach> listRefAttaches;
			gridSent.BeginUpdate();
			gridSent.Columns.Clear();
			GridColumn col=new GridColumn("DateTime",130,HorizontalAlignment.Center);
			gridSent.Columns.Add(col);
			col=new GridColumn("Meets",140,HorizontalAlignment.Center);
			gridSent.Columns.Add(col);
			_listHistorySent=EhrMeasureEvents.RefreshByType(PatCur.PatNum,EhrMeasureEventType.SummaryOfCareProvidedToDr);
			listRefAttaches=RefAttaches.GetRefAttachesForSummaryOfCareForPat(PatCur.PatNum);
			gridSent.ListGridRows.Clear();
			GridRow row;
			for(int i=0;i<_listHistorySent.Count;i++) {
				row=new GridRow();
				row.Cells.Add(_listHistorySent[i].DateTEvent.ToString());
				if(_listHistorySent[i].FKey==0) {
					row.Cells.Add("");
				}
				else {
					//Only add an X in the grid for the measure events that meet the summary of care measure so that users can see which ones meet.
					for(int j=0;j<listRefAttaches.Count;j++) {
						if(listRefAttaches[j].RefAttachNum==_listHistorySent[i].FKey) {
							row.Cells.Add("X");
							break;
						}
					}
				}
				gridSent.ListGridRows.Add(row);
			}
			gridSent.EndUpdate();
		}

		private void gridSent_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			FrmReferralsPatient FrmReferralsPatient=new FrmReferralsPatient();
			FrmReferralsPatient.DefaultRefAttachNum=_listHistorySent[gridSent.GetSelectedIndex()].FKey;
			FrmReferralsPatient.PatNum=PatCur.PatNum;
			FrmReferralsPatient.IsSelectionMode=true;
			FrmReferralsPatient.ShowDialog();
			if(FrmReferralsPatient.IsDialogCancel) {
				return;
			}
			_listHistorySent[gridSent.GetSelectedIndex()].FKey=FrmReferralsPatient.RefAttachNum;
			EhrMeasureEvents.Update(_listHistorySent[gridSent.GetSelectedIndex()]);
			FillGridSent();
		}

		private void FillGridRec() {
			gridRec.BeginUpdate();
			gridRec.Columns.Clear();
			GridColumn col=new GridColumn("DateTime",140,HorizontalAlignment.Center);
			gridRec.Columns.Add(col);
			_listCcdRec=EhrSummaryCcds.Refresh(PatCur.PatNum);
			gridRec.ListGridRows.Clear();
			GridRow row;
			for(int i=0;i<_listCcdRec.Count;i++) {
				row=new GridRow();
				row.Cells.Add(_listCcdRec[i].DateSummary.ToShortDateString());
				gridRec.ListGridRows.Add(row);
			}
			gridRec.EndUpdate();
		}

		private void gridRec_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			string xmltext=_listCcdRec[gridRec.GetSelectedIndex()].ContentSummary;
			DisplayCCD(xmltext,PatCur);
		}

		///<summary>Returns true if user performed a print job on the CCD.  Cannot be moved to OpenPmsDentalBusiness/Misc/EhrCCD.cs, because this function uses windows UI components.  
		///Strictly used for displaying CCD messages.  Will not allow user to reconcile meds, problems, or allergies into the patient's account.</summary>
		public static bool DisplayCCD(string strXmlCCD) {
			return DisplayCCD(strXmlCCD,null);
		}

		public static bool DisplayCCD(string strXmlCCD,Patient patCur) {
			return DisplayCCD(strXmlCCD,patCur,"");
		}

		///<summary>Returns true if user performed a print job on the CCD.  Cannot be moved to OpenPmsDentalBusiness/Misc/EhrCCD.cs, because this function uses windows UI components. 
		///Pass in a valid patient if the CCD is being displayed to reconcile (incoporate into patient record) medications and/or problems and/or allergies.
		///patCur can be null, or patCur.PatNum can be 0, to hide the three reconcile buttons. 
		///strXmlCCD is the actual text of the CCD.
		///strAlterateFilPathXslCCD is the file of a stylesheet that came with the CCD in an email, or "". That stylesheet is never read or run: the
		///document is shown with the program's own stylesheet (a CCD with HL7's CDA.xsl, a CCR with the AAFP's ccr.xsl), run in a child process by
		///EhrStylesheetStarter, or as XML with the reason when that fails, with its print and reconcile buttons either way.</summary>
		public static bool DisplayCCD(string strXmlCCD,Patient patCur,string strAlterateFilPathXslCCD) {
			//The child's limits first, on the text: XmlDocument.InnerXml below writes the document recursively in the program's own process, and a
			//document type declaration could expand its entities here. Such a document is shown as text, with the reason and no reconcile buttons.
			string reasonLimits=EhrStylesheetStarter.GetLimitError(strXmlCCD);
			if(reasonLimits!=null) {
				string noteLimits=Lan.g("FormEhrSummaryOfCare","The document is not shown with the program's stylesheet")+": "+reasonLimits;
				using FormEhrSummaryCcdEdit formLimits=new FormEhrSummaryCcdEdit(strXmlCCD,null,null,noteLimits);
				formLimits.ShowDialog();
				return formLimits.DidPrint;
			}
			//string xmltext=GetSampleMissingStylesheet();
			XmlDocument doc=new XmlDocument();
			try {
				doc.LoadXml(strXmlCCD);
			}
			catch {
				throw new XmlException("Invalid XML");
			}
			string rootName=doc.DocumentElement.Name.ToLower();
			bool isCcd=rootName=="clinicaldocument";//CCD, CCDA, and C32.
			bool isCcr=rootName=="continuityofcarerecord" || rootName=="ccr:continuityofcarerecord";
			if(!isCcd && !isCcr) {
				MessageBox.Show("This is not a valid CCD, CCDA, CCR, or C32 message.  Only the raw text will be shown");
				MessageBox.Show(strXmlCCD);
				return false;
			}
			bool isStylesheetAttached=isCcd && !string.IsNullOrEmpty(strAlterateFilPathXslCCD);
			//The program's own stylesheet runs in a child process (EhrStylesheetStarter): the program has no XSLT in its own process, because a
			//stylesheet that recurses without end overflows the stack, which ends the process whatever catches it, and one that loops never returns.
			//A stylesheet that came with an email is never run (E-14).
			Cursor.Current=Cursors.WaitCursor;
			string htmlRendered=EhrStylesheetStarter.Render(isCcr ? EhrRenderKind.Ccr : EhrRenderKind.Ccd,strXmlCCD,out string reasonRender);
			Cursor.Current=Cursors.Default;
			string note="";
			if(htmlRendered!=null && isStylesheetAttached) {
				note=Lan.g("FormEhrSummaryOfCare","The stylesheet that came with this document was not run. The document is shown with the program's own stylesheet.");
			}
			else if(htmlRendered==null && isStylesheetAttached) {
				note=Lan.g("FormEhrSummaryOfCare","The stylesheet that came with this document was not run, so the document is shown as XML.")
					+"\r\n"+Lan.g("FormEhrSummaryOfCare","The program's own stylesheet could not show it")+": "+reasonRender;
			}
			else if(htmlRendered==null) {
				note=Lan.g("FormEhrSummaryOfCare","The program's stylesheet could not show this document, so it is shown as XML")+": "+reasonRender;
			}
			//The document stays in memory while it is shown: the patient's clinical document never sits on disk, and a crash leaves nothing behind.
			//An older version wrote it (and the email's stylesheet) to the program's temp folder until the window closed; remove what one left there.
			DeleteOldCcdFiles(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName),DateTime.Now.AddHours(-1));
			using FormEhrSummaryCcdEdit formESCD=new FormEhrSummaryCcdEdit(doc.InnerXml,htmlRendered,patCur,note);
			formESCD.ShowDialog();
			return formESCD.DidPrint;
		}

		///<summary>The names of the files an older version wrote into the program's temp folder while it showed a CCD or CCR: the document and its
		///stylesheet (the one that came with an email).</summary>
		private static readonly string[] _arrayOldCcdFileNames={"ccd.xml","ccd.xsl","ccr.xml","ccr.xsl"};

		///<summary>Deletes the files an older version wrote into folder (the program's temp folder) while it showed a CCD or CCR, when they were
		///written before dateTimeWrittenBefore. That version deleted them when the window closed, and the program empties the folder when it closes,
		///so only an older version that ended with the window open leaves them; the time leaves alone the file of an older version that shows a CCD
		///at the same time (it reads it again to reconcile). Nothing else is touched; a file in use stays. Returns how many it deleted. Never throws.
		///Does not create folder.</summary>
		public static int DeleteOldCcdFiles(string folder,DateTime dateTimeWrittenBefore) {
			if(string.IsNullOrWhiteSpace(folder)) {
				return 0;
			}
			int countDeleted=0;
			for(int i=0;i<_arrayOldCcdFileNames.Length;i++) {
				try {
					string path=Path.Combine(folder,_arrayOldCcdFileNames[i]);
					if(!File.Exists(path) || File.GetLastWriteTime(path)>=dateTimeWrittenBefore) {
						continue;
					}
					File.Delete(path);
					countDeleted++;
				}
				catch(Exception ex) {
					ex.DoNothing();//In use, or not sufficient permissions: the program empties the folder when it closes.
				}
			}
			return countDeleted;
		}

		///<summary>A single-document file export (E-13) into folder: ccd.xml, the stylesheet files (ccd.xsl, which is CDA.xsl, and its two companions,
		///unmodified), stylesheets-license.txt (the Apache-2.0 text, E-11) and ccd.html, the page the program's own stylesheet makes of the document
		///in a child process, because browsers stop running XSLT (Chrome 158, November 2026). Returns null, or why ccd.html was not written; the other
		///files are written either way, and an older ccd.html is deleted then, so it never shows another document. Throws when a file cannot be
		///written, as the export buttons did before.</summary>
		public static string WriteCcdFiles(string folder,string ccd) {
			File.WriteAllText(Path.Combine(folder,EhrSummaryCcds.FileNameCcdXml),ccd);
			WriteStylesheetFiles(folder);
			string pathHtml=Path.Combine(folder,EhrSummaryCcds.FileNameCcdHtml);
			string html=EhrStylesheetStarter.Render(EhrRenderKind.Ccd,ccd,out string reason);
			if(html==null) {
				if(File.Exists(pathHtml)) {
					File.Delete(pathHtml);
				}
				return reason;
			}
			File.WriteAllText(pathHtml,html);
			return null;
		}

		///<summary>The stylesheet files and the licence text of a file export into folder (EhrSummaryCcds.GetCcdStylesheetFiles), unmodified.</summary>
		public static void WriteStylesheetFiles(string folder) {
			foreach(KeyValuePair<string,string> pairFile in EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:true)) {
				File.WriteAllText(Path.Combine(folder,pairFile.Key),pairFile.Value);
			}
		}

		///<summary>Bulk Patient Export (E-13): writes <name>.html beside each <name>.xml already in folder, one per document, all made by one stylesheet
		///child, so CDA.xsl is compiled once, and one set of stylesheet files and the licence text. listFileNames are the names without the
		///extension, listCcds the documents, in the same order. Returns the reasons of the pages that were not written, one line each with the name,
		///or "" when all were. Throws when a file cannot be written.</summary>
		public static string WriteCcdPages(string folder,List<string> listFileNames,List<string> listCcds) {
			WriteStylesheetFiles(folder);
			List<EhrRenderResult> listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,listCcds);
			StringBuilder stringBuilderReasons=new StringBuilder();
			for(int i=0;i<listFileNames.Count;i++) {
				if(listEhrRenderResults[i].Html==null) {
					stringBuilderReasons.AppendLine(listFileNames[i]+".html: "+listEhrRenderResults[i].Reason);
					continue;
				}
				File.WriteAllText(Path.Combine(folder,listFileNames[i]+".html"),listEhrRenderResults[i].Html);
			}
			return stringBuilderReasons.ToString();
		}

		///<summary>The message for an export whose page could not be made (reason from WriteCcdFiles), or "" when it was.</summary>
		public static string GetPageNotWrittenMessage(string reason) {
			if(reason==null) {
				return "";
			}
			return "\r\n"+Lan.g("FormEhrSummaryOfCare","ccd.html was not written, because the program's stylesheet could not show the document")+": "+reason;
		}

		private void butExport_Click(object sender,EventArgs e) {
			//Generate the CCD first so that any validation errors are apparent and up front.
			//It is better to not let the user waste their time creating a referral if there is a basic validation issue with the CCD generation.
			string ccd="";
			try {
				ccd=EhrCCD.GenerateSummaryOfCare(PatCur,out string warnings);
				if(!string.IsNullOrEmpty(warnings)) {
					if(MessageBox.Show(warnings,"Warnings",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
						return;
					}
				}
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			FrmReferralsPatient FrmReferralsPatient=new FrmReferralsPatient();
			FrmReferralsPatient.PatNum=PatCur.PatNum;
			FrmReferralsPatient.IsSelectionMode=true;
			FrmReferralsPatient.ShowDialog();
			if(FrmReferralsPatient.IsDialogCancel) {
				MessageBox.Show("Summary of Care not exported.");
				return;
			}
			using FolderBrowserDialog dlg=new FolderBrowserDialog();
			dlg.SelectedPath=ImageStore.GetPatientFolder(PatCur,ImageStore.GetPreferredAtoZpath());//Default to patient image folder.
			DialogResult result=dlg.ShowDialog();
			if(result!=DialogResult.OK) {
				return;
			}
			if(File.Exists(Path.Combine(dlg.SelectedPath,"ccd.xml"))) {
				if(MessageBox.Show("Overwrite existing ccd.xml?","",MessageBoxButtons.OKCancel)!=DialogResult.OK) {
					return;
				}
			}
			string reasonPage=WriteCcdFiles(dlg.SelectedPath,ccd);//ccd.xml, the stylesheet files, the licence text and ccd.html.
			EhrMeasureEvent newMeasureEvent = new EhrMeasureEvent();
			newMeasureEvent.DateTEvent = DateTime.Now;
			newMeasureEvent.EventType = EhrMeasureEventType.SummaryOfCareProvidedToDr;
			newMeasureEvent.PatNum = PatCur.PatNum;
			newMeasureEvent.FKey=FrmReferralsPatient.RefAttachNum;//Can be 0 if user didn't pick a referral for some reason.
			long fkey=EhrMeasureEvents.Insert(newMeasureEvent);
			newMeasureEvent=new EhrMeasureEvent();
			newMeasureEvent.DateTEvent=DateTime.Now;
			newMeasureEvent.FKey=fkey;
			newMeasureEvent.EventType=EhrMeasureEventType.SummaryOfCareProvidedToDrElectronic;
			newMeasureEvent.PatNum=PatCur.PatNum;
			newMeasureEvent.FKey=FrmReferralsPatient.RefAttachNum;//Can be 0 if user didn't pick a referral for some reason.
			EhrMeasureEvents.Insert(newMeasureEvent);
			FillGridSent();
			MessageBox.Show("Exported"+GetPageNotWrittenMessage(reasonPage));
		}

		private void butSendEmail_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.EmailSend)) {
				return;
			}
			//Generate the CCD first so that any validation errors are apparent and up front.
			//It is better to not let the user waste their time creating a referral if there is a basic validation issue with the CCD generation.
			string ccd="";
			try {
				ccd=EhrCCD.GenerateSummaryOfCare(PatCur,out string warnings);
				if(!string.IsNullOrEmpty(warnings)) {
					if(MessageBox.Show(warnings,"Warnings",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
						return;
					}
				}
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			FrmReferralsPatient FrmReferralsPatient=new FrmReferralsPatient();
			FrmReferralsPatient.PatNum=PatCur.PatNum;
			FrmReferralsPatient.IsSelectionMode=true;
			FrmReferralsPatient.ShowDialog();
			if(FrmReferralsPatient.IsDialogCancel) {
				MessageBox.Show("Summary of Care not exported.");
				return;
			}
			Cursor=Cursors.WaitCursor;
			EmailAddress emailAddressFrom=EmailAddresses.GetByClinic(0);//Default for clinic/practice.
			EmailMessage emailMessage=new EmailMessage();
			emailMessage.PatNum=PatCur.PatNum;
			emailMessage.MsgDateTime=DateTime.Now;
			emailMessage.SentOrReceived=EmailSentOrReceived.Neither;//To force FormEmailMessageEdit into "compose" mode.
			emailMessage.FromAddress=emailAddressFrom.EmailUsername;//Cannot be emailAddressFrom.SenderAddress, because it would cause encryption to fail.
			emailMessage.ToAddress="";//User must set inside of FormEmailMessageEdit
			emailMessage.Subject="Summary of Care";
			emailMessage.BodyText="Summary of Care";
			emailMessage.MsgType=EmailMessageSource.EHR;
			try {
				emailMessage.Attachments.Add(EmailAttaches.CreateAttach("ccd.xml",Encoding.UTF8.GetBytes(ccd)));
				foreach(KeyValuePair<string,string> pairFile in EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:false)) {//ccd.xsl and its two companions
					emailMessage.Attachments.Add(EmailAttaches.CreateAttach(pairFile.Key,Encoding.UTF8.GetBytes(pairFile.Value)));
				}
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				MessageBox.Show(ex.Message);
				return;
			}
			EmailMessages.Insert(emailMessage);
			using FormEmailMessageEdit formE=new FormEmailMessageEdit(emailMessage,emailAddressFrom);//Not "new" message because it already exists in db due to pre-insert.
			if(formE.ShowDialog()==DialogResult.OK) {
				EhrMeasureEvent newMeasureEvent=new EhrMeasureEvent();
				newMeasureEvent.DateTEvent=DateTime.Now;
				newMeasureEvent.EventType=EhrMeasureEventType.SummaryOfCareProvidedToDr;
				newMeasureEvent.PatNum=PatCur.PatNum;
				newMeasureEvent.FKey=FrmReferralsPatient.RefAttachNum;//Can be 0 if user didn't pick a referral for some reason.
				EhrMeasureEvents.Insert(newMeasureEvent);
				newMeasureEvent=new EhrMeasureEvent();
				newMeasureEvent.DateTEvent=DateTime.Now;
				newMeasureEvent.EventType=EhrMeasureEventType.SummaryOfCareProvidedToDrElectronic;
				newMeasureEvent.PatNum=PatCur.PatNum;
				newMeasureEvent.FKey=FrmReferralsPatient.RefAttachNum;//Can be 0 if user didn't pick a referral for some reason.
				EhrMeasureEvents.Insert(newMeasureEvent);
				FillGridSent();
			}
			Cursor=Cursors.Default;
		}

		private void butShowXhtml_Click(object sender,EventArgs e) {
			FrmReferralsPatient FrmReferralsPatient=new FrmReferralsPatient();
			FrmReferralsPatient.PatNum=PatCur.PatNum;
			FrmReferralsPatient.IsSelectionMode=true;
			FrmReferralsPatient.ShowDialog();
			if(FrmReferralsPatient.IsDialogCancel) {
				MessageBox.Show("Summary of Care not shown.");
				return;
			}
			string ccd="";
			try {
				ccd=EhrCCD.GenerateSummaryOfCare(PatCur,out string warnings);
				if(!string.IsNullOrEmpty(warnings)) {
					if(MessageBox.Show(warnings,"Warnings",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
						return;
					}
				}
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			bool didPrint=DisplayCCD(ccd);
			if(didPrint) {
				//we are printing a ccd so add new measure event.					
				EhrMeasureEvent measureEvent = new EhrMeasureEvent();
				measureEvent.DateTEvent = DateTime.Now;
				measureEvent.EventType = EhrMeasureEventType.SummaryOfCareProvidedToDr;
				measureEvent.FKey=FrmReferralsPatient.RefAttachNum;
				measureEvent.PatNum = PatCur.PatNum;
				EhrMeasureEvents.Insert(measureEvent);
				FillGridSent();
			}		
		}

		private void butShowXml_Click(object sender,EventArgs e) {
			string ccd="";
			try {
				ccd=EhrCCD.GenerateSummaryOfCare(PatCur,out string warnings);
				if(!string.IsNullOrEmpty(warnings)) {
					if(MessageBox.Show(warnings,"Warnings",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
						return;
					}
				}
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			using MsgBoxCopyPaste msgbox=new MsgBoxCopyPaste(ccd);
			msgbox.ShowDialog();
		}

		private void butRecEmail_Click(object sender,EventArgs e) {
			using FormEmailInbox form=new FormEmailInbox();
			form.ShowDialog();
		}

		private void butRecFile_Click(object sender,EventArgs e) {
			using OpenFileDialog dlg=new OpenFileDialog();
			DialogResult result=dlg.ShowDialog();
			if(result!=DialogResult.OK){
				return;
			}
			string text=File.ReadAllText(dlg.FileName);
			EhrSummaryCcd ehrSummaryCcd=new EhrSummaryCcd();
			ehrSummaryCcd.ContentSummary=text;
			ehrSummaryCcd.DateSummary=DateTime.Today;
			ehrSummaryCcd.PatNum=PatCur.PatNum;
			EhrSummaryCcds.Insert(ehrSummaryCcd);
			FillGridRec();
			DisplayCCD(text,PatCur);
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(gridSent.SelectedIndices.Length < 1) {
				MessageBox.Show("Please select at least one record to delete.");
				return;
			}
			for(int i=0;i<gridSent.SelectedIndices.Length;i++) {
				EhrMeasureEvents.Delete(_listHistorySent[gridSent.SelectedIndices[i]].EhrMeasureEventNum);
			}
			FillGridSent();
		}

	}
}

#region OldCode
/*
		public static StringBuilder GenerateCCD(Patient pat) {
			XmlWriterSettings xmlSettings=new XmlWriterSettings();
			xmlSettings.Encoding=Encoding.UTF8;
			xmlSettings.OmitXmlDeclaration=true;
			xmlSettings.Indent=true;
			xmlSettings.IndentChars="   ";
			StringBuilder strBuilder=new StringBuilder();
			using(XmlWriter writer=XmlWriter.Create(strBuilder,xmlSettings)){
				//Begin Clinical Document
				writer.WriteRaw("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n");
				writer.WriteStartElement("ClinicalDocument","urn:hl7-org:v3");
					// Asserting use of the HL7 Guide for CDA R2 - CCD
					writer.WriteStartElement("typeId");
						writer.WriteAttributeString("extension","POCD_HD0000040");
						writer.WriteAttributeString("root","2.16.840.1.113883.1.3");
				  writer.WriteEndElement();
				//Michael:Do I need this here?
					//writer.WriteStartElement("templateId");
					//  writer.WriteAttributeString("root","2.16.840.1.113883.10.20.1");
					//writer.WriteEndElement();
					// Header---------------------------------------------------------------------------------------------
					// 2.1 CCR Unique Identifier
				  writer.WriteStartElement("id");
				  writer.WriteEndElement();
					writer.WriteStartElement("code");
						writer.WriteAttributeString("code","34133-9");
						writer.WriteAttributeString("codeSystemName","LOINC");
						writer.WriteAttributeString("codeSystem","2.16.840.1.113883.6.1");
						writer.WriteAttributeString("displayName","Summary of episode note");
				  writer.WriteEndElement();
					writer.WriteStartElement("documentationOf");
						writer.WriteStartElement("serviceEvent");
							writer.WriteAttributeString("classCode","PCPR");
							string effectiveTimeHigh=DateTime.Now.ToString("yyyyMMddHHmmsszzz").Replace(":","");
							string effectiveTimeLow=pat.Birthdate.ToString("yyyyMMddHHmmsszzz").Replace(":","");
							writer.WriteStartElement("effectiveTime");
								writer.WriteStartElement("high");
									writer.WriteAttributeString("value",effectiveTimeHigh);
								writer.WriteEndElement();//End high
								writer.WriteStartElement("low");
									writer.WriteAttributeString("value",effectiveTimeLow);
								writer.WriteEndElement();//End low
							writer.WriteEndElement();//End effectiveTime
						writer.WriteEndElement();
					writer.WriteEndElement();//End documentationOf
					// 2.2 Language
					writer.WriteStartElement("languageCode");
						writer.WriteAttributeString("value","en-US");
					writer.WriteEndElement();
					// 2.3 Version
				  writer.WriteStartElement("templateId");
				    writer.WriteAttributeString("root","2.16.840.1.113883.10.20.1");
				  writer.WriteEndElement();
					// 2.4 CCR Creation Date/Time
					writer.WriteStartElement("effectiveTime");
						string ccrCreationDateTime=DateTime.Now.ToString("yyyyMMddHHmmsszzz").Replace(":","");
						writer.WriteAttributeString("value",ccrCreationDateTime);
					writer.WriteEndElement();
					// 2.5 Patient
					writer.WriteStartElement("recordTarget");
						writer.WriteStartElement("patientRole");
							writer.WriteStartElement("id");
								writer.WriteAttributeString("value",pat.PatNum.ToString());
							writer.WriteEndElement();
							writer.WriteStartElement("addr");
								writer.WriteAttributeString("use","HP");
								writer.WriteStartElement("streetAddressLine");
									writer.WriteString(pat.Address.ToString());
								writer.WriteEndElement();
								writer.WriteStartElement("streetAddressLine");
									writer.WriteString(pat.Address2.ToString());
								writer.WriteEndElement();
								writer.WriteStartElement("city");
									writer.WriteString(pat.City.ToString());
								writer.WriteEndElement();
								writer.WriteStartElement("state");
									writer.WriteString(pat.State.ToString());
								writer.WriteEndElement();
								writer.WriteStartElement("country");
									writer.WriteString("");
								writer.WriteEndElement();
							writer.WriteEndElement();//End addr
							writer.WriteStartElement("patient");
								writer.WriteStartElement("name");
									writer.WriteAttributeString("use","L");
									writer.WriteStartElement("given");
										writer.WriteString(pat.FName.ToString());
									writer.WriteEndElement();
									writer.WriteStartElement("given");
										writer.WriteString(pat.MiddleI.ToString());
									writer.WriteEndElement();
									writer.WriteStartElement("family");
										writer.WriteString(pat.LName.ToString());
									writer.WriteEndElement();
									writer.WriteStartElement("suffix");
										writer.WriteAttributeString("qualifier","TITLE");
										writer.WriteString(pat.Title.ToString());
									writer.WriteEndElement();//End suffix
								writer.WriteEndElement();//End name
							writer.WriteEndElement();//End patient
						writer.WriteEndElement();//End patientRole
						writer.WriteStartElement("text");//The following text will be parsed as html with a style sheet to be human readable.
							writer.WriteStartElement("table");
								writer.WriteAttributeString("width","100%");
								writer.WriteAttributeString("border","1");
								writer.WriteStartElement("thead");
									writer.WriteStartElement("tr");
										writer.WriteStartElement("th");
										  writer.WriteString("Name");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Date of Birth");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Gender");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Identification Number");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Identification Number Type");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Address/Phone");
										writer.WriteEndElement();
									writer.WriteEndElement();//End tr
								writer.WriteEndElement();//End thead
								writer.WriteStartElement("tbody");
									writer.WriteStartElement("tr");
										writer.WriteStartElement("td");//Name
											writer.WriteString(pat.LName+", "+pat.FName+" "+pat.MiddleI);
										writer.WriteEndElement();
										writer.WriteStartElement("td");//DoB
											writer.WriteString(pat.Birthdate.ToShortDateString());
										writer.WriteEndElement();
										writer.WriteStartElement("td");//Gender
											writer.WriteString(pat.Gender.ToString());
										writer.WriteEndElement();
										writer.WriteStartElement("td");//PatNum
											writer.WriteString(pat.PatNum.ToString());
										writer.WriteEndElement();
										writer.WriteStartElement("td");//"Open PMS - Dental PatNum"
											writer.WriteString("Open PMS - Dental PatNum");
										writer.WriteEndElement();
										writer.WriteStartElement("td");//Address/Phone
											writer.WriteString(pat.Address+" "+pat.Address2+"\r\n"+pat.City+", "
												+pat.State+"\r\n"+pat.Zip+"\r\n"+pat.HmPhone);
										writer.WriteEndElement();
									writer.WriteEndElement();//End tr
								writer.WriteEndElement();//End tbody
							writer.WriteEndElement();//End table
						writer.WriteEndElement();//End text
					writer.WriteEndElement();//End recordTarget
					// 2.6 From
					writer.WriteStartElement("author");
						writer.WriteStartElement("assignedAuthor");
							writer.WriteStartElement("assignedPerson");
								writer.WriteElementString("name","Auto Generated");
							writer.WriteEndElement();//End assignedPerson
						writer.WriteEndElement();//End assignedAuthor
					writer.WriteEndElement();//End author
					// Body--------------------------------------------------------------------------------------------
					// 3.5 Problems
					List<Disease> listProblem=Diseases.Refresh(pat.PatNum);
					int condID=1;//used to set the CondID-# in the problem list of the html table in the text section of this document.
					ICD9 icd9;
					writer.WriteStartElement("component");
						writer.WriteComment("Problems");
						writer.WriteStartElement("section");
							writer.WriteStartElement("templateId");
								writer.WriteAttributeString("root","2.16.840.1.113883.10.20.1.11");
								writer.WriteAttributeString("assigningAuthorityName","HL7 CCD");
							writer.WriteEndElement();
							writer.WriteComment("Problems section template");
							writer.WriteStartElement("code");
								writer.WriteAttributeString("code","11450-4");
								writer.WriteAttributeString("codeSystemName","LOINC");
								writer.WriteAttributeString("codeSystem","2.16.840.1.113883.6.1");
								writer.WriteAttributeString("displayName","Problem list");
						writer.WriteEndElement();
						writer.WriteStartElement("title");
							writer.WriteString("Problems");
						writer.WriteEndElement();
						writer.WriteStartElement("text");//The following text will be parsed as html with a style sheet to be human readable.
							writer.WriteStartElement("table");
								writer.WriteAttributeString("width","100%");
								writer.WriteAttributeString("border","1");
								writer.WriteStartElement("thead");
									writer.WriteStartElement("tr");
										writer.WriteStartElement("th");
										  writer.WriteString("ICD-9 Code");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Patient Problem");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Date Diagnosed");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Status");
										writer.WriteEndElement();
									writer.WriteEndElement();//End tr
								writer.WriteEndElement();//End thead
								writer.WriteStartElement("tbody");
									for(int i=0;i<listProblem.Count;i++){
										if(listProblem[i].ICD9Num!=0){
											icd9=ICD9s.GetOne(listProblem[i].ICD9Num);
											writer.WriteStartElement("tr");
												writer.WriteAttributeString("ID","CondID-"+condID);
												writer.WriteStartElement("td");
												  writer.WriteString(icd9.ICD9Code.Substring(0,3)+"."+icd9.ICD9Code.Substring(3));
												writer.WriteEndElement();
												writer.WriteStartElement("td");
													writer.WriteString(icd9.Description);
												writer.WriteEndElement();
												writer.WriteStartElement("td");
													writer.WriteString(listProblem[i].DateStart.ToShortDateString());
												writer.WriteEndElement();
												writer.WriteStartElement("td");
													writer.WriteString(listProblem[i].ProbStatus.ToString());
												writer.WriteEndElement();
											writer.WriteEndElement();//End tr
										}
									}
								writer.WriteEndElement();//End tbody
							writer.WriteEndElement();//End table
						writer.WriteEndElement();//End text
					writer.WriteEndElement();//End Problem component
					// 3.8 Alerts (for Allergies)
					List<Allergy> listAllergy=Allergies.Refresh(pat.PatNum);
					AllergyDef allergyDef;
					Medication med;
					writer.WriteStartElement("component");
						writer.WriteComment("Alerts");
						writer.WriteStartElement("section");
							writer.WriteStartElement("templateId");
								writer.WriteAttributeString("root","2.16.840.1.113883.10.20.1.2");
								writer.WriteAttributeString("assigningAuthorityName","HL7 CCD");
							writer.WriteEndElement();
							writer.WriteComment("Alerts section template");
							writer.WriteStartElement("code");
								writer.WriteAttributeString("code","48765-2");
								writer.WriteAttributeString("codeSystemName","LOINC");
								writer.WriteAttributeString("codeSystem","2.16.840.1.113883.6.1");
								writer.WriteAttributeString("displayName","Allergies, adverse reactions, alerts");
						writer.WriteEndElement();
						writer.WriteStartElement("title");
							writer.WriteString("Allergies and Adverse Reactions");
						writer.WriteEndElement();
						writer.WriteStartElement("text");//The following text will be parsed as html with a style sheet to be human readable.
							writer.WriteStartElement("table");
								writer.WriteAttributeString("width","100%");
								writer.WriteAttributeString("border","1");
								writer.WriteStartElement("thead");
									writer.WriteStartElement("tr");
										writer.WriteStartElement("th");
										  writer.WriteString("SNOMED Allergy Type Code");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Medication/Agent Allergy");
										writer.WriteEndElement();//
										writer.WriteStartElement("th");
											writer.WriteString("Reaction");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Adverse Event Date");
										writer.WriteEndElement();
									writer.WriteEndElement();//End tr
								writer.WriteEndElement();//End thead
								writer.WriteStartElement("tbody");
									for(int i=0;i<listAllergy.Count;i++){
										allergyDef=AllergyDefs.GetOne(listAllergy[i].AllergyDefNum);
										writer.WriteStartElement("tr");
											writer.WriteStartElement("td");
											  writer.WriteString(AllergyDefs.GetSnomedAllergyDesc(allergyDef.Snomed));
											writer.WriteEndElement();
											writer.WriteStartElement("td");
												med=Medications.GetMedication(allergyDef.MedicationNum);
											  writer.WriteString(med.RxCui.ToString()+" - "+med.MedName);
											writer.WriteEndElement();
											writer.WriteStartElement("td");
												writer.WriteString(listAllergy[i].Reaction);
											writer.WriteEndElement();
											writer.WriteStartElement("td");
												writer.WriteString(listAllergy[i].DateAdverseReaction.ToShortDateString());
											writer.WriteEndElement();
										writer.WriteEndElement();//End tr
									}
								writer.WriteEndElement();//End tbody
							writer.WriteEndElement();//End table
						writer.WriteEndElement();//End text
					writer.WriteEndElement();//End Alerts component
					// 3.9 Medications
					List<MedicationPat> listMedPat=MedicationPats.Refresh(pat.PatNum,true);
					writer.WriteStartElement("component");
						writer.WriteComment("Medications");
						writer.WriteStartElement("section");
							writer.WriteStartElement("templateId");
								writer.WriteAttributeString("root","2.16.840.1.113883.10.20.1.8");
								writer.WriteAttributeString("assigningAuthorityName","HL7 CCD");
							writer.WriteEndElement();
							writer.WriteComment("Medications section template");
							writer.WriteStartElement("code");
								writer.WriteAttributeString("code","10160-0");
								writer.WriteAttributeString("codeSystemName","LOINC");
								writer.WriteAttributeString("codeSystem","2.16.840.1.113883.6.1");
								writer.WriteAttributeString("displayName","History of medication use");
						writer.WriteEndElement();
						writer.WriteStartElement("title");
							writer.WriteString("Medications");
						writer.WriteEndElement();
						writer.WriteStartElement("text");//The following text will be parsed as html with a style sheet to be human readable.
							writer.WriteStartElement("table");
								writer.WriteAttributeString("width","100%");
								writer.WriteAttributeString("border","1");
								writer.WriteStartElement("thead");
									writer.WriteStartElement("tr");
										writer.WriteStartElement("th");
										  writer.WriteString("RxNorm Code");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
										  writer.WriteString("Product");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
										  writer.WriteString("Generic Name");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Brand Name");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Instructions");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Date Started");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Status");
										writer.WriteEndElement();
									writer.WriteEndElement();//End tr
								writer.WriteEndElement();//End thead
								writer.WriteStartElement("tbody");
									for(int i=0;i<listMedPat.Count;i++){
										med=Medications.GetMedication(listMedPat[i].MedicationNum);
										writer.WriteStartElement("tr");
											writer.WriteStartElement("td");
											  writer.WriteString(med.RxCui.ToString());//RxNorm Code
											writer.WriteEndElement();
											writer.WriteStartElement("td");
											  writer.WriteString("Medication");//Product
											writer.WriteEndElement();
											writer.WriteStartElement("td");
											  writer.WriteString(Medications.GetGenericName(med.GenericNum));//Generic Name
											writer.WriteEndElement();
											writer.WriteStartElement("td");
											  writer.WriteString(med.MedName);//Brand Name
											writer.WriteEndElement();
											writer.WriteStartElement("td");
												writer.WriteString(listMedPat[i].PatNote);//Instruction
											writer.WriteEndElement();
											writer.WriteStartElement("td");
												writer.WriteString(listMedPat[i].DateStart.ToShortDateString());//Date Started
											writer.WriteEndElement();
											writer.WriteStartElement("td");
										writer.WriteString(MedicationPat.IsMedActive(listMedPat[i])?"Active":"Inactive");//Status
											writer.WriteEndElement();
										writer.WriteEndElement();//End tr
									}
								writer.WriteEndElement();//End tbody
							writer.WriteEndElement();//End table
						writer.WriteEndElement();//End text
					writer.WriteEndElement();//End Medication component
					// 3.13 Results
					List<LabResult> listLabResult=LabResults.GetAllForPatient(pat.PatNum);
					writer.WriteStartElement("component");
						writer.WriteComment("Results");
						writer.WriteStartElement("section");
							writer.WriteStartElement("templateId");
								writer.WriteAttributeString("root","2.16.840.1.113883.10.20.1.14");
								writer.WriteAttributeString("assigningAuthorityName","HL7 CCD");
							writer.WriteEndElement();
							writer.WriteComment("Relevant diagnostic tests and/or labratory data");
							writer.WriteStartElement("code");
								writer.WriteAttributeString("code","30954-2");
								writer.WriteAttributeString("codeSystemName","LOINC");
								writer.WriteAttributeString("codeSystem","2.16.840.1.113883.6.1");
								writer.WriteAttributeString("displayName","Allergies, adverse reactions, alerts");
						writer.WriteEndElement();
						writer.WriteStartElement("title");
							writer.WriteString("Results");
						writer.WriteEndElement();
						writer.WriteStartElement("text");//The following text will be parsed as html with a style sheet to be human readable.
							writer.WriteStartElement("table");
								writer.WriteAttributeString("width","100%");
								writer.WriteAttributeString("border","1");
								writer.WriteStartElement("thead");
									writer.WriteStartElement("tr");
										writer.WriteStartElement("th");
										  writer.WriteString("LOINC Code");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Test");
										writer.WriteEndElement();//
										writer.WriteStartElement("th");
											writer.WriteString("Result");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Abnormal Flag");
										writer.WriteEndElement();
										writer.WriteStartElement("th");
											writer.WriteString("Date Performed");
										writer.WriteEndElement();
									writer.WriteEndElement();//End tr
								writer.WriteEndElement();//End thead
								writer.WriteStartElement("tbody");
									for(int i=0;i<listLabResult.Count;i++){
										writer.WriteStartElement("tr");
										writer.WriteStartElement("td");
										writer.WriteString(listLabResult[i].TestID.ToString());
										writer.WriteEndElement();
										writer.WriteStartElement("td");
										writer.WriteString(listLabResult[i].TestName);
										writer.WriteEndElement();
										writer.WriteStartElement("td");
										writer.WriteString(listLabResult[i].AbnormalFlag.ToString());
										writer.WriteEndElement();
										writer.WriteStartElement("td");
										writer.WriteString(listLabResult[i].DateTimeTest.ToShortDateString());
										writer.WriteEndElement();
										writer.WriteEndElement();//End tr
									}
								writer.WriteEndElement();//End tbody
							writer.WriteEndElement();//End table
						writer.WriteEndElement();//End text
					writer.WriteEndElement();//End Diagnostic Test Results component
				writer.WriteEndElement();
				return strBuilder;
			}
		}*/
#endregion OldCode