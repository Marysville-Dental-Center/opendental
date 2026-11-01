using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.SheetFramework;

namespace OpenPmsDental {
	public partial class FormPatientForms:FormODBase {
		public long PatNum;
		///<summary>Indicates the most recently selected Document.DocNum</summary>
		public long DocNum;
		private Patient _patient;
		///<summary>Every form of the patient, with the Status column (PatientFormsList.GetTable). The grid shows the rows the Show filter lists, sorted.
		///</summary>
		private DataTable _tablePatientForms;
		///<summary>The toolbar buttons by action, so they can be enabled for the selected row (Open Dental 25.2 window improvements).</summary>
		private Dictionary<EnumPatientFormAction,ODToolBarButton> _dictToolBarButtons;

		public FormPatientForms() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			DocNum=0;
		}

		private void FormPatientForms_Load(object sender,EventArgs e) {
			//Add eForm and Setup > eForms show in Release builds too (Open Dental 24.4): eForms are filled in the office on a workstation.
			_patient=Patients.GetPat(PatNum);
			Text=Lan.g(this,"Patient Forms for")+" "+_patient.GetNameFL();
			LayoutMenu();
			LayoutToolBar();
			LoadView();
			FillGrid(refreshFromDb:true);
		}

		///<summary>The actions in a fixed order (Open Dental 25.2 improvements, our design). Each button runs the handler the old buttons used. Buttons
		///that don't apply to the selected row are disabled, and their tooltip says why (UpdateToolBar). eClipboard and Web Forms are not part of this
		///program, so there are no actions for them.</summary>
		private void LayoutToolBar() {
			toolBarMain.Buttons.Clear();
			_dictToolBarButtons=new Dictionary<EnumPatientFormAction,ODToolBarButton>();
			AddToolBarButton(EnumPatientFormAction.AddSheet,Lan.g(this,"Add Sheet"),butAddSheet_Click,EnumIcons.Add);
			AddToolBarButton(EnumPatientFormAction.AddEForm,Lan.g(this,"Add eForm"),butAddEForm_Click,EnumIcons.Add);
			toolBarMain.Buttons.Add(new ODToolBarButton(ODToolBarButtonStyle.Separator));
			AddToolBarButton(EnumPatientFormAction.PreFill,Lan.g(this,"Pre-Fill"),butPreFill_Click,EnumIcons.None);
			AddToolBarButton(EnumPatientFormAction.Copy,Lan.g(this,"Copy"),butCopy_Click,EnumIcons.None);
			AddToolBarButton(EnumPatientFormAction.Import,Lan.g(this,"Import"),butImport_Click,EnumIcons.None);
			toolBarMain.Buttons.Add(new ODToolBarButton(ODToolBarButtonStyle.Separator));
			AddToolBarButton(EnumPatientFormAction.Kiosk,Lan.g(this,"Kiosk"),butTerminal_Click,EnumIcons.None);
			toolBarMain.Invalidate();
		}

		private void AddToolBarButton(EnumPatientFormAction action,string text,EventHandler eventHandlerClick,EnumIcons icon) {
			ODToolBarButton toolBarButton=new ODToolBarButton(text,eventHandlerClick,icon,"",action);
			toolBarMain.Buttons.Add(toolBarButton);
			_dictToolBarButtons[action]=toolBarButton;
		}

		///<summary>Enables the toolbar actions that apply to the selected row and sets each tooltip to what the action does, or why it is disabled.
		///</summary>
		private void UpdateToolBar() {
			if(_dictToolBarButtons==null) {
				return;
			}
			int countSelected=gridMain.SelectedIndices.Length;
			EnumPatientFormRowType rowTypeSelected=EnumPatientFormRowType.Sheet;
			bool isSelectedImportable=false;
			if(countSelected==1) {
				DataRow dataRow=(DataRow)gridMain.ListGridRows[gridMain.SelectedIndices[0]].Tag;
				rowTypeSelected=PatientFormsList.GetRowType(dataRow);
				isSelectedImportable=dataRow[PatientFormsList.COLUMN_IMPORTABLE].ToString()=="1";
			}
			bool hasKioskForms=_tablePatientForms!=null && PatientFormsList.HasKioskForms(_tablePatientForms);
			List<PatientFormActionState> listPatientFormActionStates=PatientFormsList.GetActionStates(countSelected,rowTypeSelected,isSelectedImportable,
				hasKioskForms);
			for(int i=0;i<listPatientFormActionStates.Count;i++) {
				if(!_dictToolBarButtons.TryGetValue(listPatientFormActionStates[i].Action,out ODToolBarButton toolBarButton)) {
					continue;
				}
				toolBarButton.Enabled=listPatientFormActionStates[i].IsEnabled;
				toolBarButton.ToolTipText=Lan.g(this,listPatientFormActionStates[i].ToolTip);
			}
			toolBarMain.Invalidate();
		}

