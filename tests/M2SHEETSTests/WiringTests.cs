// Checks that the windows and the sheet drawing code use the M2-SHEETS helpers. They read the source files: the windows draw only when shown,
// and OpenDental.exe is never launched (SheetDefEditKeyTests drives the Sheet Def editor's key handling in memory).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2SHEETSTests {
	[TestClass]
	public class WiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		private static int Count(string source,string text) {
			return Regex.Matches(source,Regex.Escape(text)).Count;
		}

		[TestMethod]
		public void SheetDefEdit_WarnsBeforeAddingADuplicateField() {
			string source=Read(@"OpenDental\Forms\FormSheetDefEdit.cs");
			foreach(string form in new[] { "formSheetFieldOutput","formSheetFieldInput","formSheetFieldCheckBox" }) {
				//The check comes right before the field is added: only a return in between.
				string pattern=Regex.Escape("if(!ConfirmDuplicateFields(new List<SheetFieldDef> { "+form+".SheetFieldDefCur },out _)) {")+@"\s+return;\s+}\s+"
					+Regex.Escape("AddNewSheetFieldDef("+form+".SheetFieldDefCur);");
				Assert.IsTrue(Regex.IsMatch(source,pattern),form);
			}
			StringAssert.Contains(source,"if(!ConfirmDuplicateFields(_listSheetFieldDefsCopyPaste,out bool wasWarned)) {");
			StringAssert.Contains(source,"SheetFieldDefs.GetDuplicates(listSheetFieldDefsToAdd[i],SheetDef_.SheetFieldDefs,language,_sheetFieldLayoutMode)");
			StringAssert.Contains(source,"MsgBox.Show(MsgBoxButtons.OKCancel,msg)");
			StringAssert.Contains(source,"_isDuplicatePasteAccepted=isConflictingfield;");
		}

		///<summary>The body of the method whose declaration contains the given text, from its opening brace to the matching closing brace. Braces in
		///comments, strings and chars are skipped.</summary>
		private static string MethodBody(string source,string declaration) {
			int start=source.IndexOf(declaration);
			Assert.IsTrue(start>=0,declaration);
			Assert.AreEqual(-1,source.IndexOf(declaration,start+1),"Declared once: "+declaration);
			int open=source.IndexOf('{',start+declaration.Length);
			int depth=0;
			for(int i=open;i<source.Length;i++) {
				char c=source[i];
				if(c=='/' && i+1<source.Length && source[i+1]=='/') {
					i=source.IndexOf('\n',i);
					continue;
				}
				if(c=='/' && i+1<source.Length && source[i+1]=='*') {
					i=source.IndexOf("*/",i+2)+1;
					continue;
				}
				if(c=='"' || c=='\'') {
					for(i++;source[i]!=c;i++) {
						if(source[i]=='\\') {
							i++;
						}
					}
					continue;
				}
				if(c=='{') {
					depth++;
				}
				else if(c=='}') {
					depth--;
					if(depth==0) {
						return source.Substring(open,i-open+1);
					}
				}
			}
			Assert.Fail("No closing brace: "+declaration);
			return null;
		}

		///<summary>The duplicate field box is modal, so it eats the key_up of the CTRL (CTRL+V) or ALT (ALT+click stamp) that started the paste.
		///FormSheetDefEdit_KeyUp then never clears _isCtrlDown or _isAltDown: clicks keep toggling the selection, V/Z/C alone paste, undo or copy,
		///or every click stamps another copy (or asks again after Cancel). The box must reset the keys after OK and after Cancel, as the copy prompt in
		///CopyControlsToMemory does, and the paste must decide the cascade offset from the ALT state saved before the box.</summary>
		[TestMethod]
		public void SheetDefEdit_DuplicateFieldBoxResetsTheModifierKeys() {
			string source=Read(@"OpenDental\Forms\FormSheetDefEdit.cs");
			//The paste is reached with CTRL or ALT down.
			string keyDown=MethodBody(source,"private void FormSheetDefEdit_KeyDown(object sender,KeyEventArgs e)");
			Assert.IsTrue(Regex.IsMatch(keyDown,Regex.Escape("if(_isCtrlDown && e.KeyCode==Keys.V) { //CTRL-V")+@"\s+"
				+Regex.Escape("PasteControlsFromMemory(new Point(0,0));")));
			string mouseDown=MethodBody(source,"private void panelMain_MouseDown(object sender,MouseEventArgs e)");
			Assert.IsTrue(Regex.IsMatch(mouseDown,Regex.Escape("if(_isAltDown) {")+@"\s+"+Regex.Escape("PasteControlsFromMemory(pointDoc);")));
			//The 24.3 copy prompt already resets CTRL after its box, on both paths.
			string copy=MethodBody(source,"private void CopyControlsToMemory()");
			Assert.AreEqual(2,Count(copy,"_isCtrlDown=false;"));
			//The duplicate field box: once the box has been shown, nothing returns before CTRL, ALT and the cursor are reset, so OK and Cancel both reset.
			string confirm=MethodBody(source,"private bool ConfirmDuplicateFields(List<SheetFieldDef> listSheetFieldDefsToAdd,out bool wasWarned)");
			int box=confirm.IndexOf("_funcAskDuplicateFields(msg)");
			Assert.IsTrue(box>confirm.IndexOf("wasWarned=true;"),"The box is shown only after wasWarned is set.");
			Assert.AreEqual(1,Count(confirm,"_funcAskDuplicateFields("),"One box.");
			//The question is the real modal box; only SheetDefEditKeyTests replaces it.
			StringAssert.Contains(source,"private Func<string,bool> _funcAskDuplicateFields=(msg) => MsgBox.Show(MsgBoxButtons.OKCancel,msg);");
			Assert.AreEqual(2,Count(source,"_funcAskDuplicateFields"),"Declared and called, never reassigned by the window.");
			string afterBox=confirm.Substring(box);
			int firstReturn=afterBox.IndexOf("return ");
			Assert.IsTrue(firstReturn>0);
			Assert.AreEqual(1,Count(afterBox,"return "),"A single return after the box.");
			foreach(string reset in new[] { "_isCtrlDown=false;","_isAltDown=false;","Cursor=Cursors.Default;" }) {
				int index=afterBox.IndexOf(reset);
				Assert.IsTrue(index>0 && index<firstReturn,reset);
			}
			StringAssert.Contains(afterBox,"return isAddAccepted;");
			Assert.IsTrue(confirm.IndexOf("_isCtrlDown")>box && confirm.IndexOf("_isAltDown")>box,"The keys are reset only when the box was shown.");
			//The paste saves the ALT state before the box and uses the saved state for the cascade offset, so an ALT+click stamp still lands where clicked
			//without moving the offset of the next CTRL+V.
			string paste=MethodBody(source,"private void PasteControlsFromMemory(Point pointOrigin)");
			int saved=paste.IndexOf("bool isAltStamp=_isAltDown;");
			Assert.IsTrue(saved>0);
			Assert.IsTrue(saved<paste.IndexOf("ConfirmDuplicateFields(_listSheetFieldDefsCopyPaste,out bool wasWarned)"),"Saved before the box.");
			Assert.AreEqual(1,Count(paste,"_isAltDown"),"Only the saved state is read after the box.");
			Assert.IsTrue(Regex.IsMatch(paste,Regex.Escape("if(!isAltStamp) {")+@"\s+"+Regex.Escape("_pasteOffsetY+=((_pasteOffset+10)/100)*10;")));
		}

		[TestMethod]
		public void SheetDefEdit_NonLatinLanguagesAndText() {
			string source=Read(@"OpenDental\Forms\FormSheetDefEdit.cs");
			Assert.AreEqual(3,Count(source,"SheetScripts.GetLanguageDisplayName("),"Every language name in the list.");
			Assert.AreEqual(0,Count(source,"GetCultureFromThreeLetter("));
			StringAssert.Contains(source,"using Font font=new Font(SheetFonts.GetFontNameForText(sheetFieldDef.FontName,");
			StringAssert.Contains(source,"SheetTextDrawing.ApplyDirection(stringFormat,str);");
		}

		[TestMethod]
		public void SheetFillEdit_FallbackFontAndDirectionOnScreen() {
			string source=Read(@"OpenDental\Forms\FormSheetFillEdit.cs");
			StringAssert.Contains(source,"using Font font=new Font(SheetFonts.GetFontNameForText(SheetCur.SheetFields[i].FontName,SheetCur.SheetFields[i].FieldValue),");
			StringAssert.Contains(source,"SheetTextDrawing.ApplyDirection(stringFormat,SheetCur.SheetFields[i].FieldValue);");
			StringAssert.Contains(source,"textBox.Font=new Font(SheetFonts.GetFontNameForText(sheetField.FontName,text),fontSize,style);");
			StringAssert.Contains(source,"Font font=new Font(SheetFonts.GetFontNameForText(sheetField.FontName,text),sheetField.FontSize,fontStyle);");
			StringAssert.Contains(source,"SheetTextDrawing.ApplyDirection(stringFormat,text);");
			StringAssert.Contains(source,"SheetFonts.GetFontNameForText(FontFamily.GenericSansSerif.Name,SheetFields.GetComboSelectedOption(SheetCur.SheetFields[i]))");
		}

		[TestMethod]
		public void SheetDrawingJob_AllTextGoesThroughTheHelper() {
			string source=Read(@"OpenDentBusiness\SheetFramework\SheetDrawingJob.cs");
			Assert.AreEqual(0,Count(source,"GraphicsHelper.DrawStringX("),"PDF text goes through SheetTextDrawing.DrawStringX.");
			Assert.AreEqual(0,Count(source,"GraphicsHelper.DrawString("),"Printed text goes through SheetTextDrawing.DrawString.");
			Assert.AreEqual(12,Count(source,"SheetTextDrawing.DrawStringX(gx,"));
			Assert.AreEqual(4,Count(source,"SheetTextDrawing.DrawString(g,"));
			StringAssert.Contains(source,"SheetTextDrawing.DrawStringX(gx,field.FieldValue,xfont,xbrushText,rect,field.TextAlign,field.FontName);");
			StringAssert.Contains(source,"new Font(SheetFonts.GetFontNameForText(\"Arial\",patName),10,FontStyle.Bold)");
		}
	}
}
