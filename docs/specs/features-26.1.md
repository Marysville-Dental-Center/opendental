# Open Dental 26.1: desktop feature specs for this fork

Status: draft spec, 2026-10-18. Baseline: this fork (Open Dental 24.3.63). Target: parity with Open Dental 26.1.70, after the 25.3 schema adoption described in [schema-changes-24.3-to-26.1.md](schema-changes-24.3-to-26.1.md).

This file specs every change listed on the 26.1 release-notes page (53 items, counting the ODTouch block as one), plus two 26.1 point-release backports announced on the 26.2 page (26.1.63 and 26.1.64), one change announced only in the 26.1 highlights blog post, four schema changes in the 26.1 diff document that no release note mentions, and one rule found only in the full 26.1 schema document (procedure-code characters). That is 61 items. Each item is classified, and every desktop or vendor-direct item is written so an engineer can build it without reading Open Dental's pages again.

## How this spec was built (read first)

- **Sources used (all public).**
  - Release notes: <https://www.opendental.com/site/version26_1.html> (all sections read). The 26.2 page <https://www.opendental.com/site/version26_2.html> was read for "backported to 26.1.x" items, because our target is 26.1.70.
  - Highlights blog post: <https://opendental.blog/software-update-stable-version-26-1-highlights/>. This is Open Dental's own public announcement, and it adds details the release notes leave out.
  - Published database documentation. The 26.1 diff document <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> lists every table, column and enum value added in 26.1. The full documents for <https://www.opendental.com/OpenDentalDocumentation26-1.xml>, 25.4 and 25.3 were diffed with a script to confirm it and to catch changed column descriptions.
  - The public 24.3 manual (`https://www.opendental.com/manual/*.html`, the same pages as `/manual243/`), which describes **our baseline** behavior. Each page's version picker marks v24.3 as the selected version; its "v24.4 +" entry links to the restricted-access notice. Versioned folders for 24.4 and later (`/manual244/`, `/manual253/`, `/manual261/`) return 404. The public Quick Manual (`https://www.opendental.com/site/0_*.html`) and public bridge pages (`https://www.opendental.com/site/bridge*.html`) are current-version pages.
- **The 26.1 manual is not public.** Open Dental's help pages for version 24.4 and later are only served to customers on support. The `/help261/` pages return a "blocked from public access" stub, as <https://www.opendental.com/site/helpfeature.html> explains. The 26.1 release-notes page itself links to only one manual page (ODTouch). We did not use any customer-gated page, and we never decompiled anything. So, where the public sources are silent, behavior below is **our design**, marked **Inferred**. Before release, confirm each Inferred point against a 26.1 trial install, as the decoupling plan already suggests for schema.
- **Preference names.** Preferences are rows in the `preference` table, so they are not in the schema documents. Every new preference name below is **proposed**. To stay data-compatible with an office that later arrives from real Open Dental 26.1, reconcile the names against a 26.1 database before the migration ships.
- **Schema.** All 26.1 columns were checked against the office's 25.3.48 DDL (`D:/Dev/opendental-schema/fox-db-schema.sql`). None of them exists there, except `benefit.ToothRange`, which 25.3 already has. DDL below follows the inferred conventions in [schema-changes-24.3-to-26.1.md](schema-changes-24.3-to-26.1.md), Part 2, 26.1 section: `NOT NULL`, integer default 0, `datetime` default `'0001-01-01 00:00:00'`, and an index on `bigint` foreign keys.
- **IDs.** Each item is `v26_1-<module>-<short-name>`. Dependencies on features from 24.4–25.4 use the IDs in the sibling files `features-24.4.md` through `features-25.4.md` (for example `v25_3-chat-intraoffice-chat`). The fact-check pass reconciled every dependency ID against those files.
- **Sizes.** S is 2 days or less, M is 3–5 days, L is 1–2 weeks, XL is more than 2 weeks, for one engineer who knows this codebase. Each estimate covers the item alone; dependencies are separate.
- **Touchpoints.** These are real paths in this repo (`D:/Dev/opendental`), with line numbers as of commit `ff2e2a5b4`. The fact-check confirmed every path exists and the line numbers still hold at `de8d81965`; only docs changed between the two commits.

## Summary

Counts: **36 desktop**, **3 vendor-direct**, **22 excluded** (61 items).

| ID | Title | Module | Category | Size | 26.1 schema | Depends on |
|---|---|---|---|---|---|---|
| v26_1-family-insplan-layout | Edit Insurance Plan window layout overhaul | family | desktop | M | none | none |
| v26_1-family-inshist-codegroups | Insurance History categories come from Code Groups | family | desktop | L | codegroup.ShowInHistory, HistProcCode, IsPerioFourQuads | v26_1-setup-codegroup-history-options, v25_1-family-inshist-two-dates, v25_2-family-ins-history-edit-remove, v24_4-setup-codegroup-ranges |
| v26_1-family-benefit-agelimit-teeth | Age limits by tooth number in Edit Benefits | family | desktop | M | (benefit.ToothRange, from 25.3) | v25_4-family-benefit-tooth-exclusions (which builds on v25_3-family-exclusions-tooth), v25_3-family-otherbenefits-grid |
| v26_1-account-adjust-percent-of-fee | Edit Adjustment: Fixed Amount or Percent of Fee | account | desktop | M | none | v26_1-setup-adjtype-default-percent (defaults only) |
| v26_1-account-payment-invoice-filter | Payment window: Invoice filter on Outstanding Charges | account | desktop | M | none | none |
| v26_1-account-claim-receive-related-prompt | Prompt to send related outstanding claims when any claim is received | account | desktop | M | none | none |
| v26_1-account-service-date-view | Service Date View overhaul, plus statements in Service Date View | account | desktop | L | statement.IsServiceDateView | v25_4-account-service-date-view |
| v26_1-account-repeatcharge-backdate-pref | Preference to allow or prevent backdated repeating charges | account | desktop | S | none (pref) | none |
| v26_1-account-dxc-attach-pdf | DentalXChange attachments accept PDF documents (26.1.64) | account | vendor-direct | M | none | none |
| v26_1-account-era-claimproc-link | Link received claimprocs to the exact ERA claim | account | desktop | M | claimproc.Etrans835AttachNum | none |
| v26_1-chart-groupnote-second-signature | Group Note: second signature box | chart | desktop | M | procnote.UserNum2, Signature2, SigIsTopaz2 | v25_1-chart-group-note-flexible, v25_2-security-domain-login-change-user, v25_4-chart-groupnote-size (same window) |
| v26_1-chart-ptinfo-gender | Gender display field in Chart Patient Information | chart | desktop | S | none | none |
| v26_1-chart-eform-sig-datetime | eForms record when a signature was made | chart | desktop | S | eformfield.DateTimeSig | none |
| v26_1-imaging-pdf-javascript-pref | Preference to allow JavaScript in PDFs shown in Imaging | imaging | desktop | S | none (pref) | none |
| v26_1-imaging-pearl-logging | Pearl AI bridge: troubleshooting log level | imaging | vendor-direct | S | none (program property) | none |
| v26_1-manage-chat-sound | Chat Settings: audible notification for new messages | manage | desktop | S | chatuserod.PlayNotificationSound | v25_3-chat-intraoffice-chat |
| v26_1-manage-chat-autolaunch | Chat opens automatically when the user has unread messages | manage | desktop | S | none | v25_3-chat-intraoffice-chat |
| v26_1-manage-eds-claim-validation | Enhanced claim validation for the EDS clearinghouse | manage | vendor-direct | M | none | none |
| v26_1-manage-dunning-html | Dunning messages and billing e-mail template support HTML | manage | desktop | M | dunning.EmailType | none |
| v26_1-manage-timecard-future-clockout | Allow future-dated clock-out events (26.1.63) | manage | desktop | S | none | none |
| v26_1-lists-employee-title | Employees: Title field (Employee Titles definitions) | lists | desktop | S | employee.Title, DefCat 56 | none |
| v26_1-lists-carrier-preauth-flags | Carriers flag procedures that need a preauthorization | lists | desktop | L | carrier.PreAuthCodes | v24_4-setup-codegroup-ranges, v26_1-lists-proccode-no-hyphens |
| v26_1-lists-problem-merge-keep | Problem List merge: choose the problem to keep | lists | desktop | M | none | none |
| v26_1-lists-canada-othercodes-password | Canada: Other Procedure Codes tools require a password | lists | desktop | S | none | v25_4-canada-other-proc-codes |
| v26_1-lists-proccode-no-hyphens | Procedure codes may not contain commas or hyphens | lists | desktop | S | none (rule) | none |
| v26_1-setup-automation-clinic-mismatch | Automation condition: procedure clinic differs from patient clinic | setup | desktop | S | AutoCondField 14 | none |
| v26_1-setup-adjtype-default-percent | Adjustment types carry a default percentage | setup | desktop | S | definition.Supp | none |
| v26_1-setup-make-recall-search-pref | Preference: Make Recall automatically searches for openings | setup | desktop | S | none (pref) | none |
| v26_1-setup-codegroup-history-options | Code Group Edit: insurance history options | setup | desktop | M | codegroup.ShowInHistory, HistProcCode, IsPerioFourQuads | v25_1-family-codegroups-hide-delete, v25_3-family-otherbenefits-grid |
| v26_1-setup-default-prov-unassigned-pref | Default provider for unassigned operatories moves to Preferences | setup | desktop | S | none | none |
| v26_1-setup-allergy-search | Allergy Setup: search bar | setup | desktop | S | none | none |
| v26_1-tools-audit-insplan-drop | Audit Trail: PlanNum logged when a plan is dropped | tools | desktop | S | none | none |
| v26_1-tools-audit-birthdate | Audit Trail: birthdate additions and changes | tools | desktop | S | none | none |
| v26_1-tools-dbm-duplicate-blockouts | DBM check SchedulesDeleteDuplicateBlockouts replaces the standalone tool | tools | desktop | S | none | none |
| v26_1-tools-dbm-missing-allergydefs | DBM check: fix allergies with a missing allergy definition | tools | desktop | S | none | none |
| v26_1-misc-audit-chartviews | Audit Trail: Chart View changes | misc | desktop | S | none | none |
| v26_1-misc-audit-eform-delete | Audit Trail: patient eForm deletions | misc | desktop | S | none | v25_4-sheets-datetimesubmitted (optional field in the log text) |
| v26_1-misc-audit-billing-finance | Audit Trail: opening and running Billing/Finance Charges | misc | desktop | S | none | none |
| v26_1-misc-task-attach-nonmodal | Task Attachment windows are non-modal | misc | desktop | S | none | none |
| v26_1-odtouch-25-app | ODTouch 25 app | odtouch | excluded-od-service | S | none | n/a |
| v26_1-imaging-betterdiag-patient-view | BetterDiagnostics Patient View annotations | imaging | excluded-od-service | S | EnumImageAnnotVendor 3 (reserve) | n/a |
| v26_1-setup-sheets-mobile-static-text | Mobile-only static text in Sheets Mobile Layout | setup | excluded-od-service | S | SheetFieldType MobileStaticText (reserve) | n/a |
| v26_1-setup-webform-setup-overhaul | Web Form Setup overhaul | setup | excluded-od-service | S | none | n/a |
| v26_1-tools-payment-portal-rename | Patient Portal Transactions renamed Payment Portal Transactions | tools | excluded-od-service | S | none | n/a |
| v26_1-eservices-eclipboard-branding-logo | eClipboard branding logos stored in the database | eservices | excluded-od-service | S | branding.ValueString to mediumtext (apply) | n/a |
| v26_1-eservices-econnector-firewall-alerts | Alerts when eConnector firewall rules fail | eservices | excluded-od-service | S | AlertType 51 (reserve) | n/a |
| v26_1-eservices-websched-existing-default-prov | Web Sched Existing Patient default provider | eservices | excluded-od-service | S | none | n/a |
| v26_1-eservices-texting-entry-user | Texting records the logged-on sender (schema only) | eservices | excluded-od-service | S | smstomobile.SecUserNumEntry (apply) | n/a |
| v26_1-misc-topaz-cloud | Topaz pads on Open Dental Cloud | misc | excluded-other | S | none | n/a |
| v26_1-api-deletedobj-table | Deleted-object snapshots for API watch tables | api | excluded-other | S | deletedobj table (apply) | n/a |
| v26_1-api-sub-schedule-deleted | API: ScheduleDeleted watch-table subscription | api | excluded-other | S | none | n/a |
| v26_1-api-sub-medicationpat-deleted | API: MedicationPatDeleted subscription | api | excluded-other | S | none | n/a |
| v26_1-api-sub-labcase-deleted | API: LabCaseDeleted subscription | api | excluded-other | S | none | n/a |
| v26_1-api-apptfielddefs-put | API: ApptFieldDefs PUT | api | excluded-other | S | none | n/a |
| v26_1-api-sub-patfield-deleted | API: PatFieldDeleted subscription | api | excluded-other | S | none | n/a |
| v26_1-api-sub-toothinitials-deleted | API: ToothInitialsDeleted subscription | api | excluded-other | S | none | n/a |
| v26_1-api-payments-refund | API: Payments POST Refund | api | excluded-other | S | none | n/a |
| v26_1-api-patients-hasins | API: Patients GET HasIns | api | excluded-other | S | none | n/a |
| v26_1-api-toothinitials-movement | API: ToothInitials POST movement types | api | excluded-other | S | none | n/a |
| v26_1-api-employers-address | API: Employers GET address and phone | api | excluded-other | S | none | n/a |
| v26_1-api-procedurelogs-snomedbodysite | API: ProcedureLogs SnomedBodySite | api | excluded-other | S | none | n/a |

### 26.1 schema changes and the items that own them

All of these are absent from the office's 25.3.48 DDL (checked), except `benefit.ToothRange`. The DDL is the one in [schema-changes-24.3-to-26.1.md](schema-changes-24.3-to-26.1.md).

Do not confuse the new 26.1 table `deletedobj` with `deletedobject`, which the office's 25.3.48 DDL has and our measured 24.3-vs-25.3 diff lists as "new". `deletedobject` is a legacy mobile-sync table (`DeletedObjectNum`, `ObjectNum`, `ObjectType`, `DateTStamp`) that 24.3 already defines in `OpenDentBusiness/TableTypes/DeletedObject.cs`; the code-schema extractor missed it because the class doesn't derive from `TableBase`. The two tables have different columns and purposes.

`procnote.SigIsTopaz2`: the published type is only "tinyint". The `tinyint(3) unsigned` above copies the 25.3 DDL type of the existing `procnote.SigIsTopaz` (**Inferred**).

