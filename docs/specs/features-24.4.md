# Open Dental 24.4: desktop feature specs for our 24.3.63-based fork

Status: draft, 2026-10-18; fact-checked the same day (see "Fact-check log" at the end). Scope: every change Open Dental (OD) shipped in version 24.4 (stable 2025-03-20) that affects the Windows desktop program, written so engineers can re-implement it in this fork. Companion document: [schema-changes-24.3-to-26.1.md](schema-changes-24.3-to-26.1.md) (schema only).

## How this was researched

- **Release notes**, read in full: https://www.opendental.com/site/version24_4.html. The page has one major item (eForms, with three sub-points), 29 category items (Account 5, Appointments 1, Chart 4, eServices 2, Manage 2, Miscellaneous 15) and 23 API items. Apart from the API specification, the page links to no manual pages, so the related manual pages were looked up by name.
- **How the 59 items map to the page.** The eForms major item is split in two (desktop designer, and eClipboard delivery). The 29 category items give 24 desktop items and 5 excluded items, and the 23 API items are all excluded. That makes 54. The other 5 items aren't on the release page: the manual-access change (blog), the Pearl statuses and the DentalXChange flag (DB docs, vendor-direct), and BetterDiagnostics layers and HQ eService codes (DB docs, excluded).
- **OD blog post** linked from the release page, with more detail on about 20 items: https://opendental.blog/version24_4/
- **Published database documentation**, diffed 24.3 against 24.4 column by column: https://www.opendental.com/OpenDentalDocumentation24-3.xml and https://www.opendental.com/OpenDentalDocumentation24-4.xml. The diff doc is at https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml. The diff turned up two vendor changes that the release notes don't mention (Pearl request statuses and the DentalXChange attachment flag), so both are specced below.
  - The diff also has wording-only changes that need no feature work. Each was checked against our 24.3.63 code:
    - `document.DateCreated` is user-editable but can't be moved to a future date without a permission. Our `FrmDocInfo` already enforces this with `ImageDelete`.
    - `sheetfield.IsLocked` and `sheetfielddef.IsLocked` got a clearer summary.
    - Summary text changed for `claimproc.IsOverpay`, `procnote.Signature` (decimal points allowed), the `PatStatus` Archived value, `feeschedgroup`, `insfilingcode`, `claimattach` and `orionproc` (summary blanked).
    - `ImageEdit` (89) lost the "tracking only" note. Our code already has it in the security tree with a date restriction.
- **Online manual.** The public manual at `https://www.opendental.com/manual/` is frozen at 24.3; it's the same content as `/manual243/`. It describes the *baseline* behavior. The 24.4+ manual (`/help244/` … `/help263/`) is customer-only: it needs a cookie that the program's help button sets. **We did not access it.** For a few gated pages we used only the short summaries that public web search returns; those citations are marked *(gated, search summary)*. The public "Quick Manual" pages under `https://www.opendental.com/site/0_*.html` are current and were also used.
- **Schema** was cross-checked against the 25.3.48 DDL in `D:/Dev/opendental-schema/fox-db-schema.sql`. Every 24.4 table and column below matches that dump exactly. In the measured diff (`schema-diff-24.3.63-vs-25.3.48.txt`: 15 new tables, 47 new columns, 1 rename), 24.4 accounts for 2 tables (`feeschednote`, `queryfilter`) and 7 columns: `document.ChartLetterStatus`, `document.UserNum`, `document.ChartLetterHash`, `fee.DateEffective`, `imagedraw.BetterDiagLayer`, `payplan.SheetDefNum` and `payplantemplate.SheetDefNum`. The rename (`task.TriageCategory` → `Category`) is not from 24.4.
- **Baseline check.** Three 24.4 items were backported by OD into 24.3.x and are **already in our code** (ERA mismatch matching in 24.3.43, Report Server audit in 24.3.35, and 2025 CDA codes in 24.3.32). eForms code is also already present but hidden in Release builds.

Notation used in the item sections:

- **Decision:** a design choice we make because OD's exact behavior isn't public.
- **Verify:** check against a copy of the office's 25.3.48 database before migration.

### Verify against office data (applies to several items)

OD's preference keys, some enum numbers and some names aren't in any public document. Before the office data is adopted, run these read-only checks on a copy of the office database. They keep our names and values compatible with OD's:

1. **Preference keys.** List `preference.PrefName` values that are not in our `PrefName` enum (`OpenDentBusiness/TableTypes/Pref.cs`). Use OD's key when one matches a 24.4 preference proposed below (Recession term, sales tax procedure code, incomplete-notes default, and so on).
2. **Report names.** List `displayreport.InternalName`. 24.4 adds at least one report (Production by Procedure). Match OD's internal name so that existing report permissions in `grouppermission` (PermType Reports, FKey = DisplayReportNum) carry over.
3. **Claim form fields.** Run `SELECT DISTINCT FieldName FROM claimformitem` and compare with the list in `OpenDental/Forms/FormClaimFormItemEdit.cs`. The new hospitalization-date field names will show up there.
4. **Static text tokens.** Search the text of `sheetfielddef.FieldValue` for bracketed static-text tokens that are not in `EnumStaticTextField`. This finds the new secondary-insurance field names.
5. **Chart letter statuses.** Run `SELECT ChartLetterStatus, COUNT(*) FROM document GROUP BY 1` to learn which status numbers OD used.
6. **Pearl statuses.** Run `SELECT DISTINCT RequestStatus FROM pearlrequest`. Values 5 to 7 are new in 24.4.
7. **New permissions.** Run `SELECT DISTINCT PermType FROM grouppermission WHERE PermType IN (264,265)`.

## Summary

Counts: **25 desktop**, **2 vendor-direct**, **32 excluded** (59 items in total).

| ID | Title | Module | Category | Size | Schema | Depends on | In baseline? |
|---|---|---|---|---|---|---|---|
| v24_4-eforms-desktop-forms | eForms: designer and in-office filling | eForms | desktop | M | none new (eform* tables exist) | – | code present, hidden |
| v24_4-account-payplan-sheet-select | Choose the sheet used to print each payment plan | Account | desktop | M | payplan.SheetDefNum, payplantemplate.SheetDefNum | – | no |
| v24_4-account-salestax-proc | Add Proc > Sales Tax adds one sales-tax procedure | Account | desktop | M | none (new pref) | – | no |
| v24_4-appts-copy-apptview | Copy View button for appointment views | Appointments | desktop | S | none | – | no |
| v24_4-chart-perio-implant-removed | Hide implant marker in Perio Chart when the implant is removed | Chart | desktop | S | none | – | no |
| v24_4-chart-perio-attached-gingiva | Auto-calculated Attached Gingiva row in Perio Chart | Chart | desktop | M | enum PerioSequenceType.AttGing=8 (not stored) | – | no |
| v24_4-chart-perio-recession-term | Perio Setup option: "Recession" instead of "Gingival Margin" | Chart | desktop | S | none (new pref) | – | no |
| v24_4-chart-chart-letters | Chart Letters (Word documents in the Chart module, versioned and hashed) | Chart | desktop | XL | document.ChartLetterStatus, UserNum, ChartLetterHash | – | no |
| v24_4-manage-accounting-export | Accounting Transaction History export as .csv or .txt | Manage | desktop | S | none | – | no |
| v24_4-manage-era-mismatch-match | Match an ERA to a claim despite fee or date-of-service mismatch | Manage | desktop | S | none | – | **yes (24.3.43)** |
| v24_4-tools-querymonitor-filters | Query Monitor filter groups | Misc | desktop | M | new table queryfilter | – | no |
| v24_4-imaging-signature-permissions | Permissions ImageSignatureCreate and SignedImageEdit | Imaging | desktop | M | EnumPermType 264, 265 | – | no |
| v24_4-setup-fee-effective-date | Effective dates for fees | Setup / fees | desktop | XL | fee.DateEffective + index | – | no |
| v24_4-setup-feesched-notes | Notes on fee schedules | Setup / fees | desktop | M | new table feeschednote | – | no |
| v24_4-reports-incomplete-procnotes-all | Incomplete Procedure Notes: include every procedure with no note | Reports | desktop | S | none (new pref) | – | no |
| v24_4-setup-reportserver-audit | Audit Trail entry when the Report Server setting changes | Setup | desktop | S | none | – | **yes (24.3.35)** |
| v24_4-setup-cda-codes-2025 | 2025 CDA procedure codes | Setup / Canada | desktop | S | none | – | **yes (24.3.32)** |
| v24_4-tools-rawemails-unicode | Raw Emails tool also cleans Unicode white space | Misc / DB maint | desktop | S | none | – | no |
| v24_4-reports-daily-procs-split | Daily Procedures report split into Procedures and Production by Procedure | Reports | desktop | M | displayreport row | – | no |
| v24_4-reports-individual-procs-goto-patient | Procedures report: right-click to go to the patient | Reports | desktop | S | none | v24_4-reports-daily-procs-split | no |
| v24_4-setup-codegroup-ranges | Code ranges (e.g. D2391-D2394) in Code Groups | Setup | desktop | M | none (doc change to codegroup.ProcCodes) | – | parser only |
| v24_4-lists-medication-search | Better search in the Medications list | Lists | desktop | S | none | – | no |
| v24_4-sheets-secondary-ins-statictext | More secondary-insurance static text fields | Sheets | desktop | S | none | – | no |
| v24_4-reports-dailypayments-merchant-fees | Daily Payments report: one combined merchant-fee option | Reports | desktop | S | none | – | no |
| v24_4-claims-medical-hospitalization-dates | Hospitalization dates (box 18) on the medical claim form | Claims | desktop | S | none | – | no |
| v24_4-imaging-pearl-request-statuses | Pearl AI: finer request error states | Imaging | vendor-direct | S | enum EnumPearlStatus 5–7 | – | no |
| v24_4-claims-dxc-attachment-submitted | DentalXChange attachment "Submitted" claim flag | Claims | vendor-direct | S | doc change to claim.AttachedFlags | – | no |
| v24_4-eforms-eclipboard-delivery | eForms delivered to the eClipboard app | eForms | excluded-od-service | S | – | – | – |
| v24_4-account-m2p-hide-option | Pref to hide Message-to-Pay in Account Payment menu | Account | excluded-od-service | S | – | – | – |
| v24_4-account-m2p-guarantor-statement | Message-to-Pay to guarantor with single statement | Account | excluded-od-service | S | – | – | – |
| v24_4-account-m2p-paytype | Payment type for Message-to-Pay payments | Account | excluded-od-service | S | – | – | – |
| v24_4-eservices-webforms-typed-sig-stamp | Web Forms typed signature date/time stamp | eServices | excluded-od-service | S | – | – | – |
| v24_4-eservices-automsg-time-format | Custom time format in Automated Messages | eServices | excluded-od-service | S | – | – | – |
| v24_4-help-manual-access | 24.4+ manual reached through the in-program Help feature | Help | excluded-od-service | S | – | – | – |
| v24_4-imaging-betterdiag-layers | BetterDiagnostics annotation layers | Imaging | excluded-od-service | S | imagedraw.BetterDiagLayer (carry only) | – | – |
| v24_4-hq-eservicecode-enums | New HQ eService codes (Developer Portal, Email HQ) | HQ | excluded-other | S | – | – | – |
| v24_4-api-* (23 items) | Local REST API additions | API | excluded-other | S | – | – | – |

### Consolidated schema for 24.4

All of the following match `fox-db-schema.sql` (25.3.48). Tables are MyISAM, `utf8mb3`. These go into one conversion step (for example a new `To24_4_0()` in `OpenDentBusiness/Misc/ConvertDatabases8.cs`). Then regenerate the CRUD classes with `xCrudGenerator`.

