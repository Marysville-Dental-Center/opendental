using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;
using CodeBase;
using System.Linq;

namespace OpenDental {
	public partial class FormApptBreak:FormODBase {
		public ApptBreakSelection ApptBreakSelection_;
		public ProcedureCode ProcedureCodeSelected;
		private readonly Appointment _appointment;

		public FormApptBreak(Appointment appointment) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_appointment=appointment;
		}

		private void FormApptBreak_Load(object sender,EventArgs e) {
			BrokenApptProcedure brokenApptProcs=(BrokenApptProcedure)PrefC.GetInt(PrefName.BrokenApptProcedure);
			radioMissed.Enabled=brokenApptProcs.In(BrokenApptProcedure.Missed,BrokenApptProcedure.Both);
			radioCancelled.Enabled=brokenApptProcs.In(BrokenApptProcedure.Cancelled,BrokenApptProcedure.Both);
			if(!radioMissed.Enabled && radioCancelled.Enabled) {
				radioCancelled.Checked=true;
			}
		}

		private bool ValidateSelection() {
			if(!radioMissed.Checked && !radioCancelled.Checked) {
				MsgBox.Show(this,"Please select a broken procedure type.");
				return false;
			}
			return true;
		}

		private void butUnsched_Click(object sender,EventArgs e) {
			if(!ValidateSelection()) {
				return;
			}
			if(PrefC.GetBool(PrefName.UnscheduledListNoRecalls) && Appointments.IsRecallAppointment(_appointment)) {
				if(MsgBox.Show(this,MsgBoxButtons.YesNo,"Recall appointments cannot be sent to the Unscheduled List.\r\nDelete appointment instead?")) {
					ApptBreakSelection_=ApptBreakSelection.Delete;//Set to delete so the parent form can handle the delete. 
					DialogResult=DialogResult.Cancel;
				}
				return;
			}
			ApptBreakSelection_=ApptBreakSelection.Unsched;
			DialogResult=DialogResult.OK;
		}

		private void butPinboard_Click(object sender,EventArgs e) {
			if(!ValidateSelection()) {
				return;
			}
			ApptBreakSelection_=ApptBreakSelection.Pinboard;
			DialogResult=DialogResult.OK;
		}

		private void butApptBook_Click(object sender,EventArgs e) {
			if(!ValidateSelection()) {
				return;
			}
			ApptBreakSelection_=ApptBreakSelection.ApptBook;
			DialogResult=DialogResult.OK;
		}

		private void FormApptBreak_FormClosing(object sender,FormClosingEventArgs e) {
			if(DialogResult!=DialogResult.OK) {
				return;
			}
			ProcedureCodeSelected=ProcedureCodes.GetProcCode("D9987");
			if(radioMissed.Checked) {
				ProcedureCodeSelected=ProcedureCodes.GetProcCode("D9986");
			}
		}
	}

	public enum ApptBreakSelection {
		///<summary>0 - Default.</summary>
		None,
		///<summary>1</summary>
		Unsched,
		///<summary>2</summary>
		Pinboard,
		///<summary>3</summary>
		ApptBook,
		///<summary>4</summary>
		Delete,
	}

}