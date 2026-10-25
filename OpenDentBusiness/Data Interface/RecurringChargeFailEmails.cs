// The e-mail sent to a patient when a recurring charge is declined (Open Dental 25.4, item v25_4-account-recurringcharge-fail-email, package
// M2-RECUR in docs/specs/port-backlog.md). The e-mail goes out through the office's own e-mail address (EmailAddresses, EmailMessages), never
// through a hosted service.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeBase;
using OpenDentBusiness.ForkMigrations.Seeds26_1;

namespace OpenDentBusiness {
	///<summary>Sends the patient an e-mail when a recurring charge was declined, if the office turned this on (Preferences, Account, Rec and Rep
	///Charges, preference RecurringChargesFailEmailEnabled).
	///<para>Rules (the public 25.4 release note only says that the option exists; everything else is this fork's choice, see
	///docs/port-notes/M2-RECUR.md):</para>
	///<para>1. Only a charge the card processor answered with a decline is e-mailed (RecurringChargeData.IsDeclinedByProcessor). A charge that failed
	///because of the office's setup, or whose outcome is unknown, is not: the patient can do nothing about it, and an unknown outcome may have been
	///charged.</para>
	///<para>2. The e-mail goes to the card's patient, or to the guarantor when the patient has no valid address. Patients who opted out of e-mail
	///(commoptout, All or Statements) are skipped.</para>
	///<para>3. It is sent from the default address of the charge's clinic (or the practice), through that address's own server.</para>
	///<para>4. One e-mail per declined charge. No new e-mail for the same card for 7 days, so daily retries do not repeat it.</para>
	///<para>5. The sent e-mail is saved (emailmessage) and a commlog is added to the patient. Every outcome is added as a line to the charge's
	///ErrorMsg, which the Recurring Charges History window shows. A send failure never stops the batch.</para></summary>
	public class RecurringChargeFailEmails {
		///<summary>No second e-mail for the same card within this many days.</summary>
		public const int QUIET_PERIOD_DAYS=7;
		///<summary>Start of the ErrorMsg line written when the e-mail was sent. The quiet period searches for it, so it is never translated.</summary>
		public const string ERROR_MSG_SENT_PREFIX="Failure e-mail sent to ";
		///<summary>Start of the ErrorMsg line written when no e-mail was sent for a declined charge.</summary>
		public const string ERROR_MSG_NOT_SENT_PREFIX="Failure e-mail not sent: ";
		public const string TAG_NAME_F="[NameF]";
		public const string TAG_NAME_PREF="[NamePref]";
		public const string TAG_AMOUNT="[Amount]";
		public const string TAG_CARD_LAST4="[CardLast4]";
		public const string TAG_DATE_CHARGE="[DateCharge]";
		public const string TAG_OFFICE_NAME="[OfficeName]";
		public const string TAG_OFFICE_PHONE="[OfficePhone]";
		public const string TAG_CLINIC_NAME="[ClinicName]";
		///<summary>The replacement tags of the subject and body, in the order the template window lists them.</summary>
		public static readonly List<string> ListTags=new List<string> {
			TAG_NAME_F,TAG_NAME_PREF,TAG_AMOUNT,TAG_CARD_LAST4,TAG_DATE_CHARGE,TAG_OFFICE_NAME,TAG_OFFICE_PHONE,TAG_CLINIC_NAME
		};
		private const string LAN="RecurringChargeFailEmails";

		///<summary>True when the office turned the e-mail on. False when the preference row is missing.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return PrefC.GetBoolSilent(PrefName.RecurringChargesFailEmailEnabled,false);
		}

		///<summary>The subject template. A blank preference uses the default text.</summary>
		public static string GetSubjectTemplate() {
			Meth.NoCheckMiddleTierRole();
			string subject=PrefC.GetStringSilent(PrefName.RecurringChargesFailEmailSubject);
			if(string.IsNullOrWhiteSpace(subject)) {
				return Seed26_1.RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT;
			}
			return subject;
		}

		///<summary>The body template. A blank preference uses the default text.</summary>
		public static string GetBodyTemplate() {
			Meth.NoCheckMiddleTierRole();
			string body=PrefC.GetStringSilent(PrefName.RecurringChargesFailEmailBody);
			if(string.IsNullOrWhiteSpace(body)) {
				return Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT;
			}
			return body;
		}

