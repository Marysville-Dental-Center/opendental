// The Imaging module cannot be opened in a test run (the GUI is not launched), so these tests read the sources and check that the Pearl send
// button and the Pearl status panel use the shared rules in PearlRequests, which PearlStatusRulesTests tests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VPEARL1Tests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		///<summary>The text of one method: from its signature to its closing brace, the first brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		[TestMethod]
		public void SendButton_UsesTheResendRule() {
			string body=MethodBody(Read("OpenPmsDental","Main Modules","ControlImages.cs"),"private void ToolBarProgram_Click(");
			StringAssert.Contains(body,"PearlRequests.CanSendToPearl(pearlRequest)");
			StringAssert.Contains(body,"PearlRequests.GetCannotSendText(pearlRequest)");
			Assert.IsFalse(body.Contains("EnumPearlStatus.TimedOut"),"The 24.3 rule (only TimedOut is sent again) is gone.");
		}

		[TestMethod]
		public void StatusPanel_ShowsEveryStatusText() {
			string body=MethodBody(Read("OpenPmsDental","Main Modules","ControlImages.cs"),"private bool ShouldPearlPanelShow(");
			StringAssert.Contains(body,"PearlRequests.HasStatusToShow(pearlRequest)");
			StringAssert.Contains(body,"PearlRequests.GetStatusText(pearlRequest.RequestStatus)");
			Assert.IsFalse(body.Contains("RequestStatus.ToString()"),"The enum name is no longer shown as the status.");
		}

		[TestMethod]
		public void Bridge_UsesTheResendRule() {
			string source=Read("OpenPmsDentalBusiness","Bridges","Pearl","Pearl.cs");
			string bodySetup=MethodBody(source,"public static Pearl SetupPearlForSendingSingle(");
			StringAssert.Contains(bodySetup,"PearlRequests.CanSendToPearl(pearlRequest)");
			Assert.IsFalse(bodySetup.Contains("EnumPearlStatus.TimedOut"));
			string bodyWorker=MethodBody(source,"public void SendOnThreadWorker(");
			StringAssert.Contains(bodyWorker,"PearlRequests.PrepareToSend(pearlRequest)");
			StringAssert.Contains(bodyWorker,"PearlRequests.SetUploadFailed(Document_.DocNum)");
		}
	}
}
