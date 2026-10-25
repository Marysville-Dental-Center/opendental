# B-PAY: Payment plans and paysplits (remaining)

Branch `port/b-pay`, cut from `m1/integration-a2-patch`. Built in the single-wave run, so packages built at the same time are not on this base.
Backlog section: [port-backlog.md, B-PAY](../specs/port-backlog.md). Tests: `tests/BPAYTests` (MSTest v1, x86, .NET 4.8; scratch database `forkmig_<run>_bpay`, dropped afterwards).

No schema change and no fork migration step (the level range 2027000301-2027000399 is unused).

## Status per feature

| Feature ID | Status | Notes |
|---|---|---|
| `v25_1-account-payplan-charge-logic-deprecated` | built | Combo hidden in Preferences > Account > Payments and on the Enterprise Account page; legacy value shown read-only. |
| `v25_1-account-payplan-completedamt-audit` | built | Manual edits, automatic recalculations (procedure status, procedure credit removed, dynamic plan save, Close Payment Plan tool). |
| `v25_2-account-payplan-downpayment-audit` | built | Terms change or down payment charge edit/removal, one entry per save. |
| `v25_3-account-payplan-bypass-globallock` | built | Preference name is the spec's proposal, kept outside `PrefName` (see owner question 1). |
| `v25_2-account-payplan-prepay-attach` | built | Paysplit window, plan delete, DBM review, income transfers. |
| `v25_3-account-paysplit-payplan-attach` | built (cases INFERRED) | The 25.3 cases are not public; what we allow is listed below. |

## What was built

### Pay Plan charge logic retired (25.1)
- `PayPlanChargeLogic` (new, `OpenDentBusiness/Db Multi Table/PayPlanChargeLogic.cs`): `IsLegacyMode` (anything except Age Credits and Debits, unknown values included) and `GetReadOnlyText` ("Pay Plan charge logic (legacy, no longer selectable): <mode>").
- `UserControlAccountPayments` and `UserControlEnterpriseAccount`: `comboPayPlansVersion` is hidden (designer and `FillSynced`). `label27` is widened and shows the read-only line only when the database holds a legacy value. Hide Due Now still shows only for Age Credits and Debits, as before.
- `PayPlansVersion` is still read by every reader and still synced by `FormPreferences` with its unchanged value. Nothing converts it (spec rule: changing the mode rewrites balances and aging).
- Deviation: the spec suggests "a link to this spec" next to the read-only line. Product UI does not link to developer docs, so there is no link. `PrefInfos.xml` is unchanged; its help entries for `label27` still describe the modes.

### Tx Completed Amount audit (25.1)
- `PayPlanAudits` (new): text such as `Tx Completed Amt changed from 1,200.00 to 1,350.00 (+150.00), after procedure X was set complete.` Amounts are compared rounded to cents, and nothing is logged when they are equal. Entries are PayPlanEdit, with the plan's PatNum and the PayPlanNum as FKey. With no logged-on user (a service), the entry is written with user 0 instead of throwing.
- Manual edits (old-style and insurance plans): `PayPlanL.MakeSecLogEntries` replaces the old "completed amount changed." entry with the detailed one. Dynamic plans are skipped there, because they are logged where the amount is recalculated.
- Automatic recalculations:
  - `PayPlans.UpdateTreatmentCompletedAmt` and `UpdateTreatmentCompletedAmtsDynamicPaymentPlan` take an optional `auditTrigger` and log each plan whose amount changed, one entry per plan.
  - `PayPlanCharges.UpdateAttachedPayPlanCharges` passes "after procedure <code> was set complete" or "changed status". `Procedures.Update` calls it, so completing a procedure on a plan is logged. `PayPlanCharges.DeleteForProc` passes "after the credit for procedure #N was removed".
