// Stage 0 of the .NET 10 move (S0-2a, docs/specs/net10-plan.md, decision 11): the BCBS Georgia dial-up terminal is cut. TerminalConnector (the
// modem on System.IO.Ports.SerialPort, BIN-B), its interface, the BCBSGA comm bridge code and the terminal window go. A clearinghouse whose
// Comm Bridge is still BCBSGA sends nothing and says why; EclaimsCommBridge.BCBSGA keeps its number, because clearinghouse.CommBridge stores it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BINBTests {
	[TestClass]
	public class DialupTerminalRemovedTests {
		[TestMethod]
		public void CommBridgeNumbers_Stay() {
			//The enum is implicitly numbered and the clearinghouse rows store the number.
			Assert.AreEqual(2,(int)EclaimsCommBridge.BCBSGA);
			Assert.AreEqual("BCBSGA",((EclaimsCommBridge)2).ToString());
			Assert.AreEqual(1,(int)EclaimsCommBridge.WebMD);
			Assert.AreEqual(3,(int)EclaimsCommBridge.Renaissance);
		}

		[TestMethod]
		public void DialupFiles_AreGone() {
			string root=TestPaths.RepoRoot;
			foreach(string relative in new[] { @"OpenPmsDentalBusiness\Eclaims\TerminalConnector.cs",@"OpenPmsDentalBusiness\Eclaims\ITerminalConnector.cs",
				@"OpenPmsDentalBusiness\Eclaims\BCBSGA.cs",@"OpenPmsDental\Forms\FormTerminalConnection.cs",@"OpenPmsDental\Forms\FormTerminalConnection.designer.cs",
				@"OpenPmsDental\Forms\FormTerminalConnection.resx" })
			{
				Assert.IsFalse(File.Exists(Path.Combine(root,relative)),"Still in the repository: "+relative);
			}
			Assert.IsFalse(TestPaths.ReadSource("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj").Contains("TerminalConnector"));
			Assert.IsFalse(TestPaths.ReadSource("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj").Contains(@"Eclaims\BCBSGA.cs"));
			Assert.IsFalse(TestPaths.ReadSource("OpenPmsDental","OpenPmsDental.csproj").Contains("FormTerminalConnection"));
			Assert.IsNull(typeof(Clearinghouse).Assembly.GetType("OpenPmsDentalBusiness.Eclaims.TerminalConnector"));
			Assert.IsNull(typeof(Clearinghouse).Assembly.GetType("OpenPmsDentalBusiness.Eclaims.ITerminalConnector"));
			Assert.IsNull(typeof(Clearinghouse).Assembly.GetType("OpenPmsDentalBusiness.Eclaims.BCBSGA"));
		}

		[TestMethod]
		public void SendBatch_TakesNoTerminal_AndRefusesBcbsgaFirst() {
			Type typeEclaims=typeof(OpenPmsDentalBusiness.Eclaims.Eclaims);
			System.Reflection.MethodInfo methodInfo=typeEclaims.GetMethod("SendBatch");
			Assert.IsNotNull(methodInfo);
			Assert.IsFalse(methodInfo.GetParameters().Any(x => x.ParameterType.Name.Contains("Terminal")),"SendBatch no longer takes a terminal.");
			string source=TestPaths.ReadSource("OpenPmsDentalBusiness","Eclaims","Eclaims.cs");
			int indexSendBatch=source.IndexOf("public static void SendBatch(");
			int indexRefuse=source.IndexOf("if(clearinghouseClin.CommBridge==EclaimsCommBridge.BCBSGA) {",indexSendBatch);
			int indexBatchNumber=source.IndexOf("Clearinghouses.GetNextBatchNumber(clearinghouseClin)",indexSendBatch);
			Assert.IsTrue(indexSendBatch>0 && indexRefuse>indexSendBatch && indexRefuse<indexBatchNumber,
				"A BCBSGA clearinghouse is refused before a batch number is used or a claim file is written; its claims stay unsent.");
			StringAssert.Contains(source,"MessageBox.Show(Lans.g(\"Eclaims\",\"The claims were not sent.\")+\"\\r\\n\"+Lans.g(\"Eclaims\",BCBSGA_NOT_AVAILABLE_MESSAGE));");
			StringAssert.Contains(OpenPmsDentalBusiness.Eclaims.Eclaims.BCBSGA_NOT_AVAILABLE_MESSAGE,"is not available in this version of Open PMS - Dental.");
			foreach(string relative in new[] { @"OpenPmsDental\Forms\FormClaimEdit.cs",@"OpenPmsDental\Logic\ClaimL.cs" }) {
				string caller=TestPaths.ReadSource(relative);
				StringAssert.Contains(caller,"FormClaimPrint.FillRenaissance);//this also calls SetClaimSentOrPrinted",relative);
				Assert.IsFalse(caller.Contains("FormTerminalConnection"),relative);
			}
		}

		[TestMethod]
		public void ReportRetrieval_DoesNotDial() {
			string source=TestPaths.ReadSource("OpenPmsDentalBusiness","Data Interface","Clearinghouses.cs");
			Assert.IsFalse(source.Contains("BCBSGA.Retrieve") || source.Contains("new TerminalConnector()"));
			//Get Reports says the dial-up is not available; the automatic retrieval stays quiet.
			int indexBranch=source.IndexOf("else if(clearinghouseClin.CommBridge==EclaimsCommBridge.BCBSGA) {");
			Assert.IsTrue(indexBranch>0);
			string branch=source.Substring(indexBranch,source.IndexOf("else if(",indexBranch+10)-indexBranch);
			StringAssert.Contains(branch,"if(isAutomaticMode) {");
			StringAssert.Contains(branch,"Eclaims.Eclaims.BCBSGA_NOT_AVAILABLE_MESSAGE");
		}

		[TestMethod]
		public void ClearinghouseWindow_SaysTheDialupIsNotAvailable() {
			string source=TestPaths.ReadSource("OpenPmsDental","Forms","FormClearinghouseEdit.cs");
			int indexHandler=source.IndexOf("private void comboCommBridge_SelectionChangeCommitted(");
			int indexMessage=source.IndexOf("OpenPmsDentalBusiness.Eclaims.Eclaims.BCBSGA_NOT_AVAILABLE_MESSAGE",indexHandler);
			Assert.IsTrue(indexHandler>0 && indexMessage>indexHandler && indexMessage<source.IndexOf("FillListBoxEraBehavior();",indexHandler));
		}
	}
}