| Change | DDL | Owner item | Implement? |
|---|---|---|---|
| `carrier.PreAuthCodes` | `ALTER TABLE carrier ADD PreAuthCodes text NOT NULL` | v26_1-lists-carrier-preauth-flags | yes |
| `chatuserod.PlayNotificationSound` | `ALTER TABLE chatuserod ADD PlayNotificationSound tinyint(4) NOT NULL` | v26_1-manage-chat-sound | yes (after 25.3 chat tables) |
| `claimproc.Etrans835AttachNum` | `bigint(20) NOT NULL` + index | v26_1-account-era-claimproc-link | yes |
| `codegroup.ShowInHistory`, `HistProcCode`, `IsPerioFourQuads` | `tinyint(4) NOT NULL`, `varchar(255) NOT NULL`, `tinyint(4) NOT NULL` | v26_1-setup-codegroup-history-options and v26_1-family-inshist-codegroups | yes |
| `definition.Supp` | `varchar(255) NOT NULL` | v26_1-setup-adjtype-default-percent | yes |
| `dunning.EmailType` | `tinyint(4) NOT NULL` | v26_1-manage-dunning-html | yes |
| `eformfield.DateTimeSig` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | v26_1-chart-eform-sig-datetime | yes |
| `employee.Title` | `bigint(20) NOT NULL` + index | v26_1-lists-employee-title | yes |
| `procnote.UserNum2`, `Signature2`, `SigIsTopaz2` | `bigint(20) NOT NULL` + index, `text DEFAULT NULL`, `tinyint(3) unsigned NOT NULL` | v26_1-chart-groupnote-second-signature | yes |
| `statement.IsServiceDateView` | `tinyint(4) NOT NULL` | v26_1-account-service-date-view | yes |
| `branding.ValueString` text to mediumtext | `ALTER TABLE branding MODIFY ValueString mediumtext NOT NULL` | v26_1-eservices-eclipboard-branding-logo | schema only, for data compatibility |
| `smstomobile.SecUserNumEntry` | `bigint(20) NOT NULL` + index | v26_1-eservices-texting-entry-user | schema only |
| new table `deletedobj` (`DeletedObjNum`, `DateTEntry`, `ObjType`, `ObjSerialized`) | see schema spec | v26_1-api-deletedobj-table | schema only (no writers) |
| Enum values: DefCat 56 EmployeeTitles, AutoCondField 14 PatientProcedureClinicMismatch | code enums | employee title, automation items | yes |
| Enum values to reserve but not use: AlertType 51 (EServiceListenerError), EnumImageAnnotVendor 3 (BetterDiagnosticsPatientView), EnumBrandingType 1 (LogoImage), SheetFieldType MobileStaticText, eServiceCode 57/58, EServiceTypes additions, EnumPermType 276 LogScraperHQ, ProcStat 0 None | code enums | excluded items | add the values so rows from a real 26.1 database never fail to load |

### New preferences (proposed names, reconcile with a 26.1 database)

| Proposed PrefName | Type / default | Item | UI location |
|---|---|---|---|
| `RepeatingChargesAllowBackdating` | bool, `1` (keeps 24.3 behavior) | v26_1-account-repeatcharge-backdate-pref | Preferences > Account > Rec and Rep Charges |
| `ImagingAllowPdfJavascript` | bool, `0` | v26_1-imaging-pdf-javascript-pref | Preferences > Imaging > General |
| `AppointmentMakeRecallSearchesOpenings` | bool, `1` (keeps 24.3 behavior) | v26_1-setup-make-recall-search-pref | Preferences > Appointments > General |
| `BillingEmailType` | EmailType as int, `0` (Regular) | v26_1-manage-dunning-html | Billing Defaults, e-mail section |
| `BillingDefaultServiceDateView` (optional) | bool, `0` | v26_1-account-service-date-view | Billing Defaults |
| existing `ScheduleProvUnassigned` (moved, not new) | provider FK | v26_1-setup-default-prov-unassigned-pref | Preferences > Appointments > General |
| Pearl program property `Log Level` (a `programproperty` row, not a pref) | int, `0` | v26_1-imaging-pearl-logging | Setup > Program Links > Pearl |

The ten `InsHist*Codes` preferences (`InsHistBWCodes`, `InsHistDebridementCodes`, `InsHistExamCodes`, `InsHistPanoCodes`, `InsHistPerioLLCodes`, `InsHistPerioLRCodes`, `InsHistPerioMaintCodes`, `InsHistPerioULCodes`, `InsHistPerioURCodes`, `InsHistProphyCodes`) become obsolete with v26_1-family-inshist-codegroups. Migrate them, then keep the rows; don't delete them.

### Permissions and audit-trail entries

26.1 adds no permission an office would use. The only new `EnumPermType` value, 276 `LogScraperHQ`, is HQ-only. Every audit item below logs under a permission that already exists in this fork's `OpenDentBusiness/TableTypes/GroupPermission.cs`: `InsPlanEdit`, `PatientEdit`, `ChartViewsEdit` (262), `EFormDelete` (260), `Setup`, `Billing`, `ProblemDefEdit`, `CarrierEdit`, `DefEdit`, `GroupNoteEditSigned`, `TimecardsEditAll`.

---

## Family Module

### v26_1-family-insplan-layout: Edit Insurance Plan window layout overhaul

- **Category / size:** desktop, M. **Depends on:** none. Schedule it after any 24.4–25.4 changes to this window, so the layout is done once. The only 25.4 change found is v25_4-family-writeoff-override-wording, which rewords the existing "Write-Offs for Non-Covered Services Overrides" group (already in this fork, `FormInsPlan.designer.cs` line 1591).
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Family Module). <https://opendental.blog/software-update-stable-version-26-1-highlights/> says some fields moved so that everything is visible without scrolling, and that functionality is unchanged. The baseline field inventory is in <https://www.opendental.com/manual/insplan.html> (24.3).
- **User-visible behavior.** The Edit Insurance Plan window shows every field without scrolling. Nothing about saving, validation or calculations changes.
- **UI** (**Inferred** layout; the 26.1 screenshots are not public). Arrange the window as fixed panels:
  1. Patient-plan panel: relationship, patient ID, ordinal, pending, patient-plan note, Hist button (Insurance History), patient-level Audit Trail.
  2. Subscriber panel: subscriber, subscriber ID, effective and termination dates, release of information, assignment of benefits, subscriber note.
  3. Plan panel: carrier and Pick From List, employer, group name and number, plan type, fee schedules (including carrier allowed amounts), claim form, filing code, send electronically, ortho settings, plan note, plan-level Audit Trail.
  4. Benefits summary grid with the Benefits... button.
  5. Bottom bar: Drop, Label, eligibility buttons, OK and Cancel.
  - Keep a sensible tab order. Size to the smallest supported workstation resolution, which we choose (proposed: 1366x768 at 100% scaling), using the existing `LayoutManager` DPI scaling.
