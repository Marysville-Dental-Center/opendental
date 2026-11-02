// Tests for the v0.0 installer (docs/release-process.md, "Building the installer"; docs/release-notes/v0.0.md): the list checks of
// tools/Build-Release.ps1, run against fake staging folders, and what installer/OpenPmsDental.iss and its information page must keep saying.
// The fake files are synthetic text. No setup is run here (tools/Test-Installer.ps1 does that) and no database is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class InstallerTests {
		///<summary>The attribution, word for word as in README.md, NOTICE and the About box (AttributionTests).</summary>
		private const string ATTRIBUTION="Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under "
			+"the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.";

		///<summary>One file for each kind the release must never ship (report-release.md section 2, docs/port-notes/BIN-A.md and BIN-B.md): Open
		///Dental's closed binaries, the commercial and non-redistributable files, local settings, debug symbols, the uninstaller and the pre-rename
		///names.</summary>
		private static readonly string[] _arrayDenied=new string[] {
			"xImageDeviceManager.dll","SuniUSB.dll","VBbridges.dll","VideoCamLib.dll","Sparks3D.dll","Sparks3DXEngine.dll","EZT4Jpeg.dll","EZT4Pdf.dll",
			"Eztwain4.dll","Bridges.dll","CDT.dll","stdole.dll","Interop.Word.dll","Interop.Microsoft.Office.Core.dll","Interop.VBIDE.dll",
			"Interop.QBFC10.dll","AxInterop.AcroPDFLib.dll","Interop.AcroPDFLib.dll","Windows.winmd","installutil.exe","vcruntime140.dll","MSVCR71.dll",
			"RS232.dll","SigPlusNET.dll","zxing.dll","zxing.presentation.dll","Avalara.AvaTax.RestClient.net45.dll","Ionic.Zip.dll","TWAINDSM.dll",
			"Tamir.SharpSSH.dll","Org.Mentalis.Security.dll","DiffieHellman.dll","OpenPop.dll","BouncyCastle.Crypto.dll","Google.Apis.PlatformServices.dll",
			"Google.Apis.Auth.PlatformServices.dll","FreeDentalConfig.xml","OpenPmsDental.pdb","unins000.exe","unins000.dat","OpenDental.exe","OpenDentBusiness.dll",
		};

		///<summary>The program folder's documents, staged from the repository.</summary>
		private static readonly string[] _arrayDocuments=new string[] {
			"LICENSE","LICENSE-LGPL-2.1.txt","NOTICE","THIRD-PARTY-NOTICES.txt",@"db\init\new-database.sql",@"db\README.md",
		};

		///<summary>The file the GNU LGPL 2.1 text ships as (LGPL 2.1 sections 1 and 4: a copy of the licence with every copy of the library).</summary>
		private const string LGPL_FILE="LICENSE-LGPL-2.1.txt";

		///<summary>SHA-256 of LICENSE-LGPL-2.1.txt with LF line ends: the FSF's text of the LGPL 2.1 as gnu.org publishes it, byte for byte the
		///copies that xz, gettext, libiconv and libtasn1 ship. A changed byte means the text is no longer the licence verbatim.</summary>
		private const string LGPL_SHA256_LF="20e50fe7aae3e56378ebf0417d9de904f55a0e61e4df315333e632a4d3555d95";

		///<summary>The components of THIRD-PARTY-NOTICES.txt that ship under the LGPL 2.1, and so with its text.</summary>
		private static readonly string[] _arrayLgplComponents=new string[] { "NHunspell and Hunspell","Hunspell en_US dictionary","RDL Project" };

		#region Helpers
		private class ScriptResult {
			public int ExitCode;
			public string Output;
			public List<string> Lines;
		}

		private static string Quote(string arg) {
			return "\""+arg+"\"";
		}

		private static ScriptResult RunBuildRelease(params string[] arrayArgs) {
			string script=Path.Combine(TestPaths.RepoRoot,"tools","Build-Release.ps1");
			ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe");
			processStartInfo.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(script)+" "+string.Join(" ",arrayArgs);
			processStartInfo.UseShellExecute=false;
			processStartInfo.RedirectStandardOutput=true;
			processStartInfo.RedirectStandardError=true;
			processStartInfo.CreateNoWindow=true;
			using(Process process=Process.Start(processStartInfo)) {
				Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				Task<string> taskErr=process.StandardError.ReadToEndAsync();
				if(!process.WaitForExit(120000)) {
					process.Kill();
					Assert.Fail("Build-Release.ps1 did not finish in 2 minutes.");
				}
				process.WaitForExit();
				ScriptResult scriptResult=new ScriptResult();
				scriptResult.ExitCode=process.ExitCode;
				scriptResult.Output=taskOut.Result+taskErr.Result;
				scriptResult.Lines=scriptResult.Output.Split(new[] { "\r\n","\n" },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
				return scriptResult;
			}
		}

		///<summary>The allow-list as Build-Release.ps1 -ListFiles prints it: kind ("own", "third", "doc") and path.</summary>
		private static List<KeyValuePair<string,string>> ListFiles() {
			ScriptResult scriptResult=RunBuildRelease("-ListFiles");
			Assert.AreEqual(0,scriptResult.ExitCode,scriptResult.Output);
			List<KeyValuePair<string,string>> listFiles=new List<KeyValuePair<string,string>>();
			foreach(string line in scriptResult.Lines) {
				string[] arrayParts=line.Split('|');
				Assert.AreEqual(2,arrayParts.Length,"Unexpected line: "+line);
				listFiles.Add(new KeyValuePair<string,string>(arrayParts[0],arrayParts[1]));
			}
			return listFiles;
		}

		///<summary>A fake staging folder with every allowed file: synthetic text named after the file, and the real THIRD-PARTY-NOTICES.txt.</summary>
		private static string NewFakeStaging(string name) {
			string folder=TestPaths.NewTempFolder(name);
			foreach(KeyValuePair<string,string> pair in ListFiles()) {
				string path=Path.Combine(folder,pair.Value);
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				if(pair.Value=="THIRD-PARTY-NOTICES.txt") {
					File.Copy(Path.Combine(TestPaths.RepoRoot,"THIRD-PARTY-NOTICES.txt"),path);
				}
				else {
					File.WriteAllText(path,"synthetic "+pair.Value);
				}
			}
			return folder;
		}

		private static ScriptResult CheckFolder(string folder) {
			return RunBuildRelease("-CheckFolder",Quote(folder));
		}

		private static void DeleteFolder(string folder) {
			try {
				Directory.Delete(folder,true);
			}
			catch(IOException) { }
			catch(UnauthorizedAccessException) { }
		}

		private static string ReadIss() {
			return TestPaths.ReadSource("installer","OpenPmsDental.iss");
		}

		///<summary>The value of a [Setup] directive (the first line "Name=value").</summary>
		private static string SetupDirective(string iss,string name) {
			Match match=Regex.Match(iss,"(?m)^"+Regex.Escape(name)+@"=(.*?)\r?$");
			Assert.IsTrue(match.Success,"No [Setup] directive "+name);
			return match.Groups[1].Value.Trim();
		}

		private static string ReleaseVersion() {
			Match match=Regex.Match(TestPaths.ReadSource("SharedAssemblyInfo.cs"),@"AssemblyInformationalVersion\(""([^""]+)""\)");
			Assert.IsTrue(match.Success);
			return match.Groups[1].Value;
		}
		#endregion Helpers

		#region Build-Release.ps1 lists
		[TestMethod]
		public void AllowList_ShipsTheRuntimeFiles_TheDocuments_AndEveryBuildIdFile() {
			List<KeyValuePair<string,string>> listFiles=ListFiles();
			List<string> listPaths=listFiles.Select(x => x.Value).ToList();
			foreach(string name in new[] { "WebView2Loader.dll","Hunspellx86.dll","ODR.dll","OpenPmsDental.exe","OpenPmsDental.exe.config","UpdateFileCopier.exe",
				"ImagingDeviceManager.dll","ImagingCapture.dll","RdlEngine.dll","RdlViewer.dll","Direct2dWrapper.dll" })
			{
				CollectionAssert.Contains(listPaths,name);
			}
			foreach(string path in _arrayDocuments) {
				Assert.IsTrue(listFiles.Any(x => x.Key=="doc" && x.Value==path),"Not staged: "+path);
			}
			foreach(string name in ForkReleases.ListBuildIdFiles) {
				Assert.IsTrue(listFiles.Any(x => x.Key=="own" && x.Value==name),"A build id file is not shipped as the program's own: "+name);
			}
			Assert.AreEqual(listPaths.Count,listPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(),"No file twice.");
			foreach(string path in listPaths) {
				string name=Path.GetFileName(path);
				Assert.IsFalse(_arrayDenied.Contains(name,StringComparer.OrdinalIgnoreCase),"Allowed although denied: "+path);
				Assert.IsFalse(name.EndsWith(".pdb",StringComparison.OrdinalIgnoreCase),path);
				Assert.IsFalse(name.StartsWith("unins",StringComparison.OrdinalIgnoreCase),"The uninstaller is not staged: "+path);
				Assert.IsFalse(name.StartsWith("OpenDent",StringComparison.OrdinalIgnoreCase),"A pre-rename name: "+path);
			}
			Assert.IsTrue(listFiles.Where(x => x.Key!="doc").All(x => !x.Value.Contains("\\")),"The program's files are all at the top level.");
		}

		[TestMethod]
		public void CheckFolder_PassesForTheAllowList_AndPrintsTheBuildIdOfForkReleases() {
			string folder=NewFakeStaging("pass");
			try {
				ScriptResult scriptResult=CheckFolder(folder);
				Assert.AreEqual(0,scriptResult.ExitCode,scriptResult.Output);
				CollectionAssert.Contains(scriptResult.Lines,"The check passed.");
				//The fake build id files differ from each other, so a list that differs from ForkReleases.ListBuildIdFiles gives another id.
				CollectionAssert.Contains(scriptResult.Lines,"Build id: "+ForkReleases.ComputeBuildId(folder),scriptResult.Output);
			}
			finally {
				DeleteFolder(folder);
			}
		}

		[TestMethod]
		public void CheckFolder_FailsForEveryDeniedFile_AnUnknownFile_AndASparks3DFolder() {
			string folder=NewFakeStaging("deny");
			try {
				foreach(string name in _arrayDenied) {
					File.WriteAllText(Path.Combine(folder,name),"synthetic");
				}
				File.WriteAllText(Path.Combine(folder,"Unknown.dll"),"synthetic");
				Directory.CreateDirectory(Path.Combine(folder,"Sparks3D"));
				File.WriteAllText(Path.Combine(folder,"Sparks3D","readme.txt"),"synthetic");
				Directory.CreateDirectory(Path.Combine(folder,"uninstall"));
				ScriptResult scriptResult=CheckFolder(folder);
				Assert.AreEqual(1,scriptResult.ExitCode,scriptResult.Output);
				foreach(string name in _arrayDenied) {
					Assert.IsTrue(scriptResult.Lines.Any(x => x.StartsWith("Denied file: "+name+" (")),"Not refused: "+name+"\r\n"+scriptResult.Output);
				}
				CollectionAssert.Contains(scriptResult.Lines,"Not on the allow-list: Unknown.dll");
				Assert.IsTrue(scriptResult.Lines.Any(x => x.StartsWith("Denied folder: Sparks3D (")),scriptResult.Output);
				CollectionAssert.Contains(scriptResult.Lines,"Unexpected folder: uninstall");
				Assert.IsFalse(scriptResult.Lines.Any(x => x.StartsWith("Missing:")),"Every allowed file is there.\r\n"+scriptResult.Output);
				Assert.IsFalse(scriptResult.Lines.Any(x => x.StartsWith("Not named in THIRD-PARTY-NOTICES.txt")),scriptResult.Output);
			}
			finally {
				DeleteFolder(folder);
			}
		}

		///<summary>A file that an earlier release shipped and this one does not (ForkReleases.ListRetiredFiles, such as the storage library of
		///v0.0) is never staged: it is not on the allow-list, and a leftover copy in the build output or the staging folder is refused with a
		///reason.</summary>
		[TestMethod]
		public void CheckFolder_RefusesARetiredFile() {
			Assert.IsTrue(ForkReleases.ListRetiredFiles.Count>0);
			List<string> listPaths=ListFiles().Select(x => x.Value).ToList();
			foreach(string name in ForkReleases.ListRetiredFiles) {
				Assert.IsFalse(listPaths.Contains(name,StringComparer.OrdinalIgnoreCase),"A retired file is on the allow-list: "+name);
			}
			CollectionAssert.Contains(listPaths,ProgramNames.CloudDllFileName,"The storage library ships under its new name.");
			string folder=NewFakeStaging("retired");
			try {
				foreach(string name in ForkReleases.ListRetiredFiles) {
					File.WriteAllText(Path.Combine(folder,name),"synthetic leftover");
				}
				ScriptResult scriptResult=CheckFolder(folder);
				Assert.AreEqual(1,scriptResult.ExitCode,scriptResult.Output);
				foreach(string name in ForkReleases.ListRetiredFiles) {
					Assert.IsTrue(scriptResult.Lines.Any(x => x.StartsWith("Denied file: "+name+" (")),"Not refused with a reason: "+name+"\r\n"+scriptResult.Output);
				}
				Assert.IsFalse(scriptResult.Lines.Any(x => x.StartsWith("Missing:")),scriptResult.Output);
			}
			finally {
				DeleteFolder(folder);
			}
		}

		[TestMethod]
		public void CheckFolder_FailsWhenTheRuntimeFilesOrTheDocumentsAreMissing() {
			string folder=NewFakeStaging("missing");
			try {
				foreach(string path in new[] { "WebView2Loader.dll","Hunspellx86.dll","ODR.dll","LICENSE",LGPL_FILE,@"db\init\new-database.sql" }) {
					File.Delete(Path.Combine(folder,path));
				}
				ScriptResult scriptResult=CheckFolder(folder);
				Assert.AreEqual(1,scriptResult.ExitCode,scriptResult.Output);
				foreach(string name in new[] { "WebView2Loader.dll","Hunspellx86.dll","ODR.dll",LGPL_FILE }) {
					Assert.IsTrue(scriptResult.Lines.Any(x => x.StartsWith("Missing: "+name+" (")),"No reason for "+name+"\r\n"+scriptResult.Output);
				}
				CollectionAssert.Contains(scriptResult.Lines,"Missing: LICENSE");
				CollectionAssert.Contains(scriptResult.Lines,@"Missing: db\init\new-database.sql");
			}
			finally {
				DeleteFolder(folder);
			}
		}

		[TestMethod]
		public void CheckFolder_FailsWhenTheNoticesDoNotNameAShippedThirdPartyFile() {
			string folder=NewFakeStaging("notices");
			try {
				string path=Path.Combine(folder,"THIRD-PARTY-NOTICES.txt");
				string notices=File.ReadAllText(path);
				StringAssert.Contains(notices,"Files:     NDde.dll");
				File.WriteAllText(path,notices.Replace("Files:     NDde.dll","Files:     NDde (removed for the test)"));
				ScriptResult scriptResult=CheckFolder(folder);
				Assert.AreEqual(1,scriptResult.ExitCode,scriptResult.Output);
				CollectionAssert.Contains(scriptResult.Lines,"Not named in THIRD-PARTY-NOTICES.txt: NDde.dll");
				Assert.AreEqual(1,scriptResult.Lines.Count(x => x.StartsWith("Not named in")),"Only that file.\r\n"+scriptResult.Output);
			}
			finally {
				DeleteFolder(folder);
			}
		}

		[TestMethod]
		public void LgplText_IsTheLicenceVerbatim_AndEveryLgplComponentPointsToIt() {
			//RdlEngine.dll, RdlViewer.dll, NHunspell.dll, Hunspellx86.dll and the en_US word list ship under the LGPL 2.1, which asks for a copy of
			//the licence with every copy of the library. The release stages the text (the allow-list test above) from this file.
			string text=TestPaths.ReadSource(LGPL_FILE).Replace("\r\n","\n");
			StringAssert.StartsWith(text.TrimStart(),"GNU LESSER GENERAL PUBLIC LICENSE");
			StringAssert.Contains(text,"Version 2.1, February 1999");
			StringAssert.Contains(text,"TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION");
			StringAssert.Contains(text,"END OF TERMS AND CONDITIONS");
			string sha256;
			using(SHA256 sha=SHA256.Create()) {
				sha256=string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(x => x.ToString("x2")));
			}
			Assert.AreEqual(LGPL_SHA256_LF,sha256,"The LGPL 2.1 text, unchanged.");
			string notices=TestPaths.ReadSource("THIRD-PARTY-NOTICES.txt").Replace("\r\n","\n");
			foreach(string component in _arrayLgplComponents) {
				Match match=Regex.Match(notices,@"(?m)^--- Component: "+Regex.Escape(component)+@"\n(?:(?!^--- ).*\n)*?License:(.*\n(?:[ \t]+\S.*\n)*)");
				Assert.IsTrue(match.Success,"No License field for "+component);
				StringAssert.Contains(match.Groups[1].Value,LGPL_FILE,component);
			}
			//The tri-licensed NHunspell and Hunspell ship under their LGPL option, so no MPL 1.1 text is needed.
			StringAssert.Contains(notices,"This release distributes them under the GNU Lesser General Public License, version 2.1");
			StringAssert.Contains(TestPaths.ReadSource("NOTICE"),LGPL_FILE);
			StringAssert.Contains(TestPaths.ReadSource("installer","setup-info.txt"),LGPL_FILE);
		}

		[TestMethod]
		public void Direct2dWrapper_EachConfigurationBuildsIntoItsOwnFolder() {
			//With one bin\ for Debug and Release, an incremental Release build after a Debug build copied the Debug DLL (debug C runtime) into
			//OpenPmsDental\bin\Release. Build-Release.ps1 compares the staged DLL with Direct2dWrapper\bin\Release.
			string vcxproj=TestPaths.ReadSource("Direct2dWrapper","Direct2dWrapper.vcxproj");
			MatchCollection matchCollection=Regex.Matches(vcxproj,
				@"<PropertyGroup Condition=""'\$\(Configuration\)\|\$\(Platform\)'=='(\w+)\|(\w+)'"">(?:(?!</PropertyGroup>).)*?<OutDir>([^<]+)</OutDir>",
				RegexOptions.Singleline);
			//The folder each configuration resolves to: its OutDir with $(Configuration) and $(Platform) expanded.
			Dictionary<string,string> dictOutDirs=matchCollection.Cast<Match>().ToDictionary(x => x.Groups[1].Value+"|"+x.Groups[2].Value,
				x => x.Groups[3].Value.Replace("$(Configuration)",x.Groups[1].Value).Replace("$(Platform)",x.Groups[2].Value));
			CollectionAssert.AreEquivalent(new[] { "Debug|Win32","Debug|x64","Release|Win32","Release|x64" },dictOutDirs.Keys.ToArray());
			Assert.AreEqual(4,dictOutDirs.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count(),"One output folder per configuration.");
			Assert.AreEqual(@"$(ProjectDir)bin\Release\",dictOutDirs["Release|Win32"]);
			StringAssert.Contains(TestPaths.ReadSource("tools","Build-Release.ps1"),@"'Direct2dWrapper.dll'='Direct2dWrapper\bin\Release'");
		}

		[TestMethod]
		public void BuildRelease_PinsTheCompatibilityVersion() {
			string script=TestPaths.ReadSource("tools","Build-Release.ps1");
			Match match=Regex.Match(script,@"(?m)^\$CompatVersion='([^']+)'");
			Assert.IsTrue(match.Success);
			Assert.AreEqual(ForkVersions.CompatVersion.ToString(4),match.Groups[1].Value);
		}
		#endregion Build-Release.ps1 lists

		#region installer/OpenPmsDental.iss
		[TestMethod]
		public void Iss_NamesTheProduct_TheReleaseVersion_AndItsOwnFolder() {
			string iss=ReadIss();
			string version=ReleaseVersion();
			StringAssert.Contains(iss,"#define AppVersion \""+version+"\"");
			StringAssert.Contains(iss,"#define AppName \"Open PMS - Dental\"");
			Assert.AreEqual("{#AppName}",SetupDirective(iss,"AppName"));
			Assert.AreEqual("{#AppVersion}",SetupDirective(iss,"AppVersion"));
			Assert.AreEqual(version+".0.0",SetupDirective(iss,"VersionInfoVersion"));
			Assert.AreEqual(@"{commonpf32}\Open PMS - Dental",SetupDirective(iss,"DefaultDirName"));
			Assert.AreEqual(ProgramNames.DefaultInstallFolder,@"C:\Program Files (x86)\Open PMS - Dental");
			Assert.AreEqual("OpenPmsDental-{#AppVersion}-Setup",SetupDirective(iss,"OutputBaseFilename"));
			Assert.AreEqual(@"{#StagingDir}\LICENSE",SetupDirective(iss,"LicenseFile"));
			Assert.AreEqual("{#InfoFile}",SetupDirective(iss,"InfoBeforeFile"));
			StringAssert.Contains(iss,"#define ExeName \""+ProgramNames.ExeFileName+"\"");
			Assert.IsTrue(Regex.IsMatch(SetupDirective(iss,"AppId"),@"^\{\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}$"),"AppId is a GUID of its own.");
			Assert.IsFalse(iss.Contains(@"\Open Dental"),"Never Open Dental's folder.");
		}

		[TestMethod]
		public void Iss_InstallsPerMachine_KeepsTheUninstallerOutOfTheTopLevel_AndAllowsTestInstalls() {
			string iss=ReadIss();
			Assert.AreEqual("admin",SetupDirective(iss,"PrivilegesRequired"));
			Assert.AreEqual("commandline",SetupDirective(iss,"PrivilegesRequiredOverridesAllowed"));
			Assert.AreEqual(@"{app}\uninstall",SetupDirective(iss,"UninstallFilesDir"));
			StringAssert.Contains(iss,"procedure RemoveProgramFolder;");
			Assert.IsFalse(iss.Contains("[UninstallDelete]"),"The program folder is removed in code, which keeps an A to Z folder.");
			StringAssert.Contains(iss,"function CheckInstallFolder(");
		}

		///<summary>UpdateFileCopier never deletes a file, so setup is what removes the files of an earlier release that this one no longer ships
		///(ForkReleases.ListRetiredFiles): an upgrade deletes each one from the program folder, and nothing else.</summary>
		[TestMethod]
		public void Iss_DeletesTheRetiredFilesOnAnUpgrade() {
			string iss=ReadIss();
			Match match=Regex.Match(iss,@"(?ms)^\[InstallDelete\]\r?\n(.*?)(?=^\[)");
			Assert.IsTrue(match.Success,"No [InstallDelete] section before the next section.");
			List<string> listEntries=match.Groups[1].Value.Split('\n').Select(x => x.Trim()).Where(x => x!="" && !x.StartsWith(";")).ToList();
			List<string> listExpected=ForkReleases.ListRetiredFiles.Select(x => @"Type: files; Name: ""{app}\"+x+@"""").ToList();
			CollectionAssert.AreEquivalent(listExpected,listEntries,"One entry per retired file, and only files in the program folder.");
			Assert.AreEqual(1,Regex.Matches(iss,@"(?m)^\[InstallDelete\]").Count);
			//Setup never deletes a file this release installs: CheckFolder_RefusesARetiredFile keeps every retired file off the allow-list.
			foreach(string name in ForkReleases.ListRetiredFiles) {
				Assert.IsFalse(ForkReleases.ListBuildIdFiles.Contains(name,StringComparer.OrdinalIgnoreCase),name);
			}
		}

		[TestMethod]
		public void Iss_NeverStartsTheProgram() {
			string iss=ReadIss();
			Assert.IsFalse(Regex.IsMatch(iss,@"(?m)^\[Run\]"),"No [Run] section.");
			Assert.IsFalse(Regex.IsMatch(iss,@"(?m)^\[UninstallRun\]"),"No [UninstallRun] section.");
			Assert.AreEqual("no",SetupDirective(iss,"RestartApplications"));
		}

		[TestMethod]
		public void Iss_WritesTheConfigWithoutAPassword_AndOpensOnlyThatFileToUsers() {
			string iss=ReadIss();
			string[] arrayPermissionLines=iss.Split('\n').Where(x => x.Contains("Permissions:")).ToArray();
			Assert.AreEqual(1,arrayPermissionLines.Length,"Exactly one entry changes permissions.");
			StringAssert.Contains(arrayPermissionLines[0],@"Source: ""{tmp}\{#ConfigFileName}""");
			StringAssert.Contains(arrayPermissionLines[0],"Permissions: users-modify");
			StringAssert.Contains(arrayPermissionLines[0],"onlyifdoesntexist");
			StringAssert.Contains(iss,"#define ConfigFileName \""+ProgramNames.ConfigFileName+"\"");
			Assert.IsFalse(Regex.IsMatch(iss,@"(?m)^\[Dirs\]"),"No folder permissions.");
			StringAssert.Contains(iss,"'        <Password></Password>'");
			StringAssert.Contains(iss,"'        <MySQLPassHash></MySQLPassHash>'");
			StringAssert.Contains(iss,"'        <NoShowOnStartup>False</NoShowOnStartup>'");
			foreach(string param in new[] { "{param:DBSERVER|","{param:DBNAME|","{param:DBUSER|","{param:PREREQS|" }) {
				StringAssert.Contains(iss,param);
			}
		}

		[TestMethod]
		public void Iss_ChecksThePrerequisites_AndDownloadsOnlyFromMicrosoft() {
			string iss=ReadIss();
			StringAssert.Contains(iss,"NetFx48Release = 528040;");
			StringAssert.Contains(iss,@"'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86'");
			StringAssert.Contains(iss,@"'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'");
			StringAssert.Contains(iss,"function IsSignedByMicrosoft(");
			foreach(Match match in Regex.Matches(iss,@"https?://[^\s'""]+")) {
				string url=match.Value;
				bool isMicrosoft=url.StartsWith("https://aka.ms/") || url.StartsWith("https://go.microsoft.com/") || url.StartsWith("https://dotnet.microsoft.com/");
				bool isSource=url.StartsWith("https://github.com/Marysville-Dental-Center/opendental");
				Assert.IsTrue(isMicrosoft || isSource,"Unexpected address: "+url);
			}
		}

		[TestMethod]
		public void Iss_RegistersTheProgramsEventLogSource_OnlyForAdminInstalls() {
			string iss=ReadIss();
			StringAssert.Contains(iss,"#define EventLogSource \""+ProgramNames.EventLogSource+"\"");
			string registryLine=iss.Split('\n').Single(x => x.StartsWith("Root: HKLM;"));
			StringAssert.Contains(registryLine,@"SYSTEM\CurrentControlSet\Services\EventLog\Application\{#EventLogSource}");
			StringAssert.Contains(registryLine,"Check: IsAdminInstallMode");
			StringAssert.Contains(registryLine,"uninsdeletekey");
		}

		[TestMethod]
		public void InfoPage_HasTheAttribution_TheSourceLink_AndNotice() {
			string info=TestPaths.ReadSource("installer","setup-info.txt");
			StringAssert.Contains(info,ATTRIBUTION);
			StringAssert.Contains(info,"https://github.com/Marysville-Dental-Center/opendental/tree/v@VERSION@");
			StringAssert.Contains(info,"@NOTICE@");
			StringAssert.Contains(TestPaths.ReadSource("NOTICE"),ATTRIBUTION,"NOTICE, shown on the same page, starts with it too.");
		}

		[TestMethod]
		public void ReleaseNotes_HaveTheAttribution_AndTheSourceOfThisRelease() {
			string version=ReleaseVersion();
			string notes=TestPaths.ReadSource("docs","release-notes","v"+version+".md");
			StringAssert.Contains(notes,ATTRIBUTION);
			StringAssert.Contains(notes,"https://github.com/Marysville-Dental-Center/opendental/tree/v"+version);
			StringAssert.Contains(notes,"OpenPmsDental-"+version+"-Setup.exe");
		}

		[TestMethod]
		public void ReleaseNotes_GiveTheInstalled16BitDefault_AndTheStepThatKeepsPatientCaptureAt8Bits() {
			//The notes once said patient computers "stay at 8 bits (Allow16Bit=0)", while the program installs with Allow16Bit=1: an office that
			//followed them and ran one Test capture would take 16-bit X-rays of patients before the counsel review and the clinic run.
			string catalog=TestPaths.ReadSource("ImagingCapture","Settings","CaptureSettingsCatalog.cs");
			Match match=Regex.Match(catalog,@"Add\(twain,""Allow16Bit"",CaptureSettingKind\.Switch,""([01])""");
			Assert.IsTrue(match.Success,"The Allow16Bit default in CaptureSettingsCatalog.cs");
			string notes=TestPaths.ReadSource("docs","release-notes","v"+ReleaseVersion()+".md");
			StringAssert.Contains(notes,"defaults are `[Twain] Allow16Bit="+match.Groups[1].Value+"`");
			if(match.Groups[1].Value=="1") {
				StringAssert.Contains(notes,"Before any patient capture, set `[Twain] Allow16Bit=0` in Capture settings");
				StringAssert.Contains(notes,"\"Before 16-bit X-rays for patients\"");
				StringAssert.Contains(TestPaths.ReadSource("docs","imaging","intraoral-capture-setup.md"),"**Before 16-bit X-rays for patients.**");
				Assert.IsFalse(notes.Contains("stay at 8 bits"),"Nothing in the program keeps them at 8 bits by itself.");
			}
		}
		#endregion installer/OpenPmsDental.iss
	}
}
