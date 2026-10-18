# Open Dental 25.2: feature specs for our 24.3-based fork

Status: draft, 2026-10-18. Scope: every change listed for Open Dental 25.2 that touches the Windows desktop program, plus 25.2 changes that appear only in the published database documentation or the 25.2 blog post.

Baseline: our code is upstream Open Dental 24.3.63. Target: schema and behavior parity with 25.2, as one step toward adopting the office's 25.3.48 database. 25.1 items are specced separately; where a 25.2 item builds on 25.1, the dependency is listed as `v25_1-...`.

## Sources and method

| Source | URL | What we used it for |
|---|---|---|
| 25.2 release notes | https://www.opendental.com/site/version25_2.html | The authoritative list of items. The page has no per-item manual links. |
| 25.2 blog post | https://opendental.blog/version-25-2/ | Extra behavior detail for about a dozen items, plus two changes the release page omits. |
| Published database docs | https://www.opendental.com/OpenDentalDocumentation25-2.xml, compared with https://www.opendental.com/OpenDentalDocumentation25-1.xml, and the diff doc https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml | New tables and columns, enum values, and column comments that describe behavior (Ins Pick, Patient List, inspending, sheet signer, task category, XConnect). |
| 25.3 DDL dump | `D:\Dev\opendental-schema\fox-db-schema.sql` | Exact column types and defaults. Every schema line below was checked against it. |
| Public manual | https://www.opendental.com/manual/ | **Frozen at 24.4**: the version picker on each page tops out at "v24.4". Used only to describe the starting behavior. Our code is 24.3, so a manual page can describe a 24.4 change that our code lacks; check the code before calling something "baseline". |
| Public `/site/` pages | e.g. https://www.opendental.com/site/bridgedtxstudio.html | A few current pages are public: bridge setup pages, CareCredit setup, the reports overview. |
| Schema companion spec | `docs/specs/schema-changes-24.3-to-26.1.md` | Full DDL and migration notes for all schema changes; this file only repeats what each feature needs. |

The versioned help (`/help252/` and later) is open only to Open Dental customers on support (a request for a `/help252/` page redirects to a login page). We did not use it, and we did not try to get around the restriction. That is why many items have little public detail. Each item carries a **confidence** grade:

- **High**: public sources describe the behavior: DB comments, `/site/` pages, the blog.
- **Medium**: the release note plus schema or blog hints. Some UI details are our own design.
- **Low**: only the one-line release note. The behavior written here is **our proposed design**. Treat it as a product decision, not as a copy of Open Dental.

Sizes are in engineer time, including tests: **S** ≤ 2 days, **M** 3 to 7 days, **L** 1 to 3 weeks, **XL** more than 3 weeks.

## Cross-cutting rules for every 25.2 item

1. **Enum values are stored as integers and must match Open Dental exactly.** The office's data will contain them. 25.2 adds:
   - `EnumPermType` 269 `TextAllEmployees` (HQ only, reserve), 270 `ProcTPEditFee`, 271 `EFormImport`. Our enum currently ends at 263, so values 264 to 268 from 25.1 must come first.
   - `EtransType` 36 `Claim_XConnect`, 37 `ClaimAck_XConnect`.
   - `EnumEFormFieldType` 8 `PatientList` (added in the 25.1 docs, released as a feature in 25.2) and 9 `InsPick`.
   - `EnumEFormStatus`: `Imported` (3) removed in 25.2; `UnmatchedToPatient` (4) was already removed in 25.1.
   - Reserve-only values for cut eServices: `ApptReminderType` 11, `EmailMessageSource.EClipboardWeb` (stored as a string), `SmsMessageSource` 29, and `SessionTokenType` 5.
2. **Preference names.** Open Dental's internal preference names are not public, except `TaskCategoryShow`, which the DB docs name. For each new preference below we give the UI label and a *proposed* internal name. Before coding, restore a copy of `fox-db` and run `SELECT PrefName, ValueString FROM preference`, then diff that list against our `PrefName` enum. Adopt Open Dental's names wherever a 25.2 preference already exists in the office data, so that migrated settings carry over.
3. **Other string keys that live in office data**: `program.ProgName` for new bridges and `displayreport.InternalName` for new reports. Proposed values are given below; confirm them against the restored `fox-db` before release.
4. **Database conversion**: every schema change goes into our convert script with the exact 25.3 DDL types. Changes are additive, with one rename (`task.TriageCategory` → `Category`). Regenerate the CRUD classes in `OpenDentBusiness/Crud/` with `xCrudGenerator` instead of hand-editing them.

## Summary

Counts: 35 desktop, 4 vendor-direct, 32 excluded (12 Open Dental-service, 20 other, 16 of them API endpoints). The 25.2 release page has 66 items; all 66 are covered. The other 5 IDs come from the blog and the DB docs (core sync, down-payment audit, Amount Unpaid, statement flag, TextAllEmployees). Fact-check pass (2026-10-18): the DentalXChange Patient Credit Score removal moved from excluded to desktop, because it is work we do. Changes made in the 25.2 line by later point releases are listed separately at the end; they are not in these counts.

| ID | Title | Category | Size | Confidence | Depends on |
|---|---|---|---|---|---|
| v25_2-core-schema-enum-sync | 25.2 schema, enum and type sync | desktop | M | High | v25_1 enum sync |
| v25_2-account-xconnect-claim-validation | DentalXChange XConnect claim validation | vendor-direct | L | Medium | core sync |
| v25_2-account-payconnect-saved-card-field | Read-only card field for saved-card PayConnect payments | vendor-direct | S | Medium | PayConnect 2 direct-credential rework (decoupling) |
| v25_2-account-payplan-prepay-attach | Attach Payment Plan Prepay paysplits to patient payment plans | desktop | M | Medium | none |
| v25_2-account-medical-eob-prompt | Preference: prompt for Medical EOB on primary claims | desktop | S | Low | none |
| v25_2-account-preauth-eob-attach | Attach EOBs to preauthorizations | desktop | M | High | core sync |
| v25_2-account-payplan-downpayment-audit | Audit log for payment plan down payment changes | desktop | S | Medium | v25_1 payplancharge.IsDownPayment |
| v25_2-account-prodselect-amount-unpaid | Select Production: "Amount End" becomes "Amount Unpaid" | desktop | S | High | none |
| v25_2-account-statement-show-trans-since-zero | Statements remember "Show transactions since zero balance" | desktop | S | High | none |
| v25_2-appts-provider-timebars-op-background | Preference: provider time bars on operatory background | desktop | M | Low | none |
| v25_2-appts-waiting-room-window | Waiting Room in a separate window | desktop | M | Medium | none |
| v25_2-chart-medical-rows-bold | Bold labels for Med Urgent and Medical Summary rows | desktop | S | High | none |
| v25_2-chart-eform-allergy-other-convert | Convert eForm "Other" allergies to listed allergies | desktop | M | Medium | v25_1 eForm import (24.3 base is enough) |
| v25_2-chart-eform-problem-other-convert | Convert eForm "Other" problems to listed problems | desktop | M | Medium | same as allergy item |
| v25_2-chart-perio-print-birthdate | Birthdate on printed Graphical Perio Chart | desktop | S | High | none |
| v25_2-eforms-patient-list-field | eForm field: Patient List (pick a family member) | desktop | L | High | core sync |
| v25_2-eforms-ins-pick-field | eForm field: Ins Pick, plus pending insurance (inspending) | desktop | XL | High | patient-list field, core sync |
| v25_2-eforms-patient-forms-window | Patient Forms window improvements | desktop | M | Low | none |
| v25_2-eforms-remove-imported-status | Remove the Imported status from eForms | desktop | S | High | core sync |
| v25_2-family-edit-benefit-ui | Edit Benefit window redesign | desktop | M | Low | coordinate with v25_3 Other Benefits UI |
| v25_2-family-iaplus-bridge-update | Insurance Answers Plus bridge becomes a web launcher | vendor-direct | S | High | none |
| v25_2-family-ins-history-edit-remove | Insurance History: edit or remove entered dates | desktop | M | Medium | v25_1 two dates per category |
| v25_2-manage-task-filter-unassigned | Task Filter: "Unassigned" clinic and region | desktop | S | Medium | none |
| v25_2-manage-task-category | Task Category for all offices (TaskCategoryShow) | desktop | M | High | core sync |
| v25_2-manage-task-filter-patient | Task Filter: pick a specific patient | desktop | S | High | none |
| v25_2-manage-era-carc-logic | More logic for "Don't auto-process claims w/these CARCs" | desktop | M | Low | v25_1 ERA auto-split by CARC |
| v25_2-bridges-dtx-studio | DTX Studio and DTX Studio Acquire bridges | vendor-direct | M | High (setup) / Low (launch syntax) | none |
| v25_2-reports-ins-pending-supplementals | Insurance Pending Supplementals report | desktop | M | Medium | none |
| v25_2-security-domain-login-change-user | Domain login when changing user (proc note, group note, sheets) | desktop | M | Medium | v25_1 change user on sheet signatures |
| v25_2-security-permission-renames | Permission display-name updates | desktop | S | Low (full list) | none |
| v25_2-security-tp-proc-edit-fee | Permission: Treatment Planned Procedure Edit Fee | desktop | S | High | core sync |
| v25_2-server-replication-delay | Preferences: read/write delay with replication | desktop | M | Low | none |
| v25_2-wiki-readonly-offline | Read-only wiki usable while the program is closed | desktop | L | Low | none |
| v25_2-tools-dbm-invalid-chars-gdi | DB Maintenance tool: Invalid Characters (GDI+) | desktop | M | Low | none |
| v25_2-wiki-search-limit | Wiki Search: limit the number of results | desktop | S | Medium | none |
| v25_2-security-audit-patient-clinic | Audit Trail logs patient clinic changes | desktop | S | High | none |
| v25_2-ui-blur-module-switch | Preference: blur when switching modules | desktop | S | Low | none |
| v25_2-imaging-copy-without-drawings | Copy images without drawings or annotations | desktop | S | High | none |
| v25_2-bridges-dxc-credit-score-removed | Remove the DentalXChange Patient Credit Score bridge | desktop | S | High | none |
| v25_2-eservices-eclipboard-web | eClipboard Web (browser forms) | excluded-od-service | - | - | - |
| v25_2-eservices-eclipboard-web-links | Patient-specific eClipboard Web links | excluded-od-service | - | - | - |
| v25_2-imaging-betterdiagnostics | BetterDiagnostics AI integration | excluded-od-service | - | - | - |
| v25_2-imaging-betterdiagnostics-filter | Filter BetterDiagnostics annotations by pathology | excluded-od-service | - | - | - |
| v25_2-eservices-massemail-upload | Mass Email: upload lists to Mailchimp | excluded-od-service | - | - | - |
| v25_2-eservices-massemail-commlogs | Mass Email: commlog recipients | excluded-od-service | - | - | - |
| v25_2-eforms-frequency-considers-sheets | eForm frequency rules count Sheets | excluded-od-service | - | - | - |
| v25_2-eservices-secure-email-unsubscribe | Secure Email unsubscribe | excluded-od-service | - | - | - |
| v25_2-eservices-websched-recall-secure-email-setup | Secure Email option moved to Web Sched Recall Setup | excluded-od-service | - | - | - |
| v25_2-eservices-websched-recall-secure-email-clinic | Secure Email for Web Sched Recall per clinic | excluded-od-service | - | - | - |
| v25_2-eservices-msgtopay-secure-email | Message-to-Pay via Secure Email | excluded-od-service | - | - | - |
| v25_2-eservices-msgtopay-receipt-template | Message-to-Pay receipt email template | excluded-od-service | - | - | - |
| v25_2-account-carecredit-merchant-fee-adj | CareCredit merchant fee adjustments | excluded-other | - | - | - |
| v25_2-account-carecredit-self-service | CareCredit Self-Service link | excluded-other | - | - | - |
| v25_2-account-carecredit-net-amount | CareCredit Transactions: Net Amount column | excluded-other | - | - | - |
| v25_2-security-text-all-employees-perm | HQ-only permission TextAllEmployees | excluded-other | - | - | - |
| v25_2-api-* (16 items) | Local REST API changes | excluded-other | - | - | - |

---

## Core

### v25_2-core-schema-enum-sync: 25.2 schema, enum and type sync

- **Category:** desktop · **Size:** M · **Confidence:** High
- **Sources:** https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml, https://www.opendental.com/OpenDentalDocumentation25-2.xml, `fox-db-schema.sql`, `docs/specs/schema-changes-24.3-to-26.1.md`.

**Purpose.** This item does the plumbing that the feature items assume. Do it first, so that a migrated 25.3 database can open in our build even before every feature exists.

**Schema (25.3 DDL types):**

