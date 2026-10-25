# SyntheticData

Fills a copy of the real Open Dental 25.3.48 schema with realistic, entirely fictional practice data, and records validation
reports (row counts, per-table and per-column checksums, business totals) so the adoption rehearsal (M1-Z in
[docs/specs/port-backlog.md](../../docs/specs/port-backlog.md)) can compare a database before and after adoption.

The owner decided that there is no office census and no copy of office data (port backlog, "Owner decisions (2026-10-18)").
This tool is the stand-in: it gives M1-00 and M1-Z a database with the exact 25.3.48 shape, a plausible practice in it, and
at least one row for every table and column that is new or changed between 24.3 and 25.3.48.

## Safety

- **Local only.** The tool connects only to `localhost`, `127.0.0.1` or `::1`. Any other `--server` is refused before a
  connection is attempted; there is no override. Outbound HTTP through the .NET web stack is routed to a dead local proxy, and an
  attempt makes the run fail with exit code 3. The office server (`fox-db`) can never be reached from this tool.
- **Writes only `synth*` databases.** `generate` and `schema` refuse any database name that does not start with `synth`
  (lower-case letters, digits and underscores), and refuse an existing database unless `--force` is given. `validate`,
  `coverage` and `--config-from` only read.
- **No real people.** Surnames are compounds of nature words and place endings ("Brindlewick", "Quorvale"), given names are built
  from syllables, and both are filtered against a list of common real names (`NameBank.SetCommonRealNames`, also checked by the
  tests). Phone numbers use the 555-0100 to 555-0199 block reserved for fiction, e-mail addresses use `example.com`,
  `example.net` and `example.org`, SSNs are blank, card numbers are masked (`XXXXXXXXXXXX1234`) with no processor tokens.
- **No proprietary content.** Procedure codes are invented `Z` codes (`Z0120`, `Z2392`, ...) with plain descriptions written for
  this tool; no ADA CDT code or text is used. Enum numbers and preference names come from Open Dental's published database
  documentation (via [docs/specs](../../docs/specs)); nothing was decompiled.
- `SecurityHash` columns hold placeholder values (a SHA-256 of a fixed text), not Open Dental hashes. Adoption rehashes every row
  anyway (risk R2, item 6), so only their presence matters.

## Quick start

Requirements: MariaDB or MySQL on this machine (`127.0.0.1:3306`, user `root`; a password, if any, goes in the
`SYNTH_DB_PASSWORD` environment variable), and the schema source: `D:\Dev\opendental-schema\fox-db-schema.sql` (25.3.48 DDL,
schema only) or, failing that, the local empty database `od_253_schema`.

```powershell
# Build and generate synth_253 (small preset), writing the validation report:
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Generate -Force -Out synth_253.small.json

# Medium preset with another seed, into another database:
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Generate -Database synth_253_m -Size medium -Seed 42 -Force

# Re-check an existing database:
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Validate -Database synth_253 -Out report.json -Strict
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Coverage -Database synth_253
```

Or run the program directly (`tools\SyntheticData\bin\Debug\SyntheticData.exe --help`):

| Command | What it does |
|---|---|
| `generate` | Creates the database from the 25.3.48 schema (`--force` drops it first), verifies it against `od_253_schema` when that exists, fills it, then runs `coverage` and `validate`. Exit code 4 if a consistency check or the coverage fails. |
| `schema` | Only creates the empty schema copy. |
| `validate` | Writes the report (`--out`). Read-only. `--strict` fails (exit 4) when a `check.*` total is not 0. |
| `coverage` | Checks that every 24.4-25.3 table and column holds data (exit 4 if not). |
| `compare A.json B.json [--allowed FILE]` | Lists the differences between two reports. Exit 0 when every difference is allowed, 5 otherwise. |

