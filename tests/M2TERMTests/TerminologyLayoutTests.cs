// The longer 25.4 wording still fits the controls that show it, at 96 dpi with the default font (Microsoft Sans Serif 8.25). The layout manager
// scales sizes and fonts together, so a text that fits at 96 dpi fits at every zoom.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2TERMTests {
	[TestClass]
	public class TerminologyLayoutTests {
		///<summary>OpenDental.UI.CheckBox draws its text in Width-sqrSize-4 pixels, sqrSize being 12 at 96 dpi (CheckBox.OnPaint).</summary>
		private const int CHECKBOX_BOX_AND_GAP=12+4;

		private static Font NewDefaultFont() {
			return new Font("Microsoft Sans Serif",8.25f);
		}

		[TestMethod]
		public void PreferenceCheckBoxes_TextFits() {
			List<(string Designer,string ControlName)> listCheckBoxes=new List<(string,string)> {
				(TermSources.FAMILY_INSURANCE_DESIGNER,"checkInsPlanExclusionsUseUCR"),
				(TermSources.FAMILY_INSURANCE_DESIGNER,"checkEnableZeroWriteoffOnAnnualMax"),
				(TermSources.FAMILY_INSURANCE_DESIGNER,"checkEnableZeroWriteoffOnLimitations"),
				(TermSources.FAMILY_INSURANCE_DESIGNER,"checkInsDefaultShowUCRonClaims"),
				(TermSources.ACCOUNT_CLAIM_SEND_DESIGNER,"checkPpoUseUcr"),
			};
			using Bitmap bitmap=new Bitmap(1,1);
			using Graphics g=Graphics.FromImage(bitmap);
			using Font font=NewDefaultFont();
			Assert.AreEqual(96f,g.DpiX,"Measured at 96 dpi.");
			foreach((string designer,string controlName) in listCheckBoxes) {
				DesignerControl control=DesignerControl.Read(TestPaths.ReadRepoFile(designer),controlName);
				//CheckBox.OnPaint draws with Graphics.DrawString on one line (the control is 17 pixels high).
				float widthText=g.MeasureString(control.Text,font).Width;
				int widthAvailable=control.Width-CHECKBOX_BOX_AND_GAP;
				Assert.IsTrue(widthText<=widthAvailable,controlName+": \""+control.Text+"\" needs "+widthText+" pixels, the control has "+widthAvailable+".");
			}
		}

		[TestMethod]
		public void BlueBookPercentLabel_TextFits_AndDoesNotOverlap() {
			string designer=TestPaths.ReadRepoFile(TermSources.BLUE_BOOK_RULES_DESIGNER);
			DesignerControl label=DesignerControl.Read(designer,"labelUcrFeePercent");
			using Font font=NewDefaultFont();
			//A WinForms Label draws with TextRenderer (GDI).
			int widthText=TextRenderer.MeasureText(label.Text,font).Width;
			Assert.IsTrue(widthText<=label.Width,"\""+label.Text+"\" needs "+widthText+" pixels, the label has "+label.Width+".");
			//Right-aligned next to the percent box, as before; its left edge stays clear of the Allowed Fee Method list.
			System.Text.RegularExpressions.Match matchText=System.Text.RegularExpressions.Regex.Match(designer,
				@"this\.textUcrFeePercent\.Location = new System\.Drawing\.Point\((\d+),");
			Assert.IsTrue(matchText.Success);
			Assert.AreEqual(int.Parse(matchText.Groups[1].Value)-1,label.X+label.Width,"The label ends one pixel before the percent box.");
			System.Text.RegularExpressions.Match matchListLocation=System.Text.RegularExpressions.Regex.Match(designer,
				@"this\.listAllowedFeeMethod\.Location = new System\.Drawing\.Point\((\d+),");
			System.Text.RegularExpressions.Match matchListSize=System.Text.RegularExpressions.Regex.Match(designer,
				@"this\.listAllowedFeeMethod\.Size = new System\.Drawing\.Size\((\d+),");
			Assert.IsTrue(matchListLocation.Success && matchListSize.Success);
			int rightOfList=int.Parse(matchListLocation.Groups[1].Value)+int.Parse(matchListSize.Groups[1].Value);
			Assert.IsTrue(label.X>rightOfList,"The label starts at "+label.X+", inside the list that ends at "+rightOfList+".");
		}
	}
}
