# Open Dental 25.4: desktop feature specs for the 24.3-based fork

Status: draft spec, 2026-10-18. Baseline: this fork (Open Dental 24.3.63). This file covers every line of the Open Dental 25.4 release page (stable 04/13/2026). The office runs 25.3.48, so nothing here is in the office's data yet except the items backported to 25.3.48 or earlier. 25.4 sits between our first target (25.3, for adopting the office database) and our second target (26.1.70). Its schema must be in place before 26.1 data can be adopted.

Everything below is written in our own words from Open Dental's public material. Nothing was decompiled.

## Sources and method

- **Release notes:** https://www.opendental.com/site/version25_4.html. The page has no links into the manual.
- **Manual:** since 24.4, Open Dental's full help pages are no longer public. They open only from inside the program for customers on support (https://www.opendental.com/site/helpfeature.html, https://www.opendental.com/site/documentation.html). We did **not** use those gated pages.
  - Checked 2026-10-18: the `/help2xx/` pages that the public pages link to (for example `/help263/globalpaymentsv2transaction.html`) redirect to a customer login. `/manual253/` and `/manual254/` return 404.
  - `/manual/` is still public, but it is a frozen snapshot labeled "v24.4 +". Its pages predate 25.4 (for example `/manual/insverifylist.html` still shows the single "days until appointment" box and a free-text carrier box), so it is not a source for 25.4 behavior.
  - Public material we used:
    - the public **Quick Manual** and vendor/program-link pages under https://www.opendental.com/site/ (these describe the current version, 26.x)
    - the public **24.3 manual** under https://www.opendental.com/manual243/, for baseline behavior
    - the 25.4 blog post: https://opendental.blog/version-25-4/
- **Database documentation:**
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (full 25.4 schema)
  - https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml (25.3 to 25.4 diff)
  - We also diffed the full 25.3 and 25.4 documents ourselves. That found the same table and column changes, plus enum and description changes that the diff document leaves out.
  - Shared DDL for every schema change lives in `docs/specs/schema-changes.json` (entries with `"version": "25.4"`). Types below match it.
- **25.3 DDL cross-check** (`D:/Dev/opendental-schema/fox-db-schema.sql`): every 25.4 addition below is absent from the 25.3.48 dump, as expected. These columns were confirmed against the dump:
  - `referral.Telephone varchar(10)`
  - `insverify.DateLastAssigned date`
  - `payterminal` without `CCIntegration`
  - `schoolcoursedef` without `GradingScaleNum`
  - `evaluation`/`evaluationdef` without the new columns
  - Tables and columns already present from 25.3: `schoolapproval`, `schoolcoursedef`, `schoolcoursesched`, `reqneeded.SchoolCourseDefNum`, `reqstudent.ProcNum`, `schoolcourse.SchoolClassNum/DateStart/DateEnd/GradingScaleNum`, `benefit.ToothRange`, `computerpref.CreditCardTerminalId`, `insverify.SecurityHash`. All of these also appear in our measured diff (`schema-diff-24.3.63-vs-25.3.48.txt`), and none of the 25.4 additions do.
  - `insplan.InsPlansZeroWriteOffsOnAnnualMaxOverride` and `InsPlansZeroWriteOffsOnFreqOrAgingOverride` appear in the 25.4 diff document, but they already exist in the 25.3.48 dump and in our 24.3 code. Only their descriptions changed (the "standard provider fee" wording). No DDL.
- **Codebase:** touchpoints were found by grepping this repo (main worktree only). Paths and cited line numbers were re-checked on 2026-10-18 (see "Fact-check log" at the end).

**Markers used below**

- **VERIFY**: public material does not describe the behavior. Before release, an engineer confirms it by ordinary use of an Open Dental 25.4+ install (not reverse engineering), or by reading data in a restored database copy.
- **Fork decision**: our chosen design where Open Dental's is unknown or doesn't apply to us.
- Preference names that Open Dental added in 25.4 are not public. Names we propose are marked **name VERIFY**. They matter only when later data (26.1) is adopted: the migration must map Open Dental's real preference rows onto ours.

**Already specified in `features-25.3.md`.** Several 25.4 lines were backported into 25.3 builds. Each has its own 25.4 entry here for traceability, marked "implement once". Where we found extra public detail, the 25.4 entry adds it.

## Summary

| ID | Title | Module | Category | Size | Schema | Depends on |
|---|---|---|---|---|---|---|
| v25_4-dentalschool-courses | Course instructors, student enrollment, course-def grading scale | Dental Schools | desktop | L | schoolcourseenrollee, schoolcourseinstructor (new); schoolcoursedef.GradingScaleNum | v25_3-school-course-scheduling, v25_4-dentalschool-permissions |
| v25_4-dentalschool-requirements | Course and graduation requirements | Dental Schools | desktop | L | none new (reqneeded/reqstudent 25.3 columns) | v25_4-dentalschool-courses |
| v25_4-dentalschool-approvals | Instructor approval of procedures, appointments, treatment plans | Dental Schools | desktop | L | schoolapproval (25.3 table) | v25_3-school-approval-schema, v25_4-dentalschool-courses |
| v25_4-dentalschool-planners | Instructor Planner and Student Planner | Dental Schools | desktop | L | none new | courses, requirements, approvals, evaluations |
| v25_4-dentalschool-evaluations | Evaluations inside Edit Dental School Course; grade overrides; course grades | Dental Schools | desktop | M | evaluation.GradeOverride, evaluationdef.SchoolCourseDefNum | v25_4-dentalschool-courses |
| v25_4-dentalschool-permissions | Dental school permissions renamed and redefined | Security | desktop | S | none (enum 77/78/81) | none |
| v25_4-canada-paysuite-setup | PaySuite program link, carrier support flags, extended reversals, plan details | Canada | vendor-direct | L | carrier.PaySuiteTransSup | fork local background service |
| v25_4-canada-paysuite-posting | PaySuite automatic payment posting and reconciliation | Canada | vendor-direct | XL | paysuitepayment, paysuitepaymentdetail | v25_4-canada-paysuite-setup |
| v25_4-canada-paysuite-reports | PaySuite expected payments and reconciliation windows | Canada | vendor-direct | L | none new | v25_4-canada-paysuite-posting |
| v25_4-payments-globalpayments-v2 | Global Payments V2 program link (terminals, tap/wallet payments) | Account / Payments | vendor-direct | XL | payterminal.CCIntegration; CreditCardSource 23 | v25_4-payments-globalpayments-v1-rename, v25_1-account-payconnect2-terminal-memory |
| v25_4-payments-globalpayments-v1-rename | EdgeExpress program link renamed Global Payments V1 | Account / Payments | vendor-direct | S | none | none |
| v25_4-account-service-date-view | Service Date View easier to reach and use; columns in Display Fields | Account | desktop | M | DisplayFieldCategory 22 | v25_4-misc-enum-parity |
| v25_4-account-incometransfer-limit | Preference: limit manual income transfers to patient payments | Account | desktop | S | none (preference) | none |
| v25_4-account-recurringcharge-fail-email | Automatic email when a recurring charge fails | Account | desktop | M | none (preferences) | none |
| v25_4-canada-cda-codes-2026 | 2026 CDA procedure codes | Canada | desktop | S | none | v25_3-canada-cda-codes-2026 (implement once) |
| v25_4-canada-other-proc-codes | Other Procedure Codes window (import other Canadian code systems) | Canada | desktop | M | none | none |
| v25_4-canada-codes-cdha | CDHA (hygienist) code system | Canada | desktop | S | none | v25_4-canada-other-proc-codes |
| v25_4-canada-codes-dac | DAC (denturist) code system, English and French | Canada | desktop | S | none | v25_4-canada-other-proc-codes |
| v25_4-canada-codes-ramq | RAMQ (Quebec) code system with R suffix | Canada | desktop | S | none | v25_4-canada-other-proc-codes |
| v25_4-canada-labfee-pay | "Pay Labs" when receiving claims with lab fees | Canada | desktop | S | none | none |
| v25_4-appts-insverify-daterange | Insurance Verification List: appointment date range | Appointments | desktop | M | insverify/insverifyhist.DateLastAssigned to datetime | none |
| v25_4-appts-insverify-carriers | Insurance Verification List: pick one or more carriers | Appointments | desktop | S | none | none |
| v25_4-appts-blockout-permissions | Blockouts permission split into Add, Edit, Delete | Appointments / Security | desktop | M | none (enum 13, 272, 273) | v25_1-appt-blockouts-flagged-permission |
| v25_4-chart-perio-direction | Perio Chart direction radio buttons improved | Chart | desktop | S | none | none |
| v25_4-chart-groupnote-size | Larger Notes box in Group Note | Chart | desktop | S | none | v25_1-chart-group-note-flexible |
| v25_4-chart-procnote-merge | Procedure Note Merge window on concurrent edits | Chart | desktop | M | none | none |
| v25_4-family-benefit-tooth-exclusions | Teeth entry for exclusions from the Age Limits and Other Benefits grids | Family | desktop | M | benefit.ToothRange (25.3) | v25_3-family-exclusions-tooth |
| v25_4-family-writeoff-override-wording | Clearer wording for Write-Offs for Non-Covered Services overrides | Family | desktop | S | none | v25_4-misc-ucr-terminology |
| v25_4-family-clerri | Clerri membership-plan program link | Family | vendor-direct | M | none | none |
| v25_4-tools-dbm-base64-documents | DBM tool: Create Documents From Base64 Notes | Tools | desktop | S | none | v25_3-tools-dbm-base64-docs (implement once) |
| v25_4-misc-diagnostics-info | More details in the Diagnostics window | Help | desktop | S | none | none |
| v25_4-misc-print-window | Minor Print window enhancements | Misc / Printing | desktop | S | none (printer rules clarified) | v25_3-misc-printer-options |
| v25_4-reports-udsplus | UDS+ Report for FQHC Dental Services | Reports | desktop | L | none | none |
| v25_4-claims-dentical-dc217 | Denti-Cal DC217_2019 claim form | Claims | desktop | M | none | none |
| v25_4-sheets-nonlatin | Sheets support non-Latin languages | Sheets | desktop | M | none | none |
| v25_4-eforms-patfields | eForms support custom patient field definitions | eForms | desktop | M | none (eformfield(def).DbLink content) | v24_4-eforms-desktop-forms |
| v25_4-sheets-duplicate-field-warning | Warning when a duplicate field is added to a sheet | Sheets | desktop | S | none | none |
| v25_4-eforms-richtext-paste | Rich text kept when pasting into eForm labels | eForms | desktop | M | none | v24_4-eforms-desktop-forms |
| v25_4-eforms-multi-import | Import several eForms at once | eForms | desktop | S | none | v24_4-eforms-desktop-forms |
| v25_4-setup-pem-path-label | PEM path label in Choose Database and Reports Setup | Setup | desktop | S | none | v25_3-misc-pem-path-label (implement once) |
| v25_4-reports-era-autoprocessed-filters | ERAs Automatically Processed report: PreAuth and $0 filters | Reports | desktop | S | none | none |
| v25_4-bridges-trophy-enhanced-tags | Trophy (Enhanced) bridge accepts command-line replacement tags | Program Links | vendor-direct | S | none | none |
| v25_4-bridges-cleverone | Clever One (VATECH) imaging bridge | Program Links | vendor-direct | S | none | none |
| v25_4-proccodes-codegroup-reset | Procedure Code Tools: reset Code Groups | Procedures | desktop | M | none | v25_3-procs-codegroup-reset-tool (implement once) |
| v25_4-misc-ucr-terminology | "UCR" renamed "Standard Provider Fee" throughout | Misc | desktop | M | none | v25_3-family-bluebook-providerfee |
| v25_4-tp-estimates-appt-date | Treatment plan estimates use the scheduled appointment date | Treatment Plan | desktop | L | none | none |
| v25_4-chart-nobillins-audit | Per-procedure "Do Not Bill Insurance" flag, with audit trail | Chart / Account | desktop | M | procedurelog.NoBillIns | none |
| v25_4-claims-era-displayfields | ERA Claim Paid grid columns in Display Fields | Account / ERA | desktop | M | DisplayFieldCategory 23 | v25_3-account-era-displayfields (implement once) |
| v25_4-sheets-datetimesubmitted | Sheet and eForm "date submitted" | Sheets / eForms | desktop | S | sheet.DateTimeSubmitted, eform.DateTimeSubmitted | none |
| v25_4-family-inseditlog-bigvalues | Insurance edit logs keep values longer than 255 characters | Family | desktop | S | inseditlog/inseditpatlog.OldValueBig, NewValueBig | none |
| v25_4-family-referral-phone | Referral phone no longer limited to 10 digits | Family | desktop | S | referral.Telephone varchar(30) | none |
| v25_4-security-patientselected-audit | Audit entry when a patient is selected | Security | desktop | S | none (enum 275) | none |
| v25_4-misc-enum-parity | Enum values needed so 25.4+ data loads safely | Database | desktop | S | none (enum values) | v25_3-schema-compat-columns |
| v25_4-imaging-16bit-windowing | Correct windowing for true 16-bit radiographs | Imaging | desktop | S | none (document.WindowingMin/Max meaning) | none |
| v25_4-eclipboard-ocr | OCR for eClipboard | eServices | excluded-od-service | n/a | | |
| v25_4-eclipboard-setup-reorg | eClipboard setup window reorganized | eServices | excluded-od-service | n/a | | |
| v25_4-eclipboard-appt-datetime | Appointment date and time in eClipboard 25 | eServices | excluded-od-service | n/a | | |
| v25_4-canada-oda-feeguide-2026 | 2026 ODA fee guides | Canada | excluded-od-service | n/a | | |
| v25_4-canada-bcda-feeguide-2026 | 2026 BCDA fee guides | Canada | excluded-od-service | n/a | | |
| v25_4-paymentportal-inactivity-logout | Payment Portal inactivity prompt and logout | eServices | excluded-od-service | n/a | | |
| v25_4-payments-gpv2-paymentportal | Global Payments V2 in the Payment Portal | eServices | excluded-od-service | n/a | | |
| v25_4-eservices-alert-types | eConnector/ODTouch/eClipboard alert types | eServices | excluded-od-service | n/a | | |
| v25_4-clinics-add-process | New process for adding clinics (HQ-granted clinic access) | Setup | excluded-other | n/a | registrationkey.DateTClinicAccess | |
| v25_4-hq-internal-enums | HQ-only enum values (Job Manager testing team, address validation) | HQ | excluded-other | n/a | | |
| v25_4-api-* (24 entries) | Local REST API changes | API | excluded-other | n/a | | |

Counts: 46 desktop, 8 vendor-direct, 34 excluded (10 above plus 24 API lines listed individually in the Excluded section).

**Notes for planning**

- The **dental school** items (6 entries, about XL in total) matter only to schools. The target office is a private practice, so they can be scheduled late. Still, their schema must exist for later data adoption.
- **PaySuite** and the **Canadian code systems** matter only to Canadian offices.
- For a US private practice, the highest-value 25.4 items are:
  - v25_4-tp-estimates-appt-date
  - v25_4-chart-nobillins-audit
  - v25_4-appts-blockout-permissions
  - v25_4-appts-insverify-daterange and v25_4-appts-insverify-carriers
  - v25_4-chart-procnote-merge
  - v25_4-account-recurringcharge-fail-email
  - v25_4-misc-ucr-terminology
- **Backports into 25.4 builds that the 26.1 page lists** are specified in `features-26.1.md`, not here:
  - ODTouch 25 (excluded)
  - Make Recall searches for openings (25.4.50)
  - Topaz with Cloud (excluded)
  - eForm delete audit (25.4.79)

## Schema delta 25.3 to 25.4

