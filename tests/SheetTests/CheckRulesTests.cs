// Check printing rules that need no database (Open Dental 25.3 SheetTypeEnum.Check=28; backlog package A-SHEET).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace SheetTests {
	[TestClass]
	public class CheckRulesTests {
		private static readonly Account _accountChecking=new Account { AccountNum=11,Description="Checking (synthetic)",AcctType=AccountType.Asset,BankNumber="12-345" };
		private static readonly Account _accountSupplies=new Account { AccountNum=12,Description="Supplies (synthetic)",AcctType=AccountType.Expense,BankNumber="" };
		private static readonly Account _accountLab=new Account { AccountNum=13,Description="Lab (synthetic)",AcctType=AccountType.Expense,BankNumber="" };

		[TestInitialize]
		public void TestInitialize() {
			SheetDb.UseUsEnglish();
		}

		[TestMethod]
		public void SheetTypeCheck_IsPublishedNumber28() {
			//OpenDentalDocumentation25-3.xml, sheetdef.SheetType: Check is 28.
			Assert.AreEqual(28,(int)SheetTypeEnum.Check);
			Assert.AreEqual(SheetTypeEnum.Check,(SheetTypeEnum)Enum.Parse(typeof(SheetTypeEnum),"Check"));
		}

		[TestMethod]
		public void AmountInWords_WritesDollarsAndCents() {
			Assert.AreEqual("One thousand two hundred fifty and 50/100",CheckSheets.GetAmountInWords(1250.50m));
			Assert.AreEqual("Zero and 99/100",CheckSheets.GetAmountInWords(0.99m));
			Assert.AreEqual("One hundred and 00/100",CheckSheets.GetAmountInWords(100m));
			Assert.AreEqual("Twenty-one and 05/100",CheckSheets.GetAmountInWords(21.05m));
			Assert.AreEqual("Nineteen and 00/100",CheckSheets.GetAmountInWords(19m));
			Assert.AreEqual("One million and 00/100",CheckSheets.GetAmountInWords(1000000m));
			Assert.AreEqual("Two thousand and 10/100",CheckSheets.GetAmountInWords(2000.1m));
			Assert.AreEqual("Twelve million three hundred forty-five thousand six hundred seventy-eight and 90/100",
				CheckSheets.GetAmountInWords(12345678.9m));
			Assert.AreEqual("Ten and 01/100",CheckSheets.GetAmountInWords(10.005m),"Half a cent rounds away from zero.");
			Assert.AreEqual("Five and 00/100",CheckSheets.GetAmountInWords(-5m),"The sign is ignored.");
		}

		[TestMethod]
		public void AmountFilled_PadsWithAsterisks() {
			Assert.AreEqual("******1,250.50",CheckSheets.GetAmountFilled(1250.5m));
			Assert.AreEqual("**********0.07",CheckSheets.GetAmountFilled(0.07m));
			Assert.AreEqual(CheckSheets.AMOUNT_FILL_WIDTH,CheckSheets.GetAmountFilled(12.34m).Length);
			Assert.AreEqual("1,234,567,890.00",CheckSheets.GetAmountFilled(1234567890m),"Too long to pad: never cut.");
		}

		[TestMethod]
		public void CanPrintFrom_AssetAccountsOnly() {
			Assert.IsTrue(CheckSheets.CanPrintFrom(_accountChecking));
			Assert.IsFalse(CheckSheets.CanPrintFrom(_accountSupplies));
			Assert.IsFalse(CheckSheets.CanPrintFrom(null));
			foreach(AccountType accountType in new[] { AccountType.Liability,AccountType.Equity,AccountType.Income,AccountType.Expense }) {
				Assert.IsFalse(CheckSheets.CanPrintFrom(new Account { AcctType=accountType }),accountType.ToString());
			}
		}

		[TestMethod]
		public void CheckEntry_SimpleAndSplitTransactions() {
			List<JournalEntry> listSimple=SimpleCheck(250);
			Assert.AreSame(listSimple[0],CheckSheets.GetCheckEntry(listSimple,_accountChecking.AccountNum));
			//A split check: one credit to checking, two debits to expense accounts.
			List<JournalEntry> listSplit=new List<JournalEntry> {
				Je(_accountSupplies,debit:100),
				Je(_accountChecking,credit:175,payee:"Dental Supply Co (synthetic)",checkNumber:"2001"),
				Je(_accountLab,debit:75),
			};
			Assert.AreSame(listSplit[1],CheckSheets.GetCheckEntry(listSplit,_accountChecking.AccountNum));
			Assert.AreEqual("",CheckSheets.GetPrintError(_accountChecking,listSplit));
			//The asset account twice, one debit and one credit: the credit is the check.
			List<JournalEntry> listTwice=new List<JournalEntry> {
				Je(_accountChecking,debit:20),
				Je(_accountChecking,credit:120,payee:"P",checkNumber:"1"),
				Je(_accountSupplies,debit:100),
			};
			Assert.AreSame(listTwice[1],CheckSheets.GetCheckEntry(listTwice,_accountChecking.AccountNum));
			//Two credits to the asset account: no single check.
			List<JournalEntry> listTwoCredits=new List<JournalEntry> {
				Je(_accountChecking,credit:20),
				Je(_accountChecking,credit:30),
				Je(_accountSupplies,debit:50),
			};
			Assert.IsNull(CheckSheets.GetCheckEntry(listTwoCredits,_accountChecking.AccountNum));
			Assert.IsNull(CheckSheets.GetCheckEntry(new List<JournalEntry> { Je(_accountSupplies,debit:5) },_accountChecking.AccountNum));
			Assert.IsNull(CheckSheets.GetCheckEntry(null,_accountChecking.AccountNum));
		}

		[TestMethod]
		public void PrintError_NeedsPayeeCheckNumberAndPaymentOut() {
			Assert.AreEqual("",CheckSheets.GetPrintError(_accountChecking,SimpleCheck(250)));
			StringAssert.Contains(CheckSheets.GetPrintError(_accountSupplies,SimpleCheck(250)),"asset");
			StringAssert.Contains(CheckSheets.GetPrintError(_accountChecking,SimpleCheck(250,payee:" ")),"Payee");
			StringAssert.Contains(CheckSheets.GetPrintError(_accountChecking,SimpleCheck(250,checkNumber:"")),"Check Number");
			//A deposit into checking (debit) is not a check.
			List<JournalEntry> listDeposit=new List<JournalEntry> {
				Je(_accountChecking,debit:300,payee:"P",checkNumber:"DEP"),
				Je(_accountSupplies,credit:300),
			};
			StringAssert.Contains(CheckSheets.GetPrintError(_accountChecking,listDeposit),"credit");
			StringAssert.Contains(CheckSheets.GetPrintError(_accountChecking,SimpleCheck(0)),"credit");
			StringAssert.Contains(CheckSheets.GetPrintError(_accountChecking,new List<JournalEntry>()),"exactly one credit");
		}

		[TestMethod]
		public void SplitsText_ListsTheOtherEntries() {
			List<JournalEntry> listSplit=new List<JournalEntry> {
				Je(_accountSupplies,debit:100),
				Je(_accountChecking,credit:175.25,payee:"P",checkNumber:"1"),
				Je(_accountLab,debit:75.25),
			};
			string splits=CheckSheets.GetSplitsText(listSplit,listSplit[1],GetDescript);
			Assert.AreEqual("Supplies (synthetic)  100.00\r\nLab (synthetic)  75.25",splits);
			//Saved entries are matched by primary key too, so a copy of the check entry is still left out.
			listSplit.ForEach(x => x.JournalEntryNum=listSplit.IndexOf(x)+1);
			Assert.AreEqual(splits,CheckSheets.GetSplitsText(listSplit,listSplit[1].Copy(),GetDescript));
		}

		[TestMethod]
		public void AuditDescription_NamesNumberAmountAndPayee() {
			JournalEntry journalEntry=Je(_accountChecking,credit:1250.5,payee:" Dental Supply Co (synthetic) ",checkNumber:" 1042 ");
			Assert.AreEqual("Check #1042, $1,250.50, Dental Supply Co (synthetic)",CheckSheets.GetAuditDescription(journalEntry));
		}

		[TestMethod]
		public void FieldTypes_OnlyPrintableOnes() {
			List<SheetFieldType> listAll=Enum.GetValues(typeof(SheetFieldType)).Cast<SheetFieldType>().ToList();
			List<SheetFieldType> listCheck=CheckSheets.FilterFieldTypes(listAll);
			CollectionAssert.AreEquivalent(new[] { SheetFieldType.OutputText,SheetFieldType.StaticText,SheetFieldType.Image,SheetFieldType.Line,
				SheetFieldType.Rectangle },listCheck);
			Assert.IsTrue(CheckSheets.IsStaticTextFieldAllowed(EnumStaticTextField.practiceTitle));
			Assert.IsTrue(CheckSheets.IsStaticTextFieldAllowed(EnumStaticTextField.dateToday));
			Assert.IsFalse(CheckSheets.IsStaticTextFieldAllowed(EnumStaticTextField.nameFL),"A check has no patient.");
		}

		[TestMethod]
		public void SheetFramework_KnowsTheCheckType() {
			List<SheetParameter> listParameters=SheetParameter.GetForType(SheetTypeEnum.Check);
			CollectionAssert.AreEqual(new[] { CheckSheets.PARAM_TRANSACTION_NUM,CheckSheets.PARAM_ACCOUNT_NUM },listParameters.Select(x => x.ParamName).ToList());
			Assert.IsTrue(listParameters.TrueForAll(x => x.IsRequired));
			CollectionAssert.AreEqual(CheckSheets.ListFieldNames,SheetFieldsAvailable.GetList(SheetTypeEnum.Check,OutInCheck.Out).Select(x => x.FieldName).ToList());
			Assert.AreEqual(0,SheetFieldsAvailable.GetList(SheetTypeEnum.Check,OutInCheck.In).Count,"Checks are never filled in.");
			Assert.AreEqual(0,SheetFieldsAvailable.GetList(SheetTypeEnum.Check,OutInCheck.Check).Count);
			Assert.AreEqual(SheetInternalType.Check,SheetsInternal.GetInternalType(SheetTypeEnum.Check));
			Assert.IsTrue(Sheets.SheetTypeIsSinglePage(SheetTypeEnum.Check));
		}

		[TestMethod]
		public void InternalCheckLayout_LoadsAndUsesOnlyCheckFields() {
			SheetDef sheetDef=SheetsInternal.GetSheetDef(SheetInternalType.Check);
			Assert.AreEqual(SheetTypeEnum.Check,sheetDef.SheetType);
			Assert.AreEqual("Check",sheetDef.Description);
			Assert.AreEqual(850,sheetDef.Width);
			Assert.AreEqual(1100,sheetDef.Height);
			Assert.IsFalse(sheetDef.IsLandscape);
			Assert.AreEqual(2,sheetDef.Parameters.Count);
			List<SheetFieldDef> listOutputs=sheetDef.SheetFieldDefs.FindAll(x => x.FieldType==SheetFieldType.OutputText);
			Assert.IsTrue(listOutputs.Count>0);
			foreach(SheetFieldDef sheetFieldDef in listOutputs) {
				CollectionAssert.Contains(CheckSheets.ListFieldNames,sheetFieldDef.FieldName);
			}
			//The check itself carries every field a bank needs to read; the stubs repeat the details.
			foreach(string fieldName in new[] { "date","checkNumber","payee","amount","amountInWords","memo","practiceTitle" }) {
				Assert.IsTrue(listOutputs.Any(x => x.FieldName==fieldName && x.YPos<366),"Missing on the check: "+fieldName);
			}
			Assert.IsTrue(listOutputs.Any(x => x.FieldName=="splits" && x.YPos>366),"The stub lists the splits.");
			List<SheetFieldType> listAllowed=CheckSheets.FilterFieldTypes(Enum.GetValues(typeof(SheetFieldType)).Cast<SheetFieldType>().ToList());
			Assert.IsTrue(sheetDef.SheetFieldDefs.TrueForAll(x => listAllowed.Contains(x.FieldType)),"Only field types Setup offers on a check.");
			Assert.IsTrue(sheetDef.SheetFieldDefs.TrueForAll(x => x.XPos+x.Width<=sheetDef.Width && x.YPos+x.Height<=sheetDef.Height),"Inside the page.");
			//Static text uses no bracketed field that needs a patient.
			foreach(SheetFieldDef sheetFieldDef in sheetDef.SheetFieldDefs.FindAll(x => x.FieldType==SheetFieldType.StaticText)) {
				Assert.IsFalse(sheetFieldDef.FieldValue.Contains("["),sheetFieldDef.FieldValue);
			}
			//Offered in Setup, Sheets, as an internal sheet to copy.
			Assert.IsTrue(CodeBase.EnumTools.GetAttributeOrDefault<SheetInternalAttribute>(SheetInternalType.Check).DoShowInInternalList);
			Assert.AreEqual(SheetTypeEnum.Check,SheetsInternal.GetSheetDef(SheetTypeEnum.Check).SheetType);
		}

		private static string GetDescript(long accountNum) {
			return new[] { _accountChecking,_accountSupplies,_accountLab }.First(x => x.AccountNum==accountNum).Description;
		}

		///<summary>The two entries the simple view of Edit Transaction saves for a check: a credit to checking and a debit to supplies.</summary>
		private static List<JournalEntry> SimpleCheck(double amount,string payee="Dental Supply Co (synthetic)",string checkNumber="1001") {
			return new List<JournalEntry> {
				Je(_accountChecking,credit:amount,payee:payee,checkNumber:checkNumber),
				Je(_accountSupplies,debit:amount,payee:payee,checkNumber:checkNumber),
			};
		}

		private static JournalEntry Je(Account account,double debit=0,double credit=0,string payee="",string checkNumber="") {
			return new JournalEntry {
				AccountNum=account.AccountNum,
				DateDisplayed=new DateTime(2025,6,2),
				DebitAmt=debit,
				CreditAmt=credit,
				CheckNumber=checkNumber,
				Memo="",
				Splits="",
				Payee=payee,
				Notes="",
			};
		}
	}
}