- **Business rules and edge cases.** Every control keeps its current enable/disable logic: plan type changes which fee-schedule fields are available, CarrierCreate permission forces Pick From List, and so on. Write a regression checklist from the current `FormInsPlan` event handlers and run it before and after.
- **Permissions / audit.** Unchanged (InsPlanEdit, InsPlanChangeCarrierName, CarrierCreate, and the rest).
- **Preferences.** None. **Schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormInsPlan.cs` (3,996 lines), `OpenDental/Forms/FormInsPlan.designer.cs` (2,286 lines; the main rewrite).

### v26_1-family-inshist-codegroups: Insurance History categories come from Code Groups

- **Category / size:** desktop, L.
- **Depends on:**
  - v26_1-setup-codegroup-history-options: the setup side of the same change.
  - v25_1-family-inshist-two-dates: 25.1 allows two dates per category.
  - v25_2-family-ins-history-edit-remove: 25.2 lets users edit or remove entered dates.
  - v24_4-setup-codegroup-ranges: code ranges in code groups.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Family: categories customized with Code Groups; Setup: insurance-history options on Code Groups).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/> (add or edit a code group and tick "Show In History").
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (codegroup columns). The full 26.1 schema document says `codegroup.CodeGroupFixed` is no longer used by the Insurance History window.
  - Baseline: <https://www.opendental.com/manual/inshistory.html> and <https://www.opendental.com/site/0_inshistory.html>. The 24.3 manual confirms: the first code of a category's preference is the one inserted as the EO procedure; a completed procedure on a received claim updates the date by itself; an EO procedure charted directly in the Chart module does not; and entering a recall-type date updates the recall due date.
- **User-visible behavior.**
  - Today, the Insurance History window has fixed rows: Exam, Prophy, BW, Pano/FMX, Perio Maint, Debridement, and SRP for each of the four quadrants. Their codes come from ten preferences.
  - In 26.1, the rows come from code groups. Each code group marked "Show In History" becomes one row, in code-group order. A group marked "Perio Four Quads" becomes four rows, one per quadrant, in the order UL, UR, LL, LR. The 26.1 schema document labels them "Perio Scaling" plus the quadrant, the same labels as the four 24.3 rows. Use those labels. (Whether a renamed group shows its own name instead is **Inferred**; confirm on a 26.1 install.)
  - Offices can add categories of their own, for example sealants or fluoride.
- **UI.**
  - Insurance History window (Ins Plan > Hist, or a double-click on the Family module's insurance-history grid). Grid columns stay as today: category, last date(s), and date entry. The row set is now dynamic.
  - The Family module's insurance-history area under each plan shows the same dynamic rows.
  - Preferences > Treatment Plan > General: remove the ten Insurance History code text boxes, and put a note there pointing to Setup > Code Groups.
- **Business rules.**
  1. **Reading history.** For each history row, the shown date is the most recent of these, for this subscriber's plan:
     - EO or C procedures with an InsHist claimproc;
     - C procedures attached to a received claim.
     A procedure counts when its code is in the group's `ProcCodes`, ranges included. Four-quad rows also filter on the procedure's quadrant (`Surf` = UL, UR, LL or LR). Keep the 25.1 rule of two dates per row.
  2. **Entering a date** creates or updates an Existing Other procedure, plus an InsHist claimproc, using `codegroup.HistProcCode`, which is a single code. Four-quad rows also set `Surf` to the row's quadrant. All existing rules stay:
     - only show a newer date when a completed procedure exists;
     - update the EO procedure rather than adding a second one;
     - recall due dates update when the code is a recall trigger.
  3. **Automatic history.** Batch insurance verification (`InsBatchVerifyChangeInsHist`) and 271 eligibility imports set history today by preference. Map them to code groups instead:
     - Match first on `CodeGroupFixed` (BW, PanoFMX, Exam, Perio, Prophy, SRP, FMDebride).
     - Otherwise match on a group whose `HistProcCode` equals the code the 271 names.
     - When no group matches, skip the import line and log it.
  4. **Rule from the schema document.** `HistProcCode` is required when `ShowInHistory` is set, and must be empty when it isn't. That validation lives in the setup item.
  5. A group hidden from benefits (`IsHidden`) still shows in history when `ShowInHistory` is set; the two flags are independent.
  6. Deleting a history group (25.1 allows deletion) removes its row. Existing EO procedures and claimprocs stay.
  7. Sheets' static text fields (dateLastBW and the like) keep using `CodeGroupFixed`. Don't change them.
- **Data migration** (in the 26.1 conversion step; **Inferred**, because Open Dental's own conversion is not public). For each of the ten `InsHist*Codes` preferences that has a value:
  1. Find the code group whose `CodeGroupFixed` matches the category (Exam to Exam, Prophy to Prophy, BW to BW, Pano to PanoFMX, PerioMaint to Perio, Debridement to FMDebride, the four perio quadrants to SRP). If none exists, create one named after the category.
  2. Set `ShowInHistory=1`, and set `HistProcCode` to the first code in the preference.
  3. Make sure `ProcCodes` contains every code from the preference.
  4. For the four perio quadrant preferences, use one group with `IsPerioFourQuads=1`. Its `HistProcCode` comes from the UR preference, or the first non-empty one.
  5. Keep the preferences, unused. Make the migration idempotent.
- **Permissions / audit.** Unchanged. Entering a date creates EO procedures under today's rules (ProcExistingEdit) and logs as today.
- **Preferences.** The ten `InsHist*Codes` become obsolete. `InsBatchVerifyChangeInsHist` stays.
- **Schema.** `codegroup.ShowInHistory tinyint(4) NOT NULL` (default 0), `codegroup.HistProcCode varchar(255) NOT NULL` (default ''), `codegroup.IsPerioFourQuads tinyint(4) NOT NULL` (default 0). None of them is in the 25.3 DDL. 25.3 already has `ShowInFrequency` and `ShowInOther`, which this fork lacks.
- **Touchpoints.**
  - `OpenDental/Forms/FormInsHistSetup.cs` (rows are built from `Prefs.GetInsHistPrefs()` at lines 61–108; save at 184–208).
  - `OpenDental/Main Modules/ControlFamily.cs` (the ins-history grid at 2588–2599; opens the window at 386).
  - `OpenDental/Forms/FormInsPlan.cs` (Hist button).
  - `OpenDentBusiness/Data Interface/Prefs.cs` (533–545, `GetInsHistPrefs` and `GetInsHistPrefNames`).
  - `OpenDentBusiness/Data Interface/ProcedureCodes.cs` (`GetCodeNumsForInsHistPref`, `GetByInsHistPref`).
  - `OpenDentBusiness/Data Interface/Procedures.cs` (`GetMostRecentInsHistProc`, `InsertOrUpdateInsHistProcedure`; quadrant handling at 5812–5843).
  - `OpenDentBusiness/X12/EB271.cs` (468–495 and 821–827, which map 271 history to preferences).
  - `OpenDentBusiness/Data Interface/InsVerifies.cs` (621).
  - `OpenDentBusiness/Data Interface/ClaimProcs.cs`.
  - `OpenDental/User Controls/Preferences/UserControlTreatPlanGeneral.cs` (114–123, 161 and after).
  - `OpenDentBusiness/TableTypes/CodeGroup.cs`, `OpenDentBusiness/Data Interface/CodeGroups.cs`, `OpenDentBusiness/Crud/CodeGroupCrud.cs`.
  - A new ConvertDatabases step.

### v26_1-family-benefit-agelimit-teeth: Age limits by tooth number in Edit Benefits

- **Category / size:** desktop, M.
- **Depends on:**
  - v25_4-family-benefit-tooth-exclusions, which builds on v25_3-family-exclusions-tooth. Together they add `benefit.ToothRange` (in the 25.3 schema) and the tooth-list parsing used for exclusions by tooth (25.4, backported to 25.3.39).
  - v25_3-family-otherbenefits-grid, the in-place Other Benefits grid where toothed age limits can also be entered.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Family: age limits support tooth numbers).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/>. A "Teeth" column in Edit Benefits takes the teeth for an age limit. Age limits with teeth can also be entered as Other Benefits when no code group fits. The example given is sealants covered only on certain molars up to a given age.
  - The full 26.1 schema document, `benefit.ToothRange`, which now names age limits by teeth as a use.
- **User-visible behavior.** An age limitation can be limited to specific teeth. The limit then applies only when the procedure's tooth is in the list. Example: sealant codes limited to permanent first and second molars through age 14. A sealant on a premolar is not affected by that row.
- **UI.**
  - Edit Benefits (`FormInsBenefits`), Age Limits grid. It currently has columns Code Group, Codes and Age (lines 485–489). Add a **Teeth** column, edited in place like Age.
  - Other Benefits grid and `FormBenefitEdit`: add a **Teeth** text box. Enable it when the benefit is an age limitation, or an exclusion (25.4). Otherwise disable it and clear it.
- **Business rules.**
  1. Store teeth in `ToothRange` as a comma-separated list of single teeth in internal (universal) numbering, for example `2,3,14,15` or `A,B,I,J`. Show and accept hyphen ranges (`2-3,14-15`), and convert to and from international numbering when that preference is on. Reject unknown teeth; remove duplicates.
  2. **Estimating.** When a procedure falls under an age-limit benefit that has teeth, the limit applies only if the procedure's `ToothNum` is in the set. Procedures without a tooth number (quadrant or arch codes) never match a toothed age limit.
  3. **Precedence.** When the same code group has both a toothed and a toothless age-limit row, the toothed row governs matching teeth and the toothless row governs everything else (**Inferred**).
  4. The patient's age is taken at the procedure date, as today. A procedure that fails a toothed age limit is treated as an age-limitation denial. The existing `InsPlansZeroWriteOffsOnFreqOrAging` preference therefore applies, along with its per-plan override `insplan.InsPlansZeroWriteOffsOnFreqOrAgingOverride`. Both are already in 24.3 (`OpenDentBusiness/TableTypes/InsPlan.cs` line 108); neither is a 25.4 addition.
  5. Exclusions with teeth (25.4) never consider age. Keep the two benefit kinds distinct.
- **Permissions / audit.** Unchanged (InsPlanEdit; benefit changes go to the insurance edit log as today).
- **Preferences.** None.
- **Schema.** `benefit.ToothRange varchar(255) DEFAULT NULL`. It is already in the 25.3 DDL, so 26.1 changes only its meaning. Treat NULL and '' as "no teeth".
- **Touchpoints.**
  - `OpenDental/Forms/FormInsBenefits.cs` (age-limit grid 485–489; other grids 529–650) and its designer.
  - `OpenDental/Forms/FormBenefitEdit.cs`, `OpenDental/Forms/FormBenefitFrequencies.cs`.
  - `OpenDentBusiness/TableTypes/Benefit.cs`, `OpenDentBusiness/Crud/BenefitCrud.cs`, `OpenDentBusiness/Data Interface/Benefits.cs` (age-limit evaluation).
  - `OpenDentBusiness/Data Interface/ClaimProcs.cs` (estimate computation).
  - `OpenDentBusiness/Logic/Tooth.cs` (tooth parsing and international numbering).

---

## Account Module

### v26_1-account-adjust-percent-of-fee: Edit Adjustment: Fixed Amount or Percent of Fee

- **Category / size:** desktop, M. **Depends on:** v26_1-setup-adjtype-default-percent, for defaults only. The percentage entry works without it.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Account: Edit Adjustment gains a percent-of-fee mode). <https://opendental.blog/software-update-stable-version-26-1-highlights/> describes two radio buttons: Fixed Amount, the old method, and Percent of Fee, where the user types a discount percentage and the program fills in the dollars after the user picks an adjustment type. The post doesn't say which fee the percentage uses, or whether a procedure must be attached; rule 1 and the "disabled when no procedure is attached" UI rule are **Inferred** from this fork's Add Multiple Adjustments. Baseline: <https://www.opendental.com/manual/adjustments.html> (24.3).
- **User-visible behavior.** In Edit Adjustment, the user picks **Fixed Amount**, the default and the current behavior, or **Percent of Fee**. With Percent of Fee the user types a percentage, and the window shows the computed dollar amount. It computes when an adjustment type is selected, and recomputes whenever the type, the percentage or the attached procedure changes.
- **UI.** `FormAdjust`:
  - Add a radio pair, Fixed Amount and Percent of Fee, next to the amount box.
  - With Percent of Fee, the amount box's label changes to "Percent", and a read-only "Amount" label shows the result.
  - Percent of Fee is disabled when no procedure is attached; attaching one enables it.
- **Business rules.**
  1. The fee basis is the same as in Add Multiple Adjustments (`FormAdjMulti`, `AdjAmtType.PercentOfFee`): the procedure's original charge amount (`AccountEntryProc.AmountOriginal`).
  2. `AdjAmt = round(percent / 100 × basis, 2)`. The adjustment type's sign then applies: a `-` type makes it negative.
  3. Percent must be from 0 to 1000 with up to 2 decimals. Values over 100 are allowed for surcharge types.
  4. Only the dollar amount is stored. Reopening a saved adjustment shows Fixed Amount with that amount.
  5. For a **new** adjustment, choosing a type that has a default percentage (`definition.Supp`) switches to Percent of Fee and fills in the percentage. Editing an existing adjustment never applies defaults.
  6. Existing checks still apply: AdjustmentCreate, AdjustmentEdit, the zero-amount rule (AdjustmentEditZero), lock dates and AdjustmentTypeDeny. So do the 24.3 manual's other rules: the Negative Adjustments preference (Block refuses a subtraction larger than the procedure's remaining patient portion; Warn shows the overpaid warning), and the provider and clinic matching rules for attached procedures under Rigorous allocation. Run them on the computed dollar amount.
- **Permissions / audit.** Unchanged. Optionally, add "(N% of fee)" to the security-log text for the adjustment.
- **Preferences.** None. **Schema.** None (it reads `definition.Supp`).
- **Touchpoints.**
  - `OpenDental/Forms/FormAdjust.cs` (amount handling at 107–113, 237–242, 307–349, 415–423) and `OpenDental/Forms/FormAdjust.designer.cs`.
  - `OpenDental/Forms/FormAdjMulti.cs`: reuse the logic in `GetAdjFromUI` (273–336) and `GetAdjAmtType` (339–349). Consider moving the calculation into `OpenDentBusiness/Data Interface/Adjustments.cs`.

### v26_1-account-payment-invoice-filter: Payment window: Invoice filter on Outstanding Charges

- **Category / size:** desktop, M. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Account: the Payment window can filter its Outstanding Charges by invoice).
  - The 26.1 schema document, `statement.IsInvoice`: an invoice holds procedures, adjustments and/or pay plan charges, built from the selected Account rows or else from today's items.
  - The 26.1 schema document, `payplancharge.StatementNum`: pay plan charges are linked to an invoice directly, not through stmtlink.
- **User-visible behavior.** The patient pays a specific invoice. In the Payment window, the user picks that invoice in a new **Invoice** filter. Outstanding Charges then shows only the charges on that invoice, so auto-split and manual allocation apply to just those charges.
- **UI.** `FormPayment`, "Filtering" group above Outstanding Charges (`groupBoxFilteringOutstanding`):
  - Add a label "Invoice" and a combo box. Its first item is "All"; then one item per invoice for the family, shown as invoice date, statement number and invoice total. Newest first.
  - The filter combines with the existing patient, provider, clinic, type, amount and code filters (logical AND).
- **Business rules.**
  1. An invoice is a `statement` with `IsInvoice=1`. Its members are procedures and adjustments linked through `stmtlink` (`StmtLinkTypes` proc and adj), plus pay plan charges whose `payplancharge.StatementNum` equals the invoice.
  2. List only invoices that still have at least one member with a remaining balance in the outstanding list. Fully paid invoices disappear.
  3. Invoice copies (`IsInvoiceCopy`) are the same statement, so they get no extra entries.
  4. With the filter on, "Show All Charges" (`checkShowAll`) still works inside the invoice's member set.
  5. Changing the filter keeps existing splits. It only changes what is shown and what auto-split targets.
- **Permissions / audit.** Unchanged (PaymentCreate and PaymentEdit).
- **Preferences.** None. **Schema.** None: `stmtlink` and `payplancharge.StatementNum` already exist in 24.3.
- **Touchpoints.**
  - `OpenDental/Forms/FormPayment.cs` (filter fill methods 1592–1802; `FillGridCharges` 1802) and `OpenDental/Forms/FormPayment.Designer.cs` (filter controls 179–197).
  - `OpenDentBusiness/Data Interface/StmtLinks.cs` (`GetForStatementAndType`, line 14).
  - `OpenDentBusiness/Data Interface/Statements.cs`.
  - `OpenDentBusiness/Db Multi Table/PaymentEdit.cs` (charge construction).

### v26_1-account-claim-receive-related-prompt: Prompt to send related outstanding claims when any claim is received

- **Category / size:** desktop, M. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Account: in Edit Claim, receiving a claim of any ordinal now offers to send the related unsent claims).
  - The 26.1 schema document, `claim.ClaimStatus`: status H is now described as hold until **prior** received, where it used to say primary.
  - The 26.1 schema document, `patplan.Ordinal`: ordinals go up to 10.
- **User-visible behavior.**
  - Today the prompt appears in only two cases: an unsent secondary claim when a primary is received (pref `PromptForSecondaryClaim`), and an unsent primary claim when a medical claim is received.
  - In 26.1, receiving **any** claim offers to handle the unsent claims for the same procedures at later ordinals. So receiving a secondary prompts for the tertiary, and so on. The choices stay the same: change status to Waiting to Send, Send now, Print now, or Do nothing. Do nothing is hidden when the matching "force status" preference is on.
- **UI.** The same input box built in `ClaimL.PromptForPrimaryOrSecondaryClaim`. Generalize its wording: "There is at least one unsent claim for the next insurance on the received procedures." Show the claim status "H" as "Hold Until Prior Received" in status drop-downs and grids.
- **Business rules.**
  1. **Trigger.** The release note names the Edit Claim window: the claim is saved as Received after payment was entered (`FormClaimEdit` 4144–4153). This fork also prompts at claim creation, in `ClaimL.CreateClaimFromWrapper` (line 213; prompt at 317–320), when the lead claim is already received and the next-ordinal claim is waiting. Extend that path the same way, for consistency (**Inferred**). It must not prompt during ERA automatic processing or batch receiving. Those paths use the force-status preferences silently.
  2. **Related claims** are the claims that:
     - are not received (status U, H or I);
     - are not preauthorizations;
     - share at least one ProcNum with the received claim;
     - belong to the patient's plan with the next higher ordinal. Generalize `Claims.GetPrimaryOrSecondaryClaimsNotReceived` to "next ordinal".
  3. Keep the medical-to-primary branch (`ClaimMedReceivedPromptForPrimaryClaim`).
  4. When `ClaimPrimaryReceivedRecalcSecondary` is on, recalculate the next-ordinal claims' estimates after any receipt, not only after a primary's (**Inferred**).
  5. The user must have ClaimSend, checked silently as today.
- **Permissions / audit.** ClaimSend, plus the existing claim logs.
- **Preferences.** No new preference. The existing `PromptForSecondaryClaim` (label in Preferences > Account > Claim Receive) now governs all ordinals. Relabel it "Prompt for claims of the next insurance". `ClaimPrimaryRecievedForceSecondaryStatus`, `ClaimMedReceivedForcePrimaryStatus` and `ClaimPrimaryReceivedRecalcSecondary` keep their meaning, extended to any ordinal.
- **Schema.** None.
- **Touchpoints.**
  - `OpenDental/Forms/FormClaimEdit.cs` (4142–4160).
  - `OpenDental/Logic/ClaimL.cs` (`PromptForPrimaryOrSecondaryClaim` 613–700, callers 317–320).
  - `OpenDentBusiness/Data Interface/Claims.cs` (`GetPrimaryOrSecondaryClaimsNotReceived` 213; 1059).
  - `OpenDental/User Controls/Preferences/UserControlAccountClaimReceive.cs` (63, 84) and its designer (label at 211).

### v26_1-account-service-date-view: Service Date View overhaul, plus statements in Service Date View

- **Category / size:** desktop, L. **Depends on:** v25_4-account-service-date-view (25.4 made the Account module's Service Date View easier to reach and use).
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Account: Service Date View overhauled for the Patient Account grid).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/> (the view groups line items by date of service; single or batch statements can be sent in this view).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (`statement.IsServiceDateView`).
  - The full 26.1 schema document, `statement.IsReceipt` and `statement.IsInvoice`, for receipt and invoice rules in this view.
  - Baseline: <https://www.opendental.com/manual/servicedateview.html> and <https://www.opendental.com/manual/account.html> (24.3).
- **User-visible behavior.**
  1. **Account module.** The main account grid can switch between the regular, chronological view and **Service Date View**. That view groups the ledger by date of service: each charge (procedure, unattached adjustment, pay plan charge) appears with the credits applied to it indented beneath. Those credits are insurance payments, write-offs, patient payment splits and attached adjustments. Each row shows the remaining balance. In 24.3 this was a separate report window (`FormRpServiceDateView`). 26.1 brings it into the main grid. **Inferred** detail, since the 26.1 layout is not public.
  2. **Statements.** Single statements (Statement Options) and batch statements (Billing Options) get a **Service Date View** checkbox, saved as `statement.IsServiceDateView`. The statement's main grid is then built in service-date layout. Printing, PDF, e-mail, and saving to Imaging all use it.
- **UI.**
  - **Account module.** A view toggle near the grid: radio buttons, or a "Service Date View" checkbox, replacing the 24.3 `butServiceDateView` window. In SDV mode, show a "Show details" checkbox, which adds insurance-estimate and patient-portion columns as in the 24.3 report. (Per the 24.3 manual, that report shows only daily totals when details are off.)
    - Columns in SDV: Date, Patient, Code/Description, Charge, Credits, Balance for the service.
    - Double-clicking a row opens its object (procedure, payment, claim, adjustment), as in regular view.
  - **Statement Options** (`FormStatementOptions`): a "Service Date View" checkbox.
  - **Billing Options** (`FormBillingOptions`): a "Service Date View" checkbox. Its default comes from the optional `BillingDefaultServiceDateView` preference.
- **Business rules.**
  1. Build both the SDV grid and SDV statements from one business method: generalize `RpServiceDateView.GetData(patNum, isFamily, isDetailed)` so it takes a statement's options (date range, single patient, intermingled, hide payments).
  2. Each credit appears under every procedure it is attached to, with the amount allocated to that procedure. Unattached credits appear on their own dates. This covers prepayments and unearned income, claim payments made "as total" with no procedure links, and unattached adjustments.
  3. **Receipts in SDV** contain all of today's payments, together with the procedures those payments pay, even when the procedures are older. Receipts in regular view keep today's payments only.
  4. **Invoices** in either view are built from the selected Account rows. With nothing selected, today's procedures, adjustments and pay plan charges are used.
  5. Aging, balance totals, amount due and the dunning text are the same in both views; only the main grid differs.
  6. Limited statements and super-family statements: disable the SDV checkbox for them in the first release (**Inferred** scope cut). Revisit later.
  7. Re-printing a saved statement uses the stored flag, so a statement always re-renders in the view it was sent in.
- **Permissions / audit.** Unchanged (statement creation as today).
- **Preferences.** Optional `BillingDefaultServiceDateView` (bool, default 0).
- **Schema.** `statement.IsServiceDateView tinyint(4) NOT NULL` (default 0). It is absent from the 25.3 DDL. Add the field to `OpenDentBusiness/TableTypes/Statement.cs` and to the CRUD.
- **Touchpoints.**
  - `OpenDental/Main Modules/ControlAccount.cs` (`butServiceDateView_Click` 220–238; `gridAccount` fill and click handling 302–475) and `ControlAccount.Designer.cs`.
  - `OpenDental/Forms Reports/FormRpServiceDateView.cs` and `OpenDentBusiness/Reports/RpServiceDateView.cs` (`GetData`, line 11).
  - `OpenDentBusiness/Db Multi Table/AccountModules.cs` (`GetStatementDataSet`, line 211).
  - `OpenDentBusiness/TableTypes/Statement.cs`, `OpenDentBusiness/Crud/StatementCrud.cs`, `OpenDentBusiness/Data Interface/Statements.cs`.
  - `OpenDental/Forms/FormStatementOptions.cs`, `OpenDental/Forms/FormBillingOptions.cs`.
  - `OpenDentBusiness/SheetFramework/SheetUtil.cs` (`StatementMain` grid, 323 and 484), `SheetFiller.cs`, `SheetDataTableUtil.cs`.

### v26_1-account-repeatcharge-backdate-pref: Preference to allow or prevent backdated repeating charges

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Account: a new preference that turns repeating-charge backdating on or off). Baseline: <https://www.opendental.com/manual/repeatingcharges.html> (24.3), which confirms that missed charges are posted on their scheduled dates when the tool runs up to one month and 20 days late, and mentions no preference.
- **User-visible behavior.** Today, running repeating charges posts any missed billing dates from roughly the last month and 20 days, dated on those past billing dates. `RepeatCharges.GetBillingDatesHelper` keeps up to three dates "for backdating purposes". The new preference lets an office stop that, so that no charge is ever posted with a date before the day the tool runs.
- **UI.**
  - Preferences > Account > Rec and Rep Charges (`UserControlAccountRecAndRepCharges`, Repeating Charges group): add a checkbox "Allow repeating charges to be backdated". Checked by default.
  - Edit Repeating Charge: when backdating is off and the start date is in the past, show a note that no charges will be posted for past billing dates.
- **Business rules** (**Inferred** semantics; confirm on a 26.1 install).
  1. Allowed (default): today's behavior.
  2. Prevented: the run filters out billing dates earlier than the run date before it creates procedures. A missed billing date is not posted later; the next qualifying date posts normally.
  3. This applies to the manual run (Tools > Repeating Charges) and to automatic runs alike.
  4. Security lock dates still block any date before the lock date, whatever the preference says.
  5. Duplicate detection (`IsRepeatDateHelper`) is unchanged.
- **Permissions / audit.** Changing the preference logs like other preference changes (Setup). Runs keep the existing RepeatChargeTool permission and logs.
- **Preferences.** `RepeatingChargesAllowBackdating` (proposed; bool; default '1').
- **Schema.** None (a preference row).
- **Touchpoints.**
  - `OpenDentBusiness/Data Interface/RepeatCharges.cs` (`RunRepeatingCharges` 237; `GetBillingDatesHelper` 462–499, especially 481–499).
  - `OpenDental/User Controls/Preferences/UserControlAccountRecAndRepCharges.cs` (85–116) and its designer.
  - `OpenDental/Forms/FormRepeatChargeEdit.cs`, `OpenDental/Forms/FormRepeatChargesUpdate.cs`.
  - `OpenDentBusiness/TableTypes/Pref.cs`, plus a ConvertDatabases step that inserts the preference.

### v26_1-account-dxc-attach-pdf: DentalXChange attachments accept PDF documents (26.1.64)

- **Category / size:** vendor-direct, M. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_2.html> (Account: the DentalXChange attachment service now takes PDFs; the 26.2 page says this was backported to 26.1.64). Our target, 26.1.70, includes it. Vendor overview: <https://www.opendental.com/site/dentalxchange.html>, which says only that setup uses credentials DentalXChange issues; it gives no file types or size limits. So rules 1 and 2 below are **Inferred**.
- **User-visible behavior.** When attaching documents to a claim through DentalXChange (ClaimConnect), a PDF can be chosen, either from disk or from the patient's Imaging documents, and sent as an attachment. Today, PDFs are refused with a "not supported" message.
- **UI.**
  - Claim attachment window (`FormClaimAttachment`): its file picker accepts `.pdf` when the clearinghouse is DentalXChange.
  - The attachment grid shows PDF rows with a page-1 thumbnail or a PDF icon.
  - The attachment item editor still collects the document type and the narrative.
- **Business rules.**
  1. Send PDFs to DentalXChange directly with the office's own ClaimConnect credentials, as the existing image attachments are sent, base64-encoded with a PDF content type. Follow DentalXChange's published size limits.
  2. Encrypted or password-protected PDFs are rejected with a clear message.
  3. EDS attachments keep refusing PDFs unless EDS documents PDF support. (The 24.3 EDS attachment manual page says PDFs are not supported.)
  4. "Snip" and "Paste" capture stay image-only.
  5. Match the `.pdf` extension case-insensitively. Today's check (`selectedFile.EndsWith(".pdf")`, line 523) misses `.PDF`, which then fails in `Image.FromFile` instead of getting a clear message.
- **Permissions / audit.** Unchanged (ClaimSend and the attachment logs).
- **Preferences.** None. **Schema.** None.
- **Touchpoints.**
  - `OpenDental/Forms/FormClaimAttachment.cs` (the PDF refusal at 523–526; attachment list and send code 486–491, 592–689).
  - `OpenDental/Forms/FormClaimAttachmentItemEdit.cs`, `OpenDental/Forms/FormImagePickerDXC.cs`.
  - `OpenDentBusiness/Eclaims/ClaimConnect.cs` (`ImageAttachment` and the attachment API calls).

### v26_1-account-era-claimproc-link: Link received claimprocs to the exact ERA claim

- **Category / size:** desktop, M. **Depends on:** none.
- **Sources.** No release note covers this. The evidence is the 26.1 diff document, <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml>: the new `claimproc.Etrans835AttachNum` is a foreign key to `etrans835attach`, set only on claimprocs entered from an ERA (X12 835), and it links to the ERA claim, not to an individual procedure on it.
- **User-visible behavior** (**Inferred**). When a claim payment is entered from an ERA, OD remembers which ERA claim (one CLP loop within one 835) produced each received or supplemental claimproc. The ERA window can then show reliably whether each ERA claim has been processed, even when:
  - the same claim appears in several ERAs, as with split payments, reversals and corrections;
  - the same claim appears twice in one ERA.
- **UI.** There is no new control. The ERA window (`FormEtrans835Edit`) uses the link for its received/processed status column and for the warning against posting the same ERA claim twice.
- **Business rules.**
  1. Set the link when claimprocs are created or updated by an ERA. That happens through the manual "enter payment" flow from the ERA window, and through automatic ERA processing where enabled.
  2. Clear it when:
     - the ERA claim is detached from the claim;
     - the claim payment is deleted;
     - the claimprocs are un-received.
  3. Rows from before the upgrade keep 0. For them, keep the current matching logic as a fallback.
  4. A claimproc entered by hand (an EOB, not an ERA) always has 0.
- **Permissions / audit.** Unchanged.
- **Preferences.** None.
- **Schema.** `claimproc.Etrans835AttachNum bigint(20) NOT NULL` (default 0) plus an index. It is absent from the 25.3 DDL.
- **Touchpoints.**
  - `OpenDentBusiness/TableTypes/ClaimProc.cs`, `OpenDentBusiness/Crud/ClaimProcCrud.cs`, `OpenDentBusiness/Data Interface/ClaimProcs.cs`.
  - `OpenDentBusiness/Data Interface/Etrans835Attaches.cs`, `OpenDentBusiness/TableTypes/Etrans835Attach.cs`.
  - `OpenDental/Forms/FormEtrans835Edit.cs`, `OpenDental/Forms/FormEtrans835ClaimPay.cs`, `OpenDental/Forms/FormEtrans835ClaimSelect.cs`.
  - `OpenDental/Logic/EtransL.cs`, `OpenDentBusiness/X12/X835.cs`.

---

## Chart Module

### v26_1-chart-groupnote-second-signature: Group Note: second signature box

- **Category / size:** desktop, M. **Depends on:** none for the data. Build it after the earlier changes to the same window: v25_1-chart-group-note-flexible (group notes with any procedures or none), v25_2-security-domain-login-change-user (domain login on Change User), and v25_4-chart-groupnote-size (larger, resizable window).
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Chart: a second signature box in the Group Note window).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/> (two users, such as dentist and hygienist, can sign one group note).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (procnote columns).
  - Baseline: <https://www.opendental.com/manual/procgroupnote.html> (24.3).
- **User-visible behavior.** The Procedure Group Note window has two signature areas. The first works as today. The second has its own user name, can be signed by a different user, and is stored with the same note.
- **UI.** `FormProcGroup`: add a second `SignatureBoxWrapper` labeled "Signature 2". Give it a user display and a "Change User" button, matching the first box's change-user flow, with the 25.2 domain-login option where present.
  - The Chart progress-notes grid shows the note as signed when either box is signed. The tooltip or signed column lists both signers.
  - Printed progress notes print a second signature line when present.
- **Business rules.**
  1. Both signatures are made over the same key data as the existing signature (note text plus the existing key fields). Editing the note text invalidates and clears **both** signatures, so both users must sign again.
  2. The second signer must be a different user from the first signer (**Inferred**, but implied by the purpose).
  3. A user signs a box only as themselves: the box's user is set to the current user when they sign, or switched with Change User.
  4. A note that has any signature can be edited only by:
     - its signer;
     - a user with GroupNoteEditSigned.
     This is today's rule, applied to either signature.
  5. `ProcNoteSigsBlockedAutoNoteIncomplete` blocks signing either box while auto-note prompts are incomplete.
  6. Topaz pads work for either box (`SigIsTopaz2`).
  7. Each save inserts a new `procnote` row, as today (notes keep their history). The row now carries `UserNum2`, `Signature2` and `SigIsTopaz2`.
  8. **Signature-cleared alert.** Today, when a user edits a note and clears someone else's signature, the window raises a `SignatureCleared` alert for the original signer (`FormProcGroup` 468–490, AlertType 39). Do the same for the second signer (`UserNum2`): alert each signer whose signature was cleared by another user.
  9. Locked group notes (`IsLocked`, under Procedure Lock) stay read-only for both boxes; Append and Invalidate work as today. Invalidated notes can't be edited, as the 24.3 manual describes.
- **Permissions / audit.** GroupNoteEditSigned, as above. Saving is still gated as today (`FormProcGroup` 89–118): ProcCompleteNote, checked against the procedure date, when a completed procedure is attached; otherwise ProcDelete, checked against the entry date. The Lock button respects the ProcCompleteEdit or ProcExistingEdit global lock date. Existing procedure-note security logs record the second signer's name.
- **Preferences.** None.
- **Schema.** `procnote.UserNum2 bigint(20) NOT NULL` (default 0) plus an index, `procnote.Signature2 text DEFAULT NULL`, `procnote.SigIsTopaz2 tinyint(3) unsigned NOT NULL` (default 0). None of them is in the 25.3 DDL.
- **Touchpoints.**
  - `OpenDental/Forms/FormProcGroup.cs` (signature handling at 68, 141–154, 334–354, 412–416, 470, 500) and its designer.
  - `OpenDentBusiness/UI/SignatureBoxWrapper.cs`.
  - `OpenDentBusiness/TableTypes/ProcNote.cs`, `OpenDentBusiness/Crud/ProcNoteCrud.cs`, `OpenDentBusiness/Data Interface/ProcNotes.cs`.
  - `OpenDentBusiness/TableTypes/Procedure.cs` (non-database note and signature fields 159–172; add `UserNum2`, `Signature2`, `SigIsTopaz2`).
  - `OpenDentBusiness/Data Interface/Procedures.cs` (the procnote joins at 395–396 and 805 must select the new columns).
  - `OpenDentBusiness/Data Interface/ProcGroupItems.cs`.
  - `OpenDental/Main Modules/ControlChart.cs` (progress notes).

### v26_1-chart-ptinfo-gender: Gender display field in Chart Patient Information

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Chart: a new display field to show Gender in the Patient Information area).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/>.
  - Baseline: <https://www.opendental.com/manual/displayfields.html> (24.3).
- **User-visible behavior.** In Setup > Display Fields > Chart Patient Information, "Gender" can be added to the shown fields. The Chart module's patient-info grid then has a Gender row.
- **UI.** The field appears among the available fields for that category; it isn't on by default. Its row shows the patient's gender as the same text used in Edit Patient Information. The Family module's Patient Information grid already offers a "Gender" display field in 24.3 (`DisplayFields.cs` 223 and 723; rendered in `ControlFamily.cs`, `case "Gender"` at 847, as the raw enum name `_patient.Gender.ToString()`, so an unknown gender shows "Unknown" there). Use one rendering in both modules: proposed, the translated Edit Patient Information text, with the empty-cell rule below.
- **Business rules.** Read-only. An unknown gender shows as an empty cell. Double-clicking the row opens Edit Patient Information, as other demographic rows do.
- **Permissions / audit.** None. **Preferences.** None. **Schema.** None (`displayfield` rows).
- **Touchpoints.**
  - `OpenDentBusiness/Data Interface/DisplayFields.cs` (both ChartPatientInformation lists: 303–346 and 806–847).
  - `OpenDental/Main Modules/ControlChart.cs` (patient-info switch: "Age" at 4108, "Preferred Pronoun" at 4432; add a "Gender" case).
  - `OpenDental/Main Modules/ControlFamily.cs` (existing "Gender" case at 847, as the model).

### v26_1-chart-eform-sig-datetime: eForms record when a signature was made

- **Category / size:** desktop, S. **Depends on:** none (eForms already exist in this fork under `WpfControlsOD`).
- **Sources.** Schema evidence only: <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> adds `eformfield.DateTimeSig`, the date and time the eForm was signed. No release note covers it.
- **User-visible behavior** (**Inferred**). When a patient or staff member signs an eForm's signature box in Fill eForm, the date and time are saved with the signature. They show under the signature, on screen and on prints and PDFs.
- **UI.** In `CtrlEFormFill`, show a small "Signed <date time>" caption under a signed signature box.
- **Business rules.**
  1. Set `DateTimeSig` to the server's time when the signature changes from blank or different to signed.
  2. Clear it to the minimum date when the signature is cleared.
  3. Leave it alone when the form is saved again without touching the signature.
  4. It applies only to signature-box fields.
- **Permissions / audit.** Unchanged (EFormEdit).
- **Preferences.** None.
- **Schema.** `eformfield.DateTimeSig datetime NOT NULL DEFAULT '0001-01-01 00:00:00'`. It is absent from the 25.3 DDL.
- **Touchpoints.**
  - `OpenDentBusiness/TableTypes/EFormField.cs`, `OpenDentBusiness/Crud/EFormFieldCrud.cs`, `OpenDentBusiness/Data Interface/EFormFields.cs`.
  - `WpfControlsOD/UI/Controls/UserControls/CtrlEFormFill.xaml.cs`, `WpfControlsOD/Frms/FrmEFormFillEdit.xaml.cs`, `WpfControlsOD/Frms/FrmEFormSigBoxEdit.xaml.cs`.

---

## Imaging Module

### v26_1-imaging-pdf-javascript-pref: Preference to allow JavaScript in PDFs shown in Imaging

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Imaging: a new preference that decides whether PDF scripts may run in the Imaging module's viewer). The rest of this item is **Inferred**; no public page describes it.
- **User-visible behavior.** PDFs shown inside the Imaging module run no embedded scripts unless an administrator turns the preference on. Some fillable or calculating PDFs need scripts.
- **UI.** Preferences > Imaging > General (`UserControlImagingGeneral`): a checkbox "Allow JavaScript when viewing PDFs", unchecked by default.
- **Business rules.**
  1. This fork shows PDFs in an embedded WebView2 (`ControlImageDisplay._odWebView2`). Before each PDF navigation, set script permission on that WebView2 from the preference.
  2. Verify with a test PDF that contains a script, both that it is blocked and that it runs. If the setting doesn't reach Edge's PDF viewer, use the equivalent WebView2 environment option. **Inferred** implementation detail.
  3. A change takes effect on the next PDF load.
  4. It doesn't affect PDFs opened in an external viewer (`PdfLaunchWindow`).
- **Permissions / audit.** Setup (the Preferences window). Logged like other preference changes.
- **Preferences.** `ImagingAllowPdfJavascript` (proposed; bool; default '0').
- **Schema.** None.
- **Touchpoints.**
  - `OpenDental/User Controls/Preferences/UserControlImagingGeneral.cs` (100–127) and its designer.
  - `OpenDental/User Controls/ControlImageDisplay.cs` (WebView2 creation 157–162, PDF navigation 939–972).
  - `CodeBase/Controls/ODWebView2.cs` (settings 69–70).
  - `OpenDentBusiness/TableTypes/Pref.cs`.

### v26_1-imaging-pearl-logging: Pearl AI bridge: troubleshooting log level

- **Category / size:** vendor-direct, S. **Depends on:** none (the Pearl bridge exists in 24.3 and calls Pearl's API directly).
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Misc: new log settings to help diagnose problems with the Pearl bridge). The current public bridge page, <https://www.opendental.com/site/bridgepearlai.html>, shows a "Log Level" bridge property: default 0, set to 2 for verbose logging during troubleshooting, then back to 0. Process page: <https://www.opendental.com/site/pearlaiprocess.html>.
- **User-visible behavior.** In Setup > Program Links > Pearl, a new "Log Level" property controls how much the Pearl integration writes to the local log files.
- **UI.** A new program property row, "Log Level", in the bridge's property grid (`FormProgramLinkEdit`).
- **Business rules.**
  1. The values follow this fork's existing `CodeBase` `LogLevel` enum (`Logger.cs` ~line 701: 0 Error, 1 Information, 2 Verbose). The public bridge page documents only 0 (the default; verbose logging off) and 2 (verbose logging on, for troubleshooting). The meaning of 1 is ours (**Inferred**).
     - 0: errors only (failed calls, timeouts, rejected or deleted results).
     - 1: errors plus one line per upload and per completed result.
     - 2: each API call with endpoint, request ID, document number, HTTP status, timing and polling progress.
  2. Never log the client secret, auth tokens, presigned upload URLs or image bytes.
  3. Log under a `Pearl` subfolder through `CodeBase/Utilities/Logger.cs`, using its existing age-based cleanup.
  4. Also log the case that is a TODO today: a failed result in `Pearl.cs` (line 372).
- **Permissions / audit.** Setup (Program Links).
- **Preferences.** None. Add the program property with default "0" in a ConvertDatabases step.
- **Schema.** None (a `programproperty` row).
- **Touchpoints.**
  - `OpenDentBusiness/Bridges/Pearl/Pearl.cs` (property constants 84–90, error path 372).
  - `OpenDentBusiness/Bridges/Pearl/PearlApiClient.cs` (all HTTP calls).
  - `CodeBase/Utilities/Logger.cs` (`WriteLine` 192–231; `LogLevel` 702).
  - `OpenDental/Forms/FormProgramLinkEdit.cs`.

---

## Manage Module

### v26_1-manage-chat-sound: Chat Settings: audible notification for new messages

- **Category / size:** desktop, S. **Depends on:** v25_3-chat-intraoffice-chat. The 25.3 intra-office chat (tables `chat`, `chatmsg`, `chatattach`, `chatreaction`, `chatuserattach`, `chatuserod`) is not in this fork yet.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Manage: in Chat Settings, an option to play a sound for new messages).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (`chatuserod.PlayNotificationSound`; off by default).
  - Feature overview: <https://www.opendental.com/site/0_chat.html>. It says chat history is kept in the office's own database. It also says Chat needs a registration key and isn't offered on Open Dental Cloud. Our fork drops registration keys (decoupling plan), and the chat must stay local (ground rule 6). The 26.1 schema document adds `eServiceCode` 57, described as an "xChatService api". This hints that Open Dental may relay chat through a service of its own. We don't: notifications come from the local database.
- **User-visible behavior.** Each user can turn on a sound for incoming chat messages.
- **UI.** Chat Settings window, a per-user checkbox: "Play a sound for new messages".
- **Business rules.**
  1. Play a short sound when a message from someone else arrives in one of the user's chats. Use a bundled sound or the Windows notification sound.
  2. No sound for the user's own messages, while the user's status is Do Not Disturb, or while notifications are muted. Muting is set per user (`chatuserod.MuteNotifications`; `MuteImportantNotifications` for messages with `chatmsg.IsImportant`) and per chat (`chatuserattach.IsMute`). All of these columns are in the 25.3 DDL.
  3. At most one sound per refresh cycle, however many messages arrived.
- **Permissions / audit.** None (a personal setting).
- **Preferences.** None.
- **Schema.** `chatuserod.PlayNotificationSound tinyint(4) NOT NULL` (default 0). Add it with the 25.3 chat tables; it is absent from the 25.3 DDL.
- **Touchpoints.** There is no chat code in this fork. The work lands in the files created by v25_3-chat-intraoffice-chat: a ChatUserod table type, CRUD, data interface and the Chat settings form. The Manage module button goes in `OpenDental/Main Modules/ControlManage.cs`.

### v26_1-manage-chat-autolaunch: Chat opens automatically when the user has unread messages

- **Category / size:** desktop, S. **Depends on:** v25_3-chat-intraoffice-chat.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Manage: Chat opens by itself when the user who logs on has unread messages).
- **User-visible behavior.** After logging on, or switching user, a user with unread chat messages sees the Chat tool open by itself.
- **Business rules.**
  1. Check once per log-on: does any `chatuserattach` row for the user have `IsRead=0`? Skip rows for chats the user has left (`DateTimeRemoved` set) and chats the user muted (`IsMute=1`) (**Inferred**).
  2. If Chat is already running for that user, bring it forward; don't open a second instance.
  3. Respect the 25.3 option to open Chat in the background (`chatuserod.OpenBackground`).
  4. Never auto-launch for a user with no chat access, or when the workstation runs as a service.
- **UI / permissions / preferences / schema.** None new.
- **Touchpoints.** The log-on completion path in `OpenDental/Main Modules/FormOpenDental.cs`, plus the 25.3 chat launcher.

### v26_1-manage-eds-claim-validation: Enhanced claim validation for the EDS clearinghouse

- **Category / size:** vendor-direct, M. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Manage: EDS claims get stronger validation; the note gives no details).
  - Baseline: <https://www.opendental.com/manual/edsattachmentservice.html> (24.3: Send or Save on a **new** claim validates it with EDS, and the window shows a claim validation status).
  - Vendor page: <https://www.opendental.com/site/electronicdentalservices.html>.
- **User-visible behavior** (**Inferred** scope). Claims going to EDS are checked more thoroughly before they leave the office, and the user sees every problem in one list.
- **Business rules.**
  1. Validate with EDS (the office's own EDS credentials, the clearinghouse's `LoginID` and password) in all of these cases:
     - on Send and on Save for **new** claims, as today;
     - on Send for **existing** claims;
     - for every EDS claim in a batch send from the Send Claims window.
  2. Add EDS-specific local checks to `Eclaims.GetMissingData` before any network call:
     - billing and treating provider NPI and taxonomy;
     - the payer ID is present and on the EDS payer list;
     - the subscriber ID is present;
     - service dates are not in the future;
     - a narrative is present when a procedure code requires one.
  3. Show all errors and warnings from EDS and from the local checks in one validation dialog per claim. Errors block sending. Warnings allow "Send anyway".
  4. Store the last validation result on the claim's attachment/validation status display, as 24.3 already does for attachments.
  5. If EDS can't be reached, warn and let the user send or cancel. Never block silently.
- **Permissions / audit.** ClaimSend, unchanged.
- **Preferences.** None. **Schema.** None.
- **Touchpoints.**
  - `OpenDentBusiness/Eclaims/EDS.cs` (`ValidateClaim` 363; `CreateValidateAttachmentJSON` 381; `ValidateAttatchmentResources` 506; endpoint constants 29–30).
  - `OpenDentBusiness/Eclaims/Eclaims.cs` (EDS branch 146; `GetMissingData` 240).
  - `OpenDental/Forms/FormClaimAttachment.cs` (EDS validation call 197–220).
  - `OpenDental/Forms/FormClaimEdit.cs` (Send and Save), `OpenDental/Forms/FormClaimsSend.cs`.

### v26_1-manage-dunning-html: Dunning messages and billing e-mail template support HTML

- **Category / size:** desktop, M. **Depends on:** none (this fork already has HTML e-mail templates).
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Manage: Billing List dunning messages and their e-mail templates can now be HTML).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/> (HTML formatting for billing e-mails: logo, formatted text, hyperlinks).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (`dunning.EmailType`, with the EmailType values Regular 0, Html 1 and RawHtml 2).
  - Baseline: <https://www.opendental.com/manual/billingdunningmsg.html>, <https://www.opendental.com/manual/billingdefaults.html>, <https://www.opendental.com/manual/emailhtml.html> (24.3). The 24.3 dunning page confirms that the "Email Statement Override" subject and body accept variables and override the Billing Defaults e-mail message. It says nothing about HTML.
- **User-visible behavior.** The billing e-mail template in Billing Defaults, and each dunning message's e-mail override, can be written as plain text (today), as HTML inside the standard master template, or as raw HTML. Statements e-mailed from the Billing List then go out as HTML e-mails, with the statement still attached.
- **UI.**
  - **Edit Dunning Message** (`FormDunningEdit`), group "Email Statement Override": add an e-mail type selector (Plain Text, HTML, Raw HTML) and an "Edit HTML..." button that opens the HTML editor and preview used by e-mail templates (`FormEmailTemplateEdit`).
  - **Billing Defaults** (`FormBillingDefaults`), e-mail subject and body: the same type selector and HTML editor.
- **Business rules.**
  1. The body comes from the dunning override when one applies, otherwise from Billing Defaults. Its type travels with it.
  2. **Regular** keeps today's behavior.
  3. **Html** wraps the body in the master template, then replaces tags such as [nameFL], [PatNum] and the statement and message-to-pay tags.
  4. **RawHtml** uses the body as written, then replaces tags.
  5. Build HTML e-mails the way existing HTML templates are sent: an HTML part plus a plain-text fallback.
  6. Tag-presence checks (the message-to-pay URL tags in `BillingL` and `FormBillingOptions`) must also look inside HTML bodies.
  7. `statement.EmailBody` overrides created in Billing Options inherit the source's type (**Inferred**).
- **Permissions / audit.** Billing for the Billing List; Setup for Billing Defaults. No new log entries.
- **Preferences.** `BillingEmailType` (proposed; EmailType as int; default '0'). It sits next to `BillingEmailSubject`, `BillingEmailBodyText` and `BillingEmailIncludeAutograph`.
- **Schema.** `dunning.EmailType tinyint(4) NOT NULL` (default 0). It is absent from the 25.3 DDL.
- **Touchpoints.**
  - `OpenDental/Forms/FormDunningEdit.cs` and `OpenDental/Forms/FormDunningEdit.designer.cs` (lower-case "designer"; groupBox4 "Email Statement Override", 262–310), `OpenDental/Forms/FormDunningSetup.cs`.
  - `OpenDentBusiness/TableTypes/Dunning.cs`, `OpenDentBusiness/Crud/DunningCrud.cs`, `OpenDentBusiness/Data Interface/Dunnings.cs`.
  - `OpenDental/Forms/FormBillingDefaults.cs` (81–83, 380–382), `OpenDental/Forms/FormBillingOptions.cs` (939–941).
  - `OpenDentBusiness/Data Interface/Statements.cs` (body build at 284), `OpenDentBusiness/Logic/BillingL.cs` (1025–1027).
  - `OpenDental/Forms/FormEmailTemplateEdit.cs` (HTML pattern 56, 272–274), `OpenDentBusiness/Data Interface/EmailMessages.cs`.

### v26_1-manage-timecard-future-clockout: Allow future-dated clock-out events (26.1.63)

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_2.html> (Manage: time cards accept a clock-out dated in the future; the 26.2 page says this was backported to 26.1.63). Our target, 26.1.70, includes it. Baseline: <https://www.opendental.com/site/0_timecards.html>, which says only that time-card editing can be limited by security permissions. Nothing public describes the 26.1 rules, so the rules below are **Inferred** except where they restate today's code.
- **User-visible behavior.** When an administrator edits a clock event, the clock-out can be dated after today. A typical use is entering the end of a shift ahead of time. Today `FormClockEventEdit` refuses either time when its **date** is after today (lines 382–411). It compares dates only, so a time later today is already accepted.
- **UI.** `FormClockEventEdit`. Remove the future-date refusal for the clock-out side, and show a confirmation instead: "Clock-out is in the future. Save anyway?"
- **Business rules.**
  1. Clock-in still can't be in the future.
  2. Clock-out must still be after clock-in.
  3. Only users with TimecardsEditAll may save a future clock-out (**Inferred**; the window already checks TimecardsEditAll at line 87).
  4. Mind the break events. For a `Break` event the window swaps the roles: the first time is the clock-out (start of break) and the second is the clock-in (return). The existing messages already do this. Allow a future date only on the event's clock-out side. For a break that means only a future start of break, which has little use. Proposed: allow future dates only for Home and Lunch events, and keep refusing them for breaks (**Inferred**).
  5. While the clock-out is in the future, the employee's clock status still shows as clocked in.
  6. Time-card totals, overtime rules and payroll export count the stored times as they are (**Inferred**).
- **Permissions / audit.** TimecardsEditAll. Log the edit with old and new times, as clock-event edits are logged today.
- **Preferences.** None. **Schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormClockEventEdit.cs` (future-date checks 382–411), `OpenDental/Forms/FormTimeCard.cs`, `OpenDentBusiness/Data Interface/ClockEvents.cs`.

