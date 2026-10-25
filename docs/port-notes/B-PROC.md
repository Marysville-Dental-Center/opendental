# B-PROC: Procedure codes, code tools and fee tools

Branch `port/b-proc`, cut from `m1/integration-a2-patch`. Built in the single-wave run (all M1 Tier B and M2 packages at once).

Sources: Open Dental public release notes (24.4, 25.1, 25.3, 25.4) and the public manual pages for Procedure Code Tools (US and Canada), Code Groups, Fee Schedule Tools and Edit Procedure Code. Nothing was decompiled or observed. No ADA CDT or CDA code text, fee guide or claim-form artwork is in the repository: the code imports read a licensed file that the office supplies, and the default code groups list only code numbers.

## Status per feature

| Feature ID | Status | Notes |
|---|---|---|
| `v24_4-setup-cda-codes-2025` | built | Was in our 24.3.32 baseline as a download from Open Dental's website, which the decoupling removed. Now the CDA Codes option of Procedure Code Tools (Canada) imports a code file the office supplies. The code-set year is whatever the file holds. |
| `v25_3-canada-cda-codes-2026` | built | Same CDA Codes import. Adds only missing codes and never changes existing ones (public Canadian Procedure Code Tools page). Implemented once together with its 25.4 twin. |
| `v25_4-canada-cda-codes-2026` | built (twin) | Implemented here with the 25.4 detail: missing codes only, Security Admin (the Tools button) plus Procedure Code Edit, the "2025" wording is gone. |
| `v25_3-procs-cdt-2026` | built | The D Codes option of Procedure Code Tools (US) imports a licensed CDT file the office supplies: adds missing D codes, replaces the descriptions of existing ones, fills in blank abbreviations and moves codes the file marks deleted to the hidden Obsolete category. Nothing is deleted. Year-end warning kept. |
| `v25_3-procs-codegroup-reset-tool` | built | Code Groups option of Procedure Code Tools. Implemented once together with its 25.4 twin. |
| `v25_4-proccodes-codegroup-reset` | built (twin) | Asks for the code system (CDT or CDA), deletes the code groups and creates the defaults; benefits are moved to the matching new group (fixed type, else name); groups in use that match nothing are kept as hidden groups once the user accepts. Security Admin plus Procedure Code Edit; one Procedure Code Edit log entry naming the code system and the counts. |
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
- Text, tab or comma separated (quotes allowed around comma-separated fields), UTF-8 or the computer's ANSI code page. Lines starting with `#` are comments.
  - With a header row, columns are found by name, without regard to case, spaces or underscores: ProcCode (or Code, Procedure Code, CDT Code, CDA Code), Descript (or Description, Nomenclature), AbbrDesc (or Abbreviation), Category, TreatArea, PaintType, ProcTime, NoBillIns, IsProsth, IsHygiene, CanadaTimeUnits, Deleted (or Status). Other columns are ignored.
  - Without a header, the column order of Open Dental's old Canadian `procedurecodes.txt` (ProcCode, Descript, TreatArea, NoBillIns, IsProsth, IsHygiene, PaintType, ProcCatDescript, ProcTime, AbbrDesc, CanadaTimeUnits), plus an optional Deleted column.
- A code is "deleted" when its Deleted column says yes, 1, true, x, deleted, removed or d, or when its category is "Obsolete".
- Rows that cannot be read (no code, a code over 15 characters, a bad treatment area, paint type, time pattern or time units, a code listed twice) are skipped and listed before anything is saved.

The window shows what will be added, updated, filled in and moved, and asks before saving.

## INFERRED behaviour

- Code import, CDT: only codes shaped like CDT codes (D followed by four digits) are used; other rows are counted and skipped. Existing codes get the file's description even if the office edited it (Open Dental's tool "resets" descriptions); abbreviations are only filled in where blank. A code the file marks deleted moves to the hidden Obsolete category (created, or hidden, as T Codes does); a deleted code that is not in the database is ignored.
- Code import, CDA: deleted rows are ignored; the code is never changed.
- A new code with no category in the file goes to the visible category most codes of the same block already use (the code without its last two characters, so D2741 goes with the D27xx codes); without any, to a new category "Imported Codes". No meaning of any code is shipped.
- New codes never name a default provider (as the existing XML import).
- Year-end warning: from October 30 through December 31 of every year, a D Codes import first warns that next year's codes are not valid on claims until January 1 (Open Dental used a hard-coded 2024 window).
- Code imports log one Procedure Code Edit entry per added code, per changed description and per code moved to Obsolete, plus one entry for the run with the counts (Open Dental's existing import logs per added code).
- Reset Code Groups, defaults: the 13 groups of the 22.x conversion (BW, Exam, Pano/FMX, Cancer Screening, Prophy, Fluoride, Sealant, Crown, SRP, Full Debridement, Perio Maint, Dentures, Implants), ItemOrder 0-12, every one a frequency-grid row, Fluoride and Sealant also in Age Limitations, none hidden or in Other Benefits (the 25.3 conversion default).
- Reset Code Groups, CDA: Open Dental's conversion only knew the Canadian Fluoride (12111) and Sealant (13401) codes, so the other CDA default groups start with no codes and the office fills them in Setup, Code Groups. See the questions.
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

- B-TP owns `FormProcCodes2.cs` (the Procedure Codes window with the Tools and Import buttons). Nothing there changed; the Tools button still checks Security Admin before opening Procedure Code Tools, which now also checks Procedure Code Edit for the two new options.
- M2-HIST (26.1, `codegroup.ShowInHistory`, `HistProcCode`, `IsPerioFourQuads`): `CodeGroupDefaults.GetDefaults` sets only the 25.3 columns, so after M2-01/M2-02 add the 26.1 columns the defaults leave them 0. M2-HIST should set them in `CodeGroupDefaults` (for example ShowInHistory on the fixed groups and IsPerioFourQuads on SRP) so a reset gives the 26.1 Insurance History rows. `CodeGroupResets` copies whole `CodeGroup` objects through the generated CRUD, so new columns need no change there.
- M2-CAN (26.1 Canadian code systems, takes `ProcedureCodes.cs` over): can reuse `ProcCodeImports.Parse`/`CreatePlan`/`Apply` (CDA mode adds only missing codes) for other Canadian code lists.
- M2-VERIFY: `v25_4-canada-cda-codes-2026` and `v25_4-proccodes-codegroup-reset` are implemented here with the 25.4 detail; it only needs to confirm.
- M2-TERM ("Standard Provider Fee" wording) may touch Fee Tools texts; this package did not change any label wording except the Increase tab's base-date label and note.
- A-FEE (merged): the date boxes, `Fees.SaveIncrease`, `FeeScheds.CopyFeeSchedule` and the export/import dates are untouched and live on their tabs.

## Tests

`tests/BPROCTests` (MSTest v1, x86, .NET 4.8): parser and plan tests for the code imports, the default list and reset plan, the More Fees filters, layout tests that build Fee Tools, Procedure Code Tools and More Fees in memory (never shown) and check every tool's controls are on the right tab and every click handler is still wired, and database tests on a scratch database (`forkmig_<start time>_<process id>_<random>_bproc`, dropped at the end) for the reset and both imports. Synthetic codes and descriptions only.

## Questions for the owner

1. CDA default code groups: which Canadian code numbers should the default BW, Exam, Pano/FMX, Prophy, SRP, Full Debridement, Perio Maint, Crown, Dentures, Implants and Cancer Screening groups hold? Only Fluoride (12111) and Sealant (13401) are filled in today.
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
