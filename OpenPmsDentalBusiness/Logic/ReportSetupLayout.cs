using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>The list of the redesigned Display Settings and Security Permissions tabs of Report Setup (Open Dental 26.2 items 31 and 32, "Redesigned
	///layout of Report Display Settings" and "... of Report Security Permissions"). Before 26.2 each category had its own small grid; now one list
	///holds every report under a row per category, in the order of the Reports window. Every setting is kept: display name, favorite, hidden and the
	///order within the category on the one tab, allowed per user group on the other. The layout is this program's own design (docs/port-notes/
	///P262-REPORTS.md); the rules are the 26.1 rules, moved here so that they can be tested without a window.</summary>
	public class ReportSetupLayout {
		///<summary>The categories the two tabs list, in the order of the Reports window. Arizona Primary Care is not listed, as before 26.2.</summary>
		public static readonly DisplayReportCategory[] ArrayCategories=new DisplayReportCategory[] {
			DisplayReportCategory.ProdInc,
			DisplayReportCategory.Daily,
			DisplayReportCategory.Monthly,
			DisplayReportCategory.Lists,
			DisplayReportCategory.PublicHealth,
		};

		///<summary>The text of a category's row: the titles of the grids before 26.2 (translated by the window).</summary>
		public static string GetCategoryTitle(DisplayReportCategory category) {
			switch(category) {
				case DisplayReportCategory.ProdInc:
					return "Production & Income";
				case DisplayReportCategory.Daily:
					return "Daily";
				case DisplayReportCategory.Monthly:
					return "Monthly";
				case DisplayReportCategory.Lists:
					return "Lists";
				case DisplayReportCategory.PublicHealth:
					return "Public Health";
				case DisplayReportCategory.ArizonaPrimaryCare:
					return "Arizona Primary Care";
				default:
					return category.ToString();
			}
		}

		///<summary>One row of the list: a category row (DisplayReport null) or a report.</summary>
		public class Entry {
			public DisplayReportCategory Category;
			///<summary>Null on the row of a category.</summary>
			public DisplayReport DisplayReport;

			public bool IsCategory {
				get {
					return DisplayReport==null;
				}
			}
		}

		///<summary>The reports of one category in the order of the Reports window (ItemOrder; the key breaks a tie).</summary>
		public static List<DisplayReport> GetReportsInCategory(List<DisplayReport> listDisplayReports,DisplayReportCategory category) {
			return listDisplayReports.FindAll(x => x.Category==category)
				.OrderBy(x => x.ItemOrder)
				.ThenBy(x => x.DisplayReportNum)
				.ToList();
		}

		///<summary>The rows of the list: for each category of ArrayCategories, its row and then its reports, hidden ones included. A category without
		///reports still has its row.</summary>
		public static List<Entry> GetEntries(List<DisplayReport> listDisplayReports) {
			List<Entry> listEntries=new List<Entry>();
			foreach(DisplayReportCategory category in ArrayCategories) {
				listEntries.Add(new Entry { Category=category });
				foreach(DisplayReport displayReport in GetReportsInCategory(listDisplayReports,category)) {
					listEntries.Add(new Entry { Category=category,DisplayReport=displayReport });
				}
			}
			return listEntries;
		}

		///<summary>Moves the report one place up or down within its category and numbers the category's reports 0, 1, 2 ... in their new order (the
		///Reports window orders by ItemOrder within a category). False, and nothing changed, at the top or bottom of the category or for a report not
		///in the list.</summary>
		public static bool Move(List<DisplayReport> listDisplayReports,DisplayReport displayReport,bool isUp) {
			if(displayReport==null) {
				return false;
			}
			List<DisplayReport> listInCategory=GetReportsInCategory(listDisplayReports,displayReport.Category);
			int index=listInCategory.IndexOf(displayReport);
			if(index<0) {
				return false;
			}
			int indexOther=isUp ? index-1 : index+1;
			if(indexOther<0 || indexOther>=listInCategory.Count) {
				return false;
			}
			listInCategory[index]=listInCategory[indexOther];
			listInCategory[indexOther]=displayReport;
			for(int i=0;i<listInCategory.Count;i++) {
				listInCategory[i].ItemOrder=i;
			}
			return true;
		}

		///<summary>The Favorite cell: a favorite is never hidden (26.1 rule).</summary>
		public static void ToggleFavorite(DisplayReport displayReport) {
			displayReport.IsVisibleInSubMenu=!displayReport.IsVisibleInSubMenu;
			if(displayReport.IsVisibleInSubMenu) {
				displayReport.IsHidden=false;
			}
		}

		///<summary>The Hidden cell: a hidden report is never a favorite (26.1 rule).</summary>
		public static void ToggleHidden(DisplayReport displayReport) {
			displayReport.IsHidden=!displayReport.IsHidden;
			if(displayReport.IsHidden) {
				displayReport.IsVisibleInSubMenu=false;
			}
		}
	}
}
