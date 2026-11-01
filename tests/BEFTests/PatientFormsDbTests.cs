// Database tests for the Patient Forms window improvements (package B-EF) on a scratch MariaDB database upgraded to the Open Dental 25.3.48
// schema by the fork migration runner. Synthetic patients, forms and audit entries only. Skipped (inconclusive) when the local MariaDB does not
// answer, or when BEF_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BEFTests {
	[TestClass]
	public class PatientFormsDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("forms");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh25_3(DB_NAME);
			Security.CurUser=Userods.GetFirstOrDefault(x => true)??new Userod() { UserNum=1,UserName="Admin" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("BEF_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless; the name is unique to this run, and later runs drop stale ones.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		///<summary>Sets every null string field to "", as the windows do before saving, so inserts never write NULL into NOT NULL text columns.</summary>
		private static T BlankStrings<T>(T obj) {
			foreach(FieldInfo fieldInfo in typeof(T).GetFields(BindingFlags.Public|BindingFlags.Instance)) {
				if(fieldInfo.FieldType==typeof(string) && fieldInfo.GetValue(obj)==null) {
					fieldInfo.SetValue(obj,"");
				}
			}
			return obj;
		}

		private static Patient InsertPatient(string fName,string lName) {
			Patient patient=BlankStrings(new Patient());
			patient.FName=fName;
			patient.LName=lName;
			patient.PatStatus=PatientStatus.Patient;
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			Patients.Update(patient,patientOld);
			return patient;
		}

		private static long InsertSheet(long patNum,SheetTypeEnum sheetType,string description,byte showInTerminal=0,bool isWebForm=false) {
			Sheet sheet=BlankStrings(new Sheet());
			sheet.PatNum=patNum;
			sheet.SheetType=sheetType;
			sheet.Description=description;
			sheet.FontName="Arial";
			sheet.FontSize=9;
			sheet.Width=850;
			sheet.Height=1100;
			sheet.DateTimeSheet=new DateTime(2026,10,1,9,0,0);
			sheet.DateTSheetEdited=sheet.DateTimeSheet;
			sheet.ShowInTerminal=showInTerminal;
			sheet.IsWebForm=isWebForm;
			return Sheets.Insert(sheet);
		}

		private static long InsertEForm(long patNum,string description,int statusStored) {
			EForm eForm=BlankStrings(new EForm());
			eForm.PatNum=patNum;
			eForm.Description=description;
			eForm.DateTimeShown=new DateTime(2026,10,2,10,0,0);
			eForm.DateTEdited=eForm.DateTimeShown;
			eForm.MaxWidth=450;
			eForm.Status=EnumEFormStatus.None;
			long eFormNum=EForms.Insert(eForm);
			//Write the number directly so values removed from the enum (3 Imported, 4 UnmatchedToPatient) can be stored like old data.
			DataCore.NonQ("UPDATE eform SET Status="+statusStored+" WHERE EFormNum="+eFormNum);
			return eFormNum;
		}

		private static string Status(DataTable table,string column,long num) {
			DataRow dataRow=table.Rows.OfType<DataRow>().Single(x => x[column].ToString()==num.ToString() && (column=="DocNum" || x["DocNum"].ToString()=="0"));
			return dataRow[PatientFormsList.COLUMN_STATUS].ToString();
		}

		private static string Importable(DataTable table,string column,long num) {
			DataRow dataRow=table.Rows.OfType<DataRow>().Single(x => x[column].ToString()==num.ToString() && (column=="DocNum" || x["DocNum"].ToString()=="0"));
			return dataRow[PatientFormsList.COLUMN_IMPORTABLE].ToString();
		}

		[TestMethod]
		public void GetTable_StatusColumnFromFlagsStatusAndAuditTrail() {
			Prefs.UpdateBool(PrefName.PatientFormsShowConsent,true);
			Cache.Refresh(InvalidType.Prefs);
			Patient patient=InsertPatient("Ada","Formstatus");
			Patient patientOther=InsertPatient("Bo","Otherpatient");
			long sheetKiosk=InsertSheet(patient.PatNum,SheetTypeEnum.PatientForm,"Registration",showInTerminal:1);
			long sheetWeb=InsertSheet(patient.PatNum,SheetTypeEnum.MedicalHistory,"Medical History",isWebForm:true);
			long sheetImported=InsertSheet(patient.PatNum,SheetTypeEnum.PatientForm,"Registration (adult)");
			long sheetSigned=InsertSheet(patient.PatNum,SheetTypeEnum.PatientForm,"Signed only");
			long sheetConsent=InsertSheet(patient.PatNum,SheetTypeEnum.Consent,"Extraction consent");
			long sheetOtherPatient=InsertSheet(patientOther.PatNum,SheetTypeEnum.PatientForm,"Other patient");
			//Keep eForm numbers apart from sheet numbers, so an entry naming one can't be read as naming the other by chance.
			DataCore.NonQ("ALTER TABLE eform AUTO_INCREMENT=100000");
			long eFormNone=InsertEForm(patient.PatNum,"Office eForm",0);
			long eFormWaiting=InsertEForm(patient.PatNum,"Queued eForm",1);
			long eFormImported=InsertEForm(patient.PatNum,"Filled eForm",2);
			long eFormLegacy=InsertEForm(patient.PatNum,"Old imported eForm",3);
			//Audit entries, written the way the Import window writes them.
			SecurityLogs.MakeLogEntry(EnumPermType.SheetEdit,patient.PatNum,PatientFormsList.GetImportLogTextSheet(sheetImported,"Registration (adult)"),
				sheetImported,DateTime.MinValue);
			SecurityLogs.MakeLogEntry(EnumPermType.EFormImport,patient.PatNum,PatientFormsList.GetImportLogTextEForm(eFormImported,"Filled eForm"),
				eFormImported,DateTime.MinValue);
			//Entries that must not count: a signature on a sheet, pending insurance, an eForm import entry whose FKey is a sheet's number (only
			//SheetEdit entries name sheets), and the other patient's import.
			SecurityLogs.MakeLogEntry(EnumPermType.SheetEdit,patient.PatNum,"Signed Signed only by Admin",sheetSigned,DateTime.MinValue);
			SecurityLogs.MakeLogEntry(EnumPermType.EFormImport,patient.PatNum,"Pending insurance created from eForm #"+eFormNone+" (Office eForm): x",
				eFormNone,DateTime.MinValue);
			SecurityLogs.MakeLogEntry(EnumPermType.EFormImport,patient.PatNum,PatientFormsList.GetImportLogTextEForm(sheetKiosk,"Registration"),
				sheetKiosk,DateTime.MinValue);
			SecurityLogs.MakeLogEntry(EnumPermType.SheetEdit,patientOther.PatNum,PatientFormsList.GetImportLogTextSheet(sheetOtherPatient,"Other patient"),
				sheetOtherPatient,DateTime.MinValue);
			//A scanned form in an image category flagged for Patient Forms.
			Def defCat=BlankStrings(new Def());
			defCat.Category=DefCat.ImageCats;
			defCat.ItemName="Synthetic scanned forms";
			defCat.ItemValue="F";
			defCat.ItemOrder=90;
			Defs.Insert(defCat);
			Cache.Refresh(InvalidType.Defs);
			Document document=BlankStrings(new Document());
			document.PatNum=patient.PatNum;
			document.DocCategory=defCat.DefNum;
			document.Description="Scanned registration";
			document.FileName="synthetic.pdf";
			document.DateCreated=new DateTime(2026,10,3,8,0,0);
			long docNum=Documents.Insert(document);
			DataTable table=PatientFormsList.GetTable(patient.PatNum);
			Assert.AreEqual(10,table.Rows.Count,"The patient's 5 sheets (not the other patient's), 4 eForms and 1 document: "
				+string.Join("; ",table.Rows.OfType<DataRow>().Select(x => x["description"])));
			Assert.AreEqual("Kiosk",Status(table,"SheetNum",sheetKiosk));
			Assert.AreEqual("Submitted online",Status(table,"SheetNum",sheetWeb));
			Assert.AreEqual("Imported",Status(table,"SheetNum",sheetImported));
			Assert.AreEqual("",Status(table,"SheetNum",sheetSigned));
			Assert.AreEqual("",Status(table,"SheetNum",sheetConsent));
			Assert.AreEqual("None",Status(table,"EFormNum",eFormNone));
			Assert.AreEqual("Waiting for patient",Status(table,"EFormNum",eFormWaiting));
			Assert.AreEqual("Filled, Imported",Status(table,"EFormNum",eFormImported));
			Assert.AreEqual("Filled",Status(table,"EFormNum",eFormLegacy),"Stored status 3 loads as Filled (Open Dental 25.2).");
			Assert.AreEqual("",Status(table,"DocNum",docNum));
			Assert.AreEqual("1",Importable(table,"SheetNum",sheetKiosk));
			Assert.AreEqual("1",Importable(table,"SheetNum",sheetWeb));
			Assert.AreEqual("0",Importable(table,"SheetNum",sheetConsent),"Consent sheets are not imported.");
			Assert.AreEqual("1",Importable(table,"EFormNum",eFormWaiting));
			Assert.AreEqual("0",Importable(table,"DocNum",docNum));
			Assert.IsTrue(PatientFormsList.HasKioskForms(table));
			Assert.AreEqual(6,PatientFormsList.Filter(table,EnumPatientFormsShow.Sheets).Rows.Count,"5 sheets and the document under Sheets.");
			Assert.AreEqual(4,PatientFormsList.Filter(table,EnumPatientFormsShow.EForms).Rows.Count);
			Prefs.UpdateBool(PrefName.PatientFormsShowConsent,false);
			Cache.Refresh(InvalidType.Prefs);
		}

		[TestMethod]
		public void GetStatusTable_PatientWithNoForms() {
			Patient patient=InsertPatient("Cy","Noforms");
			Assert.AreEqual(0,PatientFormsList.GetStatusTable(patient.PatNum).Rows.Count);
			Assert.AreEqual(0,PatientFormsList.GetTable(patient.PatNum).Rows.Count);
		}

		[TestMethod]
		public void View_SavedPerUserInUserOdPref() {
			long userNum=Security.CurUser.UserNum;
			long userNumOther=userNum+100000;//no such user is needed: the row is keyed by number only
			UserOdPrefs.DeleteManyForUserAndFkeyType(userNum,UserOdFkeyType.PatientFormsView);
			UserOdPrefs.DeleteManyForUserAndFkeyType(userNumOther,UserOdFkeyType.PatientFormsView);
			UserOdPrefs.RefreshCache();
			PatientFormsList.GetViewForUser(userNum,out EnumPatientFormsShow show,out EnumPatientFormsSort sort);
			Assert.AreEqual(EnumPatientFormsShow.Both,show,"Default before anything is saved.");
			Assert.AreEqual(EnumPatientFormsSort.DateTime,sort);
			Assert.IsTrue(PatientFormsList.SaveViewForUser(userNum,EnumPatientFormsShow.EForms,EnumPatientFormsSort.DescriptionDateTime));
			PatientFormsList.GetViewForUser(userNum,out show,out sort);
			Assert.AreEqual(EnumPatientFormsShow.EForms,show);
			Assert.AreEqual(EnumPatientFormsSort.DescriptionDateTime,sort);
			Assert.IsFalse(PatientFormsList.SaveViewForUser(userNum,EnumPatientFormsShow.EForms,EnumPatientFormsSort.DescriptionDateTime),"Unchanged.");
			Assert.IsTrue(PatientFormsList.SaveViewForUser(userNum,EnumPatientFormsShow.Sheets,EnumPatientFormsSort.DateTime));
			Assert.IsTrue(PatientFormsList.SaveViewForUser(userNumOther,EnumPatientFormsShow.EForms,EnumPatientFormsSort.DateTime));
			string where=" FROM userodpref WHERE FkeyType="+(int)UserOdFkeyType.PatientFormsView+" AND UserNum="+userNum;
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*)"+where),"One row per user, updated in place.");
			Assert.AreEqual("Show=1;Sort=0",DataCore.GetScalar("SELECT ValueString"+where));
			Assert.AreEqual("114",DataCore.GetScalar("SELECT FkeyType"+where),"The fork-only number fits the tinyint column.");
			PatientFormsList.GetViewForUser(userNum,out show,out sort);
			Assert.AreEqual(EnumPatientFormsShow.Sheets,show,"Another user's choice does not change this user's.");
			PatientFormsList.GetViewForUser(userNumOther,out show,out sort);
			Assert.AreEqual(EnumPatientFormsShow.EForms,show);
			Assert.IsFalse(PatientFormsList.SaveViewForUser(0,EnumPatientFormsShow.EForms,EnumPatientFormsSort.DateTime),"No user, nothing saved.");
		}
	}
}