| Change | DDL | Feature owner |
|---|---|---|
| add `clearinghouse.EnableXConnect` | `tinyint(4) NOT NULL DEFAULT 0` | xconnect item |
| add `eobattach.ClaimNumPreAuth` | `bigint(20) NOT NULL` (default 0) + `KEY ClaimNumPreAuth` | preauth EOB item |
| add `sheetfield.UserSigned` | `bigint(20) NOT NULL` (default 0) + `KEY UserSigned` | domain-login item |
| rename `task.TriageCategory` → `Category`, same for `taskhist` | `bigint(20) NOT NULL DEFAULT 0`; keep the index name `TriageCategory` as in the dump | task category item |
| add `statement.ShowTransSinceBalZero` | `tinyint(4) NOT NULL` | statement item |
| add `eformfield.ValueLabelEnglish`, `eformfield.PickListVisEnglish` | both `text NOT NULL` (25.2 made the second one `varchar(255)` and 25.3 widened it; use `text`) | eForm translation (below) |
| add `eclipboardsheetdef.SheetDefNumsConsidered` | `varchar(255) NOT NULL` | column only; the feature is cut |
| add `inssub.SecurityHash` | `varchar(255) NOT NULL` | decoupling plan: HMAC-SHA256 like the other `SecurityHash` columns |
| add table `inspending` | see the Ins Pick item | Ins Pick item |
| add table `branding` | `BrandingNum bigint(20) AUTO_INCREMENT PK, BrandingType tinyint(4), ClinicNum bigint(20) KEY, ValueString text, DateTimeUpdated datetime DEFAULT '0001-01-01 00:00:00'` | table only (eClipboard branding is cut) |
| widen `insplan.GroupNum` | `varchar(25)` → `varchar(50)` | also raise the `MaxLength` of the group number textbox in `FormInsPlan` |
| retype `histappointment.ProcDescript` | `varchar(255)` → `text` | add `[CrudColumn(SpecialType=CrudSpecialColType.IsText)]` to `HistAppointment.ProcDescript` |
| add `appointment.IsMirrored`, `histappointment.IsMirrored`, `apptview.ShowMirroredAppts` | `tinyint(4) NOT NULL` | **Columns only.** The diff doc puts them in 25.2, but the mirroring feature shipped in 25.3; see the 25.3 spec. |

**Enum work:** everything in cross-cutting rule 1. Also:

- Rename the `DefCat.TaskCategories` description from "Task Categories HqOnly" to "Task Categories".
- Mark `InvalidType.UserClinics` (67) and `InvalidType.UserOdPrefs` (100) as unused. Keep the numbers.

**eForm translation fields.** When an eForm is created from a translated def, save the English label and the English pick-list items in the new `...English` columns. Conditional logic, where a child field shows only for a given parent answer, must compare against the English values, so it keeps working after translation. If our 24.3 eForms have no translation support yet, simply copy `ValueLabel` and `PickListVis` into the English columns on insert.

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (`EnumPermType`, ends at 263 `SuperFamilyDisband`), `OpenDentBusiness/TableTypes/Etrans.cs`, `OpenDentBusiness/TableTypes/EFormField.cs` (`EnumEFormFieldType`, line ~122), `OpenDentBusiness/TableTypes/EForm.cs` (`EnumEFormStatus`, line ~62), `OpenDentBusiness/TableTypes/Def.cs` (line ~233), `OpenDentBusiness/TableTypes/HistAppointment.cs` (line ~63), `OpenDentBusiness/TableTypes/InsPlan.cs`, `OpenDentBusiness/TableTypes/Clearinghouse.cs`, `OpenDentBusiness/TableTypes/EobAttach.cs`, `OpenDentBusiness/TableTypes/SheetField.cs`, `OpenDentBusiness/TableTypes/Statement.cs`, `OpenDentBusiness/TableTypes/Task.cs`, `OpenDentBusiness/TableTypes/TaskHist.cs`, `OpenDentBusiness/Crud/*` (regenerate), and the convert script in `OpenDentBusiness/Misc/ConvertDatabases*.cs`.

---

## Account Module

### v25_2-account-xconnect-claim-validation: DentalXChange XConnect claim validation

- **Category:** vendor-direct · **Size:** L · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Account Module), https://opendental.blog/version-25-2/ (XConnect), https://www.opendental.com/site/dentalxchange.html, https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml (`clearinghouse.EnableXConnect`, `EtransType` 36/37), baseline https://www.opendental.com/manual/eclaimsclaimconnect.html.

**What already exists in 24.3.** `OpenDentBusiness/Eclaims/ClaimConnect.cs` already has an `XConnect` REST client (line ~1194). It sends `username`, `password` and an `API-Key` header, and today it is used only for claim attachments. `XConnect.IsEnabled()` (line ~1216) treats any non-blank `clearinghouse.LocationID` on a ClaimConnect clearinghouse as "XConnect on". `XConnect.ValidateClaim()` (line ~1271) posts to `/claims/validation`.

**User-visible behavior (25.2):**

- An office that has signed up with DentalXChange enters its XConnect API key in Clearinghouse Setup and turns XConnect on.
- From then on, a claim is validated automatically with DentalXChange when it is **saved** in Edit Claim and when it is **sent**.
- Validation problems open a new window that lists each problem, with its detail and how to fix it.
- Rows that still need attention are yellow. They turn white once the problem is resolved, which we detect when a re-validation no longer returns that item.

**UI:**

- **Clearinghouse Setup** (`FormClearinghouseEdit`), visible only when the comm bridge is ClaimConnect:
  - New checkbox **Enable XConnect** (`EnableXConnect`). With clinics on, it is stored on each clinic's clearinghouse override row, like other per-clinic fields.
  - The **XConnect API Key** text field. We keep the key in `LocationID`, which is where 24.3 already reads it from. Note that the 25.2 DB comment on `LocationID` no longer mentions XConnect (see open questions).
  - `IsEnabled()` changes to: comm bridge is ClaimConnect **and** `EnableXConnect` **and** the key is not blank.
- **New window "Claim Validation"** (`FormXConnectValidation`):
  - Header: patient, claim date, carrier.
  - Grid columns: Severity (Error/Warning), Location (loop or field returned by the API), Message, How to fix.
  - Unresolved rows have a yellow background. Rows the latest re-validation no longer returns turn white with a "Resolved" marker.
  - Buttons: **Edit Claim** (focuses the claim; when opened from Send Claims, opens `FormClaimEdit`), **Re-validate**, **Send Anyway** (only for warnings, and only from the send path), **Close**.

**Business rules:**

- **On save:** validation never blocks saving. If the API fails or times out (about 10 seconds), save the claim and show a non-modal note.
- **On send:**
  - Claims with Error-severity results stay in the queue unsent, and the window opens for them.
  - Warning-only results can be sent after confirmation.
  - In batch sends, validate each claim and show one combined window grouped by claim.
- Validate only primary, secondary and tertiary claims. The 24.3 code already refuses other claim types; preauths are skipped.
- Every validation call is logged as an `etrans` row: request as `Claim_XConnect` (36), response as `ClaimAck_XConnect` (37), linked as an ack. This mirrors how `ClaimConnect.ValidateClaim` logs today (line ~784).
- Credentials are the office's own DentalXChange login and API key. No Open Dental servers are involved.

**Permissions and logging:** no new permission. Turning XConnect on or off is an edit to Clearinghouse Setup, which already requires the Setup permission. Log it with `SecurityLogs.MakeLogEntry(EnumPermType.Setup, 0, "XConnect enabled/disabled for clearinghouse X, clinic Y")`.

**Preferences:** none.

**Schema:** `clearinghouse.EnableXConnect tinyint(4) NOT NULL DEFAULT 0`. On conversion, set it to 1 wherever `LocationID` is not blank on a ClaimConnect row, so that offices already using XConnect attachments keep working.

**Touchpoints:** `OpenDentBusiness/Eclaims/ClaimConnect.cs` (XConnect class, ~1194 to 1300), `OpenDentBusiness/Eclaims/Eclaims.cs` (send path), `OpenDentBusiness/TableTypes/Clearinghouse.cs`, `OpenDentBusiness/Data Interface/Clearinghouses.cs` (clinic override merge, ~418), `OpenDental/Forms/FormClearinghouseEdit.cs` (+ designer; `textLocationID` ~88/302/352), `OpenDental/Forms/FormClaimEdit.cs` (save), `OpenDental/Forms/FormClaimsSend.cs`, `OpenDental/Logic/ClaimL.cs`, `OpenDentBusiness/TableTypes/Etrans.cs`, plus the attachment forms that already branch on XConnect (`FormClaimAttachHistory.cs`, `FormClaimAttachPasteDXC.cs`, `FormClaimAttachSnipDXC.cs`).

**Depends on:** core sync (enum 36/37, column).

### v25_2-account-payconnect-saved-card-field: read-only card field for saved-card PayConnect payments

- **Category:** vendor-direct · **Size:** S · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Account Module), baseline https://www.opendental.com/manual/payconnectpaymentwindow.html.

**Behavior.** When a payment is charged to a **saved card** (a token chosen in the Payment window) through PayConnect, the PayConnect window shows a read-only **Credit Card** field with the masked number, for example `XXXXXXXXXXXX1234`. The user can then see which card will be charged before clicking OK. The legacy `FormPayConnect` already does this through `textCardNumber.ReadOnly`; the change applies it to the PayConnect 2.0 window.

**UI:**

- `FormPayConnect2`: add a label "Credit Card" and a read-only textbox above the terminal and web-service choice.
- The field is visible only when a saved card was passed in (`_creditCard != null`) and shows `CCNumberMasked`.
- When a card is being entered (iFrame) or a terminal is used, hide the field.

**Rules:**

- Never show more than the last 4 digits.
- If the saved card has no masked number, show "(saved card)".
- Refunds and voids against a saved card show the field the same way.

**Permissions and logging:** unchanged. The existing `CreditCardTerminal` and payment logs still apply.

**Schema and preferences:** none.

**Touchpoints:** `OpenDental/Forms/FormPayConnect2.cs` (+ `.Designer.cs`), `OpenDental/Forms/FormPayment.cs` (where the saved card is chosen), `OpenDentBusiness/WebBridges/PayConnect2.cs`.

**Dependency note.** In 24.3, PayConnect 2 builds its OAuth URL from the Open Dental registration key (`PayConnect2.cs` ~33/49), which goes through Open Dental. The decoupling work must first move PayConnect 2 to credentials issued directly by DentalXChange. This UI item does not depend on that technically, but it can't ship in a decoupled build until that is done.

### v25_2-account-payplan-prepay-attach: attach Payment Plan Prepay paysplits to patient payment plans

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Account Module), baseline https://www.opendental.com/manual/unearnedprepayment.html and https://www.opendental.com/manual/paymentplandynamic.html. Related later change: "Allow attaching paysplits to Payment Plans in more scenarios" (25.3, backported to 25.2.55), https://www.opendental.com/site/version25_3.html.

**Baseline (24.3).**

- In the Paysplit window, choosing any unearned type detaches the split from its payment plan and disables the payment plan checkbox (`FormPaySplitEdit.comboUnearnedTypes_SelectionChangeCommitted`, ~151 to 173).
- Dynamic payment plans already keep a hidden unearned bucket for plan prepayments. Its type is the preference `DynamicPayPlanPrepaymentUnearnedType`. `PayPlanEdit` sums and moves that money (~1251 to 1271, 1507, 1692).

**Behavior (25.2).** When the selected unearned type is the payment-plan prepayment type, the split **can** be attached to a patient payment plan. The money is then held as a prepayment on that plan and applied to its future charges by the existing dynamic-plan logic.

**UI (Paysplit window):**

- The unearned-type combo lists the payment-plan prepay type even though the def is hidden, because the user needs it here.
- When that type is selected:
  - keep `checkPayPlan` enabled and do not detach the plan;
  - the plan picker lists only **patient** payment plans for the family, never insurance payment plans;
  - the provider stays 0 unless `AllowPrepayProvider` is on, as today.
- Any other unearned type keeps today's behavior and detaches the plan.

**Rules and edge cases:**

- A split may not have both a procedure (`ProcNum`) and the prepay type. Prepayment means unattached production.
- Attaching to a **closed** plan asks the user to confirm. Attaching to a **locked** plan is allowed, because payments to locked plans are normal.
- If the plan is later deleted, these splits become unattached prepayments of the same type. The plan-delete code already moves splits off a deleted plan; check that it handles this type.
- Database Maintenance: the check at `DatabaseMaintenances.cs` ~12257 assumes `UnearnedType=0 AND PayPlanNum=0` in places. Review every DBM method that flags "unearned split attached to payment plan" so that these splits are not reported or "fixed".
- Income transfers must keep ignoring this type. Hiding it from transfers is the whole point of the hidden type.

**Permissions and logging:** unchanged (`PaymentCreate`/`PaymentEdit`).

**Preferences:** uses the existing `DynamicPayPlanPrepaymentUnearnedType`. Nothing new.

**Schema:** none.

**Touchpoints:** `OpenDental/Forms/FormPaySplitEdit.cs` (~61 to 75, 151 to 173, 476, 557), `OpenDental/Forms/FormPayment.cs`, `OpenDentBusiness/Db Multi Table/PayPlanEdit.cs`, `OpenDentBusiness/Db Multi Table/PaymentEdit.cs`, `OpenDentBusiness/Data Interface/Payments.cs` (~1113), `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`.

### v25_2-account-medical-eob-prompt: preference to prompt for a Medical EOB on primary claims

- **Category:** desktop · **Size:** S · **Confidence:** Low (only the release-note line is public)
- **Sources:** https://www.opendental.com/site/version25_2.html (Account Module), baseline https://www.opendental.com/manual/medicalins.html and https://www.opendental.com/manual/claimpaymenteobscan.html.

