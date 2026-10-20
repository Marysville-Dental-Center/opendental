-- Business totals and consistency checks recorded by "SyntheticData validate".
--
-- Each query starts with a "-- name: <total>" line and returns two columns: k (a key, '' for a single value) and v (the value).
-- A query that returns several rows gives one total per row, named "<total>.<k>". The validator sets @AsOf (a 'yyyy-MM-dd' date)
-- before running the file. Queries that fail (for example after a column was dropped) are recorded as failed, not skipped.
--
-- Totals named "check.*" count rows that break an invariant of the generated data; 0 means the check passes. They describe the
-- synthetic ledger model (see README.md), so on a real office database some of them will not be 0.

-- name: patients.by_status
SELECT PatStatus k, COUNT(*) v FROM patient GROUP BY PatStatus ORDER BY PatStatus;

-- name: patients.families
SELECT '' k, COUNT(*) v FROM patient WHERE PatNum=Guarantor;

-- name: patients.portal_active_email_nonnull
SELECT '' k, COUNT(*) v FROM patient WHERE _portal_active_email IS NOT NULL;

-- name: patients.email_shared_by_inactive
SELECT '' k, COUNT(*) v FROM patient p WHERE p.PatStatus<>0 AND p.Email<>''
	AND EXISTS(SELECT 1 FROM patient a WHERE a.PatStatus=0 AND a.Email=p.Email AND a.PatNum<>p.PatNum);

-- name: production.gross
SELECT '' k, ROUND(IFNULL(SUM(ProcFee*(UnitQty+BaseUnits)),0),2) v FROM procedurelog WHERE ProcStatus=2 AND ProcDate<=@AsOf;

-- name: production.gross_by_year
SELECT YEAR(ProcDate) k, ROUND(SUM(ProcFee*(UnitQty+BaseUnits)),2) v FROM procedurelog WHERE ProcStatus=2 AND ProcDate<=@AsOf
GROUP BY YEAR(ProcDate) ORDER BY 1;

-- name: production.by_provider
SELECT ProvNum k, ROUND(SUM(ProcFee*(UnitQty+BaseUnits)),2) v FROM procedurelog WHERE ProcStatus=2 AND ProcDate<=@AsOf
GROUP BY ProvNum ORDER BY ProvNum;

-- name: production.adjustments
SELECT '' k, ROUND(IFNULL(SUM(AdjAmt),0),2) v FROM adjustment WHERE AdjDate<=@AsOf;

-- name: production.adjustments_by_type
SELECT AdjType k, ROUND(SUM(AdjAmt),2) v FROM adjustment WHERE AdjDate<=@AsOf GROUP BY AdjType ORDER BY AdjType;

-- name: production.writeoffs
SELECT '' k, ROUND(IFNULL(SUM(WriteOff),0),2) v FROM claimproc WHERE Status IN (0,1,4) AND ClaimNum>0;

-- name: production.net
SELECT '' k, ROUND(
	(SELECT IFNULL(SUM(ProcFee*(UnitQty+BaseUnits)),0) FROM procedurelog WHERE ProcStatus=2 AND ProcDate<=@AsOf)
	+(SELECT IFNULL(SUM(AdjAmt),0) FROM adjustment WHERE AdjDate<=@AsOf)
	-(SELECT IFNULL(SUM(WriteOff),0) FROM claimproc WHERE Status IN (0,1,4) AND ClaimNum>0),2) v;

-- name: income.patient_by_year
SELECT YEAR(DatePay) k, ROUND(SUM(SplitAmt),2) v FROM paysplit WHERE DatePay<=@AsOf GROUP BY YEAR(DatePay) ORDER BY 1;

-- name: income.patient
SELECT '' k, ROUND(IFNULL(SUM(SplitAmt),0),2) v FROM paysplit WHERE DatePay<=@AsOf;

-- name: income.patient_unearned
SELECT '' k, ROUND(IFNULL(SUM(SplitAmt),0),2) v FROM paysplit WHERE UnearnedType<>0;

