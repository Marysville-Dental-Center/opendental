# B-TP: Treatment plan, add procedures to the active plan

Backlog package B-TP (M1 Tier B, wave B1, built in the single-wave run). Branch `port/b-tp`, based on `m1/integration-a2-patch`.

## Feature status

| Feature ID | Status | Notes |
|---|---|---|
| `v25_3-tp-add-procs-active` | built | Add Procs button in the Treatment Plan module, Add Procedures window, procedures created as Chart entry creates them. GUI not exercised (the GUI is never launched in this run). |

## What it does

- **Treatment Plan module toolbar: Add Procs.** It's enabled whenever a patient is selected. It targets the selected plan only if that plan is the Active plan. If the patient has an Active plan but another plan is selected, the user is told to select the Active plan. If the patient has no Active plan (no plans at all, or only Saved ones), an Active plan is created, the same one Chart entry creates.
- **Add Procedures window** (`FormTreatPlanAddProcs`):
  - **Quick Buttons** list: the Chart module's quick buttons (`procbuttonquick`), in Chart order (row, then position). Labels are left out, and so are buttons whose code is missing or in a hidden category. One click adds a row. A button's surfaces are copied only for codes whose treatment area uses them; Chart entry ignores them for other codes too.
  - **Code** box with Add (Enter works): the typed-code rules of Chart entry, including the US four-digit shortcut that tries a D in front.
  - **Search Codes...** opens the procedure code list (`FormProcCodes`) in selection mode with multiple selection. Hidden categories aren't shown, as in every selection-mode use.
  - **Procedures to Add** grid: Code, Description, Area, Tooth (editable), Surf/Area (editable), and a Notice column. The Notice column shows what is missing or invalid in red, or "Ready" / "Adds N procedures". After a row is added, the cursor goes to the cell that needs typing.
  - **Priority** (TxPriorities, default "no priority") applies to every new procedure. **Remove** deletes a row. **Add to Plan** refuses while any row has a problem, and checks again that the target plan is still the patient's Active plan.
- **Creating the procedures** (`ProcAddHelper.AddToActivePlan`): every row is validated before anything is written. Each procedure then goes through `ChartModules.AddProcHelper`, the business half of Chart entry, which sets:
  - status TP, DateTP and ProcDate today;
  - the provider and clinic chosen as the Chart chooses them;
  - the fee from the patient's fee schedules (`Fees.GetListFromObjects`, as Chart entry);
  - the default TP procedure note, CPOE flag, ortho case link and base units;
  - the estimates (`Procedures.ComputeEstimates`);
  - a `treatplanattach` to the Active plan with the chosen priority.

  As in Chart entry, the patient's first-visit date is set unless every code is a broken-appointment code. Each procedure gets a ProcEdit audit entry, like the other "treatment planned via ..." entries: `<code>, tooth 30, MOD, $0.00, treatment planned from the Treatment Plan module.`
- **Auto codes:** after creation, the module checks each procedure the way Chart entry does. If the auto codes recommend another code, the usual auto-code prompt appears. If `ProcEditRequireAutoCodes` is on and the user declines, the procedure opens in Procedure Edit, and cancelling there deletes it (Chart behaviour).
- **Refresh:** the module refreshes, which audits the plans and recomputes the grid estimates. Then it selects the Active plan and the new rows.

### Rows to procedures (per treatment area)

| Treatment area | Tooth entry | Surf/Area entry | Procedures made |
|---|---|---|---|
| Surface | required (one, list or range) | required, every letter must fit each tooth | one per tooth |
| Tooth | required | must be blank | one per tooth |
| Tooth range | required | must be blank | one, with all teeth as its tooth range |
| Quadrant | teeth, or blank | UR/UL/LR/LL, or blank | typed quadrant: one. Teeth only: one per quadrant the teeth are in (Chart behaviour). Both: only when the code also takes a tooth range, and the teeth must be in the quadrant |
| Arch | teeth, or blank | U/L, or blank | as quadrant, per arch |
| Sextant | must be blank | 1-6 (FDI offices may also type 03-08, the way FDI shows sextants) | one |
| Mouth / None | must be blank | must be blank | one |

Teeth are typed in the office's numbering (Universal, FDI, Haderup, Palmer) and stored as Universal. The parser is A-INS's `Tooth.ParseToothList`: lists, ranges in either direction, primary and supernumerary teeth.

## Files

New:
- `OpenDentBusiness/Data Interface/ProcAddHelper.cs`: the shared procedure-entry helper named in the backlog. It holds `ProcAddEntry`, `ProcAddPlacement`, `ProcAddResult`, validation, placements, quick buttons, typed codes and `AddToActivePlan`.
- `OpenDental/Main Modules/ControlTreat.AddProcs.cs`: a partial class with the button handler, the auto-code follow-up and the refresh.
- `OpenDental/Forms/FormTreatPlanAddProcs.cs` and `.Designer.cs` (no `.resx`, like `FormAdoptDatabase`).
- `tests/BTPTests/` (new MSTest v1 project, x86, .NET 4.8): `PlacementTests` (no database), `AddToActivePlanDbTests` (scratch database `forkmig_<run>_btp`, synthetic Z codes), `WiringTests` (source checks), `TpDb` (scratch database setup).
- `docs/port-notes/B-TP.md` (this file).

