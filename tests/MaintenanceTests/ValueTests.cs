// Result-field validation and limits (docs/specs/routine-maintenance.md, "Result fields and validation"). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class ValueTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		private static MaintField Number(bool hasMin,double min,bool hasMax,double max) {
			return new MaintField { MaintFieldNum=5,FieldName="Chamber temperature",FieldType=EnumMaintFieldType.Number,Unit="°C",HasMin=hasMin,ValueMin=min,
				HasMax=hasMax,ValueMax=max };
		}

		[TestMethod]
		public void Limits_AreInclusiveAndEitherMayBeMissing() {
			MaintField maintField=Number(true,2,true,8);
			Assert.IsFalse(MaintValues.IsOutOfRange(maintField,2));
			Assert.IsFalse(MaintValues.IsOutOfRange(maintField,8));
			Assert.IsFalse(MaintValues.IsOutOfRange(maintField,5.5));
			Assert.IsTrue(MaintValues.IsOutOfRange(maintField,1.99));
			Assert.IsTrue(MaintValues.IsOutOfRange(maintField,8.01));
			MaintField maintFieldMinOnly=Number(true,121,false,0);
			Assert.IsFalse(MaintValues.IsOutOfRange(maintFieldMinOnly,1000));
			Assert.IsTrue(MaintValues.IsOutOfRange(maintFieldMinOnly,120));
			MaintField maintFieldMaxOnly=Number(false,0,true,500);
			Assert.IsFalse(MaintValues.IsOutOfRange(maintFieldMaxOnly,-5));
			Assert.IsTrue(MaintValues.IsOutOfRange(maintFieldMaxOnly,501));
			Assert.IsFalse(MaintValues.IsOutOfRange(Number(false,0,false,0),1e9),"No limits: nothing is out of range.");
		}

		[TestMethod]
		public void Flagging_FailAndOutOfRange() {
			MaintField maintFieldPassFail=new MaintField { FieldName="Spore test",FieldType=EnumMaintFieldType.PassFail };
			Assert.IsTrue(MaintValues.IsFlagged(maintFieldPassFail,MaintValues.VALUE_FALSE));
			Assert.IsFalse(MaintValues.IsFlagged(maintFieldPassFail,MaintValues.VALUE_TRUE));
			Assert.IsFalse(MaintValues.IsFlagged(maintFieldPassFail,""));
			MaintField maintFieldNumber=Number(true,2,true,8);
			Assert.IsTrue(MaintValues.IsFlagged(maintFieldNumber,"9"));
			Assert.IsFalse(MaintValues.IsFlagged(maintFieldNumber,"4.5"));
			MaintRecordValue maintRecordValue=MaintValues.CreateFieldValue(maintFieldNumber,"9.5");
			Assert.IsTrue(maintRecordValue.IsFlagged);
			Assert.AreEqual(EnumMaintFieldType.Number,maintRecordValue.FieldType);
			Assert.AreEqual("Chamber temperature",maintRecordValue.Label);
			Assert.AreEqual("°C",maintRecordValue.Unit);
			Assert.AreEqual(5,maintRecordValue.MaintFieldNum);
			Assert.AreEqual(0,maintRecordValue.MaintStepNum);
			Assert.AreEqual("",MaintValues.CreateFieldValue(new MaintField { FieldName="Note",FieldType=EnumMaintFieldType.Text,Unit="x" },"ok").Unit,
				"Only numbers keep a unit.");
		}

		[TestMethod]
		public void RequiredAndInvalidValues() {
			MaintField maintField=Number(true,2,true,8);
			Assert.IsNull(MaintValues.GetValueError(maintField,""),"An optional field may be left blank.");
			maintField.IsRequired=true;
			Assert.IsNotNull(MaintValues.GetValueError(maintField,""));
			Assert.IsNotNull(MaintValues.GetValueError(maintField,"  "));
			Assert.IsNull(MaintValues.GetValueError(maintField,"20"),"Out of range is allowed (flagged), not an error.");
			Assert.IsNotNull(MaintValues.GetValueError(maintField,"warm"));
			MaintField maintFieldDate=new MaintField { FieldName="Next service",FieldType=EnumMaintFieldType.Date,IsRequired=true };
			Assert.IsNull(MaintValues.GetValueError(maintFieldDate,"2027-05-01"));
			Assert.IsNotNull(MaintValues.GetValueError(maintFieldDate,"05/01/2027"),"Dates are stored as yyyy-MM-dd.");
			MaintField maintFieldPassFail=new MaintField { FieldName="Spore test",FieldType=EnumMaintFieldType.PassFail,IsRequired=true };
			Assert.IsNull(MaintValues.GetValueError(maintFieldPassFail,"0"),"Fail is a value.");
			Assert.IsNotNull(MaintValues.GetValueError(maintFieldPassFail,"2"));
			Assert.IsNotNull(MaintValues.GetValueError(maintFieldPassFail,""));
			MaintField maintFieldAttachment=new MaintField { FieldName="Report",FieldType=EnumMaintFieldType.Attachment,IsRequired=true };
			Assert.IsNotNull(MaintValues.GetValueError(maintFieldAttachment,""));
			Assert.IsNull(MaintValues.GetValueError(maintFieldAttachment,"20261102-101500_report.pdf"));
		}

		[TestMethod]
		public void Numbers_TypedInTheUsersCultureAreStoredInvariant() {
			Assert.IsTrue(MaintValues.TryParseNumber("4.5",out double value));
			Assert.AreEqual(4.5,value);
			Assert.AreEqual("4.5",MaintValues.NumberToDb(value));
			Assert.IsFalse(MaintValues.TryParseNumber("",out value));
			Assert.IsFalse(MaintValues.TryParseNumber("abc",out value));
			Assert.IsFalse(MaintValues.TryParseNumber("NaN",out value));
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
				Assert.IsTrue(MaintValues.TryParseNumber("4,5",out value));
				Assert.AreEqual(4.5,value);
				Assert.AreEqual("4.5",MaintValues.NumberToDb(value),"Stored the same whatever the culture.");
				Assert.AreEqual("4,5",MaintValues.FormatNumber(value));
				Assert.IsTrue(MaintValues.TryNumberFromDb("4.5",out double valueDb));
				Assert.AreEqual(4.5,valueDb);
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
			Assert.AreEqual(0.1+0.2,double.Parse(MaintValues.NumberToDb(0.1+0.2),CultureInfo.InvariantCulture),"Round trip keeps every digit.");
		}

		[TestMethod]
		public void Dates_StoredAsIsoDates() {
			Assert.AreEqual("2028-02-29",MaintValues.DateToDb(new DateTime(2028,2,29)));
			Assert.AreEqual(new DateTime(2028,2,29),MaintValues.DateFromDb("2028-02-29"));
			Assert.AreEqual(DateTime.MinValue,MaintValues.DateFromDb("2027-02-29"));
			Assert.AreEqual(DateTime.MinValue,MaintValues.DateFromDb(null));
		}

		[TestMethod]
		public void FieldSetupErrors() {
			Assert.IsNotNull(MaintValues.GetFieldError(new MaintField { FieldName="",FieldType=EnumMaintFieldType.Text }));
			Assert.IsNotNull(MaintValues.GetFieldError(new MaintField { FieldName="Step",FieldType=EnumMaintFieldType.ChecklistStep }),
				"A checklist step is never a field type.");
			Assert.IsNotNull(MaintValues.GetFieldError(Number(true,9,true,2)),"The minimum must not be above the maximum.");
			Assert.IsNull(MaintValues.GetFieldError(Number(true,2,true,2)));
			Assert.IsNull(MaintValues.GetFieldError(Number(true,9,false,2)),"Only one limit set: no conflict.");
			CollectionAssert.DoesNotContain(MaintValues.GetFieldTypesForSetup(),EnumMaintFieldType.ChecklistStep);
			MaintField maintFieldText=new MaintField { FieldName="Note",FieldType=EnumMaintFieldType.Text,Unit="kg",HasMin=true,ValueMin=1,HasMax=true,ValueMax=2 };
			MaintValues.ClearUnusedSettings(maintFieldText);
			Assert.AreEqual("",maintFieldText.Unit);
			Assert.IsFalse(maintFieldText.HasMin);
			Assert.IsFalse(maintFieldText.HasMax);
		}

		[TestMethod]
		public void LimitsAndDisplayText() {
			Assert.AreEqual("2 to 8 °C",MaintValues.GetLimitsText(Number(true,2,true,8)));
			Assert.AreEqual("at least 121 °C",MaintValues.GetLimitsText(Number(true,121,false,0)));
			Assert.AreEqual("at most 0.5 °C",MaintValues.GetLimitsText(Number(false,0,true,0.5)));
			Assert.AreEqual("",MaintValues.GetLimitsText(Number(false,0,false,0)));
			Assert.AreEqual("4.5 °C",MaintValues.GetDisplayText(EnumMaintFieldType.Number,"4.5","°C"));
			Assert.AreEqual("Pass",MaintValues.GetDisplayText(EnumMaintFieldType.PassFail,"1",""));
			Assert.AreEqual("Fail",MaintValues.GetDisplayText(EnumMaintFieldType.PassFail,"0",""));
			Assert.AreEqual("Done",MaintValues.GetDisplayText(EnumMaintFieldType.ChecklistStep,"1",""));
			Assert.AreEqual("Not done",MaintValues.GetDisplayText(EnumMaintFieldType.ChecklistStep,"0",""));
			Assert.AreEqual("5/1/2027",MaintValues.GetDisplayText(EnumMaintFieldType.Date,"2027-05-01",""));
			Assert.AreEqual("",MaintValues.GetDisplayText(EnumMaintFieldType.Text,"",""));
			MaintRecordValue maintRecordValue=MaintValues.CreateStepValue(new MaintStep { MaintStepNum=8,Description="Drain the reservoir" },true);
			Assert.AreEqual(EnumMaintFieldType.ChecklistStep,maintRecordValue.FieldType);
			Assert.AreEqual("1",maintRecordValue.ValueText);
			Assert.AreEqual(8,maintRecordValue.MaintStepNum);
			Assert.IsFalse(maintRecordValue.IsFlagged,"An unticked step is not a failed result.");
			Assert.AreEqual("0",MaintValues.CreateStepValue(new MaintStep { Description="x" },false).ValueText);
		}

		[TestMethod]
		public void ChecklistText_ForTheHistoryAndItsPrintout() {
			MaintField maintFieldTemp=Number(true,121,false,0);
			List<MaintRecordValue> listValues=new List<MaintRecordValue> {
				MaintValues.CreateStepValue(new MaintStep { Description="Drain" },true),
				MaintValues.CreateStepValue(new MaintStep { Description="Shock" },true),
				MaintValues.CreateFieldValue(maintFieldTemp,"134"),
				MaintValues.CreateStepValue(new MaintStep { Description="Refill" },true),
			};
			Assert.AreEqual("3 of 3",MaintValues.GetChecklistText(listValues),"Result fields are not steps.");
			Assert.IsFalse(MaintValues.HasStepNotDone(listValues));
			listValues[0].ValueText=MaintValues.VALUE_FALSE;
			listValues[3].ValueText=MaintValues.VALUE_FALSE;
			Assert.AreEqual("1 of 3, not done: Drain, Refill",MaintValues.GetChecklistText(listValues));
			Assert.IsTrue(MaintValues.HasStepNotDone(listValues));
			//Every step skipped (after the confirmation when recording): not flagged, but the history says so.
			listValues[1].ValueText=MaintValues.VALUE_FALSE;
			Assert.AreEqual("0 of 3, not done: Drain, Shock, Refill",MaintValues.GetChecklistText(listValues));
			Assert.IsFalse(listValues.Any(x => x.IsFlagged));
			//An item without steps: blank.
			Assert.AreEqual("",MaintValues.GetChecklistText(new List<MaintRecordValue> { MaintValues.CreateFieldValue(maintFieldTemp,"119") }));
			Assert.IsFalse(MaintValues.HasStepNotDone(new List<MaintRecordValue> { MaintValues.CreateFieldValue(maintFieldTemp,"119") }));
			Assert.AreEqual("",MaintValues.GetChecklistText(new List<MaintRecordValue>()));
			Assert.AreEqual("",MaintValues.GetChecklistText(null));
		}

		[TestMethod]
		public void AttachmentFileNames() {
			DateTime dateTime=new DateTime(2026,11,2,10,15,0);
			Assert.AreEqual("20261102-101500_spore report.pdf",MaintAttachments.MakeFileName(@"C:\scans\spore report.pdf",dateTime));
			Assert.AreEqual("20261102-101500_attachment",MaintAttachments.MakeFileName("",dateTime));
			string longName=new string('a',300)+".pdf";
			string fileName=MaintAttachments.MakeFileName(longName,dateTime);
			Assert.IsTrue(fileName.EndsWith(".pdf"));
			Assert.IsTrue(fileName.Length<=16+150,fileName.Length.ToString());
			Assert.AreEqual("20261102-101500_a_b.txt",MaintAttachments.MakeFileName("a|b.txt",dateTime),"Characters a file name cannot hold are replaced.");
		}
	}
}
