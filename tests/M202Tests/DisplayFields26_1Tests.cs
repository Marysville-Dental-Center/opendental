// Display Fields lists for the Open Dental 25.4 and 26.1 display-field items.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M202Tests {
	[TestClass]
	public class DisplayFields26_1Tests {
		[TestMethod]
		public void ServiceDateView_DefaultColumnsMatchThe24_3Window() {
			List<DisplayField> listDefault=DisplayFields.GetDefaultList(DisplayFieldCategory.AccountModuleServiceDateView);
			CollectionAssert.AreEqual(new[] { "Service Date","Trans Date","Patient","Reference","Charge","Production","Prov","InsBal","AcctBal" },
				listDefault.Select(x => x.InternalName).ToList());
			Assert.IsTrue(listDefault.TrueForAll(x => x.ColumnWidth>0 && x.Category==DisplayFieldCategory.AccountModuleServiceDateView && x.Description==""));
		}

		[TestMethod]
		public void ServiceDateView_AvailableAddsClinic() {
			List<DisplayField> listAvailable=DisplayFields.GetAllAvailableList(DisplayFieldCategory.AccountModuleServiceDateView);
			CollectionAssert.AreEqual(DisplayFields26_1.GetServiceDateViewDefaultNames().Concat(new[] { "Clinic" }).ToList(),
				listAvailable.Select(x => x.InternalName).ToList());
		}

		[TestMethod]
		public void AppointmentBubble_OffersPreAuthFlagOnce() {
			List<DisplayField> listAvailable=DisplayFields.GetAllAvailableList(DisplayFieldCategory.AppointmentBubble);
			Assert.AreEqual(1,listAvailable.Count(x => x.InternalName==DisplayFields26_1.ApptBubblePreAuthFlag));
			//Not shown until an office adds it.
			Assert.IsFalse(DisplayFields.GetDefaultList(DisplayFieldCategory.AppointmentBubble).Any(x => x.InternalName==DisplayFields26_1.ApptBubblePreAuthFlag));
		}

		[TestMethod]
		public void ChartPatientInformation_AddsGenderOnce() {
			List<DisplayField> listDisplayFields=new List<DisplayField> { new DisplayField("Age",0,DisplayFieldCategory.ChartPatientInformation) };
			DisplayFields26_1.AddAvailableFields(DisplayFieldCategory.ChartPatientInformation,listDisplayFields);
			DisplayFields26_1.AddAvailableFields(DisplayFieldCategory.ChartPatientInformation,listDisplayFields);
			Assert.AreEqual(1,listDisplayFields.Count(x => x.InternalName=="Gender"));
			Assert.AreEqual(DisplayFieldCategory.ChartPatientInformation,listDisplayFields.Single(x => x.InternalName=="Gender").Category);
		}

		[TestMethod]
		public void TreatmentPlan_CatPercColumnSaysStandardFee() {
			DisplayField displayField=DisplayFields.GetAllAvailableList(DisplayFieldCategory.TreatmentPlanModule)
				.Single(x => x.InternalName==DisplayFields.InternalNames.TreatmentPlanModule.CatPercUCR);
			Assert.AreEqual("Cat% UCR",displayField.InternalName,"Stored rows use the internal name; it must not change.");
			Assert.AreEqual(DisplayFields26_1.TreatPlanCatPercStdFeeText,displayField.Description);
			Assert.IsFalse(displayField.Description.Contains("UCR"));
			//A row the office saved before (no description) shows the new wording through GetColumnText; a description the user typed wins.
			DisplayField displayFieldSaved=new DisplayField(DisplayFields.InternalNames.TreatmentPlanModule.CatPercUCR,65,DisplayFieldCategory.TreatmentPlanModule);
			Assert.AreEqual(DisplayFields26_1.TreatPlanCatPercStdFeeText,DisplayFields26_1.GetColumnText(displayFieldSaved));
			displayFieldSaved.Description="Cat %";
			Assert.AreEqual("Cat %",DisplayFields26_1.GetColumnText(displayFieldSaved));
			Assert.AreEqual("Fee",DisplayFields26_1.GetColumnText(new DisplayField("Fee",50,DisplayFieldCategory.TreatmentPlanModule)));
		}

		[TestMethod]
		public void DefaultHook_LeavesFilledListsAlone() {
			List<DisplayField> listDisplayFields=new List<DisplayField> { new DisplayField("Charge",80,DisplayFieldCategory.AccountModuleServiceDateView) };
			DisplayFields26_1.AddDefaultFields(DisplayFieldCategory.AccountModuleServiceDateView,listDisplayFields);
			Assert.AreEqual(1,listDisplayFields.Count,"Another change already built the list.");
			List<DisplayField> listOther=new List<DisplayField>();
			DisplayFields26_1.AddDefaultFields(DisplayFieldCategory.ERA,listOther);
			Assert.AreEqual(0,listOther.Count,"Other categories are untouched.");
		}

		[TestMethod]
		public void DisplayFields_CallsTheHooksAfterItsSwitch() {
			string source=File.ReadAllText(TestPaths.GetRepoFile("OpenDentBusiness","Data Interface","DisplayFields.cs"));
			Assert.AreEqual(1,CountOf(source,"DisplayFields26_1.AddDefaultFields(displayFieldCategory,listDisplayFields);"));
			Assert.AreEqual(1,CountOf(source,"DisplayFields26_1.AddAvailableFields(displayFieldCategory,listDisplayFields);"));
		}

		private static int CountOf(string text,string value) {
			int count=0;
			int idx=text.IndexOf(value,StringComparison.Ordinal);
			while(idx>=0) {
				count++;
				idx=text.IndexOf(value,idx+value.Length,StringComparison.Ordinal);
			}
			return count;
		}
	}
}
