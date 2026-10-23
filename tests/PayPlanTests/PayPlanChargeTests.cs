// 25.1 v25_1-account-payplan-downpayment-flag: payment plan charges that make up the down payment carry payplancharge.IsDownPayment, and the
// code that used to look for "down payment" in the charge note reads the flag. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace PayPlanTests {
	[TestClass]
	public class PayPlanChargeTests {
		[TestInitialize]
		public void TestInitialize() {
			PayPlanDb.Begin();
			Security.CurUser=Security.CurUser??new Userod();
		}

		[TestMethod]
		public void OldStylePlan_DownPaymentChargeIsFlagged() {
			Patient patient=PayPlanDb.CreatePatient("OldDown");
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patient.PatNum,600,DateTime.Today);
			PayPlanTerms terms=new PayPlanTerms {
				APR=0,
				DateAgreement=DateTime.Today,
				DateFirstPayment=DateTime.Today.AddMonths(1),
				DateInterestStart=DateTime.Today.AddMonths(1),
				DownPayment=150,
				Frequency=PayPlanFrequency.Monthly,
				PaySchedule=PaymentSchedule.Monthly,
				PeriodPayment=150,
				PrincipalAmount=600,
				RoundDec=2,
			};
			List<PayPlanCharge> listPayPlanCharges=new List<PayPlanCharge>();
			PayPlanEdit.CreateScheduleCharges(terms,payPlan,Patients.GetFamily(patient.PatNum),0,0,listPayPlanCharges);
			Assert.AreEqual(1,listPayPlanCharges.Count(x => x.IsDownPayment),"Exactly the down payment charge.");
			Assert.AreEqual(150,listPayPlanCharges.Single(x => x.IsDownPayment).Principal,0.001);
			PayPlanCharges.InsertMany(listPayPlanCharges);
			List<PayPlanCharge> listPayPlanChargesDb=PayPlanCharges.GetForPayPlan(payPlan.PayPlanNum);
			Assert.AreEqual(4,listPayPlanChargesDb.Count);
			Assert.AreEqual(1,listPayPlanChargesDb.Count(x => x.IsDownPayment),"The flag is stored.");
		}

		[TestMethod]
		public void DynamicPlan_EveryPieceOfTheDownPaymentIsFlagged() {
			Patient patient=PayPlanDb.CreatePatient("DynDown");
			Adjustment adjustment1=AdjustmentT.MakeAdjustment(patient.PatNum,100,DateTime.Today);
			Adjustment adjustment2=AdjustmentT.MakeAdjustment(patient.PatNum,100,DateTime.Today);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patient.PatNum,patient.PatNum,DateTime.Today,150,0,25,new List<Procedure>(),
				new List<Adjustment> { adjustment1,adjustment2 },PayPlanFrequency.Monthly,runService:false,doInsertChargeForToday:false);
			List<PayPlanCharge> listPayPlanChargesDb=PayPlanCharges.GetForPayPlan(payPlan.PayPlanNum);
			List<PayPlanCharge> listDownPayments=listPayPlanChargesDb.FindAll(x => x.IsDownPayment);
			Assert.IsTrue(listDownPayments.Count>=2,"The down payment is split across the production it pays on.");
			Assert.AreEqual(150,listDownPayments.Sum(x => x.Principal),0.001);
			Assert.IsTrue(listDownPayments.All(x => x.ChargeType==PayPlanChargeType.Debit));
			Assert.AreEqual(listPayPlanChargesDb.Count,listDownPayments.Count,"No other charges were issued yet.");
		}

		[TestMethod]
		public void DeleteDebitsWithoutPayments_KeepsFlaggedChargesWhateverTheNote() {
			Patient patient=PayPlanDb.CreatePatient("DeleteDown");
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patient.PatNum,300,DateTime.Today);
			PayPlanCharge payPlanChargeDown=NewDebit(payPlan,100,"Enganche");//Translated note, as an office using another language would have.
			payPlanChargeDown.IsDownPayment=true;
			PayPlanCharges.Insert(payPlanChargeDown);
			PayPlanCharge payPlanChargeOrdinary=NewDebit(payPlan,100,"");
			PayPlanCharges.Insert(payPlanChargeOrdinary);
			List<PayPlanCharge> listPayPlanChargesNotDeleted=PayPlanCharges.DeleteDebitsWithoutPayments(
				new List<PayPlanCharge> { payPlanChargeDown,payPlanChargeOrdinary },doDelete:true);
			CollectionAssert.AreEqual(new List<long> { payPlanChargeDown.PayPlanChargeNum },
				listPayPlanChargesNotDeleted.Select(x => x.PayPlanChargeNum).ToList());
			List<long> listPayPlanChargeNumsDb=PayPlanCharges.GetForPayPlan(payPlan.PayPlanNum).Select(x => x.PayPlanChargeNum).ToList();
			CollectionAssert.AreEqual(new List<long> { payPlanChargeDown.PayPlanChargeNum },listPayPlanChargeNumsDb);
		}

		[TestMethod]
		public void SheetRows_DownPaymentComesFromTheFlagNotTheNote() {
			DataTable table=NewPayPlanSheetTable();
			PayPlan payPlan=new PayPlan { PayPlanNum=1,PatNum=1 };
			//Translated down payment note: 24.3 would have renumbered it as an ordinary charge.
			PayPlanCharge payPlanChargeDown=NewDebit(payPlan,100,"Enganche");
			payPlanChargeDown.IsDownPayment=true;
			PayPlanCharge payPlanChargeOrdinary=NewDebit(payPlan,100,"");
			payPlanChargeOrdinary.ChargeDate=DateTime.Today.AddMonths(1);
			DataRow rowDown=PayPlanEdit.CreateRowForPayPlanChargeDT(table,payPlanChargeDown,0,false);
			DataRow rowOrdinary=PayPlanEdit.CreateRowForPayPlanChargeDT(table,payPlanChargeOrdinary,1,false);
			table.Rows.Add(rowDown);
			table.Rows.Add(rowOrdinary);
			Assert.IsTrue(PayPlanEdit.IsDownPaymentRow(rowDown));
			Assert.IsFalse(PayPlanEdit.IsDownPaymentRow(rowOrdinary));
			DataTable tableSorted=table.Clone();
			DataRow rowDownSorted=PayPlanEdit.CreateRowForPayPlanListDT(tableSorted,rowDown,0,false);
			DataRow rowOrdinarySorted=PayPlanEdit.CreateRowForPayPlanListDT(tableSorted,rowOrdinary,0,false);
			Assert.AreEqual(rowDown["Description"].ToString(),rowDownSorted["Description"].ToString(),"A down payment keeps its description.");
			Assert.IsTrue(PayPlanEdit.IsDownPaymentRow(rowDownSorted),"The flag is carried to the sorted rows.");
			Assert.AreEqual("#1",rowOrdinarySorted["Description"].ToString(),"Ordinary charges are numbered.");
			Assert.IsFalse(PayPlanEdit.IsDownPaymentRow(rowOrdinarySorted));
		}

		[TestMethod]
		public void SheetRows_NoteTextAloneIsNotADownPayment() {
			DataTable table=NewPayPlanSheetTable();
			PayPlanCharge payPlanCharge=NewDebit(new PayPlan { PayPlanNum=1,PatNum=1 },100,"down payment reminder");
			DataRow row=PayPlanEdit.CreateRowForPayPlanChargeDT(table,payPlanCharge,4,false);
			table.Rows.Add(row);
			DataRow rowSorted=PayPlanEdit.CreateRowForPayPlanListDT(table.Clone(),row,0,false);
			Assert.IsFalse(PayPlanEdit.IsDownPaymentRow(rowSorted));
			StringAssert.StartsWith(rowSorted["Description"].ToString(),"#1");
		}

		[TestMethod]
		public void Schema_IsDownPaymentColumnIsNotNull() {
			//The backfill of payplancharge.IsDownPayment (M1-01) is tested in ForkMigrationTests; here only that the column the code reads exists.
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() "
				+"AND TABLE_NAME='payplancharge' AND COLUMN_NAME='IsDownPayment' AND IS_NULLABLE='YES'"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() "
				+"AND TABLE_NAME='payplancharge' AND COLUMN_NAME='IsDownPayment'"));
		}

		private static PayPlanCharge NewDebit(PayPlan payPlan,double principal,string note) {
			return new PayPlanCharge {
				PayPlanNum=payPlan.PayPlanNum,
				PatNum=payPlan.PatNum,
				Guarantor=payPlan.PatNum,
				ChargeDate=DateTime.Today,
				Principal=principal,
				Note=note,
				ChargeType=PayPlanChargeType.Debit,
			};
		}

		///<summary>The columns of the payment plan sheet grid table (SheetDataTableUtil.GetTable_PatientPayPlanMain).</summary>
		private static DataTable NewPayPlanSheetTable() {
			DataTable table=new DataTable();
			table.Columns.AddRange(new[] {
				new DataColumn("ChargeDate",typeof(string)),
				new DataColumn("Provider",typeof(string)),
				new DataColumn("Description",typeof(string)),
				new DataColumn("Principal",typeof(string)),
				new DataColumn("Interest",typeof(string)),
				new DataColumn("Due",typeof(string)),
				new DataColumn("Payment",typeof(string)),
				new DataColumn("Adjustment",typeof(string)),
				new DataColumn("Balance",typeof(string)),
				new DataColumn("Type",typeof(string)),
				new DataColumn("paramIsBold",typeof(bool)),
			});
			return table;
		}
	}
}