**Baseline (24.3), found in the fact-check.** A closely related preference already exists:

- Preferences > Account > Claim Receive has a **Medical** group with the checkbox "Prompt for primary claims" (`ClaimMedReceivedPromptForPrimaryClaim`, bool, default true; `UserControlAccountClaimReceive.cs` ~72 and ~93, designer ~55 to 70).
- When a **medical or institutional** claim is received, it looks for the patient's primary **dental** claims that are still Unsent or "Hold Until Pri Received" and offers actions on them. This runs in three places: `FormClaimEdit` ~4148, `FormEtrans835ClaimPay` ~811 (ERA) and `ClaimL` ~316 (batch receive).
- The medical-insurance manual page describes the same flow.

The new 25.2 preference most likely extends this flow. In the usual case the medical plan pays first, and the dental carrier then wants the medical EOB with the dental claim.

**Proposed behavior (our design).** With the new preference on:

- When the existing flow above shows the primary dental claims after a medical claim is received, also ask, for each dental claim picked to send: "Attach the medical EOB to this claim?"
- **Yes** opens the dental claim's attachment step: the Attachments tab of `FormClaimEdit`, or the DentalXChange attachment window when the clearinghouse uses it.
  - If the medical claim's payment already has EOB images (`eobattach` for that `ClaimPaymentNum`), offer them first.
  - Otherwise offer Scan/Import.
- **No** continues without attaching.
- The prompt never blocks sending.

**UI:**

- Preferences > Account > Claim Receive, **Medical** group: new checkbox **Prompt for Medical EOB on primary claims**, under "Prompt for primary claims".
- Proposed internal name `ClaimMedReceivedPromptForMedicalEob`; bool, default false. Confirm the name against the restored `fox-db` (cross-cutting rule 2).

**Rules:**

- Fire only when a claim whose `MedType` is Medical or Institutional is received, which is the same trigger as the existing preference.
- The prompt is about the patient's **ordinal-1 dental** claims (`MedType` Dental) that are Unsent or "Hold Until Pri Received".
- It works even when "Prompt for primary claims" is off: then it asks only about the EOB.
- Never prompt during ERA **auto**-processing, which has no UI. Prompt when the ERA is processed by hand (`FormEtrans835ClaimPay`).
- With a batch insurance payment, ask once per dental claim, not once per procedure.

**Permissions:** none new. Attaching uses the existing claim-attachment and image permissions. **Schema:** none.

**Touchpoints:** `OpenDental/User Controls/Preferences/UserControlAccountClaimReceive.cs` (+ `.Designer.cs`), `OpenDentBusiness/TableTypes/Pref.cs` (next to `ClaimMedReceivedPromptForPrimaryClaim` ~603), `OpenDental/Forms/FormClaimEdit.cs` (~4148; Attachments tab), `OpenDental/Forms/FormEtrans835ClaimPay.cs` (~811), `OpenDental/Logic/ClaimL.cs` (~316), `OpenDentBusiness/Data Interface/EobAttaches.cs`, `OpenDental/Forms/FormClaimAttachHistory.cs` (DentalXChange attachments).

### v25_2-account-preauth-eob-attach: attach EOBs to preauthorizations

- **Category:** desktop · **Size:** M · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), https://opendental.blog/version-25-2/ (Attaching EOBs to Preauthorizations), https://www.opendental.com/OpenDentalDocumentation25-2.xml (`eobattach.ClaimNumPreAuth`), baseline https://www.opendental.com/manual/preauth.html.

**Behavior.**

- When receiving a preauth, **By Procedure** opens the Enter Estimates for PreAuth window (`FormClaimPayPreAuth`). That window gets a **Scan EOB** button that scans or imports the carrier's response into the EOB store.
- Afterwards the Edit Preauthorization window has a **View EOB** button that opens the attached images.
- This mirrors how claim-payment EOBs work.

**UI:**

- `FormClaimPayPreAuth`: add an **EOB scanned:** Yes/No read-only label and a button. The button text is "Scan EOB" when nothing is attached and "View EOB" when something is, the same pattern as `FormClaimPayBatch` ~96 to 103.
- `FormClaimEdit` in preauth mode: add a **View EOB** button, enabled when at least one EOB is attached.
- The imaging window in EOB mode (`FormImages`) takes either a claim payment or a preauth claim.

**Rules:**

- Each `eobattach` row has exactly one owner: either `ClaimPaymentNum` > 0 with `ClaimNumPreAuth` = 0, or the reverse.
- Files go in the A-to-Z `EOBs` folder, named as today, or in `RawBase64` when there is no A-to-Z folder.
- Deleting a preauth (`FormClaimEdit.butDelete_Click` ~3809, then `ClaimL.DeleteClaimHelper`, which calls `Claims.Delete` at `ClaimL.cs` ~583) also deletes its EOB rows and files, after one extra confirmation that names the number of EOBs. Model the cleanup on `FormClaimEdit` ~2366, where the EOBs of a cancelled new claim payment are removed before the payment is deleted.
- Deleting a preauth that has EOBs needs the user to also have `ImageDelete`. Otherwise block the delete with a message.
- Converting a preauth into a claim does **not** move the EOBs.

**Permissions and logging:** use the same checks as claim EOBs. Log `ImageCreate`/`ImageDelete` entries with the PatNum and "Preauth EOB" in the text.

**Schema:** `eobattach.ClaimNumPreAuth bigint(20) NOT NULL` (default 0) + `KEY ClaimNumPreAuth`.

**Touchpoints:** `OpenDentBusiness/TableTypes/EobAttach.cs`, `OpenDentBusiness/Data Interface/EobAttaches.cs` (add `RefreshForPreAuth`, `ExistsForPreAuth`; `Delete`), `OpenDentBusiness/Crud/EobAttachCrud.cs`, `OpenDentBusiness/Imaging/ImageStore.cs` (~722 to 777, ~1198), `OpenDental/Forms/FormClaimPayPreAuth.cs`, `OpenDental/Forms/FormClaimEdit.cs`, `OpenDental/Forms/FormImages.cs` (line ~14 `ClaimPaymentNum`), `OpenDental/Main Modules/ControlImages.cs` (`ModuleSelectedClaimPayment`), `OpenDental/Main Modules/ControlImagesOld.cs` (~1073 to 1108). The `ClaimPayments.cs` ~57 `hasEobAttach` query stays as is.

**Depends on:** core sync.

### v25_2-account-payplan-downpayment-audit: audit log for payment plan down payment changes

- **Category:** desktop · **Size:** S · **Confidence:** Medium
- **Sources:** https://opendental.blog/version-25-2/ (Audit Trail for Payment Plans). The release page does not list it.

**Behavior.** Changing or removing a payment plan's down payment writes an Audit Trail entry under `PayPlanEdit` whose text starts with "Down Payment Changed", followed by the old and new amounts and the PayPlanNum.

**Rules:**

- Log when the down-payment amount in the plan terms changes and is saved.
- Log when down-payment charges (`payplancharge.IsDownPayment`, a 25.1 column) are deleted or edited.
- Do not log when nothing changed.

**Touchpoints:** `OpenDental/Forms/FormPayPlanDynamic.cs`, `OpenDental/Forms/FormPayPlan.cs`, `OpenDental/Forms/FormPayPlanChargeEdit.cs`, `OpenDentBusiness/Data Interface/PayPlanCharges.cs` (`GetForDownPayment` ~14), `OpenDentBusiness/Data Interface/SecurityLogs.cs`.

**Depends on:** v25_1 `payplancharge.IsDownPayment`.

### v25_2-account-prodselect-amount-unpaid: "Amount End" becomes "Amount Unpaid"

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://opendental.blog/version-25-2/ (Miscellaneous).

**Behavior.** In the Select Production window used when adding production to a payment plan, the column header "Amount End" is renamed "Amount Unpaid". Only the label changes, not the meaning.

**Touchpoints:** `OpenDental/Forms/FormProdSelect.cs` line ~41 (`"Amount\r\nEnd"` → `"Amount\r\nUnpaid"`), plus the translation key.

### v25_2-account-statement-show-trans-since-zero: statements remember "Show transactions since zero balance"

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/OpenDentalDocumentation25-2.xml (`statement.ShowTransSinceBalZero` comment). No release-note line.

**Behavior.**

- Each statement stores the state of its "Show transactions since zero/negative balance" checkbox, so reopening a saved statement shows the setting it was made with.
- New statements take the default from the existing preference `BillingShowTransSinceBalZero`.
- Statements created from Billing follow the checkbox in Billing Options, which overrides the preference.

**Touchpoints:** `OpenDental/Forms/FormStatementOptions.cs`, `OpenDentBusiness/Data Interface/Statements.cs` (~1108 reads the preference today; read the statement's own value instead), `OpenDentBusiness/Logic/BillingL.cs` (~893, ~1087).

**Schema:** `statement.ShowTransSinceBalZero tinyint(4) NOT NULL`. On conversion, back-fill existing rows from the current preference value.

---

## Appointments Module

### v25_2-appts-provider-timebars-op-background: provider time bars on operatory background

- **Category:** desktop · **Size:** M · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Appointments Module), baseline https://www.opendental.com/manual/timebars.html.

**Baseline (checked against the manual and the code).** The schedule has three kinds of time bar:

- **Provider time bars**: narrow columns at the far left, one per provider in the view. Scheduled provider time is painted in the open color (`DrawProvScheds` ~3040). Provider time from appointments' time patterns is painted in the provider's color (`DrawProvBarsAppts` ~2992).
- **Operatory time bars**: in the **day** view only, each operatory column gets up to two narrow bars on its left edge, in the primary and secondary provider colors, for provider schedules assigned to that operatory (`DrawMainBackground`, ~2820 to 2840). The week view does not draw them.
- **Appointment time bars**: on the left edge of each appointment.

The operatory column itself paints scheduled ("open") time in one generic color, whichever provider is scheduled there (`ControlApptPanel.DrawMainBackground` ~2722).

**Proposed behavior (our design).** With the new preference on:

- Each operatory column paints scheduled time in a **light tint of the provider's appointment color**, for the provider whose schedule is assigned to that operatory, instead of the generic open color.
- Staff can then see who works in which operatory without looking at the narrow bars.
- The left provider bars and the narrow operatory time bars still show. The tint also applies in the week view, which has no operatory time bars today.

**Alternative reading.** The preference name says "provider time bars", which the manual uses for the far-left bars built from appointment time patterns. Under that reading, each provider's busy time from those bars would be drawn on the background of the operatory where the provider is scheduled. Decide between the two readings before building (open question 4).

**UI:** Preferences > Appts > Appearance: checkbox **Provider time bars show on Operatory background**. Proposed internal name `ApptProvTimeBarsOnOpBackground`; bool, default false.

**Rules:**

- Use the provider schedule rows assigned to the operatory, the same source `Schedules.GetProvSchedsForOp` uses today.
- If two providers overlap in the same operatory and time, split the column width between them.
- Hygienists use their own color.
- Holidays, blockouts and closed time draw on top exactly as today.
- Printing: tint only in full-color print mode. In "less color" and grayscale modes keep today's output.
- The tint lightens the provider color by about 70% toward white, so that appointment text stays readable.

**Permissions and schema:** none.

**Touchpoints:** `OpenDental/UI/Appt/ControlApptPanel.cs` (`DrawMainBackground` ~2722, weekly and daily branches, operatory time bars ~2820 to 2840; `DrawProvBarsAppts` ~2992; `DrawProvScheds` ~3040), `OpenDental/User Controls/Preferences/UserControlApptAppearance.cs`, `OpenDentBusiness/Data Interface/Schedules.cs`, `OpenDentBusiness/TableTypes/Pref.cs`.

### v25_2-appts-waiting-room-window: Waiting Room in a separate window

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Appointments Module), https://opendental.blog/version-25-2/ (new Wait Rm button), baseline https://www.opendental.com/manual/waitingroom.html.

**Behavior.** A new **Wait Rm** button in the Appointments Module opens the Waiting Room list in its own non-modal window. Users can move it to another monitor or keep it open while working in other modules. The docked grid in the lower right stays.

**UI:**

- Button "Wait Rm" next to the existing waiting-room grid, or on the Appointments toolbar.
- The window has the same grid as the docked one: patient name in the view's Waiting Room Name Format, and wait time.
- Rows past the alert time use the alert text color.
- A single instance: clicking again brings the window to the front.
- Size and position are remembered per workstation.

**Rules:**

- Same data and filters as the docked grid: the same day; Time Arrived set and Time Seated not set; filtered by the current clinic, and by view when `WaitingRoomFilterByView` is on. When Headquarters is the selected clinic, patients from all clinics show, as the manual describes for the docked grid.
- Refresh on the existing 60-second timer and on appointment signals.
- Double-clicking a row selects that patient in the main window and jumps to the appointment.
- The window stays open when the user switches modules and closes on log off or user change.

**Preferences:** existing `WaitingRoomFilterByView`, `WaitingRoomAlertTime`, `WaitingRoomAlertColor`. **Schema:** none.

