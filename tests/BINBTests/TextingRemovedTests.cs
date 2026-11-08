// Stage 0 of the .NET 10 move (S0-2a, docs/specs/net10-plan.md, decision 11): CallFire text messages are cut. With CallFire gone no texting
// integration remains, so the Text button, the Appointments texting menu items, the text actions of Edit Appointment and the Confirmation List,
// and FormTxtMsgEdit go too. ProgramName.CallFire and EnumPermType.TextMessageSend stay, as do the program link and permission rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BINBTests {
	[TestClass]
	public class TextingRemovedTests {
		[TestMethod]
		public void CallFireWebReferenceAndWindow_AreGone() {
			string root=TestPaths.RepoRoot;
			Assert.IsFalse(Directory.Exists(Path.Combine(root,"OpenPmsDental","Web References","CallFireService")),"The CallFire SOAP proxy is gone.");
			foreach(string fileName in new[] { "FormTxtMsgEdit.cs","FormTxtMsgEdit.designer.cs","FormTxtMsgEdit.resx" }) {
				Assert.IsFalse(File.Exists(Path.Combine(root,"OpenPmsDental","Forms",fileName)),fileName);
			}
			string csproj=TestPaths.ReadSource("OpenPmsDental","OpenPmsDental.csproj");
			foreach(string removed in new[] { "CallFire","FormTxtMsgEdit","<WebReferenceUrl","<WebReferences " }) {
				Assert.IsFalse(csproj.Contains(removed),"OpenPmsDental.csproj still has "+removed);
			}
			//The setting of the proxy goes; Change Healthcare ITS keeps its setting for S3a-6.
			foreach(string relative in new[] { @"OpenPmsDental\app.config",@"OpenPmsDental\Properties\Settings.settings",@"OpenPmsDental\Properties\Settings.Designer.cs" }) {
				string text=TestPaths.ReadSource(relative);
				Assert.IsFalse(text.Contains("CallFire"),relative);
				StringAssert.Contains(text,"OpenDental_EmdeonITS_ITSWS",relative);
			}
			Assert.IsFalse(TestPaths.ReadSource("OpenPmsDental","Forms","FormEtrans835s.Designer.cs").Contains("smsService1"),"FormEtrans835s held a CallFire proxy.");
		}

		[TestMethod]
		public void TextingUi_IsGone() {
			Assert.IsNull(typeof(GlobalFormOpenDental).GetField("SendTextMessage"),"No window sends a text any more.");
			Assert.IsNull(typeof(GlobalFormOpenDental).GetNestedType("SendTextDelegate"));
			string formOpenDental=TestPaths.ReadSource("OpenPmsDental","Main Modules","FormOpenDental.cs");
			foreach(string removed in new[] { "toolButTxtMsg_Click","IsTextingAvailable","#region SMS Text Messaging","Lan.g(this,\"Send Text Message\")",
				"case \"Text\":" })
			{
				Assert.IsFalse(formOpenDental.Contains(removed),"FormOpenDental still has "+removed);
			}
			string controlAppt=TestPaths.ReadSource("OpenPmsDental","Main Modules","ControlAppt.cs");
			foreach(string removed in new[] { "MenuItemNames.SendText","MenuItemNames.SendConfirmationText","MenuItemNames.TextDiv","\"Send Confirmation Text\"",
				"GlobalFormOpenDental.SendTextMessage" })
			{
				Assert.IsFalse(controlAppt.Contains(removed),"ControlAppt still has "+removed);
			}
			foreach(string form in new[] { "FormApptEdit","FormConfirmList" }) {
				string source=TestPaths.ReadSource("OpenPmsDental","Forms",form+".cs");
				string designer=TestPaths.ReadSource("OpenPmsDental","Forms",form+".designer.cs");
				Assert.IsFalse(source.Contains("butText") || designer.Contains("butText"),form+" still has its Text button.");
				Assert.IsFalse(source.Contains("FormTxtMsgEdit") || source.Contains("ProgramName.CallFire"),form);
			}
		}

		[TestMethod]
		public void EnumMembers_Stay() {
			//program.ProgName rows name ProgramName members; grouppermission rows store EnumPermType numbers.
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"CallFire"));
			Assert.IsTrue(Enum.IsDefined(typeof(EnumPermType),"TextMessageSend"));
		}
	}
}
