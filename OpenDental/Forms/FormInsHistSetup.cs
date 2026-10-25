using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Insurance History. From Open Dental 26.1 the rows come from code groups marked Show In History (InsHistCategories), one per group and
	///four for a Perio Four Quadrants group, in code group order. Before 26.1 there were ten fixed rows whose codes came from preferences.</summary>
	public partial class FormInsHistSetup:FormODBase {
		private Patient _patient;
		///<summary>EO and C procedures associated with the InsHist and Received claimprocs for the inssub passed into the constructor.</summary>
		private List<Procedure> _listProceduresEoAndC;
		///<summary>InsHist and Received claimprocs associated with EO and C procedures for the inssub passed into the constructor.</summary>
		private List<ClaimProc> _listClaimProcsForEoAndCProcs;
		private InsSub _insSub;
		private const string NO_INSHIST="No History";
		///<summary>The rows, built from the code group cache when the window is created.</summary>
		private List<InsHistCategory> _listInsHistCategories;
		///<summary>One date box per row, in the order of _listInsHistCategories. Each box's Tag is its InsHistCategory.</summary>
		private List<TextBox> _listTextBoxes=new List<TextBox>();
		private const int ROW_TOP=51;
		private const int ROW_HEIGHT=22;

		//1. Find claiprocs for current plan
		//2. Find EO and C procs for claimprocs on current plan
		//3. Only have procs on this insplan that are EO and C
		//4. Fill in date info based on EO or C proc list
		//5. On OK click, fetch all claimprocs for EO and C procs
			//5a. Date filled, no matching EO or C proc - User entered date themselves, make new proc with claimproc status inshist
			//5b. Date filled, matching EO or C proc and user changed the date.-
				//Modify proc and claimproc for that category ONLY if the proc has a status of EO to be the date specified
				//If the proc has a status of C and the new date is greater than the the current ProcDate, create a new EO procedure and InsHist ClaimProc with the new date entered.
			//5c. No date, no proc - Do nothing
			//5d. No date, matching EO or C proc - User erased the date, delete proc/claimproc ONLY if proc has a status of EO.

		public FormInsHistSetup(long patNum,InsSub insSub) {
			InitializeComponent();
			AddCategoryRows();//Before the layout manager, so the rows are scaled like controls from the designer.
			InitializeLayoutManager();
			Lan.F(this);
			_patient=Patients.GetPat(patNum);
			_insSub=insSub;
		}

		///<summary>Adds a label and a date box for each row, or a note when no code group is marked Show In History, and sizes the window to fit.</summary>
		private void AddCategoryRows() {
			_listInsHistCategories=InsHistCategories.GetAll();
			int y=ROW_TOP;
			for(int i=0;i<_listInsHistCategories.Count;i++) {
				Label label=new Label();
				label.Location=new Point(7,y+2);
				label.Size=new Size(138,17);
				label.Text=_listInsHistCategories[i].Description;
				label.TextAlign=ContentAlignment.MiddleRight;
				label.Name="labelInsHist"+i;
				Controls.Add(label);
				TextBox textBox=new TextBox();
				textBox.Location=new Point(146,y);
				textBox.Size=new Size(96,20);
				textBox.Name="textDateInsHist"+i;
				textBox.TabIndex=i;
				textBox.Tag=_listInsHistCategories[i];
				textBox.Validating+=TextBoxValidating;
				Controls.Add(textBox);
				_listTextBoxes.Add(textBox);
				y+=ROW_HEIGHT;
			}
			if(_listInsHistCategories.Count==0) {
				Label labelNone=new Label();
				labelNone.Location=new Point(12,y);
				labelNone.Size=new Size(379,33);
				labelNone.Text=Lan.g(this,"No code group is marked Show In History. Set up the rows in Setup, Code Groups.");
				labelNone.Name="labelNoCategories";
				Controls.Add(labelNone);
				y+=40;
			}
			butSave.TabIndex=_listInsHistCategories.Count;
			butSave.Location=new Point(306,y+2);
			ClientSize=new Size(393,y+38);
		}

		private void FormInsHistSetup_Load(object sender,EventArgs e) {
			//Get all EO and C procedures for the patient.
			_listProceduresEoAndC=Procedures.GetProcsByStatusForPat(_patient.PatNum,ProcStat.EO,ProcStat.C);
			//Get all claimprocs for the EO and C procedures.
			List<long> listProcNums=_listProceduresEoAndC.Select(x => x.ProcNum).ToList();
			List<ClaimProc> listClaimProcs=ClaimProcs.GetForProcs(listProcNums);
			//Find all of the claimprocs associated with this inssub.
			_listClaimProcsForEoAndCProcs=new List<ClaimProc>();
			listProcNums.Clear();
			for(int i=0;i<listClaimProcs.Count;i++) {
				if(listClaimProcs[i].InsSubNum!=_insSub.InsSubNum) {
					continue;
				}
				if(!listClaimProcs[i].Status.In(ClaimProcStatus.InsHist,ClaimProcStatus.Received)) {
					continue;
				}
				_listClaimProcsForEoAndCProcs.Add(listClaimProcs[i]);
				listProcNums.Add(listClaimProcs[i].ProcNum);
			}
			//Remove all procedures that are not associate with this inssub.
			_listProceduresEoAndC.RemoveAll(x => !x.ProcNum.In(listProcNums.ToArray()));
			//Show the most recent history date of each row for this inssub.
			for(int i=0;i<_listTextBoxes.Count;i++) {
				Procedure procedure=InsHistCategories.GetMostRecentProc(_listProceduresEoAndC,_listInsHistCategories[i]);
				if(procedure!=null && procedure.ProcDate.Year > 1880) {
					_listTextBoxes[i].Text=procedure.ProcDate.ToShortDateString();
				}
				else {
					_listTextBoxes[i].Text=Lan.g(this,NO_INSHIST);
				}
			}
		}

		///<summary>True when the box holds no date to save: empty or the "No History" text.</summary>
		private bool IsNoHistory(TextBox textBox) {
			string text=textBox.Text.Trim();
			return text==NO_INSHIST || text==Lan.g(this,NO_INSHIST);
		}

		///<summary></summary>
		private bool IsValid() {
			DateTime dateEntry;
			for(int i=0;i<_listTextBoxes.Count;i++){
				TextBox textBox=_listTextBoxes[i];
				//Continue if no date is entered or the date entered is valid.
				if(!textBox.Enabled || string.IsNullOrEmpty(textBox.Text) || IsNoHistory(textBox)) {
					continue;
				}
				if(!DateTime.TryParse(textBox.Text,out dateEntry)) {
					//Invalid date entered.
					MsgBox.Show(this,"Invalid date");
					return false;
				}
				if(dateEntry.Year<1880 || dateEntry.Year>2100) {
					MsgBox.Show("Invalid date. Valid dates between 1880 and 2100");
					return false;
				}
			}
			return true;
		}

		private void TextBoxValidating(object sender,System.ComponentModel.CancelEventArgs e) {
			if(sender.GetType()!=typeof(TextBox)) {
				return;
			}
			TextBox textBox=(TextBox)sender;
			//If its disabled, empty or the default text return.
			if(!textBox.Enabled || string.IsNullOrEmpty(textBox.Text) || IsNoHistory(textBox)) {
				return;
			}
			bool allNums=true;
			for(int i=0;i<textBox.Text.Length;i++) {
				if(!Char.IsNumber(textBox.Text,i)) {
					allNums=false;
				}
			}
			if(CultureInfo.CurrentCulture.TwoLetterISOLanguageName=="en") {
				if(allNums) {
					if(textBox.Text.Length==6) {
						textBox.Text=textBox.Text.Substring(0,2)+"/"+textBox.Text.Substring(2,2)+"/"+textBox.Text.Substring(4,2);
					}
					else if(textBox.Text.Length==8) {
						textBox.Text=textBox.Text.Substring(0,2)+"/"+textBox.Text.Substring(2,2)+"/"+textBox.Text.Substring(4,4);
					}
				}
			}
			try {
				textBox.Text=DateTime.Parse(textBox.Text).ToString("d");//will throw exception if invalid
			}
			catch(Exception ex) {
				//We don't want a full exception, just a popup.  OK_Click will block them from putting invalid data in the db.
				ex.DoNothing();
				MsgBox.Show(this,"Invalid date.");
				return;
			}
			if(DateTime.Parse(textBox.Text).Year<1880 || DateTime.Parse(textBox.Text).Year>2100) {
				MsgBox.Show("Valid dates between 1880 and 2100");
				return;
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!IsValid()) {
				return;
			}
			List<string> listInvalidCodes=new List<string>();
			List<Procedure> listProceduresToDelete=new List<Procedure>();
			for(int i=0;i<_listTextBoxes.Count;i++){
				TextBox textBox=_listTextBoxes[i];
				InsHistCategory insHistCategory=_listInsHistCategories[i];
				if(!textBox.Enabled || IsNoHistory(textBox)) {
					continue;
				}
				Procedure procedure=InsHistCategories.GetMostRecentProc(_listProceduresEoAndC,insHistCategory);
				if(string.IsNullOrWhiteSpace(textBox.Text)) {
					if(procedure!=null && procedure.ProcStatus==ProcStat.EO && !listProceduresToDelete.Contains(procedure)) {//Only delete EO procedures
						listProceduresToDelete.Add(procedure);//Delete proc if user deleted procedure date from textbox.
					}
					continue;
				}
				DateTime dateEntered=PIn.Date(textBox.Text);
				List<ClaimProc> listClaimProcs=new List<ClaimProc>();
				if(procedure!=null) {
					//Get all of the claimprocs for this procedure.
					listClaimProcs=_listClaimProcsForEoAndCProcs.FindAll(x => x.ProcNum==procedure.ProcNum);
				}
				InsHistSaveResult insHistSaveResult=InsHistCategories.InsertOrUpdateProcedure(_patient,insHistCategory,dateEntered,_insSub.PlanNum,
					_insSub.InsSubNum,procedure,listClaimProcs);
				if(insHistSaveResult==InsHistSaveResult.InvalidCode) {
					listInvalidCodes.Add(insHistCategory.Description);//Don't create an Existing Other procedure log for an invalid code
				}
			}
			if(listProceduresToDelete.Count>0
				&& !MsgBox.Show(this,MsgBoxButtons.YesNo,"Deleting the last procedure date for a category will delete the Existing Other procedure with that date for this patient.  Continue?"))
			{
				return;
			}
			for(int i=0;i<listProceduresToDelete.Count;i++){
				try {
					Procedures.Delete(listProceduresToDelete[i].ProcNum);
				}
				catch(Exception ex) {
					//Tried deleting the procedure. Do nothing.
					ex.DoNothing();
				}
			}
			if(listInvalidCodes.Count>0) {
				MessageBox.Show(this,Lan.g(this,"These rows have no valid History Proc Code, so their dates were not saved. Fix the code in Setup, Code Groups:")
					+"\r\n"+string.Join("\r\n",listInvalidCodes));
			}
			DialogResult=DialogResult.OK;
		}

	}
}
