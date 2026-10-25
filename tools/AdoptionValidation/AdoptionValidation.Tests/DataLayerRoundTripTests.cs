// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using SyntheticData.Infrastructure;
using SyntheticData.Validation;

namespace AdoptionValidation.Tests {
	///<summary>The parts of the data-layer round trip that need no database: which tables and columns it checks, that the business layer has a
	///class and generated CRUD for each of them, and how it compares and changes values.</summary>
	[TestClass]
	public class DataLayerRoundTripTests {
		///<summary>R1's detail (and the validation report) says "stored back unchanged" only of columns whose cells the save did not change: a 24.4-25.3
		///column with a first-save normalization is named with it. Normalizations of older columns of the same rows are not counted among them.</summary>
		[TestMethod]
		public void RoundTripDescription_CountsTheNewColumnsTheSaveNormalized() {
			RoundTripSummary summary=new RoundTripSummary();
			TableRoundTrip benefit=new TableRoundTrip { Table="benefit",Columns=new List<string> { "ToothRange" },ColumnsLoaded=1,ColumnsWritten=1,RowsSaved=370 };
			benefit.DictNormalized["SecDateTEdit: ON UPDATE timestamp of a row the save normalized"]=363;
			benefit.DictNormalized["ToothRange: NULL saved as ''"]=363;
			benefit.ListNormalizedColumns.AddRange(new[] { "ToothRange","SecDateTEdit" });
			TableRoundTrip document=new TableRoundTrip { Table="document",Columns=new List<string> { "A","B" },ColumnsLoaded=2,ColumnsWritten=2,RowsSaved=10 };
			document.DictNormalized["ExternalSource: blank or unknown enum text saved as 'None'"]=10;
			document.ListNormalizedColumns.Add("ExternalSource");
			summary.Tables.Add(benefit);
			summary.Tables.Add(document);
			string text=Rehearsal.DescribeRoundTrip(summary);
			StringAssert.Contains(text,"3 loaded and saved (380 rows saved), 2 of them stored back unchanged and 1 with first-save normalizations "
				+"(benefit.ToothRange: NULL saved as '' (363 cells)); 3 of the 3 given a new value");
			Assert.IsFalse(text.Contains("saved unchanged"),text);
			CollectionAssert.AreEqual(new List<string> { "benefit.ToothRange: NULL saved as '' (363 cells)" },Rehearsal.ListNormalizedNewColumns(summary));
		}

