// The Standard Reports row of the UDS+ report (OpenDentBusiness/Misc/ForkMigrations/ReportRowUdsPlus.cs) on scratch databases with the reference
// table definitions and synthetic rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;

namespace M2UDSTests {
	[TestClass]
	public class ReportRowUdsPlusDbTests {
		private static string _dbName;
		private static string _skipReason;
		private static readonly int _permReports=(int)EnumPermType.Reports;
		private static readonly int _categoryPublicHealth=(int)DisplayReportCategory.PublicHealth;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=UdsTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=UdsTestDb.ScratchName("rows");
			UdsTestDb.Create(_dbName,"displayreport","grouppermission","preference");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_dbName!=null) {
				UdsTestDb.Drop(_dbName);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			UdsTestDb.Connect(_dbName);
			DataCore.NonQ("DELETE FROM displayreport");
			DataCore.NonQ("DELETE FROM grouppermission");
			DataCore.NonQ("DELETE FROM preference");
		}

		///<summary>Public Health rows as a 24.3 database has them, a Lists row, and report permissions: group 1 may run the sealant measure, group 2
		///every report (FKey 0), group 3 only Raw Screening Data.</summary>
		private static void SeedPublicHealth(bool isSealantHidden) {
			UdsTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES "
					+"(40,'ODRawScreeningData',0,'Raw Screening Data',4,0,0),(41,'ODRawPopulationData',1,'Raw Population Data',4,0,0),"
					+"(42,'ODDentalSealantMeasure',2,'FQHC Dental Sealant Measure',4,"+(isSealantHidden ? 1 : 0)+",0),"
					+"(43,'ODCustomPH',3,'Custom Public Health',4,0,0),(30,'ODActivePatients',0,'Active Patients',3,0,0)",
				"INSERT INTO grouppermission (UserGroupNum,PermType,FKey) VALUES (1,"+_permReports+",42),(2,"+_permReports+",0),(3,"+_permReports+",40)");
		}

		private static string GetPublicHealthOrder() {
			return DataCore.GetScalar("SELECT GROUP_CONCAT(CONCAT(InternalName,'=',ItemOrder) ORDER BY ItemOrder SEPARATOR ',') FROM displayreport "
				+"WHERE Category="+_categoryPublicHealth);
		}

		private static DataRow GetUdsRow() {
			DataTable table=DataCore.GetTable("SELECT * FROM displayreport WHERE InternalName='"+DisplayReports.ReportNames.UdsPlus+"'");
			Assert.AreEqual(1,table.Rows.Count,"Exactly one UDS+ row.");
			return table.Rows[0];
		}

		private static List<long> GetGroupsForReport(long displayReportNum) {
			return DataCore.GetTable("SELECT UserGroupNum FROM grouppermission WHERE PermType="+_permReports+" AND FKey="+displayReportNum
				+" ORDER BY UserGroupNum").Select().Select(x => PIn.Long(x[0].ToString())).ToList();
		}

		[TestMethod]
		public void Before25_4_AddsTheRowAfterTheSealantMeasure() {
			SeedPublicHealth(false);
			List<string> listChanges=ReportRowUdsPlus.Run(true);
			Assert.AreEqual(2,listChanges.Count,string.Join("\r\n",listChanges));
			Assert.AreEqual("ODRawScreeningData=0,ODRawPopulationData=1,ODDentalSealantMeasure=2,ODUdsPlus=3,ODCustomPH=4",GetPublicHealthOrder());
			DataRow row=GetUdsRow();
			Assert.AreEqual(ReportRowUdsPlus.Description,row["Description"].ToString());
			Assert.AreEqual("0",row["IsHidden"].ToString());
			Assert.AreEqual("0",row["IsVisibleInSubMenu"].ToString());
			CollectionAssert.AreEqual(new List<long> { 1 },GetGroupsForReport(PIn.Long(row["DisplayReportNum"].ToString())),
				"The groups that may run the sealant measure; group 2 already runs every report.");
			Assert.AreEqual(0,ReportRowUdsPlus.Run(true).Count,"A second run changes nothing.");
			Assert.AreEqual(6,PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM displayreport")));
		}

		[TestMethod]
		public void Before25_4_HiddenSealantMeasureHidesTheNewRow() {
			SeedPublicHealth(true);
			ReportRowUdsPlus.Run(true);
			Assert.AreEqual("1",GetUdsRow()["IsHidden"].ToString());
		}

		[TestMethod]
		public void Before25_4_WithoutSealantMeasureAppends() {
			UdsTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden) VALUES "
					+"(40,'ODRawScreeningData',0,'Raw Screening Data',4,0)",
				"INSERT INTO grouppermission (UserGroupNum,PermType,FKey) VALUES (3,"+_permReports+",40)");
			ReportRowUdsPlus.Run(true);
			Assert.AreEqual("ODRawScreeningData=0,ODUdsPlus=1",GetPublicHealthOrder());
			Assert.AreEqual(0,GetGroupsForReport(PIn.Long(GetUdsRow()["DisplayReportNum"].ToString())).Count);
		}

		[TestMethod]
		public void Later_AdoptsOpenDentalsRowUnderAnotherName() {
			SeedPublicHealth(false);
			UdsTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden) VALUES "
					+"(44,'ODUDSPlusFuture',4,'UDS+ Report',4,0)",
				"INSERT INTO grouppermission (UserGroupNum,PermType,FKey) VALUES (5,"+_permReports+",44)");
			List<string> listChanges=ReportRowUdsPlus.Run(false);
			Assert.AreEqual(1,listChanges.Count,string.Join("\r\n",listChanges));
			StringAssert.Contains(listChanges[0],"Renamed displayreport ODUDSPlusFuture");
			DataRow row=GetUdsRow();
			Assert.AreEqual(44L,PIn.Long(row["DisplayReportNum"].ToString()),"Same row, so its permissions carry over.");
			Assert.AreEqual("UDS+ Report",row["Description"].ToString());
			CollectionAssert.AreEqual(new List<long> { 5 },GetGroupsForReport(44));
			Assert.AreEqual(0,ReportRowUdsPlus.Run(false).Count);
		}

		[TestMethod]
		public void Later_WithoutSuchRowChangesNothing() {
			SeedPublicHealth(false);
			Assert.AreEqual(0,ReportRowUdsPlus.Run(false).Count);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM displayreport WHERE InternalName='"+DisplayReports.ReportNames.UdsPlus+"'"));
		}

		[TestMethod]
		public void AmbiguousCandidates_AreLeftAlone() {
			SeedPublicHealth(false);
			UdsTestDb.Seed(_dbName,
				"INSERT INTO displayreport (DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden) VALUES "
					+"(44,'ODUdsA',4,'UDS Plus',4,0),(45,'ODUdsB',5,'uds+',4,0)");
			Assert.AreEqual(0,ReportRowUdsPlus.Run(true).Count);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM displayreport WHERE InternalName='"+DisplayReports.ReportNames.UdsPlus+"'"));
			Assert.AreEqual("udsplus",ReportRowUdsPlus.NormalizeDescription(" UDS + "));
			Assert.AreEqual("udsplusreportforfqhcdentalservices",ReportRowUdsPlus.NormalizeDescription("UDS+ Report for FQHC Dental Services"));
		}

		[TestMethod]
		public void Step_JudgesTheOriginLikeSeed26_1() {
			//An Open Dental 25.3.48 database (not yet adopted): the row is added.
			SeedPublicHealth(false);
			UdsTestDb.Seed(_dbName,"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','25.3.48.0')");
			ForkMigrationResult result=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true,ListSteps=new List<ForkMigrationStep> { new ReportRowUdsPlusStep() } });
			Assert.IsTrue(result.ListChanges.Any(x => x.Contains("Added displayreport "+DisplayReports.ReportNames.UdsPlus)),string.Join("\r\n",result.ListChanges));
			result=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true,ListSteps=new List<ForkMigrationStep> { new ReportRowUdsPlusStep() } });
			Assert.AreEqual(0,result.ListChanges.Count,"Idempotent.");
			//A database Open Dental brought to 26.1: nothing is added.
			DataCore.NonQ("DELETE FROM displayreport");
			DataCore.NonQ("DELETE FROM grouppermission");
			DataCore.NonQ("DELETE FROM preference");
			SeedPublicHealth(false);
			UdsTestDb.Seed(_dbName,"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','26.1.70.0')");
			result=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true,ListSteps=new List<ForkMigrationStep> { new ReportRowUdsPlusStep() } });
			Assert.AreEqual(0,result.ListChanges.Count,string.Join("\r\n",result.ListChanges));
		}
	}
}
