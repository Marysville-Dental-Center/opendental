// V-GP against a scratch database (24.3.63, upgraded to 26.1.70 by the fork migration runner): the program link rows and secrets, the terminal
// lists per integration, per-clinic settings, the one-processor rule, the V1 rename, duplicate checks, saved cards and recurring charges. Every call
// to Global Payments goes to a fake handler. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;
using OpenDentBusiness.VendorDirect.GlobalPayments;
using UnitTestsCore;

namespace VGPTests {
	[TestClass]
	public class GpV2DbTests {
		private const string APP_KEY_HQ="hq-app-key-synthetic";
		private GpFakeHttp _gpFakeHttp;

		[TestInitialize]
		public void TestInitialize() {
			GpDb.Begin();
			Security.CurUser=new Userod();
			SetClinicsEnabled(false);
			//Each test starts with the three exclusive card programs off, no Global Payments V2 settings and no terminals.
			GlobalPaymentsV2.EnsureProgram();
			Programs.RefreshCache();
			long programNum=Programs.GetProgramNum(ProgramName.MeetInTheCloud);
			DataCore.NonQ("DELETE FROM programproperty WHERE ProgramNum="+POut.Long(programNum));
			DataCore.NonQ("UPDATE program SET Enabled=0 WHERE ProgName IN ('Xcharge','EdgeExpress','MeetInTheCloud')");
			DataCore.NonQ("DELETE FROM payterminal");
			Refresh();
			GlobalPaymentsV2.EnsureProgram();
			Refresh();
			_gpFakeHttp=new GpFakeHttp();
			GlobalPaymentsV2.HttpHandlerForTests=_gpFakeHttp;
		}

		[TestCleanup]
		public void TestCleanup() {
			GlobalPaymentsV2.HttpHandlerForTests=null;
		}