```sql
ALTER TABLE document ADD ChartLetterStatus tinyint(4) NOT NULL,
                     ADD UserNum bigint(20) NOT NULL,
                     ADD ChartLetterHash varchar(255) NOT NULL;   -- no index on UserNum in 25.3
ALTER TABLE fee ADD DateEffective date NOT NULL DEFAULT '0001-01-01',
                ADD INDEX FeeSchedCodeClinicProvDate (FeeSched,CodeNum,ClinicNum,ProvNum,DateEffective);
                -- existing FeeSchedCodeClinicProv index is kept in 25.3
ALTER TABLE payplan ADD SheetDefNum bigint(20) NOT NULL;
ALTER TABLE payplantemplate ADD SheetDefNum bigint(20) NOT NULL;
ALTER TABLE imagedraw ADD BetterDiagLayer tinyint(4) NOT NULL;      -- carried for compatibility only
CREATE TABLE feeschednote (
  FeeSchedNoteNum bigint(20) NOT NULL AUTO_INCREMENT,
  FeeSchedNum bigint(20) NOT NULL,
  ClinicNums text NOT NULL,
  Note text NOT NULL,
  DateEntry date NOT NULL DEFAULT '0001-01-01',
  SecUserNumEntry bigint(20) NOT NULL,
  SecDateEntry date NOT NULL DEFAULT '0001-01-01',
  SecDateTEdit timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (FeeSchedNoteNum), KEY FeeSchedNum (FeeSchedNum), KEY SecUserNumEntry (SecUserNumEntry)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3;
CREATE TABLE queryfilter (
  QueryFilterNum bigint(20) NOT NULL AUTO_INCREMENT,
  GroupName varchar(255) NOT NULL,
  FilterText varchar(255) NOT NULL,
  PRIMARY KEY (QueryFilterNum)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3;
```

New enum values. Code enums in this fork must use these exact numbers:

- `PerioSequenceType.AttGing = 8`. It's calculated on the fly and never saved, the same way `CAL = 7` works.
- `EnumPermType.ImageSignatureCreate = 264` and `EnumPermType.SignedImageEdit = 265`. Our enum ends at 263 (`SuperFamilyDisband`).
- `EnumPearlStatus.ErrorUploading = 5`, `ErrorProcessing = 6`, `NoResults = 7`.
- `EnumImageAnnotVendor.BetterDiagnostics = 2`, plus the new `EnumCategoryBetterDiag` (0–13). Carry these only.
- `eServiceCode` 50–52 (HQ only). Reserve the numbers; no code needed.

---

## Desktop items

### v24_4-eforms-desktop-forms: eForms designer and in-office filling

- **Category:** desktop · **Size:** M · **Depends on:** none (the eClipboard part is excluded, see `v24_4-eforms-eclipboard-delivery`)
- **Sources:** release notes (Major Changes) https://www.opendental.com/site/version24_4.html; blog https://opendental.blog/version24_4/; https://www.opendental.com/site/featureeforms.html; https://www.opendental.com/site/0_eforms.html; https://opendental.com/help244/eforms.html and https://opendental.com/help244/eformfillout.html *(gated, search summary)*.
- **Baseline:** 24.3.63 already has the eForm tables (`eform`, `eformdef`, `eformfield`, `eformfielddef`, `eformimportrule`), the business layer and the WPF windows. **They are hidden in Release builds unless the database is OD HQ.** OD's 24.4 release is mainly the switch that turns them on, plus the built-in forms. The 24.4 manual itself said eForms weren't usable yet, pending a coming eClipboard version.
- **Classification note:** OD's public pages (0_eforms.html, featureeforms.html) present eForms as being only for eClipboard (and later eClipboard Web), both of which we cut. Keeping eForms as a desktop feature, filled in the office on a workstation, is **our decision**. It rests on the in-office fill path that already exists in our code (`FormPatientForms.butAddEForm_Click` → `FrmEFormFillEdit`).

**User-visible behavior**
- Setup > eForms (also Patient Forms > Setup > eForms) lists the office's eForm definitions. Add creates a form, either blank or as a copy of a built-in template: Patient Registration, Medical History, Dental History, HIPAA, Extraction Consent. A sixth template, Demo, is offered only in Debug builds (`EFormInternal.cs`). Editing an office copy never changes the built-in template.
- A definition is an ordered list of fields: labels, text boxes, dates, check boxes, radio-button groups, signature boxes, a medication list, and so on. Any field can have a condition: it shows only when a named parent field has a given value. Patients therefore see only the questions that apply to them ("dynamic fields").
- Many fields are linked to patient data (`DbLink`). When a form is created for a patient, those fields are pre-filled so the patient only confirms or corrects them.
- In the Patient Forms window, **Add eForm** asks for a definition, builds a filled-in copy for the patient (translated to the patient's language when translations exist), and opens it for in-office completion on the workstation. Saved forms are listed in Patient Forms. **Import** copies the answers into the patient record through the existing sheet-import review window.

**UI elements**
- `FrmEFormDefs` (Setup > eForms): grid of definitions, plus buttons Add, Duplicate, Import (from file), Export (to file), Setup.
- `FrmEFormSetup`: global spacing defaults.
- `FrmEFormDefEdit` plus one editor per field type (`FrmEFormLabelEdit`, `FrmEFormCheckBoxEdit`, `FrmEFormRadioButtonsEdit`, `FrmEFormDateFieldEdit`, `FrmEFormSigBoxEdit`, `FrmEFormMedicationListEdit`, `FrmEFormFieldPicker`).
- `FrmEFormFillEdit`: buttons Save, Print, Delete, Change Pat, Unlock Sig, Save to Images. **Hide the "To eClipboard" button.**
- `FormPatientForms`: Add eForm button, Setup > eForms menu entry, Import.

**Business rules and edge cases**
- Remove the `!ODBuild.IsDebug() && !PrefC.IsODHQ` gates in `FormOpenDental.cs` (around line 781), `FormPatientForms.cs` (lines 27 and 41) and `FormEServicesEClipboard.cs` (line 60). Remove the last one together with eClipboard rather than enabling it.
- A field whose condition isn't met is hidden and is not validated as required.
- In the existing desktop fill window, required fields are **not** enforced: a label there says they are checked only on eClipboard. With eClipboard cut, that leaves no place where they are checked. **Decision:** enforce required fields (only the visible ones) when Save is clicked in `FrmEFormFillEdit`, and remove that label.
- Signature boxes lock the form after signing; Unlock Signature clears the signature so the form can be edited again.
- The Import (sheet-import) review step is the only way to write answers into patient, medical or allergy records in 24.4. Per-field automatic import (`AutoImport`) arrives in 25.x and is out of scope here.
- **Decision:** the "Import Rules" menu (commented out in `FormPatientForms.cs`) stays hidden until 25.x parity.

**Permissions / security log**
- Opening eForm setup requires Setup. The main-menu path currently logs `SheetEdit` "EForms" and the Patient Forms path logs `Setup` "EForms". **Decision:** log `Setup` in both places.
- Editing a filled eForm requires `EFormEdit` (259, date-restricted). Deleting one requires `EFormDelete` (260). Imports are logged under `EFormEdit`.

**Preferences:** existing `EformsSpaceBelowEachField` (default 20) and `EformsSpaceToRightEachField` (default 10). Nothing new.

**Schema:** none new for 24.4. The eForm tables already exist in our code and match 25.3, except for the 25.x columns `eformfield.AutoImport`, `PrefillFromGuar`, `ValueLabelEnglish`, `PickListVisEnglish` and the matching `eformfielddef` columns. Those belong to the 25.x specs.

**Codebase touchpoints**
- `OpenDental/Main Modules/FormOpenDental.cs`, `OpenDental/Main Modules/FormOpenDentalMenus.cs`
- `OpenDental/Forms/FormPatientForms.cs`, `OpenDental/Forms/FormSheetImport.cs`
- `WpfControlsOD/Frms/FrmEFormDefs.xaml.cs`, `FrmEFormDefEdit.xaml.cs`, `FrmEFormFillEdit.xaml(.cs)`, `FrmEFormSetup.xaml.cs`, `FrmEFormPicker.xaml.cs`, and the per-field editors `FrmEForm*Edit.xaml.cs` and `FrmEFormFieldPicker.xaml.cs` (all in `WpfControlsOD/Frms/`)
- `OpenDental/Forms/FormEServicesEClipboard.cs` (the gate at line 60)
- `OpenDentBusiness/EForms/EFormFiller.cs`, `EFormImport.cs`, `EFormInternal.cs`, `OpenDentBusiness/Data Interface/EForms.cs`
- `OpenDentBusiness/Resources/EForms/*.xml`

---

### v24_4-account-payplan-sheet-select: choose the sheet used to print each payment plan

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Account Module); blog (payment plan sheets paragraph); DB docs 24.4 `payplan.SheetDefNum`, `payplantemplate.SheetDefNum`; baseline https://www.opendental.com/manual/paymentplansheets.html, https://www.opendental.com/manual/payplantemplates.html; https://opendental.com/help244/paymentplansheets.html and https://opendental.com/help244/payplantemplates.html *(gated, search summary)*.

**User-visible behavior**
- When the preference "Pay Plans use Sheets for printing" is on, the user can pick, per payment plan, which PaymentPlan-type sheet is used to print it.
- Payment plan templates can store a default sheet. A plan created from a template inherits that sheet.
- In 24.3 the printout always used the first custom PaymentPlan sheet (alphabetical), or the internal sheet when no custom one exists.

**UI elements**
- Payment Plan window (`FormPayPlanDynamic`, and the old-style `FormPayPlan`): new combo box **Sheet**. It is shown only when `PayPlansUseSheets` is on. Items: "Default" (value 0) plus every PaymentPlan sheet def (internal and custom).
- Pay Plan Template edit window (`FormPayPlanTemplateEdit`): new combo box **Sheet Definition**, shown only when `PayPlansUseSheets` is on.
- Print button: uses the selected sheet.

**Business rules and edge cases**
- `SheetDefNum = 0` means "use the old logic" (first custom, otherwise internal). The DB docs describe 0 the same way.
- Only sheet defs whose `SheetType` is PaymentPlan may be chosen.
- If the stored sheet def was deleted, fall back to the default logic silently. **Decision:** when a sheet def is deleted (`SheetDefs.DeleteObject`), reset `payplan.SheetDefNum` and `payplantemplate.SheetDefNum` rows that reference it to 0.
- When a plan is created from a template, copy the template's `SheetDefNum`.
- The remote-print path (`PrintRemoteRequestL`) must honor the plan's sheet too.
- The signature-box check (`SheetDefs.GetInternalOrCustom(...).SheetFieldDefs`, `FormPayPlan.cs` ~1143 and `FormPayPlanDynamic.cs` ~1309) must look at the selected sheet, not the default one. Per the 24.3 manual, a sheet that contains a signature box opens a preview for signing before it prints.

**Permissions / security log:** editing a plan uses the existing `PayPlanEdit` permission. Log the sheet change with the plan's other edits ("Sheet changed from X to Y"). Template edits fall under Setup.

**Preferences:** none new; reuses `PayPlansUseSheets`.

**Schema:** `payplan.SheetDefNum bigint(20) NOT NULL` and `payplantemplate.SheetDefNum bigint(20) NOT NULL`, both FKs to `sheetdef`, default 0, no index.

**Codebase touchpoints**
- `OpenDental/Forms/FormPayPlanDynamic.cs` (`butPrint_Click` ~1093, `PayPlanToSheet` ~1412), `OpenDental/Forms/FormPayPlan.cs` (~724, ~1015)
- `OpenDental/Forms/FormPayPlanTemplateEdit.cs`, `OpenDental/Logic/PrintRemoteRequestL.cs`
- `OpenDentBusiness/TableTypes/PayPlan.cs`, `OpenDentBusiness/TableTypes/PayPlanTemplate.cs`
- `OpenDentBusiness/Data Interface/SheetDefs.cs` (`GetInternalOrCustom`), `OpenDentBusiness/Data Interface/PayPlanTemplates.cs`

