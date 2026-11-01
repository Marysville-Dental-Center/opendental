// Package B-APPT (docs/specs/port-backlog.md), item v25_2-appts-waiting-room-window: the Waiting Room list in its own non-modal window, opened
// with the Wait Rm button of the Appointments module. It can sit on another monitor and stays open while the user works in other modules.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>What the Appointments module currently shows in its docked Waiting Room grid: the table it read, when it read it, and the
	///filter for its current view and clinic.</summary>
	public class ApptWaitingRoomContext {
		public DataTable TableWaitingRoom;
		public DateTime DateTimeRefreshed;
		public ApptWaitingRoomFilter Filter=new ApptWaitingRoomFilter();
	}

	///<summary>Same rows and filters as the docked grid (ApptWaitingRoom.GetRows). The wait times tick every second. The data is refreshed from
	///the database every 60 seconds, and sooner whenever the Appointments module reads a newer table (module refreshes and appointment
	///signals). Double-clicking a row selects the patient and jumps to the appointment. One instance; it closes with the other windows on log
	///off. Size and position are remembered on this computer.</summary>
	public partial class FormWaitingRoom:FormODBase {
		///<summary>Seconds between the window's own database refreshes, matching the module's 60-second refresh.</summary>
		public const int REFRESH_SECONDS=60;
		private Func<ApptWaitingRoomContext> _funcGetContext;
		private DataTable _tableWaitingRoom;
		private DateTime _dateTimeRefreshed=DateTime.MinValue;
		private List<ApptWaitingRoomRow> _listRows=new List<ApptWaitingRoomRow>();

		///<summary>funcGetContext returns the Appointments module's current waiting room state; it is called on the main thread once a second.</summary>
		public FormWaitingRoom(Func<ApptWaitingRoomContext> funcGetContext) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_funcGetContext=funcGetContext;
			Form formMain=Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Icon!=null && x!=this);
			if(formMain!=null) {
				Icon=formMain.Icon;
			}
		}

		private void FormWaitingRoom_Load(object sender,EventArgs e) {
			Rectangle? rectangleSaved=WaitingRoomWindowBounds.Load();
			if(rectangleSaved!=null) {
				StartPosition=FormStartPosition.Manual;
				Bounds=rectangleSaved.Value;
			}
			RefreshAndFill();
			timerTick.Start();
		}

		private void timerTick_Tick(object sender,EventArgs e) {
			RefreshAndFill();
		}

		///<summary>Takes the module's table when it is newer than ours, reads the database when ours is older than REFRESH_SECONDS, then refills.</summary>
		private void RefreshAndFill() {
			ApptWaitingRoomContext context=GetContext();
			if(context.TableWaitingRoom!=null && context.DateTimeRefreshed>_dateTimeRefreshed) {
				_tableWaitingRoom=context.TableWaitingRoom;
				_dateTimeRefreshed=context.DateTimeRefreshed;
			}
			if(_tableWaitingRoom==null || (DateTime.Now-_dateTimeRefreshed).TotalSeconds>=REFRESH_SECONDS) {
				try {
					_tableWaitingRoom=Appointments.GetPeriodWaitingRoomTable(DateTime.Now);
					_dateTimeRefreshed=DateTime.Now;
				}
				catch(Exception ex) {
					ex.DoNothing();//Keep the last table; the next tick tries again.
				}
			}
			FillGrid(context.Filter);
		}

		private ApptWaitingRoomContext GetContext() {
			ApptWaitingRoomContext context=null;
			try {
				context=_funcGetContext?.Invoke();
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return context??new ApptWaitingRoomContext();
		}

		private void FillGrid(ApptWaitingRoomFilter filter) {
			long aptNumSelected=0;
			if(gridMain.GetSelectedIndex()>-1 && gridMain.GetSelectedIndex()<_listRows.Count) {
				aptNumSelected=_listRows[gridMain.GetSelectedIndex()].AptNum;
			}
			_listRows=ApptWaitingRoom.GetRows(_tableWaitingRoom,DateTime.Now-_dateTimeRefreshed,filter);
			Color colorAlert=PrefC.GetColor(PrefName.WaitingRoomAlertColor);
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g("TableApptWaiting","Patient"),130){ IsWidthDynamic=true });
			gridMain.Columns.Add(new GridColumn(Lan.g("TableApptWaiting","Waited"),100,HorizontalAlignment.Center));
			gridMain.ListGridRows.Clear();
			int idxSelect=-1;
			for(int i=0;i<_listRows.Count;i++) {
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(_listRows[i].PatName);
				gridRow.Cells.Add(_listRows[i].WaitText);
				gridRow.Bold=false;
				if(_listRows[i].IsAlert) {
					gridRow.ColorText=colorAlert;
					gridRow.Bold=true;
				}
				gridRow.Tag=_listRows[i];
				gridMain.ListGridRows.Add(gridRow);
				if(aptNumSelected!=0 && _listRows[i].AptNum==aptNumSelected) {
					idxSelect=i;
				}
			}
			gridMain.EndUpdate();
			if(idxSelect>-1) {
				gridMain.SetSelected(idxSelect,true);
			}
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=_listRows.Count) {
				return;
			}
			ApptWaitingRoomRow row=_listRows[e.Row];
			if(row.PatNum==0) {
				return;
			}
			Patient patient=Patients.GetPat(row.PatNum);
			if(patient==null) {
				return;
			}
			GlobalFormOpenDental.PatientSelected(patient,false);
			GlobalFormOpenDental.GoToModule(EnumModuleType.Appointments,dateSelected:DateTime.Today,selectedAptNum:row.AptNum,patNum:row.PatNum);
		}

		private void FormWaitingRoom_FormClosing(object sender,FormClosingEventArgs e) {
			timerTick.Stop();
			if(WindowState==FormWindowState.Normal) {
				WaitingRoomWindowBounds.Save(Bounds);
			}
			else {
				WaitingRoomWindowBounds.Save(RestoreBounds);
			}
		}
	}

	///<summary>Remembers the Waiting Room window's size and position on this computer, in the user's local application data. Nothing is
	///stored in the database. A saved position that is no longer on any screen is ignored.</summary>
	public static class WaitingRoomWindowBounds {
		public static string GetFilePath() {
			string folderAppData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if(string.IsNullOrEmpty(folderAppData)) {
				folderAppData=Path.GetTempPath();
			}
			return Path.Combine(folderAppData,ProgramNames.LocalAppDataFolderName,"WaitingRoomWindow.txt");
		}

		///<summary>"x,y,width,height", or null when missing or invalid.</summary>
		public static Rectangle? Parse(string text) {
			if(string.IsNullOrWhiteSpace(text)) {
				return null;
			}
			string[] arrayParts=text.Trim().Split(',');
			if(arrayParts.Length!=4) {
				return null;
			}
			int[] arrayValues=new int[4];
			for(int i=0;i<4;i++) {
				if(!int.TryParse(arrayParts[i].Trim(),out arrayValues[i])) {
					return null;
				}
			}
			if(arrayValues[2]<100 || arrayValues[3]<100) {
				return null;
			}
			return new Rectangle(arrayValues[0],arrayValues[1],arrayValues[2],arrayValues[3]);
		}

		public static string Format(Rectangle rectangle) {
			return rectangle.X+","+rectangle.Y+","+rectangle.Width+","+rectangle.Height;
		}

		public static Rectangle? Load() {
			try {
				string path=GetFilePath();
				if(!File.Exists(path)) {
					return null;
				}
				Rectangle? rectangle=Parse(File.ReadAllText(path));
				if(rectangle==null || !System.Windows.Forms.Screen.AllScreens.Any(x => x.WorkingArea.IntersectsWith(rectangle.Value))) {
					return null;
				}
				return rectangle;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		public static void Save(Rectangle rectangle) {
			try {
				string path=GetFilePath();
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path,Format(rectangle));
			}
			catch(Exception ex) {
				ex.DoNothing();//Not remembering the position is harmless.
			}
		}
	}
}
