# B-SERVER: server connections, replication and report setup

Package B-SERVER of [../specs/port-backlog.md](../specs/port-backlog.md) (M1 Tier B, wave B1), built in the single-wave run on branch `port/b-server` from `m1/integration-a2-patch`. Tests: `tests/BSERVERTests`.

## Features

| Feature | Status | Notes |
|---|---|---|
| `v24_4-reports-incomplete-procnotes-all` | built | New filter **Include all procedures with no notes** in the Incomplete Procedure Notes report, and a matching default in Report Setup > Misc Settings. |
| `v24_4-setup-reportserver-audit` | built | The entry existed since 24.3.35. Fixed: the SSL CA line no longer replaces the earlier lines; a switch between Off, Direct Connection and Middle Tier is logged; every field is compared whatever the mode; a password change is logged without the password. |
| `v25_3-misc-pem-path-label` | built | Label "SSL CA certificate (.pem) path" in Choose Database, Report Setup and Preferences > Server Connections. |
| `v25_4-setup-pem-path-label` (25.4 twin) | built, once, with the 25.3 item | The text boxes are renamed `textSslCaPem` (were `textPEM` and `textSkySQL`); `labelSkySQL` in Server Connections is now `labelSslCaPem`. Stored values are unchanged (`FreeDentalConfig.xml` SslCA, `ReportingServerSslCa`, `ReadOnlyServerSslCa`). M2-VERIFY can mark the twin as done. |
| `v25_2-server-replication-delay` | built (our design, Low confidence) | Two settings in a new **Replication** group of Preferences > Server Connections. Everything except "a read/write delay when replication is used" is INFERRED. |

## How it works

### Incomplete Procedure Notes: all procedures with no notes

- `RpProcNote.GetQuery` (new, public, so it can be tested without the preference cache) builds the query; `GetData` gets an optional `includeAllNoNotes` parameter (Middle Tier passes it too).
- A procedure has no note when its latest `procnote` row has blank text (`TRIM` of the text is empty, NULL counts as blank) or it has no row.
- The new option replaces the same-day test with this per-procedure test. Every procedure the same-day option finds also has no note of its own, so the same-day option is implied: the window checks and locks the same-day box while the new box is checked, and restores it when unchecked.
- Kept from 24.3: excluded codes (`ReportsIncompleteProcsExcludeCodes`) apply unless Show Excluded Codes is checked; incomplete notes (`""` or `[Prompt:"..."]`) are listed; the Incomplete and No Note columns show (the new box turns them on too); the Unsigned column is blank for rows with no note.
- Not kept for the new option: the 24.3 rule that a same-day note, even an incomplete one, hides the procedure.
- Fix to 24.3 (all options): the single-procedure test now counts distinct procedures. The note joins can return a procedure more than once (the same-day join once per noted procedure of that day), and 24.3 then showed one procedure as "Multiple procedures" with no code. Found by the tests. The excluded codes are also escaped now (`POut.String`), so a code with an apostrophe cannot break the query.

### Report Server audit entry

- `ReportServerSettings.FromPrefCache()` captures the six `ReportingServer*` preferences before and after the save; `ReportServerAudit.BuildLogEntry` writes one line per change in a fixed order (mode, server name, database, MySQL user, password, SSL CA path, Middle Tier URI). Blank values show as "(blank)". The entry is logged under `Setup`, as before, and only when something changed.

### Replication read and write delays

- **Read delay after write** (`ForkReplicationReadDelayMs`). `Db.NonQ` (all overloads, through `try/finally`) records the time of this process's last write in a monotonic clock. `ReportsComplex.RunFuncOnReadOnlyServer` runs its function on the current (main) connection instead of the read-only server while the last write is less than the delay old. That covers every read-only server use: reports and lists that call it directly, `DataConnectionCancelable`, and cache fills (`CacheAbs` calls it when `PrefC.HasReadOnlyServer()`), so the caches refilled right after a save are not stale.
- **Write delay for signals** (`ForkReplicationSignalLookbackMs`). `Signalods.RefreshTimed` looks back that much before the time it was given. A `SignalLookbackFilter` keeps, per caller stream (told apart by the type filters: high-priority types, all other types, the appointment types), the signals already returned, so a looked-back signal is never processed twice (a Print or ShutDownNow signal must not act twice). It also keeps a floor per stream (the first check made with a look-back), so a workstation never processes signals from before it started looking back.
- Both are 0 to 60000 ms, default 0 (24.3 behavior), global, and ignored when no read-only server is configured and no replication servers are defined. The read delay additionally needs a read-only server (with none there is nothing to redirect).
- Saving writes an Audit Trail entry (Setup) with old and new values and signals `InvalidType.Prefs` through the Preferences window; it does not clear the connection dictionary.

