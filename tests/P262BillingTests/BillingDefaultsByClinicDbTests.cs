// Billing Defaults per clinic (Open Dental 26.2 item "Reconfigured Billing Defaults and added ability to set some settings per clinic"; package
// P262-BILLING): where the settings are kept, what a clinic without settings of its own gets, what clinics off means, and the readers (statement
// e-mails, their body type and autograph, Billing Options' General Message). Runs on a scratch database built from the seed plus the fork
// migrations, with synthetic clinics, patients and statements only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262BillingTests {
	[TestClass]
	public class BillingDefaultsByClinicDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("clinics");
		private static string _skipReason;
		private static long _clinicNumA;
		private static long _clinicNumB;
		private const string PRACTICE_NOTE="Practice general message";
		private const string PRACTICE_SUBJECT="Practice statement [PatNum]";
		private const string PRACTICE_BODY="Practice body for [nameF]";
		private const string PRACTICE_INVOICE_NOTE="Practice invoice note";
		private const string MASTER_TEMPLATE="<html><head><title>Statement</title></head>@@@body@@@</html>";

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateMigrated(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			_clinicNumA=Clinics.Insert(new Clinic { Abbr="P262A",Description="Synthetic billing clinic A" });
			_clinicNumB=Clinics.Insert(new Clinic { Abbr="P262B",Description="Synthetic billing clinic B" });
			Clinics.RefreshCache();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("P262BILLING_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateBool(PrefName.EasyNoClinics,false);
			Prefs.UpdateString(PrefName.EmailMasterTemplate,MASTER_TEMPLATE);
			Prefs.UpdateString(PrefName.BillingDefaultsNote,PRACTICE_NOTE);
			Prefs.UpdateString(PrefName.BillingEmailSubject,PRACTICE_SUBJECT);
			Prefs.UpdateString(PrefName.BillingEmailBodyText,PRACTICE_BODY);
			Prefs.UpdateInt(PrefName.BillingEmailType,(int)EmailType.Regular);
			Prefs.UpdateBool(PrefName.BillingEmailIncludeAutograph,false);
			Prefs.UpdateString(PrefName.BillingDefaultsInvoiceNote,PRACTICE_INVOICE_NOTE);
			DataCore.NonQ("DELETE FROM clinicpref");
			Cache.Refresh(InvalidType.Prefs);
			Clinics.RefreshCache();
			ClinicPrefs.RefreshCache();
			Assert.IsTrue(PrefC.HasClinicsEnabled,"The synthetic office uses clinics.");
		}

		#region Helpers
		private static int CountClinicRows(long clinicNum) {
			string prefNames=string.Join(",",BillingDefaultsByClinic.ListPrefNames.Select(x => "'"+POut.String(x.ToString())+"'"));
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref WHERE ClinicNum="+POut.Long(clinicNum)+" AND PrefName IN("+prefNames+")"));
		}

		private static BillingDefaultsSettings SaveOwnSettings(long clinicNum,string tag,EmailType emailType=EmailType.Regular,bool includeAutograph=true) {
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(clinicNum);
			settings.UsePracticeDefaults=false;
			settings.GeneralMessage=tag+" general message";
			settings.EmailSubject=tag+" subject [PatNum]";
			settings.EmailBody=tag+" body for [nameF]";
			settings.EmailType=emailType;
			settings.IncludeAutograph=includeAutograph;
			settings.InvoiceNote=tag+" invoice note";
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settings));
			return settings;
		}

		private static Patient CreatePatient(string fName,long clinicNum) {
			Patient patient=new Patient();
			patient.LName="Billing"+DateTime.Now.Ticks.ToString().Substring(10);
			patient.FName=fName;
			patient.PatStatus=PatientStatus.Patient;
			patient.Email="guarantor@example.test";
			patient.Address="1 Test Street";
			patient.City="Testville";
			patient.State="OR";
			patient.Zip="97000";
			patient.ClinicNum=clinicNum;
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.Birthdate=new DateTime(1980,1,1);
			patient.PatNum=OpenPmsDentalBusiness.Crud.PatientCrud.Insert(patient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			OpenPmsDentalBusiness.Crud.PatientCrud.Update(patient,patientOld);
			return patient;
		}

		private static Statement InsertStatement(Patient patient,string emailSubject="",string emailBody="") {
			Statement statement=new Statement();
			statement.PatNum=patient.PatNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.Today.AddMonths(-1);
			statement.DateRangeTo=DateTime.Today;
			statement.Mode_=StatementMode.Email;
			statement.StatementType=StmtType.NotSet;
			statement.Note="";
			statement.NoteBold="";
			statement.IsBalValid=true;
			statement.BalTotal=50;
			statement.InsEst=0;
			statement.EmailSubject=emailSubject;
			statement.EmailBody=emailBody;
			Statements.Insert(statement);
			return statement;
		}

		private static EmailMessage BuildEmail(Statement statement,Patient patient) {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.SenderAddress="billing@example.test";
			emailAddress.EmailUsername="billing@example.test";
			return Statements.GetEmailMessageForStatement(statement,patient,emailAddress);
		}
		#endregion Helpers

		#region Storage
		[TestMethod]
		public void PreferenceRows_EverySettingHasItsRow() {
			//The practice defaults and the electronic billing settings stay in the preference rows they had before 26.2: no row is new.
			foreach(PrefName prefName in BillingDefaultsByClinic.ListPrefNames.Concat(EbillSettings.ListPrefNames)) {
				Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName.ToString())+"'"),
					prefName.ToString());
			}
		}

		[TestMethod]
		public void ClinicWithoutRows_UsesThePracticeDefaults() {
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsTrue(settings.UsePracticeDefaults);
			Assert.AreEqual(PRACTICE_NOTE,settings.GeneralMessage);
			Assert.AreEqual(PRACTICE_SUBJECT,settings.EmailSubject);
			Assert.AreEqual(PRACTICE_BODY,settings.EmailBody);
			Assert.AreEqual(EmailType.Regular,settings.EmailType);
			Assert.IsFalse(settings.IncludeAutograph);
			Assert.AreEqual(PRACTICE_INVOICE_NOTE,settings.InvoiceNote);
			Assert.AreEqual(PRACTICE_SUBJECT,BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,_clinicNumA));
			Assert.AreEqual(PRACTICE_INVOICE_NOTE,BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsInvoiceNote,_clinicNumA));
		}

		[TestMethod]
		public void PracticeSettings_NeverUsePracticeDefaults_AndSaveToThePreferenceRows() {
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(0);
			Assert.AreEqual(0,settings.ClinicNum);
			Assert.IsFalse(settings.UsePracticeDefaults);
			settings.EmailSubject="New practice subject";
			settings.EmailType=EmailType.Html;
			settings.IncludeAutograph=true;
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settings));
			Assert.AreEqual("New practice subject",PrefC.GetString(PrefName.BillingEmailSubject));
			Assert.AreEqual((int)EmailType.Html,PrefC.GetInt(PrefName.BillingEmailType));
			Assert.IsTrue(PrefC.GetBool(PrefName.BillingEmailIncludeAutograph));
			Assert.AreEqual(PRACTICE_NOTE,PrefC.GetString(PrefName.BillingDefaultsNote),"Unchanged.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref"),"No clinic row for the practice defaults.");
			Assert.IsFalse(BillingDefaultsByClinic.SaveSettings(settings),"Nothing changes the second time.");
		}

		[TestMethod]
		public void ClinicOwnSettings_WriteAllSixRows_AndOnlyForThatClinic() {
			SaveOwnSettings(_clinicNumA,"Clinic A",EmailType.RawHtml,includeAutograph:true);
			Assert.AreEqual(6,CountClinicRows(_clinicNumA));
			Assert.AreEqual(0,CountClinicRows(_clinicNumB));
			Assert.IsTrue(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsFalse(settings.UsePracticeDefaults);
			Assert.AreEqual("Clinic A general message",settings.GeneralMessage);
			Assert.AreEqual("Clinic A subject [PatNum]",settings.EmailSubject);
			Assert.AreEqual("Clinic A body for [nameF]",settings.EmailBody);
			Assert.AreEqual(EmailType.RawHtml,settings.EmailType);
			Assert.IsTrue(settings.IncludeAutograph);
			Assert.AreEqual("Clinic A invoice note",settings.InvoiceNote);
			Assert.AreEqual("Clinic A invoice note",BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsInvoiceNote,_clinicNumA));
			Assert.IsTrue(BillingDefaultsByClinic.GetBool(PrefName.BillingEmailIncludeAutograph,_clinicNumA));
			Assert.AreEqual(EmailType.RawHtml,BillingDefaultsByClinic.GetEmailType(_clinicNumA));
			//Clinic B and the practice defaults are untouched.
			Assert.AreEqual(PRACTICE_INVOICE_NOTE,BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsInvoiceNote,_clinicNumB));
			Assert.IsFalse(BillingDefaultsByClinic.GetBool(PrefName.BillingEmailIncludeAutograph,_clinicNumB));
			Assert.AreEqual(PRACTICE_SUBJECT,PrefC.GetString(PrefName.BillingEmailSubject));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.GetBillingDefaultsEmailType());
			//The stored text is what the preference rows hold for the same values.
			Assert.AreEqual("2",DataCore.GetScalar("SELECT ValueString FROM clinicpref WHERE ClinicNum="+POut.Long(_clinicNumA)+" AND PrefName='BillingEmailType'"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM clinicpref WHERE ClinicNum="+POut.Long(_clinicNumA)
				+" AND PrefName='BillingEmailIncludeAutograph'"));
			Assert.IsFalse(BillingDefaultsByClinic.SaveSettings(BillingDefaultsByClinic.GetSettings(_clinicNumA)),"Nothing changes the second time.");
		}

		[TestMethod]
		public void ClinicBackToPracticeDefaults_RemovesItsRowsOnly() {
			SaveOwnSettings(_clinicNumA,"Clinic A");
			SaveOwnSettings(_clinicNumB,"Clinic B");
			//A Billing Options filter default of clinic A (not a Billing Defaults setting) stays.
			ClinicPrefs.InsertPref(PrefName.BillingIncludeChanged,_clinicNumA,"1");
			ClinicPrefs.RefreshCache();
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			settings.UsePracticeDefaults=true;
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settings));
			Assert.AreEqual(0,CountClinicRows(_clinicNumA));
			Assert.AreEqual(6,CountClinicRows(_clinicNumB));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref WHERE ClinicNum="+POut.Long(_clinicNumA)+" AND PrefName='BillingIncludeChanged'"));
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			Assert.AreEqual(PRACTICE_SUBJECT,BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,_clinicNumA));
			Assert.AreEqual("Clinic B subject [PatNum]",BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,_clinicNumB));
			Assert.IsFalse(BillingDefaultsByClinic.SaveSettings(settings),"Nothing left to remove.");
		}

		[TestMethod]
		public void ClinicsOff_EveryReaderGetsThePracticeDefaults() {
			SaveOwnSettings(_clinicNumA,"Clinic A",EmailType.Html);
			Prefs.UpdateBool(PrefName.EasyNoClinics,true);
			Cache.Refresh(InvalidType.Prefs);
			try {
				Assert.IsFalse(PrefC.HasClinicsEnabled);
				Assert.AreEqual(0,BillingDefaultsByClinic.GetSettingsClinicNum(_clinicNumA));
				foreach(PrefName prefName in BillingDefaultsByClinic.ListPrefNames) {
					Assert.AreEqual(PrefC.GetString(prefName),BillingDefaultsByClinic.GetString(prefName,_clinicNumA),prefName.ToString());
				}
				Assert.AreEqual(EmailType.Regular,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumA));
				Patient patient=CreatePatient("Offclinics",_clinicNumA);
				EmailMessage emailMessage=BuildEmail(InsertStatement(patient),patient);
				Assert.AreEqual("Practice statement "+patient.PatNum,emailMessage.Subject);
				Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
				//The rows are kept for when clinics are turned on again; Billing Defaults still shows what the clinic saved.
				Assert.AreEqual(6,CountClinicRows(_clinicNumA));
				Assert.IsTrue(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			}
			finally {
				Prefs.UpdateBool(PrefName.EasyNoClinics,false);
				Cache.Refresh(InvalidType.Prefs);
			}
		}

		[TestMethod]
		public void GeneralMessageRowFromBillingOptions_IsTheClinicsOwn_AndTheOthersFallBack() {
			//Billing Options saves a clinic's filter defaults with its General Message (clinicpref BillingDefaultsNote). Billing Defaults shows that
			//message as the clinic's own, and the practice value of every setting the clinic has no row for.
			ClinicPrefs.InsertPref(PrefName.BillingDefaultsNote,_clinicNumA,"Saved in Billing Options");
			ClinicPrefs.RefreshCache();
			Assert.IsTrue(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsFalse(settings.UsePracticeDefaults);
			Assert.AreEqual("Saved in Billing Options",settings.GeneralMessage);
			Assert.AreEqual(PRACTICE_SUBJECT,settings.EmailSubject);
			Assert.AreEqual(PRACTICE_BODY,BillingDefaultsByClinic.GetString(PrefName.BillingEmailBodyText,_clinicNumA));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumA));
		}

		[TestMethod]
		public void BillingOptions_ReadsTheGeneralMessageBillingDefaultsSaved() {
			SaveOwnSettings(_clinicNumA,"Clinic A");
			Assert.AreEqual("Clinic A general message",new ClinicBillingOptions(_clinicNumA).GetPrefValue(PrefName.BillingDefaultsNote));
			Assert.AreEqual(PRACTICE_NOTE,new ClinicBillingOptions(_clinicNumB).GetPrefValue(PrefName.BillingDefaultsNote));
			Assert.AreEqual(PRACTICE_NOTE,new ClinicBillingOptions(0).GetPrefValue(PrefName.BillingDefaultsNote));
			Assert.AreEqual(1,ClinicPrefs.GetWhere(x => x.ClinicNum==_clinicNumA && x.PrefName==PrefName.BillingDefaultsNote).Count,
				"One row: Billing Options and Billing Defaults share it.");
		}

		[TestMethod]
		public void GetString_RefusesAPreferenceThatIsNotPerClinic() {
			Assert.IsFalse(BillingDefaultsByClinic.IsPerClinic(PrefName.BillingDefaultsLastDays));
			foreach(PrefName prefName in new[] { PrefName.BillingDefaultsLastDays,PrefName.BillingUseElectronic }) {
				try {
					BillingDefaultsByClinic.GetString(prefName,_clinicNumA);
					Assert.Fail("Read a clinic value of "+prefName);
				}
				catch(ArgumentException) {
					//Expected.
				}
			}
		}
		#endregion Storage

		#region Readers
		[TestMethod]
		public void StatementEmail_UsesTheSubjectBodyAndTypeOfThePatientsClinic() {
			SaveOwnSettings(_clinicNumA,"Clinic A",EmailType.Html);
			Patient patientA=CreatePatient("Clinica",_clinicNumA);
			EmailMessage emailMessageA=BuildEmail(InsertStatement(patientA),patientA);
			Assert.AreEqual("Clinic A subject "+patientA.PatNum,emailMessageA.Subject);
			StringAssert.Contains(emailMessageA.BodyText,"Clinic A body for Clinica");
			Assert.AreEqual(EmailType.Html,emailMessageA.HtmlType,"The clinic's own body type.");
			Patient patientB=CreatePatient("Clinicb",_clinicNumB);
			EmailMessage emailMessageB=BuildEmail(InsertStatement(patientB),patientB);
			Assert.AreEqual("Practice statement "+patientB.PatNum,emailMessageB.Subject);
			StringAssert.Contains(emailMessageB.BodyText,"Practice body for Clinicb");
			Assert.AreEqual(EmailType.Regular,emailMessageB.HtmlType);
			Patient patientNoClinic=CreatePatient("Noclinic",0);
			EmailMessage emailMessageNoClinic=BuildEmail(InsertStatement(patientNoClinic),patientNoClinic);
			Assert.AreEqual("Practice statement "+patientNoClinic.PatNum,emailMessageNoClinic.Subject);
		}

		[TestMethod]
		public void StatementEmail_OwnSubjectAndBodyOfTheStatementWin() {
			//A statement that got its subject and body from a dunning message keeps them, whatever its clinic has.
			SaveOwnSettings(_clinicNumA,"Clinic A",EmailType.Html);
			Patient patientA=CreatePatient("Dunned",_clinicNumA);
			Statement statement=InsertStatement(patientA,"Past due [PatNum]","Please pay, [nameF].");
			EmailMessage emailMessage=BuildEmail(statement,patientA);
			Assert.AreEqual("Past due "+patientA.PatNum,emailMessage.Subject);
			StringAssert.Contains(emailMessage.BodyText,"Please pay, Dunned.");
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType,"No dunning message matches: plain text, as in 26.1.");
		}

		[TestMethod]
		public void EmailType_PerClinic() {
			Prefs.UpdateInt(PrefName.BillingEmailType,(int)EmailType.RawHtml);
			Cache.Refresh(InvalidType.Prefs);
			SaveOwnSettings(_clinicNumA,"Clinic A",EmailType.Html);
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumA));
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumB));
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.GetBillingDefaultsEmailType(0));
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.GetBillingDefaultsEmailType());
			//A stored number that is not an EmailType is plain text, as for the preference.
			DataCore.NonQ("UPDATE clinicpref SET ValueString='9' WHERE ClinicNum="+POut.Long(_clinicNumA)+" AND PrefName='BillingEmailType'");
			ClinicPrefs.RefreshCache();
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumA));
		}
		#endregion Readers
	}
}
