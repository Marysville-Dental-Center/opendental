using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace OpenDental {
	///<summary>Shows the current version, the update history and the update setup. It used to check Open Dental's servers for new versions and
	///download Setup.exe from them. That was removed: this build never contacts Open Dental's servers. Install new versions manually; other
	///workstations still update themselves from the files stored in this database (see Setup, Recopy).</summary>
	public partial class FormUpdate : FormODBase {

		///<summary></summary>
		public FormUpdate() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormUpdate_Load(object sender, System.EventArgs e) {
			if(ODEnvironment.IsCloudServer) {
				MsgBox.Show(this,"Updates are not allowed manually from within the program. Please call support.");
				Close();
				return;
			}
			LayoutMenu();
			labelVersion.Text=Lan.g(this,"Using Version:")+" "+Application.ProductVersion;
			UpdateHistory updateHistory=UpdateHistories.GetForVersion(Application.ProductVersion);
			if(updateHistory!=null) {
				labelVersion.Text+="  "+Lan.g(this,"Since")+": "+updateHistory.DateTimeUpdated.ToShortDateString();
			}
			//Checking for, downloading and installing versions from Open Dental's servers is not available.
			panelClassic.Visible=false;
			butCheck2.Visible=false;
			textConnectionMessage.Text=Lan.g(this,"Checking for updates with Open Dental is not available in this build. Install new versions manually.")
				+"\r\n"+Lan.g(this,"After a new version is installed on one computer, the other computers update themselves from this database.");
		}

		private void LayoutMenu() {
			menuMain.BeginUpdate();
			menuMain.Add(new MenuItemOD("Setup",menuItemSetup_Click));
			menuMain.EndUpdate();
		}

		private void menuItemSetup_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.UpdateInstall)) {
				return;
			}
			using FormUpdateSetup formUpdateSetup=new FormUpdateSetup();
			formUpdateSetup.ShowDialog();
		}

		private void ShowNotAvailable() {
			MessageBox.Show(Lan.g(this,"Checking for and downloading updates from Open Dental is not available in this build."));
		}

		private void butCheckForUpdates_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butShowUpdateHistory_Click(object sender,EventArgs e) {
			using FormUpdateHistory formUpdateHistory=new FormUpdateHistory();
			formUpdateHistory.ShowDialog();
		}

		#region Installs and downloads. The buttons stay hidden because nothing is ever checked for.

		private void butInstallBuild_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butInstallStable_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butInstallBeta_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butInstallAlpha_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void checkAcknowledgeBeta_CheckedChanged(object sender,EventArgs e) {
		}

		private void butDownMsiBuild_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butDownloadMsiStable_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butDownloadMsiBeta_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butDownloadMsiAlpha_Click(object sender,EventArgs e) {
			ShowNotAvailable();
		}

		private void butCheck_Click(object sender, System.EventArgs e) {
			ShowNotAvailable();
		}

		private void butDownload_Click(object sender, System.EventArgs e) {
			ShowNotAvailable();
		}

		#endregion

		///<summary>No longer used here. Moved to FormAbout.</summary>
		private void butLicense_Click(object sender,EventArgs e) {
			using FormLicense formLicense=new FormLicense();
			formLicense.ShowDialog();
		}

		private void FormUpdate_FormClosing(object sender,FormClosingEventArgs e) {
		}

	}
}
