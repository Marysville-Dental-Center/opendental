using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using System.Linq;
using CodeBase;

namespace OpenPmsDental {
	public partial class FormAutomationConditionEdit:FormODBase {
		public bool IsNew;
		public AutomationCondition AutomationConditionCur;
		///<summary>The trigger currently chosen in the parent Edit Automation window, or null when unknown. The 26.1 field "procedure clinic differs
		///from patient clinic" is listed only for the ProcedureComplete trigger (or when this condition already uses it).</summary>
		public EnumAutomationTrigger? AutomationTriggerCur;
		///<summary>The fields in listCompareField, in order. Not every field is listed for every trigger, so the list index is not the enum value.</summary>
		private List<AutoCondField> _listAutoCondFields=new List<AutoCondField>();

		public FormAutomationConditionEdit() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormAutomationConditionEdit_Load(object sender,EventArgs e) {
			for(int i=0;i<Enum.GetValues(typeof(AutoCondField)).Length;i++) {
				if(!IsFieldListed((AutoCondField)i)) {
					continue;
				}
				_listAutoCondFields.Add((AutoCondField)i);
				listCompareField.Items.Add(Lan.g("enumAutoCondField",((AutoCondField)i).GetDescription()));
				listCompareField.SelectedIndex=0;
			}
			for(int i=0;i<Enum.GetNames(typeof(AutoCondComparison)).Length;i++) {
				if(Enum.GetNames(typeof(AutoCondComparison))[i]=="None") {//Skip none as an option for the comparison field
					continue;
				}
				listComparison.Items.Add(Lan.g("enumAutoCondComparison",Enum.GetNames(typeof(AutoCondComparison))[i]));
				listComparison.SelectedIndex=0;
			}
			if(IsNew) {
				return;
			}
			textCompareString.Text=AutomationConditionCur.CompareString;
			listCompareField.SelectedIndex=_listAutoCondFields.IndexOf(AutomationConditionCur.CompareField);
			if(!AutomationConditionCur.CompareField.In(AutoCondField.InsuranceNotEffective,AutoCondField.IsControlled,AutoCondField.IsProcRequired,
				AutoCondField.IsPatientInstructionPresent,AutoCondField.PatientProcedureClinicMismatch))
			{
				listComparison.SelectedIndex=(int)AutomationConditionCur.Comparison;
			}
			ShowOrHideFields(GetFieldSelected());
		}

		///<summary>False only for the 26.1 clinic mismatch field when the parent automation's trigger is not ProcedureComplete, unless this
		///condition already uses it (so it can be seen and changed).</summary>
		private bool IsFieldListed(AutoCondField autoCondField) {
			if(autoCondField!=AutoCondField.PatientProcedureClinicMismatch) {
				return true;
			}
			if(!IsNew && AutomationConditionCur!=null && AutomationConditionCur.CompareField==autoCondField) {
				return true;
			}
			return AutomationTriggerCur!=null && AutomationClinicMismatches.IsTriggerAllowed(AutomationTriggerCur.Value);
		}

		///<summary>The field selected in listCompareField. The first listed field when nothing is selected.</summary>
		private AutoCondField GetFieldSelected() {
			if(listCompareField.SelectedIndex<0 || listCompareField.SelectedIndex>=_listAutoCondFields.Count) {
				return _listAutoCondFields[0];
			}
			return _listAutoCondFields[listCompareField.SelectedIndex];
		}

		///<summary>Show or hides the form fileds based of needs.</summary>
		private void ShowOrHideFields(AutoCondField autoCondField) {
			if(autoCondField==AutoCondField.BillingType || autoCondField==AutoCondField.ClaimContainsProcCode) {
				butSelect.Visible=true;
				labelCompareString.Text=Lan.g(this,"Text (Type or pick from list) ");
			}
			else {
				butSelect.Visible=false;
				labelCompareString.Text=Lan.g(this,"Text");
			}
			if(autoCondField.In(AutoCondField.InsuranceNotEffective,AutoCondField.IsControlled,AutoCondField.IsProcRequired
				,AutoCondField.IsPatientInstructionPresent,AutoCondField.PatientProcedureClinicMismatch))
			{//Hide fields
				labelWarning.Visible=true;
				if(autoCondField==AutoCondField.PatientProcedureClinicMismatch) {
					labelWarning.Text=Lan.g(this,"This automation field will only work when using the ProcedureComplete trigger.  It is true when a completed "
						+"procedure's clinic is not the patient's clinic.  It is never true when clinics are turned off.");
				}
				else if(autoCondField==AutoCondField.InsuranceNotEffective) {
					labelWarning.Text="At any point in time this rule is triggered, the current date and time will be compared to the "
						+"effective date range of the current patient's primary insurance.  If it does not fall within the range then the "
						+"action for this automation will be triggered.";
				}
				else {
					labelWarning.Text="This automation field will only work when using the RxCreate trigger.";
				}
				labelComparison.Visible=false;
				labelCompareString.Visible=false;
				listComparison.Visible=false;
				textCompareString.Visible=false;
			}
			else if(autoCondField==AutoCondField.ClaimContainsProcCode) {
				labelWarning.Visible=true;
				labelCompareString.Visible=true;
				textCompareString.Visible=true;
				labelComparison.Visible=false;
				listComparison.Visible=false;
				labelWarning.Text="This automation field will only work when using the CreateClaim or OpenClaim trigger.";
			}
			else {//Show fields
				labelWarning.Visible=false;
				labelComparison.Visible=true;
				labelCompareString.Visible=true;
				listComparison.Visible=true;
				textCompareString.Visible=true;
			}
		}

