using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class UserControlTreatPlanGeneral:UserControl {

		#region Fields - Private
		private List<Def> _listDefsNegAdjTypes;
		#endregion Fields - Private

		#region Fields - Public
		public bool Changed;
		#endregion Fields - Public

		#region Constructors
		public UserControlTreatPlanGeneral() {
			InitializeComponent();
			Font=LayoutManagerForms.FontInitial;
		}
		#endregion Constructors

		#region Methods - Event Handlers
		private void UserControlTreatPlanGeneral_Load(object sender,EventArgs e) {
			if(PrefC.RandomKeys) {
				groupTreatPlanSort.Visible=false;
			}
		}

		private void radioTreatPlanSortOrder_Click(object sender,EventArgs e) {
			//Sort by order is a false 
			if(PrefC.GetBool(PrefName.TreatPlanSortByTooth)==radioTreatPlanSortOrder.Checked) {
				MsgBox.Show(this,"You will need to restart the program for the change to take effect.");
			}
		}

		private void radioTreatPlanSortTooth_Click(object sender,EventArgs e) {
			if(PrefC.GetBool(PrefName.TreatPlanSortByTooth)!=radioTreatPlanSortTooth.Checked) {
				MsgBox.Show(this,"You will need to restart the program for the change to take effect.");
			}
		}
		#endregion Methods - Event Handlers

		#region Methods - Public
		public void FillTreatPlanGeneral() {
			textDiscountPercentage.Text=PrefC.GetDouble(PrefName.TreatPlanDiscountPercent).ToString();
			_listDefsNegAdjTypes=Defs.GetNegativeAdjTypes();
			comboProcDiscountType.Items.AddDefs(_listDefsNegAdjTypes);
			comboProcDiscountType.SetSelectedDefNum(PrefC.GetLong(PrefName.TreatPlanDiscountAdjustmentType));
			textTreatNote.Text=PrefC.GetString(PrefName.TreatmentPlanNote);
			checkTreatPlanShowCompleted.Checked=PrefC.GetBool(PrefName.TreatPlanShowCompleted);
			if(Clinics.IsMedicalPracticeOrClinic(Clinics.ClinicNum)) {
				checkTreatPlanShowCompleted.Visible=false;
			}
			else {
				checkTreatPlanShowCompleted.Checked=PrefC.GetBool(PrefName.TreatPlanShowCompleted);
			}
			checkTreatPlanItemized.Checked=PrefC.GetBool(PrefName.TreatPlanItemized);
			checkTPSaveSigned.Checked=PrefC.GetBool(PrefName.TreatPlanSaveSignedToPdf);
			radioTreatPlanSortTooth.Checked=PrefC.GetBool(PrefName.TreatPlanSortByTooth) || PrefC.RandomKeys;
			groupTreatPlanSort.Enabled=!PrefC.RandomKeys;
			checkPromptSaveTP.Checked=PrefC.GetBool(PrefName.TreatPlanPromptSave);
			//Open Dental 26.1: the Insurance History rows and codes are set on code groups (Setup, Code Groups). The ten InsHist*Codes preferences are
			//kept in the database but no longer shown or read.
		}

		public bool SaveTreatPlanGeneral() {
			float percent=0;
			if(!float.TryParse(textDiscountPercentage.Text,out percent)) {
				MsgBox.Show(this,"Procedure discount percent is invalid. Please enter a valid number to continue.");
				return false;
			}
			if(PrefC.GetString(PrefName.TreatmentPlanNote)!=textTreatNote.Text) {
				List<long> listTreatPlanNums=TreatPlans.GetNumsByNote(PrefC.GetString(PrefName.TreatmentPlanNote));//Find active/inactive TP's that match exactly.
				if(listTreatPlanNums.Count>0) {
					DialogResult dr=MessageBox.Show(Lan.g(this,"Unsaved treatment plans found with default notes")+": "+listTreatPlanNums.Count+"\r\n"
						+Lan.g(this,"Would you like to change them now?"),"",MessageBoxButtons.YesNoCancel);
					switch(dr) {
						case DialogResult.Cancel:
							return false;
						case DialogResult.Yes:
						case DialogResult.OK:
							TreatPlans.UpdateNotes(textTreatNote.Text,listTreatPlanNums);//change tp notes
							break;
						default://includes "No"
							//do nothing
							break;
					}
				}
			}
			if(comboProcDiscountType.SelectedIndex!=-1) {
				Changed|=Prefs.UpdateLong(PrefName.TreatPlanDiscountAdjustmentType,comboProcDiscountType.GetSelectedDefNum());
			}
			Changed|=Prefs.UpdateString(PrefName.TreatmentPlanNote,textTreatNote.Text);
			Changed|=Prefs.UpdateBool(PrefName.TreatPlanShowCompleted,checkTreatPlanShowCompleted.Checked);
			Changed|=Prefs.UpdateDouble(PrefName.TreatPlanDiscountPercent,percent);
			Changed|=Prefs.UpdateBool(PrefName.TreatPlanItemized,checkTreatPlanItemized.Checked);
			Changed|=Prefs.UpdateBool(PrefName.TreatPlanSaveSignedToPdf,checkTPSaveSigned.Checked);
			Changed|=Prefs.UpdateBool(PrefName.TreatPlanSortByTooth,radioTreatPlanSortTooth.Checked || PrefC.RandomKeys);
			Changed|=Prefs.UpdateBool(PrefName.TreatPlanPromptSave, checkPromptSaveTP.Checked);
			return true;
		}
		#endregion Methods - Public
	}
}
