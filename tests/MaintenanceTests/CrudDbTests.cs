// CRUD round trips and the data interface of routine maintenance on the scratch database (synthetic rows only).
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
	public class CrudDbTests {
		private const string TEXT_ODD="O'Brien's °C ü \\ \"quoted\"";

		[TestInitialize]
		public void TestInitialize() {
			TestDb.ResetOrSkip();
		}

		public static MaintItem InsertItem(string name,DateTime dateAnchor,long userNum=0,long userGroupNum=0,int leadDays=7) {
			MaintItem maintItem=new MaintItem { ItemName=name,Description="",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Months,DateAnchor=dateAnchor,LeadDays=leadDays,UserNumAssigned=userNum,UserGroupNumAssigned=userGroupNum };
			MaintItems.Insert(maintItem);
			return maintItem;
		}

		[TestMethod]
		public void Item_InsertSelectUpdate() {
			MaintItem maintItem=new MaintItem { ItemName="Steam sterilizer spore test",Description=TEXT_ODD+"\r\nSecond line",EquipmentNum=12,
				ScheduleType=EnumMaintScheduleType.Weekdays,IntervalCount=3,IntervalUnit=EnumMaintIntervalUnit.Weeks,
				Weekdays=MaintSchedules.GetWeekdayBit(DayOfWeek.Monday)|MaintSchedules.GetWeekdayBit(DayOfWeek.Friday),DateAnchor=new DateTime(2028,2,29),
				LeadDays=365,UserGroupNumAssigned=TestDb.GROUP_STERILIZATION,IsHidden=true };
			long maintItemNum=MaintItems.Insert(maintItem);
			Assert.IsTrue(maintItemNum>0);
			MaintItem maintItemDb=MaintItems.GetOne(maintItemNum);
			Assert.AreEqual(maintItem.ItemName,maintItemDb.ItemName);
			Assert.AreEqual(maintItem.Description,maintItemDb.Description);
			Assert.AreEqual(12,maintItemDb.EquipmentNum);
			Assert.AreEqual(EnumMaintScheduleType.Weekdays,maintItemDb.ScheduleType);
			Assert.AreEqual(3,maintItemDb.IntervalCount);
			Assert.AreEqual(EnumMaintIntervalUnit.Weeks,maintItemDb.IntervalUnit);
			Assert.AreEqual(maintItem.Weekdays,maintItemDb.Weekdays);
			Assert.AreEqual(new DateTime(2028,2,29),maintItemDb.DateAnchor);
			Assert.AreEqual(365,maintItemDb.LeadDays);
			Assert.AreEqual(0,maintItemDb.UserNumAssigned);
			Assert.AreEqual(TestDb.GROUP_STERILIZATION,maintItemDb.UserGroupNumAssigned);
			Assert.IsTrue(maintItemDb.IsHidden);
			Assert.AreEqual(TestDb.USER_ADMIN,maintItemDb.SecUserNumEntry,"The logged-on user added it.");
			Assert.IsTrue(maintItemDb.SecDateTEntry.Year>2000,"Set by the server.");
			Assert.IsTrue(maintItemDb.SecDateTEdit.Year>2000);
			//Update only the changed columns; SecUserNumEntry never changes.
			MaintItem maintItemNew=maintItemDb.Copy();
			maintItemNew.ItemName="Renamed";
			maintItemNew.IsHidden=false;
			maintItemNew.SecUserNumEntry=TestDb.USER_FRONT;
			Assert.IsTrue(MaintItems.Update(maintItemNew,maintItemDb));
			Assert.IsFalse(MaintItems.Update(maintItemNew,maintItemNew.Copy()),"Nothing changed.");
			MaintItem maintItemUpdated=MaintItems.GetOne(maintItemNum);
			Assert.AreEqual("Renamed",maintItemUpdated.ItemName);
			Assert.IsFalse(maintItemUpdated.IsHidden);
			Assert.AreEqual(TestDb.USER_ADMIN,maintItemUpdated.SecUserNumEntry,"ExcludeFromUpdate.");
			Assert.AreEqual(maintItemDb.SecDateTEntry,maintItemUpdated.SecDateTEntry);
		}

		[TestMethod]
		public void Items_GetAllLeavesHiddenOutUnlessAsked() {
			InsertItem("B visible",new DateTime(2026,11,2));
			MaintItem maintItemHidden=InsertItem("A hidden",new DateTime(2026,11,2));
			MaintItem maintItemHiddenOld=maintItemHidden.Copy();
			maintItemHidden.IsHidden=true;
			MaintItems.Update(maintItemHidden,maintItemHiddenOld);
			CollectionAssert.AreEqual(new[] { "B visible" },MaintItems.GetAll(includeHidden:false).Select(x => x.ItemName).ToList());
			CollectionAssert.AreEqual(new[] { "A hidden","B visible" },MaintItems.GetAll(includeHidden:true).Select(x => x.ItemName).ToList(),"By name.");
		}

		[TestMethod]
		public void Steps_SyncInsertsUpdatesDeletesAndOrders() {
			MaintItem maintItem=InsertItem("Waterline treatment",new DateTime(2026,11,2));
			MaintSteps.Sync(maintItem.MaintItemNum,new List<MaintStep> { new MaintStep { Description="Drain" },new MaintStep { Description="Shock" },
				new MaintStep { Description=TEXT_ODD } });
			List<MaintStep> listSteps=MaintSteps.GetForItem(maintItem.MaintItemNum);
			CollectionAssert.AreEqual(new[] { "Drain","Shock",TEXT_ODD },listSteps.Select(x => x.Description).ToList());
			CollectionAssert.AreEqual(new[] { 0,1,2 },listSteps.Select(x => x.ItemOrder).ToList());
			long stepNumShock=listSteps[1].MaintStepNum;
			//Move Shock first, edit the odd one, delete Drain, add Refill.
			List<MaintStep> listNew=new List<MaintStep> { listSteps[1],listSteps[2],new MaintStep { Description="Refill" } };
			listNew[1].Description="Flush";
			MaintSteps.Sync(maintItem.MaintItemNum,listNew);
			List<MaintStep> listAfter=MaintSteps.GetForItem(maintItem.MaintItemNum);
			CollectionAssert.AreEqual(new[] { "Shock","Flush","Refill" },listAfter.Select(x => x.Description).ToList());
			Assert.AreEqual(stepNumShock,listAfter[0].MaintStepNum,"Kept, not re-inserted.");
			Assert.AreEqual(3,TestDb.GetLong("SELECT COUNT(*) FROM maintstep"));
			MaintSteps.Sync(maintItem.MaintItemNum,new List<MaintStep>());
			Assert.AreEqual(0,MaintSteps.GetForItem(maintItem.MaintItemNum).Count);
		}

		[TestMethod]
		public void Fields_SyncKeepsLimitsWithTheirDecimals() {
			MaintItem maintItem=InsertItem("Refrigerator temperature",new DateTime(2026,11,2));
			List<MaintField> listFields=new List<MaintField> {
				new MaintField { FieldName="Temperature",FieldType=EnumMaintFieldType.Number,Unit="°C",HasMin=true,ValueMin=2.125,HasMax=true,ValueMax=7.999999,IsRequired=true },
				new MaintField { FieldName="Door seal",FieldType=EnumMaintFieldType.PassFail,Unit="" },
				new MaintField { FieldName="Log sheet",FieldType=EnumMaintFieldType.Attachment,Unit="" },
			};
			MaintFields.Sync(maintItem.MaintItemNum,listFields);
			List<MaintField> listDb=MaintFields.GetForItem(maintItem.MaintItemNum);
			CollectionAssert.AreEqual(new[] { "Temperature","Door seal","Log sheet" },listDb.Select(x => x.FieldName).ToList());
			Assert.AreEqual(2.125,listDb[0].ValueMin,1e-9);
			Assert.AreEqual(7.999999,listDb[0].ValueMax,1e-9,"Six decimals survive (CrudSpecialColType.Double).");
			Assert.IsTrue(listDb[0].HasMin && listDb[0].HasMax && listDb[0].IsRequired);
			Assert.AreEqual("°C",listDb[0].Unit);
			Assert.AreEqual(EnumMaintFieldType.Attachment,listDb[2].FieldType);
			listDb.RemoveAt(1);
			listDb[0].ValueMax=8;
			MaintFields.Sync(maintItem.MaintItemNum,listDb);
			List<MaintField> listAfter=MaintFields.GetForItem(maintItem.MaintItemNum);
			CollectionAssert.AreEqual(new[] { "Temperature","Log sheet" },listAfter.Select(x => x.FieldName).ToList());
			Assert.AreEqual(8,listAfter[0].ValueMax);
			CollectionAssert.AreEqual(new[] { 0,1 },listAfter.Select(x => x.ItemOrder).ToList());
		}

		[TestMethod]
		public void Record_InsertSetsTheUserAndFlag_AndKeepsTheValuesInOrder() {
			MaintItem maintItem=InsertItem("Autoclave",new DateTime(2026,11,1));
			MaintStep maintStep=new MaintStep { MaintStepNum=41,Description="Load the test vial" };
			MaintField maintFieldTemp=new MaintField { MaintFieldNum=51,FieldName="Temperature",FieldType=EnumMaintFieldType.Number,Unit="°C",HasMin=true,ValueMin=121 };
			MaintField maintFieldResult=new MaintField { MaintFieldNum=52,FieldName="Spore test",FieldType=EnumMaintFieldType.PassFail };
			List<MaintRecordValue> listValues=new List<MaintRecordValue> {
				MaintValues.CreateStepValue(maintStep,true),
				MaintValues.CreateFieldValue(maintFieldTemp,"119.5"),
				MaintValues.CreateFieldValue(maintFieldResult,MaintValues.VALUE_TRUE),
			};
			MaintRecord maintRecord=new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=new DateTime(2026,11,2),DateDue=new DateTime(2026,11,1),
				Note=TEXT_ODD,UserNum=TestDb.USER_FRONT };
			Security.CurUser=Userods.GetUser(TestDb.USER_ASSISTANT_A);
			long maintRecordNum=MaintRecords.Insert(maintRecord,listValues);
			MaintRecord maintRecordDb=MaintRecords.GetOne(maintRecordNum);
			Assert.AreEqual(TestDb.USER_ASSISTANT_A,maintRecordDb.UserNum,"Whoever records it did it: the logged-on user, whatever the caller set.");
			Assert.IsTrue(maintRecordDb.IsFlagged,"119.5 is below 121.");
			Assert.AreEqual(new DateTime(2026,11,2),maintRecordDb.DateDone);
			Assert.AreEqual(new DateTime(2026,11,1),maintRecordDb.DateDue);
			Assert.AreEqual(TEXT_ODD,maintRecordDb.Note);
			Assert.IsTrue(maintRecordDb.SecDateTEntry.Year>2000,"The time is the server's.");
			List<MaintRecordValue> listDb=MaintRecordValues.GetForRecord(maintRecordNum);
			CollectionAssert.AreEqual(new[] { "Load the test vial","Temperature","Spore test" },listDb.Select(x => x.Label).ToList());
			CollectionAssert.AreEqual(new[] { 0,1,2 },listDb.Select(x => x.ItemOrder).ToList());
			Assert.AreEqual(EnumMaintFieldType.ChecklistStep,listDb[0].FieldType);
			Assert.AreEqual(41,listDb[0].MaintStepNum);
			Assert.AreEqual(51,listDb[1].MaintFieldNum);
			Assert.AreEqual("119.5",listDb[1].ValueText);
			Assert.AreEqual("°C",listDb[1].Unit);
			Assert.IsTrue(listDb[1].IsFlagged);
			Assert.IsFalse(listDb[2].IsFlagged);
			Assert.AreEqual(3,MaintRecordValues.GetForRecords(new List<long> { maintRecordNum,999 }).Count);
			Assert.AreEqual(0,MaintRecordValues.GetForRecords(new List<long>()).Count);
			//A completion without a flagged value is not flagged.
			long maintRecordNumClean=MaintRecords.Insert(new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=new DateTime(2026,11,3),Note="" },
				new List<MaintRecordValue> { MaintValues.CreateFieldValue(maintFieldTemp,"134") });
			Assert.IsFalse(MaintRecords.GetOne(maintRecordNumClean).IsFlagged);
		}

		[TestMethod]
		public void Records_ByItemAndByDateRange() {
			MaintItem maintItemA=InsertItem("A",new DateTime(2026,1,1));
			MaintItem maintItemB=InsertItem("B",new DateTime(2026,1,1));
			foreach((MaintItem maintItem,DateTime dateDone) in new[] { (maintItemA,new DateTime(2026,3,1)),(maintItemA,new DateTime(2026,6,1)),
				(maintItemB,new DateTime(2026,6,15)),(maintItemB,new DateTime(2026,9,1)) })
			{
				MaintRecords.Insert(new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=dateDone,Note="" },new List<MaintRecordValue>());
			}
			CollectionAssert.AreEqual(new[] { new DateTime(2026,6,1),new DateTime(2026,3,1) },
				MaintRecords.GetForItems(new List<long> { maintItemA.MaintItemNum }).Select(x => x.DateDone).ToList(),"Newest first.");
			Assert.AreEqual(4,MaintRecords.GetForItems().Count);
			Assert.AreEqual(0,MaintRecords.GetForItems(new List<long>()).Count);
			List<MaintRecord> listRange=MaintRecords.GetForDateRange(new DateTime(2026,6,1),new DateTime(2026,6,30));
			CollectionAssert.AreEqual(new[] { new DateTime(2026,6,15),new DateTime(2026,6,1) },listRange.Select(x => x.DateDone).ToList(),"Inclusive range.");
			Assert.AreEqual(1,MaintRecords.GetForDateRange(new DateTime(2026,6,1),new DateTime(2026,6,30),maintItemB.MaintItemNum).Count);
			Assert.IsTrue(MaintItems.HasRecords(maintItemA.MaintItemNum));
		}

		[TestMethod]
		public void Delete_OnlyWithoutHistory_AndTakesStepsAndFields() {
			MaintItem maintItemKept=InsertItem("Has history",new DateTime(2026,11,2));
			MaintRecords.Insert(new MaintRecord { MaintItemNum=maintItemKept.MaintItemNum,DateDone=new DateTime(2026,11,2),Note="" },new List<MaintRecordValue>());
			bool isThrown=false;
			try {
				MaintItems.Delete(maintItemKept.MaintItemNum);
			}
			catch(ApplicationException) {
				isThrown=true;//MSTest v1 has no Assert.ThrowsException.
			}
			Assert.IsTrue(isThrown,"An item with recorded completions cannot be deleted.");
			Assert.IsNotNull(MaintItems.GetOne(maintItemKept.MaintItemNum),"Hide it instead; the history stays.");
			MaintItem maintItem=InsertItem("Typo",new DateTime(2026,11,2));
			MaintSteps.Sync(maintItem.MaintItemNum,new List<MaintStep> { new MaintStep { Description="x" } });
			MaintFields.Sync(maintItem.MaintItemNum,new List<MaintField> { new MaintField { FieldName="y",FieldType=EnumMaintFieldType.Text,Unit="" } });
			MaintSteps.Sync(maintItemKept.MaintItemNum,new List<MaintStep> { new MaintStep { Description="kept" } });
			Assert.IsFalse(MaintItems.HasRecords(maintItem.MaintItemNum));
			MaintItems.Delete(maintItem.MaintItemNum);
			Assert.IsNull(MaintItems.GetOne(maintItem.MaintItemNum));
			Assert.AreEqual(0,MaintSteps.GetForItem(maintItem.MaintItemNum).Count);
			Assert.AreEqual(0,MaintFields.GetForItem(maintItem.MaintItemNum).Count);
			Assert.AreEqual(1,MaintSteps.GetForItem(maintItemKept.MaintItemNum).Count,"Other items keep theirs.");
		}

		[TestMethod]
		public void Crud_RoundTripEveryFieldOfEveryTable() {
			List<string> listProblems=new List<string>();
			foreach(Type type in new[] { typeof(MaintItem),typeof(MaintStep),typeof(MaintField),typeof(MaintRecord),typeof(MaintRecordValue) }) {
				RoundTrip(type,listProblems);
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		private static void RoundTrip(Type type,List<string> listProblems) {
			Type typeCrud=typeof(TableBase).Assembly.GetType("OpenPmsDentalBusiness.Crud."+type.Name+"Crud",true);
			MethodInfo methodInsert=typeCrud.GetMethod("Insert",new[] { type });
			MethodInfo methodSelectOne=typeCrud.GetMethod("SelectOne",new[] { typeof(long) });
			MethodInfo methodUpdate=typeCrud.GetMethod("Update",new[] { type });
			MethodInfo methodDelete=typeCrud.GetMethod("Delete",new[] { typeof(long) });
			List<FieldInfo> listFields=type.GetFields(BindingFlags.Public|BindingFlags.Instance).ToList();
			FieldInfo fieldPriKey=listFields.First(x => GetCrudColumn(x)?.IsPriKey==true);
			for(int pass=1;pass<=2;pass++) {
				object obj=Activator.CreateInstance(type);
				Fill(obj,listFields,pass);
				long priKey=(long)methodInsert.Invoke(null,new[] { obj });
				Compare(type.Name+" insert",obj,methodSelectOne.Invoke(null,new object[] { priKey }),listFields,fieldPriKey,false,listProblems);
				Fill(obj,listFields,pass+10);
				fieldPriKey.SetValue(obj,priKey);
				methodUpdate.Invoke(null,new[] { obj });
				Compare(type.Name+" update",obj,methodSelectOne.Invoke(null,new object[] { priKey }),listFields,fieldPriKey,true,listProblems);
				methodDelete.Invoke(null,new object[] { priKey });
				if(methodSelectOne.Invoke(null,new object[] { priKey })!=null) {
					listProblems.Add(type.Name+": not deleted");
				}
			}
		}

		private static void Fill(object obj,List<FieldInfo> listFields,int seed) {
			for(int i=0;i<listFields.Count;i++) {
				FieldInfo field=listFields[i];
				if(IsSkipped(field,false)) {
					continue;
				}
				int value=seed*7+i;
				if(field.FieldType.IsEnum) {
					Array arrayValues=Enum.GetValues(field.FieldType);
					field.SetValue(obj,arrayValues.GetValue(value%arrayValues.Length));
				}
				else if(field.FieldType==typeof(string)) {
					field.SetValue(obj,"v"+value+"'"+field.Name+" °");
				}
				else if(field.FieldType==typeof(long)) {
					field.SetValue(obj,(long)(1000+value));
				}
				else if(field.FieldType==typeof(int)) {
					field.SetValue(obj,10+value);
				}
				else if(field.FieldType==typeof(bool)) {
					field.SetValue(obj,value%2==0);
				}
				else if(field.FieldType==typeof(double)) {
					field.SetValue(obj,12.345678+value);
				}
				else if(field.FieldType==typeof(DateTime)) {
					field.SetValue(obj,new DateTime(2026,5,6).AddDays(value));
				}
			}
		}

		private static void Compare(string label,object expected,object actual,List<FieldInfo> listFields,FieldInfo fieldPriKey,bool isUpdate,
			List<string> listProblems)
		{
			if(actual==null) {
				listProblems.Add(label+": SelectOne found no row.");
				return;
			}
			foreach(FieldInfo field in listFields) {
				if(field==fieldPriKey || IsSkipped(field,isUpdate)) {
					continue;
				}
				object valueExpected=field.GetValue(expected);
				object valueActual=field.GetValue(actual);
				if(valueExpected is double doubleExpected) {
					if(Math.Abs(doubleExpected-(double)valueActual)>0.000001) {
						listProblems.Add(label+": "+field.Name+" expected "+valueExpected+" but was "+valueActual);
					}
					continue;
				}
				if(!Equals(valueExpected,valueActual)) {
					listProblems.Add(label+": "+field.Name+" expected ["+valueExpected+"] but was ["+valueActual+"]");
				}
			}
		}

		private static bool IsSkipped(FieldInfo field,bool isUpdate) {
			CrudColumnAttribute crudColumn=GetCrudColumn(field);
			if(crudColumn==null) {
				return false;
			}
			if(crudColumn.IsPriKey) {
				return true;
			}
			CrudSpecialColType specialType=crudColumn.SpecialType;
			if(specialType.HasFlag(CrudSpecialColType.TimeStamp) || specialType.HasFlag(CrudSpecialColType.DateTEntry)) {
				return true;
			}
			if(isUpdate && specialType.HasFlag(CrudSpecialColType.ExcludeFromUpdate)) {
				return true;
			}
			return false;
		}

		private static CrudColumnAttribute GetCrudColumn(FieldInfo field) {
			return field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
		}
	}
}
