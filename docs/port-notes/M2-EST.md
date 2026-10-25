# Port note: M2-EST, insurance estimate engine (25.4 and 26.1)

Branch `port/m2-est`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run beside the other C1 packages. Backlog section: [port-backlog.md, M2-EST](../specs/port-backlog.md#m2-est-insurance-estimate-engine). Assigned fork migration range: 2027002701 to 2027002799 (see "The fork migration level" below: the step is written but not active).

Sources: the public 25.4 and 26.1 release notes and the feature specs in `docs/specs/` (features-25.3/25.4/26.1.md), and the published 25.4/26.1 database documents only for `procedurelog.NoBillIns` and `benefit.ToothRange` (both already in the schema from M1-01/M2-01). No Open Dental program, install or database was used. Every behavior the public sources do not settle is marked INFERRED below.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-tp-estimates-appt-date` | built | Treatment Plan module estimates of procedures on a scheduled appointment are figured as of the appointment date (today for a past appointment): benefit year of the annual max, deductible and family max, frequency limits, waiting periods, age limits (age on that date), and no estimate when the subscriber's coverage ended before it. Both paths (frequency checking on, with the TP date picker; off). Optional "Est. Date" grid column not built; a description line "Estimated as of (date)" is shown instead (fork UI, see below). |
| `v25_4-chart-nobillins-audit` | built (migration written, not active) | `procedurelog.NoBillIns` is set only by the user's click in Procedure Info, saved at once with the claimprocs, and audited; estimates created or rebuilt inherit it; billing a claimproc again in Edit Claim Procedure clears it (audited). The 24.3/25.3 back-fill (rule 4) is written and tested but the step class is abstract until its level is settled. |
| `v25_4-family-benefit-tooth-exclusions` | built | Storage, parsing, matching, the Edit Benefit Teeth box and the in-place Teeth column of the Other Benefits grid were already built in M1 (the 25.3 backport `v25_3-family-exclusions-tooth`, verified here and covered by `InsuranceTests`). This package adds the Teeth column of the Age Limits grid (with the 26.1 meaning, below), the red cell and the Save block for it, and tests. |
| `v26_1-family-benefit-agelimit-teeth` | built | Age limitations (code group, category and procedure code) can be limited to teeth: Teeth column in the Age Limits grid, Teeth cell of the Other Benefits grid and Teeth box of Edit Benefit enabled for age limits as well as exclusions; estimates apply a toothed row only to its teeth; a toothed and a toothless row of the same scope split the teeth (INFERRED precedence from the spec). |

## What was built

### Estimates as of the scheduled appointment (25.4)

- `OpenDentBusiness/Db Multi Table/TreatmentPlanModules.EstimateDates.cs` (new): `TpEstimateDates`, the per-procedure as-of date and its helpers, and `TreatmentPlanModules.GetDateForHistList`.
  - As-of date (rule 1): `AptNum` appointment with status Scheduled (or the deprecated ASAP) → its date, or today when the appointment is in the past. Planned appointments (`PlannedAptNum`), the unscheduled list, broken and other statuses do not count. Every other procedure keeps the old date: the TP date picker's when frequency checking is on (the picker is only shown then), today otherwise.
  - Age: on the as-of date for scheduled procedures; unscheduled procedures keep the age today (the spec says they "keep today's behavior").
- `TreatmentPlanModules.cs` (owned): `GetLoadActiveTpData` reads the history from the earliest of the TP date and the scheduled as-of dates (the history query has no upper bound, so later dates are covered). `GetActiveTpPlanTpRows`:
  - Frequency checking on: procedures are dated as of their estimate, exactly where 24.3 dated them with the picker; loop entries carry those dates.
  - Frequency checking off: procedure dates are never changed. A scheduled procedure is estimated through a dated copy with `useProcDateOnProc`; afterwards its sales tax is copied back and its unsent claimprocs get their own date back (`TpEstimateDates.FinishEstimate`), so the stored `claimproc.ProcDate` is what 24.3 stored.
  - Loop entries are marked `ClaimProcHist.IsDateAsOf` (new field, `ClaimProcs.cs`).
  - Description line "Estimated as of (date)" when "Show Max/Deductions" is on and a scheduled procedure is estimated as of another date than the plan (fork UI; the release note shows no UI).
  - The grid's Allowed column treats a procedure flagged Do Not Bill to Ins like a no-bill code (25.4 flag).
- `Benefits.cs` (owned) and `Benefits.EstimateTeeth.cs` (new partial): `IsLoopHistInPeriod`, `GetBenefitPeriod`. In `GetLimitationByCode` (individual and family max) and `GetDeductibleByCode` (individual, the general-deductible sum and family), a marked loop entry counts only toward the benefit period that contains its date (calendar year or service year from `insplan.MonthRenew`); lifetime benefits count every entry. Unmarked entries (claims, appointment and chart estimates, every other caller) count whatever their date, as before.
- `Procedures.EstimateAsOf.cs` (new partial): `ScopeCoverageEndCheck` (thread-scoped, like `Fees.ScopeDateAsOf`), `IsCoverageEnded`, `ZeroEstimateForCoverageEnded`; one hook line in `Procedures.ComputeForOrdinal` (`Procedures.cs`). Inside the scope (the TP module, around scheduled procedures only), an unsent estimate whose subscriber's `inssub.DateTerm` is before the procedure's date is zeroed before the next plan's paid-by-other-insurance is summed, with the note "Coverage ended (date)". Outside the scope `DateTerm` is still ignored by estimates.
- Saved treatment plans are not recalculated (`GetArchivedTpRows` is unchanged).

### Do Not Bill to Ins on the procedure (25.4)

- `Procedures.NoBillIns.cs` (new partial): `GetNoBillInsForNewEstimate` (procedure flag OR the code/plan default), `SetNoBillInsFromUser`, `ClearNoBillInsForBilledClaimProc`, `UpdateNoBillIns`, audit text and permission helpers.
- Hooks: `ClaimProcs.CreateEst` and the estimate creation in `Procedures.ComputeEstimates` use `GetNoBillInsForNewEstimate` (rule 2). This covers rebuilt estimates (a plan change, Procedure Info deleting estimates on a code change), Add Estimate, claims and preauthorizations.
- `FormProcEdit.checkNoBillIns_Click`: after the claimprocs are updated, the procedure flag is saved and audited, and `_procedureOld` is synced so OK does not write it again. The flag is saved at once because the claimprocs already are (24.3 behavior); Cancel keeps both, as before.
- `FormClaimProc.butSave_Click` (when it saves to the database): a claimproc changed from not billed to billed clears the procedure flag (rule 3).
- Audit: securitylog under ProcCompleteEditMisc for completed (C, EC, EO) procedures, else ProcEdit, FKey = ProcNum, text "(code) (date): Do Not Bill to Ins changed from (old) to (new)" with old/new "checked", "unchecked" or "mixed" (the box's state before the click), and " (cleared because a claimproc was set to bill insurance)" for rule 3. No entry when the box did not change.
- Back-fill (rule 4): `OpenDentBusiness/Misc/ForkMigrations/NoBillInsBackfillStep.cs`. Flags procedures that are TP/TPi or complete and unbilled (no claimproc on a claim other than a preauthorization), have at least one Estimate/CapEstimate claimproc, all of them NoBillIns=1, and for each of them the code/plan default would bill. Only on databases from before 25.4 (`Seed26_1Step.IsFromBefore25_4`), on the first run of its level, and only while no procedure is flagged yet. INFERRED refinement: when `InsPlanExclusionsMarkDoNotBillIns` is on, an estimate of a plan with any exclusion benefit is not taken as a user decision (the preference marks excluded estimates automatically, which looks the same in the data).

### Teeth on age limits (26.1) and exclusions (25.4)

- `Benefits.EstimateTeeth.cs`: `CanHaveTeeth` (exclusions; limitations with the AgeLimit qualifier) and `IsAgeLimitForTeeth`. `GetValidationErrors` and `GetFromOtherBenefitsRow` (owned `Benefits.cs`) accept teeth for age limits; the message is now "Teeth can only be entered for exclusions and age limitations."
- `GetLimitationByCode` takes the procedure's `toothNum`/`toothRange` (new optional parameters; `ClaimProcs.ComputeBaseEst` passes them). Every age-limit check (category, code group, procedure code, the range-aware code group pass, family) applies a row only when `IsAgeLimitForTeeth`:
  - A row with teeth applies when the procedure's tooth, or any tooth of its tooth range, is listed (same matcher as exclusions; the 26.1 spec names `ToothNum` only, INFERRED extension to ranges for consistency). A procedure with no tooth never matches it.
  - A row without teeth applies unless a row with teeth of the same scope (same code group, category and code, family or not) matches the procedure's teeth (INFERRED precedence, rule 3 of the spec).
  - Failing a toothed age limit is an age-limitation denial (`LimitationTypeMet.Aging`), so the "zero write-offs on frequency or aging" preference and plan override apply.
  - Exclusions never consider age (they are not limitations).
- `FormInsBenefits`: Age Limits grid gets an editable Teeth column; every age limit of a code group gets its own row (a toothless row first, then rows with teeth; 24.3 showed only the first and dropped the others on Save in the simplified view); a Teeth cell is tidied or turned red when left, and Save (and leaving the simplified view) refuses bad teeth or teeth without an age. Column widths of that grid were narrowed to fit. The Other Benefits Teeth cell is editable for age limitation rows. After Add Benefit, the Age Limits grid is refilled, so a second (toothed) age limit for a group added there is not lost.
- `FormBenefitEdit` (+ designer): the Teeth box follows the type and the quantity qualifier (new `SelectedIndexChanged` handler); teeth are saved for exclusions and age limits; hint text updated.

## INFERRED behaviors

- The as-of rules above (scheduled = Scheduled or ASAP; past appointment → today; planned does not count; unscheduled procedures keep the picker date and today's age).
- Coverage end checks only `inssub.DateTerm` (patplan has no dates) and only for scheduled procedures in the Treatment Plan module; the computed estimate is zeroed but the user's overrides (InsEstTotalOverride, DedEstOverride) are kept, as the TP grid does for frequency limitations; the PPO write-off estimate becomes 0.
- Loop entries of other benefit periods do not count toward the annual max, family max or deductible; lifetime limits count everything.
- "Estimated as of" description line.
- Do Not Bill audit wording and states; the flag is saved at once.
- The back-fill rule and its exclusion refinement.
- Tooth ranges match a toothed age limit when any tooth matches; toothed/toothless precedence by scope; teeth without an age are refused in the Age Limits grid.
- 25.4's own behavior of the Age Limits Teeth column ("only when the code group row is used for an exclusion", spec VERIFY) is not public; the column has the 26.1 meaning from the start.

## Deviations from the spec

- No "Est. Date" column (optional in the spec): it would need a Display Fields entry (M2-02 registry). A description line is shown instead.
- Edit Benefit keeps text typed in a disabled Teeth box (M1's choice, so the user can switch back) instead of clearing it; it is not saved.
- Fees of scheduled procedures still come from today's fee schedules (A-FEE's rule: only completed procedures use their date). The release note speaks of estimates only.
- The backlog's test approach names `ClaimProcsTests` and `BenefitsTest` (the old UnitTests project, not run by the shared runner); the tests are in `tests/M2ESTTests` instead.
- `ControlTreat.cs`, `Tooth.cs`, `BenefitLogic.cs`, `FormBenefitFrequencies.cs` (owned) needed no change: the date picker and its refresh already drive `TreatmentPlanModules`, tooth lists were complete from M1, frequencies have no teeth.

## The fork migration level

The assigned range 2027002701 to 2027002799 is not a valid level: `ForkMigrationRunner.ValidateStep` requires `yyyyMMddNN` with a real date from 2026 on, and 2027-00-27 has month 00. A step with such a level makes `GetSteps()` throw, which stops every migration (and every test that builds a database). So `NoBillInsBackfillStep` is written with `LEVEL=2027002701` but declared `abstract`, which the runner skips; its logic (`NoBillInsBackfillStep.Run`, `GetProcNumsToFlag`, `Apply`) is tested through a subclass in `M2ESTTests`. To activate it at integration: settle the level scheme for all packages (accept the package ranges in `ValidateStep`, or give the step a dated level such as one after 2026102501), then remove `abstract` (and update `LEVEL`). It must run after M2-01's 2026102401 (which adds the column) and may run before or after Seed26_1. No README row was added to `ForkMigrations/README.md` for the same reason.

## Interplay with packages built at the same time

- **M2-HIST** (C2, takes over `Procedures.cs`, `ClaimProcs.cs`): the hooks here are one line each (`GetNoBillInsForNewEstimate` in `CreateEst` and `ComputeEstimates`, `ZeroEstimateIfCoverageEnded` in `ComputeForOrdinal`, the teeth arguments of `GetLimitationByCode`); new logic is in the partial files. Insurance history from code groups should keep the `IsDateAsOf` rule if it adds loop entries.
- **M2-NOTES** (C2, takes over `FormProcEdit.cs`): the Do Not Bill hook sits in `checkNoBillIns_Click` only.
- **M2-CARRIER** (C3, takes over `ControlTreat.cs`): `ControlTreat` was not changed here.
- **M2-TERM** ("standard provider fee" text) and **M2-02**: `DisplayFields26_1.GetColumnText` ("Cat% Std Fee") is for `ControlTreat` headers; not used here because `ControlTreat` needed no change (M2-02's note suggested either M2-TERM or M2-EST).
- **M2-INSV / M2-INSPLAN**: a plan change that rebuilds estimates now keeps a procedure's Do Not Bill decision; nothing to do on their side.
- **M2-01**: uses `procedurelog.NoBillIns` (`Procedure.NoBillIns`) and `benefit.ToothRange` as they are; no schema or CRUD was added or changed.
- **A-FEE (M1)**: `Fees.GetDateAsOfForProc` is unchanged (see Deviations).

## Files touched

- New: `OpenDentBusiness/Data Interface/Benefits.EstimateTeeth.cs`, `OpenDentBusiness/Data Interface/Procedures.EstimateAsOf.cs`, `OpenDentBusiness/Data Interface/Procedures.NoBillIns.cs`, `OpenDentBusiness/Db Multi Table/TreatmentPlanModules.EstimateDates.cs`, `OpenDentBusiness/Misc/ForkMigrations/NoBillInsBackfillStep.cs` (inactive, see above), `tests/M2ESTTests/*`, this note.
- Changed (owned): `OpenDentBusiness/Data Interface/Benefits.cs` (declaration made `partial`, loop period filters, teeth on age limits, validation), `ClaimProcs.cs` (3 lines and the `ClaimProcHist.IsDateAsOf` field), `Procedures.cs` (2 lines), `OpenDentBusiness/Db Multi Table/TreatmentPlanModules.cs` (declaration made `partial`, the estimate loops), `OpenDental/Forms/FormProcEdit.cs` (4 lines), `FormClaimProc.cs` (2 lines), `FormInsBenefits.cs`, `FormBenefitEdit.cs` and its designer.
- Changed (shared, small and local): `OpenDentBusiness/OpenDentBusiness.csproj` (4 `Compile` lines), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Not changed: schema, CRUD, `Pref.cs`, `PrefInfos.xml`, permissions, `DisplayFields.cs`, enums, seed rows.

## Tests

New project `tests/M2ESTTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). Database tests use one scratch database per run (`forkmig_<time>_<pid>_<random>_m2est`, built like `tools/New-DevDatabase.ps1` and migrated with the fork runner, dropped afterwards); dates are relative to today so the tests pass on any day; synthetic Z codes only (Z7000-Z7999 are added to the General category of the scratch database).

- `EstimateDatesTests` (no database): as-of rules, age on the as-of date, the dated copy and `FinishEstimate`, loop dates, the description line, the history date, coverage end.
- `BenefitRulesTests` (no database): `CanHaveTeeth`, validation and Other Benefits row parsing of teeth (international numbering too), toothed/toothless precedence and scope, primary teeth, benefit periods (calendar and service year, renewal day), `IsLoopHistInPeriod`.
- `NoBillInsRulesTests` (no database): box state, audit words and permission, back-fill rule table, the inactive step.
- `TpEstimateAsOfDbTests`: the Treatment Plan module calls end to end, frequency checking on and off: annual max next calendar year, this year's paid history not reducing next year, service-year renewal between today and the appointment (and the day before renewal), planned and unscheduled-list appointments, past appointments, procedure and claimproc dates unchanged without the picker, a TP date in the next year with a procedure scheduled this year, deductible per year, lifetime max across years, frequency as of each appointment, age on the appointment date, coverage ended, and coverage end ignored outside the TP module.
- `BenefitPeriodDbTests`: `GetLimitationByCode`/`GetDeductibleByCode` with marked and unmarked loop entries: individual and family max, service year, lifetime, individual and family deductible.
- `AgeLimitTeethDbTests`: toothed code group age limits (molar, premolar, no tooth), precedence of toothed and toothless rows, category and procedure code age limits with teeth (primary teeth), tooth-range procedures, exclusions never considering age, round trip and the insurance edit log.
- `NoBillInsDbTests`: inheritance on creation and rebuild (also `CreateEst`), code default unchanged, audit entries and permissions, rule 3, the TP grid's Allowed column, back-fill rules and guards (`Run`, `Apply` through a subclass, databases from 25.4 or later untouched).
- `WiringTests`: source checks of the window hooks (`FormProcEdit`, `FormClaimProc`, `FormBenefitEdit`, `FormInsBenefits`), since the GUI is not launched.

Results (2026-10-25): build of `OpenDental.Local.slnf` (Debug, x86, full rebuild) 0 errors, 376 warnings (the baseline). Targeted tests all passed: `M2ESTTests` 66/66, `InsuranceTests` 49/49 (A-INS's exclusion, code group and Other Benefits tests, which cover the changed `Benefits` methods), `ForkMigrationTests` 38/38 (a file was added under `ForkMigrations`), `NoHqTests` 49/49. `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- No window was opened (OpenDental.exe is not launched): the Age Limits grid editing, Edit Benefit's Teeth box and the Procedure Info box are checked by source (`WiringTests`) and through the business methods they call.
- Middle Tier (the new methods follow the usual `Meth` patterns; the coverage-end scope is client side like the rest of the TP estimate loop).
- Canadian lab procedures with scheduled appointments (Canadian claim features are excluded).
- The back-fill as a registered step in a real migration run (it is inactive, see above); its logic is tested directly.

## Questions for the owner

1. Fork migration levels: the C1 ranges (here 2027002701-2027002799) fail `ForkMigrationRunner.ValidateStep` (month 00). Should `ValidateStep` accept package ranges, or should package steps get dated levels at integration? `NoBillInsBackfillStep` is ready either way (remove `abstract`).
2. Unscheduled procedures keep the age today and the picker date (the spec says they keep today's behavior). Should their age also follow the TP date picker?
3. Should fees of scheduled procedures follow the appointment date (dated fee schedules), like the estimates now do?
4. Coverage end: only `inssub.DateTerm`, and only in the Treatment Plan module. Should the chart and appointment estimates also honor it, and should an effective date after the appointment (`DateEffective`) also mean no estimate?
5. Back-fill refinement: with "exclusions mark Do Not Bill" on, estimates of plans with exclusions are not migrated. Acceptable, or migrate them anyway?
6. Is the "Estimated as of (date)" line in the TP description wanted, or should it become the optional "Est. Date" display field (M2-02 would register it)?
