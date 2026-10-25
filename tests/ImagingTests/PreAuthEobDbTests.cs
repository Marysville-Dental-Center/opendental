// 25.2 v25_2-account-preauth-eob-attach against a scratch 25.3.48 database: EOBs keyed by eobattach.ClaimNumPreAuth, as Open Dental writes them
// and as this program writes them, in the database and in an A-to-Z folder. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingTests {
	[TestClass]
	public class PreAuthEobDbTests {
		private readonly List<string> _listPathsToDelete=new List<string>();

		[TestInitialize]
		public void TestInitialize() {
			ImagingDb.Begin();
			ImagingDb.SetStorage(DataStorageType.InDatabase);
			Security.CurUser=new Userod { UserNum=1,UserName="aimgtest" };
		}

		[TestCleanup]
		public void TestCleanup() {
			if(ImagingDb.SkipReason==null) {
				ImagingDb.SetStorage(DataStorageType.InDatabase);
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

		///<summary>A small made-up file standing in for a scanned EOB.</summary>
		private string MakeEobFile(string extension=".pdf") {
			string path=Path.Combine(Path.GetTempPath(),"aimg_"+Guid.NewGuid().ToString("N")+extension);
			File.WriteAllBytes(path,new byte[] { 0x25,0x50,0x44,0x46,0x2D,0x31,0x2E,0x34,0x0A,0x25,0x25,0x45,0x4F,0x46 });
			_listPathsToDelete.Add(path);
			return path;
		}

		private static void InsertRawEob(long claimPaymentNum,long claimNumPreAuth,DateTime dateTCreated,string fileName) {
			DataCore.NonQ("INSERT INTO eobattach (ClaimPaymentNum,DateTCreated,FileName,RawBase64,ClaimNumPreAuth) VALUES ("
				+POut.Long(claimPaymentNum)+","+POut.DateT(dateTCreated)+",'"+POut.String(fileName)+"','',"+POut.Long(claimNumPreAuth)+")");
		}

		///<summary>Rows as Open Dental 25.2 and later write them (ClaimPaymentNum 0, ClaimNumPreAuth set) show on their own preauth only, and never as
		///the EOBs of a claim payment.</summary>
		[TestMethod]
		public void OfficeRows_ShowOnTheRightPreAuth() {
			Patient patient=ImagingDb.CreatePatient("office1");
			long claimNumA=ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-20));
			long claimNumB=ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-10));
			long claimPaymentNum=900000+claimNumA;//Any number: claim payment EOBs are matched by number only.
			InsertRawEob(0,claimNumA,DateTime.Today.AddDays(-5),"20261019_090000_b.pdf");
			InsertRawEob(0,claimNumA,DateTime.Today.AddDays(-6),"20261018_090000_a.pdf");
			InsertRawEob(claimPaymentNum,0,DateTime.Today.AddDays(-6),"20261018_090000_payment.pdf");
			List<EobAttach> listForA=EobAttaches.RefreshForPreAuth(claimNumA);
			CollectionAssert.AreEqual(new[] { "20261018_090000_a.pdf","20261019_090000_b.pdf" },listForA.Select(x => x.FileName).ToArray(),"Oldest first.");
			Assert.IsTrue(listForA.All(x => x.ClaimPaymentNum==0 && x.ClaimNumPreAuth==claimNumA));
			Assert.IsTrue(EobAttaches.ExistsForPreAuth(claimNumA));
			Assert.AreEqual(0,EobAttaches.RefreshForPreAuth(claimNumB).Count,"The other preauth of the patient has none.");
			Assert.IsFalse(EobAttaches.ExistsForPreAuth(claimNumB));
			Assert.AreEqual(0,EobAttaches.Refresh(0).Count,"Preauth rows are not EOBs of an unsaved payment.");
			Assert.IsFalse(EobAttaches.Exists(0));
			Assert.AreEqual("20261018_090000_payment.pdf",EobAttaches.Refresh(claimPaymentNum).Single().FileName);
			Assert.AreEqual(2,EobAttaches.RefreshForOwner(0,claimNumA).Count);
			Assert.AreEqual(1,EobAttaches.RefreshForOwner(claimPaymentNum,0).Count);
			Assert.IsFalse(EobAttaches.ExistsForPreAuth(0));
		}

		[TestMethod]
		public void ScanIntoDatabase_AttachesToThePreAuthAndLogs() {
			Patient patient=ImagingDb.CreatePatient("scan1");
			long claimNum=ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-3));
			EobAttach eobAttach=ImageStore.ImportEobAttach(MakeEobFile(),claimPaymentNum:0,claimNumPreAuth:claimNum);
			EobAttach eobAttachDb=EobAttaches.GetOne(eobAttach.EobAttachNum);
			Assert.AreEqual(claimNum,eobAttachDb.ClaimNumPreAuth);
			Assert.AreEqual(0,eobAttachDb.ClaimPaymentNum);
			Assert.IsTrue(eobAttachDb.FileName.EndsWith("_"+eobAttachDb.EobAttachNum+".pdf"),"Named as today: "+eobAttachDb.FileName);
			Assert.IsFalse(string.IsNullOrEmpty(eobAttachDb.RawBase64),"No A-to-Z folder: the file is in RawBase64.");
			SecurityLog securityLog=ImagingDb.GetLogs(EnumPermType.ImageCreate,patient.PatNum).Single();
			StringAssert.Contains(securityLog.LogText,"Preauth EOB");
			StringAssert.Contains(securityLog.LogText,eobAttachDb.FileName);
		}

		[TestMethod]
		public void ScanIntoAtoZ_FileInEobsFolder_DeletedWithThePreAuth() {
			Patient patient=ImagingDb.CreatePatient("atoz1");
			long claimNum=ImagingDb.CreatePreAuth(patient,DateTime.Today);
			string atoZ=Path.Combine(Path.GetTempPath(),"aimg_atoz_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(atoZ);
			_listPathsToDelete.Add(atoZ);
			ImagingDb.SetStorage(DataStorageType.LocalAtoZ,atoZ);
			EobAttach eobAttach1=ImageStore.ImportEobAttach(MakeEobFile(),0,claimNum);
			EobAttach eobAttach2=ImageStore.ImportEobAttach(MakeEobFile(".jpg"),0,claimNum);
			string pathEob1=Path.Combine(atoZ,"EOBs",eobAttach1.FileName);
			string pathEob2=Path.Combine(atoZ,"EOBs",eobAttach2.FileName);
			Assert.IsTrue(File.Exists(pathEob1),pathEob1);
			Assert.IsTrue(File.Exists(pathEob2),pathEob2);
			Assert.AreEqual("",EobAttaches.GetOne(eobAttach1.EobAttachNum).RawBase64??"","A-to-Z: nothing in RawBase64.");
			Assert.AreEqual(2,ImageStore.DeleteEobAttachesForPreAuth(claimNum));
			Assert.IsFalse(File.Exists(pathEob1));
			Assert.IsFalse(File.Exists(pathEob2));
			Assert.AreEqual(0,EobAttaches.RefreshForPreAuth(claimNum).Count);
			List<SecurityLog> listDeleteLogs=ImagingDb.GetLogs(EnumPermType.ImageDelete,patient.PatNum);
			Assert.AreEqual(2,listDeleteLogs.Count);
			Assert.IsTrue(listDeleteLogs.All(x => x.LogText.Contains("Preauth EOB deleted")));
		}

		///<summary>What every window that deletes a claim removes first (ClaimL.TryDeletePreAuthEobs): the preauth's own EOBs, oldest first. Another
		///preauth keeps its EOBs, and a claim that is not a preauth never takes any, even when a row names its ClaimNum.</summary>
		[TestMethod]
		public void ClaimDelete_TakesOnlyThePreAuthsOwnEobs() {
			Patient patient=ImagingDb.CreatePatient("pdel1");
			Claim claimPreAuthA=Claims.GetClaim(ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-20)));
			Claim claimPreAuthB=Claims.GetClaim(ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-10)));
			Claim claimPreAuthNone=Claims.GetClaim(ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-5)));
			Claim claimPrimary=Claims.GetClaim(Claims.Insert(new Claim { PatNum=patient.PatNum,ClaimType="P",ClaimStatus="S",
				DateSent=DateTime.Today,DateService=DateTime.Today }));
			InsertRawEob(0,claimPreAuthA.ClaimNum,DateTime.Today.AddDays(-2),"del1_a_newer.pdf");
			InsertRawEob(0,claimPreAuthA.ClaimNum,DateTime.Today.AddDays(-9),"del1_a_older.pdf");
			InsertRawEob(0,claimPreAuthB.ClaimNum,DateTime.Today.AddDays(-3),"del1_b.pdf");
			InsertRawEob(0,claimPrimary.ClaimNum,DateTime.Today.AddDays(-1),"del1_not_a_preauth.pdf");
			CollectionAssert.AreEqual(new[] { "del1_a_older.pdf","del1_a_newer.pdf" },
				EobAttaches.GetForClaimDelete(claimPreAuthA).Select(x => x.FileName).ToArray(),"Oldest first.");
			Assert.AreEqual("del1_b.pdf",EobAttaches.GetForClaimDelete(claimPreAuthB).Single().FileName);
			Assert.AreEqual(0,EobAttaches.GetForClaimDelete(claimPreAuthNone).Count);
			Assert.AreEqual(0,EobAttaches.GetForClaimDelete(claimPrimary).Count,"Only a preauth takes EOBs with it.");
		}

		///<summary>Deleting a preauth that has EOBs needs ImageDelete, and its date restriction is checked against the oldest EOB.</summary>
		[TestMethod]
		public void ClaimDelete_NeedsImageDeleteForTheOldestEob() {
			Patient patient=ImagingDb.CreatePatient("pdel2");
			long claimNum=ImagingDb.CreatePreAuth(patient,DateTime.Today.AddDays(-50));
			InsertRawEob(0,claimNum,DateTime.Now.AddDays(-5),"del2_newer.pdf");
			InsertRawEob(0,claimNum,DateTime.Now.AddDays(-40),"del2_older.pdf");
			List<EobAttach> listEobAttaches=EobAttaches.GetForClaimDelete(Claims.GetClaim(claimNum));
			Assert.AreEqual(2,listEobAttaches.Count);
			List<EobAttach> listEobAttachesNewer=listEobAttaches.FindAll(x => x.FileName=="del2_newer.pdf");
			ImagingDb.LogInWith(EnumPermType.ClaimDelete);
			Assert.IsFalse(EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttaches),"ClaimDelete alone does not delete images.");
			Assert.IsTrue(EobAttaches.IsAuthorizedToDeleteWithPreAuth(new List<EobAttach>()),"No EOBs, no ImageDelete needed.");
			ImagingDb.LogInWithDays((EnumPermType.ClaimDelete,0),(EnumPermType.ImageDelete,30));
			Assert.IsFalse(EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttaches),"The oldest EOB is past the 30-day limit.");
			Assert.IsTrue(EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttachesNewer));
			ImagingDb.LogInWithDays((EnumPermType.ClaimDelete,0),(EnumPermType.ImageDelete,60));
			Assert.IsTrue(EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttaches));
			ImagingDb.LogInWith(EnumPermType.ClaimDelete,EnumPermType.ImageDelete);
			Assert.IsTrue(EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttaches),"No date restriction.");
		}

		[TestMethod]
		public void Owner_ExactlyOne_NothingInsertedOtherwise() {
			string path=MakeEobFile();
			int countBefore=PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM eobattach"));
			AssertThrowsArgument(() => ImageStore.ImportEobAttach(path,0,0),"Neither owner.");
			AssertThrowsArgument(() => ImageStore.ImportEobAttach(path,5,7),"Both owners.");
			Assert.AreEqual(countBefore,PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM eobattach")));
		}

		private static void AssertThrowsArgument(Action action,string message) {
			try {
				action();
			}
			catch(ArgumentException) {
				return;
			}
			Assert.Fail("Expected ArgumentException: "+message);
		}

		///<summary>Claim payment EOBs work as before: no ClaimNumPreAuth and, as in 24.3, no security log entry.</summary>
		[TestMethod]
		public void ClaimPaymentEob_Unchanged() {
			Patient patient=ImagingDb.CreatePatient("pay1");
			int countCreateLogs=ImagingDb.GetLogs(EnumPermType.ImageCreate,0).Count;
			long claimPaymentNum=800000+patient.PatNum;
			EobAttach eobAttach=ImageStore.ImportEobAttach(MakeEobFile(),claimPaymentNum);
			EobAttach eobAttachDb=EobAttaches.GetOne(eobAttach.EobAttachNum);
			Assert.AreEqual(claimPaymentNum,eobAttachDb.ClaimPaymentNum);
			Assert.AreEqual(0,eobAttachDb.ClaimNumPreAuth);
			Assert.IsTrue(EobAttaches.Exists(claimPaymentNum));
			Assert.AreEqual(countCreateLogs,ImagingDb.GetLogs(EnumPermType.ImageCreate,0).Count);
			ImageStore.DeleteEobAttach(eobAttachDb);
			Assert.IsFalse(EobAttaches.Exists(claimPaymentNum));
		}
	}
}
