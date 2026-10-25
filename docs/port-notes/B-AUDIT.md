# B-AUDIT: patient audit-trail entries

Package B-AUDIT of [the port backlog](../specs/port-backlog.md) (wave B1, built in the single-wave run on branch `port/b-audit`, from
`m1/integration-a2-patch`). Sources: the public 25.2 and 25.3 release notes (Miscellaneous) and the public Audit Trail manual page. Neither source
says which windows write the entries or what the entries say, so the wording and the list of save paths below are our own (INFERRED).

## Features

| Feature ID | Status | Notes |
|---|---|---|
| `v25_2-security-audit-patient-clinic` | built | A change to `patient.ClinicNum` writes a `PatientEdit` line "Patient clinic changed from 'A' to 'B'", clinics named by abbreviation and clinic 0 by "None". |
| `v25_3-family-audit-preferredname` | built | A change to `patient.Preferred` writes a `PatientEdit` line "Patient preferred name changed from 'x' to 'y'". |

No schema, preference, permission or enum change. Both items use the existing `PatientEdit` log type. No fork migration step.

## How it works

The logic is in a new partial-class file, `OpenDentBusiness/Data Interface/Patients.ClinicPreferredAudit.cs`:

- `GetPreferredAndClinicChangeLogText(patOld,patCur)`: the two lines, in the same "Patient ... changed from 'x' to 'y'" form as the existing
  lines of `InsertAddressChangeSecurityLogEntry`. Empty when neither field changed. A null and a blank preferred name count as the same.
- `GetClinicChangeLogLine(clinicNumOld,clinicNumNew)`: the clinic line. A clinic missing from the cache (for example a deleted one) or without an
  abbreviation shows as "ClinicNum N"; two clinics with the same abbreviation get their ClinicNums added, so an entry never reads as a change from
  a clinic to itself.
- `InsertPreferredAndClinicChangeSecurityLogEntry(patOld,patCur,source)`: a `PatientEdit` entry with only these lines plus "From [source].", for
  the save paths that do not call `InsertAddressChangeSecurityLogEntry`.
- Private helpers that `ChangeClinicsForAll` calls to read the patients it is about to move and to write one entry per moved patient, in batches
  of 500 through the existing `SecurityLogs.MakeLogEntry(permType,listPatNums,logText)`, so every entry also gets its `securityloghash` row.

## Save paths

| Save path | What changed | Entry |
|---|---|---|
| Edit Patient Information window (`FormPatientEdit` -> `Patients.SavePatientEdit` -> `InsertAddressChangeSecurityLogEntry`) | one hook line in `InsertAddressChangeSecurityLogEntry` | the two lines join the existing `PatientEdit` entry, next to the name, status and address lines; a new patient's first save writes no change entry (it is a creation) |
| Family-wide updates (`UpdateAddressForFam`, `UpdateEmailPhoneForFam`) and the "OK to text" prompt in `FormOpenDental` | none needed | they call the same helper, but do not change these fields, so they add no lines (tested) |
| Clinics window, move all patients to another clinic (`FormClinics` -> `Patients.ChangeClinicsForAll`) | `ChangeClinicsForAll` reads the PatNums first and logs each one | one entry per moved patient ("... From moving all patients of the clinic to another clinic."); the window's existing summary entry (PatNum 0) is kept |
| Sheet and eForm import (`FormSheetImport.butImport_Click`) | one hook line after `Patients.Update` | "From sheet import." or "From eForm import." |
| Inbound HL7 PV1 segment with a patient location (`MessageParser.ProcessPV1`) | one hook line after `Patients.Update` | "From an inbound HL7 message (PV1 segment)." |
| Clone synch (`Patients.SynchCloneWithPatient`), which copies the preferred name in capitals | one hook line after the existing billing-type entry | on the clone, "From clone synch with patient #N." |
| eForm auto import (`EFormImport`, A-EF2) | none | already writes "Preferred changed from 'x' to 'y'" in its own `PatientEdit` entry |

Paths left as they are, on purpose:

- Patient creation (Add Family, 834 import, HL7 new patients, `CreateNewPatient`, `CreateClone`): a creation is logged as `PatientCreate`, not as
  a change.
