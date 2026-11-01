# B-TASK: Task filters (port note)

Package B-TASK of [../specs/port-backlog.md](../specs/port-backlog.md), M1 Tier B, wave B1 (built in the single-wave run on branch
`port/b-task`, from `m1/integration-a2-patch`). Both feature IDs come from the 25.2 "Manage Module" release notes; the behaviour detail is in
[../specs/features-25.2.md](../specs/features-25.2.md).

| Feature ID | Status | Summary |
|---|---|---|
| `v25_2-manage-task-filter-unassigned` | built | The Task Filter's Clinic and Region lists gain an **Unassigned** choice, right after All, that can be multi-selected with real clinics or regions. |
| `v25_2-manage-task-filter-patient` | built | The **Specific Patient** option and its **...** button are shown; the button opens Select Patient (`FrmPatientSelect`) and the choice shows as "Last, First - PatNum". OK refuses Specific Patient with no patient picked. |

No schema change, no preference, no permission and no migration step (the assigned level range 2027000701-2027000799 is unused).

## How it works

- **The Unassigned key is -1** (`TaskFilters.UnassignedNum`), in both lists, as the spec requires: in the task filter lists 0 already means
  "no filter" (the Headquarters default `{ 0 }` that `UserControlTasks.SetFiltersToDefault` sets, and the `isHQClinic`/`isHQRegion` tests in
  `Tasks.BuildFilterWhereClause`). Real ClinicNums and DefNums are never negative.
- **Window** (`WpfControlsOD/Frms/FrmTaskFilter.xaml(.cs)`): when clinics are on, `FillComboBoxClinics` puts a dummy clinic
  (`TaskFilters.GetUnassignedClinic()`, ClinicNum -1, Abbr "Unassigned") first and `FillComboBoxRegions` a dummy region definition
  (`TaskFilters.GetUnassignedRegion()`, DefNum -1). The existing OK code then hands the -1 to `UserControlTasks` unchanged through
  `GetListSelected<Clinic>()`/`GetListSelected<Def>()`, and reopening the window selects it again. Clinic and region stay mutually exclusive, as in 24.3.
- **Queries** (`OpenDentBusiness/Data Interface/Tasks.cs`, one hook in `BuildFilterWhereClause`): after the method has listed the clinics to
  show (`listClinicNumsForQuery`, which always holds clinic 0), `TaskFilters.AddUnassignedClinicNums` adds, for Region: Unassigned, the user's
  clinics with `Region=0`, and returns true when an Unassigned choice is in effect. The clinic condition is then kept even when only clinic 0 is
  listed (before, a one-item `{ 0 }` list meant "do not filter"). Every task query that takes the filter lists (child lists, New, Open Tickets,
  Patient Tickets, Main, Reminders, Repeating, Date/Week/Month trunks) goes through this method.
  - Clinic: Unassigned shows tasks whose attached patient (patient tasks) or appointment (appointment tasks) has ClinicNum 0.
  - Region: Unassigned shows tasks whose patient's or appointment's clinic has no region, plus the clinic-0 tasks (no clinic has no region either).
  - Tasks with nothing attached are never filtered out, as before.
  - Clinic restrictions hold: Region: Unassigned only adds clinics the user may see.
- **Task list text** (`OpenDental/User Controls/UserControlTasks.cs`, two lambdas): the "(filter)" text after the list name shows "Unassigned"
  for the -1 key (`TaskFilters.GetClinicFilterText`/`GetRegionFilterText`).
