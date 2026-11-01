// The product identity shared by every assembly that ships with Open PMS - Dental (package REL-FILES, docs/port-notes/REL-FILES.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Reflection;

//Linked into each shipped project as Properties\SharedAssemblyInfo.cs (<Compile Include="..\SharedAssemblyInfo.cs">). The project's own
//Properties\AssemblyInfo.cs keeps its title, description and version numbers, and must not repeat the attributes below.
//
//Never put AssemblyVersion or AssemblyFileVersion here. AssemblyVersion stays on each project's own line: 24.3.63.0 for OpenDental.exe and
//OpenDentBusiness.dll is the compatibility version (ForkVersions.CompatVersion) that startup and the database version checks compare, and
//OpenDental.exe's Win32 file version follows it (tests/ReleaseFlowTests/VersionSourceTests.cs).

[assembly: AssemblyProduct("Open PMS - Dental")]
[assembly: AssemblyCompany("Open PMS - Dental contributors")]
//Open Dental's copyright is kept; the fork's changes are added (NOTICE).
[assembly: AssemblyCopyright("Copyright (C) 2003 and later years, Jordan Sparks, D.M.D., Open Dental Software, Inc. and contributors. "
	+"Changes in Open PMS - Dental: Copyright (C) 2026 and later years, Open PMS - Dental contributors. GNU GPL v2 or later.")]
//The release (product) version of the program: our own scheme, shown in About (Application.ProductVersion) and written to release manifests.
//It is never compared with Open Dental version numbers; code that sends or compares a version uses ForkVersions.CompatVersion instead.
//See docs/release-process.md.
[assembly: AssemblyInformationalVersion("0.0")]