-- name: income.insurance_by_year
SELECT YEAR(DateCP) k, ROUND(SUM(InsPayAmt),2) v FROM claimproc WHERE Status IN (1,4) AND DateCP<=@AsOf GROUP BY YEAR(DateCP) ORDER BY 1;

-- name: income.insurance
SELECT '' k, ROUND(IFNULL(SUM(InsPayAmt),0),2) v FROM claimproc WHERE Status IN (1,4) AND DateCP<=@AsOf;

-- name: income.insurance_checks
SELECT '' k, ROUND(IFNULL(SUM(CheckAmt),0),2) v FROM claimpayment;

-- name: income.deposits
SELECT '' k, ROUND(IFNULL(SUM(Amount),0),2) v FROM deposit;

-- name: aging.stored
SELECT 'bal_0_30' k, ROUND(SUM(Bal_0_30),2) v FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'bal_31_60', ROUND(SUM(Bal_31_60),2) FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'bal_61_90', ROUND(SUM(Bal_61_90),2) FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'bal_over_90', ROUND(SUM(BalOver90),2) FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'bal_total', ROUND(SUM(BalTotal),2) FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'ins_est', ROUND(SUM(InsEst),2) FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'payplan_due', ROUND(SUM(PayPlanDue),2) FROM patient WHERE PatNum=Guarantor
UNION ALL SELECT 'est_balance', ROUND(SUM(EstBalance),2) FROM patient;

-- name: aging.families_with_balance
SELECT '' k, COUNT(*) v FROM patient WHERE PatNum=Guarantor AND BalTotal>0.005;

-- name: aging.families_with_credit
SELECT '' k, COUNT(*) v FROM patient WHERE PatNum=Guarantor AND BalTotal<-0.005;

-- name: aging.ledger_total
SELECT '' k, ROUND(
	(SELECT IFNULL(SUM(ProcFee*(UnitQty+BaseUnits)),0) FROM procedurelog WHERE ProcStatus=2 AND ProcDate<=@AsOf)
	+(SELECT IFNULL(SUM(AdjAmt),0) FROM adjustment WHERE AdjDate<=@AsOf)
	+(SELECT IFNULL(SUM(Principal+Interest),0) FROM payplancharge WHERE ChargeType=0 AND ChargeDate<=@AsOf)
	-(SELECT IFNULL(SUM(SplitAmt),0) FROM paysplit WHERE DatePay<=@AsOf)
	-(SELECT IFNULL(SUM(InsPayAmt),0) FROM claimproc WHERE Status IN (1,4) AND DateCP<=@AsOf)
	-(SELECT IFNULL(SUM(WriteOff),0) FROM claimproc WHERE Status IN (0,1,4) AND ClaimNum>0)
	-(SELECT IFNULL(SUM(Principal),0) FROM payplancharge WHERE ChargeType=1 AND ChargeDate<=@AsOf)
	-(SELECT IFNULL(SUM(l.AmountOverride),0) FROM payplanlink l INNER JOIN payplan pp ON pp.PayPlanNum=l.PayPlanNum
		WHERE pp.IsDynamic=1 AND pp.PayPlanDate<=@AsOf),2) v;

-- name: insurance.claims_by_status
SELECT CONCAT(ClaimType,'/',ClaimStatus) k, COUNT(*) v FROM claim GROUP BY ClaimType,ClaimStatus ORDER BY 1;

-- name: insurance.claim_fees_by_status
SELECT CONCAT(ClaimType,'/',ClaimStatus) k, ROUND(SUM(ClaimFee),2) v FROM claim GROUP BY ClaimType,ClaimStatus ORDER BY 1;

-- name: insurance.outstanding_estimate
SELECT '' k, ROUND(IFNULL(SUM(InsPayEst),0),2) v FROM claimproc WHERE Status=0 AND ClaimNum>0;

-- name: insurance.treatment_plan_estimates
SELECT '' k, ROUND(IFNULL(SUM(InsEstTotal),0),2) v FROM claimproc WHERE Status=6;