Options: `--size small|medium|large`, `--seed N` (default 20261018), `--as-of yyyy-MM-dd` (default 2026-09-30), `--families N`
and `--years N` (override the preset), `--config-from DB`, `--fill-remaining N`, `--schema-sql PATH`, `--schema-from DB`,
`--server`, `--port`, `--user`, `--database` (default `synth_253`).

## Sizes and determinism

| Preset | Families (about 2.3 patients each) | History | Rows | Time on a dev PC |
|---|---|---|---|---|
| small | 40 | 1 year | about 11,000 | about 15 s |
| medium | 600 | 2 years | about 100,000 | about 30 s |
| large | 4,000 | 3 years | about 770,000 | about 5 min |

Providers scale with the size (at least 2 dentists and 2 hygienists, one operatory each), as do carriers and expenses.

The same seed, size and as-of date always produce the same database, byte for byte in every checksum: the generator uses its own
SplitMix64 random numbers (not `System.Random`), hands out primary keys itself instead of relying on `AUTO_INCREMENT`, writes
every `timestamp` column explicitly (the server never fills `current_timestamp()`), and runs every session in UTC with strict SQL
mode. Each family draws from its own random stream (`family:N`), so families are independent of each other. The tests generate
the small preset twice and require identical reports.

The schema is created from the dump with every table's `AUTO_INCREMENT=` counter removed, so nothing about the office's row
counts carries over. The copy is compared with `od_253_schema` (tables, columns, types, nullability, defaults, positions) and
must match exactly. The generator never alters a table.

## What is generated

All of it is computed from the history window (`--years` before the as-of date) plus a 90-day booking window after it. Nothing is
completed, paid or posted after the as-of date.

**Practice set-up.** Preferences (`DataBaseVersion` and `ProgramVersion` = 25.3.48.0, practice address, `ApptMirrorSecondary`,
and a `SyntheticDataInfo` row recording seed, size and as-of date), definitions (adjustment, payment, billing, image, blockout,
commlog, task priority and the 25.2 task categories), user groups with permissions (including 264-271), users (one with a
domain login), employees, providers, operatories, procedure codes, fee schedules (standard, two PPO networks, copay, out of
network), coverage categories and spans, code groups, carriers, employers, insurance plans (one in five without a group number,
which `insplan.GroupNum` already allowed in 24.3, and one in five with a 26 to 50 character group number, which needs the 25.2
widening from 25 to 50 characters), benefits, recall types, appointment views, sheet and eForm definitions, medications,
allergies, problems, referrals, task lists, payment plan templates, clearinghouses, workstations, accounts.

**Schedules.** Working days without holidays (holidays are practice notes), one provider schedule per provider per day linked to
the provider's operatory, lunch blockouts, emergency holds and staff meetings.

**Families.** One to five members: guarantor, spouse, children, sometimes a non-patient guarantor. Statuses: patient, inactive,
archived, deceased, deleted, prospective. Two thirds are insured, some with dual coverage. Each family has a payment habit (prompt,
slow, non-payer) that drives its balance.

**Clinical.** Recall visits every six months (four for perio patients) booked into free schedule slots: exams, bitewings yearly,
cleanings, fluoride and sealants for children, full x-rays for new patients. Exams find fillings, crowns with build-ups, root
canals, extractions, implants, deep cleanings, dentures, night guards, whitening and orthodontics. Most treatment is scheduled
and completed; the rest stays treatment-planned (with insurance estimates, a planned appointment and an active treatment plan) or
is booked after the as-of date. Broken appointments, emergency visits, unscheduled-list recalls, procedure notes, commlogs,
appointment history and security-log entries are included. Existing work comes in as existing-other procedures, with
insurance-history claimprocs for some new patients.

**Insurance.** One claim per visit and plan: PPO write-offs, coverage percentages by category, deductibles waived for preventive
care, annual maximums, frequency limits, age limits, implant exclusions, crown waiting periods and exclusions limited to teeth
(surgical removal of third molars; core build-ups on teeth 4 to 13 on every third plan), which the estimates honor. Claims are
unsent, sent or received according to their age; received ones are paid (now and then denied, reduced or followed by a
supplemental payment) on insurance checks batched by carrier and date. Secondary claims are held until the primary pays. Major
treatment gets preauthorizations.

