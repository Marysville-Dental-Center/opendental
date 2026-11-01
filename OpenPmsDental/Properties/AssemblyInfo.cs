using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Db Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("OpenPmsDental")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
//AssemblyProduct, AssemblyCompany, AssemblyCopyright and AssemblyInformationalVersion come from SharedAssemblyInfo.cs in the repository root.
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("7ac7c168-694c-434b-b8cc-c351dd7fe115")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//

//The following line was added because OD was looking mangled when scaling was set above 150%. This line might make not make WPF scale as well,
//so we might consider removing it when a significant portion of OD is in WPF.
[assembly: System.Windows.Media.DisableDpiAwareness]

//AssemblyVersion stays on the Open Dental 24.3.63 compatibility line, which the database version checks use until the fork markers take over
//(OpenDentBusiness ForkVersions.CompatVersion); the Win32 file version follows it. The release (product) version, "0.0", is the
//AssemblyInformationalVersion in SharedAssemblyInfo.cs in the repository root. See docs/release-process.md.
[assembly: AssemblyVersion("24.3.63.0")]

//[assembly: AssemblyFileVersion("1.0.0.0")]