Edited (small, local):
- `OpenDental/Main Modules/ControlTreat.cs` (owned): one toolbar line in `LayoutToolBar`, one `case "AddProcs"` in `ToolBarMain_ButtonClick`, and one enabled-state line in each branch of `UpdateToolbarButtons`. Nothing was reformatted or moved.
- `OpenDentBusiness/Data Interface/TreatPlans.cs` (owned): new `TreatPlans.CanAddProcsTo(TreatPlan)`.
- `OpenDentBusiness/OpenDentBusiness.csproj`: one Compile line.
- `OpenDental/OpenDental.csproj`: three Compile entries.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the test project.

Not edited:
- `OpenDental/Forms/FormProcCodes2.cs` (owned). Its selection mode already has multiple selection (`CanAllowMultipleSelections`), so nothing needed changing.
- `OpenDental/Main Modules/ControlChart.cs`. The Chart's business helper `ChartModules.AddProcHelper` is called, not edited.
- No registries, CRUD, preferences, permissions or enums. No schema, so no fork migration step (the level range 2027001001-2027001099 is unused).

## INFERRED behaviour (public material only says adding procedures to the active plan got easier)

1. **The UI shape** (toolbar button, window with quick buttons, typed code and code search, inline tooth and area cells) follows the spec's "Our design". Open Dental's actual 25.3 window isn't public.
2. **Permission:** the button needs **Chart Module**. Adding procedures is Chart entry made reachable from another module, so a user without Chart access shouldn't gain it here. `ProcEdit` isn't grantable (it's a log-only type), so it can't be the gate. It's used for the audit entry. No completed procedure is ever created, so `ProcComplCreate` isn't involved.
3. **Priority** is picked once per window and applies to every row. The default is no priority.
4. **Dates:** DateTP and ProcDate are today, as Chart entry with its date box untouched.
5. **Stricter than Chart on typing.** Chart entry quietly drops surface letters that don't fit the tooth (O on an anterior tooth) and ignores teeth for mouth codes. Here those rows are refused with a message, so nothing typed is silently lost.
6. **No Active plan** (none, or only Saved plans): one is created, the same plan Chart entry creates. If another workstation created one meanwhile, it's used and no second one is created.
7. **Concurrency:** if the target plan stopped being the patient's Active plan while the window was open, nothing is written and the user is told.
8. **One row, several teeth:** a tooth or surface code typed for several teeth adds one procedure per tooth, mirroring Chart entry with several teeth selected.

## Deviations from the spec

- The Chart's **procedure buttons** (`procbutton`, often built on auto codes) aren't offered, only the Quick Buttons. Resolving auto-code buttons needs the tooth and surface first, which the Chart does through its tooth chart selection. The usual auto-code prompt still runs after creation.
- The window shows no fee or estimate preview. Fees and estimates are computed when the procedures are created and shown by the refreshed module grid.
- Missing tooth or area info is handled inline in the grid. Chart entry instead opens Procedure Edit for a procedure created without it.

## Interplay with packages built in parallel

- **B-CLAIM, B-SHELL, M2-EST, M2-CARRIER** take `ControlTreat.cs` over later. B-TP's edits there are four one-line hunks: the toolbar line after "Sign TP", the click case after "Consent", and the two enabled-state lines. The logic lives in `ControlTreat.AddProcs.cs`.
- **M2-EST** (estimate changes): no work needed here. New procedures get their estimates from `ChartModules.AddProcHelper` / `Procedures.ComputeEstimates`, so estimate changes there apply automatically.
- **B-PROC, A-FEE** (`FormProcCodes2.cs`): B-TP left it untouched, so there's no conflict. It relies only on `IsSelectionMode`, `CanAllowMultipleSelections` and `ListProcedureCodesSelected`.
- **`OpenDental.sln`, `OpenDental.Local.slnf`, the `.csproj` files:** entries are appended next to neighbours. Merge conflicts there are only adjacent-line additions.

## Questions for the owner

1. Is **Chart Module** the right permission for Add Procs? The alternatives are no extra permission (anyone in the Treatment Plan module) or **Edit Treatment Plan**.
2. Should the Chart's **procedure buttons** (auto-code buttons) also appear in the window, next to the Quick Buttons?
3. Should the priority default to the priority selected in the module's side list, instead of "no priority"?
4. Is refusing surface letters that don't fit the tooth (instead of dropping them as the Chart does) the behaviour you want?

## Tests

- `tests/BTPTests` has 36 tests: 17 placement and rule tests without a database, 15 database tests on a scratch database built from a fresh 24.3.63 database plus the fork migration runner, and 4 wiring tests.
- The database tests cover:
  - status, DateTP, priority and treatment plan attaches, and that `AuditPlans` leaves them alone;
  - an Active plan created when the patient has none or only Saved plans, and an existing one reused;
  - refusals for Inactive, Saved, another patient's and stale plans, with nothing written;
  - all-or-nothing validation;
  - hidden-category and group-note codes refused;
  - the fee from the patient's fee schedule and an 80% estimate;
  - quadrant and tooth-range codes;
  - the ProcEdit audit entry and the first-visit date;
  - auto-code recommendation with no auto codes set up;
  - quick button filtering and order;
  - typed codes.
- **Untested:** the window and the toolbar in a running program (the GUI is never launched), the auto-code prompt path, Canadian surface letters (en-CA), and the middle tier.