---

### v24_4-account-salestax-proc: Add Proc > Sales Tax adds one sales-tax procedure

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Account Module, sales-tax item); blog (sales tax procedure paragraph); baseline https://www.opendental.com/manual/salestax.html and https://www.opendental.com/manual/quickprocs.html; https://opendental.com/help244/salestax.html and https://opendental.com/help254/quickprocs.html *(gated, search summary)*.

**User-visible behavior**
- 24.3 posts sales tax as adjustments: manually (Adjustment drop-down > Apply Sales Tax on highlighted completed procedures) or automatically when a taxed procedure is set complete. 24.4 adds a second method: one procedure line carrying the tax for any number of selected procedures.
- In the Account module, the user highlights procedures and opens the quick-procedure drop-down. In our 24.3 code and the 24.3 manual this button is called **Quick Procs**; OD's 24.4 blog calls it "Add Proc". A new item **Sales Tax** adds one completed procedure, using a configured tax code (typically D9985). Its fee is the tax on the highlighted procedures. The blog says the amount comes from the Sales Tax Percentage preference and the selected procedures.
- OD recommends that an office use either this method or tax adjustments, not both.

**UI elements**
- Account module toolbar, Quick Procs drop-down (`contextMenuQuickProcs` in `ControlAccount`): new first item "Sales Tax". It is shown only when the new preference holds a valid code. **Decision:** keep our button label "Quick Procs" unless the office's users expect OD's newer label.
- Preferences, Account > Adjustments tab (`UserControlAccountAdjustments`), next to the existing sales-tax settings: new text box with a code picker, **Sales Tax Procedure Code**.

**Business rules and edge cases**
- Fee = Σ `ClaimProcs.ComputeSalesTax(proc, claimProcs, isEstimate:false)` over the highlighted procedures. That is the same function the adjustment method uses: for each procedure, (ProcFee − write-off) × `SalesTaxPercentage` / 100, rounded to 2 places. This matches the 24.3 manual's rule that PPO write-offs are taken off before tax is applied. Summing the per-procedure amounts (instead of applying the percentage once to the summed fees) means the two methods always give the same total.
- **Decision:** count only completed procedures (ProcStatus C). Ignore highlighted rows that aren't procedures.
- If nothing is highlighted, or the total is 0, show a message and add nothing.
- The new procedure gets: status Complete, ProcDate today, provider = `SalesTaxDefaultProvider` (fall back to the patient's primary provider), clinic = the patient's clinic (or the current clinic when clinics are on). It runs the normal "procedure set complete" path: insurance estimates, and the `ProcComplCreate` check.
- The tax procedure itself must never be taxed again. Skip it when automatic tax adjustments run (`SalesTaxDoAutomate`), even if its code has `IsTaxed` set.
- If the configured code doesn't exist, hide the menu item. Validate the code when the preference is saved.
- Show a warning if any highlighted procedure already has a sales-tax adjustment (type `SalesTaxAdjustmentType`). This avoids taxing twice.

**Permissions / security log:** like every quick-proc item, this needs `AccountProcsQuickAdd` (checked in `menuItemQuickProcs_Click` ~1739) plus `ProcComplCreate` (date-restricted) for the new procedure. Write the usual `AccountProcsQuickAdd` audit entry with a note that the procedure came from Sales Tax.

**Preferences (new):** `SalesTaxProcCode` (string, default empty). This is our proposed name; **Verify** OD's key in the office database.

**Schema:** none.

**Codebase touchpoints**
- `OpenDental/Main Modules/ControlAccount.cs` (`contextMenuQuickProcs_Popup` ~402, `menuItemQuickProcs_Click` ~1738)
- `OpenDental/User Controls/Preferences/UserControlAccountAdjustments.cs`
- `OpenDentBusiness/TableTypes/Pref.cs` (next to `SalesTaxPercentage`, `SalesTaxDefaultProvider`)
- `OpenDentBusiness/Data Interface/Adjustments.cs` (`CreateAdjustmentForSalesTax` ~433), `OpenDentBusiness/Data Interface/Procedures.cs` (`SalesTaxDoAutomate` check ~5603), `OpenDentBusiness/Data Interface/ClaimProcs.cs` (`ComputeSalesTax` ~1702)

---

### v24_4-appts-copy-apptview: Copy View button for appointment views

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Appointments Module); blog (appointment views paragraph); baseline https://www.opendental.com/manual/appointmentviewsetup.html and https://www.opendental.com/manual/appointmentvieweditwindow.html.

**User-visible behavior:** In Setup > Appointments > Appointment Views, a new **Copy View** button duplicates the selected view, then opens the copy in the edit window. The copy can then be pointed at other operatories or another clinic. It keeps all settings and display fields of the original.

**UI elements**
- `FormApptViews`: button "Copy View", enabled only when a view is selected.
- After copying, the existing `FormApptViewEdit` opens on the new view.

**Business rules and edge cases**
- Copy every `apptview` column except the primary key. These columns include the clinic, rows per increment, the scheduled-provider filters, stacking behavior, scroll start, bubble setting, minimum width and the waiting-room setting (`WaitingRmName`). Also copy the 25.x column `ShowMirroredAppts` once it exists.
  - Description becomes "<original> (copy)". **Decision:** this suffix is ours.
  - `ItemOrder` puts the copy directly after the original; renumber the views that follow.
- Copy every `apptviewitem` row of the source (new `ApptViewNum`). That covers operatories, providers, row elements, upper-right/lower-right elements, appointment field and patient field elements, and the `IsMobile` element rows.
- Do **not** copy `userodapptview` rows (users' remembered views).
- Cancelling the edit window after copying deletes the copy. This matches how Add behaves.
- With clinics on, the copy starts on the source view's clinic. If the user changes the clinic, operatories that don't belong to the new clinic must be removed in the edit window (existing validation).
- The F-key assignment follows from the view's position in the list.

**Permissions / security log:** Setup (the window already requires it). Log under Setup: "Appointment view '<name>' copied to '<new name>'".

**Preferences:** none. **Schema:** none.

**Codebase touchpoints:** `OpenDental/Forms/FormApptViews.cs` (+ `.designer.cs`), `OpenDental/Forms/FormApptViewEdit.cs`, `OpenDentBusiness/Data Interface/ApptViews.cs`, `OpenDentBusiness/Data Interface/ApptViewItems.cs`.

---

### v24_4-chart-perio-implant-removed: hide the implant marker once the implant is removed

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Chart Module); blog (perio chart paragraph: an implant counts as removed once its procedure graphics are hidden); baseline https://www.opendental.com/manual/perio.html (a tooth is flagged as an implant, e.g. "17i", when a C/EC/EO procedure on it uses a code painted as Implant).

**User-visible behavior:** The Perio Chart marks implant teeth with an "i" after the tooth number. Once the implant procedure is marked removed (its tooth-chart graphics are hidden), that marker no longer shows. The tooth also goes back to being treated as missing.

**Business rules and edge cases**
- `ContrPerio.IsImplant(int)` today returns true when the tooth has any procedure with status C, EC or EO whose code has `PaintType = Implant`. Change it to skip procedures whose `procedurelog.HideGraphics` is true.
- If the tooth has a newer, non-hidden implant procedure, it is still an implant.
- `FormPerio` uses `IsImplant` with the preference `PerioTreatImplantsAsNotMissing` to decide whether a missing tooth is skipped. A removed implant must therefore be skipped again when `PerioSkipMissingTeeth` is on.
- The cached procedure list (`_listProcedures`) must be refreshed when the Perio Chart opens, so a removal made in the same session is seen.

**Permissions / security log:** none new. **Preferences / schema:** none.

**Codebase touchpoints:** `OpenDental/User Controls/ContrPerio.cs` (`IsImplant` ~397, tooth labels ~826), `OpenDental/Forms/FormPerio.cs` (~322), `OpenDental/Forms/FormPerioGraphical.cs` (check for implant drawing).

---

### v24_4-chart-perio-attached-gingiva: auto-calculated Attached Gingiva row

- **Category:** desktop · **Size:** M · **Depends on:** none (touches the same grid as `v24_4-chart-perio-recession-term`)
- **Sources:** release notes (Chart Module, attached gingiva item); blog (perio chart paragraph: calculated from probing and MGJ); DB docs 24.4 `PerioSequenceType.AttGing = 8` (calculated, never saved); https://opendental.com/help254/perio.html *(gated, search summary: row "auto Att Ging", read-only, MGJ minus probing depth, also called gingival width)*; baseline https://www.opendental.com/manual/perio.html.

**User-visible behavior:** The Perio Chart gets a new read-only row, **auto Att Ging**, for the selected exam. It shows the width of attached gingiva at each site, calculated from data already charted.

**UI elements:** a new row in `ContrPerio`, next to the MGJ row. It appears in each arch section where MGJ can be charted: maxillary facial, and mandibular facial and lingual. It uses the "auto" styling of the auto CAL row. It's also included on the printed perio chart.

**Business rules and edge cases**
- Value per site = MGJ − probing depth, for the current exam only.
- Blank if either value is missing at that site. It is never stored (SequenceType 8 must never be written to `periomeasure`).
- Values of 0 or below show as calculated (they mean no attached gingiva).
- **Decision:** no "Red if" threshold in 24.4.
- Keyboard navigation and data entry skip this row, the same as auto CAL.
- Recalculate whenever a probing or MGJ cell changes.
- Old exams aren't shown in this row; the auto CAL row behaves the same way.

**Permissions / security log:** none (display only). **Preferences:** none.

**Schema:** enum value only: `PerioSequenceType.AttGing = 8` in `OpenDentBusiness/TableTypes/PerioMeasure.cs`. **Verify:** the office database has no `periomeasure` rows with SequenceType 8.

**Codebase touchpoints:** `OpenDental/User Controls/ContrPerio.cs` (row layout array `_perioSequenceTypeArray` ~140–160, row labels ~800–830, CAL calculation), `OpenDentBusiness/TableTypes/PerioMeasure.cs`, `OpenDental/Forms/FormPerio.cs` (print), `OpenDentBusiness/Data Interface/PerioMeasures.cs`.

---

### v24_4-chart-perio-recession-term: "Recession" instead of "Gingival Margin"

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Chart Module, Recession wording item); blog (the option lives in Perio Setup); baseline https://www.opendental.com/manual/perio.html (Ging Marg+ check box or Ctrl+number enter coronal values); https://opendental.com/help254/periosetup.html and https://opendental.com/help254/perio.html *(gated, search summary: when the Recession option is on, the row shows "Recession" unless hyperplasia is present on the exam, and then it reverts to "Ging Marg")*.

**User-visible behavior:** Perio Setup gets an option that sets the label of the gingival-margin row. With it off (default, same as 24.3), the row reads "Ging Marg". With it on, the row reads "Recession", except that it reverts to "Ging Marg" when the exam has hyperplasia: any positive (coronal) gingival-margin value, entered with Ging Marg+ or Ctrl+number.

**UI elements**
- `FormPerioSetup`: new check box **Use the term "Recession" (reverts to Gingival Margin when hyperplasia is charted)**.
- Labels that follow the setting:
  - the row label in `ContrPerio` (currently `"Ging Marg"`)
  - the count label next to `textCountGing` in `FormPerio`
  - the "Ging Marg +" check box text
  - the printed chart
  - the graphical perio setup label ("Gingival Margin")

