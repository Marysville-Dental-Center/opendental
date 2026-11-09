// The two global usings the .NET SDK adds for UseWindowsForms=true with ImplicitUsings=enable: PdfSharp-gdi.csproj and
// PdfSharp.Charting-gdi.csproj of PDFsharp keep them (MigraDoc.Rendering-gdi.csproj removes them). The project files of third-party\build
// leave ImplicitUsings off and do not use UseWindowsForms, so the Open PMS - Dental build compiles this file into those two assemblies. Part
// of the build, not of the upstream source.
global using global::System.Drawing;
global using global::System.Windows.Forms;
