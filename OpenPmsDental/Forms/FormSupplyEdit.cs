using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormSupplyEdit:FormODBase {
		public Supply SupplyCur;
		public List<Supplier> ListSuppliers;
		///<summary>Inventory tracking settings of the supply (the defaults when it has no row).</summary>
		private SupplySetting _supplySetting;
		//private bool isHiddenInitialVal;
		//private long categoryInitialVal;
		//private Supply SuppOriginal;
		//private List<Def> _listSupplyCatDefs;

		public FormSupplyEdit() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyEdit_Load(object sender,EventArgs e) {
			textSupplier.Text=Suppliers.GetName(ListSuppliers,SupplyCur.SupplierNum);
			comboCategory.Items.AddDefs(Defs.GetDefsForCategory(DefCat.SupplyCats,true));
			comboCategory.SetSelectedDefNum(SupplyCur.Category); 
			textCatalogNumber.Text=SupplyCur.CatalogNumber;
			textDescript.Text=SupplyCur.Descript;
			if(SupplyCur.LevelDesired!=0){
				textLevelDesired.Text=SupplyCur.LevelDesired.ToString();
			}
			if(SupplyCur.OrderQty!=0){
				textOrderQty.Text=SupplyCur.OrderQty.ToString();
			}
			if(SupplyCur.Price!=0){
				textPrice.Text=SupplyCur.Price.ToString("n");
			}
			if(SupplyCur.LevelOnHand!=0) {
				textOnHand.Text=InventoryTracking.FormatQty(SupplyCur.LevelOnHand);
			}
			checkIsHidden.Checked=SupplyCur.IsHidden;
			//Inventory tracking (docs/specs/inventory-tracking.md).
			textBarcode.Text=SupplyCur.BarCodeOrID;
			_supplySetting=SupplyCur.IsNew ? SupplySettings.CreateDefault(0) : SupplySettings.GetForSupply(SupplyCur.SupplyNum);
			if(_supplySetting.ReorderLevel!=0) {
				textReorderLevel.Text=InventoryTracking.FormatQty(_supplySetting.ReorderLevel);
			}
			checkLotTracked.Checked=_supplySetting.IsLotTracked;
			checkImplant.Checked=_supplySetting.IsImplant;
			checkLotTracked.Enabled=!checkImplant.Checked;
			butStock.Enabled=!SupplyCur.IsNew;
			if(InventoryTracking.IsEnabled() && !SupplyCur.IsNew) {
				//While tracking is on, stock changes only through receiving, use, counts and adjustments.
				textOnHand.ReadOnly=true;
				labelOnHandNote.Visible=true;
			}
		}

		private void checkImplant_Click(object sender,EventArgs e) {
			if(checkImplant.Checked) {
				checkLotTracked.Checked=true;
			}
			checkLotTracked.Enabled=!checkImplant.Checked;
		}

		private void butStock_Click(object sender,EventArgs e) {
			using FormSupplyStock formSupplyStock=new FormSupplyStock();
			formSupplyStock.SupplyNum=SupplyCur.SupplyNum;
			formSupplyStock.ShowDialog();
			Supply supplyDb=Supplies.GetSupply(SupplyCur.SupplyNum);
			if(supplyDb!=null) {
				SupplyCur.LevelOnHand=supplyDb.LevelOnHand;
				textOnHand.Text=supplyDb.LevelOnHand==0 ? "" : InventoryTracking.FormatQty(supplyDb.LevelOnHand);
			}
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(SupplyCur.IsNew){
				DialogResult=DialogResult.Cancel;
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete?")){
				return;
			}
			try{
				Supplies.DeleteObject(SupplyCur);
			}
			catch(ApplicationException ex){
				MessageBox.Show(ex.Message);
				return;
			}
			DialogResult=DialogResult.OK;
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textLevelDesired.IsValid()
				|| !textPrice.IsValid()
				|| !textOrderQty.IsValid()
				|| !textReorderLevel.IsValid())
			{
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			if(textDescript.Text==""){
				MsgBox.Show(this,"Please enter a description.");
				return;
			}
			SupplyCur.Category=comboCategory.GetSelectedDefNum();
			SupplyCur.CatalogNumber=textCatalogNumber.Text;
			SupplyCur.Descript=textDescript.Text;
			SupplyCur.LevelDesired=PIn.Float(textLevelDesired.Text);
			SupplyCur.OrderQty=PIn.Int(textOrderQty.Text);
			SupplyCur.Price=PIn.Double(textPrice.Text);
			SupplyCur.BarCodeOrID=textBarcode.Text.Trim();
			float levelOnHandTyped=PIn.Float(textOnHand.Text);
			bool isOnHandChanged=Math.Round(levelOnHandTyped,2)!=Math.Round(SupplyCur.LevelOnHand,2);
			if(SupplyCur.IsNew) {
				SupplyCur.LevelOnHand=levelOnHandTyped;//Insert writes it as an opening ledger row.
			}
			//the logic below handles some of the basics.  This is supplemented in some cases by the automatic order fixing.
			if(!SupplyCur.IsHidden && checkIsHidden.Checked){//hiding
				SupplyCur.ItemOrder=0;//not perfect.  Hidden get intermingled with the first item.
			}
			if(SupplyCur.IsHidden && !checkIsHidden.Checked){//unhiding
				SupplyCur.ItemOrder=0;//keeps it at top of list
			}
			SupplyCur.IsHidden=checkIsHidden.Checked;
			if(SupplyCur.IsNew){
				SupplyCur.SupplyNum=Supplies.Insert(SupplyCur);
			}
			else{
				Supplies.Update(SupplyCur);//Never writes LevelOnHand.
				if(isOnHandChanged && !InventoryTracking.IsEnabled()) {
					InventoryTracking.SetOnHand(SupplyCur.SupplyNum,levelOnHandTyped);//An Adjust row in the stock ledger.
				}
			}
			_supplySetting.SupplyNum=SupplyCur.SupplyNum;
			_supplySetting.ReorderLevel=PIn.Double(textReorderLevel.Text);
			_supplySetting.IsLotTracked=checkLotTracked.Checked;
			_supplySetting.IsImplant=checkImplant.Checked;
			if(_supplySetting.SupplySettingNum!=0 || _supplySetting.ReorderLevel!=0 || _supplySetting.IsLotTracked || _supplySetting.IsImplant) {
				SupplySettings.Upsert(_supplySetting);
			}
			if(InventoryTracking.IsEnabled()) {
				InventoryTracking.RefreshLowStockAlerts(new List<long> { SupplyCur.SupplyNum });
			}
			DialogResult=DialogResult.OK;
		}

	}
}