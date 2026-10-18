# Open Dental 25.1: desktop feature specs for the 24.3-based fork

Status: draft spec, 2026-10-18; fact-checked the same day (see "Fact-check log" at the end). Baseline: this fork (Open Dental 24.3.63). Target: parity with Open Dental 25.1 (stable 2025-05-29) as one step toward adopting the office database (25.3.48).

Companion spec: [`schema-changes-24.3-to-26.1.md`](schema-changes-24.3-to-26.1.md) lists all DDL. This file covers behavior. Where the two overlap (25.1 columns and enums), this file adds the behavior and the migration rules.

## How this was researched

| Source | URL | Used for |
|---|---|---|
| 25.1 release notes (every item on the page) | https://www.opendental.com/site/version25_1.html | The item list. The page has **no per-item manual links**. |
| Official 25.1 highlights post | https://opendental.blog/version-25-1/ | Behavior details for blockouts, insurance history, pay plans, group notes, time card, fee tools, Message-to-Pay, ERA split, Canada. |
| 25.1 database documentation | https://www.opendental.com/OpenDentalDocumentation25-1.xml (diffed against [24-4](https://www.opendental.com/OpenDentalDocumentation24-4.xml)), plus [OpenDentalDiffDocumentation25-1.xml](https://www.opendental.com/OpenDentalDiffDocumentation25-1.xml) | Columns, enum values and column/table descriptions that changed in 25.1. Several behaviors below come only from these descriptions. |
| Public pages under `/site/` that reflect current behavior | [canadaattachments](https://www.opendental.com/site/canadaattachments.html), [canadaclaims](https://www.opendental.com/site/canadaclaims.html), [bridgeoverjet](https://www.opendental.com/site/bridgeoverjet.html), [betterdiagnostics](https://www.opendental.com/site/betterdiagnostics.html), [programlinks](https://www.opendental.com/site/programlinks.html) | Current UI for Canada attachments and the Overjet bridge. |
| 24.3 manual (public, our baseline) | https://www.opendental.com/manual243/ | What our fork does today, so each spec can say what changes. |
| 24.4 release notes | https://www.opendental.com/site/version24_4.html | Dependencies that 25.1 builds on (24.4 is between our base and 25.1). |

**Limitation.** Open Dental's manual for 24.4 and later is only reachable from inside the licensed program; the public site says so at https://www.opendental.com/site/helpfeature.html. Re-checked 2026-10-18: `https://www.opendental.com/manual/` serves the same files as `/manual243/` (the newest public manual is 24.3), `/manual244/`, `/manual251/` and `/manual253/` return 404, and `/help251/` and `/help253/` redirect to a login. Those pages were **not** accessed. So the "manual" pages cited below describe the 24.3 baseline, and only the `/site/` pages and the blog describe 25.x behavior. Where public sources do not settle a detail, the spec says so under "Open questions" and proposes a behavior. Nothing here comes from Open Dental binaries.

All text is paraphrased. Release-note wording is summarized, not quoted.

## Cross-cutting rules for implementers

1. **Get unknown names from the office database, not by guessing.** The office runs 25.3.48, so its data already holds Open Dental's real names and numbers for everything 25.1 added. Before finishing an item, run these against a copy of `fox-db`:
   - New preferences: `SELECT PrefName, ValueString FROM preference` and diff it against `PrefName` in `OpenDentBusiness/TableTypes/Pref.cs`. Items below that add a preference give a proposed name; use the real one if it differs.
   - Audit-trail types: `SELECT PermType, COUNT(*) FROM securitylog WHERE LogDateTime >= '2025-03-20' GROUP BY PermType`, then read sample `LogText` rows to match the wording and PermType Open Dental used.
2. **Enum numbers must match Open Dental's exactly.** Permission numbers, alert types and other enums are stored as integers. Item `v25_1-schema-enum-compat` lists every 25.1 value. 24.4 used permission numbers 264 and 265, so our enum must reserve them even before the 24.4 features are built.
3. **One renumbering in 25.1 changes stored data.** `guardian.Relationship` moved two values (item `v25_1-family-guardian-relationship-renumber`). The 25.1 diff document only shows the new `None` value; the move is visible only when the full 24.4 and 25.1 documents are compared.
4. **Conversion steps must be idempotent.** The same code has to upgrade a 24.3 database and must also leave the office's 25.3 database unchanged. Every `ALTER TABLE` checks whether the column exists (use `LargeTableHelper.ColumnExists`, defined in `OpenDentBusiness/Db/LargeTableHelper.cs`; `OpenDentBusiness/Misc/SecurityHash.cs` shows the calling pattern), and every data backfill runs only when the column was just added. Today's conversions live in `OpenDentBusiness/Misc/ConvertDatabases1.cs` to `ConvertDatabases8.cs`.
5. **Column conventions.** Like the rest of the schema: MyISAM, `utf8mb3`, `NOT NULL` with no explicit default (MySQL then uses `0` or `''`). Every 25.1 column below was checked in `D:/Dev/opendental-schema/fox-db-schema.sql` and matches this, including its position. 25.1 adds **no tables** and exactly **11 columns**, and all 11 appear in the measured diff (`D:/Dev/opendental-schema/schema-diff-24.3.63-vs-25.3.48.txt`). Of the other 36 diff columns, 7 belong to 24.4 (`document.ChartLetterStatus`, `document.UserNum`, `document.ChartLetterHash`, `fee.DateEffective`, `imagedraw.BetterDiagLayer`, `payplan.SheetDefNum`, `payplantemplate.SheetDefNum`), 28 to 25.2 or 25.3, and `patient._portal_active_email` is in none of the 24.4, 25.1 or 25.3 database documents (probably added outside Open Dental).
6. **Data classes.** After changing a `TableTypes` class, regenerate its CRUD file in `OpenDentBusiness/Crud/` with `xCrudGenerator`, then update `DocumentationBuilder` if we keep publishing schema docs.

Size scale: **S** up to 2 dev-days, **M** 3 to 7 days, **L** 1 to 3 weeks, **XL** more than 3 weeks.

## Summary

37 items to build (34 desktop, 3 vendor-direct) and 22 excluded. Items marked † come from the 25.1 database documentation and have no release-note line.

| ID | Title | Category | Size | Schema | Depends on |
|---|---|---|---|---|---|
| v25_1-account-canada-attachment-history | Canada claim attachment window with send history | desktop | M | none (uses `etrans`, `claimattach`) | none |
| v25_1-account-payplan-charge-logic-deprecated | Retire the "Pay Plan charge logic" choice | desktop | M | none | none |
| v25_1-account-payplan-unlock-permission | Pay Plan Unlock permission | desktop | S | enum 267 | v25_1-schema-enum-compat |
| v25_1-account-payconnect2-receipt-cardholder | PayConnect 2.0 receipts show cardholder | vendor-direct | S | `creditcard.CardHolderName` | PayConnect credentials without HQ |
| v25_1-account-payplan-completedamt-audit | Audit trail for Tx Completed Amount changes | desktop | S | none | none |
| v25_1-account-payconnect2-terminal-memory | PayConnect 2.0 remembers last terminal per workstation | vendor-direct | S | `computerpref.CreditCardTerminalId` | PayConnect credentials without HQ |
| v25_1-account-salestax-sort-bottom | Sales tax rows sort last within their date | desktop | S | none | 24.4 sales tax procedure code |
| v25_1-account-salestax-procs-on-claims | Sales tax procedures allowed on claims | desktop | S | none | 24.4 sales tax procedure code |
| v25_1-account-payplan-downpayment-flag † | Flag down-payment charges | desktop | S | `payplancharge.IsDownPayment` | none |
| v25_1-account-invoice-adjustment-total † | Invoices store their adjustment total | desktop | S | none (reuses `statement.InsEst`) | none |
| v25_1-appt-blockouts-flagged-permission | Separate permission for flagged blockouts | desktop | M | enum 266 | v25_1-schema-enum-compat |
| v25_1-appt-multiple-completed-tasks | Completed tasks no longer block a new appointment task | desktop | S | none | none |
| v25_1-family-codegroups-hide-delete | Hide and delete code groups | desktop | M | `codegroup.ShowInFrequency` | none |
| v25_1-family-inshist-two-dates | Two Insurance History dates per category | desktop | M | none | none |
| v25_1-family-guardian-relationship-renumber † | Guardian relationship values renumbered | desktop | S | data migration on `guardian.Relationship` | none |
| v25_1-imaging-canada-claim-response-titles | Clearer titles for saved Canada claim responses | desktop | S | none | none |
| v25_1-imaging-scanning-setup-details | Details button in Scanning Setup | desktop | S | none | none |
| v25_1-manage-timecard-totals-grid | Time card period totals as a grid | desktop | M | none | none |
| v25_1-manage-era-autosplit-carc | Auto-split ERA claims that carry denial CARCs | desktop | M | new preference | none |
| v25_1-chart-group-note-flexible | Group notes with any procedures, or none | desktop | M | none | none |
| v25_1-misc-claimproc-override-audit | Audit Ins Est and Allowed overrides | desktop | S | none | none |
| v25_1-setup-programlink-selectedprocnums | `[SelectedProcNums]` program-link field | desktop | S | none | none |
| v25_1-sheets-signature-change-user | Change the signing user on sheet signature boxes | desktop | M | none | none |
| v25_1-sheets-autosave-images | Automatic copy of saved sheets to Imaging | desktop | S | none | none |
| v25_1-eforms-prefill-from-guarantor | eForm fields prefill from the guarantor | desktop | M | `eformfield(def).PrefillFromGuar` | none |
| v25_1-eforms-auto-import-fields | eForm fields that import on submit | desktop | M | `eformfield(def).AutoImport` | none |
| v25_1-eforms-conditional-children-view | View an eForm parent field's conditional children | desktop | S | none | none |
| v25_1-misc-dbm-email-invalid-chars | DBM: invalid characters in email bodies | desktop | S | none | none |
| v25_1-misc-medication-hide | Hide medications in the master list | desktop | S | `medication.IsHidden` | none |
| v25_1-setup-overjet-bridge | Overjet program link | vendor-direct | S | `program` row | HQ program gating removed |
| v25_1-misc-wiki-draft-compare | Compare a wiki draft with the live page | desktop | S | none | none |
| v25_1-misc-dbm-securityhash-rehash-patient | DBM: rehash the whole patient table | desktop | S | none | in-repo SecurityHash |
| v25_1-security-insverify-securityhash † | Integrity hash on insurance verification rows | desktop | S | `insverify.SecurityHash`, `insverifyhist.SecurityHash` | in-repo SecurityHash |
| v25_1-setup-proccode-morefees-filters | Provider and clinic filters in More Fees | desktop | S | none | none |
| v25_1-setup-feetools-tabs | Fee Tools window reorganized into tabs | desktop | M | none | 24.4 fee effective dates |
| v25_1-canada-etrans-counters-uncapped † | Canadian transaction counters no longer cap out | desktop | S | none | none |
| v25_1-schema-enum-compat † | Reserve all 25.1 enum values | desktop | S | enum values only | 24.4 enum values (see item) |

Excluded (details at the end): eClipboard 25, five eServices items, the Kiosk Manager change, BetterDiagnostics, the API Service installer change, and 13 API items.

---

## Account module

### v25_1-account-canada-attachment-history: Canada claim attachment window with send history

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html (Account Module); https://opendental.blog/version-25-1/ (Canada section); https://www.opendental.com/site/canadaattachments.html (current public page); baseline https://www.opendental.com/manual243/canadaattachments.html.

**Behavior.** In 24.3, Canadian attachments sit in a plain list on a "Canadian" sub-tab of the Edit Claim window's Attachments tab, with no record of what was sent. In 25.1 they move to their own window, opened by right-clicking a claim in the Account module (or a predetermination in the Treatment Plan module) and choosing **Manage Canada Attachments**. The window lists every file attached to the claim and shows whether it has been sent, so the office can see the history and send only what is still outstanding.

**UI.**
- Context menu item **Manage Canada Attachments** on claim rows in `ControlAccount` and on preauth rows in `ControlTreat`. Shown only for Canadian databases.
- New window (proposed name `FormClaimAttachCanada`):
  - Grid columns: **Status** (sent, or waiting to send), **Attachment Name**, **File Size**.
  - Buttons: **Send Attachments**, **Add**, **Perio** (attaches the patient's latest perio exam), **Export**. A **Close** button is ours; the public page does not list one.
  - Row right-click: Open, Rename, Remove, as on the 24.3 Canadian sub-tab (manual243 page). Disabling Remove and Rename for sent rows is our proposal.
- **Add** opens the existing image picker (`FormImageSelectClaimAttach`): an Existing Images grid from the patient's Imaging module, plus Import, Snipping Tool and Paste Image.
- Remove the Canadian sub-tab from Edit Claim, or turn it into a button that opens the new window.

**Rules and edge cases.**
- **Send** with rows selected sends those rows. With nothing selected it sends every unsent row.
- Existing limits stay: at most 30 files, 7 MB total per send, and the accepted file types. The carrier must support the attachment transaction (`CanSupTransTypes.Attachment_09`). An image category flagged for claim attachments must exist.
- The normal sequence is unchanged: send the claim, receive an acknowledgment or EOB, then send attachments. That can happen right away or much later.
- Images picked from Existing Images are sent without drawings or AI annotations. Send the stored image, not a composite.
- **How "Sent" is known.** No 25.1 column records it. Derive it from the `etrans` rows of type `Attachment_CA`:
  - Today `CanadianOutput.SendAttachments` (near line 59) creates that etrans through `Etranss.CreateCanadianOutput`, which has no ClaimNum parameter and always sets `etrans.ClaimNum=0` (the `0` passed as its third argument is the network number). After the call, set `etrans.ClaimNum=claim.ClaimNum` before the etrans is saved, as `CanadianOutput` already does for reversals (near line 573).
  - Record the sent file names (the `ActualFileName` values) in the etrans message text or Note.
  - A row counts as sent when a successful `Attachment_CA` etrans for the claim lists its file.
  - Rows sent before this change show "Unknown" (or "Not sent") and can be re-sent.

**Permissions and audit.** No new permission. Sending writes the existing ClaimSend audit entry; add the file names to it. Adding or removing unsent files uses the same check that FormClaimEdit uses today.

**Preferences.** None new. Export still uses the existing claim attachment export path preference.

**Schema.** None.

**Touchpoints.**
- `OpenDental/Forms/FormClaimEdit.cs`: `tabCanadianAttachments` (line 203), `listCanadianAttachments`, `butCanadianAttachAdd_Click` (line 2623), `butCanadianAttachPerio_Click` (line 2637), `butSendCanadaAttachments_Click` (line 2822).
- `OpenDentBusiness/Eclaims/CanadianOutput.cs`: `SendAttachments`, which calls `Etranss.CreateCanadianOutput`.
- `OpenDentBusiness/Data Interface/Etranss.cs`, `OpenDentBusiness/Data Interface/ClaimAttaches.cs`, `OpenDental/Forms/FormImageSelectClaimAttach.cs`.
- Menus in `OpenDental/Main Modules/ControlAccount.cs` and `OpenDental/Main Modules/ControlTreat.cs`.

**Open questions.** Open Dental may track sent status some other way. No column was added in 25.1 to 25.3, so a derived status is safe for compatibility.

### v25_1-account-payplan-charge-logic-deprecated: Retire the "Pay Plan charge logic" choice

- **Category:** desktop. **Size:** M (S for the UI part; removing the legacy code paths is optional and would be L).
- **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html (Account Module); baseline https://www.opendental.com/manual243/paymentplanpatient.html and https://www.opendental.com/manual243/paymentplanpatientfaq.html; in-repo help text in `OpenDental/Resources/PrefInfos.xml` (entries `PayPlansVersion` and `PayPlanHideDueNow`).

**Behavior.** 24.3 lets an office choose how patient payment plans hit the ledger: Do Not Age (legacy), Age Credits and Debits (default), Age Credits Only, or No Charges to Account. This is the `PayPlansVersion` preference, shown as "Pay Plan charge logic". 25.1 deprecates the setting, so it is no longer something offices choose. Public sources give no more detail than the one release-note line.

**Proposed implementation.**
1. Remove the **Pay Plan charge logic** combo from Preferences, Account > Payments (`UserControlAccountPayments`) and from the Enterprise Account page.
2. Keep reading `PayPlansVersion`. Many code paths branch on it, so behavior does not change for existing data.
3. Show the "Hide Due Now" checkbox (`PayPlanHideDueNow`) whenever the value is Age Credits and Debits, as today.
4. If a database holds any other value, show a read-only line in Preferences naming the legacy mode, plus a link to this spec.

**Rules.** Do not convert data silently. Changing the mode rewrites how balances and aging come out.

**Permissions and audit.** None.

**Preferences.** `PayPlansVersion` stays, but has no UI.

**Schema.** None.

**Touchpoints.**
- `OpenDental/User Controls/Preferences/UserControlAccountPayments.cs` and its `.Designer.cs`: `comboPayPlansVersion`, `comboPayPlansVersion_SelectionChangeCommitted`.
- `OpenDental/Resources/PrefInfos.xml`.
- `OpenDentBusiness/TableTypes/PayPlan.cs`: enum `PayPlanVersions`.
- Readers of the value (leave them as they are): `OpenDentBusiness/Db Multi Table/AccountModules.cs`, `Ledgers.cs`, `PaymentEdit.cs`, `PayPlanEdit.cs`.

**Open questions.** Check the office database's value: `SELECT ValueString FROM preference WHERE PrefName='PayPlansVersion'`. If Open Dental's 25.1 conversion forced every office onto Age Credits and Debits, the office will show `2`. Then decide whether our upgrade path from 24.3 should do the same.

### v25_1-account-payplan-unlock-permission: Pay Plan Unlock permission

- **Category:** desktop. **Size:** S. **Depends on:** v25_1-schema-enum-compat.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Account Module); 25.1 DB docs (`grouppermission.PermType` value 267, `payplan.IsLocked` description); baseline https://www.opendental.com/manual243/paymentplandynamic.html.

**Behavior.** In 24.3, a dynamic payment plan's **Permanent Lock** checkbox can never be cleared once the plan is saved locked. In 25.1 the checkbox reads **Locked**, and users with the new **Pay Plan Unlock** permission can clear it, which unlocks the plan's terms and production for editing. Users without the permission see it checked and disabled, as before.

**UI.**
- `FormPayPlanDynamic`: rename `checkProductionLock` to "Locked".
- When the plan is saved and locked, enable the checkbox only if `Security.IsAuthorized(EnumPermType.PayPlanUnlock, true)`.
- Clearing it re-enables the Unlock button, the terms area, Change Guarantor and production editing, by reversing what `FillUiForSavedPayPlan` and `LockProduction` turn off.

**Rules.**
- Unlocking does not save by itself. The normal Save still needs PayPlanEdit, including its date-lock checks.
- If the preference `PayPlanRequireLockForAPR` is on and the plan has an APR, saving still requires Locked. So an unlocked APR plan must be locked again before it can be saved.

**Permissions and audit.**
- New `EnumPermType.PayPlanUnlock = 267`, in the security tree next to Pay Plan Edit.
- When a plan is unlocked and saved, write a log entry with PermType PayPlanUnlock, the PayPlanNum as FKey, and text such as "Payment plan unlocked".
- **Upgrade grant:** the blog says unlocking used to belong to Pay Plan Edit. On upgrade, give PayPlanUnlock to every user group that has PayPlanEdit, so existing users keep that ability. (In our 24.3 code nobody can clear the lock at all: `FillUiForSavedPayPlan` disables `checkProductionLock` for every saved locked plan. So the blog's "previously" probably describes 24.4, and skipping the grant would take nothing away from our users. Grant it anyway to match Open Dental.) Compare with what Open Dental's own conversion did: `SELECT UserGroupNum FROM grouppermission WHERE PermType=267` on the office database.

**Preferences.** None.

**Schema.** None (enum value only).

**Touchpoints.**
- `OpenDental/Forms/FormPayPlanDynamic.cs`: `FillUiForSavedPayPlan`, `butUnlock` handling, `butSignPrint_Click`.
- `OpenDental/Forms/FormPayPlanDynamic.Designer.cs`.
- `OpenDentBusiness/TableTypes/GroupPermission.cs`, `OpenDental/User Controls/UserControlSecurityTree.cs`, `OpenDentBusiness/Data Interface/GroupPermissions.cs`.
- The upgrade conversion.

### v25_1-account-payconnect2-receipt-cardholder: PayConnect 2.0 receipts show cardholder

- **Category:** vendor-direct. **Size:** S.
- **Depends on:** the decoupling work that gives PayConnect office-entered credentials, not keys fetched from HQ.
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`creditcard.CardHolderName`).

**Behavior.** Receipts printed for PayConnect 2.0 transactions include the cardholder's name. For stored cards, the name is saved on the card record when the card is added, so later charges (including recurring charges) can print it too.

**UI.**
- Receipt text gains a cardholder line.
- Edit Credit Card (`FormCreditCardEdit`) shows Card Holder Name read-only when it has a value.

**Rules.**
- Take the name from the PayConnect 2.0 response: from the transaction for one-time charges, and from the tokenize or add-card response for stored cards.
- Leave it empty when the processor does not return one. Other processors may never fill it.
- Never print the full card number. The masked number stays as it is.

**Vendor-direct note.** `OpenDentBusiness/WebBridges/PayConnect2.cs` already calls DentalXChange directly (`https://api.dentalxchange.com/payments`), but it gets its API keys from HQ through `WebServiceMainHQProxy` (around lines 31 to 48). Replace that with keys the office enters in the PayConnect program properties.

**Permissions and audit.** Unchanged.

**Preferences.** None.

**Schema.** `creditcard.CardHolderName varchar(255) NOT NULL`, added after `Nickname`; existing rows get `''`. Matches the 25.3 DDL.

**Touchpoints.**
- `OpenDental/Forms/FormPayConnect2.cs`: the `PayConnect.BuildReceiptString(... cardHolder: ...)` call near line 295.
- `OpenDentBusiness/WebBridges/PayConnect.cs` (`BuildReceiptString`), `OpenDentBusiness/WebBridges/PayConnect2.cs`, `OpenDental/Forms/FormPayConnect2iFrame.cs`, `OpenDental/Logic/PayConnectL.cs`.
- `OpenDentBusiness/TableTypes/CreditCard.cs`, `OpenDentBusiness/Data Interface/CreditCards.cs`, `OpenDental/Forms/FormCreditCardEdit.cs`.

### v25_1-account-payplan-completedamt-audit: Audit trail for Tx Completed Amount changes

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Account Module).

