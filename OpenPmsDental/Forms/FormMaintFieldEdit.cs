// Routine maintenance (docs/specs/routine-maintenance.md, "Item setup"): one result field of a maintenance item.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Edits a result field in memory: name, type, and for a number its unit and optional limits; required. The item window saves it.</summary>
	public partial class FormMaintFieldEdit:FormODBase {
		private MaintField _maintField;

		public FormMaintFieldEdit(MaintField maintField) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_maintField=maintField;
		}

		private void FormMaintFieldEdit_Load(object sender,EventArgs e) {
			textName.Text=_maintField.FieldName;
			comboType.Items.Clear();
			List<EnumMaintFieldType> listFieldTypes=MaintValues.GetFieldTypesForSetup();
			foreach(EnumMaintFieldType fieldType in listFieldTypes) {
				comboType.Items.Add(Lan.g("enumEnumMaintFieldType",fieldType.GetDescription()),fieldType);
			}
			comboType.SelectedIndex=Math.Max(0,listFieldTypes.IndexOf(_maintField.FieldType));
			textUnit.Text=_maintField.Unit;
			checkMin.Checked=_maintField.HasMin;
			if(_maintField.HasMin) {
				textMin.Text=MaintValues.FormatNumber(_maintField.ValueMin);
			}
			checkMax.Checked=_maintField.HasMax;
			if(_maintField.HasMax) {
				textMax.Text=MaintValues.FormatNumber(_maintField.ValueMax);
			}
			checkRequired.Checked=_maintField.IsRequired;
			SetNumberControls();
		}

		private EnumMaintFieldType GetFieldType() {
			return comboType.SelectedIndex<0 ? EnumMaintFieldType.PassFail : comboType.GetSelected<EnumMaintFieldType>();
		}

		///<summary>The unit and the limits apply to numbers only.</summary>
		private void SetNumberControls() {
			bool isNumber=GetFieldType()==EnumMaintFieldType.Number;
			labelUnit.Enabled=isNumber;
			textUnit.Enabled=isNumber;
			checkMin.Enabled=isNumber;
			checkMax.Enabled=isNumber;
			textMin.Enabled=isNumber && checkMin.Checked;
			textMax.Enabled=isNumber && checkMax.Checked;
		}

		private void comboType_SelectionChangeCommitted(object sender,EventArgs e) {
			SetNumberControls();
		}

		private void checkLimit_Click(object sender,EventArgs e) {
			SetNumberControls();
		}

		private void butSave_Click(object sender,EventArgs e) {
			bool isNumber=GetFieldType()==EnumMaintFieldType.Number;
			double valueMin=0;
			double valueMax=0;
			if(isNumber && checkMin.Checked && !MaintValues.TryParseNumber(textMin.Text,out valueMin)) {
				MsgBox.Show(this,"Please enter a number for the minimum.");
				return;
			}
			if(isNumber && checkMax.Checked && !MaintValues.TryParseNumber(textMax.Text,out valueMax)) {
				MsgBox.Show(this,"Please enter a number for the maximum.");
				return;
			}
			MaintField maintField=_maintField.Copy();
			maintField.FieldName=textName.Text.Trim();
			maintField.FieldType=GetFieldType();
			maintField.Unit=textUnit.Text.Trim();
			maintField.HasMin=checkMin.Checked;
			maintField.ValueMin=valueMin;
			maintField.HasMax=checkMax.Checked;
			maintField.ValueMax=valueMax;
			maintField.IsRequired=checkRequired.Checked;
			MaintValues.ClearUnusedSettings(maintField);
			string error=MaintValues.GetFieldError(maintField);
			if(error!=null) {
				MessageBox.Show(error);
				return;
			}
			_maintField.FieldName=maintField.FieldName;
			_maintField.FieldType=maintField.FieldType;
			_maintField.Unit=maintField.Unit;
			_maintField.HasMin=maintField.HasMin;
			_maintField.ValueMin=maintField.ValueMin;
			_maintField.HasMax=maintField.HasMax;
			_maintField.ValueMax=maintField.ValueMax;
			_maintField.IsRequired=maintField.IsRequired;
			DialogResult=DialogResult.OK;
		}
	}
}
