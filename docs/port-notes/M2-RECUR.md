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
  - Due only when `RecurringChargesFailEmailEnabled` is on and the processor answered with a decline (`RecurringChargeData.IsDeclinedByProcessor`, set in `RecurringChargerator.MarkDeclined`). This covers every processor that reports declines through `MarkDeclined`: EdgeExpress, X-Charge and XWeb, PayConnect 2, PaySimple (cards and ACH). Global Payments V2 (V-GP) gets it as soon as its branch calls `MarkDeclined`.
  - Recipient: the card's patient when they have a valid address, else the guarantor. Skipped when the card's patient, or the guarantor who would receive it, opted out of e-mail (`commoptout`, All or Statements).
  - Sender: `EmailAddresses.GetByClinic` for the charge's clinic (the practice default when clinics are off), with the clinic's sender name override, sent with `EmailMessages.SendEmail` through that address's own server or its own Gmail / Microsoft 365 authorization. Nothing goes through an Open Dental service (ground rule 6).
  - The user running the charges needs Email Send (the existing check in `EmailMessages` would otherwise skip the send silently and still save the message as sent).
  - Message: plain text (`EmailType.Regular`), `MsgType` PaymentReceipt, filed with the card's patient (`PatNum`), addressed to the recipient (`PatNumSubj`). Tags: `[NameF]`, `[NamePref]` (preferred name, else first name), `[Amount]` (the amount attempted), `[CardLast4]`, `[DateCharge]` (the day of the run), `[OfficeName]`, `[OfficePhone]`, `[ClinicName]` (practice values when clinics are off); the other common tags (`[PracticeName]`, `[ProvName]`, ...) work too. A blank subject or body uses the default text from `Seed26_1`.
  - Quiet period: no second e-mail for the same card within 7 days. Tracked on the `recurringcharge` rows of that card (below).
  - Every outcome adds one line to the charge's `ErrorMsg` ("Failure e-mail sent to ..." or "Failure e-mail not sent: reason"), which the Recurring Charges History window shows. A sent e-mail also adds a commlog (Financial type, e-mail mode) to the card's patient. `EmailMessages.SendEmail` saves the message in every case, as `SendFailed` with the server's answer when sending fails. The existing "Email Sent" security log entry (EmailSend) is written by `EmailMessages`.
  - Never throws: a send failure or any other error is counted, logged at Information level (the manual run shows only Error lines as message boxes) and the batch goes on.
- `OpenDentBusiness/Data Interface/RecurringChargerator.FailEmails.cs` (new partial): `SendFailEmailIfNeeded` (called once per card), the counts `FailEmailsSent`, `FailEmailsNotSent`, `FailEmailsSkipped`, `GetFailEmailSummary`, and `ActionSendFailEmail` (the tests replace the sender).
- `OpenDentBusiness/Data Interface/RecurringCharges.cs`: `RecurringChargerator` is now `partial`; `RecurringChargeData.IsDeclinedByProcessor`; `MarkDeclined` sets it; `SendCharges` calls `SendFailEmailIfNeeded(chargeData)` just before it saves each charge row; `ClearStats` resets the e-mail counts. Five one-line changes.
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

1. Which charges are e-mailed: only those the processor declined (see Deviations).
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

