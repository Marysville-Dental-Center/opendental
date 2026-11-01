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
    - `MarkDeclined` is not enough on its own: besides real card declines, Open Dental calls it for answers that are the office's setup, the processor's outage or a charge that went through, and it also drives "Automatically inactivate declined cards", which is kept as it was. So each `MarkDeclined` call is followed by one line that keeps the flag only when the processor's filter (in `RecurringChargerator.FailEmails.cs`) says the card itself was declined (round t3):
    - EdgeExpress (`SendEdgeExpress`, every answer other than Approval): `IsEdgeExpressCardDecline(response.XWebResponseCode)`, true only for Declined (001), AutoDecline (009) and InvalidExpirationDate (010). No e-mail for 805 AuthenticationError (the clinic's X-Web ID, Terminal ID or Auth Key), the 806-808 merchant mode errors and the other 8xx request errors, 813 DuplicateTransactionError (the card was already charged), 818-821 settlement, maintenance and outage, 007 PartialApproval (the card was charged), Pending, or a code this program does not know (Undefined).
    - XWeb gateway (`ProcessCardXWebDTG`): the same codes. `XWebs` throws its `ODException` (`XWebDTGFailed`, "DTG failed (code): description") for every answer other than an approval, and the catch marks it declined when the text contains "decline"; `IsXWebDtgCardDecline(ex)` keeps the flag only when that code is a card decline. The answer branch (reached only for AliasSuccess) uses `IsEdgeExpressCardDecline`.
    - X-Charge client (`ProcessCardXChargeClientProgram`, every result other than SUCCESS, also "Transaction not completed" and a result file with no RESULT line): `IsXChargeClientDecline(resultText)`, true only when the RESULT line itself contains "DECLINE".
    - PayConnect 2 (`SendPayConnectV2`, every error answer PayConnect sent): `IsPayConnect2Decline(response)`, true only for PayConnect's own error (`PayConnectErrorKind.Vendor`) with an HTTP 4xx other than 408, 409 and 429, or a lost answer the transaction list shows as declined (Outcome Reconciled, status "Declined"). No e-mail for a rejected API key or Secret (401, 403), a 400 VALIDATION, any 5xx, missing settings or no answer (those last two already called `MarkFailed`). The `StatusCode!="0"` decline further down stays a decline (an error answer never reaches it: its StatusCode is blank).
    - PaySimple (cards and ACH): `SendPaySimple`'s catch calls `MarkDeclined` for any exception (Open Dental's behaviour, kept so "Automatically inactivate declined cards" works as before), so the line after it keeps the flag only when `RecurringChargerator.IsPaySimpleDecline(ex)` says PaySimple itself answered "not approved" or "declined" (the plain `Exception` that `PaySimpleApi.ToSaleResponse` throws, also when the charge was found by its OrderId after a lost answer). No e-mail for any `ODException` (`PaySimpleResultUnknownException`, whose charge may have gone through; missing credentials or PayType and a disabled program from `ValidateProgram` / `ValidateCredentials`; rejected credentials), PaySimple's error objects (`PaySimpleException`), HTTP and network errors, or an error after the card was charged.
    - Global Payments V2 (V-GP) gets it as soon as its branch calls `MarkDeclined`, followed by its own filter line (see Interplay).
  - A charge that succeeded is never e-mailed: `MarkDeclined` does not flag a charge whose status is already `ChargeSuccessful` (PaySimple's catch also runs for an error after the charge: the card row deleted during the run, the note or the receipt), and `SendIfNeeded` returns NotNeeded for `ChargeSuccessful` whatever the flag says.
  - Recipient: the card's patient when they have a valid address, else the guarantor. Skipped when the card's patient, or the guarantor who would receive it, opted out of e-mail (`commoptout`, All or Statements).
  - Sender: `EmailAddresses.GetByClinic` for the charge's clinic (the practice default when clinics are off), with the clinic's sender name override, sent with `EmailMessages.SendEmail` through that address's own server or its own Gmail / Microsoft 365 authorization. Nothing goes through an Open Dental service (ground rule 6).
  - The user running the charges needs Email Send (the existing check in `EmailMessages` would otherwise skip the send silently and still save the message as sent).
  - Message: plain text (`EmailType.Regular`), `MsgType` PaymentReceipt, filed with the card's patient (`PatNum`), addressed to the recipient (`PatNumSubj`). Tags: `[NameF]`, `[NamePref]` (preferred name, else first name), `[Amount]` (the amount attempted), `[CardLast4]`, `[DateCharge]` (the day of the run), `[OfficeName]`, `[OfficePhone]`, `[ClinicName]` (practice values when clinics are off); the other common tags (`[PracticeName]`, `[ProvName]`, ...) work too. A blank subject or body uses the default text from `Seed26_1`.
  - Quiet period: no second e-mail for the same card within 7 days. Tracked on the `recurringcharge` rows of that card (below).
  - Every outcome adds one line to the charge's `ErrorMsg` ("Failure e-mail sent to ..." or "Failure e-mail not sent: reason"), which the Recurring Charges History window shows. A sent e-mail also adds a commlog (Financial type, e-mail mode) to the card's patient. `EmailMessages.SendEmail` saves the message in every case, as `SendFailed` with the server's answer when sending fails. The existing "Email Sent" security log entry (EmailSend) is written by `EmailMessages`.
  - Never throws: a send failure or any other error is counted, logged at Information level (the manual run shows only Error lines as message boxes) and the batch goes on.
- `OpenDentBusiness/Data Interface/RecurringChargerator.FailEmails.cs` (new partial): `SendFailEmailIfNeeded` (called once per card), the counts `FailEmailsSent`, `FailEmailsNotSent`, `FailEmailsSkipped`, `GetFailEmailSummary`, `ActionSendFailEmail` (the tests replace the sender), and the processor filters `IsPaySimpleDecline`, `IsEdgeExpressCardDecline`, `IsXWebDtgCardDecline`, `IsXChargeClientDecline` and `IsPayConnect2Decline`.
- `OpenDentBusiness/Data Interface/RecurringCharges.cs`: `RecurringChargerator` is now `partial`; `RecurringChargeData.IsDeclinedByProcessor`; `MarkDeclined` sets it unless the charge already succeeded; one line after six of its seven calls keeps it only for the processor's card decline (`SendEdgeExpress`, the X-Charge client, the two in `ProcessCardXWebDTG`, `SendPayConnectV2`'s error branch, `SendPaySimple`'s catch); `SendCharges` calls `SendFailEmailIfNeeded(chargeData)` just before it saves each charge row; `ClearStats` resets the e-mail counts. Eleven small changes.
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
11. Which answers of the other processors are card declines (round t3): EdgeExpress and XWeb 001, 009 and 010 (the codes `XWebResponse.cs` documents as declines and a bad expiration date; every other code is a setup, request, duplicate, outage or still-open answer); an X-Charge client RESULT line containing "DECLINE" (X-Charge's result values are not published); a PayConnect 2 error of its own kind with a 4xx status other than 408, 409 and 429, or a reconciled status "Declined" (PayConnect's statuses for a decline are not published; 409 is left out because a conflict may be a duplicate of a charge that went through).

## Deviations from the spec

- **Failed but not declined: no e-mail.** The spec says "each declined or failed recurring charge". In this code a failure that is not a decline means an office setup problem (no credentials, PayConnect version 1, a missing or duplicate token, X-Charge already running) or an outcome the processor never confirmed (no answer, may have been charged). Because Open Dental's `MarkDeclined` is also called for some of these (PaySimple's catch-all for every exception; EdgeExpress, XWeb and X-Charge for every answer other than an approval, credential errors, outages and duplicates among them; PayConnect 2 for every error answer, a rejected key or Secret and a 5xx among them), this rests on each processor's filter (above), not on `MarkDeclined` alone. E-mailing the patient that the charge "did not go through" would be wrong in the second case and useless in the first, and a setup problem would e-mail every patient in the batch. Declines are e-mailed whatever "Automatically inactivate declined cards" is set to (with it off, a decline keeps the Failed status but still counts). One line in `SendIfNeeded` changes this if the owner wants failures included (owner question 1).
- **Quiet period tracked on `recurringcharge` rows, not commlogs.** The spec's fork decision says "through the existing commlog/emailmessage history". Neither table records the card, and "same charge and card" needs it, so the check looks for the "Failure e-mail sent to" line on the card's recurring charge rows of the last 7 days. The emailmessage and commlog are still written.
- **Per-clinic settings** (the spec's optional `clinicpref`) are not built: one setting and one template for the office.
- **Security lock dates** (26.1 spec rule 4): the 24.3 Repeating Charges tool does not check lock dates (`RepeatCharges.cs` has no lock-date check), and it still does not; with backdating off nothing is dated before today anyway. The hover help M2-02 wrote for the backdating checkbox said "Lock dates apply either way"; round t1 replaced that sentence with what the code does (see the questions).

## Interplay with packages built at the same time

- **M2-02** (on the base): registered and seeded `RecurringChargesFailEmailEnabled` (0), `RecurringChargesFailEmailSubject`, `RecurringChargesFailEmailBody` and `RepeatingChargesAllowBackdating` (1), and their `PrefInfos.xml` entries. Used as they are; nothing was added to any registry. Round t1 corrected the two hover texts (`Details` only; names, categories and control texts unchanged) and the `RecurringChargesFailEmailEnabled` summary in `Pref.cs`.
- **V-GP** (takes `RecurringCharges.cs` and `FormCreditRecurringCharges.cs` over in the next wave): a Global Payments V2 branch in `SendCharges` should report declines through `MarkDeclined` and setup or network problems through `MarkFailed`, and follow its `MarkDeclined` call with one filter line of the same shape as the others: `chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && <is this answer a card decline>;`. The filters this package uses, one per processor, all in `RecurringChargerator.FailEmails.cs`: `IsEdgeExpressCardDecline` (EdgeExpress, and XWeb's answer branch), `IsXWebDtgCardDecline` (XWeb's catch), `IsXChargeClientDecline` (X-Charge client), `IsPayConnect2Decline` (PayConnect 2), `IsPaySimpleDecline` (PaySimple's catch). `RecurUnitTests.Source_EveryMarkDeclinedIsNarrowedByItsProcessorsFilter` counts the `MarkDeclined` calls (7), so a new one fails the test until it has its filter and the count is raised. Keep the `SendFailEmailIfNeeded(chargeData);` line just before `RecurringCharges.Update(chargeData.RecurringCharge);`, the guard in `MarkDeclined`, and the six filter lines when merging.
- **V-PS / PaySimple** (`OpenDentBusiness/WebBridges/PaySimple.cs`, not edited here): `IsPaySimpleDecline` depends on `PaySimpleApi.ToSaleResponse` throwing a plain `Exception` whose message starts "Payment was declined by PaySimple." or "Payment was not approved by PaySimple.", and on every setup or unknown-outcome error being an `ODException`. If that code moves to its own exception type, `IsPaySimpleDecline` should test for the type instead; `PaySimpleDeclineTests` fails if the text changes.
- **M2-DUN** (takes `EmailMessages.cs` over): this package did not edit `EmailMessages.cs` or `EmailAddresses.cs`; it only calls them. The failure e-mail is plain text; if the owner wants it to follow `BillingEmailType` (HTML), that is a small follow-up in `RecurringChargeFailEmails`.
- **M2-PAYW** (`FormPayment`, payment splits): no overlap. The declined charge's $0 payment is created as before.

## Tests

New project `tests/M2RECURTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). It builds a fresh 24.3.63 scratch database (`forkmig_<time>_<pid>_<random>_m2recur`), upgrades it with the fork migration runner (which seeds the preferences) and drops it afterwards. Synthetic patients, cards and a synthetic procedure code (`M2RC1`, not a CDT code); the e-mail sender is replaced, so nothing leaves the computer.

- `FailEmailTests`: tags replaced (including an amount under one dollar), sent from the office address, saved with the patient, commlog, outcome line; guarantor fallback; no address; patient and guarantor opt-outs (and a text-only opt-out that still sends); failure without decline; turned off; quiet period (same card, other card, after 7 days); no office address; no Email Send permission; blank template uses the default; the chargerator counts a send failure, keeps going, saves the failed message and logs without an Error; `MarkDeclined` flags the charge with "inactivate declined cards" off, `MarkFailed` does not; `MarkDeclined` on a charge that already succeeded flags nothing, keeps the status and sends nothing; a flagged charge whose status is `ChargeSuccessful` is not e-mailed.
- `PaySimpleDeclineTests`: one recurring charge at a time through `RecurringChargerator.SendPaySimple` and `SendFailEmailIfNeeded`, in `SendCharges`' order, with PaySimple's requests answered by a fake handler (`PaySimple.SetTestHttpHandler`; nothing reaches the network). PaySimple's "declined" and "not approved" answers e-mail the patient; a dropped connection whose payment cannot be found (`PaySimpleResultUnknownException`) and a practice without credentials (`ValidateProgram`'s `ODException`, two patients, no request sent, card-inactivation status unchanged) e-mail nobody; an approved charge whose card row is deleted during the run (the error after the charge reaches the catch) stays `ChargeSuccessful` and e-mails nobody.
- `BackdatingTests`: the tool with backdating allowed (missed dates posted on their dates) and prevented (nothing before the run date, missed dates never posted later, the next billing date posts, a second run the same day adds nothing, turning it back on posts what is still in the window); `GetBillingDatesHelper`; the Edit Repeating Charge rule; a missing preference row allows backdating.
- `ProcessorDeclineEmailTests` (round t3): recurring charges through `SendEdgeExpress` (requests answered by `XWebs.Mock`) and `SendPayConnect` (answers from `PayConnect2.Mock`, built with the real `PayConnect2.ParseHttpResponse`), then `SendFailEmailIfNeeded`. EdgeExpress 805 AuthenticationError (two patients) and 813 DuplicateTransactionError, PayConnect 401 Secret rejected (two patients), 401 API key rejected and HTTP 500 e-mail nobody, add no outcome line and keep the card-inactivation status; an EdgeExpress 001 decline and a PayConnect 402 "Card declined by issuer" e-mail the patient.
- `RecurUnitTests`: card last four, date filter, `IsPaySimpleDecline` against each kind of exception `SendPaySimple` can catch, `IsEdgeExpressCardDecline` (exactly 001, 009 and 010 of every code), `IsXWebDtgCardDecline`, `IsXChargeClientDecline`, `IsPayConnect2Decline` (each PayConnect error kind and status, reconciled answers, no answer), and source checks of the hooks in `SendCharges`, `MarkDeclined` (the `ChargeSuccessful` guard), each processor's filter line right after its `MarkDeclined` call, `ClearStats`, `GetBillingDatesHelper`, the Preferences control (names and texts as `PrefInfos.xml` gives them), the windows and the project file.

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

## Finish round t3 (branch `port/m2-recur-t3`, from `port/m2-recur-t1`)

### Integration (merged `port/int-9` at `8d077f202`)

- The merge had no conflicts. Since round t1 no merged package touched `RecurringCharges.cs`, `RecurringChargerator.FailEmails.cs`, `RecurringChargeFailEmails.cs`, `RepeatCharges.cs`, `PayConnect2.cs`, `EdgeExpress.cs`, `XWebs.cs`, `PaySimple.cs`, the charge windows or the Preferences control. V-GP and V-PS are still not merged.
- Helpers on the base: the new filters reuse what PayConnect 2 and X-Web already have (`PayConnect2.ParseHttpResponse` / `ParseErrorResponse` and their `PayConnectErrorKind`, `XWebResponse.ConvertResponseCode`); nothing on the base already tells a card decline from the other answers, so nothing is duplicated.
- No fork migration step, no schema, TableTypes or row change, so nothing in `tools/AdoptionValidation` changes.

### Fixes

Two blocking review findings, both real (the same defect, seen from the spec and from the code), fixed together:

1. **The failure e-mail went out for EdgeExpress, XWeb, X-Charge and PayConnect 2 answers that are not card declines.** Round s1 narrowed only PaySimple. `SendEdgeExpress` calls `MarkDeclined` for every EdgeExpress code but Approval, which `ProcessPaymentDirect` returns without throwing (805 AuthenticationError, 806-808, 813 duplicate, 818-821 outages, 007 partial approval, Pending, Undefined); `SendPayConnectV2` for every PayConnect error answer, a 401/403 rejected key or Secret, a 400 VALIDATION and any 5xx among them; the X-Charge client for every RESULT but SUCCESS. With the e-mail on, one bad credential or one outage e-mailed every patient of that processor in the batch, and a duplicate or partial approval e-mailed a patient whose card was charged. The port note's per-processor claim and the hover text were false for these paths. Fix, the PaySimple pattern: `MarkDeclined` and the card-inactivation behaviour are unchanged; one line after each call keeps the flag only for the processor's card decline (`IsEdgeExpressCardDecline`, `IsXWebDtgCardDecline`, `IsXChargeClientDecline`, `IsPayConnect2Decline`; what each accepts is under "What was built"). The XWeb gateway path had the same shape (its catch marks declined any exception whose text says "decline", whatever the gateway's code), so it gets the same code filter.
2. Port note (per-processor list, Deviations rationale, the V-GP interplay with the filter for each processor) and hover text (`PrefInfos.xml`, `Details` only: a credential problem, an outage and a duplicate are named among the failures that send no e-mail) now say what the code does.

Tests: `ProcessorDeclineEmailTests` (new: EdgeExpress through `XWebs.Mock`, PayConnect 2 through `PayConnect2.Mock`, setup and outage answers e-mail nobody in a two-patient batch, declines e-mail the patient), the four filters and the source check of every `MarkDeclined` call in `RecurUnitTests`. With two of the new filter lines removed, six of the new tests fail.

### Verification

- One full build through the build lane: 0 errors.
- Targeted while iterating: M2RECURTests 48/48.
- `run-tests -Mode Core` (no migration, schema or adoption change, so without the slow assemblies): 66 assemblies, no failures; M2RECURTests 48/48, VendorDirectTests 873/873, M202Tests 39/39, NoHqTests 49/49. Summary in `.git/od-tools/test-results/port_m2-recur-t3.txt`.
- `tools/Test-NoHqHosts.ps1` passed.

## Questions for the owner

1. Should a failed charge that was not declined (office setup problem, or no answer from the processor) also e-mail the patient, as the spec's wording suggests? This fork says no (see Deviations), and the hover text now says so. If yes, one line in `SendIfNeeded` and the hover text change.
2. Is the 7-day quiet period per card right, or should it follow the retry schedule the office uses?
3. Should the e-mail be per clinic (separate on/off and template per clinic)?
4. The Repeating Charges tool has never checked lock dates, and the hover text no longer claims it does. Should the tool start refusing dates before a lock date (a behavior change for offices that keep backdating on)?
5. This fork has no automatic runner for recurring or repeating charges (OpenDentalService is gone), although the preferences `RecurringChargesAutomatedEnabled` and `RepeatingChargesAutomated` are still shown. With backdating off, the Repeating Charges tool must be run on each billing date. Is a local scheduled runner wanted?
6. Seen while fixing round t3, outside this package: `SendPayConnectV2` (Open Dental's code) posts a 2xx create answer as an approved charge whatever its status, although `PayConnect2.GetCreateStatusNotApproved` (PayConnect 2 parity) treats a 2xx with status "Declined" as not approved and logs it. If PayConnect ever answers a recurring sale that way, the charge is recorded as paid and no failure e-mail goes out. Should the recurring run treat that status as a decline? Not changed here: it changes payment posting, which belongs to the PayConnect work.

## Integration (branch `port/int-12`)

- Merged onto `port/int-11` after M2-SDV (then M2-EMP), with no conflicts and no integration commit. Since `port/int-9` the integration branch gained M2-AUDIT, M2-MISC, M2-CARRIER, M2-HIST and M2-SDV; none of them changed `RecurringCharges.cs`, `RecurringChargerator.FailEmails.cs`, `RecurringChargeFailEmails.cs`, `RepeatCharges.cs`, `RepeatChargeBackdating.cs`, the processors' files (`PayConnect2.cs`, `EdgeExpress.cs`, `XWebs.cs`, `PaySimple.cs`), `EmailMessages.cs`, `EmailAddresses.cs`, the charge windows or the Preferences control. The only change outside this package's files is the summary comment of `PrefName.RecurringChargesFailEmailEnabled` in `Pref.cs` (comment text only; no name, value type or seed changed). Project and solution files keep every package's entries.
- `RecurUnitTests.Source_EveryMarkDeclinedIsNarrowedByItsProcessorsFilter` still counts 7 `MarkDeclined` calls on the merged head. V-GP and V-PS are still not merged; what they must keep is under "Interplay" above.
- No duplicate helpers: `GetCardLast4` stays beside PayConnect's `GetLast4` for the reason given in round t1, and no package merged since adds a card-number or e-mail helper. **M2-SDV**: the declined charge's $0 payment shows in the Service Date View like any other payment; no shared file. **M2-EMP, M2-AUDIT, M2-MISC, M2-CARRIER, M2-HIST**: nothing in common.
- Still no fork migration step, so the adoption tools need nothing from this package. On the merged head (`port/int-12`): one full build through the build lane (0 errors), `run-tests -Mode Full` (76 assemblies, 3980 tests, none failed; the 2 not passed are M201Tests' published-document checks; `test-results/port_int-12.txt`) and `tools/Test-NoHqHosts.ps1` (passed). `M2RECURTests` 48 of 48, `M2DUNTests` 35, `M202Tests` 39, `BPAYTests` 72.

## Integration (branch `port/int-13`)

- **V-GP** merged (with M2-NOTES and V-BRIDGE2). One conflict in `SendEdgeExpress`: V-GP renamed "Response from EdgeExpress:" to "Response from Global Payments V1:"; the renamed text is kept, with this package's `IsEdgeExpressCardDecline` filter line right after the `MarkDeclined` call. `RecurUnitTests.Source_EveryMarkDeclinedIsNarrowedByItsProcessorsFilter` and `ProcessorDeclineEmailTests`' three EdgeExpress tests now expect the renamed text (the rules they check are unchanged), and the count stays 7 (V2's call is in its own partial file).
- V2's `MarkDeclined` is reached only for a card or issuer decline (`GpV2Outcome.Declined`); refused requests, setup problems, duplicates and unknown outcomes use `MarkFailed`. So it needs no filter line; the new `RecurUnitTests.Source_GlobalPaymentsV2_MarkDeclinedOnlyForACardDecline` keeps it that way. `SendFailEmailIfNeeded` runs after the V2 branch in `SendCharges`, as after every other processor.
- On the merged head: one full build (0 errors), `run-tests -Mode Full` (79 assemblies, 4174 tests, none failed; 2 skipped in M201Tests) and the host check passed. `M2RECURTests` 49 of 49.