**Money.** Payments at checkout and later (by payment habit), split per procedure; prepayments; senior, courtesy and invoice
discounts; monthly finance charges and the collections billing type for non-payers; payment plans; monthly statements while a
family owes money, receipts and invoices; weekly bank deposits; the accounting ledger (deposits against patient and insurance
income, monthly expense checks) with monthly reconciliations; the monthly A/R snapshot (`dashboardar`). Patient aging columns
(`Bal_0_30` ... `BalTotal`, `InsEst`, `EstBalance`, `PayPlanDue`) are computed from the family ledger, oldest charges paid first.

### The 24.4 to 25.3 tables and columns

Every entry of [schema-changes.json](../../docs/specs/schema-changes.json) with range 24.3-25.3 holds data (`coverage`, and
`Validation/Coverage.cs`, which a test keeps in step with the JSON file).

Holding data is not enough for the ten columns those versions **widened** (the `alter` entries): a value that fits the old width
would survive a step that wrongly redefines the column at the old width. So `coverage` also requires each widened column's longest
value to exceed its old width, and prints the longest value of each:

| Column | Old width | Long values |
|---|---|---|
| `insplan.GroupNum` | varchar(25), 50 since 25.2 | Employer-style group numbers of 31 to 37 characters on one plan in five (one in five has none) |
| `histappointment.ProcDescript` | varchar(255), text since 25.2 | The showcase family's planned restorative appointment (24 fillings) and its history row |
| `eformfield`/`eformfielddef` `PickListVis`, `PickListDb` | varchar(255), text since 25.3 | The medical history's "last dental visit" question, a 338-character pick list |
| `eformfield`/`eformfielddef` `ConditionalValue` | varchar(255), text since 25.3 | Its follow-up text box, shown for five of the six answers (a 276-character trigger list) |
| `emailmessage.FailReason` | varchar(255), text since 25.3 | Full delivery reports on the showcase family's first bounce and about one bounce in four |
| `claimpayment.Note` | varchar(255) | Remittance text on every fifth EFT, filled to exactly 255 characters (see below) |

`claimpayment.Note` is the exception: Open Dental's 25.3 documentation makes it `text`, but the office's 25.3.48 database (the
reference schema) still has `varchar(255)` (the DRIFT entry in the JSON file), so nothing longer can be stored before adoption.
Coverage requires the old width to be filled exactly, which still catches a step that narrows the column.

The data by version:

