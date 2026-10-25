// Package B-APPT (docs/specs/port-backlog.md), item v25_2-appts-waiting-room-window: the Wait Rm button of the Appointments module and the
// state it hands to the separate Waiting Room window (FormWaitingRoom). The docked Waiting Room grid is unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	public partial class ControlAppt {
		///<summary>The one Waiting Room window, or null. Closed with the other windows on log off.</summary>
		private FormWaitingRoom _formWaitingRoom;

		///<summary>Called from LayoutToolBar, after the Delete button.</summary>
		private void AddToolBarButtonWaitingRoom() {
			toolBarMain.Buttons.Add(new ODToolBarButton(ODToolBarButtonStyle.Separator));
			toolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Wait Rm"),butWaitingRoom_Click,EnumIcons.None,
				Lan.g(this,"Waiting Room in a separate window"),"WaitRm"));
		}

		///<summary>Opens the Waiting Room window, or brings the open one to the front.</summary>
		private void butWaitingRoom_Click(object sender,EventArgs e) {
			if(_formWaitingRoom!=null && !_formWaitingRoom.IsDisposed) {
				if(_formWaitingRoom.WindowState==FormWindowState.Minimized) {
					_formWaitingRoom.WindowState=FormWindowState.Normal;
				}
				_formWaitingRoom.BringToFront();
				_formWaitingRoom.Activate();
				return;
			}
			_formWaitingRoom=new FormWaitingRoom(GetWaitingRoomContext);
			_formWaitingRoom.FormClosed+=(o,args) => _formWaitingRoom=null;
			_formWaitingRoom.Show(FindForm());
		}

		///<summary>The docked grid's table and the filter for the current view and clinic, built the same way FillWaitingRoom does.</summary>
		private ApptWaitingRoomContext GetWaitingRoomContext() {
			ApptWaitingRoomContext context=new ApptWaitingRoomContext();
			context.TableWaitingRoom=contrApptPanel.TableWaitingRoom;
			context.DateTimeRefreshed=_dateTimeWaitingRmRefreshed;
			ApptView apptView=GetApptViewCur();
			if(apptView!=null) {
				context.Filter.NameFormat=apptView.WaitingRmName;
			}
			if(PrefC.GetBool(PrefName.WaitingRoomFilterByView)) {
				//Like the docked grid: the operatories visible TODAY in the current view, whatever day the module shows.
				List<Schedule> listSchedulesForToday=(contrApptPanel.ListSchedules??new List<Schedule>()).FindAll(x => x.SchedDate==DateTime.Today);
				context.Filter.IsFilteredByView=true;
				context.Filter.ListOpNumsView=ApptViewItemL.GetOpsForApptView(apptView,contrApptPanel.IsWeeklyView,listSchedulesForToday)
					.Select(x => x.OperatoryNum).ToList();
			}
			if(PrefC.HasClinicsEnabled && Clinics.ClinicNum!=0) {//Headquarters shows every clinic.
				context.Filter.IsFilteredByClinic=true;
				context.Filter.ListOpNumsClinic=Operatories.GetOpsForClinic(Clinics.ClinicNum).Select(x => x.OperatoryNum).ToList();
			}
			context.Filter.AlertMinutes=PrefC.GetInt(PrefName.WaitingRoomAlertTime);
			return context;
		}
	}
}
