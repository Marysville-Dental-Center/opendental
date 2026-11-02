# Port note: PRACTICE-NEUTRAL (no practice-specific names in source control)

Sweep on branch `pn/sweep-1` from `main` (c48e3f386). The owner asked to strip anything practice specific from source control: the
repository is public and must read as a general open-source project. No tracked file may name the practice this fork was first written for
or its town, the practice's servers, their addresses or platform, or folders of the developer's computer. The practice's own data was never
in the repository (synthetic data only), so this sweep changes wording, examples and defaults, never data.

This note gives the files and counts only; it does not repeat the names.

## What was found

Lines per file on `main` before the sweep (`git grep -c`, case-insensitive, each name in its hyphen, underscore, space and path forms):

| File | Server host name | Server address | Practice name or town | Developer PC path | Server platform | Town's ZIP code |
|---|---:|---:|---:|---:|---:|---:|
| `OpenPmsDentalBusiness/Misc/ForkMigrations/Adoption.cs` | 1 |  |  |  |  |  |
| `OpenPmsDentalBusiness/Misc/ForkMigrations/SchemaVerifier.cs` | 1 |  |  |  |  |  |
| `README.md` |  |  | 1 |  |  |  |
| `docs/adoption-runbook.md` | 25 |  |  |  | 3 |  |
| `docs/decoupling-plan.md` | 4 |  |  | 1 |  |  |
| `docs/m1-validation-report.md` | 9 |  |  | 1 | 2 |  |
| `docs/owner-questions.md` |  |  | 1 |  |  |  |
| `docs/port-notes/M2-01.md` | 1 |  |  | 1 |  |  |
| `docs/port-notes/REL-FILES.md` |  |  | 1 |  |  |  |
| `docs/release-process.md` | 1 |  |  |  |  |  |
| `docs/reviews/2026-11-01-release-readiness.md` |  |  | 1 |  | 1 |  |
| `docs/specs/features-24.4.md` | 3 |  |  | 1 |  |  |
| `docs/specs/features-25.1.md` | 3 |  |  | 2 |  |  |
| `docs/specs/features-25.2.md` | 14 |  |  | 1 |  |  |
| `docs/specs/features-25.3.md` | 6 |  |  | 1 |  |  |
| `docs/specs/features-25.4.md` | 1 |  |  | 1 |  |  |
| `docs/specs/features-26.1.md` | 2 |  |  | 2 |  |  |
| `docs/specs/port-backlog.md` | 3 |  |  |  |  |  |
| `docs/specs/schema-changes-24.3-to-26.1.md` | 3 |  |  | 2 |  |  |
| `docs/specs/schema-changes.json` | 69 |  |  |  |  |  |
| `docs/specs/vendor-direct/README.md` | 6 |  |  |  |  |  |
| `docs/specs/vendor-direct/canada.md` | 3 |  |  |  |  |  |
| `docs/specs/vendor-direct/carecredit.md` | 4 |  |  | 2 |  |  |
| `docs/specs/vendor-direct/dosespot.md` |  |  |  | 1 |  |  |
| `docs/specs/vendor-direct/dropbox.md` | 2 |  |  |  |  |  |
| `docs/specs/vendor-direct/edgeexpress.md` |  |  |  | 1 |  |  |
| `docs/specs/vendor-direct/google.md` | 2 |  |  |  |  |  |
| `docs/specs/vendor-direct/microsoft-365.md` | 1 |  |  |  |  |  |
| `docs/specs/vendor-direct/newcrop.md` | 1 |  |  | 2 |  |  |
| `docs/specs/vendor-direct/payconnect.md` | 1 |  |  | 2 |  |  |
| `docs/specs/vendor-direct/paysimple.md` | 1 |  |  | 2 |  |  |
| `docs/specs/vendor-direct/pdmp.md` |  |  |  | 1 |  |  |
| `docs/specs/vendor-direct/postcardmania.md` | 2 |  |  | 1 |  |  |
| `docs/specs/vendor-direct/quickbooks-online.md` | 3 |  |  | 1 |  |  |
| `docs/specs/vendor-direct/usps.md` | 3 |  |  | 1 |  |  |
| `tests/ForkMigrationTests/ReferenceSnapshotTests.cs` | 1 |  |  | 1 |  |  |
| `tests/ReleaseFlowTests/TestSupport.cs` | 1 |  |  |  |  |  |
| `tests/VendorDirectTests/Foundation/VendorHttpTests.cs` |  |  |  |  |  | 2 |
| `tests/VendorDirectTests/PostcardMania/PostcardManiaRecipientTests.cs` |  |  | 1 |  |  | 1 |
| `tests/VendorDirectTests/PostcardMania/PostcardManiaSerializationTests.cs` |  |  | 1 |  |  | 2 |
| `tests/VendorDirectTests/PostcardMania/PostcardManiaTestHelper.cs` |  |  | 1 |  |  | 1 |
| `tools/AdoptionValidation/AdoptionValidation.Tests/RunbookToolsTests.cs` | 2 | 2 |  |  |  |  |
| `tools/AdoptionValidation/AdoptionValidation.Tests/TcpRelay.cs` | 1 |  |  |  |  |  |
| `tools/SyntheticData/Generation/PracticeWideGenerator.cs` | 1 |  |  |  |  |  |
| `tools/SyntheticData/Infrastructure/SchemaBuilder.cs` | 2 |  |  | 1 |  |  |
| `tools/SyntheticData/Invoke-SyntheticData.ps1` | 1 |  |  |  |  |  |
| `tools/SyntheticData/Options.cs` | 1 |  |  | 1 |  |  |
| `tools/SyntheticData/README.md` | 2 |  |  | 1 |  |  |
| `tools/SyntheticData/SyntheticData.Tests/UnitTests.cs` | 3 |  |  |  |  |  |
| `tools/SyntheticData/Validation/Coverage.cs` | 1 |  |  |  |  |  |
| **Total lines** | 191 | 2 | 7 | 31 | 6 | 6 |

