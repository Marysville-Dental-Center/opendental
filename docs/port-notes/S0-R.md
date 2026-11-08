# Port note: S0-R (the DentX, Dexis Integrator and Vipersoft program links restored)

Package S0-R of stage 0 of the .NET 10 move, on branch `net10/s0r-1` from `net10/int0` (5a3c33dcd). The owner, 2026-11-08: "Don't make the
assumption that if the office doesn't use, we'd cut it. ... DentX, Dexis Integrator and Vipersoft links, and NDde.dll we should keep if we
can." Commit 0d2ab2722 ("Remove the DentX, Dexis Integrator and Vipersoft links and NDde.dll (REL-FILES 2)") and the documentation commit
5a2d00ebd had removed them on 2026-11-07. Synthetic data only; the tests use no database and open no DDE conversation.

## NDde's licence: it cannot ship with this program

"Keep if we can" first asks whether `NDde.dll` may be shipped with this GPL program. The repository's own records settle it:

- **The licence text.** NDde 1.03 (Brian Gideon, 2005) comes under its own "Shared Source License for NDde", which the repository held as
  `OpenPmsDental/Resources/NDde.txt` until 0d2ab2722 (`git show 0d2ab2722^:OpenPmsDental/Resources/NDde.txt`). It allows use and
  redistribution, derivative works included, on conditions. Three of them are restrictions the GNU GPL does not allow on a work it covers:
  clause 2 (a source distribution only under this licence, an object-only distribution only under a licence that complies with it),
  clause 5 (the licence ends for whoever sues over patents that apply to the software) and clause 7 (U.S. export rules). GPL version 2,
  section 6, forbids further restrictions on the recipients' rights; GPL version 3 allows only the additional terms its section 7 lists,
  and none of these is among them (section 10). It is the structure of the Microsoft Public License (clause 3(D) there), which the FSF lists
  as a free licence that is incompatible with the GPL.
- **The repository's records.** REL-FILES question 2 called it "in the style of MS-PL" with its GPL compatibility "not settled"; the
  owner's answer of 2026-11-07 was that it does not fit the GPL (`docs/owner-questions.md`, REL-FILES 2); the LIB-PDF note there says that
  "fo-dicom (MS-PL) and NDde (MS-PL style) stay incompatible under v3 as under v2". The binaries audit calls it "permissive", which is about
  its use terms, not about the GPL. No later NDde release or its licence is in the repository (NuGet's NDde 2.x was not examined: no
  internet), and an MS-PL release would not fit either.
- `NDde.dll` is linked into the program (the bridges call its classes), so it is part of the work the GPL covers, not an aggregate.

**Choice: NDde stays out.** The three links come back on a small in-house DDE client instead, with exactly the calls they make. `NDde.dll`
stays off `Required dlls`, the project files, the licence list and `THIRD-PARTY-NOTICES.txt`; the release still refuses it
(`tools/Build-Release.ps1`), `ForkReleases.ListRetiredFiles` still never publishes it, and setup's `[InstallDelete]` still deletes a copy an
older installation left behind. Their reasons now say it was replaced, not removed with the links. **Owner question:** if counsel finds NDde
acceptable after all, it could come back as it was; nothing in the bridges needs it.

## The in-house DDE client (`CodeBase/Utilities/ODDdeClient.cs`)

Written from the Win32 documentation of the DDE Management Library (DDEML), through P/Invoke to `user32.dll`:

| Step | DDEML call |
|---|---|
| Open the instance (first `Connect`) | `DdeInitializeA` (client only: `APPCMD_CLIENTONLY`, registrations skipped) |
| Name the service and topic | `DdeCreateStringHandleA` with `CP_WINANSI`, freed with `DdeFreeStringHandle` |
| `Connect()` | `DdeConnect` |
| `Execute(command,timeoutMs)` | `DdeClientTransaction` with `XTYP_EXECUTE`, `CF_TEXT`, a synchronous timeout |
| `Disconnect()` | `DdeDisconnect` |
| `Dispose()` | `DdeDisconnect` (if still connected), `DdeUninitialize` |
| An error | `DdeGetLastError`, in `ODDdeException` (`DMLERR_NO_CONV_ESTABLISHED` and so on) |

It sends what NDde 1.03 sent (its metadata and IL were read, nothing of it was copied): an ANSI DDEML instance, service and topic names in
the ANSI code page, the command as ASCII text (other characters become `?`) with its terminating null in `CF_TEXT`, and it refuses a
command, service or topic over 255 characters and a timeout that is not positive (NDde threw `ArgumentException`; the bridges caught it as
before). A server that ends the conversation is seen through the `XTYP_DISCONNECT` callback, so `IsConnected` turns false as NDde's did,
and the Dexis Integrator link reconnects on its next patient.

**Threads.** A DDEML instance belongs to the thread that opened it. NDde ran DentX's and Vipersoft's conversations on a thread of its own
(`new DdeContext()`) and the Dexis Integrator's on the main window's thread (`new DdeContext(form)`). The client runs on the calling thread,
which for DentX and Vipersoft is the main window's thread (the button click), or marshals every call to an `ISynchronizeInvoke` it is given:
the Dexis Integrator gives it the main window, as before, so a patient change raised on another thread still reaches its conversation. A
call on another thread without one throws `InvalidOperationException`. Synchronous DDEML transactions run their own message loop, so the
window stays painted while a command waits (DentX and Vipersoft wait at most 2 seconds, the Dexis Integrator 30).

**Tests replace the sender.** `ODDdeClient.FuncCreate` makes every conversation (`ODDdeClient.Create(service,topic,synchronizingObject)`).
Tests set a fake that records the calls and set the default back.