-- name: insurance.preauth_estimates
SELECT '' k, ROUND(IFNULL(SUM(InsPayEst),0),2) v FROM claimproc WHERE Status=2;

-- name: insurance.claimprocs_by_status
SELECT Status k, COUNT(*) v FROM claimproc GROUP BY Status ORDER BY Status;

-- name: payplans.by_kind
SELECT CONCAT(IF(IsDynamic=1,'dynamic','static'),'/',IF(IsClosed=1,'closed','open')) k, COUNT(*) v FROM payplan GROUP BY IsDynamic,IsClosed ORDER BY 1;

-- name: payplans.principal_debited
SELECT '' k, ROUND(IFNULL(SUM(Principal),0),2) v FROM payplancharge WHERE ChargeType=0;

-- name: payplans.principal_due
SELECT '' k, ROUND(IFNULL(SUM(Principal),0),2) v FROM payplancharge WHERE ChargeType=0 AND ChargeDate<=@AsOf;

-- name: payplans.interest_due
SELECT '' k, ROUND(IFNULL(SUM(Interest),0),2) v FROM payplancharge WHERE ChargeType=0 AND ChargeDate<=@AsOf;

-- name: payplans.credits
SELECT '' k, ROUND(IFNULL(SUM(Principal),0),2) v FROM payplancharge WHERE ChargeType=1;

-- name: payplans.linked_production
SELECT '' k, ROUND(IFNULL(SUM(AmountOverride),0),2) v FROM payplanlink;

-- name: payplans.paid
SELECT '' k, ROUND(IFNULL(SUM(SplitAmt),0),2) v FROM paysplit WHERE PayPlanNum<>0;

-- name: payplans.principal_paid
SELECT '' k, ROUND(IFNULL(SUM(SplitAmt),0),2) v FROM paysplit WHERE PayPlanNum<>0 AND PayPlanDebitType=1;

-- name: payplans.balance
SELECT '' k, ROUND((SELECT IFNULL(SUM(Principal),0) FROM payplancharge WHERE ChargeType=0)
	-(SELECT IFNULL(SUM(SplitAmt),0) FROM paysplit WHERE PayPlanNum<>0 AND PayPlanDebitType=1),2) v;

-- name: payplans.down_payments
SELECT '' k, ROUND(IFNULL(SUM(Principal),0),2) v FROM payplancharge WHERE IsDownPayment=1;

-- name: appointments.by_status
SELECT AptStatus k, COUNT(*) v FROM appointment GROUP BY AptStatus ORDER BY AptStatus;

-- name: appointments.completed_by_year
SELECT YEAR(AptDateTime) k, COUNT(*) v FROM appointment WHERE AptStatus=2 GROUP BY YEAR(AptDateTime) ORDER BY 1;

-- name: appointments.future_scheduled
SELECT '' k, COUNT(*) v FROM appointment WHERE AptStatus=1 AND AptDateTime>@AsOf;

-- name: appointments.mirrored
SELECT '' k, COUNT(*) v FROM appointment WHERE IsMirrored=1;

-- name: fees.rows_dated
SELECT '' k, COUNT(*) v FROM fee WHERE DateEffective>'0001-01-01';

-- name: fees.rows_future_dated
SELECT '' k, COUNT(*) v FROM fee WHERE DateEffective>@AsOf;

-- name: fees.asof_total_by_sched
SELECT f.FeeSched k, ROUND(SUM(f.Amount),2) v FROM fee f
INNER JOIN (SELECT FeeSched,CodeNum,ClinicNum,ProvNum,MAX(DateEffective) DateEffective FROM fee WHERE DateEffective<=@AsOf
	GROUP BY FeeSched,CodeNum,ClinicNum,ProvNum) m ON m.FeeSched=f.FeeSched AND m.CodeNum=f.CodeNum AND m.ClinicNum=f.ClinicNum
	AND m.ProvNum=f.ProvNum AND m.DateEffective=f.DateEffective
