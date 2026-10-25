// Database tests for the Medical EOB flow (v25_2-account-medical-eob-prompt): which dental claims the prompt asks about, which EOB files it
// offers, and attaching a file to a dental claim (claim attachment folder, claimattach row, EoB flag, audit entry).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BCLAIMTests {
	[TestClass]
	public class MedicalEobDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BClaimDb.Begin();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(BClaimDb.SkipReason==null) {
				BClaimDb.SetStorage(DataStorageType.InDatabase);
			}
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static void AssertThrowsOD(Func<ClaimAttach> func) {
			try {
				func();
			}
			catch(ODException) {
				return;
			}
			Assert.Fail("Expected an ODException.");
		}

		///<summary>A received medical claim for the scenario's procedure, with its claimproc.</summary>
		private static (Claim claim,ClaimProc claimProc) CreateReceivedMedicalClaim(ClaimScenario scenario) {
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanMedical,scenario.InsSubNumMedical,"P",EnumClaimMedType.Medical,"R",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanMedical,scenario.InsSubNumMedical,claim.ClaimNum,
				ClaimProcStatus.Received);
			return (claim,claimProc);
		}

		private static Claim CreateDentalClaim(ClaimScenario scenario,string claimType,string claimStatus) {
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,claimType,EnumClaimMedType.Dental,claimStatus,
				scenario.Procedure_.ProcDate);
			BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,ClaimProcStatus.NotReceived);
			return claim;
		}

		[TestMethod]
		public void DentalClaims_UnsentAndHoldPrimaryDentalOnly() {
			ClaimScenario scenario=ClaimScenario.Create("Z9731");
			(Claim claimMedical,ClaimProc claimProcMedical)=CreateReceivedMedicalClaim(scenario);
			Claim claimUnsent=CreateDentalClaim(scenario,"P","U");
			Claim claimHold=CreateDentalClaim(scenario,"P","H");
			CreateDentalClaim(scenario,"P","S");//Already sent: not asked about.
			CreateDentalClaim(scenario,"S","U");//Secondary: not asked about.
			List<Claim> listClaims=ClaimAttaches.GetDentalClaimsForMedicalEob(claimMedical,new List<ClaimProc> { claimProcMedical });
			CollectionAssert.AreEquivalent(new[] { claimUnsent.ClaimNum,claimHold.ClaimNum },listClaims.Select(x => x.ClaimNum).ToArray());
		}

		[TestMethod]
		public void DentalClaims_OtherMedicalClaimsAreNotAskedAbout() {
			ClaimScenario scenario=ClaimScenario.Create("Z9732");
			(Claim claimMedical,ClaimProc claimProcMedical)=CreateReceivedMedicalClaim(scenario);
			//An unsent institutional claim on the same procedure.
			Claim claimInstitutional=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanMedical,scenario.InsSubNumMedical,"P",
				EnumClaimMedType.Institutional,"U",scenario.Procedure_.ProcDate);
			BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanMedical,scenario.InsSubNumMedical,claimInstitutional.ClaimNum,
				ClaimProcStatus.NotReceived);
			Assert.AreEqual(0,ClaimAttaches.GetDentalClaimsForMedicalEob(claimMedical,new List<ClaimProc> { claimProcMedical }).Count);
		}

		[TestMethod]
		public void MedicalEobs_FromTheMedicalClaimsPayment() {
			ClaimScenario scenario=ClaimScenario.Create("Z9733");
			(Claim claimMedical,ClaimProc claimProcMedical)=CreateReceivedMedicalClaim(scenario);
			Assert.AreEqual(0,ClaimAttaches.GetMedicalEobAttaches(claimMedical.ClaimNum).Count,"No payment yet.");
			ClaimPayment claimPayment=ClaimPaymentT.CreateClaimPayment(DateTime.Today,70,"CHK1","","","Synthetic Medical Carrier",DateTime.Today,0,0,0);
			ClaimPaymentT.AttachClaimProcsToClaimPayment(new List<ClaimProc> { claimProcMedical },claimPayment.ClaimPaymentNum);
			EobAttach eobAttach1=EobAttachT.CreateEobAttach(DateTime.Now.AddMinutes(-5),claimPayment.ClaimPaymentNum,"eob_a.pdf");
			EobAttach eobAttach2=EobAttachT.CreateEobAttach(DateTime.Now,claimPayment.ClaimPaymentNum,"eob_b.pdf");
			List<EobAttach> listEobAttaches=ClaimAttaches.GetMedicalEobAttaches(claimMedical.ClaimNum);
			CollectionAssert.AreEqual(new[] { eobAttach1.EobAttachNum,eobAttach2.EobAttachNum },listEobAttaches.Select(x => x.EobAttachNum).ToArray());
		}

		[TestMethod]
		public void Attach_FileCopiedFlaggedAndLogged() {
			BClaimDb.SetStorage(DataStorageType.LocalAtoZ);
			ClaimScenario scenario=ClaimScenario.Create("Z9734");
			Claim claimDental=CreateDentalClaim(scenario,"P","U");
			string sourcePath=Path.Combine(BClaimDb.AtoZPath,"import_"+BClaimDb.NewSuffix()+".txt");
			File.WriteAllText(sourcePath,"Synthetic medical EOB");
			ClaimAttach claimAttach=ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,sourcePath,ClaimAttaches.MEDICAL_EOB_DISPLAY_PREFIX+"eob.txt");
			Claim claimSaved=Claims.GetClaim(claimDental.ClaimNum);
			Assert.AreEqual(1,claimSaved.Attachments.Count);
			Assert.AreEqual("MedicalEOB_eob.txt",claimSaved.Attachments[0].DisplayedFileName);
			Assert.AreEqual(claimAttach.ActualFileName,claimSaved.Attachments[0].ActualFileName);
			string copiedPath=Path.Combine(EmailAttaches.GetAttachPath(),claimAttach.ActualFileName);
			Assert.IsTrue(File.Exists(copiedPath),"The file is copied into the claim attachment folder.");
			Assert.AreEqual("Synthetic medical EOB",File.ReadAllText(copiedPath));
			StringAssert.StartsWith(copiedPath,BClaimDb.AtoZPath);
			Assert.AreEqual("EoB,Mail",claimSaved.AttachedFlags,"The EoB paperwork flag is added and Mail is kept.");
			Assert.AreEqual(1,BClaimDb.GetLogs(EnumPermType.ClaimEdit,claimDental.ClaimNum).Count(x => x.LogText.Contains("Medical EOB attached")));
			//A second file keeps the first one.
			ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,sourcePath,"second.txt");
			claimSaved=Claims.GetClaim(claimDental.ClaimNum);
			Assert.AreEqual(2,claimSaved.Attachments.Count);
			Assert.AreEqual("EoB,Mail",claimSaved.AttachedFlags);
		}

		[TestMethod]
		public void Attach_MedicalPaymentEobFile() {
			BClaimDb.SetStorage(DataStorageType.LocalAtoZ);
			ClaimScenario scenario=ClaimScenario.Create("Z9735");
			(Claim claimMedical,ClaimProc claimProcMedical)=CreateReceivedMedicalClaim(scenario);
			Claim claimDental=CreateDentalClaim(scenario,"P","H");
			ClaimPayment claimPayment=ClaimPaymentT.CreateClaimPayment(DateTime.Today,70,"CHK2","","","Synthetic Medical Carrier",DateTime.Today,0,0,0);
			ClaimPaymentT.AttachClaimProcsToClaimPayment(new List<ClaimProc> { claimProcMedical },claimPayment.ClaimPaymentNum);
			string eobFolder=ImageStore.GetEobFolder();
			Directory.CreateDirectory(eobFolder);
			string eobFileName="eob_"+BClaimDb.NewSuffix()+".txt";
			File.WriteAllText(Path.Combine(eobFolder,eobFileName),"Synthetic EOB image");
			EobAttachT.CreateEobAttach(DateTime.Now,claimPayment.ClaimPaymentNum,eobFileName);
			EobAttach eobAttach=ClaimAttaches.GetMedicalEobAttaches(claimMedical.ClaimNum).Single();
			ClaimAttach claimAttach=ClaimAttaches.AttachMedicalEob(claimDental.ClaimNum,eobAttach);
			Assert.AreEqual(ClaimAttaches.MEDICAL_EOB_DISPLAY_PREFIX+eobFileName,claimAttach.DisplayedFileName);
			Assert.AreEqual("Synthetic EOB image",File.ReadAllText(Path.Combine(EmailAttaches.GetAttachPath(),claimAttach.ActualFileName)));
			Assert.AreEqual(1,Claims.GetClaim(claimDental.ClaimNum).Attachments.Count);
		}

		[TestMethod]
		public void Attach_NeedsTheAtoZFolder() {
			BClaimDb.SetStorage(DataStorageType.InDatabase);
			ClaimScenario scenario=ClaimScenario.Create("Z9736");
			Claim claimDental=CreateDentalClaim(scenario,"P","U");
			string sourcePath=Path.Combine(Path.GetTempPath(),"bclaim_"+BClaimDb.NewSuffix()+".txt");
			File.WriteAllText(sourcePath,"x");
			try {
				AssertThrowsOD(() => ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,sourcePath,"eob.txt"));
				Assert.AreEqual(0,Claims.GetClaim(claimDental.ClaimNum).Attachments.Count);
			}
			finally {
				File.Delete(sourcePath);
			}
		}

		[TestMethod]
		public void Attach_MissingFileOrClaimThrows() {
			BClaimDb.SetStorage(DataStorageType.LocalAtoZ);
			ClaimScenario scenario=ClaimScenario.Create("Z9737");
			Claim claimDental=CreateDentalClaim(scenario,"P","U");
			AssertThrowsOD(() => ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,Path.Combine(BClaimDb.AtoZPath,"none.txt"),""));
			string sourcePath=Path.Combine(BClaimDb.AtoZPath,"import_"+BClaimDb.NewSuffix()+".txt");
			File.WriteAllText(sourcePath,"x");
			AssertThrowsOD(() => ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum+1000000,sourcePath,""));
		}

		[TestMethod]
		public void Prompt_OffWhileThePreferenceIsNotRegistered() {
			if(ClaimAttaches.TryGetMedicalEobPrefName(out PrefName prefName)) {
				//Registered by the registry owner: the preference row decides.
				Assert.AreEqual(PrefC.GetBoolSilent(prefName,false),ClaimAttaches.IsMedicalEobPromptEnabled());
				return;
			}
			Assert.IsFalse(ClaimAttaches.IsMedicalEobPromptEnabled());
			//Even a preference row with the proposed name does not turn it on: the name has to be confirmed and registered first.
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) SELECT '"+ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT+"','1' FROM DUAL "
				+"WHERE NOT EXISTS(SELECT 1 FROM preference WHERE PrefName='"+ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT+"')");
			try {
				Cache.Refresh(InvalidType.Prefs);
				Assert.IsFalse(ClaimAttaches.IsMedicalEobPromptEnabled());
			}
			finally {
				DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT+"'");
				Cache.Refresh(InvalidType.Prefs);
			}
		}
	}
}
