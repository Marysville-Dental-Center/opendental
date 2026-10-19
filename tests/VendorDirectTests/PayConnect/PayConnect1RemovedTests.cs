// Tests that PayConnect 1.0 and the VeriFone terminals stay removed (docs/specs/vendor-direct/payconnect.md 4.5, owner decision 2026-10-18):
// no MerchantService web reference, no PayConnect 1.0 REST class, no DpsPos.dll or log4net, no PayConnect 1.0 window, and none of its hosts or
// compiled-in client headers. The stored transaction-type names stay readable. Reads the source and the built assemblies only; nothing leaves
// this computer and no database is needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.PayConnectV1Removal {
	[TestClass]
	public class PayConnect1RemovedTests {
		private static readonly string[] _arrayRemovedAssemblies=new[] { "DpsPos","log4net" };
		///<summary>The projects whose source the program is built from.</summary>
		private static readonly string[] _arraySourceProjects=new[] { "OpenDental","OpenDentBusiness","WpfControlsOD","CodeBase","OpenDentalServer" };

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

		///<summary>Every file under the source projects with one of the extensions, as paths relative to the repository root (leading backslash).</summary>
		private static List<string> GetSourceFiles(params string[] arrayExtensions) {
			string root=GetRoot();
			List<string> listFiles=new List<string>();
			foreach(string project in _arraySourceProjects) {
				string folder=Path.Combine(root,project);
				if(!Directory.Exists(folder)) {
					continue;
				}
				foreach(string file in Directory.EnumerateFiles(folder,"*.*",SearchOption.AllDirectories)) {
					string relative=file.Substring(root.Length);
					if(IsBuildOutput(relative) || !arrayExtensions.Any(x => file.EndsWith(x,StringComparison.OrdinalIgnoreCase))) {
						continue;
					}
					listFiles.Add(relative);
				}
			}
			return listFiles;
		}

		///<summary>The names of the assemblies the file references, read from a copy of its bytes so the file stays unlocked.</summary>
		private static List<string> GetReferencedAssemblyNames(string path) {
			Assembly assembly=Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));
			return assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
		}

		[TestMethod]
		public void MerchantServiceWebReference_IsGone() {
			Assembly assemblyBusiness=typeof(Patient).Assembly;
			foreach(string typeName in new[] { "MerchantService","Credentials","creditCardRequest","transResponse","signatureRequest","expiration","Token" }) {
				Assert.IsNull(assemblyBusiness.GetType("OpenDentBusiness.PayConnectService."+typeName),"PayConnectService."+typeName+" should be gone.");
			}
			Assert.IsNull(assemblyBusiness.GetType("OpenDentBusiness.PayConnectREST"),"The PayConnect 1.0 REST class should be gone.");
			Assert.IsFalse(Directory.Exists(Path.Combine(GetRoot(),@"OpenDentBusiness\Web References\PayConnectService")));
			foreach(string file in new[] { @"OpenDentBusiness\OpenDentBusiness.csproj",@"OpenDentBusiness\app.config",
				@"OpenDentBusiness\Properties\Settings.settings",@"OpenDentBusiness\Properties\Settings.Designer.cs" })
			{
				Assert.IsFalse(ReadRepoFile(file).Contains("PayConnectService"),file+" still names the PayConnect 1.0 web reference.");
			}
		}

		[TestMethod]
		public void TransType_KeepsTheStoredNamesAndOrder() {
			//payconnectresponseweb.TransType holds the member name, and PayConnectResponseWebCrud parses it back.
			CollectionAssert.AreEqual(new[] { "SALE","AUTH","RETURN","VOID","FORCE" },Enum.GetNames(typeof(OpenDentBusiness.PayConnectService.transType)));
			Assert.AreEqual(3,(int)OpenDentBusiness.PayConnectService.transType.VOID);
			Assert.AreEqual(OpenDentBusiness.PayConnectService.transType.RETURN,
				(OpenDentBusiness.PayConnectService.transType)Enum.Parse(typeof(OpenDentBusiness.PayConnectService.transType),"RETURN"));
			FieldInfo fieldInfo=typeof(PayConnectResponseWeb).GetField(nameof(PayConnectResponseWeb.TransType));
			Assert.AreEqual(typeof(OpenDentBusiness.PayConnectService.transType),fieldInfo.FieldType,"The table type keeps its column type.");
		}

		[TestMethod]
		public void PayConnectClass_HasNoVersion1Members() {
			List<string> listMembers=typeof(PayConnect).GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)
				.Select(x => x.Name).ToList();
			foreach(string name in new[] { "ProcessCreditCard","ProcessSignature","BuildSaleRequest","IsValidCardAndExp","GetMerchantServiceUrl",
				"GetCredentials","GetHpfUrlForPayment","MakePaymentWithAlias","WakeupWebPaymentsMonitor","Mock","Monitor","MockPayConnect" })
			{
				Assert.IsFalse(listMembers.Contains(name),"PayConnect."+name+" should have been removed with PayConnect 1.0.");
			}
			//What PayConnect 2 still uses stays.
			Assert.AreEqual("Program Version",PayConnect.ProgramProperties.ProgramVersion);
			Assert.AreEqual("2",PayConnect.Version2);
			Assert.IsNotNull(typeof(PayConnect).GetMethod(nameof(PayConnect.BuildReceiptString)));
			StringAssert.Contains(PayConnect.GetVersion1RemovedText(),"no longer supported");
			StringAssert.Contains(PayConnect.GetPortalVoidRemovedText(),"Nothing was sent");
		}

		[TestMethod]
		public void DpsPosAndLog4net_AreNotReferencedByAnyProject() {
			string root=GetRoot();
			List<string> listOffenders=new List<string>();
			foreach(string pathProject in Directory.EnumerateFiles(root,"*.csproj",SearchOption.AllDirectories)) {
				string relative=pathProject.Substring(root.Length);
				if(IsBuildOutput(relative)) {
					continue;
				}
				string text=File.ReadAllText(pathProject);
				if(_arrayRemovedAssemblies.Any(x => text.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0)) {
					listOffenders.Add(relative);
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still references DpsPos or log4net: "+string.Join(", ",listOffenders));
			Assert.IsFalse(File.Exists(Path.Combine(root,"Required dlls","DpsPos.dll")),"Required dlls\\DpsPos.dll should have been deleted.");
			Assert.IsFalse(File.Exists(Path.Combine(root,"Required dlls","log4net.dll")),"Required dlls\\log4net.dll should have been deleted (only DpsPos used it).");
			Assert.IsFalse(File.Exists(Path.Combine(root,@"OpenDental\Resources\DpsPos.dll.config")));
			Assert.IsFalse(ReadRepoFile(@"OpenDental\Properties\Resources.resx").Contains("DpsPos"));
			Assert.IsFalse(ReadRepoFile(@"OpenDental\Properties\Resources.Designer.cs").Contains("DpsPos"));
		}

		[TestMethod]
		public void DpsPosAndLog4net_AreNotReferencedByTheBusinessLayer() {
			foreach(Assembly assembly in new[] { typeof(Patient).Assembly,typeof(CodeBase.Secrets).Assembly }) {
				List<string> listReferences=assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
				foreach(string removed in _arrayRemovedAssemblies) {
					Assert.IsFalse(listReferences.Contains(removed,StringComparer.OrdinalIgnoreCase),assembly.GetName().Name+" still references "+removed);
				}
			}
		}

		[TestMethod]
		public void DpsPosAndLog4net_AreNotReferencedByTheProgram() {
			foreach(string pathRelative in new[] { @"OpenDental\bin\Debug\OpenDental.exe",@"WpfControlsOD\bin\Debug\WpfControlsOD.dll" }) {
				string path=Path.Combine(GetRoot(),pathRelative);
				if(!File.Exists(path)) {
					Assert.Inconclusive(path+" is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				List<string> listReferences=GetReferencedAssemblyNames(path);
				foreach(string removed in _arrayRemovedAssemblies) {
					Assert.IsFalse(listReferences.Contains(removed,StringComparer.OrdinalIgnoreCase),Path.GetFileName(path)+" still references "+removed);
				}
			}
		}

		[TestMethod]
		public void Version1Window_IsGone() {
			string root=GetRoot();
			foreach(string file in new[] { "FormPayConnect.cs","FormPayConnect.designer.cs","FormPayConnect.resx" }) {
				Assert.IsFalse(File.Exists(Path.Combine(root,@"OpenDental\Forms",file)),file+" should have been deleted.");
			}
			string csproj=ReadRepoFile(@"OpenDental\OpenDental.csproj");
			Assert.IsFalse(csproj.Contains(@"Forms\FormPayConnect.cs"""),"OpenDental.csproj still compiles FormPayConnect.");
			Assert.IsFalse(csproj.Contains(@"Forms\FormPayConnect.resx"""));
			List<string> listHits=new List<string>();
			foreach(string relative in GetSourceFiles(".cs")) {
				string text=File.ReadAllText(root+relative);
				if(text.Contains("new FormPayConnect(") || text.Contains("DentalXChange.Dps.Pos") || text.Contains("PayConnectREST")
					|| text.Contains("ToPayConnectResponse(PosResponse"))
				{
					listHits.Add(relative);
				}
			}
			Assert.AreEqual(0,listHits.Count,"PayConnect 1.0 code still used in: "+string.Join(", ",listHits));
		}

		[TestMethod]
		public void SetupWindow_HasNoVersion1Fields() {
			foreach(string file in new[] { @"OpenDental\Forms\FormPayConnectSetup.cs",@"OpenDental\Forms\FormPayConnectSetup.designer.cs" }) {
				string text=ReadRepoFile(file);
				foreach(string removed in new[] { "radioVersion1","textUsername","textPassword","butGenerateToken","textToken","checkForceRecurring",
					"GetAccountToken" })
				{
					Assert.IsFalse(text.Contains(removed),file+" should not contain "+removed);
				}
			}
			StringAssert.Contains(ReadRepoFile(@"OpenDental\Forms\FormPayConnectSetup.cs"),"PayConnect.Version2",
				"Saving setup writes version 2.");
		}

		[TestMethod]
		public void Source_NamesNoVersion1HostOrClientHeader() {
			string root=GetRoot();
			string[] arrayForbidden=new[] { "dentalxchange.com/merchant","dentalxchange.com:443/merchant","dentalxchange.com/pay/rest",
				"GetClientRequestHeaders","GetClientRequestHeadersForWebURL" };
			List<string> listHits=new List<string>();
			foreach(string relative in GetSourceFiles(".cs",".config",".settings",".csproj")) {
				string text=File.ReadAllText(root+relative);
				if(arrayForbidden.Any(x => text.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0)) {
					listHits.Add(relative);
				}
			}
			Assert.AreEqual(0,listHits.Count,"PayConnect 1.0 hosts or client headers still named in: "+string.Join(", ",listHits));
			string payConnect=ReadRepoFile(@"OpenDentBusiness\WebBridges\PayConnect.cs");
			Assert.IsFalse(payConnect.Contains("ServiceID"),"PayConnect.cs must not carry a compiled-in service ID.");
		}
	}
}