**Business rules:** stored values don't change; gingival margin still means implied-negative recession. Only labels change. Hyperplasia is checked per displayed exam.

**Permissions / security log:** Perio Setup is under Setup. Log "Perio Setup: Recession term on/off" under Setup.

**Preferences (new):** `PerioUseRecessionTerm` (bool, default false). Proposed name; **Verify** OD's key.

**Schema:** none.

**Codebase touchpoints:** `OpenDental/Forms/FormPerioSetup.cs` (+ `.Designer.cs`), `OpenDental/User Controls/ContrPerio.cs` (~810), `OpenDental/Forms/FormPerio.cs` / `FormPerio.designer.cs` (label10 "Ging Marg", `checkGingMarg`), `OpenDental/Forms/FormPerioGraphicalSetup.designer.cs` (label4 "Gingival Margin"), `OpenDentBusiness/TableTypes/Pref.cs`.

---

### v24_4-chart-chart-letters: Chart Letters

- **Category:** desktop · **Size:** XL · **Depends on:** none among 24.4 items. It uses the existing Letter Merge templates and an editor for Word documents.
- **Sources:** release notes (Chart Module, chart letters item); blog (chart letter paragraph: aimed at specialties that want extra protection for their notes; built on Word letter merge); https://www.opendental.com/site/0_notes.html ("Chart Letters" section); DB docs 24.4 `document.ChartLetterStatus`, `UserNum`, `ChartLetterHash` and the changed summaries of `document.Signature`, `document.ProvNum` and `document.DocCategory`; https://opendental.com/help244/chartletterproperties.html *(gated, search summary)*; baseline https://www.opendental.com/manual/lettermerge.html.

**User-visible behavior**
- A Chart Letter is a long clinical note, mainly for specialties such as oral surgery, written as a Word document from a Letter Merge template. It lives in the Chart module's Progress Notes, not in the Imaging module.
- It can be sent to a referring dentist.
- Every edit is kept. The program warns when the file was changed outside the program.
- A provider can sign the letter. The signature becomes invalid if the content changes afterwards.

**UI elements**
- **Create.** From the Chart module, choose **New Chart Letter**. **Decision on placement:** in the Progress Notes right-click menu and in the Letter drop-down. Pick a Letter Merge template; the program merges the patient fields and opens the new document for editing.
- **Progress Notes grid.** Each chart letter is a row, for example "Chart Letter – <description>", with date, provider and user. Double-click opens a **read-only** copy.
- **Right-click on a chart-letter row:**
  - **Edit Document** opens the letter for editing. **Decision:** the editor works on a working copy, never on the stored file, so the bytes of every stored version stay exactly as they were hashed. The copy lives in a folder of its own per edit session under the user's local application data (`%LOCALAPPDATA%\OpenDental\ChartLetters\<DocNum>_<timestamp>`), not in the temporary folder, so temporary-file clean-up cannot remove a save that was never stored. See **Edit tracking** for how its saves are stored.
  - **Properties** opens the Chart Letter Properties window.
  - **Delete** is a soft delete.
- **Chart Letter Properties window** shows:
  - date/time and description (both editable). The 24.4 DB docs say a document date may not be moved into the future without a permission. For images, `FrmDocInfo` (~249) already requires `ImageDelete` to do that. Image permissions don't apply to chart letters, so **Decision:** a chart letter can never be future-dated.
  - provider
  - the user who last changed the content
  - the file hash and the stored hash, with **Match / No Match**
  - the signature box, for the provider to sign
  - image category combo + **Save**, which stores a PDF copy in the Imaging module
- Show tab, **Audit** check box: also lists deleted letters and older versions.

**Business rules and edge cases**
- Storage is a `document` row with `ChartLetterStatus > 0`, `DocCategory = 0` (so the Imaging tree never shows it), `ProvNum` = provider and `UserNum` = the user who made the change. Its file lives in the patient's image folder (or in the database when images are stored there).
- **Versioning.** Like a procedure note, a chart letter is never edited or deleted in place. Each time an edit is saved, a **new** document row is written with a copy of the file, and the previous row is kept as history. Delete only flags the row; it stays visible with Audit checked.
- **Hash.** `ChartLetterHash` holds the 16-byte hash of the Word file's bytes as 32 hex characters, with no separators. The 16-byte length matches MD5. Properties shows Match when the file on disk still produces the stored hash. Otherwise it shows No Match, which means the file was edited outside the program.
- **Edit tracking.** Edit Document watches the working copy (FileSystemWatcher) while the program runs. On each save it writes a new version with a new hash. Next to the working copy, a small session file records the patient, the version the copy is based on and that version's hash; it is rewritten after every stored version and held open while the session lives, which marks the session as alive.
  - **Saves after the program closed (decision).** If the program closes or ends unexpectedly while the editor still has the letter open, later saves go only into the working copy. They are not lost: when the Chart module shows the patient, and before Edit Document opens one of the patient's letters, the program looks for working folders of that patient's letters whose session is no longer alive. A working copy whose hash differs from the one in its session file (and from every version stored after that base) holds a save that was never stored. The user is told and asked whether to store it: as a new version when its base is still the current version, otherwise (the letter got a newer version or was deleted since) as a separate chart letter with the base's date, description and provider plus "(recovered edit)", so neither edit hides the other. Both are logged. A declined edit's folder is moved to `NotStored` under the working folder, never deleted; a user without the edit permission is told and the folder is kept. Working copies with nothing left to store are removed, or, while an editor still has them open, watched again for the rest of the run.
  - **Closing the program.** When the main window closes, saves still waiting for the editor to go quiet are stored, the working folders of letters no longer open in an editor are removed, and the user is warned about letters still open in an editor: saves made in them from then on are offered for storing the next time the patient's chart is opened on that computer. The warning cannot stop the close, because the main window's own closing code runs first.
  - Edits made directly to a stored file, outside Edit Document, still show No Match.
- **Signature.** It is bound to the bytes of the Word file, much as image signatures are bound to image bytes. The 24.4 DB docs warn that OD may use a slightly different algorithm for chart letters, so signatures on letters imported from the office database might not validate under our algorithm. **Verify** with a signed letter from the office copy. Any new version invalidates the signature, so the letter must be signed again.
- Changing date/time, description or signature does **not** change `UserNum`. Only content edits do.
- The Audit Trail records each create, edit, delete and sign (see below).
- **Decision (our status numbers):** `None=0, Current=1, Superseded=2, Deleted=3`. OD's numbers aren't public. **Verify** with check 5 above and adopt OD's numbers if they differ.
- **Decision (Word dependency):** OD needs Microsoft Word (`#if DISABLE_MICROSOFT_OFFICE` in `FormLetterMerges`). For the open-source fork, merge fields with the OpenXML SDK (MIT) into a .docx file and open it with whatever program is registered for .docx, which can be Word or LibreOffice. Keep Word interop as an optional path. Hash tracking works with any editor.
- The help summary says chart letters aren't limited by OD permissions. **Decision:** reuse procedure-note permissions instead. Creating a letter needs `ProcComplCreate`. Editing or deleting one needs `ProcCompleteNote`, with that permission's date limit applied to the letter's date. If we'd rather match OD exactly, gate nothing. Record the choice before implementation.

**Permissions / security log:** use a new log category, or reuse `ImageEdit`/`ImageCreate` with the description prefix "Chart Letter". Log entries: "Chart letter created", "Chart letter edited (version n)", "Chart letter deleted", "Chart letter signed", "Chart letter edited outside the program (hash mismatch detected)", and, for a save stored after its edit session ended, "Chart letter edit saved after the program stopped tracking it, recovered as version n" or "... recovered as a separate letter because version n is no longer the current version".

**Preferences:** none required. Optionally `ChartLetterDefaultCategory` for the PDF copy.

**Schema:** `document.ChartLetterStatus tinyint(4) NOT NULL` (default 0), `document.UserNum bigint(20) NOT NULL` (FK userod, 0 for normal documents) and `document.ChartLetterHash varchar(255) NOT NULL` (empty for normal documents). No new index.

**Codebase touchpoints**
- `OpenDentBusiness/TableTypes/Document.cs`, `OpenDentBusiness/Data Interface/Documents.cs`, `OpenDentBusiness/Imaging/ImageStore.cs`
- `OpenDentBusiness/Db Multi Table/ChartModules.cs` (`GetProgNotes` adds chart-letter rows, column `DocNum`)
- `OpenDental/Main Modules/ControlChart.cs` (progress-notes context menu, double-click, `checkAudit`)
- `OpenDental/Main Modules/ControlImages.cs` and `OpenDental/Forms/FormImageFloat.cs` (exclude chart letters)
- `OpenDental/Forms/FormLetterMerges.cs`, `OpenDental/Forms/FormLetterMergeEdit.cs`, `OpenDentBusiness/Data Interface/LetterMerges.cs`, `OpenDentBusiness/Misc/LetterMergesQueries.cs`
- `WpfControlsOD/Frms/FrmDocSign.xaml.cs` (signature)
- New window `FrmChartLetterProperties`

---

### v24_4-manage-accounting-export: export Transaction History as .csv or .txt

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Manage Module); baseline https://www.opendental.com/manual/transactionhistory.html (24.3: Export saves a text or Excel file).

**User-visible behavior:** In Manage > Accounting, double-click an account to open Transaction History. Its export now offers comma-delimited **.csv** and tab-delimited **.txt**, as the Chart of Accounts window already does.

**UI elements:** `FormJournal` toolbar. Replace the single "Export" button with **Export .txt** and **Export .csv**, the same way `FormAccounting` does it, or keep one button with a drop-down.

**Business rules and edge cases**
- Export every row currently in the grid (respecting the date, amount and text filters), with the grid's column headers.
- Default file name = grid title (account name and type). The initial folder is the `ExportPath` preference, created if it's missing.
- Quote fields that contain the delimiter, quotes or new lines (CSV rules). Tab-delimited output replaces embedded tabs with spaces.
- An empty grid shows "Nothing to export".
- **Decision:** reuse the export routine in `FormAccounting.Export_Click(string delimiter)`; move it into a shared helper.

**Permissions / security log:** the existing Accounting permission. **Decision:** no log entry; 24.3 doesn't log exports here.

**Preferences / schema:** none.

**Codebase touchpoints:** `OpenDental/Forms/FormJournal.cs` (toolbar ~68, `Export_Click` ~255), `OpenDental/Forms/FormAccounting.cs` (`Export_Click` ~204).

---

### v24_4-manage-era-mismatch-match: match an ERA to a claim when fee or date of service differ

- **Category:** desktop · **Size:** S (verification only) · **Depends on:** none · **Already in baseline since 24.3.43**
- **Sources:** release notes (Manage Module); baseline https://www.opendental.com/manual/claimseramatching.html (24.3 manual: these errors blocked matching and required detaching).

**User-visible behavior:** In ERA 835 Claim Select, a user can choose a claim whose total fee or date of service doesn't match the ERA. They get an OK/Cancel warning instead of a hard stop, and processing continues.

**Status:** `FormEtrans835ClaimSelect.butOK_Click` already shows "Claim fee on claim does not match ERA. Continue anyway?" and "Date of service on claim does not match service date range on ERA. Continue anyway?". This came in with the 24.3.43 commit (`9c75e394b`).

**Remaining work**
- Add the expected value to each prompt (the 24.3 messages showed "Expected …"). This helps the user decide.
- Test that `FormEtrans835Edit` and claim payment entry handle a mismatched claim. In particular: procedures that don't match show in red, and unmatched ERA lines need manual entry.

**Permissions / security log:** none new. **Preferences / schema:** none.

**Codebase touchpoints:** `OpenDental/Forms/FormEtrans835ClaimSelect.cs` (~376–410), `OpenDental/Forms/FormEtrans835Edit.cs`, `OpenDentBusiness/X12/X835.cs`.

