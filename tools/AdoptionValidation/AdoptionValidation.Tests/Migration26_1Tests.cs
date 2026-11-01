// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;
using OpenDentBusiness.ForkMigrations.FeatureSteps;
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
			//The 25.4 and 26.1 value changes; program.Enabled is the DXC Patient Credit Score step's (AllowedDifferences_TheDxcStepAllowsOnlyItsColumnAndTable).
			List<string> listColumnsChanged=allowed.Rules.Where(x => x.Kind=="ColumnChanged" && x.Column!="SecurityHash" && x.Table!="displayreport"
				&& x.Table!=DxcCreditScorePlan.TABLE_PROGRAM).Select(x => x.Table+"."+x.Column).ToList();
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

		///<summary>The DXC Patient Credit Score step (B-SHELL) changes program.Enabled and deletes toolbutitem rows on an adopted Open Dental 25.3.48
		///database: the allowed list has exactly one rule for each, program.Enabled as a whole column and toolbutitem as a whole table, both held row by
		///row by adopt's check, and no other rule names either table. The reference changes neither table's schema.</summary>
		[TestMethod]
		public void AllowedDifferences_TheDxcStepAllowsOnlyItsColumnAndTable() {
			AllowedDifferences allowed=LoadAllowed();
			List<AllowedRule> listProgram=allowed.Rules.Where(x => x.Table==DxcCreditScorePlan.TABLE_PROGRAM).ToList();
			Assert.AreEqual(1,listProgram.Count);
			Assert.AreEqual("ColumnChanged",listProgram[0].Kind);
			Assert.AreEqual(DxcCreditScorePlan.COLUMN_ENABLED,listProgram[0].Column);
			StringAssert.Contains(listProgram[0].Reason,new OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemovedStep().Level.ToString());
			List<AllowedRule> listButtons=allowed.Rules.Where(x => x.Table==DxcCreditScorePlan.TABLE_TOOLBUTITEM).ToList();
			Assert.AreEqual(1,listButtons.Count);
			Assert.AreEqual("*",listButtons[0].Kind);
			CollectionAssert.Contains(AdoptionRowCheck.ArrayWholeTables,DxcCreditScorePlan.TABLE_TOOLBUTITEM,"adopt's row check compares it row by row.");
			CollectionAssert.DoesNotContain(AdoptionRowCheck.ArrayWholeTables,DxcCreditScorePlan.TABLE_PROGRAM,"program is allowed by one column only.");
			Migration26_1Plan.SchemaChanges changes=Migration26_1Plan.GetSchemaChanges();
			Assert.IsFalse(changes.ListTablesAdded.Concat(changes.ListColumnsAdded).Concat(changes.ListColumnsChanged)
				.Any(x => x.StartsWith("program.") || x.StartsWith("toolbutitem.") || x=="program" || x=="toolbutitem"),"The 25.4 and 26.1 references change neither table.");
		}

		///<summary>What migration-preview prints for the DXC Patient Credit Score step (B-SHELL): the program row it disables and the buttons it deletes,
		///in the adoption log's words, on a 25.3.48 database; nothing where the link is already disabled without buttons, or missing.</summary>
		[TestMethod]
		public void DxcPlanLines_SayWhatTheStepChanges() {
			List<OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ProgramRow> listPrograms=new List<OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ProgramRow> {
				new OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ProgramRow { ProgramNum=4,ProgName="DXCPatientCreditScore",IsEnabled=true },
				new OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ProgramRow { ProgramNum=3,ProgName="XVWeb",IsEnabled=true },
			};
			List<OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ToolButItemRow> listButtons=new List<OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ToolButItemRow> {
				new OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=5,ProgramNum=4,ToolBar=0 },
				new OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=6,ProgramNum=4,ToolBar=7 },
				new OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=7,ProgramNum=3,ToolBar=3 },
			};
			DxcCreditScorePlan.Plan plan=new DxcCreditScorePlan.Plan { HasProgramTable=true,HasToolButItemTable=true,
				Rows=OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.Plan(listPrograms,listButtons) };
			Assert.IsTrue(plan.IsChanging);
			string text=string.Join("\r\n",plan.Lines());
			StringAssert.Contains(text,"Step 2026110101 (DXC Patient Credit Score program link): the discontinued DXC Patient Credit Score program link (ProgramNum 4) "
				+"stays, with its text; the step disables 1 program row(s) of it (ProgramNum 4), Enabled only and deletes its 2 toolbar button(s) (ToolButItemNum 5 on "
				+"AccountModule, 6 on MainToolbar)");
			StringAssert.Contains(text,"\"Deleted 2 toolbar button(s) of the discontinued DXC Patient Credit Score program link (ToolButItemNum 5, 6)\" and \"Disabled the "
				+"discontinued DXC Patient Credit Score program link (ProgramNum 4)\"");
			StringAssert.Contains(text,"adopt's row check (step 4) requires exactly this");
			Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,@"Renamed displayreport \S+ to \S+ \(same description\)"));
			//Already cleaned up (as Open Dental most likely left it), no row, no table.
			listPrograms[0].IsEnabled=false;
			DxcCreditScorePlan.Plan planClean=new DxcCreditScorePlan.Plan { HasProgramTable=true,HasToolButItemTable=true,
				Rows=OpenDentBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.Plan(listPrograms,listButtons.Where(x => x.ProgramNum!=4).ToList()) };
			Assert.IsFalse(planClean.IsChanging);
			StringAssert.Contains(string.Join("\r\n",planClean.Lines()),"changes nothing: the discontinued DXC Patient Credit Score program link (ProgramNum 4) is already "
				+"disabled and has no toolbar button.");
			StringAssert.Contains(string.Join("\r\n",new DxcCreditScorePlan.Plan { HasProgramTable=true }.Lines()),"changes nothing: no program row is the DXC Patient Credit "
				+"Score link (ProgName DXCPatientCreditScore).");
			StringAssert.Contains(string.Join("\r\n",new DxcCreditScorePlan.Plan().Lines()),"changes nothing: there is no program table.");
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

		///<summary>What migration-preview prints for the UDS+ report row step (M2-UDS): the added row, its place, the moved rows and the copied grants on
		///a 25.3.48 database; nothing on an adopted one. The dry run reads the report rows step's renames by "Renamed displayreport X to Y (same
		///description)", which these lines never contain.</summary>
		[TestMethod]
		public void UdsPlusPlanLines_SayWhatTheStepChanges() {
			UdsPlusRowPlan.Plan plan=new UdsPlusRowPlan.Plan { IsFromBefore25_4=true };
			plan.Rows.IsAdded=true;
			plan.Rows.ItemOrder=96;
			plan.Rows.RowSealant=new ReportRows25_3.DisplayReportRow { DisplayReportNum=8,InternalName=ReportRowUdsPlus.InternalNameSealant,ItemOrder=95,
				Category=DisplayReportCategory.PublicHealth };
			plan.Rows.ListRowsMoved.Add(new ReportRows25_3.DisplayReportRow { DisplayReportNum=9,InternalName="ODRawPopulationData",ItemOrder=96,
				Category=DisplayReportCategory.PublicHealth });
			plan.ListUserGroupsGranted.Add(3);
			string text=string.Join("\r\n",plan.Lines());
			StringAssert.Contains(text,"Step 2026110103 (UDS+ report row) adds the displayreport row ODUdsPlus (\"FQHC UDS+ Dental Services\", Public Health, ItemOrder 96) "
				+"right after the FQHC Dental Sealant Measure (DisplayReportNum 8), moving 1 later Public Health row(s) down one place, ItemOrder only (DisplayReportNum 9)");
			StringAssert.Contains(text,"it gives the 1 user group(s) that may run the sealant measure (user group 3) the Reports permission for it");
			StringAssert.Contains(text,"\"Added displayreport ODUdsPlus\" and \"Copied 1 report permission(s) from ODDentalSealantMeasure to ODUdsPlus\"");
			Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,@"Renamed displayreport \S+ to \S+ \(same description\)"));
			UdsPlusRowPlan.Plan planAdopted=new UdsPlusRowPlan.Plan { IsFromBefore25_4=true };
			planAdopted.Rows.RowExisting=new ReportRows25_3.DisplayReportRow { DisplayReportNum=12,InternalName=DisplayReports.ReportNames.UdsPlus };
			StringAssert.Contains(string.Join("\r\n",planAdopted.Lines()),"changes nothing: DisplayReportNum 12 already has this program's name (ODUdsPlus).");
			UdsPlusRowPlan.Plan planRenamed=new UdsPlusRowPlan.Plan();
			planRenamed.Rows.RowRenamed=new ReportRows25_3.DisplayReportRow { DisplayReportNum=14,InternalName="ODSyntheticUds",Description="UDS+ Report" };
			planRenamed.Rows.ListMatches.Add(planRenamed.Rows.RowRenamed);
			text=string.Join("\r\n",planRenamed.Lines());
			StringAssert.Contains(text,"DisplayReportNum 14 (\"UDS+ Report\"), under a name this program does not know, is renamed to ODUdsPlus, InternalName only");
			Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,@"Renamed displayreport \S+ to \S+ \(same description\)"));
		}

		///<summary>What migration-preview prints for the Insurance History step (M2-HIST): the code groups it marks, with their History Proc Code and the
		///codes it adds, and the group it creates, in the words of the adoption log; nothing on an adopted database, on one from 25.4 on, or without
		///history preferences. The dry run reads the report rows step's renames by "Renamed displayreport X to Y (same description)", which these lines
		///never contain.</summary>
		[TestMethod]
		public void InsHistPlanLines_SayWhatTheStepChanges() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=3,GroupName="BWs",ProcCodes="Z0272,Z0274",ItemOrder=0,CodeGroupFixed=EnumCodeGroupFixed.BW },
				new CodeGroup { CodeGroupNum=7,GroupName="Perio Maint",ProcCodes="Z4910",ItemOrder=1,CodeGroupFixed=EnumCodeGroupFixed.Perio },
				new CodeGroup { CodeGroupNum=8,GroupName="SRP",ProcCodes="Z4341-Z4342",ItemOrder=2,CodeGroupFixed=EnumCodeGroupFixed.SRP },
			};
			Dictionary<string,string> dictPrefs=new Dictionary<string,string> {
				{ nameof(PrefName.InsHistBWCodes),"Z0272" },{ nameof(PrefName.InsHistPerioMaintCodes),"Z4910,Z4920" },{ nameof(PrefName.InsHistExamCodes),"Z0120" },
				{ nameof(PrefName.InsHistPerioURCodes),"Z4341" },{ nameof(PrefName.InsHistProphyCodes),"" },
			};
			List<string> listProcCodes=new List<string> { "Z0272","Z0274","Z4910","Z0120","Z4341","Z4342" };
			InsHistCodeGroupPlan.Plan plan=new InsHistCodeGroupPlan.Plan { HasCodeGroupTable=true,
				Migration=InsHistCodeGroupsMigration.CreatePlan(true,false,dictPrefs,listCodeGroups,listProcCodes) };
			Assert.IsTrue(plan.IsChanging);
			string text=string.Join("\r\n",plan.Lines());
			StringAssert.Contains(text,"Step 2026110104 (Insurance History rows from code groups) marks 3 code group(s) Show In History and adds 1 (this database "
				+"comes from before Open Dental 25.4; the ten InsHist*Codes preferences are kept): ");
			StringAssert.Contains(text,"Code group 'BWs' (CodeGroupNum 3) shown in Insurance History, History Proc Code Z0272; ");
			StringAssert.Contains(text,"Code group 'Perio Maint' (CodeGroupNum 7) shown in Insurance History, History Proc Code Z4910, codes added: Z4920; ");
			StringAssert.Contains(text,"Code group 'SRP' (CodeGroupNum 8) shown in Insurance History with four quadrant rows, History Proc Code Z4341; ");
			StringAssert.Contains(text,"Added code group 'Exam' (Exam, ItemOrder 3), shown in Insurance History, History Proc Code Z0120, codes Z0120");
			StringAssert.Contains(text,"adopt's row check (step 4) requires exactly this");
			Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,@"Renamed displayreport \S+ to \S+ \(same description\)"));
			CollectionAssert.AreEqual(new long[] { 3,8,7 },plan.Migration.ListUpdated.Select(x => x.CodeGroupNum).ToArray(),"In the order of the 24.3 rows.");
			//An adopted database: the level is applied.
			InsHistCodeGroupPlan.Plan planAdopted=new InsHistCodeGroupPlan.Plan { HasCodeGroupTable=true,
				Migration=InsHistCodeGroupsMigration.CreatePlan(true,true,dictPrefs,listCodeGroups,listProcCodes) };
			Assert.IsFalse(planAdopted.IsChanging);
			StringAssert.Contains(string.Join("\r\n",planAdopted.Lines()),"Step 2026110104 (Insurance History rows from code groups) changes nothing: it already ran on "
				+"this database.");
			//From 25.4 on, Open Dental converted the rows itself; and a group already marked Show In History is left alone.
			StringAssert.Contains(string.Join("\r\n",new InsHistCodeGroupPlan.Plan { HasCodeGroupTable=true,
				Migration=InsHistCodeGroupsMigration.CreatePlan(false,false,dictPrefs,listCodeGroups,listProcCodes) }.Lines()),
				"changes nothing: this database does not come from before Open Dental 25.4.");
			List<CodeGroup> listCodeGroupsSetUp=listCodeGroups.Select(x => x.Copy()).ToList();
			listCodeGroupsSetUp[0].ShowInHistory=true;
			StringAssert.Contains(string.Join("\r\n",new InsHistCodeGroupPlan.Plan { HasCodeGroupTable=true,
				Migration=InsHistCodeGroupsMigration.CreatePlan(true,false,dictPrefs,listCodeGroupsSetUp,listProcCodes) }.Lines()),
				"changes nothing: 1 code group(s) are already marked Show In History.");
			StringAssert.Contains(string.Join("\r\n",new InsHistCodeGroupPlan.Plan { HasCodeGroupTable=true,
				Migration=InsHistCodeGroupsMigration.CreatePlan(true,false,new Dictionary<string,string>(),listCodeGroups,listProcCodes) }.Lines()),
				"changes nothing: none of the ten InsHist*Codes preferences has a value.");
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
