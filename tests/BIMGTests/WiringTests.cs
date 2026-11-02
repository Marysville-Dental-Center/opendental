// The windows cannot be opened in a test run (the GUI is not launched), so these tests read the window sources and check that each one is wired to
// the shared rules, which the other files test. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository
// root.
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BIMGTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		private static int Count(string source,string text) {
			return Regex.Matches(source,Regex.Escape(text)).Count;
		}

		///<summary>The text of one method of a class: from its signature to its closing brace, which is the first brace at the method's own indent
		///(two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		///<summary>Every DrawDrawings call in the text, and whether the line before it is "if(includeDrawings){".</summary>
		private static List<bool> GetDrawDrawingsGuarded(string body) {
			List<bool> listGuarded=new List<bool>();
			string[] arrayLines=body.Replace("\r\n","\n").Split('\n');
			for(int i=0;i<arrayLines.Length;i++) {
				if(!arrayLines[i].Contains("DrawDrawings(")) {
					continue;
				}
				listGuarded.Add(i>0 && arrayLines[i-1].Trim()=="if(includeDrawings){");
			}
			return listGuarded;
		}

		#region Copy without drawings
		[TestMethod]
		public void ImagingToolbar_CopyIsASplitButtonWithBothCommands() {
			string source=Read("OpenPmsDental","Main Modules","ControlImages.cs");
			string layout=MethodBody(source,"public void LayoutToolBars() {");
			AssertBefore(layout,"contextMenuCopy.Add(new WpfControls.UI.MenuItem(Lan.g(this,\"Copy\"),ToolBarCopy_Click));",
				"contextMenuCopy.Add(new WpfControls.UI.MenuItem(Lan.g(this,\"Copy without drawings\"),ToolBarCopyWithoutDrawings_Click));");
			StringAssert.Contains(layout,"toolBarMain.Add(Lan.g(this,\"Copy\"),ToolBarCopy_Click,WpfControls.UI.EnumIcons.Copy,"
				+"WpfControls.UI.ToolBarButtonStyle.DropDownButton,Lan.g(this,\"Copy displayed image to clipboard\"),contextMenuCopy,TB.Copy.ToString());",
				"Clicking the button still copies with drawings; the arrow opens the two choices. The tag keeps the enable/disable logic.");
			Assert.AreEqual(1,Count(layout,"TB.Copy.ToString()"));
			StringAssert.Contains(MethodBody(source,"private void ToolBarCopyWithoutDrawings_Click(object sender,EventArgs e){"),
				"GetOrMakeControlImageDisplay().ToolBarCopyWithoutDrawings_Click(sender,e);");
			StringAssert.Contains(MethodBody(source,"private void ToolBarCopy_Click(object sender, EventArgs e){"),
				"GetOrMakeControlImageDisplay().ToolBarCopy_Click(sender,e);");
		}

		[TestMethod]
		public void ImageDisplay_BothCommandsShareOneCopyThatSkipsDrawings() {
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			StringAssert.Contains(MethodBody(source,"public void ToolBarCopy_Click(object sender, EventArgs e){"),"CopyToClipboard(includeDrawings:true);");
			StringAssert.Contains(MethodBody(source,"public void ToolBarCopyWithoutDrawings_Click(object sender,EventArgs e){"),
				"CopyToClipboard(includeDrawings:false);");
			string copy=MethodBody(source,"private void CopyToClipboard(bool includeDrawings){");
			//Mount item: drawn without calling DrawDrawings.
			CollectionAssert.AreEqual(new[] { true },GetDrawDrawingsGuarded(copy).ToArray(),"The mount item's DrawDrawings is skipped without drawings.");
			AssertBefore(copy,"DrawMountOne(g,_idxSelectedInMount,translateToItemPos:false);","DrawDrawings(0,g);");
			//Whole mount and single document: drawn with includeDrawings passed on.
			Assert.AreEqual(1,Count(copy,"DrawMount(g,includeDrawings);"));
			Assert.AreEqual(0,Count(copy,"DrawMount(g);"));
			Assert.AreEqual(1,Count(copy,"DrawDocument(g,includeDrawings);"));
			Assert.AreEqual(0,Count(copy,"DrawDocument(g);"));
			//A single document is copied from its stored file only when plain Copy would do so; otherwise both commands draw it into a bitmap (the
			//drawn branch, which Copy without drawings also takes for a document whose only change is its drawings).
			StringAssert.Contains(copy,"if(!ImageCopyRules.MustDrawForCopy(GetDocumentShowing(0),includeDrawings)) {");
			Assert.AreEqual(0,Count(copy,"ImageHelper.HasImageBeenEdited("),"Plain Copy reaches HasImageBeenEdited through the rules.");
			AssertBefore(copy,"if(!ImageCopyRules.MustDrawForCopy(GetDocumentShowing(0),includeDrawings)) {",
				"bitmapCopy=ImageHelper.CopyWithCropRotate(GetDocumentShowing(0),GetBitmapShowing(0));");
			AssertBefore(copy,"bitmapCopy=ImageHelper.CopyWithCropRotate(GetDocumentShowing(0),GetBitmapShowing(0));","DrawDocument(g,includeDrawings);");
			//PDFs and other files: the stored file, as plain Copy.
			Assert.AreEqual(2,Count(copy,"//other files such as pdf will lack a bitmapCopy"));
			//The audit trail tells the two apart.
			Assert.AreEqual(3,Count(copy,",includeDrawings));"));
			Assert.AreEqual(3,Count(copy,"SecurityLogs.MakeLogEntry(EnumPermType.Copy,patNum,ImageCopyRules.GetLogText("));
		}

		[TestMethod]
		public void ImageDisplay_DrawMountAndDrawDocumentKeepDrawingsByDefault() {
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			string drawMount=MethodBody(source,"private void DrawMount(Graphics g,bool includeDrawings=true){");
			CollectionAssert.AreEqual(new[] { true },GetDrawDrawingsGuarded(drawMount).ToArray());
			string drawDocument=MethodBody(source,"private void DrawDocument(Graphics g,bool includeDrawings=true){");
			CollectionAssert.AreEqual(new[] { true },GetDrawDrawingsGuarded(drawDocument).ToArray());
			//The screen, printing and export still call them without the flag, so they keep the drawings.
			Assert.IsTrue(Count(source,"DrawMount(g);")>=1);
			Assert.IsTrue(Count(source,"DrawDocument(g);")>=1);
		}

		[TestMethod]
		public void ImageRightClickMenu_HasBothCommandsInOrder() {
			string designer=Read("OpenPmsDental","User Controls","ControlImageDisplay.Designer.cs");
			Match match=Regex.Match(designer,@"this\.menuPanelMain\.MenuItems\.AddRange\(new System\.Windows\.Forms\.MenuItem\[\] \{(?<items>[^}]*)\}\);");
			Assert.IsTrue(match.Success);
			string[] arrayItems=match.Groups["items"].Value.Split(',').Select(x => x.Trim().Replace("this.","")).ToArray();
			CollectionAssert.AreEqual(new[] { "menuItemCopy","menuItemCopyWithoutDrawings","menuItemPaste","menuItemFlipHoriz","menuItemRotateLeft",
				"menuItemRotateRight","menuItemRotate180","menuItemDelete","menuItemInfo","menuItemTasks" },arrayItems);
			for(int i=0;i<arrayItems.Length;i++) {
				StringAssert.Contains(designer,"this."+arrayItems[i]+".Index = "+i+";","The designer index matches the order.");
				StringAssert.Contains(designer,"this."+arrayItems[i]+".Click += new System.EventHandler(this.menuPanelMain_Click);");
			}
			StringAssert.Contains(designer,"this.menuItemCopyWithoutDrawings.Text = \"Copy without drawings\";");
			//The click handler goes by menu item, so the inserted item cannot shift the others.
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			string click=MethodBody(source,"private void menuPanelMain_Click(object sender,System.EventArgs e) {");
			Assert.AreEqual(0,Count(click,".Index"),"No dispatch by position.");
			Assert.IsTrue(Regex.IsMatch(click,@"if\(menuItem==menuItemCopy\) \{\s*ToolBarCopy_Click\(this,new EventArgs\(\)\);"));
			Assert.IsTrue(Regex.IsMatch(click,@"else if\(menuItem==menuItemCopyWithoutDrawings\) \{\s*ToolBarCopyWithoutDrawings_Click\(this,new EventArgs\(\)\);"));
			Dictionary<string,string> dictHandlers=new Dictionary<string,string> {
				{ "menuItemPaste","ToolBarPaste_Click(this,new EventArgs());" },
				{ "menuItemFlipHoriz","ToolBarFlip_Click();" },
				{ "menuItemRotateLeft","ToolBarRotateL_Click();" },
				{ "menuItemRotateRight","ToolBarRotateR_Click();" },
				{ "menuItemRotate180","ToolBarRotate180_Click();" },
				{ "menuItemDelete","ToolBarDelete_Click(this,new EventArgs());" },
				{ "menuItemInfo","ToolBarInfo_Click(this,new EventArgs());" },
				{ "menuItemTasks","ToolBarTasks_Click(this,new EventArgs());" },
			};
			foreach(KeyValuePair<string,string> pair in dictHandlers) {
				Assert.IsTrue(Regex.IsMatch(click,@"else if\(menuItem=="+pair.Key+@"\) \{\s*"+Regex.Escape(pair.Value)),pair.Key+" keeps its command.");
			}
		}

		///<summary>Pasting inside this program goes by the copied document or mount (NodeTypeAndKey) and builds new documents from the stored files, so
		///it never brings drawings along, whichever copy command put them on the clipboard.</summary>
		[TestMethod]
		public void PasteInsideTheProgram_NeverCopiesDrawings() {
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			string paste=MethodBody(source,"public void ToolBarPasteTypeAndKey(NodeTypeAndKey nodeTypeAndKey,bool showDocInfo){");
			Assert.AreEqual(0,Count(paste,"ImageDraw"),"No drawings are read or written while pasting.");
		}
		#endregion Copy without drawings

		#region Font sizes
		[TestMethod]
		public void EditTextWindow_CalculateButtonsRemoved() {
			string xaml=Read("WpfControlsOD","Frms","FrmImageDrawEdit.xaml");
			Assert.AreEqual(0,Count(xaml,"butCalculateApparent"));
			Assert.AreEqual(0,Count(xaml,"butCalculateActual"));
			Assert.AreEqual(0,Count(xaml,"Calculate"));
			StringAssert.Contains(xaml,"<ui:TextVDouble x:Name=\"textFontSize\"");
			StringAssert.Contains(xaml,"<ui:TextVDouble x:Name=\"textFontApparent\"");
			string code=Read("WpfControlsOD","Frms","FrmImageDrawEdit.xaml.cs");
			Assert.AreEqual(0,Count(code,"butCalculate"));
			Assert.AreEqual(0,Count(code,"LostFocus"),"No more recalculating on focus loss only.");
		}

		[TestMethod]
		public void EditTextWindow_EveryTextChangeGoesThroughTheSync() {
			string code=Read("WpfControlsOD","Frms","FrmImageDrawEdit.xaml.cs");
			StringAssert.Contains(code,"textFontSize.TextChanged+=textFontSize_TextChanged;");
			StringAssert.Contains(code,"textFontApparent.TextChanged+=textFontApparent_TextChanged;");
			StringAssert.Contains(MethodBody(code,"private void textFontSize_TextChanged(object sender,EventArgs e) {"),
				"_imageDrawFontSizeSync?.OnActualTextChanged(textFontSize.Text);");
			StringAssert.Contains(MethodBody(code,"private void textFontApparent_TextChanged(object sender,EventArgs e) {"),
				"_imageDrawFontSizeSync?.OnApparentTextChanged(textFontApparent.Text);");
			string load=MethodBody(code,"private void FrmImageDrawEdit_Load(object sender, EventArgs e){");
			AssertBefore(load,"_imageDrawFontSizeSync=new ImageDrawFontSizeSync(ZoomVal,ScaleF(1),","textFontSize.Text=ImageDrawCur.GetFontSize().ToString(\"#.##\");");
			StringAssert.Contains(load,"actionSetActualText:text => textFontSize.Text=text,");
			StringAssert.Contains(load,"actionSetApparentText:text => textFontApparent.Text=text);");
			//The limits of the sync are the limits of the boxes.
			string xaml=Read("WpfControlsOD","Frms","FrmImageDrawEdit.xaml");
			Assert.AreEqual(2,Count(xaml,"MinVal=\"1\" MaxVal=\"100000000\""));
			Assert.AreEqual(1d,OpenPmsDentalBusiness.ImageDrawFontSizeSync.MIN_SIZE);
			Assert.AreEqual(100000000d,OpenPmsDentalBusiness.ImageDrawFontSizeSync.MAX_SIZE);
			//Saving still validates and stores the actual size.
			StringAssert.Contains(code,"ImageDrawCur.FontSize=(float)textFontSize.Value;");
		}
		#endregion Font sizes

		#region Scanning Setup details
		///<summary>Location and size of each control in a WinForms designer file, by field name.</summary>
		private static Dictionary<string,Rectangle> GetBounds(string designer) {
			Dictionary<string,Point> dictLocations=new Dictionary<string,Point>();
			foreach(Match match in Regex.Matches(designer,@"this\.(?<name>\w+)\.Location = new System\.Drawing\.Point\((?<x>-?\d+), (?<y>-?\d+)\);")) {
				dictLocations[match.Groups["name"].Value]=new Point(int.Parse(match.Groups["x"].Value),int.Parse(match.Groups["y"].Value));
			}
			Dictionary<string,Rectangle> dictBounds=new Dictionary<string,Rectangle>();
			foreach(Match match in Regex.Matches(designer,@"this\.(?<name>\w+)\.Size = new System\.Drawing\.Size\((?<w>\d+), (?<h>\d+)\);")) {
				string name=match.Groups["name"].Value;
				if(dictLocations.ContainsKey(name)) {
					dictBounds[name]=new Rectangle(dictLocations[name],new Size(int.Parse(match.Groups["w"].Value),int.Parse(match.Groups["h"].Value)));
				}
			}
			return dictBounds;
		}

		[TestMethod]
		public void ScanningSetup_DetailsButtonBesideShowSelectScannerWindow() {
			string designer=Read("OpenPmsDental","Forms","FormImagingSetup.designer.cs");
			StringAssert.Contains(designer,"this.butScanDocSelectSourceDetails = new OpenPmsDental.UI.Button();");
			StringAssert.Contains(designer,"this.groupBox1.Controls.Add(this.butScanDocSelectSourceDetails);");
			StringAssert.Contains(designer,"this.butScanDocSelectSourceDetails.Text = \"Details\";");
			StringAssert.Contains(designer,"this.butScanDocSelectSourceDetails.Click += new System.EventHandler(this.butScanDocSelectSourceDetails_Click);");
			Dictionary<string,Rectangle> dictBounds=GetBounds(designer);
			Rectangle rectangleButton=dictBounds["butScanDocSelectSourceDetails"];
			Rectangle rectangleCheck=dictBounds["checkScanDocSelectSource"];
			Assert.IsTrue(rectangleButton.Left>=rectangleCheck.Right && rectangleButton.Left-rectangleCheck.Right<=10,"Right next to the check box.");
			Assert.IsTrue(rectangleButton.Top<=rectangleCheck.Top+rectangleCheck.Height/2 && rectangleButton.Bottom>=rectangleCheck.Top+rectangleCheck.Height/2,
				"On the check box's row.");
			Rectangle rectangleGroup=dictBounds["groupBox1"];
			Assert.IsTrue(rectangleButton.Right<=rectangleGroup.Width && rectangleButton.Bottom<=rectangleGroup.Height,"Inside its group box.");
			//No overlap with the other controls of the group box.
			foreach(Match match in Regex.Matches(designer,@"this\.groupBox1\.Controls\.Add\(this\.(?<name>\w+)\);")) {
				string name=match.Groups["name"].Value;
				if(name=="butScanDocSelectSourceDetails") {
					continue;
				}
				Assert.IsFalse(rectangleButton.IntersectsWith(dictBounds[name]),"Overlaps "+name);
			}
		}

		[TestMethod]
		public void ScanningSetup_DetailsExplainsTheOptionAndTouchesNoScanner() {
			string source=Read("OpenPmsDental","Forms","FormImagingSetup.cs");
			string details=MethodBody(source,"private void butScanDocSelectSourceDetails_Click(object sender,EventArgs e) {");
			StringAssert.Contains(details,"MsgBox.Show(this,");
			foreach(string text in new[] { "Canon","PDFs","Imaging module","list boxes","Commlog","before each scan" }) {
				StringAssert.Contains(details,text);
			}
			foreach(string forbidden in new[] { "Twain","TWAIN","EZTwain","ImagingDeviceManager","ComputerPrefs","Prefs.","Update(" }) {
				Assert.AreEqual(0,Count(details,forbidden),"The Details button only shows text: "+forbidden);
			}
			//The only TWAIN calls of the window are still the two of Set Default Scanner.
			Assert.AreEqual(2,Count(source,"ImagingDeviceManager."));
			StringAssert.Contains(source,"this.butScanDocSelectSourceDetails","The button text is translated with the window.");
		}
		#endregion Scanning Setup details
	}
}
