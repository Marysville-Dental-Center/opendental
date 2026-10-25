// 25.4 v25_4-account-recurringcharge-fail-email: the e-mail sent when a recurring charge is declined. Synthetic patients, cards and charges on
// the scratch database; the sender is replaced, so nothing is sent over the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations.Seeds26_1;
using UnitTestsCore;

namespace M2RECURTests {
	[TestClass]
	public class FailEmailTests {
		private const string OFFICE_ADDRESS="billing@office.example.com";
		private const string SUBJECT="Card [CardLast4] for [NameF]";
		private const string BODY="Hi [NamePref], [Amount] on [DateCharge] was declined. Call [OfficeName] at [OfficePhone]. [ClinicName]";
		///<summary>The messages the replaced sender received, in order.</summary>
		private List<EmailMessage> _listEmailMessagesSent;
		private EmailAddress _emailAddressOffice;
		private DateTime _dateTimeRun;

		[TestInitialize]
		public void TestInitialize() {
			RecurDb.Begin();
			RecurDb.LogInWith(EnumPermType.EmailSend);
			RecurDb.ClearOfficeEmailAddresses();
			_emailAddressOffice=RecurDb.SetOfficeEmailAddress(OFFICE_ADDRESS);
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,true);
			Prefs.UpdateString(PrefName.RecurringChargesFailEmailSubject,SUBJECT);
			Prefs.UpdateString(PrefName.RecurringChargesFailEmailBody,BODY);
			Prefs.UpdateString(PrefName.PracticeTitle,"Synthetic Dental");
			_listEmailMessagesSent=new List<EmailMessage>();
			_dateTimeRun=MiscData.GetNowDateTime();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(RecurDb.SkipReason!=null) {
				return;
			}
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,false);
		}

		#region Helpers
		///<summary>Like EmailMessages.SendEmail on success: the message is saved as sent.</summary>
		private void SendOk(EmailMessage emailMessage,EmailAddress emailAddress) {
			Assert.AreEqual(OFFICE_ADDRESS,emailAddress.GetFrom(),"Sent from the office's own address.");
			_listEmailMessagesSent.Add(emailMessage);
			EmailMessages.Insert(emailMessage);
		}

		///<summary>Like EmailMessages.SendEmail when the server refuses: the message is saved as failed and the error is thrown.</summary>
		private void SendFails(EmailMessage emailMessage,EmailAddress emailAddress) {
			Exception exception=new Exception("Mailbox unavailable (synthetic)");
			EmailMessages.SetFailed(emailMessage,exception);
			EmailMessages.Insert(emailMessage);
			throw exception;
		}

		///<summary>A card on file and its recurringcharge row as the chargerator inserts it, processed and declined.</summary>
		private RecurringChargeData MakeDeclinedCharge(Patient patient,double amount,DateTime dateTimeCharge) {
			CreditCard creditCard=CreditCardT.CreateCard(patient.PatNum,amount,DateTime.Today.AddMonths(-2),0);
			return MakeDeclinedCharge(patient,creditCard,amount,dateTimeCharge);
		}

		private RecurringChargeData MakeDeclinedCharge(Patient patient,CreditCard creditCard,double amount,DateTime dateTimeCharge) {
			RecurringCharge recurringCharge=new RecurringCharge {
				PatNum=patient.PatNum,
				ChargeAmt=amount,
				ChargeStatus=RecurringChargeStatus.ChargeDeclined,
				CreditCardNum=creditCard.CreditCardNum,
				DateTimeCharge=dateTimeCharge,
				ErrorMsg="Response from processor: Declined",
			};
			RecurringCharges.Insert(recurringCharge);
			return new RecurringChargeData {
				RecurringCharge=recurringCharge,
				CCNumberMasked=creditCard.CCNumberMasked,
				PatName=patient.GetNameLF(),
				IsDeclinedByProcessor=true,
			};
		}

		private List<Commlog> GetCommlogs(long patNum) {
			return Commlogs.Refresh(patNum);
		}

		private List<EmailMessage> GetEmailMessages(long patNum) {
			return OpenDentBusiness.Crud.EmailMessageCrud.SelectMany("SELECT * FROM emailmessage WHERE PatNum="+POut.Long(patNum));
		}
		#endregion Helpers

		[TestMethod]
		public void Seed_ThePackagesPreferenceRowsExist() {
			//The migration seeded them (M2-02 owns the rows and checks their defaults); this package only reads and writes them.
			string command="SELECT COUNT(*) FROM preference WHERE PrefName IN('"+nameof(PrefName.RecurringChargesFailEmailEnabled)+"','"
				+nameof(PrefName.RecurringChargesFailEmailSubject)+"','"+nameof(PrefName.RecurringChargesFailEmailBody)+"','"
				+nameof(PrefName.RepeatingChargesAllowBackdating)+"')";
			Assert.AreEqual("4",DataCore.GetScalar(command));
			CollectionAssert.AreEqual(new[] { "[NameF]","[NamePref]","[Amount]","[CardLast4]","[DateCharge]","[OfficeName]","[OfficePhone]","[ClinicName]" },
				RecurringChargeFailEmails.ListTags.ToArray(),"The spec's eight tags.");
		}

		[TestMethod]
		public void Declined_PatientWithEmail_GetsOneEmailWithTheTagsReplaced() {
			Patient patient=RecurDb.CreatePatient("Mail",email:"pat.mail@example.com",preferredName:"Sunny");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,45.5,_dateTimeRun);
			RecurringChargeFailEmailResult result=RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual(1,_listEmailMessagesSent.Count);
			EmailMessage emailMessage=_listEmailMessagesSent[0];
			Assert.AreEqual("pat.mail@example.com",emailMessage.ToAddress);
			Assert.AreEqual(OFFICE_ADDRESS,emailMessage.FromAddress);
			Assert.AreEqual("Card 1234 for "+patient.FName,emailMessage.Subject);
			Assert.AreEqual("Hi Sunny, "+45.5.ToString("c")+" on "+_dateTimeRun.ToShortDateString()+" was declined. Call Synthetic Dental at "
				+Clinics.GetOfficePhone(null)+". Synthetic Dental",emailMessage.BodyText);
			Assert.AreEqual(EmailSentOrReceived.Sent,emailMessage.SentOrReceived);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
			Assert.AreEqual(Security.CurUser.UserNum,emailMessage.UserNum);
			//Saved with the patient, with a commlog, and the outcome on the charge row.
			List<EmailMessage> listEmailMessagesDb=GetEmailMessages(patient.PatNum);
			Assert.AreEqual(1,listEmailMessagesDb.Count);
			Assert.AreEqual(EmailMessageSource.PaymentReceipt,listEmailMessagesDb[0].MsgType);
			List<Commlog> listCommlogs=GetCommlogs(patient.PatNum);
			Assert.AreEqual(1,listCommlogs.Count);
			Assert.AreEqual(CommItemMode.Email,listCommlogs[0].Mode_);
			Assert.AreEqual(CommSentOrReceived.Sent,listCommlogs[0].SentOrReceived);
			StringAssert.Contains(listCommlogs[0].Note,"pat.mail@example.com");
			StringAssert.Contains(listCommlogs[0].Note,"1234");
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Response from processor: Declined","The decline note stays.");
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX+"pat.mail@example.com.");
		}

		[TestMethod]
		public void Declined_PatientWithoutEmail_GoesToTheGuarantor() {
			Patient patientGuarantor=RecurDb.CreatePatient("Guar",email:"guar@example.com");
			Patient patientChild=RecurDb.CreatePatient("Child",guarantor:patientGuarantor.PatNum);
			RecurringChargeData chargeData=MakeDeclinedCharge(patientChild,20,_dateTimeRun);
			RecurringChargeFailEmailResult result=RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual("guar@example.com",_listEmailMessagesSent.Single().ToAddress);
			StringAssert.EndsWith(_listEmailMessagesSent[0].Subject,patientGuarantor.FName,"The name tags describe the person the e-mail is addressed to.");
			Assert.AreEqual(patientChild.PatNum,_listEmailMessagesSent[0].PatNum,"Filed with the patient whose charge was declined.");
			Assert.AreEqual(patientGuarantor.PatNum,_listEmailMessagesSent[0].PatNumSubj);
			Assert.AreEqual(1,GetCommlogs(patientChild.PatNum).Count);
		}

		[TestMethod]
		public void Declined_NoAddressAnywhere_NotSent() {
			Patient patientGuarantor=RecurDb.CreatePatient("NoMailG",email:"not an address");
			Patient patient=RecurDb.CreatePatient("NoMail",guarantor:patientGuarantor.PatNum);
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			RecurringChargeFailEmailResult result=RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,result.Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			Assert.AreEqual(0,GetCommlogs(patient.PatNum).Count);
			Assert.AreEqual(0,GetEmailMessages(patient.PatNum).Count);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_NOT_SENT_PREFIX);
		}

		[TestMethod]
		public void Declined_PatientOptedOutOfEmail_NotSent() {
			Patient patient=RecurDb.CreatePatient("OptOut",email:"optout@example.com");
			CommOptOutT.Create(patient,CommOptOutType.All,CommOptOutMode.Email);
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void Declined_PatientOptedOutOfTextOnly_StillSent() {
			Patient patient=RecurDb.CreatePatient("OptText",email:"opttext@example.com");
			CommOptOutT.Create(patient,CommOptOutType.All,CommOptOutMode.Text);
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
		}

		[TestMethod]
		public void Declined_GuarantorOptedOut_NotSent() {
			Patient patientGuarantor=RecurDb.CreatePatient("OptG",email:"optg@example.com");
			CommOptOutT.Create(patientGuarantor,CommOptOutType.All,CommOptOutMode.Email);
			Patient patient=RecurDb.CreatePatient("OptGChild",guarantor:patientGuarantor.PatNum);
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void FailedButNotDeclined_NoEmail() {
			//An office setup problem, or an outcome the processor never confirmed: not the patient's to fix, and maybe charged.
			Patient patient=RecurDb.CreatePatient("Failed",email:"failed@example.com");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			chargeData.IsDeclinedByProcessor=false;
			chargeData.RecurringCharge.ChargeStatus=RecurringChargeStatus.ChargeFailed;
			string errorMsgBefore=chargeData.RecurringCharge.ErrorMsg;
			RecurringChargeFailEmailResult result=RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,result.Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			Assert.AreEqual(errorMsgBefore,chargeData.RecurringCharge.ErrorMsg,"Nothing is added when no e-mail is due.");
		}

		[TestMethod]
		public void TurnedOff_NoEmail() {
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,false);
			Patient patient=RecurDb.CreatePatient("Off",email:"off@example.com");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void QuietPeriod_SameCardNotEmailedAgainWithinSevenDays() {
			Patient patient=RecurDb.CreatePatient("Quiet",email:"quiet@example.com");
			CreditCard creditCard=CreditCardT.CreateCard(patient.PatNum,30,DateTime.Today.AddMonths(-2),0);
			RecurringChargeData chargeData1=MakeDeclinedCharge(patient,creditCard,30,_dateTimeRun.AddDays(-2));
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,
				RecurringChargeFailEmails.SendIfNeeded(chargeData1,_dateTimeRun.AddDays(-2),SendOk).Status);
			RecurringCharges.Update(chargeData1.RecurringCharge);//The chargerator saves the row with the line the e-mail added.
			//The retry two days later.
			RecurringChargeData chargeData2=MakeDeclinedCharge(patient,creditCard,30,_dateTimeRun);
			RecurringChargeFailEmailResult result2=RecurringChargeFailEmails.SendIfNeeded(chargeData2,_dateTimeRun,SendOk);
			Assert.AreEqual(RecurringChargeFailEmailStatus.SkippedQuietPeriod,result2.Status);
			Assert.AreEqual(1,_listEmailMessagesSent.Count);
			StringAssert.Contains(chargeData2.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_NOT_SENT_PREFIX);
			RecurringCharges.Update(chargeData2.RecurringCharge);
			//Another card of the same patient is its own charge.
			RecurringChargeData chargeDataOtherCard=MakeDeclinedCharge(patient,30,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,RecurringChargeFailEmails.SendIfNeeded(chargeDataOtherCard,_dateTimeRun,SendOk).Status);
			//Eight days after the first e-mail the quiet period is over (the skipped retry does not extend it).
			RecurringChargeData chargeData3=MakeDeclinedCharge(patient,creditCard,30,_dateTimeRun.AddDays(6));
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,RecurringChargeFailEmails.SendIfNeeded(chargeData3,_dateTimeRun.AddDays(6),SendOk).Status);
			Assert.AreEqual(3,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void NoOfficeAddress_NotSent() {
			RecurDb.ClearOfficeEmailAddresses();
			Patient patient=RecurDb.CreatePatient("NoOffice",email:"nooffice@example.com");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			RecurringChargeFailEmailResult result=RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,result.Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void UserWithoutEmailSendPermission_NotSent() {
			RecurDb.LogInWith(EnumPermType.PaymentCreate);
			Patient patient=RecurDb.CreatePatient("NoPerm",email:"noperm@example.com");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void AmountUnderOneDollar_IsReplacedLiterally() {
			//Regex replacement text treats "$0" as the whole match; the amount must come out as written.
			Patient patient=RecurDb.CreatePatient("Cents",email:"cents@example.com");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,0.5,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
			StringAssert.Contains(_listEmailMessagesSent[0].BodyText,0.5.ToString("c")+" on ");
			Assert.IsFalse(_listEmailMessagesSent[0].BodyText.Contains("[Amount]"));
		}

		[TestMethod]
		public void BlankTemplate_UsesTheDefaultText() {
			Prefs.UpdateString(PrefName.RecurringChargesFailEmailSubject,"");
			Prefs.UpdateString(PrefName.RecurringChargesFailEmailBody,"  ");
			Patient patient=RecurDb.CreatePatient("Blank",email:"blank@example.com");
			RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,RecurringChargeFailEmails.SendIfNeeded(chargeData,_dateTimeRun,SendOk).Status);
			Assert.AreEqual(Seed26_1.RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT,_listEmailMessagesSent[0].Subject);
			StringAssert.Contains(_listEmailMessagesSent[0].BodyText,"1234");
		}

		[TestMethod]
		public void Chargerator_SendFailureIsCountedAndTheBatchGoesOn() {
			Patient patient1=RecurDb.CreatePatient("Batch1",email:"batch1@example.com");
			Patient patient2=RecurDb.CreatePatient("Batch2",email:"batch2@example.com");
			Patient patientNoMail=RecurDb.CreatePatient("Batch3");
			TestLog testLog=new TestLog();
			RecurringChargerator recurringChargerator=new RecurringChargerator(testLog,true);
			int countCalls=0;
			recurringChargerator.ActionSendFailEmail=(emailMessage,emailAddress) => {
				countCalls++;
				if(countCalls==1) {
					SendFails(emailMessage,emailAddress);
				}
				SendOk(emailMessage,emailAddress);
			};
			RecurringChargeData chargeData1=MakeDeclinedCharge(patient1,20,_dateTimeRun);
			RecurringChargeData chargeData2=MakeDeclinedCharge(patient2,25,_dateTimeRun);
			RecurringChargeData chargeData3=MakeDeclinedCharge(patientNoMail,30,_dateTimeRun);
			RecurringChargeData chargeDataSuccess=MakeDeclinedCharge(patient2,35,_dateTimeRun);
			chargeDataSuccess.IsDeclinedByProcessor=false;
			chargeDataSuccess.RecurringCharge.ChargeStatus=RecurringChargeStatus.ChargeSuccessful;
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,recurringChargerator.SendFailEmailIfNeeded(chargeData1).Status);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,recurringChargerator.SendFailEmailIfNeeded(chargeData2).Status);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotSent,recurringChargerator.SendFailEmailIfNeeded(chargeData3).Status);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,recurringChargerator.SendFailEmailIfNeeded(chargeDataSuccess).Status);
			Assert.AreEqual(1,recurringChargerator.FailEmailsSent);
			Assert.AreEqual(2,recurringChargerator.FailEmailsNotSent);
			Assert.AreEqual(0,recurringChargerator.FailEmailsSkipped);
			//The failed send is saved as failed, with the server's answer, and nothing claims it was sent.
			EmailMessage emailMessageFailed=GetEmailMessages(patient1.PatNum).Single();
			Assert.AreEqual(EmailSentOrReceived.SendFailed,emailMessageFailed.SentOrReceived);
			StringAssert.Contains(emailMessageFailed.FailReason,"Mailbox unavailable");
			Assert.AreEqual(0,GetCommlogs(patient1.PatNum).Count);
			StringAssert.Contains(chargeData1.RecurringCharge.ErrorMsg,"Mailbox unavailable");
			//Logged without a message box (the manual run shows only Error lines).
			Assert.AreEqual(2,testLog.ListLines.Count(x => x.Data.Contains("Failure e-mail not sent")));
			Assert.IsTrue(testLog.ListLines.All(x => x.LogLevel!=LogLevel.Error));
			string summary=recurringChargerator.GetFailEmailSummary();
			StringAssert.Contains(summary,"Failure e-mails sent: 1");
			StringAssert.Contains(summary,"Failure e-mails not sent: 2");
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,false);
			Assert.AreEqual("",recurringChargerator.GetFailEmailSummary(),"No summary line when the e-mail is turned off.");
		}

		[TestMethod]
		public void Chargerator_MarkDeclinedFlagsTheChargeWhateverTheInactivatePreference() {
			//With "inactivate declined cards" off, a decline keeps the Failed status; it is still a decline and still e-mailed.
			Prefs.UpdateBool(PrefName.RecurringChargesInactivateDeclinedCards,false);
			try {
				Patient patient=RecurDb.CreatePatient("Flag",email:"flag@example.com");
				RecurringChargeData chargeData=MakeDeclinedCharge(patient,20,_dateTimeRun);
				chargeData.IsDeclinedByProcessor=false;
				chargeData.RecurringCharge.ChargeStatus=RecurringChargeStatus.NotYetCharged;
				RecurringChargerator recurringChargerator=new RecurringChargerator(new TestLog(),true);
				recurringChargerator.ActionSendFailEmail=SendOk;
				MethodInfo methodInfoMarkDeclined=typeof(RecurringChargerator).GetMethod("MarkDeclined",BindingFlags.Instance|BindingFlags.NonPublic);
				methodInfoMarkDeclined.Invoke(recurringChargerator,new object[] { chargeData,"Response from processor: Declined",LogLevel.Information });
				Assert.IsTrue(chargeData.IsDeclinedByProcessor);
				Assert.AreEqual(RecurringChargeStatus.ChargeFailed,chargeData.RecurringCharge.ChargeStatus);
				Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,recurringChargerator.SendFailEmailIfNeeded(chargeData).Status);
				//MarkFailed alone (setup problems, unknown outcomes) does not flag a decline.
				RecurringChargeData chargeDataFailed=MakeDeclinedCharge(patient,20,_dateTimeRun);
				chargeDataFailed.IsDeclinedByProcessor=false;
				chargeDataFailed.RecurringCharge.ChargeStatus=RecurringChargeStatus.NotYetCharged;
				MethodInfo methodInfoMarkFailed=typeof(RecurringChargerator).GetMethod("MarkFailed",BindingFlags.Instance|BindingFlags.NonPublic);
				methodInfoMarkFailed.Invoke(recurringChargerator,new object[] { chargeDataFailed,"PayConnect is not set up for this clinic.",LogLevel.Information });
				Assert.IsFalse(chargeDataFailed.IsDeclinedByProcessor);
				Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,recurringChargerator.SendFailEmailIfNeeded(chargeDataFailed).Status);
			}
			finally {
				Prefs.UpdateBool(PrefName.RecurringChargesInactivateDeclinedCards,true);
			}
		}
	}

	///<summary>Collects what the chargerator logs.</summary>
	public class TestLog:Logger.IWriteLine {
		public List<(string Data,LogLevel LogLevel)> ListLines=new List<(string,LogLevel)>();

		public LogLevel LogLevel { get; set; }

		public void WriteLine(string data,LogLevel logLevel,string subDirectory="") {
			ListLines.Add((data,logLevel));
		}
	}
}
