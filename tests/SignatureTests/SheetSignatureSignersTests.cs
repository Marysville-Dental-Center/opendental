// Change of signing user on sheet signature boxes (25.1, v25_1-sheets-signature-change-user) and sheetfield.UserSigned (25.2,
// v25_2-security-domain-login-change-user).
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace SignatureTests {
	[TestClass]
	public class SheetSignatureSignersTests {
		private static SheetField SigBox(bool canESign,bool isProvRestricted,SheetFieldType fieldType=SheetFieldType.SigBox,long userSigned=0) {
			return new SheetField { FieldType=fieldType,CanElectronicallySign=canESign,IsSigProvRestricted=isProvRestricted,UserSigned=userSigned,FieldName="" };
		}

		private static readonly Userod _userodProvider=new Userod { UserNum=7,UserName="drjones",ProvNum=3 };
		private static readonly Userod _userodStaff=new Userod { UserNum=8,UserName="assistant1",ProvNum=0 };

		[TestMethod]
		public void ChangeUserButton_OnlyOnESignOrProviderBoxes_NeverInTerminal_NeverOnTreatmentPlans() {
			Assert.IsTrue(SheetSignatureSigners.CanChangeUser(SigBox(true,false),SheetTypeEnum.Consent,false));
			Assert.IsTrue(SheetSignatureSigners.CanChangeUser(SigBox(false,true),SheetTypeEnum.Consent,false));
			Assert.IsTrue(SheetSignatureSigners.CanChangeUser(SigBox(true,true,SheetFieldType.SigBoxPractice),SheetTypeEnum.ExamSheet,false));
			Assert.IsFalse(SheetSignatureSigners.CanChangeUser(SigBox(false,false),SheetTypeEnum.Consent,false),"A plain patient box.");
			Assert.IsFalse(SheetSignatureSigners.CanChangeUser(SigBox(true,true),SheetTypeEnum.Consent,true),"Kiosk or terminal.");
			Assert.IsFalse(SheetSignatureSigners.CanChangeUser(SigBox(true,true),SheetTypeEnum.TreatmentPlan,false),"Signed in the Sign TP window.");
			SheetField sheetFieldText=new SheetField { FieldType=SheetFieldType.InputField,CanElectronicallySign=true,IsSigProvRestricted=true };
			Assert.IsFalse(SheetSignatureSigners.CanChangeUser(sheetFieldText,SheetTypeEnum.Consent,false));
			Assert.IsFalse(SheetSignatureSigners.CanChangeUser(null,SheetTypeEnum.Consent,false));
		}

		[TestMethod]
		public void IsChangeUserBox_UsedByTheSignTreatmentPlanWindow() {
			Assert.IsTrue(SheetSignatureSigners.IsChangeUserBox(SigBox(true,false)));
			Assert.IsTrue(SheetSignatureSigners.IsChangeUserBox(SigBox(false,true,SheetFieldType.SigBoxPractice)));
			Assert.IsFalse(SheetSignatureSigners.IsChangeUserBox(SigBox(false,false)));
		}

		[TestMethod]
		public void ProviderRestrictedBox_NeedsAProviderUser() {
			Assert.IsTrue(SheetSignatureSigners.CanUserSign(SigBox(false,true),_userodProvider));
			Assert.IsFalse(SheetSignatureSigners.CanUserSign(SigBox(false,true),_userodStaff));
			Assert.IsFalse(SheetSignatureSigners.CanUserSign(SigBox(false,true),null));
			Assert.IsTrue(SheetSignatureSigners.CanUserSign(SigBox(true,false),_userodStaff),"Unrestricted boxes take anyone.");
		}

		[TestMethod]
		public void ESignButton_FollowsTheBoxAndTheUser() {
			Assert.IsTrue(SheetSignatureSigners.CanUserESign(SigBox(true,false),_userodStaff,false));
			Assert.IsFalse(SheetSignatureSigners.CanUserESign(SigBox(false,true),_userodProvider,false),"Box does not allow e-sign.");
			Assert.IsFalse(SheetSignatureSigners.CanUserESign(SigBox(true,false),_userodProvider,true),"Terminal.");
			Assert.IsFalse(SheetSignatureSigners.CanUserESign(SigBox(true,false),new Userod { UserNum=0 },false));
		}

		[TestMethod]
		public void UserSigned_SetOnlyForAProviderWhoSignedAfterAChangeOfUser() {
			//Signed after a change to a provider: recorded.
			Assert.AreEqual(7,SheetSignatureSigners.GetUserSignedToSave(SigBox(true,false),true,true,_userodProvider));
			Assert.AreEqual(7,SheetSignatureSigners.GetUserSignedToSave(SigBox(false,true,SheetFieldType.SigBoxPractice),true,true,_userodProvider));
			//Signed after a change to a user without a provider: 0.
			Assert.AreEqual(0,SheetSignatureSigners.GetUserSignedToSave(SigBox(true,false),true,true,_userodStaff));
			//Signed without a change of user: 0.
			Assert.AreEqual(0,SheetSignatureSigners.GetUserSignedToSave(SigBox(true,false),true,true,null));
			//Box without e-sign or provider restriction: 0.
			Assert.AreEqual(0,SheetSignatureSigners.GetUserSignedToSave(SigBox(false,false),true,true,_userodProvider));
			//Cleared: 0.
			Assert.AreEqual(0,SheetSignatureSigners.GetUserSignedToSave(SigBox(true,false,userSigned:7),true,false,_userodProvider));
		}

		[TestMethod]
		public void UserSigned_UnchangedSignatureKeepsItsValue() {
			Assert.AreEqual(7,SheetSignatureSigners.GetUserSignedToSave(SigBox(true,false,userSigned:7),false,true,null));
			Assert.AreEqual(0,SheetSignatureSigners.GetUserSignedToSave(SigBox(true,false),false,true,_userodProvider));
		}

		[TestMethod]
		public void SignedByCaption() {
			Func<long,string> getName=x => x==7 ? "drjones" : "";
			Assert.AreEqual("By: drjones",SheetSignatureSigners.GetSignedByText(7,getName));
			Assert.AreEqual("",SheetSignatureSigners.GetSignedByText(0,getName));
			Assert.AreEqual("",SheetSignatureSigners.GetSignedByText(99,getName),"A deleted user shows nothing rather than a bare prefix.");
		}

		[TestMethod]
		public void SheetEditLogText_NamesTheSignerAndTheSheet() {
			Sheet sheet=new Sheet { SheetNum=42,Description="Consent" };
			string text=SheetSignatureSigners.GetSignedLogText(sheet,new SheetField { FieldType=SheetFieldType.SigBoxPractice,FieldName="" },_userodProvider,false);
			StringAssert.Contains(text,"42");
			StringAssert.Contains(text,"Consent");
			StringAssert.Contains(text,"drjones");
			StringAssert.Contains(text,"SigBoxPractice");
			Assert.IsFalse(text.Contains("domain"));
			StringAssert.Contains(SheetSignatureSigners.GetSignedLogText(sheet,new SheetField { FieldType=SheetFieldType.SigBox,FieldName="Doctor" },_userodProvider,true),
				"(domain authentication)");
		}
	}

	[TestClass]
	public class SheetFieldUserSignedDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ScratchDb.RequireDb();
		}

		[TestMethod]
		public void UserSigned_RoundTripsThroughTheSigBoxSync() {
			const long sheetNum=500;
			SheetField sheetFieldPatient=new SheetField { SheetNum=sheetNum,FieldType=SheetFieldType.SigBox,FieldName="",FieldValue="",RadioButtonValue="",
				RadioButtonGroup="",FontName="",ReportableName="",UiLabelMobile="",UiLabelMobileRadioButton="",CanElectronicallySign=true };
			SheetField sheetFieldProvider=sheetFieldPatient.Copy();
			sheetFieldProvider.FieldType=SheetFieldType.SigBoxPractice;
			sheetFieldProvider.IsSigProvRestricted=true;
			SheetFields.Sync(new List<SheetField> { sheetFieldPatient,sheetFieldProvider },sheetNum,isSigBoxOnly:true);
			List<SheetField> listSheetFields=SheetFields.GetListForSheet(sheetNum);
			Assert.AreEqual(2,listSheetFields.Count);
			Assert.IsTrue(listSheetFields.All(x => x.UserSigned==0));
			//A provider signs the practice box after a change of user.
			SheetField sheetFieldSigned=listSheetFields.First(x => x.FieldType==SheetFieldType.SigBoxPractice);
			sheetFieldSigned.FieldValue="0digitalsig";
			sheetFieldSigned.UserSigned=SheetSignatureSigners.GetUserSignedToSave(sheetFieldSigned,true,true,new Userod { UserNum=7,ProvNum=3 });
			SheetFields.Sync(listSheetFields,sheetNum,isSigBoxOnly:true);
			listSheetFields=SheetFields.GetListForSheet(sheetNum);
			Assert.AreEqual(7,listSheetFields.First(x => x.FieldType==SheetFieldType.SigBoxPractice).UserSigned);
			Assert.AreEqual(0,listSheetFields.First(x => x.FieldType==SheetFieldType.SigBox).UserSigned);
			//An unchanged signature keeps its signer on the next save.
			SheetField sheetFieldAgain=listSheetFields.First(x => x.FieldType==SheetFieldType.SigBoxPractice);
			sheetFieldAgain.UserSigned=SheetSignatureSigners.GetUserSignedToSave(sheetFieldAgain,false,true,null);
			SheetFields.Sync(listSheetFields,sheetNum,isSigBoxOnly:true);
			Assert.AreEqual(7,SheetFields.GetListForSheet(sheetNum).First(x => x.FieldType==SheetFieldType.SigBoxPractice).UserSigned);
		}
	}
}
