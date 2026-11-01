// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.FeatureSteps;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration's Insurance History step (InsHistCodeGroupsStep, backlog package M2-HIST: from Open Dental 26.1 the Insurance
	///History rows come from code groups) changes when adoption brings an Open Dental 25.3.48 database to 26.1.70, worked out before adoption and
	///read-only. On a database from before 25.4 (judged as the 25.4 and 26.1 seed step judges it, Migration26_1Plan) whose code groups are not marked
	///Show In History yet, the step marks the code group of each history category whose InsHist*Codes preference has a value (ShowInHistory, its
	///History Proc Code and, for the four perio quadrants, Perio Four Quadrants) and appends to its ProcCodes the preference's codes it lacks; a
	///category without a code group of its fixed type gets a new group. The preferences are kept. The plan is the step's own
	///(InsHistCodeGroupsMigration.CreatePlan) on the rows read here with plain SQL. migration-preview prints it (runbook step 0.2), and adopt's row
	///check (AdoptionRowCheck) holds the adopted copy's codegroup rows to the plan made from the original. Reads preference, codegroup and
	///procedurecode.</summary>
	public static class InsHistCodeGroupPlan {
		///<summary>The three codegroup columns of Open Dental 26.1 the step writes.</summary>
		public static readonly string[] ArrayHistoryColumns={ "ShowInHistory","HistProcCode","IsPerioFourQuads" };

		///<summary>Level of the step, for messages.</summary>
		public static long StepLevel {
			get { return new InsHistCodeGroupsStep().Level; }
		}

		public class Plan {
			///<summary>False when the database has no codegroup table: the step then changes nothing (on a real database it always has one).</summary>
			public bool HasCodeGroupTable;
			///<summary>The step's plan.</summary>
			public InsHistCodeGroupsMigration.MigrationPlan Migration=new InsHistCodeGroupsMigration.MigrationPlan();

			///<summary>True when the step changes rows of this database.</summary>
			public bool IsChanging {
				get { return HasCodeGroupTable && Migration.IsRun && Migration.ListCodeGroupsChanged.Count>0; }
			}

			///<summary>What migration-preview prints for the step.</summary>
			public List<string> Lines() {
				string step="Step "+StepLevel+" (Insurance History rows from code groups)";
				List<string> listLines=new List<string>();
				if(!HasCodeGroupTable) {
					listLines.Add(step+" changes nothing: there is no codegroup table.");
					return listLines;
				}
				string reason=Migration.GetReasonUnchanged();
				if(reason!="") {
					listLines.Add(step+" changes nothing: "+reason+".");
					return listLines;
				}
				List<CodeGroup> listUpdated=Migration.ListUpdated;
				List<CodeGroup> listAdded=Migration.ListAdded;
				listLines.Add(step+" marks "+listUpdated.Count+" code group(s) Show In History and adds "+listAdded.Count+" (this database comes from before "
					+"Open Dental 25.4; the ten InsHist*Codes preferences are kept): "+string.Join("; ",Migration.GetLogLines())+". The adoption log says each of these; "
					+"adopt's row check (step 4) requires exactly this: the marked groups change only ProcCodes (codes added) and the three history columns, the added "
					+"group(s) hold these values, and no other codegroup row or column changes.");
				return listLines;
			}
		}

		///<summary>The plan for a database on this server, read with plain SQL. isFromBefore25_4 as Migration26_1Plan judges it.</summary>
		public static Plan Make(Db db,string database,bool isFromBefore25_4) {
			LocalOnly.EnsureSafeIdentifier(database);
			Plan plan=new Plan();
			plan.HasCodeGroupTable=Migration26_1Plan.HasTable(db,database,"codegroup");
			if(!plan.HasCodeGroupTable || !Migration26_1Plan.HasTable(db,database,"preference")) {
				return plan;
			}
			string applied=Convert.ToString(db.Scalar("SELECT ValueString FROM `"+database+"`.preference WHERE PrefName="
				+SqlLiteral.Quote(ForkMigrationRunner.PrefNameApplied)+" ORDER BY PrefNum LIMIT 1"))??"";
			bool isLevelApplied=applied.Split(',').Any(x => x.Trim()==StepLevel.ToString(CultureInfo.InvariantCulture));
			plan.Migration=InsHistCodeGroupsMigration.CreatePlan(isFromBefore25_4,isLevelApplied,ReadPrefs(db,database),ReadCodeGroups(db,database),
				Migration26_1Plan.HasTable(db,database,"procedurecode")
					? db.Rows("SELECT ProcCode FROM `"+database+"`.procedurecode").Select(x => Convert.ToString(x[0],CultureInfo.InvariantCulture)??"").ToList()
					: new List<string>());
			return plan;
		}

		///<summary>The ten InsHist*Codes preferences the database has, by name, as the step reads them.</summary>
		public static Dictionary<string,string> ReadPrefs(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			string names=string.Join(",",InsHistCodeGroupsMigration.ListPrefNames.Select(x => SqlLiteral.Quote(x.ToString())));
			Dictionary<string,string> dictPrefs=new Dictionary<string,string>();
			foreach(object[] row in db.Rows("SELECT PrefName,ValueString FROM `"+database+"`.preference WHERE PrefName IN ("+names+") ORDER BY PrefNum")) {
				dictPrefs[Convert.ToString(row[0],CultureInfo.InvariantCulture)]=Convert.ToString(row[1],CultureInfo.InvariantCulture)??"";
			}
			return dictPrefs;
		}

		///<summary>The code groups with the columns the step reads, in ItemOrder and CodeGroupNum order. On an Open Dental 25.3.48 database the 26.1
		///columns are missing: no group is marked Show In History, and none has a History Proc Code or Perio Four Quadrants.</summary>
		public static List<CodeGroup> ReadCodeGroups(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			bool hasHistoryColumns=ArrayHistoryColumns.All(x => db.ScalarLong("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="
				+SqlLiteral.Quote(database)+" AND TABLE_NAME='codegroup' AND COLUMN_NAME="+SqlLiteral.Quote(x))>0);
			string columns="CodeGroupNum,GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden"+(hasHistoryColumns ? ",ShowInHistory,HistProcCode,IsPerioFourQuads" : "");
			return db.Rows("SELECT "+columns+" FROM `"+database+"`.codegroup ORDER BY ItemOrder,CodeGroupNum").Select(x => new CodeGroup {
				CodeGroupNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
				GroupName=Convert.ToString(x[1],CultureInfo.InvariantCulture)??"",
				ProcCodes=Convert.ToString(x[2],CultureInfo.InvariantCulture)??"",
				ItemOrder=Convert.ToInt32(x[3],CultureInfo.InvariantCulture),
				CodeGroupFixed=(EnumCodeGroupFixed)Convert.ToInt32(x[4],CultureInfo.InvariantCulture),
				IsHidden=Convert.ToInt32(x[5],CultureInfo.InvariantCulture)!=0,
				ShowInHistory=hasHistoryColumns && Convert.ToInt32(x[6],CultureInfo.InvariantCulture)!=0,
				HistProcCode=hasHistoryColumns ? Convert.ToString(x[7],CultureInfo.InvariantCulture)??"" : "",
				IsPerioFourQuads=hasHistoryColumns && Convert.ToInt32(x[8],CultureInfo.InvariantCulture)!=0,
			}).ToList();
		}
	}
}
