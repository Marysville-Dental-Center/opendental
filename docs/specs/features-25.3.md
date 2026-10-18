# Open Dental 25.3: desktop feature specs for the 24.3-based fork

Status: draft spec, 2026-10-18, fact-checked the same day (see "Fact-check" under Sources and method). Baseline: this fork (Open Dental 24.3.63). Target: parity with Open Dental 25.3 so the office database (Open Dental 25.3.48, `fox-db`) can be adopted. Next step after this: 26.1.70.

Everything below is re-specified in our own words from Open Dental's public material. Nothing was decompiled. Where public material doesn't describe a behavior, the spec says so and marks it **VERIFY**. For those items, an engineer should either watch the behavior in the office's running Open Dental 25.3.48 (ordinary use of the program, not reverse engineering) or read the data in a **restored copy** of `fox-db`.

## Summary

| ID | Title | Module | Category | Size | Depends on |
|---|---|---|---|---|---|
| v25_3-chat-intraoffice-chat | Intra-office chat (separate chat app) | Manage / Chat | desktop | XL | v25_3-schema-compat-columns |
| v25_3-procs-cdt-2026 | 2026 CDT code set | Procedures | desktop | S | decoupling Phase 2 (CDT replacement) |
| v25_3-account-orthocase-cleanup | Ortho Case window: remove excess text | Account | desktop | S | none |
| v25_3-security-inswriteoffedit-clarify | Clarify the Insurance Write-off Edit permission | Security | desktop | S | none |
| v25_3-account-payplan-bypass-globallock | Preference: payment plans ignore the global lock date | Account | desktop | S | none |
| v25_3-account-paysplit-payplan-attach | Attach paysplits to payment plans in more cases | Account | desktop | M | v25_2-account-payplan-prepay-attach |
| v25_3-appts-timebar-secondary-color | Preference: secondary color in time bar when no provider time | Appointments | desktop | S | none |
| v25_3-appts-audit-detail | More detail in appointment move/edit audit entries | Appointments | desktop | M | none |
| v25_3-security-apptcreate-pinboard | Appointment Create also covers scheduling a planned appt from the pinboard | Security | desktop | S | none |
| v25_3-security-apptmove-scope | Appointment Move scope: all statuses, not planned-to-pinboard | Security | desktop | S | none |
| v25_3-appts-mirroring | Appointment mirroring into the secondary provider's operatory | Appointments | desktop | L | v25_3-appts-audit-detail |
| v25_3-family-otherbenefits-grid | Edit Other Benefits directly in the grid; code groups in Other Benefits | Family | desktop | M | v25_1-family-codegroups-hide-delete, v25_3-family-exclusions-tooth |
| v25_3-family-insplans-innet | In Net column in the Insurance Plans list | Family | desktop | S | none |
| v25_3-family-exclusions-tooth | Insurance exclusions limited to tooth numbers | Family | desktop | M | none |
| v25_3-manage-timeclock-reportsto | Reports To filter on the Manage module time clock | Manage | desktop | S | none |
| v25_3-manage-accounting-checknum | Button to fill the next check number | Manage / Accounting | desktop | S | none |
| v25_3-manage-accounting-printcheck | Print checks from accounting transactions; Check sheet type | Manage / Accounting | desktop | L | v25_3-manage-accounting-payee, v25_3-manage-accounting-checknum |
| v25_3-manage-accounting-payee | Payee and Notes on accounting transactions | Manage / Accounting | desktop | M | none |
| v25_3-manage-statement-email-lastname | Emailed statements carry the patient's last name | Manage / Billing | desktop | S | none |
| v25_3-manage-accounting-needsreview | Needs Review flag on accounting transactions | Manage / Accounting | desktop | S | none |
| v25_3-chart-vitalsign-notes | Notes box in Edit Vital Signs | Chart / Medical | desktop | S | none |
| v25_3-medical-medlist-missingtab | Remove the Missing Generic/Brand tab | Medical | desktop | S | none |
| v25_3-tools-dbm-whitespace-tools | Move "Notes With Too Much White Space" to the DBM Tools tab | Tools | desktop | S | none |
| v25_3-misc-printer-options | Printer options window when printing from some areas | Misc / Printing | desktop | M | none |
| v25_3-imaging-font-autocalc | Imaging text: actual and apparent font size update automatically | Imaging | desktop | S | none |
| v25_3-sheets-checkbox-cleanup | Edit CheckBox window: remove excess text | Sheets | desktop | S | none |
| v25_3-chart-orthochart-nonmodal | Ortho Chart no longer blocks the rest of the program | Chart | desktop | M | none |
| v25_3-misc-export-csv-txt | Export .csv and Export .txt in more windows | Misc / Grids | desktop | M | none |
| v25_3-manage-statement-barcode | Inserter barcode on printed statements | Manage / Billing | desktop | M | none |
| v25_3-security-claimproc-billedtoins | Billed-to-ins permission also covers Code Sent to Ins | Security / Account | desktop | S | none |
| v25_3-security-claimproc-billedtoins-desc | Second wording change for the same permission | Security | desktop | S | v25_3-security-claimproc-billedtoins |
| v25_3-tools-file-monitor | File Monitor for reads/writes to the image folder | Tools / Help menu | desktop | M | none |
| v25_3-tools-dbm-employee-supervisor | DBM check: EmployeeMissingSupervisor | Tools | desktop | S | none |
| v25_3-family-audit-preferredname | Audit trail records preferred-name changes | Family | desktop | S | none |
| v25_3-tp-add-procs-active | Easier adding of procedures to the active treatment plan | Treatment Plan | desktop | M | none |
| v25_3-school-course-scheduling | Dental school course definitions, schedules and overrides (schema and data rules; UI version unconfirmed) | Dental Schools | desktop | M | none |
| v25_3-school-approval-schema | Dental school instructor approval table (schema only in 25.3) | Dental Schools | desktop | S | none |
| v25_3-schema-compat-columns | Schema and enum values needed only for data compatibility | Database | desktop | S | none |
| v25_3-family-bluebook-providerfee | Blue Book rule "UCR Fee" renamed "Provider Fee" | Family / Insurance | desktop | S | none |
| v25_3-tools-dbm-base64-docs | DBM tool: Create Documents From Base64 Notes (25.3.39) | Tools | desktop | S | none |
| v25_3-canada-cda-codes-2026 | 2026 CDA procedure codes (Canada, 25.3.41) | Procedures | desktop | S | none |
| v25_3-misc-pem-path-label | PEM path field label in Choose Database / Reports Setup (25.3.53) | Misc | desktop | S | none |
| v25_3-procs-codegroup-reset-tool | Procedure Code Tools: reset Code Groups (25.3.54) | Procedures | desktop | S | none |
| v25_3-account-era-displayfields | ERA Claim Paid grid columns in Display Fields (25.3.78) | Account / ERA | desktop | M | v25_3-schema-compat-columns |
| v25_3-account-payconnect-error-log | Log PayConnect errors (desktop, direct to vendor) | Account / Payments | vendor-direct | S | none |
| v25_3-eservices-websched-existingpat | Web Sched Existing Patient overhaul | eServices | excluded-od-service | n/a | |
| v25_3-account-carecredit-refund-adjtype | CareCredit merchant refund adjustment type | Account | excluded-other | n/a | |
| v25_3-eservices-eclipboard-branding | eClipboard 25 color personalization | eServices | excluded-od-service | n/a | |
| v25_3-eservices-byod-deprecated | BYOD deprecated | eServices | excluded-od-service | n/a | |
| v25_3-eservices-websched-asap-user | Web Sched ASAP records the sending user | eServices | excluded-od-service | n/a | |
| v25_3-eservices-texting-register-carriers | Register with Carriers form for texting | eServices | excluded-od-service | n/a | |
| v25_3-eservices-eclipboard-licenses | View eClipboard 25 / Web licenses | eServices | excluded-od-service | n/a | |
| v25_3-help-supportstatus-qr | QR code in Support Status | Help | excluded-od-service | n/a | |
| v25_3-eservices-eclipboard-ocr | OCR for eClipboard (25.3.41) | eServices | excluded-od-service | n/a | |
| v25_3-canada-fee-guides-2026 | 2026 Ontario and BC fee guides (25.3.43, 25.3.47) | Canada | excluded-od-service | n/a | |
| v25_3-api-benefits-treatarea | API: TreatArea on benefits | API | excluded-other | n/a | |
| v25_3-api-procedurelogs-groupnote-append | API: append to group notes | API | excluded-other | n/a | |
| v25_3-api-codegroups-showin | API: CodeGroups ShowInFrequency/ShowInOther | API | excluded-other | n/a | |
| v25_3-api-patfields-put-response | API: PatFields PUT returns object | API | excluded-other | n/a | |
| v25_3-api-rxpats-erxpharmacy | API: RxPats ErxPharmacyInfo | API | excluded-other | n/a | |
| v25_3-api-payments-put-payplancharges | API: Payments PUT Partial with pay plan charges | API | excluded-other | n/a | |
| v25_3-api-procedurelogs-complete-noappt | API: completed procedures without appointment | API | excluded-other | n/a | |
| v25_3-api-statements-post | API: Statements POST | API | excluded-other | n/a | |
| v25_3-api-payplanlinks | API: PayPlanLinks resource | API | excluded-other | n/a | |
| v25_3-api-payplans-put-dynamic | API: PayPlans PUT (dynamic) | API | excluded-other | n/a | |
| v25_3-api-groupnotes-tp | API: group notes on TP procedures | API | excluded-other | n/a | |
| v25_3-hq-task-triageposition | HQ triage position on tasks | HQ | excluded-other | n/a | |
| v25_3-hq-enum-additions | HQ-only enum values (phone extension permission, map setting, schedule Override) | HQ | excluded-other | n/a | |

Counts: 44 desktop, 1 vendor-direct, 23 excluded.

Size legend: **S** up to 1 day, **M** 2 to 5 days, **L** 1 to 3 weeks, **XL** more than 3 weeks. Estimates include unit tests and the database conversion step, not QA on the office copy.

## Sources and method

| Source | URL | Used for |
|---|---|---|
| 25.3 release notes | https://www.opendental.com/site/version25_3.html | The item list (every bullet is covered below) |
| Versions index | https://www.opendental.com/site/versions.html | 25.3 summary line, which also names "Dental School enhancements" |
| 25.3 highlights blog | https://opendental.blog/version-25-3/ | Behavior notes for mirroring, insurance changes, misc |
| Chat feature blog | https://opendental.blog/feature-highlight-intra-office-chat/ | Chat behavior |
| Public overview pages | https://www.opendental.com/site/0_chat.html, https://www.opendental.com/site/0_appointments.html, https://www.opendental.com/site/0_insbenefits.html, https://www.opendental.com/site/0_dentalschools.html | Current behavior summaries |
| Database documentation 25.3 | https://www.opendental.com/OpenDentalDocumentation25-3.xml | Column meanings, enum values, preference names; compared against the 25.2 and 26.1 documents (same URL pattern) |
| Database diff document 25.3 | https://www.opendental.com/OpenDentalDiffDocumentation25-3.xml | Schema changes (see also `docs/specs/schema-changes-24.3-to-26.1.md`) |
| Database diff document 25.2 | https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml | Mirroring columns and the `ApptMirrorSecondary` preference name (the columns were added in 25.2) |
| Public API docs | https://www.opendental.com/site/apiappointments.html, https://www.opendental.com/site/apibenefits.html | Field semantics (IsMirrored, ToothRange) |
| 25.4 and 26.1 release notes | https://www.opendental.com/site/version25_4.html, https://www.opendental.com/site/version26_1.html | Items backported into 25.3.x builds, and follow-ups |
| Public 24.3 manual | https://www.opendental.com/manual243/ (for example `transactionhistory.html`, `otherbenefits.html`, `editbenefit.html`) | Baseline UI names in our fork |
| Login-gated 25.x manual | https://opendental.com/help254/ (for example `overviewapptmirror.html`, `chatsettings.html`, `filemonitor.html`, `sheetscheck.html`, `dscoursedefs.html`) | **Not opened.** Only the short excerpts a public web search returned were used; each use is marked |

Limits:

- The 25.x manuals (`/help252/` to `/help262/`) need an Open Dental customer login. We did not try to get around that. The public `/manual/` folder is still the 24.3 manual, so it describes our baseline, not 25.3. Re-checked on 2026-10-18: `/help253/` and `/help254/` pages return the customer gate, `/manual253/` returns 404, and a new 25.x page name under `/manual/` (for example `overviewapptmirror.html`) only redirects through `r.htm` into the gated help.
- The 25.3 release-note page links to no manual pages, so "follow the manual links" had nothing to follow. The sources above stand in for them.
- Preference names: the 25.3 database document names only `ApptMirrorSecondary` and a `ChatServer...` prefix. Every other preference name below is **proposed**. Open Dental's 25.3 conversion has already written its own rows into the office's `preference` table, so before coding, list them on a restored copy and reuse Open Dental's exact names. That way the office's settings carry over:
  ```sql
  -- run on a RESTORED copy of fox-db, then diff against PrefName in OpenDentBusiness/TableTypes/Pref.cs
  SELECT PrefName, ValueString FROM preference ORDER BY PrefName;
  ```
- Office version: the office runs 25.3.48. Backports that landed in 25.3.49 or later are included for completeness and marked; they are also part of 25.4, so implement each only once. The 26.1 spec refers to the tooth-exclusion item as `v25_4-family-benefit-tooth-exclusions`; it is the same work as `v25_3-family-exclusions-tooth` here.

