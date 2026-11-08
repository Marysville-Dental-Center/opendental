// The e-mail template test of the legacy UnitTests project (UnitTests/UnitTests/EmailTests.cs,
// EmailPreviewControl_ReplaceTemplateFields_AreAllReplacementStringsReplaced), moved here by package S0-T of the .NET 10 plan because it needs the
// program (EmailPreviewControl) and the WPF controls (FrmMessageReplacements). The other 26 tests of that class are in
// tests/LibraryUpdateTests/EmailMessagesTests.cs. The test keeps its name and its arrange, act and assert; only the harness changed: the scratch
// database of ScratchDb.cs (s0t_, built from the seed dump) instead of the legacy unittest database and TestBase. No window is shown.
// One correction to the legacy test's field selection: it leaves out MessageReplaceType.ProgramLink, the fork's type for fields only program
// links replace, which the e-mail windows never offer (see the comment in the test). Two tests of S0-T follow it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace LibWebTests {
	[TestClass]
	public class EmailPreviewTemplateTests {
		#region Harness
		private static readonly string DB_NAME=ScratchDb.Name("web");
		private static string _skipReason;
		private static bool _isBuilt;
		private bool _isUnitTestOld;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			ODInitialize.Initialize();
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				ScratchDb.DropStale();
				_isBuilt=true;
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(!_isBuilt || Environment.GetEnvironmentVariable("S0T_KEEP_DB")=="1") {
				return;
			}
			try {
				ScratchDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		///<summary>What the legacy TestBase did before every test: the unit test flag, the unit test user and the preferences an earlier test
		///changed put back.</summary>
		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			_isUnitTestOld=ODBuild.IsUnitTest;
			ODBuild.IsUnitTest=true;//Causes FormFriendlyException to throw rather than displaying a MessageBox.
			ScratchDb.Connect(DB_NAME);
			ScratchDb.SetUnitTestUser();
			PrefT.RevertPrefChanges();
			AgingData.ActionGetAgingDataInvoked=null;
		}

		///<summary>What the legacy TestBase did after every test.</summary>
		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
			DefT.ResetDefs();
			ODBuild.IsUnitTest=_isUnitTestOld;
		}
		#endregion Harness

		[TestMethod]
		public void EmailPreviewControl_ReplaceTemplateFields_AreAllReplacementStringsReplaced() {
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient pat=PatientT.CreatePatient(suffix);
			ReferralT.CreateReferral(pat.PatNum);//LoadTemplate(...) will use this referral.
			RecallT.CreateRecall(pat.PatNum,0,DateTime.Now,new Interval());//LoadTemplate(...) will use this recall.
			string subject="";
			List<MessageReplaceType> listMessageReplaceTypes=Enum.GetValues(typeof(MessageReplaceType)).Cast<MessageReplaceType>().ToList();
			listMessageReplaceTypes.Remove(MessageReplaceType.PaymentPlan);
			//S0-T: ProgramLink (the fork's B-SHELL type, added after this legacy test was written) lists [SelectedProcNums], which only program
			//links replace. The e-mail windows never pass this type to the picker, so the picker never offers that field for e-mail; leave it out,
			//as PaymentPlan above.
			listMessageReplaceTypes.Remove(MessageReplaceType.ProgramLink);
			List<FrmMessageReplacements.ReplacementField> listReplacementFields=FrmMessageReplacements.GetReplacementFieldList(true,listMessageReplaceTypes);
			for(int i=0;i<listReplacementFields.Count;i++){
				if(listReplacementFields[i].IsSupported){
					subject+=listReplacementFields[i].FieldName;
				}
			}
			subject=EmailPreviewControl.ReplaceTemplateFields(subject,pat,null,Clinics.GetClinic(pat.ClinicNum));
			Assert.IsFalse(subject.Any(x => x==']' || x=='['));
		}

		///<summary>S0-T: every field the e-mail windows offer (FormEmailTemplateEdit's subject and body pickers and FormEmailEdit's body picker pass
		///these five types) is replaced, one field at a time so a failure names the field. [ClinicNumCur] is among them; [SelectedProcNums] is
		///not.</summary>
		[TestMethod]
		public void EmailPreviewControl_ReplaceTemplateFields_EveryFieldTheEmailWindowsOffer() {
			Patient pat=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name);
			List<MessageReplaceType> listMessageReplaceTypes=new List<MessageReplaceType> {
				MessageReplaceType.Appointment,MessageReplaceType.Office,MessageReplaceType.Patient,MessageReplaceType.User,MessageReplaceType.Misc };
			List<FrmMessageReplacements.ReplacementField> listReplacementFields=FrmMessageReplacements.GetReplacementFieldList(true,listMessageReplaceTypes)
				.FindAll(x => x.IsSupported);
			Assert.IsTrue(listReplacementFields.Any(x => x.FieldName=="[ClinicNumCur]"));
			Assert.IsFalse(listReplacementFields.Any(x => x.FieldName==ProgramLinkSelectedProcs.FIELD));
			for(int i=0;i<listReplacementFields.Count;i++) {
				string text=EmailPreviewControl.ReplaceTemplateFields("x"+listReplacementFields[i].FieldName+"x",pat,null,Clinics.GetClinic(pat.ClinicNum));
				Assert.IsFalse(text.Contains("[") || text.Contains("]"),listReplacementFields[i].FieldName+" is offered for e-mail but not replaced: "+text);
			}
		}

		///<summary>S0-T: [ClinicNumCur] in an e-mail is the currently selected clinic, as in a program link (ProgramL), and 0 when none is
		///selected (clinics off).</summary>
		[TestMethod]
		public void EmailPreviewControl_ReplaceTemplateFields_ClinicNumCurIsTheSelectedClinic() {
			Patient pat=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name);
			Clinic clinic=ClinicT.CreateClinic("S0T e-mail clinic");
			Clinics.SetClinicNumT(clinic.ClinicNum);
			try {
				Assert.AreEqual("Clinic "+clinic.ClinicNum+".",EmailPreviewControl.ReplaceTemplateFields("Clinic [ClinicNumCur].",pat,null,clinic));
			}
			finally {
				Clinics.SetClinicNumT(0);
			}
			Assert.AreEqual(0L,Clinics.ClinicNum,"No clinic is selected in this test process.");
			Assert.AreEqual("Clinic 0.",EmailPreviewControl.ReplaceTemplateFields("Clinic [ClinicNumCur].",pat,null,null));
		}
	}
}
