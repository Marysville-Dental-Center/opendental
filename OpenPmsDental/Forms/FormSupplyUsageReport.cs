// Inventory tracking (docs/specs/inventory-tracking.md): usage and cost by item and by procedure code for a period.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Inventory, Inventory, Usage Report. Uses minus restores (and dispensing, by item) in the date range, by ledger date, with the
	///cost at the unit cost of each ledger row.</summary>
	public partial class FormSupplyUsageReport:FormODBase {
		private int _pagesPrinted;
		private bool _isHeadingPrinted;
		private int _heightHeadingPrint;

		public FormSupplyUsageReport() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyUsageReport_Load(object sender,EventArgs e) {
			DateTime dateToday=DateTime.Today;
			textDateFrom.Text=new DateTime(dateToday.Year,dateToday.Month,1).AddMonths(-1).ToShortDateString();
			textDateTo.Text=new DateTime(dateToday.Year,dateToday.Month,1).AddDays(-1).ToShortDateString();
			radioByItem.Checked=true;
			FillGrid();
		}

		private void FillGrid() {
			if(!textDateFrom.IsValid() || !textDateTo.IsValid() || textDateFrom.Text=="" || textDateTo.Text=="") {
				MsgBox.Show(this,"Please enter valid dates.");
				return;
			}
			DateTime dateFrom=PIn.Date(textDateFrom.Text);
			DateTime dateTo=PIn.Date(textDateTo.Text);
			double costTotal=0;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.ListGridRows.Clear();
			if(radioByItem.Checked) {
				DataTable table=SupplyLedgers.GetUsageByItem(dateFrom,dateTo);
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Catalog #"),90));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Supply"),330));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Qty Used"),80,HorizontalAlignment.Right));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Cost"),90,HorizontalAlignment.Right));
				foreach(DataRow dataRow in table.Rows) {
					double cost=PIn.Double(dataRow["Cost"].ToString());
					costTotal+=cost;
					gridMain.ListGridRows.Add(new GridRow(dataRow["CatalogNumber"].ToString(),dataRow["Descript"].ToString(),
						InventoryTracking.FormatQty(PIn.Double(dataRow["QtyUsed"].ToString())),cost.ToString("n")));
				}
			}
			else {
				DataTable table=SupplyLedgers.GetUsageByProcCode(dateFrom,dateTo);
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Code"),70));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Procedure"),170));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Supply"),220));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Procs"),50,HorizontalAlignment.Center));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Qty Used"),70,HorizontalAlignment.Right));
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Cost"),80,HorizontalAlignment.Right));
				string procCodeLast="";
				foreach(DataRow dataRow in table.Rows) {
					double cost=PIn.Double(dataRow["Cost"].ToString());
					costTotal+=cost;
					string procCode=dataRow["ProcCode"].ToString();
					gridMain.ListGridRows.Add(new GridRow(procCode==procCodeLast ? "" : procCode,procCode==procCodeLast ? "" : dataRow["CodeDescript"].ToString(),
						dataRow["Descript"].ToString(),dataRow["CountProcs"].ToString(),InventoryTracking.FormatQty(PIn.Double(dataRow["QtyUsed"].ToString())),
						cost.ToString("n")));
					procCodeLast=procCode;
				}
			}
			gridMain.EndUpdate();
			labelTotal.Text=Lan.g(this,"Total cost")+": "+costTotal.ToString("c");
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void radio_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(gridMain.ListGridRows.Count==0) {
				MsgBox.Show(this,"Nothing to print.");
				return;
			}
			_pagesPrinted=0;
			_isHeadingPrinted=false;
			PrinterL.TryPrintOrDebugRpPreview(pd_PrintPage,Lan.g(this,"Supply usage report printed"),margins:new Margins(50,50,40,30));
		}

		private void pd_PrintPage(object sender,PrintPageEventArgs e) {
			Rectangle bounds=e.MarginBounds;
			Graphics g=e.Graphics;
			int yPos=bounds.Top;
			if(!_isHeadingPrinted) {
				using Font fontHeading=new Font("Arial",13,FontStyle.Bold);
				using Font fontSubHeading=new Font("Arial",10,FontStyle.Bold);
				string text=Lan.g(this,"Supply Usage")+" - "+(radioByItem.Checked ? Lan.g(this,"by item") : Lan.g(this,"by procedure code"));
				g.DrawString(text,fontHeading,Brushes.Black,bounds.X+bounds.Width/2-g.MeasureString(text,fontHeading).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,fontHeading).Height;
				text=textDateFrom.Text+" - "+textDateTo.Text+"   "+labelTotal.Text;
				g.DrawString(text,fontSubHeading,Brushes.Black,bounds.X+bounds.Width/2-g.MeasureString(text,fontSubHeading).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,fontHeading).Height+5;
				_isHeadingPrinted=true;
				_heightHeadingPrint=yPos;
			}
			yPos=gridMain.PrintPage(g,_pagesPrinted,bounds,_heightHeadingPrint);
			_pagesPrinted++;
			e.HasMorePages=(yPos==-1);
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}
	}
}
