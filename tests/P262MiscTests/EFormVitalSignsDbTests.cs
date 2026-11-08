// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262MiscTests {
	///<summary>Vital signs from eForms (Open Dental 26.2 item 37) on the scratch database: the import adds one entry and logs it, never twice for the same
	///form, the form's fields start empty, and saving a form checks the answers.</summary>
	[TestClass]
	public class EFormVitalSignsDbTests {
		[TestInitialize]
		public void Init() {
			MiscDb.Begin();
		}

		private static Patient NewPatient() {
			return PatientT.CreatePatient(suffix:Guid.NewGuid().ToString("N").Substring(0,8),lName:"Vitals",fName:"Form",birthDate:new DateTime(1975,4,20));
		}

		private static EForm FormFor(Patient patient,params EFormField[] arrayFields) {
			EForm eForm=EFormVitalSignsTests.Form(arrayFields);
			eForm.PatNum=patient.PatNum;
			return eForm;
		}

		[TestMethod]
		public void Import_AddsOneEntryDatedByTheForm_AndLogsIt() {
			Patient patient=NewPatient();
			EForm eForm=FormFor(patient,Field(EFormVitalSigns.DBLINK_HEIGHT,"5'9\""),Field(EFormVitalSigns.DBLINK_WEIGHT,"165 lbs"),
				Field(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"122"),Field(EFormVitalSigns.DBLINK_BP_DIASTOLIC,"78"),Field(EFormVitalSigns.DBLINK_PULSE,"64"));
			eForm.DateTimeSubmitted=new DateTime(2026,11,4,8,15,0);
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(eForm,Vitalsigns.GetMostRecent(patient.PatNum));
			EFormVitalImportResult result=EFormVitalSigns.Import(patient.PatNum,EFormVitalSigns.GetDateTaken(eForm,DateTime.Today),listImports,
				"eForm #"+eForm.EFormNum+" ("+eForm.Description+")");
			Assert.IsTrue(result.VitalsignNum>0);
			Assert.IsFalse(result.IsDuplicate);
			Vitalsign vitalsign=Vitalsigns.Refresh(patient.PatNum).Single();
			Assert.AreEqual(result.VitalsignNum,vitalsign.VitalsignNum);
			Assert.AreEqual(new DateTime(2026,11,4),vitalsign.DateTaken);
			Assert.AreEqual(69f,vitalsign.Height);
			Assert.AreEqual(165f,vitalsign.Weight);
			Assert.AreEqual(122,vitalsign.BpSystolic);
			Assert.AreEqual(78,vitalsign.BpDiastolic);
			Assert.AreEqual(64,vitalsign.Pulse);
			Assert.AreEqual(-1,vitalsign.BMIPercentile);
			string logText=DataCore.GetScalar("SELECT LogText FROM securitylog WHERE PatNum="+POut.Long(patient.PatNum)+" AND PermType="
				+POut.Int((int)EnumPermType.EFormImport)+" ORDER BY SecurityLogNum DESC LIMIT 1");
			StringAssert.StartsWith(logText,"Vital signs added from eForm #8 (Health update), taken 11/4/2026: Height (in) 69, Weight (lb) 165");
		}

		[TestMethod]
		public void ImportTwice_SecondAddsNothing() {
			Patient patient=NewPatient();
			EForm eForm=FormFor(patient,Field(EFormVitalSigns.DBLINK_PULSE,"80"));
			DateTime dateTaken=EFormVitalSigns.GetDateTaken(eForm,DateTime.Today);
			EFormVitalImportResult result1=EFormVitalSigns.Import(patient.PatNum,dateTaken,EFormVitalSigns.GetImports(eForm,null),"eForm #8");
			EFormVitalImportResult result2=EFormVitalSigns.Import(patient.PatNum,dateTaken,EFormVitalSigns.GetImports(eForm,null),"eForm #8");
			Assert.IsTrue(result1.VitalsignNum>0);
			Assert.AreEqual(0,result2.VitalsignNum);
			Assert.IsTrue(result2.IsDuplicate);
			Assert.AreEqual(1,Vitalsigns.Refresh(patient.PatNum).Count);
			//A different answer is a new measurement.
			EForm eForm2=FormFor(patient,Field(EFormVitalSigns.DBLINK_PULSE,"84"));
			EFormVitalSigns.Import(patient.PatNum,dateTaken,EFormVitalSigns.GetImports(eForm2,null),"eForm #9");
			Assert.AreEqual(2,Vitalsigns.Refresh(patient.PatNum).Count);
		}

		[TestMethod]
		public void Import_OnlyErrors_AddsNothing() {
			Patient patient=NewPatient();
			EForm eForm=FormFor(patient,Field(EFormVitalSigns.DBLINK_HEIGHT,"tall"),Field(EFormVitalSigns.DBLINK_PULSE,"5"));
			EFormVitalImportResult result=EFormVitalSigns.Import(patient.PatNum,DateTime.Today,EFormVitalSigns.GetImports(eForm,null),"eForm #8");
			Assert.AreEqual(0,result.VitalsignNum);
			Assert.AreEqual("",result.Description);
			Assert.AreEqual(0,Vitalsigns.Refresh(patient.PatNum).Count);
			Assert.AreEqual(0,EFormVitalSigns.Import(0,DateTime.Today,null,"").VitalsignNum,"No patient, nothing to import.");
		}

		[TestMethod]
		public void Import_SkipsErrorItems_KeepsTheRest() {
			Patient patient=NewPatient();
			EForm eForm=FormFor(patient,Field(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"70"),Field(EFormVitalSigns.DBLINK_BP_DIASTOLIC,"90"),
				Field(EFormVitalSigns.DBLINK_WEIGHT,"150"));
			EFormVitalSigns.Import(patient.PatNum,DateTime.Today,EFormVitalSigns.GetImports(eForm,null),"eForm #8");
			Vitalsign vitalsign=Vitalsigns.Refresh(patient.PatNum).Single();
			Assert.AreEqual(0,vitalsign.BpSystolic,"Systolic not higher than diastolic: not imported.");
			Assert.AreEqual(90,vitalsign.BpDiastolic);
			Assert.AreEqual(150f,vitalsign.Weight);
		}

		[TestMethod]
		public void CurrentValue_ComesFromTheLatestEntry() {
			Patient patient=NewPatient();
			Vitalsigns.Insert(new Vitalsign { PatNum=patient.PatNum,DateTaken=new DateTime(2025,1,10),Height=66,Weight=150,Documentation="",WeightCode="",
				HeightExamCode="",WeightExamCode="",BMIExamCode="",BMIPercentile=-1 });
			Vitalsigns.Insert(new Vitalsign { PatNum=patient.PatNum,DateTaken=new DateTime(2026,2,10),Height=0,Weight=155,Documentation="",WeightCode="",
				HeightExamCode="",WeightExamCode="",BMIExamCode="",BMIPercentile=-1 });
			EForm eForm=FormFor(patient,Field(EFormVitalSigns.DBLINK_WEIGHT,"160"),Field(EFormVitalSigns.DBLINK_HEIGHT,"66"));
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(eForm,Vitalsigns.GetMostRecent(patient.PatNum));
			Assert.AreEqual("155 (2/10/2026)",listImports.Single(x => x.DbLink==EFormVitalSigns.DBLINK_WEIGHT).ValueOld);
			Assert.AreEqual("",listImports.Single(x => x.DbLink==EFormVitalSigns.DBLINK_HEIGHT).ValueOld,"The latest entry has no height.");
		}

		[TestMethod]
		public void NewForm_VitalFieldsStartEmpty() {
			Patient patient=NewPatient();
			Vitalsigns.Insert(new Vitalsign { PatNum=patient.PatNum,DateTaken=new DateTime(2026,2,10),Height=70,Weight=180,Pulse=60,Documentation="",
				WeightCode="",HeightExamCode="",WeightExamCode="",BMIExamCode="",BMIPercentile=-1 });
			EForm eForm=FormFor(patient,Field("FName",""),Field(EFormVitalSigns.DBLINK_HEIGHT,""),Field(EFormVitalSigns.DBLINK_PULSE,""));
			EFormFiller.FillFields(eForm);
			Assert.AreEqual(patient.FName,eForm.ListEFormFields[0].ValueString,"The filler ran.");
			Assert.AreEqual("",eForm.ListEFormFields[1].ValueString,"Each form is a new measurement: no prefill.");
			Assert.AreEqual("",eForm.ListEFormFields[2].ValueString);
		}

		[TestMethod]
		public void SavingAForm_ChecksTheVitalSigns() {
			Patient patient=NewPatient();
			EForm eForm=FormFor(patient,Field(EFormVitalSigns.DBLINK_HEIGHT,"70"),new EFormField { FieldType=EnumEFormFieldType.PageBreak },
				Field(EFormVitalSigns.DBLINK_PULSE,"fast","Pulse"));
			EFormValidation eFormValidation=EForms.Validate(eForm,"");
			Assert.AreEqual("Pulse: not a number.",eFormValidation.ErrorMsg);
			Assert.AreEqual(2,eFormValidation.PageNum);
			eForm.ListEFormFields[2].ValueString="88";
			Assert.AreEqual("",EForms.Validate(eForm,"").ErrorMsg);
		}

		private static EFormField Field(string dbLink,string value,string label="") {
			return EFormVitalSignsTests.Field(dbLink,value,label);
		}
	}
}
