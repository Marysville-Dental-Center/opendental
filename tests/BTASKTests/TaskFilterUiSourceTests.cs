// Source checks for the Task Filter window and the task list. FrmTaskFilter is a WPF window and UserControlTasks a WinForms control that these
// tests cannot open (the desktop client is never launched by tests), so they check the code that decides what is shown instead.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BTASKTests {
	[TestClass]
	public class TaskFilterUiSourceTests {
		private static string GetRepoRoot() {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir.FullName,"LICENSE")) && File.Exists(Path.Combine(dir.FullName,"NOTICE"))) {
					return dir.FullName;
				}
				dir=dir.Parent;
			}
			throw new DirectoryNotFoundException("The repository root (LICENSE and NOTICE) was not found above the test folder.");
		}

		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { GetRepoRoot() }.Concat(arrayParts).ToArray()));
		}

		private static string GetFrmCode() {
			return ReadRepoFile("WpfControlsOD","Frms","FrmTaskFilter.xaml.cs");
		}

		///<summary>The body of one method of the code, from its signature to the next method signature.</summary>
		private static string GetMethod(string code,string signature) {
			int start=code.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Method not found: "+signature);
			int end=code.IndexOf("\t\tprivate void ",start+signature.Length,StringComparison.Ordinal);
			if(end<0) {
				end=code.Length;
			}
			return code.Substring(start,end-start);
		}

		[TestMethod]
		public void SpecificPatient_IsShown() {
			string code=GetFrmCode();
			Assert.IsFalse(code.Contains("radioSpecificPatient.Visible=false"),"Specific Patient is no longer hidden.");
			Assert.IsFalse(code.Contains("butSelectPatient.Visible=false"),"The ... button is no longer hidden.");
			StringAssert.Contains(code,"radioSpecificPatient.Click+=RadioSpecificPatient_Click;");
			string xaml=ReadRepoFile("WpfControlsOD","Frms","FrmTaskFilter.xaml");
			StringAssert.Contains(xaml,"x:Name=\"butSelectPatient\"");
			StringAssert.Contains(xaml,"Click=\"butSelectPatient_Click\"");
			Assert.IsFalse(xaml.Contains("x:Name=\"radioSpecificPatient\" Visibility="),"Not hidden in the layout either.");
		}

		[TestMethod]
		public void SelectPatientButton_OpensSelectPatient_AndShowsTheChoice() {
			string code=GetFrmCode();
			StringAssert.Contains(GetMethod(code,"private void butSelectPatient_Click("),"SelectSpecificPatient();");
			string select=GetMethod(code,"private void SelectSpecificPatient(");
			StringAssert.Contains(select,"new FrmPatientSelect()");
			StringAssert.Contains(select,"frmPatientSelect.IsDialogCancel");
			StringAssert.Contains(select,"Patients.GetPat(frmPatientSelect.PatNumSelected)");
			StringAssert.Contains(select,"radioSpecificPatient.Checked=true;");
			StringAssert.Contains(select,"textPatientSelected.Text=TaskFilters.GetPatientText(_patientSpecific);","Shown as Last, First - PatNum.");
			string radio=GetMethod(code,"private void RadioSpecificPatient_Click(");
			StringAssert.Contains(radio,"SelectSpecificPatient();","Choosing Specific Patient with no patient yet opens Select Patient.");
		}

		[TestMethod]
		public void Ok_RequiresAPatient_ForSpecificPatient() {
			string ok=GetMethod(GetFrmCode(),"private void butOK_Click(");
			int check=ok.IndexOf("if(radioSpecificPatient.Checked && _patientSpecific==null) {",StringComparison.Ordinal);
			Assert.IsTrue(check>=0,"OK checks that a patient was picked.");
			Assert.IsTrue(ok.IndexOf("MsgBox.Show(",check,StringComparison.Ordinal)>check,"and says so.");
			Assert.IsTrue(check<ok.IndexOf("IsDialogOK=true;",StringComparison.Ordinal),"before the window closes.");
			StringAssert.Contains(ok,"EnumTaskPatientFilterType_=EnumTaskPatientFilterType.SpecificPatient;");
			StringAssert.Contains(ok,"PatientSelected=_patientSpecific;");
		}

		[TestMethod]
		public void ClinicAndRegionLists_OfferUnassigned_First() {
			string code=GetFrmCode();
			string clinics=GetMethod(code,"private void FillComboBoxClinics(");
			StringAssert.Contains(clinics,"listClinics.Add(TaskFilters.GetUnassignedClinic());");
			Assert.IsTrue(clinics.IndexOf("TaskFilters.GetUnassignedClinic()",StringComparison.Ordinal)
				<clinics.IndexOf("Clinics.GetAllForUserod(",StringComparison.Ordinal),"Unassigned comes right after All, before the clinics.");
			string regions=GetMethod(code,"private void FillComboBoxRegions(");
			StringAssert.Contains(regions,"listDefsRegions.Add(TaskFilters.GetUnassignedRegion());");
			Assert.IsTrue(regions.IndexOf("TaskFilters.GetUnassignedRegion()",StringComparison.Ordinal)
				<regions.IndexOf("Defs.GetDefs(DefCat.Regions",StringComparison.Ordinal),"Unassigned comes right after All, before the regions.");
			StringAssert.Contains(code,"comboClinic.GetListSelected<Clinic>().Select(x=>x.ClinicNum)","The Unassigned key reaches the filter list.");
			StringAssert.Contains(code,"comboRegion.GetListSelected<Def>().Select(x=>x.DefNum)");
			Assert.IsTrue(ReadRepoFile("WpfControlsOD","Frms","FrmTaskFilter.xaml").Contains("x:Name=\"comboClinic\" Height=\"20\" Margin=\"95,25,0,0\" Width=\"185\" IsMultiSelect=\"True\""),
				"Unassigned can be picked together with real clinics.");
		}

		[TestMethod]
		public void TaskList_FilterText_NamesUnassigned() {
			string code=ReadRepoFile("OpenDental","User Controls","UserControlTasks.cs");
			StringAssert.Contains(code,"_listClinicNumsFilter.Select(x => TaskFilters.GetClinicFilterText(x))");
			StringAssert.Contains(code,"_listDefNumsRegionFilter.Select(x => TaskFilters.GetRegionFilterText(x))");
		}

		[TestMethod]
		public void TaskQueries_UseTheUnassignedHook() {
			string code=ReadRepoFile("OpenDentBusiness","Data Interface","Tasks.cs");
			StringAssert.Contains(code,"TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,listClinicNumsFilter,listDefNumsRegionFilter,listClinicsUnrestricted)");
			StringAssert.Contains(code,"if(listClinicNumsForQuery.Count>1 || isUnassignedFilter) {");
		}
	}
}