| Change | DDL (see schema-changes.json) | Spec item |
|---|---|---|
| new table `paysuitepayment` | 14 columns, MyISAM | v25_4-canada-paysuite-posting |
| new table `paysuitepaymentdetail` | `DetailsJson mediumtext` | v25_4-canada-paysuite-posting |
| new table `schoolcourseenrollee` | `GradeNumber`/`GradeOverride float NOT NULL DEFAULT -1` | v25_4-dentalschool-courses |
| new table `schoolcourseinstructor` | course + instructor link | v25_4-dentalschool-courses |
| `carrier.PaySuiteTransSup` | `tinyint(4) NOT NULL` (0) | v25_4-canada-paysuite-setup |
| `eform.DateTimeSubmitted`, `sheet.DateTimeSubmitted` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | v25_4-sheets-datetimesubmitted |
| `evaluation.GradeOverride` | `float NOT NULL DEFAULT -1` | v25_4-dentalschool-evaluations |
| `evaluationdef.SchoolCourseDefNum` | `bigint(20) NOT NULL`, indexed | v25_4-dentalschool-evaluations |
| `schoolcoursedef.GradingScaleNum` | `bigint(20) NOT NULL`, indexed | v25_4-dentalschool-courses |
| `inseditlog`/`inseditpatlog` `.OldValueBig`, `.NewValueBig` | `text NOT NULL` | v25_4-family-inseditlog-bigvalues |
| `insverify`/`insverifyhist` `.DateLastAssigned` | `date` to `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | v25_4-appts-insverify-daterange |
| `payterminal.CCIntegration` | `varchar(50) NOT NULL` | v25_4-payments-globalpayments-v2 |
| `procedurelog.NoBillIns` | `tinyint(4) NOT NULL` (0) | v25_4-chart-nobillins-audit |
| `referral.Telephone` | `varchar(10)` to `varchar(30) DEFAULT ''` | v25_4-family-referral-phone |
| `registrationkey.DateTClinicAccess` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` (carry only) | v25_4-clinics-add-process (excluded) |

Enum changes are listed under v25_4-misc-enum-parity.

---

## Major changes

### v25_4-dentalschool-courses: Course instructors, student enrollment, grading scale on course definitions

- **Category:** desktop. **Size:** L. **Depends on:** v25_3-school-course-scheduling (course definitions, course schedules, `schoolcourse.SchoolClassNum/DateStart/DateEnd/GradingScaleNum`), v25_4-dentalschool-permissions.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (Major Changes: Overhauled Dental Schools)
  - https://www.opendental.com/site/0_dentalschools.html (public Quick Manual: classes, courses, instructors, planners)
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`schoolcourseenrollee`, `schoolcourseinstructor`, `schoolcoursedef.GradingScaleNum`, `schoolcourse` summary)
  - Baseline: https://www.opendental.com/manual243/dscourses.html, https://www.opendental.com/manual243/dsproviders.html

**Behavior.**

- A scheduled course (`schoolcourse`) gets an explicit list of **instructors** and an explicit list of **enrolled students**.
- Until 25.3, every student in the course's class was simply assumed to be in the course.
- Each enrollment carries the student's **course grade**: computed, plus an optional instructor override (see v25_4-dentalschool-evaluations).
- Course definitions (`schoolcoursedef`, the course directory) now carry a **grading scale**. Courses created from the definition inherit it.

**UI** (**fork layout**: the public material names the windows but does not show their 25.4 layout).

- **Edit Dental School Course** (`FormSchoolCourseEdit`) becomes a tabbed window:
  - **General**: Course ID, Description, Class, Start/End dates, Grading Scale (the 25.3 fields).
  - **Schedule**: 25.3 schedule and overrides.
  - **Instructors**: grid with columns Name, Abbr, ProvNum; Add (picks from providers flagged as instructors) and Remove.
  - **Students**: grid with columns Name, Abbr, Class, Grade, Override. Buttons:
    - Add Student
    - Remove
    - Sync with Class (adds every student of the course's class not yet enrolled)
    - Set Override (enter a grade, or clear it back to -1)
  - **Evaluations** and **Requirements** tabs come from v25_4-dentalschool-evaluations and v25_4-dentalschool-requirements.
- **Course definition edit** (25.3 window): adds a Grading Scale picker.
- **Dental School Courses** list (`FormSchoolCourses`): add columns Class, Dates, # Students, Instructors.

**Rules and edge cases.**

- Creating a course from a definition copies the definition's grading scale, evaluation definitions, requirements and schedule. Later edits to the definition never change existing courses. (The 25.4 schema doc says courses, and their requirements, are copied from the definition. Copying the scale, evaluation definitions and schedule is our reading.)
- All evaluation definitions of a course must use the course's scale (schema doc rule, on both `schoolcourse` and `schoolcoursedef`).
- The 25.4 schema doc still says `schoolcourse.SchoolClassNum` "can be zero for older courses", so a course with no class is valid.
- **Fork decisions** (not described publicly):
  - When a course's Class is set or changed, offer to enroll all students of that class. Students already enrolled are kept.
  - Removing an enrollee who has evaluations, completed requirements or approvals in that course: warn, and require confirmation. Their evaluations stay in the database.
  - The course grading scale can't change once any evaluation for the course exists.
  - A provider can't be both instructor and enrollee on the same course.
- **Conversion (fork decision)** for existing data:
  - For each `schoolcourse` with `SchoolClassNum>0`, insert one `schoolcourseenrollee` per student provider whose `provider.SchoolClassNum` matches, with grades -1.
  - Insert `schoolcourseinstructor` rows from the distinct `evaluation.InstructNum` per course.
  - For `schoolcoursedef.GradingScaleNum`: leave 0 (none).

**Permissions/audit.**

- Editing courses requires **Setup**.
- Editing student and instructor records requires **SchoolAdminStudentEdit** or **SchoolAdminInstructorEdit** (enum 77/78).
- Log course roster changes under Setup: "Course PEDO 732: enrolled/removed <student>".

**Preferences:** none.

**Schema:**

- `schoolcourseenrollee` (`SchoolCourseEnrolleeNum` PK, `SchoolCourseNum` FK, `StudentNum` FK to provider, `GradeNumber float DEFAULT -1`, `GradeOverride float DEFAULT -1`), indexes on both FKs.
- `schoolcourseinstructor` (`SchoolCourseInstructorNum` PK, `SchoolCourseNum`, `InstructorNum`), both indexed.
- `schoolcoursedef.GradingScaleNum bigint(20) NOT NULL` + index.

**Touchpoints:**

- `OpenDental/Forms/FormSchoolCourseEdit.cs` (+ `FormSchoolCourseEdit.designer.cs`), `OpenDental/Forms/FormSchoolCourses.cs`
- `OpenDentBusiness/TableTypes/SchoolCourse.cs`, `OpenDentBusiness/Data Interface/SchoolCourses.cs`
- new `OpenDentBusiness/TableTypes/SchoolCourseEnrollee.cs`, `SchoolCourseInstructor.cs` + `Data Interface` classes + generated CRUD (`xCrudGenerator/`)
- `OpenDental/Forms/FormProviderSetup.cs`, `OpenDental/Forms/FormProvStudentEdit.cs`
- `OpenDental/Main Modules/FormOpenDental.cs` (`menuItemSchoolCourses_Click`)
- a new ConvertDatabases step (`OpenDentBusiness/Misc/ConvertDatabases8.cs` is the latest file)

### v25_4-dentalschool-requirements: Course and graduation requirements

- **Category:** desktop. **Size:** L. **Depends on:** v25_4-dentalschool-courses.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (course and graduation requirements)
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`reqneeded`: course-def, course and graduation scopes; `reqstudent.ProcNum`)
  - Baseline: https://www.opendental.com/manual243/requirements.html

**Behavior.** Requirements can be defined at three levels:

1. **Course definition**: a template copied into each course scheduled from it.
2. **Scheduled course**: what students of that course must complete.
3. **Graduation**: tied to a class cohort, not to a course. These are things a student must complete before graduating.

Students complete a requirement by linking it to a patient, an appointment and now a **procedure**. The 24.3 baseline only allowed linking to a patient and appointment.

**UI.**

- **Requirements Needed** (`FormReqNeededs`) gets a scope selector: Course Definition / Course / Graduation. It shows the Course Def, Course or Class picker to match.
  - Copy works within the same scope, as in 24.3.
  - "Copy from definition" re-copies missing definition requirements into a course.
- **Procedure Info** (`FormProcEdit`), dental schools only: a "Requirement" picker listing the open requirements of the procedure's student provider, both course and graduation. Saving creates or updates a `reqstudent` row with `ProcNum`, `PatNum`, `AptNum`, student and instructor.
- The appointment Req window (`FormReqAppt`) keeps working and also offers graduation requirements.
- **Student Requirements** (`FormReqStudentOne`/`FormReqStudentsMany`) shows Course, Graduation and Completed/Needed counts. Detail rows show the linked procedure.

**Rules and edge cases.**

- `reqneeded` scope encoding, per the 25.4 schema doc:
  - Course definition: `SchoolCourseDefNum>0`, `SchoolCourseNum=0`, `SchoolClassNum=0`.
  - Course: `SchoolCourseNum>0`, with `SchoolClassNum` set.
  - Graduation: `SchoolClassNum>0`, `SchoolCourseNum=0`, `SchoolCourseDefNum=0`.
- `reqstudent.SchoolCourseNum` is documented as never 0. For graduation requirements, store the course the work was done in. If none applies, use the student's most recent enrolled course (**VERIFY**).
- One requirement may be completed several times (multiple `reqstudent` rows). Progress counts distinct rows.
- Deleting a procedure that fulfils a requirement: detach (`ProcNum=0`) but keep the completion only if an instructor confirmed it. Otherwise delete the completion. **Fork decision.**
- Requirements stay pass/fail (no grades).

**Permissions/audit.**

- Defining requirements requires **Setup**.
- Students may attach their own work.
- Only an instructor of the course, or **SchoolAdminAccess**, may delete a completion or change its instructor (24.3 rule, extended).
- Log completions under ProcEdit/AppointmentEdit with requirement text.

**Preferences:** none. **Schema:** none new. Uses the 25.3 columns `reqneeded.SchoolCourseDefNum` and `reqstudent.ProcNum`, present in the 25.3.48 dump.

**Touchpoints:**

- `OpenDental/Forms/FormReqNeededs.cs`, `FormReqNeededEdit.cs`, `FormReqStudentOne.cs`, `FormReqStudentsMany.cs`, `FormReqStudentEdit.cs`, `FormReqAppt.cs`
- `OpenDentBusiness/Data Interface/ReqNeededs.cs`, `ReqStudents.cs`, `OpenDentBusiness/TableTypes/ReqNeeded.cs`, `ReqStudent.cs`
- `OpenDental/Forms/FormProcEdit.cs`

### v25_4-dentalschool-approvals: Instructor approval of procedures, appointments and treatment plans

- **Category:** desktop. **Size:** L. **Depends on:** v25_3-school-approval-schema, v25_4-dentalschool-courses.
- **Sources:** https://www.opendental.com/site/version25_4.html (instructors can sign off on a student's procedures, appointments and treatment plans); https://www.opendental.com/site/0_dentalschools.html (instructors manage approvals for students in their courses); https://www.opendental.com/OpenDentalDocumentation25-4.xml (`schoolapproval`).

**Behavior.**

- When a **student** creates or changes a clinical item, the item needs an instructor's sign-off. The release names procedures, appointments and treatment plans. The table also has columns for perio exams, allergies, problems, documents and mounts; treat those as reserved.
- The student requests approval. An instructor approves or rejects it.
- Status shows next to the item, and pending items appear in the Instructor Planner.

**UI** (**fork design**; Open Dental's 25.4 screens are not public).

- Right-click menu entries **Request Approval**, **Approve**, **Reject**, **Approval History** in:
  - Chart progress notes (procedures)
  - Treatment Plan module plan list (treatment plans)
  - Appointment Edit (appointments)
- A narrow **Approval** column or status icon in the Chart progress notes grid and the Treatment Plan list. It is visible only when Dental Schools is on.
- Approve/Reject asks for an optional comment. Store the comment as a commlog on the patient: **fork decision**, since the table has no note column.

**Rules and edge cases.**

- `SignOffStatus` values are not published. **VERIFY** against Open Dental 25.4 data before release.
  - Fork values: 0=None, 1=Pending, 2=Approved, 3=Rejected.
  - The target office is not a school, so no rows are expected. If rows exist at migration, map them after VERIFY.
- One row per request. A new request after a rejection is a new row; history is the list of rows. `SecDateEntry` and `SecDateTEdit` are set by the database.
- Exactly one of the item FKs is non-zero.
- Only a provider flagged as instructor, linked to the logged-on user, may approve. They must also be an instructor (`schoolcourseinstructor`) of a course the student is enrolled in, unless they hold **SchoolAdminAccess**.
- If the student edits an approved item, the approval resets to Pending. **Fork decision:** this applies to fee, code, tooth or surface, status and note changes on procedures; time, operatory and procedures on appointments; and procedure changes on treatment plans.
- Whether approval blocks billing or completing is not published (**VERIFY**). Fork default: no hard block, warn only.

**Permissions/audit.**

- Requesting needs no extra permission.
- Approving or rejecting requires instructor status as above.
- Each status change writes a securitylog entry under ProcEdit, AppointmentEdit or TreatPlanEdit, for example "Instructor DOEJ approved procedure D2391 #3 (student SMIA)".

**Preferences:** none (Dental Schools must be on: `EasyHideDentalSchools` false).

**Schema:** `schoolapproval` (25.3 table, already in the dump; all FKs indexed).

**Touchpoints:**

- new `OpenDentBusiness/TableTypes/SchoolApproval.cs`, `OpenDentBusiness/Data Interface/SchoolApprovals.cs` (CRUD from the 25.3 item)
- `OpenDental/Main Modules/ControlChart.cs` (progress notes menu)
- `OpenDental/Main Modules/ControlTreat.cs`
- `OpenDental/Forms/FormApptEdit.cs`, `OpenDental/Forms/FormProcEdit.cs`

### v25_4-dentalschool-planners: Instructor Planner and Student Planner

- **Category:** desktop. **Size:** L. **Depends on:** v25_4-dentalschool-courses, v25_4-dentalschool-requirements, v25_4-dentalschool-approvals, v25_4-dentalschool-evaluations.
- **Sources:** https://www.opendental.com/site/version25_4.html (two new planners, one for instructors and one for students, that track courses and requirements); https://www.opendental.com/site/0_dentalschools.html (instructors manage their courses from the Instructor Planner; students see schedule, appointments and requirements in the Student Planner).

**Behavior** (the grids below are a **fork design**; the public pages list only what each planner is for). Two dashboards, opened from the main menu (Tools, Dental School Planner) and from the user's toolbar when Dental Schools is on. The menu entry opens the planner that matches the logged-on user's linked provider: instructor or student.

**Student Planner UI:**

- Header: student, class, current date range.
- **Courses** grid: Course ID, Description, Instructors, Grade (override if set, else computed, shown via the grading scale).
- **Schedule** grid: course sessions in the date range, from `schoolcoursesched` with overrides and cancellations applied.
- **Appointments** grid: future appointments where the student is the provider or hygienist.
- **Requirements** grid: Requirement, Scope (Course/Graduation), Completed/Needed.
- **Approvals** grid: the student's Pending and Rejected items.
- Double-click opens the item.

**Instructor Planner UI:**

- **My Courses**: from `schoolcourseinstructor`.
- **Today's sessions**.
- **Pending approvals**: across students of the instructor's courses, with Approve/Reject buttons.
- **Students**: enrollees of the selected course, with grade and requirements progress.
- **Evaluations**: create a new one or open existing.
- An **Open Student Planner** button shows the selected student's planner read-only.

**Rules.**

- Date range defaults to the current week, with week navigation.
- A user with **SchoolAdminAccess** gets a picker to open any instructor's or student's planner. They act with that person's abilities, per the 25.4 permission text; log every action taken this way under the admin's user with "on behalf of".
- Planners are read-mostly. Edits go through the normal windows and their permissions.

**Permissions/audit:** see above. **Preferences:** optionally remember the last date range per user in `userodpref` (fork). **Schema:** none.

**Touchpoints:**

- new `WpfControlsOD/Frms/FrmDentalSchoolPlannerStudent.xaml(.cs)`, `FrmDentalSchoolPlannerInstructor.xaml(.cs)`
- `OpenDental/Main Modules/FormOpenDentalMenus.cs`, `OpenDental/Main Modules/FormOpenDental.cs` (`menuItemReqStudents_Click` area)

### v25_4-dentalschool-evaluations: Evaluations inside Edit Dental School Course; grade overrides

- **Category:** desktop. **Size:** M. **Depends on:** v25_4-dentalschool-courses.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (Evaluations moved into Edit Dental School Course)
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`evaluation.GradeOverride`, `evaluationdef.SchoolCourseDefNum`, `schoolcourseenrollee.GradeNumber/GradeOverride`)
  - Baseline: https://www.opendental.com/manual243/dsevaluationsetup.html

**Behavior.**

