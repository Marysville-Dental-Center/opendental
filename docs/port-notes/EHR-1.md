# Port note: EHR-1 (provider key rule and permissions)

Package EHR-1 of `docs/specs/ehr-rebuild-plan.md` (section 7), on branch `ehr/ehr1-1` from `main` `3faf8831f`. The owner answered E-1 to
E-19 as recommended on 2026-11-08; this package carries out E-4 ("reports EHR" means an `ehrprovkey` row for the provider's last and first
name, the key text optional and never checked, and the permissions of section 5.1), E-5 (the quarterly-key stub with no caller goes, its
table stays) and E-6 (SNOMED's "Map to ICD9" shows to users with Setup whenever the `snomed` table has rows, with no member-country check).
No schema change, no new `PrefName`, no new third-party file, no licensed content, and no project file edited except `tests/EhrTests`'
own. Synthetic data only.

## 1. Design (written before the code)

**The row rule** (section 5). `EhrProvKeys.GetSaveError(lName,fName,yearValue)` in the business layer, a Middle Tier method (the provider
lookup runs on the server). A row may be saved when the last and first names are not blank, a provider has exactly those names, and the
year is 0 to 99 (the two digits of the window's year box, which keeps its own checks). The provider is looked up in the database with
`provider.LName='..' AND provider.FName='..'`, the comparison of the measure queries (`EhrMeasures.GetTable` joins `provider` and
`ehrprovkey` on `LName` and `FName`), so a row the window accepts is a row the measures find. The message names the names no provider
has. The key text is optional, stored as typed and never read. Open Dental's key algorithm is not rebuilt; `FormEHR.ProvKeyIsValid` keeps
returning false for the two CQM windows until EHR-5 (E-10).

**Saving and deleting a row** (section 5.1). `EhrProvKeys.SaveChecked` and `EhrProvKeys.DeleteChecked` (business layer, no Middle Tier
call of their own; every call they make is one): the Setup permission first (checked without a message, so the reason comes back), then
`GetSaveError` for a save; the radiology alert preference as Open Dental had it (the table's first row turns `IsAlertRadiologyProcsEnabled`
on, deleting the last row turns it off); one Setup security log entry with the names and year, and the preference change when there is
one (`GetLogText`, pure). The window checks Setup with the standard message first, calls them, and signals `InvalidType.Prefs` when the
preference changed.

**The EHR setup window.** Provider Keys and Code System Importer join the buttons hidden without Setup. Emergency Now and the window
itself stay for every user (`menuItemEHR_Click` is unchanged).

**SNOMED** (E-6). The "One-Time Tools" group (Map to ICD9) shows when `FormSnomeds.IsSnomedToolsVisible(hasSetup,countSnomeds)`: the
user has Setup and `Snomeds.GetCodeCount()` is above 0. The map needs `ProblemDefEdit`, as the problem windows do: the button checks it
with the standard message before its confirmation, and `FormSnomeds.MapIcd9ToSnomed` checks it again, maps as before, writes one
ProblemDefEdit security log entry with the count and signals `InvalidType.Diseases` when a definition changed.

**The EHR window.** Both provider labels say "(not marked for EHR reporting)" when no row has the provider's names
(`EhrProvKeys.GetReportingNote`, the same row test as before). The dead `butPatList14_Click` (no button in the designer) and
`QuarterlyKeyIsValid` (no caller) are deleted; the `ehrquarterlykey` table stays, unread.

**Tests** (`tests/EhrTests`): `ProvKeyRuleTests.cs` and `PermissionTests.cs`, and `Support/ScratchDb.cs`, the scratch database on the
local MariaDB (names start with `ehr1_`), built from `db/init/new-database.sql` and the fork migrations as `tests/InventoryTests` builds
its own.
