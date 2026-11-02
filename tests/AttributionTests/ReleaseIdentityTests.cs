// Tests for package REL-FILES (docs/port-notes/REL-FILES.md): the license list, THIRD-PARTY-NOTICES.txt, the product name shown in place of Open
// Dental's default title and SoftwareName, the About box copyright line, the removed Benco branding switch, and README.md and NOTICE.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace AttributionTests {
	[TestClass]
	public class ReleaseIdentityTests {
		///<summary>Every third-party component the release ships, by its name in THIRD-PARTY-NOTICES.txt (report-release.md 1.3 to 1.5).</summary>
		private static readonly string[] _arrayShippedComponents=new string[] {
			"MySqlConnector",".NET libraries","Newtonsoft.Json","MimeKit","MailKit","MSAL.NET","SSH.NET","Bouncy Castle Cryptography","PDFsharp",
			"MigraDoc","SharpDX","Tao Framework","Direct Project","NHunspell and Hunspell","Hunspell en_US dictionary",
			"RDL Project","Google APIs Client Library for .NET","fo-dicom","NDde","Microsoft Edge WebView2 SDK","TWAIN","Pearl API notice",
		};

		///<summary>Files of every listed component that ships in the output folder.</summary>
		private static readonly string[] _arrayShippedFiles=new string[] {
			"MySqlConnector.dll","Microsoft.Bcl.AsyncInterfaces.dll","Microsoft.Bcl.Cryptography.dll",
			"Microsoft.Extensions.DependencyInjection.Abstractions.dll","Microsoft.Extensions.Logging.Abstractions.dll","System.Buffers.dll",
			"System.Diagnostics.DiagnosticSource.dll","System.Formats.Asn1.dll","System.IO.Pipelines.dll","System.Memory.dll",
			"System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll","System.Text.Encodings.Web.dll","System.Text.Json.dll",
			"System.Threading.Tasks.Extensions.dll","Newtonsoft.Json.dll","MimeKit.dll","MailKit.dll",
			"Microsoft.Identity.Client.dll","Microsoft.IdentityModel.Abstractions.dll","Renci.SshNet.dll","BouncyCastle.Cryptography.dll","PdfSharp.dll",
			"PdfSharp.Charting.dll","MigraDoc.DocumentObjectModel.dll","MigraDoc.Rendering.dll","MigraDoc.RtfRendering.dll","SharpDX.dll",
			"SharpDX.Direct3D9.dll","SharpDX.Mathematics.dll","Tao.OpenGl.dll","Tao.Platform.Windows.dll","Health.Direct.Agent.dll",
			"Health.Direct.Common.dll","Health.Direct.ResolverPlugins.dll","NHunspell.dll","Hunspellx86.dll","RdlEngine.dll","RdlViewer.dll",
			"Google.Apis.dll","Google.Apis.Auth.dll","Google.Apis.Core.dll","Google.Apis.Gmail.v1.dll","Dicom.Core.dll","Dicom.Native.dll","NDde.dll",
			"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","Microsoft.Web.WebView2.Wpf.dll","WebView2Loader.dll",
		};

		///<summary>Names of the entries Open Dental's list had that are gone: its commercial registration-key terms, the removed API's and the ADA's
		///end-user licenses, Sparks3D, the stdole pre-release license, the wrong "Drifty" text for DotNetZip, and components that do not ship.</summary>
		private static readonly string[] _arrayRemovedEntries=new string[] {
			"OpenDental","OpenDental API End User","CDT","Sparks3D","Stdole","Drifty","Zxing","AForge","Angular","Bouncy Castle","BSD","Microsoft",
			"Oracle","Tao_Freeglut",
		};

		///<summary>What packages BIN-A and BIN-B delete, and Open Dental's own closed binaries: never listed as shipped.</summary>
		private static readonly string[] _arrayNotShipped=new string[] {
			"Sparks3D","SigPlusNET","Interop.Word","Interop.Microsoft.Office","Interop.VBIDE","stdole","AcroPDF","Acrobat","zxing","Avalara","RS232","DotNetZip",
			"Ionic","Drifty","VBbridges","xImageDeviceManager","EZTwain","Eztwain","SuniUSB","VideoCamLib",
		};

		#region Helpers
		private static string RepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("Repository root not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		private static string ReadRepoFile(params string[] pathParts) {
			string path=Path.Combine(new[] { RepoRoot() }.Concat(pathParts).ToArray());
			Assert.IsTrue(File.Exists(path),"Missing file: "+path);
			return File.ReadAllText(path);
		}

		private static string Unwrap(string text) {
			return string.Join(" ",text.Split(new[] { "\r\n","\n" },StringSplitOptions.None).Select(x => x.Trim()).Where(x => x!=""));
		}

		private static string NormalizeNewLines(string text) {
			return text.Replace("\r\n","\n").Replace("\r","\n");
		}

		private static List<NoticeSection> ParseNoticesFile() {
			return LicenseList.ParseNotices(ReadRepoFile("THIRD-PARTY-NOTICES.txt"));
		}

		private static Assembly MakeAssembly(Version version,string informationalVersion) {
			AssemblyName assemblyName=new AssemblyName("ReleaseIdentityTestsDynamic"+Guid.NewGuid().ToString("N"));
			assemblyName.Version=version;
			CustomAttributeBuilder[] arrayAttributes=new CustomAttributeBuilder[0];
			if(informationalVersion!=null) {
				ConstructorInfo constructorInfo=typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) });
				arrayAttributes=new[] { new CustomAttributeBuilder(constructorInfo,new object[] { informationalVersion }) };
			}
			return AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,AssemblyBuilderAccess.Run,arrayAttributes);
		}

		///<summary>Removes // and /* */ comments (not inside string literals).</summary>
		private static string StripComments(string text) {
			return Regex.Replace(text,@"""(?:[^""\\]|\\.)*""|//[^\n]*|/\*.*?\*/",m => m.Value.StartsWith("/") ? "" : m.Value,RegexOptions.Singleline);
		}
		#endregion Helpers

		#region License list
		[TestMethod]
		public void LicenseList_ProgramLicenseIsFirst_GplV2OrLater() {
			List<LicenseEntry> listLicenseEntries=LicenseList.GetEntries();
			Assert.AreEqual(LicenseList.NameProgram,listLicenseEntries[0].Name);
			StringAssert.Contains(listLicenseEntries[0].Name,"GNU GPL v2 or later");
			string text=listLicenseEntries[0].Text;
			Assert.AreEqual(LicenseList.GetProgramLicenseText(),text,"FormLicense selects the first entry by its text.");
			StringAssert.Contains(text,AboutBoxInfo.Attribution);
			StringAssert.Contains(text,"either version 2 of the License, or (at your option) any later version");
			StringAssert.Contains(text,"GNU General Public License");
			StringAssert.Contains(text,"Version 2, June 1991","Resources/GPL.txt");
			StringAssert.Contains(text,"TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION");
			Assert.AreEqual(LicenseList.NameThirdPartyNotices,listLicenseEntries[1].Name);
		}

		[TestMethod]
		public void LicenseList_ListsEveryShippedComponentOnce_WithItsLicense() {
			List<LicenseEntry> listLicenseEntries=LicenseList.GetEntries();
			List<string> listNames=listLicenseEntries.Select(x => x.Name).ToList();
			Assert.AreEqual(listNames.Count,listNames.Distinct().Count(),"No name twice: "+string.Join(", ",listNames));
			CollectionAssert.AreEqual(_arrayShippedComponents,listNames.Skip(2).ToArray(),"One entry per component, in the notices file's order.");
			List<NoticeSection> listNoticeSections=LicenseList.ParseNotices(OpenPmsDental.Properties.Resources.ThirdPartyNotices);
			List<NoticeSection> listLicenseSections=listNoticeSections.FindAll(x => !x.IsComponent);
			Dictionary<string,string> dictVendorTexts=LicenseList.GetVendorTexts();
			foreach(NoticeSection noticeSection in listNoticeSections.FindAll(x => x.IsComponent)) {
				foreach(string field in new[] { "Files","Version","Copyright","License","Source" }) {
					Assert.AreNotEqual("",noticeSection.GetField(field),noticeSection.Title+": "+field);
				}
				string licenseText=LicenseList.GetLicenseText(noticeSection,listLicenseSections,dictVendorTexts);
				//Every component shows a license text, or names where the license is published.
				Assert.IsTrue(licenseText!="" || noticeSection.GetField("License").Contains("https://") || noticeSection.GetField("License")=="Public domain"
					|| noticeSection.Title=="Hunspell en_US dictionary",noticeSection.Title+" has no license text and no address.");
				LicenseEntry licenseEntry=listLicenseEntries.Single(x => x.Name==noticeSection.Title);
				StringAssert.Contains(licenseEntry.Text,noticeSection.GetField("Copyright"));
				if(licenseText!="") {
					StringAssert.Contains(licenseEntry.Text,licenseText);
				}
			}
			//Each project text kept from Open Dental's list belongs to a listed component.
			foreach(string name in dictVendorTexts.Keys) {
				CollectionAssert.Contains(_arrayShippedComponents,name);
				Assert.IsFalse(string.IsNullOrWhiteSpace(dictVendorTexts[name]),name);
			}
			StringAssert.Contains(listLicenseEntries.Single(x => x.Name=="MySqlConnector").Text,"Permission is hereby granted, free of charge");
			StringAssert.Contains(listLicenseEntries.Single(x => x.Name=="Google APIs Client Library for .NET").Text,"Version 2.0, January 2004");
			StringAssert.Contains(listLicenseEntries.Single(x => x.Name=="fo-dicom").Text,"Microsoft Public License (MS-PL)");
			StringAssert.Contains(listLicenseEntries.Single(x => x.Name=="Direct Project").Text,"Redistribution and use in source and binary forms");
			StringAssert.Contains(listLicenseEntries.Single(x => x.Name=="NDde").Text,"Shared Source License for NDde");
		}

		[TestMethod]
		public void LicenseList_DropsOpenDentalTermsAndWhatDoesNotShip() {
			List<LicenseEntry> listLicenseEntries=LicenseList.GetEntries();
			foreach(string name in _arrayRemovedEntries) {
				Assert.IsFalse(listLicenseEntries.Any(x => x.Name==name),"Still listed: "+name);
			}
			foreach(LicenseEntry licenseEntry in listLicenseEntries) {
				Assert.IsFalse(licenseEntry.Text.Contains("Registration Key"),licenseEntry.Name+": Open Dental's registration-key terms");
				Assert.IsFalse(licenseEntry.Text.Contains("Current Dental Terminology"),licenseEntry.Name+": the ADA CDT license");
				Assert.IsFalse(licenseEntry.Text.Contains("Drifty"),licenseEntry.Name);
			}
			//The resources are gone too, so nothing can show them again.
			List<string> listResourceNames=typeof(OpenPmsDental.Properties.Resources).GetProperties(BindingFlags.Public|BindingFlags.Static)
				.Select(x => x.Name).ToList();
			foreach(string resourceName in new[] { "OpenDentalLicense","OpenDentalApiEndUserLicense","CDT_Content_End_User_License",
				"CDT_Content_End_User_License1","Sparks3D","stdole","Ionic","Zxing","AForge","Angular","BouncyCastle","Bsd","Microsoft","Oracle",
				"Tao_Freeglut","Tamir","Mentalis" })
			{
				CollectionAssert.DoesNotContain(listResourceNames,resourceName);
			}
			foreach(string fileName in new[] { "opendentallicense.txt","opendentalapienduserlicense.txt","CDT Content End User License.txt",
				"CDT Content End User License.rtf","Sparks3D.txt","stdole.txt","Ionic.txt","Zxing.txt","Tamir.txt","Mentalis.txt" })
			{
				Assert.IsFalse(File.Exists(Path.Combine(RepoRoot(),"OpenPmsDental","Resources",fileName)),fileName);
			}
		}

		[TestMethod]
		public void LicenseForms_ShowTheSharedList() {
			string formLicense=StripComments(ReadRepoFile("OpenPmsDental","Forms","FormLicense.cs"));
			StringAssert.Contains(formLicense,"LicenseList.GetEntries()");
			StringAssert.Contains(formLicense,"_selectedLicense=LicenseList.GetProgramLicenseText();");
			string formRegistrationKey=StripComments(ReadRepoFile("OpenPmsDental","Forms","FormRegistrationKey.cs"));
			StringAssert.Contains(formRegistrationKey,"LicenseList.GetEntries()");
			StringAssert.Contains(formRegistrationKey,"listBoxRegistration.SetSelected(0);");
			foreach(string text in new[] { formLicense,formRegistrationKey }) {
				Assert.IsFalse(text.Contains("Properties.Resources.OpenDentalLicense"));
				Assert.IsFalse(text.Contains("Items.Add(\""),"No hand-made list left.");
			}
		}
		#endregion License list

		#region THIRD-PARTY-NOTICES.txt
		[TestMethod]
		public void ThirdPartyNotices_IsTheEmbeddedResource() {
			Assert.AreEqual(NormalizeNewLines(ReadRepoFile("THIRD-PARTY-NOTICES.txt")),NormalizeNewLines(OpenPmsDental.Properties.Resources.ThirdPartyNotices));
		}

		[TestMethod]
		public void ThirdPartyNotices_NamesEveryShippedFileAndSource() {
			List<NoticeSection> listNoticeSections=ParseNoticesFile();
			CollectionAssert.AreEqual(_arrayShippedComponents,listNoticeSections.FindAll(x => x.IsComponent).Select(x => x.Title).ToArray());
			string files=string.Join(" ",listNoticeSections.FindAll(x => x.IsComponent).Select(x => x.GetField("Files")));
			foreach(string fileName in _arrayShippedFiles) {
				Assert.IsTrue(Regex.IsMatch(files,@"(?<![\w.])"+Regex.Escape(fileName)+@"(?![\w.])"),"Not listed: "+fileName);
			}
			foreach(NoticeSection noticeSection in listNoticeSections.FindAll(x => x.IsComponent)) {
				string source=noticeSection.GetField("Source");
				Assert.IsTrue(source.Contains("https://") || source.Contains("this repository") || source=="not applicable",
					noticeSection.Title+": "+source);
			}
			CollectionAssert.AreEquivalent(new[] { "MIT License","BSD 3-Clause License","Microsoft Public License (MS-PL)","Apache License 2.0" },
				listNoticeSections.FindAll(x => !x.IsComponent).Select(x => x.Title).ToArray());
		}

		[TestMethod]
		public void ThirdPartyNotices_MarksTheGplQuestions() {
			List<NoticeSection> listNoticeSections=ParseNoticesFile();
			foreach(string title in new[] { "fo-dicom","NDde","Google APIs Client Library for .NET" }) {
				StringAssert.Contains(listNoticeSections.Single(x => x.Title==title).GetField("Note"),"Owner question",title);
			}
			StringAssert.Contains(listNoticeSections.Single(x => x.Title=="fo-dicom").GetField("License"),"MS-PL");
		}

		[TestMethod]
		public void ThirdPartyNotices_ListsNothingThatMustNotShip() {
			string notices=ReadRepoFile("THIRD-PARTY-NOTICES.txt");
			foreach(string name in _arrayNotShipped) {
				Assert.IsFalse(notices.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0,"Listed although it does not ship: "+name);
			}
			Assert.IsFalse(Regex.IsMatch(notices,@"\bD\d{4}\b"),"No CDT code.");
		}
		#endregion THIRD-PARTY-NOTICES.txt

		#region Product name at display time
		[TestMethod]
		public void DisplayName_ShowsTheProductInPlaceOfOpenDentalsDefaults() {
			foreach(string value in new[] { "Open Dental","Open Dental Software"," open dental ","OPEN DENTAL SOFTWARE",
				"Benco Practice Management powered by Open Dental","Benco Practice Management" })
			{
				Assert.IsTrue(AboutBoxInfo.IsDefaultProductName(value),value);
				Assert.AreEqual("Open PMS - Dental",AboutBoxInfo.GetDisplayName(value),value);
			}
		}

		[TestMethod]
		public void DisplayName_KeepsAnOfficesOwnTitle() {
			foreach(string value in new[] { "Smile Dental - Front Desk","Open Dental Clinic North","","Open Dental 2" }) {
				Assert.IsFalse(AboutBoxInfo.IsDefaultProductName(value),value);
				Assert.AreEqual(value,AboutBoxInfo.GetDisplayName(value));
			}
			Assert.AreEqual("",AboutBoxInfo.GetDisplayName(null));
			Assert.IsFalse(AboutBoxInfo.IsDefaultProductName(null));
		}

		[TestMethod]
		public void DisplayName_IsUsedForTheTitleAndTheRestartPrompts() {
			string patientL=StripComments(ReadRepoFile("OpenPmsDental","Logic","PatientL.cs"));
			Assert.IsFalse(patientL.Contains("PrefName.MainWindowTitle"),"PatientL reads the title through AboutBoxInfo.");
			Assert.AreEqual(2,Regex.Matches(patientL,@"string retVal=AboutBoxInfo\.GetMainWindowTitle\(\);").Count,"GetMainTitle and GetMainTitleSamePat.");
			string formOpenDental=StripComments(ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs"));
			Assert.IsFalse(formOpenDental.Contains("PrefName.SoftwareName"));
			Assert.AreEqual(3,Regex.Matches(formOpenDental,@"Lan\.g\(this,""You will have to restart""\)\+"" ""\+AboutBoxInfo\.GetSoftwareName\(\)").Count);
			string formUpdateInProgress=StripComments(ReadRepoFile("OpenPmsDental","Forms","FormUpdateInProgress.cs"));
			Assert.IsFalse(formUpdateInProgress.Contains("PrefName.SoftwareName"));
			StringAssert.Contains(formUpdateInProgress,"AboutBoxInfo.GetSoftwareName()");
			//The preferences are only read, never written, so adoption's row check is unaffected.
			string formAbout=StripComments(ReadRepoFile("OpenPmsDental","Forms","FormAbout.cs"));
			Assert.IsFalse(Regex.IsMatch(formAbout,@"Prefs\.Update"),"FormAbout.cs writes no preference.");
		}

		[TestMethod]
		public void AboutBox_CopyrightLineCreditsOpenDentalWhateverSoftwareNameSays() {
			string formAbout=StripComments(ReadRepoFile("OpenPmsDental","Forms","FormAbout.cs"));
			StringAssert.Contains(formAbout,
				"labelCopyright.Text=\"Open Dental Software \"+Lan.g(this,\"Copyright 2003-\")+DateTime.Now.ToString(\"yyyy\")+\", Jordan Sparks, D.M.D.\";");
			int indexLoad=formAbout.IndexOf("private void FormAbout_Load(");
			int indexNext=formAbout.IndexOf("private void butDiagnostics_Click(");
			Assert.IsTrue(indexLoad>=0 && indexNext>indexLoad);
			Assert.IsFalse(formAbout.Substring(indexLoad,indexNext-indexLoad).Contains("SoftwareName"),"The copyright line is not built from SoftwareName.");
		}

		[TestMethod]
		public void UpdateHistory_IsLookedUpByReleaseThenCompatibilityVersion() {
			CollectionAssert.AreEqual(new[] { "0.0","24.3.63.0" },AboutBoxInfo.GetUpdateHistoryVersions(MakeAssembly(new Version(24,3,63,0),"0.0")));
			CollectionAssert.AreEqual(new[] { "24.3.63.0" },AboutBoxInfo.GetUpdateHistoryVersions(MakeAssembly(new Version(24,3,63,0),null)));
			StringAssert.Contains(ReadRepoFile("OpenPmsDental","Forms","FormAbout.cs"),"AboutBoxInfo.GetUpdateHistory(assemblyProgram)");
			StringAssert.Contains(ReadRepoFile("OpenPmsDental","Forms","FormUpdate.cs"),"AboutBoxInfo.GetUpdateHistory(assemblyProgram)");
		}

		[TestMethod]
		public void BencoSetup_NoLongerSwitchesBranding() {
			string frmBencoSetup=StripComments(ReadRepoFile("WpfControlsOD","Frms","FrmBencoSetup.xaml.cs"));
			Assert.IsFalse(frmBencoSetup.Contains("UpdateBencoSettings"));
			Assert.IsFalse(frmBencoSetup.Contains("PrefName.MainWindowTitle"));
			Assert.IsFalse(frmBencoSetup.Contains("PrefName.SoftwareName"));
			Assert.IsFalse(frmBencoSetup.Contains("Prefs.Update"));
			//The program link itself stays.
			StringAssert.Contains(frmBencoSetup,"Programs.Update(_program);");
			StringAssert.Contains(frmBencoSetup,"ToolButItems.Insert(newBut);");
		}
		#endregion Product name at display time

		#region README.md and NOTICE
		[TestMethod]
		public void Readme_SaysGplV2OrLater_AndHasNoStaleBuildStatements() {
			string readme=ReadRepoFile("README.md");
			StringAssert.Contains(readme,"GNU GPL v2 or later");
			StringAssert.Contains(readme,"either version 2 of the License, or (at your option) any later version");
			StringAssert.Contains(readme,"(THIRD-PARTY-NOTICES.txt)");
			Assert.IsFalse(readme.Contains("Prebuilt DLLs"),"No prebuilt Open Dental DLLs remain.");
			Assert.IsFalse(Regex.IsMatch(readme,"Open Dental.{1,3}internal prebuilt"),"Required dlls holds no Open Dental-internal assemblies.");
			Assert.IsFalse(readme.Contains("21 projects"));
			Assert.IsFalse(readme.Contains("merge it from its version branch"),"We don't merge from upstream (decoupling plan).");
			Assert.IsFalse(readme.Contains("Developer tools, not in the solution"),"tools/ and tests/ are in the solution filter.");
			StringAssert.Contains(readme,"every test project under `tests/`");
		}

		[TestMethod]
		public void Readme_HasTheOfficeInstall() {
			string readme=ReadRepoFile("README.md");
			foreach(string text in new[] {
				"## Installing in an office","MariaDB Server","**10.11**","sql_mode=","default-storage-engine=MyISAM","max_allowed_packet=40M",
				"at least 40 MB",".NET Framework 4.8","Visual C++ 2015-2022 Redistributable, x86","vc_redist.x86.exe","WebView2 Runtime, x86",
				"(db/README.md)","db/init/new-database.sql","(docs/release-process.md)","C:\\Program Files (x86)\\Open PMS - Dental\\",
				"(docs/adoption-runbook.md)" })
			{
				StringAssert.Contains(readme,text);
			}
		}

		[TestMethod]
		public void Notice_SaysGplV2OrLater_AndPointsToTheThirdPartyNotices() {
			string notice=Unwrap(ReadRepoFile("NOTICE"));
			StringAssert.Contains(notice,"GNU General Public License, version 2 or (at your option) any later version");
			StringAssert.Contains(notice,"THIRD-PARTY-NOTICES.txt");
			StringAssert.Contains(notice,"Help > About > View Licenses");
			Assert.IsTrue(File.Exists(Path.Combine(RepoRoot(),"THIRD-PARTY-NOTICES.txt")));
		}
		#endregion README.md and NOTICE
	}
}
