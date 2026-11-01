// v24_4-reports-dailypayments-merchant-fees: the Daily Payments report's single "Show merchant fees" option. One Merchant Fee column and one set of
// totals for CareCredit history, every PayConnect source and any other processor that records a fee; fees on their own row under the payment,
// for patient payments only. Signs as in 24.3: CareCredit stores its fee with the opposite sign and is negated; "after fees" is the amount plus the
// fee as shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BACCTTests {
	[TestClass]
	public class MerchantFeeTests {
		private const string COLUMN_PROVIDERS="GROUP_CONCAT(DISTINCT provider.Abbr)";

		///<summary>The patient payments table as RpPaySheet.GetPatTable returns it (with clinics).</summary>
		private static DataTable NewPatTable() {
			DataTable table=new DataTable();
			table.Columns.Add("DatePay");
			table.Columns.Add("lfname");
			table.Columns.Add(COLUMN_PROVIDERS);
			table.Columns.Add("clinicAbbr");
			table.Columns.Add("CheckNum");
			table.Columns.Add("amt");
			table.Columns.Add("PayNum");
			table.Columns.Add("ItemName");
			table.Columns.Add("PayType");
			table.Columns.Add("MerchantFee");
			table.Columns.Add("PaymentSource");
			return table;
		}

		private static void AddRow(DataTable table,long payNum,decimal amt,decimal merchantFee,CreditCardSource source,string prov) {
			table.Rows.Add("2026-10-20","Synthetic, Pat "+payNum,prov,"Main","",amt.ToString(),payNum.ToString(),"Card","1",merchantFee.ToString(),
				((int)source).ToString());
		}

		private static decimal Sum(DataTable table,string column) {
			return table.Rows.Cast<DataRow>().Sum(x => PIn.Decimal(x[column].ToString()));
		}

		[TestMethod]
		public void Sources_CareCreditPayConnectAndOtherProcessors_NotNone() {
			Assert.IsTrue(RpPaySheet.IsMerchantFeeSource(CreditCardSource.CareCredit));
			foreach(CreditCardSource source in new[] { CreditCardSource.PayConnect,CreditCardSource.XServerPayConnect,CreditCardSource.PayConnectPortal,
				CreditCardSource.PayConnectPortalLogin,CreditCardSource.PayConnectPaymentPortal,CreditCardSource.PayConnectPaymentPortalGuest })
			{
				Assert.IsTrue(RpPaySheet.IsMerchantFeeSource(source),source.ToString());
			}
			Assert.IsTrue(RpPaySheet.IsMerchantFeeSource(CreditCardSource.EdgeExpressCNP),"Any processor that records a fee (our decision).");
			Assert.IsFalse(RpPaySheet.IsMerchantFeeSource(CreditCardSource.None),"A hand-typed surcharge is not a merchant fee.");
		}

		[TestMethod]
		public void FeeForReport_NegatesCareCreditOnly() {
			Assert.AreEqual(-3m,RpPaySheet.GetMerchantFeeForReport(CreditCardSource.CareCredit,3m));
			Assert.AreEqual(2.5m,RpPaySheet.GetMerchantFeeForReport(CreditCardSource.PayConnect,2.5m));
			Assert.AreEqual(-1.25m,RpPaySheet.GetMerchantFeeForReport(CreditCardSource.PayConnectPaymentPortal,-1.25m));
			Assert.AreEqual(0m,RpPaySheet.GetMerchantFeeForReport(CreditCardSource.None,4m));
		}

		[TestMethod]
		public void FeeRows_OnePerPaymentAfterItsLastRow_WithoutProviderOrClinic() {
			DataTable table=NewPatTable();
			AddRow(table,1,60,3,CreditCardSource.CareCredit,"DOC1");//Payment 1 is split between two providers, so it has two rows.
			AddRow(table,1,40,3,CreditCardSource.CareCredit,"HYG1");
			AddRow(table,2,100,2.5m,CreditCardSource.PayConnect,"DOC1");
			AddRow(table,3,50,0,CreditCardSource.PayConnect,"DOC1");//No fee: no fee row.
			AddRow(table,4,25,1,CreditCardSource.None,"DOC1");//Not a processor: no fee row.
			RpPaySheet.AddMerchantFeeRows(table,hasClinicsEnabled:true);
			Assert.AreEqual(7,table.Rows.Count);
			List<string> listPayNumsAndAmts=table.Rows.Cast<DataRow>().Select(x => x["PayNum"]+":"+x["amt"]).ToList();
			CollectionAssert.AreEqual(new List<string> { "1:60","1:40","1:0","2:100","2:0","3:50","4:25" },listPayNumsAndAmts);
			DataRow rowFee=table.Rows[2];
			Assert.AreEqual("",rowFee[COLUMN_PROVIDERS].ToString());
			Assert.AreEqual("",rowFee["clinicAbbr"].ToString());
			Assert.AreEqual(3m,PIn.Decimal(rowFee["MerchantFee"].ToString()),"The fee stays on the fee row only.");
			Assert.AreEqual(3m+2.5m,Sum(table,"MerchantFee"),"Each fee counts once; other rows are cleared.");
		}

		[TestMethod]
		public void FeeColumn_AndAfterFeeTotals() {
			DataTable table=NewPatTable();
			AddRow(table,1,60,3,CreditCardSource.CareCredit,"DOC1");
			AddRow(table,1,40,3,CreditCardSource.CareCredit,"HYG1");
			AddRow(table,2,100,2.5m,CreditCardSource.PayConnect,"DOC1");
			AddRow(table,3,80,1.75m,CreditCardSource.EdgeExpressRCM,"DOC1");
			AddRow(table,4,25,1,CreditCardSource.None,"DOC1");
			RpPaySheet.AddMerchantFeeRows(table,hasClinicsEnabled:true);
			RpPaySheet.AddAfterFeeColumn(table);
			RpPaySheet.AddMerchantFeeColumn(table);
			Assert.AreEqual(table.Columns["amt"].Ordinal-1,table.Columns[RpPaySheet.COLUMN_MERCHANT_FEE].Ordinal,"Just before the amount.");
			decimal totalPayments=Sum(table,"amt");
			decimal totalFees=Sum(table,RpPaySheet.COLUMN_MERCHANT_FEE);
			decimal totalAfter=Sum(table,RpPaySheet.COLUMN_AFTER_FEE);
			Assert.AreEqual(305m,totalPayments,"Fee rows carry no amount.");
			Assert.AreEqual(-3m+2.5m+1.75m,totalFees,"Total Merchant Fees: CareCredit negated, the others as stored, None left out.");
			Assert.AreEqual(totalPayments+totalFees,totalAfter,"Total Patient Payments After Fees.");
			DataRow rowCareCreditFee=table.Rows.Cast<DataRow>().Single(x => x["PayNum"].ToString()=="1" && x["amt"].ToString()=="0");
			Assert.AreEqual(-3m,PIn.Decimal(rowCareCreditFee[RpPaySheet.COLUMN_MERCHANT_FEE].ToString()));
			Assert.AreEqual(-3m,PIn.Decimal(rowCareCreditFee[RpPaySheet.COLUMN_AFTER_FEE].ToString()));
		}

		[TestMethod]
		public void WithoutClinics_TheFeeRowKeepsItsColumns() {
			DataTable table=NewPatTable();
			table.Columns.Remove("clinicAbbr");
			table.Rows.Add("2026-10-20","Synthetic, Pat","DOC1","","100","9","Card","1","2",((int)CreditCardSource.PayConnect).ToString());
			RpPaySheet.AddMerchantFeeRows(table,hasClinicsEnabled:false);
			Assert.AreEqual(2,table.Rows.Count);
			Assert.AreEqual("0",table.Rows[1]["amt"].ToString());
		}

		[TestMethod]
		public void NullTables_AreIgnored() {
			RpPaySheet.AddMerchantFeeRows(null,true);
			RpPaySheet.AddAfterFeeColumn(null);
			RpPaySheet.AddMerchantFeeColumn(null);
		}
	}

	///<summary>The merchant-fee steps on the table the report's query really returns, on the scratch database.</summary>
	[TestClass]
	public class MerchantFeeDbTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDbAssembly.ConnectOrSkip();
		}

		private static void InsertPayment(Patient patient,DateTime date,double amount,double merchantFee,CreditCardSource source) {
			Payment payment=new Payment();
			payment.PatNum=patient.PatNum;
			payment.PayDate=date;
			payment.PayAmt=amount;
			payment.PayType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true).First().DefNum;
			payment.PaymentSource=source;
			payment.MerchantFee=merchantFee;
			payment.PayNum=OpenPmsDentalBusiness.Crud.PaymentCrud.Insert(payment);
			PaySplit paySplit=new PaySplit();
			paySplit.PayNum=payment.PayNum;
			paySplit.PatNum=patient.PatNum;
			paySplit.ProvNum=SalesTaxData.ProvNumDefault();
			paySplit.SplitAmt=amount;
			paySplit.DatePay=date;
			OpenPmsDentalBusiness.Crud.PaySplitCrud.Insert(paySplit);
		}

		[TestMethod]
		public void PatTable_FeesAndTotals() {
			DateTime date=new DateTime(2026,9,15);//A day no other test uses.
			Patient patient=SalesTaxData.CreatePatient("MerchantFees");
			InsertPayment(patient,date,100,3,CreditCardSource.CareCredit);
			InsertPayment(patient,date,200,4.5,CreditCardSource.PayConnect);
			InsertPayment(patient,date,50,0,CreditCardSource.None);
			DataTable table=RpPaySheet.GetPatTable(date,date,new List<long>(),new List<long>(),new List<long>(),true,true,true,false,true,false,true);
			Assert.AreEqual(3,table.Rows.Count);
			RpPaySheet.AddMerchantFeeRows(table,PrefC.HasClinicsEnabled);
			RpPaySheet.AddAfterFeeColumn(table);
			RpPaySheet.AddMerchantFeeColumn(table);
			Assert.AreEqual(5,table.Rows.Count,"One fee row for each of the two payments with a fee.");
			decimal totalPayments=table.Rows.Cast<DataRow>().Sum(x => PIn.Decimal(x["amt"].ToString()));
			decimal totalFees=table.Rows.Cast<DataRow>().Sum(x => PIn.Decimal(x[RpPaySheet.COLUMN_MERCHANT_FEE].ToString()));
			decimal totalAfter=table.Rows.Cast<DataRow>().Sum(x => PIn.Decimal(x[RpPaySheet.COLUMN_AFTER_FEE].ToString()));
			Assert.AreEqual(350m,totalPayments);
			Assert.AreEqual(-3m+4.5m,totalFees);
			Assert.AreEqual(totalPayments+totalFees,totalAfter);
		}
	}
}
