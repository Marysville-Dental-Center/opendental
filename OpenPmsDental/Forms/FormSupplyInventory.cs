using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;
using CodeBase;

namespace OpenPmsDental {
	public partial class FormSupplyInventory:FormODBase {
		private List<SupplyNeeded> _listSupplyNeededs;
		private int _pagesPrinted;
		private bool _isHeadingPrinted;
		private int _heightHeadingPrint;

		public FormSupplyInventory() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormInventory_Load(object sender,EventArgs e) {
			LayoutMenu();
			FillGridNeeded();
			FillStockCount();
		}
		private void LayoutMenu() {
			menuMain.BeginUpdate();
			//Suppliers-----------------------------------------------------------------------------------------------------------
			menuMain.Add(new MenuItemOD("Suppliers",menuItemSuppliers_Click));
			//Categories-----------------------------------------------------------------------------------------------------------
			menuMain.Add(new MenuItemOD("Categories",menuItemCategories_Click));
			//Inventory tracking (docs/specs/inventory-tracking.md)----------------------------------------------------------------
			MenuItemOD menuItemInventory=new MenuItemOD("Inventory");
			menuItemInventory.Add("Setup",menuItemInventorySetup_Click);
			menuItemInventory.Add("Default Materials of Procedure Codes",menuItemDefaultMaterials_Click);
			menuItemInventory.AddSeparator();
			menuItemInventory.Add("Stock Count",menuItemStockCount_Click);
			menuItemInventory.Add("Awaiting Materials",menuItemAwaitingMaterials_Click);
			menuItemInventory.Add("Usage Report",menuItemUsageReport_Click);
			menuItemInventory.AddSeparator();
			menuItemInventory.Add("Repair Stock from Ledger",menuItemRepairStock_Click);
			menuMain.Add(menuItemInventory);
			menuMain.EndUpdate();
		}

		#region Inventory tracking
		private void menuItemInventorySetup_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			using FormInventorySetup formInventorySetup=new FormInventorySetup();
			formInventorySetup.ShowDialog();
		}

