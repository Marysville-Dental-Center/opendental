// Vendor follow-up from public documentation (next/vendorpub-1): the Glidewell setup window's instructions for Glidewell's own ways to send a case
// (its public "Send a Case" and "Submit a Digital Case" pages; Glidewell's Medit partner ID from Medit's public article). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace P262GlidewellTests {
	[TestClass]
	public class SendWithoutThisProgramTests {
		private static string Read(string relativePath) {
			string path=Path.GetDirectoryName(typeof(SendWithoutThisProgramTests).Assembly.Location);
			while(path!=null && !(File.Exists(Path.Combine(path,"LICENSE")) && File.Exists(Path.Combine(path,"NOTICE")))) {
				path=Path.GetDirectoryName(path);
			}
			Assert.IsNotNull(path,"Repository root not found.");
			return File.ReadAllText(Path.Combine(path,relativePath)).Replace("\r\n","\n");
		}

		[TestMethod]
		public void Text_HasGlidewellsPublicSteps() {
			Assert.AreEqual("318",GlidewellProgram.ITeroLabId);
			Assert.AreEqual("CDOP@glidewelldental.com",GlidewellProgram.ScannerPartnershipAddress);
			Assert.AreEqual("7307395",GlidewellProgram.MeditPartnerId);
			Assert.AreEqual("GL-421",GlidewellProgram.PaperRxForm);
			string text=GlidewellProgram.GetSendWithoutThisProgramText();
			string[] arrayLines=text.Split(new[] { "\r\n" },StringSplitOptions.None);
			Assert.AreEqual(6,arrayLines.Length,text);
			StringAssert.Contains(arrayLines[0],"without this program sending anything");
			//iTero: Align adds Glidewell (Lab ID 318), then the partnership, then Glidewell in the Rx.
			Assert.AreEqual("- iTero scanner: ask Align to add Glidewell to the scanner (Glidewell's Lab ID is 318), then request a partnership with "
				+"CDOP@glidewelldental.com, then pick Glidewell as the lab in the iTero Rx.",arrayLines[1]);
			//Medit Link: Partner, the address, a partnership, Glidewell in the Rx; and the program link's Partner ID.
			StringAssert.StartsWith(arrayLines[2],"- Medit scanner: in Medit Link, open Partner, search for CDOP@glidewelldental.com and request a partnership, "
				+"then pick Glidewell in the Rx.");
			StringAssert.Contains(arrayLines[2],"Glidewell's Medit partner ID 7307395 in the Partner ID property of the Medit Link program link");
			Assert.AreEqual("- Other scanners: upload the STL or PLY files in Glidewell's My Account.",arrayLines[3]);
			Assert.AreEqual("- Impressions or models: send a physical case with Glidewell's GL-421 Rx form.",arrayLines[4]);
			StringAssert.Contains(arrayLines[5],"Send a Case");
			Assert.IsFalse(Regex.IsMatch(text,@"(?i)https?://|www\."),"No web address: the text names the page by its title.");
			Assert.IsFalse(Regex.IsMatch(text,@"(?i)\b(sign up|recommended|partner program|buy now|free trial)\b"),"Describes, never promotes.");
			Assert.IsTrue(text.All(x => x<128));
		}

		[TestMethod]
		public void SetupWindow_ShowsTheText_ReadOnly() {
			string form=Read(@"OpenPmsDental\Forms\FormGlidewellSetup.cs");
			StringAssert.Contains(form,"textSendWithout.Text=GlidewellProgram.GetSendWithoutThisProgramText();");
			string designer=Read(@"OpenPmsDental\Forms\FormGlidewellSetup.Designer.cs");
			StringAssert.Contains(designer,"this.textSendWithout.ReadOnly = true;");
			StringAssert.Contains(designer,"this.textSendWithout.Multiline = true;");
			StringAssert.Contains(designer,"this.Controls.Add(this.textSendWithout);");
			StringAssert.Contains(designer,"this.labelSendWithout.Text = \"Sending a case to Glidewell today\";");
			//The box sits between the transport line and Save, inside the window.
			Match matchSave=Regex.Match(designer,@"this\.butSave\.Location = new System\.Drawing\.Point\(\d+, (\d+)\);");
			Match matchSize=Regex.Match(designer,@"this\.ClientSize = new System\.Drawing\.Size\(\d+, (\d+)\);");
			Assert.IsTrue(matchSave.Success && matchSize.Success);
			Assert.IsTrue(442+130<int.Parse(matchSave.Groups[1].Value),"Save is below the box.");
			Assert.IsTrue(int.Parse(matchSave.Groups[1].Value)+24<=int.Parse(matchSize.Groups[1].Value),"Save is inside the window.");
		}
	}
}
