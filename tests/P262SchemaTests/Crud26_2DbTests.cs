// CRUD round trips for every table class P262-SCHEMA added or changed (glidewellacct, glidewellmsg, labcaseproduct, apisubscription, insbluebook,
// provider), on a scratch database at the 26.2.0 reference with synthetic rows. Skipped (inconclusive) when the local MariaDB does not answer.
// The round-trip harness follows tests/M201Tests/Crud26_1DbTests.cs (M2-01).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262SchemaTests {
	[TestClass]
	public class Crud26_2DbTests {
		private static readonly string DB_NAME=ScratchDb.Name("crud262");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			ScratchDb.CreateFromSnapshot(DB_NAME,SchemaVerifier.GetReference26_2_0());
			//The CRUD layer reads RandomPrimaryKeys on every insert.
			ScratchDb.Exec(DB_NAME,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="Synthetic" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropQuietly(DB_NAME);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
		}

		///<summary>For each table class P262-SCHEMA added or changed: fill every column field with a distinct value, Insert through the generated CRUD,
		///read the row back with SelectOne and compare every field; then change every field, Update, read back and compare again.</summary>
		[TestMethod]
		public void Crud_RoundTripEveryNewAndChangedTable() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_2_0();
			Dictionary<string,Type> dictTypes=Reference26_2Tests.GetTableTypes().ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string tableName in Reference26_2Tests.ArrayChangedTables) {
				Type type=dictTypes[tableName];
				try {
					RoundTrip(type,reference.GetTable(tableName),listProblems);
				}
				catch(Exception ex) {
					listProblems.Add(type.Name+": "+ex.GetBaseException().Message);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>The text columns hold more than the 255 characters a varchar holds, and JSON with quotes and backslashes survives.</summary>
		[TestMethod]
		public void Crud_LongTextValues() {
			string json="{\"OrderId\":42,\"Notes\":\"line1\\nline2 'quoted' \\\\ end\",\"Products\":["+string.Join(",",Enumerable.Range(1,300).Select(x => "\"P"+x+"\""))+"]}";
			GlidewellMsg glidewellMsg=new GlidewellMsg { LabCaseNum=1,PatNum=2,GlidewellAcctNum=3,OrderStatus=EnumGlidewellOrderStatus.Credited,
				RawJsonRequest=json,RawJsonResponse=new string('r',20000) };
			long glidewellMsgNum=OpenPmsDentalBusiness.Crud.GlidewellMsgCrud.Insert(glidewellMsg);
			GlidewellMsg glidewellMsgLoaded=OpenPmsDentalBusiness.Crud.GlidewellMsgCrud.SelectOne(glidewellMsgNum);
			Assert.AreEqual(json,glidewellMsgLoaded.RawJsonRequest);
			Assert.AreEqual(20000,glidewellMsgLoaded.RawJsonResponse.Length);
			Assert.AreEqual(EnumGlidewellOrderStatus.Credited,glidewellMsgLoaded.OrderStatus);
			Assert.AreEqual("7",DataCore.GetScalar("SELECT OrderStatus FROM glidewellmsg WHERE GlidewellMsgNum="+glidewellMsgNum));
			LabCaseProduct labCaseProduct=new LabCaseProduct { LabCaseNum=1,PatNum=2,Notes=new string('n',3000) };
			long labCaseProductNum=OpenPmsDentalBusiness.Crud.LabCaseProductCrud.Insert(labCaseProduct);
			Assert.AreEqual(3000,OpenPmsDentalBusiness.Crud.LabCaseProductCrud.SelectOne(labCaseProductNum).Notes.Length);
			ApiSubscription apiSubscription=new ApiSubscription { EndPointUrl="https://example.invalid/hook",Workstation="",CustomerKey="SYN",WatchTable="",
				UiEventType="",Note="",FailureReason=new string('f',1000),SubsequentFailures=12 };
			long apiSubscriptionNum=OpenPmsDentalBusiness.Crud.ApiSubscriptionCrud.Insert(apiSubscription);
			Assert.AreEqual(1000,OpenPmsDentalBusiness.Crud.ApiSubscriptionCrud.SelectOne(apiSubscriptionNum).FailureReason.Length);
		}

		///<summary>insbluebook.DateTStamp is set by the database on insert and on every change of the row, never by the CRUD.</summary>
		[TestMethod]
		public void Crud_InsBlueBookDateTStampIsSetByTheDatabase() {
			InsBlueBook insBlueBook=new InsBlueBook { ProcCodeNum=1,CarrierNum=2,PlanNum=3,GroupNum="G",InsPayAmt=10,ProcNum=4,ClaimType="P",ClaimNum=5,
				DateTStamp=new DateTime(2001,1,1) };
			long insBlueBookNum=OpenPmsDentalBusiness.Crud.InsBlueBookCrud.Insert(insBlueBook);
			Assert.AreNotEqual(new DateTime(2001,1,1),OpenPmsDentalBusiness.Crud.InsBlueBookCrud.SelectOne(insBlueBookNum).DateTStamp);
			DataCore.NonQ("UPDATE insbluebook SET DateTStamp='2020-01-01 00:00:00' WHERE InsBlueBookNum="+insBlueBookNum);
			insBlueBook=OpenPmsDentalBusiness.Crud.InsBlueBookCrud.SelectOne(insBlueBookNum);
			Assert.AreEqual(new DateTime(2020,1,1),insBlueBook.DateTStamp);
			insBlueBook.InsPayAmt=20;
			OpenPmsDentalBusiness.Crud.InsBlueBookCrud.Update(insBlueBook);
			Assert.IsTrue(OpenPmsDentalBusiness.Crud.InsBlueBookCrud.SelectOne(insBlueBookNum).DateTStamp>new DateTime(2020,1,1),"A change moves it.");
		}

		#region Round-trip harness (as in tests/M201Tests/Crud26_1DbTests.cs)
		private static void RoundTrip(Type type,SchemaTable table,List<string> listProblems) {
			Type typeCrud=typeof(TableBase).Assembly.GetType("OpenPmsDentalBusiness.Crud."+type.Name+"Crud",true);
			MethodInfo methodInsert=typeCrud.GetMethod("Insert",new[] { type });
			MethodInfo methodSelectOne=typeCrud.GetMethod("SelectOne",new[] { typeof(long) });
			MethodInfo methodUpdate=typeCrud.GetMethod("Update",new[] { type });
			List<FieldInfo> listFields=Reference26_2Tests.GetDbFields(type).ToList();
			FieldInfo fieldPriKey=listFields.First(x => GetCrudColumn(x)?.IsPriKey==true);
			object obj=Activator.CreateInstance(type);
			Fill(obj,listFields,table,1);
			long priKey=(long)methodInsert.Invoke(null,new[] { obj });
			object objLoaded=methodSelectOne.Invoke(null,new object[] { priKey });
			Compare(type.Name+" insert",obj,objLoaded,listFields,fieldPriKey,false,listProblems);
			Fill(obj,listFields,table,2);
			fieldPriKey.SetValue(obj,priKey);
			methodUpdate.Invoke(null,new[] { obj });
			objLoaded=methodSelectOne.Invoke(null,new object[] { priKey });
			Compare(type.Name+" update",obj,objLoaded,listFields,fieldPriKey,true,listProblems);
		}

		private static void Fill(object obj,List<FieldInfo> listFields,SchemaTable table,int pass) {
			for(int i=0;i<listFields.Count;i++) {
				FieldInfo field=listFields[i];
				if(IsSkipped(field,false)) {
					continue;
				}
				SchemaColumn column=table.GetColumn(field.Name);
				object value=GetValue(field,column,i+pass*7);
				if(value!=null) {
					field.SetValue(obj,value);
				}
			}
		}

		private static object GetValue(FieldInfo field,SchemaColumn column,int seed) {
			Type type=field.FieldType;
			CrudSpecialColType specialType=GetCrudColumn(field)?.SpecialType??CrudSpecialColType.None;
			if(type.IsEnum) {
				Array values=Enum.GetValues(type);
				if(values.Length==0) {
					//An enum without published values (EnumGlidewellProductName): any number in the column loads and saves as it is.
					return Enum.ToObject(type,1+seed%100);
				}
				return values.GetValue(seed%values.Length);
			}
			if(type==typeof(string)) {
				string value="v"+seed+field.Name;
				Match match=Regex.Match(column.Definition,@"^(?:var)?char\((\d+)\)");
				if(match.Success) {
					value=value.Substring(0,Math.Min(value.Length,int.Parse(match.Groups[1].Value)));
				}
				return value;
			}
			if(type==typeof(long)) {
				return (long)(1000+seed);
			}
			if(type==typeof(int)) {
				return 10+seed;
			}
			if(type==typeof(byte)) {
				return (byte)(1+seed%100);
			}
			if(type==typeof(bool)) {
				return seed%2==0;
			}
			if(type==typeof(double)) {
				return 12.5+seed;
			}
			if(type==typeof(float)) {
				return 1.5f+seed;
			}
			if(type==typeof(DateTime)) {
				if(specialType.HasFlag(CrudSpecialColType.DateT) || column.Definition.StartsWith("datetime")) {
					return new DateTime(2026,5,6,7,8,9).AddMinutes(seed);
				}
				return new DateTime(2026,5,6).AddDays(seed);
			}
			if(type==typeof(TimeSpan)) {
				if(specialType.HasFlag(CrudSpecialColType.TimeSpanLong)) {
					return TimeSpan.FromMinutes(seed);
				}
				return new TimeSpan(1,2,seed%60);
			}
			if(type==typeof(Color)) {
				return Color.FromArgb(255,seed%256,10,20);
			}
			return null;//Left at its default and not compared.
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
				if(valueExpected is Color colorExpected) {
					valueExpected=colorExpected.ToArgb();
					valueActual=((Color)valueActual).ToArgb();
				}
				if(valueExpected is double doubleExpected) {
					if(Math.Abs(doubleExpected-(double)valueActual)>0.0001) {
						listProblems.Add(label+": "+field.Name+" expected "+valueExpected+" but was "+valueActual);
					}
					continue;
				}
				if(!Equals(valueExpected??"",valueActual??"")) {
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
			if(specialType.HasFlag(CrudSpecialColType.TimeStamp) || specialType.HasFlag(CrudSpecialColType.DateEntry)
				|| specialType.HasFlag(CrudSpecialColType.DateTEntry) || specialType.HasFlag(CrudSpecialColType.DateTEntryEditable)
				|| specialType.HasFlag(CrudSpecialColType.DateEntryEditable))
			{
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
		#endregion Round-trip harness
	}
}