---

## Lists

### v26_1-lists-employee-title: Employees: Title field (Employee Titles definitions)

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Lists: a Title field in the Employees list).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (`employee.Title`, a foreign key to a definition; new DefCat EmployeeTitles = 56, used only to show job titles).
- **User-visible behavior.** Offices maintain a list of job titles in Definitions and assign one to each employee. The title shows in the Employees list.
- **UI.**
  - Setup > Definitions: a new category, "Employee Titles". It is a plain name list, with hide and reorder.
  - Employee Edit: a "Title" drop-down with "None" plus the titles. A hidden title appears only when it is the employee's current one.
  - Employees list (`FormEmployeeSelect`): a "Title" column.
- **Business rules.** Display only; no other logic reads it. Deleting or hiding a title definition leaves employees pointing at it. The list shows the name, with "(hidden)" when hidden.
- **Permissions / audit.** DefEdit for definitions. The existing employee-edit permission and logs apply.
- **Preferences.** None.
- **Schema.** `employee.Title bigint(20) NOT NULL` (default 0) plus an index. Add the C# enum value `DefCat.EmployeeTitles = 56`. Both are absent from the 25.3 DDL.
- **Touchpoints.**
  - `OpenDentBusiness/TableTypes/Employee.cs`, `OpenDentBusiness/Crud/EmployeeCrud.cs`, `OpenDentBusiness/Data Interface/Employees.cs`.
  - `OpenDental/Forms/FormEmployeeEdit.cs` and its designer, `OpenDental/Forms/FormEmployeeSelect.cs` (grid 72–91).
  - `OpenDentBusiness/TableTypes/Def.cs` (`DefCat` enum, line 67).
  - `OpenDentBusiness/Data Interface/Defs.cs` (782: every `DefCat` value needs an entry), `OpenDental/Logic/DefL.cs`, `OpenDental/Forms/FormDefinitions.cs`.

