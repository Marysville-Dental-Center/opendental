// Clerri (Open Dental 25.4, v25_4-family-clerri): the dashboard address the button opens, and the setup rules. Nothing is opened and Clerri is
// never called; the sample addresses use the reserved example.com domain.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace VBRIDGE2Tests {
	[TestClass]
	public class ClerriProgramTests {
		private const string ADDRESS="https://dashboard.example.com/office?key=[ClientKey]&patient=[PatNum]";

		[TestMethod]
		public void Url_ReplacesTheKeyAndThePatNum() {
			string error;
			string url=ClerriProgram.BuildDashboardUrl(ADDRESS,"abc123",1042,out error);
			Assert.IsNull(error);
			Assert.AreEqual("https://dashboard.example.com/office?key=abc123&patient=1042",url);
		}

		[TestMethod]
		public void Url_EncodesTheKey() {
			string error;
			string url=ClerriProgram.BuildDashboardUrl(ADDRESS," a b&c=d/+? ",7,out error);
			Assert.AreEqual("https://dashboard.example.com/office?key=a%20b%26c%3Dd%2F%2B%3F&patient=7",url,"Trimmed, then URL-encoded.");
		}

		[TestMethod]
		public void Url_NoPatientLeavesThePatNumBlank() {
			string error;
			Assert.AreEqual("https://dashboard.example.com/office?key=k&patient=",ClerriProgram.BuildDashboardUrl(ADDRESS,"k",0,out error));
		}

		[TestMethod]
		public void Url_TagsMatchInAnyCase() {
			string error;
			string url=ClerriProgram.BuildDashboardUrl("https://dashboard.example.com/[clientkey]/[PATNUM]","k1",5,out error);
			Assert.AreEqual("https://dashboard.example.com/k1/5",url);
		}

		[TestMethod]
		public void Url_NoOtherPatientTagIsReplaced() {
			string error;
			string url=ClerriProgram.BuildDashboardUrl("https://dashboard.example.com/?n=[LName]&b=[Birthdate]&s=[SSN]&p=[PatNum]","k",9,out error);
			Assert.IsNull(error);
			Assert.AreEqual("https://dashboard.example.com/?n=[LName]&b=[Birthdate]&s=[SSN]&p=9",url,"Only [ClientKey] and [PatNum] are filled in.");
		}

		[TestMethod]
		public void Url_KeyTagWithoutAKeyIsAnError() {
			string error;
			Assert.IsNull(ClerriProgram.BuildDashboardUrl(ADDRESS,"",1,out error));
			Assert.IsNotNull(error);
			Assert.IsNull(ClerriProgram.BuildDashboardUrl(ADDRESS,"  ",1,out error));
			Assert.IsNotNull(error);
		}

		[TestMethod]
		public void Url_WithoutTheKeyTagNeedsNoKey() {
			string error;
			Assert.AreEqual("https://dashboard.example.com/",ClerriProgram.BuildDashboardUrl(" https://dashboard.example.com/ ","",0,out error));
			Assert.IsNull(error);
		}

		[TestMethod]
		public void Url_OnlyHttpsAddressesOpen() {
			foreach(string address in new[] { "","   ",null,"http://dashboard.example.com/","ftp://dashboard.example.com/","file:///C:/Windows/notepad.exe",
				@"C:\Windows\System32\cmd.exe","cmd.exe /c del","javascript:alert(1)","dashboard.example.com/[ClientKey]","https://","https:///path",
				"https://user:pass@dashboard.example.com/","mailto:someone@example.com" })
			{
				string error;
				Assert.IsNull(ClerriProgram.BuildDashboardUrl(address,"k",1,out error),"Address: "+address);
				Assert.IsNotNull(error,"Address: "+address);
				Assert.IsNotNull(ClerriProgram.ValidateDashboardAddress(address),"Address: "+address);
			}
		}

		[TestMethod]
		public void Url_AKeyCannotTurnTheAddressIntoAnotherHost() {
			string error;
			//The key is encoded, so characters in it can't end the host part or start a user name.
			string url=ClerriProgram.BuildDashboardUrl("https://dashboard.example.com/[ClientKey]","@evil.example.org/",1,out error);
			Assert.AreEqual("https://dashboard.example.com/%40evil.example.org%2F",url);
			Assert.AreEqual("dashboard.example.com",new Uri(url).Host);
		}

		[TestMethod]
		public void Validate_AcceptsAnHttpsAddressWithTags() {
			Assert.IsNull(ClerriProgram.ValidateDashboardAddress(ADDRESS));
			Assert.IsNull(ClerriProgram.ValidateDashboardAddress("https://[ClientKey].example.com/"),"A tag may sit in the host.");
			Assert.IsNull(ClerriProgram.ValidateDashboardAddress("HTTPS://dashboard.example.com:8443/x"));
		}

		[TestMethod]
		public void ToolbarButton_ChangeableOnlyWhileDisabled() {
			Assert.IsTrue(ClerriProgram.CanChangeToolbarButton(isEnabledSaved:false,isEnabledChecked:false));
			Assert.IsFalse(ClerriProgram.CanChangeToolbarButton(isEnabledSaved:false,isEnabledChecked:true));
			Assert.IsFalse(ClerriProgram.CanChangeToolbarButton(isEnabledSaved:true,isEnabledChecked:false),"Disable and save first.");
			Assert.IsFalse(ClerriProgram.CanChangeToolbarButton(isEnabledSaved:true,isEnabledChecked:true));
		}

		[TestMethod]
		public void Settings_MatchThePublicSetupPage() {
			Assert.AreEqual("Clerri",ClerriProgram.ProgDesc);
			Assert.AreEqual("Client Key",ClerriProgram.PropClientKey);
			Assert.AreEqual(EnumToolBar.FamilyModule,ClerriProgram.ToolBar);
		}

		[TestMethod]
		public void ContainsTag_IgnoresCase() {
			Assert.IsTrue(ClerriProgram.ContainsTag("x[clientKEY]y",ClerriProgram.TagClientKey));
			Assert.IsFalse(ClerriProgram.ContainsTag(null,ClerriProgram.TagClientKey));
			Assert.IsFalse(ClerriProgram.ContainsTag("[ClientKey",ClerriProgram.TagClientKey));
		}

		[TestMethod]
		public void LaunchLog_HasNumbersOnly() {
			string line=ProgramLinkLaunchLog.FormatLine("Clerri",12,3,0,1042);
			Assert.AreEqual("Launch Clerri ProgramNum=12 UserNum=3 ClinicNum=0 PatNum=1042",line);
		}
	}
}
