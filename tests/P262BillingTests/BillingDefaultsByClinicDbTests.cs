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

		private static int CountRows(long clinicNum,IEnumerable<PrefName> listPrefNames) {
			string prefNames=string.Join(",",listPrefNames.Select(x => "'"+POut.String(x.ToString())+"'"));
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref WHERE ClinicNum="+POut.Long(clinicNum)+" AND PrefName IN("+prefNames+")"));
		}

		private static int CountAllRows(long clinicNum) {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref WHERE ClinicNum="+POut.Long(clinicNum)));
		}

		///<summary>Billing Options' filter preferences other than the General Message.</summary>
		private static List<PrefName> GetFilterPrefNames() {
			return BillingOptionsClinicDefaults.ListPrefNames.FindAll(x => x!=PrefName.BillingDefaultsNote);
		}

		///<summary>What Billing Options' Save Defaults does with one clinic selected: it reads the clinic rows as the window does when it opens and
		///after each save (RefreshClinicPrefs, which writes nothing) and saves the selected clinic's filter defaults with this General Message.</summary>
		private static bool SaveBillingOptionsDefaults(long clinicNum,string generalMessage,string ageOfAccount="60") {
			BillingOptionsClinicDefaults.GetFilledDefaults(BillingOptionsClinicDefaults.GetRowsByClinic());
			List<KeyValuePair<PrefName,string>> listValues=new List<KeyValuePair<PrefName,string>>() {
				new KeyValuePair<PrefName,string>(PrefName.BillingIncludeChanged,"1"),
				new KeyValuePair<PrefName,string>(PrefName.BillingSelectBillingTypes,""),
				new KeyValuePair<PrefName,string>(PrefName.BillingAgeOfAccount,ageOfAccount),
				new KeyValuePair<PrefName,string>(PrefName.BillingDaysExcludeInsPending,"0"),
				new KeyValuePair<PrefName,string>(PrefName.BillingExcludeBadAddresses,"1"),
				new KeyValuePair<PrefName,string>(PrefName.BillingExcludeInactive,"1"),
				new KeyValuePair<PrefName,string>(PrefName.BillingExcludeNegative,"1"),
				new KeyValuePair<PrefName,string>(PrefName.BillingExcludeInsPending,"0"),
				new KeyValuePair<PrefName,string>(PrefName.BillingExcludeIfUnsentProcs,"0"),
				new KeyValuePair<PrefName,string>(PrefName.BillingExcludeLessThan,"5"),
				new KeyValuePair<PrefName,string>(PrefName.BillingIgnoreInPerson,"0"),
				new KeyValuePair<PrefName,string>(PrefName.BillingShowTransSinceBalZero,"0"),
				new KeyValuePair<PrefName,string>(PrefName.BillingDefaultsNote,generalMessage),
				new KeyValuePair<PrefName,string>(PrefName.BillingSelectInsFilingCodes,""),
			};
			bool isChanged=BillingOptionsClinicDefaults.SaveForClinic(clinicNum,listValues);
			BillingOptionsClinicDefaults.GetFilledDefaults(BillingOptionsClinicDefaults.GetRowsByClinic());
			return isChanged;
		}

		private static BillingDefaultsSettings SaveOwnSettings(long clinicNum,string tag,EmailType emailType=EmailType.Regular,bool includeAutograph=true) {
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(clinicNum);
			BillingDefaultsSettings settings=settingsOld.Copy();
			settings.UsePracticeDefaults=false;
			settings.GeneralMessage=tag+" general message";
			settings.EmailSubject=tag+" subject [PatNum]";
			settings.EmailBody=tag+" body for [nameF]";
			settings.EmailType=emailType;
			settings.IncludeAutograph=includeAutograph;
			settings.InvoiceNote=tag+" invoice note";
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settings,settingsOld));
			return settings;
		}

		///<summary>What Billing Defaults saves for a clinic whose settings it read (settingsOld) and the user edited (edit), as butSave_Click does.</summary>
		private static bool SaveEdited(BillingDefaultsSettings settingsOld,Action<BillingDefaultsSettings> edit) {
			BillingDefaultsSettings settings=settingsOld.Copy();
			edit(settings);
			return BillingDefaultsByClinic.SaveSettings(settings,settingsOld);
		}

		///<summary>The pre-26.2 state of a clinic that ever saved Billing Options defaults: its 13 filter rows and its General Message row, which that
		///Save Defaults always wrote. Of the six Billing Defaults messages it has only the General Message.</summary>
		private static void InsertPre262BillingOptionsRows(long clinicNum,string generalMessage) {
			foreach(PrefName prefName in GetFilterPrefNames()) {
				ClinicPrefs.InsertPref(prefName,clinicNum,PrefC.ContainsKey(prefName.ToString()) ? PrefC.GetString(prefName) : "");
			}
			ClinicPrefs.InsertPref(PrefName.BillingDefaultsNote,clinicNum,generalMessage);
			ClinicPrefs.RefreshCache();
			Assert.AreEqual(14,CountAllRows(clinicNum));
			Assert.AreEqual(1,CountClinicRows(clinicNum));
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
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(0);
			BillingDefaultsSettings settings=settingsOld.Copy();
			Assert.AreEqual(0,settings.ClinicNum);
			Assert.IsFalse(settings.UsePracticeDefaults);
			settings.EmailSubject="New practice subject";
			settings.EmailType=EmailType.Html;
			settings.IncludeAutograph=true;
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settings,settingsOld));
			Assert.AreEqual("New practice subject",PrefC.GetString(PrefName.BillingEmailSubject));
			Assert.AreEqual((int)EmailType.Html,PrefC.GetInt(PrefName.BillingEmailType));
			Assert.IsTrue(PrefC.GetBool(PrefName.BillingEmailIncludeAutograph));
			Assert.AreEqual(PRACTICE_NOTE,PrefC.GetString(PrefName.BillingDefaultsNote),"Unchanged.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref"),"No clinic row for the practice defaults.");
			Assert.IsFalse(BillingDefaultsByClinic.SaveSettings(settings,settingsOld),"Nothing changes the second time.");
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
			Assert.IsFalse(BillingDefaultsByClinic.SaveSettings(BillingDefaultsByClinic.GetSettings(_clinicNumA),BillingDefaultsByClinic.GetSettings(_clinicNumA)),
				"Nothing changes the second time.");
		}

		[TestMethod]
		public void ClinicBackToPracticeDefaults_RemovesItsRowsOnly() {
			SaveOwnSettings(_clinicNumA,"Clinic A");
			SaveOwnSettings(_clinicNumB,"Clinic B");
			//A Billing Options filter default of clinic A (not a Billing Defaults setting) stays.
			ClinicPrefs.InsertPref(PrefName.BillingIncludeChanged,_clinicNumA,"1");
			ClinicPrefs.RefreshCache();
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			BillingDefaultsSettings settings=settingsOld.Copy();
			settings.UsePracticeDefaults=true;
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settings,settingsOld));
			Assert.AreEqual(0,CountClinicRows(_clinicNumA));
			Assert.AreEqual(6,CountClinicRows(_clinicNumB));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref WHERE ClinicNum="+POut.Long(_clinicNumA)+" AND PrefName='BillingIncludeChanged'"));
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			Assert.AreEqual(PRACTICE_SUBJECT,BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,_clinicNumA));
			Assert.AreEqual("Clinic B subject [PatNum]",BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,_clinicNumB));
			Assert.IsFalse(BillingDefaultsByClinic.SaveSettings(settings,settingsOld),"Nothing left to remove.");
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

		#region Saving a clinic with some rows (review fix of p262/billing-2)
		[TestMethod]
		public void Pre262GeneralMessageRow_EditingIt_KeepsTheOtherFiveOnThePracticeDefaults() {
			//Clinic A saved Billing Options defaults before 26.2, so its only Billing Defaults row is the General Message. A user edits only that
			//message in Billing Defaults and saves.
			InsertPre262BillingOptionsRows(_clinicNumA,"Saved in Billing Options before 26.2");
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsFalse(settingsOld.UsePracticeDefaults,"Shown as the clinic's own settings.");
			Assert.IsTrue(SaveEdited(settingsOld,x => x.GeneralMessage="Edited in Billing Defaults"));
			//Still one of the six rows: the General Message, edited in place. The filter rows are untouched.
			Assert.AreEqual(1,CountClinicRows(_clinicNumA));
			Assert.AreEqual(14,CountAllRows(_clinicNumA));
			Assert.AreEqual("Edited in Billing Defaults",BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsNote,_clinicNumA));
			CollectionAssert.AreEqual(new[] { PrefName.BillingDefaultsNote },BillingDefaultsByClinic.GetPrefNamesWithRows(_clinicNumA).ToArray());
			//The practice defaults change: clinic A's statement e-mails and invoices follow them.
			BillingDefaultsSettings settingsPracticeOld=BillingDefaultsByClinic.GetSettings(0);
			Assert.IsTrue(SaveEdited(settingsPracticeOld,x => {
				x.EmailSubject="Newer practice subject [PatNum]";
				x.EmailBody="Newer practice body for [nameF]";
				x.InvoiceNote="Newer practice invoice note";
			}));
			Assert.AreEqual(1,CountClinicRows(_clinicNumA));
			Patient patientA=CreatePatient("Legacya",_clinicNumA);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patientA),patientA);
			Assert.AreEqual("Newer practice subject "+patientA.PatNum,emailMessage.Subject);
			StringAssert.Contains(emailMessage.BodyText,"Newer practice body for Legacya");
			Assert.AreEqual("Newer practice invoice note",BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsInvoiceNote,_clinicNumA));
			Assert.AreEqual("Edited in Billing Defaults",new ClinicBillingOptions(_clinicNumA).GetPrefValue(PrefName.BillingDefaultsNote));
		}

		[TestMethod]
		public void Pre262GeneralMessageRow_PracticeEditedInTheSameSession_TheOlderPracticeBodyIsNotSaved() {
			//Billing Defaults read clinic A (only its General Message row) with the practice body of that moment. In the same window the practice body
			//is edited; Save writes the practice defaults first, then clinic A, whose General Message was edited.
			InsertPre262BillingOptionsRows(_clinicNumA,"Saved in Billing Options before 26.2");
			BillingDefaultsSettings settingsOldA=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.AreEqual(PRACTICE_BODY,settingsOldA.EmailBody);
			BillingDefaultsSettings settingsPracticeOld=BillingDefaultsByClinic.GetSettings(0);
			Assert.IsTrue(SaveEdited(settingsPracticeOld,x => x.EmailBody="Edited practice body for [nameF]"));
			Assert.IsTrue(SaveEdited(settingsOldA,x => x.GeneralMessage="Clinic A message"));
			Assert.AreEqual(1,CountClinicRows(_clinicNumA),"No copy of the practice body as it was when the window read clinic A.");
			Patient patientA=CreatePatient("Samesession",_clinicNumA);
			StringAssert.Contains(BuildEmail(InsertStatement(patientA),patientA).BodyText,"Edited practice body for Samesession");
		}

		[TestMethod]
		public void Pre262GeneralMessageRow_ShownWithThePracticeValuesAsEdited_SavesOnlyWhatChanged() {
			//Billing Defaults shows clinic A after the practice body was edited in the window: the messages A has no row for show the edited practice
			//values (FollowPracticeDefaults, as ShowSettings calls it), which is not a change. The subject is then edited for A.
			InsertPre262BillingOptionsRows(_clinicNumA,"Saved in Billing Options before 26.2");
			BillingDefaultsSettings settingsPracticeOld=BillingDefaultsByClinic.GetSettings(0);
			BillingDefaultsSettings settingsPractice=settingsPracticeOld.Copy();
			settingsPractice.EmailBody="Edited practice body for [nameF]";
			BillingDefaultsSettings settingsOldA=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			BillingDefaultsSettings settingsA=settingsOldA.Copy();
			BillingDefaultsByClinic.FollowPracticeDefaults(settingsA,settingsOldA,settingsPractice,BillingDefaultsByClinic.GetPrefNamesWithRows(_clinicNumA));
			Assert.AreEqual("Edited practice body for [nameF]",settingsA.EmailBody,"Shows the practice body as edited.");
			Assert.AreEqual("Saved in Billing Options before 26.2",settingsA.GeneralMessage,"Its own row.");
			Assert.IsTrue(settingsA.IsSameAs(settingsOldA),"Following the practice is not a change.");
			settingsA.EmailSubject="Clinic A subject [PatNum]";
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settingsPractice,settingsPracticeOld));
			Assert.IsTrue(BillingDefaultsByClinic.SaveSettings(settingsA,settingsOldA));
			Assert.AreEqual(2,CountClinicRows(_clinicNumA));
			CollectionAssert.AreEqual(new[] { PrefName.BillingDefaultsNote,PrefName.BillingEmailSubject },
				BillingDefaultsByClinic.GetPrefNamesWithRows(_clinicNumA).ToArray());
			Patient patientA=CreatePatient("Shown",_clinicNumA);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patientA),patientA);
			Assert.AreEqual("Clinic A subject "+patientA.PatNum,emailMessage.Subject);
			StringAssert.Contains(emailMessage.BodyText,"Edited practice body for Shown");
		}

		[TestMethod]
		public void ClinicWithSomeRows_EditingTheBodyOrItsType_WritesBoth() {
			//The body and its type are saved together, so a later change of the practice type does not change how the clinic's body is read.
			InsertPre262BillingOptionsRows(_clinicNumA,"Clinic A message");
			Assert.IsTrue(SaveEdited(BillingDefaultsByClinic.GetSettings(_clinicNumA),x => x.EmailBody="Clinic A plain body for [nameF]"));
			CollectionAssert.AreEqual(new[] { PrefName.BillingDefaultsNote,PrefName.BillingEmailBodyText,PrefName.BillingEmailType },
				BillingDefaultsByClinic.GetPrefNamesWithRows(_clinicNumA).ToArray());
			Assert.IsTrue(SaveEdited(BillingDefaultsByClinic.GetSettings(0),x => x.EmailType=EmailType.Html));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumA));
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumB));
			//Clinic B, with only its General Message row, changes only the type: its body (the practice body as read) goes with it.
			InsertPre262BillingOptionsRows(_clinicNumB,"Clinic B message");
			Assert.IsTrue(SaveEdited(BillingDefaultsByClinic.GetSettings(_clinicNumB),x => x.EmailType=EmailType.RawHtml));
			Assert.AreEqual(3,CountClinicRows(_clinicNumB));
			Assert.AreEqual(PRACTICE_BODY,BillingDefaultsByClinic.GetString(PrefName.BillingEmailBodyText,_clinicNumB));
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.GetBillingDefaultsEmailType(_clinicNumB));
		}

		[TestMethod]
		public void ClinicLeavingThePracticeDefaults_GetsAllSixRows_EvenWithOneMessageChanged() {
			//Owner question 5: a clinic that stops using the practice defaults starts from a copy of them.
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsTrue(settingsOld.UsePracticeDefaults);
			Assert.IsTrue(SaveEdited(settingsOld,x => {
				x.UsePracticeDefaults=false;
				x.GeneralMessage="Clinic A message";
			}));
			Assert.AreEqual(6,CountClinicRows(_clinicNumA));
			Assert.AreEqual(PRACTICE_BODY,BillingDefaultsByClinic.GetString(PrefName.BillingEmailBodyText,_clinicNumA));
			//Saved unchanged once it has its own settings: nothing is written.
			Assert.IsFalse(SaveEdited(BillingDefaultsByClinic.GetSettings(_clinicNumA),x => { }));
		}

		[TestMethod]
		public void ClinicWithAllSixRows_EditingOne_UpdatesThatRowOnly() {
			SaveOwnSettings(_clinicNumA,"Clinic A");
			//Another workstation changes the invoice note after this one read clinic A.
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			DataCore.NonQ("UPDATE clinicpref SET ValueString='Invoice note from another workstation' WHERE ClinicNum="+POut.Long(_clinicNumA)
				+" AND PrefName='BillingDefaultsInvoiceNote'");
			ClinicPrefs.RefreshCache();
			Assert.IsTrue(SaveEdited(settingsOld,x => x.EmailSubject="Edited subject [PatNum]"));
			Assert.AreEqual(6,CountClinicRows(_clinicNumA));
			Assert.AreEqual("Edited subject [PatNum]",BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,_clinicNumA));
			Assert.AreEqual("Invoice note from another workstation",BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsInvoiceNote,_clinicNumA),
				"An unchanged message is not written back.");
		}

		[TestMethod]
		public void SaveSettings_RefusesOldSettingsOfAnotherClinic() {
			BillingDefaultsSettings settingsA=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			try {
				BillingDefaultsByClinic.SaveSettings(settingsA,BillingDefaultsByClinic.GetSettings(_clinicNumB));
				Assert.Fail("Saved clinic A against clinic B's settings.");
			}
			catch(ArgumentException) {
				//Expected.
			}
			try {
				BillingDefaultsByClinic.SaveSettings(settingsA,null);
				Assert.Fail("Saved without the old settings.");
			}
			catch(ArgumentNullException) {
				//Expected.
			}
			Assert.AreEqual(0,CountAllRows(_clinicNumA));
		}
		#endregion Saving a clinic with some rows (review fix of p262/billing-2)

		#region Billing Options' Save Defaults beside Billing Defaults
		[TestMethod]
		public void BillingOptionsSave_ForAnotherClinic_GivesAClinicWithItsOwnMessagesNoFilterRows() {
			//Clinic A has its own messages from Billing Defaults (six rows, the General Message among them) and never saved Billing Options filters.
			SaveOwnSettings(_clinicNumA,"Clinic A");
			Assert.AreEqual(6,CountAllRows(_clinicNumA));
			Dictionary<long,List<ClinicPref>> dictionaryRows=BillingOptionsClinicDefaults.GetRowsByClinic();
			Assert.IsTrue(dictionaryRows.ContainsKey(_clinicNumA),"Its General Message row is one of Billing Options' rows.");
			Assert.IsFalse(BillingOptionsClinicDefaults.HasSavedFilterDefaults(dictionaryRows[_clinicNumA]));
			Assert.IsFalse(BillingOptionsClinicDefaults.GetFilledDefaults(dictionaryRows).ContainsKey(_clinicNumA),
				"Billing Options shows the practice filters for clinic A.");
			//Billing Options saves defaults for clinic B.
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumB,"Clinic B message"));
			Assert.AreEqual(14,CountRows(_clinicNumB,BillingOptionsClinicDefaults.ListPrefNames));
			//Clinic A gained no row, and its General Message is unchanged.
			Assert.AreEqual(6,CountAllRows(_clinicNumA));
			Assert.AreEqual(0,CountRows(_clinicNumA,GetFilterPrefNames()));
			Assert.AreEqual("Clinic A general message",BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsNote,_clinicNumA));
			//So billing keeps following the practice filter defaults for clinic A, later changes included.
			string ageOfAccountOld=PrefC.GetString(PrefName.BillingAgeOfAccount);
			string ageOfAccountNew=(ageOfAccountOld=="90" ? "30" : "90");
			try {
				Prefs.UpdateString(PrefName.BillingAgeOfAccount,ageOfAccountNew);
				Cache.Refresh(InvalidType.Prefs);
				Assert.AreEqual(ageOfAccountNew,new ClinicBillingOptions(_clinicNumA).GetPrefValue(PrefName.BillingAgeOfAccount));
				Assert.AreEqual("60",new ClinicBillingOptions(_clinicNumB).GetPrefValue(PrefName.BillingAgeOfAccount));
				Assert.AreEqual("Clinic A general message",new ClinicBillingOptions(_clinicNumA).GetPrefValue(PrefName.BillingDefaultsNote));
			}
			finally {
				Prefs.UpdateString(PrefName.BillingAgeOfAccount,ageOfAccountOld);
				Cache.Refresh(InvalidType.Prefs);
			}
		}

		[TestMethod]
		public void BillingOptionsSave_ForAnotherClinic_DoesNotBringBackTheGeneralMessageOfAClinicOnThePracticeDefaults() {
			//Clinic A saved Billing Options defaults (its 13 filters and its General Message)...
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumA,"Clinic A filters message"));
			Assert.AreEqual(14,CountRows(_clinicNumA,BillingOptionsClinicDefaults.ListPrefNames));
			//...and is then set to 'Use the practice defaults for this clinic' in Billing Defaults, which removes its General Message row.
			BillingDefaultsSettings settingsOld=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsFalse(settingsOld.UsePracticeDefaults);
			Assert.IsTrue(SaveEdited(settingsOld,x => x.UsePracticeDefaults=true));
			Assert.AreEqual(0,CountRows(_clinicNumA,new[] { PrefName.BillingDefaultsNote }));
			Assert.AreEqual(13,CountAllRows(_clinicNumA));
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			//Billing Options still shows clinic A's filters, with the practice General Message in place of the removed row (not written).
			Dictionary<long,List<ClinicPref>> dictionaryFilled=BillingOptionsClinicDefaults.GetFilledDefaults(BillingOptionsClinicDefaults.GetRowsByClinic());
			Assert.IsTrue(dictionaryFilled.ContainsKey(_clinicNumA));
			ClinicPref clinicPrefNote=dictionaryFilled[_clinicNumA].Single(x => x.PrefName==PrefName.BillingDefaultsNote);
			Assert.AreEqual(PRACTICE_NOTE,clinicPrefNote.ValueString);
			Assert.AreEqual(0,clinicPrefNote.ClinicPrefNum);
			//Billing Options saves defaults for clinic B: clinic A keeps the practice defaults.
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumB,"Clinic B message"));
			Assert.AreEqual(0,CountRows(_clinicNumA,new[] { PrefName.BillingDefaultsNote }));
			Assert.AreEqual(13,CountAllRows(_clinicNumA));
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			Assert.IsTrue(BillingDefaultsByClinic.GetSettings(_clinicNumA).UsePracticeDefaults);
			//Its General Message follows the practice's.
			Prefs.UpdateString(PrefName.BillingDefaultsNote,"Changed practice message");
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual("Changed practice message",new ClinicBillingOptions(_clinicNumA).GetPrefValue(PrefName.BillingDefaultsNote));
			Assert.AreEqual("Clinic B message",new ClinicBillingOptions(_clinicNumB).GetPrefValue(PrefName.BillingDefaultsNote));
		}

		[TestMethod]
		public void BillingOptionsSave_WritesTheSelectedClinicOnly() {
			//Clinic A saved filter defaults before more preferences joined Billing Options' list: one row. Billing Options shows the others with the
			//practice values, but a save for clinic B writes none of them.
			ClinicPrefs.InsertPref(PrefName.BillingIncludeChanged,_clinicNumA,"1");
			ClinicPrefs.RefreshCache();
			Dictionary<long,List<ClinicPref>> dictionaryFilled=BillingOptionsClinicDefaults.GetFilledDefaults(BillingOptionsClinicDefaults.GetRowsByClinic());
			Assert.AreEqual(14,dictionaryFilled[_clinicNumA].Count);
			Assert.AreEqual(PrefC.GetString(PrefName.BillingExcludeLessThan),
				dictionaryFilled[_clinicNumA].Single(x => x.PrefName==PrefName.BillingExcludeLessThan).ValueString);
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumB,"Clinic B message"));
			Assert.AreEqual(1,CountAllRows(_clinicNumA));
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			Assert.IsFalse(SaveBillingOptionsDefaults(_clinicNumB,"Clinic B message"),"Nothing changes the second time.");
		}

		[TestMethod]
		public void BillingOptionsSave_ForAClinicWithItsOwnMessages_UpdatesItsGeneralMessageRow() {
			//Clinic A has its own messages from Billing Defaults; Billing Options then saves its filter defaults with an edited General Message.
			SaveOwnSettings(_clinicNumA,"Clinic A");
			long clinicPrefNumNote=ClinicPrefs.GetWhere(x => x.ClinicNum==_clinicNumA && x.PrefName==PrefName.BillingDefaultsNote).Single().ClinicPrefNum;
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumA,"Edited in Billing Options"));
			Assert.AreEqual(6+13,CountAllRows(_clinicNumA));
			ClinicPref clinicPrefNote=ClinicPrefs.GetWhere(x => x.ClinicNum==_clinicNumA && x.PrefName==PrefName.BillingDefaultsNote).Single();
			Assert.AreEqual(clinicPrefNumNote,clinicPrefNote.ClinicPrefNum,"The shared row, updated in place.");
			BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(_clinicNumA);
			Assert.IsFalse(settings.UsePracticeDefaults);
			Assert.AreEqual("Edited in Billing Options",settings.GeneralMessage);
			Assert.AreEqual("Clinic A subject [PatNum]",settings.EmailSubject,"The other messages are untouched.");
			Assert.IsTrue(BillingOptionsClinicDefaults.GetFilledDefaults(BillingOptionsClinicDefaults.GetRowsByClinic()).ContainsKey(_clinicNumA));
			Assert.AreEqual("60",new ClinicBillingOptions(_clinicNumA).GetPrefValue(PrefName.BillingAgeOfAccount));
			Assert.AreEqual(0,CountAllRows(_clinicNumB));
		}

		[TestMethod]
		public void BillingOptionsSave_WithThePracticeMessage_KeepsAClinicOnThePracticeDefaults() {
			//Saving a clinic's filters with the practice General Message adds no General Message row: Billing Defaults still shows the clinic on the
			//practice defaults. A message of its own adds the row.
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumA,PRACTICE_NOTE));
			Assert.AreEqual(13,CountAllRows(_clinicNumA));
			Assert.IsFalse(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			Assert.IsFalse(SaveBillingOptionsDefaults(_clinicNumA,PRACTICE_NOTE),"Nothing changes the second time.");
			Assert.IsTrue(SaveBillingOptionsDefaults(_clinicNumA,"Clinic A own message"));
			Assert.AreEqual(14,CountAllRows(_clinicNumA));
			Assert.IsTrue(BillingDefaultsByClinic.HasOwnSettings(_clinicNumA));
			Assert.AreEqual("Clinic A own message",BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsNote,_clinicNumA));
		}

		[TestMethod]
		public void BillingOptionsSave_RefusesThePracticeAsAClinic() {
			foreach(long clinicNum in new long[] { 0,-1 }) {
				try {
					BillingOptionsClinicDefaults.SaveForClinic(clinicNum,new List<KeyValuePair<PrefName,string>>());
					Assert.Fail("Saved clinic rows for "+clinicNum);
				}
				catch(ArgumentException) {
					//Expected: the practice filter defaults are the preference rows.
				}
			}
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM clinicpref"));
		}
		#endregion Billing Options' Save Defaults beside Billing Defaults

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
