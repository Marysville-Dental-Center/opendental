// Inventory tracking (docs/specs/inventory-tracking.md): a manual stock adjustment with a reason.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Adds to or takes from the stock of one supply (and lot), with a required reason. Writes an Adjust ledger row and an Inventory Adjust
	///audit entry. The caller checks Inventory Adjust.</summary>
	public partial class FormSupplyAdjust:FormODBase {
		///<summary>Set before showing.</summary>
		public long SupplyNum;
		///<summary>The lot selected when the window opens; 0 for stock without a lot.</summary>
		public long SupplyLotNumSelected;

		public FormSupplyAdjust() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyAdjust_Load(object sender,EventArgs e) {
			Supply supply=Supplies.GetSupply(SupplyNum);
			textSupply.Text=supply?.Descript??"";
			comboLot.Items.Add(Lan.g(this,"(no lot)"),new SupplyLot { SupplyLotNum=0 });
			foreach(SupplyLot supplyLot in SupplyLots.GetForSupply(SupplyNum,isEmptyIncluded:true)) {
				comboLot.Items.Add(supplyLot.LotNumber+(supplyLot.HasExpiry() ? ", "+Lan.g(this,"exp")+" "+supplyLot.DateExpiry.ToShortDateString() : "")
					+" ("+InventoryTracking.FormatQty(supplyLot.QtyOnHand)+")",supplyLot);
			}
			comboLot.SetSelectedKey<SupplyLot>(SupplyLotNumSelected,x => x.SupplyLotNum);
			if(comboLot.SelectedIndex==-1) {
				comboLot.SelectedIndex=0;
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textChange.IsValid() || textChange.Text.Trim()=="" || Math.Round(textChange.Value,2)==0) {
				MsgBox.Show(this,"Please enter the quantity to add (or, with a minus sign, to take away).");
				return;
			}
			if(textReason.Text.Trim()=="") {
				MsgBox.Show(this,"Please enter a reason.");
				return;
			}
			try {
				InventoryTracking.Adjust(SupplyNum,comboLot.GetSelected<SupplyLot>()?.SupplyLotNum??0,textChange.Value,textReason.Text);
			}
			catch(Exception ex) {
				//The adjustment may have been saved (the answer was lost). Close instead of offering Save again, which could adjust twice; the stock
				//window reads the ledger again.
				MessageBox.Show(this,ex.Message+"\r\n\r\n"+Lan.g(this,"The adjustment may have been saved. Check the supply's ledger in Stock before adjusting again."));
				DialogResult=DialogResult.OK;
				return;
			}
			DialogResult=DialogResult.OK;
		}
	}
}
