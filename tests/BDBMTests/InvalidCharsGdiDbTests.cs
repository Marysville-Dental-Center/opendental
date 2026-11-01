// The Invalid Characters (GDI+) tool (Open Dental 25.2): scan and fix on the scratch database, signed rows never changed, one Setup log entry per
// fix. Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Crud;
using UnitTestsCore;

namespace BDBMTests {
	[TestClass]
	public class InvalidCharsGdiDbTests {
		[TestInitialize]
		public void Init() {
			BDbmDb.Begin();
		}

		private static List<string> AllLabels() {
			return DatabaseMaintenances.GetInvalidCharTargets().Select(x => x.Label).ToList();
		}

		private static DbmInvalidCharResult Get(List<DbmInvalidCharResult> listResults,string label) {
			DbmInvalidCharResult result=listResults.FirstOrDefault(x => x.Label==label);
			Assert.IsNotNull(result,label+" not in the results.");
			return result;
		}

		[TestMethod]
		public void Targets_CoverTheDesignedColumns() {
			List<string> listLabels=AllLabels();
			string[] arrayExpected=new string[] { "patient.LName","patient.FName","patient.MiddleI","patient.Preferred","patient.Address",
				"patient.Address2","patient.City","patient.State","patient.Zip","patient.AddrNote","patient.FamFinUrgNote","patient.MedUrgNote",
				"patient.ApptModNote","appointment.Note","appointment.ProcDescript","procnote.Note","commlog.Note","definition.ItemName",
				"sheetfield.FieldValue","wikipage.PageContent" };
			CollectionAssert.AreEqual(arrayExpected,listLabels);
			List<DbmInvalidCharTarget> listTargets=DatabaseMaintenances.GetInvalidCharTargets();
			CollectionAssert.AreEquivalent(new[] { "procnote.Note","commlog.Note","sheetfield.FieldValue" },
				listTargets.FindAll(x => x.SignedCondition!="").Select(x => x.Label).ToList(),"The columns a signature covers.");
		}