Fact-check (2026-10-18, second pass):

- Coverage: every bullet on the 25.3 release-note page maps to an item below: the 3 Major Changes features (with their sub-bullets), then Account 6, Appointments 5, eServices 5, Family 3, Manage 6, Miscellaneous 16 and API 11. The 25.4 page lists nine items backported into 25.3.x; all nine are covered here (two of them as exclusions). The 26.1 page lists no 25.3.x backports.
- Codebase paths: every existing path cited below was checked against this checkout. Paths that don't exist are files this spec asks us to create (marked "new").
- Schema: every column and table below was checked against `fox-db-schema.sql` and the measured diff. All 15 of the 25.3 columns and all 10 of the 25.3 tables in the measured diff are accounted for. The 25.2 mirroring columns are covered in the mirroring item.

Shared touchpoints that almost every item with schema uses (not repeated in each section):

- Table classes: `OpenDentBusiness/TableTypes/*.cs`. CRUD is generated by `xCrudGenerator/` into `OpenDentBusiness/Crud/*.cs`.
- Conversion scripts: follow the pattern in `OpenDentBusiness/Misc/ConvertDatabases8.cs` (the last step is `To24_3_63`). The 24.4 to 25.3 steps go in a new file.
- Preferences: `PrefName` enum in `OpenDentBusiness/TableTypes/Pref.cs`. Preference UI lives in `OpenDental/User Controls/Preferences/*.cs`.
- Permissions: `EnumPermType` in `OpenDentBusiness/TableTypes/GroupPermission.cs`, the tree in `OpenDental/User Controls/UserControlSecurityTree.cs`, audit entries via `OpenDentBusiness/Data Interface/SecurityLogs.cs`.

---

## Major changes

### v25_3-chat-intraoffice-chat: Intra-office chat

- **Category:** desktop. **Size:** XL. **Depends on:** v25_3-schema-compat-columns (only for the shared conversion step).
- **Sources:** https://www.opendental.com/site/version25_3.html (Major Changes), https://opendental.blog/feature-highlight-intra-office-chat/, https://www.opendental.com/site/0_chat.html, https://www.opendental.com/OpenDentalDocumentation25-3.xml (tables `chat`, `chatmsg`, `chatattach`, `chatreaction`, `chatuserattach`, `chatuserod`, `sequencecounter`), https://opendental.com/help254/chatsettings.html (login-gated; search excerpt only), https://www.opendental.com/site/version26_1.html (follow-ups).

**User-visible behavior**

- Staff send messages to other users of the same database: one-to-one or in groups. A chat can have a name, and members can be added or removed over time.
- Chat runs as its **own executable**, separate from the main program. It keeps working while the main program shows a modal dialog, and it stays running after the main program closes, including during an update.
- Started from a **Chat** button in the Manage module. It can also start automatically when the user signs on to Windows. In that case it can start hidden, showing only a tray icon.
- Messages support rich text (bold and similar formatting) and hyperlinks. Emoji appear as tokens such as `:thumbsup:` and are drawn as images.
- A user can **quote** an earlier message in a reply. The quote shows above the reply, and clicking it scrolls to the original. Users can add emoji **reactions** to a message, one row per user and emoji, from a fixed list of about 192 emoji names.
- A message can be marked **important**. Important messages use separate notification settings (see below).
- **Attachments**: images and files are stored in the database, not in the image folder. Images also store a PNG thumbnail at most 150 px on the long side. The full file is downloaded only when a user opens it. Files over 16,777,215 bytes are refused with a message.
- **Unread state**: a chat with unread messages shows in bold in the user's chat list.
- **Mute**: a user can mute a single chat.
- **Leaving**: a user who is removed from a chat (or leaves) keeps the chat in the list. It sinks down the list over time instead of jumping to the top, newer messages are hidden from that user, no notifications are sent, and the user no longer appears as a member. Membership changes show inline in the chat as plain-text event messages.
- **Presence**: each user has a status of Available, Away or Do Not Disturb.
  - If the user is linked to an employee, the status follows that employee's time-clock status.
  - Otherwise it follows whether the PC is locked or idle.
  - A user can set a status by hand for a chosen duration, after which it resets automatically.
- **Profile photo**: a picture resampled to 100 px high, with a circular crop stored as "D,X,Y" (diameter, then the centre's x and y, to 0.1 px).
- **Pop-up notifications**, even while chat is minimized. Per-user settings:
  - mute regular notifications
  - seconds before a regular pop-up closes (0 means the default of 5)
  - treat important notifications as regular ones (they are also muted if regular ones are)
  - seconds before an important pop-up closes (0 means the default of 99,999)
  - auto-start, and start in the background
  - keep running in the tray when the window is closed
- Settings window sections (from the search excerpt): Machine, User, System, Notifications, Other.
- **Privacy notice**: chat history is ordinary database data that anyone with query access can read. Show this in the UI, for example in Settings or on first launch.
- Open Dental requires a registration key and doesn't offer chat to its cloud customers. **We drop both conditions.** We have no registration keys, and Thinfinity is being removed.
- Follow-ups in 26.1 (not part of 25.3): an optional sound on new messages (`chatuserod.PlayNotificationSound`), and chat starting automatically when the user has unread messages.

**UI elements**

- Manage module: a new **Chat** button (`OpenDental/Main Modules/ControlManage.cs` and its designer file). It starts the chat executable, or brings it to the front if already running, and passes the logged-on user.
- Chat main window:
  - left: chat list with name or members, last-message time, bold when unread, mute icon, and the user's own presence
  - right: message stack with author photo and name, time, quote block, reactions bar, attachment thumbnails
  - bottom: rich-text editor with bold/italic/underline, link, emoji picker, attach-file button, important toggle, Send
- Dialogs: New Chat (pick users, optional name), Chat Members (add/remove), Rename, Status (status plus duration), Settings (the fields listed above, plus photo import and crop), a notification toast, and a tray icon with a menu (Open, Status, Exit).
- Preferences, Server Connections: optional settings for a **dedicated chat database** (`ChatServer...` preferences, see below). `OpenDental/User Controls/Preferences/UserControlServerConnections.cs` already hosts the read-only server settings.

**Business rules and edge cases**

- **Polling**: every client polls about every 5 seconds for `chatmsg` rows whose `SeqCount` is above the last one it saw, limited to chats the user belongs to. Use `SeqCount`, not dates or primary keys, as the "what's new" cursor. Dates have one-second resolution, and Open Dental allows random primary keys.
- **SeqCount allocation**: each new `chatmsg` takes the next value from the `sequencecounter` row whose `CounterName='chatmsg'`. Do it in one atomic statement:
  ```sql
  UPDATE sequencecounter SET CounterVal=LAST_INSERT_ID(CounterVal+1) WHERE CounterName='chatmsg';
  SELECT LAST_INSERT_ID();
  ```
  Then insert the message. The unique key on `chatmsg.SeqCount` catches any collision; on a collision, retry. Create the counter row at conversion if it's missing. The office DB's `sequencecounter` has `AUTO_INCREMENT=2`, so one row already exists; keep it.
- **Event rows**: some `chatmsg` rows are events, not messages (`EventType` other than 0), so clients learn about changes from the same poll. For most event types only the newest row matters, and older rows of that type are deleted when a new one is written. "Users changed" events are kept because they display inline. The numeric `EventType` values are **not documented publicly**. **VERIFY** on the restored DB with `SELECT EventType, COUNT(*), MIN(Message) FROM chatmsg GROUP BY EventType;`, then pin our enum to those values.
- **Message format**: when `EventType=0`, `Message` holds a **WPF XAML FlowDocument** string. Event messages hold plain text. To read the office's existing chat history and stay compatible, our client must read and write the same XAML format. That favors a WPF client: the repo already builds WPF in `WpfControlsOD/` and `OpenDentalWpf/`.
- **Read state**: on every new message, set `chatuserattach.IsRead=0` for all members. Set it back to 1 for a member when they view the latest message. Adding a member needs no read update.
- **Removal**: deleting the `chatuserattach` row removes membership entirely. "Left but still listed" is expressed by setting `DateTimeRemoved`. Messages after that time are ignored for sorting, hidden, and never notified.
- **Read-only replica**: if a read-only server is configured (`ReadOnlyServer...` preferences), send the frequent poll reads there and all writes to the main DB.
- **Dedicated chat database**: an office can move the chat tables into their own database and point the `ChatServer...` preferences at it. If those preferences are blank, use the main database.
- **Sign-on**: the chat process needs its own DB connection and user identity.
  - When launched from the main program, pass the current user. Use a short-lived handoff, for example a named pipe or a one-time token row; don't put a password on the command line.
  - When auto-started, show the normal log-on (domain log-on if enabled) or remember the last Windows user per machine.
  - Never store a plaintext password.
- Not supported: Open Dental's mobile relay service and mobile app (excluded with ODMobile).

**Permissions and security log**

- The 25.3 permission enum gains no chat permission (the only new values are 252 rename and 272 HQ). So any logged-on user may chat, as in Open Dental.
- No audit entries are documented. Optional for us: log deletion of a chat or message under an existing admin permission.

**Preferences**

- `ChatServer...` (the prefix is confirmed by the DB document, the full names are not). Proposed set: `ChatServerName`, `ChatServerDatabase`, `ChatServerUser`, `ChatServerPassHash`, `ChatServerSslCa`. **VERIFY** the names on the restored DB.
- Per-user settings live in `chatuserod`, not in preferences. Per-machine settings (auto-start, start in background) live in `chatuserod` too (`OpenBackground`, `CloseKeepRunning`) plus the Windows Run registry key for auto-start.

**Schema** (DDL taken verbatim from the office dump; see `docs/specs/schema-changes-24.3-to-26.1.md`, section 25.3)

- `chat` (`ChatNum` bigint PK, `Name` varchar(255) NOT NULL)
- `chatmsg` (`ChatMsgNum`, `ChatNum`, `UserNum`, `DateTimeSent` datetime default '0001-01-01 00:00:00', `Message` text, `SeqCount` bigint unsigned UNIQUE, `Quote` bigint, `EventType` tinyint(4), `IsImportant` tinyint(4)), plus indexes on UserNum, ChatNum, DateTimeSent, (ChatNum,SeqCount), Quote
- `chatattach` (`ChatAttachNum`, `ChatMsgNum`, `FileName` varchar(255), `Thumbnail` mediumblob, `FileData` mediumblob)
- `chatreaction` (`ChatReactionNum`, `ChatMsgNum`, `UserNum`, `EmojiName` varchar(255))
- `chatuserattach` (`ChatUserAttachNum`, `UserNum`, `ChatNum`, `IsRead` tinyint, `DateTimeRemoved` datetime default '0001-01-01 00:00:00', `IsMute` tinyint), plus indexes on UserNum, ChatNum, (UserNum,ChatNum)
- `chatuserod` (`ChatUserodNum`, `UserNum`, `UserStatus` tinyint (enum `EnumUserStatus`: Available, Away, DoNotDisturb. The document gives names but no numbers, so 0/1/2 in that order is an assumption; **VERIFY** with `SELECT UserStatus, COUNT(*) FROM chatuserod GROUP BY UserStatus;` on the restored DB), `DateTimeStatusReset` datetime, `Photo` text (base64, at most 65k), `PhotoCrop` varchar(255), `OpenBackground`, `CloseKeepRunning`, `MuteNotifications` tinyint, `DismissNotifySecs` int, `MuteImportantNotifications` tinyint, `DismissImportantNotifySecs` int). One row per user, created the first time it's needed.
- `sequencecounter` (`CounterNum`, `CounterName` varchar(255), `CounterVal` bigint, plus an index on `CounterVal`). The office dump has **no** unique key on `CounterName`; uniqueness is by convention only, so the conversion must not add one.
- All tables are MyISAM, utf8mb3.

**Codebase touchpoints**

- New executable project, for example `OpenDentalChat/` (WPF), added to `OpenDental.sln`. It references `OpenDentBusiness/`, `DataConnectionBase/` and `WpfControlsOD/`.
- New table classes `OpenDentBusiness/TableTypes/Chat.cs`, `ChatMsg.cs`, `ChatAttach.cs`, `ChatReaction.cs`, `ChatUserAttach.cs`, `ChatUserod.cs`, `SequenceCounter.cs`. New data-interface classes in `OpenDentBusiness/Data Interface/`.
- `OpenDental/Main Modules/ControlManage.cs` (Chat button), `OpenDental/User Controls/Preferences/UserControlServerConnections.cs`, `OpenDentBusiness/TableTypes/Pref.cs`.
- Presence from the time clock: `OpenDentBusiness/TableTypes/Employee.cs` (`ClockStatus`), `OpenDentBusiness/Data Interface/Employees.cs`, `OpenDentBusiness/TableTypes/Userod.cs` (`EmployeeNum`).
- **Name clash**: `OpenDentBusiness/InternalTools/Phones/TableTypes/ChatUser.cs` is Open Dental HQ's unrelated web-chat staff table (`chatuser`), slated for deletion in decoupling Phase 3. Don't reuse it.

---

### v25_3-procs-cdt-2026: 2026 CDT codes

- **Category:** desktop. **Size:** S (plus the import design owned by the decoupling plan). **Depends on:** decoupling Phase 2, the CDT replacement (`docs/decoupling-plan.md`).
- **Sources:** https://www.opendental.com/site/version25_3.html (Major Changes; also backported to 25.2.62), https://opendental.blog/version-25-3/ (available from 25.3.35; the blog gives 25.2.64 for the 25.2 line, which differs from the release-note page's 25.2.62). Either way, the office's 25.3.48 has them.

**Behavior.** In Open Dental, Procedure Code Tools adds any 2026 D-codes that are missing and updates descriptions. The text comes from the proprietary CDT library. Our project rule 4 forbids shipping ADA code text. So:

- Procedure Code Tools keeps a "D-codes" option that **imports a user-supplied, licensed code file** (format to be decided in the CDT replacement work). It adds missing codes, updates descriptions of existing ones, and moves deleted codes to an "Obsolete" category instead of deleting them. A code that's in use is never deleted.
- Non-text metadata for new 2026 codes can ship in our own mapping file: procedure category, treatment area, paint type, default time pattern, whether it's a "D" diagnostic code. Codes are numbers and aren't copyrightable text; descriptions are not shipped.
- **Office migration:** the office DB already holds whatever codes its 25.3.48 install added. Nothing is needed for adoption. This item is for new installs and future years.

**UI:** `OpenDental/Forms/FormProcTools.cs` (the `checkDcodes` option and `butRun_Click`, with date-window validation around year end).
**Permissions/log:** unchanged (Setup permission; a log entry for the tool run already exists).
**Preferences:** none. **Schema:** none.
**Touchpoints:** `OpenDental/Forms/FormProcTools.cs`, `OpenDentBusiness/Data Interface/ProcedureCodes.cs`, `OpenDental/Forms/FormProcCodes2.cs` (existing XML import).

---

## Account module

### v25_3-account-orthocase-cleanup: Ortho Case window, remove excess text

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.38). Baseline window: https://www.opendental.com/manual243/orthocase.html.

