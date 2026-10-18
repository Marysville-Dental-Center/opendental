# Schema changes: Open Dental 24.3 → 25.3 → 26.1

Status: draft spec, 2026-10-18. Baseline: this fork (Open Dental 24.3.63). Targets: the office database (Open Dental 25.3.48, `fox-db`), then Open Dental 26.1.70.

Machine-readable twin: [`schema-changes.json`](schema-changes.json) (one entry per change, same content as the tables below).

## Summary

| Range | New tables | New columns | Type changes | Renames | Drops | JSON entries |
|---|---|---|---|---|---|---|
| 24.3 → 25.3 | 14 | 44 | 10 | 2 | 0 | 70 |
| 25.3 → 26.1 | 5 | 28 | 4 | 0 | 0 | 37 |

- **Where it comes from.** Open Dental publishes a full schema document per version and, more usefully, a per-version *diff* document that lists every table and column added, removed or retyped since the previous version. All six diff documents between 24.3 and 26.1 were found and read in full. The 25.3 → 26.1 half of this spec rests entirely on them; the 24.3 → 25.3 half is cross-checked against the real office database.
- **24.3 → 25.3 is fully reconciled.** Every one of the 15 new tables and 47 new columns in our measured diff is explained: 14 tables and 44 columns are Open Dental changes, 2 columns are the `task`/`taskhist` rename, one table (`deletedobject`) is an artefact of our extractor, and one column (`patient._portal_active_email`) is a local customisation in the office database, not Open Dental's. The published diffs add 11 type changes the measured diff could not see (10 JSON entries, see 25.2/25.3).
- **25.3 → 26.1 is verified against the full 26.1 schema document.** Its table list (427 tables) equals the office DB's 422 plus the 5 new tables, every table's column count equals the office DB's plus the documented additions, and the types of 2,264 non-integer columns match except for the documented changes and one drift item (`claimpayment.Note`).
- **No drops and no renames after 25.2.** Open Dental only adds and widens in this range, which keeps the migration additive.
- **DDL confidence.** Types, NOT NULL and defaults for 24.3 → 25.3 are copied from the office dump (authoritative). For 25.4 and 26.1 only the type is published; NOT NULL, defaults and indexes follow the conventions visible in the dump and are marked `inferred` in the JSON.

## Sources

