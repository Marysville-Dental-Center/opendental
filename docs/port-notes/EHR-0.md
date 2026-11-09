# Port note: EHR-0 (switch, time check, clean-up)

Package EHR-0 of `docs/specs/ehr-rebuild-plan.md` (section 7), on branch `ehr/ehr0-2` (it continues `ehr/ehr0-1`, from `main`
`8cc82f38d`; the fix round of 2026-11-08 is in section 7). The owner answered E-1 to
E-19 as recommended on 2026-11-08; this package carries out E-2 (one confirmation, the same in Show Features and the Setup Wizard, no
password) and E-3 (TimeSync is Off or Warn, the program never sets the clock by itself, Windows Time keeps it, no migration). No schema
change, no new `PrefName`, no new third-party file, and no project file edited except the new `tests/EhrTests` (its sln and slnf entries are
in their own first commit for the lead). Synthetic data only; no network, no database and no window of the program in the tests, and the
computer's clock is never set.

## 1. Turning EHR on (section 3.1)

- `FormShowFeatures.checkEhr_Click` no longer refuses. It calls `FormShowFeatures.HandleEhrClick(isChecked)`, a public static method that the
  Setup Wizard calls too (`UserControlSetupWizFeatures.checkEhr_Click`, wired in its designer). Checking the box shows the one confirmation,
  `ConfirmEhrOn` (OK/Cancel, text from `GetEhrOnConfirmationText`): turning EHR on changes how prescriptions, patient race, required fields
  and the Chart work and shows the EHR tools; this program is not certified EHR technology and its measures cannot be used to attest or
  submit to any program; a restart is needed. Cancel unchecks the box; OK is followed by the restart message; unchecking shows the restart
  message, as before. No password (Show Features still asks for one for clinics only).
- Both windows save `ShowFeatureEhr` where they did (`butSave_Click`, `ControlDone`).
- The wizard's explanation (`labelInfoEHR.Tag` in `UserControlSetupWizFeatures.resx`) no longer tells offices to contact Open Dental for an
  EHR contract or calls the software 2014-Edition certified. It says what turning EHR on shows and changes, and that the program is not
  certified and its measures are for internal review only.

## 2. The time check (section 4)

**The setting.** `TimeSyncCheck.PREF_NAME` = `ForkTimeSyncMode`, a fork-owned preference row: `0` Off (the default), `1` Warn. A missing
row, a cache that is not loaded or any other value reads as Off (`ParseMode`), so no database step and no seed row. Read with
`PrefC.GetRawOrDefault`, written with `PrefC.UpsertRaw` (`SaveMode`); the window then signals `InvalidType.Prefs` and writes a Setup
security log entry (`GetChangeLogLine`), as the Recall Types window does for `RecallProphyPerioSwitch`. `EnumTimeSyncMode` is a new enum of
the fork's own, stored as 0 or 1 in a fork row; it is not one of the enums of `docs/port-notes/FORK-ENUMS.md`. The choice is the check box
"Check the database server's clock every 4 hours" in Setup > Chart > EHR > Time Synchronization. The thread reads it when it starts, so it
applies at the next start of the thread: the next logon on the database server's computer (see "How often" below).

**ShouldStart and Decide** (`OpenPmsDentalBusiness/Misc/WindowsTime.cs`, class `TimeSyncCheck` at the end of the file; pure functions):
- `ShouldStart(mode,isOnDbServer)` is true only for Warn on the database server's computer (`ODEnvironment.IsRunningOnDbServer`, the test the
  thread used before). Off on the server, Warn on a workstation and unknown values are false. `BeginTimeSyncThread` no longer reads
  `ShowFeatureEhr`.
- `Decide(mode,result)` returns `Alert` only for Warn and a valid reply whose offset is above 5 seconds (5.0 exactly is within), and `None`
  for everything else. `EnumTimeSyncDecision` has only these two values: there is no "set the clock".
