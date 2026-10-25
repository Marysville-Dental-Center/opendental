# Port backlog: Open Dental 24.4 to 26.1 for our 24.3.63 fork

Status: draft, 2026-10-18. Inputs: the verified specs in this folder ([features-24.4.md](features-24.4.md), [features-25.1.md](features-25.1.md), [features-25.2.md](features-25.2.md), [features-25.3.md](features-25.3.md), [features-25.4.md](features-25.4.md), [features-26.1.md](features-26.1.md)), the schema spec [schema-changes-24.3-to-26.1.md](schema-changes-24.3-to-26.1.md) with its twin [schema-changes.json](schema-changes.json), and [../decoupling-plan.md](../decoupling-plan.md). Every feature ID below links to its main public source; the spec files hold the full source lists and the behavior detail. This file adds only planning: grouping, file ownership, order, tests and risks.

## At a glance

- **241 spec items in scope**: 220 desktop and 21 vendor-direct. Of these, 3 are already in our 24.3.63 code (verified in B-ERA, B-SERVER and B-PROC) and 5 are 25.4 twins of 25.3 backports (verified in M2-VERIFY). **165 items are excluded** (summary near the end). Every one of the 241 IDs is assigned to exactly one package (index in Appendix A).
- **M1, "Adopt the office database (OD 25.3 parity)"**: a census step, 3 foundation packages, 20 Tier A packages (data the office database already holds), a switch gate, and 20 Tier B packages. Roughly 255 dev-days up to the switch gate and 463 dev-days for all of M1, excluding vendor work.
- **M2, "26.1 parity"**: 3 foundation packages, 30 feature packages (3 of them dental-school packages that are gated by the census), and a validation gate. Roughly 268 dev-days, of which 44 are dental school.
- **Vendor-direct integrations**: 10 packages, kept in their own list, roughly 104 dev-days. Each one has a slot in a wave so it never collides with desktop packages.
- **Parallelism**: inside a wave, packages own disjoint files, so they can be built at the same time on separate branches. M1 Tier A has 2 waves (17 and 5 packages including vendor slots), Tier B has 3 waves.
- **Biggest risks**: the version check that would pull Open Dental binaries out of the office database (R1), and proving the adopted data is intact (R2).

## Owner decisions (2026-10-18)

These override anything below that conflicts with them.

- **Dental-school features: excluded.** Drop M2-SCH1–M2-SCH3, plus the dental-school parts of A-SCH and M1 Tier B. The tables stay for schema compatibility.
- **Intra-office chat: excluded.** Drop A-CHAT and M2-CHAT. The chat tables stay; existing chat history isn't shown.
- **`patient._portal_active_email`: dropped during adoption.** It is an office-specific customization, not Open Dental schema. Remove the column and its unique index in the adoption step, so family members can share an e-mail again.
- **Public sources only.** No customer-only manual pages, and no observing the licensed Open Dental client. Inferred behaviors stay marked, and are settled by the owner's testing.
- **No office data.** M1-00 (census) and the M1-Z rehearsal use a synthetic-data generator over the real 25.3.48 schema (`od_253_schema`), never a copy of the office database. Enum numbers and preference names come from Open Dental's published database docs and the schema, not from a census.
- **Local REST API: dropped.** The 98 API items stay excluded.
- **Vendor neutrality and local-only services** (ground rules 6–7 in [../decoupling-plan.md](../decoupling-plan.md)) apply to every package.

## How to use this backlog

1. **Packages and waves.** A package is a unit of work for one engineer or agent on one branch. Packages in the same wave own disjoint files and can run in parallel. A wave starts once the packages it depends on are merged. When a package merges, ownership of its files passes to whichever package needs them in a later wave. Each package lists two kinds of dependency: "Depends on" (it needs that work) and "takes files over from" (ordering only: it edits a file that package edited earlier). If a package it takes files over from is deferred or dropped, for example a vendor package the office does not need, that constraint disappears.
2. **What "owns" means.** Only the owner edits the file while its wave runs. Calling public methods of a file owned by someone else is fine. A form's designer and resource files go with the form.
3. **Foundation-owned files.** The foundation packages (M1-01 to M1-03, M2-01, M2-02) own table classes (`OpenDentBusiness/TableTypes/`), generated CRUD (`OpenDentBusiness/Crud/`), migration steps, enum-only files, `Pref.cs`, `PrefInfos.xml`, `GroupPermission.cs`, `GroupPermissions.cs`, `UserControlSecurityTree.cs`, `DisplayFields.cs` (M2 only) and seed rows. Feature packages read them and do not edit them. If a feature finds a missing registry entry, the registry owner adds it in a small follow-up commit between waves.
4. **New-file rule.** When a package needs a helper in a heavily shared file it does not own, it puts the helper in a new file instead. M1-02 makes the hot data classes `partial` so new methods can live in new partial files (for example `DatabaseMaintenances.ProcCodes.cs`). A data migration that belongs to a feature is a new step file under `OpenDentBusiness/Misc/ForkMigrations/`.
5. **Sizes.** Spec item sizes are counted as S = 1.5, M = 4, L = 10 and XL = 20 dev-days and summed per package. Package size: S up to 3 days, M up to 8, L under 20, XL 20 or more. Estimates include unit tests, not QA on the office copy or review.
6. **Tiers inside M1.** Tier A holds every 24.4 to 25.3 feature whose data can already be in a 25.3 database: it must work before the office switches. Tier B holds the rest. The census (M1-00) can move a few items between tiers; the rules are in the Tier A section.
7. **IDs.** `M1-0x`/`M2-0x` are foundation packages, `A-*` and `B-*` are M1 Tier A and Tier B, `M2-*` are M2 feature packages, `V-*` are vendor-direct. Waves: `F1`/`F2` foundation, `A1`–`A2`, `B1`–`B3`, `C1`–`C6`.

## Overview of waves

| Milestone | Wave | Packages that run in parallel (vendor slots in italics) | Starts after |
|---|---|---|---|
| M1 | step 0 | M1-00 | a restored copy of the office database |
| M1 | F1 | M1-01, M1-02, M1-03 | M1-00 results for names (M1-02); decoupling Phase 1 (M1-03) |
| M1 | A1 | A-EF1, A-FEE, A-PAY, A-ACCT, A-TASK, A-APPT, A-CL, A-RPT, A-QM, A-STMT, A-HASH, A-SIG, A-MED, A-CHAT, A-SCH, *V-PC*, *V-DXC1* | F1 |
| M1 | A2 | A-EF2, A-INS, A-IMG, A-SHEET, A-GN | A1 |
| M1 | gate | M1-Z | all Tier A packages and the vendor packages the office uses |
| M1 | B1 | B-ACCT, B-PAY, B-ERA, B-PERIO, B-MED, B-TASK, B-IMG, B-PROC, B-TP, B-WIKI, B-TIME, B-SERVER, B-EF, B-PRINT, B-AUDIT, *V-BRIDGE1* | A2 (can overlap M1-Z) |
| M1 | B2 | B-CLAIM, B-APPT, B-INSH, B-DBM, *V-PEARL1* | B1 |
| M1 | B3 | B-SHELL | B2 |
| M2 | F2 | M2-01, M2-02, M2-VERIFY | M1 complete; a 26.1 reference install |
| M2 | C1 | M2-EST, M2-SDV, M2-PAYW, M2-RECUR, M2-CAN, M2-INSV, M2-APPT, M2-TERM, M2-UDS, M2-CLAIMFORM, M2-SHEETS, M2-ERA, M2-ADJ, M2-CLAIMRCV, M2-EMP, M2-MED, M2-IMG, M2-MISC, M2-AUDIT, M2-CHAT, M2-SCH1, *V-DXC2*, *V-PEARL2* | F2 |
| M2 | C2 | M2-NOTES, M2-HIST, M2-EF, M2-DUN, *V-GP*, *V-EDS* | C1 |
| M2 | C3 | M2-INSPLAN, M2-CARRIER, *V-BRIDGE2* | C2 |
| M2 | C4 | M2-SHELL, *V-PAYSUITE* | C3 |
| M2 | C5 | M2-SCH2 | C4 |
| M2 | C6 | M2-SCH3 | C5 |
| M2 | gate | M2-Z | all M2 packages |

## Milestone M1: Adopt the office database (OD 25.3 parity)

Goal: the fork opens a restored copy of the office database (Open Dental 25.3.48, 422 tables), migrates it, and every piece of data in it is read, shown and written correctly. Then the office switches. The order is: census, schema package and its two companions, Tier A, switch gate, Tier B.

### M1 step 0: office data census (M1-00)

- **Size:** S (~3 dev-days) · **Wave:** step 0 · **Depends on:** a restored copy of `fox-db` on a local MariaDB
- **What:** read-only SQL against the copy. The output holds counts, key names and enum values only, never patient data.
  - Preference rows whose names are not in our `PrefName` enum, with whether each differs from its default. This settles every "proposed name" in the specs.
  - `displayreport.InternalName`, `program.ProgName`, `claimformitem.FieldName` values and bracketed static-text tokens in `sheetfielddef.FieldValue` that our code does not know.
  - Distinct values of every enum column the specs touch: `grouppermission.PermType` and `securitylog.PermType`, `alertitem.Type`, `eform.Status`, `eformfielddef.FieldType`, `sheetdef.SheetType`, `displayfield.Category`, `etrans.Etype`, `pearlrequest.RequestStatus`, `guardian.Relationship`, `document.ChartLetterStatus`.
  - Usage counts for new tables and columns: dated fee rows, mirrored appointments, benefit rows with a tooth range, group notes with no or non-complete procedures, chart letters, `inspending`, chat, dental-school tables, invoices, stored cards and terminals, DentalXChange clearinghouses.
  - Row counts and data sizes of the largest tables, to time the migration.
  - Whether anything writes `patient._portal_active_email` (see R2).
- **Decides:** the real names M1-02 registers; whether A-CHAT and A-SCH stay in M1; which Tier B items move up (below); which vendor packages are needed before the switch.
- **Test approach:** the scripts run twice on two restores and give the same output; a reviewer checks that the output contains no patient data.

### M1 foundation (wave F1)

M1-01 is the schema-migration package and comes first. M1-02 and M1-03 own different files and run beside it. Tier A starts when all three are merged.

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M1-01 | Schema migration to Open Dental 25.3.48 | L | 4 | – |
| M1-02 | Registries at the 25.3 level: enums, preferences, permissions, seed rows | M | 3 | M1-00 |
| M1-03 | Version guard and adoption entry point | M | 0 | decoupling Phase 1 |

#### M1-01: Schema migration to Open Dental 25.3.48

