using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>The desktop side of chart letters (24.4): creating one from a letter merge template, opening a read-only copy, editing with
	///version tracking, deleting, the properties window and the PDF copy for the Imaging module. Business rules are in ChartLetters.</summary>
	public class ChartLetterL {
		private const string LAN="ChartLetters";
		private static readonly object _lock=new object();
		///<summary>Letters open for editing in this program run. Each one watches its working copy.</summary>
		private static readonly List<ChartLetterEditSession> _listSessions=new List<ChartLetterEditSession>();
		///<summary>DocNums whose hash mismatch was already logged in this program run, so opening a letter repeatedly does not flood the log.</summary>
		private static readonly HashSet<long> _hashSetMismatchLogged=new HashSet<long>();
		private static bool _isEditHelpShown;
		///<summary>Raised with the PatNum whenever a chart letter is created, gets a new version, is deleted or has its properties changed. May be
		///raised on a background thread.</summary>
		public static event EventHandler<long> ChartLetterChanged;

		///<summary>Folder for the working and read-only copies. Kept apart from the program's temporary folder, which is cleaned up at times.
		///</summary>
		public static string GetWorkingRoot() {
			return Path.Combine(Path.GetTempPath(),"ChartLetters");
		}

		#region Create
		///<summary>Creates a chart letter for the patient from the letter merge template and offers to open it for editing. Returns null if
		///nothing was created. Needs the procedure-complete-create permission.</summary>
		public static Document CreateFromTemplate(Patient patient,LetterMerge letterMerge) {
			if(patient==null || letterMerge==null) {
				return null;
			}
			if(!Security.IsAuthorized(ChartLetters.PERM_CREATE,DateTime.Today)) {
				return null;
			}
			string templateFile=ODFileUtils.CombinePaths(PrefC.GetString(PrefName.LetterMergePath),letterMerge.TemplateName);
			if(!File.Exists(templateFile)) {
				MsgBox.Show(Lan.g(LAN,"Template file does not exist:")+" "+templateFile);
				return null;
			}
			DataTable tableReadable;
			try {
				tableReadable=FormQuery.MakeReadable(LetterMergesQueries.GetLetterMergeInfo(patient,letterMerge),null,false);
			}
			catch(Exception ex) {
				MsgBox.Show(Lan.g(LAN,"There was an error getting letter merge info:")+"\r\n"+ex.Message);
				return null;
			}
			Dictionary<string,string> dictValues=LetterMergesQueries.GetMergeValues(letterMerge,tableReadable);
			byte[] arrayBytes;
			string extension;
			Cursor.Current=Cursors.WaitCursor;
			try {
				if(DocxMailMerge.IsSupportedTemplate(templateFile)) {
					List<string> listUnknownFields;
					arrayBytes=DocxMailMerge.Merge(File.ReadAllBytes(templateFile),dictValues,out listUnknownFields);
					extension=DocxMailMerge.GetOutputExtension(templateFile);
					if(listUnknownFields.Count>0) {
						MsgBox.Show(Lan.g(LAN,"These merge fields in the template are not fields of this letter and were left empty:")+"\r\n"
							+string.Join(", ",listUnknownFields));
					}
				}
				else {
					arrayBytes=MergeWithWordProcessor(templateFile,letterMerge,tableReadable);
					extension=".docx";
					if(arrayBytes==null) {
						return null;
					}
				}
			}
			catch(Exception ex) {
				Cursor.Current=Cursors.Default;
				FriendlyException.Show(Lan.g(LAN,"The chart letter could not be merged from the template."),ex);
				return null;
			}
			finally {
				Cursor.Current=Cursors.Default;
			}
			string patFolder=GetPatFolder(patient);
			if(patFolder==null) {
				return null;
			}
			long provNum=Security.CurUser.ProvNum;
			if(provNum==0) {
				provNum=patient.PriProv;
			}
			Document document;
			try {
				document=ChartLetters.CreateLetter(patient,arrayBytes,extension,letterMerge.Description,provNum,Security.CurUser.UserNum,DateTime.Now,
					patFolder);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(LAN,"The chart letter could not be saved."),ex);
				return null;
			}
			RaiseChanged(patient.PatNum);
			if(MsgBox.Show(MsgBoxButtons.YesNo,Lan.g(LAN,"Chart letter created. Open it for editing now?"))) {
				StartEdit(document,patient);
			}
			return document;
		}

		///<summary>Merges an older binary template (.doc, .dot) with the word processor's automation interface, saving the result as .docx.
		///Returns null, after telling the user, if no word processor automation is available.</summary>
		private static byte[] MergeWithWordProcessor(string templateFile,LetterMerge letterMerge,DataTable tableReadable) {
#if DISABLE_MICROSOFT_OFFICE
			MsgBox.Show(Lan.g(LAN,"This template is in an older format that needs a word processor to merge. Save the template in the .docx format "
				+"and select that file in the letter's setup."));
			return null;
#else
			string folder=Path.Combine(GetWorkingRoot(),"Merge_"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));
			Directory.CreateDirectory(folder);
			string dataFile=Path.Combine(folder,"data.txt");
			WriteDataFile(dataFile,letterMerge,tableReadable);
			string outputFile=Path.Combine(folder,"letter.docx");
			Word.Application wordApplication;
			try {
				wordApplication=LetterMerges.GetWordApp();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(LAN,"This template is in an older format that needs a word processor to merge, and none could be started. "
					+"Save the template in the .docx format and select that file in the letter's setup."),ex);
				return null;
			}
			object objectMissing=Missing.Value;
			object objectFalse=false;
			object objectTemplate=templateFile;
			Word._Document wordDocument=wordApplication.Documents.Open(ref objectTemplate,ref objectMissing,ref objectMissing,ref objectMissing,
				ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,
				ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing);
			try {
				wordDocument.MailMerge.OpenDataSource(dataFile,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,
					ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,ref objectMissing,
					ref objectMissing,ref objectMissing,ref objectMissing);
				wordDocument.MailMerge.Destination=Word.WdMailMergeDestination.wdSendToNewDocument;
				wordDocument.MailMerge.Execute(ref objectFalse);
				object objectOutput=outputFile;
				object objectFormat=16;//The word processor's default document format, which is .docx.
				wordApplication.ActiveDocument.SaveAs(ref objectOutput,ref objectFormat);
				wordApplication.ActiveDocument.Close();
			}
			finally {
				wordDocument.Saved=true;
				wordDocument.Close(ref objectFalse,ref objectMissing,ref objectMissing);
				wordApplication.WindowState=Word.WdWindowState.wdWindowStateMinimize;
			}
			return File.ReadAllBytes(outputFile);