---

### v24_4-tools-querymonitor-filters: filter groups in the Query Monitor

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Miscellaneous); DB docs 24.4 table `queryfilter`; https://opendental.com/help244/querymonitor.html and https://opendental.com/help244/querymonitorfilter.html *(gated, search summary)*; baseline https://www.opendental.com/manual/querymonitor.html.

**User-visible behavior:** Users can define filters so that noisy queries (for example signal polling) don't show in the Query Monitor feed or its log. Filters are kept in named groups, and the user picks which group is active.

**UI elements**
- `FormQueryMonitor`: new **Filter** button that opens the Query Filters window, plus a label showing the active group.
- New window **Query Filters**:
  - **Group** combo, for selecting, renaming or creating a group
  - grid **Filters**, one column: Filter Text
  - buttons **Add**, **Delete**, **Import**, **Export**, **Save**, **Close**
  - double-click a row to edit its text in a small edit window
- **Import** reads a file containing a group name and its filters and creates a new group (with a numeric suffix if the name already exists). **Export** writes the selected group's filters to a file.

**Business rules and edge cases**
- A query is hidden from the feed **and** left out of the saved log when its command text contains any FilterText of the active group. **Decision:** the match is case-insensitive.
- Empty filter text isn't allowed. Remove duplicates within a group on save.
- **Decision:** the active group is remembered per user. Use a `userodpref` row with a new `UserOdFkeyType`, or a local setting. With no active group, nothing is filtered.
- Renaming a group updates `GroupName` on all its rows. A group exists only while it has at least one row.
- **Decision:** export as a simple JSON or text file, one filter per line with the group name in a header line.
- Middle Tier: the 24.3 manual says the Query Monitor isn't available over Middle Tier. In our code the same window instead runs as a payload monitor (`FormQueryMonitor.IsPayloadMonitor`, true when the client is ClientMT), and it lists payloads rather than SQL. **Decision:** in payload mode, hide the Filter button and apply no filters.
- Filters are applied before queries are added to the grid (`AddQueriesToGrid`), so they don't slow the feed.

**Permissions / security log:** the window opens from Help > Query Monitor with the existing `QueryMonitor` permission (the manual calls it Query Monitor View). Log filter changes under `QueryMonitor`: "Query filter group '<g>' changed".

**Preferences:** none (see the decision on the active group).

**Schema:** new table `queryfilter (QueryFilterNum bigint PK auto, GroupName varchar(255) NOT NULL, FilterText varchar(255) NOT NULL)`. No other indexes. This needs a new TableType `QueryFilter`, CRUD and `QueryFilters` data interface.

**Codebase touchpoints:** `OpenDental/Forms/FormQueryMonitor.cs` (`AddQueriesToGrid` ~170, `ButLog_Click` ~233), `DataConnectionBase/QueryMonitor.cs`, `OpenDental/Main Modules/FormOpenDental.cs` (`MenuItemQueryMonitor_Click` ~7383), new `OpenDentBusiness/TableTypes/QueryFilter.cs`, new `OpenDentBusiness/Data Interface/QueryFilters.cs`, new form `FrmQueryFilters`.

---

### v24_4-imaging-signature-permissions: ImageSignatureCreate and SignedImageEdit

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Miscellaneous); blog (imaging permissions paragraph); DB docs 24.4 `EnumPermType` (new 264, 265, and new wording for 44 ImageDelete and 89 ImageEdit); baseline https://www.opendental.com/manual/images.html (Sign button) and https://www.opendental.com/manual/audittrailpermissions.html.

**User-visible behavior:** Two new security permissions control who may sign images and who may change signed images.
- **ImageSignatureCreate (264):** add a note and signature to an image that has no signature yet.
- **SignedImageEdit (265):** change or remove the note and signature of an already-signed image. It is also needed **in addition to** ImageEdit to flip, rotate, crop, resize or edit Item Info of a signed image, and **in addition to** ImageDelete to delete a signed image.

**UI elements**
- Setup > Security, permission tree: both new permissions, placed under the Imaging group next to ImageEdit and ImageDelete.
- Imaging module:
  - The Sign button (`FrmDocSign`) checks ImageSignatureCreate when the document has no signature, and SignedImageEdit when it already has one.
  - The rotate, flip, crop and size tools, Item Info (`FrmDocInfo`) and Delete add the SignedImageEdit check when the document has a signature.
  - A clear message names the missing permission.

**Business rules and edge cases**
- "Signed" means `document.Signature` isn't empty.
- Mounts: a mount whose items include signed images needs SignedImageEdit to delete, or to edit those items.
- These permissions have no date restriction; ImageEdit and ImageDelete keep theirs.
- Since 24.3.11, editing a signed image invalidates the signature. That rule stays.
- **Upgrade rule (decision):** to keep current behavior after upgrading, the conversion grants ImageSignatureCreate and SignedImageEdit to every user group that has ImageEdit today. This is how OD usually seeds new permissions; **Verify** with check 7 above.
- Chart letters (`v24_4-chart-chart-letters`) are not images, so these permissions don't apply to them.

**Permissions / security log:** new permission types 264 and 265. Log "Signature added" under ImageSignatureCreate. Log "Signature changed/removed", and "Signed image edited/deleted", under SignedImageEdit. Both appear in the Audit Trail permission filter.

**Preferences:** none. **Schema:** enum values only (`grouppermission.PermType`, `securitylog.PermType`).

**Codebase touchpoints**
- `OpenDentBusiness/TableTypes/GroupPermission.cs` (`EnumPermType`, after `SuperFamilyDisband` = 263)
- `OpenDentBusiness/Data Interface/GroupPermissions.cs` (descriptions, date-restriction lists ~744, ~796)
- `OpenDental/User Controls/UserControlSecurityTree.cs`
- `OpenDental/Main Modules/ControlImages.cs` (Sign ~2988, delete ~2375, edits ~1222)
- `OpenDental/User Controls/ControlImageDisplay.cs`
- `WpfControlsOD/Frms/FrmDocSign.xaml.cs`, `WpfControlsOD/Frms/FrmDocInfo.xaml.cs` (~59, ~214–257)
- `OpenDental/Forms/FormImageFloat.cs`
- `OpenDentBusiness/Misc/ConvertDatabases8.cs` (grant script)

---

### v24_4-setup-fee-effective-date: effective dates for fees

- **Category:** desktop · **Size:** XL · **Depends on:** none (`v24_4-setup-feesched-notes` is often used alongside it)
- **Sources:** release notes (Miscellaneous, fee effective-date item); blog (fee schedule effective dates paragraph); DB docs 24.4 `fee.DateEffective`; https://www.opendental.com/site/0_feeschedtools.html (public, current: Copy To, Import and Increase by % all accept an effective date); https://opendental.com/help244/feescheduleincrease.html and https://opendental.com/help244/feesimport.html *(gated, search summary)*; baseline https://www.opendental.com/manual/feescheduletools.html and https://www.opendental.com/manual/feeschedulelogic.html.

**User-visible behavior**
- A fee can have a future start date. Offices can load next year's fees now (by import, copy or percentage increase) and have them take effect automatically on that date.
- Once the date arrives, new procedures get the new fee.
- Screens without a date filter show the fee in effect **today** and never show future fees.

**UI elements (Fee Tools, `FormFeeSchedTools`)**
- **Copy To:** new **Date Effective** box. The copied fees get this date; blank means `0001-01-01`, always effective.
- **Import:** new **Date Effective** box. Imported fees get this date. A blank box means today; this comes only from the gated search summary, and no public page says it. **Verify**, and if it can't be confirmed, treat blank as `0001-01-01`, the same as Copy To.
- **Increase by %:**
  - **Date Effective** under Select Fees: the as-of date of the fees used as the base. Enter today for the fees in effect now.
  - **New Effective Date:** the start date of the resulting fees. Blank means immediately.
- **Global Updates** ("Update fees for all patients"): refreshes treatment-planned procedure fees and estimates to the fees in effect today.
- **Procedure Codes window** (`FormProcCodes`): fee columns show the fee in effect today. **Decision:** add an optional **Fees as of** date picker to view or edit fees on another date. When it isn't today, typing in a cell creates or updates the fee row for that date.

**Business rules and edge cases**
- **Lookup rule.** For (fee schedule, code, clinic, provider) and an as-of date D, the fee is the row with the greatest `DateEffective` ≤ D. `0001-01-01` counts as "always". The existing clinic/provider override order is unchanged: provider + clinic, then clinic, then the schedule's base fee. The date filter applies at each level.
- **As-of date:** today for every screen without a date filter, and for charting or treatment-planning a procedure. **Decision:** when a procedure is set complete and fees are refreshed (existing preference behavior), use the procedure date.
- Several rows may now exist for one (schedule, code, clinic, provider), differing only by `DateEffective`. Two rows with the same key **and** date aren't allowed.
- Deleting a fee from the grid deletes only the row shown (the current one).
- **Clear** (Fee Tools): the public Fee Tools page says only that it erases the schedule's current fees. **Decision:** remove all rows for the selected schedule/clinic/provider, including past and future-dated ones, after a confirmation that says future-dated fees are removed too.
- Export writes the fees in effect today. **Decision:** add an "include future fees" option that adds a DateEffective column. Import accepts both formats.
- **Fee schedule groups:** syncing fees across a group must copy `DateEffective`.
- **Global fee schedules** (`feesched.IsGlobal`): `ClinicNum` and `ProvNum` must be 0 on every row (wording tightened in the 24.4 DB docs).
- **Cache:** `FeeCache` must load all dated rows and resolve by date. Most lookups use today, so build a "current" index for speed.
- Reports or estimates that ask for a fee "on date X" (for example historical write-off estimates) should pass that date. Everything else uses today.
- Upgrade: existing rows get `0001-01-01`, so nothing changes until a dated fee is entered.

**Permissions / security log:** `FeeSchedEdit` for Fee Tools, `ProcFeeEdit` for grid edits (existing). The log text includes the effective date, e.g. "Fee changed for D1110 in <sched>: 85.00 → 90.00, effective 2026-01-01".

**Preferences:** none.

**Schema:** `fee.DateEffective date NOT NULL DEFAULT '0001-01-01'` and index `FeeSchedCodeClinicProvDate (FeeSched,CodeNum,ClinicNum,ProvNum,DateEffective)`. The old `FeeSchedCodeClinicProv` index stays.

**Codebase touchpoints**
- `OpenDentBusiness/TableTypes/Fee.cs`
- `OpenDentBusiness/Data Interface/Fees.cs` (`GetFee` ~72, `GetFeeFromDb` ~84, `GetListFromObjects` ~212, `GetAmount` ~451, `IncreaseNew` ~661, `SynchList` ~415)
- `OpenDentBusiness/Cache/FeeCache.cs`, `OpenDentBusiness/Data Interface/FeeSchedGroups.cs`
- `OpenDental/Forms/FormFeeSchedTools.cs` (+ designer)
- `OpenDental/Forms/FormProcCodes2.cs`
- `OpenDentBusiness/Data Interface/Procedures.cs` (fee assignment), `OpenDentBusiness/Data Interface/ClaimProcs.cs` (estimates)
- `OpenDental/Forms/FormFeeSchedPickRemote.cs` (Canada import). It downloads fee files from OD's website or web service, so the decoupling will probably cut it. Change it only if it is kept.

---

### v24_4-setup-feesched-notes: notes on fee schedules

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Miscellaneous, fee schedule notes item); blog (fee schedule notes paragraph: notes explain fee changes, and a right-click on a fee column in Procedure Codes shows them); DB docs 24.4 table `feeschednote`; https://opendental.com/help244/feeschedulenotes.html *(gated, search summary: opened from Edit Fee Schedule > Notes; Add / double-click to edit; clinic filter shown only when clinics are on)*.

