// Source checks for the payment windows (PAY-UI): the card-processor buttons are plain text buttons with no vendor logo, the old
// "show every processor when none is set up" fallback is gone, visibility goes through PaymentWindowRules, and CareCredit uses the in-repo client.
// Reads the source only; nothing leaves this computer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.PayUi {
	[TestClass]
	public class PaymentWindowNeutralityTests {
		private static readonly string[] _arrayLogoResources=new[] {
			"PaySimple_Button_2019_26x75","payconnect_btn_26","CareCredit_Button_NoTag_26x90","panelXcharge.BackgroundImage",
			"panelEdgeExpress.BackgroundImage" };

		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		///<summary>Removes // comments so that source checks look only at code.</summary>
		private static string StripLineComments(string source) {
			return string.Join("\n",source.Split('\n').Select(x => {
				int index=x.IndexOf("//");
				return index<0 ? x : x.Substring(0,index);
			}));
		}

		[TestMethod]
		public void PaymentWindows_ProcessorButtonsHaveNoVendorLogo() {
			foreach(string relativePath in new[] { @"OpenDental\Forms\FormPayment.designer.cs",@"OpenDental\Forms\FormClaimPayEdit.designer.cs",
				@"OpenDental\Forms\FormPayment.resx",@"OpenDental\Forms\FormClaimPayEdit.resx" })
			{
				string source=ReadRepoFile(relativePath);
				foreach(string logo in _arrayLogoResources) {
					Assert.IsFalse(source.Contains(logo),relativePath+" still uses "+logo);
				}
				Assert.IsFalse(source.Contains("BackgroundImageLayout"),relativePath+" still draws a processor button from an image.");
			}
		}

		[TestMethod]
		public void PaymentWindows_ProcessorButtonsAreTextButtons() {
			string formPayment=ReadRepoFile(@"OpenDental\Forms\FormPayment.designer.cs");
			foreach(string name in new[] { "butPayConnect","butPaySimple","butCareCredit","panelEdgeExpress","panelXcharge" }) {
				StringAssert.Contains(formPayment,"private OpenDental.UI.Button "+name+";",name);
			}
			string formClaimPayEdit=ReadRepoFile(@"OpenDental\Forms\FormClaimPayEdit.designer.cs");
			foreach(string name in new[] { "butPayConnect","butPaySimple","panelEdgeExpress","panelXcharge" }) {
				StringAssert.Contains(formClaimPayEdit,"private OpenDental.UI.Button "+name+";",name);
			}
		}

		[TestMethod]
		public void PaymentWindows_NoShowEveryProcessorFallback() {
			foreach(string relativePath in new[] { @"OpenDental\Forms\FormPayment.cs",@"OpenDental\Forms\FormClaimPayEdit.cs" }) {
				string code=StripLineComments(ReadRepoFile(relativePath));
				Assert.IsFalse(code.Contains("butPaySimple.Visible=true"),relativePath);
				Assert.IsFalse(code.Contains("butPayConnect.Visible=true"),relativePath);
				Assert.IsFalse(code.Contains("butPaySimple.Visible=progPaySimple.Enabled"),relativePath);
				Assert.IsFalse(code.Contains("butPayConnect.Visible=progPayConnect.Enabled"),relativePath);
				Assert.IsFalse(code.Contains("panelEdgeExpress.Visible=progEdgeExpress.Enabled"),relativePath);
				StringAssert.Contains(code,"PaymentWindowRules.ShowPayConnect(",relativePath);
				StringAssert.Contains(code,"PaymentWindowRules.ShowPaySimple(",relativePath);
				StringAssert.Contains(code,"PaymentWindowRules.ShowEdgeExpress(",relativePath);
			}
		}

		[TestMethod]
		public void FormPayment_UsesTheInRepoCareCreditClientAndNoSignupPage() {
			string code=StripLineComments(ReadRepoFile(@"OpenDental\Forms\FormPayment.cs"));
			Assert.IsFalse(code.Contains("using Bridges;"),"FormPayment must not use the prebuilt Bridges.dll.");
			StringAssert.Contains(code,"using OpenDentBusiness.Bridges;");
			Assert.IsFalse(code.Contains("ProviderSignupURL"),"No CareCredit sign-up page.");
			Assert.IsFalse(code.Contains("IsAdvertisingDisabled"),"No advertising toggle.");
			StringAssert.Contains(code,"butCareCredit.Visible=PaymentWindowRules.ShowCareCredit()");
		}
	}
}
