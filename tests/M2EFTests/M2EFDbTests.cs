// Database tests for package M2-EF on one scratch MariaDB database built from the seed dump, the 24.3.63 convert chain and the fork migration
// runner (schema at Open Dental 26.1.70). Synthetic patients, fields and forms only. Skipped (inconclusive) when the local server does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2EFTests {
	[TestClass]
	public class M2EFDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("db");
		private static string _skipReason;
		private static string _dirFiles;
		private CultureInfo _cultureInfoOld;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestDb.CreateFresh(DB_NAME);
			Security.CurUser=Userods.GetFirstOrDefault(x => true)??new Userod() { UserNum=1,UserName="Admin" };
			_dirFiles=Path.Combine(Path.GetTempPath(),"m2eftests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_dirFiles);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			try {
				if(_dirFiles!=null && Directory.Exists(_dirFiles)) {
					Directory.Delete(_dirFiles,true);
				}
			}
			catch(Exception) {
				//temp files only
			}
			if(_skipReason!=null || Environment.GetEnvironmentVariable("M2EF_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless; the name is unique to this run and stale ones are dropped later.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestDb.Connect(DB_NAME);
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_cultureInfoOld!=null) {
				Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
			}
		}

		#region Helpers
		private static Patient InsertPatient(string fName,string lName) {
			Patient patient=new Patient();
			patient.FName=fName;
			patient.LName=lName;
			patient.PatStatus=PatientStatus.Patient;
			patient.Address="";
			patient.City="";
			patient.State="";
			patient.Zip="";
			patient.WirelessPhone="";
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			Patients.Update(patient,patientOld);
			return patient;
		}

		///<summary>A definition with a name unique to the test, so tests sharing the database don't see each other's fields.</summary>
		private static PatFieldDef InsertDef(string name,PatFieldType type,params string[] arrayPickItems) {
			PatFieldDef patFieldDef=new PatFieldDef() { FieldName=name,FieldType=type,PickList="",ItemOrder=0 };
			PatFieldDefs.Insert(patFieldDef);
			for(int i=0;i<arrayPickItems.Length;i++) {
				PatFieldPickItems.Insert(new PatFieldPickItem() { PatFieldDefNum=patFieldDef.PatFieldDefNum,Name=arrayPickItems[i],Abbreviation="",ItemOrder=i });
			}
			PatFieldDefs.RefreshCache();
			PatFieldPickItems.RefreshCache();
			return patFieldDef;
		}

		private static string Unique(string name) {
			return name+" "+Guid.NewGuid().ToString("N").Substring(0,6);
		}

		private static EForm NewForm(long patNum,params EFormField[] arrayFields) {
			EForm eForm=new EForm();
			eForm.IsNew=true;
			eForm.PatNum=patNum;
			eForm.Description="Synthetic form";
			eForm.DateTimeShown=DateTime.Now;
			eForm.DateTEdited=DateTime.Now;
			eForm.MaxWidth=450;
			eForm.SpaceBelowEachField=-1;
			eForm.SpaceToRightEachField=-1;
			eForm.ListEFormFields=arrayFields.ToList();
			for(int i=0;i<arrayFields.Length;i++) {
				arrayFields[i].PatNum=patNum;
				arrayFields[i].ItemOrder=i;
			}
			return eForm;
		}

		private static List<SecurityLog> GetLogs(long patNum,EnumPermType permType) {
			return SecurityLogs.GetMany(new SQLWhere[] { SQLWhere.Create(nameof(SecurityLog.PatNum),ComparisonOperator.Equals,patNum),
				SQLWhere.Create(nameof(SecurityLog.PermType),ComparisonOperator.Equals,(int)permType) });
		}
		#endregion Helpers

		#region Patient fields (v25_4-eforms-patfields)
		[TestMethod]
		public void PatFields_FillFromTheStoredValues() {
			PatFieldDef patFieldDefText=InsertDef(Unique("Hobby"),PatFieldType.Text);
			PatFieldDef patFieldDefCheck=InsertDef(Unique("Morning"),PatFieldType.Checkbox);
			PatFieldDef patFieldDefPick=InsertDef(Unique("Heard"),PatFieldType.PickList,"Web","Friend");
			Patient patient=InsertPatient("Fill","Sample");
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=patFieldDefText.FieldName,FieldValue="kites" });
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=patFieldDefCheck.FieldName,FieldValue="1" });
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=patFieldDefPick.FieldName,FieldValue="Friend" });
			EForm eForm=NewForm(patient.PatNum,
				PatFieldLogicTests.Field(EnumEFormFieldType.TextField,EFormPatFields.ToDbLink(patFieldDefText.FieldName)),
				PatFieldLogicTests.Field(EnumEFormFieldType.CheckBox,EFormPatFields.ToDbLink(patFieldDefCheck.FieldName)),
				PatFieldLogicTests.Field(EnumEFormFieldType.RadioButtons,EFormPatFields.ToDbLink(patFieldDefPick.FieldName),pickListDb:"Web|Friend"),
				PatFieldLogicTests.Field(EnumEFormFieldType.TextField,"FName"));
			EFormFiller.FillFields(eForm);
			Assert.AreEqual("kites",eForm.ListEFormFields[0].ValueString);
			Assert.AreEqual("X",eForm.ListEFormFields[1].ValueString);
			Assert.AreEqual("Friend",eForm.ListEFormFields[2].ValueString);
			Assert.AreEqual("Fill",eForm.ListEFormFields[3].ValueString,"the normal fill still runs");
		}

		[TestMethod]
		public void PatFields_RadioListsComeFromThePickList() {
			PatFieldDef patFieldDefPick=InsertDef(Unique("Source"),PatFieldType.PickList,"Web","Friend","Radio");
			string dbLink=EFormPatFields.ToDbLink(patFieldDefPick.FieldName);
			CollectionAssert.AreEqual(new[] { "Web","Friend","Radio" },EFormFieldsAvailable.GetRadioDbAll(dbLink));
			CollectionAssert.AreEqual(new[] { "Web","Friend","Radio" },EFormFieldsAvailable.GetRadioDbDefault(dbLink));
			CollectionAssert.AreEqual(new[] { "Web","Friend","Radio" },EFormFieldsAvailable.GetRadioVisDefault(dbLink));
			CollectionAssert.Contains(EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.RadioButtons),dbLink);
			CollectionAssert.DoesNotContain(EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.TextField),dbLink);
		}

		[TestMethod]
		public void PatFields_ValidateRejectsANonNumberForCurrency() {
			PatFieldDef patFieldDefCurrency=InsertDef(Unique("Deposit"),PatFieldType.Currency);
			Patient patient=InsertPatient("Money","Sample");
			EForm eForm=NewForm(patient.PatNum,
				PatFieldLogicTests.Field(EnumEFormFieldType.TextField,EFormPatFields.ToDbLink(patFieldDefCurrency.FieldName),"lots","Deposit"));
			EFormValidation eFormValidation=EForms.Validate(eForm,maskedSSNOld:null);
			Assert.AreEqual("Deposit must be a number.",eFormValidation.ErrorMsg);
			eForm.ListEFormFields[0].ValueString="40";
			Assert.AreEqual("",EForms.Validate(eForm,maskedSSNOld:null).ErrorMsg);
			//A patient field whose name contains "Phone" is free text, not a phone number.
			PatFieldDef patFieldDefPhoneNote=InsertDef(Unique("Phone note"),PatFieldType.Text);
			EForm eFormPhone=NewForm(patient.PatNum,
				PatFieldLogicTests.Field(EnumEFormFieldType.TextField,EFormPatFields.ToDbLink(patFieldDefPhoneNote.FieldName),"call after 5"));
			Assert.AreEqual("",EForms.Validate(eFormPhone,maskedSSNOld:null).ErrorMsg);
			Assert.AreEqual("call after 5",eFormPhone.ListEFormFields[0].ValueString);
		}

		[TestMethod]
		public void PatFields_ImportInsertsUpdatesDeletesAndLogsEachChange() {
			PatFieldDef patFieldDefText=InsertDef(Unique("Hobby"),PatFieldType.Text);
			PatFieldDef patFieldDefDate=InsertDef(Unique("Visit"),PatFieldType.Date);
			PatFieldDef patFieldDefCheck=InsertDef(Unique("Morning"),PatFieldType.Checkbox);
			PatFieldDef patFieldDefSame=InsertDef(Unique("Same"),PatFieldType.Text);
			Patient patient=InsertPatient("Import","Sample");
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=patFieldDefText.FieldName,FieldValue="kites" });
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=patFieldDefCheck.FieldName,FieldValue="1" });
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=patFieldDefSame.FieldName,FieldValue="unchanged" });
			EForm eForm=NewForm(patient.PatNum,
				PatFieldLogicTests.Field(EnumEFormFieldType.TextField,EFormPatFields.ToDbLink(patFieldDefText.FieldName),"chess"),
				PatFieldLogicTests.Field(EnumEFormFieldType.DateField,EFormPatFields.ToDbLink(patFieldDefDate.FieldName),"2021-03-04"),
				PatFieldLogicTests.Field(EnumEFormFieldType.CheckBox,EFormPatFields.ToDbLink(patFieldDefCheck.FieldName),""),
				PatFieldLogicTests.Field(EnumEFormFieldType.TextField,EFormPatFields.ToDbLink(patFieldDefSame.FieldName),"unchanged"));
			eForm.EFormNum=123;
			List<EFormPatFieldImport> listImports=EFormPatFields.GetImports(eForm);
			Assert.AreEqual(4,listImports.Count);
			Assert.IsTrue(listImports.Single(x => x.FieldName==patFieldDefCheck.FieldName).IsClearing(),"unchecking needs the user's confirmation");
			List<string> listChanges=EFormPatFields.Import(patient.PatNum,listImports,"eForm #123 (Synthetic form)");
			Assert.AreEqual(3,listChanges.Count,"the unchanged field is skipped");
			List<PatField> listPatFields=PatFields.GetPatientData(patient.PatNum);
			Assert.AreEqual("chess",listPatFields.Single(x => x.FieldName==patFieldDefText.FieldName).FieldValue);
			Assert.AreEqual("3/4/2021",listPatFields.Single(x => x.FieldName==patFieldDefDate.FieldName).FieldValue);
			Assert.IsFalse(listPatFields.Exists(x => x.FieldName==patFieldDefCheck.FieldName),"an unchecked box has no row");
			Assert.AreEqual(1,listPatFields.Count(x => x.FieldName==patFieldDefText.FieldName),"updated, not added again");
			List<SecurityLog> listLogs=GetLogs(patient.PatNum,EnumPermType.PatientFieldEdit);
			Assert.AreEqual(3,listLogs.Count,"one PatientFieldEdit entry per changed field");
			Assert.IsTrue(listLogs.Exists(x => x.LogText.StartsWith("Edited patient field "+patFieldDefText.FieldName+" from eForm #123")));
			Assert.IsTrue(listLogs.Exists(x => x.LogText.StartsWith("Added patient field "+patFieldDefDate.FieldName)));
			Assert.IsTrue(listLogs.Exists(x => x.LogText.StartsWith("Deleted patient field "+patFieldDefCheck.FieldName)));
			//importing the same answers again changes nothing
			Assert.AreEqual(0,EFormPatFields.Import(patient.PatNum,EFormPatFields.GetImports(eForm),"again").Count);
		}

		[TestMethod]
		public void PatFields_RenameUpdatesTheLinks() {
			string nameOld=Unique("Old name");
			string nameNew=Unique("New name");
			PatFieldDef patFieldDef=InsertDef(nameOld,PatFieldType.Text);
			EFormDef eFormDef=new EFormDef() { Description=Unique("Rename form"),MaxWidth=450,SpaceBelowEachField=-1,SpaceToRightEachField=-1,DateTCreated=DateTime.Now };
			EFormDefs.Insert(eFormDef);
			EFormFieldDef eFormFieldDef=new EFormFieldDef() { EFormDefNum=eFormDef.EFormDefNum,FieldType=EnumEFormFieldType.TextField,
				DbLink=EFormPatFields.ToDbLink(nameOld),ValueLabel=nameOld,PickListVis="",PickListDb="",ConditionalParent="",ConditionalValue="",ReportableName="",
				FontScale=100,SpaceBelow=-1,SpaceToRight=-1 };
			EFormFieldDefs.Insert(eFormFieldDef);
			Patient patient=InsertPatient("Rename","Sample");
			EForm eForm=NewForm(patient.PatNum,PatFieldLogicTests.Field(EnumEFormFieldType.TextField,EFormPatFields.ToDbLink(nameOld),"x"));
			EForms.SaveNewEForms(new List<EForm>() { eForm });
			PatFields.Insert(new PatField() { PatNum=patient.PatNum,FieldName=nameOld,FieldValue="x" });
			patFieldDef.FieldName=nameNew;
			PatFieldDefs.Update(patFieldDef);
			PatFields.UpdateFieldName(nameNew,nameOld);//what Setup, Patient Field Defs does on a rename
			Assert.AreEqual(EFormPatFields.ToDbLink(nameNew),DataCore.GetScalar("SELECT DbLink FROM eformfielddef WHERE EFormFieldDefNum="
				+eFormFieldDef.EFormFieldDefNum));
			Assert.AreEqual(EFormPatFields.ToDbLink(nameNew),DataCore.GetScalar("SELECT DbLink FROM eformfield WHERE EFormNum="+eForm.EFormNum));
			Assert.AreEqual(EFormPatFields.ToDbLink(nameNew),EFormFieldDefs.GetFirstOrDefault(x => x.EFormFieldDefNum==eFormFieldDef.EFormFieldDefNum).DbLink,
				"the cache was refreshed");
			Assert.AreEqual("x",PatFields.GetPatientData(patient.PatNum).Single(x => x.FieldName==nameNew).FieldValue);
		}
		#endregion Patient fields (v25_4-eforms-patfields)

		#region Signature time, date submitted and the delete audit
		[TestMethod]
		public void SignatureTime_AndDateSubmitted_AreStored() {
			Patient patient=InsertPatient("Stored","Sample");
			EFormField eFormFieldSig=PatFieldLogicTests.Field(EnumEFormFieldType.SigBox,"","0signature");
			DateTime dateTimeSig=new DateTime(2026,10,24,15,16,17);
			eFormFieldSig.DateTimeSig=dateTimeSig;
			EForm eForm=NewForm(patient.PatNum,eFormFieldSig);
			DateTime dateTimeSubmitted=new DateTime(2026,10,24,15,20,5);
			eForm.DateTimeSubmitted=dateTimeSubmitted;
			EForms.SaveNewEForms(new List<EForm>() { eForm });
			EForm eFormRead=EForms.GetEForm(eForm.EFormNum);
			Assert.AreEqual(dateTimeSubmitted,eFormRead.DateTimeSubmitted);
			Assert.AreEqual(dateTimeSig,eFormRead.ListEFormFields[0].DateTimeSig);
			DataTable table=FormSubmittedDates.GetForPatient(patient.PatNum);
			Assert.AreEqual(FormSubmittedDates.ToDisplay(dateTimeSubmitted),FormSubmittedDates.GetDisplayForRow(table,0,eForm.EFormNum));
			Assert.AreEqual("",FormSubmittedDates.GetDisplayForRow(table,0,eForm.EFormNum+1000));
		}

		[TestMethod]
		public void DateSubmitted_OnSheets_ShowsInThePatientFormsList() {
			Patient patient=InsertPatient("Sheet","Sample");
			Sheet sheet=new Sheet();
			sheet.SheetType=SheetTypeEnum.PatientForm;
			sheet.PatNum=patient.PatNum;
			sheet.DateTimeSheet=DateTime.Now;
			sheet.Description="Synthetic sheet";
			sheet.FontName="Arial";
			sheet.FontSize=10;
			sheet.Width=850;
			sheet.Height=1100;
			DateTime dateTimeSubmitted=new DateTime(2026,10,23,10,0,0);
			sheet.DateTimeSubmitted=dateTimeSubmitted;
			sheet.SheetFields=new List<SheetField>();
			sheet.Parameters=new List<SheetParameter>();
			Sheets.Insert(sheet);
			Sheet sheetNotSubmitted=new Sheet() { SheetType=SheetTypeEnum.PatientForm,PatNum=patient.PatNum,DateTimeSheet=DateTime.Now,Description="Other",
				FontName="Arial",FontSize=10,Width=850,Height=1100,SheetFields=new List<SheetField>(),Parameters=new List<SheetParameter>() };
			Sheets.Insert(sheetNotSubmitted);
			Assert.AreEqual(dateTimeSubmitted,Sheets.GetSheet(sheet.SheetNum).DateTimeSubmitted);
			DataTable table=FormSubmittedDates.GetForPatient(patient.PatNum);
			Assert.AreEqual(1,table.Rows.Count,"only submitted forms");
			Assert.AreEqual(FormSubmittedDates.ToDisplay(dateTimeSubmitted),FormSubmittedDates.GetDisplayForRow(table,sheet.SheetNum,0));
			Assert.AreEqual("",FormSubmittedDates.GetDisplayForRow(table,sheetNotSubmitted.SheetNum,0));
		}

		[TestMethod]
		public void DeleteAudit_LogsSavedPatientFormsOnly() {
			Patient patient=InsertPatient("Delete","Sample");
			EForm eForm=NewForm(patient.PatNum,PatFieldLogicTests.Field(EnumEFormFieldType.TextField,"FName","Delete"));
			eForm.Description="Consent to delete";
			eForm.DateTimeSubmitted=new DateTime(2026,10,22,9,0,0);
			EForms.SaveNewEForms(new List<EForm>() { eForm });
			eForm.IsNew=false;
			//What FrmEFormFillEdit's Delete button does:
			EFormFields.DeleteForForm(eForm.EFormNum);
			EForms.Delete(eForm.EFormNum,eForm.PatNum);
			EForms.MakeDeleteLogEntry(eForm);
			Assert.IsNull(EForms.GetEForm(eForm.EFormNum));
			List<SecurityLog> listLogs=GetLogs(patient.PatNum,EnumPermType.EFormDelete);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.StartsWith(listLogs[0].LogText,"eForm Consent to delete dated ");
			StringAssert.Contains(listLogs[0].LogText,"(ID "+eForm.EFormNum+") deleted, submitted 10/22/2026 9:00 AM");
			//a new form that was never saved, and a form with no patient, are not logged
			EForm eFormNew=NewForm(patient.PatNum);
			EForms.MakeDeleteLogEntry(eFormNew);
			EForm eFormNoPatient=new EForm() { EFormNum=99999,PatNum=0,Description="x" };
			EForms.MakeDeleteLogEntry(eFormNoPatient);
			Assert.AreEqual(1,GetLogs(patient.PatNum,EnumPermType.EFormDelete).Count);
			Assert.AreEqual(0,GetLogs(0,EnumPermType.EFormDelete).Count(x => x.LogText.Contains("ID 99999")));
		}
		#endregion Signature time, date submitted and the delete audit

		#region Multiple import (v25_4-eforms-multi-import)
		private static string WriteExport(string fileName,EFormDef eFormDef) {
			string path=Path.Combine(_dirFiles,fileName);
			XmlSerializer xmlSerializer=new XmlSerializer(typeof(EFormDef));
			using(StreamWriter streamWriter=new StreamWriter(path)) {
				xmlSerializer.Serialize(streamWriter,eFormDef);
			}
			return path;
		}

		private static EFormDef ExportedDef(string description,int countFields) {
			EFormDef eFormDef=new EFormDef() { Description=description,MaxWidth=450,SpaceBelowEachField=-1,SpaceToRightEachField=-1,FormType=EnumEFormType.Consent };
			eFormDef.ListEFormFieldDefs=new List<EFormFieldDef>();
			for(int i=0;i<countFields;i++) {
				eFormDef.ListEFormFieldDefs.Add(new EFormFieldDef() { FieldType=EnumEFormFieldType.TextField,DbLink="",ValueLabel="Question "+(i+1),PickListVis="",
					PickListDb="",ConditionalParent="",ConditionalValue="",ReportableName="",FontScale=100,SpaceBelow=-1,SpaceToRight=-1 });
			}
			return eFormDef;
		}

		[TestMethod]
		public void MultiImport_NameOrderFailuresClashesAndLogs() {
			string descExisting=Unique("Existing consent");
			EFormDef eFormDefExisting=new EFormDef() { Description=descExisting,MaxWidth=450,SpaceBelowEachField=-1,SpaceToRightEachField=-1,DateTCreated=DateTime.Now };
			EFormDefs.Insert(eFormDefExisting);
			EFormDefs.RefreshCache();
			string descNew=Unique("Fresh form");
			List<string> listPaths=new List<string>() {
				WriteExport("c_clash.xml",ExportedDef(descExisting,2)),
				WriteExport("a_new.xml",ExportedDef(descNew,3)),
				WriteExport("b_clash_again.xml",ExportedDef(descExisting,1))
			};
			string pathBad=Path.Combine(_dirFiles,"d_bad.xml");
			File.WriteAllText(pathBad,"<NotAnEForm/>");
			listPaths.Add(pathBad);
			string pathNoDescription=WriteExport("e_nodesc.xml",ExportedDef("  ",1));
			listPaths.Add(pathNoDescription);
			long countLogsBefore=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.EFormEdit));
			EFormDefImportResult result=EFormDefImport.ImportFiles(listPaths);
			Assert.AreEqual(3,result.ListEFormDefsImported.Count);
			Assert.AreEqual(2,result.ListFailures.Count);
			StringAssert.StartsWith(result.ListFailures[0],"d_bad.xml: invalid file format");
			StringAssert.StartsWith(result.ListFailures[1],"e_nodesc.xml: the eForm has no description");
			//name order: a, b, c
			Assert.AreEqual(descNew,result.ListEFormDefsImported[0].Description);
			Assert.AreEqual(descExisting+" (2)",result.ListEFormDefsImported[1].Description);
			Assert.AreEqual(descExisting+" (3)",result.ListEFormDefsImported[2].Description);
			Assert.AreEqual(3,EFormFieldDefs.GetForEFormDef(result.ListEFormDefsImported[0].EFormDefNum).Count);
			Assert.IsTrue(EFormDefs.GetDeepCopy().Exists(x => x.Description==descExisting+" (3)"),"caches refreshed");
			long countLogsAfter=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.EFormEdit));
			Assert.AreEqual(3,countLogsAfter-countLogsBefore,"one EFormEdit entry per imported form");
			StringAssert.Contains(result.GetSummary(),"Imported: 3");
			StringAssert.Contains(result.GetSummary(),"Failed: 2");
		}

		[TestMethod]
		public void MultiImport_HelpersWithoutFiles() {
			CollectionAssert.AreEqual(new[] { @"C:\x\A.xml",@"C:\y\b.xml",@"C:\x\C.xml" },
				EFormDefImport.SortByFileName(new List<string>() { @"C:\x\C.xml",@"C:\y\b.xml",@"C:\x\A.xml","" }));
			Assert.AreEqual("Consent",EFormDefImport.GetUniqueDescription("Consent",new List<string>() { "Other" }));
			Assert.AreEqual("Consent (2)",EFormDefImport.GetUniqueDescription("Consent",new List<string>() { "consent" }));
			Assert.AreEqual("Consent (3)",EFormDefImport.GetUniqueDescription("Consent",new List<string>() { "Consent","Consent (2)" }));
			Assert.IsNull(EFormDefImport.Deserialize("not xml",out string error));
			Assert.AreEqual("invalid file format",error);
			EFormDef eFormDefBadField=ExportedDef("Bad field",1);
			eFormDefBadField.ListEFormFieldDefs[0].FieldType=(EnumEFormFieldType)99;
			StringAssert.StartsWith(EFormDefImport.Validate(eFormDefBadField),"field 1 has an unknown type");
			Assert.AreEqual("",EFormDefImport.Validate(ExportedDef("Good",0)));
		}
		#endregion Multiple import (v25_4-eforms-multi-import)
	}
}
