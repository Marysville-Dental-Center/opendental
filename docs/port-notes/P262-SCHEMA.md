# Port note: P262-SCHEMA, schema foundation Open Dental 26.1.70 to 26.2.0

Branch `p262/schema-1`, cut from `main` (2b92cfb3e). The first package of the Open Dental 26.2 port (owner decision of 2026-11-07); the nine
feature packages build on it. Schema, enum values, table classes, CRUD and the migration step only: no feature is built here.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- `OpenDentalDiffDocumentation26-2.xml` (version 26.2.0.0): the changes since 26.1. 17 tables (3 new: `glidewellacct`, `glidewellmsg`,
  `labcaseproduct`), 40 columns (29 new, 11 listed for an enum change only), 23 enum values in 13 enumerations, no deletion.
- `OpenDentalDocumentation26-2.xml` (version 26.2.0.0): the full 26.2 schema. Used to check the result: 430 tables, and for every table the
  same column names in the same order with the same types, after the documentation tool's own type normalization (as M2-01 did for 26.1).
  It also confirmed that the diff document is complete against our 26.1.70 reference: the full document differs from it in exactly the diff
  document's tables and columns.
- The 26.2 release notes (https://www.opendental.com/site/version26_2.html): 26.2 was released as stable on 2026-11-03; no 26.2.x patch
  version is named.

The documents are not in the repository. `tests/P262SchemaTests` checks the reference, the full migration chain and the enum values against
them when `P262_OD_DOCS_DIR` names a folder that holds both files; without it those four tests are inconclusive.

## What was built

### Reference schema

- `OpenPmsDentalBusiness/Misc/ForkMigrations/Reference/OdSchema-26.2.0.sql` (new, embedded): the 26.1.70 reference plus the 26.2 changes, in
  the same normalized form. Every table 26.2 changes or adds has a comment above it that names each change and says what is `INFERRED`; the
  25.4 and 26.1 comments are kept.
- `SchemaVerifier`: the default reference is now 26.2.0 (`ReferenceVersion` "26.2.0", `GetReference()`, `VerifyDatabase()`, `OdSchemaCompat`
  after a match). 26.1.70 stays embedded under its own names (`GetReference26_1_70()`, new `VerifyDatabase26_1_70()`), like 25.3.48. The
  documented exceptions are unchanged (26.2 drops no table).
- `tools/New-OdReferenceDatabase.ps1`: `-Version 26.2.0` (now the default) loads `od_262_schema` (430 tables); 26.1.70 and 25.3.48 as before.

### Migration step (`OpenPmsDentalBusiness/Misc/ForkMigrations/OdParity26_2.cs`, new)

| Level | Step | What |
|---|---|---|
| 2026110701 | `OdParity26_2Schema` ("Open Dental 26.2 schema") | `apisubscription.DateTimeLastFailure`, `FailureReason`, `SubsequentFailures`, `DateTimeNextRetry`; `insbluebook.DateTStamp`; `provider.ClaimFormNum` and `GlidewellAcctNum` with a key each (one `ALTER TABLE`); new tables `glidewellacct`, `glidewellmsg`, `labcaseproduct`. |

- Levels 2026110702 to 2026110709 stay free for 26.2 schema corrections. The step runs after every 26.1 step (2026102401 to 2026102403).
- Idempotent: built from `OdParity26_1.AlterTable` (missing columns and keys only, one statement per table) and `CreateTableIfMissing`. On a
  26.1.70 database it gives exactly the 26.2.0 reference; a second run changes nothing; a table that already has some of the columns is
  finished; a database already at 26.2.0 is not touched.
- No data migration. Existing rows hold 0, empty or `0001-01-01 00:00:00` in the new columns, except `insbluebook.DateTStamp` (below).

### Table classes and CRUD

- New: `GlidewellAcct`, `GlidewellMsg` (+ `EnumGlidewellOrderStatus`), `LabCaseProduct` (+ `EnumGlidewellProductName`).
- Changed: `ApiSubscription` (four columns; a constructor sets `FailureReason` to empty), `InsBlueBook` (`DateTStamp`, `TimeStamp`: set by the
  database only), `Provider` (`ClaimFormNum`, `GlidewellAcctNum`).
- CRUD generated with `xCrudGenerator` (`CrudGenerator.exe crud=GlidewellAcct,GlidewellMsg,LabCaseProduct,ApiSubscription,InsBlueBook,Provider`);
  none hand-edited. No data interface (S class) for the new tables: their features are separate packages.
- Data plumbing: `InsBlueBooks.SynchForClaimNums` builds its rows with an explicit column list and reads them through the CRUD, which now reads
  `DateTStamp`; the query selects `insbluebook.DateTStamp` too (NULL for a new row; the sync never writes it). Without this, receiving a claim
  with blue book data would fail.

### Enum values (Open Dental's numbers)

| Enum | Value | Member | Here |
|---|---|---|---|
| `AlertType` | 52, 53, 54 | `MariaDbFullBackupError`, `MariaDbIncrementalBackupError`, `EConnectorCritical` | Reserved (`ReservedEnumValue`): raised by the Open Dental Service's MariaDB backups and the eConnector, which this program does not have; hidden from alert category setup like 46 to 51. No alert category links are written (as for 46 to 51). |
| `EnumAutomationTrigger` | 11 | `DoNotBillInsuranceCheck` | For the 26.2 claims package (item 22). |
| `AutoCondField` | 15 | `CarriersMatch` | For the claims package; description "Carriers Match". Contiguous 0 to 15 (the condition window lists by index). |
| `DefCat` | 57 | `CommLogReferralTypes` | For the 26.2 misc package (item 39); description "Commlog Referral Types". Contiguous 0 to 57 (the definition cache indexes by category). |
| `DefLinkType` | 6 | `User` | Reserved: Open Dental HQ only (task category to user). |
| `EnumPermType` | 277 | `AdvancedTextingAccountEdit` | `IsODHQ`: Open Dental HQ only, so hidden from the security tree and the audit trail filter, never granted by Set All. |
| `ToothPaintingType` | 18 | `TextBelow` | Defined so that procedure codes written by Open Dental load and Database Maintenance's paint type check no longer resets them to None. The tooth chart draws nothing for it; no 26.2 package ports it. |
| `TsiFKeyType` | 6 | `PayPlanLink` | Defined so that Transworld log rows written by Open Dental load; this program's Transworld code does not write it. |
| `UserOdFkeyType` | 31 | `PerioShowCurrentProbingOnly` | For the 26.2 perio package (item 16). Open Dental's documents have no 30. |
| `EnumGlidewellOrderStatus` | 0 to 7 | `Initialized`, `Received`, `InProgress`, `OnHold`, `Shipped`, `Cancelled`, `CreditPending`, `Credited` | New enum; numbers INFERRED (below). |
| `EnumGlidewellProductName` | none | none | New enum without members: the documents publish no values (below). |

None collides with a fork-only value or with the earlier values of `FORK-ENUMS.md`: the tinyint enums stay below 100 (`AlertType` 54,
`UserOdFkeyType` 31), `EnumPermType` below 10000; `ForkEnumRangeTests` pass unchanged. `FORK-ENUMS.md` now names the 26.2 highest values.

### Adoption tools

- `tools/SyntheticData/allowed-differences.adoption.json`: `TableAdded` for the three tables and `ColumnAdded` for the seven columns, each
  checked by `adopt` (a test holds the rules to the difference of the 25.3.48 and default references).
- `AdoptionRowCheck`: a new timestamp column with `DEFAULT current_timestamp()` (`insbluebook.DateTStamp`) must hold one and the same time in
  every row, the time of the `ALTER TABLE`; every other new column its default, as before. The summary line names it.
- `Rehearsal`: two adoptions of the same database may differ in that column (the time of adoption), like the three snapshot tables
  (`GetTimeOfAdoptionRules`, from `Migration26_1Plan.GetAlterTimeColumns`); cross-run check X2 says so.
- `migration-preview` (`Migration26_1Plan.Lines`) names step 2026110701 and the 26.2 schema; the startup model (`OdStartupCheck`) tries an
  Open Dental 26.2.0 client (the schema target) and still 26.1.70.
- The adoption allowlist is unchanged (24.3.63 and 25.3.48): no 26.1 or 26.2 Open Dental database is adopted yet.

## The storage engine (INFERRED)

The documents give no engine or character set. The three new tables are `ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci`,
like every one of the 427 tables of the 26.1.70 reference (and the office database's 25.3.48 schema), so the verifier's exact match holds and
the CRUD and backups treat them like every other table. A 26.2 Open Dental database created on a server with another default engine would
differ only in that table option; the verifier would report it.

## INFERRED behaviours

The documents give names, order and types only. Everything below is inferred from the conventions of the 25.3.48 schema (as M2-01 did) and
marked in the reference file.

- Every new column is `NOT NULL` with no column default, except: datetime columns `DEFAULT '0001-01-01 00:00:00'` (`apisubscription`
  `DateTimeLastFailure`, `DateTimeNextRetry`; `glidewellacct.DateTimeExpires`); text columns are `text NOT NULL`.
- `insbluebook.DateTStamp` is `timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()`, the definition of every other
  `DateTStamp` column. Adding it fills every existing row with the time of the `ALTER TABLE` (MariaDB's behaviour; checked on 10.11.19). The
  documents do not say what Open Dental writes there.
- Keys: one plain key named after each new foreign-key column: `provider.ClaimFormNum`, `provider.GlidewellAcctNum`; `glidewellmsg`
  `LabCaseNum`, `PatNum`, `GlidewellAcctNum`; `labcaseproduct` `LabCaseNum`, `PatNum`. None on `glidewellacct` besides the primary key, and
  none on the `apisubscription` columns or `insbluebook.DateTStamp`.
- New columns are at the end of their tables in the published order (the published positions match).
- `EnumGlidewellOrderStatus`: the documents list the eight members in order without numbers; the numbers are their order from 0.
- `EnumGlidewellProductName`: the documents name it ("From a list supplied by Glidewell") but list no value, so the enum has no members; a stored
  number loads and is written back as it is (tested). The `labcaseproduct.GlidewellProduct` column is `tinyint(4)` as published.
  XmlSerializer refuses an enum without members ("The object does not have serializable members"), so every Middle Tier call that carried a
  `LabCaseProduct` failed; the field is `[XmlIgnore]` and travels as its number through `LabCaseProduct.GlidewellProductXml`, the pattern of
  `Color` and `TimeSpan` fields (package S0-T of the .NET 10 plan; a round trip in `tests/MiddleTierMockTests`). No zero member was added:
  0 may be one of Glidewell's products (P262-GLIDEWELL, owner question 3).
- The display texts (descriptions) of the new enum members are this program's own wording.
- The reference is labelled 26.2.0, the version of the documents. Changes in later 26.2.x patches have no published document and are not
  covered; neither are 26.1.x patch changes after the 26.1 document (M2-01).

## Not built

- No feature: the Glidewell integration, the per-provider claim form, the automation trigger and condition, the referral commlog types, the
  perio setting, API subscription retries, the MariaDB backup alerts and `TextBelow` drawing are not built here.
- `db/init/new-database.sql` is not regenerated (the integration does it). Until then `ForkMigrationTests`'
  `InitSqlRegenerationTests.Generator_WritesExactlyTheCommittedFile` fails ("not what the generator writes now"), and so would `InitSqlTests`.
- No UI was run (OpenPmsDental.exe is not launched in this run).

## Interplay with the 26.2 feature packages

- The new members show in their lists at once: `DoNotBillInsuranceCheck` in the automation trigger list, `CarriersMatch` in the condition
  list and "Commlog Referral Types" in the Definitions window (default options). The claims, misc and perio packages give them their
  behaviour. `TextBelow` is hidden from the procedure code paint type lists until the tooth chart draws it (owner answer of 2026-11-07 to
  question 2, integration `next/int262-1`: `ProcedureCodes.GetPaintTypesForList`; a code that already has it keeps it).
- Claims: `provider.ClaimFormNum`, trigger 11, condition 15. Misc: `DefCat` 57. Perio: `UserOdFkeyType` 31. Glidewell: the three tables,
  `provider.GlidewellAcctNum` and the two enums (`EnumGlidewellProductName` needs values from a public source, with Open Dental's numbers).
- A feature step with a level below 2026110701 that writes `provider`, `apisubscription` or `insbluebook` through the CRUD would fail on a
  database coming from 26.1, because the CRUD writes the 26.2 columns before this step adds them; the feature packages' ranges
  (2026110711 and up) are all above it.
- Tests that build scratch databases from the 26.1.70 reference and use the `Provider`, `ApiSubscription` or `InsBlueBook` CRUD must use
  `SchemaVerifier.GetReference()` (26.2.0). None does today (`M2EMPTests` builds 26.1.70 tables but writes no provider).

## Files touched

- New: `OdParity26_2.cs`, `Reference/OdSchema-26.2.0.sql`, `TableTypes/{GlidewellAcct,GlidewellMsg,LabCaseProduct}.cs`, their three
  `Crud/*Crud.cs`, `tests/P262SchemaTests/*`, this note.
- Changed: `TableTypes/{ApiSubscription,InsBlueBook,Provider,AlertItem,Automation,AutomationCondition,Def,DefLink,GroupPermission,TsiTransLog,
  UserOdPref}.cs`, `Db/Enumerations.cs`, `Crud/{ApiSubscription,InsBlueBook,Provider}Crud.cs` (generated), `Data Interface/InsBlueBooks.cs` (one
  query), `SchemaVerifier.cs`, comments in `Adoption.cs` and `ForkTables.cs`, the fork migrations `README.md`, `OpenPmsDentalBusiness.csproj`,
  `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Tests changed because the default reference moved to 26.2.0: `M201Tests` (the default is 26.2.0; `DefCat` contiguity instead of a fixed
  count; a fully migrated database compared with `od_261_schema` or a 26.1.70 database differs by exactly the 26.2 changes), `M202Tests`
  (`EnumPermType` ends at 277), `RegistryTests` (the 26.2 members are later additions to the 25.3 lists), `ForkMigrationTests` (the 26.2 step
  runs after the 26.1 steps; `od_261_schema` comparison as above; the tables 25.4 to 26.2 change), `AdoptionValidation.Tests`.
- Tools and docs: `tools/AdoptionValidation/*` (above), `tools/SyntheticData/allowed-differences.adoption.json`, `tools/DevDbTool` comments,
  `tools/New-DevDatabase.ps1` and `tools/New-OdReferenceDatabase.ps1`, `docs/adoption-runbook.md`, `docs/port-notes/FORK-ENUMS.md`,
  `docs/release-notes/next.md`.

## Checks (2026-11-07)

- Full build of `OpenDental.Local.slnf` (Debug, x86): 0 errors.
- Targeted runs, all with `NoHqTests` (58/58): `P262SchemaTests` 25/25 (the document checks ran, `P262_OD_DOCS_DIR` set); `RegistryTests`
  39/39; `M201Tests` 24 passed, 2 inconclusive (the 26.1 document checks; the 26.1 documents are not available); `M202Tests` 39/39;
  `ForkMigrationTests` 37 passed, 2 inconclusive (the 25.3.48 dump is not available), 1 failed: `Generator_WritesExactlyTheCommittedFile`,
  only because `db/init/new-database.sql` is regenerated at integration (above); `AdoptionValidation.Tests` 66/66; `SyntheticData.Tests` 53
  passed, 1 inconclusive (no dump); and, for the tables changed (provider, insbluebook) through the full migration chain, `FeeTests` 48/48,
  `M2CLAIMFORMTests` 16/16, `M2TERMTests` 21/21, `BINSHTests` 36/36.
- Scratch databases `test_p262schema_*` are dropped after each run.

## Questions for the owner

**Answered on 2026-11-07** (owner, "all as recommended"): question 2, hide `TextBelow` from the procedure code paint type lists until the
tooth chart work draws it (built on `next/int262-1`: Edit Procedure Code and New Procedure Code use `ProcedureCodes.GetPaintTypesForList`,
which leaves it out except for a code that already has it; tests in `tests/P262SchemaTests/PaintTypeListTests.cs`). Every other question
(1, 3, 4 and 5): decided as built (owner, 2026-11-07).

1. `insbluebook.DateTStamp` of existing rows: the time of the update (chosen; what MariaDB does, and the safe answer for anything that asks
   "changed since") or the row's `DateTEntry`?
2. `TextBelow` (paint type 18) shows in the procedure code paint type lists but the tooth chart draws nothing for it: hide it from those lists
   until the tooth chart work ports it, or leave it?
3. The inferred NOT NULL, defaults, keys and engine can only be confirmed against a real 26.2 schema. Do they stay inferred?
4. The reference is labelled 26.2.0 (the documents' version). Should it name a 26.2.x patch release instead, once the office's target is
   known?
5. The values the feature packages build on show in their lists before those packages land (Commlog Referral Types in the Definitions
   window, the Do Not Bill Insurance trigger and the Carriers Match condition in the automation windows). Acceptable while the 26.2 port is
   integrated as one release?
