// Text typed into sheets in non-Latin scripts is saved and read back unchanged (the sheet tables are utf8). One scratch database on the local
// MariaDB with the sheet tables of the 26.1.70 reference schema, synthetic rows only, dropped afterwards. Skipped (inconclusive) when the server
// does not answer or when FORKMIGRATION_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M2SHEETSTests {
	[TestClass]
	public class UnicodeDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("m2sheets");
		private static string _skipReason;
		private static readonly string[] _arraySamples=new[] { Samples.Russian,Samples.Greek,Samples.Chinese,Samples.Japanese,Samples.Korean,Samples.Hebrew,
			Samples.Arabic,Samples.Hindi,Samples.Thai,Samples.Georgian,Samples.LatinWinAnsi };

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.Recreate(DB_NAME);
				SchemaSnapshot reference=SchemaVerifier.GetReference();
				using(MySqlConnection connection=TestDb.OpenServer()) {
					TestDb.Exec(connection,"USE `"+DB_NAME+"`");
					foreach(string tableName in new[] { "preference","sheet","sheetfield","sheetdef","sheetfielddef" }) {
						TestDb.Exec(connection,reference.GetTable(tableName).ToDdl());
					}
					//The CRUD layer reads RandomPrimaryKeys on every insert.
					TestDb.Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
				}
				TestDb.Connect(DB_NAME);
				Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="synthetic" };
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database "+DB_NAME+": "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				if(TestDb.DatabaseExists(DB_NAME)) {
					TestDb.Drop(DB_NAME);
				}
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			Samples.UseUsEnglish();
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		[TestMethod]
		public void FilledSheetFields_KeepNonLatinText() {
			foreach(string text in _arraySamples) {
				SheetField sheetField=Samples.TextField(text+"\r\n"+text,sheetFieldType:SheetFieldType.InputField);
				sheetField.SheetNum=1;
				sheetField.ReportableName=text;
				sheetField.UiLabelMobile=text;
				long sheetFieldNum=OpenDentBusiness.Crud.SheetFieldCrud.Insert(sheetField);
				SheetField sheetFieldLoaded=OpenDentBusiness.Crud.SheetFieldCrud.SelectOne(sheetFieldNum);
				Assert.AreEqual(text+"\r\n"+text,sheetFieldLoaded.FieldValue,text);
				Assert.AreEqual(text,sheetFieldLoaded.ReportableName,text);
				Assert.AreEqual(text,sheetFieldLoaded.UiLabelMobile,text);
			}
		}

		[TestMethod]
		public void TranslatedSheetFieldDefs_KeepNonLatinTextAndLanguage() {
			SheetDef sheetDef=new SheetDef { Description="Intake "+Samples.Chinese,SheetType=SheetTypeEnum.PatientForm,FontName="Arial",FontSize=11f,Width=850,Height=1100 };
			long sheetDefNum=OpenDentBusiness.Crud.SheetDefCrud.Insert(sheetDef);
			Assert.AreEqual("Intake "+Samples.Chinese,OpenDentBusiness.Crud.SheetDefCrud.SelectOne(sheetDefNum).Description);
			foreach(string language in new[] { "rus","zho","heb","ara","hin","kor" }) {
				SheetFieldDef sheetFieldDef=SheetFieldDef.NewStaticText(Samples.Russian+" "+Samples.Arabic+" "+Samples.Korean,11f,"Arial",false,10,10,300,20);
				sheetFieldDef.SheetDefNum=sheetDefNum;
				sheetFieldDef.Language=language;
				sheetFieldDef.ItemColor=Color.Black;
				long sheetFieldDefNum=OpenDentBusiness.Crud.SheetFieldDefCrud.Insert(sheetFieldDef);
				SheetFieldDef sheetFieldDefLoaded=OpenDentBusiness.Crud.SheetFieldDefCrud.SelectOne(sheetFieldDefNum);
				Assert.AreEqual(sheetFieldDef.FieldValue,sheetFieldDefLoaded.FieldValue,language);
				Assert.AreEqual(language,sheetFieldDefLoaded.Language);
			}
		}
	}
}
