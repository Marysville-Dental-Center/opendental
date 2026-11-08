// Vendor follow-up from public documentation (next/vendorpub-1): the teeth of a lab case product. Open Dental's 26.2 database document says
// labcaseproduct.ToothRange is "Retrieved from procedure.ToothRange, formatted for display". The teeth come from the procedures of the appointment
// the lab case is attached to (else its planned appointment); what the user types is kept. Formatting needs no database; the procedures are read
// from the scratch database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262GlidewellTests {
	[TestClass]
	public class LabCaseProductTeethTests {
		///<summary>The stored entries (procedurelog.ToothNum or ToothRange) in the Universal numbering.</summary>
		private static string Format(params string[] arrayEntries) {
			return LabCaseProducts.FormatTeeth(arrayEntries,ToothNumberingNomenclature.Universal);
		}

		#region Formatting, no database
		[TestMethod]
		public void Format_OneToothProceduresAndRanges_AsTheChartShowsThem() {
			Assert.AreEqual("8",Format("8"),"A crown: its ToothNum.");
			Assert.AreEqual("3,8-10,12",Format("12","8","3","9","10"),"Tooth order; runs of three or more with a dash.");
			Assert.AreEqual("6-11",Format("6,7,8,9,10,11"),"A multi-tooth procedure: its ToothRange.");
			Assert.AreEqual("8,9",Format("8","8,9","9"),"Each tooth once; two teeth are not a run.");
			Assert.AreEqual("8,12",Format("12","8"));
			Assert.AreEqual("A-C",Format("C","A","B"),"Primary teeth.");
			Assert.AreEqual("4-6",Format("5"," 4 , 6 "),"Spaces in a stored range are ignored.");
		}

		[TestMethod]
		public void Format_LeavesOutWhatIsNotATooth() {
			Assert.AreEqual("",Format("",""),"An arch or quadrant procedure has neither a ToothNum nor a ToothRange.");
			Assert.AreEqual("",Format("U","UR","LL"),"Arch and quadrant codes are not teeth.");
			Assert.AreEqual("1",Format("99","0"," ","X","1,,33,50",null),"Invalid entries are left out.");
			Assert.AreEqual("",LabCaseProducts.FormatTeeth(null,ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",LabCaseProducts.FormatToothRange(null,ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",LabCaseProducts.FormatToothRange(new List<Procedure> { null },ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void Format_UsesTheOfficesToothNumbering() {
			string[] arrayEntries={ "8","9","10","12" };
			Assert.AreEqual("8-10,12",LabCaseProducts.FormatTeeth(arrayEntries,ToothNumberingNomenclature.Universal));
			Assert.AreEqual("11-22,24",LabCaseProducts.FormatTeeth(arrayEntries,ToothNumberingNomenclature.FDI),"FDI: 8=11, 9=21, 10=22, 12=24.");
			//The same as the Chart's own range display in that numbering.
			Assert.AreEqual(Tooth.DisplayRange("8,9,10,12",ToothNumberingNomenclature.FDI),LabCaseProducts.FormatTeeth(arrayEntries,ToothNumberingNomenclature.FDI));
			Assert.AreEqual("",Tooth.DisplayRange(" "),"The old overload returns blank without reading the preference.");
			Assert.AreEqual("",Tooth.DisplayRange(null));
		}

		[TestMethod]
		public void FillIfBlank_KeepsWhatTheUserTyped() {
			LabCaseProduct labCaseProduct=new LabCaseProduct();
			Assert.IsTrue(LabCaseProducts.FillToothRangeIfBlank(labCaseProduct," 8-10 "));
			Assert.AreEqual("8-10",labCaseProduct.ToothRange);
			labCaseProduct.ToothRange="UL bridge";
			Assert.IsFalse(LabCaseProducts.FillToothRangeIfBlank(labCaseProduct,"8-10"),"The manual override is kept.");
			Assert.AreEqual("UL bridge",labCaseProduct.ToothRange);
			LabCaseProduct labCaseProductBlank=new LabCaseProduct();
			Assert.IsFalse(LabCaseProducts.FillToothRangeIfBlank(labCaseProductBlank,""),"No procedures with teeth: nothing to fill.");
			Assert.AreEqual("",labCaseProductBlank.ToothRange);
			Assert.IsFalse(LabCaseProducts.FillToothRangeIfBlank(null,"8"));
		}
		#endregion Formatting, no database

		#region The procedures of the lab case, scratch database
		private static void InsertProc(long procNum,long aptNum,long plannedAptNum,string toothNum,string toothRange,ProcStat procStat) {
			DataCore.NonQ("INSERT INTO procedurelog (ProcNum,PatNum,AptNum,PlannedAptNum,ToothNum,ToothRange,Surf,ProcStatus) VALUES ("
				+POut.Long(procNum)+",1,"+POut.Long(aptNum)+","+POut.Long(plannedAptNum)+",'"+POut.String(toothNum)+"','"+POut.String(toothRange)+"','',"
				+POut.Int((int)procStat)+")");
		}

		///<summary>The office's tooth numbering, and the ICD version a new Procedure reads in its constructor.</summary>
		private static void SetToothNumbering(ToothNumberingNomenclature toothNumberingNomenclature) {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName IN ('UseInternationalToothNumbers','DxIcdVersion')");
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('UseInternationalToothNumbers','"+(int)toothNumberingNomenclature+"'),"
				+"('DxIcdVersion','10')");
			Prefs.RefreshCache();
		}

		[TestMethod]
		public void FormatToothRange_ReadsToothNumAndToothRangeOfEachProcedure() {
			GlidewellDb.Begin();
			SetToothNumbering(ToothNumberingNomenclature.Universal);
			List<Procedure> listProcedures=new List<Procedure> {
				new Procedure { ToothNum="12" },
				null,
				new Procedure { ToothRange="6,7,8" },
				new Procedure { Surf="U" },//An arch procedure.
				new Procedure { ToothNum="8",ToothRange="8,9" },
			};
			Assert.AreEqual("6-9,12",LabCaseProducts.FormatToothRange(listProcedures,ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",LabCaseProducts.FormatToothRange(new List<Procedure> { new Procedure { Surf="UR" } },ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void Procs_OfTheAppointment_ElseOfThePlannedAppointment_DeletedLeftOut() {
			GlidewellDb.Begin();
			//Appointment 900 (scheduled from planned appointment 901): teeth 8 and 9, a deleted procedure on 10, and 30 that is on both.
			InsertProc(1,900,0,"8","",ProcStat.TP);
			InsertProc(2,900,0,"9","",ProcStat.C);
			InsertProc(3,900,0,"10","",ProcStat.D);
			InsertProc(4,900,901,"30","",ProcStat.TP);
			InsertProc(5,0,901,"3","",ProcStat.TP);
			InsertProc(6,0,901,"","",ProcStat.TP);//No tooth (an exam on the planned appointment).
			InsertProc(7,950,0,"","5,6,7",ProcStat.TP);//Another lab case's appointment.
			SetToothNumbering(ToothNumberingNomenclature.Universal);
			CollectionAssert.AreEqual(new long[] { 1,2,4 },LabCaseProducts.GetProcsForLabCase(900,901).Select(x => x.ProcNum).ToList(),
				"The scheduled appointment's procedures; the deleted one left out.");
			CollectionAssert.AreEqual(new long[] { 4,5,6 },LabCaseProducts.GetProcsForLabCase(0,901).Select(x => x.ProcNum).ToList(),
				"Without a scheduled appointment, the planned one's.");
			Assert.AreEqual(0,LabCaseProducts.GetProcsForLabCase(0,0).Count,"Attached to neither.");
			Assert.AreEqual(0,LabCaseProducts.GetProcsForLabCase(902,901).Count,"An appointment without procedures: the planned one is not used.");
			try {
				Assert.AreEqual("8,9,30",LabCaseProducts.GetToothRangeFromProcs(900,901));
				Assert.AreEqual("3,30",LabCaseProducts.GetToothRangeFromProcs(0,901));
				Assert.AreEqual("5-7",LabCaseProducts.GetToothRangeFromProcs(950,0));
				Assert.AreEqual("",LabCaseProducts.GetToothRangeFromProcs(0,0));
				SetToothNumbering(ToothNumberingNomenclature.FDI);
				Assert.AreEqual("11,21,46",LabCaseProducts.GetToothRangeFromProcs(900,901),"The office's tooth numbering: FDI (30=46).");
			}
			finally {
				SetToothNumbering(ToothNumberingNomenclature.Universal);
			}
		}

		///<summary>The teeth are not written into an existing product by any save: the products are synced as the user left them.</summary>
		[TestMethod]
		public void Sync_SavesTheTeethAsTyped() {
			GlidewellDb.Begin();
			Patient patient=GlidewellDb.CreatePatient();
			Provider provider=GlidewellDb.CreateProvider("DOC1");
			LabCase labCase=GlidewellDb.CreateLabCase(patient,provider);
			InsertProc(1,900,0,"8","",ProcStat.TP);
			LabCaseProduct labCaseProduct=new LabCaseProduct { Material="BruxZir",ToothRange="8 and 9 (typed)" };
			List<LabCaseProduct> listSaved=LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct> { labCaseProduct });
			Assert.AreEqual("8 and 9 (typed)",LabCaseProducts.GetForLabCase(labCase.LabCaseNum).Single().ToothRange);
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,listSaved);
			Assert.AreEqual("8 and 9 (typed)",LabCaseProducts.GetForLabCase(labCase.LabCaseNum).Single().ToothRange);
		}
		#endregion The procedures of the lab case, scratch database

		#region Wiring of the windows
		private static string Read(string relativePath) {
			string path=Path.GetDirectoryName(typeof(LabCaseProductTeethTests).Assembly.Location);
			while(path!=null && !(File.Exists(Path.Combine(path,"LICENSE")) && File.Exists(Path.Combine(path,"NOTICE")))) {
				path=Path.GetDirectoryName(path);
			}
			Assert.IsNotNull(path,"Repository root not found.");
			return File.ReadAllText(Path.Combine(path,relativePath)).Replace("\r\n","\n");
		}

		[TestMethod]
		public void ProductWindow_FillsANewProductFromTheProcedures_FromProceduresButton() {
			string form=Read(@"OpenPmsDental\Forms\FormLabCaseProductEdit.cs");
			StringAssert.Contains(form,"public string ToothRangeFromProcs=\"\";");
			StringAssert.Contains(form,"if(IsNew) {\n\t\t\t\tLabCaseProducts.FillToothRangeIfBlank(LabCaseProductCur,ToothRangeFromProcs);\n\t\t\t}\n\t\t\ttextMaterial.Text",
				"Only a new product, before the boxes are filled.");
			StringAssert.Contains(form,"textToothRange.Text=ToothRangeFromProcs.Trim();");
			StringAssert.Contains(form,"LabCaseProductCur.ToothRange=textToothRange.Text.Trim();","What the user typed is saved.");
			string designer=Read(@"OpenPmsDental\Forms\FormLabCaseProductEdit.Designer.cs");
			StringAssert.Contains(designer,"this.butToothRangeFromProcs.Text = \"From Procedures\";");
			StringAssert.Contains(designer,"this.butToothRangeFromProcs.Click += new System.EventHandler(this.butToothRangeFromProcs_Click);");
			StringAssert.Contains(designer,"this.Controls.Add(this.butToothRangeFromProcs);");
			string partial=Read(@"OpenPmsDental\Forms\FormLabCaseEdit.Glidewell.cs");
			Assert.AreEqual(2,partial.Split(new[] { "formLabCaseProductEdit.ToothRangeFromProcs=GetGwToothRangeFromProcs();" },StringSplitOptions.None).Length-1,
				"Adding and editing a product.");
			StringAssert.Contains(partial,"return LabCaseProducts.GetToothRangeFromProcs(LabCaseCur.AptNum,LabCaseCur.PlannedAptNum);",
				"The appointments as they are on screen (Detach clears them).");
		}
		#endregion Wiring of the windows
	}
}
