// Global Payments V2 (Open Dental 25.4, v25_4-payments-globalpayments-v2): the program link, its settings and the office's Global Payments API app
// credentials, terminals, the one-processor rule, duplicate checks, and the note, receipt and log text of a transaction. Optional and off by default
// (decoupling plan, ground rules 6 and 7): the program row is inserted at runtime with Enabled=0 and nothing shows until the office enables it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using CodeBase;

namespace OpenDentBusiness.VendorDirect.GlobalPayments {
	///<summary>The settings one Global Payments V2 transaction needs, for one clinic. The App Key is plain text in memory only.</summary>
	public sealed class GlobalPaymentsV2Config {
		///<summary>The clinic the settings were read for (0 without clinics).</summary>
		public long ClinicNum;
		///<summary>Whose credentials these are: ClinicNum when that clinic has its own App ID, else 0 (Headquarters).</summary>
		public long ClinicNumCredentials;
		public string AppId="";
		public string AppKey="";
		///<summary>The Global Payments account name used for charges on saved cards (Transactions API account_name). Blank uses the first account the
		///access token lists with charge permission.</summary>
		public string AccountName="";
		public long PaymentTypeDefNum;
		public bool PrintReceipt;
		public bool PreventSavingNewCards;
		public bool ForceDuplicates;
		public bool UseSandbox;
		///<summary>A SHA-256 fingerprint of the stored settings (the encrypted key, never the plain one). A changed setting gives a new access token.
		///</summary>
		public string Fingerprint="";

		///<summary>True when the App ID and App Key are both set.</summary>
		public bool IsComplete {
			get {
				return !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrEmpty(AppKey);
			}
		}
	}

	///<summary>The Global Payments V2 program link (ProgramName.MeetInTheCloud). Settings are programproperty rows: per clinic, with the
	///Headquarters rows (ClinicNum 0) as the default. A clinic with its own App ID uses its own App ID, App Key and account name; any other clinic
	///uses Headquarters' credentials. The other settings fall back to Headquarters one by one. The App Key is stored with CodeBase Secrets (osd1$,
	///masked, high security). Access tokens are never stored.</summary>
	public static class GlobalPaymentsV2 {
		///<summary>The program row's description in Setup > Program Links, and the name every window uses.</summary>
		public const string ProgDesc="Global Payments V2";
		///<summary>The name shown to users.</summary>
		public const string DisplayName="Global Payments V2";
		///<summary>payterminal.CCIntegration of this integration's terminals.</summary>
		public const string CCIntegration=PayTerminals.CCIntegrationMeetInTheCloud;
		///<summary>Global Payments V2 is a US integration.</summary>
		public const string Currency="USD";
		public const string Country="US";
		///<summary>The App ID of the office's app in Global Payments' developer portal. Plain text, per clinic.</summary>
		public const string PropAppId="GlobalPaymentsV2AppId";
		///<summary>The App Key of that app. osd1$, masked, high security, per clinic.</summary>
		public const string PropAppKey="GlobalPaymentsV2AppKey";
		///<summary>The Global Payments account name for charges on saved cards. Optional, per clinic.</summary>
		public const string PropAccountName="GlobalPaymentsV2AccountName";
		///<summary>The default payment type (definition.DefNum). Named like PayConnect's and X-Charge's row, so the recurring charge payments and the
		///"payment type in use" check of the Definitions window find it.</summary>
		public const string PropPaymentType="PaymentType";
		public const string PropPrintReceipt="GlobalPaymentsV2PrintReceipt";
		public const string PropPreventSavingNewCC="GlobalPaymentsV2PreventSavingNewCC";
		public const string PropForceRecurringCharge="GlobalPaymentsV2ForceRecurringCharge";
		///<summary>"1" sends everything to Global Payments' sandbox (nothing is charged). Practice level (ClinicNum 0) only.</summary>
		public const string PropUseSandbox="GlobalPaymentsV2UseSandbox";
		///<summary>The plain settings of every clinic, with their defaults.</summary>
		private static readonly string[][] _arraySettingsDefault=new string[][] {
			new[] { PropAppId,"" },
			new[] { PropAccountName,"" },
			new[] { PropPaymentType,"" },
			new[] { PropPrintReceipt,"0" },
			new[] { PropPreventSavingNewCC,"0" },
			new[] { PropForceRecurringCharge,"0" },
		};
		///<summary>Tests set this to a fake handler; production leaves it null and uses the network through VendorHttp.</summary>
		public static HttpMessageHandler HttpHandlerForTests;
		private static GpV2Client _gpV2ClientDefault;
		private static readonly object _lockClient=new object();

