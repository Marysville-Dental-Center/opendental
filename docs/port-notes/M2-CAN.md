# Port note: M2-CAN, procedure codes and Canadian code systems

Branch `port/m2-can`, cut from `port/foundation` (M2-01 and M2-02 already on it). Built in the single-wave run, beside the other M1 Tier B and M2 packages. Backlog section: [port-backlog.md, M2-CAN](../specs/port-backlog.md#m2-can-procedure-codes-and-canadian-code-systems). No fork migration step was needed, so the level range 2027003101 to 2027003199 is unused. No schema, registry, preference, permission or enum file was changed.

## Sources

Public sources only: Open Dental's 25.4 and 26.1 release notes (version25_4.html, version26_1.html), the public Canadian pages the feature specs cite (canadaproccodetools.html, canada.html, dac.html, canadalabfees.html) as summarized in `docs/specs/features-25.4.md` and `features-26.1.md`, and the 26.1 schema document (the ProcCode rule). Nothing was decompiled or observed, no Open Dental program was installed, and no office data was used. No CDHA, DAC, RAMQ or CDA code text, fee guide or claim-form artwork is in the repository: the office supplies the code file, and every test uses made-up codes and descriptions.

## Features

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-canada-other-proc-codes` | built | Procedure Code Tools (Canada only, en-CA or fr-CA) gets an **Other Codes** button that opens the new **Other Procedure Codes** window: code system list (CDHA; DAC (English); DAC (French); RAMQ; CDA, Advanced Options only), the four Advanced Options (update descriptions, abbreviations, treatment area, paint type), the code file, OK. OK checks Security Admin and Procedure Code Edit, reads the file, shows what will be added, updated, skipped and refused, and saves after the user agrees. Each system's new codes go in a procedure code category named after it (created when missing, hidden ones count). Codes that exist are skipped unless an Advanced Option asks to change a field. Nothing is deleted. Log entries: Definition Edit for a new category; Procedure Code Edit per added code, per changed code (field, old and new value) and one for the run with the system name, the counts and the options. ACDQ is not offered (Open Dental's staff verify members). |
| `v25_4-canada-codes-cdha` | built | CDHA in the window, category "CDHA". Codes that also exist as CDA codes are skipped (or updated with an Advanced Option). |
| `v25_4-canada-codes-dac` | built | DAC (English) and DAC (French) are separate entries sharing the category "DAC". Importing the second language after the first adds only missing codes; descriptions keep the first language unless Update descriptions is checked (tested). |
| `v25_4-canada-codes-ramq` | partly built | RAMQ codes are stored with a trailing R (never clashing with the CDA code of the same number), category "RAMQ". Printed claims leave the R out: the claim form's procedure Code field (`FormClaimPrint`, one line) calls `ProcCodesOther.GetCodeForClaim`, which strips the final R only when the code sent is the procedure's own code and the code's category is RAMQ. **Not built: the CDAnet output (field F08 in `Eclaims/Canadian.cs`).** The owner removed the Canadian claim networks, and that field only takes effect when a claim is sent through them. (Today a Canadian claim also cuts the code sent to five characters when it is created, `Claims.cs`, so a five-digit RAMQ code already goes out without its R.) No RAMQ e-claims, as the spec says. |
| `v25_4-canada-labfee-pay` | built | Enter Payment (`FormClaimPayTotal`) gets **Pay Labs**, visible only in Canada when a procedure of the payment has its lab rows in the same payment. The split is `ClaimProcs.PayLabs`, see INFERRED. Write-offs and deductibles are untouched, nothing is saved until Save, and closing the window without Save undoes it, as with Deductible and Write-off. Uses the window's existing permissions. |
| `v26_1-lists-canada-othercodes-password` | built | Advanced Options ask the user to type the confirmation phrase **UPDATE CODES** (any case, spaces around ignored) before anything is read or saved; a plain import asks nothing. The window states the phrase (see INFERRED and the questions). Security Admin plus Procedure Code Edit on OK. |
| `v26_1-lists-proccode-no-hyphens` | built | A procedure code may not contain a comma, a hyphen-minus or a non-breaking hyphen (U+2011), and is at most 15 characters (`ProcedureCodes.GetProcCodeTextError`, new partial file `ProcedureCodes.CodeText.cs`). Enforced on Procedure Codes, New (message), on the XML and N-code import `FormProcCodes.ImportProcCodes` (allowed codes are imported, refused ones listed), and on the Other Procedure Codes import (refused rows listed). Edit Procedure Code needs nothing: its code box is read-only, so the code text can't change there. Existing codes are never changed: the new Database Maintenance check **ProcedurecodeCodeHasSeparator** (every office, not Canada-only) counts them in Check and Fix and lists them in its breakdown, with a manual-fix note. The CDA Codes and D Codes imports are B-PROC's, built at the same time (see interplay). |

## What was built

Business layer (`OpenDentBusiness/Data Interface/`):

- `ProcedureCodes.cs`: `public class` becomes `public partial class` (one word, no other change).
- `ProcedureCodes.CodeText.cs` (new partial): `PROC_CODE_MAX_LENGTH`, `ArrayProcCodeCharsNotAllowed`, `GetProcCodeTextError`, `IsProcCodeTextAllowed`, `GetCodesWithAllowedText`, `GetCodesWithSeparators`.
- `ProcCodesOther.cs` (new): `EnumOtherCodeSystem`, `EnumOtherCodeUpdates` (neither is stored), the row, plan and result classes, and `ProcCodesOther`: file decoding, parsing, `CreatePlan`/`CreatePlanFromDb` (no changes), `Apply` (middle-tier aware), the RAMQ suffix and claim rule, the confirmation phrase, summary and log texts.
- `ClaimProcs.PayLabs.cs` (new partial of the shared `ClaimProcs`): `GetPayLabsGroups`, `HasPayLabsGroups`, `PayLabs`.
- `DatabaseMaintenances.ProcCodes.cs` (new partial, the backlog's file): `ProcedurecodeCodeHasSeparator`.

Program (`OpenDental/Forms/`):

- `FormProcCodesOther.cs` and `.Designer.cs` (new window, no resx).
- `FormProcTools.cs`: one line in the constructor, `AddOtherCodesButton();`. The button and its handler live in the new partial file `FormProcTools.OtherCodes.cs`. The button is created in code at Run Now's height, left side, so it lands in the right place whether or not B-PROC's taller layout (two more options, Run Now moved down) is merged; the designer file is untouched.
- `FormClaimPayTotal.cs` and `.designer.cs`: the Pay Labs button (hidden by default, next to Write-off), its visibility in Load and its handler. Insertions only.
- `FormProcCodeNew.cs`: the rule check in `AddProc` (4 lines).
- `FormProcCodes2.cs`: the filter in `ImportProcCodes` (7 lines).
- `FormClaimPrint.cs` (outside the owned list, M2-CLAIMFORM's file this wave): the `"Code"` case of `GetProcInfo` returns `ProcCodesOther.GetCodeForClaim(...)` instead of the code sent (1 line).

Not changed: `Eclaims/Canadian.cs` (owned; only the excluded CDAnet output would change, see RAMQ), `FormProcCodeEdit.cs` (owned; read-only code), `Defs.cs`, every registry and enum file.

## Code file format (Other Procedure Codes)

- Comma or tab separated text (the delimiter of the first data line), as a spreadsheet's CSV or Unicode Text export writes it: UTF-8 or UTF-16 with a byte order mark, UTF-8, or the computer's ANSI code page. Lines starting with `#` are comments; blank lines are skipped.
- Quoted cells as spreadsheets write them (a cell that starts with a quote ends at the closing quote, `""` is one quote).
- Columns without a header: code, description, abbreviation, treatment area, paint type, time units (the spec's order). Missing trailing columns are blank.
- With a header row (its first cell names the code column: Code, ProcCode or Procedure Code), columns are found by name, ignoring case, spaces and punctuation: Description/Descript, Abbreviation/AbbrDesc/Abbr, Treatment Area/TreatArea/Area, Paint Type/Paint, Time Units/CanadaTimeUnits/Units. Other columns are ignored.
- Treatment area and paint type: the program's names (`Surf`, `Tooth`, `Mouth`, `Quad`, `Sextant`, `Arch`, `ToothRange`; `FillingLight`, `Sealant` ...) or numbers, any case, spaces ignored; "Surface" and "Range" also work. Time units: a number of 0 or more, with a period or a comma.
- A row is refused (and listed before anything is saved) when it has no code, a description over 255 or an abbreviation over 50 characters, an unknown treatment area or paint type, bad time units, a code that breaks the 26.1 rule (with the R for RAMQ), a code listed twice (the first row wins), or, for a new code, no description.

## INFERRED behaviours

- File format and column names (the spec fixes only the column list), header detection, the ANSI fallback.
- New codes: a blank abbreviation becomes the code; blank treatment area and paint type are None (as B-PROC's code import); blank time units 0; no default provider; the procedure time is the program's default `/X/`.
- Category names: CDHA, DAC (both languages share it), RAMQ. A category with that exact name is reused even when hidden; a new one goes after the last category in the list. CDA adds no codes in this window (the CDA Codes option of Procedure Code Tools does), so with CDA at least one Advanced Option is required and codes missing from the database are counted as "not found".
- Advanced Options change the matching existing codes (matched by the stored code, in any category), and only the checked fields; a blank cell never clears a field. Saving writes only the fields that differ, so another change made to the same code meanwhile is kept; a code someone added meanwhile is skipped, not duplicated.
- RAMQ: the R is added unless the file's code already ends with R (a list exported from this program). Claims strip it only for codes in the category named RAMQ (any case); if the office renames that category, printed claims show the stored code. A code sent that differs from the procedure's code (alternate code, medical code, typed by the user, cut to five characters) is printed as it is.
- Confirmation (26.1): Open Dental's password is a fixed word printed in its public manual. This program has no help system, so the prompt names the phrase itself: it stops accidental mass edits, which is its purpose, and is not security. The phrase is "UPDATE CODES".
- Pay Labs split (the public page does not give it): for each procedure row with lab rows (same claim, plan and status, at most two, in the window's order), the insurance payments of the procedure and its labs are added up; each lab gets the smaller of its fee billed and what is left; the procedure keeps the rest. Nothing goes below zero (a payment smaller than the labs leaves the procedure at 0 and the second lab short); a total of zero or less (nothing paid, a negative supplemental) is left alone; amounts are rounded to cents. Running it twice changes nothing.
- The 26.1 rule names the hyphen-minus and U+2011 only; U+2010 (HYPHEN) is allowed.
- Database Maintenance: the check is for every office, never fixes anything, and tells the office to add a replacement code and move the old one to a hidden category (codes can't be renamed in the program).

## Deviations from the backlog and specs

- RAMQ claim output: printed claims only; the CDAnet output is not built (owner decision on the Canadian networks).
- The 26.1 "password" is a phrase the window shows, not a secret (see INFERRED and the questions).
- `FormProcCodeEdit.cs` and `Eclaims/Canadian.cs` are owned but needed no change.
- The spec lists `Defs.cs` (DefCat.ProcCodeCats) as a touchpoint: categories are created through `Defs.Insert` without changing `Defs.cs`.

## Interplay with packages built at the same time

- **B-PROC** (CDA Codes / D Codes imports, `FormProcTools.cs` and its designer, `ProcCodeImports.cs`): its `ProcCodeImports.Parse` refuses codes over 15 characters but not commas or hyphens. At integration, add the 26.1 rule to its plan, for example in `ProcCodeImports.CreatePlan` skip and list every row where `!ProcedureCodes.IsProcCodeTextAllowed(row.ProcCode)` (message `ProcedureCodes.GetProcCodeTextError(row.ProcCode)`). `FormProcTools.cs`: B-PROC changes the Load method and adds methods after it; this package adds one constructor line, so the two merge without overlap. The designer is not touched here, and the Other Codes button follows Run Now to B-PROC's new position (the window test checks the button against Run Now's position, not a fixed height; it ran on this branch's layout only). B-PROC's CDA Codes adds missing CDA codes; the CDA entry here only updates existing ones, so the two do not overlap.
- **B-TP / M2-CAN on `FormProcCodes2.cs`**: only `ImportProcCodes` changed (7 lines before its loop).
- **M2-CLAIMFORM** (owns `FormClaimPrint.cs`): one changed line in the `"Code"` case of `GetProcInfo`. If the DC217 work rewrites that case, keep the call to `ProcCodesOther.GetCodeForClaim`.
- **M2-HIST** (takes `ProcedureCodes.cs` over in C2): the class is now partial; nothing else changed in that file.
- **M2-CARRIER** (`carrier.PreAuthCodes`) and **M2-HIST** (code group lists): they parse comma and hyphen lists and can rely on `ProcedureCodes.IsProcCodeTextAllowed` for new codes; old codes with separators are listed by the new check.
- **B-DBM**: also adds `DatabaseMaintenances.*.cs` partial files; this package's file is `DatabaseMaintenances.ProcCodes.cs` and its csproj line sits next to the other M2-CAN lines (after `ProcedureCodes.cs`), not next to B-DBM's.
- **B-CLAIM, V-PAYSUITE** (`Eclaims/Canadian.cs`): not touched here.
- **M2-VERIFY**: nothing here changes the 25.3 CDA twin.
- csproj and solution files: four `Compile` lines in `OpenDentBusiness.csproj` (after `ProcedureCodes.cs`), three in `OpenDental.csproj` (after `FormProcCodes2.cs`), the test project in `OpenDental.sln` and `OpenDental.Local.slnf` (after M202Tests). Conflicts there are list appends.

## Changes outside the owned list

- `OpenDental/Forms/FormClaimPrint.cs` (1 line, RAMQ on printed claims).
- `OpenDentBusiness/Data Interface/ClaimProcs.PayLabs.cs` (new partial file of the shared `ClaimProcs`; `ClaimProcs.cs` itself unchanged).
- `OpenDentBusiness/OpenDentBusiness.csproj`, `OpenDental/OpenDental.csproj`, `OpenDental.sln`, `OpenDental.Local.slnf` (compile entries, the test project).
- No foundation-owned registry, table class, CRUD, preference, permission, enum file or seed row was changed.

## Tests

`tests/M2CANTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 47 tests:

- `ProcCodeTextRuleTests`: allowed and refused codes, the separators, the N codes that ship pass the rule, every import path checks it before inserting, the Database Maintenance check's flags.
- `OtherCodesTests`: the five code systems and their categories, CDA only with Advanced Options, the RAMQ suffix and claim rule, the printed claim hook, the confirmation phrase and where the window asks it, parsing (no header, header by name, quotes, French decimal comma, bad rows, UTF-8/UTF-16/ANSI), plans for CDHA, DAC English then French, RAMQ (length with the R), CDA (update only), Advanced Options per field, summary and log texts.
- `PayLabsTests`: full payment, payment smaller than the labs, amounts already on the labs, cents, nothing paid, same claim/plan/status and at most two labs, no labs.
- `WindowTests` (built in memory on an STA thread, never shown): Other Codes only in en-CA and fr-CA, level with Run Now and below every option; the Other Procedure Codes options map to `EnumOtherCodeUpdates`; Enter Payment's hidden Pay Labs button and its wiring.
- `M2CanDbTests` (scratch database `m2can_<time>_<pid>_<random>` with the 26.1.70 reference schema, two preference rows, dropped at the end; inconclusive without the local server or with `M2CAN_TEST_DB=skip`): CDHA into a new category with logs and a second run that changes nothing; DAC English then French; RAMQ stored with R and printed without; CDA updates only; changes made meanwhile kept; a hidden category reused; the Database Maintenance check lists codes with a comma, a hyphen and U+2011 and Fix changes nothing.

## Checks

- Build of `OpenDental.Local.slnf` (Debug, x86): 0 errors, 376 warnings (the baseline). The new test project builds with 0 warnings.
- Targeted tests: `M2CANTests` 47/47, `NoHqTests` 49/49, `InsuranceTests` 49/49, `FeeTests` 48/48, `RegistryTests` 33/33 (the last three because this package touches procedure codes, claim procedures and the partial data classes).
- `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- The dialogs in a running program (file picker, summary, confirmation phrase prompt, Pay Labs in a real claim); OpenDental.exe was not launched.
- A real association code file (none may be in the repository) and a real Canadian claim with labs.
- The middle tier (ClientMT) path of `ProcCodesOther.Apply` (the plan travels as a serialized object).
- Printed claim forms with a RAMQ code (only `GetCodeForClaim` and the one-line hook are tested).
- Layout at high DPI.

## Questions for the owner

1. Confirmation phrase (26.1): the window states "UPDATE CODES" in the prompt, since this program has no help system to document a hidden word. Keep it visible, or hide it and document it elsewhere?
2. Advanced Options match existing codes by code in any category, so a CDHA or DAC file with Update descriptions also renames a CDA code with the same number. Should updates be limited to codes in the system's own category (CDA would then need its own rule)?
3. RAMQ: should printed claims strip the R by category (chosen) or for every code ending in R that the RAMQ import created, even after the office moves it to another category?
4. Pay Labs: is the split (labs first, up to their fee billed, the procedure keeps the rest) what the office's carriers need?
5. DAC English and French share one "DAC" category. Separate categories instead?
6. Codes with commas or hyphens from before 26.1 can only be replaced, not renamed. Is a rename tool wanted (it would have to update fees, code groups and every reference), or is the Database Maintenance list enough?
7. Is the Canadian part of this package needed at all for a US office? Everything Canadian is hidden outside en-CA and fr-CA.

## Integration (branch `port/int-5`)

- **B-PROC's D Codes and CDA Codes imports** (merged on `port/int-2`) now follow the 26.1 rule, as the interplay section above asks. The check is in `ProcCodeImports.Validate`, right after B-PROC's own 15-character check, so `ProcCodeImports.Parse` leaves a row whose code has a comma, a hyphen-minus or U+2011 out and lists it with `ProcedureCodes.GetProcCodeTextError`'s message; Procedure Code Tools shows those rows before anything is saved ("Rows that could not be read and are skipped"). D codes and CDA codes never contain these characters, so a real code set is unaffected. New tests: `tests/M2CANTests/ProcCodeImportsRuleTests.cs` (text and XML files).
- `FormProcTools.cs` merged without conflict: B-PROC's layout and this package's constructor line. The Other Codes button is placed from Run Now's position, so it follows B-PROC's taller layout; `WindowTests` checks that.
