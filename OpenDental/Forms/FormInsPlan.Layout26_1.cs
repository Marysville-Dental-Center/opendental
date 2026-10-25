// Edit Insurance Plan, 26.1 layout (backlog package M2-INSPLAN, item v26_1-family-insplan-layout): the four tab pages are gone and every field
// is on screen at once, in fixed panels (FormInsPlan.designer.cs). This file holds the little code the new layout needs, so that
// FormInsPlan.cs only calls it. Saving, validation and the enable and visibility rules of every control are unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormInsPlan {
		///<summary>Set in the constructor outside Canada, where the Canadian fields are hidden. At 96 dpi, how far the plan note moves down into
		///their space (zero when they show).</summary>
		private int _heightCanadianFree96;

		///<summary>Scaled location, in the form's client coordinates, just below the left end of a control inside nested panels and group boxes.
		///The employer and carrier drop-down lists are added to the form itself and open there. Only for the constructor, while every control
		///still has its designer (96 dpi) bounds.</summary>
		private Point GetDropDownLocation26_1(Control control) {
			int x=0;
			int y=control.Height;
			for(Control controlCur=control;controlCur!=null && controlCur!=this;controlCur=controlCur.Parent) {
				x+=controlCur.Left;
				y+=controlCur.Top;
			}
			return new Point(LayoutManager.Scale(x),LayoutManager.Scale(y));
		}

		///<summary>Outside Canada. Hides the Canadian fields (they used to be a tab page that was removed). ApplyLayout26_1 gives their space to
		///the benefits grid. Only for the constructor.</summary>
		private void HideCanadian26_1() {
			groupCanadian.Visible=false;
			_heightCanadianFree96=groupCanadian.Bottom-textPlanNote.Bottom;
		}

		///<summary>The Exclusions options in enum order, because comboExclusionFeeRule_SelectionChangeCommitted casts the selected index. 25.4
		///wording: "Use Standard Provider Fee" for UseUcrFee.</summary>
		private void AddExclusionRuleItems26_1() {
			comboExclusionFeeRule.Items.AddList(Enum.GetValues(typeof(ExclusionRule)).Cast<ExclusionRule>(),x => InsPlanWriteOffL.GetExclusionRuleText(x));
		}

		///<summary>Called at the end of the Load event, after FormODBase has laid out the form, so LayoutManager moves take effect.</summary>
		private void ApplyLayout26_1() {
			//25.4 help text for the write-off overrides, on the labels and on the combo boxes.
			toolTip1.SetToolTip(label11,InsPlanWriteOffL.GetExclusionsHelp());
			toolTip1.SetToolTip(comboExclusionFeeRule,InsPlanWriteOffL.GetExclusionsHelp());
			toolTip1.SetToolTip(labelZeroWriteoffAnnualMax,InsPlanWriteOffL.GetAnnualMaxHelp());
			toolTip1.SetToolTip(comboBoxZeroWriteOffsOnAnnualMaxLimitation,InsPlanWriteOffL.GetAnnualMaxHelp());
			toolTip1.SetToolTip(label39,InsPlanWriteOffL.GetFreqOrAgingHelp());
			toolTip1.SetToolTip(comboZeroWriteOffsOnLimitationOrAging,InsPlanWriteOffL.GetFreqOrAgingHelp());
			toolTip1.SetToolTip(checkClaimsUseUCR,InsPlanWriteOffL.GetClaimsUseStandardFeeHelp());
			if(_heightCanadianFree96<=0) {
				return;
			}
			//Outside Canada: the plan note moves down to the bottom of the column and the benefits grid grows into the space.
			int shift=LayoutManager.Scale(_heightCanadianFree96);
			LayoutManager.MoveLocation(label18,new Point(label18.Left,label18.Top+shift));
			LayoutManager.MoveLocation(butBenefits,new Point(butBenefits.Left,butBenefits.Top+shift));
			LayoutManager.MoveLocation(textPlanNote,new Point(textPlanNote.Left,textPlanNote.Top+shift));
			LayoutManager.MoveHeight(gridBenefits,gridBenefits.Height+shift);
		}

		///<summary>Same as double clicking the benefits grid.</summary>
		private void butBenefits_Click(object sender,EventArgs e) {
			gridBenefits_DoubleClick(sender,e);
		}
	}
}