**Touchpoints:** `OpenDental/Main Modules/ControlAppt.cs` (`timerWaitingRoom_Tick` ~2034, `gridWaiting` ~2869, `RefreshWaitingRoomTable` ~3325, `FillWaitingRoom` ~3475), `OpenDental/UI/Appt/ControlApptPanel.cs` (`TableWaitingRoom`), `OpenDentBusiness/Data Interface/Appointments.cs` (`GetPeriodWaitingRoomTable`), and a new `FormWaitingRoom` (or WPF `FrmWaitingRoom`).

---

## Chart Module

### v25_2-chart-medical-rows-bold: bold labels for Med Urgent and Medical Summary

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Chart Module), baseline https://www.opendental.com/manual/medical.html.

**Behavior.** In the Chart Module patient-info grid, the label cells of the **Med Urgent** and **Medical Summary** rows become bold, matching the Medications, Allergies and Problems header rows. The values and colors don't change: Med Urgent text stays red and bold.

**Touchpoints:** `OpenDental/Main Modules/ControlChart.cs` `FillPtInfo` (~4042). The first cell is created at ~4077 to 4085; the `"Med Urgent"` case is ~4262 and `"Medical Summary"` ~4273. Build the label as a `GridCell` with `Bold=YN.Yes` for these two rows.

### v25_2-chart-eform-allergy-other-convert: convert eForm "Other" allergies to listed allergies

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Chart Module), baseline https://www.opendental.com/manual/allergiespatient.html and https://www.opendental.com/manual/allergiesmaster.html.

**Baseline.** When an imported eForm lists an allergy that matches no allergy definition, the import attaches the patient to a catch-all allergy definition named "Other" and stores the typed text in the allergy's Reaction field (`EFormImport.cs` ~48 to 100).

**Behavior.** Staff can turn such an "Other" entry into a real allergy from the Allergy list.

**UI:**

- In the patient's Allergy edit window (`FormAllergyEdit`), when the allergy's definition is "Other", show the typed text as "Patient entered: …" and a **Convert** button.
- **Convert** opens the Allergy List (`FormAllergySetup`) in selection mode with the search box pre-filled with the typed text. The user picks an existing definition, or clicks Add to create one; the description defaults to the typed text.
- In the Medical window, Allergies tab (`FormMedical`): a right-click item **Convert "Other" allergy…** on such rows, which opens the same flow.

**Rules:**

- After conversion the record points to the chosen definition. The typed text moves into the note area (Reaction) only if the user ticks "Keep entered text as reaction"; by default it is cleared.
- If the patient already has an **active** allergy with the chosen definition, ask whether to merge: the "Other" row is set inactive and the existing row is kept.
- Converting doesn't touch other patients who have the same typed text. The office can repeat it per patient.

**Permissions and logging:**

- Requires `PatAllergyListEdit`. Creating a new definition also requires `AllergyDefEdit`.
- Log: `PatAllergyListEdit`, "Allergy 'Other: <text>' converted to '<def>'".

**Schema:** none.

**Touchpoints:** `OpenDental/Forms/FormAllergyEdit.cs`, `OpenDental/Forms/FormAllergySetup.cs`, `OpenDental/Forms/FormMedical.cs`, `OpenDentBusiness/Data Interface/Allergies.cs`, `OpenDentBusiness/Data Interface/AllergyDefs.cs`, `OpenDentBusiness/EForms/EFormImport.cs` (reference only).

### v25_2-chart-eform-problem-other-convert: convert eForm "Other" problems to listed problems

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Chart Module), baseline https://www.opendental.com/manual/problempatient.html and https://www.opendental.com/manual/problemmaster.html.

**Baseline.** Unmatched typed problems are saved as a disease with the catch-all definition "Other", with the typed text in `PatNote` (`EFormImport.cs` ~526 to 575).

**Behavior and UI.** The same pattern as the allergy item:

- In the problem edit window (`WpfControlsOD/Frms/FrmDiseaseEdit.xaml.cs`), an "Other" problem shows the patient's text and a **Convert** button.
- **Convert** opens the Problem list (`FormDiseaseDefs`) in selection mode, pre-searched, with an Add option.
- The Medical window's Problems tab gets the same right-click entry.

**Rules:**

- Keep the problem's status, start date and stop date.
- Merge with an existing **active** problem of the chosen definition if the user confirms; the "Other" row becomes Resolved/Inactive.
- Clear `PatNote` unless the user keeps it.

**Permissions and logging:** requires `PatProblemListEdit`; creating a definition also requires `ProblemDefEdit`. Log under `PatProblemListEdit`.

**Schema:** none.

**Touchpoints:** `WpfControlsOD/Frms/FrmDiseaseEdit.xaml.cs`, `OpenDental/Forms/FormDiseaseDefs.cs`, `OpenDental/Forms/FormDiseaseDefEdit.cs`, `OpenDental/Forms/FormMedical.cs`, `OpenDentBusiness/Data Interface/Diseases.cs`, `OpenDentBusiness/Data Interface/DiseaseDefs.cs`.

### v25_2-chart-perio-print-birthdate: birthdate on printed Graphical Perio Charts

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Chart Module), baseline https://www.opendental.com/manual/graphicalperiochart.html.

**Behavior.** The printed Graphical Perio Chart header shows the patient's birthdate, in the short date format of the current culture, on its own line under the patient name and above the exam date.

**Rules:** if the birthdate is blank (year before 1880), skip the line. Print preview and PDF output use the same renderer.

**Touchpoints:** `OpenDental/Forms/FormPerioGraphical.cs` `RenderPerioPrintout` (~259; the name is drawn at ~283, the exam date just after).

---

## eForms

### v25_2-eforms-patient-list-field: eForm field "Patient List"

- **Category:** desktop · **Size:** L · **Confidence:** High (from the DB comments)
- **Sources:** https://www.opendental.com/site/version25_2.html (eForms), https://opendental.blog/version-25-2/ (eForms), https://www.opendental.com/OpenDentalDocumentation25-2.xml (`EnumEFormFieldType.PatientList` = 8; `eformfield.ValueLabel` and `ValueString` comments).

**Behavior.** A new eForm field type lets the person filling the form pick a **family member**. Its first and only use is picking the **insurance subscriber**.

- On the form, the field is a read-only textbox with a **Pick** button.
- Pick opens a popup listing the family's members, read live from the database.
- The popup has **Add**, which opens a second small popup for the new person's name and birthdate, then creates that person in the office database.

**UI:**

- **Designer** (`FrmEFormDefEdit`, field picker `FrmEFormFieldPicker`): add "Patient List" to the field types. New edit window `FrmEFormPatientListEdit` with: Label, Required, conditional parent/value (as for other fields), and "Space below".
- **Fill** (`CtrlEFormFill`):
  - The textbox shows the picked person's name.
  - **Pick** opens `FrmEFormPatientPick`: a grid of First, Last and Birthdate for the form patient's family, plus buttons Add, OK and Cancel. Pick works in kiosk mode on a workstation too.
  - **Add** opens `FrmEFormPatientAdd` with First name, Last name and Birthdate; all three are required.

**Data:**

- `ValueLabel` stores a JSON-serialized `EFormPatListLayout` object (the class name the 25.2 DB docs give), which holds at least `Label`. Model it on `EFormMedListLayout`.
- `ValueString` stores the picked person's **PatNum** as text, for example `"143"`. It is blank when nothing was picked.

**Rules:**

- Only the form patient's family is listed. A new person joins **that family**, with the guarantor's address and phone copied.
- New people are created as **Non-patient** status by default (our design), since a subscriber is often not a patient.
- Show only First name and Last name, never more data, because the patient may be filling at a kiosk.
- The field itself imports nothing. Ins Pick and the insurance fields consume it on import.

**Permissions and logging:**

- Adding a person needs `PatientCreate` for the logged-in user, which is the staff user even in kiosk mode. Without it, hide Add.
- Log `PatientCreate`: "Created from eForm Patient List field, eForm #N".

**Schema:** no new columns (enum value 8 only).

**Touchpoints:** `OpenDentBusiness/TableTypes/EFormField.cs` (enum and layout class), `OpenDentBusiness/TableTypes/EFormFieldDef.cs`, `OpenDentBusiness/Data Interface/EFormFields.cs` (`FromDef`/`ToDef`), `OpenDentBusiness/EForms/EFormFiller.cs`, `WpfControlsOD/UI/Controls/UserControls/CtrlEFormFill.xaml.cs`, `WpfControlsOD/Frms/FrmEFormDefEdit.xaml.cs`, `WpfControlsOD/Frms/FrmEFormFieldPicker.xaml.cs`, and the new Frm files.

**Depends on:** core sync.

### v25_2-eforms-ins-pick-field: eForm field "Ins Pick" and pending insurance

- **Category:** desktop · **Size:** XL · **Confidence:** High for data, Medium for UI
- **Sources:** https://www.opendental.com/site/version25_2.html (eForms), https://opendental.blog/version-25-2/ (eForms), https://www.opendental.com/OpenDentalDocumentation25-2.xml (`EnumEFormFieldType.InsPick` = 9; `eformfield.ValueString`/`PickListVis` comments; table `inspending`), baseline https://www.opendental.com/manual/patientformsimport.html.

**Behavior.**

- A new eForm field asks about one insurance slot (primary or secondary) with three radio buttons: **None**, **New** and **Existing**.
- **Existing** opens a popup to choose an insurance already on file for someone in the family.
- **New** reveals the insurance detail fields through conditional child fields: subscriber (a Patient List field), relationship, carrier name, carrier phone, group name, group number, employer and subscriber ID.
- Importing does **not** create plans directly. It creates **pending insurance** rows (`inspending`).
- Pending insurance shows in the Family Module **to the left of** the real plans. Staff turn it into a real plan with one conversion step that also handles duplicates across family members.

**Field data (from the DB docs):**

| Column | Meaning for Ins Pick |
|---|---|
| `ValueLabel` | Label above the three radio buttons. |
| `PickListVis` | Pipe-delimited labels of the three options, translatable. `PickListVisEnglish` keeps the English text for conditional logic. `PickListDb` is not used. |
| `ValueString` | `""` = None; `"New"` = New; `"InsSubNum:####"` or `"InsPendingNum:####"` = Existing. Display reads the referenced row live. |

**Which slot a field represents.** This is not documented. Our design takes it from `DbLink`: `ins1Pick` = ordinal 1 and `ins2Pick` = ordinal 2, which matches the existing `ins1*`/`ins2*` DbLinks in `EFormFieldsAvailable.cs`. **Verify** against `eformfielddef` rows with `FieldType=9` in the restored `fox-db` and adopt Open Dental's convention if it differs.

**UI:**

- **Designer:** add "Ins Pick" to the field types. New edit window `FrmEFormInsPickEdit` with Label, slot (1/2 as the DbLink), the three option labels, and Required.
  - Add the bundled template: put an Ins Pick plus the ins1 and ins2 child fields into `OpenDentBusiness/Resources/EForms/EFormPatientRegistration.xml`, so new installs get a working example.
- **Fill:**
  - Three radio buttons.
  - Selecting **Existing** opens `FrmEFormInsPickExisting`, a grid of the family's current insurance (carrier, subscriber, subscriber ID, plus a "Pending" flag for `inspending` rows) with OK and Cancel. After picking, the field shows a read-only summary.
  - Selecting **New** shows the child fields through conditional logic (parent = Ins Pick, value = the English "New").
- **Family Module, insurance area** (`ControlFamily`):
  - Each pending row shows as an extra column to the **left** of the real plans, headed "Pending Ins (n)" and drawn in a muted color.
  - It shows carrier name, subscriber, relationship, subscriber ID, group name and number, employer and phone.
  - Double-click opens `FormInsPending`: edit all fields, **Delete**, and **Convert to Insurance Plan**.
- **Convert to Insurance Plan** is a wizard in one window:
  1. **Carrier:** search existing carriers by name, showing near matches. Pick one or create a new carrier with the name and phone.
  2. **Plan:** list existing plans for that carrier filtered by group number and employer. Pick one or create a new plan with group name, number and employer.
  3. **Subscriber and ID:** find an existing `inssub` for the subscriber and plan, or create one with the subscriber ID.
  4. **Ordinal:** default to the pending row's ordinal; the user may change it.
  5. Create the `patplan` with the relationship. Delete the `inspending` row.
  6. If **other family members** have pending rows with the same subscriber, subscriber ID and carrier name, offer "Also convert for: [list]". Those reuse the same `inssub` and get their own `patplan`.

**Import rules:**

- **None:** no change.
- **New:**
  - Create an `inspending` row: PatNum = form patient; PatNumSubscriber = the Patient List value, or the patient when the relationship is Self; Ordinal = slot; Relationship; the text fields from the ins1/ins2 child fields.
  - `SubscriberID`: strip dashes and spaces. A blank ID means **do not import**; mark the field and tell the user.
  - The relationship must be Self when the subscriber is the patient. If those conflict, flag and do not import.
- **Existing `InsSubNum`:** if the patient already has a `patplan` on that `inssub`, do nothing. Otherwise create an `inspending` pre-filled from that subscription, so that conversion is one click.
- **Existing `InsPendingNum`:** copy that pending row for this patient. The DB docs say family duplicates are expected.
- Never create `insplan`, `inssub` or `patplan` rows during import. Only conversion does.
- **Auto-import** (`AutoImport` field flag from 25.1) applies to Ins Pick the same way.

