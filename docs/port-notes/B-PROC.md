# B-PROC: Procedure codes, code tools and fee tools

Branch `port/b-proc`, cut from `m1/integration-a2-patch`. Built in the single-wave run (all M1 Tier B and M2 packages at once).

Sources: Open Dental public release notes (24.4, 25.1, 25.3, 25.4) and the public manual pages for Procedure Code Tools (US and Canada), Code Groups, Fee Schedule Tools and Edit Procedure Code. Nothing was decompiled or observed. No ADA CDT or CDA code text, fee guide or claim-form artwork is in the repository: the code imports read a licensed file that the office supplies, and the default code groups list only code numbers.

## Status per feature

| Feature ID | Status | Notes |
|---|---|---|
| `v24_4-setup-cda-codes-2025` | built | Was in our 24.3.32 baseline as a download from Open Dental's website, which the decoupling removed. Now the CDA Codes option of Procedure Code Tools (Canada) imports a code file the office supplies. The code-set year is whatever the file holds. |
| `v25_3-canada-cda-codes-2026` | built | Same CDA Codes import. Adds only missing codes and never changes existing ones (public Canadian Procedure Code Tools page). Implemented once together with its 25.4 twin. |
| `v25_4-canada-cda-codes-2026` | built (twin) | Implemented here with the 25.4 detail: missing codes only, Security Admin plus Procedure Code Edit (both checked by Procedure Code Tools itself when the option runs), the "2025" wording is gone. |
| `v25_3-procs-cdt-2026` | built | The D Codes option of Procedure Code Tools (US) imports a licensed CDT file the office supplies: adds missing D codes, replaces the descriptions of existing ones, fills in blank abbreviations and moves codes the file marks deleted to the hidden Obsolete category. Nothing is deleted. Year-end warning kept. |
| `v25_3-procs-codegroup-reset-tool` | built | Code Groups option of Procedure Code Tools. Implemented once together with its 25.4 twin. |
| `v25_4-proccodes-codegroup-reset` | built (twin) | Asks for the code system (CDT or CDA), deletes the code groups and creates the defaults; benefits are moved to the matching new group (fixed type, else name); groups in use that match nothing are kept as hidden groups once the user accepts; a default group with no default codes for the code system (most CDA groups) keeps the codes of the group it replaces. Security Admin plus Procedure Code Edit (both checked by Procedure Code Tools itself); one Procedure Code Edit log entry naming the code system and the counts. |
| `v25_1-setup-proccode-morefees-filters` | built | More Fees (Edit Procedure Code) gets Provider (All, Default (none), each provider) and Clinic (All, Default, each clinic the user may access; hidden without clinics) filters, and an Effective column (24.4 effective dates). |
| `v25_1-setup-feetools-tabs` | built | Fee Tools keeps the shared Select Fees panel (fee schedule, clinic or group, provider, Show Fee Schedule Groups) and puts each tool on its own tab: Copy, Increase, Clear, Export / Import, Global Updates. Every 24.3 control, check, confirmation, permission and log entry is unchanged; the 24.4 date boxes moved with their tools (the base-fees date of Increase moved from Select Fees to the Increase tab). |

The backlog section also lists `OpenDentBusiness/Misc/ConvertDatabases8` as owned. It was not edited: the owner decisions freeze the legacy convert chain. The 25.4 spec asked to "move" the default list from ConvertDatabases8 to a shared helper; it is copied instead (`CodeGroupDefaults`), with the same names, order, fixed types and codes.

## Files

New:

- `OpenDentBusiness/Data Interface/CodeGroupDefaults.cs`: `EnumDentalCodeSystem` (CDT, CDA; not stored) and the default code group list per code system. The backlog's "CodeGroupDefaults.cs (new shared default list)".
- `OpenDentBusiness/Data Interface/CodeGroupResets.cs`: Reset Code Groups (plan, summary, reset, log text).
- `OpenDentBusiness/Data Interface/ProcCodeImports.cs`: reading a code file, the import plan, applying it.
- `OpenDentBusiness/Data Interface/MoreFeesFilters.cs`: the More Fees filters.
- `tests/BPROCTests/` (new test project, in `OpenDental.sln` and `OpenDental.Local.slnf`).
- this note.

Changed:

