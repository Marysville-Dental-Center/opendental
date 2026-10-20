// Unit tests for where the Appointments module draws mirrors (ControlApptPanel.PlaceMirrors, backlog item v25_3-appts-mirroring): the column of the
// secondary operatory, the time span of each X run, the right-half rule over the operatory's own appointments, and week view. No window is shown and
// no database is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental.UI;
using OpenDentBusiness;

namespace ApptTests {
	[TestClass]
	public class MirrorLayoutTests {
		private static readonly DateTime _date=new DateTime(2026,10,20);//a Tuesday
		private const float HEIGHT_LINE=12f;
		private const float MIN_PER_INCR=10f;
		private const float ROWS_PER_INCR=1f;
		private const float NARROWED=3f;

		private static List<Operatory> GetOps() {
			return new List<Operatory> {
				new Operatory { OperatoryNum=10,ItemOrder=2,ProvDentist=1 },
				new Operatory { OperatoryNum=12,ItemOrder=0,ProvHygienist=2 },
			};
		}

		private static DataTable NewTable() {
			DataTable table=new DataTable("Appointments");
			table.Columns.Add("AptNum");
			table.Columns.Add("AptDateTime",typeof(DateTime));
			table.Columns.Add("AptStatus");
			table.Columns.Add("Op");
			table.Columns.Add("IsHygiene");
			table.Columns.Add("ProvNum");
			table.Columns.Add("ProvHyg");
			table.Columns.Add("Pattern");
			table.Columns.Add("PatternSecondary");
			table.Columns.Add("IsMirrored");
			return table;
		}

		private static void AddRow(DataTable table,long aptNum,int hour,int minute,long opNum,string pattern,string patternSecondary,bool isMirrored,
			ApptStatus apptStatus=ApptStatus.Scheduled)
		{
			DataRow dataRow=table.NewRow();
			dataRow["AptNum"]=aptNum.ToString();
			dataRow["AptDateTime"]=_date.AddHours(hour).AddMinutes(minute);
			dataRow["AptStatus"]=((int)apptStatus).ToString();
			dataRow["Op"]=opNum.ToString();
			dataRow["IsHygiene"]="0";
			dataRow["ProvNum"]="1";
			dataRow["ProvHyg"]="2";
			dataRow["Pattern"]=pattern;
			dataRow["PatternSecondary"]=patternSecondary;
			dataRow["IsMirrored"]=isMirrored ? "1" : "0";
			table.Rows.Add(dataRow);
		}

		private static List<ApptMirrorLayoutInfo> PlaceDay(DataTable table,bool doesViewShowMirrors=true,int widthMain=400) {
			List<DayOfWeek> listDays=new List<DayOfWeek> { _date.DayOfWeek };
			List<ApptLayoutInfo> listApptLayoutInfos=ControlApptPanel.PlaceAppointments(table,GetOps(),HEIGHT_LINE,false,MIN_PER_INCR,1,ROWS_PER_INCR,
				widthMain,NARROWED,_date,_date,listDays);
			return ControlApptPanel.PlaceMirrors(table,GetOps(),new List<Schedule>(),listApptLayoutInfos,doesViewShowMirrors,HEIGHT_LINE,false,MIN_PER_INCR,
				1,ROWS_PER_INCR,widthMain,NARROWED,_date,_date,listDays);
		}

		private static float Y(int hour,int minute) {
			return (hour*60+minute)/MIN_PER_INCR*HEIGHT_LINE*ROWS_PER_INCR;
		}

