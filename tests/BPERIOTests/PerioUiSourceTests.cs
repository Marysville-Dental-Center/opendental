// Source checks for the Perio Chart windows. FormPerio, Perio Setup and the Graphical Perio Chart are WinForms windows that these tests cannot
// open (the desktop client is never launched by tests), so they check the code that decides what is shown and printed instead. The grid
// itself (ContrPerio) is tested for real in ContrPerioTests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BPERIOTests {
	[TestClass]
	public class PerioUiSourceTests {
		private static string FormPerio() {
			return TestDb.ReadRepoFile("OpenDental","Forms","FormPerio.cs");
		}

		#region Attached gingiva (v24_4-chart-perio-attached-gingiva)
		[TestMethod]
		public void FormPerio_VoiceNavigation_AsksTheGridForRows() {
			//Fixed row numbers of the 24.3 layout are wrong once the auto Att Ging rows are added.
			string code=FormPerio();
			foreach(string fixedRow in new[] { "yVal=10;","yVal=32;","y=5;","y=37;","y=15;","y=26;","yVal=5;","yVal=37;","yVal=15;","yVal=26;",
				"yPos=(isFacial ? 9 : 12);","yPos=(isFacial ? 33 : 30);","yPos=(isFacial ? 7 : 14);","yPos=(isFacial ? 35 : 28);","yPos=6;",
				"yPos=(isFacial ? 36 : 27);","36+Math.Min(6","14+Math.Min(6","27-Math.Min(6","6-Math.Min(6" })
			{
				Assert.IsFalse(code.Contains(fixedRow),"FormPerio still uses the fixed row "+fixedRow);
			}
			StringAssert.Contains(code,"contrPerio.GetTableRowForSection(0,PerioSequenceType.Mobility)");
			StringAssert.Contains(code,"contrPerio.GetTableRowForSection(3,PerioSequenceType.Mobility)");
			StringAssert.Contains(code,"contrPerio.GetTableRowForSection(contrPerio.GetSection(toothNum,isFacial),PerioSequenceType.Probing)");
		}

		[TestMethod]
		public void FormPerio_PrintsTheTallerGrid() {
			string code=FormPerio();
			StringAssert.Contains(code,"LayoutManager.MoveSize(contrPerio,new Size(602, 642+3*13));");
			StringAssert.Contains(code,"yPos+=688+3*13-19;");
		}

		[TestMethod]
		public void FormPerio_ChartControlIsTallEnoughForTheNewRows() {
			string designer=TestDb.ReadRepoFile("OpenDental","Forms","FormPerio.designer.cs");
			Match match=Regex.Match(designer,@"this\.contrPerio\.Size = new System\.Drawing\.Size\((\d+), (\d+)\);");
			Assert.IsTrue(match.Success);
			Assert.IsTrue(int.Parse(match.Groups[2].Value)>=642+3*13,"The 96 dpi grid with the auto Att Ging rows is 681 pixels high.");
		}

		[TestMethod]
		public void ClaimEdit_PerioAttachmentIsTallEnoughForTheNewRows() {
			//Claim Edit > Perio (and the Canadian attach perio button) builds its own grid and draws it into the image attached to the claim. With the
			//24.3 sizes (grid bitmap 665 high) the bottom 16 pixels of the 681 pixel grid were cut off. ContrPerioTests draws the grid at these sizes.
			ClaimPerioAttachmentSizes sizes=ReadClaimPerioAttachmentSizes();
			Assert.IsTrue(sizes.SizeGrid.Height>=642+3*13,"Grid control height "+sizes.SizeGrid.Height);
			Assert.IsTrue(sizes.SizeBitmapGrid.Height>=642+3*13,"Grid bitmap height "+sizes.SizeBitmapGrid.Height);
			Assert.IsTrue(sizes.SizeBitmapImage.Height>=sizes.YGrid+sizes.SizeBitmapGrid.Height,
				"The attached image ("+sizes.SizeBitmapImage.Height+" high) holds the whole grid bitmap below the name and practice title (y="+sizes.YGrid+").");
			Assert.IsTrue(sizes.SizeBitmapImage.Width>=sizes.SizeBitmapGrid.Width);
		}

		///<summary>The 96 dpi sizes that FormClaimEdit.butAttachPerioHelper uses for the perio image attached to a claim: the grid control, the bitmap
		///the grid is drawn into, the attached image, and the y position of the grid bitmap in that image.</summary>
		internal class ClaimPerioAttachmentSizes {
			public Size SizeGrid;
			public Size SizeBitmapGrid;
			public Size SizeBitmapImage;
			public int YGrid;
		}

		///<summary>Reads the sizes from FormClaimEdit.butAttachPerioHelper. Sizes may be written as sums of products, such as 665+3*13.</summary>
		internal static ClaimPerioAttachmentSizes ReadClaimPerioAttachmentSizes() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormClaimEdit.cs");
			int indexStart=code.IndexOf("private void butAttachPerioHelper() {",StringComparison.Ordinal);
			Assert.IsTrue(indexStart>0,"FormClaimEdit.butAttachPerioHelper not found.");
			int indexEnd=code.IndexOf("private void ",indexStart+1,StringComparison.Ordinal);
			string method=code.Substring(indexStart,indexEnd-indexStart);
			const string SIZE=@"\s*([0-9+*\s]+?)\s*";
			ClaimPerioAttachmentSizes sizes=new ClaimPerioAttachmentSizes();
			sizes.SizeGrid=MatchSize(method,@"contrPerioGrid\.Size\s*=\s*new System\.Drawing\.Size\("+SIZE+","+SIZE+@"\);");
			sizes.SizeBitmapGrid=MatchSize(method,@"Bitmap bitmap\s*=\s*new Bitmap\(LayoutManager\.Scale\("+SIZE+@"\),\s*LayoutManager\.Scale\("+SIZE+@"\)\);");
			sizes.SizeBitmapImage=MatchSize(method,@"Bitmap bitmapBig\s*=\s*new Bitmap\(LayoutManager\.Scale\("+SIZE+@"\),\s*LayoutManager\.Scale\("+SIZE+@"\)\);");
			Point pointGrid=(Point)MatchSize(method,@"g\.DrawImage\(bitmap,"+SIZE+","+SIZE+@"\);");
			Assert.AreEqual(0,pointGrid.X);
			sizes.YGrid=pointGrid.Y;
			return sizes;
		}

		private static Size MatchSize(string code,string pattern) {
			Match match=Regex.Match(code,pattern);
			Assert.IsTrue(match.Success,"Not found in FormClaimEdit.butAttachPerioHelper: "+pattern);
			return new Size(EvaluateSum(match.Groups[1].Value),EvaluateSum(match.Groups[2].Value));
		}

		///<summary>Evaluates a sum of products of whole numbers, such as 665+3*13.</summary>
		private static int EvaluateSum(string expression) {
			int sum=0;
			foreach(string term in expression.Split('+')) {
				int product=1;
				foreach(string factor in term.Split('*')) {
					product*=int.Parse(factor.Trim());
				}
				sum+=product;
			}
			return sum;
		}
		#endregion Attached gingiva (v24_4-chart-perio-attached-gingiva)

		#region Recession term (v24_4-chart-perio-recession-term)
		[TestMethod]
		public void FormPerio_LabelsFollowTheTerm() {
			string code=FormPerio();
			StringAssert.Contains(code,"contrPerio.GingMarginTermChanged+=contrPerio_GingMarginTermChanged;");
			StringAssert.Contains(code,"label10.Text=Lan.g(this,gingMarginTerm);");
			StringAssert.Contains(code,"checkGingMarg.Text=Lan.g(this,gingMarginTerm+\" +\");");
			StringAssert.Contains(code,"\"Teeth with Recession greater than or equal to\"");
			StringAssert.Contains(code,"contrPerio.RefreshGingMarginLabels();");
		}

		[TestMethod]
		public void PerioSetup_LoadsAndSavesTheRecessionOption() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormPerioSetup.cs");
			string designer=TestDb.ReadRepoFile("OpenDental","Forms","FormPerioSetup.Designer.cs");
			StringAssert.Contains(code,"checkUseRecessionTerm.Checked=PerioMeasures.GetUseRecessionTerm();");
			StringAssert.Contains(code,"PerioMeasures.SetUseRecessionTerm(checkUseRecessionTerm.Checked)");
			StringAssert.Contains(code,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,\"Perio Setup: Recession term \"");
			StringAssert.Contains(code,"DataValid.SetInvalid(InvalidType.Prefs);");
			StringAssert.Contains(designer,"this.checkUseRecessionTerm = new OpenDental.UI.CheckBox();");
			StringAssert.Contains(designer,"Use the term \\\"Recession\\\" (reverts to Gingival Margin when hyperplasia is charted)");
			//Saved on both save paths: the option is saved before the "all default depths blank" early return.
			int indexSave=code.IndexOf("SaveUseRecessionTerm();");
			int indexEarlyReturn=code.IndexOf("if(textF1.Text==\"\" && textF32.Text==\"\"");
			Assert.IsTrue(indexSave>0 && indexSave<indexEarlyReturn);
		}

		[TestMethod]
		public void GraphicalSetup_GingivalMarginLabelFollowsTheOption() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormPerioGraphicalSetup.cs");
			StringAssert.Contains(code,"if(PerioMeasures.GetUseRecessionTerm()) {");
			StringAssert.Contains(code,"label4.Text=Lan.g(this,\"Recession\");");
		}
		#endregion Recession term (v24_4-chart-perio-recession-term)

		#region Implant marker (v24_4-chart-perio-implant-removed)
		[TestMethod]
		public void GraphicalPerio_RemovedImplantIsNotDrawn() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormPerioGraphical.cs");
			StringAssert.Contains(code,"procedureCode.PaintType==ToothPaintingType.Implant && !listProceduresForTooth[p].HideGraphics");
		}

		[TestMethod]
		public void ContrPerio_ImplantUsesTheSharedRule() {
			string code=TestDb.ReadRepoFile("OpenDental","User Controls","ContrPerio.cs");
			StringAssert.Contains(code,"return PerioMeasures.IsImplantTooth(intTooth,_listProcedures,codeNum => ProcedureCodes.GetProcCode(codeNum));");
			StringAssert.Contains(code,"_listProcedures=null;","The cached procedures are cleared each time a Perio Chart opens.");
		}
		#endregion Implant marker (v24_4-chart-perio-implant-removed)

		#region Birthdate on the Graphical Perio Chart (v25_2-chart-perio-print-birthdate)
		[TestMethod]
		public void GraphicalPerio_PrintsTheBirthdateBetweenNameAndExamDate() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormPerioGraphical.cs");
			int indexName=code.IndexOf("string patNameStr=_patient.GetNameFLFormal();",StringComparison.Ordinal);
			int indexBirthdate=code.IndexOf("PerioMeasures.GetBirthdateForPrint(patient.Birthdate)",StringComparison.Ordinal);
			int indexExamDate=code.IndexOf("string examDateStr=_perioExam.ExamDate.ToShortDateString();",StringComparison.Ordinal);
			Assert.IsTrue(indexName>0,"patient name line");
			Assert.IsTrue(indexBirthdate>indexName,"The birthdate line comes after the patient name.");
			Assert.IsTrue(indexExamDate>indexBirthdate,"The birthdate line comes before the exam date.");
			StringAssert.Contains(code,"if(birthdateStr!=\"\") {","The line is skipped when there is no birthdate.");
			//RenderPerioPrintout serves the printout, print preview and the saved image alike.
			Assert.AreEqual(2,Regex.Matches(code,@"RenderPerioPrintout\(g,_patient,").Count);
		}
		#endregion Birthdate on the Graphical Perio Chart (v25_2-chart-perio-print-birthdate)
	}
}
