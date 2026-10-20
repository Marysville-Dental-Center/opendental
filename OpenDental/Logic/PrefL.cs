using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using CodeBase;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;
using OpenDental.UI;
using System.Net;
using OpenDentBusiness.Misc;

namespace OpenDental {
	///<summary>Startup version checks and the fork's release channel. Two meanings of "version" are kept apart (backlog Risk R1): the release
	///version ("0.0", AssemblyInformationalVersion, shown in About and in release manifests) and the Open Dental compatibility version (24.3.63.0,
	///ForkVersions.CompatVersion), which the legacy conversion chain and the DataBaseVersion and ProgramVersion preferences use until the fork markers
	///take over. Open Dental's update files (documentmisc UpdateFiles rows) are never read, and no downgrade is ever offered.</summary>
	public class PrefL {

		///<summary>True when this workstation is the update server: the WebServiceServerName preference is blank or names this computer. Only the
		///update server converts the database and publishes releases.</summary>
		public static bool IsUpdateServer() {
			string updateServerName=PrefC.GetString(PrefName.WebServiceServerName);
			return updateServerName=="" || ODEnvironment.IdIsThisComputer(updateServerName.ToLower());
		}

		#region Version guard
		///<summary>Runs at startup before any conversion, in Debug and Release builds alike (Open Dental skipped its version check in Debug builds;
		///the fork does not). Refuses unadopted Open Dental databases above 24.3.63, adopted databases whose markers disagree, and adopted databases
		///that a newer release has updated (after trying to pull that release). Normal startup never adopts: for a database on the adoption allowlist
		///it only offers to open the adoption tool. Returns false if the program must exit.</summary>
		public static bool CheckForkVersionGuard(Form form,bool isSilent) {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState());
			if(forkGuardResult.CanProceed) {
				return true;
			}
			if(forkGuardResult.Action==ForkGuardAction.NeedsNewerBuild && !isSilent) {
				ReleaseManifest releaseManifestPublished=ForkReleases.GetPublishedManifest(out string _);
				ReleaseIdentity releaseIdentityRunning=ForkReleases.GetRunningIdentity();
				if(!ODBuild.IsDebug() && releaseManifestPublished!=null && releaseManifestPublished.BuildId!=releaseIdentityRunning.BuildId
					&& ForkVersions.CompareReleaseVersions(releaseManifestPublished.ReleaseVersion,releaseIdentityRunning.ReleaseVersion)>=0)
				{
					MessageBox.Show(forkGuardResult.Message);
					PullRelease(releaseManifestPublished,releaseIdentityRunning,isSilent);
					return false;
				}
			}
			FormOpenDental.ExitCode=forkGuardResult.ExitCode;
			if(isSilent) {
				return false;
			}
			if(forkGuardResult.Action==ForkGuardAction.NeedsAdoption) {
				OfferAdoption(form,forkGuardResult);
				return false;//Adopted or not, the program starts again so the guard sees the new state.
			}
			MessageBox.Show(forkGuardResult.Message);
			return false;
		}

		///<summary>Explains why the database needs adoption and lets an administrator open the adoption tool. Nothing is changed unless the
		///administrator goes through the tool's own checks and confirmation.</summary>
		private static void OfferAdoption(Form form,ForkGuardResult forkGuardResult) {
			string message=forkGuardResult.Message+"\r\n\r\n"
				+Lan.g("Prefs","Open the adoption tool? Only adopt a restored copy, and only after taking a backup.");
			if(MessageBox.Show(message,"",MessageBoxButtons.YesNo)!=DialogResult.Yes) {
				return;
			}
			using FormAdoptDatabase formAdoptDatabase=new FormAdoptDatabase();
			if(formAdoptDatabase.ShowDialog()==DialogResult.OK) {
				MsgBox.Show("Prefs","The database was adopted. Start the program again to use it.");
			}
		}
		#endregion Version guard