		[TestMethod]
		public void EveryNewOrChangedColumn_HasAClassFieldAndCrud_ExceptTheDocumentedOnes() {
			Dictionary<string,Type> dictTypes=DataLayerRoundTrip.GetTableTypes();
			List<string> listProblems=new List<string>();
			IEnumerable<(string table,string column)> items=Coverage.NewOrChangedColumns.Where(x => x!="patient._portal_active_email")
				.Select(x => (x.Split('.')[0],x.Split('.')[1]));
			foreach((string table,string column) in items.Concat(Coverage.NewTables.Select(x => (x,(string)null)))) {
				if(DataLayerRoundTrip.DictDocumentedExceptions.ContainsKey(table)
					|| (column!=null && DataLayerRoundTrip.DictDocumentedExceptions.ContainsKey(table+"."+column)))
				{
					continue;
				}
				if(!dictTypes.TryGetValue(table,out Type type)) {
					listProblems.Add(table+": no table class");
					continue;
				}
				if(typeof(TableBase).Assembly.GetType("OpenDentBusiness.Crud."+type.Name+"Crud")==null) {
					listProblems.Add(table+": no CRUD class for "+type.Name);
					continue;
				}
				if(column!=null && !DataLayerRoundTrip.GetDbFields(type).Any(x => string.Equals(x.Name,column,StringComparison.OrdinalIgnoreCase))) {
					listProblems.Add(table+"."+column+": no field in "+type.Name);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void ColumnsByTable_CoverEveryCoverageItem_ExceptTheDroppedOfficeColumn() {
			SchemaInfo schema=new SchemaInfo { Database="x" };
			foreach(string table in Coverage.NewTables) {
				TableInfo tableInfo=new TableInfo { Name=table };
				tableInfo.ListColumns.Add(new ColumnInfo { Name=table+"Num",IsPrimaryKey=true,DataType="bigint",ColumnType="bigint(20)" });
				tableInfo.ListColumns.Add(new ColumnInfo { Name="Value",DataType="varchar",ColumnType="varchar(255)",MaxLength=255 });
				schema.DictTables[table]=tableInfo;
			}
			SortedDictionary<string,List<string>> dict=DataLayerRoundTrip.GetColumnsByTable(schema);
			foreach(string item in Coverage.NewOrChangedColumns) {
				string table=item.Split('.')[0];
				string column=item.Split('.')[1];
				if(item=="patient._portal_active_email") {
					Assert.IsFalse(dict.ContainsKey("patient"),"Adoption drops the office column; nothing else in patient is new.");
					continue;
				}
				Assert.IsTrue(dict[table].Contains(column),item);
			}
			foreach(string table in Coverage.NewTables) {
				CollectionAssert.Contains(dict[table],"Value",table);
				CollectionAssert.DoesNotContain(dict[table],table+"Num","The primary key is not one of the checked columns.");
			}
		}

		[TestMethod]
		public void ValuesMatch_JudgesByTheColumnType() {
			ColumnInfo columnDate=new ColumnInfo { Name="D",DataType="date",ColumnType="date" };
			ColumnInfo columnText=new ColumnInfo { Name="T",DataType="varchar",ColumnType="varchar(50)" };
			Assert.IsTrue(DataLayerRoundTrip.ValuesMatch(null,"",columnText),"NULL loads as an empty string.");
			Assert.IsFalse(DataLayerRoundTrip.ValuesMatch("abc ","abc",columnText),"No trimming.");
			Assert.IsTrue(DataLayerRoundTrip.ValuesMatch(new DateTime(2025,3,4),new DateTime(2025,3,4,10,0,0),columnDate));
			Assert.IsTrue(DataLayerRoundTrip.ValuesMatch(true,true,null),"MySqlConnector reads tinyint(1) as bool.");
			Assert.IsTrue(DataLayerRoundTrip.ValuesMatch((sbyte)4,StatementMode.API,null),"Statement mode 4 (API, Open Dental 25.3) loads as API.");
			Assert.IsFalse(DataLayerRoundTrip.ValuesMatch(4L,StatementMode.InPerson,null));
			Assert.IsTrue(DataLayerRoundTrip.ValuesMatch(12.5m,12.5,null));
		}

		[TestMethod]
		public void NewValue_WidenedColumnsGetValuesLongerThanTheOldWidth() {
			FieldInfo fieldGroupNum=typeof(InsPlan).GetField(nameof(InsPlan.GroupNum));
			ColumnInfo column=new ColumnInfo { Name="GroupNum",DataType="varchar",ColumnType="varchar(50)",MaxLength=50 };
			string value=(string)DataLayerRoundTrip.NewValue(fieldGroupNum,"G1",column,"insplan.GroupNum");
			Assert.IsTrue(value.Length>25 && value.Length<=50,value);
			Assert.IsTrue(value.Contains("'") && value.Contains("é"),"Escaping and non-ASCII text are exercised.");
			FieldInfo fieldNote=typeof(ClaimPayment).GetField(nameof(ClaimPayment.Note));
			ColumnInfo columnNote=new ColumnInfo { Name="Note",DataType="varchar",ColumnType="varchar(255)",MaxLength=255 };
			Assert.AreEqual(255,((string)DataLayerRoundTrip.NewValue(fieldNote,"",columnNote,"claimpayment.Note")).Length,
				"claimpayment.Note keeps varchar(255) in 25.3.48: filled to the old width exactly.");
			Assert.AreEqual(true,DataLayerRoundTrip.NewValue(typeof(Medication).GetField(nameof(Medication.IsHidden)),false,null,"medication.IsHidden"));
		}

		[TestMethod]
		public void Normalizations_OnlyTheKnownCrudKinds() {
			FieldInfo fieldText=typeof(InsPlan).GetField(nameof(InsPlan.GroupNum));
			Assert.AreEqual("NULL saved as ''",DataLayerRoundTrip.GetNormalizationKind(fieldText,null,""));
			Assert.IsNull(DataLayerRoundTrip.GetNormalizationKind(fieldText,"A1","A2"),"A changed value is never a normalization.");
			Assert.IsNull(DataLayerRoundTrip.GetNormalizationKind(fieldText,"A1",""),"Blanking a value is never a normalization.");
			FieldInfo fieldEnumText=typeof(Document).GetField(nameof(Document.ExternalSource));
			StringAssert.StartsWith(DataLayerRoundTrip.GetNormalizationKind(fieldEnumText,"","None"),"blank or unknown enum text");
			string nameOther=Enum.GetNames(fieldEnumText.FieldType).First(x => x!="None");
			Assert.IsNull(DataLayerRoundTrip.GetNormalizationKind(fieldEnumText,nameOther,"None"),"A known enum name changing is a real change.");
			FieldInfo fieldNumber=typeof(InsPlan).GetField(nameof(InsPlan.PlanNum));
			Assert.IsNull(DataLayerRoundTrip.GetNormalizationKind(fieldNumber,null,0L));
		}
	}
}
