// Tests that the QuickBooks Online package stays wired as README section 3.5 says: in-repo types replace Bridges.dll's, the callers import them,
// nothing in the package reaches the HQ web service, and the relay page is self-contained.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness.Qbo;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboLayoutTests {
		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		[TestMethod]
		public void Types_LiveInOpenDentBusinessQbo_NotInABridgesNamespace() {
			foreach(Type type in new[] { typeof(QuickBooksOnline),typeof(QuickBooksOnlineToken),typeof(QuickBooksOnlineAccount),typeof(QuickBooksOnlineClassRef),
				typeof(QuickBooksOnlineDepositResponse) })
			{
				Assert.AreEqual("OpenDentBusiness.Qbo",type.Namespace,type.Name);
				Assert.AreEqual("OpenDentBusiness",type.Assembly.GetName().Name,type.Name);
			}
		}

		[TestMethod]
		public void Callers_ImportTheInRepoTypes_NotBridges() {
			foreach(string file in new[] { @"OpenDental\Forms\FormDepositEdit.cs",@"OpenDental\Forms\FormQuickBooksOnlineSetup.cs",
				@"OpenDental\Forms\FormQBAccountSelect.cs" })
			{
				string text=ReadRepoFile(file);
				Assert.IsFalse(Regex.IsMatch(text,@"^\s*using\s+Bridges\s*;",RegexOptions.Multiline),file+" still imports Bridges.");
				StringAssert.Contains(text,"using OpenDentBusiness.Qbo;",file);
			}
			string textDeposit=ReadRepoFile(@"OpenDental\Forms\FormDepositEdit.cs");
			Assert.IsFalse(textDeposit.Contains("_programPropertyQboAccessToken"),"The dead access token field is gone.");
			Assert.IsFalse(textDeposit.Contains("SaveTokens("),"Token saving lives in QboTokenManager.");
			StringAssert.Contains(textDeposit,"QuickBooksOnline.NewRequestId()");
		}

		[TestMethod]
		public void Package_NeverCallsTheHqWebService() {
			string folder=Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),@"OpenDentBusiness\Bridges\QuickBooksOnline");
			string[] arrayFiles=Directory.GetFiles(folder,"*.cs");
			Assert.IsTrue(arrayFiles.Length>=10);
			foreach(string file in arrayFiles) {
				string text=File.ReadAllText(file);
				foreach(string forbidden in new[] { "BuildOAuthUrl","WebServiceMainHQ","IWebServiceMainHQ","CustomerUpdates","ODCloudClient","using Bridges;" }) {
					Assert.IsFalse(text.Contains(forbidden),Path.GetFileName(file)+" contains "+forbidden);
				}
				StringAssert.Contains(text,"GNU General Public License",Path.GetFileName(file));
			}
		}

		[TestMethod]
		public void RelayPage_IsSelfContained() {
			string html=ReadRepoFile(@"docs\integrations\qbo-redirect.html");
			StringAssert.Contains(html,"var RELAY_PORT = 0;");
			Assert.IsFalse(Regex.IsMatch(html,@"\b(src|href)\s*=",RegexOptions.IgnoreCase),"No external scripts, styles, images or links.");
			Assert.IsFalse(Regex.IsMatch(html,@"\b(fetch|XMLHttpRequest|sendBeacon)\b"),"The page sends nothing anywhere.");
			string[] arrayUrls=Regex.Matches(html,@"https?://[^\s""'<)]+").Cast<Match>().Select(x => x.Value).ToArray();
			Assert.IsTrue(arrayUrls.All(x => x.StartsWith("http://localhost:") || x.StartsWith("https://<office-domain>")),string.Join(", ",arrayUrls));
		}
	}
}