		[TestMethod]
		public void ScanAndFix_RemovesBadCharactersAndNeverTouchesSignedRows() {
			List<string> listLabels=AllLabels();
			DatabaseMaintenances.InvalidCharsGdiFix(listLabels);//Start clean: only signed rows can be left.
			List<DbmInvalidCharResult> listBaseline=DatabaseMaintenances.InvalidCharsGdiScan(listLabels);
			Assert.AreEqual(0,listBaseline.Sum(x => x.CountRows));
			//Seed one bad value per kind of column.
			Patient patient=BDbmDb.CreatePatient("Gdi");
			BDbmDb.SetText("patient","LName","PatNum",patient.PatNum,"Bdbm\u0001Bad");
			BDbmDb.SetText("patient","Preferred","PatNum",patient.PatNum,"Pref\uFFFF");
			BDbmDb.SetText("patient","AddrNote","PatNum",patient.PatNum,"Line one\r\nLine\ttwo\u0000");
			long aptNum=AppointmentCrud.Insert(new Appointment { PatNum=patient.PatNum,AptDateTime=new DateTime(2025,3,4,9,0,0),Pattern="/XX/" });
			BDbmDb.SetText("appointment","Note","AptNum",aptNum,"Sun \u2600 note\u0002");
			BDbmDb.SetText("appointment","ProcDescript","AptNum",aptNum,"Exam\u0003");
			long procNoteNumUnsigned=ProcNoteCrud.Insert(new ProcNote { PatNum=patient.PatNum,Note="x" });
			BDbmDb.SetText("procnote","Note","ProcNoteNum",procNoteNumUnsigned,"Unsigned\u0004");
			long procNoteNumSigned=ProcNoteCrud.Insert(new ProcNote { PatNum=patient.PatNum,Note="x",Signature="SyntheticSignature" });
			BDbmDb.SetText("procnote","Note","ProcNoteNum",procNoteNumSigned,"Signed\u0005");
			Commlog commlogUnsigned=CommlogT.CreateCommlog(patient.PatNum,text:"x");
			BDbmDb.SetText("commlog","Note","CommlogNum",commlogUnsigned.CommlogNum,"Comm\u0006");
			Commlog commlogSigned=CommlogT.CreateCommlog(patient.PatNum,text:"x",signature:"SyntheticSignature");
			BDbmDb.SetText("commlog","Note","CommlogNum",commlogSigned.CommlogNum,"CommSigned\u0007");
			Def def=DefT.CreateDefinition(DefCat.CommLogTypes,"BdbmDef");
			BDbmDb.SetText("definition","ItemName","DefNum",def.DefNum,"Def\u000E");
			long sheetNumUnsigned=900000000+new Random().Next(1,10000000);
			long sheetNumSigned=sheetNumUnsigned+1;
			long sheetFieldNumUnsigned=SheetFieldCrud.Insert(new SheetField { SheetNum=sheetNumUnsigned,FieldType=SheetFieldType.InputField,FieldName="f" });
			BDbmDb.SetText("sheetfield","FieldValue","SheetFieldNum",sheetFieldNumUnsigned,"Val\u000F");
			long sheetFieldNumOnSignedSheet=SheetFieldCrud.Insert(new SheetField { SheetNum=sheetNumSigned,FieldType=SheetFieldType.InputField,FieldName="f" });
			BDbmDb.SetText("sheetfield","FieldValue","SheetFieldNum",sheetFieldNumOnSignedSheet,"Val\u0010");
			long sheetFieldNumSigBox=SheetFieldCrud.Insert(new SheetField { SheetNum=sheetNumSigned,FieldType=SheetFieldType.SigBox,FieldName="" });
			BDbmDb.SetText("sheetfield","FieldValue","SheetFieldNum",sheetFieldNumSigBox,"1SyntheticSignature");
			long wikiPageNum=WikiPageCrud.Insert(new WikiPage { PageTitle="Bdbm"+Guid.NewGuid().ToString("N"),PageContent="x",DateTimeSaved=DateTime.Now });
			BDbmDb.SetText("wikipage","PageContent","WikiPageNum",wikiPageNum,"Wiki\uFFFE content \u0011");
			//Scan changes nothing and reports exactly what was seeded.
			List<DbmInvalidCharResult> listScan=DatabaseMaintenances.InvalidCharsGdiScan(listLabels);
			Assert.AreEqual(1,Get(listScan,"patient.LName").CountRows);
			Assert.AreEqual(1,Get(listScan,"patient.Preferred").CountRows);
			Assert.AreEqual(1,Get(listScan,"patient.AddrNote").CountChars,"Tab and line breaks are not counted.");
			Assert.AreEqual(1,Get(listScan,"appointment.Note").CountRows);
			Assert.AreEqual(1,Get(listScan,"appointment.ProcDescript").CountRows);
			Assert.AreEqual(1,Get(listScan,"procnote.Note").CountRows);
			Assert.AreEqual(Get(listBaseline,"procnote.Note").CountRowsSigned+1,Get(listScan,"procnote.Note").CountRowsSigned);
			Assert.AreEqual(1,Get(listScan,"commlog.Note").CountRows);
			Assert.AreEqual(Get(listBaseline,"commlog.Note").CountRowsSigned+1,Get(listScan,"commlog.Note").CountRowsSigned);
			Assert.AreEqual(1,Get(listScan,"definition.ItemName").CountRows);
			Assert.AreEqual(1,Get(listScan,"sheetfield.FieldValue").CountRows);
			Assert.AreEqual(Get(listBaseline,"sheetfield.FieldValue").CountRowsSigned+1,Get(listScan,"sheetfield.FieldValue").CountRowsSigned,
				"The field on the signed sheet. The signature box itself holds no bad character.");
			Assert.AreEqual(1,Get(listScan,"wikipage.PageContent").CountRows);
			Assert.AreEqual(2,Get(listScan,"wikipage.PageContent").CountChars);
			Assert.AreEqual(10,listScan.Sum(x => x.CountRows));
			Assert.AreEqual(0,listScan.Sum(x => x.CountRowsFixed));
			Assert.AreEqual("Bdbm\u0001Bad",BDbmDb.GetText("patient","LName","PatNum",patient.PatNum));
			List<DbmInvalidCharPreview> listPreviews=listScan.SelectMany(x => x.ListPreviews).ToList();
			DbmInvalidCharPreview previewLName=listPreviews.First(x => x.Label=="patient.LName" && x.PriKey==patient.PatNum);
			Assert.AreEqual("Bdbm[U+0001]Bad",previewLName.ValueShown);
			Assert.IsFalse(previewLName.IsSigned);
			Assert.IsTrue(listPreviews.First(x => x.Label=="procnote.Note" && x.PriKey==procNoteNumSigned).IsSigned);
			Assert.AreEqual("Wiki[U+FFFE] content [U+0011]",listPreviews.First(x => x.Label=="wikipage.PageContent" && x.PriKey==wikiPageNum).ValueShown);
			//Fix.
			int countSetupLogs=BDbmDb.GetLogs(EnumPermType.Setup,0).Count;
			List<DbmInvalidCharResult> listFixed=DatabaseMaintenances.InvalidCharsGdiFix(listLabels);
			Assert.AreEqual(10,listFixed.Sum(x => x.CountRowsFixed));
			Assert.AreEqual(1,Get(listFixed,"patient.LName").CountRowsFixed);
			Assert.AreEqual("BdbmBad",BDbmDb.GetText("patient","LName","PatNum",patient.PatNum));
			Assert.AreEqual("Pref",BDbmDb.GetText("patient","Preferred","PatNum",patient.PatNum));
			Assert.AreEqual("Line one\r\nLine\ttwo",BDbmDb.GetText("patient","AddrNote","PatNum",patient.PatNum));
			Assert.AreEqual("Sun \u2600 note",BDbmDb.GetText("appointment","Note","AptNum",aptNum),"Other symbols are stored exactly as they were.");
			Assert.AreEqual("Exam",BDbmDb.GetText("appointment","ProcDescript","AptNum",aptNum));
			Assert.AreEqual("Unsigned",BDbmDb.GetText("procnote","Note","ProcNoteNum",procNoteNumUnsigned));
			Assert.AreEqual("Signed\u0005",BDbmDb.GetText("procnote","Note","ProcNoteNum",procNoteNumSigned),"Signed notes are never changed.");
			Assert.AreEqual("Comm",BDbmDb.GetText("commlog","Note","CommlogNum",commlogUnsigned.CommlogNum));
			Assert.AreEqual("CommSigned\u0007",BDbmDb.GetText("commlog","Note","CommlogNum",commlogSigned.CommlogNum));
			Assert.AreEqual("Def",BDbmDb.GetText("definition","ItemName","DefNum",def.DefNum));
			Assert.AreEqual("Val",BDbmDb.GetText("sheetfield","FieldValue","SheetFieldNum",sheetFieldNumUnsigned));
			Assert.AreEqual("Val\u0010",BDbmDb.GetText("sheetfield","FieldValue","SheetFieldNum",sheetFieldNumOnSignedSheet));
			Assert.AreEqual("1SyntheticSignature",BDbmDb.GetText("sheetfield","FieldValue","SheetFieldNum",sheetFieldNumSigBox));
			Assert.AreEqual("Wiki content ",BDbmDb.GetText("wikipage","PageContent","WikiPageNum",wikiPageNum));
			List<SecurityLog> listSetupLogs=BDbmDb.GetLogs(EnumPermType.Setup,0);
			Assert.AreEqual(countSetupLogs+1,listSetupLogs.Count,"One Setup log entry per fix.");
			string logText=listSetupLogs.Last().LogText;
			StringAssert.Contains(logText,"Invalid Characters (GDI+) removed 11 characters from 10 values.");
			StringAssert.Contains(logText,"patient.LName: fixed 1, signed 0");
			StringAssert.Contains(logText,"procnote.Note: fixed 1, signed "+Get(listScan,"procnote.Note").CountRowsSigned);
			//Afterwards only the signed rows are found.
			List<DbmInvalidCharResult> listAfter=DatabaseMaintenances.InvalidCharsGdiScan(listLabels);
			Assert.AreEqual(0,listAfter.Sum(x => x.CountRows));
			Assert.AreEqual(listScan.Sum(x => x.CountRowsSigned),listAfter.Sum(x => x.CountRowsSigned));
		}

