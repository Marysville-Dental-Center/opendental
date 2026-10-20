// Statement and invoice fields of Open Dental 25.1 and 25.2 (backlog package A-STMT):
//  - v25_1-account-invoice-adjustment-total: an invoice stores the total of its adjustments in statement.InsEst; other statements keep the
//    family insurance estimate there.
//  - v25_2-account-statement-show-trans-since-zero: each statement remembers "Show transactions since zero balance" in
//    statement.ShowTransSinceBalZero. New statements take the BillingShowTransSinceBalZero preference, statements made by Billing take the
//    Billing Options setting, and a saved statement keeps its own value when it is opened or printed again.
// Runs on a scratch database built from the 24.3.63 seed plus the fork migrations, with synthetic patients and adjustments only. Skipped
// (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace StatementTests {
	[TestClass]
	public class StatementFieldTests {
		private static readonly string DB_NAME=TestDb.ScratchName("fields");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateMigrated(DB_NAME);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("STATEMENT_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
		}

		#region Helpers
		///<summary>A made-up guarantor with no transactions.</summary>
		private static Patient CreatePatient(string lName) {
			Patient patient=new Patient();
			patient.LName=lName;
			patient.FName="Synthetic";
			patient.PatStatus=PatientStatus.Patient;
			patient.Address="1 Test Street";
			patient.City="Testville";
			patient.State="OR";
			patient.Zip="97000";
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.Birthdate=new DateTime(1980,1,1);
			patient.PatNum=OpenDentBusiness.Crud.PatientCrud.Insert(patient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			OpenDentBusiness.Crud.PatientCrud.Update(patient,patientOld);
			return patient;
		}

		private static long InsertAdjustment(long patNum,double amount,DateTime date,long statementNum) {
			Adjustment adjustment=new Adjustment();
			adjustment.PatNum=patNum;
			adjustment.AdjAmt=amount;
			adjustment.AdjDate=date;
			adjustment.ProcDate=date;
			adjustment.DateEntry=date;
			adjustment.AdjType=Defs.GetDefsForCategory(DefCat.AdjTypes,true).First().DefNum;
			adjustment.StatementNum=statementNum;
			return OpenDentBusiness.Crud.AdjustmentCrud.Insert(adjustment);
		}

		private static Statement NewStatement(long patNum,bool isInvoice) {
			Statement statement=new Statement();
			statement.PatNum=patNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.MinValue;
			statement.DateRangeTo=DateTime.Today;
			statement.Mode_=StatementMode.InPerson;
			statement.IsInvoice=isInvoice;
			statement.SinglePatient=isInvoice;
			statement.StatementType=StmtType.NotSet;
			statement.Note="";
			statement.NoteBold="";
			statement.IsBalValid=true;
			return statement;
		}

		private static void SetPrefShowTransSinceBalZero(bool value) {
			Prefs.UpdateBool(PrefName.BillingShowTransSinceBalZero,value);
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual(value,PrefC.GetBool(PrefName.BillingShowTransSinceBalZero));
		}

		private static double GetInsEstFromDb(long statementNum) {
			return PIn.Double(DataCore.GetScalar("SELECT InsEst FROM statement WHERE StatementNum="+POut.Long(statementNum)));
		}
		#endregion Helpers

		#region v25_1-account-invoice-adjustment-total
		[TestMethod]
		public void Invoice_SaveStoresAdjustmentTotalInInsEst() {
			Patient patient=CreatePatient("InvoiceAdjTotal");
			Statement invoice=NewStatement(patient.PatNum,isInvoice:true);
			invoice.InsEst=999;//What a 24.3 invoice used to store: the family insurance estimate.
			Statements.Insert(invoice);
			Assert.AreEqual(0,invoice.InsEst,"Nothing is attached yet when the invoice is first inserted.");
			Assert.AreEqual(0,GetInsEstFromDb(invoice.StatementNum));
			Statement otherInvoice=NewStatement(patient.PatNum,isInvoice:true);
			Statements.Insert(otherInvoice);
			InsertAdjustment(patient.PatNum,50,DateTime.Today,invoice.StatementNum);
			InsertAdjustment(patient.PatNum,-20.25,DateTime.Today,invoice.StatementNum);
			InsertAdjustment(patient.PatNum,100,DateTime.Today,0);//Not on any invoice.
			InsertAdjustment(patient.PatNum,7,DateTime.Today,otherInvoice.StatementNum);//On a different invoice.
			Assert.AreEqual(29.75,Statements.GetInvoiceAdjustmentTotal(invoice.StatementNum),0.0001);
			invoice.InsEst=999;
			Statements.Update(invoice);
			Assert.AreEqual(29.75,invoice.InsEst,0.0001);
			Assert.AreEqual(29.75,GetInsEstFromDb(invoice.StatementNum),0.0001);
			Assert.AreEqual(29.75,Statements.GetStatement(invoice.StatementNum).InsEst,0.0001);
			//The update that compares with the old row stores it too.
			Statement invoiceOld=Statements.GetStatement(invoice.StatementNum);
			invoiceOld.InsEst=0;
			Statement invoiceNew=invoiceOld.Copy();
			invoiceNew.InsEst=999;
			invoiceNew.Note="changed";
			Statements.Update(invoiceNew,invoiceOld);
			Assert.AreEqual(29.75,GetInsEstFromDb(invoice.StatementNum),0.0001);
			Statements.Update(otherInvoice);
			Assert.AreEqual(7,GetInsEstFromDb(otherInvoice.StatementNum),0.0001);
		}

		[TestMethod]
		public void Invoice_SetInvoiceAdjustmentTotal_OnlyChangesInvoices() {
			Patient patient=CreatePatient("InvoiceSetTotal");
			Statement invoice=NewStatement(patient.PatNum,isInvoice:true);
			Statements.Insert(invoice);
			InsertAdjustment(patient.PatNum,-15,DateTime.Today,invoice.StatementNum);
			invoice.InsEst=123;
			Statements.SetInvoiceAdjustmentTotal(invoice);
			Assert.AreEqual(-15,invoice.InsEst,0.0001,"Credit adjustments count negative, as AdjAmt stores them.");
			Statement statement=NewStatement(patient.PatNum,isInvoice:false);
			statement.InsEst=123.45;
			Statements.SetInvoiceAdjustmentTotal(statement);
			Assert.AreEqual(123.45,statement.InsEst,0.0001);
			Statements.SetInvoiceAdjustmentTotal(null);//Does not throw.
			Assert.AreEqual(0,Statements.GetInvoiceAdjustmentTotal(0));
		}

		[TestMethod]
		public void Statement_KeepsFamilyInsuranceEstimate() {
			Patient patient=CreatePatient("StatementInsEst");
			Statement statement=NewStatement(patient.PatNum,isInvoice:false);
			statement.InsEst=123.45;
			statement.BalTotal=500;
			Statements.Insert(statement);
			//Even with an adjustment that points at it, a regular statement keeps the insurance estimate.
			InsertAdjustment(patient.PatNum,40,DateTime.Today,statement.StatementNum);
			Statements.Update(statement);
			Assert.AreEqual(123.45,GetInsEstFromDb(statement.StatementNum),0.0001);
		}

		[TestMethod]
		public void GetFamilyBalance_SkipsInvoices() {
			Patient patient=CreatePatient("FamilyBalance");
			Statement statement=NewStatement(patient.PatNum,isInvoice:false);
			statement.IsSent=true;
			statement.BalTotal=300;
			statement.InsEst=100;
			Statements.Insert(statement);
			Statement invoice=NewStatement(patient.PatNum,isInvoice:true);
			invoice.IsSent=true;
			invoice.BalTotal=300;
			Statements.Insert(invoice);
			InsertAdjustment(patient.PatNum,-50,DateTime.Today,invoice.StatementNum);
			Statements.Update(invoice);
			Assert.AreEqual(-50,GetInsEstFromDb(invoice.StatementNum),0.0001);
			//The newest sent statement is the invoice, whose InsEst is not an insurance estimate, so the balance comes from the statement.
			Assert.AreEqual(200,Statements.GetFamilyBalance(patient.PatNum),0.0001);
		}
		#endregion v25_1-account-invoice-adjustment-total

		#region v25_2-account-statement-show-trans-since-zero
		[TestMethod]
		public void ShowTransSinceBalZero_StoredPerStatement() {
			Patient patient=CreatePatient("ShowTransStored");
			SetPrefShowTransSinceBalZero(false);
			Statement statementOn=NewStatement(patient.PatNum,isInvoice:false);
			statementOn.ShowTransSinceBalZero=true;
			Statements.Insert(statementOn);
			SetPrefShowTransSinceBalZero(true);
			Statement statementOff=NewStatement(patient.PatNum,isInvoice:false);
			statementOff.ShowTransSinceBalZero=false;
			Statements.Insert(statementOff);
			//Reopening a statement shows the value it was saved with, whatever the preference says now.
			SetPrefShowTransSinceBalZero(false);
			Assert.IsTrue(Statements.GetStatement(statementOn.StatementNum).ShowTransSinceBalZero);
			SetPrefShowTransSinceBalZero(true);
			Assert.IsFalse(Statements.GetStatement(statementOff.StatementNum).ShowTransSinceBalZero);
			Statement statementOnLoaded=Statements.GetStatement(statementOn.StatementNum);
			statementOnLoaded.ShowTransSinceBalZero=false;
			Statements.Update(statementOnLoaded);
			Assert.IsFalse(Statements.GetStatement(statementOn.StatementNum).ShowTransSinceBalZero);
		}

		[TestMethod]
		public void ShowTransSinceBalZero_NewStatementsTakeThePreference() {
			Patient patient=CreatePatient("ShowTransDefault");
			foreach(bool value in new[] { true,false }) {
				SetPrefShowTransSinceBalZero(value);
				Assert.AreEqual(value,Statements.GetShowTransSinceBalZeroDefault());
				Assert.AreEqual(value,Statements.GenerateStatement(patient,DateTime.MinValue,DateTime.MinValue,StatementMode.InPerson).ShowTransSinceBalZero);
				Assert.AreEqual(value,Statements.CreateReceiptStatement(patient,StatementMode.InPerson).ShowTransSinceBalZero);
			}
		}

		///<summary>Reprinting a saved statement uses its own ShowTransSinceBalZero, never the preference: with the setting on, the date range starts
		///when the balance was last zero; with it off, the date range is left as saved.</summary>
		[TestMethod]
		public void ShowTransSinceBalZero_ReprintHonorsTheStatementsOwnValue() {
			Patient patient=CreatePatient("ShowTransReprint");
			//The balance goes up, back to zero, then up again.
			InsertAdjustment(patient.PatNum,100,DateTime.Today.AddDays(-60),0);
			InsertAdjustment(patient.PatNum,-100,DateTime.Today.AddDays(-40),0);
			InsertAdjustment(patient.PatNum,50,DateTime.Today.AddDays(-20),0);
			Ledgers.ComputeAging(patient.Guarantor,DateTime.Today);
			List<PatAging> listPatAgings=Patients.GetAgingListSimple(new List<long>(),new List<long> { patient.Guarantor },true);
			DataTable tableBals=Ledgers.GetDateBalanceBegan(listPatAgings,false);
			Assert.AreEqual(1,tableBals.Rows.Count);
			DateTime dateZeroBal=PIn.Date(tableBals.Rows[0]["DateZeroBal"].ToString());
			Assert.IsTrue(dateZeroBal.Year>1880,"The synthetic ledger has a date the balance was last zero.");
			Assert.IsTrue(dateZeroBal>=DateTime.Today.AddDays(-40) && dateZeroBal<=DateTime.Today.AddDays(-20),dateZeroBal.ToShortDateString());
			DateTime dateRangeFromSaved=DateTime.Today.AddDays(-365);
			//Saved with the setting on; the preference is off now.
			Statement statementOn=NewStatement(patient.PatNum,isInvoice:false);
			statementOn.DateRangeFrom=dateRangeFromSaved;
			statementOn.ShowTransSinceBalZero=true;
			Statements.Insert(statementOn);
			SetPrefShowTransSinceBalZero(false);
			Statement statementReprint=Statements.GetStatement(statementOn.StatementNum);
			Assert.IsTrue(Statements.ApplyShowTransSinceBalZero(statementReprint,false));
			Assert.AreEqual(dateZeroBal,statementReprint.DateRangeFrom);
			//Saved with the setting off; the preference is on now.
			Statement statementOff=NewStatement(patient.PatNum,isInvoice:false);
			statementOff.DateRangeFrom=dateRangeFromSaved;
			statementOff.ShowTransSinceBalZero=false;
			Statements.Insert(statementOff);
			SetPrefShowTransSinceBalZero(true);
			statementReprint=Statements.GetStatement(statementOff.StatementNum);
			Assert.IsFalse(Statements.ApplyShowTransSinceBalZero(statementReprint,false));
			Assert.AreEqual(dateRangeFromSaved,statementReprint.DateRangeFrom);
			//A statement made from the Account module follows the statement's value too.
			SetPrefShowTransSinceBalZero(false);
			Statement statementGenerated=Statements.GenerateStatement(patient,DateTime.MinValue,DateTime.MinValue,StatementMode.InPerson);
			Assert.IsFalse(statementGenerated.ShowTransSinceBalZero);
			Assert.AreNotEqual(dateZeroBal,statementGenerated.DateRangeFrom);
			SetPrefShowTransSinceBalZero(true);
			statementGenerated=Statements.GenerateStatement(patient,DateTime.MinValue,DateTime.MinValue,StatementMode.InPerson);
			Assert.IsTrue(statementGenerated.ShowTransSinceBalZero);
			Assert.AreEqual(dateZeroBal,statementGenerated.DateRangeFrom);
		}

		///<summary>Billing stores its Billing Options setting on each statement it makes, overriding the preference.</summary>
		[TestMethod]
		public void ShowTransSinceBalZero_BillingStoresBillingOptionsValue() {
			Patient patient=CreatePatient("ShowTransBilling");
			InsertAdjustment(patient.PatNum,80,DateTime.Today.AddDays(-45),0);
			Ledgers.ComputeAging(patient.Guarantor,DateTime.Today);
			foreach(bool value in new[] { true,false }) {
				SetPrefShowTransSinceBalZero(value);
				DataCore.NonQ("DELETE FROM statement WHERE PatNum="+POut.Long(patient.PatNum));
				List<Statement> listStatements=BillingL.CreateStatementHelper(0,new SendStatementsIO());
				Statement statement=listStatements.FirstOrDefault(x => x.PatNum==patient.PatNum);
				Assert.IsNotNull(statement,"Billing made a statement for the synthetic patient.");
				Assert.AreEqual(value,statement.ShowTransSinceBalZero);
				Assert.AreEqual(value,Statements.GetStatement(statement.StatementNum).ShowTransSinceBalZero);
			}
		}
		#endregion v25_2-account-statement-show-trans-since-zero
	}
}
