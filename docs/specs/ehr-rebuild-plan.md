# The EHR rebuild: package plan

**What this is.** The package plan for bringing back what Open Dental's closed `EHR.dll` gave the program, rebuilt as open code in
Open PMS - Dental. Package briefs are cut from it the same way as from `docs/specs/net10-plan.md`. Each package names its goal, the files
it owns, its public basis and licence, its work, its tests and its window.

**Date.** 2026-11-08. **Status.** Decided: the owner answered E-1 to E-19 as recommended on 2026-11-08 (section 13). EHR-5 merges
after counsel's answer to E-10; the other counsel items (E-6, E-8, E-9, E-11) are applied when counsel answers.

**Owner decision (2026-11-08).** "Allow EHR again. build what EHR.dll would provide". The standing rule applies: keep and port everything.
That an office does not use a feature is no reason to cut it. Nothing is decompiled; only public sources are used.

**Runs alongside.** This plan runs beside the .NET 10 stages of `docs/specs/net10-plan.md` and the chart packages of
`docs/specs/wpf-tooth-chart-plan.md`, and it respects their packages and file owners. No EHR package edits a file while a stage or chart
package owns it. Section 10 gives the order for every shared file. Where this plan needs a line in one of those documents, section 16 says
so, and the lead makes the change.

**Basis.**
- Two read-only surveys of `main`: the EHR.dll hooks, and the public specifications.
- An adversarial review of the first draft with 24 findings (2 blocking, 5 high, 7 medium, 10 low), and a second review of the revised
  plan with 15 findings. Each finding was checked again in the repository and in public sources before its fix was applied. The appendix
  says how each one was resolved.
- Spot checks at `05e123429`, after S0-F merged (`1fd383731`). S0-F moved two places this plan names: `menuItemEHR_Click` in
  `FormOpenDental.cs` and the start of `ProgramEntry.Main`. This revision gives their new line numbers. The other source files this plan
  names are unchanged since `bb446f65a`, where the first draft took its line numbers.
- Public specifications and public pages (section 18). Some pages could not be opened directly during this revision. A fact that rests
  only on a search engine's summary of a page is marked "(summary)", and the package that relies on it checks it first.
- Nobody looked inside `EHR.dll`, and nothing was decompiled. The DLL is not in the repository and was not fetched. What it did is
  inferred from the code that called it (at `8c1703bdc^`), from the names involved and from Open Dental's public manual.
- Nothing was built, run, restored or downloaded.

---

## 1. The plan in one page

| Package | Rebuilds | Earliest window | Size |
|---|---|---|---|
| **EHR-0 Switch, time check, clean-up** | Show Features allows EHR (H7) and the Setup Wizard matches it; TimeSync becomes its own setting (Off or Warn, Off by default) with checked NTP replies; leftovers removed | Stage 1, first wave (S1-0 has merged) | M, 3-4 dev-days |
| **EHR-1 Provider key rule and permissions** | `ProvKeyIsValid` and `QuarterlyKeyIsValid` (H5, H6) replaced by local rules with no licence keys; the Setup permission where the key gate was the only gate | Stage 1, second wave (merges before S1-6 starts) | S, 1.5-2 days |
| **EHR-3 CCD and CCR stylesheets** | `GetEhrResource("CCD"/"CCR")` (H2): open stylesheets with notices, run only in a child process, for display and exports | After the stage 1 gate, before S2-4 starts | M, 5-7 days |
| **EHR-2 Value sets** | `EhrCodes.Listt` (H1): a fork-only `ehrcode` table, filled by an import of the office's own VSAC download of the June 2013 EP release | Stage 2 after EHR-3, or stage 4; **before the bridge release** | M, 5-7 days |
| **EHR-4a Measure Calc window** | The Meaningful Use measures window and its drill-down (H3, H4), on the existing open engine | After S1-5 and EHR-1; stage 2 or stage 4 | M, 4-6 days |
| **EHR-4b Measure fixtures** | Spec-derived meet and miss cases for the three stages, golden numbers and a known-deviation list | After EHR-4a; stage 4 or later | M, 4-6 days |
| **EHR-5 CQM reports and QRDA** | The 2014 and 2011 quality-measure windows reopened, with this program's own identity in what QRDA writes | After EHR-2 and counsel's E-10; stage 4 or 3a wave 1 | S-M, 3-4 days |

Total: about 26-36 dev-days. These are rough figures for orientation.

**Decisions in short.**
1. Turning EHR on works again, in Show Features and in the Setup Wizard, with the same confirmation in both.
2. TimeSync no longer follows `ShowFeatureEhr`. It becomes its own setting: Off (the default) or Warn. The program never sets the clock by
   itself. Windows' own time service does that (recommended), and the manual window sets the clock only on a button, with checks (E-3).
3. This open build has no licence keys. A provider "reports EHR" when an `ehrprovkey` row exists for their last and first name. The key
   text is optional and never checked. Adding or deleting a row needs the Setup permission. Open Dental's key algorithm is not rebuilt.
4. Value-set members are never shipped. Each office imports, under its own UMLS licence, VSAC's "June 2013 EP Only Release" (publication
   date June 14, 2013). That release holds the value sets of the nine 2014 eCQM versions the engine reports. The importer reads its Excel
   workbook or its SVS XML (section 8).
5. The CCD and CCR stylesheets are the public Apache-2.0 ones: HL7 CDA-core-xsl and the AAFP's `ccr.xsl`. They ship with their notices.
   XSLT never runs in the program's own process. A short-lived child process of the program's own exe runs it (`/ehr-render`), with time,
   size and depth limits. The test that forbids XSLT today is rewritten to allow it in exactly that child and nowhere else.
6. The measures window and the CQM reports are labelled for internal review only. This program is not certified.
7. The EHR track runs one package at a time and uses at most one of the three writer slots. In stage 1 it never takes a slot from S1-T
   or S1-1: it takes the slot TC-0 frees, and later one that a second-wave package frees (section 10.1). Every package aims for the
   bridge release. Only EHR-2 must merge before it, because it is the only one with a schema change.

---

## 2. What EHR.dll provided, and what replaces it

| Hook | What it did (at `8c1703bdc^`) | Today | Replacement | Package |
|---|---|---|---|---|
| H1 Code list | `EHR.EhrCodeList.GetListt` returned a `DataTable` of value-set members (2014 eCQMs and the EHR pick lists) | `EhrCodes.cs:21-36`: always an empty list | Fork-only table `ehrcode`, filled by the office's import | EHR-2 |
| H2 Stylesheets | `GetEhrResource("CCD"/"CCR")` read XSL text from the DLL's resources | `EhrSummaryCcds.cs:96-98`: returns "" | Apache-2.0 stylesheets as resources of the business library, run in a child process | EHR-3 |
| H3 Measures window object | Created when the EHR window opened | The `FormEHR.cs:31-34` constructor no longer does it | Not needed. The window is created on click | EHR-4a |
| H4 Measures button | `EHR.FormEhrMeasures` plus `SelectedPatNum` | `FormEHR.cs:526-529`: "not available in this build" | New open window on the existing engine (`EhrMeasures.cs:17,75,117,328,2834,5844`) | EHR-4a |
| H5 Provider key | `ProvKeyIsValid(lName,fName,year,key)` | `FormEHR.cs:626-628`: always false | Local rules (section 5) | EHR-1, EHR-5 |
| H6 Quarterly key | `QuarterlyKeyIsValid(...)` | `FormEHR.cs:632-634`: false, no caller | Deleted. The table stays | EHR-1 |
| H7 Show Features | Checked only that `EHR.dll` existed | `FormShowFeatures.cs:83-92`: refuses | Allowed, with a confirmation | EHR-0 |
| H8 `#if EHRTEST` | Early binding for Open Dental's own builds | `ControlChart.cs:28-30` is dead | Deleted by TC-2, which edits line 27 (section 10.2) | TC-2 |

---

## 3. Turning EHR on again

### 3.1 Both windows allow it, with the same behaviour

- **Show Features.** The refusal at `FormShowFeatures.cs:83-92` goes.
  - Checking the box shows one confirmation (OK/Cancel). Cancel unchecks it. OK is followed by the existing restart message (`:91`).
  - Unchecking shows the restart message, as today.
  - The value is saved at `:144`, as today.
- **Setup Wizard.** `checkEhr` gets a Click handler that calls the same confirmation (`UserControlSetupWizFeatures.cs` and its
  `.Designer.cs`). The value is saved at `:57`, as today.
- **One source for the text.** The confirmation is a public static method on `FormShowFeatures`, which the wizard already opens
  (`UserControlSetupWizFeatures.cs:44-50`).
- **The confirmation says:**
  - turning EHR on changes how prescriptions, patient race, required fields and the Chart work;
  - it shows the EHR tools;
  - this program is not certified EHR technology, and its measures cannot be used to attest or submit to any program;
  - a restart is needed.
- **The wizard's explanation is replaced** (`UserControlSetupWizFeatures.resx:179-182`). It still tells offices to contact Open Dental for
  an EHR contract, and says the software is 2014-Edition certified. That is a false claim for this program.
- **No password** (E-2). Open Dental asked for none for EHR. Show Features asks for one only for clinics (`FormShowFeatures.cs:65-81`).

### 3.2 What turning it on does again

The 43 `ShowFeatureEhr` lines in 25 files (hooks survey, section 3) behave as Open Dental had them:

- **Chart**
  - The EHR toolbar button (`ControlChart.cs:4643`, enabled at `:4510` and `:4937`) opens `FormEHR` (`:7846`).
  - The Tobacco Use row appears (`:4315`).
  - The chart redraw cache is used (`FormOpenDental.cs:1727,1759`; these are TC-2's lines and are not edited here).
- **Patient**
  - The EHR tab: mother's maiden name and date/time deceased (`FormPatientEdit.cs:237,2526`).
  - The EHR race list and its rules (`:74,885,2235,2282`).
- **Required fields**
  - EHR-only fields and conditions (`FormRequiredFields.cs:79,111`, `FormReqFieldCondEdit.cs:54`, `RequiredFields.cs:145`,
    `RequiredFieldConditions.cs:175`).
- **Medical**
  - The Family Health History and Tobacco Use tabs (`FormMedical.cs:83`). The Tobacco tab needs the value sets (EHR-2).
  - The "Tobacco Use" display field (`FormDisplayFields.cs:33`, `DisplayFields.cs:808`).
- **Prescriptions and orders**
  - CPOE labels (`FormMedPat.cs:32`).
  - The provider is fixed to the user when that user is a provider who is not a hygienist (`FormRxEdit.cs:46-50`), and the dosage code
    is shown (`:144`). This follows `ShowFeatureEhr` alone, not the key rows.
  - No blank Rx and the RxNorm warning (`FormRxSelect.cs:32,125,205`).
- **Elsewhere**
  - SNOMED body site and note search on procedures (`FormProcEdit.cs:273`).
  - The provider's Meaningful Use stage (`FormProvEdit.cs:62`).
  - Syndromic observations (`FormApptEdit.cs:227`).
  - Rx BIN (`FormInsPlan.cs:504`).
  - Summary-of-care warnings and the CCD e-mail on referrals (`FrmRefAttachEdit.xaml.cs:238`, `FrmReferralsPatient.xaml.cs:437`).
  - Patient Export (`FormReportsMore.cs:45`).
  - The Direct Message send option (`FormEmailMessageEdit.cs:169-171`, restored in S0-3D). The address's Direct certificate comes from
    the Direct provider's file since the certificate-import package of O-13 (`FormEmailCertRegister`, `docs/port-notes/DIRECT-CERT.md`):
    the Certificate button of Edit Email Address shows with or without EHR, because signed e-mail uses the same certificate (O-1).
- **Already reachable without EHR on:** Setup > Chart > EHR (`FormOpenDentalMenus.cs:252`) and the Chart's CCD button
  (`ControlChart.cs:4638-4642`).
- **No longer tied to EHR:** TimeSync (`FormOpenDentalThreads.cs:759`, section 4).

### 3.3 What it does not do

- It does not make the program certified, and it does not make any number usable for a CMS program (section 9.5).
- It changes no schema. `ShowFeatureEhr` is an existing preference.

---

## 4. The time check

### 4.1 Today

- `BeginTimeSyncThread` (`FormOpenDentalThreads.cs:755-768`) starts only when the program runs on the database server's PC and EHR is on
  (`:759`).
- Every 4 hours `TimeSyncWorker` (`:771-783`) asks the NTP server in `NistTimeServerUrl` (`time.nist.gov`, `ConvertDatabases8.cs:1143`),
  then calls `WindowsTime.SetTime`.
- `WindowsTime.SetTime` (`WindowsTime.cs:26-41`) calls `SetLocalTime`. That needs the "Change the system time" right
  (`SE_SYSTEMTIME_NAME`). Its return value is ignored (`:38`), so a standard user's call fails silently. It is called once, where
  Microsoft says to call it twice across a daylight-saving change.
- The NTP client trusts any reply (`NTPv4.cs:18-56`):
  - It checks no mode, stratum, leap indicator, echoed originate time or zero timestamp, and not the length received.
  - It takes the originate time from the reply (`:40`), not from what it sent.
  - An all-zero reply gives an offset of about −63 years, and the thread would set the clock to it.
  - After the NTP era ends in February 2036, every reply converts to a date near 1900.
  - The request byte `0x1B` (`:80`) asks as version 3, although the comment says version 4. 45 CFR 170.210(g) allows either.
  - It connects to the first address the name resolves to, on an IPv4 socket, so an IPv6 first address throws.
- The manual window `FormEhrTimeSynch` (Setup > Chart > EHR > Time Synch, `FormEhrSetup.cs:132-135`, hidden without Setup permission,
  `:43`) sets the clock as soon as it opens: `FormEhrTime_Load` calls `SynchTimes`, which calls `SetTime` (`FormEhrTimeSynch.cs:26-33`,
  `:77-104`), on whatever PC opens it. `TimesInSynchFast` (`:36-74`) and the `IsAutoLaunch` path have no caller.

### 4.2 The setting

- A fork-owned preference row `ForkTimeSyncMode`: `0` Off, `1` Warn. A missing row or any other value means Off, so no database step is
  needed. It is not in `PrefName`.
- It is read with `PrefC.GetRawOrDefault`. It is written with `PrefC.UpsertRaw`, then `DataValid.SetInvalid(InvalidType.Prefs)` and a
  SecurityLog entry under Setup, as `RecallProphyPerioSwitch.cs:33-60` does. (`UpsertRawNoCache` would leave this workstation's cache
  stale, `PrefC.cs:433-445`.)
- `FormEhrTimeSynch` gets the choice "Check the database server's clock every 4 hours". The setting applies at the next start.
- `TimeSyncCheck.ShouldStart(mode,isOnDbServer)` is a pure function: true only for Warn on the database server's PC (the same
  `ODEnvironment.IsRunningOnDbServer` test). That is the clock the audit timestamps come from.