- Evaluation definitions are no longer managed from Dental School Setup, Evaluations. They live on an **Evaluations** tab of Edit Dental School Course, and of the course definition window.
- Instructors can enter a **grade override** on a single evaluation.
- The student's **course grade** is calculated and stored on the enrollment. An instructor can override it too.

**UI.**

- Course/course-def window, Evaluations tab:
  - definition grid (Title, Criteria count) with Add, Duplicate, Edit, Delete. These are the 24.3 `FormEvaluationDefs` actions, filtered to this course.
  - for scheduled courses, a second grid of filled-in evaluations (Date, Student, Instructor, Title, Grade).
- `FormEvaluationEdit` gets an **Override** box next to the calculated grade. The shown grade uses the override when it isn't -1.
- Dental School Setup loses its Evaluations button. It keeps user group defaults and grading scales.

**Rules.**

- An `evaluationdef` belongs either to a course definition (`SchoolCourseDefNum>0`, `SchoolCourseNum=0`) or to a course (the reverse). Copy on course creation.
- Every evaluation definition, and its criteria, must use the course's grading scale. Enforce on save. The scale picker is removed from the definition window and shown read-only.
- **Course grade:**
  - `GradeNumber` is the average of the student's evaluations for the course, using each evaluation's override when present. All evaluations weigh the same.
  - Recalculate whenever an evaluation is saved or deleted. With no evaluations, it is -1.
  - Display: for pick-list scales, the nearest scale item; for percentage and weighted scales, the number as a percent.
  - `schoolcourseenrollee.GradeOverride`, when not -1, wins.

**Permissions/audit.**

- Creating evaluations: instructors (24.3 rule).
- Editing someone else's evaluation or any override: course instructor or **SchoolAdminAccess**.
- Log grade overrides under Setup with old and new values.

**Preferences:** none.

**Schema:** `evaluation.GradeOverride float NOT NULL DEFAULT -1`; `evaluationdef.SchoolCourseDefNum bigint(20) NOT NULL` + index. Conversion: set existing `GradeOverride=-1`.

**Touchpoints:**

- `OpenDental/Forms/FormEvaluationDefs.cs`, `FormEvaluationDefEdit.cs`, `FormEvaluationEdit.cs`, `FormEvaluations.cs`, `FormEvaluationReport.cs`, `FormDentalSchoolSetup.cs`, `FormSchoolCourseEdit.cs`
- `OpenDentBusiness/Data Interface/Evaluations.cs`, `EvaluationDefs.cs`, `GradingScales.cs`
- `OpenDentBusiness/TableTypes/Evaluation.cs`, `EvaluationDef.cs`

### v25_4-dentalschool-permissions: Dental school permissions renamed and redefined

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/OpenDentalDocumentation25-4.xml (`EnumPermType` 77, 78, 81); baseline https://www.opendental.com/manual243/dssecurity.html.

**Behavior.** The numbers stay the same, so existing `grouppermission` rows carry over. Names and meanings change:

| Value | 24.3 name | 25.4 name | 25.4 meaning |
|---|---|---|---|
| 77 | AdminDentalStudents ("Student Edit") | SchoolAdminStudentEdit | administer (add/edit) student records |
| 78 | AdminDentalInstructors ("Instructor Edit") | SchoolAdminInstructorEdit | administer instructor records. No longer implies grading; instructors grade through course membership. |
| 81 | AdminDentalEvaluations ("Admin Evaluation Edit") | SchoolAdminAccess | full access to every instructor's and student's records; may act on their behalf in the planners |

**Rules.** Rename the enum members and update the security-tree text and descriptions. Review each check that used 78 or 81. Grading now depends on being a course instructor; 81 now grants broad access.

- Open Dental's published schema spells member 81 `SchoolAdminAcesss` (typo). Permissions are stored by number, so we use the correct spelling `SchoolAdminAccess`; data is unaffected.
- The 24.3 summary of 81 says it is for emergency evaluation edits only (`GroupPermission.cs` line 280). The 25.4 meaning is much broader, so the conversion should **not** silently widen access. **Fork decision:** keep existing 81 grants, but show an upgrade note listing user groups that hold 81 so an admin can review them.

**Audit:** permission changes are logged as today. **Schema:** none.

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (lines 267-281), `OpenDental/User Controls/UserControlSecurityTree.cs`, every reference to the old names (grep `AdminDental`).

### v25_4-canada-paysuite-setup: PaySuite program link and carrier capabilities

- **Category:** vendor-direct (Canada only). **Size:** L. **Depends on:** a local background service in our fork (the job host that replaces Open Dental Service; see the decoupling plan, rule 6).
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (Major Changes: PaySuite)
  - https://www.opendental.com/site/paysuite.html
  - https://www.opendental.com/site/paysuitesetup.html
  - https://www.opendental.com/site/canadainsplan.html (Request Electronic Benefits row in Edit Insurance Plan)
  - https://opendental.blog/version-25-4/ (extended reversal window, enhanced claim status, expected payments)
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`carrier.PaySuiteTransSup`)

**Behavior.**

- PaySuite (Instream Canada) pays assigned insurance benefits directly to the office and reports payment details back.
- The office signs up with PaySuite, then enables the program link with its own API key and secret.
- Our desktop and local service call PaySuite's API directly. Nothing goes through Open Dental.
- Features enabled by the link:
  - automatic posting (separate item)
  - **extended claim reversals**: reversing a claim more than 24 hours after it was sent, for carriers that support it (the 24-hour figure is from the `carrier.PaySuiteTransSup` enum text in the 25.4 schema doc)
  - **plan detail requests**: benefit details, for supporting carriers
  - **enhanced claim status / EOB retrieval**
  - an **expected payment date** lookup
  - **discrepancy detection**: claims that don't match PaySuite's data are reported back (posting item)

**UI.**

- **Program Links, PaySuite** (visible only in Canadian regional settings; hidden until enabled, per the neutrality rule). Open Dental lists it as "PaySuite from https://instreamcanada.com"; use that `ProgDesc` for data compatibility. Fields per paysuitesetup.html:
  - Enabled
  - API Key (required), Secret Key (required). If the user pastes PaySuite's combined "key|secret" string into either box, split it at the pipe.
  - Deposit Account: enabled only when accounting automatic deposit entries are set up
  - "If not enabled, hide in Standard Reports"
  - "Hide in Reports menu"
- **Carrier edit** (`FormCarrierEdit`), Canadian section: **PaySuite supports** combo with None / Extended Reversals / Plan Details / Both.
- **Claim edit**, Canada: when "Reverse" is used more than 24 hours after the claim was sent and the carrier supports Extended Reversals, offer "Reverse through PaySuite" (fork placement).
- **Edit Insurance Plan** (Canada layout): a **Request Electronic Benefits** row with a **Request** button. Per https://www.opendental.com/site/canadainsplan.html it is available only when PaySuite is in use and the carrier is supported by PaySuite. It shows the returned plan details read-only and can save them as a patient document (fork).
- **Claim edit**: a "PaySuite Status" button showing enhanced status and the EOB, plus the expected payment date when PaySuite knows it (fork placement).

**Rules.**

- Keys are stored as program properties under the fork's secret storage (decoupling plan, CDT replacement), never in plain text.
- `carrier.PaySuiteTransSup` values: 0 None, 1 ExtendedReversal, 2 PlanDetails, 3 Both. Default 0.
  - **Fork decision:** keep it editable by hand. If PaySuite's API offers a carrier capability list, add a "Refresh from PaySuite" action.
  - The public PaySuite page names six payors known to work (Chamber of Commerce, Industrial Alliance, Johnston Group, Manulife, Maximum Benefit, Simply Benefits). It does not say which transaction types each supports, so ship no defaults.
- Extended reversals and plan details are refused, with a clear message, when the carrier flag doesn't allow them.
- All calls go out over HTTPS from the workstation or local service. Timeouts and errors are shown and logged locally, never silently ignored.
- The public PaySuite page says offices also install the Open Dental Service (our local job host replaces it) and CCDWS. CCDWS is the Telus/Instream claim web-service client our ITRANS 2.0 code already drives (`OpenDentBusiness/Eclaims/Canadian.cs`, `ccdws.ini` handling around line 1540). Claim traffic keeps using it, as today.

**Permissions/audit.**

- Setup for the program link.
- Claim reversals use the existing claim send/edit permissions.
- Log enabling, disabling and key changes under Setup, without the key values.

**Preferences:** program properties only (Enabled, API Key, Secret Key, Deposit Account, hide flags).

**Schema:** `carrier.PaySuiteTransSup tinyint(4) NOT NULL` (default 0).

**Touchpoints:**

- `OpenDentBusiness/TableTypes/Program.cs` (`ProgramName` enum: add PaySuite; internal name **VERIFY** for later data adoption)
- `OpenDental/Forms/FormProgramLinks.cs`, new `FormPaySuiteSetup`
- `OpenDental/Forms/FormCarrierEdit.cs`, `OpenDentBusiness/TableTypes/Carrier.cs`
- `OpenDental/Forms/FormInsPlan.cs` (+ designer; Request Electronic Benefits row on the Canadian layout)
- `OpenDentBusiness/Eclaims/Canadian.cs` (reversal path), `OpenDental/Forms/FormClaimEdit.cs`
- new `OpenDentBusiness/WebBridges/PaySuite.cs` (API client)

### v25_4-canada-paysuite-posting: Automatic PaySuite payment posting and reconciliation

- **Category:** vendor-direct. **Size:** XL. **Depends on:** v25_4-canada-paysuite-setup.
- **Sources:** https://www.opendental.com/site/paysuitelogic.html; https://www.opendental.com/OpenDentalDocumentation25-4.xml (`paysuitepayment`, `paysuitepaymentdetail`, `EnumReconciliationStatus`, `AlertType.PaySuite=50`, `FormType.FrmPaySuiteReconciliation=21`).

**Behavior.**

- Every night at midnight (office server time), the local background service asks PaySuite for the payment list. It records new payments and status changes.
- Payments that become **Paid** are matched to claims and posted: claims received, insurance check created, deposit created.
- Payments reversed after posting are flagged for the user. Discrepancies are reported back to PaySuite.

**Rules (from the public logic page and the schema doc).**

1. **Upsert** `paysuitepayment` by `PaymentId`.
   - Store PaySuite fields: provider (CDA number, matching `provider.NationalProvID`), method (C cheque / D direct deposit), reference, amount, date, status, reversal reason, associated payment.
   - Store the large detail JSON in `paysuitepaymentdetail.DetailsJson`, linked by `PaySuitePaymentDetailNum`.
2. **Set `ReconciliationStatus`:**
   - 0 DoNotProcess: Issued, UnderThreshold, or first seen already Reversed.
   - 1 ReadyToProcess: inserted or updated as Paid, or changed Paid to Reversed.
   - After processing:
     - 2 MultiClaimMatches
     - 3 DiscrepanciesFound
     - 4 NoDiscrepanciesFound
