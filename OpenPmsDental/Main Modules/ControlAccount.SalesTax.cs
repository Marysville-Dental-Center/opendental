// B-ACCT (Open Dental 24.4): Quick Procs > Sales Tax in the Account module. Adds one completed procedure with the office's sales-tax procedure code
// whose fee is the tax on the highlighted completed procedures. The rules live in OpenDentBusiness/Data Interface/SalesTaxProcs.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class ControlAccount {
		///<summary>Menu items that contextMenuQuickProcs_Popup puts before the quick charge definitions: 1 when Sales Tax is shown, otherwise 0.
		///menuItemQuickProcs_Click subtracts it to find the clicked definition.</summary>
		private int _countQuickProcsLeadingItems;

		///<summary>Called at the end of contextMenuQuickProcs_Popup. Puts Sales Tax first in the Quick Procs menu when the office has set a sales-tax
		///procedure code that exists (Setup > Preferences > Account > Adjustments). Otherwise the item is hidden.</summary>
		private void AddQuickProcsSalesTaxItem() {
			_countQuickProcsLeadingItems=0;
			if(!SalesTaxProcs.IsEnabled()) {
				return;
			}
			contextMenuQuickProcs.MenuItems.Add(0,new MenuItem(Lan.g(this,"Sales Tax"),menuItemQuickProcsSalesTax_Click));
			_countQuickProcsLeadingItems=1;
		}

		///<summary>Adds one completed sales-tax procedure for the highlighted completed procedures. Its fee is the sum of each procedure's sales tax,
		///the same amounts Apply Sales Tax would post as adjustments.</summary>
		private void menuItemQuickProcsSalesTax_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.AccountProcsQuickAdd)) {
				return;
			}
			long codeNum=SalesTaxProcs.GetCodeNum();
			if(codeNum==0) {
				MsgBox.Show(this,"The Sales Tax procedure code in Preferences is not a valid procedure code.");
				return;
			}
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(codeNum);
			if(ProcedureCodes.AreAnyProcCodesHidden(codeNum)) {
				MsgBox.Show($"{Lan.g(this,"Cannot add procedure because it is in a hidden category")}: {procedureCode.ProcCode}");
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.ProcComplCreate,DateTime.Today)) {
				return;
			}
			DataTable table=_dataSetMain.Tables["account"];
			List<long> listProcNums=new List<long>();
			List<int> listIndices=gridAccount.SelectedIndices.ToList();
			for(int i=0;i<listIndices.Count;i++) {
				long procNum=PIn.Long(table.Rows[listIndices[i]]["ProcNum"].ToString());
				if(procNum!=0 && !listProcNums.Contains(procNum)) {
					listProcNums.Add(procNum);
				}
			}
			if(listProcNums.Count==0) {
				MsgBox.Show(this,"Please highlight the completed procedures to add sales tax for.");
				return;
			}
			List<Procedure> listProcedures=SalesTaxProcs.GetTaxableProcs(Procedures.GetManyProc(listProcNums,false));
			if(listProcedures.Count==0) {
				MsgBox.Show(this,"None of the highlighted procedures is a completed procedure that can be taxed.");
				return;
			}
			List<long> listProcNumsTaxable=listProcedures.Select(x => x.ProcNum).ToList();
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForProcs(listProcNumsTaxable);
			double fee=SalesTaxProcs.ComputeSalesTaxTotal(listProcedures,listClaimProcs);
			if(CompareDouble.IsZero(fee)) {
				MsgBox.Show(this,"The sales tax for the highlighted procedures is zero. No sales tax procedure was added.");
				return;
			}
			if(SalesTaxProcs.GetProcNumsWithSalesTaxAdjustment(listProcNumsTaxable).Count>0
				&& !MsgBox.Show(this,MsgBoxButtons.OKCancel,"One or more of the highlighted procedures already has a sales tax adjustment. "
				+"Adding a sales tax procedure as well taxes them twice. Continue?"))
			{
				return;
			}
			if(!AddSalesTaxProcAndValidate(procedureCode,fee)) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.AccountProcsQuickAdd,_patient.PatNum,
				SalesTaxProcs.GetAuditNote(procedureCode.ProcCode,fee,listProcedures.Count));
			Signalods.SetInvalid(InvalidType.BillingList);
			ModuleSelected(_patient.PatNum);
		}

		///<summary>Inserts the sales-tax procedure and validates it silently with FormProcEdit, the same way AddProcAndValidate does for quick charge
		///procedures, but with the tax as its fee (not the fee schedule's) and the sales-tax provider. The procedure is never linked to an ortho case.
		///Returns false if the user cancelled, in which case the procedure is deleted again.</summary>
		private bool AddSalesTaxProcAndValidate(ProcedureCode procedureCode,double fee) {
			Procedure procedure=SalesTaxProcs.CreateSalesTaxProcedure(_patient,procedureCode,fee,DateTime.Now);
			procedure.ProcStatus=ProcStat.D;//Inserted like AddProcAndValidate does, then completed by FormProcEdit.
			Procedures.Insert(procedure);
			procedure.ProcStatus=ProcStat.C;
			List<ClaimProcHist> listClaimProcHistsLoop=new List<ClaimProcHist>();
			using FormProcEdit formProcEdit=new FormProcEdit(procedure,_patient,_family,true);
			formProcEdit.ListClaimProcHists=_loadData.HistList;
			formProcEdit.ListClaimProcHistsLoop=listClaimProcHistsLoop;
			formProcEdit.IsNew=true;
			formProcEdit.ShowDialog();
			if(formProcEdit.DialogResult!=DialogResult.OK) {
				Procedures.Delete(procedure.ProcNum);
				return false;
			}
			if(procedure.ProcStatus==ProcStat.C) {
				AutomationL.Trigger(EnumAutomationTrigger.ProcedureComplete,new List<string>() { procedureCode.ProcCode },_patient.PatNum);
				Procedures.AfterProcsSetComplete(new List<Procedure>() { procedure });//Never taxes the sales-tax procedure itself.
			}
			return true;
		}
	}
}