**Permissions and logging:**

- Import writes a `securitylog` row with `EFormImport` (271, log only): "Pending insurance created from eForm #N".
- Convert requires `InsPlanCreate` if a plan or carrier is created, `InsPlanEdit` to attach to an existing plan, and `PatPlanCreate` (already in our 24.3 `EnumPermType`) for the new `patplan`. Log each object created as today's plan-creation code does.
- Deleting a pending row requires `InsPlanEdit` (our choice) and is logged.

**Schema** (25.3 DDL):

```sql
CREATE TABLE `inspending` (
  `InsPendingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `PatNum` bigint(20) NOT NULL,
  `PatNumSubscriber` bigint(20) NOT NULL,
  `Ordinal` tinyint(3) unsigned NOT NULL,
  `Relationship` tinyint(4) NOT NULL,      -- enum Relat: 0 Self .. 8 Dependent
  `GroupNum` varchar(255) NOT NULL,
  `GroupName` varchar(255) NOT NULL,
  `Employer` varchar(255) NOT NULL,
  `SubscriberID` varchar(255) NOT NULL,
  `Phone` varchar(255) NOT NULL,
  `CarrierName` varchar(255) NOT NULL,
  PRIMARY KEY (`InsPendingNum`), KEY `PatNum` (`PatNum`), KEY `PatNumSubscriber` (`PatNumSubscriber`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3;
```

The 25.2 DB comment on `inspending` names the REST API and eClipboard eForms as the code that creates these rows. Both are cut here, so eForm import on a workstation or kiosk is our only producer. The office's data may still hold rows created through those channels, so display and conversion must not assume an eForm origin.

Other places that must handle `inspending`:

- Patient merge and patient delete: move or delete the patient's pending rows.
- Family move: rows travel with the patient.
- DB Maintenance: a check for orphaned rows whose patient or subscriber is missing.

**Touchpoints:** new `OpenDentBusiness/TableTypes/InsPending.cs`, `OpenDentBusiness/Data Interface/InsPendings.cs`, `OpenDentBusiness/Crud/InsPendingCrud.cs`; `OpenDentBusiness/EForms/EFormFieldsAvailable.cs`, `EFormFiller.cs`, `EFormImport.cs`; `OpenDentBusiness/Data Interface/EForms.cs`; `WpfControlsOD/UI/Controls/UserControls/CtrlEFormFill.xaml.cs`; `WpfControlsOD/Frms/FrmEFormDefEdit.xaml.cs`, `FrmEFormFieldPicker.xaml.cs`, `FrmEFormImportRules.xaml.cs`; `OpenDental/Main Modules/ControlFamily.cs` (insurance grid); `OpenDental/Forms/FormInsPlan.cs`; `OpenDentBusiness/Data Interface/Patients.cs` (merge and delete); `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`.

**Depends on:** v25_2-eforms-patient-list-field, core sync.

### v25_2-eforms-patient-forms-window: Patient Forms window improvements

- **Category:** desktop · **Size:** M · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (eForms), https://opendental.blog/version-25-2/ (eForms), baseline https://www.opendental.com/manual/patientform.html.

**What is public:** only that the Patient Forms window interface was improved. **Proposed scope (our design), chosen to fix known gaps in the 24.3 window** (`FormPatientForms`: grid of Type, Date/Time, Kiosk, Description, Image Category, Updated, with buttons below):

1. Add a **Status** column. For eForms: None / In eClipboard / Filled. For sheets: Kiosk / Web form / Imported, from today's flags. Because eClipboard is cut, show "In eClipboard" only for data that already exists.
2. Add a **Show** filter: Sheets / eForms / Both. Remember it per user (`userodpref`).
3. Move the actions into a toolbar in a fixed order: Add Sheet, Add eForm, Pre-fill, Copy, Import, Kiosk. Disable actions that don't apply to the selected row type, with a tooltip saying why.
4. Keep the sort radio buttons and remember the choice per user.

Get a product decision before building. If the owner allows looking at the office's licensed 25.3 install, compare against its window.

**Touchpoints:** `OpenDental/Forms/FormPatientForms.cs` (`FillGrid` ~52 to 100; buttons ~235 to 437), `OpenDental/Forms/FormSheetImport.cs`.

### v25_2-eforms-remove-imported-status: remove the Imported status from eForms

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (eForms), https://www.opendental.com/OpenDentalDocumentation25-2.xml (`EnumEFormStatus`: `Imported` removed; `None` no longer mentions import).

**Behavior.**

- An eForm's status no longer records whether it was imported. Importing leaves the status unchanged: Filled stays Filled, None stays None.
- The Fill eForm window no longer shows or offers "Imported".
- Whether and when a form was imported is now found in the Audit Trail (`EFormImport` log entries).

**Rules:**

- The enum keeps 0 None, 1 ShowInEClipboard and 2 Filled.
- Our 24.3 enum also has 3 Imported and 4 UnmatchedToPatient; remove both.
- The convert script maps rows with `Status` 3 or 4 to 2 (Filled). The office's 25.3 data should have none, but our own test data might.

**Touchpoints:** `OpenDentBusiness/TableTypes/EForm.cs` (~62 to 73), `WpfControlsOD/Frms/FrmEFormFillEdit.xaml(.cs)`, `OpenDentBusiness/EForms/EFormImport.cs`, `OpenDentBusiness/Data Interface/EForms.cs` (`GetForPatient` status filter ~30), `OpenDental/Forms/FormPatientForms.cs`, `OpenDental/Forms/FormSheetImport.cs`.

---

## Family Module

### v25_2-family-edit-benefit-ui: Edit Benefit window redesign

- **Category:** desktop · **Size:** M · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Family Module), https://opendental.blog/version-25-2/ (Edit Benefit window for Other Benefits), baseline https://www.opendental.com/manual/editbenefit.html and https://www.opendental.com/manual/otherbenefits.html. 25.3 continues the work ("Updated editing interface for Other Benefits", plus `benefit.TreatArea` and tooth ranges): https://www.opendental.com/site/version25_3.html.

**Baseline.**

- `FormBenefitEdit` puts six list boxes side by side: Category, Type, Time Period, Quantity Qualifier, Coverage Level and Treatment Area. It also has a Code Group combo, a Proc Code box, and Percent, Amount and Quantity boxes.
- All inputs are always enabled. The rules are enforced only when the user clicks Save, one message box at a time (`butSave_Click`, ~81 to 200):
  - only one of Category, Proc Code or Code Group;
  - Percent only for CoInsurance;
  - waiting periods need months or years and a category, code group or code;
  - some limitations can't use a category;
  - frequency limitations can't have a quantity of zero;
  - Treatment Area only for limitations.
- The blog says the 25.2 change targets the window as used for **Other Benefits**.

**Proposed redesign (our design):**

- **Applies to:** radio buttons Category / Procedure code / Code group. Only the chosen input is enabled, so the three are mutually exclusive.
- **Benefit type** combo. Selecting a type enables only the inputs that matter for it, following the baseline manual:
  - Percent for CoInsurance;
  - Amount for Limitations and Deductible;
  - Quantity and Qualifier for frequency limitations, age limits and waiting periods.
- **Time period**, **Coverage level** and **Treatment area** as combos.
- A plain-language preview line at the bottom, for example: "Limitation: 2 per Calendar Year, Individual, Category: Preventive". Users can see what they are saving.
- **Patient Override** checkbox kept, with the same meaning.

**Rules:** the existing save-time validation stays as a backstop, but the layout should make most of those messages unreachable by disabling inputs that don't apply. Add a check that blocks a save where the selected type ignores a field the user filled in, with a message naming the field.

**Coordination:** 25.3 adds tooth ranges and a further UI update. Build the 25.2 layout so that the 25.3 fields drop in.

**Touchpoints:** `OpenDental/Forms/FormBenefitEdit.cs` (+ `.designer.cs`), `OpenDental/Forms/FormInsBenefits.cs`, `OpenDentBusiness/TableTypes/Benefit.cs`.

### v25_2-family-iaplus-bridge-update: Insurance Answers Plus bridge becomes a web launcher

- **Category:** vendor-direct · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Family Module), current public bridge page https://www.opendental.com/site/iaplus.html, old behavior https://www.opendental.com/manual/iaplus.html.

**Baseline (24.3).**

- The IAP button (`butIapFind`) on the Insurance Plan window shows only when the IAP program link is enabled (`FormInsPlan` ~333). Clicking it opens `FormIap` (`butIapFind_Click` ~1493).
- `FormIap` searches a local IAP desktop database through `iaplus10.dll` (`WpfControlsOD/Bridges/Iap.cs`, `DllImport` ~205).
- `FormInsPlan` then reads the chosen IAP record field by field and fills in plan data, including the carrier, the employer and the benefits list (~1504 onward).
- IAP is not a toolbar program link, so `ProgramL` has no IAP branch.
- The public manual (v24.4) already says this bridge stopped working with newer versions in late 2017.

**Behavior (25.2).** The bridge no longer talks to the IAP desktop product. The IAP button opens the **IAP web login page** in the default browser. The office signs in there with its own IAP account. No patient or plan data is sent.

**UI:**

- Program Links > Insurance Answers Plus: Enabled, and "Path of file to open", which now holds the IAP login URL.
- The button stays on the Insurance Plan window.

**Rules:**

- If the path is blank, show "Set the IAP login address in Program Links".
- Accept only `https://` URLs; open them with the shell.
- Remove `FormIap`, the DLL calls, and the plan-copy code, including the benefit import.
- The public setup page says only that Open Dental opens the IAP login page and that the bridge no longer works with the IAP desktop product. Keeping the URL in "Path of file to open" is **our** choice; Open Dental may hard-code it. Check the `program` row for IAP in the restored `fox-db`.

**Schema:** none. On conversion, if the IAP program row still has a local file path, leave it disabled and blank the path. The office enters the vendor's URL.

**Touchpoints:** `OpenDental/Bridges/FormIap.cs` (+ `.designer.cs`, `.resx`; remove), `WpfControlsOD/Bridges/Iap.cs` (replace with a URL launcher), `OpenDental/Forms/FormInsPlan.cs` (visibility ~333, `butIapFind_Click` ~1493, record import ~1504 onward), `OpenDentBusiness/TableTypes/Program.cs` (`ProgramName.IAP` stays).

### v25_2-family-ins-history-edit-remove: Insurance History, edit or remove entered dates

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Family Module), baseline https://www.opendental.com/manual/inshistory.html. 25.1 introduced two dates per category: https://www.opendental.com/site/version25_1.html.

**Baseline (24.3).**

- `FormInsHistSetup` shows one date textbox per category.
- Typing a date creates an Existing Other (EO) procedure.
- Typing over an existing date already **moves** that EO procedure to the new date (`Procedures.InsertOrUpdateInsHistProcedure`), unless a newer completed procedure exists. The manual (v24.4) describes the same rule.
- Clearing the box on save deletes that EO procedure after a Yes/No prompt (`butSave_Click` ~178 to 225).
- So in 24.3, editing and removing happen implicitly through one textbox per category. 25.1 changed this to two dates per category, and 25.2 adds explicit actions for each date.
- Completed procedures (status C) are never deleted.

**Behavior (25.2).** Each date entered in Insurance History can be **edited** or **removed** explicitly.

**UI (on top of the 25.1 two-date layout):**

- Each date shows with a small **Edit** button (date picker) and a **Remove** button. A right-click menu on the date offers the same two actions.
- Dates that come from completed procedures in the office are read-only. Their tooltip says "From a completed procedure on <date>; edit it in the Chart".

**Rules:**

- **Edit:** changes the date of the EO procedure behind that entry. If a more recent C procedure exists, the displayed date stays the C date (baseline rule) and a note explains why.
- **Remove:** deletes the EO procedure after confirmation. The recall due date recalculates, as the baseline does when a prophy date is entered.
- Removing a date never touches C procedures, claims or adjustments.

**Permissions and logging:**

- Removing needs `ProcDelete`, with its date restrictions; editing needs `ProcExistingEdit`.
- Log under those permissions with the category name.

**Schema:** none.

**Touchpoints:** `OpenDental/Forms/FormInsHistSetup.cs` (+ designer), `OpenDentBusiness/Data Interface/Procedures.cs`, `OpenDentBusiness/Data Interface/Recalls.cs`, `OpenDental/User Controls/Preferences/UserControlTreatPlanGeneral.cs` (Insurance History category codes, reference only).

**Depends on:** v25_1 two dates per category.

---

## Manage Module (Tasks, ERAs)

### v25_2-manage-task-filter-unassigned: Task Filter, "Unassigned" clinic and region

- **Category:** desktop · **Size:** S · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Manage Module), baseline https://www.opendental.com/manual/tasksetup.html (Default Filter for Tasks in Lists).

**Behavior.** The Clinic and Region dropdowns in the Task Filter window gain an **Unassigned** choice:

- **Clinic: Unassigned** shows tasks whose attached patient or appointment has no clinic (ClinicNum 0).
- **Region: Unassigned** shows tasks whose clinic has no region.