- `Next(state,mode,result)` adds the rules for the other replies, so the thread holds no logic of its own: a valid reply clears the count of
  failures; an invalid reply or no reply is written to the log (`Logger`, folder `TimeSync`) only, and every sixth failure in a row (about a
  day, see "How often" below) raises one alert with the last reason; a kiss-o'-death reply logs its code and stops the checks until the
  program starts again (`TimeSyncCheckState.IsStopped`, which lives as long as the main window). `Next` is not pure: it is deterministic and
  changes only its state argument. `Apply` writes the log line and raises the alert through `RaiseAlertIfNoneRecent`: a generic alert for
  every clinic and user (`ClinicNum` -1, Medium, Mark as Read, Delete, Show Item Value). The offset alert reads "The clock of the database
  server's computer is N seconds off <server>. Set up Windows Time (see the manual window)."; its item value gives the direction and the
  `w32tm` steps.
- **When an alert is repeated** (`HasRecentAlert`, fix round of 2026-11-08). An alert of the same kind (the same description start, so the
  offset alert and the failed-for-a-day alert are separate) suppresses a new one only while it exists **and** was raised less than 24 hours
  ago (`AlertItem.SecDateTEntry` against the database server's `NOW()`, the clock that wrote it). An older one does not, so an alert that was
  read but never deleted no longer silences every later one for good: while the problem lasts there is at most one new alert a day. One
  whose entry time is later than now (the clock went back) does not suppress either. Deleting an alert re-arms it: the next check that finds
  the problem raises it again.
- **How often** (approximate). The thread runs one check as soon as it starts and then every 4 hours. `SetThreads` stops it at logoff and
  starts it again at logon, so on the database server's computer there is also a check at every logon, as the old thread had. "Every 4
  hours" is therefore at least every 4 hours, and "six failures in a row = one day" is about a day: the count of failures lives as long as
  the main window, so checks at logons make the day shorter.

**The reply rules** (`OpenPmsDentalBusiness/Misc/NTPv4.cs`, class `NtpReply` at the end of the file; pure functions).
`NtpReply.TryParse(reply,sentTransmit,destinationUtc,out offset,out reason)` returns true only for a valid reply, with the offset in seconds
(RFC 5905's theta: positive when this computer is behind); `Parse` gives the whole `NtpResult` (`Valid`, `Invalid`, `KissOfDeath`, and
`NoReply` from the client). In this order:
1. at least 48 bytes;
2. version 3 or 4;
3. mode 4 (server);
4. the originate time equals, bit for bit, the transmit time the client sent (and that one is not zero);
5. stratum 0 is a kiss-o'-death reply, with its four-letter code. It is checked only after the originate time matched, so a forged one from
   off the path cannot stop the checks;
6. leap indicator not 3;
7. stratum 1 to 15;
8. receive and transmit times not zero;
9. round-trip delay from 0 to 1 second;
10. root delay / 2 + root dispersion below 1 second (MAXDIST).

Each timestamp is placed in the NTP era nearest the client's clock (`FromTimestamp`), so replies on both sides of 2036-02-07 06:28:16 UTC
and a rollover during the round trip work, and the offset uses the client's own send time, never the copy in the reply.

**The request.** `NTPv4.Query(server)` sends version 4 (`0x23`, Open Dental sent `0x1B`, version 3) to the first IPv4 address of the name
(an IPv6 first address threw before) on a connected UDP socket, with the transmit time set. The low 20 bits of the transmit time's fraction
are random (below the clock's resolution, about 0.24 ms), so the originate time is harder to guess. The round trip is timed with a
stopwatch and the destination time is the client's send time plus it, so the coarse `DateTime.UtcNow` cannot make a short round trip
negative. `Query` never throws: no name, no IPv4 address, a timeout or a socket error come back as `NoReply` with the reason. One Event Log
entry per request, through `ProgramNames.EventLogSource` as before (a computer where the source is not registered now gets no entry instead of
a failed request). `getTime` stays, built on `Query`, and returns `double.MaxValue` without a valid reply; it no longer throws for a bad
name. Nothing in the program calls it any more, only the tests; it is kept because working code stays (the owner's rule).

**Setting the clock.** `WindowsTime.TrySetUtc(newTimeUtc,out error)` calls `SetSystemTime` (UTC: no daylight-saving ambiguity) through the
seam `WindowsTime.FuncSetSystemTimeUtc` and checks Windows' result. On success it writes the Event Log entry ("System date and time set
to ... UTC"); on failure it returns the reason. `ERROR_PRIVILEGE_NOT_HELD` (1314) says that only an administrator running the program
elevated, or an account given "Change the system time" in Local Security Policy, can set the clock. The program never asks for elevation
and never changes user rights. `WindowsTime.SetTime` and its `SetLocalTime` import (result ignored, one call across a daylight-saving
change) are removed: nothing calls them any more. The class stays in the global namespace, as before.

**The manual window** (`FormEhrTimeSynch`, section 4.5).
- Opening it and Measure (the button formerly "Sync Time") only measure, through `TimeSyncCheck.Measure`: this computer against the NTP
  server and against the database server, as before. It never sets the clock.
- "Set this computer's clock" is enabled only when `TimeSyncCheck.GetManualSetOffer` allows it: the last reply was valid (with a finite
  offset), the offset is from 1 second to 1 hour, the user has the Setup permission, and Windows Time is disabled (or not installed) on that
  computer. The service is read with `ServiceController` (`GetWindowsTimeState`), start type first. Otherwise the button's note gives the
  advice: Windows Time running, run `w32tm /resync` as administrator; stopped but not disabled, `net start w32time` and then
  `w32tm /resync`; more than an hour, set the date and time by hand. Disabled: the note says another time program on that computer may
  change the clock again.
- **The button never applies the last measurement** (fix round of 2026-11-08), which may be hours old and may describe a clock that has
  changed since. It checks Setup and the 4-second request limit, then measures again (`TimeSyncCheck.PrepareManualSet`, through the same
  three functions as Measure: the NTP request, the database server's clock, this computer's clock), shows that measurement in the window
  and checks `GetManualSetOffer` on the fresh reply, with Windows Time read again. If the fresh reply is not valid or the offer no longer
  holds, it says why ("Measured again just now; the clock was not set." and the offer's reason) and sets nothing. Otherwise the
  confirmation shows the fresh server and offset, and `TimeSyncCheck.TrySetManual` sets the clock from that measurement only: now (UTC)
  plus the fresh offset, through `TrySetUtc`, if the confirmation was answered within 2 minutes of the measurement
  (`ManualSetMaxAge`, timed by a stopwatch, which a change of the clock does not move); `TrySetManual` also checks the reply's rules once
  more. Success writes a Setup security log entry with the old and the new time (UTC) and the fresh server; failure shows the reason.
  `TrySetManual` is `TrySetUtc`'s only caller, and the window's button is `TrySetManual`'s.
- `TimesInSynchFast`, `ServerInSynchFast` and the `IsAutoLaunch` path (no caller; they set the clock without these checks) are removed. The
  window's text no longer says that the program must run as administrator or stay open on the server to keep the clock.
- The server address is saved to `NistTimeServerUrl` only after a valid reply, now with the preference signal.

## 3. Clean-up

- The `EhrCodeList` "do not kill" case of `SetThreads` goes. `FormODThreadNames.EhrCodeList` stays, marked `[Obsolete]` like
  `CacheFillForFees`, so the later values keep their numbers. No preload of the code list; EHR-2 measures whether one is needed.
- `NoHqTests.Ehr_ReflectionHooksAreGone` leaves NoHqTests (EHR-2 and EHR-3 make its three assertions false). Its place is
  `tests/EhrTests/NoEhrDllTests.cs`: no string in the program's code names `EHR.dll` or a type in it (comments may tell the history), no
  `*.csproj`, `*.config` or `*.resx` file of the repository names `EHR.dll` (or references an assembly named EHR) outside XML comments and
  `.resx` comment elements, no EHR file loads an assembly, and `#if EHRTEST` only in `ControlChart.cs` until TC-2 deletes it there.
- `docs/decoupling-plan.md` and `docs/third-party-binaries-audit.md` no longer say "remove" for EHR.

## 4. Deviations and choices the plan left open

- `Decide` returns None or Alert as the plan says; the failure count, the day alert and the kiss-o'-death stop are in `Next`, which is
  deterministic and changes only its state argument, so every rule of section 4.3 is testable without the thread.
- The day alert comes with every sixth failure in a row (about once per failed day), and the count starts again with a valid reply.
- "Not repeated while one is open" (section 4.3) is now the 24-hour rule of section 2: a recent alert of the same kind that still exists
  suppresses a new one; an older one, or a deleted one, does not.
- The set button measures again and must be confirmed within 2 minutes (section 2, "The manual window"); the plan's text sets the clock
  from "the last reply".
- The offset of `TryParse` is in seconds.
- Both time files write the Event Log through a seam (`NTPv4.ActionWriteEventLog`, `WindowsTime.ActionWriteEventLog`) whose real method
  calls `EventLog.WriteEntry(CodeBase.ProgramNames.EventLogSource,...)`, so tests write nothing to the Event Log.
- `FormEhrTimeSynch.resx` (the window's own text, `label6`) is edited too; the plan's list names the window's `.cs` and `.designer.cs`.
- The window keeps measuring on the UI thread, as before (one request, at most 2 seconds).

## 5. Tests

`tests/EhrTests` (classic MSTest v1, x86, explicit items, references the program and the business library as `tests/LibWebTests` does).
An `[AssemblyInitialize]` replaces `WindowsTime.FuncSetSystemTimeUtc` with a fake that throws and the Event Log seams with no-ops before any
test runs (both of them: `NTPv4.ActionWriteEventLog` and `WindowsTime.ActionWriteEventLog`); a test that needs a seam puts its own fake in
and restores the guard. The messages go through `Lans.g`, which returns the English text in en-US; in another culture it would look the
text up in the language table, and insert a row there, if a database connection were open (none is; the project header says so). 67 tests:

- `EhrSwitchTests` (7): the confirmation's content; checking asks once, Cancel unchecks without the restart message, OK keeps it and shows
  the message, unchecking asks nothing; both windows call `HandleEhrClick` and are wired to their handlers, no password; the refusal is gone
  and both windows still save `ShowFeatureEhr`; the wizard's explanation makes no certification claim.
- `TimeSyncTests` (25): the guard has replaced the clock seam and both Event Log seams before any test (none is its real method); the
  setting (Off unless 1, not a `PrefName`, written with `UpsertRaw`, the Prefs signal and the Setup entry; Off
  without the cache); `ShouldStart` for every combination; `Decide` for every mode and kind of reply; `Next` (the alert text, failures,
  the day alert, the reset, kiss-o'-death); an alert suppresses a new one only while it exists and is less than 24 hours old (ages from 0
  to 30 days, no entry time, an entry time after now, an old and a recent one, a deleted one, the two kinds, other types), and a drift that
  lasts three days gives one alert a day, with a deleted one re-armed; the thread starts only through `ShouldStart` and never reaches
  `WindowsTime`; the set button's rules and the Windows Time advice; the start type decides first; measuring never calls the clock seam;
  the set button, through the seams: a stale measurement (600 seconds off another server) is never applied, the fresh one (30 seconds,
  then -12.5 with a local "now") is, and the confirmation shows the fresh server and offset; a fresh reply that is invalid, no reply,
  kiss-o'-death, no offset, under 1 second, over an hour, no Setup, Windows Time running, stopped or unreadable each set nothing and say
  why, as do a changed offer, a confirmation answered after 2 minutes and a negative age; the window's Load, Measure, its display and
  refresh never set the clock, and its button checks Setup and the request limit, measures again, checks the offer on that measurement,
  confirms, calls `TrySetManual` (its one call; the window names no `WindowsTime` member) and writes the audit entry, in that order, and
  never reads the last measurement; `TrySetUtc` passes UTC and writes the Event Log; a failed `SetSystemTime` (1314, 87, -1, a throwing
  seam) gives its reason and no success entry; only `WindowsTime.cs` reaches `SetSystemTime`, `TrySetManual` is `TrySetUtc`'s one caller
  and nothing calls `SetLocalTime`; both time files write through `ProgramNames.EventLogSource`.
- `NtpReplyTests` (27): synthetic packets built from RFC 5905's layout (`Support/NtpPacket.cs`, timestamps computed independently of the
  code): a good reply, hand-worked timestamps, version 3, stratum 15, 68 bytes and leap indicator 1 accepted, a local destination time;
  each rule broken in turn (short and null replies, leap indicator 3, versions 0-2 and 5-7, modes other than 4, strata 16 and up, the
  originate time one bit, one era or wholly off, a request without a transmit time, zero receive or transmit times, delays above 1 second
  or negative, root distances from 1 second); kiss-o'-death with its code, and a forged one with another originate time; the all-zero
  reply, which fails at version 0, then (with a valid first byte) at the originate time, then (with the originate time echoed) at the zero
  receive and transmit times; RFC 5905's known timestamps; the era placement; replies on both sides of the 2036 rollover and a rollover during the round trip;
  a later era; the version 4 request; the random low bits; the first IPv4 address (192.0.2.x and the IPv6 loopback only); no request without a
  server name, with its one Event Log entry caught by the test's own fake.
- `NoEhrDllTests` (8): the scan reads the program (more than 3000 files, more than 100 EHR files) and not the tests; the small C# reader
  separates comments from strings; no string names `EHR.dll`; no project file, configuration file or resource names `EHR.dll` or
  references an assembly named EHR outside comments (more than 50, 10 and 500 of them read, no build output, this project's own header
  passes by the comment rule); the XML reader drops `<!-- -->` and `.resx` comment elements only; no EHR file loads an assembly;
  `#if EHRTEST` only in `ControlChart.cs` and defined nowhere; the `EhrCodeList` case is gone and its value stays `[Obsolete]` with its
  number.

Targeted runs (NoHqTests always included): NoHqTests, BSHELLTests, ReleaseFlowTests, LibraryBindingTests, ProjectFileTests and EhrTests in
the first round; NoHqTests, BSHELLTests, ReleaseFlowTests, LibraryBindingTests and EhrTests in the fix round. `InitSqlTests` and
`ForkMigrationTests` are not affected (no schema change) and were not run.

## 6. For the lead

- **Release notes.**
  - EHR can be turned on again in Show Features and in the Setup Wizard, with one confirmation. Turning it on changes how prescriptions,
    patient race, required fields and the Chart work. This program is not certified EHR technology; its measures cannot be used to attest
    or submit to any program.
  - The automatic clock setting of the EHR feature is gone. Databases that had EHR on no longer set the database server's clock (no
    migration). Let Windows Time keep the clock: as administrator on the database server's computer, `w32tm /config
    /manualpeerlist:time.nist.gov /syncfromflags:manual /update`, then `w32tm /query /status`.
  - New setting in Setup > Chart > EHR > Time Synchronization: "Check the database server's clock every 4 hours" (off by default). On the
    database server's computer it checks at each logon and every 4 hours, and raises an alert when the clock is more than 5 seconds off
    the time server, and after about a day of failed checks; while the problem lasts the alert comes again once a day, or at the next
    check after it is deleted. It never sets the clock.
  - The Time Synchronization window only measures when it opens. "Set this computer's clock" is offered only where Windows Time is
    disabled, for an offset from 1 second to 1 hour, to users with Setup, and it needs an administrator (or the "Change the system time"
    right); otherwise the window gives the Windows Time steps. The button measures again before it asks, and sets the clock from that
    measurement only.
- **Sln and slnf.** `tests/EhrTests` is wired in the first commit (`OpenDental.sln`, `OpenDental.Local.slnf`), as TC-0 did; keep or redo
  it, and re-run `tools/Get-ProjectBaseline.ps1` for the new project. S1-6 converts it and S4-1 moves it to MSTest 4 (section 16 of the plan
  asks the lead to add it to S1-6's list in `docs/specs/net10-plan.md`, with `WindowsTime.cs` and `SetSystemTime` for S3a-12).
- **Not done here.** The test of section 6 that fails on a licensed code literal in `tests/EhrTests` belongs with the first package that
  adds fixtures (EHR-2 or EHR-4b); EHR-0 adds no fixture and no code list.
- **Launch checks (section 11).**
  1. Show Features: check EHR, see the confirmation, OK, then the restart message; after a restart the Chart has its EHR button. Cancel
     leaves the box unchecked. Turn it off again: the restart message. The same in the Setup Wizard (Features page), with the same
     confirmation; its info text makes no certification claim.
  4. Time check, on a test database:
     - Off (the default): no `Logger\TimeSync` folder or line after start, no TimeSync thread.
     - Warn on the database server's computer, with the clock moved 30 seconds: after the next logon one alert ("... is 30 seconds off
       ..."), seen by a user with the default alert categories; no second one at the next logon while it exists (less than 24 hours);
       after deleting it, the next logon raises it again.
     - Opening the Time Synchronization window does not change the clock.
     - On a test computer with Windows Time disabled, measure, then move the clock by hand by a minute and click the set button: the
       confirmation shows the new, fresh offset, not the one in the window before the click.
     - Windows Time disabled, the set button as a standard user: the message names "Change the system time" and the administrator.
     - Elevated, Windows Time disabled: the clock is corrected; Event Log entry and a Setup security log entry with the old and new time.
     - Windows Time running: the button is disabled and the note says `w32tm /resync`.
     - Windows Time stopped but not disabled: the note says `net start w32time`, then `w32tm /resync`.

## 7. Fix round of 2026-11-08

The reviews of `ehr/ehr0-2` passed apart from the Event Log seam (fixed in `2ea665a0e`). The lead's follow-ups, each with its commit and
test:

1. **Safety: the set button applied the last measurement, however old.** It now measures again on the click, checks the offer on that
   fresh reply, shows the fresh server and offset, and sets the clock from it only, within 2 minutes; otherwise it says why and sets
   nothing (section 2, "The manual window"). Commit `983de6abd`. Tests `SetButton_MeasuresAgain_AndAppliesOnlyTheFreshMeasurement` (a
   stale measurement is never applied; the fresh one is), `SetButton_FreshReplyInvalidOrOfferGone_SetsNothingAndSaysWhy`, and the source
   checks in `OpeningTheWindow_OnlyMeasures_AndOnlyTheButtonSetsTheClock` and `OnlyTrySetUtc_ReachesSetSystemTime`.
2. **One read-but-not-deleted alert silenced every later one.** An alert of the same kind now suppresses a new one only while it exists
   and was raised less than 24 hours ago (`HasRecentAlert`, `RaiseAlertIfNoneRecent`; section 2, "When an alert is repeated"). Commit
   `9399fb733`. Tests `Alerts_SuppressANewOneOnlyWhileTheyExistAndAreLessThan24HoursOld` and `Alerts_ADriftThatLasts_GivesOneAlertADay`.
3. **The EHR.dll scan read only C# string literals.** `NoProjectFileConfigOrResource_NamesEhrDll` scans `*.csproj`, `*.config` and
   `*.resx` outside comments too. Commit `19635cd55`. Tests `NoProjectFileConfigOrResource_NamesEhrDll` and
   `TheXmlReader_DropsCommentsOnly`.
4. **Wording.** `Next` is deterministic and changes only its state argument, not pure (`9399fb733`); the all-zero reply test checks each
   rule in turn, with the originate time echoed so the zero-time rule fires (`AllZeroReply_IsInvalid`); `getTime` has no caller in the
   program, only the tests, and stays; `docs/decoupling-plan.md:48` says what was done; the `tests/EhrTests` project header notes the
   `Lans.g` language table (`3c486fd52`).
5. **This note.** The thread checks at every logon on the server as well, so "every 4 hours" and "six failures = one day" are approximate
   (section 2, "How often"); deleting an alert re-arms it (with rule 2). The commit that adds this section.
