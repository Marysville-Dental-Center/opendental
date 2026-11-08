# ODOPTS: the eight Open Dental 25.3 options this program had built without

The owner's answer of 2026-11-07 to the list of options this program built without ([SWMAP.md](SWMAP.md), "Settings of features this
program built without that option"): "why not just build it?". Branch `next/odopts-1`, cut from `next/swmap-2`. Tests: `tests/ODOPTSTests`,
plus the package test assemblies named under Checks.

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
| `ClaimUpdateFeeForSecondary` | 1: when a procedure's fee is changed and saved in the procedure window, and the procedure is on a primary claim, each of its secondary claims that is not sent or received is recalculated: the fee billed follows the new fee (the procedure fee, or the UCR fee when the plan bills UCR, as Recalculate does), with the claim total and the estimates that follow, and a Claim Edit Audit Trail entry. The primary claim is not touched, nor a secondary claim that is sent or received. **INFERRED** (not public). | Preferences, Account, Claim Receive, new group "Secondary Claims". | Off, the behaviour of 24.3 (**INFERRED**) |
| `EFormImportAllergiesAsOther` | 1: an allergy the patient typed on an eForm (`allergiesOther`) that matches no allergy in the list is imported as an "Other" allergy with the patient's text, as this program always did (24.3; converted later with `v25_2-chart-eform-allergy-other-convert`). 0: it is not imported: auto import skips it, and the manual import lists it unticked ("no match: tick to add as Other"), so staff can still add it. **INFERRED** (not public). | eForm Setup (Setup, eForms, Setup). | On, the behaviour of 24.3 (**INFERRED**) |
| `EFormImportProblemsAsOther` | The same for problems (`problemsOther`, an "Other" problem with the text in the patient note). **INFERRED.** | eForm Setup. | On (**INFERRED**) |
| `InsHistPerioScalingCode` | A procedure code (or the CodeNum of one, read too, like `SalesTaxProcCode`). When it names a procedure code, the Insurance History rows of every perio scaling code group (code group category SRP, its four quadrant rows included) use it as their History Proc Code: a date entered creates a procedure with that code, its procedures count as the row's history, and an eligibility (271) date for that code goes to the row. Blank, or a value that names no procedure code: each group's own History Proc Code, the behaviour of 26.1's code-group Insurance History ([M2-HIST.md](M2-HIST.md)). **INFERRED** (not public). | Preferences, Treatment Plan, Frequency Limitations, new group "Insurance History" (a box with the procedure code picker; blank or a valid code). | Blank |
| `StatementAutoSendEmail` | When Billing creates statements (Billing Options, Create List), the new statements whose mode is E-mail are sent as the Billing List opens with them, through the Billing List's own Send path (the statement PDF to the patient's e-mail address, marked sent, Audit Trail as before). **INFERRED values:** 0 never (they wait for Send, as in 24.3), 1 ask once with the count, 2 always; any other value reads as 0, so an unknown value never sends. Statements made one at a time in the Account module are not affected (the user chooses e-mail there already). | Billing Defaults, new button "Send Setup..." (the Statement Send Setup window, as in Open Dental 25.1). | 0, never (**INFERRED**) |
| `StatementAutoSendSMS` | The same values for texts. **No text is sent:** Open Dental's hosted texting was cut and this program has no texting provider, so when it is 1 or 2 and billing created statements, the Billing List says the statements were not texted because texting needs a provider; the window says so too. The hook for a provider is `StatementAutoSend.TextSender` (`IStatementTextSender`), which a provider package sets (owner decision). | Statement Send Setup. | 0, never (**INFERRED**) |

## Design notes

- **Where the values come from.** The windows read each option through its class (`PerioMeasures.GetRedAttGing`,
  `DomainLogin.IsForSigBoxesSettingOn`, `ClaimFeeForSecondary.IsEnabled`, `EFormImport.IsAllergiesAsOther` and `IsProblemsAsOther`,
  `InsHistCategories.GetPerioScalingCodeSetting`, `StatementAutoSend.GetEmailMode` and `GetSmsMode`); each reads Open Dental's row from the
  preference cache and never throws. A save signals `InvalidType.Prefs` so other workstations reload.
