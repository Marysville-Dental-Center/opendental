using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CodeBase;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary></summary>
	public partial class FrmWikiSetup:FrmODBase {
		///<summary>The master page as loaded, to tell whether Save changed it.</summary>
		private string _masterPageOld="";
		///<summary>The read-only copy settings need the Setup permission: the copy holds every wiki page and goes wherever the folder points.</summary>
		private bool _canEditReadOnlyCopy;

		public FrmWikiSetup() {
			InitializeComponent();
			Load+=FrmWikiSetup_Load;
			PreviewKeyDown+=FrmWikiSetup_PreviewKeyDown;
		}

		private void FrmWikiSetup_Load(object sender,EventArgs e) {
			Lang.F(this);
			textMaster.Text=WikiPages.WikiPageMaster.PageContent;
			_masterPageOld=textMaster.Text;
			checkDetectLinks.Checked=PrefC.GetBool(PrefName.WikiDetectLinks);
			checkCreatePageFromLinks.Checked=PrefC.GetBool(PrefName.WikiCreatePageFromLink);
			//Read-only copy of the wiki (v25_2-wiki-readonly-offline).
			string exportPath=WikiStaticExport.GetExportPath();
			checkReadOnly.Checked=(exportPath!="");
			textReadOnlyPath.Text=(exportPath!="" ? exportPath : WikiStaticExport.GetDefaultExportPath());
			checkReadOnlyAuto.Checked=WikiStaticExport.GetAutoExport();
			_canEditReadOnlyCopy=Security.IsAuthorized(EnumPermType.Setup,true);
			WikiExportResult wikiExportResult=WikiStaticExport.LastResult;
			if(wikiExportResult!=null) {
				labelReadOnlyStatus.Text=wikiExportResult.GetSummary();
			}
			SetReadOnlyControls();
		}

		private void FrmWikiSetup_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
		}

		private void SetReadOnlyControls() {
			bool isOn=(checkReadOnly.Checked==true);
			checkReadOnly.IsEnabled=_canEditReadOnlyCopy;
			textReadOnlyPath.IsEnabled=isOn && _canEditReadOnlyCopy;
			butReadOnlyBrowse.IsEnabled=isOn && _canEditReadOnlyCopy;
			checkReadOnlyAuto.IsEnabled=isOn && _canEditReadOnlyCopy;
			butExportNow.IsEnabled=isOn && _canEditReadOnlyCopy;
			butOpenCopy.IsEnabled=isOn;
			butShortcut.IsEnabled=isOn;
		}

		private void checkReadOnly_Click(object sender,EventArgs e) {
			if(checkReadOnly.Checked==true && textReadOnlyPath.Text.Trim()=="") {
				textReadOnlyPath.Text=WikiStaticExport.GetDefaultExportPath();
			}
			SetReadOnlyControls();
		}

		private void butReadOnlyBrowse_Click(object sender,EventArgs e) {
			using System.Windows.Forms.FolderBrowserDialog folderBrowserDialog=new System.Windows.Forms.FolderBrowserDialog();
			if(Directory.Exists(textReadOnlyPath.Text.Trim())) {
				folderBrowserDialog.SelectedPath=textReadOnlyPath.Text.Trim();
			}
			if(folderBrowserDialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK) {
				return;
			}
			textReadOnlyPath.Text=folderBrowserDialog.SelectedPath;
		}

		///<summary>Blank when the folder in the box can hold the read-only copy, otherwise why not.</summary>
		private string ValidateReadOnlyPath() {
			return WikiStaticExport.ValidateExportPath(textReadOnlyPath.Text,WikiStaticExport.GetWikiFolderOrNull(),WikiStaticExport.GetAtoZFolderOrNull());
		}

		private void butExportNow_Click(object sender,EventArgs e) {
			string error=ValidateReadOnlyPath();
			if(error!="") {
				MsgBox.Show(error);
				return;
			}
			string folder=textReadOnlyPath.Text.Trim();
			WikiExportResult wikiExportResult=null;
			OpenDental.UI.ProgressWin progressWin=new OpenDental.UI.ProgressWin();
			progressWin.ShowCancelButton=false;
			progressWin.StartingMessage=Lans.g(this,"Writing the read-only copy of the wiki...");
			progressWin.ActionMain=() => {
				wikiExportResult=WikiStaticExport.TryExport(folder,null,isFullExport:true,DateTime.Now);
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
				return;
			}
			if(wikiExportResult==null) {
				return;
			}
			labelReadOnlyStatus.Text=wikiExportResult.GetSummary();
			if(wikiExportResult.IsSuccess) {
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Wiki read-only copy exported to "+folder+" ("+wikiExportResult.CountPagesWritten+" pages).");
			}
			MsgBox.Show(wikiExportResult.GetSummary());
		}

		private void butOpenCopy_Click(object sender,EventArgs e) {
			string pathIndex=WikiStaticExport.GetIndexPath(textReadOnlyPath.Text.Trim());
			if(!File.Exists(pathIndex)) {
				MsgBox.Show(this,"There is no read-only copy in this folder yet. Click Export Now first.");
				return;
			}
			try {
				System.Diagnostics.Process.Start(pathIndex);
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
			}
		}

		///<summary>Puts an internet shortcut to index.html on this user's desktop, so staff can open the copy without this program.</summary>
		private void butShortcut_Click(object sender,EventArgs e) {
			string pathIndex=WikiStaticExport.GetIndexPath(textReadOnlyPath.Text.Trim());
			try {
				string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
				string pathShortcut=System.IO.Path.Combine(desktop,Lans.g(this,"Wiki (read-only copy)")+".url");
				File.WriteAllText(pathShortcut,"[InternetShortcut]\r\nURL="+WikiStaticExport.ToFileUri(pathIndex)+"\r\n");
				MsgBox.Show(Lans.g(this,"Shortcut created:")+" "+pathShortcut);
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			//Read-only copy settings are checked first, so a bad folder stops the save before anything is written.
			bool isReadOnlyOn=(checkReadOnly.Checked==true);
			string exportPath=(isReadOnlyOn ? textReadOnlyPath.Text.Trim() : "");
			if(_canEditReadOnlyCopy && isReadOnlyOn) {
				string error=ValidateReadOnlyPath();
				if(error!="") {
					MsgBox.Show(error);
					return;
				}
			}
			bool isReadOnlyChanged=false;
			if(_canEditReadOnlyCopy) {
				isReadOnlyChanged=WikiStaticExport.SaveSettings(exportPath,checkReadOnlyAuto.Checked==true);
				if(isReadOnlyChanged) {
					string logText=(exportPath=="" ? "Wiki read-only copy turned off."
						: "Wiki read-only copy folder set to "+exportPath+", update after each save "+(checkReadOnlyAuto.Checked==true ? "on." : "off."));
					SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,logText);
				}
			}
			//Prefs
			if(Prefs.UpdateBool(PrefName.WikiDetectLinks,(bool)checkDetectLinks.Checked)
				| Prefs.UpdateBool(PrefName.WikiCreatePageFromLink,(bool)checkCreatePageFromLinks.Checked)
				| isReadOnlyChanged)
			{
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			//Master Page
			WikiPage wikiPage=WikiPages.WikiPageMaster;
			wikiPage.PageContent=textMaster.Text;
			wikiPage.UserNum=Security.CurUser.UserNum;
			WikiPages.InsertAndArchive(wikiPage);
			DataValid.SetInvalid(InvalidType.Wiki);
			//Every page of the read-only copy uses the master page, and a new folder needs a first export.
			if(isReadOnlyChanged || textMaster.Text!=_masterPageOld) {
				WikiStaticExport.ExportInBackground(null,isFullExport:true);
			}
			IsDialogOK=true;
		}

	}
}