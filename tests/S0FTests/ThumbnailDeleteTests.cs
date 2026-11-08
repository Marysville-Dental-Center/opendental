// S0-F (2): deleting a document also deletes its thumbnail, Thumbnails\[file name] in the patient's folder, which Open Dental left behind (so a
// deleted image stayed on disk as its 100x100 copy). Thumbnails are files only with an A to Z folder; the other storage modes have none and
// nothing is done for them. A mount's thumbnail, Thumbnails\Mount[MountNum].jpg, goes with the mount. The file rules need no database; the delete
// itself runs on the scratch s0f_ database with a scratch A-to-Z folder in the temp folder. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace S0FTests {
	[TestClass]
	public class ThumbnailDeleteTests {
		private readonly List<string> _listFoldersToDelete=new List<string>();

		[TestCleanup]
		public void TestCleanup() {
			for(int i=0;i<_listFoldersToDelete.Count;i++) {
				try {
					Directory.Delete(_listFoldersToDelete[i],true);
				}
				catch(Exception) {
					//The temp folder is emptied later.
				}
			}
		}

		///<summary>A new, empty folder in the temp folder, deleted after the test.</summary>
		private string NewTempFolder(string purpose) {
			string folder=Path.Combine(Path.GetTempPath(),"s0f_"+purpose+"_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			_listFoldersToDelete.Add(folder);
			return folder;
		}

		///<summary>A patient folder with Thumbnails\fileName in it. Returns the thumbnail's path.</summary>
		private static string WriteThumbnail(string patFolder,string fileName) {
			string folderThumbnails=Path.Combine(patFolder,"Thumbnails");
			Directory.CreateDirectory(folderThumbnails);
			string path=Path.Combine(folderThumbnails,fileName);
			File.WriteAllBytes(path,new byte[] { 1,2,3 });
			return path;
		}

		private static void WriteJpg(string path) {
			using Bitmap bitmap=new Bitmap(40,30);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.SteelBlue);
				g.FillRectangle(Brushes.White,5,5,10,10);
			}
			bitmap.Save(path,ImageFormat.Jpeg);
		}

		#region File rules, no database
		[TestMethod]
		public void DeleteThumbnailFile_AtoZ_DeletesOnlyThatThumbnail() {
			string patFolder=NewTempFolder("rules");
			string pathThumbnail=WriteThumbnail(patFolder,"BitewingL123.jpg");
			string pathOther=WriteThumbnail(patFolder,"BitewingR124.jpg");
			string pathImage=Path.Combine(patFolder,"BitewingL123.jpg");
			File.WriteAllBytes(pathImage,new byte[] { 9 });
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,"BitewingL123.jpg",DataStorageType.LocalAtoZ));
			Assert.IsFalse(File.Exists(pathThumbnail),"The thumbnail is deleted.");
			Assert.IsTrue(File.Exists(pathOther),"Another document's thumbnail stays.");
			Assert.IsTrue(File.Exists(pathImage),"Only the thumbnail: the image itself is DeleteDocuments' to delete.");
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,"BitewingL123.jpg",DataStorageType.LocalAtoZ),"No thumbnail is no error.");
		}

		[TestMethod]
		public void DeleteThumbnailFile_OtherStorage_TouchesNoFile() {
			string patFolder=NewTempFolder("storage");
			string pathThumbnail=WriteThumbnail(patFolder,"Photo5.jpg");
			foreach(DataStorageType dataStorageType in new[] { DataStorageType.InDatabase,DataStorageType.SftpAtoZ,DataStorageType.DropboxAtoZ }) {
				Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,"Photo5.jpg",dataStorageType),dataStorageType.ToString());
				Assert.IsTrue(File.Exists(pathThumbnail),dataStorageType+": thumbnails are files only with an A to Z folder, so nothing is deleted.");
			}
		}

		[TestMethod]
		public void DeleteThumbnailFile_NoFolderOrNoPlainName_TouchesNoFile() {
			string folder=NewTempFolder("names");
			string patFolder=Path.Combine(folder,"Pat");
			Directory.CreateDirectory(patFolder);
			string pathOutside=WriteThumbnail(folder,"x.jpg");//folder\Thumbnails\x.jpg, outside the patient's folder
			Assert.IsTrue(ImageStore.DeleteThumbnailFile("","x.jpg",DataStorageType.LocalAtoZ));
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,"",DataStorageType.LocalAtoZ));
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,"   ",DataStorageType.LocalAtoZ));
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,@"..\..\Thumbnails\x.jpg",DataStorageType.LocalAtoZ));
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(Path.Combine(folder,"Missing"),"x.jpg",DataStorageType.LocalAtoZ));
			Assert.IsTrue(File.Exists(pathOutside),"Only a plain file name in an existing patient folder is used.");
			Assert.IsTrue(Directory.Exists(Path.Combine(patFolder)),"The patient folder stays.");
		}

		[TestMethod]
		public void DeleteThumbnailFile_Locked_StaysAndNeverThrows() {
			string patFolder=NewTempFolder("locked");
			string pathThumbnail=WriteThumbnail(patFolder,"Locked.jpg");
			using(FileStream fileStream=new FileStream(pathThumbnail,FileMode.Open,FileAccess.Read,FileShare.None)) {
				Assert.IsFalse(ImageStore.DeleteThumbnailFile(patFolder,"Locked.jpg",DataStorageType.LocalAtoZ),"A locked thumbnail is reported, not thrown.");
			}
			Assert.IsTrue(File.Exists(pathThumbnail));
			Assert.IsTrue(ImageStore.DeleteThumbnailFile(patFolder,"Locked.jpg",DataStorageType.LocalAtoZ),"Deleted once it is free.");
			Assert.IsFalse(File.Exists(pathThumbnail));
		}
		#endregion File rules, no database

		#region DeleteDocuments, scratch database and A-to-Z folder
		[TestMethod]
		public void DeleteDocuments_AtoZ_DeletesTheImageItsThumbnailAndTheRow() {
			S0FDb.Begin();
			string atoZ=NewTempFolder("atoz");
			try {
				S0FDb.SetStorage(DataStorageType.LocalAtoZ,atoZ);
				Patient patient=S0FDb.NewPatient();
				string pathImport=Path.Combine(NewTempFolder("import"),"photo.jpg");
				WriteJpg(pathImport);
				long defNumCategory=Defs.GetDefsForCategory(DefCat.ImageCats,true)[0].DefNum;
				Document document=ImageStore.Import(pathImport,defNumCategory,patient);
				Document documentOther=ImageStore.Import(pathImport,defNumCategory,patient);
				string patFolder=ImageStore.GetPatientFolder(patient,ImageStore.GetPreferredAtoZpath());
				string pathImage=Path.Combine(patFolder,document.FileName);
				//The thumbnails, made as the Imaging module's list makes them.
				Documents.GetThumbnail(document,patFolder)?.Dispose();
				Documents.GetThumbnail(documentOther,patFolder)?.Dispose();
				string pathThumbnail=Path.Combine(patFolder,"Thumbnails",document.FileName);
				string pathThumbnailOther=Path.Combine(patFolder,"Thumbnails",documentOther.FileName);
				Assert.IsTrue(StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(atoZ),Path.GetFullPath(Path.GetDirectoryName(Path.GetDirectoryName(patFolder)))),
					"The patient's folder is in the scratch A-to-Z folder: "+patFolder);
				Assert.IsTrue(File.Exists(pathImage),"Imported: "+pathImage);
				Assert.IsTrue(File.Exists(pathThumbnail),"GetThumbnail saved the thumbnail: "+pathThumbnail);
				Assert.IsTrue(File.Exists(pathThumbnailOther));
				ImageStore.DeleteDocuments(new List<Document> { document },patFolder);
				Assert.IsFalse(File.Exists(pathImage),"The image is deleted.");
				Assert.IsFalse(File.Exists(pathThumbnail),"Its thumbnail is deleted too.");
				Assert.IsNull(Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true),"The row is deleted.");
				Assert.IsTrue(File.Exists(pathThumbnailOther),"The other document's thumbnail stays.");
				Assert.IsTrue(File.Exists(Path.Combine(patFolder,documentOther.FileName)));
				//A mount's thumbnail goes with the mount.
				Mount mount=new Mount();
				mount.MountNum=900000+S0FDb.Next();
				string pathThumbnailMount=Path.Combine(patFolder,"Thumbnails","Mount"+mount.MountNum+".jpg");
				File.WriteAllBytes(pathThumbnailMount,new byte[] { 1 });
				ImageStore.DeleteThumbnailOfDeletedMount(mount,patFolder);
				Assert.IsFalse(File.Exists(pathThumbnailMount),"The mount's thumbnail is deleted.");
				ImageStore.DeleteDocuments(new List<Document> { documentOther },patFolder);
				Assert.IsFalse(File.Exists(pathThumbnailOther));
			}
			finally {
				S0FDb.SetStorage(DataStorageType.InDatabase);
			}
		}

		[TestMethod]
		public void DeleteDocuments_InDatabase_DeletesTheRowAndNoFile() {
			S0FDb.Begin();
			S0FDb.SetStorage(DataStorageType.InDatabase);
			Patient patient=S0FDb.NewPatient();
			Document document=new Document();
			document.PatNum=patient.PatNum;
			document.FileName=".jpg";
			document.Description="";
			document.Note="";
			document.DocCategory=Defs.GetDefsForCategory(DefCat.ImageCats,true)[0].DefNum;
			document.DateCreated=DateTime.Now;
			document.ImgType=ImageType.Photo;
			document.RawBase64="";
			document.ToothNumbers="";
			Documents.Insert(document,patient);
			document=Documents.GetByNum(document.DocNum);
			//A file where a thumbnail would be, were this an A to Z folder: images in the database have none, so it is never touched.
			string folder=NewTempFolder("indb");
			string pathFile=WriteThumbnail(folder,document.FileName);
			ImageStore.DeleteDocuments(new List<Document> { document },folder);
			Assert.IsNull(Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true),"The row is deleted.");
			Assert.IsTrue(File.Exists(pathFile),"No file is deleted when the images are in the database.");
		}
		#endregion DeleteDocuments, scratch database and A-to-Z folder
	}
}