| Version | Data |
|---|---|
| 24.4 | Dated fees (yearly increases, a PPO contract change, an increase dated after the as-of date that must not apply yet, a provider's own fees) and fee schedule notes; chart letters (`document.ChartLetterStatus`, `UserNum`, `ChartLetterHash`, some with a second version); payment plans and templates with a chosen sheet (`SheetDefNum`); BetterDiagnostics drawings (`imagedraw.ImageAnnotVendor` 2, `BetterDiagLayer`); Query Monitor filters |
| 25.1 | Code group frequency flag; card terminal per workstation; card-holder names; eForm field AutoImport and PrefillFromGuar; insurance verification hashes; hidden medications still on patients; down-payment charges on payment plans; guardian relationships in the renumbered form (Mother 1, Father 6, Sitter 23) |
| 25.2 | Mirrored appointments (hygiene visits with a dentist exam, from September 2025) and their history rows; the view setting; English copies of translated eForm labels and pick lists (some forms in Spanish); eForm Patient List (8) and Ins Pick (9) fields with pending insurance (`inspending`); EOBs on preauthorizations (`eobattach.ClaimNumPreAuth`); XConnect flag; eClipboard sheets considered; `inssub.SecurityHash`; signer of practice signature boxes (`sheetfield.UserSigned`); statements remembering "show transactions since zero balance"; task categories; branding rows |
| 25.3 | Tooth-range exclusions (on Exclusions rows only, stored expanded); Other Benefits code groups; accounting payees, notes and review flags; ASAP texts with the sending user; statements in API mode (4) and invoices whose `InsEst` holds the adjustment total; Check sheets (type 28); e-mail failure reasons (text since 25.3); task triage positions (an HQ column; a few rows prove it survives) |
| 25.3, excluded features | Chat (`chat`, `chatmsg` with `SeqCount`, `chatattach`, `chatreaction`, `chatuserattach`, `chatuserod`, `sequencecounter`) and dental school (`schoolcoursedef`, `schoolcoursesched`, `schoolapproval`, the new `schoolcourse`, `reqneeded` and `reqstudent` columns, hidden student and instructor providers): a few rows each, so the rehearsal proves they survive untouched |
| Office only | `patient._portal_active_email` (generated column with a unique key): active patients have unique e-mail addresses, and some inactive family members share the guarantor's address, which the key allows |

Family 0 is the **showcase family**: its random choices are steered (always insured with a PPO plan, a pre-authorized crown on a
payment plan with a down payment and a chosen sheet, a signed consent, a chart letter, eForms with Ins Pick and pending
insurance, a categorized task, a BetterDiagnostics drawing, a hidden medication, a card on file, a bounced statement e-mail with
its full delivery report, a planned restorative appointment with a long procedure description), so
coverage is complete for every seed and size, not just the default one. The data stays consistent; only the dice are loaded.

### Values that are not published

Where the public documentation names a value without a number, the generator uses a documented assumption. Census and adoption
work must not treat these as facts:

| Column | Value used | Status |
|---|---|---|
| `document.ChartLetterStatus` | 1 for every chart letter | Numbers not published (features-24.4, open point 5) |
| `chatuserod.UserStatus` | 0 Available, 1 Away, 2 DoNotDisturb | Names published, numbers assumed |
| `chatmsg.EventType` | 0 message, 1 for the "chat created" event | Event numbers not published |
| `schoolapproval.SignOffStatus` | 0, 1, 2 | Not published (M2) |
| `branding.BrandingType` | 0 name, 1 logo path, 2 colours | 1 is LogoFilePath in 25.2 and LogoImage in 26.1 |
| `refattach.RefToStatus`, `asapcomm.SmsSendStatus` | 3 | Existing enums; values chosen to look plausible |

### Stored formats

The rehearsal and the A-INS and A-EF parsers must meet only values Open Dental 25.3.48 itself stores, so these columns follow the
formats the published database docs give (as recorded in the feature specs) and the fork's own eForm classes:

| Column | Stored form | Source |
|---|---|---|
| `benefit.ToothRange` | Internal tooth numbers (or primary letters), comma-separated, ranges expanded, no hyphens: an entered "4-13" is stored as `4,5,6,7,8,9,10,11,12,13`. Only on Exclusions rows (`BenefitType` 4); blank or NULL elsewhere, since tooth ranges on age limitations arrive in 26.1 | features-25.3 (v25_3-family-exclusions-tooth, Rules); `benefit.ToothRange` in the 25.3 database docs |
| `eformfield`/`eformfielddef` `PickListVis`, `PickListDb` | Pipe-delimited, one `PickListDb` item per visible item, at least two (`Male\|Female\|Unknown` and `M\|F\|U`). Translated forms translate `PickListVis` item by item and keep the English list in `PickListVisEnglish` | `EFormField.cs` (PickListVis, PickListDb) |
| `ConditionalValue` | Several trigger values are pipe-delimited; each matches an item of the parent's visible pick list, or "X" for a checkbox | `EFormField.cs` (ConditionalValue) |
| Ins Pick (FieldType 9) | `PickListVis` = the three option labels, `None\|Existing\|New`; `PickListDb` unused (blank). `ValueString` = "" (None), `New`, `InsSubNum:n` (the patient's own subscription) or `InsPendingNum:n` (a family member's pending row, which import copies for this patient). New reveals the Patient List and the `ins1CarrierName`, `ins1GroupNum` and `ins1SubscriberID` text fields through conditional logic (parent = the Ins Pick label, value = `New`). Import creates `inspending` rows, never plans | features-25.2 (v25_2-eforms-ins-pick-field, "Field data") from the 25.2 database docs |
| Patient List (FieldType 8) | `ValueLabel` = JSON `EFormPatListLayout` (`{"Label":"Subscriber"}`); `ValueString` = the picked subscriber's PatNum as text, or "" | features-25.2 (v25_2-eforms-patient-list-field, "Data") from the 25.2 database docs |
| MedicationList (FieldType 7) | `ValueLabel` = JSON `EFormMedListLayout` (Title and the defaults); `ValueString` = JSON list of `EFormMed` (`[{"MedName":"Ibuprofen","StrengthFreq":""}]`, `[]` when empty) | `EFormField.cs` (ValueLabel, ValueString, EFormMed, EFormMedListLayout) |

Assumptions where the docs are silent: the Ins Pick slot is taken from `DbLink` `ins1` (the convention is not documented; see
features-25.2, open question 2); `EFormPatListLayout` is written with only the `Label` property the docs mention; for the two JSON
label types, `ValueLabelEnglish` holds the same JSON with the English label.

## Validation reports

`validate` writes a JSON report:

- `Tables`: per table the row count, `CHECKSUM TABLE ... EXTENDED`, and a checksum per column: `SUM(CRC32(primary key | value))`.
  The per-column checksum ignores the physical row format and the other columns, so dropping a column or rewriting `SecurityHash`
  leaves every other column's checksum unchanged.
- `Totals`: the named queries in [sql/business-totals.sql](sql/business-totals.sql), as of the report's date (`--as-of`, else the
  date in `SyntheticDataInfo`, else 2026-09-30): patients by status, gross and net production (by year and provider),
  adjustments, write-offs, patient and insurance income, deposits, stored aging buckets and the ledger total, outstanding and
  treatment-plan insurance estimates, claims by type and status, payment-plan debits, credits, payments and balance, appointments
  by status, mirrored appointments, dated fees and the as-of fee total per schedule, benefit counts, accounting totals, statement
  modes and flags, and feature counts. A query that fails (for example after a column was dropped) is recorded as `error: ...`.
