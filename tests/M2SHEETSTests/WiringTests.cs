// Checks that the windows (OpenDental.exe, which the tests cannot load) and the sheet drawing code use the M2-SHEETS helpers. They read the
// source files.
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
