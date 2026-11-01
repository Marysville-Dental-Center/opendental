# RDL-SRC: the report add-in (ODR) and the RDL engine and viewer, built from source

Package RDL-SRC, run before the rename. Branch `m16/rdl-src-1`, from `main` 9095f3e80. Sources: `report-release.md` 1.1 and 1.4 and `report-rename.md` 3.5 (the release and rename reviews), the call sites in this repository, and the in-repo source of ODR, RdlEngine and RdlViewer. No Open Dental binary was decompiled, disassembled or inspected beyond its file name and size. The prebuilt binaries were only run, in a scratch folder, to compare what they render with what the source renders (section 4).

## Why

- `Required dlls/ODR.dll` was a prebuilt GPL binary of `ODR/xODR.csproj`. RDL report definitions load it as their code module (`<CodeModule>ODR.dll</CodeModule>`, classes `ODR.Query`, `ODR.DataConnection`, `ODR.GetData`, `ODR.Format`, `ODR.Aggregate`). It binds to OpenDentBusiness, CodeBase and DataConnectionBase by name, so the coming rename would have broken every report that loads it. It also needed `MySql.Data` 8.0.11, which the program no longer ships (section 4).
- `Required dlls/RdlEngine.dll` and `RdlViewer.dll` were prebuilt LGPL 2.9.3 binaries (fyiReporting RDL Project). Their source is in `RdlEngine/` and `RdlViewer/`, but nobody had checked it against them. Now the shipped binaries are built from the shipped source.

## What changed

### 1. ODR (`ODR/`)

- **`xODR.csproj`**:
  - It is in `OpenDental.sln` and `OpenDental.Local.slnf`. The mappings are Debug|Any CPU, Debug|x86, Release|Any CPU and Release|x86 to the same configuration, like xODCrypt.
  - The project file is rewritten in the style of the newer projects: .NET Framework 4.8 (was 4.7.2), and `PlatformTarget` x86 when the platform is x86.
  - It references `Required dlls\MySqlConnector.dll` instead of the missing `MySql.Data.dll`.
  - The ClickOnce and bootstrapper leftovers and the stale `app.config` (binding redirects for 2.6.10 BCL facades, a 4.7.2 startup sku) are gone.
  - The assembly name stays **ODR** and the root namespace stays OpenDentalReporting. Every class stays `ODR.*`.
- **`ODR/AssemblyInfo.cs`** moves to `ODR/Properties/AssemblyInfo.cs`. It links `..\SharedAssemblyInfo.cs` like every shipped project of ours, so ODR.dll carries the product name, release version and copyright. Its own empty product, company and copyright lines are gone. `AssemblyVersion` stays 4.6.1, the version the audit listed for the prebuilt file.
- **`ODR/DataConnection.cs`**:
  - It uses `MySqlConnector` instead of `MySql.Data.MySqlClient`. Every type it uses has the same name there.
  - `GetODConnStr()` (named by report definitions as `=ODR.DataConnection.GetODConnStr()`) and the instance constructor share one connection string. It is the running program's own direct MySQL connection (`DataConnectionBase.DataConnection.GetCurrentConnectionString()`, when `HasDatabaseConnection` and the type is MySQL). It falls back to `FreeDentalConfig.xml` in the program folder, the only source before; this code is unchanged and now exists once instead of twice. INFERRED, see decision 2.
- **OpenDental** has a `ProjectReference` to `ODR\xODR.csproj`. No code in OpenDental uses ODR's types. The reference only puts ODR.dll (and its pdb) next to OpenDental.exe, where `RdlEngine` loads it by file name. Because ODR.dll is a reference, it is one of the inputs of OpenDental.exe's build stamp, so a rebuilt ODR.dll gives a new build id.
- **`Required dlls/ODR.dll` is deleted.**

### 2. RdlEngine (`RdlEngine/`) and RdlViewer (`RdlViewer/`)

- **Both projects** are in `OpenDental.sln` and `OpenDental.Local.slnf` with the same four mappings. Both already had Debug|x86 and Release|x86 property groups.
- **OpenDental** references `..\RdlEngine\RdlEngine.csproj` and `..\RdlViewer\RdlViewer.csproj`. They replace the HintPaths to `Required dlls\RdlEngine.dll` and `RdlViewer.dll`. Nothing else references them. `RdlDesign/ReportDesigner.csproj`, which is not in the solution, already used project references.
- **`RdlEngine.csproj`**:
  - .NET Framework 4.8 (was 4.5; Visual Studio 2022 no longer installs the 4.5 targeting pack).
  - The `MySql.Data` 6.9.9 reference is removed, with `packages.config` and `app.config` (a `DbProviderFactories` entry for MySql.Data).
  - No compiled file used MySql.Data. The only file that does, `Definition/DataSource.cs`, is not in the project.