**Behavior.** Cosmetic. Redundant instruction text is removed from the Ortho Case window. Likely candidates in our window are the static labels, such as the note that banding/visit/debond codes are set in preferences (`label3`) and the stand-alone "or" label (`label1`). **VERIFY** exactly which labels Open Dental removed by opening an ortho case on the office's 25.3.48. Move any helpful text to a tooltip instead of dropping it.
**UI:** `OpenDental/Forms/FormOrthoCase.cs` and `FormOrthoCase.designer.cs`.
**Rules / permissions / preferences / schema:** none.

### v25_3-security-inswriteoffedit-clarify: Clarify the Insurance Write-off Edit permission

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.38), https://www.opendental.com/OpenDentalDocumentation25-3.xml (`EnumPermType.InsWriteOffEdit`, value 119).

**Behavior.** The permission's description now spells out what it already enforces:

- editing the Write-off and Write-off Override fields of claim procedures
- **creating** a claimproc, so an existing write-off can't be worked around
- **deleting** a claimproc, so the date restriction can't be dodged by deleting an old row and adding a new one

It uses the date or days restriction from the procedure's `DateEntryC`, or the claimproc's `SecDateEntry` for total payments, across all plan types.
**UI:** the permission's text in Security, User Group permissions tree. Our wording: "Insurance Write-off Edit (also blocks adding or deleting claim procedures)".
**Rules:** check that 24.3's create and delete paths really call this permission with the date. If any don't, fix them.
**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (the `[Description]` near line 401), `OpenDental/User Controls/UserControlSecurityTree.cs`, `OpenDental/Forms/FormClaimProc.cs`, `OpenDentBusiness/Data Interface/ClaimProcs.cs`.
**Schema/preferences:** none. The enum value is unchanged.

### v25_3-account-payplan-bypass-globallock: Payment plans can ignore the global lock date

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.44), https://opendental.blog/version-25-3/. Current lock behavior: https://www.opendental.com/manual243/securitylockdates.html, and help254 `paymentplandynamic.html` (search excerpt: under a global lock, the plan's Locked checkbox becomes unavailable even with Pay Plan Unlock).

**Behavior.** A new yes/no preference, default off. When on, the **global** lock date and lock days no longer block editing, deleting, closing or signing payment plans whose date falls in the locked period.

- User-group lock dates still apply.
- The plan's own Locked checkbox and the Pay Plan Unlock permission still apply.
- Other locked items (payments, adjustments, procedures) stay locked.

**UI:** a checkbox in the Global Lock Date window (Setup, Security, Lock Date), next to "Lock includes administrators". Our label: "Payment plans are not affected by the global lock".
**Rules:**

- `Security.IsGlobalDateLock` returns false for `EnumPermType.PayPlanEdit` when the preference is on. Also cover any pay-plan charge permissions that route through it. `Security.IsAuthorized` calls `IsGlobalDateLock` too (line 219), so dated `IsAuthorized(PayPlanEdit,…)` checks follow automatically.
- The direct callers need no change:
  - `FormPayPlan.cs`: 4 places (lines 529, 577, 926, 1344)
  - `FormPayPlanDynamic.cs`: line 1470
  - `FormPayPlanCredits.cs`: lines 218 and 251
  - `OpenDentBusiness/Db Multi Table/PayPlanEdit.cs`: line 1996

  Test each one with the preference on and off.

**Audit:** log the preference change the way `FormSecurityLock.cs` already logs lock changes. That is one `SecurityGlobal` entry (line 98), built from "changed from X to Y" lines (for example line 57).
**Preferences:** proposed `SecurityLockExcludesPayPlans` (bool, default 0). **VERIFY** Open Dental's name on the restored DB.
**Touchpoints:** `OpenDentBusiness/Db Multi Table/Security.cs` (`IsGlobalDateLock`, line 273), `OpenDental/Forms/FormSecurityLock.cs`, `OpenDental/Forms/FormPayPlan.cs`, `OpenDental/Forms/FormPayPlanDynamic.cs`, `OpenDental/Forms/FormPayPlanCredits.cs`, `OpenDentBusiness/Db Multi Table/PayPlanEdit.cs`, `OpenDentBusiness/TableTypes/Pref.cs`.
**Schema:** none.

### v25_3-account-paysplit-payplan-attach: Attach paysplits to payment plans in more cases

- **Category:** desktop. **Size:** M. **Depends on:** v25_2-account-payplan-prepay-attach, the 25.2 change that lets the "Payment Plan Prepay" unearned type attach (https://www.opendental.com/site/version25_2.html).
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.55).

**Behavior.** Public material names no specific cases. In 24.3, `FormPaySplitEdit.checkPayPlan_Click` refuses to attach a split to a payment plan when:

1. "Is from another family" is checked
2. the split carries treatment-planned prepayment (unearned type on a TP procedure)
3. the split's patient is neither the patient nor the guarantor of any patient payment plan
4. the plan is an insurance payment plan

25.3 relaxes some of these. **VERIFY** on 25.3.48 with this test matrix: run each case in the payment window and in Edit Paysplit, and record allowed or blocked:

- a split for a family member who is the plan's patient but not the guarantor
- a split from another family where the plan's guarantor is in the paying family
- a split with the Payment Plan Prepay unearned type and no procedure
- a split on an adjustment that is a plan credit
- a split attached to a procedure that is a credit on a dynamic plan

Implement exactly the cases 25.3.48 allows. Keep rule 2 and rule 4 unless the test shows otherwise. Keep the existing "Treatment planned unearned cannot be applied" check.

**UI:** `OpenDental/Forms/FormPaySplitEdit.cs` (`checkPayPlan_Click`, `IsValid`), `OpenDental/Forms/FormPayment.cs` (split validation near line 2906), `OpenDental/Forms/FormPayPlanSelect.cs`.
**Rules:** after attaching, the existing "overpaying interest or principal" warning in `FormPayment.cs` must still run.
**Audit:** PaymentEdit entries already record split changes. Add the pay plan number to the change text.
**Touchpoints:** the forms above, plus `OpenDentBusiness/Data Interface/PayPlans.cs` (`GetForPatNum` returns plans where the patient is either the patient or the guarantor) and `OpenDentBusiness/Data Interface/PaySplits.cs`.
**Preferences/schema:** none.

---

## Appointments module

### v25_3-appts-timebar-secondary-color: Secondary color in the time bar when no provider time is set

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html. Baseline: https://www.opendental.com/manual243/timebars.html.

**Behavior.**

- Today the provider time bar on the left edge of an appointment colors only the rows marked as provider time: `X` in `Pattern` for the main provider, `X` in `PatternSecondary` for the secondary.
- If an appointment has no `X` in either pattern, the bar is empty.
- New preference, default off: in that case, fill the whole bar with the **secondary provider's color**. That's the hygienist when the appointment isn't flagged hygiene, otherwise the dentist.

**UI:** a checkbox in Preferences, Appts, Appearance. Our label: "If neither provider has time set, show the secondary provider color in the time bar".
**Rules:**

- Only when both patterns contain no `X`.
- If there's no secondary provider, leave the bar as today.
- Grayscale printing uses the desaturated color, as the existing code does.
- Don't change appointment length or any production numbers.

**Preferences:** proposed `ApptTimeBarSecondaryColorNoProvTime` (bool, default 0). **VERIFY** the name.
**Touchpoints:** `OpenDental/UI/Appt/ControlApptPanel.cs` (`DrawOneAppt`, line 3236; its time-bar loop is around lines 3384 to 3410, `pattern2Showing`), `OpenDental/User Controls/Preferences/UserControlApptAppearance.cs`. The same file's `ProvBarShading` (line 5018) shades the per-provider columns at the left of the schedule from the same two patterns. **VERIFY** on 25.3.48 whether those columns also follow the new preference; if they don't, leave `ProvBarShading` alone.
**Schema:** none.

### v25_3-appts-audit-detail: More detail when appointments are moved or edited

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://opendental.blog/version-25-3/. Baseline: https://www.opendental.com/manual243/audittrail.html.

**Behavior.** Audit entries for appointment moves and edits now say what changed. Today a move logs only the procedure description with the old and new date/time.

**Rules (our design; VERIFY the wording on 25.3.48)**

- **Move entries** (`AppointmentMove`) for drag, pinboard drop, Move-to and Unschedule should state:
  - old and new date and time
  - old and new operatory (abbreviation)
  - old and new provider and hygienist, if a drag into another op re-assigned them
  - for an unschedule, the old status and slot
- **Edit entries** (`AppointmentEdit`) from Edit Appointment should list each changed field as "field: old → new". Cover:
  - providers: `ProvNum`, `ProvHyg`, `Assistant`
  - placement and timing: `Op`, `AptDateTime`, length (pattern length in minutes)
  - status and flags: `AptStatus`, `Confirmed`, `Priority`, `IsHygiene`, `IsNewPatient`, `IsMirrored` (see mirroring)
  - type and location: `AppointmentTypeNum`, `ClinicNum`
  - attached procedures (added or removed codes)
  - the note: say only that it changed, not the text
- Keep entries under the 4,000-character log limit, truncating with "…".

**Permissions:** none new. Existing `AppointmentMove`, `AppointmentEdit` and `AppointmentCreate` entries carry the detail.
**Touchpoints:**

- `OpenDental/Main Modules/ControlAppt.cs` (pinboard drop logs around lines 1755 to 1790)
- `OpenDental/UI/Appt/ControlApptPanel.cs` (drag and resize; the resize writes an `AppointmentEdit` entry at line 1191, while the drag-move entry itself is written in `Appointments.cs`, below)
- `OpenDental/Forms/FormApptEdit.cs` (create entry at line 1327, move entry at line 2745, edit entry at line 2815)
- `OpenDental/Logic/AppointmentL.cs` (around line 383)
- `OpenDental/Forms/FormApptsOther.cs` (around line 486)
- `OpenDentBusiness/Data Interface/Appointments.cs` (`TryMoveAppointment`, line 4398, writes the drag/drop move entry at line 4614)
- New helper `Appointments.GetChangeSummary(old,new)` in the same file

**Preferences/schema:** none.

### v25_3-security-apptcreate-pinboard: Appointment Create also covers scheduling a planned appointment from the pinboard

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.40).

**Behavior.**

- The permission's display name now says it also governs dropping a **planned** appointment from the pinboard onto the schedule. Our wording: "Appointment Create (also covers scheduling a planned appointment from the pinboard)".
- 24.3 already checks `AppointmentCreate` in `pinBoard_ApptMovedFromPinboard` for planned appointments.
- Make every path that turns a planned appointment into a scheduled one require `AppointmentCreate`, and never `AppointmentMove`. Paths: pinboard drop, "Schedule" from the Planned Appointment Tracker, and Other Appointments.

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (Description, line 112), `OpenDental/Main Modules/ControlAppt.cs` (line 1471), `OpenDental/Forms/FormApptsOther.cs`, `OpenDental/User Controls/UserControlSecurityTree.cs`.
**Schema/preferences:** none.

### v25_3-security-apptmove-scope: Appointment Move applies to all statuses, but not planned-to-pinboard

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.40).

**Behavior.** New display text, our wording: "Appointment Move (drag, send to pinboard, or set unscheduled, for any status; not needed to move from the Unscheduled List or to pin a planned appointment)". Behavior changes:

1. **Any status.**
   - Setting a **complete** appointment unscheduled now needs `AppointmentMove` **and** `AppointmentCompleteEdit`. Today `AppointmentL.ValidateApptUnsched` checks only `AppointmentCompleteEdit` for complete appointments.
   - Patient-note appointments are already covered.
2. **Planned appointments to the pinboard** no longer need `AppointmentMove`. A planned appointment is copied, not moved.
   - Remove the check from `ControlChart.butPin_Click` (Planned Appointments grid, line 3081).
   - Remove it from planned rows in `FormApptsOther.butPin_Click` (line 534).
   - Keep the patient-restriction and archived/deceased checks.
