# M16-INTEGRATION: RDL-SRC, CLAIM-DCM and CAP-16 merged

Branch `m16/integration`, from `main` (9095f3e80). Three verified branches were merged with `git merge --no-ff`, in this order:

| # | Branch | Head | Package | Note |
|---|---|---|---|---|
| 1 | `m16/rdl-src-1` | 0b7ede4a0 | RDL-SRC: ODR, RdlEngine and RdlViewer built from source | [RDL-SRC.md](RDL-SRC.md) |
| 2 | `m16/claim-dcm-1` | 91ba296b4 | CLAIM-DCM: DICOM X-rays on insurance claims | [CLAIM-DCM.md](CLAIM-DCM.md) |
| 3 | `cap/bits-16` | 3bcf8dfbc | CAP-16: 16 bits by default, falling back to 14 or 8 | the imaging session's hand-off (not in the repository); the runbook `docs/imaging/intraoral-capture-setup.md` and `docs/imaging-capture-owner-questions.md` |

All three are merged; none was reverted. No feature or test of any package was dropped. Nothing is renamed: the rename of the internal code
names comes next.

## Conflicts and how they were resolved

- **None.** All three branches start at `main` 9095f3e80 (`cap/bits-16` has it merged in), and no file was changed by two of them in
  overlapping places. `OpenDental.sln`, `OpenDental.Local.slnf` and `OpenDental/OpenDental.csproj` were changed by RDL-SRC and CLAIM-DCM and
  merged cleanly (the sln and slnf through the local merge driver).
- **Solution checks after each merge** (a script over `OpenDental.sln` and `OpenDental.Local.slnf`): no `ODXam` line, `Project(` and
  `EndProject` counts equal, no configuration line for a GUID without a `Project(` entry, CRLF only, every slnf entry in the sln and none twice.
  After RDL-SRC: 116 projects (xODR, RdlEngine, RdlViewer added, 8 configuration lines each). After CLAIM-DCM: 117 (CLAIMDCMTests added).
  After CAP-16: 117. `OpenDental.csproj` has both RDL-SRC's three project references and CLAIM-DCM's `Logic\ClaimAttachDicomL.cs`.

## Integration commits