**Behavior.** 24.3 writes "Payment Plan completed amount changed." with no amount, and only when the plan window is saved. 25.1 logs every change to the Tx Completed Amount with the amounts:
- manual edits in the plan window;
- automatic recalculations, for example when attached production is completed, its fee changes, or an attached adjustment is added or edited.

**Rules.**
- Log only when the value really changes, comparing amounts rounded to cents.
- The text gives the old amount, the new amount and the difference, for example "Tx Completed Amt changed from 1,200.00 to 1,350.00 (+150.00)".
- For automatic changes, add the trigger when known, such as the procedure code or adjustment.
- When a batch process updates many plans, write one entry per plan.

**Permissions and audit.** PermType PayPlanEdit (as the blog says), the plan's PatNum, and the PayPlanNum as FKey. Logging only; no new authorization check.

**Preferences.** None.

**Schema.** None.

**Touchpoints.**
- `OpenDental/Logic/PayPlanL.cs`: the "completed amount changed" block near line 258.
- `OpenDentBusiness/Data Interface/PayPlans.cs`: `UpdateTreatmentCompletedAmt`, `UpdateTreatmentCompletedAmtsDynamicPaymentPlan`, and the ins-plan close path near line 811.
- `OpenDentBusiness/Db Multi Table/PayPlanEdit.cs`: near line 2036, `GetDynamicPayPlanCompletedAmount`.
- `OpenDental/Forms/FormPayPlan.cs`, `OpenDental/Forms/FormPayPlanDynamic.cs`, `OpenDentBusiness/Data Interface/SecurityLogs.cs`.

### v25_1-account-payconnect2-terminal-memory: PayConnect 2.0 remembers the last terminal per workstation

- **Category:** vendor-direct. **Size:** S.
- **Depends on:** PayConnect credentials without HQ (see the receipt item above).
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`computerpref.CreditCardTerminalId`).

**Behavior.** When a terminal payment is started in the PayConnect 2.0 window, the Terminal dropdown preselects the terminal this workstation used last, instead of always the first one.

