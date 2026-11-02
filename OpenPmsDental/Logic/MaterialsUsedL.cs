// Inventory tracking (docs/specs/inventory-tracking.md): opens the Materials used panel by itself after procedures are completed, from every
// completion path of the program, one panel per user action.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>The business layer raises InventoryTracking.ProcSetComplete for every procedure completed in this program (Procedures.Insert and
	///Procedures.Update, also through the Middle Tier). This class collects the ones completed on the main thread, by the user's own action, and
	///when the program is idle again and the window the user worked in is active again (any message box or window the same action opened has
	///closed), opens one Materials used panel for the procedures of that action that await materials. So no completion path has to call it: the
	///Chart module, the procedure edit window, the appointment windows, broken appointments and the Account module all reach it. Procedures
	///completed on another thread are left on the Awaiting Materials list.</summary>
	public static class MaterialsUsedL {
		private static readonly List<long> _listProcNumsQueued=new List<long>();
		///<summary>The window that was active when the first procedure of the action was completed.</summary>
		private static Form _formOrigin;
		private static Control _controlMainThread;
		private static bool _isShowing;
		private static bool _isStarted;

		///<summary>Called once by the main window. controlMainThread tells the main thread from the others.</summary>
		public static void Start(Control controlMainThread) {
			if(_isStarted) {
				return;
			}
			_isStarted=true;
			_controlMainThread=controlMainThread;
			InventoryTracking.ProcSetComplete+=InventoryTracking_ProcSetComplete;
			Application.Idle+=Application_Idle;
		}

		private static void InventoryTracking_ProcSetComplete(long procNum) {
			if(_controlMainThread==null || _controlMainThread.IsDisposed || _controlMainThread.InvokeRequired) {
				return;//Not the user's action on the main thread: the procedure stays on the Awaiting Materials list.
			}
			if(!_listProcNumsQueued.Contains(procNum)) {
				_listProcNumsQueued.Add(procNum);
			}
			if(_formOrigin==null) {
				_formOrigin=Form.ActiveForm;
			}
		}

		private static void Application_Idle(object sender,EventArgs e) {
			if(_listProcNumsQueued.Count==0 || _isShowing) {
				return;
			}
			Form formActive=Form.ActiveForm;
			if(formActive==null) {
				return;//Another program is in front, or a message box of the action is still open. Wait.
			}
			if(_formOrigin!=null && !_formOrigin.IsDisposed && _formOrigin.Visible && formActive!=_formOrigin) {
				return;//A window the action opened is still open.
			}
			List<long> listProcNums=_listProcNumsQueued.ToList();
			_listProcNumsQueued.Clear();
			_formOrigin=null;
			_isShowing=true;
			try {
				ShowForAwaiting(formActive,listProcNums);
			}
			catch(Exception ex) {
				MessageBox.Show(formActive,Lan.g("FormMaterialsUsed","The Materials used window could not open. The procedures are on the Awaiting Materials list.")
					+"\r\n"+ex.Message);
			}
			finally {
				_isShowing=false;
			}
		}

		///<summary>Opens the Materials used panel for those of the procedures that await materials, filled with their codes' default materials. Does
		///nothing when tracking is off or none awaits materials. Returns true when the user confirmed.</summary>
		public static bool ShowForAwaiting(IWin32Window owner,List<long> listProcNums) {
			if(!InventoryTracking.IsEnabled() || listProcNums.Count==0) {
				return false;
			}
			List<long> listProcNumsAwaiting=SupplyProcPendings.GetForProcs(listProcNums).Select(x => x.ProcNum).ToList();
			if(listProcNumsAwaiting.Count==0) {
				return false;
			}
			using FormMaterialsUsed formMaterialsUsed=new FormMaterialsUsed();
			formMaterialsUsed.ListProcNums=listProcNums.FindAll(x => listProcNumsAwaiting.Contains(x));
			return formMaterialsUsed.ShowDialog(owner)==DialogResult.OK;
		}

		///<summary>The procedure edit window's Materials button: the materials of one complete procedure, what it holds now or, while it awaits
		///materials, its code's defaults. Changing materials that were already confirmed needs Completed Procedure Edit Misc for the procedure's date.
		///</summary>
		public static void ShowForProcedure(IWin32Window owner,long procNum) {
			Procedure procedure=Procedures.GetOneProc(procNum,false);
			if(procedure==null || procedure.ProcStatus!=ProcStat.C) {
				MsgBox.Show("FormMaterialsUsed","Materials are recorded for complete procedures. Set the procedure complete and save it first.");
				return;
			}
			bool isAwaiting=SupplyProcPendings.GetForProcs(new List<long> { procNum }).Count>0;
			using FormMaterialsUsed formMaterialsUsed=new FormMaterialsUsed();
			formMaterialsUsed.ListProcNums=new List<long> { procNum };
			formMaterialsUsed.IsReadOnly=!isAwaiting && !Security.IsAuthorized(EnumPermType.ProcCompleteEditMisc,procedure.ProcDate,suppressMessage:true);
			formMaterialsUsed.ShowDialog(owner);
		}
	}
}