- `OpenDental/Forms/FormProcTools.cs` and `.designer.cs`: the code import option (`checkCodeImport`: "D Codes" in the US, "CDA Codes" in Canada) and `checkCodeGroups`; the options below moved down to make room.
- `OpenDental/Forms/FormProcCodeEditMore.cs` and `.designer.cs`: filters and the Effective column.
- `OpenDental/Forms/FormFeeSchedTools.cs` and `.designer.cs`: the tab control; the group boxes moved into the tabs unchanged.
- `OpenDentBusiness/OpenDentBusiness.csproj`: four `Compile` lines, next to `CodeGroups.cs`.

Not changed: `ProcedureCodes.cs`, `Fees.cs`, `CodeGroups.cs` (owned, but nothing needed them; this keeps them free for M2-CAN and M2-HIST), `FormProcCodeEdit.cs`, `FormProcCodes2.cs` (B-TP's), every registry, enum file, preference, permission and the schema. No fork migration step was needed, so none of the level range 2027000901-2027000999 is used.

## Code file formats (D Codes and CDA Codes)

The format was left open by the decoupling plan ("format to be decided in the CDT replacement work"). This package reads, without the office converting anything:

- XML: a procedure code list as Procedure Codes, Export writes it (`ArrayOfProcedureCode`). An optional `ProcCatDescript` element per code gives the category, as in the N Codes resource.
- Text, tab or comma separated, UTF-16 with a byte order mark (a spreadsheet's "Unicode Text" export), UTF-8 or the computer's ANSI code page. Lines starting with `#` are comments.
  - With either delimiter a field may be in double quotes, as spreadsheet programs write a cell that holds the delimiter, a comma or a quote in their tab separated ("Text (Tab delimited)", "Unicode Text") and comma separated exports: a field that starts with a quote (spaces before it are ignored) ends at the closing quote, `""` inside it is one quote, and the quotes are not part of the value. A quote anywhere else in a field (an inch mark in a hand-made file) is kept as text.
  - With a header row, columns are found by name, without regard to case, spaces or underscores: ProcCode (or Code, Procedure Code, CDT Code, CDA Code), Descript (or Description, Nomenclature), AbbrDesc (or Abbreviation), Category, TreatArea, PaintType, ProcTime, NoBillIns, IsProsth, IsHygiene, CanadaTimeUnits, Deleted (or Status). Other columns are ignored.
  - Without a header, the column order of Open Dental's old Canadian `procedurecodes.txt` (ProcCode, Descript, TreatArea, NoBillIns, IsProsth, IsHygiene, PaintType, ProcCatDescript, ProcTime, AbbrDesc, CanadaTimeUnits), plus an optional Deleted column.
- A code is "deleted" when its Deleted column says yes, 1, true, x, deleted, removed or d, or when its category is "Obsolete".
- Rows that cannot be read (no code, a code over 15 characters, a bad treatment area, paint type, time pattern or time units, a code listed twice) are skipped and listed before anything is saved.

The window shows what will be added, updated, filled in and moved, and asks before saving.

## INFERRED behaviour

- Code files: quotes follow the usual spreadsheet export rules (a field that starts with a quote ends at the closing quote, `""` is one quote) for tab and comma separated lines alike; a quote that does not start a field is text; an unclosed quote runs to the end of the line. UTF-16 is recognised only by its byte order mark, which spreadsheet programs always write.
- Code import, CDT: only codes shaped like CDT codes (D followed by four digits) are used; other rows are counted and skipped. Existing codes get the file's description even if the office edited it (Open Dental's tool "resets" descriptions); abbreviations are only filled in where blank. A code the file marks deleted moves to the hidden Obsolete category (created, or hidden, as T Codes does); a deleted code that is not in the database is ignored.
- Code import, CDA: deleted rows are ignored; the code is never changed.
- A new code with no category in the file goes to the visible category most codes of the same block already use (the code without its last two characters, so D2741 goes with the D27xx codes); without any, to a new category "Imported Codes". No meaning of any code is shipped.
- New codes never name a default provider (as the existing XML import).
- Year-end warning: from October 30 through December 31 of every year, a D Codes import first warns that next year's codes are not valid on claims until January 1 (Open Dental used a hard-coded 2024 window).
- Code imports log one Procedure Code Edit entry per added code, per changed description and per code moved to Obsolete, plus one entry for the run with the counts (Open Dental's existing import logs per added code).
- Reset Code Groups, defaults: the 13 groups of the 22.x conversion (BW, Exam, Pano/FMX, Cancer Screening, Prophy, Fluoride, Sealant, Crown, SRP, Full Debridement, Perio Maint, Dentures, Implants), ItemOrder 0-12, every one a frequency-grid row, Fluoride and Sealant also in Age Limitations, none hidden or in Other Benefits (the 25.3 conversion default).
- Reset Code Groups, CDA: Open Dental's conversion only knew the Canadian Fluoride (12111) and Sealant (13401) codes, so the other CDA default groups have no default codes. See the questions.
- Reset Code Groups, defaults without codes (fork rule, branch `port/b-proc-s1`): a default group that has no default codes for the chosen code system takes the codes of the existing group it replaces (the same match as for benefits: fixed type, else name), whether benefits use that group or not, so moved frequency limitations, Insurance History and the sheets' date-last fields keep finding the office's procedures. When several existing groups match, the one with the most benefits wins, then the first in ItemOrder; existing groups without codes are passed over. Defaults that have codes (all CDT groups, CDA Fluoride and Sealant) always get the default codes. A default with nothing to take from is created empty. The confirmation lists the groups that keep their codes ("no default codes for CDA: your current codes are kept", with the source group and its benefit count) and names the groups that will be created empty; the log entry adds the number of groups that kept their codes.
- Reset Code Groups, benefits: a benefit is moved to the new default group with the same fixed type or, failing that, the same name (trimmed, any case). A group in use with no match is kept as a hidden (retired) group, after the defaults in ItemOrder, so its benefits and estimates keep working; the reset only runs when the user accepts that (the window lists those groups and their benefit counts). Unused groups, custom or not, are deleted. Order of writes: insert the defaults, move the benefits, retire the kept groups, delete the rest, so an interruption never leaves a benefit on a deleted group; running the tool again finishes the job. Two old groups that map to the same default can leave a plan with two identical frequency rows; Database Maintenance's duplicate-benefit check already finds those.
- More Fees: with a provider or clinic filter on, the blank placeholder row for a fee schedule without a default fee is shown only when a default fee (no provider, default clinic) would pass the filters. Hidden providers are listed only when they have a fee for the code. The Effective column shows when the fee in effect today started (blank for always).
- Fee Tools: the last tab is remembered per user until the program closes (in memory). Open Dental proposes a user preference; there is no `UserOdFkeyType` value for it and adding one is a registry change.

## Deviations from the spec

- Reset Code Groups keeps in-use groups that match no default (as hidden groups) instead of deleting all groups as the public Canadian page describes. This is the fork decision in the 25.4 spec ("benefits that point to code groups must not be orphaned"); the 25.4 spec says "stop unless the user accepts", and what the user accepts here is keeping those groups hidden.
- The default list is copied from ConvertDatabases8, not moved (frozen convert chain).
- Fee Tools has no Import Canada button on the Export / Import tab: the Canadian fee schedule download was removed by the decoupling (`NoHqTests.RefDataTests` guards that), and fee guides are licensed content the owner excluded.
- The code import option is named `checkCodeImport`, not `checkDcodes`: `NoHqTests.CdtRetiredTests` guards that the old ADA-content option is gone, and this is a different feature (a user import).
- "Non-text metadata for new 2026 codes can ship in our own mapping file" (25.3 spec): not shipped. There is no public list of the 2026 code numbers we can verify, and the office's file carries treatment area, paint type, time and category anyway.

## Interplay with packages built at the same time

- B-TP owns `FormProcCodes2.cs` (the Procedure Codes window with the Tools and Import buttons). Nothing there changed; the Tools button still checks Security Admin before opening Procedure Code Tools. Procedure Code Tools has a second entry point that does not check Security Admin (the Setup Wizard's Registration Key page, `UserControlSetupWizRegKey.butProcCodeTools_Click`), so the two new options check Security Admin and Procedure Code Edit themselves when they run, and an option whose check fails is unchecked and skipped. The 24.3 options (T Codes, N Codes, Auto Codes and the rest) keep their 24.3 behaviour from that page.
- M2-HIST (26.1, `codegroup.ShowInHistory`, `HistProcCode`, `IsPerioFourQuads`): `CodeGroupDefaults.GetDefaults` sets only the 25.3 columns, so after M2-01/M2-02 add the 26.1 columns the defaults leave them 0. M2-HIST should set them in `CodeGroupDefaults` (for example ShowInHistory on the fixed groups and IsPerioFourQuads on SRP) so a reset gives the 26.1 Insurance History rows. `CodeGroupResets` copies whole `CodeGroup` objects through the generated CRUD, so new columns need no change there.
- M2-CAN (26.1 Canadian code systems, takes `ProcedureCodes.cs` over): can reuse `ProcCodeImports.Parse`/`CreatePlan`/`Apply` (CDA mode adds only missing codes) for other Canadian code lists.
- M2-VERIFY: `v25_4-canada-cda-codes-2026` and `v25_4-proccodes-codegroup-reset` are implemented here with the 25.4 detail; it only needs to confirm.
- M2-TERM ("Standard Provider Fee" wording) may touch Fee Tools texts; this package did not change any label wording except the Increase tab's base-date label and note.
- A-FEE (merged): the date boxes, `Fees.SaveIncrease`, `FeeScheds.CopyFeeSchedule` and the export/import dates are untouched and live on their tabs.

## Review fixes (branch `port/b-proc-r1`)

- Quoted cells in tab separated files. `ProcCodeImports.SplitLine` used to split a line that has a tab on every tab without looking at quotes, so a spreadsheet's tab separated export (which puts quotes around every cell with a comma or a quote, and doubles the quotes inside) gave descriptions with literal surrounding quotes. D Codes then saw a difference and overwrote every such existing description with the quoted text. The same quote rules now apply to both delimiters (see Code file formats). Side effect for comma separated lines: a quote in the middle of an unquoted field used to start a quoted part; it is now kept as text, so an inch mark no longer swallows the following columns.
- "Unicode Text" exports. `ProcCodeImports.DecodeFileBytes` read UTF-16 files (a spreadsheet's Unicode Text export, which starts with a UTF-16 byte order mark) as ANSI, so no code in them could be read. It now decodes UTF-16 (little or big endian) when the file starts with its byte order mark.
- Security Admin. `FormProcTools.RunCodeImport` and `RunCodeGroupReset` checked only Procedure Code Edit and relied on the caller for Security Admin, but the Setup Wizard's Registration Key page opens Procedure Code Tools without that check. Both now check Security Admin and then Procedure Code Edit (`Security.IsAuthorized` shows the usual message for the one that is missing) and uncheck the option when either fails.
- Tests: `ProcCodeImportTests.SplitLine_TabsWithQuotes_AsSpreadsheetsWriteThem`, `Parse_TabHeaderWithQuotedCells_DescriptionsHaveNoQuotes`, `DecodeFileBytes_Utf16UnicodeTextExport`, `CreatePlan_TabFileWithQuotedCells_LeavesSameDescriptionsAlone`; `ProcToolsDbTests.CdtImport_TabFileWithQuotedCells_StoresDescriptionsWithoutQuotes` (stored descriptions have no quotes and a matching description is not overwritten); `FormLayoutTests.ProcTools_HasCodeImportAndCodeGroups` asserts that both methods check Security Admin and Procedure Code Edit in their first `if`.

## Fix round 2 (branch `port/b-proc-r2`)

The full-suite run of `port/b-proc-r1` (commit 97d32d058, worktree wf_2be38036-940-158, 2026-10-25 03:38-03:56) failed 14 ReleaseFlowTests: the 8 database tests of `AdoptionDbTests` and the 6 of `ReleaseChannelDbTests`. The other 44 ReleaseFlowTests and every other assembly passed. The failures are not caused by this package, so no code was changed:

- Every one of the 14 failed with `MySqlConnector.MySqlException: Unknown database 'releaseflow_adoption'` or `'releaseflow_release'`. Those are fixed names (`AdoptionDbTests.DB_NAME`, `ReleaseChannelDbTests.DB_NAME`), and `ScratchDb.Create` drops and recreates the database for every scenario. Every worktree uses the same local MariaDB, so a ReleaseFlowTests run in another worktree drops the database while this one is using it.
- At least two ReleaseFlowTests processes ran together when the failure happened. The tests make their temp folders under `%TEMP%\ReleaseFlowTests`. At 03:51:15 two sets of `verify_`/`product_`/`buildid_` folders were created (03:51:15.269 and 03:51:15.443-.734), one set per process, and neither process got as far as the `pull_`/`tamper_`/`republish_` folders its database tests make.
- B-PROC does not touch ReleaseFlow code, its tests or the release and adoption code they cover. Its own scratch databases use per-run names (`forkmig_<start time>_<process id>_<random>_bproc`), and none of its cleanup drops a `releaseflow_` database. Its changed files are listed under Files.
- The same flaky failure appears in other worktrees' runs on unrelated branches (1, 2 and 6 failures in wf_2be38036-940-153, -154, -82 and -84).
- Re-run on this branch, same code as r1 (2026-10-25 04:03-04:06, targeted plus ReleaseFlowTests): BPROCTests 40/40, FeeTests 48/48, InsuranceTests 49/49, NoHqTests 49/49, ReleaseFlowTests 58/58. The build has 0 errors and the usual 376 warnings, and the host check passes.

The root-cause fix (per-run names for the ReleaseFlowTests scratch databases) is already on two other branches, in two different versions: `port/v-pearl1-r1` (f2e56c97f, adds `ScratchDbNameTests.cs`) and `port/v-dxc2-r1` (638c52b4a, adds `ScratchDbTests.cs`). Both edit `TestSupport.cs`, `AdoptionDbTests.cs` and `ReleaseChannelDbTests.cs`. A third version here would only add another conflict at integration, so this branch leaves ReleaseFlowTests alone. At integration, take one of the two versions and drop the other.

## Fix round 3 (branch `port/b-proc-s1`)

- A CDA reset emptied the office's code lists. `CodeGroupDefaults` has CDA codes only for Fluoride and Sealant, but `CodeGroupResets.CreatePlan` still moved benefits on the other groups (BW, Exam, Prophy, SRP and the rest) to the matching default and the reset then deleted the old groups. Those benefits ended up on groups with no codes, so estimates stopped applying the limitations and Insurance History and the sheets' date-last fields found nothing; the confirmation only showed "(CDA)". The same happened to a US office that picked CDA. Now `CreatePlan` calls `KeepCodesForEmptyDefaults`, which gives every default without codes the codes of the existing group it replaces (rule under INFERRED). `CodeGroupResetItem.IsProcCodesKept`, `CodeGroupResetPlan.GetCodesKept` and `GetDefaultsWithoutCodes`, and `CodeGroupResetResult.CountProcCodesKept` are new; `GetPlanSummary` lists the groups that keep their codes and names the ones created empty; `GetLogText` adds the count when it is not 0. `Reset` itself is unchanged: it inserts the plan's default groups, which now carry the kept codes. CDT resets do not change (every CDT default has codes).
- Tests: `CodeGroupResetPlanTests.CreatePlan_Cda_EmptyDefaultKeepsTheCurrentCodes` (most benefits wins, a tie goes to the first in ItemOrder, unused groups keep their codes, a group without codes is passed over, default CDA codes win, the static defaults are not changed, the summary texts), `CreatePlan_Cdt_DefaultCodesAlwaysWin`, and `GetLogText_NamesCodeSystemAndCounts` checks the new count; `ProcToolsDbTests.Reset_Cda_GroupInUseKeepsItsCodes` (a fixed Exam group with codes 99001 and one benefit: after a CDA reset the benefit's new Exam group has 99001, the old group is gone, nothing is orphaned, the log names the kept codes) and `Reset_Twice_IsStable` now also moves a BW benefit twice and checks that its group keeps the office's codes.

## Tests

`tests/BPROCTests` (MSTest v1, x86, .NET 4.8): parser and plan tests for the code imports, the default list and reset plan, the More Fees filters, layout tests that build Fee Tools, Procedure Code Tools and More Fees in memory (never shown) and check every tool's controls are on the right tab and every click handler is still wired, and database tests on a scratch database (`forkmig_<start time>_<process id>_<random>_bproc`, dropped at the end) for the reset and both imports. Synthetic codes and descriptions only.

## Questions for the owner

1. CDA default code groups: which Canadian code numbers should the default BW, Exam, Pano/FMX, Prophy, SRP, Full Debridement, Perio Maint, Crown, Dentures, Implants and Cancer Screening groups hold? Only Fluoride (12111) and Sealant (13401) are filled in today; a reset keeps the office's current codes for the others. If several existing groups match one default, should their codes be merged instead of taking the group with the most benefits?
2. Which licensed CDT file will the office import (for example the ADA's spreadsheet export)? If its column names differ from the recognised headers above, they can be added in `ProcCodeImports.GetColumnForHeader`.
3. Is keeping unmatched in-use code groups as hidden groups (instead of deleting them) the behaviour you want for Reset Code Groups?
4. Should D Codes keep overwriting descriptions the office edited, as Open Dental does, or only update codes whose description is blank?
5. Should Fee Tools remember the last tab across sessions? That needs a new `UserOdFkeyType` value (foundation registry).

## Not tested

- The dialogs (file picker, code system picker, confirmations) and message texts in a running program; the GUI was not launched.
- The middle tier (ClientMT) path of `CodeGroupResets.Reset` and `ProcCodeImports.Apply`.
- A Canadian (en-CA) culture run of Procedure Code Tools.
- Layout at high DPI and with clinics turned on in More Fees.
- A real licensed CDT or CDA file (none is in the repository, by rule).
