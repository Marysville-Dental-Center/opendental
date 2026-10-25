# Port note: M2-SHELL, main window and cross-module audit (26.1)

Branch `port/m2-shell`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run, beside the other M1 Tier B and M2 packages. Backlog section: [port-backlog.md, M2-SHELL](../specs/port-backlog.md#m2-shell-main-window-and-cross-module-audit-261). No fork migration step (see "Migration level range" below).

Sources: the feature specs in `docs/specs/features-25.4.md` and `features-26.1.md`, and the public pages they cite (the 25.4 and 26.1 release notes, the public manual pages for the Audit Trail and Audit Trail Permissions, read 2026-10-25). No Open Dental program, install or office data was used. Every text below and in the code is written for this fork.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-security-patientselected-audit` | built | Patient Selected (275) entry on every change of the loaded patient, off by default, 60-second repeat rule. Setting in Setup, Security, Global Security Settings. |
| `v26_1-misc-audit-billing-finance` | built | "window opened" entry and one entry per run (settings and result, also for a cancelled run); the 24.3 entry written on close is gone; undo and aging entries unchanged. |
| `v26_1-tools-dbm-duplicate-blockouts` | built | DBM check `SchedulesDeleteDuplicateBlockouts`; Tools, Misc Tools, Clear Duplicate Blockouts and `FormBlockoutDuplicatesFix` deleted; both defects of `Schedules.ClearDuplicates` fixed. |
| `v26_1-manage-chat-autolaunch` | not built | Owner decision 2026-10-18: intra-office chat is excluded (A-CHAT and M2-CHAT dropped), so there is no chat window to open. |
| `v26_1-chart-ptinfo-gender` | built | "Gender" row in the Chart module's Patient Information grid (field registered by M2-02). The Family module's existing Gender row is not changed (see Deviations). |
| `v26_1-misc-audit-chartviews` | built | Entries for add, edit (every changed setting, including columns), delete and the up/down moves, under ChartViewsEdit (262). |

## What was built

### Patient Selected audit (25.4)

- `OpenDentBusiness/Misc/Shell26_1.cs`, class `PatientSelectedAudit`: `IsEnabled`, `LogIfEnabled(patNum)`, the repeat rule `ShouldLog(userNum,patNum,now)` and `SetEnabled(bool)`.
- Hook: one line in the `FormOpenDental.PatNumCur` setter, after the value changed. Every path that changes the loaded patient goes through that setter (patient select window, family menu, search results, `GlobalFormOpenDental.PatientSelected` from every module and window, task and appointment "go to"), so one hook covers them all.
- Entry: `securitylog` row with PermType 275, the patient's PatNum and the text "Patient selected." Not written for PatNum 0 (unloading the patient) or when nobody is logged on.
- Repeat rule (spec, fork): a user who selects the same patient again within 60 seconds gets no second entry. It is kept in memory per workstation and counts from the last entry written.
- Setting: `PrefName.AuditTrailPatientSelected` (bool, off). Checkbox "Log each patient selection in the Audit Trail" at the bottom of Global Security Settings (SecurityAdmin). There is no seed row: a missing row reads as off (`PrefC.GetBoolSilent`), and `SetEnabled` inserts the row the first time an administrator turns the audit on. Turning it on or off writes a SecurityAdmin entry, so switching the audit off is itself audited.

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

### Chart View audit (26.1)

- `ChartViews.cs`: `LogAdded`, `LogEdited`, `LogDeleted`, `LogMoved` and the pure text builders (`GetEditChanges`, `GetFlagText`, `GetColumnsText`). Entries go under ChartViewsEdit (262) with the ChartViewNum as FKey.
- `FormChartView`: keeps a copy of the view and its columns when it opens; Save writes "added" for a new view or "edited: setting old to new; ..." for every changed setting (description, procedure statuses, object types, dates, procedure notes, audit mode, selected teeth only, treatment plan charting, Orion statuses, columns with their custom names and widths). Saving with no real change writes nothing. Delete writes "deleted".
- `ControlChart.SwapChartViews` (the up/down buttons): "moved from position a to b", 1-based. The automatic renumbering after a delete (`SyncChartViewItemOrders`) is not a user move and is not logged.

## Files touched

- New: `OpenDentBusiness/Misc/Shell26_1.cs`, `OpenDentBusiness/Data Interface/DatabaseMaintenances.Schedules.cs`, `tests/M2SHELLTests/` (project), this note.
- Owned by this package: `OpenDentBusiness/Data Interface/ChartViews.cs`, `Schedules.cs`; `OpenDental/Main Modules/ControlChart.cs` (2 small hunks), `FormOpenDental.cs` (setter line, removed handler, removed close entry), `FormOpenDentalMenus.cs` (1 line removed); `OpenDental/Forms/FormChartView.cs`, `FormFinanceCharges.cs`, `FormBlockoutDuplicatesFix.*` (deleted).
- Outside the owned list (also in the report):
  - `OpenDentBusiness/TableTypes/Pref.cs` (foundation registry): one `PrefName` member, `AuditTrailPatientSelected`, next to the other AuditTrail preferences. M2-02 deliberately added no on/off preference for this item ("M2-SHELL can use the existing audit trail settings"), but no existing setting can turn one audit type off, and the spec requires "off by default".
  - `OpenDental/Forms/FormGlobalSecurity.cs` and `.designer.cs`: the checkbox (no package owns this window).
  - `OpenDentBusiness/OpenDentBusiness.csproj` (2 Compile lines), `OpenDental/OpenDental.csproj` (form entries removed), `OpenDental.sln`, `OpenDental.Local.slnf` (test project).
- Not changed: `PrefInfos.xml` (the setting is not in the Preferences window), `GroupPermission(s).cs`, `UserControlSecurityTree.cs` (275 already registered by M2-02, audit only, hidden from the tree), `DisplayFields.cs`, `SecurityLogs.cs`, `ControlFamily.cs`.

## Migration level range

The assigned range 2027005401 to 2027005499 cannot be used: `ForkMigrationRunner.ValidateStep` requires `yyyyMMddNN` with a real date, and "20270054" has month 00. No step was needed: the one new preference has no seed row (missing reads as off, the row is inserted when an administrator turns the audit on). `tools/DevDbTool` lists it among the "optional prefs" with no row.

## INFERRED behaviours

- Patient Selected: what counts as a selection (a change of the loaded patient; selecting the patient who is already loaded is not logged), the log text, the per-workstation memory of the repeat rule, the setting's name, place and wording, and the SecurityAdmin entry when it changes. Open Dental's public pages do not say how the 25.4 entry is switched on; the public Audit Trail Permissions page does not list it.
- Billing/Finance Charges: the entry texts and which settings they list, the open entry being written before the missing-setting checks, the cancelled-run entry, and per-run counters.
- DBM: the check's output wording; deleting only the blockouts the run emptied (the spec's proposal).
- Chart Gender: blank for numbers this program does not know.
- Chart View: the wording of each change, positions shown 1-based, columns compared in order with name, custom name and width, and the renumbering after a delete not being logged.

## Deviations from the spec

- Chart Gender, "use one rendering in both modules": the Family module's Gender row (`ControlFamily.cs`, raw enum name, "Unknown" for unknown) is unchanged, because `ControlFamily.cs` is not in this package's file list and other packages edit it in this wave. The helper is public; switching the Family row is a one-line change (`PatientGenderDisplay.GetText(_patient.Gender)` in its `case "Gender"`), left to the owner's decision (question 3).
- `Schedules.ClearDuplicates` and `GetDuplicateBlockoutCount` are reused as the spec says, but their query was rewritten without `GROUP_CONCAT` (see above). Results are the same except that large groups are no longer at risk of being cut off.
- Patient Selected setting: the spec says "controlled by Audit Trail Permissions"; this fork has no per-permission audit switch, so the setting is one checkbox in Global Security Settings.

## Interplay with packages built at the same time

- **M2-02** (merged in the base): 275 PatientSelected is registered, audit only, and in the Audit Trail filter; the Chart "Gender" display field is in its lists. This package adds `PrefName.AuditTrailPatientSelected` in `Pref.cs` (one member between `AuditTrailEntriesDisplayed` and `AuditTrailUseReportingServer`); a merge with other `PrefName` additions only needs both members kept.
- **M2-APPT** (also edits `Schedules.cs`): this package's hunk is limited to `GetDuplicateBlockoutCount`, the new `GetDuplicateBlockoutScheduleOpNums` and `ClearDuplicates`. Blockout permissions (272, 273, 13) do not apply to Database Maintenance, which stays under the DBM rules (Setup).
- **M2-NOTES, M2-CARRIER, B-SHELL** (also edit `ControlChart.cs`, and B-SHELL `FormOpenDental.cs`/`FormOpenDentalMenus.cs`): the hunks here are one line in `SwapChartViews`, one new `case` between "Discharge Date" and "Med Urgent" in `FillPtInfo`, one line in the `PatNumCur` setter, the removed `menuItemDuplicateBlockouts_Click` and its menu line, and one line in `menuItemFinanceCharge_Click`. If another package rebuilds the Misc Tools menu, the "Clear Duplicate Blockouts" line must stay out.
- **ControlFamily owners (M2-HIST, V-BRIDGE2, B-SHELL)**: can use `PatientGenderDisplay.GetText` for the Family Gender row once the owner decides (question 3).
- **B-DBM and other DBM packages**: the new check is its own partial file; `DatabaseMaintenances.cs` is not edited.
- **M2-SCH2, M2-SCH3**: take `ControlChart.cs`, `FormOpenDental.cs` and `FormOpenDentalMenus.cs` over after this package; excluded by the owner's dental-school decision.
- **M2-CHAT / A-CHAT**: excluded, so `v26_1-manage-chat-autolaunch` has nothing to hook into.

## Tests

New project `tests/M2SHELLTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 35 tests:

- `PatientSelectedAuditTests`: repeat rule (per user, per patient, 60 seconds, suppressed selections do not extend it, clock moved back); database: off by default with no row and no entries, `SetEnabled` inserts the row only when turning on, one entry per selection with PatNum and user, hashed, nothing for PatNum 0 or no user, nothing after turning off; 275 is in the Audit Trail filter.
- `ChartViewAuditTests`: texts, every changed setting, no change gives no entry, column order changes, flag splitting; database: entries under 262 with the ChartViewNum.
- `FinanceChargeAuditTests`: finance, billing, cancelled and zero-charge texts (en-US).
- `ChartGenderTests`: text per gender, blank for Unknown and unknown numbers, the `FillPtInfo` case (source check).
- `DuplicateBlockoutDbTests`: the A/B/C example (links kept, blockout C deleted, signals), no duplicates (quiet check, valid fix, verbose output), only exact matches, NULL notes, a blockout with no operatory before the run kept, a 301-blockout group, the DBM attribute.
- `WiringTests` (source checks): the setter hook, the Global Security checkbox, the removed tool (files, csproj, menu, handler), the finance entries (open, run, cancelled run, undo and aging kept, totals), the Chart View window and the up/down moves.

Database tests use one scratch MariaDB database per run (`m2shell_<time>_<pid>_<random>`, Open Dental 26.1.70 reference schema, synthetic rows), dropped at the end; they are inconclusive when no server answers or `M2SHELL_TEST_DB=skip`.

Related existing assemblies: `M202Tests` (the 275 permission and the Chart Gender display field), `RegistryTests` (PrefName rules), `ApptTests` (blockout code in `Schedules.cs`).

Not tested: the WinForms windows themselves (the program was not launched): the Global Security checkbox layout, the Billing/Finance Charges run end to end, and the Chart View window and Chart grid on screen.

## Questions for the owner

1. Patient Selected audit: is one on/off switch in Global Security Settings (off by default) the control you want, or should it be per user group?
2. Should selecting the patient who is already loaded (for example picking the same patient again in Select Patient) also write an entry after the 60 seconds? Today only a change of patient is logged.
3. Family module Gender row: switch it to the same text as the new Chart row (translated, blank for Unknown instead of "Unknown")?
4. The level range assigned to this package (2027005401 to 2027005499) is not a valid `yyyyMMddNN` level; future packages with a step need a range the runner accepts.