**Rules.**
- Store the terminal's `TerminalID` in this computer's `computerpref` row after a terminal transaction is submitted.
- On load, select the matching `PayTerminal` for the current clinic. If it is not in the list (wrong clinic, or the terminal was deleted), fall back to the first entry, as today.

**Permissions and audit.** None.

**Preferences.** None (this is a workstation setting).

**Schema.** `computerpref.CreditCardTerminalId varchar(255) NOT NULL`, added after `VideoRectangle`; default `''`. Matches the 25.3 DDL.

**Touchpoints.**
- `OpenDental/Forms/FormPayConnect2.cs`: the `comboTerminal` fill near lines 60 to 70, and the terminal submit near line 161.
- `OpenDentBusiness/TableTypes/ComputerPref.cs`, `OpenDentBusiness/Data Interface/ComputerPrefs.cs`, `OpenDentBusiness/Data Interface/PayTerminals.cs`.

### v25_1-account-salestax-sort-bottom: Sales tax rows sort last within their date

- **Category:** desktop. **Size:** S.
- **Depends on:** the 24.4 feature "Quickly add procedure code for Sales Tax", which posts tax as a single procedure.
- **Sources:** https://www.opendental.com/site/version25_1.html (also backported to 24.4.52); https://www.opendental.com/site/version24_4.html; baseline https://www.opendental.com/manual243/salestax.html.

**Behavior.** In the Account module's patient ledger, sales tax lines for a date of service appear after every other line for that date, so the tax follows the work it applies to.

**Rules.**
- A row counts as sales tax when either:
  - it is a procedure whose code is the office's sales tax procedure code (24.4 preference); or
  - it is an adjustment whose type equals `SalesTaxAdjustmentType`.
- Within the same date, sales tax rows go after all others and keep their order relative to each other. Ordering across dates does not change.
- Apply the same rule to statements that use the account ledger sort.

**Permissions, preferences, schema.** None.

**Touchpoints.**
- `OpenDentBusiness/Db Multi Table/AccountModules.cs`: `AccountLineComparer` (near line 3726), used at line 403 and at line 2296.
- `OpenDental/Main Modules/ControlAccount.cs`.

### v25_1-account-salestax-procs-on-claims: Sales tax procedures allowed on claims

- **Category:** desktop. **Size:** S.
- **Depends on:** the 24.4 sales tax procedure code (as above).
- **Sources:** https://www.opendental.com/site/version25_1.html; https://www.opendental.com/site/version24_4.html.

**Behavior.** Procedures that carry the sales tax code (in the US usually D9985) can be attached to insurance claims like any other procedure. Before 25.1 they were left off.

**Rules.**
- Include them in the claim when the user selects them, or when Create Claim picks up all eligible procedures.
- Normal insurance rules apply. If the office does not want to bill tax to insurance, they mark the code "Do not usually bill to insurance", and the existing NoBillIns exclusion and message in `ClaimL` handle it.
- The 7-procedure limit for Canadian claims still applies.

**Permissions, preferences, schema.** None.

**Touchpoints.**
- `OpenDental/Logic/ClaimL.cs`: procedure selection and NoBillIns filtering near lines 448 to 465.
- `OpenDentBusiness/Data Interface/Procedures.cs` (`NoBillIns`).
- `OpenDentBusiness/X12/X837_5010.cs`, for claim-line output.

**Open questions.** The fork does not have the 24.4 sales tax procedure feature yet. When it is built, make sure it does not add its own claim exclusion.

### v25_1-account-payplan-downpayment-flag †: Flag down-payment charges

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** 25.1 DB docs, `payplancharge.IsDownPayment` (https://www.opendental.com/OpenDentalDocumentation25-1.xml). No release-note line.

**Behavior.** Payment-plan charges that make up the down payment carry an explicit flag. One plan can have several flagged charges, because a down payment is split across the production it is attached to.

**Rules.**
- Set `IsDownPayment=1` on the debit charges created from `terms.DownPayment`, including every split piece.
- Everything that now detects down payments by looking for "downpayment" or "down payment" in the charge note (`PayPlanEdit.CreateRowForPayPlanListDT`, near line 272) should read the flag instead.
- **Backfill on upgrade from 24.3:** set the flag where the note matches that same text. The note is written through `Lans.g("PayPlanEdit","Down payment")` (near line 544), so also match the office's translated text if a language other than English is in use. Never backfill a database that already had the column.

**Permissions, preferences.** None.

**Schema.** `payplancharge.IsDownPayment tinyint(4) NOT NULL`, after `IsOffset`; default 0. Matches the 25.3 DDL.

**Touchpoints.**
- `OpenDentBusiness/Db Multi Table/PayPlanEdit.cs`: down-payment charge creation near lines 538 to 545 and 958 to 1035, and note matching near line 272.
- `OpenDentBusiness/TableTypes/PayPlanCharge.cs`, `OpenDentBusiness/Data Interface/PayPlanCharges.cs`.

### v25_1-account-invoice-adjustment-total †: Invoices store their adjustment total

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** 25.1 DB docs, `statement.InsEst` description (https://www.opendental.com/OpenDentalDocumentation25-1.xml). No release-note line.

**Behavior.** The 25.1 description of `statement.InsEst` says that for invoices the column holds the total adjustment amount, not an insurance estimate. Read it that way so office invoices display correctly.

**Rules.**
- When saving a statement with `IsInvoice=1`, store the sum of the adjustments included on the invoice in `InsEst`.
- Invoice sheet output that shows an adjustment total reads it from there.
- Regular statements keep the family insurance estimate.

**Schema.** None (meaning change only).

**Touchpoints.** `OpenDental/Forms/FormStatementOptions.cs` (InsEst assignments near lines 498, 614 and 794), `OpenDental/Main Modules/ControlAccount.cs` (near lines 1429 and 1794), `OpenDentBusiness/Data Interface/Statements.cs`, `OpenDental/Forms/FormInvoiceItemSelect.cs`.

**Open questions.** Confirm on office data: `SELECT StatementNum, InsEst FROM statement WHERE IsInvoice=1 ORDER BY DateSent DESC LIMIT 20`, and compare with the invoice's adjustments.

---

## Appointments module

### v25_1-appt-blockouts-flagged-permission: Separate permission for flagged blockouts

- **Category:** desktop. **Size:** M. **Depends on:** v25_1-schema-enum-compat.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Appointments Module); 25.1 DB docs (`EnumPermType` 266); baseline https://www.opendental.com/manual243/blockouts.html and https://www.opendental.com/manual243/permissions.html.

**Behavior.** Blockout types can be flagged "NS" (no appointment scheduling) or "DC" (disable cut/copy/paste).
- **24.3:** the Blockouts permission covers unflagged blockouts, and anyone, even without permission, may add, edit and delete flagged ones.
- **25.1:** a new permission, **Blockouts flagged as NoSched or DisableCopyPaste** (`BlockoutsFlagged`), controls adding, editing, cutting, copying, pasting, clearing and deleting flagged blockouts. Blockouts keeps covering unflagged ones. A user needs both to manage every blockout.

**UI.**
- Appointments module blockout context menu (`ControlAppt`):
  - Enable each item according to the flags of the blockout under the mouse, or of the types offered.
  - Add Blockout lists only the types the user may create: unflagged types need Blockouts, flagged types need BlockoutsFlagged.
  - Clear-for-day/op/clinic removes only the kinds the user is allowed to remove. If that skips any, say so.
- `FormScheduleBlockEdit`: the type dropdown and the Delete button follow the same rule.
- `FormBlockoutCutCopyPaste`: flagged DC types are already excluded; NS-flagged blockouts need BlockoutsFlagged.

**Rules.** A blockout with any flag needs BlockoutsFlagged. With no flag it needs Blockouts. Changing a blockout's type from one kind to the other needs both.

**Permissions and audit.**
- New `EnumPermType.BlockoutsFlagged = 266` in the security tree under Appointments, next to Blockouts.
- Audit entries for flagged blockouts (add, edit, delete, cut, copy, paste, clear) use PermType BlockoutsFlagged. Unflagged ones keep Blockouts.
- **Upgrade grant:** in 24.3 every user could manage flagged blockouts (confirmed by the 24.3 Blockouts and Permissions manual pages), so grant BlockoutsFlagged to every user group on upgrade. Nobody loses access. Compare with the office database: `SELECT UserGroupNum FROM grouppermission WHERE PermType=266`.

**Preferences.** None.

**Schema.** None (enum value only).

**Touchpoints.**
- `OpenDental/Main Modules/ControlAppt.cs`: permission checks near lines 312 to 400 and 2328 to 2474, including `menuBlockAdd_Click`.
- `OpenDental/Forms/FormScheduleBlockEdit.cs`, `OpenDental/Forms/FormBlockoutCutCopyPaste.cs`.
- `OpenDentBusiness/Data Interface/Schedules.cs`: `BlockoutLogHelper` (line 261), with its `SecurityLogs.MakeLogEntry(EnumPermType.Blockouts, ...)` calls at lines 252 and 326. Callers are in `ControlAppt.cs` (near lines 2320 to 2421), `FormScheduleBlockEdit.cs` (near lines 87 to 138) and `FormBlockoutCutCopyPaste.cs` (near lines 49 to 56).
- `OpenDental/User Controls/UserControlSecurityTree.cs` (near line 299), `OpenDentBusiness/TableTypes/GroupPermission.cs`.

### v25_1-appt-multiple-completed-tasks: Completed tasks no longer block a new appointment task

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; baseline https://www.opendental.com/manual243/tasksetup.html.

**Behavior.** When the task preference that allows multiple tasks per appointment (`TasksForApptAllowMultiple`) is off, 24.3 allows exactly one task per appointment, open or done. In 25.1 the one-task limit counts only tasks that are not complete. An appointment can therefore collect any number of completed tasks plus at most one open task.

**UI.** Edit Appointment's **Task** button (`FormApptEdit.butTask_Click`):
- With exactly one open task, open it.
- With none open, start a new task even if completed ones exist.
- If the user needs the completed ones, show them in `FormTasksForAppt`.

**Rules.**
- "Complete" means `task.TaskStatus == Done`.
- Apply the same counting in every other place that enforces the limit:
  - attaching an appointment to a task from Task Edit (`FormApptsOther` with `IsForTask`);
  - paste and move in the task tree (`UserControlTasks`);
  - the Send to Multiple option in `FormTaskListSelect`.
- Reopening a completed task when another open task exists on the same appointment: allow it (the limit applies when creating or attaching), or warn. Proposed: warn but allow.

**Permissions, preferences, schema.** None. The existing preference `TasksForApptAllowMultiple` stays as is.

**Touchpoints.**
- `OpenDental/Forms/FormApptEdit.cs` (near lines 1355 to 1395), `OpenDental/Forms/FormApptsOther.cs` (near line 785), `OpenDental/Forms/FormTaskListSelect.cs` (near line 36).
- `OpenDental/User Controls/UserControlTasks.cs` (near lines 2005 and 2250), `OpenDental/Forms/FormTasksForAppt.cs`.
- `OpenDentBusiness/Data Interface/Tasks.cs` (`GetMany(long AptNum)`, line 495; add a variant that excludes Done), `OpenDentBusiness/Data Interface/Appointments.cs` (`GetApptNumsAttachedToTask`, near line 3065, used by `FormApptsOther`; it counts every task whatever its status, so add a `TaskStatus` filter).

**Note.** The 24.3 Task Setup manual page describes "Allow multiple Tasks attached to one Appointment" backwards (it says Enabled allows only one). The code is the reference: when `TasksForApptAllowMultiple` is false, one task per appointment.

---

## Family module

### v25_1-family-codegroups-hide-delete: Hide and delete code groups

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`codegroup.ShowInFrequency`, new meaning of `codegroup.IsHidden`); baseline https://www.opendental.com/manual243/codegroups.html.

**Behavior.**
- **24.3:** "hidden" means "not in the Frequency grid" (`IsHidden`), and code groups cannot be deleted.
- **25.1** separates the two ideas:
  - **Show in Frequencies** gets its own column (`ShowInFrequency`).
  - **Hidden** (`IsHidden`) now means the group is retired. It disappears from the Edit Benefit code-group dropdown and from every grid in the Insurance Benefits window.
  - A code group can be **deleted**.

