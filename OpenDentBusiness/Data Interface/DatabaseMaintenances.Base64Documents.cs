// Database Maintenance tool "Create Documents From Base64 Notes" (Open Dental 25.4, backported to 25.3.39; backlog package B-DBM, implemented
// once for v25_3-tools-dbm-base64-docs and v25_4-tools-dbm-base64-documents). Open Dental's API stored an uploaded file in document.Note as
// "_rawBase64_:" + extension + "_" + base64 data, and the Images module made the real file only when someone opened that document
// (ControlImageDisplay.DownloadDocumentNoteFile). This tool does the same for every such document at once. "_download_:" notes would fetch a file
// from the internet and are left alone.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenDentBusiness {
	public partial class DatabaseMaintenances {
		///<summary>The note prefix of a document whose file is still stored as base64 text in its note.</summary>
		public const string DOCUMENT_NOTE_PREFIX_BASE64="_rawBase64_:";
		///<summary>The note prefix of a document whose file is still to be downloaded from a URL. Never processed here.</summary>
		public const string DOCUMENT_NOTE_PREFIX_DOWNLOAD="_download_:";

		///<summary>DocNums of the documents whose note starts with "_rawBase64_:" (case-sensitive), oldest first.</summary>
		public static List<long> GetDocNumsWithBase64Notes() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod());
			}
			//LIKE narrows the scan cheaply but ignores case; the binary comparison makes the prefix exact.
			string command="SELECT DocNum FROM document WHERE Note LIKE '\\_rawBase64\\_:%' "
				+"AND BINARY LEFT(Note,"+DOCUMENT_NOTE_PREFIX_BASE64.Length+")='"+POut.String(DOCUMENT_NOTE_PREFIX_BASE64)+"' "
				+"ORDER BY DocNum";
			return Db.GetListLong(command);
		}

		///<summary>How many documents have a "_download_:" note, which this tool leaves alone.</summary>
		public static long CountDocumentsWithDownloadNotes() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod());
			}
			string command="SELECT COUNT(*) FROM document WHERE Note LIKE '\\_download\\_:%' "
				+"AND BINARY LEFT(Note,"+DOCUMENT_NOTE_PREFIX_DOWNLOAD.Length+")='"+POut.String(DOCUMENT_NOTE_PREFIX_DOWNLOAD)+"'";
			return PIn.Long(Db.GetCount(command));
		}

		///<summary>Reads a "_rawBase64_:" note: the extension (returned with its leading dot) and the file bytes. False, with the reason in error, when
		///the note has no such prefix, no extension, an extension that is not 1 to 10 letters or digits, base64 that does not decode, or no data.
		///</summary>
		public static bool TryParseBase64DocumentNote(string note,out string extension,out byte[] arrayBytes,out string error) {
			Meth.NoCheckMiddleTierRole();
			extension="";
			arrayBytes=null;
			error="";
			if(note==null || !note.StartsWith(DOCUMENT_NOTE_PREFIX_BASE64,StringComparison.Ordinal)) {
				error=Lans.g("DatabaseMaintenance","The note does not start with")+" "+DOCUMENT_NOTE_PREFIX_BASE64;
				return false;
			}
			string rest=note.Substring(DOCUMENT_NOTE_PREFIX_BASE64.Length);
			int idxUnderscore=rest.IndexOf('_');//Base64 never contains an underscore, so the first one ends the extension.
			if(idxUnderscore<0) {
				error=Lans.g("DatabaseMaintenance","No file extension before the data.");
				return false;
			}
			string ext=rest.Substring(0,idxUnderscore).Trim();
			if(ext.StartsWith(".")) {
				ext=ext.Substring(1);
			}
			if(!Regex.IsMatch(ext,"^[A-Za-z0-9]{1,10}$")) {
				error=Lans.g("DatabaseMaintenance","Invalid file extension:")+" "+rest.Substring(0,idxUnderscore);
				return false;
			}
			try {
				arrayBytes=Convert.FromBase64String(rest.Substring(idxUnderscore+1));//Ignores white space and line breaks.
			}
			catch(FormatException) {
				arrayBytes=null;
				error=Lans.g("DatabaseMaintenance","The data is not valid base64.");
				return false;
			}
			if(arrayBytes.Length==0) {
				arrayBytes=null;
				error=Lans.g("DatabaseMaintenance","The note holds no data.");
				return false;
			}
			extension="."+ext.ToLower();
			return true;
		}

		///<summary>For each document with a "_rawBase64_:" note: decodes the bytes, imports them into the patient's image folder (or the database, for
		///in-database storage) with the stored extension through ImageStore.Import, copies Description, DocCategory, ImgType, DateCreated, ToothNumbers,
		///ProvNum and PrintHeading to the new document, and deletes the placeholder row. An existing file is never overwritten: the new file gets a
		///numbered suffix. Rows that can't be converted are skipped and listed. Import makes one ImageCreate log entry per document. Runs on the
		///workstation, like the Images module, because it writes files through this computer's A to Z folder path.</summary>
		public static Base64DocumentsResult CreateDocumentsFromBase64Notes() {
			Meth.NoCheckMiddleTierRole();
			Base64DocumentsResult result=new Base64DocumentsResult();
			List<long> listDocNums=GetDocNumsWithBase64Notes();
			result.CountFound=listDocNums.Count;
			result.CountDownloadNotes=CountDocumentsWithDownloadNotes();
			for(int i=0;i<listDocNums.Count;i++) {
				ODEvent.Fire(ODEventType.ProgressBar,Lans.g("DatabaseMaintenance","Creating document")+" "+(i+1)+" / "+listDocNums.Count);
				Document document=Documents.GetByNum(listDocNums[i],doReturnNullIfNotFound:true);
				if(document==null || document.DocNum==0 || !document.Note.StartsWith(DOCUMENT_NOTE_PREFIX_BASE64,StringComparison.Ordinal)) {
					continue;//Converted or deleted meanwhile, for example by someone opening it in the Images module.
				}
				string error=CreateDocumentFromBase64Note(document);
				if(error=="") {
					result.CountCreated++;
					continue;
				}
				result.ListSkipped.Add(Lans.g("DatabaseMaintenance","DocNum")+" "+document.DocNum+", "+Lans.g("DatabaseMaintenance","PatNum")+" "
					+document.PatNum+": "+error);
			}
			return result;
		}

		///<summary>Converts one placeholder document. Returns "" when done, otherwise why it was skipped (nothing is changed then).</summary>
		private static string CreateDocumentFromBase64Note(Document document) {
			if(!TryParseBase64DocumentNote(document.Note,out string extension,out byte[] arrayBytes,out string error)) {
				return error;
			}
			if(document.MountItemNum!=0) {
				return Lans.g("DatabaseMaintenance","The document is part of a mount. Open it in the Images module instead.");
			}
			Patient patient=Patients.GetPat(document.PatNum);
			if(patient==null) {
				return Lans.g("DatabaseMaintenance","The patient was not found.");
			}
			if(Defs.GetDef(DefCat.ImageCats,document.DocCategory)==null) {
				return Lans.g("DatabaseMaintenance","The image category was not found.");
			}
			string pathTemp=ODFileUtils.CombinePaths(PrefC.GetTempFolderPath(),"base64doc_"+document.DocNum+"_"+Guid.NewGuid().ToString("N")+extension);
			Document documentNew;
			try {
				File.WriteAllBytes(pathTemp,arrayBytes);
				documentNew=ImageStore.Import(pathTemp,document.DocCategory,patient,doAvoidOverwrite:true);
			}
			catch(Exception ex) {
				return Lans.g("DatabaseMaintenance","The file could not be created:")+" "+ex.Message;
			}
			finally {
				ODException.SwallowAnyException(() => File.Delete(pathTemp));
			}
			documentNew.Description=document.Description;
			documentNew.DocCategory=document.DocCategory;
			documentNew.ImgType=document.ImgType;
			documentNew.DateCreated=document.DateCreated;
			documentNew.ToothNumbers=document.ToothNumbers;
			documentNew.ProvNum=document.ProvNum;
			documentNew.PrintHeading=document.PrintHeading;
			Documents.Update(documentNew);
			Documents.Delete(document);
			return "";
		}
	}

	///<summary>What the Create Documents From Base64 Notes tool did.</summary>
	public class Base64DocumentsResult {
		///<summary>Documents with a "_rawBase64_:" note when the run started.</summary>
		public int CountFound;
		public int CountCreated;
		///<summary>One line per skipped document: DocNum, PatNum and the reason.</summary>
		public List<string> ListSkipped=new List<string>();
		///<summary>Documents with a "_download_:" note, which were left alone.</summary>
		public long CountDownloadNotes;

		///<summary>The summary shown to the user and saved to the DBM log.</summary>
		public string GetLog() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("DatabaseMaintenance","Documents with base64 notes found:")+" "+CountFound);
			stringBuilder.AppendLine(Lans.g("DatabaseMaintenance","Documents created:")+" "+CountCreated);
			stringBuilder.AppendLine(Lans.g("DatabaseMaintenance","Documents skipped:")+" "+ListSkipped.Count);
			for(int i=0;i<ListSkipped.Count;i++) {
				stringBuilder.AppendLine("   "+ListSkipped[i]);
			}
			if(CountDownloadNotes>0) {
				stringBuilder.AppendLine(Lans.g("DatabaseMaintenance","Documents with a download link in the note, left unchanged:")+" "+CountDownloadNotes);
			}
			return stringBuilder.ToString().Trim();
		}
	}
}
