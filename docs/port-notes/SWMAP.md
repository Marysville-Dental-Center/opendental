# SWMAP: Open Dental's setting names at the switch-over

Owner question Switch-over 1 ([../owner-questions.md](../owner-questions.md)), answered on 2026-11-07. Branch `next/swmap-1`. Tests:
`tests/SWMAPTests`, plus the package test assemblies named below.

## What the owner gave

The owner exported the setting **names** of the office's Open Dental 25.3.48 database (`preference.PrefName`, names only) and the names of
four of its program links. The export is not in the repository, and no value from it was read or used: every test and example below uses
synthetic databases with made-up values.

- 51 names in the office database are not in this program's `PrefName` enum. Each is listed below with what this program does with it.
- These names are in the office database and in `PrefName` already, so they carry over unchanged: `InsBlueBookUcrFeePercent` (B-INSH 2),
  `PayPlansVersion` (Switch-over 9, B-PAY 2), `ReportsIncompleteProcsNoNotes`, `ReportsIncompleteProcsUnsigned` and
  `ReportsIncompleteProcsExcludeCodes` (B-SERVER 1).
- The office's program table has links named exactly `Overjet`, `DTXStudio`, `DTXStudioAcquire` and `IAP` (V-BRIDGE1 1).

## The rule

A setting this program already had under a name of its own now uses Open Dental's name, so the office's value is read as it is and a later
Open Dental database maps one to one. Nothing else depended on our names: every one of them was a preference row outside `PrefName`, read and
written by one class each. Where Open Dental's value means something else than ours, the code converts on reading and writing, never the
stored value.

- **On the office database** the switch-over changes no preference row: this program reads the office's rows under Open Dental's names.
  `AdoptionValidation migration-preview` (runbook step 0.2) lists them with the value this program will show.
- **On a database release v0.0 already used** (a new database, a database from 24.3, or one adopted by v0.0), the step
  `OdSettingNamesStep` (level 2026110731) moves each row v0.0 wrote under our name to Open Dental's name, converting the value, and deletes
  the old row. When both rows exist, the old row wins: it is the value v0.0 used. A value that equals the default of a missing row is not
  written when Open Dental's row is missing (a missing row already reads as that default). The step finds nothing to do on an Open Dental
  25.3.48 database, which never had our names, so adoption and its row check see no change.
- The names stay outside `PrefName`, read by name (`PrefC.GetRawOrDefault`) like before: a database from 24.3 or a new database has no such
  row, and a missing row reads as the default.

## Settings now read under Open Dental's names

| Setting (package) | Release v0.0 row | Open Dental 25.3.48 name | Value | Public source for the meaning |
|---|---|---|---|---|
| Sales Tax procedure code (B-ACCT 1) | `SalesTaxProcCode` | `SalesTaxProcCode` (the same) | A procedure code, or the CodeNum of one (both read) | https://www.opendental.com/site/version24_4.html (Account Module), https://www.opendental.com/manual/salestax.html |
| Prompt for Medical EOB on primary claims (B-CLAIM 1) | `ForkClaimMedReceivedPromptForMedicalEob` | `ClaimCheckForMedicalEOB` | 1/0, as stored | https://www.opendental.com/site/version25_2.html (Account Module) |
| Provider time on the operatory background (B-APPT 1) | `ForkApptProvTimeBarsOnOpBackground` | `ApptProvbarShowOnOps` | 1/0, as stored | https://www.opendental.com/site/version25_2.html (Appointments Module) |
| Secondary provider color in a time bar without provider time (B-APPT 1) | `ForkApptTimeBarSecondaryColorNoProvTime` | `ApptUseSecProvTimeIfEmpty` | 1/0, as stored | https://www.opendental.com/site/version25_3.html (Appointments) |
| Auto split ERAs with the listed CARC denial codes (B-ERA 1) | `ForkEraAutoSplitCarcDenials` | `EraAutoProcessSplitByCarcCodes` | 1/0, as stored | https://www.opendental.com/site/version25_1.html, https://opendental.blog/version-25-1/ |
| Payment plans and the global lock date (B-PAY 1) | `SecurityLockExcludesPayPlans` | `PayPlansIncludeInGlobalLock` | **Inverted.** Open Dental's 1 means payment plans are included in (blocked by) the global lock date; this program's box "Payment plans are not affected by the global lock" is ticked when the row is 0 and unticked when it is 1 or missing | https://www.opendental.com/site/version25_3.html (backported to 25.2.44), https://www.opendental.com/manual243/securitylockdates.html |
| Perio "Recession" wording (B-PERIO 1) | `PerioUseRecessionTerm` | `PerioRecessionInsteadOfGM` | 1/0, as stored | https://www.opendental.com/site/version24_4.html (Chart Module), https://opendental.blog/version24_4/ |
| Read delay after write, replication (B-SERVER 1) | `ForkReplicationReadDelayMs` (ms, 0 = off) | `ReplicationWriteReadDelayEnabled` (1/0) and `ReplicationWriteReadDelayMS` (ms) | The delay is the milliseconds when Enabled is 1, otherwise 0 (clamped to 0 to 60000). Saving a delay writes both; saving 0 writes Enabled 0 and keeps the milliseconds | https://www.opendental.com/site/version25_2.html (Miscellaneous) |
| Blur when switching modules (B-SHELL 1) | `ForkModuleSwitchBlur` | `BlurModuleOnLoad` | 1/0, as stored | https://www.opendental.com/site/version25_2.html (Miscellaneous) |

