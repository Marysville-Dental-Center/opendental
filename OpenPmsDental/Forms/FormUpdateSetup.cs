using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using CodeBase;
using System.Xml;
using System.Text;

namespace OpenPmsDental{
	///<summary>The update settings. There is no registration key here: Open Dental's registration keys were removed (docs/decoupling-plan.md), and the
	///RegistrationKey preference is left as it is.</summary>
	public partial class FormUpdateSetup : FormODBase {
		private DateTime _dateTimeUpdate;

		///<summary></summary>
		public FormUpdateSetup()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormUpdateSetup_Load(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin,true)) {
				butSave.Enabled=false;
			}
			textUpdateServerAddress.Text=PrefC.GetString(PrefName.UpdateServerAddress);
			textWebsitePath.Text=PrefC.GetString(PrefName.UpdateWebsitePath);
			textWebProxyAddress.Text=PrefC.GetString(PrefName.UpdateWebProxyAddress);
			textWebProxyUserName.Text=PrefC.GetString(PrefName.UpdateWebProxyUserName);
			textWebProxyPassword.Text=PrefC.GetString(PrefName.UpdateWebProxyPassword);
			textMultiple.Text=PrefC.GetString(PrefName.UpdateMultipleDatabases);
			checkShowMsi.Checked=PrefC.GetBool(PrefName.UpdateShowMsiButtons);
			_dateTimeUpdate=PrefC.GetDateT(PrefName.UpdateDateTime);
			textUpdateTime.Text=_dateTimeUpdate.ToString();
			if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
				labelRecopy.Text=@"Recopy all of the files from C:\Program Files (x86)\Open PMS - Dental\ into a special place in the database for future use in updating other computers.";
			}
			//These settings only pointed at Open Dental's update servers, which this build never contacts. Their preference values are left unchanged.
			label1.Visible=false;
			textUpdateServerAddress.Visible=false;
			label3.Visible=false;
			textWebsitePath.Visible=false;
			groupBox1.Visible=false;
			checkShowMsi.Visible=false;
			label10.Visible=false;
		}

		private void butRecopy_Click(object sender,EventArgs e) {
			//The compatibility version, not the release version ("0.0"): a version number passed on, never shown.
			Version versionCurrent=ForkVersions.CompatVersion;
			string folderUpdate=ODFileUtils.CombinePaths(PrefC.GetTempFolderPath(),"UpdateFiles");
			if(Directory.Exists(folderUpdate)) {
				try {
					Directory.Delete(folderUpdate,true);
				}
				catch {
					MsgBox.Show(this,"Recopy failed.  Please run as administrator then try again.");
					return;
				}
			}
			Cursor=Cursors.WaitCursor;
			if(!PrefL.CopyFromHereToUpdateFiles(versionCurrent,false,true,true)) {
				Cursor=Cursors.Default;
				return;
			}
			Cursor=Cursors.Default;
			MsgBox.Show(this,"Recopied.");
		}

		private void butChangeTime_Click(object sender,EventArgs e) {
			using FormTimePick formTimePick=new FormTimePick(true);
			if(_dateTimeUpdate!=DateTime.MinValue) {
				formTimePick.DateTimeSelected=_dateTimeUpdate;
			}
			formTimePick.ShowDialog();
			if(formTimePick.DialogResult!=DialogResult.OK) {
				return;
			}
			_dateTimeUpdate=formTimePick.DateTimeSelected;
			textUpdateTime.Text=_dateTimeUpdate.ToString();
			if(!Prefs.UpdateDateT(PrefName.UpdateDateTime,_dateTimeUpdate)) {
				return;
			}
			//Updating to db now in case the user does not have enough permission to click the OK button on this form.			
			Cursor=Cursors.WaitCursor;
			DataValid.SetInvalid(InvalidType.Prefs);
			Cursor=Cursors.Default;
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(textMultiple.Text.Contains(" ")) {
				MsgBox.Show(this,"No spaces allowed in the database list.");
				return;
			}
			bool isChanged=false;
			isChanged |= Prefs.UpdateString(PrefName.UpdateServerAddress,textUpdateServerAddress.Text);
			isChanged |= Prefs.UpdateBool(PrefName.UpdateShowMsiButtons,checkShowMsi.Checked);
			isChanged |= Prefs.UpdateString(PrefName.UpdateWebsitePath,textWebsitePath.Text);
			isChanged |= Prefs.UpdateString(PrefName.UpdateWebProxyAddress,textWebProxyAddress.Text);
			isChanged |= Prefs.UpdateString(PrefName.UpdateWebProxyUserName,textWebProxyUserName.Text);
			isChanged |= Prefs.UpdateString(PrefName.UpdateWebProxyPassword,textWebProxyPassword.Text);
			isChanged |= Prefs.UpdateString(PrefName.UpdateMultipleDatabases,textMultiple.Text);
			if(isChanged) {
				Cursor=Cursors.WaitCursor;
				DataValid.SetInvalid(InvalidType.Prefs);
				Cursor=Cursors.Default;
			}
			DialogResult=DialogResult.OK;
		}

		private void FormUpdateSetup_FormClosing(object sender,FormClosingEventArgs e) {
			EnumPermType perm=(DialogResult==DialogResult.OK ? EnumPermType.SecurityAdmin : EnumPermType.Setup);
			SecurityLogs.MakeLogEntry(perm,0,"Update Setup window accesssed.");
		}
	}
}