# Release process and version guard

Status: 2026-10-19. Implements decoupling plan Phase 4 (our own release channel, local only) and backlog package M1-03 with Risk R1 ([specs/port-backlog.md](specs/port-backlog.md)).

This page covers four things:

- how version numbers work
- what the program checks at startup
- how an Open Dental database is adopted
- how a new build reaches every workstation

Nothing here talks to Open Dental's servers or reads Open Dental's update files.

## Two kinds of version

| Version | Value now | Where it lives | Used for |
|---|---|---|---|
| Release (product) version | `0.0` | `AssemblyInformationalVersion` in `OpenDental/Properties/AssemblyInfo.cs` | About window (`Application.ProductVersion`), release manifests, the `ForkProgramVersion` marker. Our own scheme. It is never compared with Open Dental numbers. |
| Compatibility version | `24.3.63.0` | `AssemblyVersion` (file version), `ForkVersions.CompatVersion` | The frozen legacy conversion chain, and `DataBaseVersion`/`ProgramVersion` on databases the fork markers don't govern yet. |

Raise the release version by editing `AssemblyInformationalVersion`, for example to `0.1`. Leave `AssemblyVersion` at `24.3.63.0`. Once the fork markers take over, the database checks no longer look at it.

A build is identified by its **build id**: a SHA-256 over the program's own assemblies (`ForkReleases.ListBuildIdFiles`: `OpenDental.exe`, `OpenDentBusiness.dll`, `CodeBase.dll` and the others). Every rebuild of `0.0` therefore counts as a different build, and third-party DLLs aren't hashed at every startup.

Because the release version stays `0.0` across rebuilds, it can't tell an older build from a newer one. Each build therefore also carries its **build time**. `OpenDental.csproj` (target `ForkWriteBuildStamp`) writes `[assembly: AssemblyMetadata("ForkBuildUtc", "…Z")]` into `OpenDental.exe`. It rewrites the stamp only when something compiled into `OpenDental.exe` changed (its sources, references, resources, the project file or `UpdateFileCopier.exe`), so an unchanged rebuild keeps its build id. The release manifest records the stamp as `BuildUtc`. A higher release version is newer. Within one release version, the later build time is newer. If either time is missing or the two are equal, the order is unknown.

## Fork version markers

These are four preference rows. The fork reads and writes them with plain SQL (`ForkVersions`), so they need no `PrefName` entry. None of the names is an Open Dental preference.

| Preference | Meaning |
|---|---|
| `ForkSchemaLevel` | The last fork migration step applied. `0` right after adoption. If this row exists, the database is adopted. |
| `OdSchemaCompat` | The Open Dental schema the database matches exactly (`25.3.48` or `24.3.63`). The M1-01 runner and schema verifier advance it. |
| `ForkAdoptedFrom` | The adoption record: `source=25.3.48.0;started=…;adopted=…` (UTC). If it has no `adopted=` and there is no `ForkSchemaLevel`, the adoption didn't finish. If it has `adopted=` but `ForkSchemaLevel` or `OdSchemaCompat` is missing, the markers of an adopted database were removed: that database is refused, never adopted again. |
| `ForkProgramVersion` | The release published from the update server: `release+buildId`, for example `0.0+3f2a…`. |

Adoption also sets `DataBaseVersion` and `ProgramVersion` to the **sentinel** `99.0.0.0`. No Open Dental release will reach that number. An Open Dental client therefore sees a "newer" database: it skips its own conversion, finds no update files and can't use the copy. The fork reads those two preferences only to recognize an unadopted database.

## Startup checks

`FormOpenDental.PrefsStartup` calls `PrefL.CheckForkVersionGuard` once the preference cache is loaded and before anything writes to the database (index re-enabling, the MySQL version backup and repair, any conversion), then once more after conversion. The guard itself only reads. Debug builds run the guard too. Open Dental skipped its version check in Debug builds; the fork doesn't, so a developer build pointed at an office copy can't convert or adopt it by accident. The decision is `ForkVersionGuard.Evaluate`:

| Database | Result |
|---|---|
| Unadopted, `DataBaseVersion` ≤ 24.3.63 | Runs. The frozen legacy chain converts older databases up to 24.3.63, never further. If the fork migration runner has steps and the database is at 24.3.63, startup asks for adoption instead. |
| Unadopted 25.3.48.0 (on the allowlist) | Refused (exit 171). Startup only offers to open the adoption tool. |
| Unadopted, any other Open Dental version (25.3.50, 25.4.x, 24.4.x, …) | Refused (exit 170). Nothing is changed. |
| Adopted, markers and sentinel consistent | Runs. Pending fork migration steps run on the update server, after a backup. |
| Adopted by a newer release (`ForkSchemaLevel` above what this build knows) | Pulls the published release if there is one, otherwise refused (exit 172). |
| Adopted, but `DataBaseVersion`/`ProgramVersion` changed, or markers incomplete | Refused (exit 173). |
| Sentinel versions with no markers (markers removed) | Refused (exit 173). Restore a backup. |
| `ForkAdoptedFrom` says `adopted=`, but `ForkSchemaLevel` (or `ForkSchemaLevel` and `OdSchemaCompat`) was removed | Refused (exit 173). This is not an unfinished adoption: the adoption tool refuses it too, so the schema level is never reset and the fork migration steps never run a second time. Restore a backup. |
| Adoption started but not finished (`ForkAdoptedFrom` without `adopted=`, no `ForkSchemaLevel`) | Refused. The adoption tool can finish it. |

