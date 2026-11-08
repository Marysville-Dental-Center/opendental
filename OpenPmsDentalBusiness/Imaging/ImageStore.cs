using CodeBase;
using OpenPmsDentalBusiness.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenPmsDentalBusiness {
	/// <summary></summary>
	public class ImageStore {

		///<summary>Only makes a call to the database on startup.  After that, just uses cached data.  Does not validate that the path exists except if the main one is used.  ONLY used from Client layer or S class methods that call Meth.NoCheckMiddleTierRole() and which also make sure PrefC.AtoZfolderUsed.  Returns Cloud AtoZ path if CloudStorage.IsCloudStorage</summary>
		public static string GetPreferredAtoZpath() {
			//There were so many references to the current function that we decided to temporarily call the FileAtoZ version here.
			return FileAtoZ.GetPreferredAtoZpath();
		}

		///<summary>Throw exceptions. Returns patient's AtoZ folder if local AtoZ used, blank if database is used, 
		///or Cloud AtoZ path if CloudStorage.IsCloudStorage. Will validate that folder exists. Will create folder if needed. 
		///It will set the pat.ImageFolder if pat.ImageFolder is blank.</summary>
		public static string GetPatientFolder(Patient pat,string AtoZpath) {
			string retVal="";
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				return retVal;
			}
			if(CloudStorage.IsCloudStorage) {
				AtoZpath=CloudStorage.AtoZPath;
			}
			Patient PatOld=pat.Copy();
			if(string.IsNullOrEmpty(pat.ImageFolder)) {//creates new folder for patient if none present
				pat.ImageFolder=GetImageFolderName(pat);
			}
			if(CloudStorage.IsCloudStorage) {
				retVal=ODFileUtils.CombinePaths(AtoZpath,
					pat.ImageFolder.Substring(0,1).ToUpper(),
					pat.ImageFolder,'/');//use '/' char instead of Path.DirectorySeparatorChar
			}
			else if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				object[] objectArray={pat,AtoZpath};
				Plugins.HookAddCode(null,"ImageStore.GetPatientFolder_LocalAtoZ",objectArray);
				AtoZpath=(string)objectArray[1];
				retVal=ODFileUtils.CombinePaths(AtoZpath,
					pat.ImageFolder.Substring(0,1).ToUpper(),
					pat.ImageFolder);//use Path.DirectorySeparatorChar
				try {
					if(string.IsNullOrEmpty(AtoZpath)) {
						//If AtoZpath parameter was null or empty string and DataStorageType is LocalAtoZ, don't create a directory since retVal would then be
						//considered a relative path. Example: If AtoZpath is null, retVal will be like "P\PatientAustin1" after ODFileUtils.CombinePaths.
						//CreateDirectory treats this as a relative path and the full path would be "C:\Program Files (x86)\Open Dental\P\PatientAustin1".
						throw new ApplicationException("AtoZpath was null or empty");
					}
					using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Exists,retVal);
					if(!Directory.Exists(retVal)) {
						Directory.CreateDirectory(retVal);
					}
				}
				catch(Exception ex) {
					throw new ApplicationException(Lans.g("ContrDocs","Error.  Could not create folder for patient:")+" "+retVal,ex);
				}
			}
			if(string.IsNullOrEmpty(PatOld.ImageFolder)) {
				Patients.Update(pat,PatOld);
			}
			return retVal;
		}

		///<summary>Returns the name of the ImageFolder. Removes any non letter to the patient's name.</summary>
		public static string GetImageFolderName(Patient pat) {
			string name=pat.LName+pat.FName;
			string folder="";
			for(int i=0;i<name.Length;i++) {
				if(Char.IsLetter(name,i)) {
					folder+=name.Substring(i,1);
				}
			}
			folder+=pat.PatNum.ToString();//ensures unique name
			return folder;
		}

		///<summary>Will create folder if needed.  Will validate that folder exists.</summary>
		public static string GetEobFolder() {
			string retVal="";
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				return retVal;
			}
			string AtoZPath=GetPreferredAtoZpath();
			retVal=ODFileUtils.CombinePaths(AtoZPath,"EOBs");
			if(CloudStorage.IsCloudStorage) {
				retVal=retVal.Replace("\\","/");
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ && !Directory.Exists(retVal)) {
				if(string.IsNullOrEmpty(AtoZPath)) {
					throw new ApplicationException(Lans.g("ContrDocs","Could not find the path for the AtoZ folder."));
				}
				Directory.CreateDirectory(retVal);
			}
			return retVal;
		}

		///<summary>Will create folder if needed.  Will validate that folder exists.</summary>
		public static string GetAmdFolder() {
			string retVal="";
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				return retVal;
			}
			string AtoZPath=GetPreferredAtoZpath();
			retVal=ODFileUtils.CombinePaths(AtoZPath,"Amendments");
			if(CloudStorage.IsCloudStorage) {
				retVal=retVal.Replace("\\","/");
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ && !Directory.Exists(retVal)) {
				if(string.IsNullOrEmpty(AtoZPath)) {
					throw new ApplicationException(Lans.g("ContrDocs","Could not find the path for the AtoZ folder."));
				}
				Directory.CreateDirectory(retVal);
			}
			return retVal;
		}

		///<summary>Will create folder if needed. Will validate that folder exists.</summary>
		public static string GetMobileBrandingImageFolder() {
			string retVal="";
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				return retVal;
			}
			string AtoZPath=GetPreferredAtoZpath();
			retVal=ODFileUtils.CombinePaths(AtoZPath,"MobileBrandingImages");
			if(CloudStorage.IsCloudStorage) {
				retVal=retVal.Replace("\\","/");
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ && !Directory.Exists(retVal)) {
				if(string.IsNullOrEmpty(AtoZPath)) {
					throw new ApplicationException(Lans.g("ContrDocs","Could not find the path for the AtoZ folder."));
				}
				Directory.CreateDirectory(retVal);
			}
			return retVal;
		}

		///<summary>Gets the folder name where provider images are stored. Will create folder if needed.</summary>
		public static string GetProviderImagesFolder() {
			string retVal="";
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				return retVal;
			}
			string AtoZPath=GetPreferredAtoZpath();
			retVal=FileAtoZ.CombinePaths(AtoZPath,"ProviderImages");
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ && !Directory.Exists(retVal)) {
				if(string.IsNullOrEmpty(AtoZPath)) {
					throw new ApplicationException(Lans.g("ContrDocs","Could not find the path for the AtoZ folder."));
				}
				Directory.CreateDirectory(retVal);
			}
			return retVal;
		}
		
		///<summary>Surround with try/catch.  Typically returns something similar to \\SERVER\OpenDentImages\EmailImages.
		///This is the location of the email html template images.  The images are stored in this central location in order to
		///make them reusable on multiple email messages.  These images are not patient specific, therefore are in a different
		///location than the email attachments.  For location of patient attachments, see EmailAttaches.GetAttachPath().</summary>
		public static string GetEmailImagePath() {
			Meth.NoCheckMiddleTierRole();
			string emailPath;
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				throw new ApplicationException(Lans.g("WikiPages","Must be using AtoZ folders."));
			}
			emailPath=FileAtoZ.CombinePaths(GetPreferredAtoZpath(),"EmailImages");
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ && !Directory.Exists(emailPath)) {
				Directory.CreateDirectory(emailPath);
			}
			return emailPath;
		} 

		///<summary>When the Image module is opened, this loads newly added files.</summary>
		public static void AddMissingFilesToDatabase(Patient pat) {
			//There is no such thing as adding files from any directory when not using AtoZ
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				return;
			}
			string patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			List<string> fileList=new List<string>();
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				DirectoryInfo di = new DirectoryInfo(patFolder);
				List<FileInfo> fiList = di.GetFiles().Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden)).ToList();
				fileList.AddRange(fiList.Select(x => x.FullName));
			}
			else {//Cloud
				List<string> listFiles=CloudStorage.ListFolderContents(patFolder);
				List<Document> listDocs=Documents.GetAllWithPat(pat.PatNum).ToList();
				listFiles=listFiles.Select(x => Path.GetFileName(x)).ToList();
				foreach(string fileName in listFiles) {
					if(!listDocs.Exists(x => x.FileName==fileName)) {
						fileList.Add(fileName);
					}
				}
			}
			int countAdded=Documents.InsertMissing(pat,fileList);//Automatically detects and inserts files that are in the patient's folder that aren't present in the database. Logs entries.
			//should notify user
			//if(countAdded > 0) {
			//	Debug.WriteLine(countAdded.ToString() + " documents found and added to the first category.");
			//}
			//it will refresh in FillDocList
		}

		///<summary>includeFileInHash only applies to DataStorageTypes InDatabase and LocalAtoZ.</summary>
		public static string GetHashString(Document doc,string patFolder,bool includeFileInHash=true) {
			//There was a previous bug that existed for about 5 years.
			//The bug was that the hash string result was not based on file bytes.
			//File bytes was "0" with a the note still properly appended.
			//If the file was subequently changed, the signature was not getting invalidated.
			//Since this really never happens, nobody cared.
			//But it was wrong. The signature must prove that the file didn't change.
			//We can't go back and fix all those signatures, so we do here what we always do when there are bugs with sigs.
			//We try it both ways.
			//Run this method first with includeFileInHash set to true (the new correct way).
			//If the signature decryption fails, then run this again with includeFileInHash false.
			//This bug did not apply to CloudStorage, which always used the file and will continue to use the file.
			byte[] textbytes;
			byte[] filebytes=new byte[1];
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase){
				patFolder=ODFileUtils.CombinePaths(Path.GetTempPath(),ProgramNames.TempFolderName);
				byte[] rawData=Convert.FromBase64String(doc.RawBase64);
				using(FileStream file=new FileStream(ODFileUtils.CombinePaths(patFolder,doc.FileName),FileMode.Create,FileAccess.Write)) {
					file.Write(rawData,0,rawData.Length);
					file.Close();
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				filebytes=GetBytes(doc,patFolder);
			}
			else if(includeFileInHash) {
				filebytes=GetBytes(doc,patFolder);
			}
			if(doc.Note == null) {
				textbytes = Encoding.UTF8.GetBytes("");
			}
			else {
				textbytes = Encoding.UTF8.GetBytes(doc.Note);
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				try {
					File.Delete(ODFileUtils.CombinePaths(patFolder,doc.FileName));//Delete temp file
				}
				catch { }//Should never happen since the file was just created and the permissions were there moments ago when the file was created.
			}
			int fileLength = filebytes.Length;
			byte[] buffer = new byte[textbytes.Length + filebytes.Length];
			Array.Copy(filebytes,0,buffer,0,fileLength);
			Array.Copy(textbytes,0,buffer,fileLength,textbytes.Length);
			return Encoding.ASCII.GetString(ODCrypt.MD5.Hash(buffer));
		}

		///<summary>Can be null. Analogous to OpenImage. OpenBitmapDicomResult says why there is no image.</summary>
		public static BitmapDicom OpenBitmapDicom(Document doc,string patFolder,string localPath=""){
			if(!doc.FileName.EndsWith(".dcm")){
				return null;
			}
			return OpenBitmapDicomResult(doc,patFolder,localPath).BitmapDicom;
		}

		///<summary>The pixels of a .dcm document from its storage (the patient's folder, cloud storage, or the database row), read by DicomHelper
		///(the in-house subset reader, then the full reader), with the reason when there is no image: DicomHelper.GetNotReadMessage turns it
		///into the message the user sees, for example for a compressed format this version does not open.</summary>
		public static DicomImageReadResult OpenBitmapDicomResult(Document doc,string patFolder,string localPath=""){
			if(doc==null || doc.FileName==null || !doc.FileName.EndsWith(".dcm")){
				return new DicomImageReadResult(DicomImageReadStatus.NotDicom,"The document is not a .dcm file.",null,null);
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				return DicomHelper.ReadFileResult(ODFileUtils.CombinePaths(patFolder,doc.FileName));
			}
			if(CloudStorage.IsCloudStorage) {
				if(localPath!="") {
					return DicomHelper.ReadFileResult(localPath);
				}
				byte[] byteArray=CloudStorage.Download(patFolder.Replace("\\","/"),doc.FileName);
				if(byteArray==null) {
					return new DicomImageReadResult(DicomImageReadStatus.FileError,"The file could not be downloaded.",null,null);
				}
				using(MemoryStream ms=new MemoryStream(byteArray)) {
					return DicomHelper.ReadStreamResult(ms);
				}
			}
			byte[] bytesRaw;
			try {
				bytesRaw=Convert.FromBase64String(doc.RawBase64);
			}
			catch(Exception) {//null or not base64
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"The document holds no image data.",null,null);
			}
			using(MemoryStream ms=new MemoryStream(bytesRaw)) {
				return DicomHelper.ReadStreamResult(ms);
			}
		}

		public static Collection<Bitmap> OpenImages(IList<Document> documents,string patFolder,string localPath="") {
			//string patFolder=GetPatientFolder(pat);
			Collection<Bitmap> bitmaps = new Collection<Bitmap>();
			foreach(Document document in documents) {
				if(document == null) {
					bitmaps.Add(null);
				}
				else {
					bitmaps.Add(OpenImage(document,patFolder,localPath));
				}
			}
			return bitmaps;
		}

		///<summary>Individual bitmaps can be null.</summary>
		public static Bitmap[] OpenImages(Document[] documents,string patFolder,string localPath="") {
			//Bitmap[] arrayBitmaps = new Bitmap[documents.Length];
			Collection<Bitmap> collectionBitmaps = OpenImages(new Collection<Document>(documents),patFolder,localPath);
			//collectionBitmaps.CopyTo(arrayBitmaps,0);
			return collectionBitmaps.ToArray();
			//return arrayBitmaps;
		}

		///<summary>Can be null.</summary>
		public static Bitmap OpenImage(Document doc,string patFolder,string localPath="") {
			using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Read,patFolder,doc?.FileName);
			//todo: use a stream so that the returned bitmap does not have a file lock.
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string srcFileName = ODFileUtils.CombinePaths(patFolder,doc.FileName);
				if(HasImageExtension(srcFileName)) {
					//if(File.Exists(srcFileName) && HasImageExtension(srcFileName)) {
					try {
						return new Bitmap(srcFileName);
					}
					catch {
						return null;
					}
				}
				else {
					return null;
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				if(HasImageExtension(doc.FileName)) {
					Bitmap bmp=null;
					if(localPath!="") {
						bmp=new Bitmap(localPath);
					}
					else {
						try {
							byte[] byteArray=CloudStorage.Download(patFolder.Replace("\\","/"),doc.FileName);
							using(MemoryStream ms=new MemoryStream(byteArray)) {
								bmp=new Bitmap(ms);
							}
						}
						catch(Exception e) {
							e.DoNothing();
						}
					}
					return bmp;
				}
				else {
					return null;
				}
			}
			else {
				if(HasImageExtension(doc.FileName)) {
					return PIn.Bitmap(doc.RawBase64);
				}
				else {
					return null;
				}
			}
		}

		public static Bitmap[] OpenImagesEob(EobAttach eob,string localPath="") {
			Bitmap[] values = new Bitmap[1];
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string eobFolder=GetEobFolder();
				string srcFileName = ODFileUtils.CombinePaths(eobFolder,eob.FileName);
				if(HasImageExtension(srcFileName)) {
					if(File.Exists(srcFileName)) {
						try {
							values[0]=new Bitmap(srcFileName);
						}
						catch(Exception ex) {
							throw new ApplicationException(Lans.g("ImageStore","File found but could not be opened:")+" "+srcFileName,ex);
						}
					}
					else {
						throw new ApplicationException(Lans.g("ImageStore","File not found:")+" "+srcFileName);
					}
				}
				else {
					values[0]= null;
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				if(HasImageExtension(eob.FileName)) {
					Bitmap bmp=null;
					try {
						if(localPath!="") {
							bmp=new Bitmap(localPath);
						}
						else {
							byte[] byteArray=CloudStorage.Download(GetEobFolder(),eob.FileName);
							using(MemoryStream ms = new MemoryStream(byteArray)) {
								bmp=new Bitmap(ms);
							}
						}
					}
					catch(Exception ex) {
						throw new ApplicationException(Lans.g("ImageStore","File could not be opened:")+" "+eob.FileName,ex);
					}
					values[0]=bmp;
				}
				else {
					values[0]=null;
				}
			}
			else {
				if(HasImageExtension(eob.FileName)) {
					values[0]= PIn.Bitmap(eob.RawBase64);
				}
				else {
					values[0]= null;
				}
			}
			return values;
		}

		public static Bitmap[] OpenImagesAmd(EhrAmendment amd) {
			Bitmap[] values = new Bitmap[1];
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string amdFolder=GetAmdFolder();
				string srcFileName = ODFileUtils.CombinePaths(amdFolder,amd.FileName);
				if(HasImageExtension(srcFileName)) {
					if(File.Exists(srcFileName)) {
						try {
							values[0]=new Bitmap(srcFileName);
						}
						catch(Exception ex) {
							throw new ApplicationException(Lans.g("ImageStore","File found but could not be opened:")+" "+srcFileName,ex);
						}
					}
					else {
						throw new ApplicationException(Lans.g("ImageStore","File not found:")+" "+srcFileName);
					}
				}
				else {
					values[0]= null;
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				if(HasImageExtension(amd.FileName)) {
					try {
						byte[] byteArray=CloudStorage.Download(GetAmdFolder(),amd.FileName);
						Bitmap bmp=null;
						using(MemoryStream ms = new MemoryStream(byteArray)) {
							bmp=new Bitmap(ms);
						}
						values[0]=bmp;
					}
					catch(Exception ex) {
						throw new ApplicationException(Lans.g("ImageStore","File could not be opened:")+" "+amd.FileName,ex);
					}
				}
				else {
					values[0]=null;
				}
			}
			else {
				if(HasImageExtension(amd.FileName)) {
					values[0]= PIn.Bitmap(amd.RawBase64);
				}
				else {
					values[0]= null;
				}
			}
			return values;
		}

		public static byte[] GetBytes(Document doc,string patFolder) {
			using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Read,patFolder,doc?.FileName);
			if(CloudStorage.IsCloudStorage) {
				byte[] byteArray=CloudStorage.Download(patFolder.Replace("\\","/"),doc.FileName);
				return byteArray;
			}
			string path = ODFileUtils.CombinePaths(patFolder,doc.FileName);
			if(!File.Exists(path)) {
				return new byte[] { };
			}
			byte[] buffer;
			using(FileStream fs = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
				int fileLength = (int)fs.Length;
				buffer = new byte[fileLength];
				fs.Read(buffer,0,fileLength);
			}
			return buffer;
		}

		///<summary>The document's file bytes wherever they are stored: the patient's folder, cloud storage, or the row's RawBase64 when images are
		///stored in the database. Empty if there is no file. Used for chart letters, whose hash and signature are bound to these bytes.</summary>
		public static byte[] GetDocumentFileBytes(Document doc,string patFolder) {
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				if(string.IsNullOrEmpty(doc.RawBase64)) {
					return new byte[0];
				}
				return Convert.FromBase64String(doc.RawBase64);
			}
			byte[] arrayBytes=GetBytes(doc,patFolder);
			return arrayBytes??new byte[0];
		}

		///<summary>Stores a chart letter's file for a document row that was already inserted. In the patient's folder, a name that is already taken
		///by a stray file gets an "x" prefix, as Documents.Insert does, so no file is ever overwritten; save the row afterwards. When images are
		///stored in the database, fills doc.RawBase64. Does not log; ChartLetters logs chart letter changes itself.</summary>
		public static void SaveChartLetterFile(Document doc,byte[] arrayBytes,string patFolder) {
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				while(File.Exists(ODFileUtils.CombinePaths(patFolder,doc.FileName))) {
					doc.FileName="x"+doc.FileName;
				}
				using(FileStream fileStream=new FileStream(ODFileUtils.CombinePaths(patFolder,doc.FileName),FileMode.CreateNew,FileAccess.Write)) {
					fileStream.Write(arrayBytes,0,arrayBytes.Length);
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Upload(patFolder,doc.FileName,arrayBytes);
			}
			else {//InDatabase
				doc.RawBase64=Convert.ToBase64String(arrayBytes);
			}
		}

		///<summary>The DICOM part of Import, for a document of a .dcm file: a radiograph with a printed heading, and the initial window from the
		///image (DicomHelper.CalculateWindowingOnImport). Returns the image read, or null: a file no reader can read (for example a compressed
		///format this version does not open) is still imported, with no windowing, as Documents.InsertMissing attaches it; opening it says why it
		///is not shown.</summary>
		public static BitmapDicom SetDicomImportFields(Document doc,string pathImportFrom) {
			doc.ImgType=ImageType.Radiograph;
			BitmapDicom bitmapDicom=DicomHelper.GetFromFile(pathImportFrom);
			doc.PrintHeading=true;
			if(bitmapDicom!=null) {
				DicomHelper.CalculateWindowingOnImport(bitmapDicom);
				doc.WindowingMin=bitmapDicom.WindowingMin;
				doc.WindowingMax=bitmapDicom.WindowingMax;
			}
			return bitmapDicom;
		}

		/// <summary>Imports any document, not just images.  Also handles dicom by calculating initial windowing.  Also processes Exif rotation info on jpg files.
		/// Set doAvoidOverwrite to give the new file a numbered suffix when a file of its name is already in the patient's folder; otherwise the import fails then.</summary>
		public static Document Import(string pathImportFrom,long docCategory,Patient pat,bool doAvoidOverwrite=false) {
			string patFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage)  {
				patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			}
			Document doc = new Document();
			//Document.Insert will use this extension when naming:
			if(Path.GetExtension(pathImportFrom)=="") {//If the file has no extension
				try {
					Bitmap bmp=new Bitmap(pathImportFrom);//check to see if file is an image and add .jpg extension
					bmp.Dispose();//release file lock
					doc.FileName=".jpg";
				}
				catch(Exception ex) {
					ex.DoNothing(); //catch the error and do nothing. Default the file to .txt to prevent errors.
					doc.FileName=".txt";
				}
			}
			else {
				doc.FileName = Path.GetExtension(pathImportFrom);
			}
			doc.DateCreated=File.GetLastWriteTime(pathImportFrom); // Per Jordan, use lastwritetime instead of DateTime.Now/Today.
			doc.PatNum = pat.PatNum;
			if(HasImageExtension(doc.FileName)) {
				doc.ImgType=ImageType.Photo;
				if(pathImportFrom.ToLower().EndsWith("jpg") || pathImportFrom.ToLower().EndsWith("jpeg")){
					Image image=Image.FromFile(pathImportFrom);
					PropertyItem propertyItem=image.PropertyItems.FirstOrDefault(x=>x.Id==0x0112);//Exif orientation
					if(propertyItem!=null && propertyItem.Value.Length>0){
						//if(propertyItem.Value[0]==1)//no rotation. Do nothing
						if(propertyItem.Value[0]==6){
							doc.DegreesRotated=90;
						}
						else if(propertyItem.Value[0]==3){
							doc.DegreesRotated=180;
						}
						else if(propertyItem.Value[0]==8){
							doc.DegreesRotated=270;
						}
					}
					image.Dispose();//releases file lock
				}
			}
			else if(doc.FileName.ToLower().EndsWith(".dcm")){
				SetDicomImportFields(doc,pathImportFrom);
			}
			else {
				doc.ImgType=ImageType.Document;
			}
			doc.DocCategory = docCategory;
			doc=Documents.InsertAndGet(doc,pat);//this assigns a filename and saves to db
			if(doAvoidOverwrite) {
				SetFileNameNotInFolder(doc,patFolder);
			}
			try {
				SaveDocument(doc,pathImportFrom,patFolder);//Makes log entry
				if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
					Documents.Update(doc);//Because SaveDocument() modified doc.RawBase64
				}
			}
			catch (Exception ex){
				Documents.Delete(doc);
				throw ex;
			}
			return doc;
		}

		///<summary>For Import with doAvoidOverwrite (the Base64 Documents tool, backlog package B-DBM): when a file named doc.FileName is already in
		///patFolder, or another document of the patient has that name, renames doc to name_2.ext, name_3.ext and so on, and saves the new name.
		///Does nothing for in-database storage (blank patFolder).</summary>
		private static void SetFileNameNotInFolder(Document doc,string patFolder) {
			if(string.IsNullOrEmpty(patFolder)) {
				return;
			}
			List<string> listFileNamesUsed=Documents.GetAllWithPat(doc.PatNum).Where(x => x.DocNum!=doc.DocNum).Select(x => x.FileName).ToList();
			string fileName=doc.FileName;
			for(int i=2;FileAtoZ.Exists(FileAtoZ.CombinePaths(patFolder,fileName)) || listFileNamesUsed.Contains(fileName);i++) {
				fileName=Path.GetFileNameWithoutExtension(doc.FileName)+"_"+i+Path.GetExtension(doc.FileName);
			}
			if(fileName!=doc.FileName) {
				doc.FileName=fileName;
				Documents.Update(doc);
			}
		}

		/// <summary>Saves to AtoZ folder, Cloud, or to db.  Saves image as a jpg.  Compression will differ depending on imageType. Throws exception if it can't save. doPrintHeading is set to true for ToothChart and radiographs.</summary>
		public static Document Import(Bitmap image,long docCategory,ImageType imageType,Patient pat,string mimeType="image/jpeg",bool doPrintHeading=false) {
			string patFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			}
			Document doc = new Document();
			doc.ImgType = imageType;
			doc.FileName=GetImageFileExtensionByMimeType(mimeType);
			doc.DateCreated = DateTime.Now;
			doc.PatNum = pat.PatNum;
			doc.DocCategory = docCategory;
			if(doPrintHeading) {
				doc.PrintHeading=true;
			}
			Documents.Insert(doc,pat);//creates filename and saves to db
			doc=Documents.GetByNum(doc.DocNum);
			long qualityL = 0;
			if(imageType.In(ImageType.Radiograph,ImageType.Photo,ImageType.Attachment)) {
				qualityL=100;
			}
			else {//Assume document
						//Possible values 0-100?
				qualityL=(long)ComputerPrefs.LocalComputer.ScanDocQuality;
			}
			ImageCodecInfo imageCodecInfo;
			ImageCodecInfo[] encoders;
			encoders = ImageCodecInfo.GetImageEncoders();
			imageCodecInfo = null;
			for(int j = 0;j < encoders.Length;j++) {
				if(encoders[j].MimeType==mimeType) {
					imageCodecInfo = encoders[j];
				}
			}
			EncoderParameters encoderParameters = new EncoderParameters(1);
			EncoderParameter encoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,qualityL);
			encoderParameters.Param[0] = encoderParameter;
			//AutoCrop()?
			try {
				SaveDocument(doc,image,imageCodecInfo,encoderParameters,patFolder,uploadHasExceptions:true); //Makes log entry. Allows the file upload to throw if it fails.
				if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
					Documents.Update(doc);//because SaveDocument stuck the image in doc.RawBase64.
					//no thumbnail yet
				}
			}
			catch(Exception e) {
				e.DoNothing();
				Documents.Delete(doc);
				throw;
			}
			return doc;
		}

		/// <summary>Saves to AtoZ folder, Cloud, or to db.  Saves document based off of the mimeType passed in.</summary>
		public static Document Import(byte[] arrayBytes,long docCategory,ImageType imageType,Patient pat,string mimeType="image/jpeg",string fileExtension=null) {
			string patFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			}
			Document doc=new Document();
			doc.ImgType=imageType;
			if(fileExtension==null) {
				doc.FileName=GetImageFileExtensionByMimeType(mimeType);
			}
			else {
				doc.FileName=fileExtension;
			}
			doc.DateCreated=DateTime.Now;
			doc.PatNum=pat.PatNum;
			doc.DocCategory=docCategory;
			Documents.Insert(doc,pat);//creates filename and saves to db
			doc=Documents.GetByNum(doc.DocNum);
			try {
				SaveDocument(doc,arrayBytes,patFolder);//Makes log entry
				if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
					Documents.Update(doc);//because SaveDocument stuck the image in doc.RawBase64.
					//no thumbnail yet
				}
			}
			catch(Exception e) {
				e.DoNothing();
				Documents.Delete(doc);
				throw;
			}
			return doc;
		}

		///<summary>Returns the file extension for the passed in mime type.</summary>
		private static string GetImageFileExtensionByMimeType(string mimeType) {
			switch(mimeType) {
				case "image/jpeg":
					return ".jpg";
				case "image/png":
					return ".png";
				case "image/tiff":
					return ".tif";
				default:
					throw new NotSupportedException();
			}
		}

		/// <summary>Obviously no support for db storage</summary>
		public static Document ImportForm(string form,long docCategory,Patient pat) {
			string patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			string pathSourceFile=CloudStorage.PathTidy(ODFileUtils.CombinePaths(GetPreferredAtoZpath(),"Forms",form));
			if(!FileAtoZ.Exists(pathSourceFile)) {
				throw new Exception(Lans.g("ContrDocs","Could not find file: ") + pathSourceFile);
			}
			Document doc = new Document();
			doc.FileName = Path.GetExtension(pathSourceFile);
			doc.DateCreated = DateTime.Now;
			doc.DocCategory = docCategory;
			doc.PatNum = pat.PatNum;
			doc.ImgType = ImageType.Document;
			Documents.Insert(doc,pat);//this assigns a filename and saves to db
			doc=Documents.GetByNum(doc.DocNum);
			try {
				if(CloudStorage.IsCloudStorage) {
					//byte[] bytes=OpenPmsDentalBusiness.FileIO.FileAtoZ.ReadAllBytes(pathSourceFile);
					//OpenPmsDentalBusiness.FileIO.FileAtoZ.WriteAllBytes(CloudStorage.PathTidy(ODFileUtils.CombinePaths(patFolder,doc.FileName)),bytes);
					CloudStorage.Copy(pathSourceFile,CloudStorage.PathTidy(ODFileUtils.CombinePaths(patFolder,doc.FileName)));
					ImageStore.LogDocument(Lans.g("ContrImages","Document Created")+": ",EnumPermType.ImageEdit,doc,DateTime.MinValue); //new doc, min date.
				}
				else {
					SaveDocument(doc,pathSourceFile,patFolder);//Makes log entry
				}
			}
			catch {
				Documents.Delete(doc);
				throw;
			}
			return doc;
		}

		/// <summary>Always saves as bmp.  So the 'paste to mount' logic needs to be changed to prevent conversion to bmp.</summary>
		public static Document ImportImageToMount(Bitmap image,short rotationAngle,long mountItemNum,long docCategory,Patient pat) {
			string patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			string fileExtention = ".bmp";//The file extention to save the greyscale image as.
			Document doc = new Document();
			doc.MountItemNum = mountItemNum;
			doc.DegreesRotated = rotationAngle;
			doc.ImgType = ImageType.Radiograph;
			doc.FileName = fileExtention;
			doc.DateCreated = DateTime.Now;
			doc.PatNum = pat.PatNum;
			doc.DocCategory = docCategory;
			doc.PrintHeading=true;
			doc.WindowingMin = PrefC.GetInt(PrefName.ImageWindowingMin);
			doc.WindowingMax = PrefC.GetInt(PrefName.ImageWindowingMax);
			Documents.Insert(doc,pat);//creates filename and saves to db
			doc=Documents.GetByNum(doc.DocNum);
			try {
				SaveDocument(doc,image,ImageFormat.Bmp,patFolder);//Makes log entry
			}
			catch {
				Documents.Delete(doc);
				throw;
			}
			return doc;
		}

		/// <summary>Saves to either AtoZ folder or to db.  Saves image as a jpg.  Compression will be according to user setting.</summary>
		public static EobAttach ImportEobAttach(Bitmap image,long claimPaymentNum) {
			return ImportEobAttach(image,claimPaymentNum,claimNumPreAuth:0);
		}

		/// <summary>Saves to either AtoZ folder or to db.  Saves image as a jpg.  Compression will be according to user setting.
		/// The EOB belongs to the claim payment, or (Open Dental 25.2) to the preauthorization when claimNumPreAuth is set; exactly one of the two.</summary>
		public static EobAttach ImportEobAttach(Bitmap image,long claimPaymentNum,long claimNumPreAuth) {
			EobAttach eob=new EobAttach();
			SetEobOwner(eob,claimPaymentNum,claimNumPreAuth);
			string eobFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				eobFolder=GetEobFolder();
			}
			eob.FileName=".jpg";
			eob.DateTCreated = DateTime.Now;
			EobAttaches.Insert(eob);//creates filename and saves to db
			eob=EobAttaches.GetOne(eob.EobAttachNum);
			long qualityL=(long)ComputerPrefs.LocalComputer.ScanDocQuality;
			ImageCodecInfo myImageCodecInfo;
			ImageCodecInfo[] encoders;
			encoders = ImageCodecInfo.GetImageEncoders();
			myImageCodecInfo = null;
			for(int j = 0;j < encoders.Length;j++) {
				if(encoders[j].MimeType == "image/jpeg") {
					myImageCodecInfo = encoders[j];
				}
			}
			EncoderParameters myEncoderParameters = new EncoderParameters(1);
			EncoderParameter myEncoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,qualityL);
			myEncoderParameters.Param[0] = myEncoderParameter;
			try {
				SaveEobAttach(eob,image,myImageCodecInfo,myEncoderParameters,eobFolder);
				if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
					EobAttaches.Update(eob);//because SaveEobAttach stuck the image in EobAttach.RawBase64.
					//no thumbnail
				}
				//No security log for creation of claim payment EOB's because they don't show up in the images module. Preauth EOBs are logged.
			}
			catch {
				EobAttaches.Delete(eob.EobAttachNum);
				throw;
			}
			LogPreAuthEob(EnumPermType.ImageCreate,eob);
			return eob;
		}

		/// <summary></summary>
		public static EobAttach ImportEobAttach(string pathImportFrom,long claimPaymentNum) {
			return ImportEobAttach(pathImportFrom,claimPaymentNum,claimNumPreAuth:0);
		}

		/// <summary>The EOB belongs to the claim payment, or (Open Dental 25.2) to the preauthorization when claimNumPreAuth is set; exactly one of the
		/// two.</summary>
		public static EobAttach ImportEobAttach(string pathImportFrom,long claimPaymentNum,long claimNumPreAuth) {
			EobAttach eob=new EobAttach();
			SetEobOwner(eob,claimPaymentNum,claimNumPreAuth);
			string eobFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				eobFolder=GetEobFolder();
			}
			if(Path.GetExtension(pathImportFrom)=="") {//If the file has no extension
				eob.FileName=".jpg";
			}
			else {
				eob.FileName=Path.GetExtension(pathImportFrom);
			}
			eob.DateTCreated=File.GetLastWriteTime(pathImportFrom);
			EobAttaches.Insert(eob);//creates filename and saves to db
			eob=EobAttaches.GetOne(eob.EobAttachNum);
			try {
				SaveEobAttach(eob,pathImportFrom,eobFolder);
				//No security log for creation of claim payment EOB's because they don't show up in the images module. Preauth EOBs are logged.
				if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
					EobAttaches.Update(eob);
				}
			}
			catch {
				EobAttaches.Delete(eob.EobAttachNum);
				throw;
			}
			LogPreAuthEob(EnumPermType.ImageCreate,eob);
			return eob;
		}

		///<summary>Sets the one owner of a new EOB. Throws when neither or both keys are set, because every eobattach row belongs to exactly one claim
		///payment or one preauthorization.</summary>
		private static void SetEobOwner(EobAttach eob,long claimPaymentNum,long claimNumPreAuth) {
			eob.ClaimPaymentNum=Math.Max(0,claimPaymentNum);
			eob.ClaimNumPreAuth=Math.Max(0,claimNumPreAuth);
			if(!EobAttaches.HasValidOwner(eob)) {
				throw new ArgumentException(Lans.g("ContrImages","An EOB must belong to exactly one insurance payment or one preauthorization."));
			}
		}

		///<summary>The security log text for an EOB on a preauthorization (Open Dental 25.2). Always contains "Preauth EOB".</summary>
		public static string GetPreAuthEobLogText(EnumPermType perm,string fileName) {
			string action=(perm==EnumPermType.ImageDelete ? Lans.g("ContrImages","deleted") : Lans.g("ContrImages","attached"));
			return "Preauth EOB "+action+": "+fileName;
		}

		///<summary>Security log for an EOB on a preauthorization (Open Dental 25.2): ImageCreate when one is attached, ImageDelete when one is removed,
		///with the patient of the preauthorization and "Preauth EOB" in the text. Claim payment EOBs keep having no log entry.</summary>
		private static void LogPreAuthEob(EnumPermType perm,EobAttach eob) {
			if(eob==null || eob.ClaimNumPreAuth<=0) {
				return;
			}
			Claim claim=Claims.GetClaim(eob.ClaimNumPreAuth);
			long patNum=(claim==null ? 0 : claim.PatNum);
			SecurityLogs.MakeLogEntry(perm,patNum,GetPreAuthEobLogText(perm,eob.FileName));
		}

		public static EhrAmendment ImportAmdAttach(Bitmap image,EhrAmendment amd) {
			string amdFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				amdFolder=GetAmdFolder();
			}
			amd.FileName=DateTime.Now.ToString("yyyyMMdd_HHmmss_")+amd.EhrAmendmentNum;
			amd.FileName+=".jpg";
			amd.DateTAppend=DateTime.Now;
			EhrAmendments.Update(amd);
			amd=EhrAmendments.GetOne(amd.EhrAmendmentNum);
			long qualityL=(long)ComputerPrefs.LocalComputer.ScanDocQuality;
			ImageCodecInfo myImageCodecInfo;
			ImageCodecInfo[] encoders;
			encoders = ImageCodecInfo.GetImageEncoders();
			myImageCodecInfo = null;
			for(int j = 0;j < encoders.Length;j++) {
				if(encoders[j].MimeType == "image/jpeg") {
					myImageCodecInfo = encoders[j];
				}
			}
			EncoderParameters myEncoderParameters = new EncoderParameters(1);
			EncoderParameter myEncoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,qualityL);
			myEncoderParameters.Param[0] = myEncoderParameter;
			try {
				SaveAmdAttach(amd,image,myImageCodecInfo,myEncoderParameters,amdFolder);
				//No security log for creation of AMD Attaches because they don't show up in the images module
			}
			catch {
				//EhrAmendments.Delete(amd.EhrAmendmentNum);
				throw;
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				//EhrAmendments.Update(amd);
				//no thumbnail
			}
			return amd;
		}

		public static EhrAmendment ImportAmdAttach(string pathImportFrom,EhrAmendment amd) {
			string amdFolder="";
			string amdFilename="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				amdFolder=GetAmdFolder();
				amdFilename=amd.FileName;
			}
			amd.FileName=DateTime.Now.ToString("yyyyMMdd_HHmmss_")+amd.EhrAmendmentNum+Path.GetExtension(pathImportFrom);
			if(Path.GetExtension(pathImportFrom)=="") {//If the file has no extension
				amd.FileName+=".jpg";
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				//S0-6: with images in the database the file is appended too, as with an A to Z folder and in the bitmap overload. SaveAmdAttach stores
				//the date in its one update of the row, with RawBase64.
				amd.DateTAppend=DateTime.Now;
			}
			//EhrAmendments.Update(amd);
			//amd=EhrAmendments.GetOne(amd.EhrAmendmentNum);
			try {
				SaveAmdAttach(amd,pathImportFrom,amdFolder);
				//No security log for creation of AMD Attaches because they don't show up in the images module
			}
			catch {
				//EhrAmendments.Delete(amd.EhrAmendmentNum);
				throw;
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				amd.DateTAppend=DateTime.Now;
				EhrAmendments.Update(amd);
				CleanAmdAttach(amdFilename);
			}
			return amd;
		}

		///<summary>Saves image obtained from eClipboard as well as inserting it into the database. Call ImageCaptureXs.SaveImageCaptureXs() to properly set up parameters. Returns Document</summary>
		public static Document ImportImage(byte[] imageBytes,string header,Patient pat,long defNum,EncoderParameters encoderParams,ImageCodecInfo imgCodecInfo) {
			//Format the description and part of the file name to be used when saving the image. Appends a number at the end if the file name already exists
			string getDocDescription(string header,string fileFolderPath,long patNum,string date) {
				string cleanFileName=ODFileUtils.CleanFileName($@"{patNum}_{header}_{date}.bmp");
				if(!File.Exists($@"{fileFolderPath}\{cleanFileName}")) {
					return ODFileUtils.CleanFileName($"{header}_{date}");
				}
				int x=0;
				string docDesc="";
				do {
					x++;
					docDesc=ODFileUtils.CleanFileName($"{header}_{date}_{x}");
				}
				while(File.Exists($@"{fileFolderPath}\{patNum}_{docDesc}.bmp"));
				return docDesc;
			}
			Document doc=new Document();
			string patFolder=GetPatientFolder(pat,GetPreferredAtoZpath());
			string dateForDoc=DateTime.Today.ToString("yyyyMMdd");
			string docDescription=getDocDescription(header,patFolder,pat.PatNum,dateForDoc);
			doc.Description=docDescription;
			doc.DocCategory=defNum;
			doc.FileName=$"{pat.PatNum}_{docDescription}.bmp";
			doc.PatNum=pat.PatNum;
			doc.ImgType=ImageType.Photo;
			doc.DateCreated=DateTime.Now;
			long docNum=Documents.Insert(doc);
			doc=Documents.GetByNum(docNum);
			using (MemoryStream mStream=new MemoryStream(imageBytes))
			using (Bitmap bp=new Bitmap(mStream)) {
				SaveDocument(doc,bp,imgCodecInfo,encoderParams,patFolder);
			}
			return doc;
		}

		///<summary> Save a Document to another location on the disk (outside of Open Dental). </summary>
		public static void Export(string saveToPath,Document doc,Patient pat) {
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				byte[] rawData=Convert.FromBase64String(doc.RawBase64);
				using(FileStream file=new FileStream(saveToPath,FileMode.Create,FileAccess.Write)) {
					file.Write(rawData,0,rawData.Length);
					file.Close();
				}
			}
			else {//Using an AtoZ folder
				string docPath=FileAtoZ.CombinePaths(GetPatientFolder(pat,GetPreferredAtoZpath()),doc.FileName);
				FileAtoZ.Copy(docPath,saveToPath,FileAtoZSourceDestination.AtoZToLocal,doOverwrite:true);
			}
		}

		///<summary> Save an Eob to another location on the disk (outside of Open Dental). With images in the database, writes the stored bytes of
		///eobattach.RawBase64 as they are, so a PDF exports too (S0-6: this read an empty stream and failed). An existing file at saveToPath is
		///replaced in every storage mode, as Export does for a document (S0-6: with an A to Z folder this failed); the Save dialog asks first.</summary>
		public static void ExportEobAttach(string saveToPath,EobAttach eob) {
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				File.WriteAllBytes(saveToPath,GetRawBytes(eob.RawBase64,eob.FileName));
			}
			else {//Using an AtoZ folder
				string eobPath=ODFileUtils.CombinePaths(GetEobFolder(),eob.FileName);
				FileAtoZ.Copy(eobPath,saveToPath,FileAtoZSourceDestination.AtoZToLocal,doOverwrite:true);
			}
		}

		///<summary> Save an EHR amendment to another location on the disk (outside of Open Dental). With images in the database, writes the stored
		///bytes of ehramendment.RawBase64 as they are (S0-6: this read an empty stream and failed). An existing file at saveToPath is replaced in
		///every storage mode, as for an EOB.</summary>
		public static void ExportAmdAttach(string saveToPath,EhrAmendment amd) {
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				File.WriteAllBytes(saveToPath,GetRawBytes(amd.RawBase64,amd.FileName));
			}
			else {//Using an AtoZ folder
				string eobPath=ODFileUtils.CombinePaths(GetAmdFolder(),amd.FileName);
				FileAtoZ.Copy(eobPath,saveToPath,FileAtoZSourceDestination.AtoZToLocal,doOverwrite:true);
			}
		}

		///<summary>The bytes of a file stored in the database (an EOB's or an amendment's RawBase64). Throws an ApplicationException when there are
		///none, so nothing writes an empty file in its place.</summary>
		internal static byte[] GetRawBytes(string rawBase64,string fileName) {
			if(string.IsNullOrEmpty(rawBase64)) {
				throw new ApplicationException(Lans.g("ImageStore","File not found:")+" "+fileName);
			}
			return Convert.FromBase64String(rawBase64);
		}

		///<summary>Saves the image to the given fileName. Quality is 0 to 100.  0 is most compression, lowest quality.</summary>
		public static void SaveBitmapJpg(Bitmap bitmap, string fileName, long quality) {
			if(bitmap==null) {
				return;
			}
			EncoderParameter encoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,quality);
			ImageCodecInfo ImageCodecInfo = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType=="image/jpeg");
			EncoderParameters encoderParameters = new EncoderParameters(1);
			encoderParameters.Param[0] = encoderParameter;
			bitmap.Save(fileName,ImageCodecInfo,encoderParameters);
		}

		/// <summary>Uses the fileName extension to determine MimeType. The default quality is 90 if no value is passed in. 100 quality is 0% compression.</summary>
		public static void SaveBitmap(Bitmap bitmap,string fileName,long quality=90) {
			if(bitmap==null) {
				return;
			}
			string extension=Path.GetExtension(fileName);
			EncoderParameter encoderParameter=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,quality);
			EncoderParameters encoderParameters=new EncoderParameters(1);
			//Image will be saved as JPEG by default. So we don't need to check for .jpeg.
			ImageCodecInfo imageCodecInfo=ImageCodecInfo.GetImageEncoders().First(x=>x.MimeType=="image/jpeg");
			encoderParameters.Param[0]=encoderParameter;
			if(extension==".tiff"||extension==".tif") {
				imageCodecInfo=ImageCodecInfo.GetImageEncoders().First(x=>x.MimeType=="image/tiff");
				bitmap.Save(fileName,imageCodecInfo,encoderParameters);
				return;
			}
			if(extension==".bmp") {
				imageCodecInfo=ImageCodecInfo.GetImageEncoders().First(x=>x.MimeType=="image/bmp");
				bitmap.Save(fileName,imageCodecInfo,encoderParameters);
				return;
			}
			if(extension==".png") {
				imageCodecInfo=ImageCodecInfo.GetImageEncoders().First(x=>x.MimeType=="image/png");
				bitmap.Save(fileName,imageCodecInfo,encoderParameters);
				return;
			}
			if(extension==".gif") {
				imageCodecInfo=ImageCodecInfo.GetImageEncoders().First(x=>x.MimeType=="image/gif");
				bitmap.Save(fileName,imageCodecInfo,encoderParameters);
				return;
			}
			bitmap.Save(fileName,imageCodecInfo,encoderParameters);
		}

		///<summary>If using AtoZ folder, then patFolder must be fully qualified and valid.  
		///If not using AtoZ folder, this uploads to Cloud or fills the doc.RawBase64 which must then be updated to db.  
		///The image format can be bmp, jpg, etc, but this overload does not allow specifying jpg compression quality.</summary>
		public static void SaveDocument(Document doc,Bitmap image,ImageFormat format,string patFolder) {
			using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Write,patFolder,doc?.FileName);
			//Had to reassign image to new bitmap due to a possible C# bug. Would sometimes cause UE: "A generic error occurred in GDI+."
			using(Bitmap bitmap=new Bitmap(image)) {
				if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
					string pathFileOut = ODFileUtils.CombinePaths(patFolder,doc.FileName);
					bitmap.Save(pathFileOut);
				}
				else if(CloudStorage.IsCloudStorage) {
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,format);
						CloudStorage.Upload(patFolder,doc.FileName,stream.ToArray());
					}
				}
				else {//saving to db
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,format);
						byte[] rawData=stream.ToArray();
						doc.RawBase64=Convert.ToBase64String(rawData);
					}
				}
			}
			LogDocument(Lans.g("ContrImages","Document Created")+": ",EnumPermType.ImageEdit,doc,DateTime.MinValue); //a brand new document is always passed-in
		}

		///<summary>If usingAtoZfoler, then patFolder must be fully qualified and valid.  If not usingAtoZ folder, this uploads to Cloud or fills the doc.RawBase64 which must then be updated to db.</summary>
		public static void SaveDocument(Document doc,Bitmap image,ImageCodecInfo codec,EncoderParameters encoderParameters,string patFolder,bool uploadHasExceptions=false) {
			using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Write,patFolder,doc?.FileName);
			//Had to reassign image to new bitmap due to a possible C# bug. Would sometimes cause UE: "A generic error occurred in GDI+."
			using(Bitmap bitmap=new Bitmap(image)) {
				if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {//if saving to AtoZ folder
					bitmap.Save(ODFileUtils.CombinePaths(patFolder,doc.FileName),codec,encoderParameters);
				}
				else if(CloudStorage.IsCloudStorage) {
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,codec,encoderParameters);
						CloudStorage.Upload(patFolder,doc.FileName,stream.ToArray(),hasExceptions:uploadHasExceptions);
					}
				}
				else {//if saving to db
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,codec,encoderParameters);
						byte[] rawData=stream.ToArray();
						doc.RawBase64=Convert.ToBase64String(rawData);
					}
				}
			}
			LogDocument(Lans.g("ContrImages",doc.ImgType+" Created")+": ",EnumPermType.ImageCreate,doc,DateTime.MinValue); //a brand new document is always passed-in
		}

		///<summary>If using AtoZfolder, then patFolder must be fully qualified and valid.  If not using AtoZfolder, this uploads to Cloud or fills the eob.RawBase64 which must then be updated to db.</summary>
		public static void SaveDocument(Document doc,string pathSourceFile,string patFolder) {
			using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Write,patFolder,doc?.FileName);
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				File.Copy(pathSourceFile,ODFileUtils.CombinePaths(patFolder,doc.FileName));
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Upload(patFolder,doc.FileName,File.ReadAllBytes(pathSourceFile));
			}
			else {//saving to db
				byte[] rawData=File.ReadAllBytes(pathSourceFile);
				doc.RawBase64=Convert.ToBase64String(rawData);
			}
			LogDocument(Lans.g("ContrImages",doc.ImgType+" Created")+": ",EnumPermType.ImageCreate,doc,DateTime.MinValue); //a brand new document is always passed-in
		}

		///<summary>If using AtoZfolder, then patFolder must be fully qualified and valid.  If not using AtoZfolder, this uploads to Cloud or fills the doc.RawBase64 which must then be updated to db.</summary>
		public static void SaveDocument(Document doc,byte[] arrayBytes,string patFolder) {
			using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Write,patFolder,doc?.FileName);
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				File.WriteAllBytes(ODFileUtils.CombinePaths(patFolder,doc.FileName),arrayBytes);
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Upload(patFolder,doc.FileName,arrayBytes);
			}
			else {//Assume DataStorageType.InDatabase
				doc.RawBase64=Convert.ToBase64String(arrayBytes);
			}
			LogDocument(Lans.g("ContrImages",doc.ImgType+" Created")+": ",EnumPermType.ImageCreate,doc,DateTime.MinValue); //a brand new document is always passed-in
		}

		///<summary>If using AtoZfolder, then patFolder must be fully qualified and valid.  If not using AtoZfolder, this fills the eob.RawBase64 which must then be updated to db.</summary>
		public static void SaveEobAttach(EobAttach eob,Bitmap image,ImageCodecInfo codec,EncoderParameters encoderParameters,string eobFolder) {
			//Had to reassign image to new bitmap due to a possible C# bug. Would sometimes cause UE: "A generic error occurred in GDI+."
			using(Bitmap bitmap=new Bitmap(image)) {
				if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
					bitmap.Save(ODFileUtils.CombinePaths(eobFolder,eob.FileName),codec,encoderParameters);
				}
				else if(CloudStorage.IsCloudStorage) {
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,codec,encoderParameters);
						CloudStorage.Upload(eobFolder,eob.FileName,stream.ToArray());
					}
				}
				else {//saving to db
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,codec,encoderParameters);
						byte[] rawData=stream.ToArray();
						eob.RawBase64=Convert.ToBase64String(rawData);
					}
				}
			}
			//No security log for creation of EOB because they don't show up in the images module.
		}

		///<summary>If using AtoZfolder, then patFolder must be fully qualified and valid.  If not using AtoZfolder, this fills the eob.RawBase64 which must then be updated to db.</summary>
		public static void SaveAmdAttach(EhrAmendment amd,Bitmap image,ImageCodecInfo codec,EncoderParameters encoderParameters,string amdFolder) {
			//Had to reassign image to new bitmap due to a possible C# bug. Would sometimes cause UE: "A generic error occurred in GDI+."
			using(Bitmap bitmap=new Bitmap(image)) {
				if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
					bitmap.Save(ODFileUtils.CombinePaths(amdFolder,amd.FileName),codec,encoderParameters);
				}
				else if(CloudStorage.IsCloudStorage) {
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,codec,encoderParameters);
						CloudStorage.Upload(amdFolder,amd.FileName,stream.ToArray());
					}
				}
				else {//saving to db
					using(MemoryStream stream=new MemoryStream()) {
						bitmap.Save(stream,codec,encoderParameters);
						byte[] rawData=stream.ToArray();
						amd.RawBase64=Convert.ToBase64String(rawData);
						EhrAmendments.Update(amd);
					}
				}
			}
			//No security log for creation of AMD Attaches because they don't show up in the images module
		}

		///<summary>If using AtoZfolder, then patFolder must be fully qualified and valid.  If not using AtoZfolder, this fills the eob.RawBase64 which must then be updated to db.</summary>
		public static void SaveEobAttach(EobAttach eob,string pathSourceFile,string eobFolder) {
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				File.Copy(pathSourceFile,ODFileUtils.CombinePaths(eobFolder,eob.FileName));
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Upload(eobFolder,eob.FileName,File.ReadAllBytes(pathSourceFile));
			}
			else {//saving to db
				byte[] rawData=File.ReadAllBytes(pathSourceFile);
				eob.RawBase64=Convert.ToBase64String(rawData);
			}
			//No security log for creation of EOB because they don't show up in the images module
		}

		///<summary>If using AtoZfolder, then patFolder must be fully qualified and valid.  If not using AtoZfolder, this fills the eob.RawBase64 which must then be updated to db.</summary>
		public static void SaveAmdAttach(EhrAmendment amd,string pathSourceFile,string amdFolder) {
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				File.Copy(pathSourceFile,ODFileUtils.CombinePaths(amdFolder,amd.FileName));
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Upload(amdFolder,amd.FileName,File.ReadAllBytes(pathSourceFile));
			}
			else {//saving to db
				byte[] rawData=File.ReadAllBytes(pathSourceFile);
				amd.RawBase64=Convert.ToBase64String(rawData);
				EhrAmendments.Update(amd);
			}
			//No security log for creation of AMD Attaches because they don't show up in the images module
		}

		///<summary>For each of the documents in the list, deletes row from db and image from AtoZ folder if needed.  Throws exception if the file cannot be deleted.  Surround in try/catch.</summary>
		public static void DeleteDocuments(IList<Document> documents,string patFolder) {
			for(int i=0;i<documents.Count;i++) {
				if(documents[i]==null) {
					continue;
				}
				//Check if document is referenced by a sheet. (PatImages)
				List<Sheet> sheetRefList=Sheets.GetForDocument(documents[i].DocNum);
				if(sheetRefList.Count!=0) {
					//throw Exception with error message.
					string msgText=Lans.g("ContrImages","Cannot delete image, it is referenced by sheets with the following dates")+":";
					foreach(Sheet sheet in sheetRefList) {
						msgText+="\r\n"+sheet.DateTimeSheet.ToShortDateString();
					}
					throw new Exception(msgText);
				}
				//Attempt to delete the file.
				if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
					try {
						string filePath = ODFileUtils.CombinePaths(patFolder,documents[i].FileName);
						using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Delete,filePath);
						if(File.Exists(filePath)) {
							File.Delete(filePath);
							LogDocument(Lans.g("ContrImages","Document Deleted")+": ",EnumPermType.ImageDelete,documents[i],documents[i].DateTStamp);
						}
					}
					catch {
						throw new Exception(Lans.g("ContrImages","Could not delete file.  It may be in use by another program, flagged as read-only, or you might not have sufficient permissions."));
					}
				}
				else if(CloudStorage.IsCloudStorage) {
					CloudStorage.Delete(ODFileUtils.CombinePaths(patFolder,documents[i].FileName,'/'));
				}
				//S0-F: the thumbnail too (a small copy of the image), which the Imaging module saved and nothing removed before.
				DeleteThumbnailOfDeletedDocument(documents[i],patFolder);
				//Row from db.  This deletes the "image file" also if it's stored in db.
				Documents.Delete(documents[i]);
				ImageDraws.DeleteByDocNum(documents[i].DocNum);
				PearlRequests.DeleteByDocNum(documents[i].DocNum);
				//24.4: deleting a signed image needs SignedImageEdit, so it is logged under that permission too.
				ImageSignaturePermissions.LogSignedImageDeleted(documents[i]);
			}//end documents
		}

		///<summary>Also handles deletion of db object.</summary>
		public static void DeleteEobAttach(EobAttach eob) {
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string eobFolder=GetEobFolder();
				string filePath=ODFileUtils.CombinePaths(eobFolder,eob.FileName);
				if(File.Exists(filePath)) {
					try {
						File.Delete(filePath);
						//No security log for deletion of EOB's because they don't show up in the images module.
					}
					catch { }//file seems to be frequently locked.
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Delete(ODFileUtils.CombinePaths(GetEobFolder(),eob.FileName,'/'));
			}
			//db
			EobAttaches.Delete(eob.EobAttachNum);
			LogPreAuthEob(EnumPermType.ImageDelete,eob);
		}

		///<summary>Deletes every EOB attached to a preauthorization: the files in the EOBs folder (or cloud) and the rows. Used when the preauthorization
		///itself is deleted. Returns how many were deleted.</summary>
		public static int DeleteEobAttachesForPreAuth(long claimNumPreAuth) {
			List<EobAttach> listEobAttaches=EobAttaches.RefreshForPreAuth(claimNumPreAuth);
			for(int i=0;i<listEobAttaches.Count;i++) {
				DeleteEobAttach(listEobAttaches[i]);
			}
			return listEobAttaches.Count;
		}

		///<summary>Also handles deletion of db object.</summary>
		public static void DeleteAmdAttach(EhrAmendment amendment) {
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string amdFolder=GetAmdFolder();
				string filePath=ODFileUtils.CombinePaths(amdFolder,amendment.FileName);
				if(File.Exists(filePath)) {
					try {
						File.Delete(filePath);
						//No security log for deletion of AMD Attaches because they don't show up in the images module.
					}
					catch {
						MessageBox.Show("Delete was unsuccessful. The file may be in use.");
						return;
					}//file seems to be frequently locked.
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Delete(ODFileUtils.CombinePaths(GetAmdFolder(),amendment.FileName,'/'));
			}
			//db
			amendment.DateTAppend=DateTime.MinValue;
			amendment.FileName="";
			amendment.RawBase64="";
			EhrAmendments.Update(amendment);
		}

		///<summary>Attempts to delete the file for the given filePath, return true if no exception occurred (doesnt mean a file was deleted necessarily).
		///actInUseException is invoked with an exception message. Up to developer on if/what they would like to do anything with it.</summary>
		public static bool TryDeleteFile(string filePath,Action<string> actInUseException=null) {
			try {
				File.Delete(filePath);
			}
			catch(Exception ex) {
				if(!ex.Message.ToLower().Contains("being used by another process")) {
					throw ex;//Currently we only care about the above exception. Other exceptions should be brought to our attention still.
				}
				actInUseException?.Invoke(ex.Message);
				return false;
			}
			return true;
		}

		///<summary>Cleans up unreferenced Amendments</summary>
		public static void CleanAmdAttach(string amdFileName) {
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string amdFolder=GetAmdFolder();
				string filePath=ODFileUtils.CombinePaths(amdFolder,amdFileName);
				if(File.Exists(filePath)) {
					try {
						File.Delete(filePath);
						//No security log for deletion of AMD Attaches because they don't show up in the images module.
					}
					catch {
						//MessageBox.Show("Delete was unsuccessful. The file may be in use.");
						return;
					}//file seems to be frequently locked.
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Delete(ODFileUtils.CombinePaths(GetAmdFolder(),amdFileName,'/'));
			}
		}

		///<summary>The thumbnail will then be recreated in Documents.GetThumbnail.</summary>
		public static void DeleteThumbnailImage(Document doc,string patFolder) {
			/*if(ImageStoreIsDatabase) {
				using(IDbConnection connection = DataSettings.GetConnection())
				using(IDbCommand command = connection.CreateCommand()) {
					command.CommandText =
					@"UPDATE files SET Thumbnail = NULL WHERE DocNum = ?DocNum";

					IDataParameter docNumParameter = command.CreateParameter();
					docNumParameter.ParameterName = "?DocNum";
					docNumParameter.Value = doc.DocNum;
					command.Parameters.Add(docNumParameter);

					connection.Open();
					command.ExecuteNonQuery();
					connection.Close();
				}
			}
			else {*/
			//string patFolder=GetPatientFolder(pat);
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string thumbnailFile=ODFileUtils.CombinePaths(patFolder,"Thumbnails",doc.FileName);
				if(File.Exists(thumbnailFile)) {
					try {
						File.Delete(thumbnailFile);
					}
					catch {
						//Two users *might* edit the same image at the same time, so the image might already be deleted.
					}
				}
			}
			else if(CloudStorage.IsCloudStorage) {
				CloudStorage.Delete(ODFileUtils.CombinePaths(patFolder,"Thumbnails",doc.FileName,'/'));
			}
		}

		///<summary>S0-F: deletes the thumbnail of a document that is being deleted, Thumbnails\[its file name] in the patient's folder (DeleteDocuments
		///calls it for every document it deletes). Open Dental left it behind, so a deleted image stayed on disk as its thumbnail. See
		///DeleteThumbnailFile for the storage modes. Never throws.</summary>
		public static void DeleteThumbnailOfDeletedDocument(Document document,string patFolder) {
			if(document==null) {
				return;
			}
			DeleteThumbnailFile(patFolder,document.FileName,GetStorageTypeNoThrow());
		}

		///<summary>S0-F: deletes the thumbnail of a mount that is being deleted, Thumbnails\Mount[MountNum].jpg in the patient's folder (the name
		///Mounts.GetThumbnail and the Imaging module use). Call it where the mount is deleted, with the patient's folder. Never throws.</summary>
		public static void DeleteThumbnailOfDeletedMount(Mount mount,string patFolder) {
			if(mount==null) {
				return;
			}
			DeleteThumbnailFile(patFolder,"Mount"+mount.MountNum.ToString()+".jpg",GetStorageTypeNoThrow());
		}

		///<summary>S0-F: deletes patFolder\Thumbnails\fileName. Thumbnails are files only with an A to Z folder (LocalAtoZ): Documents.GetThumbnail,
		///Mounts.GetThumbnail and the Imaging module save them nowhere else, so with cloud storage or images in the database there is nothing to delete
		///and nothing is sent anywhere. Only a plain file name in an existing patient folder is used, so a blank folder or name never points at another
		///file. Never throws: a thumbnail that is locked stays behind, as every thumbnail did before, and nothing shows it once its document or mount
		///is gone. True when no such thumbnail is left.</summary>
		public static bool DeleteThumbnailFile(string patFolder,string fileName,DataStorageType dataStorageType) {
			if(dataStorageType!=DataStorageType.LocalAtoZ) {
				return true;//No thumbnail files with this storage.
			}
			if(string.IsNullOrWhiteSpace(patFolder) || string.IsNullOrWhiteSpace(fileName)) {
				return true;//No patient folder or no file name: no thumbnail of its own.
			}
			try {
				if(fileName!=Path.GetFileName(fileName) || !Directory.Exists(patFolder)) {
					return true;
				}
				string pathThumbnail=ODFileUtils.CombinePaths(patFolder,"Thumbnails",fileName);
				if(File.Exists(pathThumbnail)) {
					File.Delete(pathThumbnail);
				}
				return !File.Exists(pathThumbnail);
			}
			catch(Exception ex) {
				ex.DoNothing();//Locked, read-only or no permission: the thumbnail stays, as before.
				return false;
			}
		}

		///<summary>PrefC.AtoZfolderUsed, or InDatabase (no thumbnail files) when the preference cannot be read.</summary>
		private static DataStorageType GetStorageTypeNoThrow() {
			try {
				return PrefC.AtoZfolderUsed;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return DataStorageType.InDatabase;
			}
		}

		public static string GetExtension(Document doc) {
			return Path.GetExtension(doc.FileName).ToLower();
		}

		public static string GetFilePath(Document doc,string patFolder) {
			//string patFolder=GetPatientFolder(pat);
			return FileAtoZ.CombinePaths(patFolder,doc.FileName);
		}

		/*
		public static bool IsImageFile(string filename) {
			try {
				Bitmap bitmap = new Bitmap(filename);
				bitmap.Dispose();
				bitmap=null;
				return true;
			}
			catch {
				return false;
			}
		}*/

		///<summary>Returns true if the given filename contains a supported file image extension.</summary>
		public static bool HasImageExtension(string fileName) {
			string ext = Path.GetExtension(fileName).ToLower();
			//The following supported bitmap types were found on a microsoft msdn page:
			//==02/25/2014 - Added .tig as an accepted image extention for tigerview enhancement.
			return (ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".bmp" || ext == ".tif" ||
				ext == ".tiff" || ext == ".gif" || ext == ".emf" || ext == ".exif" || ext == ".ico" || ext == ".png" || ext == ".wmf" || ext == ".tig");
		}

		///<summary>Makes log entry for documents.  Supply beginning text, permission, document, and the DateTStamp that the document was previously last 
		///edited.</summary>
		public static void LogDocument(string logMsgStart,EnumPermType perm,Document doc, DateTime secDatePrevious,long userNum=0) {
			string logMsg=logMsgStart+doc.FileName;
			if(doc.Description!="") {
				string descriptDoc=doc.Description;
				if(descriptDoc.Length>50) {
					descriptDoc=descriptDoc.Substring(0,50);
				}
				logMsg+=" "+Lans.g("ContrImages","with description")+" "+descriptDoc;
			}
			Def docCat=Defs.GetDef(DefCat.ImageCats,doc.DocCategory);
			logMsg+=" "+Lans.g("ContrImages","with category")+" "+docCat.ItemName;
			if(userNum==0) {
				SecurityLogs.MakeLogEntry(perm,doc.PatNum,logMsg,doc.DocNum,secDatePrevious);
				return;
			}
			SecurityLogs.MakeLogEntry(perm,doc.PatNum,logMsg,doc.DocNum,LogSources.None,secDatePrevious,userNum);
		}

	}

	///<summary>The logic of the EOB and amendment window (FormImages), without its UI (S0-6, rebuilt on the current Imaging module). The window
	///shows the EOBs of one claim payment (Insurance Payment, Edit Claim) or of one preauthorization (Edit Claim, Enter Estimates for PreAuth;
	///eobattach.ClaimNumPreAuth, Open Dental 25.2), oldest first, or the one file of an EHR amendment (Edit Amendment; ehramendment.FileName).
	///Every file stays where ImageStore keeps it: the EOBs and Amendments folders of the A to Z folder or of cloud storage, or the row's RawBase64
	///with images in the database, under the same file names. The ImageCreate and ImageDelete log entries are ImageStore's (a preauthorization
	///EOB has them; a claim payment EOB and an amendment's file have none); the Printing and Copy entries are worded here. Client side only, like
	///ImageStore: every database call goes through the S classes.</summary>
	public static class EobAmendmentFiles {
		#region Enums
		///<summary>What the window shows.</summary>
		public enum EnumMode {
			///<summary>No owner was given. The window shows nothing.</summary>
			None,
			///<summary>The EOBs of one claim payment (eobattach.ClaimPaymentNum).</summary>
			ClaimPayment,
			///<summary>The EOBs of one preauthorization (eobattach.ClaimNumPreAuth).</summary>
			PreAuth,
			///<summary>The one file of an EHR amendment (ehramendment.FileName); none while FileName is empty.</summary>
			Amendment,
		}

		///<summary>The window's commands, in toolbar order.</summary>
		public enum EnumCommand {
			Print,
			Delete,
			///<summary>Scan Document: one page, saved as JPG at this computer's scan quality and also put on the clipboard.</summary>
			ScanDoc,
			///<summary>Scan Multi-Page Document: saved as PDF from a temp file, which is then deleted.</summary>
			ScanMultiDoc,
			Import,
			Export,
			Copy,
			Paste,
			Close,
		}

		///<summary>How the window shows a file.</summary>
		public enum EnumShowAs {
			///<summary>Neither an image nor a PDF: nothing shows.</summary>
			Nothing,
			///<summary>Fitted, with the zoom slider and drag to pan.</summary>
			Image,
			///<summary>In the PDF viewer, which pages through it.</summary>
			Pdf,
		}
		#endregion Enums

		#region Nested class
		///<summary>One file in the window's list: an EOB, or the amendment's file.</summary>
		public class FileEntry {
			///<summary>The EOB's key. 0 for an amendment's file.</summary>
			public long EobAttachNum;
			///<summary>The amendment's key. 0 for an EOB.</summary>
			public long EhrAmendmentNum;
			///<summary>The stored file name, which the list shows.</summary>
			public string FileName="";
			///<summary>The amendment's description, which its print log entry names. Empty for an EOB.</summary>
			public string Description="";
			///<summary>The amendment's patient. 0 for an EOB.</summary>
			public long PatNum;

			///<summary>True for an EOB, false for an amendment's file.</summary>
			public bool IsEob {
				get {
					return EobAttachNum!=0;
				}
			}
		}
		#endregion Nested class

		#region Methods - Modes and commands
		///<summary>The mode for the window's three public fields, chosen as the old window chose: a preauthorization first, then a claim payment,
		///then an amendment.</summary>
		public static EnumMode GetMode(long claimPaymentNum,long claimNumPreAuth,EhrAmendment ehrAmendment) {
			if(claimNumPreAuth!=0) {
				return EnumMode.PreAuth;
			}
			if(claimPaymentNum!=0) {
				return EnumMode.ClaimPayment;
			}
			if(ehrAmendment!=null) {
				return EnumMode.Amendment;
			}
			return EnumMode.None;
		}

		///<summary>True for the two EOB modes.</summary>
		public static bool IsEobMode(EnumMode mode) {
			return mode==EnumMode.ClaimPayment || mode==EnumMode.PreAuth;
		}

		///<summary>The toolbar commands of the mode, in order: the same nine for EOBs and for an amendment, as in the old window, and only Close when
		///there is nothing to show. There is no Info, Sign, radiograph or photo scan, crop, rotate, flip or windowing: those work on a patient's
		///documents only.</summary>
		public static List<EnumCommand> GetCommands(EnumMode mode) {
			if(mode==EnumMode.None) {
				return new List<EnumCommand> { EnumCommand.Close };
			}
			return new List<EnumCommand> {
				EnumCommand.Print,
				EnumCommand.Delete,
				EnumCommand.ScanDoc,
				EnumCommand.ScanMultiDoc,
				EnumCommand.Import,
				EnumCommand.Export,
				EnumCommand.Copy,
				EnumCommand.Paste,
				EnumCommand.Close,
			};
		}

		///<summary>The commands of the list's right-click menu: Print and Delete, in both modes, as in the old window.</summary>
		public static List<EnumCommand> GetListMenuCommands(EnumMode mode) {
			if(mode==EnumMode.None) {
				return new List<EnumCommand>();
			}
			return new List<EnumCommand> { EnumCommand.Print,EnumCommand.Delete };
		}

		///<summary>Import takes several files for EOBs and one for an amendment, which has only one file.</summary>
		public static bool IsImportMultiSelect(EnumMode mode) {
			return IsEobMode(mode);
		}

		///<summary>True when scan, import or paste would replace the amendment's file, so the window asks "This will delete your old file.
		///Proceed?" first.</summary>
		public static bool IsReplaceConfirmNeeded(EnumMode mode,EhrAmendment ehrAmendment) {
			return mode==EnumMode.Amendment && ehrAmendment!=null && !string.IsNullOrEmpty(ehrAmendment.FileName);
		}

		///<summary>True when a multi-page scan that ended with an EZTwain error, so that its PDF may be missing pages, needs a second yes before it is
		///stored (S0-6): when it would replace an amendment's file. A partial scan of an EOB is added as before, because it replaces nothing; so is the
		///first file of an amendment that has none.</summary>
		public static bool IsPartialScanConfirmNeeded(EnumMode mode,EhrAmendment ehrAmendment) {
			return IsReplaceConfirmNeeded(mode,ehrAmendment);
		}

		///<summary>An image shows fitted, a PDF in the PDF viewer, any other type not at all.</summary>
		public static EnumShowAs GetShowAs(string fileName) {
			if(ImageStore.HasImageExtension(fileName)) {
				return EnumShowAs.Image;
			}
			if(Path.GetExtension(fileName).ToLower()==".pdf") {
				return EnumShowAs.Pdf;
			}
			return EnumShowAs.Nothing;
		}

		///<summary>True when a double-click opens the file in its Windows program: an EOB other than JPG, JPEG or GIF, as in the old window. An
		///amendment's file never opens this way. With images in the database the window asks for an export instead.</summary>
		public static bool CanOpenInWindowsProgram(FileEntry fileEntry) {
			if(fileEntry==null || !fileEntry.IsEob) {
				return false;
			}
			string extension=Path.GetExtension(fileEntry.FileName).ToLower();
			return extension!=".jpg" && extension!=".jpeg" && extension!=".gif";
		}
		#endregion Methods - Modes and commands

		#region Methods - The list
		///<summary>The window's list: the EOBs of the claim payment or of the preauthorization, oldest first (EobAttaches.RefreshForOwner), or the
		///amendment's one file, none while its FileName is empty.</summary>
		public static List<FileEntry> GetFileEntries(EnumMode mode,long claimPaymentNum,long claimNumPreAuth,EhrAmendment ehrAmendment) {
			List<FileEntry> listFileEntries=new List<FileEntry>();
			if(IsEobMode(mode)) {
				GetOwnerKeys(mode,claimPaymentNum,claimNumPreAuth,out long claimPaymentNumOwner,out long claimNumPreAuthOwner);
				List<EobAttach> listEobAttaches=EobAttaches.RefreshForOwner(claimPaymentNumOwner,claimNumPreAuthOwner);
				for(int i=0;i<listEobAttaches.Count;i++) {
					listFileEntries.Add(GetFileEntry(listEobAttaches[i]));
				}
				return listFileEntries;
			}
			if(mode==EnumMode.Amendment && ehrAmendment!=null && !string.IsNullOrEmpty(ehrAmendment.FileName)) {
				listFileEntries.Add(GetFileEntry(ehrAmendment));
			}
			return listFileEntries;
		}

		public static FileEntry GetFileEntry(EobAttach eobAttach) {
			FileEntry fileEntry=new FileEntry();
			fileEntry.EobAttachNum=eobAttach.EobAttachNum;
			fileEntry.FileName=eobAttach.FileName??"";
			return fileEntry;
		}

		public static FileEntry GetFileEntry(EhrAmendment ehrAmendment) {
			FileEntry fileEntry=new FileEntry();
			fileEntry.EhrAmendmentNum=ehrAmendment.EhrAmendmentNum;
			fileEntry.FileName=ehrAmendment.FileName??"";
			fileEntry.Description=ehrAmendment.Description??"";
			fileEntry.PatNum=ehrAmendment.PatNum;
			return fileEntry;
		}

		///<summary>The one owner of the mode's EOBs: the claim payment, or the preauthorization. Both 0 for any other mode.</summary>
		private static void GetOwnerKeys(EnumMode mode,long claimPaymentNum,long claimNumPreAuth,out long claimPaymentNumOwner,
			out long claimNumPreAuthOwner)
		{
			claimPaymentNumOwner=0;
			claimNumPreAuthOwner=0;
			if(mode==EnumMode.PreAuth) {
				claimNumPreAuthOwner=claimNumPreAuth;
			}
			else if(mode==EnumMode.ClaimPayment) {
				claimPaymentNumOwner=claimPaymentNum;
			}
		}
		#endregion Methods - The list

		#region Methods - Adding, replacing and deleting
		///<summary>A new eobattach row of the claim payment or of the preauthorization, with a copy of the file (ImageStore.ImportEobAttach with both
		///owner keys). A preauthorization EOB logs ImageCreate. Throws, and adds no row, when the file cannot be stored.</summary>
		public static EobAttach AddEob(string pathImportFrom,EnumMode mode,long claimPaymentNum,long claimNumPreAuth) {
			GetOwnerKeys(mode,claimPaymentNum,claimNumPreAuth,out long claimPaymentNumOwner,out long claimNumPreAuthOwner);
			return ImageStore.ImportEobAttach(pathImportFrom,claimPaymentNumOwner,claimNumPreAuthOwner);
		}

		///<summary>A new eobattach row of the claim payment or of the preauthorization from a scanned or pasted image, saved as JPG at this
		///computer's scan quality (ImageStore.ImportEobAttach). A preauthorization EOB logs ImageCreate.</summary>
		public static EobAttach AddEob(Bitmap bitmap,EnumMode mode,long claimPaymentNum,long claimNumPreAuth) {
			GetOwnerKeys(mode,claimPaymentNum,claimNumPreAuth,out long claimPaymentNumOwner,out long claimNumPreAuthOwner);
			return ImageStore.ImportEobAttach(bitmap,claimPaymentNumOwner,claimNumPreAuthOwner);
		}

		///<summary>Replaces the amendment's file with a copy of this one under a new name (ImageStore.ImportAmdAttach), then removes the old file
		///(ImageStore.CleanAmdAttach). Returns the amendment as stored. The window releases the old file before calling this. The object passed in is
		///not changed, and when the replacement fails it throws with the amendment still on its old file (RestoreAfterFailedReplace).</summary>
		public static EhrAmendment ReplaceAmendmentFile(EhrAmendment ehrAmendment,string pathImportFrom) {
			//ImportAmdAttach sets the new name on the object it is given before it stores the file, so it gets a copy.
			EhrAmendment ehrAmendmentWork=ehrAmendment.Clone();
			EhrAmendment ehrAmendmentNew;
			try {
				ehrAmendmentNew=ImageStore.ImportAmdAttach(pathImportFrom,ehrAmendmentWork);
			}
			catch {
				RestoreAfterFailedReplace(ehrAmendment,ehrAmendmentWork.FileName);
				throw;
			}
			RemoveOldAmendmentFile(ehrAmendment.FileName,ehrAmendmentNew.FileName);
			return ehrAmendmentNew;
		}

		///<summary>Replaces the amendment's file with a scanned or pasted image, saved as JPG at this computer's scan quality under a new name
		///(ImageStore.ImportAmdAttach), then removes the old file (ImageStore.CleanAmdAttach). Returns the amendment as stored. The object passed in is
		///not changed, and when the replacement fails it throws with the amendment still on its old file (RestoreAfterFailedReplace).</summary>
		public static EhrAmendment ReplaceAmendmentFile(EhrAmendment ehrAmendment,Bitmap bitmap) {
			//ImportAmdAttach sets the new name on the object it is given, and stores it on the row, before it stores the file.
			EhrAmendment ehrAmendmentWork=ehrAmendment.Clone();
			EhrAmendment ehrAmendmentNew;
			try {
				ehrAmendmentNew=ImageStore.ImportAmdAttach(bitmap,ehrAmendmentWork);
			}
			catch {
				RestoreAfterFailedReplace(ehrAmendment,ehrAmendmentWork.FileName);
				throw;
			}
			RemoveOldAmendmentFile(ehrAmendment.FileName,ehrAmendmentNew.FileName);
			return ehrAmendmentNew;
		}

		///<summary>After a replacement of the amendment's file that failed (S0-6): the amendment stays on its old file, which is still there because it
		///is removed only after the new one is stored. The bitmap overload of ImportAmdAttach stores the new name on the row before it writes the file,
		///so the row gets its old name and append date back; a new file that was partly written under the new name is removed. Never throws, so the
		///caller sees the replacement's own exception.</summary>
		private static void RestoreAfterFailedReplace(EhrAmendment ehrAmendmentOld,string fileNameNew) {
			ODException.SwallowAnyException(() => {
				EhrAmendment ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendmentOld.EhrAmendmentNum);
				if(ehrAmendmentDb==null
					|| (ehrAmendmentDb.FileName==ehrAmendmentOld.FileName && ehrAmendmentDb.DateTAppend==ehrAmendmentOld.DateTAppend))
				{
					return;//The row was not changed.
				}
				ehrAmendmentDb.FileName=ehrAmendmentOld.FileName;
				ehrAmendmentDb.DateTAppend=ehrAmendmentOld.DateTAppend;
				EhrAmendments.Update(ehrAmendmentDb);
			});
			if(string.IsNullOrEmpty(fileNameNew) || fileNameNew==ehrAmendmentOld.FileName) {
				return;//The same name as the old file (a replacement within the same second): that file is the amendment's and stays.
			}
			ODException.SwallowAnyException(() => ImageStore.CleanAmdAttach(fileNameNew));
		}

		///<summary>The new name has the date and time to the second, so a second replacement within the same second may reuse the old name. The file
		///under that name is then the new one and stays.</summary>
		private static void RemoveOldAmendmentFile(string fileNameOld,string fileNameNew) {
			if(string.IsNullOrEmpty(fileNameOld) || fileNameOld==fileNameNew) {
				return;
			}
			ImageStore.CleanAmdAttach(fileNameOld);
		}

		///<summary>Deletes the EOB's file and its row (ImageStore.DeleteEobAttach). A preauthorization EOB logs ImageDelete. Does nothing when another
		///workstation already deleted it.</summary>
		public static void DeleteEob(FileEntry fileEntry) {
			EobAttach eobAttach=EobAttaches.GetOne(fileEntry.EobAttachNum);
			if(eobAttach==null) {
				return;
			}
			ImageStore.DeleteEobAttach(eobAttach);
		}

		///<summary>Deletes the amendment's file; the row stays, with an empty file name (ImageStore.DeleteAmdAttach).</summary>
		public static void DeleteAmendmentFile(EhrAmendment ehrAmendment) {
			ImageStore.DeleteAmdAttach(ehrAmendment);
		}
		#endregion Methods - Adding, replacing and deleting

		#region Methods - Reading and exporting
		///<summary>The image of the entry (ImageStore.OpenImagesEob or OpenImagesAmd), or null when it is not an image. The bitmap is a copy, so the
		///stored file is not held open while it shows and can be deleted or replaced. localPathCloud is the file just imported, with cloud storage,
		///so it is not downloaded again. Throws an ApplicationException when the file is missing or cannot be read.</summary>
		public static Bitmap OpenImage(FileEntry fileEntry,string localPathCloud="") {
			Bitmap bitmap;
			if(fileEntry.IsEob) {
				bitmap=ImageStore.OpenImagesEob(GetEobAttach(fileEntry),localPathCloud??"")[0];
			}
			else {
				bitmap=ImageStore.OpenImagesAmd(GetEhrAmendment(fileEntry))[0];
			}
			if(bitmap==null) {
				return null;
			}
			Bitmap bitmapCopy=new Bitmap(bitmap);
			bitmap.Dispose();
			return bitmapCopy;
		}

		///<summary>A file on this computer with the entry's bytes, for the PDF viewer and for the double-click open: with an A to Z folder the stored
		///file itself; with cloud storage localPathCloud when that is the file just imported, otherwise a temp copy downloaded now; with images in
		///the database a temp copy written from RawBase64. isTempCopy is true for a copy the caller deletes when done (DeleteTempCopy). Throws an
		///ApplicationException when there is no file.</summary>
		public static string GetLocalPath(FileEntry fileEntry,out bool isTempCopy,string localPathCloud="") {
			isTempCopy=false;
			string folder;
			string fileName;
			string rawBase64;
			if(fileEntry.IsEob) {
				EobAttach eobAttach=GetEobAttach(fileEntry);
				fileName=eobAttach.FileName;
				rawBase64=eobAttach.RawBase64;
				folder=ImageStore.GetEobFolder();
			}
			else {
				EhrAmendment ehrAmendment=GetEhrAmendment(fileEntry);
				fileName=ehrAmendment.FileName;
				rawBase64=ehrAmendment.RawBase64;
				folder=ImageStore.GetAmdFolder();
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ) {
				string path=ODFileUtils.CombinePaths(folder,fileName);
				if(!File.Exists(path)) {
					throw new ApplicationException(Lans.g("ImageStore","File not found:")+" "+path);
				}
				return path;
			}
			byte[] arrayBytes;
			if(CloudStorage.IsCloudStorage) {
				if(!string.IsNullOrEmpty(localPathCloud) && File.Exists(localPathCloud)) {
					return localPathCloud;
				}
				arrayBytes=CloudStorage.Download(folder.Replace("\\","/"),fileName);
				if(arrayBytes==null || arrayBytes.Length==0) {
					throw new ApplicationException(Lans.g("ImageStore","File could not be opened:")+" "+fileName);
				}
			}
			else {//Images in the database
				arrayBytes=ImageStore.GetRawBytes(rawBase64,fileName);
			}
			string pathTemp=PrefC.GetRandomTempFile(Path.GetExtension(fileName));
			File.WriteAllBytes(pathTemp,arrayBytes);
			isTempCopy=true;
			return pathTemp;
		}

		///<summary>Deletes a temp copy from GetLocalPath. Never throws: a copy that is still open stays in the temp folder.</summary>
		public static void DeleteTempCopy(string path) {
			if(string.IsNullOrEmpty(path)) {
				return;
			}
			ODException.SwallowAnyException(() => {
				if(File.Exists(path)) {
					File.Delete(path);
				}
			});
		}

		///<summary>Saves a copy of the entry's file at saveToPath (ImageStore.ExportEobAttach or ExportAmdAttach): the stored file with an A to Z folder
		///or cloud storage, the stored bytes with images in the database.</summary>
		public static void Export(FileEntry fileEntry,string saveToPath) {
			if(fileEntry.IsEob) {
				ImageStore.ExportEobAttach(saveToPath,GetEobAttach(fileEntry));
				return;
			}
			ImageStore.ExportAmdAttach(saveToPath,GetEhrAmendment(fileEntry));
		}

		///<summary>The EOB's row as it is now. Throws an ApplicationException when another workstation deleted it.</summary>
		private static EobAttach GetEobAttach(FileEntry fileEntry) {
			EobAttach eobAttach=EobAttaches.GetOne(fileEntry.EobAttachNum);
			if(eobAttach==null) {
				throw new ApplicationException(Lans.g("ImageStore","File not found:")+" "+fileEntry.FileName);
			}
			return eobAttach;
		}

		///<summary>The amendment's row as it is now. Throws an ApplicationException when it has no file.</summary>
		private static EhrAmendment GetEhrAmendment(FileEntry fileEntry) {
			EhrAmendment ehrAmendment=EhrAmendments.GetOne(fileEntry.EhrAmendmentNum);
			if(ehrAmendment==null || string.IsNullOrEmpty(ehrAmendment.FileName)) {
				throw new ApplicationException(Lans.g("ImageStore","File not found:")+" "+fileEntry.FileName);
			}
			return ehrAmendment;
		}
		#endregion Methods - Reading and exporting

		#region Methods - Log entries
		///<summary>The text of the Printing log entry of a print (PrinterL makes it, once per print): "EOB printed" for an EOB, as the old window
		///logged it; "Patient image", the amendment's description (its file name when it has none) and "printed" for an amendment's file.</summary>
		public static string GetPrintAuditDescription(FileEntry fileEntry) {
			if(fileEntry.IsEob) {
				return "EOB printed";
			}
			if(string.IsNullOrEmpty(fileEntry.Description)) {
				return "Patient image "+fileEntry.FileName+" printed";
			}
			return "Patient image "+fileEntry.Description+" printed";
		}

		///<summary>The patient of the Printing log entry: none for an EOB, as the old window logged it; the amendment's patient for an amendment's
		///file (the old window had no patient and showed an error after printing).</summary>
		public static long GetPrintAuditPatNum(FileEntry fileEntry) {
			if(fileEntry.IsEob) {
				return 0;
			}
			return fileEntry.PatNum;
		}

		///<summary>The text of the Copy log entry: the file's own name (the old window read a document row with the EOB's key instead).</summary>
		public static string GetCopyLogText(FileEntry fileEntry) {
			return "Patient image "+fileEntry.FileName+" copied to clipboard";
		}

		///<summary>The Copy log entry of a copy to the clipboard, with no patient, as the old window made it.</summary>
		public static void LogCopied(FileEntry fileEntry) {
			SecurityLogs.MakeLogEntry(EnumPermType.Copy,0,GetCopyLogText(fileEntry));
		}
		#endregion Methods - Log entries
	}
}