- Dynamic plans: `PayPlanEdit.SaveDynamicPaymentPlanAndTerms` compares the new amount with the database value, so form saves and close-outs are logged once. New plans are logged as "created" only.
- The Close Payment Plan tool (`PayPlans`, insurance plans whose amount is set to the insurance paid) logs "by the Close Payment Plan tool".
- `PayPlanL.AdvanceAuditBaseline`: after a logged save, the logged amounts become the new baseline, so Print or Sign and Print followed by OK does not log the same change twice (`FormPayPlan`, `FormPayPlanDynamic`).

### Down payment audit (25.2)
- `PayPlanAudits.GetDownPaymentLogText`: `Down Payment Changed from 200.00 to 150.00, PayPlanNum 12.`, with "(removed)" when the new amount is 0. A change in the plan terms wins. Otherwise a change of the down payment charges (marked by `payplancharge.IsDownPayment`, A-PAY) is logged with "(down payment charges)". So a new schedule built for a new down payment gives one entry.
- `FormPayPlan` keeps the sum of the down payment charges as of load or the last logged save, and passes it to `MakeSecLogEntries` (new optional parameters `downPaymentChargesOld`/`downPaymentChargesNew`).
- Dynamic plans: our code allows a down payment only on a new plan, and down payment charges cannot be edited or deleted. So no change can happen after creation, and none is logged.
- INFERRED: a new plan's down payment is part of "Payment Plan created." and gets no separate entry. A print-only save logs nothing, as for the other payment plan entries.

### Payment plans ignore the global lock (25.3)
- `PayPlanGlobalLock` (new):
  - `IsOn`, `Save` (inserts the row the first time it is turned on), `IsExcludedFromGlobalLock(perm)` (PayPlanEdit only), `GetChangeLogLine`.
  - The value lives in a preference row that is not in `PrefName`, the same pattern as `DxcPartnerIdentity`. The name is the spec's proposal, `SecurityLockExcludesPayPlans`, which `RegistryTests.PrefNameTests` keeps out of `PrefName` until it is confirmed.
  - A missing row reads as off, the default, so an adopted database needs no migration.
- `Security.IsGlobalDateLock` returns false for PayPlanEdit when the setting is on. Dated `Security.IsAuthorized(PayPlanEdit, date)` calls and every listed direct caller follow automatically:
  - `FormPayPlan` (4 calls)
  - `FormPayPlanDynamic` (1)
  - `FormPayPlanCredits` (2)
  - `PayPlanEdit.ValidateDynamicPaymentPlanTerms` (1)
- Payments, adjustments, procedures and the other global-lock permissions stay locked.
- `FormSecurityLock`: new checkbox "Payment plans are not affected by the global lock" under "Lock includes administrators". Its change goes into the existing single SecurityGlobal entry as one more "changed from 'X' to 'Y'" line.
- Note: in our code PayPlanEdit takes no user-group date limit (`GroupPermissions.PermTakesDates`), so "user-group lock dates still apply" has nothing to apply to for PayPlanEdit. The plan's Locked box and Pay Plan Unlock are untouched.
- Not built: the help254 search excerpt says that under a global lock the Locked box is unavailable even with Pay Plan Unlock. That page is not public, so the behavior is not added. In practice, saving an unlocked plan dated inside the lock is still refused by the date check in `ValidateDynamicPaymentPlanTerms` unless the setting is on.

### Payment Plan Prepay splits on patient payment plans (25.2)
- `PaySplitPayPlanAttach` (new) holds the rules; `FormPaySplitEdit` uses them.
- The unearned-type combo also lists the Payment Plan Prepay type (preference `DynamicPayPlanPrepaymentUnearnedType`) when its definition is hidden (`GetUnearnedDefsForSplitEdit`).
- Choosing that type keeps "Attached to Payment Plan" enabled and does not detach the plan. The provider stays None unless AllowPrepayProvider is on, as before. Any other unearned type keeps 24.3 behavior and detaches.
- A prepayment is offered only patient payment plans. INFERRED: these are dynamic plans, which Open Dental calls Patient Payment Plans and whose existing logic applies hidden prepayments to future charges. Old-style plans and insurance plans are never offered.
  - Switching an attached split to the prepay type detaches it from an old-style plan.
  - A prepayment pays no charge (`PayPlanChargeNum` 0).
  - A prepayment cannot have a procedure or an adjustment (checked when attaching and on OK).
