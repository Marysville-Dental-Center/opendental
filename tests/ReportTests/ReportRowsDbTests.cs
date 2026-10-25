// The displayreport and report-permission rows for the 24.4 and 25.2 reports (OpenDentBusiness/Misc/ForkMigrations/ReportRows25_3.cs), on scratch
// databases with the Open Dental 25.3.48 table definitions and synthetic rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;

namespace ReportTests {
	[TestClass]
	public class ReportRowsDbTests {
		private static string _dbName;
		private static string _skipReason;
		private static readonly int _permReports=(int)EnumPermType.Reports;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ReportTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=ReportTestDb.ScratchName("rows");
			ReportTestDb.Create(_dbName,"displayreport","grouppermission","preference");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_dbName!=null) {
				ReportTestDb.Drop(_dbName);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ReportTestDb.Connect(_dbName);
			DataCore.NonQ("DELETE FROM displayreport");
			DataCore.NonQ("DELETE FROM grouppermission");
			DataCore.NonQ("DELETE FROM preference");
		}

		///<summary>Report rows as a 24.3 conversion leaves them (names, orders and categories as the 24.3 code inserts them), and report permissions:
		///group 1 may run Procedures and Insurance Overpaid, group 2 only Procedures, group 3 every report (FKey 0).</summary>
		private static void Seed24_3() {
			ReportTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES "
					+"(10,'ODAdjustments',0,'Adjustments',1,0,0),(11,'ODPayments',1,'Payments',1,0,0),(12,'ODProcedures',2,'Procedures',1,0,1),"
					+"(13,'ODWriteoffs',3,'Writeoffs',1,0,0),(14,'ODIncompleteProcNotes',4,'Incomplete Procedure Notes',1,0,0),"
					+"(20,'ODInsuranceOverpaid',0,'Insurance Overpaid',2,0,0),"
					+"(30,'ODActivePatients',0,'Active Patients',3,0,0),(31,'ODBirthdays',1,'Birthdays',3,0,0)",
				"INSERT INTO grouppermission (UserGroupNum,PermType,FKey) VALUES (1,"+_permReports+",12),(1,"+_permReports+",20),(2,"+_permReports+",12),"
					+"(3,"+_permReports+",0),(1,"+(int)EnumPermType.UserQuery+",0)");
		}

		private static DataRow GetRow(string internalName) {
			DataTable table=DataCore.GetTable("SELECT * FROM displayreport WHERE InternalName='"+POut.String(internalName)+"'");
			Assert.AreEqual(1,table.Rows.Count,"Exactly one row for "+internalName);
			return table.Rows[0];
		}

		private static List<long> GetGroupsAllowed(long displayReportNum) {
			return DataCore.GetTable("SELECT UserGroupNum FROM grouppermission WHERE PermType="+_permReports+" AND FKey="+displayReportNum+" ORDER BY UserGroupNum")
				.Select().Select(x => Convert.ToInt64(x[0])).ToList();
		}

		private static string Snapshot() {
			return string.Join(";",DataCore.GetTable("SELECT * FROM displayreport ORDER BY DisplayReportNum").Select().Select(x => string.Join("/",x.ItemArray)))
				+"|"+string.Join(";",DataCore.GetTable("SELECT UserGroupNum,PermType,FKey FROM grouppermission ORDER BY GroupPermNum").Select()
				.Select(x => string.Join("/",x.ItemArray)));
		}

		[TestMethod]
		public void From24_3_SplitsProceduresAndAddsPendingSupplementals() {
			Seed24_3();
			List<string> listChanges=ReportRows25_3.Run(true);
			Assert.IsTrue(listChanges.Count>0);
			//The old row is the itemized Procedures report now, with its number, order and permissions.
			DataRow rowProcs=GetRow(DisplayReports.ReportNames.ProceduresIndividual);
			Assert.AreEqual(12L,Convert.ToInt64(rowProcs["DisplayReportNum"]));
			Assert.AreEqual(2,Convert.ToInt32(rowProcs["ItemOrder"]));
			Assert.AreEqual(0,DataCore.GetTable("SELECT * FROM displayreport WHERE InternalName='ODProcedures'").Rows.Count);
			CollectionAssert.AreEqual(new List<long> { 1,2 },GetGroupsAllowed(12));
			//Production by Procedure sits right after it, with the same permissions.
			DataRow rowProduction=GetRow(DisplayReports.ReportNames.ProductionByProcedure);
			long numProduction=Convert.ToInt64(rowProduction["DisplayReportNum"]);
			Assert.AreEqual("Production by Procedure",rowProduction["Description"].ToString());
			Assert.AreEqual((int)DisplayReportCategory.Daily,Convert.ToInt32(rowProduction["Category"]));
			Assert.AreEqual(3,Convert.ToInt32(rowProduction["ItemOrder"]));
			CollectionAssert.AreEqual(new List<long> { 1,2 },GetGroupsAllowed(numProduction),"Copied from the old Procedures report; group 3 has FKey 0.");
			//Report Setup needs ItemOrder 0, 1, 2, ... in each category.
			CollectionAssert.AreEqual(new List<int> { 0,1,2,3,4,5 },DataCore.GetTable("SELECT ItemOrder FROM displayreport WHERE Category=1 ORDER BY ItemOrder")
				.Select().Select(x => Convert.ToInt32(x[0])).ToList());
			Assert.AreEqual(5,Convert.ToInt32(GetRow("ODIncompleteProcNotes")["ItemOrder"]));
			//Insurance Pending Supplementals at the end of Lists, for the groups that may run Insurance Overpaid.
			DataRow rowPending=GetRow(DisplayReports.ReportNames.InsPendingSupplementals);
			Assert.AreEqual((int)DisplayReportCategory.Lists,Convert.ToInt32(rowPending["Category"]));
			Assert.AreEqual(2,Convert.ToInt32(rowPending["ItemOrder"]));
			Assert.AreEqual("Insurance Pending Supplementals",rowPending["Description"].ToString());
			CollectionAssert.AreEqual(new List<long> { 1 },GetGroupsAllowed(Convert.ToInt64(rowPending["DisplayReportNum"])));
		}

		[TestMethod]
		public void SecondRun_ChangesNothing() {
			Seed24_3();
			ReportRows25_3.Run(true);
			string snapshot=Snapshot();
			List<string> listChanges=ReportRows25_3.Run(true);
			Assert.AreEqual(0,listChanges.Count,string.Join(", ",listChanges));
			Assert.AreEqual(snapshot,Snapshot());
		}

		[TestMethod]
		public void OpenDentalRowsWithOurNames_AreLeftAlone() {
			ReportTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES "
					+"(12,'ODProceduresIndividual',2,'Procedures',1,0,0),(15,'ODProductionByProcedure',3,'Production by Procedure',1,0,0),"
					+"(16,'ODProcedures',4,'Procedures (old)',1,1,0),"
					+"(30,'ODInsPendingSupplementals',0,'Ins Pending Supplementals',3,0,0)",
				"INSERT INTO grouppermission (UserGroupNum,PermType,FKey) VALUES (1,"+_permReports+",12)");
			string snapshot=Snapshot();
			List<string> listChanges=ReportRows25_3.Run(false);
			Assert.AreEqual(0,listChanges.Count,string.Join(", ",listChanges));
			Assert.AreEqual(snapshot,Snapshot());
		}

		[TestMethod]
		public void RowsUnderOtherNames_AreAdoptedByDescription() {
			ReportTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES "
					+"(12,'ODProcedures',2,'Procedures',1,0,0),(40,'ODOtherProduction',3,'Production By Procedures',1,0,0),"
					+"(41,'ODOtherIndividual',4,'Individual Procedures',1,0,0),"
					+"(50,'ODOtherPending',0,'Insurance Pending Supplementals',3,0,0)",
				"INSERT INTO grouppermission (UserGroupNum,PermType,FKey) VALUES (1,"+_permReports+",40),(2,"+_permReports+",50)");
			ReportRows25_3.Run(false);
			Assert.AreEqual(4,DataCore.GetTable("SELECT * FROM displayreport").Rows.Count,"Nothing added.");
			Assert.AreEqual(40L,Convert.ToInt64(GetRow(DisplayReports.ReportNames.ProductionByProcedure)["DisplayReportNum"]));
			Assert.AreEqual(41L,Convert.ToInt64(GetRow(DisplayReports.ReportNames.ProceduresIndividual)["DisplayReportNum"]));
			Assert.AreEqual(50L,Convert.ToInt64(GetRow(DisplayReports.ReportNames.InsPendingSupplementals)["DisplayReportNum"]));
			Assert.AreEqual(12L,Convert.ToInt64(GetRow(DisplayReports.ReportNames.Procedures)["DisplayReportNum"]),"Left as it is; it still opens the itemized report.");
			CollectionAssert.AreEqual(new List<long> { 1 },GetGroupsAllowed(40));
			CollectionAssert.AreEqual(new List<long> { 2 },GetGroupsAllowed(50));
		}

		///<summary>PlanRenames (what AdoptionValidation migration-preview prints, and what adopt's row check expects) predicts exactly what the step
		///renames on a database Open Dental converted: the same rows, to the same names, and nothing else.</summary>
		[TestMethod]
		public void PlanRenames_PredictsWhatTheStepRenames() {
			ReportTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES "
					+"(12,'ODProcedures',2,'Procedures',1,0,0),(40,'ODOtherProduction',3,'Production By Procedures',1,0,0),"
					+"(41,'ODOtherIndividual',4,'Procedures (Individual)',1,0,0),(42,'ODOtherLater',5,'A Later Report',1,0,0),"
					+"(50,'ODOtherPendingA',0,'Pending Supplementals',3,0,0),(51,'ODOtherPendingB',1,'Insurance Pending Supplementals',3,0,0)");
			List<ReportRows25_3.DisplayReportRow> listRows=DataCore.GetTable("SELECT DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden "
				+"FROM displayreport ORDER BY Category,ItemOrder,DisplayReportNum").Select().Select(x => new ReportRows25_3.DisplayReportRow {
					DisplayReportNum=Convert.ToInt64(x["DisplayReportNum"]),InternalName=x["InternalName"].ToString(),ItemOrder=Convert.ToInt32(x["ItemOrder"]),
					Description=x["Description"].ToString(),Category=(DisplayReportCategory)Convert.ToInt32(x["Category"]),IsHidden=Convert.ToInt32(x["IsHidden"])!=0,
				}).ToList();
			List<string> listPlanned=ReportRows25_3.PlanRenames(listRows).Where(x => x.RowRenamed!=null)
				.Select(x => ReportRows25_3.DescribeRename(x.RowRenamed.InternalName,x.Target.InternalName)).ToList();
			string before=Snapshot();
			List<string> listChanges=ReportRows25_3.Run(false);
			CollectionAssert.AreEqual(listPlanned,listChanges);
			Assert.AreEqual(2,listChanges.Count,"Production by Procedure and Procedures (individual); Pending Supplementals is ambiguous.");
			//Only InternalName of those two rows changed.
			string after=Snapshot();
			string expected=before.Replace("40/ODOtherProduction/","40/"+DisplayReports.ReportNames.ProductionByProcedure+"/")
				.Replace("41/ODOtherIndividual/","41/"+DisplayReports.ReportNames.ProceduresIndividual+"/");
			Assert.AreEqual(expected,after);
		}

		[TestMethod]
		public void AmbiguousRows_AreNotAdopted() {
			ReportTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES "
					+"(50,'ODOtherPendingA',0,'Pending Supplementals',3,0,0),(51,'ODOtherPendingB',1,'Pending Supplementals',3,0,0)");
			ReportRows25_3.Run(true);
			DataRow rowPending=GetRow(DisplayReports.ReportNames.InsPendingSupplementals);
			Assert.IsTrue(Convert.ToInt64(rowPending["DisplayReportNum"])>51);
			Assert.AreEqual(2,Convert.ToInt32(rowPending["ItemOrder"]));
			Assert.AreEqual(1,DataCore.GetTable("SELECT * FROM displayreport WHERE InternalName='ODOtherPendingA'").Rows.Count);
		}

		[TestMethod]
		public void OpenDentalDatabase_GetsNoNewRows() {
			//The same rows as a 24.3 database, but Open Dental converted it: rule 2, nothing is added or renamed.
			Seed24_3();
			string snapshot=Snapshot();
			List<string> listChanges=ReportRows25_3.Run(false);
			Assert.AreEqual(0,listChanges.Count,string.Join(", ",listChanges));
			Assert.AreEqual(snapshot,Snapshot());
		}

		[TestMethod]
		public void Step_RunsThroughTheMigrationContext_From24_3() {
			Seed24_3();
			ReportTestDb.Seed(_dbName,"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','24.3.63.0')");
			ForkMigrationContext context=new ForkMigrationContext(_dbName);
			new ReportRows25_3Step().Apply(context);
			Assert.IsTrue(context.ListChanges.Any(x => x.Contains(DisplayReports.ReportNames.ProductionByProcedure)),string.Join("\r\n",context.ListChanges));
			GetRow(DisplayReports.ReportNames.ProceduresIndividual);
			GetRow(DisplayReports.ReportNames.InsPendingSupplementals);
		}

		[TestMethod]
		public void Step_LeavesAnOpenDental25_3DatabaseAlone() {
			Seed24_3();
			ReportTestDb.Seed(_dbName,"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','25.3.48.0')");
			string snapshot=Snapshot();
			ForkMigrationContext context=new ForkMigrationContext(_dbName);
			new ReportRows25_3Step().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
			Assert.AreEqual(snapshot,Snapshot());
		}
	}
}
