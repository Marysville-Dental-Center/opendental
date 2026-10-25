using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Chart letters (Open Dental 24.4): long clinical notes written as word processing documents from a letter merge template. Each one is a
	///document row with ChartLetterStatus above 0 and DocCategory 0, so it shows in the Chart module's progress notes and never in the Imaging tree.
	///A letter is never changed in place: every saved edit writes a new document row with a copy of the file and its hash, and the row it
	///replaces is kept as history. Deleting only flags the row.</summary>
	public class ChartLetters {
		///<summary>Prefix of every security log entry about chart letters. The entries reuse the image permissions (ImageCreate, ImageEdit,
		///ImageDelete), as the spec allows, because no permission type is published for chart letters.</summary>
		public const string LOG_PREFIX="Chart Letter";
		///<summary>Creating a chart letter needs the permission to create completed procedures, as for a procedure note (spec decision).</summary>
		public const EnumPermType PERM_CREATE=EnumPermType.ProcComplCreate;
		///<summary>Editing, signing, changing the properties of, or deleting a chart letter needs the completed procedure note permission, with that
		///permission's date limit applied to the letter's date (spec decision).</summary>
		public const EnumPermType PERM_EDIT=EnumPermType.ProcCompleteNote;

		#region Status and hash
		///<summary>True for a document row that is a chart letter, whatever its version or status.</summary>
		public static bool IsChartLetter(Document document) {
			return document!=null && document.ChartLetterStatus!=0;
		}

		///<summary>The status of a document. Open Dental does not publish its non-zero values, so values we do not know are treated as Current:
		///the letter stays visible rather than being hidden.</summary>
		public static EnumChartLetterStatus GetStatus(Document document) {
			if(document==null || document.ChartLetterStatus==0) {
				return EnumChartLetterStatus.None;
			}
			if(document.ChartLetterStatus==(byte)EnumChartLetterStatus.Superseded) {
				return EnumChartLetterStatus.Superseded;
			}
			if(document.ChartLetterStatus==(byte)EnumChartLetterStatus.Deleted) {
				return EnumChartLetterStatus.Deleted;
			}
			return EnumChartLetterStatus.Current;
		}

		///<summary>The hash stored in ChartLetterHash: the 16-byte MD5 hash of the file's bytes as 32 upper-case hex characters, with no separators.
		///</summary>
		public static string ComputeHash(byte[] fileBytes) {
			using(MD5 md5=MD5.Create()) {
				byte[] arrayHash=md5.ComputeHash(fileBytes??new byte[0]);
				StringBuilder stringBuilder=new StringBuilder(32);
				for(int i=0;i<arrayHash.Length;i++) {
					stringBuilder.Append(arrayHash[i].ToString("X2"));
				}
				return stringBuilder.ToString();
			}
		}

		///<summary>Hashes are compared without regard to case or surrounding spaces, because Open Dental does not say which case it writes.</summary>
		public static bool HashesMatch(string hash1,string hash2) {
			return string.Equals((hash1??"").Trim(),(hash2??"").Trim(),StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Whether the file still produces the hash stored with this version. No file (or an empty one) is FileMissing.</summary>
		public static EnumChartLetterHashMatch GetHashMatch(Document document,byte[] fileBytes) {
			if(fileBytes==null || fileBytes.Length==0) {
				return EnumChartLetterHashMatch.FileMissing;
			}
			if(HashesMatch(document.ChartLetterHash,ComputeHash(fileBytes))) {
				return EnumChartLetterHashMatch.Match;
			}
			return EnumChartLetterHashMatch.NoMatch;
		}

		///<summary>The key data a chart letter signature is bound to: the hash of the file's bytes. A new version has new bytes, so its signature
		///must be made again.</summary>
		public static string GetSignatureKeyData(byte[] fileBytes) {
			return ComputeHash(fileBytes);
		}

		///<summary>Every key data a stored signature may have been made with, ours first. Open Dental documents that its chart letter signatures
		///may use a slightly different algorithm than image signatures; the image algorithm (file bytes followed by the note) is tried as well so a
		///signature made that way still shows.</summary>
		public static List<string> GetSignatureKeyDataCandidates(byte[] fileBytes,string note) {
			List<string> listKeys=new List<string>();
			listKeys.Add(GetSignatureKeyData(fileBytes));
			byte[] arrayText=Encoding.UTF8.GetBytes(note??"");
			byte[] arrayBuffer=new byte[(fileBytes??new byte[0]).Length+arrayText.Length];
			Array.Copy(fileBytes??new byte[0],0,arrayBuffer,0,(fileBytes??new byte[0]).Length);
			Array.Copy(arrayText,0,arrayBuffer,arrayBuffer.Length-arrayText.Length,arrayText.Length);
			using(MD5 md5=MD5.Create()) {
				listKeys.Add(Encoding.ASCII.GetString(md5.ComputeHash(arrayBuffer)));
			}
			return listKeys;
		}

		///<summary>The text for the status, for grids and the properties window.</summary>
		public static string GetStatusText(Document document) {
			switch(GetStatus(document)) {
				case EnumChartLetterStatus.Current:
					return Lans.g("ChartLetters","Current");
				case EnumChartLetterStatus.Superseded:
					return Lans.g("ChartLetters","Old version");
				case EnumChartLetterStatus.Deleted:
					return Lans.g("ChartLetters","Deleted");
			}
			return "";
		}
		#endregion Status and hash

		#region Get
		///<summary>The patient's chart letters by date. Without history, only current versions (old versions and deleted letters are left out).
		///</summary>
		public static List<Document> GetForPatient(long patNum,bool includeHistory) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Document>>(MethodBase.GetCurrentMethod(),patNum,includeHistory);
			}
			string command="SELECT * FROM document WHERE PatNum="+POut.Long(patNum)+" AND ChartLetterStatus>0 ";
			if(!includeHistory) {
				command+="AND ChartLetterStatus NOT IN ("+POut.Int((int)EnumChartLetterStatus.Superseded)+","
					+POut.Int((int)EnumChartLetterStatus.Deleted)+") ";
			}
			command+="ORDER BY DateCreated,DocNum";
			return Crud.DocumentCrud.SelectMany(command);
		}

		///<summary>The raw rows behind the chart letter lines of the progress notes, without the file data. In audit mode, old versions and deleted
		///letters are included.</summary>
		public static DataTable GetTableForProgNotes(long patNum,bool isAuditMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),patNum,isAuditMode);
			}
			string command="SELECT DocNum,PatNum,Description,DateCreated,ProvNum,UserNum,ChartLetterStatus,"
				+"(CASE WHEN Signature!='' THEN 1 ELSE 0 END) IsSigned "
				+"FROM document WHERE PatNum="+POut.Long(patNum)+" AND ChartLetterStatus>0 ";
			if(!isAuditMode) {
				command+="AND ChartLetterStatus NOT IN ("+POut.Int((int)EnumChartLetterStatus.Superseded)+","
					+POut.Int((int)EnumChartLetterStatus.Deleted)+") ";
			}
			command+="ORDER BY DateCreated,DocNum";
			return Db.GetTable(command);
		}

		///<summary>The progress notes description of a chart letter row.</summary>
		public static string GetProgNoteDescription(string description,byte chartLetterStatus) {
			string retVal=Lans.g("ChartLetters","Chart Letter");
			if(!string.IsNullOrWhiteSpace(description)) {
				retVal+=" - "+description;
			}
			if(chartLetterStatus==(byte)EnumChartLetterStatus.Superseded) {
				retVal+=" "+Lans.g("ChartLetters","(old version)");
			}
			else if(chartLetterStatus==(byte)EnumChartLetterStatus.Deleted) {
				retVal+=" "+Lans.g("ChartLetters","(deleted)");
			}
			return retVal;
		}

		///<summary>The file's bytes, from the patient's folder, cloud storage or the database. Empty if the file is missing.</summary>
		public static byte[] GetFileBytes(Document document,string patFolder) {
			Meth.NoCheckMiddleTierRole();
			return ImageStore.GetDocumentFileBytes(document,patFolder);
		}
		#endregion Get

		#region Create, version, delete
		///<summary>Inserts a new chart letter with the file's bytes and logs it. fileExtension is like ".docx". The date may not be in the future.
		///patFolder is the patient's image folder (blank when images are stored in the database).</summary>
		public static Document CreateLetter(Patient patient,byte[] fileBytes,string fileExtension,string description,long provNum,long userNum,
			DateTime dateCreated,string patFolder)
		{
			Meth.NoCheckMiddleTierRole();
			if(patient==null) {
				throw new ArgumentNullException(nameof(patient));
			}
			if(fileBytes==null || fileBytes.Length==0) {
				throw new ApplicationException(Lans.g("ChartLetters","The chart letter has no content."));
			}
			if(dateCreated>DateTime.Now) {
				throw new ApplicationException(Lans.g("ChartLetters","A chart letter cannot be dated in the future."));
			}
			Document document=new Document();
			document.PatNum=patient.PatNum;
			document.DateCreated=dateCreated;
			document.Description=description??"";
			document.ProvNum=provNum;
			document.UserNum=userNum;
			document.ImgType=ImageType.File;
			document.DocCategory=0;
			document.ChartLetterStatus=(byte)EnumChartLetterStatus.Current;
			document.ChartLetterHash=ComputeHash(fileBytes);
			document.FileName=NormalizeExtension(fileExtension);
			InsertWithFile(document,patient,fileBytes,patFolder);
			MakeLog(EnumPermType.ImageCreate,document,Lans.g("ChartLetters","Chart letter created"),userNum);
			return document;
		}

		///<summary>Writes a new version of the letter when the bytes differ from the current version's stored hash. The new row copies the letter's
		///date, description and provider, takes the new file, hash and user, and has no signature. The row passed in becomes an old version.
		///Returns null if the bytes are unchanged. Throws if the letter was deleted or replaced by someone else in the meantime.</summary>
		public static Document SaveNewVersion(Document documentCurrent,Patient patient,byte[] fileBytes,long userNum,string patFolder) {
			Meth.NoCheckMiddleTierRole();
			if(fileBytes==null || fileBytes.Length==0) {
				throw new ApplicationException(Lans.g("ChartLetters","The chart letter has no content."));
			}
			Document documentDb=Documents.GetByNum(documentCurrent.DocNum,doReturnNullIfNotFound:true);
			if(documentDb==null || GetStatus(documentDb)!=EnumChartLetterStatus.Current) {
				throw new ApplicationException(Lans.g("ChartLetters","This chart letter was changed or deleted by another user. Your edits were not saved "
					+"as a new version."));
			}
			string hash=ComputeHash(fileBytes);
			if(HashesMatch(documentDb.ChartLetterHash,hash)) {
				return null;
			}
			Document documentNew=documentDb.Copy();
			documentNew.DocNum=0;
			documentNew.FileName=NormalizeExtension(Path.GetExtension(documentDb.FileName));
			documentNew.Signature="";
			documentNew.SigIsTopaz=false;
			documentNew.RawBase64="";
			documentNew.Thumbnail="";
			documentNew.ExternalGUID="";
			documentNew.ExternalSource=ExternalSourceType.None;
			documentNew.MountItemNum=0;
			documentNew.DocCategory=0;
			documentNew.UserNum=userNum;
			documentNew.ChartLetterHash=hash;
			documentNew.ChartLetterStatus=(byte)EnumChartLetterStatus.Current;
			InsertWithFile(documentNew,patient,fileBytes,patFolder);
			Document documentOld=documentDb.Copy();
			documentDb.ChartLetterStatus=(byte)EnumChartLetterStatus.Superseded;
			Documents.Update(documentDb,documentOld);
			MakeLog(EnumPermType.ImageEdit,documentNew,Lans.g("ChartLetters","Chart letter edited, new version")+" "+documentNew.DocNum+" "
				+Lans.g("ChartLetters","replaces")+" "+documentDb.DocNum,userNum);
			return documentNew;
		}

		///<summary>Flags the letter as deleted. The row and its file are kept and show with Audit checked.</summary>
		public static void Delete(Document document,long userNum) {
			Meth.NoCheckMiddleTierRole();
			Document documentDb=Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true);
			if(documentDb==null || !IsChartLetter(documentDb)) {
				throw new ApplicationException(Lans.g("ChartLetters","The chart letter no longer exists."));
			}
			if(GetStatus(documentDb)==EnumChartLetterStatus.Deleted) {
				return;
			}
			Document documentOld=documentDb.Copy();
			documentDb.ChartLetterStatus=(byte)EnumChartLetterStatus.Deleted;
			Documents.Update(documentDb,documentOld);
			document.ChartLetterStatus=documentDb.ChartLetterStatus;
			MakeLog(EnumPermType.ImageDelete,documentDb,Lans.g("ChartLetters","Chart letter deleted"),userNum);
		}

		///<summary>Saves changes to the date, description, provider, note or signature of the current version, in place. These never change the
		///user who last changed the content, the file, the hash or the status. Logs the change, and the signing separately. Returns false if nothing
		///changed.</summary>
		public static bool UpdateProperties(Document document,Document documentOld,long userNum) {
			Meth.NoCheckMiddleTierRole();
			if(document.DateCreated>DateTime.Now) {
				throw new ApplicationException(Lans.g("ChartLetters","A chart letter cannot be dated in the future."));
			}
			document.UserNum=documentOld.UserNum;
			document.ChartLetterHash=documentOld.ChartLetterHash;
			document.ChartLetterStatus=documentOld.ChartLetterStatus;
			document.FileName=documentOld.FileName;
			document.DocCategory=documentOld.DocCategory;
			bool isSignatureChanged=document.Signature!=documentOld.Signature || document.SigIsTopaz!=documentOld.SigIsTopaz;
			bool isOtherChanged=document.DateCreated!=documentOld.DateCreated || document.Description!=documentOld.Description
				|| document.ProvNum!=documentOld.ProvNum || document.Note!=documentOld.Note;
			if(!isSignatureChanged && !isOtherChanged) {
				return false;
			}
			Documents.Update(document,documentOld);
			if(isOtherChanged) {
				MakeLog(EnumPermType.ImageEdit,document,Lans.g("ChartLetters","Chart letter properties changed"),userNum);
			}
			if(isSignatureChanged) {
				string text=document.Signature=="" ? Lans.g("ChartLetters","Chart letter signature removed") : Lans.g("ChartLetters","Chart letter signed");
				MakeLog(EnumPermType.ImageEdit,document,text,userNum);
			}
			return true;
		}

		///<summary>Logs that the file no longer produces its stored hash, so it was edited outside the program.</summary>
		public static void LogHashMismatch(Document document,long userNum) {
			Meth.NoCheckMiddleTierRole();
			MakeLog(EnumPermType.ImageEdit,document,Lans.g("ChartLetters","Chart letter edited outside the program (hash mismatch detected)"),userNum);
		}

		///<summary>Inserts the row, writes the file next to the patient's other documents (or into the row when images are stored in the database)
		///and saves the final file name.</summary>
		private static void InsertWithFile(Document document,Patient patient,byte[] fileBytes,string patFolder) {
			Documents.Insert(document,patient);//Assigns a unique file name from the extension.
			CopyFileNameFromDb(document);
			ImageStore.SaveChartLetterFile(document,fileBytes,patFolder);
			Documents.Update(document);
		}

		///<summary>Documents.Insert(document,patient) gives the row a unique file name (from the patient's name and the DocNum) on the server. Under
		///Middle Tier only the DocNum comes back to the workstation, so the object still holds just the extension. This reads the name back from
		///the row, as ImageStore.Import does after inserting, so the file is never written under the bare extension (which would overwrite the
		///file of another letter or version in cloud storage) and the row's name is not overwritten by the following update.</summary>
		public static void CopyFileNameFromDb(Document document) {
			Meth.NoCheckMiddleTierRole();
			Document documentDb=Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true);
			if(documentDb==null) {
				throw new ApplicationException(Lans.g("ChartLetters","The chart letter's row could not be read back after it was inserted."));
			}
			document.FileName=documentDb.FileName;
		}

		private static string NormalizeExtension(string fileExtension) {
			if(string.IsNullOrWhiteSpace(fileExtension)) {
				return ".docx";
			}
			fileExtension=fileExtension.Trim().ToLowerInvariant();
			if(!fileExtension.StartsWith(".")) {
				fileExtension="."+fileExtension;
			}
			return fileExtension;
		}

		private static void MakeLog(EnumPermType permType,Document document,string action,long userNum) {
			string text=LOG_PREFIX+": "+action;
			if(!string.IsNullOrWhiteSpace(document.Description)) {
				string description=document.Description;
				if(description.Length>50) {
					description=description.Substring(0,50);
				}
				text+=", "+description;
			}
			text+=" ("+document.FileName+")";
			SecurityLogs.MakeLogEntry(permType,document.PatNum,text,document.DocNum,LogSources.None,DateTime.MinValue,userNum);
		}
		#endregion Create, version, delete
	}

	///<summary>The values of document.ChartLetterStatus. Open Dental publishes only that 0 means an ordinary document; the other numbers are our
	///decision (features-24.4.md) and should be compared with real chart letters before an office database is adopted.</summary>
	public enum EnumChartLetterStatus : byte {
		///<summary>0 - Not a chart letter.</summary>
		None=0,
		///<summary>1 - The current version of a chart letter.</summary>
		Current=1,
		///<summary>2 - An older version, replaced by a newer row. Shown only in audit mode.</summary>
		Superseded=2,
		///<summary>3 - Deleted. The row and file are kept and show only in audit mode.</summary>
		Deleted=3,
	}

	///<summary>Whether a chart letter's file still produces its stored hash.</summary>
	public enum EnumChartLetterHashMatch {
		Match,
		///<summary>The file was changed outside the program.</summary>
		NoMatch,
		///<summary>The file could not be found or is empty.</summary>
		FileMissing,
	}

	///<summary>Event data for a version written by a ChartLetterEditSession.</summary>
	public class ChartLetterVersionEventArgs:EventArgs {
		public Document DocumentOld;
		public Document DocumentNew;
		public Exception Error;
	}

	///<summary>Tracks one chart letter opened for editing. The letter's file is copied to a private working folder and opened there. Each time the
	///editor saves the working copy, a new version is written (ChartLetters.SaveNewVersion). Saves are noticed by a FileSystemWatcher while the
	///program runs; edits made while it is closed cannot be tracked and show later as a hash mismatch only if made to the stored file.</summary>
	public class ChartLetterEditSession:IDisposable {
		private readonly object _lock=new object();
		private readonly Patient _patient;
		private readonly string _patFolder;
		private readonly long _userNum;
		private string _hashLast;
		private FileSystemWatcher _fileSystemWatcher;
		private Timer _timer;
		private int _debounceMs;
		private int _countRetries;
		private bool _isDisposed;
		///<summary>The newest version of the letter. Changes each time a version is written.</summary>
		public Document DocumentCur { get; private set; }
		///<summary>Full path of the working copy the editor works on.</summary>
		public string WorkingFilePath { get; private set; }
		///<summary>Raised after a new version is written, or with Error set when writing one failed. Raised on a background thread when watching.
		///</summary>
		public event EventHandler<ChartLetterVersionEventArgs> VersionSaved;

		private ChartLetterEditSession(Document document,Patient patient,string patFolder,long userNum) {
			DocumentCur=document;
			_patient=patient;
			_patFolder=patFolder;
			_userNum=userNum;
		}

		///<summary>Copies the current version's file into a new subfolder of workingRoot, named after the description, and returns the session.
		///Does not start watching (see StartWatching). Throws if the letter is not the current version or its file is missing.</summary>
		public static ChartLetterEditSession Start(Document document,Patient patient,string patFolder,long userNum,string workingRoot) {
			if(ChartLetters.GetStatus(document)!=EnumChartLetterStatus.Current) {
				throw new ApplicationException(Lans.g("ChartLetters","Only the current version of a chart letter can be edited."));
			}
			byte[] fileBytes=ChartLetters.GetFileBytes(document,patFolder);
			if(fileBytes.Length==0) {
				throw new ApplicationException(Lans.g("ChartLetters","The chart letter's file could not be found."));
			}
			ChartLetterEditSession chartLetterEditSession=new ChartLetterEditSession(document,patient,patFolder,userNum);
			string folder=Path.Combine(workingRoot,document.DocNum.ToString()+"_"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));
			Directory.CreateDirectory(folder);
			string name=ODFileUtils.CleanFileName(document.Description??"").Trim();
			if(name=="") {
				name=Lans.g("ChartLetters","Chart Letter");
			}
			chartLetterEditSession.WorkingFilePath=Path.Combine(folder,name+Path.GetExtension(document.FileName));
			File.WriteAllBytes(chartLetterEditSession.WorkingFilePath,fileBytes);
			chartLetterEditSession._hashLast=ChartLetters.ComputeHash(fileBytes);
			return chartLetterEditSession;
		}

		///<summary>Reads the working copy and writes a new version if it changed since the last version. Returns the new version, or null if
		///nothing changed. Throws IOException while the editor still holds the file locked.</summary>
		public Document CheckForChanges() {
			Document documentOld;
			Document documentNew;
			lock(_lock) {
				if(_isDisposed || !File.Exists(WorkingFilePath)) {
					return null;
				}
				byte[] fileBytes=ReadShared(WorkingFilePath);
				if(fileBytes.Length==0) {
					return null;//An editor that truncates before writing; the next change event reads the full file.
				}
				string hash=ChartLetters.ComputeHash(fileBytes);
				if(ChartLetters.HashesMatch(hash,_hashLast)) {
					return null;
				}
				documentOld=DocumentCur;
				documentNew=ChartLetters.SaveNewVersion(DocumentCur,_patient,fileBytes,_userNum,_patFolder);
				_hashLast=hash;
				if(documentNew==null) {
					return null;
				}
				DocumentCur=documentNew;
			}
			VersionSaved?.Invoke(this,new ChartLetterVersionEventArgs { DocumentOld=documentOld,DocumentNew=documentNew });
			return documentNew;
		}

		///<summary>Watches the working copy and calls CheckForChanges once the editor has been quiet for debounceMs after a save.</summary>
		public void StartWatching(int debounceMs=1500) {
			lock(_lock) {
				if(_fileSystemWatcher!=null || _isDisposed) {
					return;
				}
				_debounceMs=debounceMs;
				_timer=new Timer(Timer_Tick,null,Timeout.Infinite,Timeout.Infinite);
				_fileSystemWatcher=new FileSystemWatcher(Path.GetDirectoryName(WorkingFilePath));
				//Editors often save to a temporary file and rename it over the original, so watch the whole folder.
				_fileSystemWatcher.NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size;
				_fileSystemWatcher.Changed+=FileSystemWatcher_Event;
				_fileSystemWatcher.Created+=FileSystemWatcher_Event;
				_fileSystemWatcher.Renamed+=FileSystemWatcher_Event;
				_fileSystemWatcher.EnableRaisingEvents=true;
			}
		}

		private void FileSystemWatcher_Event(object sender,FileSystemEventArgs e) {
			if(!string.Equals(e.FullPath,WorkingFilePath,StringComparison.OrdinalIgnoreCase)) {
				return;
			}
			lock(_lock) {
				_countRetries=0;
				_timer?.Change(_debounceMs,Timeout.Infinite);
			}
		}

		private void Timer_Tick(object state) {
			try {
				CheckForChanges();
			}
			catch(IOException) {
				lock(_lock) {
					_countRetries++;
					if(_countRetries<=20) {
						_timer?.Change(_debounceMs,Timeout.Infinite);//The editor still holds the file. Try again shortly.
					}
				}
			}
			catch(Exception ex) {
				VersionSaved?.Invoke(this,new ChartLetterVersionEventArgs { DocumentOld=DocumentCur,Error=ex });
			}
		}

		private static byte[] ReadShared(string path) {
			using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
			using(MemoryStream memoryStream=new MemoryStream()) {
				fileStream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}

		public void Dispose() {
			lock(_lock) {
				_isDisposed=true;
				if(_fileSystemWatcher!=null) {
					_fileSystemWatcher.EnableRaisingEvents=false;
					_fileSystemWatcher.Dispose();
					_fileSystemWatcher=null;
				}
				_timer?.Dispose();
				_timer=null;
			}
		}
	}
}