3. **Posting** (ReadyToProcess and Paid):
   - Use the CDAnet-format claims and EOBs inside the payment details to find each claim. Match on the transaction reference and office claim number, then patient, provider and date (**VERIFY** exact keys against PaySuite's API documentation).
   - Matched claims in a valid state are received with the EOB amounts.
   - Create one `claimpayment` (insurance check): check number = `PaymentReference`, amount = payment amount, date = `PaymentDate`.
   - Link the claimpayment through `ClaimPaymentNum`.
   - Payment-level adjustments and PaySuite fees that belong to no claim are recorded on that claimpayment without claimprocs (**VERIFY** representation; fork: claimpayment note line plus a total adjustment).
   - Claims missing or invalid are skipped, which leaves a partial check.
   - If the check covers the full payment amount, also create a **deposit**. When a Deposit Account is configured, make the accounting entries too (the existing automatic deposit logic).
4. **Claim reversal adjustments** inside a payment apply to one claim. Match them like claims; report a discrepancy if no claim is found.
5. **Multiple matches:** receive or reverse nothing for that claim. Set MultiClaimMatches. The user fixes the duplicates in the reconciliation window, after which the payment returns to ReadyToProcess.
6. **Discrepancies** (amounts, patient or procedure differences): set DiscrepanciesFound and send the discrepancy report to PaySuite.
7. **HasUnresolvedClaimPayment** is set when either:
   - the payment is Paid but the linked check amount, its claimprocs' sum, or the claimproc set doesn't agree, or
   - the payment became Reversed while a check is still linked.

   Never delete or reverse posted money automatically; the user resolves it.
8. After each run, create an `alertitem` of type PaySuite (50) summarizing new postings and problems, with FormToOpen = PaySuite Reconciliation (21).

**Permissions/audit.**

- Automated actions log with LogSource OpenDentalService (24.3 enum value), or our service's equivalent, under InsPayCreate and DepositSlips.
- Manual fixes use InsPayEdit and ClaimProcReceivedEdit.

**Preferences:** fork-defined `PaySuiteLastRunDateTime` (**name VERIFY**) and a run-time setting (default 00:00).

**Schema:**

- `paysuitepayment`: `PaySuitePaymentNum` PK, varchar(255) text fields, `PaymentAmount double`, `PaymentDate date`, `PaySuitePaymentDetailNum`, `HasUnresolvedClaimPayment tinyint`, `ReconciliationStatus tinyint`, `ClaimPaymentNum` FK
- `paysuitepaymentdetail` (`DetailsJson mediumtext`)
- Indexes on `PaySuitePaymentDetailNum` and `ClaimPaymentNum`, as in `schema-changes.json`. That file has **no** index on `PaymentId`. The nightly upsert looks rows up by `PaymentId`, so a `PaymentId` index is a reasonable fork addition; if we add it, add it to `schema-changes.json` as well, so the two stay in step.

**Touchpoints:**

- new `OpenDentBusiness/TableTypes/PaySuitePayment.cs`, `PaySuitePaymentDetail.cs` + Data Interface + CRUD
- `OpenDentBusiness/Data Interface/ClaimPayments.cs`, `ClaimProcs.cs`, `Deposits.cs`, `AlertItems.cs`
- `OpenDentBusiness/TableTypes/AlertItem.cs` (AlertType 50, FormType 21)
- `OpenDentBusiness/Eclaims/Canadian.cs` (EOB parsing reuse)
- the fork's local service host

### v25_4-canada-paysuite-reports: PaySuite expected payments and reconciliation

- **Category:** vendor-direct. **Size:** L. **Depends on:** v25_4-canada-paysuite-posting.
- **Sources:** https://www.opendental.com/site/version25_4.html (reports for expected payments and payment reconciliation); https://www.opendental.com/site/paysuitesetup.html (Standard Reports and Reports-menu entries, hide options).

**UI.**

- **PaySuite Reconciliation** window (Reports menu, Standard Reports, or alert click):
  - Filters: date range, provider, payment status, reconciliation status, "only unresolved".
  - Payments grid: Date, PaymentId, Provider, Method, Reference, Amount, Status, Reconciliation, Unresolved.
  - Claims grid for the selected payment: Patient, Date of Service, Carrier, Claim fee, PaySuite paid, Matched claim, Discrepancy.
  - Actions:
    - **Resolve Duplicates** (pick the correct claim among the matches)
    - **Reprocess**
    - **Open Claim**
    - **Open Insurance Payment**
    - **Mark Resolved** (clears HasUnresolvedClaimPayment after the user fixes the money)
    - **Export CSV**
- **PaySuite Expected Payments** report: Issued and future payments, plus outstanding claims with PaySuite's expected payment date. Columns: Provider, Payment/Claim, Expected Date, Amount. Print and Export.

**Rules.** Both appear only when the link is enabled, unless the hide options say otherwise. Report access follows Report Setup security (a `displayreport` row per report).

**Permissions/audit:** Reports plus per-report security. Resolve actions log under InsPayEdit.

**Schema:** none.

**Touchpoints:** new `FrmPaySuiteReconciliation` / `FormRpPaySuiteExpected`; `OpenDentBusiness/Data Interface/DisplayReports.cs`; `OpenDental/Forms/FormReportsMore.cs`.

### v25_4-payments-globalpayments-v2: Global Payments V2 program link

- **Category:** vendor-direct. **Size:** XL. **Depends on:** v25_4-payments-globalpayments-v1-rename, v25_1-account-payconnect2-terminal-memory (`computerpref.CreditCardTerminalId`).
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (Major Changes: Global Payments V2; tap payments including Apple Pay and Google Pay on a compatible terminal)
  - https://www.opendental.com/site/globalpaymentsv2setup.html
  - https://www.opendental.com/site/globalpaymentsintegration.html
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml: internal name **MeetInTheCloud** (Global Payments' cloud terminal service); `CreditCardSource` 23/25/26; `payterminal.CCIntegration`; `computerpref.CreditCardTerminalId` reused

**Behavior.**

- A US card-processing integration that drives Global Payments terminals through Global Payments' cloud terminal API, called directly from the desktop with the office's own account credential.
- Supports:
  - card-present sales, refunds and voids on a terminal, including contactless and wallet taps
  - saving a token for later charges
  - recurring charges using saved tokens

**UI.**

- **Program Links, Global Payments V2** (hidden until enabled). Open Dental lists it as "Global Payments V2 from www.globalpaymentsintegrated.com"; use that `ProgDesc`. Fields per globalpaymentsv2setup.html:
  - Enabled (all clinics)
  - Clinic picker (per-clinic settings)
  - Payment Type (default pay type)
  - Account Credential
  - Print receipts by default
  - Prevent saving new cards
  - Recurring charges force duplicates by default
  - The public page's "Enable payments for Payment Portal and eClipboard" box is **not built** (excluded).
- **Terminals** grid: Terminal Name, Terminal ID, with Add Terminal / Edit (double-click) / Delete (`FormPayTerminalEdit`). Terminal ID is the device's serial number (public setup page and the `payterminal.TerminalID` doc). A deleted terminal is no longer offered when taking payments.
- **Payment window** (`FormPayment`): a **Global Payments V2** button opens a transaction window:
  - Terminal (defaults to this workstation's last terminal)
  - Type: Sale / Refund / Void / Save card
  - Amount, and Save card checkbox
  - Status text while waiting on the terminal
  - Cancel
- Receipt prints to the Receipts printer situation when "print receipts" is on.
- **Credit Card Manage**: cards saved through V2 are listed with source "Global Payments V2".

**Rules.**

- Only one of XCharge, Global Payments V1 and Global Payments V2 may be enabled at a time. Enforce on save of any of the three.
- `payterminal.CCIntegration` holds the integration a terminal belongs to.
  - V2 terminals use "MeetInTheCloud" (**VERIFY** exact string).
  - Existing PayConnect terminal rows: the migration sets `CCIntegration='PayConnect'` where it is empty.
  - Each integration's terminal list filters by this column.
- Remember the last terminal per workstation in `computerpref.CreditCardTerminalId`. The documentation says PayConnect 2.0 and V2 share this field.
- Duplicate protection: same amount, same card, same day is declined unless "force duplicates" is set (recurring charges) or the user overrides.
- New payments get `payment.PaymentSource`/`creditcard.CCSource` = MeetInTheCloudTerminal (23).
- Token storage column: **VERIFY**. Global Payments V1 reuses `creditcard.XChargeToken`, so V2 likely does too. Never store the PAN.
- "Prevent saving new cards", when checked, stops users from running transactions on new cards and from saving new card details; saved cards still work. The "Automatically store credit card tokens" preference decides whether a token is saved after each sale.
- "Print receipts by default" prints to the default receipt printer when a transaction finishes.
- All traffic goes from the workstation to Global Payments over TLS. Credentials use the fork's secret storage.

**Permissions/audit.**

- Taking payments requires PaymentCreate.
- Terminal use requires CreditCardTerminal (24.3 enum).
- Saving or removing cards requires CreditCardEdit.
- Setup changes require Setup.
- Log sales, refunds and voids with amount, last 4 digits and terminal name. Never log tokens.

**Preferences:** program properties per clinic (listed above). **name VERIFY** for later adoption.

**Schema:** `payterminal.CCIntegration varchar(50) NOT NULL`; CreditCardSource 23 (25 and 26 are portal-only; add them for data parity only).

**Touchpoints:**

- `OpenDentBusiness/TableTypes/Program.cs` (new ProgramName), `OpenDentBusiness/TableTypes/PayTerminal.cs`, `OpenDentBusiness/Data Interface/PayTerminals.cs`
- `OpenDental/Forms/FormPayTerminalEdit.cs`
- `OpenDentBusiness/TableTypes/CreditCard.cs` (CreditCardSource enum, line 130)
- `OpenDental/Forms/FormPayment.cs`
- `OpenDentBusiness/Data Interface/RecurringCharges.cs` (`RecurringChargerator`)
- `OpenDental/Forms/FormCreditRecurringCharges.cs`
- `OpenDentBusiness/Data Interface/ComputerPrefs.cs`
- new `OpenDentBusiness/WebBridges/GlobalPaymentsV2.cs`, `OpenDental/Forms/FormGlobalPaymentsV2Setup.cs` and transaction window
- Model on `OpenDental/Forms/FormPayConnect2.cs` and `OpenDentBusiness/WebBridges/EdgeExpress.cs`

### v25_4-payments-globalpayments-v1-rename: EdgeExpress renamed Global Payments V1

- **Category:** vendor-direct. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/globalpaymentssetup.html (the link was previously labeled EdgeExpress; V1 also works in Canada); https://www.opendental.com/site/edgeexpresssetup.html.

**Behavior.** The EdgeExpress program link, setup window, payment buttons and receipts say **Global Payments V1** (or "Global Payments"). Behavior is unchanged.

**Rules.**

- Keep `ProgramName.EdgeExpress` and the `program.ProgName` value, so data is compatible. Change only `ProgDesc`/labels. The public V1 setup page shows the Program Links entry as "Global Payments from www.globalpaymentsintegrated.com" (previously "EdgeExpress from www.globalpaymentsintegrated.com"), with no "V1" in the entry itself; the window title and docs say "Global Payments V1". Use those exact strings.
- Mutual exclusivity with V2 and XCharge, as in the V2 item.
- Update the payment source descriptions: "EdgeExpress" to "Global Payments V1".

**Touchpoints:**

- `OpenDental/Forms/FormEdgeExpressSetup.cs`, `OpenDental/Forms/FormEdgeExpressTrans.cs`
- `OpenDentBusiness/WebBridges/EdgeExpress.cs`
- `OpenDentBusiness/TableTypes/CreditCard.cs` (descriptions)
- a ConvertDatabases step updating `program.ProgDesc`

**Schema:** none.

---

## Account module

### v25_4-account-service-date-view: Service Date View easier to reach and use

- **Category:** desktop. **Size:** M. **Depends on:** v25_4-misc-enum-parity (DisplayFieldCategory 22). Followed by v26_1-account-service-date-view, which moves the view into the main grid in 26.1.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html
  - https://www.opendental.com/site/0_account.html (Main tab button)
  - https://www.opendental.com/OpenDentalDocumentation25-3.xml and 25-4 (`DisplayFieldCategory.AccountModuleServiceDateView = 22`, described in 25.3 as a placeholder for 25.4)
  - Baseline: https://www.opendental.com/manual243/servicedateview.html

**Behavior (25.4 scope; details VERIFY).**

- Service Date View lists each charge with the credits applied beneath it. 25.4 makes it quicker to open and adds configurable columns.
- 26.1 later integrates it into the account grid, so don't build that here.

**UI.**

- Keep the Main-tab button in the Account module.
- Also add **Service Date View** to the Account toolbar's Statement/Payment area and to the right-click menu of the account grid (fork placement).
- The window is modeless:
  - It follows the Account module's selected patient and the Family/Patient choice automatically. 24.3 closes and reopens it on a patient change; the new version refreshes in place.
  - It remembers "Show detailed view" per user.
- Columns come from **Display Fields, Account Module Service Date View**. Defaults match today:
  - Service Date, Trans Date, Patient, Reference, Charge, Production, Prov, InsBal, AcctBal
  - Optional extra: Clinic
- Print and Save to Images use the chosen columns.

**Rules.**

- Column widths and order are per Display Fields rules (practice-wide or per user).
- Totals rows stay bold.
- Tooth-number nomenclature conversion stays in the UI, not the business query: the query is shared with the API path in Open Dental.

**Permissions/audit:** unchanged (Account module access).

**Preferences:** a per-user "detailed view" memory in `userodpref` (fork).

**Schema:** none (enum value 22).

**Touchpoints:**

- `OpenDental/Forms Reports/FormRpServiceDateView.cs`, `OpenDentBusiness/Reports/RpServiceDateView.cs`
- `OpenDental/Main Modules/ControlAccount.cs` (`butServiceDateView_Click`, line 220)
- `OpenDentBusiness/TableTypes/DisplayField.cs` (enum, line 62), `OpenDentBusiness/Data Interface/DisplayFields.cs`
- `OpenDental/Forms/FormDisplayFields.cs`, `OpenDental/Forms/FormDisplayFieldCategories.cs`

### v25_4-account-incometransfer-limit: Limit manual income transfers to the amount of patient payments

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html (new preference named "Limit manual income transfers to the amount of patient payments"); baseline https://www.opendental.com/manual243/incometransfer.html.

**Behavior.**

- With the preference on, a **manual** income transfer can move only money that came from patient payments.
- It can't take more from a payment than that payment still holds.
- It can't pull insurance income or adjustments into patient credit.
- The Income Transfer Manager's automatic Transfer / Transfer FIFO tools are unaffected (**VERIFY**).

**UI.**

- Preferences, Account, Payments: a checkbox with the exact label above.
- In the Payment window in income-transfer mode, a blocking message when a split breaks the rule. The message names the source and the available amount.

**Rules (fork interpretation, VERIFY).**

1. Sum the negative (taking) splits per source paysplit. That sum may not exceed the source's remaining unallocated patient amount: original payment split minus amounts already transferred away.
2. Sources must be real patient payments, not other transfers' positive legs, unless that leg is itself unallocated.
3. The total of positive splits equals the total of negative splits, as today (zero-sum rule unchanged).
4. Default **off**, so 24.3 behavior is unchanged.

**Permissions/audit:**

- PaymentCreate, as today.
- Changing the preference logs under Setup.
- A blocked transfer is not logged.

**Preferences:** `IncomeTransfersLimitToPatPayments` bool, default false (**name VERIFY**).

**Schema:** none.

**Touchpoints:**

- `OpenDental/Forms/FormPayment.cs` (income-transfer validation around line 2918)
- `OpenDentBusiness/Data Interface/PaySplits.cs`
- `OpenDental/User Controls/Preferences/UserControlAccountPayments.cs` (+ designer)
- `OpenDentBusiness/TableTypes/Pref.cs`

### v25_4-account-recurringcharge-fail-email: Automatic email when a recurring charge fails

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://opendental.blog/version-25-4/; baseline https://www.opendental.com/manual243/recurringcharges.html (a declined charge posts a $0 payment with the decline note).

**Behavior.**

- When the office turns this on, each declined or failed recurring charge sends an email to the patient (or guarantor) asking them to update their card or contact the office.
- It works for manual runs (Credit Card Recurring Charges window) and automated runs.

**UI.**

- Preferences, Account, Recurring and Repeating Charges:
  - **Email patients when a recurring charge fails** checkbox
  - **Edit Template** button opening subject/body with replacement tags:
    - [NameF], [NamePref], [Amount], [CardLast4], [DateCharge]
    - [OfficeName], [OfficePhone], [ClinicName]
- The recurring charges results summary adds an "Emails sent / not sent" count.

**Rules.**

- Send from the clinic's (or practice's) default email address, through that address's own SMTP. **No Open Dental hosted email.**
- Recipient: the card's patient. If they have no email, use the guarantor's.
- Skip when there is no address, or the patient opted out of email (`commoptout`).
- One email per failed charge per run.
- Don't resend on the following daily retries for the same charge and card. **Fork decision:** a 7-day quiet period, tracked through the existing commlog/emailmessage history.
- Save an `emailmessage` row and a commlog on the patient (as other automated emails do).
- A send failure doesn't stop the batch; it is counted and logged.
- Also covers charges declined by Global Payments V2, PayConnect, PaySimple, XCharge and V1.

**Permissions/audit:**

- Running recurring charges needs the existing permissions.
- Emails log as EmailSend with LogSource of the runner.

**Preferences** (**name VERIFY**):

- `RecurringChargesFailEmailEnabled` bool, default false
- `RecurringChargesFailEmailSubject` string
- `RecurringChargesFailEmailBody` string
- Optionally per clinic through `clinicpref`

**Schema:** none.

**Touchpoints:**

- `OpenDentBusiness/Data Interface/RecurringCharges.cs` (`RecurringChargerator`, line 259)
- `OpenDental/Forms/FormCreditRecurringCharges.cs`
- `OpenDental/User Controls/Preferences/UserControlAccountRecAndRepCharges.cs`
- `OpenDentBusiness/Data Interface/EmailMessages.cs`, `EmailAddresses.cs`
- `OpenDentBusiness/TableTypes/Pref.cs`

---

## Canada

### v25_4-canada-cda-codes-2026: 2026 CDA procedure codes

- **Category:** desktop. **Size:** S. **Depends on:** v25_3-canada-cda-codes-2026 (**implement once**).
- **Sources:** https://www.opendental.com/site/version25_4.html (backported to 25.3.41); https://www.opendental.com/site/canadaproccodetools.html.

**Behavior and rules:** see the 25.3 entry. Extra detail from the public page:

- the tool adds only **missing** CDA codes and never updates existing ones
- it needs SecurityAdmin and ProcCodeEdit

In 24.3 the code list is downloaded from Open Dental's website (`FormProcTools.CanadaDownloadProcedureCodes`, line 91). In our fork, replace that with an import from a CDA-licensed file the office supplies. Update the "2025" wording in the checkbox text (line 60).

**Touchpoints:** `OpenDental/Forms/FormProcTools.cs`.

### v25_4-canada-other-proc-codes: Other Procedure Codes window

- **Category:** desktop. **Size:** M. **Depends on:** none. Followed by v26_1-lists-canada-othercodes-password, which adds a password in 26.1, so 25.4 has none.
- **Sources:** https://www.opendental.com/site/version25_4.html (CDHA, DAC, RAMQ codes); https://www.opendental.com/site/canadaproccodetools.html (Other Codes section); https://www.opendental.com/site/canada.html.

**Behavior.** Procedure Code Tools (Canada) gets an **Other Codes** button. It opens a window that imports other Canadian code systems into the procedure code list.

**UI.**

- Code system list: CDHA; DAC (English); DAC (French); RAMQ; CDA (only used with Advanced Options). ACDQ is not built; it needs member verification by Open Dental.
- **Advanced Options** checkboxes, applied to existing codes of the chosen system:
  - Update descriptions
  - Update abbreviations
  - Update treatment area
  - Update paint type
- OK runs the import. A summary shows added, skipped and updated counts.

**Rules.**

- Each system is imported into a new procedure-code category definition named after the system.
- Codes that already exist are skipped unless an Advanced Option asks to update a field.
- **Fork decision:** Open Dental ships these lists from its own servers. We import from an office-supplied file, because the code sets are owned by their associations. Use a CSV with columns: code, description, abbreviation, treatment area, paint type, time units.
- Codes are never deleted.

**Permissions/audit:** SecurityAdmin and ProcCodeEdit. A ProcCodeEdit log entry with the system name and counts.

**Schema:** none.

**Touchpoints:**

- `OpenDental/Forms/FormProcTools.cs` (+ `FormProcTools.designer.cs`), new `FormProcCodesOther`
- `OpenDentBusiness/Data Interface/ProcedureCodes.cs`, `OpenDentBusiness/Data Interface/Defs.cs` (DefCat.ProcCodeCats)
- `OpenDental/Forms/FormProcCodes2.cs` (`ImportProcCodes`)

### v25_4-canada-codes-cdha: CDHA code system

- **Category:** desktop. **Size:** S. **Depends on:** v25_4-canada-other-proc-codes.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/canada.html (independent hygienists; CDHAnet).

**Behavior:** import the Canadian Dental Hygienists Association code set (from an office-supplied file) for independent hygiene practices. Our code already sends hygienist and denturist claims through Claimstream, and redirects them between the Instream and TELUS networks (`OpenDentBusiness/Data Interface/CanadianNetworks.cs`, `GetNetwork`, lines 84-96). **Rules:** as in the parent item. **Schema:** none.

### v25_4-canada-codes-dac: DAC code system

- **Category:** desktop. **Size:** S. **Depends on:** v25_4-canada-other-proc-codes.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/dac.html.

**Behavior:** import the Denturist Association of Canada code set, in English or French, for denturist practices. **Rules:** English and French are separate entries. Importing one after the other only adds missing codes, so descriptions keep the first language unless "Update descriptions" is ticked. **Schema:** none.

### v25_4-canada-codes-ramq: RAMQ code system

- **Category:** desktop. **Size:** S. **Depends on:** v25_4-canada-other-proc-codes.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/canadaproccodetools.html (RAMQ codes get an "R" appended, which is not sent on claims); https://www.opendental.com/site/canada.html (RAMQ clearinghouse not available).

**Rules.**

- Imported RAMQ codes are stored with a trailing **R** to avoid clashing with CDA codes.
- Claim output (printed and CDAnet) strips that final R.
- **Touchpoint:** the procedure-code output in `OpenDentBusiness/Eclaims/Canadian.cs` and the claim-form fill. Strip only when the code is in the RAMQ category.
- No RAMQ e-claim support.

### v25_4-canada-labfee-pay: Pay Labs when receiving claims

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/canadalabfees.html (Receiving Claims: a Pay Labs button moves payment onto lab procedures when the carrier paid the full amount against the main procedure).

**Behavior.** In the claim payment entry window, **Pay Labs** distributes the insurance payment already typed on each procedure onto that procedure's attached lab fee lines. The public page says only that it is for carriers that pay the whole amount against the main procedure, and that the lab amounts are filled in on the lab lines at once. Each procedure can have at most two labs (same page).

**UI:** a **Pay Labs** button in `FormClaimPayTotal`. Fork: show it only for Canadian claims that have lab procedures (the window already shows a Labs column, line 117).

**Rules (fork interpretation; the exact split is not public, VERIFY).**

1. For each procedure row with attached labs (up to two), sum its InsPayAmt.
2. Move to each lab row the smaller of its fee billed and what is left, subtracting it from the parent row.
3. Never go negative; leave the parent at 0 if the payment was smaller than the labs.
4. Recompute totals and leave the user on the window to review. Undo is Cancel.
5. Don't touch write-offs or deductibles.

**Permissions/audit:** existing InsPayCreate/ClaimProcReceivedEdit. **Schema:** none.

**Touchpoints:** `OpenDental/Forms/FormClaimPayTotal.cs` (+ designer), `OpenDentBusiness/Data Interface/Procedures.cs` (`GetCanadianLabFees`).

---

## Appointments module

### v25_4-appts-insverify-daterange: Insurance Verification List searches an appointment date range

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html
  - https://opendental.blog/version-25-4/ (the filters now take a date range and several carriers)
  - https://www.opendental.com/site/0_insverifylist.html (current Quick Manual: patients are filtered by a **minimum and a maximum** number of days until the appointment)
  - Baseline: https://www.opendental.com/manual243/insverifylist.html
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`DateLastAssigned` now datetime)

**Behavior.**

- 24.3 has one "Days until scheduled appointment" number N and lists appointments from today through today+N.
- 25.4 adds a lower bound. The user enters a **minimum** and a **maximum** number of days until the appointment, for example 7 to 14 days out.
- The public Quick Manual describes the range in days relative to today, not as calendar dates. Corrected on fact-check: the earlier draft of this item used From/To date pickers.

**UI.**

- In the filter area of `FormInsVerificationList`, each "Days until scheduled appointment" box (Standard and Medicaid) becomes a **Min** and **Max** pair of day counts. Exact labels and layout: **VERIFY**.
- Defaults: Min 0, Max = the existing Insurance Verification Setup day count (`InsVerifyAppointmentScheduledDays`, `...Medicaid`). So the default list matches 24.3.
- Whether Insurance Verification Setup gained its own default for Min is not public (**VERIFY**). **Fork decision:** no new setup preference. Min starts at 0 each time the window opens.
- The other day filters (benefits not verified in N days, eligibility not verified in N days) are unchanged.
- The Past Due tab keeps its own fixed window (`InsVerifyDaysFromPastDueAppt`, `InsVerifyDaysFromPastDueApptMedicaid`).

**Rules.**

- Both values are whole numbers, 0 or more, and Min may not exceed Max. Invalid input blocks the refresh, as the 24.3 `IsValid()` checks do.
- The query (`InsVerifies.GetVerifyGridList`) already takes start and end dates. Pass today+Min and today+Max (24.3 passes today and today+N; `FormInsVerificationList.cs` lines 320-323).
- **Assignment time:** `insverify.DateLastAssigned` and `insverifyhist.DateLastAssigned` become datetime. Store `DateTime.Now` wherever 24.3 stores `DateTime.Today`:
  - `FormInsVerificationList.cs`: assign, unassign and status-change paths (lines 715, 721, 906, 915, 927, 935, 959, 964)
  - `InsVerifies.cs` line 670
  - Resets to MinValue stay as they are (`PatPlans.cs` line 461 on patplan drop; `InsVerifies.cs` line 456). MinValue maps to `0001-01-01 00:00:00`.
  - The schema doc lists the events that touch this column: assign or unassign, status change, a dropped patplan, and a batch-verification error.
- The 30-day auto-unassign check compares datetimes (`InsVerifies.cs` lines 484-496).
- The grid shows date and time where "assigned" is displayed.

**Permissions/audit:** unchanged (InsuranceVerification).

**Preferences:** none new (fork; see above).

**Schema:** `insverify.DateLastAssigned` and `insverifyhist.DateLastAssigned` to `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` (MODIFY keeps data: midnight times). This column is in the `SecurityHash` input for insverify rows (25.1 integrity hash). Recompute the hash format so it uses the date part only, or rehash. Coordinate with v25_1-security-insverify-securityhash.

**Touchpoints:**

- `OpenDental/Forms/FormInsVerificationList.cs` (lines 50-75, 309-326, and the `DateLastAssigned` writes listed above) + designer
- `OpenDental/Forms/FormInsVerificationSetup.cs` (only if a Min default is added later)
- `OpenDentBusiness/Data Interface/InsVerifies.cs` (line 246 `GetVerifyGridList`, 456-496, 670)
- `OpenDentBusiness/Data Interface/InsVerifyHists.cs`, `OpenDentBusiness/Data Interface/PatPlans.cs`
- `OpenDentBusiness/TableTypes/InsVerify.cs`, `InsVerifyHist.cs`

### v25_4-appts-insverify-carriers: Insurance Verification List, one or more carriers

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://opendental.blog/version-25-4/.

**Behavior.** The Carrier filter changes from a free-text name box (24.3 `textVerifyCarrier`) to a selection of one or more carriers.

**UI** (fork layout): a read-only box showing "All" or the chosen names (or "N carriers"), with a [...] button that opens the carrier list in multi-select mode, and a Clear button. `FormCarriers` today has only a single-pick `IsSelectMode` (line 21), so add a multi-select mode that returns a list of `CarrierNum`s.

**Rules.**

- An empty selection means all carriers.
- Filter on `carrier.CarrierNum IN (...)`.
- Carriers combined later (Carrier Combine) drop out of the selection silently.
- Optionally remember the selection per user (`userodpref`, fork).

**Schema:** none.

**Touchpoints:** `OpenDental/Forms/FormInsVerificationList.cs`, `OpenDentBusiness/Data Interface/InsVerifies.cs`, `OpenDental/Forms/FormCarriers.cs`.

### v25_4-appts-blockout-permissions: Blockouts permission split into Add, Edit and Delete

- **Category:** desktop. **Size:** M. **Depends on:** v25_1-appt-blockouts-flagged-permission (`BlockoutsFlagged`=266 handles flagged blockouts).
- **Sources:**
  - https://www.opendental.com/site/version25_4.html, https://opendental.blog/version-25-4/
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`EnumPermType`: BlockoutEdit=13, BlockoutAdd=272, BlockoutDelete=273)
  - Baseline: https://www.opendental.com/manual243/permissions.html