### Preferences that are not in `PrefName`

The three settings are fork-owned preference rows read with the new `PrefC.GetRawOrDefault` and written with `PrefC.UpsertRaw` (insert the first time, then update; cache updated), the pattern of `DxcPartnerIdentity`. A missing row reads as the default, so neither a 24.3 database nor an adopted Open Dental 25.3.48 database needs a migration step, and saving a window with the defaults writes no row. No fork migration level was used (range 2027001301-2027001399 left free).

| Fork row | Default | Proposed name in the spec |
|---|---|---|
| `ForkReportsIncompleteProcsAllNoNotes` | 0 | `ReportsIncompleteProcsAllNoNotes` |
| `ForkReplicationReadDelayMs` | 0 | `ReplicationReadDelayMs` |
| `ForkReplicationSignalLookbackMs` | 0 | `ReplicationSignalLookbackMs` |

`tests/RegistryTests/PrefNameTests` keeps the proposed names out of `PrefName` until they are confirmed, and public sources (25.2 release notes, the replication and replication-server manual pages, checked 2026-10-24) do not name Open Dental's keys. The `Fork` prefix keeps our rows from ever colliding with an Open Dental key of a different meaning. When the real keys are known, a small migration step copies each value to the real name and the constant changes.

## INFERRED behaviors

1. The new report option counts a procedure as noted when it belongs to a group note (`procgroupitem`) whose latest note has text and whose group procedure is not deleted. Without this, every procedure of a group note would be listed. A group note with blank text covers nothing and is itself listed.
2. A note of spaces only is "no note" for the new option (the 24.3 same-day option still treats any non-empty text as a note, unchanged).
3. Report Setup's two defaults (same-day and all) are independent check boxes; only the report window links them.
4. The audit entry's wording is ours; the 24.3 lines (Server Name, Database, MySQL User) keep their wording, the SSL CA line now uses the new label, and "URI" became "Middle Tier URI". "MySQL Password changed" is new and never shows the value.
5. The whole replication design (meaning, units, range 0 to 60000, global values, where they live) is ours, from the spec's proposal.
6. Any write by this process starts the read delay, including background writes (for example heartbeats). With delays of a few seconds this only sends a few more reads to the main server.
7. Middle Tier: a Middle Tier client sends no SQL, so its read delay starts when it inserts a signal (which follows most saves). The signal look-back is applied by the client (adjusted time sent to the server, duplicates dropped on return); the Middle Tier server never applies it to a client call, and its own cache polling (`DtoProcessor`) is not looked back.
8. The look-back is not applied to `Signalods.GetCountForTypes` (shut-down and sites checks), so a shut-down signal is never counted twice.

## Deviations from the spec

1. Preference names: the spec proposes `ReportsIncompleteProcsAllNoNotes`, `ReplicationReadDelayMs` and `ReplicationSignalLookbackMs` (to verify against the office data). We store `Fork...` rows outside `PrefName` until Open Dental's keys are confirmed (see above), so no `Pref.cs` or `PrefInfos.xml` change was needed.
2. Same-day box: the spec says to disable it while "Include all procedures with no notes" is checked. It is also checked while disabled (the option implies it), and its own state comes back when the new box is cleared.
3. Report Server audit: beyond the spec's fix (`logentry+=` and mode switches), every field is now compared whatever the mode, and a password change is noted without the value.
4. Incomplete Procedure Notes: the distinct-procedure count fix applies to all options, not only the new one (a 24.3 defect the tests found).

## Files touched

Owned by B-SERVER:

- `OpenDentBusiness/Cache/PrefC.cs`: `GetRawOrDefault`, `UpsertRaw`, `UpsertRawNoCache` (new methods only).
- `OpenDentBusiness/Db/Db.cs`: `ReplicationDelay.NoteWrite()` after `NonQ` (three places, `try/finally`).
- `OpenDentBusiness/Db/ReplicationDelay.cs` (new): settings, read delay, `SignalLookbackFilter`.
- `OpenDentBusiness/Data Interface/Signalods.cs`: look-back in `RefreshTimed`; Middle Tier client write mark in `Insert`.
- `OpenDentBusiness/Reports/RpProcNote.cs`: `GetQuery`, `includeAllNoNotes`, the default's accessors.
- `OpenDentBusiness/Reports/ReportServerAudit.cs` (new): `ReportServerMode`, `ReportServerSettings`, `ReportServerAudit`.
- `OpenDental/Forms Reports/FormRpProcNote.cs` and designer: new check box (filters group 21 px taller, grid 21 px shorter).
- `OpenDental/Forms/FormReportSetup.cs` and designer: new default check box (Misc Settings rows below it move down 17 px), audit through `ReportServerAudit`, label and `textSslCaPem`.
- `OpenDental/Forms/FormChooseDatabase.cs` and designer: label and `textSslCaPem`.
- `OpenDental/User Controls/Preferences/UserControlServerConnections.cs` and designer: label (two lines) and `textSslCaPem`, Replication group.
- `OpenDentBusiness/Data Interface/ReplicationServers.cs`, `OpenDentBusiness/Db/ConnectionStore.cs`: owned, not changed (the read redirect is decided per call, not in the cached connection).

