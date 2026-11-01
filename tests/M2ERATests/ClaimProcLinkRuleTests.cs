// v26_1-account-era-claimproc-link: how Hx835_Claim.IsProcessed and the ERA windows read claimproc.Etrans835AttachNum, on in-memory ERAs and
// claimprocs. Synthetic 835s and made-up numbers only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2ERATests {
	[TestClass]
	public class ClaimProcLinkRuleTests {
		private const long CLAIM_NUM=500;
		private static readonly DateTime _dateEra=new DateTime(2026,10,20,9,0,0);

		///<summary>The SegmentIndex of each CLP segment, in order: what Hx835_Claim.ClpSegmentIndex and etrans835attach.ClpSegmentIndex hold.</summary>
		private static List<int> GetClpSegmentIndexes(string text) {
			return new X12object(text).Segments.Where(x => x.SegmentID=="CLP").Select(x => x.SegmentIndex).ToList();
		}

		private static X835 MakeX835(string text,long etransNum,DateTime dateTimeTrans,List<Etrans835Attach> listAttaches) {
			Etrans etrans=new Etrans() { EtransNum=etransNum,DateTimeTrans=dateTimeTrans,TranSetId835="0001" };
			return new X835(etrans,text,"0001",listAttaches);
		}

		private static Etrans835Attach Attach(long etrans835AttachNum,long etransNum,int clpSegmentIndex,DateTime dateTimeTrans,long claimNum=CLAIM_NUM) {
			return new Etrans835Attach() { Etrans835AttachNum=etrans835AttachNum,EtransNum=etransNum,ClaimNum=claimNum,ClpSegmentIndex=clpSegmentIndex,
				DateTimeEntry=dateTimeTrans,DateTimeTrans=dateTimeTrans };
		}

		///<summary>A received procedure claimproc with nothing but the payment, the way automatic processing leaves it (AllowedOverride blank).</summary>
		private static Hx835_ShortClaimProc ShortClaimProc(long claimProcNum,long procNum,string code,double feeBilled,double insPayAmt,long etrans835AttachNum,
			ClaimProcStatus status=ClaimProcStatus.Received,long claimNum=CLAIM_NUM)
		{
			return new Hx835_ShortClaimProc() { ClaimProcNum=claimProcNum,ProcNum=procNum,ClaimNum=claimNum,CodeSent=code,FeeBilled=feeBilled,
				InsPayAmt=insPayAmt,Status=status,AllowedOverride=-1,WriteOffEstOverride=-1,WriteOffEst=-1,Etrans835AttachNum=etrans835AttachNum,
				SecDateEntry=new DateTime(2026,10,1) };
		}

		private static EraClp ClpTwoProcs(string clp02="1",decimal paid1=80,decimal paid2=40) {
			EraClp eraClp=new EraClp() { ClaimIdentifier="SYNCLM1",Clp02=clp02,PayerControlNumber="SYNPCN1" };
			eraClp.ListLines.Add(new EraLine("Z0120",100,paid1,procNum:11));
			eraClp.ListLines.Add(new EraLine("Z0220",50,paid2,procNum:12));
			return eraClp;
		}

		[TestMethod]
		public void EnteredFromThisEraClaim_Processed_EvenWhenAmountsWereChangedByHand() {
			string text=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() { Attach(9001,1,GetClpSegmentIndexes(text)[0],_dateEra) };
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			Hx835_Claim hx835_Claim=x835.ListClaimsPaid.Single();
			Assert.AreEqual(CLAIM_NUM,hx835_Claim.ClaimNum);
			//The first payment was changed by hand after posting ($70, the ERA says $80).
			List<Hx835_ShortClaimProc> listLinked=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,70,9001),
				ShortClaimProc(2,12,"Z0220",50,40,9001),
			};
			Assert.IsTrue(hx835_Claim.IsProcessed(listLinked,listAttaches));
			//Without the link (a row from before the upgrade) the old matching sees the changed amount and says not processed.
			List<Hx835_ShortClaimProc> listUnlinked=listLinked.Select(x => { x.Etrans835AttachNum=0; return x; }).ToList();
			Assert.IsFalse(hx835_Claim.IsProcessed(listUnlinked,listAttaches));
		}

		[TestMethod]
		public void Unlinked_MatchingAmounts_ProcessedAsBefore() {
			string text=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() { Attach(9001,1,GetClpSegmentIndexes(text)[0],_dateEra) };
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			List<Hx835_ShortClaimProc> listClaimProcs=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,80,0),
				ShortClaimProc(2,12,"Z0220",50,40,0),
			};
			Assert.IsTrue(x835.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
		}

		[TestMethod]
		public void LinkToAnAttachThatNoLongerExists_FallsBackToMatching() {
			string text=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() { Attach(9001,1,GetClpSegmentIndexes(text)[0],_dateEra) };
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			List<Hx835_ShortClaimProc> listMatching=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,80,7777),
				ShortClaimProc(2,12,"Z0220",50,40,7777),
			};
			Assert.IsTrue(x835.ListClaimsPaid[0].IsProcessed(listMatching,listAttaches));
			List<Hx835_ShortClaimProc> listNotMatching=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,10,7777),
				ShortClaimProc(2,12,"Z0220",50,40,7777),
			};
			Assert.IsFalse(x835.ListClaimsPaid[0].IsProcessed(listNotMatching,listAttaches));
		}

		[TestMethod]
		public void SameClaimInTwoEras_EnteredFromTheFirst_SecondNotProcessed() {
			string text1=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			string text2=Era835Builder.Build("SYNTRN2",ClpTwoProcs());//Same claim and amounts, a later payment (another trace number).
			DateTime dateEra2=_dateEra.AddDays(10);
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() {
				Attach(9001,1,GetClpSegmentIndexes(text1)[0],_dateEra),
				Attach(9002,2,GetClpSegmentIndexes(text2)[0],dateEra2),
			};
			X835 x835First=MakeX835(text1,1,_dateEra,listAttaches);
			X835 x835Second=MakeX835(text2,2,dateEra2,listAttaches);
			List<Hx835_ShortClaimProc> listClaimProcs=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,80,9001),
				ShortClaimProc(2,12,"Z0220",50,40,9001),
			};
			Assert.IsTrue(x835First.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
			Assert.IsFalse(x835Second.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
			//The matching of 24.3 took the first ERA's claimprocs for the second ERA's payment.
			List<Hx835_ShortClaimProc> listUnlinked=listClaimProcs.Select(x => { Hx835_ShortClaimProc copy=ShortClaimProc(x.ClaimProcNum,x.ProcNum,x.CodeSent,
				x.FeeBilled,x.InsPayAmt,0); return copy; }).ToList();
			Assert.IsTrue(x835Second.ListClaimsPaid[0].IsProcessed(listUnlinked,listAttaches));
			//Once the second payment is entered (supplemental claimprocs linked to the second ERA claim), both show processed.
			listClaimProcs.Add(ShortClaimProc(3,11,"Z0120",0,80,9002,ClaimProcStatus.Supplemental));
			listClaimProcs.Add(ShortClaimProc(4,12,"Z0220",0,40,9002,ClaimProcStatus.Supplemental));
			Assert.IsTrue(x835Second.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
			Assert.IsTrue(x835First.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
		}

		[TestMethod]
		public void SameClaimTwiceInOneEra_EachClaimOnItsOwn() {
			//A reversal of the earlier payment and the corrected payment, both for the same claim, in one ERA.
			EraClp clpReversal=ClpTwoProcs("22",-80,-40);
			clpReversal.ListLines[0].Fee=-100;
			clpReversal.ListLines[1].Fee=-50;
			EraClp clpCorrection=ClpTwoProcs("1",90,40);
			clpCorrection.PayerControlNumber="SYNPCN2";
			string text=Era835Builder.Build("SYNTRN3",clpReversal,clpCorrection);
			List<int> listIndexes=GetClpSegmentIndexes(text);
			DateTime dateEra0=_dateEra.AddDays(-20);
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() {
				Attach(9000,3,99,dateEra0),//The ERA that paid the claim first (another etrans).
				Attach(9001,1,listIndexes[0],_dateEra),
				Attach(9002,1,listIndexes[1],_dateEra),
			};
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			Hx835_Claim hx835_ClaimReversal=x835.ListClaimsPaid[0];
			Hx835_Claim hx835_ClaimCorrection=x835.ListClaimsPaid[1];
			Assert.IsTrue(hx835_ClaimReversal.IsReversal);
			Assert.IsFalse(hx835_ClaimCorrection.IsSplitClaim,"A reversal and its correction are two ERA claims, not a split claim.");
			Assert.AreEqual(CLAIM_NUM,hx835_ClaimReversal.ClaimNum);
			Assert.AreEqual(CLAIM_NUM,hx835_ClaimCorrection.ClaimNum);
			List<Hx835_ShortClaimProc> listClaimProcs=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,80,9000),
				ShortClaimProc(2,12,"Z0220",50,40,9000),
			};
			Assert.IsFalse(hx835_ClaimReversal.IsProcessed(listClaimProcs,listAttaches));
			Assert.IsFalse(hx835_ClaimCorrection.IsProcessed(listClaimProcs,listAttaches));
			//The reversal is entered.
			listClaimProcs.Add(ShortClaimProc(3,11,"Z0120",0,-80,9001,ClaimProcStatus.Supplemental));
			listClaimProcs.Add(ShortClaimProc(4,12,"Z0220",0,-40,9001,ClaimProcStatus.Supplemental));
			Assert.IsTrue(hx835_ClaimReversal.IsProcessed(listClaimProcs,listAttaches));
			Assert.IsFalse(hx835_ClaimCorrection.IsProcessed(listClaimProcs,listAttaches));
			//The correction is entered.
			listClaimProcs.Add(ShortClaimProc(5,11,"Z0120",0,90,9002,ClaimProcStatus.Supplemental));
			listClaimProcs.Add(ShortClaimProc(6,12,"Z0220",0,40,9002,ClaimProcStatus.Supplemental));
			Assert.IsTrue(hx835_ClaimReversal.IsProcessed(listClaimProcs,listAttaches));
			Assert.IsTrue(hx835_ClaimCorrection.IsProcessed(listClaimProcs,listAttaches));
		}

		[TestMethod]
		public void SplitClaimOfThe835_EnteredTogether_BothProcessed() {
			//The payer split the claim into two CLP loops with the same claim identifier; they are entered together, linked to the first.
			EraClp clp1=new EraClp() { ClaimIdentifier="SYNCLM1",PayerControlNumber="SYNPCN1" };
			clp1.ListLines.Add(new EraLine("Z0120",100,80,procNum:11));
			EraClp clp2=new EraClp() { ClaimIdentifier="SYNCLM1",PayerControlNumber="SYNPCN1" };
			clp2.ListLines.Add(new EraLine("Z0220",50,40,procNum:12));
			string text=Era835Builder.Build("SYNTRN4",clp1,clp2);
			List<int> listIndexes=GetClpSegmentIndexes(text);
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() {
				Attach(9001,1,listIndexes[0],_dateEra),
				Attach(9002,1,listIndexes[1],_dateEra),
			};
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			Assert.IsTrue(x835.ListClaimsPaid[0].IsSplitClaim);
			Assert.IsTrue(x835.ListClaimsPaid[1].IsSplitClaim);
			List<Hx835_ShortClaimProc> listClaimProcs=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,75,9001),//Amount changed by hand, so only the link can tell.
				ShortClaimProc(2,12,"Z0220",50,35,9001),
			};
			Assert.IsTrue(x835.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
			Assert.IsTrue(x835.ListClaimsPaid[1].IsProcessed(listClaimProcs,listAttaches));
		}

		[TestMethod]
		public void ProcedureStillNotReceived_TheLinkAloneIsNotEnough() {
			string text=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() { Attach(9001,1,GetClpSegmentIndexes(text)[0],_dateEra) };
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			//The second procedure was set back to Not Received (to enter it again); it lost its link (ClaimProcs.Update).
			List<Hx835_ShortClaimProc> listClaimProcs=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,80,9001),
				ShortClaimProc(2,12,"Z0220",50,0,0,ClaimProcStatus.NotReceived),
			};
			Assert.IsTrue(EraClaimProcLinks.GetAttachNumsForEraClaim(x835.ListClaimsPaid[0],listAttaches).SequenceEqual(new long[] { 9001 }));
			Assert.IsFalse(EraClaimProcLinks.IsEnteredFromEraClaim(x835.ListClaimsPaid[0],listClaimProcs,listAttaches));
			Assert.IsFalse(x835.ListClaimsPaid[0].IsProcessed(listClaimProcs,listAttaches));
		}

		[TestMethod]
		public void RemoveEnteredFromOtherEraClaims_KeepsUnlinkedAndUnknownLinks() {
			string text=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() {
				Attach(9001,1,GetClpSegmentIndexes(text)[0],_dateEra),
				Attach(9005,5,42,_dateEra.AddDays(-30)),
			};
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			List<Hx835_ShortClaimProc> listClaimProcs=new List<Hx835_ShortClaimProc>() {
				ShortClaimProc(1,11,"Z0120",100,80,0),//No link.
				ShortClaimProc(2,12,"Z0220",50,40,9001),//This ERA claim.
				ShortClaimProc(3,11,"Z0120",0,5,9005,ClaimProcStatus.Supplemental),//Another ERA claim.
				ShortClaimProc(4,12,"Z0220",0,5,8888,ClaimProcStatus.Supplemental),//A row that no longer exists.
			};
			List<Hx835_ShortClaimProc> listResult=EraClaimProcLinks.RemoveEnteredFromOtherEraClaims(x835.ListClaimsPaid[0],listClaimProcs,listAttaches);
			CollectionAssert.AreEqual(new long[] { 1,2,4 },listResult.Select(x => x.ClaimProcNum).ToArray());
			Assert.AreEqual(4,listClaimProcs.Count,"The list passed in is not changed.");
		}

		[TestMethod]
		public void GetEnteredFromEraClaim_OnlyReceivedClaimProcsOfThisEraClaim() {
			string text=Era835Builder.Build("SYNTRN1",ClpTwoProcs());
			List<Etrans835Attach> listAttaches=new List<Etrans835Attach>() { Attach(9001,1,GetClpSegmentIndexes(text)[0],_dateEra) };
			X835 x835=MakeX835(text,1,_dateEra,listAttaches);
			List<ClaimProc> listClaimProcs=new List<ClaimProc>() {
				new ClaimProc() { ClaimProcNum=1,ClaimNum=CLAIM_NUM,Status=ClaimProcStatus.Received,Etrans835AttachNum=9001 },
				new ClaimProc() { ClaimProcNum=2,ClaimNum=CLAIM_NUM,Status=ClaimProcStatus.Received,Etrans835AttachNum=0 },
				new ClaimProc() { ClaimProcNum=3,ClaimNum=CLAIM_NUM,Status=ClaimProcStatus.Supplemental,Etrans835AttachNum=9002 },
				new ClaimProc() { ClaimProcNum=4,ClaimNum=CLAIM_NUM,Status=ClaimProcStatus.NotReceived,Etrans835AttachNum=9001 },
			};
			CollectionAssert.AreEqual(new long[] { 1 },
				EraClaimProcLinks.GetEnteredFromEraClaim(x835.ListClaimsPaid[0],listClaimProcs,listAttaches).Select(x => x.ClaimProcNum).ToArray());
		}

		[TestMethod]
		public void StatusRules_OnlyStatusesAnEraPostsKeepTheLink() {
			Assert.IsTrue(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.Received));
			Assert.IsTrue(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.Supplemental));
			Assert.IsTrue(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.Preauth));
			Assert.IsTrue(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.CapClaim));
			Assert.IsFalse(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.NotReceived));
			Assert.IsFalse(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.Estimate));
			Assert.IsFalse(ClaimProcs.CanHaveEtrans835AttachNum(ClaimProcStatus.Adjustment));
			ClaimProc claimProcUnreceived=new ClaimProc() { Status=ClaimProcStatus.NotReceived,Etrans835AttachNum=9001 };
			ClaimProcs.ClearEtrans835AttachNumIfNotReceived(claimProcUnreceived);
			Assert.AreEqual(0,claimProcUnreceived.Etrans835AttachNum);
			ClaimProc claimProcReceived=new ClaimProc() { Status=ClaimProcStatus.Received,Etrans835AttachNum=9001 };
			ClaimProcs.ClearEtrans835AttachNumIfNotReceived(claimProcReceived);
			Assert.AreEqual(9001,claimProcReceived.Etrans835AttachNum);
			ClaimProcs.ClearEtrans835AttachNumIfNotReceived(null);//No exception.
		}

		[TestMethod]
		public void SetEtrans835AttachNum_SkipsWhatAnEraDoesNotPost() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc>() {
				new ClaimProc() { ClaimProcNum=1,Status=ClaimProcStatus.Received },
				new ClaimProc() { ClaimProcNum=2,Status=ClaimProcStatus.Supplemental },
				new ClaimProc() { ClaimProcNum=3,Status=ClaimProcStatus.NotReceived },
				null,
			};
			ClaimProcs.SetEtrans835AttachNum(listClaimProcs,9001);
			Assert.AreEqual(9001,listClaimProcs[0].Etrans835AttachNum);
			Assert.AreEqual(9001,listClaimProcs[1].Etrans835AttachNum);
			Assert.AreEqual(0,listClaimProcs[2].Etrans835AttachNum);
		}

		[TestMethod]
		public void IsSamePayment_TraceNumberAndPayer() {
			Etrans835 etrans835=new Etrans835() { TransRefNum=" SYNTRN1 ",PayerName="Synthetic Test Payer" };
			Assert.IsTrue(EraClaimProcLinks.IsSamePayment(etrans835,"syntrn1","SYNTHETIC TEST PAYER"));
			Assert.IsFalse(EraClaimProcLinks.IsSamePayment(etrans835,"SYNTRN2","Synthetic Test Payer"));
			Assert.IsFalse(EraClaimProcLinks.IsSamePayment(etrans835,"SYNTRN1","Another Payer"));
			Assert.IsFalse(EraClaimProcLinks.IsSamePayment(etrans835,"","Synthetic Test Payer"));
			Assert.IsFalse(EraClaimProcLinks.IsSamePayment(null,"SYNTRN1","Synthetic Test Payer"));
		}
	}
}
