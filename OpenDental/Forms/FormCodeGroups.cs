using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormCodeGroups:FormODBase {
		private List<CodeGroup> _listCodeGroupsOld;
		private List<CodeGroup> _listCodeGroups;

		public FormCodeGroups() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormCodeGroups_Load(object sender,EventArgs e) {
			_listCodeGroupsOld=CodeGroups.GetDeepCopy();
			_listCodeGroups=_listCodeGroupsOld.Select(x => x.Copy()).ToList();
			FillGrid();
		}

		private void FillGrid() {
			CodeGroup codeGroupSelected=gridMain.SelectedTag<CodeGroup>();
			UpdateItemOrder();
			string trans=gridMain.TranslationName;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Group Name"),0) { IsWidthDynamic=true,DynamicWeight=1 });
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Fixed Group"),110));
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Proc Codes"),0) { IsWidthDynamic=true,DynamicWeight=2 });
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Freq"),35,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Age"),35,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Other"),40,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(trans,"Hidden"),45,HorizontalAlignment.Center));
			gridMain.ListGridRows.Clear();
			int index=-1;
			for(int i=0;i<_listCodeGroups.Count;i++) {
				//From Open Dental 25.1, hidden means retired.
				if(!checkShowHidden.Checked && _listCodeGroups[i].IsHidden) {
					continue;
				}
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(_listCodeGroups[i].GroupName);
				string strCodeGroupFixed="";
				if(_listCodeGroups[i].CodeGroupFixed!=EnumCodeGroupFixed.None) {
					strCodeGroupFixed=_listCodeGroups[i].CodeGroupFixed.GetDescription();
				}
				gridRow.Cells.Add(strCodeGroupFixed);
				gridRow.Cells.Add(_listCodeGroups[i].ProcCodes);
				gridRow.Cells.Add(_listCodeGroups[i].ShowInFrequency?"X":"");
				gridRow.Cells.Add(_listCodeGroups[i].ShowInAgeLimit?"X":"");
				gridRow.Cells.Add(_listCodeGroups[i].ShowInOther?"X":"");
				gridRow.Cells.Add(_listCodeGroups[i].IsHidden?"X":"");
				gridRow.Tag=_listCodeGroups[i];
				if(_listCodeGroups[i].Equals(codeGroupSelected)) {
					//Use the count before adding this row to the list because index is 0 based.
					index=gridMain.ListGridRows.Count;
				}
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
			gridMain.SetSelected(index);
		}

		private void UpdateItemOrder() {
			int itemOrder=0;
			// First the code groups in use are ordered, then the hidden (retired) ones. Also renumbers after a delete.
			List<CodeGroup> listCodeGroupsShown=_listCodeGroups.FindAll(x=>!x.IsHidden);
			for(itemOrder=0;itemOrder<listCodeGroupsShown.Count;itemOrder++) {
				listCodeGroupsShown[itemOrder].ItemOrder=itemOrder;
			}
			// Then the hidden code groups are ordered.
			List<CodeGroup> listCodeGroupsHidden=_listCodeGroups.FindAll(x=>x.IsHidden);
			for(int h=0;h<listCodeGroupsHidden.Count;h++) {
				listCodeGroupsHidden[h].ItemOrder=itemOrder;
				itemOrder++;
			}
			_listCodeGroups=_listCodeGroups.OrderBy(x=>x.ItemOrder).ToList();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			using FormCodeGroupEdit formCodeGroupEdit=new FormCodeGroupEdit();
			formCodeGroupEdit.CodeGroup=new CodeGroup();
			formCodeGroupEdit.CodeGroup.IsNew=true;
			formCodeGroupEdit.ListEnumCodeGroupFixedsInUse=_listCodeGroups.Select(x => x.CodeGroupFixed).ToList();
			if(formCodeGroupEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			if(!_listCodeGroups.IsNullOrEmpty()) {
				formCodeGroupEdit.CodeGroup.ItemOrder=_listCodeGroups.Max(x => x.ItemOrder) + 1;
			}
			_listCodeGroups.Add(formCodeGroupEdit.CodeGroup);
			FillGrid();
		}

		private void butUp_Click(object sender,EventArgs e) {
			CodeGroup codeGroupSelected=gridMain.SelectedTag<CodeGroup>();
			if(codeGroupSelected==null) {
				return;
			}
			//Only manipulate the ItemOrder values of the code groups that are visible to the user.
			List<CodeGroup> listCodeGroups=gridMain.GetTags<CodeGroup>();
			//Figure out where the selected code group falls within the grid.
			int index=listCodeGroups.IndexOf(codeGroupSelected);
			if(index<=0) {
				return;//Not found or first in the grid.
			}
			if(codeGroupSelected.IsHidden && !listCodeGroups[index-1].IsHidden) {
				MsgBox.Show("Hidden CodeGroups cannot be ordered above visible codegroups.");
				return;
			}
			//Swap the ItemOrder with the code group directly above the selected code group.
			CodeGroup codeGroupSwap=listCodeGroups[index - 1];
			//Swap places with the swappable code group found.
			int itemOrder=codeGroupSelected.ItemOrder;
			codeGroupSelected.ItemOrder=codeGroupSwap.ItemOrder;
			codeGroupSwap.ItemOrder=itemOrder;
			//Physically reorder the list of code groups now that the ItemOrder values have been manipulated.
			_listCodeGroups=_listCodeGroups.OrderBy(x => x.ItemOrder).ToList();
			FillGrid();
		}

		private void butDown_Click(object sender,EventArgs e) {
			CodeGroup codeGroupSelected=gridMain.SelectedTag<CodeGroup>();
			if(codeGroupSelected==null) {
				return;
			}
			//Only manipulate the ItemOrder values of the code groups that are visible to the user.
			List<CodeGroup> listCodeGroups=gridMain.GetTags<CodeGroup>();
			//Figure out where the selected code group falls within the grid.
			int index=listCodeGroups.IndexOf(codeGroupSelected);
			if(index==-1 || index>=(listCodeGroups.Count - 1)) {
				return;//Not found or last in the grid.
			}
			if(!codeGroupSelected.IsHidden && listCodeGroups[index+1].IsHidden) {
				MsgBox.Show("Hidden CodeGroups cannot be ordered above visible codegroups.");
				return;
			}
			//Swap the ItemOrder with the code group directly below the selected code group.
			CodeGroup codeGroupSwap=listCodeGroups[index + 1];
			//Swap places with the swappable code group found.
			int itemOrder=codeGroupSelected.ItemOrder;
			codeGroupSelected.ItemOrder=codeGroupSwap.ItemOrder;
			codeGroupSwap.ItemOrder=itemOrder;
			//Physically reorder the list of code groups now that the ItemOrder values have been manipulated.
			_listCodeGroups=_listCodeGroups.OrderBy(x => x.ItemOrder).ToList();
			FillGrid();
		}

		private void checkShowHidden_CheckedChanged(object sender,EventArgs e) {
			FillGrid();
		}

		private void gridMain_CellDoubleClick(object sender,UI.ODGridClickEventArgs e) {
			using FormCodeGroupEdit formCodeGroupEdit=new FormCodeGroupEdit();
			formCodeGroupEdit.CodeGroup=gridMain.SelectedTag<CodeGroup>();
			formCodeGroupEdit.ListEnumCodeGroupFixedsInUse=_listCodeGroups.Select(x => x.CodeGroupFixed).ToList();
			if(formCodeGroupEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			if(formCodeGroupEdit.IsDeleted) {
				//Removed from the database by Sync when the list is saved; FillGrid renumbers ItemOrder.
				_listCodeGroups.Remove(formCodeGroupEdit.CodeGroup);
			}
			FillGrid();
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(CodeGroups.Sync(_listCodeGroups,_listCodeGroupsOld)) {
				MakeLogEntries();
				DataValid.SetInvalid(InvalidType.CodeGroups);
			}
			DialogResult=DialogResult.OK;
		}

		///<summary>Setup log entries for added, deleted and edited code groups. Edits show the Proc Codes text before and after when it changed.</summary>
		private void MakeLogEntries() {
			for(int i=0;i<_listCodeGroupsOld.Count;i++) {
				CodeGroup codeGroupOld=_listCodeGroupsOld[i];
				CodeGroup codeGroup=_listCodeGroups.Find(x => x.CodeGroupNum==codeGroupOld.CodeGroupNum);
				if(codeGroup==null) {
					SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,Lan.g(this,"Code group deleted:")+" "+codeGroupOld.GroupName);
					continue;
				}
				List<string> listChanges=new List<string>();
				if(codeGroup.GroupName!=codeGroupOld.GroupName) {
					listChanges.Add(Lan.g(this,"name from")+" '"+codeGroupOld.GroupName+"' "+Lan.g(this,"to")+" '"+codeGroup.GroupName+"'");
				}
				if(codeGroup.ProcCodes!=codeGroupOld.ProcCodes) {
					listChanges.Add(Lan.g(this,"proc codes from")+" '"+codeGroupOld.ProcCodes+"' "+Lan.g(this,"to")+" '"+codeGroup.ProcCodes+"'");
				}
				if(codeGroup.IsHidden!=codeGroupOld.IsHidden) {
					listChanges.Add(codeGroup.IsHidden?Lan.g(this,"hidden"):Lan.g(this,"unhidden"));
				}
				if(codeGroup.ShowInFrequency!=codeGroupOld.ShowInFrequency
					|| codeGroup.ShowInAgeLimit!=codeGroupOld.ShowInAgeLimit
					|| codeGroup.ShowInOther!=codeGroupOld.ShowInOther
					|| codeGroup.CodeGroupFixed!=codeGroupOld.CodeGroupFixed)
				{
					listChanges.Add(Lan.g(this,"display options"));
				}
				if(listChanges.Count>0) {
					SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,Lan.g(this,"Code group")+" '"+codeGroup.GroupName+"' "+Lan.g(this,"edited:")+" "
						+string.Join("; ",listChanges));
				}
			}
			List<CodeGroup> listCodeGroupsAdded=_listCodeGroups.FindAll(x => !_listCodeGroupsOld.Any(y => y.CodeGroupNum==x.CodeGroupNum && x.CodeGroupNum!=0));
			for(int i=0;i<listCodeGroupsAdded.Count;i++) {
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,Lan.g(this,"Code group added:")+" "+listCodeGroupsAdded[i].GroupName
					+" ("+listCodeGroupsAdded[i].ProcCodes+")");
			}
		}
	}
}