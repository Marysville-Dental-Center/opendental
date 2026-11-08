// Tests for the vendor bridge actions that Open Dental ran through its closed VBbridges.dll (package BIN-A, docs/port-notes/BIN-A.md): Dr. Ceph,
// the eClinicalWorks Rx window and Patterson Imaging. No vendor publishes the interface, so each says it is not available in this version.
// Schick CDR (both versions) says the same since stage 0 of the .NET 10 move removed it with Schick's COM interop files (S0-2a).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BINATests {
	[TestClass]
	public class BridgeNotAvailableTests {
		[TestMethod]
		public void Messages_SayNotAvailableInThisVersion() {
			foreach(string message in new[] { BridgeNotAvailable.DrCeph,BridgeNotAvailable.EcwRx,BridgeNotAvailable.Patterson,BridgeNotAvailable.Schick }) {
				StringAssert.Contains(message,"is not available in this version of Open PMS - Dental.");
				StringAssert.DoesNotMatch(message,new Regex("VBbridges|Open Dental Software"),"The message is for the office, not about the removed file.");
			}
			StringAssert.Contains(BridgeNotAvailable.Schick,"Schick CDR");
			Assert.IsFalse(BridgeNotAvailable.Schick.Contains("version 5"),"No Schick version works any more, so none is suggested.");
		}

		[TestMethod]
		public void DrCephLaunch_ThrowsNotAvailable() {
			try {
				DrCephUtils.Launch(new DrCephArgs { ID="1",FName="Test",LName="Patient" });
				Assert.Fail("Expected BridgeNotAvailableException.");
			}
			catch(BridgeNotAvailableException ex) {
				Assert.AreEqual(BridgeNotAvailable.DrCeph,ex.Message);
			}
		}

		[TestMethod]
		public void EachFormerCallSite_ShowsItsMessage() {
			string controlChart=RepoFiles.Read(@"OpenPmsDental\Main Modules\ControlChart.cs");
			string formRxManage=RepoFiles.Read(@"OpenPmsDental\Forms\FormRxManage.cs");
			StringAssert.Contains(controlChart,"MsgBox.Show(this,BridgeNotAvailable.EcwRx);");
			StringAssert.Contains(formRxManage,"MsgBox.Show(this,BridgeNotAvailable.EcwRx);");
			StringAssert.Contains(RepoFiles.Read(@"WpfControlsOD\Bridges\Patterson.cs"),"MsgBox.Show(\"Patterson\",BridgeNotAvailable.Patterson);");
			StringAssert.Contains(RepoFiles.Read(@"WpfControlsOD\Bridges\DrCeph.cs"),"MsgBox.Show(\"DrCeph\",BridgeNotAvailable.DrCeph);");
			//An enabled Schick program link says Schick CDR is not available; the bridge and its COM interop files are gone (S0-2a).
			string programL=RepoFiles.Read(@"WpfControlsOD\Logic\ProgramL.cs");
			int indexSchick=programL.IndexOf("else if(program.ProgName==ProgramName.Schick.ToString()) {");
			int indexMessage=programL.IndexOf("MsgBox.Show(\"Schick\",BridgeNotAvailable.Schick);",indexSchick);
			int indexNext=programL.IndexOf("else if(program.ProgName==",indexSchick+1);
			Assert.IsTrue(indexSchick>0 && indexMessage>indexSchick && indexMessage<indexNext,"The Schick branch of ProgramL shows the message.");
			StringAssert.DoesNotMatch(programL,new Regex(@"Schick\.SendData|DISABLE_WINDOWS_BRIDGES[^\r\n]*\r?\n[^\r\n]*Schick"));
			Assert.IsFalse(File.Exists(Path.Combine(RepoFiles.Root(),@"WpfControlsOD\Bridges\Schick.cs")),"The Schick bridge is gone.");
			foreach(string fileName in new[] { "Interop.CDRDATALib.dll","Interop.CDRDicom.dll" }) {
				Assert.IsFalse(File.Exists(Path.Combine(RepoFiles.Root(),"Required dlls",fileName)),fileName);
				foreach(string project in new[] { @"OpenPmsDental\OpenPmsDental.csproj",@"WpfControlsOD\WpfControlsOD.csproj" }) {
					Assert.IsFalse(RepoFiles.Read(project).Contains(Path.GetFileNameWithoutExtension(fileName)),project+" still names "+fileName);
				}
			}
		}
	}
}