- **Size:** L (~10 dev-days) · **Wave:** F1 · **Depends on:** none inside this backlog
- **Features:** [`v25_2-core-schema-enum-sync`](https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml) (M), [`v25_3-schema-compat-columns`](https://www.opendental.com/OpenDentalDiffDocumentation25-3.xml) (S), [`v25_3-school-approval-schema`](https://www.opendental.com/site/version25_4.html) (S), [`v25_1-family-guardian-relationship-renumber`](https://www.opendental.com/OpenDentalDocumentation25-1.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/TableTypes/`: Appointment, ApptView, AsapComm, Benefit, Branding (new), Chat (new), ChatAttach (new), ChatMsg (new), ChatReaction (new), ChatUserAttach (new), ChatUserod, ClaimPayment, Clearinghouse, CodeGroup, ComputerPref, CreditCard, Document, EClipboardSheetDef, EFormField, EFormFieldDef, EmailMessage, EobAttach, Fee, FeeSchedNote (new), Guardian, HistAppointment, ImageDraw, InsPending (new), InsPlan, InsSub, InsVerify, InsVerifyHist, JournalEntry, Medication, PayPlan, PayPlanCharge, PayPlanTemplate, QueryFilter (new), ReqNeeded, ReqStudent, SchoolApproval (new), SchoolCourse, SchoolCourseDef, SchoolCourseSched (new), SequenceCounter (new), SheetField, Statement, Task, TaskHist, Transaction
  - OpenDentBusiness/Misc/ForkMigrations/OdParity25_3.cs (new; the 24.4 to 25.3.48 steps)
  - OpenDentBusiness/Crud/*Crud.cs for every changed or new table (regenerated with xCrudGenerator, never hand-edited)
  - DocumentationBuilder/ (only if we keep publishing schema docs)
  - OpenDentBusiness/Misc/ForkMigrations/SchemaVerifier.cs (new) plus a reference DDL snapshot of the target Open Dental schema (new)
- **Test approach:** (1) Upgrade an empty 24.3.63 database and diff it against fox-db-schema.sql with the schema verifier: tables, columns, types, NULL/default, indexes, engine and charset must match, except the documented exceptions. (2) Run the migration twice: the second run changes nothing. (3) Run it on a restored copy of the office database: only the documented drift fixes may change anything, row counts stay equal, and the guardian renumbering and back-fills do not run. (4) CRUD round-trip unit tests (UnitTests/UnitTests) for every new table and column.
- **Notes:** Scope, from schema-changes.json entries with range 24.3-25.3: 14 new tables (feeschednote, queryfilter, branding, inspending, chat, chatmsg, chatattach, chatreaction, chatuserattach, chatuserod, sequencecounter, schoolapproval, schoolcoursedef, schoolcoursesched); 44 new columns; 9 type changes; the task and taskhist TriageCategory to Category rename (same type and position, keep the old index name `TriageCategory`); the indexes the dump shows on new columns (for example `FeeSchedCodeClinicProvDate`, `ClaimNumPreAuth`, `UserSigned`).
  - `claimpayment.Note` stays `varchar(255) DEFAULT ''`, because that is what 25.3.48 has. It is widened to text in M2-01.
  - Close the documented index gap: compare the KEY lines of a 24.3.63-created database with the dump and fix any differences on existing columns here.
  - Not Open Dental schema: `patient._portal_active_email` (a generated column with a unique key). The verifier lists it as a known exception; the owner decides whether adoption drops it (see Risks).
  - `deletedobject` already exists in 24.3 (the extractor missed it). The 54 HQ-only tables are never created in an office database; the verifier ignores them.
  - Enum values that live in changed table classes are done here: `EnumEFormFieldType` 8 and 9, `StatementMode.API=4`, `EmailMessageSource.EClipboardWeb` (stored as text), `EnumCategoryBetterDiag` and `EnumImageAnnotVendor.BetterDiagnostics=2` (imagedraw), and the `GuardianRelationship` renumbering with its data migration.
  - Every step is idempotent. Data migrations and back-fills (guardian renumbering, `statement.ShowTransSinceBalZero` from the preference) run only when the step itself just created the column, never on a database that Open Dental already converted.
  - Owns the fork migration runner and verifier. M1-03 calls them from startup and from the adoption tool. The runner finds steps by naming convention or attribute in separate files, so a feature package that needs a data migration adds its own step file without editing a shared one.

#### M1-02: Registries at the 25.3 level: enums, preferences, permissions, seed rows

- **Size:** M (~5–8 dev-days) · **Wave:** F1 · **Depends on:** M1-00
- **Features:** [`v25_1-schema-enum-compat`](https://www.opendental.com/OpenDentalDiffDocumentation25-1.xml) (S), [`v25_2-security-permission-renames`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-security-claimproc-billedtoins-desc`](https://www.opendental.com/site/version25_3.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/TableTypes/`: AlertItem, ApptReminderRule, Commlog, Def, DisplayField, EForm, EServiceLog, EServiceSignal, Etrans, GroupPermission, InsBlueBookRule, PearlRequest, PerioMeasure, Pref, Program, SessionToken, SmsToMobile, UserOdPref
  - `OpenDentBusiness/Data Interface/`: Appointments, ClaimProcs, DatabaseMaintenances, GroupPermissions, Patients, Procedures, Statements
  - `OpenDentBusiness/Db/`: Enumerations
  - `OpenDental/Forms/`: FormAudit
  - `OpenDental/Resources/`: PrefInfos.xml
  - `OpenDentBusiness/SheetFramework/`: SheetEnums
  - `OpenDental/User Controls/`: UserControlSecurityTree
  - OpenDentBusiness/Misc/ForkMigrations/Seed25_3.cs (new; preference, permission-grant, displayreport and program rows for databases that come from 24.3)
  - Limit: In the six hot data-class files, only the class declaration line.
- **Test approach:** Unit tests that pin every reserved enum value to its published number (a table-driven test per enum). A test that every preference name found by the census exists in `PrefName`. Security tree test: reserved and HQ-only permissions are hidden; new permissions appear under the right node. Seed rows are inserted only on databases that come from 24.3.
- **Notes:** Adds, with the exact published numbers: `EnumPermType` 264 to 272 (268, 269 and 272 reserved and hidden), `AlertType` 41 to 45, `FormType` 19 and 20, `CommItemSource` 10, `EtransType` 36 and 37, `InvalidType` 116, `EnumEFormStatus` removals (load 3 and 4 as Filled), `SheetTypeEnum.Check=28`, `DisplayFieldCategory` 22 and 23, `InsBlueBookRuleType` 5 and `SchedStatus` 1 renames, `UserOdFkeyType` 28, `EnumPearlStatus` 5 to 7, `PerioSequenceType.AttGing=8`, and the reserve-only values for cut services (`ApptReminderType` 11, `SmsMessageSource` 29, `SessionTokenType` 5, the eService type, action and code numbers).
  - Preference names: one `PrefName` entry and one PrefInfos entry per 24.4 to 25.3 preference, using the real names from the census (M1-00), so the office settings carry over.
  - Permission display text for the 25.2 and 25.3 wording changes (items v25_2-security-permission-renames and v25_3-security-claimproc-billedtoins-desc), plus the descriptions used by the enforcement items in A-SIG, B-CLAIM and B-APPT.
  - Seed rows for databases that come from 24.3: upgrade grants (PayPlanUnlock to PayPlanEdit holders, BlockoutsFlagged to every group, and the others the specs give), `displayreport` rows for the two new reports, `program` rows for new bridges, all with the office's real key names.
  - Makes the hot data classes `partial` (`DatabaseMaintenances`, `Procedures`, `ClaimProcs`, `Patients`, `Appointments`, `Statements`) so later packages can add new methods in new files. This is a one-line change per class and changes no behavior.

#### M1-03: Version guard and adoption entry point

- **Size:** M (~5–8 dev-days) · **Wave:** F1 · **Depends on:** decoupling Phase 1
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Misc/`: ClassConvertDatabase
  - `OpenDentBusiness/Misc/`: ConvertDatabases
  - `OpenDentBusiness/TableTypes/`: DocumentMisc
  - `OpenDentBusiness/Data Interface/`: DocumentMiscs
  - `OpenDental/Main Modules/`: FormOpenDental
  - `OpenDental/Logic/`: PrefL
  - OpenDentBusiness/Misc/ForkMigrations/Adoption.cs and OpenDental/Forms/FormAdoptDatabase.cs (both new)
  - UpdateFileCopier/ (project)
  - Limit: OpenDental/Main Modules/FormOpenDental.cs is limited to the startup path (`PrefsStartup` near line 1520 and `IsDbConnectionSafe` near line 3893).
- **Test approach:** Startup matrix on scratch databases: a fresh 24.3.63 database, a restored office copy (25.3.48), a copy edited to 25.3.50 and to 25.4.0, an adopted copy, and an adopted copy whose fork markers were removed. For each: assert that conversion runs or refuses as specified, that no `documentmisc` UpdateFiles row is read, and that no "downgrade" prompt appears. Repeat in a Release build, because Open Dental skips the version check in Debug builds.
- **Notes:** Implements the version strategy in Risk R1: fork-owned version and schema markers, an allowlist of adoptable Open Dental versions (24.3.63 and 25.3.48 in M1), sentinel values in `ProgramVersion` and `DataBaseVersion` after adoption, and fork-only `DocumentMiscType` values for our own update files.
  - Freezes the legacy `ConvertDatabases` chain at 24.3.63 and hands off to the fork migration runner from M1-01.
  - The adoption tool is explicit: an administrator runs it against a restored copy. Normal startup never adopts an Open Dental database on its own.
  - Coordinate with decoupling Phase 1, which also edits `PrefL.cs` (removing the HQ update check and the service, eConnector and API installers). This package starts after Phase 1 merges or rebases onto it.

### M1 Tier A: features whose data the office database already holds

**Rule.** A feature is in Tier A when an Open Dental 25.3.48 database can contain rows, columns, enum values or string keys that the feature reads or writes, and our 24.3 code would ignore, misread or overwrite them. Riders are included when a Tier A feature cannot work without them (for example audit detail for mirroring, next check number for check printing, the 25.1 change-user button for the 25.2 signing user).

**Census adjustments (made once, after M1-00):**

- **Gated down:** A-CHAT and A-SCH stay in Tier A only if the office has rows in `chatmsg` or in the dental-school tables. Otherwise they move to M2 (A-SCH merges into M2-SCH1). Their tables exist either way, from M1-01.
- **Promoted up:** a Tier B item moves into Tier A when the census shows the office relies on its stored setting. Candidates: v24_4-account-salestax-proc (sales-tax procedure code), v25_1-manage-era-autosplit-carc and v25_2-manage-era-carc-logic (CARC settings), v25_2-account-medical-eob-prompt, v25_3-account-payplan-bypass-globallock, v25_2-server-replication-delay, v25_3-manage-statement-barcode, v24_4-chart-perio-recession-term. A promoted item keeps its package and runs that package early.
- **Vendor:** V-PC and V-DXC1 are needed before the switch only if the office uses PayConnect or DentalXChange.

#### Wave A1

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| A-EF1 | eForms on the desktop (core) | L | 4 | M1-01, M1-02, M1-03 |
| A-FEE | Fee effective dates and fee schedule notes | XL | 2 | M1-01; files from M1-02 |
| A-PAY | Payment plans: sheet choice, unlock permission, down-payment flag | M | 3 | M1-01, M1-02 |
| A-ACCT | Accounting transactions: payee, notes, review flag, next check number | M | 3 | M1-01 |
| A-TASK | Task categories for all offices | M | 1 | M1-01, M1-02 |
| A-APPT | Appointment mirroring, appointment audit detail, flagged-blockout permission | L | 3 | M1-01, M1-02 |
| A-CL | Chart letters | XL | 1 | M1-01 |
| A-RPT | New reports whose rows already exist in the office database | L | 3 | M1-02 |
| A-QM | Query Monitor filter groups | M | 1 | M1-01 |
| A-STMT | Statement and invoice fields | S | 2 | M1-01; files from M1-02 |
| A-HASH | Row-integrity hashes | S | 2 | M1-01; files from M1-02 |
| A-SIG | Signatures: change user, domain login, TP fee permission | L | 3 | M1-01, M1-02 |
| A-MED | Hidden medications | S | 1 | M1-01 |
| A-CHAT | Intra-office chat (gated by the census) | XL | 1 | M1-01 |
| A-SCH | Dental school course scheduling data layer (gated by the census) | M | 1 | M1-01 |
| V-PC | PayConnect 2 (direct) | M | 4 | M1-01 |
| V-DXC1 | DentalXChange attachments and XConnect validation | L | 2 | M1-01, M1-02 |

#### Wave A2

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| A-EF2 | eForm Patient List and Ins Pick fields, pending insurance | XL | 2 | A-EF1, A-HASH |
| A-INS | Code groups, tooth-range exclusions, Other Benefits grid | L | 4 | A-FEE |
| A-IMG | Imaging signature permissions and preauthorization EOBs | M | 2 | A-CL; files from V-DXC1 |
| A-SHEET | Check printing, secondary-insurance static text, hospitalization dates | L | 3 | A-ACCT, A-SIG; files from V-DXC1 |
| A-GN | Flexible group notes | M | 1 | A-CL, A-SIG |

#### A-EF1: eForms on the desktop (core)

- **Size:** L (~13.5 dev-days) · **Wave:** A1 · **Depends on:** M1-01, M1-02, M1-03
- **Features:** [`v24_4-eforms-desktop-forms`](https://www.opendental.com/site/version24_4.html) (M), [`v25_2-eforms-remove-imported-status`](https://www.opendental.com/site/version25_2.html) (S), [`v25_1-eforms-prefill-from-guarantor`](https://www.opendental.com/site/version25_1.html) (M), [`v25_1-eforms-auto-import-fields`](https://www.opendental.com/site/version25_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `WpfControlsOD/UI/Controls/UserControls/`: CtrlEFormFill
  - `OpenDentBusiness/Data Interface/`: EFormDefs, EFormFieldDefs, EFormFields, EForms
  - `OpenDentBusiness/EForms/`: EFormFieldsAvailable, EFormFiller, EFormImport, EFormInternal
  - `OpenDental/Forms/`: FormEServicesEClipboard, FormPatientForms, FormSheetImport
  - `OpenDental/Main Modules/`: FormOpenDental, FormOpenDentalMenus
  - `WpfControlsOD/Frms/`: FrmEFormCheckBoxEdit, FrmEFormDateFieldEdit, FrmEFormDefEdit, FrmEFormDefs, FrmEFormFieldPicker, FrmEFormFillEdit, FrmEFormImportRules, FrmEFormLabelEdit, FrmEFormMedicationListEdit, FrmEFormPicker, FrmEFormRadioButtonsEdit, FrmEFormSetup, FrmEFormSigBoxEdit, FrmEFormTextBoxEdit
  - Limit: In `FormOpenDental.cs` and `FormOpenDentalMenus.cs`, only the eForms menu entries and their Release-build gate.
- **Test approach:** Unit tests: prefill from the guarantor only when the patient value is empty; AutoImport writes only flagged fields; statuses 3 and 4 load as Filled; English copies saved on insert. UI script on the office copy: open eForms that eClipboard filled, import one into a test patient, check required fields on Save, check that the hidden-medication filter applies in the medication list editor.
- **Notes:** Turns on the eForms code that 24.3 hides in Release builds and finishes the in-office fill path (our decision, since eClipboard is cut). Also does the eForm translation columns from v25_2-core-schema-enum-sync (copy label and pick list into the English columns) and applies the hidden-medication filter in `FrmEFormMedicationListEdit` for A-MED.

#### A-FEE: Fee effective dates and fee schedule notes

- **Size:** XL (~24 dev-days) · **Wave:** A1 · **Depends on:** M1-01; **takes files over from:** M1-02
- **Features:** [`v24_4-setup-fee-effective-date`](https://www.opendental.com/site/version24_4.html) (XL), [`v24_4-setup-feesched-notes`](https://www.opendental.com/site/version24_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ClaimProcs, FeeSchedGroups, FeeSchedNotes (new), FeeScheds, Fees, Procedures
  - `OpenDentBusiness/Cache/`: FeeCache
  - `OpenDental/Forms/`: FormFeeSchedPickRemote, FormFeeSchedTools, FormFeeScheds, FormProcCodes2
  - `WpfControlsOD/Frms/`: FrmFeeSchedEdit, FrmFeeSchedNoteEdit (new), FrmFeeSchedNotes (new)
- **Test approach:** Extend UnitTests/UnitTests/FeesTests.cs with a lookup matrix: as-of date by clinic and provider overrides, a 0001-01-01 row versus dated rows, two rows with the same key and date rejected, group sync copying the date. Cache tests for the "current" index. On the office copy: for a sample of codes and schedules, fees "today" match what Open Dental 25.3.48 shows, and future-dated rows do not appear before their date.
- **Notes:** Long pole of M1 (XL). The date-aware lookup lives in `Fees.cs` and `FeeCache.cs`; `Procedures.cs` and `ClaimProcs.cs` only get call-site changes that pass the as-of date. FormFeeSchedPickRemote is changed only if the decoupling keeps it.

#### A-PAY: Payment plans: sheet choice, unlock permission, down-payment flag

- **Size:** M (~7 dev-days) · **Wave:** A1 · **Depends on:** M1-01, M1-02
- **Features:** [`v24_4-account-payplan-sheet-select`](https://www.opendental.com/site/version24_4.html) (M), [`v25_1-account-payplan-unlock-permission`](https://www.opendental.com/site/version25_1.html) (S), [`v25_1-account-payplan-downpayment-flag`](https://www.opendental.com/OpenDentalDocumentation25-1.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormPayPlan, FormPayPlanDynamic, FormPayPlanTemplateEdit
  - `OpenDentBusiness/Data Interface/`: PayPlanCharges, PayPlanTemplates, SheetDefs
  - `OpenDentBusiness/Db Multi Table/`: PayPlanEdit
  - `OpenDental/Logic/`: PrintRemoteRequestL
- **Test approach:** PayPlansTests and PayPlanChargeTests: template sheet copied to new plans; deleted sheet def falls back to the default; down-payment charges carry the flag; unlock allowed only with PayPlanUnlock. Print office plans that have a SheetDefNum and compare with the chosen sheet.
- **Notes:** Permission values, tree entries and the upgrade grant come from M1-02; this package does the enforcement in the pay plan windows.

#### A-ACCT: Accounting transactions: payee, notes, review flag, next check number

- **Size:** M (~7 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v25_3-manage-accounting-payee`](https://www.opendental.com/site/version25_3.html) (M), [`v25_3-manage-accounting-needsreview`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-manage-accounting-checknum`](https://www.opendental.com/site/version25_3.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormJournal, FormJournalEntryEdit, FormTransactionEdit
  - `OpenDentBusiness/Data Interface/`: JournalEntries, Transactions
- **Test approach:** JournalEntries tests for the next check number per account. UI on the office copy: entries with Payee, Notes and NeedsReview display and save without losing the Memo text.
- **Notes:** Includes v25_3-manage-accounting-checknum (no data of its own) because check printing in A-SHEET needs it and it shares the same window.

#### A-TASK: Task categories for all offices

- **Size:** M (~4 dev-days) · **Wave:** A1 · **Depends on:** M1-01, M1-02
- **Features:** [`v25_2-manage-task-category`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Logic/`: DefL
  - `OpenDental/Forms/`: FormTaskEdit, FormTaskPreferences
  - `OpenDentBusiness/Data Interface/`: Tasks
  - `OpenDental/User Controls/`: UserControlTasks
- **Test approach:** TasksTests: the renamed column reads and writes; `TaskCategoryShow` on and off; office tasks keep their category; HQ triage position is never shown.
- **Notes:** The column rename itself is in M1-01. This package does the dropdown, the preference and the definition category text.

#### A-APPT: Appointment mirroring, appointment audit detail, flagged-blockout permission

- **Size:** L (~18 dev-days) · **Wave:** A1 · **Depends on:** M1-01, M1-02
- **Features:** [`v25_3-appts-audit-detail`](https://www.opendental.com/site/version25_3.html) (M), [`v25_3-appts-mirroring`](https://www.opendental.com/site/version25_3.html) (L), [`v25_1-appt-blockouts-flagged-permission`](https://www.opendental.com/site/version25_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Logic/`: AppointmentL
  - `OpenDentBusiness/Data Interface/`: Appointments, ApptViews, HistAppointments, Schedules
  - `OpenDental/Main Modules/`: ControlAppt
  - `OpenDental/UI/Appt/`: ControlApptPanel
  - `OpenDental/Forms/`: FormApptEdit, FormApptViewEdit, FormApptsOther, FormBlockoutCutCopyPaste, FormScheduleBlockEdit
  - `OpenDental/User Controls/Preferences/`: UserControlApptGeneral
- **Test approach:** Unit tests for the mirror calculation: which provider is secondary, which operatory, and the span from `PatternSecondary`, including split X runs. Audit text generator tests. Manual: views with and without `ShowMirroredAppts` on office appointments that have `IsMirrored`; mirrors cannot be dragged; the flagged-blockout permission matrix.
- **Notes:** v25_3-appts-audit-detail rides along because mirroring logs through it.

#### A-CL: Chart letters

- **Size:** XL (~20 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v24_4-chart-chart-letters`](https://www.opendental.com/site/version24_4.html) (XL)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Db Multi Table/`: ChartModules
  - `OpenDental/Main Modules/`: ControlChart, ControlImages
  - `OpenDentBusiness/Data Interface/`: Documents, LetterMerges
  - `OpenDental/Forms/`: FormImageFloat, FormLetterMergeEdit, FormLetterMerges
  - `WpfControlsOD/Frms/`: FrmChartLetterProperties (new), FrmDocSign
  - `OpenDentBusiness/Imaging/`: ImageStore
  - `OpenDentBusiness/Misc/`: LetterMergesQueries
  - OpenDentBusiness/Data Interface/ChartLetters.cs (new helper)
- **Test approach:** Documents tests: chart-letter rows are left out of the imaging tree and listed in progress notes; a changed hash writes a new version row. Manual Word round-trip on office chart letters, using the status values the census found.
- **Notes:** XL. Needs a Word editor integration and the existing letter-merge templates. Owns `ControlChart.cs` and `ControlImages.cs` in wave A1, so the other chart and imaging work waits for wave A2.

#### A-RPT: New reports whose rows already exist in the office database

- **Size:** L (~9.5 dev-days) · **Wave:** A1 · **Depends on:** M1-02
- **Features:** [`v24_4-reports-daily-procs-split`](https://www.opendental.com/site/version24_4.html) (M), [`v24_4-reports-individual-procs-goto-patient`](https://www.opendental.com/site/version24_4.html) (S), [`v25_2-reports-ins-pending-supplementals`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: DisplayReports
  - `OpenDental/Forms/`: FormReportsMore
  - `OpenDental/Forms Reports/`: FormRpInsPendingSupplementals (new), FormRpProcSheet, FormRpProcedures (new)
  - `OpenDentBusiness/Reports/`: RpInsPendingSupplementals (new), RpProcSheet
  - `OpenDental/User Controls/`: UserControlReportSetup
- **Test approach:** Golden numbers: run each report on the office copy for fixed date ranges and compare with the same report in Open Dental 25.3.48 (if the owner allows observing that install) or with hand-checked SQL. Existing report permissions (`grouppermission` rows keyed by `DisplayReportNum`) still apply.
- **Notes:** The `displayreport` internal names come from the census; seed rows for fresh databases come from M1-02.

#### A-QM: Query Monitor filter groups

- **Size:** M (~4 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v24_4-tools-querymonitor-filters`](https://www.opendental.com/site/version24_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormQueryMonitor
  - `WpfControlsOD/Frms/`: FrmQueryFilters (new)
  - `OpenDentBusiness/Data Interface/`: QueryFilters (new)
  - `DataConnectionBase/`: QueryMonitor
- **Test approach:** Unit tests for filter matching; UI check with the office's saved filters.

#### A-STMT: Statement and invoice fields

- **Size:** S (~3 dev-days) · **Wave:** A1 · **Depends on:** M1-01; **takes files over from:** M1-02
- **Features:** [`v25_1-account-invoice-adjustment-total`](https://www.opendental.com/OpenDentalDocumentation25-1.xml) (S), [`v25_2-account-statement-show-trans-since-zero`](https://www.opendental.com/OpenDentalDocumentation25-2.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Logic/`: BillingL
  - `OpenDental/Main Modules/`: ControlAccount
  - `OpenDental/Forms/`: FormInvoiceItemSelect, FormStatementOptions
  - `OpenDentBusiness/Data Interface/`: Statements
- **Test approach:** Statements tests: invoices store the adjustment total in `InsEst`; reprinting a saved statement honors its own ShowTransSinceBalZero value. Compare a sample of office invoices with their adjustments.
- **Notes:** Meaning-only changes on existing columns plus one new column.

#### A-HASH: Row-integrity hashes

- **Size:** S (~3 dev-days) · **Wave:** A1 · **Depends on:** M1-01; **takes files over from:** M1-02
- **Features:** [`v25_1-security-insverify-securityhash`](https://www.opendental.com/site/version25_1.html) (S), [`v25_1-misc-dbm-securityhash-rehash-patient`](https://www.opendental.com/site/version25_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: DatabaseMaintenances, InsSubs, InsVerifies, InsVerifyHists, Patients
  - `OpenDental/Forms/`: FormDatabaseMaintenance, FormInsVerificationList
  - `OpenDentBusiness/Misc/`: SecurityHash
- **Test approach:** Hash is written on insert and update of insverify, insverifyhist, inssub and patient rows; an out-of-band edit is detected; the rehash DBM tool covers the whole patient table.
- **Notes:** Depends on decoupling Phase 2 (in-repo HMAC-SHA256 replacing the proprietary hash). Open Dental's existing hashes cannot be checked by us, so adoption rehashes everything (see Risk R2).

#### A-SIG: Signatures: change user, domain login, TP fee permission

- **Size:** L (~9.5 dev-days) · **Wave:** A1 · **Depends on:** M1-01, M1-02
- **Features:** [`v25_1-sheets-signature-change-user`](https://www.opendental.com/site/version25_1.html) (M), [`v25_2-security-domain-login-change-user`](https://www.opendental.com/site/version25_2.html) (M), [`v25_2-security-tp-proc-edit-fee`](https://www.opendental.com/site/version25_2.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormLogOn, FormOrthoChart, FormProcEdit, FormProcEditAll, FormProcGroup, FormProcTPEdit, FormSheetFillEdit, FormTPsign
  - `OpenDentBusiness/Data Interface/`: PatientNotes, Userods
  - `OpenDentBusiness/SheetFramework/`: SheetDrawingJob
  - `OpenDental/SheetFramework/`: SheetPrinting
  - OpenDentBusiness/Misc/DomainLogin.cs (new shared helper; FormOpenDental switches to it in B-SHELL)
  - OpenDentBusiness/UI/SignatureBoxWrapper.cs, WpfControlsOD/UI/Controls/SignatureBoxWrapper.xaml.cs
- **Test approach:** Change-user on sheet signature boxes, group notes, TP signing and the ortho chart; `sheetfield.UserSigned` set only when the signer is a provider user; domain login through a mocked directory; Treatment Planned Procedure Edit Fee permission matrix in the procedure windows.
- **Notes:** v25_1-sheets-signature-change-user rides along because the 25.2 domain login and `UserSigned` build on it. The domain-login code goes into a new shared helper; the main window switches to it later in B-SHELL.

#### A-MED: Hidden medications

- **Size:** S (~1.5 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v25_1-misc-medication-hide`](https://www.opendental.com/site/version25_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormAllergyDefEdit, FormMedical, FormMedicationEdit, FormMedicationReconcile, FormMedications
  - `OpenDentBusiness/Data Interface/`: Medications
- **Test approach:** MedicationsTests: hidden medications are left out of pickers but still show on patients who already have them.
- **Notes:** The eForm medication list filter is done inside A-EF1.

#### A-CHAT: Intra-office chat (gated by the census)

- **Size:** XL (~20 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v25_3-chat-intraoffice-chat`](https://www.opendental.com/site/version25_3.html) (XL)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlManage
  - `OpenDentBusiness/Data Interface/`: Employees
  - `OpenDental/User Controls/Preferences/`: UserControlServerConnections
  - OpenDentBusiness/Data Interface/Chats.cs, ChatMsgs.cs, ChatAttaches.cs, ChatReactions.cs, ChatUserAttaches.cs, ChatUserods.cs, SequenceCounters.cs (new)
  - OpenDentalChat/ (new WPF executable project) and its entry in OpenDental.sln
- **Test approach:** Two workstations against the office copy: send, quote, react, attach, mute, leave; new messages are fetched by `SeqCount`; office chat history is readable.
- **Notes:** Gate: if the census finds no rows in `chatmsg`, this package moves to M2 (the tables still exist from M1-01). Shipping a separate chat program is a product decision that the specs leave open.

#### A-SCH: Dental school course scheduling data layer (gated by the census)

- **Size:** M (~4 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v25_3-school-course-scheduling`](https://www.opendental.com/site/version25_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormDentalSchoolSetup, FormEvaluationDefEdit, FormReqNeededEdit, FormReqNeededs, FormReqStudentEdit, FormSchoolCourseEdit, FormSchoolCourses
  - `OpenDentBusiness/Data Interface/`: ReqNeededs, ReqStudents, SchoolCourseDefs (new), SchoolCourseScheds (new), SchoolCourses
- **Test approach:** Unit tests for course definitions, weekly slots and overrides.
- **Notes:** Gate: the office is a private practice, so the census will most likely show no school rows; then this package moves to M2 and merges with M2-SCH1. The schema is in M1-01 either way.

#### A-EF2: eForm Patient List and Ins Pick fields, pending insurance

- **Size:** XL (~30 dev-days) · **Wave:** A2 · **Depends on:** A-EF1, A-HASH
- **Features:** [`v25_2-eforms-patient-list-field`](https://www.opendental.com/site/version25_2.html) (L), [`v25_2-eforms-ins-pick-field`](https://www.opendental.com/site/version25_2.html) (XL)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlFamily
  - `WpfControlsOD/UI/Controls/UserControls/`: CtrlEFormFill
  - `OpenDentBusiness/Data Interface/`: DatabaseMaintenances, EFormFields, EForms, InsPendings (new), Patients
  - `OpenDentBusiness/EForms/`: EFormFieldsAvailable, EFormFiller, EFormImport
  - `OpenDental/Forms/`: FormInsPlan
  - `WpfControlsOD/Frms/`: FrmEFormDefEdit, FrmEFormFieldPicker, FrmEFormImportRules, FrmEFormInsPickEdit (new), FrmEFormPatientListEdit (new), FrmInsPendingReview (new)
- **Test approach:** Render, fill and import both field types; inspending rows from the office copy show up for review and turn into real insplan, inssub and patplan rows; patient merge and delete move or remove inspending rows; a DBM check finds orphans.
- **Notes:** Second eForms wave; runs after A-EF1 and after A-HASH, which owns `Patients.cs` and `DatabaseMaintenances.cs` in wave A1.

#### A-INS: Code groups, tooth-range exclusions, Other Benefits grid

- **Size:** L (~16 dev-days) · **Wave:** A2 · **Depends on:** A-FEE
- **Features:** [`v24_4-setup-codegroup-ranges`](https://www.opendental.com/site/version24_4.html) (M), [`v25_1-family-codegroups-hide-delete`](https://www.opendental.com/site/version25_1.html) (M), [`v25_3-family-exclusions-tooth`](https://www.opendental.com/site/version25_3.html) (M), [`v25_3-family-otherbenefits-grid`](https://www.opendental.com/site/version25_3.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Benefits, ClaimProcs, CodeGroups, ProcedureCodes, Procedures
  - `OpenDental/Forms/`: FormBenefitEdit, FormBenefitFrequencies, FormCodeGroupEdit, FormCodeGroups, FormInsBenefits
  - `OpenDentBusiness/UI/`: GridOD
  - `OpenDentBusiness/Logic/`: Tooth
- **Test approach:** CodeGroups range parser tests (single codes, ranges like D2391-D2394, mixed lists); `IsExcluded` with tooth ranges; BenefitsTest and ClaimProcsTests estimate cases; the frequency and Other Benefits grids show the office's code groups as their flags say.
- **Notes:** Runs after A-FEE because both change `ClaimProcs.cs` and `Procedures.cs`. Editable and combo cells in `GridOD.cs` are added here.

#### A-IMG: Imaging signature permissions and preauthorization EOBs

- **Size:** M (~8 dev-days) · **Wave:** A2 · **Depends on:** A-CL; **takes files over from:** V-DXC1
- **Features:** [`v24_4-imaging-signature-permissions`](https://www.opendental.com/site/version24_4.html) (M), [`v25_2-account-preauth-eob-attach`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/User Controls/`: ControlImageDisplay
  - `OpenDental/Main Modules/`: ControlImages, ControlImagesOld
  - `OpenDentBusiness/Data Interface/`: EobAttaches
  - `OpenDental/Forms/`: FormClaimEdit, FormClaimPayPreAuth, FormImageFloat, FormImages
  - `WpfControlsOD/Frms/`: FrmDocInfo, FrmDocSign
  - `OpenDentBusiness/Imaging/`: ImageStore
- **Test approach:** Permission matrix for signing images and editing signed images; attach, view and delete an EOB on a preauthorization; office EOBs with `ClaimNumPreAuth` show on the right claim.
- **Notes:** Runs after A-CL, which owns the imaging files in wave A1.

#### A-SHEET: Check printing, secondary-insurance static text, hospitalization dates

- **Size:** L (~13 dev-days) · **Wave:** A2 · **Depends on:** A-ACCT, A-SIG; **takes files over from:** V-DXC1
- **Features:** [`v25_3-manage-accounting-printcheck`](https://www.opendental.com/site/version25_3.html) (L), [`v24_4-sheets-secondary-ins-statictext`](https://www.opendental.com/site/version24_4.html) (S), [`v24_4-claims-medical-hospitalization-dates`](https://www.opendental.com/site/version24_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Accounts, EhrPatients, Sheets
  - `OpenDentBusiness/Resources/ClaimForms/`: ClaimForm1500_02_12.xml
  - `OpenDentBusiness/SheetFramework/`: EnumStaticTextField, SheetFieldsAvailable, SheetFiller, SheetParameter, SheetsInternal, StaticTextData
  - `OpenDental/Forms/`: FormClaimFormEdit, FormClaimFormItemEdit, FormClaimPrint, FormSheetDef, FormSheetDefEdit, FormSheetDefs, FormSheetFieldStatic, FormTransactionEdit
  - `OpenDentBusiness/Resources/`: InternalCheck.xml (new)
  - `OpenDentBusiness/Properties/`: Resources.resx
  - `OpenDental/SheetFramework/`: SheetPrinting
- **Test approach:** Print a check from an accounting transaction to PDF; office sheets of type Check render; custom sheets with the new secondary-insurance tokens fill; the new claim-form fields print on the 1500 form.
- **Notes:** Runs after A-ACCT (transaction window) and A-SIG (`SheetPrinting.cs`).

#### A-GN: Flexible group notes

- **Size:** M (~4 dev-days) · **Wave:** A2 · **Depends on:** A-CL, A-SIG
- **Features:** [`v25_1-chart-group-note-flexible`](https://www.opendental.com/site/version25_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Db Multi Table/`: ChartModules
  - `OpenDental/Main Modules/`: ControlChart
  - `OpenDental/Forms/`: FormProcGroup
  - `OpenDentBusiness/Data Interface/`: ProcGroupItems
  - OpenDentBusiness/Data Interface/GroupNotes.cs (new helper, so Procedures.cs is not touched)
- **Test approach:** Create group notes with no procedures and with mixed statuses; office group notes that 24.3 rules would reject open, edit and sort correctly; tooth-filter visibility.
- **Notes:** Data shape exists in the office database (group notes with zero or non-complete procedures), so this is Tier A. New helpers go into a new file so `Procedures.cs` is not touched.

### M1 switch gate (M1-Z): adoption rehearsal and cutover

- **Status (2026-10-24):** rehearsed on synthetic data (owner decision: no office data). Tooling and integration tests in [`tools/AdoptionValidation/`](../../tools/AdoptionValidation/README.md), procedure in [`docs/adoption-runbook.md`](../adoption-runbook.md), results for the owner's sign-off in [`docs/m1-validation-report.md`](../m1-validation-report.md). Revised after review (r1): runbook steps 3 to 6.1 run the rehearsal's own code without starting the program (`AdoptionValidation adopt`) and were dry-run with the runbook's commands; backups are restored only as new databases, never from a file that names a database; workstations are removed and the original is checked before go-live and before it is dropped. Revised after the second review (r2): the runbook is written for the office's Ubuntu database server, with the tools on a Windows admin PC through an SSH forward (step 0.0); the copy is made by the server and the rollback point is a MyISAM folder copy, both exact where a dump rounds FLOAT values; `adopt` checks row by row what it changed; the rollback removes this program and drops the adopted database; the report's claims match the evidence.
- **Size:** L (~10 dev-days, plus office time) · **Depends on:** M1-00, M1-01 to M1-03, every Tier A package, and the vendor packages the office uses
- **Owns:** the adoption runbook (new, for example `docs/adoption-runbook.md`) and the validation tooling (new, for example `tools/AdoptionValidation/`), plus integration tests under `UnitTests/`.
- **Work:**
  1. Rehearse adoption on fresh restores until it runs clean twice in a row: preflight, schema check, migration, rehash, sentinel and fork markers (R1), validation suite (R2).
  2. Golden comparisons: before the cutover, capture totals from Open Dental 25.3.48 for fixed dates (aging, production and income, insurance estimates for sample treatment plans, payment-plan balances, appointment counts) and compare with the fork on the migrated copy.
  3. Time each step on a full-size copy and plan the outage window. MyISAM has no transactions, so the runbook must start from a fresh backup.
  4. Make the list of bridge passwords to re-enter (decoupling plan ground rule 3) and the rehash record.
  5. Before adoption, stop and uninstall the Open Dental services on the office server (Open Dental Service, eConnector, API service and any other Open Dental scheduled job). They connect to the same database, talk to Open Dental HQ, and would keep writing.
  6. Cutover: all workstations switch together, because the adopted database locks out Open Dental clients (R1). Rollback means restoring the pre-adoption backup; changes made in the fork after cutover would be re-entered by hand, so keep the rollback window short and decide it in advance.
- **Test approach:** the rehearsal itself; a signed checklist per run.
- **Exit:** the owner signs off on the validation report. Tier B can continue after the switch and ship through the fork's own update channel.

### M1 Tier B: the remaining 24.4 to 25.3 desktop features

Tier B can start as soon as wave A2 is merged and can overlap the switch gate. B-SHELL comes last because it owns the main window and all main module controls together.

#### Wave B1

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| B-ACCT | Account module and sales tax | L | 6 | –; files from V-DXC1, A-STMT, A-INS |
| B-PAY | Payment plans and paysplits (remaining) | L | 6 | –; files from M1-02, A-PAY, V-PC, A-EF2 |
| B-ERA | ERA processing and ERA display fields | L | 4 | – |
| B-PERIO | Perio chart | L | 4 | – |
| B-MED | Medical lists and vital signs | L | 5 | –; files from A-MED |
| B-TASK | Task filters | S | 2 | –; files from A-TASK |
| B-IMG | Imaging tools | M | 3 | –; files from A-IMG |
| B-PROC | Procedure codes, code tools and fee tools | L | 6 | –; files from A-FEE, A-INS |
| B-TP | Treatment plan: add procedures to the active plan | M | 1 | –; files from A-FEE |
| B-WIKI | Wiki | L | 3 | – |
| B-TIME | Time clock and time cards | M | 2 | –; files from A-CHAT |
| B-SERVER | Server connections, replication and report setup | L | 4 | –; files from A-CHAT |
| B-EF | eForm designer and Patient Forms window | M | 2 | –; files from A-EF1, A-EF2 |
| B-PRINT | Statements, sheets, printing and grid export | L | 7 | –; files from A-ACCT, A-SIG, A-STMT, A-SHEET, A-INS |
| B-AUDIT | Patient audit-trail entries | S | 2 | –; files from A-EF2 |
| V-BRIDGE1 | Overjet, DTX Studio, Insurance Answers Plus | M | 3 | –; files from A-EF2 |

#### Wave B2

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| B-CLAIM | Claims, Canadian attachments, claim-procedure audit and permissions | L | 7 | B-ERA, B-ACCT; files from V-DXC1, A-INS, A-IMG, B-TP |
| B-APPT | Appointments module and appointment permissions | L | 7 | B-TASK; files from A-APPT, A-GN |
| B-INSH | Insurance history, benefit editing and plan list | L | 5 | –; files from A-EF2, A-INS, B-ACCT |
| B-DBM | Database maintenance tools | L | 6 | –; files from A-HASH, A-IMG, B-PAY |
| V-PEARL1 | Pearl request statuses | S | 1 | –; files from B-IMG |

#### Wave B3

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| B-SHELL | Main window and cross-module integration | L | 6 | B-ACCT, B-APPT, B-TP, B-INSH, B-IMG, B-CLAIM, B-DBM; files from A-EF1, A-SIG, V-BRIDGE1, V-PEARL1 |

#### B-ACCT: Account module and sales tax

- **Size:** L (~11.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** V-DXC1, A-STMT, A-INS
- **Features:** [`v24_4-account-salestax-proc`](https://www.opendental.com/site/version24_4.html) (M), [`v25_1-account-salestax-sort-bottom`](https://www.opendental.com/site/version25_1.html) (S), [`v25_1-account-salestax-procs-on-claims`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-account-prodselect-amount-unpaid`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-account-orthocase-cleanup`](https://www.opendental.com/site/version25_3.html) (S), [`v24_4-reports-dailypayments-merchant-fees`](https://www.opendental.com/site/version24_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Db Multi Table/`: AccountModules
  - `OpenDentBusiness/Data Interface/`: Adjustments, Procedures
  - `OpenDental/Logic/`: ClaimL
  - `OpenDental/Main Modules/`: ControlAccount
  - `OpenDental/Forms/`: FormOrthoCase, FormProdSelect
  - `OpenDental/Forms Reports/`: FormRpPaySheet
  - `OpenDentBusiness/Reports/`: RpPaySheet
  - `OpenDental/User Controls/Preferences/`: UserControlAccountAdjustments
  - `OpenDentBusiness/X12/`: X837_5010
- **Test approach:** Sales tax sum equals the adjustment method on the same procedures; tax procedure never taxed again; sort order of sales-tax rows; sales-tax procedures on claims; Daily Payments merchant-fee option.
- **Notes:** v24_4-account-salestax-proc is promoted to Tier A if the census finds the sales-tax procedure-code preference set.

#### B-PAY: Payment plans and paysplits (remaining)

- **Size:** L (~16.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** M1-02, A-PAY, V-PC, A-EF2
- **Features:** [`v25_1-account-payplan-charge-logic-deprecated`](https://www.opendental.com/site/version25_1.html) (M), [`v25_1-account-payplan-completedamt-audit`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-account-payplan-downpayment-audit`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-account-payplan-bypass-globallock`](https://www.opendental.com/site/version25_3.html) (S), [`v25_2-account-payplan-prepay-attach`](https://www.opendental.com/site/version25_2.html) (M), [`v25_3-account-paysplit-payplan-attach`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: DatabaseMaintenances, PayPlanCharges, PayPlans, PaySplits, Payments
  - `OpenDental/Forms/`: FormPayPlan, FormPayPlanChargeEdit, FormPayPlanCredits, FormPayPlanDynamic, FormPayPlanSelect, FormPaySplitEdit, FormPayment, FormSecurityLock
  - `OpenDentBusiness/Db Multi Table/`: PayPlanEdit, PaymentEdit, Security
  - `OpenDental/Logic/`: PayPlanL
  - `OpenDental/Resources/`: PrefInfos.xml
  - `OpenDental/User Controls/Preferences/`: UserControlAccountPayments
- **Test approach:** PayPlanEditTests and PaySplitsTests for the prepay and attach cases that the 25.3 spec's test matrix lists; global lock bypass on and off at each listed call site; audit entries for completed-amount and down-payment changes.
- **Notes:** Also reviews the existing DBM checks that flag unearned splits attached to plans (`DatabaseMaintenances.cs`).

#### B-ERA: ERA processing and ERA display fields

- **Size:** L (~13.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog
- **Features:** [`v24_4-manage-era-mismatch-match`](https://www.opendental.com/site/version24_4.html) (S), [`v25_1-manage-era-autosplit-carc`](https://www.opendental.com/site/version25_1.html) (M), [`v25_2-manage-era-carc-logic`](https://www.opendental.com/site/version25_2.html) (M), [`v25_3-account-era-displayfields`](https://www.opendental.com/site/version25_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Claims, DisplayFields
  - `OpenDental/Forms/`: FormDisplayFieldCategories, FormDisplayFields, FormEtrans835ClaimPay, FormEtrans835ClaimSelect, FormEtrans835Edit, FormRpEraAutoProcessed
  - `OpenDental/User Controls/Preferences/`: UserControlManageGeneral
  - `OpenDentBusiness/X12/`: X835
- **Test approach:** X835 tests with CARC-bearing claims for auto-split and the "don't auto-process" logic; the ERA claim-paid grid follows Display Fields; mismatch matching still works (it is already in our code since 24.3.43).
- **Notes:** v25_3-account-era-displayfields is implemented once here; its 25.4 twin is only verified in M2-VERIFY.

#### B-PERIO: Perio chart

- **Size:** L (~8.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog
- **Features:** [`v24_4-chart-perio-implant-removed`](https://www.opendental.com/site/version24_4.html) (S), [`v24_4-chart-perio-attached-gingiva`](https://www.opendental.com/site/version24_4.html) (M), [`v24_4-chart-perio-recession-term`](https://www.opendental.com/site/version24_4.html) (S), [`v25_2-chart-perio-print-birthdate`](https://www.opendental.com/site/version25_2.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/User Controls/`: ContrPerio
  - `OpenDental/Forms/`: FormPerio, FormPerioGraphical, FormPerioGraphicalSetup, FormPerioSetup
  - `OpenDentBusiness/Data Interface/`: PerioMeasures
- **Test approach:** Unit tests for the attached-gingiva calculation (computed like CAL, never stored); implant marker hidden after removal; recession label preference; birthdate on the graphical printout.

#### B-MED: Medical lists and vital signs

- **Size:** L (~12.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-MED
- **Features:** [`v24_4-lists-medication-search`](https://www.opendental.com/site/version24_4.html) (S), [`v25_3-medical-medlist-missingtab`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-chart-vitalsign-notes`](https://www.opendental.com/site/version25_3.html) (S), [`v25_2-chart-eform-allergy-other-convert`](https://www.opendental.com/site/version25_2.html) (M), [`v25_2-chart-eform-problem-other-convert`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Allergies, AllergyDefs, DiseaseDefs, Diseases, Medications
  - `OpenDental/Forms/`: FormAllergyEdit, FormAllergySetup, FormDiseaseDefEdit, FormDiseaseDefs, FormMedical, FormMedications, FormVitalsignEdit2014
  - `WpfControlsOD/Frms/`: FrmDiseaseEdit
- **Test approach:** Search, tab removal, vital-sign notes bound to `vitalsign.Documentation`, converting eForm "Other" allergies and problems to listed ones.

#### B-TASK: Task filters

- **Size:** S (~3 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-TASK
- **Features:** [`v25_2-manage-task-filter-unassigned`](https://www.opendental.com/site/version25_2.html) (S), [`v25_2-manage-task-filter-patient`](https://www.opendental.com/site/version25_2.html) (S)
- **Owns (exclusive while the wave runs):**
  - `WpfControlsOD/Frms/`: FrmTaskFilter
  - `OpenDentBusiness/Data Interface/`: TaskLists, Tasks
  - `OpenDental/User Controls/`: UserControlTasks
- **Test approach:** Filter by "Unassigned" clinic and region and by one patient.

#### B-IMG: Imaging tools

- **Size:** M (~4.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-IMG
- **Features:** [`v25_2-imaging-copy-without-drawings`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-imaging-font-autocalc`](https://www.opendental.com/site/version25_3.html) (S), [`v25_1-imaging-scanning-setup-details`](https://www.opendental.com/site/version25_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/User Controls/`: ControlImageDisplay
  - `OpenDental/Main Modules/`: ControlImages
  - `OpenDental/Forms/`: FormImagingSetup
  - `WpfControlsOD/Frms/`: FrmImageDrawEdit
  - `CodeBase/Utilities/`: ODCloudClient
- **Test approach:** Copy without drawings; font size and apparent size stay in sync; Scanning Setup details.

#### B-PROC: Procedure codes, code tools and fee tools

- **Size:** L (~11.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-FEE, A-INS
- **Features:** [`v24_4-setup-cda-codes-2025`](https://www.opendental.com/site/version24_4.html) (S), [`v25_3-canada-cda-codes-2026`](https://www.opendental.com/site/version25_4.html) (S), [`v25_3-procs-cdt-2026`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-procs-codegroup-reset-tool`](https://www.opendental.com/site/version25_4.html) (S), [`v25_1-setup-proccode-morefees-filters`](https://www.opendental.com/site/version25_1.html) (S), [`v25_1-setup-feetools-tabs`](https://www.opendental.com/site/version25_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: CodeGroups, Fees, ProcedureCodes
  - `OpenDentBusiness/Misc/`: ConvertDatabases8
  - `OpenDental/Forms/`: FormFeeSchedTools, FormProcCodeEdit, FormProcCodeEditMore, FormProcTools
  - OpenDentBusiness/Data Interface/CodeGroupDefaults.cs (new shared default list)
- **Test approach:** Reset Code Groups restores the default list; More Fees filters; Fee Tools tabs keep every 24.3 function and the 24.4 date boxes; CDA 2026 import.
- **Notes:** v25_3-procs-cdt-2026 depends on decoupling Phase 2 (codes come from a user import, no ADA text in the repo). v25_3-procs-codegroup-reset-tool uses the extra detail in its 25.4 twin.

#### B-TP: Treatment plan: add procedures to the active plan

- **Size:** M (~4 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-FEE
- **Features:** [`v25_3-tp-add-procs-active`](https://www.opendental.com/site/version25_3.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlTreat
  - `OpenDental/Forms/`: FormProcCodes2
  - `OpenDentBusiness/Data Interface/`: TreatPlans
  - OpenDentBusiness/Data Interface/ProcAddHelper.cs (new; shared procedure-entry helper so ControlChart.cs is not touched)
- **Test approach:** Add procedures from the code list straight into the active plan, with correct priority and estimates.
- **Notes:** Shared procedure-entry code goes into a new helper so `ControlChart.cs` is untouched.

#### B-WIKI: Wiki

- **Size:** L (~13 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog
- **Features:** [`v25_1-misc-wiki-draft-compare`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-wiki-readonly-offline`](https://www.opendental.com/site/version25_2.html) (L), [`v25_2-wiki-search-limit`](https://www.opendental.com/site/version25_2.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormWiki, FormWikiCompare, FormWikiDrafts, FormWikiEdit, FormWikiFileFolder, FormWikiHistory, FormWikiSearch
  - `WpfControlsOD/Frms/`: FrmWikiSetup
  - `OpenDentBusiness/Data Interface/`: WikiPages
  - `OpenDentBusiness/Misc/`: WikiStaticExport (new)
- **Test approach:** Draft compare; read-only export opens with the program closed; search limit.
- **Notes:** The offline wiki is L with low confidence in the spec.

#### B-TIME: Time clock and time cards

- **Size:** M (~5.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-CHAT
- **Features:** [`v25_1-manage-timecard-totals-grid`](https://www.opendental.com/site/version25_1.html) (M), [`v25_3-manage-timeclock-reportsto`](https://www.opendental.com/site/version25_3.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ClockEvents, Employees, TimeCardRules
  - `OpenDental/Main Modules/`: ControlManage
  - `OpenDental/Forms/`: FormTimeCard, FormTimeCardManage, FormTimeCardSetup
- **Test approach:** Period totals grid in both formats; Reports To filter.

#### B-SERVER: Server connections, replication and report setup

- **Size:** L (~8.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-CHAT
- **Features:** [`v24_4-reports-incomplete-procnotes-all`](https://www.opendental.com/site/version24_4.html) (S), [`v24_4-setup-reportserver-audit`](https://www.opendental.com/site/version24_4.html) (S), [`v25_3-misc-pem-path-label`](https://www.opendental.com/site/version25_4.html) (S), [`v25_2-server-replication-delay`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Db/`: ConnectionStore, Db
  - `OpenDental/Forms/`: FormChooseDatabase, FormReportSetup
  - `OpenDental/Forms Reports/`: FormRpProcNote
  - `OpenDentBusiness/Cache/`: PrefC
  - `OpenDentBusiness/Data Interface/`: ReplicationServers, Signalods
  - `OpenDentBusiness/Reports/`: RpProcNote
  - `OpenDental/User Controls/Preferences/`: UserControlServerConnections
- **Test approach:** Incomplete-notes option; PEM path label; read/write delay settings with a replica; the Report Server audit entry (already in our code since 24.3.35).

#### B-EF: eForm designer and Patient Forms window

- **Size:** M (~5.5 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-EF1, A-EF2
- **Features:** [`v25_1-eforms-conditional-children-view`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-eforms-patient-forms-window`](https://www.opendental.com/site/version25_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormPatientForms, FormSheetImport
  - `WpfControlsOD/Frms/`: FrmEFormCheckBoxEdit, FrmEFormDefEdit, FrmEFormFieldChildren, FrmEFormRadioButtonsEdit
- **Test approach:** Conditional children view; Patient Forms window changes.

#### B-PRINT: Statements, sheets, printing and grid export

- **Size:** L (~18 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-ACCT, A-SIG, A-STMT, A-SHEET, A-INS
- **Features:** [`v25_3-manage-statement-email-lastname`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-manage-statement-barcode`](https://www.opendental.com/site/version25_3.html) (M), [`v25_1-sheets-autosave-images`](https://www.opendental.com/site/version25_1.html) (S), [`v25_3-sheets-checkbox-cleanup`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-misc-printer-options`](https://www.opendental.com/site/version25_3.html) (M), [`v25_3-misc-export-csv-txt`](https://www.opendental.com/site/version25_3.html) (M), [`v24_4-manage-accounting-export`](https://www.opendental.com/site/version24_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormAccounting, FormBilling, FormBillingDefaults, FormJournal, FormSheetDef, FormSheetFieldCheckBox, FormSheetFillEdit, FormStatementOptions
  - `WpfControlsOD/Frms/`: FrmPrintPreview
  - `OpenDentBusiness/UI/`: GridOD
  - `OpenDental/Logic/`: PrinterL
  - `OpenDentBusiness/SheetFramework/`: SheetFieldsAvailable, SheetFiller
  - `OpenDental/SheetFramework/`: SheetPrinting
  - `OpenDentBusiness/Data Interface/`: Statements
  - the grid windows that gain Export .csv/.txt buttons (listed in the 25.3 spec item)
- **Test approach:** Statement barcode and last name in e-mailed statements; sheet auto-save to Imaging; printer options window; Export .csv and .txt from the listed grids.

#### B-AUDIT: Patient audit-trail entries

- **Size:** S (~3 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-EF2
- **Features:** [`v25_2-security-audit-patient-clinic`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-family-audit-preferredname`](https://www.opendental.com/site/version25_3.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormClinics, FormPatientEdit
  - `OpenDentBusiness/Data Interface/`: Patients
- **Test approach:** SecurityLogsTests: clinic changes and preferred-name changes are logged from every save path.
- **Notes:** Logic sits in `Patients.cs`, so the main window needs no change.

#### B-CLAIM: Claims, Canadian attachments, claim-procedure audit and permissions

- **Size:** L (~13 dev-days) · **Wave:** B2 · **Depends on:** B-ERA, B-ACCT; **takes files over from:** V-DXC1, A-INS, A-IMG, B-TP
- **Features:** [`v25_1-account-canada-attachment-history`](https://www.opendental.com/site/version25_1.html) (M), [`v25_1-canada-etrans-counters-uncapped`](https://www.opendental.com/site/version25_1.html) (S), [`v25_1-imaging-canada-claim-response-titles`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-account-medical-eob-prompt`](https://www.opendental.com/site/version25_2.html) (S), [`v25_1-misc-claimproc-override-audit`](https://www.opendental.com/site/version25_1.html) (S), [`v25_3-security-inswriteoffedit-clarify`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-security-claimproc-billedtoins`](https://www.opendental.com/site/version25_3.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Eclaims/`: Canadian, CanadianOutput
  - `OpenDentBusiness/Data Interface/`: ClaimAttaches, ClaimProcs, EobAttaches, Etranss
  - `OpenDental/Logic/`: ClaimL
  - `OpenDental/Main Modules/`: ControlAccount, ControlTreat
  - `OpenDental/Forms/`: FormCCDPrint, FormClaimAttachHistory, FormClaimEdit, FormClaimProc, FormEtrans835ClaimPay, FormImageSelectClaimAttach
  - `OpenDental/User Controls/Preferences/`: UserControlAccountClaimReceive
- **Test approach:** Canadian attachment send history; counters beyond the old cap; saved CCD response titles; Medical EOB prompt; audit entries for Ins Est and Allowed overrides; write-off and billed-to-ins permission checks on create, edit and delete.
- **Notes:** Enforcement side of the two claim-procedure permission items (their wording is in M1-02).

#### B-APPT: Appointments module and appointment permissions

- **Size:** L (~15.5 dev-days) · **Wave:** B2 · **Depends on:** B-TASK; **takes files over from:** A-APPT, A-GN
- **Features:** [`v24_4-appts-copy-apptview`](https://www.opendental.com/site/version24_4.html) (S), [`v25_2-appts-provider-timebars-op-background`](https://www.opendental.com/site/version25_2.html) (M), [`v25_2-appts-waiting-room-window`](https://www.opendental.com/site/version25_2.html) (M), [`v25_3-appts-timebar-secondary-color`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-security-apptcreate-pinboard`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-security-apptmove-scope`](https://www.opendental.com/site/version25_3.html) (S), [`v25_1-appt-multiple-completed-tasks`](https://www.opendental.com/site/version25_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Logic/`: AppointmentL
  - `OpenDentBusiness/Data Interface/`: Appointments, ApptViewItems, ApptViews, Schedules, Tasks
  - `OpenDental/Main Modules/`: ControlAppt, ControlChart
  - `OpenDental/UI/Appt/`: ControlApptPanel
  - `OpenDental/Forms/`: FormApptEdit, FormApptViewEdit, FormApptViews, FormApptsOther, FormTaskListSelect, FormTasksForAppt, FormWaitingRoom (new)
  - `OpenDental/User Controls/Preferences/`: UserControlApptAppearance
  - `OpenDental/User Controls/`: UserControlTasks
- **Test approach:** Copy View; time bars on the operatory background; waiting room window; secondary color; Appointment Create and Move permission checks on the pinboard and unscheduled paths; completed tasks no longer block a new appointment task.
- **Notes:** Enforcement side of v25_3-security-apptcreate-pinboard and v25_3-security-apptmove-scope; their wording is in M1-02.

#### B-INSH: Insurance history, benefit editing and plan list

- **Size:** L (~15 dev-days) · **Wave:** B2 · **Depends on:** none inside this backlog; **takes files over from:** A-EF2, A-INS, B-ACCT
- **Features:** [`v25_1-family-inshist-two-dates`](https://www.opendental.com/site/version25_1.html) (M), [`v25_2-family-ins-history-edit-remove`](https://www.opendental.com/site/version25_2.html) (M), [`v25_2-family-edit-benefit-ui`](https://www.opendental.com/site/version25_2.html) (M), [`v25_3-family-insplans-innet`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-family-bluebook-providerfee`](https://www.opendental.com/site/version25_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlFamily
  - `OpenDentBusiness/X12/`: EB271
  - `OpenDental/Forms/`: FormBenefitEdit, FormInsBenefits, FormInsBlueBookRules, FormInsHistSetup, FormInsPlans
  - `OpenDentBusiness/Data Interface/`: InsBlueBooks, InsPlans, Prefs, Procedures, Recalls
- **Test approach:** Two history dates per category; edit and remove entered dates; redesigned Edit Benefit window; In Net column; Blue Book "Provider Fee" wording.

#### B-DBM: Database maintenance tools

- **Size:** L (~11.5 dev-days) · **Wave:** B2 · **Depends on:** none inside this backlog; **takes files over from:** A-HASH, A-IMG, B-PAY
- **Features:** [`v24_4-tools-rawemails-unicode`](https://www.opendental.com/site/version24_4.html) (S), [`v25_1-misc-dbm-email-invalid-chars`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-tools-dbm-invalid-chars-gdi`](https://www.opendental.com/site/version25_2.html) (M), [`v25_3-tools-dbm-whitespace-tools`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-tools-dbm-employee-supervisor`](https://www.opendental.com/site/version25_3.html) (S), [`v25_3-tools-dbm-base64-docs`](https://www.opendental.com/site/version25_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: DatabaseMaintenances, EmailMessages
  - `OpenDentBusiness/Misc/`: FileAtoZ
  - `OpenDental/Forms/`: FormDatabaseMaintInvalidChars (new), FormDatabaseMaintenance
  - `OpenDentBusiness/Imaging/`: ImageStore
- **Test approach:** DatabaseMaintenanceTests for each new check and tool, in check and fix modes.
- **Notes:** v25_3-tools-dbm-base64-docs is implemented once here; its 25.4 twin is verified in M2-VERIFY.

#### B-SHELL: Main window and cross-module integration

- **Size:** L (~14 dev-days) · **Wave:** B3 · **Depends on:** B-ACCT, B-APPT, B-TP, B-INSH, B-IMG, B-CLAIM, B-DBM; **takes files over from:** A-EF1, A-SIG, V-BRIDGE1, V-PEARL1
- **Features:** [`v25_1-setup-programlink-selectedprocnums`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-ui-blur-module-switch`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-tools-file-monitor`](https://www.opendental.com/site/version25_3.html) (M), [`v25_3-chart-orthochart-nonmodal`](https://www.opendental.com/site/version25_3.html) (M), [`v25_2-chart-medical-rows-bold`](https://www.opendental.com/site/version25_2.html) (S), [`v25_2-bridges-dxc-credit-score-removed`](https://www.opendental.com/site/version25_2.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlAccount, ControlAppt, ControlChart, ControlFamily, ControlImages, ControlTreat, FormOpenDental, FormOpenDentalMenus
  - `WpfControlsOD/Bridges/`: DentalXChange
  - `OpenDentBusiness/Misc/`: FileAtoZ
  - `OpenDental/Forms/`: FormFileMonitor (new), FormOrthoChart, FormProgramLinkEdit
  - `WpfControlsOD/Frms/`: FrmMessageReplacements
  - `OpenDentBusiness/Imaging/`: ImageStore
  - `OpenDentBusiness/Data Interface/`: PatientNotes
  - `WpfControlsOD/Logic/`: ProgramL
  - `OpenDental/User Controls/Preferences/`: UserControlMainWindowMisc
- **Test approach:** `[SelectedProcNums]` replaced from each module; blur on module switch; File Monitor; ortho chart left open while working elsewhere; bold medical rows; the DentalXChange credit-score bridge is gone and its program row disabled.
- **Notes:** Last wave of M1: it owns `FormOpenDental.cs`, the menus and the main module controls together. Also switches the main window to the domain-login helper from A-SIG.

## Milestone M2: 26.1 parity

Goal: the adopted database and the fork reach Open Dental 26.1.70 parity for desktop features. M2 starts after M1 is complete. Its schema package comes first; the 25.4 and 26.1 features follow. Unlike M1, there is no office data at 26.1 yet, so nothing in M2 blocks the office; priorities follow value to a US private practice (see the planning notes in features-25.4.md).

### M2 foundation (wave F2)

Before M2-01 starts, set up a 26.1 reference: a fresh Open Dental 26.1 trial install used only to read its schema, preference names and enum values (decoupling plan, Phase 6). This settles the "inferred" DDL and the open naming questions in the 25.4 and 26.1 specs.

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-01 | Schema migration 25.3.48 to 26.1.70 | M | 1 | – |
| M2-02 | Registries at the 26.1 level | M | 1 | – |
| M2-VERIFY | Implement-once checks | S | 5 | – |

#### M2-01: Schema migration 25.3.48 to 26.1.70

- **Size:** M (~5–8 dev-days) · **Wave:** F2 · **Depends on:** none inside this backlog
- **Features:** [`v25_4-misc-enum-parity`](https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/TableTypes/`: Branding (new), Carrier, ChatUserod, ClaimPayment, ClaimProc, CodeGroup, Def, DeletedObj (new), Dunning, EForm, EFormField, Employee, Evaluation, EvaluationDef, InsEditLog, InsEditPatLog, InsVerify, InsVerifyHist, PaySuitePayment (new), PaySuitePaymentDetail (new), PayTerminal, ProcNote, Procedure, Referral, RegistrationKey, SchoolCourseDef, SchoolCourseEnrollee (new), SchoolCourseInstructor (new), Sheet, SmsToMobile, Statement
  - `OpenDentBusiness/Data Interface/`: Procedures
  - OpenDentBusiness/Misc/ForkMigrations/OdParity26_1.cs (new; the 25.3.48 to 26.1.70 steps)
  - OpenDentBusiness/Crud/*Crud.cs for every changed or new table (regenerated with xCrudGenerator, never hand-edited)
  - OpenDentBusiness/Misc/ForkMigrations/SchemaVerifier.cs (new) plus a reference DDL snapshot of the target Open Dental schema (new)
- **Test approach:** Same as M1-01, against the 26.1 reference schema. The reference comes from a fresh 26.1 trial install (or the published 26.1 schema document where no install is available). Also time the ALTERs on the office copy: `claimproc` and `procedurelog` are the largest tables.
- **Notes:** Scope: schema-changes.json entries with range 25.3-26.1 (5 new tables, 28 columns, 4 type changes) plus widening `claimpayment.Note` to text. NOT NULL, defaults and indexes for 25.4 and 26.1 are inferred in the specs; confirm them on the reference install first.
  - Enum values that live in changed table classes are done here, for example `DefCat.EmployeeTitles=56` (`Def.cs`), `EnumBrandingType` value 1 (`Branding.cs`; settle LogoFilePath versus LogoImage first) and `EnumDeletedObjType` (`DeletedObj.cs`).
  - Updates the two hand-written procnote join queries in `Procedures.cs` so the second-signature columns load (data plumbing for the new columns only).
  - Sets existing `evaluation.GradeOverride` rows to -1 when the column is added; `insverify.DateLastAssigned` keeps its index when it becomes datetime.
  - Updates `OdSchemaCompat` to 26.1.70 and extends the adoption allowlist only after the verifier passes (R1).

#### M2-02: Registries at the 26.1 level

- **Size:** M (~5–8 dev-days) · **Wave:** F2 · **Depends on:** none inside this backlog
- **Features:** [`v25_4-dentalschool-permissions`](https://www.opendental.com/OpenDentalDocumentation25-4.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/TableTypes/`: AlertItem, AutomationCondition, CreditCard, DisplayField, EServiceLog, GroupPermission, ImageDraw, Pref, Program, SecurityLog, UserOdPref
  - `OpenDentBusiness/Misc/ForkMigrations/`: Seed26_1 (new)
  - `OpenDental/Logic/`: DefL
  - `OpenDentBusiness/Data Interface/`: Defs, DisplayFields, GroupPermissions
  - `OpenDentBusiness/Db/`: Enumerations
  - `OpenDentBusiness/WebBridges/Erx/`: Erx
  - `OpenDental/Forms/`: FormDefinitions
  - `OpenDental/Resources/`: PrefInfos.xml
  - `OpenDental/User Controls/`: UserControlSecurityTree
  - OpenDentBusiness/SheetFramework/SheetEnums.cs (SheetFieldType)
- **Test approach:** Same as M1-02. Settle the `EnumPermType` numbering conflict (272 is PhoneExtension in the 25.3 diff, BlockoutAdd in 25.4) on the reference install before any value is used.
- **Notes:** Permissions 13, 77, 78, 81, 272, 273, 275 (276 reserved), AlertType 46 to 51, FormType 21, CreditCardSource 23, 25, 26, LogSources 26 to 28, ErxStatus 6, the definition setup for DefCat 56 EmployeeTitles in `Defs.cs`, `DefL.cs` and `FormDefinitions.cs` (the enum value itself is in M2-01, which owns `Def.cs`), AutoCondField 14, EnumImageAnnotVendor 3 and SheetFieldType MobileStaticText (reserved), the 25.4 and 26.1 preferences, and the available-field lists in `DisplayFields.cs` for the new display-field items (service date view, gender, preauthorization flag, the "standard provider fee" column text).

#### M2-VERIFY: Implement-once checks

- **Size:** S (~2 dev-days) · **Wave:** F2 · **Depends on:** none inside this backlog
- **Features:** [`v25_4-canada-cda-codes-2026`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-tools-dbm-base64-documents`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-setup-pem-path-label`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-proccodes-codegroup-reset`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-claims-era-displayfields`](https://www.opendental.com/site/version25_4.html) (M)
- **Test approach:** Compare each M1 implementation with the extra detail in its 25.4 entry.
- **Notes:** Each listed 25.4 item duplicates a 25.3 backport already built in M1 (B-PROC, B-DBM, B-SERVER, B-ERA). Gaps found here become small fixes, made by whichever M2 package owns the file at that time, or in wave F2 when no package owns it. Owns no files itself.

### Wave C1

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-EST | Insurance estimate engine | XL | 4 | M2-01 |
| M2-SDV | Service Date View (25.4 and 26.1) | L | 2 | M2-01, M2-02 |
| M2-PAYW | Payment window | M | 2 | M2-02 |
| M2-RECUR | Recurring and repeating charges | M | 2 | M2-02 |
| M2-CAN | Procedure codes and Canadian code systems | L | 7 | M2-02 |
| M2-INSV | Insurance Verification List | M | 2 | M2-01 |
| M2-APPT | Blockout permissions and appointment preferences | M | 3 | M2-02 |
| M2-TERM | "Standard Provider Fee" terminology | M | 1 | M2-02 |
| M2-UDS | UDS+ report | L | 1 | M2-02 |
| M2-CLAIMFORM | Denti-Cal DC217 claim form | M | 1 | M2-02 |
| M2-SHEETS | Sheets: non-Latin text and duplicate-field warning | M | 2 | M2-01 |
| M2-ERA | ERA report filters and claimproc-to-ERA link | M | 2 | M2-01 |
| M2-ADJ | Percent-of-fee adjustments | M | 2 | M2-01 |
| M2-CLAIMRCV | Related-claims prompt on receive | M | 1 | M2-02 |
| M2-EMP | Employee titles and future clock-outs | S | 2 | M2-01, M2-02 |
| M2-MED | Problem merge, allergy search, allergy DBM | M | 3 | M2-02 |
| M2-IMG | Imaging: 16-bit windowing, PDF JavaScript preference | S | 2 | M2-02 |
| M2-MISC | Small 25.4 and 26.1 items | L | 6 | M2-01, M2-02 |
| M2-AUDIT | Birthdate audit | S | 1 | M2-02 |
| M2-CHAT | Chat notification sound | S | 1 | M2-01 |
| M2-SCH1 | Dental school: courses, enrollment, evaluations (gated) | L | 2 | M2-01, M2-02 |
| V-DXC2 | DentalXChange PDF attachments | M | 1 | – |
| V-PEARL2 | Pearl troubleshooting log level | S | 1 | – |

#### M2-EST: Insurance estimate engine

- **Size:** XL (~22 dev-days) · **Wave:** C1 · **Depends on:** M2-01
- **Features:** [`v25_4-tp-estimates-appt-date`](https://www.opendental.com/site/version25_4.html) (L), [`v25_4-chart-nobillins-audit`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-family-benefit-tooth-exclusions`](https://www.opendental.com/site/version25_4.html) (M), [`v26_1-family-benefit-agelimit-teeth`](https://www.opendental.com/site/version26_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Logic/`: BenefitLogic, Tooth
  - `OpenDentBusiness/Data Interface/`: Benefits, ClaimProcs, Procedures
  - `OpenDental/Main Modules/`: ControlTreat
  - `OpenDental/Forms/`: FormBenefitEdit, FormBenefitFrequencies, FormClaimProc, FormInsBenefits, FormProcEdit
  - `OpenDentBusiness/Db Multi Table/`: TreatmentPlanModules
- **Test approach:** ClaimProcsTests and BenefitsTest: estimates as of the scheduled appointment date across benefit-year boundaries; Do Not Bill Insurance copied to claimprocs and audited; exclusions and age limits by tooth.
- **Notes:** Groups the 25.4 and 26.1 items that change `ClaimProcs.cs` and `Procedures.cs`, so other packages can run beside it.

#### M2-SDV: Service Date View (25.4 and 26.1)

- **Size:** L (~14 dev-days) · **Wave:** C1 · **Depends on:** M2-01, M2-02
- **Features:** [`v25_4-account-service-date-view`](https://www.opendental.com/site/version25_4.html) (M), [`v26_1-account-service-date-view`](https://www.opendental.com/site/version26_1.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Db Multi Table/`: AccountModules
  - `OpenDental/Main Modules/`: ControlAccount
  - `OpenDental/Forms/`: FormBillingOptions, FormDisplayFieldCategories, FormDisplayFields, FormStatementOptions
  - `OpenDental/Forms Reports/`: FormRpServiceDateView
  - `OpenDentBusiness/Reports/`: RpServiceDateView
  - `OpenDentBusiness/SheetFramework/`: SheetDataTableUtil, SheetFiller, SheetUtil
  - `OpenDentBusiness/Data Interface/`: Statements
- **Test approach:** Account grid and statements in service-date view match the report; display-field columns.

#### M2-PAYW: Payment window

- **Size:** M (~5.5 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-account-incometransfer-limit`](https://www.opendental.com/site/version25_4.html) (S), [`v26_1-account-payment-invoice-filter`](https://www.opendental.com/site/version26_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormPayment
  - `OpenDentBusiness/Data Interface/`: PaySplits, StmtLinks
  - `OpenDentBusiness/Db Multi Table/`: PaymentEdit
  - `OpenDental/User Controls/Preferences/`: UserControlAccountPayments
- **Test approach:** Income transfers limited to patient payments; invoice filter on outstanding charges.

#### M2-RECUR: Recurring and repeating charges

- **Size:** M (~5.5 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-account-recurringcharge-fail-email`](https://www.opendental.com/site/version25_4.html) (M), [`v26_1-account-repeatcharge-backdate-pref`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: EmailAddresses, EmailMessages, RecurringCharges, RepeatCharges
  - `OpenDental/Forms/`: FormCreditRecurringCharges, FormRepeatChargeEdit, FormRepeatChargesUpdate
  - `OpenDental/User Controls/Preferences/`: UserControlAccountRecAndRepCharges
- **Test approach:** RecurringChargesTests and RepeatChargesTests: failure e-mail; backdating preference.

#### M2-CAN: Procedure codes and Canadian code systems

- **Size:** L (~13 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-canada-other-proc-codes`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-canada-codes-cdha`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-canada-codes-dac`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-canada-codes-ramq`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-canada-labfee-pay`](https://www.opendental.com/site/version25_4.html) (S), [`v26_1-lists-canada-othercodes-password`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-lists-proccode-no-hyphens`](https://www.opendental.com/OpenDentalDocumentation26-1.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Eclaims/`: Canadian
  - `OpenDental/Forms/`: FormClaimPayTotal, FormProcCodeEdit, FormProcCodeNew, FormProcCodes2, FormProcCodesOther (new), FormProcTools
  - `OpenDentBusiness/Data Interface/`: ProcedureCodes
  - OpenDentBusiness/Data Interface/DatabaseMaintenances.ProcCodes.cs (new partial file)
- **Test approach:** Import CDHA, DAC and RAMQ files; RAMQ R suffix stripped on claims; Pay Labs; password on the tools; codes with commas or hyphens refused.
- **Notes:** Mostly relevant to Canadian offices; v26_1-lists-proccode-no-hyphens is for everyone.

#### M2-INSV: Insurance Verification List

- **Size:** M (~5.5 dev-days) · **Wave:** C1 · **Depends on:** M2-01
- **Features:** [`v25_4-appts-insverify-daterange`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-appts-insverify-carriers`](https://www.opendental.com/site/version25_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormCarriers, FormInsVerificationList, FormInsVerificationSetup
  - `OpenDentBusiness/Data Interface/`: InsVerifies, InsVerifyHists, PatPlans
- **Test approach:** InsVerifyTests: date range and carrier filters; assignment time kept as datetime.

#### M2-APPT: Blockout permissions and appointment preferences

- **Size:** M (~7 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-appts-blockout-permissions`](https://www.opendental.com/site/version25_4.html) (M), [`v26_1-setup-make-recall-search-pref`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-setup-default-prov-unassigned-pref`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlAppt
  - `OpenDental/Forms/`: FormApptsOther, FormBlockoutCutCopyPaste, FormScheduleBlockEdit, FormScheduleDayEdit
  - `OpenDentBusiness/Data Interface/`: Schedules
  - `OpenDental/User Controls/Preferences/`: UserControlApptGeneral
- **Test approach:** Add, Edit and Delete blockout permission matrix; Make Recall search preference; default provider for unassigned operatories.

#### M2-TERM: "Standard Provider Fee" terminology

- **Size:** M (~4 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-misc-ucr-terminology`](https://www.opendental.com/site/version25_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormInsBlueBookRules
  - `OpenDental/Forms Reports/`: FormRpCapitation, FormRpNetProdDetail, FormRpProviderPayroll
  - `OpenDentBusiness/Data Interface/`: InsBlueBooks
  - `OpenDentBusiness/TableTypes/`: InsPlan
  - `OpenDentBusiness/Reports/`: RpProdInc
  - `OpenDental/User Controls/Preferences/`: UserControlAccountClaimSend, UserControlFamilyInsurance
- **Test approach:** String review; report headers.
- **Notes:** The strings in the Edit Insurance Plan window are changed in M2-INSPLAN, together with the layout rewrite.

#### M2-UDS: UDS+ report

- **Size:** L (~10 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-reports-udsplus`](https://www.opendental.com/site/version25_4.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: DisplayReports
  - `OpenDental/Forms/`: FormReportsMore
  - `OpenDental/Forms Reports/`: FormRpUdsPlus (new)
  - `OpenDentBusiness/Reports/`: RpUdsPlus (new)
- **Test approach:** Report output against hand-computed numbers on test data.
- **Notes:** Only for FQHC offices.

#### M2-CLAIMFORM: Denti-Cal DC217 claim form

- **Size:** M (~4 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-claims-dentical-dc217`](https://www.opendental.com/site/version25_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ClaimForms
  - `OpenDental/Forms/`: FormClaimForms, FormClaimPrint
  - `OpenDentBusiness/Properties/`: Resources.resx
  - OpenDentBusiness/Resources/ClaimForms/ClaimFormDenti-Cal DC217_2019.xml (new)
- **Test approach:** Print alignment on the form.
- **Notes:** Only for California Medi-Cal offices.

#### M2-SHEETS: Sheets: non-Latin text and duplicate-field warning

- **Size:** M (~5.5 dev-days) · **Wave:** C1 · **Depends on:** M2-01
- **Features:** [`v25_4-sheets-nonlatin`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-sheets-duplicate-field-warning`](https://www.opendental.com/site/version25_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormSheetDefEdit, FormSheetFillEdit
  - `OpenDentBusiness/SheetFramework/`: SheetDrawingJob
  - `OpenDentBusiness/Data Interface/`: SheetFieldDefs
  - `OpenDental/SheetFramework/`: SheetPrinting
- **Test approach:** Sheets with non-Latin text render and print; duplicate field warning.

#### M2-ERA: ERA report filters and claimproc-to-ERA link

- **Size:** M (~5.5 dev-days) · **Wave:** C1 · **Depends on:** M2-01
- **Features:** [`v25_4-reports-era-autoprocessed-filters`](https://www.opendental.com/site/version25_4.html) (S), [`v26_1-account-era-claimproc-link`](https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Etrans835Attaches, Etrans835s
  - `OpenDental/Logic/`: EtransL
  - `OpenDental/Forms/`: FormEtrans835ClaimPay, FormEtrans835ClaimSelect, FormEtrans835Edit, FormRpEraAutoProcessed
  - `OpenDentBusiness/X12/`: X835
  - OpenDentBusiness/Data Interface/ClaimProcs.Era.cs (new partial file)
- **Test approach:** PreAuth and $0 filters; claimprocs posted from an ERA store the 835 claim attachment.
- **Notes:** New ClaimProcs queries go into a new partial file.

#### M2-ADJ: Percent-of-fee adjustments

- **Size:** M (~5.5 dev-days) · **Wave:** C1 · **Depends on:** M2-01
- **Features:** [`v26_1-account-adjust-percent-of-fee`](https://www.opendental.com/site/version26_1.html) (M), [`v26_1-setup-adjtype-default-percent`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Adjustments
  - `OpenDental/Forms/`: FormAdjMulti, FormAdjust, FormDefEdit
- **Test approach:** AdjustmentsTests: fixed or percent; default percentage from the adjustment type.

#### M2-CLAIMRCV: Related-claims prompt on receive

- **Size:** M (~4 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v26_1-account-claim-receive-related-prompt`](https://www.opendental.com/site/version26_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Logic/`: ClaimL
  - `OpenDentBusiness/Data Interface/`: Claims
  - `OpenDental/Forms/`: FormClaimEdit
  - `OpenDental/User Controls/Preferences/`: UserControlAccountClaimReceive
- **Test approach:** Receiving any claim prompts for related outstanding claims.

#### M2-EMP: Employee titles and future clock-outs

- **Size:** S (~3 dev-days) · **Wave:** C1 · **Depends on:** M2-01, M2-02
- **Features:** [`v26_1-manage-timecard-future-clockout`](https://www.opendental.com/site/version26_2.html) (S), [`v26_1-lists-employee-title`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ClockEvents, Employees
  - `OpenDental/Forms/`: FormClockEventEdit, FormEmployeeEdit, FormEmployeeSelect, FormTimeCard
- **Test approach:** Title on employees; future-dated clock-out allowed as specified.

#### M2-MED: Problem merge, allergy search, allergy DBM

- **Size:** M (~7 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v26_1-lists-problem-merge-keep`](https://www.opendental.com/site/version26_1.html) (M), [`v26_1-setup-allergy-search`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-tools-dbm-missing-allergydefs`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Allergies, AllergyDefs, DiseaseDefs
  - `OpenDental/Forms/`: FormAllergySetup, FormDiseaseDefs
  - OpenDentBusiness/Data Interface/DatabaseMaintenances.Allergies.cs (new partial file)
- **Test approach:** Merge keeps the chosen problem and repoints references; allergy search; DBM fix for allergies without a definition.
- **Notes:** The basic problem merge does not exist in our code, so this builds all of it.

#### M2-IMG: Imaging: 16-bit windowing, PDF JavaScript preference

- **Size:** S (~3 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v25_4-imaging-16bit-windowing`](https://www.opendental.com/OpenDentalDocumentation25-4.xml) (S), [`v26_1-imaging-pdf-javascript-pref`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Imaging/`: BitmapDicom, ImageHelper
  - `OpenDental/User Controls/`: ControlImageDisplay
  - `CodeBase/Controls/`: ODWebView2
  - `OpenDental/User Controls/Preferences/`: UserControlImagingGeneral
- **Test approach:** True 16-bit radiographs window correctly; PDFs open with JavaScript off by default.

#### M2-MISC: Small 25.4 and 26.1 items

- **Size:** L (~9 dev-days) · **Wave:** C1 · **Depends on:** M2-01, M2-02
- **Features:** [`v25_4-misc-diagnostics-info`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-misc-print-window`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-family-referral-phone`](https://www.opendental.com/OpenDentalDocumentation25-4.xml) (S), [`v26_1-setup-automation-clinic-mismatch`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-misc-task-attach-nonmodal`](https://www.opendental.com/site/version26_1.html) (S), [`v25_4-chart-perio-direction`](https://www.opendental.com/site/version25_4.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: AutomationConditions, Automations, BugSubmissions, Printers
  - `OpenDental/Logic/`: AutomationL, PrinterL
  - `OpenDental/User Controls/`: ContrPerio, UserControlTasks
  - `OpenDental/Forms/`: FormAbout, FormAutomationConditionEdit, FormAutomationEdit, FormPerio, FormPrinterEdit, FormPrinterSetup, FormTaskAttachmentEdit, FormTaskAttachments, FormTaskEdit
  - `WpfControlsOD/Frms/`: FrmReferralEdit
  - `OpenDentBusiness/TableTypes/`: Printer
  - `WpfControlsOD/Logic/`: PrinterL
  - a non-HQ diagnostics helper (new) replacing BugSubmissions.GetDiagnostics
- **Test approach:** Per item, as in the specs.
- **Notes:** Diagnostics window, print window, referral phone length, automation condition, non-modal task attachments, perio direction buttons.

#### M2-AUDIT: Birthdate audit

- **Size:** S (~1.5 dev-days) · **Wave:** C1 · **Depends on:** M2-02
- **Features:** [`v26_1-tools-audit-birthdate`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormPatientEdit
  - `OpenDentBusiness/Data Interface/`: Patients
- **Test approach:** Birthdate changes logged from every save path, including sheet and eForm import.
- **Notes:** Logic sits in `Patients.cs`.

#### M2-CHAT: Chat notification sound

- **Size:** S (~1.5 dev-days) · **Wave:** C1 · **Depends on:** M2-01
- **Features:** [`v26_1-manage-chat-sound`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - OpenDentBusiness/Data Interface/Chats.cs, ChatMsgs.cs, ChatAttaches.cs, ChatReactions.cs, ChatUserAttaches.cs, ChatUserods.cs, SequenceCounters.cs (new)
  - OpenDentalChat/ (new WPF executable project) and its entry in OpenDental.sln
- **Test approach:** Sound setting saved per user.
- **Notes:** Needs the chat program from A-CHAT. If the census moved A-CHAT out of M1, build A-CHAT first in this slot (its files are free in wave C1), then this item; the auto-launch item in M2-SHELL waits for both.

#### M2-SCH1: Dental school: courses, enrollment, evaluations (gated)

- **Size:** L (~14 dev-days) · **Wave:** C1 · **Depends on:** M2-01, M2-02
- **Features:** [`v25_4-dentalschool-courses`](https://www.opendental.com/site/version25_4.html) (L), [`v25_4-dentalschool-evaluations`](https://www.opendental.com/site/version25_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: EvaluationDefs, Evaluations, GradingScales, SchoolCourseEnrollees (new), SchoolCourseInstructors (new), SchoolCourses
  - `OpenDental/Forms/`: FormDentalSchoolSetup, FormEvaluationDefEdit, FormEvaluationDefs, FormEvaluationEdit, FormEvaluationReport, FormEvaluations, FormProvStudentEdit, FormProviderSetup, FormSchoolCourseEdit, FormSchoolCourses
- **Test approach:** Course, instructor and enrollee setup; grade overrides.
- **Notes:** Gated like A-SCH. If A-SCH moved here, it is done first in this package.

### Wave C2

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-NOTES | Procedure notes and group notes | L | 3 | M2-01; files from M2-EST |
| M2-HIST | Insurance history from code groups | L | 2 | M2-01; files from M2-EST, M2-INSV, M2-CAN |
| M2-EF | eForms 25.4 and 26.1 | L | 6 | M2-01, M2-SHEETS |
| M2-DUN | HTML dunning and billing e-mail | M | 1 | M2-01, M2-SDV; files from M2-RECUR |
| V-GP | Global Payments V1 rename and V2 | XL | 2 | M2-01, M2-02; files from M2-RECUR, M2-PAYW |
| V-EDS | EDS enhanced claim validation | M | 1 | –; files from V-DXC2, M2-CLAIMRCV |

#### M2-NOTES: Procedure notes and group notes

- **Size:** L (~9.5 dev-days) · **Wave:** C2 · **Depends on:** M2-01; **takes files over from:** M2-EST
- **Features:** [`v25_4-chart-groupnote-size`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-chart-procnote-merge`](https://www.opendental.com/site/version25_4.html) (M), [`v26_1-chart-groupnote-second-signature`](https://www.opendental.com/site/version26_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlChart
  - `OpenDental/Forms/`: FormProcEdit, FormProcGroup
  - `WpfControlsOD/Frms/`: FrmProcNoteMerge (new)
  - `OpenDentBusiness/Data Interface/`: ProcGroupItems, ProcNotes
  - OpenDentBusiness/UI/SignatureBoxWrapper.cs, WpfControlsOD/UI/Controls/SignatureBoxWrapper.xaml.cs
- **Test approach:** Larger group-note box; merge window on concurrent note edits; second signature saved and shown.

#### M2-HIST: Insurance history from code groups

- **Size:** L (~14 dev-days) · **Wave:** C2 · **Depends on:** M2-01; **takes files over from:** M2-EST, M2-INSV, M2-CAN
- **Features:** [`v26_1-setup-codegroup-history-options`](https://www.opendental.com/site/version26_1.html) (M), [`v26_1-family-inshist-codegroups`](https://www.opendental.com/site/version26_1.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ClaimProcs, CodeGroups, InsVerifies, Prefs, ProcedureCodes, Procedures
  - `OpenDental/Main Modules/`: ControlFamily
  - `OpenDentBusiness/X12/`: EB271
  - `OpenDental/Forms/`: FormCodeGroupEdit, FormCodeGroups, FormInsHistSetup, FormInsPlan
  - `OpenDental/User Controls/Preferences/`: UserControlTreatPlanGeneral
- **Test approach:** Migrate the ten InsHist*Codes preferences into code groups (keep the preference rows); history grid; 271 mapping.
- **Notes:** How Open Dental migrated these preferences is a 26.1 open question; check it on the reference install. The migration itself is a new step file under `ForkMigrations/`.

#### M2-EF: eForms 25.4 and 26.1

- **Size:** L (~14 dev-days) · **Wave:** C2 · **Depends on:** M2-01, M2-SHEETS
- **Features:** [`v25_4-eforms-patfields`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-eforms-richtext-paste`](https://www.opendental.com/site/version25_4.html) (M), [`v25_4-eforms-multi-import`](https://www.opendental.com/site/version25_4.html) (S), [`v26_1-chart-eform-sig-datetime`](https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml) (S), [`v26_1-misc-audit-eform-delete`](https://www.opendental.com/site/version26_1.html) (S), [`v25_4-sheets-datetimesubmitted`](https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `WpfControlsOD/UI/Controls/UserControls/`: CtrlEFormFill
  - `OpenDentBusiness/Data Interface/`: EFormFields, EForms, PatFieldDefs, PatFields
  - `OpenDentBusiness/EForms/`: EFormFieldsAvailable, EFormFiller, EFormImport
  - `WpfControlsOD/Logic/`: EFormL
  - `OpenDental/Forms/`: FormSheetFillEdit
  - `WpfControlsOD/Frms/`: FrmEFormDefs, FrmEFormFieldPicker, FrmEFormFillEdit, FrmEFormLabelEdit, FrmEFormSigBoxEdit
- **Test approach:** Patient fields in eForms; rich-text labels; multi-import; signature time; date submitted on sheets and eForms; audit entry on eForm delete.

#### M2-DUN: HTML dunning and billing e-mail

- **Size:** M (~4 dev-days) · **Wave:** C2 · **Depends on:** M2-01, M2-SDV; **takes files over from:** M2-RECUR
- **Features:** [`v26_1-manage-dunning-html`](https://www.opendental.com/site/version26_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Logic/`: BillingL
  - `OpenDentBusiness/Data Interface/`: Dunnings, EmailMessages, Statements
  - `OpenDental/Forms/`: FormBillingDefaults, FormBillingOptions, FormDunningEdit, FormDunningSetup, FormEmailTemplateEdit
- **Test approach:** Dunning e-mails in each EmailType render.

### Wave C3

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-INSPLAN | Edit Insurance Plan layout overhaul | M | 3 | M2-TERM, M2-HIST; files from M2-INSV |
| M2-CARRIER | Carrier preauthorization flags and long insurance log values | L | 2 | M2-01, M2-02; files from M2-EST, M2-NOTES |
| V-BRIDGE2 | Trophy Enhanced tags, Clever One, Clerri | M | 3 | M2-02; files from V-PEARL2, M2-AUDIT, M2-HIST |

#### M2-INSPLAN: Edit Insurance Plan layout overhaul

- **Size:** M (~7 dev-days) · **Wave:** C3 · **Depends on:** M2-TERM, M2-HIST; **takes files over from:** M2-INSV
- **Features:** [`v26_1-family-insplan-layout`](https://www.opendental.com/site/version26_1.html) (M), [`v25_4-family-writeoff-override-wording`](https://www.opendental.com/site/version25_4.html) (S), [`v26_1-tools-audit-insplan-drop`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormInsPlan
  - `OpenDentBusiness/Data Interface/`: PatPlans
- **Test approach:** Every control of the old window is still reachable; drop logs the PlanNum.
- **Notes:** Done after every other change to this window, so the layout is rebuilt once.

#### M2-CARRIER: Carrier preauthorization flags and long insurance log values

- **Size:** L (~11.5 dev-days) · **Wave:** C3 · **Depends on:** M2-01, M2-02; **takes files over from:** M2-EST, M2-NOTES
- **Features:** [`v26_1-lists-carrier-preauth-flags`](https://www.opendental.com/site/version26_1.html) (L), [`v25_4-family-inseditlog-bigvalues`](https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: Appointments, Carriers, InsEditLogs, InsEditPatLogs
  - `OpenDentBusiness/TableTypes/`: ApptViewItem
  - `OpenDental/Logic/`: ApptViewItemL
  - `OpenDental/UI/Appt/`: ControlApptPanel
  - `OpenDental/Main Modules/`: ControlChart, ControlTreat
  - `OpenDental/Forms/`: FormApptViewEdit, FormCarrierCombine, FormCarrierEdit, FormInsEditLogs, FormInsEditPatLog
- **Test approach:** Preauth codes parsed with the shared range parser; reminders in the chart, TP and appointment views; long values kept in the insurance logs.

### Wave C4

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-SHELL | Main window and cross-module audit (26.1) | L | 6 | M2-CHAT, M2-APPT, M2-NOTES, M2-CARRIER |
| V-PAYSUITE | PaySuite for Canadian claim payments | XL | 3 | M2-01, M2-02; files from M2-CAN, M2-UDS, V-EDS, M2-CARRIER, M2-INSPLAN |

#### M2-SHELL: Main window and cross-module audit (26.1)

- **Size:** L (~9 dev-days) · **Wave:** C4 · **Depends on:** M2-CHAT, M2-APPT, M2-NOTES, M2-CARRIER
- **Features:** [`v25_4-security-patientselected-audit`](https://www.opendental.com/OpenDentalDocumentation25-4.xml) (S), [`v26_1-misc-audit-billing-finance`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-tools-dbm-duplicate-blockouts`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-manage-chat-autolaunch`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-chart-ptinfo-gender`](https://www.opendental.com/site/version26_1.html) (S), [`v26_1-misc-audit-chartviews`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ChartViews, Schedules
  - `OpenDental/Main Modules/`: ControlChart, FormOpenDental, FormOpenDentalMenus
  - `OpenDental/Forms/`: FormBlockoutDuplicatesFix, FormChartView, FormFinanceCharges
  - OpenDentBusiness/Data Interface/DatabaseMaintenances.Schedules.cs (new partial file)
- **Test approach:** Patient-selected audit entry; Billing and Finance Charges audit; DBM duplicate-blockout check replaces the standalone tool; chat opens on unread messages; Gender display field; chart view audit.
- **Notes:** Last general wave of M2.

### Wave C5

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-SCH2 | Dental school: requirements and approvals (gated) | XL | 2 | M2-SCH1, M2-NOTES; files from M2-CARRIER, M2-SHELL |

#### M2-SCH2: Dental school: requirements and approvals (gated)

- **Size:** XL (~20 dev-days) · **Wave:** C5 · **Depends on:** M2-SCH1, M2-NOTES; **takes files over from:** M2-CARRIER, M2-SHELL
- **Features:** [`v25_4-dentalschool-requirements`](https://www.opendental.com/site/version25_4.html) (L), [`v25_4-dentalschool-approvals`](https://www.opendental.com/site/version25_4.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlChart, ControlTreat
  - `OpenDental/Forms/`: FormApptEdit, FormProcEdit, FormReqAppt, FormReqNeededEdit, FormReqNeededs, FormReqStudentEdit, FormReqStudentOne, FormReqStudentsMany
  - `OpenDentBusiness/Data Interface/`: ReqNeededs, ReqStudents, SchoolApprovals (new)
- **Test approach:** Requirements satisfied by procedures; instructor approvals.

### Wave C6

| Package | Title | Size | Features | Depends on |
|---|---|---|---|---|
| M2-SCH3 | Dental school: planners (gated) | L | 1 | M2-SCH2, M2-SHELL |

#### M2-SCH3: Dental school: planners (gated)

- **Size:** L (~10 dev-days) · **Wave:** C6 · **Depends on:** M2-SCH2, M2-SHELL
- **Features:** [`v25_4-dentalschool-planners`](https://www.opendental.com/site/version25_4.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: FormOpenDental, FormOpenDentalMenus
  - `WpfControlsOD/Frms/`: FrmDentalSchoolPlannerInstructor (new), FrmDentalSchoolPlannerStudent (new)
- **Test approach:** Student and instructor planners.

### M2 gate (M2-Z): 26.1 validation

- **Size:** M (~6 dev-days) · **Depends on:** every M2 package
- **Work:** run the M2 migration on a fresh copy of the adopted office database; schema check against the 26.1 reference; regression run of the M1 validation suite; a short parallel run before releasing M2 to the office.

## Vendor-direct integrations

These talk to the vendor directly from the desktop, with the office's own account (decoupling plan, ground rules 6 and 7: off by default, no promotion, shown only after the office enables them). They are kept apart from the milestones because each needs a vendor contract, test credentials and the vendor's API documentation. Each has a wave slot so it never edits a file that a desktop package owns at the same time.

| Package | Integration | Feature IDs | Milestone / wave slot | Needed before the switch? | Vendor prerequisites |
|---|---|---|---|---|---|
| V-PC | PayConnect 2 (card processing) | [`v25_1-account-payconnect2-receipt-cardholder`](https://www.opendental.com/site/version25_1.html), [`v25_1-account-payconnect2-terminal-memory`](https://www.opendental.com/site/version25_1.html), [`v25_2-account-payconnect-saved-card-field`](https://www.opendental.com/site/version25_2.html), [`v25_3-account-payconnect-error-log`](https://www.opendental.com/site/version25_3.html) | M1 / A1 | Yes, if the office uses PayConnect (stored cards, terminals) | PayConnect merchant test account; decoupling removes HQ from PayConnect credentials |
| V-DXC1 | DentalXChange claim attachments and XConnect validation | [`v24_4-claims-dxc-attachment-submitted`](https://www.opendental.com/OpenDentalDocumentation24-4.xml), [`v25_2-account-xconnect-claim-validation`](https://www.opendental.com/site/version25_2.html) | M1 / A1 | Yes, if the office uses DentalXChange | DentalXChange API documentation and test account |
| V-BRIDGE1 | Overjet, DTX Studio, Insurance Answers Plus program links | [`v25_1-setup-overjet-bridge`](https://www.opendental.com/site/version25_1.html), [`v25_2-bridges-dtx-studio`](https://www.opendental.com/site/version25_2.html), [`v25_2-family-iaplus-bridge-update`](https://www.opendental.com/site/version25_2.html) | M1 / B1 | No | Vendor launch syntax; program-link gating by HQ removed (decoupling Phase 1) |
| V-PEARL1 | Pearl AI request statuses | [`v24_4-imaging-pearl-request-statuses`](https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml) | M1 / B2 | No (values already load after M1-02) | None new; the Pearl bridge already calls Pearl directly |
| V-DXC2 | DentalXChange PDF attachments | [`v26_1-account-dxc-attach-pdf`](https://www.opendental.com/site/version26_2.html) | M2 / C1 | No | DentalXChange test account |
| V-PEARL2 | Pearl troubleshooting log level | [`v26_1-imaging-pearl-logging`](https://www.opendental.com/site/version26_1.html) | M2 / C1 | No | None |
| V-GP | Global Payments V1 rename and V2 (terminals, tap payments) | [`v25_4-payments-globalpayments-v1-rename`](https://www.opendental.com/site/version25_4.html), [`v25_4-payments-globalpayments-v2`](https://www.opendental.com/site/version25_4.html) | M2 / C2 | No | Global Payments sandbox and docs |
| V-EDS | EDS enhanced claim validation | [`v26_1-manage-eds-claim-validation`](https://www.opendental.com/site/version26_1.html) | M2 / C2 | No | EDS documentation for the extra checks |
| V-BRIDGE2 | Trophy Enhanced tags, Clever One, Clerri | [`v25_4-bridges-trophy-enhanced-tags`](https://www.opendental.com/site/version25_4.html), [`v25_4-bridges-cleverone`](https://www.opendental.com/site/version25_4.html), [`v25_4-family-clerri`](https://www.opendental.com/site/version25_4.html) | M2 / C3 | No | Clerri: confirm value without Open Dental data sync |
| V-PAYSUITE | PaySuite (Canadian claim payments) | [`v25_4-canada-paysuite-setup`](https://www.opendental.com/site/version25_4.html), [`v25_4-canada-paysuite-posting`](https://www.opendental.com/site/paysuitelogic.html), [`v25_4-canada-paysuite-reports`](https://www.opendental.com/site/version25_4.html) | M2 / C4 | No (Canada only) | PaySuite contract; the fork's local background service host |

Related desktop items that are not vendor-direct: v25_2-bridges-dxc-credit-score-removed (removal, in B-SHELL) and v24_4-reports-dailypayments-merchant-fees (report option, in B-ACCT). CareCredit is cut by the decoupling plan, so its items are excluded.

### V-PC: PayConnect 2 (direct)

- **Size:** M (~6 dev-days) · **Wave:** A1 · **Depends on:** M1-01
- **Features:** [`v25_1-account-payconnect2-receipt-cardholder`](https://www.opendental.com/site/version25_1.html) (S), [`v25_1-account-payconnect2-terminal-memory`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-account-payconnect-saved-card-field`](https://www.opendental.com/site/version25_2.html) (S), [`v25_3-account-payconnect-error-log`](https://www.opendental.com/site/version25_3.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ComputerPrefs, CreditCards, PayTerminals, RecurringCharges
  - `OpenDental/Forms/`: FormCreditCardEdit, FormCreditRecurringCharges, FormPayConnect, FormPayConnect2, FormPayConnect2iFrame, FormPayConnectSetup, FormPayment
  - `CodeBase/Utilities/`: Logger
  - `OpenDentBusiness/WebBridges/`: PayConnect, PayConnect2
  - `OpenDental/Logic/`: PayConnectL
- **Test approach:** Against PayConnect's test environment with the office's own credentials: cardholder on receipts, last terminal remembered per workstation, saved-card field, errors logged locally.
- **Notes:** Needed before the switch if the office uses PayConnect (the census shows stored cards and terminals). Depends on the decoupling work that removes HQ from PayConnect credentials.

### V-DXC1: DentalXChange attachments and XConnect validation

- **Size:** L (~11.5 dev-days) · **Wave:** A1 · **Depends on:** M1-01, M1-02
- **Features:** [`v24_4-claims-dxc-attachment-submitted`](https://www.opendental.com/OpenDentalDocumentation24-4.xml) (S), [`v25_2-account-xconnect-claim-validation`](https://www.opendental.com/site/version25_2.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ClaimAttaches, Clearinghouses
  - `OpenDentBusiness/Eclaims/`: ClaimConnect, Eclaims
  - `OpenDental/Logic/`: ClaimL
  - `OpenDental/Forms/`: FormClaimAttachHistory, FormClaimAttachPasteDXC, FormClaimAttachSnipDXC, FormClaimAttachment, FormClaimEdit, FormClaimInstEdit, FormClaimPrint, FormClaimsSend, FormClearinghouseEdit
  - `OpenDentBusiness/X12/`: X837_4010, X837_5010
- **Test approach:** DentalXChange test account: attachment "Submitted" flag; XConnect validation on claim save and send.
- **Notes:** Needed before the switch if the office uses DentalXChange (`clearinghouse.EnableXConnect`). Needs DentalXChange API documentation (25.2 open question 6).

### V-BRIDGE1: Overjet, DTX Studio, Insurance Answers Plus

- **Size:** M (~7 dev-days) · **Wave:** B1 · **Depends on:** none inside this backlog; **takes files over from:** A-EF2
- **Features:** [`v25_1-setup-overjet-bridge`](https://www.opendental.com/site/version25_1.html) (S), [`v25_2-bridges-dtx-studio`](https://www.opendental.com/site/version25_2.html) (M), [`v25_2-family-iaplus-bridge-update`](https://www.opendental.com/site/version25_2.html) (S)
- **Owns (exclusive while the wave runs):**
  - `WpfControlsOD/Bridges/`: DTXStudio (new), Iap, Overjet (new)
  - `OpenDental/Bridges/`: FormIap
  - `OpenDental/Forms/`: FormInsPlan, FormProgramLinkEdit
  - `WpfControlsOD/Logic/`: ProgramL
- **Test approach:** Launch each bridge with test settings; Insurance Answers Plus opens the vendor web page.
- **Notes:** Program rows and names come from M1-02. Bridges stay hidden until the office enables them (ground rule 7).

### V-PEARL1: Pearl request statuses

- **Size:** S (~1.5 dev-days) · **Wave:** B2 · **Depends on:** none inside this backlog; **takes files over from:** B-IMG
- **Features:** [`v24_4-imaging-pearl-request-statuses`](https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Main Modules/`: ControlImages
  - `OpenDentBusiness/Bridges/Pearl/`: Pearl, PearlApiClient
  - `OpenDentBusiness/Data Interface/`: PearlRequests
  - `UnitTests/UnitTests/`: PearlTests
- **Test approach:** PearlTests for the three new statuses.
- **Notes:** Stored values already load after M1-02; this adds display and retry handling.

### V-DXC2: DentalXChange PDF attachments

- **Size:** M (~4 dev-days) · **Wave:** C1 · **Depends on:** none inside this backlog
- **Features:** [`v26_1-account-dxc-attach-pdf`](https://www.opendental.com/site/version26_2.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Eclaims/`: ClaimConnect
  - `OpenDental/Forms/`: FormClaimAttachment, FormClaimAttachmentItemEdit, FormImagePickerDXC
- **Test approach:** Send a PDF attachment through the DentalXChange test account.

### V-PEARL2: Pearl troubleshooting log level

- **Size:** S (~1.5 dev-days) · **Wave:** C1 · **Depends on:** none inside this backlog
- **Features:** [`v26_1-imaging-pearl-logging`](https://www.opendental.com/site/version26_1.html) (S)
- **Owns (exclusive while the wave runs):**
  - `OpenDental/Forms/`: FormProgramLinkEdit
  - `CodeBase/Utilities/`: Logger
  - `OpenDentBusiness/Bridges/Pearl/`: Pearl, PearlApiClient
- **Test approach:** Log level property controls local logging only.

### V-GP: Global Payments V1 rename and V2

- **Size:** XL (~21.5 dev-days) · **Wave:** C2 · **Depends on:** M2-01, M2-02; **takes files over from:** M2-RECUR, M2-PAYW
- **Features:** [`v25_4-payments-globalpayments-v1-rename`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-payments-globalpayments-v2`](https://www.opendental.com/site/version25_4.html) (XL)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: ComputerPrefs, PayTerminals, RecurringCharges
  - `OpenDentBusiness/WebBridges/`: EdgeExpress, GlobalPaymentsV2 (new)
  - `OpenDental/Forms/`: FormCreditRecurringCharges, FormEdgeExpressSetup, FormEdgeExpressTrans, FormGlobalPaymentsV2Setup (new), FormPayTerminalEdit, FormPayment
  - GP V2 transaction window (new)
- **Test approach:** Global Payments sandbox: terminal and tap payments, recurring charges, refunds.
- **Notes:** Needs vendor documentation and a merchant test account.

### V-EDS: EDS enhanced claim validation

- **Size:** M (~4 dev-days) · **Wave:** C2 · **Depends on:** none inside this backlog; **takes files over from:** V-DXC2, M2-CLAIMRCV
- **Features:** [`v26_1-manage-eds-claim-validation`](https://www.opendental.com/site/version26_1.html) (M)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Eclaims/`: EDS, Eclaims
  - `OpenDental/Forms/`: FormClaimAttachment, FormClaimEdit, FormClaimsSend
- **Test approach:** EDS test account: validation results on save and send.
- **Notes:** Which extra checks 26.1 runs is not public (26.1 open question 4).

### V-BRIDGE2: Trophy Enhanced tags, Clever One, Clerri

- **Size:** M (~7 dev-days) · **Wave:** C3 · **Depends on:** M2-02; **takes files over from:** V-PEARL2, M2-AUDIT, M2-HIST
- **Features:** [`v25_4-bridges-trophy-enhanced-tags`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-bridges-cleverone`](https://www.opendental.com/site/version25_4.html) (S), [`v25_4-family-clerri`](https://www.opendental.com/site/version25_4.html) (M)
- **Owns (exclusive while the wave runs):**
  - `WpfControlsOD/Bridges/`: Clerri (new), CleverOne (new), TrophyEnhanced
  - `OpenDental/Main Modules/`: ControlFamily
  - `OpenDental/Forms/`: FormProgramLinkEdit
  - `OpenDentBusiness/Data Interface/`: Patients
  - `WpfControlsOD/Logic/`: ProgramL
- **Test approach:** Launch tests with sample command lines.
- **Notes:** Clerri: ask the vendor whether a plain launch helps without Open Dental's data sync (25.4 open question 10).

### V-PAYSUITE: PaySuite for Canadian claim payments

- **Size:** XL (~40 dev-days) · **Wave:** C4 · **Depends on:** M2-01, M2-02; **takes files over from:** M2-CAN, M2-UDS, V-EDS, M2-CARRIER, M2-INSPLAN
- **Features:** [`v25_4-canada-paysuite-setup`](https://www.opendental.com/site/version25_4.html) (L), [`v25_4-canada-paysuite-posting`](https://www.opendental.com/site/paysuitelogic.html) (XL), [`v25_4-canada-paysuite-reports`](https://www.opendental.com/site/version25_4.html) (L)
- **Owns (exclusive while the wave runs):**
  - `OpenDentBusiness/Data Interface/`: AlertItems, ClaimPayments, Deposits, DisplayReports, PaySuitePayments (new)
  - `OpenDentBusiness/Eclaims/`: Canadian
  - `OpenDental/Forms/`: FormCarrierEdit, FormClaimEdit, FormInsPlan, FormPaySuiteSetup (new), FormProgramLinks, FormReportsMore
  - PaySuite expected-payments window (new)
  - PaySuite reconciliation window (new)
  - the fork's local background service host (decoupling plan, rule 6)
  - OpenDentBusiness/WebBridges/PaySuite.cs (new API client)
- **Test approach:** PaySuite test environment: carrier capabilities, automatic posting, reconciliation windows.
- **Notes:** Canada only. Needs the fork's local background service host. Lowest priority for a US office.

## Excluded

165 items are out of scope. None needs feature work, but where the office data holds related columns, tables or enum values, the schema packages still create them so the data loads (listed at the end of this section). Full per-item reasons are in the Excluded section of each spec file.

| Version | Excluded | Open Dental hosted services and HQ | Local REST API | Other | Source |
|---|---|---|---|---|---|
| 24.4 | 32 | 8 | 23 | 1 HQ eService codes | [24.4 release notes](https://www.opendental.com/site/version24_4.html) |
| 25.1 | 22 | 8 | 13 | 1 API Service installer | [25.1 release notes](https://www.opendental.com/site/version25_1.html) |
| 25.2 | 32 | 12 | 16 | 3 CareCredit, 1 HQ-only permission | [25.2 release notes](https://www.opendental.com/site/version25_2.html) |
| 25.3 | 23 | 9 | 11 | 1 CareCredit, 2 HQ-only (triage position, HQ enums) | [25.3 release notes](https://www.opendental.com/site/version25_3.html) |
| 25.4 | 34 | 8 | 24 | 1 HQ-granted clinic access, 1 HQ-only enums | [25.4 release notes](https://www.opendental.com/site/version25_4.html) |
| 26.1 | 22 | 9 | 11 | 1 Topaz under Open Dental Cloud, 1 `deletedobj` writers | [26.1 release notes](https://www.opendental.com/site/version26_1.html) |
| **Total** | **165** | **54** | **98** | **13** | |

**By reason:**

- **Open Dental hosted services:** eClipboard, eClipboard Web and its licences, OCR and branding; ODTouch; Web Sched (recall, ASAP, existing patient); Web Forms and the Sheets mobile layout; integrated texting, Message-to-Pay, Secure Email and Mass Email; the Payment Portal; automated messaging; eConnector alerts; BetterDiagnostics, which is signed up for and billed through Open Dental; the BYOD and Kiosk Manager changes.
- **HQ support, licensing and internal tools:** the customer-gated manual, the Support Status QR code, HQ-granted clinic access (`registrationkey.DateTClinicAccess`), HQ triage position on tasks, HQ-only permissions and enum values (TextAllEmployees, PhoneExtension, LogScraperHQ, Job Manager and map settings), HQ eService codes.
- **Open Dental Cloud:** Topaz pads under Thinfinity or AppStream.
- **Local REST API:** 98 endpoint and subscription items, the always-installed API Service, and the writers of the 26.1 `deletedobj` table. The decoupling plan still lists the local API as an open decision; if it is kept, these come back as desktop items with no Open Dental dependency.
- **Cut vendor:** CareCredit (4 items), which the decoupling plan removes.
- **Licensed content delivered by Open Dental:** the 2026 ODA and BC fee guides. Offices can still import fee schedules themselves; 2026 CDT codes stay in scope as a user import (B-PROC).

**Kept only for data compatibility** (created by M1-01 or M2-01, never used by features): `imagedraw.BetterDiagLayer`, `branding` (and its 26.1 widening), `eclipboardsheetdef.SheetDefNumsConsidered`, `asapcomm.UserNum`, `task.TriagePosition` and `taskhist.TriagePosition`, `registrationkey.DateTClinicAccess`, `smstomobile.SecUserNumEntry`, `deletedobj`, and every reserved enum value in Appendix A of the schema spec.

## Risks

### R1. Version numbers: the office database could force a "downgrade" or install Open Dental binaries

**What happens today.** Our 24.3.63 startup runs two checks, in this order (`OpenDental/Main Modules/FormOpenDental.cs` near line 1525, then line 1541):

1. `PrefL.ConvertDB` calls `ClassConvertDatabase.Convert(DataBaseVersion, Application.ProductVersion)`. When the database version is higher than the program version it returns "nothing to do" and leaves the problem to the next check. When the database is lower, the update workstation runs every `ToX_Y_Z` method in `ConvertDatabases*.cs` above the database version.
2. `PrefL.CheckProgramVersion` compares the `ProgramVersion` preference with the program version (it is skipped in Debug builds).
   - Stored version higher: it reads the zipped `UpdateFiles` rows from `documentmisc`, copies those binaries over the local install and restarts.
   - Stored version lower, and this is not the update workstation: it offers to "downgrade" to the server version, again from `documentmisc`.
   - Stored version lower on the update workstation: it zips the local install into `documentmisc`, writes `ProgramVersion`, rehashes, and tries to install the Open Dental Service, eConnector and API service.

**Why that breaks adoption.** The office database says 25.3.48 in both preferences and holds Open Dental 25.3.48 binaries in `documentmisc`. A fork build numbered 24.3.x would skip our conversion and then replace itself with Open Dental binaries. A fork build numbered above 25.3.48 would skip our re-implemented 24.4 to 25.3 steps, push its own binaries into `documentmisc`, and every Open Dental workstation still running would quietly "update" itself to the fork. And if an Open Dental 25.4 or later client ever connects, its own conversion scripts would run against our schema.

**Strategy (built in M1-03, with the runner from M1-01):**

1. **Separate the two meanings of a version.** The fork has its own product version (its own scheme, never compared with Open Dental numbers) and its own markers, stored as new preference rows (data only, no schema change):
   - `ForkSchemaLevel`: the last applied fork migration step.
   - `ForkProgramVersion`: the fork build that last upgraded the database, used for our own workstation update propagation.
   - `OdSchemaCompat`: the Open Dental schema the database matches exactly ("25.3.48" after M1, "26.1.70" after M2), checked by the schema verifier.
   - `ForkAdoptedFrom`: a record of the adoption (source version and date).
   Names are proposals; they must not clash with any Open Dental preference name.
2. **Freeze the old chain.** The `ToX_Y_Z` reflection chain stays frozen at 24.3.63 for importing older databases. Everything after that runs in the fork migration runner, keyed by `ForkSchemaLevel`. The re-implemented Open Dental steps (24.4 through 25.3.48, later 25.4 through 26.1.70) are idempotent fork steps, so the same code upgrades a 24.3.63 database and leaves the office database unchanged.
3. **Adopt by allowlist, explicitly.** A database without fork markers is adopted only when its `DataBaseVersion` is on the allowlist: 24.3.63.0 (our own development databases) and exactly 25.3.48.0 (the office). Any other Open Dental version, for example 25.3.50 or 25.4.x, is refused with a clear message until a schema diff proves it equal. Normal startup never adopts; an administrator runs the adoption tool against a restored copy (M1-Z).
4. **Never touch Open Dental update files.** Adoption deletes the Open Dental `UpdateFiles` and `UpdateFilesSegment` rows from the copy and blanks the update and HQ address preferences (decoupling plan, Phase 1, step 5). Our own update files use new `DocumentMiscType` values far from Open Dental's numbers, carry a manifest that names our product, and are checked by SHA-256 before use. The fork never offers a downgrade.
5. **Lock out Open Dental clients.** After adoption, `ProgramVersion` and `DataBaseVersion` are set to a sentinel that no Open Dental release will reach (for example 99.0.0.0). An Open Dental client then skips conversion (database "newer"), finds no update files to pull, and exits without converting the schema or installing anything. Confirm this once on a copy, with the owner's agreement to run the licensed client for that test (R3). The fork itself never reads those two preferences except to detect an unadopted database.
6. **Debug builds are guarded too.** Open Dental skips the version check in Debug builds. The fork must not, so a developer build pointed at an office copy cannot adopt or convert it by accident.
7. **Rollback is a restore.** Because of the lock-out, a mixed fleet is impossible; all workstations switch together, and going back means restoring the pre-adoption backup (M1-Z, step 6).

Other code that reads these preferences must follow the same rule: `FormOpenDental.IsDbConnectionSafe` (near line 3893), `UpdateHistories`, `UpdateFileCopier/`, and any service or tool that compares versions. Decoupling Phase 1 already removes the HQ update check and the service installers from `PrefL.cs`; M1-03 builds on that branch.

### R2. Data-migration validation

The office data must arrive complete, unchanged where it should be unchanged, and correctly interpreted. Checks, all on restored copies (never the live database) and all part of M1-Z:

1. **Schema.** The verifier compares the migrated copy with `fox-db-schema.sql`: tables, columns, types, NULL and defaults, indexes, engine and charset. Known exceptions are listed explicitly. **Decision needed:** `patient._portal_active_email` is not Open Dental schema; its unique key rejects a second active patient with the same e-mail address, which is normal in families. Something outside Open Dental depends on it. Keep it, or drop the column and index during adoption, before M1-Z starts.
2. **Drift.** `claimpayment.Note` is still `varchar(255)` in 25.3.48 although the docs say text. M1 keeps it as is for an exact match; M2-01 widens it.
3. **Row and content checks.** Row counts per table before and after adoption. `CHECKSUM TABLE` for every table the migration should not touch. For touched tables, per-column checksums over the columns that should not change. Allowed differences are listed: removed Open Dental update files, preference rows, rehashed `SecurityHash` columns.
4. **No double conversion.** Open Dental already converted the office data. Steps that move data must not run again: the guardian relationship renumbering, the `statement.ShowTransSinceBalZero` back-fill, and permission grants such as PayPlanUnlock and BlockoutsFlagged. Test this by running the migration on the office copy and asserting that those tables are byte-identical.
5. **Meaning of stored values.** Every enum value found by the census must exist in our code with the same number. Open points to settle first: `EnumPermType` 272 (PhoneExtension in the 25.3 diff, BlockoutAdd in 25.4) and `EnumBrandingType` 1 (LogoFilePath versus LogoImage). Preference, report and program key names come from the census, so office settings are not silently ignored.
6. **Hashes.** Open Dental's `SecurityHash` values cannot be checked by our code (the algorithm is proprietary). Adoption rehashes every row with our HMAC after the census and checksums have shown the data is as expected. Record the counts and the decision, because rehashing accepts the data as it stands.
7. **Business results.** Golden totals from Open Dental 25.3.48 for fixed dates (aging, production and income, sample insurance estimates, payment-plan balances, appointments per day) must match the fork on the migrated copy. Fee effective dates, tooth-range exclusions and code-group ranges are the most likely sources of estimate differences.
8. **UI smoke.** Open sample patients in every module, plus office records that use the new features: dated fees, chart letters, mirrored appointments, eForms filled by eClipboard, pending insurance, group notes without procedures, Check sheets.
9. **Engine limits.** All tables are MyISAM: no transactions, table locks during ALTER. Run from a fresh backup, time every step on a full-size copy, and use the large-table helpers for `procedurelog`, `claimproc` and similar tables (also in M2).
10. **Privacy.** Restored copies and census output stay on local machines. Census output holds counts and names only. Nothing from the office database is committed.

### R3. Behavior rebuilt from public sources

The 24.4+ manual is customer-only, so many items are marked Inferred, VERIFY or Low confidence in the specs. Mitigation: settle names and numbers from the office data (M1-00) and from a 26.1 reference install (M2). Whether engineers may watch the office's licensed Open Dental 25.3 in ordinary use (not decompiling) is an open owner decision (25.2 spec, open question 4); several golden comparisons in R2 depend on it.

### R4. Hot files and merge pressure

A few files are touched by many features: `Procedures.cs`, `ClaimProcs.cs`, the main module controls, `FormOpenDental.cs`, `FormInsPlan.cs`, `GridOD.cs` and `DatabaseMaintenances.cs`. The waves keep each of them single-owner at any time (Appendix B shows the hand-offs), at the cost of a few serial steps. Breaking the new-file rule would undo this.

### R5. Coordination with the decoupling work

Other branches are cutting HQ calls, replacing the proprietary DLLs and removing eServices. Dependencies: M1-03 needs Phase 1 (`PrefL.cs`); A-HASH needs the Phase 2 HMAC; v25_3-procs-cdt-2026 needs the Phase 2 CDT replacement; A-EF1 removes the eForms gate in `FormEServicesEClipboard.cs`, which Phase 3 deletes; V-PC needs PayConnect credentials without HQ; V-PAYSUITE needs the local service host; v25_4-misc-diagnostics-info needs a home outside `BugSubmissions`. Merge order matters more than dates here.

### R6. Patch-level timing

The docs are per minor version. An item marked 25.3 may have shipped in any 25.3.x build, and some backports landed after the office's 25.3.48 (25.3.53, 25.3.54, 25.3.78). Those are in Tier B. The census confirms what the office data really contains.

### R7. Inferred 26.1 schema

Only types are published for 25.4 and 26.1. NOT NULL, defaults and indexes are inferred. M2-01 checks them against a 26.1 reference install before any office copy is migrated.

### R8. Product decisions that change scope

- Ship the chat program at all (A-CHAT, M2-CHAT).
- Keep the local REST API (would bring back 98 excluded items).
- Dental-school features for a private practice (M2-SCH1 to M2-SCH3, about a third of M2).
- Which vendor integrations to keep and fund (vendor list).

## Appendix A: feature index

All 241 items, in spec order. Size is the spec's item size. Source is the main public page for the item; every spec section lists the rest.

| Feature ID | Title | Size | Package | Wave | Source |
|---|---|---|---|---|---|
| `v24_4-eforms-desktop-forms` | eForms: designer and in-office filling | M | A-EF1 | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-account-payplan-sheet-select` | Choose the sheet used to print each payment plan | M | A-PAY | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-account-salestax-proc` | Add Proc > Sales Tax adds one sales-tax procedure | M | B-ACCT | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-appts-copy-apptview` | Copy View button for appointment views | S | B-APPT | B2 | https://www.opendental.com/site/version24_4.html |
| `v24_4-chart-perio-implant-removed` | Hide implant marker in Perio Chart when the implant is removed | S | B-PERIO | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-chart-perio-attached-gingiva` | Auto-calculated Attached Gingiva row in Perio Chart | M | B-PERIO | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-chart-perio-recession-term` | Perio Setup option: "Recession" instead of "Gingival Margin" | S | B-PERIO | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-chart-chart-letters` | Chart Letters (Word documents in the Chart module, versioned and hashed) | XL | A-CL | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-manage-accounting-export` | Accounting Transaction History export as .csv or .txt | S | B-PRINT | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-manage-era-mismatch-match` | Match an ERA to a claim despite fee or date-of-service mismatch | S | B-ERA | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-tools-querymonitor-filters` | Query Monitor filter groups | M | A-QM | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-imaging-signature-permissions` | Permissions ImageSignatureCreate and SignedImageEdit | M | A-IMG | A2 | https://www.opendental.com/site/version24_4.html |
| `v24_4-setup-fee-effective-date` | Effective dates for fees | XL | A-FEE | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-setup-feesched-notes` | Notes on fee schedules | M | A-FEE | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-reports-incomplete-procnotes-all` | Incomplete Procedure Notes: include every procedure with no note | S | B-SERVER | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-setup-reportserver-audit` | Audit Trail entry when the Report Server setting changes | S | B-SERVER | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-setup-cda-codes-2025` | 2025 CDA procedure codes | S | B-PROC | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-tools-rawemails-unicode` | Raw Emails tool also cleans Unicode white space | S | B-DBM | B2 | https://www.opendental.com/site/version24_4.html |
| `v24_4-reports-daily-procs-split` | Daily Procedures report split into Procedures and Production by Procedure | M | A-RPT | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-reports-individual-procs-goto-patient` | Procedures report: right-click to go to the patient | S | A-RPT | A1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-setup-codegroup-ranges` | Code ranges (e.g. D2391-D2394) in Code Groups | M | A-INS | A2 | https://www.opendental.com/site/version24_4.html |
| `v24_4-lists-medication-search` | Better search in the Medications list | S | B-MED | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-sheets-secondary-ins-statictext` | More secondary-insurance static text fields | S | A-SHEET | A2 | https://www.opendental.com/site/version24_4.html |
| `v24_4-reports-dailypayments-merchant-fees` | Daily Payments report: one combined merchant-fee option | S | B-ACCT | B1 | https://www.opendental.com/site/version24_4.html |
| `v24_4-claims-medical-hospitalization-dates` | Hospitalization dates (box 18) on the medical claim form | S | A-SHEET | A2 | https://www.opendental.com/site/version24_4.html |
| `v24_4-imaging-pearl-request-statuses` | Pearl AI: finer request error states | S | V-PEARL1 | B2 | https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml |
| `v24_4-claims-dxc-attachment-submitted` | DentalXChange attachment "Submitted" claim flag | S | V-DXC1 | A1 | https://www.opendental.com/OpenDentalDocumentation24-4.xml |
| `v25_1-account-canada-attachment-history` | Canada claim attachment window with send history | M | B-CLAIM | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-payplan-charge-logic-deprecated` | Retire the "Pay Plan charge logic" choice | M | B-PAY | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-payplan-unlock-permission` | Pay Plan Unlock permission | S | A-PAY | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-payconnect2-receipt-cardholder` | PayConnect 2.0 receipts show cardholder | S | V-PC | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-payplan-completedamt-audit` | Audit trail for Tx Completed Amount changes | S | B-PAY | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-payconnect2-terminal-memory` | PayConnect 2.0 remembers last terminal per workstation | S | V-PC | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-salestax-sort-bottom` | Sales tax rows sort last within their date | S | B-ACCT | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-salestax-procs-on-claims` | Sales tax procedures allowed on claims | S | B-ACCT | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-account-payplan-downpayment-flag` | Flag down-payment charges | S | A-PAY | A1 | https://www.opendental.com/OpenDentalDocumentation25-1.xml |
| `v25_1-account-invoice-adjustment-total` | Invoices store their adjustment total | S | A-STMT | A1 | https://www.opendental.com/OpenDentalDocumentation25-1.xml |
| `v25_1-appt-blockouts-flagged-permission` | Separate permission for flagged blockouts | M | A-APPT | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-appt-multiple-completed-tasks` | Completed tasks no longer block a new appointment task | S | B-APPT | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-family-codegroups-hide-delete` | Hide and delete code groups | M | A-INS | A2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-family-inshist-two-dates` | Two Insurance History dates per category | M | B-INSH | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-family-guardian-relationship-renumber` | Guardian relationship values renumbered | S | M1-01 | F1 | https://www.opendental.com/OpenDentalDocumentation25-1.xml |
| `v25_1-imaging-canada-claim-response-titles` | Clearer titles for saved Canada claim responses | S | B-CLAIM | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-imaging-scanning-setup-details` | Details button in Scanning Setup | S | B-IMG | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-manage-timecard-totals-grid` | Time card period totals as a grid | M | B-TIME | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-manage-era-autosplit-carc` | Auto-split ERA claims that carry denial CARCs | M | B-ERA | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-chart-group-note-flexible` | Group notes with any procedures, or none | M | A-GN | A2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-misc-claimproc-override-audit` | Audit Ins Est and Allowed overrides | S | B-CLAIM | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-setup-programlink-selectedprocnums` | `[SelectedProcNums]` program-link field | S | B-SHELL | B3 | https://www.opendental.com/site/version25_1.html |
| `v25_1-sheets-signature-change-user` | Change the signing user on sheet signature boxes | M | A-SIG | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-sheets-autosave-images` | Automatic copy of saved sheets to Imaging | S | B-PRINT | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-eforms-prefill-from-guarantor` | eForm fields prefill from the guarantor | M | A-EF1 | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-eforms-auto-import-fields` | eForm fields that import on submit | M | A-EF1 | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-eforms-conditional-children-view` | View an eForm parent field's conditional children | S | B-EF | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-misc-dbm-email-invalid-chars` | DBM: invalid characters in email bodies | S | B-DBM | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-misc-medication-hide` | Hide medications in the master list | S | A-MED | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-setup-overjet-bridge` | Overjet program link | S | V-BRIDGE1 | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-misc-wiki-draft-compare` | Compare a wiki draft with the live page | S | B-WIKI | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-misc-dbm-securityhash-rehash-patient` | DBM: rehash the whole patient table | S | A-HASH | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-security-insverify-securityhash` | Integrity hash on insurance verification rows | S | A-HASH | A1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-setup-proccode-morefees-filters` | Provider and clinic filters in More Fees | S | B-PROC | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-setup-feetools-tabs` | Fee Tools window reorganized into tabs | M | B-PROC | B1 | https://www.opendental.com/site/version25_1.html |
| `v25_1-canada-etrans-counters-uncapped` | Canadian transaction counters no longer cap out | S | B-CLAIM | B2 | https://www.opendental.com/site/version25_1.html |
| `v25_1-schema-enum-compat` | Reserve all 25.1 enum values | S | M1-02 | F1 | https://www.opendental.com/OpenDentalDiffDocumentation25-1.xml |
| `v25_2-core-schema-enum-sync` | 25.2 schema, enum and type sync | M | M1-01 | F1 | https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml |
| `v25_2-account-xconnect-claim-validation` | DentalXChange XConnect claim validation | L | V-DXC1 | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-payconnect-saved-card-field` | Read-only card field for saved-card PayConnect payments | S | V-PC | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-payplan-prepay-attach` | Attach Payment Plan Prepay paysplits to patient payment plans | M | B-PAY | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-medical-eob-prompt` | Preference: prompt for Medical EOB on primary claims | S | B-CLAIM | B2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-preauth-eob-attach` | Attach EOBs to preauthorizations | M | A-IMG | A2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-payplan-downpayment-audit` | Audit log for payment plan down payment changes | S | B-PAY | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-prodselect-amount-unpaid` | Select Production: "Amount End" becomes "Amount Unpaid" | S | B-ACCT | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-account-statement-show-trans-since-zero` | Statements remember "Show transactions since zero balance" | S | A-STMT | A1 | https://www.opendental.com/OpenDentalDocumentation25-2.xml |
| `v25_2-appts-provider-timebars-op-background` | Preference: provider time bars on operatory background | M | B-APPT | B2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-appts-waiting-room-window` | Waiting Room in a separate window | M | B-APPT | B2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-chart-medical-rows-bold` | Bold labels for Med Urgent and Medical Summary rows | S | B-SHELL | B3 | https://www.opendental.com/site/version25_2.html |
| `v25_2-chart-eform-allergy-other-convert` | Convert eForm "Other" allergies to listed allergies | M | B-MED | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-chart-eform-problem-other-convert` | Convert eForm "Other" problems to listed problems | M | B-MED | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-chart-perio-print-birthdate` | Birthdate on printed Graphical Perio Chart | S | B-PERIO | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-eforms-patient-list-field` | eForm field: Patient List (pick a family member) | L | A-EF2 | A2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-eforms-ins-pick-field` | eForm field: Ins Pick, plus pending insurance (inspending) | XL | A-EF2 | A2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-eforms-patient-forms-window` | Patient Forms window improvements | M | B-EF | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-eforms-remove-imported-status` | Remove the Imported status from eForms | S | A-EF1 | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-family-edit-benefit-ui` | Edit Benefit window redesign | M | B-INSH | B2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-family-iaplus-bridge-update` | Insurance Answers Plus bridge becomes a web launcher | S | V-BRIDGE1 | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-family-ins-history-edit-remove` | Insurance History: edit or remove entered dates | M | B-INSH | B2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-manage-task-filter-unassigned` | Task Filter: "Unassigned" clinic and region | S | B-TASK | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-manage-task-category` | Task Category for all offices (TaskCategoryShow) | M | A-TASK | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-manage-task-filter-patient` | Task Filter: pick a specific patient | S | B-TASK | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-manage-era-carc-logic` | More logic for "Don't auto-process claims w/these CARCs" | M | B-ERA | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-bridges-dtx-studio` | DTX Studio and DTX Studio Acquire bridges | M | V-BRIDGE1 | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-reports-ins-pending-supplementals` | Insurance Pending Supplementals report | M | A-RPT | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-security-domain-login-change-user` | Domain login when changing user (proc note, group note, sheets) | M | A-SIG | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-security-permission-renames` | Permission display-name updates | S | M1-02 | F1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-security-tp-proc-edit-fee` | Permission: Treatment Planned Procedure Edit Fee | S | A-SIG | A1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-server-replication-delay` | Preferences: read/write delay with replication | M | B-SERVER | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-wiki-readonly-offline` | Read-only wiki usable while the program is closed | L | B-WIKI | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-tools-dbm-invalid-chars-gdi` | DB Maintenance tool: Invalid Characters (GDI+) | M | B-DBM | B2 | https://www.opendental.com/site/version25_2.html |
| `v25_2-wiki-search-limit` | Wiki Search: limit the number of results | S | B-WIKI | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-security-audit-patient-clinic` | Audit Trail logs patient clinic changes | S | B-AUDIT | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-ui-blur-module-switch` | Preference: blur when switching modules | S | B-SHELL | B3 | https://www.opendental.com/site/version25_2.html |
| `v25_2-imaging-copy-without-drawings` | Copy images without drawings or annotations | S | B-IMG | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_2-bridges-dxc-credit-score-removed` | Remove the DentalXChange Patient Credit Score bridge | S | B-SHELL | B3 | https://www.opendental.com/site/version25_2.html |
| `v25_3-chat-intraoffice-chat` | Intra-office chat (separate chat app) | XL | A-CHAT | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-procs-cdt-2026` | 2026 CDT code set | S | B-PROC | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-account-orthocase-cleanup` | Ortho Case window: remove excess text | S | B-ACCT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-security-inswriteoffedit-clarify` | Clarify the Insurance Write-off Edit permission | S | B-CLAIM | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-account-payplan-bypass-globallock` | Preference: payment plans ignore the global lock date | S | B-PAY | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-account-paysplit-payplan-attach` | Attach paysplits to payment plans in more cases | M | B-PAY | B1 | https://www.opendental.com/site/version25_2.html |
| `v25_3-appts-timebar-secondary-color` | Preference: secondary color in time bar when no provider time | S | B-APPT | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-appts-audit-detail` | More detail in appointment move/edit audit entries | M | A-APPT | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-security-apptcreate-pinboard` | Appointment Create also covers scheduling a planned appt from the pinboard | S | B-APPT | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-security-apptmove-scope` | Appointment Move scope: all statuses, not planned-to-pinboard | S | B-APPT | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-appts-mirroring` | Appointment mirroring into the secondary provider's operatory | L | A-APPT | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-family-otherbenefits-grid` | Edit Other Benefits directly in the grid; code groups in Other Benefits | M | A-INS | A2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-family-insplans-innet` | In Net column in the Insurance Plans list | S | B-INSH | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-family-exclusions-tooth` | Insurance exclusions limited to tooth numbers | M | A-INS | A2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-timeclock-reportsto` | Reports To filter on the Manage module time clock | S | B-TIME | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-accounting-checknum` | Button to fill the next check number | S | A-ACCT | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-accounting-payee` | Payee and Notes on accounting transactions | M | A-ACCT | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-accounting-needsreview` | Needs Review flag on accounting transactions | S | A-ACCT | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-accounting-printcheck` | Print checks from accounting transactions; Check sheet type | L | A-SHEET | A2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-statement-email-lastname` | Emailed statements carry the patient's last name | S | B-PRINT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-manage-statement-barcode` | Inserter barcode on printed statements | M | B-PRINT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-chart-vitalsign-notes` | Notes box in Edit Vital Signs | S | B-MED | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-medical-medlist-missingtab` | Remove the Missing Generic/Brand tab | S | B-MED | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-tools-dbm-whitespace-tools` | Move "Notes With Too Much White Space" to the DBM Tools tab | S | B-DBM | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-misc-printer-options` | Printer options window when printing from some areas | M | B-PRINT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-imaging-font-autocalc` | Imaging text: actual and apparent font size update automatically | S | B-IMG | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-sheets-checkbox-cleanup` | Edit CheckBox window: remove excess text | S | B-PRINT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-chart-orthochart-nonmodal` | Ortho Chart no longer blocks the rest of the program | M | B-SHELL | B3 | https://www.opendental.com/site/version25_3.html |
| `v25_3-misc-export-csv-txt` | Export .csv and Export .txt in more windows | M | B-PRINT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-security-claimproc-billedtoins` | Billed-to-ins permission also covers Code Sent to Ins | S | B-CLAIM | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-security-claimproc-billedtoins-desc` | Second wording change for the same permission | S | M1-02 | F1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-tools-file-monitor` | File Monitor for reads/writes to the image folder | M | B-SHELL | B3 | https://www.opendental.com/site/version25_3.html |
| `v25_3-tools-dbm-employee-supervisor` | DBM check: EmployeeMissingSupervisor | S | B-DBM | B2 | https://www.opendental.com/site/version25_3.html |
| `v25_3-family-audit-preferredname` | Audit trail records preferred-name changes | S | B-AUDIT | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-tp-add-procs-active` | Easier adding of procedures to the active treatment plan | M | B-TP | B1 | https://www.opendental.com/site/version25_3.html |
| `v25_3-school-course-scheduling` | Dental school course definitions, schedules and overrides (schema and data rules; UI version unconfirmed) | M | A-SCH | A1 | https://www.opendental.com/site/version25_4.html |
| `v25_3-school-approval-schema` | Dental school instructor approval table (schema only in 25.3) | S | M1-01 | F1 | https://www.opendental.com/site/version25_4.html |
| `v25_3-schema-compat-columns` | Schema and enum values needed only for data compatibility | S | M1-01 | F1 | https://www.opendental.com/OpenDentalDiffDocumentation25-3.xml |
| `v25_3-family-bluebook-providerfee` | Blue Book rule "UCR Fee" renamed "Provider Fee" | S | B-INSH | B2 | https://www.opendental.com/site/version25_4.html |
| `v25_3-tools-dbm-base64-docs` | DBM tool: Create Documents From Base64 Notes (25.3.39) | S | B-DBM | B2 | https://www.opendental.com/site/version25_4.html |
| `v25_3-canada-cda-codes-2026` | 2026 CDA procedure codes (Canada, 25.3.41) | S | B-PROC | B1 | https://www.opendental.com/site/version25_4.html |
| `v25_3-misc-pem-path-label` | PEM path field label in Choose Database / Reports Setup (25.3.53) | S | B-SERVER | B1 | https://www.opendental.com/site/version25_4.html |
| `v25_3-procs-codegroup-reset-tool` | Procedure Code Tools: reset Code Groups (25.3.54) | S | B-PROC | B1 | https://www.opendental.com/site/version25_4.html |
| `v25_3-account-era-displayfields` | ERA Claim Paid grid columns in Display Fields (25.3.78) | M | B-ERA | B1 | https://www.opendental.com/site/version25_4.html |
| `v25_3-account-payconnect-error-log` | Log PayConnect errors (desktop, direct to vendor) | S | V-PC | A1 | https://www.opendental.com/site/version25_3.html |
| `v25_4-dentalschool-courses` | Course instructors, student enrollment, course-def grading scale | L | M2-SCH1 | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-dentalschool-requirements` | Course and graduation requirements | L | M2-SCH2 | C5 | https://www.opendental.com/site/version25_4.html |
| `v25_4-dentalschool-approvals` | Instructor approval of procedures, appointments, treatment plans | L | M2-SCH2 | C5 | https://www.opendental.com/site/version25_4.html |
| `v25_4-dentalschool-planners` | Instructor Planner and Student Planner | L | M2-SCH3 | C6 | https://www.opendental.com/site/version25_4.html |
| `v25_4-dentalschool-evaluations` | Evaluations inside Edit Dental School Course; grade overrides; course grades | M | M2-SCH1 | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-dentalschool-permissions` | Dental school permissions renamed and redefined | S | M2-02 | F2 | https://www.opendental.com/OpenDentalDocumentation25-4.xml |
| `v25_4-canada-paysuite-setup` | PaySuite program link, carrier support flags, extended reversals, plan details | L | V-PAYSUITE | C4 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-paysuite-posting` | PaySuite automatic payment posting and reconciliation | XL | V-PAYSUITE | C4 | https://www.opendental.com/site/paysuitelogic.html |
| `v25_4-canada-paysuite-reports` | PaySuite expected payments and reconciliation windows | L | V-PAYSUITE | C4 | https://www.opendental.com/site/version25_4.html |
| `v25_4-payments-globalpayments-v2` | Global Payments V2 program link (terminals, tap/wallet payments) | XL | V-GP | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-payments-globalpayments-v1-rename` | EdgeExpress program link renamed Global Payments V1 | S | V-GP | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-account-service-date-view` | Service Date View easier to reach and use; columns in Display Fields | M | M2-SDV | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-account-incometransfer-limit` | Preference: limit manual income transfers to patient payments | S | M2-PAYW | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-account-recurringcharge-fail-email` | Automatic email when a recurring charge fails | M | M2-RECUR | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-cda-codes-2026` | 2026 CDA procedure codes | S | M2-VERIFY | F2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-other-proc-codes` | Other Procedure Codes window (import other Canadian code systems) | M | M2-CAN | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-codes-cdha` | CDHA (hygienist) code system | S | M2-CAN | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-codes-dac` | DAC (denturist) code system, English and French | S | M2-CAN | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-codes-ramq` | RAMQ (Quebec) code system with R suffix | S | M2-CAN | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-canada-labfee-pay` | "Pay Labs" when receiving claims with lab fees | S | M2-CAN | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-appts-insverify-daterange` | Insurance Verification List: appointment date range | M | M2-INSV | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-appts-insverify-carriers` | Insurance Verification List: pick one or more carriers | S | M2-INSV | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-appts-blockout-permissions` | Blockouts permission split into Add, Edit, Delete | M | M2-APPT | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-chart-perio-direction` | Perio Chart direction radio buttons improved | S | M2-MISC | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-chart-groupnote-size` | Larger Notes box in Group Note | S | M2-NOTES | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-chart-procnote-merge` | Procedure Note Merge window on concurrent edits | M | M2-NOTES | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-chart-nobillins-audit` | Per-procedure "Do Not Bill Insurance" flag, with audit trail | M | M2-EST | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-family-benefit-tooth-exclusions` | Teeth entry for exclusions from the Age Limits and Other Benefits grids | M | M2-EST | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-family-writeoff-override-wording` | Clearer wording for Write-Offs for Non-Covered Services overrides | S | M2-INSPLAN | C3 | https://www.opendental.com/site/version25_4.html |
| `v25_4-family-clerri` | Clerri membership-plan program link | M | V-BRIDGE2 | C3 | https://www.opendental.com/site/version25_4.html |
| `v25_4-tools-dbm-base64-documents` | DBM tool: Create Documents From Base64 Notes | S | M2-VERIFY | F2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-misc-diagnostics-info` | More details in the Diagnostics window | S | M2-MISC | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-misc-print-window` | Minor Print window enhancements | S | M2-MISC | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-reports-udsplus` | UDS+ Report for FQHC Dental Services | L | M2-UDS | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-claims-dentical-dc217` | Denti-Cal DC217_2019 claim form | M | M2-CLAIMFORM | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-sheets-nonlatin` | Sheets support non-Latin languages | M | M2-SHEETS | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-eforms-patfields` | eForms support custom patient field definitions | M | M2-EF | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-sheets-duplicate-field-warning` | Warning when a duplicate field is added to a sheet | S | M2-SHEETS | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-eforms-richtext-paste` | Rich text kept when pasting into eForm labels | M | M2-EF | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-eforms-multi-import` | Import several eForms at once | S | M2-EF | C2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-setup-pem-path-label` | PEM path label in Choose Database and Reports Setup | S | M2-VERIFY | F2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-reports-era-autoprocessed-filters` | ERAs Automatically Processed report: PreAuth and $0 filters | S | M2-ERA | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-bridges-trophy-enhanced-tags` | Trophy (Enhanced) bridge accepts command-line replacement tags | S | V-BRIDGE2 | C3 | https://www.opendental.com/site/version25_4.html |
| `v25_4-bridges-cleverone` | Clever One (VATECH) imaging bridge | S | V-BRIDGE2 | C3 | https://www.opendental.com/site/version25_4.html |
| `v25_4-proccodes-codegroup-reset` | Procedure Code Tools: reset Code Groups | M | M2-VERIFY | F2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-misc-ucr-terminology` | "UCR" renamed "Standard Provider Fee" throughout | M | M2-TERM | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-tp-estimates-appt-date` | Treatment plan estimates use the scheduled appointment date | L | M2-EST | C1 | https://www.opendental.com/site/version25_4.html |
| `v25_4-claims-era-displayfields` | ERA Claim Paid grid columns in Display Fields | M | M2-VERIFY | F2 | https://www.opendental.com/site/version25_4.html |
| `v25_4-sheets-datetimesubmitted` | Sheet and eForm "date submitted" | S | M2-EF | C2 | https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml |
| `v25_4-family-inseditlog-bigvalues` | Insurance edit logs keep values longer than 255 characters | S | M2-CARRIER | C3 | https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml |
| `v25_4-family-referral-phone` | Referral phone no longer limited to 10 digits | S | M2-MISC | C1 | https://www.opendental.com/OpenDentalDocumentation25-4.xml |
| `v25_4-security-patientselected-audit` | Audit entry when a patient is selected | S | M2-SHELL | C4 | https://www.opendental.com/OpenDentalDocumentation25-4.xml |
| `v25_4-misc-enum-parity` | Enum values needed so 25.4+ data loads safely | S | M2-01 | F2 | https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml |
| `v25_4-imaging-16bit-windowing` | Correct windowing for true 16-bit radiographs | S | M2-IMG | C1 | https://www.opendental.com/OpenDentalDocumentation25-4.xml |
| `v26_1-family-insplan-layout` | Edit Insurance Plan window layout overhaul | M | M2-INSPLAN | C3 | https://www.opendental.com/site/version26_1.html |
| `v26_1-family-inshist-codegroups` | Insurance History categories come from Code Groups | L | M2-HIST | C2 | https://www.opendental.com/site/version26_1.html |
| `v26_1-family-benefit-agelimit-teeth` | Age limits by tooth number in Edit Benefits | M | M2-EST | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-account-adjust-percent-of-fee` | Edit Adjustment: Fixed Amount or Percent of Fee | M | M2-ADJ | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-account-payment-invoice-filter` | Payment window: Invoice filter on Outstanding Charges | M | M2-PAYW | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-account-claim-receive-related-prompt` | Prompt to send related outstanding claims when any claim is received | M | M2-CLAIMRCV | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-account-service-date-view` | Service Date View overhaul, plus statements in Service Date View | L | M2-SDV | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-account-repeatcharge-backdate-pref` | Preference to allow or prevent backdated repeating charges | S | M2-RECUR | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-account-dxc-attach-pdf` | DentalXChange attachments accept PDF documents (26.1.64) | M | V-DXC2 | C1 | https://www.opendental.com/site/version26_2.html |
| `v26_1-account-era-claimproc-link` | Link received claimprocs to the exact ERA claim | M | M2-ERA | C1 | https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml |
| `v26_1-chart-groupnote-second-signature` | Group Note: second signature box | M | M2-NOTES | C2 | https://www.opendental.com/site/version26_1.html |
| `v26_1-chart-ptinfo-gender` | Gender display field in Chart Patient Information | S | M2-SHELL | C4 | https://www.opendental.com/site/version26_1.html |
| `v26_1-chart-eform-sig-datetime` | eForms record when a signature was made | S | M2-EF | C2 | https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml |
| `v26_1-imaging-pdf-javascript-pref` | Preference to allow JavaScript in PDFs shown in Imaging | S | M2-IMG | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-imaging-pearl-logging` | Pearl AI bridge: troubleshooting log level | S | V-PEARL2 | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-manage-chat-sound` | Chat Settings: audible notification for new messages | S | M2-CHAT | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-manage-chat-autolaunch` | Chat opens automatically when the user has unread messages | S | M2-SHELL | C4 | https://www.opendental.com/site/version26_1.html |
| `v26_1-manage-eds-claim-validation` | Enhanced claim validation for the EDS clearinghouse | M | V-EDS | C2 | https://www.opendental.com/site/version26_1.html |
| `v26_1-manage-dunning-html` | Dunning messages and billing e-mail template support HTML | M | M2-DUN | C2 | https://www.opendental.com/site/version26_1.html |
| `v26_1-manage-timecard-future-clockout` | Allow future-dated clock-out events (26.1.63) | S | M2-EMP | C1 | https://www.opendental.com/site/version26_2.html |
| `v26_1-lists-employee-title` | Employees: Title field (Employee Titles definitions) | S | M2-EMP | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-lists-carrier-preauth-flags` | Carriers flag procedures that need a preauthorization | L | M2-CARRIER | C3 | https://www.opendental.com/site/version26_1.html |
| `v26_1-lists-problem-merge-keep` | Problem List merge: choose the problem to keep | M | M2-MED | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-lists-canada-othercodes-password` | Canada: Other Procedure Codes tools require a password | S | M2-CAN | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-lists-proccode-no-hyphens` | Procedure codes may not contain commas or hyphens | S | M2-CAN | C1 | https://www.opendental.com/OpenDentalDocumentation26-1.xml |
| `v26_1-setup-automation-clinic-mismatch` | Automation condition: procedure clinic differs from patient clinic | S | M2-MISC | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-setup-adjtype-default-percent` | Adjustment types carry a default percentage | S | M2-ADJ | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-setup-make-recall-search-pref` | Preference: Make Recall automatically searches for openings | S | M2-APPT | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-setup-codegroup-history-options` | Code Group Edit: insurance history options | M | M2-HIST | C2 | https://www.opendental.com/site/version26_1.html |
| `v26_1-setup-default-prov-unassigned-pref` | Default provider for unassigned operatories moves to Preferences | S | M2-APPT | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-setup-allergy-search` | Allergy Setup: search bar | S | M2-MED | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-tools-audit-insplan-drop` | Audit Trail: PlanNum logged when a plan is dropped | S | M2-INSPLAN | C3 | https://www.opendental.com/site/version26_1.html |
| `v26_1-tools-audit-birthdate` | Audit Trail: birthdate additions and changes | S | M2-AUDIT | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-tools-dbm-duplicate-blockouts` | DBM check SchedulesDeleteDuplicateBlockouts replaces the standalone tool | S | M2-SHELL | C4 | https://www.opendental.com/site/version26_1.html |
| `v26_1-tools-dbm-missing-allergydefs` | DBM check: fix allergies with a missing allergy definition | S | M2-MED | C1 | https://www.opendental.com/site/version26_1.html |
| `v26_1-misc-audit-chartviews` | Audit Trail: Chart View changes | S | M2-SHELL | C4 | https://www.opendental.com/site/version26_1.html |
| `v26_1-misc-audit-eform-delete` | Audit Trail: patient eForm deletions | S | M2-EF | C2 | https://www.opendental.com/site/version26_1.html |
| `v26_1-misc-audit-billing-finance` | Audit Trail: opening and running Billing/Finance Charges | S | M2-SHELL | C4 | https://www.opendental.com/site/version26_1.html |
| `v26_1-misc-task-attach-nonmodal` | Task Attachment windows are non-modal | S | M2-MISC | C1 | https://www.opendental.com/site/version26_1.html |

## Appendix B: ownership hand-offs for the most shared files

Each line lists, in wave order, the packages that edit the file. Only one of them edits it at a time.

| File | Packages in order |
|---|---|
| `OpenDentBusiness/Data Interface/Procedures.cs` | M1-02 (F1) → A-FEE (A1) → A-INS (A2) → B-ACCT (B1) → B-INSH (B2) → M2-01 (F2) → M2-EST (C1) → M2-HIST (C2) |
| `OpenDental/Main Modules/ControlChart.cs` | A-CL (A1) → A-GN (A2) → B-APPT (B2) → B-SHELL (B3) → M2-NOTES (C2) → M2-CARRIER (C3) → M2-SHELL (C4) → M2-SCH2 (C5) |
| `OpenDentBusiness/Data Interface/ClaimProcs.cs` | M1-02 (F1) → A-FEE (A1) → A-INS (A2) → B-CLAIM (B2) → M2-EST (C1) → M2-HIST (C2) |
| `OpenDentBusiness/Data Interface/Patients.cs` | M1-02 (F1) → A-HASH (A1) → A-EF2 (A2) → B-AUDIT (B1) → M2-AUDIT (C1) → V-BRIDGE2 (C3) |
| `OpenDental/Forms/FormClaimEdit.cs` | V-DXC1 (A1) → A-IMG (A2) → B-CLAIM (B2) → M2-CLAIMRCV (C1) → V-EDS (C2) → V-PAYSUITE (C4) |
| `OpenDental/Main Modules/ControlTreat.cs` | B-TP (B1) → B-CLAIM (B2) → B-SHELL (B3) → M2-EST (C1) → M2-CARRIER (C3) → M2-SCH2 (C5) |
| `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` | M1-02 (F1) → A-HASH (A1) → A-EF2 (A2) → B-PAY (B1) → B-DBM (B2) |
| `OpenDentBusiness/Data Interface/Statements.cs` | M1-02 (F1) → A-STMT (A1) → B-PRINT (B1) → M2-SDV (C1) → M2-DUN (C2) |
| `OpenDental/Main Modules/FormOpenDental.cs` | M1-03 (F1) → A-EF1 (A1) → B-SHELL (B3) → M2-SHELL (C4) → M2-SCH3 (C6) |
| `OpenDental/Main Modules/ControlImages.cs` | A-CL (A1) → A-IMG (A2) → B-IMG (B1) → V-PEARL1 (B2) → B-SHELL (B3) |
| `OpenDental/Main Modules/ControlAccount.cs` | A-STMT (A1) → B-ACCT (B1) → B-CLAIM (B2) → B-SHELL (B3) → M2-SDV (C1) |
| `OpenDental/Main Modules/ControlFamily.cs` | A-EF2 (A2) → B-INSH (B2) → B-SHELL (B3) → M2-HIST (C2) → V-BRIDGE2 (C3) |
| `OpenDental/Forms/FormInsPlan.cs` | A-EF2 (A2) → V-BRIDGE1 (B1) → M2-HIST (C2) → M2-INSPLAN (C3) → V-PAYSUITE (C4) |
| `OpenDentBusiness/Data Interface/Appointments.cs` | M1-02 (F1) → A-APPT (A1) → B-APPT (B2) → M2-CARRIER (C3) |
| `OpenDental/Main Modules/FormOpenDentalMenus.cs` | A-EF1 (A1) → B-SHELL (B3) → M2-SHELL (C4) → M2-SCH3 (C6) |
| `OpenDental/User Controls/UserControlTasks.cs` | A-TASK (A1) → B-TASK (B1) → B-APPT (B2) → M2-MISC (C1) |
| `OpenDental/Main Modules/ControlAppt.cs` | A-APPT (A1) → B-APPT (B2) → B-SHELL (B3) → M2-APPT (C1) |
| `OpenDentBusiness/Data Interface/Schedules.cs` | A-APPT (A1) → B-APPT (B2) → M2-APPT (C1) → M2-SHELL (C4) |
| `OpenDentBusiness/Imaging/ImageStore.cs` | A-CL (A1) → A-IMG (A2) → B-DBM (B2) → B-SHELL (B3) |
| `OpenDental/Forms/FormProcEdit.cs` | A-SIG (A1) → M2-EST (C1) → M2-NOTES (C2) → M2-SCH2 (C5) |
| `OpenDental/Forms/FormSheetFillEdit.cs` | A-SIG (A1) → B-PRINT (B1) → M2-SHEETS (C1) → M2-EF (C2) |
| `OpenDental/SheetFramework/SheetPrinting.cs` | A-SIG (A1) → A-SHEET (A2) → B-PRINT (B1) → M2-SHEETS (C1) |
| `OpenDental/Forms/FormPayment.cs` | V-PC (A1) → B-PAY (B1) → M2-PAYW (C1) → V-GP (C2) |
| `OpenDental/Logic/ClaimL.cs` | V-DXC1 (A1) → B-ACCT (B1) → B-CLAIM (B2) → M2-CLAIMRCV (C1) |
| `OpenDentBusiness/Data Interface/ProcedureCodes.cs` | A-INS (A2) → B-PROC (B1) → M2-CAN (C1) → M2-HIST (C2) |
| `OpenDental/Forms/FormProgramLinkEdit.cs` | V-BRIDGE1 (B1) → B-SHELL (B3) → V-PEARL2 (C1) → V-BRIDGE2 (C3) |
