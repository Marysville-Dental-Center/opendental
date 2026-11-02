// Routine maintenance (docs/specs/routine-maintenance.md, "History"): who did what and when, per item or overall.
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
	///<summary>The recorded completions of one maintenance item, or of every item, for a range of dates done, with each completion's checklist and
	///results, and when and by whom a completion was last corrected. Printable and exportable like other grids. A completion is never deleted;
	///Correct opens the selected one for correction (FormMaintRecordEdit), for the user who recorded it or a user with MaintenanceSetup.</summary>
	public partial class FormMaintHistory:FormODBase {
		///<summary>0 for every item.</summary>
		private long _maintItemNum;
		private List<MaintItem> _listMaintItems=new List<MaintItem>();
		private List<MaintRecord> _listMaintRecords=new List<MaintRecord>();
		private List<MaintRecordValue> _listMaintRecordValues=new List<MaintRecordValue>();
		private int _pagesPrinted;
		private bool _isHeadingPrinted;
		private int _heightHeading;

		public FormMaintHistory(long maintItemNum) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_maintItemNum=maintItemNum;
		}

		private void FormMaintHistory_Load(object sender,EventArgs e) {
			_listMaintItems=MaintItems.GetAll(includeHidden:true);
			DateTime dateToday=MaintAlerts.GetToday();
			textDateFrom.Text=dateToday.AddYears(-1).ToShortDateString();
			textDateTo.Text=dateToday.ToShortDateString();
			if(_maintItemNum==0) {
				labelItem.Text=Lan.g(this,"All maintenance items");
			}
			else {
				labelItem.Text=GetItemName(_maintItemNum);
			}
			FillGrid();
		}

		private string GetItemName(long maintItemNum) {
			MaintItem maintItem=_listMaintItems.FirstOrDefault(x => x.MaintItemNum==maintItemNum);
			return maintItem==null ? "("+Lan.g(this,"deleted item")+")" : maintItem.ItemName;
		}

		private void FillGrid() {
			if(!textDateFrom.IsValid() || !textDateTo.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			DateTime dateFrom=textDateFrom.Text=="" ? new DateTime(1880,1,1) : PIn.Date(textDateFrom.Text);
			DateTime dateTo=textDateTo.Text=="" ? new DateTime(9999,12,31) : PIn.Date(textDateTo.Text);
			_listMaintRecords=MaintRecords.GetForDateRange(dateFrom,dateTo,_maintItemNum);
			_listMaintRecordValues=MaintRecordValues.GetForRecords(_listMaintRecords.Select(x => x.MaintRecordNum).ToList());
			long maintRecordNumSelected=gridMain.SelectedTag<MaintRecord>()?.MaintRecordNum??0;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Date Done"),80));
			if(_maintItemNum==0) {
				gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Item"),180));
			}
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Done By"),100));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Due"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Late"),50,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Flagged"),55,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Checklist"),120));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Results"),200){ IsWidthDynamic=true });
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Note"),130));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Recorded"),120));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Corrected"),150));
			int indexSelect=-1;
			gridMain.ListGridRows.Clear();
			foreach(MaintRecord maintRecord in _listMaintRecords) {
				GridRow row=new GridRow();
				row.Cells.Add(maintRecord.DateDone.ToShortDateString());
				if(_maintItemNum==0) {
					row.Cells.Add(GetItemName(maintRecord.MaintItemNum));
				}
				row.Cells.Add(Userods.GetName(maintRecord.UserNum));
				row.Cells.Add(maintRecord.DateDue.Year>1880 ? maintRecord.DateDue.ToShortDateString() : "");
				string late="";
				if(maintRecord.DateDue.Year>1880 && maintRecord.DateDone.Date>maintRecord.DateDue.Date) {
					late=((int)(maintRecord.DateDone.Date-maintRecord.DateDue.Date).TotalDays).ToString();
				}
				row.Cells.Add(late);
				row.Cells.Add(maintRecord.IsFlagged ? "X" : "");
				List<MaintRecordValue> listValuesRecord=_listMaintRecordValues.FindAll(x => x.MaintRecordNum==maintRecord.MaintRecordNum);
				//The checklist outcome is in the main grid, so the printout and the export show a completion with skipped steps as such.
				row.Cells.Add(MaintValues.GetChecklistText(listValuesRecord));
				List<MaintRecordValue> listValues=listValuesRecord.FindAll(x => x.FieldType!=EnumMaintFieldType.ChecklistStep && x.ValueText!="");
				row.Cells.Add(string.Join("; ",listValues.Select(x => x.Label+": "+MaintValues.GetDisplayText(x))));
				row.Cells.Add(maintRecord.Note);
				row.Cells.Add(maintRecord.SecDateTEntry.Year>1880 ? maintRecord.SecDateTEntry.ToString() : "");
				//A corrected completion says so in the grid, so the printout and the export show it too.
				row.Cells.Add(MaintCorrections.GetCorrectedText(maintRecord,maintRecord.UserNumCorrected==0 ? "" : Userods.GetName(maintRecord.UserNumCorrected)));
				if(maintRecord.IsFlagged) {
					row.ColorBackG=Color.FromArgb(255,215,215);
				}
				else if(MaintValues.HasStepNotDone(listValuesRecord)) {
					row.ColorBackG=Color.FromArgb(255,245,200);//Light yellow: a checklist step was not done.
				}
				row.Tag=maintRecord;
				gridMain.ListGridRows.Add(row);
				if(maintRecord.MaintRecordNum==maintRecordNumSelected) {
					indexSelect=gridMain.ListGridRows.Count-1;
				}
			}
			gridMain.EndUpdate();
			if(indexSelect>=0) {
				gridMain.SetSelected(indexSelect,true);
			}
			FillGridValues();
		}

		private void FillGridValues() {
			MaintRecord maintRecord=gridMain.SelectedTag<MaintRecord>();
			gridValues.BeginUpdate();
			gridValues.Columns.Clear();
			gridValues.Columns.Add(new GridColumn(Lan.g(gridValues.TranslationName,"Checklist Step or Result"),250));
			gridValues.Columns.Add(new GridColumn(Lan.g(gridValues.TranslationName,"Value"),200){ IsWidthDynamic=true });
			gridValues.Columns.Add(new GridColumn(Lan.g(gridValues.TranslationName,"Flag"),60,HorizontalAlignment.Center));
			gridValues.ListGridRows.Clear();
			if(maintRecord!=null) {
				foreach(MaintRecordValue maintRecordValue in _listMaintRecordValues.FindAll(x => x.MaintRecordNum==maintRecord.MaintRecordNum)) {
					GridRow row=new GridRow();
					row.Cells.Add(maintRecordValue.Label);
					row.Cells.Add(MaintValues.GetDisplayText(maintRecordValue));
					row.Cells.Add(maintRecordValue.IsFlagged ? "X" : "");
					if(maintRecordValue.IsFlagged) {
						row.ColorBackG=Color.FromArgb(255,215,215);
					}
					else if(maintRecordValue.FieldType==EnumMaintFieldType.ChecklistStep && maintRecordValue.ValueText!=MaintValues.VALUE_TRUE) {
						row.ColorBackG=Color.FromArgb(255,245,200);
					}
					row.Tag=maintRecordValue;
					gridValues.ListGridRows.Add(row);
				}
			}
			gridValues.EndUpdate();
		}

		private void gridMain_CellClick(object sender,ODGridClickEventArgs e) {
			FillGridValues();
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void OpenAttachment() {
			MaintRecordValue maintRecordValue=gridValues.SelectedTag<MaintRecordValue>();
			MaintRecord maintRecord=gridMain.SelectedTag<MaintRecord>();
			if(maintRecordValue==null || maintRecord==null || maintRecordValue.FieldType!=EnumMaintFieldType.Attachment || maintRecordValue.ValueText=="") {
				MsgBox.Show(this,"Please select an attachment in the lower list first.");
				return;
			}
			string path=MaintAttachments.GetFilePath(maintRecord.MaintItemNum,maintRecordValue.ValueText);
			if(path==null || !FileAtoZ.Exists(path)) {
				MsgBox.Show(this,"The attached file was not found in the images folder.");
				return;
			}
			FileAtoZ.OpenFile(path,maintRecordValue.ValueText);
		}

		private void butOpenAttachment_Click(object sender,EventArgs e) {
			OpenAttachment();
		}

		private void gridValues_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			MaintRecordValue maintRecordValue=gridValues.SelectedTag<MaintRecordValue>();
			if(maintRecordValue!=null && maintRecordValue.FieldType==EnumMaintFieldType.Attachment) {
				OpenAttachment();
			}
		}

		///<summary>Corrects the selected completion: its checklist ticks, results, attachments and note. Only the user who recorded it or a user with
		///MaintenanceSetup; a completion is never deleted.</summary>
		private void butCorrect_Click(object sender,EventArgs e) {
			MaintRecord maintRecordSelected=gridMain.SelectedTag<MaintRecord>();
			if(maintRecordSelected==null) {
				MsgBox.Show(this,"Please select a completion first.");
				return;
			}
			MaintRecord maintRecord=MaintRecords.GetOne(maintRecordSelected.MaintRecordNum);
			if(maintRecord==null) {
				MsgBox.Show(this,"This completion was not found.");
				FillGrid();
				return;
			}
			if(!MaintRecords.CanCorrect(Security.CurUser,maintRecord)) {
				MsgBox.Show(this,"Only the user who recorded this completion or a user with Maintenance Setup can correct it.");
				return;
			}
			MaintItem maintItem=MaintItems.GetOne(maintRecord.MaintItemNum)??_listMaintItems.FirstOrDefault(x => x.MaintItemNum==maintRecord.MaintItemNum);
			if(maintItem==null) {
				MsgBox.Show(this,"The item of this completion was not found.");
				return;
			}
			using FormMaintRecordEdit formMaintRecordEdit=new FormMaintRecordEdit(maintItem,maintRecord);
			if(formMaintRecordEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			FillGrid();
		}

		private void butExport_Click(object sender,EventArgs e) {
			gridMain.Export(gridMain.Title);
		}

		private void butPrint_Click(object sender,EventArgs e) {
			_pagesPrinted=0;
			_isHeadingPrinted=false;
			PrinterL.TryPrintOrDebugRpPreview(pd_PrintPage,Lan.g(this,"Maintenance history printed"),PrintoutOrientation.Landscape);
		}

		private void pd_PrintPage(object sender,System.Drawing.Printing.PrintPageEventArgs e) {
			Rectangle rectangle=e.MarginBounds;
			Graphics g=e.Graphics;
			using Font fontHeading=new Font("Arial",13,FontStyle.Bold);
			using Font fontSubHeading=new Font("Arial",10,FontStyle.Bold);
			int yPos=rectangle.Top;
			int center=rectangle.X+rectangle.Width/2;
			if(!_isHeadingPrinted) {
				string text=Lan.g(this,"Maintenance History")+" - "+labelItem.Text;
				g.DrawString(text,fontHeading,Brushes.Black,center-g.MeasureString(text,fontHeading).Width/2,yPos);
				yPos+=(int)g.MeasureString(text,fontHeading).Height;
				text=textDateFrom.Text+" "+Lan.g(this,"to")+" "+textDateTo.Text;
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
