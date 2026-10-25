// B-PRINT: automatic copy of saved sheets to Imaging (Open Dental 25.1). The rules of SheetAutoSaveImages that need no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BPRINTTests {
	[TestClass]
	public class SheetAutoSaveTests {
		private static Sheet NewSheet() {
			Sheet sheet=new Sheet {
				SheetNum=10,PatNum=5,SheetDefNum=3,SheetType=SheetTypeEnum.PatientForm,Description="Registration",InternalNote="note\r\nline 2",
				DateTimeSheet=new DateTime(2026,10,1,9,30,15),ShowInTerminal=0,
			};
			sheet.SheetFields=new List<SheetField> {
				NewField(SheetFieldType.InputField,"FName","Synthetic",100,100),
				NewField(SheetFieldType.CheckBox,"misc","X",100,140),
				NewField(SheetFieldType.SigBox,"","0abcdef",100,200),
			};
			return sheet;
		}

		private static SheetField NewField(SheetFieldType sheetFieldType,string fieldName,string fieldValue,int x,int y) {
			return new SheetField { FieldType=sheetFieldType,FieldName=fieldName,FieldValue=fieldValue,XPos=x,YPos=y,Width=100,Height=20 };
		}

		[TestMethod]
		public void CopyDue_OnlyWhenEveryConditionHolds() {
			Assert.IsTrue(SheetAutoSaveImages.IsCopyDue(patNum:5,hasAutosaveCategory:true,isLayoutOptionOn:true,isInTerminal:false,hasChanged:true));
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(0,true,true,false,true),"No patient.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,false,true,false,true),"No image category flagged Autosave Forms.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,true,false,false,true),"The layout does not ask for copies.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,true,true,true,true),"Kiosk or terminal.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,true,true,false,false),"Nothing changed.");
		}

		[TestMethod]
		public void AutosaveCategory_MustBeVisibleAndFlaggedU() {
			Assert.IsTrue(SheetAutoSaveImages.HasAutosaveCategory(new List<Def> { new Def { ItemValue="X" },new Def { ItemValue="PU" } }));
			Assert.IsFalse(SheetAutoSaveImages.HasAutosaveCategory(new List<Def> { new Def { ItemValue="U",IsHidden=true } }));
			Assert.IsFalse(SheetAutoSaveImages.HasAutosaveCategory(new List<Def> { new Def { ItemValue="XS" },new Def { ItemValue=null } }));
			Assert.IsFalse(SheetAutoSaveImages.HasAutosaveCategory(null));
		}

		[TestMethod]
		public void OpenAndCloseWithoutChanges_IsNoChange() {
			Sheet sheet=NewSheet();
			string fingerprint=SheetAutoSaveImages.GetFingerprint(sheet);
			Sheet sheetReloaded=NewSheet();
			sheetReloaded.SheetFields.Reverse();//the window reloads the fields from the database after a save, maybe in another order
			sheetReloaded.InternalNote="note\nline 2";//the text box and the database store line breaks differently
			sheetReloaded.DateTimeSheet=new DateTime(2026,10,1,9,30,0);//the window saves the time without seconds
			sheetReloaded.DateTSheetEdited=DateTime.Now;//every save sets it
			Assert.IsFalse(SheetAutoSaveImages.HasChanged(fingerprint,false,sheetReloaded));
		}

		[TestMethod]
		public void EveryUserChange_IsAChange() {
			string fingerprint=SheetAutoSaveImages.GetFingerprint(NewSheet());
			List<Action<Sheet>> listChanges=new List<Action<Sheet>> {
				x => x.SheetFields[0].FieldValue="Changed",
				x => x.SheetFields[1].FieldValue="",
				x => x.SheetFields[2].FieldValue="1fedcba",//a new signature
				x => x.SheetFields[0].YPos+=10,
				x => x.SheetFields.Add(NewField(SheetFieldType.PatImage,"5","",300,300)),
				x => x.SheetFields.RemoveAt(1),
				x => x.Description="Registration 2",
				x => x.InternalNote="other",
				x => x.DateTimeSheet=x.DateTimeSheet.AddMinutes(1),
				x => x.ShowInTerminal=1,
				x => x.PatNum=6,
			};
			for(int i=0;i<listChanges.Count;i++) {
				Sheet sheet=NewSheet();
				listChanges[i](sheet);
				Assert.IsTrue(SheetAutoSaveImages.HasChanged(fingerprint,false,sheet),"Change "+i+".");
			}
		}

		[TestMethod]
		public void NewSheet_OrSignatureChange_AlwaysCounts() {
			Sheet sheet=NewSheet();
			string fingerprint=SheetAutoSaveImages.GetFingerprint(sheet);
			Assert.IsTrue(SheetAutoSaveImages.HasChanged(fingerprint,true,sheet),"The first save of a new sheet.");
			Assert.IsTrue(SheetAutoSaveImages.HasChanged(fingerprint,false,sheet,isSignatureChanged:true));
			Assert.AreEqual("",SheetAutoSaveImages.GetFingerprint(null));
		}

		[TestMethod]
		public void LayoutOption_IsOffForASheetWithoutLayout() {
			Assert.IsFalse(SheetAutoSaveImages.IsLayoutOptionOn(null));
			Assert.IsFalse(SheetAutoSaveImages.IsLayoutOptionOn(new Sheet { SheetDefNum=0 }));
		}
	}
}
