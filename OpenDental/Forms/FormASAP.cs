using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using Tamir.SharpSsh.java.lang;

namespace OpenDental {
	///<summary></summary>
	public partial class FormASAP:FormODBase {
		private List<Appointment> _listAppointmentsASAP;
		private bool _doPrintHeading;
		private int _pagesPrinted;
		///<summary>A list of patient names and patNums. Gets refilled every time FillGridAppts() is invoked.</summary>
		private List<Patients.PatientName> _listPatientNames=new List<Patients.PatientName>();
		private List<Provider> _listProviders;
		private List<Site> _listSites;
		///<summary></summary>
		public FormASAP() {
			InitializeComponent();// Required for Windows Form Designer support
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormASAP_Load(object sender,System.EventArgs e) {
			Cursor=Cursors.WaitCursor;
			SetFilterControlsAndAction(() => {
					if(tabControl.SelectedIndex==0) {
						FillGridAppts();
					}
					else {
						FillGridRecalls();
					}
				},comboProv,comboSite,comboClinic,comboAptStatus,comboNumberReminders,checkGroupFamilies);
			_listProviders=Providers.GetDeepCopy(true);
			comboProv.IncludeAll=true;
			comboProv.Items.AddProvsFull(_listProviders);
			comboProv.IsAllSelected=true;
			if(PrefC.GetBool(PrefName.EnterpriseApptList)){
				comboClinic.IncludeAll=false;
			}
			if(PrefC.GetBool(PrefName.EasyHidePublicHealth)){
				comboSite.Visible=false;
				labelSite.Visible=false;
			}
			else{
				_listSites=Sites.GetDeepCopy();
				comboSite.IncludeAll=true;
				comboSite.SelectedIndex=0;
				comboSite.Items.AddList(_listSites,x => x.Description);
				comboSite.IsAllSelected=true;
			}
			splitContainer.Panel2Collapsed=true;
			comboAptStatus.Items.Add(Lan.g(this,ApptStatus.Scheduled.ToString()),ApptStatus.Scheduled);
			comboAptStatus.Items.Add(Lan.g(this,ApptStatus.Planned.ToString()),ApptStatus.Planned);
			comboAptStatus.Items.Add(Lan.g(this,ApptStatus.UnschedList.ToString()),ApptStatus.UnschedList);
			comboAptStatus.Items.Add(Lan.g(this,ApptStatus.Broken.ToString()),ApptStatus.Broken);
			comboShowHygiene.Items.Add(Lan.g(this,FromASAPShowAppointment.All.GetDescription()),FromASAPShowAppointment.All);
			comboShowHygiene.Items.Add(Lan.g(this,FromASAPShowAppointment.NonHygiene.GetDescription()),FromASAPShowAppointment.NonHygiene);
			comboShowHygiene.Items.Add(Lan.g(this,FromASAPShowAppointment.Hygiene.GetDescription()),FromASAPShowAppointment.Hygiene);
			checkGroupFamilies.Checked=PrefC.GetBool(PrefName.RecallGroupByFamily);
			comboNumberReminders.Items.Add(Lan.g(this,"All"));
			comboNumberReminders.Items.Add("0");
			comboNumberReminders.Items.Add("1");
			comboNumberReminders.Items.Add("2");
			comboNumberReminders.Items.Add("3");
			comboNumberReminders.Items.Add("4");
			comboNumberReminders.Items.Add("5");
			comboNumberReminders.Items.Add("6+");
			comboNumberReminders.SelectedIndex=0;
			int daysPast=PrefC.GetInt(PrefName.RecallDaysPast);
			int daysFuture=PrefC.GetInt(PrefName.RecallDaysFuture);
			if(daysPast==-1){
				textDateStart.Text="";
			}
			else{
				textDateStart.Text=DateTime.Today.AddDays(-daysPast).ToShortDateString();
			}
			if(daysFuture==-1) {
				textDateEnd.Text="";
			}
			else {
				textDateEnd.Text=DateTime.Today.AddDays(daysFuture).ToShortDateString();
			}
			LayoutManager.LayoutFormBoundsAndFonts(this);
			Cursor=Cursors.Default;
		}

		///<summary>There is a bug in ODProgress.cs that forces windows that use a progress bar on load to go behind other applications. 
		///This is a temporary workaround until we decide how to address the issue.</summary>
		private void FormASAP_Shown(object sender,EventArgs e) {
			FillGridAppts();
		}

		private void tabControl_SelectedIndexChanged(object sender,EventArgs e) {
			switch(tabControl.SelectedIndex) {
				case 0:
				default:
					FillGridAppts();
					break;
				case 1:
					FillGridRecalls();
					break;
			}
		}

		#region Appointments Tab

		private void FillGridAppts(){
			long siteNum=0;
			if(!PrefC.GetBool(PrefName.EasyHidePublicHealth)) {
				siteNum=comboSite.GetSelectedKey<Site>(x => x.SiteNum);
			}
			List<ApptStatus> listAppStatuses=comboAptStatus.GetListSelected<ApptStatus>();
			long clinicNum=-1;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=comboClinic.ClinicNumSelected;
			}
			ProgressWin progressOD=new ProgressWin();
			progressOD.ActionMain=() => { 
				_listAppointmentsASAP=Appointments.RefreshASAP(comboProv.GetSelectedProvNum(),siteNum,clinicNum,listAppStatuses,codeRangeFilter.StartRange,
					codeRangeFilter.EndRange);
				};
			progressOD.ShowDialog();
			int scrollVal=gridAppts.ScrollValue;
			List<long> listAptNumsSelected=gridAppts.SelectedTags<Appointment>().Select(x => x.AptNum).ToList();
			gridAppts.BeginUpdate();
			gridAppts.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g("TableASAP","Patient"),140);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Date"),65,HorizontalAlignment.Center,GridSortingStrategy.DateParse);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Unsched Status"),110);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Apt Status"),75);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Prov"),50);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Procedures"),150);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Length"),60,GridSortingStrategy.AmountParse);
			gridAppts.Columns.Add(col);
			col=new GridColumn(Lan.g("TableASAP","Notes"),160);
			gridAppts.Columns.Add(col);
			gridAppts.ListGridRows.Clear();
			GridRow row;
			List<long> listPatNums=_listPatientNames.Select(x=>x.PatNum).ToList();
			if(_listAppointmentsASAP.Any(x => !listPatNums.Contains(x.PatNum))) {
				_listPatientNames=Patients.GetPatientNameList(_listAppointmentsASAP.Select(x => x.PatNum).ToList());
			}
			FromASAPShowAppointment fromASAPShowAppointment=comboShowHygiene.GetSelected<FromASAPShowAppointment>();
			for(int i=0;i<_listAppointmentsASAP.Count;i++) {
				if(fromASAPShowAppointment==FromASAPShowAppointment.Hygiene && !_listAppointmentsASAP[i].IsHygiene) {
					continue;
				}
				if(fromASAPShowAppointment==FromASAPShowAppointment.NonHygiene && _listAppointmentsASAP[i].IsHygiene) {
					continue;
				}
				row=new GridRow();
				Patients.PatientName patientName=_listPatientNames.FirstOrDefault(x=>x.PatNum==_listAppointmentsASAP[i].PatNum);
				if(patientName.Name is null) {
					patientName.Name=Lan.g(this,"UNKNOWN");
				}
				row.Cells.Add(patientName.Name);
				if(_listAppointmentsASAP[i].AptDateTime.Year < 1880){//shouldn't be possible.
					row.Cells.Add("");
				}
				else{
					row.Cells.Add(_listAppointmentsASAP[i].AptDateTime.ToShortDateString());
				}
				row.Cells.Add(Defs.GetName(DefCat.RecallUnschedStatus,_listAppointmentsASAP[i].UnschedStatus));
				row.Cells.Add(_listAppointmentsASAP[i].AptStatus.ToString());
				if(_listAppointmentsASAP[i].IsHygiene) {
					row.Cells.Add(Providers.GetAbbr(_listAppointmentsASAP[i].ProvHyg));
				}
				else {
					row.Cells.Add(Providers.GetAbbr(_listAppointmentsASAP[i].ProvNum));
				}
				row.Cells.Add(_listAppointmentsASAP[i].ProcDescript);
				row.Cells.Add(_listAppointmentsASAP[i].Length.ToString());
				row.Cells.Add(_listAppointmentsASAP[i].Note);
				row.Tag=_listAppointmentsASAP[i];
				gridAppts.ListGridRows.Add(row);
			}
			gridAppts.EndUpdate();
			gridAppts.ScrollValue=scrollVal;
			for(int i=0;i<gridAppts.ListGridRows.Count;i++) {
				if(listAptNumsSelected.Contains(((Appointment)gridAppts.ListGridRows[i].Tag).AptNum)) {
					gridAppts.SetSelected(i,true);
				}
			}
		}

		private void gridAppts_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			int currentSelection=e.Row;
			int currentScroll=gridAppts.ScrollValue;
			Patient patient=Patients.GetPat(((Appointment)gridAppts.ListGridRows[e.Row].Tag).PatNum); //check against grid
			GlobalFormOpenDental.PatientSelected(patient,isRefreshCurModule: true);
			using FormApptEdit formApptEdit=new FormApptEdit(((Appointment)gridAppts.ListGridRows[e.Row].Tag).AptNum);
			formApptEdit.PinIsVisible=true;
			formApptEdit.ShowDialog();
			if(formApptEdit.DialogResult!=DialogResult.OK){
				return;
			}
			if(formApptEdit.PinClicked) {
				SendPinboard_Click(); //Whatever they double clicked on will still be selected, just fire the event to send it to the pinboard.
				DialogResult=DialogResult.OK;
			}
			else {
				FillGridAppts();
				gridAppts.SetSelected(currentSelection,true);
				gridAppts.ScrollValue=currentScroll;
			}
		}

		private void RemoveAppts_Click() {
			if(!Security.IsAuthorized(EnumPermType.AppointmentEdit)) {
				return;
			}
			if(gridAppts.SelectedIndices.Length>0 && !MsgBox.Show(this,MsgBoxButtons.OKCancel,"Change priority to normal for all selected appointments?")) {
				return;
			}
			for(int i=0;i<gridAppts.SelectedIndices.Length;i++) {
				Appointment appointment=(Appointment)gridAppts.SelectedGridRows[i].Tag;
				DateTime datePrevious=appointment.DateTStamp;
				Appointments.SetPriority(appointment,ApptPriority.Normal);
				SecurityLogs.MakeLogEntry(EnumPermType.AppointmentEdit,appointment.PatNum,"Appointment priority set from ASAP to normal from ASAP list.",appointment.AptNum,
					datePrevious);
			}
			FillGridAppts();
		}

		private void SendPinboard_Click() {
			if(gridAppts.SelectedIndices.Length==0) {
				MsgBox.Show(this,"Please select an appointment first.");
				return;
			}
			List<long> listAptNums=new List<long>();
			List<Appointment> listAppointmentsSelected=gridAppts.SelectedIndices.Select(x => (Appointment)gridAppts.ListGridRows[x].Tag).ToList();
			List<Patient> listPatients=Patients.GetMultPats(listAppointmentsSelected.Select(x => x.PatNum).ToList()).ToList();
			int patsArchivedOrDeceased=0;
			for(int i=0;i<listAppointmentsSelected.Count;i++) {
				Patient patient=listPatients.FirstOrDefault(x => x.PatNum==listAppointmentsSelected[i].PatNum);
				if(patient!=null && patient.PatStatus.In(PatientStatus.Archived,PatientStatus.Deceased)) {
					patsArchivedOrDeceased++;
					continue;
				}
				if(listAppointmentsSelected[i].AptStatus==ApptStatus.Planned) {
					if(Procedures.GetProcsForSingle(listAppointmentsSelected[i].AptNum,isPlanned: true).Count(x => x.ProcStatus==ProcStat.C) > 0) {
						MsgBox.Show(this,"Not allowed to send a planned appointment to the pinboard if completed procedures are attached. Edit the planned "
							+"appointment first.");
						return;
					}
					Appointment apppointmentNext=Appointments.GetScheduledPlannedApt(listAppointmentsSelected[i].AptNum);
					if(apppointmentNext!=null) {
						if(apppointmentNext.AptStatus==ApptStatus.Complete) {
							//Warn the user they are moving a completed appointment.
							if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"You are about to move an already completed appointment.  Continue?")) {
								return;
							}
						}
						else if(apppointmentNext.AptStatus==ApptStatus.Scheduled) {
							//Warn the user they are moving an already scheduled appointment.
							if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"You are about to move an appointment already on the schedule.  Continue?")) {
								return;
							}
						}
					}
				}
				listAptNums.Add(listAppointmentsSelected[i].AptNum);
			}
			List<string> listUserMsgs=new List<string>();
			if(patsArchivedOrDeceased > 0) {
				listUserMsgs.Add(Lan.g(this,"Appointments skipped because patient status is archived or deceased:")+" "+patsArchivedOrDeceased+".");
			}
			if(listAptNums.Count==0) {
				listUserMsgs.Add(Lan.g(this,"There are no appointments to send to the pinboard."));
			}
			if(listUserMsgs.Count>0) {
				MessageBox.Show(string.Join("\r\n",listUserMsgs));
				if(listAptNums.Count==0) {
					return;
				}
			}
			GlobalFormOpenDental.GoToModule(EnumModuleType.Appointments, listPinApptNums:listAptNums,dateSelected:DateTime.Today);//Pins all appointments to the pinboard that were in listAptNums.
		}
		#endregion Appointments Tab

		#region Recalls Tab

		private void FillGridRecalls() {
			DateTime dateFrom;
			DateTime dateTo;
			if(textDateStart.Text==""){
				dateFrom=DateTime.MinValue;
			}
			else{
				dateFrom=PIn.Date(textDateStart.Text);
			}
			if(textDateEnd.Text=="") {
				dateTo=DateTime.MaxValue;
			}
			else {
				dateTo=PIn.Date(textDateEnd.Text);
			}
			long siteNum=0;
			if(!PrefC.GetBool(PrefName.EasyHidePublicHealth)) {
				siteNum=comboSite.GetSelectedKey<Site>(x => x.SiteNum);
			}
			long clinicNum=-1;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=comboClinic.ClinicNumSelected;
			}
			RecallListShowNumberReminders recallListShowNumberReminders=(RecallListShowNumberReminders)comboNumberReminders.SelectedIndex;
			DataTable tableRecalls=null;
			List<Recall> listRecalls=null;
			long maxReminders=PrefC.GetLong(PrefName.RecallMaxNumberReminders);
			ProgressWin progressOD=new ProgressWin();
			progressOD.ActionMain=() => { 
				tableRecalls=Recalls.GetRecallList(dateFrom,dateTo,checkGroupFamilies.Checked,comboProv.GetSelectedProvNum(),clinicNum,
					siteNum,RecallListSort.DueDate,recallListShowNumberReminders,maxReminders,isAsap: true,codeRangeFilter.StartRange,codeRangeFilter.EndRange);
				listRecalls=Recalls.GetMultRecalls(tableRecalls.Rows.OfType<DataRow>().Select(x => PIn.Long(x["RecallNum"].ToString())).ToList());
			};
			progressOD.ShowDialog();
			if(progressOD.IsCancelled){
				return;
			}
			List<long> listRecallNumsSelected=gridRecalls.SelectedTags<Recall>().Select(x => x.RecallNum).ToList();
			gridRecalls.BeginUpdate();
			gridRecalls.Columns.Clear();
			GridColumn col;
			List<DisplayField> listDisplayFields=DisplayFields.GetForCategory(DisplayFieldCategory.RecallList);
			//Web Sched was removed. Databases migrated from Open Dental can still have a 'WebSched' display field row, which is left in place but not shown.
			listDisplayFields.RemoveAll(x => x.InternalName=="WebSched");
			for(int i=0;i<listDisplayFields.Count;i++) {
				if(listDisplayFields[i].Description=="") {
					col=new GridColumn(listDisplayFields[i].InternalName,listDisplayFields[i].ColumnWidth);
				}
				else {
					col=new GridColumn(listDisplayFields[i].Description,listDisplayFields[i].ColumnWidth);
				}
				col.Tag=listDisplayFields[i].InternalName;
				gridRecalls.Columns.Add(col);
			}
			gridRecalls.ListGridRows.Clear();
			GridRow row;
			for(int i=0;i<tableRecalls.Rows.Count;i++){
				row=new GridRow();
				for(int f=0;f<listDisplayFields.Count;f++) {
					switch(listDisplayFields[f].InternalName){
						case "Due Date":
							row.Cells.Add(tableRecalls.Rows[i]["dueDate"].ToString());
							break;
						case "Patient":
							row.Cells.Add(tableRecalls.Rows[i]["patientName"].ToString());
							break;
						case "Age":
							row.Cells.Add(tableRecalls.Rows[i]["age"].ToString());
							break;
						case "Type":
							row.Cells.Add(tableRecalls.Rows[i]["recallType"].ToString());
							break;
						case "Interval":
							row.Cells.Add(tableRecalls.Rows[i]["recallInterval"].ToString());
							break;
						case "#Remind":
							row.Cells.Add(tableRecalls.Rows[i]["numberOfReminders"].ToString());
							break;
						case "LastRemind":
							row.Cells.Add(tableRecalls.Rows[i]["dateLastReminder"].ToString());
							break;
						case "Contact":
							row.Cells.Add(tableRecalls.Rows[i]["contactMethod"].ToString());
							break;
						case "Status":
							row.Cells.Add(tableRecalls.Rows[i]["status"].ToString());
							break;
						case "Note":
							row.Cells.Add(tableRecalls.Rows[i]["Note"].ToString());
							break;
						case "BillingType":
							row.Cells.Add(tableRecalls.Rows[i]["billingType"].ToString());
							break;
					}
				}
				row.Tag=listRecalls.FirstOrDefault(x => x.RecallNum==PIn.Long(tableRecalls.Rows[i]["RecallNum"].ToString()));
				Patients.PatientName patientName=new Patients.PatientName();
				patientName.PatNum=PIn.Long(tableRecalls.Rows[i]["PatNum"].ToString());
				patientName.Name=PIn.String(tableRecalls.Rows[i]["patientName"].ToString());
				if(_listPatientNames.Count==0 || !_listPatientNames.Any(x => x.PatNum==patientName.PatNum)) {
					_listPatientNames.Add(patientName);
				}
				gridRecalls.ListGridRows.Add(row);
			}
			gridRecalls.EndUpdate();
			for(int i=0;i<gridRecalls.ListGridRows.Count;i++) {
				if(listRecallNumsSelected.Contains(((Recall)gridRecalls.ListGridRows[i].Tag).RecallNum)) {
					gridRecalls.SetSelected(i,true);
				}
			}
		}

		private void gridRecalls_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			Recall recall=(Recall)gridRecalls.ListGridRows[e.Row].Tag;
			Patient patient=Patients.GetPat(recall.PatNum);
			GlobalFormOpenDental.PatientSelected(patient,isRefreshCurModule: true);
			using FormRecallEdit formRecallEdit=new FormRecallEdit();
			formRecallEdit.RecallCur=recall;
			formRecallEdit.ShowDialog();
			if(formRecallEdit.DialogResult!=DialogResult.OK) {
				return;
			}
			FillGridRecalls();
		}

		private void RemoveRecalls_Click() {
			if(gridRecalls.SelectedIndices.Length==0 
				|| !MsgBox.Show(this,MsgBoxButtons.OKCancel,"Change priority to normal for all selected recalls?")) 
			{
				return;
			}
			List<Recall> listRecallsSelected=gridRecalls.SelectedIndices.Select(x => (Recall)gridRecalls.ListGridRows[x].Tag).ToList();
			for(int i=0;i<listRecallsSelected.Count;i++) {
				listRecallsSelected[i].Priority=RecallPriority.Normal;
				Recalls.Update(listRecallsSelected[i]);
				SecurityLogs.MakeLogEntry(EnumPermType.RecallEdit,listRecallsSelected[i].PatNum,"Recall priority set to Normal from the ASAP List.");
			}
			FillGridRecalls();
		}

		private void MakeAppointment_Click() {
			if(!Security.IsAuthorized(EnumPermType.AppointmentCreate) || gridRecalls.SelectedIndices.Length==0) {
				return;
			}
			List<Recall> listRecallsSelected=gridRecalls.SelectedIndices.Select(x => (Recall)gridRecalls.ListGridRows[x].Tag).ToList();
			for(int i=0;i<listRecallsSelected.Count;i++) {
				Patient patient=Patients.GetPat(listRecallsSelected[i].PatNum);
				Patient patientMerged=AppointmentL.GetPatientMergePrompt(patient.PatNum);
				if(patientMerged!=null) {
					patient=patientMerged;
				}
				if(patient.PatStatus.In(PatientStatus.Archived,PatientStatus.Deceased)) {
					MsgBox.Show(this,"Appointments cannot be scheduled for "+patient.PatStatus.ToString().ToLower()+" patients.");
					return;
				}
				Family family=Patients.GetFamily(patient.PatNum);
				List<InsSub> listInsSubs=InsSubs.RefreshForFam(family);
				List<InsPlan> listInsPlans=InsPlans.RefreshForSubList(listInsSubs);
				Appointment appointment=AppointmentL.CreateRecallApt(patient,listInsPlans,listRecallsSelected[i].RecallNum,listInsSubs);
				GlobalFormOpenDental.GoToModule(EnumModuleType.Appointments, listPinApptNums:new List<long>() { appointment.AptNum }, patNum:patient.PatNum, dateSelected:DateTime.Today);
			}
			FillGridRecalls();
		}
		#endregion Recalls Tab
		
		private void gridMenuRight_click(object sender,System.EventArgs e) {
			ToolStripMenuItem toolStripMenuItem=(ToolStripMenuItem)sender;
			if(tabControl.SelectedIndex==0) {
				if(gridAppts.SelectedIndices.Length<=0) {
					return;
				}
				switch(menuApptsRightClick.Items.IndexOf(toolStripMenuItem)) {
					case 0:
						SelectPatient_Click(((Appointment)gridAppts.SelectedGridRows.Last().Tag).PatNum);
						break;
					case 1:
						if(gridAppts.SelectedIndices.Length==0) {
							MsgBox.Show(this,"Please select an appointment first.");
							return;
						}
						SeeChart_Click(((Appointment)gridAppts.SelectedGridRows.Last().Tag).PatNum);
						break;
					case 2:
						SendPinboard_Click();
						break;
					case 3:
						RemoveAppts_Click();
						break;
				}
			}
			else {//Recall tab selected
				if(gridRecalls.SelectedIndices.Length<=0) {
					return;
				}
				Recall recall=gridRecalls.SelectedIndices.Select(x => (Recall)gridRecalls.ListGridRows[x].Tag).ToList().FirstOrDefault();
				switch(menuRecallsRightClick.Items.IndexOf(toolStripMenuItem)) {
					case 0:
						SelectPatient_Click(recall.PatNum);
						break;
					case 1:
						if(gridRecalls.SelectedIndices.Length==0) {
							MsgBox.Show(this,"Please select a recall first first.");
							return;
						}
						SeeChart_Click(recall.PatNum);
						break;
					case 2:
						MakeAppointment_Click();
						break;
					case 3:
						RemoveRecalls_Click();
						break;
				}
			}
		}

		private void SelectPatient_Click(long patnum) {
			Patient patient=Patients.GetPat(patnum);//If multiple selected, just take the last one to remain consistent with SendPinboard_Click.
			GlobalFormOpenDental.PatientSelected(patient,isRefreshCurModule: true);
		}

		///<summary>If multiple patients are selected in the list, it will use the last patient to show the chart for.</summary>
		private void SeeChart_Click(long patnum) {
			Patient patient=Patients.GetPat(patnum); //If multiple selected, just use the last one.
			GlobalFormOpenDental.PatientSelected(patient,isRefreshCurModule: false); //Selects the patient in OpenDental.
			GlobalFormOpenDental.GoToModule(EnumModuleType.Chart,patNum:patient.PatNum);
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGridAppts();
			FillGridRecalls();
		}

		private void butPrint_Click(object sender,EventArgs e) {
			_pagesPrinted=0;	
			_doPrintHeading=false;
			PrinterL.TryPrintOrDebugRpPreview(pd_PrintPage,Lan.g(this,"ASAP list printed"),PrintoutOrientation.Landscape);
		}

		private void pd_PrintPage(object sender,System.Drawing.Printing.PrintPageEventArgs e) {
			Rectangle bounds=e.MarginBounds;
			//new Rectangle(50,40,800,1035);//Some printers can handle up to 1042
			Graphics g=e.Graphics;
			string text;
			using Font headingFont=new Font("Arial",13,FontStyle.Bold);
			using Font subHeadingFont=new Font("Arial",10,FontStyle.Bold);
			int y=bounds.Top;
			int center=bounds.X+bounds.Width/2;
			#region printHeading
			int headingPrintH=0;
			if(!_doPrintHeading) {
				if(tabControl.SelectedIndex==0) {
					text=Lan.g(this,"ASAP Appointment List");
				}
				else {
					text=Lan.g(this,"ASAP Recall List");
				}
				g.DrawString(text,headingFont,Brushes.Black,center-g.MeasureString(text,headingFont).Width/2,y);
				//yPos+=(int)g.MeasureString(text,headingFont).Height;
				//text=textDateFrom.Text+" "+Lan.g(this,"to")+" "+textDateTo.Text;
				//g.DrawString(text,subHeadingFont,Brushes.Black,center-g.MeasureString(text,subHeadingFont).Width/2,yPos);
				y+=25;
				_doPrintHeading=true;
				headingPrintH=y;
			}
			#endregion
			if(tabControl.SelectedIndex==0) {
				y=gridAppts.PrintPage(g,_pagesPrinted,bounds,headingPrintH);
			}
			else {
				y=gridRecalls.PrintPage(g,_pagesPrinted,bounds,headingPrintH);
			}
			_pagesPrinted++;
			if(y==-1) {
				e.HasMorePages=true;
			}
			else {
				e.HasMorePages=false;
			}
		}

		private void comboShowHygiene_SelectionChangeCommitted(object sender,EventArgs e) {
			FillGridAppts();
		}
	
	}
}