// CRUD round trips for every table class M2-01 added or changed, the second-signature columns of the procedure note queries, and the one-ALTER
// helper of the 26.1 steps, on scratch databases at the 26.1.70 reference with synthetic rows. Skipped (inconclusive) when the local MariaDB does
// not answer. The round-trip harness follows tests/ForkMigrationTests/CrudRoundTripTests.cs (M1-01).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M201Tests {
	[TestClass]
	public class Crud26_1DbTests {
		private static readonly string DB_NAME=ScratchDb.Name("crud261");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			ScratchDb.CreateFromSnapshot(DB_NAME,SchemaVerifier.GetReference26_1_70());
			//The CRUD layer reads RandomPrimaryKeys on every insert; a new Procedure reads DxIcdVersion.
			ScratchDb.Exec(DB_NAME,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('DxIcdVersion','9')");
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

		///<summary>For each table class M2-01 added or changed: fill every column field with a distinct value, Insert through the regenerated CRUD,
		///read the row back with SelectOne and compare every field; then change every field, Update, read back and compare again.</summary>
		[TestMethod]
		public void Crud_RoundTripEveryChangedTable() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
			Dictionary<string,Type> dictTypes=Reference26_1Tests.GetTableTypes().ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string tableName in Reference26_1Tests.ArrayChangedTables) {
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

		///<summary>Long values in the new text columns survive: more than the 255 characters the varchar columns held.</summary>
		[TestMethod]
		public void Crud_LongTextValues() {
			ClaimPayment claimPayment=new ClaimPayment { CheckDate=new DateTime(2026,1,2),Note=new string('c',1000),CheckNum="",BankBranch="",CarrierName="" };
			long claimPaymentNum=OpenPmsDentalBusiness.Crud.ClaimPaymentCrud.Insert(claimPayment);
			Assert.AreEqual(1000,OpenPmsDentalBusiness.Crud.ClaimPaymentCrud.SelectOne(claimPaymentNum).Note.Length);
			InsEditLog insEditLog=new InsEditLog { FieldName="PlanNote",OldValue="",NewValue="",Description="",OldValueBig=new string('o',5000),
				NewValueBig=new string('w',6000) };
			long insEditLogNum=OpenPmsDentalBusiness.Crud.InsEditLogCrud.Insert(insEditLog);
			InsEditLog insEditLogLoaded=OpenPmsDentalBusiness.Crud.InsEditLogCrud.SelectOne(insEditLogNum);
			Assert.AreEqual(5000,insEditLogLoaded.OldValueBig.Length);
			Assert.AreEqual(6000,insEditLogLoaded.NewValueBig.Length);
			Carrier carrier=new Carrier { CarrierName="Synthetic",PreAuthCodes=string.Join(",",Enumerable.Range(1000,200).Select(x => "D"+x)) };
			long carrierNum=OpenPmsDentalBusiness.Crud.CarrierCrud.Insert(carrier);
			Assert.AreEqual(carrier.PreAuthCodes,OpenPmsDentalBusiness.Crud.CarrierCrud.SelectOne(carrierNum).PreAuthCodes);
			string logo=new string('A',200000);//A base64 logo, longer than text (64 KB) allows.
			Branding branding=new Branding { BrandingType=EnumBrandingType.LogoImage,ValueString=logo };
			long brandingNum=OpenPmsDentalBusiness.Crud.BrandingCrud.Insert(branding);
			Assert.AreEqual(logo.Length,OpenPmsDentalBusiness.Crud.BrandingCrud.SelectOne(brandingNum).ValueString.Length);
		}

		///<summary>Statements store StmtType by name: Walkout (25.4) is read back as Walkout, not as NotSet.</summary>
		[TestMethod]
		public void Crud_StatementTypeWalkoutByName() {
			Statement statement=new Statement { PatNum=1,DateSent=new DateTime(2026,3,1),StatementType=StmtType.Walkout,IsServiceDateView=true };
			long statementNum=OpenPmsDentalBusiness.Crud.StatementCrud.Insert(statement);
			Assert.AreEqual("Walkout",DataCore.GetScalar("SELECT StatementType FROM statement WHERE StatementNum="+statementNum));
			Statement statementLoaded=OpenPmsDentalBusiness.Crud.StatementCrud.SelectOne(statementNum);
			Assert.AreEqual(StmtType.Walkout,statementLoaded.StatementType);
			Assert.IsTrue(statementLoaded.IsServiceDateView);
		}

		///<summary>Both hand-written procedure note queries in Procedures.cs load the second signature of the most recent note.</summary>
		[TestMethod]
		public void Procedures_LoadTheSecondSignatureOfTheLatestNote() {
			Procedure procedure=new Procedure { PatNum=901,ProcDate=new DateTime(2026,2,10),ProcStatus=ProcStat.C,CodeNum=1,ProcFee=50 };
			long procNum=OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(procedure);
			Procedure procedureNoSecond=new Procedure { PatNum=901,ProcDate=new DateTime(2026,2,10),ProcStatus=ProcStat.C,CodeNum=1,ProcFee=60 };
			long procNumNoSecond=OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(procedureNoSecond);
			long procNoteNumOld=OpenPmsDentalBusiness.Crud.ProcNoteCrud.Insert(new ProcNote { PatNum=901,ProcNum=procNum,UserNum=1,Note="First note",Signature="old" });
			long procNoteNumNew=OpenPmsDentalBusiness.Crud.ProcNoteCrud.Insert(new ProcNote { PatNum=901,ProcNum=procNum,UserNum=1,Note="Group note",Signature="sig1",
				UserNum2=7,Signature2="sig2",SigIsTopaz2=true });
			OpenPmsDentalBusiness.Crud.ProcNoteCrud.Insert(new ProcNote { PatNum=901,ProcNum=procNumNoSecond,UserNum=3,Note="Single signature",Signature="sigA" });
			//Distinct entry times, so the most recent note is well defined.
			DataCore.NonQ("UPDATE procnote SET EntryDateTime='2026-02-10 09:00:00' WHERE ProcNoteNum="+procNoteNumOld);
			DataCore.NonQ("UPDATE procnote SET EntryDateTime='2026-02-10 10:00:00' WHERE ProcNoteNum="+procNoteNumNew);
			List<Procedure> listMany=Procedures.GetManyProc(new List<long> { procNum,procNumNoSecond },true);
			AssertSecondSignature(listMany.Single(x => x.ProcNum==procNum),"Group note",7,"sig2",true);
			AssertSecondSignature(listMany.Single(x => x.ProcNum==procNumNoSecond),"Single signature",0,"",false);
			List<Procedure> listCompleted=Procedures.GetCompletedForDateRange(new DateTime(2026,2,10),new DateTime(2026,2,10),
				listPatNums:new List<long> { 901 },includeNote:true);
			AssertSecondSignature(listCompleted.Single(x => x.ProcNum==procNum),"Group note",7,"sig2",true);
			AssertSecondSignature(listCompleted.Single(x => x.ProcNum==procNumNoSecond),"Single signature",0,"",false);
			//Without notes nothing is loaded.
			Procedure procedureWithoutNote=Procedures.GetManyProc(new List<long> { procNum },false).Single();
			Assert.AreEqual(0,procedureWithoutNote.UserNum2);
			Assert.AreEqual("",procedureWithoutNote.Signature2);
		}

		private static void AssertSecondSignature(Procedure procedure,string note,long userNum2,string signature2,bool sigIsTopaz2) {
			Assert.AreEqual(note,procedure.Note);
			Assert.AreEqual(userNum2,procedure.UserNum2,note);
			Assert.AreEqual(signature2,procedure.Signature2,note);
			Assert.AreEqual(sigIsTopaz2,procedure.SigIsTopaz2,note);
		}

		///<summary>The helper the 26.1 steps use adds only the missing columns and keys, in one ALTER, at the right positions; a second call changes
		///nothing. This is how a table that already has some of the 26.1 columns is finished.</summary>
		[TestMethod]
		public void AlterTable_AddsTheMissingColumnsInOneStatement() {
			string dbName=ScratchDb.Name("alter");
			try {
				SchemaSnapshot snapshot=new SchemaSnapshot();
				SchemaTable tableCodeGroup=SchemaTable.Parse(SchemaVerifier.GetReference25_3_48().GetTable("codegroup").ToDdl());
				tableCodeGroup.Columns.Add(new SchemaColumn { Name="ShowInHistory",Definition="tinyint(4) NOT NULL",Position=tableCodeGroup.Columns.Count+1 });
				snapshot.AddTable(tableCodeGroup);
				snapshot.AddTable(SchemaTable.Parse(SchemaVerifier.GetReference25_3_48().GetTable("procnote").ToDdl()));
				ScratchDb.CreateFromSnapshot(dbName,snapshot);
				ScratchDb.Connect(dbName);
				ForkMigrationContext context=new ForkMigrationContext(dbName);
				OdColumn[] arrayColumns=new[] {
					new OdColumn("ShowInHistory","tinyint(4) NOT NULL","ShowInOther"),
					new OdColumn("HistProcCode","varchar(255) NOT NULL","ShowInHistory"),
					new OdColumn("IsPerioFourQuads","tinyint(4) NOT NULL","HistProcCode"),
				};
				List<string> listAdded=OdParity26_1.AlterTable(context,"codegroup",arrayColumns);
				CollectionAssert.AreEqual(new[] { "HistProcCode","IsPerioFourQuads" },listAdded);
				Assert.AreEqual(1,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
				//Three columns and a key: still one statement.
				OdParity26_1.AlterTable(context,"procnote",new[] {
					new OdColumn("UserNum2","bigint(20) NOT NULL","Signature"),
					new OdColumn("Signature2","text DEFAULT NULL","UserNum2"),
					new OdColumn("SigIsTopaz2","tinyint(3) unsigned NOT NULL","Signature2"),
				},new[] { new OdKey("UserNum2","`UserNum2`") });
				Assert.AreEqual(2,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
				SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
				foreach(string table in new[] { "codegroup","procnote" }) {
					SchemaVerifyResult result=SchemaVerifier.Compare(Only(reference,table),Only(SchemaSnapshot.FromDatabase(dbName),table));
					Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
				}
				Assert.AreEqual(0,OdParity26_1.AlterTable(context,"codegroup",arrayColumns).Count);
				Assert.AreEqual(2,context.ListChanges.Count,"Nothing changes the second time.");
			}
			finally {
				ScratchDb.DropQuietly(dbName);
				ScratchDb.Connect(DB_NAME);
			}
		}

		private static SchemaSnapshot Only(SchemaSnapshot snapshot,string table) {
			SchemaSnapshot snapshotOne=new SchemaSnapshot();
			snapshotOne.AddTable(snapshot.GetTable(table));
			return snapshotOne;
		}

		#region Round-trip harness (as in tests/ForkMigrationTests/CrudRoundTripTests.cs)
		private static void RoundTrip(Type type,SchemaTable table,List<string> listProblems) {
			Type typeCrud=typeof(TableBase).Assembly.GetType("OpenPmsDentalBusiness.Crud."+type.Name+"Crud",true);
			MethodInfo methodInsert=typeCrud.GetMethod("Insert",new[] { type });
			MethodInfo methodSelectOne=typeCrud.GetMethod("SelectOne",new[] { typeof(long) });
			MethodInfo methodUpdate=typeCrud.GetMethod("Update",new[] { type });
			List<FieldInfo> listFields=Reference26_1Tests.GetDbFields(type).ToList();
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
