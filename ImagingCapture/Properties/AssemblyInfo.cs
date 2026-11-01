// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ImagingCapture")]
[assembly: AssemblyDescription("Native image capture for the Imaging module: generic UVC camera layer, triggers and capture windows.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ImagingCapture")]
[assembly: AssemblyCopyright("GNU General Public License v2")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("e7ff4ef0-8a1e-471c-90be-da1cb4625d53")]
//In step with the program version, like ImagingDeviceManager. This assembly is new, so no prebuilt DLL in "Required dlls" was
//compiled against an older version of it.
[assembly: AssemblyVersion("24.3.63.0")]
[assembly: InternalsVisibleTo("ImagingCaptureTests")]
