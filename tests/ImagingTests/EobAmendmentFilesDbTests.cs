// S0-6 of the .NET 10 move: the EOB and amendment window (FormImages), rebuilt on the current Imaging code, and its logic without UI
// (EobAmendmentFiles, at the end of OpenPmsDentalBusiness/Imaging/ImageStore.cs). The file handling runs against a scratch database of its own
// (TestDb.ScratchName("eobamd"), built like the A-IMG one, dropped afterwards, and dropped by a later run's TestDb.DropStaleScratchDatabases
// if a run crashes), once with an A-to-Z folder and once with images in the database:
// the same eobattach and ehramendment rows, files and log entries as the old window, except the differences S0-6 lists. The window cannot be
// opened in a test run (the GUI is not launched), so its source is read. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using EnumCommand=OpenPmsDentalBusiness.EobAmendmentFiles.EnumCommand;
using EnumMode=OpenPmsDentalBusiness.EobAmendmentFiles.EnumMode;
using EnumShowAs=OpenPmsDentalBusiness.EobAmendmentFiles.EnumShowAs;
using FileEntry=OpenPmsDentalBusiness.EobAmendmentFiles.FileEntry;

namespace ImagingTests {
	///<summary>The scratch database of the S0-6 tests, apart from the A-IMG one: named by TestDb.ScratchName("eobamd") (this run's id), built like
	///TestDb.CreateFresh24_3 (the seed dump and the frozen 24.3.63 convert chain), upgraded by the fork migration runner, and dropped when the
	///tests are done. A run that crashes leaves it behind for a later run's TestDb.DropStaleScratchDatabases, which ImagingDb runs first. Only this
	///database is ever created or dropped here.</summary>
	internal static class EobAmendmentDb {
		public static readonly string DbName=TestDb.ScratchName("eobamd");
		///<summary>Null when the database is ready; otherwise why the tests are skipped.</summary>
		public static string SkipReason;

		public static void Create() {
			SkipReason=ImagingDb.SkipReason;//No local MariaDB, or FORKMIGRATION_TEST_DB=skip.
			if(SkipReason!=null) {
				return;
			}
			TestDb.Recreate(DbName);
			TestDb.Connect(DbName);
			DataCore.NonQ(File.ReadAllText(FindSeedDump(),new UTF8Encoding(false)));
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			Cache.Refresh(InvalidType.Prefs);
			ForkMigrationRunner.Run();
		}

		public static void Drop() {
			TestDb.Drop(DbName);//refuses any name that is not a scratch name
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it and fills the caches from it.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			TestDb.Connect(DbName);
			ImagingDb.RefreshCaches();
		}

		///<summary>Call from [TestCleanup]: puts the business layer back on the A-IMG database, as the other test classes expect it.</summary>
		public static void End() {
			if(ImagingDb.SkipReason!=null) {
				return;
			}
			TestDb.Connect(ImagingDb.DbName);
			ImagingDb.RefreshCaches();
		}

		///<summary>S0-5 moves the seed dump from UnitTests\Resources to db\seed; either place works.</summary>
		private static string FindSeedDump() {
			string[] arrayPaths=new[] { Path.Combine("db","seed","dump.txt"),Path.Combine("UnitTests","Resources","dump.txt") };
			for(int i=0;i<arrayPaths.Length;i++) {
				try {
					return TestDb.FindRepoFile(arrayPaths[i]);
				}
				catch(FileNotFoundException) {
					//try the next place
				}
			}
			throw new FileNotFoundException("The seed dump (db\\seed\\dump.txt or UnitTests\\Resources\\dump.txt) was not found above the test folder.");
		}
	}

	[TestClass]
	public class EobAmendmentFilesDbTests {
		private static readonly byte[] _arrayBytesPdf=new byte[] { 0x25,0x50,0x44,0x46,0x2D,0x31,0x2E,0x34,0x0A,0x25,0x25,0x45,0x4F,0x46 };
		private readonly List<string> _listPathsToDelete=new List<string>();
		private string _atoZ;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			EobAmendmentDb.Create();
			EobAmendmentDb.End();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(EobAmendmentDb.SkipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				EobAmendmentDb.Drop();
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			EobAmendmentDb.Begin();
			ImagingDb.SetStorage(DataStorageType.InDatabase);
			Security.CurUser=new Userod { UserNum=1,UserName="s06test" };
		}

		[TestCleanup]
		public void TestCleanup() {
			if(EobAmendmentDb.SkipReason==null) {
				ImagingDb.SetStorage(DataStorageType.InDatabase);//also clears the A-to-Z override path
				EobAmendmentDb.End();
			}
			Security.CurUser=new Userod();
			foreach(string path in _listPathsToDelete) {
				try {
					if(Directory.Exists(path)) {
						Directory.Delete(path,recursive:true);
					}
					else if(File.Exists(path)) {
						File.Delete(path);
					}
				}
				catch(Exception) {
					//Temp files only.
				}
			}
		}

		#region Helpers
		///<summary>Uses a new, empty temp folder as this computer's A-to-Z folder.</summary>
		private void UseAtoZ() {
			_atoZ=Path.Combine(Path.GetTempPath(),"s06_atoz_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_atoZ);
			_listPathsToDelete.Add(_atoZ);
			ImagingDb.SetStorage(DataStorageType.LocalAtoZ,_atoZ);
		}

		private string MakeTempPath(string extension) {
			string path=Path.Combine(Path.GetTempPath(),"s06_"+Guid.NewGuid().ToString("N")+extension);
			_listPathsToDelete.Add(path);
			return path;
		}

		///<summary>A small made-up PDF file standing in for an EOB.</summary>
		private string MakePdfFile() {
			string path=MakeTempPath(".pdf");
			File.WriteAllBytes(path,_arrayBytesPdf);
			return path;
		}

		///<summary>A small made-up image standing in for a scanned or pasted page.</summary>
		private static Bitmap MakeBitmap() {
			Bitmap bitmap=new Bitmap(40,30);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.White);
				g.FillRectangle(Brushes.Black,5,5,10,10);
			}
			return bitmap;
		}

		private string MakeJpgFile() {
			string path=MakeTempPath(".jpg");
			using(Bitmap bitmap=MakeBitmap()) {
				bitmap.Save(path,ImageFormat.Jpeg);
			}
			return path;
		}

		private static bool IsJpeg(byte[] arrayBytes) {
			return arrayBytes.Length>2 && arrayBytes[0]==0xFF && arrayBytes[1]==0xD8;
		}

		private static void InsertRawEob(long claimPaymentNum,long claimNumPreAuth,DateTime dateTCreated,string fileName,string rawBase64="") {
			DataCore.NonQ("INSERT INTO eobattach (ClaimPaymentNum,DateTCreated,FileName,RawBase64,ClaimNumPreAuth) VALUES ("
				+POut.Long(claimPaymentNum)+","+POut.DateT(dateTCreated)+",'"+POut.String(fileName)+"','"+POut.String(rawBase64)+"',"
				+POut.Long(claimNumPreAuth)+")");
		}

