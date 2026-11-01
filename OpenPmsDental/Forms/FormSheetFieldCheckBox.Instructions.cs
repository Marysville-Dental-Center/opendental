// B-PRINT: the Edit CheckBox window without its long instruction labels (Open Dental 25.3). The guidance is kept as tooltips.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormSheetFieldCheckBox {
		///<summary>Holds the instruction tooltips. Disposed with the window.</summary>
		private ToolTip _toolTipInstructions;

		public const string TIP_RADIO_MISC="Use the same Field Name (misc) and the same Group Name for each radio button in a group.";
		public const string TIP_RADIO="Use the same Field Name for each radio button in a group, but a different Radio Button Value for each.";
		public const string TIP_REQUIRED="The radio buttons of one group must all be required, or all not required.";
		public const string TIP_REPORTABLE="To make misc radio buttons reportable, give each button in the group its own Reportable Name.";
		public const string TIP_YES_NO="Make one checkbox with the Yes value and a second one for the same item with the No value. "
			+"The two act as a radio button group.";

		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);//Raises Load: FormSheetFieldCheckBox_Load fills the window first.
			SetInstructionToolTips();
		}

		private void SetInstructionToolTips() {
			_toolTipInstructions=new ToolTip();
			_toolTipInstructions.AutoPopDelay=20000;
			_toolTipInstructions.InitialDelay=300;
			Disposed+=(sender,e) => _toolTipInstructions?.Dispose();
			SetTip(groupRadioMisc,TIP_RADIO_MISC);
			SetTip(comboRadioGroupNameMisc,TIP_RADIO_MISC);
			SetTip(groupRadio,TIP_RADIO);
			SetTip(listRadio,TIP_RADIO);
			SetTip(checkRequired,TIP_REQUIRED);
			SetTip(labelRequired,TIP_REQUIRED);
			SetTip(textReportableName,TIP_REPORTABLE);
			SetTip(labelReportableName,TIP_REPORTABLE);
			SetTip(labelMiscInstructions,TIP_REPORTABLE);
			SetTip(radioYes,TIP_YES_NO);
			SetTip(radioNo,TIP_YES_NO);
			SetTip(labelYesNo,TIP_YES_NO);
		}

		private void SetTip(Control control,string text) {
			if(control==null) {
				return;
			}
			_toolTipInstructions.SetToolTip(control,Lan.g(this,text));
		}
	}
}
