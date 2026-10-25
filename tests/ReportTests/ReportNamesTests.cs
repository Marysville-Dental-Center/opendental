// Checks that need no database: the report dispatcher and the list of known report names agree, Report Setup shows the new names, the fork
// migration step is registered, and the description matching used to adopt Open Dental's own rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;

namespace ReportTests {
	[TestClass]
	public class ReportNamesTests {
		private static string GetRepoFile(params string[] arrayParts) {
			string dir=Path.GetDirectoryName(typeof(ReportNamesTests).Assembly.Location);
			while(dir!=null && !File.Exists(Path.Combine(dir,"OpenDental.sln"))) {
				dir=Path.GetDirectoryName(dir);
			}
			Assert.IsNotNull(dir,"Could not find the repository root above the test assembly.");
			return Path.Combine(new[] { dir }.Concat(arrayParts).ToArray());
		}

		///<summary>The internal names in "case ...:" lines of a C# file: string literals, and DisplayReports.ReportNames constants resolved by
		///reflection.</summary>
		private static HashSet<string> GetCaseNames(string source) {
			HashSet<string> hashSet=new HashSet<string>();
			foreach(Match match in Regex.Matches(source,@"case\s+""(OD\w+)""\s*:")) {
				hashSet.Add(match.Groups[1].Value);
			}
			foreach(Match match in Regex.Matches(source,@"case\s+DisplayReports\.ReportNames\.(\w+)\s*:")) {
				FieldInfo field=typeof(DisplayReports.ReportNames).GetField(match.Groups[1].Value,BindingFlags.Public|BindingFlags.Static);
				Assert.IsNotNull(field,"Unknown constant ReportNames."+match.Groups[1].Value);
				hashSet.Add((string)field.GetValue(null));
			}
			return hashSet;
		}

