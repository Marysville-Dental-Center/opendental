// What the user types in the Add Procedures window (teeth, surfaces, quadrant, arch, sextant) turned into procedures per treatment area, and the
// target plan rules. No database: the codes are built in memory.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BTPTests {
	[TestClass]
	public class PlacementTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static ProcedureCode Code(TreatmentArea treatmentArea,bool isAlsoToothRange=false) {
			return new ProcedureCode { CodeNum=1,ProcCode="Z9001",TreatArea=treatmentArea,AreaAlsoToothRange=isAlsoToothRange };
		}

		private static List<ProcAddPlacement> Place(ProcedureCode procedureCode,string toothEntry,string surfEntry,out string error,
			ToothNumberingNomenclature toothNumberingNomenclature=ToothNumberingNomenclature.Universal)
		{
			List<ProcAddPlacement> listProcAddPlacements=ProcAddHelper.GetPlacements(procedureCode,toothEntry,surfEntry,toothNumberingNomenclature,out error);
			if(error!="") {
				Assert.AreEqual(0,listProcAddPlacements.Count,"An error never comes with procedures to create.");
			}
			return listProcAddPlacements;
		}

		private static void AssertError(ProcedureCode procedureCode,string toothEntry,string surfEntry,string expectedPart,
			ToothNumberingNomenclature toothNumberingNomenclature=ToothNumberingNomenclature.Universal)
		{
			Place(procedureCode,toothEntry,surfEntry,out string error,toothNumberingNomenclature);
			Assert.AreNotEqual("",error,"Expected an error for tooth '"+toothEntry+"' and area '"+surfEntry+"'.");
			StringAssert.Contains(error,expectedPart);
		}

		[TestMethod]
		public void Mouth_OneProcedureAndNoTeeth() {
			foreach(TreatmentArea treatmentArea in new[] { TreatmentArea.Mouth,TreatmentArea.None }) {
				List<ProcAddPlacement> list=Place(Code(treatmentArea),"","",out string error);
				Assert.AreEqual("",error);
				Assert.AreEqual(1,list.Count);
				Assert.AreEqual("",list[0].ToothNum+list[0].Surf+list[0].ToothRange);
				AssertError(Code(treatmentArea),"3","","whole mouth");
				AssertError(Code(treatmentArea),"","UR","whole mouth");
			}
		}

		[TestMethod]
		public void Tooth_OneProcedurePerTooth() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Tooth),"3","",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("3",list.Single().ToothNum);
			list=Place(Code(TreatmentArea.Tooth),"3, 5","",out error);
			CollectionAssert.AreEqual(new[] { "3","5" },list.Select(x => x.ToothNum).ToArray());
			list=Place(Code(TreatmentArea.Tooth),"3-5","",out error);
			CollectionAssert.AreEqual(new[] { "3","4","5" },list.Select(x => x.ToothNum).ToArray());
			list=Place(Code(TreatmentArea.Tooth),"a","",out error);
			Assert.AreEqual("A",list.Single().ToothNum,"Primary teeth are typed in either case.");
			Assert.IsTrue(list.All(x => x.Surf=="" && x.ToothRange==""));
		}

		[TestMethod]
		public void Tooth_InvalidOrMissingIsRefused() {
			AssertError(Code(TreatmentArea.Tooth),"","","Enter the tooth");
			AssertError(Code(TreatmentArea.Tooth),"33","","not a valid tooth");
			AssertError(Code(TreatmentArea.Tooth),"3x","","not a valid tooth");
			AssertError(Code(TreatmentArea.Tooth),"3","MO","tooth only");
		}

		[TestMethod]
		public void Tooth_FdiNumbersAreStoredAsUniversal() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Tooth),"11,48","",out string error,ToothNumberingNomenclature.FDI);
			Assert.AreEqual("",error);
			CollectionAssert.AreEqual(new[] { "8","32" },list.Select(x => x.ToothNum).ToArray());
			AssertError(Code(TreatmentArea.Tooth),"3","","not a valid tooth",ToothNumberingNomenclature.FDI);
		}

		[TestMethod]
		public void Surf_ToothAndSurfacesTidied() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Surf),"30","dmo",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("30",list.Single().ToothNum);
			Assert.AreEqual("MOD",list.Single().Surf,"Surfaces are stored in the usual order.");
			list=Place(Code(TreatmentArea.Surf),"8","MIF",out error);
			Assert.AreEqual("MIF",list.Single().Surf);
			list=Place(Code(TreatmentArea.Surf),"2,3","MO",out error);
			Assert.AreEqual(2,list.Count,"One procedure per tooth.");
			Assert.IsTrue(list.All(x => x.Surf=="MO"));
		}

		[TestMethod]
		public void Surf_LettersThatDoNotFitTheToothAreRefused() {
			AssertError(Code(TreatmentArea.Surf),"8","MO","O");//occlusal on an anterior tooth
			AssertError(Code(TreatmentArea.Surf),"30","MI","I");//incisal on a posterior tooth
			AssertError(Code(TreatmentArea.Surf),"30","MX","X");
			AssertError(Code(TreatmentArea.Surf),"3,8","MO","tooth 8");//the second tooth does not fit
		}

		[TestMethod]
		public void Surf_MissingToothOrSurfacesIsRefused() {
			AssertError(Code(TreatmentArea.Surf),"","MO","Enter the tooth");
			AssertError(Code(TreatmentArea.Surf),"30","","Enter the surfaces");
		}

		[TestMethod]
		public void Quad_TypedOrFromTeeth() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Quad),"","ur",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("UR",list.Single().Surf);
			Assert.AreEqual("",list.Single().ToothNum+list.Single().ToothRange);
			list=Place(Code(TreatmentArea.Quad),"14,3,30","",out error);
			CollectionAssert.AreEqual(new[] { "UR","UL","LR" },list.Select(x => x.Surf).ToArray(),"One procedure per quadrant, as Chart entry.");
			Assert.IsTrue(list.All(x => x.ToothRange==""),"Without the tooth range option the teeth only pick the quadrants.");
			AssertError(Code(TreatmentArea.Quad),"","","Enter the quadrant");
			AssertError(Code(TreatmentArea.Quad),"","UX","UR, UL, LR or LL");
			AssertError(Code(TreatmentArea.Quad),"3","UR","not both");
		}

		[TestMethod]
		public void Quad_AlsoToothRange() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Quad,isAlsoToothRange:true),"2,3","UR",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("UR",list.Single().Surf);
			Assert.AreEqual("2,3",list.Single().ToothRange);
			list=Place(Code(TreatmentArea.Quad,isAlsoToothRange:true),"2,14,15","",out error);
			Assert.AreEqual(2,list.Count);
			Assert.AreEqual("UR",list[0].Surf);
			Assert.AreEqual("2",list[0].ToothRange);
			Assert.AreEqual("UL",list[1].Surf);
			Assert.AreEqual("14,15",list[1].ToothRange);
			AssertError(Code(TreatmentArea.Quad,isAlsoToothRange:true),"14","UR","not in UR");
			list=Place(Code(TreatmentArea.Quad,isAlsoToothRange:true),"","LL",out error);
			Assert.AreEqual("",list.Single().ToothRange,"The tooth range stays optional.");
		}

		[TestMethod]
		public void Arch_TypedOrFromTeeth() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Arch),"","l",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("L",list.Single().Surf);
			list=Place(Code(TreatmentArea.Arch),"19,3","",out error);
			CollectionAssert.AreEqual(new[] { "U","L" },list.Select(x => x.Surf).ToArray());
			list=Place(Code(TreatmentArea.Arch,isAlsoToothRange:true),"19,20","L",out error);
			Assert.AreEqual("19,20",list.Single().ToothRange);
			AssertError(Code(TreatmentArea.Arch,isAlsoToothRange:true),"3","L","not in L");
			AssertError(Code(TreatmentArea.Arch),"","X","U or L");
			AssertError(Code(TreatmentArea.Arch),"","","Enter the arch");
			AssertError(Code(TreatmentArea.Arch),"3","U","not both");
		}

		[TestMethod]
		public void Sextant_OneToSix() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.Sextant),"","3",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("3",list.Single().Surf);
			AssertError(Code(TreatmentArea.Sextant),"","7","sextant");
			AssertError(Code(TreatmentArea.Sextant),"","","sextant");
			AssertError(Code(TreatmentArea.Sextant),"3","2","not teeth");
			list=Place(Code(TreatmentArea.Sextant),"","05",out error,ToothNumberingNomenclature.FDI);
			Assert.AreEqual("3",list.Single().Surf,"FDI shows sextants as 03 to 08.");
			AssertError(Code(TreatmentArea.Sextant),"","05","sextant");
		}

		[TestMethod]
		public void ToothRange_OneProcedureForAllTeeth() {
			List<ProcAddPlacement> list=Place(Code(TreatmentArea.ToothRange),"6-11","",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("6,7,8,9,10,11",list.Single().ToothRange);
			Assert.AreEqual("",list.Single().ToothNum);
			AssertError(Code(TreatmentArea.ToothRange),"","","Enter the teeth");
			AssertError(Code(TreatmentArea.ToothRange),"6-11","U","tooth range only");
		}

		[TestMethod]
		public void NoCode_IsRefused() {
			ProcAddHelper.GetPlacements(null,"3","",ToothNumberingNomenclature.Universal,out string error);
			StringAssert.Contains(error,"not found");
		}

		[TestMethod]
		public void ParseSextant() {
			Assert.AreEqual("1",ProcAddHelper.ParseSextant(" 1 ",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("6",ProcAddHelper.ParseSextant("08",ToothNumberingNomenclature.FDI));
			Assert.AreEqual("6",ProcAddHelper.ParseSextant("6",ToothNumberingNomenclature.FDI));
			Assert.AreEqual("",ProcAddHelper.ParseSextant("0",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",ProcAddHelper.ParseSextant(null,ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void AreaText_And_SurfEntryUse() {
			foreach(TreatmentArea treatmentArea in System.Enum.GetValues(typeof(TreatmentArea))) {
				Assert.AreNotEqual("",ProcAddHelper.GetTreatAreaText(treatmentArea));
			}
			Assert.IsTrue(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.Surf)));
			Assert.IsTrue(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.Quad)));
			Assert.IsTrue(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.Sextant)));
			Assert.IsTrue(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.Arch)));
			Assert.IsFalse(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.Tooth)));
			Assert.IsFalse(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.Mouth)));
			Assert.IsFalse(ProcAddHelper.UsesSurfEntry(Code(TreatmentArea.ToothRange)));
			Assert.IsFalse(ProcAddHelper.UsesSurfEntry(null));
		}

		[TestMethod]
		public void OnlyTheActivePlanCanBeTargeted() {
			Assert.IsTrue(TreatPlans.CanAddProcsTo(new TreatPlan { TPStatus=TreatPlanStatus.Active }));
			Assert.IsFalse(TreatPlans.CanAddProcsTo(new TreatPlan { TPStatus=TreatPlanStatus.Inactive }));
			Assert.IsFalse(TreatPlans.CanAddProcsTo(new TreatPlan { TPStatus=TreatPlanStatus.Saved }));
			Assert.IsFalse(TreatPlans.CanAddProcsTo(null));
			Assert.AreEqual("",ProcAddHelper.GetTargetError(new TreatPlan { PatNum=5,TPStatus=TreatPlanStatus.Active },5));
			StringAssert.Contains(ProcAddHelper.GetTargetError(new TreatPlan { PatNum=5,TPStatus=TreatPlanStatus.Inactive },5),"Active");
			StringAssert.Contains(ProcAddHelper.GetTargetError(new TreatPlan { PatNum=5,TPStatus=TreatPlanStatus.Saved },5),"Active");
			StringAssert.Contains(ProcAddHelper.GetTargetError(new TreatPlan { PatNum=6,TPStatus=TreatPlanStatus.Active },5),"another patient");
			StringAssert.Contains(ProcAddHelper.GetTargetError(null,5),"no longer exists");
		}

		[TestMethod]
		public void BrokenAppointmentCodes() {
			Assert.IsTrue(ProcAddHelper.IsBrokenApptCode("D9986"));
			Assert.IsTrue(ProcAddHelper.IsBrokenApptCode("D9987"));
			Assert.IsFalse(ProcAddHelper.IsBrokenApptCode("Z9001"));
		}
	}
}