		private static EhrAmendment CreateAmendment(Patient patient,string description) {
			EhrAmendment ehrAmendment=new EhrAmendment();
			ehrAmendment.PatNum=patient.PatNum;
			ehrAmendment.Description=description;
			ehrAmendment.SourceName="";
			ehrAmendment.FileName="";
			ehrAmendment.RawBase64="";
			ehrAmendment.DateTRequest=DateTime.Today;
			EhrAmendments.Insert(ehrAmendment);
			return EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum);
		}

		private static int CountLogs(EnumPermType perm,long patNum) {
			return ImagingDb.GetLogs(perm,patNum).Count;
		}

		private string EobPath(string fileName) {
			return Path.Combine(_atoZ,"EOBs",fileName);
		}

		private string AmdPath(string fileName) {
			return Path.Combine(_atoZ,"Amendments",fileName);
		}

		private static bool SamePath(string path1,string path2) {
			return string.Equals(Path.GetFullPath(path1),Path.GetFullPath(path2),StringComparison.OrdinalIgnoreCase);
		}
		#endregion Helpers

		#region The list
		[TestMethod]
		public void List_TheClaimPaymentsAndThePreAuthsOwnEobs_OldestFirst() {
			Patient patient=ImagingDb.CreatePatient("s06list");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-10));
			long claimPaymentNum=700000+patient.PatNum;//Any number: claim payment EOBs are matched by number only.
			InsertRawEob(claimPaymentNum,0,DateTime.Now.AddDays(-1),"s06list_pay_newer.pdf");
			InsertRawEob(claimPaymentNum,0,DateTime.Now.AddDays(-3),"s06list_pay_older.jpg");
			InsertRawEob(0,claimNumPreAuth,DateTime.Now.AddDays(-2),"s06list_pre_newer.pdf");
			InsertRawEob(0,claimNumPreAuth,DateTime.Now.AddDays(-4),"s06list_pre_older.pdf");
			List<FileEntry> listPayment=EobAmendmentFiles.GetFileEntries(EnumMode.ClaimPayment,claimPaymentNum,claimNumPreAuth,null);
			CollectionAssert.AreEqual(new[] { "s06list_pay_older.jpg","s06list_pay_newer.pdf" },listPayment.Select(x => x.FileName).ToArray());
			List<FileEntry> listPreAuth=EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,claimPaymentNum,claimNumPreAuth,null);
			CollectionAssert.AreEqual(new[] { "s06list_pre_older.pdf","s06list_pre_newer.pdf" },listPreAuth.Select(x => x.FileName).ToArray());
			Assert.IsTrue(listPayment.Concat(listPreAuth).All(x => x.IsEob && x.EhrAmendmentNum==0 && x.PatNum==0));
			CollectionAssert.AreEqual(EobAttaches.RefreshForPreAuth(claimNumPreAuth).Select(x => x.EobAttachNum).ToList(),
				listPreAuth.Select(x => x.EobAttachNum).ToList());
			Assert.AreEqual(0,EobAmendmentFiles.GetFileEntries(EnumMode.None,claimPaymentNum,claimNumPreAuth,null).Count);
		}

		[TestMethod]
		public void List_TheAmendmentsOneFile_NoneWhileItHasNone() {
			Patient patient=ImagingDb.CreatePatient("s06amdlist");
			EhrAmendment ehrAmendment=CreateAmendment(patient,"S06 list amendment");
			Assert.AreEqual(0,EobAmendmentFiles.GetFileEntries(EnumMode.Amendment,0,0,ehrAmendment).Count);
			ehrAmendment.FileName="20200101_000000_"+ehrAmendment.EhrAmendmentNum+".pdf";
			FileEntry fileEntry=EobAmendmentFiles.GetFileEntries(EnumMode.Amendment,0,0,ehrAmendment).Single();
			Assert.IsFalse(fileEntry.IsEob);
			Assert.AreEqual(ehrAmendment.EhrAmendmentNum,fileEntry.EhrAmendmentNum);
			Assert.AreEqual(ehrAmendment.FileName,fileEntry.FileName);
			Assert.AreEqual("S06 list amendment",fileEntry.Description);
			Assert.AreEqual(patient.PatNum,fileEntry.PatNum);
		}
		#endregion The list

		#region Adding EOBs
		[TestMethod]
		public void AddEob_AtoZ_FromFileAndBitmap_FileInEobsFolder_BothOwnerKeys_PreAuthLogged() {
			UseAtoZ();
			Patient patient=ImagingDb.CreatePatient("s06addatoz");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			long claimPaymentNum=710000+patient.PatNum;
			int countCreateLogs=CountLogs(EnumPermType.ImageCreate,0);
			EobAttach eobPayFile=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.ClaimPayment,claimPaymentNum,claimNumPreAuth);
			EobAttach eobPayBitmap;
			using(Bitmap bitmap=MakeBitmap()) {
				eobPayBitmap=EobAmendmentFiles.AddEob(bitmap,EnumMode.ClaimPayment,claimPaymentNum,claimNumPreAuth);
			}
			Assert.AreEqual(countCreateLogs,CountLogs(EnumPermType.ImageCreate,0),"A claim payment EOB logs nothing, as before.");
			EobAttach eobPreFile=EobAmendmentFiles.AddEob(MakeJpgFile(),EnumMode.PreAuth,claimPaymentNum,claimNumPreAuth);
			EobAttach eobPreBitmap;
			using(Bitmap bitmap=MakeBitmap()) {
				eobPreBitmap=EobAmendmentFiles.AddEob(bitmap,EnumMode.PreAuth,claimPaymentNum,claimNumPreAuth);
			}
			foreach(EobAttach eobAttach in new[] { eobPayFile,eobPayBitmap }) {
				EobAttach eobAttachDb=EobAttaches.GetOne(eobAttach.EobAttachNum);
				Assert.AreEqual(claimPaymentNum,eobAttachDb.ClaimPaymentNum);
				Assert.AreEqual(0,eobAttachDb.ClaimNumPreAuth);
			}
			foreach(EobAttach eobAttach in new[] { eobPreFile,eobPreBitmap }) {
				EobAttach eobAttachDb=EobAttaches.GetOne(eobAttach.EobAttachNum);
				Assert.AreEqual(0,eobAttachDb.ClaimPaymentNum);
				Assert.AreEqual(claimNumPreAuth,eobAttachDb.ClaimNumPreAuth);
			}
			foreach(EobAttach eobAttach in new[] { eobPayFile,eobPayBitmap,eobPreFile,eobPreBitmap }) {
				EobAttach eobAttachDb=EobAttaches.GetOne(eobAttach.EobAttachNum);
				Assert.IsTrue(File.Exists(EobPath(eobAttachDb.FileName)),"In the EOBs folder: "+eobAttachDb.FileName);
				Assert.AreEqual("",eobAttachDb.RawBase64??"","A-to-Z: nothing in RawBase64.");
				StringAssert.Contains(eobAttachDb.FileName,"_"+eobAttachDb.EobAttachNum+".","Named as today, with the key.");
			}
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(EobPath(EobAttaches.GetOne(eobPayFile.EobAttachNum).FileName)),"A copy of the file.");
			Assert.IsTrue(eobPayBitmap.FileName.EndsWith(".jpg") && eobPreBitmap.FileName.EndsWith(".jpg"),"A scanned or pasted page is a JPG.");
			Assert.IsTrue(IsJpeg(File.ReadAllBytes(EobPath(eobPreBitmap.FileName))));
			List<SecurityLog> listCreateLogs=ImagingDb.GetLogs(EnumPermType.ImageCreate,patient.PatNum);
			Assert.AreEqual(2,listCreateLogs.Count,"One ImageCreate entry for each preauth EOB.");
			Assert.IsTrue(listCreateLogs.Any(x => x.LogText=="Preauth EOB attached: "+eobPreFile.FileName));
			Assert.IsTrue(listCreateLogs.Any(x => x.LogText=="Preauth EOB attached: "+eobPreBitmap.FileName));
			//The new EOBs are in the list, oldest first.
			Assert.AreEqual(2,EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,0,claimNumPreAuth,null).Count);
			Assert.AreEqual(2,EobAmendmentFiles.GetFileEntries(EnumMode.ClaimPayment,claimPaymentNum,0,null).Count);
		}

		[TestMethod]
		public void AddEob_InDatabase_FromFileAndBitmap_BytesInRawBase64() {
			Patient patient=ImagingDb.CreatePatient("s06adddb");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			long claimPaymentNum=720000+patient.PatNum;
			EobAttach eobPayFile=EobAttaches.GetOne(EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.ClaimPayment,claimPaymentNum,0).EobAttachNum);
			EobAttach eobPreBitmap;
			using(Bitmap bitmap=MakeBitmap()) {
				eobPreBitmap=EobAttaches.GetOne(EobAmendmentFiles.AddEob(bitmap,EnumMode.PreAuth,0,claimNumPreAuth).EobAttachNum);
			}
			Assert.AreEqual(claimPaymentNum,eobPayFile.ClaimPaymentNum);
			Assert.AreEqual(0,eobPayFile.ClaimNumPreAuth);
			Assert.AreEqual(0,eobPreBitmap.ClaimPaymentNum);
			Assert.AreEqual(claimNumPreAuth,eobPreBitmap.ClaimNumPreAuth);
			CollectionAssert.AreEqual(_arrayBytesPdf,Convert.FromBase64String(eobPayFile.RawBase64),"The file's bytes, in the row.");
			Assert.IsTrue(eobPreBitmap.FileName.EndsWith(".jpg"));
			Assert.IsTrue(IsJpeg(Convert.FromBase64String(eobPreBitmap.RawBase64)));
			Assert.AreEqual("Preauth EOB attached: "+eobPreBitmap.FileName,ImagingDb.GetLogs(EnumPermType.ImageCreate,patient.PatNum).Single().LogText);
		}

		[TestMethod]
		public void AddEob_OnlyTheEobModes() {
			int countRows=PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM eobattach"));
			string path=MakePdfFile();
			foreach(EnumMode mode in new[] { EnumMode.None,EnumMode.Amendment }) {
				try {
					EobAmendmentFiles.AddEob(path,mode,5,7);
					Assert.Fail("No EOB for mode "+mode);
				}
				catch(ArgumentException) {
					//An EOB belongs to exactly one claim payment or one preauthorization.
				}
			}
			Assert.AreEqual(countRows,PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM eobattach")));
		}
		#endregion Adding EOBs

		#region Deleting EOBs
		[TestMethod]
		public void DeleteEob_AtoZ_FileAndRowGone_PreAuthLogsImageDelete_EvenWhileItShows() {
			UseAtoZ();
			Patient patient=ImagingDb.CreatePatient("s06delatoz");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			long claimPaymentNum=730000+patient.PatNum;
			EobAttach eobPre=EobAmendmentFiles.AddEob(MakeJpgFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			EobAttach eobPay=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.ClaimPayment,claimPaymentNum,0);
			FileEntry fileEntryPre=EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,0,claimNumPreAuth,null).Single();
			FileEntry fileEntryPay=EobAmendmentFiles.GetFileEntries(EnumMode.ClaimPayment,claimPaymentNum,0,null).Single();
			int countDeleteLogs=CountLogs(EnumPermType.ImageDelete,0);
			using(Bitmap bitmapShowing=EobAmendmentFiles.OpenImage(fileEntryPre)) {
				Assert.IsNotNull(bitmapShowing);
				//The window shows a copy, so the stored file is not held open and the delete removes it.
				EobAmendmentFiles.DeleteEob(fileEntryPre);
				Assert.IsFalse(File.Exists(EobPath(eobPre.FileName)),"The file is gone while the image still shows.");
			}
			Assert.IsNull(EobAttaches.GetOne(eobPre.EobAttachNum),"The row is gone.");
			Assert.AreEqual("Preauth EOB deleted: "+eobPre.FileName,ImagingDb.GetLogs(EnumPermType.ImageDelete,patient.PatNum).Single().LogText);
			EobAmendmentFiles.DeleteEob(fileEntryPay);
			Assert.IsFalse(File.Exists(EobPath(eobPay.FileName)));
			Assert.IsNull(EobAttaches.GetOne(eobPay.EobAttachNum));
			Assert.AreEqual(countDeleteLogs+1,CountLogs(EnumPermType.ImageDelete,0),"A claim payment EOB logs nothing, as before.");
			EobAmendmentFiles.DeleteEob(fileEntryPay);//Already deleted, by another workstation say: nothing happens.
			Assert.AreEqual(0,EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,0,claimNumPreAuth,null).Count);
		}

		[TestMethod]
		public void DeleteEob_InDatabase_RowGone_PreAuthLogsImageDelete() {
			Patient patient=ImagingDb.CreatePatient("s06deldb");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			EobAttach eobPre=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			EobAmendmentFiles.DeleteEob(EobAmendmentFiles.GetFileEntry(eobPre));
			Assert.IsNull(EobAttaches.GetOne(eobPre.EobAttachNum));
			Assert.AreEqual("Preauth EOB deleted: "+eobPre.FileName,ImagingDb.GetLogs(EnumPermType.ImageDelete,patient.PatNum).Single().LogText);
		}
		#endregion Deleting EOBs

		#region Amendments
		[TestMethod]
		public void Amendment_AtoZ_ReplaceGivesANewNameAndRemovesTheOldFile_DeleteKeepsTheRow() {
			UseAtoZ();
			Patient patient=ImagingDb.CreatePatient("s06amdatoz");
			EhrAmendment ehrAmendment=CreateAmendment(patient,"S06 amendment");
			long ehrAmendmentNum=ehrAmendment.EhrAmendmentNum;
			Assert.IsFalse(EobAmendmentFiles.IsReplaceConfirmNeeded(EnumMode.Amendment,ehrAmendment),"No file yet: nothing to confirm.");
			ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,MakePdfFile());
			string fileName1=ehrAmendment.FileName;
			Assert.IsTrue(fileName1.EndsWith("_"+ehrAmendmentNum+".pdf"),fileName1);
			Assert.AreEqual(fileName1,EhrAmendments.GetOne(ehrAmendmentNum).FileName);
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(AmdPath(fileName1)),"In the Amendments folder.");
			Assert.IsTrue(EobAmendmentFiles.IsReplaceConfirmNeeded(EnumMode.Amendment,ehrAmendment),"Scan, import and paste ask first now.");
			//Replace by a scanned or pasted page:
			using(Bitmap bitmap=MakeBitmap()) {
				ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,bitmap);
			}
			string fileName2=ehrAmendment.FileName;
			Assert.AreNotEqual(fileName1,fileName2);
			Assert.IsTrue(fileName2.EndsWith("_"+ehrAmendmentNum+".jpg"),fileName2);
			Assert.AreEqual(fileName2,EhrAmendments.GetOne(ehrAmendmentNum).FileName);
			Assert.IsTrue(IsJpeg(File.ReadAllBytes(AmdPath(fileName2))));
			Assert.IsFalse(File.Exists(AmdPath(fileName1)),"The old file is gone.");
			Assert.AreEqual(fileName2,EobAmendmentFiles.GetFileEntries(EnumMode.Amendment,0,0,ehrAmendment).Single().FileName);
			//Delete: the file goes, the row stays with an empty file name.
			EobAmendmentFiles.DeleteAmendmentFile(ehrAmendment);
			EhrAmendment ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendmentNum);
			Assert.IsNotNull(ehrAmendmentDb,"The row stays.");
			Assert.AreEqual("",ehrAmendmentDb.FileName);
			Assert.AreEqual("S06 amendment",ehrAmendmentDb.Description);
			Assert.IsFalse(File.Exists(AmdPath(fileName2)));
			Assert.AreEqual(0,EobAmendmentFiles.GetFileEntries(EnumMode.Amendment,0,0,ehrAmendmentDb).Count);
			Assert.AreEqual(0,CountLogs(EnumPermType.ImageCreate,patient.PatNum)+CountLogs(EnumPermType.ImageDelete,patient.PatNum),
				"An amendment's file logs nothing, as before.");
		}

		[TestMethod]
		public void Amendment_InDatabase_ReplaceAndDelete_BytesInRawBase64() {
			Patient patient=ImagingDb.CreatePatient("s06amddb");
			EhrAmendment ehrAmendment=CreateAmendment(patient,"");
			long ehrAmendmentNum=ehrAmendment.EhrAmendmentNum;
			using(Bitmap bitmap=MakeBitmap()) {
				ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,bitmap);
			}
			EhrAmendment ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendmentNum);
			Assert.IsTrue(ehrAmendmentDb.FileName.EndsWith(".jpg"));
			Assert.IsTrue(IsJpeg(Convert.FromBase64String(ehrAmendmentDb.RawBase64)));
			ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,MakePdfFile());
			ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendmentNum);
			Assert.IsTrue(ehrAmendmentDb.FileName.EndsWith("_"+ehrAmendmentNum+".pdf"),ehrAmendmentDb.FileName);
			CollectionAssert.AreEqual(_arrayBytesPdf,Convert.FromBase64String(ehrAmendmentDb.RawBase64),"The new file replaces the old bytes.");
			EobAmendmentFiles.DeleteAmendmentFile(ehrAmendment);
			ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendmentNum);
			Assert.IsNotNull(ehrAmendmentDb);
			Assert.AreEqual("",ehrAmendmentDb.FileName);
			Assert.AreEqual("",ehrAmendmentDb.RawBase64??"");
		}

		///<summary>S0-6 part 2, fix 2: a file imported onto an amendment sets ehramendment.DateTAppend with images in the database too, as it does with
		///an A-to-Z folder and as a scanned or pasted page does in both modes.</summary>
		[TestMethod]
		public void Amendment_ReplaceFromAFile_SetsDateTAppend_InBothModes() {
			Patient patient=ImagingDb.CreatePatient("s06bappend");
			foreach(bool isAtoZ in new[] { false,true }) {
				if(isAtoZ) {
					UseAtoZ();
				}
				string mode=(isAtoZ ? "A-to-Z" : "in database");
				EhrAmendment ehrAmendment=CreateAmendment(patient,"S06b append "+mode);
				Assert.IsTrue(EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum).DateTAppend.Year<1880,"No file yet: "+mode);
				DateTime dateTimeBefore=DateTime.Now.AddSeconds(-2);
				ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,MakePdfFile());
				EhrAmendment ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum);
				Assert.IsTrue(ehrAmendmentDb.DateTAppend>=dateTimeBefore && ehrAmendmentDb.DateTAppend<=DateTime.Now.AddSeconds(2),
					"Appended now ("+mode+"): "+ehrAmendmentDb.DateTAppend);
				Assert.IsTrue(ehrAmendmentDb.FileName.EndsWith(".pdf"),mode);
				//The bitmap overload, which always set it:
				EhrAmendment ehrAmendmentBitmap=CreateAmendment(patient,"S06b append bitmap "+mode);
				using(Bitmap bitmap=MakeBitmap()) {
					EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendmentBitmap,bitmap);
				}
				Assert.IsTrue(EhrAmendments.GetOne(ehrAmendmentBitmap.EhrAmendmentNum).DateTAppend>=dateTimeBefore,mode);
			}
			//ImageStore itself, which the window calls through EobAmendmentFiles:
			ImagingDb.SetStorage(DataStorageType.InDatabase);
			EhrAmendment ehrAmendmentStore=CreateAmendment(patient,"S06b append store");
			ImageStore.ImportAmdAttach(MakePdfFile(),ehrAmendmentStore);
			Assert.IsTrue(EhrAmendments.GetOne(ehrAmendmentStore.EhrAmendmentNum).DateTAppend.Year>1880);
		}

		///<summary>S0-6 part 2, fix 4: a replacement that fails leaves the amendment on its old file. The object passed in keeps its name (the window's
		///list, export and a later delete use it), the row keeps its name and append date (the bitmap path stores the new name before the file), and
		///the old file is still there. A file that fails from disk (gone before it is copied) and a page that cannot be saved (a disposed bitmap).
		///</summary>
		[TestMethod]
		public void Amendment_FailedReplace_KeepsTheOldFileAndName_AtoZ() {
			UseAtoZ();
			Patient patient=ImagingDb.CreatePatient("s06bfailatoz");
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06b fail"),MakeJpgFile());
			string fileNameOld=ehrAmendment.FileName;
			DateTime dateTAppendOld=EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum).DateTAppend;
			ehrAmendment=EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum);
			string pathMissing=Path.Combine(_atoZ,"s06b_missing_"+Guid.NewGuid().ToString("N")+".pdf");
			AssertThrowsAny(() => EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,pathMissing));
			AssertStillOnTheOldFile(ehrAmendment,fileNameOld,dateTAppendOld);
			Bitmap bitmapDisposed=MakeBitmap();
			bitmapDisposed.Dispose();
			System.Threading.Thread.Sleep(1100);//A new name, a second later than the old one, which the bitmap path stores before the file.
			AssertThrowsAny(() => EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,bitmapDisposed));
			AssertStillOnTheOldFile(ehrAmendment,fileNameOld,dateTAppendOld);
			CollectionAssert.AreEqual(new[] { fileNameOld },Directory.GetFiles(Path.Combine(_atoZ,"Amendments")).Select(x => Path.GetFileName(x)).ToArray(),
				"Only the old file: nothing was left under the new name.");
			Assert.IsTrue(IsJpeg(File.ReadAllBytes(AmdPath(fileNameOld))));
			//And it can still be replaced and deleted under its real name.
			ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,MakePdfFile());
			Assert.IsFalse(File.Exists(AmdPath(fileNameOld)),"The old file goes once a replacement works.");
			EobAmendmentFiles.DeleteAmendmentFile(ehrAmendment);
			Assert.AreEqual(0,Directory.GetFiles(Path.Combine(_atoZ,"Amendments")).Length);
		}

		[TestMethod]
		public void Amendment_FailedReplace_KeepsTheOldFileAndName_InDatabase() {
			Patient patient=ImagingDb.CreatePatient("s06bfaildb");
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06b fail db"),MakePdfFile());
			string fileNameOld=ehrAmendment.FileName;
			ehrAmendment=EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum);
			DateTime dateTAppendOld=ehrAmendment.DateTAppend;
			string pathMissing=Path.Combine(Path.GetTempPath(),"s06b_missing_"+Guid.NewGuid().ToString("N")+".pdf");
			AssertThrowsAny(() => EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,pathMissing));
			AssertStillOnTheOldFile(ehrAmendment,fileNameOld,dateTAppendOld);
			Bitmap bitmapDisposed=MakeBitmap();
			bitmapDisposed.Dispose();
			System.Threading.Thread.Sleep(1100);
			AssertThrowsAny(() => EobAmendmentFiles.ReplaceAmendmentFile(ehrAmendment,bitmapDisposed));
			AssertStillOnTheOldFile(ehrAmendment,fileNameOld,dateTAppendOld);
			CollectionAssert.AreEqual(_arrayBytesPdf,Convert.FromBase64String(EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum).RawBase64),
				"The stored bytes are the old file's.");
		}

		private static void AssertStillOnTheOldFile(EhrAmendment ehrAmendment,string fileNameOld,DateTime dateTAppendOld) {
			Assert.AreEqual(fileNameOld,ehrAmendment.FileName,"The object passed in is not changed.");
			EhrAmendment ehrAmendmentDb=EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum);
			Assert.AreEqual(fileNameOld,ehrAmendmentDb.FileName,"The row keeps its name.");
			Assert.AreEqual(dateTAppendOld,ehrAmendmentDb.DateTAppend,"The row keeps its append date.");
			Assert.AreEqual(fileNameOld,EobAmendmentFiles.GetFileEntries(EnumMode.Amendment,0,0,ehrAmendment).Single().FileName);
		}
		#endregion Amendments

		#region Reading, the viewer's path and export
		[TestMethod]
		public void LocalPath_AtoZ_TheStoredFileItself() {
			UseAtoZ();
			Patient patient=ImagingDb.CreatePatient("s06pathatoz");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			EobAttach eobAttach=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			string path=EobAmendmentFiles.GetLocalPath(EobAmendmentFiles.GetFileEntry(eobAttach),out bool isTempCopy);
			Assert.IsTrue(SamePath(EobPath(eobAttach.FileName),path),path);
			Assert.IsFalse(isTempCopy,"Never deleted by the window.");
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06 path"),MakePdfFile());
			path=EobAmendmentFiles.GetLocalPath(EobAmendmentFiles.GetFileEntry(ehrAmendment),out isTempCopy);
			Assert.IsTrue(SamePath(AmdPath(ehrAmendment.FileName),path),path);
			Assert.IsFalse(isTempCopy);
			//A row whose file is missing from the folder:
			InsertRawEob(0,claimNumPreAuth,DateTime.Now,"s06path_missing.pdf");
			FileEntry fileEntryMissing=EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,0,claimNumPreAuth,null).Single(x => x.FileName=="s06path_missing.pdf");
			AssertThrowsApplication(() => EobAmendmentFiles.GetLocalPath(fileEntryMissing,out _));
		}

		[TestMethod]
		public void LocalPath_InDatabase_ATempCopyOfTheStoredBytes() {
			Patient patient=ImagingDb.CreatePatient("s06pathdb");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			EobAttach eobAttach=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			string path=EobAmendmentFiles.GetLocalPath(EobAmendmentFiles.GetFileEntry(eobAttach),out bool isTempCopy);
			_listPathsToDelete.Add(path);
			Assert.IsTrue(isTempCopy);
			Assert.IsTrue(SamePath(PrefC.GetTempFolderPath(),Path.GetDirectoryName(path)),"In the program's temp folder: "+path);
			Assert.AreEqual(".pdf",Path.GetExtension(path),"The viewer needs the extension.");
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(path),"S0-6: a PDF EOB shows with images in the database.");
			EobAmendmentFiles.DeleteTempCopy(path);
			Assert.IsFalse(File.Exists(path));
			EobAmendmentFiles.DeleteTempCopy(path);//Already gone: never throws.
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06 path db"),MakePdfFile());
			path=EobAmendmentFiles.GetLocalPath(EobAmendmentFiles.GetFileEntry(ehrAmendment),out isTempCopy);
			_listPathsToDelete.Add(path);
			Assert.IsTrue(isTempCopy);
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(path),"S0-6: a PDF amendment shows with images in the database.");
			//A row as Open Dental writes it without a file (RawBase64 empty):
			InsertRawEob(0,claimNumPreAuth,DateTime.Now,"s06pathdb_empty.pdf");
			FileEntry fileEntryEmpty=EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,0,claimNumPreAuth,null).Single(x => x.FileName=="s06pathdb_empty.pdf");
			AssertThrowsApplication(() => EobAmendmentFiles.GetLocalPath(fileEntryEmpty,out _));
		}

		[TestMethod]
		public void OpenImage_BothModes_ACopyThatHoldsNoLock_NullForAPdf() {
			Patient patient=ImagingDb.CreatePatient("s06open");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			EobAttach eobJpgDb=EobAmendmentFiles.AddEob(MakeJpgFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			EobAttach eobPdfDb=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			using(Bitmap bitmap=EobAmendmentFiles.OpenImage(EobAmendmentFiles.GetFileEntry(eobJpgDb))) {
				Assert.AreEqual(new Size(40,30),bitmap.Size);
			}
			Assert.IsNull(EobAmendmentFiles.OpenImage(EobAmendmentFiles.GetFileEntry(eobPdfDb)),"A PDF shows in the viewer instead.");
			UseAtoZ();
			EobAttach eobJpgAtoZ=EobAmendmentFiles.AddEob(MakeJpgFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06 open"),MakeJpgFile());
			using(Bitmap bitmapEob=EobAmendmentFiles.OpenImage(EobAmendmentFiles.GetFileEntry(eobJpgAtoZ)))
			using(Bitmap bitmapAmd=EobAmendmentFiles.OpenImage(EobAmendmentFiles.GetFileEntry(ehrAmendment))) {
				Assert.AreEqual(new Size(40,30),bitmapEob.Size);
				Assert.AreEqual(new Size(40,30),bitmapAmd.Size);
				using(FileStream fileStream=new FileStream(EobPath(eobJpgAtoZ.FileName),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
					//Opens: the shown image does not hold the stored file.
				}
				using(FileStream fileStream=new FileStream(AmdPath(ehrAmendment.FileName),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
				}
			}
		}

		[TestMethod]
		public void Export_AtoZ_ACopyOfTheStoredFile() {
			UseAtoZ();
			Patient patient=ImagingDb.CreatePatient("s06expatoz");
			long claimPaymentNum=740000+patient.PatNum;
			EobAttach eobAttach=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.ClaimPayment,claimPaymentNum,0);
			string pathExport=MakeTempPath(".pdf");
			EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(eobAttach),pathExport);
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(pathExport));
			Assert.IsTrue(File.Exists(EobPath(eobAttach.FileName)),"The stored file stays.");
			string pathJpg=MakeJpgFile();
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06 export"),pathJpg);
			string pathExportAmd=MakeTempPath(".jpg");
			EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(ehrAmendment),pathExportAmd);
			CollectionAssert.AreEqual(File.ReadAllBytes(pathJpg),File.ReadAllBytes(pathExportAmd));
		}

		[TestMethod]
		public void Export_InDatabase_WritesTheStoredBytes() {
			Patient patient=ImagingDb.CreatePatient("s06expdb");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			EobAttach eobPdf=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			string pathExport=MakeTempPath(".pdf");
			EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(eobPdf),pathExport);
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(pathExport),"S0-6: a PDF exports too (this failed before).");
			EobAttach eobJpg;
			using(Bitmap bitmap=MakeBitmap()) {
				eobJpg=EobAttaches.GetOne(EobAmendmentFiles.AddEob(bitmap,EnumMode.PreAuth,0,claimNumPreAuth).EobAttachNum);
			}
			string pathExportJpg=MakeTempPath(".jpg");
			EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(eobJpg),pathExportJpg);
			CollectionAssert.AreEqual(Convert.FromBase64String(eobJpg.RawBase64),File.ReadAllBytes(pathExportJpg),"The stored bytes as they are.");
			EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06 export db"),MakePdfFile());
			string pathExportAmd=MakeTempPath(".pdf");
			EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(ehrAmendment),pathExportAmd);
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(pathExportAmd));
			//The ImageStore methods themselves, which the window no longer calls any other way:
			string pathExportStore=MakeTempPath(".pdf");
			ImageStore.ExportEobAttach(pathExportStore,EobAttaches.GetOne(eobPdf.EobAttachNum));
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(pathExportStore));
			string pathExportStoreAmd=MakeTempPath(".pdf");
			ImageStore.ExportAmdAttach(pathExportStoreAmd,EhrAmendments.GetOne(ehrAmendment.EhrAmendmentNum));
			CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(pathExportStoreAmd));
			//No bytes stored: nothing is written.
			InsertRawEob(0,claimNumPreAuth,DateTime.Now,"s06expdb_empty.pdf");
			FileEntry fileEntryEmpty=EobAmendmentFiles.GetFileEntries(EnumMode.PreAuth,0,claimNumPreAuth,null).Single(x => x.FileName=="s06expdb_empty.pdf");
			string pathExportEmpty=MakeTempPath(".pdf");
			AssertThrowsApplication(() => EobAmendmentFiles.Export(fileEntryEmpty,pathExportEmpty));
			Assert.IsFalse(File.Exists(pathExportEmpty));
		}

		///<summary>S0-6 part 2, fix 1: export over an existing file replaces it in both storage modes, after the Save dialog has asked (with an A-to-Z
		///folder ImageStore's copy failed with "file exists"; with images in the database it already replaced it).</summary>
		[TestMethod]
		public void Export_OverAnExistingFile_ReplacesIt_InBothModes() {
			Patient patient=ImagingDb.CreatePatient("s06bexpover");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			foreach(bool isAtoZ in new[] { true,false }) {
				if(isAtoZ) {
					UseAtoZ();
				}
				else {
					ImagingDb.SetStorage(DataStorageType.InDatabase);
				}
				string mode=(isAtoZ ? "A-to-Z" : "in database");
				EobAttach eobAttach=EobAmendmentFiles.AddEob(MakePdfFile(),EnumMode.PreAuth,0,claimNumPreAuth);
				string pathExport=MakeTempPath(".pdf");
				File.WriteAllBytes(pathExport,new byte[] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20 });//longer than the EOB
				EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(eobAttach),pathExport);
				CollectionAssert.AreEqual(_arrayBytesPdf,File.ReadAllBytes(pathExport),"The EOB replaced the file that was there ("+mode+").");
				string pathJpg=MakeJpgFile();
				EhrAmendment ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(CreateAmendment(patient,"S06b export over "+mode),pathJpg);
				string pathExportAmd=MakeTempPath(".jpg");
				File.WriteAllBytes(pathExportAmd,new byte[] { 9,9,9 });
				EobAmendmentFiles.Export(EobAmendmentFiles.GetFileEntry(ehrAmendment),pathExportAmd);
				CollectionAssert.AreEqual(File.ReadAllBytes(pathJpg),File.ReadAllBytes(pathExportAmd),"The amendment's file replaced it ("+mode+").");
			}
		}
		#endregion Reading, the viewer's path and export

		#region Log entries
		[TestMethod]
		public void Copy_LogsTheFilesOwnName() {
			Patient patient=ImagingDb.CreatePatient("s06copy");
			long claimNumPreAuth=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			EobAttach eobAttach=EobAmendmentFiles.AddEob(MakeJpgFile(),EnumMode.PreAuth,0,claimNumPreAuth);
			int countCopyLogs=CountLogs(EnumPermType.Copy,0);
			EobAmendmentFiles.LogCopied(EobAmendmentFiles.GetFileEntry(eobAttach));
			List<SecurityLog> listCopyLogs=ImagingDb.GetLogs(EnumPermType.Copy,0);
			Assert.AreEqual(countCopyLogs+1,listCopyLogs.Count);
			Assert.AreEqual("Patient image "+eobAttach.FileName+" copied to clipboard",listCopyLogs.Last().LogText,
				"S0-6: the EOB's own name (the old window read a document row with the EOB's key).");
			Assert.AreEqual(0,listCopyLogs.Last().PatNum,"No patient, as the old window logged it.");
		}
		#endregion Log entries

		private static void AssertThrowsApplication(Action action) {
			try {
				action();
			}
			catch(ApplicationException) {
				return;
			}
			Assert.Fail("Expected ApplicationException.");
		}

		private static void AssertThrowsAny(Action action) {
			try {
				action();
			}
			catch(Exception) {
				return;
			}
			Assert.Fail("Expected an exception.");
		}
	}

	///<summary>The window's rules that need no database: its commands in each mode, how it shows and opens a file, and the wording of the
	///Printing and Copy log entries.</summary>
	[TestClass]
	public class EobAmendmentFilesRulesTests {
		private static readonly EnumCommand[] _arrayCommandsAll=new[] {
			EnumCommand.Print,EnumCommand.Delete,EnumCommand.ScanDoc,EnumCommand.ScanMultiDoc,EnumCommand.Import,EnumCommand.Export,
			EnumCommand.Copy,EnumCommand.Paste,EnumCommand.Close };

		[TestMethod]
		public void Commands_TheSameNineInEachMode_PrintAndDeleteOnTheList() {
			foreach(EnumMode mode in new[] { EnumMode.ClaimPayment,EnumMode.PreAuth,EnumMode.Amendment }) {
				CollectionAssert.AreEqual(_arrayCommandsAll,EobAmendmentFiles.GetCommands(mode),"Toolbar of "+mode);
				CollectionAssert.AreEqual(new[] { EnumCommand.Print,EnumCommand.Delete },EobAmendmentFiles.GetListMenuCommands(mode),"Right-click of "+mode);
			}
			CollectionAssert.AreEqual(new[] { EnumCommand.Close },EobAmendmentFiles.GetCommands(EnumMode.None));
			Assert.AreEqual(0,EobAmendmentFiles.GetListMenuCommands(EnumMode.None).Count);
			//No Info, Sign, radiograph or photo scan, crop, rotate, flip or windowing: they work on a patient's documents only.
			CollectionAssert.AreEquivalent(_arrayCommandsAll,Enum.GetValues(typeof(EnumCommand)).Cast<EnumCommand>().ToArray());
		}

		[TestMethod]
		public void Modes_PreAuthFirst_ImportSeveralForEobsOneForAnAmendment() {
			EhrAmendment ehrAmendment=new EhrAmendment { FileName="" };
			Assert.AreEqual(EnumMode.PreAuth,EobAmendmentFiles.GetMode(11,22,ehrAmendment),"The preauth first, as in the old window.");
			Assert.AreEqual(EnumMode.ClaimPayment,EobAmendmentFiles.GetMode(11,0,ehrAmendment));
			Assert.AreEqual(EnumMode.Amendment,EobAmendmentFiles.GetMode(0,0,ehrAmendment));
			Assert.AreEqual(EnumMode.None,EobAmendmentFiles.GetMode(0,0,null));
			Assert.IsTrue(EobAmendmentFiles.IsImportMultiSelect(EnumMode.ClaimPayment));
			Assert.IsTrue(EobAmendmentFiles.IsImportMultiSelect(EnumMode.PreAuth));
			Assert.IsFalse(EobAmendmentFiles.IsImportMultiSelect(EnumMode.Amendment));
			Assert.IsFalse(EobAmendmentFiles.IsReplaceConfirmNeeded(EnumMode.Amendment,ehrAmendment),"No file yet.");
			ehrAmendment.FileName="20200101_000000_5.jpg";
			Assert.IsTrue(EobAmendmentFiles.IsReplaceConfirmNeeded(EnumMode.Amendment,ehrAmendment));
			Assert.IsFalse(EobAmendmentFiles.IsReplaceConfirmNeeded(EnumMode.PreAuth,ehrAmendment),"New EOBs replace nothing.");
			Assert.IsFalse(EobAmendmentFiles.IsReplaceConfirmNeeded(EnumMode.Amendment,null));
		}

		///<summary>S0-6 part 2, fix 7: a multi-page scan that ended with an EZTwain error may be missing pages. It replaces an amendment's file only
		///after a second yes; for EOBs, and for an amendment that has no file yet, it is added as before, because it replaces nothing.</summary>
		[TestMethod]
		public void PartialScan_AsksOnlyBeforeReplacingAnAmendmentsFile() {
			EhrAmendment ehrAmendmentNoFile=new EhrAmendment { FileName="" };
			EhrAmendment ehrAmendmentWithFile=new EhrAmendment { FileName="20261101_101010_4.jpg" };
			Assert.IsTrue(EobAmendmentFiles.IsPartialScanConfirmNeeded(EnumMode.Amendment,ehrAmendmentWithFile));
			Assert.IsFalse(EobAmendmentFiles.IsPartialScanConfirmNeeded(EnumMode.Amendment,ehrAmendmentNoFile));
			Assert.IsFalse(EobAmendmentFiles.IsPartialScanConfirmNeeded(EnumMode.ClaimPayment,ehrAmendmentWithFile));
			Assert.IsFalse(EobAmendmentFiles.IsPartialScanConfirmNeeded(EnumMode.PreAuth,ehrAmendmentWithFile));
			Assert.IsFalse(EobAmendmentFiles.IsPartialScanConfirmNeeded(EnumMode.Amendment,null));
		}

		///<summary>S0-6 part 2, fix 8: the S0-6 scratch database has the TestDb scratch name, so a run that crashes before it drops it leaves nothing
		///for good: a later run's TestDb.DropStaleScratchDatabases drops it once it is stale.</summary>
		[TestMethod]
		public void ScratchDatabase_HasTheTestDbScratchName() {
			Assert.AreEqual(TestDb.ScratchName("eobamd"),EobAmendmentDb.DbName);
			StringAssert.Matches(EobAmendmentDb.DbName,new Regex(@"^forkmig_\d{12}_\d+_\d+_[a-z0-9_]+$"),
				"The pattern TestDb.DropStaleScratchDatabases drops.");
			string source=File.ReadAllText(TestDb.FindRepoFile(Path.Combine("tests","ForkMigrationTests","TestDb.cs")));
			StringAssert.Contains(source,"new Regex(\"^\"+PREFIX+@\"(\\d{12})_\\d+_\\d+_[a-z0-9_]+$\")");
		}

		[TestMethod]
		public void ShowAs_ImagesFitted_PdfsInTheViewer_OtherTypesNothing() {
			Assert.AreEqual(EnumShowAs.Image,EobAmendmentFiles.GetShowAs("a.jpg"));
			Assert.AreEqual(EnumShowAs.Image,EobAmendmentFiles.GetShowAs("a.JPEG"));
			Assert.AreEqual(EnumShowAs.Image,EobAmendmentFiles.GetShowAs("a.tif"));
			Assert.AreEqual(EnumShowAs.Image,EobAmendmentFiles.GetShowAs("a.png"));
			Assert.AreEqual(EnumShowAs.Pdf,EobAmendmentFiles.GetShowAs("a.pdf"));
			Assert.AreEqual(EnumShowAs.Pdf,EobAmendmentFiles.GetShowAs("a.PDF"));
			Assert.AreEqual(EnumShowAs.Nothing,EobAmendmentFiles.GetShowAs("a.docx"));
			Assert.AreEqual(EnumShowAs.Nothing,EobAmendmentFiles.GetShowAs("a"));
		}

		[TestMethod]
		public void DoubleClick_AnEobOtherThanJpgJpegGif_NeverAnAmendment() {
			Assert.IsTrue(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EobAttachNum=1,FileName="a.pdf" }));
			Assert.IsTrue(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EobAttachNum=1,FileName="a.tif" }));
			Assert.IsTrue(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EobAttachNum=1,FileName="a.docx" }));
			Assert.IsFalse(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EobAttachNum=1,FileName="a.jpg" }));
			Assert.IsFalse(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EobAttachNum=1,FileName="a.JPEG" }));
			Assert.IsFalse(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EobAttachNum=1,FileName="a.gif" }));
			Assert.IsFalse(EobAmendmentFiles.CanOpenInWindowsProgram(new FileEntry { EhrAmendmentNum=1,FileName="a.pdf" }));
			Assert.IsFalse(EobAmendmentFiles.CanOpenInWindowsProgram(null));
		}

		[TestMethod]
		public void PrintLog_EobPrintedWithNoPatient_AmendmentWithItsPatient() {
			FileEntry fileEntryEob=new FileEntry { EobAttachNum=3,FileName="20261101_101010_3.jpg" };
			Assert.AreEqual("EOB printed",EobAmendmentFiles.GetPrintAuditDescription(fileEntryEob),"As the old window logged it.");
			Assert.AreEqual(0,EobAmendmentFiles.GetPrintAuditPatNum(fileEntryEob));
			FileEntry fileEntryAmd=new FileEntry { EhrAmendmentNum=4,FileName="20261101_101010_4.jpg",Description="Allergy list corrected",PatNum=77 };
			Assert.AreEqual("Patient image Allergy list corrected printed",EobAmendmentFiles.GetPrintAuditDescription(fileEntryAmd));
			Assert.AreEqual(77,EobAmendmentFiles.GetPrintAuditPatNum(fileEntryAmd),"S0-6: the amendment's patient (the old window had none).");
			fileEntryAmd.Description="";
			Assert.AreEqual("Patient image 20261101_101010_4.jpg printed",EobAmendmentFiles.GetPrintAuditDescription(fileEntryAmd));
			Assert.AreEqual("Patient image 20261101_101010_3.jpg copied to clipboard",EobAmendmentFiles.GetCopyLogText(fileEntryEob));
		}
	}

	///<summary>Source checks of the rebuilt window (it cannot be opened in a test run).</summary>
	[TestClass]
	public class EobAmendmentWindowSourceTests {
		private const string HOOK="SetScriptEnabled(ImageHelper.IsPdfJavascriptAllowed());";

		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		private static int Count(string source,string text) {
			return Regex.Matches(source,Regex.Escape(text)).Count;
		}

		///<summary>The text of one method: from its signature to the first closing brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		private static string ReadWindow() {
			return Read("OpenPmsDental","Forms","FormImages.cs")+Read("OpenPmsDental","Forms","FormImages.designer.cs");
		}

		[TestMethod]
		public void FiveCallSites_Unchanged() {
			string formImages=Read("OpenPmsDental","Forms","FormImages.cs");
			StringAssert.Contains(formImages,"public partial class FormImages:FormODBase {");
			StringAssert.Contains(formImages,"public long ClaimPaymentNum;");
			StringAssert.Contains(formImages,"public long ClaimNumPreAuth;");
			StringAssert.Contains(formImages,"public EhrAmendment EhrAmendmentCur;");
			string claimEdit=Read("OpenPmsDental","Forms","FormClaimEdit.cs");
			Assert.AreEqual(2,Count(claimEdit,"using FormImages formImages=new FormImages();"));
			StringAssert.Contains(claimEdit,"formImages.ClaimPaymentNum=claimPayment.ClaimPaymentNum;");
			StringAssert.Contains(claimEdit,"formImages.ClaimNumPreAuth=_claim.ClaimNum;");
			string payBatch=Read("OpenPmsDental","Forms","FormClaimPayBatch.cs");
			Assert.AreEqual(1,Count(payBatch,"using FormImages formImages=new FormImages();"));
			StringAssert.Contains(payBatch,"formImages.ClaimPaymentNum=_claimPayment.ClaimPaymentNum;");
			string payPreAuth=Read("OpenPmsDental","Forms","FormClaimPayPreAuth.cs");
			Assert.AreEqual(1,Count(payPreAuth,"using FormImages formImages=new FormImages();"));
			StringAssert.Contains(payPreAuth,"formImages.ClaimNumPreAuth=_claimNumPreAuth;");
			string amendmentEdit=Read("OpenPmsDental","Forms","FormEhrAmendmentEdit.cs");
			Assert.AreEqual(1,Count(amendmentEdit,"using FormImages formImages=new FormImages();"));
			StringAssert.Contains(amendmentEdit,"formImages.EhrAmendmentCur=_ehrAmendment;");
			//No other window opens it.
			string folder=Path.GetDirectoryName(TestDb.FindRepoFile(Path.Combine("OpenPmsDental","Forms","FormImages.cs")));
			string folderProgram=Path.GetDirectoryName(folder);
			int countOpens=0;
			foreach(string path in Directory.GetFiles(folderProgram,"*.cs",SearchOption.AllDirectories)) {
				if(path.Contains("\\bin\\") || path.Contains("\\obj\\")) {
					continue;
				}
				countOpens+=Count(File.ReadAllText(path),"new FormImages()");
			}
			Assert.AreEqual(5,countOpens,"The five call sites, in four files.");
		}

		///<summary>S0-6 step 2: the old 2020 Images module is gone with its start-up choice and its Setup check box. Only the convert scripts, which
		///add and reset the preference row, and Pref.cs, which keeps the PrefName because they name it, may still name it. The row stays in the
		///database and nothing reads it, so a database with the option on simply gets the current Images module.</summary>
		[TestMethod]
		public void OldImagesModule_Removed_OnlyTheConvertScriptsAndPrefCsNameIt() {
			string folderMainModules=Path.GetDirectoryName(TestDb.FindRepoFile(Path.Combine("OpenPmsDental","Main Modules","ControlImages.cs")));
			foreach(string fileName in new[] { "ControlImagesOld.cs","ControlImagesOld.designer.cs","ControlImagesOld.resx" }) {
				Assert.IsFalse(File.Exists(Path.Combine(folderMainModules,fileName)),fileName+" is deleted.");
			}
			string root=Path.GetDirectoryName(Path.GetDirectoryName(folderMainModules));
			Regex regexNames=new Regex(@"ControlImagesOld|ImagesModuleUsesOld2020");
			HashSet<string> hashSetExtensions=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				".cs",".csproj",".xml",".xaml",".resx",".config",".settings",".ps1",".iss" };
			List<string> listOffenders=new List<string>();
			int countFiles=0;
			foreach(string path in EnumerateProgramFiles(root)) {
				if(!hashSetExtensions.Contains(Path.GetExtension(path))) {
					continue;
				}
				countFiles++;
				string fileName=Path.GetFileName(path);
				bool isConvertScript=Regex.IsMatch(fileName,@"^ConvertDatabases\d*\.cs$",RegexOptions.IgnoreCase);
				bool isPrefCs=SamePath(path,Path.Combine(root,"OpenPmsDentalBusiness","TableTypes","Pref.cs"));
				if(isConvertScript || isPrefCs) {
					continue;
				}
				if(regexNames.IsMatch(File.ReadAllText(path))) {
					listOffenders.Add(path.Substring(root.Length));
				}
			}
			Assert.IsTrue(countFiles>1000,"The scan found the program's sources ("+countFiles+" files).");
			Assert.AreEqual(0,listOffenders.Count,"Still names the old Images module or its option: "+string.Join(", ",listOffenders));
			//The PrefName stays, for the convert scripts, and says that nothing reads it any more.
			StringAssert.Contains(Read("OpenPmsDentalBusiness","TableTypes","Pref.cs"),"\t\tImagesModuleUsesOld2020,");
			StringAssert.Contains(Read("OpenPmsDentalBusiness","Misc","ConvertDatabases6.cs"),"'ImagesModuleUsesOld2020'");
			//Setup > Imaging has no check box for it, and the boxes below it moved up into its row.
			string designer=Read("OpenPmsDental","User Controls","Preferences","UserControlImagingGeneral.Designer.cs");
			StringAssert.Contains(designer,"this.checkImagingOrderDescending.Location = new System.Drawing.Point(5, 41);");
			StringAssert.Contains(designer,"this.checkImagingAllowPdfJavascript.Location = new System.Drawing.Point(5, 72);");
			StringAssert.Contains(designer,"this.groupBoxFunctionality.Size = new System.Drawing.Size(450, 98);");
			Assert.AreEqual(0,Count(Read("OpenPmsDental","Forms","FormPreferences.cs"),"DoSwapImagingModule"),"No forced restart after a module swap.");
			//FormOpenDental always creates the current module.
			string formOpenDental=Read("OpenPmsDental","Main Modules","FormOpenDental.cs");
			Assert.AreEqual(1,Count(formOpenDental,"controlImages=new ControlImages() { Visible=false };"));
		}

		///<summary>The program's own files under the repository root: not the tests, the documentation, the third-party sources, the build output or
		///the package folder.</summary>
		private static IEnumerable<string> EnumerateProgramFiles(string folder) {
			HashSet<string> hashSetSkip=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				"tests","docs","third-party","packages","bin","obj","NoHqTests","UnitTests","UnitTestsCore","node_modules","TestResults" };
			foreach(string path in Directory.EnumerateFiles(folder)) {
				yield return path;
			}
			foreach(string folderSub in Directory.EnumerateDirectories(folder)) {
				string name=Path.GetFileName(folderSub);
				if(name.StartsWith(".") || hashSetSkip.Contains(name)) {
					continue;
				}
				foreach(string path in EnumerateProgramFiles(folderSub)) {
					yield return path;
				}
			}
		}

		private static bool SamePath(string path1,string path2) {
			return string.Equals(Path.GetFullPath(path1),Path.GetFullPath(path2),StringComparison.OrdinalIgnoreCase);
		}

		[TestMethod]
		public void Window_NoWinFormsMenu_NoThreadAbort_NoProcessStart_NoPluginHooks_NoFileDrop() {
			string source=ReadWindow();
			Assert.AreEqual(0,Regex.Matches(source,@"\bContextMenu\b|\bMenuItem\b|\bMainMenu\b").Count,"A ContextMenuStrip, not the legacy menus.");
			StringAssert.Contains(source,"new ToolStripMenuItem(");
			Assert.AreEqual(0,Count(source,".Abort("));
			Assert.AreEqual(0,Count(source,"Process.Start("));
			Assert.AreEqual(0,Count(source,"new Process("));
			Assert.AreEqual(1,Count(source,"ODFileUtils.ProcessStart(filePath);"),"The double-click open.");
			Assert.AreEqual(0,Count(source,"Plugins."),"No plugin hooks (decision 16).");
			Assert.AreEqual(0,Count(source,"AllowDrop")+Count(source,"DragDrop"),"No file drop onto the list.");
			Assert.AreEqual(0,Count(source,"ControlImagesOld"));
			Assert.AreEqual(0,Count(source,"UI.ImageSelector(")+Count(source,"new ControlImageDisplay("),"Not the patient image selector or display.");
		}

		[TestMethod]
		public void Window_AppliesThePdfPreferenceBeforeEachNavigation() {
			string source=Read("OpenPmsDental","Forms","FormImages.cs");
			AssertBefore(MethodBody(source,"private async void LoadPdf("),"_odWebView2."+HOOK,"_odWebView2.ODWebView2Navigate(filePath);");
			Assert.AreEqual(Count(source,"ODWebView2Navigate("),Count(source,HOOK),"Every navigation of the PDF viewer reads the preference first.");
			Assert.IsTrue(Count(source,"ODWebView2Navigate(")>=1);
			Assert.AreEqual(0,Count(source,"CoreWebView2.Navigate("),"No navigation that skips the preference.");
			StringAssert.Contains(source,"_odWebView2.DoBlockNavigation=true;");
		}

		[TestMethod]
		public void Window_UsesTheImagingModulesParts() {
			string source=ReadWindow();
			StringAssert.Contains(source,"toolBarMain=new WpfControls.UI.ToolBar();");
			StringAssert.Contains(source,"zoomSlider=new WpfControls.UI.ZoomSlider();");
			StringAssert.Contains(source,"_odWebView2=new ODWebView2();");
			StringAssert.Contains(MethodBody(source,"private void LayoutToolBar()"),"EobAmendmentFiles.GetCommands(_mode)");
			StringAssert.Contains(MethodBody(source,"private void LayoutListMenu()"),"EobAmendmentFiles.GetListMenuCommands(_mode)");
			//Printing as ControlImages.ToolBarPrint_Click does, with today's half-inch margins and origin; PrinterL makes the one log entry.
			string print=MethodBody(source,"private void ToolBarPrint_Click(object sender,EventArgs e)");
			StringAssert.Contains(print,"PrinterL.TryPrintOrDebugClassicPreview(printDocument_PrintPage,EobAmendmentFiles.GetPrintAuditDescription(fileEntry),");
			StringAssert.Contains(print,"margins:new Margins(50,50,50,50)");
			StringAssert.Contains(print,"printoutOrigin:PrintoutOrigin.AtMargin");
			StringAssert.Contains(print,"auditPatNum:EobAmendmentFiles.GetPrintAuditPatNum(fileEntry)");
			Assert.AreEqual(0,Count(source,"EnumPermType.Printing"),"One Printing entry per print, made by PrinterL.");
			Assert.AreEqual(0,Count(source,"new PrintDialog"),"The program's Print Options window, not the Windows print dialog.");
			//Scanning with the module's EZTwain calls and this computer's scan settings.
			string scan=MethodBody(source,"private void ToolBarScanDoc_Click(object sender,EventArgs e)");
			AssertBefore(scan,"EZTwain.SetJpegQuality(ComputerPrefs.LocalComputer.ScanDocQuality);","EZTwain.Acquire(this.Handle);");
			AssertBefore(scan,"Clipboard.SetImage(bitmapScanned);","EobAmendmentFiles.AddEob(bitmapScanned,");
			string scanMulti=MethodBody(source,"private void ToolBarScanMulti_Click(object sender,EventArgs e)");
			AssertBefore(scanMulti,"EZTwain.AcquireMultipageFile(this.Handle,tempFile);","EobAmendmentFiles.AddEob(tempFile,");
			AssertBefore(scanMulti,"EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,tempFile)","ImageStore.TryDeleteFile(tempFile");
			Assert.AreEqual(0,Count(source,"Security.IsAuthorized("),"The window adds no permission check (there is none today).");
			//Paste through ODClipboard; onto an amendment that has a file it asks first, as scan and import do.
			string paste=MethodBody(source,"private void ToolBarPaste_Click(object sender,EventArgs e)");
			StringAssert.Contains(paste,"ODClipboard.GetImage()");
			StringAssert.Contains(paste,"ODClipboard.GetFileDropList()");
			AssertBefore(paste,"ConfirmReplaceAmendmentFile()","EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,bitmapPaste)");
			foreach(string signature in new[] { "private void ToolBarImport_Click(object sender,EventArgs e)",
				"private void ToolBarScanDoc_Click(object sender,EventArgs e)","private void ToolBarScanMulti_Click(object sender,EventArgs e)" })
			{
				AssertBefore(MethodBody(source,signature),"ConfirmReplaceAmendmentFile()","EobAmendmentFiles.ReplaceAmendmentFile(");
			}
			StringAssert.Contains(MethodBody(source,"private bool ConfirmReplaceAmendmentFile()"),"\"This will delete your old file. Proceed?\"");
			//Delete asks as before; Close returns OK.
			string delete=MethodBody(source,"private void ToolBarDelete_Click(object sender,EventArgs e)");
			AssertBefore(delete,"\"Delete EOB?\"","EobAmendmentFiles.DeleteEob(fileEntry);");
			AssertBefore(delete,"\"Delete amendment?\"","EobAmendmentFiles.DeleteAmendmentFile(_ehrAmendment);");
			StringAssert.Contains(MethodBody(source,"private void ToolBarClose_Click(object sender,EventArgs e)"),"DialogResult=DialogResult.OK;");
			//Copy logs through EobAmendmentFiles.
			StringAssert.Contains(MethodBody(source,"private void ToolBarCopy_Click(object sender,EventArgs e)"),"EobAmendmentFiles.LogCopied(fileEntry);");
			//With images in the database a double-click asks for an export first.
			StringAssert.Contains(MethodBody(source,"private void listFiles_MouseDoubleClick(object sender,MouseEventArgs e)"),
				"\"Images stored directly in database. Export file in order to open with external program.\"");
		}

		///<summary>S0-6 part 2, fix 3: every write (a new EOB with its row, the amendment's new file) runs without a Cancel button. Cancel aborts the
		///worker thread (FrmProgressAuto), which part-way through an upload could leave a row or an amendment without its file. Only reads (the
		///viewer's downloads, the double-click open and export) can be cancelled.</summary>
		[TestMethod]
		public void Window_WritesCannotBeCancelledPartWay() {
			string source=Read("OpenPmsDental","Forms","FormImages.cs");
			Assert.AreEqual(4,Count(source,"RunWriteWithCloudProgress(() => eobAttach=EobAmendmentFiles.AddEob("),"Import, paste, scan, multi-page scan.");
			Assert.AreEqual(4,Count(source,"RunWriteWithCloudProgress(() => _ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile("),
				"Import, paste, scan, multi-page scan.");
			Assert.AreEqual(4,Count(source,"EobAmendmentFiles.AddEob("),"No other write of an EOB.");
			Assert.AreEqual(4,Count(source,"EobAmendmentFiles.ReplaceAmendmentFile("),"No other write of the amendment's file.");
			Assert.AreEqual(0,Regex.Matches(source,@"RunWithCloudProgress\([^;]*(AddEob|ReplaceAmendmentFile)\(").Count,"No write in a cancellable progress window.");
			string write=MethodBody(source,"private void RunWriteWithCloudProgress(Action action,string startingMessage)");
			AssertBefore(write,"progressWin.ShowCancelButton=false;","progressWin.ShowDialog();");
			StringAssert.Contains(write,"progressWin.StopNotAllowedMessage=");
			//Without the Cancel button the progress window cannot be closed (its X shows the message instead) until the action is done.
			string frmProgressAuto=Read("WpfControlsOD","UI","Progress","FrmProgressAuto.xaml.cs");
			Assert.IsTrue(Regex.IsMatch(frmProgressAuto,@"if\(!ShowCancelButton\) \{//tried to close with X[^\n]*\n\s*MessageBox\.Show\(StopNotAllowedMessage\);[^\n]*\n\s*e\.Cancel=true;"),
				"FrmProgressAuto refuses to close while the action runs when there is no Cancel button.");
			//The reads stay cancellable.
			StringAssert.Contains(MethodBody(source,"private bool RunWithCloudProgress(Action action,string startingMessage)"),"return !progressWin.IsCancelled;");
		}

		///<summary>S0-6 part 2, fixes 4 and 7: after a failed replacement the window's amendment is still on its old file (EobAmendmentFiles does not
		///change the object it is given), which shows again; a multi-page scan that ended with an error asks before it replaces the amendment's file.
		///</summary>
		[TestMethod]
		public void Window_AFailedOrPartialReplaceKeepsTheAmendmentsFile() {
			string source=Read("OpenPmsDental","Forms","FormImages.cs");
			string import=MethodBody(source,"private void ToolBarImport_Click(object sender,EventArgs e)");
			Assert.IsTrue(Regex.IsMatch(import,@"catch\(Exception ex\) \{\s*MessageBox\.Show\(Lan\.g\(this,""Unable to copy file, May be in use: ""\)\+ex\.Message\+"": ""\+listFileNames\[0\]\);"
				+@"\s*//[^\n]*\n\s*FillList\(\);\s*SelectAmendment\(\);\s*return;"),"A failed import shows the stored file, not the one that failed.");
			string paste=MethodBody(source,"private void ToolBarPaste_Click(object sender,EventArgs e)");
			Assert.IsTrue(Regex.IsMatch(paste,@"""Error saving amendment\.""\)\);\s*//[^\n]*\n\s*FillList\(\);\s*SelectAmendment\(\);\s*return;"));
			foreach(string signature in new[] { "private void ToolBarScanDoc_Click(object sender,EventArgs e)","private void ToolBarScanMulti_Click(object sender,EventArgs e)" }) {
				Assert.IsTrue(Regex.IsMatch(MethodBody(source,signature),@"if\(!isSaved\) \{\s*if\(_mode==EobAmendmentFiles\.EnumMode\.Amendment\) \{\s*SelectAmendment\(\);"),signature);
			}
			string scanMulti=MethodBody(source,"private void ToolBarScanMulti_Click(object sender,EventArgs e)");
			AssertBefore(scanMulti,"\"Unable to scan. Please make sure you can scan using other software. Error: \"",
				"EobAmendmentFiles.IsPartialScanConfirmNeeded(_mode,_ehrAmendment)");
			AssertBefore(scanMulti,"EobAmendmentFiles.IsPartialScanConfirmNeeded(_mode,_ehrAmendment)","EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,tempFile)");
			StringAssert.Contains(scanMulti,
				"MsgBox.Show(this,MsgBoxButtons.YesNo,\"The scan ended with an error, so the document may be missing pages. Replace the amendment's file with it anyway?\")");
			Assert.IsTrue(Regex.IsMatch(scanMulti,@"anyway\?""\)\)\s*\{\s*EobAmendmentFiles\.DeleteTempCopy\(tempFile\);\s*return;"),"No: the temp file goes and nothing is stored.");
			//The other errors return before anything is stored, as before; only the generic one goes on.
			Assert.AreEqual(1,Count(scanMulti,"IsPartialScanConfirmNeeded("));
		}

		///<summary>S0-6 part 2, fix 5: the files are listed and the first one shown when the window is shown, not while it loads, so a download's
		///progress and any message appear over a visible window, as in the old window.</summary>
		[TestMethod]
		public void Window_ShowsTheFirstFileOnceItIsShown() {
			string source=Read("OpenPmsDental","Forms","FormImages.cs");
			string load=MethodBody(source,"private void FormImages_Load(object sender,EventArgs e)");
			Assert.AreEqual(0,Count(load,"FillList()")+Count(load,"ShowSelected(")+Count(load,"SetSelected("),"Nothing is listed or shown while it loads.");
			string shown=MethodBody(source,"private void FormImages_Shown(object sender,EventArgs e)");
			AssertBefore(shown,"FillList();","listFiles.SetSelected(0);");
			AssertBefore(shown,"listFiles.SetSelected(0);","ShowSelected();");
			StringAssert.Contains(Read("OpenPmsDental","Forms","FormImages.designer.cs"),"this.Shown += new System.EventHandler(this.FormImages_Shown);");
		}

		///<summary>S0-6 part 2, fix 6: UI.ListBox raises SelectionChangeCommitted on MouseDown and on every MouseMove of a drag, so while a mouse button
		///is down the window waits, and shows the selected file once, when the button comes up (or the list loses the mouse). Dragging over the list no
		///longer opens every file it passes, which with cloud storage is a download each.</summary>
		[TestMethod]
		public void Window_DraggingOverTheListShowsOnlyTheFileWhereTheButtonComesUp() {
			string listBox=Read("OpenPmsDental","UI","ListBox.cs");
			StringAssert.Contains(MethodBody(listBox,"protected override void OnMouseMove(MouseEventArgs e)"),"SelectionChangeCommitted?.Invoke(this,new EventArgs());",
				"The reason for the fix: the list box raises it while dragging.");
			string source=Read("OpenPmsDental","Forms","FormImages.cs");
			string changed=MethodBody(source,"private void listFiles_SelectionChangeCommitted(object sender,EventArgs e)");
			Assert.IsTrue(Regex.IsMatch(changed,@"if\(Control\.MouseButtons!=MouseButtons\.None\) \{\s*_isListSelectionPending=true;\s*return;\s*\}"));
			AssertBefore(changed,"return;","ShowSelected();");
			Assert.IsTrue(Regex.IsMatch(MethodBody(source,"private void listFiles_MouseUp(object sender,MouseEventArgs e)"),
				@"if\(!_isListSelectionPending\) \{\s*return;\s*\}\s*_isListSelectionPending=false;\s*ShowSelected\(\);"));
			Assert.IsTrue(Regex.IsMatch(MethodBody(source,"private void listFiles_MouseCaptureChanged(object sender,EventArgs e)"),
				@"if\(!_isListSelectionPending \|\| listFiles\.Capture\) \{\s*return;\s*\}\s*_isListSelectionPending=false;\s*ShowSelected\(\);"));
			string designer=Read("OpenPmsDental","Forms","FormImages.designer.cs");
			StringAssert.Contains(designer,"this.listFiles.MouseUp += new System.Windows.Forms.MouseEventHandler(this.listFiles_MouseUp);");
			StringAssert.Contains(designer,"this.listFiles.MouseCaptureChanged += new System.EventHandler(this.listFiles_MouseCaptureChanged);");
		}

		///<summary>S0-6 part 2, fix 1: the Save dialog asks before it replaces a file, and ImageStore then replaces it with an A-to-Z folder too.</summary>
		[TestMethod]
		public void Export_TheSaveDialogAsks_ThenTheCopyReplaces() {
			StringAssert.Contains(MethodBody(Read("OpenPmsDental","Forms","FormImages.cs"),"private void ToolBarExport_Click(object sender,EventArgs e)"),
				"saveFileDialog.OverwritePrompt=true;");
			string imageStore=Read("OpenPmsDentalBusiness","Imaging","ImageStore.cs");
			StringAssert.Contains(MethodBody(imageStore,"public static void ExportEobAttach(string saveToPath,EobAttach eob)"),
				"FileAtoZ.Copy(eobPath,saveToPath,FileAtoZSourceDestination.AtoZToLocal,doOverwrite:true);");
			StringAssert.Contains(MethodBody(imageStore,"public static void ExportAmdAttach(string saveToPath,EhrAmendment amd)"),
				"FileAtoZ.Copy(eobPath,saveToPath,FileAtoZSourceDestination.AtoZToLocal,doOverwrite:true);");
		}
	}
}