		private void gridMain_SelectionCommitted(object sender,EventArgs e) {
			UpdateToolBar();
		}

		///<summary>Sets the Show and Sort radio buttons from the user's saved choice.</summary>
		private void LoadView() {
			if(Security.CurUser==null) {
				return;
			}
			PatientFormsList.GetViewForUser(Security.CurUser.UserNum,out EnumPatientFormsShow show,out EnumPatientFormsSort sort);
			radioShowBoth.Checked=show==EnumPatientFormsShow.Both;
			radioShowSheets.Checked=show==EnumPatientFormsShow.Sheets;
			radioShowEForms.Checked=show==EnumPatientFormsShow.EForms;
			radioSortByDateTime.Checked=sort==EnumPatientFormsSort.DateTime;
			radioSortByDescDateT.Checked=sort==EnumPatientFormsSort.DescriptionDateTime;
		}

		///<summary>Remembers the Show and Sort choice for this user (userodpref).</summary>
		private void SaveView() {
			if(Security.CurUser==null) {
				return;
			}
			if(PatientFormsList.SaveViewForUser(Security.CurUser.UserNum,GetShowSelected(),GetSortSelected())) {
				DataValid.SetInvalid(InvalidType.UserOdPrefs);
			}
		}

		private EnumPatientFormsShow GetShowSelected() {
			if(radioShowSheets.Checked) {
				return EnumPatientFormsShow.Sheets;
			}
			if(radioShowEForms.Checked) {
				return EnumPatientFormsShow.EForms;
			}
			return EnumPatientFormsShow.Both;
		}

		private EnumPatientFormsSort GetSortSelected() {
			if(radioSortByDescDateT.Checked) {
				return EnumPatientFormsSort.DescriptionDateTime;
			}
			return EnumPatientFormsSort.DateTime;
		}

		private void radioShow_Click(object sender,EventArgs e) {
			SaveView();
			FillGrid(refreshFromDb:false);
		}

		private void LayoutMenu() {
			menuMain.BeginUpdate();
			MenuItemOD menuItemSetup=new MenuItemOD("Setup");
			menuMain.Add(menuItemSetup);
			menuItemSetup.Add("Sheets",menuItemSheets_Click);
			menuItemSetup.Add("eForms",menuItemEForms_Click);
			menuItemSetup.Add("Image Categories",menuItemImageCats_Click);
			menuItemSetup.Add("Options",menuItemOptions_Click);
			//eFormImportRule is documented as unused since Open Dental 25.1, so its setup window stays hidden.
			//if(ODBuild.IsDebug()) {
			//	menuItemSetup.Add("Import Rules",menuItemImportRules_Click);
			//}
			menuMain.EndUpdate();
		}