		#region Conversion
		///<summary>This ONLY runs when first opening the program.  It returns true if either no conversion is necessary, or if conversion was successful.  False for other situations like corrupt db, trying to convert to older version, etc.  Silent mode is mostly used from internal tools.  It is currently used in the Main Program if the silent command line argument is set.
		///An adopted database never enters the frozen legacy chain: the fork migration runner (M1-01) takes over. Any other database is converted by
		///the legacy chain up to the compatibility version (24.3.63.0), never further.</summary>
		public static bool ConvertDB(bool isSilent,Form form,bool useDynamicMode) {
			bool isSuccess;
			if(ForkVersions.ReadState().IsForkMarked) {
				isSuccess=RunForkMigrations(isSilent);
			}
			else {
				ClassConvertDatabase classConvertDatabase=new ClassConvertDatabase();
				string prefDbVersion=PrefC.GetString(PrefName.DataBaseVersion);
				isSuccess=classConvertDatabase.Convert(prefDbVersion,ForkVersions.CompatVersion.ToString(4),isSilent,form,useDynamicMode);
			}
			if(isSuccess) {
				return true;
			}
			if(FormOpenDental.ExitCode==0) {
				FormOpenDental.ExitCode=200;//Convert Database has failed during execution (Unknown Error)
			}
			Environment.Exit(FormOpenDental.ExitCode);
			return false;
		}

		///<summary>This ONLY runs when first opening the program.  It returns true if either no conversion is necessary, or if conversion was successful.  False for other situations like corrupt db, trying to convert to older version, etc.</summary>
		public static bool ConvertDB(Form form,bool useDynamicMode) {
			return ConvertDB(isSilent: false,form,useDynamicMode);
		}

