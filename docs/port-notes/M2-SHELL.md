# Port note: M2-SHELL, main window and cross-module audit (26.1)

Branch `port/m2-shell`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run, beside the other M1 Tier B and M2 packages. Backlog section: [port-backlog.md, M2-SHELL](../specs/port-backlog.md#m2-shell-main-window-and-cross-module-audit-261). No fork migration step (see "Migration level range" below).

Finish round `port/m2-shell-t1` (from `port/m2-shell`, whose last run was cut off by a usage limit): `port/int-13` (bb150f537) merged in, the Family module's Gender row switched to the Chart row's text (the spec's "one rendering in both modules"), and the checks run on the merged head. See [Finish round t1](#finish-round-t1).

Finish round `port/m2-shell-t3` (from `port/m2-shell-t1`): `port/int-14` (9a10a840e) merged in and two review findings fixed: Chart View Edit is now in the Audit Trail filter, and the Patient Selected setting moved out of `PrefName` into the fork-owned row `ForkAuditTrailPatientSelected`. See [Finish round t3](#finish-round-t3).

Sources: the feature specs in `docs/specs/features-25.4.md` and `features-26.1.md`, and the public pages they cite (the 25.4 and 26.1 release notes, the public manual pages for the Audit Trail and Audit Trail Permissions, read 2026-10-25). No Open Dental program, install or office data was used. Every text below and in the code is written for this fork.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-security-patientselected-audit` | built | Patient Selected (275) entry on every change of the loaded patient, off by default, 60-second repeat rule. Setting in Setup, Security, Global Security Settings. |
| `v26_1-misc-audit-billing-finance` | built | "window opened" entry and one entry per run (settings and result, also for a cancelled run); the 24.3 entry written on close is gone; undo and aging entries unchanged. |
| `v26_1-tools-dbm-duplicate-blockouts` | built | DBM check `SchedulesDeleteDuplicateBlockouts`; Tools, Misc Tools, Clear Duplicate Blockouts and `FormBlockoutDuplicatesFix` deleted; both defects of `Schedules.ClearDuplicates` fixed. |
| `v26_1-manage-chat-autolaunch` | not built | Owner decision 2026-10-18: intra-office chat is excluded (A-CHAT and M2-CHAT dropped), so there is no chat window to open. |
| `v26_1-chart-ptinfo-gender` | built | "Gender" row in the Chart module's Patient Information grid (field registered by M2-02). Since t1 the Family module's existing Gender row shows the same text. |
| `v26_1-misc-audit-chartviews` | built | Entries for add, edit (every changed setting, including columns), delete and the up/down moves, under ChartViewsEdit (262). |

## What was built

### Patient Selected audit (25.4)

- `OpenDentBusiness/Misc/Shell26_1.cs`, class `PatientSelectedAudit`: `IsEnabled`, `LogIfEnabled(patNum)`, the repeat rule `ShouldLog(userNum,patNum,now)` and `SetEnabled(bool)`.
- Hook: one line in the `FormOpenDental.PatNumCur` setter, after the value changed. Every path that changes the loaded patient goes through that setter (patient select window, family menu, search results, `GlobalFormOpenDental.PatientSelected` from every module and window, task and appointment "go to"), so one hook covers them all.
- Entry: `securitylog` row with PermType 275, the patient's PatNum and the text "Patient selected." Not written for PatNum 0 (unloading the patient) or when nobody is logged on.
- Repeat rule (spec, fork): a user who selects the same patient again within 60 seconds gets no second entry. It is kept in memory per workstation and counts from the last entry written.
- Setting (since t3): the fork-owned preference row `ForkAuditTrailPatientSelected` (`PatientSelectedAudit.PREF_NAME`, bool, off), not a `PrefName` member, because `PrefName` holds only names Open Dental publishes and the 25.4 switch's name is not public. The `Fork` prefix keeps it from clashing with an Open Dental preference, as for every other fork-owned setting (B-SERVER, B-WIKI, B-SHELL, B-CLAIM and others). Checkbox "Log each patient selection in the Audit Trail" at the bottom of Global Security Settings (SecurityAdmin). There is no seed row: `IsEnabled` reads the row through `PrefC.GetRawOrDefault(PREF_NAME,"0")`, so a missing row reads as off from the cache without throwing (it is read on every patient change), and `SetEnabled` writes through `PrefC.UpsertRaw`, inserting the row the first time an administrator turns the audit on. Turning it off while there is no row writes nothing. `SetEnabled` keeps its Middle Tier guard (the write and its cache update run on the server) and returns whether the value changed, so Global Security Settings sends the Prefs signal. Turning it on or off writes a SecurityAdmin entry, so switching the audit off is itself audited.

### Billing/Finance Charges audit (26.1)

- `Shell26_1.cs`, `FinanceChargeAudit` and `FinanceChargeRun`: the entry texts.
- `FormFinanceCharges`: "Billing/Finance Charges window opened" at the start of Load (written even when the window then closes because a setting is missing), and after each run "<Finance|Billing> charges run: date, APR, at least, only if over, compound (finance) or amount (billing), balances over N days, billing types, provider, N charges created totaling $X". A run the user cancels part way is logged with "(cancelled before every account was processed)", because the charges already created stay.
- The run total is summed as each adjustment is inserted (the charges are added on several threads, so the total and count are guarded). The run counters are reset at the start of each run, so a second run after a cancelled one reports only its own charges (24.3 kept counting).
- `FormOpenDental.menuItemFinanceCharge_Click` no longer writes "Run Finance Charges" on close. The undo entry and the AgingRan entries are unchanged.

### Duplicate blockouts in Database Maintenance (26.1)

- `OpenDentBusiness/Data Interface/DatabaseMaintenances.Schedules.cs` (new partial file): `[DbmMethodAttr] SchedulesDeleteDuplicateBlockouts`. Check: "Duplicate blockouts found: N"; Fix: "Duplicate blockouts removed: N". Middle Tier guard and verbose output as the other checks; the DBM run log records the run as for every check.
- `Schedules.cs`: `GetDuplicateBlockoutCount` and `ClearDuplicates` now share `GetDuplicateBlockoutScheduleOpNums`. The rule is unchanged: same date, start, stop, blockout type and note on the same operatory; the lowest ScheduleOpNum of each group stays.
  - Spec defect 1 fixed: no duplicates returns early (no empty `IN ()`).
  - Spec defect 2 fixed: no upper-case `SCHEDULE`.
  - Spec item 3: only blockouts that this run left with no operatory are deleted (the spec's proposed option). A blockout that already had no operatory is left alone.
  - Also fixed: the 24.3 queries built each group with `GROUP_CONCAT`, which a server cuts at `group_concat_max_len` (1024 bytes unless the session variable stuck), so a large group could be cut mid-number and a wrong ScheduleOpNum deleted. The new query is a join with no `GROUP_CONCAT` (tested with a 301-blockout group). NULL notes match each other (`<=>`), as the 24.3 grouping treated them.
  - `ClearDuplicates` returns the number of links removed (was void). Its only caller was the deleted form.
- Removed: the Misc Tools menu item, `menuItemDuplicateBlockouts_Click`, `FormBlockoutDuplicatesFix` (.cs, designer, resx) and their three `OpenDental.csproj` entries; with the form goes its Setup entry "Clear duplicate blockouts."
- Not marked replication-unsafe: the fix deletes rows by primary key only.

### Chart Gender row (26.1)

- `ControlChart.FillPtInfo`: `case DisplayFields26_1.ChartPatientInfoGender` (M2-02's constant, "Gender") shows `PatientGenderDisplay.GetText(gender)`: the translated Edit Patient Information text (the `enumPatientGender` group that `ListBox.AddEnums<PatientGender>` uses), blank for Unknown and for numbers this program does not know. The row keeps a null tag, so a double click opens Edit Patient Information like the other demographic rows. Not on by default (M2-02's lists).
- `ControlFamily.FillPatientData`, `case "Gender"` (the 24.3 Family display field): the same `PatientGenderDisplay.GetText` since t1, so both modules show one text. Before, the Family row showed the raw enum name, "Unknown" for an unknown gender, untranslated.

### Chart View audit (26.1)

- `ChartViews.cs`: `LogAdded`, `LogEdited`, `LogDeleted`, `LogMoved` and the pure text builders (`GetEditChanges`, `GetFlagText`, `GetColumnsText`). Entries go under ChartViewsEdit (262) with the ChartViewNum as FKey.
- Audit Trail filter (since t3): `GroupPermissions.HasAuditTrail` returns true for ChartViewsEdit (its `case` in the "does not have audit trail" block is commented out, the way PatientSelected's is), so "Chart View Edit" is in the Audit Trail window's permission filter (`FormAudit` builds the filter from `HasAuditTrail`). Before, the entries could only be found under "All", which is capped at `AuditTrailEntriesDisplayed` rows.
- `FormChartView`: keeps a copy of the view and its columns when it opens; Save writes "added" for a new view or "edited: setting old to new; ..." for every changed setting (description, procedure statuses, object types, dates, procedure notes, audit mode, selected teeth only, treatment plan charting, Orion statuses, columns with their custom names and widths). Saving with no real change writes nothing. Delete writes "deleted".
- `ControlChart.SwapChartViews` (the up/down buttons): "moved from position a to b", 1-based. The automatic renumbering after a delete (`SyncChartViewItemOrders`) is not a user move and is not logged.

## Files touched

- New: `OpenDentBusiness/Misc/Shell26_1.cs`, `OpenDentBusiness/Data Interface/DatabaseMaintenances.Schedules.cs`, `tests/M2SHELLTests/` (project), this note.
- Owned by this package: `OpenDentBusiness/Data Interface/ChartViews.cs`, `Schedules.cs`; `OpenDental/Main Modules/ControlChart.cs` (2 small hunks), `FormOpenDental.cs` (setter line, removed handler, removed close entry), `FormOpenDentalMenus.cs` (1 line removed); `OpenDental/Forms/FormChartView.cs`, `FormFinanceCharges.cs`, `FormBlockoutDuplicatesFix.*` (deleted).
- Outside the owned list (also in the report):
  - `OpenDentBusiness/Data Interface/GroupPermissions.cs` (t3, foundation registry): one line in `HasAuditTrail`, `case EnumPermType.ChartViewsEdit:` commented out so the Chart View entries are in the Audit Trail filter (the rule M202Tests and RegistryTests state: a permission that writes audit entries belongs in the filter).
  - `OpenDental/Forms/FormGlobalSecurity.cs` and `.designer.cs`: the checkbox (no package owns this window).
  - `OpenDental/Main Modules/ControlFamily.cs` (t1): one line, the Family Gender row (M2-HIST, V-BRIDGE2 and B-SHELL, which also edit this file, are already merged on `port/int-13`).
  - `OpenDentBusiness/OpenDentBusiness.csproj` (2 Compile lines), `OpenDental/OpenDental.csproj` (form entries removed), `OpenDental.sln`, `OpenDental.Local.slnf` (test project).
- Not changed: `OpenDentBusiness/TableTypes/Pref.cs` (since t3: the first build's `PrefName.AuditTrailPatientSelected` member was removed, so the file is as on the base; M2-02, the registry's owner, had chosen not to add an on/off preference for this item, and the setting is now the fork-owned row `ForkAuditTrailPatientSelected`), `PrefInfos.xml` (the setting is not in the Preferences window), `GroupPermission.cs` (table type), `UserControlSecurityTree.cs` (275 already registered by M2-02, audit only, hidden from the tree), `DisplayFields.cs`, `SecurityLogs.cs`.

## Migration level range

No step was needed, so the package's range 2027005401 to 2027005499 is unused. The one new setting, the fork-owned row `ForkAuditTrailPatientSelected`, has no seed row (missing reads as off, the row is inserted when an administrator turns the audit on); since it is not a `PrefName` member, `tools/DevDbTool` no longer lists it among the "optional prefs" with no row. An adopted Open Dental database has no such row, so adoption leaves the audit off. Because no step changes schema or rows on an adopted 25.3.48 database, the adoption tools (`tools/AdoptionValidation`: migration-preview, AdoptionRowCheck, allowed differences) need nothing from this package.

When this package was first built, `ForkMigrationRunner.ValidateStep` accepted only dated `yyyyMMddNN` levels, so the range looked unusable (old question 4). The integration head now accepts package ranges `yyyy00PPNN` (`ForkMigrationRunner.IsReservedPackageLevel`), so the range would be valid if a later change needs a step.

## INFERRED behaviours

- Patient Selected: what counts as a selection (a change of the loaded patient; selecting the patient who is already loaded is not logged), the log text, the per-workstation memory of the repeat rule, the setting's row name (`ForkAuditTrailPatientSelected`), place and wording, and the SecurityAdmin entry when it changes. Open Dental's public pages do not say how the 25.4 entry is switched on; the public Audit Trail Permissions page does not list it.
- Billing/Finance Charges: the entry texts and which settings they list, the open entry being written before the missing-setting checks, the cancelled-run entry, and per-run counters.
- DBM: the check's output wording; deleting only the blockouts the run emptied (the spec's proposal).
- Chart Gender: blank for numbers this program does not know; the Family row following the same rule (the spec's proposal).
- Chart View: the wording of each change, positions shown 1-based, columns compared in order with name, custom name and width, and the renumbering after a delete not being logged.

## Deviations from the spec

- `Schedules.ClearDuplicates` and `GetDuplicateBlockoutCount` are reused as the spec says, but their query was rewritten without `GROUP_CONCAT` (see above). Results are the same except that large groups are no longer at risk of being cut off.
- Patient Selected setting: the spec says "controlled by Audit Trail Permissions"; this fork has no per-permission audit switch, so the setting is one checkbox in Global Security Settings.

## Interplay with packages built at the same time

- **M2-02** (merged in the base): 275 PatientSelected is registered, audit only, and in the Audit Trail filter; the Chart "Gender" display field is in its lists. Since t3 this package adds nothing to `Pref.cs`: M2-02 chose not to add an on/off preference for this item, and the setting is the fork-owned row `ForkAuditTrailPatientSelected` (read and written through B-SERVER's `PrefC.GetRawOrDefault` and `PrefC.UpsertRaw`). In `GroupPermissions.HasAuditTrail` this package comments out the ChartViewsEdit `case` (one line); a merge with other packages that move permissions in or out of that block only needs every line kept.
- **M2-APPT** (also edits `Schedules.cs`): this package's hunk is limited to `GetDuplicateBlockoutCount`, the new `GetDuplicateBlockoutScheduleOpNums` and `ClearDuplicates`. Blockout permissions (272, 273, 13) do not apply to Database Maintenance, which stays under the DBM rules (Setup).
- **M2-NOTES, M2-CARRIER, B-SHELL** (also edit `ControlChart.cs`, and B-SHELL `FormOpenDental.cs`/`FormOpenDentalMenus.cs`): the hunks here are one line in `SwapChartViews`, one new `case` between "Discharge Date" and "Med Urgent" in `FillPtInfo`, one line in the `PatNumCur` setter, the removed `menuItemDuplicateBlockouts_Click` and its menu line, and one line in `menuItemFinanceCharge_Click`. If another package rebuilds the Misc Tools menu, the "Clear Duplicate Blockouts" line must stay out.
- **ControlFamily owners (M2-HIST, V-BRIDGE2, B-SHELL)**: since t1 the Family Gender row calls `PatientGenderDisplay.GetText` (one line in `FillPatientData`, `case "Gender"`). A later package that rewrites that switch keeps the call.
- **B-DBM and other DBM packages**: the new check is its own partial file; `DatabaseMaintenances.cs` is not edited.
- **M2-SCH2, M2-SCH3**: take `ControlChart.cs`, `FormOpenDental.cs` and `FormOpenDentalMenus.cs` over after this package; excluded by the owner's dental-school decision.
- **M2-CHAT / A-CHAT**: excluded, so `v26_1-manage-chat-autolaunch` has nothing to hook into.

## Tests

New project `tests/M2SHELLTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 38 tests:

- `PatientSelectedAuditTests`: repeat rule (per user, per patient, 60 seconds, suppressed selections do not extend it, clock moved back); database: off by default with no row and no entries, `SetEnabled` inserts the row only when turning on (and the writing workstation's cache reads it at once), one entry per selection with PatNum and user, hashed, nothing for PatNum 0 or no user, nothing after turning off; 275 is in the Audit Trail filter; the setting is the row `ForkAuditTrailPatientSelected` and not a `PrefName` member (t3).
- `ChartViewAuditTests`: texts, every changed setting, no change gives no entry, column order changes, flag splitting; 262 is in the Audit Trail filter (t3); database: entries under 262 with the ChartViewNum.
- `FinanceChargeAuditTests`: finance, billing, cancelled and zero-charge texts (en-US).
- `ChartGenderTests`: text per gender, blank for Unknown and unknown numbers, the `FillPtInfo` case and the Family module's `case "Gender"` (source checks).
- `DuplicateBlockoutDbTests`: the A/B/C example (links kept, blockout C deleted, signals), no duplicates (quiet check, valid fix, verbose output), only exact matches, NULL notes, a blockout with no operatory before the run kept, a 301-blockout group, the DBM attribute.
- `WiringTests` (source checks): the setter hook, the Global Security checkbox, the removed tool (files, csproj, menu, handler), the finance entries (open, run, cancelled run, undo and aging kept, totals), the Chart View window and the up/down moves.

Database tests use one scratch MariaDB database per run (`m2shell_<time>_<pid>_<random>`, Open Dental 26.1.70 reference schema, synthetic rows), dropped at the end; they are inconclusive when no server answers or `M2SHELL_TEST_DB=skip`.

Related existing assemblies: `M202Tests` (the 275 permission and the Chart Gender display field), `RegistryTests` (PrefName rules), `ApptTests` (blockout code in `Schedules.cs`).

Not tested: the WinForms windows themselves (the program was not launched): the Global Security checkbox layout, the Billing/Finance Charges run end to end, and the Chart View window and Chart grid on screen.

## Questions for the owner

1. Patient Selected audit: is one on/off switch in Global Security Settings (off by default) the control you want, or should it be per user group?
2. Should selecting the patient who is already loaded (for example picking the same patient again in Select Patient) also write an entry after the 60 seconds? Today only a change of patient is logged.
3. Family module Gender row: since t1 it shows the same text as the new Chart row (translated, blank for Unknown instead of "Unknown"), as the spec proposes. Keep it, or put the 24.3 text back (one line in `ControlFamily.FillPatientData`)?

(Old question 4, about the package's level range, is settled: the runner now accepts `yyyy00PPNN` package levels.)

## Finish round t1

- Base: `port/int-13` (bb150f537) merged into `port/m2-shell-t1`. One conflict, `OpenDentBusiness.csproj`: this package's `DatabaseMaintenances.Schedules.cs` and M2-APPT's `BlockoutPerms.cs` Compile lines, both kept. Everything else merged cleanly; the hooks in `FormOpenDental.cs` (the `PatNumCur` setter, the removed Clear Duplicate Blockouts handler and menu line, the finance-charge close entry), `ControlChart.cs` and `Schedules.cs` (beside M2-APPT's blockout permission code) were checked on the merged head.
- No helper on the base duplicates this package's code (checked: the audit text builders of B-AUDIT, M2-AUDIT, B-CLAIM and M2-CARRIER, and the gender texts in sheets and imports, which call `Lans.g("enumPatientGender",...)` directly and keep "Unknown").
- Brief: no blocking findings were on record (the last run had been cut off). The five in-scope features were already built; `v26_1-manage-chat-autolaunch` stays excluded by the owner's chat decision.
- Change in this round: the Family module's Gender row uses `PatientGenderDisplay.GetText` (see Chart Gender row), with a source test in `ChartGenderTests`.
- Still no fork migration step, schema, CRUD or adoption change.
- Checks on the merged head: one full build of `OpenDental.Local.slnf` through the build lane (0 errors); `run-tests -Mode Core` with `AdoptionValidation.Tests`, `ForkMigrationTests`, `M201Tests` and `SyntheticData.Tests` added (80 assemblies, 4210 tests, none failed; the 2 not passed are M201Tests' published-document checks, skipped as on earlier heads; `.git/od-tools/test-results/port_m2-shell-t1.txt`); `tools/Test-NoHqHosts.ps1` passed. `M2SHELLTests` 36 of 36, `M202Tests` 39, `BDBMTests` 30, `BSHELLTests` 66, `M2APPTTests` 37, `ApptTests` 76, `RegistryTests` 33.

## Finish round t3

- Base: `port/int-14` (9a10a840e, which adds M2-INSPLAN) merged into `port/m2-shell-t3` with no conflicts. M2-INSPLAN touches none of this package's files; the `.sln`, `.slnf` and csproj lines merged automatically. No helper on the new base duplicates this package's code.
- Review finding 1 (spec), fixed: `GroupPermissions.HasAuditTrail` listed `case EnumPermType.ChartViewsEdit:` in its "does not have audit trail" block, so "Chart View Edit" was missing from the Audit Trail filter although this package writes its 26.1 Chart View entries under it. The `case` is now commented out, as PatientSelected's is, and `ChartViewAuditTests.Permission_IsInTheAuditTrailFilter` checks it. `GroupPermissions.cs` is listed under "Outside the owned list". No other test asserted the opposite.
- Review finding 2 (regression), fixed: the fork-invented `PrefName.AuditTrailPatientSelected` was removed from `Pref.cs` (a foundation registry that holds only names Open Dental publishes; M2-02 had chosen not to add this setting). The setting is now the fork-owned row `PatientSelectedAudit.PREF_NAME` = `ForkAuditTrailPatientSelected`, read with `PrefC.GetRawOrDefault(PREF_NAME,"0")` (no exception on every patient change while the row is missing, as `PrefC.GetBoolSilent` threw and swallowed one) and written with `PrefC.UpsertRaw` (no SQL of its own). `SetEnabled` keeps its Middle Tier guard, still writes no row when the audit is turned off and none exists, and returns whether the value changed. `PatientSelectedAuditTests` uses the constant and checks that the name is not in `PrefName`.
- No database ever had the old `AuditTrailPatientSelected` row outside this branch's own test runs: the branch was never merged, and no step seeds the row. So no migration or adoption rule is needed for the rename.
- Still no fork migration step, schema, CRUD or adoption change.
- Checks on the merged head: one full build of `OpenDental.Local.slnf` through the build lane (0 errors); `run-tests -Mode Core` with `AdoptionValidation.Tests`, `ForkMigrationTests`, `M201Tests` and `SyntheticData.Tests` added (81 assemblies, 4243 tests, none failed; the 2 not passed are M201Tests' published-document checks, skipped as on earlier heads; `.git/od-tools/test-results/port_m2-shell-t3.txt`); `tools/Test-NoHqHosts.ps1` passed. `M2SHELLTests` 38 of 38, `M202Tests` 39, `RegistryTests` 33, `BDBMTests` 30, `BSHELLTests` 66, `M2APPTTests` 37, `ApptTests` 76, `NoHqTests` 49.

## Integration (branch `port/int-15`)

- Merged onto `port/int-14` (9a10a840e) with `git merge --no-ff`. `port/m2-shell-t3` had already merged that same head, so the merge had no conflicts and its tree is the branch's tree: the interplay listed above (M2-APPT in `Schedules.cs`; the `ControlChart.cs`, `FormOpenDental.cs` and `FormOpenDentalMenus.cs` hunks beside M2-NOTES, M2-CARRIER and B-SHELL; the `ControlFamily.cs` line beside M2-HIST, V-BRIDGE2 and B-SHELL; the one `HasAuditTrail` line beside M2-02) was connected in rounds t1 and t3 and is unchanged. No helper on the integration head duplicates `PatientSelectedAudit`, `PatientGenderDisplay` or the duplicate-blockout code.
- `OpenDental.sln` and `OpenDental.Local.slnf`: the merge driver added the M2SHELLTests project; every project GUID is unique.
- Still no fork migration step, schema, CRUD or adoption change, so the M1-Z adoption tools need nothing (an adopted database has no `ForkAuditTrailPatientSelected` row, so the audit stays off).
- On the merged head: one full build through the build lane (0 errors), `run-tests -Mode Full` (`test-results/port_int-15.txt`: 81 assemblies, 4,243 tests, none failed; the 2 not passed are M201Tests' published-document checks, inconclusive without `M201_OD_DOCS_DIR` as on earlier heads) and `tools/Test-NoHqHosts.ps1` (passed). M2SHELLTests 38 of 38, M202Tests 39, M2APPTTests 37, ApptTests 76, BDBMTests 30, BSHELLTests 66, M2NOTESTests 51, M2CARRIERTests 55, M2HISTTests 43, RegistryTests 33, AdoptionValidation.Tests 59, ForkMigrationTests 38, SyntheticData.Tests 54.
