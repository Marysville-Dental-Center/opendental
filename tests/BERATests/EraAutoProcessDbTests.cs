// Automatic ERA processing on a scratch database: the 25.1 auto split of claims with denial CARCs, the 25.2 CARC entry logic, the fork-owned
// preference row, and (24.4) what payment entry does with a claim that does not match the ERA. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BERATests {
	[TestClass]
	public class EraAutoProcessDbTests {
		private static readonly DateTime _dateService=new DateTime(2026,9,1);
		private static int _countCases=0;

		private class EraCase {
			public Patient Patient;
			public List<Procedure> ListProcs;
			public Claim Claim;
			public Etrans Etrans;
			public string MessageText;
			public X835 X835;
			public List<Etrans835Attach> ListAttaches;
			///<summary>The claim of the secondary insurance, for cases made with one (CreateClaimWithSecondary). Null otherwise.</summary>
			public Claim ClaimSecondary;
		}

		[TestInitialize]
		public void Init() {
			EraDb.Begin();
			Prefs.UpdateInt(PrefName.EraAutomationBehavior,(int)EraAutomationMode.SemiAutomatic);
			Prefs.UpdateString(PrefName.EraNoAutoProcessCarcCodes,"97,16");
			EraAutoSplit.SetEnabled(true);
		}

		///<summary>A patient with primary insurance and a sent claim for the given completed procedures (made-up Z codes), each claimproc billed at
		///the procedure fee.</summary>
		private static EraCase CreateClaim(params (string Code,double Fee)[] arrayProcs) {
			return CreateCase(null,arrayProcs);
		}

		///<summary>As CreateClaim, and the patient also has secondary insurance with a claim on Hold Until Prior Received (EraCase.ClaimSecondary) for
		///the procedures with these indexes, as Create Claim leaves a secondary claim while the primary claim is out.</summary>
		private static EraCase CreateClaimWithSecondary(int[] arrayProcIndexesSecondary,params (string Code,double Fee)[] arrayProcs) {
			return CreateCase(arrayProcIndexesSecondary,arrayProcs);
		}

		private static EraCase CreateCase(int[] arrayProcIndexesSecondary,(string Code,double Fee)[] arrayProcs) {
			_countCases++;
			string suffix=new string((char)('A'+(_countCases%26)),1)+new string((char)('A'+(_countCases/26%26)),1);
			EraCase eraCase=new EraCase();
			eraCase.Patient=PatientT.CreatePatient(suffix,lName:"Bera",fName:"Synth");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(eraCase.Patient,"Synthetic Payer "+suffix);
			if(arrayProcIndexesSecondary!=null) {
				insuranceInfo.AddInsurance(eraCase.Patient,"Synthetic Second Payer "+suffix,ordinal:2);
			}
			eraCase.ListProcs=arrayProcs.Select(x => ProcedureT.CreateProcedure(eraCase.Patient,x.Code,ProcStat.C,"",x.Fee,_dateService)).ToList();
			Claim claim=ClaimT.CreateClaim(eraCase.ListProcs,insuranceInfo,"P","BERA"+_countCases+suffix);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claim.ClaimNum);
			foreach(ClaimProc claimProc in listClaimProcs) {
				claimProc.FeeBilled=eraCase.ListProcs.First(x => x.ProcNum==claimProc.ProcNum).ProcFee;
				ClaimProcs.Update(claimProc);
			}
			claim.ClaimFee=listClaimProcs.Sum(x => x.FeeBilled);
			claim.ClaimStatus="S";
			Claims.Update(claim);
			eraCase.Claim=Claims.GetClaim(claim.ClaimNum);
			if(arrayProcIndexesSecondary!=null) {
				List<Procedure> listProcsSecondary=arrayProcIndexesSecondary.Select(x => eraCase.ListProcs[x]).ToList();
				Claim claimSecondary=ClaimT.CreateClaim(listProcsSecondary,insuranceInfo,"S","BERA"+_countCases+suffix+"S");
				claimSecondary.ClaimStatus="H";
				Claims.Update(claimSecondary);
				eraCase.ClaimSecondary=Claims.GetClaim(claimSecondary.ClaimNum);
			}
			return eraCase;
		}

		///<summary>Imports a synthetic 835 for the case's claim and attaches its CLP to the claim, as matching would.</summary>
		private static void AddEra(EraCase eraCase,List<Era835Line> listLines,List<(string,string,decimal)> listClaimAdjs=null) {
			eraCase.MessageText=Era835Builder.Build(eraCase.Claim.ClaimIdentifier,eraCase.Patient.LName,eraCase.Patient.FName,_dateService,listLines,listClaimAdjs);
			eraCase.Etrans=EtransT.Insert835Etrans(eraCase.MessageText,DateTime.Now);
			List<ODTuple<Patient,long>> listPats=new List<ODTuple<Patient,long>> { new ODTuple<Patient,long>(eraCase.Patient,eraCase.Claim.ClaimNum) };
			eraCase.X835=EtransT.Construct835(eraCase.Etrans,eraCase.MessageText,listPats,out eraCase.ListAttaches);
		}

		private static long ProcNum(EraCase eraCase,int index) {
			return eraCase.ListProcs[index].ProcNum;
		}

		///<summary>Z0120 $60 paid $48 (CO-45 $12) and Z0220 $40 denied with CO-97.</summary>
		private static EraCase CreateOnePaidOneDenied() {
			EraCase eraCase=CreateClaim(("Z0120",60),("Z0220",40));
			AddEra(eraCase,new List<Era835Line> {
				new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m)),
				new Era835Line("Z0220",40,0,ProcNum(eraCase,1),("CO","97",40m)),
			});
			return eraCase;
		}

		private static ClaimProc GetClaimProcForProc(long procNum) {
			return ClaimProcs.GetForProcs(new List<long> { procNum }).Single(x => x.ClaimNum!=0);
		}

		[TestMethod]
		public void AutoSplit_SomeProcsDenied_SplitsThemOffAndProcessesTheRest() {
			EraCase eraCase=CreateOnePaidOneDenied();
			long claimNumOriginal=eraCase.Claim.ClaimNum;
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.ListClaimErrors.Count,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.CountClaimsProcessed);
			Assert.IsTrue(result.IsPaymentFinalized,result.PaymentFinalizationError);
			//The paid procedure stays on the original claim, which is received.
			Claim claimOriginal=Claims.GetClaim(claimNumOriginal);
			Assert.AreEqual("R",claimOriginal.ClaimStatus);
			Assert.AreEqual(60,claimOriginal.ClaimFee,0.001);
			Assert.AreEqual(48,claimOriginal.InsPayAmt,0.001);
			ClaimProc claimProcPaid=GetClaimProcForProc(ProcNum(eraCase,0));
			Assert.AreEqual(claimNumOriginal,claimProcPaid.ClaimNum);
			Assert.AreEqual(ClaimProcStatus.Received,claimProcPaid.Status);
			Assert.AreEqual(48,claimProcPaid.InsPayAmt,0.001);
			Assert.AreNotEqual(0L,claimProcPaid.ClaimPaymentNum);
			//The denied procedure is on a new, unsent claim, waiting to be corrected and sent again.
			ClaimProc claimProcDenied=GetClaimProcForProc(ProcNum(eraCase,1));
			Assert.AreNotEqual(claimNumOriginal,claimProcDenied.ClaimNum);
			Assert.AreEqual(ClaimProcStatus.NotReceived,claimProcDenied.Status);
			Assert.AreEqual(0,claimProcDenied.InsPayAmt,0.001);
			Claim claimSplit=Claims.GetClaim(claimProcDenied.ClaimNum);
			Assert.AreEqual("U",claimSplit.ClaimStatus);
			Assert.AreEqual(40,claimSplit.ClaimFee,0.001);
			Assert.AreEqual(eraCase.Patient.PatNum,claimSplit.PatNum);
			Assert.AreEqual(claimOriginal.PlanNum,claimSplit.PlanNum);
			//The message and the note name both claims and the matching entry.
			Assert.AreEqual(1,result.ListAutoSplitNotes.Count);
			string note=result.ListAutoSplitNotes[0];
			StringAssert.Contains(note,"#"+claimNumOriginal);
			StringAssert.Contains(note,"#"+claimSplit.ClaimNum);
			StringAssert.Contains(note,"CO-97 on procedure Z0220");
			string message=EraAutomationResult.CreateMessage(new List<EraAutomationResult> { result },isForSingleEra:true);
			StringAssert.Contains(message,"Claims Split Automatically: 1");
			StringAssert.Contains(message,note);
			StringAssert.StartsWith(eraCase.X835.EtransSource.Note,note);
			//Audit entry for the original claim.
			string countLogs=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.ClaimEdit+" AND FKey="+claimNumOriginal
				+" AND LogText LIKE '%split automatically%'");
			Assert.AreEqual("1",countLogs);
		}

		[TestMethod]
		public void AutoSplit_EraStillPointsAtTheOriginalClaimAndShowsProcessed() {
			EraCase eraCase=CreateOnePaidOneDenied();
			long claimNumOriginal=eraCase.Claim.ClaimNum;
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(1,result.ListAutoSplitNotes.Count);
			List<Etrans835Attach> listAttaches=Etrans835Attaches.GetForEtrans(eraCase.Etrans.EtransNum);
			Assert.AreEqual(2,listAttaches.Count);
			Assert.AreEqual(claimNumOriginal,listAttaches[0].ClaimNum,"The original attach must sort first (DateTimeEntry).");
			Assert.IsTrue(listAttaches[1].DateTimeEntry>listAttaches[0].DateTimeEntry);
			X835 x835Reloaded=new X835(eraCase.Etrans,eraCase.MessageText,eraCase.Etrans.TranSetId835,listAttaches);
			Assert.AreEqual(claimNumOriginal,x835Reloaded.ListClaimsPaid[0].ClaimNum);
			x835Reloaded.RefreshAttachesAndClaimProcsFromDb(out List<Etrans835Attach> listAttachesDb,out List<Hx835_ShortClaimProc> listShortClaimProcs);
			Assert.IsTrue(x835Reloaded.ListClaimsPaid[0].IsProcessed(listShortClaimProcs,listAttachesDb));
		}

		///<summary>Integration with M2-ERA (26.1 claimproc-to-ERA link): the claimprocs automatic processing posts after an auto split remember the ERA
		///claim they came from (the original claim's etrans835attach row); the claimproc split off to the new claim stays Not Received, without a link.
		///</summary>
		[TestMethod]
		public void AutoSplit_PostedClaimProcsAreLinkedToTheEraClaim() {
			EraCase eraCase=CreateOnePaidOneDenied();
			long claimNumOriginal=eraCase.Claim.ClaimNum;
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(1,result.CountClaimsProcessed,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.ListAutoSplitNotes.Count);
			Etrans835Attach attachOriginal=Etrans835Attaches.GetForEtrans(eraCase.Etrans.EtransNum).First(x => x.ClaimNum==claimNumOriginal);
			ClaimProc claimProcPaid=GetClaimProcForProc(ProcNum(eraCase,0));
			Assert.AreEqual(ClaimProcStatus.Received,claimProcPaid.Status);
			Assert.AreEqual(attachOriginal.Etrans835AttachNum,claimProcPaid.Etrans835AttachNum,"The paid claimproc points at the ERA claim it was entered from.");
			ClaimProc claimProcDenied=GetClaimProcForProc(ProcNum(eraCase,1));
			Assert.AreNotEqual(claimNumOriginal,claimProcDenied.ClaimNum);
			Assert.AreEqual(ClaimProcStatus.NotReceived,claimProcDenied.Status);
			Assert.AreEqual(0L,claimProcDenied.Etrans835AttachNum,"Nothing was entered on the split claim.");
		}

		///<summary>The procedure's claimproc on a claim of the case's primary plan.</summary>
		private static ClaimProc GetPrimaryClaimProcForProc(EraCase eraCase,long procNum) {
			return ClaimProcs.GetForProcs(new List<long> { procNum }).Single(x => x.ClaimNum!=0 && x.PlanNum==eraCase.Claim.PlanNum);
		}

		///<summary>Combined review (2026-11-01). After an auto split, automatic processing moved the claim of the next insurance to Waiting to Send,
		///although that claim still held the denied procedure, which the split had moved to a new unsent primary claim: a secondary claim queued with
		///a line the primary insurance has not adjudicated (the "bogus" secondary claim X837_5010 describes). It now stays on Hold Until Prior
		///Received, with a note, and the normal next-insurance lookup releases it once the split claim is received.</summary>
		[TestMethod]
		public void AutoSplit_SecondaryClaimWithTheDeniedProcStaysOnHold() {
			EraCase eraCase=CreateClaimWithSecondary(new[] { 0,1 },("Z0120",60),("Z0220",40));
			AddEra(eraCase,new List<Era835Line> {
				new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m)),
				new Era835Line("Z0220",40,0,ProcNum(eraCase,1),("CO","97",40m)),
			});
			Assert.AreEqual("H",eraCase.ClaimSecondary.ClaimStatus);
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.ListClaimErrors.Count,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.CountClaimsProcessed);
			Assert.AreEqual("R",Claims.GetClaim(eraCase.Claim.ClaimNum).ClaimStatus);
			long claimNumSplit=GetPrimaryClaimProcForProc(eraCase,ProcNum(eraCase,1)).ClaimNum;
			Assert.AreNotEqual(eraCase.Claim.ClaimNum,claimNumSplit,"The denied procedure moved to a new claim.");
			Assert.AreEqual("U",Claims.GetClaim(claimNumSplit).ClaimStatus);
			Assert.AreEqual("H",Claims.GetClaim(eraCase.ClaimSecondary.ClaimNum).ClaimStatus,
				"The secondary claim holds the denied procedure, whose primary claim is not even sent: it stays on hold.");
			Assert.AreEqual(2,result.ListAutoSplitNotes.Count,string.Join("\r\n",result.ListAutoSplitNotes));
			string note=result.ListAutoSplitNotes[1];
			StringAssert.Contains(note,"#"+eraCase.ClaimSecondary.ClaimNum);
			StringAssert.Contains(note,"#"+claimNumSplit);
			StringAssert.Contains(note,"left on hold");
			StringAssert.Contains(EraAutomationResult.CreateMessage(new List<EraAutomationResult> { result },isForSingleEra:true),note);
			//Once the split claim is received, the next-insurance lookup offers the secondary claim as usual.
			Claim claimSplitOld=Claims.GetClaim(claimNumSplit);
			Claim claimSplit=claimSplitOld.Copy();
			claimSplit.ClaimStatus="R";
			Claims.Update(claimSplit,claimSplitOld);
			List<ClaimProc> listClaimProcsSplit=ClaimProcs.RefreshForClaim(claimNumSplit);
			foreach(ClaimProc claimProc in listClaimProcsSplit) {
				ClaimProc claimProcOld=claimProc.Copy();
				claimProc.Status=ClaimProcStatus.Received;
				ClaimProcs.Update(claimProc,claimProcOld);
			}
			CollectionAssert.AreEqual(new List<long> { eraCase.ClaimSecondary.ClaimNum },
				Claims.GetPrimaryOrSecondaryClaimsNotReceived(ClaimProcs.RefreshForClaim(claimNumSplit)).Select(x => x.ClaimNum).ToList());
		}

		///<summary>The secondary claim of the paid procedure alone has nothing to wait for: automatic processing still moves it to Waiting to Send.
		///</summary>
		[TestMethod]
		public void AutoSplit_SecondaryClaimOfThePaidProcOnly_IsWaitingToSend() {
			EraCase eraCase=CreateClaimWithSecondary(new[] { 0 },("Z0120",60),("Z0220",40));
			AddEra(eraCase,new List<Era835Line> {
				new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m)),
				new Era835Line("Z0220",40,0,ProcNum(eraCase,1),("CO","97",40m)),
			});
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(1,result.CountClaimsProcessed,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.ListAutoSplitNotes.Count,"Only the split note.");
			Assert.AreEqual("W",Claims.GetClaim(eraCase.ClaimSecondary.ClaimNum).ClaimStatus);
		}

		[TestMethod]
		public void AutoSplit_ImportPath_EtransNoteNamesTheNewClaim() {
			EraCase eraCase=CreateOnePaidOneDenied();
			List<EraAutomationResult> listResults=Etranss.TryAutoProcessEras(new List<Etrans> { eraCase.Etrans },eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(1,listResults.Single().ListAutoSplitNotes.Count);
			Etrans etrans=Etranss.GetEtrans(eraCase.Etrans.EtransNum);
			StringAssert.StartsWith(etrans.Note,"Automatically received on");
			StringAssert.Contains(etrans.Note,listResults.Single().ListAutoSplitNotes[0]);
			Assert.AreEqual("Recd",etrans.AckCode);
		}

		[TestMethod]
		public void OptionOff_ClaimHeldBackAsIn243() {
			EraAutoSplit.SetEnabled(false);
			EraCase eraCase=CreateOnePaidOneDenied();
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.CountClaimsProcessed);
			Assert.AreEqual(1,result.ListClaimErrors.Count);
			StringAssert.Contains(result.ListClaimErrors[0],"CO-97 on procedure Z0220");
			Assert.IsFalse(result.ListClaimErrors[0].Contains("Auto split"));
			Assert.AreEqual(0,result.ListAutoSplitNotes.Count);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,1)).ClaimNum);
			Assert.AreEqual("S",Claims.GetClaim(eraCase.Claim.ClaimNum).ClaimStatus);
		}

		[TestMethod]
		public void EveryProcDenied_NoSplit_ReasonGiven() {
			EraCase eraCase=CreateClaim(("Z0220",40),("Z0230",30));
			AddEra(eraCase,new List<Era835Line> {
				new Era835Line("Z0220",40,0,ProcNum(eraCase,0),("CO","97",40m)),
				new Era835Line("Z0230",30,0,ProcNum(eraCase,1),("CO","16",30m)),
			});
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.CountClaimsProcessed);
			StringAssert.Contains(result.ListClaimErrors.Single(),"every procedure");
			Assert.AreEqual(0,result.ListAutoSplitNotes.Count);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,0)).ClaimNum);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,1)).ClaimNum);
		}

		[TestMethod]
		public void ClaimLevelCarc_NoSplit_ReasonGiven() {
			EraCase eraCase=CreateClaim(("Z0120",60),("Z0220",40));
			AddEra(eraCase,new List<Era835Line> {
					new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m)),
					new Era835Line("Z0220",40,0,ProcNum(eraCase,1),("CO","97",30m)),
				},
				new List<(string,string,decimal)> { ("CO","16",10m) });
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.CountClaimsProcessed);
			StringAssert.Contains(result.ListClaimErrors.Single(),"CO-16 on the claim");
			StringAssert.Contains(result.ListClaimErrors.Single(),"on the claim itself");
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,1)).ClaimNum);
		}

		[TestMethod]
		public void UnlistedClaimLevelAdjustment_RestWouldNeedByTotal_NothingIsSplit() {
			//OA-23 $5 on the claim itself is not listed, but it makes the claim payment ($43) differ from the paid line ($48), so payment entry of the
			//rest of the claim would need a By Total row, which automatic processing refuses. The split must not happen without its payment.
			EraCase eraCase=CreateClaim(("Z0120",60),("Z0220",40));
			AddEra(eraCase,new List<Era835Line> {
					new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m)),
					new Era835Line("Z0220",40,0,ProcNum(eraCase,1),("CO","97",40m)),
				},
				new List<(string,string,decimal)> { ("OA","23",5m) });
			Assert.AreEqual(43m,eraCase.X835.ListClaimsPaid.Single().InsPaid);
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.CountClaimsProcessed);
			Assert.AreEqual(0,result.ListAutoSplitNotes.Count);
			string error=result.ListClaimErrors.Single();
			StringAssert.Contains(error,"CO-97 on procedure Z0220");
			StringAssert.Contains(error,"Auto split was not possible: the rest of the claim would need a By Total payment.");
			Assert.AreEqual(1,Etrans835Attaches.GetForEtrans(eraCase.Etrans.EtransNum).Count);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,0)).ClaimNum);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,1)).ClaimNum);
			Assert.AreEqual(ClaimProcStatus.NotReceived,GetClaimProcForProc(ProcNum(eraCase,0)).Status);
			Assert.AreEqual("S",Claims.GetClaim(eraCase.Claim.ClaimNum).ClaimStatus);
			Assert.AreEqual(1,Claims.Refresh(eraCase.Patient.PatNum).Count,"No new claim.");
			Assert.IsFalse((eraCase.X835.EtransSource.Note??"").Contains("Auto split"));
			string countLogs=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.ClaimEdit+" AND FKey="+eraCase.Claim.ClaimNum
				+" AND LogText LIKE '%split automatically%'");
			Assert.AreEqual("0",countLogs);
		}

		[TestMethod]
		public void CanAutoPostWriteOff_FollowsEraAutoPostWriteOff() {
			int valueOld=PrefC.GetInt(PrefName.EraAutoPostWriteOff);
			try {
				Hx835_Claim claimPaidPrimary=new Hx835_Claim { CodeClp02="1" };//Processed as Primary
				Hx835_Claim claimPaidSecondary=new Hx835_Claim { CodeClp02="2" };//Processed as Secondary
				Claim claimPrimary=new Claim { ClaimType="P" };
				Claim claimSecondary=new Claim { ClaimType="S" };
				Prefs.UpdateInt(PrefName.EraAutoPostWriteOff,(int)EnumEraAutoPostWriteOff.Always);
				Assert.IsTrue(EraAutoSplit.CanAutoPostWriteOff(claimPaidSecondary,claimSecondary));
				Prefs.UpdateInt(PrefName.EraAutoPostWriteOff,(int)EnumEraAutoPostWriteOff.PriFromERA);
				Assert.IsTrue(EraAutoSplit.CanAutoPostWriteOff(claimPaidPrimary,claimSecondary));
				Assert.IsFalse(EraAutoSplit.CanAutoPostWriteOff(claimPaidSecondary,claimPrimary));
				Prefs.UpdateInt(PrefName.EraAutoPostWriteOff,(int)EnumEraAutoPostWriteOff.PriFromPlan);
				Assert.IsTrue(EraAutoSplit.CanAutoPostWriteOff(claimPaidSecondary,claimPrimary));
				Assert.IsFalse(EraAutoSplit.CanAutoPostWriteOff(claimPaidPrimary,claimSecondary));
			}
			finally {
				Prefs.UpdateInt(PrefName.EraAutoPostWriteOff,valueOld);
			}
		}

		[TestMethod]
		public void QualifiedEntryOfAnotherGroup_DoesNotHoldTheClaim() {
			Prefs.UpdateString(PrefName.EraNoAutoProcessCarcCodes,"PR-97");
			EraCase eraCase=CreateOnePaidOneDenied();
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.ListClaimErrors.Count,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.CountClaimsProcessed);
			Assert.AreEqual(0,result.ListAutoSplitNotes.Count);
			Assert.AreEqual("R",Claims.GetClaim(eraCase.Claim.ClaimNum).ClaimStatus);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,1)).ClaimNum);
		}

		[TestMethod]
		public void ZeroAmountListedCode_DoesNotHoldTheClaim() {
			EraCase eraCase=CreateClaim(("Z0120",60));
			AddEra(eraCase,new List<Era835Line> { new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m),("CO","97",0m)) });
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.ListClaimErrors.Count,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.CountClaimsProcessed);
		}

		[TestMethod]
		public void OtherChecksFail_NothingIsSplit() {
			Prefs.UpdateInt(PrefName.EraAutomationBehavior,(int)EraAutomationMode.ReviewAll);
			EraCase eraCase=CreateOnePaidOneDenied();
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraCase.X835,eraCase.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.CountClaimsProcessed);
			string error=result.ListClaimErrors.Single();
			StringAssert.Contains(error,"CO-97 on procedure Z0220");
			StringAssert.Contains(error,"not set to allow automated ERA processing");
			Assert.AreEqual(0,result.ListAutoSplitNotes.Count);
			Assert.AreEqual(eraCase.Claim.ClaimNum,GetClaimProcForProc(ProcNum(eraCase,1)).ClaimNum);
			Assert.AreEqual(1,Etrans835Attaches.GetForEtrans(eraCase.Etrans.EtransNum).Count);
		}

		[TestMethod]
		public void AutoSplitPreference_ForkRowWrittenOnFirstChange() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+EraAutoSplit.PREF_NAME+"'");
			Prefs.RefreshCache();
			Assert.IsFalse(EraAutoSplit.IsEnabled(),"A missing row reads as off.");
			Assert.IsFalse(EraAutoSplit.SetEnabled(false));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+EraAutoSplit.PREF_NAME+"'"));
			Assert.IsTrue(EraAutoSplit.SetEnabled(true));
			Assert.IsFalse(EraAutoSplit.SetEnabled(true));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+EraAutoSplit.PREF_NAME+"'"));
			Prefs.RefreshCache();
			Assert.IsTrue(EraAutoSplit.IsEnabled());
			Assert.IsTrue(EraAutoSplit.SetEnabled(false));
			Prefs.RefreshCache();
			Assert.IsFalse(EraAutoSplit.IsEnabled());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+EraAutoSplit.PREF_NAME+"'"));
			Assert.IsFalse(Enum.GetNames(typeof(PrefName)).Contains(EraAutoSplit.PREF_NAME),"The fork row is not a PrefName (registries stay foundation-owned).");
		}

		[TestMethod]
		public void MismatchedClaim_ManualEntryPutsUnmatchedLinesOnATotalRow_AutomaticRefuses() {
			//24.4: the user may attach an ERA claim to a claim that differs from it. Here the ERA reports Z0330 where the claim has Z0220.
			EraCase eraCase=CreateClaim(("Z0120",60),("Z0220",40));
			AddEra(eraCase,new List<Era835Line> {
				new Era835Line("Z0120",60,48,ProcNum(eraCase,0),("CO","45",12m)),
				new Era835Line("Z0330",40,30,0,("CO","45",10m)),
			});
			Hx835_Claim claimPaid=eraCase.X835.ListClaimsPaid.Single();
			//Automatic processing never posts an unmatched line.
			EraAutomationResult result=new EraAutomationResult();
			List<ClaimProc> listClaimProcsAuto=ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum);
			Assert.IsFalse(Etranss.TryImportEraClaimData(eraCase.X835,claimPaid,eraCase.Claim,eraCase.Patient,true,listClaimProcsAuto,0,result));
			StringAssert.Contains(result.ListClaimErrors.Single(),"could not be matched to a procedure on the claim");
			//Manual entry goes on: the matched procedure gets its payment, the unmatched one is left for the user, and the unmatched ERA line's
			//payment lands on a By Total row that the user allocates.
			List<ClaimProc> listClaimProcsManual=ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum);
			Assert.IsTrue(Etranss.TryImportEraClaimData(eraCase.X835,claimPaid,eraCase.Claim,eraCase.Patient,false,listClaimProcsManual,0));
			ClaimProc claimProcByTotal=listClaimProcsManual.Single(x => x.ProcNum==0);
			Assert.AreEqual(30,claimProcByTotal.InsPayAmt,0.001);
			Assert.AreEqual(48,listClaimProcsManual.Single(x => x.ProcNum==ProcNum(eraCase,0)).InsPayAmt,0.001);
			Assert.AreEqual(0,listClaimProcsManual.Single(x => x.ProcNum==ProcNum(eraCase,1)).InsPayAmt,0.001);
			//Nothing was saved by manual entry; the payment window does that.
			Assert.AreEqual("S",Claims.GetClaim(eraCase.Claim.ClaimNum).ClaimStatus);
		}
	}
}
