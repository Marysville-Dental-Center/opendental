// B-PRINT: automatic copy of saved sheets to Imaging (Open Dental 25.1). The rules of SheetAutoSaveImages that need no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPRINTTests {
	[TestClass]
	public class SheetAutoSaveTests {
		private static Sheet NewSheet() {
			Sheet sheet=new Sheet {
				SheetNum=10,PatNum=5,SheetDefNum=3,SheetType=SheetTypeEnum.PatientForm,Description="Registration",InternalNote="note\r\nline 2",
				DateTimeSheet=new DateTime(2026,10,1,9,30,15),ShowInTerminal=0,Parameters=new List<SheetParameter>(),
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
			Assert.IsTrue(SheetAutoSaveImages.IsCopyDue(patNum:5,hasAutosaveCategory:true,isLayoutOptionOn:true,hasChanged:true));
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(0,true,true,true),"No patient.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,false,true,true),"No image category flagged Autosave Forms.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,true,false,true),"The layout does not ask for copies.");
			Assert.IsFalse(SheetAutoSaveImages.IsCopyDue(5,true,true,false),"Nothing changed.");
		}

		///<summary>The sheet as the kiosk loads it: the office sent a blank form to the kiosk (To Kiosk, which makes no copy).</summary>
		private static Sheet NewKioskSheet() {
			Sheet sheet=NewSheet();
			sheet.ShowInTerminal=1;
			sheet.SheetFields[0].FieldValue="";
			sheet.SheetFields[1].FieldValue="";
			sheet.SheetFields[2].FieldValue="";
			return sheet;
		}

		///<summary>What the patient does at the kiosk: fills in the form and signs it.</summary>
		private static void FillInAtKiosk(Sheet sheet) {
			sheet.SheetFields[0].FieldValue="Synthetic";
			sheet.SheetFields[1].FieldValue="X";
			sheet.SheetFields[2].FieldValue="0abcdef";
		}

		///<summary>The same sheet as another window loads it from the database later. The kiosk's Done button only sets ShowInTerminal to 0.</summary>
		private static Sheet Reload(Sheet sheet,byte showInTerminal) {
			Sheet sheetReloaded=sheet.Copy();
			sheetReloaded.IsNew=false;
			sheetReloaded.ShowInTerminal=showInTerminal;
			sheetReloaded.SheetFields.Reverse();
			return sheetReloaded;
		}

		[TestMethod]
		public void FilledInATerminal_ThenOkOutsideWithoutChanges_CopiesOnce() {
			int countCopies=0;
			//At the kiosk: the window shows the blank form, the patient fills it in, signs and clicks OK.
			Sheet sheetKiosk=NewKioskSheet();
			SheetAutoSaveTracker sheetAutoSaveTrackerKiosk=new SheetAutoSaveTracker();
			sheetAutoSaveTrackerKiosk.TakeSnapshot(sheetKiosk);
			FillInAtKiosk(sheetKiosk);
			if(sheetAutoSaveTrackerKiosk.IsCopyDue(sheetKiosk,hasAutosaveCategory:true,isLayoutOptionOn:true)) {
				countCopies++;
			}
			Assert.AreEqual(1,countCopies,"The patient's save at the kiosk makes the copy.");
			//The patient opens the finished form again from the kiosk list and clicks OK without changing it.
			Sheet sheetKioskAgain=Reload(sheetKiosk,showInTerminal:1);
			SheetAutoSaveTracker sheetAutoSaveTrackerKioskAgain=new SheetAutoSaveTracker();
			sheetAutoSaveTrackerKioskAgain.TakeSnapshot(sheetKioskAgain);
			if(sheetAutoSaveTrackerKioskAgain.IsCopyDue(sheetKioskAgain,true,true)) {
				countCopies++;
			}
			//In the office, after the patient pressed Done: staff open the completed form and click OK without editing it.
			Sheet sheetOffice=Reload(sheetKiosk,showInTerminal:0);
			SheetAutoSaveTracker sheetAutoSaveTrackerOffice=new SheetAutoSaveTracker();
			sheetAutoSaveTrackerOffice.TakeSnapshot(sheetOffice);
			if(sheetAutoSaveTrackerOffice.IsCopyDue(sheetOffice,true,true)) {
				countCopies++;
			}
			//And once more, later.
			Sheet sheetOfficeLater=Reload(sheetOffice,showInTerminal:0);
			SheetAutoSaveTracker sheetAutoSaveTrackerOfficeLater=new SheetAutoSaveTracker();
			sheetAutoSaveTrackerOfficeLater.TakeSnapshot(sheetOfficeLater);
			if(sheetAutoSaveTrackerOfficeLater.IsCopyDue(sheetOfficeLater,true,true)) {
				countCopies++;
			}
			Assert.AreEqual(1,countCopies,"The form reaches Imaging exactly once.");
			//Staff correct the form: that save copies again.
			Sheet sheetOfficeEdit=Reload(sheetOffice,showInTerminal:0);
			SheetAutoSaveTracker sheetAutoSaveTrackerOfficeEdit=new SheetAutoSaveTracker();
			sheetAutoSaveTrackerOfficeEdit.TakeSnapshot(sheetOfficeEdit);
			sheetOfficeEdit.SheetFields.Find(x => x.FieldName=="FName").FieldValue="Synthetic Corrected";
			Assert.IsTrue(sheetAutoSaveTrackerOfficeEdit.IsCopyDue(sheetOfficeEdit,true,true));
		}

		[TestMethod]
		public void FilledInATerminal_WithoutAnyEntry_MakesNoCopy() {
			Sheet sheetKiosk=NewKioskSheet();
			SheetAutoSaveTracker sheetAutoSaveTracker=new SheetAutoSaveTracker();
			sheetAutoSaveTracker.TakeSnapshot(sheetKiosk);
			Assert.IsFalse(sheetAutoSaveTracker.IsCopyDue(sheetKiosk,true,true),"OK at the kiosk without entering anything writes nothing.");
		}

		[TestMethod]
		public void Tracker_CopiesAgainInTheSameWindowOnlyAfterAnotherChange() {
			Sheet sheet=NewSheet();
			sheet.IsNew=true;
			SheetAutoSaveTracker sheetAutoSaveTracker=new SheetAutoSaveTracker();
			sheetAutoSaveTracker.TakeSnapshot(sheet);
			sheet.IsNew=false;//the save inserted it
			Assert.IsTrue(sheetAutoSaveTracker.IsCopyDue(sheet,true,true),"The first save of a new sheet.");
			Assert.IsFalse(sheetAutoSaveTracker.IsCopyDue(sheet,true,true),"A second save in the same window, nothing changed since the copy.");
			sheet.Description="Registration 2";
			Assert.IsFalse(sheetAutoSaveTracker.IsCopyDue(sheet,true,false),"The layout does not ask for copies.");
			Assert.IsTrue(sheetAutoSaveTracker.IsCopyDue(sheet,true,true));
			Assert.IsFalse(sheetAutoSaveTracker.IsCopyDue(null,true,true));
			Sheet sheetNoPatient=NewSheet();
			sheetNoPatient.PatNum=0;
			SheetAutoSaveTracker sheetAutoSaveTrackerNoPatient=new SheetAutoSaveTracker();
			sheetAutoSaveTrackerNoPatient.TakeSnapshot(sheetNoPatient);
			sheetNoPatient.Description="Changed";
			Assert.IsFalse(sheetAutoSaveTrackerNoPatient.IsCopyDue(sheetNoPatient,true,true),"No patient.");
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
