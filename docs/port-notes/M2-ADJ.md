# Port note: M2-ADJ, percent-of-fee adjustments

Branch `port/m2-adj`, cut from `port/foundation` (which holds M2-01 and M2-02). Built in the single-wave run, beside the other C1 packages. Backlog section: [port-backlog.md, M2-ADJ](../specs/port-backlog.md#m2-adj-percent-of-fee-adjustments). Specs: [features-26.1.md](../specs/features-26.1.md), items `v26_1-account-adjust-percent-of-fee` and `v26_1-setup-adjtype-default-percent`.

Sources: the public 26.1 release notes and highlights post the specs cite, the 26.1 database document's description of `definition.Supp` (as summarized in the spec), and the public 24.3 manual for the baseline. No Open Dental program was installed, observed or decompiled, and no office data was used. No fork migration step: the `definition.Supp` column, its table class field and CRUD come from M2-01 (level 2026102402).

## Features

| ID | Status | Notes |
|---|---|---|
| `v26_1-account-adjust-percent-of-fee` | built | Edit Adjustment has Fixed Amount and Percent of Fee. Percent of Fee is enabled only while a procedure is attached; the amount box then takes a percentage (label "Percent", 0 to 1000, at most 2 decimals) and a label next to it shows the dollars. The dollars are recomputed when the type, the percentage or the attached procedure changes. Only the dollar amount is saved; a saved adjustment reopens in Fixed Amount. The zero-amount permission rule, the DPP rule, the Negative Adjustments preference (Warn and Block) and the remaining-amount display all run on the computed dollars. The security log entry gets " (N% of fee)". Add Multiple Adjustments uses the same calculation. |
| `v26_1-setup-adjtype-default-percent` | built | Definitions > Adj Types > edit has a "Default %" box (blank for none), shown in the user's culture and stored in `definition.Supp` as invariant text with two decimals ("90.50"). Only the AdjTypes category reads or writes `Supp`; other categories keep whatever they have. A new adjustment in Edit Adjustment, and Add Multiple Adjustments, switch to Percent of Fee and fill in the default when such a type is chosen. Not built: a "Default %" column in the Definitions grid (optional; that grid is drawn by `DefL.cs`, an M2-02 file). |

## What was built

### Business layer (`OpenDentBusiness/Data Interface/Adjustments.cs`, region "Percent of Fee (Open Dental 26.1)")

Methods that only compute (no database, `Meth.NoCheckMiddleTierRole`), so both windows and the tests share one implementation:

- `GetPercentOfFeeBasis(Procedure)`: the procedure's `ProcFeeTotal`, which is what Add Multiple Adjustments shows in its Fee column (`AccountEntry.AmountOriginal`).
- `GetPercentOfFeeAmount(fee,percent)`: percent/100 x fee in `decimal`, rounded to cents with half a cent away from zero. `ApplyAdjTypeSign` (negative for "-" and "dp" types) and `GetPercentOfFeeAdjAmt` (both together).
- `IsValidPercentOfFee`, `TryParsePercentInput` (user's culture, spaces and one trailing % ignored), `FormatPercentForDisplay` (user's culture, "0.##").
- `TryParseSuppPercent`, `FormatSuppPercent` (invariant, "0.00"), `TryGetSuppFromPercentInput` (Default % box to `Supp`), `GetDefaultPercent(Def)` (null unless the def is an adjustment type with a valid `Supp`).
- `GetPercentOfFeeLogNote` (" (12.5% of fee)").

### Edit Adjustment (`OpenDental/Forms/FormAdjust.cs`, `FormAdjust.designer.cs`)

- Designer: `radioFixedAmount` (checked) and `radioPercentOfFee` (disabled until a procedure is attached) to the right of the date boxes, `labelPercentOfFeeAmount` beside the amount box, and a `TextChanged` handler on `textAmount`. Nothing existing was moved.
- Code: the new logic is in the region "Percent of Fee" at the end of the class. Hooks in existing code: every place that read `PIn.Double(textAmount.Text)` (remaining amount, zero-amount check, DPP check, the three `AdjAmt` assignments) now calls `GetAmountEntered()`, which returns the box in Fixed Amount mode and the computed dollars in Percent of Fee mode; `FillProcedure` calls `FillPercentOfFee`; the default percentage is applied on load, on either type list and on attaching a procedure; Save checks the percentage first; both security log entries append the percentage.

### Add Multiple Adjustments (`OpenDental/Forms/FormAdjMulti.cs`)

- The Percent of Fee case calls `Adjustments.GetPercentOfFeeAmount` (same basis as before).
- Choosing a type with a default percentage checks "Percent of Fee" and fills in the percentage.

### Edit Definition (`OpenDental/Forms/FormDefEdit.cs`, `FormDefEdit.designer.cs`)

- Designer: `labelDefaultPercent` ("Default % (blank for none)") and `textDefaultPercent`, hidden unless the definition is an adjustment type.
- Save validates the box with the other AdjTypes rules and writes `Supp`; DefEdit permission and the existing definition logs are unchanged.

## INFERRED behaviours

The public sources describe the two radio buttons, the default percentage and the `Supp` column, nothing more. Chosen here:

- Rounding: half a cent rounds away from zero, computed in `decimal`. Add Multiple Adjustments used `Math.Round` on doubles (half to even, subject to binary error); the two now agree, and the results differ from 24.3's only where the exact result ends in half a cent (tested).
- `Supp` is written as "0.00" invariant text, following the published example "90.50"; any invariant decimal from 0 to 1000 with at most two decimals is read. A stored value that is not valid is ignored by the adjustment windows and shown as stored in Edit Definition, where it must be fixed or cleared before saving.
- When the default applies: for a new adjustment only, when a procedure is attached, (a) whenever a type with a default is chosen (it replaces what is in the box, per the spec), (b) on load when the caller preset such a type and the amount is zero, (c) on attaching a procedure after such a type was chosen, when the amount is still zero. A type without a default leaves the mode and the box alone. Editing a saved adjustment never applies defaults.
- Switching to Percent of Fee by hand clears the box; switching back to Fixed Amount puts the computed dollars in the box. Detaching the procedure while in Percent of Fee switches to Fixed Amount and keeps the dollars.
- A blank percentage gets the existing "Please enter an amount." message; 0 is allowed (a $0 adjustment then follows the zero-amount permission rules).
- The Default % box is offered for every adjustment type, discount plan ("dp") types included; dp types are never offered for new adjustments, so their default is never used.
- Add Multiple Adjustments keeps its 24.3 percentage range (any number); only Edit Adjustment enforces 0 to 1000.
- Label texts ("Fixed Amount", "Percent of Fee", "Percent", "Amount: $x", "Default % (blank for none)") are this fork's wording.

## Deviations from the spec

- None in behavior. The spec suggested optionally moving the calculation into `Adjustments.cs`; done. The Definitions grid does not show the default percentage (optional, file not owned).

## Interplay with packages built at the same time

- M2-01 (on the base): `definition.Supp`, `Def.Supp` and `DefCrud` are used as they are; nothing was added to the schema, the table classes or the CRUD. The definition cache already loads `Supp`.
- M2-02 (on the base): nothing used or changed.
- No other C1 package owns `Adjustments.cs`, `FormAdjust`, `FormAdjMulti` or `FormDefEdit`. Packages that create adjustments in code (for example M2-PAYW or M2-RECUR) are not affected: nothing in the existing `Adjustments` methods changed, and the new methods are additions.
- `FormDefEdit` is shared by every definition category. M2-02 configures Employee Titles in `DefL.cs`/`Defs.cs`, not in this form, so there is no overlap. If a later package adds its own use of `Supp` for another category, it must add its own `case`; `FormDefEdit` keeps `Supp` unchanged for categories other than AdjTypes.

## Files touched

- Changed (owned): `OpenDentBusiness/Data Interface/Adjustments.cs` (one `using`, one new region), `OpenDental/Forms/FormAdjust.cs`, `OpenDental/Forms/FormAdjust.designer.cs`, `OpenDental/Forms/FormAdjMulti.cs`, `OpenDental/Forms/FormDefEdit.cs`, `OpenDental/Forms/FormDefEdit.designer.cs`.
- New: `tests/M2ADJTests/*` (test project), this note.
- Changed (shared, small): `OpenDental.sln` and `OpenDental.Local.slnf` (the test project). No `.csproj` of the product was changed: all new code is in owned files that are already compiled.
- Outside the owned list: none (no registry, schema, CRUD, preference or permission change).

## Tests (`tests/M2ADJTests`)

- `PercentOfFeeTests` (no database): fixed amounts, rounding (half cent away from zero), surcharges over 100%, sign by type, the basis equals Add Multiple Adjustments' Fee column, agreement with the old Add Multiple Adjustments formula away from half cents, percentage range and decimals, parsing and display in en-US and de-DE, `Supp` format and parsing independent of the user's culture, default only for adjustment types, Default % box to `Supp` and back, the default giving the adjustment amount.
- `AdjTypeDefaultPercentDbTests` (scratch MariaDB database from the 26.1.70 reference, synthetic rows, unique name, dropped afterwards; skipped when no local server answers or `M2ADJ_TEST_DB=skip`): `Supp` saved by `Defs.Insert`/`Update` and read through the definition cache and the positive and negative adjustment type lists; clearing it; other categories leave it empty; a Percent of Fee adjustment saved through `Adjustments.Insert` stores only the dollars.
- `WindowWiringTests` (source checks, because the windows are not opened in a test run): every amount Edit Adjustment saves or checks goes through `GetAmountEntered`, the hooks and controls are present, Add Multiple Adjustments uses the shared calculation and the defaults, Edit Definition saves `Supp`.

## Checks

- Build of `OpenDental.Local.slnf` (Debug, x86): 0 errors, 376 warnings (the baseline).
- Targeted tests: `M2ADJTests` 26/26, `PayPlanTests` 29/29 (covers `Adjustments`), `M202Tests` 39/39, `NoHqTests` 49/49.
- `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- The windows themselves (OpenDental.exe is not launched in this run): layout of the new controls at other zoom levels, focus and tab order, the radio buttons' behavior, the Definitions window. The logic behind them is tested through the business methods, and the wiring by the source checks.
- A real Open Dental 26.1 database: whether Open Dental writes `Supp` as "90.50" or "90.5", and whether its rounding matches.

## Questions for the owner

1. Rounding of a percentage that ends in exactly half a cent: away from zero (chosen) or half to even?
2. Should the default percentage also apply when the procedure is attached after a type with a default was chosen and an amount was already typed (now: only if the amount is zero)?
3. Show the default percentage as a column in Definitions > Adj Types (needs `DefL.cs`, an M2-02 file)?
4. Should Add Multiple Adjustments also limit percentages to 0 to 1000 with two decimals, as Edit Adjustment does?
5. Hide the Default % box for discount plan ("dp") adjustment types?