The pages above are the ones the feature specs cite for each setting ([features-24.4.md](../specs/features-24.4.md) to
[features-25.3.md](../specs/features-25.3.md)); they were not opened again for this package. The meaning of each name was matched to the
feature by those pages' wording and by the name itself; **INFERRED** where only the name tells: `ReplicationWriteReadDelay*` is taken as the
read delay after a write (Open Dental names one delay; this program's second one, the look-back for signals, has no Open Dental setting),
and the bool settings are taken to store 1 for on, as every Open Dental bool preference does (`POut.Bool`).

## Settings kept under this program's names

The office database has no setting for these (none of the 51 names matches them), so there is nothing to carry over and the office cannot
have set them in Open Dental. They keep their rows and their defaults:

| Setting (package) | Row |
|---|---|
| Write delay for signals (B-SERVER) | `ForkReplicationSignalLookbackMs` |
| Report Setup default "Include all procedures with no notes" (B-SERVER) | `ForkReportsIncompleteProcsAllNoNotes` |
| Read-only copy of the wiki: folder, and update after each save (B-WIKI 5) | `ForkWikiReadOnlyExportPath`, `ForkWikiReadOnlyAutoExport` |
| Print inserter barcode on statements (B-PRINT) | `BillingStatementInserterBarcode` |

Open Dental may keep the inserter barcode with the statement layout instead of in a preference (the 25.3 spec allowed for that), so the
runbook still has the office look for it in Open Dental before the day (step 0.11).

## The 51 names, one by one

**Read by this program (10):** `SalesTaxProcCode`, `ClaimCheckForMedicalEOB`, `ApptProvbarShowOnOps`, `ApptUseSecProvTimeIfEmpty`,
`EraAutoProcessSplitByCarcCodes`, `PayPlansIncludeInGlobalLock`, `PerioRecessionInsteadOfGM`, `ReplicationWriteReadDelayEnabled`,
`ReplicationWriteReadDelayMS`, `BlurModuleOnLoad` (the table above).

**Rows older than 24.3, which a database from 24.3 carries too (8).** Left alone, as 24.3 left them:

| Name | Why |
|---|---|
| `B11013Status` | The marker of an older conversion (`ConvertDatabases5`), read by that conversion only. |
| `ImageStore` | No longer supported since an old conversion (`ConvertDatabases2`). |
| `LettersIncludeReturnAddress`, `StationaryDocument`, `StationaryImage` | Marked "Obsolete" by `ConvertDatabases2`. |
| `ShortCodeEConfirmationTemplate` | Written blank by an older conversion (`ConvertDatabases6`) for Open Dental's hosted short-code texting, which this program does not have. |
| `ToothChartLowerQuality` | Written by an old conversion; nothing reads it. |
| `XRayExposureLevel` | A member of `PrefName` (its last one), so it carries over as it is. |

**Features this program left out (26).** Left alone: nothing reads them, and the rows stay in the database as they were.

| Name | Feature | Why it is out |
|---|---|---|
| `BetterDiagnosticsEnabled` | BetterDiagnostics AI (25.2) | Open Dental service, excluded (`v25_2-imaging-betterdiagnostics`). |
| `BrandingUseDefaults` | eClipboard branding (25.2, 25.3) | Hosted eClipboard, excluded (`v25_3-eservices-eclipboard-branding`). |
| `ChatServerCompName`, `ChatServerDbName`, `ChatServerMySqlPassHash`, `ChatServerMySqlUser` | Intra-office chat (25.3) | Excluded ([port-backlog.md](../specs/port-backlog.md): the chat tables stay, chat is not built). |
| `CloudApiServiceUrl` | Open Dental Cloud | Hosted service; the fork has no cloud mode. |
| `EClipboardWebEmailMessageTemplate`, `EClipboardWebSubjectTemplate`, `EClipboardWebTextMessageTemplate` | eClipboard Web (25.2) | Hosted eService, excluded (`v25_2-eservices-eclipboard-web`). |
| `MassEmailSignedUp` | Mass Email | Hosted eService, excluded (`v25_2-eservices-massemail-*`). |
| `MiddleTierSerializeNew` | Open Dental's choice between two Middle Tier serializers | This program has one, and does not ship the Middle Tier (owner-questions, decided by default 13). |
| `MsgToPayMenuOptionShow`, `PayTypeMessageToPay` | Message-to-Pay (24.4) | Hosted, excluded (`v24_4-account-m2p-hide-option`, `v24_4-account-m2p-paytype`). |
| `NewPatThankYouTypedSignatureDisable` | New patient thank-you messages (25.1) | Hosted eService, excluded (`v25_1-eservices-thankyou-disable-typed-signature`). |
| `PatientCommunicationTimeFormat` | Time format of automated messages (24.4) | Sent through Open Dental's eConnector, excluded (`v24_4-eservices-automsg-time-format`). |
| `PaymentPortalReceiptEmailBodyTemplate`, `PaymentPortalReceiptEmailSubjectTemplate` | Payment Portal and Message-to-Pay receipts (25.2) | Hosted, excluded (`v25_2-eservices-msgtopay-receipt-template`). |
| `PearlDateTimeLastUsed` | Open Dental's record of the last Pearl use | The Pearl integration of this program (V-PEARL1, V-PEARL2) keeps no such timestamp. |
| `RamqMachineID`, `RamqMachinePassword`, `RamqMachinePasswordDate` | Quebec RAMQ machine credentials | Canadian RAMQ connection changes of 24.4 to 25.3 were not ported. |
| `StatementAutoSendEmail`, `StatementAutoSendSMS` | Sending statements automatically by e-mail and text | Not in the public 24.4 to 25.3 feature lists this program ported, and sending through Open Dental's texting is hosted; this program has no automatic sender. |
| `WSEPSubDomain` | A sub-domain of Open Dental's hosted web pages | Hosted service. |
| `WebSchedRecallDoUseSecureEmail` | Secure Email for Web Sched Recall (25.2) | Hosted, excluded (`v25_2-eservices-websched-recall-secure-email-setup`). |

**Settings of features this program built without that option (7).** Nothing reads them; each is a possible follow-up, not a carry-over:

| Name | What this program does instead |
|---|---|
| `ClaimUpdateFeeForSecondary` | No such option; its meaning is not public (not in the 24.4 to 25.3 feature lists). |
| `DomainLoginForSigBoxes` | The change-user prompts of signature boxes (25.2, `v25_2-security-domain-login-change-user`) offer domain login whenever domain login is on; there is no separate switch for signature boxes. |
| `EFormImportAllergiesAsOther`, `EFormImportProblemsAsOther` | An eForm allergy or problem that matches no list entry is always filed under "Other", as in 24.3; staff convert it later (`v25_2-chart-eform-allergy-other-convert`). |
| `EnableXConnect` | XConnect is switched per clearinghouse row (`clearinghouse.EnableXConnect`, Open Dental 25.2 schema, carried over as it is); the preference of the same name is not read. |
| `InsHistPerioScalingCode` | Its meaning is not public. Insurance History moves into code groups at the switch-over (M2-HIST), taking each category's first code from the `InsHist*Codes` preferences. |
| `PerioRedAttGing` | The auto Att Ging row of the perio chart (24.4) is never drawn red; there is no threshold for it. |

## Program links (V-BRIDGE1 1)

This program's names are Open Dental's: `ProgramName.Overjet`, `DTXStudio`, `DTXStudioAcquire` and `IAP` match the office's rows exactly, so
`BridgeLinkRows.EnsureProgram` finds each office row by name and adds no second link: the office's row keeps its settings and toolbar
buttons, and only missing ClinicNum 0 properties are added. `SWMAPTests` checks this on an adopted synthetic 25.3.48 database.

## Item 9 and B-EF 4

- **Payment plan version (Switch-over 9, B-PAY 2):** `PayPlansVersion` is in the office database under the name this program reads, so the
  office's version carries over unchanged. Nothing converts it, on the office database or on a database from 24.3.
- **Forms imported before the switch-over (B-EF 4):** setting names do not show audit entries, so the switch-over converts none. The audit
  trail is copied unchanged. Patient Forms shows an eForm as Imported when an `EFormImport` audit entry carries its EFormNum in `FKey` and
  says "imported", the test it applies to its own entries; an entry Open Dental wrote that way counts, and one without the form's number
  shows the form's status instead.

## Files

- New: `OpenPmsDentalBusiness/Misc/ForkMigrations/OdSettingNamesStep.cs` (`OdSettingNames`, the renames and their plan; `OdSettingNamesStep`).
- Changed (names and conversion): `ApptTimeBars.cs`, `ClaimAttaches.MedicalEob.cs`, `EraAutoSplit.cs`, `ModuleSwitchBlur.cs`,
  `PerioMeasures.cs`, `PayPlanGlobalLock.cs`, `ReplicationDelay.cs`; comments only: `SalesTaxProcs.cs`, `PrefC.cs`.
- `tools/AdoptionValidation/MigrationPreview.cs`: the two settings lines (the settings read under Open Dental's names with what this program
  shows; the v0.0 rows the step would move). `OfficeFixture.cs`: five made-up rows under Open Dental's names, which the rehearsal's adoption
  keeps (`RehearsalIntegrationTests.MigrationPreview_...` checks the lines).
- New test project `tests/SWMAPTests`; changed tests in RegistryTests, BAPPTTests, BCLAIMTests, BERATests, BPAYTests, BPERIOTests,
  BSERVERTests and BSHELLTests (the names).
- Docs: owner questions Switch-over 1 and 9 and B-EF 4, the adoption runbook (what adoption does, step 0.2's preview lines, step 0.11 down to
  the five settings Open Dental has no name for, step 7.1 and the checklist), the port notes of the packages above, the fork migration README
  and one line in `docs/release-notes/next.md`.

## Checks

- Build of `OpenDental.Local.slnf` (Debug x86) through the build lane: 0 errors.
- Targeted tests: SWMAPTests 24/24, RegistryTests 39/39, AdoptionValidation.Tests 66/66, M201Tests 24 passed of 26 (2 skipped: they need Open Dental's
  published XML documents, `M201_OD_DOCS_DIR`), VBRIDGE1Tests 37/37, BACCTTests 41/41, BAPPTTests 61/61, BCLAIMTests 55/55, BERATests 75/75, BINSHTests 36/36,
  BPAYTests 73/73, BPERIOTests 55/55, BSERVERTests 57/57, BSHELLTests 68/68, BWIKITests 38/38, BEFTests 29/29, BPRINTTests 71/71,
  PayPlanTests 29/29, NoHqTests 58/58.
- Not run here: ForkMigrationTests and InitSqlTests. The new step makes `db/init/new-database.sql` one level behind until it is regenerated
  (`ForkMigrationTests.InitSqlRegenerationTests` and the InitSqlTests check that); the file is regenerated once after the schema packages are
  merged.
