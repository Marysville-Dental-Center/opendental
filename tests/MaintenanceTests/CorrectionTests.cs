// Corrections of recorded completions (docs/specs/routine-maintenance.md, "Correcting a completion"; owner decision of 2026-11-02): who may
// correct one, what a correction changes, the audit trail text and the history's marker. The plan without a database, then MaintRecords.Correct
// on the scratch database (synthetic rows only).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class CorrectionTests {
		private static readonly DateTime _dateToday=new DateTime(2026,11,2);

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		#region Without a database
		private static MaintField FieldTemperature() {
			return new MaintField { MaintFieldNum=51,FieldName="Temperature",FieldType=EnumMaintFieldType.Number,Unit="C",HasMin=true,ValueMin=121,
				HasMax=true,ValueMax=140 };
		}

		private static MaintField FieldSpore() {
			return new MaintField { MaintFieldNum=52,FieldName="Spore test",FieldType=EnumMaintFieldType.PassFail,IsRequired=true };
		}

		private static MaintField FieldPhoto() {
			return new MaintField { MaintFieldNum=53,FieldName="Printout",FieldType=EnumMaintFieldType.Attachment };
		}

		///<summary>A recorded completion (MaintRecordNum 7) with a checklist line and three results, each with its MaintRecordValueNum.</summary>
		private static List<MaintRecordValue> StoredValues() {
			List<MaintRecordValue> listValues=new List<MaintRecordValue> {
				MaintValues.CreateStepValue(new MaintStep { MaintStepNum=41,Description="Load the test vial" },false),
				MaintValues.CreateFieldValue(FieldTemperature(),"119.5"),
				MaintValues.CreateFieldValue(FieldSpore(),MaintValues.VALUE_FALSE),
				MaintValues.CreateFieldValue(FieldPhoto(),"20261102-090000_old.pdf"),
			};
			for(int i=0;i<listValues.Count;i++) {
				listValues[i].MaintRecordValueNum=100+i;
				listValues[i].MaintRecordNum=7;
				listValues[i].ItemOrder=i;
			}
			return listValues;
		}

		private static MaintRecord StoredRecord() {
			return new MaintRecord { MaintRecordNum=7,MaintItemNum=3,DateDone=_dateToday,DateDue=_dateToday.AddDays(-1),UserNum=TestDb.USER_ASSISTANT_A,
				Note="",IsFlagged=true };
		}

		private static List<MaintRecordValue> WithValue(List<MaintRecordValue> listValues,long maintRecordValueNum,string valueText) {
			List<MaintRecordValue> listCopies=listValues.Select(x => x.Copy()).ToList();
			listCopies.Single(x => x.MaintRecordValueNum==maintRecordValueNum).ValueText=valueText;
			return listCopies;
		}

		[TestMethod]
		public void CanCorrect_TheRecorderOrAMaintenanceSetupUser() {
			MaintRecord maintRecord=StoredRecord();
			Userod userodRecorder=new Userod { UserNum=TestDb.USER_ASSISTANT_A,UserName="Assistant A" };
			Userod userodOther=new Userod { UserNum=TestDb.USER_ASSISTANT_B,UserName="Assistant B" };
			Assert.IsTrue(MaintCorrections.CanCorrect(userodRecorder,maintRecord,false),"The user who recorded it.");
			Assert.IsFalse(MaintCorrections.CanCorrect(userodOther,maintRecord,false),"Anybody else without Maintenance Setup.");
			Assert.IsTrue(MaintCorrections.CanCorrect(userodOther,maintRecord,true),"Any user with Maintenance Setup.");
			Assert.IsFalse(MaintCorrections.CanCorrect(null,maintRecord,true));
			Assert.IsFalse(MaintCorrections.CanCorrect(userodRecorder,null,true));
			Assert.IsFalse(MaintCorrections.CanCorrect(new Userod { UserNum=0 },new MaintRecord { UserNum=0 },false),"No recorder: only Maintenance Setup.");
		}

		[TestMethod]
		public void LogText_IsTheSameOnEveryWorkstation() {
			Assert.AreEqual("done",MaintCorrections.GetLogText(EnumMaintFieldType.ChecklistStep,"1",""));
			Assert.AreEqual("not done",MaintCorrections.GetLogText(EnumMaintFieldType.ChecklistStep,"0",""));
			Assert.AreEqual("pass",MaintCorrections.GetLogText(EnumMaintFieldType.PassFail,"1",""));
			Assert.AreEqual("fail",MaintCorrections.GetLogText(EnumMaintFieldType.PassFail,"0",""));
			Assert.AreEqual("(blank)",MaintCorrections.GetLogText(EnumMaintFieldType.Text,"",""));
			Assert.AreEqual("2026-11-02",MaintCorrections.GetLogText(EnumMaintFieldType.Date,"2026-11-02",""));
			Assert.AreEqual("O'Brien",MaintCorrections.GetLogText(EnumMaintFieldType.Text,"O'Brien",""));
			TestDb.RunInCulture("de-DE",() => {
				Assert.AreEqual("121.5 C",MaintCorrections.GetLogText(EnumMaintFieldType.Number,"121.5","C"),"Invariant culture, whatever the workstation's.");
			});
		}

		[TestMethod]
		public void Plan_EveryKindOfChange_WithOldAndNewValues() {
			List<MaintRecordValue> listStored=StoredValues();
			List<MaintRecordValue> listNew=listStored.Select(x => x.Copy()).ToList();
			listNew[0].ValueText=MaintValues.VALUE_TRUE;
			listNew[1].ValueText="134";
			listNew[2].ValueText=MaintValues.VALUE_TRUE;
			listNew[3].ValueText="20261103-101500_new.pdf";
			List<MaintField> listFields=new List<MaintField> { FieldTemperature(),FieldSpore(),FieldPhoto() };
			MaintCorrections.CorrectionPlan plan=MaintCorrections.GetPlan(StoredRecord(),listStored,"Re-read the gauge",listNew,listFields);
			Assert.IsNull(plan.Error);
			Assert.IsTrue(plan.IsChanging);
			CollectionAssert.AreEqual(new[] {
					"Load the test vial: 'not done' to 'done'",
					"Temperature: '119.5 C' to '134 C'",
					"Spore test: 'fail' to 'pass'",
					"Printout: attachment removed 20261102-090000_old.pdf, attachment added 20261103-101500_new.pdf",
					"note: '(blank)' to 'Re-read the gauge'",
				},plan.ListChanges);
			Assert.AreEqual(4,plan.ListValuesChanged.Count);
			Assert.IsTrue(plan.ListValuesChanged.All(x => !x.IsFlagged),"134 is within 121 to 140 and Pass is not a failure.");
			Assert.IsFalse(plan.MaintRecord.IsFlagged);
			Assert.IsTrue(plan.IsFlagChanged(StoredRecord()));
			CollectionAssert.AreEqual(new long[] { 100,101,102,103 },plan.ListValuesOld.Select(x => x.MaintRecordValueNum).ToList());
			Assert.AreEqual("119.5",plan.ListValuesOld[1].ValueText,"The old lines as stored.");
			Assert.AreEqual("Re-read the gauge",plan.MaintRecord.Note);
			Assert.AreEqual("Maintenance completion corrected: Autoclave, done 2026-11-02 (MaintRecordNum 7): Load the test vial: 'not done' to 'done'; "
				+"Temperature: '119.5 C' to '134 C'; Spore test: 'fail' to 'pass'; Printout: attachment removed 20261102-090000_old.pdf, attachment added "
				+"20261103-101500_new.pdf; note: '(blank)' to 'Re-read the gauge', no longer flagged",
				MaintAudit.GetTextCorrected("Autoclave",StoredRecord(),plan));
		}

		[TestMethod]
		public void Plan_AttachmentRemovedOrAdded_AndTheLabelsComeFromTheRecord() {
			List<MaintRecordValue> listStored=StoredValues();
			MaintCorrections.CorrectionPlan planRemoved=MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,103,""),new List<MaintField>());
			CollectionAssert.AreEqual(new[] { "Printout: attachment removed 20261102-090000_old.pdf" },planRemoved.ListChanges);
			listStored[3].ValueText="";
			List<MaintRecordValue> listNew=WithValue(listStored,103,"20261103-101500_new.pdf");
			listNew[3].Label="Renamed by the caller";//Only ValueText is taken from the caller.
			listNew[3].IsFlagged=true;
			MaintCorrections.CorrectionPlan planAdded=MaintCorrections.GetPlan(StoredRecord(),listStored,"",listNew,new List<MaintField>());
			CollectionAssert.AreEqual(new[] { "Printout: attachment added 20261103-101500_new.pdf" },planAdded.ListChanges);
			Assert.AreEqual("Printout",planAdded.ListValuesChanged.Single().Label);
			Assert.IsFalse(planAdded.ListValuesChanged.Single().IsFlagged,"The flag is worked out, not taken from the caller.");
		}

		[TestMethod]
		public void Plan_Flags_UnchangedLinesKeepTheirs_ChangedLinesAgainstTheCurrentField() {
			List<MaintRecordValue> listStored=StoredValues();
			Assert.IsTrue(listStored[1].IsFlagged,"119.5 is below 121.");
			Assert.IsTrue(listStored[2].IsFlagged,"Fail.");
			//Only the note changes: every line keeps its flag, so the completion stays flagged.
			MaintCorrections.CorrectionPlan planNote=MaintCorrections.GetPlan(StoredRecord(),listStored,"Note only",listStored.Select(x => x.Copy()).ToList(),
				new List<MaintField>());
			Assert.AreEqual(0,planNote.ListValuesChanged.Count);
			Assert.IsTrue(planNote.MaintRecord.IsFlagged);
			Assert.IsFalse(planNote.IsFlagChanged(StoredRecord()));
			Assert.AreEqual("Maintenance completion corrected: Autoclave, done 2026-11-02 (MaintRecordNum 7): note: '(blank)' to 'Note only'",
				MaintAudit.GetTextCorrected("Autoclave",StoredRecord(),planNote));
			//A changed number is checked against the item's current field: now out of range above the maximum.
			MaintCorrections.CorrectionPlan planHigh=MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,101,"150"),
				new List<MaintField> { FieldTemperature() });
			Assert.IsTrue(planHigh.ListValuesChanged.Single().IsFlagged);
			//The field was deleted since: its limits are unknown, so a corrected number is not flagged.
			MaintCorrections.CorrectionPlan planDeleted=MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,101,"150"),new List<MaintField>());
			Assert.IsFalse(planDeleted.ListValuesChanged.Single().IsFlagged);
			Assert.IsTrue(planDeleted.MaintRecord.IsFlagged,"The Fail line keeps its flag.");
			//The field changed its type since: not the line's field any more.
			MaintField maintFieldRetyped=FieldTemperature();
			maintFieldRetyped.FieldType=EnumMaintFieldType.Text;
			Assert.IsNull(MaintCorrections.GetCurrentField(listStored[1],new List<MaintField> { maintFieldRetyped }));
			Assert.IsNull(MaintCorrections.GetCurrentField(listStored[0],new List<MaintField> { FieldTemperature() }),"A checklist line has no field.");
			//Fail and pass: flagged by the value itself.
			Assert.IsTrue(MaintCorrections.GetFlag(WithValue(listStored,102,MaintValues.VALUE_TRUE)[2],MaintValues.VALUE_FALSE,null));
			Assert.IsFalse(MaintCorrections.GetFlag(listStored[2],MaintValues.VALUE_TRUE,null));
		}

		[TestMethod]
		public void Plan_InvalidValuesAndForeignLines_AreRefused() {
			List<MaintRecordValue> listStored=StoredValues();
			List<MaintField> listFields=new List<MaintField> { FieldTemperature(),FieldSpore(),FieldPhoto() };
			//The spore test is required: it cannot be cleared.
			MaintCorrections.CorrectionPlan planBlank=MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,102,""),listFields);
			StringAssert.Contains(planBlank.Error,"Spore test: ");
			Assert.IsNull(planBlank.MaintRecord);
			Assert.AreEqual(0,planBlank.ListChanges.Count);
			//Not a number.
			StringAssert.Contains(MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,101,"warm"),listFields).Error,"Temperature: ");
			//A checklist line is done or not done.
			StringAssert.Contains(MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,100,"2"),listFields).Error,"Load the test vial: ");
			//A line of another completion, or one given twice.
			List<MaintRecordValue> listForeign=listStored.Select(x => x.Copy()).ToList();
			listForeign.Add(new MaintRecordValue { MaintRecordValueNum=999,MaintRecordNum=8,ValueText="1" });
			Assert.IsNotNull(MaintCorrections.GetPlan(StoredRecord(),listStored,"",listForeign,listFields).Error);
			List<MaintRecordValue> listTwice=listStored.Select(x => x.Copy()).ToList();
			listTwice.Add(listStored[1].Copy());
			Assert.IsNotNull(MaintCorrections.GetPlan(StoredRecord(),listStored,"",listTwice,listFields).Error);
			//A field made required since never blocks correcting another line: unchanged lines are not checked again.
			List<MaintRecordValue> listBlankPhoto=WithValue(listStored,103,"");
			for(int i=0;i<listStored.Count;i++) {
				listStored[i].ValueText=listBlankPhoto[i].ValueText;
			}
			MaintField maintFieldPhotoRequired=FieldPhoto();
			maintFieldPhotoRequired.IsRequired=true;
			MaintCorrections.CorrectionPlan planOther=MaintCorrections.GetPlan(StoredRecord(),listStored,"",WithValue(listStored,101,"130"),
				new List<MaintField> { FieldTemperature(),FieldSpore(),maintFieldPhotoRequired });
			Assert.IsNull(planOther.Error);
			Assert.AreEqual(1,planOther.ListValuesChanged.Count);
			//Nothing given: nothing changes.
			Assert.IsFalse(MaintCorrections.GetPlan(StoredRecord(),listStored,"",null,listFields).IsChanging);
		}

		[TestMethod]
		public void CorrectedText_ForTheHistory() {
			Assert.AreEqual("",MaintCorrections.GetCorrectedText(StoredRecord(),"Admin"),"Never corrected.");
			Assert.IsFalse(MaintCorrections.IsCorrected(StoredRecord()));
			MaintRecord maintRecord=StoredRecord();
			maintRecord.UserNumCorrected=TestDb.USER_FRONT;
			maintRecord.DateTCorrected=new DateTime(2026,11,3,10,15,0);
			Assert.IsTrue(MaintCorrections.IsCorrected(maintRecord));
			//The date and who: the time is in the audit trail, so that the column leaves room for Results (MaintHistoryGrid).
			Assert.AreEqual("11/3/2026 by Front Desk",MaintCorrections.GetCorrectedText(maintRecord,"Front Desk"));
		}

		[TestMethod]
		public void Completions_AreNeverDeleted() {
			foreach(Type type in new[] { typeof(MaintRecords),typeof(MaintRecordValues) }) {
				List<string> listDeletes=type.GetMethods(BindingFlags.Public|BindingFlags.Static).Select(x => x.Name)
					.Where(x => x.StartsWith("Delete",StringComparison.OrdinalIgnoreCase)).ToList();
				Assert.AreEqual(0,listDeletes.Count,type.Name+" has no delete: "+string.Join(", ",listDeletes));
			}
		}
		#endregion Without a database

		#region MaintRecords.Correct on the scratch database
		///<summary>An item with a checklist step and two result fields, and a completion Assistant A recorded with the step not done and a low
		///temperature (flagged).</summary>
		private static MaintRecord InsertRecorded(out MaintItem maintItem) {
			TestDb.ResetOrSkip();
			maintItem=CrudDbTests.InsertItem("Autoclave",_dateToday.AddDays(-1),userNum:TestDb.USER_ASSISTANT_A);
			MaintSteps.Sync(maintItem.MaintItemNum,new List<MaintStep> { new MaintStep { Description="Load the test vial" } });
			MaintFields.Sync(maintItem.MaintItemNum,new List<MaintField> {
				new MaintField { FieldName="Temperature",FieldType=EnumMaintFieldType.Number,Unit="C",HasMin=true,ValueMin=121 },
				new MaintField { FieldName="Operator note",FieldType=EnumMaintFieldType.Text,Unit="" },
			});
			MaintStep maintStep=MaintSteps.GetForItem(maintItem.MaintItemNum).Single();
			List<MaintField> listFields=MaintFields.GetForItem(maintItem.MaintItemNum);
			Security.CurUser=Userods.GetUser(TestDb.USER_ASSISTANT_A);
			MaintRecord maintRecord=new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=_dateToday,DateDue=_dateToday.AddDays(-1),Note="First note" };
			MaintRecords.Insert(maintRecord,new List<MaintRecordValue> {
				MaintValues.CreateStepValue(maintStep,false),
				MaintValues.CreateFieldValue(listFields[0],"119.5"),
				MaintValues.CreateFieldValue(listFields[1],""),
			});
			Security.CurUser=Userods.GetUser(TestDb.USER_ADMIN);
			return MaintRecords.GetOne(maintRecord.MaintRecordNum);
		}

		private static List<MaintRecordValue> Corrected(long maintRecordNum,string temperature,bool isStepDone) {
			List<MaintRecordValue> listValues=MaintRecordValues.GetForRecord(maintRecordNum);
			listValues[0].ValueText=isStepDone ? MaintValues.VALUE_TRUE : MaintValues.VALUE_FALSE;
			listValues[1].ValueText=temperature;
			return listValues;
		}

		private static DataTable SecurityLog() {
			return DataCore.GetTable("SELECT PermType,FKey,LogText,UserNum FROM securitylog ORDER BY SecurityLogNum");
		}

		[TestMethod]
		public void Correct_ByTheUserWhoRecordedIt_SavesTheChangesWhoAndWhen_AndLogsThem() {
			MaintRecord maintRecordBefore=InsertRecorded(out MaintItem maintItem);
			List<MaintRecordValue> listBefore=MaintRecordValues.GetForRecord(maintRecordBefore.MaintRecordNum);
			Assert.IsTrue(maintRecordBefore.IsFlagged);
			Assert.AreEqual("",MaintCorrections.GetCorrectedText(maintRecordBefore,""),"Not corrected yet.");
			Security.CurUser=Userods.GetUser(TestDb.USER_ASSISTANT_A);
			Assert.IsTrue(MaintRecords.CanCorrect(Security.CurUser,maintRecordBefore));
			string logText=MaintRecords.Correct(maintRecordBefore.MaintRecordNum,"Gauge re-read",Corrected(maintRecordBefore.MaintRecordNum,"121.5",true));
			Assert.AreEqual("Maintenance completion corrected: Autoclave, done 2026-11-02 (MaintRecordNum "+maintRecordBefore.MaintRecordNum+"): "
				+"Load the test vial: 'not done' to 'done'; Temperature: '119.5 C' to '121.5 C'; note: 'First note' to 'Gauge re-read', no longer flagged",logText);
			MaintRecord maintRecordAfter=MaintRecords.GetOne(maintRecordBefore.MaintRecordNum);
			Assert.AreEqual("Gauge re-read",maintRecordAfter.Note);
			Assert.IsFalse(maintRecordAfter.IsFlagged,"Worked out again from the lines.");
			Assert.AreEqual(TestDb.USER_ASSISTANT_A,maintRecordAfter.UserNumCorrected);
			Assert.IsTrue(maintRecordAfter.DateTCorrected.Year>2000,"The server's time.");
			Assert.IsTrue(MaintCorrections.IsCorrected(maintRecordAfter));
			//The item, the dates, who did it and when it was recorded never change.
			Assert.AreEqual(maintRecordBefore.MaintItemNum,maintRecordAfter.MaintItemNum);
			Assert.AreEqual(maintRecordBefore.DateDone,maintRecordAfter.DateDone);
			Assert.AreEqual(maintRecordBefore.DateDue,maintRecordAfter.DateDue);
			Assert.AreEqual(TestDb.USER_ASSISTANT_A,maintRecordAfter.UserNum);
			Assert.AreEqual(maintRecordBefore.SecDateTEntry,maintRecordAfter.SecDateTEntry);
			//The same lines, with the new values; nothing added or removed.
			List<MaintRecordValue> listAfter=MaintRecordValues.GetForRecord(maintRecordBefore.MaintRecordNum);
			CollectionAssert.AreEqual(listBefore.Select(x => x.MaintRecordValueNum).ToList(),listAfter.Select(x => x.MaintRecordValueNum).ToList());
			CollectionAssert.AreEqual(new[] { "1","121.5","" },listAfter.Select(x => x.ValueText).ToList());
			Assert.IsFalse(listAfter[1].IsFlagged);
			CollectionAssert.AreEqual(listBefore.Select(x => x.Label+"|"+x.Unit+"|"+x.FieldType).ToList(),listAfter.Select(x => x.Label+"|"+x.Unit+"|"+x.FieldType).ToList());
			//One audit trail entry, under Maintenance Record Completion, for the item, by the user.
			DataTable table=SecurityLog();
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("10001",table.Rows[0]["PermType"].ToString());
			Assert.AreEqual(maintItem.MaintItemNum.ToString(),table.Rows[0]["FKey"].ToString());
			Assert.AreEqual(logText,table.Rows[0]["LogText"].ToString());
			Assert.AreEqual(TestDb.USER_ASSISTANT_A.ToString(),table.Rows[0]["UserNum"].ToString());
			//The history's marker: when and by whom.
			MaintRecord maintRecordHistory=MaintRecords.GetForDateRange(_dateToday,_dateToday).Single();
			Assert.AreEqual(maintRecordAfter.DateTCorrected.ToShortDateString()+" by Assistant A",
				MaintCorrections.GetCorrectedText(maintRecordHistory,Userods.GetName(maintRecordHistory.UserNumCorrected)));
		}

		[TestMethod]
		public void Correct_ByAMaintenanceSetupUser_IsAllowed_AndNamesThatUser() {
			MaintRecord maintRecord=InsertRecorded(out MaintItem maintItem);
			TestDb.Grant(TestDb.GROUP_FRONT,EnumPermType.MaintenanceSetup);
			Security.CurUser=Userods.GetUser(TestDb.USER_FRONT);
			Assert.IsTrue(MaintRecords.CanCorrect(Security.CurUser,maintRecord));
			MaintRecords.Correct(maintRecord.MaintRecordNum,maintRecord.Note,Corrected(maintRecord.MaintRecordNum,"125",false));
			MaintRecord maintRecordAfter=MaintRecords.GetOne(maintRecord.MaintRecordNum);
			Assert.AreEqual(TestDb.USER_FRONT,maintRecordAfter.UserNumCorrected);
			Assert.AreEqual(TestDb.USER_ASSISTANT_A,maintRecordAfter.UserNum,"Who did it stays.");
			Assert.AreEqual(TestDb.USER_FRONT.ToString(),SecurityLog().Rows[0]["UserNum"].ToString());
			Assert.AreEqual("Maintenance completion corrected: Autoclave, done 2026-11-02 (MaintRecordNum "+maintRecord.MaintRecordNum+"): Temperature: '119.5 C' "
				+"to '125 C', no longer flagged",SecurityLog().Rows[0]["LogText"].ToString());
			StringAssert.EndsWith(MaintCorrections.GetCorrectedText(maintRecordAfter,Userods.GetName(maintRecordAfter.UserNumCorrected))," by Front Desk");
			//A second correction: the record names the latest one; the audit trail keeps both.
			Security.CurUser=Userods.GetUser(TestDb.USER_ASSISTANT_A);
			MaintRecords.Correct(maintRecord.MaintRecordNum,"Second",Corrected(maintRecord.MaintRecordNum,"125",false));
			Assert.AreEqual(TestDb.USER_ASSISTANT_A,MaintRecords.GetOne(maintRecord.MaintRecordNum).UserNumCorrected);
			Assert.AreEqual(2,SecurityLog().Rows.Count);
		}

		[TestMethod]
		public void Correct_ByAnyoneElse_IsRefused_AndChangesNothing() {
			MaintRecord maintRecord=InsertRecorded(out MaintItem maintItem);
			//Maintenance Record Completion and Security Admin are not enough to correct someone else's completion.
			TestDb.Grant(TestDb.GROUP_STERILIZATION,EnumPermType.MaintenanceRecord);
			TestDb.Grant(TestDb.GROUP_STERILIZATION,EnumPermType.SecurityAdmin);
			foreach(long userNum in new[] { TestDb.USER_ASSISTANT_B,TestDb.USER_FRONT,TestDb.USER_ADMIN }) {
				Security.CurUser=Userods.GetUser(userNum);
				Assert.IsFalse(MaintRecords.CanCorrect(Security.CurUser,maintRecord),"User "+userNum);
				bool isThrown=false;
				try {
					MaintRecords.Correct(maintRecord.MaintRecordNum,"Changed",Corrected(maintRecord.MaintRecordNum,"130",true));
				}
				catch(ApplicationException ex) {
					isThrown=true;
					StringAssert.Contains(ex.Message,"Only the user who recorded this completion or a user with Maintenance Setup can correct it.");
				}
				Assert.IsTrue(isThrown,"User "+userNum+" is refused.");
			}
			MaintRecord maintRecordAfter=MaintRecords.GetOne(maintRecord.MaintRecordNum);
			Assert.AreEqual("First note",maintRecordAfter.Note);
			Assert.AreEqual(0,maintRecordAfter.UserNumCorrected);
			Assert.IsTrue(maintRecordAfter.DateTCorrected.Year<1880);
			CollectionAssert.AreEqual(new[] { "0","119.5","" },MaintRecordValues.GetForRecord(maintRecord.MaintRecordNum).Select(x => x.ValueText).ToList());
			Assert.AreEqual(0,SecurityLog().Rows.Count);
			Assert.IsFalse(MaintRecords.CanCorrect(null,maintRecord));
		}

		[TestMethod]
		public void Correct_NothingChanged_SavesAndLogsNothing_AndBadInputIsRefused() {
			MaintRecord maintRecord=InsertRecorded(out MaintItem maintItem);
			Security.CurUser=Userods.GetUser(TestDb.USER_ASSISTANT_A);
			Assert.AreEqual("",MaintRecords.Correct(maintRecord.MaintRecordNum,"First note",MaintRecordValues.GetForRecord(maintRecord.MaintRecordNum)));
			Assert.AreEqual(0,MaintRecords.GetOne(maintRecord.MaintRecordNum).UserNumCorrected);
			Assert.AreEqual(0,SecurityLog().Rows.Count);
			//Not a number: refused, nothing saved.
			bool isThrown=false;
			try {
				MaintRecords.Correct(maintRecord.MaintRecordNum,"x",Corrected(maintRecord.MaintRecordNum,"warm",true));
			}
			catch(ApplicationException) {
				isThrown=true;
			}
			Assert.IsTrue(isThrown);
			Assert.AreEqual("First note",MaintRecords.GetOne(maintRecord.MaintRecordNum).Note);
			//A completion that does not exist.
			isThrown=false;
			try {
				MaintRecords.Correct(maintRecord.MaintRecordNum+1000,"x",new List<MaintRecordValue>());
			}
			catch(ApplicationException) {
				isThrown=true;
			}
			Assert.IsTrue(isThrown);
			Assert.AreEqual(0,SecurityLog().Rows.Count);
		}
		#endregion MaintRecords.Correct on the scratch database
	}
}
