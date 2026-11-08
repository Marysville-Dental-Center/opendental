# Port note: P262-BILLING, billing and statements overhaul (Open Dental 26.2 items 8, 9, 17 and 18)

Branch `p262/billing-1`, cut from `p262/schema-1` (39be2b04b), and its review fixes on `p262/billing-2` and `p262/billing-3`. One of the 26.2
feature packages the owner chose on 2026-11-07.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html). The four items are one line each, with no link:
  - item 8: "Moved Statement dropdown options to a new window";
  - item 9: "Redesigned the Statement options window";
  - item 17: "Moved Electronic Billing settings to a new window";
  - item 18: "Reconfigured Billing Defaults and added ability to set some settings per clinic".
- The public online manual pages of the windows involved: Billing Defaults (`manual/billingdefaults.html`), Statement Window
  (`manual/statementwindow.html`, which also lists the Account module's Statement dropdown) and Statement (`manual/statement.html`). The
  public manual still describes the windows before 26.2 (its pages are headed v24.4); it says nothing of the 26.2 windows. The versioned help
  pages of 26.2 (`help262/...`) are not public (they answer only to a cookie the program sets), so they were not used.
- Open Dental's 26.2 database documents (`OpenDentalDiffDocumentation26-2.xml`, `OpenDentalDocumentation26-2.xml`): the diff document has no
  table, column or enum value for these items (no billing, statement, `ebill` or `clinicpref` entry), so the four items need no schema
  change. The full document's descriptions of `clinicpref` ("preferences specific to clinics ... in conjunction with the Pref table") and
  `ebill` ("account details of e-statements per clinic", `ClinicNum` 0 for the defaults) are what this package stores into; both tables are
  unchanged since 26.1.

Everything about the windows beyond the four lines above is this program's own design and is marked INFERRED below.

## What was built

### Item 8: the Statement Options window

- The Account module's Statement button keeps its click: it prints the default statement at once, as before. Its dropdown arrow, which
  showed a menu before 26.2, now opens the new **Statement Options** window (`FormStatementChoices`). The window lists the default statement
  and every entry of the former menu (Walkout, Email, Receipt, Invoice, Limited, Limited (Custom), More Options and the Service Date View
  window), each with a one-line description (`StatementChoices`); double-click (or select and OK) runs the entry exactly as the menu entry
  ran it (`MenuItem.PerformClick`, or the button's own print for the default statement).
- The window reads the entries from `contextMenuStatement` each time it opens (visible and enabled entries only), so an entry another part of
  the program or a plugin adds to that menu shows too (without a description). Each menu entry carries its `StatementChoice` in its `Tag`.
- `ToolBarOD` raises a new event, `ButtonDropDownClick`, when the arrow of a dropdown button without a menu is clicked. Every other toolbar
  button is unchanged (a button with a menu still shows it; nothing else subscribes).
- Files: `Main Modules/ControlAccount.StatementOptions.cs` (new partial), `Forms/FormStatementChoices.*` (new), `ControlAccount.cs`
  (constructor call, no dropdown menu on the Statement button), `ControlAccount.ServiceDateView.cs` (the Tag of its entry),
  `UI/ToolBarOD.cs`, `OpenPmsDentalBusiness/Logic/StatementChoices.cs` (new).

### Item 9: the redesigned Statement window

- The Statement window (`FormStatementOptions`: More Options, the Billing List's statements, opening a saved statement) is laid out in
  boxes: **Statement** (date, mode, sent, receipt or the limited statement mark), **Invoice** (as before), **Date Range** (as before),
  **Contents** (hide payment options, single patient, intermingle, super family, Service Date View, patient last name, exclude income
  transfers, export as CSV), then the Note, the Bold Note and the buttons. The window is shorter (no empty band above the buttons) and the
  notes are wider.
- No control was added or removed, and every event and rule is unchanged (`SetEnabled`, the bulk edit, the invoice and limited statement
  rules, the Service Date View rules). The limited custom statement keeps exactly the same controls usable: `DisableAllExcept` only looks at
  the window's own controls and would disable a whole box, so the window uses `DisableAllExceptInBoxes`, which keeps a box that holds an
  allowed control and disables its other controls one by one.

### Item 17: the Electronic Billing window

- The Electronic Billing box of Billing Defaults is now its own window, **Electronic Billing** (`FormEbillSetup`): the method (No electronic
  billing, DentalXChange, Output to file, ClaimX / ExtraDent, EDS), the accepted credit cards, vendor ID and PMS code, the account number,
  user name and password and the practice and remit addresses (per clinic, through the `ebill` rows, as before), Generate PDF, Include
  'Adjust', Include Clinic Number and the output path or URL override. The code moved from `FormBillingDefaults` unchanged in behaviour: the
  same preference rows, the same `ebill` sync, the same output path check and the same DentalXChange URL default.
- Billing Defaults opens it with its **Electronic Billing...** button. The Billing List's **Defaults** button, which before 26.2 opened
  Billing Defaults limited to its Electronic Billing box (`IsUserPasswordOnly`), opens the Electronic Billing window; the limited mode is gone.
- `EbillSettings` (business layer) holds the stored form shared with the tests: the method list, the output path preference of each
  method, which methods need a folder or can make a PDF, and the credit card text ("MC,V,D,A", read by its letters as before).

### Item 18: Billing Defaults reconfigured, some settings per clinic

- Billing Defaults has two boxes. **Statements** (practice-wide; "Statements (all clinics)" when clinics are on): Intermingle family members,
  Start Date Last N Days, Show all transactions since zero or negative balance, Print inserter barcode on statements, and the hidden Single
  patient only default, as before. **Messages**: the General Message, the Email Statements subject, body, body type (Edit HTML) and autograph
  box, and the Invoice Note.
- While clinics are on, a clinic picker ("Practice Defaults" and each clinic the user may see) chooses whose Messages the window shows, with
  **Use the practice defaults for this clinic**. A clinic that uses the practice defaults shows them read-only; unchecking the box starts its
  own settings from the practice values in the window. With clinics off, the picker and the box are hidden and the window works on the
  practice defaults only, as before.
- Storage (`BillingDefaultsByClinic`): the practice defaults stay in their preference rows (`BillingDefaultsNote`, `BillingEmailSubject`,
  `BillingEmailBodyText`, `BillingEmailType`, `BillingEmailIncludeAutograph`, `BillingDefaultsInvoiceNote`). A clinic's own value of one of
  them is a `clinicpref` row under the same name; a clinic that uses the practice defaults has none, and each message a clinic has no row for
  follows the practice default. Saving writes only the clinics whose settings changed in the window. The General Message row is the one
  Billing Options already keeps with a clinic's filter defaults, so both windows show and change the same value (Billing Options re-reads the
  clinic rows after Billing Defaults closes).
- Saving a clinic (`BillingDefaultsByClinic.SaveSettings`, given the settings as the window read them; review fix of `p262/billing-2`): a
  clinic that stops using the practice defaults (clears the box) gets all six rows, a copy of the practice values in the window (owner
  question 5). A clinic that already had rows gets a row only for each message changed for it in the window; its unchanged rows are not
  written again, and a message it has no row for keeps following the practice default. This matters for every clinic that saved Billing
  Options defaults before 26.2: that Save Defaults always stored the clinic's General Message with its 13 filters, so the clinic comes into
  26.2 with that one row and follows the practice for the other five. Editing only its General Message in Billing Defaults no longer gives it
  frozen copies of the practice e-mail subject, body, body type, autograph and invoice note. The e-mail body and its type are saved together
  (when either changes, both are written), so a clinic's body is never read with a type other than the one it was saved with. When the window
  shows such a clinic, each message it has no row for (and not changed for it) shows the practice value as it is in the window at that
  moment, also after the practice defaults were edited in the same window, so a value read earlier is neither shown nor saved.
- Billing Options beside it (`BillingOptionsClinicDefaults`, from the review of `p262/billing-1`): Save Defaults with one clinic selected
  writes that clinic's rows only, never another clinic's. A clinic whose only Billing Options row is the General Message (it has its own
  messages in Billing Defaults) has no filter defaults there: Billing Options shows it the practice filters with its own General Message, and
  a save for another clinic gives it no filter row, so later changes to the practice filter defaults still reach it. A clinic set back to the
  practice defaults in Billing Defaults keeps its Billing Options filters without a General Message row, and a save for another clinic does not
  write that row back. A clinic's saved rows are updated in place (the shared General Message row is never inserted twice); a clinic without
  a General Message row gets one only when the message typed differs from the practice message, so saving its filters alone does not take it
  off the practice defaults. The window also shows the selected clinic's own General Message when it opens and after Billing Defaults closes
  (before, it showed the practice message there, which Save Defaults would then have written over the clinic's own).
- Readers: statement e-mails (`Statements.GetEmailMessageForStatement`: billing, the Statement window, the sheet window, the Account
  module's Email) take the subject and body of the patient's clinic when the statement has none of its own, and the matching body type
  (`BillingEmailHtml.GetEmailTypeForStatement`); billing adds the autograph when the patient's clinic says so; a new invoice gets its
  patient's clinic's Invoice Note; billing and Billing Options already took the clinic's General Message (`ClinicBillingOptions`). A clinic
  row is read only while clinics are on: with clinics off every reader gets the practice default, and the rows are kept for when clinics
  are turned on again.

### Migration

None. Every setting keeps its preference row (and the `ebill` rows), so every saved setting carries over as it is; a clinic's own rows are
written only when the office saves that clinic in Billing Defaults. No preference row is new, so no migration step was needed: levels
2026110721 to 2026110729 stay unused (a test checks it). No table, column or enum value changed, and nothing was missing from
`p262/schema-1` for these items.

## INFERRED

Each point is this program's own choice; the public sources state none of them.

- Item 8: that the "new window" is opened by the Statement button's dropdown arrow, that the button's click still prints the default
  statement at once, that the window lists the former menu entries plus the default statement, its title "Statement Options", its list form
  (a grid with a description per entry) and the wording of the descriptions.
- Item 9: that "the Statement options window" is the Statement window opened by More Options (the window whose manual picture is named
  `statementOptions.png`), and the whole new layout: the boxes, their names, what each holds and the window's size. Its title stays
  "Statement".
- Item 17: the window's title "Electronic Billing", that it holds exactly the former box, that the Billing List's Defaults button opens it
  (its text stays "Defaults"), and that Billing Defaults keeps a button to it.
- Item 18: which settings are per clinic (the six messages above) and which stay practice-wide; the "Use the practice defaults for this
  clinic" box (all six rows when a clinic leaves the practice defaults, none when it goes back); that each message a clinic has no row for
  follows the practice default on its own, that saving a clinic that already has rows writes only the messages changed for it (unchanged
  rows are not written back), that the e-mail body and its type are saved together, and that the window shows the practice values as edited
  in it for the messages a clinic has no row for (review fix of `p262/billing-2`); storing a clinic's settings as `clinicpref` rows under
  the preference names; sharing the General Message row with Billing Options; which clinic applies (the clinic of the statement's patient,
  as the e-mail's sender address already used, and the invoice patient's clinic); that clinic rows are ignored while clinics are off; that
  the e-mail body type goes with the body; and that the picker starts at the user's current clinic.
- Item 18, Billing Options beside the per-clinic messages: that a clinic whose only Billing Options row is the General Message has no filter
  defaults there; that Save Defaults writes the selected clinic only (the window as it was before this package wrote every clinic in its list,
  which then only completed the rows of clinics with saved filters); that a save with the practice message adds no General Message row for a clinic without one; and that
  the window opens with the selected clinic's own General Message.

## Owner questions

**Decided as built (owner, 2026-11-07):** every question below is settled as the package built it ("all as recommended").

1. Items 8 and 9 are read as two windows: a new Statement Options window behind the Statement button's arrow, and a new layout of the
   existing Statement window. Is that the intended reading, or should the choices live in the Statement window itself? Should the
   Statement button's click keep printing at once?
2. Which Billing Defaults settings should be per clinic? This package makes the six messages per clinic. Candidates left practice-wide: the
   Start Date Last days, Intermingle and Show all transactions since zero (the last is already saved per clinic with Billing Options' filter
   defaults).
3. The Billing List's Defaults button now opens the Electronic Billing window: keep the text "Defaults", or rename it ("Electronic
   Billing")?
4. Should the electronic billing method itself (`BillingUseElectronic`) become per clinic? It stays practice-wide; the account, user name,
   password and addresses were already per clinic.
5. A clinic that stops using the practice defaults gets a copy of them, and later changes to the practice defaults no longer reach it. Is
   that wanted, or should each message fall back to the practice default on its own (for example when left blank)? (A clinic that already
   has some rows, such as the General Message every clinic that saved Billing Options defaults before 26.2 has, does follow the practice for
   each message it has no row for, and gets a row only for a message changed for it.)
7. For a clinic with some messages of its own and the rest from the practice, the window does not mark which is which (each looks like
   the clinic's own). Should it, for example with a "practice default" note beside the boxes that follow the practice?
6. Billing Options' Save Defaults for a clinic on the practice defaults, with the practice General Message in its box, saves the clinic's
   filters but no General Message of its own, so the clinic stays on the practice defaults in Billing Defaults. Before 26.2 that save also
   stored a copy of the message for the clinic. Keep the new behaviour?

## Not built

- Nothing of the 26.2 windows that the public sources do not describe (any further option, text or behaviour of Open Dental's 26.2 windows).
- No SMS statement text or texting modes in Billing Defaults: this program has no integrated texting (as before 26.2 here).
- No UI was run (OpenPmsDental.exe is not launched in this run); the windows are covered by source checks and the business layer by
  database tests.

## Tests

- New assembly `tests/P262BillingTests` (in the solution and the local filter), scratch databases `test_p262billing_*` (dropped):
  - `BillingDefaultsByClinicDbTests`: every setting keeps its preference row; a clinic without rows uses the practice defaults; the
    practice defaults save to the preference rows; a clinic's own settings are six rows for that clinic only, read back as written; back to
    the practice defaults removes only those rows; clinics off makes every reader take the practice defaults (rows kept); a General Message
    row saved by Billing Options is the clinic's own and the other messages fall back; Billing Options reads the General Message Billing
    Defaults saved (one shared row); statement e-mails take the subject, body and body type of the patient's clinic, and a statement's own
    subject and body still win; the e-mail type per clinic (an unknown stored number is plain text). Billing Options' Save Defaults beside
    Billing Defaults (review fix): a save for clinic B gives clinic A, which has only its Billing Defaults messages, no row (and billing keeps
    following the practice filters for A); a save for clinic B does not bring back the General Message row of clinic A, set back to the
    practice defaults (A stays on them); only the selected clinic is written; the shared General Message row is updated in place; the practice
    message adds no row; the practice is refused as a clinic. Saving a clinic with some rows (review fix of `p262/billing-2`): a clinic in the
    pre-26.2 Billing Options state (13 filter rows and its General Message) saved with only its General Message changed keeps exactly one of
    the six rows, and its statement e-mails and invoices then follow later practice changes of the subject, body and invoice note; the
    practice body edited and saved in the same window before the clinic is not copied to the clinic; the window's practice values shown for
    the messages without a row are not a change, and only the edited subject is added; changing the body or the type writes both; a clinic
    leaving the practice defaults gets all six rows; editing one message of a clinic with six rows does not write back an unchanged row
    another workstation changed; old settings of another clinic, or none, are refused.
  - `BillingOptionsClinicDefaultsTests` (no database): the 14 preferences, which rows count as saved filter defaults, and the rows a save
    writes (new rows, existing rows updated in place, another clinic's rows and other preferences left out, the practice message).
  - `EbillSettingsTests`, `StatementChoicesTests`, `BillingDefaultsSettingsTests` (no database): the credit card text and its reading for
    every combination, the method list against `BillingUseElectronicEnum`, the output path preferences, the choices and their descriptions,
    the settings object and its stored values, the rows a clinic save writes (all six when leaving the practice defaults, otherwise what
    changed, the body with its type) and which messages follow the practice values in the window.
  - `WiringSourceTests`: the Statement button and the toolbar event, the menu entries' choices, the Statement Options window, every
    control of the Statement window in exactly one container and the limited custom rule, every electronic billing setting saved by the
    new window and none by Billing Defaults, the Billing List's Defaults button, the practice-wide and per-clinic saves of Billing Defaults
    (a clinic saved against its settings as read, and shown with the practice values for the messages it has no row for),
    every reader of a per-clinic setting (no other code reads one directly), Billing Options re-reading the clinic rows, Billing Options'
    Save Defaults writing the selected clinic only and showing a clinic's own General Message, and no migration step at 2026110721 to
    2026110729.
- Changed: `M2DUNTests` (the e-mail type is kept per clinic and saved by `BillingDefaultsByClinic`), `M2SDVTests` (the Service Date View box
  is in the Contents box), `BPRINTTests` (the inserter barcode box is in the Statements box).

## Files touched

- New: `OpenPmsDentalBusiness/Logic/{BillingDefaultsByClinic,BillingOptionsClinicDefaults,EbillSettings,StatementChoices}.cs`,
  `OpenPmsDental/Forms/FormEbillSetup.*`,
  `OpenPmsDental/Forms/FormStatementChoices.*`, `OpenPmsDental/Main Modules/ControlAccount.StatementOptions.cs`, `tests/P262BillingTests/*`,
  this note.
- Changed: `OpenPmsDental/Forms/FormBillingDefaults.cs` and `.Designer.cs` (rewritten), `FormStatementOptions.cs` and `.designer.cs`,
  `FormBilling.cs`, `FormBillingOptions.cs`, `Main Modules/ControlAccount.cs`, `ControlAccount.ServiceDateView.cs`, `UI/ToolBarOD.cs`,
  `OpenPmsDentalBusiness/Data Interface/Statements.cs`, `Logic/BillingEmailHtml.cs`, `Logic/BillingL.cs`, a comment in `TableTypes/Pref.cs`,
  both `.csproj` files, `OpenDental.sln`, `OpenDental.Local.slnf`, `docs/release-notes/next.md`.