**Behavior.** For blockouts with **no** flag:

| Permission | Value | Covers | BlockoutAction values |
|---|---|---|---|
| **Blockout Add** | 272 | add, paste, copy | Create, Paste, Copy |
| **Blockout Edit** | 13 (renamed from Blockouts) | edit | Edit |
| **Blockout Delete** | 273 | delete, cut, clear (day, operatory, clinic) | Delete, Cut, Clear |

Flagged blockouts (NS or DC) stay under BlockoutsFlagged.

**UI.**

- The Security tree shows the three under Appointments, next to Blockouts Flagged.
- In the Appointments module's right-click menu (`ControlAppt`), enable each item by its own permission:
  - Add, Paste, Copy: Add
  - Edit: Edit
  - Delete, Cut, Clear for Day/Op/Clinic: Delete
  - "Cut-Copy-Paste" tool: see the rules below

**Rules.**

- The Blockout Cut-Copy-Paste tool (`FormBlockoutCutCopyPaste`, `Schedules.CopyBlockouts`):
  - copy and paste need Add
  - its "replace existing" option also needs Delete
  - cut needs Delete and Add
- Moving or resizing a blockout by dragging counts as Edit.
- Changing a blockout's type between flagged and unflagged needs BlockoutsFlagged plus the relevant split permission.
- Deleting the Web Sched ASAP blockout (24.3 used Blockouts) needs Delete. Web Sched is cut in our fork, so this applies only while that menu item still exists for adopted data.
- **Conversion (fork decision; Open Dental's own conversion is not public, VERIFY on a 25.4 copy):** every user group that had Blockouts (13) keeps BlockoutEdit (same value) and also gets BlockoutAdd and BlockoutDelete, with the same date restriction settings. Behavior is unchanged until an admin narrows it.
- The 25.4 schema text for each value confirms the split: 13 covers editing unflagged blockouts, 272 adding, pasting and copying them, 273 deleting, cutting and clearing them.

**Audit:** `Schedules.BlockoutLogHelper` and the cut/copy/paste log write under the matching permission instead of the single Blockouts value (`Schedules.cs` lines 252, 326).

**Schema:** none (enum values; grouppermission rows inserted by conversion).

**Touchpoints:**

- `OpenDentBusiness/TableTypes/GroupPermission.cs` (line 79)
- `OpenDental/Main Modules/ControlAppt.cs` (lines 312-400, 2320-2480)
- `OpenDental/Forms/FormBlockoutCutCopyPaste.cs`, `OpenDental/Forms/FormScheduleBlockEdit.cs`
- `OpenDentBusiness/Data Interface/Schedules.cs` (lines 162-330)
- `OpenDentBusiness/Db/Enumerations.cs` (`BlockoutAction`, line 536)
- `OpenDental/User Controls/UserControlSecurityTree.cs` (line 299)
- `OpenDentBusiness/Data Interface/GroupPermissions.cs`
- a ConvertDatabases step

---

## Chart module

### v25_4-chart-perio-direction: Perio Chart direction radio buttons improved

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; baseline https://www.opendental.com/manual243/perio.html ("Current Direction" shows whether auto-advance is going forward or in reverse and can be toggled).

**Behavior.** The release says only that the Direction radio buttons work better. Exact changes: **VERIFY**.

**Fork design.**

- Clicking a direction applies at once to the next keystroke from the current cell; the user doesn't have to click back into the chart.
- The Forward/Reverse label updates immediately.
- A manual choice holds while the user keeps entering in the same row section. It is overridden only when auto-advance crosses into a new section, where the advance sequence (Maxillary First / Facials First) sets it.
- The arrow keys never flip the direction.

**Rules:** voice perio keeps its own direction updates (`FormPerio.SetCurrentDirection`).

**Touchpoints:** `OpenDental/Forms/FormPerio.cs` (`radioRight_Click`/`radioLeft_Click` lines 1505-1516, `gridP_DirectionChanged*` lines 1461-1467, lines 1780-1845), `OpenDental/User Controls/ContrPerio.cs` (`EnumCurrentDirection_`). **Schema:** none.

### v25_4-chart-groupnote-size: Larger Notes box in Group Note

- **Category:** desktop. **Size:** S. **Depends on:** v25_1-chart-group-note-flexible.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://opendental.blog/version-25-4/ (size and layout of the Group Note window updated); baseline https://www.opendental.com/manual243/procgroupnote.html.

**Behavior.** The Group Note window is resizable and larger by default. The Notes box takes most of the extra space.

**Rules.**

- Anchor the Notes box to grow with the window.
- Keep the procedures grid at its current height, with a scroll bar.
- Keep the signature box visible at the bottom.
- Remember the window size per user (fork).
- 26.1 later adds a second signature box (see the 26.1 spec).

**Touchpoints:** `OpenDental/Forms/FormProcGroup.cs`, `OpenDental/Forms/FormProcGroup.designer.cs`. **Schema:** none.

### v25_4-chart-procnote-merge: Procedure Note Merge window

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html (a merge window in Procedure Info resolves concurrency problems when saving notes); baseline https://www.opendental.com/manual243/procedureedit.html.

**Behavior.** If, while a user has Procedure Info open, someone else saves a new note on the same procedure, the save shows a **Procedure Note Merge** window instead of silently writing a newer note over theirs.

**UI.**

- Three panes:
  - **Their note**: read-only, with user and time
  - **Your note**: read-only
  - **Merged note**: editable, pre-filled with their note, a separator line, then your added text
- Buttons:
  - **Keep Mine** (overwrites)
  - **Keep Theirs** (discards my note change, keeps my other edits)
  - **Save Merged**
  - **Cancel** (back to Procedure Info)

**Rules.**

- On load, remember the `ProcNoteNum` and `EntryDateTime` of the newest `procnote` row for the procedure.
- At save, re-read the newest row. Merging is needed only if it changed **and** this user changed the note text.
- Saving inserts a new `procnote` row, as today (notes are versioned).
- A signed note: any merge result different from the signed text clears the signature, and the user must re-sign. The existing "signature cleared" alert rules apply (`FormProcEdit.butSave_Click`, around line 2548).
- Group notes: same check in `FormProcGroup`.

**Permissions/audit:**

- Existing ProcEdit / ProcCompleteNote permissions.
- Log "note merged with changes by <user>" under the same permission used for the note save.

**Schema:** none.

**Touchpoints:** `OpenDental/Forms/FormProcEdit.cs` (lines 2539-2590), `OpenDentBusiness/Data Interface/ProcNotes.cs`, `OpenDentBusiness/TableTypes/ProcNote.cs`, `OpenDental/Forms/FormProcGroup.cs`, new `FrmProcNoteMerge`.

### v25_4-chart-nobillins-audit: Per-procedure Do Not Bill Insurance, with audit trail

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html (Audit Trail tracks changes to the Do Not Bill Insurance flag); https://www.opendental.com/OpenDentalDocumentation25-4.xml (`procedurelog.NoBillIns`, `claimproc.NoBillIns` summary).

**Behavior.**

- In 24.3, "Do Not Bill to Ins" in Procedure Info only sets `claimproc.NoBillIns` on the procedure's current claimprocs. That decision is lost when claimprocs are rebuilt, for example after an insurance plan change.
- 25.4 stores the user's decision on the procedure itself (`procedurelog.NoBillIns`). Claimprocs inherit it when created or regenerated.
- Every change of the flag is written to the Audit Trail.

**UI:** no new control. The existing Procedure Info checkbox now sets both the claimprocs and the procedure flag.

**Rules (from the schema doc).**

1. `procedurelog.NoBillIns` becomes true **only** when the user deliberately checks the box in Procedure Info.
2. When claimprocs are created or rebuilt, `claimproc.NoBillIns` = procedure flag OR the existing default from the procedure code / insurance plan preference (`InsPlanPreferences.NoBillIns`, `ClaimProcs.cs` line 1178).
3. If a user edits any attached claimproc so that it is billed to insurance (unchecks NoBillIns in `FormClaimProc`), clear the procedure flag.
4. **Migration for 24.3/25.3 data (fork decision):** set `procedurelog.NoBillIns=1` where all estimate claimprocs of a non-complete or complete-unbilled procedure have NoBillIns=1 while the code and plan defaults would give 0. Otherwise 0.

**Permissions/audit.**

- The change is gated as today: ProcCompleteEditMisc for completed procedures, else ProcEdit.
- Write a securitylog entry under that permission: "<code> <date>: Do Not Bill to Ins changed from <old> to <new>".
- Also log automatic clears (rule 3) with "(cleared because a claimproc was set to bill insurance)".

**Schema:** `procedurelog.NoBillIns tinyint(4) NOT NULL` (0).

**Touchpoints:**

- `OpenDental/Forms/FormProcEdit.cs` (lines 1007-1123, `checkNoBillIns_Click` line 1815)
- `OpenDentBusiness/Data Interface/ClaimProcs.cs` (lines 1178, 1931-1933, 2234)
- `OpenDentBusiness/Data Interface/Procedures.cs` (line 2523)
- `OpenDentBusiness/TableTypes/Procedure.cs`
- `OpenDental/Forms/FormClaimProc.cs`
- `OpenDentBusiness/Data Interface/SecurityLogs.cs`

---

## Family module

### v25_4-family-benefit-tooth-exclusions: Teeth for exclusions from the Age Limits and Other Benefits grids

- **Category:** desktop. **Size:** M. **Depends on:** v25_3-family-exclusions-tooth (storage, parsing and estimate matching; implement once). Followed by v26_1-family-benefit-agelimit-teeth.
- **Sources:** https://www.opendental.com/site/version25_4.html (backported to 25.3.39); https://opendental.blog/version-25-4/ (tooth numbers can now be typed in either grid, but only on exclusion rows); https://www.opendental.com/site/0_insbenefits.html.

**Behavior.** Besides the Edit Benefit window (25.3 item), tooth numbers for **exclusion** benefits can be typed directly in the Edit Benefits grids.

**UI.**

- `FormInsBenefits`:
  - Other Benefits grid (`FillGridBenefits`, line 630): a **Teeth** column, editable in place on Exclusions rows. Other rows show it blank and read-only.
  - Age Limits grid (`FillGridAgeLimits`, line 482): a **Teeth** column. In 25.4 it applies only when the code group row is used for an exclusion; 26.1 extends it to age limits. **VERIFY** exact 25.4 grid behavior.
- Invalid entries turn the cell red and block Save with a message.

**Rules.** Parsing, storage (`benefit.ToothRange`, internal numbering, ranges expanded) and matching are as in the 25.3 item. Blank means all teeth.

**Audit:** the benefit-change log includes the teeth. **Schema:** `benefit.ToothRange` (25.3).

**Touchpoints:** `OpenDental/Forms/FormInsBenefits.cs` (+ designer), `OpenDentBusiness/Logic/Tooth.cs`, `OpenDentBusiness/Data Interface/Benefits.cs`.

### v25_4-family-writeoff-override-wording: Write-Offs for Non-Covered Services Override wording

- **Category:** desktop. **Size:** S. **Depends on:** v25_4-misc-ucr-terminology.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`insplan.ExclusionFeeRule`, `InsPlansZeroWriteOffsOnAnnualMaxOverride`, `InsPlansZeroWriteOffsOnFreqOrAgingOverride` descriptions)
  - https://www.opendental.com/site/canadainsplan.html ("Use Standard Provider Fee" option name)
  - Baseline: https://www.opendental.com/manual243/insplan.html