#endif
		}

		///<summary>The tab-delimited data file a word processor's mail merge reads: one header line of data field names, then the patient's row.
		///Same format as the Letter Merge window writes.</summary>
		private static void WriteDataFile(string fileName,LetterMerge letterMerge,DataTable tableReadable) {
			List<string> listFields=letterMerge.Fields;
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(string.Join("\t",listFields.Select(x => LetterMergesQueries.GetDataFieldName(x))));
			for(int i=0;i<tableReadable.Rows.Count;i++) {
				List<string> listCells=new List<string>();
				for(int j=0;j<tableReadable.Columns.Count;j++) {
					listCells.Add(tableReadable.Rows[i][j].ToString().Replace("\r","").Replace("\n","").Replace("\t","").Replace("\"",""));
				}
				stringBuilder.AppendLine(string.Join("\t",listCells));
			}
			File.WriteAllText(fileName,stringBuilder.ToString());
		}
		#endregion Create

		#region Open, edit, delete
		///<summary>Opens a read-only copy of this version with the program registered for its file type. Warns when the stored file no longer matches
		///its hash.</summary>
		public static void OpenReadOnly(Document document,Patient patient) {
			string patFolder=GetPatFolder(patient);
			if(patFolder==null) {
				return;
			}
			byte[] arrayBytes=ChartLetters.GetFileBytes(document,patFolder);
			if(arrayBytes.Length==0) {
				MsgBox.Show(Lan.g(LAN,"The chart letter's file could not be found:")+" "+document.FileName);
				return;
			}
			WarnIfHashMismatch(document,arrayBytes);
			string folder=Path.Combine(GetWorkingRoot(),"ReadOnly_"+document.DocNum+"_"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));
			string path;
			try {
				Directory.CreateDirectory(folder);
				path=Path.Combine(folder,GetFileTitle(document)+" ("+Lan.g(LAN,"read-only")+")"+Path.GetExtension(document.FileName));
				File.WriteAllBytes(path,arrayBytes);
				File.SetAttributes(path,FileAttributes.ReadOnly);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(LAN,"Could not make a read-only copy of the chart letter."),ex);
				return;
			}
			StartProcess(path);
		}

		///<summary>Opens the current version for editing. Every save in the editor becomes a new version (new document row and file) while this
		///program runs. Needs the procedure note permission for the letter's date.</summary>
		public static void StartEdit(Document document,Patient patient) {
			Document documentDb=Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true);
			if(documentDb==null || !ChartLetters.IsChartLetter(documentDb)) {
				MsgBox.Show(Lan.g(LAN,"The chart letter no longer exists."));
				return;
			}
			if(ChartLetters.GetStatus(documentDb)!=EnumChartLetterStatus.Current) {
				MsgBox.Show(Lan.g(LAN,"Only the current version of a chart letter can be edited. Old versions and deleted letters are kept for the "
					+"audit trail."));
				return;
			}
			if(!Security.IsAuthorized(ChartLetters.PERM_EDIT,documentDb.DateCreated)) {
				return;
			}
			ChartLetterEditSession chartLetterEditSession;
			lock(_lock) {
				chartLetterEditSession=_listSessions.FirstOrDefault(x => x.DocumentCur.DocNum==documentDb.DocNum && File.Exists(x.WorkingFilePath));
			}
			if(chartLetterEditSession!=null) {
				StartProcess(chartLetterEditSession.WorkingFilePath);
				return;
			}
			string patFolder=GetPatFolder(patient);
			if(patFolder==null) {
				return;
			}
			byte[] arrayBytes=ChartLetters.GetFileBytes(documentDb,patFolder);
			if(arrayBytes.Length==0) {
				MsgBox.Show(Lan.g(LAN,"The chart letter's file could not be found:")+" "+documentDb.FileName);
				return;
			}
			WarnIfHashMismatch(documentDb,arrayBytes);
			try {
				chartLetterEditSession=ChartLetterEditSession.Start(documentDb,patient,patFolder,Security.CurUser.UserNum,GetWorkingRoot());
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(LAN,"The chart letter could not be opened for editing."),ex);
				return;
			}
			chartLetterEditSession.VersionSaved+=ChartLetterEditSession_VersionSaved;
			chartLetterEditSession.StartWatching();
			lock(_lock) {
				_listSessions.Add(chartLetterEditSession);
			}
			if(!_isEditHelpShown) {
				_isEditHelpShown=true;
				MsgBox.Show(Lan.g(LAN,"Each time you save the document in the editor, a new version of the chart letter is stored and the older one is "
					+"kept for the audit trail. Keep this program running until you have closed the document."));
			}
			StartProcess(chartLetterEditSession.WorkingFilePath);
		}

		private static void ChartLetterEditSession_VersionSaved(object sender,ChartLetterVersionEventArgs e) {
			if(e.Error!=null) {
				ShowOnUiThread(Lan.g(LAN,"A saved edit of the chart letter could not be stored as a new version:")+"\r\n"+e.Error.Message);
				return;
			}
			if(e.DocumentNew!=null) {
				RaiseChanged(e.DocumentNew.PatNum);
			}
		}

		///<summary>Soft-deletes the letter after confirming. Needs the procedure note permission for the letter's date. Returns true if deleted.
		///</summary>
		public static bool Delete(Document document) {
			Document documentDb=Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true);
			if(documentDb==null || !ChartLetters.IsChartLetter(documentDb)) {
				MsgBox.Show(Lan.g(LAN,"The chart letter no longer exists."));
				return false;
			}
			if(ChartLetters.GetStatus(documentDb)!=EnumChartLetterStatus.Current) {
				MsgBox.Show(Lan.g(LAN,"Only the current version of a chart letter can be deleted."));
				return false;
			}
			if(!Security.IsAuthorized(ChartLetters.PERM_EDIT,documentDb.DateCreated)) {
				return false;
			}
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,Lan.g(LAN,"Delete this chart letter? It stays visible in the audit trail (Audit checked)."))) {
				return false;
			}
			try {
				ChartLetters.Delete(documentDb,Security.CurUser.UserNum);
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
				return false;
			}
			EndSessionsFor(documentDb.DocNum);
			RaiseChanged(documentDb.PatNum);
			return true;
		}

		///<summary>Shows the Chart Letter Properties window. Returns true if something was saved.</summary>
		public static bool ShowProperties(Document document,Patient patient) {
			Document documentDb=Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true);
			if(documentDb==null || !ChartLetters.IsChartLetter(documentDb)) {
				MsgBox.Show(Lan.g(LAN,"The chart letter no longer exists."));
				return false;
			}
			FrmChartLetterProperties frmChartLetterProperties=new FrmChartLetterProperties(documentDb,patient);
			frmChartLetterProperties.FuncSavePdfCopy=(documentLetter,defNumImageCat) => SavePdfCopy(documentLetter,patient,defNumImageCat);
			frmChartLetterProperties.ShowDialog();
			if(frmChartLetterProperties.IsDialogOK) {
				RaiseChanged(documentDb.PatNum);
				return true;
			}
			return false;
		}
		#endregion Open, edit, delete

		#region PDF copy
		///<summary>Stores a PDF copy of the letter in the image category, so it can be sent or printed from the Imaging module. Converting needs a
		///word processor with an automation interface; without one, the user may store a copy of the document file instead. Returns the new
		///Imaging document, or null.</summary>
		public static Document SavePdfCopy(Document document,Patient patient,long defNumImageCat) {
			if(defNumImageCat==0) {
				MsgBox.Show(Lan.g(LAN,"Please select an image category first."));
				return null;
			}
			if(!Security.IsAuthorized(EnumPermType.ImageCreate)) {
				return null;
			}
			string patFolder=GetPatFolder(patient);
			if(patFolder==null) {
				return null;
			}
			byte[] arrayBytes=ChartLetters.GetFileBytes(document,patFolder);
			if(arrayBytes.Length==0) {
				MsgBox.Show(Lan.g(LAN,"The chart letter's file could not be found:")+" "+document.FileName);
				return null;
			}
			string folder=Path.Combine(GetWorkingRoot(),"Pdf_"+document.DocNum+"_"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));
			Directory.CreateDirectory(folder);
			string pathDocument=Path.Combine(folder,GetFileTitle(document)+Path.GetExtension(document.FileName));
			File.WriteAllBytes(pathDocument,arrayBytes);
			string pathPdf=Path.Combine(folder,GetFileTitle(document)+".pdf");
			string pathImport=pathPdf;
			Cursor.Current=Cursors.WaitCursor;
			bool isConverted=TryConvertToPdf(pathDocument,pathPdf);
			Cursor.Current=Cursors.Default;
			if(!isConverted) {
				if(!MsgBox.Show(MsgBoxButtons.YesNo,Lan.g(LAN,"No program on this computer could convert the letter to PDF. Store a copy of the document "
					+"file in the image category instead?")))
				{
					return null;
				}
				pathImport=pathDocument;
			}
			Document documentCopy;
			try {
				documentCopy=ImageStore.Import(pathImport,defNumImageCat,patient);
				Document documentCopyOld=documentCopy.Copy();
				documentCopy.Description=ChartLetters.GetProgNoteDescription(document.Description,(byte)EnumChartLetterStatus.Current);
				documentCopy.ProvNum=document.ProvNum;
				Documents.Update(documentCopy,documentCopyOld);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(LAN,"The copy could not be stored in the Imaging module."),ex);
				return null;
			}
			return documentCopy;
		}

		///<summary>Converts with a word processor that offers the common "Word.Application" automation interface, late bound so nothing is needed at
		///build time. False if none is installed or the conversion failed.</summary>
		private static bool TryConvertToPdf(string pathDocument,string pathPdf) {
			Type type=Type.GetTypeFromProgID("Word.Application");
			if(type==null) {
				return false;
			}
			object application=null;
			try {
				application=Activator.CreateInstance(type);
				object documents=type.InvokeMember("Documents",BindingFlags.GetProperty,null,application,null);
				object documentWord=documents.GetType().InvokeMember("Open",BindingFlags.InvokeMethod,null,documents,new object[] { pathDocument,false,true });
				try {
					documentWord.GetType().InvokeMember("SaveAs",BindingFlags.InvokeMethod,null,documentWord,new object[] { pathPdf,17 });//17=PDF
				}
				finally {
					documentWord.GetType().InvokeMember("Close",BindingFlags.InvokeMethod,null,documentWord,new object[] { false });
				}
				return File.Exists(pathPdf);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
			finally {
				if(application!=null) {
					try {
						type.InvokeMember("Quit",BindingFlags.InvokeMethod,null,application,new object[] { false });
					}
					catch(Exception ex) {
						ex.DoNothing();
					}
				}
			}
		}
		#endregion PDF copy

		#region Helpers
		///<summary>Warns once per letter version when the stored file no longer matches its hash, and logs it once per program run.</summary>
		public static void WarnIfHashMismatch(Document document,byte[] arrayBytes) {
			if(ChartLetters.GetHashMatch(document,arrayBytes)!=EnumChartLetterHashMatch.NoMatch) {
				return;
			}
			bool isNew;
			lock(_lock) {
				isNew=_hashSetMismatchLogged.Add(document.DocNum);
			}
			if(isNew) {
				ChartLetters.LogHashMismatch(document,Security.CurUser.UserNum);
			}
			MsgBox.Show(Lan.g(LAN,"Warning: this chart letter's file no longer matches its stored hash, so it was changed outside the program."));
		}

		///<summary>Null, after showing the error, if the patient's folder cannot be found or made. Blank when images are stored in the database.
		///</summary>
		private static string GetPatFolder(Patient patient) {
			try {
				return ImageStore.GetPatientFolder(patient,ImageStore.GetPreferredAtoZpath());
			}
			catch(Exception ex) {
				FriendlyException.Show(ex.Message,ex);
				return null;
			}
		}

		private static string GetFileTitle(Document document) {
			string title=ODFileUtils.CleanFileName(document.Description??"").Trim();
			if(title=="") {
				title=Lan.g(LAN,"Chart Letter");
			}
			return title;
		}

		private static void StartProcess(string path) {
			try {
				Process.Start(path);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(LAN,"No program could open the file. Install a program that opens this file type:")+" "
					+Path.GetExtension(path),ex);
			}
		}

		///<summary>Stops watching the working copies of this letter, for example after it was deleted.</summary>
		private static void EndSessionsFor(long docNum) {
			List<ChartLetterEditSession> listEnded;
			lock(_lock) {
				listEnded=_listSessions.FindAll(x => x.DocumentCur.DocNum==docNum);
				_listSessions.RemoveAll(x => x.DocumentCur.DocNum==docNum);
			}
			listEnded.ForEach(x => x.Dispose());
		}

		private static void RaiseChanged(long patNum) {
			ChartLetterChanged?.Invoke(null,patNum);
		}

		private static void ShowOnUiThread(string text) {
			Form form=Application.OpenForms.Cast<Form>().FirstOrDefault(x => !x.IsDisposed && x.IsHandleCreated);
			if(form==null) {
				return;
			}
			form.BeginInvoke((Action)(() => MsgBox.Show(text)));
		}
		#endregion Helpers
	}
}