**User-visible behavior:** Each fee schedule can carry dated notes, optionally tied to one or more clinics. Offices use them to record why and when fees changed.

**UI elements**
- `FrmFeeSchedEdit` (Edit Fee Schedule): new **Notes** button, available for an existing schedule.
- New window **Fee Schedule Notes**:
  - **Clinic** filter combo, shown only when clinics are on
  - grid with columns Date, Clinics, Note (first line or truncated), Entered By
  - buttons **Add** and **Close**; double-click a row to edit
- New window **Fee Schedule Note Edit**:
  - **Date**: defaults to today and can be changed
  - **Clinics**: multi-select, clinics only
  - **Note**: multi-line, no length limit
  - buttons **Save**, **Delete**, **Cancel**
- Procedure Codes window (`FormProcCodes`): right-click a fee column header (or cell) and choose **View Fee Schedule Notes** to open the notes, read-only, for that column's schedule and clinic.

**Business rules and edge cases**
- `ClinicNums` is a comma-separated list. Empty means all clinics.
- With the clinic filter set, show notes whose list contains that clinic, plus notes for all clinics.
- Sort the grid by `DateEntry` descending.
- `SecUserNumEntry` and `SecDateEntry` are set on insert and can't be edited. `DateEntry` is the user's date.
- Deleting a fee schedule deletes its notes (or the delete is blocked; follow the existing rule for deleting a schedule that has fees).

**Permissions / security log:** `FeeSchedEdit` to add, edit or delete. Log "Fee schedule note added/edited/deleted for <sched>" under `FeeSchedEdit`. Viewing needs no permission.

**Preferences:** none.

**Schema:** new table `feeschednote`: FeeSchedNoteNum PK, FeeSchedNum bigint (FK, indexed), ClinicNums text, Note text, DateEntry date default '0001-01-01', SecUserNumEntry bigint (indexed), SecDateEntry date default '0001-01-01', SecDateTEdit timestamp. Exact DDL is in the consolidated schema above.

**Codebase touchpoints:** `WpfControlsOD/Frms/FrmFeeSchedEdit.xaml(.cs)`, `OpenDental/Forms/FormFeeScheds.cs`, `OpenDental/Forms/FormProcCodes2.cs`, `OpenDentBusiness/Data Interface/FeeScheds.cs`, new `OpenDentBusiness/TableTypes/FeeSchedNote.cs`, new `OpenDentBusiness/Data Interface/FeeSchedNotes.cs`, new forms `FrmFeeSchedNotes`, `FrmFeeSchedNoteEdit`.

---

### v24_4-reports-incomplete-procnotes-all: include every procedure with no note

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Miscellaneous, incomplete procedure notes item); blog (incomplete procedure notes paragraph, which gives the new filter's label); baseline https://www.opendental.com/manual/reportincompleteprocnote.html.

**User-visible behavior:** The Incomplete Procedure Notes report gets a third filter: list **every** completed procedure in the date range that has no note at all.
- The 24.3 option "no notes on any procedure for the same day" finds only days where none of the patient's procedures have a note.
- The new option also catches a single unnoted procedure on a day when other procedures do have notes.

**UI elements**
- `FormRpProcNote` Filters group: new check box **Include all procedures with no notes**, under the existing "Include procedures with no notes on any procedure for the same day" and "Include procedures with a note that is unsigned".
- Report Setup > Misc Settings (`FormReportSetup`): a matching default check box.

**Business rules and edge cases**
- "No note" means the latest `procnote` row for the procedure has empty text, or there is no procnote row.
- When the new box is checked, the same-day box is implied. **Decision:** disable the same-day box while the new box is checked.
- Excluded codes (`ReportsIncompleteProcsExcludeCodes`) still apply.
- The 24.3 rule that hides procedures when the same-day procedure has an incomplete quotation note doesn't apply to this option.
- The Unsigned column is blank for rows with no note.
- In 24.3 the No Note column (and the Incomplete column) show only when one of the existing optional filters is checked (24.3 manual). The new box must also turn them on.

**Permissions / security log:** existing report permission (`IncompleteProcNotes` display report). The run is logged as it is today.

**Preferences (new):** `ReportsIncompleteProcsAllNoNotes` (bool, default false). Proposed name; **Verify** OD's key.

**Schema:** none.

**Codebase touchpoints:** `OpenDental/Forms Reports/FormRpProcNote.cs` (+ designer), `OpenDentBusiness/Reports/RpProcNote.cs`, `OpenDental/Forms/FormReportSetup.cs` (next to `ReportsIncompleteProcsNoNotes`), `OpenDentBusiness/TableTypes/Pref.cs`.

---

### v24_4-setup-reportserver-audit: Audit Trail entry when the Report Server changes

- **Category:** desktop · **Size:** S · **Depends on:** none · **Already in baseline (OD backported it to 24.3.35)**
- **Sources:** release notes (Miscellaneous: an Audit Trail entry for Report Server changes, noted as backported to 24.3.35); baseline https://www.opendental.com/manual/reportserver.html.

**Status:** `FormReportSetup.UpdateReportingServer()` already writes a Setup log entry listing old → new server name, database, MySQL user, SSL CA and Middle Tier URI.

**Small fix to make:** the SSL CA line uses `logentry=` instead of `logentry+=` (around line 290), so it overwrites the earlier lines in the same entry. Also log when the user switches between Direct and Middle Tier modes. Passwords must never appear in the log (already true).

**Permissions / prefs / schema:** none new. **Touchpoints:** `OpenDental/Forms/FormReportSetup.cs` (~260–303).

---

### v24_4-setup-cda-codes-2025: 2025 CDA procedure codes

- **Category:** desktop · **Size:** S · **Depends on:** none · **Already in baseline (OD backported it to 24.3.32)**
- **Sources:** release notes (Miscellaneous, CDA 2025 codes item, noted there as backported to 24.3.32); our git history (commit "version 24.3.32.0" changed the `FormProcTools` label).

**Status:** `FormProcTools` already offers "CDA Codes – Add any missing 2025 CDA Codes" (Canada only).

**Fork note:** CDA code descriptions belong to the Canadian Dental Association. The decoupling plan's "nothing proprietary in the repo" rule names ADA CDT text. By the same reasoning, the fork should load the code list from a file the office supplies or imports, not from data shipped by OD. No 24.4 work beyond that.

**Touchpoints:** `OpenDental/Forms/FormProcTools.cs` (~60).

---

### v24_4-tools-rawemails-unicode: Raw Emails tool also cleans Unicode white space

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Miscellaneous, Raw Emails item); baseline https://www.opendental.com/manual/databasemaintenancetools.html (Raw Emails entry).

**User-visible behavior:** Database Maintenance > Tools > Raw Emails cleans more inbound emails. Messages whose subject or body contain unusual Unicode white space no longer end up in the "error" or "no change" counts. That white space includes non-breaking spaces, zero-width spaces, byte-order marks, the U+2000–U+200A spaces and the ideographic space.

**Business rules (interpretation; the exact OD rule isn't public)**
- In `DatabaseMaintenances.CleanUpRawEmails()`, normalize white space before comparing the old and re-processed message (Subject, BodyText) and before saving:
  - map Unicode space separators to an ASCII space
  - remove zero-width characters (U+200B–U+200D, U+FEFF)
  - leave line breaks alone
- This prevents updates that change nothing and comparison failures, and it lets the RawEmailIn attachment bloat be removed from more rows.
- Don't change encrypted (Direct) messages; the tool already skips them.
- The tool's summary message still reports cleaned / unchanged / error counts.

**Permissions / security log:** existing Database Maintenance access. The tool run is logged as it is today.

**Preferences / schema:** none.

**Codebase touchpoints:** `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` (`CleanUpRawEmails` ~11652), `OpenDental/Forms/FormDatabaseMaintenance.cs` (`RawEmailsFix` ~541), `OpenDentBusiness/Data Interface/EmailMessages.cs` (`ProcessRawEmailMessageIn`).

---

### v24_4-reports-daily-procs-split: Daily Procedures report becomes "Procedures" and "Production by Procedure"

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Miscellaneous, two items: the report split, and the right-click on the itemized report, which the notes call "Individual Procedures"); blog (daily procedures paragraph: the two reports are called Procedures and Production by Procedures); baseline https://www.opendental.com/manual/reportdailyprocs.html. https://www.opendental.com/manual/reportproductionbyprocedure.html is not a 24.3 page; it only redirects to the gated current manual; https://opendental.com/help244/reportproductionbyprocedure.html and https://opendental.com/help244/reportindividualprocedures.html *(gated, search summary)*.

**User-visible behavior:** Standard Reports > Daily now has two entries in place of one "Procedures" report with a Type choice:
1. **Production by Procedure.** This is the old "Grouped By Procedure Code" mode, always grouped by code, with no patient names. Its columns are category, code, description, quantity, average fee and total fees (24.3 manual). It has the same filters as before: date range, providers, clinics, and "Only for procedure codes similar to".
2. **Procedures** (an itemized, interactive report). It opens empty. The user sets From/To dates, clinics (with "All (Includes hidden)") and providers ("All" includes providers hidden on reports), then clicks **Refresh**. Each completed procedure is listed with date, patient, code, description, tooth/surface, provider, clinic and fee, plus a total. Filters can be changed and refreshed without closing. Print and Export are available.

**UI elements**
- `FormRpProcSheet` keeps only the grouped mode: remove the Type group box, and use the title "Production by Procedure Report".
- New non-modal window `FormRpProcedures`, laid out like the Incomplete Procedure Notes report: filters at the top, grid, Refresh, Print, Export, Close.

**Business rules and edge cases**
- Users without "Daily – View All Providers" (`ReportDailyAllProviders`) see only their own provider, and the All box is disabled. `FormRpProcSheet` (~30) does this today. A user who is not a provider and lacks that permission can't open the report at all; `FormReportsMore` (~446) blocks it. Apply both rules to both reports.
- Clinic restrictions apply.
- Totals in the itemized report must equal the old Individual mode for the same filters. Reuse `RpProcSheet` queries.
- Report menu and permissions: the current `displayreport` row `ODProcedures` becomes the itemized "Procedures" report, and a new row is added for Production by Procedure. **Verify** OD's internal name (check 2) so the office's report permissions carry over. Copy the existing `grouppermission` Reports rows of `ODProcedures` to the new row during conversion.

**Permissions / security log:** report access through display-report permissions. Log "Procedures report run." / "Production by Procedure report run." under Reports.

**Preferences:** none.

**Schema:** a data row in `displayreport` (and copied `grouppermission` rows). No DDL.

**Codebase touchpoints:** `OpenDental/Forms Reports/FormRpProcSheet.cs` (+ designer), `OpenDentBusiness/Reports/RpProcSheet.cs`, `OpenDental/Forms/FormReportsMore.cs` (~446 `"ODProcedures"`), `OpenDentBusiness/Data Interface/DisplayReports.cs` (`ReportNames`), `OpenDental/User Controls/UserControlReportSetup.cs` (~353, `"ODProcedures"` in the report-permission list), `OpenDentBusiness/Misc/ConvertDatabases8.cs`.

---

### v24_4-reports-individual-procs-goto-patient: right-click to go to the patient

