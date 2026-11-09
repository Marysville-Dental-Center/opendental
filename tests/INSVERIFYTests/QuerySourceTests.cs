// A source check of the Insurance Verification List's query (INSVERIFY, INSV2 and INSV3, docs/port-notes/INSVERIFY.md): InsVerifies.GetVerifyGridListQuery
// keeps a patient plan's eligibility and benefits rows apart by the verify type and joins only the latest verification of each key, with no window
// function (the program still starts on MySQL 5.0), in an order that puts the earliest appointment of each key first; GetVerifyGridList keeps that
// first row of each key in C# after each query. So a later port of Open Dental's file cannot undo it without this test saying so. The database
// tests (VerifyGridKeyTests) show what it does; this check needs no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace INSVERIFYTests {
	[TestClass]
	public class QuerySourceTests {
		private const string METHOD_SIGNATURE="private static string GetVerifyGridListQuery(";

		[TestMethod]
		public void GetVerifyGridListQuery_NoWindowFunctionAndTheEarliestAppointmentOfEachKeyFirst() {
			//INSVERIFY grouped by PatPlanNum,VerifyType,FKey; INSV2 numbered the rows of each patient plan, verify type and key with ROW_NUMBER() OVER
			//and kept the first. INSV3: no window function (it needs MariaDB 10.2 or MySQL 8.0, and the program still starts on MySQL 5.0), no GROUP BY;
			//the query returns a row for each appointment and orders them so the earliest appointment of each key (ties by AptNum) comes first, which
			//GetVerifyGridList keeps. The zero-date filter of Open Dental's HAVING MAX(DateLastVerified) stays, as DateLastVerified; Open Dental's
			//ORDER BY AptDateTime (24.3.15) is completed so the order never varies.
			List<string> listLines=GetMethodLines(Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),METHOD_SIGNATURE);
			foreach(string sql in new[] { "GROUP BY"," OVER(","OVER (","ROW_NUMBER","RANK(","PARTITION BY","AptOrder" }) {
				List<string> listFound=listLines.FindAll(x => x.ToUpperInvariant().Contains(sql.ToUpperInvariant()));
				Assert.AreEqual(0,listFound.Count,"no "+sql.Trim()+" in the query: "+string.Join(" | ",listFound));
			}
			List<string> listReturn=listLines.FindAll(x => x.StartsWith("return "));
			CollectionAssert.AreEqual(new List<string> {
					"return \"SELECT * FROM (\"+command+\") AS iv WHERE DateLastVerified ORDER BY AptDateTime,AptNum,PatPlanNum,VerifyType\";"
				},listReturn);
		}

		[TestMethod]
		public void GetVerifyGridList_KeepsTheEarliestAppointmentOfEachKeyAfterEachQuery() {
			//INSV3: the choice of INSV2's AptOrder=1 is made in C#, on the server side of a Middle Tier call: GetVerifyGridList passes the table of each
			//query (the standard and the Medicaid list) to RemoveLaterAppointmentRows before it becomes grid rows.
			List<string> listLines=GetMethodLines(Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),
				"public static List<InsVerifyGridObject> GetVerifyGridList(");
			int indexMiddleTier=listLines.FindIndex(x => x.StartsWith("return Meth.GetObject<List<InsVerifyGridObject>>("));
			Assert.IsTrue(indexMiddleTier>=0,"the Middle Tier call comes first");
			List<int> listIndexesQuery=Enumerable.Range(0,listLines.Count).Where(i => listLines[i]=="DataTable table=Db.GetTable(command);").ToList();
			Assert.AreEqual(2,listIndexesQuery.Count,"the standard and the Medicaid query");
			foreach(int index in listIndexesQuery) {
				Assert.IsTrue(index>indexMiddleTier,"on the server side of the call");
				Assert.AreEqual("RemoveLaterAppointmentRows(table);",listLines[index+1],"the choice right after the query");
				StringAssert.StartsWith(listLines[index+2],"List<InsVerifyGridObject> listInsVerifyGridObjects","then the grid rows");
			}
		}

		[TestMethod]
		public void GetVerifyGridListQuery_BothUnionPartsKeepOnlyTheLatestVerificationOfTheirKey() {
			//INSV2: each part of the union joins insverify a second time, on the same verify type and key, for a later row (or one of the same date
			//inserted later), and keeps the row only when there is none. On insverify.FKey, so MySQL can use that index, as for the first join.
			List<string> listLines=GetMethodLines(Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),METHOD_SIGNATURE);
			int index=listLines.IndexOf("string insVerifyLaterJoin=@\"");
			Assert.IsTrue(index>=0,"insVerifyLaterJoin");
			CollectionAssert.AreEqual(new List<string> {
					"LEFT JOIN insverify insverifylater ON insverifylater.VerifyType=insverify.VerifyType",
					"AND insverifylater.FKey=insverify.FKey",
					"AND (insverifylater.DateLastVerified>insverify.DateLastVerified",
					"OR (insverifylater.DateLastVerified=insverify.DateLastVerified AND insverifylater.InsVerifyNum>insverify.InsVerifyNum)) \";"
				},listLines.GetRange(index+1,4));
			Assert.AreEqual(2,listLines.Count(x => x=="insVerifyLaterJoin+"),"in both parts of the union");
			Assert.AreEqual(1,listLines.Count(x => x=="AND insverifylater.InsVerifyNum IS NULL"),"in whereClause, which both parts end with");
			Assert.AreEqual(2,listLines.Count(x => x=="whereClause+@\"" || x=="whereClause;"),"both parts end with whereClause");
		}

		[TestMethod]
		public void GetVerifyGridListQuery_BothUnionPartsExposeTheVerifyType() {
			//The outer ORDER BY and RemoveLaterAppointmentRows name VerifyType: both parts of the union select insverify.* (mainQuery), and each joins
			//insverify on one verify type.
			List<string> listLines=GetMethodLines(Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),METHOD_SIGNATURE);
			Assert.AreEqual(1,listLines.Count(x => x=="SELECT insverify.*,"),"the select list of mainQuery");
			Assert.AreEqual(2,listLines.Count(x => x=="mainQuery+"),"mainQuery starts both parts of the union");
			Assert.IsTrue(listLines.Any(x => x.Contains("insverify.VerifyType=\"+POut.Int((int)VerifyTypes.InsuranceBenefit)")));
			Assert.IsTrue(listLines.Any(x => x.Contains("insverify.VerifyType=\"+POut.Int((int)VerifyTypes.PatientEnrollment)")));
		}

		///<summary>The trimmed lines of the method that starts with the signature, through the closing brace at the signature's indentation. Lines
		///that are comments are left out.</summary>
		private static List<string> GetMethodLines(string relativePath,string signature) {
			string[] arrayLines=File.ReadAllLines(InsvDb.FindRepoFile(relativePath));
			int indexStart=Array.FindIndex(arrayLines,x => x.TrimStart().StartsWith(signature));
			Assert.IsTrue(indexStart>=0,"Not found in "+relativePath+": "+signature);
			string indent=arrayLines[indexStart].Substring(0,arrayLines[indexStart].Length-arrayLines[indexStart].TrimStart().Length);
			List<string> listLines=new List<string>();
			for(int i=indexStart;i<arrayLines.Length;i++) {
				string line=arrayLines[i].Trim();
				if(!line.StartsWith("//")) {
					listLines.Add(line);
				}
				if(i>indexStart && arrayLines[i]==indent+"}") {
					return listLines;
				}
			}
			Assert.Fail("No end of the method in "+relativePath+": "+signature);
			return null;
		}
	}
}
