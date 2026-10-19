using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Form used to manage kiosk terminals and load/clear patient forms displayed on the kiosks. Kiosks are other workstations on the
	///office network running Tools > Kiosk. Open Dental's hosted eClipboard devices were removed.</summary>
	public partial class FormTerminalManager:FormODBase {
		private bool _isSetupMode;

		///<summary></summary>
		public FormTerminalManager(bool isSetupMode=false) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_isSetupMode=isSetupMode;
			groupBoxPassword.Visible=_isSetupMode;
		}

		private void FormTerminalManager_Load(object sender,EventArgs e) {
			ODEvent.Fired+=PatientChangedEvent_Fired;
			textPassword.Text=PrefC.GetString(PrefName.TerminalClosePassword);
			FillGrid();
		}

		public override void ProcessSignalODs(List<Signalod> listSignalods) {
			if(listSignalods.Any(x => x.IType==InvalidType.Prefs)) {
				textPassword.Text=PrefC.GetString(PrefName.TerminalClosePassword);
			}
			int processIdCur=Process.GetCurrentProcess().Id;
			if(listSignalods.All(x => x.IType!=InvalidType.Kiosk || (x.FKeyType==KeyType.ProcessId && x.FKey==processIdCur))) {
				return;
			}
			FillGrid();
		}

		private void FillGrid() {
			long terminalActiveNumSelected=-1;
			if(gridMain.GetSelectedIndex()>-1) {
				terminalActiveNumSelected=((TerminalActive)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag).TerminalActiveNum;
			}
			List<TerminalActive> listTerminalActives=TerminalActives.Refresh().OrderBy(x => x.ComputerName).ToList();
			int selectedIndex=-1;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn("Device Name",135);
			gridMain.Columns.Add(col);
			col=new GridColumn("Session Name",110);
			gridMain.Columns.Add(col);
			col=new GridColumn("Device State",100);
			gridMain.Columns.Add(col);
			col=new GridColumn("Patient",150);
			gridMain.Columns.Add(col);
			col=new GridColumn("Action",50,HorizontalAlignment.Center);
			gridMain.Columns.Add(col);
			if(_isSetupMode) {
				col=new GridColumn("Delete",50,HorizontalAlignment.Center);
				gridMain.Columns.Add(col);
			}
			gridMain.ListGridRows.Clear();
			for(int i=0;i<listTerminalActives.Count;i++){
				GridRow row=new GridRow();
				//We assign the currently-indexed TerminalActive to a local object so that the CellClick() handler defined below here can refer to it safely
				//Using the list index to refer to the TerminalActive will cause an out-of-bounds index UE after CellClick() invokes FillGrid()
				TerminalActive terminalActiveCurrent=listTerminalActives[i];
				row.Tag=terminalActiveCurrent;
				row.Cells.Add(new GridCell(terminalActiveCurrent.ComputerName));
				row.Cells.Add(new GridCell(terminalActiveCurrent.SessionName));
				row.Cells.Add(new GridCell(terminalActiveCurrent.TerminalStatus.GetDescription()));
				row.Cells.Add(new GridCell(GetPatName(terminalActiveCurrent.PatNum)));
				#region Load/Clear click handler
				void CellClick(object sender,EventArgs e) {
					FillGrid();
					if(terminalActiveCurrent.PatNum==0) { //we are trying to load the patient
						if(FormOpenDental.PatNumCur==0) {
							MsgBox.Show(this,"There is currently no patient selected to send to the device Select a patient in Open Dental " +
								"in order to continue.");
							return;
						}
						if(listSheets.Items.Count==0) {
							MsgBox.Show(this,"There are no sheets to send to the computer or device for the current patient.");
							return;
						}
						SetPatNum(terminalActiveCurrent,FormOpenDental.PatNumCur);
					}
					else { //we are trying to clear the patient
						if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"A patient is currently using the terminal.  If you continue, they will lose the information that is on their "
							+"screen.  Continue anyway?")) {
							return;
						}
						SetPatNum(terminalActiveCurrent,0);
					}
					FillGrid();
				}
				#endregion Load/Clear click handler
				GridCell cell=new GridCell(terminalActiveCurrent.PatNum==0?"Load":"Clear");
				cell.ColorBackG=Color.LightGray;
				cell.ClickEvent=CellClick;
				row.Cells.Add(cell);
				if(_isSetupMode) {
					#region Delete click handler
					void DeleteClick(object sender,EventArgs e) {
						FillGrid();
						if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"A row should not be deleted unless it is showing erroneously and there really is "+
							"nothing running on the computer or device shown.  Continue anyway?")) {
							return;
						}
						TerminalActives.DeleteForCmptrSessionAndId(terminalActiveCurrent.ComputerName,terminalActiveCurrent.SessionId,processId:terminalActiveCurrent.ProcessId);
						FillGrid();
					}
					#endregion Delete click handler
					cell=new GridCell("Delete");
					cell.ColorBackG=Color.LightGray;
					cell.ClickEvent=DeleteClick;
					row.Cells.Add(cell);
				}
				gridMain.ListGridRows.Add(row);
				if(terminalActiveCurrent.TerminalActiveNum==terminalActiveNumSelected) {
					selectedIndex=gridMain.ListGridRows.Count-1;
				}
			}
			gridMain.EndUpdate();
			gridMain.SetSelected(selectedIndex,true);//selectedIndex could be -1 if the selected term is not in the list, default to row 0
			FillPat();
		}

		///<summary>The name of the patient who is currently using this kiosk. Returns empty string if no patient.</summary>
		private string GetPatName(long patNum) {
			if(patNum>0) {
				return Patients.GetLim(patNum).GetNameLF();
			}
			return "";
		}

		///<summary>Sets the PatNum for the kiosk and signals the other terminal managers to refresh their grids.</summary>
		private void SetPatNum(TerminalActive terminalActive,long patNum) {
			TerminalActives.SetPatNum(terminalActive.TerminalActiveNum,patNum);
			Signalods.SetInvalid(InvalidType.Kiosk,KeyType.ProcessId,Process.GetCurrentProcess().Id);//signal the terminal manager to refresh its grid
		}

		private void FillPat() {
			bool isRowSelected=gridMain.GetSelectedIndex()>-1;
			groupBoxPatient.Enabled=isRowSelected;
			listSheets.Visible=isRowSelected;
			butPatForms.Visible=isRowSelected;
			if(!isRowSelected) {
				groupBoxPatient.Text="Select a Device First";
				labelPatient.Text="";
				labelSheets.Text="";
				return;
			}
			TerminalActive terminalActive=(TerminalActive)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag;
			listSheets.Items.Clear();
			if(terminalActive.PatNum==0) {
				groupBoxPatient.Text="Patient to Load to Device";
				labelSheets.Text="Forms to Load to Device";
				if(FormOpenDental.PatNumCur==0) {
					labelPatient.Text="None Selected";
					butPatForms.Enabled=false;
				}
				else {
					labelPatient.Text=Patients.GetLim(FormOpenDental.PatNumCur).GetNameLF();
					butPatForms.Enabled=true;
					Sheets.GetForTerminal(FormOpenDental.PatNumCur).ForEach(x => listSheets.Items.Add(x.Description,x));
				}
			}
			else {
				groupBoxPatient.Text="Patient on Device";
				labelSheets.Text="Forms on Device";
				labelPatient.Text=GetPatName(terminalActive.PatNum);
				butPatForms.Enabled=false;
				Sheets.GetForTerminal(terminalActive.PatNum).ForEach(x => listSheets.Items.Add(x.Description,x));
			}
		}

		#region EventHandlers

		public void PatientChangedEvent_Fired(ODEventArgs e) {
			if(e.EventType!=ODEventType.Patient || e.Tag.GetType()!=typeof(long) || this.IsDisposed) {
				return;
			}
			FillPat();
		}

		private void gridMain_SelectionCommitted(object sender,EventArgs e) {
			FillPat();
		}

		private void butPatForms_Click(object sender,EventArgs e) {
			if(gridMain.GetSelectedIndex()<0) {
				return;
			}
			using FormPatientForms formPatientForms=new FormPatientForms();
			TerminalActive terminalActive=(TerminalActive)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag;
			formPatientForms.PatNum=terminalActive.PatNum;
			if(formPatientForms.PatNum==0){
				formPatientForms.PatNum=FormOpenDental.PatNumCur;
			}
			formPatientForms.ShowDialog();
			FillPat();
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(Prefs.UpdateString(PrefName.TerminalClosePassword,textPassword.Text)){
				Signalods.SetInvalid(InvalidType.Prefs);
			}
			MsgBox.Show(this,"Done.");
		}

		private void FormTerminalManager_FormClosing(object sender,FormClosingEventArgs e) {
			ODEvent.Fired-=PatientChangedEvent_Fired;
			if(Prefs.UpdateString(PrefName.TerminalClosePassword,textPassword.Text)){
				Signalods.SetInvalid(InvalidType.Prefs);
			}
		}

		#endregion EventHandlers

	}
}