**Behavior.** Clearer labels and help text in Edit Insurance Plan, Other Ins Info, "Write-Offs for Non-Covered Services Overrides". Values and logic are unchanged.

**UI text (ours).**

- **Exclusions** combo: Practice Default / Do Nothing / **Use Standard Provider Fee**. Tooltip:
  - Do Nothing applies the plan's normal fee and write-off.
  - Use Standard Provider Fee bills the full standard fee with no write-off.
- **Annual Max** and **Age or Frequency Limit** combos: Default / Yes / No. Tooltip:
  - Yes bills the full standard provider fee with no write-off once the limit is hit.
  - No keeps the normal fee and write-off.
  - Default follows the matching preference.

**Rules:** enum values unchanged (`ExclusionRule` 0/1/2; YN 0/1/2). Rename only the `Description` attribute of `ExclusionRule.UseUcrFee`; keep the member name for compatibility.

**Touchpoints:** `OpenDental/Forms/FormInsPlan.cs`, `OpenDental/Forms/FormInsPlan.designer.cs` (group box near line 1591), `OpenDentBusiness/TableTypes/InsPlan.cs` (line 161). **Schema:** none.

### v25_4-family-clerri: Clerri membership-plan program link

- **Category:** vendor-direct. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/bridgeclerri.html; https://www.opendental.com/site/clerri.html; https://opendental.blog/version-25-4/.

**Behavior.**

- Clerri runs in-house dental membership plans. The program link adds a Family module toolbar button that opens the Clerri dashboard for the office (and patient), including potential savings and Clerri's huddle report.
- In Open Dental, Clerri also syncs data **through Open Dental's eConnector/API**. That path is cut in our fork. Only the launch is built. Data sync would need a direct agreement with Clerri.

**UI.**

- **Program Links, Clerri** (fields per bridgeclerri.html):
  - Clinic: shown only when Clinics is on; picks the clinic the Client Key belongs to
  - Enabled
  - Show Toolbar Button: unchecking hides the Family module button
  - Internal Name and Description, read-only
  - Client Key, from Clerri
- Open Dental also shows an eConnector Service Status line. Its page says the eConnector must be installed and running for the Clerri bridge to work at all. We omit that line (the eConnector is cut).
- Family module toolbar button "Clerri", shown only when enabled (neutrality rule).

**Rules.**

- The URL format and parameters come from Clerri's integration documentation (**VERIFY with vendor**). Open the default browser with the office's client key and, when a patient is selected, the patient's identifier (patient context is our assumption; the public pages mention only the dashboard). Send no other PHI in the URL.
- Per the public page, Show Toolbar Button can't be changed while the bridge is enabled. Mirror that.
- **Classification check:** this stays vendor-direct, but with low value. In Open Dental the bridge depends on the eConnector, and the public Clerri page says data is synced between the two systems. Without that sync, Clerri's dashboard has no practice data from us. Build the launch only after Clerri confirms it is useful without the sync, or offers a direct (non-Open Dental) data path.

**Permissions/audit:** Setup for the link. Log launches with PatNum under a generic program-link log (fork).

**Preferences:** program properties only. **Schema:** none.

**Touchpoints:** `OpenDentBusiness/TableTypes/Program.cs`, `OpenDental/Forms/FormProgramLinkEdit.cs`, `WpfControlsOD/Logic/ProgramL.cs`, `OpenDental/Main Modules/ControlFamily.cs` (toolbar), new `WpfControlsOD/Bridges/Clerri.cs`.

---

## Miscellaneous

### v25_4-tools-dbm-base64-documents: DBM tool "Create Documents From Base64 Notes"

- **Category:** desktop. **Size:** S. **Depends on:** v25_3-tools-dbm-base64-docs (**implement once**). This entry resolves that item's VERIFY on which field is scanned.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (backported to 25.3.39)
  - https://www.opendental.com/site/apidocuments.html: an API upload stores the file in `document.Note` as the prefix `_rawBase64_:`, then the extension, an underscore and the base64 data. The real file is created and the note cleared only when a user opens that document.

**Behavior.** A DBM Tools-tab entry converts **all** such pending documents at once, so they don't wait for someone to click each one. This matters before a migration or image-folder move.

**Rules.** For each `document` whose `Note` starts with `_rawBase64_:`:

- decode the bytes
- import them into the patient's image folder with the stored extension (same path as `ImageStore.Import`)
- copy Description, DocCategory, ImgType, DateCreated, ToothNumbers, ProvNum and PrintHeading to the new row
- delete the placeholder row

This is the same steps `ControlImageDisplay.DownloadDocumentNoteFile` does today (`OpenDental/User Controls/ControlImageDisplay.cs`, lines 3920-3975).

- Show a count first and ask to confirm.
- Skip and list rows that fail to decode.
- Leave `_download_:` URL notes alone: those would fetch from the internet.
- Works for AtoZ folder and in-database image storage.

**Permissions/audit:** DBM access as today (Setup). One ImageCreate log per document created.

**Touchpoints:** `OpenDental/Forms/FormDatabaseMaintenance.cs` (`FillGridTools`, line 170), `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`, `OpenDentBusiness/Imaging/ImageStore.cs`. **Schema:** none.

### v25_4-misc-diagnostics-info: More details in the Diagnostics window

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html (computer hardware, current date/time, other details); baseline https://www.opendental.com/manual243/diagnostics.html (Help, About, Diagnostics; Copy All and Print).

**Behavior.** The Diagnostics text adds:

- **current date/time** on the workstation and on the database server (`NOW()`), with the difference between them
- **computer hardware**: CPU model and cores, RAM total/free, OS version and build, 32/64-bit, screen count and resolution/DPI, free disk on the program drive and on the image folder drive
- program path, .NET runtime version, MySQL/MariaDB server version, time zone

**Rules.**

- Local only; nothing is sent anywhere.
- Never include passwords.
- Drop the HQ connection block (`PrefC.IsODHQ` branch).
- Gather hardware through WMI with try/catch per line, as the current code does.

**Touchpoints:** `OpenDental/Forms/FormAbout.cs` (`butDiagnostics_Click`, line 51), `OpenDentBusiness/Data Interface/BugSubmissions.cs` (`GetDiagnostics`, line 188). Move it to a non-HQ helper when BugSubmissions is removed. **Schema:** none.

### v25_4-misc-print-window: Minor Print window enhancements

- **Category:** desktop. **Size:** S. **Depends on:** v25_3-misc-printer-options.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (various minor enhancements)
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`printer` table, clarified)
  - Baseline: https://www.opendental.com/manual243/printersetup.html

**Behavior.** The specific enhancements are not public (**VERIFY**). The 25.4 schema documentation clarifies printer rules that the Print window must follow:

- `PrintSituation.Default` means the program's own default printer situation, not Windows' default printer.
- An empty `PrinterName`, or no row at all, means "use the Windows default, or the program default".
- A row must exist when **Prompt** (`DisplayPrompt`) is on, even with no printer chosen.

**Fork design for the Print window (25.3 item):**

- shows which rule picked the printer, for example "From Statements setting" or "Windows default"
- remembers copies per situation for the session
- keeps the page range when switching printers
- shows the printer's online/offline state where Windows reports it

**Touchpoints:** `OpenDental/Logic/PrinterL.cs`, `WpfControlsOD/Logic/PrinterL.cs`, `OpenDentBusiness/Data Interface/Printers.cs`, `OpenDentBusiness/TableTypes/Printer.cs`, `OpenDental/Forms/FormPrinterSetup.cs`, `OpenDental/Forms/FormPrinterEdit.cs`. **Schema:** none.

### v25_4-reports-udsplus: UDS+ Report for FQHC Dental Services

- **Category:** desktop. **Size:** L. **Depends on:** none.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html (new UDS+ report for FQHC dental services in Standard Reports)
  - https://www.opendental.com/site/0_reports.html and https://www.opendental.com/site/0_showfeatures.html (Public Health reports)
  - Baseline: https://www.opendental.com/manual243/reportsuds.html (existing FQHC Dental Sealant Measure)
  - HRSA UDS Table 6A guidance: https://bphc.hrsa.gov/sites/default/files/bphc/data-reporting/table-6a-fact-sheet-2024.pdf

**Behavior.**

- For Federally Qualified Health Centers, a Standard Reports entry produces the dental-services data that HRSA's UDS asks for. Placing it in the Public Health section, next to the existing UDS (sealant) report, is our choice; the release note says only "Standard Reports".
- **UDS+** is HRSA's patient-level submission.
- Open Dental's exact output is not public (**VERIFY**).

**Fork design.**

- **Inputs:** reporting year (or From/To), clinics, providers.
- **Output 1, aggregate:** the UDS dental service lines (Table 6A dental rows). For each line, the number of **visits** and the number of distinct **patients** with at least one qualifying completed procedure.
  - A visit is a patient and date.
  - A service counts once per visit, even when performed several times.
- **Output 2, patient-level CSV** for UDS+ preparation: one row per patient-visit-service line.
  - Columns: date of birth, sex, ZIP, race, ethnicity, preferred language, insurance type, visit date, CDT codes, UDS line.
  - It is a local file for the center's own UDS+ tooling. We build no FHIR submission client.
- **Code-to-line mapping:** HRSA updates it yearly, so store it as an editable list in the report's setup (**fork:** a JSON preference `UdsPlusDentalLineCodes`). Ship an empty default with instructions. No CDT text is shipped, per the decoupling plan.

**Rules.**

- Completed procedures only, by `ProcDate` in range.
- Exclude deleted procedures and procedures of hidden or test patients.

**Permissions/audit:** Reports plus Report Setup security. Running the report logs a Reports entry.

**Touchpoints:** `OpenDental/Forms/FormReportsMore.cs` (Public Health list; `listUDSReports` stub at line 79), `OpenDentBusiness/Data Interface/DisplayReports.cs`, new `OpenDentBusiness/Reports/RpUdsPlus.cs` and `OpenDental/Forms Reports/FormRpUdsPlus.cs`. Model on `FormRpDentalSealantMeasure.cs`. **Schema:** none.

### v25_4-claims-dentical-dc217: Denti-Cal DC217_2019 claim form

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html
  - Medi-Cal Dental (Denti-Cal) TAR/Claim form DC-217, the laser-printer version, printed onto preprinted stock. Field layout and instructions: Provider Handbook section 6, https://www.dental.dhcs.ca.gov/MCD_documents/providers/provider_handbook/PHB_section_06_forms.pdf
  - Baseline: https://www.opendental.com/manual243/dentical.html (older Denti-Cal form imported from a download) and https://www.opendental.com/manual243/claimforms.html

**Behavior.** A built-in claim form "Denti-Cal DC217_2019" that prints claim data in the positions of the DC-217 form (2019 revision) on preprinted stock.

**Rules.**

- No background image: the stock is preprinted, so there is no artwork concern.
- Item positions come from measuring the official form. Calibrate on real stock.
- Map fields using existing claim-form field names: patient, subscriber and Medi-Cal ID, billing/rendering provider NPI, procedures with tooth, surface, quad, code and fee, TAR fields, signatures and date.
- Up to the form's procedure line count per page; further lines continue on a new form.
- Installed by a ConvertDatabases step, like other built-in forms. Selectable as an insurance plan's claim form.

**Touchpoints:** new `OpenDentBusiness/Resources/ClaimForms/ClaimFormDenti-Cal DC217_2019.xml` (existing `ClaimFormDenti-Cal.xml` as reference), `OpenDentBusiness/Data Interface/ClaimForms.cs`, `OpenDental/Forms/FormClaimForms.cs`, `OpenDental/Forms/FormClaimPrint.cs`, `OpenDentBusiness/Properties/Resources.resx`. **Schema:** none (rows in `claimform`/`claimformitem`).

### v25_4-sheets-nonlatin: Sheets support non-Latin languages

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; baseline https://www.opendental.com/manual243/sheetsetuplanguage.html (translations per patient language).

**Behavior.** Sheets can be translated into, filled in and printed in languages written in non-Latin scripts: Cyrillic, Greek, Arabic, Hebrew, Chinese, Japanese, Korean, Hindi and others. Text displays and prints correctly, and PDFs keep it.

**Rules.**

- **PDF output:** `SheetDrawingJob` creates `XFont` without Unicode options at 14 call sites (lines 247, 561, 579, 596, 609, 623, 689, 716, 719, 1000, 1106, 1132, 1136, 1477), so non-Latin text breaks in PDFs. Route them all through one helper. Use `XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.Always)`, as `ImageHelper.cs` line 691 and `GridOD.cs` line 3818 already do.
- **Font fallback:** when the sheet's font lacks glyphs for the text, fall back to a font that has them (Segoe UI / Microsoft YaHei / Malgun Gothic / Nirmala UI, by script). Do this on screen, in print and in PDF.
- **Right-to-left** text (Arabic, Hebrew) renders right-to-left within its field; field positions stay as designed.
- The Language list in the Sheet Def editor accepts any culture in `LanguagesUsedByPatients`, including non-Latin ones. Display names come from `CultureInfo`.
- Input fields filled on screen accept and save Unicode (columns are utf8; **VERIFY** utf8mb3 limits for emoji, which are out of scope).

**Touchpoints:** `OpenDentBusiness/SheetFramework/SheetDrawingJob.cs`, `OpenDental/SheetFramework/SheetPrinting.cs`, `OpenDental/Forms/FormSheetDefEdit.cs` (`RefreshLanguages`, line 2668), `OpenDental/Forms/FormSheetFillEdit.cs`. **Schema:** none.

### v25_4-eforms-patfields: eForms support custom patient field definitions

- **Category:** desktop. **Size:** M. **Depends on:** v24_4-eforms-desktop-forms.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html, https://opendental.blog/version-25-4/
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml: `eformfield.DbLink` and `eformfielddef.DbLink` may now hold custom PatFields besides the built-in list