| What | URL |
|---|---|
| Programming resources (entry point) | https://www.opendental.com/site/programmingresources.html |
| Database schema page (links only the newest doc, 26.2) | https://www.opendental.com/site/databaseschema.html |
| Full schema docs used | https://www.opendental.com/OpenDentalDocumentation24-3.xml, https://www.opendental.com/OpenDentalDocumentation25-3.xml, https://www.opendental.com/OpenDentalDocumentation26-1.xml (24.4, 25.1, 25.2, 25.4 and 26.2 also exist under the same naming) |
| Diff docs (each lists changes since the previous version) | [24.4](https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml), [25.1](https://www.opendental.com/OpenDentalDiffDocumentation25-1.xml), [25.2](https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml), [25.3](https://www.opendental.com/OpenDentalDiffDocumentation25-3.xml), [25.4](https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml), [26.1](https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml) |
| Release notes | [24.4](https://www.opendental.com/site/version24_4.html), [25.1](https://www.opendental.com/site/version25_1.html), [25.2](https://www.opendental.com/site/version25_2.html), [25.3](https://www.opendental.com/site/version25_3.html), [25.4](https://www.opendental.com/site/version25_4.html), [26.1](https://www.opendental.com/site/version26_1.html) |
| Manual (appointment mirroring) | https://opendental.com/help254/overviewapptmirror.html |
| Office schema (authoritative for 25.3.48) | `D:/Dev/opendental-schema/fox-db-schema.sql` |
| Measured diff | `D:/Dev/opendental-schema/schema-diff-24.3.63-vs-25.3.48.txt` |

The URL pattern is `OpenDentalDocumentationXX-Y.xml` and `OpenDentalDiffDocumentationXX-Y.xml`. Only the newest is linked from the site; the older ones are reachable by URL. The diff file is produced by Open Dental's own `DocumentationBuilder` tool (its source is in this repo under `DocumentationBuilder/`), which compares a version's schema with the previous version's. A column appears in a diff when it is new, deleted, retyped (listed twice, old and new type), or when its enum gained or lost values. Enum-only entries are not schema changes; they are collected in [Appendix A](#appendix-a-enum-value-changes-no-ddl).

## How to read the tables

- **Change**: `add-table`, `add-column`, `alter` (type change), `rename`, `drop`.
- **MariaDB type / DDL**: the column definition; full statements are in the JSON `ddl` field. Tables are MyISAM, `utf8mb3`, like the rest of the schema.
- **Feature**: the release-note entry that introduced the change, when one matches; otherwise the diff doc is the only evidence. Feature names are our paraphrases.
- **Scope**: `core` = keep; `vendor` = vendor integration we keep, talking to the vendor directly; `cut-feature` = belongs to a cut Open Dental hosted service, but the column or table must still exist so office data loads; `hq-internal` = Open Dental HQ use only, kept for schema parity.

## Part 1: 24.3 → 25.3 (to adopt the office database)

Open Dental released 24.4, 25.1, 25.2 and 25.3 in this range. DDL in this part is copied from the 25.3.48 dump unless marked otherwise.

### 24.4 (stable 2025-03-20)

Sources: [diff doc 24.4](https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml), [release notes 24.4](https://www.opendental.com/site/version24_4.html).

| Table.column | Change | MariaDB type / DDL | Purpose | Feature | Scope |
|---|---|---|---|---|---|
| `document`.`ChartLetterStatus` | add-column | `tinyint(4) NOT NULL` | Non-zero marks the document as a chart letter, so it is listed in the Chart module instead of the Imaging tree. | Chart letters written in Microsoft Word (Chart Module) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `document`.`UserNum` | add-column | `bigint(20) NOT NULL` | FK to userod. For chart letters only: the user who last changed the letter. | Chart letters written in Microsoft Word (Chart Module) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `document`.`ChartLetterHash` | add-column | `varchar(255) NOT NULL` | For chart letters only: a hash of the saved Word file, used to notice edits so a new document row (version) is written when the letter changes. | Chart letters written in Microsoft Word (Chart Module) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `fee`.`DateEffective` | add-column | `date NOT NULL DEFAULT '0001-01-01'` + index `FeeSchedCodeClinicProvDate` | Date from which a fee amount applies. The 0001-01-01 default means always effective; several rows for the same code/schedule can now coexist with different start dates. *Note: The existing FeeSchedCodeClinicProv index is kept; the dump adds FeeSchedCodeClinicProvDate alongside it.* | Effective date for fee schedules (Miscellaneous) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `payplan`.`SheetDefNum` | add-column | `bigint(20) NOT NULL` | FK to sheetdef. Payment-plan sheet layout used when printing this plan; 0 keeps the built-in printout. | Select a specific sheet for printing payment plans (Account Module) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `payplantemplate`.`SheetDefNum` | add-column | `bigint(20) NOT NULL` | FK to sheetdef. Default payment-plan sheet for plans created from this template; 0 keeps the built-in printout. | Select a specific sheet for printing payment plans (Account Module) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `imagedraw`.`BetterDiagLayer` | add-column | `tinyint(4) NOT NULL` | Enum EnumCategoryBetterDiag: which BetterDiagnostics finding layer (enamel, caries, bone loss and so on) an AI-drawn annotation belongs to, so layers can be toggled in Imaging. *Note: BetterDiagnostics was routed through Open Dental HQ; the feature is cut, but the column must exist to load office data.* | BetterDiagnostics AI imaging integration (Imaging Module; announced in 25.1) ([notes](https://www.opendental.com/site/version25_1.html)) | cut-feature |
| `feeschednote` | add-table | see CREATE TABLE below | Free-text notes attached to a fee schedule, optionally limited to a comma-separated list of clinics, with an editable date and the usual audit columns. | Notes on fee schedules (Miscellaneous) ([notes](https://www.opendental.com/site/version24_4.html)) | core |
| `queryfilter` | add-table | see CREATE TABLE below | Saved text filters for the Query Monitor: any running query containing FilterText is hidden. GroupName is a plain label used to group filters. | Filters in the Query Monitor (Miscellaneous) ([notes](https://www.opendental.com/site/version24_4.html)) | core |

New tables in 24.4 (DDL from the 25.3.48 dump):

```sql
CREATE TABLE `feeschednote` (
  `FeeSchedNoteNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `FeeSchedNum` bigint(20) NOT NULL,
  `ClinicNums` text NOT NULL,
  `Note` text NOT NULL,
  `DateEntry` date NOT NULL DEFAULT '0001-01-01',
  `SecUserNumEntry` bigint(20) NOT NULL,
  `SecDateEntry` date NOT NULL DEFAULT '0001-01-01',
  `SecDateTEdit` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`FeeSchedNoteNum`),
  KEY `FeeSchedNum` (`FeeSchedNum`),
  KEY `SecUserNumEntry` (`SecUserNumEntry`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `queryfilter` (
  `QueryFilterNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `GroupName` varchar(255) NOT NULL,
  `FilterText` varchar(255) NOT NULL,
  PRIMARY KEY (`QueryFilterNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```

### 25.1 (stable 2025-05-29)

Sources: [diff doc 25.1](https://www.opendental.com/OpenDentalDiffDocumentation25-1.xml), [release notes 25.1](https://www.opendental.com/site/version25_1.html).

| Table.column | Change | MariaDB type / DDL | Purpose | Feature | Scope |
|---|---|---|---|---|---|
| `codegroup`.`ShowInFrequency` | add-column | `tinyint(4) NOT NULL` | Whether the code group appears as a row in the frequency-limitation grid of insurance benefits. | Code groups in the frequency limitations grid (25.1 Family Module code-group changes; listed in the 25.3 API notes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `computerpref`.`CreditCardTerminalId` | add-column | `varchar(255) NOT NULL` | Per-workstation memory of the last PayConnect 2 card terminal used, pre-selected next time. | PayConnect 2.0 remembers the last terminal per workstation (Account Module) ([notes](https://www.opendental.com/site/version25_1.html)) | vendor integration |
| `creditcard`.`CardHolderName` | add-column | `varchar(255) NOT NULL` | Name on the card for a stored card/token. Only some merchant integrations fill it. | PayConnect 2.0 receipts show cardholder information (Account Module) ([notes](https://www.opendental.com/site/version25_1.html)) | vendor integration |
| `eformfield`.`AutoImport` | add-column | `tinyint(4) NOT NULL` | When set, the field value is written into the patient record as soon as the patient finishes the form, without a manual import step. | Automatic import of selected eForm fields (Miscellaneous) ([notes](https://www.opendental.com/site/version25_1.html)) | core |
| `eformfield`.`PrefillFromGuar` | add-column | `tinyint(4) NOT NULL` | When the patient has no value for the linked field, pre-fill it from the guarantor (mainly addresses for children). | Prefill eForm fields from the guarantor (Miscellaneous) ([notes](https://www.opendental.com/site/version25_1.html)) | core |
| `eformfielddef`.`AutoImport` | add-column | `tinyint(4) NOT NULL` | When set, the field value is written into the patient record as soon as the patient finishes the form, without a manual import step. | Automatic import of selected eForm fields (Miscellaneous) ([notes](https://www.opendental.com/site/version25_1.html)) | core |
| `eformfielddef`.`PrefillFromGuar` | add-column | `tinyint(4) NOT NULL` | When the patient has no value for the linked field, pre-fill it from the guarantor (mainly addresses for children). | Prefill eForm fields from the guarantor (Miscellaneous) ([notes](https://www.opendental.com/site/version25_1.html)) | core |
| `insverify`.`SecurityHash` | add-column | `varchar(255) NOT NULL` | Salted hash over the assignment fields (user, status definition, assignment date) to detect edits made outside the program. *Note: Our fork replaces Open Dental's hash with HMAC-SHA256 (decoupling plan); existing values will not validate and must be re-hashed.* | Row-integrity hashes (SecurityHash family) | core |
| `insverifyhist`.`SecurityHash` | add-column | `varchar(255) NOT NULL` | Copy of the insverify hash taken when a history row is made; stays valid because the hashed fields are copied unchanged. | Row-integrity hashes (SecurityHash family) | core |
| `medication`.`IsHidden` | add-column | `tinyint(4) NOT NULL` | Hides a medication from selection lists without deleting it. | Option to hide medications (Miscellaneous) ([notes](https://www.opendental.com/site/version25_1.html)) | core |
| `payplancharge`.`IsDownPayment` | add-column | `tinyint(4) NOT NULL` | Marks a payment-plan charge as (part of) the down payment; a plan may have several. | Payment plan down payments (no specific release-note entry; related Account Module payment-plan changes) ([notes](https://www.opendental.com/site/version25_1.html)) | core |

### 25.2 (stable 2025-09-09)

Sources: [diff doc 25.2](https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml), [release notes 25.2](https://www.opendental.com/site/version25_2.html).

| Table.column | Change | MariaDB type / DDL | Purpose | Feature | Scope |
|---|---|---|---|---|---|
| `appointment`.`IsMirrored` | add-column | `tinyint(4) NOT NULL` | Shows a linked copy of the appointment in the secondary provider's operatory, sized by that provider's time; edits stay in sync. | Appointment mirroring (Appointments Module) ([notes](https://opendental.com/help254/overviewapptmirror.html)) | core |
| `apptview`.`ShowMirroredAppts` | add-column | `tinyint(4) NOT NULL` | Whether this appointment view draws mirrored copies. | Appointment mirroring (Appointments Module) ([notes](https://opendental.com/help254/overviewapptmirror.html)) | core |
| `histappointment`.`IsMirrored` | add-column | `tinyint(4) NOT NULL` | History copy of appointment.IsMirrored. | Appointment mirroring (Appointments Module) ([notes](https://opendental.com/help254/overviewapptmirror.html)) | core |
| `histappointment`.`ProcDescript` | alter | `text NOT NULL` | Widened from varchar(255) to text so the history copy can hold the same procedure description as appointment.ProcDescript (already text). *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `branding` | add-table | see CREATE TABLE below | Per-clinic branding values (ClinicNum 0 = practice default) keyed by BrandingType: logo, colour palette (JSON), office description. Patient-facing apps cache rows by DateTimeUpdated. *Note: Only used by Open Dental hosted/mobile apps. ValueString becomes mediumtext in 26.1.* | eClipboard brandings (eServices) ([notes](https://www.opendental.com/site/version26_1.html)) | cut-feature |
| `clearinghouse`.`EnableXConnect` | add-column | `tinyint(4) NOT NULL DEFAULT 0` | Turns on DentalXChange XConnect claim validation for this clearinghouse (per clinic). | XConnect claim validation for DentalXChange (Account Module) ([notes](https://www.opendental.com/site/version25_2.html)) | vendor integration |
| `eclipboardsheetdef`.`SheetDefNumsConsidered` | add-column | `varchar(255) NOT NULL` | Comma-separated sheetdef numbers that count toward this eForm's frequency rule, easing the move from sheets to eForms. | eForms consider Sheets when calculating frequency (eForms) ([notes](https://www.opendental.com/site/version25_2.html)) | cut-feature |
| `eformfield`.`ValueLabelEnglish` | add-column | `text NOT NULL` | English copy of the field label kept after translation, so conditional logic can find the field in any language. | eForm translation support (no specific release-note entry) | core |
| `eformfield`.`PickListVisEnglish` | add-column | `text NOT NULL` | English copy of the visible pick list for translated eForms; used for conditional logic and import. *Note: Open Dental created this as varchar(255) in 25.2 and widened it to text in 25.3; the DDL here is the final 25.3 type.* | eForm translation support (no specific release-note entry) | core |
| `eobattach`.`ClaimNumPreAuth` | add-column | `bigint(20) NOT NULL` + index `ClaimNumPreAuth` | FK to claim (a preauthorization). Lets an EOB image be attached to a preauth instead of a claim payment; 0 for normal EOBs. | Attach EOBs to preauthorizations (Miscellaneous) ([notes](https://www.opendental.com/site/version25_2.html)) | core |
| `inspending` | add-table | see CREATE TABLE below | Insurance details a patient entered (carrier name, subscriber, IDs, group, employer, relationship, ordinal) held for staff to review before real insplan/inssub/patplan rows are made. *Note: Release notes do not name this table; the link to the Ins Pick field is our inference from timing and column content.* | Ins Pick eForm field / patient-entered insurance (eForms; inferred link) ([notes](https://www.opendental.com/site/version25_2.html)) | core |
| `insplan`.`GroupNum` | alter | `varchar(50) DEFAULT NULL` | Group number widened from 25 to 50 characters. *Note: Previous type: varchar(25).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `inssub`.`SecurityHash` | add-column | `varchar(255) NOT NULL` | Salted hash over plan, subscriber, subscriber ID and term date, to detect edits made outside the program. | Row-integrity hashes (SecurityHash family) | core |
| `sheetfield`.`UserSigned` | add-column | `bigint(20) NOT NULL` + index `UserSigned` | FK to userod. For signature boxes signed by a logged-in provider user: who signed. | Record the signing user on sheets (Miscellaneous) ([notes](https://www.opendental.com/site/version25_2.html)) | core |
| `statement`.`ShowTransSinceBalZero` | add-column | `tinyint(4) NOT NULL` | Remembers the "show transactions since the balance was last zero or negative" option for this statement. | No matching release-note entry; schema-only evidence from the diff doc | core |
| `task`.`Category` | rename | `TriageCategory` → `Category`, `bigint(20) NOT NULL DEFAULT 0` | Renamed from TriageCategory. FK to definition; general task category shown when the TaskCategoryShow preference is on (previously an HQ triage field). *Note: Same type and position; the index keeps its old name `TriageCategory` in the 25.3.48 dump. The diff doc shows it as add Category + delete TriageCategory.* | Task Category dropdown (Manage Module) ([notes](https://www.opendental.com/site/version25_2.html)) | core |
| `taskhist`.`Category` | rename | `TriageCategory` → `Category`, `bigint(20) NOT NULL DEFAULT 0` | Renamed from TriageCategory. FK to definition; general task category shown when the TaskCategoryShow preference is on (previously an HQ triage field). *Note: Same type and position; the index keeps its old name `TriageCategory` in the 25.3.48 dump. The diff doc shows it as add Category + delete TriageCategory.* | Task Category dropdown (Manage Module) ([notes](https://www.opendental.com/site/version25_2.html)) | core |

New tables in 25.2 (DDL from the 25.3.48 dump):

```sql
CREATE TABLE `branding` (
  `BrandingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `BrandingType` tinyint(4) NOT NULL,
  `ClinicNum` bigint(20) NOT NULL,
  `ValueString` text NOT NULL,
  `DateTimeUpdated` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`BrandingNum`),
  KEY `ClinicNum` (`ClinicNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `inspending` (
  `InsPendingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `PatNum` bigint(20) NOT NULL,
  `PatNumSubscriber` bigint(20) NOT NULL,
  `Ordinal` tinyint(3) unsigned NOT NULL,
  `Relationship` tinyint(4) NOT NULL,
  `GroupNum` varchar(255) NOT NULL,
  `GroupName` varchar(255) NOT NULL,
  `Employer` varchar(255) NOT NULL,
  `SubscriberID` varchar(255) NOT NULL,
  `Phone` varchar(255) NOT NULL,
  `CarrierName` varchar(255) NOT NULL,
  PRIMARY KEY (`InsPendingNum`),
  KEY `PatNum` (`PatNum`),
  KEY `PatNumSubscriber` (`PatNumSubscriber`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```

### 25.3 (stable 2025-12-23)

Sources: [diff doc 25.3](https://www.opendental.com/OpenDentalDiffDocumentation25-3.xml), [release notes 25.3](https://www.opendental.com/site/version25_3.html).

| Table.column | Change | MariaDB type / DDL | Purpose | Feature | Scope |
|---|---|---|---|---|---|
| `asapcomm`.`UserNum` | add-column | `bigint(20) NOT NULL` | FK to userod. Who sent this ASAP (short-notice opening) message; 0 if unknown. | Web Sched ASAP tracks which user sent a message (eServices) ([notes](https://www.opendental.com/site/version25_3.html)) | cut-feature |
| `benefit`.`ToothRange` | add-column | `varchar(255) DEFAULT NULL` | Comma-separated tooth numbers (ranges allowed in the UI) that a benefit row applies to, for limitations tied to specific teeth. | Insurance benefits by tooth number (Family Module; partial match to "exclusions by tooth number") ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `chat` | add-table | see CREATE TABLE below | A conversation (group or direct); optional name. *Note: Runs against the local database, so it can be kept; whether to ship the chat app is a separate product decision.* | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `chatmsg` | add-table | see CREATE TABLE below | One message: author, sent time, rich-text body, optional quoted message, event type (for system events such as membership changes), importance flag, and a strictly increasing SeqCount used to fetch new messages. *Note: Runs against the local database, so it can be kept; whether to ship the chat app is a separate product decision.* | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `chatattach` | add-table | see CREATE TABLE below | Files attached to a chat message, stored in the database with a small PNG thumbnail. *Note: Runs against the local database, so it can be kept; whether to ship the chat app is a separate product decision.* | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `chatreaction` | add-table | see CREATE TABLE below | Emoji reactions to a message by a user. *Note: Runs against the local database, so it can be kept; whether to ship the chat app is a separate product decision.* | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `chatuserattach` | add-table | see CREATE TABLE below | Membership of a user in a chat, read/unread state, mute flag, and when the user left. *Note: Runs against the local database, so it can be kept; whether to ship the chat app is a separate product decision.* | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `chatuserod` | add-table | see CREATE TABLE below | Per-user chat profile and preferences: presence status (may follow time-clock status), photo and crop, start-up and notification behaviour. *Note: Runs against the local database, so it can be kept; whether to ship the chat app is a separate product decision.* | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `sequencecounter` | add-table | see CREATE TABLE below | Named counters (one row per consumer, e.g. chatmsg) handing out increasing sequence numbers; more reliable than timestamps for "what is new" polling. | Internal office chat (Major Changes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `claimpayment`.`Note` | alter | `text NOT NULL` | Check/EFT note widened to text (65k) because automated incoming payment notes can be long. *Note: DRIFT: the office 25.3.48 database still has varchar(255) DEFAULT ''. Widening is safe; do it when adopting the data.* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `codegroup`.`ShowInOther` | add-column | `tinyint(4) NOT NULL` | Whether the code group appears in the Other Benefits grid of the insurance benefits window. | Updated Other Benefits editing (Family Module); CodeGroups ShowInOther (API notes) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `eformfield`.`PickListVis` | alter | `text NOT NULL` | Widened from varchar(255) to text so long pick lists and conditional values fit. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `eformfield`.`PickListDb` | alter | `text NOT NULL` | Widened from varchar(255) to text so long pick lists and conditional values fit. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `eformfield`.`ConditionalValue` | alter | `text NOT NULL` | Widened from varchar(255) to text so long pick lists and conditional values fit. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `eformfielddef`.`PickListVis` | alter | `text NOT NULL` | Widened from varchar(255) to text so long pick lists and conditional values fit. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `eformfielddef`.`PickListDb` | alter | `text NOT NULL` | Widened from varchar(255) to text so long pick lists and conditional values fit. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `eformfielddef`.`ConditionalValue` | alter | `text NOT NULL` | Widened from varchar(255) to text so long pick lists and conditional values fit. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `emailmessage`.`FailReason` | alter | `text NOT NULL` | Widened from varchar(255) to text so full send-error text is kept. *Note: Previous type: varchar(255).* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `journalentry`.`Payee` | add-column | `varchar(255) NOT NULL` | Who the money went to; previously typed into Memo. Up to 255 characters. | Payee name on accounting transactions (Manage Module) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `journalentry`.`Notes` | add-column | `text NOT NULL` | Long free-text notes, split out from Memo. | Accounting transaction notes / check printing (Manage Module) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `journalentry`.`NeedsReview` | add-column | `tinyint(4) NOT NULL` | Flags the entry for review; only effect is highlighting the row in red. | Mark an accounting transaction as Needs Review (Manage Module) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `transaction`.`NeedsReview` | add-column | `tinyint(4) NOT NULL` | Transaction-level review flag; Open Dental already marks it deprecated in favour of journalentry.NeedsReview. | Mark an accounting transaction as Needs Review (Manage Module) ([notes](https://www.opendental.com/site/version25_3.html)) | core |
| `reqneeded`.`SchoolCourseDefNum` | add-column | `bigint(20) NOT NULL` + index `SchoolCourseDefNum` | FK to schoolcoursedef. Ties a requirement to a course definition; 0 for course-level or graduation requirements. | Dental school overhaul (schema in 25.3; feature announced in 25.4 Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `reqstudent`.`ProcNum` | add-column | `bigint(20) NOT NULL` + index `ProcNum` | FK to procedurelog. The procedure that satisfied the student requirement. | Dental school overhaul (schema in 25.3; feature announced in 25.4 Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcourse`.`DateStart` | add-column | `date NOT NULL DEFAULT '0001-01-01'` | First day of the course's default schedule. | Dental school overhaul (schema in 25.3; feature announced in 25.4 Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcourse`.`DateEnd` | add-column | `date NOT NULL DEFAULT '0001-01-01'` | Last day of the course's default schedule. | Dental school overhaul (schema in 25.3; feature announced in 25.4 Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcourse`.`SchoolClassNum` | add-column | `bigint(20) NOT NULL DEFAULT 0` + index `SchoolClassNum` | FK to schoolclass. Which class cohort takes this course; 0 for older courses. | Dental school overhaul (schema in 25.3; feature announced in 25.4 Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcourse`.`GradingScaleNum` | add-column | `bigint(20) NOT NULL` + index `GradingScaleNum` | FK to gradingscale. All evaluations of the course must use this scale. | Dental school overhaul (schema in 25.3; feature announced in 25.4 Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolapproval` | add-table | see CREATE TABLE below | Instructor sign-off on a student's work item (appointment, procedure, treatment plan, perio exam, allergy, problem, document or mount); one FK set per row, with review status. | Approval for instructors to authorize procedures (Dental Schools, announced 25.4) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcoursedef` | add-table | see CREATE TABLE below | Reusable course definition (course ID and title) from which yearly schoolcourse rows are scheduled. | Dental school overhaul (announced 25.4) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcoursesched` | add-table | see CREATE TABLE below | Weekly time slots for a course definition or course, plus dated overrides and cancellations. | Instructor and student planners (Dental Schools, announced 25.4) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `task`.`TriagePosition` | add-column | `int(11) NOT NULL` | 1-based position the task held in the HQ "Triage" list when a support technician claimed it. | Open Dental HQ support triage | hq-internal |
| `taskhist`.`TriagePosition` | add-column | `int(11) NOT NULL` | 1-based position the task held in the HQ "Triage" list when a support technician claimed it. | Open Dental HQ support triage | hq-internal |

New tables in 25.3 (DDL from the 25.3.48 dump):

```sql
CREATE TABLE `chat` (
  `ChatNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) NOT NULL,
  PRIMARY KEY (`ChatNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `chatmsg` (
  `ChatMsgNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ChatNum` bigint(20) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTimeSent` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `Message` text NOT NULL,
  `SeqCount` bigint(20) unsigned NOT NULL,
  `Quote` bigint(20) NOT NULL,
  `EventType` tinyint(4) NOT NULL,
  `IsImportant` tinyint(4) NOT NULL,
  PRIMARY KEY (`ChatMsgNum`),
  UNIQUE KEY `SeqCount` (`SeqCount`),
  KEY `UserNum` (`UserNum`),
  KEY `ChatNum` (`ChatNum`),
  KEY `DateTimeSent` (`DateTimeSent`),
  KEY `chatnum_seqcount` (`ChatNum`,`SeqCount`),
  KEY `Quote` (`Quote`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `chatattach` (
  `ChatAttachNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ChatMsgNum` bigint(20) NOT NULL,
  `FileName` varchar(255) NOT NULL,
  `Thumbnail` mediumblob NOT NULL,
  `FileData` mediumblob NOT NULL,
  PRIMARY KEY (`ChatAttachNum`),
  KEY `ChatMsgNum` (`ChatMsgNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `chatreaction` (
  `ChatReactionNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ChatMsgNum` bigint(20) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `EmojiName` varchar(255) NOT NULL,
  PRIMARY KEY (`ChatReactionNum`),
  KEY `ChatMsgNum` (`ChatMsgNum`),
  KEY `UserNum` (`UserNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `chatuserattach` (
  `ChatUserAttachNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `UserNum` bigint(20) NOT NULL,
  `ChatNum` bigint(20) NOT NULL,
  `IsRead` tinyint(4) NOT NULL,
  `DateTimeRemoved` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `IsMute` tinyint(4) NOT NULL,
  PRIMARY KEY (`ChatUserAttachNum`),
  KEY `UserNum` (`UserNum`),
  KEY `ChatNum` (`ChatNum`),
  KEY `usernum_chatnum` (`UserNum`,`ChatNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `chatuserod` (
  `ChatUserodNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `UserNum` bigint(20) NOT NULL,
  `UserStatus` tinyint(4) NOT NULL,
  `DateTimeStatusReset` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `Photo` text NOT NULL,
  `PhotoCrop` varchar(255) NOT NULL,
  `OpenBackground` tinyint(4) NOT NULL,
  `CloseKeepRunning` tinyint(4) NOT NULL,
  `MuteNotifications` tinyint(4) NOT NULL,
  `DismissNotifySecs` int(11) NOT NULL,
  `MuteImportantNotifications` tinyint(4) NOT NULL,
  `DismissImportantNotifySecs` int(11) NOT NULL,
  PRIMARY KEY (`ChatUserodNum`),
  KEY `UserNum` (`UserNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `sequencecounter` (
  `CounterNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `CounterName` varchar(255) NOT NULL,
  `CounterVal` bigint(20) NOT NULL,
  PRIMARY KEY (`CounterNum`),
  KEY `CounterVal` (`CounterVal`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `schoolapproval` (
  `SchoolApprovalNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ProvNum` bigint(20) NOT NULL,
  `SignOffStatus` tinyint(4) NOT NULL,
  `InstructorNum` bigint(20) NOT NULL,
  `AptNum` bigint(20) NOT NULL,
  `ProcNum` bigint(20) NOT NULL,
  `TreatPlanNum` bigint(20) NOT NULL,
  `PerioExamNum` bigint(20) NOT NULL,
  `AllergyNum` bigint(20) NOT NULL,
  `DiseaseNum` bigint(20) NOT NULL,
  `DocNum` bigint(20) NOT NULL,
  `MountNum` bigint(20) NOT NULL,
  `SecDateEntry` date NOT NULL DEFAULT '0001-01-01',
  `SecDateTEdit` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`SchoolApprovalNum`),
  KEY `ProvNum` (`ProvNum`),
  KEY `InstructorNum` (`InstructorNum`),
  KEY `AptNum` (`AptNum`),
  KEY `ProcNum` (`ProcNum`),
  KEY `TreatPlanNum` (`TreatPlanNum`),
  KEY `PerioExamNum` (`PerioExamNum`),
  KEY `AllergyNum` (`AllergyNum`),
  KEY `DiseaseNum` (`DiseaseNum`),
  KEY `DocNum` (`DocNum`),
  KEY `MountNum` (`MountNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `schoolcoursedef` (
  `SchoolCourseDefNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `CourseID` varchar(255) NOT NULL,
  `Descript` varchar(255) NOT NULL,
  PRIMARY KEY (`SchoolCourseDefNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `schoolcoursesched` (
  `SchoolCourseSchedNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SchoolCourseDefNum` bigint(20) NOT NULL,
  `SchoolCourseNum` bigint(20) NOT NULL,
  `TimeStart` time NOT NULL DEFAULT '00:00:00',
  `TimeEnd` time NOT NULL DEFAULT '00:00:00',
  `DayOfTheWeek` tinyint(4) NOT NULL,
  `DateOverride` date NOT NULL DEFAULT '0001-01-01',
  `IsOverride` tinyint(4) NOT NULL,
  `IsCanceled` tinyint(4) NOT NULL,
  PRIMARY KEY (`SchoolCourseSchedNum`),
  KEY `SchoolCourseDefNum` (`SchoolCourseDefNum`),
  KEY `SchoolCourseNum` (`SchoolCourseNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```

### Reconciliation with the measured diff

Measured: 15 tables and 47 columns in the office DB that our 24.3.63 code does not know, plus `task.triagecategory`/`taskhist.triagecategory` known to the code but absent from the DB.

| Measured item | Explanation |
|---|---|
| 14 of 15 new tables | Open Dental additions: `feeschednote`, `queryfilter` (24.4); `branding`, `inspending` (25.2); `chat`, `chatattach`, `chatmsg`, `chatreaction`, `chatuserattach`, `chatuserod`, `schoolapproval`, `schoolcoursedef`, `schoolcoursesched`, `sequencecounter` (25.3). Column lists match the diff docs exactly. |
| `deletedobject` | **Not a change.** A legacy mobile-sync table that 24.3 still has (`OpenDentBusiness/TableTypes/DeletedObject.cs`, marked obsolete). It does not derive from `TableBase`, so `ExtractCodeSchema` skipped it. It is still in the 26.1 schema doc. Do not confuse it with the new 26.1 table `deletedobj`. |
| 44 of 47 new columns | Open Dental additions, listed per version above (24.4: 7, 25.1: 11, 25.2: 11, 25.3: 15). |
| `task.category`, `taskhist.category` + missing `triagecategory` | One rename per table in 25.2 (same type and position; the dump keeps the old index name `TriageCategory`). |
| `patient._portal_active_email` | **Not Open Dental.** A local customisation: `varchar(100) GENERATED ALWAYS AS (case when PatStatus = 0 and Email <> '' then Email else NULL end) VIRTUAL` with `UNIQUE KEY portal_active_email_uniq`. It appears in no Open Dental doc. The unique key makes the database reject a second active patient with the same e-mail address, which is common within families, so Open Dental-style inserts and updates can fail. Something outside Open Dental (likely a patient-portal integration) depends on it. **Decision needed before adoption:** keep it, or drop the column and index during import. |

Type changes the measured diff could not see (names only were compared): `histappointment.ProcDescript` and `insplan.GroupNum` (25.2); `claimpayment.Note`, `eformfield.PickListVis`, `.PickListDb`, `.ConditionalValue`, `.PickListVisEnglish`, `eformfielddef.PickListVis`, `.PickListDb`, `.ConditionalValue`, `emailmessage.FailReason` (25.3). All match the dump except **`claimpayment.Note`**: the docs say `text` from 25.3 on, but the office DB still has `varchar(255) DEFAULT ''`. Widening is safe; the JSON entry includes a NULL clean-up before the change. `eformfield.PickListVisEnglish` was created as `varchar(255)` in 25.2 and widened in 25.3, so the JSON folds it into a single add with the final type.

Indexes: the diff docs carry no index information. Indexes on *new* columns are taken from the dump (for example `fee.FeeSchedCodeClinicProvDate`, `eobattach.ClaimNumPreAuth`, `sheetfield.UserSigned`). Index changes on *pre-existing* columns between 24.3 and 25.3 are not covered by this spec (see Gaps).

### HQ-internal tables (not changes)

These 54 tables are defined in our 24.3.63 code but absent from the office DB. None of them appears in the published 26.1 schema document either, which confirms they live only in Open Dental's own HQ database. Keep the classes until the HQ tools are deleted (decoupling plan, Phase 1), but never create the tables in an office database.

`address`, `apibilling`, `bugsubmission`, `bugsubmissionhash`, `chatuser`, `child`, `childparent`, `childparentlink`, `childroom`, `childroomlog`, `cloudbilling`, `commloghist`, `ehrcode`, `eservicebilling`, `eservicecodelink`, `faq`, `job`, `jobactivelink`, `jobcontrol`, `joblink`, `joblog`, `jobnote`, `jobnotification`, `jobpermission`, `jobproject`, `jobquote`, `jobreview`, `jobsprint`, `jobsprintlink`, `jobteam`, `jobteamuser`, `maparea`, `mapareacontainer`, `phone`, `phonecomp`, `phoneconf`, `phoneempdefault`, `phoneempsubgroup`, `phonegraph`, `phonemetric`, `plannedappt`, `reseller`, `resellerservice`, `sitelink`, `smsbilling`, `tasktaken`, `triagemetric`, `voicemail`, `webchataiassistant`, `webchatmessage`, `webchatnote`, `webchatpref`, `webchatsession`, `webchatsurvey`.

(`chatuser` is HQ web chat; the new office chat uses `chatuserod`.)

## Part 2: 25.3 → 26.1

Open Dental released 25.4 and 26.1 in this range. The type of every item is from the published docs; NOT NULL, defaults and indexes are **inferred** from conventions in the 25.3.48 dump: `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'`, `date NOT NULL DEFAULT '0001-01-01'`, other types `NOT NULL` without default, `bigint` foreign keys indexed under the column name, `AUTO_INCREMENT` `bigint(20)` primary keys. Where a summary states a non-zero default (grades default to -1), it is applied.

### 25.4 (stable 2026-04-13)

Sources: [diff doc 25.4](https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml), [release notes 25.4](https://www.opendental.com/site/version25_4.html).

| Table.column | Change | MariaDB type / DDL | Purpose | Feature | Scope |
|---|---|---|---|---|---|
| `carrier`.`PaySuiteTransSup` | add-column | `tinyint(4) NOT NULL` | Enum EnumPaySuiteTransTypes (0 none, 1 extended reversal, 2 plan details, 3 both): which PaySuite transactions a Canadian carrier supports. | PaySuite integration for Canadian claim payments and reconciliation (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | vendor integration |
| `paysuitepayment` | add-table | see CREATE TABLE below | One insurer payment (cheque or direct deposit) received through PaySuite: payer IDs, amount, date, status (issued, paid, reversed, under threshold), reversal reason, roll-up link, reconciliation stage and the claimpayment it was posted to. | PaySuite integration for Canadian claim payments and reconciliation (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | vendor integration |
| `paysuitepaymentdetail` | add-table | see CREATE TABLE below | JSON blob with the line detail behind a PaySuite payment (rolled-up payments, claims, adjustments). | PaySuite integration for Canadian claim payments and reconciliation (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | vendor integration |
| `eform`.`DateTimeSubmitted` | add-column | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When the patient submitted the eForm (set automatically from eClipboard, editable when filling in the office). | No matching release-note entry; schema-only evidence from the diff doc | core |
| `sheet`.`DateTimeSubmitted` | add-column | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When the patient submitted the sheet (set automatically from eClipboard, editable when filling in the office). | No matching release-note entry; schema-only evidence from the diff doc | core |
| `evaluation`.`GradeOverride` | add-column | `float NOT NULL DEFAULT -1` | Instructor-entered grade that replaces the computed one when set; -1 means no override. *Note: Doc says -1 by default; existing rows must be set to -1 when the column is added.* | Overhauled Dental Schools feature (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `evaluationdef`.`SchoolCourseDefNum` | add-column | `bigint(20) NOT NULL` + index `SchoolCourseDefNum` | FK to schoolcoursedef. Evaluation template belongs to a course definition; 0 when it belongs to a course. | Overhauled Dental Schools feature (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcoursedef`.`GradingScaleNum` | add-column | `bigint(20) NOT NULL` + index `GradingScaleNum` | FK to gradingscale. Grading scale for courses made from this definition. | Overhauled Dental Schools feature (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcourseenrollee` | add-table | see CREATE TABLE below | A student (provider row) enrolled in a course, with the computed course grade and an optional instructor override (-1 = none). | Overhauled Dental Schools feature (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `schoolcourseinstructor` | add-table | see CREATE TABLE below | An instructor (provider row) assigned to a course. | Overhauled Dental Schools feature (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `inseditlog`.`OldValueBig` | add-column | `text NOT NULL` | Holds the old value when it is longer than the 255-character OldValue column (e.g. plan or subscriber notes). | No matching release-note entry; schema-only evidence from the diff doc | core |
| `inseditlog`.`NewValueBig` | add-column | `text NOT NULL` | Holds the new value when it is longer than the 255-character NewValue column. | No matching release-note entry; schema-only evidence from the diff doc | core |
| `inseditpatlog`.`OldValueBig` | add-column | `text NOT NULL` | Holds the old value when it is longer than the 255-character OldValue column (e.g. plan or subscriber notes). | No matching release-note entry; schema-only evidence from the diff doc | core |
| `inseditpatlog`.`NewValueBig` | add-column | `text NOT NULL` | Holds the new value when it is longer than the 255-character NewValue column. | No matching release-note entry; schema-only evidence from the diff doc | core |
| `insverify`.`DateLastAssigned` | alter | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | Assignment timestamp now keeps the time of day, not just the date. *Note: Previous type: date NOT NULL DEFAULT '0001-01-01'. Keep the existing DateLastAssigned index.* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `insverifyhist`.`DateLastAssigned` | alter | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | History copy of insverify.DateLastAssigned, also widened to datetime. *Note: Previous type: date NOT NULL DEFAULT '0001-01-01'.* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `payterminal`.`CCIntegration` | add-column | `varchar(50) NOT NULL` | Which card-processing integration a payment terminal belongs to (e.g. PayConnect). | Payment terminals for multiple processors; probable link to Global Payments V2 program link (Major Changes) ([notes](https://www.opendental.com/site/version25_4.html)) | vendor integration |
| `procedurelog`.`NoBillIns` | add-column | `tinyint(4) NOT NULL` | Per-procedure "Do Not Bill Insurance" decision; claimprocs copy it when first estimated. | Do Not Bill Insurance per procedure (audit-trail entry in Miscellaneous) ([notes](https://www.opendental.com/site/version25_4.html)) | core |
| `referral`.`Telephone` | alter | `varchar(30) DEFAULT ''` | Referral phone no longer limited to exactly 10 digits. *Note: Previous type: varchar(10) DEFAULT ''.* | No matching release-note entry; schema-only evidence from the diff doc | core |
| `registrationkey`.`DateTClinicAccess` | add-column | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | Set by Open Dental HQ: the office may add clinics until this time. | Open Dental licensing | hq-internal |

New tables in 25.4 (DDL inferred):

```sql
CREATE TABLE `paysuitepayment` (
  `PaySuitePaymentNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `PaymentId` varchar(255) NOT NULL,
  `ProviderId` varchar(255) NOT NULL,
  `PaymentMethod` varchar(255) NOT NULL,
  `PaymentReference` varchar(255) NOT NULL,
  `PaymentAmount` double NOT NULL,
  `PaymentDate` date NOT NULL DEFAULT '0001-01-01',
  `PaymentStatus` varchar(255) NOT NULL,
  `ReversalReasonCode` varchar(255) NOT NULL,
  `AssociatedPaymentId` varchar(255) NOT NULL,
  `PaySuitePaymentDetailNum` bigint(20) NOT NULL,
  `HasUnresolvedClaimPayment` tinyint(4) NOT NULL,
  `ReconciliationStatus` tinyint(4) NOT NULL,
  `ClaimPaymentNum` bigint(20) NOT NULL,
  PRIMARY KEY (`PaySuitePaymentNum`),
  KEY `PaySuitePaymentDetailNum` (`PaySuitePaymentDetailNum`),
  KEY `ClaimPaymentNum` (`ClaimPaymentNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `paysuitepaymentdetail` (
  `PaySuitePaymentDetailNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `DetailsJson` mediumtext NOT NULL,
  PRIMARY KEY (`PaySuitePaymentDetailNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `schoolcourseenrollee` (
  `SchoolCourseEnrolleeNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SchoolCourseNum` bigint(20) NOT NULL,
  `StudentNum` bigint(20) NOT NULL,
  `GradeNumber` float NOT NULL DEFAULT -1,
  `GradeOverride` float NOT NULL DEFAULT -1,
  PRIMARY KEY (`SchoolCourseEnrolleeNum`),
  KEY `SchoolCourseNum` (`SchoolCourseNum`),
  KEY `StudentNum` (`StudentNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```
```sql
CREATE TABLE `schoolcourseinstructor` (
  `SchoolCourseInstructorNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SchoolCourseNum` bigint(20) NOT NULL,
  `InstructorNum` bigint(20) NOT NULL,
  PRIMARY KEY (`SchoolCourseInstructorNum`),
  KEY `SchoolCourseNum` (`SchoolCourseNum`),
  KEY `InstructorNum` (`InstructorNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```

### 26.1 (stable 2026-08-06)

Sources: [diff doc 26.1](https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml), [release notes 26.1](https://www.opendental.com/site/version26_1.html).

| Table.column | Change | MariaDB type / DDL | Purpose | Feature | Scope |
|---|---|---|---|---|---|
| `branding`.`ValueString` | alter | `mediumtext NOT NULL` | Widened from text to mediumtext (16 MB) so a logo image can be stored as base64 in the row. *Note: Previous type: text.* | eClipboard brandings: logo uploads saved in the database (eServices) ([notes](https://www.opendental.com/site/version26_1.html)) | cut-feature |
| `carrier`.`PreAuthCodes` | add-column | `text NOT NULL` | Procedure code list/ranges (commas and hyphens) that this carrier requires preauthorization for. | Carriers list: flag procedures that need preauthorization (Lists) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `chatuserod`.`PlayNotificationSound` | add-column | `tinyint(4) NOT NULL` | Play a sound when a chat message arrives (default off). | Chat settings: notification sound (Manage Module) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `claimproc`.`Etrans835AttachNum` | add-column | `bigint(20) NOT NULL` + index `Etrans835AttachNum` | FK to etrans835attach. For claimprocs paid from an ERA, which 835 claim attachment they came from; 0 otherwise. | No matching release-note entry; schema-only evidence from the diff doc | core |
| `codegroup`.`ShowInHistory` | add-column | `tinyint(4) NOT NULL` | Show the code group as its own row in the Insurance History window. | Insurance history options in Code Groups (Setup; Family Module Insurance History) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `codegroup`.`HistProcCode` | add-column | `varchar(255) NOT NULL` | The single procedure code entered when the user records history for this code group. | Insurance history options in Code Groups (Setup; Family Module Insurance History) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `codegroup`.`IsPerioFourQuads` | add-column | `tinyint(4) NOT NULL` | Show the code group four times (one per quadrant) in Insurance History, for perio codes. | Insurance history options in Code Groups (Setup) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `definition`.`Supp` | add-column | `varchar(255) NOT NULL` | Extra value for definition categories that need one more field than ItemValue. *Note: Release notes do not name the column; the use for adjustment-type percentages is our inference.* | Generic extra definition value (likely used by "adjustment type default percentage", Setup) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `deletedobj` | add-table | see CREATE TABLE below | Serialized snapshot of a deleted row (types: schedules, lab cases, medication-patient links, patient fields, tooth initials) with the deletion time, so consumers can learn about deletions. *Note: Feeds Open Dental's API subscriptions, which are cut; also usable for local sync/audit. Unrelated to the legacy `deletedobject` table, which stays.* | Watchtable subscriptions for deleted records (API section) ([notes](https://www.opendental.com/site/version26_1.html)) | cut-feature |
| `dunning`.`EmailType` | add-column | `tinyint(4) NOT NULL` | Enum EmailType (0 regular, 1 HTML, 2 raw HTML): format of the dunning e-mail. | Dunning messages and templates support HTML (Manage Module, Billing List) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `eformfield`.`DateTimeSig` | add-column | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When the eForm signature was made. | No matching release-note entry; schema-only evidence from the diff doc | core |
| `employee`.`Title` | add-column | `bigint(20) NOT NULL` + index `Title` | FK to definition (new category EmployeeTitles, DefCat 56): the employee's job title. | Employees list: Title field (Lists) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `procnote`.`UserNum2` | add-column | `bigint(20) NOT NULL` + index `UserNum2` | FK to userod. User who made the second signature. | Group Note second signature box (Chart Module) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `procnote`.`Signature2` | add-column | `text DEFAULT NULL` | Second signature, same encoding as Signature. *Note: Nullability copied from the existing procnote.Signature column in the dump.* | Group Note second signature box (Chart Module) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `procnote`.`SigIsTopaz2` | add-column | `tinyint(3) unsigned NOT NULL` | Whether the second signature came from a Topaz pad. *Note: Published type is plain "tinyint" (the doc tool prints tinyint(3) unsigned that way, as for SigIsTopaz); match SigIsTopaz's type in the dump.* | Group Note second signature box (Chart Module) ([notes](https://www.opendental.com/site/version26_1.html)) | core |
| `smstomobile`.`SecUserNumEntry` | add-column | `bigint(20) NOT NULL` + index `SecUserNumEntry` | FK to userod. User logged in when the text was sent. *Note: Open Dental texting is a hosted service and is cut; column kept for data compatibility.* | No matching release-note entry; schema-only evidence from the diff doc | cut-feature |
| `statement`.`IsServiceDateView` | add-column | `tinyint(4) NOT NULL` | Build the statement's main grid in service-date view rather than the transaction view. | Overhauled Service Date View (Account Module) ([notes](https://www.opendental.com/site/version26_1.html)) | core |

New tables in 26.1 (DDL inferred):

```sql
CREATE TABLE `deletedobj` (
  `DeletedObjNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `DateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `ObjType` tinyint(4) NOT NULL,
  `ObjSerialized` mediumtext NOT NULL,
  PRIMARY KEY (`DeletedObjNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
```

### Verification against the full 26.1 schema document

1. **Tables.** The 26.1 document lists 427 tables: the office DB's 422 plus `paysuitepayment`, `paysuitepaymentdetail`, `schoolcourseenrollee`, `schoolcourseinstructor` (25.4) and `deletedobj` (26.1). No office table is missing, so nothing was dropped.
2. **Columns.** For every table, the column count in the 26.1 document (from each table's last `order` attribute) equals the office DB's count plus the additions above. The only exception is `patient`, where the office DB has one extra column, the local `_portal_active_email`.
3. **Types.** For 2,264 columns that are not `bigint(20)` or `tinyint`, the 26.1 document's type was compared with the dump, after applying the documentation tool's own normalisation (for example `tinyint(3) unsigned` is printed as `tinyint`). Every difference is one of the documented 25.4/26.1 changes, except `claimpayment.Note` (drift, see Part 1).
4. **Enum-only entries** in the 25.4 and 26.1 diffs were checked against the dump. Columns such as `carrier.TrustedEtransFlags`, `insplan.InsPlansZeroWriteOffsOn*Override`, `histappointment.ApptSource`, `statement.StatementType` and `sheetfield.FieldType` already exist with the same type, so they are value changes only.

What this does not prove: integer display widths and signedness of `bigint`/`tinyint` columns were compared only by count, not type; index definitions are not in the docs at all.

## Appendix A: enum value changes (no DDL)

These diff entries change the meaning of stored numbers, not the schema. Our code must use the same numeric values to read office data. Values are as printed in the diff docs (`n - description`); "?" means the doc did not print a number. Columns that store enums as strings (`emailmessage.MsgType`, `statement.StatementType`, `eserviceshortguid.EServiceCode`) depend on the name, not the number.

| Version | Column(s) | Enum | Values added (number) |
|---|---|---|---|
| 24.4 | `eserviceshortguid.EServiceCode` | eServiceCode | DeveloperPortalApi 50, DeveloperPortalUI 51, EmailHqService 52 |
| 24.4 | `grouppermission.PermType`, `securitylog.PermType` | EnumPermType | ImageSignatureCreate 264, SignedImageEdit 265 |
| 24.4 | `imagedraw.ImageAnnotVendor` | EnumImageAnnotVendor | BetterDiagnostics 2 |
| 24.4 | `imagedraw.BetterDiagLayer` (new column) | EnumCategoryBetterDiag | None 0 … Sinus 13 (14 values) |
| 24.4 | `pearlrequest.RequestStatus` | EnumPearlStatus | ErrorUploading 5, ErrorProcessing 6, NoResults 7 |
| 24.4 | `periomeasure.SequenceType` | PerioSequenceType | AttGing 8 (attached gingiva) |
| 25.1 | `alertcategorylink.AlertType`, `alertitem.Type` | AlertType | BetterDiagnostics 41, MessageToPayTag 42, MassEmailUpload 43, MassEmailReceipt 44, HQNotification 45 |
| 25.1 | `alertitem.FormToOpen` | FormType | FrmStatementSendSetup 19, FormAdvertisingMassEmailUpload 20 |
| 25.1 | `commlog.CommSource` | CommItemSource | MassEmail 10 |
| 25.1 | `eformfielddef.FieldType` | EnumEFormFieldType | PatientList 8 |
| 25.1 | `eservicelog.EServiceType` | eServiceType | BetterDiag 14, MassEmail 15, Pearl 16 |
| 25.1 | `eservicelog.EServiceAction` | eServiceAction | 118–126 (BetterDiag pass/fail/exception, eClipboard eForm-to-imaging error, mass-email upload/error/receipt, OCR card-scanner error, Pearl image sent) |
| 25.1 | `eserviceshortguid.EServiceCode` | eServiceCode | BetterDiagnostics 53, EmailMassAccess 54 |
| 25.1 | `grouppermission.PermType`, `securitylog.PermType` | EnumPermType | BlockoutsFlagged 266, PayPlanUnlock 267, SendAlertsFromHQ 268 |
| 25.1 | `guardian.Relationship` | GuardianRelationship | None 0 |
| 25.1 | `imagedraw.BetterDiagLayer` | EnumCategoryBetterDiag | RootStumps 14, MissingTooth 15, Disclaimer 17, MarginalDiscrepancy 18 (some older values were removed or renamed) |
| 25.1 | `signalod.IType` | InvalidType | MassEmail 116 |
| 25.1, 25.2 | `eform.Status` | EnumEFormStatus | values removed (names not shown) |
| 25.2 | `apptreminderrule.TypeCur` | ApptReminderType | EClipboardWeb 11 |
| 25.2 | `eformfielddef.FieldType` | EnumEFormFieldType | InsPick 9 |
| 25.2 | `emailmessage.MsgType` (string) | EmailMessageSource | EClipboardWeb |
| 25.2 | `etrans.Etype` | EtransType | Claim_XConnect 36, ClaimAck_XConnect 37 |
| 25.2 | `grouppermission.PermType`, `securitylog.PermType` | EnumPermType | TextAllEmployees 269, ProcTPEditFee 270, EFormImport 271 |
| 25.2 | `sessiontoken.TokenType` | SessionTokenType | TwoFactorAuth 5 |
| 25.2 | `smstomobile.MsgType` | SmsMessageSource | EClipboardWeb 29 |
| 25.3 | `displayfield.Category` | DisplayFieldCategory | AccountModuleServiceDateView 22, ERA 23 |
| 25.3 | `eservicelog.EServiceAction` | eServiceAction | 105, 117, 127–133 (Web Sched existing-patient flow events) |
| 25.3 | `eserviceshortguid.EServiceCode` | eServiceCode | WebSchedExistingPatientUI 55, WebSchedExistingPatientApi 56 |
| 25.3 | `grouppermission.PermType`, `securitylog.PermType` | EnumPermType | ClaimProcFieldsBilledToInsEdit 252, PhoneExtension 272 |
| 25.3 | `insbluebookrule.RuleType` | InsBlueBookRuleType | ProviderFee 5 |
| 25.3 | `schedule.Status` | SchedStatus | Override 1 (HQ use) |
| 25.3 | `sheet.SheetType`, `sheetdef.SheetType` | SheetTypeEnum | Check 28 (check printing) |
| 25.3 | `statement.Mode_` | StatementMode | API 4 |
| 25.3 | `userodpref.FkeyType` | UserOdFkeyType | DefaultMapSetting 28 |
| 25.4 | `alertcategorylink.AlertType`, `alertitem.Type` | AlertType | TreatmentPlanImagesFolderInaccesible 46, PaymentPlanImagesFolderInaccesible 47, CannotCreateOrAccessPatientFolder 48, DefaultEmailNotSet 49, PaySuite 50 |
| 25.4 | `alertitem.FormToOpen` | FormType | FrmPaySuiteReconciliation 21 |
| 25.4 | `carrier.TrustedEtransFlags` | TrustedEtransTypes | None 0, RealTimeEligibility 1 (bit flags; enum newly documented) |
| 25.4 | `clinicerx.EnabledStatus`, `providererx.IsEnabled` | ErxStatus | Pending 6 |
| 25.4 | `creditcard.CCSource`, `payconnectresponseweb.CCSource`, `payment.PaymentSource`, `xwebresponse.CCSource` | CreditCardSource | MeetInTheCloudTerminal 23, MeetInTheCloudPaymentPortal 25, MeetInTheCloudPaymentPortalGuest 26 |
| 25.4 | `entrylog.LogSource`, `securitylog.LogSource` | LogSources | Era835Import 26, ODTouch 27, eClipboard 28 |
| 25.4 | `eservicelog.EServiceType` / `EServiceAction` | eServiceType / eServiceAction | AddressValidation 17 / 134–138 |
| 25.4 | `grouppermission.PermType`, `securitylog.PermType` | EnumPermType | BlockoutEdit 13, SchoolAdminStudentEdit 77, SchoolAdminInstructorEdit 78, SchoolAdminAcesss 81, BlockoutAdd 272, BlockoutDelete 273, PatientSelected 275 |
| 25.4 | `insplan.InsPlansZeroWriteOffsOnAnnualMaxOverride`, `…OnFreqOrAgingOverride` | YN | Unknown 0, Yes 1, No 2 (enum newly documented; columns already in 24.3) |
| 25.4 | `statement.StatementType` (string) | StmtType | Walkout |
| 25.4 | `userodpref.FkeyType` | UserOdFkeyType | DefaultTestingTeamJM 29 (HQ) |
| 26.1 | `alertcategorylink.AlertType`, `alertitem.Type` | AlertType | EServiceListenerError 51 |
| 26.1 | `automationcondition.CompareField` | AutoCondField | PatientProcedureClinicMismatch 14 |
| 26.1 | `branding.BrandingType` | EnumBrandingType | LogoImage 1 (25.2 had LogoFilePath 1; see Gaps) |
| 26.1 | `definition.Category` | DefCat | EmployeeTitles 56 |
| 26.1 | `eserviceshortguid.EServiceCode` | eServiceCode | Chat 57, TranscriptionAI 58 |
| 26.1 | `grouppermission.PermType`, `securitylog.PermType` | EnumPermType | LogScraperHQ 276 |
| 26.1 | `histappointment.ApptSource` | EServiceTypes | SecureEmailService, ODXam, EClipboard, OpenDentalLogScraper, OpenDental32Bit (numbers ?) |
| 26.1 | `imagedraw.ImageAnnotVendor` | EnumImageAnnotVendor | BetterDiagnosticsPatientView 3 |
| 26.1 | `proccodenote.ProcStatus`, `procmultivisit.ProcStatus`, `toothgridcol.ProcStatus`, `toothgriddef.ProcStatus` | ProcStat | None 0 |
| 26.1 | `sheetfield.FieldType`, `sheetfielddef.FieldType` | SheetFieldType | MobileStaticText (number ?) |
| 25.2–26.1 | new tables' enum columns | EnumBrandingType, Relat (inspending), EnumUserStatus, EnumReconciliationStatus, EnumPaySuiteTransTypes, EnumDeletedObjType, EmailType | full value lists are in the diff docs |

Also: in 24.4 the `orionproc` columns `DPC`, `Status2` and `DPCpost` lost their enum annotations in the docs. Their types did not change, so there is nothing to do.

## Gaps and open questions

1. **Inferred DDL for 25.4 and 26.1.** Only types are published. NOT NULL, defaults and indexes follow the 25.3.48 dump's conventions. Check them against a real 26.1 database when one is available.
2. **Index changes on existing columns** (any version) are not in the published docs. To close this for 24.3 → 25.3, create an empty database with our 24.3.63 code and diff its `KEY` lines against `fox-db-schema.sql`.
3. **Version granularity.** The docs are per major.minor (for example "25.3.0.0"). Patch-level timing is not shown, so an item marked 25.3 may have landed in any 25.3.x. The office DB at 25.3.48 matches the 25.3 doc except for `claimpayment.Note`, which suggests that conversion came in a later 25.3 patch or did not run. Likewise, the 26.1 doc may predate 26.1.70.
4. **Enum numbering to verify** before the code is ported: `EnumPermType` 272 is `PhoneExtension` in the 25.3 diff but `BlockoutAdd` in 25.4, and 25.4 lists 13, 77, 78 and 81 as new names (probably renames of old permissions). `EnumBrandingType` 1 is `LogoFilePath` in 25.2 and `LogoImage` in 26.1. Numbers marked "?" were not printed.
5. **Release-note mapping is partial.** Many schema items have no matching release-note entry (marked so, with the diff doc as the only source). Some links are inferences and are labelled as such: `inspending` ↔ Ins Pick eForm field, `definition.Supp` ↔ adjustment-type default percentage, `payterminal.CCIntegration` ↔ Global Payments V2.
6. **Preferences and definitions are out of scope.** New preference rows (for example the mirroring default and `TaskCategoryShow`) and new definition categories (DefCat 56) are data, not schema, and need their own inventory from the manual and release notes.
7. **The 26.2 doc exists** (`OpenDentalDocumentation26-2.xml`), but 26.2 is beyond the 26.1.70 target and was not analysed.
8. **Extraction method.** The XML files were read through a page-reading tool, not downloaded. All six diff documents were reproduced in full and cross-checked. The full 26.1 document was checked by per-table column counts and by the types of non-integer columns. The `patient` table's type list was partly cut at a page boundary, but its column count was checked.
