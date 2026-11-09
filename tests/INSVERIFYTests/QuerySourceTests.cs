// A source check of the Insurance Verification List's query (INSVERIFY, docs/port-notes/INSVERIFY.md): the outer GROUP BY of
// InsVerifies.GetVerifyGridListQuery keeps the verify type, so a later port of Open Dental's file cannot drop it again without this test saying
// so. The database tests (VerifyGridKeyTests) show what it does; this check needs no database.
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
		public void GetVerifyGridListQuery_GroupsByPatientPlanVerifyTypeAndKey() {
			List<string> listLines=GetMethodLines(Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),METHOD_SIGNATURE);
			List<string> listGroupBy=listLines.FindAll(x => x.Contains("GROUP BY"));
			Assert.AreEqual(1,listGroupBy.Count,"one GROUP BY in the query: "+string.Join(" | ",listGroupBy));
			//The HAVING and the ORDER BY are Open Dental's (24.3.15) and stay as they are.
			Assert.AreEqual("return \"SELECT * FROM (\"+command+\") AS iv GROUP BY PatPlanNum,VerifyType,FKey HAVING MAX(DateLastVerified) ORDER BY AptDateTime\";",
				listGroupBy[0]);
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