Tasks with no object attached remain unfiltered, as before.

**UI:**

- `FrmTaskFilter`: in `FillComboBoxClinics` (~84) and `FillComboBoxRegions` (~97), add a first item "Unassigned" after "All".
- It can be multi-selected together with real clinics or regions.

**Rules:**

- **Do not use 0 as the "Unassigned" marker.** In 24.3, 0 already means "no filter":
  - `UserControlTasks` sets the clinic list to `{ 0 }` for Headquarters (~572, ~582) and treats a one-item `{ 0 }` list as "HQ" in the status text (~459 to 466);
  - `TaskLists.BuildFilterJoins` and `BuildFilterWhereClause` (~259 to 330) skip filtering when the clinic or region key is 0.
- Use a separate sentinel instead, -1 in both lists, or a separate `IsUnassignedSelected` flag.
- `TaskLists.cs` must translate the sentinel into conditions for "patient or appointment clinic is 0" and "the clinic's `Region` is 0".
- The filter status text in `UserControlTasks` (~461 to 470) shows "Unassigned".

**Touchpoints:** `WpfControlsOD/Frms/FrmTaskFilter.xaml(.cs)`, `OpenDental/User Controls/UserControlTasks.cs` (~78, ~371, ~553 to 582, ~917), `OpenDentBusiness/Data Interface/TaskLists.cs`, `OpenDentBusiness/Data Interface/Tasks.cs` (~988, ~1021).

### v25_2-manage-task-category: Task Category for all offices

- **Category:** desktop · **Size:** M · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Manage Module), https://www.opendental.com/OpenDentalDocumentation25-2.xml (`task.Category`: FK to definition, used only when the preference `TaskCategoryShow` is on; `DefCat.TaskCategories` no longer HQ only), baseline https://www.opendental.com/manual/tasksetup.html and https://www.opendental.com/manual/taskswindow.html.

**Baseline.** Task categories exist only for Open Dental HQ's triage list:

- the Task window hides `labelCategory`/`listCategory` unless `PrefC.IsODHQ` (`FormTaskEdit` ~103 to 110);
- the column is `TriageCategory`;
- the definitions help text says HQ only.

**Behavior (25.2).**

- Tasks Preferences gets a **Show task category** checkbox, with preference `TaskCategoryShow` (bool, default false).
- When it is on:
  - the Task window shows a **Category** dropdown: None plus the non-hidden Task Categories definitions, and keeps a hidden category that is already assigned;
  - task-list grids show a **Category** column whose cell is colored by the definition color.

**UI:**

- `FormTaskPreferences`: checkbox.
- `FormTaskEdit`: change the list box to a combo labeled "Category", visible when `TaskCategoryShow` is on (no longer tied to `IsODHQ`).
- `UserControlTasks`: add the column when the preference is on. Today it is added only when `isHqAndTriageList` (~1018 to 1020); the cell color is set at ~1095 and the text at ~1111.
- Definitions window: the category name is "Task Categories", with color enabled (`DefL.cs` ~257).

**Rules:**

- Category is optional, and 0 means none.
- Turning the preference off hides the UI but keeps the data.
- Task history copies the category.
- Replace the HQ-only "Invalid Triage Category" message (~861) with ordinary None handling.

**Permissions:** editing categories in Definitions needs `Setup`; setting a task's category needs normal task edit rights.

**Schema:** rename `task.TriageCategory` → `task.Category` and `taskhist.TriageCategory` → `taskhist.Category`, both `bigint(20) NOT NULL DEFAULT 0`. Keep the index named `TriageCategory`, as in the dump.

**Touchpoints:** `OpenDentBusiness/TableTypes/Task.cs` (~73, ~111), `OpenDentBusiness/TableTypes/TaskHist.cs` (~72, ~110), `OpenDentBusiness/Crud/TaskCrud.cs`, `OpenDentBusiness/Crud/TaskHistCrud.cs`, `OpenDental/Forms/FormTaskEdit.cs` (~103, ~713 to 717, ~861 to 866), `OpenDental/Forms/FormTaskPreferences.cs`, `OpenDental/User Controls/UserControlTasks.cs` (~1019, ~1035, ~1095, ~1111), `OpenDental/Logic/DefL.cs` (~257), `OpenDentBusiness/TableTypes/Def.cs` (~233), `OpenDentBusiness/TableTypes/Pref.cs`.

### v25_2-manage-task-filter-patient: Task Filter, pick a specific patient

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Manage Module).

**Baseline.** `FrmTaskFilter` already has `radioSpecificPatient` and `butSelectPatient`, hidden with the comment "until formSelectPatient is built in WPF" (constructor ~45). `FrmPatientSelect` now exists in WPF and is used by `FrmEFormFillEdit`.

**Behavior.** The filter's patient options become: All patients / Patient selected in main window / Patient of selected task / **Specific patient**. Specific patient opens Select Patient and shows the choice as "Last, First - PatNum".

**Rules:**

- OK fails with a message if Specific patient is chosen with no patient picked.
- The filter matches tasks attached to the patient, or to an appointment of the patient. This is the same rule the other patient options use.

**Touchpoints:** `WpfControlsOD/Frms/FrmTaskFilter.xaml(.cs)` (unhide the controls; implement `butSelectPatient_Click` with `FrmPatientSelect`), `OpenDental/User Controls/UserControlTasks.cs` (`_enumTaskPatientFilterType` ~81, filter application).

### v25_2-manage-era-carc-logic: more logic for "Don't auto-process claims w/these CARCs"

- **Category:** desktop · **Size:** M · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Manage Module), baseline https://www.opendental.com/manual/eraprocess.html and https://www.opendental.com/manual/reporterasautomaticallyprocess.html. 25.1 added auto-split of ERAs by CARC denial codes: https://www.opendental.com/site/version25_1.html.

**Baseline.**

- Preference `EraNoAutoProcessCarcCodes` holds a comma list, defaulting to `97,16,252,242,50,251,B7,226,250`.
- If any claim-level or procedure-level adjustment on the ERA uses one of those codes, auto-processing skips the claim (`X835.GetCarcCodesNoAutoProcessForClaim` ~3651, message ~4303).

**What is public:** only that "additional logic" was added. **Proposed logic (our design), each part optional:**

1. **Group-code qualified entries.** Allow entries such as `CO-97` or `PR-16` next to bare codes. A bare code matches any group code; a qualified entry matches only that pair. This lets offices skip only contractual denials.
2. **Ignore zero-amount adjustments.** A code on an adjustment of $0.00 does not block auto-processing. Some payers echo informational CARCs with no money attached.
3. **Interaction with the 25.1 auto-split.** When the 25.1 option splits denied procedures onto their own claim, apply the no-auto-process check to **each resulting claim separately**. A clean split can then still auto-process.
4. In the ERAs Automatically Processed report, the "not processed" reason names the matching entry, for example "CO-97 on procedure D2740".

**UI:**

- Preferences > Manage > ERA: the same textbox, with a tooltip explaining the `GROUP-CODE` syntax.
- Validate entries on save: 1 to 5 alphanumeric characters, optionally prefixed by CO/PR/OA/PI/CR and a dash.

**Touchpoints:** `OpenDentBusiness/X12/X835.cs` (~3651 to 3657, ~4303), `OpenDental/User Controls/Preferences/UserControlManageGeneral.cs` (~135, ~164), `OpenDentBusiness/TableTypes/Pref.cs` (~1102 to 1107), `OpenDental/Forms/FormRpEraAutoProcessed.cs`.

**Depends on:** v25_1 ERA auto-split by CARC.

---

## Miscellaneous: bridges, reports, security, server, wiki, tools, UI, imaging

### v25_2-bridges-dtx-studio: DTX Studio and DTX Studio Acquire bridges

- **Category:** vendor-direct · **Size:** M · **Confidence:** High for setup, Low for launch syntax
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), https://www.opendental.com/site/bridgedtxstudio.html, https://www.opendental.com/site/bridgedtxstudioacquire.html, vendor https://www.dtxstudio.com/en-us.

**Behavior.** There are two program links for the DTX Studio imaging software.

- **DTX Studio** opens the current patient in DTX Studio, creating the patient there if needed.
- **DTX Studio Acquire** opens an existing patient with the acquisition screen ready.
- The public pages say to enable **both**.

**Setup (Program Links), per bridge:**

| Setting | Default |
|---|---|
| Enabled | off |
| Path of file to open | `C:\Program Files\DTX Studio Clinic\DTXsync.exe` |
| Text on button | "DTX Studio". Open Dental's Acquire page also lists "DTX Studio"; we default Acquire to "DTX Studio Acquire" so the two buttons can be told apart (our choice). |
| Additional properties | free text appended to the command line |
| Property "Birthdate Format" | `MM/dd/yyyy`, editable (for example `dd/MM/yyyy`) |
| Hide button for clinics | standard |
| Toolbars | Imaging Module and Main toolbar |
| Button image | optional, 22×22 |

**Launch.** Start `DTXsync.exe` with the patient's identifier, last and first names, birthdate (formatted by the property), and gender. **The command-line syntax is not public.** Get the integration spec from DTX/Envista under our own developer agreement before coding. Do not copy another program's argument format.

**Rules:**

- With no patient selected, show "Please select a patient first."
- If the exe is missing, show the path in the error.
- The patient ID is PatNum by default. Offer the standard "PatNum or ChartNumber" program property used by other imaging bridges. The public setup pages list only Birthdate Format, so this property is **our** addition. If the restored `fox-db` has DTX `programproperty` rows, match them exactly and add ours only if missing.
- Acquire with a patient who does not exist in DTX yet: launch anyway and let DTX handle it. Note this in the help text.

**Schema and data:**

- New `ProgramName` values. Proposed: `DTXStudio`, `DTXStudioAcquire`. Confirm against the `program.ProgName` rows in the restored `fox-db`.
- The convert script inserts the `program`, `programproperty` (Birthdate Format, PatNum/ChartNumber) and `toolbutitem` rows, disabled.

**Touchpoints:** `OpenDentBusiness/TableTypes/Program.cs` (`ProgramName` enum), new `WpfControlsOD/Bridges/DTXStudio.cs` (model it on `WpfControlsOD/Bridges/DentalStudio.cs`), `WpfControlsOD/Logic/ProgramL.cs` (dispatch ~138 to 190), `OpenDental/Forms/FormProgramLinkEdit.cs` (no change expected), convert script.

### v25_2-reports-ins-pending-supplementals: Insurance Pending Supplementals report

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), https://opendental.blog/version-25-2/ (Insurance Pending Supplementals Report), https://www.opendental.com/site/0_reports.html (listed under Lists), baseline for pending supplementals https://www.opendental.com/manual/claimpendingsupplemental.html.

**Baseline.**

- Edit Claim already supports pending supplementals (PndSup). These are `claimproc` rows with `IsOverpay=1` and `Status=NotReceived`.
- The expected amount is in `InsEstTotalOverride`: negative for an overpayment, positive for an underpayment.
- "To Supplemental" turns them into real supplementals (`FormClaimOverpay` ~258 to 271).

**Behavior.** A new standard report under **Reports > Standard > Lists** shows every outstanding pending supplemental. Offices use it to find refunds they owe insurers and extra payments they should chase.

**UI (`FormRpInsPendingSupplementals`, non-modal like the other interactive reports):**

- **Filters:**
  - Clinics (multi-select, restricted to the user's clinics);
  - Type: All / Overpaid / Underpaid;
  - optional Date of service from/to (blank means all);
  - **Refresh**.
- **Grid:** Date of Service, Patient, Carrier, Clinic, Type (Overpaid/Underpaid), Amount, and Claim Date Sent.
- **Total** of the visible rows at the bottom.
- **Double-click** opens the claim (`FormClaimEdit`).
- **Right-click:** Go to Account, Copy cell, Copy row.
- **Print** and **Export** buttons, as in other report grids.

**Rules:**

- A row is one pending `claimproc`. The amount is its `InsEstTotalOverride`. Type comes from the sign.
- Rows disappear when the amount is cleared, set to 0, or converted to supplemental.
- Hidden from users without access to that clinic.

**Permissions:** standard report security. Add a `displayreport` row; access is through `Reports` permission with `FKey=DisplayReportNum` (Report Setup). Proposed internal name `ODInsPendingSupplementals`; confirm against `fox-db.displayreport`. The convert script grants it to user groups that already have `ODInsuranceOverpaid` (our choice).

**Schema:** no new table; one `displayreport` row.

**Touchpoints:** `OpenDentBusiness/Data Interface/DisplayReports.cs` (`ReportNames` constants ~130 to 141), `OpenDental/Forms/FormReportsMore.cs` (dispatch ~335/542/725 and the non-modal enum ~794), new `OpenDentBusiness/Reports/RpInsPendingSupplementals.cs` (model on `RpInsOverpaid.cs`), new `OpenDental/Forms Reports/FormRpInsPendingSupplementals.cs` (model on `FormRpInsOverpaid.cs` / `FormRpEraAutoProcessed.cs`), `OpenDental/Forms/FormClaimEdit.cs`.

### v25_2-security-domain-login-change-user: domain login when changing user

- **Category:** desktop · **Size:** M · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), https://www.opendental.com/OpenDentalDocumentation25-2.xml (`sheetfield.UserSigned`; `userod.DomainUser` format), baseline https://www.opendental.com/manual/singlesignon.html. 25.1 added the change-user button on sheet signature boxes: https://www.opendental.com/site/version25_1.html.

