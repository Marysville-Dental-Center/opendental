// Check printing against the scratch database: fill a Check sheet from an accounting transaction, render it to PDF, and use office check
// sheets (sheetdef.SheetType=28). Synthetic data only (backlog package A-SHEET).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using PdfSharp.Pdf;

namespace SheetTests {
	[TestClass]
	public class CheckDbTests {
		private static Account _accountChecking;
		private static Account _accountSupplies;
		private static Account _accountLab;
		private static bool _isSetUp;

		[TestInitialize]
		public void TestInitialize() {
			SheetDb.Begin();
			if(_isSetUp) {
				return;
			}
			Prefs.UpdateString(PrefName.PracticeTitle,"Synthetic Dental Group");
			Prefs.UpdateString(PrefName.PracticeAddress,"100 Example Way");
			Prefs.UpdateString(PrefName.PracticeAddress2,"Suite 2");
			Prefs.UpdateString(PrefName.PracticeCity,"Exampleton");
			Prefs.UpdateString(PrefName.PracticeST,"WA");
			Prefs.UpdateString(PrefName.PracticeZip,"98000");
			Cache.Refresh(InvalidType.Prefs);
			_accountChecking=InsertAccount("Operating checking (synthetic)",AccountType.Asset,"0000-1111 (synthetic)");
			_accountSupplies=InsertAccount("Dental supplies (synthetic)",AccountType.Expense,"");
			_accountLab=InsertAccount("Lab fees (synthetic)",AccountType.Expense,"");
			Accounts.RefreshCache();
			_isSetUp=true;
		}

		[TestMethod]
		public void InternalCheck_FillsFromTheTransaction() {
			long transactionNum=InsertTransaction(
				Je(_accountChecking,credit:1250.5,checkNumber:"1042",memo:"Invoice 77",payee:"Dental Supply Co (synthetic)",notes:"Net 30"),
				Je(_accountSupplies,debit:1250.5,checkNumber:"1042",memo:"Invoice 77",payee:"Dental Supply Co (synthetic)",notes:"Net 30"));
			Sheet sheet=FillCheck(SheetsInternal.GetSheetDef(SheetInternalType.Check),transactionNum);
			Assert.AreEqual(SheetTypeEnum.Check,sheet.SheetType);
			AssertField(sheet,"payee","Dental Supply Co (synthetic)");
			AssertField(sheet,"checkNumber","1042");
			AssertField(sheet,"date","6/2/2025");
			AssertField(sheet,"amount","******1,250.50");
			AssertField(sheet,"amountInWords","One thousand two hundred fifty and 50/100");
			AssertField(sheet,"memo","Invoice 77");
			AssertField(sheet,"notes","Net 30");
			AssertField(sheet,"accountName","Operating checking (synthetic)");
			AssertField(sheet,"bankNumber","0000-1111 (synthetic)");
			AssertField(sheet,"practiceTitle","Synthetic Dental Group");
			AssertField(sheet,"practiceAddress","100 Example Way, Suite 2");
			AssertField(sheet,"practiceCityStateZip","Exampleton, WA  98000");
			AssertField(sheet,"splits","Dental supplies (synthetic)  1,250.50");
			Assert.AreEqual("",CheckSheets.GetPrintError(_accountChecking,JournalEntries.GetForTrans(transactionNum)));
		}

		[TestMethod]
		public void SplitCheck_ListsEverySplitOnTheStub() {
			long transactionNum=InsertTransaction(
				Je(_accountSupplies,debit:300,payee:"Lab and Supply (synthetic)",checkNumber:"1043"),
				Je(_accountChecking,credit:425.75,payee:"Lab and Supply (synthetic)",checkNumber:"1043"),
				Je(_accountLab,debit:125.75,payee:"Lab and Supply (synthetic)",checkNumber:"1043"));
			Sheet sheet=FillCheck(SheetsInternal.GetSheetDef(SheetInternalType.Check),transactionNum);
			AssertField(sheet,"amount","********425.75");
			AssertField(sheet,"amountInWords","Four hundred twenty-five and 75/100");
			AssertField(sheet,"splits","Dental supplies (synthetic)  300.00\r\nLab fees (synthetic)  125.75");
		}

