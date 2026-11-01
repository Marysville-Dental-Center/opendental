// v25_3-medical-medlist-missingtab (Missing Generic/Brand tab removed) and v25_3-chart-vitalsign-notes (Notes box in Edit Vital Sign), package
// B-MED. The windows cannot be opened in a test run (the GUI is not launched), so the wiring tests read the window sources. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BMEDTests {
	[TestClass]
	public class MissingTabRemovedTests {
		[TestMethod]
		public void MedicationsWindow_HasNoMissingTab() {
			string source=BMedDb.ReadRepoFile("OpenDental","Forms","FormMedications.cs");
			string designer=BMedDb.ReadRepoFile("OpenDental","Forms","FormMedications.designer.cs");
			foreach(string name in new[] { "tabMissing","gridMissing","FillGridMissing","butConvertGeneric","butConvertBrand","GetAllMissingMedications",
				"tabMedications","tabAllMedications","Missing Generic/Brand" })
			{
				Assert.IsFalse(source.Contains(name),"FormMedications.cs still has "+name);
				Assert.IsFalse(designer.Contains(name),"FormMedications.designer.cs still has "+name);
			}
			//Any namespace before UI.TabControl, so the check still sees the control after the namespaces are renamed. A window that keeps its
			//tab control shows that the pattern finds one.
			Regex regexTabControl=new Regex(@"\bUI\.TabControl\b");
			Assert.IsTrue(regexTabControl.IsMatch(BMedDb.ReadRepoFile("OpenDental","Forms","FormPatientEdit.Designer.cs")));
			Assert.IsFalse(regexTabControl.IsMatch(designer),"The tab control is gone.");
		}

		[TestMethod]
		public void MedicationsWindow_KeepsTheListAndItsSearch() {
			string source=BMedDb.ReadRepoFile("OpenDental","Forms","FormMedications.cs");
			string designer=BMedDb.ReadRepoFile("OpenDental","Forms","FormMedications.designer.cs");
			foreach(string control in new[] { "gridAllMedications","textSearch","butAddGeneric","butAddBrand","butImportMedications","butExportMedications",
				"checkShowHidden","butOK" })
			{
				StringAssert.Contains(designer,"this.Controls.Add(this."+control+");","Placed on the window itself: "+control);
			}
			StringAssert.Contains(source,"Medications.GetListForPicker(textSearch.Text,includeHidden:isShowingHidden)");
			StringAssert.Contains(source,"_timer.Interval=500;","Results still refresh after a short pause in typing.");
			Assert.AreEqual(1,Regex.Matches(source,@"private void textSearch_TextChanged").Count);
		}
	}

	[TestClass]
	public class VitalsignNotesWiringTests {
		[TestMethod]
		public void EditWindow_LoadsAndSavesTheNotesBox() {
			string source=BMedDb.ReadRepoFile("OpenDental","Forms","FormVitalsignEdit2014.cs");
			string designer=BMedDb.ReadRepoFile("OpenDental","Forms","FormVitalsignEdit2014.Designer.cs");
			StringAssert.Contains(source,"textNotes.Text=VitalsignCur.Documentation??\"\";");
			StringAssert.Contains(source,"VitalsignCur.Documentation=textNotes.Text;");
			//Loaded before the early returns of the load method, saved before the insert or update.
			int indexLoad=source.IndexOf("textNotes.Text=VitalsignCur.Documentation");
			Assert.IsTrue(indexLoad<source.IndexOf("if(VitalsignCur.EhrNotPerformedNum<=0) {"));
			int indexSave=source.IndexOf("VitalsignCur.Documentation=textNotes.Text;");
			Assert.IsTrue(indexSave<source.IndexOf("Vitalsigns.Insert(VitalsignCur);"));
			StringAssert.Contains(designer,"this.textNotes.Multiline = true;");
			StringAssert.Contains(designer,"this.labelNotes.Text = \"Notes\";");
			StringAssert.Contains(designer,"this.Controls.Add(this.textNotes);");
		}

		[TestMethod]
		public void MedicalWindow_ShowsTheColumnAsNotes() {
			string source=BMedDb.ReadRepoFile("OpenDental","Forms","FormMedical.cs");
			StringAssert.Contains(source,"col=new GridColumn(\"Notes\",150);");
			Assert.IsFalse(source.Contains("Documentation for Followup or Ineligible"));
			StringAssert.Contains(source,"row.Cells.Add(_listVitalSigns[i].Documentation);","The column still shows vitalsign.Documentation.");
		}
	}

	[TestClass]
	public class VitalsignNotesDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BMedDb.Begin();
		}

		[TestMethod]
		public void Notes_AreStoredInTheDocumentationColumn() {
			Vitalsign vitalsign=new Vitalsign();
			vitalsign.PatNum=BMedDb.NextSyntheticNum();//synthetic patient number
			vitalsign.DateTaken=new DateTime(2026,10,1);
			vitalsign.Height=64;
			vitalsign.Weight=150;
			vitalsign.Documentation="Synthetic note: patient anxious, retake BP.\r\nSecond line.";
			vitalsign.WeightCode="";
			vitalsign.HeightExamCode="";
			vitalsign.WeightExamCode="";
			vitalsign.BMIExamCode="";
			Vitalsigns.Insert(vitalsign);
			Assert.AreEqual(vitalsign.Documentation,
				DataCore.GetScalar("SELECT Documentation FROM vitalsign WHERE VitalsignNum="+POut.Long(vitalsign.VitalsignNum)));
			Vitalsign vitalsignDb=Vitalsigns.GetOne(vitalsign.VitalsignNum);
			Assert.AreEqual(vitalsign.Documentation,vitalsignDb.Documentation,"Existing text shows in the Notes box.");
			vitalsignDb.Documentation="";
			Vitalsigns.Update(vitalsignDb);
			Assert.AreEqual("",Vitalsigns.Refresh(vitalsign.PatNum).Single().Documentation,"Clearing the box clears the column.");
		}
	}
}
