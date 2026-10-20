// ODCrypt: open source replacement for the prebuilt ODCrypt.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ODCrypt")]
[assembly: AssemblyDescription("Standard hash, random and compression helpers (MD5, SHA3-512, salts, gzip).")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ODCrypt")]
[assembly: AssemblyCopyright("GNU GPL v2 or later")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("c37b8157-f665-4ebe-8fd6-20d1c92a0797")]
//Keep in step with the program version, and never lower than the version the prebuilt DLLs in "Required dlls" were compiled
//against (24.3.x; the Bridges.dll and OpenDentalHelp.dll copies that set this floor are deleted). A lower version makes MSBuild treat this build as a
//conflicting older copy and silently leave the DLL out of any output that gets it only through another assembly.
[assembly: AssemblyVersion("24.3.63.0")]
[assembly: AssemblyFileVersion("24.3.63.0")]
[assembly: InternalsVisibleTo("CryptoTests")]