### 4.3 How the check works

- **Recommended: Windows' own time service.** Windows Time uses NTP. On Windows 10 1607 / Server 2016 or later it can reach 1-second
  accuracy with the settings Microsoft's support-boundary page describes. ONC accepts an operating-system clock that is synchronised to a
  traceable source, with a 5-second test tolerance (ONC NTP Reference Document v1.0).
  - An administrator points the database server at `time.nist.gov`:
    `w32tm /config /manualpeerlist:time.nist.gov /syncfromflags:manual /update`, and checks it with `w32tm /query /status`.
  - This is an office IT step. The program never changes it.
- **Warn (mode 1).**
  - Every 4 hours the thread sends one request and reads the reply with `NtpReply.TryParse` (below).
  - `TimeSyncCheck.Decide(mode,parseResult)` is a pure function. It returns None or Alert, never "set the clock".
  - When a valid reply shows an offset above 5 seconds, it raises one `AlertType.Generic` alert (`AlertItem.cs:80-81`): "The clock of the
    database server's computer is N seconds off <server>. Set up Windows Time (see the manual window)." The alert is not repeated while
    one is open.
  - An invalid reply or no reply is written to the log only. After 6 failed checks in a row (one day), one alert says that the time check
    has failed for a day, with the last reason.
  - A kiss-o'-death reply (stratum 0) stops the checks until the next start and logs its code.
