# Port note: M2-CLAIMFORM, Denti-Cal DC217 claim form

Branch `port/m2-claimform`, cut from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, M2-CLAIMFORM](../specs/port-backlog.md#m2-claimform-denti-cal-dc217-claim-form). Fork migration levels used: none (the assigned range 2027003601 to 2027003699 cannot be used, see "Deviations").

## Sources

Public sources only. No Open Dental program, install or database was used, and no form artwork was copied.

- Open Dental 25.4 release notes (https://www.opendental.com/site/version25_4.html): one line, a new claim form for Denti-Cal named DC217_2019. Nothing about fields or installation.
- Open Dental manual 24.3, Denti-Cal and Claim Forms pages: the older Denti-Cal form is a downloaded XML plus a background image.
- Medi-Cal Dental provider bulletins (2019 to 2021, found through a web search; their PDF text could not be opened, see below): DC-217 (R 9/13) and DC-217 (R 10/19) are both current laser-printer versions, and the 10/19 forms carry the program's new name and logo, with no change of policy or criteria.
- The DC-217 field positions already in this repository: `OpenDentBusiness/Resources/ClaimForms/ClaimFormDenti-Cal.xml` ("Denti-Cal-13"), Open Dental's GPL layout of the DC-217 on preprinted stock.

Not available: the Medi-Cal Dental Provider Handbook, section 6 (forms), named in the spec. The DHCS server's certificate chain is incomplete, so the fetch tool refused it, and the browser offered the PDF only as a download, which was not taken. So the 10/19 form itself was not measured (see INFERRED 1).

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-claims-dentical-dc217` | partial | Built: the built-in claim form "Denti-Cal DC217_2019" (no background image, 15 service lines, fields mapped with existing claim-form field names), shipped as an internal claim form, an installer that adds it to the office's claim forms, and a fix that lets a claim print on it from the claim window. Not built: the automatic install by a migration step (the level range is unusable and an install row for every office is a foundation seed row; the installer is ready for that step), and positions measured on the 10/19 form itself. |

## What was built

- `OpenDentBusiness/Resources/ClaimForms/ClaimFormDenti-Cal DC217_2019.xml` (new): the form. Description `Denti-Cal DC217_2019`, Arial 9, 850 x 1100 (1/100 inch), `PrintImages` false, no image item, offsets 0, 142 items:
  - patient: `PatientLastFirst`, `PatientIsMale`/`PatientIsFemale`, `PatientDOB`, `PatientMedicaidID` (the Medi-Cal benefits ID), `PatientAddress`, `PatientCity`, `PatientST`, `PatientZip`;
  - questions: `IsRadiographsAttached`, `RadiographsNumAttached`, `OtherInsExists`, `IsAutoAccident` and `IsOtherAccident` (one box), `IsOccupational`, `IsOrtho`;
  - billing provider: `BillingDentist`, `BillingDentistNPI`, `PayToDentistAddress`, `PayToDentistCity`, `PayToDentistST`, `PayToDentistZip`, `BillingDentistPh123`/`Ph456`/`Ph78910`;
  - place of service: `PlaceIsOffice`, `PlaceIsPatientsHome`, `PlaceIsSkilledNursFac`, `PlaceIsAdultLivCareFac`, `PlaceIsInpatHosp`, `PlaceIsOutpatHosp`, `PlaceIsOtherLocation`;
  - 15 service lines, left to right: `P#ToothNumOrArea` (tooth, or arch/quadrant), `P#Surface`, `P#Description`, `P#Date` (`MM/dd/yy`), `P#Code`, `P#Fee`, `P#TreatProvNPI` (rendering provider NPI);
  - `TotalFee`, `Remarks`, `TreatingDentistSignature`, `TreatingDentistSigDate` (two date boxes, `MM/dd/yyyy`).
- `OpenDentBusiness/Properties/Resources.resx` and `Resources.Designer.cs`: resource `ClaimFormDenti_Cal_DC217_2019`. Every resource whose name starts with `ClaimForm` is an internal claim form, so it shows in Setup, Claim Forms, internal list, next to the older "Denti-Cal-13" (kept unchanged).
- `OpenDentBusiness/Data Interface/ClaimForms.cs`:
  - `DescriptionDentiCalDC217_2019` (the description, also the key of an installed copy);
  - `GetInternalClaim(description)`;
  - `InsertInternalIfMissing(description)`: adds the internal form with all its items to `claimform`/`claimformitem` unless a claim form with that description exists, and returns its ClaimFormNum (0 for an unknown description). It uses the CRUD's no-cache inserts, so a migration step can call it.
- `OpenDental/Forms/FormClaimPrint.cs` (fix, 5 lines): the box 18 hospitalization-date check that A-SHEET added (24.4) read `ClaimFormCur.Items` before the printout picks the claim's or the plan's form. Every claim printed or previewed from the claim window, the send list or the primary/secondary prompt (all leave `ClaimFormCur` null) threw a NullReferenceException there, whatever its form. The printout now picks the form at that point with the same rule as the block further down (which then finds it set), and the box 18 check skips a missing form, so the existing "Could not retrieve claim form" message still shows.
- How an office uses the form today: Setup, Claim Forms, select "Denti-Cal DC217_2019" in the internal list, Copy. The copy is an ordinary claim form: set it on the Medi-Cal plan (or as the default), and adjust Offset X/Y in the claim form window after a test print on the stock.
- Multi-form claims need no new code: the printout counts the `P#Fee` items (15), puts lines 16 and on onto further forms, and `TotalFee` totals each form's own lines.

## INFERRED behaviours

1. Positions. The 10/19 revision changed the program name and logo (bulletins), so the box positions are taken to be those of the earlier DC-217 layout in this repository, normalized: every item of a service line on one row (moves of 1/100 inch), the place-of-service row on one row, check marks with no size (two had a stray width). They must be calibrated on real 10/19 stock (spec rule "Calibrate on real stock").
2. Lines 11 to 15 print `ToothNumOrArea` like lines 1 to 10. The old layout used `ToothNumber` there, which leaves the arch or quadrant off those lines.
3. Arch and quadrant print as the area codes the printout already uses (upper arch 01, lower 02, quadrants 10, 20, 30, 40). For a quadrant procedure the description also starts with the quadrant letters (UR, UL, LL, LR). Whether Medi-Cal wants letters in that box is an owner question.
4. TAR: a preauthorization prints without dates of service (existing printout behaviour), which is how a DC-217 reads as a treatment authorization request. No TAR or NOA document control numbers are printed; the claim window already keeps them for e-claims.
5. The billing provider signature date box gets the treating dentist's signature date, as in the old layout (`TreatingDentistSigDate` twice).
6. `UniqueID` blank (deprecated since 17.2), font Arial 9 (as the old form), visible (not hidden) once installed.
7. Boxes with no claim-form field are not printed: for example patient share of cost and other-coverage amounts.

## Deviations from the spec and backlog

- No migration step. The spec says the form is installed by a convert step, like other built-in forms. The assigned level range 2027003601 to 2027003699 does not pass `ForkMigrationRunner.ValidateStep` (levels are yyyyMMddNN with a real date; 20270036 has month 00 and day 36), so a step there would stop every migration, and changing the runner's rule is outside this package. A claim form row added to every office database is also a seed row, which the foundation packages own. `ClaimForms.InsertInternalIfMissing` is the whole body of that step once a level is settled:

  ```csharp
  [ForkMigration(<level>,"Denti-Cal DC217_2019 claim form")]
  public class ClaimFormDentiCalDC217Step:ForkMigrationStep {
  	public override void Apply(ForkMigrationContext context) {
  		ClaimForms.InsertInternalIfMissing(ClaimForms.DescriptionDentiCalDC217_2019);
  	}
  }
  ```
- `FormClaimForms.cs` is unchanged: its Copy button already installs an internal form.
- Test approach "Print alignment on the form": no stock and no printer here (and OpenDental.exe is not launched). The tests check the alignment that can be checked without them (rows, columns, line pitch, no overlap, quarter-inch margins, typical values measured in Arial 9) and run the printout's own fill code on synthetic claims.

## Interplay with packages built at the same time

- None of the other C1 packages owns or edits these files. M2-01 and M2-02 added nothing this package uses; `claimform` and `claimformitem` are unchanged from 24.3 to 26.1.
- Seed owner (M2-02) or integration: add the step above once a valid level is chosen, if every office should get the form automatically (see question 1).
- A-SHEET (merged earlier): the `FormClaimPrint.cs` fix restores printing for every claim form and makes its box 18 dates print on claims printed from the claim window. `SheetTests` pass unchanged.

## Files touched

- New: `OpenDentBusiness/Resources/ClaimForms/ClaimFormDenti-Cal DC217_2019.xml`, `tests/M2CLAIMFORMTests/*`, this note.
- Changed (owned): `OpenDentBusiness/Data Interface/ClaimForms.cs`, `OpenDentBusiness/Properties/Resources.resx` and `Resources.Designer.cs`, `OpenDental/Forms/FormClaimPrint.cs`.
- Changed (shared, one entry each): `OpenDental.sln`, `OpenDental.Local.slnf`.
- Foundation files: none.

## Tests (`tests/M2CLAIMFORMTests`)

- No database: the shipped resource equals the XML file and is listed once among the internal forms; no artwork (no image item, no DC-217 image file in `OpenDentBusiness` or `OpenDental`); every item is a field of the claim form editor's list; the Medi-Cal fields are mapped; 15 service lines and 15 procedures per form (the printout's own count); each line on one row at six lines per inch and each column at one position in DC-217 order; no two boxes overlap and all stay a quarter inch inside the page; typical values (a 14-character Medi-Cal ID, NPIs, dates, fees, MODBL) fit their boxes in Arial 9.
- Scratch database (fresh 24.3.63 database plus every fork step, synthetic rows): the installer adds the form once with every item, a second call and an existing office form with the same description are left alone, an unknown description installs nothing, and Copy from the Claim Forms window gives the same form. A synthetic Medi-Cal claim with 17 procedures (surface, quadrant, arch and single-tooth lines) printed through the plan's form: lines 1 to 15 on form 1 and 16 and 17 on form 2, each with code, fee, date, tooth or area, surface, description and rendering NPI, each form's own total, the patient's Medi-Cal ID and the billing NPI on both forms. A preauthorization prints without dates. A medical claim on the 1500 form printed through the plan's form (the path the fix repairs) gets its box 18 date.

## Checks

- Build of `OpenDental.Local.slnf` (Debug, x86): 0 errors; the test project has no warnings.
- Targeted tests: `M2CLAIMFORMTests` 16/16, `SheetTests` 31/31 (A-SHEET, because of the `FormClaimPrint.cs` fix), `NoHqTests` 49/49.
- `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- Printing on real DC-217 (R 10/19) stock, and Medi-Cal acceptance of a printed form.
- The claim print window itself (OpenDental.exe is not launched); the tests call the printout's fill methods directly.
- Middle tier (`InsertInternalIfMissing` has the usual middle-tier call, not exercised).

## Questions for the owner

1. Automatic install: should every office get "Denti-Cal DC217_2019" in its claim forms by a migration step (as Open Dental installs built-in forms), or only offices that copy it? If automatic, the level range given to this package (2027003601 to 2027003699) needs replacing with a dated level, or the runner's level rule needs widening.
2. Calibration: please print a test claim on 10/19 stock and report any shift; small shifts go into Offset X/Y, a box in the wrong place into the shipped positions. If the handbook's section 6 PDF can be supplied (it is public), the positions can be checked against it.
3. Arch and quadrant: area codes (01, 10, ...) as now, or letters (UA/LA, UR/UL/LL/LR) in the tooth box?
4. Boxes not printed (share of cost, other-coverage amount, a separate billing-provider signature date, TAR/NOA document control numbers): are any needed on paper?
