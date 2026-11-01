# Port note: BIN-B, binaries we may not redistribute (part B) and dead references

Branch `pre/bin-b-1`, cut from `main` (d9c177ca6). Part of the run before the rename, for the v0.0 release. Source: the read-only release review of main, `report-release.md` sections 1.2, 1.3, 1.5 and 1.6 (scratch notes of the organization review; not in the repository).

## Sources and rules

- No binary was decompiled, disassembled or inspected beyond its file name. Topaz's control is called by the method names the existing code already called. The four Word enumeration values come from Word's public object model. What DotNetZip did comes from its public documentation: a string entry is stored in the system's default code page, `ExtractAll` throws for a file that already exists, and `AddFile(path)` keeps the file's folders without the drive.
- No documentation text was copied. Every test uses synthetic data. The Topaz "present" tests use a stand-in that the test compiles into a temp folder; they never use Topaz's own file.
- No database, table, column or preference name changed. No migration. No namespace, assembly, project or exe was renamed.

## What changed

### 1. Topaz SigPlusNET.dll (no published redistribution terms)

- No project references `SigPlusNET` any more (`CodeBase`, `OpenDentBusiness`, `OpenDental`, `UnitTests`). `Required dlls/SigPlusNET.dll` is deleted.
- `OpenDentBusiness/UI/TopazWrapper.cs` loads `Topaz.SigPlusNET` at run time with `Assembly.LoadFrom`, once per run, from the first `SigPlusNET.dll` it finds in:
  1. the program folder. An office that ran Open Dental already has the file there;
  2. every `Topaz Systems` folder under Program Files (x86) and Program Files, and up to three folder levels below it;
  3. `%windir%\SigPlus`.
  INFERRED: these are the folders Topaz's installers use. See owner question 1.
- Each wrapper method calls the control's public method by name through reflection. Arguments are converted to the method's parameter types, and the method's own exceptions are rethrown unwrapped. `FillSignatureEncodings` still reaches the control's private `Sig` field, now through `topaz.GetType()`. Every existing signature path, including the retries and the plugin hooks, is unchanged.
- When the software is absent:
  - `GetTopaz` throws `TopazWrapper.TopazNotInstalledException` with the message "Topaz signature pad software is not installed." If a file was found but could not be loaded, the message adds the reason.
  - Every other wrapper method does nothing, or returns 0 or "", when it gets a null control, so callers that keep a null control never crash.
- Callers:
  - `SignatureBoxWrapper` shows the message in its label instead of "Invalid Signature" when it has to show a Topaz signature, and shows it in a message box when the Topaz sign button is clicked. The label's own text (`LabelText`) is put back on the next fill or clear. Callers that set `LabelText` while the message shows keep their value.
  - `ControlImages` and `ControlImagesOld` show the same message for a Topaz-signed document and no longer dereference a missing control.
  - The Topaz code in `FormTPsign` and `FormAnestheticRecord` is commented out or not compiled, so it is untouched.

### 2. Word letter merge: late binding

- `LetterMerges.GetWordApp()` returns `object`. It creates the instance with `Type.GetTypeFromProgID("Word.Application")` and `Activator.CreateInstance`, then sets `Visible` and calls `Activate` late bound, as before.
- `ChartLetterL.MergeWithWordProcessor`, `FormLetterMerges` (print, preview, edit template) and `FormLetterMergeEdit` (new .doc template) use it as `dynamic`. Omitted optional arguments are passed as missing, exactly as the interop calls passed `Missing.Value`.
- Word's enumeration values are constants in `LetterMerges`: `WORD_MAIL_MERGE_TO_NEW_DOCUMENT`=0, `WORD_MAIL_MERGE_TO_PRINTER`=1, `WORD_WINDOW_STATE_MAXIMIZE`=1, `WORD_WINDOW_STATE_MINIMIZE`=2.
- The `Interop.Word` and `Interop.Microsoft.Office.Core` references are removed (`OpenDental`, `OpenDentBusiness`, `UnitTests`). So are the unused `using Word;` lines (`CanadianOutput`, `RequiredFieldConditions`, `ProcedureCodes`) and the unused `using Microsoft.Office.Core;` (`FormApptsOther`).
- `Interop.Word.dll`, `Interop.Microsoft.Office.Core.dll`, `Interop.VBIDE.dll` and `stdole.dll` are deleted. VBIDE and stdole were copied only as dependencies of the two interop assemblies.
- The .docx letter merge (`DocxMailMerge`, no word processor needed) is untouched.

