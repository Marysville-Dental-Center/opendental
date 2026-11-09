using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;

namespace OpenPmsDental {
	public partial class FormSnomeds:FormODBase {
		public bool IsSelectionMode;
		public bool IsMultiSelectMode;
		public Snomed SnomedSelected;
		public List<Snomed> ListSnomedsSelected;
		private List<Snomed> _listSnomeds;
		private bool _showingInfoButton;//used when filling grid. for increased speed.
		private int _showingInfobuttonShift;//used when sorting grid rows. 1 if showing, 0 if hidden

		public FormSnomeds() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSnomeds_Load(object sender,EventArgs e) {
			_showingInfoButton=CDSPermissions.GetForUser(Security.CurUser.UserNum).ShowInfobutton;
			_showingInfobuttonShift=(_showingInfoButton?1:0);
			if(!IsSelectionMode && !IsMultiSelectMode) {
				butOK.Visible=false;
			}
			if(IsMultiSelectMode) {
				gridMain.SelectionMode=GridSelectionMode.MultiExtended;
			}
			ActiveControl=textCode;
			//The One-Time Tools group (Map to ICD9). Open Dental showed it only to a provider with a valid EHR key, "to prevent Snomeds from being used in
			//non-member nations". Each office imports SNOMED CT under its own licence, so there is no member-country check (E-6 of
			//docs/specs/ehr-rebuild-plan.md): the group shows to users with the Setup permission whenever the snomed table has rows.
			//The count is a query (a Middle Tier call on a Middle Tier client): asked only of a user who could see the tools, since the window
			//also opens as a code picker.
			bool hasSetup=Security.IsAuthorized(EnumPermType.Setup,true);
			groupBox1.Visible=hasSetup && IsSnomedToolsVisible(hasSetup,Snomeds.GetCodeCount());
		}

		///<summary>True when the One-Time Tools group shows: the user has the Setup permission and the snomed table has rows. Pure.</summary>
		public static bool IsSnomedToolsVisible(bool hasSetup,long countSnomeds) {
			return hasSetup && countSnomeds>0;
		}
		
		private void butSearch_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col;
			if(_showingInfoButton) {//Security.IsAuthorized(Permissions.EhrInfoButton,true)) {
				col=new GridColumn("",18);//infoButton
				col.ImageList=imageListInfoButton;
				gridMain.Columns.Add(col);
			}
			col=new GridColumn(Lan.g(this,"SNOMED CT"),125);//column width of 125 holds the longest Snomed CT code as of 8/7/15 which is 900000000000002006
			gridMain.Columns.Add(col);
			//col=new ODGridColumn("Deprecated",75,HorizontalAlignment.Center);
			//gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Description"),500);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Used By CQM's"),185);//width 185 so all of our CQM measure nums as of 8/7/15 will fit 68,69,74,75,127,138,147,155,165
			gridMain.Columns.Add(col);
			//col=new ODGridColumn("Date Of Standard",100);
			//gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			GridRow row;
			if(textCode.Text.Contains(",")) {
				_listSnomeds=Snomeds.GetByCodes(textCode.Text);
			}
			else {
				_listSnomeds=Snomeds.GetByCodeOrDescription(textCode.Text);
			}
			if(_listSnomeds.Count>=10000) {//Max number of results returned.
				MsgBox.Show(this,"Too many results. Only the first 10,000 results will be shown.");
			}
			List<GridRow> listGridRowsAll=new List<GridRow>();
			for(int i=0;i<_listSnomeds.Count;i++) {
				row=new GridRow();
				if(_showingInfoButton) {//Security.IsAuthorized(Permissions.EhrInfoButton,true)) {
					row.Cells.Add("0");//index of infobutton
				}
				row.Cells.Add(_listSnomeds[i].SnomedCode);
				//row.Cells.Add("");//IsActive==NotDeprecated
				row.Cells.Add(_listSnomeds[i].Description);
				row.Cells.Add(EhrCodes.GetMeasureIdsForCode(_listSnomeds[i].SnomedCode,"SNOMEDCT"));
				row.Tag=_listSnomeds[i];
				//row.Cells.Add("");
				listGridRowsAll.Add(row);
			}
			listGridRowsAll.Sort(SortMeasuresMet);
			for(int i=0;i<listGridRowsAll.Count;i++) {
				gridMain.ListGridRows.Add(listGridRowsAll[i]);
			}
			gridMain.EndUpdate();
		}

		private void gridMain_CellClick(object sender,ODGridClickEventArgs e) {
			if(!CDSPermissions.GetForUser(Security.CurUser.UserNum).ShowInfobutton) {//Security.IsAuthorized(Permissions.EhrInfoButton,true)) {
				return;
			}
			if(e.Col!=0) {
				return;
			}
			List<KnowledgeRequest> listKnowledgeRequests=EhrTriggers.ConvertToKnowledgeRequests(Snomeds.GetByCode(gridMain.ListGridRows[e.Row].Cells[1].Text));
			using FormInfobutton formInfoButton=new FormInfobutton(listKnowledgeRequests);
			formInfoButton.ShowDialog();
		}

