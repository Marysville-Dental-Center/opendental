// Chart letters against a scratch MariaDB database (a fresh 24.3.63 database migrated to 25.3.48 by the fork runner), with synthetic patients
// and letters only. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when
// CHARTLETTER_TEST_DB=skip. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ChartLetterTests {
	[TestClass]
	public class ChartLetterDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("letters");
		private static string _skipReason;
		private static string _folderRoot;
		private static long _userNum1;
		private static long _userNum2;
		private static long _provNum;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh25_3(DB_NAME);
			SetStorage(DataStorageType.LocalAtoZ);
			DataCore.NonQ("INSERT INTO userod (UserName) VALUES ('ChartLetterUserOne')");
			_userNum1=PIn.Long(DataCore.GetScalar("SELECT MAX(UserNum) FROM userod"));
			DataCore.NonQ("INSERT INTO userod (UserName) VALUES ('ChartLetterUserTwo')");
			_userNum2=PIn.Long(DataCore.GetScalar("SELECT MAX(UserNum) FROM userod"));
			_provNum=PIn.Long(DataCore.GetScalar("SELECT MIN(ProvNum) FROM provider"));
			Cache.Refresh(InvalidType.Security);
			Security.CurUser=Userods.GetUser(_userNum1)??new Userod { UserNum=_userNum1 };
			_folderRoot=Path.Combine(Path.GetTempPath(),"ChartLetterTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folderRoot);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
			try {
				Directory.Delete(_folderRoot,true);
			}
			catch(Exception) {
				//Temporary files only.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		private static void SetStorage(DataStorageType dataStorageType) {
			DataCore.NonQ("UPDATE preference SET ValueString='"+(int)dataStorageType+"' WHERE PrefName='AtoZfolderUsed'");
			Cache.Refresh(InvalidType.Prefs);
		}

		private static Patient MakePatient(string lName) {
			DataCore.NonQ("INSERT INTO patient (LName,FName,PatStatus,PriProv,BillingType,DateFirstVisit) VALUES ('"+lName+"','Synthetic',0,"
				+_provNum+",0,'2024-01-01')");
			long patNum=PIn.Long(DataCore.GetScalar("SELECT MAX(PatNum) FROM patient"));
			DataCore.NonQ("UPDATE patient SET Guarantor=PatNum WHERE PatNum="+patNum);
			return Patients.GetPat(patNum);
		}

		private static string PatFolder(Patient patient) {
			string folder=Path.Combine(_folderRoot,patient.PatNum.ToString());
			Directory.CreateDirectory(folder);
			return folder;
		}

		private static byte[] Letter(string text) {
			return DocxMailMergeTests.MakeDocx("<w:p><w:r><w:t xml:space=\"preserve\">"+text+"</w:t></w:r></w:p>");
		}

		private static Document Create(Patient patient,string description,string text,DateTime? date=null) {
			return ChartLetters.CreateLetter(patient,Letter(text),".docx",description,_provNum,_userNum1,date??DateTime.Now.AddMinutes(-5),
				PatFolder(patient));
		}

		private static DataTable ProgNotes(Patient patient,bool isAuditMode) {
			ChartModuleFilters chartModuleFilters=new ChartModuleFilters(false);
			chartModuleFilters.ShowSheetsEForms=true;
			return ChartModules.GetProgNotes(patient.PatNum,isAuditMode,chartModuleFilters);
		}

		private static List<DataRow> LetterRows(DataTable table) {
			return table.Rows.Cast<DataRow>().Where(x => ChartModules.GetRowType(x,out long _)==ProgNotesRowType.ChartLetter).ToList();
		}

		[TestMethod]
		public void CreateLetter_StoresRowFileAndHash() {
			Patient patient=MakePatient("ClCreate");
			byte[] bytes=Letter("Post-op instructions");
			Document document=ChartLetters.CreateLetter(patient,bytes,".docx","Post-op",_provNum,_userNum1,DateTime.Now.AddMinutes(-1),
				PatFolder(patient));
			Document documentDb=Documents.GetByNum(document.DocNum);
			Assert.AreEqual((byte)EnumChartLetterStatus.Current,documentDb.ChartLetterStatus);
			Assert.AreEqual(0,documentDb.DocCategory,"A chart letter has no image category, which keeps it out of the Imaging tree.");
			Assert.AreEqual(_userNum1,documentDb.UserNum);
			Assert.AreEqual(_provNum,documentDb.ProvNum);
			Assert.AreEqual(ImageType.File,documentDb.ImgType);
			Assert.AreEqual(ChartLetters.ComputeHash(bytes),documentDb.ChartLetterHash);
			Assert.AreEqual(32,documentDb.ChartLetterHash.Length);
			Assert.IsTrue(documentDb.FileName.EndsWith(".docx"));
			CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(PatFolder(patient),documentDb.FileName)));
			CollectionAssert.AreEqual(bytes,ChartLetters.GetFileBytes(documentDb,PatFolder(patient)));
			Assert.AreEqual(EnumChartLetterHashMatch.Match,ChartLetters.GetHashMatch(documentDb,ChartLetters.GetFileBytes(documentDb,PatFolder(patient))));
			string logText=DataCore.GetScalar("SELECT LogText FROM securitylog WHERE FKey="+document.DocNum+" AND PermType="
				+(int)EnumPermType.ImageCreate+" ORDER BY SecurityLogNum DESC LIMIT 1");
			StringAssert.StartsWith(logText,"Chart Letter: Chart letter created");
		}

		[TestMethod]
		public void CreateLetter_FutureDateOrEmpty_Rejected() {
			Patient patient=MakePatient("ClFuture");
			Expect.Throws<ApplicationException>(() => ChartLetters.CreateLetter(patient,Letter("x"),".docx","x",_provNum,_userNum1,
				DateTime.Now.AddDays(2),PatFolder(patient)));
			Expect.Throws<ApplicationException>(() => ChartLetters.CreateLetter(patient,new byte[0],".docx","x",_provNum,_userNum1,
				DateTime.Now.AddDays(-2),PatFolder(patient)));
			Assert.AreEqual(0,ChartLetters.GetForPatient(patient.PatNum,true).Count);
		}

		[TestMethod]
		public void ImagingTree_LeavesOutChartLetters_AndNeverMovesThem() {
			Patient patient=MakePatient("ClTree");
			Document documentLetter=Create(patient,"Referral","Dear doctor");
			//An ordinary document with no category: the tree still moves it into the first category, as before.
			Document documentOrphan=new Document { PatNum=patient.PatNum,FileName="orphan.jpg",Description="orphan",DateCreated=DateTime.Now,DocCategory=0 };
			Documents.Insert(documentOrphan);
			DataTable table=Documents.GetTreeListTableForPatient(patient.PatNum,false);
			List<long> listDocNums=table.Rows.Cast<DataRow>().Select(x => PIn.Long(x["DocNum"].ToString())).ToList();
			CollectionAssert.DoesNotContain(listDocNums,documentLetter.DocNum);
			CollectionAssert.Contains(listDocNums,documentOrphan.DocNum);
			Assert.AreEqual(0,Documents.GetByNum(documentLetter.DocNum).DocCategory,"The chart letter keeps DocCategory 0.");
			Assert.AreNotEqual(0,Documents.GetByNum(documentOrphan.DocNum).DocCategory);
		}

		[TestMethod]
		public void DatabaseMaintenance_DocumentWithNoCategory_LeavesChartLettersAlone() {
			Patient patient=MakePatient("ClDbm");
			Document documentLetter=Create(patient,"Dbm","Text");
			DatabaseMaintenances.DocumentWithNoCategory(false,DbmMode.Fix);
			Document documentDb=Documents.GetByNum(documentLetter.DocNum);
			Assert.AreEqual(0,documentDb.DocCategory);
			Assert.AreEqual((byte)EnumChartLetterStatus.Current,documentDb.ChartLetterStatus);
		}

		[TestMethod]
		public void ProgressNotes_ListCurrentLetters_HistoryOnlyInAudit() {
			Patient patient=MakePatient("ClNotes");
			Document documentV1=Create(patient,"Surgery note","Version one",DateTime.Now.AddHours(-3));
			Document documentV2=ChartLetters.SaveNewVersion(documentV1,patient,Letter("Version two"),_userNum2,PatFolder(patient));
			Document documentOther=Create(patient,"Other","Other letter",DateTime.Now.AddHours(-2));
			ChartLetters.Delete(documentOther,_userNum1);
			List<DataRow> listRows=LetterRows(ProgNotes(patient,false));
			Assert.AreEqual(1,listRows.Count,"Only the current version of the letter that is not deleted.");
			Assert.AreEqual(documentV2.DocNum.ToString(),listRows[0]["DocNum"].ToString());
			Assert.AreEqual("Chart Letter - Surgery note",listRows[0]["description"].ToString());
			Assert.AreEqual(Userods.GetName(_userNum2),listRows[0]["user"].ToString());
			Assert.AreEqual("0",listRows[0]["SheetNum"].ToString());
			Assert.AreEqual(documentV1.DateCreated.Date,((DateTime)listRows[0]["ProcDate"]).Date,"A new version keeps the letter's date.");
			List<DataRow> listRowsAudit=LetterRows(ProgNotes(patient,true));
			Assert.AreEqual(3,listRowsAudit.Count);
			Assert.IsTrue(listRowsAudit.Any(x => x["DocNum"].ToString()==documentV1.DocNum.ToString()
				&& x["description"].ToString().EndsWith("(old version)")));
			Assert.IsTrue(listRowsAudit.Any(x => x["DocNum"].ToString()==documentOther.DocNum.ToString()
				&& x["description"].ToString().EndsWith("(deleted)")));
			Assert.AreEqual(0,LetterRows(ChartModules.GetProgNotes(patient.PatNum,false,new ChartModuleFilters(false))).Count,
				"Chart letters show with the sheets and eForms filter.");
		}

		[TestMethod]
		public void SaveNewVersion_WritesNewRow_SupersedesOld_ClearsSignature() {
			Patient patient=MakePatient("ClVersion");
			Document documentV1=Create(patient,"Consult","First text");
			Document documentOld=documentV1.Copy();
			documentV1.Signature="synthetic-signature";
			Assert.IsTrue(ChartLetters.UpdateProperties(documentV1,documentOld,_userNum1));
			byte[] bytesV2=Letter("Second text");
			Document documentV2=ChartLetters.SaveNewVersion(documentV1,patient,bytesV2,_userNum2,PatFolder(patient));
			Assert.IsNotNull(documentV2);
			Assert.AreNotEqual(documentV1.DocNum,documentV2.DocNum);
			Document documentV1Db=Documents.GetByNum(documentV1.DocNum);
			Document documentV2Db=Documents.GetByNum(documentV2.DocNum);
			Assert.AreEqual((byte)EnumChartLetterStatus.Superseded,documentV1Db.ChartLetterStatus);
			Assert.AreEqual("synthetic-signature",documentV1Db.Signature,"The old version keeps its signature.");
			Assert.AreEqual((byte)EnumChartLetterStatus.Current,documentV2Db.ChartLetterStatus);
			Assert.AreEqual("",documentV2Db.Signature,"A new version must be signed again.");
			Assert.AreEqual(_userNum2,documentV2Db.UserNum);
			Assert.AreEqual(_userNum1,documentV1Db.UserNum);
			Assert.AreEqual(ChartLetters.ComputeHash(bytesV2),documentV2Db.ChartLetterHash);
			Assert.AreNotEqual(documentV1Db.FileName,documentV2Db.FileName,"Each version has its own file.");
			Assert.AreEqual(documentV1Db.Description,documentV2Db.Description);
			Assert.AreEqual(documentV1Db.DateCreated,documentV2Db.DateCreated);
			Assert.AreEqual(0,documentV2Db.DocCategory);
			Assert.AreEqual(EnumChartLetterHashMatch.Match,ChartLetters.GetHashMatch(documentV1Db,ChartLetters.GetFileBytes(documentV1Db,PatFolder(patient))),
				"The old version's file is kept unchanged.");
			//Saving the same bytes again writes nothing.
			Assert.IsNull(ChartLetters.SaveNewVersion(documentV2Db,patient,bytesV2,_userNum2,PatFolder(patient)));
			//An old version can no longer be edited.
			Expect.Throws<ApplicationException>(() => ChartLetters.SaveNewVersion(documentV1Db,patient,Letter("Third"),_userNum2,
				PatFolder(patient)));
			Assert.AreEqual(1,ChartLetters.GetForPatient(patient.PatNum,false).Count);
			Assert.AreEqual(2,ChartLetters.GetForPatient(patient.PatNum,true).Count);
		}

		[TestMethod]
		public void Delete_OnlyFlagsTheRow() {
			Patient patient=MakePatient("ClDelete");
			Document document=Create(patient,"To delete","Text");
			ChartLetters.Delete(document,_userNum2);
			Document documentDb=Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true);
			Assert.IsNotNull(documentDb);
			Assert.AreEqual((byte)EnumChartLetterStatus.Deleted,documentDb.ChartLetterStatus);
			Assert.AreEqual(_userNum1,documentDb.UserNum,"Deleting does not change who last changed the content.");
			Assert.IsTrue(File.Exists(Path.Combine(PatFolder(patient),documentDb.FileName)));
			Assert.AreEqual(0,ChartLetters.GetForPatient(patient.PatNum,false).Count);
			Expect.Throws<ApplicationException>(() => ChartLetters.SaveNewVersion(documentDb,patient,Letter("x"),_userNum1,PatFolder(patient)));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE FKey="+document.DocNum+" AND PermType="
				+(int)EnumPermType.ImageDelete+" AND LogText LIKE 'Chart Letter: Chart letter deleted%'"));
		}

		[TestMethod]
		public void UpdateProperties_KeepsUserHashAndStatus() {
			Patient patient=MakePatient("ClProps");
			Document document=Create(patient,"Before","Text",DateTime.Now.AddDays(-3));
			Document documentOld=document.Copy();
			document.Description="After";
			document.DateCreated=DateTime.Now.AddDays(-2);
			document.ProvNum=0;
			document.UserNum=_userNum2;//Ignored: only content edits change the user.
			document.ChartLetterHash="tampered";
			document.ChartLetterStatus=(byte)EnumChartLetterStatus.Deleted;
			Assert.IsTrue(ChartLetters.UpdateProperties(document,documentOld,_userNum2));
			Document documentDb=Documents.GetByNum(document.DocNum);
			Assert.AreEqual("After",documentDb.Description);
			Assert.AreEqual(0,documentDb.ProvNum);
			Assert.AreEqual(_userNum1,documentDb.UserNum);
			Assert.AreEqual(documentOld.ChartLetterHash,documentDb.ChartLetterHash);
			Assert.AreEqual((byte)EnumChartLetterStatus.Current,documentDb.ChartLetterStatus);
			Document documentFuture=documentDb.Copy();
			documentFuture.DateCreated=DateTime.Now.AddDays(1);
			Expect.Throws<ApplicationException>(() => ChartLetters.UpdateProperties(documentFuture,documentDb,_userNum1),
				"A chart letter can never be future-dated.");
			Assert.IsFalse(ChartLetters.UpdateProperties(documentDb.Copy(),documentDb,_userNum1),"Nothing changed.");
		}

		[TestMethod]
		public void HashMismatch_WhenFileEditedOutsideTheProgram() {
			Patient patient=MakePatient("ClMismatch");
			Document document=Create(patient,"Outside","Original");
			File.WriteAllBytes(Path.Combine(PatFolder(patient),document.FileName),Letter("Changed outside"));
			Assert.AreEqual(EnumChartLetterHashMatch.NoMatch,ChartLetters.GetHashMatch(document,ChartLetters.GetFileBytes(document,PatFolder(patient))));
			ChartLetters.LogHashMismatch(document,_userNum1);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE FKey="+document.DocNum
				+" AND LogText LIKE '%hash mismatch detected%'"));
			File.Delete(Path.Combine(PatFolder(patient),document.FileName));
			Assert.AreEqual(EnumChartLetterHashMatch.FileMissing,ChartLetters.GetHashMatch(document,ChartLetters.GetFileBytes(document,PatFolder(patient))));
		}

		[TestMethod]
		public void EditSession_SavedWorkingCopy_BecomesNewVersion() {
			Patient patient=MakePatient("ClSession");
			Document document=Create(patient,"Session letter","Draft");
			string workingRoot=Path.Combine(_folderRoot,"working");
			using(ChartLetterEditSession chartLetterEditSession=ChartLetterEditSession.Start(document,patient,PatFolder(patient),_userNum2,workingRoot)) {
				Assert.IsTrue(chartLetterEditSession.WorkingFilePath.EndsWith("Session letter.docx"));
				CollectionAssert.AreEqual(ChartLetters.GetFileBytes(document,PatFolder(patient)),File.ReadAllBytes(chartLetterEditSession.WorkingFilePath));
				Assert.IsNull(chartLetterEditSession.CheckForChanges(),"Nothing saved yet.");
				byte[] bytesEdited=Letter("Edited in the word processor");
				File.WriteAllBytes(chartLetterEditSession.WorkingFilePath,bytesEdited);
				Document documentNew=chartLetterEditSession.CheckForChanges();
				Assert.IsNotNull(documentNew);
				Assert.AreEqual(documentNew.DocNum,chartLetterEditSession.DocumentCur.DocNum);
				Assert.AreEqual(ChartLetters.ComputeHash(bytesEdited),Documents.GetByNum(documentNew.DocNum).ChartLetterHash);
				Assert.AreEqual(_userNum2,Documents.GetByNum(documentNew.DocNum).UserNum);
				Assert.IsNull(chartLetterEditSession.CheckForChanges(),"The same save is not stored twice.");
				File.WriteAllBytes(chartLetterEditSession.WorkingFilePath,Letter("Second save"));
				Assert.IsNotNull(chartLetterEditSession.CheckForChanges());
			}
			Assert.AreEqual(3,ChartLetters.GetForPatient(patient.PatNum,true).Count);
			Assert.AreEqual(1,ChartLetters.GetForPatient(patient.PatNum,false).Count);
		}

		[TestMethod]
		public void EditSession_Watcher_StoresVersionAfterSave() {
			Patient patient=MakePatient("ClWatch");
			Document document=Create(patient,"Watched","Draft");
			ManualResetEventSlim manualResetEvent=new ManualResetEventSlim(false);
			ChartLetterVersionEventArgs eventArgs=null;
			using(ChartLetterEditSession chartLetterEditSession=ChartLetterEditSession.Start(document,patient,PatFolder(patient),_userNum1,
				Path.Combine(_folderRoot,"working")))
			{
				chartLetterEditSession.VersionSaved+=(sender,e) => {
					eventArgs=e;
					manualResetEvent.Set();
				};
				chartLetterEditSession.StartWatching(200);
				File.WriteAllBytes(chartLetterEditSession.WorkingFilePath,Letter("Saved by the editor"));
				Assert.IsTrue(manualResetEvent.Wait(TimeSpan.FromSeconds(20)),"The save was noticed.");
			}
			Assert.IsNull(eventArgs.Error,eventArgs.Error?.ToString());
			Assert.IsNotNull(eventArgs.DocumentNew);
			Assert.AreEqual(document.DocNum,eventArgs.DocumentOld.DocNum);
			Assert.AreEqual((byte)EnumChartLetterStatus.Superseded,Documents.GetByNum(document.DocNum).ChartLetterStatus);
		}

		[TestMethod]
		public void EditSession_OnlyCurrentVersion() {
			Patient patient=MakePatient("ClSessionOld");
			Document document=Create(patient,"Old","Draft");
			ChartLetters.Delete(document,_userNum1);
			Document documentDb=Documents.GetByNum(document.DocNum);
			Expect.Throws<ApplicationException>(() => ChartLetterEditSession.Start(documentDb,patient,PatFolder(patient),_userNum1,
				Path.Combine(_folderRoot,"working")));
		}

		[TestMethod]
		public void ImagesStoredInDatabase_LetterInRawBase64() {
			Patient patient=MakePatient("ClInDb");
			SetStorage(DataStorageType.InDatabase);
			try {
				byte[] bytes=Letter("In the database");
				Document document=ChartLetters.CreateLetter(patient,bytes,".docx","InDb",_provNum,_userNum1,DateTime.Now.AddMinutes(-1),"");
				Document documentDb=Documents.GetByNum(document.DocNum);
				Assert.AreEqual(Convert.ToBase64String(bytes),documentDb.RawBase64);
				CollectionAssert.AreEqual(bytes,ChartLetters.GetFileBytes(documentDb,""));
				byte[] bytesV2=Letter("Version two in the database");
				Document documentV2=ChartLetters.SaveNewVersion(documentDb,patient,bytesV2,_userNum1,"");
				CollectionAssert.AreEqual(bytesV2,ChartLetters.GetFileBytes(Documents.GetByNum(documentV2.DocNum),""));
				CollectionAssert.AreEqual(bytes,ChartLetters.GetFileBytes(Documents.GetByNum(documentDb.DocNum),""),"The old version keeps its bytes.");
			}
			finally {
				SetStorage(DataStorageType.LocalAtoZ);
			}
		}

		[TestMethod]
		public void LetterMergeValues_FromTheLetterQuery() {
			Patient patient=MakePatient("ClMerge");
			LetterMerge letterMerge=new LetterMerge { Description="Chart letter test",TemplateName="x.docx",DataFileName="x.txt",Category=0 };
			LetterMerges.Insert(letterMerge);
			foreach(string field in new[] { "FName","LName" }) {
				LetterMergeFields.Insert(new LetterMergeField { LetterMergeNum=letterMerge.LetterMergeNum,FieldName=field });
			}
			LetterMergeFields.RefreshCache();
			DataTable table=LetterMergesQueries.GetLetterMergeInfo(patient,letterMerge);
			Dictionary<string,string> dictValues=LetterMergesQueries.GetMergeValues(letterMerge,table);
			Assert.AreEqual("Synthetic",dictValues["fname"]);
			Assert.AreEqual("ClMerge",dictValues["LName"]);
			byte[] merged=DocxMailMerge.Merge(DocxMailMerge.CreateTemplate("",new[] { "FName","LName" }),dictValues,out List<string> listUnknown);
			Assert.AreEqual(0,listUnknown.Count);
			Assert.AreEqual("FName: Synthetic\nLName: ClMerge",DocxMailMerge.GetDocumentText(merged));
		}
	}
}
