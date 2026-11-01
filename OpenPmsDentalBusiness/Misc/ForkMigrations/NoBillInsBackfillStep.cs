// M2-EST: carries the "Do Not Bill to Ins" decisions of a 24.3/25.3 database over to procedurelog.NoBillIns (Open Dental 25.4).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	//A feature package's data step, not one of the Open Dental schema steps: it lives in its own namespace, like XConnectEnableStep.
	///<summary>Before 25.4 the Do Not Bill to Ins box in Procedure Info only changed the procedure's claimprocs (claimproc.NoBillIns), so the decision
	///was lost whenever the estimates were rebuilt. 25.4 keeps it on the procedure (procedurelog.NoBillIns, added by M2-01's 25.4 schema step at level
	///2026102401, default 0). Open Dental's own conversion is not public; this is the fork's rule from the 25.4 spec (INFERRED):
	///<para>A procedure gets NoBillIns=1 when it is treatment planned (TP or TPi) or complete and not billed (no claimproc on a claim other than a
	///preauthorization), it has at least one estimate claimproc (Estimate or CapEstimate), every one of those estimates has NoBillIns=1, and for every
	///one of them the defaults would have given 0: the procedure code's NoBillIns, or the plan's override of it (insplanpreference, FKeyType 0) when
	///there is one. Then the box was checked by a user. Everything else keeps 0.</para>
	///<para>When the "exclusions mark Do Not Bill to Ins" preference (InsPlanExclusionsMarkDoNotBillIns) is on, estimates are also marked
	///automatically for excluded procedures, which looks the same in the data. To be safe, an estimate of a plan with any exclusion benefit (plan or
	///patient override) is then not taken as a user's decision.</para>
	///Runs only on a database that comes from before Open Dental 25.4 (Seed26_1Step.IsFromBefore25_4), only on its first run (a RunAll rerun of an
	///applied level skips it), and only while no procedure has NoBillIns=1 yet, that is while the column is as M2-01's step created it.
	///<para>NOT ACTIVE: the class is abstract, so the runner skips it. Its level is a valid dated level (2026110102, renumbered on 2026-11-01
	///from the retired package range level 2027002701), so the level does not stop it; the owner has not decided whether the back-fill should run,
	///because it changes procedurelog.NoBillIns on an adopted office database. To turn it on, remove "abstract" and give adoption's tools the plan
	///(tools/AdoptionValidation: migration-preview and adopt's row check, as for the other data steps). The startup check runs a step whose level is
	///below a database's highest applied level (ForkVersionGuard decides from ForkMigrationsApplied), so the level can stay. See
	///docs/port-notes/M2-EST.md.</para></summary>
	[ForkMigration(LEVEL,"Do Not Bill to Ins decisions moved to procedurelog.NoBillIns (25.4)")]
	public abstract class NoBillInsBackfillStep:ForkMigrationStep {
		///<summary>The level of package M2-EST's step.</summary>
		public const long LEVEL=2026110102;
		private const int PROC_STAT_TP=1;
		private const int PROC_STAT_C=2;
		private const int PROC_STAT_TPI=8;
		private const int CLAIM_PROC_STATUS_PREAUTH=2;
		private const int CLAIM_PROC_STATUS_ESTIMATE=6;
		private const int CLAIM_PROC_STATUS_CAP_ESTIMATE=8;
		private const int INS_BENEFIT_TYPE_EXCLUSIONS=4;
		///<summary>InsPlanPrefFKeyType.ProcCodeNoBillIns.</summary>
		private const int FKEY_TYPE_PROC_CODE_NO_BILL_INS=0;
		private const int CHUNK_SIZE=500;

		public override void Apply(ForkMigrationContext context) {
			if(!context.ColumnExists("procedurelog","NoBillIns")) {
				throw new ApplicationException("procedurelog.NoBillIns is missing; the 25.4 schema step must run first.");
			}
			if(!Seed26_1Step.IsFromBefore25_4(context)) {
				return;
			}
			if(ForkMigrationRunner.GetAppliedLevels().Contains(LEVEL)) {
				return;
			}
			long countFlagged=Run(context);
			if(countFlagged>0) {
				context.Log("Procedures given Do Not Bill to Ins from their claimprocs: "+countFlagged);
			}
		}

		///<summary>Flags the procedures (see the class summary) and returns how many. Does nothing when any procedure already has NoBillIns=1.
		///context may be null (tests); then nothing is logged.</summary>
		public static long Run(ForkMigrationContext context=null) {
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM procedurelog WHERE NoBillIns=1"))>0) {
				return 0;
			}
			bool isExclusionMarkOn=PIn.Bool(Db.GetScalar(
				"SELECT ValueString FROM preference WHERE PrefName='"+POut.String(nameof(PrefName.InsPlanExclusionsMarkDoNotBillIns))+"'"));
			string statusesEstimate=CLAIM_PROC_STATUS_ESTIMATE+","+CLAIM_PROC_STATUS_CAP_ESTIMATE;
			string statusesProc=PROC_STAT_TP+","+PROC_STAT_C+","+PROC_STAT_TPI;
			DataTable tableCandidates=Db.GetTable("SELECT DISTINCT claimproc.ProcNum FROM claimproc "
				+"INNER JOIN procedurelog ON procedurelog.ProcNum=claimproc.ProcNum "
				+"WHERE claimproc.ProcNum>0 AND claimproc.NoBillIns=1 AND claimproc.Status IN("+statusesEstimate+") "
				+"AND procedurelog.NoBillIns=0 AND procedurelog.ProcStatus IN("+statusesProc+")");
			List<long> listProcNumsCandidate=tableCandidates.Rows.Cast<DataRow>().Select(x => PIn.Long(x["ProcNum"].ToString())).ToList();
			long countFlagged=0;
			for(int i=0;i<listProcNumsCandidate.Count;i+=CHUNK_SIZE) {
				List<long> listProcNumsChunk=listProcNumsCandidate.Skip(i).Take(CHUNK_SIZE).ToList();
				DataTable table=Db.GetTable("SELECT claimproc.ProcNum,claimproc.Status,claimproc.ClaimNum,claimproc.NoBillIns,"
					+"procedurecode.NoBillIns CodeNoBillIns,"
					+"(SELECT insplanpreference.ValueString FROM insplanpreference WHERE insplanpreference.FKeyType="+FKEY_TYPE_PROC_CODE_NO_BILL_INS+" "
						+"AND insplanpreference.FKey=procedurelog.CodeNum AND insplanpreference.PlanNum=claimproc.PlanNum "
						+"ORDER BY insplanpreference.InsPlanPrefNum LIMIT 1) PlanOverride,"
					+"(SELECT COUNT(*) FROM benefit WHERE benefit.BenefitType="+INS_BENEFIT_TYPE_EXCLUSIONS+" AND (benefit.PlanNum=claimproc.PlanNum "
						+"OR benefit.PatPlanNum IN(SELECT patplan.PatPlanNum FROM patplan WHERE patplan.PatNum=claimproc.PatNum "
						+"AND patplan.InsSubNum=claimproc.InsSubNum))) CountExclusions "
					+"FROM claimproc "
					+"INNER JOIN procedurelog ON procedurelog.ProcNum=claimproc.ProcNum "
					+"LEFT JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum "
					+"WHERE claimproc.ProcNum IN("+string.Join(",",listProcNumsChunk)+")");
				List<long> listProcNumsToFlag=GetProcNumsToFlag(table,isExclusionMarkOn);
				if(listProcNumsToFlag.Count==0) {
					continue;
				}
				//A back-fill is not an edit of the row (README rule 4): keep procedurelog's ON UPDATE timestamp.
				string command="UPDATE procedurelog SET NoBillIns=1,DateTStamp=DateTStamp WHERE ProcNum IN("+string.Join(",",listProcNumsToFlag)+")";
				if(context==null) {
					countFlagged+=Db.NonQ(command);
				}
				else {
					countFlagged+=context.ExecuteData(command);
				}
			}
			return countFlagged;
		}

		///<summary>The procedures to flag, from one row per claimproc of the candidate procedures with the columns ProcNum, Status, ClaimNum, NoBillIns,
		///CodeNoBillIns, PlanOverride (insplanpreference.ValueString or null) and CountExclusions. See the class summary for the rule.</summary>
		public static List<long> GetProcNumsToFlag(DataTable table,bool isExclusionMarkOn) {
			List<long> listProcNums=new List<long>();
			IEnumerable<IGrouping<long,DataRow>> groups=table.Rows.Cast<DataRow>().GroupBy(x => PIn.Long(x["ProcNum"].ToString()));
			foreach(IGrouping<long,DataRow> group in groups) {
				bool isBilled=group.Any(x => PIn.Long(x["ClaimNum"].ToString())>0 && PIn.Int(x["Status"].ToString())!=CLAIM_PROC_STATUS_PREAUTH);
				if(isBilled) {
					continue;
				}
				List<DataRow> listRowsEstimate=group.Where(x => PIn.Int(x["Status"].ToString()).In(CLAIM_PROC_STATUS_ESTIMATE,CLAIM_PROC_STATUS_CAP_ESTIMATE))
					.ToList();
				if(listRowsEstimate.Count==0 || !listRowsEstimate.All(x => PIn.Bool(x["NoBillIns"].ToString()))) {
					continue;
				}
				if(listRowsEstimate.Any(x => IsNoBillInsByDefault(x))) {
					continue;//The code or the plan already says not to bill: nothing the user decided.
				}
				if(isExclusionMarkOn && listRowsEstimate.Any(x => PIn.Long(x["CountExclusions"].ToString())>0)) {
					continue;//Might have been marked by an exclusion.
				}
				listProcNums.Add(group.Key);
			}
			return listProcNums;
		}

		///<summary>What InsPlanPreferences.NoBillIns would give for the row's code and plan: the plan's override (0 bill, 1 do not usually bill) when
		///there is one, else the code's NoBillIns.</summary>
		private static bool IsNoBillInsByDefault(DataRow row) {
			string planOverride=(row["PlanOverride"]==DBNull.Value ? "" : row["PlanOverride"].ToString().Trim());
			if(planOverride=="0") {
				return false;//NoBillInsOverride.BillToIns
			}
			if(planOverride=="1") {
				return true;//NoBillInsOverride.DoNotUsuallyBillToIns
			}
			return PIn.Bool(row["CodeNoBillIns"].ToString());
		}
	}
}
