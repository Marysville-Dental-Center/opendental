using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using CodeBase;
using Microsoft.Win32;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Edits the Query Monitor filter groups (queryfilter rows) and picks the current user's active group. Changes are kept in memory until
	///Save. Opened from the Filter button of the Query Monitor, which already required the QueryMonitor permission.</summary>
	public partial class FrmQueryFilters:FrmODBase {
		///<summary>After Save: the active group name, or "" when filtering is off.</summary>
		public string GroupNameActive="";
		///<summary>The rows as they were loaded, for Sync and the security log.</summary>
		private List<QueryFilter> _listQueryFiltersOld;
		///<summary>The edited rows of every group.</summary>
		private List<QueryFilter> _listQueryFilters;
		///<summary>Groups created in this window that have no filter yet. A group without rows is not saved.</summary>
		private List<string> _listGroupNamesEmpty=new List<string>();
		private string _activeGroupKey;
		private bool _isChanged;

		public FrmQueryFilters() {
			InitializeComponent();
			Load+=FrmQueryFilters_Load;
			FormClosing+=FrmQueryFilters_FormClosing;
			gridMain.CellDoubleClick+=gridMain_CellDoubleClick;
			comboGroup.SelectionChangeCommitted+=comboGroup_SelectionChangeCommitted;
			checkActive.Click+=(sender,e) => _isChanged=true;
			PreviewKeyDown+=FrmQueryFilters_PreviewKeyDown;
		}

		private void FrmQueryFilters_Load(object sender,EventArgs e) {
			Lang.F(this);
			_listQueryFiltersOld=QueryFilters.GetAll();
			_listQueryFilters=_listQueryFiltersOld.Select(x => x.Copy()).ToList();
			_activeGroupKey=QueryFilterActiveGroups.GetKey(Security.CurUser.UserNum);
			string groupNameActive=QueryFilterActiveGroups.GetActiveGroup(_activeGroupKey);
			List<string> listGroupNames=GetAllGroupNames();
			string groupNameSelect=listGroupNames.FirstOrDefault(x => string.Equals(x,groupNameActive,StringComparison.OrdinalIgnoreCase));
			checkActive.Checked=(groupNameSelect!=null);
			FillCombo(groupNameSelect??listGroupNames.FirstOrDefault());
		}

		///<summary>Every group name: the ones with rows and the new empty ones, sorted ignoring case.</summary>
		private List<string> GetAllGroupNames() {
			return QueryFilters.GetGroupNames(_listQueryFilters).Concat(_listGroupNamesEmpty)
				.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList();
		}

		private string GetSelectedGroupName() {
			if(comboGroup.SelectedIndex<0) {
				return "";
			}
			return comboGroup.Items.GetObjectAt(comboGroup.SelectedIndex) as string??"";
		}

		private void FillCombo(string groupNameSelect) {
			List<string> listGroupNames=GetAllGroupNames();
			comboGroup.Items.Clear();
			comboGroup.Items.AddList(listGroupNames,x => x);
			int index=listGroupNames.FindIndex(x => string.Equals(x,groupNameSelect,StringComparison.OrdinalIgnoreCase));
			if(index<0 && listGroupNames.Count>0) {
				index=0;
			}
			if(index>=0) {
				comboGroup.SelectedIndex=index;
			}
			bool hasGroup=(listGroupNames.Count>0);
			butRenameGroup.IsEnabled=hasGroup;
			butAdd.IsEnabled=hasGroup;
			butDelete.IsEnabled=hasGroup;
			butExport.IsEnabled=hasGroup;
			checkActive.IsEnabled=hasGroup;
			FillGrid();
		}

		private void FillGrid() {
			string groupName=GetSelectedGroupName();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lang.g("TableQueryFilters","Filter Text"),100){ IsWidthDynamic=true });
			gridMain.ListGridRows.Clear();
			List<QueryFilter> listQueryFiltersGroup=GetRowsForGroup(groupName);
			for(int i=0;i<listQueryFiltersGroup.Count;i++) {
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(listQueryFiltersGroup[i].FilterText);
				gridRow.Tag=listQueryFiltersGroup[i];
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
		}

		private List<QueryFilter> GetRowsForGroup(string groupName) {
			if(string.IsNullOrEmpty(groupName)) {
				return new List<QueryFilter>();
			}
			return _listQueryFilters.FindAll(x => string.Equals(x.GroupName,groupName,StringComparison.OrdinalIgnoreCase));
		}

		private void comboGroup_SelectionChangeCommitted(object sender,EventArgs e) {
			FillGrid();
		}

		///<summary>Asks for a filter text. Returns null when the user cancels or the text is blank or already in the group (after a message).
		///</summary>
		private string PromptFilterText(string groupName,string textDefault,QueryFilter queryFilterEditing) {
			InputBox inputBox=new InputBox(Lang.g(this,"Filter text (a query containing this text is hidden)"),textDefault);
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return null;
			}
			string filterText=QueryFilters.CleanFilterText(inputBox.StringResult);
			if(filterText=="") {
				MsgBox.Show(this,"Filter text cannot be blank.");
				return null;
			}
			if(GetRowsForGroup(groupName).Any(x => x!=queryFilterEditing && string.Equals(x.FilterText,filterText,StringComparison.OrdinalIgnoreCase))) {
				MsgBox.Show(this,"That filter is already in this group.");
				return null;
			}
			return filterText;
		}

		private void butAdd_Click(object sender,EventArgs e) {
			string groupName=GetSelectedGroupName();
			if(groupName=="") {
				MsgBox.Show(this,"Create a group first.");
				return;
			}
			string filterText=PromptFilterText(groupName,"",null);
			if(filterText==null) {
				return;
			}
			QueryFilter queryFilter=new QueryFilter();
			queryFilter.GroupName=groupName;
			queryFilter.FilterText=filterText;
			_listQueryFilters.Add(queryFilter);
			_listGroupNamesEmpty.RemoveAll(x => string.Equals(x,groupName,StringComparison.OrdinalIgnoreCase));
			_isChanged=true;
			FillGrid();
		}

		private void gridMain_CellDoubleClick(object sender,GridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridMain.ListGridRows.Count) {
				return;
			}
			QueryFilter queryFilter=(QueryFilter)gridMain.ListGridRows[e.Row].Tag;
			string filterText=PromptFilterText(queryFilter.GroupName,queryFilter.FilterText,queryFilter);
			if(filterText==null || filterText==queryFilter.FilterText) {
				return;
			}
			queryFilter.FilterText=filterText;
			_isChanged=true;
			FillGrid();
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length==0) {
				MsgBox.Show(this,"Select at least one filter.");
				return;
			}
			string groupName=GetSelectedGroupName();
			List<QueryFilter> listQueryFiltersSelected=gridMain.SelectedIndices.Select(x => (QueryFilter)gridMain.ListGridRows[x].Tag).ToList();
			bool isWholeGroup=(listQueryFiltersSelected.Count==GetRowsForGroup(groupName).Count);
			if(isWholeGroup && !MsgBox.Show(this,MsgBoxButtons.YesNo,"Deleting every filter removes the group when you save. Continue?")) {
				return;
			}
			_listQueryFilters.RemoveAll(x => listQueryFiltersSelected.Contains(x));
			_isChanged=true;
			if(isWholeGroup) {
				FillCombo("");
				return;
			}
			FillGrid();
		}

		private void butNewGroup_Click(object sender,EventArgs e) {
			string groupName=PromptGroupName("","");
			if(groupName==null) {
				return;
			}
			_listGroupNamesEmpty.Add(groupName);
			_isChanged=true;
			FillCombo(groupName);
		}

		private void butRenameGroup_Click(object sender,EventArgs e) {
			string groupNameOld=GetSelectedGroupName();
			if(groupNameOld=="") {
				return;
			}
			string groupNameNew=PromptGroupName(groupNameOld,groupNameOld);
			if(groupNameNew==null || groupNameNew==groupNameOld) {
				return;
			}
			QueryFilters.RenameGroup(_listQueryFilters,groupNameOld,groupNameNew);
			for(int i=0;i<_listGroupNamesEmpty.Count;i++) {
				if(string.Equals(_listGroupNamesEmpty[i],groupNameOld,StringComparison.OrdinalIgnoreCase)) {
					_listGroupNamesEmpty[i]=groupNameNew;
				}
			}
			_isChanged=true;
			FillCombo(groupNameNew);
		}

		///<summary>Asks for a group name. Returns null when the user cancels, or the name is blank or used by another group (after a message).
		///groupNameCurrent is the group being renamed, or "" for a new group.</summary>
		private string PromptGroupName(string textDefault,string groupNameCurrent) {
			InputBox inputBox=new InputBox(Lang.g(this,"Group name"),textDefault);
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return null;
			}
			string groupName=QueryFilters.CleanGroupName(inputBox.StringResult);
			if(groupName=="") {
				MsgBox.Show(this,"Group name cannot be blank.");
				return null;
			}
			if(GetAllGroupNames().Any(x => string.Equals(x,groupName,StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(x,groupNameCurrent,StringComparison.OrdinalIgnoreCase)))
			{
				MsgBox.Show(this,"Another group already has that name.");
				return null;
			}
			return groupName;
		}

		private void butImport_Click(object sender,EventArgs e) {
			OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Filter="Text files (*.txt)|*.txt|All files (*.*)|*.*";
			string initDir=PrefC.GetString(PrefName.ExportPath);
			if(Directory.Exists(initDir)) {
				openFileDialog.InitialDirectory=initDir;
			}
			if(openFileDialog.ShowDialog()!=true) {
				return;
			}
			string text;
			try {
				text=File.ReadAllText(openFileDialog.FileName,Encoding.UTF8);
			}
			catch(Exception ex) {
				MsgBox.Show(Lang.g(this,"Could not read the file")+":\r\n"+ex.Message);
				return;
			}
			if(!QueryFilters.TryParseImportText(text,out string groupNameFile,out List<string> listFilterTexts,out string error)) {
				MsgBox.Show(Lang.g(this,"Invalid file format.")+"\r\n"+Lang.g(this,error));
				return;
			}
			string groupName=QueryFilters.GetUniqueGroupName(groupNameFile,GetAllGroupNames());
			for(int i=0;i<listFilterTexts.Count;i++) {
				QueryFilter queryFilter=new QueryFilter();
				queryFilter.GroupName=groupName;
				queryFilter.FilterText=listFilterTexts[i];
				_listQueryFilters.Add(queryFilter);
			}
			_isChanged=true;
			FillCombo(groupName);
			MsgBox.Show(Lang.g(this,"Imported group")+": "+groupName);
		}

		private void butExport_Click(object sender,EventArgs e) {
			string groupName=GetSelectedGroupName();
			List<QueryFilter> listQueryFiltersGroup=GetRowsForGroup(groupName);
			if(listQueryFiltersGroup.Count==0) {
				MsgBox.Show(this,"The selected group has no filters to export.");
				return;
			}
			SaveFileDialog saveFileDialog=new SaveFileDialog();
			saveFileDialog.Filter="Text files (*.txt)|*.txt|All files (*.*)|*.*";
			string initDir=PrefC.GetString(PrefName.ExportPath);
			if(Directory.Exists(initDir)) {
				saveFileDialog.InitialDirectory=initDir;
			}
			string fileName=new string(groupName.Select(x => Path.GetInvalidFileNameChars().Contains(x) ? '_' : x).ToArray());
			saveFileDialog.FileName="QueryFilters_"+fileName+".txt";
			if(saveFileDialog.ShowDialog()!=true) {
				return;
			}
			try {
				File.WriteAllText(saveFileDialog.FileName,QueryFilters.ExportGroupToText(groupName,listQueryFiltersGroup.Select(x => x.FilterText)),
					new UTF8Encoding(false));
			}
			catch(Exception ex) {
				MsgBox.Show(Lang.g(this,"Could not write the file")+":\r\n"+ex.Message);
				return;
			}
			MsgBox.Show(this,"Exported");
		}

		private void FrmQueryFilters_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
			if(butAdd.IsAltKey(Key.A,e)) {
				butAdd_Click(this,new EventArgs());
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			List<QueryFilter> listQueryFiltersNew=QueryFilters.NormalizeForSave(_listQueryFilters);
			List<string> listGroupNamesChanged=QueryFilters.GetChangedGroupNames(listQueryFiltersNew,_listQueryFiltersOld);
			try {
				QueryFilters.Sync(listQueryFiltersNew,_listQueryFiltersOld);
			}
			catch(Exception ex) {
				MsgBox.Show(Lang.g(this,"Could not save the filters")+":\r\n"+ex.Message);
				return;
			}
			for(int i=0;i<listGroupNamesChanged.Count;i++) {
				SecurityLogs.MakeLogEntry(EnumPermType.QueryMonitor,0,QueryFilters.GetLogText(listGroupNamesChanged[i]));
			}
			string groupNameSelected=GetSelectedGroupName();
			GroupNameActive="";
			if(checkActive.Checked==true && QueryFilters.GetFilterTextsForGroup(listQueryFiltersNew,groupNameSelected).Count>0) {
				GroupNameActive=groupNameSelected;
			}
			if(!QueryFilterActiveGroups.SetActiveGroup(_activeGroupKey,GroupNameActive)) {
				MsgBox.Show(this,"The filters were saved, but the active group could not be remembered on this computer.");
			}
			_isChanged=false;
			IsDialogOK=true;
		}

		private void butClose_Click(object sender,EventArgs e) {
			IsDialogCancel=true;
		}

		private void FrmQueryFilters_FormClosing(object sender,CancelEventArgs e) {
			if(IsDialogOK || !_isChanged) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Discard unsaved changes?")) {
				e.Cancel=true;
			}
		}
	}
}