1. **The capture design and the owner questions after CAP-16** (the imaging session's replacement text, line numbers as on d9c177ca6; the
   design document has not changed since). Only the sections the hand-off names: `docs/imaging-capture-design.md` 7.1 (CAP-3), 7.8 (anatomy,
   BitsStored rule, Test capture, "When 16-bit is enabled" and "Until then"), 7.10, 7.12 (the "16-bit refused" row and the new "layout not
   usable" row), 9.3 (verification records, hash defaults, sample ini), 11.4, section 13 (two bullets, step 8, the 16-bit DICOM gate row),
   section 14 (the windowing risk) and section 15 row 18; and the CAP design 6 Today line of `docs/imaging-capture-owner-questions.md` (16 bits
   by default, patient computers at `Allow16Bit=0` until counsel and the clinic run sign off; TWAIN-W is on main). No vendor, product or
   private-project name was added.
2. **CLAIM-DCM follow-ups** (package review):
   - `MountHelper.GetBitmapOfMountFromDb(mountNum,list)`: the DICOM branch converts a pre-21.4 crop with `ConvertCropIfNeeded(...,doSave:false)`,
     so attaching a mount never writes the DICOM document's row;
   - `FormClaimAttachment.CreateAndSendAttachments` (EDS): `ClaimAttachDicom.ThrowIfAnyIsDicom` runs first, before `EDS.GetAttachmentID`, the
     claim's AttachmentID and AttachedFlags and `Claims.Update`, so a refused attachment never leaves the claim with an AttachmentID and nothing
     sent;
   - `ClaimConnect.CreateAttachment(listImages,...)` (no callers): the same send guard.
   Tests: three source checks in `CLAIMDCMTests.WiringTests` (the windows and the network sends are not run in tests).
3. **RDL-SRC's source tests find the repository by `LICENSE` and `NOTICE`.** `RdlSourceBuildTests.Root()` still looked for `OpenDental.sln`,
   against REN-PROOF's rule that main follows everywhere else (PRE-INTEGRATION commit 3). Reading the sln and slnf by name stays, as in
   BINATests and VendorDirectTests.
4. **RDL-SRC follow-up** (package review): `README.md` lists `ODR/`, `RdlEngine/` and `RdlViewer/` as built from source (they left the "not
   built" row); the upgrade comments of `FormReportForRdl.cs` and `FormReportCustom.cs` say the three are built here and show the `MySQL.NET`
   data source with MySqlConnector, as `RdlEngineConfig.cs` has it; `docs/third-party-binaries-audit.md`'s ODR.dll, RdlEngine.dll and
   RdlViewer.dll rows name OpenDental's ProjectReference and say "built from source", and its missing-dependency note marks the MySql.Data
   HintPaths resolved. Appendix A (the version snapshot as audited) is unchanged, as for every deleted file. Test:
   `ReportTests.RdlSourceBuildTests.Docs_SayTheThreeAreBuiltFromSource`.
5. **CAP-16 meets CLAIM-DCM** (neither branch had this test): `tests/CLAIMDCMTests/SessionCaptureToClaimTests.cs` saves a synthetic 16-bit
   capture and a 14-bit one (in 16-bit containers, no `effectiveBits` in the metadata) the way the capture session saves them, through
   `RadiographImport.Save` (RadiographSavePlanner, DicomIntraOralWriter, the stored 0-255 window) into a mount slot, with an in-memory store that
   writes into a temporary A to Z folder. Each goes through Edit Claim's copy, DentalXChange and EDS, and each gets an 8-bit JPEG of the right
   size whose values are what the Imaging module shows for the stored window (16-bit: window 8000-40000 stored as 31-157, bands 1, 127, 253,
   within 2 of the session's own preview; 14-bit: BitsStored 16, `effectiveBits=14` in Image Comments, window 2000-10000 stored as 7-40, bands 6,
   127, 248). The send guards pass the copies, the saved file is DICOM, and neither the file nor the document changes. No database, no network.

## Checks

- **Stale build output**: this worktree had no earlier build output, so there was no stale copy of Windows.winmd, Sparks3D, SigPlusNET,
  Interop.Word, stdole or the prebuilt ODR.dll, RdlEngine.dll or RdlViewer.dll to delete. After the build none of the removed binaries is under
  any bin or obj folder, and `OpenDental\bin\Debug` holds ODR.dll, RdlEngine.dll and RdlViewer.dll as built from source.
- **Full build** of `OpenDental.Local.slnf` through the build lane: 0 errors (`odbuild-20261101-152102-24016.log`). No new warning in the
  changed files (FormClaimAttachment.cs has one older CS0168 at line 500, outside the change).
- **`run-tests -Mode Full`** (`.git/od-tools/test-results/m16_integration.txt`): 88 assemblies, 5,804 tests, 5,802 passed, 0 failed, M201Tests 2
  skipped as on earlier heads (the two published-document reference checks). Among them CLAIMDCMTests 31/31, ReportTests 46/46,
  ImagingCaptureTests 801/801, ImagingDeviceManagerTests 525/525, ImagingTests 43/43, M2IMGTests 25/25, DicomReaderTests 42/42, VDXC2Tests
  31/31, VEDSTests 83/83, BCLAIMTests 55/55, ReleaseFlowTests 89/89, NoHqTests 53/53, AdoptionValidation.Tests 64/64. Before it, one Targeted
  run of CLAIMDCMTests (31/31) and NoHqTests (53/53) checked the new tests. Nothing failed, so nothing was re-run.
- **`tools/Test-NoHqHosts.ps1`**: passed (7,196 files in 115 projects, 26 allowed references).
- **`tools/Test-NoOldNames.ps1`** (report mode): 27,043 old names on 26,438 lines in 5,787 files outside the allow-list, 3,420 allowed (8,110
  files scanned); exit 0. PRE-INTEGRATION recorded 26,983 on main before these packages.
- **Migration check**: DevDbTool built through the lane (`-Solution tools/DevDbTool/DevDbTool.csproj`), then
  `tools/New-DevDatabase.ps1 -Name m16_<digits> -Force -SkipBuild -NoAtoZ`: exit 0, 16 fork steps, fork schema level 2026110104, schema of Open
  Dental 26.1.70 (14 documented exceptions), 441 MyISAM tables. Dropped.
- **Adoption rehearsal**: AdoptionValidation built through the lane, `Invoke-AdoptionRehearsal.ps1 -Size medium -Runs 1 -Prefix
  synth_m16i<digits>_ -SkipBuild`: PASS, 31 of 31 checks, 36 s. The tool dropped its databases; none with the prefix is left.

## Left as they are (not named by the hand-off or the reviews)

- `docs/imaging-capture-owner-questions.md`, CAP design 7: its Today line still says TWAIN-W is "not merged yet". The hand-off asked for that
  change in CAP design 6 only.
- `docs/imaging-capture-design.md` 7.8, condition 5 of "When 16-bit is enabled": still says claim attachment "rejects .dcm" today. The
  hand-off's new paragraph after the list says condition 5 ships with the 16-bit default; the condition's own text was not named.

## Owner questions

None new. The packages' own owner questions are in their notes (RDL-SRC 1 to 5, CLAIM-DCM 1 to 6) and, for CAP-16, in
`docs/imaging-capture-owner-questions.md` and the imaging session's hand-off ("Decisions the implementer made, for review", 1 to 8).
