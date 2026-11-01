// Tests for the vendor bridge actions that Open Dental ran through its closed VBbridges.dll (package BIN-A, docs/port-notes/BIN-A.md): Dr. Ceph,
// the eClinicalWorks Rx window, Patterson Imaging and Schick CDR version 4. No vendor publishes the interface, so each says it is not available
// in this version, and the parts that never needed the helper (Schick CDR DICOM version 5) still work.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BINATests {
	[TestClass]
	public class BridgeNotAvailableTests {
		[TestMethod]
		public void Messages_SayNotAvailableInThisVersion() {
			foreach(string message in new[] { BridgeNotAvailable.DrCeph,BridgeNotAvailable.EcwRx,BridgeNotAvailable.Patterson,BridgeNotAvailable.SchickVersion4 }) {
				StringAssert.Contains(message,"is not available in this version of Open PMS - Dental.");
				StringAssert.DoesNotMatch(message,new Regex("VBbridges|Open Dental Software"),"The message is for the office, not about the removed file.");
			}
			StringAssert.Contains(BridgeNotAvailable.SchickVersion4,"version 5","Schick users are pointed at the version that still works.");
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
			string controlChart=RepoFiles.Read(@"OpenDental\Main Modules\ControlChart.cs");
			string formRxManage=RepoFiles.Read(@"OpenDental\Forms\FormRxManage.cs");
			StringAssert.Contains(controlChart,"MsgBox.Show(this,BridgeNotAvailable.EcwRx);");
			StringAssert.Contains(formRxManage,"MsgBox.Show(this,BridgeNotAvailable.EcwRx);");
			StringAssert.Contains(RepoFiles.Read(@"WpfControlsOD\Bridges\Patterson.cs"),"MsgBox.Show(\"Patterson\",BridgeNotAvailable.Patterson);");
			StringAssert.Contains(RepoFiles.Read(@"WpfControlsOD\Bridges\DrCeph.cs"),"MsgBox.Show(\"DrCeph\",BridgeNotAvailable.DrCeph);");
			string schick=RepoFiles.Read(@"WpfControlsOD\Bridges\Schick.cs");
			StringAssert.Contains(schick,"MsgBox.Show(\"Schick\",BridgeNotAvailable.SchickVersion4);");
			StringAssert.Contains(schick,"ShowExam(pat,patID);","Schick CDR DICOM (version 5) still uses Schick's own automation interface.");
		}
	}
}