"Developer PC path" covers the folder of schema dumps, diffs and the HQ vendor table, and two references to the repository's own folder on
the developer's PC. "Server platform" is the exact distribution and MariaDB package version of the office server. Besides these, eight lines
name the practice only inside this repository's GitHub address (`docs/release-notes/v0.0.md` 3, `installer/OpenPmsDental.iss` 1,
`installer/setup-info.txt` 1, `tests/ReleaseFlowTests/InstallerTests.cs` 3). They are kept: the GPL needs the source address.

## What changed

- **Adoption runbook.** The server is `<db-server>`, its address `<db-server-ip>` where a workstation's `FreeDentalConfig.xml` names it by
  address; the opening section says that names in angle brackets are the office's own values, and "Names used below" lists both. The
  platform reads "MariaDB 10.11 on Linux, installed from a Debian or Ubuntu package"; step 0.0.1 expects a Debian or Ubuntu release and a
  `10.11.x-MariaDB-` version with its package suffix, and the checklist row matches. Every command is unchanged apart from the placeholder.
- **Specs, plan, validation report, reviews and port notes.** Only the names changed: "the office database", "the office schema dump (a
  schema-only dump of an Open Dental 25.3.48 office database, kept outside the repository)", "the HQ vendor table (kept outside the
  repository)", the diff by its file name only. `schema-changes.json` gives its source as "office 25.3.48 dump"; no code reads that field.
- **README and owner questions.** The opening sentence calls this a fork "written for one dental practice and published for anyone to use";
  the `AssemblyCompany` question offers "the practice's name".
- **Tests.** The refused non-local hosts are `db.example.test` and `192.0.2.10` (a documentation address, RFC 5737), and the refused
  database name `office-db`, so each test keeps its meaning. The PostcardMania and vendor HTTP tests use Springfield, IL 62701.
- **Default dump path.** `SchemaBuilder.DefaultSchemaSqlPath` (SyntheticData, also AdoptionValidation's rehearsal) named a file on the
  developer's PC. It is now the path in the `SYNTH_SCHEMA_SQL` environment variable, and null when that is not set, so the tools fall back to
  the local database `od_253_schema`, as they did where the file was missing. `ReferenceSnapshotTests` reads only
  `FORKMIGRATION_REFERENCE_DUMP`; without it its two dump tests are inconclusive, as they were where the file was missing. **On the
  developer's PC, set both variables to the dump's path** to keep those checks; `--schema-sql` still overrides the tools' default.

Kept, because they are not this practice's: Open Dental's own example addresses (for example in the HL7 windows), the dictionary's ordinary
words, the EhrLab enum member `MDC`, and the checkout paths of Open Dental's developers in the old `UnitTests` project.

## The guard

`NoHqTests/PracticeNeutralityTests.cs` scans every tracked text file (`git ls-files`; binary files are skipped by extension, and by a NUL byte
unless the file starts with a UTF-16 byte order mark) for the names, as `tests/ImagingCaptureTests/VendorNeutralityTests.cs` does for
vendor names: the deny-list holds SHA-256 hashes of the names lowercased and stripped of everything but letters and digits, so the test
names nothing. Each line is split into words, camelCase parts and letter or digit runs, and every run of up to five parts joined inside a
word or by one space, tab, `-`, `_`, `.`, `/`, `\` or `:` is compared. The developer's folder name is not matched across a space, because its
words are ordinary prose. One allowlist entry, also a hash, permits exactly this repository's GitHub address (`https://github.com/` with the
owner and repository name, with or without `.git`); the same owner anywhere else is still a hit. A second test proves that the scanner
finds made-up names hyphenated, spaced, in camelCase, in Windows and Unix paths, in URLs, in host names and as an address, and that it
allows the made-up repository address and nothing more. To add a name, the test's summary gives the PowerShell line that prints its hash
and length.

## Not changed: what remains in git history

The sweep changes the working tree only. Counted on `main` before it (1,242 commits; `git log -G`, `--grep` and the author fields, the
names not printed):

- Older file versions: 20 commits add or remove a line with the server's host name, 1 with its address, 8 with the practice's name or town
  and 7 with the developer's folder name.
- Commit messages: 3 name the server; none names the practice, its town, the address or the folder.
- Authors: no author or committer name or e-mail domain contains these names, but the fork's own 1,197 commits (2026-10-18 to 2026-11-01)
  use one e-mail address on a domain that is not Open Dental's and may identify the practice.
- Branch and tag names: none of the 364 refs contains the names.

Rewriting history (for example with `git filter-repo`) would change every commit id of the public repository; that is the owner's decision.
