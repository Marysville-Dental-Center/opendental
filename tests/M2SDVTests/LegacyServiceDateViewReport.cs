// The display query of Open Dental 24.3's Service Date View report (OpenDentBusiness/Reports/RpServiceDateView.cs, GetData, as it was before
// package M2-SDV), kept here unchanged as the reference the new builder (ServiceDateViews.Build) must match. It wraps the report's core query,
// which RpServiceDateView still has (private GetCoreQuery, called by reflection). GPL code from the Open Dental 24.3 source this fork is based on.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Reflection;
using OpenPmsDentalBusiness;

namespace M2SDVTests {
	public static class LegacyServiceDateViewReport {
		///<summary>The 24.3 report's result for the same arguments as RpServiceDateView.GetData.</summary>
		public static DataTable GetData(long patNum,bool isFamily,bool isDetailed) {
			int payPlanVersion=PIn.Int(PrefC.GetStringNoCache(PrefName.PayPlansVersion));
			if(payPlanVersion==0) {
				payPlanVersion=1;
			}
			string command=$@"
				/*top layer gets final columns for display*/
				SELECT
					IF(display.TranDate != '', '',display.ProcDate) AS 'Date',
					display.TranDate AS 'Trans Date',
					display.Patient,
					display.Reference,
					display.Charge,
					display.Credit,
					display.Pvdr,
					display.InsBal,
					display.AcctBal,
					display.Type,
					display.TranNum AS 'PrimaryKey',
					display.PatNum
				FROM(
				/*cases out data from rows that we need, performing aggregate functions for day and overall totals.*/
				SELECT
					dup.Num,
					IF(dup.Num = 2, 0,core.ProcNum) AS ProcNum,
					(CASE dup.Num
						WHEN 1
							THEN IF(core.Type IN ('Proc','Adj-Att.','PatPay Att.','WriteOff-Att.','InsPay-Att.','PayPlan Charge Att.','PatPay Att. PayPlan','Unallocated'),1,2) -- get allocated items ordered before unallocated ones
						WHEN 2
							THEN IF(core.Type = 'Unallocated',1,2)
						WHEN 3
							THEN 4
						WHEN 4
							THEN 5
					END) AS OrderingNum,
					(CASE dup.Num
						WHEN 1
							THEN core.Type
						WHEN 2
							THEN 'Unallocated'
						WHEN 3
							THEN 'Day Total'
						WHEN 4
							THEN 'Overall Total'
					END) AS 'Type',
					(CASE dup.Num
						WHEN 1
							THEN core.ProcDate
						WHEN 2
							THEN core.ProcDate
						WHEN 3
							THEN core.ProcDate
						WHEN 4
							THEN NULL
					END) AS 'ProcDate',
					(CASE dup.Num
						WHEN 1
							THEN IF(core.Type = 'Proc', '',core.TranDate)
						WHEN 2
							THEN ''
						WHEN 3
							THEN ''
						WHEN 4
							THEN NULL
					END) AS 'TranDate',
					(CASE dup.Num
						WHEN 1
							THEN core.Patient
						WHEN 2
							THEN '' -- intentionaly blank
						WHEN 3
							THEN ''
						WHEN 4
							THEN '' -- intentionally blank
					END) AS 'Patient',
					(CASE dup.Num
						WHEN 1
							THEN IF(core.Type IN ('Adj-Att.','PatPay Att.','WriteOff-Att.','InsPay-Att.','PayPlan Charge Att.','PatPay Att. PayPlan'),CONCAT('  ',core.Reference),core.Reference)
						WHEN 2
							THEN 'Unallocated Trans'
						WHEN 3
							THEN CONCAT(REPEAT(' ',2),'Total for Date')
						WHEN 4
							THEN {(isFamily ? "'Family Total:'" : "'Patient Total:'" )}
					END) AS 'Reference',
					(CASE dup.Num
						WHEN 1
							THEN FORMAT(SUM(core.Charge),2)
						WHEN 2
							THEN '' -- intentionaly blank
						WHEN 3
							THEN FORMAT(SUM(core.Charge),2) -- total for date
						WHEN 4
							THEN FORMAT(SUM(core.Charge),2) -- overall total
					END) AS 'Charge',
					(CASE dup.Num
						WHEN 1
							THEN FORMAT(SUM(core.Credit),2)
						WHEN 2
							THEN '' -- intentionaly blank
						WHEN 3
							THEN FORMAT(SUM(core.Credit),2) -- total for date
						WHEN 4
							THEN FORMAT(SUM(core.Credit),2) -- overall total
					END) AS 'Credit',
					(CASE dup.Num
						WHEN 1
							THEN core.Prov
						WHEN 2
							THEN '' -- intentionaly blank
						WHEN 3
							THEN '' -- intentionally blank
						WHEN 4
							THEN '' -- intentionally blank
					END) AS 'Pvdr',
					(CASE dup.Num
						WHEN 1
							THEN {(isDetailed ? "IF(core.Type = 'Proc', FORMAT(core.InsCredits,2),'')": "''")} 
						WHEN 2
							THEN '' -- intentionally blank
						WHEN 3
							THEN FORMAT(SUM(IF(core.Type = 'Proc', core.InsCredits,0)),2) -- total for date
						WHEN 4
							THEN FORMAT(SUM(IF(core.Type = 'Proc', core.InsCredits,0)),2) -- overall total
					END) AS 'InsBal',
					(CASE dup.Num
						WHEN 1
							THEN {(isDetailed ? "IF(core.Type = 'Proc', FORMAT(core.Charge - (core.ProcCredits + core.InsCredits),2),'')" : "''")}
						WHEN 2
							THEN '' -- intentionally blank
						WHEN 3
							THEN FORMAT(SUM(core.Charge - core.Credit - core.InsCredits),2) -- total for date
						WHEN 4
							THEN FORMAT(SUM(core.Charge - core.Credit - core.InsCredits),2) -- overall total
					END) AS 'AcctBal',
					TranNum,
					PatNum
				FROM(
				  {GetCoreQuery(patNum,isFamily,payPlanVersion)}
				) core
				INNER JOIN(
				/*add 2 extra rows to every transaction so we can use the data more than once for per day and overall totals without running the query again*/
					SELECT 1 AS Num
					UNION ALL
					SELECT 2
					UNION ALL
					SELECT 3
					UNION ALL
					SELECT 4
				) dup
					ON IF(dup.Num = 2 AND (core.Type LIKE '%Att.' || core.Type LIKE 'Proc'),FALSE,TRUE) -- only join on the 2 row when there are unallocated transactions on a day. 
				GROUP BY (CASE WHEN dup.Num = 1 -- per transaction
								THEN CONCAT(core.TranNum,'|',core.ProcDate,'|',core.Type) -- unique identifier for transaction rows
							WHEN dup.Num IN (2,3) -- per date of service, including an extra row per date for unallocated transactions identifier
								THEN CONCAT(dup.Num,'-',core.ProcDate) -- unique identifier for date of service row
							WHEN dup.Num = 4 -- ending total for family/patient
								THEN dup.Num -- only need to have one row here
						END)
				) display
				/*ensure final order is what we need it to be*/
				ORDER BY
					ISNULL(display.ProcDate),
					display.ProcDate,
					display.OrderingNum,
					display.Patient,
					display.ProcNum DESC,
					display.TranDate,
					FIELD(display.Type,'Proc','Adj-Att.','PatPay Att.','WriteOff-Att.','InsPay-Att.','PayPlan Charge Att.','PatPay Att. PayPlan','Unallocated','PatPay','WriteOff','Adj','InsPay','PayPlan Credit','Dynamic PayPlan Credit','PayPlan Charge','PatPay PayPlan','Day Total','Overall Total')
			";
			return DataCore.GetTable(command);
		}

		///<summary>The core query as the 24.3 report ran it: includePayPlanSplits false (only receipts set it).</summary>
		private static string GetCoreQuery(long patNum,bool isFamily,int payPlanVersion) {
			MethodInfo methodInfo=typeof(RpServiceDateView).GetMethod("GetCoreQuery",BindingFlags.NonPublic|BindingFlags.Static);
			return (string)methodInfo.Invoke(null,new object[] { patNum,isFamily,payPlanVersion,false });
		}
	}
}