		///<summary>The hand-off from the frozen legacy chain: runs the pending fork migration steps of an adopted database. Only the update server
		///runs them, after a backup (Release builds), with the same in-progress flags as a conversion. Steps are idempotent, so a failed run can be
		///repeated. Returns false if the program must exit.</summary>
		private static bool RunForkMigrations(bool isSilent) {
			IForkMigrationRunner runner=ForkMigrationHandOff.Runner;
			ForkDbState forkDbState=ForkVersions.ReadState();
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState,runner);
			if(!forkGuardResult.CanProceed) {
				FormOpenDental.ExitCode=forkGuardResult.ExitCode;
				if(!isSilent) {
					MessageBox.Show(forkGuardResult.Message);
				}
				return false;
			}
			if(!forkGuardResult.HasPendingMigrations) {
				return true;
			}
			if(!IsUpdateServer()) {
				FormOpenDental.ExitCode=141;//Updates are only allowed from a designated web server
				if(!isSilent) {
					MessageBox.Show(Lan.g("Prefs","The database needs an update that only the update server can run:")+" "
						+PrefC.GetString(PrefName.WebServiceServerName));
				}
				return false;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				FormOpenDental.ExitCode=140;//Web client cannot convert database
				if(!isSilent) {
					MsgBox.Show("Prefs","Web client cannot convert database.  Must be using a direct connection.");
				}
				return false;
			}
			if(!isSilent && MessageBox.Show(Lan.g("Prefs","The database will now be updated")+"\r\n"
				+Lan.g("Prefs","from schema level")+" "+forkDbState.ForkSchemaLevel+" "+Lan.g("Prefs","to")+" "+runner.LatestLevel+".","",
				MessageBoxButtons.OKCancel)!=DialogResult.OK)
			{
				return false;
			}
			if(!ODBuild.IsDebug() && !Shared.MakeABackup(isSilent,BackupLocation.ConvertScript,false)) {
				FormOpenDental.ExitCode=101;//Database Backup failed
				return false;
			}
			Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,Environment.MachineName);
			Prefs.UpdateBool(PrefName.CorruptedDatabase,true);
			try {
				if(isSilent) {
					runner.RunPending(x => ODEvent.Fire(ODEventType.ConvertDatabases,x));
				}
				else {
					ProgressWin progressWin=new ProgressWin();
					progressWin.ActionMain=() => runner.RunPending(x => ODEvent.Fire(ODEventType.ConvertDatabases,x));
					progressWin.ShowCancelButton=false;
					progressWin.ShowDialog();
				}
			}
			catch(Exception ex) {
				FormOpenDental.ExitCode=ForkVersionGuard.EXIT_CODE_MIGRATION_FAILED;
				if(!isSilent) {
					FriendlyException.Show(Lan.g("Prefs","A database update step failed. The steps can be run again; restore the backup if it fails again."),ex);
				}
				return false;
			}
			finally {
				//Fork steps are idempotent, so a failed run leaves the database usable for another attempt instead of flagged as corrupted.
				Prefs.UpdateBool(PrefName.CorruptedDatabase,false,true);
				Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,"");
			}
			Cache.Refresh(InvalidType.Prefs,InvalidType.Programs);
			SecurityLog securityLog=new SecurityLog {
				PermType=EnumPermType.UpdateInstall,
				UserNum=0,//Created before a user logs in.
				LogDateTime=DateTime.Now,
				LogText="Database updated from fork schema level "+forkDbState.ForkSchemaLevel+" to "+runner.LatestLevel+".",
				CompName=Security.GetComplexComputerName()
			};
			SecurityLogs.MakeLogEntry(securityLog);
			return true;
		}
		#endregion Conversion

		#region Release channel
		///<summary>Called from FormOpenDental.PrefsStartup after conversion. Compares this build with the release the update server published and
		///publishes, pulls or refuses (ForkReleases.Decide). There is never a downgrade prompt, and Open Dental's update files are never read.
		///Debug builds neither publish nor pull. Returns false if the program must exit.</summary>
		///<param name="formCurrent">The form where the method is being called from.</param>
		///<param name="isSilent">Whether this is a silent update. A silent update will have no UI elements appear.</param>
		///<param name="chooseDatabaseInfo">May be null. Dynamic mode (per-version program folders) is not supported by the fork's release channel.</param>
		public static bool CheckProgramVersion(Form formCurrent,bool isSilent,ChooseDatabaseInfo chooseDatabaseInfo,CommandLineArgs commandLineArgs) {
			ForkDbState forkDbState=ForkVersions.ReadState();
			bool isUpdateServer=IsUpdateServer();
			if(!forkDbState.IsForkMarked && isUpdateServer && !ODBuild.IsDebug()) {
				//Compatibility line: ProgramVersion keeps its Open Dental meaning. After the legacy chain converted an older database, raise it to
				//24.3.63.0. An adopted database keeps its sentinel and is never touched here.
				Version versionProgram=ForkVersions.ParseVersion(forkDbState.ProgramVersion);
				if(versionProgram==null || versionProgram<ForkVersions.CompatVersion) {
					Prefs.UpdateString(PrefName.ProgramVersion,ForkVersions.CompatVersion.ToString(4));
					Cache.Refresh(InvalidType.Prefs);
				}
			}
			ReleaseIdentity releaseIdentityRunning=ForkReleases.GetRunningIdentity();
			ReleaseManifest releaseManifestPublished=ForkReleases.GetPublishedManifest(out string _);
			ReleaseDecision releaseDecision=ForkReleases.Decide(releaseManifestPublished,releaseIdentityRunning,isUpdateServer,ODBuild.IsDebug(),
				PrefC.GetString(PrefName.WebServiceServerName));
			switch(releaseDecision.Action) {
				case ReleaseAction.Publish:
					return PublishRelease(releaseIdentityRunning,isSilent,releaseDecision.Message,doAsk:true);
				case ReleaseAction.Pull:
					PullRelease(releaseManifestPublished,releaseIdentityRunning,isSilent);
					return false;//The copier restarts the program, or the user cancelled.
				case ReleaseAction.RefuseNewerThanPublished:
					FormOpenDental.ExitCode=ForkVersionGuard.EXIT_CODE_RELEASE_NEWER_THAN_PUBLISHED;
					if(!isSilent) {
						MessageBox.Show(releaseDecision.Message);
					}
					return false;
				default:
					return true;
			}
		}

		///<summary>Recopy (Update Setup): publishes the files of this installation as the current release, on the update server only. The parameters
		///are those of Open Dental's update-file copy and are ignored: the fork stores only its own release rows. Returns true on success.</summary>
		public static bool CopyFromHereToUpdateFiles(Version versionCurrent,bool isSilent,bool hasAtoZ,bool hasConcatFiles) {
			if(!IsUpdateServer()) {
				if(!isSilent) {
					MessageBox.Show(Lan.g("Prefs","Only the update server publishes releases:")+" "+PrefC.GetString(PrefName.WebServiceServerName));
				}
				return false;
			}
			return PublishRelease(ForkReleases.GetRunningIdentity(),isSilent,"",doAsk:false);
		}

		///<summary>Stores this installation's files in the database as the published release, so the other workstations update to it on their next
		///start. Returns false only in silent mode when publishing failed; otherwise this workstation keeps running either way.</summary>
		private static bool PublishRelease(ReleaseIdentity releaseIdentity,bool isSilent,string reason,bool doAsk) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return true;//Releases are published over a direct connection only.
			}
			if(!isSilent && doAsk) {
				string message=(reason==""?"":reason+"\r\n\r\n")
					+Lan.g("Prefs","Publish this build (release")+" "+releaseIdentity+") "
					+Lan.g("Prefs","so the other workstations update to it when they start?")+"\r\n"
					+Lan.g("Prefs","Click Cancel to run it on this workstation only.");
				if(MessageBox.Show(message,"",MessageBoxButtons.OKCancel)!=DialogResult.OK) {
					return true;
				}
			}
			ReleaseManifest releaseManifest=null;
			Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,Environment.MachineName);//Other workstations wait while the rows are replaced.
			try {
				if(isSilent) {
					releaseManifest=ForkReleases.Publish(Application.StartupPath,releaseIdentity,ODEnvironment.MachineName);
				}
				else {
					ProgressWin progressWin=new ProgressWin();
					progressWin.StartingMessage=Lan.g("Prefs","Publishing the release...");
					progressWin.ActionMain=() => releaseManifest=ForkReleases.Publish(Application.StartupPath,releaseIdentity,ODEnvironment.MachineName);
					progressWin.ShowCancelButton=false;
					progressWin.ShowDialog();
				}
			}
			catch(Exception ex) {
				FormOpenDental.ExitCode=303;//Failed inserting update files into the database.
				if(!isSilent) {
					FriendlyException.Show(Lan.g("Prefs","Publishing the release failed. The other workstations keep the previous release."),ex);
				}
			}
			finally {
				Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,"");
			}
			if(releaseManifest==null) {
				return !isSilent;
			}
			if(UpdateHistories.GetForVersion(releaseIdentity.ReleaseVersion)==null) {
				UpdateHistories.Insert(new UpdateHistory(releaseIdentity.ReleaseVersion));
			}
			Cache.Refresh(InvalidType.Prefs);
			SecurityLogs.MakeLogEntry(new SecurityLog {
				PermType=EnumPermType.UpdateInstall,
				UserNum=0,//Created before a user logs in.
				LogDateTime=DateTime.Now,
				LogText="Release "+releaseIdentity+" published with "+releaseManifest.ListFiles.Count+" files.",
				CompName=Security.GetComplexComputerName()
			});
			if(!isSilent && doAsk) {
				MsgBox.Show("Prefs","Release published. The other workstations update when they start.");
			}
			return true;
		}

		///<summary>Downloads the published release, checks the archive and every file with SHA-256, and starts UpdateFileCopier, which checks the
		///files again, copies them over this installation and restarts the program. On any problem nothing on this workstation changes.</summary>
		private static void PullRelease(ReleaseManifest releaseManifestPublished,ReleaseIdentity releaseIdentityRunning,bool isSilent) {
			if(isSilent) {
				FormOpenDental.ExitCode=312;//Stored version is higher that client version after an update was successful.
				return;
			}
			string folderUpdate=ODFileUtils.CombinePaths(PrefC.GetTempFolderPath(),"ForkRelease");
			ReleaseDownloadResult releaseDownloadResult=null;
			try {
				ProgressWin progressWin=new ProgressWin();
				progressWin.StartingMessage=Lan.g("Prefs","Downloading the release...");
				progressWin.ActionMain=() => releaseDownloadResult=ForkReleases.DownloadAndVerify(folderUpdate);
				progressWin.ShowCancelButton=false;
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				FormOpenDental.ExitCode=ForkVersionGuard.EXIT_CODE_RELEASE_INVALID;
				FriendlyException.Show(Lan.g("Prefs","The published release could not be downloaded. Nothing was changed on this workstation."),ex);
				return;
			}
			if(releaseDownloadResult==null || !releaseDownloadResult.IsOk) {
				FormOpenDental.ExitCode=ForkVersionGuard.EXIT_CODE_RELEASE_INVALID;
				List<string> listProblems=releaseDownloadResult?.ListProblems??new List<string>();
				MessageBox.Show(Lan.g("Prefs","The published release failed its checks, so it was not installed. Nothing was changed on this workstation.")
					+"\r\n"+string.Join("\r\n",listProblems.Take(10))+(listProblems.Count>10?"\r\n...":"")+"\r\n\r\n"
					+Lan.g("Prefs","Publish the release again from the update server."));
				return;
			}
			ReleaseIdentity releaseIdentityPublished=new ReleaseIdentity {
				ReleaseVersion=releaseDownloadResult.Manifest.ReleaseVersion,
				BuildId=releaseDownloadResult.Manifest.BuildId
			};
			if(MessageBox.Show(Lan.g("Prefs","Files will now be copied.")+"\r\n"
				+Lan.g("Prefs","This workstation will be updated from")+" "+releaseIdentityRunning+" "+Lan.g("Prefs","to")+" "+releaseIdentityPublished+".",
				"",MessageBoxButtons.OKCancel)!=DialogResult.OK)
			{
				return;
			}
			bool useLocalUpdateFileCopier=!File.Exists(ODFileUtils.CombinePaths(folderUpdate,"UpdateFileCopier.exe"));
			OpenFileCopier(folderUpdate,Application.StartupPath,killServices:true,useLocalUpdateFileCopier,openCopiedFiles:true);
		}
		#endregion Release channel

		///<summary>This ONLY runs when first opening the program.  Gets run early in the sequence. Returns false if the program should exit.</summary>
		public static bool CheckMySqlVersion() {
			return CheckMySqlVersion(false);
		}

		///<summary>This ONLY runs when first opening the program.  Gets run early in the sequence. Returns false if the program should exit.</summary>
		public static bool CheckMySqlVersion(bool isSilent) {
			if(DataConnection.DBtype!=DatabaseType.MySql) {
				return true;
			}
			bool hasBackup=false;
			string thisVersion=MiscData.GetMySqlVersion();
			Version versionMySQL=new Version(thisVersion);
			if(versionMySQL < new Version(5,0)) {
				FormOpenDental.ExitCode=110;//MySQL version lower than 5.0
				if(!isSilent) {
					//We will force users to upgrade to 5.0, but not yet to 5.5
					MessageBox.Show(Lan.g("Prefs","Your version of MySQL won't work with this program")+": "+thisVersion
						+".  "+Lan.g("Prefs","You should upgrade to MySQL 5.0 using the installer on our website."));
				}
				return false;
			}
			if(!PrefC.ContainsKey("MySqlVersion")) {//db has not yet been updated to store this pref
				//We're going to skip this.  We will recommend that people first upgrade OD, then MySQL, so this won't be an issue.
			}
			else {//Using a version that stores the MySQL version as a preference.
				//There was an old bug where the MySQLVersion preference could be stored as 5,5 instead of 5.5 due to converting the version into a float.
				//Replace any commas with periods before checking if the preference is going to change.
				//This is simply an attempt to avoid making unnecessary backups for users with a corrupt version (e.g. 5,5).
				if(PrefC.GetString(PrefName.MySqlVersion).Contains(",")) {
					Prefs.UpdateString(PrefName.MySqlVersion,PrefC.GetString(PrefName.MySqlVersion).Replace(",","."));
				}
				//Now check to see if the MySQL version has been updated.  If it has, make an automatic backup, repair, and optimize all tables.
				string mysqlVersionPrev=PrefC.GetString(PrefName.MySqlVersion);
				if(Prefs.UpdateString(PrefName.MySqlVersion,(thisVersion))) {
					if(!ODBuild.IsDebug()) {
						if(!isSilent) {
							if(!MsgBox.Show("Prefs",MsgBoxButtons.OKCancel,"Tables will now be backed up, optimized, and repaired.  This will take a minute or two.  Continue?")) {
								//User wanted to cancel, set the MySQL version to previous.
								Prefs.UpdateString(PrefName.MySqlVersion,mysqlVersionPrev);
								FormOpenDental.ExitCode=0;
								return false;
							}
						}
						if(!Shared.BackupRepairAndOptimize(isSilent,BackupLocation.ConvertScript,false)) {
							//Failed. Set the MySQL version to previous version so next time OD is launched, BackupRepairAndOptimize happens again.
							Prefs.UpdateString(PrefName.MySqlVersion,mysqlVersionPrev);
							FormOpenDental.ExitCode=101;//Database Backup failed
							return false;
						}
						hasBackup=true;
					}
				}
			}
			if(PrefC.ContainsKey("DatabaseConvertedForMySql41")) {
				return true;//already converted
			}
			if(!isSilent) {
				if(!MsgBox.Show("Prefs",MsgBoxButtons.OKCancel,"Your database will now be converted for use with MySQL 4.1.")) {
					FormOpenDental.ExitCode=0;
					return false;
				}
			}
			//ClassConvertDatabase CCD=new ClassConvertDatabase();
			if(!hasBackup) {//A backup could have been made if the tables were optimized and repaired above.
				if(!Shared.MakeABackup(isSilent,BackupLocation.ConvertScript,false)) {
					FormOpenDental.ExitCode=101;//Database Backup failed
					return false;//but this should never happen
				}
			}
			if(!isSilent) {
				MsgBox.Show("Prefs","Backup performed");
			}
			Prefs.ConvertToMySqlVersion41();
			if(!isSilent) {
				MsgBox.Show("Prefs","Converted");
			}
			return true;
		}

		///<summary>This runs when first opening the program.  If MySql is not at 5.5 or higher, it reminds the user, but does not force them to upgrade.</summary>
		public static void MySqlVersion55Remind(){
			if(DataConnection.DBtype!=DatabaseType.MySql) {
				return;
			}
			string thisVersion=MiscData.GetMySqlVersion();
			Version versionMySQL=new Version(thisVersion);
			if(versionMySQL < new Version(5,5) && !Programs.IsEnabled(ProgramName.eClinicalWorks)) {//Do not show msg if MySQL version is 5.5 or greater or eCW is enabled
				MsgBox.Show("Prefs","You should upgrade to MySQL 5.5 using the installer posted on our website.  It's not urgent, but until you upgrade, you are likely to get a few errors each day which will require restarting the MySQL service.");
			}
		}

		///<summary>Tries to install the OpenDentalService if needed.  Returns false if failed.
		///Set isSilent to false to show meaningful error messages, otherwise fails silently.</summary>
		public static bool TryInstallOpenDentalService(bool isSilent) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				if(!isSilent) {
					MsgBox.Show("ServicesHelper","Not allowed to install services when using the middle tier.");
				}
				return false;
			}
			List<ServiceController> listServiceControllersOD=new List<ServiceController>();
			FileInfo fileInfoOdServiceExe;
			string odServiceFilePath;
			try {
				listServiceControllersOD=ServicesHelper.GetServicesByExe("OpenDentalService.exe");
				odServiceFilePath=ODFileUtils.CombinePaths(Directory.GetCurrentDirectory(),"OpenDentalService","OpenDentalService.exe");
			}
			catch(Exception e) {
				AlertItems.CreateGenericAlert(Lans.g("ServicesHelper","Open Dental Service Error"),Lans.g("ServicesHelper","Unknown exception:")+" "+e.Message);
				return false;
			}
			if(listServiceControllersOD.Count>0) {
					return true;//An Open Dental Service is already installed.
			}				
			fileInfoOdServiceExe=new FileInfo(odServiceFilePath);
			if(!fileInfoOdServiceExe.Exists) {
				AlertItems.CreateGenericAlert(Lans.g("ServicesHelper","Open Dental Service Error"),(Lans.g("ServicesHelper","Unable to install the OpenDentalService; Cannot find")+$" '{odServiceFilePath}'"));
				return false;
				}
			if(!ServicesHelper.Install("OpenDentalService",fileInfoOdServiceExe)) {
				AlertItems.CreateGenericAlert(Lans.g("ServicesHelper","Open Dental Service Error"),Lans.g("ServicesHelper","Failed to install OpenDentalService, try running	as admin."));
				return false;
			}
			//Create a new OpenDentalServiceConfig.xml file for Open Dental Service if one is not already present.
			if(!CreateConfigForOpenDentalService()) {
				AlertItems.CreateGenericAlert(Lans.g("ServicesHelper","Open Dental Service Error"),Lans.g("ServicesHelper","Failed to create OpenDentalServiceConfig.xml file."));
				return false;
			}
			//Now that the service has finally installed we need to try and start it.
			listServiceControllersOD=ServicesHelper.GetServicesByExe("OpenDentalService.exe");
			if(listServiceControllersOD.Count<1) {
				AlertItems.CreateGenericAlert(Lans.g("ServicesHelper","Open Dental Service Error"),Lans.g("ServicesHelper","OpenDental Service could not be found."));
				return false;
			}
			string openDentalServiceStartingErrors=ServicesHelper.StartServices(listServiceControllersOD);
			if(!string.IsNullOrEmpty(openDentalServiceStartingErrors)) {
				AlertItems.CreateGenericAlert(Lans.g("ServicesHelper","Open Dental Service Error"),Lans.g("ServicesHelper","The following service(s) could not start:")+" "+openDentalServiceStartingErrors);
				return false;
			}
			return true;
		}

		///<summary>Creates a default OpenDentalServiceConfig.xml file for Open Dental Service if one is not already present.
		///Uses the current connection settings in DataConnection.  This method does NOT work if called via middle tier.
		///Users should not be installing Open Dental Service via the middle tier.</summary>
		public static bool CreateConfigForOpenDentalService() {
			string odServiceConfigPath=ODFileUtils.CombinePaths(Directory.GetCurrentDirectory(),"OpenDentalService","OpenDentalServiceConfig.xml");
			//Check to see if there is already a config file present.
			if(File.Exists(odServiceConfigPath)) {
				return true;//Nothing to do.
			}
			//At this point we know that Open Dental Service does not have a config file present.
			string mySqlPassHash;
			Secrets.Encrypt(DataConnection.GetMysqlPass(),out mySqlPassHash);
			return ServicesHelper.CreateServiceConfigFile(odServiceConfigPath
				,DataConnection.GetServerName()
				,DataConnection.GetDatabaseName()
				,DataConnection.GetMysqlUser()
				,DataConnection.GetMysqlPass()
				,mySqlPassHash
				,DataConnection.GetMysqlUserLow()
				,DataConnection.GetMysqlPassLow());
		}

		///<summary>Sets up the executable file and opens the UpdateFileCopier with the correct command line arguments passed in. Returns whether
		///the file copier was successfully started.</summary>
		///<param name="folderUpdate">Where the update files are stored.</param>
		///<param name="destDir">Where the update files will be copied to.</param>
		///<param name="killServices">Will tell the file copier whether to kill all Open Dental services or not.</param>
		///<param name="useLocalUpdateFileCopier">Will use the update file copier in the local installation directory rather than the one downloaded from
		///the server.</param>
		///<param name="openCopiedFiles">Tells the file copier to open the copied files after completion.</param>
		public static bool OpenFileCopier(string folderUpdate,string destDir,bool killServices,bool useLocalUpdateFileCopier,bool openCopiedFiles) {
			string tempDir=PrefC.GetTempFolderPath();
			//copy UpdateFileCopier.exe to the temp directory
			//In the case of using dynamic mode, because we have modified the update file copier when we released this feature, we need to be 
			//guarenteed we are using the correct version. We know that the version in our installation directory has the updates we need as otherwise
			//they would never be able to reach these lines of code.
			string updateFileCopierLocation="";
			if(useLocalUpdateFileCopier) {
				if(Application.StartupPath.Contains("DynamicMode")) {
					//If they are within a different dynamic mode folder, the installation directory will be two directories up.
					updateFileCopierLocation=Directory.GetParent(Directory.GetParent(Application.StartupPath).FullName).FullName;
				}
				else {
					//Otherwise, this is the installation directory.
					updateFileCopierLocation=Application.StartupPath;
				}
			}
			else {//Otherwise use the update file copier from the server.
				updateFileCopierLocation=folderUpdate;
			}
			try { 
				File.Copy(ODFileUtils.CombinePaths(updateFileCopierLocation,"UpdateFileCopier.exe"),//source
					ODFileUtils.CombinePaths(tempDir,"UpdateFileCopier.exe"),//dest
					true);//overwrite
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("Prefs","Unable to copy ")+"UpdateFileCopier.exe "+Lans.g("Prefs","from ")+updateFileCopierLocation+".",ex);
				return false;
			}
			//wait a moment to make sure the file was copied
			Thread.Sleep(500);
			//launch UpdateFileCopier to copy all files to here.
			int processId=Process.GetCurrentProcess().Id;
			string startFileName=ODFileUtils.CombinePaths(tempDir,"UpdateFileCopier.exe");
			string arguments="\""+folderUpdate+"\""//pass the source directory to the file copier.
				+" "+processId.ToString()//and the processId of Open Dental.
				+" \""+destDir+"\""//and the destination directory
				+" "+killServices.ToString()//and whether to kill all processes or not
				+" "+openCopiedFiles.ToString();//and whether to open the copied files or not.
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientDirect && !string.IsNullOrEmpty(DataConnection.GetServerName())) {
				Secrets.Encrypt(DataConnection.GetMysqlPass(),out string mySqlDatabasePassHash);//sets encryptedPwd ot value or null
				arguments+=" \""+DataConnection.GetServerName()+"\""//and server name for reconnecting to database
					+" \""+DataConnection.GetDatabaseName()+"\""//and database name for reconnecting to database
					+" \""+DataConnection.GetMysqlUser()+"\""//and mysqluser for reconnecting to database
					+" \""+mySqlDatabasePassHash+"\"";//and mysqlpassHash for reconnecting to database
			}
			Process process=new Process();
			process.StartInfo.FileName=startFileName;
			process.StartInfo.Arguments=arguments;
			try {
				process.Start();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g("Prefs","Unable to start the update file copier. Try closing and reopening the program."),ex);
				FormOpenDental.ExitCode=305;//Unable to start the UpdateFileCopier.exe process.
				Environment.Exit(FormOpenDental.ExitCode);
				return false;
			}
			process.WaitForExit();//Waits for the file copier to be complete.
			return true;
		}

		//IsRegKeyForTesting() was removed. It asked Open Dental HQ whether the registration key was a developer-only key.
	}
}
