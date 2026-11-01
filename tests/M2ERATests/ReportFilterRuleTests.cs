// v25_4-reports-era-autoprocessed-filters: the Show PreAuths and Show $0 Payments filters of the ERAs Automatically Processed report, without a
// database. Synthetic 835s only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2ERATests {
	[TestClass]
	public class ReportFilterRuleTests {
		private static EraClp Clp(string claimIdentifier,string clp02,decimal insPaid) {
			EraClp eraClp=new EraClp() { ClaimIdentifier=claimIdentifier,Clp02=clp02 };
			eraClp.ListLines.Add(new EraLine("Z0120",100,insPaid));
			return eraClp;
		}

		[TestMethod]
		public void IsPreauthOnly_EveryClaimAPredetermination_True() {
			string text=Era835Builder.Build("SYNTRN1",Clp("PA1","25",0),Clp("PA2","25",0));
			Assert.IsTrue(Etrans835s.IsPreauthOnly(text,"0001"));
			Assert.IsTrue(Etrans835s.IsPreauthOnly(text,""));
		}

		[TestMethod]
		public void IsPreauthOnly_OnePaidClaim_False() {
			string text=Era835Builder.Build("SYNTRN1",Clp("PA1","25",0),Clp("CL2","1",80));
			Assert.IsFalse(Etrans835s.IsPreauthOnly(text,"0001"));
		}

		[TestMethod]
		public void IsPreauthOnly_NoClaimsOrNotX12_False() {
			string textNoClaims=Era835Builder.Build(new EraEob() { TraceNumber="SYNEMPTY" });
			Assert.IsFalse(Etrans835s.IsPreauthOnly(textNoClaims,"0001"));
			Assert.IsFalse(Etrans835s.IsPreauthOnly("This is a text report, not X12.",""));
			Assert.IsFalse(Etrans835s.IsPreauthOnly("",""));
			Assert.IsFalse(Etrans835s.IsPreauthOnly(null,null));
		}

		[TestMethod]
		public void IsPreauthOnly_LooksAtTheEobOfTheEtrans() {
			EraEob eobPreauth=new EraEob() { TranSetId="0001",TraceNumber="SYNA",ListClps=new List<EraClp>() { Clp("PA1","25",0) } };
			EraEob eobPaid=new EraEob() { TranSetId="0002",TraceNumber="SYNB",ListClps=new List<EraClp>() { Clp("CL1","1",50) } };
			string text=Era835Builder.Build(eobPreauth,eobPaid);
			Assert.IsTrue(Etrans835s.IsPreauthOnly(text,"0001"));
			Assert.IsFalse(Etrans835s.IsPreauthOnly(text,"0002"));
			//An etrans from 14.2 or older (blank TranSetId835) stands for every EOB in the file.
			Assert.IsFalse(Etrans835s.IsPreauthOnly(text,""));
			//An unknown ST02 falls back to the first EOB, as X835 does.
			Assert.IsTrue(Etrans835s.IsPreauthOnly(text,"9999"));
			EraEob eobPreauth2=new EraEob() { TranSetId="0002",TraceNumber="SYNC",ListClps=new List<EraClp>() { Clp("PA2","25",0) } };
			Assert.IsTrue(Etrans835s.IsPreauthOnly(Era835Builder.Build(eobPreauth,eobPreauth2),""));
		}

		[TestMethod]
		public void IsPreauthOnly_AgreesWithX835IsPreauth() {
			string text=Era835Builder.Build("SYNTRN1",Clp("PA1","25",0),Clp("CL2","1",80));
			X835 x835=new X835(new Etrans() { EtransNum=1,TranSetId835="0001",DateTimeTrans=new DateTime(2026,10,20) },text,"0001",
				new List<Etrans835Attach>());
			Assert.AreEqual(2,x835.ListClaimsPaid.Count);
			Assert.IsTrue(x835.ListClaimsPaid[0].IsPreauth);
			Assert.IsFalse(x835.ListClaimsPaid[1].IsPreauth);
		}

		[TestMethod]
		public void IsHidden_BothShown_NeverHidden() {
			string textPreauth=Era835Builder.Build("SYNTRN1",Clp("PA1","25",0));
			Etrans835 etrans835Zero=new Etrans835() { InsPaid=0 };
			Assert.IsFalse(Etrans835s.IsHiddenForAutoProcessedReport(etrans835Zero,textPreauth,"0001",doShowPreAuths:true,doShowZeroPayments:true));
		}

		[TestMethod]
		public void IsHidden_ZeroPayments() {
			Etrans835 etrans835Zero=new Etrans835() { InsPaid=0 };
			Etrans835 etrans835Paid=new Etrans835() { InsPaid=12.5 };
			Etrans835 etrans835Negative=new Etrans835() { InsPaid=-40 };
			string textPaid=Era835Builder.Build("SYNTRN1",Clp("CL1","1",12.5m));
			Assert.IsTrue(Etrans835s.IsHiddenForAutoProcessedReport(etrans835Zero,textPaid,"0001",doShowPreAuths:true,doShowZeroPayments:false));
			Assert.IsFalse(Etrans835s.IsHiddenForAutoProcessedReport(etrans835Paid,textPaid,"0001",doShowPreAuths:true,doShowZeroPayments:false));
			//A reversal that takes money back is not a $0 payment.
			Assert.IsFalse(Etrans835s.IsHiddenForAutoProcessedReport(etrans835Negative,textPaid,"0001",doShowPreAuths:true,doShowZeroPayments:false));
		}

		[TestMethod]
		public void IsHidden_PreAuths() {
			string textPreauth=Era835Builder.Build("SYNTRN1",Clp("PA1","25",0));
			string textMixed=Era835Builder.Build("SYNTRN2",Clp("PA1","25",0),Clp("CL2","1",0));
			Etrans835 etrans835=new Etrans835() { InsPaid=0 };
			Assert.IsTrue(Etrans835s.IsHiddenForAutoProcessedReport(etrans835,textPreauth,"0001",doShowPreAuths:false,doShowZeroPayments:true));
			Assert.IsFalse(Etrans835s.IsHiddenForAutoProcessedReport(etrans835,textMixed,"0001",doShowPreAuths:false,doShowZeroPayments:true));
		}

		[TestMethod]
		public void RemoveHidden_ZeroPaymentsOnly_KeepsTheListsInStep() {
			List<Etrans> listEtrans=new List<Etrans>();
			List<Etrans835> listEtrans835s=new List<Etrans835>();
			double[] arrayInsPaid=new double[] { 10,0,25,0,0.004,7 };
			for(int i=0;i<arrayInsPaid.Length;i++) {
				listEtrans.Add(new Etrans() { EtransNum=i+1 });
				listEtrans835s.Add(new Etrans835() { EtransNum=i+1,InsPaid=arrayInsPaid[i] });
			}
			//No ERA text is needed (and no database is read) when PreAuths are shown.
			Etrans835s.RemoveHiddenForAutoProcessedReport(listEtrans,listEtrans835s,doShowPreAuths:true,doShowZeroPayments:false);
			CollectionAssert.AreEqual(new long[] { 1,3,5,6 },listEtrans.Select(x => x.EtransNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 1,3,5,6 },listEtrans835s.Select(x => x.EtransNum).ToArray());
		}

		[TestMethod]
		public void RemoveHidden_BothShown_ChangesNothing() {
			List<Etrans> listEtrans=new List<Etrans>() { new Etrans() { EtransNum=1 } };
			List<Etrans835> listEtrans835s=new List<Etrans835>() { new Etrans835() { EtransNum=1,InsPaid=0 } };
			Etrans835s.RemoveHiddenForAutoProcessedReport(listEtrans,listEtrans835s,doShowPreAuths:true,doShowZeroPayments:true);
			Assert.AreEqual(1,listEtrans.Count);
			Assert.AreEqual(1,listEtrans835s.Count);
		}

		[TestMethod]
		public void Choices_RememberedPerUser_DefaultBothOn() {
			EraAutoProcessedReportFilters.ClearAll();
			EraAutoProcessedReportFilters filtersDefault=EraAutoProcessedReportFilters.GetForUser(7);
			Assert.IsTrue(filtersDefault.DoShowPreAuths);
			Assert.IsTrue(filtersDefault.DoShowZeroPayments);
			EraAutoProcessedReportFilters.SetForUser(7,doShowPreAuths:false,doShowZeroPayments:true);
			EraAutoProcessedReportFilters.SetForUser(8,doShowPreAuths:true,doShowZeroPayments:false);
			Assert.IsFalse(EraAutoProcessedReportFilters.GetForUser(7).DoShowPreAuths);
			Assert.IsTrue(EraAutoProcessedReportFilters.GetForUser(7).DoShowZeroPayments);
			Assert.IsTrue(EraAutoProcessedReportFilters.GetForUser(8).DoShowPreAuths);
			Assert.IsFalse(EraAutoProcessedReportFilters.GetForUser(8).DoShowZeroPayments);
			Assert.IsTrue(EraAutoProcessedReportFilters.GetForUser(9).DoShowPreAuths,"Another user starts with the defaults.");
			//GetForUser returns a copy: changing it does not change what is remembered.
			EraAutoProcessedReportFilters filtersCopy=EraAutoProcessedReportFilters.GetForUser(7);
			filtersCopy.DoShowPreAuths=true;
			Assert.IsFalse(EraAutoProcessedReportFilters.GetForUser(7).DoShowPreAuths);
			EraAutoProcessedReportFilters.ClearAll();
		}
	}
}
