// The PreAuthFlag appointment element and bubble field (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER in
// docs/specs/port-backlog.md): the appointment data table gets the flag, the codes and the carrier's appointment color. Called once at the end of
// GetPeriodApptsTable, so the shared Appointments.cs only gains that call.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;

namespace OpenPmsDentalBusiness {
	public partial class Appointments {
		///<summary>"PreAuth" (translated) when the appointment holds a TP or TPi procedure that still needs a preauthorization, otherwise empty.</summary>
		public const string PreAuthFlagColumn="preAuthFlag";
		///<summary>The codes of those procedures, comma separated, for the bubble.</summary>
		public const string PreAuthCodesColumn="preAuthCodes";
		///<summary>The ARGB appointment text back color (carrier.ApptTextBackColor) of the first carrier that wants the preauthorization, when that
		///carrier has a color (black means none). Otherwise empty.</summary>
		public const string PreAuthColorColumn="preAuthColor";

		///<summary>Adds the three preauthorization columns to the appointment data table and fills them. Runs no query unless a carrier has a
		///preauthorization list. isPlanned: the rows are planned appointments, so procedures are found by PlannedAptNum.</summary>
		private static void FillPreAuthFlags(DataTable table,bool isPlanned) {
			Meth.NoCheckMiddleTierRole();
			foreach(string columnName in new[] { PreAuthFlagColumn,PreAuthCodesColumn,PreAuthColorColumn }) {
				if(!table.Columns.Contains(columnName)) {
					table.Columns.Add(columnName);
				}
			}
			for(int i=0;i<table.Rows.Count;i++) {
				table.Rows[i][PreAuthFlagColumn]="";
				table.Rows[i][PreAuthCodesColumn]="";
				table.Rows[i][PreAuthColorColumn]="";
			}
			if(table.Rows.Count==0 || !table.Columns.Contains("AptNum") || !CarrierPreAuths.IsInUse()) {
				return;
			}
			List<long> listAptNums=table.Rows.Cast<DataRow>().Select(x => PIn.Long(x["AptNum"].ToString())).ToList();
			List<PreAuthNeed> listPreAuthNeeds=CarrierPreAuths.GetNeedsForAppointments(listAptNums,isPlanned);
			if(listPreAuthNeeds.Count==0) {
				return;
			}
			for(int i=0;i<table.Rows.Count;i++) {
				long aptNum=PIn.Long(table.Rows[i]["AptNum"].ToString());
				List<PreAuthNeed> listPreAuthNeedsApt=listPreAuthNeeds
					.FindAll(x => (isPlanned ? x.PlannedAptNum : x.AptNum)==aptNum)
					.OrderBy(x => x.Ordinal)
					.ToList();
				if(listPreAuthNeedsApt.Count==0) {
					continue;
				}
				table.Rows[i][PreAuthFlagColumn]=Lans.g("Appointments","PreAuth");
				table.Rows[i][PreAuthCodesColumn]=string.Join(",",listPreAuthNeedsApt.Select(x => x.ProcCode).Distinct(StringComparer.OrdinalIgnoreCase));
				for(int n=0;n<listPreAuthNeedsApt.Count;n++) {
					Color color=Carriers.GetCarrier(listPreAuthNeedsApt[n].CarrierNum).ApptTextBackColor;
					if(IsCarrierColorSet(color)) {
						table.Rows[i][PreAuthColorColumn]=color.ToArgb().ToString();
						break;
					}
				}
			}
		}

		///<summary>Black (with any alpha) and empty mean "no color" for carrier.ApptTextBackColor, as the Insurance Color element treats them.</summary>
		public static bool IsCarrierColorSet(Color color) {
			Meth.NoCheckMiddleTierRole();
			if(color.IsEmpty || color.A==0) {
				return false;
			}
			return !(color.R==0 && color.G==0 && color.B==0);
		}
	}
}
