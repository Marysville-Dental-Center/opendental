using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	interface IDashWidgetField {
		///<summary>Directly sets data required for display, rather than querying the database for this information.</summary>
		void SetData(PatientDashboardDataEventArgs data,SheetField sheetField);

		///<summary>Refreshes all the data required for display.  Must be implemented to be able to run on a thread.</summary>
		void RefreshData(Patient pat,SheetField sheetField);

		///<summary>Refreshes the view.  Must be implemented in a way to safely invoke back to the UI thread.</summary>
		void RefreshView();

		void PassLayoutManager(LayoutManagerForms layoutManager);
	}
}