- `check.*` totals count rows that break an invariant of the generated data (0 = pass): aging buckets add up, family ledger equals
  `BalTotal`, `InsEst` equals the outstanding claim estimates, payments equal their splits, insurance checks equal their claimprocs,
  deposits equal their items, journal transactions balance, claim fees equal their procedures, active e-mails are unique, no
  orphaned claimprocs, procedures or splits, and the stored formats below hold (`check.benefit_tooth_range_format`,
  `check.eform_pick_lists`, `check.eform_value_formats`).

Two reports of the same data are identical: nothing in them depends on time or machine except the server version.

### The ledger model

Patient balances follow Open Dental's model with payment plans "aging credits and debits" (version 1):

    balance = completed procedure fees + adjustments + payment-plan debits due
            - patient payments (all splits, including prepayments and plan payments)
            - insurance paid (received and supplemental claimprocs)
            - write-offs (on claims, received or not)
            - payment-plan credits (static plans) and linked production (dynamic plans, AmountOverride)

`check.ledger_matches_baltotal` recomputes this per family. On a real office database some `check.*` totals may legitimately be
non-zero; they describe the synthetic data.

## The adoption rehearsal (M1-Z)

1. `generate` into `synth_253` and keep the report (`--out before.json`). Optionally `--config-from` a fresh 24.3.63 dev database
   (from `tools\New-DevDatabase.ps1`) first, which copies its preference, definition, user, permission, program, report and claim
   form rows (shared columns only), so the fork can open the database.
