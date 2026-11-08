using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>The Allowed cell, Set All and Set None of the Security Permissions tab of Report Setup, on the window's working list of Reports
	///permissions (GroupPermissions.GetPermsForReports; saved with GroupPermissions.Sync). The 26.1 rules, moved out of the window with the 26.2
	///redesign (ReportSetupLayout): a group with a Reports permission whose FKey is 0 may run every report; otherwise one permission per allowed
	///report, FKey the DisplayReportNum. Arizona Primary Care reports count only when the office uses them.</summary>
	public class ReportPermissionEdit {
		///<summary>True when the group may run the report (FKey 0, or the report's own permission).</summary>
		public static bool IsAllowed(List<GroupPermission> listGroupPermissions,long userGroupNum,long displayReportNum) {
			return GroupPermissions.HasPermission(userGroupNum,EnumPermType.Reports,displayReportNum,listGroupPermissions:listGroupPermissions);
		}

		///<summary>True when the group has the permission for every report (FKey 0).</summary>
		public static bool HasAll(List<GroupPermission> listGroupPermissions,long userGroupNum) {
			return listGroupPermissions.Any(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.Reports && x.FKey==0);
		}

		///<summary>Set All: the group's report permissions become the one with FKey 0.</summary>
		public static void SetAll(List<GroupPermission> listGroupPermissions,long userGroupNum) {
			listGroupPermissions.RemoveAll(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.Reports && x.FKey!=0);
			if(!HasAll(listGroupPermissions,userGroupNum)) {
				listGroupPermissions.Add(CreatePermission(userGroupNum,0));
			}
		}

		///<summary>Set None: the group loses every report permission.</summary>
		public static void SetNone(List<GroupPermission> listGroupPermissions,long userGroupNum) {
			listGroupPermissions.RemoveAll(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.Reports);
		}

		///<summary>A click in the Allowed cell. Denying one report of a group that has every report replaces the FKey 0 permission with one per other
		///report (Arizona Primary Care reports only when the office uses them). Allowing the last report a group lacks turns its permissions into the one
		///with FKey 0. listDisplayReports is the window's list of reports.</summary>
		public static void Toggle(List<GroupPermission> listGroupPermissions,long userGroupNum,DisplayReport displayReport,
			List<DisplayReport> listDisplayReports,bool isArizonaPrimaryCareUsed)
		{
			if(IsAllowed(listGroupPermissions,userGroupNum,displayReport.DisplayReportNum)) {
				listGroupPermissions.RemoveAll(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.Reports && x.FKey==displayReport.DisplayReportNum);
				if(!HasAll(listGroupPermissions,userGroupNum)) {
					return;
				}
				SetNone(listGroupPermissions,userGroupNum);
				foreach(DisplayReport displayReportOther in GetCountedReports(listDisplayReports,isArizonaPrimaryCareUsed)) {
					if(displayReportOther.DisplayReportNum==displayReport.DisplayReportNum) {
						continue;
					}
					listGroupPermissions.Add(CreatePermission(userGroupNum,displayReportOther.DisplayReportNum));
				}
				return;
			}
			listGroupPermissions.Add(CreatePermission(userGroupNum,displayReport.DisplayReportNum));
			List<long> listAllowedNums=listGroupPermissions
				.FindAll(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.Reports && x.FKey!=0)
				.Select(x => x.FKey)
				.ToList();
			if(GetCountedReports(listDisplayReports,isArizonaPrimaryCareUsed).All(x => listAllowedNums.Contains(x.DisplayReportNum))) {
				SetAll(listGroupPermissions,userGroupNum);
			}
		}

		///<summary>How many of the given reports the group may run (the tab's count of allowed reports).</summary>
		public static int CountAllowed(List<GroupPermission> listGroupPermissions,long userGroupNum,List<DisplayReport> listDisplayReports) {
			return listDisplayReports.Count(x => IsAllowed(listGroupPermissions,userGroupNum,x.DisplayReportNum));
		}

		///<summary>The reports that "every report" means: all of the list, without Arizona Primary Care when the office does not use it.</summary>
		private static List<DisplayReport> GetCountedReports(List<DisplayReport> listDisplayReports,bool isArizonaPrimaryCareUsed) {
			if(isArizonaPrimaryCareUsed) {
				return listDisplayReports;
			}
			return listDisplayReports.FindAll(x => x.Category!=DisplayReportCategory.ArizonaPrimaryCare);
		}

		private static GroupPermission CreatePermission(long userGroupNum,long fKey) {
			GroupPermission groupPermission=new GroupPermission();
			groupPermission.NewerDate=DateTime.MinValue;
			groupPermission.NewerDays=0;
			groupPermission.PermType=EnumPermType.Reports;
			groupPermission.UserGroupNum=userGroupNum;
			groupPermission.FKey=fKey;
			return groupPermission;
		}
	}
}
