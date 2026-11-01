// v26_1-chart-eform-sig-datetime, v25_4-sheets-datetimesubmitted and v26_1-misc-audit-eform-delete without a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2EFTests {
	[TestClass]
	public class SignatureAndSubmittedLogicTests {
		private static readonly DateTime _dateTimeNow=new DateTime(2026,10,25,9,30,15);
		private static readonly DateTime _dateTimeEarlier=new DateTime(2026,10,20,14,5,47);
		private CultureInfo _cultureInfoOld;

		[TestInitialize]
		public void TestInitialize() {
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		private static EFormField SigBox(string value,DateTime dateTimeSig) {
			EFormField eFormField=PatFieldLogicTests.Field(EnumEFormFieldType.SigBox,"",value);
			eFormField.DateTimeSig=dateTimeSig;
			return eFormField;
		}

		private static List<EFormField> Copy(List<EFormField> listEFormFields) {
			return EFormFields.GetDeepCopy(listEFormFields);
		}

		[TestMethod]
		public void SigTime_NewSignatureGetsTheServerTime() {
			List<EFormField> listOld=new List<EFormField>() { PatFieldLogicTests.Field(EnumEFormFieldType.TextField,"FName","Ann"),SigBox("",DateTime.MinValue) };
			List<EFormField> listNew=Copy(listOld);
			listNew[1].ValueString="0abcSIGNATURE";
			int calls=0;
			Assert.IsTrue(EFormFields.SetDateTimeSigs(listNew,listOld,() => { calls++; return _dateTimeNow; }));
			Assert.AreEqual(_dateTimeNow,listNew[1].DateTimeSig);
			Assert.AreEqual(DateTime.MinValue,listNew[0].DateTimeSig,"only signature boxes");
			Assert.AreEqual(1,calls,"the server time is read once");
		}

		[TestMethod]
		public void SigTime_ChangedSignatureGetsANewTime() {
			List<EFormField> listOld=new List<EFormField>() { SigBox("0first",_dateTimeEarlier) };
			List<EFormField> listNew=Copy(listOld);
			listNew[0].ValueString="1second";
			EFormFields.SetDateTimeSigs(listNew,listOld,() => _dateTimeNow);
			Assert.AreEqual(_dateTimeNow,listNew[0].DateTimeSig);
		}

		[TestMethod]
		public void SigTime_ClearedSignatureLosesItsTime() {
			List<EFormField> listOld=new List<EFormField>() { SigBox("0first",_dateTimeEarlier) };
			List<EFormField> listNew=Copy(listOld);
			listNew[0].ValueString="0";//the fill control stores "0" for an empty box
			Assert.IsTrue(EFormFields.SetDateTimeSigs(listNew,listOld,() => _dateTimeNow));
			Assert.AreEqual(DateTime.MinValue,listNew[0].DateTimeSig);
		}

		[TestMethod]
		public void SigTime_UnchangedSignatureKeepsItsTime() {
			List<EFormField> listOld=new List<EFormField>() { SigBox("0first",_dateTimeEarlier) };
			List<EFormField> listNew=Copy(listOld);
			Assert.IsFalse(EFormFields.SetDateTimeSigs(listNew,listOld,() => throw new AssertFailedException("no time needed")));
			Assert.AreEqual(_dateTimeEarlier,listNew[0].DateTimeSig);
			//signed before 26.1: no time recorded, and saving again does not invent one
			List<EFormField> listOldNoTime=new List<EFormField>() { SigBox("0first",DateTime.MinValue) };
			List<EFormField> listNewNoTime=Copy(listOldNoTime);
			EFormFields.SetDateTimeSigs(listNewNoTime,listOldNoTime,() => _dateTimeNow);
			Assert.AreEqual(DateTime.MinValue,listNewNoTime[0].DateTimeSig);
		}

		[TestMethod]
		public void SigCaption_OnlyForSignedBoxesWithATime() {
			Assert.AreEqual("Signed 10/20/2026 2:05 PM",EFormFields.GetSigCaption(SigBox("0sig",_dateTimeEarlier)));
			Assert.AreEqual("",EFormFields.GetSigCaption(SigBox("0sig",DateTime.MinValue)));
			Assert.AreEqual("",EFormFields.GetSigCaption(SigBox("0",_dateTimeEarlier)));
			Assert.IsFalse(EFormFields.IsSigned(PatFieldLogicTests.Field(EnumEFormFieldType.TextField,"","0signature")));
		}

		[TestMethod]
		public void Submitted_DisplayAndParse() {
			Assert.AreEqual("",FormSubmittedDates.ToDisplay(DateTime.MinValue));
			Assert.AreEqual("10/20/2026 2:05 PM",FormSubmittedDates.ToDisplay(_dateTimeEarlier));
			Assert.IsTrue(FormSubmittedDates.TryParse("  ",out DateTime dateTimeBlank));
			Assert.AreEqual(DateTime.MinValue,dateTimeBlank);
			Assert.IsTrue(FormSubmittedDates.TryParse("10/21/2026 8:00 AM",out DateTime dateTimeTyped));
			Assert.AreEqual(new DateTime(2026,10,21,8,0,0),dateTimeTyped);
			Assert.IsFalse(FormSubmittedDates.TryParse("yesterday",out DateTime _));
			Assert.IsFalse(FormSubmittedDates.TryParse("1/1/1801",out DateTime _));
		}

		[TestMethod]
		public void Submitted_UnchangedBoxKeepsTheStoredSeconds() {
			Assert.IsTrue(FormSubmittedDates.TryParse(FormSubmittedDates.ToDisplay(_dateTimeEarlier),_dateTimeEarlier,out DateTime dateTimeKept));
			Assert.AreEqual(_dateTimeEarlier,dateTimeKept,"opening and saving must not drop the seconds");
			Assert.IsTrue(FormSubmittedDates.TryParse("",DateTime.MinValue,out DateTime dateTimeNone));
			Assert.AreEqual(DateTime.MinValue,dateTimeNone);
			Assert.IsTrue(FormSubmittedDates.TryParse("",_dateTimeEarlier,out DateTime dateTimeCleared));
			Assert.AreEqual(DateTime.MinValue,dateTimeCleared,"the user cleared it");
		}

		[TestMethod]
		public void Submitted_NewSignedFormDefaultsToNow() {
			Assert.AreEqual(_dateTimeNow,FormSubmittedDates.GetToSave(DateTime.MinValue,isNewForm:true,isSigned:true,_dateTimeNow));
			Assert.AreEqual(DateTime.MinValue,FormSubmittedDates.GetToSave(DateTime.MinValue,isNewForm:true,isSigned:false,_dateTimeNow));
			Assert.AreEqual(DateTime.MinValue,FormSubmittedDates.GetToSave(DateTime.MinValue,isNewForm:false,isSigned:true,_dateTimeNow),
				"only a new form gets the default");
			Assert.AreEqual(_dateTimeEarlier,FormSubmittedDates.GetToSave(_dateTimeEarlier,isNewForm:true,isSigned:true,_dateTimeNow),
				"an entered date wins");
			Assert.IsTrue(FormSubmittedDates.IsEFormSigned(new List<EFormField>() { SigBox("0sig",DateTime.MinValue) }));
			Assert.IsFalse(FormSubmittedDates.IsEFormSigned(new List<EFormField>() { SigBox("0",DateTime.MinValue) }));
		}

		[TestMethod]
		public void Submitted_PatientFormSheetTypes() {
			Assert.IsTrue(FormSubmittedDates.IsPatientFormSheetType(SheetTypeEnum.PatientForm));
			Assert.IsTrue(FormSubmittedDates.IsPatientFormSheetType(SheetTypeEnum.MedicalHistory));
			Assert.IsTrue(FormSubmittedDates.IsPatientFormSheetType(SheetTypeEnum.Consent));
			Assert.IsFalse(FormSubmittedDates.IsPatientFormSheetType(SheetTypeEnum.ReferralLetter));
			Assert.IsFalse(FormSubmittedDates.IsPatientFormSheetType(SheetTypeEnum.TreatmentPlan));
		}

		[TestMethod]
		public void DeleteLogText_NamesFormDateIdAndSubmitted() {
			EForm eForm=new EForm() { EFormNum=42,PatNum=7,Description="Consent",DateTimeShown=_dateTimeEarlier };
			Assert.AreEqual("eForm Consent dated 10/20/2026 2:05 PM (ID 42) deleted",EForms.GetDeleteLogText(eForm));
			eForm.DateTimeSubmitted=new DateTime(2026,10,20,14,30,0);
			Assert.AreEqual("eForm Consent dated 10/20/2026 2:05 PM (ID 42) deleted, submitted 10/20/2026 2:30 PM",EForms.GetDeleteLogText(eForm));
		}
	}
}