2. Copy the database (for example to `synth_253_adopted`) and run the adoption tool on the copy.
3. `validate --database synth_253_adopted --out after.json`.
4. `compare before.json after.json --allowed allowed-differences.adoption.json`. The allowed differences are the ones adoption is
   meant to make: `patient._portal_active_email` and its unique key dropped (owner decision), `SecurityHash` rehashed, preference
   rows (fork markers, version sentinel), Open Dental update files, adoption's own security-log rows. Everything else, for
   example a second guardian renumbering, a `ShowTransSinceBalZero` back-fill or a repeated permission grant, is reported as
   unexpected and the exit code is 5.

Note for the adoption tool: many tables have a `timestamp ... ON UPDATE current_timestamp()` column (`SecDateTEdit`,
`DateTStamp`). An `UPDATE` that rehashes `SecurityHash` without also writing `SecDateTEdit=SecDateTEdit` moves those audit
timestamps to the adoption time, and `compare` reports it (the database tests show both cases). The allowed list deliberately
does not excuse it.

`--fill-remaining N` adds N schema-valid filler rows to every table that is still empty (except tables whose rows would change
start-up behaviour, such as `documentmisc`, `updatehistory` and `preference`), so the checksums cover all 422 tables. Filler rows
are not meaningful (foreign keys point nowhere), which is why the option is off by default.

## Tests

`tools/SyntheticData/SyntheticData.Tests` (in `OpenDental.sln` and `OpenDental.Local.slnf`):

```
vstest.console.exe tools\SyntheticData\SyntheticData.Tests\bin\Debug\SyntheticData.Tests.dll /Platform:x86
```

Unit tests cover the random numbers (pinned sequence), the local-only and `synth*` guards, names and contact ranges, SQL literal
formatting, dump parsing, aging, the report comparer, the coverage list and the widened-column list (old widths, DRIFT entries)
against `schema-changes.json`, the lengths of the generator's long values, and the stored formats (tooth ranges only on
Exclusions and expanded, pipe-delimited pick lists with matching item counts, conditional values that match their parent, Ins Pick
values, and JSON that deserializes into OpenDentBusiness's `EFormMedListLayout` and `EFormMed`); enum numbers that
existed in 24.3 are cross-checked against OpenDentBusiness. The database tests (scratch databases `synth_test_*` with a per-run
suffix, so concurrent runs never collide, dropped afterwards; inconclusive without a local server) check the schema against `od_253_schema`, determinism, all consistency checks,
coverage for two seeds, fictional data, dated fees on procedures, the stored formats of the generated rows, guardian numbering,
the filler, refusal to write non-`synth` databases, and a simulated adoption that must show only allowed differences until a
forbidden change is made.

## Layout

| Path | Contents |
|---|---|
| `Program.cs`, `Options.cs` | Command line |
| `Infrastructure/` | Local-only guard, connection, schema reader and builder, buffered row sink, random numbers |
| `Generation/` | Settings, names, catalog (codes, coverage categories, code groups), practice set-up, schedules, the family simulator (clinical, ledger, extras), eForm value shapes, practice-wide rows, filler |
| `Validation/` | Report, validator, comparer, coverage list |
| `sql/business-totals.sql` | Business totals and checks |
| `allowed-differences.adoption.json` | Differences adoption may make |
| `Invoke-SyntheticData.ps1` | Build-and-run wrapper |

## Limitations

- No clinics: every row has `ClinicNum` 0. Medical claims, Canadian fields, orthodontic auto-billing, insurance payment plans,
  e-services, eRx, labs, perio charts, mounts and wiki pages are not generated (except with `--fill-remaining`).
- Without `--config-from`, the database has only the preference rows listed above; the fork or Open Dental need the full set to
  start.
- The ledger model, estimates and timing are simplified on purpose; they are realistic, not Open Dental's exact algorithms.
- An as-of date before 2026 makes some 25.x feature dates fall outside the window; coverage is guaranteed for as-of dates from
  2026-01-01.
