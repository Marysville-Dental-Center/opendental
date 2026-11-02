// Routine maintenance (docs/specs/routine-maintenance.md): the due and overdue board, opened from the Manage module and from the alerts.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Every maintenance item with its due date and status: overdue, due soon (within its reminder lead time) and upcoming. Records a
	///completion (double-click or Record), sets items up (Add Item, Edit Item) and opens the history. Viewing needs only the Manage module;
	///setup needs MaintenanceSetup and recording MaintenanceRecord.</summary>
	public partial class FormMaintBoard:FormODBase {
		private List<MaintItem> _listMaintItems=new List<MaintItem>();
		private List<MaintRecord> _listMaintRecords=new List<MaintRecord>();
		private List<MaintBoardRow> _listMaintBoardRows=new List<MaintBoardRow>();
		private List<Equipment> _listEquipments=new List<Equipment>();
		///<summary>The server's date, read when the window opens and on Refresh.</summary>
		private DateTime _dateToday;
		private int _pagesPrinted;
		private bool _isHeadingPrinted;
		private int _heightHeading;
		private const string FILTER_ALL="all";
		private const string FILTER_UNASSIGNED="none";

		public FormMaintBoard() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormMaintBoard_Load(object sender,EventArgs e) {
			RefreshData();
			FillComboAssignee();
			FillGrid();
		}

		private void RefreshData() {
			_dateToday=MaintAlerts.GetToday();
			_listMaintItems=MaintItems.GetAll(includeHidden:true);
			_listMaintRecords=MaintRecords.GetForItems(_listMaintItems.Select(x => x.MaintItemNum).ToList());
			_listEquipments=Equipments.GetList(DateTime.MinValue.AddDays(1),DateTime.MaxValue,EnumEquipmentDisplayMode.All,"");
		}

		///<summary>All, Unassigned, then every user and user group that some item is assigned to. Keeps the selection when it still exists.</summary>
		private void FillComboAssignee() {
			string tagSelected=comboAssignee.SelectedIndex>=0 ? comboAssignee.GetSelected<string>() : FILTER_ALL;
			comboAssignee.Items.Clear();
			comboAssignee.Items.Add(Lan.g(this,"All"),FILTER_ALL);
			comboAssignee.Items.Add(Lan.g(this,"Unassigned"),FILTER_UNASSIGNED);
			foreach(long userNum in _listMaintItems.Where(x => x.UserNumAssigned!=0).Select(x => x.UserNumAssigned).Distinct()
				.OrderBy(x => Userods.GetName(x)))
			{
				comboAssignee.Items.Add(Userods.GetName(userNum),"U"+userNum);
			}
			foreach(long userGroupNum in _listMaintItems.Where(x => x.UserGroupNumAssigned!=0).Select(x => x.UserGroupNumAssigned).Distinct()) {
				UserGroup userGroup=UserGroups.GetGroup(userGroupNum);
				comboAssignee.Items.Add(Lan.g(this,"Group")+": "+(userGroup==null ? "?" : userGroup.Description),"G"+userGroupNum);
			}
			int index=comboAssignee.Items.GetAll<string>().IndexOf(tagSelected);
			comboAssignee.SelectedIndex=Math.Max(0,index);
		}

		private bool IsShown(MaintItem maintItem) {
			if(maintItem.IsHidden && !checkShowHidden.Checked) {
				return false;
			}
			if(checkMine.Checked && !MaintItems.IsAssignedTo(maintItem,Security.CurUser)) {
				return false;
			}
			string filter=comboAssignee.SelectedIndex>=0 ? comboAssignee.GetSelected<string>() : FILTER_ALL;
			if(filter==FILTER_UNASSIGNED) {
				return maintItem.UserNumAssigned==0 && maintItem.UserGroupNumAssigned==0;
			}
			if(filter.StartsWith("U")) {
				return maintItem.UserNumAssigned==PIn.Long(filter.Substring(1));
			}
			if(filter.StartsWith("G")) {
				return maintItem.UserGroupNumAssigned==PIn.Long(filter.Substring(1));
			}
			return true;
		}

		private void FillGrid() {
			long maintItemNumSelected=gridMain.SelectedTag<MaintBoardRow>()?.MaintItem.MaintItemNum??0;
			_listMaintBoardRows=MaintBoard.GetRows(_listMaintItems.FindAll(x => IsShown(x)),_listMaintRecords,_dateToday);
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Status"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Due"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Days"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Item"),220));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Assigned To"),130));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Schedule"),200));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Last Done"),160));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Equipment"),100){ IsWidthDynamic=true });
			gridMain.ListGridRows.Clear();
			int indexSelect=-1;
			for(int i=0;i<_listMaintBoardRows.Count;i++) {
				MaintBoardRow maintBoardRow=_listMaintBoardRows[i];
				GridRow row=new GridRow();
				row.Cells.Add(MaintSchedules.GetStatusText(maintBoardRow.Status));
				row.Cells.Add(maintBoardRow.DateDue.Year>1880 ? maintBoardRow.DateDue.ToShortDateString() : "");
				row.Cells.Add(maintBoardRow.MaintItem.IsHidden ? "" : MaintSchedules.GetDaysText(maintBoardRow.DateDue,_dateToday));
				row.Cells.Add(maintBoardRow.MaintItem.ItemName);
				row.Cells.Add(MaintItems.GetAssigneeText(maintBoardRow.MaintItem));
				row.Cells.Add(MaintSchedules.GetScheduleText(maintBoardRow.MaintItem));
				string lastDone="";
				if(maintBoardRow.MaintRecordLast!=null) {
					lastDone=maintBoardRow.MaintRecordLast.DateDone.ToShortDateString()+" "+Userods.GetName(maintBoardRow.MaintRecordLast.UserNum);
				}
				row.Cells.Add(lastDone);
				row.Cells.Add(GetEquipmentText(maintBoardRow.MaintItem.EquipmentNum));
				if(maintBoardRow.Status==EnumMaintStatus.Overdue) {
					row.ColorBackG=Color.FromArgb(255,215,215);
				}
				else if(maintBoardRow.Status==EnumMaintStatus.DueSoon) {
					row.ColorBackG=Color.FromArgb(255,245,200);
				}
				else if(maintBoardRow.Status==EnumMaintStatus.Hidden) {
					row.ColorText=Color.Gray;
				}
				row.Tag=maintBoardRow;
				gridMain.ListGridRows.Add(row);
				if(maintBoardRow.MaintItem.MaintItemNum==maintItemNumSelected) {
					indexSelect=i;
				}
			}
			gridMain.EndUpdate();
			if(indexSelect>=0) {
				gridMain.SetSelected(indexSelect,true);
			}
			int countOverdue=_listMaintBoardRows.Count(x => x.Status==EnumMaintStatus.Overdue);
			int countDueSoon=_listMaintBoardRows.Count(x => x.Status==EnumMaintStatus.DueSoon);
			labelSummary.Text=countOverdue+" "+Lan.g(this,"overdue")+", "+countDueSoon+" "+Lan.g(this,"due soon")+" ("+Lan.g(this,"today is")+" "
				+_dateToday.ToShortDateString()+")";
		}

		private string GetEquipmentText(long equipmentNum) {
			if(equipmentNum==0) {
				return "";
			}
			Equipment equipment=_listEquipments.FirstOrDefault(x => x.EquipmentNum==equipmentNum);
			if(equipment==null) {
				return Lan.g(this,"(not in the Equipment list)");
			}
			return equipment.Description+(string.IsNullOrEmpty(equipment.SerialNumber) ? "" : " ("+equipment.SerialNumber+")");
		}

		private MaintBoardRow GetSelectedRow() {
			MaintBoardRow maintBoardRow=gridMain.SelectedTag<MaintBoardRow>();
			if(maintBoardRow==null) {
				MsgBox.Show(this,"Please select an item first.");
			}
			return maintBoardRow;
		}

		private void RefreshAll() {
			RefreshData();
			FillComboAssignee();
			FillGrid();
		}

		private void comboAssignee_SelectionChangeCommitted(object sender,EventArgs e) {
			FillGrid();
		}

		private void checkMine_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void checkShowHidden_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			RefreshAll();
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			RecordSelected();
		}

		private void butRecord_Click(object sender,EventArgs e) {
			RecordSelected();
		}

		private void RecordSelected() {
			MaintBoardRow maintBoardRow=GetSelectedRow();
			if(maintBoardRow==null) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.MaintenanceRecord)) {
				return;
			}
			if(maintBoardRow.MaintItem.IsHidden) {
				MsgBox.Show(this,"This item is hidden. Show it again in Edit Item before recording a completion.");
				return;
			}
			using FormMaintRecordEdit formMaintRecordEdit=new FormMaintRecordEdit(maintBoardRow.MaintItem,maintBoardRow.DateDue,_dateToday);
			if(formMaintRecordEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			SyncAlerts();
			RefreshAll();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.MaintenanceSetup)) {
				return;
			}
			//The first due date (the anchor) defaults to today, the server's date.
			MaintItem maintItem=MaintItems.CreateNew(MaintAlerts.GetToday());
			using FormMaintItemEdit formMaintItemEdit=new FormMaintItemEdit(maintItem,isNew:true);
			if(formMaintItemEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			SyncAlerts();
			RefreshAll();
		}

		private void butEdit_Click(object sender,EventArgs e) {
			MaintBoardRow maintBoardRow=GetSelectedRow();
			if(maintBoardRow==null) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.MaintenanceSetup)) {
				return;
			}
			MaintItem maintItem=MaintItems.GetOne(maintBoardRow.MaintItem.MaintItemNum);
			if(maintItem==null) {
				MsgBox.Show(this,"This item was deleted by another user.");
				RefreshAll();
				return;
			}
			using FormMaintItemEdit formMaintItemEdit=new FormMaintItemEdit(maintItem,isNew:false);
			if(formMaintItemEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			SyncAlerts();
			RefreshAll();
		}

		private void butHistory_Click(object sender,EventArgs e) {
			MaintBoardRow maintBoardRow=GetSelectedRow();
			if(maintBoardRow==null) {
				return;
			}
			using FormMaintHistory formMaintHistory=new FormMaintHistory(maintBoardRow.MaintItem.MaintItemNum);
			formMaintHistory.ShowDialog();
		}

		private void butHistoryAll_Click(object sender,EventArgs e) {
			using FormMaintHistory formMaintHistory=new FormMaintHistory(0);
			formMaintHistory.ShowDialog();
		}

		///<summary>Brings the maintenance alerts up to date at once, so the Alerts menu reflects the change without waiting for the next check.</summary>
		private void SyncAlerts() {
			try {
				MaintAlerts.Sync(MaintAlerts.GetToday());
				DataValid.SetInvalid(InvalidType.AlertItems);
			}
			catch(Exception ex) {
				ex.DoNothing();//The background check retries; the completion or the item is already saved.
			}
		}

		private void butExport_Click(object sender,EventArgs e) {
			gridMain.Export(gridMain.Title);
		}

		private void butPrint_Click(object sender,EventArgs e) {
			_pagesPrinted=0;
			_isHeadingPrinted=false;
			PrinterL.TryPrintOrDebugRpPreview(pd_PrintPage,Lan.g(this,"Maintenance board printed"),CodeBase.PrintoutOrientation.Landscape);
		}

		private void pd_PrintPage(object sender,System.Drawing.Printing.PrintPageEventArgs e) {
			Rectangle rectangle=e.MarginBounds;
			Graphics g=e.Graphics;
			using Font fontHeading=new Font("Arial",13,FontStyle.Bold);
			using Font fontSubHeading=new Font("Arial",10,FontStyle.Bold);
			int yPos=rectangle.Top;
			int center=rectangle.X+rectangle.Width/2;
			if(!_isHeadingPrinted) {
				string text=Lan.g(this,"Maintenance Board");
				g.DrawString(text,fontHeading,Brushes.Black,center-g.MeasureString(text,fontHeading).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,fontHeading).Height;
				text=labelSummary.Text;
				g.DrawString(text,fontSubHeading,Brushes.Black,center-g.MeasureString(text,fontSubHeading).Width/2,yPos);
				yPos+=20;
				_isHeadingPrinted=true;
				_heightHeading=yPos;
			}
			yPos=gridMain.PrintPage(g,_pagesPrinted,rectangle,_heightHeading);
			_pagesPrinted++;
			e.HasMorePages=(yPos==-1);
			g.Dispose();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
