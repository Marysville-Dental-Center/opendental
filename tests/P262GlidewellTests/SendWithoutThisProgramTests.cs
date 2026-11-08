// Vendor follow-up from public documentation (next/vendorpub-1): the Glidewell setup window's instructions for Glidewell's own ways to send a case
// (its public "Send a Case" and "Submit a Digital Case" pages; Glidewell's Medit partner ID from Medit's public article). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
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

		///<summary>Review follow-up (next/vendorfix-1): the steps in Glidewell's own words from its Submit a Digital Case page (read on 2026-11-07),
		///3Shape TRIOS added, the scanners Glidewell's page has its own steps for named, STL upload through My Account with the number to set it up,
		///the GL-421 line kept, and the vendors in alphabetical order within each list.</summary>
		[TestMethod]
		public void Text_HasGlidewellsPublicSteps() {
			Assert.AreEqual("318",GlidewellProgram.ITeroLabId);
			Assert.AreEqual("CDOP@glidewelldental.com",GlidewellProgram.ScannerPartnershipAddress);
			Assert.AreEqual("7307395",GlidewellProgram.MeditPartnerId);
			Assert.AreEqual("GL-421",GlidewellProgram.PaperRxForm);
			Assert.AreEqual("800-854-7256",GlidewellProgram.MyAccountSetupPhone);
			string text=GlidewellProgram.GetSendWithoutThisProgramText();
			string[] arrayLines=text.Split(new[] { "\r\n" },StringSplitOptions.None);
			Assert.AreEqual(8,arrayLines.Length,text);
			Assert.AreEqual("Glidewell's own ways to send a case work today, without this program sending anything:",arrayLines[0]);
			//3Shape TRIOS: the account, More, Settings, Connections, Labs, Add, Connect; Glidewell under Scan & Send once approved.
			Assert.AreEqual("- 3Shape TRIOS: Log in to your 3Shape account. Under More, select Settings, then Connections, Labs and Add; search for "
				+"Glidewell and click Connect to send a request. Once the connection request has been approved, Glidewell appears under the Scan & Send "
				+"menu.",arrayLines[1]);
			//iTero: the office contacts Align Technology to add Glidewell (Lab ID 318), then the partnership, then Glidewell in the digital Rx.
			Assert.AreEqual("- iTero: Contact Align Technology directly to add Glidewell to your list of 'Favorite Labs'. Provide Glidewell Lab ID as 318. "
				+"Then, under Partner, search for CDOP@glidewelldental.com and request partnership. Once partnership is approved, you can select "
				+"Glidewell when completing the digital Rx.",arrayLines[2]);
			//Medit Link: meditlink.com, Partner, the address, the partnership, Glidewell in the digital Rx; and the program link's Partner ID.
			Assert.AreEqual("- Medit Link: Log in at meditlink.com. Under Partner, search for CDOP@glidewelldental.com and request partnership. Once "
				+"partnership is approved, you can select Glidewell when completing the digital Rx. To have every case created with the Chart's Medit "
				+"Link button assigned to Glidewell, enter Glidewell's Medit partner ID 7307395 in the Partner ID property of the Medit Link program "
				+"link (Setup, Program Links).",arrayLines[3]);
			Assert.AreEqual("- Alliedstar, CEREC, DEXIS, Heron IOS and Planmeca: Glidewell's Submit a Digital Case page gives the steps for each.",
				arrayLines[4]);
			Assert.AreEqual("- Any other scanner: Glidewell accepts scans from almost any scanner. Export the STL files and upload them through the My "
				+"Account portal. An office not registered for My Account calls Glidewell at 800-854-7256 to set it up.",arrayLines[5]);
			Assert.AreEqual("- Impressions or models: send a physical case with Glidewell's GL-421 Rx form.",arrayLines[6]);
			Assert.AreEqual("Glidewell's website describes these under Send a Case and Submit a Digital Case.",arrayLines[7]);
			//Neutral: the scanners with steps here, and the scanners named in one line, each in alphabetical order.
			List<string> listScanners=arrayLines.Skip(1).Take(3).Select(x => x.Substring(2,x.IndexOf(':')-2)).ToList();
			CollectionAssert.AreEqual(new[] { "3Shape TRIOS","iTero","Medit Link" },listScanners);
			CollectionAssert.AreEqual(listScanners.OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList(),listScanners);
			List<string> listNamed=Regex.Split(arrayLines[4].Substring(2,arrayLines[4].IndexOf(':')-2),", | and ").ToList();
			CollectionAssert.AreEqual(new[] { "Alliedstar","CEREC","DEXIS","Heron IOS","Planmeca" },listNamed);
			CollectionAssert.AreEqual(listNamed.OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList(),listNamed);
			Assert.IsFalse(Regex.IsMatch(text,@"(?i)https?://|www\."),"No web address to open: the text names the pages by their titles.");
			Assert.IsFalse(Regex.IsMatch(text,@"(?i)\b(sign up|recommend\w*|best|preferred|partner program|buy now|free trial)\b"),"Describes, never promotes.");
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
