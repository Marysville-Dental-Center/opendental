// Routine maintenance: the history window's grid at the window's default size, on screen and printed (docs/specs/routine-maintenance.md,
// "History (FormMaintHistory)"). The real grid (OpenPmsDental.UI.GridOD) lays out MaintHistoryGrid's columns and prints them onto a bitmap the
// size of a landscape letter page; no window is shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class HistoryGridTests {
		private const string TRANSLATION_NAME="TableMaintHistory";
		///<summary>A landscape letter page in hundredths of an inch, the unit of a printer's Graphics.</summary>
		private static readonly Size _sizePage=new Size(1100,850);
		///<summary>Its margin bounds with PrinterL's default margins (left and right 25, top and bottom 40).</summary>
		private static readonly Rectangle _rectangleMargins=new Rectangle(25,40,1050,770);
		///<summary>Where the grid starts below the heading the window prints (FormMaintHistory.pd_PrintPage).</summary>
		private const int Y_GRID_PRINTED=100;

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		#region Helpers
		///<summary>gridMain's size in FormMaintHistory.Designer.cs: the grid's size at the window's default size.</summary>
		private static Size GridSizeDefault() {
			string designer=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormMaintHistory.Designer.cs");
			Match match=Regex.Match(designer,@"this\.gridMain\.Size = new System\.Drawing\.Size\((\d+), (\d+)\);");
			Assert.IsTrue(match.Success,"gridMain's size was not found in the designer file.");
			return new Size(int.Parse(match.Groups[1].Value),int.Parse(match.Groups[2].Value));
		}

		///<summary>The history grid as the window fills it at its default size, with countRows rows of empty cells. The grid leaves room for the
		///vertical scrollbar whether the rows fit or not.</summary>
		private static GridOD MakeGrid(bool isAllItems,int countRows) {
			GridOD gridOD=new GridOD();
			gridOD.TranslationName=TRANSLATION_NAME;
			gridOD.Size=GridSizeDefault();
			gridOD.BeginUpdate();
			gridOD.Columns.Clear();
			foreach(GridColumn gridColumn in MaintHistoryGrid.GetColumns(gridOD.TranslationName,isAllItems)) {
				gridOD.Columns.Add(gridColumn);
			}
			gridOD.ListGridRows.Clear();
			for(int i=0;i<countRows;i++) {
				GridRow gridRow=new GridRow();
				for(int c=0;c<gridOD.Columns.Count;c++) {
					gridRow.Cells.Add("");
				}
				gridOD.ListGridRows.Add(gridRow);
			}
			gridOD.EndUpdate();
			return gridOD;
		}

		private static int IndexOfColumn(GridOD gridOD,string heading) {
			for(int i=0;i<gridOD.Columns.Count;i++) {
				if(gridOD.Columns[i].Heading==heading) {
					return i;
				}
			}
			Assert.Fail("No column "+heading);
			return -1;
		}

		private static List<int> ColumnWidths(GridOD gridOD) {
			List<int> listWidths=new List<int>();
			for(int i=0;i<gridOD.Columns.Count;i++) {
				listWidths.Add(gridOD.Columns[i].State.Width);
			}
			return listWidths;
		}

		///<summary>The x of each vertical line of the printed grid (its left edge, then the right edge of each column), read from the first pixel row
		///below the column headings that has only those lines: the cells are empty, so nothing else is drawn there.</summary>
		private static List<int> PrintedColumnLines(Bitmap bitmap,int countColumns) {
			for(int y=Y_GRID_PRINTED;y<bitmap.Height;y++) {
				List<int> listX=new List<int>();
				for(int x=0;x<bitmap.Width;x++) {
					Color color=bitmap.GetPixel(x,y);
					if(color.R<250 || color.G<250 || color.B<250) {
						listX.Add(x);
					}
				}
				if(listX.Count==countColumns+1) {
					return listX;
				}
			}
			Assert.Fail("The printed grid's column lines were not found.");
			return null;
		}
		#endregion Helpers

		[TestMethod]
		public void Columns_AllHistoryHasTheItem_OneItemDoesNot_AndResultsAloneTakesTheRest() {
			List<GridColumn> listAll=MaintHistoryGrid.GetColumns(TRANSLATION_NAME,isAllItems:true);
			CollectionAssert.AreEqual(new[] { "Date Done","Item","Done By","Due","Late","Flagged","Checklist","Results","Note","Recorded","Corrected" },
				listAll.Select(x => x.Heading).ToList());
			List<GridColumn> listOne=MaintHistoryGrid.GetColumns(TRANSLATION_NAME,isAllItems:false);
			CollectionAssert.AreEqual(listAll.Where(x => x.Heading!="Item").Select(x => x.Heading).ToList(),listOne.Select(x => x.Heading).ToList());
			CollectionAssert.AreEqual(new[] { "Results" },listAll.Where(x => x.IsWidthDynamic).Select(x => x.Heading).ToList());
			CollectionAssert.AreEqual(new[] { "Results" },listOne.Where(x => x.IsWidthDynamic).Select(x => x.Heading).ToList());
		}

		[TestMethod]
		public void Results_KeepsItsWidthOnScreen_InAllHistoryAndInOneItem() {
			Size sizeGrid=GridSizeDefault();
			foreach(bool isAllItems in new[] { true,false }) {
				//No rows, rows that fit, and more rows than fit (the vertical scrollbar shows).
				foreach(int countRows in new[] { 0,3,60 }) {
					using GridOD gridOD=MakeGrid(isAllItems,countRows);
					string view=(isAllItems ? "All History" : "One item")+", "+countRows+" rows";
					int widthResults=gridOD.Columns[IndexOfColumn(gridOD,"Results")].State.Width;
					Assert.IsTrue(widthResults>=MaintHistoryGrid.WIDTH_RESULTS_MIN,view+": Results is "+widthResults+" wide.");
					List<int> listWidths=ColumnWidths(gridOD);
					Assert.IsTrue(listWidths.All(x => x>0),view+": every column has a width.");
					//Every column is inside the grid, which has no horizontal scrollbar: nothing is cut off at the right.
					Assert.IsTrue(gridOD.Columns[gridOD.Columns.Count-1].State.Right<=sizeGrid.Width-2,view+": the columns are wider than the grid.");
				}
			}
			using GridOD gridAll=MakeGrid(isAllItems:true,countRows:60);
			using GridOD gridOne=MakeGrid(isAllItems:false,countRows:60);
			int widthItem=gridAll.Columns[IndexOfColumn(gridAll,"Item")].State.Width;
			Assert.AreEqual(gridAll.Columns[IndexOfColumn(gridAll,"Results")].State.Width+widthItem,gridOne.Columns[IndexOfColumn(gridOne,"Results")].State.Width,
				"One item's history gives Results the Item column's width.");
			//The other columns keep the widths they were given.
			for(int i=0;i<gridAll.Columns.Count;i++) {
				if(gridAll.Columns[i].IsWidthDynamic) {
					continue;
				}
				Assert.AreEqual(gridAll.Columns[i].ColWidth,gridAll.Columns[i].State.Width,gridAll.Columns[i].Heading);
			}
		}

		[TestMethod]
		public void Results_KeepsItsWidthInThePrintout_AndTheGridIsOnThePage() {
			foreach(bool isAllItems in new[] { true,false }) {
				string view=isAllItems ? "All History" : "One item";
				using GridOD gridOD=MakeGrid(isAllItems,countRows:20);
				List<int> listWidthsScreen=ColumnWidths(gridOD);
				using Bitmap bitmap=new Bitmap(_sizePage.Width,_sizePage.Height);
				bitmap.SetResolution(100,100);//A printer's Graphics draws in hundredths of an inch, and the font's points are sized for that.
				using(Graphics g=Graphics.FromImage(bitmap)) {
					g.Clear(Color.White);
					int yEnd=gridOD.PrintPage(g,0,_rectangleMargins,Y_GRID_PRINTED);
					Assert.AreNotEqual(-1,yEnd,view+": twenty rows fit on one page.");
				}
				List<int> listLines=PrintedColumnLines(bitmap,gridOD.Columns.Count);
				List<int> listWidthsPrinted=new List<int>();
				for(int i=1;i<listLines.Count;i++) {
					listWidthsPrinted.Add(listLines[i]-listLines[i-1]);
				}
				//The printout has the on-screen widths (GridOD.PrintPage), so Results keeps its width on paper too.
				CollectionAssert.AreEqual(listWidthsScreen,listWidthsPrinted,view+": printed "+string.Join(",",listWidthsPrinted));
				int widthResultsPrinted=listWidthsPrinted[IndexOfColumn(gridOD,"Results")];
				Assert.IsTrue(widthResultsPrinted>=MaintHistoryGrid.WIDTH_RESULTS_MIN,view+": Results is printed "+widthResultsPrinted+" wide.");
				Assert.IsTrue(listLines[0]>=_rectangleMargins.Left,view+": the grid starts inside the left margin.");
				Assert.IsTrue(listLines[listLines.Count-1]<_sizePage.Width,view+": the grid ends on the page, at "+listLines[listLines.Count-1]+".");
			}
		}

		[TestMethod]
		public void RecordedText_IsTheDateAndTheTimeWithoutSeconds() {
			Assert.AreEqual("11/2/2026 10:15 AM",MaintHistoryGrid.GetRecordedText(new DateTime(2026,11,2,10,15,32)));
			Assert.AreEqual("12/31/2026 4:05 PM",MaintHistoryGrid.GetRecordedText(new DateTime(2026,12,31,16,5,0)));
			Assert.AreEqual("",MaintHistoryGrid.GetRecordedText(DateTime.MinValue),"Not known.");
		}
	}
}
