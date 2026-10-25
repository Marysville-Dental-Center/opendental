// Unit tests for the chart letter rules that need no database: hash, statuses, progress notes descriptions and row types, and the letter merge
// field names. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ChartLetterTests {
	[TestClass]
	public class ChartLetterRulesTests {
		[TestMethod]
		public void ComputeHash_Is32UpperHexOfMd5() {
			//RFC 1321 test vector: MD5("abc").
			Assert.AreEqual("900150983CD24FB0D6963F7D28E17F72",ChartLetters.ComputeHash(Encoding.ASCII.GetBytes("abc")));
			Assert.AreEqual("D41D8CD98F00B204E9800998ECF8427E",ChartLetters.ComputeHash(new byte[0]));
			Assert.AreEqual(32,ChartLetters.ComputeHash(new byte[] { 1,2,3 }).Length);
		}

		[TestMethod]
		public void HashesMatch_IgnoresCaseAndSpaces() {
			Assert.IsTrue(ChartLetters.HashesMatch("900150983cd24fb0d6963f7d28e17f72"," 900150983CD24FB0D6963F7D28E17F72 "));
			Assert.IsFalse(ChartLetters.HashesMatch("900150983CD24FB0D6963F7D28E17F72","D41D8CD98F00B204E9800998ECF8427E"));
			Assert.IsFalse(ChartLetters.HashesMatch("","900150983CD24FB0D6963F7D28E17F72"));
		}

		[TestMethod]
		public void GetHashMatch_MatchNoMatchMissing() {
			byte[] bytes=Encoding.ASCII.GetBytes("abc");
			Document document=new Document { ChartLetterStatus=1,ChartLetterHash="900150983cd24fb0d6963f7d28e17f72" };
			Assert.AreEqual(EnumChartLetterHashMatch.Match,ChartLetters.GetHashMatch(document,bytes));
			Assert.AreEqual(EnumChartLetterHashMatch.NoMatch,ChartLetters.GetHashMatch(document,Encoding.ASCII.GetBytes("abd")));
			Assert.AreEqual(EnumChartLetterHashMatch.FileMissing,ChartLetters.GetHashMatch(document,new byte[0]));
			Assert.AreEqual(EnumChartLetterHashMatch.FileMissing,ChartLetters.GetHashMatch(document,null));
		}

		[TestMethod]
		public void GetStatus_KnownAndUnknownValues() {
			Assert.AreEqual(EnumChartLetterStatus.None,ChartLetters.GetStatus(new Document()));
			Assert.AreEqual(EnumChartLetterStatus.None,ChartLetters.GetStatus(null));
			Assert.AreEqual(EnumChartLetterStatus.Current,ChartLetters.GetStatus(new Document { ChartLetterStatus=1 }));
			Assert.AreEqual(EnumChartLetterStatus.Superseded,ChartLetters.GetStatus(new Document { ChartLetterStatus=2 }));
			Assert.AreEqual(EnumChartLetterStatus.Deleted,ChartLetters.GetStatus(new Document { ChartLetterStatus=3 }));
			//A value Open Dental might use that we do not know stays visible.
			Assert.AreEqual(EnumChartLetterStatus.Current,ChartLetters.GetStatus(new Document { ChartLetterStatus=7 }));
			Assert.IsTrue(ChartLetters.IsChartLetter(new Document { ChartLetterStatus=7 }));
			Assert.IsFalse(ChartLetters.IsChartLetter(new Document()));
		}

		[TestMethod]
		public void StatusNumbers_ArePinned() {
			Assert.AreEqual(0,(int)EnumChartLetterStatus.None);
			Assert.AreEqual(1,(int)EnumChartLetterStatus.Current);
			Assert.AreEqual(2,(int)EnumChartLetterStatus.Superseded);
			Assert.AreEqual(3,(int)EnumChartLetterStatus.Deleted);
		}

		[TestMethod]
		public void GetProgNoteDescription_MarksHistory() {
			Assert.AreEqual("Chart Letter - Post-op",ChartLetters.GetProgNoteDescription("Post-op",1));
			Assert.AreEqual("Chart Letter",ChartLetters.GetProgNoteDescription("",1));
			Assert.AreEqual("Chart Letter - Post-op (old version)",ChartLetters.GetProgNoteDescription("Post-op",2));
			Assert.AreEqual("Chart Letter - Post-op (deleted)",ChartLetters.GetProgNoteDescription("Post-op",3));
		}

		[TestMethod]
		public void SignatureKeyData_BoundToFileBytes() {
			byte[] bytes=Encoding.ASCII.GetBytes("abc");
			Assert.AreEqual(ChartLetters.ComputeHash(bytes),ChartLetters.GetSignatureKeyData(bytes));
			Assert.AreNotEqual(ChartLetters.GetSignatureKeyData(bytes),ChartLetters.GetSignatureKeyData(Encoding.ASCII.GetBytes("abd")));
			List<string> listKeys=ChartLetters.GetSignatureKeyDataCandidates(bytes,"note");
			Assert.AreEqual(2,listKeys.Count);
			Assert.AreEqual(ChartLetters.GetSignatureKeyData(bytes),listKeys[0]);
			Assert.AreNotEqual(listKeys[0],listKeys[1]);
		}

		[TestMethod]
		public void LetterMergeFieldNames_ReferralPrefix() {
			Assert.AreEqual("RefLName",LetterMergesQueries.GetDataFieldName("referral.LName"));
			Assert.AreEqual("FName",LetterMergesQueries.GetDataFieldName("FName"));
			Assert.AreEqual("",LetterMergesQueries.GetDataFieldName(null));
		}

		private static DataTable MakeProgNotesTable() {
			DataTable table=new DataTable();
			foreach(string column in new[] { "AptNum","ProcNum","CommlogNum","WebChatSessionNum","TaskNum","EmailMessageNum","LabCaseNum","RxNum",
				"SheetNum","FormPatNum","EFormNum","DocNum" })
			{
				table.Columns.Add(column);
			}
			table.Columns.Add("ProcDate",typeof(DateTime));
			return table;
		}

		private static DataRow AddRow(DataTable table,string pkColumn,long pk,long docNum,DateTime date) {
			DataRow row=table.NewRow();
			foreach(DataColumn dataColumn in table.Columns) {
				if(dataColumn.ColumnName!="ProcDate") {
					row[dataColumn]="0";
				}
			}
			if(pkColumn!=null) {
				row[pkColumn]=pk.ToString();
			}
			row["DocNum"]=docNum.ToString();
			row["ProcDate"]=date;
			table.Rows.Add(row);
			return row;
		}

		[TestMethod]
		public void GetRowType_ChartLetterVersusSheetWithDocNum() {
			DataTable table=MakeProgNotesTable();
			DataRow rowLetter=AddRow(table,null,0,55,DateTime.Today);
			DataRow rowSheet=AddRow(table,"SheetNum",9,77,DateTime.Today);
			Assert.AreEqual(ProgNotesRowType.ChartLetter,ChartModules.GetRowType(rowLetter,out long pkLetter));
			Assert.AreEqual(55,pkLetter);
			Assert.AreEqual(ProgNotesRowType.Sheet,ChartModules.GetRowType(rowSheet,out long pkSheet),"A sheet's saved copy is not a chart letter.");
			Assert.AreEqual(9,pkSheet);
		}

		[TestMethod]
		public void GetRowType_TableWithoutDocNumColumn_StillWorks() {
			DataTable table=MakeProgNotesTable();
			table.Columns.Remove("DocNum");
			DataRow row=table.NewRow();
			foreach(DataColumn dataColumn in table.Columns) {
				if(dataColumn.ColumnName!="ProcDate") {
					row[dataColumn]="0";
				}
			}
			table.Rows.Add(row);
			Assert.AreEqual(ProgNotesRowType.None,ChartModules.GetRowType(row,out long _));
		}

		[TestMethod]
		public void CompareChartRows_ChartLettersAfterSheetsSameDay() {
			DataTable table=MakeProgNotesTable();
			DateTime date=new DateTime(2025,3,4,10,0,0);
			DataRow rowLetter=AddRow(table,null,0,55,date);
			DataRow rowSheet=AddRow(table,"SheetNum",9,0,date.AddHours(2));
			DataRow rowLetterLater=AddRow(table,null,0,56,date.AddHours(1));
			Assert.IsTrue(ChartModules.CompareChartRows(rowSheet,rowLetter)<0);
			Assert.IsTrue(ChartModules.CompareChartRows(rowLetter,rowSheet)>0);
			Assert.IsTrue(ChartModules.CompareChartRows(rowLetter,rowLetterLater)<0,"Letters on the same day sort by time.");
		}
	}
}