### 3. Windows.winmd and installutil

- `PasswordVaultWrapper.csproj`: the `Windows` reference (`Required dlls/Windows.winmd`) is now `Private=False`. It is needed to compile only; at run time Windows resolves `Windows.Security.Credentials` from its own metadata. The file stays in `Required dlls` for the build (owner question 5).
- `ServiceManager.csproj`: the `installutil` reference is removed. No ServiceManager code used it, and it only dropped a copy of `installutil.exe` into `ServiceManager/bin`. `Required dlls/installutil.exe` is deleted.
- `CodeBase/Utilities/ServicesHelper.cs` (outside this package, one field) installs services with `[CurrentDirectory]\InstallUtil\installutil.exe` when an installation has one, and otherwise with the .NET Framework's own `InstallUtil.exe` (`RuntimeEnvironment.GetRuntimeDirectory()`). Without this change, a service install would fail on every installation that ships no installutil.
- Saved PasswordVault credentials: there is no existing test for them. The new test reads the vault for a resource that cannot exist, without the copied `Windows.winmd`. It gets "not found" (false), not a type-load failure. Nothing is written to the vault: writing a test credential to the user's Windows vault was avoided on purpose.

### 4. Dead references still copied

The references were removed and the files deleted. No code used any of them (checked by search before removal, and pinned by the tests).

| File | Referenced by |
|---|---|
| `AxInterop.AcroPDFLib.dll`, `Interop.AcroPDFLib.dll` (its dependency) | `OpenDental` |
| `Interop.Acrobat.dll`, `Interop.AFORMAUTLib.dll` | `OpenDental`, and `PluginExample` (not in the solution) |
| `zxing.dll`, `zxing.presentation.dll` | `OpenDental` |
| `MigraDoc.RtfRendering.dll` | `OpenDental`, `PluginExample` |
| `Tao.FreeGlut.dll`, `freeglut.dll` (its native library) | `CodeBase`, `SparksToothChart`; also the unreferenced copies in `SparksToothChart/RequiredDlls` |
| `Avalara.AvaTax.RestClient.net45.dll` | `OpenDentBusiness` |

AvaTax:
- `OpenDentBusiness/WebBridges/AvaTax.cs` keeps only the always-disabled checks the callers use: `IsEnabled`, `IsTaxable`, `DoSendProcToAvalara`, `PingAvaTax`, `GetEstimate`, `DidUpdateAdjustment` and `DoCreateReturnAdjustment` all say no. It also keeps the three program-property readers (`GetSalesTaxAdjType`, `GetSalesTaxReturnAdjType`, `GetTaxLockDate`), which existing sales tax adjustment queries still use.
- Every call into Avalara's SDK is deleted, and so is the setup window `FormAvaTax` (it was unreachable: Program Links already hid the AvaTax row) with its case in `FormProgramLinks`.
- The `ProgramName.AvaTax` row and its properties stay in the database.
- INFERRED: a disabled stub, not the removal of every `AvaTax.IsEnabled()` branch in `Adjustments`, `Procedures`, `RepeatCharges`, `TreatPlans`, `SheetDrawingJob`, `ControlTreat`, `FormAdjust`, `FormProcEdit` and `FormRepeatChargesUpdate`. Removing those branches would edit many files other packages work in, for no change in behaviour (owner question 2).

### 5. RS232.dll (unknown author and licence)

`Eclaims/TerminalConnector.cs` was a thin wrapper, so it now uses `System.IO.Ports.SerialPort` with the same settings:
- COM1 until a port is opened, then `COM<n>`;
- 9600 baud, 8 data bits, 1 stop bit, no parity;
- 1.5 second read and write timeouts;
- DTR and RTS on after open;
- the buffers purged before each send.

