// The committed db/init/new-database.sql loaded into a scratch database the way db/README.md tells an office to (an empty utf8mb3 database, then
// the file statement by statement): the schema, the version guard, the adoption record, the procedure codes and the rows a new database must and
// must not have. Skipped (inconclusive) when the local MariaDB does not answer. No office data is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace InitSqlTests {
	[TestClass]
	public class InitSqlDatabaseTests {
		private static readonly string DB_NEW=ScratchDb.Name("file");
		private static string _skipReason;
		private static int _countStatements;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			ScratchDb.Recreate(DB_NEW);
			_countStatements=ScratchDb.LoadSqlFile(DB_NEW,ScratchDb.FindRepoFile(InitSqlFileTests.RepoPathFile));
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropQuietly(DB_NEW);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NEW);
		}

		[TestMethod]
		public void Load_CreatesEveryTableAsMyIsam() {
			Assert.IsTrue(_countStatements>400,"Statements run: "+_countStatements);
			DataTable table=DataCore.GetTable("SELECT ENGINE,TABLE_COLLATION,COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+DB_NEW+"' "
				+"GROUP BY ENGINE,TABLE_COLLATION");
			Assert.AreEqual(1,table.Rows.Count,"One engine and one collation for every table.");
			Assert.AreEqual("MyISAM",table.Rows[0][0].ToString());
			Assert.AreEqual("utf8mb3_general_ci",table.Rows[0][1].ToString());
		}

		[TestMethod]
		public void Schema_MatchesTheReferenceApartFromDocumentedExceptions() {
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(DB_NEW);
			Assert.IsTrue(result.IsMatch,"Schema differs from Open Dental "+SchemaVerifier.ReferenceVersion+":\r\n"
				+string.Join("\r\n",result.ListMismatches.Select(x => x.ToString())));
			Assert.IsTrue(result.ListAllowed.All(x => x.IsExcepted));
		}

		[TestMethod]
		public void VersionGuard_ProceedsWithNoPendingAndNoUnknownLevel() {
			ForkDbState forkDbState=ForkVersions.ReadState();
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState);
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action,forkGuardResult.Message);
			Assert.AreEqual(0,forkGuardResult.ListPendingLevels.Count,"Pending: "+string.Join(", ",forkGuardResult.ListPendingLevels)
				+". Regenerate db/init/new-database.sql with tools/New-InitSql.ps1.");
			Assert.AreEqual(0,forkGuardResult.ListUnknownLevels.Count,"Unknown: "+string.Join(", ",forkGuardResult.ListUnknownLevels));
			Assert.AreEqual(0,ForkMigrationRunner.GetPendingSteps().Count);
			IForkMigrationRunner runner=ForkMigrationHandOff.Runner;
			Assert.AreEqual(runner.LatestLevel,forkDbState.ForkSchemaLevel);
			CollectionAssert.AreEquivalent(runner.GetLevels(),forkDbState.GetAppliedLevels(),"ForkMigrationsApplied holds every level of this build.");
			Assert.AreEqual(SchemaVerifier.ReferenceVersion,forkDbState.OdSchemaCompat);
			Assert.AreEqual(ForkVersions.SentinelVersion.ToString(4),forkDbState.DataBaseVersion);
			Assert.AreEqual(ForkVersions.SentinelVersion.ToString(4),forkDbState.ProgramVersion);
			Assert.IsNull(forkDbState.ForkProgramVersion,"No release is published to a new database until its update server publishes one.");
			Assert.AreEqual("",Pref("ForkMigrationInProgress"));
			Assert.AreEqual("0",Pref("CorruptedDatabase"));
			Assert.AreEqual("",Pref("UpdateInProgressOnComputerName"));
		}

		[TestMethod]
		public void AdoptionRecord_SaysNewDatabase() {
			ForkDbState forkDbState=ForkVersions.ReadState();
			string level=ForkMigrationHandOff.Runner.LatestLevel;
			Assert.AreEqual("new=1;source=24.3.63.0;level="+level+";adopted=db/init/new-database.sql",forkDbState.ForkAdoptedFrom);
			Assert.IsTrue(forkDbState.IsForkMarked);
			Assert.IsTrue(forkDbState.IsAdoptionRecordFinished,"The guard and the adoption tools read it as a finished adoption.");
			Assert.IsFalse(forkDbState.IsAdoptionIncomplete);
			Assert.AreEqual("1",ForkVersions.ParseAdoptedFromField(forkDbState.ForkAdoptedFrom,"new"));
			//The line its rows come from, which the fork migration steps read to decide what a database still needs.
			Assert.AreEqual(ForkVersions.CompatVersion,Adoption.ParseAdoptedFromSource(forkDbState.ForkAdoptedFrom));
			Assert.IsNull(ForkVersions.ParseAdoptedFromField(forkDbState.ForkAdoptedFrom,"started"),"No run times.");
			AdoptionPreflight adoptionPreflight=Adoption.Preflight(forkDbState);
			Assert.IsTrue(adoptionPreflight.IsAlreadyAdopted);
			Assert.IsFalse(adoptionPreflight.CanAdopt,"The adoption tool never adopts it again.");
		}

		[TestMethod]
		public void ProcedureCodes_AreNamedByCodeOnly() {
			DataTable table=DataCore.GetTable("SELECT ProcCode,Descript,AbbrDesc,LaymanTerm,DefaultNote,DefaultClaimNote,DefaultTPNote FROM procedurecode");
			int countCdt=0;
			foreach(DataRow row in table.Rows) {
				string code=row["ProcCode"].ToString();
				if(!Regex.IsMatch(code,@"^D\d{4}$")) {
					Assert.IsTrue(InitSqlFileTests.DictOtherCodes.ContainsKey(code),"A procedure code that is not CDT-style: "+code);
					Assert.AreEqual(InitSqlFileTests.DictOtherCodes[code],row["Descript"].ToString());
					continue;
				}
				countCdt++;
				Assert.AreEqual("Procedure "+code,row["Descript"].ToString());
				Assert.AreEqual(code,row["AbbrDesc"].ToString());
				foreach(string column in new[] { "LaymanTerm","DefaultNote","DefaultClaimNote","DefaultTPNote" }) {
					Assert.AreEqual("",row[column].ToString(),column+" of "+code);
				}
			}
			Assert.IsTrue(countCdt>500,"CDT-style codes: "+countCdt);
		}

		[TestMethod]
		public void Rows_NoPatientAppointmentComputerOrAuditRow() {
			foreach(string tableName in new[] { "patient","appointment","schedule","computer","computerpref","securitylog","securityloghash","signalod",
				"zipcode","alertitem","emailhostingtemplate" })
			{
				Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM `"+tableName+"`"),tableName+" must be empty.");
			}
			DataTable table=DataCore.GetTable("SELECT TABLE_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+DB_NEW+"' AND COLUMN_NAME='PatNum'");
			Assert.IsTrue(table.Rows.Count>50);
			foreach(DataRow row in table.Rows) {
				Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM `"+row[0]+"` WHERE PatNum<>0"),"Rows of a patient in "+row[0]+".");
			}
		}

		[TestMethod]
		public void Rows_EveryProgramLinkIsOff() {
			Assert.IsTrue(PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM program"))>0,"The program link rows are there.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM program WHERE Enabled<>0"));
		}

		[TestMethod]
		public void Admin_HasBlankPasswordThatMustBeSetAtFirstLogon() {
			DataTable table=DataCore.GetTable("SELECT UserNum,Password,IsPasswordResetRequired,IsHidden FROM userod WHERE UserName='Admin'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("",table.Rows[0]["Password"].ToString());
			Assert.AreEqual("1",table.Rows[0]["IsPasswordResetRequired"].ToString(),"The program asks for a new password at the first logon.");
			Assert.AreEqual("0",table.Rows[0]["IsHidden"].ToString());
			//Startup logs the first security administrator without a password on automatically, then asks for the password reset.
			Assert.AreEqual(PIn.Long(table.Rows[0]["UserNum"].ToString()),Userods.GetFirstSecurityAdminUserNumNoPasswordNoCache());
			Assert.IsTrue(Userods.HasSecurityAdminUserNoCache());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM userod"),"Admin is the only user.");
		}

		[TestMethod]
		public void Rows_NoFictionalPracticeOrProviderData() {
			foreach(string prefName in new[] { "PracticeTitle","PracticeAddress","PracticeCity","PracticeST","PracticeZip","PracticePhone",
				"PracticeBillingPhone","PracticePayToPhone" })
			{
				Assert.AreEqual("",Pref(prefName),prefName);
			}
			Assert.AreEqual(@"C:\OpenDentImages\",Pref("DocPath"),"DocPath as the seed dump has it.");
			DataTable table=DataCore.GetTable("SELECT Abbr,LName,FName,MI,SSN,StateLicense,DEANum,NationalProvID,MedicaidID FROM provider ORDER BY ProvNum");
			Assert.IsTrue(table.Rows.Count>0,"A provider to start from.");
			foreach(DataRow row in table.Rows) {
				Assert.IsTrue(row["LName"].ToString()=="Dentist" || row["LName"].ToString()=="Hygienist","LName "+row["LName"]);
				foreach(string column in new[] { "FName","MI","SSN","StateLicense","DEANum","NationalProvID","MedicaidID" }) {
					Assert.AreEqual("",row[column].ToString(),column+" of provider "+row["Abbr"]);
				}
			}
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM providerclinic WHERE DEANum<>'' OR StateLicense<>''"));
		}

		[TestMethod]
		public void BusinessLayer_ReadsThePreferences() {
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual(@"C:\OpenDentImages\",PrefC.GetString(PrefName.DocPath));
			Assert.AreEqual(1,PrefC.GetLong(PrefName.PracticeDefaultProv));
		}

		private static string Pref(string prefName) {
			return DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
		}
	}
}
