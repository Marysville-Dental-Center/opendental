using OpenDentBusiness;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenDental {
	///<summary></summary>
	public partial class FormBenefitEdit : FormODBase {
		///<summary>Set this to the benefit that is going to be edited before loading the window. This object will be manipulated if the user makes changes and clicks OK. Gets set to null if the user clicks Delete.</summary>
		public Benefit BenefitCur;
		private long _planNum;
		private long _patPlanNum;

		///<summary></summary>
		public FormBenefitEdit(long patPlanNum,long planNum) {
			InitializeComponent();
			InitializeLayoutManager();
			_patPlanNum=patPlanNum;
			_planNum=planNum;
			Lan.F(this);
		}

		private void FormBenefitEdit_Load(object sender, System.EventArgs e) {
			if(BenefitCur==null) {
				MessageBox.Show("Benefit cannot be null.");
				DialogResult=DialogResult.Cancel;
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.InsPlanEdit)) {
				butSave.Enabled=false;
			}
			if(BenefitCur.PatPlanNum==0) {//attached to insplan
				checkPat.Checked=false;
			}
			else{
				checkPat.Checked=true;
			}
			List<CovCat> listCovCats=CovCats.GetDeepCopy(true);
			listCategory.Items.Clear();
			listCategory.Items.AddNone<CovCat>();
			listCategory.Items.AddList<CovCat>(listCovCats,x => x.Description);
			int selectedIndex=listCovCats.FindIndex(x => x.CovCatNum==BenefitCur.CovCatNum);
			listCategory.SelectedIndex=selectedIndex+1;//Add one due to the 'none' option always being first.
			textProcCode.Text=ProcedureCodes.GetStringProcCode(BenefitCur.CodeNum);
			CodeGroup codeGroupNone=new CodeGroup();
			codeGroupNone.CodeGroupNum=0;
			codeGroupNone.ProcCodes="";
			codeGroupNone.GroupName=Lans.g(this,"None");
			List<CodeGroup> listCodeGroups=new List<CodeGroup>();
			listCodeGroups.Add(codeGroupNone);
			//Retired (hidden) code groups are not offered, except the one this benefit already uses.
			listCodeGroups.AddRange(CodeGroups.GetForBenefitPicker(BenefitCur.CodeGroupNum));
			comboCodeGroup.Items.Clear();
			comboCodeGroup.Items.AddList(listCodeGroups,x => CodeGroups.GetGroupName(x,isHidden:x.IsHidden));
			comboCodeGroup.SetSelectedKey<CodeGroup>(BenefitCur.CodeGroupNum,x => x.CodeGroupNum,x => CodeGroups.GetGroupName(x));
			listBenefitType.Items.Clear();
			listBenefitType.Items.AddEnums<InsBenefitType>();
			listBenefitType.SetSelectedEnum(BenefitCur.BenefitType);
			if(BenefitCur.Percent==-1) {
				textPercent.Text="";
			}
			else {
				textPercent.Text=BenefitCur.Percent.ToString();
			}
			textAmount.Text=BenefitCur.GetDisplayMonetaryAmt();
			listTimePeriod.Items.Clear();
			listTimePeriod.Items.AddEnums<BenefitTimePeriod>();
			listTimePeriod.SetSelectedEnum(BenefitCur.TimePeriod);
			textQuantity.Text=BenefitCur.Quantity.ToString();
			listQuantityQualifier.Items.Clear();
			listQuantityQualifier.Items.AddEnums<BenefitQuantity>();
			listQuantityQualifier.SetSelectedEnum(BenefitCur.QuantityQualifier);
			listCoverageLevel.Items.Clear();
			listCoverageLevel.Items.AddEnums<BenefitCoverageLevel>();
			listCoverageLevel.SetSelectedEnum(BenefitCur.CoverageLevel);
			listTreatArea.Items.Clear();
			listTreatArea.Items.Add("Default",TreatmentArea.None);
			listTreatArea.Items.AddEnums<TreatmentArea>();
			listTreatArea.Items.RemoveAt(1); // There are now two instances of TreatmentArea.None, remove the one that will display as "None"
			listTreatArea.SetSelectedEnum(BenefitCur.TreatArea);
			textTeeth.Text=Benefits.GetToothRangeDisplay(BenefitCur);
			SetTeethEnabled();
		}

		private void listBenefitType_SelectedIndexChanged(object sender,EventArgs e) {
			SetTeethEnabled();
		}

		///<summary>Teeth (benefit.ToothRange, Open Dental 25.3) only apply to exclusions. Text already typed is kept so the user can switch back.</summary>
		private void SetTeethEnabled() {
			bool isExclusion=listBenefitType.SelectedIndex>-1 && listBenefitType.GetSelected<InsBenefitType>()==InsBenefitType.Exclusions;
			textTeeth.Enabled=isExclusion;
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(BenefitCur.QuantityQualifier==BenefitQuantity.AgeLimit && PIn.Int(textQuantity.Text,false)==0)  {
				string messageText=Lan.g(this,"field is invalid.\r\n"
					+"Enter an age greater than 0 to denote coverage through that year, or click delete to remove this benefit.");
				MessageBox.Show(this,groupQuantity.Text+" "+messageText);
				return;
			}
			if(!textPercent.IsValid() || !textAmount.IsValid() || !textQuantity.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			//Read the UI into variables for validation.
			long covCatNum=listCategory.GetSelectedKey<CovCat>(x => x.CovCatNum);
			long codeNum=ProcedureCodes.GetCodeNum(textProcCode.Text);
			long codeGroupNum=comboCodeGroup.GetSelectedKey<CodeGroup>(x => x.CodeGroupNum);
			InsBenefitType benefitType=listBenefitType.GetSelected<InsBenefitType>();
			BenefitQuantity quantityQualifier=listQuantityQualifier.GetSelected<BenefitQuantity>();
			int percent;
			if(textPercent.Text=="") {
				percent=-1;
			}
			else {
				percent=PIn.Int(textPercent.Text);
			}
			double monetaryAmt;
			if(textAmount.Text=="") {
				monetaryAmt=-1;
			}
			else {
				monetaryAmt=PIn.Double(textAmount.Text);
			}
			byte quantity=PIn.Byte(textQuantity.Text);
			BenefitTimePeriod timePeriod=listTimePeriod.GetSelected<BenefitTimePeriod>();
			BenefitCoverageLevel coverageLevel=listCoverageLevel.GetSelected<BenefitCoverageLevel>();
			TreatmentArea treatArea=(TreatmentArea)listTreatArea.SelectedIndex;
			if(textProcCode.Text!="" && codeNum==0) {
				MsgBox.Show(this,"Invalid procedure code.");
				return;
			}
			string toothRange="";
			if(benefitType==InsBenefitType.Exclusions) {
				try {
					toothRange=Tooth.ParseToothList(textTeeth.Text);
				}
				catch(ApplicationException ex) {
					MessageBox.Show(this,Lan.g(this,"Teeth")+": "+ex.Message);
					return;
				}
			}
			long patPlanNum;
			long planNum;
			if(checkPat.Checked) {
				patPlanNum=_patPlanNum;
				planNum=0;
			}
			else {
				patPlanNum=0;
				planNum=_planNum;
			}
			//Create a new benefit object out of the fields from the UI in order to invoke helper methods.
			//Validate seven extremely specific fields from the UI for limitation benefits in general.
			Benefit benefit=new Benefit();
			benefit.BenefitType=benefitType;
			benefit.MonetaryAmt=monetaryAmt;
			benefit.Percent=percent;
			benefit.QuantityQualifier=quantityQualifier;
			benefit.TimePeriod=timePeriod;
			benefit.CoverageLevel=coverageLevel;
			benefit.TreatArea=treatArea;
			benefit.Quantity=quantity;
			//Fluoride and sealant age limitations need to validate the code, code group, and patPlanNum.
			benefit.CodeGroupNum=codeGroupNum;
			benefit.CodeNum=codeNum;
			benefit.CovCatNum=covCatNum;
			benefit.PatPlanNum=patPlanNum;
			benefit.PlanNum=planNum;
			benefit.ToothRange=toothRange;
			//The same rules the Other Benefits grid uses when a cell is edited in place.
			List<BenefitValidationError> listErrors=Benefits.GetValidationErrors(benefit,_patPlanNum);
			if(listErrors.Count>0) {
				MessageBox.Show(this,listErrors[0].Message);
				return;
			}
			//End of validation. Manipulate BenefitCur with the values from the UI.
			BenefitCur.PatPlanNum=patPlanNum;
			BenefitCur.PlanNum=planNum;
			BenefitCur.CovCatNum=covCatNum;
			BenefitCur.CodeNum=codeNum;
			BenefitCur.CodeGroupNum=codeGroupNum;
			BenefitCur.BenefitType=benefitType;
			BenefitCur.Percent=percent;
			BenefitCur.MonetaryAmt=monetaryAmt;
			BenefitCur.TimePeriod=timePeriod;
			BenefitCur.Quantity=quantity;
			BenefitCur.QuantityQualifier=quantityQualifier;
			BenefitCur.CoverageLevel=coverageLevel;
			BenefitCur.TreatArea=treatArea;
			BenefitCur.ToothRange=toothRange;
			DialogResult=DialogResult.OK;
		}

		private void butDelete_Click(object sender, System.EventArgs e) {
			if(BenefitCur.IsNew) {
				DialogResult=DialogResult.Cancel;
			}
			else {
				DialogResult=DialogResult.OK;
			}
			BenefitCur=null;
		}
	}
}
