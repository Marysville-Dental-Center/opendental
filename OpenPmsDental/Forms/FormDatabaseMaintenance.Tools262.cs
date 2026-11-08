// Tools-tab entry of Database Maintenance that came with Open Dental 26.2 (package P262-TOOLS): Conversion Payment Dates (item 24, on demand only).
// FillGridTools adds the row. The 26.2 checks PaymentIncomeTransferIssues and InsVerifyRedundantEntries are ordinary methods of the Checks tab.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormDatabaseMaintenance {
		///<summary>Opens the Conversion Payment Dates window. Changing dates is replication-unsafe, so the same replication prompt as for the unsafe Fix
		///methods comes first, and Change Dates stays disabled when it is not passed.</summary>
		private void ConversionPaymentDatesFix() {
			if(_isUsingReplication || !_hasReplicationPermission) {
				VerifyPermissionToRunReplicationUnsafeMethods();
			}
			bool canFix=_hasReplicationPermission && (!_isUsingReplication || _isReplicationPasswordEntered);
			using FormDatabaseMaintConvPayDates formDatabaseMaintConvPayDates=new FormDatabaseMaintConvPayDates(canFix);
			formDatabaseMaintConvPayDates.ShowDialog();
			if(formDatabaseMaintConvPayDates.HasFixed) {
				SaveLogToFile(formDatabaseMaintConvPayDates.LogText);
			}
		}
	}
}
