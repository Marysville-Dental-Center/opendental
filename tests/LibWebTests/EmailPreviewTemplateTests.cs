// The e-mail template test of the legacy UnitTests project (UnitTests/UnitTests/EmailTests.cs,
// EmailPreviewControl_ReplaceTemplateFields_AreAllReplacementStringsReplaced), moved here by package S0-T of the .NET 10 plan because it needs the
// program (EmailPreviewControl) and the WPF controls (FrmMessageReplacements). The other 26 tests of that class are in
// tests/LibraryUpdateTests/EmailMessagesTests.cs. The test keeps its name and its arrange, act and assert; only the harness changed: the scratch
// database of ScratchDb.cs (s0t_, built from the seed dump) instead of the legacy unittest database and TestBase. No window is shown.
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
			List<FrmMessageReplacements.ReplacementField> listReplacementFields=FrmMessageReplacements.GetReplacementFieldList(true,listMessageReplaceTypes);
			for(int i=0;i<listReplacementFields.Count;i++){
				if(listReplacementFields[i].IsSupported){
					subject+=listReplacementFields[i].FieldName;
				}
			}
			subject=EmailPreviewControl.ReplaceTemplateFields(subject,pat,null,Clinics.GetClinic(pat.ClinicNum));
			Assert.IsFalse(subject.Any(x => x==']' || x=='['));
		}
	}
}