**UI.**
- Code Groups (`FormCodeGroups`):
  - Grid columns today: Group Name, Fixed (the `CodeGroupFixed` description), Proc Codes, Freq (shows X when `IsHidden` is false), Age. Freq now reads `ShowInFrequency`; add a new **Hidden** column.
  - The **Show Hidden** checkbox today shows groups that are in neither the Frequency nor the Age grid (`IsHidden && !ShowInAgeLimit`). It now means "include groups with IsHidden=1". `UpdateItemOrder` (which orders visible groups before hidden ones) changes the same way.
- Code Group Edit (`FormCodeGroupEdit`):
  - The **Show In Frequencies** checkbox maps to `ShowInFrequency`, no longer to `!IsHidden`.
  - New **Hidden** checkbox.
  - New **Delete** button for existing groups.

**Rules.**
- **Delete:**
  - Blocked while any `benefit` row references the group (`benefit.CodeGroupNum`). Say how many plans use it and suggest hiding instead.
  - Blocked for groups with a Fixed Group (`CodeGroupFixed`) that a static text field such as dateLastBW depends on. Proposed: allow, with a warning.
  - After delete, renumber `ItemOrder`.
- **Hidden groups:**
  - They stay valid on existing benefits. Benefit calculations keep using them.
  - They are not offered for new benefits.
  - The Insurance Benefits window does not show rows for them.
- `CodeGroups.IsHidden(...)` in `Benefits.cs` (near line 2006) currently means "not a frequency row". Callers that mean frequency visibility must switch to `ShowInFrequency`.
- After any change, send the existing `InvalidType.CodeGroups` signal.

**Permissions and audit.** Setup permission, as today. Log deletes with the group name.

**Preferences.** None.

**Schema and migration.**
- `codegroup.ShowInFrequency tinyint(4) NOT NULL`, after `ShowInAgeLimit`. Matches the 25.3 DDL. (`ShowInOther` follows it; it is not in the 25.2 documentation, so it arrives in 25.3 and is out of scope here.)
- Upgrade from 24.3, only when the column was just added: `UPDATE codegroup SET ShowInFrequency = (IsHidden=0), IsHidden = 0`. This keeps today's screens unchanged, because no group was "retired" before 25.1.

**Touchpoints.**
- `OpenDental/Forms/FormCodeGroups.cs` (lines 43 to 75), `OpenDental/Forms/FormCodeGroupEdit.cs` (lines 25 to 26 and 76 to 77).
- `OpenDental/Forms/FormInsBenefits.cs` (Age Limits grid near line 499, which filters on `ShowInAgeLimit`; Frequency grid near line 566, which filters on `IsHidden`), `OpenDental/Forms/FormBenefitFrequencies.cs` (near line 56), `OpenDental/Forms/FormBenefitEdit.cs`.
- `OpenDentBusiness/Data Interface/CodeGroups.cs` (lines 115 to 138), `OpenDentBusiness/Data Interface/Benefits.cs` (near line 2006), `OpenDentBusiness/TableTypes/CodeGroup.cs`.
- `UnitTestsCore/TableTypes/CodeGroupT.cs`.

**Open questions.** Confirm the backfill on the office database: rows with `ShowInFrequency=0 AND IsHidden=0` should exist where 24.x had `IsHidden=1`.

### v25_1-family-inshist-two-dates: Two Insurance History dates per category

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Family Module); baseline https://www.opendental.com/manual243/inshistory.html.

**Behavior.** The Insurance History window (Family module insurance grid, or Hist in an insurance plan) holds two dates per category instead of one. Example: a patient had two prophies elsewhere this year, and frequency limits must count both.
- Each date entered creates an Existing Other (EO) procedure with the category's default code, plus an InsHist claimproc for the subscriber's plan, as one date does today.
- Changing the date on such an EO procedure in the Chart updates the history.

**UI.**
- `FormInsHistSetup`: every category row (Exam, Prophy, BW, FMX/Pano, the four perio quadrants, Perio Maint, Debridement) gets a second date box. Columns: **Most Recent** and **Previous**.
- The Family module insurance history grid shows both dates, or the most recent with the previous one in a tooltip.

**Rules.**
- Collect all EO and C procedures for the category's codes that have InsHist or Received claimprocs on this InsSub, and sort by date descending. The first two fill the two boxes.
- Saving a box:
  - If it was empty, create a new EO and InsHist pair.
  - If it held an EO this window created, change that procedure's date.
  - If it showed a Completed procedure, never edit it; a newer date creates a new EO, the same rule as 24.3 applies to a single date.
  - Clearing a box deletes only an EO procedure.
- The two dates may not be equal, and the Previous date must be older than the Most Recent. Re-sort on save.
- 271 eligibility import (`EB271.cs`) keeps updating only the most recent date.
- Recall due dates keep updating from the most recent date (existing behavior for prophy and similar).

**Permissions and audit.** As 24.3: creating or deleting EO procedures is logged like the current Insurance History save.

**Preferences.** The existing `InsHist*Codes` preferences. In 24.3 the first code in each is the default code (`ProcedureCodes.GetByInsHistPref`). The 25.1 blog names a "Default Code" that the Insurance History preference holds, so 25.1 may show the default explicitly in Preferences; keep the first-code rule unless the office `preference` rows show a separate setting.

**Schema.** None.

**Touchpoints.**
- `OpenDental/Forms/FormInsHistSetup.cs` and its designer.
- `OpenDental/Main Modules/ControlFamily.cs` (near line 2588, the history grid; line 386 opens the window).
- `OpenDentBusiness/Data Interface/Procedures.cs`: `GetMostRecentInsHistProc` (line 5828; add a variant that returns the two most recent) and `InsertOrUpdateInsHistProcedure` (line 5859).
- `OpenDentBusiness/X12/EB271.cs` (near line 882), `OpenDentBusiness/Data Interface/Prefs.cs` (`GetInsHistPrefNames`).

### v25_1-family-guardian-relationship-renumber †: Guardian relationship values renumbered

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** full DB docs 24.4 vs 25.1, `guardian.Relationship`: https://www.opendental.com/OpenDentalDocumentation24-4.xml and https://www.opendental.com/OpenDentalDocumentation25-1.xml.

**Behavior.** 25.1 adds `None = 0` to `GuardianRelationship` for eForm imports. To free 0 it moved two values:

| Name | 24.3 / 24.4 | 25.1 and later |
|---|---|---|
| None | (none) | 0 (never stored in `guardian`; eForm use only) |
| Father | 0 | 6 |
| Sitter | 6 | 23 |

All other values keep their numbers.

**Rules and migration.**
- Change the enum in `OpenDentBusiness/TableTypes/Guardian.cs` to the 25.1 numbering.
- On upgrade from a 24.x database, in this order:
  1. `UPDATE guardian SET Relationship=23 WHERE Relationship=6`
  2. `UPDATE guardian SET Relationship=6 WHERE Relationship=0`
- Never run the update on a database already at 25.1 or later, such as the office database. Use a schema-version marker, or run it in the same conversion step that adds the 25.1 columns.
- UI pickers must not offer None for real guardian rows.

**Permissions, preferences.** None.

**Touchpoints.**
- `OpenDentBusiness/TableTypes/Guardian.cs` (enum at line 35), and any guardian edit form (search for `GuardianRelationship`).
- Code that maps relationships by name, which needs no change once the enum is right but should be retested: `OpenDentBusiness/Data Interface/Guardians.cs` (abbreviations near lines 76 to 93), `OpenDentBusiness/Misc/TsiMsgConstructor.cs` (near lines 85 to 103), `OpenDentBusiness/HL7old/EhrVXU.cs` (near lines 124 to 175; the 25.1 description says Sitter maps to caregiver in EHR, which this file already handles), and `OpenDentBusiness/Data Interface/Patients.cs` (near line 5591, which creates Father links).
- eForm guardian fields: our 24.3 `OpenDentBusiness/EForms/EFormImport.cs` has **no** guardian handling at all. The 25.1 description only says `None` may be stored in an eForm field for import, which implies an eForm guardian field that no release note announces. Treat that as a separate, later item; this item only needs the enum and the data migration.

---

## Imaging module

### v25_1-imaging-canada-claim-response-titles: Clearer titles for saved Canada claim responses

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Canada section); https://www.opendental.com/site/canadaclaims.html (automatic saving of claim responses).

**Behavior.** When a Canadian claim response is saved as a PDF to the Imaging module (an image category marked Claim Responses), the document title names the response type and whether it was for the primary or secondary carrier. Examples: "Claim Rejection Pri", "Claim Ack Sec", "Claim EOB Pri".

**Rules.**
- Title = response kind + "Pri" or "Sec".
- Response kinds:
  - Claim Rejection, Claim Ack, Claim EOB;
  - Predetermination Ack, Predetermination EOB;
  - Employer Certified;
  - the reversal, outstanding-transactions and reconciliation forms that are printed and saved.
- Pri/Sec comes from which carrier the response belongs to: `etrans.CarrierNum` versus the claim's primary and secondary carriers.
- When it cannot be told, leave the suffix off.
- Translate the text with `Lan.g`.

**Permissions, preferences, schema.** None.

**Touchpoints.** `OpenDental/Forms/FormCCDPrint.cs`:
- the `_docDescript` assignments in the print switch (near lines 507 to 584);
- `_claimResponseDefNum` and the `ImageStore.Import` call near line 633, then `doc.Description`.

### v25_1-imaging-scanning-setup-details: Details button in Scanning Setup

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`computerpref.ScanDocSelectSource` description).

**Behavior.** The Scanning Setup window (Setup, Imaging) gets a **Details** button next to **Show Select Scanner Window**. It opens a short explanation of when to turn the option on. The 25.1 column description gives the reason: turning it on avoids two known problems after scanning with some Canon scanners:
- the Imaging module fails to load some PDFs;
- list boxes in other windows, such as Commlog, stop repainting their selection.

**UI.** A **Details** button beside `checkScanDocSelectSource` that opens a message box or small dialog with that explanation, in our own words.

**Permissions, preferences, schema.** None (`computerpref.ScanDocSelectSource` already exists).

**Touchpoints.** `OpenDental/Forms/FormImagingSetup.cs` and `OpenDental/Forms/FormImagingSetup.designer.cs` (form title "Scanning Setup"; `checkScanDocSelectSource`).

**Note.** The decoupling plan replaces EZTwain scanning with NTwain or WIA. Recheck after that rewrite whether the Canon problems still happen; keep the button either way.

---

## Manage module

### v25_1-manage-timecard-totals-grid: Time card period totals as a grid

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Manage Module); baseline https://www.opendental.com/manual243/timeclockedit.html and https://www.opendental.com/manual243/timecardsetup.html.

**Behavior.** 24.3 shows the pay-period totals as labeled text boxes under the clock events, with each total in both colon (h:mm) and decimal form (confirmed by the 24.3 Time Card manual page and by `FormTimeCard.cs`, which fills both boxes regardless of any preference). 25.1 replaces them with a horizontal grid under the clock-event grid:
- It shows the period total for each numeric column of the time card, lined up under that column.
- Every value uses one format only. Per the blog, the user picks either "Decimal Format" or "Colon Format" (no longer both side by side), so the choice lives in the Time Card window itself.

**UI.**
- `FormTimeCard`: remove `textTotal`, `textTotal2` and the per-category boxes (Regular Time, Overtime, Rate2, Rate3, PTO, Protected Leave).
- Add a one-row grid named "Totals for Period" with columns aligned to the main grid: Total, Adjust, Rate2, Rate3, PTO, OT, PL, Day (the Regular Time total).
- Add a **Decimal Format** / **Colon Format** choice (two radio buttons) next to the totals grid.
- Show the same totals on the Breaks view.
- Printing (the Print button) prints the totals row in the format currently chosen.

**Rules.**
- The format choice starts from `TimeCardsUseDecimalInsteadOfColon` each time the window opens. Switching it changes only this window's display and does not change the preference (proposed; public sources do not say whether Open Dental remembers the choice).
- Colon format shows seconds only when `TimeCardShowSeconds` is on.
- Totals keep the 24.3 math: only completed clock events count, seconds are counted and rounded only for display, OT is the sum of overtime adjustments, and PTO and Protected Leave come from adjustments.

**Permissions, preferences, schema.** No new permission, preference or schema. Existing preferences are used.

