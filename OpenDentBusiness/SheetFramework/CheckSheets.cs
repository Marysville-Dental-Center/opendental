// Check printing from accounting transactions (Open Dental 25.3: SheetTypeEnum.Check=28; backlog package A-SHEET).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Rules for printing a check from an asset-account accounting transaction onto a Check sheet. Shared by Edit Transaction, the sheet
	///filler and the tests. The check is the one credit to the asset (checking) account; the other entries of the transaction are its splits.</summary>
	public class CheckSheets {
		///<summary>Sheet parameter: the transaction the check is printed from.</summary>
		public const string PARAM_TRANSACTION_NUM="TransactionNum";
		///<summary>Sheet parameter: the asset account the check is drawn on.</summary>
		public const string PARAM_ACCOUNT_NUM="AccountNum";
		///<summary>The numeric amount is padded on the left with asterisks to this many characters, so nothing can be written in front of it.</summary>
		public const int AMOUNT_FILL_WIDTH=14;

		///<summary>The output fields a Check sheet can hold, in the order Setup lists them.</summary>
		public static readonly List<string> ListFieldNames=new List<string> {
			"accountName",
			"amount",
			"amountInWords",
			"bankNumber",
			"checkNumber",
			"date",
			"memo",
			"notes",
			"payee",
			"practiceAddress",
			"practiceCityStateZip",
			"practiceTitle",
			"splits",
		};

		private static readonly string[] _arrayOnes={"zero","one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve",
			"thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen"};
		private static readonly string[] _arrayTens={"","","twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"};
		private static readonly string[] _arrayScales={"","thousand","million","billion"};

		///<summary>Checks are printed from asset accounts only (checking accounts).</summary>
		public static bool CanPrintFrom(Account account) {
			return account!=null && account.AcctType==AccountType.Asset;
		}

		///<summary>The entry the check is written for: the one entry of the account, or, when the account has several entries in the transaction, the
		///only one that is a credit. Null when there is no such single entry.</summary>
		public static JournalEntry GetCheckEntry(List<JournalEntry> listJournalEntries,long accountNum) {
			if(listJournalEntries==null) {
				return null;
			}
			List<JournalEntry> listForAccount=listJournalEntries.FindAll(x => x.AccountNum==accountNum);
			if(listForAccount.Count==1) {
				return listForAccount[0];
			}
			List<JournalEntry> listCredits=listForAccount.FindAll(x => x.CreditAmt>0);
			if(listCredits.Count==1) {
				return listCredits[0];
			}
			return null;
		}

		///<summary>The check amount: the credit to the asset account, to the cent.</summary>
		public static decimal GetAmount(JournalEntry journalEntryCheck) {
			if(journalEntryCheck==null) {
				return 0;
			}
			return Math.Round((decimal)journalEntryCheck.CreditAmt-(decimal)journalEntryCheck.DebitAmt,2,MidpointRounding.AwayFromZero);
		}

		///<summary>Returns an empty string when a check can be printed from the transaction, otherwise the reason it cannot. A check needs an asset
		///account, a single credit to that account above zero, a payee and a check number.</summary>
		public static string GetPrintError(Account account,List<JournalEntry> listJournalEntries) {
			if(!CanPrintFrom(account)) {
				return Lans.g("CheckSheets","Checks can only be printed from asset accounts.");
			}
			JournalEntry journalEntryCheck=GetCheckEntry(listJournalEntries,account.AccountNum);
			if(journalEntryCheck==null) {
				return Lans.g("CheckSheets","The transaction must have exactly one credit to this account to print a check.");
			}
			if(GetAmount(journalEntryCheck)<=0) {
				return Lans.g("CheckSheets","A check must be a payment out of this account (a credit) of more than zero.");
			}
			if(string.IsNullOrWhiteSpace(journalEntryCheck.Payee)) {
				return Lans.g("CheckSheets","A Payee is required to print a check.");
			}
			if(string.IsNullOrWhiteSpace(journalEntryCheck.CheckNumber)) {
				return Lans.g("CheckSheets","A Check Number is required to print a check.");
			}
			return "";
		}

		///<summary>The amount written out, as on a check: "One thousand two hundred fifty and 50/100". The sign is ignored.</summary>
		public static string GetAmountInWords(decimal amount) {
			amount=Math.Round(Math.Abs(amount),2,MidpointRounding.AwayFromZero);
			long dollars=(long)decimal.Truncate(amount);
			int cents=(int)((amount-dollars)*100);
			string words=GetNumberInWords(dollars);
			return char.ToUpperInvariant(words[0])+words.Substring(1)+" and "+cents.ToString("00")+"/100";
		}

		///<summary>A whole number in English words, lower case, for example "one thousand two hundred fifty". Up to 999 billion.</summary>
		public static string GetNumberInWords(long number) {
			if(number<0) {
				number=-number;
			}
			if(number==0) {
				return _arrayOnes[0];
			}
			if(number>=1000000000000) {
				throw new ArgumentOutOfRangeException(nameof(number),"Amount too large for a check.");
			}
			List<string> listParts=new List<string>();
			for(int scale=_arrayScales.Length-1;scale>=0;scale--) {
				long divisor=(long)Math.Pow(1000,scale);
				int group=(int)(number/divisor%1000);
				if(group==0) {
					continue;
				}
				string groupWords=GetHundredsInWords(group);
				if(_arrayScales[scale]!="") {
					groupWords+=" "+_arrayScales[scale];
				}
				listParts.Add(groupWords);
			}
			return string.Join(" ",listParts);
		}

		private static string GetHundredsInWords(int number) {
			List<string> listParts=new List<string>();
			if(number>=100) {
				listParts.Add(_arrayOnes[number/100]+" hundred");
				number%=100;
			}
			if(number>=20) {
				string tens=_arrayTens[number/10];
				if(number%10>0) {
					tens+="-"+_arrayOnes[number%10];
				}
				listParts.Add(tens);
			}
			else if(number>0) {
				listParts.Add(_arrayOnes[number]);
			}
			return string.Join(" ",listParts);
		}

		///<summary>The numeric amount with two decimals, padded on the left with asterisks, for example "******1,250.00".</summary>
		public static string GetAmountFilled(decimal amount) {
			return Math.Abs(amount).ToString("N2").PadLeft(AMOUNT_FILL_WIDTH,'*');
		}

		///<summary>One line per other entry of the transaction (the splits, for the stub): the account and the amount.</summary>
		public static string GetSplitsText(List<JournalEntry> listJournalEntries,JournalEntry journalEntryCheck,Func<long,string> funcGetAccountDescript=null) {
			if(listJournalEntries==null) {
				return "";
			}
			if(funcGetAccountDescript==null) {
				funcGetAccountDescript=Accounts.GetDescript;
			}
			List<string> listLines=new List<string>();
			for(int i=0;i<listJournalEntries.Count;i++) {
				JournalEntry journalEntry=listJournalEntries[i];
				if(journalEntry==journalEntryCheck
					|| (journalEntryCheck!=null && journalEntry.JournalEntryNum!=0 && journalEntry.JournalEntryNum==journalEntryCheck.JournalEntryNum))
				{
					continue;
				}
				double amount=(journalEntry.DebitAmt!=0 ? journalEntry.DebitAmt : journalEntry.CreditAmt);
				listLines.Add(funcGetAccountDescript(journalEntry.AccountNum)+"  "+amount.ToString("n"));
			}
			return string.Join("\r\n",listLines);
		}

		///<summary>The text of the Printing audit entry: "Check #1001, $1,250.00, Payee".</summary>
		public static string GetAuditDescription(JournalEntry journalEntryCheck) {
			return Lans.g("CheckSheets","Check")+" #"+journalEntryCheck.CheckNumber.Trim()+", "+GetAmount(journalEntryCheck).ToString("c")
				+", "+journalEntryCheck.Payee.Trim();
		}

		///<summary>The values of every Check sheet output field, keyed by field name. Fields that need the check entry stay blank when there is none.</summary>
		public static Dictionary<string,string> GetFieldValues(Account account,List<JournalEntry> listJournalEntries,Func<long,string> funcGetAccountDescript=null) {
			Dictionary<string,string> dictValues=ListFieldNames.ToDictionary(x => x,x => "");
			GetPracticeOrClinic(out string title,out string address,out string cityStateZip);
			dictValues["practiceTitle"]=title;
			dictValues["practiceAddress"]=address;
			dictValues["practiceCityStateZip"]=cityStateZip;
			if(account==null) {
				return dictValues;
			}
			dictValues["accountName"]=account.Description;
			dictValues["bankNumber"]=account.BankNumber??"";
			JournalEntry journalEntryCheck=GetCheckEntry(listJournalEntries,account.AccountNum);
			if(journalEntryCheck==null) {
				return dictValues;
			}
			decimal amount=GetAmount(journalEntryCheck);
			dictValues["date"]=journalEntryCheck.DateDisplayed.ToShortDateString();
			dictValues["checkNumber"]=journalEntryCheck.CheckNumber.Trim();
			dictValues["payee"]=journalEntryCheck.Payee;
			dictValues["amount"]=GetAmountFilled(amount);
			dictValues["amountInWords"]=GetAmountInWords(amount);
			dictValues["memo"]=journalEntryCheck.Memo;
			dictValues["notes"]=journalEntryCheck.Notes;
			dictValues["splits"]=GetSplitsText(listJournalEntries,journalEntryCheck,funcGetAccountDescript);
			return dictValues;
		}

		///<summary>The name and address printed on the check: the clinic selected in the main window, or the practice when there is none.</summary>
		public static void GetPracticeOrClinic(out string title,out string address,out string cityStateZip) {
			Clinic clinic=null;
			if(Clinics.ClinicNum!=0) {
				clinic=Clinics.GetClinic(Clinics.ClinicNum);
			}
			if(clinic==null) {
				title=PrefC.GetString(PrefName.PracticeTitle);
				address=PrefC.GetString(PrefName.PracticeAddress);
				if(PrefC.GetString(PrefName.PracticeAddress2)!="") {
					address+=", "+PrefC.GetString(PrefName.PracticeAddress2);
				}
				cityStateZip=PrefC.GetString(PrefName.PracticeCity)+", "+PrefC.GetString(PrefName.PracticeST)+"  "+PrefC.GetString(PrefName.PracticeZip);
				return;
			}
			title=clinic.Description;
			address=clinic.Address;
			if(clinic.Address2!="") {
				address+=", "+clinic.Address2;
			}
			cityStateZip=clinic.City+", "+clinic.State+"  "+clinic.Zip;
		}

		///<summary>Field types Setup offers on a Check sheet. A check is printed, never filled in, so input fields, check boxes, combo boxes, patient
		///images and signature boxes are left out. Static text, lines, rectangles and images (for example a signature image) work as on other sheets.</summary>
		public static List<SheetFieldType> FilterFieldTypes(List<SheetFieldType> listSheetFieldTypes) {
			return listSheetFieldTypes.FindAll(x => x.In(SheetFieldType.OutputText,SheetFieldType.StaticText,SheetFieldType.Image,
				SheetFieldType.Line,SheetFieldType.Rectangle));
		}

		///<summary>Static text fields that can be filled on a Check sheet. There is no patient, so only the ones that need none.</summary>
		public static bool IsStaticTextFieldAllowed(EnumStaticTextField enumStaticTextField) {
			return enumStaticTextField.In(EnumStaticTextField.dateToday,EnumStaticTextField.dateTodayLong,EnumStaticTextField.practiceTitle);
		}
	}
}