### v26_1-lists-carrier-preauth-flags: Carriers flag procedures that need a preauthorization

- **Category / size:** desktop, L.
- **Depends on:**
  - v24_4-setup-codegroup-ranges: shared parsing of code lists with ranges.
  - v26_1-lists-proccode-no-hyphens: codes must not contain the separators.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Lists: each carrier in the Carriers list can mark procedures that need a preauthorization).
  - <https://www.opendental.com/site/versions.html> (26.1 summary: PreAuth flags by carrier).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/>. Codes are added per carrier. Charting one of them reminds the user to create a preauth, and the Treatment Plan module marks that procedure. A new PreAuthFlag field also shows the reminder in the Appointments module, once an office puts it in a view or in the bubble.
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (`carrier.PreAuthCodes`: code list with commas and hyphen ranges, no spaces; non-breaking hyphens for display). The full 26.1 schema document's example mixes ranges and single codes (a range of exam and x-ray codes, a range of amalgams, and one crown code).
- **User-visible behavior.**
  1. In Edit Carrier, enter the procedure codes (and ranges) the carrier wants preauthorized.
  2. Charting one of those codes as treatment-planned for a patient covered by that carrier pops up a reminder to create a preauthorization.
  3. In the Treatment Plan module, those procedures are marked "preauth needed" until a preauth exists.
  4. Appointment views and bubbles can show a **PreAuthFlag** when the appointment holds such a procedure.
