# Port note: M2-UDS, UDS+ report

Branch `port/m2-uds`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, M2-UDS](../specs/port-backlog.md#m2-uds-uds-report). Feature spec: [features-25.4.md, v25_4-reports-udsplus](../specs/features-25.4.md#v25_4-reports-udsplus-uds-report-for-fqhc-dental-services). Fork migration level used: 2027003501 (range 2027003501 to 2027003599).

Sources: the 25.4 release note line and the public Public Health report pages the spec cites, and HRSA's public UDS Table 6A material for the line names. Open Dental's own UDS+ output is not public, so the whole design below is the spec's fork design. No Open Dental binary, install or customer-only page was used. No procedure codes or code text ship with this program.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-reports-udsplus` | built | Report, setup window, patient-level file, report row and permissions. Behaviour INFERRED where listed below. |

## What was built

- **Standard Reports > Public Health > "FQHC UDS+ Dental Services"** (`ODUdsPlus`). Opens `FormRpUdsPlus`: a date range (defaults to the last full calendar year), clinics (when clinics are on), providers (All or a selection), Setup, Report and Save File.
- **Report** (`RpUdsPlus.Run`, shown through `ReportComplex`, so it prints and exports like other reports): per UDS line in setup order, the number of **visits** (one patient on one date) and of distinct **patients** with at least one counted procedure, then one total row (visits and patients with any line, each counted once, not the sum of the lines).
- **Save File**: the patient-level CSV for the center's own UDS+ tooling, one row per patient, visit and line. Columns: `PatNum, DateOfBirth, Sex, Zip, Race, RaceCodes, Ethnicity, EthnicityCodes, PreferredLanguage, InsuranceType, InsuranceFilingCode, VisitDate, ProcCodes, UdsLine, UdsService`. UTF-8 without BOM, CRLF, dates `yyyy-MM-dd`, standard CSV quoting. The file is written locally where the user chooses; nothing is sent anywhere (no FHIR client, per the spec).
- **Setup** (`FormRpUdsPlusSetup`, needs the Setup permission like Report Setup): an editable grid of lines (UDS line, service, procedure codes) stored as JSON in M2-02's `UdsPlusDentalLineCodes` preference (`{"Lines":[{"Line":"..","Description":"..","Codes":".."}]}`; a bare array is read too; blank by default). Codes: exact codes, ranges of two codes of the same length (`D1000-D1999`), prefixes (`D1*`), separated by commas, semicolons or spaces, case-insensitive. A hyphenated token whose sides differ in length (a custom code such as `D2740-L`) is an exact code. Save validates: a line number is required and unique, ranges must not run backwards, `*` only at the end. An "Add Table 6A Lines" button adds the eight dental line numbers and names (lines 27 to 34) **without codes**.
- **Rules** (spec): completed procedures only (`ProcStatus` C), by `ProcDate` in the range; deleted procedures are never complete; patients with `PatStatus` Deleted are left out; a service counts once per visit however often it was done; a procedure whose code is on two lines counts on both.
- **Permissions and audit**: the report row uses the standard Reports permission per report (checked by `OpenReportHelper`). Each Report run and each saved file writes a Reports security log entry (date range; for files the row and patient counts, no patient data). Saving the setup writes a Setup entry.
- **Report row** (`ReportRowUdsPlusStep`, level 2027003501): on a database from before 25.4 (judged like `Seed26_1Step`: `ForkAdoptedFrom` on an adopted database, otherwise `DataBaseVersion`; unknown origins are not converted) the row is inserted right after the FQHC Dental Sealant Measure (later Public Health rows move down one), hidden when the sealant measure is hidden, and every user group that may run the sealant measure gets the Reports permission for it (groups with FKey 0 run every report already). On any database, a single row with an unknown internal name and a UDS+ description (for example "UDS+ Report", "UDS Plus") is renamed to `ODUdsPlus` instead, so a row Open Dental itself wrote keeps its number and permissions. A second run changes nothing.

## Files

New:
- `OpenDentBusiness/Reports/RpUdsPlus.cs` (queries, counting, insurance and race rules, CSV)
- `OpenDentBusiness/Reports/UdsPlusLineMap.cs` (the setup's JSON, validation, code matching, Table 6A line names)
- `OpenDentBusiness/Misc/ForkMigrations/ReportRowUdsPlus.cs` (the report row step; namespace `OpenDentBusiness.ForkMigrations.Features`)
- `OpenDental/Forms Reports/FormRpUdsPlus.cs`, `.designer.cs`, `.resx`
- `OpenDental/Forms Reports/FormRpUdsPlusSetup.cs`, `.designer.cs`, `.resx` (the `.resx` files hold only the standard window icon, copied from `FormRpInsPendingSupplementals.resx`)
- `tests/M2UDSTests/` (new test project)

Changed (owned): `OpenDentBusiness/Data Interface/DisplayReports.cs` (`ReportNames.UdsPlus`, added to `ArrayKnownNames`), `OpenDental/Forms/FormReportsMore.cs` (one `case` in `OpenReportHelper`).

Changed outside the owned list (also in the report):
- `OpenDentBusiness/Misc/ForkMigrations/ForkMigrationRunner.cs` (M1-01's runner): `ValidateStep` accepted only `yyyyMMddNN` levels with a real date, so the assigned level 2027003501 (month 00) made `GetSteps()` throw. Two lines now also accept `yyyy00PPNN` levels from 2027 on (the single-wave package ranges); a real date is still required otherwise (month 13 and `2026003501` are still rejected).
- `OpenDental/User Controls/UserControlReportSetup.cs`: one `case` so Report Setup shows the internal name.
- `tests/ForkMigrationTests/Od253MigrationTests.cs` (M1-01's test) and `tests/M201Tests/Migration26_1DbTests.cs` (M2-01's test): a 25.3.48 database now gets one `displayreport` row from the new step; both "nothing else changed" checks allow that table (the M2-01 test also asserts exactly one `ODUdsPlus` row and a row count one higher).
- `OpenDentBusiness/OpenDentBusiness.csproj` and `OpenDental/OpenDental.csproj` (`Compile`/`EmbeddedResource` lines for the new files), `OpenDental.sln`, `OpenDental.Local.slnf` (the test project).

No schema change, no registry change: the preference is M2-02's (`UdsPlusDentalLineCodes`, seeded blank by `Seed26_1`).

## INFERRED behaviours

- The internal name `ODUdsPlus`, the description "FQHC UDS+ Dental Services", the placement after the sealant measure, hiding it with the sealant measure, and copying the sealant measure's report permissions.
- The output columns and the aggregate table (feature spec open question 7). Two columns beyond the spec: `PatNum` (the internal patient number, so the center's tooling can group rows by patient; no name or other identifier) and the code columns `RaceCodes`/`EthnicityCodes` and `InsuranceFilingCode` next to their descriptions.
- "Hidden or test patients": the schema has no test-patient flag, so only `PatStatus` Deleted is left out. Inactive, archived, non-patients, deceased and prospective patients with completed procedures are counted.
- Insurance type on the visit date: the patient's current plans whose subscriber effective and term dates cover the visit (blank dates are open); the lowest ordinal decides, Medicare together with Medicaid is "Dually Eligible". Category by the plan's claim filing code (X12 claim filing indicator): MC Medicaid/CHIP; MA, MB, 16 Medicare; CH, VA, OF, TV Other Public; anything else, or no filing code (claims then go out as CI), Private; no plan in effect None/Uninsured. Plans dropped from the patient before the run are not seen (no plan history is kept).
- Race and ethnicity: from `patientrace`, described by the `cdcrec` table; ethnicity when its hierarchical code starts with E. Without `cdcrec` rows the OMB minimum categories (from the CDC code set) are still named and recognised as race or ethnicity; other codes show the code. Declined and multiracial use fixed texts.
- Sex is the `Gender` field (Male, Female, Unknown, Other); language is the stored `Language` value (usually an ISO 639-2 code).
- The Table 6A line numbers and names (27 Emergency services through 34 Rehabilitative services) follow recent HRSA UDS manuals; HRSA renumbers lines, so the window tells the office to check them against the manual of the reporting year. No codes are shipped.
- The default date range is the last full calendar year.
- Codes are matched on `procedurecode.ProcCode` only (not `procedurelog.MedicalCode`).

## Deviations from the spec

- Extra CSV columns, listed above.
- Logging: the spec says running the report logs a Reports entry. The window logs each run and each saved file instead of logging once when the window closes (as the older Public Health reports do), so an opened-and-closed window logs nothing and every file that leaves the program is recorded.

## Interplay with packages built at the same time

- **M2-02**: owns `UdsPlusDentalLineCodes`; this package only reads and writes it.
- **M2-ERA** (`v25_4-reports-era-autoprocessed-filters`) and any package that adds a report: `DisplayReports.ReportNames.ArrayKnownNames` and `OpenReportHelper` are kept in step by `ReportTests.ReportNamesTests`; merges must keep both the `UdsPlus` entry and theirs.
- **V-PAYSUITE** takes `DisplayReports.cs` and `FormReportsMore.cs` over from this package in wave C4; the additions here are one constant, one array entry and one `case`.
- **Every package with a fork migration step**: if another branch also relaxes `ForkMigrationRunner.ValidateStep` for its package-range level, keep one version at merge time. The `Od253MigrationTests`/`Migration26_1DbTests` allowances of other packages that add rows to a 25.3.48 database must be merged with the `displayreport` allowance here.

## Tests

New project `tests/M2UDSTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 34 tests:

- `UdsPlusLineMapTests`: blank default, reading and writing the JSON, bad text, code matching (exact, ranges, prefixes, custom hyphenated codes), validation, setup order, the Table 6A line names without codes.
- `RpUdsPlusComputeTests`: counts against hand-computed numbers on hand-built rows (a repeated service counted once per visit, codes on several lines, an unmapped code), the rows per patient, visit and line, insurance on the visit date (effective and term dates, dual eligibility), race and ethnicity with and without `cdcrec`, CSV header, quoting and blank dates.
- `RpUdsPlusDbTests`: scratch MariaDB database (`m2uds_<time>_<pid>_<random>_report`, dropped afterwards) with synthetic patients, procedures, races and plans: completed-only, deleted procedures, deleted patients, existing work, the date range ends, clinic, provider and date filters, and that only the report's patients' race and plan rows are read.
- `ReportRowUdsPlusDbTests`: the row step on scratch databases: placement and reordering, hidden copy, permission copy, appending without a sealant row, adopting Open Dental's own row on a later database, leaving ambiguous rows alone, the origin rule through the runner, idempotence.
- `UdsPlusWiringTests`: report name, dispatcher, Report Setup, audit and permission calls, the preference seed, the step's level range and namespace, and `ValidateStep` with package-range levels.

Run for this package: `M2UDSTests`, `ReportTests`, `ForkMigrationTests`, `M201Tests`, `M202Tests`, `ReleaseFlowTests`, `RowHashTests` (plus `NoHqTests`, always added): all passed. `M201Tests` has 2 inconclusive tests that need the published XML documents (`M201_OD_DOCS_DIR`), as before.

Not tested: the two windows themselves (the GUI is not launched in this run). Their logic is in `RpUdsPlus` and `UdsPlusLineMap`, which are tested; the windows were only compiled.

## Questions for the owner

1. **Fork migration level ordering.** The assigned range (2027003501 to 2027003599) is not a `yyyyMMddNN` date, so `ValidateStep` had to accept it. More important: the startup guard runs migrations only when `ForkSchemaLevel` is below the program's highest level. Once any 20270000NN level is merged, a step written later with a real date in November or December 2026 (for example 2026112001) sorts below it and will never run on a database that already has the higher level. Before release either renumber the package levels at integration, or make the guard use the runner's pending-step list (applied levels) instead of the maximum.
2. Is `PatNum` acceptable in the patient-level file (needed to group a patient's rows), or should the file carry a per-file pseudonymous patient key instead?
3. Should the insurance category come from an office-editable mapping (filing code to UDS category) instead of the fixed X12 list above?
4. Should non-FQHC offices see the report at all? It is added visible unless the FQHC Dental Sealant Measure is hidden.

## Integration (branch `port/int-6`)

- **`ForkMigrationRunner.ValidateStep`.** B-SHELL (merged on `port/int-5`) had already let the package ranges through (`IsReservedPackageLevel`: a `yyyy00PPNN` level from 2027 on). The merge kept B-SHELL's version and dropped this package's two-line copy of the same rule; `UdsPlusWiringTests.ValidateStep_AcceptsPackageRangeLevelsOnly` passes against it unchanged.
- **The M1-Z adoption tools.** The report row step also runs when adoption brings an Open Dental 25.3.48 database to 26.1.70 (a database from before 25.4), so the adopted copy gains the UDS+ row, the later Public Health rows move down one place and the sealant measure's Reports grants are copied. `tools/AdoptionValidation` allowed only renames in `displayreport`, so adopt's row check failed and 8 `AdoptionValidation.Tests` failed. Now:
  - `ReportRowUdsPlus.Plan` works the step's rule out without writing; `Run` applies it, so the step, `migration-preview` and adopt's row check share one implementation. The step uses `ReportRows25_3`'s `GetRows`, `InsertRow` and `CopyReportPermissions` (made `internal`) instead of its own copies of them.
  - `UdsPlusRowPlan` (new, `tools/AdoptionValidation`) is the plan for a database; `migration-preview` prints it (runbook step 0.2). `AdoptionRowCheck` lets `displayreport` gain exactly the planned row (every value checked) and move exactly the planned rows (`ItemOrder` only), and `grouppermission` gain exactly the planned Reports grants for the new row. `allowed-differences.adoption.json` allows `displayreport` as a whole table (an added row changes every column checksum), held row by row by adopt.
  - The rehearsal has a new check M5, and its office fixture a sealant measure with a Reports grant and a Public Health report after it. The runbook (steps 0.2, 4, 6.1 and the checklist) and the tool's README say what to expect.
  - New tests: `ReportRowUdsPlusDbTests.Plan_IsWhatRunDoes`; `Migration26_1Tests.UdsPlusPlanLines_SayWhatTheStepChanges`; negative controls in the rehearsal tests (a UDS+ row with another value, a moved row put back, an extra Reports grant). Changed expectations: the rehearsal tests (M5, the added row and grant) and SyntheticData's `displayreport` rule test (whole table; a table without a rule is still caught). No test was removed.
- Question 1 above still stands: every package level (2027002301, 2027003501, ...) sorts after the dated 2026 levels, so a step written later with a 2026 date would not run on a database that already has a package level.