Outside the owned list (each a minimal hook):

- `OpenDentBusiness/Reports/ReportsComplex.cs`: one condition in `RunFuncOnReadOnlyServer` (`|| ReplicationDelay.IsReadFromPrimaryRequired()`), the single place every read-only server read goes through. No package owns this file.
- `OpenDentBusiness/OpenDentBusiness.csproj`: two `Compile` lines, next to `Db\Db.cs` and `Reports\RpProcNote.cs`.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.

## Tests (`tests/BSERVERTests`)

- `IncompleteProcNotesDbTests`: the report query on a scratch database: the 24.3 results without options and with the same-day option (and the distinct-count fix); the new option's rows, columns, group-note coverage, implication of the same-day option, excluded codes, unsigned notes, grouping by patient.
- `ReportServerAuditTests`: modes, no-change, the SSL CA line no longer replacing earlier lines, mode switches, no password in the entry.
- `PemPathLabelTests`: the three designer files have the label and `textSslCaPem` and no SkySQL wording; the code behind still saves the same stored values. (The GUI is not launched.)
- `ReplicationDelayTests`: parsing and clamping, the delay window, stream keys, the look-back floor and second rounding, de-duplication per stream, pruning, turning it off.
- `ReplicationDelayDbTests`: two scratch databases stand in for the main server and a replica (the read-only server connection is pointed at the replica). Settings round-trip (rows inserted once, clamped); reads go to the replica with the delay off, to the main server for exactly the delay after a write, and back afterwards; cache fills right after a save come from the main server; a late-arriving signal is missed with the write delay off and found exactly once with it on; both settings are ignored without a read-only server or replication servers.

Checks on the final branch: build 0 errors, 376 warnings (baseline, none in the changed files); `BSERVERTests` 40/40, `RegistryTests` 33/33, `ReportTests` 28/28, `NoHqTests` 49/49; `tools/Test-NoHqHosts.ps1` passed. Not tested: the WinForms windows themselves (the GUI is not launched; the designer files are checked as text), and a real Middle Tier or a real replication pair (two scratch databases stand in for the main server and the replica).

## Interplay with packages built in parallel

- **M1-02 / M2-02 (registries)**: if a census or public source confirms Open Dental's key names, add `PrefName` entries and PrefInfos rows, a migration step that copies the `Fork*` rows to them, and switch the constants in `ReplicationDelay` and `RpProcNote`. The new controls have no PrefInfos entries, so the Preferences search does not find them yet.
- **M2-VERIFY**: `v25_4-setup-pem-path-label` is implemented here; verify only.
- **M2-NOTES** (26.1 group note second signature, `procnote.UserNum2`/`Signature2`): the report's Unsigned test still looks only at `Signature`. If a second signature should count, change `RpProcNote.GetQuery` (the `unsignedNotes` join).
- **Other packages with unconfirmed preference names** can reuse `PrefC.GetRawOrDefault` / `PrefC.UpsertRaw` after the merge.
- `FormOpenDental.cs` is not touched: `PrefC.HasReadOnlyServer` and the signal timer code stay as they are.

## Questions for the owner

1. The three settings use fork names (`Fork...`). Keep them, or do you know Open Dental's real keys from your 25.3 office database (a read-only `SELECT PrefName FROM preference WHERE PrefName LIKE '%Replication%' OR PrefName LIKE '%IncompleteProcs%'` would show them)?
2. Is it right that a procedure in a group note with text counts as noted for "Include all procedures with no notes"?
3. Should a note of spaces only count as "no note"?
4. Should the read delay start on any write (current choice) or only on user saves?
5. Does the office run a Middle Tier? If so, is starting the read delay on signal inserts (and not looking back on the Middle Tier server's own polling) acceptable?
6. Report Setup: should checking the "all procedures" default also lock the same-day default, as the report window does?