Text is sent in ISO-8859-1 (one byte per character), the same mapping the receive side has always used. Received bytes go to the buffer and to `TextReceivedEvent` as before; the buffer is now locked, because it is filled on the serial port's thread. `ReceiveBytes` is public so it can be tested without a modem. The BCBS Georgia path (`BCBSGA`, `FormTerminalConnection`) is unchanged. `RS232.dll` is deleted.

Fix found on the way: `WaitFor(text,timeout)` passed a null second reply to `string.IndexOf`, which throws. So a single expected reply that was not already in the buffer at the first look could never be seen. The null reply is now skipped.

### 6. DotNetZip (Ionic.Zip: MS-PL, abandoned, CVE-2024-48510)

New `CodeBase/Utilities/ODZip.cs` on `System.IO.Compression`:
- `CreateArchive` and `CreateArchiveFile` keep entry order, use '/' separators and deflate;
- `GetDefaultEncodingBytes` stores a string entry the way DotNetZip did;
- `GetEntryNameWithFolders` gives the name DotNetZip's `AddFile(path)` used;
- `GetSafeExtractPath` returns null for an empty name, a rooted path, a leading separator, any ':' (drive or alternate data stream), a ".." that leaves the folder, the folder itself, or invalid characters;
- `ExtractEntry` and `ExtractAll` check every entry before writing anything, so an archive with one unsafe path writes nothing;
- `ReadEntry` and `CopyBounded` refuse an entry that inflates beyond a limit.

Every use was replaced:

| Where | Before (DotNetZip) | Now |
|---|---|---|
| `ForkReleases.BuildRelease` (release channel) | files under their relative paths with '/', in `GetReleaseFiles` order, then `ForkRelease.manifest` at the root | the same layout, written with `ZipArchive` |
| `ForkReleases.ExtractArchive` (the pull; it runs in OpenDental before UpdateFileCopier copies the folder, and UpdateFileCopier itself never unzips) | safe-path and listed-file checks | the same checks, plus: an entry that appears twice, or inflates beyond its listed size, is refused; a manifest entry is read up to 16 MB; an unreadable archive is a problem, not an exception. Now public, so it can be tested without a database. Only the zip code of `ForkReleases.cs` was touched (REN-PROOF edits its lists). |
| `EmdeonMedical.Launch` | batch file at the archive root, base64 of `MemoryStream.GetBuffer()` (with unused trailing bytes) | the same entry, base64 of the archive alone |
| `EmdeonMedical.Retrieve` | `ExtractAll` into the response folder, throwing for an existing file | `ODZip.ExtractAll(...,overwrite:false)`: the same, and refuses any path outside the folder |
| `FormCodeSystemsImport` (CPT MEDU file) | `Extract` into the temp folder, overwriting | `ODZip.ExtractEntry(...,overwrite:true)`: the same, and refuses any path outside the temp folder |
| `QualityMeasures` (QRDA Category I zips) | string entries | the same names and bytes (default code page) |
| `Ramq` (claims zip saved to the export path) | string entry, Zip64 always | the same name and bytes; Zip64 headers only when needed (the RAMQ upload was never written) |
| `WebMD.SubmitBatch` (never called) | `AddFile(path)` with folders, `GetBuffer()` | the same entry name, the archive alone |
| `RxNorms`, `FormCDSSetup`, `FormEhrSetup`, `FormUpdateSetup` | unused `using Ionic.Zip;` | removed |

The `Ionic.Zip` references are removed from `OpenDental`, `OpenDentBusiness`, `UnitTests` and `ReleaseFlowTests` (which never used it). `Required dlls/Ionic.Zip.dll` is deleted.

## Decisions marked INFERRED

1. The Topaz search folders, listed in item 1.
2. AvaTax kept as a disabled stub; its branches in shared files stay (item 4).
3. RAMQ and QRDA string entries keep DotNetZip's default code page. The bytes are identical to before, even though the XML inside may declare another encoding.
4. Emdeon and WebMD now send only the archive, without the unused trailing buffer bytes `GetBuffer()` added. Every zip reader ignores those bytes; dropping them makes the payload a correct zip.
5. The BCBS Georgia dial-up path was kept on `SerialPort`, not removed, because the wrapper was thin. It is not tested with a modem.
6. The single-reply `WaitFor` fix in item 5.
7. The installutil fallback in `ServicesHelper` (outside this package).