No case offers a "downgrade". Open Dental's `UpdateFiles` and `UpdateFilesSegment` rows are never read. The only code that names them is `DocumentMiscs.DeleteOpenDentalUpdateFiles`, which adoption calls.

When an inactive workstation becomes active again, `FormOpenDental.IsDbConnectionSafe` runs the same guard, in Debug builds too. In Release builds, a workstation also restarts when a different build has been published, unless it may publish its own build under the rules below (`ForkReleases.MayRunUnpublishedBuild`): the named update server, or, with a blank `WebServiceServerName`, a build known to be newer. A blank preference never exempts a stale build.

The legacy `ConvertDatabases` chain is frozen at 24.3.63 (`ConvertDatabases.FrozenVersion`). Adding a `ToX_Y_Z` method above it throws, and so does running the chain on a newer database. Later schema changes are fork migration steps. `PrefL.ConvertDB` hands an adopted database to the runner through `IForkMigrationRunner` (`ForkMigrationHandOff`), which finds `ForkMigrationRunnerAdapter`: it runs M1-01's `ForkMigrationRunner` and, when the schema verifier then finds an exact match, sets `OdSchemaCompat` to `25.3.48`. The baseline runner with no steps is used only when no runner is found. `tools/New-DevDatabase.ps1` adopts the 24.3.63 database it builds, so a development database carries the same markers as an adopted office copy.

## Adopting a database

Adoption is explicit and runs once, on a restored copy. Normal startup never adopts. For a database on the allowlist (`24.3.63.0`, `25.3.48.0`), the startup message offers to open **Adopt Database** (`FormAdoptDatabase`). The administrator must confirm three things: that this is a restored copy with a backup, the exact database name, and one final OK.

For the office switch, the runbook does not use this window: `AdoptionValidation adopt` (runbook step 4) runs the same `Adoption.Run` on a copy that the database server made of the office database (runbook step 3.2), without starting the program, because starting it and logging in write to the database before the post-adoption validation (runbook step 6.1). The M1-Z rehearsal copies and adopts with the same code.

`Adoption.Run` performs these steps, in this order. Each step is idempotent. MyISAM has no transactions. A run interrupted before step 6 is finished by running it again. A run interrupted after step 6 is not: `ForkSchemaLevel` then marks the database as adopted while `ForkAdoptedFrom` has no finish time, `Adoption.Run` refuses it as already adopted, and `AdoptionValidation adopt` fails on it, closed (validation report, F13). The office runbook therefore never resumes an adoption: after any stop or failure it drops the copy and adopts a new one.

1. Write `ForkAdoptedFrom` with the start time.
2. Drop `patient._portal_active_email` and its unique index `portal_active_email_uniq`. This is an office-specific column, not Open Dental schema. Afterwards, family members can share an e-mail address again.
3. Delete Open Dental's `UpdateFiles` and `UpdateFilesSegment` rows from `documentmisc` without reading them.
4. Blank the preferences that point at Open Dental's servers. This covers a fixed list (`WebServiceHQServerURL`, `UpdateServerAddress`, `UpdateWebsitePath`, `WebHostSynchServerURL`, `MobileSyncServerURL`, `EmailHostingEndpoint`) plus any preference whose whole value is a web address on a blocked host (`CodeBase.VendorDirect.HqHosts`). Text that only mentions such a host is kept.
5. Set `DataBaseVersion` and `ProgramVersion` to `99.0.0.0`.
6. Write `OdSchemaCompat`, then `ForkSchemaLevel` (`0`). From this point the markers govern the database.
7. Run the pending fork migration steps (M1-01 runner).
8. Complete `ForkAdoptedFrom` with the finish time, and add a security log entry.

The preflight refuses these databases:

- any version off the allowlist
- `ProgramVersion` different from `DataBaseVersion`
- a database flagged `CorruptedDatabase`
- an update marked in progress
- an already adopted database
- an adopted database whose markers were removed (`ForkAdoptedFrom` says `adopted=`)
- a middle-tier connection

Re-entering bridge passwords and rehashing `SecurityHash` columns are separate steps of the M1-Z rehearsal and of the office procedure in [adoption-runbook.md](adoption-runbook.md). `Adoption.Run` doesn't do them; `AdoptionValidation adopt` runs the rehash right after it (runbook step 5).

Before adopting the office copy, stop and uninstall the Open Dental services and clients (M1-Z). All workstations switch together. Rollback means going back to the untouched original (or, if something wrote to it, to the folder copy taken before the copy was made) after this program has been removed from every workstation and the adopted database has been dumped and dropped ([adoption-runbook.md](adoption-runbook.md), Rollback).

## Publishing a release