		private void menuItemDefaultMaterials_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			using FormSupplyProcCodes formSupplyProcCodes=new FormSupplyProcCodes();
			formSupplyProcCodes.ShowDialog();
		}

		private void menuItemStockCount_Click(object sender,EventArgs e) {
			OpenStockCount();
		}

		private void butStockCount_Click(object sender,EventArgs e) {
			OpenStockCount();
		}

		///<summary>The Stock Count window, which resumes the office's open count if there is one (docs/specs/inventory-tracking.md, 3.5).</summary>
		private void OpenStockCount() {
			using FormSupplyCount formSupplyCount=new FormSupplyCount();
			formSupplyCount.ShowDialog();
			FillStockCount();
		}

		///<summary>Shows whether a stock count is open (saved while it is counted; any user can resume it): who started it, when, how many lines, and
		///whether its post runs or did not finish. The Stock Count button then reads Resume Count.</summary>
		private void FillStockCount() {
			string description=null;
			try {
				if(InventoryTracking.IsInstalled()) {
					description=InventoryTracking.GetOpenCountDescription();
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//A database without the count tables: the window works as before.
			}
			if(description==null) {
				labelStockCount.Text=Lan.g(this,"No stock count is open.");
				butStockCount.Text=Lan.g(this,"Stock Count");
				return;
			}
			labelStockCount.Text=description;
			butStockCount.Text=Lan.g(this,"Resume Count");
		}

		private void menuItemAwaitingMaterials_Click(object sender,EventArgs e) {
			using FormSupplyProcPendings formSupplyProcPendings=new FormSupplyProcPendings();
			formSupplyProcPendings.ShowDialog();
		}

		private void menuItemUsageReport_Click(object sender,EventArgs e) {
			using FormSupplyUsageReport formSupplyUsageReport=new FormSupplyUsageReport();
			formSupplyUsageReport.ShowDialog();
		}

		private void menuItemRepairStock_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InventoryAdjust)) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Put back what procedures that are no longer complete still hold, and set every supply's and lot's stock to the sum of its ledger rows?")) {
				return;
			}
			SupplyRepairResult supplyRepairResult;
			try {
				supplyRepairResult=InventoryTracking.Repair();
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
				return;
			}
			MessageBox.Show(this,Lan.g(this,"Procedures no longer complete, materials put back")+": "+supplyRepairResult.CountProcs+"\r\n"
				+Lan.g(this,"Awaiting Materials rows removed")+": "+supplyRepairResult.CountAwaiting+"\r\n"
				+Lan.g(this,"Supplies corrected")+": "+supplyRepairResult.CountSupplies+"\r\n"+Lan.g(this,"Lots corrected")+": "+supplyRepairResult.CountLots);
		}
		#endregion Inventory tracking

		private void FillGridNeeded(){
			_listSupplyNeededs=SupplyNeededs.CreateObjects();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g(this,"Date Added"),80);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Description"),300);
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			GridRow row;
			for(int i=0;i<_listSupplyNeededs.Count;i++){
				row=new GridRow();
				row.Cells.Add(_listSupplyNeededs[i].DateAdded.ToShortDateString());
				row.Cells.Add(_listSupplyNeededs[i].Description);
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private void gridNeeded_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			FrmSupplyNeededEdit frmSupplyNeededEdit=new FrmSupplyNeededEdit();
			frmSupplyNeededEdit.SupplyNeededCur=_listSupplyNeededs[e.Row];
			frmSupplyNeededEdit.ShowDialog();
			if(frmSupplyNeededEdit.IsDialogOK) {
				FillGridNeeded();
			}
		}

		private void butAddNeeded_Click(object sender,EventArgs e) {
			SupplyNeeded supplyNeeded=new SupplyNeeded();
			supplyNeeded.IsNew=true;
			supplyNeeded.DateAdded=DateTime.Today;
			FrmSupplyNeededEdit frmSupplyNeededEdit=new FrmSupplyNeededEdit();
			frmSupplyNeededEdit.SupplyNeededCur=supplyNeeded;
			frmSupplyNeededEdit.ShowDialog();
			if(frmSupplyNeededEdit.IsDialogOK){
				FillGridNeeded();
			}
		}

		private void menuItemSuppliers_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.SupplierEdit)) {
				return;
			}
			using FormSuppliers formSuppliers=new FormSuppliers();
			formSuppliers.ShowDialog();
		}

		private void menuItemCategories_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.DefEdit)) {
				return;
			}
			using FormDefinitions formDefinitions=new FormDefinitions(DefCat.SupplyCats);
			formDefinitions.ShowDialog();
			SecurityLogs.MakeLogEntry(EnumPermType.DefEdit,0,"Definitions.");
		}

		private void butEquipment_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.EquipmentSetup)) {
				return;
			}
			using FormEquipment formEquipment=new FormEquipment();
			formEquipment.ShowDialog();
		}

		private void butOrders_Click(object sender,EventArgs e) {
			using FormSupplyOrders formSupplyOrders=new FormSupplyOrders();
			formSupplyOrders.ShowDialog();
		}

		private void butSupplies_Click(object sender,EventArgs e) {
			using FormSupplies formSupplies=new FormSupplies();
			formSupplies.ShowDialog();
		}

		private void butPrint_Click(object sender,EventArgs e) {
			_pagesPrinted=0;
			_isHeadingPrinted=false;
			PrinterL.TryPrintOrDebugRpPreview(pd_PrintPage,Lan.g(this,"Supplies needed list printed"));
		}

		private void pd_PrintPage(object sender,System.Drawing.Printing.PrintPageEventArgs e) {
			Rectangle bounds=e.MarginBounds;
			//new Rectangle(50,40,800,1035);//Some printers can handle up to 1042
			using Graphics g=e.Graphics;
			string text;
			Font fontHeading=new Font("Arial",13,FontStyle.Bold);
			Font fontSubHeading=new Font("Arial",10,FontStyle.Bold);
			int yPos=bounds.Top;
			int centerPos=bounds.X+bounds.Width/2;
			#region printHeading
			if(!_isHeadingPrinted) {
				text=Lan.g(this,"Supplies Needed");
				g.DrawString(text,fontHeading,Brushes.Black,centerPos-g.MeasureString(text,fontHeading).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,fontHeading).Height;
				//text=Lan.g(this,"Supplies Needed");
				//g.DrawString(text,headingFont,Brushes.Black,center-g.MeasureString(text,headingFont).Width/2,yPos);
				//yPos+=(int)g.MeasureString(text,headingFont).Height;
				yPos+=20;
				_isHeadingPrinted=true;
				_heightHeadingPrint=yPos;
			}
			#endregion
			yPos=gridMain.PrintPage(g,_pagesPrinted,bounds,_heightHeadingPrint);
			_pagesPrinted++;
			if(yPos==-1) {
				e.HasMorePages=true;
			}
			else {
				e.HasMorePages=false;
			}
		}

	}
}