- **Specific Patient**: the patient filter is the one 24.3 already applies (`patient.PatNum` or `appointment.PatNum`, so tasks attached to the
  patient or to one of the patient's appointments). `PatientSelected` is now set from the checked option when OK is clicked, so it always
  matches what the window shows (before, a stale patient could stay behind when the main-window patient was gone).

## Files touched

- `OpenDentBusiness/Data Interface/TaskFilters.cs` (new): the Unassigned key, the query hook, the texts, the dummy list items, the patient text.
- `OpenDentBusiness/Data Interface/Tasks.cs`: two lines in `BuildFilterWhereClause` (hook call and the `|| isUnassignedFilter` condition).
- `OpenDental/User Controls/UserControlTasks.cs`: two lambdas in the filter text of `FillTree`.
- `WpfControlsOD/Frms/FrmTaskFilter.xaml`, `FrmTaskFilter.xaml.cs`: Unassigned items, Specific Patient, OK check.
- `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line for `TaskFilters.cs`.
- `tests/BTASKTests/` (new test project), `OpenDental.sln`, `OpenDental.Local.slnf`.
- `TaskLists.cs` is owned by this package but needed no change: its filter clauses take the main window's clinic and region
  (`Clinics.ClinicNum`), never the Task Filter's lists, so the -1 key never reaches it. This deviates from the spec's touchpoint list, which
  says `TaskLists.cs` must translate the sentinel.

## INFERRED behaviour (not public; settle by owner testing)

The public sources only say that the Clinic and Region lists gain an Unassigned option and that a specific patient can be chosen. Everything
below is our choice.

1. **Clinic-0 tasks stay visible under every clinic or region filter**, as in 24.3 (`listClinicNumsForQuery` starts with 0, commented "All
   users can see Tasks associated to HQ clinic"). So Clinic: Unassigned alone narrows the list to tasks with no clinic, while "Unassigned + A"
   shows the same tasks as "A". We did not make "A" hide clinic-0 tasks, because that would change what every office's default Clinic or Region
   filter shows. See owner question 1.
2. Region: Unassigned also shows the clinic-0 tasks (a task with no clinic has no region either).
3. The Unassigned choices are offered only when clinics are on (`PrefC.HasClinicsEnabled`). With clinics off the lists are empty anyway.
4. The Unassigned choices are offered even when every clinic has a region (Region) and are placed right after All.
5. The Region default filter type still uses the current clinic's region, and a clinic with no region still means "no region filter" (`{ 0 }`),
   as in 24.3; it does not switch to Region: Unassigned.
6. Choosing Specific Patient before a patient is picked opens Select Patient right away; Cancel leaves the option checked with no patient,
   and OK then says "Please select a patient, or choose a different patient option." The picked patient is remembered while the user tries
   the other options in the same window. Select Patient opens on the previously picked patient (`PatNumInitial`).
7. The date and patient filters keep applying only to child task lists (`Tasks.RefreshChildren`), as in 24.3; the trunks take only the
   clinic and region lists.

## Tests

`tests/BTASKTests` (MSTest v1, x86, .NET 4.8):

- `TaskFilterLogicTests`: the hook on in-memory clinics (clinic and region Unassigned, combined with real choices, restrictions, the clinic
  list winning over the region list, nothing changes without Unassigned), the dummy items and the texts.
- `TaskFilterDbTests`: a scratch database (fresh 24.3.63 database upgraded by the fork migration runner, name
  `forkmig_<run id>_btask`, dropped afterwards) with three clinics (two regions, one with no region), four patients, two appointments and
  seven tasks. Runs `Tasks.RefreshChildren` and `Tasks.RefreshPatientTickets` with the filter lists: All and default, Clinic: Unassigned,
  appointment tasks use the appointment's clinic, Unassigned with real clinics or regions, Region: Unassigned, a clinic-restricted user, one
  specific patient alone and combined with Unassigned, and the filter text.
- `TaskFilterUiSourceTests`: source checks of `FrmTaskFilter` (Specific Patient shown and wired, Select Patient, OK check, Unassigned first in
  both lists) and of the two hooks. The WPF window itself is not opened by tests (the desktop client is never launched).

Untested: the window in a running client (layout of the now-visible Specific Patient row, Select Patient opening from the Task Filter).

## Interplay with packages built in parallel

- **B-APPT** (B2, depends on B-TASK) and **M2-MISC** (C1) also edit `Tasks.cs` and `UserControlTasks.cs`. B-TASK's edits are two lines in
  `Tasks.BuildFilterWhereClause` and two lambdas in `UserControlTasks.FillTree`; the logic is in the new `TaskFilters.cs`. A merge conflict
  can only come from edits to those same lines.
- **A-TASK** (already merged on the base) added the category helpers to `Tasks.cs`; nothing here touches them.

## Questions for the owner

1. Should Clinic: A (or Region: 1) still show tasks of patients with no clinic, as 24.3 does, so that Unassigned is only useful on its own?
   Or should clinic-0 tasks show only when Unassigned is picked? The second is a one-line change in `Tasks.BuildFilterWhereClause` but
   changes what every default filter shows.
2. With the Region default filter and a clinic that has no region, should the list default to Region: Unassigned instead of no region filter?
