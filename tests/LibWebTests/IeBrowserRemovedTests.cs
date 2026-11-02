// The Internet Explorer browser is gone from everything the release ships: no WebBrowser control of WinForms or WPF, no mshtml or SHDocVw
// interop, no IE emulation registry key or other IE-only call, in the source, the project files, "Required dlls" and (when built) the program's
// assemblies. The installer checks the WebView2 Runtime and never downloads it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace LibWebTests {
	[TestClass]
	public class IeBrowserRemovedTests {
		///<summary>What the IE browser leaves in C# or XAML source. Comment lines are not checked.</summary>
		private static readonly Regex[] _arrayRegexSource={
			new Regex(@"\bSystem\.Windows\.Forms\.WebBrowser\b"),
			new Regex(@"\bSystem\.Windows\.Controls\.WebBrowser\b"),
			new Regex(@"\bWebBrowser(Navigating|Navigated|DocumentCompleted|Progress|NewWindow|ReadyState|Encryption|RefreshOption)\w*\b"),
			new Regex(@"\bnew\s+WebBrowser\s*\("),
			new Regex(@"\b(private|public|protected|internal)\s+WebBrowser\s+\w+"),
			new Regex(@"\btypeof\s*\(\s*WebBrowser\s*\)"),
			new Regex(@"<\s*(ui:)?WebBrowser\b"),
			new Regex(@"\bmshtml\b",RegexOptions.IgnoreCase),
			new Regex(@"\bSHDocVw\b",RegexOptions.IgnoreCase),
			new Regex(@"\bIHTML\w*(Document|Element|Window)\w*\b"),
			new Regex(@"\bObjectForScripting\b"),
			new Regex(@"\bActiveXInstance\b"),
			new Regex(@"\bFEATURE_BROWSER_EMULATION\b"),
			new Regex(@"Internet Explorer\\+Main\\+FeatureControl",RegexOptions.IgnoreCase),
			new Regex(@"\bCoInternetSetFeatureEnabled\b"),
			new Regex(@"\bInternetSetCookie\b"),
			new Regex(@"\bTryUpdateIeEmulation\b"),
		};
		///<summary>The IE interop assemblies, by file or reference name.</summary>
		private static readonly string[] _arrayIeAssemblies={ "Interop.SHDocVw","AxInterop.SHDocVw","SHDocVw","Microsoft.mshtml","mshtml" };
		///<summary>Third-party libraries that carry an IE browser path the program never takes, with the reason; a test checks each reason.</summary>
		private static readonly Dictionary<string,string> _dictThirdPartyIeUnused=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			{ "Microsoft.Identity.Client.dll","MSAL's embedded sign-in window of .NET Framework; the program always asks for the system browser "
				+"(Microsoft365_SignInUsesTheSystemBrowser)" },
		};

		private static bool IsCommentLine(string line) {
			string trimmed=line.TrimStart();
			return trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*") || trimmed.StartsWith("<!--");
		}

		[TestMethod]
		public void ShippedSource_UsesNoInternetExplorerBrowser() {
			List<string> listFiles=TestPaths.GetShippedFiles(".cs",".xaml");
			//The scan covers the program, so a folder list that went wrong cannot pass by checking nothing.
			Assert.IsTrue(listFiles.Any(x => x.EndsWith(@"OpenPmsDental\Forms\FormWiki.cs",StringComparison.OrdinalIgnoreCase)));
			Assert.IsTrue(listFiles.Any(x => x.EndsWith(@"CodeBase\Controls\ODWebView2.cs",StringComparison.OrdinalIgnoreCase)));
			Assert.IsTrue(listFiles.Any(x => x.EndsWith(@"WpfControlsOD\Frms\FrmTestAllControls.xaml",StringComparison.OrdinalIgnoreCase)));
			List<string> listViolations=new List<string>();
			foreach(string path in listFiles) {
				string[] arrayLines=File.ReadAllLines(path);
				for(int i=0;i<arrayLines.Length;i++) {
					if(IsCommentLine(arrayLines[i])) {
						continue;
					}
					foreach(Regex regex in _arrayRegexSource) {
						Match match=regex.Match(arrayLines[i]);
						if(match.Success) {
							listViolations.Add(TestPaths.Relative(path)+"("+(i+1)+"): "+match.Value);
						}
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"The Internet Explorer browser is still used:\r\n"+string.Join("\r\n",listViolations));
		}

		[TestMethod]
		public void IeControlFiles_AreDeleted() {
			foreach(string relative in new[] { @"WpfControlsOD\UI\Controls\WebBrowser.xaml",@"WpfControlsOD\UI\Controls\WebBrowser.xaml.cs",
				@"OpenPmsDental\Forms\FormWebBrowser.cs",@"OpenPmsDental\Forms\FormWebBrowser.designer.cs",@"OpenPmsDental\Forms\FormWebBrowser.resx" })
			{
				Assert.IsFalse(File.Exists(Path.Combine(TestPaths.RepoRoot,relative)),relative+" is the IE browser and should be gone.");
			}
			string launcher=TestPaths.ReadSource("WpfControlsOD","Misc","FormLauncher.cs");
			Assert.IsFalse(Regex.IsMatch(launcher,@"\bFormWebBrowser\b"),"FormLauncher no longer names the IE window.");
		}

		[TestMethod]
		public void Projects_ReferenceNoIeInterop() {
			List<string> listProjects=TestPaths.GetShippedFiles(".csproj");
			Assert.IsTrue(listProjects.Any(x => x.EndsWith(@"OpenPmsDental\OpenPmsDental.csproj",StringComparison.OrdinalIgnoreCase)));
			List<string> listViolations=new List<string>();
			foreach(string path in listProjects) {
				string text=File.ReadAllText(path);
				if(Regex.IsMatch(text,@"<COMReference\s+Include=""(SHDocVw|MSHTML)""",RegexOptions.IgnoreCase)) {
					listViolations.Add(TestPaths.Relative(path)+": a COM reference to the IE browser");
				}
				foreach(string name in _arrayIeAssemblies) {
					if(Regex.IsMatch(text,@"Include=""(..\\)*(Required dlls\\)?"+Regex.Escape(name)+@"(\.dll)?[,""]",RegexOptions.IgnoreCase)) {
						listViolations.Add(TestPaths.Relative(path)+": "+name);
					}
				}
				if(Regex.IsMatch(text,@"Include=""UI\\Controls\\WebBrowser\.xaml",RegexOptions.IgnoreCase)) {
					listViolations.Add(TestPaths.Relative(path)+": the WPF WebBrowser control");
				}
			}
			Assert.AreEqual(0,listViolations.Count,string.Join("\r\n",listViolations));
		}

		[TestMethod]
		public void RequiredDlls_HoldNoIeInterop() {
			string folder=Path.Combine(TestPaths.RepoRoot,"Required dlls");
			foreach(string name in _arrayIeAssemblies) {
				Assert.IsFalse(File.Exists(Path.Combine(folder,name+".dll")),name+".dll");
			}
			//The browser that replaces it is there, at the SDK version the program is built against.
			foreach(string name in new[] { "Microsoft.Web.WebView2.Core","Microsoft.Web.WebView2.WinForms","Microsoft.Web.WebView2.Wpf" }) {
				string path=Path.Combine(folder,name+".dll");
				Assert.IsTrue(File.Exists(path),name);
				Assert.AreEqual(new Version(1,0,2420,47),System.Reflection.AssemblyName.GetAssemblyName(path).Version,name);
			}
			Assert.IsTrue(File.Exists(Path.Combine(folder,"WebView2Loader.dll")));
		}

		///<summary>The built program folder, when this checkout has built it: none of its assemblies references an IE browser type or carries
		///embedded IE interop types. Read with Mono.Cecil; nothing is loaded.</summary>
		[TestMethod]
		public void BuiltAssemblies_ReferenceNoIeBrowserType() {
			string folder=Path.Combine(TestPaths.RepoRoot,"OpenPmsDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenPmsDental.exe"))) {
				Assert.Inconclusive("The program is not built.");
			}
			List<string> listViolations=new List<string>();
			int countChecked=0;
			foreach(string path in Directory.GetFiles(folder).Where(x => x.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)
				|| x.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))
			{
				ModuleDefinition moduleDefinition;
				try {
					moduleDefinition=ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(path)));
				}
				catch(Exception) {
					continue;//native, not an assembly
				}
				using(moduleDefinition) {
					countChecked++;
					string name=Path.GetFileName(path);
					if(_dictThirdPartyIeUnused.ContainsKey(name)) {
						continue;
					}
					foreach(AssemblyNameReference assemblyNameReference in moduleDefinition.AssemblyReferences) {
						if(_arrayIeAssemblies.Contains(assemblyNameReference.Name,StringComparer.OrdinalIgnoreCase)) {
							listViolations.Add(name+" references "+assemblyNameReference.Name);
						}
					}
					foreach(TypeReference typeReference in moduleDefinition.GetTypeReferences()) {
						if(IsIeType(typeReference.Namespace,typeReference.Name)) {
							listViolations.Add(name+" uses "+typeReference.FullName);
						}
					}
					foreach(TypeDefinition typeDefinition in moduleDefinition.Types) {
						if(IsIeType(typeDefinition.Namespace,typeDefinition.Name)) {
							listViolations.Add(name+" embeds "+typeDefinition.FullName);
						}
					}
				}
			}
			Assert.IsTrue(countChecked>10,"Only "+countChecked+" assemblies were read.");
			Assert.AreEqual(0,listViolations.Count,string.Join("\r\n",listViolations));
		}

		///<summary>MSAL (Microsoft.Identity.Client) still has the IE sign-in window of .NET Framework, which it uses unless told otherwise. Every
		///interactive sign-in of the program tells it to use the system browser instead.</summary>
		[TestMethod]
		public void Microsoft365_SignInUsesTheSystemBrowser() {
			int countCalls=0;
			List<string> listViolations=new List<string>();
			foreach(string path in TestPaths.GetShippedFiles(".cs")) {
				string text=File.ReadAllText(path);
				foreach(Match match in Regex.Matches(text,@"\.AcquireTokenInteractive\(")) {
					countCalls++;
					//The builder chain of the call, up to the end of its statement.
					int idxEnd=text.IndexOf(';',match.Index);
					string statement=text.Substring(match.Index,(idxEnd<0 ? text.Length : idxEnd)-match.Index);
					if(!Regex.IsMatch(statement,@"\.WithUseEmbeddedWebView\(\s*false\s*\)")) {
						listViolations.Add(TestPaths.Relative(path));
					}
				}
			}
			Assert.IsTrue(countCalls>0,"The Microsoft 365 sign-in is where MSAL is used.");
			Assert.AreEqual(0,listViolations.Count,"An interactive sign-in without WithUseEmbeddedWebView(false) opens IE: "+string.Join(", ",listViolations));
			Assert.AreEqual(1,_dictThirdPartyIeUnused.Count,"Each third-party exception has its own test.");
		}

		private static bool IsIeType(string nameSpace,string name) {
			if(nameSpace.Equals("SHDocVw",StringComparison.OrdinalIgnoreCase) || nameSpace.Equals("mshtml",StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			if((nameSpace=="System.Windows.Forms" || nameSpace=="System.Windows.Controls") && name.StartsWith("WebBrowser")) {
				return true;
			}
			return nameSpace=="System.Windows.Forms" && (name=="HtmlDocument" || name=="HtmlElement" || name=="HtmlWindow");
		}

		[TestMethod]
		public void Installer_ChecksTheWebView2Runtime_AndNeverDownloadsIt() {
			string iss=TestPaths.ReadSource("installer","OpenPmsDental.iss");
			//Microsoft's documented check: the pv value of the runtime's client key.
			StringAssert.Contains(iss,@"'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'");
			StringAssert.Contains(iss,"NeedWebView2 := not IsWebView2Installed;");
			//When it is missing, a page tells the administrator how to install it, and the log says so for silent installs.
			StringAssert.Contains(iss,"WebView2Page := CreateOutputMsgPage(");
			StringAssert.Contains(iss,"Result := not NeedWebView2;");
			StringAssert.Contains(iss,"Setup does not download it.");
			StringAssert.Contains(iss,"Windows 11 includes it.");
			//Nothing downloads or runs Microsoft's WebView2 installer.
			Assert.IsFalse(Regex.IsMatch(iss,@"DownloadPrereq\(\s*WebView2"),"Setup never downloads the WebView2 Runtime.");
			Assert.IsFalse(Regex.IsMatch(iss,@"RunPrereq\(\s*WebView2"),"Setup never runs the WebView2 installer.");
			Assert.IsFalse(iss.Contains("MicrosoftEdgeWebview2Setup.exe"),"No WebView2 installer file name.");
			Assert.IsFalse(Regex.IsMatch(iss,@"DownloadPrereqs\s+and\s+\([^)]*NeedWebView2"),"The download step is for the Visual C++ runtime only.");
		}

		[TestMethod]
		public void Program_NeverDownloadsTheWebView2Runtime() {
			string source=TestPaths.ReadSource("CodeBase","Controls","ODWebView2.cs");
			Assert.IsFalse(source.Contains("Process.Start"),"ODWebView2 starts no download or browser for the runtime.");
			StringAssert.Contains(source,"an administrator can install it from Microsoft");
		}
	}
}
