# Port note: M2-TERM, "Standard Provider Fee" terminology

Branch `port/m2-term`, cut from `port/foundation` (M1 complete plus M2-01 and M2-02). Built in the single-wave run, beside the other packages. Backlog section: [port-backlog.md, M2-TERM](../specs/port-backlog.md#m2-term-standard-provider-fee-terminology). No fork migration step (the range 2027003401 to 2027003499 is unused), no schema, preference, permission or enum change.

## Sources

Public sources only. No Open Dental program was installed, observed or decompiled, and no office data was used.

- https://www.opendental.com/site/version25_4.html (Miscellaneous: "UCR" is now called "Standard Provider Fee" across the program, preferences and settings included).
- https://www.opendental.com/site/canadainsplan.html (the Exclusions option is named "Use Standard Provider Fee").
- The feature spec `v25_4-misc-ucr-terminology` in [features-25.4.md](../specs/features-25.4.md) and its 25.3 predecessor `v25_3-family-bluebook-providerfee` in [features-25.3.md](../specs/features-25.3.md).

The public manual pages (fee schedule logic, Blue Book setup) still use the old "UCR" labels, so no exact 25.4 label text is public apart from the option name above. Every label below is this fork's own wording (INFERRED), following the rule in the spec: "standard provider fee", or "standard fee" where space is short.

## Feature

| ID | Status | Notes |
|---|---|---|
| `v25_4-misc-ucr-terminology` | built (for the files this package owns) | Every shown "UCR" in the owned windows, reports, enum description and estimate log now says standard provider fee. Logic, preference names, column names, enum members, control names and the display-field internal name are unchanged. Report exports keep their column names, as the spec's rules ask (see "Report exports"). The few strings in other packages' files are listed under "Left to other packages". |

## What changed

| Where | Old text | New text |
|---|---|---|
| Preferences, Family > Insurance (`UserControlFamilyInsurance.Designer.cs`) | Ins plans with exclusions use UCR fee (zero out write-offs) | Ins plans with exclusions use standard provider fee (no write-offs) |
| | Ins plans use UCR fee (zero out write-offs) when annual max is met | Ins plans use standard provider fee (no write-offs) when annual max is met |
| | Ins plans use UCR fee (zero out write-offs) when frequency or age limits are met | Ins plans use standard provider fee (no write-offs) at frequency or age limits |
| | Insurance plans default to show UCR fee on claims | Insurance plans default to show standard provider fee on claims |
| Same window, prompt (`UserControlFamilyInsurance.cs`) | Would you like to immediately change all plans to show office UCR fees on claims? | Would you like to immediately change all plans to show the office's standard provider fees on claims? |
| Preferences, Account > Claim Send (`UserControlAccountClaimSend.Designer.cs`) | Use UCR fee for billed fee even if PPO fee is higher | Use standard provider fee for billed fee even if PPO fee is higher |
| Insurance Blue Book Setup, rules grid (`FormInsBlueBookRules.cs`) | UCR fee | Standard provider fee |
| Same window, percent label (designer) and save check | UCR Fee Percent / Please enter a UCR Fee Percent between 0 and 100. | Standard Provider Fee Percent / Please enter a Standard Provider Fee Percent between 0 and 100. |
| Blue Book estimate log (`InsBlueBooks.cs`, `BlueBookEstimateData.GetAllowed`) | Rule Type: UCR Fee ... (80% of UCR fee) | Rule Type: Standard Provider Fee ... (80% of standard provider fee) |
| Edit Insurance Plan, Exclusions combo (`InsPlan.cs`, `ExclusionRule.UseUcrFee` description) | Use UCR Fee | Use Standard Provider Fee |
| Capitation Utilization report (`FormRpCapitation.cs`, both layouts), column header on screen and in print | UCR Fee | Standard Fee (the export column stays "UCR Fee") |
| Net Production Detail report (`FormRpNetProdDetail.cs`), same | UCR | Standard Fee (the export column stays "UCR") |
| Provider Payroll report (`FormRpProviderPayroll.cs`), same | UCR Production | Standard Fee Production (the export column stays "UCR Production") |
| Provider Payroll data table (`RpProdInc.cs`, `GetProviderPayrollDataForClinics`), internal, never shown or exported | UCR Poduction (typo) | Standard Fee Production |
| Hover help (`PrefInfos.xml`, outside the owned list) | the three ControlText copies above; "exclusions use UCR fee"; "billed using the UCR fee"; "Example: UCR =$200" (4 times) | the new control texts; "exclusions use standard provider fee"; "billed using the standard provider fee"; "Example: Standard provider fee = $200" |

Doc comments only: `InsPlan.ClaimsUseUCR` and `ExclusionRule.UseUcrFee` summaries say "standard provider fee" and that the names stay for compatibility.

Layout: the Blue Book percent label grew to the left (Location 306 to 246, width 123 to 183) so the longer text fits; it still ends one pixel before the percent box and stays clear of the Allowed Fee Method list. No other control moved. Report headers wrap inside their column (QueryObject measures the header with the column width), so "Standard Fee" in the 60 and 70 pixel Capitation columns takes two lines; column widths are unchanged.

### Report exports (built per the spec's rule)

The spec's rule: "Keep report CSV header keys stable for users' downstream tools: add the new header text, and note the change in release notes." A report's Export (the tab-separated file from the report window) writes a header line with one name per column, and offices' spreadsheets and tools find the columns by these names. So the shown headers changed and the export keys did not:

| Report | Header on screen and in print | Name in the export's header line |
|---|---|---|
| Capitation Utilization (both layouts) | Standard Fee (was UCR Fee) | UCR Fee (unchanged) |
| Net Production Detail | Standard Fee (was UCR) | UCR (unchanged) |
| Provider Payroll (summary and detailed) | Standard Fee Production (was UCR Production) | UCR Production (unchanged) |

How: `QueryObject.AddColumn` names the export table's column after the header text, and `FormReportComplex.OnExport_Click` writes each export column's `Caption`, which follows `ColumnName`. Each renamed `AddColumn` in the three forms is followed by one statement that gives the export column its old name back: `query.ExportTable.Columns[query.ExportTable.Columns.Count-1].ColumnName="UCR Fee";` ("UCR" for Net Production Detail, "UCR Production" for Provider Payroll). Nothing else reads the export table by column name: export cells and totals are found by position through `ArrDataFields`, which keeps the shown header, and `DeepCopyQueryObject` (split reports) copies the column names. Column order and values are unchanged.

Release note text: "Reports: the Capitation Utilization, Net Production Detail and Provider Payroll reports show "Standard Fee" ("Standard Fee Production") where they showed "UCR". Exported report files keep the old column names, so spreadsheets and tools that read them need no change."

Data that is never shown: the Net Production Detail query's own column name `UCR` stays, so anything that reads `RpProdInc.GetNetProductionDetailDataSet` by name still works. The Provider Payroll data table's column was named "UCR Poduction" with a typo; it is now "Standard Fee Production". That table is neither shown nor exported (the export table takes its names from the form's `AddColumn` calls and its values by position), and nothing in the repository reads it by name.

## Not changed, on purpose

- Preference names (`InsDefaultShowUCRonClaims`, `InsPlanUseUcrFeeForExclusions`, `InsPpoAlwaysUseUcrFee`, `InsBlueBookUcrFeePercent`), column names (`insplan.ClaimsUseUCR`, `proctp.CatPercUCR`), the enum member `ExclusionRule.UseUcrFee` (value 2), control names (`checkInsPlanExclusionsUseUCR`, `checkPpoUseUcr`, `labelUcrFeePercent`, ...; PrefInfos.xml finds hover help by control name) and the display-field internal name "Cat% UCR". Tested in `TerminologyStringTests.InternalNames_Unchanged`.
- `InsBlueBookRuleType.ProviderFee` keeps M1-02's description "Provider Fee" (25.3). It says no "UCR", and `InsBlueBookRule.cs` is a foundation registry file outside this package. So the Blue Book grid row says "Standard provider fee", while the "cannot be edited" message on double-click, which prints the enum description, says "Provider Fee". See question 2.
- Code comments, SQL aliases, `DocumentationBuilder/` (generated, historical database documents) and unit-test fixture names.
- Translations: `Lan.g` keys are the English text, so a translated office sees the new English text for these strings until its translation rows are updated. Nothing in the repository holds translations.

## Left to other packages (files they own in this run)

- `OpenDental/Forms/FormInsPlan.designer.cs`, "Claims show UCR fee, not billed fee": M2-INSPLAN (C3) changes the Edit Insurance Plan strings with its layout rewrite, as the backlog says. Its Exclusions combo already reads "Use Standard Provider Fee" through `ExclusionRule`'s description, which is what its write-off override wording item (`v25_4-family-writeoff-override-wording`) asks for.
- Treatment Plan "Cat% UCR" column: M2-02 already gives the available and default entries the text "Cat% Std Fee" (`DisplayFields26_1`). A saved `displayfield` row with a blank description still shows the internal name in `ControlTreat` (M2-EST owns it) and in printed treatment plan sheets (`SheetDrawingJob`, M2-SHEETS). Both build the header as `Description` or else `InternalName`; calling `DisplayFields26_1.GetColumnText(displayField)` there instead shows "Cat% Std Fee" for those rows too.

## INFERRED behaviours

- All the wording above. "Standard provider fee" in sentences and preferences, "Standard Fee" in narrow report columns (the spec's short form), "Standard provider fee" in the Blue Book grid.
- "(zero out write-offs)" became "(no write-offs)" in the three Write-Offs for Non-Covered Services check boxes: the frequency or age limit text did not fit its 422-pixel check box with "standard provider fee" and the old phrase, and the three boxes read alike this way. Same meaning.
- The frequency or age limit box says "at frequency or age limits" instead of "when frequency or age limits are met", for the same width reason.

## Deviations from the spec

- The spec lists `InsBlueBookRule.cs` `[Description("UCR Fee")]`; on this base it is already "Provider Fee" (M1-02, from the 25.3 documents) and was left alone (see above).
- `PrefInfos.xml` is a foundation registry file. It was edited anyway (only text inside existing entries, no entry added or removed), because its hover help and ControlText copies are shown text that said "UCR", and ControlText is the fallback key that finds a control's help.

## Interplay with packages built at the same time

- **B-INSH** (`v25_3-family-bluebook-providerfee`) also edits `FormInsBlueBookRules.cs` and `InsBlueBooks.cs` in this run, most likely the same lines ("UCR fee" to "Provider fee", "Rule Type: UCR Fee" to "Rule Type: Provider Fee"). On a merge conflict on those lines, keep M2-TERM's text: 25.4 replaces the 25.3 wording. Keep B-INSH's other changes. If B-INSH renamed the percent label or the save check, keep "Standard Provider Fee Percent" and the label's new Location and Size. After the merge, run `M2TERMTests` (`TerminologyStringTests`, `TerminologyLayoutTests`, `BlueBookLogDbTests`): they fail if a "UCR" or a 25.3-only text came back.
- **M2-INSPLAN** (C3, depends on M2-TERM): changes `checkClaimsUseUCR`'s text in `FormInsPlan`; the combo text comes from here.
- **M2-EST, M2-SHEETS**: the "Cat% UCR" header for saved rows (above).
- **M2-02**: owns `PrefInfos.xml`; only text inside three existing ControlText and four Details elements changed (lines of `InsPpoAlwaysUseUcrFee`, `CoPay_FeeSchedule_BlankLikeZero`, `FixedBenefitBlankLikeZero`, `InsPlanExclusionsMarkDoNotBillIns`, `InsPlanUseUcrFeeForExclusions` and `checkInsDefaultShowUCRonClaims`).
- Any package that edits `FormRpCapitation.cs`, `FormRpNetProdDetail.cs` or `FormRpProviderPayroll.cs`: keep the export key statement directly after the renamed `AddColumn` (and nothing between them). `ReportHeaderTests.ExportKeys_Unchanged_ShownHeadersRenamed` fails if an export key changed or the statement moved.
- Every package that adds a test project edits the same places in `OpenDental.sln` and `OpenDental.Local.slnf`; those conflicts are a matter of keeping all lines.

## Found, not changed

Provider Payroll, detailed: `RpProdInc.GetProviderPayrollDataForClinics` adds a "Patient" column after "Day", but fills each row by position with the patient's name in column 1 and production in column 2. The data table's column names are therefore one place off in detailed mode (production sits under the column named "Patient"). The report itself is right, because the form's headers (Date, Patient, Standard Fee Production, ...) are bound by position; only something reading the data table by name would be wrong, and nothing does. Left as Open Dental has it. `ReportHeaderDbTests.ProviderPayroll_Detailed_ProductionUnderTheThirdHeader` pins the positional behaviour.

## Tests

New project `tests/M2TERMTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `TerminologyStringTests`: the wording table (old text gone, new text there, in each owned file; an old report header stays only as the export key in the statement right after its renamed column); no shown literal (designer Text, message boxes, translations, report headers, grid cells, enum descriptions, the estimate log) in the owned files says "UCR"; the scan sees every changed literal; `ExclusionRule` descriptions with unchanged numbers and member name; internal names unchanged; PrefInfos.xml ControlText equals the control texts and no hover help says "UCR".
- `TerminologyLayoutTests`: each changed check box text fits its control (Microsoft Sans Serif 8.25 at 96 dpi, the width `OpenDental.UI.CheckBox` draws in); the Blue Book label text fits, ends next to the percent box and clears the list.
- `ReportHeaderTests`: the header order of the three report forms, with "Standard Fee" over the fee column of the capitation query. `ExportKeys_Unchanged_ShownHeadersRenamed` reads each form's `AddColumn` calls and export key statements and checks that the export keys are the 24.3 headers, that each statement directly follows the column it renames, and that no shown header says "UCR". `QueryObject_ExportWritesTheOldKey_ScreenShowsTheNewHeader` builds the real `OpenDental.ReportingComplex.QueryObject` for each layout (both Capitation layouts, Provider Payroll summary and detailed, Net Production Detail) from those calls on a filled table and checks the export's header line (`Caption`s) keeps the old names, the column headers and data fields show the new text, cells and totals are found by position, and `DeepCopyQueryObject` keeps the names. The project references `OpenDental.csproj` for this (as `ApptTests` does); no window is shown.
- `ReportHeaderDbTests` and `BlueBookLogDbTests`: one scratch database for the assembly (fresh 24.3.63 database upgraded by the fork migration runner, name `forkmig_<time>_<pid>_<random>_m2term`, dropped afterwards; skipped when the local MariaDB does not answer). Synthetic code, fee schedule, provider, patient and a completed procedure. Provider Payroll (summary and detailed) and Net Production Detail put the fee under the renamed header; the Blue Book provider fee rule logs the new text, with and without a fee.

Related existing assemblies: `RegistryTests` (PrefInfos.xml entries, `InsBlueBookRuleType` pins), `M202Tests` (PrefInfos.xml entries for the 26.1 preferences), `ReportTests` (reports).

## Questions for the owner

1. Report headers: "Standard Fee" (fits, or wraps to two lines in Capitation) or the full "Standard Provider Fee" (wraps everywhere)? Either way the export keeps the old column names ("UCR Fee", "UCR", "UCR Production"), as the spec's rule asks. If you would rather have the new names in exported files too, that is a deviation from the spec: it needs your explicit approval before merging, would be recorded under "Deviations from the spec", and is done by removing the four export key statements.
2. Blue Book: the rule's enum description is "Provider Fee" (25.3) and the grid row now says "Standard provider fee" (25.4). Should the registry owner change the description to "Standard Provider Fee" too, so the double-click message matches?
3. Is "(no write-offs)" acceptable in place of "(zero out write-offs)" in the three preference check boxes, or should the check boxes be widened and keep the old phrase?
