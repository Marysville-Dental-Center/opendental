// A source check of the Insurance Verification List's query (INSVERIFY and INSV2, docs/port-notes/INSVERIFY.md): InsVerifies.GetVerifyGridListQuery
// gives one row per patient plan, verify type and key (the verify type keeps a patient plan's eligibility and benefits rows apart), with the latest
// verification of the key and the earliest appointment, so a later port of Open Dental's file cannot undo it without this test saying so. The
// database tests (VerifyGridKeyTests) show what it does; this check needs no database.
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
		public void GetVerifyGridListQuery_OneRowPerPatientPlanVerifyTypeAndKey() {
			//INSVERIFY grouped by PatPlanNum,VerifyType,FKey; INSV2 numbers the rows of each patient plan, verify type and key from the earliest
			//appointment (ties by AptNum) and keeps the first. The zero-date filter of Open Dental's HAVING MAX(DateLastVerified) stays, on that one row;
			//Open Dental's ORDER BY AptDateTime (24.3.15) is completed so the order never varies.
			List<string> listLines=GetMethodLines(Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),METHOD_SIGNATURE);
			List<string> listGroupBy=listLines.FindAll(x => x.Contains("GROUP BY"));
			Assert.AreEqual(0,listGroupBy.Count,"no GROUP BY in the query: "+string.Join(" | ",listGroupBy));
			List<string> listPartitionBy=listLines.FindAll(x => x.Contains("PARTITION BY"));
			CollectionAssert.AreEqual(new List<string> {
					"ROW_NUMBER() OVER(PARTITION BY patplan.PatPlanNum,insverify.VerifyType,insverify.FKey ORDER BY appointment.AptDateTime,appointment.AptNum) AS AptOrder"
				},listPartitionBy,"the numbering in mainQuery");
			List<string> listReturn=listLines.FindAll(x => x.StartsWith("return "));
			CollectionAssert.AreEqual(new List<string> {
					"return \"SELECT * FROM (\"+command+\") AS iv WHERE AptOrder=1 AND DateLastVerified ORDER BY AptDateTime,AptNum,PatPlanNum,VerifyType\";"
				},listReturn);
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
			//The outer GROUP BY names VerifyType: both parts of the union select insverify.* (mainQuery), and each joins insverify on one verify type.
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
