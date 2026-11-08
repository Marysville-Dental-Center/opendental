// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262MiscTests {
	///<summary>Vital signs on eForms (Open Dental 26.2 item 37), on objects only: the links, reading answers, the save check, what the import window
	///shows and the entry an import makes.</summary>
	[TestClass]
	public class EFormVitalSignsTests {
		[TestInitialize]
		public void Init() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		public static EFormField Field(string dbLink,string value,string label="",EnumEFormFieldType fieldType=EnumEFormFieldType.TextField) {
			return new EFormField { FieldType=fieldType,DbLink=dbLink,ValueString=value,ValueLabel=label };
		}

		public static EForm Form(params EFormField[] arrayFields) {
			return new EForm { PatNum=5,EFormNum=8,Description="Health update",DateTimeShown=new DateTime(2026,11,2,9,30,0),
				ListEFormFields=new List<EFormField>(arrayFields) };
		}

		private static double Parse(string dbLink,string answer) {
			Assert.IsTrue(EFormVitalSigns.TryParse(dbLink,answer,out double value,out string error),answer+": "+error);
			Assert.AreEqual("",error);
			return value;
		}

		private static string ParseError(string dbLink,string answer) {
			Assert.IsFalse(EFormVitalSigns.TryParse(dbLink,answer,out double _,out string error),answer);
			return error;
		}

		#region Links
		[TestMethod]
		public void Links_OfferedForTextFields_NeverAutoImportOrPrefillFromGuarantor() {
			List<string> listTextBox=EFormFieldsAvailable.GetList_TextBox();
			foreach(string dbLink in EFormVitalSigns.ArrayDbLinks) {
				CollectionAssert.Contains(listTextBox,dbLink);
				Assert.IsTrue(EFormVitalSigns.IsVitalLink(dbLink));
				Assert.IsFalse(EFormFieldsAvailable.IsAutoImportAllowed(dbLink),"Open Dental's 26.2 Auto Import list has no vital sign.");
				Assert.IsFalse(EFormFieldsAvailable.IsPrefillFromGuarAllowed(dbLink));
				CollectionAssert.DoesNotContain(EFormFieldsAvailable.GetList_DateField(),dbLink);
				CollectionAssert.DoesNotContain(EFormFieldsAvailable.GetList_RadioButtons(),dbLink);
			}
			Assert.AreEqual(5,EFormVitalSigns.ArrayDbLinks.Distinct().Count());
			Assert.AreEqual(listTextBox.Count,listTextBox.Distinct().Count(),"No link is listed twice.");
			Assert.IsFalse(EFormVitalSigns.IsVitalLink("Height"));
			Assert.IsFalse(EFormVitalSigns.IsVitalLink(""));
			Assert.IsFalse(EFormVitalSigns.IsVitalLink(null));
		}

		[TestMethod]
		public void DefaultLabels_NameTheUnit() {
			Assert.AreEqual("Height (inches)",EFormVitalSigns.GetDefaultLabel(EFormVitalSigns.DBLINK_HEIGHT));
			Assert.AreEqual("Weight (pounds)",EFormVitalSigns.GetDefaultLabel(EFormVitalSigns.DBLINK_WEIGHT));
			StringAssert.Contains(EFormVitalSigns.GetDefaultLabel(EFormVitalSigns.DBLINK_BP_SYSTOLIC),"mmHg");
			StringAssert.Contains(EFormVitalSigns.GetDefaultLabel(EFormVitalSigns.DBLINK_PULSE),"per minute");
			Assert.AreEqual("City",EFormVitalSigns.GetDefaultLabel("City"));
		}
		#endregion Links

		#region Reading answers
		[TestMethod]
		public void Height_InchesFeetAndUnits() {
			string h=EFormVitalSigns.DBLINK_HEIGHT;
			Assert.AreEqual(70,Parse(h,"70"));
			Assert.AreEqual(70,Parse(h," 70 in "));
			Assert.AreEqual(70,Parse(h,"70 inches"));
			Assert.AreEqual(70,Parse(h,"70\""));
			Assert.AreEqual(68.5,Parse(h,"68.5"));
			Assert.AreEqual(70,Parse(h,"5'10\""));
			Assert.AreEqual(70,Parse(h,"5' 10"));
			Assert.AreEqual(70,Parse(h,"5 ft 10 in"));
			Assert.AreEqual(70,Parse(h,"5ft10in"));
			Assert.AreEqual(70,Parse(h,"5 feet 10 inches"));
			Assert.AreEqual(60,Parse(h,"5'"));
			Assert.AreEqual(66,Parse(h,"5.5 ft"));
			Assert.AreEqual("not a number",ParseError(h,"5'14\""),"14 inches is not an inches part.");
			Assert.AreEqual("not a number",ParseError(h,"tall"));
			Assert.AreEqual("not a number",ParseError(h,"-70"));
			Assert.AreEqual("not a number",ParseError(h,"178 cm"),"Other units are not converted.");
			StringAssert.StartsWith(ParseError(h,"9"),"outside 10 to 108");
			Assert.AreEqual(108,Parse(h,"9'"),"108 inches is the most.");
			StringAssert.StartsWith(ParseError(h,"9'1\""),"outside");
		}

		[TestMethod]
		public void Weight_PoundsAndUnits() {
			string w=EFormVitalSigns.DBLINK_WEIGHT;
			Assert.AreEqual(180,Parse(w,"180"));
			Assert.AreEqual(180,Parse(w,"180 lbs"));
			Assert.AreEqual(180,Parse(w,"180lb"));
			Assert.AreEqual(180,Parse(w,"180 pounds"));
			Assert.AreEqual(180,Parse(w,"180#"));
			Assert.AreEqual(180.4,Parse(w,"180.4"));
			Assert.AreEqual("not a number",ParseError(w,"1,800"),"No group separators.");
			Assert.AreEqual("not a number",ParseError(w,"82 kg"));
			StringAssert.StartsWith(ParseError(w,"0"),"outside");
			StringAssert.StartsWith(ParseError(w,"1600"),"outside");
		}

		[TestMethod]
		public void Weight_DecimalCommaInACommaCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
			Assert.AreEqual(82.5,Parse(EFormVitalSigns.DBLINK_WEIGHT,"82,5"));
			Assert.AreEqual(82.5,Parse(EFormVitalSigns.DBLINK_WEIGHT,"82.5"),"A period is read too.");
		}

		[TestMethod]
		public void BloodPressureAndPulse_WholeNumbers() {
			Assert.AreEqual(120,Parse(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"120"));
			Assert.AreEqual(120,Parse(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"120 mmHg"));
			Assert.AreEqual(80,Parse(EFormVitalSigns.DBLINK_BP_DIASTOLIC,"80mmhg"));
			Assert.AreEqual(72,Parse(EFormVitalSigns.DBLINK_PULSE,"72 bpm"));
			Assert.AreEqual(72,Parse(EFormVitalSigns.DBLINK_PULSE,"72.0"),"A whole number written with a decimal.");
			Assert.AreEqual("not a whole number",ParseError(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"120.5"));
			Assert.AreEqual("not a number",ParseError(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"120/80"),"Each pressure has its own field.");
			Assert.AreEqual("not a number",ParseError(EFormVitalSigns.DBLINK_PULSE,"fast"));
			StringAssert.StartsWith(ParseError(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"30"),"outside 50 to 300");
			StringAssert.StartsWith(ParseError(EFormVitalSigns.DBLINK_BP_DIASTOLIC,"250"),"outside 20 to 200");
			StringAssert.StartsWith(ParseError(EFormVitalSigns.DBLINK_PULSE,"10"),"outside 20 to 300");
		}

		[TestMethod]
		public void EmptyAnswer_IsNoAnswerNotAnError() {
			Assert.IsFalse(EFormVitalSigns.TryParse(EFormVitalSigns.DBLINK_HEIGHT,"  ",out double _,out string error));
			Assert.AreEqual("",error);
			Assert.IsFalse(EFormVitalSigns.TryParse(EFormVitalSigns.DBLINK_PULSE,null,out double _,out error));
			Assert.AreEqual("",error);
		}
		#endregion Reading answers

		#region Save check
		[TestMethod]
		public void Validate_OnSave() {
			Assert.AreEqual("",EFormVitalSigns.Validate(Field(EFormVitalSigns.DBLINK_HEIGHT,"")));
			Assert.AreEqual("",EFormVitalSigns.Validate(Field(EFormVitalSigns.DBLINK_HEIGHT,"5'10\"")));
			Assert.AreEqual("Height (inches): not a number.",EFormVitalSigns.Validate(Field(EFormVitalSigns.DBLINK_HEIGHT,"tall")));
			Assert.AreEqual("How tall are you?: not a number.",EFormVitalSigns.Validate(Field(EFormVitalSigns.DBLINK_HEIGHT,"tall","How tall are you?")));
			StringAssert.StartsWith(EFormVitalSigns.Validate(Field(EFormVitalSigns.DBLINK_PULSE,"400")),"Pulse (beats per minute): outside");
			EFormField eFormFieldHidden=Field(EFormVitalSigns.DBLINK_WEIGHT,"heavy");
			eFormFieldHidden.IsHiddenCondit=true;
			Assert.AreEqual("",EFormVitalSigns.Validate(eFormFieldHidden),"Hidden by conditional logic.");
			Assert.AreEqual("",EFormVitalSigns.Validate(Field("City","tall")),"Other links are not checked here.");
			Assert.AreEqual("",EFormVitalSigns.Validate(null));
		}
		#endregion Save check

		#region Import window
		[TestMethod]
		public void GetImports_NoVitalFields_Empty() {
			Assert.AreEqual(0,EFormVitalSigns.GetImports(Form(Field("FName","Pat")),null).Count);
			Assert.AreEqual(0,EFormVitalSigns.GetImports(new EForm(),null).Count);
			Assert.AreEqual(0,EFormVitalSigns.GetImports(null,null).Count);
		}

		[TestMethod]
		public void GetImports_OneItemPerAnsweredVitalSign_InFixedOrder() {
			EForm eForm=Form(
				Field(EFormVitalSigns.DBLINK_PULSE,"72"),
				Field(EFormVitalSigns.DBLINK_HEIGHT,"5'10\""),
				Field(EFormVitalSigns.DBLINK_WEIGHT,""),//not answered
				Field(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"118"),
				Field(EFormVitalSigns.DBLINK_BP_DIASTOLIC,"76"));
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(eForm,null);
			CollectionAssert.AreEqual(new[] { EFormVitalSigns.DBLINK_HEIGHT,EFormVitalSigns.DBLINK_BP_SYSTOLIC,EFormVitalSigns.DBLINK_BP_DIASTOLIC,
				EFormVitalSigns.DBLINK_PULSE },listImports.Select(x => x.DbLink).ToArray());
			Assert.IsTrue(listImports.All(x => x.Error==""));
			Assert.AreEqual(70,listImports[0].Value);
			Assert.AreEqual("5'10\"",listImports[0].Answer);
			Assert.AreEqual("Height (in)",listImports[0].FieldDisplay);
			Assert.AreEqual("",listImports[0].ValueOld,"No entry on file.");
		}

		[TestMethod]
		public void GetImports_HiddenFieldsSkipped_LastAnsweredFieldWins() {
			EFormField eFormFieldParent=new EFormField { FieldType=EnumEFormFieldType.CheckBox,ValueLabel="Measured today",ValueString="" };
			EFormField eFormFieldChild=Field(EFormVitalSigns.DBLINK_PULSE,"90");
			eFormFieldChild.ConditionalParent="Measured today";
			eFormFieldChild.ConditionalValue="X";
			EForm eForm=Form(eFormFieldParent,eFormFieldChild,Field(EFormVitalSigns.DBLINK_WEIGHT,"150"),Field(EFormVitalSigns.DBLINK_WEIGHT,"155"),
				Field(EFormVitalSigns.DBLINK_WEIGHT," "));
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(eForm,null);
			Assert.AreEqual(1,listImports.Count,"The pulse field is hidden because the box is not checked.");
			Assert.AreEqual(155,listImports[0].Value,"The last weight field with an answer.");
			eFormFieldParent.ValueString="X";
			Assert.AreEqual(2,EFormVitalSigns.GetImports(eForm,null).Count,"Checked: the pulse shows and is imported.");
		}

		[TestMethod]
		public void GetImports_ErrorsAndBloodPressureOrder() {
			EForm eForm=Form(Field(EFormVitalSigns.DBLINK_HEIGHT,"tall"),Field(EFormVitalSigns.DBLINK_BP_SYSTOLIC,"80"),
				Field(EFormVitalSigns.DBLINK_BP_DIASTOLIC,"80"));
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(eForm,null);
			Assert.AreEqual("not a number",listImports.Single(x => x.DbLink==EFormVitalSigns.DBLINK_HEIGHT).Error);
			Assert.AreEqual(0,listImports.Single(x => x.DbLink==EFormVitalSigns.DBLINK_HEIGHT).Value);
			Assert.AreEqual("not higher than the diastolic value",listImports.Single(x => x.DbLink==EFormVitalSigns.DBLINK_BP_SYSTOLIC).Error);
			Assert.AreEqual("",listImports.Single(x => x.DbLink==EFormVitalSigns.DBLINK_BP_DIASTOLIC).Error);
		}

		[TestMethod]
		public void GetImports_OnlyTextFieldsCount() {
			EForm eForm=Form(Field(EFormVitalSigns.DBLINK_PULSE,"72",fieldType:EnumEFormFieldType.Label));
			Assert.AreEqual(0,EFormVitalSigns.GetImports(eForm,null).Count);
		}

		[TestMethod]
		public void GetImports_CurrentValueFromLatestEntry() {
			Vitalsign vitalsignLatest=new Vitalsign { DateTaken=new DateTime(2026,5,4),Height=64.5f,Weight=0,Pulse=70 };
			EForm eForm=Form(Field(EFormVitalSigns.DBLINK_HEIGHT,"65"),Field(EFormVitalSigns.DBLINK_WEIGHT,"130"),Field(EFormVitalSigns.DBLINK_PULSE,"70"));
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(eForm,vitalsignLatest);
			Assert.AreEqual("64.5 (5/4/2026)",listImports[0].ValueOld);
			Assert.AreEqual("",listImports[1].ValueOld,"0 means not recorded.");
			Assert.AreEqual("70 (5/4/2026)",listImports[2].ValueOld);
		}

		[TestMethod]
		public void DateTaken_SubmittedThenShownThenToday() {
			DateTime dateToday=new DateTime(2026,11,7);
			EForm eForm=Form();
			eForm.DateTimeSubmitted=new DateTime(2026,11,3,17,45,0);
			Assert.AreEqual(new DateTime(2026,11,3),EFormVitalSigns.GetDateTaken(eForm,dateToday));
			eForm.DateTimeSubmitted=DateTime.MinValue;
			Assert.AreEqual(new DateTime(2026,11,2),EFormVitalSigns.GetDateTaken(eForm,dateToday));
			eForm.DateTimeShown=DateTime.MinValue;
			Assert.AreEqual(dateToday,EFormVitalSigns.GetDateTaken(eForm,dateToday));
		}
		#endregion Import window

		#region The entry
		private static List<EFormVitalImport> Imports(params (string DbLink,double Value)[] arrayValues) {
			return arrayValues.Select(x => new EFormVitalImport { DbLink=x.DbLink,FieldDisplay=EFormVitalSigns.GetFieldDisplay(x.DbLink),Value=x.Value })
				.ToList();
		}

		[TestMethod]
		public void CreateVitalsign_Values_AdultWithCodes() {
			Patient patient=new Patient { PatNum=5,Birthdate=new DateTime(1980,6,1),Gender=PatientGender.Female };
			List<EFormVitalImport> listImports=Imports((EFormVitalSigns.DBLINK_HEIGHT,64),(EFormVitalSigns.DBLINK_WEIGHT,200),
				(EFormVitalSigns.DBLINK_BP_SYSTOLIC,128),(EFormVitalSigns.DBLINK_BP_DIASTOLIC,84),(EFormVitalSigns.DBLINK_PULSE,66));
			listImports.Add(new EFormVitalImport { DbLink=EFormVitalSigns.DBLINK_PULSE,Value=99,Error="outside" });//an error item is never used
			Vitalsign vitalsign=EFormVitalSigns.CreateVitalsign(patient,new DateTime(2026,11,3,10,0,0),listImports,x => true,x => true);
			Assert.AreEqual(5,vitalsign.PatNum);
			Assert.AreEqual(new DateTime(2026,11,3),vitalsign.DateTaken);
			Assert.AreEqual(64f,vitalsign.Height);
			Assert.AreEqual(200f,vitalsign.Weight);
			Assert.AreEqual(128,vitalsign.BpSystolic);
			Assert.AreEqual(84,vitalsign.BpDiastolic);
			Assert.AreEqual(66,vitalsign.Pulse);
			Assert.AreEqual("8302-2",vitalsign.HeightExamCode);
			Assert.AreEqual("29463-7",vitalsign.WeightExamCode);
			Assert.AreEqual(-1,vitalsign.BMIPercentile,"Only for ages 3 to 16.");
			Assert.AreEqual("",vitalsign.BMIExamCode);
			Assert.AreEqual("238131007",vitalsign.WeightCode,"BMI 34.3: overweight, as the vital sign window codes it.");
			Assert.AreEqual(0,vitalsign.EhrNotPerformedNum);
			Assert.AreEqual(0,vitalsign.PregDiseaseNum);
		}

		[TestMethod]
		public void CreateVitalsign_NoCodesWithoutTheCodeTables_NothingNotImported() {
			Patient patient=new Patient { PatNum=5,Birthdate=new DateTime(1980,6,1),Gender=PatientGender.Male };
			Vitalsign vitalsign=EFormVitalSigns.CreateVitalsign(patient,new DateTime(2026,11,3),
				Imports((EFormVitalSigns.DBLINK_HEIGHT,70),(EFormVitalSigns.DBLINK_WEIGHT,100)),null,null);
			Assert.AreEqual("",vitalsign.HeightExamCode);
			Assert.AreEqual("",vitalsign.WeightExamCode);
			Assert.AreEqual("",vitalsign.WeightCode,"Underweight, but SNOMED CT is not loaded.");
			Assert.AreEqual(0,vitalsign.BpSystolic);
			Assert.AreEqual(0,vitalsign.Pulse);
			Vitalsign vitalsignBpOnly=EFormVitalSigns.CreateVitalsign(patient,new DateTime(2026,11,3),
				Imports((EFormVitalSigns.DBLINK_BP_SYSTOLIC,120),(EFormVitalSigns.DBLINK_BP_DIASTOLIC,80)),x => true,x => true);
			Assert.AreEqual("",vitalsignBpOnly.HeightExamCode,"Can be blank if BP only.");
			Assert.AreEqual("",vitalsignBpOnly.WeightExamCode);
			Assert.AreEqual(0f,vitalsignBpOnly.Height);
		}

		[TestMethod]
		public void CreateVitalsign_ChildGetsBmiPercentile() {
			Patient patient=new Patient { PatNum=6,Birthdate=new DateTime(2016,3,15),Gender=PatientGender.Male };
			Vitalsign vitalsign=EFormVitalSigns.CreateVitalsign(patient,new DateTime(2026,11,3),
				Imports((EFormVitalSigns.DBLINK_HEIGHT,55),(EFormVitalSigns.DBLINK_WEIGHT,70)),x => true,x => true);
			Assert.IsTrue(vitalsign.BMIPercentile>=0 && vitalsign.BMIPercentile<=100,"Age 9 on January 1: percentile "+vitalsign.BMIPercentile);
			Assert.AreEqual("59576-9",vitalsign.BMIExamCode);
			Assert.AreEqual("",vitalsign.WeightCode,"Children are not coded over or under weight.");
		}

		[TestMethod]
		public void SameMeasurement() {
			Vitalsign vitalsignA=new Vitalsign { DateTaken=new DateTime(2026,11,3),Height=70,Weight=180,BpSystolic=120,BpDiastolic=80,Pulse=70 };
			Vitalsign vitalsignB=vitalsignA.Copy();
			Assert.IsTrue(EFormVitalSigns.IsSameMeasurement(vitalsignA,vitalsignB));
			vitalsignB.Pulse=71;
			Assert.IsFalse(EFormVitalSigns.IsSameMeasurement(vitalsignA,vitalsignB));
			vitalsignB=vitalsignA.Copy();
			vitalsignB.DateTaken=new DateTime(2026,11,4);
			Assert.IsFalse(EFormVitalSigns.IsSameMeasurement(vitalsignA,vitalsignB),"Another day is another measurement.");
		}
		#endregion The entry
	}
}