		///<summary>The source of one method, from its signature to the next method at the same indentation.</summary>
		private static string GetMethodSource(string source,string signatureStart) {
			int start=source.IndexOf(signatureStart,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Method not found: "+signatureStart);
			int end=source.IndexOf("\n\t\t///<summary>",start,StringComparison.Ordinal);
			int endPrivate=source.IndexOf("\n\t\tprivate ",start+signatureStart.Length,StringComparison.Ordinal);
			if(end<0 || (endPrivate>=0 && endPrivate<end)) {
				end=endPrivate;
			}
			return end<0 ? source.Substring(start) : source.Substring(start,end-start);
		}

		[TestMethod]
		public void Dispatcher_OpensExactlyTheKnownNames() {
			string source=File.ReadAllText(GetRepoFile("OpenDental","Forms","FormReportsMore.cs"));
			HashSet<string> hashSetDispatched=GetCaseNames(GetMethodSource(source,"public static ReportNonModalSelection OpenReportHelper("));
			HashSet<string> hashSetKnown=new HashSet<string>(DisplayReports.ReportNames.ArrayKnownNames);
			Assert.AreEqual(hashSetKnown.Count,DisplayReports.ReportNames.ArrayKnownNames.Length,"ArrayKnownNames has duplicates.");
			List<string> listNotKnown=hashSetDispatched.Except(hashSetKnown).OrderBy(x => x).ToList();
			List<string> listNotDispatched=hashSetKnown.Except(hashSetDispatched).OrderBy(x => x).ToList();
			Assert.AreEqual(0,listNotKnown.Count,"Dispatched but not in ArrayKnownNames: "+string.Join(", ",listNotKnown));
			Assert.AreEqual(0,listNotDispatched.Count,"In ArrayKnownNames but not dispatched: "+string.Join(", ",listNotDispatched));
			foreach(string name in new[] { DisplayReports.ReportNames.Procedures,DisplayReports.ReportNames.ProceduresIndividual,
				DisplayReports.ReportNames.ProductionByProcedure,DisplayReports.ReportNames.InsPendingSupplementals })
			{
				Assert.IsTrue(DisplayReports.ReportNames.IsKnown(name),name);
			}
			Assert.IsFalse(DisplayReports.ReportNames.IsKnown(DisplayReports.ReportNames.WebSchedAppointments),"The removed Web Sched report.");
		}

		[TestMethod]
		public void Dispatcher_NewReportsKeepTheDailyProviderRuleAndLogs() {
			string source=GetMethodSource(File.ReadAllText(GetRepoFile("OpenDental","Forms","FormReportsMore.cs")),
				"public static ReportNonModalSelection OpenReportHelper(");
			foreach(string constant in new[] { "ProceduresIndividual","ProductionByProcedure" }) {
				int index=source.IndexOf("case DisplayReports.ReportNames."+constant+":",StringComparison.Ordinal);
				Assert.IsTrue(index>=0,constant);
				int indexNextBreak=Math.Min(IndexOrMax(source,"break;",index),IndexOrMax(source,"return ReportNonModalSelection",index));
				string body=source.Substring(index,indexNextBreak-index);
				StringAssert.Contains(body,"EnumPermType.ReportDailyAllProviders",constant+" must block users who are not providers and lack the permission.");
			}
			StringAssert.Contains(source,"\"Procedures report run.\"");
			StringAssert.Contains(source,"\"Production by Procedure report run.\"");
			StringAssert.Contains(source,"\"Insurance Pending Supplementals report run.\"");
		}

		private static int IndexOrMax(string source,string value,int startIndex) {
			int index=source.IndexOf(value,startIndex,StringComparison.Ordinal);
			return index<0 ? int.MaxValue : index;
		}

		[TestMethod]
		public void ReportSetup_NamesTheNewReports() {
			string source=File.ReadAllText(GetRepoFile("OpenDental","User Controls","UserControlReportSetup.cs"));
			HashSet<string> hashSet=GetCaseNames(GetMethodSource(source,"private string GetInternalName("));
			foreach(string name in new[] { DisplayReports.ReportNames.Procedures,DisplayReports.ReportNames.ProceduresIndividual,
				DisplayReports.ReportNames.ProductionByProcedure,DisplayReports.ReportNames.InsPendingSupplementals })
			{
				Assert.IsTrue(hashSet.Contains(name),"Report Setup has no display name for "+name);
			}
		}

		[TestMethod]
		public void ProcSheet_IsGroupedOnly() {
			string designer=File.ReadAllText(GetRepoFile("OpenDental","Forms Reports","FormRpProcSheet.designer.cs"));
			Assert.IsFalse(designer.Contains("radioIndividual"),"The Type group box is gone.");
			StringAssert.Contains(designer,"\"Production by Procedure Report\"");
		}

		[TestMethod]
		public void MigrationStep_IsRegisteredAfterTheSeedStep() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationRunner.ValidateLevelsUnique(listSteps);
			ForkMigrationStep stepReports=listSteps.SingleOrDefault(x => x is ReportRows25_3Step);
			Assert.IsNotNull(stepReports);
			ForkMigrationStep stepSeed=listSteps.Single(x => x is Seed25_3Step);
			Assert.IsTrue(stepReports.Level>stepSeed.Level);
		}

		private static ReportRows25_3.DisplayReportRow Row(long num,string internalName,string description,DisplayReportCategory category) {
			return new ReportRows25_3.DisplayReportRow { DisplayReportNum=num,InternalName=internalName,Description=description,Category=category };
		}

