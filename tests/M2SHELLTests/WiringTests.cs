// Source checks that the WinForms code calls the M2-SHELL business code (the tests do not load OpenPmsDental.exe): the patient change hook, the
// removed Clear Duplicate Blockouts tool, the Billing/Finance Charges entries, the Chart View entries and the Global Security setting.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2SHELLTests {
	[TestClass]
	public class WiringTests {
		private static string GetMethod(string source,string signature) {
			int start=source.IndexOf(signature);
			Assert.IsTrue(start>=0,signature+" not found.");
			//The method ends at the first line that closes a member: two tabs and a brace.
			Match match=Regex.Match(source.Substring(start),@"\r?\n\t\t\}\r?\n");
			Assert.IsTrue(match.Success,signature+" has no end.");
			return source.Substring(start,match.Index+match.Length);
		}

		#region v25_4-security-patientselected-audit
		[TestMethod]
		public void PatientChange_CallsTheAudit() {
			string source=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs");
			string property=GetMethod(source,"public static long PatNumCur {");
			int idxAssign=property.IndexOf("_patNumCur=value;");
			int idxAudit=property.IndexOf("PatientSelectedAudit.LogIfEnabled(value);");
			Assert.IsTrue(idxAssign>0 && idxAudit>idxAssign,"The setter logs after the patient changed (it returns early when the patient is the same).");
		}

		[TestMethod]
		public void LogOn_DifferentUserKeepingThePatient_CallsTheAudit() {
			//Combined review (2026-11-01): with clinics off, or PatientMaintainedOnUserChange on, the patient stays loaded for the next user, so the
			//PatNumCur setter never runs and the new user worked in that patient's modules with no Patient Selected entry.
			string source=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs");
			string method=GetMethod(source,"private void FinishLogOff(bool isForced) {");
			string call="if(PatNumCur!=0 && userod.UserNum!=Security.CurUser.UserNum) {";
			int idxLogOn=method.IndexOf("ShowLogOn(doClearCaches:true);");
			int idxClinicBlock=method.IndexOf("RefreshMenuClinics();");
			int idxCall=method.IndexOf(call);
			int idxModule=method.IndexOf("SetModuleSelected();");
			Assert.IsTrue(idxLogOn>0 && idxClinicBlock>idxLogOn,"The new user logs on, then the patient is kept or cleared for that user's clinics.");
			Assert.IsTrue(idxCall>idxClinicBlock && idxCall<idxModule,"The entry is written once the patient is known to stay, before the modules show it.");
			StringAssert.Contains(method.Substring(idxCall,idxModule-idxCall),"PatientSelectedAudit.LogIfEnabled(PatNumCur);");
		}

		[TestMethod]
		public void GlobalSecurity_HasTheSetting() {
			string source=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormGlobalSecurity.cs");
			StringAssert.Contains(source,"checkAuditPatientSelected.Checked=PatientSelectedAudit.IsEnabled();");
			StringAssert.Contains(source,"PatientSelectedAudit.SetEnabled(checkAuditPatientSelected.Checked)");
			string designer=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormGlobalSecurity.designer.cs");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkAuditPatientSelected);");
		}
		#endregion

		#region v26_1-tools-dbm-duplicate-blockouts
		[TestMethod]
		public void ClearDuplicateBlockoutsTool_IsGone() {
			foreach(string fileName in new[] { "FormBlockoutDuplicatesFix.cs","FormBlockoutDuplicatesFix.designer.cs","FormBlockoutDuplicatesFix.resx" }) {
				Assert.IsFalse(File.Exists(TestPaths.GetRepoFile("OpenPmsDental","Forms",fileName)),fileName+" is deleted.");
			}
			Assert.IsFalse(TestPaths.ReadRepoFile("OpenPmsDental","OpenPmsDental.csproj").Contains("FormBlockoutDuplicatesFix"));
			string menus=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDentalMenus.cs");
			Assert.IsFalse(menus.Contains("Clear Duplicate Blockouts"),"Tools, Misc Tools no longer lists the tool.");
			Assert.IsFalse(TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs").Contains("menuItemDuplicateBlockouts_Click"));
		}
		#endregion

		#region v26_1-misc-audit-billing-finance
		[TestMethod]
		public void FinanceCharges_LogsOpenAndRun_NotClose() {
			string main=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs");
			string menuHandler=GetMethod(main,"private void menuItemFinanceCharge_Click(");
			Assert.IsFalse(menuHandler.Contains("SecurityLogs.MakeLogEntry"),"The 24.3 entry written when the window closed is replaced.");
			Assert.IsFalse(main.Contains("\"Run Finance Charges\""));
			string form=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormFinanceCharges.cs");
			StringAssert.Contains(GetMethod(form,"private void FormFinanceCharges_Load("),"FinanceChargeAudit.LogOpened();");
			string run=GetMethod(form,"private void butRun_Click(");
			Assert.AreEqual(2,Regex.Matches(run,@"FinanceChargeAudit\.LogRun\(").Count,"One entry for a finished run and one for a cancelled run.");
			StringAssert.Contains(run,"isCancelled:true");
			StringAssert.Contains(GetMethod(form,"private void butUndo_Click("),"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,chargeType+\" Charges undo.","The undo entry stays.");
			Assert.AreEqual(2,Regex.Matches(form,@"EnumPermType\.AgingRan").Count,"The aging entries stay.");
			StringAssert.Contains(GetMethod(form,"private bool AddFinanceCharge("),"AddToChargesTotal(adjustment.AdjAmt);","Finance charges add to the total.");
			StringAssert.Contains(GetMethod(form,"private void AddBillingCharge("),"AddToChargesTotal(AdjustmentCur.AdjAmt);","Billing charges add to the total.");
		}
		#endregion

		#region v26_1-misc-audit-chartviews
		[TestMethod]
		public void ChartViewWindow_LogsAddEditDelete() {
			string form=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormChartView.cs");
			StringAssert.Contains(GetMethod(form,"private void butDelete_Click("),"ChartViews.LogDeleted(ChartViewCur);");
			string save=GetMethod(form,"private void butSave_Click(");
			StringAssert.Contains(save,"ChartViews.LogAdded(ChartViewCur);");
			StringAssert.Contains(save,"ChartViews.LogEdited(_chartViewOld,ChartViewCur,_listDisplayFieldsOld,_listDisplayFieldsShowing);");
			StringAssert.Contains(GetMethod(form,"private void FormChartView_Load("),"_chartViewOld=ChartViewCur.Copy();");
		}

		[TestMethod]
		public void ChartModule_LogsMoves() {
			string source=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","ControlChart.cs");
			StringAssert.Contains(GetMethod(source,"private void SwapChartViews("),"ChartViews.LogMoved(_listChartViews[newIdx],oldIdx,newIdx);");
		}
		#endregion
	}
}