WHERE f.ClinicNum=0 AND f.ProvNum=0
GROUP BY f.FeeSched ORDER BY f.FeeSched;

-- name: benefits.with_tooth_range
SELECT '' k, COUNT(*) v FROM benefit WHERE ToothRange IS NOT NULL AND ToothRange<>'';

-- name: benefits.with_code_group
SELECT '' k, COUNT(*) v FROM benefit WHERE CodeGroupNum<>0;

-- name: accounting.totals
SELECT 'debits' k, ROUND(IFNULL(SUM(DebitAmt),0),2) v FROM journalentry
UNION ALL SELECT 'credits', ROUND(IFNULL(SUM(CreditAmt),0),2) FROM journalentry
UNION ALL SELECT 'needs_review', COUNT(*) FROM journalentry WHERE NeedsReview=1;

-- name: accounting.net_by_account_type
SELECT a.AcctType k, ROUND(SUM(j.DebitAmt-j.CreditAmt),2) v FROM journalentry j INNER JOIN account a ON a.AccountNum=j.AccountNum
GROUP BY a.AcctType ORDER BY a.AcctType;

-- name: statements.by_mode
SELECT Mode_ k, COUNT(*) v FROM statement GROUP BY Mode_ ORDER BY Mode_;

-- name: statements.show_trans_since_zero
SELECT '' k, COUNT(*) v FROM statement WHERE ShowTransSinceBalZero=1;

-- name: statements.invoice_adjustment_total
SELECT '' k, ROUND(IFNULL(SUM(InsEst),0),2) v FROM statement WHERE IsInvoice=1;

-- name: features.chart_letters
SELECT '' k, COUNT(*) v FROM document WHERE ChartLetterStatus<>0;

-- name: features.eforms_by_status
SELECT Status k, COUNT(*) v FROM eform GROUP BY Status ORDER BY Status;

-- name: features.inspending
SELECT '' k, COUNT(*) v FROM inspending;

-- name: features.tasks_by_category
SELECT Category k, COUNT(*) v FROM task GROUP BY Category ORDER BY Category;

-- name: features.hidden_medications_on_patients
SELECT '' k, COUNT(*) v FROM medicationpat mp INNER JOIN medication m ON m.MedicationNum=mp.MedicationNum WHERE m.IsHidden=1;

-- name: features.guardians_by_relationship
SELECT Relationship k, COUNT(*) v FROM guardian GROUP BY Relationship ORDER BY Relationship;

-- name: features.chat_messages
SELECT '' k, COUNT(*) v FROM chatmsg;

-- name: features.chat_sequence
SELECT CounterName k, CounterVal v FROM sequencecounter ORDER BY CounterName;

-- name: check.aging_buckets_sum
SELECT '' k, COUNT(*) v FROM patient WHERE PatNum=Guarantor AND ABS(Bal_0_30+Bal_31_60+Bal_61_90+BalOver90-BalTotal)>0.011;

