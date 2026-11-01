// v26_1-chart-ptinfo-gender: the Gender row of the Chart module's Patient Information grid.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2SHELLTests {
	[TestClass]
	public class ChartGenderTests {
		[TestMethod]
		public void Text_IsTheEditPatientText_AndUnknownIsBlank() {
			Assert.AreEqual("Male",PatientGenderDisplay.GetText(PatientGender.Male));
			Assert.AreEqual("Female",PatientGenderDisplay.GetText(PatientGender.Female));
			Assert.AreEqual("Other",PatientGenderDisplay.GetText(PatientGender.Other));
			Assert.AreEqual("",PatientGenderDisplay.GetText(PatientGender.Unknown));
			Assert.AreEqual("",PatientGenderDisplay.GetText((PatientGender)99),"A number this program does not know shows as blank, not as a number.");
		}

		//The display field itself (available, not on by default) is M2-02's and is tested in M202Tests (Registry26_1DbTests).

		[TestMethod]
		public void ChartModule_RendersTheRow() {
			string source=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlChart.cs");
			int start=source.IndexOf("public void FillPtInfo(");
			int end=source.IndexOf("public void FunctionKeyPressContrChart(",start);
			Assert.IsTrue(start>0 && end>start,"FillPtInfo not found.");
			string fillPtInfo=source.Substring(start,end-start);
			int idxCase=fillPtInfo.IndexOf("case DisplayFields26_1.ChartPatientInfoGender:");
			Assert.IsTrue(idxCase>0,"The patient information switch has a Gender case.");
			int idxBreak=fillPtInfo.IndexOf("break;",idxCase);
			string caseBody=fillPtInfo.Substring(idxCase,idxBreak-idxCase);
			StringAssert.Contains(caseBody,"PatientGenderDisplay.GetText(Pd.Patient.Gender)");
			Assert.IsFalse(caseBody.Contains("row.Tag="),"The row keeps a null tag, so a double click opens Edit Patient Information.");
		}

		[TestMethod]
		public void FamilyModule_UsesTheSameText() {
			//The spec: one rendering in both modules. The Family module's 24.3 row showed the raw enum name ("Unknown" for an unknown gender).
			string source=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlFamily.cs");
			int idxCase=source.IndexOf("case \"Gender\":");
			Assert.IsTrue(idxCase>0,"The Family patient information switch has a Gender case.");
			int idxBreak=source.IndexOf("break;",idxCase);
			string caseBody=source.Substring(idxCase,idxBreak-idxCase);
			StringAssert.Contains(caseBody,"PatientGenderDisplay.GetText(_patient.Gender)");
			Assert.IsFalse(caseBody.Contains("Gender.ToString()"),"The raw enum name is no longer shown.");
		}
	}
}