		[TestMethod]
		public void StaticTextOnACheck_FillsWithoutAPatient() {
			long transactionNum=InsertTransaction(
				Je(_accountChecking,credit:10,payee:"P (synthetic)",checkNumber:"1044"),
				Je(_accountSupplies,debit:10,payee:"P (synthetic)",checkNumber:"1044"));
			SheetDef sheetDef=SheetsInternal.GetSheetDef(SheetInternalType.Check);
			sheetDef.SheetFieldDefs.Add(SheetFieldDef.NewStaticText("Issued by [practiceTitle] on [dateToday]",9,"Arial",false,50,330,400,17));
			Sheet sheet=FillCheck(sheetDef,transactionNum);
			SheetField sheetField=sheet.SheetFields.Single(x => x.FieldType==SheetFieldType.StaticText && x.FieldValue.StartsWith("Issued by"));
			Assert.AreEqual("Issued by Synthetic Dental Group on "+DateTime.Today.ToShortDateString(),sheetField.FieldValue);
		}

		[TestMethod]
		public void Check_RendersToPdf() {
			long transactionNum=InsertTransaction(
				Je(_accountChecking,credit:89.99,payee:"Pdf Payee (synthetic)",checkNumber:"1045",memo:"PDF test"),
				Je(_accountSupplies,debit:89.99,payee:"Pdf Payee (synthetic)",checkNumber:"1045",memo:"PDF test"));
			Sheet sheet=FillCheck(SheetsInternal.GetSheetDef(SheetInternalType.Check),transactionNum);
			string path=Path.Combine(Path.GetTempPath(),"asheet_check_"+Guid.NewGuid().ToString("N")+".pdf");
			try {
				PdfDocument pdfDocument=new SheetDrawingJob().CreatePdf(sheet);
				Assert.AreEqual(1,pdfDocument.PageCount,"A check is one page.");
				Assert.IsTrue(SheetDrawingJob.SavePdfToFile(pdfDocument,path));
				byte[] arrayBytes=File.ReadAllBytes(path);
				Assert.IsTrue(arrayBytes.Length>1000,"PDF has content: "+arrayBytes.Length+" bytes");
				Assert.AreEqual("%PDF",Encoding.ASCII.GetString(arrayBytes,0,4));
			}
			finally {
				if(File.Exists(path)) {
					File.Delete(path);
				}
			}
		}

		[TestMethod]
		public void OfficeCheckSheet_SavedAsType28_LoadsAndFills() {
			//A custom check sheet as Open Dental 25.3 saves it: sheetdef.SheetType=28, written straight to the table.
			long sheetDefNum;
			using(MySqlConnection connection=TestDb.OpenServer()) {
				TestDb.Exec(connection,"USE `"+SheetDb.DbName+"`");
				TestDb.Exec(connection,"INSERT INTO sheetdef (Description,SheetType,FontSize,FontName,Width,Height,IsLandscape,PageCount,IsMultiPage,"
					+"BypassGlobalLock,HasMobileLayout,DateTCreated,RevID,AutoCheckSaveImage,AutoCheckSaveImageDocCategory) "
					+"VALUES ('Office check (synthetic)',28,10,'Arial',850,1100,0,1,0,0,0,'0001-01-01 00:00:00',1,0,0)");
				sheetDefNum=long.Parse(TestDb.Scalar(connection,"SELECT LAST_INSERT_ID()"));
				foreach(string fieldName in new[] { "payee","amount","checkNumber" }) {
					TestDb.Exec(connection,"INSERT INTO sheetfielddef (SheetDefNum,FieldType,FieldName,FieldValue,FontSize,FontName,FontIsBold,XPos,YPos,Width,"
						+"Height,GrowthBehavior,RadioButtonValue,RadioButtonGroup,IsRequired,TabOrder,ReportableName,TextAlign,IsPaymentOption,IsLocked,"
						+"ItemColor,TabOrderMobile,UiLabelMobile,UiLabelMobileRadioButton,LayoutMode,Language,CanElectronicallySign,IsSigProvRestricted) "
						+"VALUES ("+sheetDefNum+",0,'"+fieldName+"','',10,'Arial',0,50,50,200,18,0,'','',0,0,'',0,0,0,-16777216,0,'','',0,'',0,0)");
				}
			}
			Cache.Refresh(InvalidType.Sheets);
			SheetDef sheetDef=SheetDefs.GetCustomForType(SheetTypeEnum.Check).Single(x => x.SheetDefNum==sheetDefNum);
			Assert.AreEqual(SheetTypeEnum.Check,sheetDef.SheetType);
			SheetDefs.GetFieldsAndParameters(sheetDef);
			Assert.AreEqual(3,sheetDef.SheetFieldDefs.Count);
			long transactionNum=InsertTransaction(
				Je(_accountChecking,credit:5,payee:"Office Layout Payee (synthetic)",checkNumber:"1046"),
				Je(_accountSupplies,debit:5,payee:"Office Layout Payee (synthetic)",checkNumber:"1046"));
			Sheet sheet=FillCheck(sheetDef,transactionNum);
			AssertField(sheet,"payee","Office Layout Payee (synthetic)");
			AssertField(sheet,"amount","**********5.00");
			AssertField(sheet,"checkNumber","1046");
			Assert.AreEqual(1,new SheetDrawingJob().CreatePdf(sheet).PageCount);
		}