		private void FillGrid(bool refreshFromDb){
			//js This is an edge case pattern because we are not normally dealing with a table.
			DataTable table=new DataTable();
			if(refreshFromDb || _tablePatientForms==null){
				_tablePatientForms=PatientFormsList.GetTable(PatNum);//adds the Status column (Open Dental 25.2 window improvements)
			}
			//Not rebuilt from the grid's rows, because rows hidden by the Show filter are not in the grid.
			table=PatientFormsList.Filter(_tablePatientForms,GetShowSelected());
			//Sort DataTable.
			if(radioSortByDateTime.Checked && table.Rows.Count>0){
				DataView dataView=table.DefaultView;
				dataView.Sort="dateTime";
				table=dataView.ToTable();
			}
			else if(radioSortByDescDateT.Checked && table.Rows.Count>0){
				DataView dataView=table.DefaultView;
				dataView.Sort="description,dateTime";
				table=dataView.ToTable();
			}
			//Get previously selected form, if one was selected.
			long sheetNumSelected=0;
			long eFormNumSelected=0;
			long docNumSelected=0;
			if(gridMain.GetSelectedIndex()!=-1) {
				DataRow dataRow=(DataRow)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag;
				sheetNumSelected=PIn.Long(dataRow["SheetNum"].ToString());
				eFormNumSelected=PIn.Long(dataRow["EFormNum"].ToString());
				docNumSelected=PIn.Long(dataRow["DocNum"].ToString());
			}
			//Fill Grid.
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g(this,"Type"),60);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Date/Time"),140,GridSortingStrategy.DateParse);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Kiosk"),55,HorizontalAlignment.Center,GridSortingStrategy.AmountParse);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Description"),170);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Status"),110);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Image Category"),100);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Updated"),65,GridSortingStrategy.DateParse);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Submitted"),115,GridSortingStrategy.DateParse);//Open Dental 25.4 sheet/eform.DateTimeSubmitted
			gridMain.Columns.Add(col);
			DataTable tableSubmitted=FormSubmittedDates.GetForPatient(PatNum);
			gridMain.ListGridRows.Clear();
			GridRow row;
			for(int i=0;i<table.Rows.Count;i++){
				row=new GridRow();
				//Test each form for its form type
				if(table.Rows[i]["DocNum"].ToString()!="0"){
					row.Cells.Add("Document");
				}
				else if(table.Rows[i]["SheetNum"].ToString()!="0"){
					row.Cells.Add("Sheet");
				}
				else if(table.Rows[i]["EFormNum"].ToString()!="0"){
					row.Cells.Add("eForm");
				}
				row.Cells.Add(table.Rows[i]["dateTime"].ToString());
				row.Cells.Add(table.Rows[i]["showInTerminal"].ToString());
				row.Cells.Add(table.Rows[i]["description"].ToString());
				row.Cells.Add(table.Rows[i][PatientFormsList.COLUMN_STATUS].ToString());
				row.Cells.Add(table.Rows[i]["imageCat"].ToString());
				row.Cells.Add(table.Rows[i]["DateTSheetEdited"].ToString());
				row.Cells.Add(FormSubmittedDates.GetDisplayForRow(tableSubmitted,PIn.Long(table.Rows[i]["SheetNum"].ToString()),
					PIn.Long(table.Rows[i]["EFormNum"].ToString())));
				row.Tag=table.Rows[i];
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			//Set selected row if there was one selected.
			List<DataRow> listDataRows=gridMain.ListGridRows.Select(x=>(DataRow)x.Tag).ToList();
			if(sheetNumSelected!=0){
				int idx=listDataRows.FindIndex(x=>x["SheetNum"].ToString()==sheetNumSelected.ToString());
				gridMain.SetSelected(idx,true);
			}
			else if(eFormNumSelected!=0){
				int idx=listDataRows.FindIndex(x=>x["eFormNum"].ToString()==eFormNumSelected.ToString());
				gridMain.SetSelected(idx,true);
			}
			else if(docNumSelected!=0){
				int idx=listDataRows.FindIndex(x=>x["DocNum"].ToString()==docNumSelected.ToString());
				gridMain.SetSelected(idx,true);
			}
			UpdateToolBar();
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			DataRow dataRow=(DataRow)gridMain.ListGridRows[e.Row].Tag;
			//Images
			//Hold onto docNum so Image module refresh persists selection when closing FormPatientForms.
			DocNum=PIn.Long(dataRow["DocNum"].ToString());//Set to 0 if not a Document, i.e. a Sheet.
			if(DocNum!=0) {
				if(!Security.IsAuthorized(EnumPermType.ImagingModule)) {
					return;
				}
				GlobalFormOpenDental.GoToModule(EnumModuleType.Imaging,patNum:PatNum,docNum:DocNum);
				return;
			}
			//Sheets
			long sheetNum=PIn.Long(dataRow["SheetNum"].ToString());
			if(sheetNum!=0){
				Sheet sheet=Sheets.GetSheet(sheetNum);
				FormSheetFillEdit.ShowForm(sheet,FormSheetFillEdit_FormClosing);
				return;
			}
			//EForms
			long eFormNum=PIn.Long(dataRow["EFormNum"].ToString());
			if(eFormNum!=0){
				EForm eForm=EForms.GetEForm(eFormNum);
				FrmEFormFillEdit frmEFormFillEdit=new FrmEFormFillEdit();
				frmEFormFillEdit.EFormCur=eForm;
				frmEFormFillEdit.ShowDialog();
				if(frmEFormFillEdit.IsDialogCancel){
					return;
				}
				//Must have saved to db.
				//This might have included "deleting" the eForm, which we don't need to test for here.
				FillGrid(refreshFromDb:true);
				return;
			}
		}

		private void menuItemSheets_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			using FormSheetDefs formSheetDefs=new FormSheetDefs();
			formSheetDefs.ShowDialog();
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Sheets");
			FillGrid(refreshFromDb:false);
		}

		private void menuItemEForms_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			FrmEFormDefs frmEFormDefs=new FrmEFormDefs();
			frmEFormDefs.ShowDialog();
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"EForms");
			FillGrid(refreshFromDb:false);
		}

		private void menuItemImageCats_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.DefEdit)) {
				return;
			}
			using FormDefinitions formDefinitions=new FormDefinitions(DefCat.ImageCats);
			formDefinitions.ShowDialog();
			SecurityLogs.MakeLogEntry(EnumPermType.DefEdit,0,"Defs");
			FillGrid(refreshFromDb:true);
		}

		private void menuItemOptions_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			FrmSheetSetup frmSheetSetup=new FrmSheetSetup();
			frmSheetSetup.ShowDialog();
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"ShowForms");
			FillGrid(refreshFromDb:true);
		}

		private void radioSortByDateTime_Click(object sender,EventArgs e) {
			SaveView();
			//Refill gridMain without pulling from the db.
			FillGrid(refreshFromDb:false);
		}

		private void radioSortByDescriptionDateTime_Click(object sender,EventArgs e) {
			SaveView();
			//Refill gridMain without pulling from the db.
			FillGrid(refreshFromDb:false);
		}

		//private void menuItemImportRules_Click(object sender,EventArgs e) {
		//	if(!Security.IsAuthorized(EnumPermType.Setup)) {
		//		return;
		//	}
		//	FrmEFormImportRules frmEFormImportRules=new FrmEFormImportRules();
		//	frmEFormImportRules.ShowDialog();
		//	SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"EForm Import Rules");
		//	FillGrid();
		//}

		private void butAddSheet_Click(object sender,EventArgs e) {
			FrmSheetPicker frmSheetPicker=new FrmSheetPicker();
			frmSheetPicker.AllowMultiSelect=true;
			frmSheetPicker.SheetType=SheetTypeEnum.PatientForm;
			frmSheetPicker.ShowDialog();
			if(!frmSheetPicker.IsDialogOK) {
				return;
			}
			SheetDef sheetDef;
			Sheet sheet=null;//only useful if not Terminal
			for(int i=0;i<frmSheetPicker.ListSheetDefsSelected.Count;i++) {
				sheetDef=frmSheetPicker.ListSheetDefsSelected[i];
				sheet=SheetUtil.CreateSheet(sheetDef,PatNum);
				if(SheetDefs.ContainsGrids(sheetDef,"ProcsWithFee","ProcsNoFee")) {
					using FormSheetProcSelect formSheetProcSelect=new FormSheetProcSelect();
					formSheetProcSelect.PatNum=PatNum;
					formSheetProcSelect.ShowDialog();
					if(formSheetProcSelect.DialogResult==DialogResult.OK) {
						SheetParameter.SetParameter(sheet,"ListProcNums",formSheetProcSelect.ListProcNumsSelected);
					}
				}
				//Will display FormApptsOther for the user to select an appointment or procedures to display on the sheet.
				SheetUtilL.SetApptProcParamsForSheet(sheet,sheetDef,PatNum);
				SheetParameter.SetParameter(sheet,"PatNum",PatNum);
				SheetFiller.FillFields(sheet);
				SheetUtil.CalculateHeights(sheet);
				if(frmSheetPicker.DoKioskSend) {
					sheet.InternalNote="";//because null not ok
					sheet.ShowInTerminal=(byte)(Sheets.GetBiggestShowInTerminal(PatNum)+1);
					Sheets.SaveNewSheet(sheet);//save each sheet.
					Sheets.SaveParameters(sheet);
				}
			}
			if(frmSheetPicker.DoKioskSend) {
				//do not show a dialog now.  User will need to click the terminal button.
				FillGrid(refreshFromDb:true);
				Signalods.SetInvalid(InvalidType.Kiosk);
			}
			else if(sheet!=null) {
				FormSheetFillEdit.ShowForm(sheet,FormSheetFillEdit_FormClosing);
			}
		}

		private void butAddEForm_Click(object sender,EventArgs e) {
			FrmEFormPicker frmEFormPicker=new FrmEFormPicker();
			frmEFormPicker.ShowDialog();
			if(frmEFormPicker.IsDialogCancel){
				return;
			}
			EFormDef eFormDef=frmEFormPicker.EFormDefSelected;
			//fields already attached
			EForm eForm=EForms.CreateEFormFromEFormDef(eFormDef,PatNum,EnumEFormStatus.None);//sets IsNew=true
			eForm.DateTimeShown=DateTime.Now;
			EFormFiller.FillFields(eForm);
			EForms.TranslateFields(eForm,_patient.Language);
			FrmEFormFillEdit frmEFormFillEdit=new FrmEFormFillEdit();
			frmEFormFillEdit.EFormCur=eForm;
			frmEFormFillEdit.ShowDialog();
			if(frmEFormFillEdit.IsDialogCancel){
				//This could include "deleting" the new eForm, which would then never have made it to the db at all.
				return;
			}
			//must have saved to db.
			FillGrid(refreshFromDb:true);
		}

		private void butTerminal_Click(object sender,EventArgs e) {
			//All of the patient's forms, not only the rows the Show filter lists.
			if(_tablePatientForms==null || !PatientFormsList.HasKioskForms(_tablePatientForms)){
				MsgBox.Show(this,"No forms for this patient are set to show in the kiosk.");
				return;
			}
			if(PrefC.GetLong(PrefName.ProcessSigsIntervalInSecs)==0) {
				MsgBox.Show(this,"Cannot open kiosk unless process signal interval is set. To set it, go to Setup > Miscellaneous.");
				return;
			}
			using FormTerminal formTerminal=new FormTerminal();
			formTerminal.IsSimpleMode=true;
			formTerminal.PatNum=PatNum;
			formTerminal.ShowDialog();
			FillGrid(refreshFromDb:true);
		}

		private void butPreFill_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length !=1) {
				MsgBox.Show(this,"Please select one completed sheet from the list above first.");
				return;
			}
			DataRow dataRow=(DataRow)gridMain.ListGridRows[gridMain.SelectedIndices[0]].Tag;
			long sheetNum=PIn.Long(dataRow["SheetNum"].ToString());
			if(sheetNum==0) {
				MsgBox.Show(this,"Must select a sheet.");
				return;
			}
			Sheet sheet=Sheets.GetSheet(sheetNum);
			if(sheet==null) {
				MsgBox.Show(this,"The selected sheet has been deleted by another workstation.");
				return;
			}
			SheetDef sheetDefOriginal=SheetDefs.GetSheetDef(sheet.SheetDefNum,hasExceptions:false);
			if(sheetDefOriginal==null) {
				//We could not find the sheetDef which this sheet was based on.
				//Prompt the user, asking if they would like to manually select a sheet def.
				if(!MsgBox.Show(this, MsgBoxButtons.YesNo, "Sheet Def not found. Unable to pre-fill. Would you like to select the correct Sheet Def manually?")) {
					return;//If the user chose not to pick a sheetDef, return.
				}
				//Find SheetDefs of the same type as the sheet we are pre-filling, and allow the user to select one.
				List<SheetDef> listSheetDefs=SheetDefs.GetCustomForType(sheet.SheetType);
				FrmSheetPicker frmSheetPicker=new FrmSheetPicker();
				frmSheetPicker.SheetType=sheet.SheetType;
				frmSheetPicker.ListSheetDefs=listSheetDefs;
				frmSheetPicker.IsPreFill=true;
				frmSheetPicker.ShowDialog();
				if(!frmSheetPicker.IsDialogOK) {
					return;//If the user cancelled, return.
				}
				sheetDefOriginal=frmSheetPicker.ListSheetDefsSelected.First();
				sheet.SheetDefNum=sheetDefOriginal.SheetDefNum;
			}
			Sheet sheetNew=Sheets.PreFillSheetFromPreviousAndDatabase(sheetDefOriginal,sheet);
			sheetNew.IsNew=true;
			SheetUtil.CalculateHeights(sheetNew);
			using FormSheetFillEdit formSheetFillEdit=new FormSheetFillEdit();
			formSheetFillEdit.SheetCur=sheetNew;
			formSheetFillEdit.ShowDialog();
			//If they press ok, the form is inserted, so refresh the grid, make a security log, and select the new entry.
			if(formSheetFillEdit.DialogResult==DialogResult.OK) {
				SecurityLogs.MakeLogEntry(EnumPermType.Copy,PatNum,"Patient form "+sheet.Description+" from "+sheet.DateTimeSheet.ToString()+" copied via Pre-Fill");
				FillGrid(refreshFromDb:true);
				//Select the newly added sheet.
				for(int i=0;i<gridMain.ListGridRows.Count;i++) {
					dataRow=(DataRow)gridMain.ListGridRows[i].Tag;
					if(dataRow["SheetNum"].ToString()==sheetNew.SheetNum.ToString()) {
						gridMain.SetSelected(i,true);
						break;
					}
				}
			}
		}

		private void butCopy_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length!=1) {
				MsgBox.Show(this,"Please select one completed sheet from the list above first.");
				return;
			}
			DataRow dataRow=(DataRow)gridMain.ListGridRows[gridMain.SelectedIndices[0]].Tag;
			long sheetNum=PIn.Long(dataRow["SheetNum"].ToString());
			if(sheetNum==0) {
				MsgBox.Show(this,"Must select a sheet.");
				return;
			}
			Sheet sheet=Sheets.GetSheet(sheetNum);
			Sheet sheet2=sheet.Copy();
			sheet2.DateTimeSheet=DateTime.Now;
			sheet2.SheetFields=new List<SheetField>(sheet.SheetFields);
			for(int i=0;i<sheet2.SheetFields.Count;i++){
				sheet2.SheetFields[i].IsNew=true;
				if(sheet2.SheetFields[i].FieldType==SheetFieldType.SigBox){
					sheet2.SheetFields[i].FieldValue="";//clear signatures
				}
				//no need to set SheetNums here.  That's done from inside FormSheetFillEdit
			}
			sheet2.IsNew=true;
			using FormSheetFillEdit formSheetFillEdit=new FormSheetFillEdit();
			formSheetFillEdit.SheetCur=sheet2;
			formSheetFillEdit.ShowDialog();
			if(formSheetFillEdit.DialogResult==DialogResult.OK || formSheetFillEdit.DidChangeSheet) {
				FillGrid(refreshFromDb:true);
				for(int i=0;i<gridMain.ListGridRows.Count;i++){
					dataRow=(DataRow)gridMain.ListGridRows[i].Tag;
					if(dataRow["SheetNum"].ToString()==sheet2.SheetNum.ToString()){
						gridMain.SetSelected(i,true);
					}
				}
				SecurityLogs.MakeLogEntry(EnumPermType.Copy,PatNum,"Patient form "+sheet.Description+" from "+sheet.DateTimeSheet.ToString()+" copied");
			}
		}

		private void butImport_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length !=1) {
				MsgBox.Show(this,"Please select one completed form from the list above first.");
				return;
			}
			DataRow dataRow=(DataRow)gridMain.ListGridRows[gridMain.SelectedIndices[0]].Tag;
			long docNum=PIn.Long(dataRow["DocNum"].ToString());
			if(docNum!=0) {
				//document=Documents.GetByNum(docNum);
				//Pdf importing broke with dot net 4.0 and was enver reimplemented.
				//See FormSheetImport.Load() region Acro 
				//string extens=Path.GetExtension(doc.FileName);
				//if(extens.ToLower()!=".pdf") {
				//	MsgBox.Show(this,"Only pdf's and sheets can be imported into the database.");
				//	return;
				//}
				MsgBox.Show(this,"PDFs cannot be imported into the database.");
				return;
			}
			long sheetNum=PIn.Long(dataRow["SheetNum"].ToString());
			long eFormNum=PIn.Long(dataRow["EFormNum"].ToString());
			Sheet sheet=null;
			if(sheetNum!=0) {
				sheet=Sheets.GetSheet(sheetNum);
				if(!SheetDefs.IsWebFormAllowed(sheet.SheetType)) {
					MsgBox.Show(this,"For now, only sheets of type 'PatientForm' and 'MedicalHistory' can be imported.");
					return;
				}
			}
			//if(ODBuild.IsDebug()) {
				//this is for testing the new automatic import
				//	MsgBox.Show(this,"Done");
				//	return;
			//}
			EForm eForm=null;
			if(eFormNum!=0){
				eForm=EForms.GetEForm(eFormNum);
			}
			using FormSheetImport formSheetImport=new FormSheetImport();
			formSheetImport.SheetCur=sheet;
			formSheetImport.EFormCur=eForm;
			formSheetImport.ShowDialog();
			if(formSheetImport.DialogResult==DialogResult.OK) {
				//The import is recorded in the audit trail, which the Status column shows as Imported.
				FillGrid(refreshFromDb:true);
			}
		}

		/// <summary>Event handler for closing FormSheetFillEdit when it is non-modal.</summary>
		private void FormSheetFillEdit_FormClosing(object sender,FormClosingEventArgs e) {
			if(((FormSheetFillEdit)sender).DialogResult==DialogResult.OK || ((FormSheetFillEdit)sender).DidChangeSheet) {
				FillGrid(refreshFromDb:true);
			}
		}

	}
}