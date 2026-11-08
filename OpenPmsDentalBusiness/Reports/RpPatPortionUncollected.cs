using CodeBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Diagnostics;
using DataConnectionBase;

namespace OpenPmsDentalBusiness {
	public class RpPatPortionUncollected {
		///<summary>Column of the table GetPatUncollected returns: the amount of the procedure put on a patient payment plan (its treatment credits) or a
		///dynamic payment plan (the link's amount override, or the procedure's remaining value when the link has none). Open Dental 26.2 item 34
		///("Pay Plan column added to Patient Portion Uncollected report"); what the column holds is this program's reading (docs/port-notes/P262-REPORTS.md).
		///It does not change the Uncollected column.</summary>
		public const string ColumnPayPlan="PayPlan";
		///<summary>Column of the table GetPatUncollected returns: 1 on a row of a procedure completed before the From date, which only the "Show prior
		///uncollected balances" option lists (Open Dental 26.2 item 33), otherwise 0.</summary>
		public const string ColumnIsPrior="IsPrior";

		///<summary>Completed procedures in the date range (and the selected clinics, when clinics are on) whose patient portion is not collected. With
		///doShowPrior, also the procedures completed before dateFrom that are not collected, of every patient who has a procedure completed in the date
		///range in those clinics; those rows have IsPrior 1 and come first (they are older). Rows are ordered by date, then patient and code.</summary>
		public static DataTable GetPatUncollected(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,bool doShowPrior=false) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),dateFrom,dateTo,listClinicNums,doShowPrior);
			}
			Stopwatch s=new Stopwatch();;
			if(ODBuild.IsDebug()) {
				s.Start();
			}
			bool hasClinicsEnabled=ReportsComplex.RunFuncOnReportServer(() => Prefs.HasClinicsEnabledNoCache);
			List<long> listHiddenUnearnedDefNums=ReportsComplex.RunFuncOnReportServer(() =>
				Defs.GetDefsNoCache(DefCat.PaySplitUnearnedType).FindAll(x => !string.IsNullOrEmpty(x.ItemValue)).Select(x => x.DefNum).ToList()
			);
			string query=GetQuery(dateFrom,dateTo,listClinicNums,hasClinicsEnabled,listHiddenUnearnedDefNums,isPrior:false);
			DataTable table=ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(query));
			if(doShowPrior) {
				string queryPrior=GetQuery(dateFrom,dateTo,listClinicNums,hasClinicsEnabled,listHiddenUnearnedDefNums,isPrior:true);
				DataTable tablePrior=ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(queryPrior));
				//Every prior procedure is older than every procedure of the date range, so the prior rows first keeps the order by date.
				DataTable tableAll=table.Clone();
				foreach(DataRow row in tablePrior.Rows) {
					tableAll.ImportRow(row);
				}
				foreach(DataRow row in table.Rows) {
					tableAll.ImportRow(row);
				}
				table=tableAll;
			}
			if(ODBuild.IsDebug()) {
				s.Stop();
				Console.WriteLine("Total time to generate report with "+string.Format("{0:#,##0.##}",table.Rows.Count)+" rows: "
					+(s.Elapsed.Hours>0?(s.Elapsed.Hours+" hours "):"")+(s.Elapsed.Minutes>0?(s.Elapsed.Minutes+" min "):"")
					+(s.Elapsed.TotalSeconds-(s.Elapsed.Hours*60*60)-(s.Elapsed.Minutes*60))+" sec");
			}
			return table;
		}

		///<summary>The query of the date range (isPrior false: the report as before 26.2, plus the PayPlan and IsPrior columns at the end) or of the
		///prior procedures (isPrior true). A prior procedure counts the adjustments attached to it dated up to dateTo; a procedure of the date range,
		///as before, those dated in the date range. Payments attached to the procedure count whatever their date, as before.</summary>
		private static string GetQuery(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,bool hasClinicsEnabled,List<long> listHiddenUnearnedDefNums,
			bool isPrior)
		{
			string whereClinics="";
			if(hasClinicsEnabled && listClinicNums.Count>0) {
				whereClinics=$@"
						AND procedurelog.ClinicNum IN({string.Join(",",listClinicNums.Select(x => SOut.Long(x)))})";
			}
			string query=$@"SELECT proc.PatNum, proc.ProcDate,CONCAT(patient.LName,', ',patient.FName) Patient,procedurecode.AbbrDesc,proc.Fee,
				proc.Fee-proc.InsEst PatPortion,
				COALESCE(adj.adjAmt,0) Adjustment,
				COALESCE(pay.splitAmt,0) Payment,
				proc.Fee-proc.InsEst+COALESCE(adj.adjAmt,0)-COALESCE(pay.splitAmt,0) Uncollected,
				ROUND(COALESCE(ppc.creditAmt,0)+COALESCE(ppl.overrideAmt,0)
					+IF(COALESCE(ppl.hasNoOverride,0)=1,
						GREATEST(proc.Fee-proc.InsEst+COALESCE(adj.adjAmt,0)-COALESCE(pay.splitAmtNoPlan,0),0),0),2) {ColumnPayPlan},
				{(isPrior ? "1" : "0")} {ColumnIsPrior}
				FROM (
					SELECT procedurelog.ProcNum,procedurelog.CodeNum,procedurelog.ProcDate,procedurelog.PatNum,
					procedurelog.ProcFee*(procedurelog.BaseUnits+procedurelog.UnitQty)
						-SUM(IF(claimproc.Status={SOut.Int((int)ClaimProcStatus.CapComplete)},claimproc.WriteOff,0)) Fee,
					SUM(IF(claimproc.Status={SOut.Int((int)ClaimProcStatus.NotReceived)},claimproc.InsPayEst,
						IF(claimproc.Status IN({SOut.Int((int)ClaimProcStatus.Received)},{SOut.Int((int)ClaimProcStatus.Supplemental)}),claimproc.InsPayAmt,0)))
						+SUM(IF(claimproc.Status NOT IN({SOut.Int((int)ClaimProcStatus.CapComplete)},{SOut.Int((int)ClaimProcStatus.InsHist)}),claimproc.WriteOff,0)) InsEst
					FROM procedurelog";
			if(isPrior) {
				//Only the patients who have a procedure completed in the date range (in the selected clinics).
				query+=$@"
					INNER JOIN (
						SELECT DISTINCT procedurelog.PatNum
						FROM procedurelog
						WHERE procedurelog.ProcStatus={SOut.Int((int)ProcStat.C)}
						AND procedurelog.ProcDate BETWEEN {SOut.Date(dateFrom)} AND {SOut.Date(dateTo)}{whereClinics}
					) patseen ON patseen.PatNum=procedurelog.PatNum";
			}
			query+=$@"
					LEFT JOIN claimproc ON claimproc.ProcNum=procedurelog.ProcNum
						AND claimproc.Status IN ({
							string.Join(",",new[] { ClaimProcStatus.NotReceived,ClaimProcStatus.Received,ClaimProcStatus.Supplemental,ClaimProcStatus.CapComplete }
								.Select(x => SOut.Int((int)x)))})
					WHERE procedurelog.ProcStatus={SOut.Int((int)ProcStat.C)}";
			if(isPrior) {
				query+=$@"
					AND procedurelog.ProcDate < {SOut.Date(dateFrom)}";
			}
			else {
				query+=$@"
					AND procedurelog.ProcDate BETWEEN {SOut.Date(dateFrom)} AND {SOut.Date(dateTo)}";
			}
			query+=whereClinics;
			query+=$@"
					GROUP BY procedurelog.ProcNum
					ORDER BY NULL
				) proc
				INNER JOIN patient ON patient.PatNum=proc.PatNum
				INNER JOIN procedurecode ON procedurecode.CodeNum=proc.CodeNum
				LEFT JOIN (
					SELECT adjustment.ProcNum,SUM(adjustment.AdjAmt) - COALESCE(SUM(paysplit.SplitAmt),0) adjAmt
					FROM adjustment
					LEFT JOIN paysplit
						ON paysplit.AdjNum = adjustment.AdjNum
						AND paysplit.AdjNum != 0 ";
			if(listHiddenUnearnedDefNums.Count>0) {
				query+=$"AND paysplit.UnearnedType NOT IN ({string.Join(",",listHiddenUnearnedDefNums)}) ";
			}
			query+=$@"WHERE adjustment.ProcNum!=0";
			if(isPrior) {
				query+=$@"
					AND adjustment.ProcDate <= {SOut.Date(dateTo)}";
			}
			else {
				query+=$@"
					AND adjustment.ProcDate BETWEEN {SOut.Date(dateFrom)} AND {SOut.Date(dateTo)}";
			}
			query+=$@"
					GROUP BY adjustment.ProcNum
					ORDER BY NULL
				) adj ON adj.ProcNum=proc.ProcNum
				LEFT JOIN (
					SELECT paysplit.ProcNum,SUM(paysplit.SplitAmt) splitAmt,
					SUM(IF(paysplit.PayPlanNum=0 AND paysplit.PayPlanChargeNum=0,paysplit.SplitAmt,0)) splitAmtNoPlan
					FROM paysplit
					WHERE paysplit.ProcNum!=0 ";
			if(listHiddenUnearnedDefNums.Count>0) {
				query+=$"AND paysplit.UnearnedType NOT IN ({string.Join(",",listHiddenUnearnedDefNums)}) ";
			}
			query+=$@"GROUP BY paysplit.ProcNum
					ORDER BY NULL
				) pay ON pay.ProcNum=proc.ProcNum
				LEFT JOIN (
					SELECT payplancharge.ProcNum,SUM(payplancharge.Principal) creditAmt
					FROM payplancharge
					INNER JOIN payplan ON payplan.PayPlanNum=payplancharge.PayPlanNum
						AND payplan.PlanNum=0
					WHERE payplancharge.ChargeType={SOut.Int((int)PayPlanChargeType.Credit)}
					AND payplancharge.ProcNum!=0
					GROUP BY payplancharge.ProcNum
					ORDER BY NULL
				) ppc ON ppc.ProcNum=proc.ProcNum
				LEFT JOIN (
					SELECT payplanlink.FKey ProcNum,
					SUM(IF(ABS(payplanlink.AmountOverride)>=0.005,payplanlink.AmountOverride,0)) overrideAmt,
					MAX(ABS(payplanlink.AmountOverride)<0.005) hasNoOverride
					FROM payplanlink
					WHERE payplanlink.LinkType={SOut.Int((int)PayPlanLinkType.Procedure)}
					GROUP BY payplanlink.FKey
					ORDER BY NULL
				) ppl ON ppl.ProcNum=proc.ProcNum
				WHERE proc.Fee-proc.InsEst+COALESCE(adj.adjAmt,0)-COALESCE(pay.splitAmt,0)>0.005
				ORDER BY proc.ProcDate,patient.LName,patient.FName,procedurecode.ProcCode";
			return query;
		}
	}
}