## The three links

| Link | ProgName | Service, topic | Command | Timeout | Code |
|---|---|---|---|---|---|
| DentX (ProImage) | `DentX` | ProImage, Image | `Xray,PatientNo,FirstName,LastName,Birth Date,Sex,Address,City,State,Zip` (commas in values dropped) | 2 s | `WpfControlsOD/Bridges/DentX.cs` |
| Dexis Integrator | `DexisIntegrator` | Dexis, Patient (or a communication file) | `SET id LN="..." FN="..." MI="..." BD=yyyyMMdd` (BD only while within 255 characters) | 30 s, the conversation kept open | `WpfControlsOD/Bridges/DexisIntegrator.cs` |
| Vipersoft (Clarity) | `Vipersoft` | Vipersoft, Advanced IntraOral | `\x04hwnd|OpenDental|ID|Last|First|MI||Provider|...|SSN|1|` | 2 s | `WpfControlsOD/Bridges/Vipersoft.cs` |

The bridge files are those of 0d2ab2722^ with two changes: each builds its command in a public method (`DentX.GetCommand`,
`Vipersoft.GetCommand`, `DexisIntegrator.GetDdeCommand`) and sends it through `ODDdeClient` (`DentX.SendCommand`, `Vipersoft.SendCommand`,
`DexisIntegrator.ExecuteDDE` with its one retry after `ObjectDisposedException`). The behaviour is that of before: the same checks and
messages, the same program start (ProImage from `%windir%\dentx.ini`, Vipersoft with `-nostartup`), errors of DentX and Vipersoft ignored,
the Dexis Integrator's shown with the same message, and the Dexis Integrator following patient and user changes. Vipersoft keeps sending
"OpenDental" as the calling program's name (the value it expects; `tools/Test-NoOldNames.ps1` allows it again).

**Program Links.** `ProgramLinksDiscontinued.ListProgramNamesRemoved` and its "(removed)" label, launch message and helpers are gone again:
the three rows are ordinary program links that the office can enable, give toolbar buttons and launch. `ProgramL.Execute` dispatches them
(`DentX` and `DexisIntegrator`, `Vipersoft` under `#if !DISABLE_WINDOWS_BRIDGES`, as before). What 0d2ab2722 added for every discontinued
link stays: `ToolButItems.GetForToolBar` shows no button of a discontinued link (DXC Patient Credit Score, Trojan Express Collect) even when
its row is enabled.

**Rows and properties.** 0d2ab2722 deleted no row and no fork migration step changed them, so **no migration step** is needed: every
database still has the program rows (DentX 20, Vipersoft 34, DexisIntegrator 113 in `db/init/new-database.sql`), their properties ("Enter 0
to use PatientNum, or 1 to use ChartNum"; the Dexis Integrator's "Enter 0 to use DDE, or 1 to use communication file" and "Communication
files folder path") and toolbar rows, all as Open Dental's conversions made them, disabled. `db/init/new-database.sql` is unchanged.

## Moving to .NET 10 and to 64-bit

- **The DDE client** uses only `DllImport` to `user32.dll`, `ISynchronizeInvoke` and `Encoding.ASCII`, all in .NET 10 on Windows
  (`net10.0-windows`). `DllImport` can stay, or become `LibraryImport` (source-generated; it needs `StringMarshalling` for the ANSI names,
  or the names marshalled by hand). Nothing of NDde is needed; whether NDde ran on .NET 10 is moot.
- **64-bit.** The handles (`HCONV`, `HSZ`, `HDDEDATA`) are `IntPtr`, the callback's `dwData1`/`dwData2` are `UIntPtr` (`ULONG_PTR`), the
  instance id, sizes, flags and the result are `uint` (`DWORD`), so the declarations are right for x64 as they are; the stdcall convention
  is ignored there. DDE works between 64-bit and 32-bit programs, so a 64-bit build can still reach a 32-bit ProImage, Dexis Integrator or
  Vipersoft (not tried here: no such program on this computer). Vipersoft gets the main window's handle as a number; window handles have 32
  significant bits on 64-bit Windows too, so it is the same kind of number.
- **Dexis Integrator's communication file** is written in code page 1252 (`Encoding.GetEncoding(1252)`). On .NET 10 that needs the
  code-pages provider (`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` once at start-up), or it throws.
- **WinForms.** The bridges use `Application.OpenForms`, `MessageBox` and a form's `Handle`; all exist in .NET 10's Windows Forms.

## Tests

- `tests/VendorDirectTests/BridgesRemoval/DdeBridgesRemovedTests.cs` becomes `tests/VendorDirectTests/DdeBridges/DdeBridgesTests.cs`: the
  bridge files, their `Compile` lines and `ProgramL` branches are there; the rows are ordinary links; NDde stays out (files, references,
  licence list, notices, the release, setup); no built assembly references NDde.
- `tests/VendorDirectTests/DdeBridges/DdeCommandTests.cs`: each bridge's command (PatNum and ChartNumber, commas, birthdates, the
  255-character rule), the service, topic, calls and timeouts each bridge makes on a fake conversation, the Dexis Integrator's reuse,
  reconnect and single retry; the client's own rules (ASCII bytes with the null, the length and timeout checks, use before `Connect`, after
  `Dispose`) and that each DDEML function it declares exists in `user32.dll`. VendorDirectTests now references WpfControlsOD.
- `BSHELLTests` (Program Links labels discontinued links again with the one-argument call), `ReleaseFlowTests` (`ProgramNamesTests`
  checks the Dexis Integrator's main-window lookup again), `AttributionTests` (the comment on NDde).
