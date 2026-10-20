// 24.4 v24_4-account-payplan-sheet-select: the sheet used to print each payment plan, and the default sheet of a payment plan template.
// Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace PayPlanTests {
	[TestClass]
	public class PayPlanSheetTests {
		[TestInitialize]
		public void TestInitialize() {
			PayPlanDb.Begin();
			ClearPaymentPlanSheetDefs();
			Security.CurUser=Security.CurUser??new Userod();
		}

		///<summary>Each test starts with no custom PaymentPlan sheets, so the default is the internal sheet.</summary>
		private static void ClearPaymentPlanSheetDefs() {
			DataCore.NonQ("DELETE sheetfielddef FROM sheetfielddef INNER JOIN sheetdef ON sheetdef.SheetDefNum=sheetfielddef.SheetDefNum "
				+"WHERE sheetdef.SheetType="+POut.Int((int)SheetTypeEnum.PaymentPlan));
			DataCore.NonQ("DELETE FROM sheetdef WHERE SheetType="+POut.Int((int)SheetTypeEnum.PaymentPlan));
			SheetDefs.RefreshCache();
			SheetFieldDefs.RefreshCache();
		}

		private static PayPlan InsertPayPlan(long patNum,long sheetDefNum) {
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patNum,500,DateTime.Today);
			payPlan.SheetDefNum=sheetDefNum;
			PayPlans.Update(payPlan);
			return payPlan;
		}

		[TestMethod]
		public void GetForPayPlan_ZeroWithoutCustomSheets_IsTheInternalSheet() {
			SheetDef sheetDef=SheetDefs.GetForPayPlan(0);
			Assert.AreEqual(SheetTypeEnum.PaymentPlan,sheetDef.SheetType);
			Assert.AreEqual(0,sheetDef.SheetDefNum,"The internal sheet is not in the database.");
			Assert.IsTrue(sheetDef.SheetFieldDefs.Count>0);
		}

		[TestMethod]
		public void GetForPayPlan_ZeroWithCustomSheets_IsTheFirstCustomSheetByDescription() {
			SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"B plan sheet");
			SheetDef sheetDefA=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"A plan sheet");
			Assert.AreEqual(sheetDefA.SheetDefNum,SheetDefs.GetForPayPlan(0).SheetDefNum,"24.3 logic: first custom sheet, alphabetical.");
		}

		[TestMethod]
		public void GetForPayPlan_ChosenSheet_IsUsedEvenWhenItIsNotTheFirst() {
			SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"A plan sheet");
			SheetDef sheetDefZ=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"Z plan sheet");
			SheetDef sheetDef=SheetDefs.GetForPayPlan(sheetDefZ.SheetDefNum);
			Assert.AreEqual(sheetDefZ.SheetDefNum,sheetDef.SheetDefNum);
			Assert.IsNotNull(sheetDef.SheetFieldDefs,"Fields are loaded for printing.");
		}

		[TestMethod]
		public void GetForPayPlan_SheetOfAnotherType_FallsBackToTheDefault() {
			SheetDef sheetDefStatement=SheetDefT.CreateSheetDef(SheetTypeEnum.Statement,"A statement");
			Assert.IsFalse(SheetDefs.IsValidPayPlanSheetDefNum(sheetDefStatement.SheetDefNum));
			SheetDef sheetDef=SheetDefs.GetForPayPlan(sheetDefStatement.SheetDefNum);
			Assert.AreEqual(SheetTypeEnum.PaymentPlan,sheetDef.SheetType);
			Assert.AreEqual(0,sheetDef.SheetDefNum);
		}

		[TestMethod]
		public void GetForPayPlan_MissingSheet_FallsBackToTheDefault() {
			SheetDef sheetDefA=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"A plan sheet");
			Assert.AreEqual(sheetDefA.SheetDefNum,SheetDefs.GetForPayPlan(987654321).SheetDefNum);
			Assert.IsFalse(SheetDefs.IsValidPayPlanSheetDefNum(987654321));
			Assert.IsTrue(SheetDefs.IsValidPayPlanSheetDefNum(0),"0 always means the default sheet.");
		}

		[TestMethod]
		public void GetPayPlanSheetDefChoices_OnlyPaymentPlanSheetsInDescriptionOrder() {
			SheetDef sheetDefB=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"B plan sheet");
			SheetDef sheetDefA=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"A plan sheet");
			SheetDefT.CreateSheetDef(SheetTypeEnum.Statement,"A statement");
			List<long> listSheetDefNums=SheetDefs.GetPayPlanSheetDefChoices().Select(x => x.SheetDefNum).ToList();
			CollectionAssert.AreEqual(new List<long> { sheetDefA.SheetDefNum,sheetDefB.SheetDefNum },listSheetDefNums);
		}

		[TestMethod]
		public void PayPlan_SheetDefNum_SavesAndLoads() {
			Patient patient=PayPlanDb.CreatePatient("SheetSave");
			SheetDef sheetDef=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"Saved plan sheet");
			PayPlan payPlan=InsertPayPlan(patient.PatNum,sheetDef.SheetDefNum);
			Assert.AreEqual(sheetDef.SheetDefNum,PayPlans.GetOne(payPlan.PayPlanNum).SheetDefNum);
		}

		[TestMethod]
		public void DeleteSheetDef_ResetsPlansAndTemplatesThatChoseIt() {
			Patient patient=PayPlanDb.CreatePatient("SheetDelete");
			SheetDef sheetDefDeleted=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"Deleted plan sheet");
			SheetDef sheetDefKept=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"Kept plan sheet");
			PayPlan payPlanDeleted=InsertPayPlan(patient.PatNum,sheetDefDeleted.SheetDefNum);
			PayPlan payPlanKept=InsertPayPlan(patient.PatNum,sheetDefKept.SheetDefNum);
			PayPlanTemplate payPlanTemplate=new PayPlanTemplate { PayPlanTemplateName="Deleted sheet template",Note="",SheetDefNum=sheetDefDeleted.SheetDefNum };
			PayPlanTemplates.Insert(payPlanTemplate);
			string hashBefore=PayPlans.GetOne(payPlanDeleted.PayPlanNum).SecurityHash;
			SheetDefs.DeleteObject(sheetDefDeleted.SheetDefNum);
			SheetDefs.RefreshCache();
			PayPlan payPlanDeletedDb=PayPlans.GetOne(payPlanDeleted.PayPlanNum);
			Assert.AreEqual(0,payPlanDeletedDb.SheetDefNum,"The plan goes back to the default sheet.");
			Assert.AreEqual(hashBefore,payPlanDeletedDb.SecurityHash,"SheetDefNum is not part of the row hash.");
			Assert.AreEqual(sheetDefKept.SheetDefNum,PayPlans.GetOne(payPlanKept.PayPlanNum).SheetDefNum,"Other plans keep their sheet.");
			PayPlanTemplate payPlanTemplateDb=PayPlanTemplates.GetAll().First(x => x.PayPlanTemplateNum==payPlanTemplate.PayPlanTemplateNum);
			Assert.AreEqual(0,payPlanTemplateDb.SheetDefNum,"The template goes back to the default sheet.");
		}

		[TestMethod]
		public void Template_SheetIsCopiedToNewPlans() {
			SheetDef sheetDef=SheetDefT.CreateSheetDef(SheetTypeEnum.PaymentPlan,"Template plan sheet");
			PayPlanTemplate payPlanTemplate=new PayPlanTemplate { PayPlanTemplateName="Sheet template",Note="",SheetDefNum=sheetDef.SheetDefNum };
			PayPlanTemplates.Insert(payPlanTemplate);
			PayPlanTemplate payPlanTemplateDb=PayPlanTemplates.GetAll().First(x => x.PayPlanTemplateNum==payPlanTemplate.PayPlanTemplateNum);
			Assert.AreEqual(sheetDef.SheetDefNum,payPlanTemplateDb.SheetDefNum,"The template stores its sheet.");
			PayPlan payPlan=new PayPlan();
			PayPlanTemplates.ApplySheetDefToPayPlan(payPlanTemplateDb,payPlan);
			Assert.AreEqual(sheetDef.SheetDefNum,payPlan.SheetDefNum);
		}

		[TestMethod]
		public void Template_DeletedOrWrongTypeSheet_GivesTheDefault() {
			SheetDef sheetDefStatement=SheetDefT.CreateSheetDef(SheetTypeEnum.Statement,"A statement");
			PayPlan payPlan=new PayPlan { SheetDefNum=5 };
			PayPlanTemplates.ApplySheetDefToPayPlan(new PayPlanTemplate { SheetDefNum=sheetDefStatement.SheetDefNum },payPlan);
			Assert.AreEqual(0,payPlan.SheetDefNum);
			Assert.AreEqual(0,PayPlanTemplates.GetSheetDefNumForPayPlan(new PayPlanTemplate { SheetDefNum=987654321 }));
			Assert.AreEqual(0,PayPlanTemplates.GetSheetDefNumForPayPlan(null));
		}

		[TestMethod]
		public void PayPlanToSheet_PrintsWithTheChosenSheet() {
			Patient patient=PayPlanDb.CreatePatient("SheetPrint");
			SheetDef sheetDefCustom=SheetDefT.CreateCustomSheet(SheetInternalType.PaymentPlan);
			sheetDefCustom.Description="Z office plan sheet";
			SheetDefs.Update(sheetDefCustom);
			SheetDef sheetDefFirst=SheetDefT.CreateCustomSheet(SheetInternalType.PaymentPlan);
			sheetDefFirst.Description="A office plan sheet";
			SheetDefs.Update(sheetDefFirst);
			SheetDefs.RefreshCache();
			PayPlan payPlan=PayPlanT.CreatePayPlan(patient.PatNum,300,100,DateTime.Today,0);
			payPlan.SheetDefNum=sheetDefCustom.SheetDefNum;
			PayPlans.Update(payPlan);
			Sheet sheet=PayPlans.PayPlanToSheet(PayPlans.GetOne(payPlan.PayPlanNum));
			Assert.AreEqual(sheetDefCustom.SheetDefNum,sheet.SheetDefNum,"Not the first custom sheet, the chosen one.");
			payPlan.SheetDefNum=0;
			PayPlans.Update(payPlan);
			sheet=PayPlans.PayPlanToSheet(PayPlans.GetOne(payPlan.PayPlanNum));
			Assert.AreEqual(sheetDefFirst.SheetDefNum,sheet.SheetDefNum,"0 keeps the 24.3 choice.");
		}
	}
}