- **UI.**
  - **Edit Carrier** (`FormCarrierEdit`): a text box "Procedures requiring preauthorization", with a "..." button that opens the procedure-code picker to add codes. Show ranges with non-breaking hyphens.
  - **Chart module.** After a TP procedure is added (`ControlChart.AddProcHelper`), show a reminder naming the carrier and code, with buttons "Create Preauth Now" (opens the existing preauth flow) and "Later".
  - **Treatment Plan module.** A visible marker on the procedure row: a "PreAuth" flag column, or a text prefix in the description, **Inferred** presentation.
  - **Appointment View Edit** (`FormApptViewEdit`): a new available element, `PreAuthFlag`. The bubble display fields get the same element. It shows a short "PreAuth" tag in the carrier's appointment text color.
- **Business rules.**
  1. A procedure "needs preauth" when all of these hold:
     - its status is TP or TPi;
     - at least one of the patient's active plans has a carrier whose `PreAuthCodes` matches the procedure's code;
     - no preauth claimproc (claimproc status Preauth) exists for that procedure and that plan.
  2. **Code matching.**
     - Commas separate entries; `X-Y` means every code from X to Y.
     - Compare codes of the same length and prefix in ordinal string order; spaces are not allowed.
     - Store a plain hyphen; show U+2011. Convert back on save.
  3. **Validation on save.** Reject spaces, empty entries and ranges whose start is after their end. Warn, but allow, codes that don't exist yet.
  4. Show the reminder once per charting action, even when several codes match: one dialog listing all of them.
  5. The flags clear automatically once a preauth claimproc exists, sent or not.
  6. **Carrier combine** (`FormCarrierCombine`): the surviving carrier keeps its own list, merged with the union of the combined carriers' lists (**Inferred**).
  7. Changes to the field are written to the insurance edit log like other carrier fields.
- **Permissions / audit.** CarrierEdit to change the list. The insurance edit log records the change.
- **Preferences.** None.
- **Schema.** `carrier.PreAuthCodes text NOT NULL` (default ''). It is absent from the 25.3 DDL.
- **Touchpoints.**
  - `OpenDental/Forms/FormCarrierEdit.cs` and its designer, `OpenDental/Forms/FormCarrierCombine.cs`.
  - `OpenDentBusiness/TableTypes/Carrier.cs`, `OpenDentBusiness/Crud/CarrierCrud.cs`, `OpenDentBusiness/Data Interface/Carriers.cs`, `OpenDentBusiness/Data Interface/InsEditLogs.cs`.
  - `OpenDental/Main Modules/ControlChart.cs` (`AddProcedure` 9535, `AddProcHelper` 9584, quick-add callers 2205–2257 and 9707–9783).
  - `OpenDental/Main Modules/ControlTreat.cs` (TP grid; existing preauth button 173 and 390).
  - `OpenDental/Logic/ApptViewItemL.cs`, `OpenDentBusiness/TableTypes/ApptViewItem.cs`, `OpenDental/Forms/FormApptViewEdit.cs`, `OpenDental/UI/Appt/ControlApptPanel.cs`.
  - `OpenDentBusiness/Data Interface/Appointments.cs` (appointment field data).
  - `OpenDentBusiness/Data Interface/DisplayFields.cs` (bubble fields).
  - `OpenDentBusiness/Data Interface/CodeGroups.cs` (shared range parser).

### v26_1-lists-problem-merge-keep: Problem List merge: choose the problem to keep

- **Category / size:** desktop, M. **Depends on:** none. This fork's Problem List (`FormDiseaseDefs`, `DiseaseDefs.cs`) has no merge at all, so this item builds the whole merge with the 26.1 "choose which to keep" behavior. No release note from 24.4 to 25.4, and no sibling spec file, adds the basic Problem merge. The 26.1 note implies it existed before 26.1, so it probably came in a point release. Confirm its behavior on a 26.1 install.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Lists: when merging problems, the user picks the survivor). The model to follow is this fork's allergy merge: <https://www.opendental.com/manual/allergiesmaster.html> (24.3) and `FormAllergySetup.buttonMerge_Click`. Note that the 24.3 manual says the allergy merge keeps an arbitrary one of the two; in code it is simply the first selected row. The 26.1 problem change exists to remove exactly that arbitrariness.
- **User-visible behavior.** In Setup > Problems, the user selects two problems and clicks **Merge**. A dialog asks which one to keep. Every reference to the other problem moves to the kept one, and the other is deleted.
- **UI.**
  - `FormDiseaseDefs`: a Merge button, enabled when exactly two rows are selected and the window is not in selection mode.
  - Merge dialog: two radio buttons, each with the problem's name, ICD-10 and SNOMED codes, and how many patients use it. Then OK and Cancel.
  - Warn that the merge can't be undone.
- **Business rules.**
  1. Repoint every reference from the discarded `diseasedef` to the kept one, at least:
     - `disease.DiseaseDefNum` (patient problems);
     - `rxalert.DiseaseDefNum`;
     - EHR and CDS references that store DiseaseDefNum (education resources, reminder rules, family health, CDS triggers): grep `DiseaseDefNum`.
  2. Then delete the discarded definition.
  3. Patients who now have the same problem twice: keep both rows (history), but warn in the result message (**Inferred**).
  4. Pick the kept definition's codes; never mix the two definitions' fields.
  5. Run in one transaction-like sequence and refresh the cache afterwards.
- **Permissions / audit.** ProblemDefEdit, with no new permission (26.1 adds none). Security log: "Problem <discarded name> merged into <kept name>".
- **Preferences.** None. **Schema.** None.
- **Touchpoints.**
  - `OpenDental/Forms/FormDiseaseDefs.cs` and its designer.
  - `OpenDentBusiness/Data Interface/DiseaseDefs.cs` (add `Merge`/`Combine` modeled on `AllergyDefs.Combine`).
  - `OpenDentBusiness/Data Interface/AllergyDefs.cs` (model), `OpenDental/Forms/FormAllergySetup.cs` (82–112 model).

### v26_1-lists-canada-othercodes-password: Canada: Other Procedure Codes tools require a password

- **Category / size:** desktop, S. **Depends on:** v25_4-canada-other-proc-codes (25.4 added CDHA, DAC and RAMQ code sets and the Other Procedure Codes window).
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Misc: in Canada, the Other Procedure Codes tools are now password-protected). <https://www.opendental.com/site/canadaproccodetools.html>, the current public page. Its Advanced Options change existing codes in bulk: the description, the abbreviation, the treatment area and the paint type of each. The page says these options need a password. It lists CDHA, Denturist Association (DAC), RAMQ, ACDQ (through Open Dental support, members only) and CDA as code systems. The ACDQ path relies on Open Dental staff, so it is out of scope for us.
- **User-visible behavior.** In Procedure Codes > Tools > Other Codes (Canada only), running the Advanced Options asks for a password. That guards against accidental mass edits.
- **UI.** A password prompt shown when the user clicks OK with any Advanced Option ticked.
- **Business rules.** Open Dental's password is a fixed word printed in its public manual, so it is only a speed bump, not security. For our fork, use a confirmation phrase that we document in our own help: a speed bump of the same strength. Also require ProcCodeEdit and SecurityAdmin, which the page already lists for code tools. Importing a code system without Advanced Options needs no password.
- **Permissions / audit.** SecurityAdmin plus ProcCodeEdit. The existing ProcCodeEdit logs record code changes.
- **Preferences.** None. **Schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormProcTools.cs` (Canada tools, 32 and 91–180), the Other Codes window created by the 25.4 item, and `OpenDentBusiness/Data Interface/ProcedureCodes.cs`.

### v26_1-lists-proccode-no-hyphens: Procedure codes may not contain commas or hyphens

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources.** Schema evidence: the 26.1 full schema document now limits `procedurecode.ProcCode` to 15 characters and forbids commas, hyphens and non-breaking hyphens (U+2011). The 25.4 description had no such limit. Diff of <https://www.opendental.com/OpenDentalDocumentation25-4.xml> against <https://www.opendental.com/OpenDentalDocumentation26-1.xml>.
- **Why.** Code groups and `carrier.PreAuthCodes` store code lists with commas and hyphen ranges. A code that contains those characters breaks parsing.
- **User-visible behavior.** Adding or renaming a procedure code that contains a comma, a hyphen (either kind) or more than 15 characters is refused with a message.
- **Business rules.**
  1. Enforce on manual add, on edit where the code text can change, and on every import path: fee and code imports, code-system imports, Canadian downloads.
  2. Existing bad codes are not changed automatically. Add a DBM check that lists them, so the office can rename them; it doesn't fix them.
- **Permissions / audit.** ProcCodeEdit, unchanged.
- **Preferences.** None. **Schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormProcCodeNew.cs`, `OpenDental/Forms/FormProcCodeEdit.cs`, `OpenDentBusiness/Data Interface/ProcedureCodes.cs`, `OpenDental/Forms/FormProcTools.cs`, `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`.

---

## Setup

### v26_1-setup-automation-clinic-mismatch: Automation condition: procedure clinic differs from patient clinic

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Setup: Edit Automation gets a condition that fires when a completed procedure's clinic doesn't match).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/> (warns when the patient's clinic and the procedure's clinic differ; works only with the ProcedureComplete trigger).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (AutoCondField 14 PatientProcedureClinicMismatch; ProcedureComplete only; no compare string; comparison None).
  - Baseline: <https://www.opendental.com/manual/automation.html> (24.3; the conditions are described on this page). It already has conditions that work with only one trigger: three that work only with RxCreate, and Claim Contains Procedure Code, which works only with the Create Claim and OpenClaim triggers. Follow the same pattern.
- **User-visible behavior.** An automation on the ProcedureComplete trigger can require that the completed procedure's clinic differs from the patient's home clinic. The usual action is a pop-up warning.
- **UI.** Automation Condition Edit: the field list includes "Patient Procedure Clinic Mismatch" **only** when the parent automation's trigger is ProcedureComplete. Choosing it hides or disables the comparison and value inputs, and saves comparison None and an empty compare string.
- **Business rules.**
  1. The condition is true when `procedure.ClinicNum != patient.ClinicNum`, evaluated per completed procedure.
  2. Patients with no clinic (0) count as a mismatch against any non-zero procedure clinic.
  3. With clinics turned off, the condition is always false.
  4. Saving an automation whose trigger isn't ProcedureComplete is refused while it has this condition.
- **Permissions / audit.** Setup (Automation), unchanged.
- **Preferences.** None.
- **Schema.** None. Add the C# enum value `AutoCondField.PatientProcedureClinicMismatch = 14`; this fork has values 0–13.
- **Touchpoints.**
  - `OpenDentBusiness/TableTypes/AutomationCondition.cs` (enum at line 30).
  - `OpenDentBusiness/Data Interface/Automations.cs` (condition switch 372–440; `Trigger` 100).
  - `OpenDentBusiness/Data Interface/AutomationConditions.cs`.
  - `OpenDental/Forms/FormAutomationConditionEdit.cs`, `OpenDental/Forms/FormAutomationEdit.cs`, `OpenDental/Logic/AutomationL.cs`.

### v26_1-setup-adjtype-default-percent: Adjustment types carry a default percentage

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Setup: Adj Type definitions can set a default percentage).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/>.
  - The 26.1 schema document, `definition.Supp`: extra value for definition categories; used today only for adjustment types' default percent, read by both adjustment windows; text such as "90.50"; 0 to 1000; empty means no default.
- **User-visible behavior.** Each adjustment type can have a default percentage. New adjustments of that type start in Percent of Fee mode with that percentage filled in.
- **UI.** Definitions > Adj Types > edit: a "Default %" text box. Empty means none.
- **Business rules.**
  1. Store the value in `definition.Supp` as an invariant-culture decimal, at most 2 places. Allow 0 to 1000; empty means none. Show it in the user's culture.
  2. Treat `Supp` as category-specific: only the AdjTypes category reads it, and other categories must leave it empty.
  3. The value is used by v26_1-account-adjust-percent-of-fee and by Add Multiple Adjustments (`FormAdjMulti`).