		[TestMethod]
		public void InternalCheck_CopiedToACustomSheet_KeepsTypeAndFields() {
			SheetDef sheetDefInternal=SheetsInternal.GetSheetDef(SheetInternalType.Check);
			SheetDef sheetDefCopy=SheetsInternal.GetSheetDef(SheetInternalType.Check);
			sheetDefCopy.IsNew=true;
			sheetDefCopy.Description="Copied check (synthetic)";
			long sheetDefNum=SheetDefs.InsertOrUpdate(sheetDefCopy);
			Cache.Refresh(InvalidType.Sheets);
			SheetDef sheetDefLoaded=SheetDefs.GetSheetDef(sheetDefNum);
			Assert.AreEqual(SheetTypeEnum.Check,sheetDefLoaded.SheetType);
			Assert.AreEqual(28,int.Parse(DataCore.GetScalar("SELECT SheetType FROM sheetdef WHERE SheetDefNum="+sheetDefNum)));
			Assert.AreEqual(sheetDefInternal.SheetFieldDefs.Count,sheetDefLoaded.SheetFieldDefs.Count);
		}

		private static Sheet FillCheck(SheetDef sheetDef,long transactionNum) {
			Sheet sheet=SheetUtil.CreateSheet(sheetDef,0);
			SheetParameter.SetParameter(sheet,CheckSheets.PARAM_TRANSACTION_NUM,transactionNum);
			SheetParameter.SetParameter(sheet,CheckSheets.PARAM_ACCOUNT_NUM,_accountChecking.AccountNum);
			Assert.AreEqual("",SheetFiller.FillFields(sheet));
			SheetUtil.CalculateHeights(sheet);
			return sheet;
		}

		private static void AssertField(Sheet sheet,string fieldName,string expected) {
			List<SheetField> listFields=sheet.SheetFields.FindAll(x => x.FieldType==SheetFieldType.OutputText && x.FieldName==fieldName);
			Assert.IsTrue(listFields.Count>0,"No field "+fieldName);
			foreach(SheetField sheetField in listFields) {
				Assert.AreEqual(expected,sheetField.FieldValue,fieldName);
			}
		}

		private static long InsertTransaction(params JournalEntry[] arrayJournalEntries) {
			Transaction transaction=new Transaction { UserNum=Security.CurUser.UserNum };
			Transactions.Insert(transaction);
			foreach(JournalEntry journalEntry in arrayJournalEntries) {
				journalEntry.TransactionNum=transaction.TransactionNum;
				JournalEntries.Insert(journalEntry);
			}
			return transaction.TransactionNum;
		}

		private static JournalEntry Je(Account account,double debit=0,double credit=0,string checkNumber="",string memo="",string payee="",string notes="") {
			return new JournalEntry {
				AccountNum=account.AccountNum,
				DateDisplayed=new DateTime(2025,6,2),
				DebitAmt=debit,
				CreditAmt=credit,
				CheckNumber=checkNumber,
				Memo=memo,
				Splits="",
				Payee=payee,
				Notes=notes,
			};
		}

		private static Account InsertAccount(string description,AccountType accountType,string bankNumber) {
			Account account=new Account { Description=description,AcctType=accountType,BankNumber=bankNumber };
			OpenDentBusiness.Crud.AccountCrud.Insert(account);
			return account;
		}
	}
}