- **Category:** desktop · **Size:** S · **Depends on:** v24_4-reports-daily-procs-split
- **Sources:** release notes (Miscellaneous, right-click item for the itemized report); blog (the itemized Procedures report is interactive, and right-click goes to the patient's chart).

**User-visible behavior:** In the itemized Procedures report grid, right-click a row and choose **Go to Patient**. The main window selects that patient and switches to the Chart module; the report stays open. Double-clicking does the same. **Decision:** double-click behaves the same; this matches "See Chart" in the Incomplete Procedure Notes report.

**Business rules:** use the existing global patient-select call (as `FormRpProcNote` "See Chart" does). If the patient is archived and the user lacks `ArchivedPatientSelect`, show the normal message and stay on the report.

**Permissions / prefs / schema:** none new.

**Touchpoints:** new `FormRpProcedures` (see above); pattern in `OpenDental/Forms Reports/FormRpProcNote.cs` ("See Chart").

---

### v24_4-setup-codegroup-ranges: code ranges in Code Groups

- **Category:** desktop · **Size:** M · **Depends on:** none
- **Sources:** release notes (Miscellaneous, code group ranges item); blog (code group ranges paragraph, example D2391-D2394); DB docs 24.4 `codegroup.ProcCodes`: now commas and dashes, e.g. `D0000-D0999,D2140-D2161,D2750` (24.3 said comma-delimited only); baseline https://www.opendental.com/manual/codegroups.html.

**User-visible behavior:** In Setup > Code Groups > Code Group Edit, the Proc Codes box accepts ranges such as `D2391-D2394` mixed with single codes. Frequency limitations, age limitations and history benefits that use the group treat every code in the range as a member.

**UI elements:** `FormCodeGroupEdit`. The hint label changes to "Codes and ranges separated by commas (e.g. D2391-D2394,D2740)". The **Add** picker still appends single codes.

**Business rules and edge cases**
- A range is `<code>-<code>` where both ends share a prefix letter and a numeric part. Membership compares numerically within the same prefix.
- Codes that themselves contain a dash (e.g. `D1110-2`) are not ranges. `ProcedureCodes.GetCodeNumsForProcCodes` already handles this by requiring "D" on both ends. **Decision:** generalize to "same alphabetic prefix on both ends, numeric remainder".
- Reject reversed ranges (start > end) on save with a message. Don't require that the codes exist; this matches 24.3.
- Remove spaces and duplicate entries on save.
- **Important bug fix:** many membership checks do a plain substring test, `codeGroup.ProcCodes.Contains(procCode)`, in `Benefits.cs` (at least lines 312, 417, 831, 980, 1060, 1117, 1246, 1293, 1493, 1697) and in `CodeGroups.IsProcInCodeGroup`. Replace them all with one range-aware `CodeGroups.IsProcInCodeGroup(string procCode, CodeGroup)`, backed by a cached set of CodeNums per group. Without this, ranges silently break frequency limits. The substring test is also wrong today: "D111" matches "D1110".

**Permissions / security log:** Setup (existing). The log entry for a code group edit shows the ProcCodes text before and after.

**Preferences / schema:** none (text column unchanged).

**Codebase touchpoints:** `OpenDental/Forms/FormCodeGroupEdit.cs` (`GetProcCodesFromUI` ~57), `OpenDentBusiness/Data Interface/CodeGroups.cs` (`IsProcInCodeGroup` ~106, `GetCodeNums` ~150), `OpenDentBusiness/Data Interface/ProcedureCodes.cs` (`GetCodeNumsForProcCodes` ~457), `OpenDentBusiness/Data Interface/Benefits.cs`.

---

### v24_4-lists-medication-search: better search in the Medications list

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Miscellaneous, medication search item); baseline https://www.opendental.com/manual/medicationmaster.html; https://opendental.com/help254/medicationmaster.html *(gated, search summary: results appear once typing pauses)*.

**User-visible behavior:** Search in Lists > Medications finds more of what the user means. OD gives no further detail, so this spec defines the behavior:
- Matching is case-insensitive.
- The text is split into words, and every word must appear in the name, in any order. "hcl metformin" finds "Metformin HCl".
- A brand medication also matches when the search text matches its **generic** name. Searching a generic lists its brands too.
- When the search text is all digits, it also matches the RxNorm code (`RxCui`).
- Results refresh after a short pause in typing. A 500 ms debounce timer (`_timer` in `FormMedications`) already exists; keep it. The 24.3 manual says results show while the user types.

**UI elements:** `FormMedications`, Search box and both grids ("All Medications" and "Missing Generic/Brand"). No new controls.

**Business rules:** the search only filters the list. Selection mode (picking a medication for a patient) uses the same search. Hidden medications (25.x `medication.IsHidden`) are out of scope for 24.4.

**Permissions / prefs / schema:** none.

**Codebase touchpoints:** `OpenDental/Forms/FormMedications.cs` (~101, `textSearch_TextChanged` ~349), `OpenDentBusiness/Data Interface/Medications.cs` (`GetList` ~107).

---

### v24_4-sheets-secondary-ins-statictext: more secondary-insurance static text fields

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Miscellaneous, secondary insurance static text item); blog (static text fields paragraph); baseline https://www.opendental.com/manual/statictextfields.html.

**User-visible behavior:** In the sheet editor's Static Text field list, more secondary-insurance fields are available. Patient letters, treatment plans and other sheets can then show secondary coverage details the same way they show primary ones.

**Fields to add.** 24.3 has these primary fields with no secondary counterpart:

| Primary (existing) | Proposed secondary token |
|---|---|
| `insFeeSchedule` | `ins2FeeSchedule` |
| `insPlanNote` | `ins2PlanNote` |
| `insSubBirthDate` | `ins2SubBirthDate` |
| `insSubNote` | `ins2SubNote` |
| `insType` | `ins2Type` |
| `subscriberID` | `subscriber2ID` |

**Verify:** run check 4 on the office's sheet defs and adopt OD's exact token names. Sheets saved in 25.3 that use them must keep working.

**Business rules:** secondary means the second non-medical PatPlan by ordinal, as the existing `ins2*`/`carrier2*` fields already use. Blank if the patient has no secondary plan. Each new field declares the same `StaticTextFieldDependency` flags as its primary counterpart (ListInsSubs, ListInsPlans, ListPatPlans, ListBenefits).

**Permissions / prefs / schema:** none.

**Codebase touchpoints:** `OpenDentBusiness/SheetFramework/EnumStaticTextField.cs`, `OpenDentBusiness/SheetFramework/StaticTextData.cs`, `OpenDentBusiness/SheetFramework/SheetFiller.cs` (where the `ins2*` values are filled), the static text picker in `OpenDental/Forms/FormSheetFieldStatic.cs`.

---

### v24_4-reports-dailypayments-merchant-fees: one merchant-fee option on the Daily Payments report

- **Category:** desktop · **Size:** S · **Depends on:** none. CareCredit itself is being cut per the decoupling plan; PayConnect stays as a direct vendor integration.
- **Sources:** release notes (Miscellaneous, merchant fees item); baseline https://www.opendental.com/manual/reportdailypayments.html (24.3: separate "Show CareCredit fees" and "Show PayConnect fees" boxes, columns and totals).

**User-visible behavior:** The two check boxes become one, **Show merchant fees**. Fees from both processors appear in a single **Merchant Fee** column/row, with single totals:
- "Total Merchant Fees" at the end of the Patient Payments section and for the whole report. As in 24.3, fees show only for patient payments.
- "Total Patient Payments After Fees" and "Total All Payments After Fees", computed as payments minus merchant fees

**Business rules and edge cases**
- A payment counts if its `PaymentSource` is CareCredit or any PayConnect source (PayConnect, XServerPayConnect, PayConnectPortal, PayConnectPortalLogin, PayConnectPaymentPortal, PayConnectPaymentPortalGuest).
- The fee is `MerchantFee`. For CareCredit, `MerchantFee` is stored with the opposite sign, so negate it, as `AddCareCreditFeeColumn` does today.
- Keep showing historical CareCredit fees after CareCredit is removed from the fork, because office data contains them.
- Fees stay on their own row under the payment (24.3 layout).
- **Decision:** the combined column also picks up any future direct processor that records `MerchantFee`.

**Permissions / security log:** existing report permission and log entry.

**Preferences / schema:** none.

**Codebase touchpoints:** `OpenDental/Forms Reports/FormRpPaySheet.cs` (`AddCareCreditFeeColumn` ~182, `AddPayConnectFeeColumn` ~198, layout ~299–303) and its designer, `OpenDentBusiness/Reports/RpPaySheet.cs`.

---

### v24_4-claims-medical-hospitalization-dates: hospitalization dates on the medical claim form

- **Category:** desktop · **Size:** S · **Depends on:** none
- **Sources:** release notes (Miscellaneous, hospitalization dates item); baseline https://www.opendental.com/manual/claimform1500.html (box 18 "Hospitalization Dates" has no data source in 24.3).

**User-visible behavior:** The CMS-1500 (02/12) form can print box 18, the hospitalization dates related to current services (From and To).

**Data source.** The 24.4 DB docs (and the 25.3 dump) add **no** claim column, so OD must fill box 18 from data that already exists.
- **Decision:** From = `patient.AdmitDate` ("used in hospitals") and To = `ehrpatient.DischargeDate`.
- Print only when the claim is medical (`claim.MedType` = Medical). Print From only when it's a real date; print To only if it is on or after From.
- Leave both blank otherwise.

**UI elements**
- Claim form item field list (`FormClaimFormItemEdit`): two new field names.
- **Decision:** call them `HospitalizationDateFrom` and `HospitalizationDateTo`. **Verify** with check 3, and use OD's names if the office's claim forms already contain them.
- Add both items at the box 18 positions of the internal `ClaimForm1500_02_12.xml`, using the same date format as boxes 14 and 15 (MM DD YY).

**Permissions / prefs / schema:** none.

**Codebase touchpoints:** `OpenDental/Forms/FormClaimFormItemEdit.cs` (field list ~285), `OpenDental/Forms/FormClaimPrint.cs` (fill switch ~2155), `OpenDental/Forms/FormClaimFormEdit.cs` (preview ~467), `OpenDentBusiness/Resources/ClaimForms/ClaimForm1500_02_12.xml`, `OpenDentBusiness/Data Interface/EhrPatients.cs`.

---

## Vendor-direct items

These two aren't in the release notes. They come from the 24.4 database documentation diff and matter for parity with the office's data.

### v24_4-imaging-pearl-request-statuses: Pearl AI request error states

- **Category:** vendor-direct · **Size:** S · **Depends on:** the Pearl bridge, which already calls `https://native-integration-prod.hellopearl.com` directly with the office's own credentials (client ID, secret, office ID and organization ID from the Pearl program properties; see `PearlApiClient.cs` ~50)
- **Sources:** DB docs 24.4 `pearlrequest.RequestStatus` (`EnumPearlStatus`); https://www.opendental.com/site/pearlaiprocess.html and https://www.opendental.com/site/bridgepearlai.html (public bridge pages).

**Behavior.** 24.3 had Polling 0, Received 1, Error 2, TimedOut 3, Uploading 4. 24.4 narrows Error 2 to "Pearl rejected the image, never retry" and adds:
- **ErrorUploading 5:** upload failed; the image may be uploaded again.
- **ErrorProcessing 6:** Pearl failed while processing; don't re-upload, but polling can restart.
- **NoResults 7:** Pearl returned nothing; don't re-upload.

**UI:** in the Imaging module, the Pearl status message tells these cases apart. In 24.3 that message shows Polling and Timed out (public Pearl process page). The "send to Pearl" action is offered again only for 5 (re-upload) and 3/6 (re-poll). Today a resend is allowed only for TimedOut, in two places: `Pearl.SetupPearlForSendingSingle` (`Pearl.cs` ~120) and the single-document path in `ControlImages` (~2594). Both must change.

**Rules:**
- Treat 2, 5, 6 and 7 as handled in `PearlRequests.IsRequestHandled` (~112), along with 1. Today it checks only Received and Error.
- Retry logic in `Pearl.cs` (~120, ~222, ~370) maps exceptions to 5 or 6 instead of 2.
- Unknown future values are treated as Error.

