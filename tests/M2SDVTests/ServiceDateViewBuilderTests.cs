// The service-date view builder (ServiceDateViews.Build), without a database: the 24.3 report's layout and the fork's options for statements
// and date ranges (package M2-SDV). Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SDVTests {
	[TestClass]
	public class ServiceDateViewBuilderTests {
		private static readonly DateTime _date1=new DateTime(2026,3,2);
		private static readonly DateTime _date2=new DateTime(2026,3,9);
		private static readonly DateTime _date3=new DateTime(2026,4,1);

		#region Helpers
		private static ServiceDateViewCoreRow Proc(long procNum,DateTime date,decimal fee,decimal procCredits=0,decimal insCredits=0,long patNum=1,
			string patient="Ann")
		{
			return new ServiceDateViewCoreRow { TranType="Proc",TranNum=procNum.ToString(),DateService=date,DateTrans=DateTime.MinValue,PatNum=patNum,
				PatientName=patient,ProcNum=procNum,Reference="Z1001:Exam",Charge=fee,ProcCredits=procCredits,InsCredits=insCredits,ProvAbbr="DOC",
				ProvNum=1,CodeNum=5,ProcCode="Z1001" };
		}

		private static ServiceDateViewCoreRow Tran(string type,long tranNum,DateTime dateService,DateTime dateTrans,decimal charge,decimal credit,
			long procNum=0,long parentNum=0,long patNum=1,string patient="Ann",string reference=null)
		{
			string tranNumText=type.StartsWith("WriteOff") ? "W"+tranNum : tranNum.ToString();
			return new ServiceDateViewCoreRow { TranType=type,TranNum=tranNumText,DateService=dateService,DateTrans=dateTrans,PatNum=patNum,
				PatientName=patient,ProcNum=procNum,Reference=reference??(type+" "+tranNum),Charge=charge,Credit=credit,ProvAbbr="DOC",ProvNum=1,
				ParentNum=parentNum };
		}

		private static List<string> Types(List<ServiceDateViewRow> listRows) {
			return listRows.Select(x => x.TranType).ToList();
		}
		#endregion Helpers

		[TestMethod]
		public void Build_ProcedureThenItsCreditsIndented_ThenDayAndOverallTotals() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Tran("InsPay-Att.",30,_date1,_date2,0,40,procNum:10,parentNum:7),
				Proc(10,_date1,100,procCredits:70),
				Tran("PatPay Att.",20,_date1,_date1,0,30,procNum:10,parentNum:3),
			};
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions());
			CollectionAssert.AreEqual(new[] { "Proc","PatPay Att.","InsPay-Att.","Day Total","Overall Total" },Types(listRows));
			Assert.AreEqual("Z1001:Exam",listRows[0].Reference);
			Assert.AreEqual("  PatPay Att. 20",listRows[1].Reference,"Credits attached to a procedure are indented.");
			Assert.IsTrue(listRows[1].IsAttachedCredit);
			ServiceDateViewRow rowDay=listRows[3];
			Assert.AreEqual(100m,rowDay.Charge);
			Assert.AreEqual(70m,rowDay.Credit);
			Assert.AreEqual(30m,rowDay.AcctBal);
			Assert.AreEqual(_date1,rowDay.DateService);
			Assert.AreEqual(ServiceDateViews.REFERENCE_PATIENT_TOTAL,listRows[4].Reference);
			Assert.AreEqual(30m,listRows[0].Balance,"A procedure's balance is its charge less the credits applied to it.");
			Assert.IsTrue(listRows[0].ShowsBalance);
		}

		[TestMethod]
		public void Build_SameDay_PatientsByFirstNameThenHigherProcNumFirst() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(10,_date1,50,patNum:2,patient:"ben"),
				Proc(11,_date1,60,patNum:1,patient:"Ann"),
				Proc(12,_date1,70,patNum:1,patient:"Ann"),
				Tran("Adj-Att.",5,_date1,_date1,0,10,procNum:11),
			};
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions { IsFamily=true });
			CollectionAssert.AreEqual(new long[] { 12,11,11,10,0,0 },listRows.Select(x => x.ProcNum).ToList());
			Assert.AreEqual("Adj-Att.",listRows[2].TranType);
			Assert.AreEqual(ServiceDateViews.REFERENCE_FAMILY_TOTAL,listRows.Last().Reference);
		}

		[TestMethod]
		public void Build_UnattachedTransactions_UnderUnallocatedHeadingAfterProcedures() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Tran("PatPay",21,_date1,_date1,0,25,parentNum:4),
				Tran("Adj",6,_date1,_date1,15,0),
				Proc(10,_date1,100),
			};
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions());
			CollectionAssert.AreEqual(new[] { "Proc","Unallocated","PatPay","Adj","Day Total","Overall Total" },Types(listRows));
			Assert.AreEqual(ServiceDateViewRowKind.UnallocatedHeading,listRows[1].RowKind);
			Assert.AreEqual("PatPay 21",listRows[2].Reference,"Unattached transactions are not indented.");
			Assert.AreEqual(115m,listRows[4].Charge);
			Assert.AreEqual(25m,listRows[4].Credit);
		}

		[TestMethod]
		public void Build_Dates_AscendingWithOneTotalEach_OverallLast() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(20,_date3,30),
				Proc(10,_date1,10),
				Proc(15,_date2,20),
			};
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions());
			CollectionAssert.AreEqual(new[] { "Proc","Day Total","Proc","Day Total","Proc","Day Total","Overall Total" },Types(listRows));
			CollectionAssert.AreEqual(new[] { _date1,_date1,_date2,_date2,_date3,_date3 },listRows.Take(6).Select(x => x.DateService).ToList());
			Assert.AreEqual(60m,listRows.Last().Charge);
			Assert.AreEqual(60m,listRows.Last().AcctBal);
		}

		[TestMethod]
		public void Build_InsBalAndAcctBal_DetailedOnProceduresAlwaysOnTotals() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(10,_date1,200,procCredits:20,insCredits:120),
				Tran("WriteOff-Att.",31,_date1,_date1,0,0,procNum:10,parentNum:8),
				Tran("PatPay Att.",22,_date1,_date1,0,20,procNum:10,parentNum:5),
			};
			List<ServiceDateViewRow> listNotDetailed=ServiceDateViews.Build(listCore,new ServiceDateViewOptions { IsDetailed=false });
			Assert.IsFalse(listNotDetailed[0].ShowsInsAndAcctBal);
			Assert.IsTrue(listNotDetailed.Where(x => x.IsTotal).All(x => x.ShowsInsAndAcctBal));
			Assert.IsFalse(listNotDetailed.Where(x => x.IsAttachedCredit).Any(x => x.ShowsInsAndAcctBal));
			List<ServiceDateViewRow> listDetailed=ServiceDateViews.Build(listCore,new ServiceDateViewOptions { IsDetailed=true });
			Assert.IsTrue(listDetailed[0].ShowsInsAndAcctBal);
			Assert.AreEqual(120m,listDetailed[0].InsBal);
			Assert.AreEqual(60m,listDetailed[0].AcctBal,"Charge less credits less the insurance still expected.");
			ServiceDateViewRow rowDay=listDetailed.Single(x => x.RowKind==ServiceDateViewRowKind.DayTotal);
			Assert.AreEqual(120m,rowDay.InsBal);
			Assert.AreEqual(60m,rowDay.AcctBal);
		}

		[TestMethod]
		public void Build_SameTransactionNumberDateAndType_OneRowSummed() {
			//Dynamic payment plan credits come as one row per payment plan link.
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Tran("Dynamic PayPlan Credit",3,_date1.AddHours(9),_date1.AddHours(9),0,40,parentNum:3,reference:"PayPlan Credit"),
				Tran("Dynamic PayPlan Credit",3,_date1.AddHours(11),_date1.AddHours(11),0,60,parentNum:3,reference:"PayPlan Credit"),
			};
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions());
			CollectionAssert.AreEqual(new[] { "Unallocated","Dynamic PayPlan Credit","Day Total","Overall Total" },Types(listRows));
			Assert.AreEqual(100m,listRows[1].Credit,"Links of one plan on one date are one row (the 24.3 report kept the time, so it split them).");
			Assert.AreEqual(_date1,listRows[1].DateService);
		}

		[TestMethod]
		public void Build_DateRange_BalanceForwardFirstAndLaterServicesLeftOut() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(10,_date1,100,procCredits:30,insCredits:50),
				Tran("PatPay Att.",20,_date1,_date3,0,30,procNum:10,parentNum:3),//Paid after the range: travels with its service.
				Proc(11,_date2,80),
				Proc(12,_date3,60),
			};
			ServiceDateViewOptions options=new ServiceDateViewOptions { DateFrom=_date2,DateTo=_date2,IsDetailed=true };
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,options);
			CollectionAssert.AreEqual(new[] { "Balance Forward","Proc","Day Total","Overall Total" },Types(listRows));
			ServiceDateViewRow rowForward=listRows[0];
			Assert.AreEqual(70m,rowForward.Balance);
			Assert.AreEqual(50m,rowForward.InsBal);
			Assert.AreEqual(20m,rowForward.AcctBal);
			ServiceDateViewRow rowOverall=listRows.Last();
			Assert.AreEqual(80m,rowOverall.Charge,"Charges and credits of the range only.");
			Assert.AreEqual(150m,rowOverall.Balance,"Ending balance includes the balance forward.");
			Assert.AreEqual(100m,rowOverall.AcctBal);
		}

		[TestMethod]
		public void Build_DateRangeOpenEnded_NoBalanceForwardWhenNothingBefore() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> { Proc(10,_date2,100) };
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions { DateFrom=_date1,DateTo=new DateTime(2200,1,1) });
			CollectionAssert.AreEqual(new[] { "Proc","Day Total","Overall Total" },Types(listRows));
		}

		[TestMethod]
		public void Build_Receipt_PaymentsInRangeWithTheProceduresTheyPay() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(10,_date1,100,procCredits:90),
				Tran("InsPay-Att.",30,_date1,_date2,0,60,procNum:10,parentNum:7),
				Tran("PatPay Att.",20,_date1,_date3,0,30,procNum:10,parentNum:3),//Paid today, on an older procedure.
				Tran("PatPay Att.",23,_date1,_date1,0,5,procNum:10,parentNum:2),//Paid earlier.
				Tran("PatPay",24,_date3,_date3,0,15,parentNum:3),//Prepayment today.
				Proc(11,_date3,40),//Done today, not paid today.
			};
			ServiceDateViewOptions options=new ServiceDateViewOptions { IsReceipt=true,DateFrom=_date3,DateTo=_date3 };
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,options);
			CollectionAssert.AreEqual(new[] { "Proc","PatPay Att.","Day Total","Unallocated","PatPay","Day Total","Overall Total" },Types(listRows));
			Assert.AreEqual(10,listRows[0].ProcNum);
			Assert.AreEqual("20",listRows[1].TranNum);
			Assert.AreEqual(10m,listRows[0].Balance,"The procedure shows what is still owed on it after every credit.");
			Assert.IsFalse(listRows.Any(x => x.TranType=="Balance Forward"));
		}

		[TestMethod]
		public void Build_Invoice_ItsChargesWithTheCreditsAppliedToThem() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(10,_date1,100,procCredits:25),
				Tran("PatPay Att.",20,_date1,_date2,0,25,procNum:10,parentNum:3),
				Proc(11,_date1,40),
				Tran("Adj",6,_date2,_date2,12,0),
				Tran("Adj",7,_date2,_date2,8,0),
				Tran("PayPlan Charge",9,_date3,_date3,50,0,parentNum:4),
				Tran("PatPay",21,_date3,_date3,0,10,parentNum:3),
			};
			ServiceDateViewInvoiceItems invoiceItems=new ServiceDateViewInvoiceItems();
			invoiceItems.ListProcNums.Add(10);
			invoiceItems.ListAdjNums.Add(6);
			invoiceItems.ListPayPlanChargeNums.Add(9);
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(listCore,new ServiceDateViewOptions { InvoiceItems=invoiceItems,DateFrom=_date3 });
			CollectionAssert.AreEqual(new[] { "Proc","PatPay Att.","Day Total","Unallocated","Adj","Day Total","Unallocated","PayPlan Charge","Day Total",
				"Overall Total" },Types(listRows));
			Assert.AreEqual(162m,listRows.Last().Charge);
			Assert.IsFalse(listRows.Any(x => x.TranType=="Balance Forward"),"Invoices ignore the date range.");
		}

		[TestMethod]
		public void Build_NoRows_OnlyAZeroOverallTotal() {
			List<ServiceDateViewRow> listRows=ServiceDateViews.Build(new List<ServiceDateViewCoreRow>(),new ServiceDateViewOptions());
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual(ServiceDateViewRowKind.OverallTotal,listRows[0].RowKind);
			Assert.AreEqual(0m,listRows[0].Charge);
		}

		[TestMethod]
		public void Build_DoesNotChangeTheInputRows() {
			ServiceDateViewCoreRow coreRow=Tran("PatPay Att.",20,_date1.AddHours(3),_date1,0,30,procNum:10);
			ServiceDateViews.Build(new List<ServiceDateViewCoreRow> { coreRow },new ServiceDateViewOptions());
			Assert.AreEqual("PatPay Att. 20",coreRow.Reference);
			Assert.AreEqual(_date1.AddHours(3),coreRow.DateService);
		}

		[TestMethod]
		public void ToReportTable_HasThe24_3ColumnsAndBlanks() {
			List<ServiceDateViewCoreRow> listCore=new List<ServiceDateViewCoreRow> {
				Proc(10,_date1,100,procCredits:30),
				Tran("PatPay Att.",20,_date1,_date2,0,30,procNum:10,parentNum:3),
				Tran("PatPay",21,_date1,_date1,0,5,parentNum:3),
			};
			DataTable table=ServiceDateViews.ToReportTable(ServiceDateViews.Build(listCore,new ServiceDateViewOptions()));
			CollectionAssert.IsSubsetOf(new[] { "Date","Trans Date","Patient","Reference","Charge","Credit","Pvdr","InsBal","AcctBal","Type","PrimaryKey","PatNum" },
				table.Columns.OfType<DataColumn>().Select(x => x.ColumnName).ToList());
			Assert.AreEqual("2026-03-02",table.Rows[0]["Date"]);
			Assert.AreEqual("",table.Rows[0]["Trans Date"]);
			Assert.AreEqual("",table.Rows[0]["InsBal"],"Not detailed: no balances on procedures.");
			Assert.AreEqual("",table.Rows[1]["Date"]);
			Assert.AreEqual("2026-03-09",table.Rows[1]["Trans Date"]);
			Assert.AreEqual("Unallocated",table.Rows[2]["Type"]);
			Assert.AreEqual("",table.Rows[2]["Charge"]);
			Assert.AreEqual("2026-03-02",table.Rows[2]["Date"]);
			Assert.AreEqual("Day Total",table.Rows[4]["Type"]);
			Assert.AreEqual(100m,PIn.Decimal(table.Rows[4]["Charge"].ToString()));
			Assert.AreEqual("",table.Rows[5]["Date"]);
			Assert.AreEqual("",table.Rows[5]["Patient"]);
		}

		[TestMethod]
		public void ReadCoreRows_ReadsTypedAndTextColumns() {
			DataTable table=new DataTable();
			foreach(string column in new[] { "Type","TranNum","Patient","Reference","Prov","ProcCode","ToothNum","ToothRange","Surf" }) {
				table.Columns.Add(column);
			}
			table.Columns.Add("ProcDate",typeof(DateTime));
			table.Columns.Add("TranDate");//Text, as the union of a text literal and dates returns it.
			table.Columns.Add("PatNum",typeof(long));
			table.Columns.Add("ProcNum",typeof(long));
			table.Columns.Add("Charge",typeof(double));
			table.Columns.Add("Credit",typeof(decimal));
			table.Columns.Add("ProcCredits",typeof(decimal));
			table.Columns.Add("InsCredits");
			table.Columns.Add("ClinicNum",typeof(long));
			table.Columns.Add("ProvNum",typeof(long));
			table.Columns.Add("ParentNum");
			table.Columns.Add("CodeNum",typeof(long));
			DataRow dataRow=table.NewRow();
			dataRow["Type"]="WriteOff-Att.";
			dataRow["TranNum"]="W77";
			dataRow["Patient"]="Ann";
			dataRow["Reference"]="W/O- Check";
			dataRow["Prov"]=DBNull.Value;
			dataRow["ProcDate"]=_date1;
			dataRow["TranDate"]="2026-03-09";
			dataRow["PatNum"]=4L;
			dataRow["ProcNum"]=10L;
			dataRow["Charge"]=0d;
			dataRow["Credit"]=12.5m;
			dataRow["ProcCredits"]=0m;
			dataRow["InsCredits"]="0";
			dataRow["ClinicNum"]=2L;
			dataRow["ProvNum"]=3L;
			dataRow["ParentNum"]="55";
			dataRow["CodeNum"]=0L;
			table.Rows.Add(dataRow);
			ServiceDateViewCoreRow coreRow=ServiceDateViews.ReadCoreRows(table).Single();
			Assert.AreEqual("W77",coreRow.TranNum);
			Assert.AreEqual(_date1,coreRow.DateService);
			Assert.AreEqual(_date2,coreRow.DateTrans);
			Assert.AreEqual(12.5m,coreRow.Credit);
			Assert.AreEqual("",coreRow.ProvAbbr);
			Assert.AreEqual(55,coreRow.ParentNum);
			Assert.AreEqual(2,coreRow.ClinicNum);
			ServiceDateViewRow row=ServiceDateViews.Build(new List<ServiceDateViewCoreRow> { coreRow },new ServiceDateViewOptions())[0];
			Assert.AreEqual(77,row.GetPriKey());
		}

		[TestMethod]
		public void TypeHelpers_Classify24_3Types() {
			Assert.IsTrue(ServiceDateViews.IsPatPayType("PatPay Att. PayPlan"));
			Assert.IsTrue(ServiceDateViews.IsInsuranceType("WriteOff"));
			Assert.IsTrue(ServiceDateViews.IsAdjustmentType("Adj-Att."));
			Assert.IsTrue(ServiceDateViews.IsPayPlanChargeType("PayPlan Charge Att."));
			Assert.IsTrue(ServiceDateViews.IsPayPlanType("Dynamic PayPlan Credit"));
			Assert.IsTrue(ServiceDateViews.IsPayPlanType("PayPlan Credit"));
			Assert.IsFalse(ServiceDateViews.IsPayPlanType("PatPay PayPlan"));
			Assert.IsTrue(ServiceDateViews.IsAttachedType("Proc"));
			Assert.IsFalse(ServiceDateViews.IsAttachedType("PatPay"));
		}

		[TestMethod]
		public void FormatAmount_TwoDecimalsReadBackByPIn() {
			Assert.AreEqual(1234.57m,PIn.Decimal(ServiceDateViews.FormatAmount(1234.565m)));
			Assert.AreEqual(-5m,PIn.Decimal(ServiceDateViews.FormatAmount(-5m)));
		}

		[TestMethod]
		public void StatementTypes_LimitedCustomAndSuperFamilyCannotUseServiceDateView() {
			Assert.IsTrue(Statements.CanUseServiceDateView(new Statement()));
			Assert.IsFalse(Statements.CanUseServiceDateView(new Statement { StatementType=StmtType.LimitedStatement }));
			Assert.IsFalse(Statements.CanUseServiceDateView(new Statement { SuperFamily=5 }));
			Assert.IsFalse(Statements.CanUseServiceDateView(new Statement { LimitedCustomFamily=EnumLimitedCustomFamily.Family }));
			Assert.IsFalse(Statements.CanUseServiceDateView(null));
			Assert.IsTrue(Statements.CanUseServiceDateView(new Statement { IsReceipt=true }));
			Assert.IsTrue(Statements.CanUseServiceDateView(new Statement { IsInvoice=true }));
			Assert.IsTrue(Statements.CanUseServiceDateView(new Statement { StatementType=StmtType.Walkout }));
			Statement statement=new Statement { StatementType=StmtType.LimitedStatement };
			Statements.SetServiceDateView(statement,true);
			Assert.IsFalse(statement.IsServiceDateView);
			Assert.IsFalse(Statements.IsShownInServiceDateView(new Statement { IsServiceDateView=true,SuperFamily=3 }));
			statement=new Statement();
			Statements.SetServiceDateView(statement,true);
			Assert.IsTrue(statement.IsServiceDateView);
			Assert.IsTrue(Statements.IsShownInServiceDateView(statement));
		}
	}
}
