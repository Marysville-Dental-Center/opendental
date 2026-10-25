using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
			//Procedure codes (CDT and CDA) and their descriptions come from the procedurecode table. No code text ships with this program: the D Codes
			//(CDA Codes in Canada) option reads a licensed code file that the office supplies.
			if(CultureInfo.CurrentCulture.Name.EndsWith("CA")) {//Canadian. en-CA or fr-CA
				checkRecallTypes.Text="Recall Types - Resets the recall types and triggers to default. Replaces any T Codes with CDA Codes.";
				checkCodeImport.Text=Lan.g(this,"CDA Codes - Add any missing CDA codes from a licensed code file that you supply. Existing codes are not changed.");
			}
			if(ODBuild.IsTrial()) {
				checkCodeImport.Checked=false;
				checkCodeImport.Enabled=false;
			}
		}

		///<summary>D Codes (CDT) or, in Canada, CDA Codes: imports the code file the office picks. Returns true if any code was added or changed.</summary>
		private bool RunCodeImport() {
			//Security Admin is checked here too, not only by the caller: the Setup Wizard also opens this window.
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin) || !Security.IsAuthorized(EnumPermType.ProcCodeEdit)) {
				checkCodeImport.Checked=false;
				return false;
			}
			EnumDentalCodeSystem codeSystem=CodeGroupDefaults.GetCodeSystemForCulture(CultureInfo.CurrentCulture.Name);
			//New CDT codes come out before the year they take effect. Claims that use them before January 1 could be rejected.
			if(codeSystem==EnumDentalCodeSystem.CDT && ProcCodeImports.IsYearEndWindow(DateTime.Today)) {
				string msgYearEnd=Lan.g(this,"Codes from next year's code set are not valid until")+" "+new DateTime(DateTime.Today.Year+1,1,1).ToShortDateString()+". "
					+Lan.g(this,"Claims that use them before then could be rejected. Import the D codes anyway?");
				if(MessageBox.Show(this,msgYearEnd,Lan.g(this,"Procedure Codes"),MessageBoxButtons.YesNo)!=DialogResult.Yes) {
					checkCodeImport.Checked=false;
					return false;
				}
			}
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			if(codeSystem==EnumDentalCodeSystem.CDA) {
				openFileDialog.Title=Lan.g(this,"Choose the CDA code file");
			}
			else {
				openFileDialog.Title=Lan.g(this,"Choose the CDT code file");
			}
			openFileDialog.Filter=Lan.g(this,"Code files")+" (*.xml;*.txt;*.csv;*.tsv)|*.xml;*.txt;*.csv;*.tsv|"+Lan.g(this,"All files")+" (*.*)|*.*";
			if(Directory.Exists(PrefC.GetString(PrefName.ExportPath))) {
				openFileDialog.InitialDirectory=PrefC.GetString(PrefName.ExportPath);
			}
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				checkCodeImport.Checked=false;
				return false;
			}
			string fileText;
			try {
				fileText=ProcCodeImports.DecodeFileBytes(File.ReadAllBytes(openFileDialog.FileName));
			}
			catch(Exception ex) {
				MessageBox.Show(this,Lan.g(this,"The code file could not be read:")+"\r\n"+ex.Message);
				return false;
			}
			List<ProcCodeImportRow> listProcCodeImportRows=ProcCodeImports.Parse(fileText,out List<string> listErrors);
			if(listProcCodeImportRows.Count==0) {
				using MsgBoxCopyPaste msgBoxCopyPasteNone=new MsgBoxCopyPaste(Lan.g(this,"No procedure codes were found in the file.")+"\r\n\r\n"
					+string.Join("\r\n",listErrors.Take(20)));
				msgBoxCopyPasteNone.ShowDialog();
				return false;
			}
			Cursor=Cursors.WaitCursor;
			ProcCodeImportPlan procCodeImportPlan=ProcCodeImports.CreatePlanFromDb(listProcCodeImportRows,codeSystem);
			Cursor=Cursors.Default;
			string summary=ProcCodeImports.GetPlanSummary(procCodeImportPlan,listErrors);
			if(!procCodeImportPlan.HasWork()) {
				using MsgBoxCopyPaste msgBoxCopyPasteNothing=new MsgBoxCopyPaste(Lan.g(this,"Nothing to change.")+"\r\n\r\n"+summary);
				msgBoxCopyPasteNothing.ShowDialog();
				return false;
			}
			if(MessageBox.Show(this,summary+"\r\n"+Lan.g(this,"Continue?"),Lan.g(this,"Procedure Codes"),MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				checkCodeImport.Checked=false;
				return false;
			}
			Cursor=Cursors.WaitCursor;
			ProcCodeImportResult procCodeImportResult=ProcCodeImports.Apply(procCodeImportPlan);
			Cursor=Cursors.Default;
			DataValid.SetInvalid(InvalidType.Defs,InvalidType.ProcCodes);
			MessageBox.Show(this,ProcCodeImports.GetLogText(codeSystem,procCodeImportResult));
			return true;
		}

		///<summary>Code Groups: asks for the code system, shows what will change and, when groups in use have no default match, asks to keep them as
		///hidden groups. Returns true if the code groups were reset.</summary>
		private bool RunCodeGroupReset() {
			//Security Admin is checked here too, not only by the caller: the Setup Wizard also opens this window.
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin) || !Security.IsAuthorized(EnumPermType.ProcCodeEdit)) {
				return false;
			}
			List<EnumDentalCodeSystem> listCodeSystems=new List<EnumDentalCodeSystem> { EnumDentalCodeSystem.CDT,EnumDentalCodeSystem.CDA };
			List<string> listCodeSystemNames=new List<string> { Lan.g(this,"CDT (United States)"),Lan.g(this,"CDA (Canada)") };
			EnumDentalCodeSystem codeSystemDefault=CodeGroupDefaults.GetCodeSystemForCulture(CultureInfo.CurrentCulture.Name);
			InputBox inputBox=new InputBox(Lan.g(this,"Which code system should the default code groups use?"),listCodeSystemNames,
				listCodeSystems.IndexOf(codeSystemDefault));
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel || inputBox.SelectedIndex<0) {
				return false;
			}
			EnumDentalCodeSystem codeSystem=listCodeSystems[inputBox.SelectedIndex];
			CodeGroupResetPlan codeGroupResetPlan=CodeGroupResets.CreatePlanFromDb(codeSystem);
			string msg=CodeGroupResets.GetPlanSummary(codeGroupResetPlan)+"\r\n\r\n";
			bool hasUnmatched=codeGroupResetPlan.GetUnmatched().Count>0;
			if(hasUnmatched) {
				msg+=Lan.g(this,"Keep those code groups as hidden groups and reset the rest?");
			}
			else {
				msg+=Lan.g(this,"Custom code groups will be lost. Continue?");
			}
			if(MessageBox.Show(this,msg,Lan.g(this,"Code Groups"),MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return false;
			}
			CodeGroupResetResult codeGroupResetResult=CodeGroupResets.Reset(codeSystem,doKeepUnmatched:hasUnmatched);
			if(!codeGroupResetResult.IsDone) {
				//Benefits started using a group that matches no default after the summary was shown.
				MsgBox.Show(this,"Code groups were changed by someone else. Nothing was reset. Please run the tool again.");
				return false;
			}
			CodeGroups.RefreshCache();
			DataValid.SetInvalid(InvalidType.CodeGroups);
			MessageBox.Show(this,CodeGroupResets.GetLogText(codeGroupResetResult));
			return true;
		}

		private void butUncheck_Click(object sender,EventArgs e) {
			checkTcodes.Checked=false;
			checkNcodes.Checked=false;
			checkCodeImport.Checked=false;
			checkCodeGroups.Checked=false;
			checkAutocodes.Checked=false;
			checkProcButtons.Checked=false;
			checkApptProcsQuickAdd.Checked=false;
			checkRecallTypes.Checked=false;
		}

		private void butRun_Click(object sender,EventArgs e) {
			if(!checkTcodes.Checked
				&& !checkNcodes.Checked
				&& !checkCodeImport.Checked
				&& !checkCodeGroups.Checked
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
			#region D Codes or CDA Codes
			//After the N codes and before the tools below that need codes to exist, as Open Dental ran it.
			if(checkCodeImport.Checked && RunCodeImport()) {
				Changed=true;
			}
			#endregion
			#region Code Groups
			if(checkCodeGroups.Checked) {
				if(RunCodeGroupReset()) {
					Changed=true;
				}
				else {
					checkCodeGroups.Checked=false;//uncheck and continue if the user cancels
				}
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