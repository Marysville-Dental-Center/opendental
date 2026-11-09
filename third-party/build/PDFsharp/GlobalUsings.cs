// The global usings the .NET SDK generates for upstream's ImplicitUsings=enable (src\Directory.Build.props of PDFsharp), for its .NET
// Framework builds. The project files of third-party\build leave ImplicitUsings off (Directory.Build.props), so the Open PMS - Dental build
// of PDFsharp and MigraDoc compiles this file into each assembly (third-party\build\PDFsharp.props). Part of the build, not of the upstream
// source.
global using global::System;
global using global::System.Collections.Generic;
global using global::System.IO;
global using global::System.Linq;
global using global::System.Threading;
global using global::System.Threading.Tasks;