		///<summary>The plan that migration-preview prints and adopt's row check holds the adopted copy to: only rows under unknown names whose
		///description matches, only in the report's category, never when our name exists or the match is ambiguous.</summary>
		[TestMethod]
		public void PlanRenames_RenamesOnlyAnUnambiguousRowUnderAnUnknownName() {
			List<ReportRows25_3.DisplayReportRow> listRows=new List<ReportRows25_3.DisplayReportRow> {
				Row(40,"ODOtherProduction","Production By Procedures",DisplayReportCategory.Daily),
				Row(41,"ODOtherIndividual","Procedures (Individual)",DisplayReportCategory.Daily),
				Row(42,"ODProcedures","Procedures",DisplayReportCategory.Daily),
				Row(43,"ODOtherFuture","Some Later Report",DisplayReportCategory.Daily),
				Row(50,"ODOtherPendingA","Pending Supplementals",DisplayReportCategory.Lists),
				Row(51,"ODOtherPendingB","Insurance Pending Supplementals",DisplayReportCategory.Lists),
				Row(60,"ODOtherWrongCategory","Production by Procedure",DisplayReportCategory.Monthly),
			};
			string before=string.Join(";",listRows.Select(x => x.DisplayReportNum+"/"+x.InternalName));
			List<ReportRows25_3.ReportTargetPlan> listPlans=ReportRows25_3.PlanRenames(listRows);
			Assert.AreEqual(before,string.Join(";",listRows.Select(x => x.DisplayReportNum+"/"+x.InternalName)),"The rows passed in are not changed.");
			CollectionAssert.AreEqual(ReportRows25_3.ArrayTargets.Select(x => x.InternalName).ToList(),listPlans.Select(x => x.Target.InternalName).ToList());
			ReportRows25_3.ReportTargetPlan planProduction=listPlans[0];
			Assert.AreEqual(40L,planProduction.RowRenamed.DisplayReportNum,"The Monthly row with the same description is not a Daily report.");
			Assert.AreEqual("ODOtherProduction",planProduction.RowRenamed.InternalName,"The plan keeps the name the row has before.");
			StringAssert.Contains(planProduction.Describe(),ReportRows25_3.DescribeRename("ODOtherProduction",DisplayReports.ReportNames.ProductionByProcedure));
			Assert.AreEqual(41L,listPlans[1].RowRenamed.DisplayReportNum);
			Assert.IsNull(listPlans[2].RowRenamed,"Two rows match Insurance Pending Supplementals: ambiguous, nothing is renamed.");
			Assert.IsTrue(listPlans[2].IsAmbiguous);
			StringAssert.Contains(listPlans[2].Describe(),"nothing is renamed");
			//Our name already there: nothing to rename, whatever else matches.
			listRows.Add(Row(70,DisplayReports.ReportNames.ProductionByProcedure,"Production by Procedure",DisplayReportCategory.Daily));
			listPlans=ReportRows25_3.PlanRenames(listRows);
			Assert.IsNull(listPlans[0].RowRenamed);
			Assert.AreEqual(70L,listPlans[0].RowExisting.DisplayReportNum);
			Assert.AreEqual(0,ReportRows25_3.PlanRenames(new List<ReportRows25_3.DisplayReportRow>()).Count(x => x.RowRenamed!=null));
		}

		///<summary>ForkMigrationRunnerAdapter logs every change line of a step that changed the database, without the level prefix, so the adoption log
		///names a report row it renamed.</summary>
		[TestMethod]
		public void RunnerAdapter_ListsTheChangesOfEachStep() {
			ReportRows25_3Step step=new ReportRows25_3Step();
			ForkMigrationResult result=new ForkMigrationResult();
			result.ListChanges.Add("2026101901: ALTER TABLE x ADD y");
			result.ListChanges.Add(step.Level+": "+ReportRows25_3.DescribeRename("ODOtherProduction",DisplayReports.ReportNames.ProductionByProcedure));
			CollectionAssert.AreEqual(new List<string> { "Renamed displayreport ODOtherProduction to ODProductionByProcedure (same description)" },
				ForkMigrationRunnerAdapter.GetChangesOfStep(result,step));
		}

		[TestMethod]
		public void NormalizeDescription_IgnoresCaseSpacesAndPunctuation() {
			Assert.AreEqual("productionbyprocedures",ReportRows25_3.NormalizeDescription("Production By Procedures"));
			Assert.AreEqual("insurancependingsupplementals",ReportRows25_3.NormalizeDescription(" Insurance  Pending-Supplementals "));
			Assert.AreEqual("",ReportRows25_3.NormalizeDescription(null));
		}

		[TestMethod]
		public void IndividualQuery_NeverWritesAnEmptyInList() {
			string query=RpProcSheet.GetIndividualQuery(new DateTime(2026,1,1),new DateTime(2026,1,31),new List<long>(),new List<long>(),"",false,false,
				true,true);
			Assert.IsFalse(query.Contains("IN ()"),query);
			StringAssert.Contains(query,"procedurelog.PatNum,procedurelog.ProcNum,procedurelog.ClinicNum");
			string queryGrouped=RpProcSheet.GetGroupedQuery(new DateTime(2026,1,1),new DateTime(2026,1,31),new List<long>(),new List<long>(),"",false,true);
			Assert.IsFalse(queryGrouped.Contains("IN ()"),queryGrouped);
		}
	}
}
