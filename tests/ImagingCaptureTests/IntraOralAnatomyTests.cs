// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Dicom;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>Anatomy and Patient Orientation of an intra-oral image from its slot (design 7.8, 7.10).</summary>
	[TestClass]
	public class IntraOralAnatomyTests {
		///<summary>Every slot of the standard templates Open Dental generates (FormMountDefGenerate: FMX with horizontal or vertical
		///bitewings, 4BW horizontal and vertical), as (template, ItemOrder, ToothNumbers, RotateOnAcquire), with the expected Patient
		///Orientation of the stored image without and with Mount.FlipOnAcquire, and the expected Image Laterality. The orientation is the
		///slot's labial-mount view as displayed (anterior L\F, right posterior A\F, left posterior P\F) with the rotation and mirror undone.
		///The phantom check of design 13 step 4 confirms these before 16-bit is enabled for a device.</summary>
		private static readonly object[][] _arrayTemplateSlots={
			//FMX, horizontal bitewings
			new object[] { "FMX horiz",1,"2,3,30,31",0,@"A\F",@"P\F","R" },
			new object[] { "FMX horiz",2,"4,5,28,29",0,@"A\F",@"P\F","R" },
			new object[] { "FMX horiz",3,"14,15,18,19",180,@"A\H",@"P\H","L" },
			new object[] { "FMX horiz",4,"12,13,20,21",180,@"A\H",@"P\H","L" },
			new object[] { "FMX horiz",5,"2,3",0,@"A\F",@"P\F","R" },
			new object[] { "FMX horiz",6,"4,5",0,@"A\F",@"P\F","R" },
			new object[] { "FMX horiz",7,"18,19",180,@"A\H",@"P\H","L" },
			new object[] { "FMX horiz",8,"20,21",180,@"A\H",@"P\H","L" },
			new object[] { "FMX horiz",9,"30,31",0,@"A\F",@"P\F","R" },
			new object[] { "FMX horiz",10,"28,29",0,@"A\F",@"P\F","R" },
			new object[] { "FMX horiz",11,"14,15",180,@"A\H",@"P\H","L" },
			new object[] { "FMX horiz",12,"12,13",180,@"A\H",@"P\H","L" },
			new object[] { "FMX horiz",13,"6,7",90,@"F\R",@"H\R","R" },
			new object[] { "FMX horiz",14,"8,9",90,@"F\R",@"H\R","B" },
			new object[] { "FMX horiz",15,"10,11",90,@"F\R",@"H\R","L" },
			new object[] { "FMX horiz",16,"26,27",270,@"H\L",@"F\L","R" },
			new object[] { "FMX horiz",17,"24,25",270,@"H\L",@"F\L","B" },
			new object[] { "FMX horiz",18,"22,23",270,@"H\L",@"F\L","L" },
			//FMX, vertical bitewings: only the bitewings differ
			new object[] { "FMX vert",1,"2,3,30,31",90,@"F\P",@"H\P","R" },
			new object[] { "FMX vert",2,"4,5,28,29",90,@"F\P",@"H\P","R" },
			new object[] { "FMX vert",3,"14,15,18,19",90,@"F\A",@"H\A","L" },
			new object[] { "FMX vert",4,"12,13,20,21",90,@"F\A",@"H\A","L" },
			new object[] { "FMX vert",5,"2,3",0,@"A\F",@"P\F","R" },
			new object[] { "FMX vert",6,"4,5",0,@"A\F",@"P\F","R" },
			new object[] { "FMX vert",7,"18,19",180,@"A\H",@"P\H","L" },
			new object[] { "FMX vert",8,"20,21",180,@"A\H",@"P\H","L" },
			new object[] { "FMX vert",9,"30,31",0,@"A\F",@"P\F","R" },
			new object[] { "FMX vert",10,"28,29",0,@"A\F",@"P\F","R" },
			new object[] { "FMX vert",11,"14,15",180,@"A\H",@"P\H","L" },
			new object[] { "FMX vert",12,"12,13",180,@"A\H",@"P\H","L" },
			new object[] { "FMX vert",13,"6,7",90,@"F\R",@"H\R","R" },
			new object[] { "FMX vert",14,"8,9",90,@"F\R",@"H\R","B" },
			new object[] { "FMX vert",15,"10,11",90,@"F\R",@"H\R","L" },
			new object[] { "FMX vert",16,"26,27",270,@"H\L",@"F\L","R" },
			new object[] { "FMX vert",17,"24,25",270,@"H\L",@"F\L","B" },
			new object[] { "FMX vert",18,"22,23",270,@"H\L",@"F\L","L" },
			//4BW, horizontal and vertical
			new object[] { "4BW horiz",1,"2,3,30,31",0,@"A\F",@"P\F","R" },
			new object[] { "4BW horiz",2,"4,5,28,29",0,@"A\F",@"P\F","R" },
			new object[] { "4BW horiz",3,"14,15,18,19",180,@"A\H",@"P\H","L" },
			new object[] { "4BW horiz",4,"12,13,20,21",180,@"A\H",@"P\H","L" },
			new object[] { "4BW vert",1,"2,3,30,31",0,@"A\F",@"P\F","R" },
			new object[] { "4BW vert",2,"4,5,28,29",0,@"A\F",@"P\F","R" },
			new object[] { "4BW vert",3,"14,15,18,19",180,@"A\H",@"P\H","L" },
			new object[] { "4BW vert",4,"12,13,20,21",180,@"A\H",@"P\H","L" },
		};

		[TestMethod]
		public void PatientOrientation_PinnedForEveryStandardTemplateSlot() {
			List<string> listFailures=new List<string>();
			foreach(object[] slot in _arrayTemplateSlots) {
				foreach(bool isFlipped in new[] { false,true }) {
					IntraOralTarget target=new IntraOralTarget { HasMountSlot=true,ToothNumbers=(string)slot[2],DegreesRotated=(int)slot[3],IsFlipped=isFlipped };
					string error;
					IntraOralAnatomy anatomy=IntraOralAnatomy.FromTarget(target,out error);
					string label=slot[0]+" slot "+slot[1]+(isFlipped ? " flipped" : "");
					if(anatomy==null) {
						listFailures.Add(label+": "+error);
						continue;
					}
					string expected=(string)(isFlipped ? slot[5] : slot[4]);
					if(anatomy.PatientOrientation!=expected) {
						listFailures.Add(label+": "+anatomy.PatientOrientation+", expected "+expected);
					}
					if(anatomy.ImageLaterality!=(string)slot[6]) {
						listFailures.Add(label+": laterality "+anatomy.ImageLaterality+", expected "+slot[6]);
					}
				}
			}
			Assert.AreEqual(0,listFailures.Count,string.Join("; ",listFailures));
			Assert.AreEqual(18+18+4+4,_arrayTemplateSlots.Length);
		}

		[TestMethod]
		public void PatientOrientation_UndoesRotationThenMirror() {
			//As displayed (rotation 0, no mirror) each view shows its own directions.
			Assert.AreEqual(@"L\F",IntraOralAnatomy.GetPatientOrientation(IntraOralView.Anterior,0,false));
			Assert.AreEqual(@"A\F",IntraOralAnatomy.GetPatientOrientation(IntraOralView.RightPosterior,0,false));
			Assert.AreEqual(@"P\F",IntraOralAnatomy.GetPatientOrientation(IntraOralView.LeftPosterior,0,false));
			//Anterior, each rotation, without and with the mirror (the display mirrors first, then turns clockwise).
			string[,] arrayExpected={ { @"L\F",@"R\F" },{ @"F\R",@"H\R" },{ @"R\H",@"L\H" },{ @"H\L",@"F\L" } };
			for(int i=0;i<4;i++) {
				Assert.AreEqual(arrayExpected[i,0],IntraOralAnatomy.GetPatientOrientation(IntraOralView.Anterior,i*90,false),"rotate "+(i*90));
				Assert.AreEqual(arrayExpected[i,1],IntraOralAnatomy.GetPatientOrientation(IntraOralView.Anterior,i*90,true),"rotate "+(i*90)+" flipped");
			}
			foreach(IntraOralView view in Enum.GetValues(typeof(IntraOralView))) {
				foreach(bool isFlipped in new[] { false,true }) {
					for(int degrees=0;degrees<360;degrees+=90) {
						string orientation=IntraOralAnatomy.GetPatientOrientation(view,degrees,isFlipped);
						Assert.AreEqual(orientation,IntraOralAnatomy.GetPatientOrientation(view,degrees+360,isFlipped));
						Assert.AreEqual(orientation,IntraOralAnatomy.GetPatientOrientation(view,degrees-360,isFlipped));
						//Rows and columns are perpendicular: one is H or F, the other is not.
						string[] arrayParts=orientation.Split('\\');
						Assert.AreEqual(2,arrayParts.Length);
						Assert.AreNotEqual(arrayParts[0]=="H" || arrayParts[0]=="F",arrayParts[1]=="H" || arrayParts[1]=="F",orientation);
					}
				}
			}
			Assert.IsNull(IntraOralAnatomy.GetPatientOrientation(IntraOralView.Anterior,45,false));
		}

		[TestMethod]
		public void FromTarget_RegionLateralityAndToothCodes() {
			string error;
			IntraOralAnatomy anatomyBitewing=IntraOralAnatomy.FromTarget(Target("2,3,30,31",0),out error);
			Assert.AreEqual(IntraOralView.RightPosterior,anatomyBitewing.View);
			Assert.AreEqual("661005",anatomyBitewing.AnatomicRegion.CodeValue,"Both jaws.");
			Assert.AreEqual("SCT",anatomyBitewing.AnatomicRegion.CodingSchemeDesignator);
			CollectionAssert.AreEqual(new[] { "17","16","46","47" },anatomyBitewing.ListToothCodes.Select(x => x.CodeValue).ToList());
			Assert.IsTrue(anatomyBitewing.ListToothCodes.All(x => x.CodingSchemeDesignator=="ISO3950"));
			Assert.AreEqual("Permanent maxillary right second molar",anatomyBitewing.ListToothCodes[0].CodeMeaning);
			Assert.AreEqual("70925003",IntraOralAnatomy.FromTarget(Target("14,15",180),out error).AnatomicRegion.CodeValue,"Maxilla.");
			Assert.AreEqual("91609006",IntraOralAnatomy.FromTarget(Target("24,25",270),out error).AnatomicRegion.CodeValue,"Mandible.");
			//Primary teeth: anterior C-H, posterior A-B on the right.
			IntraOralAnatomy anatomyPrimary=IntraOralAnatomy.FromTarget(Target("E,F",90),out error);
			Assert.AreEqual(IntraOralView.Anterior,anatomyPrimary.View);
			Assert.AreEqual("B",anatomyPrimary.ImageLaterality);
			CollectionAssert.AreEqual(new[] { "51","61" },anatomyPrimary.ListToothCodes.Select(x => x.CodeValue).ToList());
			Assert.AreEqual(IntraOralView.RightPosterior,IntraOralAnatomy.FromTarget(Target("A,B,S,T",0),out error).View);
			Assert.AreEqual(IntraOralView.LeftPosterior,IntraOralAnatomy.FromTarget(Target("I,J,K,L",0),out error).View);
		}

		[TestMethod]
		public void FromTarget_RefusesWhatHasNoKnownOrientation() {
			string error;
			Assert.IsNull(IntraOralAnatomy.FromTarget(null,out error));
			StringAssert.Contains(error,"no mount slot");
			Assert.IsNull(IntraOralAnatomy.FromTarget(new IntraOralTarget { HasMountSlot=false,ToothNumbers="8,9" },out error));
			StringAssert.Contains(error,"no mount slot");
			foreach(string teeth in new[] { null,""," ",", ," }) {
				Assert.IsNull(IntraOralAnatomy.FromTarget(Target(teeth,0),out error));
				StringAssert.Contains(error,"no tooth numbers");
			}
			Assert.IsNull(IntraOralAnatomy.FromTarget(Target("2,51",0),out error),"Supernumerary teeth have no ISO 3950 code.");
			StringAssert.Contains(error,"cannot be coded");
			Assert.IsNull(IntraOralAnatomy.FromTarget(Target("5,6",0),out error),"A premolar and a canine: no single view.");
			StringAssert.Contains(error,"no single view");
			Assert.IsNull(IntraOralAnatomy.FromTarget(Target("3,14",0),out error),"Posterior teeth of both sides.");
			Assert.IsNull(IntraOralAnatomy.FromTarget(Target("8,9",45),out error));
			StringAssert.Contains(error,"rotation");
			Assert.IsNotNull(IntraOralAnatomy.FromTarget(Target("8,9",-90),out error),"-90 is 270.");
			Assert.AreEqual(@"H\L",IntraOralAnatomy.FromTarget(Target("8,9",-90),out error).PatientOrientation);
		}

		private static IntraOralTarget Target(string teeth,int rotate) {
			return new IntraOralTarget { HasMountSlot=true,ToothNumbers=teeth,DegreesRotated=rotate };
		}
	}
}