		///<summary>The process-wide API client (or a client over HttpHandlerForTests in tests).</summary>
		public static GpV2Client GetClient() {
			lock(_lockClient) {
				if(HttpHandlerForTests!=null) {
					if(_gpV2ClientDefault==null || !_gpV2ClientDefault.UsesHandler(HttpHandlerForTests)) {
						_gpV2ClientDefault=new GpV2Client(HttpHandlerForTests);
					}
					return _gpV2ClientDefault;
				}
				if(_gpV2ClientDefault==null || _gpV2ClientDefault.HasTestHandler) {
					_gpV2ClientDefault=new GpV2Client();
				}
				return _gpV2ClientDefault;
			}
		}

		///<summary>Inserts the program row (Enabled=0) and the Headquarters property rows if they are missing, and brings pre-25.4 terminal rows in line
		///(PayTerminals.BackfillPayConnectIntegration). Returns true if anything was inserted. Never calls Global Payments. The caller refreshes the
		///caches and signals InvalidType.Programs.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.MeetInTheCloud,ProgDesc);
			long programNum=Programs.GetProgramNum(ProgramName.MeetInTheCloud);
			if(programNum==0) {
				Programs.RefreshCache();
				programNum=Programs.GetProgramNum(ProgramName.MeetInTheCloud);
			}
			if(programNum==0) {
				return isInserted;
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(programNum);
			for(int i=0;i<_arraySettingsDefault.Length;i++) {
				string desc=_arraySettingsDefault[i][0];
				if(!listProgramProperties.Any(x => x.PropertyDesc==desc && x.ClinicNum==0)) {
					VendorCredentials.EnsureSetting(programNum,desc,0,_arraySettingsDefault[i][1]);
					isInserted=true;
				}
			}
			ProgramProperty programPropertyKey=listProgramProperties.FirstOrDefault(x => x.PropertyDesc==PropAppKey && x.ClinicNum==0);
			if(programPropertyKey==null || !programPropertyKey.IsMasked || !programPropertyKey.IsHighSecurity) {
				VendorCredentials.EnsureSecret(programNum,PropAppKey);
				isInserted=true;
			}
			if(!listProgramProperties.Any(x => x.PropertyDesc==PropUseSandbox && x.ClinicNum==0)) {
				VendorCredentials.EnsureSetting(programNum,PropUseSandbox,0,"0");
				isInserted=true;
			}
			try {
				PayTerminals.BackfillPayConnectIntegration();
			}
			catch(Exception ex) {
				//Never keeps Program Links from opening; readers treat a blank integration as PayConnect anyway.
				ex.DoNothing();
			}
			return isInserted;
		}

		///<summary>True if the program link is enabled. Reads the cache.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return Programs.IsEnabled(ProgramName.MeetInTheCloud);
		}

		///<summary>Without clinics every lookup uses the practice rows (ClinicNum 0), whatever clinic a payment still carries.</summary>
		public static long NormalizeClinicNum(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(!PrefC.HasClinicsEnabled || clinicNum<0) {
				return 0;
			}
			return clinicNum;
		}

