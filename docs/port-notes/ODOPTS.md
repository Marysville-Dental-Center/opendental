# ODOPTS: the eight Open Dental 25.3 options this program had built without

The owner's answer of 2026-11-07 to the list of options this program built without ([SWMAP.md](SWMAP.md), "Settings of features this
program built without that option"): "why not just build it?". Branch `next/odopts-1`, cut from `next/swmap-2`; review fixes on
`next/odopts-2` (Statement Send Setup is Message-to-Pay in Open Dental, so a carried-over Always asks; every path that re-prices a procedure
updates the secondary claim; eForm entries that are not imported are recorded and shown). Tests: `tests/ODOPTSTests`, plus the package test
assemblies named under Checks.

## The rule

Each option is Open Dental's own preference row, read and written under Open Dental's exact name, outside `PrefName`, the way SWMAP reads the
settings Open Dental 24.4 to 25.3 added (`PrefC.GetRawOrDefault`, `PrefC.UpsertRaw`, through `OdOptions` in
`OpenPmsDentalBusiness/Misc/OdOptions.cs`):

- **On the office database** the switch-over changes no row: this program reads the office's rows as Open Dental left them. There is no
  conversion step and no fork migration step: none of these settings ever had a name of this program's own.
- **On a database without the row** (a new database, a database from 24.3) the setting reads as the default in the table below. A row is
  only written when someone changes the setting, and saving the default while the row is missing writes nothing.
- Every change of one of these settings in its window makes a Setup (or Security Admin) Audit Trail entry naming the setting, Open Dental's
  name of the row and the old and the new value (`OdOptions.GetChangeLogText`).
- `AdoptionValidation migration-preview` (runbook step 0.2) lists each of them with the value this program will show
  (`OdSettingNames.ListReads`).

No Open Dental program was installed, observed or decompiled, no office value was used (the office's values were given for understanding
only), and nothing was downloaded: the public sources are the feature specs of this repository (`docs/specs/features-24.4.md` to
`features-25.3.md`) and the pages they cite. Where they say nothing, the option does what its name says; such meanings and defaults are
marked **INFERRED** and listed as owner questions below, to confirm by testing the office's copy in Open Dental before the switch-over.

## The eight options

