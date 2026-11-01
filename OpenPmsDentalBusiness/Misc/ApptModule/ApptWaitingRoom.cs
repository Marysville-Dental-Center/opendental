// Package B-APPT (docs/specs/port-backlog.md), item v25_2-appts-waiting-room-window: the rows of the separate Waiting Room window. They follow
// the same rules as the docked Waiting Room grid of the Appointments module (ControlAppt.FillWaitingRoom), which is left as it is.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace OpenPmsDentalBusiness {
	///<summary>How the waiting room list is filtered and shown. Built from the Appointments module's current view, clinic and preferences.</summary>
	public class ApptWaitingRoomFilter {
		///<summary>The current view's Waiting Room Name Format, or null when there is no view (then the table's patName is shown).</summary>
		public EnumWaitingRmName? NameFormat;
		///<summary>WaitingRoomFilterByView: only rows whose operatory is in ListOpNumsView.</summary>
		public bool IsFilteredByView;
		public List<long> ListOpNumsView=new List<long>();
		///<summary>Clinics on and a clinic (not Headquarters) selected: only rows whose operatory is in ListOpNumsClinic. With Headquarters
		///selected, patients from every clinic show.</summary>
		public bool IsFilteredByClinic;
		public List<long> ListOpNumsClinic=new List<long>();
		///<summary>WaitingRoomAlertTime in minutes; 0 means no alert.</summary>
		public int AlertMinutes;
	}

	///<summary>One row of the waiting room list.</summary>
	public class ApptWaitingRoomRow {
		public string PatName;
		///<summary>Time waited, H:mm:ss.</summary>
		public string WaitText;
		///<summary>True when the wait reached the alert time; shown bold in the alert color.</summary>
		public bool IsAlert;
		public long AptNum;
		public long PatNum;
		public long OpNum;
	}

	public static class ApptWaitingRoom {
		///<summary>Builds the rows from Appointments.GetPeriodWaitingRoomTable. timeSinceRefresh is added to each stored wait time, so the times
		///keep ticking between database refreshes.</summary>
		public static List<ApptWaitingRoomRow> GetRows(DataTable table,TimeSpan timeSinceRefresh,ApptWaitingRoomFilter filter) {
			List<ApptWaitingRoomRow> listRows=new List<ApptWaitingRoomRow>();
			if(table==null) {
				return listRows;
			}
			if(filter==null) {
				filter=new ApptWaitingRoomFilter();
			}
			for(int i=0;i<table.Rows.Count;i++) {
				DataRow dataRow=table.Rows[i];
				long opNum=PIn.Long(GetString(dataRow,"OpNum"));
				//Filter by view first, regardless of clinics, as the docked grid does.
				if(filter.IsFilteredByView && (filter.ListOpNumsView==null || !filter.ListOpNumsView.Contains(opNum))) {
					continue;
				}
				if(filter.IsFilteredByClinic && (filter.ListOpNumsClinic==null || !filter.ListOpNumsClinic.Contains(opNum))) {
					continue;
				}
				ApptWaitingRoomRow row=new ApptWaitingRoomRow();
				row.PatName=GetPatName(dataRow,filter.NameFormat);
				DateTime dateTimeWait=GetTimeWaited(GetString(dataRow,"waitTime"),timeSinceRefresh);
				row.WaitText=dateTimeWait.ToString("H:mm:ss");
				row.IsAlert=(filter.AlertMinutes>0 && filter.AlertMinutes<=dateTimeWait.Minute+dateTimeWait.Hour*60);
				row.AptNum=PIn.Long(GetString(dataRow,"AptNum"));
				row.PatNum=PIn.Long(GetString(dataRow,"PatNum"));
				row.OpNum=opNum;
				listRows.Add(row);
			}
			return listRows;
		}

		///<summary>The patient name in the view's Waiting Room Name Format.</summary>
		public static string GetPatName(DataRow dataRow,EnumWaitingRmName? nameFormat) {
			string fName=GetString(dataRow,"FName");
			string lName=GetString(dataRow,"LName");
			if(nameFormat==null) {
				return GetString(dataRow,"patName");
			}
			switch(nameFormat.Value) {
				case EnumWaitingRmName.LastFirst:
					return lName+", "+fName;
				case EnumWaitingRmName.FirstLastI:
					return fName+", "+(lName.Length>0 ? lName.Substring(0,1) : "");
				case EnumWaitingRmName.First:
					return fName;
				default:
					return GetString(dataRow,"patName");
			}
		}

		///<summary>The stored wait time (H:mm:ss, a time of day standing for the time waited) plus the time since the table was read.</summary>
		public static DateTime GetTimeWaited(string waitTime,TimeSpan timeSinceRefresh) {
			DateTime dateTimeWait;
			if(!DateTime.TryParse(waitTime,out dateTimeWait)) {
				if(TimeSpan.TryParse(waitTime,CultureInfo.InvariantCulture,out TimeSpan timeSpanWait)) {
					dateTimeWait=DateTime.Today.Add(timeSpanWait);
				}
				else {
					dateTimeWait=DateTime.Today;
				}
			}
			if(timeSinceRefresh>TimeSpan.Zero) {
				dateTimeWait=dateTimeWait.Add(timeSinceRefresh);
			}
			return dateTimeWait;
		}

		private static string GetString(DataRow dataRow,string columnName) {
			if(dataRow.Table==null || !dataRow.Table.Columns.Contains(columnName)) {
				return "";
			}
			return dataRow[columnName]?.ToString()??"";
		}
	}
}
