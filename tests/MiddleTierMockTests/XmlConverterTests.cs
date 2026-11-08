// The two tests of the legacy XmlConverterTests (UnitTests/UnitTests/XmlConverterTests.cs), copied here by package S0-T2 of the .NET 10 plan;
// the originals stay in UnitTests. XmlConverter.XmlEscapeRecursion runs on every Middle Tier DTO (Remoting/DataTransferObject.cs): an object
// inside another (a Patient in Family.ListPats) is escaped once only, and line breaks in string fields get the wire spelling that survives
// XmlSerializer. The round trips of MiddleTierRoundTripTests.cs check the same escaping end to end; these pin the escaped form itself. Both
// tests are pure (no database), so they run when ScratchDb skips. Each keeps its name and its arrange, act and assert; the expected string
// spells the section sign as § so that this file stays ASCII.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MiddleTierMockTests {
	[TestClass]
	public class XmlConverterTests {

		///<summary>Strange classes (like Family) were escaping strings too much for HTTP transmission sometimes due to properties and such.</summary>
		[TestMethod]
		public void XmlConverter_XmlEscapeRecursion_Family() {
			Patient pat=new Patient {
				FName="Jason",
				LName="Salmon & Smith",
			};
			Family family=new Family { ListPats=new[] { pat, } };
			//Regardless if this is the correct way to create a Family object, the fact remains that the pat object is used within another object (ListPats)
			//This was causing our recursive method to escape the pat object too many times.
			object familyEscaped=XmlConverter.XmlEscapeRecursion(family.GetType(),family);
			Assert.AreEqual("Salmon &amp; Smith",((Family)familyEscaped).ListPats[0].LName);
		}

		///<summary></summary>
		[TestMethod]
		public void XmlConverter_XmlEscapeRecursion_NewLines() {
			WebServiceTests.WebServiceTestObject testObj=new WebServiceTests.WebServiceTestObject() {
				ValueStr=WebServiceTests.NewLineString,
				ValueStr2=WebServiceTests.NewLineString,
			};
			object testObjEscaped=XmlConverter.XmlEscapeRecursion(testObj.GetType(),testObj);
			string expectedStr="Line1§#13;Line2§#10;Line3§#13;§#10;Line4";
			Assert.AreEqual(expectedStr,((WebServiceTests.WebServiceTestObject)testObjEscaped).ValueStr);
			Assert.AreEqual(expectedStr,((WebServiceTests.WebServiceTestObject)testObjEscaped).ValueStr2);
		}

	}
}