**Permissions / prefs:** none new. **Schema:** enum values only (the column is unchanged).

**Touchpoints:** `OpenDentBusiness/TableTypes/PearlRequest.cs`, `OpenDentBusiness/Bridges/Pearl/Pearl.cs`, `OpenDentBusiness/Bridges/Pearl/PearlApiClient.cs`, `OpenDentBusiness/Data Interface/PearlRequests.cs`, `OpenDental/Main Modules/ControlImages.cs` (~2594), `UnitTests/UnitTests/PearlTests.cs`.

### v24_4-claims-dxc-attachment-submitted: DentalXChange attachment "Submitted" flag

- **Category:** vendor-direct · **Size:** S · **Depends on:** the DentalXChange (ClaimConnect) attachment integration, which talks to DentalXChange with the office's own account
- **Sources:** DB docs 24.4 `claim.AttachedFlags` (new keyword `Submitted` for a submitted DentalXChange attachment) and the new `claimattach` table note (DentalXChange uses "attachment" to mean a group of images); https://www.opendental.com/site/dentalxchange.html.
- **Vendor-direct caveat:** our DentalXChange calls send the office's clearinghouse login, but also a partner identity hard-coded as Open Dental's (`ClaimConnect.DxcCredentials.GetDentalxchangeCredentials`, `Client="OpenDental"` and OD's DCI service ID, ~1150). To be independent, the fork needs its own partner Client/ServiceID from DentalXChange, or written permission to keep using OD's. This affects every DentalXChange feature, not just this flag.

**Behavior:** after attachments are sent to DentalXChange for a claim, add the keyword `Submitted` to `claim.AttachedFlags` (comma list, alongside EoB, Note, Perio, Misc, Unsup and Mail/Elect). Use it to:
- show the claim's attachment state ("attachments submitted") in Edit Claim and the attachment windows
- avoid sending the same attachments twice
- skip the "attachments missing" warning when the claim is sent

**Rules**
- Remove the flag when all attachments are deleted, or when new ones are added that haven't been sent.
- Keep the flag when parsing AttachedFlags anywhere that splits on commas. Existing code that rebuilds the string from check boxes (`FormClaimEdit` ~3628) must keep unknown keywords instead of dropping them. The DentalXChange attachment windows currently overwrite the whole string with `"Misc"` (`FormClaimAttachment` ~703/~733, `FormClaimAttachPasteDXC` ~248, `FormClaimAttachSnipDXC` ~365); that is where `Submitted` gets added.
- The X12 837 builders treat any flag other than exactly `"Mail"` as a sign that paperwork is attached (`X837_5010.cs` ~3123, `X837_4010.cs` ~1925). They must ignore `Submitted`, or a claim flagged `Mail,Submitted` would wrongly trigger the PWK logic and the attachments warning.

**Permissions / prefs:** none. **Schema:** none (value convention only).

**Touchpoints:** `OpenDental/Forms/FormClaimEdit.cs`, `OpenDental/Forms/FormClaimAttachment.cs`, `OpenDental/Forms/FormClaimAttachPasteDXC.cs`, `OpenDental/Forms/FormClaimAttachSnipDXC.cs`, `OpenDental/Forms/FormClaimAttachHistory.cs`, `OpenDental/Forms/FormClaimInstEdit.cs` (~1919), `OpenDental/Forms/FormClaimPrint.cs` (~1460), `OpenDentBusiness/Data Interface/ClaimAttaches.cs`, `OpenDentBusiness/Eclaims/ClaimConnect.cs`, `OpenDentBusiness/X12/X837_5010.cs`, `OpenDentBusiness/X12/X837_4010.cs`.

---

## Excluded

Each item below is cut by project scope. Where the office data holds related rows or columns, the schema is still carried so the data can be adopted.

| ID | Item (source: https://www.opendental.com/site/version24_4.html unless noted) | Category | Why excluded |
|---|---|---|---|
| v24_4-eforms-eclipboard-delivery | Sending eForms to the updated eClipboard app (Major Changes; https://www.opendental.com/site/featureeforms.html) | excluded-od-service | eClipboard is an OD-hosted eService. The desktop designer and in-office filling are kept as v24_4-eforms-desktop-forms. |
| v24_4-account-m2p-hide-option | Preference to hide Message-to-Pay in the Account Payment drop-down | excluded-od-service | Message-to-Pay runs on OD texting and the payment portal; the fork removes the menu item entirely. |
| v24_4-account-m2p-guarantor-statement | Send Message-to-Pay to the guarantor when sending a single statement (blog) | excluded-od-service | Depends on OD-hosted Message-to-Pay and texting. |
| v24_4-account-m2p-paytype | Payment Type for Message-to-Pay payments, set in Payment Portal Setup (blog) | excluded-od-service | Payment Portal and Message-to-Pay are OD-hosted. |
| v24_4-eservices-webforms-typed-sig-stamp | Typed Web Forms signatures show a date/time stamp on the imported form (the blog adds that the typed name is shown) | excluded-od-service | Web Forms are hosted by OD (SheetsSynch through HQ). |
| v24_4-eservices-automsg-time-format | Custom time format in Automated Messages (eReminders, eConfirmations) | excluded-od-service | Automated messaging is sent through OD's eConnector and HQ. |
| v24_4-help-manual-access | 24.4+ manual opened from Help (? button, Help > Online Help, Shift+F1) (blog) | excluded-od-service | The content is OD's customer-gated manual. The menu items and Shift+F1 are already in 24.3; the decoupling plan points them at our own docs. |
| v24_4-imaging-betterdiag-layers | BetterDiagnostics AI annotation layers (DB docs only: `imagedraw.BetterDiagLayer`, `EnumImageAnnotVendor.BetterDiagnostics`) | excluded-od-service | BetterDiagnostics goes through OD: sign-up is in OD's eServices Signup Portal, the eConnector must be running, and OD does the billing (https://www.opendental.com/site/bridgebetterdiagnostics.html, https://www.opendental.com/site/betterdiagnostics.html). Carry the column (`tinyint(4) NOT NULL`) and enum values for compatibility only. |
| v24_4-hq-eservicecode-enums | New `eServiceCode` values Developer Portal API/UI and Email HQ Service (DB docs only) | excluded-other | HQ-internal service codes. Reserve the numbers 50–52 only. |
| v24_4-api-carriers-get-single | API: Carriers GET (single) | excluded-other | Local REST API is cut. |
| v24_4-api-procedurelogs-filter-plannedapt | API: ProcedureLogs GET filtered by PlannedAptNum | excluded-other | Local REST API is cut. |
| v24_4-api-labturnarounds-put-post | API: LabTurnarounds PUT and POST | excluded-other | Local REST API is cut. |
| v24_4-api-claimprocs-filter-status | API: ClaimProcs GET (multiple) filtered by Status | excluded-other | Local REST API is cut. |
| v24_4-api-patients-medicaidid | API: MedicaidID on Patients POST and PUT | excluded-other | Local REST API is cut (the column already exists). |
| v24_4-api-tasks-get-single | API: Tasks GET (single) | excluded-other | Local REST API is cut. |
| v24_4-api-referrals-get-businessname | API: Referrals GET by BusinessName | excluded-other | Local REST API is cut. |
| v24_4-api-referrals-more-fields | API: more fields on the Referrals object | excluded-other | Local REST API is cut (exposes existing columns only). |
| v24_4-api-insverifies-filter-fkey | API: InsVerifies GET (multiple) filtered by FKey | excluded-other | Local REST API is cut. |
| v24_4-api-providers-get-single-multiple | API: Providers GET single and multiple | excluded-other | Local REST API is cut. |
| v24_4-api-labcases-put-post-delete | API: LabCases PUT, POST, DELETE | excluded-other | Local REST API is cut. |
| v24_4-api-tasknotes-resource | API: new TaskNotes resource | excluded-other | Local REST API is cut. |
| v24_4-api-laboratories-post | API: Laboratories POST | excluded-other | Local REST API is cut. |
| v24_4-api-appointments-get-appttype | API: Appointments GET by AppointmentTypeNum | excluded-other | Local REST API is cut. |
| v24_4-api-laboratories-put | API: Laboratories PUT | excluded-other | Local REST API is cut. |
| v24_4-api-queries-screening | API: Queries PUT and POST, stricter query screening | excluded-other | Local REST API is cut. |
| v24_4-api-proctps-put | API: ProcTPs PUT | excluded-other | Local REST API is cut. |
| v24_4-api-paysplits-get-procnum | API: PaySplits GET by ProcNum | excluded-other | Local REST API is cut. |
| v24_4-api-treatplans-delete | API: TreatPlans DELETE | excluded-other | Local REST API is cut. |
| v24_4-api-payments-post-refund | API: Payments POST Refund | excluded-other | Local REST API is cut. |
| v24_4-api-toothinitial-apievent | API: ToothInitial table ApiEvent | excluded-other | API event subscriptions are part of the cut API. |
| v24_4-api-payplans-get-single | API: PayPlans GET (single) | excluded-other | Local REST API is cut. |
| v24_4-api-providers-post-license-npi | API: Providers POST can set StateLicense and NationalProvID | excluded-other | Local REST API is cut (the columns already exist). |

---

## Fact-check log (2026-10-18)

A second pass re-read the release page and the blog, checked public manual pages, rechecked every codebase path, and diffed the 24.3 and 24.4 DB docs. It changed the following:

- **Coverage:** every item on the release page is in this spec (1 major, 29 category and 23 API items); none were missing. Fixed the category count (was 34) and added the item-to-spec mapping.
- **Summary table:** the "Depends on" and "In baseline?" values were swapped for the Report Server and CDA rows.
- **Codebase paths:** every listed path exists. The only missing files are the ones marked "new". Clarified which file holds which signature-box line (pay plans). Added missing touchpoints: `UserControlReportSetup.cs`, `ClaimProcs.ComputeSalesTax`, the X12 837 builders, `ClaimConnect.cs`, `FormClaimPrint.cs`, `FormPerioGraphicalSetup.designer.cs`, and the eForm per-field editors.
- **Behavior fixes, from manual pages and code:**
  - eForms: the Demo template is Debug-only; the button labels now match the code; required fields aren't enforced on the desktop; a classification note was added.
  - Sales tax: the button is "Quick Procs" in 24.3; `AccountProcsQuickAdd` is required; tax is computed per procedure after PPO write-offs.
  - Appointment views: the waiting-room setting is an `apptview` column.
  - Query Monitor: over Middle Tier the window becomes a payload monitor.
  - Medications: the debounce timer is 500 ms, not 300.
  - Daily Procedures: the provider rules live in `FormRpProcSheet` and `FormReportsMore`, and the Production by Procedure manual URL is only a redirect to the gated manual.
  - Incomplete Procedure Notes: the column-visibility rule now covers the new box.
  - Fee tools: the blank date on Import and the scope of Clear are now marked unverified, or as decisions.
  - Chart letters: the future-date rule and the signature-algorithm caveat were added.
  - Pearl: the exact methods that block a resend are now named.
  - DentalXChange: the X12 "anything but Mail" pitfall and the hard-coded OD partner identity were added.
- **Schema:** all 24.4 DDL matches `fox-db-schema.sql`. Added the reconciliation with the measured diff (2 of 15 tables, 7 of 47 columns, not the rename) and a list of the DB-doc wording changes that need no work.
- **Classification:** no item moved. Added public evidence for the BetterDiagnostics exclusion and a note on the eForms desktop decision.
- **Wording:** release-note titles and blog headings that were quoted in Sources lines are now paraphrased. The remaining exact strings are UI labels or messages from our own code.

