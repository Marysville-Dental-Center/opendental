// The Database Maintenance tool Conversion Payment Dates (Open Dental 26.2 item 24) against the scratch database, and its split rule without one.
// Synthetic data only. Every test uses its own patient and its own conversion date (in 2010, or in the last month for the test of the split hashes,
// which are only checked inside that window), and changes only the transfers it made, so the rows of other tests do not interfere.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262ToolsTests {
	[TestClass]
	public class ConversionPaymentDatesDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ToolsDb.Begin();
		}

		///<summary>A converted family: a payment of $100 left as unearned before the conversion, a procedure and an adjustment before it, and a
		///procedure after it.</summary>
		private class Converted {
			public DateTime DateConversion;
			public Patient Patient;
			public PaySplit SplitUnearned;
			public Procedure ProcBefore;
			public Adjustment AdjBefore;
			public Procedure ProcAfter;

			public Converted(DateTime dateConversion) {
				DateConversion=dateConversion;
				Patient=ToolsDb.NewPatient();
				Payment payment=ToolsDb.InsertPayment(Patient.PatNum,ToolsDb.NewPayType(),100,dateConversion.AddMonths(-3));
				SplitUnearned=ToolsDb.InsertSplit(payment,100,unearnedType:1);
				ProcBefore=ToolsDb.InsertProcedure(Patient.PatNum,dateConversion.AddMonths(-2));
				AdjBefore=ToolsDb.InsertAdjustment(Patient.PatNum,dateConversion.AddMonths(-1),20);
				ProcAfter=ToolsDb.InsertProcedure(Patient.PatNum,dateConversion.AddMonths(2));
			}

			///<summary>An income transfer that moves the unearned $100 to a procedure.</summary>
			public Payment NewTransfer(DateTime datePay,Procedure procedure) {
				Payment payment=ToolsDb.InsertPayment(Patient.PatNum,0,0,datePay);
				ToolsDb.InsertSplit(payment,-100,fSplitNum:SplitUnearned.SplitNum);
				ToolsDb.InsertSplit(payment,100,procNum:procedure.ProcNum);
				return payment;
			}
		}

		private static DbmConvPaymentDateRow GetRow(DateTime dateConversion,long payNum) {
			return DatabaseMaintenances.GetConversionPaymentDateRows(dateConversion).SingleOrDefault(x => x.PayNum==payNum);
		}

		[TestMethod]
		public void Rows_SuggestTransfersOfConvertedMoneyOnly() {
			Converted converted=new Converted(new DateTime(2010,6,30));
			Payment paymentConv=converted.NewTransfer(new DateTime(2011,1,10),converted.ProcBefore);
			Payment paymentToNewProc=converted.NewTransfer(new DateTime(2011,1,11),converted.ProcAfter);
			Payment paymentAdj=ToolsDb.InsertPayment(converted.Patient.PatNum,0,0,new DateTime(2011,1,12));
			ToolsDb.InsertSplit(paymentAdj,-20,unearnedType:1);
			ToolsDb.InsertSplit(paymentAdj,20,adjNum:converted.AdjBefore.AdjNum);
			Payment paymentUnearnedOnly=ToolsDb.InsertPayment(converted.Patient.PatNum,0,0,new DateTime(2011,1,13));
			ToolsDb.InsertSplit(paymentUnearnedOnly,-5,unearnedType:1);
			ToolsDb.InsertSplit(paymentUnearnedOnly,5,unearnedType:1);
			Payment paymentPayPlan=ToolsDb.InsertPayment(converted.Patient.PatNum,0,0,new DateTime(2011,1,14));
			ToolsDb.InsertSplit(paymentPayPlan,-30,procNum:converted.ProcBefore.ProcNum);
			ToolsDb.InsertSplit(paymentPayPlan,30,payPlanNum:12345);
			DbmConvPaymentDateRow rowConv=GetRow(converted.DateConversion,paymentConv.PayNum);
			Assert.IsNotNull(rowConv);
			Assert.AreEqual(2,rowConv.CountSplits);
			Assert.AreEqual(2,rowConv.CountSplitsConverted);
			Assert.IsTrue(rowConv.IsConversionTransfer);
			DbmConvPaymentDateRow rowToNewProc=GetRow(converted.DateConversion,paymentToNewProc.PayNum);
			Assert.AreEqual(1,rowToNewProc.CountSplitsAfterConversion);
			Assert.IsFalse(rowToNewProc.IsConversionTransfer);
			DbmConvPaymentDateRow rowAdj=GetRow(converted.DateConversion,paymentAdj.PayNum);
			Assert.AreEqual(1,rowAdj.CountSplitsConverted);
			Assert.AreEqual(1,rowAdj.CountSplitsUnattached);
			Assert.IsTrue(rowAdj.IsConversionTransfer,"Unearned splits do not count against a transfer.");
			Assert.IsFalse(GetRow(converted.DateConversion,paymentUnearnedOnly.PayNum).IsConversionTransfer,"Only unearned money: not suggested.");
			DbmConvPaymentDateRow rowPayPlan=GetRow(converted.DateConversion,paymentPayPlan.PayNum);
			Assert.AreEqual(1,rowPayPlan.CountSplitsPayPlan);
			Assert.IsFalse(rowPayPlan.IsConversionTransfer);
			Assert.AreEqual(converted.Patient.PatNum,rowConv.PatNum);
			StringAssert.Contains(rowConv.PatientName,"P262Tools");
		}

		[TestMethod]
		public void Rows_LeaveOutTransfersOnOrBeforeTheDateUnbalancedOnesAndPayments() {
			Converted converted=new Converted(new DateTime(2010,7,31));
			Payment paymentBefore=converted.NewTransfer(converted.DateConversion.AddDays(-5),converted.ProcBefore);
			Payment paymentOnDate=converted.NewTransfer(converted.DateConversion,converted.ProcBefore);
			Payment paymentUnbalanced=ToolsDb.InsertPayment(converted.Patient.PatNum,0,0,new DateTime(2011,2,1));
			ToolsDb.InsertSplit(paymentUnbalanced,-100,fSplitNum:converted.SplitUnearned.SplitNum);
			ToolsDb.InsertSplit(paymentUnbalanced,90,procNum:converted.ProcBefore.ProcNum);
			Payment paymentWithType=ToolsDb.InsertPayment(converted.Patient.PatNum,ToolsDb.NewPayType(),0,new DateTime(2011,2,2));
			ToolsDb.InsertSplit(paymentWithType,-10,procNum:converted.ProcBefore.ProcNum);
			ToolsDb.InsertSplit(paymentWithType,10,unearnedType:1);
			Payment paymentNoDate=converted.NewTransfer(DateTime.MinValue,converted.ProcBefore);
			List<DbmConvPaymentDateRow> listRows=DatabaseMaintenances.GetConversionPaymentDateRows(converted.DateConversion);
			Assert.IsFalse(listRows.Any(x => x.PayNum==paymentBefore.PayNum));
			Assert.IsFalse(listRows.Any(x => x.PayNum==paymentOnDate.PayNum));
			Assert.IsFalse(listRows.Any(x => x.PayNum==paymentUnbalanced.PayNum),"Unbalanced transfers belong to PaymentIncomeTransferIssues.");
			Assert.IsFalse(listRows.Any(x => x.PayNum==paymentWithType.PayNum),"Only payments without a payment type are income transfers.");
			Assert.IsTrue(listRows.Single(x => x.PayNum==paymentNoDate.PayNum).IsConversionTransfer,"A transfer with no date is listed.");
		}

		[TestMethod]
		public void Fix_DatesTheChosenTransfersAndTheirSplits_AndLogsEachOne() {
			Converted converted=new Converted(new DateTime(2010,8,31));
			Payment paymentConv=converted.NewTransfer(new DateTime(2011,3,1),converted.ProcBefore);
			Payment paymentToNewProc=converted.NewTransfer(new DateTime(2011,3,2),converted.ProcAfter);
			Payment paymentLeft=converted.NewTransfer(new DateTime(2011,3,3),converted.ProcBefore);
			string log=DatabaseMaintenances.FixConversionPaymentDates(new List<long> { paymentConv.PayNum,paymentToNewProc.PayNum },
				converted.DateConversion);
			StringAssert.Contains(log,"Income transfers dated on the conversion date "+converted.DateConversion.ToShortDateString()+": 2");
			foreach(Payment payment in new[] { paymentConv,paymentToNewProc }) {
				Assert.AreEqual(converted.DateConversion,ToolsDb.GetPayment(payment.PayNum).PayDate);
				Assert.IsTrue(ToolsDb.GetSplits(payment.PayNum).All(x => x.DatePay==converted.DateConversion),"Every split follows the payment date.");
				CollectionAssert.AreEqual(new[] { -100.0,100.0 },ToolsDb.GetSplits(payment.PayNum).Select(x => x.SplitAmt).ToArray());
				DbmLog dbmLog=ToolsDb.GetDbmLogs("ConversionPaymentDates",payment.PayNum).Single();
				Assert.AreEqual(DbmLogFKeyType.Payment,dbmLog.FKeyType);
				StringAssert.Contains(dbmLog.LogText,"from "+payment.PayDate.ToString("yyyy-MM-dd")+" to "+converted.DateConversion.ToString("yyyy-MM-dd"));
				StringAssert.Contains(dbmLog.LogText,"its 2 split(s)");
				SecurityLog securityLog=ToolsDb.GetSecurityLogs(EnumPermType.PaymentEdit,payment.PayNum).Single();
				Assert.AreEqual(converted.Patient.PatNum,securityLog.PatNum);
				StringAssert.Contains(securityLog.LogText,"moved to the conversion date");
			}
			Assert.AreEqual(new DateTime(2011,3,3),ToolsDb.GetPayment(paymentLeft.PayNum).PayDate,"A transfer that was not chosen is not changed.");
			Assert.IsNull(GetRow(converted.DateConversion,paymentConv.PayNum),"Once dated, the transfer is no longer listed.");
		}

		///<summary>The tool run soon after go-live: the conversion date and the transfers are inside the paysplit hashing window
		///(SecurityHash.GetHashingDate), where the split hash, which covers DatePay, is checked. A split whose hash was valid is rehashed with the
		///new date, so the account shows no false tampering warning; a split whose hash was already wrong keeps its warning; a split dated before
		///the window (no date) is hashed once it is moved into it.</summary>
		[TestMethod]
		public void Fix_InsideTheHashingWindow_RehashesTheSplitsWhoseHashWasValid() {
			DateTime dateConversion=DateTime.Today.AddDays(-7);
			Assert.IsTrue(dateConversion>=OpenPmsDentalBusiness.Misc.SecurityHash.GetHashingDate(),"The conversion date must be inside the window.");
			Converted converted=new Converted(dateConversion);
			DateTime dateTransfer=DateTime.Today.AddDays(-2);
			Payment paymentHashed=ToolsDb.InsertPayment(converted.Patient.PatNum,0,0,dateTransfer);
			ToolsDb.InsertSplit(paymentHashed,-100,fSplitNum:converted.SplitUnearned.SplitNum,isHashed:true);
			ToolsDb.InsertSplit(paymentHashed,100,procNum:converted.ProcBefore.ProcNum,isHashed:true);
			Payment paymentTampered=ToolsDb.InsertPayment(converted.Patient.PatNum,0,0,dateTransfer);
			ToolsDb.InsertSplit(paymentTampered,-40,fSplitNum:converted.SplitUnearned.SplitNum,isHashed:true);
			PaySplit splitTampered=ToolsDb.InsertSplit(paymentTampered,40,procNum:converted.ProcBefore.ProcNum,isHashed:true);
			//A hash that does not match the row, as if the row had been changed outside the program.
			PaySplit splitOther=splitTampered.Copy();
			splitOther.SplitAmt=999;
			splitTampered.SecurityHash=PaySplits.HashFields(splitOther);
			OpenPmsDentalBusiness.Crud.PaySplitCrud.Update(splitTampered);
			Payment paymentNoDate=converted.NewTransfer(DateTime.MinValue,converted.ProcBefore);
			foreach(PaySplit paySplit in ToolsDb.GetSplits(paymentHashed.PayNum)) {
				Assert.AreEqual(PaySplits.HashFields(paySplit),paySplit.SecurityHash,"Inserted with a real hash.");
				Assert.IsTrue(PaySplits.IsPaySplitHashValid(paySplit));
			}
			Assert.IsFalse(Payments.ArePaySplitHashesValid(paymentTampered.PayNum,ToolsDb.GetSplits(paymentTampered.PayNum)));
			Assert.IsTrue(Payments.ArePaySplitHashesValid(paymentNoDate.PayNum,ToolsDb.GetSplits(paymentNoDate.PayNum)),"Before the window: not checked.");
			string log=DatabaseMaintenances.FixConversionPaymentDates(
				new List<long> { paymentHashed.PayNum,paymentTampered.PayNum,paymentNoDate.PayNum },dateConversion);
			StringAssert.Contains(log,"Income transfers dated on the conversion date "+dateConversion.ToShortDateString()+": 3");
			List<PaySplit> listSplitsHashed=ToolsDb.GetSplits(paymentHashed.PayNum);
			Assert.AreEqual(2,listSplitsHashed.Count);
			foreach(PaySplit paySplit in listSplitsHashed) {
				Assert.AreEqual(dateConversion,paySplit.DatePay);
				Assert.AreEqual(PaySplits.HashFields(paySplit),paySplit.SecurityHash,"Rehashed with the conversion date.");
				Assert.IsTrue(PaySplits.IsPaySplitHashValid(paySplit));
			}
			Assert.IsTrue(Payments.ArePaySplitHashesValid(paymentHashed.PayNum,listSplitsHashed),"No false tampering warning.");
			List<PaySplit> listSplitsTampered=ToolsDb.GetSplits(paymentTampered.PayNum);
			Assert.IsTrue(listSplitsTampered.All(x => x.DatePay==dateConversion));
			Assert.IsTrue(PaySplits.IsPaySplitHashValid(listSplitsTampered.Single(x => x.SplitNum!=splitTampered.SplitNum)));
			Assert.IsFalse(PaySplits.IsPaySplitHashValid(listSplitsTampered.Single(x => x.SplitNum==splitTampered.SplitNum)),
				"A split whose hash was already wrong keeps its warning.");
			Assert.IsFalse(Payments.ArePaySplitHashesValid(paymentTampered.PayNum,listSplitsTampered));
			List<PaySplit> listSplitsNoDate=ToolsDb.GetSplits(paymentNoDate.PayNum);
			Assert.IsTrue(listSplitsNoDate.All(x => x.DatePay==dateConversion));
			Assert.IsTrue(listSplitsNoDate.All(x => PaySplits.IsPaySplitHashValid(x)),"Moved into the window from before it: hashed.");
			Assert.IsTrue(Payments.ArePaySplitHashesValid(paymentNoDate.PayNum,listSplitsNoDate));
			StringAssert.Contains(ToolsDb.GetDbmLogs("ConversionPaymentDates",paymentHashed.PayNum).Single().LogText,"its 2 split(s)");
		}

		[TestMethod]
		public void Fix_SkipsPaymentsThatAreNoLongerListed() {
			Converted converted=new Converted(new DateTime(2010,9,30));
			Payment paymentOnDate=converted.NewTransfer(converted.DateConversion,converted.ProcBefore);
			Payment paymentReal=ToolsDb.InsertPayment(converted.Patient.PatNum,ToolsDb.NewPayType(),40,new DateTime(2011,4,1));
			ToolsDb.InsertSplit(paymentReal,40);
			string log=DatabaseMaintenances.FixConversionPaymentDates(new List<long> { paymentOnDate.PayNum,paymentReal.PayNum },converted.DateConversion);
			StringAssert.Contains(log,": 0");
			StringAssert.Contains(log,"skipped because they changed since they were listed: 2");
			Assert.AreEqual(new DateTime(2011,4,1),ToolsDb.GetPayment(paymentReal.PayNum).PayDate);
			Assert.AreEqual(0,ToolsDb.GetDbmLogs("ConversionPaymentDates",paymentReal.PayNum).Count);
		}

		[TestMethod]
		public void Fix_WithoutAValidDate_Throws() {
			try {
				DatabaseMaintenances.FixConversionPaymentDates(new List<long> { 1 },DateTime.MinValue);
				Assert.Fail("No exception.");
			}
			catch(ApplicationException) {
			}
		}
	}

	[TestClass]
	public class ConversionSplitKindTests {
		private static readonly DateTime _dateConv=new DateTime(2020,6,30);

		[TestMethod]
		public void PayPlan_Wins() {
			Assert.AreEqual(ConvSplitKind.PayPlan,DatabaseMaintenances.GetConvSplitKind(_dateConv,5,0,1,_dateConv.AddDays(-1),0,null,0,null));
			Assert.AreEqual(ConvSplitKind.PayPlan,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,6,0,null,0,null,0,null));
		}

		[TestMethod]
		public void Unattached_WhenNothingIsAttached() {
			Assert.AreEqual(ConvSplitKind.Unattached,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,0,null,0,null,0,null));
			Assert.AreEqual(ConvSplitKind.Unattached,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,0,null,0,null,7,null),
				"A reversed split that no longer exists says nothing.");
		}

		[TestMethod]
		public void Converted_OnOrBeforeTheDate() {
			Assert.AreEqual(ConvSplitKind.Converted,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,1,_dateConv,0,null,0,null));
			Assert.AreEqual(ConvSplitKind.Converted,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,0,null,2,_dateConv.AddYears(-1),0,null));
			Assert.AreEqual(ConvSplitKind.Converted,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,0,null,0,null,3,_dateConv.AddDays(-9)));
		}

		[TestMethod]
		public void AfterConversion_WhenAnyDateIsLaterOrTheEntryIsMissing() {
			Assert.AreEqual(ConvSplitKind.AfterConversion,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,1,_dateConv.AddDays(1),0,null,0,null));
			Assert.AreEqual(ConvSplitKind.AfterConversion,
				DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,1,_dateConv.AddDays(-1),0,null,3,_dateConv.AddDays(1)),
				"Money of a payment after the conversion moved onto a converted procedure.");
			Assert.AreEqual(ConvSplitKind.AfterConversion,DatabaseMaintenances.GetConvSplitKind(_dateConv,0,0,1,null,0,null,0,null),
				"A procedure that no longer exists.");
		}
	}
}