-- name: check.ledger_matches_baltotal
SELECT '' k, COUNT(*) v FROM (
	SELECT g.PatNum,g.BalTotal,IFNULL(SUM(t.amt),0) ledger FROM patient g
	LEFT JOIN (
		SELECT p.Guarantor guar,pl.ProcFee*(pl.UnitQty+pl.BaseUnits) amt FROM procedurelog pl INNER JOIN patient p ON p.PatNum=pl.PatNum
			WHERE pl.ProcStatus=2 AND pl.ProcDate<=@AsOf
		UNION ALL SELECT p.Guarantor,a.AdjAmt FROM adjustment a INNER JOIN patient p ON p.PatNum=a.PatNum WHERE a.AdjDate<=@AsOf
		UNION ALL SELECT p.Guarantor,c.Principal+c.Interest FROM payplancharge c INNER JOIN patient p ON p.PatNum=c.PatNum
			WHERE c.ChargeType=0 AND c.ChargeDate<=@AsOf
		UNION ALL SELECT p.Guarantor,-ps.SplitAmt FROM paysplit ps INNER JOIN patient p ON p.PatNum=ps.PatNum WHERE ps.DatePay<=@AsOf
		UNION ALL SELECT p.Guarantor,-cp.InsPayAmt FROM claimproc cp INNER JOIN patient p ON p.PatNum=cp.PatNum
			WHERE cp.Status IN (1,4) AND cp.DateCP<=@AsOf
		UNION ALL SELECT p.Guarantor,-cp.WriteOff FROM claimproc cp INNER JOIN patient p ON p.PatNum=cp.PatNum
			WHERE cp.Status IN (0,1,4) AND cp.ClaimNum>0
		UNION ALL SELECT p.Guarantor,-c.Principal FROM payplancharge c INNER JOIN patient p ON p.PatNum=c.PatNum
			WHERE c.ChargeType=1 AND c.ChargeDate<=@AsOf
		UNION ALL SELECT p.Guarantor,-l.AmountOverride FROM payplanlink l INNER JOIN payplan pp ON pp.PayPlanNum=l.PayPlanNum
			INNER JOIN patient p ON p.PatNum=pp.PatNum WHERE pp.IsDynamic=1 AND pp.PayPlanDate<=@AsOf
	) t ON t.guar=g.PatNum
	WHERE g.PatNum=g.Guarantor
	GROUP BY g.PatNum,g.BalTotal
	HAVING ABS(ledger-g.BalTotal)>0.011
) x;

-- name: check.insest_matches_claims
SELECT '' k, COUNT(*) v FROM (
	SELECT g.PatNum FROM patient g
	LEFT JOIN (SELECT p.Guarantor guar,cp.InsPayEst est FROM claimproc cp INNER JOIN patient p ON p.PatNum=cp.PatNum
		WHERE cp.Status=0 AND cp.ClaimNum>0) t ON t.guar=g.PatNum
	WHERE g.PatNum=g.Guarantor
	GROUP BY g.PatNum,g.InsEst
	HAVING ABS(IFNULL(SUM(t.est),0)-g.InsEst)>0.011
) x;

-- name: check.payment_matches_splits
SELECT '' k, COUNT(*) v FROM (
	SELECT pay.PayNum FROM payment pay LEFT JOIN paysplit ps ON ps.PayNum=pay.PayNum
	GROUP BY pay.PayNum,pay.PayAmt HAVING ABS(pay.PayAmt-IFNULL(SUM(ps.SplitAmt),0))>0.005
) x;

-- name: check.insurance_check_matches_claimprocs
SELECT '' k, COUNT(*) v FROM (
	SELECT cpay.ClaimPaymentNum FROM claimpayment cpay LEFT JOIN claimproc cp ON cp.ClaimPaymentNum=cpay.ClaimPaymentNum
	GROUP BY cpay.ClaimPaymentNum,cpay.CheckAmt HAVING ABS(cpay.CheckAmt-IFNULL(SUM(cp.InsPayAmt),0))>0.005
) x;

-- name: check.deposit_matches_items
SELECT '' k, COUNT(*) v FROM deposit d
WHERE ABS(d.Amount-(SELECT IFNULL(SUM(PayAmt),0) FROM payment WHERE DepositNum=d.DepositNum)
	-(SELECT IFNULL(SUM(CheckAmt),0) FROM claimpayment WHERE DepositNum=d.DepositNum))>0.005;

-- name: check.journal_balanced
SELECT '' k, COUNT(*) v FROM (
	SELECT TransactionNum FROM journalentry GROUP BY TransactionNum HAVING ABS(SUM(DebitAmt)-SUM(CreditAmt))>0.005
) x;

-- name: check.claim_fee_matches_claimprocs
SELECT '' k, COUNT(*) v FROM (
	SELECT c.ClaimNum FROM claim c LEFT JOIN claimproc cp ON cp.ClaimNum=c.ClaimNum AND cp.Status IN (0,1,2)
	GROUP BY c.ClaimNum,c.ClaimFee HAVING ABS(c.ClaimFee-IFNULL(SUM(cp.FeeBilled),0))>0.005
) x;

