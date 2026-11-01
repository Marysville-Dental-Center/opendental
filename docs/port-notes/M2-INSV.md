# Port note: M2-INSV, Insurance Verification List (Open Dental 25.4)

Branch `port/m2-insv`, cut from `port/foundation` (which holds M2-01 and M2-02). Built in the single-wave run, beside the other C1 packages. Backlog section: [port-backlog.md, M2-INSV](../specs/port-backlog.md#m2-insv-insurance-verification-list). No fork migration step was needed, so no level from the assigned range (2027003201 to 2027003299) is used.

## Sources

Built from the feature specs in [features-25.4.md](../specs/features-25.4.md) (`v25_4-appts-insverify-daterange`, `v25_4-appts-insverify-carriers`), which were written from Open Dental's public 25.4 release notes, the public 25.4 blog post, the public Quick Manual page for the Insurance Verification List, the 24.3 manual (baseline) and the published 25.4 database documents. No other source was used in this run; no Open Dental program was installed, observed or decompiled, and no office data was used.

## Features

| ID | Status | Notes |
|---|---|---|
| `v25_4-appts-insverify-daterange` | built | Min and Max "days until appointment" for the Standard and the Medicaid list; assignment and status-change times kept as datetime; Status Date shows date and time. |
| `v25_4-appts-insverify-carriers` | built | Carrier filter is a pick of one or more carriers (read-only box, `...` and `Clear` buttons); `FormCarriers` has a multi-select mode. Per-user memory of the pick (optional in the spec) not built, see the questions. |

## What was built

### Appointment day range (`v25_4-appts-insverify-daterange`)

- `FormInsVerificationList` (+ designer): the filter box has a new first row, "Min days until appointment", with a Standard and a Medicaid box (`textAppointmentScheduledDaysMinStandard`, `...MinMedicaid`). The existing boxes keep their names and become the Max row ("Max days until appointment"). The two other day rows moved down one row; nothing else moved.
- Min starts at 0 every time the window opens; Max starts from the existing setup preferences (`InsVerifyAppointmentScheduledDays`, `...Medicaid`). So the list a user sees before touching the boxes is the 24.3 list.
- Validation: both boxes are whole numbers from 0 to 99999 (as the other day boxes), and Max may not be below Min. The Max box gets its lower bound from the Min box before every refresh (`ValidNum.MinVal`), so the box shows the usual error icon and the refresh is blocked, like any other invalid day box. `InsVerifies.IsApptDayRangeValid` is a second guard.
- Query: `InsVerifies.GetApptDateRange(today,min,max)` gives today+min through today+max, whole days. `GetVerifyGridList` already took start and end dates; it is now given today+min instead of today. The Past Due tab keeps its own fixed window.
- Assignment time: `insverify.DateLastAssigned` and `insverifyhist.DateLastAssigned` are datetime since M2-01 (with `DateT` CRUD). Every place that stamped the date now stores `DateTime.Now`: the Assign button, the assign-to-user menu, the status change (both grids) in `FormInsVerificationList`, and the batch verification error status in `InsVerifies.InsVerifySetStatus`. One timestamp per user action, so the patient and plan rows of one click get the same time. Resets to `DateTime.MinValue` (`PatPlans` on a dropped plan, `InsVerifyHists.InsertFromInsVerify`, `ClearAssignments`) are unchanged.
- The Status Date column shows date and time (`"g"`, no seconds), sorts by date, and is blank for a row that was never assigned (24.3 printed `1/1/0001`).
- The 30-day auto-unassign compares the datetime column with midnight 30 days ago (`POut.DateT`), so it still clears an assignment once it is more than 30 whole days old, whatever its time of day.
- Integrity hash: the A-HASH hash (already on the base) hashes only the date part of `DateLastAssigned`, so saving a time of day keeps rows valid; tested.

### Carrier filter (`v25_4-appts-insverify-carriers`)

- `FormInsVerificationList`: `textVerifyCarrier` is now read-only and shows "All", the picked carrier names (comma separated, when they fit in 13 characters, or always for one carrier), or "N carriers". `...` (`butCarrierPick`) opens the carrier list in multi-select mode with the current pick selected; `Clear` (`butCarrierClear`) goes back to all carriers. Both refresh the list. The box is no longer a typing filter.
- Before every refresh, carriers of the pick that no longer exist (combined into another carrier, or deleted) are dropped silently (`InsVerifies.GetCarrierNumsStillPresent`, a database check); when none is left, the filter is "All".
- `InsVerifies.GetVerifyGridList`: the `string carrierName` parameter (a `LIKE '%name%'` match) is replaced by `List<long> listCarrierNums`; null or empty means all carriers, otherwise `carrier.CarrierNum IN(...)` (`GetCarrierFilterClause`). The only callers are the window and `TryBatchPatInsVerify` (all carriers).
- `FormCarriers`: new `IsMultiSelectMode` and `ListCarrierNumsSelected` (in: preselected; out: picked carriers in name order). In this mode OK is shown and returns the pick (an empty pick is allowed and means all carriers); a double-click also closes with OK instead of opening the carrier editor. The pick survives changes to the search boxes: selected rows of the listed carriers are picked, unselected listed rows are not, and carriers the search hides keep their state (`MergePickedCarrierNums`). If a picked carrier is hidden, Show Hidden is turned on when the window opens. Single-select mode (`IsSelectMode`) and the plain list are unchanged.

## INFERRED behaviours

The public material describes the two filters only in outline (the spec marks labels and layout VERIFY). Chosen here:

- Labels "Min days until appointment" / "Max days until appointment"; the Min row above the Max row; the existing box names reused for Max.
- Max below Min is shown as an error on the Max box and blocks the refresh (no message box, because the boxes refresh while typing).
- Min resets to 0 on every open; no setup preference (the spec's fork decision).
- The cleanup that runs when the window opens keeps using today through today+Max, not today+Min: an appointment between today and the minimum still keeps the assignment.
- Carrier box: the 13-character rule for names versus "N carriers"; a `Clear` button next to `...`; double-click in multi-select mode closes with OK; OK with nothing picked means all carriers; Show Hidden turned on for a hidden picked carrier.
- The 30-day auto-unassign counts whole days (midnight 30 days ago), as with the date column.
- Status Date blank for never-assigned rows; date-and-time format without seconds.

## Deviations from the backlog and spec

- Tests are in the new project `tests/M2INSVTests`, not in `UnitTests/UnitTests/InsVerifyTests.cs` (the backlog's "InsVerifyTests"): the run instructions require a new project, and the legacy UnitTests project is not part of the fork's suite.
- Per-user memory of the carrier pick (optional in the spec, "userodpref, fork") is not built: it needs a new `UserOdFkeyType` value, which is a foundation-owned registry.
- `FormInsVerificationSetup.cs`, `InsVerifyHists.cs` and `PatPlans.cs` are in the owned list but needed no change (no Min setup default; resets to MinValue stay as they are, as the spec says).

## Interplay with packages built at the same time

- M2-01 (on the base): the datetime column and `DateT` CRUD are used as they are. M2-02: nothing used.
- A-HASH (on the base): `InsVerifies.HashFields` hashes the date part only, so this package needed no hash change.
- M2-HIST (wave C2) takes `InsVerifies.cs` over: the new region "Verification List filters (Open Dental 25.4)" and the changed `GetVerifyGridList` signature (`List<long> listCarrierNums`) are this package's; any new caller passes a list (empty for all carriers).
- M2-INSPLAN and M2-CARRIER (wave C3) may edit `FormCarriers.cs`: the multi-select members (`IsMultiSelectMode`, `ListCarrierNumsSelected`, `MergePickedCarrierNums`, `SyncPickedCarrierNums`, `CloseMultiSelect`) are additive; the existing single-select and list behaviour is unchanged.
- No other C1 package owns these files. No shared hot file, registry, enum, preference, CRUD or migration was changed.

## Files touched

- Changed (owned): `OpenDentBusiness/Data Interface/InsVerifies.cs`, `OpenDental/Forms/FormInsVerificationList.cs`, `OpenDental/Forms/FormInsVerificationList.designer.cs`, `OpenDental/Forms/FormCarriers.cs`.
- New: `tests/M2INSVTests/*` (project, `InsVerifyFilterTests.cs`, `InsVerifyListDbTests.cs`, `Properties/AssemblyInfo.cs`), this note.
- Changed (shared, small): `OpenDental.sln` and `OpenDental.Local.slnf` (the test project only).
- Not changed: `.csproj` files of the product, registries, CRUD, fork migrations.

## Tests

New project `tests/M2INSVTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). It references `OpenDental` only for two static helpers; no window is created.

- `InsVerifyFilterTests` (no database): date range from Min and Max (whole days, Min 0 equals the 24.3 list), the Min/Max rule, the carrier SQL condition, the carrier box text, the multi-select merge rule, the Status Date text, and a source check that no assignment stamp uses `DateTime.Today` any more.
- `InsVerifyListDbTests` (scratch database `forkmig_<time>_<pid>_<random>_m2insv`: a fresh 24.3.63 database migrated to 26.1.70 by the fork runner, dropped afterwards): only appointments from Min through Max days are listed (including late on the last day); the Medicaid pair is independent of the Standard pair; one or more carriers filter the list, empty means all; carriers and day range combine; a combined carrier drops out of the pick and its plans are listed under the kept carrier; `DateLastAssigned` keeps the time of day in `insverify` and `insverifyhist` with valid hashes; the 30-day cleanup clears an assignment 30 days and one second old but keeps one made 30 days ago at midnight or later, and keeps old assignments of patients with an appointment in the list.

Results on the finished branch (2026-10-25): build of `OpenDental.Local.slnf` (Debug, x86) 0 errors, 376 warnings on the first full build (the baseline); a rebuild shows no warning in any touched file. Targeted tests: `M2INSVTests` 17/17, `RowHashTests` 21/21 (insverify hashes, `Update`, `ClearAssignments`, `InsVerifyHists`), `NoHqTests` 49/49. `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- The window itself was not run (OpenDental.exe is not launched in this run): the new layout of the filter box, the ValidNum error on Max, the carrier picker interaction (Ctrl-click, search changes, double-click), and the Status Date column width.
- Middle tier: the changed `GetVerifyGridList` parameter (`List<long>`) and `GetCarrierNumsStillPresent` were not run through the middle tier.
- The batch verification error path (`InsVerifySetStatus`, needs a 270/271 exchange).

## Questions for the owner

1. Labels and layout of the Min/Max boxes are inferred ("Min/Max days until appointment", Min row above Max). Acceptable?
2. Should Insurance Verification Setup get a default for Min (a new preference), or does Min stay 0 on every open?
3. Remember the carrier pick per user? That needs a new `UserOdFkeyType` value from the registry owner.
4. The 30-day auto-unassign counts whole days (an assignment made 30 days ago at 08:00 is kept until tomorrow). Or should it be exactly 30 x 24 hours now that the time is stored?
5. Status Date is blank for rows never assigned (24.3 showed 1/1/0001). Keep?