- Attaching to a closed plan asks for confirmation; locked plans are allowed. INFERRED: the closed-plan confirmation applies to every attach from the Paysplit window, not only to prepayments.
- `PayPlans.Delete`: plan prepayments alone no longer block deleting a patient plan. They are detached (PayPlanNum and PayPlanChargeNum set to 0; neither is in the row hash) and stay unearned of the same type. One PayPlanEdit entry gives their count and total. Any other attached split still blocks the delete, as before.
- Income transfers already skip hidden unearned splits attached to plans (`PaymentEdit`), and a test confirms this.
- DBM review: none of the checks that look at unearned or payment plan splits reports or "fixes" an attached prepayment, so `DatabaseMaintenances.cs` is unchanged. The checks reviewed are:
  - `PaySplitWithInvalidPayPlanNum`
  - `PaySplitWithInvalidPayPlanChargeNum`
  - `PaySplitAttachedToInsurancePaymentPlan`
  - `PaySplitTransfersWithNoUnearnedType`
  - `PaySplitUnearnedAttachedToCompletedProcedures`
  - the `GetCommandForProcedurelogNotCompletedAttached` query (it already requires `UnearnedType=0 AND PayPlanNum=0`)
- `FormPayment` is unchanged. Its overpaid interest/principal check (`CheckDynamicPaymentPlanRebalance` → `PayPlanEdit.AreAnyPayPlansOverpaid`) still runs for attached splits and ignores unearned splits.

### More paysplit attach cases (25.3, INFERRED)
Public material names no cases. `PaySplitPayPlanAttach.GetOptions` implements this matrix (spec test matrix → our result):