		///<summary>The last four characters of a masked card number (XXXXXXXXXXXX1234 gives 1234). Blank when there is none.</summary>
		public static string GetCardLast4(string ccNumberMasked) {
			Meth.NoCheckMiddleTierRole();
			string number=(ccNumberMasked??"").Trim();
			if(number.Length<=4) {
				return number;
			}
			return number.Substring(number.Length-4);
		}

		///<summary>Replaces the tags of a template. The name tags describe the recipient. [NamePref] is the preferred name, or the first name when there
		///is none. The office tags describe the clinic (the practice when clinic is null). Other tags the office's templates use elsewhere (for example
		///[PracticeName]) are replaced as well. The e-mail is plain text, so nothing is escaped.</summary>
		public static string ReplaceTemplateTags(string template,Patient patientRecipient,double amount,string cardLast4,DateTime dateCharge,Clinic clinic) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(template)) {
				return template;
			}
			if(clinic==null) {
				clinic=Clinics.GetPracticeAsClinicZero();
			}
			string text=template;
			//Replaced literally: ReplaceTags.ReplaceOneTag passes the value to Regex.Replace as a pattern, where an amount like $0.50 would turn "$0"
			//into the tag itself.
			text=ReplaceTagLiteral(text,TAG_NAME_PREF,patientRecipient.GetNameFirstOrPreferred());
			text=ReplaceTagLiteral(text,TAG_AMOUNT,amount.ToString("c"));
			text=ReplaceTagLiteral(text,TAG_CARD_LAST4,cardLast4);
			text=ReplaceTagLiteral(text,TAG_DATE_CHARGE,dateCharge.ToShortDateString());
			//[NameF], [OfficeName], [OfficePhone], [ClinicName] and the other common tags.
			return MessageTagReplacer.ReplaceCommonTags(text,patientRecipient.FName,patientRecipient.GetNameFirstOrPreferred(),
				patientRecipient.PriProv,clinic,isEmailBody:false);
		}

		///<summary>Replaces every occurrence of the tag, ignoring case, with the value as plain text.</summary>
		private static string ReplaceTagLiteral(string text,string tag,string value) {
			return Regex.Replace(text,Regex.Escape(tag),match => value??"",RegexOptions.IgnoreCase);
		}

		///<summary>True if the patient has at least one valid e-mail address.</summary>
		public static bool HasValidEmail(Patient patient) {
			Meth.NoCheckMiddleTierRole();
			return patient!=null && EmailAddresses.GetValidAddresses(patient.Email).Count>0;
		}

		///<summary>The card's patient when they have a valid e-mail address, otherwise the guarantor when the guarantor has one, otherwise null.</summary>
		public static Patient GetRecipient(Patient patientCard,Patient patientGuarantor) {
			Meth.NoCheckMiddleTierRole();
			if(HasValidEmail(patientCard)) {
				return patientCard;
			}
			if(patientGuarantor!=null && patientCard!=null && patientGuarantor.PatNum!=patientCard.PatNum && HasValidEmail(patientGuarantor)) {
				return patientGuarantor;
			}
			return null;
		}

		///<summary>True if the patient opted out of all e-mail or of account e-mails (statements). The desktop program only offers "All".</summary>
		public static bool IsOptedOut(long patNum) {
			Meth.NoCheckMiddleTierRole();
			CommOptOut commOptOut=CommOptOuts.GetForPat(patNum);
			return commOptOut!=null && commOptOut.IsOptedOut(CommOptOutMode.Email,CommOptOutType.Statements);
		}

		///<summary>True if a failure e-mail was sent for this card within the quiet period before dateTimeNow, by any recurring charge row other than
		///recurringChargeNumExclude (the charge being processed).</summary>
		public static bool WasSentRecently(long creditCardNum,long recurringChargeNumExclude,DateTime dateTimeNow) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),creditCardNum,recurringChargeNumExclude,dateTimeNow);
			}
			string command="SELECT COUNT(*) FROM recurringcharge "
				+"WHERE CreditCardNum="+POut.Long(creditCardNum)+" "
				+"AND RecurringChargeNum!="+POut.Long(recurringChargeNumExclude)+" "
				+"AND DateTimeCharge>="+POut.DateT(dateTimeNow.AddDays(-QUIET_PERIOD_DAYS))+" "
				+"AND ErrorMsg LIKE '%"+POut.String(ERROR_MSG_SENT_PREFIX)+"%'";
			return Db.GetCount(command)!="0";
		}

		///<summary>The office address the e-mail is sent from: the clinic's default, else the practice default, else the first address that belongs to no
		///user. Null when there is none or its sender address is not a valid e-mail address. A copy, with the clinic's sender name applied.</summary>
		public static EmailAddress GetSenderAddress(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			EmailAddress emailAddress=EmailAddresses.GetByClinic(clinicNum,allowNullReturn:true);
			if(emailAddress==null || EmailAddresses.GetValidMailAddress(emailAddress.GetFrom())==null) {
				return null;
			}
			return EmailAddresses.OverrideSenderAddressClinical(emailAddress.Clone(),clinicNum);
		}

		///<summary>Sends the failure e-mail for one processed recurring charge when it is needed. Never throws. Adds a line about the outcome to
		///chargeData.RecurringCharge.ErrorMsg; the caller saves the row (RecurringChargerator does, right after). dateTimeNow is the time of the run.
		///actionSendEmail sends and saves the message like EmailMessages.SendEmail (null uses it); it must throw when sending fails.</summary>
		public static RecurringChargeFailEmailResult SendIfNeeded(RecurringChargeData chargeData,DateTime dateTimeNow,
			Action<EmailMessage,EmailAddress> actionSendEmail=null)
		{
			Meth.NoCheckMiddleTierRole();
			if(chargeData?.RecurringCharge==null || !chargeData.IsDeclinedByProcessor || !IsEnabled()) {
				return new RecurringChargeFailEmailResult(RecurringChargeFailEmailStatus.NotNeeded,"");
			}
			RecurringChargeFailEmailResult result;
			try {
				result=SendForDeclinedCharge(chargeData,dateTimeNow,actionSendEmail??((emailMessage,emailAddress) => EmailMessages.SendEmail(emailMessage,emailAddress)));
			}
			catch(Exception ex) {
				result=new RecurringChargeFailEmailResult(RecurringChargeFailEmailStatus.NotSent,Lans.g(LAN,"unexpected error")+": "+ex.Message);
			}
			if(result.Status==RecurringChargeFailEmailStatus.Sent) {
				chargeData.RecurringCharge.ErrorMsg=StringTools.AppendLine(chargeData.RecurringCharge.ErrorMsg,ERROR_MSG_SENT_PREFIX+result.Reason+".");
			}
			else {
				chargeData.RecurringCharge.ErrorMsg=StringTools.AppendLine(chargeData.RecurringCharge.ErrorMsg,ERROR_MSG_NOT_SENT_PREFIX+result.Reason);
			}
			return result;
		}

		///<summary>For a declined charge. Throws only on unexpected errors. For a sent e-mail, Reason holds the address it went to.</summary>
		private static RecurringChargeFailEmailResult SendForDeclinedCharge(RecurringChargeData chargeData,DateTime dateTimeNow,
			Action<EmailMessage,EmailAddress> actionSendEmail)
		{
			RecurringCharge recurringCharge=chargeData.RecurringCharge;
			if(WasSentRecently(recurringCharge.CreditCardNum,recurringCharge.RecurringChargeNum,dateTimeNow)) {
				return new RecurringChargeFailEmailResult(RecurringChargeFailEmailStatus.SkippedQuietPeriod,
					Lans.g(LAN,"one was already sent for this card in the last")+" "+QUIET_PERIOD_DAYS+" "+Lans.g(LAN,"days."));
			}
			if(Security.CurUser==null || !Security.IsAuthorized(EnumPermType.EmailSend,suppressMessage:true)) {
				return NotSent(Lans.g(LAN,"the user running the charges does not have the Email Send permission."));
			}
			Patient patientCard=Patients.GetPat(recurringCharge.PatNum);
			if(patientCard==null) {
				return NotSent(Lans.g(LAN,"the patient was not found."));
			}
			if(IsOptedOut(patientCard.PatNum)) {
				return NotSent(Lans.g(LAN,"the patient opted out of e-mail."));
			}
			Patient patientGuarantor=patientCard;
			if(patientCard.Guarantor!=0 && patientCard.Guarantor!=patientCard.PatNum) {
				patientGuarantor=Patients.GetPat(patientCard.Guarantor);
			}
			Patient patientRecipient=GetRecipient(patientCard,patientGuarantor);
			if(patientRecipient==null) {
				return NotSent(Lans.g(LAN,"neither the patient nor the guarantor has a valid e-mail address."));
			}
			if(patientRecipient.PatNum!=patientCard.PatNum && IsOptedOut(patientRecipient.PatNum)) {
				return NotSent(Lans.g(LAN,"the guarantor opted out of e-mail."));
			}
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=recurringCharge.ClinicNum;
			}
			EmailAddress emailAddressSender=GetSenderAddress(clinicNum);
			if(emailAddressSender==null) {
				return NotSent(Lans.g(LAN,"no office e-mail address is set up."));
			}
			Clinic clinic=null;
			if(clinicNum!=0) {
				clinic=Clinics.GetClinic(clinicNum);
			}
			string cardLast4=GetCardLast4(chargeData.CCNumberMasked);
			EmailMessage emailMessage=new EmailMessage();
			emailMessage.PatNum=patientCard.PatNum;//Filed with the patient whose charge was declined, like the $0 payment.
			emailMessage.PatNumSubj=patientRecipient.PatNum;
			emailMessage.ToAddress=string.Join(",",EmailAddresses.GetValidAddresses(patientRecipient.Email));
			emailMessage.FromAddress=emailAddressSender.GetFrom();
			emailMessage.Subject=ReplaceTemplateTags(GetSubjectTemplate(),patientRecipient,recurringCharge.ChargeAmt,cardLast4,dateTimeNow,clinic);
			emailMessage.BodyText=ReplaceTemplateTags(GetBodyTemplate(),patientRecipient,recurringCharge.ChargeAmt,cardLast4,dateTimeNow,clinic);
			emailMessage.MsgType=EmailMessageSource.PaymentReceipt;
			emailMessage.HtmlType=EmailType.Regular;
			emailMessage.SentOrReceived=EmailSentOrReceived.Sent;
			emailMessage.MsgDateTime=dateTimeNow;
			emailMessage.UserNum=Security.CurUser.UserNum;
			try {
				actionSendEmail(emailMessage,emailAddressSender);//Saves the message too, also when sending fails.
			}
			catch(Exception ex) {
				return NotSent(Lans.g(LAN,"the e-mail could not be sent")+": "+ex.Message);
			}
			Commlog commlog=new Commlog();
			commlog.PatNum=patientCard.PatNum;
			commlog.CommDateTime=dateTimeNow;
			commlog.CommType=Commlogs.GetTypeAuto(CommItemTypeAuto.FIN);
			commlog.Mode_=CommItemMode.Email;
			commlog.SentOrReceived=CommSentOrReceived.Sent;
			commlog.UserNum=Security.CurUser.UserNum;
			commlog.CommSource=CommItemSource.User;
			commlog.Note=Lans.g(LAN,"Recurring charge declined. E-mail sent to")+" "+emailMessage.ToAddress
				+" ("+Lans.g(LAN,"card ending in")+" "+cardLast4+", "+recurringCharge.ChargeAmt.ToString("c")+").";
			Commlogs.Insert(commlog);
			return new RecurringChargeFailEmailResult(RecurringChargeFailEmailStatus.Sent,emailMessage.ToAddress);
		}

		private static RecurringChargeFailEmailResult NotSent(string reason) {
			return new RecurringChargeFailEmailResult(RecurringChargeFailEmailStatus.NotSent,reason);
		}
	}

	///<summary>What happened to the failure e-mail of one recurring charge.</summary>
	public enum RecurringChargeFailEmailStatus {
		///<summary>No e-mail is due: the option is off, or the charge was not declined.</summary>
		NotNeeded,
		///<summary>Sent.</summary>
		Sent,
		///<summary>Due but not sent; Reason says why.</summary>
		NotSent,
		///<summary>Not sent because the card was e-mailed within the quiet period.</summary>
		SkippedQuietPeriod,
	}

	///<summary>The outcome of RecurringChargeFailEmails.SendIfNeeded.</summary>
	public class RecurringChargeFailEmailResult {
		public RecurringChargeFailEmailStatus Status;
		///<summary>Why it was not sent, or the address it was sent to.</summary>
		public string Reason;

		public RecurringChargeFailEmailResult(RecurringChargeFailEmailStatus status,string reason) {
			Status=status;
			Reason=reason;
		}
	}
}
