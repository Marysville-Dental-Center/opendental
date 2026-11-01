// Source checks for the B-APPT window and module hooks. The Appointments module, the Chart module and their windows are WinForms code that
// these tests cannot open (the desktop client is never launched by tests), so they check the code that wires the tested rules in instead.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BAPPTTests {
	[TestClass]
	public class UiSourceTests {
		private static string Read(params string[] arrayParts) {
			return ScratchDb.ReadRepoFile(arrayParts);
		}

		private static string GetMethodBody(string code,string signatureStart) {
			int idxStart=code.IndexOf(signatureStart);
			Assert.IsTrue(idxStart>=0,"Not found: "+signatureStart);
			int idxBrace=code.IndexOf('{',idxStart);
			int depth=0;
			for(int i=idxBrace;i<code.Length;i++) {
				if(code[i]=='{') {
					depth++;
				}
				else if(code[i]=='}') {
					depth--;
					if(depth==0) {
						return code.Substring(idxStart,i-idxStart+1);
					}
				}
			}
			return code.Substring(idxStart);
		}

		[TestMethod]
		public void CopyView_ButtonCopiesAndOpensTheCopy() {
			string designer=Read("OpenDental","Forms","FormApptViews.designer.cs");
			string code=Read("OpenDental","Forms","FormApptViews.cs");
			StringAssert.Contains(designer,"this.butCopy.Text = \"Copy View\";");
			StringAssert.Contains(designer,"this.butCopy.Enabled = false;");
			string body=GetMethodBody(code,"private void butCopy_Click(");
			StringAssert.Contains(body,"ApptViews.CopyView(apptViewSource.ApptViewNum)");
			StringAssert.Contains(body,"formApptViewEdit.IsNew=true;");
			StringAssert.Contains(body,"formApptViewEdit.IsCopy=true;");
			StringAssert.Contains(body,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,ApptViews.GetCopyLogText(");
			StringAssert.Contains(GetMethodBody(code,"private void listViews_SelectedIndexChanged("),"butCopy.Enabled=(listViews.SelectedIndex!=-1);");
			string edit=Read("OpenDental","Forms","FormApptViewEdit.cs");
			StringAssert.Contains(edit,"if(IsNew && !IsCopy) {","A copy keeps its bubble setting.");
		}

		[TestMethod]
		public void TimeBars_DrawingHooks() {
			string panel=Read("OpenDental","UI","Appt","ControlApptPanel.cs");
			Assert.AreEqual(2,Regex.Matches(panel,@"DrawOpBackgroundProvTime\(g,provSchedsForOp,").Count,"Week view and day view.");
			StringAssert.Contains(GetMethodBody(panel,"private void DrawOneAppt("),"DrawTimeBarSecondaryNoProvTime(g,dataRoww,graphicsPathLeftSide);");
			string timeBars=Read("OpenDental","UI","Appt","ControlApptPanel.TimeBars.cs");
			StringAssert.Contains(timeBars,"ApptTimeBars.IsProvTimeOnOpBackground()");
			StringAssert.Contains(timeBars,"PrintColorBehavior.In(ApptPrintColorBehavior.LessColor,ApptPrintColorBehavior.Grayscale)");
			StringAssert.Contains(timeBars,"ApptTimeBars.IsSecondaryColorNoProvTime()");
			Assert.IsFalse(GetMethodBody(panel,"private void ProvBarShading(").Contains("ApptTimeBars"),"The provider bar shading at the left is left alone.");
		}

		[TestMethod]
		public void TimeBars_PreferenceCheckboxes() {
			string code=Read("OpenDental","User Controls","Preferences","UserControlApptAppearance.cs");
			string designer=Read("OpenDental","User Controls","Preferences","UserControlApptAppearance.Designer.cs");
			StringAssert.Contains(code,"checkProvTimeOnOpBackground.Checked=ApptTimeBars.IsProvTimeOnOpBackground();");
			StringAssert.Contains(code,"checkTimeBarSecondaryColor.Checked=ApptTimeBars.IsSecondaryColorNoProvTime();");
			StringAssert.Contains(code,"ApptTimeBars.UpdateBoolPref(ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND,checkProvTimeOnOpBackground.Checked)");
			StringAssert.Contains(code,"ApptTimeBars.UpdateBoolPref(ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME,checkTimeBarSecondaryColor.Checked)");
			StringAssert.Contains(designer,"this.groupBoxTimeBars.Controls.Add(this.checkProvTimeOnOpBackground);");
			StringAssert.Contains(designer,"this.Controls.Add(this.groupBoxTimeBars);");
		}

		[TestMethod]
		public void WaitingRoom_ToolbarButtonOpensOneWindow() {
			string module=Read("OpenDental","Main Modules","ControlAppt.cs");
			StringAssert.Contains(GetMethodBody(module,"public void LayoutToolBar("),"AddToolBarButtonWaitingRoom();");
			string partial=Read("OpenDental","Main Modules","ControlAppt.WaitingRoom.cs");
			StringAssert.Contains(partial,"Lan.g(this,\"Wait Rm\")");
			string click=GetMethodBody(partial,"private void butWaitingRoom_Click(");
			StringAssert.Contains(click,"_formWaitingRoom.BringToFront();","A second click brings the open window to the front.");
			StringAssert.Contains(click,"_formWaitingRoom.Show(","Non-modal.");
			string form=Read("OpenDental","Forms","FormWaitingRoom.cs");
			StringAssert.Contains(form,"ApptWaitingRoom.GetRows(");
			StringAssert.Contains(form,"Appointments.GetPeriodWaitingRoomTable(DateTime.Now)");
			StringAssert.Contains(form,"GlobalFormOpenDental.GoToModule(EnumModuleType.Appointments");
			StringAssert.Contains(form,"WaitingRoomWindowBounds.Save(");
		}

		[TestMethod]
		public void Permissions_Unschedule_AndPinFromAppointmentL() {
			string code=Read("OpenDental","Logic","AppointmentL.cs");
			StringAssert.Contains(GetMethodBody(code,"public static bool ValidateApptUnsched("),"ApptPermissionRules.GetPermsToUnschedule(appointment.AptStatus)");
			StringAssert.Contains(GetMethodBody(code,"public static bool ValidateApptToPinboard("),"ApptPermissionRules.IsMoveNeededToPin(appointment.AptStatus)");
		}

		[TestMethod]
		public void Permissions_PlannedToPinboardNeedsNoMove() {
			string chart=GetMethodBody(Read("OpenDental","Main Modules","ControlChart.cs"),"private void butPin_Click(");
			StringAssert.Contains(chart,"ApptPermissionRules.IsPlannedRowPinAMove(");
			Assert.IsFalse(Regex.IsMatch(chart,@"\{\s*if\(!Security\.IsAuthorized\(EnumPermType\.AppointmentMove\)\)"),"No up-front Appointment Move check.");
			string other=GetMethodBody(Read("OpenDental","Forms","FormApptsOther.cs"),"private void butPin_Click(");
			StringAssert.Contains(other,"ApptPermissionRules.IsMoveNeededToPin(apptOther.AptStatus) && !Security.IsAuthorized(EnumPermType.AppointmentMove)");
			StringAssert.Contains(other,"PatRestrictionL.IsRestricted(","The patient restriction stays.");
			string edit=GetMethodBody(Read("OpenDental","Forms","FormApptEdit.cs"),"private void butPin_Click(");
			StringAssert.Contains(edit,"ApptPermissionRules.IsMoveNeededToPin(_appointment.AptStatus) && !Security.IsAuthorized(EnumPermType.AppointmentMove)");
		}

		[TestMethod]
		public void Permissions_PlannedFromPinboardNeedsCreate() {
			//Already in 24.3: dropping a planned appointment off the pinboard checks Appointment Create, and nothing there checks Move.
			string drop=GetMethodBody(Read("OpenDental","Main Modules","ControlAppt.cs"),"private void pinBoard_ApptMovedFromPinboard(");
			StringAssert.Contains(drop,"if(!Security.IsAuthorized(EnumPermType.AppointmentCreate))");
			Assert.IsFalse(drop.Contains("EnumPermType.AppointmentMove)"),"Scheduling from the pinboard never checks Appointment Move.");
		}

		[TestMethod]
		public void Tasks_OnlyOpenTasksCount() {
			string edit=GetMethodBody(Read("OpenDental","Forms","FormApptEdit.cs"),"private void butTask_Click(");
			StringAssert.Contains(edit,"ApptTasks.GetTaskButtonAction(false,Tasks.GetMany(_appointment.AptNum),out Task taskOpen)");
			string other=GetMethodBody(Read("OpenDental","Forms","FormApptsOther.cs"),"public void RefreshData(");
			StringAssert.Contains(other,"ApptTasks.GetApptNumsWithOpenTask(");
			string tree=Read("OpenDental","User Controls","UserControlTasks.cs");
			StringAssert.Contains(GetMethodBody(tree,"private void Copy_Clicked("),"ApptTasks.IsCopyBlocked(PrefC.GetBool(PrefName.TasksForApptAllowMultiple),taskCur)");
			StringAssert.Contains(GetMethodBody(tree,"private void Paste_Clicked("),"ApptTasks.IsClipboardClearedAfterPaste(PrefC.GetBool(PrefName.TasksForApptAllowMultiple),_taskClip,_wasCut)");
		}
	}
}
