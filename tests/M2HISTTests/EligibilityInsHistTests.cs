// What an eligibility response (271) line gives Insurance History from Open Dental 26.1, without a database: the latest-visit date (DTP*304),
// the category its MSG names and the procedure code it names. Synthetic segments only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2HISTTests {
	[TestClass]
	public class EligibilityInsHistTests {
		///<summary>An EB271 built from raw segments without the procedure code cache: the constructor's spoof path (null segment), then the fields set.
		///</summary>
		internal static EB271 MakeEb271(string eb,params string[] arraySupplemental) {
			EB271 eb271=new EB271(null,true,false);
			eb271.Segment=new X12Segment(eb,new X12Separators());
			eb271.SupplementalSegments=new List<X12Segment>();
			for(int i=0;i<arraySupplemental.Length;i++) {
				eb271.SupplementalSegments.Add(new X12Segment(arraySupplemental[i],new X12Separators()));
			}
			return eb271;
		}

		[TestMethod]
		public void GetInsHistDate_ReadsTheMonth() {
			EB271 eb271=MakeEb271("EB*F*IND*4","DTP*304*D8*20250918","MSG*BITEWING X-RAYS");
			Assert.AreEqual(new DateTime(2025,9,18),eb271.GetInsHistDate(),"CCYYMMDD; the 24.3 format string read the month as minutes.");
			Assert.AreEqual(new DateTime(2025,9,18),eb271.GetInsHistDate(PrefName.InsHistBWCodes),"The 24.3 reader is fixed the same way.");
			Assert.AreEqual(EnumCodeGroupFixed.BW,eb271.GetCodeGroupFixedInsHist());
			Assert.AreEqual("",eb271.GetProcCodeInsHist());
		}

		[TestMethod]
		public void GetInsHistDate_NeedsALimitationWithDtp304AndAMsgOrACode() {
			Assert.AreEqual(DateTime.MinValue,MakeEb271("EB*C*IND*4","DTP*304*D8*20250918","MSG*EXAMS").GetInsHistDate(),"Not a limitation.");
			Assert.AreEqual(DateTime.MinValue,MakeEb271("EB*F*IND*4","DTP*346*D8*20250918","MSG*EXAMS").GetInsHistDate(),"Not a latest visit.");
			Assert.AreEqual(DateTime.MinValue,MakeEb271("EB*F*IND*4","DTP*304*D8*20250918").GetInsHistDate(),"Nothing says what it is for.");
			EB271 eb271Code=MakeEb271("EB*F*IND*4"+new string('*',10)+"AD:Z1206","DTP*304*D8*20250102");//EB13 is element 13.
			Assert.AreEqual(new DateTime(2025,1,2),eb271Code.GetInsHistDate(),"A procedure code says what it is for.");
			Assert.AreEqual("Z1206",eb271Code.GetProcCodeInsHist());
			Assert.AreEqual(EnumCodeGroupFixed.None,eb271Code.GetCodeGroupFixedInsHist());
			Assert.AreEqual("(Z1206)",eb271Code.GetInsHistDescription());
			EB271 eb271Fluoride=MakeEb271("EB*F*IND*4","DTP*304*D8*20250102","MSG*FLUORIDE");
			Assert.AreEqual(new DateTime(2025,1,2),eb271Fluoride.GetInsHistDate(),"Any MSG text: matching is the caller's job.");
			Assert.AreEqual(EnumCodeGroupFixed.None,eb271Fluoride.GetCodeGroupFixedInsHist());
			Assert.AreEqual("FLUORIDE",eb271Fluoride.GetInsHistDescription());
		}

		[TestMethod]
		public void ParseInsHistDate_RefusesBadOldAndFutureDates() {
			Assert.AreEqual(new DateTime(2024,12,31),EB271.ParseInsHistDate("20241231"));
			Assert.AreEqual(DateTime.MinValue,EB271.ParseInsHistDate("20241331"));
			Assert.AreEqual(DateTime.MinValue,EB271.ParseInsHistDate("18000101"));
			Assert.AreEqual(DateTime.MinValue,EB271.ParseInsHistDate(DateTime.Today.AddDays(1).ToString("yyyyMMdd")));
			Assert.AreEqual(DateTime.MinValue,EB271.ParseInsHistDate("20240101-20241231"),"A date range is not a visit date.");
			Assert.AreEqual(DateTime.MinValue,EB271.ParseInsHistDate(null));
		}

		[TestMethod]
		public void GetCodeGroupFixedMatchedMsg_TheFourKnownTexts() {
			Assert.AreEqual(EnumCodeGroupFixed.BW,EB271.GetCodeGroupFixedMatchedMsg("BITEWING X-RAYS"));
			Assert.AreEqual(EnumCodeGroupFixed.Exam,EB271.GetCodeGroupFixedMatchedMsg("BenefitClass=Exams"));
			Assert.AreEqual(EnumCodeGroupFixed.PanoFMX,EB271.GetCodeGroupFixedMatchedMsg("full mouth/panorex"));
			Assert.AreEqual(EnumCodeGroupFixed.Prophy,EB271.GetCodeGroupFixedMatchedMsg("PROPHYLAXIS"));
			Assert.AreEqual(EnumCodeGroupFixed.None,EB271.GetCodeGroupFixedMatchedMsg("SEALANTS"));
			Assert.AreEqual(EnumCodeGroupFixed.None,EB271.GetCodeGroupFixedMatchedMsg(null));
		}
	}
}
