// Unit tests for the rows of the separate Waiting Room window (item v25_2-appts-waiting-room-window): the same filters, name formats, alert
// and ticking wait times as the docked grid, and the remembered window position. No database, no window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;
using OpenDentBusiness;

namespace BAPPTTests {
	[TestClass]
	public class WaitingRoomRowTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		///<summary>The columns Appointments.GetPeriodWaitingRoomTable returns.</summary>
		private static DataTable NewTable(bool hasKeys=true) {
			DataTable table=new DataTable("WaitingRoom");
			foreach(string column in new[] { "patName","FName","LName","waitTime","OpNum" }) {
				table.Columns.Add(column);
			}
			if(hasKeys) {
				table.Columns.Add("AptNum");
				table.Columns.Add("PatNum");
			}
			return table;
		}

		private static void AddRow(DataTable table,string fName,string lName,string waitTime,long opNum,long aptNum=0,long patNum=0) {
			DataRow dataRow=table.NewRow();
			dataRow["patName"]=lName+", "+fName;
			dataRow["FName"]=fName;
			dataRow["LName"]=lName;
			dataRow["waitTime"]=waitTime;
			dataRow["OpNum"]=opNum.ToString();
			if(table.Columns.Contains("AptNum")) {
				dataRow["AptNum"]=aptNum.ToString();
				dataRow["PatNum"]=patNum.ToString();
			}
			table.Rows.Add(dataRow);
		}

		private static DataTable ThreeRows() {
			DataTable table=NewTable();
			AddRow(table,"Ann","Alpha","0:02:00",11,aptNum:101,patNum:201);
			AddRow(table,"Bob","Bravo","0:09:30",12,aptNum:102,patNum:202);
			AddRow(table,"Cy","Charlie","0:20:00",13,aptNum:103,patNum:203);
			return table;
		}

