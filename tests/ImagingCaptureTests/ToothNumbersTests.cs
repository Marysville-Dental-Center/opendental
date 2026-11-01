// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Dicom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>Universal to ISO 3950 tooth numbers (design 7.8: tooth codes in the Primary Anatomic Structure Sequence).</summary>
	[TestClass]
	public class ToothNumbersTests {
		private static IEnumerable<string> AllUniversal() {
			for(int i=1;i<=32;i++) {
				yield return i.ToString();
			}
			for(char c='A';c<='T';c++) {
				yield return c.ToString();
			}
		}

		[TestMethod]
		public void UniversalToIso_PinnedValues() {
			string[,] arrayPairs={
				{ "1","18" },{ "8","11" },{ "9","21" },{ "16","28" },{ "17","38" },{ "24","31" },{ "25","41" },{ "32","48" },
				{ "3","16" },{ "14","26" },{ "19","36" },{ "30","46" },{ "6","13" },{ "11","23" },{ "22","33" },{ "27","43" },
				{ "A","55" },{ "E","51" },{ "F","61" },{ "J","65" },{ "K","75" },{ "O","71" },{ "P","81" },{ "T","85" },{ "C","53" },{ "R","83" },
			};
			for(int i=0;i<arrayPairs.GetLength(0);i++) {
				Assert.AreEqual(arrayPairs[i,1],ToothNumbers.UniversalToIso(arrayPairs[i,0]),"Universal "+arrayPairs[i,0]);
				Assert.AreEqual(arrayPairs[i,0],ToothNumbers.IsoToUniversal(arrayPairs[i,1]),"ISO "+arrayPairs[i,1]);
			}
		}

		[TestMethod]
		public void UniversalToIso_AgreesWithOpenDentalsFdiDisplay() {
			//Open Dental's own nomenclature table (Tooth.Display with FDI) is an independent copy of the same map.
			foreach(string universal in AllUniversal()) {
				Assert.AreEqual(Tooth.Display(universal,ToothNumberingNomenclature.FDI),ToothNumbers.UniversalToIso(universal),"Universal "+universal);
			}
		}

		[TestMethod]
		public void Map_IsOneToOneAndRoundTrips() {
			List<string> listIso=AllUniversal().Select(x => ToothNumbers.UniversalToIso(x)).ToList();
			Assert.AreEqual(52,listIso.Distinct().Count());
			foreach(string universal in AllUniversal()) {
				Assert.AreEqual(universal,ToothNumbers.IsoToUniversal(ToothNumbers.UniversalToIso(universal)));
			}
			int countIso=0;
			for(int quadrant=1;quadrant<=8;quadrant++) {
				for(int position=0;position<=9;position++) {
					string iso=quadrant.ToString()+position.ToString();
					string universal=ToothNumbers.IsoToUniversal(iso);
					bool isValid=position>=1 && (quadrant<=4 ? position<=8 : position<=5);
					Assert.AreEqual(isValid,universal!=null,"ISO "+iso);
					if(universal!=null) {
						countIso++;
						Assert.AreEqual(iso,ToothNumbers.UniversalToIso(universal));
					}
				}
			}
			Assert.AreEqual(52,countIso);
		}

		[TestMethod]
		public void InvalidNumbers_AreNotMapped() {
			string[] arrayInvalid={ null,"","0","33","51","82","AS","TS","U","Z","01","+1","1 2","1-3","3a","-1","99","ab" };
			foreach(string universal in arrayInvalid) {
				Assert.IsNull(ToothNumbers.UniversalToIso(universal),"Universal "+(universal??"null"));
				Assert.IsNull(ToothNumbers.GetInfo(universal));
			}
			string[] arrayInvalidIso={ null,"","10","19","56","66","76","86","90","99","1","118","a1" };
			foreach(string iso in arrayInvalidIso) {
				Assert.IsNull(ToothNumbers.IsoToUniversal(iso),"ISO "+(iso??"null"));
			}
			Assert.AreEqual("55",ToothNumbers.UniversalToIso("a"),"Letters in either case.");
			Assert.AreEqual("18",ToothNumbers.UniversalToIso(" 1 "));
		}

		[TestMethod]
		public void GetInfo_ArchSideAndKind() {
			ToothInfo tooth3=ToothNumbers.GetInfo("3");
			Assert.AreEqual(1,tooth3.Quadrant);
			Assert.AreEqual(6,tooth3.Position);
			Assert.IsTrue(tooth3.IsMaxillary);
			Assert.IsTrue(tooth3.IsPatientRight);
			Assert.IsFalse(tooth3.IsAnterior);
			Assert.IsFalse(tooth3.IsPrimary);
			Assert.AreEqual("Permanent maxillary right first molar",tooth3.Name);
			ToothInfo tooth22=ToothNumbers.GetInfo("22");
			Assert.IsFalse(tooth22.IsMaxillary);
			Assert.IsFalse(tooth22.IsPatientRight);
			Assert.IsTrue(tooth22.IsAnterior);
			Assert.AreEqual("Permanent mandibular left canine",tooth22.Name);
			ToothInfo toothK=ToothNumbers.GetInfo("K");
			Assert.IsTrue(toothK.IsPrimary);
			Assert.IsFalse(toothK.IsMaxillary);
			Assert.IsFalse(toothK.IsPatientRight);
			Assert.IsFalse(toothK.IsAnterior);
			Assert.AreEqual("Deciduous mandibular left second molar",toothK.Name);
			Assert.AreEqual("Deciduous maxillary right central incisor",ToothNumbers.GetInfo("E").Name);
			//Universal 1-8 and 25-32 are the patient's right, 9-24 the left (design 7.8 Image Laterality); A-E and P-T right, F-O left.
			foreach(string universal in AllUniversal()) {
				ToothInfo toothInfo=ToothNumbers.GetInfo(universal);
				int number;
				bool isRight;
				bool isUpper;
				bool isAnterior;
				if(int.TryParse(universal,out number)) {
					isRight=number<=8 || number>=25;
					isUpper=number<=16;
					isAnterior=(number>=6 && number<=11) || (number>=22 && number<=27);
				}
				else {
					int index=universal[0]-'A';
					isRight=index<=4 || index>=15;
					isUpper=index<=9;
					isAnterior=(index>=2 && index<=7) || (index>=12 && index<=17);
				}
				Assert.AreEqual(isRight,toothInfo.IsPatientRight,universal);
				Assert.AreEqual(isUpper,toothInfo.IsMaxillary,universal);
				Assert.AreEqual(isAnterior,toothInfo.IsAnterior,universal);
				Assert.AreEqual(Tooth.IsAnterior(universal),toothInfo.IsAnterior,"Open Dental's own rule, "+universal);
				Assert.IsTrue(toothInfo.Name.Length<=64 && toothInfo.Name.All(x => x>=' ' && x<='~'),"A DICOM LO value: "+toothInfo.Name);
			}
		}

		[TestMethod]
		public void TryParseList_StoredFormat() {
			List<ToothInfo> listTeeth;
			string error;
			Assert.IsTrue(ToothNumbers.TryParseList("2,3,30,31",out listTeeth,out error));
			CollectionAssert.AreEqual(new[] { "17","16","46","47" },listTeeth.Select(x => x.Iso).ToList());
			Assert.IsTrue(ToothNumbers.TryParseList(" 8 , 9,,9,a ",out listTeeth,out error));
			CollectionAssert.AreEqual(new[] { "8","9","A" },listTeeth.Select(x => x.Universal).ToList(),"Trimmed, blanks and repeats dropped.");
			Assert.IsTrue(ToothNumbers.TryParseList("",out listTeeth,out error));
			Assert.AreEqual(0,listTeeth.Count);
			Assert.IsTrue(ToothNumbers.TryParseList(null,out listTeeth,out error));
			Assert.AreEqual(0,listTeeth.Count);
			foreach(string toothNumbers in new[] { "2-5","2,51","3,AS","2;3","40" }) {
				Assert.IsFalse(ToothNumbers.TryParseList(toothNumbers,out listTeeth,out error),toothNumbers);
				Assert.AreEqual(0,listTeeth.Count);
				Assert.IsTrue(error.Length>0);
			}
		}
	}
}
