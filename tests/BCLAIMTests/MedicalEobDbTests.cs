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

		///<summary>The main path: the Medical EOB prompt attaches the EOB, then the "Outstanding primary claims" prompt sets the claim to Waiting to Send
		///(and sends or prints it). That prompt reloads the claims without their attachments; saving them used to delete every claimattach row.
		///</summary>
		[TestMethod]
		public void WaitingToSend_KeepsTheAttachedMedicalEobAndEarlierAttachments() {
			BClaimDb.SetStorage(DataStorageType.LocalAtoZ);
			ClaimScenario scenario=ClaimScenario.Create("Z9738");
			(_,ClaimProc claimProcMedical)=CreateReceivedMedicalClaim(scenario);
			Claim claimDental=CreateDentalClaim(scenario,"P","U");
			ClaimAttach claimAttachEarlier=new ClaimAttach {
				ClaimNum=claimDental.ClaimNum,DisplayedFileName="narrative.txt",ActualFileName="earlier_"+BClaimDb.NewSuffix()+".txt" };
			ClaimAttaches.Insert(claimAttachEarlier);
			string sourcePath=Path.Combine(BClaimDb.AtoZPath,"import_"+BClaimDb.NewSuffix()+".txt");
			File.WriteAllText(sourcePath,"Synthetic medical EOB");
			ClaimAttach claimAttachEob=ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,sourcePath,ClaimAttaches.MEDICAL_EOB_DISPLAY_PREFIX+"eob.txt");
			Assert.AreEqual(2,Claims.GetClaim(claimDental.ClaimNum).Attachments.Count);
			//What ClaimL.PromptForPrimaryOrSecondaryClaim does next, for "Change the claim status", "Send primary claim(s) now" and "Print".
			List<Claim> listClaims=Claims.GetPrimaryOrSecondaryClaimsNotReceived(new List<ClaimProc> { claimProcMedical },isSecondaryClaim:false);
			Assert.AreEqual(claimDental.ClaimNum,listClaims.Single().ClaimNum);
			ClaimAttaches.SetClaimsWaitingToSend(listClaims,doSaveToDb:true);
			Claim claimSaved=Claims.GetClaim(claimDental.ClaimNum);
			Assert.AreEqual("W",claimSaved.ClaimStatus);
			CollectionAssert.AreEquivalent(new[] { claimAttachEarlier.ActualFileName,claimAttachEob.ActualFileName },
				claimSaved.Attachments.Select(x => x.ActualFileName).ToArray(),"Both attachments are still on the claim.");
			Assert.AreEqual("EoB,Mail",claimSaved.AttachedFlags);
		}

		///<summary>The primary-to-secondary branch with "recalculate secondary estimates" on: the prompt only sets the status, and
		///Claims.CalculateAndUpdateSecondaries saves the claims. Their attachments are loaded first, so that save keeps them too.</summary>
		[TestMethod]
		public void WaitingToSend_RecalculatedSecondaryClaimKeepsItsAttachments() {
			ClaimScenario scenario=ClaimScenario.Create("Z9739");
			InsPlan planSecondary=BClaimDb.CreatePlan("Synthetic Second Carrier "+BClaimDb.NewSuffix());
			long insSubNumSecondary=InsSubT.CreateInsSub(scenario.Patient_.PatNum,planSecondary.PlanNum).InsSubNum;
			PatPlanT.CreatePatPlan(1,scenario.Patient_.PatNum,scenario.InsSubNumDental);
			PatPlanT.CreatePatPlan(2,scenario.Patient_.PatNum,insSubNumSecondary);
			Claim claimPrimary=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"R",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProcPrimary=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claimPrimary.ClaimNum,
				ClaimProcStatus.Received);
			Claim claimSecondary=BClaimDb.CreateClaim(scenario.Patient_,planSecondary,insSubNumSecondary,"S",EnumClaimMedType.Dental,"U",
				scenario.Procedure_.ProcDate);
			BClaimDb.CreateClaimProc(scenario.Procedure_,planSecondary,insSubNumSecondary,claimSecondary.ClaimNum,ClaimProcStatus.NotReceived);
			ClaimAttach claimAttach=new ClaimAttach {
				ClaimNum=claimSecondary.ClaimNum,DisplayedFileName="primary_eob.txt",ActualFileName="sec_"+BClaimDb.NewSuffix()+".txt" };
			ClaimAttaches.Insert(claimAttach);
			List<Claim> listClaims=Claims.GetPrimaryOrSecondaryClaimsNotReceived(new List<ClaimProc> { claimProcPrimary });
			Assert.AreEqual(claimSecondary.ClaimNum,listClaims.Single().ClaimNum);
			ClaimAttaches.SetClaimsWaitingToSend(listClaims,doSaveToDb:false);
			Assert.AreEqual("W",listClaims[0].ClaimStatus);
			Assert.AreEqual("U",Claims.GetClaim(claimSecondary.ClaimNum).ClaimStatus,"Not saved here; the recalculation saves it.");
			Claims.CalculateAndUpdateSecondaries(listClaims);
			Claim claimSaved=Claims.GetClaim(claimSecondary.ClaimNum);
			Assert.AreEqual("W",claimSaved.ClaimStatus);
			Assert.AreEqual(claimAttach.ActualFileName,claimSaved.Attachments.Single().ActualFileName);
		}

		///<summary>Combined review (2026-11-01). With the medical plan first, a received medical claim recalculates the next insurance's claims (Open
		///Dental 26.1: after any claim but a preauthorization), and the next insurance is the dental primary claim the Medical EOB prompt has just
		///attached the EOB to. When the user picks "Do nothing" (or the prompts are off), the claim is still on hold and the recalculation saves it with
		///the one-argument Claims.Update, which rewrites claimattach from the claim object. The lookup loads no attachments, so the recalculation loads
		///them first; otherwise the EOB and every earlier attachment were deleted.</summary>
		[TestMethod]
		public void ReceivedMedicalClaim_RecalculatedDentalClaimKeepsTheMedicalEob() {
			BClaimDb.SetStorage(DataStorageType.LocalAtoZ);
			ClaimScenario scenario=ClaimScenario.Create("Z9740");
			PatPlanT.CreatePatPlan(1,scenario.Patient_.PatNum,scenario.InsSubNumMedical);
			PatPlanT.CreatePatPlan(2,scenario.Patient_.PatNum,scenario.InsSubNumDental);
			//Medical claims are created with ClaimType "Other" (AccountModules, claim type "Med").
			Claim claimMedical=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanMedical,scenario.InsSubNumMedical,"Other",EnumClaimMedType.Medical,"R",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProcMedical=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanMedical,scenario.InsSubNumMedical,claimMedical.ClaimNum,
				ClaimProcStatus.Received);
			Claim claimDental=CreateDentalClaim(scenario,"P","H");
			ClaimAttach claimAttachEarlier=new ClaimAttach {
				ClaimNum=claimDental.ClaimNum,DisplayedFileName="narrative.txt",ActualFileName="earlier_"+BClaimDb.NewSuffix()+".txt" };
			ClaimAttaches.Insert(claimAttachEarlier);
			string sourcePath=Path.Combine(BClaimDb.AtoZPath,"import_"+BClaimDb.NewSuffix()+".txt");
			File.WriteAllText(sourcePath,"Synthetic medical EOB");
			ClaimAttach claimAttachEob=ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,sourcePath,ClaimAttaches.MEDICAL_EOB_DISPLAY_PREFIX+"eob.txt");
			string[] arrayFileNames=new[] { claimAttachEarlier.ActualFileName,claimAttachEob.ActualFileName };
			//Edit Claim's SaveCleanup after "Do nothing": the recalculation is due and finds the dental primary claim on hold.
			Assert.IsTrue(ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(claimMedical,isReceivedAsNoPay:false,isRecalcPrefOn:true));
			List<Claim> listClaims=Claims.GetNextOrdinalClaimsNotReceived(new List<ClaimProc> { claimProcMedical },includeHoldForInProcess:true);
			Assert.AreEqual(claimDental.ClaimNum,listClaims.Single().ClaimNum);
			Assert.AreEqual(0,listClaims[0].Attachments.Count,"The lookup loads no attachments.");
			Claims.CalculateAndUpdateSecondaries(listClaims);
			Claim claimSaved=Claims.GetClaim(claimDental.ClaimNum);
			Assert.AreEqual(120,claimSaved.ClaimFee,0.001,"The dental claim was recalculated and saved.");
			Assert.AreEqual("H",claimSaved.ClaimStatus);
			CollectionAssert.AreEquivalent(arrayFileNames,claimSaved.Attachments.Select(x => x.ActualFileName).ToArray(),
				"Both attachments are still on the claim.");
			Assert.AreEqual("EoB,Mail",claimSaved.AttachedFlags);
			//The ERA payment, receive with no payment and zero-claims paths recalculate through CalculateAndUpdateSecondariesFromPrimaries.
			Claims.CalculateAndUpdateSecondariesFromPrimaries(new List<Claim> { Claims.GetClaim(claimMedical.ClaimNum) });
			CollectionAssert.AreEquivalent(arrayFileNames,Claims.GetClaim(claimDental.ClaimNum).Attachments.Select(x => x.ActualFileName).ToArray());
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

		private static string GetPrefRowValue(string prefName) {
			return DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
		}

		private static long CountPrefRows(string prefName) {
			return PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName)+"'"));
		}

		private static void DeletePrefRows() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName IN ('"+POut.String(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT_FORK)+"','"
				+POut.String(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT)+"')");
			Cache.Refresh(InvalidType.Prefs);
		}

		///<summary>The setting is a fork-owned preference row: off while it is missing (a 24.3 or adopted Open Dental 25.3.48 database), saving the
		///default writes no row, turning it on inserts the row, and the value survives a cache reload (another workstation).</summary>
		[TestMethod]
		public void Prompt_FollowsTheForkOwnedPreferenceRow() {
			string prefName=ClaimAttaches.GetMedicalEobPrefNameInUse();
			DeletePrefRows();
			try {
				Assert.IsFalse(ClaimAttaches.IsMedicalEobPromptEnabled(),"A missing row reads as off.");
				Assert.IsFalse(ClaimAttaches.SetMedicalEobPromptEnabled(false),"Saving the default changes nothing.");
				Assert.AreEqual(0,CountPrefRows(prefName),"Saving the default writes no row.");
				Assert.IsTrue(ClaimAttaches.SetMedicalEobPromptEnabled(true));
				Assert.AreEqual("1",GetPrefRowValue(prefName));
				Assert.IsTrue(ClaimAttaches.IsMedicalEobPromptEnabled());
				Cache.Refresh(InvalidType.Prefs);
				Assert.IsTrue(ClaimAttaches.IsMedicalEobPromptEnabled(),"Read back from the database.");
				Assert.IsFalse(ClaimAttaches.SetMedicalEobPromptEnabled(true),"Unchanged: no Prefs signal needed.");
				Assert.IsTrue(ClaimAttaches.SetMedicalEobPromptEnabled(false));
				Assert.AreEqual("0",GetPrefRowValue(prefName));
				Assert.AreEqual(1,CountPrefRows(prefName),"Updated in place.");
				Cache.Refresh(InvalidType.Prefs);
				Assert.IsFalse(ClaimAttaches.IsMedicalEobPromptEnabled());
			}
			finally {
				DeletePrefRows();
			}
		}

		///<summary>While Open Dental's name is unconfirmed, the setting is the fork-owned row only: a row with the proposed (guessed) name, which an
		///Open Dental database could hold with another meaning, neither turns the prompt on nor is written.</summary>
		[TestMethod]
		public void Prompt_IgnoresTheUnconfirmedOpenDentalName() {
			if(ClaimAttaches.TryGetMedicalEobPrefName(out _)) {
				Assert.AreEqual(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT,ClaimAttaches.GetMedicalEobPrefNameInUse(),"Registered: that row is used.");
				return;
			}
			Assert.AreEqual(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT_FORK,ClaimAttaches.GetMedicalEobPrefNameInUse());
			DeletePrefRows();
			try {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT)+"','1')");
				Cache.Refresh(InvalidType.Prefs);
				Assert.IsFalse(ClaimAttaches.IsMedicalEobPromptEnabled());
				Assert.IsTrue(ClaimAttaches.SetMedicalEobPromptEnabled(true));
				Assert.AreEqual("1",GetPrefRowValue(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT_FORK));
				Assert.AreEqual("1",GetPrefRowValue(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT),"The guessed row is not touched.");
				ClaimAttaches.SetMedicalEobPromptEnabled(false);
				Assert.AreEqual("1",GetPrefRowValue(ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT),"The guessed row is not touched.");
			}
			finally {
				DeletePrefRows();
			}
		}
	}
}
