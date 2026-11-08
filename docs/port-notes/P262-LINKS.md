# Port note: P262-LINKS, the Open Dental 26.2 program links (items 44 to 47)

Branch `p262/links-1`, cut from `p262/schema-1` (39be2b04b); review fixes on `p262/links-2` (section "Review fixes" below). One of the 26.2 feature packages the owner chose on 2026-11-07 (release notes
https://www.opendental.com/site/version26_2.html, "Program Links"). Fork migration level used: 2026110781 (range 2026110781 to 2026110789).
Tests: `tests/P262LinksTests`.

| Item | 26.2 release note | Here |
|---|---|---|
| 44 | New bridge for Alliedstar Connect | built (the info file layout INFERRED) |
| 45 | New imaging bridge to OrthoComposite | built (the launch arguments partner-gated) |
| 46 | Deprecated Trojan Express Collect bridge | retired, as the DXC Patient Credit Score link was (B-SHELL) |
| 47 | Removed support for Suni | done; the capture itself was removed in v0.0 (BIN-A) |
| (not ported) | Removed the Ez3D-i Bridge | the Ez3D-i link stays (owner decision) |

## Sources

Public sources only, read on 2026-11-07. No Open Dental program was installed, observed or decompiled; no vendor SDK or documentation was
downloaded; no office data was used. Only field names, defaults and technical facts were taken; every text in the code and in this note is
this program's own.

- The 26.2 release notes (version26_2.html): the five "Program Links" lines above. They link no setup page.
- Open Dental's program bridge list (site/programbridges.html): names the link "Allied Star Connect" (page `bridgeasconnect.html`); it lists
  no OrthoComposite, Trojan Express Collect, Suni or Ez3D-i entry.
- Allied Star Connect setup page (site/bridgeasconnect.html): a case management platform that performs intraoral scans; Enabled; Path of file
  to open, default `C:\Program Files\Alliedstar\Connect\Connect.exe`; the properties "Enter 0 to use PatientNum, or 1 to use ChartNum" (the
  patient identifier) and "Info file path" (the .json file, default `C:\ProgramData\Alliedstar\Connect\info.json`); Text on button (optional,
  no default given); Hide Button for Clinics; toolbars, default ChartModule; a 22 x 22 button image. Technical details: of several email
  addresses, the first of the comma-separated list is used. Troubleshooting: the folders of the Info file path must already exist, the bridge
  does not create them (otherwise a "Could not find a part of the path" error).
- OrthoComposite setup page (site/bridgeorthocomposite.html): an imaging program from the Microsoft Store; Enabled; Path of file to open (no
  default); Text on button, default "OrthoComposite"; Hide Button for Clinics; toolbars, default ChartModule; button image. Technical data:
  the patient is found by first name, last name and birthdate; the date format is MM/DD/YYYY; accented letters are allowed in names. It names
  no property and no argument syntax.
- Trojan Express Collect pages (site/trojancollect.html, site/trojancollectbridge.html): the service is no longer offered. The 24.4 manual
  pages (manual/trojancollectbridge.html) describe the old setup only.
- Suni (manual/suni.html, 24.4 manual): Suni discontinued operations; the link switched the Imaging module to the 19.4 interface.
- Open Dental's 26.2 database documents (`OpenDentalDocumentation26-2.xml`, `OpenDentalDiffDocumentation26-2.xml`): `program.ProgName` is
  "enum ProgramName", whose values are not published; the diff lists no program link change; the full document still has the four Suni
  columns of `computerpref` (`SensorType`, `SensorBinned`, `SensorPort`, `SensorExposure`) and `insplan.TrojanID`. No 26.2 table, column or
  enum value this package needs is missing from `p262/schema-1`; none was changed.

## What was built

### Allied Star Connect (item 44)

- `OpenPmsDentalBusiness/VendorDirect/ProgramLinks/AlliedstarConnectLink.cs`: the row (`GetDefinition`, `EnsureProgram` through V-BRIDGE1's
  `BridgeLinkRows`), the launch rules (`GetLaunch`), the info file (`BuildInfoJson`), `GetInfoFilePath`, `IsFullFilePath`, `GetFirstEmail`.
  `BridgeFileLaunch` (same file) is a `BridgeLaunch` with the hand-off file and its text.
- The row is inserted at runtime with `Enabled=0` the first time Setup > Program Links opens (`AlliedstarConnectProgramLinkSetup` in
  `OpenPmsDental/Logic/VendorDirect/P262ProgramLinkSetups.cs`, found by `ProgramLinkSetupRegistry`), like every vendor-direct link: ProgDesc
  "Allied Star Connect", the public default path, the two public properties with their defaults ("0" and the default info path), a Chart
  module button "Allied Star Connect", and a Note that says what the button does and what the office must set up. It is edited in the
  standard Program Link window (Hide Button for Clinics, toolbars, button image work as for any link). The button shows only once the office
  enables the link. A row that exists keeps its settings; only missing properties are added.
- The button (`WpfControlsOD/Bridges/AlliedstarConnect.cs`, dispatched from `ProgramL`), in this order:
  1. a blank path: "Enter the location of the program ..."; a missing program file: the message shows the path;
  2. the Info file path (blank means the default) must be a full file path (a drive or `\\server` path with a folder and a file name);
  3. with a patient: the folder must exist, otherwise a message names the folder; the program never creates it (public page);
  4. with a patient, the info file is written (UTF-8 without a byte order mark); without one, an old info file is removed;
  5. Connect is started with the program's Command line as typed (blank by default; no patient data on the command line).
  If writing or removing the file fails, Connect is not started, so it can never open the wrong patient.
- The info file: one JSON object, every value a string: `PatientId` (the PatNum, or the ChartNumber when the property is 1), `LastName`,
  `FirstName`, `Birthdate` (`yyyy-MM-dd`, blank without one), `Gender` (`Male`, `Female`, `Other`, `Unknown`), `Email` (the first address of
  the patient's comma-separated list). No address, phone, SSN or other data.
- `Programs.ScrubExportedPatientData` removes the info file when the program closes (the link enabled), like the other file hand-offs; its
  helper `ScrubFile` was split out of `ScrubFileForProperty` (same behaviour) so the default path is used when the property is blank.

### OrthoComposite (item 45)

- `OpenPmsDentalBusiness/VendorDirect/ProgramLinks/OrthoCompositeLink.cs`: the row, the launch rules, `FillArguments`.
- The row (runtime, `Enabled=0`, `OrthoCompositeProgramLinkSetup`): ProgDesc "OrthoComposite", no path (the public page gives none), a Chart
  module button "OrthoComposite", and one property of ours, "Patient arguments" (blank).
- The button (`WpfControlsOD/Bridges/OrthoComposite.cs`): a blank path is refused; a drive or network path must name an existing file; a name
  without a folder (an app execution alias such as `OrthoComposite.exe`, which Microsoft Store programs install) or a `shell:` target is left
  to Windows to find. With a patient, blank Patient arguments are refused with "The patient arguments for this program link are not set up
  ..." (V-BRIDGE1's message); otherwise `[FName]`, `[LName]` and `[Birthdate]` (`MM/dd/yyyy` whatever the regional settings) are filled in, in
  one pass, double quotes and control characters removed from the values, accented letters kept. Only these three tokens are filled in, so no
  other patient data can reach the program. The Command line follows the patient arguments. Without a patient the program starts with the
  Command line only.

### Trojan Express Collect retired (item 46)

Done the way B-SHELL retired the DXC Patient Credit Score link (25.2):

- `ProgramLinksDiscontinued.ListProgramNames` gets `TrojanExpressCollect`: `ProgramL.Execute` refuses the link before any dispatch, Program
  Links lists it as "(discontinued)", Program Link Edit keeps it disabled with no toolbars and saves it disabled.
- Removed: `FormTrojanCollect`, `FormTrojanCollectSetup` and `FormTrojanHelp` (code, designer, resx, project entries), the `ProgramL` branch,
  the Program Links case, `EnumFormName.FormTrojanCollect` (used only at runtime to open a window, never stored) and its
  `FormLauncherHelper` case, and the three collect-only queries of `TrojanQueries` (`GetMaxProcedureDate`, `GetMaxPaymentDate`,
  `GetUniqueFileNum`).
- Kept: `ProgramName.TrojanExpressCollect` (migrated rows refer to it by name), the row's properties, the deprecated `TrojanExpressCollect*`
  preferences, the billing type merge in `Defs` (it keeps the stored property consistent), and everything of the Trojan benefit link
  (`ProgramName.Trojan`, `FormPrintTrojan`, the benefit queries, `insplan.TrojanID`).
- Fork migration step `TrojanExpressCollectRemovedStep`, level 2026110781
  (`OpenPmsDentalBusiness/Misc/ForkMigrations/TrojanExpressCollectRemovedStep.cs`, namespace `FeatureSteps`): on every database it sets
  `Enabled=0` on each enabled `program` row of the link and deletes every `toolbutitem` row of such a row; the row, its text and its
  properties stay. `TrojanExpressCollectRemoved.Plan` works the rule out without writing (rows read with the DXC rule's readers, ProgName
  compared as MariaDB compares it) and `Run` writes exactly the planned rows. Idempotent.
- What the step finds: Open Dental's 19.2.29 conversion (`ConvertDatabases6.To19_2_29`, in this repository) created the row in every
  database, with its four properties (`FolderPath`, `BillingType`, `Password`, `PreviousFileNumber`) and one Account module button
  (`toolbutitem` ToolBar 0, "TrojanCollect"), whether or not the office used the link; it enabled the row only where the old Trojan
  preferences held values. `db/init/new-database.sql` has the same shape (program 112 disabled, toolbutitem `(82,112,0,'TrojanCollect')`). So
  on a new database and on an office that never used the link, the row is disabled with that one button, and the step deletes the button
  only ("Deleted 1 toolbar button(s) ... (ToolButItemNum 82)" on a new database); where the office used the link it also disables the row.
  It changes nothing only where there is no row, or the row is disabled and someone removed its button. Nearly every adopted Open Dental
  25.3.48 database therefore loses one button, which migration-preview lists as "deletes its 1 toolbar button(s) (ToolButItemNum N on
  AccountModule)". The button showed nowhere while the row was disabled, so nothing visible changes. Until the integration regenerates
  `db/init/new-database.sql`, the init SQL still holds the button and the step deletes it on the first start of a new database.
- Adoption tools (the step runs on an adopted Open Dental 25.3.48 copy, as the DXC step does):
  - `tools/AdoptionValidation/TrojanExpressCollectPlan.cs`: the step's plan on the rows `DxcCreditScorePlan` reads; migration-preview prints
    it (after the inventory lines and, since the integration, after SWMAP's two settings lines, before the plug-in line).
  - `AdoptionRowCheck`: `program` and `toolbutitem` are held to the DXC and the Trojan Express Collect plans together (the planned rows of
    either may change, `Enabled` only, to 0; the planned buttons of either are deleted; nothing else). The DXC messages keep their wording;
    a Trojan Express Collect part follows when its plan lists rows. `Result.ListProgramNumsDisabledTrojan` and
    `ListToolButItemNumsDeletedTrojan`; the PASS line counts them.
  - `allowed-differences.adoption.json`: the existing `program.Enabled` and `toolbutitem` rules now name both steps; no rule was added.
  - Docs: `docs/adoption-runbook.md` (the step's explanation, step 0.2's preview line and checklist, step 4's log and row check lines, step
    6.1's allowed lines), `tools/AdoptionValidation/README.md`, the usage text of `AdoptionValidation`.
  - Rehearsal (`p262/links-2`): the office fixture (`OfficeFixture`) holds the Trojan Express Collect row as 19.2.29 left it for an office
    that never used the link (disabled, the four properties as `new-database.sql` has them, one Account module "TrojanCollect" button). New
    check M8 (`Rehearsal.CheckTrojanExpressCollect`, after the DXC check M7): migration-preview's Trojan plan names that row and deletes that
    button and disables nothing; the row check's `ListProgramNumsDisabledTrojan` (empty) and `ListToolButItemNumsDeletedTrojan` (the button)
    match the plan; the adoption log has the "Deleted 1 toolbar button(s)" line and no "Disabled" line; the adopted copy keeps the row
    disabled with its text and its four properties, without a button. The rehearsal now runs 32 checks per run (31 before).

### Suni removed (item 47)

- Suni's direct capture (`xImageDeviceManager.dll`, `SuniUSB.dll`, the Capture button of the old Images module, the Suni settings of Imaging
  Setup) was removed in v0.0 by BIN-A. Nothing left reads the four `computerpref` Suni columns or looks for a "Suni" program link.
- This package: the summary of `PrefName.ImagesModuleUsesOld2020` no longer says Suni needs it; the `ComputerPref` Suni fields say they are
  not used; comments in `ControlImages`, `ControlImagesOld`, `FormImagingSetup` and `ComputerPrefs` say that Open Dental 26.2 removed Suni
  support. The columns stay (the 26.2 schema keeps them) and new `computerpref` rows keep their old values. The old Images module itself and
  its preference stay (the release note removes Suni, not that module).

### Ez3D-i

Not ported (owner decision): the Ez3D-i link, its dispatch and its exit scrub are unchanged, and a test checks it is not discontinued.

## Files

- New: `OpenPmsDentalBusiness/VendorDirect/ProgramLinks/AlliedstarConnectLink.cs`, `OrthoCompositeLink.cs` (compiled by the existing
  `VendorDirect\**\*.cs` wildcard); `OpenPmsDentalBusiness/Misc/ForkMigrations/TrojanExpressCollectRemovedStep.cs` (wildcard);
  `WpfControlsOD/Bridges/AlliedstarConnect.cs`, `OrthoComposite.cs`; `OpenPmsDental/Logic/VendorDirect/P262ProgramLinkSetups.cs`;
  `tools/AdoptionValidation/TrojanExpressCollectPlan.cs`; `tests/P262LinksTests/`; this note.
- Deleted: `OpenPmsDental/Forms/FormTrojanCollect.*`, `FormTrojanCollectSetup.*`, `FormTrojanHelp.*`.
- Changed: `TableTypes/Program.cs` (two `ProgramName` values appended after `MeetInTheCloud`), `BridgeLaunch.cs` (three
  `BridgeLaunchProblem` values appended, with messages), `ProgramLinksDiscontinued.cs`, `TrojanQueries.cs`, `Data Interface/Programs.cs`
  (exit scrub), `Pref.cs`, `ComputerPref.cs`, `ComputerPrefs.cs` (comments), `WpfControlsOD/Logic/ProgramL.cs`, `WpfControlsOD/Misc/FormLauncher.cs`,
  `OpenPmsDental/UI/FormLauncherHelper.cs`, `OpenPmsDental/Forms/FormProgramLinks.cs`, comments in `ControlImages.cs`, `ControlImagesOld.cs`,
  `FormImagingSetup.cs`; the adoption tools above; `OpenPmsDentalBusiness/Misc/ForkMigrations/README.md`; the project files,
  `OpenDental.sln` and `OpenDental.Local.slnf`; `docs/release-notes/next.md`.
- No schema, CRUD, preference, permission or stored enum change. `db/init/new-database.sql` is not regenerated (the integration does it).

## INFERRED behaviours (our choices; public sources are silent)

1. **ProgName values** `AlliedstarConnect` and `OrthoComposite`. Open Dental's `ProgramName` values are not published (the 26.2 document
   says only "enum ProgramName").
2. **Rows at runtime, not by a migration step**, as every vendor-direct link since V-BRIDGE1: the rows appear the first time someone opens
   Setup > Program Links, the only place they can be enabled. So adoption adds no program row and the adoption tools need no rule for them.
3. **Allied Star Connect info file layout**: the field names, the order, string values, `yyyy-MM-dd`, the gender words, UTF-8 without a byte
   order mark, and which data is sent (id, names, birthdate, gender, the first email address; the public page names the email only).
4. **Allied Star Connect launch**: Connect is started with no arguments (the Command line as typed) and reads the file itself; without a
   patient the old file is removed and Connect starts without one; a failed write or removal stops the launch; the button text "Allied Star
   Connect" (the page calls it optional and gives none); the ProgDesc follows Open Dental's bridge list.
5. **OrthoComposite launch syntax**: partner-gated. The "Patient arguments" property and its three tokens are ours; the public page gives the
   data and the date format only. Without a patient OrthoComposite starts with the Command line only. A path without a folder or a `shell:`
   target is not checked for existence.
6. **Trojan Express Collect**: the step runs on every database and disables the row and deletes its buttons, because what Open Dental's own
   26.2 conversion does is not public (the DXC precedent). The stored properties (including the encrypted collection password) stay.
7. **Suni**: the four columns and the old Images module stay; only the comments change.

## Owner questions

**Answered on 2026-11-07** (owner, "all as recommended"): the file layout of Allied Star Connect and the arguments of OrthoComposite are
not public. If the office uses either program, the office asks that vendor (questions 2 and 3); until then both links stay switched off
(they are inserted with `Enabled=0` and nothing turns them on). Questions 1 and 4: decided as built (owner, 2026-11-07). No code change.

1. **ProgName values.** If an Open Dental 26.2 database is ever adopted, its rows for these links may use other names; ours would then be
   added beside them. Rename the members if Open Dental's names become known.
2. **Allied Star Connect file layout.** Ask Alliedstar which file layout Connect reads (field names, date format, gender codes, encoding)
   and whether it takes any argument. Until then a patient may not open in Connect.
3. **OrthoComposite arguments.** Ask OrthoComposite's maker how the program takes the name and birthdate. Once known, should a default
   Patient arguments template ship, or stay with the office?
4. **Trojan Express Collect data.** The step keeps the row's properties, among them the encrypted Trojan Collection password and the export
   folder. Remove them in a later step, or keep them as the DXC step kept its row?

## Review fixes (p262/links-2)

The review found that `p262/links-1` rested the Trojan Express Collect retirement on a wrong fact: it said a new database, and an office
that never used the link, has the row disabled without buttons, so the step changes nothing there. Open Dental's 19.2.29 conversion
(`OpenPmsDentalBusiness/Misc/ConvertDatabases6.cs`, `To19_2_29`) writes the Account module button for every database, and
`db/init/new-database.sql` and `UnitTests/Resources/dump.txt` both hold `(82,112,0,'TrojanCollect')`. The step itself was right (it deletes
every button of the link); its description, the test of the new-database case, this note and the runbook were wrong, and the rehearsal
never reached the Trojan branches of the row check, which every real adoption will run.

- Corrected: the summary of `TrojanExpressCollectRemovedStep`, the "What the step finds" paragraph above, `docs/adoption-runbook.md`
  (the step's explanation, step 0.2's preview line, step 4's log and the second person's comparison), `tools/AdoptionValidation/README.md`
  (the fixture, the preview, check M8, `adopt`'s row check and its PASS line), the summary of `Migration26_1Tests`'s Trojan test.
- Tests: the new-database case of `TrojanExpressCollectRetiredTests` inserts the rows as `new-database.sql` has them; the office fixture,
  check M8 and the `RehearsalIntegrationTests` above.
- Changed files: `TrojanExpressCollectRemovedStep.cs` (comment only), `tests/P262LinksTests/TrojanExpressCollectRetiredTests.cs`,
  `tools/AdoptionValidation/OfficeFixture.cs`, `Rehearsal.cs`, `AdoptionValidation.Tests/RehearsalIntegrationTests.cs`,
  `Migration26_1Tests.cs`, `tools/AdoptionValidation/README.md`, `docs/adoption-runbook.md`, `docs/release-notes/next.md`, this note. No
  change to the step's behaviour, the row check's rules or `allowed-differences.adoption.json`.

## Tests

`tests/P262LinksTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 44 tests:

- `AlliedstarConnectLinkTests`: the definition, the order of the checks, the default info path, the launch without a patient, the command
  line as typed, the info file (fields, order, values, ChartNumber, blanks, no other data, regional settings), the first email, the full path
  rules, the note, a message for every `BridgeLaunchProblem`.
- `OrthoCompositeLinkTests`: the definition, the order of the checks, no patient, the Command line, aliases and `shell:` targets, only the
  three tokens filled in, one pass, the birthdate format, control characters, the note.
- `TrojanExpressCollectRetiredTests`: the windows, dispatch and queries are gone and the benefit link's stay, the link is discontinued, the
  step's level, name, namespace and README row, the plan, and the step on the scratch database (disables, deletes the buttons, keeps the
  row, its text and properties, leaves the Trojan and DXC links alone, a second run changes nothing; no row; the rows as
  `new-database.sql` has them, of which only the button goes, with one log line and nothing on a second run; a disabled row with a button).
- `ProgramRowsDbTests`: the rows inserted once with the public defaults, buttons only once enabled, an existing row's settings kept, the
  launches reading the saved properties.
- `WiringTests`: `ProgramName` appended after `MeetInTheCloud`, the dispatch after the discontinued check, the bridges' file handling, the
  setups and project files, the exit scrub, no web address and ASCII-only in the new files, Suni removed with the columns kept, Ez3D-i kept.
- The database tests use one scratch database `test_p262links_<time>_<pid>_<random>` with every table of the 26.2.0 reference, dropped
  afterwards (inconclusive when the server does not answer or `P262LINKS_TEST_DB=skip`).
- `AdoptionValidation.Tests`: `Migration26_1Tests.TrojanExpressCollectPlan_SharesTheDxcRulesAndSaysWhatTheStepChanges` (also the
  plan line for the disabled row with one button). `RehearsalIntegrationTests` (`p262/links-2`): M8 in the check list;
  `Adoption_DeletesOnlyTheTrojanExpressCollectButton` (the fixture's row, Enabled included, and its properties unchanged, its button gone; the
  preview line in M8's detail, the adoption log line and the row check's toolbutitem line agree); `RowCheck_CatchesProgramAndToolButItemChangesTheRulesAllow`
  now also catches a changed column of the Trojan row and its button left in place, with the full wording of the combined messages;
  `RowCheck_HoldsAnEnabledTrojanExpressCollectRowToThePlan` runs `TrojanExpressCollectPlan.Make` and the row check on an original whose
  Trojan row is enabled (an office that used the link: the row disabled and its button deleted pass, the row left enabled or changed in
  another column fail); the runbook dry run checks the preview line, the log line and adopt's `Adopt: PASS` fragment for the link
  (`; it disabled 0 Trojan Express Collect program row(s) and deleted 1 of its toolbar button(s), ...`), out of process and in process;
  `Adoption_DisablesTheDxcLinkAndDeletesItsButtons` expects the Trojan button among the deleted ones; the credential test reads the XVWeb
  password by its link (the Trojan row's empty password is not listed).

Runs on 2026-11-07 (Targeted, each with `NoHqTests` 58/58): `P262LinksTests` 44/44, `VendorDirectTests` 873/873, `VBRIDGE1Tests` 37/37,
`VBRIDGE2Tests` 64/64, `BINATests` 23/23, `AdoptionValidation.Tests` 67/67 (the rehearsal included), `SyntheticData.Tests` 53 passed, 1
inconclusive (no 25.3.48 dump on this computer). `BSHELLTests` 67/68: `Step_HasItsDatedLevel_AndPassesValidation` fails on `p262/schema-1`
already, because the 26.2 schema step (2026110701, not a feature step) is above the DXC step's level; not caused by this package (the new
step is a feature step). `ForkMigrationTests` was not run (its init SQL test fails until the integration regenerates
`db/init/new-database.sql`).

Runs of `p262/links-2` on 2026-11-07, after one full build with 0 errors (Targeted, each with `NoHqTests` 58/58): `P262LinksTests` 44/44,
`VendorDirectTests` 873/873, `VBRIDGE1Tests` 37/37, `VBRIDGE2Tests` 64/64, `AdoptionValidation.Tests` 69/69 (the rehearsal included, 32
checks per run, M8 passing; the two new tests passing). No scratch database was left behind.

Not tested: the windows themselves (the program is not launched), starting Connect or OrthoComposite, writing into a real Connect folder.

## Interplay with the other 26.2 packages

- `ProgramName`: two values appended after `MeetInTheCloud`; another package appending there keeps both blocks (names are stored, not
  numbers).
- `BridgeLaunchProblem`: three values appended; not stored.
- Fork levels: only 2026110781 is used; 2026110782 to 2026110789 stay free for this package.
- The adoption tools' row check now combines two discontinued-link plans; a package that retires another link should extend the same way
  (or turn the two plans into a list).

## Vendor follow-up from public documentation (`next/vendorpub-1`, 2026-11-07)

Owner, 2026-11-07: "yes" to building what a verified search of public vendor documentation found. Branch `next/vendorpub-1`, cut from
`main` e2251ea96. Fork migration level used: 2026110801. Sources read on 2026-11-07, public pages only; nothing was downloaded, no vendor
program was installed, no sign-in was used.

### Sources

- **OrthoComposite's Microsoft Store listing** (https://apps.microsoft.com/detail/9p2mp6p71zk9): the program supports the command arguments
  `--lastname`, `--firstname` and `--dob`; a practice program that starts it with them opens its patient list with the search filled in.
- **Open Dental's OrthoComposite page** (https://www.opendental.com/site/bridgeorthocomposite.html, already listed above): the patient is
  found by first name, last name and birthdate; the date format is MM/DD/YYYY.
- **Medit's article on its Open Dental integration** (https://support.medit.com/hc/en-us/articles/4418103622925, read through its public
  JSON copy https://support.medit.com/api/v2/help_center/en-us/articles/4418103622925.json; title "Open Dental Integration", updated
  2026-07-12): under "Partner Selection (Optional)", a `PartnerID` key added as the last key of the JSON object in Open Dental's "Optional
  command line arguments" pre-assigns a partner to every case created through the integration, for example
  `..., \"Birthdate_yyyyMMdd\": \"[Birthdate_yyyyMMdd]\", \"PartnerID\": \"xxxxx\"}` (the `xxxxx` replaced by the partner's ID); the value
  is in the command line, so changing the partner means changing the program link's settings; its table of supported partners lists one,
  Glidewell Dental, PartnerID 7307395.

### OrthoComposite: the public launch arguments (item 45)

- The launch syntax is public, no longer partner-gated. `OrthoCompositeLink.DefaultPatientArguments` is
  `--lastname "[LName]" --firstname "[FName]" --dob "[Birthdate]"`, with the birthdate as `MM/dd/yyyy` in the invariant culture (always "/").
  A new row gets it as its Patient arguments value (`GetDefinition`), and the Note says what the link sends.
- Existing rows: fork migration step `OrthoCompositePatientArgumentsStep`, level 2026110801
  (`OpenPmsDentalBusiness/Misc/ForkMigrations/OrthoCompositePatientArgumentsStep.cs`, namespace `FeatureSteps`), runs once per database:
  on each OrthoComposite program row (ProgName compared as MariaDB compares it), a ClinicNum 0 "Patient arguments" row that is blank
  (empty or white space only, which the button treats as not set) gets the default; a value the office typed is never changed. The row's
  Note is replaced only where it is still exactly the text `p262/links-1` wrote (`OrthoCompositePatientArguments.NoteBefore`), so an
  office's own note stays. A row without the property gets it from `EnsureProgram` the next time Program Links opens. A database without an
  OrthoComposite row (every adopted Open Dental 25.3.48 database: the row is inserted at runtime by this program) changes nothing, so the
  adoption tools need no rule. Idempotent; the step is needed only because rows inserted by the unreleased 26.2 integration hold the old
  blank default, and after it ran a blank value is the office's own choice (the button then says the arguments are not set up).
- The quoting and control-character stripping stay as built: double quotes and control characters are removed from the values, accented
  letters kept. New: a value no longer ends in a backslash (trailing backslashes are removed), because `"value\"` would escape the closing
  quote of a quoted token on a Windows command line.
- Still INFERRED: the ProgName value; that a blank value with a patient is refused; the Command line after the patient arguments; without a
  patient the program starts with the Command line only; a path without a folder or a `shell:` target is not checked.

### Medit Link: the Partner ID (existing program link)

- New ClinicNum 0 program property "Partner ID" on the Medit Link row (`ProgName` MeditLink, created by Open Dental's 21.1 conversion in
  every database). Inserted blank at runtime, the first time Program Links opens (`MeditLinkProgramLinkSetup`,
  `OpenPmsDental/Logic/VendorDirect/MeditLinkProgramLinkSetup.cs`, through `MeditLinkPartner.EnsureProperty`), like the other runtime rows;
  the program row itself is never inserted or changed, so adoption is unaffected. The setup opens the standard Program Link window as before.
- The button (`WpfControlsOD/Bridges/MeditLink.cs`, rules in `OpenPmsDentalBusiness/VendorDirect/ProgramLinks/MeditLinkPartner.cs`):
  blank Partner ID: exactly the arguments as before (`"--commandUuid" "<uuid>"`, and with a patient `"--commandParam" "<Command line with the
  patient filled in>"`). With a Partner ID and a patient, `, \"PartnerID\": \"<id>\"` is added before the closing `}` of the Command line, as
  the last key of its JSON object, exactly as Medit's article shows, then the patient is filled in. Without a patient there is no command
  parameter, so nothing is added.
- Refused with a message, nothing started (patient selected and Partner ID set): a Partner ID with characters other than letters, digits,
  hyphens and underscores (INFERRED; Medit's IDs are digits); a Command line that already has a PartnerID key (set by hand, as Medit's
  article tells Open Dental users; remove one of the two); a Command line that does not end with `}` (not the JSON object Medit gives).
- How an office uses it: in Setup, Program Links, Medit Link, enter its lab's Medit partner ID in Partner ID (Glidewell's is 7307395 in
  Medit's table), and every case created from the Chart's Medit Link button is assigned to that lab in Medit Link. Clear it to stop. Medit's
  article does not say whether the lab must already be a partner in Medit Link; set up the partnership first (for Glidewell: Partner,
  CDOP@glidewelldental.com, `P262-GLIDEWELL.md`).
