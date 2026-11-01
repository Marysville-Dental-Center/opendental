// v25_1-account-salestax-sort-bottom: in the account ledger, and on statements that use the ledger sort, sales tax rows (procedures with the
// sales-tax code and adjustments of the sales-tax adjustment type) go after every other row of their date, keeping the usual order among
// themselves. Order across dates does not change.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BACCTTests {
	[TestClass]
	public class SalesTaxLedgerDbTests {
		private static readonly DateTime _date=DateTime.Today.AddDays(-2);

		[TestInitialize]
		public void TestInitialize() {
			TestDbAssembly.ConnectOrSkip();
			SalesTaxData.SetUp(10,SalesTaxData.CODE_TAX);
		}

		[TestCleanup]
		public void TestCleanup() {
			if(TestDbAssembly.SkipReason!=null) {
				return;
			}
			SalesTaxData.SetUp(0,"");
		}

		///<summary>On one date: a plain procedure, the sales-tax procedure, another plain procedure, a sales-tax adjustment and another adjustment, in
		///that insert order; the next day another plain procedure.</summary>
		private class Ledger {
			public Patient Patient;
			public Procedure ProcPlain1;
			public Procedure ProcTax;
			public Procedure ProcPlain2;
			public Adjustment AdjTax;
			public Adjustment AdjOther;
			public Procedure ProcNextDay;
		}

		private static Ledger CreateLedger(string lName) {
			Ledger ledger=new Ledger();
			ledger.Patient=SalesTaxData.CreatePatient(lName);
			ledger.ProcPlain1=SalesTaxData.CreateProc(ledger.Patient,SalesTaxData.CODE_PLAIN,100,ProcStat.C,_date);
			ledger.ProcTax=SalesTaxData.CreateProc(ledger.Patient,SalesTaxData.CODE_TAX,15,ProcStat.C,_date);
			ledger.ProcPlain2=SalesTaxData.CreateProc(ledger.Patient,SalesTaxData.CODE_PLAIN,50,ProcStat.C,_date);
			ledger.AdjTax=SalesTaxData.CreateAdjustment(ledger.Patient,15,_date,PrefC.GetLong(PrefName.SalesTaxAdjustmentType));
			ledger.AdjOther=SalesTaxData.CreateAdjustment(ledger.Patient,-5,_date,SalesTaxData.OtherAdjType());
			ledger.ProcNextDay=SalesTaxData.CreateProc(ledger.Patient,SalesTaxData.CODE_PLAIN,70,ProcStat.C,_date.AddDays(1));
			return ledger;
		}

		///<summary>"P123" for a procedure row, "A45" for an adjustment row, in grid order.</summary>
		private static List<string> GetKeys(DataTable tableAccount) {
			List<string> listKeys=new List<string>();
			foreach(DataRow dataRow in tableAccount.Rows) {
				if(dataRow["ProcNum"].ToString()!="0") {
					listKeys.Add("P"+dataRow["ProcNum"]);
				}
				else if(dataRow["AdjNum"].ToString()!="0") {
					listKeys.Add("A"+dataRow["AdjNum"]);
				}
			}
			return listKeys;
		}

		private static DataTable GetAccountModuleTable(long patNum) {
			//The Account module: not intermingled, ledger sort (AccountLineComparer).
			return AccountModules.GetAll(patNum,DateTime.MinValue,DateTime.Today.AddDays(1),false,false,false,false,false,false).DataSetMain.Tables["account"];
		}

		private static DataTable GetStatementTable(long patNum,bool isIntermingled) {
			Statement statement=new Statement();
			statement.PatNum=patNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.MinValue;
			statement.DateRangeTo=DateTime.Today.AddDays(1);
			statement.Mode_=StatementMode.InPerson;
			statement.SinglePatient=true;
			statement.Intermingled=isIntermingled;
			statement.StatementType=StmtType.NotSet;
			statement.Note="";
			statement.NoteBold="";
			return AccountModules.GetAccount(patNum,statement).Tables["account"];
		}

		private static void AssertTaxLast(Ledger ledger,List<string> listKeys) {
			List<string> listExpectedLast=new List<string> { "P"+ledger.ProcTax.ProcNum,"A"+ledger.AdjTax.AdjNum };
			int indexNextDay=listKeys.IndexOf("P"+ledger.ProcNextDay.ProcNum);
			Assert.AreEqual(6,listKeys.Count,string.Join(",",listKeys));
			Assert.AreEqual(5,indexNextDay,"The next day's row stays after every row of the earlier date.");
			CollectionAssert.AreEqual(listExpectedLast,listKeys.GetRange(3,2),"Tax rows last within the date, procedure before adjustment.");
			CollectionAssert.AreEquivalent(new List<string> { "P"+ledger.ProcPlain1.ProcNum,"P"+ledger.ProcPlain2.ProcNum,"A"+ledger.AdjOther.AdjNum },
				listKeys.GetRange(0,3));
			Assert.AreEqual("A"+ledger.AdjOther.AdjNum,listKeys[2],"Other rows keep the usual order: procedures, then adjustments.");
		}

		[TestMethod]
		public void AccountModule_PutsSalesTaxRowsLastWithinTheirDate() {
			Ledger ledger=CreateLedger("LedgerModule");
			AssertTaxLast(ledger,GetKeys(GetAccountModuleTable(ledger.Patient.PatNum)));
		}

		[TestMethod]
		public void Statement_Intermingled_PutsSalesTaxRowsLast() {
			Ledger ledger=CreateLedger("LedgerStmtMingled");
			AssertTaxLast(ledger,GetKeys(GetStatementTable(ledger.Patient.PatNum,isIntermingled:true)));
		}

		[TestMethod]
		public void Statement_ByPatient_PutsSalesTaxRowsLast() {
			Ledger ledger=CreateLedger("LedgerStmtByPat");
			AssertTaxLast(ledger,GetKeys(GetStatementTable(ledger.Patient.PatNum,isIntermingled:false)));
		}

		[TestMethod]
		public void SuperFamilyStatement_PutsSalesTaxRowsLast() {
			Ledger ledger=CreateLedger("LedgerSuperFam");
			Patient patientOld=ledger.Patient.Copy();
			ledger.Patient.SuperFamily=ledger.Patient.PatNum;
			ledger.Patient.HasSuperBilling=true;
			OpenDentBusiness.Crud.PatientCrud.Update(ledger.Patient,patientOld);
			Statement statement=new Statement();
			statement.PatNum=ledger.Patient.PatNum;
			statement.SuperFamily=ledger.Patient.PatNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.MinValue;
			statement.DateRangeTo=DateTime.Today.AddDays(1);
			statement.Mode_=StatementMode.InPerson;
			statement.StatementType=StmtType.NotSet;
			statement.Note="";
			statement.NoteBold="";
			DataTable table=AccountModules.GetSuperFamAccount(statement).Tables["account"];
			AssertTaxLast(ledger,GetKeys(table));
		}

		[TestMethod]
		public void BalanceAfterTheDate_IsUnchanged() {
			Ledger ledger=CreateLedger("LedgerBalance");
			DataTable table=GetAccountModuleTable(ledger.Patient.PatNum);
			DataRow rowLast=table.Rows.Cast<DataRow>().Last(x => x["ProcNum"].ToString()==ledger.ProcNextDay.ProcNum.ToString());
			Assert.AreEqual(100+15+50+15-5+70,(double)(decimal)rowLast["balanceDouble"],0.0001);
		}

		[TestMethod]
		public void WithoutTheProcCode_SalesTaxAdjustmentsStillGoLast() {
			SalesTaxData.SetUp(10,"");
			Ledger ledger=CreateLedger("LedgerAdjOnly");
			List<string> listKeys=GetKeys(GetAccountModuleTable(ledger.Patient.PatNum));
			Assert.AreEqual("A"+ledger.AdjTax.AdjNum,listKeys[4],"The sales-tax adjustment is the last row of its date.");
			Assert.AreEqual("A"+ledger.AdjOther.AdjNum,listKeys[3]);
			Assert.IsTrue(listKeys.IndexOf("P"+ledger.ProcTax.ProcNum)<3,"Without the setting the code is an ordinary procedure.");
		}

		[TestMethod]
		public void NothingConfigured_KeepsThe24_3Order() {
			SalesTaxData.SetUp(10,"");
			Prefs.UpdateLong(PrefName.SalesTaxAdjustmentType,0);
			Ledger ledger=CreateLedger("LedgerNone");
			List<string> listKeys=GetKeys(GetAccountModuleTable(ledger.Patient.PatNum));
			CollectionAssert.AreEquivalent(new List<string> { "P"+ledger.ProcPlain1.ProcNum,"P"+ledger.ProcTax.ProcNum,"P"+ledger.ProcPlain2.ProcNum },
				listKeys.GetRange(0,3),"Procedures first.");
			CollectionAssert.AreEqual(new List<string> { "A"+ledger.AdjTax.AdjNum,"A"+ledger.AdjOther.AdjNum },listKeys.GetRange(3,2),"Adjustments by key.");
		}
	}

	///<summary>SalesTaxLedgerOrder on its own, without a database.</summary>
	[TestClass]
	public class SalesTaxLedgerOrderTests {
		private static readonly DateTime _date=new DateTime(2026,10,20);

		[TestMethod]
		public void LedgerOrder_IsInactive_WhenNothingCanBeSalesTax() {
			SalesTaxLedgerOrder salesTaxLedgerOrder=new SalesTaxLedgerOrder("",null);
			Assert.IsFalse(salesTaxLedgerOrder.IsActive);
			Comparison<DataRow> comparison=(x,y) => 0;
			Assert.AreSame(comparison,salesTaxLedgerOrder.Wrap(comparison,isByPatient:false),"The ledger sort runs unchanged.");
			Assert.IsTrue(new SalesTaxLedgerOrder(SalesTaxData.CODE_TAX,null).IsActive);
			Assert.IsTrue(new SalesTaxLedgerOrder("",new List<long> { 5 }).IsActive);
		}

		[TestMethod]
		public void Wrap_ByPatient_KeepsPatientsApart_AndDatesInOrder() {
			//Rows as SortRowsForStatmentPrinting sees them: patient first, then date, then type.
			DataTable table=new DataTable();
			table.Columns.Add("PatNum",typeof(long));
			table.Columns.Add("DateTime",typeof(DateTime));
			table.Columns.Add("ProcNum");
			table.Columns.Add("AdjNum");
			table.Columns.Add("ProcCode");
			table.Rows.Add(2L,_date,"0","9","");//patient 2, tax adjustment
			table.Rows.Add(1L,_date,"11","0",SalesTaxData.CODE_TAX);//patient 1, tax procedure
			table.Rows.Add(1L,_date,"10","0",SalesTaxData.CODE_PLAIN);
			table.Rows.Add(1L,_date.AddDays(-1),"0","8","");//earlier date, tax adjustment
			table.Rows.Add(2L,_date,"12","0",SalesTaxData.CODE_PLAIN);
			Comparison<DataRow> comparisonBase=(x,y) => {
				int patCompare=((long)x["PatNum"]).CompareTo((long)y["PatNum"]);
				if(patCompare!=0) {
					return patCompare;
				}
				int dateCompare=((DateTime)x["DateTime"]).CompareTo((DateTime)y["DateTime"]);
				if(dateCompare!=0) {
					return dateCompare;
				}
				return string.Compare(x["ProcNum"].ToString()+x["AdjNum"],y["ProcNum"].ToString()+y["AdjNum"],StringComparison.Ordinal);
			};
			SalesTaxLedgerOrder salesTaxLedgerOrder=new SalesTaxLedgerOrder(SalesTaxData.CODE_TAX,new List<long> { 8,9 });
			List<DataRow> listRows=table.Rows.Cast<DataRow>().ToList();
			listRows.Sort(salesTaxLedgerOrder.Wrap(comparisonBase,isByPatient:true));
			List<string> listKeys=listRows.Select(x => x["PatNum"]+":"+x["ProcNum"]+"/"+x["AdjNum"]).ToList();
			CollectionAssert.AreEqual(new List<string> { "1:0/8","1:10/0","1:11/0","2:12/0","2:0/9" },listKeys);
		}

		[TestMethod]
		public void IsSalesTaxRow_ProcedureByCode_AdjustmentByKey() {
			DataTable table=new DataTable();
			table.Columns.Add("ProcNum");
			table.Columns.Add("AdjNum");
			table.Columns.Add("ProcCode");
			DataRow rowTaxProc=table.Rows.Add("5","0",SalesTaxData.CODE_TAX);
			DataRow rowPlainProc=table.Rows.Add("6","0",SalesTaxData.CODE_PLAIN);
			DataRow rowTaxAdj=table.Rows.Add("0","7","Adjust");
			DataRow rowOtherAdj=table.Rows.Add("0","8","Adjust");
			DataRow rowPayment=table.Rows.Add("0","0","Pay");
			SalesTaxLedgerOrder salesTaxLedgerOrder=new SalesTaxLedgerOrder(SalesTaxData.CODE_TAX,new List<long> { 7 });
			Assert.IsTrue(salesTaxLedgerOrder.IsSalesTaxRow(rowTaxProc));
			Assert.IsFalse(salesTaxLedgerOrder.IsSalesTaxRow(rowPlainProc));
			Assert.IsTrue(salesTaxLedgerOrder.IsSalesTaxRow(rowTaxAdj));
			Assert.IsFalse(salesTaxLedgerOrder.IsSalesTaxRow(rowOtherAdj));
			Assert.IsFalse(salesTaxLedgerOrder.IsSalesTaxRow(rowPayment));
			Assert.AreEqual(1,salesTaxLedgerOrder.CompareSalesTaxLast(rowTaxProc,rowPlainProc));
			Assert.AreEqual(-1,salesTaxLedgerOrder.CompareSalesTaxLast(rowPayment,rowTaxAdj));
			Assert.AreEqual(0,salesTaxLedgerOrder.CompareSalesTaxLast(rowTaxProc,rowTaxAdj),"Two tax rows keep the ledger's own order.");
			Assert.AreEqual(0,salesTaxLedgerOrder.CompareSalesTaxLast(rowPlainProc,rowOtherAdj));
			Assert.IsFalse(new SalesTaxLedgerOrder("",new List<long> { 7 }).IsSalesTaxRow(rowTaxProc),"No code set: no procedure is sales tax.");
		}
	}
}