		///<summary>The settings for clinicNum from the cache, with the App Key decrypted. Missing rows read as blank. Never throws.</summary>
		public static GlobalPaymentsV2Config GetConfig(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			GlobalPaymentsV2Config config=new GlobalPaymentsV2Config();
			try {
				clinicNum=NormalizeClinicNum(clinicNum);
				config.ClinicNum=clinicNum;
				long programNum=Programs.GetProgramNum(ProgramName.MeetInTheCloud);
				if(programNum==0) {
					return config;
				}
				config.ClinicNumCredentials=GetClinicNumForCredentials(programNum,clinicNum);
				config.AppId=GetOwnValue(programNum,PropAppId,config.ClinicNumCredentials).Trim();
				//The App Key was never stored under Open Dental (its V2 settings aren't public), so a plain-text value can only have been typed into the
				//generic property editor. Accept it; the setup window encrypts it on the next save.
				config.AppKey=SecretValue.Read(GetOwnValue(programNum,PropAppKey,config.ClinicNumCredentials),LegacyPolicy.AcceptPlaintext).PlainText.Trim();
				config.AccountName=GetOwnValue(programNum,PropAccountName,config.ClinicNumCredentials).Trim();
				config.PaymentTypeDefNum=PIn.Long(ProgramProperties.GetValOrDefault(programNum,PropPaymentType,clinicNum),false);
				config.PrintReceipt=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,PropPrintReceipt,clinicNum));
				config.PreventSavingNewCards=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,PropPreventSavingNewCC,clinicNum));
				config.ForceDuplicates=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,PropForceRecurringCharge,clinicNum));
				config.UseSandbox=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,PropUseSandbox,0));
				config.Fingerprint=VendorCredentials.GetFingerprint(programNum,config.ClinicNumCredentials,PropAppId,PropAppKey,PropAccountName)
					+(config.UseSandbox ? "|sandbox" : "|production");
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return config;
		}

		///<summary>clinicNum if it has its own (non-blank) App ID row, else 0.</summary>
		public static long GetClinicNumForCredentials(long programNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum!=0 && GetOwnValue(programNum,PropAppId,clinicNum).Trim()!="") {
				return clinicNum;
			}
			return 0;
		}

		///<summary>The value of the clinic's own row from the cache (no fallback to Headquarters), or "".</summary>
		private static string GetOwnValue(long programNum,string desc,long clinicNum) {
			ProgramProperty programProperty=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==desc && x.ClinicNum==clinicNum)
				.OrderBy(x => x.ProgramPropertyNum).FirstOrDefault();
			return programProperty?.PropertyValue??"";
		}

		///<summary>True if the program link is enabled and clinicNum (or Headquarters) has an App ID and a readable App Key. Reads the cache; never calls
		///Global Payments.</summary>
		public static bool IsConfigured(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			try {
				return IsEnabled() && GetConfig(clinicNum).IsComplete;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True if the Global Payments V2 button shows in the payment window for clinicNum: configured, and with clinics on a valid payment type.
		///listDefsPaymentType null skips the payment-type check. Never throws.</summary>
		public static bool ShowButton(long clinicNum,List<Def> listDefsPaymentType) {
			Meth.NoCheckMiddleTierRole();
			try {
				if(!IsConfigured(clinicNum)) {
					return false;
				}
				if(listDefsPaymentType==null || !PrefC.HasClinicsEnabled) {
					return true;
				}
				long defNum=GetConfig(clinicNum).PaymentTypeDefNum;
				return listDefsPaymentType.Any(x => x.DefNum==defNum);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>The clinic's Global Payments V2 terminals.</summary>
		public static List<PayTerminal> GetTerminals(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return PayTerminals.GetForIntegration(NormalizeClinicNum(clinicNum),CCIntegration);
		}

		///<summary>The integrations of which only one may be enabled at a time: X-Charge, Global Payments V1 (EdgeExpress) and Global Payments V2.
		///</summary>
		public static readonly ProgramName[] ArrayExclusivePrograms=new[] { ProgramName.Xcharge,ProgramName.EdgeExpress,ProgramName.MeetInTheCloud };

		///<summary>The other integrations of ArrayExclusivePrograms that are enabled now (from the cache), excluding programNameSaving. Saving
		///programNameSaving as enabled is refused while this list is not empty.</summary>
		public static List<ProgramName> GetEnabledExclusiveConflicts(ProgramName programNameSaving) {
			Meth.NoCheckMiddleTierRole();
			return ArrayExclusivePrograms.Where(x => x!=programNameSaving && Programs.IsEnabled(x)).ToList();
		}

		///<summary>The name users see for one of ArrayExclusivePrograms.</summary>
		public static string GetExclusiveDisplayName(ProgramName programName) {
			Meth.NoCheckMiddleTierRole();
			switch(programName) {
				case ProgramName.Xcharge:
					return "X-Charge";
				case ProgramName.EdgeExpress:
					return GlobalPaymentsV1.DisplayName;
				case ProgramName.MeetInTheCloud:
					return DisplayName;
				default:
					return programName.ToString();
			}
		}

		///<summary>The message shown when programNameSaving can't be enabled, or "" when nothing conflicts.</summary>
		public static string GetExclusiveConflictMessage(ProgramName programNameSaving) {
			Meth.NoCheckMiddleTierRole();
			List<ProgramName> listConflicts=GetEnabledExclusiveConflicts(programNameSaving);
			if(listConflicts.Count==0) {
				return "";
			}
			return Lans.g("GlobalPayments","Only one of X-Charge, Global Payments V1 and Global Payments V2 can be enabled at a time. Turn off")+" "
				+string.Join(", ",listConflicts.Select(x => GetExclusiveDisplayName(x)))+" "
				+Lans.g("GlobalPayments","in Setup > Program Links first.");
		}

		///<summary>The amount in minor units (cents), as Global Payments wants it: "1234" for $12.34. Rounded half away from zero.</summary>
		public static long ToMinorUnits(decimal amount) {
			Meth.NoCheckMiddleTierRole();
			return (long)Math.Round(amount*100m,MidpointRounding.AwayFromZero);
		}

		///<summary>The Transactions API reference of a new recurring charge attempt, 8 to 50 characters; it is also the request's X-GP-Idempotency key.
		///Every run inserts new recurringcharge rows, so this is unique per attempt and a re-run never repeats it by itself. The one exception is a retry
		///after an attempt whose outcome is unknown: it deliberately reuses that attempt's reference (RecurringChargerator.SendGlobalPaymentsV2,
		///GetRecurringChargeInDoubt), so Global Payments can answer with the first request's result instead of charging again (INFERRED: how long Global
		///Payments keeps an idempotency key is not stated in the public material).</summary>
		public static string GetRecurringReference(long recurringChargeNum,DateTime dateCharge) {
			Meth.NoCheckMiddleTierRole();
			return "RC"+POut.Long(recurringChargeNum)+"-"+dateCharge.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
		}

		///<summary>A Transactions API reference for a charge made in the payment window: unique, 8 to 50 characters, and naming nothing about the patient.
		///</summary>
		public static string NewReference(string prefix) {
			Meth.NoCheckMiddleTierRole();
			return (prefix??"")+DateTime.UtcNow.ToString("yyyyMMddHHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
		}

		#region Duplicate checks
		///<summary>True if patNum already has a completed Global Payments V2 payment of exactly amount on date. The payment window asks before charging
		///again. The card isn't known before the terminal reads it, so "same card" is checked as "same patient" (INFERRED).</summary>
		public static bool HasSamePaymentOnDate(long patNum,double amount,DateTime date) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),patNum,amount,date);
			}
			string command="SELECT COUNT(*) FROM payment "
				+"WHERE PatNum="+POut.Long(patNum)+" "
				+"AND PaymentSource="+POut.Int((int)CreditCardSource.MeetInTheCloudTerminal)+" "
				+"AND IsCcCompleted=1 "
				+"AND PayDate="+POut.Date(date)+" "
				+"AND ROUND(PayAmt,2)=ROUND("+POut.Double(amount)+",2)";
			return PIn.Long(Db.GetCount(command))>0;
		}

		///<summary>True if the card was already charged amount successfully on date by another recurring charge row. The recurring charge is then declined
		///as a duplicate unless duplicates are forced.</summary>
		public static bool HasSameRecurringChargeOnDate(long creditCardNum,double amount,DateTime date,long recurringChargeNumExclude) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),creditCardNum,amount,date,recurringChargeNumExclude);
			}
			string command="SELECT COUNT(*) FROM recurringcharge "
				+"WHERE CreditCardNum="+POut.Long(creditCardNum)+" "
				+"AND RecurringChargeNum!="+POut.Long(recurringChargeNumExclude)+" "
				+"AND ChargeStatus="+POut.Int((int)RecurringChargeStatus.ChargeSuccessful)+" "
				+"AND "+DbHelper.DtimeToDate("DateTimeCharge")+"="+POut.Date(date)+" "
				+"AND ROUND(ChargeAmt,2)=ROUND("+POut.Double(amount)+",2)";
			return PIn.Long(Db.GetCount(command))>0;
		}

		///<summary>Marks a recurring charge row whose request may have taken the money without this program knowing: no answer, an answer it could not
		///read, or an approval whose payment could not be saved. Written into recurringcharge.ErrorMsg with the attempt's reference
		///(BuildRecurringInDoubtTag) and never translated, so GetRecurringChargeInDoubt finds it whatever the language.</summary>
		public const string RecurringInDoubtTag="GPV2-IN-DOUBT";

		///<summary>The ErrorMsg tag of an in-doubt recurring charge attempt: "[GPV2-IN-DOUBT reference]". The reference also lets the office find the
		///charge in Global Payments' records.</summary>
		public static string BuildRecurringInDoubtTag(string reference) {
			Meth.NoCheckMiddleTierRole();
			return "["+RecurringInDoubtTag+" "+(reference??"").Trim()+"]";
		}

		///<summary>The reference in an ErrorMsg that BuildRecurringInDoubtTag wrote (the last one when there are several), or "".</summary>
		public static string GetReferenceFromInDoubtTag(string errorMsg) {
			Meth.NoCheckMiddleTierRole();
			string text=errorMsg??"";
			string prefix="["+RecurringInDoubtTag+" ";
			int indexStart=text.LastIndexOf(prefix,StringComparison.Ordinal);
			if(indexStart<0) {
				return "";
			}
			indexStart+=prefix.Length;
			int indexEnd=text.IndexOf(']',indexStart);
			if(indexEnd<0) {
				return "";
			}
			return text.Substring(indexStart,indexEnd-indexStart).Trim();
		}

		///<summary>The card's latest recurring charge row whose outcome is unknown (RecurringInDoubtTag) and that no later charge on the card has settled,
		///or null. A later row with a payment (approved, or declined with its $0 payment) means Global Payments has given a definite answer on this card
		///since. While this returns a row, RecurringChargerator does not charge the card again, in any run, unless a user forces it from the Recurring
		///Charges window.</summary>
		public static RecurringCharge GetRecurringChargeInDoubt(long creditCardNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<RecurringCharge>(MethodBase.GetCurrentMethod(),creditCardNum);
			}
			string command="SELECT * FROM recurringcharge "
				+"WHERE CreditCardNum="+POut.Long(creditCardNum)+" "
				+"AND ChargeStatus!="+POut.Int((int)RecurringChargeStatus.NotYetCharged)+" "
				+"AND ErrorMsg LIKE '%"+POut.String("["+RecurringInDoubtTag+" ")+"%' "
				+"AND RecurringChargeNum>(SELECT COALESCE(MAX(rcSettled.RecurringChargeNum),0) FROM recurringcharge rcSettled "
					+"WHERE rcSettled.CreditCardNum="+POut.Long(creditCardNum)+" AND rcSettled.PayNum>0) "
				+"ORDER BY RecurringChargeNum DESC";
			return Crud.RecurringChargeCrud.SelectOne(DbHelper.LimitOrderBy(command,1));
		}
		#endregion Duplicate checks

		#region Cards
		///<summary>True if creditCard is a card saved through Global Payments V2 with a reusable payment method.</summary>
		public static bool IsGlobalPaymentsV2Card(CreditCard creditCard) {
			Meth.NoCheckMiddleTierRole();
			return creditCard!=null && creditCard.CCSource==CreditCardSource.MeetInTheCloudTerminal && !string.IsNullOrWhiteSpace(creditCard.XChargeToken);
		}

		///<summary>Saves the card of an approved transaction for later charges, when Global Payments returned a reusable payment method. The token goes in
		///creditcard.XChargeToken, as Global Payments V1 does (INFERRED for V2), with CCSource MeetInTheCloudTerminal; only the last four digits are
		///kept. Returns the new card, or null when there was nothing to save or the same token is already saved for the patient.</summary>
		public static CreditCard SaveCard(long patNum,long clinicNum,GlobalPaymentsV2Result result) {
			Meth.NoCheckMiddleTierRole();
			if(result==null || !result.IsApproved || string.IsNullOrWhiteSpace(result.PaymentMethodId) || patNum==0) {
				return null;
			}
			List<CreditCard> listCreditCards=CreditCards.RefreshAll(patNum);
			if(listCreditCards.Any(x => x.XChargeToken==result.PaymentMethodId)) {
				return null;
			}
			CreditCard creditCard=new CreditCard();
			creditCard.ItemOrder=listCreditCards.Count;
			creditCard.PatNum=patNum;
			creditCard.XChargeToken=result.PaymentMethodId;
			creditCard.CCNumberMasked=result.GetMaskedCardNumber();
			if(result.ExpMonth>=1 && result.ExpMonth<=12 && result.ExpYear>2000) {
				creditCard.CCExpiration=new DateTime(result.ExpYear,result.ExpMonth,1);
			}
			creditCard.Procedures=PrefC.GetString(PrefName.DefaultCCProcs);
			creditCard.CCSource=CreditCardSource.MeetInTheCloudTerminal;
			creditCard.ClinicNum=clinicNum;
			CreditCards.Insert(creditCard);
			SecurityLogs.MakeLogEntry(EnumPermType.CreditCardEdit,patNum,DisplayName+" "+Lans.g("GlobalPayments","card added, ending")+" "
				+result.CardLast4);
			return creditCard;
		}
		#endregion Cards

		#region Notes, receipts and logs
		///<summary>The label of the transaction ID line in a payment note. Deliberately not "Transaction ID:" (the payment window reads that as an
		///EdgeExpress transaction when it voids on cancel) and not "Ref Number:" (read as PayConnect).</summary>
		public const string NoteLabelTransaction="Global Payments V2 transaction";

		///<summary>The words for a transaction type.</summary>
		public static string GetTransTypeText(GpV2TransType transType) {
			Meth.NoCheckMiddleTierRole();
			switch(transType) {
				case GpV2TransType.Refund:
					return Lans.g("GlobalPayments","Refund");
				case GpV2TransType.Void:
					return Lans.g("GlobalPayments","Void");
				default:
					return Lans.g("GlobalPayments","Sale");
			}
		}

		///<summary>The words for an outcome.</summary>
		public static string GetOutcomeText(GpV2Outcome outcome) {
			Meth.NoCheckMiddleTierRole();
			switch(outcome) {
				case GpV2Outcome.Approved:
					return Lans.g("GlobalPayments","Approved");
				case GpV2Outcome.Declined:
					return Lans.g("GlobalPayments","Declined");
				case GpV2Outcome.Failed:
					return Lans.g("GlobalPayments","Not processed");
				case GpV2Outcome.Cancelled:
					return Lans.g("GlobalPayments","Cancelled before the terminal finished");
				case GpV2Outcome.TimedOut:
					return Lans.g("GlobalPayments","No answer from the terminal in time");
				default:
					return Lans.g("GlobalPayments","Result unknown");
			}
		}

		///<summary>The lines added to the payment note. Shows the last four digits only and never the card token.</summary>
		public static string BuildPayNote(GlobalPaymentsV2Result result) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			if(result.IsSandbox) {
				stringBuilder.AppendLine(Lans.g("GlobalPayments","SANDBOX TEST - NOT A REAL CHARGE"));
			}
			stringBuilder.AppendLine(DisplayName);
			stringBuilder.AppendLine(Lans.g("GlobalPayments","Transaction Type")+": "+GetTransTypeText(result.TransType));
			stringBuilder.AppendLine(Lans.g("GlobalPayments","Status")+": "+GetOutcomeText(result.Outcome)
				+(string.IsNullOrWhiteSpace(result.Message) ? "" : " ("+result.Message+")"));
			decimal amountSigned=(result.TransType==GpV2TransType.Sale) ? result.Amount : -result.Amount;
			stringBuilder.AppendLine(Lans.g("GlobalPayments","Amount")+": "+amountSigned.ToString("C",CultureInfo.CurrentCulture));
			if(result.CardBrand!="") {
				stringBuilder.AppendLine(Lans.g("GlobalPayments","Card Type")+": "+result.CardBrand);
			}
			if(result.CardLast4!="") {
				stringBuilder.AppendLine(Lans.g("GlobalPayments","Account")+": "+result.GetMaskedCardNumber());
			}
			if(result.AuthCode!="") {
				stringBuilder.AppendLine(Lans.g("GlobalPayments","Auth Code")+": "+result.AuthCode);
			}
			if(result.TerminalName!="" || result.TerminalId!="") {
				stringBuilder.AppendLine(Lans.g("GlobalPayments","Terminal")+": "+((result.TerminalName!="") ? result.TerminalName : result.TerminalId));
			}
			if(result.TransactionId!="") {
				stringBuilder.AppendLine(NoteLabelTransaction+": "+result.TransactionId);
			}
			if(result.DeviceCommandId!="" && result.IsInDoubt) {
				stringBuilder.AppendLine(Lans.g("GlobalPayments","Terminal command")+": "+result.DeviceCommandId);
			}
			return stringBuilder.ToString().TrimEnd();
		}

		///<summary>The transaction ID in a note BuildPayNote wrote, or "".</summary>
		public static string GetTransactionIdFromNote(string note) {
			Meth.NoCheckMiddleTierRole();
			string[] arrayLines=(note??"").Replace("\r\n","\n").Split('\n');
			for(int i=arrayLines.Length-1;i>=0;i--) {
				string line=arrayLines[i].Trim();
				if(line.StartsWith(NoteLabelTransaction+": ")) {
					return line.Substring(NoteLabelTransaction.Length+2).Trim();
				}
			}
			return "";
		}

		///<summary>The receipt text. header is the practice or clinic block (CreditCardUtils.AddClinicToReceipt); patName may be blank. Last four digits
		///only; never the card token.</summary>
		public static string BuildReceipt(GlobalPaymentsV2Result result,string header,string patName,DateTime dateTime) {
			Meth.NoCheckMiddleTierRole();
			const int widthLabel=15;
			const int widthLine=37;
			StringBuilder stringBuilder=new StringBuilder();
			if(result.IsSandbox) {
				stringBuilder.AppendLine("SANDBOX TEST - NOT A REAL CHARGE");
			}
			stringBuilder.AppendLine();
			stringBuilder.Append(header??"");
			stringBuilder.AppendLine("Date".PadRight(widthLabel,'.')+dateTime.ToString(CultureInfo.CurrentCulture));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Trans Type".PadRight(widthLabel,'.')+GetTransTypeText(result.TransType));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Transaction #".PadRight(widthLabel,'.')+result.TransactionId);
			if(!string.IsNullOrWhiteSpace(patName)) {
				stringBuilder.AppendLine("Patient".PadRight(widthLabel,'.')+patName);
			}
			if(result.CardLast4!="") {
				stringBuilder.AppendLine("Account".PadRight(widthLabel,'.')+result.CardLast4.PadLeft(16,'*'));
			}
			if(result.CardBrand!="") {
				stringBuilder.AppendLine("Card Type".PadRight(widthLabel,'.')+result.CardBrand);
			}
			if(result.EntryMode!="") {
				stringBuilder.AppendLine("Entry".PadRight(widthLabel,'.')+result.EntryMode);
			}
			if(result.AuthCode!="") {
				stringBuilder.AppendLine("Auth Code".PadRight(widthLabel,'.')+result.AuthCode);
			}
			stringBuilder.AppendLine("Result".PadRight(widthLabel,'.')+GetOutcomeText(result.Outcome));
			if(result.TerminalName!="" || result.TerminalId!="") {
				stringBuilder.AppendLine("Terminal".PadRight(widthLabel,'.')+((result.TerminalName!="") ? result.TerminalName : result.TerminalId));
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			decimal amountSigned=(result.TransType==GpV2TransType.Sale) ? result.Amount : -result.Amount;
			stringBuilder.AppendLine("Total Amt".PadRight(widthLabel,'.')+amountSigned.ToString("C",CultureInfo.CurrentCulture));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			if(result.TransType==GpV2TransType.Sale) {
				stringBuilder.AppendLine("I agree to pay the above total amount according to my card issuer/bank agreement.");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("Signature X".PadRight(widthLine,'_'));
			}
			return stringBuilder.ToString();
		}

		///<summary>The security log text for a transaction: integration, type, amount, last four digits, terminal and outcome. Never the card token.
		///</summary>
		public static string BuildLogText(GlobalPaymentsV2Result result) {
			Meth.NoCheckMiddleTierRole();
			string text=DisplayName+" "+GetTransTypeText(result.TransType).ToLowerInvariant()+" "+result.Amount.ToString("C",CultureInfo.CurrentCulture);
			if(result.CardLast4!="") {
				text+=", "+Lans.g("GlobalPayments","card ending")+" "+result.CardLast4;
			}
			if(result.TerminalName!="" || result.TerminalId!="") {
				text+=", "+Lans.g("GlobalPayments","terminal")+" "+((result.TerminalName!="") ? result.TerminalName : result.TerminalId);
			}
			text+=": "+GetOutcomeText(result.Outcome);
			if(result.TransactionId!="") {
				text+=", "+Lans.g("GlobalPayments","transaction")+" "+result.TransactionId;
			}
			if(result.IsSandbox) {
				text+=" ("+Lans.g("GlobalPayments","sandbox")+")";
			}
			if(result.IsInDoubt && result.DeviceCommandId!="") {
				text+=", "+Lans.g("GlobalPayments","terminal command")+" "+result.DeviceCommandId;
			}
			return text;
		}
		#endregion Notes, receipts and logs
	}

	///<summary>Global Payments V1: the EdgeExpress program link under its 25.4 name. Display only: ProgramName.EdgeExpress, the program row's
	///ProgName, its properties and its behaviour are unchanged.</summary>
	public static class GlobalPaymentsV1 {
		///<summary>The name users see.</summary>
		public const string DisplayName="Global Payments V1";
		///<summary>The program row's description after the rename. Neutral, like the other vendor-direct rows (no vendor web address).</summary>
		public const string ProgDesc="Global Payments V1";
		///<summary>The description Open Dental's 24.3 convert script gave the row. Only this exact value is renamed; an office's own text is kept.</summary>
		public const string ProgDescOld="EdgeExpress from www.globalpaymentsintegrated.com";

		///<summary>Renames the EdgeExpress program row's description to ProgDesc when it still holds the 24.3 default. Returns true if it changed the
		///row. Idempotent and cheap: it reads the cache first. The caller refreshes the program cache.</summary>
		public static bool RenameProgDescIfDefault() {
			Meth.NoCheckMiddleTierRole();
			Program program=Programs.GetCur(ProgramName.EdgeExpress);
			if(program==null || program.ProgDesc!=ProgDescOld) {
				return false;
			}
			program.ProgDesc=ProgDesc;
			Programs.Update(program);
			return true;
		}
	}
}
