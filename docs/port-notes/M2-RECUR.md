# Port note: M2-RECUR, recurring and repeating charges

Branch `port/m2-recur`, cut from `port/foundation` (which holds M2-01 and M2-02). Built in the single-wave run, beside the other packages. Backlog section: [port-backlog.md, M2-RECUR](../specs/port-backlog.md#m2-recur-recurring-and-repeating-charges). No fork migration step was needed, so none of the level range 2027003001 to 2027003099 is used.

## Sources

Public sources only. The 25.4 release note (https://www.opendental.com/site/version25_4.html) says only that a recurring charge that fails can now send an e-mail automatically. The 26.1 release note (https://www.opendental.com/site/version26_1.html) says only that a new Account preference allows or prevents backdated repeating charges. The public Recurring Charges manual page (`manual/recurringcharges.html`, still version 24.4) does not mention the e-mail, and the Repeating Charges page the spec cites mentions no backdating preference. Everything else below is this fork's choice, from the feature specs (`docs/specs/features-25.4.md`, `features-26.1.md`) and marked INFERRED where it goes beyond them. No Open Dental program was installed, observed or decompiled; no office data was used.

## Features

| ID | Status | Notes |
|---|---|---|
| `v25_4-account-recurringcharge-fail-email` | built | E-mail through the office's own address when the card processor declines a recurring charge; template with the eight tags; 7-day quiet period; emailmessage row, commlog, outcome line on the charge; counts in the run summary. One deviation from the spec (failures that are not declines get no e-mail, below). Per-clinic settings (the spec's optional `clinicpref`) not built. |
| `v26_1-account-repeatcharge-backdate-pref` | built | `RepeatingChargesAllowBackdating` honored by the Repeating Charges tool; checkbox in Preferences; note in Edit Repeating Charge; tool description follows the preference. |

## What was built

### 25.4: e-mail when a recurring charge fails

- `OpenDentBusiness/Data Interface/RecurringChargeFailEmails.cs` (new): the rules.
  - Due only when `RecurringChargesFailEmailEnabled` is on and the processor answered with a decline (`RecurringChargeData.IsDeclinedByProcessor`, set in `RecurringChargerator.MarkDeclined`), and never for a charge whose status is `ChargeSuccessful`. Per processor:
    - EdgeExpress, X-Charge, XWeb and PayConnect 2: every `MarkDeclined` call is a decline the processor reported (their setup, network and unknown-outcome paths already call `MarkFailed`).
    - PaySimple (cards and ACH): `SendPaySimple`'s catch calls `MarkDeclined` for any exception (Open Dental's behaviour, kept so "Automatically inactivate declined cards" works as before), so the line after it keeps the flag only when `RecurringChargerator.IsPaySimpleDecline(ex)` says PaySimple itself answered "not approved" or "declined" (the plain `Exception` that `PaySimpleApi.ToSaleResponse` throws, also when the charge was found by its OrderId after a lost answer). No e-mail for any `ODException` (`PaySimpleResultUnknownException`, whose charge may have gone through; missing credentials or PayType and a disabled program from `ValidateProgram` / `ValidateCredentials`; rejected credentials), PaySimple's error objects (`PaySimpleException`), HTTP and network errors, or an error after the card was charged.
    - Global Payments V2 (V-GP) gets it as soon as its branch calls `MarkDeclined` for real declines only.
  - A charge that succeeded is never e-mailed: `MarkDeclined` does not flag a charge whose status is already `ChargeSuccessful` (PaySimple's catch also runs for an error after the charge: the card row deleted during the run, the note or the receipt), and `SendIfNeeded` returns NotNeeded for `ChargeSuccessful` whatever the flag says.
  - Recipient: the card's patient when they have a valid address, else the guarantor. Skipped when the card's patient, or the guarantor who would receive it, opted out of e-mail (`commoptout`, All or Statements).
  - Sender: `EmailAddresses.GetByClinic` for the charge's clinic (the practice default when clinics are off), with the clinic's sender name override, sent with `EmailMessages.SendEmail` through that address's own server or its own Gmail / Microsoft 365 authorization. Nothing goes through an Open Dental service (ground rule 6).
  - The user running the charges needs Email Send (the existing check in `EmailMessages` would otherwise skip the send silently and still save the message as sent).
  - Message: plain text (`EmailType.Regular`), `MsgType` PaymentReceipt, filed with the card's patient (`PatNum`), addressed to the recipient (`PatNumSubj`). Tags: `[NameF]`, `[NamePref]` (preferred name, else first name), `[Amount]` (the amount attempted), `[CardLast4]`, `[DateCharge]` (the day of the run), `[OfficeName]`, `[OfficePhone]`, `[ClinicName]` (practice values when clinics are off); the other common tags (`[PracticeName]`, `[ProvName]`, ...) work too. A blank subject or body uses the default text from `Seed26_1`.
  - Quiet period: no second e-mail for the same card within 7 days. Tracked on the `recurringcharge` rows of that card (below).
  - Every outcome adds one line to the charge's `ErrorMsg` ("Failure e-mail sent to ..." or "Failure e-mail not sent: reason"), which the Recurring Charges History window shows. A sent e-mail also adds a commlog (Financial type, e-mail mode) to the card's patient. `EmailMessages.SendEmail` saves the message in every case, as `SendFailed` with the server's answer when sending fails. The existing "Email Sent" security log entry (EmailSend) is written by `EmailMessages`.
  - Never throws: a send failure or any other error is counted, logged at Information level (the manual run shows only Error lines as message boxes) and the batch goes on.
- `OpenDentBusiness/Data Interface/RecurringChargerator.FailEmails.cs` (new partial): `SendFailEmailIfNeeded` (called once per card), the counts `FailEmailsSent`, `FailEmailsNotSent`, `FailEmailsSkipped`, `GetFailEmailSummary`, `ActionSendFailEmail` (the tests replace the sender), and `IsPaySimpleDecline`.
- `OpenDentBusiness/Data Interface/RecurringCharges.cs`: `RecurringChargerator` is now `partial`; `RecurringChargeData.IsDeclinedByProcessor`; `MarkDeclined` sets it unless the charge already succeeded; `SendPaySimple`'s catch keeps it only for `IsPaySimpleDecline(ex)` (one line after its `MarkDeclined` call); `SendCharges` calls `SendFailEmailIfNeeded(chargeData)` just before it saves each charge row; `ClearStats` resets the e-mail counts. Six small changes.
- `OpenDental/Forms/FormRecurringChargeFailEmailTemplate.cs` and `.Designer.cs` (new): the Edit Template window (subject, body, the tag list, Use Default). It saves nothing itself; the Preferences window saves the text with the other preferences.
- `OpenDental/User Controls/Preferences/UserControlAccountRecAndRepCharges.cs` and designer: "Email patients when a recurring charge fails" (`checkRecurringChargesFailEmailEnabled`, the name `PrefInfos.xml` gives it) and the Edit Template button in the Recurring Charges group.
- `OpenDental/Forms/FormCreditRecurringCharges.cs`: the "Done charging cards" message adds "Failure e-mails sent" and "not sent" (and the quiet-period skips when there are any) when the e-mail is on.

### 26.1: backdating preference

- `OpenDentBusiness/Data Interface/RepeatChargeBackdating.cs` (new): `IsAllowed` (`RepeatingChargesAllowBackdating`; a missing row counts as allowed), `RemoveBackdatedDates`, `IsStartDateSkipped`.
- `OpenDentBusiness/Data Interface/RepeatCharges.cs`: one call in `GetBillingDatesHelper`, after "remove any dates after today": with backdating off, every billing date before the day of the run is removed too. The tool, and any automatic runner, goes through this method; duplicate detection (`IsRepeatDateHelper`) is untouched.
- `UserControlAccountRecAndRepCharges`: "Allow repeating charges to be backdated" (`checkRepeatingChargesAllowBackdating`) in the Repeating Charges group.
- `FormRepeatChargeEdit.cs`: on Save, when backdating is off and a new (or changed) start date is before today, a note says that no charge is posted for a billing date before today.
- `FormRepeatChargesUpdate.cs`: the tool's description says "backdated up to one month and 20 days" only when backdating is allowed; otherwise it says that passed billing dates are skipped and the tool must run on each billing date.

### Project files

- `OpenDentBusiness/OpenDentBusiness.csproj`: three `Compile` lines next to `RecurringCharges.cs`.
- `OpenDental/OpenDental.csproj`: two `Compile` lines for the template window (no .resx; the window has no resources).
- `OpenDental.sln`, `OpenDental.Local.slnf`: the test project.

## INFERRED behaviours

1. Which charges are e-mailed: only those the processor declined (see Deviations). For PaySimple, "declined" means PaySimple's own "not approved" or "declined" answer to the charge; the message text of `PaySimpleApi.ToSaleResponse` is what tells it apart (PaySimple.cs throws a plain `Exception` for it and was not changed), and `PaySimpleDeclineTests` runs charges through that code so a change to the text fails a test.
2. Recipient rule, opt-out types (All or Statements, since there is no recurring-charge opt-out type), and that the name tags describe the recipient while the e-mail is filed with the card's patient.
3. Sender: the charge's clinic default address with the existing fallbacks; the clinic's sender name override applies.
4. `[Amount]` is the amount attempted, `[DateCharge]` the day of the run, `[CardLast4]` the last four characters of the masked number (bank accounts too, for PaySimple ACH).
5. `MsgType` PaymentReceipt: the published 25.4 and 26.1 documents add no `EmailMessageSource` value, so an existing one is used.
6. Quiet period: 7 days per card, counted from the last e-mail actually sent; a skipped retry does not extend it.
7. The runner needs Email Send; without it every e-mail is counted as not sent.
8. Commlog only for a sent e-mail (a failed send leaves its saved `SendFailed` message instead); commlog source User with the runner's user number.
9. Backdating off means skipping, not re-dating: a passed billing date is never posted, and the next billing date posts normally. Re-dating a missed charge to today would break duplicate detection, which matches procedures to the scheduled billing dates.
10. The Edit Repeating Charge note appears only for a new charge or a changed start date, so editing an old charge does not repeat it.

## Deviations from the spec

- **Failed but not declined: no e-mail.** The spec says "each declined or failed recurring charge". In this code a failure that is not a decline means an office setup problem (no credentials, PayConnect version 1, a missing or duplicate token, X-Charge already running) or an outcome the processor never confirmed (no answer, may have been charged). For PaySimple, whose catch-all marks every exception declined, this rests on `IsPaySimpleDecline` (above), not on `MarkDeclined` alone. E-mailing the patient that the charge "did not go through" would be wrong in the second case and useless in the first, and a setup problem would e-mail every patient in the batch. Declines are e-mailed whatever "Automatically inactivate declined cards" is set to (with it off, a decline keeps the Failed status but still counts). One line in `SendIfNeeded` changes this if the owner wants failures included (owner question 1).
- **Quiet period tracked on `recurringcharge` rows, not commlogs.** The spec's fork decision says "through the existing commlog/emailmessage history". Neither table records the card, and "same charge and card" needs it, so the check looks for the "Failure e-mail sent to" line on the card's recurring charge rows of the last 7 days. The emailmessage and commlog are still written.
- **Per-clinic settings** (the spec's optional `clinicpref`) are not built: one setting and one template for the office.
- **Security lock dates** (26.1 spec rule 4): the 24.3 Repeating Charges tool does not check lock dates (`RepeatCharges.cs` has no lock-date check), and it still does not; with backdating off nothing is dated before today anyway. The hover help M2-02 wrote for the backdating checkbox said "Lock dates apply either way"; round t1 replaced that sentence with what the code does (see the questions).

## Interplay with packages built at the same time

- **M2-02** (on the base): registered and seeded `RecurringChargesFailEmailEnabled` (0), `RecurringChargesFailEmailSubject`, `RecurringChargesFailEmailBody` and `RepeatingChargesAllowBackdating` (1), and their `PrefInfos.xml` entries. Used as they are; nothing was added to any registry. Round t1 corrected the two hover texts (`Details` only; names, categories and control texts unchanged) and the `RecurringChargesFailEmailEnabled` summary in `Pref.cs`.
- **V-GP** (takes `RecurringCharges.cs` and `FormCreditRecurringCharges.cs` over in the next wave): a Global Payments V2 branch in `SendCharges` should report declines through `MarkDeclined` and setup or network problems through `MarkFailed`; the e-mail then follows with no other change. A catch-all that calls `MarkDeclined` for any exception (as PaySimple's does) needs a check like `IsPaySimpleDecline` after it. Keep the `SendFailEmailIfNeeded(chargeData);` line just before `RecurringCharges.Update(chargeData.RecurringCharge);`, the guard in `MarkDeclined`, and the `IsPaySimpleDecline` line in `SendPaySimple`'s catch when merging.
- **V-PS / PaySimple** (`OpenDentBusiness/WebBridges/PaySimple.cs`, not edited here): `IsPaySimpleDecline` depends on `PaySimpleApi.ToSaleResponse` throwing a plain `Exception` whose message starts "Payment was declined by PaySimple." or "Payment was not approved by PaySimple.", and on every setup or unknown-outcome error being an `ODException`. If that code moves to its own exception type, `IsPaySimpleDecline` should test for the type instead; `PaySimpleDeclineTests` fails if the text changes.
- **M2-DUN** (takes `EmailMessages.cs` over): this package did not edit `EmailMessages.cs` or `EmailAddresses.cs`; it only calls them. The failure e-mail is plain text; if the owner wants it to follow `BillingEmailType` (HTML), that is a small follow-up in `RecurringChargeFailEmails`.
- **M2-PAYW** (`FormPayment`, payment splits): no overlap. The declined charge's $0 payment is created as before.

## Tests

New project `tests/M2RECURTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). It builds a fresh 24.3.63 scratch database (`forkmig_<time>_<pid>_<random>_m2recur`), upgrades it with the fork migration runner (which seeds the preferences) and drops it afterwards. Synthetic patients, cards and a synthetic procedure code (`M2RC1`, not a CDT code); the e-mail sender is replaced, so nothing leaves the computer.

- `FailEmailTests`: tags replaced (including an amount under one dollar), sent from the office address, saved with the patient, commlog, outcome line; guarantor fallback; no address; patient and guarantor opt-outs (and a text-only opt-out that still sends); failure without decline; turned off; quiet period (same card, other card, after 7 days); no office address; no Email Send permission; blank template uses the default; the chargerator counts a send failure, keeps going, saves the failed message and logs without an Error; `MarkDeclined` flags the charge with "inactivate declined cards" off, `MarkFailed` does not; `MarkDeclined` on a charge that already succeeded flags nothing, keeps the status and sends nothing; a flagged charge whose status is `ChargeSuccessful` is not e-mailed.
- `PaySimpleDeclineTests`: one recurring charge at a time through `RecurringChargerator.SendPaySimple` and `SendFailEmailIfNeeded`, in `SendCharges`' order, with PaySimple's requests answered by a fake handler (`PaySimple.SetTestHttpHandler`; nothing reaches the network). PaySimple's "declined" and "not approved" answers e-mail the patient; a dropped connection whose payment cannot be found (`PaySimpleResultUnknownException`) and a practice without credentials (`ValidateProgram`'s `ODException`, two patients, no request sent, card-inactivation status unchanged) e-mail nobody; an approved charge whose card row is deleted during the run (the error after the charge reaches the catch) stays `ChargeSuccessful` and e-mails nobody.
- `BackdatingTests`: the tool with backdating allowed (missed dates posted on their dates) and prevented (nothing before the run date, missed dates never posted later, the next billing date posts, a second run the same day adds nothing, turning it back on posts what is still in the window); `GetBillingDatesHelper`; the Edit Repeating Charge rule; a missing preference row allows backdating.
- `RecurUnitTests`: card last four, date filter, `IsPaySimpleDecline` against each kind of exception `SendPaySimple` can catch, and source checks of the hooks in `SendCharges`, `MarkDeclined` (the `ChargeSuccessful` guard), `SendPaySimple`'s catch, `ClearStats`, `GetBillingDatesHelper`, the Preferences control (names and texts as `PrefInfos.xml` gives them), the windows and the project file.

Existing tests: `UnitTests/UnitTests/RecurringChargesTests.cs` and `RepeatChargesTests.cs` (the backlog's suggested home) belong to the old UnitTests harness, which the shared runner does not run; they were not changed. Related assemblies to run: `M2RECURTests`, `VendorDirectTests` (covers `RecurringChargerator` with PayConnect), `M202Tests` (the preferences).

Not tested: the WinForms windows themselves (the GUI is not launched; the hooks are source-checked), and a real send through an SMTP, Gmail or Microsoft 365 account.

## Fix round s1 (branch `port/m2-recur-s1`)

Two review findings, both real, both fixed:

1. PaySimple's catch-all marked every exception declined, so with the e-mail on, a charge whose result was unknown (and may have been charged), a clinic without PaySimple credentials, or a network error e-mailed the patient that the charge did not go through. Now only PaySimple's own decline keeps the flag (`IsPaySimpleDecline`); the card-inactivation behaviour is unchanged.
2. `MarkDeclined` flagged a charge that had already succeeded (PaySimple's catch also runs for an error after the charge), which would have e-mailed a patient whose card was charged. Now `MarkDeclined` leaves the flag off for `ChargeSuccessful`, and `SendIfNeeded` returns NotNeeded for `ChargeSuccessful` as a second guard.

Tests: `PaySimpleDeclineTests` (new), two new `FailEmailTests` cases, `IsPaySimpleDecline` and the hook checks in `RecurUnitTests`. Targeted run: M2RECURTests 36/36, VendorDirectTests 873/873, M202Tests 39/39, NoHqTests 49/49; `tools/Test-NoHqHosts.ps1` passed. No fork migration step, no registry change.

## Finish round t1 (branch `port/m2-recur-t1`)

### Integration (merged `port/int-7` at `1763cc7ae`)

- The merge had no conflicts. Since `port/m2-recur-s1` was cut, no merged package touched `RecurringCharges.cs`, `RepeatCharges.cs`, `EmailAddresses.cs`, `PaySimple.cs`, the three charge windows or the Preferences control; B-PRINT changed one line of `EmailMessages.cs` (the statement PDF's name), which the failure e-mail does not use, and M2-DUN did not edit it. The `.sln` and `.slnf` keep the test project with no duplicate project GUIDs.
- Helpers on the base: nothing merged duplicates this package's code. `PayConnectErrorLog.GetLast4` (PayConnect 2, already on main) also takes the last four characters, but it belongs to the PayConnect log and drops anything that is not a letter or a digit; the e-mail keeps its own `GetCardLast4` for the masked number every processor stores. M2-DUN's `BillingEmailHtml.ApplyEmailType` is for HTML billing e-mail; the failure e-mail stays plain text (M2-DUN's note says the same), so it is not used.
- V-GP and V-PS are not merged yet; what they must keep is under "Interplay" above.
- No fork migration step, no schema or row change on an adopted database (the four preference rows are M2-02's `Seed26_1`, which the adoption tools already cover), so nothing in `tools/AdoptionValidation` changes.

### Fixes

1. Hover help (`PrefInfos.xml`, owned by M2-02, which is merged and done): the failure e-mail text said "Each declined or failed recurring charge"; it now says that a declined charge sends one, the same card is not e-mailed again for 7 days, and a failure for another reason sends none. The backdating text said "Lock dates apply either way", which no code does; it now says that a missed billing date is skipped and the next one posts normally. `Pref.cs`'s summary of `RecurringChargesFailEmailEnabled` follows.
2. Preferences, Rec and Rep Charges: the control loaded the template with the default text filled in, so saving the window wrote the default into a blank preference (and froze an older default). It now loads the stored text; the Edit Template window shows the default for a blank one.
3. Preferences: with "Hide repeating charges" (`EasyHideRepeatCharges`) on, the backdating box is disabled like the other repeating charge settings.

### Verification

- One full build through the build lane: 0 errors.
- Targeted: M2RECURTests 36/36 (two new source checks in an existing test), M202Tests 39/39, VendorDirectTests 873/873, NoHqTests 49/49.
- `run-tests -Mode Core` with ForkMigrationTests, M201Tests, SyntheticData.Tests and AdoptionValidation.Tests (`Pref.cs` is a TableTypes file, comment only): 65 assemblies, no failures (M201Tests 24 of 26 passed; the 2 skipped ones compare with Open Dental's published database documents, which `M201_OD_DOCS_DIR` points to and this PC does not set); M2RECURTests 36/36, ForkMigrationTests 38/38, AdoptionValidation.Tests 56/56, SyntheticData.Tests 53/53. Summary in `.git/od-tools/test-results/port_m2-recur-t1.txt`.
- `tools/Test-NoHqHosts.ps1` passed.

## Questions for the owner

1. Should a failed charge that was not declined (office setup problem, or no answer from the processor) also e-mail the patient, as the spec's wording suggests? This fork says no (see Deviations), and the hover text now says so. If yes, one line in `SendIfNeeded` and the hover text change.
2. Is the 7-day quiet period per card right, or should it follow the retry schedule the office uses?
3. Should the e-mail be per clinic (separate on/off and template per clinic)?
4. The Repeating Charges tool has never checked lock dates, and the hover text no longer claims it does. Should the tool start refusing dates before a lock date (a behavior change for offices that keep backdating on)?
5. This fork has no automatic runner for recurring or repeating charges (OpenDentalService is gone), although the preferences `RecurringChargesAutomatedEnabled` and `RepeatingChargesAutomated` are still shown. With backdating off, the Repeating Charges tool must be run on each billing date. Is a local scheduled runner wanted?
