// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Seeds26_1;
using SyntheticData.Validation;

namespace AdoptionValidation.Tests {
	///<summary>The adoption tools against the 26.1 target, without a database: an Open Dental 25.3.48 database adopts and migrates to Open Dental 26.1.70,
	///so the allowed differences must list exactly the reference's 25.4 and 26.1 changes, each of them held to its values by adopt's row check.</summary>
	[TestClass]
	public class Migration26_1Tests {
		private static AllowedDifferences LoadAllowed() {
			return AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..","allowed-differences.adoption.json"));
		}

		private static List<string> Sorted(IEnumerable<string> items) {
			return items.Select(x => x.ToLowerInvariant()).OrderBy(x => x,StringComparer.Ordinal).ToList();
		}

		[TestMethod]
		public void Target_IsThe26_1_70Reference_SourceStays25_3_48() {
			Assert.AreEqual("26.1.70",SchemaVerifier.ReferenceVersion);
			Assert.AreEqual(OdStartupCheck.VersionSchemaTarget.ToString(3),SchemaVerifier.ReferenceVersion,"The startup model tries the release whose schema the "
				+"adopted database has.");
			Assert.IsTrue(ForkVersions.IsAdoptable(new Version(25,3,48,0)),"The office's 25.3.48 database is still adoptable.");
			Assert.IsFalse(ForkVersions.IsAdoptable(new Version(26,1,70,0)),"An Open Dental 26.1 database is not on the allowlist.");
		}

		///<summary>The allowed list's 25.4 and 26.1 rules are exactly the difference between the two embedded references: every new table, every new
		///column, and the changed columns whose values change; no rule allows anything else.</summary>
		[TestMethod]
		public void AllowedDifferences_ListExactlyTheReferenceChanges() {
			AllowedDifferences allowed=LoadAllowed();
			Migration26_1Plan.SchemaChanges changes=Migration26_1Plan.GetSchemaChanges();
			Assert.AreEqual(5,changes.ListTablesAdded.Count,string.Join(", ",changes.ListTablesAdded));
			Assert.AreEqual(28,changes.ListColumnsAdded.Count,string.Join(", ",changes.ListColumnsAdded));
			Assert.AreEqual(21,changes.ListColumnsAdded.Select(x => x.Split('.')[0]).Distinct().Count());
			CollectionAssert.AreEqual(Sorted(changes.ListTablesAdded),Sorted(allowed.Rules.Where(x => x.Kind=="TableAdded").Select(x => x.Table)));
			CollectionAssert.AreEqual(Sorted(changes.ListColumnsAdded),Sorted(allowed.Rules.Where(x => x.Kind=="ColumnAdded").Select(x => x.Table+"."+x.Column)));
			List<string> listColumnsChanged=allowed.Rules.Where(x => x.Kind=="ColumnChanged" && x.Column!="SecurityHash" && x.Table!="displayreport")
				.Select(x => x.Table+"."+x.Column).ToList();
			CollectionAssert.AreEqual(Sorted(Migration26_1Plan.ArrayValueChangingColumns),Sorted(listColumnsChanged));
			foreach(string item in Migration26_1Plan.ArrayValueChangingColumns) {
				CollectionAssert.Contains(changes.ListColumnsChanged,item,"The reference changes "+item+".");
			}
			//Every whole-table rule names a table adopt's row check compares row by row; and no rule names updatehistory.
			CollectionAssert.AreEqual(Sorted(AdoptionRowCheck.ArrayWholeTables),Sorted(allowed.Rules.Where(x => x.Kind=="*").Select(x => x.Table)));
			Assert.IsFalse(allowed.Rules.Any(x => x.Table=="updatehistory" || x.Table=="*" && x.Column!="SecurityHash"),"No other wildcard rule.");
			Assert.IsTrue(allowed.Rules.All(x => x.Kind=="ColumnRemoved" || x.Kind=="TotalChanged" || x.Column=="SecurityHash" || x.Reason.Contains("adopt")),
				"Every 25.4, 26.1 and whole-table rule says how adopt checks it.");
		}

		[TestMethod]
		public void DefaultExpression_FollowsTheColumnDefaultsMariaDbPrints() {
			Assert.AreEqual("'0001-01-01 00:00:00'",AdoptionRowCheck.DefaultExpression("'0001-01-01 00:00:00'",false,"datetime"));
			Assert.AreEqual("-1",AdoptionRowCheck.DefaultExpression("-1",false,"float"),"evaluation.GradeOverride: the back-fill is the column default.");
			Assert.AreEqual("NULL",AdoptionRowCheck.DefaultExpression("NULL",true,"text"),"procnote.Signature2 text DEFAULT NULL.");
			Assert.AreEqual("NULL",AdoptionRowCheck.DefaultExpression(null,true,"varchar"));
			Assert.AreEqual("''",AdoptionRowCheck.DefaultExpression(null,false,"text"),"text NOT NULL: the implicit default is empty.");
			Assert.AreEqual("''",AdoptionRowCheck.DefaultExpression(null,false,"varchar"));
			Assert.AreEqual("0",AdoptionRowCheck.DefaultExpression(null,false,"tinyint"));
			Assert.AreEqual("0",AdoptionRowCheck.DefaultExpression(null,false,"bigint"));
			Assert.AreEqual("'it''s'",AdoptionRowCheck.DefaultExpression("'it''s'",false,"varchar"));
			Assert.IsNull(AdoptionRowCheck.DefaultExpression("current_timestamp()",false,"timestamp"),"An expression is not compared.");
			Assert.IsNull(AdoptionRowCheck.DefaultExpression(null,false,"date"),"No implicit default is assumed for dates.");
			Assert.IsNull(AdoptionRowCheck.DefaultExpression("1; DROP TABLE x",false,"int"),"Only plain literals are used in SQL.");
			Assert.IsNull(AdoptionRowCheck.DefaultExpression("'a\\'b'",false,"varchar"));
		}

