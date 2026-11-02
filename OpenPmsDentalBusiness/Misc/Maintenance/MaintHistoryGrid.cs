// Routine maintenance (docs/specs/routine-maintenance.md, "History (FormMaintHistory)"): the columns of the history window's grid. No database
// access.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenPmsDental.UI;

namespace OpenPmsDentalBusiness {
	///<summary>The columns of the history window's grid (FormMaintHistory), here so that a test lays them out in the real grid at the window's
	///default size. Results is the only column that takes the width the others leave, and a printout has the on-screen width
	///(GridOD.PrintPage), so the fixed columns of All History add up to 900 of the grid's 1076: Results keeps about 157 after the vertical
	///scrollbar, on screen and printed. One item's history has no Item column, so its Results is 150 wider.</summary>
	public static class MaintHistoryGrid {
		///<summary>The narrowest Results may be in either view at the window's default size, on screen or printed.</summary>
		public const int WIDTH_RESULTS_MIN=150;

		///<summary>The grid's columns in order. isAllItems adds the Item column (All History). translationName is the grid's TranslationName.
		///Checklist, Results, Note and Corrected wrap onto more lines when their text is longer than the column.</summary>
		public static List<GridColumn> GetColumns(string translationName,bool isAllItems) {
			List<GridColumn> listGridColumns=new List<GridColumn>();
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Date Done"),70));
			if(isAllItems) {
				listGridColumns.Add(new GridColumn(Lans.g(translationName,"Item"),150));
			}
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Done By"),90));
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Due"),70));
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Late"),40,HorizontalAlignment.Right));
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Flagged"),50,HorizontalAlignment.Center));
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Checklist"),100));
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Results"),200){ IsWidthDynamic=true });
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Note"),110));
			//Date and time without seconds, so that it fits on one line.
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Recorded"),120));
			//The date and who corrected it (MaintCorrections.GetCorrectedText); the time is in the audit trail.
			listGridColumns.Add(new GridColumn(Lans.g(translationName,"Corrected"),100));
			return listGridColumns;
		}

		///<summary>The Recorded column: when the completion was entered (SecDateTEntry), as the date and the time without seconds in the user's
		///culture, for example "11/2/2026 10:15 AM". "" when it is not known.</summary>
		public static string GetRecordedText(DateTime dateTEntry) {
			if(dateTEntry.Year<=1880) {
				return "";
			}
			return dateTEntry.ToShortDateString()+" "+dateTEntry.ToShortTimeString();
		}
	}
}