**Touchpoints.**
- `OpenDental/Forms/FormTimeCard.cs`: the totals are filled near lines 626 to 672 (`textTotal`/`textTotal2`, `textOvertime`/`2`, `textRateTwo`/`2`, `textRateThree`/`2`, `textPTO`/`2`, `textUnpaidProtectedLeave`/`2`; the first of each pair is h:mm, the second decimal) and printed near lines 969 to 977. (Lines 363 to 377 only format the In and Out cells with `TimeCardShowSeconds`.)
- `OpenDental/Forms/FormTimeCard.designer.cs` (the total labels and boxes).
- `OpenDentBusiness/Data Interface/ClockEvents.cs` (`Format`, near line 514, already switches between colon and decimal by the preference; give it a variant that takes the format as a parameter).
- `OpenDental/Forms/FormTimeCardSetup.cs` (`checkUseDecimal`, the preference's UI), `OpenDentBusiness/Data Interface/TimeCardRules.cs`.

### v25_1-manage-era-autosplit-carc: Auto-split ERA claims that carry denial CARCs

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Other Changes); baseline https://www.opendental.com/manual243/eraprocess.html and https://www.opendental.com/manual243/claimsera835.html.

**Behavior.** 24.3 already has the preference **Don't auto-process claims w/ these CARCs** (`EraNoAutoProcessCarcCodes`). When an ERA claim carries one of those Claim Adjustment Reason Codes, auto-processing skips the whole claim, and the user must split it by hand ("Split Claim" in Verify and Enter Payment).

25.1 adds a checkbox under it, **Auto split ERAs with CARC denial codes listed above**. When it is on:
- denied procedures are split off into a new claim automatically;
- the paid procedures are then processed;
- the denied ones wait on the new claim to be fixed and resubmitted.

**UI.**
- Preferences, Manage, ERA group (`UserControlManageGeneral`): a checkbox under `textEraNoAutoProcessCarcCodes`. Disable it when that text box is empty.
- The ERA window's Note column and the auto-process summary message say which claims were split, and the new ClaimNum.

**Rules.**
- During auto-processing (and in manual Auto Process), for each ERA claim:
  - CARCs from the list on the claim-level adjustments: no split; skip the claim, as 24.3 does.
  - CARCs from the list only on some procedure lines: move those procedures' claimprocs to a new claim with `Claims.InsertSplitClaim`. Leave the new claim unsent (status U) so it can be corrected and resubmitted. Then process the rest normally if the other auto-process checks pass.
  - Every procedure line has a listed CARC: no split; skip the claim.
- Splitting uses the same code as the manual Split Claim button, so attachments, notes and tracking behave the same.
- Never split a claim that is already received, or a preauth.

**Permissions and audit.**
- No new permission.
- Write a log entry when a claim is split automatically. The manual split (`butSplitProcs_Click` calling `Claims.InsertSplitClaim`) writes no audit entry today, so there is no existing pattern. Proposed PermType: `ClaimEdit`, with the original ClaimNum as FKey and the new ClaimNum in the text. Confirm against the office securitylog.

**Preferences.** New bool, default off. Proposed name `EraAutoSplitCarcDenials`. **Confirm the real name in the office `preference` table.**

**Schema.** Preference row only.

**Touchpoints.**
- `OpenDentBusiness/X12/X835.cs`: `GetCarcCodesNoAutoProcessForClaim` (near line 3652), `CanClaimBeAutoProcessed` (near line 4296).
- `OpenDentBusiness/Data Interface/Claims.cs` (`InsertSplitClaim`, line 1659), `OpenDental/Forms/FormEtrans835ClaimPay.cs` (`butSplitProcs_Click`, line 711), `OpenDental/Forms/FormEtrans835Edit.cs`.
- `OpenDental/User Controls/Preferences/UserControlManageGeneral.cs` and its `.Designer.cs`, `OpenDentBusiness/TableTypes/Pref.cs`, `OpenDental/Resources/PrefInfos.xml`.

---

## Chart module

### v25_1-chart-group-note-flexible: Group notes with any procedures, or none

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Chart Module); 25.1 DB docs (`procedurelog` table description of Group Notes); baseline https://www.opendental.com/manual243/procgroupnote.html.

**Behavior.** In 24.3 a group note can only be made for completed procedures that share date, clinic and provider. In 25.1:
- any procedures can be attached, whatever their status, date or provider;
- a group note can have no procedures at all;
- procedures can be added to or removed from an existing group note inside the Group Note window.

The 25.1 table description confirms the model:
- a group note is a procedure with status EC and code `~GRP~`, with exactly one note;
- its links go through `procgroupitem`;
- its ProvNum is set on its own, apart from the attached procedures.

**UI.**
- Chart module: the right-click **Group Note** item works for any selection of procedures, and with nothing selected it makes an empty group note.
- Group Note window (`FormProcGroup`):
  - The procedures grid gets **Add** (pick procedures from the patient's chart) and **Remove** buttons.
  - **Procedure Date** becomes editable. Default: the latest attached procedure date, or today when there are none.
  - Add a **Provider** dropdown. Default: the attached procedures' provider when they all share one, otherwise the user's provider, otherwise the practice default.

**Rules.**
- Keep these blocks:
  - another group note cannot be attached;
  - locked or invalidated procedures cannot be attached;
  - deleted procedures cannot be attached;
  - multi-visit groups cannot contain a group note.
- Drop the 24.3 checks for "must be complete" (`CanGroupRow`), and "same date", "same clinic" and "same provider" (`CanAddGroupNote`).
- Progress notes: a group note shows after the last procedure it is attached to; with none attached, it sorts by its own date.
- When the progress notes are filtered by tooth, 24.3 shows a group note only if one of its attached procedures is visible (`DoesGridProgRowPassFilter`). A group note with nothing attached would then never show; proposed: show it whenever its date is in range.
- Aggregate notes (`ProcGroupNoteDoesAggregate`) work as before for the attached procedures that have notes.
- Signing, locking and invalidating are unchanged.
- **Permission to edit or delete:**
  - any attached procedure complete: Edit Completed Procedure (`ProcCompleteEdit`);
  - procedures attached but none complete: `ProcDelete` (TP Procedure Delete), as 24.3 does;
  - **nothing attached** (proposed): `ProcCompleteEdit`, applied to the group note's own date.

**Permissions and audit.** The existing permissions, plus `GroupNoteEditSigned`. Log adding and removing procedures as procedure edits on the group note.

**Preferences.** None.

**Schema.** None.

**Touchpoints.**
- `OpenDental/Main Modules/ControlChart.cs`:
  - `CanAddGroupNote` (near line 5544) and its date, clinic and provider checks at lines 5568 to 5580;
  - per-row checks in `CanGroupRow` (near line 6146; the "must be complete" check is near line 6180);
  - menu handler near lines 1224 to 1280;
  - tooth-filter visibility in `DoesGridProgRowPassFilter` (near line 6326).
- `OpenDental/Forms/FormProcGroup.cs` and its designer.
- `OpenDentBusiness/Data Interface/ProcGroupItems.cs`, `OpenDentBusiness/Db Multi Table/ChartModules.cs`, `OpenDentBusiness/Data Interface/Procedures.cs`.

**Open questions.** The blog mentions only date and provider. Dropping the same-clinic rule is our choice. A group note's ClinicNum then defaults the same way as its provider.

---

## Sheets, eForms, setup and miscellaneous

### v25_1-misc-claimproc-override-audit: Audit Ins Est and Allowed overrides

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Other Changes).

**Behavior.** Changing the insurance-estimate override (`claimproc.InsEstTotalOverride`) or the allowed-amount override (`claimproc.AllowedOverride`) on a procedure's estimate writes an audit-trail entry with the old and new values.

**Rules.**
- `-1` means "no override" and displays as blank, so "blank to 50.00" and "50.00 to blank" are both logged.
- Log from every place that saves these fields, not only from `FormClaimProc`:
  - batch estimate tools;
  - ERA posting that sets allowed amounts;
  - treatment plan estimate edits.
- One entry per claimproc per save. Include the procedure code and tooth, and the carrier or plan.

**Permissions and audit.**
- Logging only.
- PermType: unknown. Check the office securitylog (`LogText LIKE '%Override%'`). Fallback proposal: `ProcEdit` for TP and estimate claimprocs, `ClaimProcReceivedEdit` for received ones.
- FKey = ClaimProcNum.

**Preferences, schema.** None.

**Touchpoints.**
- `OpenDental/Forms/FormClaimProc.cs`: save at lines 751 to 785, change detection at lines 1286 to 1291, and the existing InsWriteOffEdit log at line 1390 as a pattern.
- `OpenDentBusiness/Data Interface/ClaimProcs.cs` (near line 741, write-off logging pattern), `OpenDentBusiness/Data Interface/SecurityLogs.cs`.

### v25_1-setup-programlink-selectedprocnums: `[SelectedProcNums]` program-link field

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html (also backported to 24.4.34; not in 24.3.63); https://www.opendental.com/site/programlinks.html; https://www.opendental.com/site/programlinkoutputfile.html.

**Behavior.** Program links (bridges) can use a new replacement field, `[SelectedProcNums]`, in the command line, the path or URL, and the output-file template. When the bridge button is clicked it becomes the ProcNums of the procedures currently selected in the module, separated by commas. This lets a custom bridge or a web tool act on exactly those procedures.

**Rules.**
- Where the selection comes from:
  - Chart: selected procedure rows in the progress notes grid; group notes and non-procedure rows are ignored.
  - Treatment Plan: selected procedures.
  - Account: selected ledger rows that are procedures.
  - Other modules and the main toolbar: empty string.
- No selection gives an empty string, not an error.
- Order follows the grid's row order. No spaces.
- Today `ProgramL` runs `ReplaceHelper` only when a patient is selected (the `if(patient!=null)` block near line 533). Replace `[SelectedProcNums]` with an empty string in the no-patient path too, so the literal tag never reaches the other program.
- List the field in the Replacements picker for program-link output files.
- The public Program Links and Output File pages do not mention the field yet, so the rules above are our own design.

**Permissions, preferences, schema.** None.

**Touchpoints.**
- `WpfControlsOD/Logic/ProgramL.cs`: `Execute(long programNum, Patient patient)` (line 16) needs an overload that takes the selected ProcNums; `ReplaceHelper` near line 560.
- Callers: `OpenDental/Main Modules/ControlChart.cs` (near line 3635), `ControlTreat.cs` (near line 424), `ControlAccount.cs`, `ControlAppt.cs` (near line 836), `ControlFamily.cs` (near line 484), `ControlImages.cs` (near line 2546), and `FormOpenDental.cs` (near lines 2438 and 6212, main toolbar, no selection).
- `WpfControlsOD/Frms/FrmMessageReplacements.xaml.cs` (`MessageReplaceType`).

### v25_1-sheets-signature-change-user: Change the signing user on sheet signature boxes

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Other Changes); 25.1 DB docs (`sheetfield.IsSigProvRestricted` and `CanElectronicallySign` descriptions; `patientnote.UserNumOrthoLocked` value -5); baseline https://www.opendental.com/manual243/sheetssignature.html.

**Behavior.** When a provider has to sign a sheet that someone else has open, for example on an assistant's workstation, the signer can be switched right at the signature box. Nobody has to log off. The temporary user is used for:
- the provider-only check (Restrict Signatures to Providers);
- the electronic-signature stamp (the E button).

The Group Note window already does this in 24.3 (a "..." button next to User).

**UI.**
- In Fill Sheet (`FormSheetFillEdit`), each SigBox and SigBoxPractice that allows electronic signing or is provider-restricted gets a small **Change User** button beside it.
- Clicking opens the simple log-on dialog (`FormLogOn` with `isSimpleSwitch: true`).
- On success, that box's signer becomes the chosen user and a caption under the box shows the name.
- The box enables if the user passes the provider check. The E button stamps that user's name.

**Rules.**
- The switch applies to that box only, and only while the sheet is open. The program's logged-on user (`Security.CurUser`) does not change.
- Changing the user clears any signature already in that box.
- Not available in kiosk or terminal mode.
- 25.1 stores no "signed by" user on sheet fields; `sheetfield.UserSigned` arrives in 25.2. Until then the signer shows only in the stamp text and the audit log.
- **Ortho Chart.** The 25.1 description of `patientnote.UserNumOrthoLocked` adds the value `-5`, meaning "this workstation switched users locally to sign an ortho chart row". `-1` still means another session with the same username took over.
  - When `FormOrthoChart`'s existing Change User is used, write `-5` while the switch is in effect.
  - Treat `-5` like a held lock.

**Permissions and audit.** Write a SheetEdit log entry naming the user who signed through a switch, with the sheet's PatNum and SheetNum.