- **`RdlEngine/Runtime/RdlEngineConfig.cs`**: the engine's built-in data provider list is used when no `RdlEngineConfig.xml` sits next to the program, and the program ships none. Its `MySQL.NET` provider now loads `MySqlConnector.dll` and creates `MySqlConnector.MySqlConnection` (was `MySql.Data.dll` and `MySql.Data.MySqlClient.MySqlConnection`). Report definitions keep naming the provider `MySQL.NET`. The sample `RdlEngine/RdlEngineConfig.xml`, which is not shipped, says the same.
- **`RdlViewer.csproj`**: .NET Framework 4.8 (was 4.5).
- **LGPL notices**: each changed file (both project files, `RdlEngineConfig.cs`, `RdlEngineConfig.xml`) says what was changed, by whom and when (LGPL 2.1 section 2a). The assemblies keep their own identity: AssemblyVersion 2.9.3, fyiReporting company and copyright. They do not link SharedAssemblyInfo, because they are not ours.
- **`Required dlls/RdlEngine.dll` and `RdlViewer.dll` are deleted.**
- **`THIRD-PARTY-NOTICES.txt`**, RDL Project: "2.9.3, built from RdlEngine/ and RdlViewer/ of this repository (changes are marked in the changed files)". It used to say "prebuilt". The corresponding source line is unchanged.
- **`docs/third-party-binaries-audit.md`**: the three rows say Done (RDL-SRC).

### 3. Tests

- **`tests/ReportTests/RdlReportRenderTests.cs`** (database).
  - **Scratch database** (`RdlReportDb.cs`): built from the 24.3.63 seed (`UnitTests/Resources/dump.txt` and the frozen convert chain), then the fork migration runner. It uses the shared `tests/ForkMigrationTests/TestDb.cs`, linked into ReportTests. Its name is `forkmig_<run id>_rdlsrc` and it is dropped at the end of the run.
  - **Synthetic rows**: one patient; a treatment-planned procedure in 2025 that must show, plus a completed one and an older planned one that must not; five accounts with 2006 and 2007 journal entries. The accounting reports are hard-coded to 2007.
  - **Engine tests**: each report goes through `RDLParser.Parse`, `Report.RunGetData` and `Report.BuildPages`, the calls `RdlViewer.GetReport` and `GetPages` make. Each must give pages and no engine message of any severity. The pages must show:
    - **Unsched Treat Plans**: the patient, code, description and fee in the date range, and nothing outside it.
    - **Balance sheet**: the practice title from `ODR.GetData.Pref`, the balances, retained earnings 400.00 from `ODR.GetData.RetainedEarningsAuto`, and net income 800.00 from `ODR.GetData.NetIncomeThisYear`.
    - **General ledger**: every account, and the `ODR.Aggregate.RunningSumForAccounts` running balances 5,700.00, 6,700.00 and 6,500.00.
  - **Viewer tests**: each report goes into the `RdlViewer` control the way `FormReportForRdl` uses it (`SourceRdl`, `ShowParameterPanel=false`, `Rebuild`). Each must give pages and keep no message.
  - **The parameter window**: `ODR.Query` asks for the dates in a modal window (`ODR.FormParameterInput`). The test fills it in from the render thread's message loop (`Application.Idle`) and presses OK. A render that does not finish in 3 minutes fails the test instead of hanging the run.
  - **The connection string**: the accounting reports leave it as the placeholder `@@@ConnectionString@@@` (section 5). The tests fill it in with `ODR.DataConnection.GetODConnStr()`.