		///<summary>Logic might get more complex as fields and comparisons are added so a seperate function was made.</summary>
		private bool ReasonableLogic() {
			AutoCondComparison autoCondComparison=(AutoCondComparison)listComparison.SelectedIndex;
			AutoCondField autoCondField=GetFieldSelected();
			//So far Age is only thing that allows GreaterThan or LessThan.
			if(autoCondField!=AutoCondField.Age) {
				if(autoCondComparison==AutoCondComparison.GreaterThan || autoCondComparison==AutoCondComparison.LessThan) {
					return false;
				}
			}
			else {
				int age;
				//Make sure that the user typed in an integer and not a word.
				if(!int.TryParse(textCompareString.Text,out age)) {
					return false;
				}
			}
			return true;
		}

		private void listCompareField_Click(object sender,EventArgs e) {
			ShowOrHideFields(GetFieldSelected());
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(IsNew) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete this condition?")) {
				return;
			}
			try {
				AutomationConditions.Delete(AutomationConditionCur.AutomationConditionNum);
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			DialogResult=DialogResult.OK;
		}

		private void butSelect_Click(object sender,EventArgs e) {
			if(GetFieldSelected()==AutoCondField.BillingType) {
				using FormDefinitionPicker formDefinitionPicker=new FormDefinitionPicker(DefCat.BillingTypes);
				formDefinitionPicker.HasShowHiddenOption=false;
				formDefinitionPicker.IsMultiSelectionMode=false;
				formDefinitionPicker.ShowDialog();
				if(formDefinitionPicker.DialogResult==DialogResult.OK) {
					if(formDefinitionPicker.ListDefsSelected.Count==0) {
						textCompareString.Text="";
						return;
					}
					textCompareString.Text=formDefinitionPicker.ListDefsSelected?[0]?.ItemName??"";
				}
			}
			else if(GetFieldSelected()==AutoCondField.ClaimContainsProcCode) {
				using FormProcCodes formProcCodes=new FormProcCodes();
				formProcCodes.IsSelectionMode=true;
				formProcCodes.ShowDialog();
				if(formProcCodes.DialogResult==DialogResult.OK) {
					textCompareString.Text=ProcedureCodes.GetStringProcCode(formProcCodes.CodeNumSelected);
				}
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(GetFieldSelected()==AutoCondField.InsuranceNotEffective
				|| GetFieldSelected()==AutoCondField.IsProcRequired
				|| GetFieldSelected()==AutoCondField.IsControlled
				|| GetFieldSelected()==AutoCondField.IsPatientInstructionPresent
				|| GetFieldSelected()==AutoCondField.PatientProcedureClinicMismatch) //26.1: no comparison, no text
			{
				AutomationConditionCur.CompareString="";
				AutomationConditionCur.Comparison=AutoCondComparison.None;
			}
			else if(GetFieldSelected()==AutoCondField.ClaimContainsProcCode) {
				if(textCompareString.Text.Contains(" ")){
					MsgBox.Show(this,"Procedure codes cannot contain any spaces.");
					return;
				}
				if(textCompareString.Text.Trim()=="") {
					MsgBox.Show(this,"Please enter a valid procedure code first.");
					return;
				}
				if(!ProcedureCodes.IsValidCode(textCompareString.Text)) {
					MsgBox.Show(this,"Procedure code is not valid");
					return;
				}
				AutomationConditionCur.CompareString=textCompareString.Text;
				AutomationConditionCur.Comparison=AutoCondComparison.None;
			}
			else {
				if(textCompareString.Text.Trim()=="") {
					MsgBox.Show(this,"Text not allowed to be blank.");
					return;
				}
				if(!ReasonableLogic()) {
					MsgBox.Show(this,"Comparison does not make sense with chosen field.");
					return;
				}
				if(GetFieldSelected()==AutoCondField.Gender
					&& !(textCompareString.Text.ToLower()=="m" || textCompareString.Text.ToLower()=="f")) 
				{
					MsgBox.Show(this,"Allowed gender values are M or F.");
					return;
				}
				AutomationConditionCur.CompareString=textCompareString.Text;
				AutomationConditionCur.Comparison=(AutoCondComparison)listComparison.SelectedIndex;
			}
			AutomationConditionCur.CompareField=GetFieldSelected();
			if(IsNew) {
				AutomationConditions.Insert(AutomationConditionCur);
			}
			else {
				AutomationConditions.Update(AutomationConditionCur);
			}
			DialogResult=DialogResult.OK;
		}

	}
}