- **The NTP reply rules** (`NtpReply.TryParse(reply,sentTransmit,destinationUtc,out offset,out reason)` in `NTPv4.cs`, a pure function):
  - at least 48 bytes received;
  - leap indicator not 3 (unsynchronised), version 3 or 4, mode 4 (server);
  - stratum 1 to 15;
  - the originate time equals, bit for bit, the transmit time the client sent (an off-path sender cannot guess it);
  - receive and transmit times are not zero;
  - round-trip delay between 0 and 1 second, and root delay / 2 + root dispersion below 1 second (RFC 5905's MAXDIST);
  - each timestamp is placed in the NTP era nearest the local clock, so the 2036 rollover works;
  - the offset uses the client's own send time, never the copy in the reply.
- **The request.** The client sends version 4 (`0x23`), sets the transmit time, and connects to the first IPv4 address of the name.
  `getTime` stays, built on `TryParse`, for the manual window.

### 4.4 Why the program no longer sets the clock by itself

The first draft had a third mode, "Set the clock". It is dropped (E-3), for these reasons:
- NTP from `time.nist.gov` is not authenticated. The reply rules stop forged replies from off the network path, but anyone on the path can
  still move the database server's clock, and with it every audit timestamp.
- One sample from one server cannot do what a full NTP client does: filter samples, compare servers, slew instead of step.
- The program would need the "Change the system time" right in the account of whoever is logged on to the server.
- Windows Time already does this job as a service, with its own limits on large corrections, and two clock-setters fight.
- The capability is kept: Windows Time keeps the clock, Warn says when it drifts, and the manual window can still set it on a button
  where Windows Time is disabled (section 4.5).

### 4.5 The manual window

- Opening it only measures: this computer's offset from the NTP server and from the database server, as today. It never sets the clock.
- A button "Set this computer's clock" sets it. It is enabled only when:
  - the last reply was valid under the rules of section 4.3;
  - the offset is between 1 second and 1 hour (a larger error is fixed by hand in Windows);
  - the user has the Setup permission (the window is already hidden without it, `FormEhrSetup.cs:43`).
- If the Windows Time service is not disabled on that PC, the button gives way to advice. The window reads the service with
  `ServiceController`, as `CodeBase/Utilities/ODprintout.cs:46-48` reads the print spooler, and checks its `StartType`, not only its
  status. On a PC outside a domain, Windows Time starts on demand ("Manual (Trigger Start)"), so it is often stopped although Windows
  still uses it to set the clock.
  - Running: the advice is to run `w32tm /resync` as administrator.
  - Stopped but not disabled: the advice is to start it (`net start w32time`) and then run `w32tm /resync`, as administrator, because
    `w32tm /resync` fails while the service is stopped.
  - Disabled: the button is offered. Its note says that another time program on that PC may change the clock again.
- It shows the server and the offset and asks to confirm. Then `WindowsTime.TrySetUtc` calls `SetSystemTime` with UTC (no daylight-saving
  ambiguity) and checks the result.
  - On success it writes the Event Log entry (`WindowsTime.cs:40`) and a SecurityLog entry with the old and new time.
  - With `ERROR_PRIVILEGE_NOT_HELD` it says that only an elevated administrator, or an account given "Change the system time" in Local
    Security Policy, can set the clock. The program never asks for elevation and never changes user rights.
- `TimesInSynchFast` and the `IsAutoLaunch` path (no caller; they set the clock without these checks) are removed.

### 4.6 Databases that have EHR on today

Those databases lose the automatic clock setting. There is no migration. The release notes say so and give the Windows Time steps. E-3 asks
the owner to confirm.

---

## 5. The key checks replaced

There are no licence keys in this open build. `ehrprovkey` rows stay as the office's list of "providers who report EHR". `ehrquarterlykey`
stays in the database and is not read.

| Gate (file:line) | Today | Becomes | Package |
|---|---|---|---|
| Saving a key, `FormEhrProviderKeyEdit.cs:50` | `ProvKeyIsValid` is false, so "Invalid provider key" | Valid when the last and first names are not blank, a provider has exactly those names (the comparison the measure SQL uses, `EhrMeasures.cs:333-338`), and the year has two digits (already checked at `:44-49`). The key text is optional and stored as typed. The message names the missing provider. New `EhrProvKeys.GetSaveError` | EHR-1 |
| CQM provider lists, `FormEhrQualityMeasures.cs:38`, `FormEhrQualityMeasures2014.cs:43` | Nobody is listed: "No providers found with ehr keys." | Listed when `EhrProvKeys.HasProvHadKey(LName,FName)` (`EhrProvKeys.cs:50`): any row for those names, any year. The message says where to add them | EHR-5 (E-10) |
| SNOMED "Map to ICD9", `FormSnomeds.cs:33-49` | Hidden unless the logged-on user is a provider with a valid key ("to prevent Snomeds from being used in non-member nations") | Shown when the user has the Setup permission and the `snomed` table has rows. The office imports SNOMED under its own licence (E-6) | EHR-1 |
| EHR window labels, `FormEHR.cs:45-56` | "(no ehr provider key entered)" | "(not marked for EHR reporting)". The same row test | EHR-1 |
| CPOE, `Userods.cs:464`; lab windows, `FormEhrLabOrders.cs:116`, `FormEhrLabOrderEdit2014.cs:531`, `FormEhrLabOrderImport.cs:179` | Row tests that already work | Unchanged. Adding a row turns CPOE on for that provider's user at 9 call sites, as in Open Dental | none |
| First key row, `FormEhrProviderKeyEdit.cs:60-67`; last row deleted, `:32-37` | Turns `IsAlertRadiologyProcsEnabled` on, and off again | Unchanged. Nothing in this program raises its alert (section 15, E-18) | none |
| `QuarterlyKeyIsValid`, `FormEHR.cs:632-634` | No caller | Deleted (E-5) | EHR-1 |
| `ProvKeyIsValid`, `FormEHR.cs:626-628` | Stub | Deleted when its last callers (the CQM windows) move | EHR-5 |
| Measure dates (the old `EhrQuarterlyKeys.FindStartDate` and `ValidateDateRange`, removed in `31caf62fd`) | none | No date gate | EHR-4a |

- Provider names stay locked while a key row exists (`FormProvEdit.cs:104-111`), so a row keeps matching its provider.
- The two CQM windows keep calling `ProvKeyIsValid`, which stays false, until EHR-5. That keeps counsel's E-10 apart from EHR-1.

### 5.1 Permissions once the key gate is gone

The key was a licence check, but it was also the only thing that kept these actions from every user. With it gone (E-4):

| Action (file:line) | Today | Becomes | Package |
|---|---|---|---|
| Provider Keys and Code System Importer buttons, `FormEhrSetup.cs:30-44` | Visible to every user; the Setup check hides other buttons only | Hidden without the Setup permission. Emergency Now and Close stay for everyone, as today | EHR-1 |
| Saving or deleting a key row, `FormEhrProviderKeyEdit.cs:24-74` | No check | Needs Setup. Writes a SecurityLog entry (Setup) with the names and year, and with the radiology preference change when there is one | EHR-1 |
| SNOMED tools group, `FormSnomeds.cs:33-49` | Key gate only | Setup permission and SNOMED rows (above) | EHR-1 |
| "Map to ICD9", `FormSnomeds.cs:149-164` | Bulk-updates every problem definition with no check | Needs `ProblemDefEdit`, as the problem windows do (`FormDiseaseDefs.cs:220`). Writes a ProblemDefEdit SecurityLog entry with the count and signals `InvalidType.Diseases` | EHR-1 |
| The value-set import and the CPT import, `FormCodeSystemsImport.cs` | No check (the button is visible to all) | Need Setup, checked in the window as well as by the hidden button | EHR-2 |
| Measure Calc and the CQM windows | The key gate | No new gate, as in Open Dental: they open from the EHR window, like the EHR patient lists today (`FormEHR.cs:566-569`). The release notes say they list patients across the practice | EHR-4a, EHR-5 |

- `menuItemEHR_Click` (`FormOpenDental.cs:4596-4602`) stays as it is: every user may open the EHR setup window for Emergency Now.
- `EnumPermType.EhrKeyAdd` stays unused. It was Open Dental HQ's ("Only used at OD HQ", `GroupPermission.cs:190-192`), and the security
  tree hides it (`UserControlSecurityTree.cs:698,738`).

---

## 6. Rules for every EHR package

- **Ownership.** One EHR writer at a time, in its own worktree, using one of the three writer slots (net10-plan 2.1). No two packages edit
  a file at once. The lead greps each brief's patterns against the running packages first.
- **Project files.** No EHR package edits a project file, except `tests/EhrTests`' own project.
  - New business classes go at the end of existing files, as S0-3 and S0-6 did.
  - A migration step goes under `Misc\ForkMigrations\`, which the SDK-style project compiles by its default glob since S1-3
    (`44f55ca54`).
  - Stylesheets go in through `ResXFileRef` entries of the existing `OpenPmsDentalBusiness/Properties/Resources.resx`.
  - New files in `OpenPmsDental` wait for S1-5 (SDK-style, globbed).
- **Tests.**
  - EHR-0 creates `tests/EhrTests/` as a classic MSTest v1 project with explicit items, as TC-0 does. EHR-1 adds its items while it is
    classic. The lead adds the sln and slnf entries and re-runs `tools/Get-ProjectBaseline.ps1` for it.
  - It references the program and the business library, as `tests/LibWebTests` does, so the program's exe is next to the tests.
  - S1-6 converts it with the other test projects, and S4-1 moves it to MSTest 4. No EHR test edit merges while S1-6 or S4-1 runs.
  - Golden and fixture files are read from the repository folder, as the other test projects find their sources, so no copy item is
    needed in either project format.
  - Database tests use the scratch-database helpers. Middle Tier tests set the mock server per test
    (`RemotingClient.MiddleTierRole=ClientMT`, `OpenDentalServerMockIIS`), as in S0-T.
- **Rules of 3a, kept in advance.**
  - No WinForms `ContextMenu`, `MenuItem` or `DataGrid` (S3a-M3's scan).
  - Every `new Process()` sets `UseShellExecute` explicitly: false only when streams are redirected (S3a-2's scan).
  - No `Thread.Abort` (S3a-1), no `Encoding.Default` (S3a-3), no `Encoding.GetEncoding` of a code page in new code.
  - `InvariantCulture` for machine-read numbers (S3a-3).
  - No new `PrefName`. Fork rows are read by string, so S3a-9 is not affected.
- **Data.**
  - No real CPT, CDT, SNOMED CT or VSAC content enters the repository through EHR work.
  - Test fixtures use synthetic codes starting with `TEST`, codes of CDCREC, CVX and HCPCS Level II (US government systems), and codes
    the engine's source already contains. A test takes those last codes from the engine's own strings at run time; they are never
    written as literals in fixture files. So nothing new is added, and no fixture changes if counsel has the inherited lists moved to the
    office's import (E-8, E-9).
  - A test enforces this. It also fails on a literal of any code from the engine's lists in `tests/EhrTests`, other than CDCREC, CVX and
    HCPCS Level II codes.
  - ICD-10-CM and SOP codes are treated as licensed until counsel says otherwise (E-8).
- **Schema.** Only EHR-2 changes the schema, through a fork step. It must merge before the bridge release: stage 3b changes no schema
  (net10-plan 2.1), and the bridge release must open every database it may be rolled back to (`ForkVersionGuard`).
- **Each package** writes `docs/port-notes/EHR-n.md` and gives its release-note lines to the lead.

---

## 7. Packages

### EHR-0 Switch, time check, clean-up

- **Goal.** Turning EHR on works in both windows (section 3). TimeSync becomes the setting of section 4. The leftovers of the hook removal
  go.
- **Owns.**
  - The switch:
    - `OpenPmsDental/Forms/FormShowFeatures.cs` (83-92, plus the shared confirmation)
    - `OpenPmsDental/User Controls/SetupWizControls/UserControlSetupWizFeatures.cs`, `.Designer.cs` (the Click wiring) and `.resx`
      (179-182)
  - The time check:
    - `OpenPmsDental/Main Modules/FormOpenDentalThreads.cs` (755-783; the `EhrCodeList` leftovers at 104 and 821)
    - `OpenPmsDentalBusiness/Misc/NTPv4.cs` (the request byte, the IPv4 address, `NtpReply.TryParse` as a second class at the end)
    - `OpenPmsDentalBusiness/Misc/WindowsTime.cs` (`TrySetUtc` on `SetSystemTime` with a checked result; `TimeSyncCheck` with
      `ShouldStart` and `Decide` as a second class at the end of the file)
    - `OpenPmsDental/Forms/FormEhrTimeSynch.cs` and `.designer.cs`
  - Tests:
    - `NoHqTests/NoHqTests.cs` (155-162, the EHR region only)
    - new `tests/EhrTests/` with `EhrSwitchTests.cs`, `TimeSyncTests.cs`, `NtpReplyTests.cs` and `NoEhrDllTests.cs`
  - Docs: `docs/decoupling-plan.md:48`, `docs/third-party-binaries-audit.md:133` (both still say "remove"). TC-4 and TC-9b own the
    audit later (section 10.2).
- **Public basis.**
  - 45 CFR 170.210(g) (NTP).
  - ONC NTP Reference Document v1.0.
  - Microsoft's `SetSystemTime`/`SetLocalTime` and Windows Time documentation.
  - RFC 5905 (the NTP packet, kiss-o'-death, the era rule).
  - No licensed content.
- **Work.**
  - Section 3.1 and section 4.
  - The thread starts only when `ShouldStart` says so, and calls `Decide`. The window uses `TryParse` and the rules of 4.5.
  - The `EhrCodeList` "do not kill" case goes. The enum value stays, marked `[Obsolete]` like `CacheFillForFees`, so the later values keep
    their numbers. There is no preload; EHR-2 measures whether one is needed.
  - `Ehr_ReflectionHooksAreGone` leaves NoHqTests, because EHR-2 and EHR-3 make its three assertions false. Its place is a source scan in
    `NoEhrDllTests`: no `"EHR.dll"` outside comments, no `Assembly.LoadFile` in the EHR files, and no `#if EHRTEST` outside
    `ControlChart.cs` until TC-2 deletes it there.
  - Both changed time files keep writing the Event Log through `ProgramNames.EventLogSource`, which ReleaseFlowTests pins
    (`ProgramNamesTests.cs:227-228`).
- **Tests.**
  - Both windows call the same confirmation, and saving no longer refuses.
  - `ShouldStart` and `Decide` cover every combination, including Off on the server and Warn on a workstation.
  - `TryParse` with synthetic bytes built from RFC 5905's field layout: a good reply, each rule broken in turn, a kiss-o'-death reply, an
    all-zero reply, a reply with a different originate time, a short reply, and replies on both sides of the 2036 era rollover.
  - A failed `SetSystemTime` (through a seam) gives the message with the reason, not silence.
  - Opening the manual window sets no clock (the seam is never called).
  - NoHqTests, BSHELLTests and ReleaseFlowTests.
- **Order and size.** First. It takes the slot TC-0 frees and merges in stage 1's first wave (section 10.1). It touches no project file
  except its own new test project. M, about 3-4 dev-days.

### EHR-1 Provider key rule and permissions

- **Goal.** The gates of section 5 that do not depend on counsel, and the permissions of section 5.1.
- **Owns.**
  - `OpenPmsDental/Forms/FormEhrProviderKeyEdit.cs` (24-74) and `.designer.cs` (the key label says "optional, not checked")
  - `OpenPmsDental/Forms/FormEhrSetup.cs` (30-44)
  - `OpenPmsDental/Forms/FormSnomeds.cs` (33-49, 149-164)
  - `OpenPmsDental/Forms/FormEHR.cs` (29-30, 45-56; 571-575 and 630-634: the dead `butPatList14` handler, which has no button in the
    designer, and the quarterly stub)
  - `OpenPmsDentalBusiness/Data Interface/EhrProvKeys.cs` (new `GetSaveError`)
  - `tests/EhrTests/ProvKeyRuleTests.cs` and `PermissionTests.cs`
- **Public basis.** Open Dental's public manual pages on annual and quarterly keys, for what the keys gated only. No key algorithm is
  reproduced or validated.
- **Work.** As in sections 5 and 5.1. The SNOMED rule uses the existing `Snomeds` count.
- **Tests.**
  - Saving: blank names, a name no provider has, a two-digit year and an empty key are each accepted or refused as section 5 says
    (scratch database).
  - Saving and deleting without Setup are refused; with Setup they write the SecurityLog entry.
  - The two setup buttons are hidden without Setup, and Emergency Now stays.
  - The SNOMED group shows only with Setup and rows; the map refuses without `ProblemDefEdit` (source check plus a data test).
  - `HasProvHadKey` through the Middle Tier mock.
- **Order and size.** After EHR-0. It is written when the first second-wave package merges, and it merges before S1-6 starts, because
  it adds items to the classic EhrTests project (section 10.1). No project file except EhrTests'. S, about 1.5-2 days.

### EHR-3 CCD and CCR stylesheets

- **Goal.**
  - `GetEhrResource` returns real stylesheets again.
  - Received and generated CCDs and CCRs show as readable pages.
  - File exports carry a working stylesheet and a rendered page.
  - XSLT never runs inside the program's own process, and a stylesheet that came with an e-mail never runs anywhere.
- **Owns.**
  - The stylesheets and their resources:
    - new `OpenPmsDentalBusiness/Resources/Ehr/` (`CDA.xsl`, `cda_l10n.xml`, `cda_narrativeblock.xml`, `ccr.xsl`, unmodified)
    - `OpenPmsDentalBusiness/Properties/Resources.resx` and `.Designer.cs`
    - `OpenPmsDentalBusiness/Data Interface/EhrSummaryCcds.cs` (94-98; a second class `EhrStylesheetStarter` at the end: the parent side)
  - The child: new `OpenPmsDental/Main Modules/EhrRenderChild.cs` (the only file that may name an XSLT class), and
    `OpenPmsDental/Main Modules/ProgramEntry.cs` (`Main` at 20; the new first statement goes before line 21).
  - Display: `FormEhrSummaryOfCare.cs` (110-146) and `FormEhrSummaryCcdEdit.cs`.
  - The export and attachment lines (eight lines in six files):
    - `FormContinuityOfCareDocument.cs:69`
    - `FormEhrClinicalSummary.cs:75,126`
    - `FormEhrElectronicCopy.cs:122`
    - `FormEhrPatientExport.cs:200-229`
    - `FormEhrSummaryOfCare.cs:213,271`
    - `WpfControlsOD/Frms/FrmReferralsPatient.xaml.cs:247`
  - The test that forbids XSLT: `tests/LibWebTests/HtmlGenerationTests.cs` (`NoStylesheet_FromAnEmail_RunsInTheProgram`, 101-145, and
    `CcdView_KeepsTheDocumentInMemory_AndWritesNoFile`, 147-180). No stage or chart package names this file: S1-6 owns only the
    LibWebTests project file, and S4-1 owns it only if it fails to compile under MSTest 4.
  - Notices: `THIRD-PARTY-NOTICES.txt` (two entries) and `tests/AttributionTests/ReleaseIdentityTests.cs` (18-22).
  - The rename guard: `tests/ReleaseFlowTests/ProgramNamesTests.cs` (one entry in the map at 217-240).
  - Docs: `docs/port-notes/LIB-WEB.md` (the `FormEhrSummaryCcdEdit` row, 121).
  - Tests: `tests/EhrTests/Cda*Tests.cs` with golden files under `tests/EhrTests/Golden/`.
- **Public basis and licence.**
  - HL7 CDA-core-xsl: XSLT 1.0, renders any CDA R2 document (so C-CDA R1.1 as `EhrCCD.cs:218-220` writes, and R2.1), Apache-2.0. Its
    README says it needs `cda_l10n.xml` and `cda_narrativeblock.xml` beside `CDA.xsl`. Its top level has no LICENSE or NOTICE file; the
    README and the stylesheet's header state the licence.
  - The AAFP `ccr.xsl`: "Copyright 2007 American Academy of Family Physicians", Apache-2.0.
  - Apache-2.0 fits the program as distributed under GPL v3 (`NOTICE`, "The program as distributed"), as the Google APIs entry already
    does (`THIRD-PARTY-NOTICES.txt:206-212`; the Apache text is at `:286`).
  - Displaying a CCR does not need the proprietary ASTM E2369 text.
- **Work.**
  1. **Resources.**
     - The four files, unmodified, at the pinned upstream versions (E-11, batch E). The one exception is `ccr.xsl`, if the owner picks
       the copy marked as modified (E-11).
     - Each is a `ResXFileRef` of type `System.String` with the encoding part `;utf-8`, as the two e-form entries already are
       (`Resources.resx:344,347`). Without it the text is read in the build's default encoding: ANSI on net48, UTF-8 on .NET, and
       `cda_l10n.xml` is mostly non-ASCII.
     - `GetEhrResource("CCD")` returns `CDA.xsl`, and `"CCR"` returns `ccr.xsl`. Two new names give the companions.
  2. **The child** (`EhrRenderChild`, in the program's exe).
     - `ProgramEntry.Main` begins with `if(args.Length==2 && args[0]=="/ehr-render") { Environment.Exit(EhrRenderChild.Run(args[1])); }`,
       before line 21 (today's first statement) and anything else. No window, database, `ODInitialize` or `Application` call is
       involved.
     - It calls `SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX)` first, so a crash shows no Windows error-report dialog.
       (An office that configured Windows Error Reporting dumps could still get one; the release notes say so.)
     - It reads framed documents from standard input (`Console.OpenStandardInput`, raw bytes): a kind byte (CCD or CCR), a 4-byte
       length and that many bytes of UTF-8. It decodes them as UTF-8 and reads the text with an `XmlReader` over a `StringReader`
       (`DtdProcessing.Prohibit`, no resolver), so the document's declared encoding is ignored and the child needs no code page.
     - It loads each stylesheet once per run, from an `XmlReader` with a fixed base URI (`ehr-res:///CDA.xsl`, `ehr-res:///ccr.xsl`),
       with `XsltSettings(enableDocumentFunction:true,enableScript:false)`.
     - Its `XmlResolver` serves exactly the four resources under those URIs and refuses everything else. It is passed to `Load` and to
       `Transform`, because `document()` resolves through the resolver given to `Transform`. `ccr.xsl` reads its own date tables with
       `document('')`, which resolves to its own base URI and is served.
     - Parameters: `limit-external-images=yes`, an empty `external-image-whitelist`, `limit-pdf=yes` and `useJavascript=false`
       (CDA-core-xsl's default is true). Upstream issue #41 reports that the two image parameters are not used by the current code,
       whose sanitizer drops any URL with a colon; they are set anyway, and the tests check the output, not the parameters.
     - `ccr.xsl` copies five `Description/Text` fields with `disable-output-escaping` (social history, the two result rows, and the plan
       of care's order and goal), so a received CCR's text would become markup. The CCR transform therefore writes through an output
       writer that writes such raw text as escaped text. At the tag, `CDA.xsl` uses the attribute once, on its own CSS text, so the CCD
       transform keeps the normal writer. If the owner picks the copy marked as modified (E-11), that copy also drops the five
       attributes. Either way the tests check the output.
     - It runs each transform on a thread with a 16 MB stack, writes UTF-8 HTML (`UTF8Encoding(false)`) through
       `Console.OpenStandardOutput`, framed as a status byte, a length and the bytes.
     - A watchdog thread in the child ends it after 20 seconds without progress, so a child whose parent died does not linger.
  3. **The starter** (`EhrStylesheetStarter`, in the business library).
     - It starts `ProgramNames.ExeFileName` from `AppDomain.CurrentDomain.BaseDirectory` (never `GetEntryAssembly`, which is the test
       host in tests) through a seam tests can replace, with `UseShellExecute=false` (explicit), redirected streams and `CreateNoWindow`.
       It never spells the exe's name, which the rename guard checks (`ProgramNamesTests.cs:217-240`).
     - Before starting it, it refuses a document over 32 MB or nested more than 200 levels deep (counted with an `XmlReader`, which does
       not recurse). Real CDA documents nest a few dozen levels.
     - It writes one frame and reads one reply at a time, so neither pipe can fill. It reads standard error asynchronously, capped at
       64 KB.
     - It kills the child after 15 seconds on one document. For a batch it records that document as failed and starts a new child for the
       rest.
     - A crash, a timeout or a non-zero status gives `null` and a reason. A stack overflow ends only the child. Its exit code is
       `0x800703E9` (`COR_E_STACKOVERFLOW`) or `0xC00000FD` (`STATUS_STACK_OVERFLOW`); reports show both from .NET Framework
       processes. The starter treats every non-zero exit code alike, and names these two in the reason.
     - The document never goes to disk, keeping the rule of `FormEhrSummaryOfCare.cs:138-140`.
  4. **Display.**
     - `DisplayCCD` renders a CCD or a CCR with the shipped stylesheet. Today a CCR always shows as raw text, and a CCD without an
       attached stylesheet does too; both now render.
     - `DisplayCCD` runs the size and depth check on the text before it builds its `XmlDocument`, because `InnerXml` serialises
       recursively in the program's own process. A document over the limits goes to the XML view, built from the text.
     - `FormEhrSummaryCcdEdit` gets the rendered page through a new constructor parameter, `htmlRendered`, and shows it with scripts off,
       nothing from the web and no file but its own (the existing settings). It keeps print, find and the three reconcile buttons, which
       use the XML. A CCR has no reconcile buttons, as today.
     - A stylesheet that came with an e-mail is still never read or run (`a48f7ccc6`, E-14). The note line says that the document is
       shown with the program's own stylesheet.
     - If rendering fails, the window shows today's XML view with the reason in the note.
  5. **Exports and e-mails** (E-13).
     - Every site writes or attaches `ccd.xsl` (the `CDA.xsl` text, because `EhrCCD.cs:208` points to `ccd.xsl`) plus the two companions
       under their own names, unmodified.
     - Single-document file exports also write a rendered `ccd.html`, because Chrome stops running XSLT in Chrome 158 (November 17, 2026).
     - Bulk Patient Export (`FormEhrPatientExport.cs:200-229`) writes one `<name>.html` beside each `<name>.xml`, through one batch child
       for the whole run, so `CDA.xsl` is compiled once. One set of stylesheet files serves all.
     - File exports also write the Apache-2.0 text as `stylesheets-license.txt`, unless counsel says the files' own headers are enough
       (E-11).
     - E-mails stay XML plus stylesheet files, now with the companions.
  6. **Notices.** Two `THIRD-PARTY-NOTICES.txt` entries: the files (embedded in `OpenPmsDentalBusiness.dll`), the version and tag or
     revision, the SHA-256 of each file, the copyright line and "License: Apache License 2.0" (the text at `:286`). The tag has no
     LICENSE or NOTICE file, so the CDA-core-xsl entry cites its README and the file headers. If `ccr.xsl` is the copy marked as modified
     (E-11), its entry lists the changes. Two names in the shipped-components list of `ReleaseIdentityTests.cs`. `FormLicense.cs` only if
     the lead's grep finds a per-component list.
  7. **The test that forbids XSLT** (blocking item of the review). Its security intent stays: a stylesheet that recurses without end ends
     the process that runs it, and one that loops never returns, so the program's own process runs none, and none from an e-mail runs at
     all. EHR-3 rewrites the two tests so that they allow exactly the child:
     - **Source scan.** XSLT names (`XslCompiledTransform`, `XslTransform`, `TransformXsl`) are allowed in `RdlEngine\`,
       `DocumentationBuilder\` and the one file `OpenPmsDental\Main Modules\EhrRenderChild.cs`. The scan asserts that it saw that file.
     - **Built assemblies (Mono.Cecil).** `CodeBase.dll`, `OpenPmsDentalBusiness.dll`, `WpfControlsOD.dll` and `DataConnectionBase.dll`
       reference no `System.Xml.Xsl` type, as today. In `OpenPmsDental.exe`, every method whose IL uses a `System.Xml.Xsl` type or member
       is declared in `OpenPmsDental.EhrRenderChild` or a compiler-generated type nested in it. `XslTransform` is used nowhere.
     - **One way in.** `EhrRenderChild.Run` has exactly one caller, `ProgramEntry.Main` (Cecil). The source shows the `/ehr-render`
       test as Main's first statement, ending in `Environment.Exit` before any `Application`, `RenderOptions` or `ODInitialize` call.
     - **Unchanged.** `HtmlPage` has no `TransformXsl`. `DisplayCCD` never reads `strAlterateFilPathXslCCD`. The fallback note text
       stays.
     - **The CCD window.** The constructor's parameters are `xmlCcd`, `htmlRendered`, `patCur`, `note`. `DisplayCCD` contains
       `new FormEhrSummaryCcdEdit(doc.InnerXml,htmlRendered,patCur,note)`, and the window still reads and writes no file. The starter's
       class contains no `File.`, `Path.GetTemp`, `FileStream` or `StreamWriter`.
     - The test's summary says why the child is allowed: an overflow or a loop ends or stalls only the child, which the starter kills.
- **Tests.**
  - Golden inputs:
    - upstream's `examples/CCD-full.xml`. The pinned tag has no `examples/` folder; it exists only on the default branch, added in
      October 2026. It is taken at a pinned commit (its SHA in `docs/port-notes/EHR-3.md`) and used as input only. It holds SNOMED CT and
      CPT codes, so the repository gets a copy with every code of a licensed system and its display name replaced by `TEST` values
      (section 6), marked as modified, with the source, the commit and the licence in its header. Its golden HTML is recorded from the
      pinned tag's `CDA.xsl`, not taken from upstream's `CCD-full.html`, which a newer `CDA.xsl` made;
    - three CCDs that `EhrCCD` builds from a synthetic patient, one with a non-ASCII name;
    - a synthetic CCR with dates.
  - Each is rendered through the child and compared with recorded HTML after normalising line ends.
  - Each resource's text, encoded as UTF-8, has the SHA-256 of its file (a BOM aside).
  - Hostile inputs:
    - 100,000 nested elements: refused before any child starts; the window shows the XML view with the reason;
    - a DTD or entity expansion: refused;
    - `document()` or an entity pointing to a file or http: refused;
    - a `javascript:` link and a script element in the narrative: the output has no `<script>` and no `javascript:` URL;
    - a UNC or `file:` image path: no such `src` in the output;
    - a CCR whose social history, result and plan-of-care `Description/Text` hold a script element, a `javascript:` link and a UNC
      image: the output has none of them;
    - 33 MB: refused before the child starts;
    - through the seam, a child that stalls (killed at 15 seconds) and children that exit with each of the two stack-overflow codes:
      the reason is shown, the test process is unaffected.
  - The eight export lines write non-empty files equal to the resources; bulk export writes one HTML per XML.
  - `EhrSummaryCcds.cs` joins the map of `RuntimeNameSites_UseProgramNames` (`ProgramNamesTests.cs:217-240`), with `"OpenPmsDental.exe"`
    and `"OpenDental.exe"` forbidden.
  - LibWebTests (with the rewritten `HtmlGenerationTests`; `WebView2PolicyTests` unchanged unless a pin must move), PdfLibraryTests
    (it reads `ProgramEntry.cs`, `LibraryFilesTests.cs:184-187`), S0FTests (`ExitWatchdogTests.cs:139` reads it too), AttributionTests,
    ReleaseFlowTests. The Release dry run's staged file list is unchanged, because the resources are embedded.
- **Order and size.**
  - After S1-6 has merged and the stage 1 gate has passed, and before S2-4 starts. S2-4 owns `THIRD-PARTY-NOTICES.txt`; TC-4 then owns it
    and `ReleaseIdentityTests.cs`, and S3a-8 owns `ReleaseIdentityTests.cs` in 3a wave 1.
  - If it misses that window: stage 4, after S4-1 and TC-4 have merged, and before 3a wave 1 starts.
  - The new resource files become `None` items of the SDK-style business project. The lead lists them as deliberate for stage 2's checks.
  - M, about 5-7 dev-days.

### EHR-2 Value sets

- **Goal.** `EhrCodes.Listt` is filled again, on every workstation and on the Middle Tier server, from the office's own import of the
  June 2013 EP release (section 8). The pick lists and the 2014 CQMs then work.
- **Owns.**
  - `OpenPmsDentalBusiness/Data Interface/EhrCodes.cs` (the `Listt` getter at 21-36, `UpdateList` at 39-46, `FillCache` at 185-188; a
    second class `EhrValueSetImport` with the parsers and the shipped manifest)
  - `OpenPmsDentalBusiness/Cache/Cache.cs` (an `EhrCodes` branch in `FillCache`, which starts at 417)
  - `TableTypes/EhrCode.cs` (4 and 6: a real table; `IsInDb` becomes `IsNotDbColumn`)
  - `Crud/EhrCodeCrud.cs` (regenerated with xCrudGenerator)
  - new `Misc/ForkMigrations/EhrSteps.cs` (the `EhrTables` `ForkTableSet` and one step) and, in `Misc/ForkMigrations/README.md`, the
    steps table and line 61
  - `Misc/ForkMigrations/SchemaVerifier.cs` (49: `ehrcode` leaves `ArrayHqOnlyTables`, which then lists 53 tables)
  - `tools/SyntheticData/allowed-differences.adoption.json` (one `TableAdded` rule)
  - `OpenPmsDental/Forms/FormCodeSystemsImport.cs` and `.designer.cs`
  - stale comments: `FormMedical.cs:880`, `FormEhrNotPerformedEdit.cs:50`, `FormEhrPatientSmoking.cs:109`
  - `tests/EhrTests/ValueSet*Tests.cs` with synthetic files
- **Public basis and licence.** Section 8.
- **Work.**
  1. **Table.**
     - Fork-only `ehrcode`, with the columns of `EhrCode` except `IsInDb`, keys on `ValueSetOID` and on (`CodeValue`,`CodeSystem`).
     - DDL exactly as `SHOW CREATE TABLE` prints it on MariaDB 10.11, created empty by a step at the next free dated level, with nine more
       levels kept for EHR. A table of that name with another definition stops the step with a message.
     - `ehrcode` is in the schema verifier's list of Open Dental HQ-only tables today (`SchemaVerifier.cs:49`), because the 24.3 code
       declares the type; no convert script creates it. It moves from that list to the fork-only declaration, so it is excepted once.
     - `ForkTables` and the schema verifier pick it up. Adoption leaves it empty.
  2. **Loading.**
     - `Listt` reads the table: through `Meth` on a Middle Tier client, directly elsewhere.
     - `UpdateList` reloads, then runs `updateCodeExistsHelper` as today.
     - After an import, `InvalidType.EhrCodes` reloads it everywhere. On a direct connection and on the Middle Tier server,
       `GetCacheDs` calls `UpdateList` (`Cache.cs:176-179`). A Middle Tier client runs `GetCacheDs` on the server (`:62-64`) and then
       only its own `FillCache` (`Refresh`, `:48-53`), which has no `EhrCodes` branch today, so the client would keep its old list until it
       restarts. EHR-2 adds that branch: it drops the client's list, and the next `Listt` read fetches it through `Meth`.
     - The full load is timed on the seed database with a full synthetic import. A background load comes back only if it takes over 1
       second.
  3. **Import** (section 8.2).
     - `FormCodeSystemsImport` gets an "eCQM value sets (VSAC, June 2013 EP release)" row next to CPT. Both rows need Setup (section 5.1).
     - It reads the Excel workbook (`.xlsx`) or the SVS XML (`.xml`, or a `.zip` of either).
     - It maps code systems by their HL7 OIDs, the ones the engine already uses (for example SNOMED CT `2.16.840.1.113883.6.96`,
       ICD-9-CM `.6.103`, HL7 AdministrativeGender `.5.1` to `AdministrativeSex`).
     - It takes `QDMCategory` and `MeasureIds` from the file when present, and otherwise from the manifest.
     - It checks the measure versions (section 8.2). When the file names measures, a missing measure or another version gives a
       warning, and a file with none of the nine at the right versions is refused. When it names none, its value-set OIDs are checked
       against the manifest, a file with none of them is refused, and the report says that the measure versions could not be checked.
     - It parses the whole file first. Then it replaces all rows and records the release label, file name, SHA-256 and date in a
       `ForkEhrValueSetRelease` row (`PrefC.UpsertRaw`, as in section 4.2). A failed parse leaves the old rows.
     - It reports how many of the manifest's value sets it found (about 80; the two surveys counted 79 and 81) and names the missing ones.
     - It refuses under the Middle Tier, as the CPT import does (`FormCodeSystemsImport.cs:69`).
     - A licence prompt names the office's UMLS licence and the CPT, CDT and SNOMED CT terms.
  4. **Code-system status.** The same window shows, for each code system, the rows in the database next to the minimum the code list
     expects (`EhrCodes.cs:69-79`). The office then knows which tables to load directly (owner decision of 2026-10-18, `8e230c8c3`; E-17).
  5. **First step, documentation only.** Someone with a UMLS licence opens the archived June 2013 EP package once and records, in
     `docs/port-notes/EHR-2.md`, its file names and formats, the workbook's sheet and header names and one SVS file's element names. The
     files never enter the repository (E-7). The parsers' header names are fixed from that record.
- **Tests.**
  - Synthetic workbooks and SVS files shaped like the record of step 5: `TEST-*` codes, real code-system OIDs.
  - Parse, import, reload, and the Middle Tier round trip.
  - Middle Tier mock: import, signal `InvalidType.EhrCodes`, and the client's `Listt` has the new rows.
  - A file with the versions of the 2014 EP update (VSAC profile "MU2 EP Update 2014-05-30", 2015 reporting; for example `CMS74v4`)
    gives the version warning; a file with none of the nine is refused.
  - A synthetic SVS file without `Group` elements imports, with the manifest's measure IDs and the note that the versions were not
    checked. One whose value-set OIDs are not in the manifest is refused.
  - `IsInDb` with synthetic code-table rows above the thresholds.
  - The tobacco tab and `FormEhrPatientSmoking` lists fill (`GetForValueSetOIDs` at `:218`).
  - `DiseaseDefs.cs:404` finds a pregnancy code.
  - The manifest's nine measure IDs equal what `GetEMeasureNum` and `GetEMeasureVersion` return (read by reflection, so EHR-2 does not
    edit `QualityMeasures.cs`), and its OIDs equal the OIDs the source names.
  - The step is idempotent.
  - ForkMigrationTests, M201Tests and P262SchemaTests (the HQ-only list and the documented exceptions), SyntheticData.Tests (the adoption
    rule), MiddleTierMockTests (`MiddleTierRoundTripTests.cs:1197` stops skipping `ehrcode` once `IsMissingInGeneral` goes), a run of
    `tools/DevDbTool`'s schema check (`SchemaCheck.cs:127` starts covering it too), NoHqTests.
- **Order and size.**
  - Stage 2 after EHR-3, or stage 4 after S4-1 has merged.
  - It edits no project file, and its step file is globbed.
  - **It must merge before the bridge release.**
  - M, about 5-7 dev-days.

### EHR-4a Measure Calc window

- **Goal.** The window behind "Measure Calc" (`FormEHR.designer.cs:61`), built on the open engine (section 9.1).
- **Owns.**
  - new `OpenPmsDental/Forms/FormEhrMeasureCalc.cs`, `.designer.cs`, `.resx`
  - new `FormEhrMeasureCalcDetail.cs`, `.designer.cs`, `.resx`
  - `FormEHR.cs` (526-529)
  - `EhrMeasures.cs`: `SelectAll`, `SelectAllMu2` and `SelectAllMu2Mod` (17-158), and the provider lists after 338, 1618, 2844, 3918 and
    5854
  - `tests/EhrTests/MeasureCalcTests.cs` with a fixture builder
- **Public basis.**
  - CMS Stage 1 (2014), Stage 2 and Modified Stage 2 EP specification sheets and the 2015-2017 rule (CMS works, public).
  - Open Dental's public manual for the window's behaviour.
- **Work.**
  - Section 9.1 and 9.2.
  - The engine guard: an empty provider list becomes `0`, which matches nothing, so the 107 `IN()` queries stay valid.
  - The bodies of the three `SelectAll*` methods move, unchanged, into a per-measure method that is safe on the Middle Tier, with a
    public list of the measure types of each stage. The three methods call them, so their results do not change.
  - The window computes one measure at a time on a background `ODThread` (no `Join` with a timeout). Its Cancel button sets a flag that is
    read between measures. It does not use `ProgressWin`'s cancel, which aborts the thread on net48 until S3a-1 lands
    (`FrmProgressAuto.xaml.cs:125,142,242,248`).
  - Before each refresh it drops `tempehrmeasure%` and `tempehrquality%` tables created more than one day earlier
    (`information_schema.TABLES.CREATE_TIME`). The engines create them as real tables (`EhrMeasures.cs:531-572`, `:817-853`;
    `QualityMeasures.cs:513-546`), and the schema verifier reports leftovers as extra tables (`SchemaVerifier.cs:219-222`). One day, not
    "older than this run", because another workstation may be running a refresh. The helper is public and runs on the server for a
    Middle Tier client (through `Meth`), so EHR-5 calls it too.
  - `EhrMeasures.Update` and the `ehrmeasure` table's Numerator and Denominator columns stay unused (E-19).
- **Tests.**
  - A provider with no key row: no SQL error, denominator 0.
  - The rule of section 9.2 for each stage, measure and year.
  - The per-measure method gives the same list as each `SelectAll*` on a small fixture.
  - Cancel between measures leaves no work table.
  - The work-table helper through the Middle Tier mock.
  - Source checks: no WinForms menu and no `Process.Start`.
  - The patient-pick round trip (`SelectedPatNum` to `EhrFormResultOnClose=PatientSelect`).
- **Order and size.**
  - After S1-5 (new forms need the globbed program project) and after EHR-1 (the provider list). Best in stage 2 or stage 4, outside the
    stage 1 baseline diff.
  - M, about 4-6 dev-days.

### EHR-4b Measure fixtures

- **Goal.** Numbers that a test has checked against the public specifications (section 9.3).
- **Owns.** `tests/EhrTests/MeasureSpecTests.cs`, `tests/EhrTests/Fixtures/`, and the known-deviation list in `docs/port-notes/EHR-4.md`.
  It edits no engine code.
- **Work.**
  - For each stage, at least one patient who meets and one who misses each measure type, with numerator and denominator taken from the
    specification sheet's definitions (for example, Stage 2 e-prescribing: 4 prescriptions, 3 electronic, gives 75%).
  - Fixture codes follow the data rule of section 6.
  - A case where the engine disagrees with the sheet becomes Inconclusive with a deviation ID and a line in the list. Engine fixes are
    follow-ups that the lead assigns.
  - A recorded run of `SelectAll`, `SelectAllMu2` and `SelectAllMu2Mod` on the fixture as golden numbers.
- **Order and size.** After EHR-4a. Stage 4 or later; it changes no schema and may land after the bridge release. M, about 4-6 dev-days.

### EHR-5 CQM reports and QRDA

- **Goal.** The 2014 quality-measure window and its QRDA I and III export are reachable and tested, and what they write names this
  program and the office, never Open Dental.
- **Owns.**
  - `FormEhrQualityMeasures.cs` (28-45)
  - `FormEhrQualityMeasures2014.cs` (30-51, plus a caption line; the QRDA button at 135-204, for the legacy-root warning)
  - `FormEHR.cs` (624-628: `ProvKeyIsValid` deleted)
  - `Db Multi Table/QualityMeasures.cs`: 5862-5870 and 5927 (QRDA III author and comment), 7205-7216 and 7273 (QRDA I author and
    comment), 7845-7865 (the QRDA I zips), the folder where QRDA III is written (5821); nothing else unless a test finds a fault
  - `tests/EhrTests/CqmTests.cs` and `QrdaTests.cs`
- **Public basis.**
  - The 2014 eCQM specifications of the nine measures (`QualityMeasures.cs:5371-5514`), June 2013 versions (section 8.1).
  - HL7 QRDA Category I and III implementation guides (template IDs only).
  - Licence: E-10 (steward terms) and section 8.
- **Work.**
  - The provider lists use the rule of section 5, and the message changes.
  - **Identity in QRDA** (E-15). Today both documents write Open Dental's registered OID arc as the author's ID root (`:5866`, `:7209`),
    although `OIDInternals.cs:15-18` says never to issue identifiers under it. QRDA I also writes Open Dental's street address and phone
    (`:7210-7211`), and both name "Open Dental" (`:5868`, `:7213-7214`, and the comments written into the output at `:5927`, `:7273`).
    - The author's ID becomes the office's own root (`_strOIDInternalRoot`, which the export already requires, `:5778-5781`) with the
      extension `OpenPmsDental`, and no "HL7 OID Registry" authority name.
    - An office's root may itself have been issued under Open Dental's arc (`LegacyOidArc+".1486.<n>"`, `OIDInternals.cs:15-18`). Such
      a root is never replaced automatically (`IsLegacyRoot`, `:80-84`), and the custodian, authenticator and provider IDs are written
      under it (`QualityMeasures.cs:5882`, `:5897`, `:7225`). When `OIDInternals.IsLegacyRoot(_strOIDInternalRoot)`, the export warns
      that the identifiers carry a root issued under Open Dental's arc and offers the OID window, which already flags such a root
      (`FormOIDRegistryInternal.cs:31`).
    - The address and phone become the practice's, from the same preferences the custodian uses.
    - The software name becomes "Open PMS - Dental" with the version. The two output comments drop the vendor name.
  - **Encoding.** QRDA I documents declare UTF-8, but the zips store them in the default code page (`ODZip.GetDefaultEncodingBytes`,
    `:7860-7861`). They are stored as UTF-8 without a BOM.
  - **Stylesheets.** QRDA I points to `cda.xsl` (`:7029`) and QRDA III to `qrda.xsl` (`:5823`); neither ever shipped. CDA-core-xsl renders
    any CDA R2 document, so EHR-5 writes it under both names, with the companions, beside the files and into each zip. No HTML (E-13).
  - A caption says the report is for internal review and the program is not certified (E-1).
  - The 2011 window gets the same gate. It stays without a button, as in Open Dental: `FormEHR.designer.cs` has no `butQuality`, and
    `FormEHR.cs:541-545` is a dead handler. It calls EHR-4a's work-table helper before a run.
- **Tests.**
  - With synthetic value sets and patients: CMS74v3 and CMS75v2 (the dental measures, CMS-stewarded) checked against their June 2013
    definitions; all nine recorded as golden numbers.
  - QRDA I and III written and checked structurally: template IDs, and III's aggregate counts equal the report.
  - No generated QRDA I or III has an `id` root equal to `OIDInternals.LegacyOidArc`, and every identifier under that arc starts with
    the office's own root. None contains Open Dental's street address, phone number or name. One fixture has a synthetic root, and one
    a legacy root (the warning case).
  - A non-ASCII patient name reads back correctly from a QRDA I zip.
  - `InvariantCulture` in QRDA numbers.
- **Order and size.**
  - After EHR-2 and counsel's answer to E-10. Stage 4, or 3a wave 1. It changes no schema, so it may also land on `main` after the bridge
    release.
  - If S3a-3's grep for machine-read numbers or default encodings lists `QualityMeasures.cs`, EHR-5 merges first.
  - S-M, about 3-4 dev-days.

---

## 8. Value sets: the release, the file and licence handling

### 8.1 The release

- **What the engine reports.** The nine 2014 eCQM versions hard-coded in `QualityMeasures.cs:5371-5514` (`GetEMeasureNum`,
  `GetEMeasureVersion`): CMS68v3, CMS69v2, CMS74v3, CMS75v2, CMS127v2, CMS138v2, CMS147v2, CMS155v2 and CMS165v2. The `EhrCode.MeasureIds`
  example "69v2,147v2" agrees.
- **Which specification that is.** These are the June 2013 versions of the EP eCQMs: CMS's "2013 June eCQM Specifications for Eligible
  Professionals", for the 2014 reporting period (eCQI archive). CMS said the June 2013 versions had to be used for the 2014 EHR Incentive
  Program (CMS's June 2013 eCQM logic guidance) (summary). Open Dental's 2014-Edition certification of version 14.3 (September 30, 2014)
  falls in that period (summary).
- **Which VSAC release matches.** VSAC's "June 2013 EP Only Release eCQM Value Sets", publication date June 14, 2013, expansion profile
  "MU2 EP Update 2013-06-14" (VSAC's expansion-profile list, section 18). It is listed among the archived releases on VSAC's Downloadable
  Resources page (`https://vsac.nlm.nih.gov/download/ecqm`), which needs a UMLS sign-in for the files (summary).
- **Not the right release.** The first draft cited VSAC's note of May 6, 2015. That note is the annual update of the 2014 eCQMs for 2016
  reporting, which uses later measure versions. The 2014 EP update (VSAC profile "MU2 EP Update 2014-05-30", for 2015 reporting) also
  uses later versions, for example `CMS74v4`. Neither matches the engine. ("MU2 EH Update 2014-04-01" is the hospital update.)

### 8.2 The file the office downloads, and how it is read

- **The download.** From the June 2013 EP release on VSAC's Downloadable Resources page, the package sorted by CMS ID: the Excel workbook
  (`.xlsx`), or the XML (SVS) package where the page offers it. VSAC's help page says eCQM packages come in Excel and XML, sorted by CMS
  ID, value set name or QDM category (summary). Which of the two the 2013 package has is recorded in EHR-2's first step.
- **Why Excel first.** `EhrCode`'s fields (`MeasureIds`, `ValueSetName`, `ValueSetOID`, `QDMCategory`, `CodeValue`, `Description`,
  `CodeSystem`, `CodeSystemOID`) match the columns of VSAC's workbooks of that time, with the CMS IDs of the measures that use each code.
  The review also found the June 2013 package named as an `.xlsx` file in an ONC tracker entry (not re-checked).
- **The Excel reader.** It opens the workbook with `ZipArchive` (the business library already references `System.IO.Compression`) and
  reads the sheet XML and shared strings, so no new library is needed.
  - It reads every sheet, finds the header row and matches columns by header text (ignoring case and spaces), with the names recorded in
    EHR-2's first step.
  - A sheet without the required columns is refused, and the message names the missing columns.
  - Rows without an OID or a code are skipped and counted.
- **The SVS reader.** `DescribedValueSet` elements (ID, display name, version) with their `ConceptList/Concept` entries (code, code
  system, its name and version, display name), and `Group` elements for the QDM category and the measure IDs when present (VSAC's SVS
  API page).
- **The version check.** When the file names measures, its CMS IDs are compared with the nine of section 8.1. A missing measure or another
  version (for example `CMS74v4`) gives a warning naming it; a file with none of the nine at the right versions is refused. When the file
  names no measure (for example an SVS file without `Group` elements), its value-set OIDs are checked against the manifest. A file with
  none of them is refused, and the report says that the measure versions could not be checked.
- **If the archived package is ever withdrawn.** VSAC's SVS API lists "MU2 EP Update 2013-06-14" among its expansion profiles (VSAC's
  expansion-profile list, section 18).
  An office could then fetch each manifest OID with its own UMLS API key. That route is not in EHR-2 (E-7).

### 8.3 What ships and why

| What | Ships in the repository? | Basis |
|---|---|---|
| Value-set OIDs, names, QDM categories, measure IDs (the manifest) | Yes. The OIDs are in the source today; the names and categories come from the CMS eCQM specifications, whose measures NCQA and others steward (E-8, E-10) | CMS eCQM specifications. OIDs and names are facts in the public specifications |
| Value-set members (CPT, CDT, ICD-9/10, LOINC, RxNorm, SNOMED CT, CVX, CDCREC, HCPCS, SOP) | **No** | VSAC content falls under the UMLS licence. §3: no distribution of the Metathesaurus "or subsets of it". §1: not transferable. §12: CPT and CDT are level 3, internal use |
| Code lists already in the engine's SQL (SNOMED CT lab-result codes, `EhrMeasures.cs:14`; two CDT codes in the Demographics measure; RxNorm and CPT lists in `QualityMeasures.cs:1408`, `:1539`) | Already shipped in v0.0; nothing is added | Counsel E-8 and E-9 |
| An importer for the office's VSAC download | Yes (EHR-2) | The model of the CPT import, which reads the office's AMA file |
| Test data | Synthetic `TEST-*` codes, codes already in the source, CDCREC, CVX and HCPCS Level II (section 6) | none needed |
| Code-system tables | No. The office loads them directly (owner, 2026-10-18) | SNOMED CT: each user needs their own affiliate licence, and a vendor's licence does not cover its customers. CPT: AMA. CDT: the ADA requires a licence for every use. LOINC is redistributable with a notice. CVX, CDCREC and HCPCS come from CDC and CMS. ICD-10-CM's WHO notice and SOP's terms go to counsel |

### 8.4 What each office does

- It holds its own UMLS licence and sends NLM the yearly usage report.
- It downloads the June 2013 EP release's package (section 8.2).
- It imports it in Setup > Chart > EHR > Code System Importer (Setup permission).
- It loads its code-system tables.

---

## 9. The measures window and the CQM reports

### 9.1 The Measure Calc window (EHR-4a)

- **Top of the window.**
  - A provider list: one entry per last and first name among providers with a key row. The engine already groups by name
    (`EhrMeasures.cs:333-338`).
  - Start and end dates. The default is this calendar year, as `FormEhrQualityMeasures2014.cs:58-59` does.
  - A stage choice. The default follows `FormEHR.cs:85-110`: `provider.EhrMuStage` 1, 2 or 3 gives Stage 1, Stage 2 or Modified Stage 2
    (`SelectAll`, `SelectAllMu2`, `SelectAllMu2Mod`). 0 uses `PrefName.MeaningfulUseTwo` (0, 1 or 2).
  - Refresh, Cancel, Print, Close.
- **Grid.**
  - Columns: measure (`MeasureTypeTitleHelper` for Modified Stage 2, as `FormEHR.cs:115-118` does), numerator, denominator, percent,
    requirement, exclusion count.
  - A percent row is green (178,255,178, the colour `FillGridMu` uses) when the denominator is above 0 and the numerator times 100 is more
    than the threshold times the denominator. The field is "More than this percent", `EhrMeasure.cs`.
  - Rows with another kind of requirement follow section 9.2.
  - A value of −1 shows as "not calculated".
- **Detail (double-click a row).**
  - Objective, Measure, the three explanation texts, and the exclusion count with its description.
  - The audit grid from `GetTable`, `GetTableMu2` or `GetTableMu2Mod` (patient, explanation, Met "X"; `EhrMeasures.cs:1075-1078`).
  - Double-clicking a patient returns them, as `FormEhrQualityMeasureEdit2014.cs:141-143` does. The EHR window then closes with
    `PatientSelect`, and the Chart switches (`ControlChart.cs:7865`, unchanged).
- **Print.** The grid, through `GridOD.PrintPage` (`GridOD.cs:973`), with provider, dates, stage and the "internal review" line.

### 9.2 Requirements that depend on the year, and measures this build cannot meet

- **Modified Stage 2 by year.** `SelectAllMu2Mod` takes its thresholds from `GetThresholdMu2` (`EhrMeasures.cs:139`, `:2782-2830`),
  which gives 5% for `ElectronicCopy` (patient electronic access, measure 2: view, download or transmit) and for `SecureMessaging`. The
  2015-2017 rule set other requirements for 2015 and 2016:

| Measure (Modified Stage 2) | 2015 | 2016 | 2017 |
|---|---|---|---|
| Patient electronic access, measure 2 (`ElectronicCopy`) | At least 1 patient | At least 1 patient | More than 5% |
| Secure electronic messaging (`SecureMessaging`) | Yes/no: the capability was enabled | At least 1 patient | More than 5% |

- The window applies this rule itself, by the year of the reporting period, so the engine's output does not change. A period that spans
  two calendar years gets a warning, because Modified Stage 2 periods lie within one year. A yes/no row shows "attestation, not
  calculated".
- The alternate exclusions for providers who were scheduled for Stage 1 in 2015 are not modelled. The window says so, and E-16 records it.
- **Measures this build cannot meet.** The numerators of those two measures come from the patient portal and web mail
  (`EhrMeasures.cs:4685`, `:4931`, `:5607`, `:6257-6271`). The portal was removed in `2c62a2490`. The window marks these rows "depends on
  the removed patient portal" instead of a red 0%, and the release notes say so.

### 9.3 Fixtures and known deviations (EHR-4b)

- The engine has no test today, and its SQL already contains real codes (section 8.3). The fixture rule of section 6 lets those branches
  be tested without adding any code to the repository: the tests read those codes from the engine's strings at run time.
- A spec case that disagrees with the engine is recorded, not fixed in EHR-4b: Inconclusive with an ID, and a line in the deviation list.

### 9.4 The CQM reports (EHR-5)

- They work once value sets are imported (EHR-2) and providers are marked (section 5).
- The engine implements the June 2013 versions. Moving to today's versions (CMS74v15, CMS75v14, QDM 5.6) is new work and not in this plan
  (E-16).

### 9.5 Status of the programs (why the label)

- Open Dental was certified for the 2011 and 2014 Editions until December 2018. It built no 2015 Edition.
- The Medicaid Promoting Interoperability program ended on December 31, 2021.
- Medicare eligible professionals moved to MIPS. MIPS Promoting Interoperability needs certified technology, and eCQM submission does too.
- This program is not certified. Its numbers are for internal review only (E-1).

---

## 10. Sequencing against the .NET 10 stages and the chart track

### 10.1 Windows

| Stage window | Stage and chart packages | EHR | Why it fits |
|---|---|---|---|
| Now (S1-0 merged at `4871b4959`): stage 1, first wave | S1-T, S1-1, S1-2; TC-0 merges | **EHR-0** written, then merged | EHR-0 owns only `.cs` and `.resx` content plus its new test project. While TC-0 is open, S1-T, S1-1 and TC-0 hold the three writer slots, and S1-2 waits (chart plan, section 2). EHR-0 takes the slot TC-0 frees when it merges, so S1-T and S1-1 never wait for EHR. If TC-0 is late, EHR-0 waits. S1-2 takes the first slot that S1-T, S1-1 or EHR-0 frees |
| Stage 1, second wave | S1-3, S1-4; TC-1 | **EHR-1** written, then merged | S1-3, S1-4 and TC-1 fill the three slots. If EHR-0 is still open then, it keeps its slot until it merges, and the last of the three to be ready waits. EHR-1 is written when the first of them merges, and it merges before S1-6 starts, because it adds items to the classic EhrTests project. It edits no other project file, and none of its files belongs to a stage 1 package |
| Stage 1, later | S1-5, S1-6; TC-2, TC-3 | EHR-1 merges if it has not; then EHR-3 written; nothing merges while S1-6 runs | S1-5 and S1-6 run one after the other, and so do TC-2 and TC-3, so one slot stays free. EHR-3's writing starts after EHR-1 merges (one EHR writer). EHR-3 edits tests, and no EHR test edit merges while S1-6 runs |
| After the stage 1 gate, before S2-4 starts | S2-1 to S2-3 | **EHR-3** merges | Notices and AttributionTests have no owner until S2-4. `ProgramEntry.cs` has none until S3b-4 |
| Stage 2 | S2-4; TC-3, TC-4 | **EHR-2**, then **EHR-4a** | EHR-2 and EHR-4a edit no project file (globbed files). S2-4 and TC-4 own the notices |
| Stage 4 | S4-1, S4-2; TC-5, TC-7 | Whatever is left, after S4-1 merges; **EHR-4b**; **EHR-5** after E-10; EHR-3 if it missed its window (after TC-4) | S4-1 owns every test file that fails to compile |
| 3a, wave 1 | S3a-1 to S3a-11 | EHR-5 if not done (before S3a-3 if it claims `QualityMeasures.cs`) | `FormInfobutton.cs:1117` is S3a-2's; EHR does not touch it. S3a-8 owns `ReleaseIdentityTests.cs`; EHR-5 does not touch it |
| 3a, wave 2 | S3a-M1 to M3 | none | `FormMedical.cs` and `FormEmailMessageEdit.cs` use WinForms menus and belong to M3. `EmailPreviewControl.*` belongs to M2. EHR edits to them (one EHR-2 comment) are done before |
| 3a gate, bridge release | – | **EHR-2 required.** EHR-0, EHR-1, EHR-3 and EHR-4a planned; EHR-4b and EHR-5 if ready | Rollback rule (section 6). EHR-4b and EHR-5 change no schema and may land later, on net10 |
| 3b | S3b-0 to S3b-7 | none | S3b-3 retargets the business library; `XslCompiledTransform` exists on .NET 10. S3b-4 keeps the `/ehr-render` branch first in `Main`; it creates no window, so `SetDefaultFont` and the code-page registration line come after it, and the child needs no code page (it reads UTF-8 only). S3b-5 runs `tests/EhrTests` on x64 and re-records a golden file only with a reason |

### 10.2 Shared files and their order

| File | EHR packages | Other owners | Order |
|---|---|---|---|
| `OpenPmsDental/Forms/FormEHR.cs` | EHR-1 (29-30, 45-56, 571-575, 630-634), EHR-4a (526-529), EHR-5 (624-628) | none | EHR-1, EHR-4a, EHR-5 |
| `Main Modules/FormOpenDentalThreads.cs` | EHR-0 | none (S3a-1 owns `FormOpenDental.cs:6840`, not this file) | EHR-0 only |
| `Misc/WindowsTime.cs`, `Misc/NTPv4.cs` | EHR-0 | S3a-12 reviews every `DllImport`; ReleaseFlowTests pins their Event Log source | EHR-0 before 3a |
| `Main Modules/ControlChart.cs:28-30` | none | TC-2 (it edits line 27), then S3a-1 and S3a-M2 | The lead adds the 3-line deletion to TC-2's brief, or to S3a-M2's if TC-2 has merged |
| `FormOpenDental.cs:1727,1759` | none (read only) | TC-2, S3a-M2 | not edited |
| `NoHqTests/NoHqTests.cs` | EHR-0 (155-162) | S1-5 converts the project | EHR-0 before S1-5 starts |
| `tests/EhrTests/*.csproj` | EHR-0 creates it; EHR-1 adds items while it is classic | S1-6 (conversion), S4-1 | No EHR test edit while S1-6 or S4-1 runs |
| `tests/LibWebTests/HtmlGenerationTests.cs` | EHR-3 | S1-6 (the project file only), S4-1 (if it fails to compile) | EHR-3 after S1-6, not while S4-1 runs |
| `tests/LibWebTests/WebView2PolicyTests.cs` | EHR-3 runs it; edits only if a pin of `FormEhrSummaryCcdEdit` must change (255, 273) | TC-2 (275) | not at the same time as TC-2 |
| `THIRD-PARTY-NOTICES.txt` | EHR-3 | S2-4, TC-4, S3a-13, TC-9b, S3b-7 | EHR-3 before S2-4, or after TC-4 and before 3a wave 1 |
| `docs/third-party-binaries-audit.md` | EHR-0 (133) | TC-4, TC-9b | EHR-0 in stage 1, before TC-4 |
| `tests/AttributionTests/ReleaseIdentityTests.cs` | EHR-3 (18-22) | TC-4, TC-9b, S3a-8 (wave 1) | EHR-3 before TC-4, or after TC-4 and before 3a wave 1 |
| `OpenPmsDentalBusiness/Properties/Resources.resx` | EHR-3 | none (TC-9b edits the program's own `Resources.resx`) | EHR-3 only |
| `Main Modules/ProgramEntry.cs` | EHR-3 (20-21) | S3b-4; PdfLibraryTests (`LibraryFilesTests.cs:185`), S0FTests (`ExitWatchdogTests.cs:139`) and ReleaseFlowTests (`ProgramNamesTests.cs:223`) read it | EHR-3 before 3b |
| `tests/ReleaseFlowTests/ProgramNamesTests.cs` | EHR-3 (one entry in the map at 217-240) | S3a-7 (183-196), S4-1 (if it fails to compile) | EHR-3 before 3a wave 1, not while S4-1 runs |
| `FormMedical.cs` | EHR-2 (880, a comment) | S3a-M3 | EHR-2 before 3a wave 2 |
| `Misc/ForkMigrations/SchemaVerifier.cs`, `README.md` | EHR-2 | none | EHR-2 only |
| `OpenPmsDentalBusiness/Cache/Cache.cs` | EHR-2 (`FillCache`) | none | EHR-2 only |
| `Db Multi Table/QualityMeasures.cs` | EHR-5 | possibly S3a-3 | EHR-5 first |
| `Data Interface/EhrMeasures.cs` | EHR-4a | none | EHR-4a only |
| `User Controls/Preferences/UserControlChartGeneral.cs` | none (the radiology follow-up, section 15) | TC-5 | the follow-up after TC-5 |
| `tools/SyntheticData/allowed-differences.adoption.json` | EHR-2 | S1-1 converts its project only | any time |

---

## 11. Launch check list for the lead (screenshots, on a test database)

1. **Show Features.** Turn EHR on: the confirmation, then the restart message. After the restart, the Chart has its EHR button. Turn it
   off again. Do the same in the Setup Wizard, with the same confirmation, and check that the wizard's text makes no certification claim.
2. **EHR window.** Open it for a patient. The label reads "(not marked for EHR reporting)".
3. **Provider keys and permissions.**
   - As a user without Setup: Provider Keys and Code System Importer are hidden in the EHR setup window; Emergency Now is there.
   - As a user with Setup: add a row with no key text for an existing provider; it saves and the security log has the entry.
   - A name no provider has: refused, with the reason.
   - Log on as that provider's user, enter a procedure, and check that the procedure is marked CPOE (`Userods.IsUserCpoe`,
     `FormProcEdit.cs:2664`, `Procedures.cs:4435`).
   - The SNOMED tools show only with Setup and SNOMED rows; Map to ICD9 refuses without Problem Definition Edit.
4. **Time check.**
   - Off: no TimeSync thread in the log.
   - Warn, on the database server's PC with the clock moved 30 seconds: one alert, seen by a user with the default alert categories.
   - Opening the manual window does not change the clock.
   - With Windows Time disabled, the set button as a standard user: the message with the reason.
   - Elevated, with Windows Time disabled: the clock is corrected, with Event Log and security log entries.
   - With Windows Time running: the resync advice only.
   - With Windows Time stopped but not disabled: the advice to start it and resync, only.
5. **Value sets.**
   - Import a synthetic workbook and a synthetic SVS file: the found and missing report and the release label.
   - A synthetic file with the 2014 EP update's versions (for example `CMS74v4`): the version warning.
   - With synthetic SNOMED rows, the Tobacco Use tab and `FormEhrPatientSmoking` open with lists.
   - Code-system status rows are shown.
6. **CCD.**
   - Chart > CCD export: the folder has `ccd.xml`, `ccd.xsl`, the two companions, `ccd.html` and the licence text (E-11). `ccd.html` opens
     readable in a browser, with no script.
   - Patient Export of three patients: three XML files, three HTML files, one set of stylesheet files.
   - A received CCD in e-mail shows rendered. One with its own `.xsl` shows rendered, with the note.
   - A CCR with dates shows rendered, with readable dates.
   - A received CCR whose description texts hold markup shows that markup as text, with no link or image from it.
   - A patient name with accents shows correctly.
   - The deep-nesting CCD gives the XML view with the reason, and the program stays up. No window flashes while documents render.
7. **Measure Calc.** Provider, dates, stage, Refresh. Green rows. A Modified Stage 2 period in 2016 shows "at least 1 patient" rows. The
   portal rows are marked. Cancel during a refresh, then check that no `tempehrmeasure` table is left. The detail and its audit.
   Double-clicking an audit patient switches the Chart to that patient. Print preview.
8. **Quality Meas.** Providers with rows are listed. Run the report and the QRDA export, then open the QRDA files: the software name is
   Open PMS - Dental, the address is the practice's, and no Open Dental identifier is present except the office's own root. On a database
   whose root was issued under Open Dental's arc, the export first shows the warning.
9. **Release dry run.** The staged file list is unchanged by every EHR package.
10. **Direct.** With EHR on, the e-mail window offers Direct sending. The address's certificate is imported from a generated test `.pfx`
    (never a real one) with the Certificate button of Edit Email Address, as the launch checks of `docs/port-notes/DIRECT-CERT.md` say.

---

## 12. Risks and mitigations

- **Turning EHR on changes everyday work** (Rx rules, CPOE, race picker, required fields).
  - It stays off by default. The confirmation names the changes, and the release notes list them.
- **A key row turns on CPOE for that provider's user** (9 places).
  - Section 5, the Setup permission and launch check 3. The release notes say so.
- **Measure numbers that no test has ever checked** (no test covers `EhrMeasures`, `QualityMeasures` or `EhrCCD` today).
  - Spec-derived and golden tests on synthetic fixtures, with a known-deviation list (EHR-4b, EHR-5).
  - The "internal review" label.
- **Licensed codes leaking into the repository.**
  - The fixture rule and a test that enforces it.
  - The import reads only the office's file. Counsel E-8 and E-9.
- **Measure steward terms.** NCQA permits internal non-commercial use by providers but asks vendors who embed its measures for a licence.
  - The engine was already distributed in v0.0. EHR-5 waits for counsel (E-10).
- **The wrong value-set release.** A later release fills the lists with codes of later measure versions.
  - Section 8.1 names the release, and the importer checks the measure versions.
- **The archived release is withdrawn, or its files differ from what the parsers expect.**
  - EHR-2's first step records the real files. The API route of section 8.2 remains as a later option.
- **XSLT that ends or hangs the program.**
  - Only shipped stylesheets run, and only in a child with limits and a closed resolver. The rewritten test keeps XSLT out of the
    program's process.
  - Received stylesheets never run. The child's failure falls back to the XML view.
- **Stylesheet weaknesses** (the older HL7 `CDA.xsl` had XSS flaws, CVE-2014-3861 and CVE-2014-5452; upstream issue #41 says two image
  parameters are unused).
  - The latest release with its security parameters and `useJavascript=false`.
  - Scripts off, nothing from the web, and tests on the output.
  - A bump procedure: new tag, golden diff, notices.
- **The AAFP stylesheet writes five text fields of the received document as markup** (`disable-output-escaping`).
  - The CCR transform's escaping writer, and a hostile CCR test on the output (EHR-3).
- **The AAFP stylesheet's date rendering.** A later fork of it reports that the original shows ISO-8601 dates wrongly (summary).
  - The golden CCR with dates shows it. E-11 lets the owner choose an unmodified copy with the defect or a copy marked as modified.
- **Encodings.** Resources read as ANSI on net48, and code pages on pipes.
  - `;utf-8` on each resource with a hash test; UTF-8 bytes on both pipes; a non-ASCII golden.
- **Recipients' browsers stop running XSLT** (Chrome 158).
  - A rendered HTML beside file exports (E-13).
- **The database server's clock is moved through the network.**
  - The program never sets it by itself. Replies are checked, and the manual set needs a person with Setup, a confirmation and a cap.
- **Clock changes fighting Windows Time, or failing for lack of rights.**
  - No automatic setting. The manual button gives way to Windows Time, and every failure says why.
- **The rollback installer cannot open the database.**
  - EHR-2's step merges before the bridge release.
- **Work tables left behind by a cancelled measure run.**
  - Cancel only between measures, and drop day-old leftovers before each run.
- **Collisions with stage and chart packages.**
  - The tables of section 10 and one EHR writer at a time.
  - No project-file edits except EhrTests', and stage 3a's scans kept in advance.

---

## 13. Owner and counsel questions

Questions marked "counsel" need counsel's answer; the others are the owner's. Each has a recommendation.

**Answered 2026-11-08.** The owner: "E-1 to E-19 as recommended". So every owner question is decided as its recommendation
says, download batch E (section 14) is approved, and the counsel items follow their recommendations: the work goes ahead, the
code lists already in the engine's SQL stay until counsel answers E-8 and E-9, nothing licensed is shipped, EHR-5 merges after
counsel answers E-10, and the answers to E-6, E-8, E-9 and E-11 are applied when they come, as each recommendation says.
E-7 needs one step from the office: someone with a UMLS licence opens VSAC's June 2013 EP release once and records its files and
headers (EHR-2's first step; nothing from it enters the repository).

| # | Question | Recommendation |
|---|---|---|
| **E-1** (owner) | Should the measures window and CQM reports say "internal review only; this program is not certified; not for attestation or submission"? | Yes |
| **E-2** (owner) | One confirmation when turning EHR on, the same in Show Features and the Setup Wizard, and no password? | Yes |
| **E-3** (owner) | TimeSync modes Off (default) and Warn only. No automatic clock setting: Windows Time keeps the clock, and the manual window sets it only on a button with checks (section 4). No migration for databases that have EHR on. The alternative, an automatic "Set the clock" mode with the reply rules of 4.3 and a 1-hour cap, is not recommended: NTP is not authenticated, the program would need the clock right, and it would fight Windows Time | Yes. Recommend Windows Time plus Warn in the docs |
| **E-4** (owner) | "Reports EHR" means an `ehrprovkey` row for the provider's names (key text optional), not a new provider check box (a schema change on an Open Dental table)? And the permissions of section 5.1: Setup for key rows, the value-set and CPT imports and the SNOMED tools; Problem Definition Edit for the map; the measure windows open as in Open Dental? | Yes to both |
| **E-5** (owner) | Quarterly keys: delete the stub with no caller and keep the table unread, with no window? | Yes |
| **E-6** (owner, counsel) | Show SNOMED's "Map to ICD9" to users with Setup whenever the `snomed` table has rows, without a member-country check, since each office licenses SNOMED itself? | Yes. Counsel confirms that the affiliate licence is the office's matter |
| **E-7** (owner) | Value sets by file import of the June 2013 EP release only (Excel workbook or SVS XML), with no API for now? Can someone with a UMLS licence open the archived package once and record its files and headers (nothing enters the repository)? | Yes to both. The API with the "MU2 EP Update 2013-06-14" profile is a later option |
| **E-8** (counsel) | Is shipping only the manifest (OIDs; names and QDM categories taken from specifications whose measures NCQA, the AMA-PCPI and CMS steward; measure IDs) plus an importer outside UMLS §3, §4 and §5? Is the office's import its own internal use under §12? May the code lists already in the engine's SQL (SNOMED CT lab results, RxNorm and CPT lists) stay? Are ICD-10-CM codes (WHO notice) and SOP codes free for test fixtures? | Expected yes. Ship no members. The inherited lists stay until counsel answers (v0.0 already distributed them), and move to the office's import if counsel says so |
| **E-9** (counsel) | Do CDT codes inside value sets fall under the ADA's terms even without descriptors? Do the two CDT code numbers in the Demographics measure's SQL? | Import only, never ship descriptors. Counsel says whether the two code numbers may stay |
| **E-10** (counsel) | Steward terms of the 2014 CQMs whose logic is in `QualityMeasures.cs` (NCQA and AMA-PCPI measures; CMS74 and CMS75 are CMS's), and of the manifest's names and categories. Does the open distribution need a licence? May the window be reopened? | Keep the code. Counsel answers before the release that contains EHR-5 |
| **E-11** (owner, counsel) | Ship HL7 CDA-core-xsl v4.1.0-beta.2 (May 2024, the latest release; iframes sandboxed and PDF off by default; every release since 4.0.2-beta8 carries the same security notice) and the AAFP `ccr.xsl` at a pinned revision, unmodified, under Apache-2.0 within the GPL v3 distribution, with notices? Write the Apache-2.0 text beside file exports? If the AAFP dates render wrongly: ship unmodified with the defect, or a copy marked as modified? A second reason for that copy: it can also drop the five `disable-output-escaping` attributes (EHR-3 step 2; the child's escaping writer covers them either way). Keep in the tests a copy of upstream's `CCD-full.xml` with its licensed codes replaced, marked as modified? Approve download batch E (section 14)? | Yes; yes, unless counsel says the files' headers are enough; a copy marked as modified, for the dates and the five attributes, with the changes listed in the notices; yes; yes |
| **E-12** (owner) | Run XSLT in a child process of the program's own exe (`/ehr-render`), with the LIB-WEB test rewritten to allow XSLT only there? Or in a new small exe (keeps "no XSLT in the program's assemblies" literally, one more release file), or replace XSLT with a C# renderer (different from what recipients see)? The first changes the rule of `a48f7ccc6` from "no XSLT in the program's assemblies" to "no XSLT in the program's process, and no received stylesheet anywhere" | The program's own exe |
| **E-13** (owner) | File exports also write a rendered HTML: `ccd.html` for one document, one per patient for Patient Export (one batch child per run)? E-mails stay XML plus stylesheet files? QRDA files get the stylesheet files but no HTML? | Yes. The smaller alternative for Patient Export is no HTML |
| **E-14** (owner) | A stylesheet that arrives with an e-mail still never runs, and received documents show with the program's own stylesheet? | Yes (`a48f7ccc6` stands) |
| **E-15** (owner) | Outgoing documents carry Open Dental's identity. QRDA: the author's ID root (Open Dental's OID arc), address, phone, names and output comments (`QualityMeasures.cs:5866-5868`, `:5927`, `:7209-7214`, `:7273`). HL7: MSH-3 (`EhrVXU.cs:85`, `EhrADT_A01.cs:100`, `EhrORU.cs:46`) and the PID-3.4 assigning authority (`EhrVXU.cs:562,575`, `EhrADT_A01.cs:281,294`). Change them to this program and the office? | QRDA now (EHR-5): the office's root with the extension `OpenPmsDental`, the practice's address and phone, "Open PMS - Dental". A legacy office root (issued under Open Dental's arc) stays until the office changes it in the OID window; the export warns. HL7 in a small follow-up, unless an office has a live registry interface that knows the old names |
| **E-16** (owner) | Leave out current eCQM versions (CMS74v15, CMS75v14), Stage 3 or MIPS PI, C-CDA Companion Guide R4.1 with USCDI v3, and the alternate exclusions of 2015 for Stage 1 providers? None counts without certification | Leave them out. A separate plan if wanted |
| **E-17** (owner) | Code-system tables stay "loaded directly" with EHR-2's status view? Other EHR gaps not from EHR.dll go to the follow-ups of section 15? | Yes. Plan the follow-ups after EHR-5 |
| **E-18** (owner) | The radiology-orders alert: a key row turns on `IsAlertRadiologyProcsEnabled`, but the service that raised the alert was closed and is gone; its query remains (`Procedures.cs:584-607`). Rebuild it as a check on the database server's PC, like the time check, or relabel the check box on the Chart preferences page as having no effect? | Rebuild it in a follow-up after TC-5, which owns that page (keep everything) |
| **E-19** (owner) | `EhrMeasures.Update` and the `ehrmeasure` table's Numerator and Denominator columns were used only by `EHR.dll`. Keep them, unused, with no schema change? | Yes, recorded in `docs/port-notes/EHR-4.md` |

---

## 14. Downloads (batch E; nothing is fetched by this plan)

- HL7 CDA-core-xsl at tag `v4.1.0-beta.2`: `CDA.xsl`, `cda_l10n.xml`, `cda_narrativeblock.xml` and `README.md`, from
  `github.com/HL7/CDA-core-xsl`. The tag has no LICENSE or NOTICE file; the notice cites the README and the file headers.
- `examples/CCD-full.xml` from the same repository's default branch at a pinned commit (its SHA in `docs/port-notes/EHR-3.md`), for the
  golden tests only. The tag has no `examples/` folder. Only the copy with its licensed codes replaced enters the repository (EHR-3).
- The AAFP `ccr.xsl` at a pinned revision of `sourceforge.net/p/ccr-resources`, with its licence header. EHR-3 records whether the SVN
  head or the "CCR XSL V2.0" file release (2014) is the pinned one, and why.
- CMS's "2013 June eCQM Specifications for Eligible Professionals" package and the June 2013 eCQM logic guidance (public, eCQI and CMS).
  EHR-2 checks the manifest against them, and EHR-5 checks CMS74v3 and CMS75v2. They stay outside the repository, because they carry the
  stewards' notices.
- These are text and document files, not binaries. Each stylesheet's SHA-256 goes into the notices and `docs/port-notes/EHR-3.md`.
- They need the owner's approval. The standing approval covers only the monthly .NET downloads.
- No VSAC, UMLS, CPT, CDT or SNOMED file is ever downloaded for the repository.

---

## 15. Follow-ups outside these packages (after EHR-5, E-17)

- **The radiology-orders alert** (E-18), after TC-5.
- **HL7 sending application and assigning authority** (E-15).
- **InfoButton's dead URL** (`FormInfobutton.cs:1093,1117`; MedlinePlus Connect is now `https://connect.medlineplus.gov/application`),
  after S3a-2, which owns line 1117.
- **SHA-1 in `FormEhrHash.cs:40`.** Today's 170.210(c) asks for SHA-2.
- **RxNav's discontinued interaction API.**
- **Engine fixes** from EHR-4b's deviation list.
- **Direct certificates:** the certificate-import package of O-13 (net10-plan section 12), done by DIRECT-CERT
  (`docs/port-notes/DIRECT-CERT.md`).

---

## 16. Changes to other documents (lead, with this plan)

- **`docs/specs/net10-plan.md`:**
  - S1-6's list gains `tests/EhrTests` (111 projects with TC-0's and S0-F's `S0FTests`).
  - S3a-2's list gains the starter's redirected `new Process()` in `EhrSummaryCcds.cs`.
  - S3a-3 notes that EHR-5 stores QRDA I zip entries as UTF-8 (one caller of `ODZip.GetDefaultEncodingBytes` fewer).
  - S3a-12 lists `WindowsTime.cs` (`SetSystemTime`) and `EhrRenderChild.cs` (`SetErrorMode`) as checked.
  - S3b-4 keeps the `/ehr-render` branch first in `ProgramEntry.Main`, before the code-page registration line.
  - Section 7's default-font source test allows that branch before `SetDefaultFont`, since it returns before any window.
  - Section 6 gains section 11's checks for 3a.
- **`docs/specs/wpf-tooth-chart-plan.md`:** TC-2's brief deletes `ControlChart.cs:28-30`. Section 3's rows for `THIRD-PARTY-NOTICES.txt`
  and `ReleaseIdentityTests.cs` gain EHR-3. Section 3 gains a row for `docs/third-party-binaries-audit.md` (TC-4, TC-9b; EHR-0 before
  TC-4). Section 2's first-wave row notes that EHR-0 takes the slot TC-0 frees, and S1-2 the next free one (section 10.1).
- **`docs/owner-questions.md`:** E-1 to E-19.
- **`docs/decoupling-plan.md:48` and `docs/third-party-binaries-audit.md:133`:** done by EHR-0.
- **`docs/port-notes/LIB-WEB.md:121`:** done by EHR-3.
- **`docs/roadmap.md`:** one EHR line.

---

## 17. Size

Seven packages, about 26-36 dev-days in all: EHR-0 3-4, EHR-1 1.5-2, EHR-3 5-7, EHR-2 5-7, EHR-4a 4-6, EHR-4b 4-6, EHR-5 3-4. Counsel
time for E-6 and E-8 to E-11 is extra. EHR-5 waits for E-10.

---

## 18. Sources

| Group | Source |
|---|---|
| Open Dental manual (public) | [EHR Measure Reports](https://opendental.com/manual/ehrmeasures.html) |
| Open Dental manual (public) | [EHR Dashboard](https://opendental.com/manual/ehrdashboard1.html) |
| Open Dental manual (public) | [Annual Provider Keys](https://www.opendental.com/Manual/ehrproviderkeys.html) |
| Open Dental manual (public) | [Quarterly Keys](https://opendental.com/manual/ehrquarterlykeys.html) |
| CMS and ONC | [CMS EHR Incentive Programs](https://www.cms.gov/Regulations-and-Guidance/Legislation/EHRIncentivePrograms/index.html) |
| CMS and ONC | [Stage 2 EP table of contents](https://www.cms.gov/Regulations-and-Guidance/Legislation/EHRIncentivePrograms/Downloads/Stage2_MeaningfulUseSpecSheet_TableContents_EPs.pdf) |
| CMS and ONC | [2016 EP table of contents](https://www.cms.gov/regulations-and-guidance/legislation/ehrincentiveprograms/downloads/2016_eptableofcontents.pdf) |
| CMS and ONC | [2015 through 2017 (Modified Stage 2) overview](https://www.cms.gov/Regulations-and-Guidance/Legislation/EHRIncentivePrograms/Downloads/2015_EHR2015_2017.pdf) |
| CMS and ONC | [Stage 3 and modifications to Meaningful Use in 2015 through 2017 (final rule)](https://www.federalregister.gov/documents/2015/10/16/2015-25595/medicare-and-medicaid-programs-electronic-health-record-incentive-program-stage-3-and-modifications) |
| CMS and ONC | [2013 June eCQM specifications for eligible professionals](https://ecqi.healthit.gov/content/2013-june-ecqm-specifications-eligible-professionals) |
| CMS and ONC | [2014 eCQM logic guidance, June 2013](https://www.cms.gov/regulations-and-guidance/legislation/ehrincentiveprograms/downloads/2014_ecqm_logicguidance_june2013.pdf) |
| CMS and ONC | [CMS74v15 specification](https://ecqi.healthit.gov/sites/default/files/ecqm/measures/CMS74-v15.0.000-QDM.html) |
| CMS and ONC | [45 CFR 170.210](https://www.law.cornell.edu/cfr/text/45/170.210) |
| CMS and ONC | [ONC NTP Reference Document v1.0](https://www.healthit.gov/sites/default/files/ntp_reference_doc_v1_0.pdf) |
| Measure stewards | [NCQA terms on CMS165 (FHIR IG mirror)](https://build.fhir.org/ig/cqframework/dqm-content-cms-2025/Measure-CMS165FHIRControllingHighBP.html) |
| Measure stewards | [NCQA CMMI participant terms](https://www.ncqa.org/wp-content/uploads/NCQA-Measures-CMMI-Program-Participant-Terms-of-Use.pdf) |
| NLM (VSAC, UMLS, SNOMED CT) | [VSAC Downloadable Resources (archived eCQM releases)](https://vsac.nlm.nih.gov/download/ecqm) |
| NLM (VSAC, UMLS, SNOMED CT) | [VSAC eCQM downloads help](https://nlm.nih.gov/vsac/support/usingvsac/downloadecqm.html) |
| NLM (VSAC, UMLS, SNOMED CT) | [VSAC SVS API](https://www.nlm.nih.gov/vsac/support/usingvsac/vsacsvsapiv2.html) |
| NLM (VSAC, UMLS, SNOMED CT) | [VSAC API: expansion profiles](https://www.nlm.nih.gov/vsac/support/usingvsac/svsapiendpoints/listexpansionprofiles.html) |
| NLM (VSAC, UMLS, SNOMED CT) | [VSAC code systems](https://www.nlm.nih.gov/vsac/support/authorguidelines/code-systems.html) |
| NLM (VSAC, UMLS, SNOMED CT) | [UMLS licence](https://www.nlm.nih.gov/research/umls/knowledge_sources/metathesaurus/release/license_agreement.html) |
| NLM (VSAC, UMLS, SNOMED CT) | [SNOMED CT licensing](https://www.nlm.nih.gov/healthit/snomedct/snomed_licensing.html) |
| Stylesheets and browsers | [HL7 CDA-core-xsl](https://github.com/HL7/CDA-core-xsl) |
| Stylesheets and browsers | [CDA-core-xsl files at tag v4.1.0-beta.2](https://github.com/HL7/CDA-core-xsl/tree/v4.1.0-beta.2) |
| Stylesheets and browsers | [CDA-core-xsl examples (default branch)](https://github.com/HL7/CDA-core-xsl/tree/master/examples) |
| Stylesheets and browsers | [CDA-core-xsl releases](https://github.com/HL7/CDA-core-xsl/releases) |
| Stylesheets and browsers | [CDA-core-xsl security notes](https://github.com/HL7/CDA-core-xsl/wiki/Security-Notes) |
| Stylesheets and browsers | [CDA-core-xsl issue 41: image parameters unused](https://github.com/HL7/CDA-core-xsl/issues/41) |
| Stylesheets and browsers | [CDA.xsl parameter documentation (Nictiz)](https://decor.nictiz.nl/pub/CDA/xsl/documentation/CDA.html) |
| Stylesheets and browsers | [AAFP ccr.xsl](https://sourceforge.net/p/ccr-resources/code/HEAD/tree/CCR%20XSLT/src/xslt/ccr.xsl) |
| Stylesheets and browsers | [ccr.xsl fork with a date fix](https://github.com/snosrap/ccr) |
| Stylesheets and browsers | [SMART Health IT on CDA.xsl risks](https://smarthealthit.org/?p=5927) |
| Stylesheets and browsers | [Chrome: deprecating XSLT](https://developer.chrome.com/docs/web-platform/deprecating-xslt) |
| Windows time and NTP | [SetSystemTime](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-setsystemtime) |
| Windows time and NTP | [SetLocalTime](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-setlocaltime) |
| Windows time and NTP | [Windows Time service technical reference](https://learn.microsoft.com/en-ca/windows-server/networking/windows-time-service/windows-time-service-tech-ref) |
| Windows time and NTP | [Windows Time accuracy support boundary](https://learn.microsoft.com/windows-server/networking/windows-time-service/support-boundary) |
| Windows time and NTP | [RFC 5905, NTP version 4](https://www.rfc-editor.org/rfc/rfc5905) |
| Windows time and NTP | [ServiceController.StartType](https://learn.microsoft.com/dotnet/api/system.serviceprocess.servicecontroller.starttype) |
| Other | [StackOverflowException](https://learn.microsoft.com/dotnet/api/system.stackoverflowexception) |
| Other | [MedlinePlus Connect](https://medlineplus.gov/medlineplus-connect/web-application/) |

---

## Appendix. Changes from the reviews

### First review

Each finding was checked again at `4871b4959` and in public sources before its fix was applied. "As proposed" means the review's fix was
applied. "Changed" means the fix was wrong or incomplete, and the line says what was done instead.

| Item | How it was resolved | Where |
|---|---|---|
| 1 (blocking) A test forbids XSLT in the program | Changed: EHR-3 owns `HtmlGenerationTests.cs` (no other package names it) and rewrites both tests to allow XSLT in exactly one place. The runner lives in the exe (`EhrRenderChild`), not the business library, so four of the five assemblies stay XSLT-free as today; Cecil checks the exe by method and that `ProgramEntry.Main` is the only caller; the e-mail stylesheet is still never read. E-12 states the rule change and the separate-exe option | 1, EHR-3 step 7, 10.2, E-12 |
| 2 (blocking) Wrong value-set release and file format | As proposed: the June 2013 EP release (June 14, 2013; profile "MU2 EP Update 2013-06-14") matches the engine's nine versions; the 2015 note is dropped. Plus: the importer reads the Excel workbook and SVS XML, matches headers by name, checks versions with the engine's own methods read by reflection, and the first step records the real files. The eCQI and CMS June 2013 documents join batch E | 1, 8.1, 8.2, EHR-2, E-7, 14, 18 |
| 3 (high) QRDA carries Open Dental's identity | As proposed (author root, address, phone, names). Plus: the two comments written into the output (5927, 7273), which the test's "Open Dental" check would catch; the office root gets the extension `OpenPmsDental`; HL7 PID-3.4 joins E-15 | EHR-5, E-15 |
| 4 (high) `ccr.xsl` reads itself | As proposed (fixed base URIs, four resources served, a golden CCR with dates). Plus: the resolver goes to `Transform` too, because `document()` resolves through it there | EHR-3 step 2, Tests |
| 5 (high) Encodings in resources and pipes | Changed: `;utf-8` as the e-form entries already have, with a hash test, as proposed. The pipes carry framed UTF-8 bytes in both directions, encoded and decoded by the program itself, and the child ignores the declared encoding, so no `StandardOutputEncoding` and no code page are needed (this also settles item 24's first point) | EHR-3 steps 1-3 |
| 6 (high) "Set the clock" trusts NTP | Changed: the automatic Set mode is dropped (section 4.4, E-3). `NtpReply.TryParse` gets the proposed rules plus length, round-trip and distance limits, kiss-o'-death handling and the era rule; EHR-0 owns `NTPv4.cs`. The all-zero offset is about −63 years, not −126. The request becomes version 4 and uses an IPv4 address | 4, EHR-0 |
| 7 (high) No permission checks without the key gate | As proposed (Setup for the buttons, key rows with a security log entry, the SNOMED tools, the import). Plus: the map needs Problem Definition Edit as the problem windows do; `EhrKeyAdd` stays unused (HQ-only); the CPT import gets the same check; the measure windows stay open as in Open Dental (E-4) | 5.1, EHR-1, EHR-2, E-4 |
| 8 (medium) Launch check 3 tests the wrong thing | As proposed: CPOE is checked through a procedure entered by the provider's user. Section 3.2 now says the Rx lock follows `ShowFeatureEhr` alone | 3.2, 11 |
| 9 (medium) Modified Stage 2 thresholds | As proposed, plus: 2015's secure messaging is a yes/no attestation; the rule lives in the window, so the engine's output and golden numbers do not change; a period across two years gets a warning; the 2015 alternate exclusions are recorded as not modelled | 9.2, EHR-4a, E-16 |
| 10 (medium) Measures this build cannot meet | As proposed (marked rows, a release note, an E-16 line) | 9.2, E-16 |
| 11 (medium) Test rules cannot be met; EHR-4 undersized | Changed: the allowlist is the engine's own codes plus CDCREC, CVX and HCPCS Level II; ICD-10-CM and SOP go to counsel (their terms are not confirmed). The deviation list and the split into EHR-4a and EHR-4b (8-12 days) as proposed | 6, 9.3, EHR-4a, EHR-4b, E-8 |
| 12 (medium) Work tables left behind | Changed: cancel between measures through a per-measure method and a flag, not `ProgressWin`; drop leftovers older than one day, not "older than the run", which would drop another workstation's running tables | EHR-4a, EHR-5 |
| 13 (medium) Bulk export and one `ccd.html` | As proposed: one HTML per XML through one batch child per run, with a per-document limit and a restart; the no-HTML alternative is in E-13 | EHR-3 steps 3, 5, E-13 |
| 14 (medium) EHR-3's window contradicts the rule | As proposed: after S1-6 (and the stage 1 gate), before S2-4. S3a-8 joins section 10.2; the fallback window is after S4-1 and TC-4, before 3a wave 1 | EHR-3, 10.1, 10.2 |
| 15 (low) The project-file rule contradicts itself | As proposed ("except `tests/EhrTests`' own project"). Changed for the golden files: tests read them from the repository folder, so no copy item is needed at all | 6 |
| 16 (low) EHR-5 cannot be promised for the bridge | As proposed: only EHR-2 is required; EHR-4b and EHR-5 may land later | 1, 10.1, EHR-4b, EHR-5 |
| 17 (low) CDA.xsl may emit script | As proposed (`useJavascript=false`, empty whitelist, output tests). Plus: upstream issue #41 says the image parameters are unused, so the tests check the output | EHR-3 step 2, Tests, 12 |
| 18 (low) Licence file details | As proposed: no upstream LICENSE or NOTICE at the top level; the entries cite the Apache text already in `THIRD-PARTY-NOTICES.txt`; E-11 reworded. Plus: the licence text beside file exports, for counsel | EHR-3 step 6, E-11 |
| 19 (low) Licensed codes in the source; counsel questions | As proposed: the inherited code lists in E-8 and E-9, the manifest's sources in E-8 and E-10 | 8.3, E-8, E-9, E-10 |
| 20 (low) Missing gated features | As proposed: the radiology alert as a follow-up after TC-5 (E-18), Direct as a launch check and release note, the unused measure storage recorded (E-19), the QRDA stylesheets written by EHR-5 under both names | 3.2, 5, 11, 15, EHR-5, E-18, E-19 |
| 21 (low) The manual window sets the clock on opening | As proposed: opening only measures; a button sets the clock under the rules of 4.5. The dead `TimesInSynchFast` path goes | 4.5, EHR-0 |
| 22 (low) The wrong preference write call | As proposed: `UpsertRaw`, the preference signal and a security log entry, as `RecallProphyPerioSwitch` does | 4.2 |
| 23 (low) Tests to run, and one that cannot be written | As proposed (ReleaseFlowTests and `ShouldStart` for EHR-0; PdfLibraryTests and ReleaseFlowTests for EHR-3; MiddleTierMockTests and the DevDbTool schema check for EHR-2). Plus: `ehrcode` is in the verifier's HQ-only list today, so EHR-2 moves it and runs ForkMigrationTests, M201Tests and P262SchemaTests | EHR-0, EHR-2, EHR-3 |
| 24 (low) Points for the .NET 10 stages | As proposed (`SetErrorMode`, an explicit exe path with a seam, a depth limit before the child). Changed: the limit is 200 levels, which real documents never reach, because the child's stack is what overflows; the child also has a watchdog; the code-page point needs no S3b-4 change (item 5); section 7's default-font test is amended | EHR-3 steps 2-3, 10.1, 16 |

### Second review

Each finding was checked again at `05e123429` and in public sources before its fix was applied. None was declined.

| Item | How it was resolved | Where |
|---|---|---|
| R2-1 `ccr.xsl` writes received text as markup (items 4, 17) | Changed: confirmed five `disable-output-escaping` fields at the SourceForge head (`method="html"`). `CDA.xsl` uses the attribute once, on its own CSS text, so only the CCR transform gets an escaping output writer, which keeps the file unmodified; the copy marked as modified also drops the five attributes (E-11). A hostile CCR test and a launch check were added | EHR-3 step 2, Tests, 11, 12, E-11 |
| R2-2 No `examples/` at the pinned tag (items 4, 18) | Changed: confirmed (tag: `CDA.xsl`, `README.md`, three `cda_l10n` files, `cda_narrativeblock.xml`, `documentation/`). `CCD-full.xml` comes from the default branch at a pinned commit, and its golden HTML is recorded from the tag's `CDA.xsl`, as proposed. Plus: the file holds SNOMED CT and CPT codes, so only a copy with those codes and their display names replaced enters the repository (section 6). The NOTICE wording was fixed in step 6 too | EHR-3 Tests, step 6, 14, E-11 |
| R2-3 A legacy office root keeps Open Dental's arc in QRDA (item 3) | As proposed (the test assertion, a legacy-root fixture, the warning, launch check 8, E-15). Plus: EHR-5 owns the QRDA button handler (135-204) for the warning | EHR-5, 11, E-15 |
| R2-4 Writer slots overbooked in stage 1 | As proposed (EHR-0 takes TC-0's slot; S1-T and S1-1 never wait; the second wave is S1-3, S1-4 and TC-1; EHR-1 is written when the first of them merges). Changed: EHR-1 must merge before S1-6 starts (its classic EhrTests items), not before S1-5. S1-2's longer wait is stated, and section 16 asks for a chart-plan line | 1, 7 (decision), EHR-0, EHR-1, 10.1, 16 |
| R2-5 A Middle Tier client keeps a stale `EhrCodes` list | As proposed: EHR-2 owns `Cache.cs` (`FillCache`), a Middle Tier mock test, a 10.2 row | EHR-2, 10.2 |
| R2-6 A file without measure IDs would be refused | As proposed, plus: such a file is refused when none of its OIDs is in the manifest | EHR-2 step 3, Tests, 8.2 |
| R2-7 RFC 5905's distance threshold (item 6) | As proposed: 1 second (MAXDIST, RFC 5905's global parameters, checked) | 4.3 |
| R2-8 The stack-overflow exit code on net48 | Changed: public reports show both `0x800703E9` and `0xC00000FD` from .NET Framework processes, so no runtime is tied to either code. Every non-zero exit is handled alike, and the seam test uses both codes | EHR-3 step 3, Tests, 18 |
| R2-9 The 2014 update is the EP one of May 30, 2014 (item 2) | As proposed: VSAC's expansion-profile list was opened directly. "April 2014" became the 2014 EP update ("MU2 EP Update 2014-05-30"). The 2013 profile now cites that list in 8.1 and 8.2, and 8.2's "(summary)" went; the Downloadable Resources listing keeps its mark | 8.1, 8.2, EHR-2 Tests, 11 |
| R2-10 Line references after S0-F | As proposed (`menuItemEHR_Click` 4596-4602; `ProgramEntry.Main` at 20, first statement 21; 111 test projects; S0FTests and `ProgramNamesTests.cs:223` read `ProgramEntry.cs`). Plus: the 10.2 row's numbers, and the basis names `05e123429` (the code is as at `08f7f8ca6`) | Basis, 5.1, EHR-3, 10.2, 16 |
| R2-11 The binaries audit is shared with the chart track | As proposed: a 10.2 row, and a section 16 line for the chart plan's section 3 | EHR-0, 10.2, 16 |
| R2-12 Engine codes as literals in fixtures (items 11, 19) | As proposed: the tests read them from the engine's strings at run time, and the enforcing test fails on such literals | 6, 9.3 |
| R2-13 A stopped Windows Time still sets the clock | Changed: the start type decides, as proposed, but `w32tm /resync` fails while the service is stopped, so a stopped service gets the advice to start it first. The button is offered only when Windows Time is disabled. Section 4.4 and launch check 4 follow | 4.4, 4.5, 11, 18 |
| R2-14 The starter spells the exe name | As proposed: `ProgramNames.ExeFileName`, and `EhrSummaryCcds.cs` joins the rename guard's map. Plus: EHR-3 owns that map entry, and a 10.2 row names S3a-7 and S4-1 | EHR-3, 10.2 |
| R2-15 The work-table helper on the Middle Tier (item 12) | As proposed: it runs on the server through `Meth`, with a Middle Tier mock test | EHR-4a |
