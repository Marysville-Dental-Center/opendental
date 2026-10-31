// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration that Adoption.Run runs will change in the rows of an Open Dental 25.3.48 database, worked out before adoption
	///and read-only ("AdoptionValidation migration-preview", runbook step 0.2). On such a database the schema steps find nothing to change (the
	///schema verifier then confirms the exact match), and the seed and XConnect steps run only on a database from 24.3. The one step that can change
	///rows is the report rows step (ReportRows25_3Step): it renames the displayreport row of Production by Procedure, Procedures (individual) or
	///Insurance Pending Supplementals that Open Dental wrote under an internal name this program does not know. The plan is
	///ReportRows25_3.PlanRenames, the matching the step itself uses; adopt's row check (AdoptionRowCheck) holds the adopted copy to the same plan,
	///made from the original. Reads the displayreport table and the version preferences only.</summary>
	public static class MigrationPreview {
		public class Result {
			public string Database;
			public string DataBaseVersion;
			///<summary>True when the database comes from 24.3: then the steps also convert the schema and add rows, which this preview does not list.
			///</summary>
			public bool IsFrom24_3;
			public int CountRows;
			public List<ReportRows25_3.ReportTargetPlan> ListPlans=new List<ReportRows25_3.ReportTargetPlan>();

			///<summary>The plans that rename a row.</summary>
			public List<ReportRows25_3.ReportTargetPlan> ListRenames {
				get { return ListPlans.Where(x => x.RowRenamed!=null).ToList(); }
			}

			///<summary>What the command prints.</summary>
			public List<string> Lines() {
				List<string> listLines=new List<string>();
				listLines.Add("Fork migration preview of "+Database+" (DataBaseVersion "+(DataBaseVersion??"missing")+"): "+CountRows+" displayreport rows read. "
					+"Step "+StepReportRows+" (report rows) on the reports this program names itself:");
				foreach(ReportRows25_3.ReportTargetPlan plan in ListPlans) {
					listLines.Add("  "+plan.Describe());
				}
				List<ReportRows25_3.ReportTargetPlan> listRenames=ListRenames;
				if(listRenames.Count==0) {
					listLines.Add("Adoption's fork migration will rename no displayreport row.");
				}
				else {
					listLines.Add("Adoption's fork migration will rename "+listRenames.Count+" displayreport row(s), changing only their InternalName: "
						+string.Join("; ",listRenames.Select(x => x.RowRenamed.InternalName+" to "+x.Target.InternalName+" (DisplayReportNum "+x.RowRenamed.DisplayReportNum+")"))
						+". Write them on the checklist (runbook step 0.2); adopt's row check must show exactly these (step 4).");
				}
				if(IsFrom24_3) {
					listLines.Add("This database comes from 24.3: the fork migration also converts the schema and adds rows and permissions, which this preview "
						+"does not list. The office runbook adopts only an Open Dental 25.3.48 database.");
				}
				else {
					listLines.Add("The other fork migration steps change no row of an Open Dental 25.3.48 database: the schema steps find the 25.3.48 schema, "
						+"and the seed and XConnect steps run only on a database from 24.3.");
				}
				return listLines;
			}
		}

		///<summary>The level of the report rows step, for messages.</summary>
		public static long StepReportRows {
			get { return new ReportRows25_3Step().Level; }
		}

		public static Result Run(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			if(!db.DatabaseExists(database)) {
				throw new RefusedException("Database "+database+" does not exist on this server.");
			}
			Result result=new Result { Database=database };
			ForkDbState state=HeadlessAdoption.ReadState(db,database);
			result.DataBaseVersion=state.DataBaseVersion;
			result.IsFrom24_3=state.ForkAdoptedFrom==null && Seed25_3.IsVersionFrom24_3(state.DataBaseVersion);
			List<ReportRows25_3.DisplayReportRow> listRows=ReadRows(db,database);
			result.CountRows=listRows.Count;
			result.ListPlans=ReportRows25_3.PlanRenames(listRows);
			return result;
		}

		///<summary>Every displayreport row of the database, read with plain SQL in the order the step reads them. Empty when there is no such table.
		///</summary>
		public static List<ReportRows25_3.DisplayReportRow> ReadRows(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			if(!HasTable(db,database)) {
				return new List<ReportRows25_3.DisplayReportRow>();
			}
			return db.Rows("SELECT DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden FROM `"+database+"`.displayreport "
				+"ORDER BY Category,ItemOrder,DisplayReportNum")
				.Select(x => new ReportRows25_3.DisplayReportRow {
					DisplayReportNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
					InternalName=Convert.ToString(x[1],CultureInfo.InvariantCulture)??"",
					ItemOrder=Convert.ToInt32(x[2],CultureInfo.InvariantCulture),
					Description=Convert.ToString(x[3],CultureInfo.InvariantCulture)??"",
					Category=(DisplayReportCategory)Convert.ToInt32(x[4],CultureInfo.InvariantCulture),
					IsHidden=Convert.ToInt32(x[5],CultureInfo.InvariantCulture)!=0,
				}).ToList();
		}

		public static bool HasTable(Db db,string database) {
			return db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_NAME='displayreport'")>0;
		}
	}
}