**Behavior.** In the eForm designer, a field's database link can point to an office-defined **patient field** (Setup, Patient Field Defs). The form pre-fills from the patient's current value and writes the answer back on import.

**UI:** the DbLink picker (`FrmEFormFieldPicker`, used by the text box, date, checkbox and radio editors) gets a "Patient Fields" group listing `patfielddef` entries of compatible types.

**Rules.**

- **Type mapping:**
  - Text: text box
  - Date: date field
  - Checkbox: checkbox
  - PickList: radio buttons, with picklist items as choices
  - Currency: text box with numeric validation
- **Storage format** of the link: **VERIFY** for data compatibility. Fork: the prefix `PatField:` followed by `patfielddef.FieldName`.
- Renaming a patfielddef must update the links, or warn when they exist.
- Hidden or deleted defs show as "(missing)" and are skipped on import.
- Import creates or updates the `patfield` row for the patient. A blank answer clears it only if the user confirms (same as other fields).

**Permissions/audit:** EFormEdit to design. Import logs PatientFieldEdit per field changed.

**Touchpoints:** `OpenDentBusiness/EForms/EFormFieldsAvailable.cs`, `OpenDentBusiness/EForms/EFormImport.cs`, `OpenDentBusiness/EForms/EFormFiller.cs`, `WpfControlsOD/Frms/FrmEFormFieldPicker.xaml.cs`, `OpenDentBusiness/Data Interface/PatFieldDefs.cs`, `PatFields.cs`. **Schema:** none.

### v25_4-sheets-duplicate-field-warning: Duplicate field warning in the sheet editor

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_4.html.

**Behavior.** Adding a field (new, paste or copy) whose identity already exists on the sheet def shows a warning with Continue / Cancel.

**Rules.**

- Identity:
  - input and output fields: `FieldName` within the same language and layout mode
  - checkboxes and radio buttons: `FieldName` + `RadioButtonValue`
- Static text, lines, rectangles, images, signature boxes and grids are never checked.
- "misc" input fields are checked by their user-defined name.

**Touchpoints:** `OpenDental/Forms/FormSheetDefEdit.cs`, `OpenDentBusiness/Data Interface/SheetFieldDefs.cs`. **Schema:** none.

### v25_4-eforms-richtext-paste: Rich text paste into eForm labels

- **Category:** desktop. **Size:** M. **Depends on:** v24_4-eforms-desktop-forms.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://opendental.blog/version-25-4/ (bold, italic and the like are kept when pasting from an external source); https://www.opendental.com/OpenDentalDocumentation25-4.xml (`eformfield.ValueLabel` is a WPF FlowDocument).

**Behavior.** Pasting from Word, a browser or email into an eForm label keeps basic formatting instead of becoming plain text.

**Rules.**

- Accept the RTF and HTML clipboard formats and convert to a FlowDocument.
- Keep:
  - bold, italic, underline
  - font size, as relative sizes
  - text color
  - paragraphs, line breaks, bulleted and numbered lists
- Drop:
  - images, tables, hyperlinks (keep their text)
  - external fonts (map to the form font)
  - background colors
- Plain text paste is unchanged.

**Touchpoints:** `WpfControlsOD/Frms/FrmEFormLabelEdit.xaml.cs` (+ .xaml), `WpfControlsOD/Logic/EFormL.cs`. **Schema:** none.

### v25_4-eforms-multi-import: Import several eForms at once

- **Category:** desktop. **Size:** S. **Depends on:** v24_4-eforms-desktop-forms.
- **Source:** https://www.opendental.com/site/version25_4.html.

**Behavior.** In Setup, eForms, Import, the file dialog allows selecting several exported eForm files. Each is imported.

**Rules.**

- Process files in name order.
- A file that fails validation is skipped and listed.
- Name clashes get " (2)" appended.
- At the end, show imported and failed counts.
- Log one EFormEdit entry per form, matching today's single import.

**Touchpoints:** `WpfControlsOD/Frms/FrmEFormDefs.xaml.cs` (`butImport_Click`, line 120). **Schema:** none.

### v25_4-setup-pem-path-label: PEM path label

- **Category:** desktop. **Size:** S. **Depends on:** v25_3-misc-pem-path-label (**implement once**).
- **Sources:** https://www.opendental.com/site/version25_4.html (backported to 25.3.53); baseline https://www.opendental.com/manual243/choosedatabase.html (24.3 label tied the field to MariaDB SkySQL).

**Behavior.**

- The label "(only if using MariaDB SkySQL) PEM path" (`FormChooseDatabase.Designer.cs` line 331, `label5`; `FormReportSetup.designer.cs` line 145, `label8`) becomes vendor-neutral: "SSL CA certificate (.pem) path". Open Dental's exact new wording is not public; this text is ours.
- Rename the text boxes to `textSslCaPem`: it is `textPEM` in `FormChooseDatabase` and `textSkySQL` in `FormReportSetup`.
- Stored values (`FreeDentalConfig.xml` SslCA, `ReportingServerSslCa` preference) are unchanged.

**Schema:** none.

### v25_4-reports-era-autoprocessed-filters: ERAs Automatically Processed report, PreAuth and $0 filters

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; baseline https://www.opendental.com/manual243/reporterasautomaticallyprocess.html.

**Behavior.** Two new filter checkboxes: **Show PreAuths** and **Show $0 Payments**.

**Rules (fork defaults, VERIFY).**

- Both default to checked, so results match 24.3.
- Unchecking PreAuths hides ERAs whose claims are all predeterminations.
- Unchecking $0 Payments hides ERAs whose total payment is zero.
- Remember the choices per user.

**Touchpoints:** `OpenDental/Forms/FormRpEraAutoProcessed.cs`, `OpenDentBusiness/Data Interface/Etrans835s.cs`. **Schema:** none.

### v25_4-bridges-trophy-enhanced-tags: Trophy (Enhanced) bridge replacement tags

- **Category:** vendor-direct (local program launch, no network). **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; baseline https://www.opendental.com/manual243/bridgetrophyenhanced.html.

**Behavior.** The Trophy (Enhanced) program link honors its **Command line** field with the standard patient replacement tags. The built-in `-P<folder> -N<last, first>` arguments are no longer the only option.

**Rules.**

- If the Command line field is blank, behave exactly as 24.3.
- Otherwise run `Patients.ReplacePatient` on it, plus a bridge-specific `[TrophyFolder]` tag (fork) holding the resolved patient folder path, and pass the result as the arguments.
- Folder creation and numbered mode still run first, so `[TrophyFolder]` is valid.

**Touchpoints:** `WpfControlsOD/Bridges/TrophyEnhanced.cs` (lines 60-90), `OpenDentBusiness/Data Interface/Patients.cs` (`ReplacePatient`), `OpenDental/Forms/FormProgramLinkEdit.cs`. **Schema:** none.

### v25_4-bridges-cleverone: Clever One (VATECH) bridge

- **Category:** vendor-direct (local program launch). **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_4.html; https://www.opendental.com/site/bridgecleverone.html; https://opendental.blog/version-25-4/.

**Behavior.** A program link "Clever One by VATECH" for VATECH's newer imaging product. It launches Clever One for the selected patient. The EzDent-i bridge stays. Per the public page, new VATECH users should start on Clever One, and existing EzDent-i users may need to move to the new bridge.

**Setup fields:**

- Enabled
- Path of file to open (default `C:\Program Files\VATECH\CleverOne\Bin\CleverOne.exe`)
- Text on button (default "Clever One")
- Property "Enter 0 to use PatientNum, or 1 to use ChartNum" (usually 0)
- Hide Button for Clinics
- Toolbars (default Chart module)
- Button image (optional, exactly 22x22)

**Rules.**

- The hand-off format is not public (**VERIFY with VATECH**).
- **Fork default:** reuse the EzDent-i `linkage.xml` hand-off (`WpfControlsOD/Bridges/EzDenti.cs`), the closest VATECH precedent: patient names, ID per the property, birthdate as dd/MM/yyyy, address, phones, gender.
- Do **not** send SSN unless VATECH requires it. EzDenti sends `SocialID`; drop it for Clever One.
- The button appears only when enabled (neutrality rule).

**Touchpoints:** `OpenDentBusiness/TableTypes/Program.cs` (ProgramName), `WpfControlsOD/Logic/ProgramL.cs` (dispatch), new `WpfControlsOD/Bridges/CleverOne.cs`, a ConvertDatabases step inserting the `program`/`programproperty` rows (disabled). **Schema:** none.

### v25_4-proccodes-codegroup-reset: Reset Code Groups in Procedure Code Tools

- **Category:** desktop. **Size:** M. **Depends on:** v25_3-procs-codegroup-reset-tool (**implement once**). This entry adds public detail.
- **Sources:** https://www.opendental.com/site/version25_4.html (backported to 25.3.54); https://www.opendental.com/site/canadaproccodetools.html: the Code Groups option deletes all existing code groups and recreates the defaults, after asking which code system to use.

**Behavior.** A **Code Groups** checkbox in Procedure Code Tools. Running it asks for the code system (CDT for US, CDA for Canada), then replaces the office's code groups with the default set for that system.

**Rules.**

- **Defaults:** the frequency groups created by `ConvertDatabases8` (BW, Exam, Pano/FMX, Prophy, Fluoride, Sealant, SRP, Full Debridement, Perio Maint, …) with their `CodeGroupFixed` values and code lists.
- **Benefits that point to code groups** (frequency, age limit, other benefits) must not be orphaned (**fork decision**; the public page says only that existing groups are deleted and defaults recreated):
  - re-point each benefit to the new group with the same `CodeGroupFixed`, or failing that, the same name
  - list any that can't be re-pointed, and stop unless the user accepts
- This refines the 25.3 rule "never delete a referenced group".
- Warn clearly that custom groups are deleted.

**Permissions/audit:** SecurityAdmin and ProcCodeEdit. A ProcCodeEdit log entry naming the code system and counts.

**Touchpoints:** `OpenDental/Forms/FormProcTools.cs` (+ designer), `OpenDentBusiness/Data Interface/CodeGroups.cs`, `OpenDentBusiness/Data Interface/Benefits.cs`, `OpenDentBusiness/Misc/ConvertDatabases8.cs` (default list at lines 20-35; move it to a shared helper). **Schema:** none.

### v25_4-misc-ucr-terminology: "UCR" renamed "Standard Provider Fee"

- **Category:** desktop. **Size:** M. **Depends on:** v25_3-family-bluebook-providerfee.
- **Sources:**
  - https://www.opendental.com/site/version25_4.html, https://opendental.blog/version-25-4/
  - https://www.opendental.com/site/canadainsplan.html and https://www.opendental.com/site/0_discountplan.html (current wording "standard provider fee")
  - https://www.opendental.com/OpenDentalDocumentation25-4.xml (`insplan.ClaimsUseUCR` described as the standard provider fee)

**Behavior.** User-visible text that said "UCR" says "Standard Provider Fee" (or "standard fee" where space is short). Logic, preference names and column names are unchanged.

**Strings to change** (from grep):

- `OpenDental/User Controls/Preferences/UserControlFamilyInsurance.Designer.cs`:
  - "Ins plans with exclusions use UCR fee (zero out write-offs)"
  - the frequency/age and annual max variants
  - "Insurance plans default to show UCR fee on claims"
- `UserControlFamilyInsurance.cs` (line 79 prompt)
- `OpenDental/Forms/FormInsPlan.designer.cs`: "Claims show UCR fee, not billed fee"
- `OpenDental/User Controls/Preferences/UserControlAccountClaimSend.Designer.cs`: "Use UCR fee for billed fee even if PPO fee is higher"
- `OpenDental/Forms/FormInsBlueBookRules.cs` (lines 92, 216) and designer (line 163): "UCR fee", "UCR Fee Percent" (coordinate with the 25.3 Blue Book item)
- `OpenDentBusiness/TableTypes/InsBlueBookRule.cs` `[Description("UCR Fee")]` and the log text in `OpenDentBusiness/Data Interface/InsBlueBooks.cs` line 382
- Report columns:
  - `OpenDental/Forms Reports/FormRpCapitation.cs` "UCR Fee" (lines 82, 91)
  - `OpenDental/Forms Reports/FormRpNetProdDetail.cs` "UCR" (line 273)
  - `OpenDental/Forms Reports/FormRpProviderPayroll.cs` "UCR Production" (line 314)
  - `OpenDentBusiness/Reports/RpProdInc.cs` "UCR Poduction" (line 568; also fixes the typo)
- `OpenDentBusiness/TableTypes/InsPlan.cs` `ExclusionRule.UseUcrFee` description (line 169)
- Treatment Plan grid column "Cat% UCR": the display-field description in `OpenDentBusiness/Data Interface/DisplayFields.cs` line 1260 (`CatPercUCR`). Keep the internal name; change only the shown text.
- Corrected on fact-check: `ControlTreat.cs` and `OpenDental/Logic/EtransL.cs` have **no** user-visible "UCR" strings (ControlTreat shows the column above through Display Fields; EtransL only sets `ClaimsUseUCR`).
- translation keys (`Lans`)

**Rules.**

- Keep `PrefName` members (`InsDefaultShowUCRonClaims`, `InsPlanExclusionsUseUCR`, …) and DB column names.
- Keep report CSV header keys stable for users' downstream tools: add the new header text, and note the change in release notes.

**Schema:** none.

### v25_4-tp-estimates-appt-date: Treatment plan estimates use the scheduled appointment's date

- **Category:** desktop. **Size:** L. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_4.html (Treatment Plan module estimates consider the appointment date of scheduled procedures).

**Behavior.**

- For a treatment-planned procedure attached to a **scheduled** appointment, insurance estimates are figured as of the **appointment date**, not today or the TP date picker.
- This affects the benefit year (annual max, deductible remaining), frequency limits, age limits, and plan/coverage dates.
- Procedures not on a scheduled appointment keep today's behavior.

**Rules (fork interpretation, VERIFY edge cases).**

1. As-of date per procedure:
   - if `AptNum>0` and the appointment status is Scheduled or ASAP, use the appointment date when it is today or later
   - for a past-dated scheduled appointment, use today
   - planned appointments (`PlannedAptNum` only) do not count
2. History and "used" amounts (`ClaimProcs.GetHistList`) are evaluated per benefit period relative to each procedure's as-of date. A procedure scheduled next benefit year draws on next year's max and deductible, and earlier TP procedures in the same year still accumulate.
3. Ordering within the plan stays the TP order. Accumulation across periods is done per period.
4. Age-limit checks use the patient's age on the as-of date.
5. Coverage ending before the appointment date (patplan/inssub dates) means no estimate.
6. Saved (snapshot) treatment plans are not recalculated.
7. The TP module's date picker (`dateTimeTP`) still sets the as-of date for procedures without a scheduled appointment.

**UI:** optionally show the as-of date in a TP grid column "Est. Date" (fork). Otherwise none.

**Permissions/audit:** none. **Preferences:** none (**VERIFY** whether 25.4 added an on/off preference).

**Touchpoints:**

- `OpenDentBusiness/Db Multi Table/TreatmentPlanModules.cs` (`GetLoadActiveTpData` line 49, `GetActiveTpPlanTpRows` line 233)
- `OpenDental/Main Modules/ControlTreat.cs` (lines 1896-1908, 2594-2647)
- `OpenDentBusiness/Data Interface/Procedures.cs` (`ComputeEstimates`)
- `OpenDentBusiness/Data Interface/ClaimProcs.cs` (`GetHistList`)
- `OpenDentBusiness/Logic/BenefitLogic.cs`

**Schema:** none.

### v25_4-claims-era-displayfields: ERA Claim Paid grid in Display Fields

- **Category:** desktop. **Size:** M. **Depends on:** v25_3-account-era-displayfields (**implement once**).
- **Sources:** https://www.opendental.com/site/version25_4.html (backported to 25.3.78, after the office's 25.3.48); https://www.opendental.com/OpenDentalDocumentation25-4.xml (`DisplayFieldCategory.ERA = 23`).

**Behavior and rules:** see the 25.3 entry.

- Available columns are the 24.3 set in `FormEtrans835Edit.FillClaimDetails` (lines 141-159): Recd, Patient, DateService, Claim Identifier, Payor Control#, Status, ClaimFee, InsPaid, PatPort, Deduct, Writeoff.
- Optional extras (fork): Subscriber ID, Allowed, Matched Claim.
- Recd and Patient can't be hidden.

**Schema:** none.

---

## Not on the 25.4 page, but in the 25.4 schema

