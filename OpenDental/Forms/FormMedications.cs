using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	/// <summary>
	/// Summary description for FormBasicTemplate.
	/// </summary>
	public partial class FormMedications : FormODBase {
		public bool IsSelectionMode;
		///<summary>the number returned if using select mode.</summary>
		public long SelectedMedicationNum;
		///<summary>Selection mode only.  When true, the Show Hidden box is offered so a hidden medication can be picked, as the merge tool needs.
		///Every other picker leaves this false: hidden medications are not offered for patients, allergies, EHR rules or reconciles.</summary>
		public bool CanSelectHidden;
		private Timer _timer=new Timer();

		///<summary>The list of all medications.  Open Dental 25.3 removed the tab of eRx medications that had no matching generic or brand.</summary>
		public FormMedications() {
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormMedications_Load(object sender, System.EventArgs e) {
			//Show Hidden is offered in the master list, and in selection mode only to callers that allow hidden medications (the merge tool).
			checkShowHidden.Visible=(!IsSelectionMode || CanSelectHidden);
			FillGridAllMedications();
			if(IsSelectionMode){
				this.Text=Lan.g(this,"Select Medication");
			}
			else{
				butOK.Visible=false;
			}
			//Results refresh once typing pauses for half a second.
			_timer.Interval=500;
			_timer.Tick+=_timer_Tick;
		}

		///<summary>Forces cursor to start in the search textbox.</summary>
		private void FormMedications_Shown(object sender,EventArgs e) {
			textSearch.Focus();//We've previously had issues with the tabindex not working.
		}

		private void _timer_Tick(object sender,EventArgs e) {
			_timer.Stop();
			FillGridAllMedications();
		}

		private void checkShowHidden_Click(object sender,EventArgs e) {
			FillGridAllMedications();
		}

		///<summary>True when hidden medications are listed in the All Medications grid.</summary>
		private bool IsShowingHidden() {
			if(IsSelectionMode && !CanSelectHidden) {
				return false;
			}
			return checkShowHidden.Checked;
		}

		private void FillGridAllMedications(bool shouldRetainSelection=true){
			Medication medicationSelected=null;
			if(shouldRetainSelection && gridAllMedications.GetSelectedIndex()!=-1) {
				medicationSelected=(Medication)gridAllMedications.ListGridRows[gridAllMedications.GetSelectedIndex()].Tag;
			}
			List <long> listInUseMedicationNums=Medications.GetAllInUseMedicationNums();
			int sortColIndex=gridAllMedications.GetSortedByColumnIdx();
			bool isSortAscending=gridAllMedications.IsSortedAscending();
			gridAllMedications.BeginUpdate();
			gridAllMedications.Columns.Clear();
			//The order of these columns is important.  See gridAllMedications_CellClick()
			GridColumn col=new GridColumn(Lan.g(this,"Drug Name"),120,GridSortingStrategy.StringCompare);
			gridAllMedications.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Generic Name"),120,GridSortingStrategy.StringCompare);
			gridAllMedications.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"InUse"),55,HorizontalAlignment.Center,GridSortingStrategy.StringCompare);
			gridAllMedications.Columns.Add(col);
			if(CultureInfo.CurrentCulture.Name.EndsWith("US")) {//United States
				col=new GridColumn(Lan.g(this,"RxNorm"),70,GridSortingStrategy.StringCompare);
				gridAllMedications.Columns.Add(col);
			}
			col=new GridColumn(Lan.g(this,"Notes for Generic"),250,GridSortingStrategy.StringCompare);
			gridAllMedications.Columns.Add(col);
			bool isShowingHidden=IsShowingHidden();
			if(isShowingHidden) {
				//Last column, so the RxNorm column index used by the click handlers does not move.
				col=new GridColumn(Lan.g(this,"Hidden"),50,HorizontalAlignment.Center,GridSortingStrategy.StringCompare);
				gridAllMedications.Columns.Add(col);
			}
			gridAllMedications.ListGridRows.Clear();
			//Words in any order, a brand by its generic's name, and an RxNorm code when the text is all digits (Medications.IsSearchMatchWords).
			List <Medication> listMedications=Medications.GetListForPicker(textSearch.Text,includeHidden:isShowingHidden);
			for(int i=0;i<listMedications.Count;i++) {
				GridRow row=new GridRow();
				row.Tag=listMedications[i];
				if(listMedications[i].MedicationNum==listMedications[i].GenericNum) {//isGeneric
					row.Cells.Add(listMedications[i].MedName);
					row.Cells.Add("");
				}
				else{
					row.Cells.Add(listMedications[i].MedName);
					row.Cells.Add(Medications.GetGenericName(listMedications[i].GenericNum));
				}
				if(listInUseMedicationNums.Contains(listMedications[i].MedicationNum)) {
					row.Cells.Add("X");//InUse
				}
				else {
					row.Cells.Add("");//InUse
				}
				if(CultureInfo.CurrentCulture.Name.EndsWith("US")) {//United States
					if(listMedications[i].RxCui==0) {
						row.Cells.Add(Lan.g(this,"(select)"));
						row.Cells[row.Cells.Count-1].Bold=YN.Yes;
					}
					else {
						row.Cells.Add(listMedications[i].RxCui.ToString());
					}
				}
				row.Cells.Add(listMedications[i].Notes);
				if(isShowingHidden) {
					row.Cells.Add(listMedications[i].IsHidden ? "X" : "");
				}
				if(listMedications[i].IsHidden) {
					row.ColorText=System.Drawing.Color.Gray;
				}
				gridAllMedications.ListGridRows.Add(row);
			}
			gridAllMedications.EndUpdate();
			if(sortColIndex>=gridAllMedications.Columns.Count) {
				sortColIndex=-1;//Was sorted by the Hidden column, which Show Hidden just removed.
			}
			gridAllMedications.SortForced(sortColIndex,isSortAscending);
			if(medicationSelected!=null) {//Will be null if nothing is selected.
				for(int i=0;i<gridAllMedications.ListGridRows.Count;i++) {
					Medication medication=(Medication)gridAllMedications.ListGridRows[i].Tag;
					if(medication.MedicationNum==medicationSelected.MedicationNum) {
						gridAllMedications.SetSelected(i,true);
						break;
					}
				}
			}
		}

		private void butAddGeneric_Click(object sender, System.EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.MedicationDefEdit)) {
				return;
			}
			Medication medication=new Medication();
			Medications.Insert(medication);//so that we will have the primary key
			medication.GenericNum=medication.MedicationNum;
			using FormMedicationEdit formMedicationEdit=new FormMedicationEdit();
			formMedicationEdit.MedicationCur=medication;
			formMedicationEdit.IsNew=true;
			formMedicationEdit.ShowDialog();//This window refreshes the Medication cache if the user clicked OK.
			FillGridAllMedications();
		}

		private void butAddBrand_Click(object sender, System.EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.MedicationDefEdit)) {
				return;
			}
			if(gridAllMedications.GetSelectedIndex()==-1){
				MessageBox.Show(Lan.g(this,"You must first highlight the generic medication from the list.  If it is not already on the list, then you must add it first."));
				return;
			}
			Medication medicationSelected=(Medication)gridAllMedications.ListGridRows[gridAllMedications.GetSelectedIndex()].Tag;
			if(medicationSelected.MedicationNum!=medicationSelected.GenericNum){
				MessageBox.Show(Lan.g(this,"The selected medication is not generic."));
				return;
			}
			Medication medication=new Medication();
			Medications.Insert(medication);//so that we will have the primary key
			medication.GenericNum=medicationSelected.MedicationNum;
			using FormMedicationEdit formMedicationEdit=new FormMedicationEdit();
			formMedicationEdit.MedicationCur=medication;
			formMedicationEdit.IsNew=true;
			formMedicationEdit.ShowDialog();//This window refreshes the Medication cache if the user clicked OK.
			FillGridAllMedications();
		}
		
		private void butImportMedications_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.MedicationDefEdit)) {
				return;
			}
			List<Medication> listMedicationImports=new List<Medication>();
			string fileName;
			fileName=GetFilenameFromUser(true);
			if(fileName.IsNullOrEmpty()) {
				return;
			}
			Cursor=Cursors.WaitCursor;
			try {
				listMedicationImports=MedicationL.GetMedicationsFromFile(fileName);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				string msg=Lans.g(this,"Error accessing file. Close all programs using file and try again.");
				MessageBox.Show(this,msg+"\r\n: "+ex.Message);
				return;
			}
			int countImportedMedications=MedicationL.ImportMedications(listMedicationImports,Medications.GetList());
			int countDuplicateMedications=listMedicationImports.Count-countImportedMedications;
			DataValid.SetInvalid(InvalidType.Medications);
			Cursor=Cursors.Default;
			MessageBox.Show(this,POut.Int(countDuplicateMedications)+" "+Lan.g(this,"duplicate medications found.")+"\r\n"
				+POut.Int(countImportedMedications)+" "+Lan.g(this,"medications imported."));
			FillGridAllMedications();
		}

		private void butExportMedications_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.MedicationDefEdit)) {
				return;
			}
			int countExportedMeds=0;
			string fileName;
			//Prompt for file.
			fileName=GetFilenameFromUser(false);
			if(string.IsNullOrEmpty(fileName)) {
				return;
			}
			Cursor=Cursors.WaitCursor;
			try {
				countExportedMeds=MedicationL.ExportMedications(fileName,Medications.GetList());
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				string msg=Lans.g(this,"Error: ");
				MessageBox.Show(this,msg+": "+ex.Message);
			}
			Cursor=Cursors.Default;
			MessageBox.Show(this,POut.Int(countExportedMeds)+" "+Lan.g(this,"medications exported to:")+" "+fileName);
		}

		///<summary>When isImport is true, prompts users to select file and returns the full file path if OK clicked, otherwise an empty string.
		///When isImport is false (export), prompts user to select a file destination if OK clicked, otherwise an empty string..</summary>
		private string GetFilenameFromUser(bool isImport) {
			Cursor=Cursors.WaitCursor;
			string initialDirectory="";
			if(Directory.Exists(PrefC.GetString(PrefName.DocPath))) {
				initialDirectory=PrefC.GetString(PrefName.DocPath);
			}
			else if(Directory.Exists("C:\\")) {
				initialDirectory="C:\\";
			}
			FileDialog fileDialog=null;
			if(isImport) {
				fileDialog=new OpenFileDialog();
			}
			else {//Export
				fileDialog=new SaveFileDialog();
				fileDialog.Filter="(*.txt)|*.txt|All files (*.*)|*.*";
				fileDialog.FilterIndex=1;
				fileDialog.DefaultExt=".txt";
				fileDialog.FileName="ExportedMedications.txt";
			}
			fileDialog.InitialDirectory=initialDirectory;
			if(fileDialog.ShowDialog()!=DialogResult.OK) {
				fileDialog.Dispose();
				fileDialog=null;
			}
			else if(isImport && !File.Exists(fileDialog.FileName)){
				MsgBox.Show(this,"Error accessing file.");
				fileDialog.Dispose();
				fileDialog=null;
			}
			Cursor=Cursors.Default;
			string fileName="";
			if(fileDialog!=null) {
				fileName=fileDialog.FileName;
				fileDialog.Dispose();
			}
			return fileName;
		}
		
		private void textSearch_TextChanged(object sender,EventArgs e) {
			_timer.Stop();
			_timer.Start();
		}

		private void gridAllMedications_CellClick(object sender,ODGridClickEventArgs e) {
			Medication medication=(Medication)gridAllMedications.ListGridRows[e.Row].Tag;
			if(CultureInfo.CurrentCulture.Name.EndsWith("US") && e.Col==3) {//United States RxNorm Column
				using FormRxNorms formRxNorms=new FormRxNorms();
				formRxNorms.IsSelectionMode=true;
				formRxNorms.InitSearchCodeOrDescript=medication.MedName;
				formRxNorms.ShowDialog();
				if(formRxNorms.DialogResult==DialogResult.OK) {
					medication.RxCui=PIn.Long(formRxNorms.RxNormSelected.RxCui);
					//The following behavior mimics FormMedicationEdit OK click.
					Medications.Update(medication);
					MedicationPats.UpdateRxCuiForMedication(medication.MedicationNum,medication.RxCui);
					DataValid.SetInvalid(InvalidType.Medications);
				}
				FillGridAllMedications();
			}
		}

		private void gridAllMedications_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			Medication medication=(Medication)gridAllMedications.ListGridRows[e.Row].Tag;
			medication=Medications.GetMedication(medication.MedicationNum);
			if(medication==null) {//Possible to delete the medication from a separate WS while medication loaded in memory.
				MsgBox.Show(this,"An error occurred loading medication.");
				return;
			}
			if(IsSelectionMode){
				SelectedMedicationNum=medication.MedicationNum;
				DialogResult=DialogResult.OK;
			}
			else{//normal mode from main menu
				if(!CultureInfo.CurrentCulture.Name.EndsWith("US") || e.Col!=3) {//Not United States RxNorm Column
					using FormMedicationEdit formMedicationEdit=new FormMedicationEdit();
					formMedicationEdit.MedicationCur=medication;
					formMedicationEdit.ShowDialog();//This window refreshes the Medication cache if the user clicked OK.
					FillGridAllMedications();
				}
			}
		}

		private void butOK_Click(object sender, System.EventArgs e) {
			//this button is not visible if not selection mode.
			if(gridAllMedications.GetSelectedIndex()==-1) {
				MessageBox.Show(Lan.g(this,"Please select an item first."));
				return;
			}
			SelectedMedicationNum=((Medication)gridAllMedications.ListGridRows[gridAllMedications.GetSelectedIndex()].Tag).MedicationNum;
			DialogResult=DialogResult.OK;
		}

	}
}