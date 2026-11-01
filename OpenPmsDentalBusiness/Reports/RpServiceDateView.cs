using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenPmsDentalBusiness {
	public class RpServiceDateView {
		///<summary>The 24.3 report's table (see ServiceDateViews.ToReportTable), now built by ServiceDateViews.Build from the rows of GetCoreTable, so
		///the Service Date View window, the Account module's service-date view and service-date statements show the same rows.</summary>
		public static DataTable GetData(long patNum,bool isFamily,bool isDetailed) {
			Meth.NoCheckMiddleTierRole();//GetCoreTable makes the middle tier call.
			ServiceDateViewOptions options=new ServiceDateViewOptions();
			options.IsFamily=isFamily;
			options.IsDetailed=isDetailed;
			return ServiceDateViews.ToReportTable(GetRows(patNum,isFamily,options));
		}

		///<summary>The rows of the service-date view of a patient (isFamily false) or of the patient's whole family (patNum is then the guarantor), in
		///display order. options.IsFamily is set from isFamily. A receipt (options.IsReceipt) reads the payment plan payments too, whatever
		///PayPlansVersion is, so it lists every patient payment of its range (26.1 spec rule 3).</summary>
		public static List<ServiceDateViewRow> GetRows(long patNum,bool isFamily,ServiceDateViewOptions options) {
			Meth.NoCheckMiddleTierRole();//GetCoreTable makes the middle tier call.
			options=options??new ServiceDateViewOptions();
			options.IsFamily=isFamily;
			bool includePayPlanSplits=options.IsReceipt && options.InvoiceItems==null;//Build treats an invoice as an invoice even when IsReceipt is set.
			return ServiceDateViews.Build(ServiceDateViews.ReadCoreRows(GetCoreTable(patNum,isFamily,includePayPlanSplits)),options);
		}

		///<summary>One row per procedure and per transaction, each dated by the service it belongs to: the query of the 24.3 report, plus the
		///ClinicNum, ProvNum, ParentNum (PayNum, ClaimNum or PayPlanNum), CodeNum, ProcCode, ToothNum, ToothRange and Surf columns. When isFamily is
		///true, patNum is the guarantor and the whole family is included. Patient payment splits attached to a payment plan are returned only under
		///PayPlansVersion 2, as in the 24.3 report, unless includePayPlanSplits is true (receipts): they then come back in every version, as
		///'PatPay PayPlan' or 'PatPay Att. PayPlan' rows.</summary>
		public static DataTable GetCoreTable(long patNum,bool isFamily,bool includePayPlanSplits=false) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),patNum,isFamily,includePayPlanSplits);
			}
			int payPlanVersion=PIn.Int(PrefC.GetStringNoCache(PrefName.PayPlansVersion));
			if(payPlanVersion==0) {
				payPlanVersion=1;
			}
			string command=GetCoreQuery(patNum,isFamily,payPlanVersion,includePayPlanSplits);
			return ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(command));
		}

		///<summary>Get core data ordered by procedure date and transactions attached to procs first, with specific ordering for transaction type.
		///Using aging preference to get payment plan info. Defining transaction type separate from reference for specific ordering.</summary>
		private static string GetCoreQuery(long patNum,bool isFamily,int payPlanVersion,bool includePayPlanSplits) {
			Meth.NoCheckMiddleTierRole();//Private method.
			string wherePatOrFam=isFamily ? $"patient.Guarantor={POut.Long(patNum)}":$"patient.PatNum={POut.Long(patNum)}";
			#region Procedures
			//Get all completed procedures for patient/family with charges and credits. Also includes separate column for insurance credits only.
			string command=$@"SELECT 'Proc' AS 'Type', 
				procedurelog.ProcNum AS 'TranNum', 
				procedurelog.ProcDate, 
				'0001-01-01' AS 'TranDate', 
				procedurelog.PatNum, 
				patient.FName AS Patient, 
				procedurelog.ProcNum, 
				CONCAT(procedurecode.ProcCode,':',COALESCE(NULLIF(CONCAT('#',procedurelog.ToothNum,'-'),'#-'),NULLIF(CONCAT(IF(LENGTH(procedurelog.ToothRange)>0,'Tth Rng',''),'-'),'-'),''),COALESCE(NULLIF(CONCAT(procedurelog.Surf,'-'),'-'),''),procedurecode.AbbrDesc) AS 'Reference', 
				procedurelog.ProcFee*(procedurelog.BaseUnits+procedurelog.UnitQty)- 
				COALESCE((SELECT SUM(claimproc.WriteOff) FROM claimproc WHERE claimproc.Status={POut.Int((int)ClaimProcStatus.CapComplete)} AND claimproc.ProcNum=procedurelog.ProcNum),0) AS 'Charge', 
				0 AS 'Credit', 
				0 AS 'InsPayEst', 
				(SELECT Abbr FROM provider WHERE provider.ProvNum=procedurelog.ProvNum) AS Prov, 
				(COALESCE((SELECT -SUM(AdjAmt) FROM adjustment WHERE adjustment.ProcNum=procedurelog.ProcNum),0)+ 
					COALESCE((SELECT SUM(SplitAmt) FROM paysplit WHERE paysplit.ProcNum=procedurelog.ProcNum),0)+ 
					COALESCE((SELECT SUM(WriteOff) + SUM(InsPayAmt) FROM claimproc 
						WHERE claimproc.ProcNum=procedurelog.ProcNum 
						AND claimproc.Status IN({POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)},{POut.Int((int)ClaimProcStatus.CapClaim)})),0) 
				) AS 'ProcCredits', 
				COALESCE((SELECT SUM(WriteOff) + SUM(InsPayEst) FROM claimproc WHERE claimproc.Status={POut.Int((int)ClaimProcStatus.NotReceived)} 
					AND claimproc.ProcNum=procedurelog.ProcNum 
				),0) AS 'InsCredits', 
				procedurelog.ClinicNum,procedurelog.ProvNum,0 AS 'ParentNum',procedurelog.CodeNum,procedurecode.ProcCode,procedurelog.ToothNum,procedurelog.ToothRange,procedurelog.Surf 
				FROM procedurelog 
				INNER JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum 
				INNER JOIN patient ON patient.PatNum=procedurelog.PatNum 
				WHERE procedurelog.ProcStatus={POut.Int((int)ProcStat.C)} 
				AND {wherePatOrFam} ";
			#endregion
			#region Adjustment
			//Get all adjustment for patient/family. Positive adjustment as Charges and negative adjustments as Credits. 
			command+=$@"
				UNION ALL
				SELECT IF(adjustment.ProcNum > 0,'Adj-Att.','Adj') AS 'Type',
				adjustment.AdjNum AS 'TranNum',
				IF(adjustment.ProcDate = '0001-01-01',adjustment.AdjDate,adjustment.ProcDate) AS ProcDate,
				adjustment.AdjDate AS 'TranDate',
				adjustment.PatNum,
				patient.FName AS Patient,
				adjustment.ProcNum,
				CONCAT('Adj- ',(SELECT ItemName FROM definition WHERE definition.DefNum=adjustment.AdjType)) AS 'Reference',
				IF(adjustment.AdjAmt > 0,adjustment.AdjAmt,0) AS 'Charge',
				IF(adjustment.AdjAmt <= 0,-adjustment.AdjAmt,0) AS 'Credit',
				0 AS 'InsPayEst',
				(SELECT Abbr FROM provider WHERE provider.ProvNum=adjustment.ProvNum) AS Prov,
				0 AS 'ProcCredits',
				0 AS 'InsCredits', 
				adjustment.ClinicNum,adjustment.ProvNum,0 AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
				FROM adjustment 
				INNER JOIN patient ON patient.PatNum=adjustment.PatNum 
				WHERE {wherePatOrFam} ";
			#endregion
			#region Pat Payments
			//Get all payments, except where they should only show inside payplan. Receipts (includePayPlanSplits) get the payment plan payments in
			//every version.
			bool hasPayPlanSplits=(payPlanVersion==2 || includePayPlanSplits);
			command+=$@"
				UNION ALL 
				SELECT (CASE WHEN paysplit.ProcNum > 0 AND paysplit.PayPlanNum > 0 THEN 'PatPay Att. PayPlan' 
				WHEN paysplit.ProcNum > 0 THEN 'PatPay Att.' 
				WHEN paysplit.ProcNum = 0 AND paysplit.PayPlanNum > 0 
				THEN 'PatPay PayPlan' 
				ELSE 'PatPay' END) AS 'Type', 
				paysplit.SplitNum AS 'TranNum', 
				COALESCE((SELECT ProcDate FROM procedurelog WHERE procedurelog.ProcNum=paysplit.ProcNum), paysplit.DatePay) AS 'ProcDate', 
				paysplit.DatePay AS 'TranDate', 
				paysplit.PatNum, 
				patient.FName AS Patient, 
				paysplit.ProcNum, 
				CONCAT('PatPay- ',IF(paysplit.UnearnedType > 0,'PrePay ',''),COALESCE((SELECT definition.ItemName FROM payment INNER JOIN definition ON definition.DefNum=payment.PayType 
					WHERE payment.PayNum = paysplit.PayNum),'Inc Transfer')) AS 'Reference', 
				0 AS 'Charge', 
				paysplit.SplitAmt AS 'Credit', 
				0 AS 'InsPayEst', 
				(SELECT Abbr FROM provider WHERE provider.ProvNum = paysplit.ProvNum) AS Prov, 
				0 AS 'ProcCredits', 
				0 AS 'InsCredits', 
				paysplit.ClinicNum,paysplit.ProvNum,paysplit.PayNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
				FROM paysplit 
				INNER JOIN patient ON patient.PatNum=paysplit.PatNum 
				WHERE {(hasPayPlanSplits ? "TRUE": "paysplit.PayPlanNum=0")}
				AND {wherePatOrFam} ";
			#endregion
			#region Ins Payments
			//Get all insurance payments
			command+=$@"
				UNION ALL 
				SELECT IF(claimproc.ProcNum > 0,'InsPay-Att.','InsPay') AS 'Type', 
				claimproc.ClaimProcNum AS 'TranNum', 
				IF(claimproc.ProcNum > 0,claimproc.ProcDate,claimproc.DateCP) AS 'ProcDate', 
				claimproc.DateCP AS 'TranDate', 
				claimproc.PatNum, 
				patient.FName AS Patient, 
				claimproc.ProcNum, 
				CONCAT('InsPay- ',COALESCE((SELECT ItemName FROM claimpayment INNER JOIN definition ON definition.DefNum=claimpayment.PayType 
					WHERE claimpayment.ClaimPaymentNum=claimproc.ClaimPaymentNum),'Not Finalized')) AS 'Reference', 
				0 AS 'Charge', 
				claimproc.InsPayAmt AS 'Credit', 
				0 AS 'InsPayEst', 
				(SELECT Abbr FROM provider WHERE provider.ProvNum=claimproc.ProvNum) AS Prov, 
				0 AS 'ProcCredits', 
				0 AS 'InsCredits', 
				claimproc.ClinicNum,claimproc.ProvNum,claimproc.ClaimNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
				FROM claimproc 
				INNER JOIN patient ON patient.PatNum=claimproc.PatNum 
				WHERE claimproc.Status IN({POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)},{POut.Int((int)ClaimProcStatus.CapClaim)}) 
				AND claimproc.PayPlanNum=0 
				AND claimproc.InsPayAmt!=0 
				AND {wherePatOrFam} ";
			#endregion
			#region WriteOffs
			//Get all writeoffs, estimated and actual. Only will show estimated when claim is outstanding, otherwise shows actual with payment type of payment.
			command+=$@"
				UNION ALL
				SELECT IF(claimproc.ProcNum > 0,'WriteOff-Att.','WriteOff') AS 'Type', 
				CONCAT('W',claimproc.ClaimProcNum) AS 'TranNum', 
				IF(claimproc.ProcNum > 0,claimproc.ProcDate,claimproc.DateCP) AS 'ProcDate', 
				claimproc.DateCP AS 'TranDate', 
				claimproc.PatNum, 
				patient.FName AS Patient, 
				claimproc.ProcNum, 
				(CASE WHEN claimproc.Status={POut.Int((int)ClaimProcStatus.NotReceived)} AND claimproc.InsPayEst > 0  
					THEN CONCAT('Ins Pay Est:',claimproc.InsPayEst,IF(claimproc.WriteOff>0,CONCAT(' WriteOff:',claimproc.WriteOff),'')) 
					WHEN claimproc.Status={POut.Int((int)ClaimProcStatus.NotReceived)} AND claimproc.Writeoff > 0 AND claimproc.InsPayEst <= 0 
					THEN CONCAT('WriteOff Est:',claimproc.WriteOff) 
					ELSE CONCAT('W/O- ',COALESCE((SELECT ItemName FROM claimpayment INNER JOIN definition ON definition.DefNum=claimpayment.PayType 
						WHERE claimpayment.ClaimPaymentNum=claimproc.ClaimPaymentNum),'')) END) AS 'Reference', 
				0 AS 'Charge', 
				IF(claimproc.Status IN({POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)}),claimproc.WriteOff,0) AS 'Credit', 
				IF(claimproc.Status = 0,claimproc.InsPayEst,0) AS 'InsPayEst', 
				(SELECT Abbr FROM provider WHERE provider.ProvNum = claimproc.ProvNum) AS Prov, 
				0 AS 'ProcCredits', 
				0 AS 'InsCredits', 
				claimproc.ClinicNum,claimproc.ProvNum,claimproc.ClaimNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
				FROM claimproc 
				INNER JOIN patient ON patient.PatNum=claimproc.PatNum 
				WHERE IF(claimproc.Status IN ({POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)}),ABS(claimproc.WriteOff) > 0.005,(ABS(claimproc.WriteOff) > 0.005 OR claimproc.InsPayEst > 0)) 
				AND claimproc.Status IN({POut.Int((int)ClaimProcStatus.NotReceived)},{POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)}) 
				AND {wherePatOrFam} ";
			#endregion
			#region PayPlan Version 1
			if(payPlanVersion==1) {
				//get only pay plan credits for pay plan versions 1
				command+=$@"
					UNION ALL 
					SELECT 'PayPlan Credit' AS 'Type', 
					payplan.PayPlanNum AS 'TranNum', 
					payplan.PayPlanDate AS 'ProcDate', 
					payplan.PayPlanDate AS 'TranDate', 
					payplan.PatNum, 
					patient.FName AS Patient, 
					0 AS 'ProcNum', 
					'PayPlan Credit' AS 'Reference', 
					0 AS 'Charge', 
					payplan.CompletedAmt AS 'Credit', 
					0 AS 'InsPayEst', 
					(SELECT Abbr FROM provider WHERE provider.ProvNum = payplancharge.ProvNum) AS Prov, 
					0 AS 'ProcCredits', 
					0 AS 'InsCredits', 
					COALESCE(payplancharge.ClinicNum,0) AS 'ClinicNum',COALESCE(payplancharge.ProvNum,0) AS 'ProvNum',payplan.PayPlanNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
					FROM payplan 
					LEFT JOIN payplancharge ON payplancharge.PayPlanNum=payplan.PayPlanNum AND payplancharge.ChargeType=0 
					INNER JOIN patient ON patient.PatNum=payplan.PatNum 
					WHERE {wherePatOrFam} 
					GROUP BY payplan.PayPlanNum ";
			}
			#endregion
			#region PayPlan Version 2
			else if(payPlanVersion==2) {
				//get all pay plan credits and debits for pay plan version 2
				command+=$@"
					UNION ALL
					SELECT (CASE WHEN payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Debit)} AND payplancharge.ProcNum > 0 THEN 'PayPlan Charge Att.' 
					WHEN payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Debit)} AND payplancharge.ProcNum=0 THEN 'PayPlan Charge' 
					WHEN payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Credit)} THEN 'PayPlan Credit' END) AS 'Type', 
					payplancharge.PayPlanChargeNum AS 'TranNum', 
					payplancharge.ChargeDate AS 'ProcDate', 
					payplancharge.ChargeDate AS 'TranDate', 
					payplancharge.PatNum, 
					patient.FName AS Patient, 
					payplancharge.ProcNum, 
					(CASE WHEN payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Debit)} AND payplancharge.ProcNum > 0 THEN 'PayPlan Charge Att.' 
						WHEN payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Debit)} AND payplancharge.ProcNum=0 THEN 'PayPlan Charge' 
						WHEN payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Credit)} THEN 'PayPlan Credit'END) AS 'Reference', 
					IF(payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Debit)},payplancharge.Principal+payplancharge.Interest,0) AS 'Charge', 
					IF(payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Credit)},payplancharge.Principal,0) AS 'Credit', 
					0 AS 'InsPayEst', 
					(SELECT Abbr FROM provider WHERE provider.ProvNum=payplancharge.ProvNum) AS Prov, 
					0 AS 'ProcCredits', 
					0 AS 'InsCredits', 
					payplancharge.ClinicNum,payplancharge.ProvNum,payplancharge.PayPlanNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
					FROM payplancharge 
					LEFT JOIN payplan ON payplan.PayplanNum=payplancharge.PayPlanNum 
					INNER JOIN patient ON patient.PatNum=payplancharge.PatNum 
					WHERE payplancharge.ChargeDate <= CURDATE() 
					AND IF(payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Debit)},payplan.PlanNum=0,TRUE) 
					AND {wherePatOrFam} ";
			}
			#endregion
			#region PayPlan Version 3
			else if(payPlanVersion==3) {
				//get only pay plan credits for pay plan versions 3
				command+=$@"
					UNION ALL
					SELECT 'PayPlan Credit' AS 'Type', 
					payplancharge.PayPlanChargeNum AS 'TranNum', 
					payplancharge.ChargeDate AS 'ProcDate', 
					payplancharge.ChargeDate AS 'TranDate', 
					payplancharge.PatNum, 
					patient.FName AS Patient, 
					payplancharge.ProcNum, 
					'PayPlan Credit' AS 'Reference', 
					0 AS 'Charge', 
					IF(payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Credit)},payplancharge.Principal,0) AS 'Credit', 
					0 AS 'InsPayEst', 
					(SELECT Abbr FROM provider WHERE provider.ProvNum=payplancharge.ProvNum) AS Prov, 
					0 AS 'ProcCredits', 
					0 AS 'InsCredits', 
					payplancharge.ClinicNum,payplancharge.ProvNum,payplancharge.PayPlanNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
					FROM payplancharge 
					INNER JOIN patient ON patient.PatNum=payplancharge.PatNum 
					WHERE payplancharge.ChargeType={POut.Int((int)PayPlanChargeType.Credit)} 
					AND {wherePatOrFam} ";
			}
			#endregion
			else { 
				//Version 4 will not show payplan information
			}
			#region Dynamic Payment Plans (Credits) 
			if(payPlanVersion==2 || payPlanVersion==3) {//PayPlanVersions.AgeCreditsAndDebits or PayPlanVersions.AgeCreditsOnly
				//Dynamic payment plan charges will have been picked up by their cooresponding version query above. However, credits for dynamic payment plans
				//are not made from paymentplancharges when using dynamic payment plans,they are their own table. We need to handle the credits here 
				//according to what the user wants to see based on their payment plans version. 
				command+=$@"
					UNION ALL
					SELECT 'Dynamic PayPlan Credit' AS 'Type', 
					payplan.PayPlanNum AS 'TranNum', 
					payplanlink.SecDateTEntry AS 'ProcDate', 
					payplanlink.SecDateTEntry AS 'TranDate', 
					payplan.PatNum, 
					patient.FName AS Patient, 
					0 AS 'ProcNum', 
					'PayPlan Credit' AS 'Reference', 
					0 AS 'Charge', 
					(CASE WHEN ABS(payplanlink.AmountOverride)>0.005 THEN payplanlink.AmountOverride ELSE prodlink.Fee END) AS 'Credit', 
					0 AS 'InsPayEst', 
					(SELECT Abbr FROM provider WHERE provider.ProvNum = prodlink.ProvNum) AS Prov, 
					0 AS 'ProcCredits', 
					0 AS 'InsCredits', 
					COALESCE(prodlink.ClinicNum,0) AS 'ClinicNum',COALESCE(prodlink.ProvNum,0) AS 'ProvNum',payplan.PayPlanNum AS 'ParentNum',0 AS 'CodeNum','' AS 'ProcCode','' AS 'ToothNum','' AS 'ToothRange','' AS 'Surf' 
					FROM payplanlink
					INNER JOIN payplan ON payplan.PayPlanNum = payplanlink.PayPlanNum 
					INNER JOIN patient ON patient.PatNum=payplan.PatNum 
					LEFT JOIN (
						SELECT procedurelog.PatNum,
								(procedurelog.ProcFee*(procedurelog.UnitQty+procedurelog.BaseUnits)
								+COALESCE(procAdj.AdjAmt,0)+COALESCE(procClaimProc.InsPay,0)
								+COALESCE(procClaimProc.WriteOff,0)
								+COALESCE(procSplit.SplitAmt,0)
								) Fee,
								procedurelog.ProcNum Num,payplanlink.LinkType,payplanlink.PayPlanLinkNum,procedurelog.ClinicNum,procedurelog.ProvNum 
						FROM payplanlink 
						INNER JOIN procedurelog ON procedurelog.ProcNum=payplanlink.FKey AND payplanlink.LinkType={POut.Int((int)PayPlanLinkType.Procedure)} 
						LEFT JOIN (
							SELECT SUM(adjustment.AdjAmt) AdjAmt,adjustment.ProcNum
							FROM adjustment
							WHERE adjustment.PatNum IN ({patNum})
							GROUP BY adjustment.ProcNum
						)procAdj ON procAdj.ProcNum=procedurelog.ProcNum
						LEFT JOIN (
							SELECT SUM(COALESCE((CASE WHEN claimproc.Status IN (
									{POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)},{POut.Int((int)ClaimProcStatus.CapComplete)}
								) THEN claimproc.InsPayAmt 
								WHEN claimproc.InsEstTotalOverride!=-1 THEN claimproc.InsEstTotalOverride ELSE claimproc.InsPayEst END),0)*-1) InsPay
							,SUM(COALESCE((CASE WHEN claimproc.Status IN (
									{POut.Int((int)ClaimProcStatus.Received)},{POut.Int((int)ClaimProcStatus.Supplemental)},{POut.Int((int)ClaimProcStatus.CapComplete)}
								)	THEN claimproc.WriteOff 
								WHEN claimproc.WriteOffEstOverride!=-1 THEN claimproc.WriteOffEstOverride 
								WHEN claimproc.WriteOffEst!=-1 THEN claimproc.WriteOffEst ELSE 0 END),0)*-1) WriteOff
							,claimproc.ProcNum
							FROM claimproc 
							WHERE claimproc.Status!={POut.Int((int)ClaimProcStatus.Preauth)}
							AND claimproc.PatNum IN ({patNum})
							GROUP BY claimproc.ProcNum
						)procClaimProc ON procClaimProc.ProcNum=procedurelog.ProcNum 
						LEFT JOIN (
							SELECT SUM(paysplit.SplitAmt)*-1 SplitAmt,paysplit.ProcNum
							FROM paysplit
							WHERE paysplit.PayPlanNum=0
							AND paysplit.PatNum IN ({patNum})
							GROUP BY paysplit.ProcNum
						)procSplit ON procSplit.ProcNum=procedurelog.ProcNum
						UNION ALL
						SELECT adjustment.PatNum,adjustment.AdjAmt + COALESCE(adjSplit.SplitAmt,0) Fee,adjustment.AdjNum Num,payplanlink.LinkType
							,payplanlink.PayPlanLinkNum,adjustment.ClinicNum,adjustment.ProvNum
							FROM payplanlink 
							INNER JOIN adjustment ON adjustment.AdjNum=payplanlink.FKey 
								AND payplanlink.LinkType={POut.Int((int)PayPlanLinkType.Adjustment)}
								AND adjustment.ProcNum=0
							LEFT JOIN (
								SELECT SUM(COALESCE(paysplit.SplitAmt,0))*-1 SplitAmt,paysplit.AdjNum
								FROM paysplit
								WHERE paysplit.PayPlanNum=0
								AND paysplit.PatNum IN ({patNum})
								GROUP BY paysplit.AdjNum
							)adjSplit ON adjSplit.AdjNum=adjustment.AdjNum
					) prodlink ON prodlink.PayPlanLinkNum=payplanlink.PayPlanLinkNum 
					WHERE (payplan.Guarantor IN ({patNum}) OR payplan.PatNum IN ({patNum})) ";
				}
			#endregion
			//Order the core data
			command+=@"
				ORDER BY ProcDate,ProcNum DESC, PatNum, TranDate,
				FIELD('Type','Proc','Adj-Att.','PatPay Att.','WriteOff-Att.','InsPay-Att.','PayPlan Charge Att.','PatPay Att. PayPlan','Unallocated','PatPay',
				'WriteOff','Adj','InsPay','PayPlan Credit','Dynamic PayPlan Credit','PayPlan Charge','PatPay PayPlan') ";
			return command;
		}
	}
}