| Case | 24.3 | Ours |
|---|---|---|
| Family member who is the plan's patient but not its guarantor | allowed | allowed (unchanged) |
| Split from another family, plan's guarantor in the paying family | refused silently | allowed, only plans that the paying family guarantees |
| Payment Plan Prepay type, no procedure | detached | allowed (dynamic plans, above) |
| Split on an adjustment that is a plan credit | plan detached when the adjustment is attached | allowed: plans with the adjustment as production are offered even if the split's patient is not the plan's patient or guarantor; attaching the adjustment keeps such a plan |
| Split on a procedure that is a credit on a dynamic plan (or on an old-style plan's credits) | plan detached when the procedure is attached | allowed, same as the adjustment case |

Kept: insurance payment plans are never offered (rule 4), and treatment planned unearned (an unearned type on a TP procedure) is refused (rule 2).

Other changes:
- Charge choice when attaching (`GetPayPlanChargeNum`): the oldest due debit, preferring one for the split's procedure or adjustment. Fix: 24.3 took the oldest due charge of any type, which could be a credit. Credits are never chosen now.
- Cancel in the Paysplit window now also restores `PayPlanChargeNum`.
- Audit: a new split's PaymentEdit entry adds ", payment plan #N". An edited split's entry adds a "payment plan number changed from 'X' to 'Y'" line.
- The "no plans" message now reads "is not the patient or guarantor of any payment plans". The 24.3 text said "guarantor" although the query also matched the patient.

## Files touched
- New:
  - `OpenDentBusiness/Db Multi Table/PayPlanAudits.cs`, `PayPlanChargeLogic.cs`, `PayPlanGlobalLock.cs`, `PaySplitPayPlanAttach.cs`, with 4 lines in `OpenDentBusiness.csproj` next to `PayPlanEdit.cs`
  - `tests/BPAYTests/*`
  - this note
- Changed:
  - `OpenDentBusiness/Data Interface/PayPlans.cs`, `PayPlanCharges.cs`
  - `OpenDentBusiness/Db Multi Table/PayPlanEdit.cs`, `Security.cs` (3 lines)
  - `OpenDental/Logic/PayPlanL.cs`
  - `OpenDental/Forms/FormPayPlan.cs`, `FormPayPlanDynamic.cs` (1 line), `FormPaySplitEdit.cs`, `FormSecurityLock.cs` + designer
  - `OpenDental/User Controls/Preferences/UserControlAccountPayments.cs` + designer
  - `UserControlEnterpriseAccount.cs` + designer (not in B-PAY's Owns list and owned by no package; the spec names the Enterprise Account page)
  - `OpenDental.sln`, `OpenDental.Local.slnf`
- Not changed although owned: `DatabaseMaintenances.cs` (review only), `Payments.cs`, `PaySplits.cs`, `PaymentEdit.cs`, `FormPayment.cs`, `FormPayPlanChargeEdit.cs`, `FormPayPlanCredits.cs`, `FormPayPlanSelect.cs`, `PrefInfos.xml`.

## Interplay with packages built at the same time
- M1-02/M2-02 (registries): when the real preference name is confirmed, add it to `PrefName` (bool, default off) with a PrefInfos entry, remove it from `RegistryTests.PrefNameTests.ArrayUnconfirmedProposedNames`, and change `PayPlanGlobalLock.PREF_NAME` (or make `IsOn`/`Save` use the enum). If the real name differs, an office that tried the setting before then has an orphan `SecurityLockExcludesPayPlans` row. It is harmless, and the integration can delete it.
- B-DBM (B2) takes `DatabaseMaintenances.cs` over: B-PAY left it unchanged. A future check that flags splits with both a procedure and the prepay type would fit the 25.2 rule.
- M2-PAYW (C1) and V-GP (C2) take `FormPayment.cs` over: B-PAY left it unchanged.
- B-ACCT (owns `Adjustments`): our base never recalculates a dynamic plan's completed amount when an attached adjustment is edited. If that is added, call `PayPlans.UpdateTreatmentCompletedAmtsDynamicPaymentPlan(listPayPlans,auditTrigger)` to get the 25.1 entry.
- Any package that adds callers of `PayPlans.UpdateTreatmentCompletedAmt*` gets the audit automatically. Passing a trigger is optional.

## Questions for the owner
1. What is Open Dental's real preference name for "payment plans are not affected by the global lock"? Check on the office database with `SELECT PrefName,ValueString FROM preference WHERE PrefName LIKE '%Lock%' OR PrefName LIKE '%PayPlan%'`. Until it is confirmed, the setting is stored under the proposed name, and an office that had it on in Open Dental must tick it again after adoption.
2. Which `PayPlansVersion` value does the office database hold? If Open Dental's 25.1 conversion forced Age Credits and Debits (value 2), should our 24.3 upgrade path do the same? Today nothing converts it.
3. Please check the inferred 25.3 attach cases (table above) on 25.3.48: another-family splits, production-linked plans, and the closed-plan confirmation for all attaches.
4. Should a Payment Plan Prepay split also attach to old-style (non-dynamic) patient plans? We allow dynamic plans only, because only those apply prepayments.

## Untested
- The windows themselves were not opened (no GUI launch): the Global Lock Date checkbox, the Paysplit window flows (combo, checkbox, attach procedure or adjustment, closed-plan prompt) and the Preferences pages. The rules they call are tested in `tests/BPAYTests`. The designer and caller wiring is covered only by source checks: the hidden combo, and the global-lock call-site inventory.
- Middle tier (`ClientMT`) dispatch of the new optional `auditTrigger` parameters and of `PayPlanGlobalLock.Upsert` / `PaySplitPayPlanAttach.DetachPrepaymentsFromPayPlan` was not exercised.