## Owner questions

1. Topaz: where does the clinic's Topaz software put `SigPlusNET.dll`? If it is not under a `Topaz Systems` folder or `%windir%\SigPlus`, the office copies it into the program folder (or the search gets that folder added).
2. AvaTax: remove the always-false AvaTax branches in the shared billing files (Adjustments, Procedures, RepeatCharges and the windows), and the stub with them, in a later cleanup?
3. RAMQ and QRDA zips: keep the system code page, or store the XML as UTF-8?
4. BCBS Georgia dial-up claims: does any office still use them? If not, the clearinghouse path and `TerminalConnector` can go.
5. `Required dlls/Windows.winmd` (a Windows SDK file) is still in the repository, to compile `PasswordVaultWrapper`. Keep it, or reference the installed Windows SDK's `UnionMetadata\Windows.winmd` instead?
6. The in-app licence list (`FormLicense`, `FormRegistrationKey`) still lists "Drifty" (DotNetZip), "Stdole", "Zxing" and "Tao_Freeglut", for files no longer shipped. Should the licence package drop them? This package did not touch the list.

## Not done here

- No Topaz pad, Word, modem or Emdeon account was used. The late-bound Word calls and the Topaz run-time load are verified by build, by the stand-in tests and by review only. The program GUI was never launched.
- The legacy `UnitTests` project's Topaz signature tests (`SignatureBoxWrapperTests`) now need Topaz's software installed. That project is not part of the fork's test runs.
- `RequiredServerDlls/installutil.exe`, `docs/third-party-binaries-audit.md` and the README statements about `Required dlls` belong to other packages and were left as they are.

## Tests

- New project `tests/BINBTests` (added to `OpenDental.sln` and `OpenDental.Local.slnf`), 31 tests, no database:
  - `RemovedBinariesTests`: the deleted files stay deleted; no `Reference`, `COMReference`, `HintPath`, `Content` or `None` item names them; no source imports them; neither the test's output nor `OpenDental/bin/Debug` holds them or `Windows.winmd`; the `Windows.winmd` reference is `Private=False`; ServiceManager has no installutil reference; the PasswordVault wrapper loads without the copied winmd.
  - `CompiledReferencesTests`: `OpenDentBusiness`, `CodeBase`, `PasswordVaultWrapper` and, when built, `OpenDental.exe`, `ServiceManager.exe` and `SparksToothChart.dll` reference none of the removed assemblies (read with `GetReferencedAssemblies`); Word is late bound (no interop types in source, `GetWordApp` returns object, the four constants); AvaTax stays disabled.
  - `ODZipTests`: round trip, entry names and order, the default code page bytes, `GetSafeExtractPath` refusing 14 hostile names, `ExtractAll` writing nothing from an archive with a traversal entry, overwrite rules, and the inflate limit.
  - `TopazWrapperTests`: the message text; absent (exception, null control, the signature box label and its restore); present through the stand-in (loaded from a search folder at run time, the same call sequence as the compiled code, the private-key path of `FillSignatureEncodings`, a Topaz signature shown in the signature box); the default search order.
  - `TerminalConnectorTests`: the port settings, received bytes and special codes, `WaitFor` with one reply, bytes back as received, and opening a port that does not exist.
- `tests/ReleaseFlowTests/ReleaseArchiveTests.cs` (6 tests, no database): the archive layout of `BuildRelease`, the round trip through `ExtractArchive` and `VerifyFolder`, refusal of 9 hostile paths with nothing written outside the folder, refusal of duplicate, unlisted and oversized entries, the inside-manifest check, and an archive that is not a zip. The existing ReleaseFlowTests pass unchanged, including the database round trip through the published segments.
- Full build of `OpenDental.Local.slnf`: 0 errors. `tools/Test-NoHqHosts.ps1`: passed. `run-tests -Mode Core -Include ReleaseFlowTests`: see the final report of this run.