**Preferences, schema.** None in 25.1.

**Touchpoints.**
- `OpenDental/Forms/FormSheetFillEdit.cs` (signature box creation near lines 1740 to 1775).
- `OpenDentBusiness/UI/SignatureBoxWrapper.cs` (`UserSig`, line 664, already used by the E button). `WpfControlsOD/UI/Controls/SignatureBoxWrapper.xaml.cs` has no `UserSig`; it wraps the WinForms control (field `signatureBoxWrapper`), so add a pass-through property if WPF sheets need it.
- `OpenDental/Forms/FormLogOn.cs`, `OpenDental/Forms/FormProcGroup.cs` (`butChangeUser_Click`, line 326, as a pattern), `OpenDental/Forms/FormTPsign.cs` (near lines 169 to 176, same checks).
- `OpenDental/Forms/FormOrthoChart.cs` (lock handling near lines 147 to 290, `butChangeUser_Click` near line 1031), `OpenDentBusiness/Data Interface/PatientNotes.cs`.

### v25_1-sheets-autosave-images: Automatic copy of saved sheets to Imaging

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`sheetdef.AutoCheckSaveImage` description).

**Behavior.**
- **24.3:** Fill Sheet shows a **Save to Images** checkbox. It appears when an image category is flagged Autosave Forms, and its default comes from the sheet def's `AutoCheckSaveImage`. Users can forget to check it, or uncheck it by mistake.
- **25.1:** the checkbox is gone. When the sheet def has the option on, every save that changed the sheet also saves a PDF copy to the Imaging module.

**UI.**
- Remove `checkSaveToImages` from `FormSheetFillEdit`.
- In `FormSheetDef`, relabel `checkAutoSaveCheck` (today "Check 'Autosave' By Default") to describe what now happens, for example "Copy to Imaging when saved".
- Keep the category override (`AutoCheckSaveImageDocCategory`).

**Rules.**
- A copy is written only when:
  - the sheet has a patient (PatNum not 0); and
  - a non-hidden image category flagged Autosave Forms ("U") exists (24.3 requires this even when the def names an override category; keep that); and
  - something on the sheet changed in this save, including the signature.
- Opening and closing without changes writes nothing.
- Category: the def's override if set, otherwise the first "U" category (as today).
- 24.3 never autosaves while the sheet is open in a terminal (`!IsInTerminal`, near line 588). Proposed: keep that, and make the copy when the office later saves or imports the kiosk or terminal sheet.

**Permissions, preferences, schema.** None.

**Touchpoints.**
- `OpenDental/Forms/FormSheetFillEdit.cs`: the uses of `_isAutoSave && checkSaveToImages.Checked` at lines 212, 279, 325, 450 and 2545; setup at lines 586 to 598; categories near line 2459.
- `OpenDental/Forms/FormSheetDef.cs` (lines 74 to 77 and 326 to 327).

### v25_1-eforms-prefill-from-guarantor: eForm fields prefill from the guarantor

- **Category:** desktop. **Size:** M. **Depends on:** none (eForms exist in 24.3).
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`eformfielddef.PrefillFromGuar`, `eformfield.PrefillFromGuar`).

**Behavior.** An eForm field linked to a database value can be set to **Prefill from guarantor**. When the form is filled for a patient whose own value is empty, the field starts with the guarantor's value. The main use is address fields on children's forms, so families do not type the same address twice.

**UI.**
- In the eForm text-field editor (`FrmEFormTextBoxEdit`), a checkbox **Prefill from guarantor**.
- Enable it only when the chosen DB link is an eligible field: Address, Address2, City, State, Zip and HmPhone. Extend the list if the office database shows others in use.

**Rules.**
- Prefill only when the patient's stored value is empty and the patient is not their own guarantor.
- The patient can still change the value.
- On import, the value is treated like any other entry (or imported at once if the field is also Auto import; next item).
- When an eForm is created from its def, copy the flag from `EFormFieldDef` to `EFormField`.
- Kiosk fill and office fill on the desktop behave the same.

**Permissions, preferences.** None.

**Schema.**
- `eformfielddef.PrefillFromGuar tinyint(4) NOT NULL`, after `AutoImport`.
- `eformfield.PrefillFromGuar tinyint(4) NOT NULL`, after `AutoImport`.
- Default 0. Matches the 25.3 DDL (in `eformfield`, the 25.2 columns `ValueLabelEnglish` and `PickListVisEnglish` follow).

**Touchpoints.**
- `OpenDentBusiness/EForms/EFormFiller.cs` (prefill) and `OpenDentBusiness/EForms/EFormFieldsAvailable.cs`.
- `OpenDentBusiness/TableTypes/EFormFieldDef.cs` and `EFormField.cs`; `OpenDentBusiness/Data Interface/EFormFieldDefs.cs` and `EFormFields.cs` (copying def to field); CRUD files.
- `WpfControlsOD/Frms/FrmEFormTextBoxEdit.xaml` and `.xaml.cs`, `WpfControlsOD/Frms/FrmEFormFillEdit.xaml.cs`.

### v25_1-eforms-auto-import-fields: eForm fields that import on submit

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; 25.1 DB docs (`eformfielddef.AutoImport`, `eformfield.AutoImport`; changed descriptions of `eform.Status`, `eform.SaveImageCategory`, `eformimportrule`, `eformfield.IsRequired`, `eformfielddef.IsRequired`).

**Behavior.** A database-linked eForm field can be marked **Auto import**. When the patient finishes the form, those fields go straight into the database. Typical uses are name, address and other demographics, where the patient knows best and a review step only slows the office. Other fields still go through the normal import review.

**UI.** An **Auto import** checkbox in the field editors for linked fields: `FrmEFormTextBoxEdit`, `FrmEFormDateFieldEdit`, `FrmEFormRadioButtonsEdit`, `FrmEFormCheckBoxEdit`. Disable it when the field has no DB link.

**Rules.**
- Ticking Auto import makes the field required: tick and lock `IsRequired`. That way an empty answer can never wipe stored data. This applies to text, date and radio-button fields. Checkboxes are different (next rule): for an auto-import checkbox, do not force `IsRequired`, and import the checked state as it is.
- **25.1 rule for Required on checkboxes (from the 25.1 description of `eformfield.IsRequired` and `eformfielddef.IsRequired`).** Only checkboxes linked to `allergiesNone` or `problemsNone` may be Required; for them it means the patient must tick at least one allergy (or problem) or tick None. In our 24.3 `FrmEFormCheckBoxEdit.SetVisAllerProb` (near line 236), the Required box shows for every checkbox link except `allergy:` and `problem:`, so hide it for the others and clear `IsRequired` on load.
- Import happens when the form is submitted as Filled, from desktop fill or kiosk. Write each auto-import field through the same import code (`EFormImport`) and log it the same way.
- The form stays **Filled** until the office imports the rest. Then it becomes **Imported**.
- Related 25.1 rule changes:
  - Remove `EnumEFormStatus.UnmatchedToPatient (4)`. Forms are always tied to a patient; load any stored 4 as Filled.
  - `eform.SaveImageCategory` is applied only when the user saves the form by hand, no longer during import.
  - `eformimportrule` is documented as unused. Hide its setup window (`FrmEFormImportRules`) or leave it inactive.

**Permissions and audit.** Patient changes made by auto import are logged with the same PermTypes the manual import uses (for example PatientEdit), noting that the source was an eForm.

**Preferences.** None.

**Schema.**
- `eformfielddef.AutoImport tinyint(4) NOT NULL`, after `SpaceToRight`.
- `eformfield.AutoImport tinyint(4) NOT NULL`, after `SpaceToRight`.
- Default 0. Matches the 25.3 DDL.

**Touchpoints.**
- `OpenDentBusiness/EForms/EFormImport.cs` (`Import(EForm)`), `OpenDentBusiness/TableTypes/EForm.cs` (status enum), `OpenDentBusiness/Data Interface/EForms.cs`.
- `WpfControlsOD/Frms/FrmEFormFillEdit.xaml.cs`, the four field editors named above, `WpfControlsOD/Frms/FrmEFormImportRules.xaml.cs`.

### v25_1-eforms-conditional-children-view: View an eForm parent field's conditional children

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html.

**Behavior.** In eForm setup, the editor for a checkbox or radio-button field (a possible parent) can list every field whose visibility depends on it, meaning its `ConditionalParent` points to this field. The list shows each child's label and the trigger value (`ConditionalValue`), and double-clicking a child opens it for editing.

**UI.**
- A **Children** button or list in `FrmEFormRadioButtonsEdit` and `FrmEFormCheckBoxEdit`.
- Finish `FrmEFormFieldChildren`; in 24.3 it is a stub with commented-out code.

**Rules.**
- Match children by the parent's label. After 25.2, `ValueLabelEnglish` is used for translated forms; for 25.1, match `ValueLabel`.
- Renaming a parent's label offers to update its children's `ConditionalParent`.

**Permissions, preferences, schema.** None.

**Touchpoints.** `WpfControlsOD/Frms/FrmEFormFieldChildren.xaml` and `.xaml.cs`, `WpfControlsOD/Frms/FrmEFormRadioButtonsEdit.xaml.cs` (commented call near line 501), `WpfControlsOD/Frms/FrmEFormCheckBoxEdit.xaml.cs`, `WpfControlsOD/Frms/FrmEFormDefEdit.xaml.cs`.

### v25_1-misc-dbm-email-invalid-chars: DBM: invalid characters in email bodies

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html (method `EmailmessageInvalidCharactersInBodyText`, also backported to 24.4.42); related 24.4 item about cleaning Unicode white space in Raw Emails: https://www.opendental.com/site/version24_4.html.

**Behavior.** A new Database Maintenance check finds email messages whose body text holds characters that break display, printing or re-sending, and removes them in Fix mode.

**Rules.** Public sources name only the method, so the character set below is our proposal.
- Characters to remove: control characters other than tab, CR and LF (U+0000 to U+0008, U+000B, U+000C, U+000E to U+001F) and unpaired UTF-16 surrogates.
- Check mode reports the count.
- Fix mode updates only `emailmessage.BodyText` (and `RawEmailIn` only if it is used for display) and reports how many rows it fixed.
- Process in batches by `EmailMessageNum` so large tables do not lock for long.
- The method name must match Open Dental's (`EmailmessageInvalidCharactersInBodyText`), because DBM preferences and `databasemaintenance` rows are keyed by method name.

**Permissions.** Existing DBM rules (Setup).

**Preferences, schema.** None.

**Touchpoints.**
- `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`: add `[DbmMethodAttr(HasBreakDown=true)] public static string EmailmessageInvalidCharactersInBodyText(bool verbose, DbmMode dbmMode)`, modeled on `SpecialCharactersInNotes` (line 488).
- `OpenDentBusiness/Data Interface/EmailMessages.cs`, `OpenDental/Forms/FormDatabaseMaintenance.cs`.

### v25_1-misc-medication-hide: Hide medications in the master list

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Other Changes); 25.1 DB docs (`medication.IsHidden`); baseline https://www.opendental.com/manual243/medicationmaster.html.

**Behavior.** Medications in the master list (Lists, Medications) can be marked hidden. 24.3 cannot delete a medication that a patient uses, that is in the allergy list, or that is an EHR indicator (24.3 Medications manual page), so offices could not clean up the list until now.

**UI.**
- Edit Medication (`FormMedicationEdit`): a **Hidden** checkbox.
- Medications (`FormMedications`):
  - a **Show Hidden** checkbox, off by default;
  - hidden rows show grayed or with a Hidden column when shown.

**Rules.**
- Hidden medications are not offered when picking a medication for a patient, an allergy, an EHR rule, an eForm medication list or a reconcile.
- Existing patient medications, allergies and reports keep showing them.
- Hiding a generic does not hide its brands. Warn if visible brands remain.
- Export includes the flag. Import leaves it off for new items.
- Merge (`FormMedicationMerge`) can still pick hidden items.

**Permissions and audit.** Medication Definition Edit (`MedicationDefEdit`), which already exists. Log hide and unhide.

**Preferences.** None.

**Schema.** `medication.IsHidden tinyint(4) NOT NULL`, after `RxCui`; default 0. Matches the 25.3 DDL.

