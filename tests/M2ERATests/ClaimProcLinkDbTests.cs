// v26_1-account-era-claimproc-link on a scratch database: the claimprocs an ERA enters store the ERA claim (claimproc.Etrans835AttachNum), by
// automatic processing and by the manual "enter payment" flow; a hand-entered claimproc has 0; the link is cleared when the claimproc is
// un-received or the ERA claim is detached, and kept when the attach row is made again for the same claim; and the ERA claim's processed state
// follows the link when the same claim is in several ERAs. Synthetic data only (made-up Z codes, payer, patients and 835s).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2ERATests {
	[TestClass]
	public class ClaimProcLinkDbTests {
		private static readonly DateTime _dateService=new DateTime(2026,9,1);
		private static int _countCases=0;

		private class EraCase {
			public Patient Patient;
			public List<Procedure> ListProcs;
			public Claim Claim;
		}

		private class EraImport {
			public Etrans Etrans;
			public string Text;
			public X835 X835;
			public List<Etrans835Attach> ListAttaches;
		}

		[TestInitialize]
		public void Init() {
			EraDb.Begin();
			Prefs.UpdateInt(PrefName.EraAutomationBehavior,(int)EraAutomationMode.SemiAutomatic);
			Prefs.UpdateString(PrefName.EraNoAutoProcessCarcCodes,"");
		}

		///<summary>A patient with primary insurance and a sent claim for the given completed procedures (made-up Z codes), each claimproc billed at
		///the procedure fee.</summary>
		private static EraCase CreateClaim(params (string Code,double Fee)[] arrayProcs) {
			_countCases++;
			string suffix=new string((char)('A'+(_countCases%26)),1)+new string((char)('A'+(_countCases/26%26)),1);
			EraCase eraCase=new EraCase();
			eraCase.Patient=PatientT.CreatePatient(suffix,lName:"Mera",fName:"Synth");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(eraCase.Patient,"Synthetic Payer "+suffix);
			eraCase.ListProcs=arrayProcs.Select(x => ProcedureT.CreateProcedure(eraCase.Patient,x.Code,ProcStat.C,"",x.Fee,_dateService)).ToList();
			Claim claim=ClaimT.CreateClaim(eraCase.ListProcs,insuranceInfo,"P","MERA"+_countCases+suffix);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claim.ClaimNum);
			foreach(ClaimProc claimProc in listClaimProcs) {
				claimProc.FeeBilled=eraCase.ListProcs.First(x => x.ProcNum==claimProc.ProcNum).ProcFee;
				ClaimProcs.Update(claimProc);
			}
			claim.ClaimFee=listClaimProcs.Sum(x => x.FeeBilled);
			claim.ClaimStatus="S";
			Claims.Update(claim);
			eraCase.Claim=Claims.GetClaim(claim.ClaimNum);
			return eraCase;
		}

		///<summary>One CLP for the case's claim paying each procedure (by ProcNum) the given amount, with the rest written off (CO-45).</summary>
		private static EraClp ClpFor(EraCase eraCase,params decimal[] arrayPaid) {
			EraClp eraClp=new EraClp() { ClaimIdentifier=eraCase.Claim.ClaimIdentifier,LName=eraCase.Patient.LName,FName=eraCase.Patient.FName,
				PayerControlNumber="SYNPCN"+eraCase.Claim.ClaimNum,DateService=_dateService };
			for(int i=0;i<eraCase.ListProcs.Count;i++) {
				Procedure procedure=eraCase.ListProcs[i];
				decimal fee=(decimal)procedure.ProcFee;
				eraClp.ListLines.Add(new EraLine(ProcedureCodes.GetStringProcCode(procedure.CodeNum),fee,arrayPaid[i],procedure.ProcNum,fee-arrayPaid[i]));
			}
			return eraClp;
		}

		///<summary>Stores the 835 as an ERA received at dateTimeTrans and attaches each CLP to the claim given for it (0: no attach), as matching or the
		///ERA window would. Also stores the etrans835 row, as the ERA list does.</summary>
		private static EraImport ImportEra(string text,DateTime dateTimeTrans,params long[] arrayClaimNumsPerClp) {
			EraImport eraImport=new EraImport();
			eraImport.Text=text;
			Etrans etrans=Etranss.CreateEtrans(dateTimeTrans,0,text,0);
			etrans.Etype=EtransType.ERA_835;
			etrans.TranSetId835="0001";
			Etranss.Insert(etrans);
			DataCore.NonQ("UPDATE etrans SET DateTimeTrans="+POut.DateT(dateTimeTrans)+" WHERE EtransNum="+POut.Long(etrans.EtransNum));
			etrans.DateTimeTrans=dateTimeTrans;
			eraImport.Etrans=etrans;
			List<int> listClpIndexes=new X12object(text).Segments.Where(x => x.SegmentID=="CLP").Select(x => x.SegmentIndex).ToList();
			for(int i=0;i<arrayClaimNumsPerClp.Length;i++) {
				if(arrayClaimNumsPerClp[i]==0) {
					continue;
				}
				Etrans835Attaches.Insert(new Etrans835Attach() { EtransNum=etrans.EtransNum,ClaimNum=arrayClaimNumsPerClp[i],ClpSegmentIndex=listClpIndexes[i] });
			}
			Reload(eraImport);
			Etrans835s.Upsert(new Etrans835() { EtransNum=etrans.EtransNum },eraImport.X835);
			return eraImport;
		}

		///<summary>Reads the ERA again with the attach rows (and the claims' claimprocs) from the database, as the ERA window does on load.</summary>
		private static void Reload(EraImport eraImport) {
			List<Etrans835Attach> listAttachesEra=Etrans835Attaches.GetForEtrans(eraImport.Etrans.EtransNum);
			eraImport.X835=new X835(eraImport.Etrans,eraImport.Text,eraImport.Etrans.TranSetId835,listAttachesEra);
			eraImport.X835.RefreshAttachesAndClaimProcsFromDb(out eraImport.ListAttaches,out List<Hx835_ShortClaimProc> _);
		}

		private static bool IsProcessed(EraImport eraImport,int indexClp=0) {
			Reload(eraImport);
			eraImport.X835.RefreshAttachesAndClaimProcsFromDb(out List<Etrans835Attach> listAttaches,out List<Hx835_ShortClaimProc> listClaimProcs);
			return eraImport.X835.ListClaimsPaid[indexClp].IsProcessed(listClaimProcs,listAttaches);
		}

		private static long GetAttachNum(EraImport eraImport,int indexClp=0) {
			Hx835_Claim hx835_Claim=eraImport.X835.ListClaimsPaid[indexClp];
			return Etrans835Attaches.GetNumForEraClaim(eraImport.Etrans.EtransNum,hx835_Claim.ClpSegmentIndex,hx835_Claim.ClaimNum);
		}

		private static List<ClaimProc> GetProcClaimProcs(EraCase eraCase) {
			return ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum).FindAll(x => x.ProcNum!=0).OrderBy(x => x.ClaimProcNum).ToList();
		}

		///<summary>A claim for Z0120 $100 and Z0220 $50, and an ERA paying $80 and $40, automatically processed.</summary>
		private static (EraCase EraCase,EraImport EraImport) CreateAutoProcessedEra() {
			EraCase eraCase=CreateClaim(("Z0120",100),("Z0220",50));
			EraImport eraImport=ImportEra(Era835Builder.Build("SYNTRN"+eraCase.Claim.ClaimNum,ClpFor(eraCase,80,40)),DateTime.Now.AddDays(-2),
				eraCase.Claim.ClaimNum);
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraImport.X835,eraImport.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.ListClaimErrors.Count,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.CountClaimsProcessed);
			return (eraCase,eraImport);
		}

		[TestMethod]
		public void AutomaticProcessing_LinksTheReceivedClaimProcsToTheEraClaim() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			long attachNum=GetAttachNum(eraImport);
			Assert.AreNotEqual(0L,attachNum);
			List<ClaimProc> listClaimProcs=GetProcClaimProcs(eraCase);
			Assert.AreEqual(2,listClaimProcs.Count);
			Assert.IsTrue(listClaimProcs.All(x => x.Status==ClaimProcStatus.Received));
			Assert.IsTrue(listClaimProcs.All(x => x.Etrans835AttachNum==attachNum),"Every received claimproc names the ERA claim.");
			Assert.IsTrue(IsProcessed(eraImport));
			Assert.AreEqual(X835Status.Finalized,eraImport.X835.GetStatus());
		}

		[TestMethod]
		public void ManualEntry_LinksTheClaimProcsAndTheByTotalLine() {
			EraCase eraCase=CreateClaim(("Z0120",100),("Z0220",50));
			//The second line comes back without the procedure number and with another code, so its payment goes to a By Total line.
			EraClp eraClp=ClpFor(eraCase,80,40);
			eraClp.ListLines[1].ProcNum=0;
			eraClp.ListLines[1].Code="Z9999";
			EraImport eraImport=ImportEra(Era835Builder.Build("SYNTRNM"+eraCase.Claim.ClaimNum,eraClp),DateTime.Now.AddDays(-2),eraCase.Claim.ClaimNum);
			Hx835_Claim hx835_Claim=eraImport.X835.ListClaimsPaid[0];
			Claim claim=Claims.GetClaim(eraCase.Claim.ClaimNum);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claim.ClaimNum);
			//What EtransL.ImportEraClaimData and the OK button of the ERA payment window (FormEtrans835ClaimPay) do.
			Assert.IsTrue(Etranss.TryImportEraClaimData(eraImport.X835,hx835_Claim,claim,eraCase.Patient,isAutomatic:false,listClaimProcs,0));
			long attachNum=GetAttachNum(eraImport);
			Assert.IsTrue(listClaimProcs.Where(x => x.Status==ClaimProcStatus.Received).All(x => x.Etrans835AttachNum==attachNum),"Linked in memory.");
			Etranss.ReceiveEraPayment(claim,hx835_Claim,listClaimProcs,true,false);
			List<ClaimProc> listClaimProcsDb=ClaimProcs.RefreshForClaim(claim.ClaimNum);
			ClaimProc claimProcByTotal=listClaimProcsDb.Single(x => x.ProcNum==0);
			Assert.AreEqual(40,claimProcByTotal.InsPayAmt,0.001);
			Assert.AreEqual(attachNum,claimProcByTotal.Etrans835AttachNum,"The By Total line came from the ERA claim too.");
			Assert.IsTrue(listClaimProcsDb.Where(x => x.ProcNum!=0).All(x => x.Status==ClaimProcStatus.Received && x.Etrans835AttachNum==attachNum));
			Assert.IsTrue(IsProcessed(eraImport));
		}

		[TestMethod]
		public void ManualEntry_Cancelled_NothingStored() {
			EraCase eraCase=CreateClaim(("Z0120",100),("Z0220",50));
			EraImport eraImport=ImportEra(Era835Builder.Build("SYNTRNC"+eraCase.Claim.ClaimNum,ClpFor(eraCase,80,40)),DateTime.Now.AddDays(-2),
				eraCase.Claim.ClaimNum);
			Claim claim=Claims.GetClaim(eraCase.Claim.ClaimNum);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claim.ClaimNum);
			Assert.IsTrue(Etranss.TryImportEraClaimData(eraImport.X835,eraImport.X835.ListClaimsPaid[0],claim,eraCase.Patient,isAutomatic:false,listClaimProcs,0));
			//The user cancels the ERA payment window: nothing is saved.
			Assert.IsTrue(GetProcClaimProcs(eraCase).All(x => x.Etrans835AttachNum==0 && x.Status==ClaimProcStatus.NotReceived));
			Assert.IsFalse(IsProcessed(eraImport));
		}

		[TestMethod]
		public void HandEnteredClaimProcs_HaveNoLink() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			List<ClaimProc> listClaimProcs=GetProcClaimProcs(eraCase);
			Assert.IsTrue(listClaimProcs.All(x => x.Etrans835AttachNum!=0));
			//The Supplemental button of the Edit Claim window copies the received claimprocs.
			List<ClaimProc> listSupplementals=ClaimProcs.CreateSuppClaimProcs(listClaimProcs);
			Assert.AreEqual(2,listSupplementals.Count);
			Assert.IsTrue(listSupplementals.All(x => x.Etrans835AttachNum==0),"In memory.");
			List<ClaimProc> listSupplementalsDb=ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum).FindAll(x => x.Status==ClaimProcStatus.Supplemental);
			Assert.AreEqual(2,listSupplementalsDb.Count);
			Assert.IsTrue(listSupplementalsDb.All(x => x.Etrans835AttachNum==0),"Stored.");
			//A claim received by hand (By Procedure in the Edit Claim window) has no link either.
			EraCase eraCaseByHand=CreateClaim(("Z0120",100));
			ClaimT.ReceiveClaim(Claims.GetClaim(eraCaseByHand.Claim.ClaimNum),ClaimProcs.RefreshForClaim(eraCaseByHand.Claim.ClaimNum),doSetInsPayAmt:true);
			Assert.IsTrue(GetProcClaimProcs(eraCaseByHand).All(x => x.Status==ClaimProcStatus.Received && x.Etrans835AttachNum==0));
		}

		[TestMethod]
		public void Unreceived_ForgetsTheEraClaim() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			List<ClaimProc> listClaimProcs=GetProcClaimProcs(eraCase);
			ClaimProc claimProcOld=listClaimProcs[0].Copy();
			listClaimProcs[0].Status=ClaimProcStatus.NotReceived;
			ClaimProcs.Update(listClaimProcs[0],claimProcOld);
			ClaimProc claimProcOther=listClaimProcs[1];
			claimProcOther.Status=ClaimProcStatus.NotReceived;
			ClaimProcs.Update(claimProcOther);//Without the old copy.
			List<ClaimProc> listClaimProcsDb=GetProcClaimProcs(eraCase);
			Assert.IsTrue(listClaimProcsDb.All(x => x.Status==ClaimProcStatus.NotReceived && x.Etrans835AttachNum==0));
			Assert.IsFalse(IsProcessed(eraImport),"Payment taken back: the ERA claim can be entered again.");
		}

		[TestMethod]
		public void OneProcedureUnreceived_EraClaimNotProcessedAndWarned() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			List<ClaimProc> listClaimProcs=GetProcClaimProcs(eraCase);
			ClaimProc claimProcOld=listClaimProcs[1].Copy();
			listClaimProcs[1].Status=ClaimProcStatus.NotReceived;
			ClaimProcs.Update(listClaimProcs[1],claimProcOld);
			Assert.IsFalse(IsProcessed(eraImport));
			//The ERA window warns before the same ERA claim is entered again (EtransL.ImportEraClaimData).
			Hx835_Claim hx835_Claim=eraImport.X835.ListClaimsPaid[0];
			List<Etrans835Attach> listAttaches=Etrans835Attaches.GetForEtransNumOrClaimNums(false,eraImport.Etrans.EtransNum,eraCase.Claim.ClaimNum);
			List<ClaimProc> listEntered=EraClaimProcLinks.GetEnteredFromEraClaim(hx835_Claim,ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum),listAttaches);
			CollectionAssert.AreEqual(new long[] { listClaimProcs[0].ClaimProcNum },listEntered.Select(x => x.ClaimProcNum).ToArray());
		}

		[TestMethod]
		public void EraClaimDetached_ClaimProcsForgetIt() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			Etrans835Attaches.DetachEraClaim(eraImport.X835.ListClaimsPaid[0]);
			List<ClaimProc> listClaimProcs=GetProcClaimProcs(eraCase);
			Assert.IsTrue(listClaimProcs.All(x => x.Etrans835AttachNum==0),"The link goes.");
			Assert.IsTrue(listClaimProcs.All(x => x.Status==ClaimProcStatus.Received),"The payment stays (detaching does not reverse payments).");
		}

		[TestMethod]
		public void AttachMadeAgain_SameClaim_KeepsTheLink_OtherClaim_ClearsIt() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			long attachNumOld=GetAttachNum(eraImport);
			Hx835_Claim hx835_Claim=eraImport.X835.ListClaimsPaid[0];
			//The ERA window or automatic processing makes the attach row again for the same claim (isNewAttachNeeded).
			Etrans835Attaches.CreateForClaim(eraImport.X835,hx835_Claim,eraCase.Claim.ClaimNum,isNewAttachNeeded:true,eraImport.ListAttaches);
			long attachNumNew=GetAttachNum(eraImport);
			Assert.AreNotEqual(attachNumOld,attachNumNew);
			Assert.IsTrue(GetProcClaimProcs(eraCase).All(x => x.Etrans835AttachNum==attachNumNew),"Moved to the new row.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM claimproc WHERE Etrans835AttachNum="+POut.Long(attachNumOld)));
			Assert.IsTrue(IsProcessed(eraImport));
			//The ERA claim is attached to another claim: the first claim's claimprocs no longer come from it.
			EraCase eraCaseOther=CreateClaim(("Z0120",100),("Z0220",50));
			Reload(eraImport);
			Etrans835Attaches.CreateForClaim(eraImport.X835,eraImport.X835.ListClaimsPaid[0],eraCaseOther.Claim.ClaimNum,isNewAttachNeeded:true,
				eraImport.ListAttaches);
			Assert.IsTrue(GetProcClaimProcs(eraCase).All(x => x.Etrans835AttachNum==0));
		}

		[TestMethod]
		public void EraDeleted_ClaimProcsForgetIt() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			Etranss.Delete835(eraImport.Etrans);
			Assert.IsTrue(GetProcClaimProcs(eraCase).All(x => x.Etrans835AttachNum==0));
		}

		[TestMethod]
		public void ClaimPaymentDeleted_LinkKept() {
			(EraCase eraCase,EraImport eraImport)=CreateAutoProcessedEra();
			List<ClaimProc> listClaimProcs=GetProcClaimProcs(eraCase);
			long claimPaymentNum=listClaimProcs[0].ClaimPaymentNum;
			Assert.AreNotEqual(0L,claimPaymentNum,"Automatic processing finalized the payment.");
			DataCore.NonQ("UPDATE claimpayment SET DepositNum=0 WHERE ClaimPaymentNum="+POut.Long(claimPaymentNum));
			ClaimPayments.Delete(ClaimPayments.GetOne(claimPaymentNum));
			listClaimProcs=GetProcClaimProcs(eraCase);
			Assert.IsTrue(listClaimProcs.All(x => x.ClaimPaymentNum==0));
			//Deviation from the spec (see the port note): the claimprocs are still received with the ERA's amounts, so they still come from the ERA claim.
			Assert.IsTrue(listClaimProcs.All(x => x.Etrans835AttachNum!=0));
			Assert.IsTrue(IsProcessed(eraImport));
		}

		[TestMethod]
		public void SameClaimInALaterEra_NotProcessedUntilItsOwnPaymentIsEntered() {
			(EraCase eraCase,EraImport eraImport1)=CreateAutoProcessedEra();
			//A second payment for the same claim and amounts, with its own check number, ten days later.
			EraImport eraImport2=ImportEra(Era835Builder.Build("SYNTRN2ND"+eraCase.Claim.ClaimNum,ClpFor(eraCase,80,40)),DateTime.Now.AddDays(-1),
				eraCase.Claim.ClaimNum);
			Assert.IsFalse(IsProcessed(eraImport2),"The claimprocs came from the first ERA.");
			Assert.IsTrue(IsProcessed(eraImport1));
			//Rows from before the upgrade have no link: the old matching (which took the first ERA's claimprocs for this one) is kept for them.
			DataCore.NonQ("UPDATE claimproc SET Etrans835AttachNum=0 WHERE ClaimNum="+POut.Long(eraCase.Claim.ClaimNum));
			Assert.IsTrue(IsProcessed(eraImport2));
			DataCore.NonQ("UPDATE claimproc SET Etrans835AttachNum="+POut.Long(GetAttachNum(eraImport1))+" WHERE ClaimNum="+POut.Long(eraCase.Claim.ClaimNum));
			//Automatic processing enters the second payment as supplemental claimprocs linked to the second ERA claim.
			Reload(eraImport2);
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraImport2.X835,eraImport2.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.ListClaimErrors.Count,string.Join("\r\n",result.ListClaimErrors));
			Assert.AreEqual(1,result.CountClaimsProcessed);
			long attachNum2=GetAttachNum(eraImport2);
			List<ClaimProc> listSupplementals=ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum).FindAll(x => x.Status==ClaimProcStatus.Supplemental);
			Assert.AreEqual(2,listSupplementals.Count);
			Assert.IsTrue(listSupplementals.All(x => x.Etrans835AttachNum==attachNum2));
			Assert.IsTrue(IsProcessed(eraImport2));
			Assert.IsTrue(IsProcessed(eraImport1));
		}

		[TestMethod]
		public void SamePaymentImportedTwice_AutomaticProcessingHoldsItBack() {
			(EraCase eraCase,EraImport eraImport1)=CreateAutoProcessedEra();
			//The very same 835 (same check number and payer) comes in again.
			EraImport eraImportCopy=ImportEra(eraImport1.Text,DateTime.Now.AddDays(-1),eraCase.Claim.ClaimNum);
			Assert.IsFalse(IsProcessed(eraImportCopy));
			Reload(eraImportCopy);
			eraImportCopy.X835.RefreshAttachesAndClaimProcsFromDb(out List<Etrans835Attach> listAttaches,out List<Hx835_ShortClaimProc> listShortClaimProcs);
			CollectionAssert.AreEqual(new long[] { eraImport1.Etrans.EtransNum },
				EraClaimProcLinks.GetEtransNumsWithSamePayment(eraImportCopy.X835.ListClaimsPaid[0],listShortClaimProcs,listAttaches).ToArray());
			EraAutomationResult result=Etranss.TryAutoProcessEraEob(eraImportCopy.X835,eraImportCopy.ListAttaches,isFullyAutomatic:false);
			Assert.AreEqual(0,result.CountClaimsProcessed);
			StringAssert.Contains(string.Join("\r\n",result.ListClaimErrors),"same check or EFT trace number");
			Assert.AreEqual(0,ClaimProcs.RefreshForClaim(eraCase.Claim.ClaimNum).Count(x => x.Status==ClaimProcStatus.Supplemental),"Nothing posted twice.");
		}

		[TestMethod]
		public void SameClaimTwiceInOneEra_ReversalAndCorrection_EachEnteredOnItsOwn() {
			(EraCase eraCase,EraImport eraImport1)=CreateAutoProcessedEra();
			EraClp clpReversal=ClpFor(eraCase,-80,-40);
			clpReversal.Clp02="22";
			foreach(EraLine line in clpReversal.ListLines) {
				line.Fee=-line.Fee;
				line.WriteOff=0;
			}
			EraClp clpCorrection=ClpFor(eraCase,90,40);
			clpCorrection.PayerControlNumber="SYNPCNCORR"+eraCase.Claim.ClaimNum;
			EraImport eraImport2=ImportEra(Era835Builder.Build("SYNTRNRC"+eraCase.Claim.ClaimNum,clpReversal,clpCorrection),DateTime.Now.AddDays(-1),
				eraCase.Claim.ClaimNum,eraCase.Claim.ClaimNum);
			Assert.IsFalse(eraImport2.X835.ListClaimsPaid[1].IsSplitClaim);
			Assert.IsFalse(IsProcessed(eraImport2,0));
			Assert.IsFalse(IsProcessed(eraImport2,1));
			//Enter the reversal by hand from the ERA window (supplemental, negative).
			Claim claim=Claims.GetClaim(eraCase.Claim.ClaimNum);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claim.ClaimNum);
			Hx835_Claim hx835_ClaimReversal=eraImport2.X835.ListClaimsPaid[0];
			Assert.IsTrue(Etranss.TryImportEraClaimData(eraImport2.X835,hx835_ClaimReversal,claim,eraCase.Patient,isAutomatic:false,listClaimProcs,0));
			Etranss.ReceiveEraPayment(claim,hx835_ClaimReversal,listClaimProcs,true,isSupplementalPay:true);
			long attachNumReversal=GetAttachNum(eraImport2,0);
			Assert.AreNotEqual(GetAttachNum(eraImport2,1),attachNumReversal);
			List<ClaimProc> listReversal=ClaimProcs.RefreshForClaim(claim.ClaimNum).FindAll(x => x.Etrans835AttachNum==attachNumReversal);
			Assert.IsTrue(listReversal.Count>0);
			Assert.IsTrue(listReversal.All(x => x.Status==ClaimProcStatus.Supplemental));
			Assert.IsTrue(IsProcessed(eraImport2,0));
			Assert.IsFalse(IsProcessed(eraImport2,1),"The correction is still to be entered.");
			Assert.IsTrue(IsProcessed(eraImport1),"The first ERA's claimprocs are unchanged.");
		}
	}
}