- Deleting a patient in the Family module sets the clinic to 0 and is logged as "Patient deleted" (`ControlFamily`, not this package's file).
- DBM `PatientDeletedWithClinicNumSet` clears the clinic of deleted patients and already writes a `dbmlog` row per patient.
- The PT Dental (Paperless Technology) bridge import overwrites the preferred name without any audit entry for any field. It is a vendor bridge,
  off by default; see the owner questions.
- Appointment-driven clinic changes: the spec suggests looking for them, but our 24.3.63 code has none (no code sets a patient's clinic from an
  appointment).

## Files touched

- New: `OpenDentBusiness/Data Interface/Patients.ClinicPreferredAudit.cs`, `tests/BAUDITTests/` (project, `AuditTextTests.cs`,
  `PatientAuditDbTests.cs`, `ScratchDb.cs`), this note.
- `OpenDentBusiness/Data Interface/Patients.cs` (owned): 4 added lines, nothing else changed (the file is Windows-1252; edited byte for byte).
- `OpenDental/Forms/FormClinics.cs` (owned): the move's thread timeout goes from 2 to 10 minutes, because each move now also writes one entry per
  patient and the timeout kills the threads, which would leave patients moved without their entries.
- Outside this package's list: `OpenDental/Forms/FormSheetImport.cs` (owned by B-EF in the same wave) and `OpenDentBusiness/HL7/MessageParser.cs`
  (no owner), one added line each; `OpenDentBusiness/OpenDentBusiness.csproj` (one `Compile` line), `OpenDental.sln` and `OpenDental.Local.slnf`
  (the test project).

## INFERRED behaviour

- The wording of both lines, and the "From ..." source line on the paths outside the Edit Patient window.
- Logging from every save path listed above, and one entry per patient for the Clinics window move (the spec lists `ChangeClinicsForAll` as a
  touchpoint but does not say whether Open Dental writes per-patient entries).
- Clinic names: abbreviation, "None" for 0, "ClinicNum N" for an unknown clinic or a blank abbreviation.
- Case-only changes of the preferred name are logged (the comparison is exact, like the other lines).
- The sheet/eForm import hook logs only the preferred name and clinic, not the other fields the import changes (that import has never written
  field-level `PatientEdit` entries; widening it is an owner decision).

## Deviations from the spec

- None in behaviour. The spec's `FormPatientEdit.cs` and `FormOpenDental.cs` touchpoints needed no edit: both reach the shared helper through
  `Patients.cs`, as the backlog note says.

## Interplay with packages built in parallel

- **M2-AUDIT** (birthdate audit, "from every save path, including sheet and eForm import") edits the same method and the same import paths. It
  can add its birthdate line to `GetPreferredAndClinicChangeLogText` (or a sibling in the same partial file) and so reach every hook added here,
  including the sheet/eForm import and HL7 paths, without another hook. If it adds its own line next to the hook in
  `InsertAddressChangeSecurityLogEntry`, the merge is a one-line conflict.
- **B-EF** owns `FormSheetImport.cs` in this wave. The only change here is one line after `Patients.Update(_patient,_patientOld);` in
  `butImport_Click`; if B-EF restructures that method, keep the call right after the patient row is saved.
- **A-HASH** (merged): the per-patient entries go through `SecurityLogs.MakeLogEntry(permType,listPatNums,logText)`, which writes the
  `securityloghash` rows; the bulk test checks that every entry has one.
- **V-BRIDGE packages**: if one of them takes over `WpfControlsOD/Bridges/PaperlessTechnology.cs`, it can call
  `Patients.InsertPreferredAndClinicChangeSecurityLogEntry(patOld,pat,"PT Dental import")` after its `Patients.Update` if the owner wants that.

## Tests

`tests/BAUDITTests` (MSTest v1, x86, .NET 4.8):

- `AuditTextTests` (no database; the clinic cache is filled from a made-up table): line wording, null/blank preferred names, case changes,
  "None", unknown and blank clinics, duplicate abbreviations, line order.
- `PatientAuditDbTests` (scratch database `baudittest_<UTC time>_<pid>_<random>` on 127.0.0.1:3306, dropped afterwards; stale ones older than
  12 hours are dropped by later runs): `InsertAddressChangeSecurityLogEntry` (preferred, clinic, combined with name lines in one entry, other
  fields only, nothing changed), the Edit Patient window save (`SavePatientEdit`: logged exactly once and saved; family address sync adds no
  preferred/clinic lines for other members; a new patient writes none), `ChangeClinicsForAll` (each moved patient logged once and nobody else;
  1,201 patients logged and hashed across three batches; same clinic writes nothing), the import helper with its source line, clone synch, the
  HL7 PV1 path (known and unknown clinic) and the family e-mail/phone sync.

Not covered by an automated test: the one-line hook in `FormSheetImport` (a WinForms window; the helper it calls is tested) and the Clinics window
itself (its business call is tested).

## Checks (2026-10-25)

The work was saved by an interrupted run as one "WIP (interrupted run)" commit; this run found it complete, built it and ran the checks without
code changes.

- Build of `OpenDental.Local.slnf` (Debug, x86): 0 errors, 376 warnings (the baseline; none new).
- Targeted tests through the shared runner: `BAUDITTests` 29/29, `EFormTests` 60/60 (the eForm import's own `PatientEdit` entries),
  `RowHashTests` 21/21 (the `securityloghash` rows), `NoHqTests` 49/49. No test was inconclusive.
- `tools/Test-NoHqHosts.ps1`: passed.
- No fork migration step, so `ForkMigrationTests` was not part of the targeted run.

## Questions for the owner

1. Should a sheet or eForm import also log the other patient fields it changes (name, address, phones), as the Edit Patient window does? Today
   it logs only the preferred name and clinic (new) plus the existing "imported" entries.
2. Should the PT Dental (Paperless Technology) bridge import log its patient changes?
3. Is one Audit Trail entry per patient right for the Clinics window move, or is the existing single summary entry enough? Per-patient entries
   make the change visible in each patient's own Audit Trail but add one row per patient.