3. Moving from the Unscheduled List stays unblocked, as today.

**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (line 115), `OpenDental/Logic/AppointmentL.cs` (`ValidateApptToPinboard`, `ValidateApptUnsched`), `OpenDental/Main Modules/ControlChart.cs`, `OpenDental/Forms/FormApptsOther.cs`, `OpenDental/UI/Appt/ControlApptPanel.cs` (lines 1205 and 1250).
**Audit:** unchanged entries; detail comes from v25_3-appts-audit-detail.
**Schema/preferences:** none.

### v25_3-appts-mirroring: Appointment mirroring

- **Category:** desktop. **Size:** L. **Depends on:** v25_3-appts-audit-detail (to log IsMirrored changes).
- **Sources:**
  - https://www.opendental.com/site/version25_3.html and https://opendental.blog/version-25-3/
  - https://www.opendental.com/site/0_appointments.html
  - https://www.opendental.com/OpenDentalDocumentation25-3.xml and https://www.opendental.com/OpenDentalDiffDocumentation25-2.xml (`appointment.IsMirrored`, `apptview.ShowMirroredAppts`, `histappointment.IsMirrored`, preference `ApptMirrorSecondary`)
  - https://www.opendental.com/site/apiappointments.html (IsMirrored)
  - https://opendental.com/help254/overviewapptmirror.html (login-gated; search excerpt only)
- Note: the columns first appear in the 25.2 schema document, but the feature ships in 25.3. Implement it once, here.

**User-visible behavior**

- When a dentist and a hygienist see the same patient in one appointment, the appointment can also appear as a **mirror** in the **secondary provider's operatory**. A typical use: a doctor exam during a cleaning. No second appointment is created.
- The mirror covers only the **secondary provider's time** (the `X` rows of `PatternSecondary`), so it's usually shorter than the original.
- The mirror is derived, not stored, so any change to the original shows on the mirror at once.

**UI elements**

- **Edit Appointment** (`FormApptEdit`): a **Mirror** checkbox bound to `IsMirrored`. New appointments get the default from preference `ApptMirrorSecondary`.
- **Appointment View Edit** (`FormApptViewEdit`): a **Show mirrored appointments** checkbox bound to `ApptView.ShowMirroredAppts`, default off.
- **Preferences, Appts**: a checkbox for `ApptMirrorSecondary`. Our label: "New appointments mirror into the secondary provider's operatory".
- Appointments module: the mirror draws in the secondary op with a distinct style (hatched or semi-transparent fill, a small "mirror" marker), patient name and procedures as the view defines.

**Business rules and edge cases**

- **Which provider is secondary.** If the appointment is flagged `IsHygiene`, the hygienist is primary and the dentist (`ProvNum`) is secondary. Otherwise the dentist is primary and the hygienist (`ProvHyg`) is secondary. This matches the meaning of `PatternSecondary` in the DB documentation.
- **No mirror at all** if:
  - `IsMirrored=0`
  - the view has `ShowMirroredAppts=0`
  - there's no secondary provider
  - `PatternSecondary` has no `X`
  - the appointment isn't on the schedule (planned, unscheduled list, broken-and-unscheduled)
- **Secondary operatory.**
  1. First, the op the secondary provider is scheduled into that day (a provider schedule block assigned to an op).
  2. Otherwise, the op whose `ProvDentist` (for a dentist) or `ProvHygienist` (for a hygienist) is that provider.
  3. Pick only among ops visible in the current view. If several match, take the lowest `ItemOrder`.
  4. If the result is the appointment's own op, show no mirror.

  The search excerpt confirms that each provider must be assigned to an operatory for this to work. It also says the view must be set to show mirrored appointments, and that the mirror spans the provider time selected for the secondary provider. The 25.2 diff document adds that toggling the hygiene flag swaps which provider is secondary, which changes the mirror's color and length.
- **Position**: from the first `X` to the last `X` of `PatternSecondary`, offset from `AptDateTime`. If the `X` runs are not contiguous, draw one block per run.
- **Interaction**:
  - A single click selects the original appointment.
  - A double click opens the original's Edit Appointment.
  - The mirror **can't be dragged**, resized, cut or copied. Right-click shows the original's menu, with move actions disabled.
  - The appointment bubble shows the original.
- **Counts**: mirrors don't count toward production totals, op appointment counts, the waiting room or confirmation lists. They don't take part in the overlap test for the secondary op's own appointments; the secondary-provider double-booking rules are unchanged.
- **Week view and printing**: drawn like day view. Printed schedules include mirrors with the same style.
- **History**: `histappointment.IsMirrored` copies the value whenever a history row is written.
- **External**: eServices and the API are excluded, so nothing else reads the flag.

**Permissions and audit:** changing the Mirror checkbox needs the usual `AppointmentEdit`. Log the change as an `AppointmentEdit` detail line, "Mirror: off → on".

**Preferences:** `ApptMirrorSecondary` (bool, default 0; name confirmed by the DB documentation).

**Schema:**

| Column | Type (office dump) |
|---|---|
| `appointment.IsMirrored` | `tinyint(4) NOT NULL` |
| `histappointment.IsMirrored` | `tinyint(4) NOT NULL` |
| `apptview.ShowMirroredAppts` | `tinyint(4) NOT NULL` |

**Touchpoints:**

- Table classes: `OpenDentBusiness/TableTypes/Appointment.cs`, `HistAppointment.cs`, `ApptView.cs`
- Data: `OpenDentBusiness/Data Interface/Appointments.cs` (`GetPeriodApptsTable`, line 653: add `IsMirrored`, `PatternSecondary`, `IsHygiene` and the provider columns to the period table), `OpenDentBusiness/Data Interface/HistAppointments.cs`, `OpenDentBusiness/Data Interface/ApptViews.cs`
- UI: `OpenDental/UI/Appt/ControlApptPanel.cs` (layout and drawing of mirror blocks; hit-testing that maps a mirror back to its `AptNum`), `OpenDental/Main Modules/ControlAppt.cs`, `OpenDental/Forms/FormApptEdit.cs`, `OpenDental/Forms/FormApptViewEdit.cs`, `OpenDental/User Controls/Preferences/UserControlApptGeneral.cs`

---

## Family module

### v25_3-family-otherbenefits-grid: Edit Other Benefits directly in the grid

- **Category:** desktop. **Size:** M. **Depends on:** v25_1-family-codegroups-hide-delete (adds `codegroup.ShowInFrequency` and gives `IsHidden` its new "retired" meaning) and v25_3-family-exclusions-tooth (for the Teeth column).
- **Sources:** https://www.opendental.com/site/version25_3.html, https://opendental.blog/version-25-3/, https://www.opendental.com/site/0_insbenefits.html, https://www.opendental.com/OpenDentalDocumentation25-3.xml (`codegroup.ShowInOther`). Baseline: https://www.opendental.com/manual243/otherbenefits.html, https://www.opendental.com/manual243/editbenefit.html.

**Behavior**

- In the Edit Benefits window, rows of the **Other Benefits** grid can be changed **in place** instead of opening Edit Benefit for each one. Double-clicking or the Edit button still opens the full Edit Benefit window.
- Code groups flagged **Show in Other Benefits** appear as pre-filled rows, the same way the Age Limitations grid lists code groups flagged `ShowInAgeLimit`.
- Typing values into such a row creates the benefit. Clearing them removes it on save.

**UI elements**

- Grid: `gridBenefits` in `FormInsBenefits`, titled "Other Benefits" in Simplified View. Columns, editable in place:

  | Column | Editor |
  |---|---|
  | Pat | checkbox, patient override |
  | Level | combo: None/Individual/Family |
  | Type | combo: `InsBenefitType` |
  | Category / Code / Code Group | read-only for code-group rows; otherwise a picker cell |
  | % | text |
  | Amt | text |
  | Time Period | combo |
  | Quantity | number plus a qualifier combo |
  | Treat Area | combo, frequency rows only |
  | Teeth | text, for exclusions |

  Buttons Add, Edit, Delete.
- Code Group Edit (`FormCodeGroupEdit`): a new **Show in Other Benefits** checkbox. The Code Groups list (`FormCodeGroups`) gets a matching column.

**Rules and edge cases**

- On leaving a cell, validate it with the same rules Edit Benefit uses:
  - Percent is 0 to 100, and only for CoInsurance.
  - Amount applies to limitations, deductibles and copays.
  - Quantity is 0 to 255 and needs a qualifier.
  - Treat Area applies only to frequency limitations.
- An invalid cell turns red, and OK is blocked until it's fixed.
- A code-group row left completely blank isn't saved. Hidden code groups never appear. This uses the 25.1 meaning of `IsHidden` (retired). In our 24.3 code, `IsHidden` still means "not shown in the frequency grid" (`CodeGroup.cs` line 21, `FormCodeGroupEdit.cs` line 25), so this rule needs the 25.1 item first.
- Row View (Simplified View unchecked) gets the same in-place editing on its single Benefits grid.
- Saving still goes through the existing compare-and-sync on OK, so cancel discards edits.

**Permissions/audit:** unchanged. Insurance plan edits already log through `InsPlanEdit` and the ins-edit logs.
**Preferences:** none.
**Schema:** `codegroup.ShowInOther tinyint(4) NOT NULL` (office dump; default 0 at conversion).
**Touchpoints:** `OpenDental/Forms/FormInsBenefits.cs` (`FillGridBenefits`, around line 630; `FillSimple`), `OpenDental/Forms/FormBenefitEdit.cs`, `OpenDentBusiness/TableTypes/CodeGroup.cs`, `OpenDentBusiness/Data Interface/CodeGroups.cs`, `OpenDental/Forms/FormCodeGroupEdit.cs`, `OpenDental/Forms/FormCodeGroups.cs`, `OpenDentBusiness/UI/GridOD.cs` (editable cells and combo cells), `OpenDentBusiness/Data Interface/Benefits.cs`.

### v25_3-family-insplans-innet: In Net column in the Insurance Plans list

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://opendental.blog/version-25-3/. Baseline: https://www.opendental.com/manual243/insplanlist.html.

**Behavior.** Lists, Insurance Plans gets an **In Net** column. It's marked for plans whose type counts as in network:

| Plan type | `insplan.PlanType` | In network |
|---|---|---|
| PPO Percentage | `p` | yes |
| PPO Fixed Benefit | `p` with a fixed-benefit fee schedule | yes |
| Medicaid or Flat Co-pay | `f` | yes |
| Capitation | `c` | yes |
| Category Percentage | blank | no |

**Rules:** display only; it's sortable. No filter is required.
**Touchpoints:** `OpenDental/Forms/FormInsPlans.cs` (columns from line 69), `OpenDentBusiness/Data Interface/InsPlans.cs` (`GetBigList`, line 673: add `PlanType` to the SELECT and the table).
**Schema/preferences/permissions:** none.

### v25_3-family-exclusions-tooth: Insurance exclusions by tooth number

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:**
  - https://www.opendental.com/site/version25_3.html and https://opendental.blog/version-25-3/
  - https://www.opendental.com/site/version25_4.html (the teeth entry for exclusions was backported to 25.3.39, so it's in the office's 25.3.48). The 26.1 spec calls this work `v25_4-family-benefit-tooth-exclusions`; implement it once, here.
  - https://www.opendental.com/OpenDentalDocumentation25-3.xml (`benefit.ToothRange`) and https://www.opendental.com/site/apibenefits.html
  - follow-up https://www.opendental.com/site/version26_1.html (age limits by tooth arrive in 26.1)

**Behavior.** An Exclusions benefit can be limited to specific teeth. Example: a porcelain crown code isn't covered on teeth 2 and 15. Procedures on other teeth are estimated normally.

**UI:**

- Edit Benefit (`FormBenefitEdit`): a **Teeth** text box, enabled only when Type is **Exclusions**. Input uses the office's tooth nomenclature, with commas and hyphen ranges, for example "2,15" or "23-26,7-10".
- The Benefits/Other Benefits grids get a Teeth column.
- The benefit description text gets a "teeth …" suffix.

**Rules and edge cases**

- **Storage**: internal tooth numbers, comma-separated, ranges expanded, with no hyphens in the database. Use `Tooth.ParseRange` to store and `Tooth.DisplayRange` to show (`OpenDentBusiness/Logic/Tooth.cs`). Validate each tooth with `Tooth.IsValidEntry`. Primary teeth letters and supernumerary numbers are allowed.
- **Blank or NULL** means the exclusion applies to every tooth. That's the existing behavior and keeps old data valid. The column is nullable in the office dump, so treat NULL as blank.
- **Matching**:
  - A procedure is excluded only if its `ToothNum` is in the set.
  - For procedures that carry a tooth range (bridges, partials), it's excluded if **any** tooth in the range is in the set. **VERIFY** on 25.3.48.
  - A procedure with no tooth (mouth, quadrant, arch) isn't excluded by a tooth-limited row.
- Applies to exclusion rows defined by procedure code, category or code group.
- Changing teeth on an exclusion must re-run estimates the same way any benefit change does: claimprocs on unsent claims update, sent claims need a recalc.
- **Not in 25.3**: `ToothRange` on age limitations. The 25.3 DB document already mentions it, but the UI arrives in 26.1. Keep the parsing code shared.

