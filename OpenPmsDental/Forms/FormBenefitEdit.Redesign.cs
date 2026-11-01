using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>The Open Dental 25.2 layout of the Edit Benefit window: Applies to (category, procedure code or code group, one at a time), inputs
	///enabled by benefit type, and a plain-language preview of the benefit. The rules are in BenefitEditRules.</summary>
	public partial class FormBenefitEdit {
		///<summary>The benefit as the window opened it. Values it already had, with the same type, may be saved even where the type ignores them.
		///</summary>
		private Benefit _benefitLoaded;
		///<summary>False until Load has filled every input, so change events don't build the preview from a half-filled window.</summary>
		private bool _isRedesignLoaded;

		///<summary>Called at the end of Load.</summary>
		private void LoadRedesign() {
			_benefitLoaded=BenefitCur.Copy();
			if(BenefitCur.CodeNum!=0) {
				radioProcCode.Checked=true;
			}
			else if(BenefitCur.CodeGroupNum!=0) {
				radioCodeGroup.Checked=true;
			}
			else {
				radioCategory.Checked=true;//Category "None" is a general benefit.
			}
			radioCategory.CheckedChanged+=radioAppliesTo_CheckedChanged;
			radioProcCode.CheckedChanged+=radioAppliesTo_CheckedChanged;
			radioCodeGroup.CheckedChanged+=radioAppliesTo_CheckedChanged;
			checkPat.CheckedChanged+=input_Changed;
			listCategory.SelectedIndexChanged+=input_Changed;
			comboCodeGroup.SelectedIndexChanged+=input_Changed;
			listTimePeriod.SelectedIndexChanged+=input_Changed;
			listQuantityQualifier.SelectedIndexChanged+=input_Changed;
			listCoverageLevel.SelectedIndexChanged+=input_Changed;
			listTreatArea.SelectedIndexChanged+=input_Changed;
			textProcCode.TextChanged+=input_Changed;
			textPercent.TextChanged+=input_Changed;
			textAmount.TextChanged+=input_Changed;
			textQuantity.TextChanged+=input_Changed;
			textTeeth.TextChanged+=input_Changed;
			_isRedesignLoaded=true;
			SetAppliesToEnabled();
			SetInputsEnabledForType();
		}

		private void radioAppliesTo_CheckedChanged(object sender,EventArgs e) {
			SetAppliesToEnabled();
			FillPreview();
		}

		private void input_Changed(object sender,EventArgs e) {
			FillPreview();
		}

		///<summary>Only the input picked under Applies to is enabled, so category, procedure code and code group are mutually exclusive.</summary>
		private void SetAppliesToEnabled() {
			listCategory.Enabled=radioCategory.Checked;
			textProcCode.Enabled=radioProcCode.Checked;
			comboCodeGroup.Enabled=radioCodeGroup.Checked;
		}

		///<summary>Enables the inputs the selected type uses (BenefitEditRules.IsFieldUsed). An input that holds a value stays enabled even when the
		///type ignores it, so the user can see and clear it; saving then names the field. Teeth keep their own rule (SetTeethEnabled). Time period and
		///coverage level apply to every type.</summary>
		private void SetInputsEnabledForType() {
			if(!_isRedesignLoaded) {
				return;
			}
			InsBenefitType insBenefitType=listBenefitType.GetSelected<InsBenefitType>();
			bool isPercentOn=BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Percent) || textPercent.Text.Trim()!="";
			textPercent.Enabled=isPercentOn;
			labelPercent.Enabled=isPercentOn;
			bool isAmountOn=BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Amount) || textAmount.Text.Trim()!="";
			textAmount.Enabled=isAmountOn;
			labelAmount.Enabled=isAmountOn;
			int.TryParse(textQuantity.Text.Trim(),out int quantity);
			textQuantity.Enabled=BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Quantity) || quantity!=0;
			bool isQualifierOn=BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Qualifier)
				|| (listQuantityQualifier.SelectedIndex>0);//Index 0 is None.
			listQuantityQualifier.Enabled=isQualifierOn;
			label8.Enabled=isQualifierOn;
			bool isTreatAreaOn=BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.TreatArea) || listTreatArea.SelectedIndex>0;//0 is Default
			listTreatArea.Enabled=isTreatAreaOn;
			labelTreatmentArea.Enabled=isTreatAreaOn;
			FillPreview();
		}

		///<summary>Keeps only the value of the input picked under Applies to. Returns false, after a message, when the picked input is empty.</summary>
		private bool ReadAppliesTo(ref long covCatNum,ref long codeNum,ref long codeGroupNum) {
			if(radioProcCode.Checked) {
				if(textProcCode.Text.Trim()=="") {
					MsgBox.Show(this,"Enter a procedure code, or pick another choice under Applies to.");
					return false;
				}
				covCatNum=0;
				codeGroupNum=0;
				return true;
			}
			if(radioCodeGroup.Checked) {
				if(codeGroupNum==0) {
					MsgBox.Show(this,"Pick a code group, or pick another choice under Applies to.");
					return false;
				}
				covCatNum=0;
				codeNum=0;
				return true;
			}
			codeNum=0;
			codeGroupNum=0;
			return true;
		}

		///<summary>Shows what the window would save, in words. Values that don't parse yet are left out.</summary>
		private void FillPreview() {
			if(!_isRedesignLoaded) {
				return;
			}
			Benefit benefit=new Benefit();
			if(listBenefitType.SelectedIndex>-1) {
				benefit.BenefitType=listBenefitType.GetSelected<InsBenefitType>();
			}
			if(int.TryParse(textPercent.Text.Trim(),out int percent)) {
				benefit.Percent=percent;
			}
			if(double.TryParse(textAmount.Text.Trim(),out double amount)) {
				benefit.MonetaryAmt=amount;
			}
			if(byte.TryParse(textQuantity.Text.Trim(),out byte quantity)) {
				benefit.Quantity=quantity;
			}
			benefit.TimePeriod=listTimePeriod.GetSelected<BenefitTimePeriod>();
			benefit.QuantityQualifier=listQuantityQualifier.GetSelected<BenefitQuantity>();
			benefit.CoverageLevel=listCoverageLevel.GetSelected<BenefitCoverageLevel>();
			if(listTreatArea.SelectedIndex>0) {
				benefit.TreatArea=(TreatmentArea)listTreatArea.SelectedIndex;
			}
			string categoryName="";
			if(radioCategory.Checked && listCategory.SelectedIndex>0) {
				categoryName=listCategory.GetSelected<CovCat>()?.Description??"";
			}
			string procCode="";
			if(radioProcCode.Checked) {
				procCode=textProcCode.Text;
			}
			string codeGroupName="";
			long codeGroupNum=comboCodeGroup.GetSelectedKey<CodeGroup>(x => x.CodeGroupNum);//Also a retired group that is not in the list.
			if(radioCodeGroup.Checked && codeGroupNum!=0) {
				codeGroupName=CodeGroups.GetGroupName(codeGroupNum);
			}
			labelPreview.Text=BenefitEditRules.GetPreviewText(benefit,categoryName,procCode,codeGroupName,textTeeth.Text,checkPat.Checked);
		}
	}
}
