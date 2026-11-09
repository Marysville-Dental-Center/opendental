# Port note: EHR-1 (provider key rule and permissions)

Package EHR-1 of `docs/specs/ehr-rebuild-plan.md` (section 7), on branch `ehr/ehr1-1` from `main` `3faf8831f`. The owner answered E-1 to
E-19 as recommended on 2026-11-08; this package carries out E-4 ("reports EHR" means an `ehrprovkey` row for the provider's last and first
name, the key text optional and never checked, and the permissions of section 5.1), E-5 (the quarterly-key stub with no caller goes, its
table stays) and E-6 (SNOMED's "Map to ICD9" shows to users with Setup whenever the `snomed` table has rows, with no member-country check).
No schema change, no new `PrefName`, no new third-party file, no licensed content, and no project file edited except `tests/EhrTests`'
own. Synthetic data only. The only removals are the two the plan names: the dead `butPatList14_Click` handler and the quarterly-key stub.

## 1. The key checks replaced (section 5), as built

| Gate | Built |
|---|---|
| Saving a key row (`FormEhrProviderKeyEdit.butSave_Click`) | No longer asks `FormEHR.ProvKeyIsValid` (always false, "Invalid provider key"). The rule is `EhrProvKeys.GetSaveError(lName,fName,yearValue)`, a Middle Tier method (the lookup runs on the server): `""` when the last and first names are not blank (`IsNullOrWhiteSpace`), a provider has exactly those names, and the year is 0 to 99; otherwise the reason. The provider is looked up in the database with `provider.LName='..' AND provider.FName='..'`, the comparison of the measure queries (`EhrMeasures.GetTable` joins `provider` and `ehrprovkey` on `LName` and `FName`; the collation makes it case-insensitive and ignores trailing spaces, for both). The message names the names: "No provider has the last name 'X' and the first name 'Y'. Enter the names exactly as they are in the provider's record, or add the provider first." The window keeps its own year checks first (blank; `ValidNum` 0 to 99). The key text is optional, stored as typed (a null becomes "") and read by nothing. The designer says "optional, not checked" beside the key box, and the explanation above no longer sends offices to Open Dental's customer service for a key |
| SNOMED "Map to ICD9" (`FormSnomeds_Load`) | `groupBox1.Visible=IsSnomedToolsVisible(Security.IsAuthorized(EnumPermType.Setup,true),Snomeds.GetCodeCount())`: Setup and at least one `snomed` row (the existing count). No provider, key or member-country check (E-6) |
| EHR window labels (`FormEHR_Shown`) | Both provider labels end with `EhrProvKeys.GetReportingNote(LName,FName)`: " (not marked for EHR reporting)" when `GetKeysByFLName` finds no row (the same row test as before, so blank names have no row), otherwise nothing |
| `QuarterlyKeyIsValid` | Deleted (no caller). `ehrquarterlykey`, `EhrQuarterlyKey` and its Crud stay, unread |
| `ProvKeyIsValid` | Unchanged (EHR-5's lines 624-628): still false; its last callers are the two CQM windows, until EHR-5 (E-10) |
| CPOE and lab windows, the radiology preference | Unchanged in behaviour: a row turns CPOE on for that provider's user; the table's first row turns `IsAlertRadiologyProcsEnabled` on and deleting the last row turns it off, now inside `SaveChecked` / `DeleteChecked` |

Provider names stay locked while a row exists (`FormProvEdit.cs`, not edited), so a row keeps matching its provider.

## 2. Permissions once the key gate is gone (section 5.1), as built

| Action | Built |
|---|---|
| Provider Keys and Code System Importer (`FormEhrSetup_Load`) | `butProviderKeys.Visible=false;` and `butCodeImport.Visible=false;` join the block that runs without Setup. Emergency Now (and its panel) and the window stay for every user; the Code Systems group itself stays. `menuItemEHR_Click` is unchanged |
| Saving or deleting a row (`FormEhrProviderKeyEdit`) | Both buttons check `Security.IsAuthorized(EnumPermType.Setup)` (the standard message) first, Delete before its confirmation, then call `EhrProvKeys.SaveChecked(ehrProvKey,ehrProvKeyOld,out isPrefChanged)` / `DeleteChecked(ehrProvKey,out isPrefChanged)`. These check Setup again without a message (`GetSetupError`, the standard "Not authorized" text, also with no user), then `GetSaveError` for a save, write the row, the radiology preference as before, and one Setup security log entry (`GetLogText`): "EHR reporting row added: 'Last, First', year 26.", "... changed from 'A, B', year 25 to 'C, D', year 26.", "... saved, names and year unchanged: ...", "... deleted: ...", each followed by " The radiology procedures alert (IsAlertRadiologyProcsEnabled) was turned on." (or off) when the save or delete changed it. The key text is never logged. A refusal writes nothing. The window shows the reason, and signals `InvalidType.Prefs` exactly when the preference changed |
| SNOMED tools group | Setup and rows (above) |
| "Map to ICD9" (`butMapToSnomed_Click`) | `Security.IsAuthorized(EnumPermType.ProblemDefEdit)` (the standard message) before the confirmation, as `FormDiseaseDefs.cs` does. The map itself moved into `FormSnomeds.MapIcd9ToSnomed(out error)`: ProblemDefEdit checked again without a message (-1 and the reason, nothing changed), then the same map as before (a problem definition with no SNOMED CT code whose ICD-9 code is in `Snomeds.GetICD9toSNOMEDDictionary`), one ProblemDefEdit security log entry "Map to ICD9 (SNOMED CT window): SNOMED CT codes added to N problem definitions from their ICD-9 codes.", and `DataValid.SetInvalid(InvalidType.Diseases)` when N is above 0 |
| `EnumPermType.EhrKeyAdd` | Unused, as before; the security tree still hides it |
| Value-set and CPT imports, Measure Calc, CQM windows | Not this package (EHR-2, EHR-4a, EHR-5) |

## 3. Deviations and choices the plan left open

- **Where the rules live.** The plan names one new method, `EhrProvKeys.GetSaveError`. The Setup check, the radiology preference and the
  security log entry are in the business layer too (`SaveChecked`, `DeleteChecked`, `GetLogText`, `GetReportingNote`, at the end of
  `EhrProvKeys.cs`), so they are testable without a window and work the same through the Middle Tier (each call they make is a Middle
  Tier call of its own). The signals stay in the windows (`DataValid` is not in the business layer), as EHR-0 did.
- **The map moved into a static method of the window** (`FormSnomeds.MapIcd9ToSnomed`, with the pure `IsSnomedToolsVisible`), because it
  signals through `DataValid` and no business file of the map is EHR-1's. The window's `FormShowFeatures.HandleEhrClick` of EHR-0 is the
  precedent.
- **The map's security log entry is written on every run**, also when it changed nothing (count 0), so every use of the bulk tool is in
  the log; the Diseases signal only when something changed.
- **The year** is 0 to 99, as the window's year box allows ("Ex. 12"; a "5" was stored as 5 before and still is).
- **The explanation text of `FormEhrProviderKeyEdit`** is now set in the designer (`label2.Text`), because this package edits only `.cs`
  and `.designer.cs` content of the program while S1-5 converts its project. The old text's `label2.Text` entry in
  `FormEhrProviderKeyEdit.resx` is no longer read by anything; the lead may delete that one `<data>` element after S1-5 (it still says to
  get a key from customer service). The designer's `ComponentResourceManager` line went with it (no other resource of the window is read).
- **`FormEHR.cs` 57-63** (a comment and a commented-out message box of Open Dental's that quote "ehr provider key") are outside this
  package's lines and stay; comments tell the history.
- **Tests: a third file.** The plan's list names `ProvKeyRuleTests.cs` and `PermissionTests.cs`; the scratch database they share is
  `tests/EhrTests/Support/ScratchDb.cs`, so EHR-2 and EHR-4b can use it. The project gained its three `Compile` items and a reference to
  `Required dlls\MySqlConnector.dll` (the helper creates and drops its database itself, as `tests/InventoryTests` does).

## 4. Tests

`tests/EhrTests`, 21 new tests (88 in all). The database tests share one scratch database on the local MariaDB (127.0.0.1:3306):
`ehr1_<UTC start>_<process>_<random>_main`, built by the first test that needs it from `db/init/new-database.sql` (statement by statement)
and the fork migrations, dropped by `[AssemblyCleanup]`; leftovers older than 12 hours are dropped first, and only names starting with
`ehr1_`. `EHR_TEST_DB=skip` skips (inconclusive), `EHR_KEEP_DB=1` keeps it. Synthetic providers, users and groups (made-up letter names),
SNOMED rows with made-up codes `TEST0001` and `TEST0002`, and problem definitions with the ICD-9 code taken at run time from the engine's
own list (`Snomeds.GetICD9toSNOMEDDictionary().First()`) or the made-up `TEST999`; no code of any licensed system is written in the tests.
The tests set the en-US culture before they connect, so `Lans.g` never touches the language table.

- `ProvKeyRuleTests` (12): blank names (empty, spaces, a tab, null) refused; a name no provider has refused, and the message names it; the
  last name of one provider with the first name of another refused; an apostrophe accepted; years 0, 9, 12, 26, 99 accepted and -1, 100,
  2026 and the extremes refused (and the window's own year checks); `GetSaveError` agrees with the measures' join for the exact names,
  other case, a trailing space, a different name and swapped names; an empty, a null and any other key text accepted and stored as typed,
  a refused row not written; a changed row updated, not inserted, and a bad change refused; the radiology preference (first row on, second
  row nothing, deleting one of two nothing, deleting the last off, each in the log, and the window signals Prefs only then); the EHR
  window's note; the log texts (never the key); **`HasProvHadKey` through the Middle Tier mock** (`OpenDentalServerMockIIS` on the scratch
  database, `RemotingClient.MiddleTierRole=ClientMT`, a user with a known password): true for a row of any year and for a name with an
  apostrophe, false without a row and for another first name, with `GetSaveError`, `GetReportingNote` and `SaveChecked` through the mock
  too (the Setup entry carries the user), and the same answer directly afterwards; the window asks no key and says the key is optional;
  only the two deliberate removals are gone (`ProvKeyIsValid`'s callers are the two CQM windows, `EhrQuarterlyKey` stays,
  `butPatList_Click` stays).
- `PermissionTests` (9): save, change and delete without Setup (a user with ProblemDefEdit, EhrKeyAdd and EhrMeasureEventEdit, and no
  user) refused, nothing written or logged; with Setup the three Setup entries with the user, the names and the year; the window checks
  Setup before saving and before the delete confirmation, writes only through the checked methods, and those check Setup before anything
  else; the setup window hides the two buttons without Setup and nothing hides Emergency Now, the menu item checks nothing; `EhrKeyAdd`
  stays unused; the SNOMED rule (every combination), the window's use of it, and the data (two synthetic rows counted; Setup sees the
  group, ProblemDefEdit alone does not; no rows, hidden); the map without ProblemDefEdit refused (-1, the reason, nothing mapped, logged or
  signalled); with it mapped, the ProblemDefEdit entry with the count, one Diseases signal, and a second run that logs 0 and signals
  nothing; the button checks ProblemDefEdit before its confirmation.

Targeted runs (`run-tests.ps1 -Mode Targeted`, NoHqTests always included), after the full lane build:

| Assembly | Result |
|---|---|
| EhrTests | 88 of 88 passed |
| MiddleTierMockTests | 125 of 125 passed (its Middle Tier rules read the new business methods' IL) |
| NoHqTests | 58 of 58 passed |
| ReleaseFlowTests | 119 passed, 1 skipped (`BuildIdFiles_AreAllInTheReleaseOutput`, which needs a release output), 0 failed |
| LibraryBindingTests | 12 of 12 passed |
| BSHELLTests | 68 of 68 passed |

`InitSqlTests` and `ForkMigrationTests` are not affected (no schema change) and were not run. After the runs no `ehr1_` database was left.

## 5. For the lead

- **Release notes.**
  - EHR provider keys: this program has no license keys. A provider reports EHR when Setup > Chart > EHR > Provider Keys has a row with
    the provider's last and first name, exactly as in the provider's record, and a two-digit year; the key is optional and not checked.
    As in Open Dental, a row turns CPOE on for that provider's user, and the first row turns on the radiology procedures alert setting.
  - Adding, changing or deleting a provider row needs the Setup permission and is recorded in the security log. Provider Keys and Code
    System Importer are hidden in the EHR setup window for users without Setup; Emergency Now stays for everyone.
  - SNOMED CT window: the One-Time Tools (Map to ICD9) show to users with Setup whenever SNOMED CT codes have been imported, with no
    member-country check (each office licenses SNOMED CT itself). Map to ICD9 needs Problem Definition Edit and is recorded in the
    security log.
  - The EHR window says "(not marked for EHR reporting)" after a provider who has no row.
  - The quality measure windows still list no provider until a later release (EHR-5).
- **Project file and baseline.** `tests/EhrTests/EhrTests.csproj` gained `PermissionTests.cs`, `ProvKeyRuleTests.cs` and
  `Support\ScratchDb.cs` as `Compile` items and a `MySqlConnector` reference (`Required dlls`). `tools/project-baseline` on `main` does
  not list EhrTests yet; re-record it for the project (section 6 of the plan). S1-6 converts the project after this merges.
- **The stale `.resx` entry** of section 3 (`FormEhrProviderKeyEdit.resx`, `label2.Text`), for after S1-5.
- **Worktree.** The `p262-tools` worktree was at a detached `953160fbf` with an uncommitted regeneration of `tools/project-baseline/*`
  (not this package's); it was set aside with `git stash` ("p262-tools: uncommitted project-baseline regeneration left at detached
  953160fbf, set aside before EHR-1") before the branch was made. Drop it or keep it.
- **Launch checks (section 11).**
  2. EHR window: open it for a patient whose primary provider has no row: the provider label ends with "(not marked for EHR reporting)",
     and so does the user's label for a provider user without a row. Add a row for that provider and open it again: no note.
  3. Provider keys and permissions, on a test database:
     - As a user without Setup: Setup > Chart > EHR shows no Provider Keys and no Code System Importer button; Emergency Now is there and
       turns red and back.
     - As a user with Setup: Provider Keys > Add, an existing provider's last and first name, year 26, no key text: it saves; the security
       log has a Setup entry "EHR reporting row added: 'Last, First', year 26." (for the table's first row also the radiology alert line).
       Edit it to year 25: "changed from ... to ...". Delete it: "deleted".
     - A last name no provider has: refused with "No provider has the last name '...' and the first name '...'".
     - Log on as that provider's user, enter a procedure, and check that it is marked CPOE (`Userods.IsUserCpoe`,
       `FormProcEdit.cs:2664`, `Procedures.cs:4435`).
     - SNOMED CT window (Setup > Chart > EHR > SNOMED CT): with no SNOMED rows the One-Time Tools group is hidden even with Setup; with
       rows (an import, or one synthetic test row) it shows for a user with Setup and not for one without. Map to ICD9 as a user without
       Problem Definition Edit: the standard "Not authorized" message before the confirmation, nothing changed; with it: the count, and a
       Problem Definition Edit entry in the security log.