- **`tests/ReportTests/RdlSourceBuildTests.cs`** (no database) pins that:
  - the three prebuilt files are gone from `Required dlls`;
  - no `.csproj`, `.vcxproj`, `.vbproj`, `.props` or `.targets` file has a HintPath, file reference or copy item for `ODR.dll`, `RdlEngine.dll` or `RdlViewer.dll`;
  - OpenDental has project references to all three, with the output copied;
  - the three are in the `.sln` (with the Debug|x86 and Release|x86 mappings) and the `.slnf`;
  - no compiled file and no project file of the three uses MySql.Data, and the built-in `MySQL.NET` provider is `MySqlConnector.MySqlConnection`;
  - ODR keeps its assembly name, `ODR.Query` with both `Get` overloads, and every `ODR.Class.Method(` the three report definitions call;
  - ODR.dll references MySqlConnector, OpenDentBusiness, CodeBase and DataConnectionBase, and not MySql.Data;
  - `OpenDental\bin\Debug` holds ODR.dll, RdlEngine.dll and RdlViewer.dll byte for byte as their projects built them.
- **`tests/ReleaseFlowTests/ReleaseFilesTests.cs`**: ODR is a shipped project (it links SharedAssemblyInfo and keeps version 4.6.1), and ODR.dll is one of the built assemblies that must carry the product name.

**Results** (targeted, `run-tests.ps1 -Mode Targeted`):
- ReportTests: 45 of 45 passed (the 14 new tests, plus the 31 that were already there).
- NoHqTests: 53 of 53.
- ReleaseFlowTests: 89 of 89.
- AttributionTests: 32 of 32.

The full build and the host check results are under Checks, at the end of this note.

## 4. Does the source render the program's reports the same way as the prebuilt binaries? Yes

**How it was checked.** This was a one-off check with a scratch harness in the session's scratchpad, not part of the repository.
- The harness renders the three reports through the engine API (it dumps every page item: type, position, size and text) and through the `RdlViewer` control. It also prints every page with `RdlViewer.Print` to a `PreviewPrintController` and hashes the pixels of each page.
- The scratch database `rdlcmp_<time>` was built the same way as the test database, with the same rows, and was dropped afterwards.
- The harness ran in three copies of the build output:

| Copy | RdlEngine.dll, RdlViewer.dll | ODR.dll |
|---|---|---|
| src | built from source | built from source |
| pre | prebuilt, from `main` | prebuilt, from `main` |
| mix | prebuilt | built from source |

**Results:**
- **Source engine and viewer against the prebuilt ones** (src against mix, both with an `RdlEngineConfig.xml` that points `MySQL.NET` at MySqlConnector): **identical**. That covers all 211 dump lines: every page item's position, size and text, the messages (none), the page counts and the number of parameter windows. The pixel hash of every printed page also matched.
- **As shipped, with no `RdlEngineConfig.xml`**:
  - The source renders every report with data and no message. Its output is identical to the run with the configuration file.
  - The prebuilt engine cannot connect at all. The messages are "DataSource 'DS1'. The system cannot find the file specified" and "Unable to connect to datasource". Its built-in `MySQL.NET` provider evidently looks for a file that is not there. The source's built-in list named `MySql.Data.dll`, which the program does not ship: its own MySQL client is MySqlConnector. Every report showed "Query returned no rows!". `FormReportForRdl` hides these messages, because its parameter panel is off.
- **The prebuilt ODR.dll** fails in this program even with a working provider. The message is "Could not load file or assembly 'MySql.Data, Version=8.0.11.0'" from `ODR.GetData.*`, and `ODR.DataConnection.GetODConnStr()` throws as well.
- So before this package, the RDL reports that use ODR or the `MySQL.NET` provider showed no data. That covers the three resources, and an office's own `.rdl` files opened from the A to Z folder's report folder (`FormOpenDental.menuItemRDLReport_Click`). With the source build they work. This is a fix, not a change in how a working report looks.

**Behaviour the source shares with the prebuilt engine** (not changed; see owner question 1): a report whose query calls `ODR.Query.Get` with parameters asks for the parameters more than once.
- ODR asks once per `ODR.Query` instance.
- The engine creates one instance when it compiles the report (`Query.FinalPass` runs the query for its column list) and another when it runs it.
- `RdlViewer.Rebuild` runs it once more.
- So through the engine API the window opens twice, and through the viewer as `FormReportForRdl` uses it, three times. The tests pin these counts (2 and 3), so a fix has to update them on purpose.

## INFERRED decisions