**Permissions/audit:** unchanged (insurance plan edit logging). Include the teeth in the benefit-change log text.
**Preferences:** none (the existing "exclusions use UCR fee" preference still governs write-offs).
**Schema:** `benefit.ToothRange varchar(255) DEFAULT NULL` (office dump; note it's nullable, unlike most columns).
**Touchpoints:**

- `OpenDentBusiness/TableTypes/Benefit.cs`
- `OpenDentBusiness/Data Interface/Benefits.cs` (`IsExcluded`, line 1762: add a tooth parameter)
- callers in `OpenDentBusiness/Data Interface/ClaimProcs.cs` (lines 2057, 2227, 2262) and `OpenDentBusiness/Data Interface/Procedures.cs` (lines 880, 3087)
- `OpenDental/Forms/FormBenefitEdit.cs`, `OpenDental/Forms/FormInsBenefits.cs`, `OpenDentBusiness/Logic/Tooth.cs`

---

## Manage module

### v25_3-manage-timeclock-reportsto: Reports To filter for the time clock

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html. Baseline: https://www.opendental.com/manual243/timeclock.html, https://www.opendental.com/manual243/employees.html.

**Behavior.** The Manage module's employee time-clock grid gets a **Reports To** filter. Choose a supervisor to see only the employees who report to that person. `employee.ReportsTo` already exists in 24.3 and is set in Edit Employee.
**UI:** a combo above the employee grid, next to the existing first-name filter. Options are "All", then every employee that at least one other employee reports to.
**Rules:**

- It filters on direct reports only. **VERIFY** whether 25.3.48 also lists the supervisor and indirect reports.
- It combines with the clinic and name filters.
- The selection is remembered per user for the session. Optionally persist it with a user-preference row.
- If the selected supervisor becomes hidden, fall back to "All".

**Touchpoints:** `OpenDental/Main Modules/ControlManage.cs` (`FillEmps`, line 786) and `ControlManage.designer.cs`, `OpenDentBusiness/Data Interface/Employees.cs`, `OpenDentBusiness/TableTypes/Employee.cs`. Consider the same filter in `OpenDental/Forms/FormTimeCardManage.cs` (**VERIFY**).
**Schema/preferences/permissions:** none.

### v25_3-manage-accounting-checknum: Quick "next check number" button

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html. Baseline: https://www.opendental.com/manual243/transactionhistory.html.

**Behavior.** In Edit Transaction, a button beside **Check Number** fills in the next check number for the account.
**Rules (our design; VERIFY):**

- Next number = 1 + the highest purely numeric `journalentry.CheckNumber` among entries of the same asset account (the account being viewed in Transaction History). If there are none, use 1001 and select the text so the user can overwrite it.
- Non-numeric check numbers ("DEP", "AW") are ignored.
- The button is visible only when the account is an Asset account.

**UI:** `OpenDental/Forms/FormTransactionEdit.cs` (next to `textCheckNumber`).
**Touchpoints:** `OpenDental/Forms/FormTransactionEdit.cs`, `OpenDentBusiness/Data Interface/JournalEntries.cs` (new `GetNextCheckNumber(accountNum)`).
**Schema/preferences/permissions:** none (saving still needs AccountingEdit or AccountingCreate).

### v25_3-manage-accounting-payee: Payee and Notes on accounting transactions

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://www.opendental.com/OpenDentalDocumentation25-3.xml (`journalentry.Payee`, `journalentry.Notes`, both described as split out of Memo).

**Behavior.** Transactions get a **Payee** (who was paid) and longer **Notes**, so these no longer have to be squeezed into Memo.

**UI**

- Edit Transaction, simple view: **Payee** (single line, max 255) and **Notes** (multi-line). Both apply to every journal entry of the transaction, just like Memo in simple view.
- Complex view: Edit Journal Entry gets Payee and Notes fields, with "same for all" following the existing Memo Same For All checkbox.
- Transaction History (`FormJournal`): a **Payee** column after Memo. Notes appear in a tooltip or a narrow column (our choice). **Find Text** also searches Payee and Notes.
- Exports include Payee.

**Rules:**

- No migration of old Memo text.
- Payee is optional, but check printing requires it (see v25_3-manage-accounting-printcheck).
- Max lengths: Payee 255, Notes 65,535.

**Audit:** the existing AccountingEdit/AccountingCreate entries include "Payee: old → new" when it changes.
**Schema:** `journalentry.Payee varchar(255) NOT NULL`, `journalentry.Notes text NOT NULL` (office dump).
**Touchpoints:** `OpenDentBusiness/TableTypes/JournalEntry.cs`, `OpenDentBusiness/Data Interface/JournalEntries.cs`, `OpenDental/Forms/FormTransactionEdit.cs`, `OpenDental/Forms/FormJournalEntryEdit.cs`, `OpenDental/Forms/FormJournal.cs` (columns from line 104).
**Preferences:** none.

### v25_3-manage-accounting-needsreview: Needs Review flag

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://www.opendental.com/OpenDentalDocumentation25-3.xml (`journalentry.NeedsReview`: its only effect is to show the row in red; `transaction.NeedsReview`: deprecated).

**Behavior.** Edit Transaction gets a **Needs Review** checkbox. Flagged transactions show **red** in Transaction History. There's no filter; offices that want a list use a query.
**Rules:**

- The checkbox writes `NeedsReview` on **every journal entry** of the transaction.
- On load it's checked if any entry has the flag.
- `transaction.NeedsReview` exists only for schema compatibility. Always write 0 and never read it.

**UI:** `OpenDental/Forms/FormTransactionEdit.cs` (checkbox), `OpenDental/Forms/FormJournal.cs` (row `ColorText` red).
**Audit:** include "Needs Review: off → on" in the AccountingEdit entry.
**Schema:** `journalentry.NeedsReview tinyint(4) NOT NULL`, `transaction.NeedsReview tinyint(4) NOT NULL` (office dump).
**Touchpoints:** the two forms above, plus `OpenDentBusiness/TableTypes/JournalEntry.cs`, `OpenDentBusiness/TableTypes/Transaction.cs`, `OpenDentBusiness/Data Interface/JournalEntries.cs`.

### v25_3-manage-accounting-printcheck: Print checks from accounting transactions

- **Category:** desktop. **Size:** L. **Depends on:** v25_3-manage-accounting-payee, v25_3-manage-accounting-checknum.
- **Sources:**
  - https://www.opendental.com/site/version25_3.html
  - https://www.opendental.com/OpenDentalDocumentation25-3.xml (`SheetTypeEnum.Check = 28`)
  - https://opendental.com/help254/sheetscheck.html (login-gated; search excerpt only)

  The excerpt says three things: check sheets are used when printing checks from a transaction; a layout can be made by choosing the Check type, duplicating a custom check, or copying the internal default; and blank check paper with an adjusted layout is recommended. It also says checks print from **asset** accounting transactions, which matches the release note.

**Behavior.** For **asset** accounts (checking accounts), the office can print a check straight from an accounting transaction onto check stock, using a customizable **Check** sheet layout.

**UI**

- Edit Transaction: a **Print Check** button, visible only when the account viewed is an Asset account and the transaction is in simple view with a single other account. For split transactions, see below.
- Setup, Sheets: **Check** appears in the sheet-type list. An internal default layout, "Internal Check", is added. Custom check sheets can be made from it.

**Check sheet fields** (our field names; **VERIFY** against the internal layout on 25.3.48)

| Field | Source |
|---|---|
| date | `DateDisplayed` |
| checkNumber | `CheckNumber` |
| payee | `Payee` |
| amount | numeric, with asterisks fill |
| amountInWords | for example "One thousand two hundred and 50/100" |
| memo | `Memo` |
| notes | `Notes` |
| accountName | the asset account's description |
| bankNumber | `account.BankNumber` |
| practiceTitle, practiceAddress, practiceCityStateZip | practice (or clinic) |

Static text, lines, rectangles and images (for example a signature image) work as on other sheets.

**Business rules and edge cases**

- Print Check requires a Payee, an amount above zero (credit to the asset account) and a check number. The quick button offers the next number.
- If the transaction is unsaved, save it first. That needs AccountingCreate or AccountingEdit and respects the accounting lock date.
- After printing, ask whether the check printed correctly.
  - If no, offer to reprint. The check number stays.
- **Split (complex) transactions**: print one check for the asset-account entry. The other splits list in an optional stub grid. **VERIFY** whether 25.3.48 supports splits; if not, disable the button for them.
- **MICR**: our `account` table has no routing-number column, so a full MICR line can't be built. Recommend pre-printed check stock. Optionally allow static text set in an E-13B font installed by the office.
- Printer: use the Default print situation unless a new "Checks" print situation is added. Route through `PrinterL` so the printer-options window (v25_3-misc-printer-options) applies.
- **Office data**: `sheetdef` rows with `SheetType=28` may already exist in `fox-db`. `SheetTypeEnum` must have `Check=28` before the office data is loaded. That part is covered by v25_3-schema-compat-columns.

**Permissions and audit:** printing writes the usual `Printing` audit entry ("Check #N printed, $X, Payee"), plus an AccountingEdit entry if the transaction was saved. No new permission (the 25.3 enum adds none).
**Preferences:** none required.
**Schema:** no new columns; `sheetdef.SheetType` gains value 28.
**Touchpoints:**

- Sheet framework: `OpenDentBusiness/SheetFramework/SheetEnums.cs` (`SheetTypeEnum`; also `SheetInternalType.InternalCheck`), `OpenDentBusiness/SheetFramework/SheetsInternal.cs` (map the type to its internal sheet), `OpenDentBusiness/SheetFramework/SheetFieldsAvailable.cs`, `OpenDentBusiness/SheetFramework/SheetFiller.cs`, `OpenDentBusiness/SheetFramework/SheetParameter.cs`, `OpenDentBusiness/Data Interface/Sheets.cs`
- Internal layout resource: new `OpenDentBusiness/Resources/InternalCheck.xml` and `OpenDentBusiness/Properties/Resources.resx`, following `Resources/DepositSlip.xml`
- UI and printing: `OpenDental/SheetFramework/SheetPrinting.cs`, `OpenDental/Forms/FormSheetDefs.cs`, `OpenDental/Forms/FormSheetDef.cs` (the sheet-type list for New), `OpenDental/Forms/FormSheetDefEdit.cs`, `OpenDental/Forms/FormTransactionEdit.cs`
- Accounts: `OpenDentBusiness/Data Interface/Accounts.cs`

### v25_3-manage-statement-email-lastname: Emailed statements include the patient's last name

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html.

**Behavior.** An emailed statement now identifies the patient by last name, so recipients managing several family accounts can tell them apart. Today the attachment is always named "Statement.pdf".

- Our design: name the attachment `Statement_<GuarantorLastName>.pdf`, with characters that are invalid in file names stripped.
- **VERIFY** on 25.3.48 whether the last name goes in the attachment name, the subject, or both. Use the same place.

**Rules:** use the statement's patient (the guarantor for family statements). Never put the first name, date of birth or account number in the file name.
**Touchpoints:** `OpenDentBusiness/Data Interface/Statements.cs` (line 613), `OpenDental/Forms/FormBilling.cs` (line 455), `OpenDental/Forms/FormStatementOptions.cs` (line 726).
**Schema/preferences/permissions:** none.

### v25_3-manage-statement-barcode: Inserter barcode on statements

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html.

**Behavior.** Offices that fold and stuff statements with an envelope-inserting machine can print a machine-readable barcode on each statement page. The inserter uses it to group the pages of one statement into one envelope.

**Our design (VERIFY Open Dental's symbology, content and placement on 25.3.48)**

- Option in Billing Defaults: **Print inserter barcode on statements**, off by default.
- The barcode is a **Special** field on Statement sheets. That needs no schema change, which matches the 25.3 DB document: no new sheet field type. Placing it on a custom statement layout sets its position. On the internal statement layout it defaults to the top-right margin.
- **Content**, one barcode per printed page:
  - a run sequence number for the statement within the billing batch
  - the page number
  - the total pages
  - an end-of-set flag on the last page

  Use Code 39, or DataMatrix for small footprints. `ZXing` is already referenced (see `OpenDental/Forms/FormMobileCode.cs`). That form belongs to the cut mobile-app feature, so keep the `ZXing` package reference when the form is deleted.
- Applies to printed and PDF statements. Emailed statements skip the barcode.

**Rules:** the barcode must keep a clear zone, so warn in the sheet editor if it overlaps other fields. A batch run restarts sequence numbers at 1.
**Preferences:** proposed `BillingStatementInserterBarcode` (bool, default 0). **VERIFY** the name. If 25.3 stores it per statement sheet instead, follow that.
**Touchpoints:** `OpenDental/Forms/FormBillingDefaults.cs`, `OpenDentBusiness/SheetFramework/SheetFieldsAvailable.cs`, `OpenDentBusiness/SheetFramework/SheetFiller.cs`, `OpenDental/SheetFramework/SheetPrinting.cs`, `OpenDental/Forms/FormBilling.cs`, `OpenDentBusiness/Data Interface/Statements.cs`.
**Schema:** none.

---

## Miscellaneous

### v25_3-chart-vitalsign-notes: Notes box in Edit Vital Signs

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.36), https://opendental.blog/version-25-3/, https://www.opendental.com/OpenDentalDocumentation25-3.xml (`vitalsign.Documentation` is now described as a general note in the window). Baseline: https://www.opendental.com/manual243/vitalsignsenter.html.

**Behavior.** Edit Vital Signs gets a multi-line **Notes** box for free text.
**Rules:**

- **No new column.** The box is bound to the existing `vitalsign.Documentation` text column. In 24.3 nothing in the edit window writes it (`FormVitalsignEdit2014.cs` has no reference to it); the 24.3 manual calls the matching grid column a leftover from 2011 certification. Any existing text shows up in the Notes box.
- The Medical window's vital-signs grid already shows this column as "Documentation for Followup or Ineligible" (`FormMedical.cs`, header at line 705, filled at line 729). Rename that header to "Notes" instead of adding a column.

**Touchpoints:** `OpenDental/Forms/FormVitalsignEdit2014.cs` and `FormVitalsignEdit2014.Designer.cs`, `OpenDental/Forms/FormMedical.cs` (opens the window at lines 736 and 746; vital-signs grid columns from about line 695), `OpenDentBusiness/TableTypes/Vitalsign.cs`.
**Schema/preferences/permissions:** none.

### v25_3-medical-medlist-missingtab: Remove the Missing Generic/Brand tab

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.37). Baseline: https://www.opendental.com/manual243/medicationmaster.html.