		///<summary>Sort function to put the codes that apply to the most number of CQM's at the top so the user can see which codes they should select.</summary>
		private int SortMeasuresMet(GridRow row1,GridRow row2) {
			//int i=(CDSPermissions.GetForUser(Security.CurUser.UserNum).ShowInfobutton?1:0);//used to accomodate infobutton column.
			//First sort by the number of measures the codes apply to in a comma delimited list
			int diff=row2.Cells[2+_showingInfobuttonShift].Text.Split(new string[] { "," },StringSplitOptions.RemoveEmptyEntries).Length
				-row1.Cells[2+_showingInfobuttonShift].Text.Split(new string[] { "," },StringSplitOptions.RemoveEmptyEntries).Length;
			if(diff!=0) {
				return diff;
			}
			try {
				//if the codes apply to the same number of CQMs, order by the code values
				//return PIn.Long(row1.Cells[2+_showingInfobuttonShift].Text).CompareTo(PIn.Long(row2.Cells[2+_showingInfobuttonShift].Text));
				//Just string compare
				return row1.Cells[2+_showingInfobuttonShift].Text.CompareTo(row2.Cells[2+_showingInfobuttonShift].Text);
			}
			catch(Exception) {
				return 0;
			}
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(!IsSelectionMode && !IsMultiSelectMode) {
				return;
			}
			SnomedSelected=(Snomed)gridMain.ListGridRows[e.Row].Tag;
			ListSnomedsSelected=new List<Snomed>();
			ListSnomedsSelected.Add((Snomed)gridMain.ListGridRows[e.Row].Tag);
			DialogResult=DialogResult.OK;
		}

		///<summary>Map to ICD9 changes every problem definition it can, so it needs ProblemDefEdit, as the problem windows do (section 5.1 of
		///docs/specs/ehr-rebuild-plan.md). The standard message comes before the confirmation; MapIcd9ToSnomed checks the permission again.</summary>
		private void butMapToSnomed_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.ProblemDefEdit)) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Will add SNOMED CT code to existing problems list only if the ICD9 code correlates to exactly one SNOMED CT code. If there is any ambiguity at all the code will not be added.")) {
				return;
			}
			int changeCount=MapIcd9ToSnomed(out string error);
			if(error!="") {
				MessageBox.Show(error);
				return;
			}
			MessageBox.Show(Lan.g(this,"SNOMED CT codes added: ")+changeCount);
		}

		///<summary>Adds the SNOMED CT code to every problem definition that has none and whose ICD-9 code maps to exactly one SNOMED CT code
		///(Snomeds.GetICD9toSNOMEDDictionary), as the button always did. Needs ProblemDefEdit, checked here without a message: without it, returns -1
		///with the reason and changes nothing. Writes one ProblemDefEdit security log entry with the count, and signals InvalidType.Diseases when a
		///definition changed. Returns the number of definitions changed.</summary>
		public static int MapIcd9ToSnomed(out string error) {
			string errorAuthorization="";
			if(!Security.IsAuthorized(EnumPermType.ProblemDefEdit,DateTime.MinValue,actionNotAuthorized:x => errorAuthorization=x)) {
				error=errorAuthorization;
				if(error=="") {
					error=Lan.g("FormSnomeds","Not authorized for")+" "+GroupPermissions.GetDesc(EnumPermType.ProblemDefEdit);
				}
				return -1;
			}
			error="";
			int changeCount=0;
			Dictionary<string,string> dictionaryIcd9ToSnomed = Snomeds.GetICD9toSNOMEDDictionary();
			//Jordan 2022-11-15-Dict ok as exception to normal patterns
			DiseaseDefs.RefreshCache();
			List<DiseaseDef> listDiseaseDefs=DiseaseDefs.GetWhere(x => x.SnomedCode=="" && dictionaryIcd9ToSnomed.ContainsKey(x.ICD9Code));
			for(int i=0;i<listDiseaseDefs.Count;i++) {
				listDiseaseDefs[i].SnomedCode=dictionaryIcd9ToSnomed[listDiseaseDefs[i].ICD9Code];
				DiseaseDefs.Update(listDiseaseDefs[i]);
				changeCount++;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.ProblemDefEdit,0,"Map to ICD9 (SNOMED CT window): SNOMED CT codes added to "+changeCount
				+" problem definitions from their ICD-9 codes.");
			if(changeCount>0) {
				DataValid.SetInvalid(InvalidType.Diseases);
			}
			return changeCount;
		}

		private void butOK_Click(object sender,EventArgs e) {
			//not even visible unless IsSelectionMode
			if(gridMain.GetSelectedIndex()==-1) {
				MsgBox.Show(this,"Please select an item first.");
				return;
			}
			SnomedSelected=(Snomed)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag;
			ListSnomedsSelected=new List<Snomed>();
			for(int i=0;i<gridMain.SelectedIndices.Length;i++) {
				ListSnomedsSelected.Add((Snomed)gridMain.ListGridRows[gridMain.SelectedIndices[i]].Tag);
			}
			DialogResult=DialogResult.OK;
		}

	}
}