**Baseline.**

- Domain (Active Directory) login works only at program start. `FormOpenDental` ~7630 matches the Windows user `<DomainObjectGuid>\<user>` against `userod.DomainUser`.
- The **Change User** buttons on Procedure notes (`FormProcEdit.butChangeUser_Click` ~2493) and Group notes (`FormProcGroup` ~326) open `FormLogOn(isSimpleSwitch:true)`, which always asks for the Open Dental password.

**Behavior.** When domain login is enabled, the change-user prompt also accepts **domain credentials**. The covered places are:

- procedure notes;
- group notes;
- signing sheets (the 25.1 change-user button inside sheet signature boxes).

When a provider-user signs a sheet signature box after changing user, the sheet records **who** signed.

**UI:**

- `FormLogOn` in simple-switch mode: when `DomainLoginEnabled` and `DomainLoginPath` are set, show a **Use Windows (domain) login** option.
- With that option, the user types a domain user name and password. If the domain account maps to several Open Dental users, show the same pick list the startup flow uses.
- Sheets: the signature box shows "By: <user name>" when `UserSigned` is set.

**Rules:**

- Validate the credentials against the domain at `DomainLoginPath` with `PrincipalContext.ValidateCredentials`, never against the local machine.
- Map to Open Dental users with `Userods.GetUsersByDomainUserName("<DomainObjectGuid>\<name>")`.
- Never store the domain password.
- Failed attempts count toward the normal lockout rules.
- `sheetfield.UserSigned` is set only for SigBox fields signed after a change-user, and only when that user has a ProvNum > 0. Sig boxes without electronic signatures or provider restriction keep 0.

**Permissions and logging:** the same checks as today's change-user (`ProcedureNoteUser`, `ProcedureNoteFull`, `GroupNoteEditSigned`, and sheet edit). Log entries say the switch used domain authentication.

**Schema:** `sheetfield.UserSigned bigint(20) NOT NULL` (default 0) + `KEY UserSigned`.

**Touchpoints:** `OpenDental/Forms/FormLogOn.cs`, `OpenDental/Forms/FormProcEdit.cs` (~2493), `OpenDental/Forms/FormProcGroup.cs` (~326), `OpenDental/Forms/FormSheetFillEdit.cs` (sig boxes ~758, ~1740, ~2061), `OpenDentBusiness/SheetFramework/SheetDrawingJob.cs`, `OpenDental/SheetFramework/SheetPrinting.cs`, `OpenDentBusiness/Data Interface/Userods.cs` (~97 to 126, ~449), `OpenDental/Main Modules/FormOpenDental.cs` (~7630; factor the domain code into a shared helper), `OpenDentBusiness/TableTypes/SheetField.cs`.

**Depends on:** v25_1 change user on sheet signatures.

### v25_2-security-permission-renames: permission display-name updates

- **Category:** desktop · **Size:** S · **Confidence:** Low for the full list
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous). Known renames that reached the 25.2 line by backport, from https://www.opendental.com/site/version25_3.html: Appointment Create and Appointment Move, both in 25.2.40.

**Behavior.** Permission names shown in Security settings and the Audit Trail are clearer. Only the display strings (`[Description]` attributes) change, never the enum numbers or names. Stored data is untouched.

**Known changes to make:**

- `AppointmentCreate`: say that it also covers moving a planned appointment off the Pinboard to schedule it.
- `AppointmentMove`: say that it covers dragging, sending to the Pinboard, and setting unscheduled, for any appointment status. It does not block moving an Unscheduled-list appointment or moving planned appointments to the Pinboard.
- `InsWriteOffEdit`: the DB docs now say it also blocks creating and deleting claimprocs, so that write-off date limits can't be bypassed. Add a short clarification to the name or tooltip (25.3 notes, backported to 25.2.38).

The rest of the 25.2 rename list is not public. Collect it from the office's 25.3 Security window, if the owner approves (see open questions).

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (Description attributes, e.g. ~112, ~115, ~401), `OpenDentBusiness/Data Interface/GroupPermissions.cs` (`GetDesc` ~724), and the translation table (`Lans`, category `enumPermissions`).

### v25_2-security-tp-proc-edit-fee: permission "Treatment Planned Procedure Edit Fee"

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), https://opendental.blog/version-25-2/ (Miscellaneous), https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml (`ProcTPEditFee` = 270).

**Behavior.** A new permission controls who may change the **fee** of a treatment-planned procedure. Users without it see the fee but cannot edit it.

**UI:**

- Security tree: Chart Module > Procedure > **Treatment Planned Procedure Edit Fee**, next to `ProcEditShowFee` (`UserControlSecurityTree.cs` ~418 to 421).
- Edit Procedure (`FormProcEdit`): when the status is TP or TPi and the user lacks the permission, make `textProcFee` read-only with a tooltip naming the permission.
- Other fee-changing paths for TP procedures must check it too:
  - edit-all (`FormProcEditAll`), when a provider change would change the fee;
  - saved-treatment-plan rows (`FormProcTPEdit`, `textFeeAmt`). These are `proctp` snapshot rows, not the procedure itself, so covering them is **our** choice.

**Rules:**

- If the user lacks `ProcEditShowFee`, the fee is hidden anyway (~152).
- Automatic fee updates are **not** blocked: a fee-schedule change, changing the patient's insurance, or the "Update fees" tools. The permission is about manual edits.
- Changing the status from TP to complete is governed by the completed-procedure permissions, as today.

**Logging:** on save, if the TP fee changed, log `ProcTPEditFee` with the PatNum and "Fee for <code> changed from X to Y".

