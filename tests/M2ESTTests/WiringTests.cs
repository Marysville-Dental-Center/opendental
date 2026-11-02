// The windows cannot be opened in a test run (the GUI is not launched), so these tests read the window sources and check that each one calls the
// shared rules, which the other files test. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2ESTTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		///<summary>The text of one method: from its signature to the first closing brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			Match match=new Regex(@"\r?\n\t\t\}").Match(source,idxStart);
			Assert.IsTrue(match.Success,signature+" has no closing brace.");
			return source.Substring(idxStart,match.Index-idxStart);
		}

		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		[TestMethod]
		public void ProcedureInfo_DoNotBillBox_SavesAndAuditsTheProcedureFlag() {
			string body=MethodBody(Read("OpenPmsDental","Forms","FormProcEdit.cs"),"private void checkNoBillIns_Click(");
			AssertBefore(body,"Security.IsAuthorized(EnumPermType.InsWriteOffEdit","Procedures.GetNoBillInsState(_listClaimProcs)");
			AssertBefore(body,"Procedures.GetNoBillInsState(_listClaimProcs)","ClaimProcs.Update(_listClaimProcs[i])");
			AssertBefore(body,"ClaimProcs.Update(_listClaimProcs[i])",
				"Procedures.SetNoBillInsFromUser(_procedure,checkNoBillIns.CheckState==CheckState.Checked,isNoBillInsOld)");
			AssertBefore(body,"Procedures.SetNoBillInsFromUser(","_procedureOld.NoBillIns=_procedure.NoBillIns;");
			AssertBefore(body,"_procedureOld.NoBillIns=_procedure.NoBillIns;","Procedures.ComputeEstimates(_procedure,");
		}

		[TestMethod]
		public void EditClaimProcedure_BillingAgain_ClearsTheProcedureFlag() {
			string body=MethodBody(Read("OpenPmsDental","Forms","FormClaimProc.cs"),"private void butSave_Click(");
			AssertBefore(body,"ClaimProcs.Update(_claimProc,_claimProcOld);","Procedures.ClearNoBillInsForBilledClaimProc(_procedure,_claimProcOld,_claimProc);");
			AssertBefore(body,"if(_doSaveToDb) {","Procedures.ClearNoBillInsForBilledClaimProc(");
			AssertBefore(body,"Procedures.ClearNoBillInsForBilledClaimProc(","}//otherwise, the change to db will be made by calling class");
		}

		[TestMethod]
		public void EditBenefit_TeethFollowTypeAndQualifier() {
			string source=Read("OpenPmsDental","Forms","FormBenefitEdit.cs");
			StringAssert.Contains(source,"return Benefits.CanHaveTeeth(listBenefitType.GetSelected<InsBenefitType>(),benefitQuantity);");
			StringAssert.Contains(MethodBody(source,"private void listQuantityQualifier_SelectedIndexChanged("),"SetTeethEnabled();");
			StringAssert.Contains(MethodBody(source,"private void butSave_Click("),"if(Benefits.CanHaveTeeth(benefitType,quantityQualifier)) {");
			string designer=Read("OpenPmsDental","Forms","FormBenefitEdit.Designer.cs");
			StringAssert.Contains(designer,
				"this.listQuantityQualifier.SelectedIndexChanged += new System.EventHandler(this.listQuantityQualifier_SelectedIndexChanged);");
		}

		[TestMethod]
		public void EditBenefits_AgeLimitsGridHasTeeth() {
			string source=Read("OpenPmsDental","Forms","FormInsBenefits.cs");
			string fill=MethodBody(source,"private void FillGridAgeLimits(");
			StringAssert.Contains(fill,"new GridColumn(Lan.g(this,\"Teeth\"),50,isEditable:true)");
			StringAssert.Contains(fill,"gridRow.Cells.Add(Benefits.GetToothRangeDisplay(benefit));");
			StringAssert.Contains(fill,"_listBenefitsAll.FindAll(","Every age limit of a group gets a row.");
			StringAssert.Contains(source,"gridAgeLimits.CellLeave+=gridAgeLimits_CellLeave;");
			string convert=MethodBody(source,"private bool ConvertFormToBenefits(");
			AssertBefore(convert,"GetAgeLimitTeethErrors()","benefit.ToothRange=Tooth.ParseToothList(gridAgeLimits.ListGridRows[i].Cells[COL_AGE_LIMIT_TEETH].Text);");
			string editable=MethodBody(source,"private bool IsOtherBenefitCellEditable(");
			StringAssert.Contains(editable,"Benefits.CanHaveTeeth((InsBenefitType)typeIndex,");
			StringAssert.Contains(MethodBody(source,"private void butAddBenefit_Click("),"FillGridAgeLimits();");
		}
	}
}