**Behavior.** The release note names the Edit Medication window. In our code, the tab ("Missing Generic/Brand", `tabMissing`) is in the **Medications** list window, `FormMedications`. The 24.3 manual agrees: the tab is shown only for US databases and lists medications that came in from the eRx bridge without a matching entry, with buttons to convert them. Remove that tab and its loading code (`FillGridMissing`, line 144, and the convert buttons). The "All Medications" list stays, and the tab control can go away.
**Touchpoints:** `OpenDental/Forms/FormMedications.cs`, `OpenDental/Forms/FormMedications.designer.cs`.
**Schema/preferences/permissions:** none.

### v25_3-tools-dbm-whitespace-tools: Move "Notes With Too Much White Space" to the Tools tab

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.41). Baseline: https://www.opendental.com/manual243/databasemaintenancetools.html.

**Behavior.** This method no longer runs with the regular Check/Fix pass. It becomes a row on the **Tools** tab that the user runs on purpose.
**Rules:**

- Remove `[DbmMethodAttr]` from `DatabaseMaintenances.NotesWithTooMuchWhiteSpace`, so it leaves the Checks list.
- Add a Tools row, for example "Notes White Space": "Trims excessive blank lines and spaces from notes". It calls the method in Fix mode and shows the log.
- The office's `databasemaintenance` row for this method name may remain; ignore it.

**Touchpoints:** `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` (line 573), `OpenDental/Forms/FormDatabaseMaintenance.cs` (`FillGridTools`, line 170).
**Schema/preferences/permissions:** none.