		[TestMethod]
		public void Scan_OnlyTheChosenColumnsAndAtMostTheRequestedPreviews() {
			Patient patient=BDbmDb.CreatePatient("GdiChosen");
			BDbmDb.SetText("patient","City","PatNum",patient.PatNum,"City\u0012");
			for(int i=0;i<3;i++) {
				Commlog commlog=CommlogT.CreateCommlog(patient.PatNum,text:"x");
				BDbmDb.SetText("commlog","Note","CommlogNum",commlog.CommlogNum,"Chosen "+i+"\u0013");
			}
			List<DbmInvalidCharResult> listResults=DatabaseMaintenances.InvalidCharsGdiScan(new List<string> { "commlog.Note","no.SuchColumn" },maxPreviews:2);
			Assert.AreEqual(1,listResults.Count,"Unknown names are ignored.");
			Assert.AreEqual("commlog.Note",listResults[0].Label);
			Assert.IsTrue(listResults[0].CountRows>=3);
			Assert.AreEqual(2,listResults[0].ListPreviews.Count);
			DatabaseMaintenances.InvalidCharsGdiFix(new List<string> { "commlog.Note" });
			Assert.AreEqual("City\u0012",BDbmDb.GetText("patient","City","PatNum",patient.PatNum),"A column that was not chosen is left alone.");
			DatabaseMaintenances.InvalidCharsGdiFix(new List<string> { "patient.City" });
			Assert.AreEqual("City",BDbmDb.GetText("patient","City","PatNum",patient.PatNum));
		}

		[TestMethod]
		public void Fix_WorksAcrossBatches() {
			Patient patient=BDbmDb.CreatePatient("GdiBatch");
			List<long> listCommlogNums=new List<long>();
			for(int i=0;i<520;i++) {//More than one batch of 500.
				Commlog commlog=CommlogT.CreateCommlog(patient.PatNum,text:"x");
				BDbmDb.SetText("commlog","Note","CommlogNum",commlog.CommlogNum,"Batch "+i+"\u0014");
				listCommlogNums.Add(commlog.CommlogNum);
			}
			Assert.IsTrue(DatabaseMaintenances.InvalidCharsGdiScan(new List<string> { "commlog.Note" })[0].CountRows>=520);
			List<DbmInvalidCharResult> listFixed=DatabaseMaintenances.InvalidCharsGdiFix(new List<string> { "commlog.Note" });
			Assert.IsTrue(listFixed[0].CountRowsFixed>=520);
			Assert.AreEqual("Batch 0",BDbmDb.GetText("commlog","Note","CommlogNum",listCommlogNums[0]));
			Assert.AreEqual("Batch 519",BDbmDb.GetText("commlog","Note","CommlogNum",listCommlogNums[519]));
			Assert.AreEqual(0,DatabaseMaintenances.InvalidCharsGdiScan(new List<string> { "commlog.Note" })[0].CountRows);
		}
	}
}
