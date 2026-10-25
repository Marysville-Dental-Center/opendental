# Port note: M2-CARRIER, carrier preauthorization flags and long insurance log values

Branch `port/m2-carrier`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run, beside the other M2 packages. Backlog section: [port-backlog.md, M2-CARRIER](../specs/port-backlog.md#m2-carrier-carrier-preauthorization-flags-and-long-insurance-log-values). No fork migration step: the columns are M2-01's (`carrier.PreAuthCodes`, `inseditlog`/`inseditpatlog` `OldValueBig`/`NewValueBig`), and no registry entry was missing (M2-02 registered the bubble field `PreAuthFlag`). The level range 2027005201 to 2027005299 is unused.

Sources: public only. The 26.1 release notes and summary (version26_1.html, versions.html), the public 26.1 highlights post the spec cites, and the published database documents (`OpenDentalDiffDocumentation25-4.xml` and `26-1.xml`, through the specs in `docs/specs/features-25.4.md` and `features-26.1.md`). No Open Dental program was installed, observed or decompiled; no office data was used.

## Features

| ID | Status | Notes |
|---|---|---|
| `v26_1-lists-carrier-preauth-flags` | built | Edit Carrier list with the procedure code picker and save checks; Chart reminder once per charting action with "Create Preauth Now" and "Later"; Treatment Plan marker; `PreAuthFlag` appointment view element and bubble field; Carrier Combine union; changes logged in the insurance edit log. |
| `v25_4-family-inseditlog-bigvalues` | built | Values over 255 characters go in `OldValueBig`/`NewValueBig` (short column empty) for both logs; the two log windows show whichever is filled. |

## What was built

### Rules (`OpenDentBusiness/Data Interface/CarrierPreAuths.cs`, new)

- Stored form: entries separated by commas, a plain hyphen inside a range, no spaces. Matching uses the shared code-group parser `ProcCodeRanges` (`CodeGroups.cs`, from `v24_4-setup-codegroup-ranges`): a range holds the codes with the same letter prefix and a numeric part of the same length between its ends; a single code matches whole, without regard to case.
- Display: `ToDisplay` turns hyphens into U+2011 (non-breaking hyphen); `FromDisplay` turns U+2011, and the dashes a paste can bring (U+2010, en dash, em dash, minus sign), back into `-`.
- `Check` (save): refuses spaces inside the list, empty entries (`,,`, leading or trailing comma) and ranges whose start is after their end; ignores whitespace around the whole text; drops repeated entries (first kept, without regard to case); lists single codes that are not in the procedure code list so the window can warn (ranges are not looked up).
- `GetNeeds` (the "needs preauth" rule): status TP or TPi, not a Canadian lab procedure; one of the patient's plans (patplan → inssub → insplan) belongs to a carrier whose list holds the code; no claimproc with status Preauth (sent or not) for that procedure and plan; and (INFERRED) the procedure's estimate for that plan is not marked Do Not Bill Ins. One entry per procedure and plan, ordered by plan ordinal; the same plan through two subscriptions counts once.
- `IsInUse` (cache only): every caller checks it first, so an office with no carrier list runs no extra query anywhere.
- Database readers: `GetNeedsForProcNums`, `GetNeedsForAppointments` (by `AptNum`, or `PlannedAptNum` for planned appointments), `GetClaimProcsForNeeds`; all with the Middle Tier pattern.
- `CombineCodes`: Carrier Combine gives the kept carrier the union of its list and the combined carriers' lists (kept list first, then new entries in carrier order), writes it with the CRUD update and logs it in the insurance edit log. Called from `Carriers.Combine` (one line) before the other carriers are deleted.

### Long insurance edit log values (`OpenDentBusiness/Data Interface/InsEditLogBigValues.cs`, new)

- `MoveLongValues`: a value over 255 characters moves to the Big column and the short column is left empty; 255 or fewer stays. Each side is decided on its own. Called by `InsEditLogs.Insert`, `InsEditLogs.InsertMany` and `InsEditPatLogs.InsertMany` (one line each), so every writer follows the rule, including the manual `MakeLogEntry(... doInsert:false)` logs that Carrier Combine inserts later.
- Values are cut to what a text column holds (65,535 bytes in UTF-8), never inside a surrogate pair.
- `GetOldValue`/`GetNewValue`: the Big column when filled, otherwise the short one. `FormInsEditLogs` and `FormInsEditPatLog` show these (two lines each).

### User interface

- **Edit Carrier** (`FormCarrierEdit`): "Procedures requiring preauthorization" box (multi-line, so long lists wrap between entries) with a "..." button that adds codes from the procedure code list (multi-select). Ranges show with non-breaking hyphens. Save refuses an invalid list and asks before saving unknown codes. Read-only without CarrierEdit/CarrierCreate, like the other fields. The security log message names the change ("preauth codes"). Designer: three controls added under the dependent-plans box; the CDAnet check box and group, the two buttons and the client height moved down 42 pixels (the non-Canada shrink still works). Helpers are in `FormCarrierEdit.PreAuth.cs` (new partial).
- **Chart** (`ControlChart.PreAuth.cs`, new partial; `ControlChart.cs` gains one call at the end of `AddProcedure` and of `AddQuick`): every added procedure is queued and a 100 ms timer shows one reminder (`FormPreAuthReminder`, new) when the charting action is over: it waits while any modal dialog keeps the main window disabled (the edit window of the next procedure of the same action, auto codes, message boxes). The statuses are read again then, so a procedure changed to complete in its edit window is not listed. The reminder lists code, tooth, surface, description, carrier and plan (primary, secondary).
- **Create Preauth Now** (`OpenDental/Logic/PreAuthL.cs`, new): one preauthorization per plan named, primary first; each is built as the Treatment Plan module's PreAuthorization button builds it (clearinghouse checks, Do Not Bill Ins, Canadian 7-procedure limit, same clinic and place of service, $0 procedure rule, provider and billing provider, ortho flag, alternate and medical codes, default claim notes, `Claims.CalculateAndUpdate`) and opened in Edit Claim as new (cancel deletes it). The relationship comes from the patient's patplan. The Chart refreshes after a save. `ControlTreat.cs` is not changed for this: its method depends on its grids, so the steps are repeated (see owner questions).
- **Treatment Plan** (`ControlTreat.PreAuth.cs`, new partial; `ControlTreat.cs` gains one call before `EndUpdate` in `FillMainDisplay`): in a current (Active or Inactive) treatment plan the Description cell of a procedure that still needs a preauthorization starts with "[PreAuth]" in red; the Code cell when Description is not shown. Saved plans and printouts are not marked. The flag clears by itself once a preauth claimproc exists.
- **Appointments**: `EnumApptViewElement.PreAuthFlag` (added last, number 46, stored by name), so Appointment View Edit lists it with the other elements without a change to `FormApptViewEdit`. `Appointments.GetPeriodApptsTable` gains one call at the end (`Appointments.PreAuth.cs`, new partial) that adds `preAuthFlag` ("PreAuth"), `preAuthCodes` and `preAuthColor` (the first needing carrier's appointment text back color, when it has one). `ControlApptPanel.cs` gains one `case` in `DrawElement` and one in the bubble; the drawing is in `ControlApptPanel.PreAuth.cs` (new partial): in the main area a "PreAuth" line in the element's color, highlighted with the carrier's color when set; in the upper and lower right a boxed tag like the other flag letters (carrier color as the box fill with black text, otherwise white box with the element's color). Bubble: "PreAuth needed: <codes>" in red, highlighted with the carrier's color when set.

## INFERRED behaviours

- "In the carrier's appointment text color": read as `carrier.ApptTextBackColor` (the only carrier color, documented as the highlight in the appointment and bubble). Used as a highlight behind the tag; the text keeps the element's color. Black and transparent mean no color, as the Insurance Color element treats them.
- Treatment Plan presentation: "[PreAuth]" prefix in red on the Description cell (the announcement only says the procedure is marked).
- Bubble text: "PreAuth needed:" followed by the codes.
- Do Not Bill Ins on the procedure's estimate for a plan means that plan needs no preauthorization.
- Repeated entries are dropped on save; whitespace around the whole list is ignored; a hyphenated entry that is not a well-formed range (for example `D1110-2`) is one code.
- Carrier Combine: union of the lists, kept carrier's entries first (the spec marks this rule INFERRED).
- "Create Preauth Now" with several plans: one preauthorization per plan, primary first; stops at the first one the user cancels.
- One charting action = everything charted before control returns to an idle main window (timer plus enabled-window check).

## Deviations from the spec

- `FormCarrierCombine` is not changed: the union happens in `Carriers.Combine`, which the window calls.
- `FormApptViewEdit` and `ApptViewItemL` are not changed: the element list comes from the enum.
- `v26_1-lists-proccode-no-hyphens` (codes may not contain the separators) is another package's item; until it is merged, a code with a hyphen in it still works as one entry, but it can be confused with a range only when both ends look like codes of the same shape.

## Interplay with packages built at the same time

- **M2-01 / M2-02** (on the base): columns and CRUD, and `DisplayFields26_1.ApptBubblePreAuthFlag`, are used as they are.
- **V-PAYSUITE** takes `Carriers.cs` and possibly `FormCarrierEdit` over from this package (`carrier.PaySuiteTransSup`). Its designer edits will conflict with the 42-pixel shift here; keep both sets of controls and re-stack them.
- **M2-INSPLAN** owns `FormInsPlan`: carriers created there through `Carriers.GetIdentical` start with an empty list, like the other carrier-only settings; an existing carrier found there keeps its list.
- **M2-SHELL** and **M2-NOTES** edit `ControlChart.cs`, **M2-EST**/**M2-TERM** edit `ControlTreat.cs`, **M2-APPT** edits `Appointments.cs`: this package adds only the one-line calls named above, at the ends of `AddProcedure`/`AddQuick`, before `gridMain.EndUpdate()` in `FillMainDisplay`, and before `return table;` in `GetPeriodApptsTable`. If another package rewrites one of those methods, keep the call at the same place.
- **M2-SCH2** (excluded, dental school) takes files over from this package: nothing to do.

## Files touched

- New: `OpenDentBusiness/Data Interface/{CarrierPreAuths,InsEditLogBigValues,Appointments.PreAuth}.cs`; `OpenDental/Main Modules/{ControlChart.PreAuth,ControlTreat.PreAuth}.cs`; `OpenDental/UI/Appt/ControlApptPanel.PreAuth.cs`; `OpenDental/Forms/{FormCarrierEdit.PreAuth,FormPreAuthReminder,FormPreAuthReminder.Designer}.cs`; `OpenDental/Logic/PreAuthL.cs`; `tests/M2CARRIERTests/*`; this note.
- Changed (owned, one-line hooks unless noted): `OpenDentBusiness/Data Interface/{Appointments,Carriers,InsEditLogs,InsEditPatLogs}.cs`; `OpenDentBusiness/TableTypes/ApptViewItem.cs` (enum member); `OpenDental/Main Modules/{ControlChart,ControlTreat}.cs`; `OpenDental/UI/Appt/ControlApptPanel.cs` (two cases); `OpenDental/Forms/FormCarrierEdit.cs` (load, save, read-only, log line) and `FormCarrierEdit.designer.cs` (three controls, layout shift); `OpenDental/Forms/{FormInsEditLogs,FormInsEditPatLog}.cs` (two lines each).
- Changed (shared, outside the owned list): `OpenDentBusiness/OpenDentBusiness.csproj` (3 compile entries), `OpenDental/OpenDental.csproj` (7 compile entries), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project). `ControlApptPanel.cs` and `Carriers.cs` have mixed line endings in the repository; the edits keep every other line's ending.

## Tests

New project `tests/M2CARRIERTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `PreAuthCodesTextTests`: display with non-breaking hyphens and back; save checks (spaces, empty entries, backward ranges, repeats, unknown codes warned, hyphenated single code); Carrier Combine merge.
- `PreAuthNeedsTests`: matching with the shared range parser (ranges, codes, case, prefix and length); every rule of `GetNeeds` (statuses, carriers without a list, plan order, preauth clears per plan, other claimprocs do not clear, Do Not Bill Ins, labs, repeats, same plan twice, no insurance).
- `InsEditLogBigValuesTests`: 255/256 boundary, each side on its own, patient log, nulls, text-column limit and surrogate pairs.
- `ApptViewAndHookTests`: `PreAuthFlag` is last (46) and earlier members keep their numbers, stored and read by name; bubble field registered; carrier color rule; source checks that the shared files gained exactly the hooks described above and that the new files are compiled.
- `CarrierPreAuthDbTests` (scratch database `m2carrier_<time>_<pid>_<random>` built from `UnitTests/Resources/dump.txt`, the 24.3 convert chain and the fork runner, synthetic rows and made-up `Z` codes, dropped afterwards): needs read from the database and cleared by a preauth claimproc; nothing when no carrier has a list; appointments and planned appointments; the appointment table's three columns (flag, codes, carrier color, black means none, single-appointment form, columns present and empty when unused); carrier field change logged (long list in `NewValueBig`); Carrier Combine union and no-change case; long plan note and subscriber note written to the Big columns and read back through the log readers.

Checks (2026-10-25): build of `OpenDental.Local.slnf` (Debug, x86) 0 errors, 376 warnings (baseline). Targeted tests all passed: `M2CARRIERTests` 55/55, `ApptTests` 76/76, `InsuranceTests` 49/49, `M202Tests` 39/39, `RegistryTests` 33/33, `M201Tests` 24 passed and 2 inconclusive (the published-document checks; `M201_OD_DOCS_DIR` not set), `NoHqTests` 49/49. `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- No UI was run (OpenDental.exe is not launched in this run): the Edit Carrier layout, the reminder timing in the Chart, the Treatment Plan marker, the appointment drawing and the bubble are covered only by the business-layer tests and the source checks.
- "Create Preauth Now" (`PreAuthL`) opens Edit Claim, so it is not run in tests; it follows the Treatment Plan module's PreAuthorization steps line by line.
- Middle Tier.

## Questions for the owner

1. Presentation: is "[PreAuth]" in red on the Treatment Plan description, and the carrier's appointment color as a highlight behind the appointment tag, what the office wants? Both are inferred.
2. Should Do Not Bill Ins on a procedure's estimate keep the flag off for that plan (chosen), or should the flag show anyway?
3. "Create Preauth Now" repeats the Treatment Plan module's preauthorization steps in `PreAuthL`. Should a later package make `ControlTreat.ToolBarMainPreAuth_Click` call `PreAuthL.CreatePreAuth` so the two cannot drift apart (a small change in a hot file)?
4. When the patient's primary and secondary carriers both want a preauthorization, one is created per plan, primary first. Is that the wanted order, or only the primary?