- **The secondary claim fee** is updated where the procedure window saves its claimprocs (`FormProcEdit_FormClosing`, after
  `Procedures.ComputeEstimates`), so the window's in-memory claimprocs cannot write the old fee back. The update is
  `Claims.CalculateAndUpdateSecondaries`, the recalculation the program already runs for secondary claims when a primary claim is received.
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
  (the two settings and the auto import), `InsHistCategories.cs` (the perio scaling code), `OdSettingNamesStep.cs` (`ListReads`: the eight
  for migration-preview).
- Changed, windows: `ContrPerio.cs` and `FormPerio.cs` (red, counts, the new row, the printout), `FormLogOn.cs` and the six signature box
  callers (`FormProcEdit`, `FormProcGroup` twice, `FormSheetFillEdit`, `FormTPsign`, `FormOrthoChart`), `FormGlobalSecurity`,
  `UserControlAccountClaimReceive`, `FormProcEdit` (the fee hook), `FrmEFormSetup`, `FormSheetImport`, `UserControlTreatPlanFreqLimit`,
  `FormBillingDefaults`, `FormBillingOptions`, `ControlManage`, `FormBilling`.
- Tests: the new `tests/ODOPTSTests` (rules without a database, the windows' wiring, the documents, and each option on a scratch database
  `test_odopts_*` built like a fresh 24.3.63 database and upgraded by the fork migration runner); `SWMAPTests` (the eight names now read,
  made-up values on the adopted synthetic 25.3.48 databases); the wiring tests of `SignatureTests` and `M2NOTESTests` (the change-user
  prompts' new `isSigBox` argument).
- Docs: this note, [SWMAP.md](SWMAP.md) (the eight names moved from "built without" to "read by this program"), the notes of B-PERIO,
  B-SHELL and B-EF, the owner questions, the adoption runbook (the eight in "Settings that carry over by Open Dental's name", so step 7.1
  compares them) and one line each in `docs/release-notes/next.md`.

## Checks

- Build of `OpenDental.Local.slnf` (Debug x86) through the build lane: 0 errors.
- Targeted tests, all passed: ODOPTSTests 46/46, SWMAPTests 24/24, RegistryTests 39/39, BPERIOTests 55/55, BCLAIMTests 55/55,
  InsuranceTests 49/49, BINSHTests 36/36, EFormTests 60/60, M2EFTests 46/46, StatementTests 8/8, BACCTTests 41/41, M2NOTESTests 51/51,
  SignatureTests 55/55, GroupNoteTests 54/54, BSHELLTests 68/68, M2HISTTests 43/43, BPRINTTests 71/71, M2DUNTests 35/35, M2SDVTests 47/47,
  M2MISCTests 78/78, M2SHELLTests 39/39, M2AUDITTests 28/28, M2CLAIMRCVTests 54/54, M2SHEETSTests 53/53, M2ESTTests 70/70, BTIMETests 28/28,
  M2EMPTests 35/35, MaintenanceTests 111/111, SshHostKeyTests 46/46, BEFTests 29/29, AdoptionValidation.Tests 66/66, NoHqTests 58/58.
- Not run here: ForkMigrationTests and InitSqlTests (no schema change and no fork migration step in this package; `db/init/new-database.sql`
  is not regenerated).
- Not checked in the running program: the windows (Perio Chart, Global Security Settings, Preferences, eForm Setup, Statement Send Setup,
  the Billing List). OpenPmsDental.exe is not launched in package work; their wiring is checked by reading the sources (`OptionWiringTests`)
  and the rules they call are tested.

## Owner questions (to confirm by testing in Open Dental)

These are in [../owner-questions.md](../owner-questions.md), section "Open Dental 25.3 options built (ODOPTS, 2026-11-07)".