-- name: check.duplicate_active_email
SELECT '' k, COUNT(*) v FROM (
	SELECT Email FROM patient WHERE PatStatus=0 AND Email<>'' GROUP BY Email HAVING COUNT(*)>1
) x;

-- name: check.orphan_claimprocs
SELECT '' k, COUNT(*) v FROM claimproc cp WHERE cp.ProcNum<>0 AND NOT EXISTS(SELECT 1 FROM procedurelog pl WHERE pl.ProcNum=cp.ProcNum);

-- name: check.orphan_procedure_patients
SELECT '' k, COUNT(*) v FROM procedurelog pl WHERE NOT EXISTS(SELECT 1 FROM patient p WHERE p.PatNum=pl.PatNum);

-- name: check.orphan_split_procedures
SELECT '' k, COUNT(*) v FROM paysplit ps WHERE ps.ProcNum<>0 AND NOT EXISTS(SELECT 1 FROM procedurelog pl WHERE pl.ProcNum=ps.ProcNum);

-- name: check.benefit_tooth_range_format
-- 25.3 stores a tooth range only on Exclusions rows (BenefitType 4), as comma-separated tooth numbers or letters with ranges expanded.
SELECT '' k, COUNT(*) v FROM benefit WHERE ToothRange IS NOT NULL AND ToothRange<>'' AND (BenefitType<>4
	OR ToothRange NOT REGEXP '(?-i)^([1-9]|[12][0-9]|3[0-2]|5[1-9]|[67][0-9]|8[0-2]|[A-T]S?)(,([1-9]|[12][0-9]|3[0-2]|5[1-9]|[67][0-9]|8[0-2]|[A-T]S?))*$');

-- name: check.eform_pick_lists
-- Radio buttons: pipe-delimited PickListVis and PickListDb with the same number of items, at least two. Ins Pick: three visible options.
SELECT '' k, COUNT(*) v FROM (
	SELECT FieldType,PickListVis,PickListDb FROM eformfield
	UNION ALL SELECT FieldType,PickListVis,PickListDb FROM eformfielddef
) f
WHERE (f.FieldType=4 AND (f.PickListDb NOT LIKE '%|%'
		OR LENGTH(f.PickListVis)-LENGTH(REPLACE(f.PickListVis,'|',''))<>LENGTH(f.PickListDb)-LENGTH(REPLACE(f.PickListDb,'|',''))))
	OR (f.FieldType=9 AND LENGTH(f.PickListVis)-LENGTH(REPLACE(f.PickListVis,'|',''))<>2);

-- name: check.eform_value_formats
-- MedicationList (7): ValueLabel a JSON layout, ValueString a JSON list. Patient List (8): ValueLabel a JSON layout, ValueString a PatNum
-- or blank. Ins Pick (9): ValueString blank, New, InsSubNum:n or InsPendingNum:n, and the number names an existing row.
SELECT '' k, COUNT(*) v FROM eformfield f WHERE
	(f.FieldType=7 AND (f.ValueLabel NOT LIKE '{%}' OR f.ValueString NOT LIKE '[%]'))
	OR (f.FieldType=8 AND (f.ValueLabel NOT LIKE '{%}' OR f.ValueString NOT REGEXP '^[0-9]*$'
		OR (f.ValueString<>'' AND NOT EXISTS(SELECT 1 FROM patient p WHERE p.PatNum=f.ValueString))))
	OR (f.FieldType=9 AND (f.ValueString NOT REGEXP '(?-i)^(|New|InsSubNum:[0-9]+|InsPendingNum:[0-9]+)$'
		OR (f.ValueString LIKE 'InsSubNum:%' AND NOT EXISTS(SELECT 1 FROM inssub s WHERE s.InsSubNum=SUBSTRING(f.ValueString,11)))
		OR (f.ValueString LIKE 'InsPendingNum:%' AND NOT EXISTS(SELECT 1 FROM inspending i WHERE i.InsPendingNum=SUBSTRING(f.ValueString,15)))));