### v25_3-misc-printer-options: Printer options window

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html; 25.4 later adds "various minor enhancements to Print window" (https://www.opendental.com/site/version25_4.html). Baseline: https://www.opendental.com/manual243/printersetup.html.

**Behavior.** When printing from some areas, a program-styled **Print Options** window appears so the user can pick the printer, number of copies and page range before printing. Today those areas either print silently to the situation's default printer, or show the Windows print dialog only when "Prompt" is set for the situation.

**Our design (VERIFY which areas on 25.3.48)**

- A new WPF window `FrmPrintOptions` with:
  - a printer combo of installed printers, pre-selected from the print situation
  - copies
  - page range: all, or from/to
  - collate
  - Print and Cancel
- Wire it into `PrinterL.SetPrinter` in place of the WinForms `PrintDialog` whenever `showPrompt` is true.
- Also show it in the areas 25.3 added. Likely candidates are grid prints and sheet prints that bypassed the prompt.
- Remember the last choice per print situation for the session.

**Rules:** the existing `Printing` audit entry still fires. Virtual printers and print-to-file paths behave as today.
**Touchpoints:** `OpenDental/Logic/PrinterL.cs` (`SetPrinter`, line 211), `WpfControlsOD/Frms/FrmPrintPreview.xaml.cs`, `OpenDentBusiness/UI/GridOD.cs` (print), `OpenDental/SheetFramework/SheetPrinting.cs`.
**Schema/preferences:** none.

### v25_3-imaging-font-autocalc: Imaging text, actual and apparent font size update automatically

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (also backported to 25.2.41). Baseline: https://www.opendental.com/manual243/draw.html.

**Behavior.** When editing a text drawing in the Imaging module, typing in **Font Size** (actual) immediately updates **Apparent Size** at the current zoom, and the other way round. No calculate buttons.
**Rules:**

- Our 24.3 code already recalculates on focus loss and has two Calculate buttons. Change it to recalculate on every valid text change, with a guard flag to stop the two boxes updating each other in a loop.
- Remove `butCalculateApparent` and `butCalculateActual`.
- Ignore invalid or blank input until it becomes valid.

**Touchpoints:** `WpfControlsOD/Frms/FrmImageDrawEdit.xaml.cs` (lines 25 to 96) and `FrmImageDrawEdit.xaml`.
**Schema/preferences/permissions:** none.

### v25_3-sheets-checkbox-cleanup: Edit CheckBox window, remove excess text

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html. Baseline: https://www.opendental.com/manual243/sheetscheckbox.html.

**Behavior.** Cosmetic. Long instruction labels are removed or shortened in the sheet **Edit CheckBox** window. Candidates in our window: `label9`, `label1`, `labelRequired`, `labelMiscInstructions`, `labelYesNo`, which explain radio-button grouping and Yes/No pairs. **VERIFY** on 25.3.48. Keep the guidance as tooltips. The eForm checkbox window (`WpfControlsOD/Frms/FrmEFormCheckBoxEdit.xaml`) shares the title; leave it alone unless 25.3.48 shows it changed.
**Touchpoints:** `OpenDental/Forms/FormSheetFieldCheckBox.cs` and `FormSheetFieldCheckBox.designer.cs`.
**Schema/preferences/permissions:** none.

### v25_3-chart-orthochart-nonmodal: Ortho Chart stays open while working elsewhere

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://opendental.blog/version-25-3/. Baseline: https://www.opendental.com/manual243/orthochart.html.

**Behavior.** The Ortho Chart window no longer blocks the rest of the program. Users can switch modules, open other windows and look things up while it stays open.

**Rules and edge cases**

- **Opening**: open with `Show()`, owned by the main window, instead of `ShowDialog()`. Keep **one window per patient**: if it's already open for that patient, bring it to the front and switch to the requested tab.
- The title shows the patient's name. The window stays tied to its patient when the main window's selected patient changes.
- **Saving and closing**:
  - Changes are saved by the window's own Save/OK, as today.
  - Closing with unsaved edits prompts.
  - On save, refresh the Chart module if it shows the same patient.
  - On log-off, user switch or program close, close all open ortho chart windows, prompting for unsaved edits.
- **Signatures**: signature boxes work as today. A signature is invalidated if data changes after signing.
- **Patient merge or delete** while the window is open: close it with a message.
- **Edit lock**: today the window takes a per-patient edit lock (`PatientNotes.SetUserNumOrthoLocked`). Another user sees the chart read-only with a **Take Control** button, and a timer (`timerLocked_Tick`) notices when control moves. The lock is released on Save (line 1240) and on closing (lines 1298 and 1315). Once the window can sit open in the background, a forgotten window holds the lock for a long time. Keep Take Control working. Release the lock whenever the window closes for any reason, including the forced closes above. Consider releasing it after a period of inactivity (**VERIFY** what 25.3.48 does).

**Touchpoints:** `OpenDental/Main Modules/ControlChart.cs` (lines 1730, 9095, 11629), `OpenDental/Main Modules/ControlAppt.cs` (line 2212), `OpenDental/Forms/FormOrthoChart.cs` (lock region from line 142, `butSave_Click` line 1238, `FormOrthoChart_FormClosing` line 1286), `OpenDentBusiness/Data Interface/PatientNotes.cs` (`SetUserNumOrthoLocked`), `OpenDental/Main Modules/FormOpenDental.cs` (log-off and close handling).
**Schema/preferences/permissions:** none (OrthoChartEditFull/User permissions unchanged).

### v25_3-misc-export-csv-txt: Export .csv and Export .txt in more windows

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html. Baseline: https://www.opendental.com/manual243/transactionhistory.html (single Export).

**Behavior.** Windows that offered one Export (text or Excel) now offer **Export .csv** (comma-separated) and **Export .txt** (tab-separated). The Chart of Accounts in our 24.3 `FormAccounting` already has both.
**Rules:**

- `.csv` follows RFC 4180: quote fields that contain commas, quotes or newlines, and double any quotes inside.
- `.txt` is tab-separated with tabs and newlines inside fields replaced by spaces.
- The first row holds column headers. Use UTF-8 with a BOM so Excel reads accents.
- The default folder is the `ExportPath` preference, as today.

**Where (VERIFY the full list on 25.3.48).** Every current `GridOD.Export` caller:

- `OpenDental/Forms/FormJournal.cs` (line 261)
- `OpenDental/Forms/FormTransactionEdit.cs` (line 381)
- `OpenDental/Forms/FormReconcileEdit.cs` (line 242)
- `OpenDental/Forms/FormEmployeeSelect.cs` (line 187)
- `OpenDental/Forms/FormArManager.cs` (lines 2489 to 2497)
- `OpenDental/Forms Reports/FormRpProcNote.cs` (line 254)

**Touchpoints:** `OpenDentBusiness/UI/GridOD.cs` (`Export`, line 865: add a delimiter or format parameter), the callers above, and `OpenDental/Forms/FormAccounting.cs` (`Export_Click`, line 204), which can reuse the shared code.
**Schema/preferences/permissions:** none.

### v25_3-security-claimproc-billedtoins: Billed-to-ins permission also covers Code Sent to Ins

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://www.opendental.com/OpenDentalDocumentation25-3.xml (`EnumPermType` 252 renamed `ClaimProcFieldsBilledToInsEdit`; it covers Fee Billed and Code Sent in Edit Claim Procedure, new or existing).

**Behavior.** The permission that guarded only **Fee Billed to Ins** in Edit Claim Procedure now also guards **Code Sent to Ins**. New display name, our wording: "Claim Procedure: edit Fee Billed or Code Sent to Insurance".
**Rules:**

- Rename the enum member `ClaimProcFeeBilledToInsEdit` to `ClaimProcFieldsBilledToInsEdit`. **Keep the value 252**, so existing `grouppermission` and `securitylog` rows stay valid.
- In `FormClaimProc`:
  - without the permission, `textCodeSent` is read-only, as `textFeeBilled` already is
  - on OK, assign `CodeSent` only when authorized, mirroring how `FeeBilled` is handled near line 1310
  - applies to new and existing claimprocs, and stays compatible with the cases that already disable, lock or hide `textCodeSent` (lines 169, 289 and 305)
- The 24.3 conversion that grants 252 to groups needs no change.

**Audit:** log under 252 when either field changes: "Fee billed: a → b; Code sent: x → y".
**Touchpoints:** `OpenDentBusiness/TableTypes/GroupPermission.cs` (line 795), `OpenDentBusiness/Data Interface/GroupPermissions.cs` (line 662), `OpenDental/User Controls/UserControlSecurityTree.cs` (line 356), `OpenDental/Forms/FormClaimProc.cs` (lines 319, 1309 and 1310), `OpenDentBusiness/Misc/ConvertDatabases8.cs` (comment at line 1595).
**Schema:** none (value unchanged).

### v25_3-security-claimproc-billedtoins-desc: Second wording change for the same permission

- **Category:** desktop. **Size:** S. **Depends on:** v25_3-security-claimproc-billedtoins.
- **Source:** https://www.opendental.com/site/version25_3.html (a separate bullet renaming the longer description of the same permission).

**Behavior.** The longer explanatory text for permission 252 changes from "fee only" to "fee or procedure code". It appears where the permission is described in full: the security tree tooltip or info text, the audit-trail permission list and the user-group permissions report. Our wording: "Edit the fee or the procedure code billed to insurance on claim procedures". Implement in the same change as the item above. **VERIFY** where 25.3.48 shows the longer text.
**Touchpoints:** `OpenDental/User Controls/UserControlSecurityTree.cs`, `OpenDentBusiness/Data Interface/GroupPermissions.cs`, the audit trail permission list (`OpenDental/Forms/FormAudit.cs`).
**Schema/preferences:** none.

### v25_3-tools-file-monitor: File Monitor

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://opendental.com/help254/filemonitor.html (login-gated; search excerpt only).

  The excerpt says:
  - it's opened from Help, File Monitor
  - it shows reads and writes to the image (AtoZ) folder in real time
  - it has Start/Stop and Clear
  - it can stay open in the background
  - it only shows activity from the program instance that opened it
  - cloud storage (Dropbox, SFTP and similar) and local path overrides log fewer actions, because they skip the check that the AtoZ folder exists

**Behavior.** A diagnostic window that lists file operations against the image folder (`OpenDentImages`/AtoZ) as they happen. It helps support staff find slow or failing image access.
**UI:**

- Main menu, Help, **File Monitor** opens a non-modal window, single instance.
- **Feed** grid columns: Time (ms precision), Action (Read, Write, Delete, Copy, Move, CreateDirectory, Exists, List), Path (relative to the AtoZ root), Bytes, Duration (ms), Result (OK or the error message).
- Buttons: **Start/Stop** (toggles capture; capture is off when the window first opens) and **Clear**.

**Rules:**

- Capture only in the current process: a static event that `FileAtoZ` and `ImageStore` raise.
- Zero cost when nobody is listening.
- Keep the newest 5,000 rows.
- Storage back ends that skip the AtoZ existence checks (cloud, SFTP, local path override) produce fewer rows. That's expected.
- When images are stored in the database, there's no AtoZ folder. Say so in the window instead of showing an empty feed.
- Paths contain patient folder names, so never write the feed to disk unless the user exports it.

**Permissions:** none in Open Dental (the Help menu is open to all). Our optional hardening: require the existing Setup permission to open it, because paths reveal patient names.
**Touchpoints:** `OpenDentBusiness/Misc/FileAtoZ.cs` (raise events in `ReadAllBytes`, `WriteAllBytes`, `ReadAllText`, `WriteAllText`, `Copy`, `Move`, `Delete`, `Exists`, `GetFilesInDirectory`, `CreateDirectory`, `GetImage`), `OpenDentBusiness/Imaging/ImageStore.cs`, `OpenDental/Main Modules/FormOpenDentalMenus.cs` (`LayoutMenuHelp`, line 484), and a new `OpenDental/Forms/FormFileMonitor.cs` patterned on `OpenDental/Forms/FormQueryMonitor.cs`.
**Schema/preferences:** none.

### v25_3-tools-dbm-employee-supervisor: DBM check EmployeeMissingSupervisor

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html.

**Behavior.** A new Database Maintenance check finds employees whose **Reports To** points at an employee that no longer exists.

- Check mode logs "Employees with an invalid supervisor: N".
- Fix mode sets `employee.ReportsTo = 0` for them and logs the count.

**Rules:** `ReportsTo = 0` means "no supervisor" and is valid.
**Touchpoints:** `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`. Add a `[DbmMethodAttr] public static string EmployeeMissingSupervisor(bool verbose,DbmMode dbmMode)` modelled on `TimeCardRuleEmployeeNumInvalid` at line 10592. Feeds v25_3-manage-timeclock-reportsto.
**Schema/preferences/permissions:** none.

### v25_3-family-audit-preferredname: Audit trail tracks preferred-name changes

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/site/version25_3.html, https://opendental.blog/version-25-3/.

**Behavior.** Changing a patient's **Preferred** name writes "preferred name changed from 'x' to 'y'" to the PatientEdit audit entry, alongside the existing name, status and address lines.
**Touchpoints:** `OpenDentBusiness/Data Interface/Patients.cs` (`InsertAddressChangeSecurityLogEntry`, line 2646: add a `Preferred` line; it's already called from `SavePatientEdit` at line 5473 and from the family-wide updates at lines 2638 and 2816), `OpenDental/Main Modules/FormOpenDental.cs` (line 3121).
**Schema/preferences:** none. **Permission:** existing PatientEdit log type.

### v25_3-tp-add-procs-active: Easier adding of procedures to the active treatment plan

- **Category:** desktop. **Size:** M. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html. Baseline: https://www.opendental.com/manual243/treatmentplanedit.html, https://www.opendental.com/manual243/charttp.html.

**Behavior.** Public material only says adding procedures to an **active** treatment plan got easier.

**Our design (VERIFY on 25.3.48)**

- In the Treatment Plan module, with the Active Treatment Plan selected, a toolbar button **Add Procs** opens a procedure-code picker with search and quick buttons.
- The user can enter tooth or surface as each code's treatment area requires, then the procedures are created with status TP and attached to the active plan in one step. Today the user has to go to the Chart module.
- Fees and estimates compute as for chart entry.

**Rules:**

- Same validation as Chart entry: treatment area prompts, invalid tooth or surface blocked, procedure-code `IsHidden` excluded.
- Respect the TP procedure permissions (`ProcEdit` and similar).
- Refresh the module after adding.
- Inactive or saved plans can't be targeted. Only the active plan.

**Touchpoints:** `OpenDental/Main Modules/ControlTreat.cs` (toolbar from line 173), `OpenDentBusiness/Data Interface/TreatPlans.cs`, `OpenDentBusiness/Data Interface/Procedures.cs`, `OpenDental/Forms/FormProcCodes2.cs` (selection mode), `OpenDental/Main Modules/ControlChart.cs` (reuse its procedure-entry helpers).
**Audit:** existing `ProcEdit`/TP log entries.
**Schema/preferences:** none.

---

## Not on the 25.3 page but part of 25.3

These come from the versions page summary ("Dental School enhancements"), the 25.3 DB document, and 25.4 bullets that say they were backported into 25.3.x.

### v25_3-school-course-scheduling: Dental school course definitions, schedules and overrides

- **Category:** desktop. **Size:** M for the 25.3 scope (schema, table classes, data rules). The UI below is another L if it isn't deferred. **Depends on:** none.
- **Sources:**
  - https://www.opendental.com/site/versions.html (25.3 summary: Dental School enhancements; 25.4 summary: Dental Schools overhaul)
  - https://www.opendental.com/site/version25_4.html (Major Changes: the overhauled Dental Schools feature, with course and graduation requirements, instructor approval, instructor and student planners, and evaluations moved into the course window)
  - https://www.opendental.com/OpenDentalDocumentation25-3.xml (`schoolcoursedef`, `schoolcoursesched`, `schoolcourse`, `reqneeded`, `reqstudent`; `InvalidType.DentalSchools` now covers SchoolCourseDef and SchoolCourseSched)
  - https://www.opendental.com/site/0_dentalschools.html
  - https://opendental.com/help254/dscoursedefs.html and https://opendental.com/help254/dscourses.html (login-gated; search excerpts say course definitions act as templates and that schedule overrides cover cancelled or moved classes)
  - Baseline: https://www.opendental.com/manual243/dscourses.html
- **Priority:** low for this office, which isn't a school. The **schema is required** for data adoption either way. Confirm there's no school data on the restored DB before deferring the UI:
  ```sql
  SELECT (SELECT COUNT(*) FROM schoolcoursedef) defs, (SELECT COUNT(*) FROM schoolcoursesched) scheds,
         (SELECT COUNT(*) FROM schoolcourse) courses, (SELECT COUNT(*) FROM schoolapproval) approvals;
  ```
- **Version caveat.** No public source ties any of the UI below to 25.3. The 25.3 release-note page has no dental-school bullet. The UI described here comes from 25.4 manual excerpts (`/help254/`). The 25.4 release notes announce the dental-school overhaul as a Major Change. The 25.3 schema may only prepare for that overhaul, the way the mirroring columns arrived in 25.2 before the feature shipped in 25.3. So for the 25.3 step: create the schema, table classes, CRUD and data rules. Hand the windows to the 25.4 dental-school item unless 25.3.48 is shown to have them (**VERIFY** on a 25.3.48 test install with Dental Schools turned on under Show Features, not on the office's live system).

**Behavior**

- **Course definitions** (a "course directory"): reusable templates holding a Course ID (for example "PEDO 732"), a full name, a default weekly schedule and default requirements. Deleting a definition doesn't affect courses already scheduled from it.
- **Scheduled courses** are created from a definition for a **class cohort**. The definition's ID, name, weekly schedule and requirements are copied. Each course has:
  - a start and end date for its default schedule
  - the class it belongs to (all students of that class are in the course)
  - a grading scale
- **Schedule overrides** per course: dated entries that change a session's times or cancel it. Several per day are allowed.
- Completed requirements (`reqstudent`) can link to the **procedure** that satisfied them.

**UI**

- Dental School Setup (`FormDentalSchoolSetup`): a new **Course Defs** button opens the list and edit windows. The edit window has Course ID, Description, a weekly schedule grid (Day, Start, End) and a requirements grid.
- Courses (`FormSchoolCourses`/`FormSchoolCourseEdit`):
  - **Add from Def**
  - fields for Class, Date Start, Date End and Grading Scale
  - a Weekly Schedule grid
  - a **Schedule Overrides** grid (Date, Start, End, Canceled) with Add/Edit/Delete
- Requirements windows show and filter by definition. Student requirement edit gets a procedure picker.

**Rules (from column documentation)**

- `schoolcoursesched` rows belong either to a definition (`SchoolCourseDefNum`>0, `SchoolCourseNum`=0) or to a course (the reverse), never both.
- `DayOfTheWeek` runs 0 (Sunday) to 6 and is ignored on overrides.
- `TimeStart`/`TimeEnd` may be zero only on canceled overrides.
- `IsOverride` and `DateOverride` are used only for courses.
- Start and end dates are required when a course has a default schedule. End must not be before start.
- All evaluation definitions attached to a course must use the course's grading scale. Block saving a mismatch.
- `reqneeded` is copied from definition to course on creation: `SchoolCourseDefNum` becomes 0, and `SchoolCourseNum` and `SchoolClassNum` are set. Graduation requirements have course and definition both 0.
- Older courses keep `SchoolClassNum=0` and stay valid.

**Permissions/audit:** the existing dental-school permissions (Setup, AdminDentalInstructors/Students, AdminDentalEvaluations); no new values in 25.3.

**Schema** (office dump; full DDL in `docs/specs/schema-changes-24.3-to-26.1.md`):

- new tables `schoolcoursedef` and `schoolcoursesched`
- `schoolcourse.DateStart date NOT NULL DEFAULT '0001-01-01'`
- `schoolcourse.DateEnd date NOT NULL DEFAULT '0001-01-01'`
- `schoolcourse.SchoolClassNum bigint(20) NOT NULL DEFAULT 0`
- `schoolcourse.GradingScaleNum bigint(20) NOT NULL`
- `reqneeded.SchoolCourseDefNum bigint(20) NOT NULL`
- `reqstudent.ProcNum bigint(20) NOT NULL`

**Touchpoints:** `OpenDentBusiness/TableTypes/SchoolCourse.cs`, `ReqNeeded.cs`, `ReqStudent.cs` (new `SchoolCourseDef.cs`, `SchoolCourseSched.cs`); `OpenDentBusiness/Data Interface/SchoolCourses.cs`, `ReqNeededs.cs`, `ReqStudents.cs`; `OpenDental/Forms/FormDentalSchoolSetup.cs`, `FormSchoolCourses.cs`, `FormSchoolCourseEdit.cs`, `FormReqNeededs.cs`, `FormReqNeededEdit.cs`, `FormReqStudentEdit.cs`, `FormEvaluationDefEdit.cs`; cache invalidation for `InvalidType.DentalSchools`.

### v25_3-school-approval-schema: Instructor approval table (schema only in 25.3)

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/OpenDentalDocumentation25-3.xml (`schoolapproval`), https://www.opendental.com/site/version25_4.html (approval UI ships in 25.4).

**Behavior.** None visible in 25.3. Add the table and a table class now so office data loads. The approval workflow (instructor sign-off on appointments, procedures, treatment plans and so on) belongs to the 25.4 spec.
**Schema:** `schoolapproval`. Each row has a student `ProvNum` (never 0), a `SignOffStatus` tinyint, an `InstructorNum`, and one non-zero FK among `AptNum`, `ProcNum`, `TreatPlanNum`, `PerioExamNum`, `AllergyNum`, `DiseaseNum`, `DocNum`, `MountNum`, plus `SecDateEntry` and `SecDateTEdit`. All FKs are indexed (office dump).
**Rules:** the `SignOffStatus` values aren't documented in 25.3. **VERIFY** in the 25.4 document before giving them names.
**Touchpoints:** a new `OpenDentBusiness/TableTypes/SchoolApproval.cs` plus generated CRUD.

### v25_3-schema-compat-columns: Schema and enum values needed only for data compatibility

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/OpenDentalDiffDocumentation25-3.xml, https://www.opendental.com/OpenDentalDocumentation25-3.xml, `D:/Dev/opendental-schema/fox-db-schema.sql`.

**Why.** Some 25.3 schema serves features we cut, or only changes types. The office data still uses it, so the conversion must create it and the code must not break on it.

**Columns** (types from the office dump):

| Column | Type | Why we need it |
|---|---|---|
| `asapcomm.UserNum` | `bigint(20) NOT NULL`, no index | Web Sched ASAP is excluded. If any ASAP rows are still written locally, set it to the current user. |
| `task.TriagePosition`, `taskhist.TriagePosition` | `int(11) NOT NULL` | HQ-only; never shown |
| `eformfield.PickListVis`, `.PickListDb`, `.ConditionalValue`, `.PickListVisEnglish`; `eformfielddef.PickListVis`, `.PickListDb`, `.ConditionalValue` | widened to `text NOT NULL` | Mark the class fields `[CrudColumn(SpecialType=CrudSpecialColType.IsText)]` in `OpenDentBusiness/TableTypes/EFormField.cs` and `EFormFieldDef.cs`. `PickListVisEnglish` itself is a 25.2 addition. |
| `emailmessage.FailReason` | widened to `text NOT NULL` | `OpenDentBusiness/TableTypes/EmailMessage.cs` |
| `claimpayment.Note` | the diff document says `text`, but the office DB still has `varchar(255) DEFAULT ''` | Keep varchar(255) for 25.3.48 compatibility and widen during the 26.1 step, as the schema spec recommends |

**Enum values to reserve**, so office rows with these values load and display:

| Enum | Values |
|---|---|
| `SheetTypeEnum` | `Check=28`. In 24.3 the last member is `None=27`, a UI-only "none/all" value that is never saved. Add `Check` after it with an explicit `=28`, and check code that lists all values, for example `FormSheetDef.cs` line 45 |
| `EnumPermType` | 252 renamed (see the claimproc item); `PhoneExtension=272` (HQ, reserve only) |
| `DisplayFieldCategory` | `AccountModuleServiceDateView=22` (placeholder for 25.4), `ERA=23` |
| `StatementMode` | `API=4`. The office may have statements a third-party API created. Show "API" and treat them like Electronic for reprinting. |
| `InsBlueBookRuleType` | 5 renamed (see the Blue Book item) |
| `SchedStatus` | 1 renamed from `Closed` to `Override` (HQ phone-graph use only). Nothing in our 24.3 code references `SchedStatus.Closed`, so rename the member and never write it. Blockouts and holidays keep using `Open`/`Holiday` |
| `UserOdFkeyType` | `DefaultMapSetting=28` (HQ, reserve only) |
| `eServiceCode` | 55, 56 (reserve only) |
| `eServiceAction` | 105, 117, 127 to 133 (reserve only) |

**Touchpoints:** the table classes named above, `OpenDentBusiness/TableTypes/AsapComm.cs`, `OpenDentBusiness/TableTypes/Task.cs`, `TaskHist.cs`, `OpenDentBusiness/SheetFramework/SheetEnums.cs`, `OpenDental/Forms/FormSheetDef.cs`, `OpenDentBusiness/TableTypes/DisplayField.cs`, `OpenDentBusiness/TableTypes/Statement.cs` (`StatementMode`, line 385), `OpenDentBusiness/Db/Enumerations.cs` (`SchedStatus`, line 586), `OpenDentBusiness/TableTypes/UserOdPref.cs` (`UserOdFkeyType`), `OpenDentBusiness/TableTypes/EServiceSignal.cs` (`eServiceCode`), `OpenDentBusiness/TableTypes/EServiceLog.cs` (`eServiceAction`), `OpenDentBusiness/TableTypes/GroupPermission.cs` (`EnumPermType`), `OpenDentBusiness/TableTypes/InsBlueBookRule.cs`, and the new conversion file.

### v25_3-family-bluebook-providerfee: Blue Book rule "UCR Fee" becomes "Provider Fee"

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Sources:** https://www.opendental.com/OpenDentalDocumentation25-3.xml (`InsBlueBookRuleType` value 5: `UcrFee` replaced by `ProviderFee`, which bases estimates on the provider fee and ignores Blue Book history). Context: https://www.opendental.com/site/version25_4.html (UCR terminology renamed "Standard Provider Fee" throughout in 25.4).

**Behavior.** The out-of-network estimate rule that used the UCR fee is now named **Provider Fee** in the Blue Book rules setup and in the estimate log text. Value 5 is unchanged, so data is compatible.
**Rules:**

- Rename the enum member and update labels.
- The percentage preference (`InsBlueBookUcrFeePercent`) keeps its database name. **VERIFY** whether 25.3.48 or only 25.4 renamed the preference row; never rename a preference row the office DB still uses.
- Calculation is unchanged unless VERIFY shows otherwise.

**Touchpoints:** `OpenDentBusiness/TableTypes/InsBlueBookRule.cs`, `OpenDentBusiness/Data Interface/InsBlueBooks.cs` (lines 295 to 422), `OpenDental/Forms/FormInsBlueBookRules.cs`.
**Schema:** none.

### v25_3-tools-dbm-base64-docs: DBM tool "Create Documents From Base64 Notes" (backported to 25.3.39)

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_4.html (Miscellaneous; also backported to 25.3.39).

**Behavior.** A Tools-tab utility that finds documents whose file content was stored as base64 text in a note field, writes each one out as a real file in the patient's image folder, and links the `document` row to that file. **VERIFY** on 25.3.48 which table and field it scans, and whether it clears the note afterwards. No public detail beyond the name.
**Rules:** dry-run count first, then confirm. Skip undecodable rows and log them. Never overwrite an existing file; add a suffix instead.
**Touchpoints:** `OpenDental/Forms/FormDatabaseMaintenance.cs` (`FillGridTools`), `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`, `OpenDentBusiness/Imaging/ImageStore.cs`, `OpenDentBusiness/Misc/FileAtoZ.cs`.
**Schema/preferences/permissions:** none (DBM requires Setup).

### v25_3-canada-cda-codes-2026: 2026 CDA procedure codes (backported to 25.3.41)

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_4.html (Canada; also backported to 25.3.41).

**Behavior.** Procedure Code Tools' "CDA codes" option adds missing 2026 Canadian codes. **Only relevant for Canadian offices.** The same licensing constraint as CDT applies (user-supplied file, no code text in the repo).
**Touchpoints:** `OpenDental/Forms/FormProcTools.cs` (the CDA text at line 60), `OpenDentBusiness/Data Interface/ProcedureCodes.cs`.
**Schema:** none.

### v25_3-misc-pem-path-label: PEM path field label (backported to 25.3.53, after the office's 25.3.48)

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_4.html (Miscellaneous; also backported to 25.3.53).

**Behavior.** Choose Database and Reports Setup use clearer wording for the SSL certificate-authority PEM file path. Text change only. Our wording: "SSL CA certificate (.pem) path". The fields behind the label differ by window:

- Choose Database: `textPEM`, saved to the connection's `SslCA`
- Reports Setup: `textSkySQL`, saved to preference `ReportingServerSslCa`
- Preferences, Server Connections: `textSkySQL`, saved to `ReadOnlyServerSslCa`. The release note doesn't name this window; use the same label for consistency.

**Touchpoints:** `OpenDental/Forms/FormChooseDatabase.cs` (and its designer), `OpenDental/Forms/FormReportSetup.cs`, `OpenDental/User Controls/Preferences/UserControlServerConnections.cs`.
**Schema:** none. This is also in 25.4; implement once.

### v25_3-procs-codegroup-reset-tool: Reset Code Groups tool (backported to 25.3.54, after the office's 25.3.48)

- **Category:** desktop. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_4.html (Miscellaneous; also backported to 25.3.54).

**Behavior.** Procedure Code Tools gets an option to reset the Code Groups to the program defaults: re-create missing default groups and restore their codes and fixed-type mapping. **VERIFY** whether custom groups are kept (assume kept, defaults restored) and whether benefits linked to groups are re-pointed.
**Rules:** never delete a code group that benefits reference.
**Touchpoints:** `OpenDental/Forms/FormProcTools.cs`, `OpenDentBusiness/Data Interface/CodeGroups.cs`, `OpenDentBusiness/TableTypes/CodeGroup.cs`.
**Schema:** none. This is also in 25.4; implement once.

### v25_3-account-era-displayfields: ERA Claim Paid grid columns in Display Fields (backported to 25.3.78)

- **Category:** desktop. **Size:** M. **Depends on:** v25_3-schema-compat-columns (enum value 23).
- **Sources:** https://www.opendental.com/site/version25_4.html (also backported to 25.3.78), https://www.opendental.com/OpenDentalDocumentation25-3.xml (`DisplayFieldCategory.ERA = 23`; value 22 reserved for 25.4's Account Service Date View).

**Behavior.** Setup, Display Fields gets an **ERA** category. The office chooses, orders and sizes the columns of the **Claim Paid** grid in the ERA (835) window.
**Rules:**

- Defaults match today's columns.
- Stored as `displayfield` rows with `Category=23`, per clinic/user like other categories.
- Columns that can't be removed (for example claim identifiers) stay fixed.

**Touchpoints:** `OpenDentBusiness/TableTypes/DisplayField.cs` (enum at line 62; the last 24.3 value is `SuperFamilyGridCols=21`, so add placeholder 22 before `ERA=23`), `OpenDentBusiness/Data Interface/DisplayFields.cs` (defaults and available list), `OpenDental/Forms/FormDisplayFieldCategories.cs`, `OpenDental/Forms/FormDisplayFields.cs`, `OpenDental/Forms/FormEtrans835Edit.cs` (the "Claims Paid" grid is `gridClaimDetails`, filled by `FillClaimDetails` at line 142). `FormEtrans835ClaimPay.cs` holds the per-claim EOB grids, not this one, so it needs no change.
**Schema:** none beyond the enum value.

---

## Vendor-direct

### v25_3-account-payconnect-error-log: Log PayConnect errors

- **Category:** vendor-direct. **Size:** S. **Depends on:** none.
- **Source:** https://www.opendental.com/site/version25_3.html (in Open Dental the errors go to the eConnector log; also backported to 25.2.40).

**Behavior for us.** We have no eConnector. Every PayConnect call our desktop makes goes straight to the vendor: manual charges and refunds, recurring charges run from the desktop, and token operations. Failures must be logged locally so staff can diagnose declines and outages.
**Rules:**

- Scope note: in Open Dental the unattended run of recurring charges (preference `RecurringChargesAutomatedEnabled`) happens outside the desktop program; nothing in this repo runs it. Without the eConnector, that run needs a local runner, which belongs to the decoupling plan's local-services work, not to this item. It should write to the same log.
- Write one line per failure under the local log folder, subdirectory `PayConnect` (via `CodeBase/Utilities/Logger.cs`). Include:
  - time and workstation
  - user
  - clinic or merchant ID
  - PatNum
  - operation (sale, refund, void, token, recurring)
  - amount
  - HTTP status, PayConnect response code and message
  - the last 4 digits of the card only
- **Never** log the full card number, CVV, tokens, passwords or API keys.
- Recurring-charge batches add a summary line per run.
- Optionally show the last 50 errors in the PayConnect setup window (read from the log).
- Keep logs 90 days (Logger default).

**Touchpoints:** `OpenDentBusiness/WebBridges/PayConnect.cs`, `OpenDentBusiness/WebBridges/PayConnect2.cs`, `OpenDental/Forms/FormPayConnect.cs`, `FormPayConnect2.cs`, `FormPayConnectSetup.cs`, `OpenDentBusiness/Data Interface/RecurringCharges.cs`, `OpenDental/Forms/FormCreditRecurringCharges.cs`, `CodeBase/Utilities/Logger.cs`.
**Credentials:** the office's own PayConnect merchant credentials, entered in PayConnect Setup.
**Schema/preferences:** none.

---

## Excluded

| ID | Item | Why excluded |
|---|---|---|
| v25_3-eservices-websched-existingpat | Web Sched Existing Patient redesign and its own URL | Web Sched is an Open Dental hosted eService; our cut list removes it. |
| v25_3-account-carecredit-refund-adjtype | CareCredit merchant refund adjustment type | The decoupling plan cuts the CareCredit integration entirely (removed vendor). |
| v25_3-eservices-eclipboard-branding | eClipboard 25 device colors (and logo) | eClipboard is a hosted eService. The `branding` table is 25.2 schema; create it empty for compatibility (see the 25.2 spec). |
| v25_3-eservices-byod-deprecated | BYOD (bring your own device) deprecated | A BYOD eClipboard check-in feature; the whole eClipboard UI is being removed anyway. |
| v25_3-eservices-websched-asap-user | Web Sched ASAP records which user sent a message | ASAP texting goes through Open Dental's hosted texting. The column is created via v25_3-schema-compat-columns. |
| v25_3-eservices-texting-register-carriers | Updated Register with Carriers form | Integrated texting is an Open Dental hosted service. |
| v25_3-eservices-eclipboard-licenses | View eClipboard 25 / Web licenses | Licenses come from Open Dental HQ for a hosted service. |
| v25_3-help-supportstatus-qr | QR code to Help in Support Status | Support Status shows the HQ support contract and links to opendental.com; it's being removed. |
| v25_3-eservices-eclipboard-ocr | OCR for eClipboard (25.4, backported to 25.3.41) | A hosted eClipboard add-on eService. |
| v25_3-canada-fee-guides-2026 | 2026 Ontario and BC dental association fee guides (25.3.43, 25.3.47) | The guides are licensed content that Open Dental delivers as downloads from its servers. Only that delivery is excluded. The import path stays: the decoupling plan's "Other downloads" row makes Canadian fee schedules a configurable local import. Relevant only to Canadian offices. |
| v25_3-api-benefits-treatarea | API: TreatArea on the benefits endpoint | Local REST API is cut. |
| v25_3-api-procedurelogs-groupnote-append | API: Procedurelogs PUT GroupNote can append | Local REST API is cut. |
| v25_3-api-codegroups-showin | API: CodeGroups ShowInFrequency/ShowInOther | Local REST API is cut. The columns are covered by desktop items. |
| v25_3-api-patfields-put-response | API: PatFields PUT returns the object | Local REST API is cut. |
| v25_3-api-rxpats-erxpharmacy | API: RxPats GET adds ErxPharmacyInfo | Local REST API is cut. |
| v25_3-api-payments-put-payplancharges | API: Payments PUT Partial accepts PayPlanCharges | Local REST API is cut. |
| v25_3-api-procedurelogs-complete-noappt | API: completed procedures without an appointment | Local REST API is cut. |
| v25_3-api-statements-post | API: Statements POST | Local REST API is cut. The `StatementMode.API=4` value is reserved for data compatibility. |
| v25_3-api-payplanlinks | API: PayPlanLinks resource | Local REST API is cut. |
| v25_3-api-payplans-put-dynamic | API: PayPlans PUT (dynamic) | Local REST API is cut. |
| v25_3-api-groupnotes-tp | API: group notes on TP procedures | Local REST API is cut. |
| v25_3-hq-task-triageposition | Task triage position (`task`/`taskhist.TriagePosition`) | HQ-internal support triage. The columns are created via v25_3-schema-compat-columns. |
| v25_3-hq-enum-additions | HQ-only enum values: `EnumPermType.PhoneExtension` (272), `UserOdFkeyType.DefaultMapSetting` (28), `SchedStatus` 1 renamed `Override` for the HQ phone graph | HQ-internal tools. The values are reserved for data compatibility only. |