| Open Dental's row | What this program does | Where it is set | Missing row reads as |
|---|---|---|---|
| `PerioRedAttGing` | A red threshold for the Perio Chart's auto Att Ging row (24.4): a site whose attached gingiva is **at or below** the value (mm) is drawn red and bold, like the MGJ row; the "# Teeth" box counts the teeth, and the printed perio chart draws the same red and adds the line "Teeth with Att Ging less than or equal to N mm" (only when a threshold is set). **INFERRED:** the direction (narrow attached gingiva is the finding, as for MGJ); a blank or unreadable value means no threshold. | Perio Chart, "Numbers in red": the new row "Att Ging (<=)" with its up and down buttons (Setup permission). Down from 0 removes the threshold (blank); up from none starts at 0. | No threshold: never red, as in 24.4 (**INFERRED**) |
| `DomainLoginForSigBoxes` | 1: the Change User prompts of signature boxes offer domain login while domain login is on (25.2, `v25_2-security-domain-login-change-user`): procedure notes, group notes (both boxes), sheets, treatment plan signing and the ortho chart. 0: those prompts ask for the program's own password only. The task window's From User prompt is not a signature box and keeps offering domain login whenever it is on. | Security, Global Security Settings, Domain Login group: "Offer domain login in signature boxes' Change User" (Security Admin entry). | On, the behaviour of 25.2 (**INFERRED**; the public sources do not give Open Dental's default) |
| `ClaimUpdateFeeForSecondary` | 1: when a procedure's fee changes and is saved while the procedure is on a primary claim, each of its secondary claims that is not sent or received is recalculated: the fee billed follows the new fee (the procedure fee, or the UCR fee when the plan bills UCR, as Recalculate does), with the claim total and the estimates that follow, and a Claim Edit Audit Trail entry. This happens on every path that changes the fee of an existing procedure that can be on a claim: the procedure window, Edit All (a new provider re-prices the selected procedures, `ProcFeeUpdatePrompt`), and the paths that re-price the procedures of an appointment that is not complete when its provider changes (moving it in the Appointments module or from the Pinboard, and the appointment window's save). The primary claim is not touched, nor a secondary claim that is sent or received. **INFERRED** (not public). | Preferences, Account, Claim Receive, new group "Secondary Claims". | Off, the behaviour of 24.3 (**INFERRED**) |
| `EFormImportAllergiesAsOther` | 1: an allergy the patient typed on an eForm (`allergiesOther`) that matches no allergy in the list is imported as an "Other" allergy with the patient's text, as this program always did (24.3; converted later with `v25_2-chart-eform-allergy-other-convert`). 0: it is not imported, but never without a trace: auto import skips it, writes the line "Allergy not imported (matches no allergy in the list; EFormImportAllergiesAsOther off): *the patient's text*" in the form's auto import Audit Trail entry (EFormImport, after its count of changes, which does not count it) and tells the user who saved the form; the manual import lists it unticked ("no match: tick to add as Other"), so staff can still add it. **INFERRED** (not public). | eForm Setup (Setup, eForms, Setup). | On, the behaviour of 24.3 (**INFERRED**) |
| `EFormImportProblemsAsOther` | The same for problems (`problemsOther`, an "Other" problem with the text in the patient note; a skipped one is the line "Problem not imported (matches no problem in the list; EFormImportProblemsAsOther off): *the patient's text*"). **INFERRED.** | eForm Setup. | On (**INFERRED**) |
| `InsHistPerioScalingCode` | A procedure code (or the CodeNum of one, read too, like `SalesTaxProcCode`). When it names a procedure code, the Insurance History rows of every perio scaling code group (code group category SRP, or a Perio Four Quadrants group such as the "Perio Scaling History" group the Insurance History step adds with no category since the owner's decision of 2026-11-07 (integration `next/int262-1`, `InsHistCategories.IsPerioScalingGroup`); its four quadrant rows included) use it as their History Proc Code: a date entered creates a procedure with that code, its procedures count as the row's history, and an eligibility (271) date for that code goes to the row. Blank, or a value that names no procedure code: each group's own History Proc Code, the behaviour of 26.1's code-group Insurance History ([M2-HIST.md](M2-HIST.md)). **INFERRED** (not public). | Preferences, Treatment Plan, Frequency Limitations, new group "Insurance History" (a box with the procedure code picker; blank or a valid code). | Blank |
| `StatementAutoSendEmail` | **What the public source says Open Dental does:** its Statement Send Setup window (25.1) sends Message-to-Pay links automatically when statements are created ([features-25.1.md](../specs/features-25.1.md), `v25_1-eservices-msgtopay-on-statement-creation`; 25.2 added Message-to-Pay by secure e-mail, `v25_2-eservices-msgtopay-secure-email`). Message-to-Pay is a hosted Open Dental service this program left out, so in Open Dental this row most likely controls automatic Message-to-Pay messages, not e-mailing the statement PDF. **What this program does instead (INFERRED substitute for the excluded hosted feature):** when Billing creates statements (Billing Options, Create List), the new statements whose mode is E-mail are e-mailed as the Billing List opens with them, through the Billing List's own Send path (the statement PDF to the patient's e-mail address, marked sent, Audit Trail as before). **INFERRED values:** 0 never (they wait for Send, as in 24.3), 1 ask once with the count, 2 always; any other value reads as 0, so an unknown value never sends. **Always asks until the window is saved here:** a stored 2 is used as 1 (the Billing List asks, and says why) until the Statement Send Setup window has been saved once in this program (this program's own row `ForkStatementSendSetupSaved`), so a value set in Open Dental's window for Message-to-Pay never e-mails statements by itself after the switch-over. Statements made one at a time in the Account module are not affected (the user chooses e-mail there already). | Billing Defaults, new button "Send Setup..." (named after Open Dental 25.1's Statement Send Setup window, which is for Message-to-Pay; see above). | 0, never (**INFERRED**) |
| `StatementAutoSendSMS` | The same values for texts; in Open Dental most likely Message-to-Pay by text (the same public source). **No text is sent:** Open Dental's hosted texting was cut and this program has no texting provider, so when it is 1 or 2 and billing created statements, the Billing List says the statements were not texted because texting needs a provider; the window says so too. The hook for a provider is `StatementAutoSend.TextSender` (`IStatementTextSender`), which a provider package sets (owner decision). | Statement Send Setup. | 0, never (**INFERRED**) |

## Design notes

- **Where the values come from.** The windows read each option through its class (`PerioMeasures.GetRedAttGing`,
  `DomainLogin.IsForSigBoxesSettingOn`, `ClaimFeeForSecondary.IsEnabled`, `EFormImport.IsAllergiesAsOther` and `IsProblemsAsOther`,
  `InsHistCategories.GetPerioScalingCodeSetting`, `StatementAutoSend.GetEmailMode` and `GetSmsMode`); each reads Open Dental's row from the
  preference cache and never throws. A save signals `InvalidType.Prefs` so other workstations reload.
- **The secondary claim fee** is updated (`ClaimFeeForSecondary.UpdateForProcFeeChange`) on every path that changes the fee of an existing
  procedure that can be on a claim, each time after the path has saved its claimprocs, so in-memory claimprocs cannot write the old fee
  billed back: the procedure window (`FormProcEdit_FormClosing`, after `Procedures.ComputeEstimates`), Edit All (`FormProcEditAll.butSave_Click`,
  after `ClaimProcs.UpdateMany`), and the appointment paths that re-price when the provider changes, which can include a completed procedure
  attached to an appointment that is not complete (`Procedures.SetProvidersInAppointment` after its `Sync`, used when an appointment is moved
  in the Appointments module or from the Pinboard, and `Procedures.UpdateProcsInApptHelper` after each `Update`, used by the appointment
  window's save; neither saves claimprocs). The other places that set a fee make a new procedure (no claim yet), or re-price treatment
  planned procedures only (the Treatment Plan module's fee update; their claims are pre-authorizations, never primary or secondary claims);
  the mobile apps' `Procedures.TryValidateProcFee` overload has no caller (the mobile apps are cut). The update is
  `Claims.CalculateAndUpdateSecondaries`, the recalculation the program already runs for secondary claims when a primary claim is received;
  it does nothing while the setting is off or the fee did not change.
- **Statement Send Setup is Message-to-Pay in Open Dental.** The only public source on `StatementAutoSendEmail` and `StatementAutoSendSMS` is
  the 25.1 item `v25_1-eservices-msgtopay-on-statement-creation` ([features-25.1.md](../specs/features-25.1.md), excluded there as a hosted
  service): Open Dental's Statement Send Setup window sends Message-to-Pay links automatically when statements are created (and 25.2 can send
  Message-to-Pay by secure e-mail). This program has no Message-to-Pay, so it gives the rows a meaning of its own, e-mailing the statement PDF
  itself; that meaning is **INFERRED**, a substitute for the excluded feature, and Open Dental never e-mailed statements on creation through
  these rows as far as the public sources show. So that an office whose Open Dental row turned on automatic Message-to-Pay does not have every
  e-mail statement e-mailed as soon as billing creates it after the switch-over, a stored Always (2) is used as Ask
  (`StatementAutoSend.GetEmailModeToUse`) until the Statement Send Setup window has been saved once in this program; the Billing List's
  question and the window say why, and `migration-preview` shows the row as "Always (asks until Statement Send Setup is saved in this
  program)". The first save writes this program's own row `ForkStatementSendSetupSaved` = 1 (missing = never saved here; no migration writes
  it) with a Setup Audit Trail entry. Owner question ODOPTS 6 asks first whether the office uses the rows for Message-to-Pay and whether it
  wants them to mean "e-mail the statement" here.
- **eForm entries that are not imported** (`EFormImportAllergiesAsOther` or `EFormImportProblemsAsOther` off) are collected by
  `EFormImport.ImportAutoFields(eForm,out listNotImported)`: one line each in the form's `EFormImport` Audit Trail entry, after "N change(s).",
  which counts only the changes (the entry is made even when nothing was imported), and the eForm window shows them to the user who saved the
  form. The form keeps the patient's text, so the manual import can still add them.
- **The perio scaling code** replaces the History Proc Code of the perio scaling rows in `InsHistCategories.GetAll`, so the Insurance History
  window, the Family module, entering dates and eligibility imports all use it. A code typed in another case is matched to the procedure code
  list's spelling.
- **The statements a billing run created** are the unsent statements after the run that were not unsent before it (Billing Options notes both
  lists only while one of the two settings can send something). Statements another user creates at the same moment would count too; billing
  runs are not run side by side in practice.
- **The Perio Chart's threshold box** follows the other "Numbers in red" rows: up and down buttons, Setup permission, the tooth count. The
  window's buttons below the group moved down 20 pixels to make room.

## Files

- New: `OpenPmsDentalBusiness/Misc/OdOptions.cs` (the eight names, reading and saving bool and text rows, the log text),
  `OpenPmsDentalBusiness/Data Interface/ClaimFeeForSecondary.cs`, `OpenPmsDentalBusiness/Data Interface/StatementAutoSend.cs`,
  `WpfControlsOD/Frms/FrmStatementSendSetup.xaml(.cs)`, the test project `tests/ODOPTSTests`.
- Changed, business layer: `PerioMeasures.cs` (the threshold), `DomainLogin.cs` (the switch, `IsOfferedInChangeUser`), `EFormImport.cs`
  (the two settings, the auto import and the entries it did not import), `InsHistCategories.cs` (the perio scaling code), `Procedures.cs`
  (the fee hook of the appointment paths), `OdSettingNamesStep.cs` (`ListReads`: the eight for migration-preview).
- Changed, windows: `ContrPerio.cs` and `FormPerio.cs` (red, counts, the new row, the printout), `FormLogOn.cs` and the six signature box
  callers (`FormProcEdit`, `FormProcGroup` twice, `FormSheetFillEdit`, `FormTPsign`, `FormOrthoChart`), `FormGlobalSecurity`,
  `UserControlAccountClaimReceive`, `FormProcEdit` and `FormProcEditAll` (the fee hook), `FrmEFormSetup`, `FormSheetImport`,
  `FrmEFormFillEdit` (the entries not imported), `UserControlTreatPlanFreqLimit`, `FormBillingDefaults`, `FormBillingOptions`, `ControlManage`,
  `FormBilling`.
- Tests: the new `tests/ODOPTSTests` (rules without a database, the windows' wiring, the documents, and each option on a scratch database
  `test_odopts_*` built like a fresh 24.3.63 database and upgraded by the fork migration runner); `SWMAPTests` (the eight names now read,
  made-up values on the adopted synthetic 25.3.48 databases); the wiring tests of `SignatureTests` and `M2NOTESTests` (the change-user
  prompts' new `isSigBox` argument).
- Docs: this note, [SWMAP.md](SWMAP.md) (the eight names moved from "built without" to "read by this program"), the notes of B-PERIO,
  B-SHELL and B-EF, the owner questions, the adoption runbook (the eight in "Settings that carry over by Open Dental's name", so step 7.1
  compares them) and one line each in `docs/release-notes/next.md`.

## Checks

- Build of `OpenDental.Local.slnf` (Debug x86) through the build lane: 0 errors.
- Targeted tests, all passed (after the review fixes, `next/odopts-2`): ODOPTSTests 56/56, SWMAPTests 24/24, RegistryTests 39/39,
  BPERIOTests 55/55, BCLAIMTests 55/55, InsuranceTests 49/49, BINSHTests 36/36, EFormTests 60/60, M2EFTests 46/46, StatementTests 8/8,
  BACCTTests 41/41, M2NOTESTests 51/51, SignatureTests 55/55, GroupNoteTests 54/54, BSHELLTests 68/68, M2HISTTests 43/43, BPRINTTests 71/71,
  M2DUNTests 35/35, M2SDVTests 47/47, M2MISCTests 78/78, M2SHELLTests 39/39, M2AUDITTests 28/28, M2CLAIMRCVTests 54/54, M2SHEETSTests 53/53,
  M2ESTTests 70/70, BTIMETests 28/28, M2EMPTests 35/35, MaintenanceTests 111/111, SshHostKeyTests 46/46, BEFTests 29/29,
  AdoptionValidation.Tests 66/66, NoHqTests 58/58; for the fee paths and the appointment code also FeeTests 48/48, InventoryTests 93/93,
  ApptTests 76/76, BAPPTTests 61/61, M2APPTTests 37/37, BPROCTests 43/43.
- Not run here: ForkMigrationTests and InitSqlTests (no schema change and no fork migration step in this package; `db/init/new-database.sql`
  is not regenerated).
- Not checked in the running program: the windows (Perio Chart, Global Security Settings, Preferences, eForm Setup, Statement Send Setup,
  the Billing List, Edit All, the eForm window's message). OpenPmsDental.exe is not launched in package work; their wiring is checked by reading the sources (`OptionWiringTests`)
  and the rules they call are tested.

## Owner questions (to confirm by testing in Open Dental)

**Decided as built (owner, 2026-11-07):** every ODOPTS question is settled as built ("all as recommended"). For `StatementAutoSendEmail`
and `StatementAutoSendSMS` (Message-to-Pay in Open Dental) the safeguard stays as built: a stored Always acts as Ask until Statement Send
Setup is saved in this program.

These are in [../owner-questions.md](../owner-questions.md), section "Open Dental 25.3 options built (ODOPTS, 2026-11-07)".
