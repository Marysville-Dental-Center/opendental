// Tests for the ERX package that read the source: no HQ path, no compiled-in NewCrop credentials, vendor URLs in one place each, redacted logs,
// neutral wording.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Erx {
	[TestClass]
	public class ErxSourceTests {
		///<summary>The eRx files the ERX package owns.</summary>
		private static readonly string[] _arrayErxFiles={
			@"OpenDentBusiness\WebBridges\Erx\DoseSpot.cs",
			@"OpenDentBusiness\WebBridges\Erx\DoseSpotV2.cs",
			@"OpenDentBusiness\WebBridges\Erx\Erx.cs",
			@"OpenDentBusiness\WebBridges\Erx\ErxXml.cs",
			@"OpenDentBusiness\WebBridges\Erx\NewCrop\NewCrop.cs",
			@"OpenDentBusiness\WebBridges\Erx\NewCrop\NewCropConfig.cs",
			@"OpenDentBusiness\WebBridges\Erx\DoseSpot\DoseSpotApiClient.cs",
			@"OpenDentBusiness\WebBridges\Erx\DoseSpot\DoseSpotConfig.cs",
			@"OpenDentBusiness\WebBridges\Erx\DoseSpot\DoseSpotSso.cs",
			@"OpenDentBusiness\WebBridges\Erx\DoseSpot\ErxPollThrottle.cs",
			@"OpenDental\Forms\FormErxSetup.cs",
			@"OpenDental\Forms\FormErxSetup.Designer.cs",
			@"OpenDental\Forms\FormDoseSpotProperyEdit.cs",
			@"OpenDental\Forms\FormDoseSpotProperyEdit.designer.cs",
			@"OpenDental\Forms\FormErx.cs",
		};

		private static string Read(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		private static IEnumerable<string> GetSourceFiles() {
			string root=FoundationLayoutTests.GetRepositoryRoot();
			foreach(string folder in new[] { "OpenDentBusiness","OpenDental","WpfControlsOD","CodeBase" }) {
				foreach(string path in Directory.EnumerateFiles(Path.Combine(root,folder),"*.cs",SearchOption.AllDirectories)) {
					string relative=path.Substring(root.Length).TrimStart('\\','/');
					if(Regex.IsMatch(relative,@"(^|\\)(bin|obj)\\")) {
						continue;
					}
					yield return relative;
				}
			}
		}

		[TestMethod]
		public void ErxFiles_HaveNoHqPathOrDebugSwitch() {
			Regex regex=new Regex(@"CustomerUpdatesProxy|WebServiceMainHQ|BuildOAuthUrl|RequestCustomerID|GetErxAccess|GetClinicErxAccess|ODBuild\.IsDebug|CustomerPatNum|MigrateDoseSpotIfNeeded");
			foreach(string file in _arrayErxFiles) {
				Match match=regex.Match(Read(file));
				Assert.IsFalse(match.Success,file+" still has "+match.Value);
			}
		}

		[TestMethod]
		public void ControlChart_ErxPathsAreLocal() {
			string text=Read(@"OpenDental\Main Modules\ControlChart.cs");
			StringAssert.DoesNotMatch(text,new Regex(@"CustomerUpdatesProxy|RequestCustomerID|GetErxAccess|SyncClinicErxsWithHQ|MigrateDoseSpotIfNeeded"
				+@"|ComposeNewRxNewCropWebRequest|NewCropIsAccountIdValid|DoseSpotV2\.|DoseSpotApiVersion|UpdateServerAddress"));
			StringAssert.DoesNotMatch(text,new Regex(@"Contact support to enable eRx|see our online manual|Fees are associated",RegexOptions.IgnoreCase));
			//Every erxlog row gets redacted text: the SSO codes and the NewCrop credentials never reach the database.
			MatchCollection matches=Regex.Matches(text,@"erxLog\w*\.MsgText=([^;]*);");
			Assert.IsTrue(matches.Count>=2);
			foreach(Match match in matches) {
				Assert.IsTrue(match.Groups[1].Value.Contains("RedactForLog") || match.Groups[1].Value.Contains("redacted"),"Unredacted erxlog text: "+match.Value);
			}
		}

		[TestMethod]
		public void NewCrop_HasNoCompiledInCredentials() {
			string text=Read(@"OpenDentBusiness\WebBridges\Erx\NewCrop\NewCrop.cs");
			Assert.IsFalse(text.Contains("MiscUtils.Decrypt(\""),"Open Dental's encrypted NewCrop credentials must stay deleted.");
			StringAssert.DoesNotMatch(text,new Regex("\"OpenDental(Lexi)?\""));
			foreach(string file in _arrayErxFiles) {
				Assert.IsFalse(Regex.IsMatch(Read(file),@"Decrypt\(""[A-Za-z0-9+/=]{16,}""\)"),file);
			}
		}

		[TestMethod]
		public void VendorUrls_LiveOnlyInTheirConfigClasses() {
			//Identifiers (XML and SOAP namespaces in NCScript.cs and the generated web references) and the web reference defaults in
			//Settings.Designer.cs are not calls, so they are allowed.
			string[] arrayAllowedNewCrop={ @"OpenDentBusiness\WebBridges\Erx\NewCrop\NewCropConfig.cs",@"OpenDentBusiness\WebBridges\Erx\NewCrop\NCScript.cs",
				@"OpenDentBusiness\Properties\Settings.Designer.cs" };
			string[] arrayAllowedDoseSpot={ @"OpenDentBusiness\WebBridges\Erx\DoseSpot\DoseSpotConfig.cs",@"OpenDentBusiness\Properties\Settings.Designer.cs" };
			foreach(string file in GetSourceFiles()) {
				if(file.Contains(@"\Web References\")) {
					continue;
				}
				string text=File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),file));
				if(Regex.IsMatch(text,@"https?://[a-z.]*newcropaccounts\.com",RegexOptions.IgnoreCase)) {
					Assert.IsTrue(arrayAllowedNewCrop.Contains(file,StringComparer.OrdinalIgnoreCase),"NewCrop URL outside NewCropConfig: "+file);
				}
				if(Regex.IsMatch(text,@"https?://[a-z.]*dosespot\.com",RegexOptions.IgnoreCase)) {
					Assert.IsTrue(arrayAllowedDoseSpot.Contains(file,StringComparer.OrdinalIgnoreCase),"DoseSpot URL outside DoseSpotConfig: "+file);
				}
			}
			StringAssert.DoesNotMatch(Read(@"OpenDentBusiness\Properties\Settings.Designer.cs"),new Regex(@"http://secure\.newcropaccounts"));
		}

		[TestMethod]
		public void ErxScreens_AreNeutral() {
			Regex regex=new Regex(@"Open Dental|eRxTeamPW|acknowledge and approve|See the website|online manual|contact support|Call support",RegexOptions.IgnoreCase);
			foreach(string file in _arrayErxFiles.Where(x => x.StartsWith(@"OpenDental\"))) {
				Match match=regex.Match(Read(file));
				Assert.IsFalse(match.Success,file+" has \""+match.Value+"\"");
			}
			StringAssert.DoesNotMatch(Read(@"OpenDental\Forms\FormProvEdit.cs"),new Regex(@"acknowledge and approve Electronic Rx|See the website"));
			string business=Read(@"OpenDentBusiness\WebBridges\Erx\DoseSpot.cs")+Read(@"OpenDentBusiness\WebBridges\Erx\Erx.cs");
			StringAssert.DoesNotMatch(business,new Regex(@"Call support|acknowledge Electronic Prescription fees"));
		}

		[TestMethod]
		public void FormErx_KeepsDevToolsClosedAndGuardsNavigation() {
			string text=Read(@"OpenDental\Forms\FormErx.cs");
			StringAssert.Contains(text,"AreDevToolsEnabled=false");
			StringAssert.Contains(text,"NavigationStarting+=");
			StringAssert.Contains(text,"NewCropConfig.Load().GetRxEntryUrl()");
		}

		[TestMethod]
		public void ErxSetup_EncryptsSecretsBeforeSaving() {
			string text=Read(@"OpenDental\Forms\FormErxSetup.cs");
			StringAssert.Contains(text,"SecretValue.Protect(");
			StringAssert.Contains(text,"DoseSpotConfig.EnsureProperties()");
			StringAssert.Contains(text,"NewCropConfig.InsertMissingProperties()");
			//The ensure calls come before the list is loaded, so ProgramProperties.Sync never deletes the new rows.
			Assert.IsTrue(text.IndexOf("NewCropConfig.InsertMissingProperties()")<text.IndexOf("_listProgramProperties=ProgramProperties.GetForProgram("));
		}
	}
}