		///<summary>What migration-preview prints for the 25.4 and 26.1 steps (runbook step 0.2), for a 25.3.48 database with the office fixture's
		///grants, and for an adopted one.</summary>
		[TestMethod]
		public void PlanLines_SayWhatTheSchemaAndSeedStepsChange() {
			Migration26_1Plan.Plan plan=new Migration26_1Plan.Plan { Database="synth_x",IsFromBefore25_4=true,CountClaimPaymentNotesNull=1 };
			plan.ListPrefsInserted.AddRange(Seed26_1.ListPrefDefaults.Select(x => x.PrefName.ToString()));
			plan.ListRenumbered.Add(new Migration26_1Plan.GroupPermRow { GroupPermNum=40,UserGroupNum=1,PermType=272 });
			foreach((EnumPermType permTypeNew,EnumPermType permTypeFrom) grant in Seed26_1.ListGrants) {
				Migration26_1Plan.GrantPlan grantPlan=new Migration26_1Plan.GrantPlan { PermTypeNew=grant.permTypeNew,PermTypeFrom=grant.permTypeFrom };
				grantPlan.ListSources.Add(new Migration26_1Plan.GroupPermRow { GroupPermNum=41,UserGroupNum=2,PermType=13 });
				grantPlan.ListSources.Add(new Migration26_1Plan.GroupPermRow { GroupPermNum=43,UserGroupNum=3,PermType=13,NewerDays=30 });
				plan.ListGrants.Add(grantPlan);
			}
			string text=string.Join("\r\n",plan.Lines());
			StringAssert.Contains(text,"bring the schema to Open Dental 26.1.70: 5 new table(s), empty (");
			StringAssert.Contains(text,"1 NULL note(s) become empty");
			StringAssert.Contains(text,"inserts "+Seed26_1.ListPrefDefaults.Count+" preference(s) with their defaults: IncomeTransfersLimitToPatPayments,");
			StringAssert.Contains(text,"moves 1 Open Dental 25.3 PhoneExtension grant(s) from 272 to 274, then grants BlockoutAdd (272) to 2 user group(s) and "
				+"grants BlockoutDelete (273) to 2 user group(s)");
			Assert.IsFalse(text.Contains("Renamed displayreport"),"The dry run reads the renames from the preview by that text.");
			Migration26_1Plan.Plan planAdopted=new Migration26_1Plan.Plan { Database="synth_x_adopted",IsFromBefore25_4=true,IsSeedStepApplied=true };
			text=string.Join("\r\n",planAdopted.Lines());
			StringAssert.Contains(text,"inserts 0 preference(s) with their defaults.");
			StringAssert.Contains(text,"changes no permission: it already ran on this database.");
			Assert.AreEqual(Seed26_1.ListPrefDefaults.Count,plan.ListPrefsInserted.Count);
			Assert.AreEqual(4,plan.ListGrants.Sum(x => x.CountGranted));
			plan.ListGrants[1].IsAlreadyConfigured=true;
			StringAssert.Contains(string.Join("\r\n",plan.Lines()),"BlockoutDelete is already configured, so it is not granted");
			Assert.AreEqual(2,plan.ListGrants.Sum(x => x.CountGranted));
		}

		[TestMethod]
		public void GroupPermRow_DescribesWhatAGrantedCopyHolds() {
			Migration26_1Plan.GroupPermRow row=new Migration26_1Plan.GroupPermRow { GroupPermNum=9,UserGroupNum=3,PermType=13,FKey=0,NewerDate=new DateTime(1,1,1),
				NewerDays=30 };
			Assert.AreEqual("user group 3, PermType 272, FKey 0, NewerDate 0001-01-01, NewerDays 30",row.Describe(272));
		}

		///<summary>An adopted database has the 26.1.70 schema, so an Open Dental 26.1.70 client would find every table it expects: the version checks
		///must still lock it out, as an update server, a workstation and with the classic update view.</summary>
		[TestMethod]
		public void StartupModel_The26_1ClientWhoseSchemaTheAdoptedDatabaseHasIsLockedOut() {
			List<(string name,OdClient client,bool isClassicView)> listScenarios=OdStartupCheck.StandardScenarios()
				.Where(x => x.client.Version==OdStartupCheck.VersionSchemaTarget && !x.client.IsDebugBuild).ToList();
			Assert.AreEqual(3,listScenarios.Count,string.Join(", ",listScenarios.Select(x => x.name)));
			Assert.IsTrue(listScenarios.Any(x => x.isClassicView) && listScenarios.Any(x => !x.client.IsUpdateServer));
			foreach((string name,OdClient client,bool isClassicView) scenario in listScenarios) {
				OdDatabaseState state=new OdDatabaseState { DataBaseVersion=ForkVersions.SentinelVersion.ToString(4),ProgramVersion=ForkVersions.SentinelVersion.ToString(4),
					IsClassicUpdateView=scenario.isClassicView };
				OdStartupResult result=OdStartupCheck.Evaluate(scenario.client,state);
				Assert.IsTrue(result.IsRefused,scenario.name+": "+result.Describe());
			}
			//On the unadopted 25.3.48 copy the same client would convert it and install its services (the control case).
			OdStartupResult resultUnadopted=OdStartupCheck.Evaluate(listScenarios.First(x => x.client.IsUpdateServer && !x.isClassicView).client,
				new OdDatabaseState { DataBaseVersion="25.3.48.0",ProgramVersion="25.3.48.0" });
			Assert.IsTrue(resultUnadopted.RunsConversion && resultUnadopted.InstallsServices);
		}
	}
}
