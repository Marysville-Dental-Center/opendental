// The Chart module's preauthorization reminder (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER in
// docs/specs/port-backlog.md): charting a treatment-planned procedure whose code one of the patient's carriers lists in carrier.PreAuthCodes pops up
// one reminder per charting action, with "Create Preauth Now" and "Later". ControlChart.cs only gains one call at the end of AddProcedure and AddQuick.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class ControlChart {
		///<summary>Procedures charted for _patNumPreAuthPending since the last reminder, by ProcNum.</summary>
		private List<long> _listProcNumsPreAuthPending=new List<long>();
		private long _patNumPreAuthPending;
		///<summary>Waits until the charting action is over: every procedure of one button click or code entry is added in one go, and the dialogs it
		///opens on the way (procedure edit, auto codes) are closed.</summary>
		private System.Windows.Forms.Timer _timerPreAuthReminder;

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindowEnabled(IntPtr hWnd);

		///<summary>Called at the end of AddProcedure and AddQuick for every procedure that was added. Collects the procedure and starts the short timer
		///that shows the reminder once the charting action is over, so one action shows at most one reminder listing every procedure. Does nothing when
		///no carrier has a preauthorization list. The procedure's status is read again when the reminder is built, because the edit window can change
		///it.</summary>
		private void PreAuthReminder_Queue(Procedure procedure) {
			if(procedure==null || procedure.ProcNum==0 || IsPatientNull() || !CarrierPreAuths.IsInUse()) {
				return;
			}
			if(_patNumPreAuthPending!=Pd.PatNum) {
				_listProcNumsPreAuthPending.Clear();
				_patNumPreAuthPending=Pd.PatNum;
			}
			if(!_listProcNumsPreAuthPending.Contains(procedure.ProcNum)) {
				_listProcNumsPreAuthPending.Add(procedure.ProcNum);
			}
			if(_timerPreAuthReminder==null) {
				_timerPreAuthReminder=new System.Windows.Forms.Timer();
				_timerPreAuthReminder.Interval=100;
				_timerPreAuthReminder.Tick+=TimerPreAuthReminder_Tick;
				Disposed+=(sender,e) => _timerPreAuthReminder?.Dispose();
			}
			_timerPreAuthReminder.Stop();
			_timerPreAuthReminder.Start();
		}

		private void TimerPreAuthReminder_Tick(object sender,EventArgs e) {
			if(IsDisposed) {
				_timerPreAuthReminder.Stop();
				return;
			}
			if(!IsMainWindowEnabled()) {
				return;//A dialog is still open, for example the edit window of the next procedure of the same action. Check again on the next tick.
			}
			_timerPreAuthReminder.Stop();
			List<long> listProcNums=new List<long>(_listProcNumsPreAuthPending);
			long patNum=_patNumPreAuthPending;
			_listProcNumsPreAuthPending.Clear();
			if(listProcNums.Count==0 || IsPatientNull() || Pd.PatNum!=patNum) {
				return;
			}
			PreAuthReminder_Show(listProcNums);
		}

		///<summary>False while a modal dialog is open: a modal dialog (Windows Forms, WPF or message box) disables the main window.</summary>
		private bool IsMainWindowEnabled() {
			Form form=FindForm();
			if(form==null || !form.IsHandleCreated) {
				return true;
			}
			return IsWindowEnabled(form.Handle);
		}

		///<summary>Shows one reminder listing every charted procedure that still needs a preauthorization. "Create Preauth Now" creates the
		///preauthorizations and opens each one in Edit Claim, as the Treatment Plan module's PreAuthorization button does; "Later" closes the reminder,
		///and the procedures stay flagged in the Treatment Plan and Appointments modules.</summary>
		private void PreAuthReminder_Show(List<long> listProcNums) {
			List<PreAuthNeed> listPreAuthNeeds=CarrierPreAuths.GetNeedsForProcNums(listProcNums);
			if(listPreAuthNeeds.Count==0) {
				return;
			}
			using FormPreAuthReminder formPreAuthReminder=new FormPreAuthReminder(listPreAuthNeeds);
			if(formPreAuthReminder.ShowDialog()!=DialogResult.OK) {
				return;//Later
			}
			if(PreAuthL.CreatePreAuths(Pd.Patient,Pd.Family,listPreAuthNeeds)) {
				ModuleSelected(Pd.PatNum);
			}
		}
	}
}
