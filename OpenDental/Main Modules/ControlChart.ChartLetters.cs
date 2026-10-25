using System;
using System.Data;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Chart letters (24.4) in the Progress Notes: the right-click menu items, double-click to open a read-only copy, and refreshing when a
	///letter changes, including when a save in the editor writes a new version in the background.</summary>
	public partial class ControlChart {
		private MenuItem _menuItemChartLetterSeparator;
		private MenuItem _menuItemChartLetterNew;
		private MenuItem _menuItemChartLetterEdit;
		private MenuItem _menuItemChartLetterProperties;
		private MenuItem _menuItemChartLetterDelete;

		///<summary>Adds the chart letter items to the Progress Notes right-click menu. Called once at startup.</summary>
		private void ChartLetters_InitializeOnStartup() {
			if(_menuItemChartLetterNew!=null) {
				return;
			}
			_menuItemChartLetterSeparator=new MenuItem("-");
			_menuItemChartLetterNew=new MenuItem(Lan.g(this,"New Chart Letter..."),menuItemChartLetterNew_Click);
			_menuItemChartLetterEdit=new MenuItem(Lan.g(this,"Edit Document"),menuItemChartLetterEdit_Click);
			_menuItemChartLetterProperties=new MenuItem(Lan.g(this,"Properties"),menuItemChartLetterProperties_Click);
			_menuItemChartLetterDelete=new MenuItem(Lan.g(this,"Delete Chart Letter"),menuItemChartLetterDelete_Click);
			menuProgRight.MenuItems.AddRange(new MenuItem[] {
				_menuItemChartLetterSeparator,
				_menuItemChartLetterNew,
				_menuItemChartLetterEdit,
				_menuItemChartLetterProperties,
				_menuItemChartLetterDelete,
			});
			ChartLetterL.ChartLetterChanged+=ChartLetterL_ChartLetterChanged;
			Disposed+=(sender,e) => ChartLetterL.ChartLetterChanged-=ChartLetterL_ChartLetterChanged;
			//Letters still open in an editor when the program closes get a warning, and their pending saves are stored. If the module is not on
			//the main window yet, ChartLetters_CheckOrphanedEdits does this the first time a patient is shown.
			ChartLetterL.WatchProgramClosing(FindForm());
		}

		///<summary>After the module shows a patient, offers to store saves made into working copies of the patient's chart letters after their edit
		///session ended (the program was closed or ended while the editor still had the letter open). Runs once the module has finished loading.
		///</summary>
		private void ChartLetters_CheckOrphanedEdits() {
			if(_menuItemChartLetterNew==null || IsPatientNull() || !IsHandleCreated) {
				return;
			}
			ChartLetterL.WatchProgramClosing(FindForm());//Only the first call with a window counts.
			long patNum=Pd.PatNum;
			BeginInvoke((Action)(() => {
				if(IsDisposed || !_isModuleSelected || IsPatientNull() || Pd.PatNum!=patNum) {
					return;
				}
				ChartLetterL.RecoverOrphanedEdits(Pd.Patient);
			}));
		}

		///<summary>Shows the chart letter items that apply to the selection: New always (with a patient), the others only for one selected chart
		///letter row.</summary>
		private void ChartLetters_MenuPopup() {
			if(_menuItemChartLetterNew==null) {
				return;
			}
			bool hasPatient=!IsPatientNull();
			bool isLetterSelected=hasPatient && GetSelectedChartLetterDocNum()!=0;
			_menuItemChartLetterSeparator.Visible=hasPatient;
			_menuItemChartLetterNew.Visible=hasPatient;
			_menuItemChartLetterNew.Enabled=!checkAudit.Checked;
			_menuItemChartLetterEdit.Visible=isLetterSelected;
			_menuItemChartLetterEdit.Enabled=!checkAudit.Checked;
			_menuItemChartLetterProperties.Visible=isLetterSelected;
			_menuItemChartLetterDelete.Visible=isLetterSelected;
			_menuItemChartLetterDelete.Enabled=!checkAudit.Checked;
		}

		///<summary>The DocNum of the one selected progress notes row if it is a chart letter, otherwise 0.</summary>
		private long GetSelectedChartLetterDocNum() {
			if(gridProg.SelectedIndices.Length!=1) {
				return 0;
			}
			DataRow dataRow=(DataRow)gridProg.ListGridRows[gridProg.SelectedIndices[0]].Tag;
			if(ChartModules.GetRowType(dataRow,out long docNum)!=ProgNotesRowType.ChartLetter) {
				return 0;
			}
			return docNum;
		}

		private Document GetSelectedChartLetter() {
			long docNum=GetSelectedChartLetterDocNum();
			if(docNum==0) {
				return null;
			}
			Document document=Documents.GetByNum(docNum,doReturnNullIfNotFound:true);
			if(document==null) {
				MsgBox.Show(this,"The chart letter has been deleted by another user.");
			}
			return document;
		}

		///<summary>Double-click on a chart letter row opens a read-only copy.</summary>
		private void ChartLetters_OpenReadOnly(long docNum) {
			Document document=Documents.GetByNum(docNum,doReturnNullIfNotFound:true);
			if(document==null) {
				MsgBox.Show(this,"The chart letter has been deleted by another user.");
				return;
			}
			ChartLetterL.OpenReadOnly(document,Pd.Patient);
		}

		private void menuItemChartLetterNew_Click(object sender,EventArgs e) {
			if(IsPatientNull()) {
				return;
			}
			using FormLetterMerges formLetterMerges=new FormLetterMerges(Pd.Patient);
			formLetterMerges.IsChartLetterMode=true;
			formLetterMerges.ShowDialog();
		}

		private void menuItemChartLetterEdit_Click(object sender,EventArgs e) {
			Document document=GetSelectedChartLetter();
			if(document!=null) {
				ChartLetterL.StartEdit(document,Pd.Patient);
			}
		}

		private void menuItemChartLetterProperties_Click(object sender,EventArgs e) {
			Document document=GetSelectedChartLetter();
			if(document!=null) {
				ChartLetterL.ShowProperties(document,Pd.Patient);
			}
		}

		private void menuItemChartLetterDelete_Click(object sender,EventArgs e) {
			Document document=GetSelectedChartLetter();
			if(document!=null) {
				ChartLetterL.Delete(document);
			}
		}

		///<summary>Refreshes the progress notes when a letter of the patient showing changes. May be raised on a background thread.</summary>
		private void ChartLetterL_ChartLetterChanged(object sender,long patNum) {
			if(IsDisposed || !IsHandleCreated) {
				return;
			}
			BeginInvoke((Action)(() => {
				if(!_isModuleSelected || IsPatientNull() || Pd.PatNum!=patNum) {
					return;
				}
				ModuleSelected(Pd.PatNum);
			}));
		}
	}
}