1. **ODR.dll reaches the output through an ordinary project reference.** The alternative was a build-order-only reference plus a copy target, like UpdateFileCopier. The ordinary reference also copies the pdb and makes ODR.dll a build-stamp input. OpenDental uses none of ODR's types.
2. **ODR uses the program's live connection first.** Before, it used only `FreeDentalConfig.xml`.
   - ODR's own comment said this would go wrong with several copies open. It also goes wrong when the database is chosen at startup without saving the file, or given on the command line.
   - The program's connection string also has the program's options, such as "Convert Zero Datetime", so a `0000-00-00` date no longer stops a report.
   - Without a direct connection (a middle tier client, or ODR loaded by another host), the file is read as before.
   - It also made the tests possible: under the test host, `Application.StartupPath` is the test platform's folder.
3. **ODR links SharedAssemblyInfo and is in ReleaseFilesTests' shipped list.** It is our own GPL assembly in the program folder. It keeps AssemblyVersion 4.6.1, because report definitions load it by file name, so its version is never compared.
4. **RdlEngine and RdlViewer keep fyiReporting's identity** (version 2.9.3, company, copyright) and get change notices in the files we changed. They do not get our product name.
5. **RdlEngine and RdlViewer target .NET Framework 4.8**, so a fresh Visual Studio 2022 machine can build them. Section 4 showed the same rendering.
6. **ODR.dll, RdlEngine.dll and RdlViewer.dll are not added to `ForkReleases.ListBuildIdFiles`.** Each is a reference of OpenDental.exe, so a rebuilt one already changes OpenDental.exe's build stamp and therefore the build id.
7. **The 8 old files in `RdlEngine/Definition` that the project does not compile are left alone**: `DataSet.cs`, `DataSets.cs`, `DataSource.cs`, `DataSources.cs`, `Pages.cs`, `Report.cs`, `StyleInfo.cs` and `XmlUtil.cs`. They duplicate compiled files elsewhere. `DataSource.cs` still says `using MySql.Data.MySqlClient`. The source test checks compiled files only. See owner question 3.

## Owner questions

1. **Parameter window shown two or three times.** Should ODR remember the parameters it was given for the same report run? For example, it could keep the last values per query text for a few seconds, or the viewer could build the report once. This is pre-existing behaviour of the 2.9.3 engine with ODR, unchanged here.
2. **The three report resources are dead in the program.** `Properties.Resources.Unsched_Treat_Plans`, `ReportAccountingBalanceSheet` and `ReportAccountingGenLedger` are embedded, but no code opens them. The accounting reports are hard-coded to 2007 and carry an `@@@ConnectionString@@@` placeholder that nothing fills in. Keep them as samples, or delete them? Only `menuItemRDLReport_Click` (an office's own `.rdl` files) reaches the RDL viewer.
3. **Delete the 8 old files in `RdlEngine/Definition`** that are not compiled (INFERRED 7)? One of them carries an older GPL header instead of the LGPL one.
4. **`Required dlls/RdlDesigner.exe`.** It is the prebuilt report designer, built against the prebuilt engine. It is not shipped, and `RdlDesign/` holds its source, which is not in the solution. Delete it, or build RdlDesign from source too?
5. **ODR's error handling.** `ODR.DataConnection.GetTable` shows the failed query in a MessageBox and returns an empty table. The `Integer` parameter type stores a `long` and then unboxes it as `int`, which throws. Leave these as they are, or fix them?

## For the rename

- ODR is now built with the program, so after the rename it binds to the renamed assemblies by itself.
- The rename must still keep the file name `ODR.dll`, the assembly name ODR and the namespace and class names `ODR.*`. They are named inside report definitions, including offices' own. `RdlSourceBuildTests.Odr_KeepsTheNamesReportDefinitionsUse` fails if they change.
- The `MySQL.NET` provider name and `MySqlConnector.MySqlConnection` are not ours to rename.
- The project names in the solution (`xODR`, `RdlEngine`, `RdlViewer`) are what the build lane's `-Projects` takes.

## Checks

- **Full build** (`odbuild.ps1`, no `-Projects`, `OpenDental.Local.slnf` Debug|x86): succeeded, 0 errors, 116 projects.
- **Host check** (`tools/Test-NoHqHosts.ps1`): passed. It scanned 7186 files in 114 projects and found 26 allowed references.
- **Targeted tests after the full build**: ReportTests 45/45, NoHqTests 53/53, ReleaseFlowTests 89/89, AttributionTests 32/32. The full suite was not run, as the owner asked.
- **Scratch databases**: none left behind (`forkmig_*_rdlsrc` and `rdlcmp_*` checked).
