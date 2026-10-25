# Port note: M2-DUN, HTML dunning and billing e-mail

Branch `port/m2-dun`, cut from `port/foundation` (which holds M2-01 and M2-02). Built in the single-wave run, beside the other packages. Backlog section: [port-backlog.md, M2-DUN](../specs/port-backlog.md#m2-dun-html-dunning-and-billing-e-mail). Feature spec: [features-26.1.md, v26_1-manage-dunning-html](../specs/features-26.1.md#v26_1-manage-dunning-html-dunning-messages-and-billing-e-mail-template-support-html). No fork migration step: the schema and the preference were already on the base.

## Sources

Public sources only: the 26.1 release notes and the 26.1 highlights post (billing e-mails and dunning messages can be HTML), `OpenDentalDiffDocumentation26-1.xml` (`dunning.EmailType` with Regular 0, Html 1, RawHtml 2), and the 24.3 manual pages on dunning messages, Billing Defaults and HTML e-mail, all as cited in the feature spec. Nothing public describes how Open Dental 26.1 finds the type of a statement's own body, how it adds an autograph to an HTML statement e-mail, or what it does with a body it cannot translate; those choices are marked INFERRED below. No Open Dental program was installed, observed or decompiled, and no office data was used.

## Features

| Feature ID | Status | Notes |
|---|---|---|
| `v26_1-manage-dunning-html` | built | Edit Dunning Message and Billing Defaults each get an "Email type" selector (Plain Text, HTML, Raw HTML) and an "Edit HTML..." button. Statement e-mails from the Billing List, automated billing, the statement window, the sheet window and the Account module go out as HTML or raw HTML when their body's type says so, with the statement PDF still attached. |

## What was built

- **Data** (from the base, not added here): `dunning.EmailType` (M2-01, level 2026102402, with CRUD) and the `BillingEmailType` preference (M2-02, `Seed26_1`, default `0`). `EmailType` already had the published numbers.
- **`OpenDentBusiness/Logic/BillingEmailHtml.cs`** (new). All the logic:
  - `GetBillingDefaultsEmailType`, `ToEmailType` (an unknown stored number is plain text).
  - `GetEmailTypeForStatement` / `FindDunningEmailType`: rule 1 of the spec, "the type travels with the body" (see INFERRED 1).
  - `ApplyEmailType`: rules 2 to 5. Regular leaves the e-mail exactly as before. Html translates the tag-replaced markup with `MarkupEdit.TranslateToXhtml` and places it in the e-mail master template (`EmailMasterTemplate`), the way the e-mail window builds a message from an HTML template: `BodyText` keeps the markup, `HtmlText` holds the HTML, `HtmlType=Html`. RawHtml uses the body as written (`BodyText`=`HtmlText`, `HtmlType=RawHtml`).
  - `TryInsertAutograph`: the sender's autograph for HTML statement e-mails (INFERRED 3).
  - `GetBodyError`: the check the two windows run before they save an HTML body.
- **`OpenDentBusiness/Data Interface/Statements.cs`**: one added line in `GetEmailMessageForStatement`, after the tags are replaced (every statement e-mail is built there), and one added line in the patient-portal receipt error path so its plain error text is not replaced by an HTML statement body.
- **`OpenDentBusiness/Logic/BillingL.cs`**: three added lines before the existing autograph block in `PrintBatch`; a plain-text e-mail still takes the old path unchanged.
- **`OpenDental/Forms/FormDunningEdit.cs` and `.designer.cs`**: group "Email Statement Override" gets a type selector and "Edit HTML..." (the body box moved down 6 px; the bottom edge is unchanged). The type is loaded, checked on save and saved in `dunning.EmailType`. "Duplicate" in `FormDunningSetup` already copies every field, so the type is copied too.
- **`OpenDental/Forms/FormBillingDefaults.cs` and `.Designer.cs`**: the same selector and button on the bottom row of "Email Statements" (the body box is 4 px shorter); the type is saved in `BillingEmailType`.
- **"Edit HTML..."** opens `FormEmailEdit`, the HTML editor with live preview that the e-mail template window (`FormEmailTemplateEdit`) opens from its own "Edit HTML" button. Saving there makes the body HTML, or raw HTML when its raw box is checked; the selector follows. Choosing a type in the selector without the editor is allowed. An HTML body that is not valid markup cannot be saved in either window (the same XML check the HTML editor runs); raw HTML is not checked, as in the HTML editor.

## Spec rules, one by one

1. Body from the dunning override, otherwise Billing Defaults; its type travels with it: built (INFERRED 1 for how).
2. Regular keeps today's behavior: built; the plain-text path is untouched (tested).
3. Html in the master template, tags replaced: built. Tags are replaced in the markup first, with the values escaped, then the markup is translated; doing it the other way round would let a value such as a name with "<" break the HTML.
4. RawHtml as written, tags replaced: built; values with "<" or ">" become `&lt;`/`&gt;` (INFERRED 2).
5. HTML part plus a plain-text fallback, built the way HTML templates are sent: built the way HTML templates are sent, which in this code means the HTML part only (see Deviations).
6. Tag-presence checks for the message-to-pay URL tags in `BillingL` and `FormBillingOptions`: nothing to do. This fork removed the hosted statement and payment-portal links; `MessageTagReplacer` blanks `[StatementURL]`, `[StatementShortURL]` and `[MsgToPayURL]` anywhere in the body, HTML or not, and neither file checks for them any more.
7. Overrides copied to `statement.EmailBody` in Billing Options inherit the source's type: built through INFERRED 1, the same way for Billing Options and automated billing.

## INFERRED behaviours

1. **Type of a statement's own body.** The statement table has no e-mail type column (26.1 added one only to dunning), and `statement.EmailBody` is only ever written by copying a dunning message's override (Billing Options, `BillingL.CreateStatementHelper`). So at send time the type is taken from the dunning whose e-mail body is the same text. Several matches: same subject first, then the patient's clinic, then "All clinics" (-2), and a full tie goes to the last one (billing also picks the last matching dunning). No match (the dunning was edited or deleted after billing made the statement): plain text, as before 26.1. A statement without its own body uses the Billing Defaults body and therefore the Billing Defaults type. This costs one small `SELECT * FROM dunning` per statement e-mail that has its own body.
2. **Raw HTML escapes.** Tag replacement for e-mails escapes "<" and ">" in values as the markup escapes `&<`/`&>`; in raw HTML these are turned into `&lt;`/`&gt;`. A raw HTML template that itself contains `&<` would be changed too (that text is not valid HTML anyway).
3. **Autograph** (Billing Defaults "Include Email Autograph"). Html: the autograph is added to the markup (a plain-text autograph with "<" and ">" escaped) and the message is translated again; if that fails it goes without the autograph. RawHtml: the autograph becomes an HTML fragment (an HTML autograph translated without the master template, plain text encoded with line breaks) inserted before the last `</body>`, or appended when there is none. Plain text: unchanged (including the old behavior where an HTML autograph turns the whole message into HTML).
4. **Body that cannot be translated** (only possible for data the windows did not save, because both refuse an invalid HTML body): sent as plain text, as before 26.1, rather than skipped. This keeps the statement window, the Account module and the sheet window, which call `GetEmailMessageForStatement` without a try/catch, from failing.
5. Selector labels "Plain Text", "HTML", "Raw HTML" and the "Email type" label, the position of the controls, and the save-time message.

## Deviations from the spec

- **No separate plain-text part.** The spec says HTML e-mails carry an HTML part plus a plain-text fallback, "the way existing HTML templates are sent". In this code every HTML e-mail, templates included, is sent by `OpenDentBusiness/Email/SendEmails.cs` as the HTML part only (the SMTP path sets `IsBodyHtml` with an HTML alternate view; the MimeKit path sets `HtmlBody`). Statement e-mails follow that path exactly; adding a text part would change every HTML e-mail the program sends and is outside this package's files. Owner question 2.
- **"Edit HTML..." opens `FormEmailEdit`, not `FormEmailTemplateEdit`.** `FormEmailTemplateEdit` edits an e-mail template row (description, attachments) and opens `FormEmailEdit` for the HTML itself. The dunning and Billing Defaults windows already have their own subject and body boxes, so they open the editor and preview directly, as the template window does. `FormEmailTemplateEdit` is unchanged.
- **Owned files not changed:** `Dunnings.cs` (`Refresh(null)` already returns every dunning with the new field), `EmailMessages.cs` (the existing `IsHtmlEmail`, `InsertAutograph` and send path are used as they are), `FormBillingOptions.cs` (it copies the override text exactly as before; rule 6 has nothing left to check), `FormDunningSetup.cs` (its "Duplicate" copies the type), `FormEmailTemplateEdit.cs`. The dunning grids keep their "X" in the Email column for any override.

## Changes outside the owned list

- `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line for `Logic\BillingEmailHtml.cs`.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.
- No registry, enum, preference, schema or CRUD change.

## Interplay with packages built at the same time

- **M2-SDV** (also edits `Statements.cs`, `FormBillingOptions.cs`, and may add a `BillingDefaultServiceDateView` box to Billing Defaults): this package only adds two lines to `Statements.cs` (in `GetEmailMessageForStatement` and the receipt error path) and does not touch `FormBillingOptions.cs`. In `FormBillingDefaults.cs` the preference save is a separate `SaveBillingEmailType()` call after the existing `Prefs.Update...` chain, so a line M2-SDV adds to that chain does not conflict; the designer additions are on the bottom row of the "Email Statements" group. If both branches add controls to the same designer, keep both blocks. `HookSourceTests` fails if a merge drops any of these hooks.
- **M2-RECUR** (edits `EmailMessages.cs` for the recurring-charge failure e-mail): no overlap; this package does not edit `EmailMessages.cs`. A failure e-mail template that should also allow HTML could reuse `BillingEmailHtml.ApplyEmailType`.
- **M2-01 / M2-02**: used as they are (`Dunning.EmailType`, `PrefName.BillingEmailType`, the seeded row).

## Tests

New project `tests/M2DUNTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `BillingEmailHtmlTests` (no database): published `EmailType` numbers; unknown numbers are plain text; which dunning a statement body came from (subject, clinic, all clinics, ties, edited dunning, blank and null subjects); Regular unchanged; raw HTML as written with escapes turned into entities; raw HTML autographs (before `</BODY>` in any case, appended without a body tag, encoding and line breaks, no autograph); the save-time HTML check.
- `DunningEmailRenderTests` (scratch MariaDB database `m2duntest_<time>_<pid>_<random>_render`, built from the 24.3.63 seed plus the fork migrations, dropped afterwards; synthetic patients only): the seeded preference; `dunning.EmailType` saved and loaded; a dunning e-mail in each EmailType through `Statements.GetEmailMessageForStatement` and `EmailMessages.ODEmailMessageToBasic` (plain text as before; HTML inside the master template with tags replaced; raw HTML exactly as written); escaped tag values in both HTML types; an edited dunning falls back to plain text; a subject-only override uses the Billing Defaults body and type; an invalid HTML body falls back to plain text; Billing Defaults in each type; an unknown stored type; autographs in HTML, raw HTML and plain-text e-mails.
- `HookSourceTests`: the hooks in `Statements.cs`, `BillingL.cs`, `FormDunningEdit` and `FormBillingDefaults` (the windows can't be opened in a test run).

Related existing assembly: `StatementTests` (covers `Statements.cs`).

Not tested: the two windows themselves (the GUI is not launched in this run), and sending a real e-mail (no mail server; `ODEmailMessageToBasic` is the last step tested before the transport).

## Questions for the owner

1. INFERRED 1: is matching a statement's body back to its dunning message acceptable, or should the type be fixed when the statement is made? The latter needs a column the 26.1 schema does not have (a fork-only `statement` column would break schema parity), or storing the type in the statement's text.
2. Should HTML e-mails (statements and every other HTML e-mail) get a plain-text part as well? It is a change to the shared e-mail sender for all HTML e-mail, so it belongs in its own small package.
3. INFERRED 4: should an HTML statement body that cannot be translated rather be refused (the patient skipped in billing with an error) than sent as plain text?