These come from the 25.3-to-25.4 schema documentation. They have to be built so that 25.4+ data loads and behaves correctly.

### v25_4-sheets-datetimesubmitted: Date submitted on sheets and eForms

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml (`sheet.DateTimeSubmitted`, `eform.DateTimeSubmitted`).

**Behavior.**

- Each patient form records when it was submitted.
- eClipboard sets it automatically; that part is excluded.
- When a form is completed in the office, the user can set it in Fill Sheet / Fill eForm. It is blank (MinValue) until set.

**UI:**

- A "Submitted" date-time box in `FormSheetFillEdit` and `FrmEFormFillEdit`. Empty means not submitted.
- Offer "Now" when the patient signs. **Fork:** default to now when a signature box is signed on a new form.
- A Submitted column in the patient's Forms list.

**Rules:** don't confuse it with `DateTSheetEdited` (last opened and saved) or `DateTEdited`.

**Schema:** two `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` columns.

**Touchpoints:** `OpenDentBusiness/TableTypes/Sheet.cs`, `OpenDentBusiness/TableTypes/EForm.cs`, `OpenDental/Forms/FormSheetFillEdit.cs`, `WpfControlsOD/Frms/FrmEFormFillEdit.xaml.cs`.

### v25_4-family-inseditlog-bigvalues: Insurance edit logs keep long values

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml.

**Behavior.** Insurance plan and subscriber edit logs record full old and new values even when they are longer than 255 characters, for example plan notes and subscriber notes.

**Rules.**

- Values of 255 characters or fewer go in `OldValue`/`NewValue`, as today.
- Longer values go in `OldValueBig`/`NewValueBig`; the short column is left empty.
- The log windows show whichever is filled.
- Up to 65k characters (text).

**Schema:** `inseditlog.OldValueBig`, `.NewValueBig`, `inseditpatlog.OldValueBig`, `.NewValueBig`, all `text NOT NULL`.

**Touchpoints:** `OpenDentBusiness/Data Interface/InsEditLogs.cs`, `InsEditPatLogs.cs`, `OpenDental/Forms/FormInsEditLogs.cs`, `OpenDental/Forms/FormInsEditPatLog.cs`.

### v25_4-family-referral-phone: Referral phone no longer limited to 10 digits

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/OpenDentalDocumentation25-4.xml (`referral.Telephone`: before 25.4 exactly 10 digits only).

**Behavior.** The referral's primary phone accepts up to 30 characters, with formatting and extensions, like other phone fields.

**Rules.**

- Remove the 10-digit validation.
- Keep displaying 10-digit values in the office phone format.

**Schema:** `referral.Telephone varchar(30) DEFAULT ''`.

**Touchpoints:** `WpfControlsOD/Frms/FrmReferralEdit.xaml.cs`, `OpenDentBusiness/TableTypes/Referral.cs`.

### v25_4-security-patientselected-audit: Audit entry when a patient is selected

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/OpenDentalDocumentation25-4.xml (`EnumPermType.PatientSelected = 275`, audit when a patient has been selected).

**Behavior.** When enabled for auditing, every patient selection (patient select window, module patient change, search results) writes a securitylog row with PatNum. This supports HIPAA access reviews.

**Rules.**

- It is an audit-only permission type, with no access gate.
- Logging is controlled by Audit Trail Permissions, so offices that don't want the volume can turn it off. **Fork default: off.**
- Don't log repeated selection of the same patient within 60 seconds by the same user (fork).

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs`, `OpenDental/Main Modules/FormOpenDental.cs` (patient change path), `OpenDentBusiness/Data Interface/SecurityLogs.cs`. **Schema:** none.

### v25_4-misc-enum-parity: Enum values for safe loading of 25.4+ data

- **Category:** desktop. **Size:** S. **Depends on:** v25_3-schema-compat-columns.
- **Sources:** https://www.opendental.com/OpenDentalDiffDocumentation25-4.xml; our full-document diff of OpenDentalDocumentation25-3.xml against 25-4.

**Why.** Several columns store enums by number or by name (`statement.StatementType` is a varchar holding the name). An unknown value can crash parsing or display, so add every 25.4 value, even for features we don't build.

| Enum (column) | Values to add or rename |
|---|---|
| `CreditCardSource` (`creditcard.CCSource`, `payment.PaymentSource`, `xwebresponse.CCSource`, `payconnectresponseweb.CCSource`) | MeetInTheCloudTerminal=23, MeetInTheCloudPaymentPortal=25, MeetInTheCloudPaymentPortalGuest=26 |
| `AlertType` (`alertitem.Type`, `alertcategorylink.AlertType`) | 46 TreatmentPlanImagesFolderInaccesible, 47 PaymentPlanImagesFolderInaccesible, 48 CannotCreateOrAccessPatientFolder, 49 DefaultEmailNotSet (display only), 50 PaySuite |
| `FormType` (`alertitem.FormToOpen`) | FrmPaySuiteReconciliation=21 |
| `LogSources` (`securitylog.LogSource`, `entrylog.LogSource`) | Era835Import=26, ODTouch=27, eClipboard=28 |
| `EnumPermType` | rename 13 to BlockoutEdit; add 272 BlockoutAdd, 273 BlockoutDelete, 275 PatientSelected; rename 77/78/81 (dental school item). 264-271 and 274 belong to earlier specs. |
| `StmtType` (`statement.StatementType`, varchar) | Walkout. A walkout statement prints like a regular one. |
| `DisplayFieldCategory` | 22 AccountModuleServiceDateView, 23 ERA |
| `eServiceAction` / `eServiceType` | 134-138 / AddressValidation=17 (reserve only) |
| `ErxStatus` (`clinicerx.EnabledStatus`, `providererx.IsEnabled`) | value 6 renamed from PendingPodio to Pending |
| `UserOdFkeyType` | DefaultTestingTeamJM=29 (HQ, reserve only) |
| `TrustedEtransTypes` (`carrier.TrustedEtransFlags`) | documented as None=0, RealTimeEligibility=1 (bit flag); confirm ours matches |

**Rules.**

- Unknown future numeric values render as the number, never throw.
- `StatementType` parsing falls back to NotSet with a log line.

**Touchpoints:**

- `OpenDentBusiness/TableTypes/CreditCard.cs` (line 130)
- `OpenDentBusiness/TableTypes/AlertItem.cs` (lines 79, 237)
- `OpenDentBusiness/TableTypes/SecurityLog.cs` (line 60)
- `OpenDentBusiness/TableTypes/GroupPermission.cs` (line 35)
- `OpenDentBusiness/TableTypes/Statement.cs` (line 361)
- `OpenDentBusiness/TableTypes/DisplayField.cs` (line 62)
- `OpenDentBusiness/TableTypes/EServiceLog.cs` (line 129)
- `OpenDentBusiness/WebBridges/Erx/Erx.cs` (line 353)
- `OpenDentBusiness/TableTypes/UserOdPref.cs` (line 34)

**Schema:** none.

### v25_4-imaging-16bit-windowing: Correct windowing for true 16-bit radiographs

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/OpenDentalDocumentation25-4.xml (`document.WindowingMax` description changed from the 25.3 text).

**Behavior.**

- Brightness/contrast windowing is still stored as 0-255 in `document.WindowingMin/Max`.
- For 12-bit images, multiply by 16 (as in 24.3).
- For true 16-bit images, use the top 12 bits: multiply by 256.
- In 24.3, both 12- and 16-bit DICOM data are divided by 16 (`BitmapDicom.ApplyWindowing`, line 44), so bright 16-bit images saturate.

**Rules.**

- Tell 12-bit from 16-bit by the DICOM BitsStored (not BitsAllocated).
- Existing stored windowing values keep their 0-255 meaning. Images saved by Open Dental 25.4+ then display the same here.

**Touchpoints:** `OpenDentBusiness/Imaging/BitmapDicom.cs` (lines 31-70), `OpenDentBusiness/Imaging/ImageHelper.cs` (lines 22, 309 "todo" notes), `OpenDentBusiness/TableTypes/Document.cs`. **Schema:** none.

---

## Excluded

One line each.

### Open Dental hosted services (excluded-od-service)

| ID | Item | Why excluded |
|---|---|---|
| v25_4-eclipboard-ocr | OCR for eClipboard (also 25.2.71, 25.3.41) | A paid add-on to Open Dental's hosted eClipboard apps. Insurance-card text is read by their service. Same as v25_3-eservices-eclipboard-ocr. |
| v25_4-eclipboard-setup-reorg | eClipboard setup window reorganized | eClipboard is a cut Open Dental eService; we remove its setup UI. |
| v25_4-eclipboard-appt-datetime | Appointment date and time shown in eClipboard 25 | A change inside the hosted eClipboard 25 app. |
| v25_4-canada-oda-feeguide-2026 | 2026 Ontario Dental Association fee guides (also 25.2.73, 25.3.43) | Downloaded from Open Dental's servers under Open Dental's licence agreement with the ODA, with member credentials and support-assisted import (https://www.opendental.com/site/canadafeeschedules.html). Offices can type or import fees themselves. Same as v25_3-canada-fee-guides-2026. |
| v25_4-canada-bcda-feeguide-2026 | 2026 BC Dental Association fee guides (also 25.3.47) | Same reason: BCDA member password plus an Open Dental-hosted, support-run import. |
| v25_4-paymentportal-inactivity-logout | Payment Portal inactivity prompt and automatic logout | The Payment Portal is an Open Dental-hosted patient web page. |
| v25_4-payments-gpv2-paymentportal | Global Payments V2 usable in the Payment Portal (25.4.53+) | Same hosted portal. The desktop V2 integration is specified separately. |
| v25_4-eservices-alert-types | New alerts for the eConnector failing to save signed TP/payment plan PDFs, patient folder access, default email not set for eServices (AlertType 46-49) | They report problems of the eConnector, ODTouch and eClipboard, which are cut. The values are added only for data parity (v25_4-misc-enum-parity). |

### Other exclusions (excluded-other)

| ID | Item | Why excluded |
|---|---|---|
| v25_4-clinics-add-process | Updated process for adding new clinics | Tied to `registrationkey.DateTClinicAccess`, which Open Dental HQ sets to allow adding clinics until a given time (licensing). Our fork keeps adding clinics unrestricted, as in 24.3 (`FormClinics.butAdd_Click`). Carry the column for schema parity only. |
| v25_4-hq-internal-enums | Job Manager default testing team (UserOdFkeyType 29), address validation eService logging (eServiceType 17, eServiceAction 138) | HQ-internal tools and services. Reserve the values only. |
| v25_4-api-procedurelogs-groupnote-noprocs | API: ProcedureLogs POST GroupNote without attached procedures | Open Dental's local REST API is removed in our fork. The desktop already allows procedure-less group notes (v25_1-chart-group-note-flexible). |
| v25_4-api-vitalsigns | API: new Vitalsigns resource | Local REST API removed. |
| v25_4-api-claims-other-medical | API: Claims support Other and Medical claims | Local REST API removed. |
| v25_4-api-subscription-medicationpat | API: subscription to MedicationPat table | Local REST API removed. |
| v25_4-api-subscription-labcase | API: subscription to LabCase table | Local REST API removed. |
| v25_4-api-procedurelogs-priority-zero | API: ProcedureLogs PUT can set Priority to 0 | Local REST API removed. |
| v25_4-api-tasks-tasklistnum-zero | API: Tasks POST allows TaskListNum 0 | Local REST API removed. |
| v25_4-api-payments-merchantfee | API: Payments POST MerchantFee field | Local REST API removed. |
| v25_4-api-guardians-get | API: Guardians GET | Local REST API removed. |
| v25_4-api-guardians-post-put-delete | API: Guardians POST, PUT, DELETE | Local REST API removed. |
| v25_4-api-appointments-get-assistant-mirrored | API: Appointments GET returns Assistant and IsMirrored | Local REST API removed. |
| v25_4-api-payments-partial-tp-procs | API: Payments PUT Partial accepts TP procedures when TP prepayments are allowed | Local REST API removed. |
| v25_4-api-appointments-post-put-assistant-mirrored | API: Appointments POST/PUT set Assistant and IsMirrored | Local REST API removed. |
| v25_4-api-patrestrictions | API: PatRestriction GET, POST, DELETE | Local REST API removed. |
| v25_4-api-patients-simple-phone-filters | API: Patients GET Simple filters by home, work and wireless phone | Local REST API removed. |
| v25_4-api-statements-delete | API: Statements DELETE | Local REST API removed. |
| v25_4-api-deposits | API: Deposits GET, POST, PUT, DELETE | Local REST API removed. |
| v25_4-api-procedurelogs-diagnosticcode | API: ProcedureLogs POST/PUT DiagnosticCode fields | Local REST API removed. |
| v25_4-api-employees-post-put | API: Employees POST and PUT | Local REST API removed. |
| v25_4-api-payments-unallocated-prepayment | API: Payments POST isUnallocatedPrepayment field | Local REST API removed. |
| v25_4-api-queries-sftpport | API: SftpPort for Queries POST | Local REST API removed. |
| v25_4-api-sheets-sftpport | API: SftpPort for Sheets POST DownloadSftp | Local REST API removed. |
| v25_4-api-documents-sftpport | API: SftpPort for Documents POST UploadSftp, DownloadSftp, Thumbnails, DownloadMount | Local REST API removed. |
| v25_4-api-eobattaches-sftpport | API: SftpPort for EobAttaches POST UploadSftp, DownloadSftp | Local REST API removed. |

## Open questions (VERIFY list)

1. Real names of the 25.4 preferences:
   - income transfer limit
   - recurring-charge failure email
   - PaySuite and Global Payments V2 program properties
   - `ProgName` values for PaySuite, Global Payments V2, Clerri and Clever One

   These are needed to map 26.1 data on adoption.
2. `schoolapproval.SignOffStatus` values and whether approval blocks completing or billing.
3. Exact eForm `DbLink` text for patient fields.
4. Global Payments V2: the token column and the `payterminal.CCIntegration` string.
5. Treatment plan estimates by appointment date: is there an on/off preference? How are past-dated scheduled appointments handled?
6. Perio direction and Print window: exact 25.4 behavior.
7. UDS+ report: Open Dental's exact columns and whether it also outputs the aggregate table.
8. Insurance Verification List: exact Min/Max labels, and whether Insurance Verification Setup has a default for the minimum.
9. Pay Labs: how Open Dental splits a procedure's payment between the procedure and its (up to two) labs.
10. Clerri: does the launch alone help an office without Open Dental's eConnector data sync? Ask Clerri before building.
11. Blockout permission conversion: which groups Open Dental's 25.4 conversion gave BlockoutAdd and BlockoutDelete.

## Fact-check log (2026-10-18)

An adversarial review re-read the release page, the public pages cited above, the 25.3 and 25.4 schema documents, the 25.3.48 DDL dump, our measured diff and the repo. Results:

- **Coverage:** every line on https://www.opendental.com/site/version25_4.html has an entry: the 4 major features (10 bullets), 43 category lines and 24 API lines. Nothing was missing. Counts are unchanged: 46 desktop, 8 vendor-direct, 34 excluded.
- **Schema:** every type, default and enum value in the delta table matches the 25.4 diff document and `schema-changes.json`, and every 25.4 addition is absent from the 25.3.48 dump. One wrong statement was fixed: `schema-changes.json` has no `PaymentId` index on `paysuitepayment`.
- **Behavior corrected:**
  - Insurance Verification date range: a min/max number of days until the appointment, not From/To dates.
  - PaySuite plan details: requested from the **Request Electronic Benefits** row in Edit Insurance Plan.
  - Global Payments V1 program-link description wording.
  - The Clever One / EzDent-i relationship.
  - The UCR string list: added 5 missed places; removed ControlTreat and EtransL, which have no such strings.
- **Rules marked as fork decisions or VERIFY** where the public material is silent:
  - dental-school UI and roster rules
  - Pay Labs split
  - Code Groups benefit re-pointing
  - Blockout conversion
  - UDS+ placement
  - PEM label text
- **Touchpoints:** all cited existing files exist. Fixed:
  - PEM control names
  - the `CanadianNetworks.cs` reference
  - `SheetDrawingJob` `XFont` sites (14, not 3)
  - `FormCarriers` multi-select (new work, not an existing flag)
  - added the missed `DateLastAssigned` write sites
- **Copied text:** no passage of 15 or more words matches a source. The longest match (13 words, a blog phrase) was reworded, along with two shorter release-note parentheticals.