		[TestMethod]
		public void Day_TwoRunsInTheHygienistColumn() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true);
			List<ApptMirrorLayoutInfo> list=PlaceDay(table);
			Assert.AreEqual(2,list.Count);
			Assert.IsTrue(list.All(x => x.IdxInTableAppointments==0 && x.ApptMirror.AptNum==5 && x.ApptMirror.OpNum==12));
			Assert.AreEqual(new RectangleF(200,Y(9,0),200-NARROWED,HEIGHT_LINE),list[0].RectangleBounds);
			Assert.AreEqual(new RectangleF(200,Y(9,20),200-NARROWED,HEIGHT_LINE),list[1].RectangleBounds);
		}

		[TestMethod]
		public void Day_MirrorOverAnAppointmentUsesTheRightHalf() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true);
			AddRow(table,6,9,0,12,"XX","",false);//the hygienist's own patient, 9:00-9:10
			List<ApptMirrorLayoutInfo> list=PlaceDay(table);
			Assert.AreEqual(2,list.Count);
			float width=200-NARROWED;
			Assert.AreEqual(new RectangleF(200+width/2f,Y(9,0),width/2f,HEIGHT_LINE),list[0].RectangleBounds);
			Assert.AreEqual(new RectangleF(200,Y(9,20),width,HEIGHT_LINE),list[1].RectangleBounds,"The second run is clear of the appointment.");
		}

		[TestMethod]
		public void Day_MirrorsDoNotChangeTheAppointmentLayout() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true);
			AddRow(table,6,9,0,12,"XX","",false);
			List<ApptLayoutInfo> listApptLayoutInfos=ControlApptPanel.PlaceAppointments(table,GetOps(),HEIGHT_LINE,false,MIN_PER_INCR,1,ROWS_PER_INCR,
				400,NARROWED,_date,_date,new List<DayOfWeek> { _date.DayOfWeek });
			//The hygienist's own appointment keeps the whole column: the mirror takes no part in the overlap layout.
			Assert.AreEqual(1,listApptLayoutInfos[1].OverlapSections);
			Assert.AreEqual(200-NARROWED,listApptLayoutInfos[1].RectangleBounds.Width);
		}

		[TestMethod]
		public void Day_NoMirrorsWhenViewOffOrNotMirrored() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true);
			Assert.AreEqual(0,PlaceDay(table,doesViewShowMirrors:false).Count);
			table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",false);
			Assert.AreEqual(0,PlaceDay(table).Count);
			table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true,ApptStatus.UnschedList);
			Assert.AreEqual(0,PlaceDay(table).Count);
		}

		[TestMethod]
		public void Day_OutsideTheDateRangeIsSkipped() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true);
			table.Rows[0]["AptDateTime"]=_date.AddDays(1).AddHours(9);
			Assert.AreEqual(0,PlaceDay(table).Count);
		}

		[TestMethod]
		public void Week_ColumnOfTheDayAndOperatory() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","//XX//",true);
			List<DayOfWeek> listDays=new List<DayOfWeek> { DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday };
			DateTime dateMonday=_date.AddDays(-1);
			List<ApptLayoutInfo> listApptLayoutInfos=ControlApptPanel.PlaceAppointments(table,GetOps(),HEIGHT_LINE,true,MIN_PER_INCR,5,ROWS_PER_INCR,
				1000,NARROWED,dateMonday,dateMonday.AddDays(4),listDays);
			List<ApptMirrorLayoutInfo> list=ControlApptPanel.PlaceMirrors(table,GetOps(),null,listApptLayoutInfos,true,HEIGHT_LINE,true,MIN_PER_INCR,5,
				ROWS_PER_INCR,1000,NARROWED,dateMonday,dateMonday.AddDays(4),listDays);
			Assert.AreEqual(1,list.Count);
			//Each day is 200 wide and each op 100 wide within it: Tuesday is day 1, the hygienist's op is op 1.
			Assert.AreEqual(new RectangleF(300,Y(9,10),100,HEIGHT_LINE),list[0].RectangleBounds);
		}

		[TestMethod]
		public void Row_WithoutMirrorColumnsIsIgnored() {
			DataTable table=new DataTable();
			table.Columns.Add("AptNum");
			DataRow dataRow=table.NewRow();
			dataRow["AptNum"]="5";
			table.Rows.Add(dataRow);
			Assert.IsNull(ControlApptPanel.GetApptForMirror(dataRow));
			Assert.IsNull(ControlApptPanel.GetApptForMirror(null));
		}

		[TestMethod]
		public void Row_ReadsTheMirrorFields() {
			DataTable table=NewTable();
			AddRow(table,5,9,0,10,"XXXXXX","XX//XX",true,ApptStatus.Complete);
			Appointment appointment=ControlApptPanel.GetApptForMirror(table.Rows[0]);
			Assert.AreEqual(5,appointment.AptNum);
			Assert.AreEqual(_date.AddHours(9),appointment.AptDateTime);
			Assert.AreEqual(ApptStatus.Complete,appointment.AptStatus);
			Assert.AreEqual(10,appointment.Op);
			Assert.IsFalse(appointment.IsHygiene);
			Assert.AreEqual(1,appointment.ProvNum);
			Assert.AreEqual(2,appointment.ProvHyg);
			Assert.AreEqual("XX//XX",appointment.PatternSecondary);
			Assert.IsTrue(appointment.IsMirrored);
		}
	}
}
