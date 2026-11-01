// The UDS+ line setup (preference UdsPlusDentalLineCodes): reading, writing, validating and code matching. No database.
// Codes in these tests are made-up placeholders (X-codes), not procedure codes of any published code set.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2UDSTests {
	[TestClass]
	public class UdsPlusLineMapTests {
		[TestMethod]
		public void Blank_IsAnEmptyMap() {
			foreach(string json in new[] { null,""," ","{}","{\"Lines\":null}","[]" }) {
				Assert.IsTrue(UdsPlusLineMap.TryParse(json,out UdsPlusLineMap udsPlusLineMap,out string error),json+": "+error);
				Assert.AreEqual(0,udsPlusLineMap.ListLines.Count,json);
			}
			Assert.AreEqual("",new UdsPlusLineMap().ToJson(),"The shipped default is a blank value.");
		}

		[TestMethod]
		public void Parse_ReadsObjectAndBareArray() {
			UdsPlusLineMap udsPlusLineMap=UdsPlusLineMap.Parse("{\"Lines\":[{\"Line\":\" 27 \",\"Description\":\"Emergency\",\"Codes\":\"X100\"},"
				+"{\"line\":\"28\",\"description\":\"Exams\",\"codes\":\"X200-X299\"}]}");
			Assert.AreEqual(2,udsPlusLineMap.ListLines.Count);
			Assert.AreEqual("27",udsPlusLineMap.ListLines[0].Line,"Trimmed.");
			Assert.AreEqual("Exams",udsPlusLineMap.ListLines[1].Description,"Names ignore case.");
			Assert.AreEqual("X200-X299",udsPlusLineMap.ListLines[1].Codes);
			UdsPlusLineMap udsPlusLineMapArray=UdsPlusLineMap.Parse("[{\"Line\":\"30\",\"Codes\":\"X300\"}]");
			Assert.AreEqual("30",udsPlusLineMapArray.ListLines.Single().Line);
			Assert.AreEqual("",udsPlusLineMapArray.ListLines.Single().Description,"A missing name is blank.");
		}

		[TestMethod]
		public void Parse_RejectsBadText() {
			Assert.IsFalse(UdsPlusLineMap.TryParse("{not json",out _,out string error));
			StringAssert.Contains(error,"not valid JSON");
			Assert.IsFalse(UdsPlusLineMap.TryParse("{\"Lines\":5}",out _,out error));
			StringAssert.Contains(error,"list of lines");
			Assert.IsFalse(UdsPlusLineMap.TryParse("[1,2]",out _,out error));
			StringAssert.Contains(error,"object");
			try {
				UdsPlusLineMap.Parse("\"text\"");
				Assert.Fail("Parse throws on a value that is not a list.");
			}
			catch(ApplicationException) {
			}
		}

		[TestMethod]
		public void ToJson_RoundTrips() {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="27",Description="Emergency \"urgent\" care, walk-in",Codes="X100, X101" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="32",Description="Restorative",Codes="X2*" });
			string json=udsPlusLineMap.ToJson();
			StringAssert.StartsWith(json,"{\"Lines\":[");
			UdsPlusLineMap udsPlusLineMapBack=UdsPlusLineMap.Parse(json);
			Assert.AreEqual(2,udsPlusLineMapBack.ListLines.Count);
			Assert.AreEqual("Emergency \"urgent\" care, walk-in",udsPlusLineMapBack.ListLines[0].Description);
			Assert.AreEqual("X2*",udsPlusLineMapBack.ListLines[1].Codes);
			Assert.AreEqual(json,udsPlusLineMapBack.ToJson());
		}

		[TestMethod]
		public void CodesMatch_ExactRangeAndPrefix() {
			//Exact, ignoring case and spaces; separators are commas, semicolons, spaces and line breaks.
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X100,x101;X102 X103\r\nX104","x101"));
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X100","  X100 "));
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X100","X1000"));
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X100",""));
			//Range: same length, inclusive.
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X200-X299","X200"));
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X200-X299","X250"));
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X200-X299","x299"));
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X200-X299","X300"));
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X200-X299","X25"),"A shorter code is not in a range of longer codes.");
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X200-X299","X2500"),"Nor a longer one.");
			//Prefix.
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X7*","X7"));
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X7*","x7140"));
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X7*","X8140"));
			//A hyphen with sides of different lengths is a custom code, matched exactly.
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X2740-L","X2740-L"));
			Assert.IsFalse(UdsPlusLineMap.CodesMatch("X2740-L","X2741"));
		}

		[TestMethod]
		public void Validate_FindsBlankDuplicateAndBadTokens() {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="27",Codes="X100" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="",Codes="X200" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line=" 27",Codes="" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="29",Codes="X299-X200, *, X*1, X3**" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="30",Codes="" });
			List<string> listErrors=udsPlusLineMap.Validate();
			Assert.AreEqual(5,listErrors.Count,string.Join("\r\n",listErrors));
			Assert.IsTrue(listErrors.Any(x => x.StartsWith("Row 2:") && x.Contains("blank")));
			Assert.IsTrue(listErrors.Any(x => x.StartsWith("Line 27:") && x.Contains("more than once")));
			Assert.IsTrue(listErrors.Any(x => x.Contains("'X299-X200'") && x.Contains("starts after it ends")));
			Assert.IsTrue(listErrors.Any(x => x.Contains("'*'")));
			Assert.IsTrue(listErrors.Any(x => x.Contains("'X*1'")));
			Assert.IsFalse(listErrors.Any(x => x.Contains("'X3**'")),"Several trailing asterisks are one prefix.");
			Assert.IsTrue(UdsPlusLineMap.CodesMatch("X3**","X310"));
			Assert.IsFalse(listErrors.Any(x => x.StartsWith("Line 30")),"A line without codes is allowed.");
		}

		[TestMethod]
		public void GetLinesForCode_ReturnsEveryMatchingLineInSetupOrder() {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="32",Codes="X2*" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="27",Codes="X250" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="28",Codes="X100" });
			CollectionAssert.AreEqual(new[] { "32","27" },udsPlusLineMap.GetLinesForCode("x250").Select(x => x.Line).ToArray());
			CollectionAssert.AreEqual(new[] { "32","27" },udsPlusLineMap.GetLinesForCode("X250").Select(x => x.Line).ToArray(),"Cached lookup.");
			Assert.AreEqual(0,udsPlusLineMap.GetLinesForCode("X999").Count);
			Assert.AreEqual(0,udsPlusLineMap.GetLinesForCode(null).Count);
		}

		[TestMethod]
		public void Table6ATemplate_HasNamesButNoCodes() {
			List<UdsPlusLine> listLines=UdsPlusLineMap.GetTable6ALineTemplate();
			Assert.AreEqual(8,listLines.Count);
			Assert.IsTrue(listLines.All(x => x.Line!="" && x.Description!=""));
			Assert.IsTrue(listLines.All(x => x.Codes==""),"No procedure codes ship with this program.");
			Assert.AreEqual(listLines.Count,listLines.Select(x => x.Line).Distinct().Count());
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap { ListLines=listLines };
			Assert.AreEqual(0,udsPlusLineMap.Validate().Count);
		}
	}
}