**Touchpoints.**
- `OpenDental/Forms/FormMedications.cs` (`FillGrid` uses `Medications.GetList`, near line 101), `OpenDental/Forms/FormMedicationEdit.cs`.
- `OpenDentBusiness/TableTypes/Medication.cs`, `OpenDentBusiness/Data Interface/Medications.cs`.
- Pickers: `OpenDental/Forms/FormMedical.cs` (near line 253), `OpenDental/Forms/FormMedicationReconcile.cs`, `OpenDental/Forms/FormAllergyDefEdit.cs`, `WpfControlsOD/Frms/FrmEFormMedicationListEdit.xaml.cs`.

### v25_1-setup-overjet-bridge: Overjet program link

- **Category:** vendor-direct. **Size:** S.
- **Depends on:** the decoupling step that removes HQ program gating (`Programs.IsEnabledByHq`).
- **Sources:** https://www.opendental.com/site/version25_1.html; https://www.opendental.com/site/bridgeoverjet.html; https://www.opendental.com/site/programbridges.html.

**Behavior.** A new program link, **Overjet**, opens Overjet's AI diagnosis web app in the browser.
- If a patient is selected when the button is clicked, Overjet opens on that patient.
- If not, Overjet's login page opens.

**UI.** Program Links entry "Overjet" with the standard settings:
- Enabled.
- Path of file to open, defaulting to `https://clinic.overjet.ai/app/bridge_redirect`.
- Text on button.
- Hide Button for Clinics.
- Toolbars, defaulting to Main Toolbar.
- Button image (22×22).

**Rules.**
- Build the URL from the path plus patient parameters.
- Public Open Dental pages do not give the parameter names. Get the format from Overjet's integration docs. Expect at least the patient ID and probably name and birthdate.
- Proposed: add a program property for the patient ID source (PatNum or ChartNumber), as other bridges have. The public Overjet page lists no such property (only Enabled, Path, Text on button and Button Settings), so Open Dental's bridge may always send one fixed ID.
- Open with `ODFileUtils.ProcessStart`.
- Credentials are entered in Overjet's own login page. Nothing passes through any Open Dental server.

**Permissions and audit.** As other bridges.

**Preferences.** None.

**Schema.** A new `program` row (ProgName `Overjet`) and its `toolbutitem` row, inserted by a conversion step if missing.

**Touchpoints.**
- `OpenDentBusiness/TableTypes/Program.cs` (`ProgramName` enum, add `Overjet`).
- `WpfControlsOD/Logic/ProgramL.cs` (dispatch near line 55, where `AiDental` is handled).
- New `WpfControlsOD/Bridges/Overjet.cs`, modeled on `WpfControlsOD/Bridges/AiDental.cs`.
- The upgrade conversion.

### v25_1-misc-wiki-draft-compare: Compare a wiki draft with the live page

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; baseline https://www.opendental.com/manual243/wikiaddedit.html and https://www.opendental.com/manual243/wikihistory.html.

**Behavior.** The Wiki Drafts window gets a **Compare** button. It shows the selected draft next to the current published page, with differences marked, before the user decides to edit, publish or delete the draft.

**UI.** In `FormWikiDrafts`, a **Compare** button that needs exactly one selected draft. It opens the existing `FormWikiCompare`, live page on the left and draft on the right.

**Rules.**
- If the live page was deleted or archived, compare against its last history version.
- Compare on markup text, as wiki history already does.

**Permissions.** Viewing needs only wiki access. The Wiki Admin rules for locked pages apply only to editing.

**Preferences, schema.** None.

**Touchpoints.** `OpenDental/Forms/FormWikiDrafts.cs` and its designer; `OpenDental/Forms/FormWikiCompare.cs` (add an overload of `InitializeRevisionsSelected` that takes two `WikiPage` objects); `OpenDental/Forms/FormWikiHistory.cs` (`butCompare_Click`, line 153, as a pattern); `OpenDentBusiness/Data Interface/WikiPages.cs`.

### v25_1-misc-dbm-securityhash-rehash-patient: DBM: rehash the whole patient table

- **Category:** desktop. **Size:** S.
- **Depends on:** the decoupling plan's in-repo SecurityHash (HMAC-SHA256 instead of the proprietary CDT hash).
- **Sources:** https://www.opendental.com/site/version25_1.html (method `SecurityHashRehashPatientTable`, also backported to 24.4.48); 25.1 DB docs (`patient.SecurityHash`); `D:/Dev/opendental/docs/decoupling-plan.md`.

**Behavior.** A new Database Maintenance method recomputes `patient.SecurityHash` for every patient row. The normal hashing pass after an update (`SecurityHash.UpdateHashing`) never fixes a bad patient hash: for patients, `PatientWorker` only fills rows whose `SecurityHash` is empty, while the other tables rehash only rows newer than `GetHashingDate()` (one month back, or the hashing start date). Use the method when integrity warnings show on many patients.

**Why it matters more for us.** Every hash in the office database was made with Open Dental's key and algorithm. After migration, our HMAC will fail all of them. This method, extended to the other hashed tables (claimproc, paysplit, appointment, claim, payplan, and in 25.1 insverify; see the next item), is how we re-baseline integrity after the office data is adopted.

**Rules.**
- Check mode counts rows whose hash does not verify.
- Fix mode rehashes all rows, in batches, on a background thread with progress, as `SecurityHash.RunPatient` and its `PatientWorker` do.
- Write one audit entry with the count.

**Permissions.** DBM (Setup). Proposed: also require Security Admin, because rehashing hides tampering.

**Preferences, schema.** None.

**Touchpoints.** `OpenDentBusiness/Misc/SecurityHash.cs` (`UpdateHashing` line 26, `ResetPatientHashing` line 40, `RunPatient` line 161 and its `PatientWorker`, `GetHashingDate`), `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`, `OpenDentBusiness/Data Interface/Patients.cs`.

### v25_1-security-insverify-securityhash †: Integrity hash on insurance verification rows

- **Category:** desktop. **Size:** S.
- **Depends on:** the in-repo SecurityHash (as above).
- **Sources:** 25.1 DB docs (`insverify.SecurityHash`, `insverifyhist.SecurityHash`, and the reworded `insverify.DateLastVerified` and `DateLastAssigned`). No release-note line.

**Behavior.**
- `insverify` rows get an integrity hash over `UserNum`, `DefNum` and `DateLastAssigned`, the assignment and status fields that drive the Insurance Verification List.
- When a row is copied to `insverifyhist`, the hash is copied unchanged and stays valid, because those three fields do not change.
- Rows that fail the check get the same "integrity" warning treatment as other hashed tables.

**Rules.**
- Hash on insert, and on every update that changes one of the three fields.
- Include the table in the post-update hashing pass, limited to rows newer than the hashing start date, as the other tables are.
- The 25.1 descriptions also restate when the dates change; keep 24.3 behavior:
  - `DateLastVerified` changes only when verification is saved in the Verification List or the dates are typed in Edit Insurance Plan;
  - `DateLastAssigned` changes on assign or unassign, on status change, on plan drop, and on batch-verify errors.

**Permissions, preferences.** None.

**Schema.**
- `insverify.SecurityHash varchar(255) NOT NULL`, after `SecDateTEdit`.
- `insverifyhist.SecurityHash varchar(255) NOT NULL`, after `SecDateTEdit`.
- Default `''`. Matches the 25.3 DDL.

**Touchpoints.** `OpenDentBusiness/TableTypes/InsVerify.cs` and `InsVerifyHist.cs`, `OpenDentBusiness/Data Interface/InsVerifies.cs` and `InsVerifyHists.cs`, `OpenDentBusiness/Misc/SecurityHash.cs`, `OpenDental/Forms/FormInsVerificationList.cs`.

### v25_1-setup-proccode-morefees-filters: Provider and clinic filters in More Fees

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_1.html; baseline https://www.opendental.com/manual243/procedurecodeedit.html.

**Behavior.** The More Fees window opened from Edit Procedure Code lists every fee for the code: each fee schedule, plus clinic and provider overrides. It gets **Provider** and **Clinic** filters, so offices with many overrides can find one fee.

**UI.** `FormProcCodeEditMore`:
- a **Provider** dropdown (All, Default/none, each provider);
- a **Clinic** dropdown (All, Default/HQ, each clinic the user may access; shown only with clinics on).

The grid refreshes when either changes.

**Rules.**
- "All" keeps today's list.
- "Default/none" shows fees with ProvNum 0 (or ClinicNum 0).
- Respect clinic restrictions on the user.
- Once 24.4 effective dates exist, show the date column and filter the same way.

**Permissions, preferences, schema.** None.

**Touchpoints.** `OpenDental/Forms/FormProcCodeEditMore.cs` and its designer, `OpenDental/Forms/FormProcCodeEdit.cs`, `OpenDentBusiness/Data Interface/Fees.cs`.

### v25_1-setup-feetools-tabs: Fee Tools window reorganized into tabs

- **Category:** desktop. **Size:** M.
- **Depends on:** 24.4 fee schedule effective dates, so the tabs carry the effective-date inputs. The rework itself does not need them.
- **Sources:** https://www.opendental.com/site/version25_1.html; https://opendental.blog/version-25-1/ (Fee Tools); baseline https://www.opendental.com/manual243/feescheduletools.html.

**Behavior.** Layout change only. Fee Tools currently shows every tool (Copy To, Increase by %, Clear, Export/Import, Global Updates) at once around a shared "Select Fees" box. 25.1 puts each tool on its own tab and shows only the inputs that tool needs. The tools work exactly as before.

**UI.**
- `FormFeeSchedTools` becomes:
  - the shared **Select Fees** panel at the top (fee schedule, clinic or group, provider, and Show Fee Schedule Groups when enabled);
  - a tab control with **Copy**, **Increase**, **Clear**, **Export / Import** (with Import Canada for Canada) and **Global Updates**.
- Each tab has its own action button.
- The rounding options appear only on Increase.
- The target schedule, clinic and provider appear only on Copy.

**Rules.**
- No logic changes. Keep every existing check, confirmation and permission (FeeSchedEdit) and every audit entry.
- Remember the last tab per user (proposed: `userodpref`; optional).

**Preferences, schema.** None.

**Touchpoints.** `OpenDental/Forms/FormFeeSchedTools.cs` and its designer (`groupBox1` Copy To, `groupBox2` Clear, `groupBox3` Increase by %, `groupBox5` Export/Import, `groupGlobalUpdateFees`, `groupBox7` Select Fees).

### v25_1-canada-etrans-counters-uncapped †: Canadian transaction counters no longer cap out

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** 25.1 DB docs (`etrans.OfficeSequenceNumber`, `CarrierTransCounter`, `CarrierTransCounter2`). No release-note line.

**Behavior.** 24.3 throws an error when the office sequence number reaches 999999, or a carrier counter reaches 99999. Offices that have sent that many transactions cannot send any more. In 25.1 the stored counters keep counting up. Only the value written into the CDAnet message is reduced: modulo 1,000,000 for the office sequence number and modulo 100,000 for carrier counters.

**Rules.**
- Store the full number in `etrans` (`int(11)` has plenty of room).
- Remove the exceptions in `Etranss.cs`.
- When formatting the message field, use `value % 1000000` or `value % 100000`. A result of 0 must be sent as allowed by the CDA spec. Proposed: map 0 to 1, to match the old wrap to 1.
- Lookups that match a response to its request by counter must compare the modded values.

**Permissions, preferences, schema.** None (meaning change only).

**Touchpoints.** `OpenDentBusiness/Data Interface/Etranss.cs` (lines 365 to 366, 392 to 393, 421 to 422), `OpenDentBusiness/Eclaims/Canadian.cs` and `OpenDentBusiness/Eclaims/CanadianOutput.cs` (field formatting).

### v25_1-schema-enum-compat †: Reserve all 25.1 enum values

- **Category:** desktop. **Size:** S. **Depends on:** the 24.4 values that our 24.3 code lacks: `EnumPermType` 264 and 265, `eServiceCode` 50 to 52, and all of `EnumCategoryBetterDiag` with the `imagedraw.BetterDiagLayer` column (see the notes under the table).
- **Sources:** https://www.opendental.com/OpenDentalDiffDocumentation25-1.xml; full docs https://www.opendental.com/OpenDentalDocumentation24-4.xml and https://www.opendental.com/OpenDentalDocumentation25-1.xml. See also Appendix A of [`schema-changes-24.3-to-26.1.md`](schema-changes-24.3-to-26.1.md).

**Behavior.** The office database stores these values as integers. Our enums must define them, even for features we cut, so that loading, filtering and showing old rows does not fail or mislabel anything.

