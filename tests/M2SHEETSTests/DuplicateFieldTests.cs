// The duplicate field warning of the Sheet Def editor (Open Dental 25.4): which fields count as the same field.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SHEETSTests {
	[TestClass]
	public class DuplicateFieldTests {
		[TestInitialize]
		public void TestInitialize() {
			Samples.UseUsEnglish();
		}

		private static SheetFieldDef Def(SheetFieldType sheetFieldType,string fieldName,string language="",SheetFieldLayoutMode layoutMode=SheetFieldLayoutMode.Default,
			string reportableName="",string radioButtonValue="",string radioButtonGroup="",string fieldValue="")
		{
			return new SheetFieldDef {
				FieldType=sheetFieldType,
				FieldName=fieldName,
				FieldValue=fieldValue,
				Language=language,
				LayoutMode=layoutMode,
				ReportableName=reportableName,
				RadioButtonValue=radioButtonValue,
				RadioButtonGroup=radioButtonGroup,
			};
		}

		private static int CountDuplicates(SheetFieldDef sheetFieldDefNew,params SheetFieldDef[] arrayExisting) {
			return SheetFieldDefs.GetDuplicates(sheetFieldDefNew,new List<SheetFieldDef>(arrayExisting),sheetFieldDefNew.Language,sheetFieldDefNew.LayoutMode).Count;
		}

		[TestMethod]
		public void InputAndOutput_SameNameSameLanguageAndLayout() {
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.InputField,"FName"),Def(SheetFieldType.InputField,"FName")));
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.OutputText,"dateToday"),Def(SheetFieldType.OutputText,"dateToday")));
			Assert.AreEqual(2,CountDuplicates(Def(SheetFieldType.InputField,"FName"),Def(SheetFieldType.InputField,"FName"),Def(SheetFieldType.InputField,"fname")),
				"Case does not matter, as in the database.");
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.InputField,"FName"),Def(SheetFieldType.InputField,"LName")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.InputField,"FName"),Def(SheetFieldType.OutputText,"FName")),"Input and output are different fields.");
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.InputField,"FName",language:"spa"),Def(SheetFieldType.InputField,"FName")),"Other language.");
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.InputField,"FName",language:"rus"),Def(SheetFieldType.InputField,"FName",language:"rus")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.OutputText,"PatNum",layoutMode:SheetFieldLayoutMode.Ecw),Def(SheetFieldType.OutputText,"PatNum")),
				"Other layout mode.");
		}

		[TestMethod]
		public void MiscFields_ByUserDefinedName() {
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.InputField,"misc",reportableName:"Reason"),Def(SheetFieldType.InputField,"misc",reportableName:" reason ")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.InputField,"misc",reportableName:"Reason"),Def(SheetFieldType.InputField,"misc",reportableName:"Notes")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.InputField,"misc"),Def(SheetFieldType.InputField,"misc")),"Misc fields without a name are not checked.");
			Assert.IsNull(SheetFieldDefs.GetDuplicateKey(Def(SheetFieldType.InputField,"misc")));
		}

		[TestMethod]
		public void CheckBoxes_NameAndRadioValue() {
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.CheckBox,"allergy:Penicillin",radioButtonValue:"Y"),
				Def(SheetFieldType.CheckBox,"allergy:Penicillin",radioButtonValue:"Y")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.CheckBox,"allergy:Penicillin",radioButtonValue:"Y"),
				Def(SheetFieldType.CheckBox,"allergy:Penicillin",radioButtonValue:"N")),"Yes and No boxes of one question.");
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.CheckBox,"Gender",radioButtonValue:"Male"),Def(SheetFieldType.CheckBox,"Gender",radioButtonValue:"Male")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.CheckBox,"Gender",radioButtonValue:"Male"),Def(SheetFieldType.InputField,"Gender")));
		}

		[TestMethod]
		public void MiscCheckBoxes_ByGroupAndUserDefinedName() {
			Assert.AreEqual(1,CountDuplicates(Def(SheetFieldType.CheckBox,"misc",reportableName:"Smoker",radioButtonGroup:"Habits"),
				Def(SheetFieldType.CheckBox,"misc",reportableName:"Smoker",radioButtonGroup:"Habits")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.CheckBox,"misc",reportableName:"Smoker",radioButtonGroup:"Habits"),
				Def(SheetFieldType.CheckBox,"misc",reportableName:"Smoker",radioButtonGroup:"Other")));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.CheckBox,"misc",radioButtonGroup:"Habits"),Def(SheetFieldType.CheckBox,"misc",radioButtonGroup:"Habits")),
				"Unnamed misc checkboxes of one group are the group's choices.");
		}

		[TestMethod]
		public void OtherTypes_NeverChecked() {
			foreach(SheetFieldType sheetFieldType in new[] { SheetFieldType.StaticText,SheetFieldType.Line,SheetFieldType.Rectangle,SheetFieldType.Image,
				SheetFieldType.PatImage,SheetFieldType.SigBox,SheetFieldType.SigBoxPractice,SheetFieldType.Grid,SheetFieldType.ComboBox,SheetFieldType.Special,
				SheetFieldType.ScreenChart,SheetFieldType.MobileHeader })
			{
				Assert.AreEqual(0,CountDuplicates(Def(sheetFieldType,"same",fieldValue:"Same"),Def(sheetFieldType,"same",fieldValue:"Same")),sheetFieldType.ToString());
				Assert.IsNull(SheetFieldDefs.GetDuplicateKey(Def(sheetFieldType,"same")),sheetFieldType.ToString());
			}
		}

		[TestMethod]
		public void Duplicates_NeverItselfAndNoNames() {
			SheetFieldDef sheetFieldDef=Def(SheetFieldType.InputField,"FName");
			Assert.AreEqual(0,CountDuplicates(sheetFieldDef,sheetFieldDef));
			Assert.AreEqual(0,CountDuplicates(Def(SheetFieldType.InputField,""),Def(SheetFieldType.InputField,"")));
			Assert.AreEqual(0,SheetFieldDefs.GetDuplicates(sheetFieldDef,null,"",SheetFieldLayoutMode.Default).Count);
		}

		[TestMethod]
		public void DisplayNames() {
			Assert.AreEqual("FName",SheetFieldDefs.GetDuplicateDisplayName(Def(SheetFieldType.InputField,"FName")));
			Assert.AreEqual("misc (Reason)",SheetFieldDefs.GetDuplicateDisplayName(Def(SheetFieldType.InputField,"misc",reportableName:"Reason")));
			Assert.AreEqual("allergy:Penicillin (Y)",SheetFieldDefs.GetDuplicateDisplayName(Def(SheetFieldType.CheckBox,"allergy:Penicillin",radioButtonValue:"Y")));
			Assert.AreEqual("misc (Smoker, group Habits)",
				SheetFieldDefs.GetDuplicateDisplayName(Def(SheetFieldType.CheckBox,"misc",reportableName:"Smoker",radioButtonGroup:"Habits")));
		}
	}
}