- **Permissions / audit.** DefEdit, with the existing definition logs.
- **Preferences.** None.
- **Schema.** `definition.Supp varchar(255) NOT NULL` (default ''). It is absent from the 25.3 DDL. Add the field to `Def` and the CRUD, and include it in the definition cache.
- **Touchpoints.**
  - `OpenDentBusiness/TableTypes/Def.cs`, `OpenDentBusiness/Crud/DefCrud.cs`, `OpenDentBusiness/Data Interface/Defs.cs`.
  - `OpenDental/Forms/FormDefEdit.cs` (AdjTypes handling at 127 and 286) and its designer.
  - `OpenDental/Forms/FormAdjust.cs`, `OpenDental/Forms/FormAdjMulti.cs`.

### v26_1-setup-make-recall-search-pref: Preference: Make Recall automatically searches for openings

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Setup: a new preference that decides whether Make Recall runs the opening search; the page says it was backported to 25.4.50).
- **User-visible behavior.** Today, Make Recall in the Appointments module always puts the new recall appointment on the pinboard and then opens and runs the opening search. With the preference off, it only pins the appointment.
- **UI.** Preferences > Appointments > General: a checkbox, "Make Recall automatically searches for openings", checked by default.
- **Business rules.** When off, skip `ShowSearch()` and `DoSearch(isForMakeRecall:true)` in both paths:
  - the toolbar Make Recall button;
  - the "pinboard and search" result from Other Appointments.
  Pinning, date jump and refresh still happen. Weekly view behaves as today.
- **Permissions / audit.** Setup to change it. AppointmentCreate for Make Recall, unchanged.
- **Preferences.** `AppointmentMakeRecallSearchesOpenings` (proposed; bool; default '1').
- **Schema.** None.
- **Touchpoints.**
  - `OpenDental/Main Modules/ControlAppt.cs` (`butMakeRecall_Click` 1350–1391; `OtherResult.PinboardAndSearch` 4721–4731; `DoSearch` 3689).
  - `OpenDental/Forms/FormApptsOther.cs` (200–282).
  - `OpenDental/User Controls/Preferences/UserControlApptGeneral.cs`, `OpenDentBusiness/TableTypes/Pref.cs`.

### v26_1-setup-codegroup-history-options: Code Group Edit: insurance history options

- **Category / size:** desktop, M. **Depends on:** v25_1-family-codegroups-hide-delete (the 25.1 hide and delete of code groups, plus `ShowInFrequency`), v25_3-family-otherbenefits-grid (`ShowInOther`, which the 25.3 release notes list under API), and v24_4-setup-codegroup-ranges.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Setup: insurance-history options added to Code Groups).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/> (tick Show In History).
  - <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> (`ShowInHistory`; `HistProcCode`, a single code used when history is created and required exactly when `ShowInHistory` is set; `IsPerioFourQuads`, which makes four quadrant rows).
  - Baseline: <https://www.opendental.com/manual/codegroups.html> (24.3).
- **User-visible behavior.** In Setup > Code Groups, a group can be shown as an Insurance History category. It then needs a history procedure code, and it can optionally expand into four quadrant rows.
- **UI.**
  - **Code Group Edit** (`FormCodeGroupEdit`):
    - a "Show In History" checkbox;
    - a "History Proc Code" text box with a "..." code picker, enabled only when Show In History is checked;
    - a "Perio Four Quadrants" checkbox, enabled only when Show In History is checked.
  - **Code Groups list** (`FormCodeGroups`): a "Hist" column next to Freq and Age.
- **Business rules.**
  1. `HistProcCode` is required when Show In History is checked, and cleared when it is unchecked.
  2. `HistProcCode` must be an existing procedure code. Warn if it isn't inside the group's own code list.
  3. Perio Four Quadrants requires Show In History.
  4. Item order sets the row order in Insurance History.
  5. Deleting or hiding a group with Show In History warns that its history row disappears.
  6. Save refreshes the code-group cache, so open Insurance History windows reload.
- **Permissions / audit.** Setup. Log "Code group <name> edited" with the changed fields.
- **Preferences.** None.
- **Schema.** Same three codegroup columns as v26_1-family-inshist-codegroups.
- **Touchpoints.** `OpenDental/Forms/FormCodeGroupEdit.cs` and `FormCodeGroupEdit.Designer.cs` (current controls 27–38), `OpenDental/Forms/FormCodeGroups.cs`, `OpenDentBusiness/TableTypes/CodeGroup.cs`, `OpenDentBusiness/Data Interface/CodeGroups.cs`, `OpenDentBusiness/Crud/CodeGroupCrud.cs`.

### v26_1-setup-default-prov-unassigned-pref: Default provider for unassigned operatories moves to Preferences

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Setup: the "Default Prov for Unassigned" setting leaves Schedule Setup for Preferences, under a longer name that spells out "operatories"). Baseline: <https://www.opendental.com/manual/scheduleeditdaywindow.html> (24.3, Edit Day window, Add a Time Block panel). The previously cited `schedule.html` doesn't mention the setting.
- **User-visible behavior.** The setting is in Preferences instead of the schedule day editor. It does the same thing: it picks the provider whose open hours apply to operatories that have no assigned or scheduled provider. Per the 24.3 manual, if that provider already has a time block whose operatories are "not specified", that block's hours are used instead. The move doesn't change this.
- **UI.**
  - Remove the "Default Prov for Unassigned*" combo, and its footnote, from Schedule Day Edit.
  - Add a provider combo to Preferences > Appointments > General, labeled "Default provider for unassigned operatories".
- **Business rules.** It keeps using the existing `ScheduleProvUnassigned` preference, so no data changes. Consumers are unchanged. Saving schedules no longer writes the preference.
- **Permissions / audit.** Setup for Preferences. Previously the preference was saved by whoever could edit schedules (Schedules).
- **Preferences.** Existing `ScheduleProvUnassigned`; nothing new.
- **Schema.** None.
- **Touchpoints.**
  - `OpenDental/Forms/FormScheduleDayEdit.cs` (66, 651) and its designer (label3 at 264).
  - `OpenDental/User Controls/Preferences/UserControlApptGeneral.cs`.
  - Consumers, for testing: `OpenDental/Main Modules/ControlAppt.cs` (4963) and `OpenDentBusiness/Data Interface/Schedules.cs` (849).

### v26_1-setup-allergy-search: Allergy Setup: search bar

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:**
  - <https://www.opendental.com/site/version26_1.html> (Setup: a search bar in Allergy Setup).
  - <https://opendental.blog/software-update-stable-version-26-1-highlights/>.
  - Baseline: <https://www.opendental.com/manual/allergiesmaster.html> (24.3).
- **User-visible behavior.** Typing in a search box above the allergy list filters it as the user types.
- **UI.** `FormAllergySetup`: a "Search" text box above the grid, with focus on open.
- **Business rules.**
  1. A case-insensitive "contains" match on the description, combined with Show Hidden.
  2. Selection mode works on the filtered rows.
  3. Merge still needs exactly two selected rows, and they must be visible.
  4. After add, edit or merge, the filter stays and the grid refreshes.
- **Permissions / audit.** Unchanged (AllergyDefEdit, AllergyMerge).
- **Preferences.** None. **Schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormAllergySetup.cs` (`FillGrid` 30–52; merge 82–112) and its designer.

---

## Tools

### v26_1-tools-audit-insplan-drop: Audit Trail: PlanNum logged when a plan is dropped

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Tools: dropping a plan now records its PlanNum in the audit entry). <https://opendental.blog/software-update-stable-version-26-1-highlights/>.
- **User-visible behavior.** The audit entry for a dropped plan includes the plan's number, so two plans with the same carrier name can be told apart.
- **Business rules.**
  1. Change the log text to: "The insurance plan for the carrier <name> (PlanNum <n>) was dropped." The log already stores the PlanNum as the entry's foreign key; it is only missing from the text.
  2. Apply the same to every other code path that drops a patient plan. Grep `PatPlans.Delete`, for example the family-module paths and the X12 834 import, which logs InsPlanDropPat.
- **Permissions / audit.** InsPlanEdit entry (InsPlanDropPat for 834).
- **Preferences / schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormInsPlan.cs` (`butDrop_Click` 2064; `DropClickHelper` log at 2100–2102), other `PatPlans.Delete` callers, `OpenDentBusiness/Data Interface/PatPlans.cs`.

### v26_1-tools-audit-birthdate: Audit Trail: birthdate additions and changes

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Tools: birthdate edits now appear in the audit log). The blog says both adding and editing a birthdate are tracked.
- **User-visible behavior.** The Audit Trail shows a PatientEdit entry whenever a patient's birthdate is set or changed, with the old and new dates.
- **Business rules.**
  1. Log on save when the value changes, including from blank to a date and from a date to blank.
  2. Cover every save path:
     - Edit Patient Information;
     - Add Family Member and new patients;
     - sheet and eForm import of the birthdate field;
     - any other caller of `Patients.Update` that changes Birthdate.
  3. Implement it as a helper next to `InsertAddressChangeSecurityLogEntry`.
  4. Show dates in short-date format. The 24.3 masked-birthdate display (`textBirthdateMask`) is a UI mask only: log the real values, as address changes are logged today.
- **Permissions / audit.** PatientEdit entry.
- **Preferences / schema.** None.
- **Touchpoints.**
  - `OpenDentBusiness/Data Interface/Patients.cs` (`InsertAddressChangeSecurityLogEntry` 2646–2664 as the model; call sites 2638 and 2816; `SavePatientEdit` 5358).
  - `OpenDental/Forms/FormPatientEdit.cs`, `OpenDental/Main Modules/FormOpenDental.cs` (3121 pattern).
  - Sheet and eForm import code.

### v26_1-tools-dbm-duplicate-blockouts: DBM check SchedulesDeleteDuplicateBlockouts replaces the standalone tool

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Tools: a `SchedulesDeleteDuplicateBlockouts` check replaces the standalone Clear Duplicate Blockouts tool). Baseline: <https://www.opendental.com/manual/blockoutsduplicate.html> (24.3). That page reaches the tool through Tools > Misc Tools, says duplicates make the Appointments module slow, and says they usually come from combined operatories or data conversions. It doesn't define a duplicate; the code does (rule 1).
- **User-visible behavior.** Tools > Misc Tools > Clear Duplicate Blockouts is gone. Database Maintenance has a check named `SchedulesDeleteDuplicateBlockouts`: Check reports the number of duplicate blockouts, and Fix deletes them.
- **Business rules.**
  1. Reuse `Schedules.GetDuplicateBlockoutCount()` (1788) for Check and `Schedules.ClearDuplicates()` (1856) for Fix. Two blockouts are duplicates when they match on date, start time, stop time, blockout type, **note** and operatory. In each group, the link with the lowest `ScheduleOpNum` is kept; the others are removed.
     - Two defects in today's `ClearDuplicates` must be fixed before DBM can call it on every run. First, with no duplicates its `IN (...)` lists are empty, which is invalid SQL, so return early when the list is empty. Second, it writes `FROM SCHEDULE` in upper case, which fails on servers with case-sensitive table names.
     - Its last step deletes **every** blockout left with no operatory link. That includes any blockout that had none before the run. Report that count separately in the verbose output, or limit the delete to the schedules touched in this run (proposed).
  2. Follow the `[DbmMethodAttr]` pattern: a Middle Tier guard, verbose output and log text.
  3. The fix is replication-unsafe in the same way as other schedule fixes; mark it if needed.
- **Permissions / audit.** DBM rules (Setup), plus the existing DBM run log. Remove the Setup log "Clear duplicate blockouts." from the deleted form.
- **Preferences / schema.** None.
- **Touchpoints.**
  - `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` (add near `SchedulesDeleteShort` 9964–9996).
  - `OpenDentBusiness/Data Interface/Schedules.cs` (1788–1880).
  - `OpenDental/Main Modules/FormOpenDentalMenus.cs` (455) and `OpenDental/Main Modules/FormOpenDental.cs` (6368): remove the menu item.
  - Delete `OpenDental/Forms/FormBlockoutDuplicatesFix.cs` and its designer and resx.

### v26_1-tools-dbm-missing-allergydefs: DBM check: fix allergies with a missing allergy definition

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Tools: a DBM check that repairs allergies whose definition is gone).
- **User-visible behavior.** A DBM check finds patient allergies that point to an allergy definition that no longer exists, and fixes them.
- **Business rules** (modeled on the existing `DiseaseWithInvalidDiseaseDef`).
  1. Check: count the `allergy` rows whose `AllergyDefNum` is not in `allergydef`.
  2. Fix: find or create an allergy definition named "UNKNOWN ALLERGY" (not hidden), point the orphans at it, and report the count. Patients keep the allergy record, flagged for review.
  3. Proposed method name: `AllergyWithInvalidAllergyDef`.
- **Permissions / audit.** As for the other DBM methods.
- **Preferences / schema.** None.
- **Touchpoints.** `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` (model at 4138–4180; the related `AllergiesIndicateNone` check at 7851–7860), `OpenDentBusiness/Data Interface/AllergyDefs.cs`, `OpenDentBusiness/Data Interface/Allergies.cs`.

---

## Miscellaneous

### v26_1-misc-audit-chartviews: Audit Trail: Chart View changes

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Misc: edits to Chart Views now write audit entries). <https://opendental.blog/software-update-stable-version-26-1-highlights/>.
- **User-visible behavior.** Adding, editing, deleting or reordering a Chart View writes an Audit Trail entry.
- **Business rules.**
  1. Add: "Chart view <name> added."
  2. Edit: "Chart view <name> edited: <field> <old> to <new>; …" for each changed field, including the checked procedure statuses, date filter, orion settings and similar.
  3. Delete: "Chart view <name> deleted."
  4. Reorder (the up/down buttons in the Chart module): "Chart view <name> moved from position <a> to <b>."