| Enum (column) | Values to add | Use in fork |
|---|---|---|
| `EnumPermType` (`grouppermission.PermType`, `securitylog.PermType`) | 264 ImageSignatureCreate, 265 SignedImageEdit (both 24.4), 266 BlockoutsFlagged, 267 PayPlanUnlock, 268 SendAlertsFromHQ | 266 and 267 active (items above). 264 and 265 wait for 24.4. 268 reserved, hidden from the security tree, never granted. |
| `AlertType` (`alertitem.Type`, `alertcategorylink.AlertType`) | 41 BetterDiagnostics, 42 MessageToPayTag, 43 MassEmailUpload, 44 MassEmailReceipt, 45 HQNotification | Reserved; never raised. Hide them from alert category setup. |
| `FormType` (`alertitem.FormToOpen`) | 19 FrmStatementSendSetup, 20 FormAdvertisingMassEmailUpload | Reserved. Clicking such an alert does nothing. |
| `CommItemSource` (`commlog.CommSource`) | 10 MassEmail | Show as "Mass Email" on imported commlogs. |
| `EnumEFormFieldType` (`eformfielddef.FieldType`) | 8 PatientList | Reserved. The 25.1 description calls it a patient picker (so far only for choosing an insurance subscriber) that can also add a new patient; no release note announces it. Render as read-only text if one ever appears. |
| `EnumEFormStatus` (`eform.Status`) | remove 4 UnmatchedToPatient | Load 4 as Filled. |
| `GuardianRelationship` (`guardian.Relationship`) | 0 None, plus the renumbering | See v25_1-family-guardian-relationship-renumber. |
| `eServiceType`, `eServiceAction`, `eServiceCode` (`eservicelog`, `eserviceshortguid`) | 14 to 16; 118 to 126; 53 and 54 | Reserved (cut services). Only needed if we keep the eService log viewer. |
| `InvalidType` (`signalod.IType`) | 116 MassEmail | Reserved; ignore the signal. |
| `EnumCategoryBetterDiag` (`imagedraw.BetterDiagLayer`) | 8 now PeriapicalRadiolucency (was Infection); new 14 RootStumps, 15 MissingTooth, 16 Calculus, 17 Disclaimer, 18 MarginalDiscrepancy | Reserved. BetterDiagnostics is cut, but drawings that already exist must load. **Depends on 24.4:** neither this enum nor the `imagedraw.BetterDiagLayer` column exists in our 24.3 code (the column is one of the 24.4 columns in the measured diff), so add the 24.4 values 0 to 13 together with these. |

Most other enums in this table already end at their 24.4 values in our 24.3 code (for example `AlertType` ends at 40 Pearl, `InvalidType` at 115, `eServiceAction` at 117), so the 25.1 numbers follow directly. Two exceptions need their 24.4 values first: `EnumPermType` ends at 263 in our code (add 264 and 265), and `eServiceCode` ends at 49 UrlRedirect (add 24.4's 50 DeveloperPortalApi, 51 DeveloperPortalUI and 52 EmailHqService before 53 and 54).

**Touchpoints.** `OpenDentBusiness/TableTypes/GroupPermission.cs`, `OpenDentBusiness/TableTypes/AlertItem.cs` (`AlertType` at line 79, `FormType` at line 237), `OpenDentBusiness/TableTypes/Commlog.cs`, `OpenDentBusiness/TableTypes/EFormField.cs` (`EnumEFormFieldType`, line 122) and `EForm.cs` (`EnumEFormStatus`), `OpenDentBusiness/TableTypes/Guardian.cs`, `OpenDentBusiness/TableTypes/EServiceLog.cs` (`eServiceType`, `eServiceAction`), `OpenDentBusiness/TableTypes/EServiceSignal.cs` (`eServiceCode`), `OpenDentBusiness/Db/Enumerations.cs` (`InvalidType`), `OpenDental/User Controls/UserControlSecurityTree.cs` (keep the reserved ones out).

---

## Excluded

Each line says why. Cut services follow `docs/decoupling-plan.md`.

### Open Dental hosted services and HQ (excluded-od-service)

| ID | Item | Why excluded |
|---|---|---|
| v25_1-eclipboard-eclipboard25 | eClipboard 25: new app look, accessibility, dark mode, eForms in the app | eClipboard is an Open Dental hosted mobile service run through their eConnector and HQ. |
| v25_1-eservices-msgtopay-tag-alert | Alert when Message-to-Pay templates lack the payment-link tag | Message-to-Pay is an Open Dental hosted texting and payment-portal service. `AlertType` 42 is only reserved (v25_1-schema-enum-compat). |
| v25_1-eservices-thankyou-disable-typed-signature | "Disable Typed Signature" preference for New Patient Thank-You messages | Automated thank-you messages and their hosted web forms are Open Dental eServices. |
| v25_1-eservices-eforms-autosave-eclipboard | eForms submitted from eClipboard 25 are saved automatically | Applies to forms submitted through the hosted eClipboard app; desktop eForm saving is unchanged. |
| v25_1-eservices-msgtopay-on-statement-creation | Send Message-to-Pay links automatically when statements are created (new Statement Send Setup window) | Depends on Open Dental's Message-to-Pay service. `FormType` 19 is only reserved. Statement settings that stay in Billing Defaults keep working as in 24.3. |
| v25_1-eservices-eclipboard-rule-prefill | eClipboard form rule can prefill non-database fields from earlier forms | eClipboard-only setting stored in `eclipboardsheetdef.PrefillStatus`, which already exists in 24.3 (25.1 only rewords it as blank or prefill) and is kept for data compatibility. |
| v25_1-eservices-kiosk-manager-no-delete | Kiosk Manager no longer has Delete for eClipboard devices | Concerns eClipboard device registration, which is HQ-linked. |
| v25_1-imaging-betterdiagnostics | BetterDiagnostics AI integration in Imaging | Signup, billing and image processing go through Open Dental (per https://www.opendental.com/site/betterdiagnostics.html). The `imagedraw.BetterDiagLayer` values are reserved so existing drawings load. A direct-to-vendor bridge could be specced separately if BetterDiagnostics offers an API. |

### API, cloud and other (excluded-other)

| ID | Item | Why excluded |
|---|---|---|
| v25_1-misc-api-service-install | The API Service is always installed with office updates | The local REST API is cut (decoupling plan, ODApi). **Already in our base:** the release note says this was backported to 24.3.61, and our 24.3.63 code does it: `OpenDental/Logic/PrefL.cs` calls `TryInstallOpenDentalApiService(isSilent:true)` near line 536 after every database update (method near line 1268). The decoupling work must remove that call; skipping this 25.1 item is not enough. |
| v25_1-api-insplans-ismedical | API InsPlans: IsMedical | Local REST API cut. |
| v25_1-api-claims-put-inssubnum2 | API Claims PUT: optional InsSubNum2 | Local REST API cut. |
| v25_1-api-userods-put-groups-pwreset | API Userods PUT: userGroupNums, IsPasswordResetRequired | Local REST API cut. |
| v25_1-api-payments-paysplit-amounts | API Payments: set paysplit amounts | Local REST API cut. |
| v25_1-api-procedurelogs-status-cn-ec-r | API ProcedureLogs POST/PUT: status CN, EC or R | Local REST API cut. |
| v25_1-api-popups-put-datetimedisabled | API Popups PUT: DateTimeDisabled | Local REST API cut. |
| v25_1-api-commlogs-get-datetstamp | API Commlogs GET: DateTStamp | Local REST API cut. |
| v25_1-api-schedules-datetstamp | API Schedules: DateTStamp | Local REST API cut. |
| v25_1-api-sheetdefs-get-sheettype | API SheetDefs GET: SheetType filter | Local REST API cut. |
| v25_1-api-insplan-more-fields | API insplan resource: more fields | Local REST API cut. |
| v25_1-api-sheetfielddefs-get | API SheetFieldDefs GET | Local REST API cut. |
| v25_1-api-claimprocs-get-single | API ClaimProcs GET (single) | Local REST API cut. |
| v25_1-api-appointments-post-secusernumentry | API Appointments POST: SecUserNumEntry | Local REST API cut. |

---

## Fact-check log (2026-10-18)

An independent pass checked this file against the public sources and the code. Everything below was re-read on 2026-10-18.

**Coverage.** The 25.1 release-note page lists 53 items: the eClipboard 25 headline, 8 Account, 2 Appointments, 6 eServices, 2 Family, 3 Imaging, 2 Manage, 16 Miscellaneous and 13 API. Every one maps to exactly one ID above (31 built, 22 excluded). Nothing was missing. The 6 items marked † come from the database documentation. A diff of the full 24.4 and 25.1 database documents found one more rule with no release note (Required on eForm checkboxes); it was added to `v25_1-eforms-auto-import-fields` rather than as a new item. All other description changes in that diff only reword behavior that our 24.3 code already has. Counts are unchanged: 34 desktop, 3 vendor-direct, 22 excluded.

**Classification.** No item was moved. Every exclusion matches the decoupling plan. One exclusion needed a warning: the API Service installer change is already in our 24.3.63 base, so it must be removed, not just skipped.

**Behavior checks against public pages** (24.3 manual for the baseline; `/site/` pages and the blog for 25.x):
- Canada attachments (site page and manual243): columns, buttons, limits and send order confirmed. The status wording, the Perio button and which UI parts are our own were corrected. The sent-status design was fixed: `CreateCanadianOutput` takes no ClaimNum.
- Overjet (site page): fields, default URL and toolbar confirmed. The patient-ID property is now marked as our proposal.
- Blockouts and Permissions (manual243): confirmed that 24.3 lets anyone manage NS and DC blockouts.
- Code Groups (manual243 and code): the grid columns and the meaning of Show Hidden were corrected.
- Insurance History (manual243 and blog): confirmed. Added the blog's "Default Code" wording as an open point.
- Dynamic payment plans (manual243 and code): confirmed that a lock cannot be cleared in 24.3. The upgrade-grant note was clarified.
- Group notes (manual243 and code): confirmed that 24.3 needs the same date, provider and clinic. Fixed a wrong method name and added a rule for the tooth filter.
- Time Card (manual243 and blog): added the Decimal/Colon switch that the blog describes. The touchpoint lines were wrong (363 to 377 is In/Out formatting) and were fixed.
- Task setup (manual243 and code): the manual describes the preference backwards; noted.
- Medications (manual243): the 24.3 delete restriction was widened to its documented scope.
- Sheet signature boxes (manual243): confirmed.
- Sales tax, Fee Tools, More Fees, Wiki drafts (manual243): confirmed.
- Program links and output file (site pages): `[SelectedProcNums]` is not documented yet. Added the no-patient replacement rule.
- BetterDiagnostics (site page): confirmed that signup and billing go through Open Dental, so the exclusion holds.

**Codebase touchpoints.** The file cites 179 distinct file paths or names. Each one exists, either as written or under the folder given earlier on the same line, except `WpfControlsOD/Bridges/Overjet.cs` (a file to be created) and the bare suffixes `.Designer.cs` and `.xaml.cs`. Cited symbols were checked too. These were wrong and are fixed:
- `IsValidForGroupNote` does not exist; it is `CanGroupRow`.
- The WPF `SignatureBoxWrapper` has no `UserSig`.
- `EFormImport.cs` has no guardian handling.
- `Appointments.cs` line 3073 is `GetApptNumsAttachedToTask`.
- `FormTimeCard.cs` has no reference to `TimeCardsUseDecimalInsteadOfColon`.
- `EnumEFormFieldType` lives in `EFormField.cs`, not `EFormFieldDef.cs`.
- `LargeTableHelper` lives in `OpenDentBusiness/Db/`.
- Signal and eService enum files are now named.
- The ERA item assumed the manual Split Claim writes an audit entry; it writes none.
- The DBM rehash item said the normal pass "only covers recent rows". For patients it only fills empty hashes.

Small line-number drifts were corrected where found.

**Schema.** All 11 columns from 25.1 match `fox-db-schema.sql` in type, nullability, engine, charset and position, and all 11 appear in the measured diff. 25.1 adds no tables. Two dependencies on 24.4 were made explicit: the `imagedraw.BetterDiagLayer` column with its enum, and the missing `eServiceCode` values 50 to 52.

**Copied text.** The file was compared against all cited pages, the blog and the 24.4 and 25.1 database documents, looking for runs of 8 or more identical words. The only matches are UI labels and the default URL, none longer than 11 words. No passage needed rewriting.
