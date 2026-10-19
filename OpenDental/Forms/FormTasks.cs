using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental{
	/// <summary></summary>
	public partial class FormTasks:FormODBase {
		//private System.ComponentModel.IContainer components;
		/////<summary>After closing, if this is not zero, then it will jump to the object specified in GotoKeyNum.</summary>
		//public TaskObjectType GotoType;
		/////<summary>After closing, if this is not zero, then it will jump to the specified patient.</summary>
		//public long GotoKeyNum;
		public static Color ColorLightRed=Color.FromArgb(247,110,110);

		public void SetUserControlTasksTab(UserControlTasksTab newTab){
			userControlTasks1.TaskTab=newTab;
		}

		///<summary></summary>
		public FormTasks()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			//Lan.F(this);
		}

		private void FormTasks_Load(object sender,EventArgs e) {
			userControlTasks1.InitializeOnStartup();
			splitContainer.Panel2Collapsed=true;
		}

		private void userControlTasks1_GoToChanged(object sender,EventArgs e) {
			TaskObjectType taskObjectTypeGoto=userControlTasks1.TaskObjectTypeGoTo;
			long gotoKeyNum=userControlTasks1.KeyNumGoTo;
			if(taskObjectTypeGoto==TaskObjectType.Patient){
				if(gotoKeyNum!=0){
					Patient patient=Patients.GetPat(gotoKeyNum);
					//OnPatientSelected(pat);
					GlobalFormOpenDental.GoToModule(EnumModuleType.Account,patNum:patient.PatNum);
				}
			}
			if(taskObjectTypeGoto!=TaskObjectType.Appointment){
				return;
			}
			if(gotoKeyNum==0){
				return;
			}
			Appointment appointment=Appointments.GetOneApt(gotoKeyNum);
			if(appointment==null){
				MsgBox.Show(this,"Appointment has been deleted, so it's not available.");
				return;
				//this could be a little better, because window has closed, but they will learn not to push that button.
			}
			DateTime dateTimeSelected=DateTime.MinValue;
			if(appointment.AptStatus==ApptStatus.Planned || appointment.AptStatus==ApptStatus.UnschedList){
				//I did not add feature to put planned or unsched apt on pinboard.
				MsgBox.Show(this,"Cannot navigate to appointment.  Use the Other Appointments button.");
				//return;
			}
			else{
				dateTimeSelected=appointment.AptDateTime;
			}
			//Patient patient=Patients.GetPat(appointment.PatNum);
			//OnPatientSelected(pat);
			GlobalFormOpenDental.GoToModule(EnumModuleType.Appointments, dateSelected:dateTimeSelected,selectedAptNum:appointment.AptNum);
			//DialogResult=DialogResult.OK;
		}

		private void UserControlTasks1_FillGridEvent(object sender,EventArgs e) {
			this.Text=userControlTasks1.TitleControlParent;
		}

		/* private void timer1_Tick(object sender,EventArgs e) {
				if(Security.CurUser!=null) {//Possible if OD auto logged a user off and they left the task window open in the background.
					userControlTasks1.RefreshTasks();
				}
				//this quick and dirty refresh is not as intelligent as the one used when tasks are docked.
				//Sound notification of new task is controlled from main form completely
				//independently of this visual refresh.
			}
		}
		*/














	}
}





















