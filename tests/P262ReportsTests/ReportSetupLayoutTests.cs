// The list of the redesigned Report Setup tabs (ReportSetupLayout) and the rules of the Security Permissions tab (ReportPermissionEdit), without a
// database or a window: the rows and their order, moving a report within its category, the Favorite and Hidden cells, and the Allowed cell, Set All
// and Set None with the "every report" permission (FKey 0). The 26.1 rules are the reference.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262ReportsTests {
	[TestClass]
	public class ReportSetupLayoutTests {
		private static DisplayReport Report(long num,DisplayReportCategory category,int itemOrder,string description="",bool isHidden=false) {
			return new DisplayReport { DisplayReportNum=num,Category=category,ItemOrder=itemOrder,InternalName="OD"+num,
				Description=description=="" ? "Report "+num : description,IsHidden=isHidden };
		}

		private static List<DisplayReport> GetReports() {
			return new List<DisplayReport> {
				Report(1,DisplayReportCategory.Daily,1),
				Report(2,DisplayReportCategory.ProdInc,0),
				Report(3,DisplayReportCategory.Daily,0),
				Report(4,DisplayReportCategory.Lists,0,isHidden:true),
				Report(5,DisplayReportCategory.ArizonaPrimaryCare,0),
				Report(6,DisplayReportCategory.Daily,2),
				Report(7,DisplayReportCategory.ProdInc,1),
				Report(8,DisplayReportCategory.PublicHealth,0),
			};
		}

		[TestMethod]
		public void Categories_AreTheFiveOfTheReportsWindowInOrder() {
			CollectionAssert.AreEqual(new[] { DisplayReportCategory.ProdInc,DisplayReportCategory.Daily,DisplayReportCategory.Monthly,
				DisplayReportCategory.Lists,DisplayReportCategory.PublicHealth },ReportSetupLayout.ArrayCategories);
			CollectionAssert.AreEqual(new[] { "Production & Income","Daily","Monthly","Lists","Public Health" },
				ReportSetupLayout.ArrayCategories.Select(x => ReportSetupLayout.GetCategoryTitle(x)).ToArray(),"The titles of the 26.1 grids.");
		}

		[TestMethod]
		public void Entries_ARowPerCategoryThenItsReportsInOrder() {
			List<ReportSetupLayout.Entry> listEntries=ReportSetupLayout.GetEntries(GetReports());
			string shape=string.Join(" ",listEntries.Select(x => x.IsCategory ? "["+x.Category+"]" : x.DisplayReport.DisplayReportNum.ToString()));
			//Monthly has no report and keeps its row; Arizona Primary Care is not listed (26.1 neither); the hidden report is listed.
			Assert.AreEqual("[ProdInc] 2 7 [Daily] 3 1 6 [Monthly] [Lists] 4 [PublicHealth] 8",shape);
			Assert.IsTrue(listEntries.Where(x => !x.IsCategory).All(x => x.Category==x.DisplayReport.Category));
		}

		[TestMethod]
		public void Entries_TieOfItemOrderKeepsTheKeyOrder() {
			List<DisplayReport> listReports=new List<DisplayReport> {
				Report(12,DisplayReportCategory.Daily,0),
				Report(11,DisplayReportCategory.Daily,0),
			};
			CollectionAssert.AreEqual(new long[] { 11,12 },
				ReportSetupLayout.GetReportsInCategory(listReports,DisplayReportCategory.Daily).Select(x => x.DisplayReportNum).ToArray());
		}

		[TestMethod]
		public void Move_SwapsWithinTheCategoryAndNumbersItFromZero() {
			List<DisplayReport> listReports=GetReports();
			listReports.Single(x => x.DisplayReportNum==6).ItemOrder=7;//A gap, as an old database may have.
			DisplayReport report6=listReports.Single(x => x.DisplayReportNum==6);
			Assert.IsTrue(ReportSetupLayout.Move(listReports,report6,isUp:true));
			CollectionAssert.AreEqual(new long[] { 3,6,1 },
				ReportSetupLayout.GetReportsInCategory(listReports,DisplayReportCategory.Daily).Select(x => x.DisplayReportNum).ToArray());
			CollectionAssert.AreEqual(new[] { 0,1,2 },
				ReportSetupLayout.GetReportsInCategory(listReports,DisplayReportCategory.Daily).Select(x => x.ItemOrder).ToArray());
			Assert.AreEqual(0,listReports.Single(x => x.DisplayReportNum==2).ItemOrder,"Other categories are not touched.");
			Assert.AreEqual(1,listReports.Single(x => x.DisplayReportNum==7).ItemOrder);
			Assert.IsTrue(ReportSetupLayout.Move(listReports,report6,isUp:true));
			Assert.IsFalse(ReportSetupLayout.Move(listReports,report6,isUp:true),"Already the first of its category.");
			CollectionAssert.AreEqual(new long[] { 6,3,1 },
				ReportSetupLayout.GetReportsInCategory(listReports,DisplayReportCategory.Daily).Select(x => x.DisplayReportNum).ToArray());
			DisplayReport report1=listReports.Single(x => x.DisplayReportNum==1);
			Assert.IsFalse(ReportSetupLayout.Move(listReports,report1,isUp:false),"Already the last of its category.");
			Assert.IsTrue(ReportSetupLayout.Move(listReports,report1,isUp:true));
			CollectionAssert.AreEqual(new long[] { 6,1,3 },
				ReportSetupLayout.GetReportsInCategory(listReports,DisplayReportCategory.Daily).Select(x => x.DisplayReportNum).ToArray());
			Assert.IsFalse(ReportSetupLayout.Move(listReports,Report(99,DisplayReportCategory.Daily,0),isUp:true),"Not in the list.");
			Assert.IsFalse(ReportSetupLayout.Move(listReports,null,isUp:true));
		}

		[TestMethod]
		public void FavoriteAndHidden_ExcludeEachOther() {
			DisplayReport report=Report(1,DisplayReportCategory.Daily,0,isHidden:true);
			ReportSetupLayout.ToggleFavorite(report);
			Assert.IsTrue(report.IsVisibleInSubMenu);
			Assert.IsFalse(report.IsHidden,"A favorite is never hidden.");
			ReportSetupLayout.ToggleHidden(report);
			Assert.IsTrue(report.IsHidden);
			Assert.IsFalse(report.IsVisibleInSubMenu,"A hidden report is never a favorite.");
			ReportSetupLayout.ToggleHidden(report);
			Assert.IsFalse(report.IsHidden);
			Assert.IsFalse(report.IsVisibleInSubMenu);
			ReportSetupLayout.ToggleFavorite(report);
			ReportSetupLayout.ToggleFavorite(report);
			Assert.IsFalse(report.IsVisibleInSubMenu);
			Assert.IsFalse(report.IsHidden);
		}

		#region Security Permissions
		private const long GROUP=10;
		private const long GROUP_OTHER=20;

		private static GroupPermission Perm(long userGroupNum,long fKey) {
			return new GroupPermission { UserGroupNum=userGroupNum,PermType=EnumPermType.Reports,FKey=fKey,NewerDate=DateTime.MinValue };
		}

		private static string Keys(List<GroupPermission> listPerms,long userGroupNum) {
			return string.Join(",",listPerms.FindAll(x => x.UserGroupNum==userGroupNum).Select(x => x.FKey).OrderBy(x => x));
		}

		[TestMethod]
		public void SetAllAndSetNone_TouchOnlyTheGroup() {
			List<GroupPermission> listPerms=new List<GroupPermission> { Perm(GROUP,1),Perm(GROUP,3),Perm(GROUP_OTHER,1) };
			ReportPermissionEdit.SetAll(listPerms,GROUP);
			Assert.AreEqual("0",Keys(listPerms,GROUP));
			ReportPermissionEdit.SetAll(listPerms,GROUP);
			Assert.AreEqual("0",Keys(listPerms,GROUP),"Once.");
			Assert.IsTrue(ReportPermissionEdit.HasAll(listPerms,GROUP));
			Assert.IsTrue(GetReports().All(x => ReportPermissionEdit.IsAllowed(listPerms,GROUP,x.DisplayReportNum)));
			ReportPermissionEdit.SetNone(listPerms,GROUP);
			Assert.AreEqual("",Keys(listPerms,GROUP));
			Assert.AreEqual("1",Keys(listPerms,GROUP_OTHER));
			Assert.IsFalse(ReportPermissionEdit.IsAllowed(listPerms,GROUP,1));
			Assert.IsTrue(ReportPermissionEdit.IsAllowed(listPerms,GROUP_OTHER,1));
		}

		[TestMethod]
		public void Toggle_AllowsAndDeniesOneReport() {
			List<DisplayReport> listReports=GetReports();
			List<GroupPermission> listPerms=new List<GroupPermission>();
			DisplayReport report3=listReports.Single(x => x.DisplayReportNum==3);
			ReportPermissionEdit.Toggle(listPerms,GROUP,report3,listReports,isArizonaPrimaryCareUsed:false);
			Assert.AreEqual("3",Keys(listPerms,GROUP));
			Assert.AreEqual(1,ReportPermissionEdit.CountAllowed(listPerms,GROUP,listReports));
			ReportPermissionEdit.Toggle(listPerms,GROUP,report3,listReports,isArizonaPrimaryCareUsed:false);
			Assert.AreEqual("",Keys(listPerms,GROUP));
		}

		[TestMethod]
		public void Toggle_DenyingOneOfAllListsEveryOtherReport() {
			List<DisplayReport> listReports=GetReports();
			List<GroupPermission> listPerms=new List<GroupPermission> { Perm(GROUP,0),Perm(GROUP_OTHER,0) };
			ReportPermissionEdit.Toggle(listPerms,GROUP,listReports.Single(x => x.DisplayReportNum==3),listReports,isArizonaPrimaryCareUsed:false);
			//Every report but 3, and not the Arizona Primary Care report (5) while the office does not use it; hidden report 4 included.
			Assert.AreEqual("1,2,4,6,7,8",Keys(listPerms,GROUP));
			Assert.AreEqual("0",Keys(listPerms,GROUP_OTHER));
			listPerms=new List<GroupPermission> { Perm(GROUP,0) };
			ReportPermissionEdit.Toggle(listPerms,GROUP,listReports.Single(x => x.DisplayReportNum==3),listReports,isArizonaPrimaryCareUsed:true);
			Assert.AreEqual("1,2,4,5,6,7,8",Keys(listPerms,GROUP),"Arizona Primary Care counts when the office uses it.");
		}

		[TestMethod]
		public void Toggle_AllowingTheLastMissingReportBecomesAll() {
			List<DisplayReport> listReports=GetReports();
			List<GroupPermission> listPerms=new List<GroupPermission> { Perm(GROUP,0) };
			DisplayReport report3=listReports.Single(x => x.DisplayReportNum==3);
			ReportPermissionEdit.Toggle(listPerms,GROUP,report3,listReports,isArizonaPrimaryCareUsed:false);
			ReportPermissionEdit.Toggle(listPerms,GROUP,report3,listReports,isArizonaPrimaryCareUsed:false);
			Assert.AreEqual("0",Keys(listPerms,GROUP),"Back to every report.");
			//With Arizona Primary Care in use, the last missing report may be the Arizona one.
			listPerms=listReports.FindAll(x => x.DisplayReportNum!=5).Select(x => Perm(GROUP,x.DisplayReportNum)).ToList();
			ReportPermissionEdit.Toggle(listPerms,GROUP,listReports.Single(x => x.DisplayReportNum==8),listReports,isArizonaPrimaryCareUsed:true);
			Assert.AreEqual("1,2,3,4,6,7",Keys(listPerms,GROUP),"Denied 8.");
			ReportPermissionEdit.Toggle(listPerms,GROUP,listReports.Single(x => x.DisplayReportNum==8),listReports,isArizonaPrimaryCareUsed:true);
			Assert.AreEqual("1,2,3,4,6,7,8",Keys(listPerms,GROUP),"Still not every report: 5 is missing.");
			ReportPermissionEdit.Toggle(listPerms,GROUP,listReports.Single(x => x.DisplayReportNum==5),listReports,isArizonaPrimaryCareUsed:true);
			Assert.AreEqual("0",Keys(listPerms,GROUP));
		}

		[TestMethod]
		public void Toggle_AStalePermissionDoesNotCountAsAReport() {
			//26.1 compared counts, so a permission of a report no longer in the list could stand in for a missing one.
			List<DisplayReport> listReports=new List<DisplayReport> { Report(1,DisplayReportCategory.Daily,0),Report(2,DisplayReportCategory.Daily,1) };
			List<GroupPermission> listPerms=new List<GroupPermission> { Perm(GROUP,77) };
			ReportPermissionEdit.Toggle(listPerms,GROUP,listReports[0],listReports,isArizonaPrimaryCareUsed:false);
			Assert.AreEqual("1,77",Keys(listPerms,GROUP));
			Assert.IsFalse(ReportPermissionEdit.IsAllowed(listPerms,GROUP,2));
		}
		#endregion Security Permissions
	}
}
