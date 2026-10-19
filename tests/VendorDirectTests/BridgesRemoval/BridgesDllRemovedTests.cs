// Tests that the prebuilt Bridges.dll stays deleted (docs/specs/vendor-direct/README.md, package Z2): no project, solution entry, source file or
// built assembly needs it or the binaries only it pulled in (Intuit, Serilog, Microsoft Graph and its closure, Dropbox.Api), every type the
// program used from it is in-repo source inside OpenDentBusiness, and the HQ provisioning leftovers and vendor logos removed with it stay gone.
// Reads the source and the built assemblies only; nothing leaves this computer and no database is needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.BridgesRemoval {
	[TestClass]
	public class BridgesDllRemovedTests {
		///<summary>Bridges.dll and the files in "Required dlls" that only it (directly or through each other) needed.</summary>
		private static readonly string[] _arrayRemovedFiles=new[] {
			"Bridges.dll",
			"Intuit.Ipp.Core.dll","Intuit.Ipp.Data.dll","Intuit.Ipp.DataService.dll","Intuit.Ipp.Diagnostics.dll","Intuit.Ipp.EntitlementService.dll",
			"Intuit.Ipp.Exception.dll","Intuit.Ipp.GlobalTaxService.dll","Intuit.Ipp.OAuth2PlatformClient.dll","Intuit.Ipp.QueryFilter.dll",
			"Intuit.Ipp.ReportService.dll","Intuit.Ipp.Retry.dll","Intuit.Ipp.Security.dll","Intuit.Ipp.Utility.dll","Intuit.Ipp.WebHooksService.dll",
			"IppDotNetSdkForQuickBooksApiV3.dll",
			"Serilog.dll","Serilog.Enrichers.Environment.dll","Serilog.Enrichers.Thread.dll","Serilog.Extensions.Logging.dll",
			"Serilog.Settings.Configuration.dll","Serilog.Sinks.AzureDocumentDB.dll","Serilog.Sinks.Console.dll","Serilog.Sinks.Debug.dll",
			"Serilog.Sinks.File.dll","Serilog.Sinks.Literate.dll","Serilog.Sinks.RollingFile.dll","Serilog.Sinks.Trace.dll","SerilogTraceListener.dll",
			"Microsoft.Azure.Documents.Client.dll","Microsoft.DotNet.PlatformAbstractions.dll",
			"Microsoft.Graph.dll","Microsoft.Graph.Core.dll","Azure.Core.dll","System.Memory.Data.dll","System.Text.Json.dll",
			"System.Text.Encodings.Web.dll","Microsoft.Bcl.AsyncInterfaces.dll","System.Net.Http.WinHttpHandler.dll",
			"Microsoft.IdentityModel.Tokens.dll","Microsoft.IdentityModel.Logging.dll","Microsoft.IdentityModel.Protocols.dll",
			"Microsoft.IdentityModel.Protocols.OpenIdConnect.dll","Microsoft.IdentityModel.JsonWebTokens.dll",
			"Dropbox.Api.dll" };
		///<summary>Assembly names that no built assembly of the program may reference.</summary>
		private static readonly string[] _arrayRemovedAssemblies=_arrayRemovedFiles.Select(x => Path.GetFileNameWithoutExtension(x)).ToArray();
		///<summary>The projects whose source the program is built from.</summary>
		private static readonly string[] _arraySourceProjects=new[] { "OpenDental","OpenDentBusiness","WpfControlsOD","CodeBase","OpenDentalServer",
			"OpenDentalCloud","UnitTests" };

		private static string GetRoot() {
			return FoundationLayoutTests.GetRepositoryRoot();
		}

		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(GetRoot(),relativePath));
		}

		private static bool IsBuildOutput(string relativePath) {
			return relativePath.Contains(@"\bin\") || relativePath.Contains(@"\obj\") || relativePath.Contains(@"\packages\")
				|| relativePath.StartsWith(@"\.claude\") || relativePath.StartsWith(@"\.git\");
		}

		///<summary>Every file under the root with the pattern, as paths relative to the root (leading backslash), skipping build output.</summary>
		private static List<string> GetFiles(string folder,string searchPattern) {
			string root=GetRoot();
			List<string> listFiles=new List<string>();
			if(!Directory.Exists(folder)) {
				return listFiles;
			}
			foreach(string file in Directory.EnumerateFiles(folder,searchPattern,SearchOption.AllDirectories)) {
				string relative=file.Substring(root.Length);
				if(!IsBuildOutput(relative)) {
					listFiles.Add(relative);
				}
			}
			return listFiles;
		}

		///<summary>The names of the assemblies the file references, read from a copy of its bytes so the file stays unlocked. Reuses a copy that
		///another test already loaded for reflection, because the same assembly cannot be loaded into that context twice.</summary>
		private static List<string> GetReferencedAssemblyNames(string path) {
			string name=AssemblyName.GetAssemblyName(path).FullName;
			Assembly assembly=AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(x => x.FullName==name)
				??Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));
			return assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
		}

		[TestMethod]
		public void RequiredDlls_HasNoBridgesOrItsDependencies() {
			string folder=Path.Combine(GetRoot(),"Required dlls");
			List<string> listLeft=_arrayRemovedFiles.Where(x => File.Exists(Path.Combine(folder,x))).ToList();
			Assert.AreEqual(0,listLeft.Count,"Still in Required dlls: "+string.Join(", ",listLeft));
			//MSAL stays for the Microsoft 365 sign-in, with the one IdentityModel assembly it needs.
			Assert.IsTrue(File.Exists(Path.Combine(folder,"Microsoft.Identity.Client.dll")));
			Assert.IsTrue(File.Exists(Path.Combine(folder,"Microsoft.IdentityModel.Abstractions.dll")));
		}

		[TestMethod]
		public void Projects_DoNotReferenceBridgesOrItsDependencies() {
			string root=GetRoot();
			//"Bridges\" alone is the in-repo bridges folder of OpenDental, OpenDentBusiness and WpfControlsOD, and VBbridges.dll is a different
			//binary, so look for the Bridges assembly forms only.
			Regex regex=new Regex(@"(?<![A-Za-z])Bridges\.dll|xBridges|Include=""Bridges[,""]|Intuit\.Ipp|IppDotNetSdk|Serilog|Microsoft\.Graph|Azure\.Core|Dropbox\.Api"
				+@"|System\.Text\.Json|Encodings\.Web|Bcl\.AsyncInterfaces|WinHttpHandler|System\.Memory\.Data|Documents\.Client|PlatformAbstractions"
				+@"|Microsoft\.IdentityModel\.(Tokens|Logging|Protocols|JsonWebTokens)",RegexOptions.IgnoreCase);
			List<string> listOffenders=new List<string>();
			foreach(string relative in GetFiles(root,"*.csproj").Concat(GetFiles(root,"*.sln")).Concat(GetFiles(root,"*.slnf"))
				.Concat(GetFiles(root,"*.targets")).Concat(GetFiles(root,"*.props")).Concat(GetFiles(root,"*.config")))
			{
				Match match=regex.Match(File.ReadAllText(Path.Combine(root,relative.TrimStart('\\'))));
				if(match.Success) {
					listOffenders.Add(relative+" ("+match.Value+")");
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still named in: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void Source_HasNoUsingOfTheRemovedAssemblies() {
			string root=GetRoot();
			Regex regex=new Regex(@"^\s*(using\s+(Bridges|Intuit|Serilog|Microsoft\.Graph|Azure|Dropbox\.Api)\b|extern\s+alias\s+xBridges)",RegexOptions.Multiline);
			List<string> listOffenders=new List<string>();
			foreach(string project in _arraySourceProjects) {
				foreach(string relative in GetFiles(Path.Combine(root,project),"*.cs")) {
					if(regex.IsMatch(File.ReadAllText(Path.Combine(root,relative.TrimStart('\\'))))) {
						listOffenders.Add(relative);
					}
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still imports a removed assembly: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void BusinessLayer_ReferencesNoRemovedAssembly() {
			foreach(Assembly assembly in new[] { typeof(Patient).Assembly,typeof(CodeBase.Secrets).Assembly }) {
				List<string> listReferences=assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
				foreach(string removed in _arrayRemovedAssemblies) {
					Assert.IsFalse(listReferences.Contains(removed,StringComparer.OrdinalIgnoreCase),assembly.GetName().Name+" still references "+removed);
				}
			}
		}

		[TestMethod]
		public void Program_ReferencesNoRemovedAssembly() {
			foreach(string pathRelative in new[] { @"OpenDental\bin\Debug\OpenDental.exe",@"WpfControlsOD\bin\Debug\WpfControlsOD.dll",
				@"UnitTests\bin\Debug\Unit Tests.exe" })
			{
				string path=Path.Combine(GetRoot(),pathRelative);
				if(!File.Exists(path)) {
					Assert.Inconclusive(pathRelative+" is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				List<string> listReferences=GetReferencedAssemblyNames(path);
				foreach(string removed in _arrayRemovedAssemblies) {
					Assert.IsFalse(listReferences.Contains(removed,StringComparer.OrdinalIgnoreCase),pathRelative+" still references "+removed);
				}
			}
		}

		[TestMethod]
		public void MsalDependency_StillReachesTheProgram() {
			//The Graph SDK's dependencies used to bring Microsoft.IdentityModel.Abstractions in. MSAL 4.47.1 asks for 6.22 and the shipped copy is
			//6.23.1, so the business layer references it itself, and the program must both copy it and redirect to it.
			Assert.IsTrue(typeof(Patient).Assembly.GetReferencedAssemblies().Any(x => x.Name=="Microsoft.IdentityModel.Abstractions"),
				"OpenDentBusiness should reference Microsoft.IdentityModel.Abstractions.");
			string folder=Path.Combine(GetRoot(),@"OpenDental\bin\Debug");
			if(!File.Exists(Path.Combine(folder,"OpenDental.exe"))) {
				Assert.Inconclusive(@"OpenDental\bin\Debug is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			Assert.IsTrue(File.Exists(Path.Combine(folder,"Microsoft.Identity.Client.dll")));
			Assert.IsTrue(File.Exists(Path.Combine(folder,"Microsoft.IdentityModel.Abstractions.dll")),"MSAL's dependency is not copied to the program folder.");
			string config=File.ReadAllText(Path.Combine(folder,"OpenDental.exe.config"));
			StringAssert.Matches(config,new Regex(@"name=""Microsoft\.IdentityModel\.Abstractions""[^>]*/>\s*<bindingRedirect oldVersion=""0\.0\.0\.0-6\.23\.1\.0"""),
				"OpenDental.exe.config needs the binding redirect MSAL relies on.");
		}

		[TestMethod]
		public void Program_OutputHasNoBridgesDll() {
			string folder=Path.Combine(GetRoot(),@"OpenDental\bin\Debug");
			if(!File.Exists(Path.Combine(folder,"OpenDental.exe"))) {
				Assert.Inconclusive(@"OpenDental\bin\Debug is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			//A stale copy from an older build can stay in bin until a clean, so only check that this build did not write a newer one.
			string pathBridges=Path.Combine(folder,"Bridges.dll");
			if(File.Exists(pathBridges)) {
				Assert.IsTrue(File.GetLastWriteTimeUtc(pathBridges)<File.GetLastWriteTimeUtc(Path.Combine(folder,"OpenDental.exe")).AddMinutes(-1),
					@"OpenDental\bin\Debug\Bridges.dll was copied by the latest build.");
			}
		}

		[TestMethod]
		public void ReplacementTypes_AreInRepoSourceInsideOpenDentBusiness() {
			Assembly assembly=typeof(Patient).Assembly;
			string[] arrayTypeNames=new[] {
				"OpenDentBusiness.Qbo.QuickBooksOnline","OpenDentBusiness.Qbo.QuickBooksOnlineToken","OpenDentBusiness.Qbo.QuickBooksOnlineAccount",
				"OpenDentBusiness.Qbo.QuickBooksOnlineClassRef","OpenDentBusiness.Qbo.QuickBooksOnlineDepositResponse",
				"OpenDentBusiness.Microsoft365.MicrosoftApiConnector","OpenDentBusiness.Microsoft365.MicrosoftTokenHelper",
				"OpenDentBusiness.USPSAddressValidationDatas","OpenDentBusiness.USPSAddressValidationResponse","OpenDentBusiness.EnumDPVConfirmation",
				"OpenDentBusiness.Bridges.CareCredit","OpenDentBusiness.Bridges.CareCredit+CareCreditException","OpenDentBusiness.Bridges.CareCreditLandingPage",
				"OpenDentBusiness.Bridges.EnumCareCreditPrefillServiceType","OpenDentBusiness.Bridges.EnumCareCreditBatch",
				"OpenDentBusiness.Bridges.CareCreditQSBatchResponse" };
			foreach(string typeName in arrayTypeNames) {
				Assert.IsNotNull(assembly.GetType(typeName),typeName+" should be in OpenDentBusiness.");
			}
			//README 3.5: never a type in a namespace called just "Bridges" (it was Bridges.dll's).
			Assert.IsFalse(assembly.GetTypes().Any(x => x.Namespace=="Bridges"),"OpenDentBusiness declares a type in the namespace Bridges.");
		}

		[TestMethod]
		public void HqProvisioningLeftovers_AreGone() {
			Assembly assembly=typeof(Patient).Assembly;
			Assert.IsFalse(assembly.GetTypes().Any(x => x.Namespace=="OpenDentBusiness.DoseSpotService"),"The DoseSpot v1 SOAP proxy should have been removed.");
			Assert.IsFalse(Directory.Exists(Path.Combine(GetRoot(),@"OpenDentBusiness\Web References\DoseSpotService")));
			Assert.IsFalse(ReadRepoFile(@"OpenDentBusiness\OpenDentBusiness.csproj").Contains("DoseSpotService"));
			foreach(string file in new[] { @"OpenDentBusiness\Properties\Settings.settings",@"OpenDentBusiness\Properties\Settings.Designer.cs",
				@"OpenDentBusiness\app.config" })
			{
				Assert.IsFalse(ReadRepoFile(file).Contains("dosespot_staging_my_API"),file+" still has the DoseSpot v1 URL setting.");
			}
			Assert.IsNull(typeof(OIDInternals).GetProperty("CustomerPatNum"),"OIDInternals.CustomerPatNum asked HQ and should have been removed.");
			Assert.IsFalse(typeof(ICustomerUpdates).GetMethods().Any(x => x.Name=="RequestCustomerID"));
			string csproj=ReadRepoFile(@"OpenDental\OpenDental.csproj");
			foreach(string form in new[] { "FormAdvertisingPostcardsAccountSetup","FormDoseSpotAssignClinicId","FormDoseSpotAssignUserId","FormNewCropBilling",
				"FormErxAccess" })
			{
				Assert.IsFalse(csproj.Contains(form),"OpenDental.csproj still lists "+form);
				Assert.IsFalse(File.Exists(Path.Combine(GetRoot(),@"OpenDental\Forms\"+form+".cs")),form+".cs should have been deleted.");
			}
			string formOpenDental=ReadRepoFile(@"OpenDental\Main Modules\FormOpenDental.cs");
			Assert.IsFalse(formOpenDental.Contains("new FormDoseSpotAssign"),"Old DoseSpot alerts must not open the removed forms.");
			//The stored values stay, so alert rows written before still load.
			Assert.IsTrue(Enum.IsDefined(typeof(FormType),"FormDoseSpotAssignUserId"));
			Assert.IsTrue(Enum.IsDefined(typeof(FormType),"FormDoseSpotAssignClinicId"));
		}

		[TestMethod]
		public void VendorLogosAndDropboxLicence_AreGone() {
			string resx=ReadRepoFile(@"OpenDental\Properties\Resources.resx");
			string designer=ReadRepoFile(@"OpenDental\Properties\Resources.Designer.cs");
			string csproj=ReadRepoFile(@"OpenDental\OpenDental.csproj");
			foreach(string name in new[] { "CareCredit_Button_NoTag_26x90","google_signin_","PaySimple_Button","payconnect-btn-26","payconnect_btn_26",
				"Dropbox_Api" })
			{
				Assert.IsFalse(resx.Contains(name),"Resources.resx still has "+name);
				Assert.IsFalse(designer.Contains(name),"Resources.Designer.cs still has "+name);
				Assert.IsFalse(csproj.Contains(name),"OpenDental.csproj still lists "+name);
			}
			List<string> listLeft=Directory.EnumerateFiles(Path.Combine(GetRoot(),@"OpenDental\Resources"))
				.Select(x => Path.GetFileName(x))
				.Where(x => Regex.IsMatch(x,@"^(CareCredit_Button|google_signin|PaySimple_Button|payconnect-btn|Dropbox_Api)",RegexOptions.IgnoreCase))
				.ToList();
			Assert.AreEqual(0,listLeft.Count,"Still in OpenDental\\Resources: "+string.Join(", ",listLeft));
		}
	}
}