The **update server** is the workstation named in the `WebServiceServerName` preference (Update Setup). Name it before you publish rebuilds. If that preference is blank (the default), any workstation may act as the update server, but only within limits: it may publish the first release, or a build known to be newer than the published one (a higher release version, or the same release with a later build time). It never replaces a published build that is newer or whose order is unknown. It updates to that build instead.

1. Build the solution in Release. `OpenDental.csproj` copies `UpdateFileCopier.exe` next to `OpenDental.exe`, because every release ships it.
2. Install the build on the update server, for example by copying the output folder over the installation, and start it as an administrator.
3. At startup, `PrefL.CheckProgramVersion` sees that this build isn't the published one and asks whether to publish it. If you click Cancel, this workstation runs the build without publishing it. Update Setup's Recopy button publishes the running build at any time, under the same rules: with a blank `WebServiceServerName` it refuses to replace a different published build that this one isn't known to be newer than. A silent run publishes only when nothing is published yet. It never replaces a published release, because nobody is asked.
4. Publishing (`ForkReleases.Publish`) works like this:
   - While it runs, `UpdateInProgressOnComputerName` is set, so other workstations wait.
   - It zips the installation folder, except local files: `FreeDentalConfig.xml`, logs, files with `__` in the name, and Open Dental's `Manifest.txt`. The `Sparks3D` and `OpenDentalReplicationService` sub folders are included.
   - It writes the zip as `ForkReleaseSegment` rows (`DocMiscType` 101, about 1 MB of base64 each).
   - It then writes the manifest as one `ForkReleaseManifest` row (`DocMiscType` 100).
   - Finally it sets `ForkProgramVersion`.
   - Open Dental's types are 0 to 2; the column is `tinyint(4)`.

The manifest (`ForkRelease.manifest`, `ReleaseManifest`) names the product `Open PMS - Dental`. It also records:

- the release version, build id, build time and compatibility version
- the publish time and the workstation that published it
- the SHA-256 of the archive
- for every file: its path, size and SHA-256

The archive holds a copy of the manifest without the archive hash.

### What the other workstations do

At startup each workstation compares its build with the published one (`ForkReleases.Decide`):

| Situation | Named update server (`WebServiceServerName` is this computer) | `WebServiceServerName` blank | Another computer is named |
|---|---|---|---|
| Same build id | Nothing. | Nothing. | Nothing. |
| Nothing published yet | Offer to publish. | Offer to publish. | Nothing. |
| A newer build is published (higher release, or same release with a later build time) | Pull. | Pull. | Pull. |
| This build is newer (higher release, or same release with a later build time) | Offer to publish. | Offer to publish. | Refused (exit 174), with a message to run the new build on the update server first. |
| Same release, different build, order unknown | Offer to publish. | Pull. The message says how to publish this build instead: name this computer as the update server. | Pull. |
| Debug build | Never publishes or pulls. The version guard still runs. | Same. | Same. |

**Pull** means: the workstation downloads the release and checks it: product name, archive SHA-256, only listed files, safe paths, archive manifest equal to the published one, and every file's size and SHA-256. It then asks for OK and starts `UpdateFileCopier`. UpdateFileCopier checks the manifest and every file again, both in the download and in its local copy, then copies the files, writes the manifest last and restarts the program. If any check fails, nothing on the workstation changes.

No row offers a downgrade. A pull always goes to a build that is newer, or, when the order is unknown, to the build the office published. A workstation still running an old build is never asked to publish it over the office's release. Only the named update server, which the office chose, can replace a build it can't prove older.

Dynamic mode, where Open Dental kept per-version program folders, isn't part of the fork's release channel.

## Tests

The tests are in `tests/ReleaseFlowTests` (in the solution and in `OpenDental.Local.slnf`). Run them with:

```
vstest.console.exe tests/ReleaseFlowTests/bin/Debug/ReleaseFlowTests.dll /Platform:x86
```

- Guard decisions for the full startup matrix: fresh 24.3.63, office copy 25.3.48, copies edited to 25.3.50 and 25.4.0, adopted, adopted with markers removed, adopted by a newer build, unfinished adoption.
- Adoption on scratch databases with synthetic data. The 25.3.48 case uses the `od_253_schema` patient definition when it's loaded, otherwise the column and index from `fox-db-schema.sql`.
- Publish and pull round trips, including tampered segments, a tampered manifest and a manifest for another product.
- The publish/pull decision for the named, blank and other update server cases, with and without build times; the restart rule of `IsDbConnectionSafe`; and the build time stamped into `OpenDental.exe`.
- An adopted copy with only `ForkSchemaLevel`, or `ForkSchemaLevel` and `OdSchemaCompat`, removed: refused (exit 173) and never adopted again.
- Source checks: `OpenDental.exe` reports `0.0` and `24.3.63.0`; the legacy chain is frozen; the guard code has no Debug bypass; Open Dental's update file types are only ever deleted; no downgrade prompt; UpdateFileCopier checks the manifest before copying.

Database tests use `127.0.0.1:3306` (root, blank password). They are inconclusive if no server answers, or if `RELEASEFLOW_TEST_DB=skip` is set.
