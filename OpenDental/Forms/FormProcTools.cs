using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental{
	/// <summary>
	/// Summary description for FormBasicTemplate.
	/// </summary>
	public partial class FormProcTools : FormODBase {
		public bool Changed;

		///<summary></summary>
		public FormProcTools()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormProcTools_Load(object sender,EventArgs e) {
			if(ODBuild.IsTrial()) {
				checkTcodes.Checked=false;
				checkNcodes.Checked=false;
				checkAutocodes.Checked=false;
				checkProcButtons.Checked=false;
				checkApptProcsQuickAdd.Checked=false;
				checkTcodes.Enabled=false;
				//checkNcodes.Enabled=false;
				checkAutocodes.Enabled=false;
				checkProcButtons.Enabled=false;
				if(CultureInfo.CurrentCulture.Name.EndsWith("US")) {
					checkRecallTypes.Enabled=false;
					checkApptProcsQuickAdd.Enabled=false;
				}
			}
			//Procedure codes (CDT and CDA) and their descriptions and treatment areas come from the procedurecode table, which is loaded into the
			//database directly. This tool doesn't add or reset them.
			if(CultureInfo.CurrentCulture.Name.EndsWith("CA")) {//Canadian. en-CA or fr-CA
				checkRecallTypes.Text="Recall Types - Resets the recall types and triggers to default. Replaces any T Codes with CDA Codes.";
			}
		}

		private void butUncheck_Click(object sender,EventArgs e) {
			checkTcodes.Checked=false;
			checkNcodes.Checked=false;
			checkAutocodes.Checked=false;
			checkProcButtons.Checked=false;
			checkApptProcsQuickAdd.Checked=false;
			checkRecallTypes.Checked=false;
		}

		private void butRun_Click(object sender,EventArgs e) {
			if(!checkTcodes.Checked
				&& !checkNcodes.Checked
				&& !checkAutocodes.Checked
				&& !checkProcButtons.Checked 
				&& !checkApptProcsQuickAdd.Checked 
				&& !checkRecallTypes.Checked)
			{
				MsgBox.Show(this,"Please select at least one tool first.");
				return;
			}
			Changed=false;
			int rowsInserted=0;
			#region N Codes
			if(checkNcodes.Checked) {
				try {
					rowsInserted+=FormProcCodes.ImportProcCodes("",null,Properties.Resources.NoFeeProcCodes);
				}
				catch(ApplicationException ex) {
					MessageBox.Show(ex.Message);
				}
				Changed=true;
				DataValid.SetInvalid(InvalidType.Defs, InvalidType.ProcCodes);
				MessageBox.Show("Procedure codes inserted: "+rowsInserted);
			}
			#endregion
			#region Auto Codes
			if(checkAutocodes.Checked) {
				//checking for any AutoCodes and prompting the user if they exist
				if(AutoCodes.GetCount() > 0) {
					string msgText=Lan.g(this,"This tool will delete all current auto codes and then add in the default auto codes.")+"\r\n";
					//If the proc tool isn't going to put the procedure buttons back to default, warn them that they will need to reassociate them.
					if(!checkProcButtons.Checked) {
						msgText+=Lan.g(this,"Any procedure buttons associated with the current auto codes will be dissociated and will need to be reassociated manually.")+"\r\n";
					}
					msgText+=Lan.g(this,"Continue?");
					if(MsgBox.Show(this,MsgBoxButtons.YesNo,msgText)) {
						AutoCodes.SetToDefault();
						Changed=true;
						DataValid.SetInvalid(InvalidType.AutoCodes);
					}
					else {
						checkAutocodes.Checked=false; //if the user hits no on the popup, uncheck and continue 
					}
				}
				//If there are no autocodes then add the defaults
				else {
					AutoCodes.SetToDefault();
					Changed=true;
					DataValid.SetInvalid(InvalidType.AutoCodes);
				}
			}
			#endregion
			#region Proc Buttons
			if(checkProcButtons.Checked) {
					if(MsgBox.Show(this,MsgBoxButtons.YesNo,"This tool will delete all current Procedure Buttons and Quick Buttons from the Chart Module and add in the defaults. Continue?")) {
						ProcButtons.SetToDefault();
						ProcButtonQuicks.SetToDefault();
						Changed=true;
						DataValid.SetInvalid(InvalidType.ProcButtons,InvalidType.Defs);
					}
					else {
						checkProcButtons.Checked=false;//continue and uncheck if user hits no on the popup
					}
			}
			#endregion
			#region Appt Procs Quick Add
			if(checkApptProcsQuickAdd.Checked) {
				//checking for any ApptProcsQuickAdd and prompting the user if they exist
				if(Defs.GetDefsForCategory(DefCat.ApptProcsQuickAdd).Count>0) {
					if(MsgBox.Show(this,MsgBoxButtons.YesNo,"This tool will reset the list of procedures in the appointment edit window to the defaults. Continue?")) {
						if(CultureInfo.CurrentCulture.Name.EndsWith("CA")) {//Canadian. en-CA or fr-CA
							ProcedureCodes.ResetApptProcsQuickAddCA();
						}
						else {//USA
							ProcedureCodes.ResetApptProcsQuickAdd();
						}
						Changed=true;
						DataValid.SetInvalid(InvalidType.Defs);
					}
					else {
						checkApptProcsQuickAdd.Checked=false;//uncheck and continue if no is selected on the popup
					}
				}
				//run normally if no customizations are found
				else {
					if(CultureInfo.CurrentCulture.Name.EndsWith("CA")) {//Canadian. en-CA or fr-CA
						ProcedureCodes.ResetApptProcsQuickAddCA();
					}
					else {//USA
						ProcedureCodes.ResetApptProcsQuickAdd();
					}
					Changed=true;
					DataValid.SetInvalid(InvalidType.Defs);
				}
			}
			#endregion
			#region Recall Types
			if(checkRecallTypes.Checked) {
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"This will delete all patient recalls for recall types which were manually added, and resets recall types that were modified. Continue?")) 
				{  
					checkRecallTypes.Checked=false;
				}
				else {
					if(CultureInfo.CurrentCulture.Name.EndsWith("CA")) {//Canadian. en-CA or fr-CA
						RecallTypes.SetToDefaultCA();
					}
					else {//USA
						RecallTypes.SetToDefault();
					}
					Changed=true;
					DataValid.SetInvalid(InvalidType.RecallTypes,InvalidType.Prefs);				
					SecurityLogs.MakeLogEntry(EnumPermType.RecallEdit,0,"Recall types set to default.");
				}
			}
			#endregion
			#region T Codes
			if(checkTcodes.Checked){//Even though this is first in the interface, we need to run it last, since other regions need the T codes above.
				ProcedureCodes.TcodesClear();
				Changed=true;
				//yes, this really does refresh before moving on.
				DataValid.SetInvalid(InvalidType.Defs, InvalidType.ProcCodes);
				SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,"T Codes deleted.");
			}
			#endregion
			if(Changed) {
				MessageBox.Show(Lan.g(this,"Done."));
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"New Customer Procedure codes tool was run.");
			}
		}

	}
}