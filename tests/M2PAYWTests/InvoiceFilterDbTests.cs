// The Invoice filter (26.1) against stored invoices in the scratch database, and the Pay button's allocation over the filtered charges.
// Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2PAYWTests {
	[TestClass]
	public class InvoiceFilterDbTests {
		[TestInitialize]
		public void TestInitialize() {
			PaywDb.Begin();
		}

		private static Statement CreateInvoice(Patient patient,DateTime dateSent,bool isInvoice=true) {
			Statement statement=new Statement {
				PatNum=patient.PatNum,
				DateSent=dateSent,
				Mode_=StatementMode.InPerson,
				IsInvoice=isInvoice,
				SinglePatient=true,
				Note="",
				NoteBold="",
				DateRangeFrom=DateTime.MinValue,
				DateRangeTo=dateSent,
			};
			Statements.Insert(statement);
			return statement;
		}

		private static void AttachProc(Procedure procedure,Statement statement) {
			DataCore.NonQ("UPDATE procedurelog SET StatementNum="+POut.Long(statement.StatementNum)+" WHERE ProcNum="+POut.Long(procedure.ProcNum));
		}

		private static void AttachAdj(Adjustment adjustment,Statement statement) {
			DataCore.NonQ("UPDATE adjustment SET StatementNum="+POut.Long(statement.StatementNum)+" WHERE AdjNum="+POut.Long(adjustment.AdjNum));
		}

		private static long AdjType() {
			Def def=Defs.GetDefsForCategory(DefCat.AdjTypes,true).FirstOrDefault(x => x.ItemName=="M2PAYW Charge");
			if(def==null) {
				def=DefT.CreateDefinition(DefCat.AdjTypes,"M2PAYW Charge","+");
			}
			return def.DefNum;
		}

		[TestMethod]
		public void GetInvoicesForPats_ReadsMembersFromStatementNumAndStmtLinkWithTotals() {
			Patient patient=PaywDb.CreatePatient("Inv");
			Patient patientDependent=PatientT.CreatePatient("InvDep",lName:"Payw",fName:"SyntheticInvDep",guarantor:patient.PatNum);
			Statement invoiceOld=CreateInvoice(patient,DateTime.Today.AddDays(-10));
			Statement invoiceMid=CreateInvoice(patient,DateTime.Today.AddDays(-5));
			Statement invoiceNew=CreateInvoice(patientDependent,DateTime.Today.AddDays(-1));
			Statement statementLimited=CreateInvoice(patient,DateTime.Today,isInvoice:false);
			Procedure procedureA=ProcedureT.CreateProcedure(patient,"Z9201",ProcStat.C,"",100);
			Procedure procedureDeleted=ProcedureT.CreateProcedure(patient,"Z9202",ProcStat.D,"",999);
			Procedure procedureC=ProcedureT.CreateProcedure(patient,"Z9203",ProcStat.C,"",80);
			Procedure procedureDependent=ProcedureT.CreateProcedure(patientDependent,"Z9204",ProcStat.C,"",30);
			Procedure procedureLimited=ProcedureT.CreateProcedure(patient,"Z9205",ProcStat.C,"",60);
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,20,adjType:AdjType());
			AttachProc(procedureA,invoiceOld);
			AttachProc(procedureDeleted,invoiceOld);
			AttachAdj(adjustment,invoiceOld);
			PayPlanCharge payPlanCharge=new PayPlanCharge {
				PayPlanNum=1,
				PatNum=patient.PatNum,
				Guarantor=patient.PatNum,
				ChargeDate=DateTime.Today,
				Principal=40,
				Interest=5,
				ChargeType=PayPlanChargeType.Debit,
				Note="",
				StatementNum=invoiceOld.StatementNum,
			};
			PayPlanCharges.Insert(payPlanCharge);
			AttachProc(procedureC,invoiceMid);
			StmtLinks.AttachProcsToStatement(invoiceNew.StatementNum,new List<long> { procedureDependent.ProcNum });
			StmtLinks.AttachProcsToStatement(statementLimited.StatementNum,new List<long> { procedureLimited.ProcNum });
			List<InvoiceInfo> listInvoiceInfos=PaymentInvoiceFilter.GetInvoicesForPats(new List<long> { patient.PatNum,patientDependent.PatNum });
			CollectionAssert.AreEqual(new[] { invoiceNew.StatementNum,invoiceMid.StatementNum,invoiceOld.StatementNum },
				listInvoiceInfos.Select(x => x.StatementNum).ToArray(),"Newest first; the limited statement is not an invoice.");
			InvoiceInfo invoiceInfoOld=listInvoiceInfos.Single(x => x.StatementNum==invoiceOld.StatementNum);
			CollectionAssert.AreEqual(new[] { procedureA.ProcNum },invoiceInfoOld.ListProcNums,"The deleted procedure is not a member.");
			CollectionAssert.AreEqual(new[] { adjustment.AdjNum },invoiceInfoOld.ListAdjNums);
			CollectionAssert.AreEqual(new[] { payPlanCharge.PayPlanChargeNum },invoiceInfoOld.ListPayPlanChargeNums);
			Assert.AreEqual(165,invoiceInfoOld.InvoiceTotal,0.001);
			InvoiceInfo invoiceInfoNew=listInvoiceInfos.Single(x => x.StatementNum==invoiceNew.StatementNum);
			CollectionAssert.AreEqual(new[] { procedureDependent.ProcNum },invoiceInfoNew.ListProcNums,"Members can come from stmtlink rows.");
			Assert.AreEqual(30,invoiceInfoNew.InvoiceTotal,0.001);
			//The dependent alone still finds the invoice made for them.
			CollectionAssert.AreEqual(new[] { invoiceNew.StatementNum },
				PaymentInvoiceFilter.GetInvoicesForPats(new List<long> { patientDependent.PatNum }).Select(x => x.StatementNum).ToArray());
			Assert.AreEqual(0,PaymentInvoiceFilter.GetInvoicesForPats(new List<long>()).Count);
		}

		[TestMethod]
		public void OutstandingCharges_FilteredToAnInvoice_AndThePayButtonPaysOnlyItsCharges() {
			Patient patient=PaywDb.CreatePatient("Pay");
			Statement invoiceOpen=CreateInvoice(patient,DateTime.Today.AddDays(-3));
			Statement invoicePaid=CreateInvoice(patient,DateTime.Today.AddDays(-2));
			Procedure procedureA=ProcedureT.CreateProcedure(patient,"Z9211",ProcStat.C,"",100,procDate:DateTime.Today.AddDays(-20));
			Procedure procedureB=ProcedureT.CreateProcedure(patient,"Z9212",ProcStat.C,"",50,procDate:DateTime.Today.AddDays(-30));
			Procedure procedureC=ProcedureT.CreateProcedure(patient,"Z9213",ProcStat.C,"",80,procDate:DateTime.Today.AddDays(-10));
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,20,adjDate:DateTime.Today.AddDays(-20),adjType:AdjType());
			AttachProc(procedureA,invoiceOpen);
			AttachAdj(adjustment,invoiceOpen);
			AttachProc(procedureC,invoicePaid);
			PaymentT.MakePayment(patient.PatNum,80,procNum:procedureC.ProcNum,provNum:procedureC.ProvNum);
			Payment payment=PaymentT.MakePaymentNoSplits(patient.PatNum,120);
			List<AccountEntry> listAccountEntries=PaymentEdit.ConstructAndLinkChargeCredits(new List<long> { patient.PatNum },patient.PatNum,
				new List<PaySplit>(),payment,null).ListAccountEntries;
			List<InvoiceInfo> listInvoiceInfos=PaymentInvoiceFilter.GetInvoicesForPats(new List<long> { patient.PatNum });
			Assert.AreEqual(2,listInvoiceInfos.Count);
			List<InvoiceInfo> listShown=PaymentInvoiceFilter.GetInvoicesToShow(listInvoiceInfos,listAccountEntries,payment.PayNum);
			CollectionAssert.AreEqual(new[] { invoiceOpen.StatementNum },listShown.Select(x => x.StatementNum).ToArray(),"The paid invoice is not listed.");
			List<AccountEntry> listFiltered=PaymentInvoiceFilter.Filter(listAccountEntries,listShown[0]);
			CollectionAssert.AreEquivalent(new[] { "Procedure#"+procedureA.ProcNum,"Adjustment#"+adjustment.AdjNum },
				listFiltered.Select(x => x.GetType().Name+"#"+x.PriKey).ToArray());
			//The Pay button allocates over the rows that the grid shows.
			PaymentEdit.PayResults payResults=PaymentEdit.MakePayment(listFiltered.Select(x => new List<AccountEntry> { x }).ToList(),payment,
				(decimal)payment.PayAmt,listAccountEntries);
			Assert.AreEqual(120,payResults.ListSplitsCur.Sum(x => x.SplitAmt),0.001);
			Assert.AreEqual(100,payResults.ListSplitsCur.Where(x => x.ProcNum==procedureA.ProcNum).Sum(x => x.SplitAmt),0.001);
			Assert.AreEqual(20,payResults.ListSplitsCur.Where(x => x.AdjNum==adjustment.AdjNum).Sum(x => x.SplitAmt),0.001);
			Assert.IsFalse(payResults.ListSplitsCur.Any(x => x.ProcNum==procedureB.ProcNum),"Charges outside the invoice get nothing.");
		}
	}
}