- **Permissions / audit.** The entries go under ChartViewsEdit (permission 262), which already gates these actions.
- **Preferences / schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormChartView.cs` (delete 368; insert and update 440–443), `OpenDental/Main Modules/ControlChart.cs` (permission checks and reorder 255, 344, 470), `OpenDentBusiness/Data Interface/ChartViews.cs`.

### v26_1-misc-audit-eform-delete: Audit Trail: patient eForm deletions

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Misc: removing a patient's eForm now leaves an audit entry; the page says this also went into 25.4.79). <https://opendental.blog/software-update-stable-version-26-1-highlights/>. `eform.DateTimeSubmitted` comes from v25_4-sheets-datetimesubmitted; it is not in the 25.3 DDL.
- **User-visible behavior.** Deleting a patient's eForm writes an Audit Trail entry under eForm Delete, naming the form and its date.
- **Business rules.** After `EForms.Delete` succeeds, log:
  - the patient;
  - "eForm <description> dated <DateTimeShown> (ID <EFormNum>) deleted";
  - the submitted date-time, when the 25.4 `DateTimeSubmitted` is set.
  Only eForms attached to a patient are logged; deleting an unsaved new form isn't.
- **Permissions / audit.** EFormDelete (permission 260; already checked at `FrmEFormFillEdit` line 90).
- **Preferences / schema.** None.
- **Touchpoints.** `WpfControlsOD/Frms/FrmEFormFillEdit.xaml.cs` (`butDelete_Click` 101–110), `OpenDentBusiness/Data Interface/EForms.cs`.

### v26_1-misc-audit-billing-finance: Audit Trail: opening and running Billing/Finance Charges

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://www.opendental.com/site/version26_1.html> (Misc: both opening and running the Billing/Finance Charges tool now write audit entries). Baseline: <https://www.opendental.com/manual/financecharges.html> (24.3), which mentions no audit logging. It does say aging runs when the window opens, after a run and after an undo. This fork logs AgingRan only around the run on open (lines 107 and 127), and rule 3 keeps those entries.
- **User-visible behavior.** The Audit Trail shows when someone opened Tools > Billing/Finance Charges, and every run with its settings and result.
- **Business rules.**
  1. Today 24.3 writes a single Setup entry, "Run Finance Charges", when the window **closes**, whether or not anything ran. Replace it with:
     - on open: "Billing/Finance Charges window opened";
     - on Run: "<Finance|Billing> charges run: date <d>, <rate/amount settings>, <n> charges created totaling <amount>".
  2. The existing undo entry stays.
  3. The aging entries (AgingRan) stay.
- **Permissions / audit.** Setup, which already gates the tool.
- **Preferences / schema.** None.
- **Touchpoints.** `OpenDental/Main Modules/FormOpenDental.cs` (`menuItemFinanceCharge_Click` 6573–6580), `OpenDental/Forms/FormFinanceCharges.cs` (`butRun_Click` 280; existing logs 107, 127, 276).

### v26_1-misc-task-attach-nonmodal: Task Attachment windows are non-modal

- **Category / size:** desktop, S. **Depends on:** none.
- **Sources:** <https://opendental.blog/software-update-stable-version-26-1-highlights/>, which lists it among the minor 26.1 enhancements. The release-notes page doesn't list it.
- **User-visible behavior.** The Task Attachments and Task Attachment Edit windows can stay open while the user works in other Open Dental windows, for example to find the image or document to attach.
- **Business rules.**
  1. Open both windows with `Show()` instead of `ShowDialog()`, owned by their parent.
  2. Keep at most one Task Attachments window per task: re-activate it if it is already open.
  3. When the task is saved, deleted or closed, close or refresh its attachment windows.
  4. If the task is deleted by another user while a window is open, show a message on save and discard.
- **Permissions / audit.** TaskEdit, unchanged.
- **Preferences / schema.** None.
- **Touchpoints.** `OpenDental/Forms/FormTaskEdit.cs` (1057), `OpenDental/Forms/FormTaskAttachments.cs` (63, 100), `OpenDental/Forms/FormTaskAttachmentEdit.cs`, `OpenDental/User Controls/UserControlTasks.cs` (2406).

---

## Excluded

Each line gives the reason. "Reserve" means: add the enum value or column so rows written by a real Open Dental 26.1 database load cleanly, but build no feature on it.

| ID | Source | Category | Why excluded |
|---|---|---|---|
| v26_1-odtouch-25-app | <https://www.opendental.com/site/version26_1.html>, <https://www.opendental.com/site/odtouch.html> | excluded-od-service | ODTouch is an Open Dental eService tablet app. That includes its new interface, dark mode, pharmacies, patient editing and Ensora eRx support. All of it is Open Dental-hosted and requires a registration key. |
| v26_1-imaging-betterdiag-patient-view | <https://www.opendental.com/site/version26_1.html>, <https://www.opendental.com/site/betterdiagnosticsprocess.html>, <https://www.opendental.com/site/bridgebetterdiagnostics.html> | excluded-od-service | The BetterDiagnostics integration is signed up for, and billed, through Open Dental's eServices portal and relayed through the eConnector, which is the "through-HQ" path we cut. The current bridge page confirms both requirements: eServices Signup Portal enrollment and a running eConnector. Reserve `EnumImageAnnotVendor` 2 (BetterDiagnostics, added before 26.1) and 3 (BetterDiagnosticsPatientView, new in 26.1). This fork's enum stops at 1 (Pearl). If a vendor-direct BetterDiagnostics bridge is ever built, the public process page describes Patient View: solid simplified shapes, always-visible short captions, no tooth parts. |
| v26_1-setup-sheets-mobile-static-text | <https://www.opendental.com/site/version26_1.html>, <https://www.opendental.com/site/webforms.html> | excluded-od-service | The Sheets Mobile Layout is used only by Open Dental-hosted Web Forms and eClipboard. Reserve the `SheetFieldType.MobileStaticText` value; the desktop must skip such fields when rendering. |
| v26_1-setup-webform-setup-overhaul | <https://www.opendental.com/site/version26_1.html>, <https://www.opendental.com/site/webforms.html> | excluded-od-service | Web Forms are an Open Dental-hosted eService (the decoupling plan cuts it). |
| v26_1-tools-payment-portal-rename | <https://www.opendental.com/site/version26_1.html> | excluded-od-service | It renames the window for the Open Dental-hosted Payment Portal, which is cut. |
| v26_1-eservices-eclipboard-branding-logo | <https://www.opendental.com/site/version26_1.html>, <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> | excluded-od-service | eClipboard branding is cut. Still apply `branding.ValueString` text to `mediumtext` and reserve `EnumBrandingType.LogoImage`, for schema parity. |
| v26_1-eservices-econnector-firewall-alerts | <https://www.opendental.com/site/version26_1.html>, <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> | excluded-od-service | The eConnector exists only for Open Dental-hosted services. Reserve `AlertType` 51. |
| v26_1-eservices-websched-existing-default-prov | <https://www.opendental.com/site/version26_1.html> | excluded-od-service | Web Sched is an Open Dental-hosted eService. |
| v26_1-eservices-texting-entry-user | <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> | excluded-od-service | `smstomobile.SecUserNumEntry` belongs to Open Dental's hosted texting. Apply the column for parity only. Also reserve `eServiceCode` 57 (Chat service API) and 58 (AI transcription). Both are HQ-side service codes with no release-note feature. |
| v26_1-misc-topaz-cloud | <https://www.opendental.com/site/version26_1.html> | excluded-other | It makes Topaz pads work under Open Dental Cloud (Thinfinity/AppStream), which is cut. Local Topaz pads already work in this fork. |
| v26_1-api-deletedobj-table | <https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml> | excluded-other | The new `deletedobj` table stores snapshots of deleted schedules, lab cases, medication-patient rows, patient fields and tooth initials (`EnumDeletedObjType` 1–5), for the API's "Deleted" watch tables. The local API is cut. Create the table for parity; write nothing to it. It is a different table from the legacy `deletedobject` (see the schema section above). |
| v26_1-api-sub-schedule-deleted | <https://www.opendental.com/site/version26_1.html> | excluded-other | API subscription; the local REST API is cut. |
| v26_1-api-sub-medicationpat-deleted | <https://www.opendental.com/site/version26_1.html> | excluded-other | API subscription; the API is cut. |
| v26_1-api-sub-labcase-deleted | <https://www.opendental.com/site/version26_1.html> | excluded-other | API subscription; the API is cut. |
| v26_1-api-apptfielddefs-put | <https://www.opendental.com/site/version26_1.html> | excluded-other | API endpoint; the API is cut. |
| v26_1-api-sub-patfield-deleted | <https://www.opendental.com/site/version26_1.html> | excluded-other | API subscription; the API is cut. |
| v26_1-api-sub-toothinitials-deleted | <https://www.opendental.com/site/version26_1.html> | excluded-other | API subscription; the API is cut. |
| v26_1-api-payments-refund | <https://www.opendental.com/site/version26_1.html> | excluded-other | API endpoint; the API is cut. |
| v26_1-api-patients-hasins | <https://www.opendental.com/site/version26_1.html> | excluded-other | API endpoint; the API is cut. |
| v26_1-api-toothinitials-movement | <https://www.opendental.com/site/version26_1.html> | excluded-other | API endpoint; the API is cut. |
| v26_1-api-employers-address | <https://www.opendental.com/site/version26_1.html> | excluded-other | API endpoint; the API is cut. |
| v26_1-api-procedurelogs-snomedbodysite | <https://www.opendental.com/site/version26_1.html> | excluded-other | API endpoint; the API is cut. `procedurelog.SnomedBodySite` already exists, so there is no schema work. |

Other 26.1 enum additions with no feature: `EnumPermType` 276 `LogScraperHQ` (HQ-internal); the `EServiceTypes` values SecureEmailService, ODXam, EClipboard, OpenDentalLogScraper and OpenDental32Bit (HQ services; the document gives no numbers, so take them from a 26.1 database); `EnumDeletedObjType` 0–5 (for `deletedobj`); and `ProcStat` 0 `None` (a documentation placeholder). Add the C# values when the enums are synced. No UI.

**API scope caveat.** This spec excludes the API items because the task brief cuts the local REST API. `docs/decoupling-plan.md` still lists "the local REST API" under Open decisions, as an integration to keep. If that decision goes the other way, the 11 API items and the `deletedobj` writers come back into scope, as desktop items with no Open Dental dependency.

## Open questions to settle on a 26.1 test install

1. Real preference names for: the backdating preference, the PDF JavaScript preference, the Make Recall search preference, and the billing e-mail type.
2. Repeating-charge "backdating" semantics: is it the skip-past-dates behavior specced above, or a restriction on past start dates?
3. Service Date View on statements: the exact grid columns, and whether limited or super-family statements support it.
4. EDS "enhanced validation": which extra checks and which EDS endpoint 26.1 actually calls.
5. How Open Dental's own 26.1 conversion migrated the `InsHist*Codes` preferences into code groups (names, ordering, and flags on existing groups).
6. Whether the preauth reminder also fires for completed procedures, or for every plan rather than primary only.
7. Whether a future-dated clock-out is allowed on break events, and which permission 26.1 requires for it.
8. The labels of the four quadrant rows when a "Perio Four Quads" group is renamed, and whether more than one group may set that flag.
9. Where 26.1 got the basic Problem merge from (no 24.4–25.4 release note lists it), and how it repoints references.

## Fact-check log (2026-10-18)

An adversarial review of this file, done after the research pass. The changes above are already merged into the items.

- **Coverage.** All 57 bullets on the 26.1 release-notes page were re-read from the page itself. That is the four ODTouch sub-bullets plus 53 items; nothing was missing. The 26.2 page has exactly **two** backports to 26.1, not three: the DentalXChange PDF item (26.1.64) and future clock-outs (26.1.63). The intro now gives the correct breakdown of the 61 items. The category counts were re-counted and still hold: 36 desktop, 3 vendor-direct, 22 excluded.
- **Manual spot-checks** (24.3 public manual, quick manual and bridge pages):
  - `inshistory`, `procgroupnote`, `codegroups`, `repeatingcharges`, `edsattachmentservice`, `automation`, `adjustments`, `employees`, `scheduleeditdaywindow`, `allergiesmaster`, `billingdunningmsg`, `servicedateview`, `financecharges`, `blockoutsduplicate`;
  - `0_timecards`, `0_chat`, `canadaproccodetools`, `bridgepearlai`, `bridgebetterdiagnostics`, `dentalxchange`, `webforms`, `helpfeature`.
- **Corrections from the spot-checks.**
  - The baseline links `manual/automationcondition.html` (404) and `manual/schedule.html` (doesn't cover the setting) were replaced.
  - Group Note: added the SignatureCleared alert, the real save permissions and the locked-note rule.
  - Pearl: Log Level values now map to the existing `LogLevel` enum.
  - Duplicate blockouts: the definition now includes the note, plus two defects found in `ClearDuplicates`.
  - Adjustments: added the Negative Adjustments and Rigorous allocation rules.
  - Time cards: today's check compares dates only; added the break-event semantics.
  - Default provider: added the precedence caveat.
  - Gender: the Family module already has a Gender field.
  - Chat: per-chat mute and left-chat rows; the local-only requirement.
  - Age limits: the per-plan write-off override already exists in 24.3; it is not a 25.4 addition.
- **Classification.** No item changed category.
  - The BetterDiagnostics exclusion was re-confirmed: its bridge page requires the eServices Signup Portal and the eConnector.
  - Web Forms, the Sheets Mobile Layout, eClipboard, Web Sched, eConnector alerts, the Payment Portal, ODTouch and Topaz-on-Cloud are all Open Dental-hosted or cloud features.
  - EDS, DentalXChange and Pearl call the vendors directly in this fork (`EDS.cs` endpoint constants, `ClaimConnect.cs`, `PearlApiClient.cs`).
  - Chat stays desktop, since its history lives in the office database. Added the API-scope caveat from the decoupling plan.
- **Touchpoints.** Every path in this file was checked on disk, and about 60 line references were opened. One path was wrong in case only: `FormDunningEdit.designer.cs`. One call site was mis-described: the claim-receive prompt at `ClaimL` 317–320 sits in claim creation, not in an Account-module receive path.
- **Schema.**
  - Every 26.1 column was checked against `fox-db-schema.sql` (25.3.48). All are absent, except `benefit.ToothRange`, which is present. The 25.3 prerequisites the items rely on are present: the chat tables and their mute and read flags, `codegroup.ShowInFrequency`/`ShowInOther`, `statement.IsInvoice`/`IsReceipt`, `payplancharge.StatementNum` and `branding.ValueString text`.
  - Added the `deletedobj` vs `deletedobject` distinction.
  - The BetterDiagnostics row now gives the reason for reserving `EnumImageAnnotVendor` 2. The schema table names the reserved values.
  - The `SigIsTopaz2` type is marked as inferred.
- **Dependency IDs** were reconciled with `features-24.4.md` through `features-25.4.md`:
  - `v25_3-manage-chat` became `v25_3-chat-intraoffice-chat`;
  - `v25_2-family-inshist-edit-dates` became `v25_2-family-ins-history-edit-remove`;
  - `v25_1-setup-codegroup-showin-flags` became `v25_1-family-codegroups-hide-delete` plus `v25_3-family-otherbenefits-grid`;
  - added `v25_3-family-exclusions-tooth`, `v25_1-chart-group-note-flexible`, `v25_2-security-domain-login-change-user`, `v25_4-chart-groupnote-size`, `v25_4-sheets-datetimesubmitted` and `v25_4-family-writeoff-override-wording` where relevant.
- **Copied text.** An n-gram scan compared this file with every cited page and both schema documents. No passage reached 15 words. Several source notes repeated whole release-note lines, at 10 to 14 words each, and two blog phrases ran 8 to 9 words. All of these were reworded, so no page now contributes more than one short phrase. UI labels and preference names quoted as names, such as "Make Recall automatically searches for openings", were kept.