		private static void Refresh() {
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static void SetClinicsEnabled(bool isEnabled) {
			Prefs.UpdateBool(PrefName.EasyNoClinics,!isEnabled);
			Prefs.RefreshCache();
		}

		private static long ProgramNum {
			get {
				return Programs.GetProgramNum(ProgramName.MeetInTheCloud);
			}
		}

		private static void SetEnabled(ProgramName programName,bool isEnabled) {
			DataCore.NonQ("UPDATE program SET Enabled="+POut.Bool(isEnabled)+" WHERE ProgName='"+programName+"'");
			Refresh();
		}

		///<summary>App ID, App Key (osd1$) and account name for a clinic, plus the payment type.</summary>
		private static void SetCredentials(long clinicNum,string appId,string appKey,string accountName="acct",long payTypeDefNum=0) {
			VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropAppId,clinicNum,appId);
			Assert.IsTrue(VendorCredentials.WriteSecret(ProgramNum,GlobalPaymentsV2.PropAppKey,clinicNum,appKey));
			VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropAccountName,clinicNum,accountName);
			if(payTypeDefNum!=0) {
				VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropPaymentType,clinicNum,POut.Long(payTypeDefNum));
			}
			Refresh();
		}

		[TestMethod]
		public void EnsureProgram_InsertsADisabledRowWithItsSettings_Once() {
			DataCore.NonQ("DELETE FROM programproperty WHERE ProgramNum="+POut.Long(ProgramNum));
			DataCore.NonQ("DELETE FROM program WHERE ProgName='MeetInTheCloud'");
			Refresh();
			Assert.IsTrue(GlobalPaymentsV2.EnsureProgram());
			Refresh();
			Program program=Programs.GetCur(ProgramName.MeetInTheCloud);
			Assert.IsNotNull(program);
			Assert.IsFalse(program.Enabled,"Off by default.");
			Assert.AreEqual("Global Payments V2",program.ProgDesc);
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(program.ProgramNum);
			foreach(string desc in new[] { GlobalPaymentsV2.PropAppId,GlobalPaymentsV2.PropAppKey,GlobalPaymentsV2.PropAccountName,
				GlobalPaymentsV2.PropPaymentType,GlobalPaymentsV2.PropPrintReceipt,GlobalPaymentsV2.PropPreventSavingNewCC,
				GlobalPaymentsV2.PropForceRecurringCharge,GlobalPaymentsV2.PropUseSandbox })
			{
				Assert.AreEqual(1,listProgramProperties.Count(x => x.PropertyDesc==desc && x.ClinicNum==0),desc);
			}
			ProgramProperty programPropertyKey=listProgramProperties.Single(x => x.PropertyDesc==GlobalPaymentsV2.PropAppKey);
			Assert.IsTrue(programPropertyKey.IsMasked && programPropertyKey.IsHighSecurity,"The App Key row is masked and high security.");
			Assert.AreEqual("0",listProgramProperties.Single(x => x.PropertyDesc==GlobalPaymentsV2.PropUseSandbox).PropertyValue);
			Assert.IsFalse(GlobalPaymentsV2.EnsureProgram(),"A second call inserts nothing.");
			Refresh();
			Assert.AreEqual(1,Programs.GetWhere(x => x.ProgName==ProgramName.MeetInTheCloud.ToString()).Count);
		}

		[TestMethod]
		public void EnsureProgram_GivesPre25_4TerminalsToPayConnect() {
			DataCore.NonQ("INSERT INTO payterminal (Name,ClinicNum,TerminalID,CCIntegration) VALUES ('Old',0,'T-OLD',''),('V2',0,'T-V2','MeetInTheCloud')");
			GlobalPaymentsV2.EnsureProgram();
			Assert.AreEqual("PayConnect",DataCore.GetScalar("SELECT CCIntegration FROM payterminal WHERE TerminalID='T-OLD'"));
			Assert.AreEqual("MeetInTheCloud",DataCore.GetScalar("SELECT CCIntegration FROM payterminal WHERE TerminalID='T-V2'"));
			Assert.AreEqual(0L,PayTerminals.BackfillPayConnectIntegration(),"Nothing left to change.");
		}

		[TestMethod]
		public void Terminals_EachIntegrationListsItsOwn() {
			DataCore.NonQ("INSERT INTO payterminal (Name,ClinicNum,TerminalID,CCIntegration) VALUES ('Pre 25.4',0,'T-BLANK','')");
			PayTerminals.Insert(new PayTerminal { Name="PayConnect desk",ClinicNum=0,TerminalID="T-PC" });
			PayTerminals.Insert(new PayTerminal { Name="V2 desk",ClinicNum=0,TerminalID="T-GP",CCIntegration=PayTerminals.CCIntegrationMeetInTheCloud });
			PayTerminals.Insert(new PayTerminal { Name="V2 other clinic",ClinicNum=7,TerminalID="T-GP7",CCIntegration=PayTerminals.CCIntegrationMeetInTheCloud });
			Assert.AreEqual("PayConnect",DataCore.GetScalar("SELECT CCIntegration FROM payterminal WHERE TerminalID='T-PC'"),
				"A terminal saved without an integration is a PayConnect terminal.");
			CollectionAssert.AreEquivalent(new[] { "T-BLANK","T-PC" },PayTerminals.Refresh(0).Select(x => x.TerminalID).ToArray(),
				"The PayConnect windows never offer a Global Payments V2 terminal.");
			CollectionAssert.AreEqual(new[] { "T-GP" },GlobalPaymentsV2.GetTerminals(0).Select(x => x.TerminalID).ToArray());
			CollectionAssert.AreEqual(new[] { "T-GP" },PayTerminals.GetForIntegration(0,"MeetInTheCloud").Select(x => x.TerminalID).ToArray());
			CollectionAssert.AreEquivalent(new[] { "T-BLANK","T-PC" },PayTerminals.GetForIntegration(0,"PayConnect").Select(x => x.TerminalID).ToArray());
		}

		[TestMethod]
		public void Config_KeyIsEncrypted_AndTheLinkIsOffUntilEnabled() {
			SetCredentials(0,"hq-app",APP_KEY_HQ);
			string stored=DataCore.GetScalar("SELECT PropertyValue FROM programproperty WHERE ProgramNum="+POut.Long(ProgramNum)
				+" AND PropertyDesc='"+GlobalPaymentsV2.PropAppKey+"' AND ClinicNum=0");
			Assert.AreNotEqual(APP_KEY_HQ,stored,"Never stored as plain text.");
			StringAssert.StartsWith(stored,"osd1$");
			GlobalPaymentsV2Config config=GlobalPaymentsV2.GetConfig(0);
			Assert.AreEqual("hq-app",config.AppId);
			Assert.AreEqual(APP_KEY_HQ,config.AppKey);
			Assert.AreEqual("acct",config.AccountName);
			Assert.IsTrue(config.IsComplete);
			Assert.IsFalse(GlobalPaymentsV2.IsConfigured(0),"Off by default.");
			Assert.IsFalse(GlobalPaymentsV2.ShowButton(0,null),"No button before the office enables it.");
			SetEnabled(ProgramName.MeetInTheCloud,true);
			Assert.IsTrue(GlobalPaymentsV2.IsConfigured(0));
			Assert.IsTrue(GlobalPaymentsV2.ShowButton(0,new List<Def>()),"Without clinics the payment type isn't required for the button.");
		}

		[TestMethod]
		public void Config_UnreadableKeyIsNotConfigured_PlainTextKeyIsAccepted() {
			SetCredentials(0,"hq-app",APP_KEY_HQ);
			VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropAppKey,0,"osd1$not-a-valid-ciphertext");
			Refresh();
			SetEnabled(ProgramName.MeetInTheCloud,true);
			Assert.AreEqual("",GlobalPaymentsV2.GetConfig(0).AppKey,"An unreadable key is never sent.");
			Assert.IsFalse(GlobalPaymentsV2.IsConfigured(0));
			VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropAppKey,0,"typed-in-plain");
			Refresh();
			Assert.AreEqual("typed-in-plain",GlobalPaymentsV2.GetConfig(0).AppKey);
		}

		[TestMethod]
		public void Config_ClinicWithItsOwnAppId_UsesItsOwnCredentials_OthersUseHeadquarters() {
			//Clinics count as on only when at least one clinic exists.
			long clinicNum5=InsertClinic("VGP clinic A");
			long clinicNum6=InsertClinic("VGP clinic B");
			long clinicNum8=InsertClinic("VGP clinic C");
			SetClinicsEnabled(true);
			try {
				long defNumHq=Defs.GetDefsForCategory(DefCat.PaymentTypes,true).First().DefNum;
				SetCredentials(0,"hq-app",APP_KEY_HQ,"hq_account",defNumHq);
				SetCredentials(clinicNum5,"clinic5-app","clinic5-key","clinic5_account");
				VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropPrintReceipt,clinicNum5,"1");
				Refresh();
				GlobalPaymentsV2Config config5=GlobalPaymentsV2.GetConfig(clinicNum5);
				Assert.AreEqual(clinicNum5,config5.ClinicNumCredentials);
				Assert.AreEqual("clinic5-app",config5.AppId);
				Assert.AreEqual("clinic5-key",config5.AppKey);
				Assert.AreEqual("clinic5_account",config5.AccountName);
				Assert.IsTrue(config5.PrintReceipt);
				Assert.AreEqual(defNumHq,config5.PaymentTypeDefNum,"Settings the clinic doesn't have come from Headquarters.");
				GlobalPaymentsV2Config config6=GlobalPaymentsV2.GetConfig(clinicNum6);
				Assert.AreEqual(0L,config6.ClinicNumCredentials);
				Assert.AreEqual("hq-app",config6.AppId);
				Assert.AreEqual(APP_KEY_HQ,config6.AppKey);
				Assert.AreEqual("hq_account",config6.AccountName);
				Assert.IsFalse(config6.PrintReceipt);
				Assert.AreNotEqual(config5.Fingerprint,config6.Fingerprint,"Different credentials never share a cached token.");
				//A clinic with only a key and no App ID still uses Headquarters' pair, never a mix.
				Assert.IsTrue(VendorCredentials.WriteSecret(ProgramNum,GlobalPaymentsV2.PropAppKey,clinicNum8,"stray-key"));
				Refresh();
				Assert.AreEqual(APP_KEY_HQ,GlobalPaymentsV2.GetConfig(clinicNum8).AppKey);
				SetEnabled(ProgramName.MeetInTheCloud,true);
				List<Def> listDefs=Defs.GetDefsForCategory(DefCat.PaymentTypes,true);
				Assert.IsTrue(GlobalPaymentsV2.ShowButton(clinicNum6,listDefs));
				Assert.IsFalse(GlobalPaymentsV2.ShowButton(clinicNum6,new List<Def>()),"With clinics the clinic needs a valid payment type.");
			}
			finally {
				SetClinicsEnabled(false);
			}
		}

		private static long InsertClinic(string description) {
			Clinic clinic=new Clinic();
			clinic.Description=description;
			clinic.Abbr=description;
			long clinicNum=Clinics.Insert(clinic);
			Clinics.RefreshCache();
			return clinicNum;
		}

		[TestMethod]
		public void OnlyOneOfXChargeV1AndV2() {
			Assert.AreEqual(0,GlobalPaymentsV2.GetEnabledExclusiveConflicts(ProgramName.MeetInTheCloud).Count);
			Assert.AreEqual("",GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.MeetInTheCloud));
			SetEnabled(ProgramName.EdgeExpress,true);
			CollectionAssert.AreEqual(new[] { ProgramName.EdgeExpress },GlobalPaymentsV2.GetEnabledExclusiveConflicts(ProgramName.MeetInTheCloud));
			StringAssert.Contains(GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.MeetInTheCloud),"Turn off Global Payments V1");
			Assert.AreEqual(0,GlobalPaymentsV2.GetEnabledExclusiveConflicts(ProgramName.EdgeExpress).Count,"Re-saving the enabled one is fine.");
			SetEnabled(ProgramName.MeetInTheCloud,true);
			SetEnabled(ProgramName.Xcharge,true);
			CollectionAssert.AreEquivalent(new[] { ProgramName.EdgeExpress,ProgramName.MeetInTheCloud },
				GlobalPaymentsV2.GetEnabledExclusiveConflicts(ProgramName.Xcharge));
			StringAssert.Contains(GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.EdgeExpress),"X-Charge, Global Payments V2");
			Assert.AreEqual(0,GlobalPaymentsV2.GetEnabledExclusiveConflicts(ProgramName.PayConnect).Count(x => x==ProgramName.PayConnect),
				"PayConnect is not part of the rule.");
		}

		[TestMethod]
		public void V1Rename_OnlyTheOldDefaultDescription() {
			DataCore.NonQ("UPDATE program SET ProgDesc='"+POut.String(GlobalPaymentsV1.ProgDescOld)+"' WHERE ProgName='EdgeExpress'");
			Refresh();
			Assert.IsTrue(GlobalPaymentsV1.RenameProgDescIfDefault());
			Refresh();
			Assert.AreEqual("Global Payments V1",Programs.GetCur(ProgramName.EdgeExpress).ProgDesc);
			Assert.AreEqual("EdgeExpress",Programs.GetCur(ProgramName.EdgeExpress).ProgName,"The program name stays, for data compatibility.");
			Assert.IsFalse(GlobalPaymentsV1.RenameProgDescIfDefault(),"Idempotent.");
			DataCore.NonQ("UPDATE program SET ProgDesc='Our card machine' WHERE ProgName='EdgeExpress'");
			Refresh();
			Assert.IsFalse(GlobalPaymentsV1.RenameProgDescIfDefault());
			Refresh();
			Assert.AreEqual("Our card machine",Programs.GetCur(ProgramName.EdgeExpress).ProgDesc,"An office's own description is kept.");
		}

		[TestMethod]
		public void Duplicates_SamePatientAmountAndDay() {
			Patient patient=PatientT.CreatePatient("vgpdup",lName:"Vgp",fName:"Duplicate");
			Payment payment=new Payment { PatNum=patient.PatNum,PayAmt=20,PayDate=DateTime.Today,PaymentSource=CreditCardSource.MeetInTheCloudTerminal,
				IsCcCompleted=true,PayNote="",ExternalId="TRN_x" };
			Payments.Insert(payment);
			Assert.IsTrue(GlobalPaymentsV2.HasSamePaymentOnDate(patient.PatNum,20,DateTime.Today));
			Assert.IsFalse(GlobalPaymentsV2.HasSamePaymentOnDate(patient.PatNum,21,DateTime.Today));
			Assert.IsFalse(GlobalPaymentsV2.HasSamePaymentOnDate(patient.PatNum,20,DateTime.Today.AddDays(-1)));
			payment.PaymentSource=CreditCardSource.PayConnect;
			Payments.Update(payment,true);
			Assert.IsFalse(GlobalPaymentsV2.HasSamePaymentOnDate(patient.PatNum,20,DateTime.Today),"Only Global Payments V2 payments count.");
			RecurringCharge recurringCharge=new RecurringCharge { PatNum=patient.PatNum,CreditCardNum=9001,ChargeAmt=30,DateTimeCharge=DateTime.Now,
				ChargeStatus=RecurringChargeStatus.ChargeSuccessful,ErrorMsg="" };
			RecurringCharges.Insert(recurringCharge);
			Assert.IsTrue(GlobalPaymentsV2.HasSameRecurringChargeOnDate(9001,30,DateTime.Today,0));
			Assert.IsFalse(GlobalPaymentsV2.HasSameRecurringChargeOnDate(9001,30,DateTime.Today,recurringCharge.RecurringChargeNum),"Not itself.");
			Assert.IsFalse(GlobalPaymentsV2.HasSameRecurringChargeOnDate(9001,31,DateTime.Today,0));
			Assert.IsFalse(GlobalPaymentsV2.HasSameRecurringChargeOnDate(9002,30,DateTime.Today,0));
		}

		[TestMethod]
		public void SaveCard_KeepsTheTokenAndLastFourOnly() {
			Patient patient=PatientT.CreatePatient("vgpcard",lName:"Vgp",fName:"Card");
			GlobalPaymentsV2Result result=new GlobalPaymentsV2Result { TransType=GpV2TransType.Sale,Outcome=GpV2Outcome.Approved,Amount=10m,
				TransactionId="TRN_c",CardLast4="4242",PaymentMethodId="PMT_card_1",ExpMonth=8,ExpYear=2029 };
			CreditCard creditCard=GlobalPaymentsV2.SaveCard(patient.PatNum,0,result);
			Assert.IsNotNull(creditCard);
			CreditCard creditCardDb=CreditCards.GetOne(creditCard.CreditCardNum);
			Assert.AreEqual("PMT_card_1",creditCardDb.XChargeToken);
			Assert.AreEqual(CreditCardSource.MeetInTheCloudTerminal,creditCardDb.CCSource);
			Assert.AreEqual("XXXXXXXXXXXX4242",creditCardDb.CCNumberMasked);
			Assert.AreEqual(new DateTime(2029,8,1),creditCardDb.CCExpiration);
			Assert.IsTrue(GlobalPaymentsV2.IsGlobalPaymentsV2Card(creditCardDb));
			Assert.IsNull(GlobalPaymentsV2.SaveCard(patient.PatNum,0,result),"The same token is saved once.");
			result.PaymentMethodId="";
			Assert.IsNull(GlobalPaymentsV2.SaveCard(patient.PatNum,0,result),"Nothing to save without a reusable card reference.");
			result.PaymentMethodId="PMT_card_2";
			result.Outcome=GpV2Outcome.Declined;
			Assert.IsNull(GlobalPaymentsV2.SaveCard(patient.PatNum,0,result),"Only approved transactions.");
			Assert.AreEqual(1,CreditCards.Refresh(patient.PatNum).Count);
		}

		#region Recurring charges
		private RecurringChargeData MakeChargeData(Patient patient,CreditCard creditCard,double amount) {
			RecurringChargeData recurringChargeData=new RecurringChargeData();
			recurringChargeData.RecurringCharge=new RecurringCharge();
			recurringChargeData.RecurringCharge.PatNum=patient.PatNum;
			recurringChargeData.RecurringCharge.ClinicNum=0;
			recurringChargeData.RecurringCharge.ChargeAmt=amount;
			recurringChargeData.RecurringCharge.CreditCardNum=creditCard.CreditCardNum;
			recurringChargeData.RecurringCharge.ChargeStatus=RecurringChargeStatus.NotYetCharged;
			recurringChargeData.RecurringCharge.ErrorMsg="";
			recurringChargeData.RecurringChargeDate=DateTime.Today;
			recurringChargeData.DateStart=DateTime.Today;
			recurringChargeData.PatName=patient.GetNameLF();
			recurringChargeData.Guarantor=patient.Guarantor;
			recurringChargeData.XChargeToken=creditCard.XChargeToken;
			recurringChargeData.CCNumberMasked=creditCard.CCNumberMasked;
			recurringChargeData.CCSource=creditCard.CCSource;
			recurringChargeData.CCIsRecurringActive=true;
			recurringChargeData.Procedures="";
			return recurringChargeData;
		}

		private (Patient,CreditCard) MakePatientWithCard(string suffix) {
			Patient patient=PatientT.CreatePatient(suffix,lName:"Vgp",fName:"Recurring");
			CreditCard creditCard=new CreditCard { PatNum=patient.PatNum,XChargeToken="PMT_rc_"+suffix,CCNumberMasked="XXXXXXXXXXXX1881",
				CCSource=CreditCardSource.MeetInTheCloudTerminal,ChargeAmt=30,DateStart=DateTime.Today,Procedures="",ChargeFrequency="" };
			CreditCards.Insert(creditCard);
			return (patient,creditCard);
		}

		private void SetUpV2ForRecurring() {
			SetCredentials(0,"hq-app",APP_KEY_HQ,"acct");
			SetEnabled(ProgramName.MeetInTheCloud,true);
		}

		private static List<Payment> GetPayments(long patNum) {
			return Payments.Refresh(patNum);
		}

		[TestMethod]
		public void Recurring_ApprovedCharge_GetsAPaymentWithTheTransaction() {
			SetUpV2ForRecurring();
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc1");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc1","CAPTURED","3000"));
			RecurringChargerator recurringChargerator=new RecurringChargerator(new TestLog(),isManual:true);
			RecurringChargeData recurringChargeData=MakeChargeData(patient,creditCard,30);
			recurringChargerator.SendCharges(new List<RecurringChargeData> { recurringChargeData },forceDuplicates:false);
			Assert.AreEqual(RecurringChargeStatus.ChargeSuccessful,recurringChargeData.RecurringCharge.ChargeStatus,
				recurringChargeData.RecurringCharge.ErrorMsg);
			Assert.AreEqual(1,recurringChargerator.Success);
			Payment payment=GetPayments(patient.PatNum).Single();
			Assert.AreEqual(30,payment.PayAmt,0.001);
			Assert.AreEqual(CreditCardSource.MeetInTheCloudTerminal,payment.PaymentSource);
			Assert.AreEqual("TRN_rc1",payment.ExternalId);
			Assert.IsTrue(payment.IsRecurringCC);
			Assert.IsTrue(payment.IsCcCompleted);
			StringAssert.Contains(payment.PayNote,"Global Payments V2");
			Assert.IsFalse(payment.PayNote.Contains(creditCard.XChargeToken),"The card token is never written to the note.");
			Assert.IsFalse(payment.Receipt.Contains(creditCard.XChargeToken));
			GpRequest gpRequest=_gpFakeHttp.ApiRequests.Single();
			Assert.AreEqual(creditCard.XChargeToken,(string)gpRequest.GetJson()["payment_method"]["id"]);
			Assert.AreEqual(GlobalPaymentsV2.GetRecurringReference(recurringChargeData.RecurringCharge.RecurringChargeNum,DateTime.Now),
				gpRequest.GetHeader("X-GP-Idempotency"));
			Assert.AreEqual("acct",(string)gpRequest.GetJson()["account_name"]);
		}

		[TestMethod]
		public void Recurring_SameCardSameAmountSameDay_IsNotChargedTwiceUnlessForced() {
			SetUpV2ForRecurring();
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc2");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc2","CAPTURED","3000"));
			new RecurringChargerator(new TestLog(),isManual:true).SendCharges(new List<RecurringChargeData> { MakeChargeData(patient,creditCard,30) },false);
			RecurringChargerator recurringChargerator=new RecurringChargerator(new TestLog(),isManual:true);
			RecurringChargeData recurringChargeData=MakeChargeData(patient,creditCard,30);
			recurringChargerator.SendCharges(new List<RecurringChargeData> { recurringChargeData },forceDuplicates:false);
			Assert.AreEqual(RecurringChargeStatus.ChargeFailed,recurringChargeData.RecurringCharge.ChargeStatus);
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,"already charged the same amount today");
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count,"Nothing sent for the duplicate.");
			Assert.AreEqual(1,GetPayments(patient.PatNum).Count);
			//Forced: charged again.
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc3","CAPTURED","3000"));
			recurringChargeData=MakeChargeData(patient,creditCard,30);
			new RecurringChargerator(new TestLog(),isManual:true).SendCharges(new List<RecurringChargeData> { recurringChargeData },forceDuplicates:true);
			Assert.AreEqual(RecurringChargeStatus.ChargeSuccessful,recurringChargeData.RecurringCharge.ChargeStatus,recurringChargeData.RecurringCharge.ErrorMsg);
			Assert.AreEqual(2,_gpFakeHttp.ApiRequests.Count);
		}

		[TestMethod]
		public void Recurring_Declined_GetsAZeroPayment() {
			SetUpV2ForRecurring();
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc4");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc4","DECLINED","3000","05"));
			RecurringChargerator recurringChargerator=new RecurringChargerator(new TestLog(),isManual:true);
			RecurringChargeData recurringChargeData=MakeChargeData(patient,creditCard,30);
			recurringChargerator.SendCharges(new List<RecurringChargeData> { recurringChargeData },false);
			Assert.AreNotEqual(RecurringChargeStatus.ChargeSuccessful,recurringChargeData.RecurringCharge.ChargeStatus);
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,"Response from Global Payments V2");
			Payment payment=GetPayments(patient.PatNum).Single();
			Assert.AreEqual(0,payment.PayAmt,0.001,"A $0 payment records that the charge was tried.");
			Assert.IsFalse(payment.IsCcCompleted);
		}

		[TestMethod]
		public void Recurring_NoAnswer_IsFailedWithNoPaymentAndNotRetried() {
			SetUpV2ForRecurring();
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc5");
			_gpFakeHttp.Enqueue(HttpStatusCode.InternalServerError,"");
			RecurringChargerator recurringChargerator=new RecurringChargerator(new TestLog(),isManual:true);
			RecurringChargeData recurringChargeData=MakeChargeData(patient,creditCard,30);
			recurringChargerator.SendCharges(new List<RecurringChargeData> { recurringChargeData },false);
			Assert.AreEqual(RecurringChargeStatus.ChargeFailed,recurringChargeData.RecurringCharge.ChargeStatus);
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,"Check the card processor's records");
			Assert.AreEqual(0,GetPayments(patient.PatNum).Count);
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count);
		}

		private static RecurringChargeData RunOne(RecurringChargeData recurringChargeData,bool isManual,bool forceDuplicates) {
			new RecurringChargerator(new TestLog(),isManual).SendCharges(new List<RecurringChargeData> { recurringChargeData },forceDuplicates);
			return recurringChargeData;
		}

		[TestMethod]
		public void Recurring_NoAnswer_BlocksEveryLaterRun_UntilAForcedRetryWithTheSameKey() {
			SetUpV2ForRecurring();
			//The clinic forces duplicates on automated runs: that must not retry a charge in doubt.
			VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropForceRecurringCharge,0,"1");
			Refresh();
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc7");
			//Global Payments took the money but the answer was lost (5xx).
			_gpFakeHttp.Enqueue(HttpStatusCode.InternalServerError,"");
			RecurringChargeData recurringChargeDataFirst=RunOne(MakeChargeData(patient,creditCard,30),isManual:false,forceDuplicates:false);
			Assert.AreEqual(RecurringChargeStatus.ChargeFailed,recurringChargeDataFirst.RecurringCharge.ChargeStatus);
			string keyFirst=_gpFakeHttp.ApiRequests.Single().GetHeader("X-GP-Idempotency");
			StringAssert.Contains(recurringChargeDataFirst.RecurringCharge.ErrorMsg,GlobalPaymentsV2.BuildRecurringInDoubtTag(keyFirst));
			Assert.AreEqual(recurringChargeDataFirst.RecurringCharge.RecurringChargeNum,
				GlobalPaymentsV2.GetRecurringChargeInDoubt(creditCard.CreditCardNum)?.RecurringChargeNum);
			//The next automated run, the next manual run, and an automated run days later: nothing is sent.
			RecurringChargeData recurringChargeData=RunOne(MakeChargeData(patient,creditCard,30),isManual:false,forceDuplicates:false);
			Assert.AreEqual(RecurringChargeStatus.ChargeFailed,recurringChargeData.RecurringCharge.ChargeStatus);
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,"unknown outcome");
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,keyFirst,"Names the reference to look up.");
			Assert.IsFalse(recurringChargeData.RecurringCharge.ErrorMsg.Contains("["+GlobalPaymentsV2.RecurringInDoubtTag),
				"A blocked run sent nothing, so it is not itself in doubt.");
			RunOne(MakeChargeData(patient,creditCard,30),isManual:true,forceDuplicates:false);
			DataCore.NonQ("UPDATE recurringcharge SET DateTimeCharge="+POut.DateT(DateTime.Now.AddHours(-20))
				+" WHERE RecurringChargeNum="+POut.Long(recurringChargeDataFirst.RecurringCharge.RecurringChargeNum));
			RunOne(MakeChargeData(patient,creditCard,30),isManual:false,forceDuplicates:true);
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count,"Never charged again without a person forcing it.");
			Assert.AreEqual(0,GetPayments(patient.PatNum).Count);
			//A person checked and forces it: the same request again (same reference and idempotency key), so Global Payments can answer with the first
			//result. Here it reports the capture, which is recorded once.
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc7","CAPTURED","3000"));
			recurringChargeData=RunOne(MakeChargeData(patient,creditCard,30),isManual:true,forceDuplicates:true);
			Assert.AreEqual(RecurringChargeStatus.ChargeSuccessful,recurringChargeData.RecurringCharge.ChargeStatus,recurringChargeData.RecurringCharge.ErrorMsg);
			Assert.AreEqual(2,_gpFakeHttp.ApiRequests.Count);
			Assert.AreEqual(keyFirst,_gpFakeHttp.ApiRequests[1].GetHeader("X-GP-Idempotency"));
			Assert.AreEqual(keyFirst,(string)_gpFakeHttp.ApiRequests[1].GetJson()["reference"]);
			Assert.AreEqual("TRN_rc7",GetPayments(patient.PatNum).Single().ExternalId);
			Assert.IsNull(GlobalPaymentsV2.GetRecurringChargeInDoubt(creditCard.CreditCardNum),"Settled by the definite answer.");
			//Settled: the same-day duplicate rule applies again as usual (the clinic forces duplicates on automated runs).
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc7b","CAPTURED","3000"));
			recurringChargeData=RunOne(MakeChargeData(patient,creditCard,30),isManual:true,forceDuplicates:false);
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,"already charged the same amount today");
			Assert.AreEqual(2,_gpFakeHttp.ApiRequests.Count);
		}

		[TestMethod]
		public void Recurring_InDoubtFromMoreThanADayAgo_ForcedRetryIsANewRequest_AndADeclineSettlesIt() {
			SetUpV2ForRecurring();
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc8");
			_gpFakeHttp.Enqueue(HttpStatusCode.InternalServerError,"");
			RecurringChargeData recurringChargeDataFirst=RunOne(MakeChargeData(patient,creditCard,30),isManual:true,forceDuplicates:false);
			string keyFirst=_gpFakeHttp.ApiRequests.Single().GetHeader("X-GP-Idempotency");
			DataCore.NonQ("UPDATE recurringcharge SET DateTimeCharge="+POut.DateT(DateTime.Now.AddDays(-3))
				+" WHERE RecurringChargeNum="+POut.Long(recurringChargeDataFirst.RecurringCharge.RecurringChargeNum));
			RunOne(MakeChargeData(patient,creditCard,30),isManual:false,forceDuplicates:false);
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count,"Still blocked days later.");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_rc8","DECLINED","3000","05"));
			RecurringChargeData recurringChargeData=RunOne(MakeChargeData(patient,creditCard,30),isManual:true,forceDuplicates:true);
			Assert.AreEqual(2,_gpFakeHttp.ApiRequests.Count);
			string keySecond=_gpFakeHttp.ApiRequests[1].GetHeader("X-GP-Idempotency");
			Assert.AreNotEqual(keyFirst,keySecond,"Too old to be the same request: a new one.");
			Assert.AreEqual(GlobalPaymentsV2.GetRecurringReference(recurringChargeData.RecurringCharge.RecurringChargeNum,DateTime.Now),keySecond);
			Assert.AreEqual(0,GetPayments(patient.PatNum).Single().PayAmt,0.001,"The decline's $0 payment.");
			Assert.IsNull(GlobalPaymentsV2.GetRecurringChargeInDoubt(creditCard.CreditCardNum),"A decline is a definite answer on the card.");
		}

		[TestMethod]
		public void Recurring_ClinicFollowingHeadquarters_GetsHeadquartersPaymentType() {
			long clinicNumFollows=InsertClinic("VGP clinic R1");
			long clinicNumOwn=InsertClinic("VGP clinic R2");
			SetClinicsEnabled(true);
			long payTypeCCOld=PrefC.GetLong(PrefName.RecurringChargesPayTypeCC);
			try {
				Prefs.UpdateLong(PrefName.RecurringChargesPayTypeCC,0);
				Prefs.RefreshCache();
				Def defHq=DefT.CreateDefinition(DefCat.PaymentTypes,"VGP HQ card type");
				Def defOwn=DefT.CreateDefinition(DefCat.PaymentTypes,"VGP clinic card type");
				SetCredentials(0,"hq-app",APP_KEY_HQ,"acct",defHq.DefNum);
				//The setup window writes no clinic row for a value that equals Headquarters'; only clinicNumOwn has its own payment type.
				VendorCredentials.WriteSetting(ProgramNum,GlobalPaymentsV2.PropPaymentType,clinicNumOwn,POut.Long(defOwn.DefNum));
				Refresh();
				SetEnabled(ProgramName.MeetInTheCloud,true);
				foreach((long clinicNum,long defNumExpected,string suffix) in new[] { (clinicNumFollows,defHq.DefNum,"vgprc9"),(clinicNumOwn,defOwn.DefNum,"vgprc10") }) {
					(Patient patient,CreditCard creditCard)=MakePatientWithCard(suffix);
					_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_"+suffix,"CAPTURED","3000"));
					RecurringChargeData recurringChargeData=MakeChargeData(patient,creditCard,30);
					recurringChargeData.RecurringCharge.ClinicNum=clinicNum;
					RunOne(recurringChargeData,isManual:true,forceDuplicates:false);
					Assert.AreEqual(RecurringChargeStatus.ChargeSuccessful,recurringChargeData.RecurringCharge.ChargeStatus,recurringChargeData.RecurringCharge.ErrorMsg);
					Payment payment=GetPayments(patient.PatNum).Single();
					Assert.AreEqual(clinicNum,payment.ClinicNum);
					Assert.AreEqual(defNumExpected,payment.PayType,"The payment type the payment window would use for this clinic.");
					Assert.AreEqual(GlobalPaymentsV2.GetConfig(clinicNum).PaymentTypeDefNum,payment.PayType);
				}
			}
			finally {
				Prefs.UpdateLong(PrefName.RecurringChargesPayTypeCC,payTypeCCOld);
				Prefs.RefreshCache();
				SetClinicsEnabled(false);
			}
		}

		[TestMethod]
		public void InDoubtTag_RoundTrips() {
			string errorMsg="Error from Global Payments V2: x\r\n"+GlobalPaymentsV2.BuildRecurringInDoubtTag("RC12-20261031");
			Assert.AreEqual("RC12-20261031",GlobalPaymentsV2.GetReferenceFromInDoubtTag(errorMsg));
			Assert.AreEqual("",GlobalPaymentsV2.GetReferenceFromInDoubtTag("Error from Global Payments V2: declined"));
			Assert.AreEqual("",GlobalPaymentsV2.GetReferenceFromInDoubtTag(null));
		}

		[TestMethod]
		public void Recurring_V2Disabled_CardIsNotSentAnywhere() {
			SetCredentials(0,"hq-app",APP_KEY_HQ,"acct");//Set up but not enabled.
			(Patient patient,CreditCard creditCard)=MakePatientWithCard("vgprc6");
			RecurringChargeData recurringChargeData=MakeChargeData(patient,creditCard,30);
			new RecurringChargerator(new TestLog(),isManual:true).SendCharges(new List<RecurringChargeData> { recurringChargeData },false);
			Assert.AreEqual(0,_gpFakeHttp.Requests.Count);
			Assert.AreEqual(0,GetPayments(patient.PatNum).Count);
		}
		#endregion Recurring charges

		///<summary>Keeps what the recurring-charge run logs, so nothing is written to disk.</summary>
		private class TestLog:Logger.IWriteLine {
			public List<string> ListLines=new List<string>();
			public LogLevel LogLevel { get; set; }=LogLevel.Verbose;

			public void WriteLine(string data,LogLevel logLevel,string subDirectory="") {
				ListLines.Add(data);
			}
		}
	}
}
