// Inventory tracking (docs/specs/inventory-tracking.md): turning tracking on or off, and the lead days of the expiring alert.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Inventory, Inventory, Setup. Needs Setup.</summary>
	public partial class FormInventorySetup:FormODBase {
		public FormInventorySetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormInventorySetup_Load(object sender,EventArgs e) {
			checkEnabled.Checked=InventoryTracking.IsEnabled();
			textExpiryDays.Text=InventoryTracking.GetExpiryAlertDays().ToString();
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textExpiryDays.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			bool wasEnabled=InventoryTracking.IsEnabled();
			bool isChanged=Prefs.UpdateBool(PrefName.InventoryTrackingEnabled,checkEnabled.Checked);
			isChanged|=Prefs.UpdateInt(PrefName.InventoryExpiryAlertDays,PIn.Int(textExpiryDays.Text));
			if(isChanged) {
				DataValid.SetInvalid(InvalidType.Prefs);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Inventory tracking "+(checkEnabled.Checked ? "on" : "off")+", expiring alert "
					+PIn.Int(textExpiryDays.Text)+" days.");
			}
			if(wasEnabled && !checkEnabled.Checked) {
				InventoryTracking.DeleteAllAlerts();
			}
			else if(checkEnabled.Checked) {
				InventoryTracking.RefreshAllAlerts();
			}
			DialogResult=DialogResult.OK;
		}
	}
}
