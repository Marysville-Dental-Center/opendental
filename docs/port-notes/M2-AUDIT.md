# Port note: M2-AUDIT, birthdate audit

Branch `port/m2-audit`, cut from `port/foundation` (which holds M2-01 and M2-02) in the single-wave run. Backlog section: [port-backlog.md, M2-AUDIT](../specs/port-backlog.md#m2-audit-birthdate-audit). Feature spec: [features-26.1.md, v26_1-tools-audit-birthdate](../specs/features-26.1.md#v26_1-tools-audit-birthdate-audit-trail-birthdate-additions-and-changes). No fork migration step, no schema and no registry entry: the entries use the existing `PatientEdit` audit type (range 2027004501-2027004599 left unused).

Sources: the public 26.1 release notes (Tools section) and Open Dental's public 26.1 highlights blog post, as cited in the spec. No Open Dental program was installed, observed or decompiled, and no office data was used. Every behavior that the public sources leave open is marked INFERRED below.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v26_1-tools-audit-birthdate` | built | A `PatientEdit` audit entry with the old and new dates whenever a patient's birthdate is added, changed or removed, from every save path. |

## How it works

- `Patients.GetBirthdateChangeLogText(dateOld,dateNew)` builds the text, `Patients.InsertBirthdateChangeSecurityLogEntry(patOld,patCur)` writes the entry. Both are in the new partial-class file `OpenDentBusiness/Data Interface/Patients.BirthdateAudit.cs`.
- Two one-line hooks in `Patients.cs` call the helper on the server side of the Middle Tier check, right after the row is written:
  - `Patients.Insert`: `InsertBirthdateChangeSecurityLogEntry(null,pat)`, so a patient created with a birthdate gets an "added" entry.
  - `Patients.Update`: `InsertBirthdateChangeSecurityLogEntry(oldPatient,patient)`. It logs exactly when the CRUD update writes a different `Birthdate` (both compare the date part).
- So no window or import calls the helper itself. That covers every save path the spec names, and the ones it doesn't name:
  - Edit Patient Information (`FormPatientEdit` → `Patients.SavePatientEdit` → `Update`), for existing patients and for new ones (`IsNew`);
  - Add Family Member (the Family module's Add button inserts a blank patient, then Edit Patient Information saves it);
  - the Add Family window (`FrmPatientAddAll`, `Patients.Insert` with the birthdate already set) and `Patients.CreateNewPatient`;
  - sheet import (`FormSheetImport` sets `_patient.Birthdate`, then `Patients.Update(_patient,_patientOld)`);
  - eForm auto import (`EFormImport.ImportAutoFields` → `Update`) and the manual eForm import, which goes through `FormSheetImport`;
  - every other `Patients.Update` caller that changes the birthdate: HL7, the 834 import, bridges, Database Maintenance.
- A source test makes sure nothing writes patient rows around those two methods: no `PatientCrud` write outside `Patients.cs`, and no raw `UPDATE patient`/`INSERT INTO patient` SQL that sets `Birthdate`. The frozen legacy convert chain, generated CRUD and tests are not checked.

## Files touched

- `OpenDentBusiness/Data Interface/Patients.BirthdateAudit.cs` (new): the helper.
- `OpenDentBusiness/Data Interface/Patients.cs`: two added lines, one in `Insert`, one in `Update`. The file is Windows-1252 with CRLF line endings; it was edited byte for byte, with no other change.
- `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line for the new file, right after `Patients.cs`.
- `tests/M2AUDITTests/` (new test project), `OpenDental.sln` and `OpenDental.Local.slnf` (one entry each).
- `docs/port-notes/M2-AUDIT.md` (this file).

`OpenDental/Forms/FormPatientEdit.cs` (owned by this package) and `OpenDental/Main Modules/FormOpenDental.cs` are not changed: the hook in `Patients.Update` already covers both.

## INFERRED behaviors

1. **Wording.** "Patient birthdate changed from '3/4/1985' to '4/3/1985'". This is the wording of the other patient-field lines (`SecurityLogEntryHelper`), so the Audit Trail reads alike and a search for "birthdate" finds every entry. A blank date shows as `''`, like a blank address or phone: an addition reads "changed from '' to '3/4/1985'", a removal "changed from '3/4/1985' to ''".
2. **Blank means before 1880**, the program's usual rule (`DOBFormatHelper`, the import windows). Going from 0001-01-01 to another pre-1880 date is not logged; a pre-1880 old date is shown as `''`.
3. **Date part only.** The column is a `date`, so a time of day on either side is not a change.
4. **Short-date format** of the computer that saves the patient (`ToShortDateString`). With Middle Tier, that is the server's culture, because the hook runs on the server.
5. **A separate entry.** The birthdate gets its own `PatientEdit` entry. It is not merged into the name/status/address entry that `InsertAddressChangeSecurityLogEntry` writes, because that helper only runs on some save paths (Edit Patient Information for existing patients, the family-wide updates, the TxtOK prompt). So saving Edit Patient Information with a new birthdate and a new address writes two `PatientEdit` entries.
6. **New patients.** A patient inserted with a birthdate gets an "added" entry, besides the window's own `PatientCreate` entry. The blog's "additions" is read as covering new patients as well, and the spec lists Add Family Member and new patients as save paths.
7. **Real dates**, also when Edit Patient Information shows the birthdate masked (spec rule 4). A masked birthdate the user didn't unmask is never saved, so it is never logged.
8. **No signed-in user** (a background service): the entry is written for user 0 through `SecurityLogs.MakeLogEntryNoCache`, so an import never fails because of the audit. In every other case it is written for the signed-in user with the current `SecurityLogs.LogSource`.
9. **eForm auto import** keeps its own summary entry ("Auto import from eForm #n …"). That entry already lists a birthdate change, so for eForm imports the birthdate shows in both entries.

## Deviations from the spec

- Rule 3 says to put the helper next to `InsertAddressChangeSecurityLogEntry`. It is in a new partial-class file instead, because of the single-wave rule for shared files: `Patients.cs` gets only the two hook lines. The helper is still a `Patients` method beside the others.
- The spec's touchpoints name `FormPatientEdit.cs` and the `FormOpenDental.cs` pattern. Neither needed a change, because the hooks are in the data layer that every save path goes through.

## Interplay with other packages

- **B-AUDIT** (patient clinic and preferred-name audit, merged on the integration branch before this package). The code merges cleanly: B-AUDIT's lines are in `InsertAddressChangeSecurityLogEntry` and its own partial file `Patients.ClinicPreferredAudit.cs`, and its import hooks (`FormSheetImport`, HL7 PV1, clone synch) sit after `Patients.Update`. Only the `OpenDentBusiness.csproj` `Compile` lines conflicted (both kept). The entries stay separate and the birthdate is never logged twice:
  - Edit Patient Information for an existing patient: the name/address entry (with B-AUDIT's preferred-name and clinic lines) plus this package's birthdate entry.
  - Sheet and eForm import through `FormSheetImport`: the birthdate entry plus B-AUDIT's "... From sheet import." entry, which only has the preferred-name and clinic lines (tested in `SheetImport_Birthdate_Logged`).
  - B-AUDIT's note suggested adding the birthdate line to `GetPreferredAndClinicChangeLogText` instead. That was not done: it would reach only the paths B-AUDIT hooks, not Add Family, `CreateNewPatient`, the 834 import, bridges or Database Maintenance, and together with the `Patients.Update` hook it would log the birthdate twice.
  - The tests did not merge cleanly, although the code did. B-AUDIT's `AddPatient` helper inserts every synthetic patient with a 1980-01-01 birthdate, so since this package each patient starts with a "changed from '' to '1/1/1980'" entry, and 12 B-AUDIT tests that count all of a patient's `PatientEdit` rows got one row too many. The integration fix is in the test only: `AddPatient` remembers the last `securitylog` row of its setup, and `GetPatientEditLogs` returns only the rows written after it, so each test still sees exactly the entries of the action it tests. No assertion was loosened.
  - The two packages differ on new patients: B-AUDIT logs nothing for a creation (it is a `PatientCreate`), this package logs a birthdate the patient is created with as added (INFERRED 6, owner question 2).
- **M2-EF** (eForms): patient field rows are written by `PatFields`, not `Patients`, so they are not part of this audit. Any eForm or sheet import that saves through `Patients.Update` is covered without changes; a new import that wrote patient rows any other way would fail `BirthdateSavePathTests`.
- **V-BRIDGE packages** (V-BRIDGE2 takes `Patients.cs` over from this package): bridges that update patients through `Patients.Update` are audited automatically.
- **M2-SHELL** logs `PatientSelected` (M2-02's enum value 275). That is unrelated; both appear in the Audit Trail under their own types.

## Integration (`port/m2-audit-t1`, 2026-10-31)

- Merged `port/int-7` at `1763cc7ae` into the package branch. Conflict: `OpenDentBusiness/OpenDentBusiness.csproj`, where B-AUDIT's and this package's partial files had their `Compile` lines in the same place; both are kept.
- Review finding (B-AUDIT's tests break after the merge, and this note said the packages were independent): real, fixed as described under B-AUDIT above, and this note corrected.
- No fork migration step, no schema, no preference and no row change, so the adoption tools (`tools/AdoptionValidation`) need nothing new.

## Tests

`tests/M2AUDITTests` (MSTest v1, x86, .NET 4.8), 28 tests:

- `BirthdateAuditTextTests` (no database): added, changed, removed, same date with a time of day, blank on both sides, pre-1880 as blank, culture-dependent short dates (en-US, en-GB), null patients.
- `BirthdateAuditDbTests`, on a scratch database `m2audit_<time>_<pid>_<random>_audit` built from the seed dump, the frozen 24.3.63 chain and the fork migration runner, then dropped. Covers `Patients.Update` (changed, other fields only, blank to date and back), `Patients.Insert` (with and without a birthdate), no signed-in user (user 0, save still succeeds), Edit Patient Information through `Patients.SavePatientEdit` (existing patient: one birthdate entry plus the separate address entry; birthdate untouched: none), Add Family Member (blank insert, then `SavePatientEdit` with `isNew`), `Patients.CreateNewPatient`, sheet import (the window's save sequence), and eForm auto import (`EFormImport.ImportAutoFields` with a Birthdate date field).
- `BirthdateSavePathTests` (source checks): no direct `PatientCrud` writes outside `Patients.cs`, no raw SQL that writes `Birthdate`, the hooks sit in `Insert` and `Update`, and Edit Patient Information, sheet import, the Add Family window and eForm import still save through them.

Related existing assemblies run for this package: `EFormTests` (eForm import and its `PatientEdit` entry), `ApptTests` and `RowHashTests` (they insert and update patients and check security logs or hashes), plus `NoHqTests`. All passed.

Not tested: the WinForms and WPF windows themselves (`FormSheetImport`, `FrmPatientAddAll`, `FormPatientEdit`): their save sequence is checked in the source and repeated in the business-layer tests. Middle Tier: the hook's server-side placement is checked in the source only.

## Owner questions

1. Is the wording "Patient birthdate changed from '' to '3/4/1985'" fine for additions and removals, or should those read "birthdate added" and "birthdate removed"?
2. Should a patient created with a birthdate get the "added" entry (built), or only later edits?
3. The Audit Trail shows real birthdates, as the spec asks, also to users who see them masked in Edit Patient Information. Is that acceptable, given who has the Audit Trail permission in the office?