**Upgrade default:** grant the permission to **every user group that has `ProcExistingEdit`** (our choice), so that nobody loses access on upgrade. The office's 25.3 data already has the rows.

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs`, `OpenDental/User Controls/UserControlSecurityTree.cs`, `OpenDental/Forms/FormProcEdit.cs` (~129, ~152, ~855, ~2366), `OpenDental/Forms/FormProcEditAll.cs`, `OpenDental/Forms/FormProcTPEdit.cs`, convert script.

### v25_2-server-replication-delay: read/write delay preferences for replication

- **Category:** desktop · **Size:** M · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), baseline https://www.opendental.com/manual/replication.html and https://www.opendental.com/manual/replicationservers.html.

**What is public:** only that new preferences set a read/write delay when replication is used. **Proposed behavior (our design):** with replication, data written on the primary reaches a replica after a lag. Two preferences let the office absorb that lag:

1. **Read delay after write.** After a workstation commits a write, reads that would go to the **read-only server** (the "Server Connections" replica used for reports and some lists) go to the primary instead for N milliseconds. Users then don't see stale data right after saving.
2. **Write delay for signals.** When processing cache-refresh signals (`signalod`), each workstation looks back an extra N milliseconds beyond its last poll time. Signals that land late on a replica are then not missed in daisy-chain setups.

**UI:**

- Preferences > Server Connections, new group "Replication".
- Two numeric boxes in ms, range 0 to 60000, default 0, which keeps today's behavior.
- Proposed names `ReplicationReadDelayMs` and `ReplicationSignalLookbackMs`. **Verify** against the `fox-db` preference rows and, if Open Dental's prefs exist, adopt their names and meaning.

**Rules:**

- Both are ignored when no read-only server is configured and no replication servers are defined.
- Values are global.

**Touchpoints:** `OpenDental/User Controls/Preferences/UserControlServerConnections.cs` (~190 to 245), `OpenDentBusiness/Db/ConnectionStore.cs` (~54 to 73), `OpenDentBusiness/Cache/PrefC.cs` (`ReadOnlyServer` ~600), `OpenDentBusiness/Db/Db.cs` (~90 to 120), `OpenDentBusiness/Data Interface/Signalods.cs`, `OpenDentBusiness/Data Interface/ReplicationServers.cs`.

### v25_2-wiki-readonly-offline: read-only wiki usable while the program is closed

- **Category:** desktop · **Size:** L · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), baseline https://www.opendental.com/manual/wiki.html.

**What is public:** only that a read-only copy of the wiki can be opened when the program is closed. **Proposed design.** Offices often keep emergency procedures in the wiki, for example "what to do when the server is down". So we keep a **static HTML export** that any browser can open with no database connection:

- Wiki Setup gets **Read-only copy folder** (default `<A-to-Z>\WikiReadOnly`) and an **Export now** button.
- The export writes one HTML file per page, using the existing wiki-to-HTML renderer, with internal links rewritten to relative file links.
- It also writes an `index.html` listing all non-archived pages alphabetically, with a small client-side title and content search in plain JavaScript and no external libraries.
- Files and images that pages reference from the wiki file folders are copied into `_files/`.
- After each wiki page save, that page is re-exported in the background, and `index.html` is rebuilt when a title is added or removed.
- **Open read-only wiki** in the Wiki menu opens `index.html`. Staff can also bookmark it or put a desktop shortcut to it.

**Rules:**

- Archived pages are excluded.
- Every page carries a banner: "Read-only copy exported <date time>".
- If the folder is unreachable, log it and do not interrupt the save.
- The office is responsible for the folder's access rights. Setup warns that wiki pages may contain patient information.

**Preferences:** proposed `WikiReadOnlyExportPath` (string, default blank = off) and `WikiReadOnlyAutoExport` (bool, default true when a path is set).

**Touchpoints:** `OpenDental/Forms/FormWiki.cs`, `WpfControlsOD/Frms/FrmWikiSetup.xaml.cs`, `OpenDentBusiness/Data Interface/WikiPages.cs`, `OpenDental/Forms/FormWikiEdit.cs` (save hook), `OpenDental/Forms/FormWikiFileFolder.cs`, a new `OpenDentBusiness/Misc/WikiStaticExport.cs`.

**Alternative interpretation:** a small read-only viewer program that reads the database while the main program is closed. That needs the database up, so it doesn't help in the "server down" case; we prefer the static export.

### v25_2-tools-dbm-invalid-chars-gdi: DB Maintenance tool, Invalid Characters (GDI+)

- **Category:** desktop · **Size:** M · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), baseline https://www.opendental.com/manual/databasemaintenancetools.html.

**Behavior (our design).** A new entry on the Database Maintenance **Tools** tab, "Invalid Characters (GDI+)". It finds and removes characters that the WinForms GDI+ text renderer can't draw and that can crash or garble grids and printouts: unpaired UTF-16 surrogates, C0 control characters other than tab, CR and LF, and U+FFFF/U+FFFE.

**UI:**

- Tools grid row: name and a one-line description.
- Clicking opens a window with:
  - a checklist of target columns;
  - **Scan**, which shows a count per column;
  - a preview of the first 50 affected values with the bad characters shown as `[U+XXXX]`;
  - **Fix**, which needs confirmation and runs after a reminder to back up.
- Default target columns: `patient` name, address, preferred name and note fields; `appointment.Note` and `ProcDescript`; `procedurelog.Note` (non-signed notes only); `commlog.Note`; `definition.ItemName`; `sheetfield.FieldValue`; `wikipage.PageContent`.

**Rules:**

- **Never modify signed notes.** Removing characters would break the signature hash; report them instead.
- Write a `securitylog` entry under `Setup` with the counts fixed.
- Respect the replication warning: this tool is not replication-safe, so use the same "unsafe for replication" prompt as other DBM fixes.

**Related existing tools** (manual, Database Maintenance Tools tab): "Spec Char" strips unsupported characters from appointment notes and appointment procedure descriptions only, and "Remove Nulls" removes hidden null characters. The new tool covers more columns and more character classes.

**Touchpoints:** `OpenDental/Forms/FormDatabaseMaintenance.cs` (`FillGridTools` ~170 to 270, the "Spec Char" row at ~256; `SpecCharFix` ~431 is the closest existing tool), `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` (`FixSpecialCharacters`), and a new `FormDatabaseMaintInvalidChars`.

### v25_2-wiki-search-limit: Wiki Search, limit the number of results

- **Category:** desktop · **Size:** S · **Confidence:** Medium
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), baseline https://www.opendental.com/manual/wikisearch.html.

**Behavior.** The Wiki Search window gets a **Limit results to [N]** checkbox and number box. N defaults to 100 and allows 1 to 5000. When the box is checked, the search returns at most N page titles, and a label says "Showing first N matches". This keeps big wikis fast.

**Rules:**

- Ordering stays as today. `GetForSearch` runs up to four queries in a fixed order (keyword matches, title matches, pages linking to keyword pages, content matches) and appends the results. The manual (v24.4) documents the same order. So the limit can't be one `LIMIT` on one query:
  - pass the remaining count as `LIMIT` to each query in turn;
  - skip the later queries once N titles are collected;
  - keep the existing duplicate removal between tiers, and ask for a few extra rows so that duplicates don't leave the result short;
  - the title tier also supplies the page numbers that the link tier searches for, so a limited title tier narrows the link tier too. That is acceptable, but test it.
- The option also applies when the search runs on the report server.
- Remember the checkbox and N per user (`userodpref`).

**Touchpoints:** `OpenDental/Forms/FormWikiSearch.cs` (`FillGrid` ~103 to 121), `OpenDentBusiness/Data Interface/WikiPages.cs` (`GetForSearch` ~186; add a `limit` parameter).

### v25_2-security-audit-patient-clinic: Audit Trail logs patient clinic changes

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), baseline https://www.opendental.com/manual/audittrail.html.

**Behavior.** When a patient's clinic changes, the Audit Trail gets a `PatientEdit` entry such as "Clinic changed from <abbr> to <abbr>". "None" stands for 0.

**Rules:**

- Add the clinic comparison to `Patients.InsertAddressChangeSecurityLogEntry` (`Patients.cs` ~2645), which already logs name, status and address changes.
- Also log in every other path that sets `patient.ClinicNum`. Find them with `grep "\.ClinicNum="` over patient code, for example appointment-driven clinic updates and family moves.
- Keep the existing bulk log in `FormClinics` (~272).

**Touchpoints:** `OpenDentBusiness/Data Interface/Patients.cs` (~2645 to 2666, `ChangeClinicsForAll` ~4421), `OpenDental/Forms/FormPatientEdit.cs` (~2821), `OpenDental/Forms/FormClinics.cs` (~271).

### v25_2-ui-blur-module-switch: blur when switching between modules

- **Category:** desktop · **Size:** S · **Confidence:** Low
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous).

**Behavior (our design).** With the new preference on, switching modules briefly covers the incoming module with a blurred snapshot (about 400 ms) and ignores mouse clicks during that time. A double-click on the module bar, or a click that lands as the screen redraws, then can't hit a button or grid row in the new module.

**UI:**

- Preferences > Main Window > Misc: checkbox **Blur when switching between modules to prevent accidental clicking**.
- Proposed name `ModuleSwitchBlur`; bool, default false.
- It is a global preference. A per-computer override can come later.

**Rules:**

- Keyboard input is not blocked.
- If the snapshot fails (for example over remote desktop with no GDI surface), fall back to a plain translucent overlay.
- Skip the effect when the module change comes from code rather than the user, for example from "Go to" actions.

**Touchpoints:** `OpenDental/Main Modules/FormOpenDental.cs` (`moduleBar_ButtonClicked` ~4297, `SetModuleSelected` ~4366, `UnselectActive` ~4478), `OpenDental/User Controls/Preferences/UserControlMainWindowMisc.cs`, `OpenDentBusiness/TableTypes/Pref.cs`.

### v25_2-imaging-copy-without-drawings: copy images without drawings or annotations

- **Category:** desktop · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous), baseline https://www.opendental.com/manual/images.html.

**Behavior.** The Imaging Module's **Copy** gains a second choice, **Copy without drawings**. It copies the image with crop, rotation, flip and brightness/contrast applied, but **no** drawings, text, measurements or AI annotations.

**UI:** make the Copy toolbar button a split button with a dropdown holding "Copy" and "Copy without drawings". Add the same two items to the image right-click menu.

**Rules:**

- For a mount item: render the item without calling `DrawDrawings`.
- For a whole mount: render the mount without drawings.
- For a single document that has drawings: render without them, even when the file itself is unedited.
- PDFs and other non-image files behave like plain Copy.

**Touchpoints:** `OpenDental/User Controls/ControlImageDisplay.cs` (`ToolBarCopy_Click` ~1363; `DrawDrawings(0,g)` ~1395), `OpenDental/Main Modules/ControlImages.cs` (toolbar ~261, handler ~2261), `CodeBase/Utilities/ODCloudClient.cs` (`CopyToClipboard`, ODCloud path only; to be removed by the decoupling plan).

### v25_2-bridges-dxc-credit-score-removed: remove the DentalXChange Patient Credit Score bridge

- **Category:** desktop (removal) · **Size:** S · **Confidence:** High
- **Sources:** https://www.opendental.com/site/version25_2.html (Miscellaneous). The page says the service was discontinued.
- **Classification note:** the first draft listed this as excluded-other while also asking us to remove the bridge. Removing it is work we do, so it is a desktop item.

**Baseline (24.3).** The "DXC Patient Credit Score" program link posts the ClaimConnect clearinghouse's login and password to a DentalXChange registration page in an embedded browser (`WpfControlsOD/Bridges/DentalXChange.cs`). That file holds **only** this bridge. `ProgramL` dispatches to it at ~185, and `ConvertDatabases6.cs` ~1021 inserted the program row.

**Change:**

- Delete `WpfControlsOD/Bridges/DentalXChange.cs` and the dispatch branch in `ProgramL`. This removes a path that sends clearinghouse credentials to a dead endpoint.
- In the convert script, set the program row to disabled and delete its `toolbutitem` rows, so no button shows.
- Keep `ProgramName.DXCPatientCreditScore` in the enum, because migrated rows reference it by name. If Program Links lists it, label it "(discontinued)" and don't let it be enabled.

**Touchpoints:** `WpfControlsOD/Bridges/DentalXChange.cs` (delete), `WpfControlsOD/Logic/ProgramL.cs` (~185), `OpenDentBusiness/TableTypes/Program.cs` (~93, enum stays), `OpenDental/Forms/FormProgramLinkEdit.cs` (block enabling), convert script.

---

## Excluded

**Open Dental-hosted services (excluded-od-service):**

- **v25_2-eservices-eclipboard-web**: eClipboard Web is part of Open Dental's hosted eClipboard eService (browser forms served by Open Dental). Reserve its enum values only.
- **v25_2-eservices-eclipboard-web-links**: patient-specific eClipboard Web links are sent through Open Dental's hosted texting and email (`ApptReminderType` 11 and `SmsMessageSource` 29 reserved).
- **v25_2-imaging-betterdiagnostics**: the public page says offices sign up through Open Dental's Signup Portal, Open Dental bills them, and the BAA goes through Open Dental. The 25.2 DB docs list it as `eServiceCode` 53. The task brief excludes "BetterDiagnostics-through-HQ" by name. Sources: https://www.opendental.com/site/betterdiagnostics.html. Keep `imagedraw.BetterDiagLayer` (25.1) for data compatibility. A future integration would be vendor-direct with our own BetterDiagnostics contract.
- **v25_2-imaging-betterdiagnostics-filter**: the pathology filter only applies to that hosted feature.
- **v25_2-eservices-massemail-upload**: the Mailchimp upload is part of Mass Email, which is an Open Dental eService. It is turned on through the eServices Signup Portal, Open Dental charges an access fee for it, and it needs an active registration key on support (https://www.opendental.com/site/massemail.html). So it is not a plain vendor bridge. A self-built Mailchimp integration with the office's own API key would be a separate, later decision.
- **v25_2-eservices-massemail-commlogs**: the commlogs come from Open Dental's Mass Email receipts.
- **v25_2-eforms-frequency-considers-sheets**: frequency rules decide which forms eClipboard offers at check-in. eClipboard is cut. Keep the column `eclipboardsheetdef.SheetDefNumsConsidered` (core sync).
- **v25_2-eservices-secure-email-unsubscribe**: Secure Email is Open Dental's hosted email.
- **v25_2-eservices-websched-recall-secure-email-setup**: Web Sched is an Open Dental eService.
- **v25_2-eservices-websched-recall-secure-email-clinic**: the same.
- **v25_2-eservices-msgtopay-secure-email**: Message-to-Pay is an Open Dental hosted payment eService.
- **v25_2-eservices-msgtopay-receipt-template**: the same.

**Other exclusions (excluded-other):**

- **v25_2-account-carecredit-merchant-fee-adj**: CareCredit is cut (`docs/decoupling-plan.md`, Bridges row). Open Dental's CareCredit integration lives in the proprietary Bridges DLL, and the setup page says the eConnector and Open Dental Service are required (https://www.opendental.com/site/carecreditsetup.html).
- **v25_2-account-carecredit-self-service**: the same vendor cut, and that is the only reason. The blog says Synchrony itself sends the self-service link by text or email, not Open Dental's texting (https://opendental.blog/version-25-2/). If CareCredit is ever brought back as a vendor-direct bridge, this item can come back with it.
- **v25_2-account-carecredit-net-amount**: the same vendor cut (https://www.opendental.com/site/carecredittransactions.html).
- **v25_2-security-text-all-employees-perm**: `EnumPermType` 269 is Open Dental HQ-internal (texting employees). Reserve the number only.
- **API (local REST API, removed in our fork)**: one line each, all from https://www.opendental.com/site/apispecification.html via the release notes.
  - v25_2-api-paysplits-put-provnum: PaySplits PUT can set ProvNum.
  - v25_2-api-claimprocs-post-supplemental: ClaimProcs POST supplemental.
  - v25_2-api-sheetfields-get-single: SheetFields GET single.
  - v25_2-api-asapcomms-recalls: ASAPComms supports recalls.
  - v25_2-api-appointments-time-arrived: Appointments can set Time Arrived.
  - v25_2-api-refattaches-datetstamp: RefAttaches GET DateTStamp.
  - v25_2-api-patientnotes-fields: more patientnotes fields.
  - v25_2-api-sheetfields-put: SheetFields PUT.
  - v25_2-api-patients-get-feesched: Patients GET FeeSched.
  - v25_2-api-commlogs-put: Commlogs PUT.
  - v25_2-api-procedurelogs-prosthesis: procedurelogs prosthesis fields.
  - v25_2-api-insplans-claimform-manualfeesched: InsPlans POST/PUT ClaimFormNum and ManualFeeSchedNum.
  - v25_2-api-eobattaches-delete: EobAttaches DELETE.
  - v25_2-api-claimforms-get: ClaimForms GET.
  - v25_2-api-claimpayments-put-checkamt: ClaimPayments PUT CheckAmt.
  - v25_2-api-patients-post-premed: Patients POST Premed.

## Changes in later 25.2 point releases (listed on the 25.3 page)

The 25.3 release notes (https://www.opendental.com/site/version25_3.html) mark several items as also added to 25.2 point releases. They are not on the 25.2 page and are not in the counts above. The office runs 25.3.48, so the 25.3 spec owns them, except where noted. They are listed here so that nobody assumes "25.2 parity" means only the 66 items above.

| 25.2 build | Item | Category | Owner |
|---|---|---|---|
| 25.2.36 | Edit Vital Signs window gets a notes box (no new column in our measured schema diff) | desktop | 25.3 spec |
| 25.2.37 | Edit Medication window loses its Missing/Generic tab | desktop | 25.3 spec |
| 25.2.38 | Ortho Case window: excess text removed | desktop | 25.3 spec |
| 25.2.38 | Insurance Write-off Edit permission: clarified | desktop | this file, v25_2-security-permission-renames |
| 25.2.38 | Support Status window: QR code for help | excluded-other (HQ support is cut) | - |
| 25.2.40 | Appointment Create and Appointment Move: permission names changed | desktop | this file, v25_2-security-permission-renames |
| 25.2.40 | PayConnect errors logged in the eConnector | excluded-od-service (eConnector is cut); log PayConnect errors locally instead | - |
| 25.2.41 | DB Maintenance: the "Notes With Too Much White Space" method moves to the Tools tab | desktop | 25.3 spec |
| 25.2.41 | Imaging text: the font's real and apparent size are calculated automatically | desktop | 25.3 spec |
| 25.2.44 | New preference: payment plans can bypass the global lock date | desktop | 25.3 spec |
| 25.2.55 | Paysplits can be attached to payment plans in more cases | desktop | 25.3 spec (builds on v25_2-account-payplan-prepay-attach) |
| 25.2.62 | 2026 CDT codes added | excluded-other (ground rule 4: no ADA CDT text in the repo; codes come from a user import) | - |

## Open questions

1. **Where the XConnect API key lives in 25.2+.** The `LocationID` DB comment dropped its XConnect mention. Inspect `clearinghouse.LocationID` values in the restored `fox-db` before deciding the conversion.
2. **Ins Pick slot convention.** How Open Dental marks primary versus secondary on an Ins Pick field. Read the `eformfielddef` rows with `FieldType=9` in `fox-db`.
3. **Preference, ProgName and DisplayReport names.** Diff the `preference`, `program` and `displayreport` rows in the restored `fox-db` against our enums. This settles the proposed names in this file.
4. **Observing the office's licensed 25.3 install.** Many Low-confidence items (permission renames, Patient Forms window, Edit Benefit layout, time-bar preference, replication delays, blur, DBM tool scope, offline wiki, CARC logic, Medical EOB prompt) could be confirmed by looking at the office's own running Open Dental 25.3. This is black-box observation, not decompiling, but it goes beyond "public documentation only". **The owner should decide whether it is allowed.**
5. **Medical EOB prompt trigger.** Our design hooks the prompt into the existing "Prompt for primary claims" flow (`ClaimMedReceivedPromptForPrimaryClaim`): when a medical claim is received, ask to attach the medical EOB to the primary dental claims. Two other readings are possible. (a) Ask to scan the EOB for the medical claim's own payment when it is received. (b) Ask when a dental claim is created for a patient who also has medical coverage. Choose one.
6. **XConnect claim submission.** `EtransType` 36/37 suggest Open Dental may also *send* claims through XConnect, not only validate them. Decide whether we need that, and get DentalXChange's API documentation either way.
