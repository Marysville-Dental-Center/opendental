// Service Date View on a scratch database (package M2-SDV): the report rows still match Open Dental 24.3's query, statements are built in
// service-date layout from the same rows, per-user settings, the window and Account grid cells, and the Billing default. One synthetic family:
// Ann (guarantor) and Ben, with procedures, an attached and an unattached adjustment, payment splits, received and pending insurance and an
// insurance payment made as a total. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.SheetFramework;

namespace M2SDVTests {
	[TestClass]
	public class ServiceDateViewDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("sdv");
		private static string _skipReason;
		private static readonly DateTime _date1=DateTime.Today.AddDays(-60);
		private static readonly DateTime _date2=DateTime.Today.AddDays(-30);
		private static readonly DateTime _date3=DateTime.Today.AddDays(-10);
		private static Patient _patientAnn;
		private static Patient _patientBen;
		private static long _provNum;
		private static long _codeNumExam;
		private static long _codeNumFilling;
		private static long _procNum1;
		private static long _procNum2;
		private static long _procNum3;
		private static long _procNum4;
		private static long _procNum5;
		private static long _adjNumAttached;
		private static long _adjNumUnattached;
		private static long _payNum1;
		private static long _payNum2;
		private static long _claimNumReceived;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateMigrated(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			CreateSyntheticFamily();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("M2SDV_KEEP_DB")=="1") {
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

		#region Synthetic data
		private static void CreateSyntheticFamily() {
			Provider provider=new Provider { Abbr="SYNDOC",FName="Syn",LName="Doctor" };
			_provNum=OpenDentBusiness.Crud.ProviderCrud.Insert(provider);
			_codeNumExam=OpenDentBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="Z9101",Descript="Synthetic exam",AbbrDesc="SynEx",
				TreatArea=TreatmentArea.Mouth,ProcCat=Defs.GetDefsForCategory(DefCat.ProcCodeCats,true).First().DefNum });
			_codeNumFilling=OpenDentBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="Z9102",Descript="Synthetic filling",
				AbbrDesc="SynFil",TreatArea=TreatmentArea.Tooth,ProcCat=Defs.GetDefsForCategory(DefCat.ProcCodeCats,true).First().DefNum });
			Cache.Refresh(InvalidType.ProcCodes,InvalidType.Providers);
			_patientAnn=CreatePatient("Ann",0);
			_patientBen=CreatePatient("Ben",_patientAnn.PatNum);
			_procNum1=InsertProc(_patientAnn.PatNum,_codeNumFilling,_date1,100,"8");
			_procNum2=InsertProc(_patientAnn.PatNum,_codeNumExam,_date1,60,"");
			_procNum3=InsertProc(_patientBen.PatNum,_codeNumExam,_date1,80,"");
			_procNum4=InsertProc(_patientAnn.PatNum,_codeNumExam,_date2,200,"");
			_procNum5=InsertProc(_patientBen.PatNum,_codeNumFilling,_date3,150,"3");
			_adjNumAttached=InsertAdjustment(_patientAnn.PatNum,-10,_date1,_procNum1);
			_adjNumUnattached=InsertAdjustment(_patientAnn.PatNum,25,_date2,0);
			_payNum1=InsertPayment(_patientAnn.PatNum,_date2,70);
			InsertSplit(_payNum1,_patientAnn.PatNum,50,_date2,_procNum1);
			InsertSplit(_payNum1,_patientBen.PatNum,20,_date2,_procNum3);
			_payNum2=InsertPayment(_patientAnn.PatNum,_date3,40);
			InsertSplit(_payNum2,_patientAnn.PatNum,40,_date3,0);
			//Received insurance on procedure 4, plus a payment made as a total on the same claim.
			long claimPaymentNum=OpenDentBusiness.Crud.ClaimPaymentCrud.Insert(new ClaimPayment { CheckDate=_date3,CheckAmt=135,CarrierName="Synthetic Ins",
				Note="",PayType=Defs.GetDefsForCategory(DefCat.InsurancePaymentType,true).FirstOrDefault()?.DefNum??0 });
			_claimNumReceived=InsertClaim(_patientAnn.PatNum,_date2,"R");
			InsertClaimProc(_claimNumReceived,_patientAnn.PatNum,_procNum4,_date2,ClaimProcStatus.Received,insPayAmt:120,writeOff:30,insPayEst:120,
				dateCP:_date3,claimPaymentNum:claimPaymentNum);
			InsertClaimProc(_claimNumReceived,_patientAnn.PatNum,0,_date2,ClaimProcStatus.Received,insPayAmt:15,writeOff:0,insPayEst:0,dateCP:_date3,
				claimPaymentNum:claimPaymentNum);
			//Pending insurance on procedure 5.
			long claimNumPending=InsertClaim(_patientBen.PatNum,_date3,"S");
			InsertClaimProc(claimNumPending,_patientBen.PatNum,_procNum5,_date3,ClaimProcStatus.NotReceived,insPayAmt:0,writeOff:10,insPayEst:90,
				dateCP:DateTime.MinValue,claimPaymentNum:0);
			//A patient payment plan for procedure 2: its credit, and one charge due. Which rows the report shows depends on PayPlansVersion.
			PayPlan payPlan=new PayPlan { PatNum=_patientAnn.PatNum,Guarantor=_patientAnn.PatNum,PayPlanDate=_date2,CompletedAmt=30,Note="" };
			long payPlanNum=OpenDentBusiness.Crud.PayPlanCrud.Insert(payPlan);
			OpenDentBusiness.Crud.PayPlanChargeCrud.Insert(new PayPlanCharge { PayPlanNum=payPlanNum,PatNum=_patientAnn.PatNum,Guarantor=_patientAnn.PatNum,
				ChargeDate=_date2,Principal=30,ChargeType=PayPlanChargeType.Credit,ProcNum=_procNum2,ProvNum=_provNum,Note="" });
			OpenDentBusiness.Crud.PayPlanChargeCrud.Insert(new PayPlanCharge { PayPlanNum=payPlanNum,PatNum=_patientAnn.PatNum,Guarantor=_patientAnn.PatNum,
				ChargeDate=_date3,Principal=10,ChargeType=PayPlanChargeType.Debit,ProvNum=_provNum,Note="" });
		}

		private static Patient CreatePatient(string fName,long guarantor) {
			Patient patient=new Patient();
			patient.LName="Sdvtest";
			patient.FName=fName;
			patient.PatStatus=PatientStatus.Patient;
			patient.Address="1 Test Street";
			patient.City="Testville";
			patient.State="OR";
			patient.Zip="97000";
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.Birthdate=new DateTime(1980,1,1);
			patient.PriProv=_provNum;
			patient.PatNum=OpenDentBusiness.Crud.PatientCrud.Insert(patient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=guarantor==0 ? patient.PatNum : guarantor;
			OpenDentBusiness.Crud.PatientCrud.Update(patient,patientOld);
			return patient;
		}

		private static long InsertProc(long patNum,long codeNum,DateTime date,double fee,string toothNum) {
			Procedure procedure=new Procedure();
			procedure.PatNum=patNum;
			procedure.CodeNum=codeNum;
			procedure.ProcDate=date;
			procedure.DateEntryC=date;
			procedure.ProcFee=fee;
			procedure.UnitQty=1;
			procedure.ProcStatus=ProcStat.C;
			procedure.ProvNum=_provNum;
			procedure.ToothNum=toothNum;
			procedure.Surf="";
			procedure.ToothRange="";
			return OpenDentBusiness.Crud.ProcedureCrud.Insert(procedure);
		}

		private static long InsertAdjustment(long patNum,double amount,DateTime date,long procNum) {
			Adjustment adjustment=new Adjustment();
			adjustment.PatNum=patNum;
			adjustment.AdjAmt=amount;
			adjustment.AdjDate=date;
			adjustment.ProcDate=date;
			adjustment.DateEntry=date;
			adjustment.ProcNum=procNum;
			adjustment.ProvNum=_provNum;
			adjustment.AdjType=Defs.GetDefsForCategory(DefCat.AdjTypes,true).First().DefNum;
			return OpenDentBusiness.Crud.AdjustmentCrud.Insert(adjustment);
		}

		private static long InsertPayment(long patNum,DateTime date,double amount) {
			Payment payment=new Payment();
			payment.PatNum=patNum;
			payment.PayDate=date;
			payment.DateEntry=date;
			payment.PayAmt=amount;
			payment.PayType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true).FirstOrDefault()?.DefNum??0;
			payment.CheckNum="";
			payment.BankBranch="";
			payment.PayNote="";
			payment.Receipt="";
			payment.ExternalId="";
			return OpenDentBusiness.Crud.PaymentCrud.Insert(payment);
		}

		private static void InsertSplit(long payNum,long patNum,double amount,DateTime date,long procNum) {
			PaySplit paySplit=new PaySplit();
			paySplit.PayNum=payNum;
			paySplit.PatNum=patNum;
			paySplit.SplitAmt=amount;
			paySplit.DatePay=date;
			paySplit.DateEntry=date;
			paySplit.ProcNum=procNum;
			paySplit.ProvNum=_provNum;
			OpenDentBusiness.Crud.PaySplitCrud.Insert(paySplit);
		}

		private static long InsertClaim(long patNum,DateTime dateService,string claimStatus) {
			Claim claim=new Claim();
			claim.PatNum=patNum;
			claim.DateService=dateService;
			claim.DateSent=dateService;
			claim.ClaimStatus=claimStatus;
			claim.ProvTreat=_provNum;
			claim.ProvBill=_provNum;
			claim.ClaimType="P";
			return OpenDentBusiness.Crud.ClaimCrud.Insert(claim);
		}

		private static void InsertClaimProc(long claimNum,long patNum,long procNum,DateTime procDate,ClaimProcStatus status,double insPayAmt,double writeOff,
			double insPayEst,DateTime dateCP,long claimPaymentNum)
		{
			ClaimProc claimProc=new ClaimProc();
			claimProc.ClaimNum=claimNum;
			claimProc.PatNum=patNum;
			claimProc.ProcNum=procNum;
			claimProc.ProcDate=procDate;
			claimProc.Status=status;
			claimProc.InsPayAmt=insPayAmt;
			claimProc.WriteOff=writeOff;
			claimProc.InsPayEst=insPayEst;
			claimProc.DateCP=dateCP;
			claimProc.ClaimPaymentNum=claimPaymentNum;
			claimProc.ProvNum=_provNum;
			claimProc.InsEstTotalOverride=-1;
			claimProc.WriteOffEstOverride=-1;
			claimProc.WriteOffEst=-1;
			claimProc.CopayOverride=-1;
			claimProc.PaidOtherInsOverride=-1;
			claimProc.DedEstOverride=-1;
			claimProc.PercentOverride=-1;
			claimProc.AllowedOverride=-1;
			OpenDentBusiness.Crud.ClaimProcCrud.Insert(claimProc);
		}

		private static Statement NewStatement(long patNum) {
			Statement statement=new Statement();
			statement.PatNum=patNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.MinValue;
			statement.DateRangeTo=new DateTime(2200,1,1);
			statement.Mode_=StatementMode.InPerson;
			statement.StatementType=StmtType.NotSet;
			statement.Note="";
			statement.NoteBold="";
			statement.IsBalValid=true;
			statement.Intermingled=true;
			return statement;
		}

		private static DataSet GetDataSet(Statement statement) {
			return AccountModules.GetAccount(Statements.GetPatNumForGetAccount(statement),statement,doShowHiddenPaySplits:statement.IsReceipt);
		}
		#endregion Synthetic data

		#region The report matches the 24.3 query
		[TestMethod]
		public void GetData_MatchesThe24_3Report_FamilyDetailed() {
			AssertMatchesLegacy(_patientAnn.PatNum,isFamily:true,isDetailed:true);
		}

		[TestMethod]
		public void GetData_MatchesThe24_3Report_FamilyNotDetailed() {
			AssertMatchesLegacy(_patientAnn.PatNum,isFamily:true,isDetailed:false);
		}

		[TestMethod]
		public void GetData_MatchesThe24_3Report_EachPatient() {
			AssertMatchesLegacy(_patientAnn.PatNum,isFamily:false,isDetailed:true);
			AssertMatchesLegacy(_patientBen.PatNum,isFamily:false,isDetailed:true);
			AssertMatchesLegacy(_patientBen.PatNum,isFamily:false,isDetailed:false);
		}

		[TestMethod]
		public void GetData_MatchesThe24_3Report_EveryPayPlanVersion() {
			string versionOld=PrefC.GetStringNoCache(PrefName.PayPlansVersion);
			try {
				foreach(int version in new[] { 1,2,3,4 }) {
					Prefs.UpdateStringNoCache(PrefName.PayPlansVersion,version.ToString());
					AssertMatchesLegacy(_patientAnn.PatNum,isFamily:true,isDetailed:true);
					List<ServiceDateViewRow> listRows=RpServiceDateView.GetRows(_patientAnn.PatNum,true,new ServiceDateViewOptions());
					Assert.AreEqual(version!=4,listRows.Any(x => ServiceDateViews.IsPayPlanType(x.TranType)),"Version "+version);
				}
			}
			finally {
				Prefs.UpdateStringNoCache(PrefName.PayPlansVersion,versionOld);
			}
		}

		private static void AssertMatchesLegacy(long patNum,bool isFamily,bool isDetailed) {
			DataTable tableLegacy=LegacyServiceDateViewReport.GetData(patNum,isFamily,isDetailed);
			DataTable tableNew=RpServiceDateView.GetData(patNum,isFamily,isDetailed);
			Assert.IsTrue(tableLegacy.Rows.Count>3,"The synthetic family has rows.");
			Assert.AreEqual(tableLegacy.Rows.Count,tableNew.Rows.Count,Dump(tableLegacy)+"\r\n---\r\n"+Dump(tableNew));
			for(int i=0;i<tableLegacy.Rows.Count;i++) {
				DataRow rowLegacy=tableLegacy.Rows[i];
				DataRow rowNew=tableNew.Rows[i];
				string where="Row "+i+" ("+rowLegacy["Type"]+")";
				Assert.AreEqual(rowLegacy["Type"].ToString(),rowNew["Type"].ToString(),where);
				Assert.AreEqual(rowLegacy["Reference"].ToString(),rowNew["Reference"].ToString(),where);
				Assert.AreEqual(rowLegacy["Patient"].ToString(),rowNew["Patient"].ToString(),where);
				Assert.AreEqual(rowLegacy["Pvdr"].ToString(),rowNew["Pvdr"].ToString(),where);
				Assert.AreEqual(NormalizeDate(rowLegacy["Date"]),NormalizeDate(rowNew["Date"]),where+" Date");
				Assert.AreEqual(NormalizeDate(rowLegacy["Trans Date"]),NormalizeDate(rowNew["Trans Date"]),where+" Trans Date");
				foreach(string column in new[] { "Charge","Credit","InsBal","AcctBal" }) {
					Assert.AreEqual(NormalizeAmount(rowLegacy[column],CultureInfo.InvariantCulture),NormalizeAmount(rowNew[column],CultureInfo.CurrentCulture),
						where+" "+column);
				}
				if(!rowLegacy["Type"].ToString().In("Unallocated","Day Total","Overall Total")) {//The report leaves these two arbitrary on totals.
					Assert.AreEqual(rowLegacy["PrimaryKey"].ToString(),rowNew["PrimaryKey"].ToString(),where);
					Assert.AreEqual(rowLegacy["PatNum"].ToString(),rowNew["PatNum"].ToString(),where);
				}
			}
		}

		private static string NormalizeDate(object value) {
			if(value==null || value==DBNull.Value || value.ToString()=="") {
				return "";
			}
			if(value is DateTime dateTime) {
				return dateTime.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			}
			string text=value.ToString();
			if(text.Length>=10 && DateTime.TryParseExact(text.Substring(0,10),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime date)) {
				return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			}
			return PIn.DateT(text).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
		}

		private static string NormalizeAmount(object value,CultureInfo cultureInfo) {
			if(value==null || value==DBNull.Value || value.ToString()=="") {
				return "";
			}
			return decimal.Parse(value.ToString(),NumberStyles.Number,cultureInfo).ToString("0.00",CultureInfo.InvariantCulture);
		}

		private static string Dump(DataTable table) {
			return string.Join("\r\n",table.Rows.OfType<DataRow>().Select(x => string.Join(" | ",new[] { "Date","Trans Date","Patient","Reference","Charge","Credit",
				"InsBal","AcctBal","Type" }.Select(y => x[y].ToString()))));
		}

		[TestMethod]
		public void GetCoreTable_HasTheForkColumns() {
			DataTable table=RpServiceDateView.GetCoreTable(_patientAnn.PatNum,true);
			foreach(string column in new[] { "ClinicNum","ProvNum","ParentNum","CodeNum","ProcCode","ToothNum","ToothRange","Surf" }) {
				Assert.IsTrue(table.Columns.Contains(column),column);
			}
			List<ServiceDateViewCoreRow> listCoreRows=ServiceDateViews.ReadCoreRows(table);
			ServiceDateViewCoreRow rowProc=listCoreRows.Single(x => x.TranType=="Proc" && x.ProcNum==_procNum1);
			Assert.AreEqual(_codeNumFilling,rowProc.CodeNum);
			Assert.AreEqual("Z9102",rowProc.ProcCode);
			Assert.AreEqual("8",rowProc.ToothNum);
			Assert.AreEqual(_provNum,rowProc.ProvNum);
			Assert.AreEqual(60m,rowProc.ProcCredits,"Adjustment of -10 and a payment of 50.");
			Assert.AreEqual(_payNum1,listCoreRows.Single(x => x.TranType=="PatPay Att." && x.ProcNum==_procNum1).ParentNum);
			Assert.AreEqual(_claimNumReceived,listCoreRows.Single(x => x.TranType=="InsPay-Att.").ParentNum);
			Assert.AreEqual(_claimNumReceived,listCoreRows.Single(x => x.TranType=="InsPay").ParentNum,"Payment made as a total.");
			Assert.AreEqual(_adjNumUnattached.ToString(),listCoreRows.Single(x => x.TranType=="Adj").TranNum);
		}
		#endregion The report matches the 24.3 query

		#region Statements
		[TestMethod]
		public void Statement_RegularView_KeepsTheTransactionRows() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.IsServiceDateView=false;
			DataSet dataSet=GetDataSet(statement);
			DataTable table=SheetDataTableUtil.GetTable_StatementMain(dataSet,statement);
			Assert.IsFalse(table.Columns.Contains(Statements.COLUMN_IS_BOLD));
			Assert.IsNull(dataSet.Tables[Statements.TABLE_NAME_SERVICE_DATE_VIEW]);
		}

		[TestMethod]
		public void Statement_ServiceDateView_Intermingled_SameRowsAsTheView() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.IsServiceDateView=true;
			DataSet dataSet=GetDataSet(statement);
			DataTable table=SheetDataTableUtil.GetTable_StatementMain(dataSet,statement);
			List<ServiceDateViewRow> listRows=RpServiceDateView.GetRows(_patientAnn.PatNum,true,new ServiceDateViewOptions());
			Assert.AreEqual(listRows.Count,table.Rows.Count);
			for(int i=0;i<listRows.Count;i++) {
				Assert.AreEqual(listRows[i].Charge,(decimal)table.Rows[i]["chargesDouble"],"Row "+i);
				Assert.AreEqual(listRows[i].Credit,(decimal)table.Rows[i]["creditsDouble"],"Row "+i);
				Assert.AreEqual(listRows[i].IsTotal,(bool)table.Rows[i][Statements.COLUMN_IS_BOLD],"Row "+i);
			}
			//The procedure on tooth 8 with its adjustment and payment beneath.
			int idxProc1=listRows.FindIndex(x => x.IsProcedure && x.ProcNum==_procNum1);
			DataRow rowProc1=table.Rows[idxProc1];
			Assert.AreEqual("Z9102",rowProc1["ProcCode"]);
			Assert.AreEqual(Tooth.Display("8"),rowProc1["tth"]);
			StringAssert.Contains(rowProc1["description"].ToString(),"Synthetic filling");
			Assert.AreEqual(40m.ToString("n"),rowProc1["balance"],"100 less the adjustment of 10 and the payment of 50.");
			Assert.AreEqual(_date1.ToShortDateString(),rowProc1["date"]);
			Assert.AreEqual(_procNum1.ToString(),rowProc1["ProcNum"]);
			DataRow rowPayment=table.Rows[listRows.FindIndex(x => x.TranType=="PatPay Att." && x.ProcNum==_procNum1)];
			Assert.AreEqual(_payNum1,(long)rowPayment["PayNum"]);
			Assert.AreEqual(_date2.ToShortDateString(),rowPayment["date"],"Credits show their own date.");
			Assert.AreEqual("0",rowPayment["ProcNum"].ToString());
			Assert.IsTrue(rowPayment["description"].ToString().StartsWith("  "),"Credits are indented under their procedure.");
			DataRow rowOverall=table.Rows[table.Rows.Count-1];
			StringAssert.Contains(rowOverall["description"].ToString(),"Family Total");
			//Every row of an intermingled statement is in the one grid; balances and aging come from the regular data.
			Assert.IsNotNull(dataSet.Tables[Statements.TABLE_NAME_SERVICE_DATE_VIEW],"Kept in the DataSet for redraws.");
		}

		[TestMethod]
		public void Statement_ServiceDateView_NotIntermingled_OneBlockPerPatient() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.Intermingled=false;
			statement.SinglePatient=false;
			statement.IsServiceDateView=true;
			DataSet dataSet=GetDataSet(statement);
			Assert.IsNotNull(dataSet.Tables["account"+_patientAnn.PatNum]);
			DataTable table=SheetDataTableUtil.GetTable_StatementMain(dataSet,statement);
			List<long> listPatNums=table.Rows.OfType<DataRow>().Select(x => (long)x["PatNum"]).ToList();
			//The sheet splits the grid where PatNum changes: Ann's block, then Ben's, each ending with its own total.
			int idxFirstBen=listPatNums.IndexOf(_patientBen.PatNum);
			Assert.IsTrue(idxFirstBen>0);
			Assert.IsTrue(listPatNums.Take(idxFirstBen).All(x => x==_patientAnn.PatNum));
			Assert.IsTrue(listPatNums.Skip(idxFirstBen).All(x => x==_patientBen.PatNum));
			StringAssert.Contains(table.Rows[idxFirstBen-1]["description"].ToString(),"Patient Total");
			StringAssert.Contains(table.Rows[table.Rows.Count-1]["description"].ToString(),"Patient Total");
			Assert.IsFalse(table.Rows.OfType<DataRow>().Take(idxFirstBen).Any(x => x["ProcNum"].ToString()==_procNum3.ToString()));
		}

		[TestMethod]
		public void Statement_ServiceDateView_SinglePatient_OnlyThatPatient() {
			Statement statement=NewStatement(_patientBen.PatNum);
			statement.Intermingled=false;
			statement.SinglePatient=true;
			statement.IsServiceDateView=true;
			DataTable table=SheetDataTableUtil.GetTable_StatementMain(GetDataSet(statement),statement);
			List<string> listProcNums=table.Rows.OfType<DataRow>().Select(x => x["ProcNum"].ToString()).Where(x => x!="0").ToList();
			CollectionAssert.AreEquivalent(new[] { _procNum3.ToString(),_procNum5.ToString() },listProcNums);
			Assert.IsTrue(table.Rows.OfType<DataRow>().All(x => (long)x["PatNum"]==_patientBen.PatNum));
		}

		[TestMethod]
		public void Statement_ServiceDateView_DateRange_BalanceForwardThenTheRange() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.DateRangeFrom=_date2;
			statement.IsServiceDateView=true;
			DataTable table=SheetDataTableUtil.GetTable_StatementMain(GetDataSet(statement),statement);
			Assert.AreEqual(Lans.g("AccountModule","Balance Forward"),table.Rows[0]["description"]);
			//Date 1: Ann 100+60, Ben 80, less the adjustment 10 and the splits 50 and 20.
			Assert.AreEqual(160m,(decimal)table.Rows[0]["balanceDouble"]);
			Assert.IsFalse(table.Rows.OfType<DataRow>().Any(x => x["ProcNum"].ToString()==_procNum1.ToString()),"Services before the range are in the balance forward.");
			Assert.IsTrue(table.Rows.OfType<DataRow>().Any(x => x["ProcNum"].ToString()==_procNum4.ToString()));
		}

		[TestMethod]
		public void Statement_ServiceDateView_Receipt_PaymentsOfTheDayWithTheirProcedures() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.IsReceipt=true;
			statement.DateRangeFrom=_date2;
			statement.DateRangeTo=_date2;
			statement.IsServiceDateView=true;
			DataTable table=SheetDataTableUtil.GetTable_StatementMain(GetDataSet(statement),statement);
			List<string> listProcNums=table.Rows.OfType<DataRow>().Select(x => x["ProcNum"].ToString()).Where(x => x!="0").ToList();
			CollectionAssert.AreEquivalent(new[] { _procNum1.ToString(),_procNum3.ToString() },listProcNums,"The procedures the day's splits pay, though older.");
			List<long> listPayNums=table.Rows.OfType<DataRow>().Select(x => (long)x["PayNum"]).Where(x => x!=0).ToList();
			Assert.AreEqual(2,listPayNums.Count);
			Assert.IsTrue(listPayNums.All(x => x==_payNum1));
			Assert.IsFalse(table.Rows.OfType<DataRow>().Any(x => (long)x["ClaimNum"]!=0),"No insurance on a receipt.");
		}

		[TestMethod]
		public void Statement_ServiceDateView_Invoice_ItsChargesWithTheirCredits() {
			Statement invoice=NewStatement(_patientAnn.PatNum);
			invoice.IsInvoice=true;
			invoice.SinglePatient=true;
			invoice.Intermingled=false;
			invoice.IsServiceDateView=true;
			invoice.StatementNum=Statements.Insert(invoice);
			try {
				DataCore.NonQ("UPDATE procedurelog SET StatementNum="+POut.Long(invoice.StatementNum)+" WHERE ProcNum="+POut.Long(_procNum1));
				DataCore.NonQ("UPDATE adjustment SET StatementNum="+POut.Long(invoice.StatementNum)+" WHERE AdjNum="+POut.Long(_adjNumUnattached));
				DataTable table=SheetDataTableUtil.GetTable_StatementMain(GetDataSet(invoice),invoice);
				List<string> listProcNums=table.Rows.OfType<DataRow>().Select(x => x["ProcNum"].ToString()).Where(x => x!="0").ToList();
				CollectionAssert.AreEqual(new[] { _procNum1.ToString() },listProcNums);
				Assert.IsTrue(table.Rows.OfType<DataRow>().Any(x => (long)x["AdjNum"]==_adjNumUnattached));
				Assert.IsTrue(table.Rows.OfType<DataRow>().Any(x => (long)x["AdjNum"]==_adjNumAttached),"The adjustment applied to the invoiced procedure.");
				Assert.IsTrue(table.Rows.OfType<DataRow>().Any(x => (long)x["PayNum"]==_payNum1),"The split applied to the invoiced procedure.");
				Assert.IsFalse(table.Rows.OfType<DataRow>().Any(x => x["ProcNum"].ToString()==_procNum2.ToString()));
			}
			finally {
				DataCore.NonQ("UPDATE procedurelog SET StatementNum=0 WHERE ProcNum="+POut.Long(_procNum1));
				DataCore.NonQ("UPDATE adjustment SET StatementNum=0 WHERE AdjNum="+POut.Long(_adjNumUnattached));
				DataCore.NonQ("DELETE FROM statement WHERE StatementNum="+POut.Long(invoice.StatementNum));
			}
		}

		[TestMethod]
		public void Statement_LimitedAndSuperFamily_StayInTheRegularView() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.StatementType=StmtType.LimitedStatement;
			statement.IsServiceDateView=true;
			Assert.IsFalse(Statements.IsShownInServiceDateView(statement));
			statement=NewStatement(_patientAnn.PatNum);
			statement.IsServiceDateView=true;
			statement.SuperFamily=_patientAnn.PatNum;
			Assert.IsFalse(Statements.IsShownInServiceDateView(statement));
		}

		[TestMethod]
		public void Statement_SavedFlag_IsUsedWhenTheStatementIsLoadedAgain() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			Statements.SetServiceDateView(statement,true);
			long statementNum=Statements.Insert(statement);
			try {
				Statement statementLoaded=Statements.GetStatement(statementNum);
				Assert.IsTrue(statementLoaded.IsServiceDateView);
				DataTable table=SheetDataTableUtil.GetTable_StatementMain(GetDataSet(statementLoaded),statementLoaded);
				Assert.IsTrue(table.Columns.Contains(Statements.COLUMN_IS_BOLD),"A saved statement prints again in the view it was sent in.");
			}
			finally {
				DataCore.NonQ("DELETE FROM statement WHERE StatementNum="+POut.Long(statementNum));
			}
		}

		[TestMethod]
		public void Statement_ServiceDateView_DoesNotChangeTotalsOrAging() {
			Statement statementRegular=NewStatement(_patientAnn.PatNum);
			Statement statementSdv=NewStatement(_patientAnn.PatNum);
			statementSdv.IsServiceDateView=true;
			DataSet dataSetRegular=GetDataSet(statementRegular);
			DataSet dataSetSdv=GetDataSet(statementSdv);
			SheetDataTableUtil.GetTable_StatementMain(dataSetSdv,statementSdv);//Adds only its own table.
			foreach(DataTable tableRegular in dataSetRegular.Tables) {
				DataTable tableSdv=dataSetSdv.Tables[tableRegular.TableName];
				Assert.IsNotNull(tableSdv,tableRegular.TableName);
				Assert.AreEqual(tableRegular.Rows.Count,tableSdv.Rows.Count,tableRegular.TableName);
				for(int i=0;i<tableRegular.Rows.Count;i++) {
					foreach(DataColumn dataColumn in tableRegular.Columns) {
						Assert.AreEqual(tableRegular.Rows[i][dataColumn.ColumnName].ToString(),tableSdv.Rows[i][dataColumn.ColumnName].ToString(),
							tableRegular.TableName+"."+dataColumn.ColumnName);
					}
				}
			}
			Assert.AreEqual(dataSetRegular.Tables.Count+1,dataSetSdv.Tables.Count);
		}

		[TestMethod]
		public void Statement_ServiceDateView_SheetFillsAndRendersToPdf() {
			Statement statement=NewStatement(_patientAnn.PatNum);
			statement.IsServiceDateView=true;
			statement.StatementNum=Statements.Insert(statement);
			string path=Path.Combine(Path.GetTempPath(),"m2sdv_"+Guid.NewGuid().ToString("N")+".pdf");
			try {
				DataSet dataSet=GetDataSet(statement);
				SheetDef sheetDef=SheetUtil.GetStatementSheetDef(statement);
				Sheet sheet=SheetUtil.CreateSheet(sheetDef,statement.PatNum,statement.HidePayment);
				sheet.Parameters.Add(new SheetParameter(true,"Statement") { ParamValue=statement });
				SheetFiller.FillFields(sheet,dataSet,statement);
				SheetUtil.CalculateHeights(sheet,dataSet,statement);
				PdfSharp.Pdf.PdfDocument pdfDocument=new SheetDrawingJob().CreatePdf(sheet,statement,null,dataSet,null,null);
				SheetDrawingJob.SavePdfToFile(pdfDocument,path);
				Assert.IsTrue(File.Exists(path) && new FileInfo(path).Length>1000);
			}
			finally {
				DataCore.NonQ("DELETE FROM statement WHERE StatementNum="+POut.Long(statement.StatementNum));
				try {
					File.Delete(path);
				}
				catch(Exception) {
				}
			}
		}

		[TestMethod]
		public void BillingDefault_IsOffUntilTheOfficeTurnsItOn() {
			Assert.IsFalse(Statements.GetBillingDefaultServiceDateView());
			Prefs.UpdateBool(PrefName.BillingDefaultServiceDateView,true);
			Cache.Refresh(InvalidType.Prefs);
			try {
				Assert.IsTrue(Statements.GetBillingDefaultServiceDateView());
			}
			finally {
				Prefs.UpdateBool(PrefName.BillingDefaultServiceDateView,false);
				Cache.Refresh(InvalidType.Prefs);
			}
		}
		#endregion Statements

		#region Per-user settings
		[TestMethod]
		public void UserPrefs_KeptPerUserBesideTheProcBreakdownSetting() {
			long userNum=987654;
			Assert.IsTrue(ServiceDateViewUserPrefs.GetBool(userNum,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,true),"Default when the user has none.");
			UserOdPref userOdPrefBreakdown=new UserOdPref { UserNum=userNum,FkeyType=UserOdFkeyType.AcctProcBreakdown,Fkey=0,ValueString=POut.Bool(true) };
			UserOdPrefs.Insert(userOdPrefBreakdown);
			UserOdPrefs.RefreshCache();
			ServiceDateViewUserPrefs.SetBool(userNum,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,false);
			ServiceDateViewUserPrefs.SetBool(userNum,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,true);
			ServiceDateViewUserPrefs.SetBool(userNum,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,false);
			ServiceDateViewUserPrefs.SetBool(userNum,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,true);
			Assert.IsFalse(ServiceDateViewUserPrefs.GetBool(userNum,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,true));
			Assert.IsTrue(ServiceDateViewUserPrefs.GetBool(userNum,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,false));
			Assert.IsFalse(ServiceDateViewUserPrefs.GetBool(userNum+1,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,false),"Other users keep their own.");
			Assert.AreEqual("3",DataCore.GetScalar("SELECT COUNT(*) FROM userodpref WHERE UserNum="+POut.Long(userNum)),"One row per setting.");
			UserOdPref userOdPrefRead=ServiceDateViewUserPrefs.GetProcBreakdownPref(userNum);
			Assert.AreEqual(0,userOdPrefRead.Fkey);
			Assert.IsTrue(PIn.Bool(userOdPrefRead.ValueString),"The proc breakdown setting is untouched.");
			Assert.AreEqual(0,UserOdPrefs.GetByCompositeKey(userNum,ServiceDateViewUserPrefs.FKEY_PROC_BREAKDOWN,UserOdFkeyType.AcctProcBreakdown).Fkey);
		}
		#endregion Per-user settings

		#region Window and Account grid cells
		[TestMethod]
		public void GridCells_FollowTheDisplayFields() {
			List<DisplayField> listDisplayFields=OpenDental.ServiceDateViewGrid.GetDisplayFields();
			CollectionAssert.AreEqual(DisplayFields26_1.GetServiceDateViewDefaultNames(),listDisplayFields.Select(x => x.InternalName).ToList());
			ServiceDateViewOptions options=new ServiceDateViewOptions { IsDetailed=true };
			List<ServiceDateViewRow> listRows=RpServiceDateView.GetRows(_patientAnn.PatNum,true,options);
			ServiceDateViewRow rowProc=listRows.First(x => x.IsProcedure && x.ProcNum==_procNum4);
			Assert.AreEqual(_date2.ToShortDateString(),OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"Service Date"));
			Assert.AreEqual("",OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"Trans Date"));
			Assert.AreEqual("Ann",OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"Patient"));
			Assert.AreEqual("200.00",OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"Charge").Replace(",","."));
			Assert.AreEqual("SYNDOC",OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"Prov"));
			Assert.AreEqual("50.00",OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"AcctBal").Replace(",","."),"200 less insurance 120 and write-off 30.");
			ServiceDateViewRow rowInsPay=listRows.First(x => x.TranType=="InsPay-Att.");
			Assert.AreEqual(_date3.ToShortDateString(),OpenDental.ServiceDateViewGrid.GetCellText(rowInsPay,"Trans Date"));
			Assert.AreEqual("120.00",OpenDental.ServiceDateViewGrid.GetCellText(rowInsPay,"Production").Replace(",","."));
			Assert.AreEqual("",OpenDental.ServiceDateViewGrid.GetCellText(rowInsPay,"Service Date"));
			ServiceDateViewRow rowHeading=listRows.First(x => x.RowKind==ServiceDateViewRowKind.UnallocatedHeading);
			Assert.AreEqual("",OpenDental.ServiceDateViewGrid.GetCellText(rowHeading,"Charge"));
			Assert.AreEqual("",OpenDental.ServiceDateViewGrid.GetCellText(rowInsPay,"Clinic"));
			Assert.IsTrue(OpenDental.ServiceDateViewGrid.MakeGridRow(listRows.Last(),listDisplayFields).Bold,"Totals stay bold.");
		}

		[TestMethod]
		public void GridCells_ToothNumbersInTheOfficeNomenclature() {
			ServiceDateViewRow rowProc=RpServiceDateView.GetRows(_patientAnn.PatNum,false,new ServiceDateViewOptions())
				.First(x => x.IsProcedure && x.ProcNum==_procNum1);
			StringAssert.Contains(rowProc.Reference,"#8-","The query keeps the American number.");
			int prefOld=PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			try {
				Prefs.UpdateInt(PrefName.UseInternationalToothNumbers,(int)ToothNumberingNomenclature.FDI);
				Cache.Refresh(InvalidType.Prefs);
				StringAssert.Contains(OpenDental.ServiceDateViewGrid.GetCellText(rowProc,"Reference"),"#"+Tooth.Display("8",ToothNumberingNomenclature.FDI)+"-");
			}
			finally {
				Prefs.UpdateInt(PrefName.UseInternationalToothNumbers,prefOld);
				Cache.Refresh(InvalidType.Prefs);
			}
		}
		#endregion Window and Account grid cells
	}
}