		[TestMethod]
		public void NoFilter_AllRowsWithKeys() {
			List<ApptWaitingRoomRow> listRows=ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,new ApptWaitingRoomFilter());
			Assert.AreEqual("Alpha, Ann|Bravo, Bob|Charlie, Cy",string.Join("|",listRows.Select(x => x.PatName)));
			CollectionAssert.AreEqual(new long[] { 101,102,103 },listRows.Select(x => x.AptNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 201,202,203 },listRows.Select(x => x.PatNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 11,12,13 },listRows.Select(x => x.OpNum).ToArray());
		}

		[TestMethod]
		public void NullInputs_GiveEmptyOrUnfilteredRows() {
			Assert.AreEqual(0,ApptWaitingRoom.GetRows(null,TimeSpan.Zero,new ApptWaitingRoomFilter()).Count);
			Assert.AreEqual(3,ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,null).Count);
		}

		[TestMethod]
		public void FilterByView_KeepsTheViewsOperatories() {
			ApptWaitingRoomFilter filter=new ApptWaitingRoomFilter { IsFilteredByView=true,ListOpNumsView=new List<long> { 11,13 } };
			CollectionAssert.AreEqual(new long[] { 101,103 },ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,filter).Select(x => x.AptNum).ToArray());
		}

		[TestMethod]
		public void FilterByClinic_KeepsTheClinicsOperatories_AndCombinesWithTheView() {
			ApptWaitingRoomFilter filter=new ApptWaitingRoomFilter { IsFilteredByClinic=true,ListOpNumsClinic=new List<long> { 12,13 } };
			CollectionAssert.AreEqual(new long[] { 102,103 },ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,filter).Select(x => x.AptNum).ToArray());
			filter.IsFilteredByView=true;
			filter.ListOpNumsView=new List<long> { 11,13 };
			CollectionAssert.AreEqual(new long[] { 103 },ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,filter).Select(x => x.AptNum).ToArray());
		}

		[TestMethod]
		public void Headquarters_ShowsEveryClinic() {
			//With Headquarters selected the module does not filter by clinic (IsFilteredByClinic false), even with an empty clinic list.
			ApptWaitingRoomFilter filter=new ApptWaitingRoomFilter { IsFilteredByClinic=false,ListOpNumsClinic=new List<long>() };
			Assert.AreEqual(3,ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,filter).Count);
		}

		[TestMethod]
		public void NameFormats_FollowTheView() {
			DataTable table=NewTable();
			AddRow(table,"Ann","Alpha","0:01:00",11);
			Assert.AreEqual("Alpha, Ann",ApptWaitingRoom.GetPatName(table.Rows[0],EnumWaitingRmName.LastFirst));
			Assert.AreEqual("Ann, A",ApptWaitingRoom.GetPatName(table.Rows[0],EnumWaitingRmName.FirstLastI));
			Assert.AreEqual("Ann",ApptWaitingRoom.GetPatName(table.Rows[0],EnumWaitingRmName.First));
			Assert.AreEqual("Alpha, Ann",ApptWaitingRoom.GetPatName(table.Rows[0],null),"No view: the table's own patName.");
		}

		[TestMethod]
		public void NameFormat_FirstLastInitial_EmptyLastName() {
			DataTable table=NewTable();
			AddRow(table,"Ann","","0:01:00",11);
			Assert.AreEqual("Ann, ",ApptWaitingRoom.GetPatName(table.Rows[0],EnumWaitingRmName.FirstLastI));
		}

		[TestMethod]
		public void WaitTimes_TickBetweenRefreshes() {
			List<ApptWaitingRoomRow> listRows=ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.FromSeconds(75),new ApptWaitingRoomFilter());
			CollectionAssert.AreEqual(new[] { "0:03:15","0:10:45","0:21:15" },listRows.Select(x => x.WaitText).ToArray());
		}

		[TestMethod]
		public void Alert_FromTheAlertMinutes() {
			ApptWaitingRoomFilter filter=new ApptWaitingRoomFilter { AlertMinutes=10 };
			CollectionAssert.AreEqual(new[] { false,false,true },ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.Zero,filter).Select(x => x.IsAlert).ToArray());
			CollectionAssert.AreEqual(new[] { false,true,true },
				ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.FromSeconds(31),filter).Select(x => x.IsAlert).ToArray(),"0:09:30 plus 31 seconds reaches 10 minutes.");
			filter.AlertMinutes=0;
			Assert.IsFalse(ApptWaitingRoom.GetRows(ThreeRows(),TimeSpan.FromHours(2),filter).Any(x => x.IsAlert),"0 means no alert.");
		}

		[TestMethod]
		public void OldTableWithoutKeys_StillShows() {
			DataTable table=NewTable(hasKeys:false);
			AddRow(table,"Ann","Alpha","0:02:00",11);
			List<ApptWaitingRoomRow> listRows=ApptWaitingRoom.GetRows(table,TimeSpan.Zero,new ApptWaitingRoomFilter());
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual(0,listRows[0].AptNum);
			Assert.AreEqual(0,listRows[0].PatNum);
		}

		[TestMethod]
		public void WindowBounds_RoundTrip() {
			Rectangle rectangle=new Rectangle(-1200,40,300,420);
			Assert.AreEqual(rectangle,WaitingRoomWindowBounds.Parse(WaitingRoomWindowBounds.Format(rectangle)));
			Assert.AreEqual(new Rectangle(1,2,300,400),WaitingRoomWindowBounds.Parse(" 1, 2,300 ,400 "));
		}

		[TestMethod]
		public void WindowBounds_InvalidTextIsIgnored() {
			Assert.IsNull(WaitingRoomWindowBounds.Parse(null));
			Assert.IsNull(WaitingRoomWindowBounds.Parse(""));
			Assert.IsNull(WaitingRoomWindowBounds.Parse("1,2,3"));
			Assert.IsNull(WaitingRoomWindowBounds.Parse("a,b,c,d"));
			Assert.IsNull(WaitingRoomWindowBounds.Parse("0,0,20,20"),"Too small to be a real window.");
			StringAssert.EndsWith(WaitingRoomWindowBounds.GetFilePath(),"WaitingRoomWindow.txt");
		}
	}
}