- **Failed but not declined: no e-mail.** The spec says "each declined or failed recurring charge". In this code a failure that is not a decline means an office setup problem (no credentials, PayConnect version 1, a missing or duplicate token, X-Charge already running) or an outcome the processor never confirmed (no answer, may have been charged). E-mailing the patient that the charge "did not go through" would be wrong in the second case and useless in the first, and a setup problem would e-mail every patient in the batch. Declines are e-mailed whatever "Automatically inactivate declined cards" is set to (with it off, a decline keeps the Failed status but still counts). One line in `SendIfNeeded` changes this if the owner wants failures included (owner question 1).
- **Quiet period tracked on `recurringcharge` rows, not commlogs.** The spec's fork decision says "through the existing commlog/emailmessage history". Neither table records the card, and "same charge and card" needs it, so the check looks for the "Failure e-mail sent to" line on the card's recurring charge rows of the last 7 days. The emailmessage and commlog are still written.
- **Per-clinic settings** (the spec's optional `clinicpref`) are not built: one setting and one template for the office.
- **Security lock dates** (26.1 spec rule 4): the 24.3 Repeating Charges tool does not check lock dates, and it still does not; with backdating off nothing is dated before today anyway. The hover help that M2-02 wrote for the backdating checkbox in `PrefInfos.xml` says "Lock dates apply either way"; that sentence is not true of this code (see the questions).

## Interplay with packages built at the same time

- **M2-02** (on the base): registered and seeded `RecurringChargesFailEmailEnabled` (0), `RecurringChargesFailEmailSubject`, `RecurringChargesFailEmailBody` and `RepeatingChargesAllowBackdating` (1), and their `PrefInfos.xml` entries. Used as they are; nothing was added to any registry. Two hover texts in `PrefInfos.xml` need M2-02's wording fix (questions 1 and 4).
- **V-GP** (takes `RecurringCharges.cs` and `FormCreditRecurringCharges.cs` over in the next wave): a Global Payments V2 branch in `SendCharges` should report declines through `MarkDeclined` and setup or network problems through `MarkFailed`; the e-mail then follows with no other change. Keep the `SendFailEmailIfNeeded(chargeData);` line just before `RecurringCharges.Update(chargeData.RecurringCharge);` when merging.
- **M2-DUN** (takes `EmailMessages.cs` over): this package did not edit `EmailMessages.cs` or `EmailAddresses.cs`; it only calls them. The failure e-mail is plain text; if the owner wants it to follow `BillingEmailType` (HTML), that is a small follow-up in `RecurringChargeFailEmails`.
- **M2-PAYW** (`FormPayment`, payment splits): no overlap. The declined charge's $0 payment is created as before.

## Tests

New project `tests/M2RECURTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). It builds a fresh 24.3.63 scratch database (`forkmig_<time>_<pid>_<random>_m2recur`), upgrades it with the fork migration runner (which seeds the preferences) and drops it afterwards. Synthetic patients, cards and a synthetic procedure code (`M2RC1`, not a CDT code); the e-mail sender is replaced, so nothing leaves the computer.

- `FailEmailTests`: tags replaced (including an amount under one dollar), sent from the office address, saved with the patient, commlog, outcome line; guarantor fallback; no address; patient and guarantor opt-outs (and a text-only opt-out that still sends); failure without decline; turned off; quiet period (same card, other card, after 7 days); no office address; no Email Send permission; blank template uses the default; the chargerator counts a send failure, keeps going, saves the failed message and logs without an Error; `MarkDeclined` flags the charge with "inactivate declined cards" off, `MarkFailed` does not.
- `BackdatingTests`: the tool with backdating allowed (missed dates posted on their dates) and prevented (nothing before the run date, missed dates never posted later, the next billing date posts, a second run the same day adds nothing, turning it back on posts what is still in the window); `GetBillingDatesHelper`; the Edit Repeating Charge rule; a missing preference row allows backdating.
- `RecurUnitTests`: card last four, date filter, and source checks of the hooks in `SendCharges`, `MarkDeclined`, `ClearStats`, `GetBillingDatesHelper`, the Preferences control (names and texts as `PrefInfos.xml` gives them), the windows and the project file.

Existing tests: `UnitTests/UnitTests/RecurringChargesTests.cs` and `RepeatChargesTests.cs` (the backlog's suggested home) belong to the old UnitTests harness, which the shared runner does not run; they were not changed. Related assemblies to run: `M2RECURTests`, `VendorDirectTests` (covers `RecurringChargerator` with PayConnect), `M202Tests` (the preferences).

Not tested: the WinForms windows themselves (the GUI is not launched; the hooks are source-checked), and a real send through an SMTP, Gmail or Microsoft 365 account.

## Questions for the owner

1. Should a failed charge that was not declined (office setup problem, or no answer from the processor) also e-mail the patient, as the spec's wording suggests? This fork says no (see Deviations). If no, M2-02's hover text "Each declined or failed recurring charge sends one e-mail" should say "declined".
2. Is the 7-day quiet period per card right, or should it follow the retry schedule the office uses?
3. Should the e-mail be per clinic (separate on/off and template per clinic)?
4. The backdating hover text says "Lock dates apply either way", but the Repeating Charges tool has never checked lock dates. Remove the sentence, or should the tool start refusing dates before a lock date (a behavior change for offices that keep backdating on)?
5. This fork has no automatic runner for recurring or repeating charges (OpenDentalService is gone), although the preferences `RecurringChargesAutomatedEnabled` and `RepeatingChargesAutomated` are still shown. With backdating off, the Repeating Charges tool must be run on each billing date. Is a local scheduled runner wanted?
