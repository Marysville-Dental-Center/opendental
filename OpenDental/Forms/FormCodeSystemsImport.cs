using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using CodeBase;
using Ionic.Zip;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Imports CPT codes from the CPT data file (.zip) that the office licenses from the American Medical Association.
	///The other code systems (CDCREC, CVX, HCPCS, ICD-9-CM, ICD-10-CM, LOINC, RxNorm, SNOMED CT, SOP and UCUM) used to be downloaded from Open Dental's
	///servers in Open Dental's own file format. Those downloads are removed and not replaced; that data is loaded into the database directly.</summary>
	public partial class FormCodeSystemsImport:FormODBase {
		///<summary>The CPT row of the codesystem table. Null if the database has no CPT row.</summary>
		private CodeSystem _codeSystemCpt;
		///<summary>Indicates if user has imported codes while in the window.</summary>
		private bool _hasImported;
		///<summary>Current status of the CPT import, shown in the grid.</summary>
		private string _status="";

		public FormCodeSystemsImport() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormCodeSystemsImport_Load(object sender,EventArgs e) {
			UpdateCodeSystemThread.Finished+=new EventHandler(UpdateCodeSystemThread_FinishedSafe);
			FillGrid();
		}

		private void FillGrid() {
			_codeSystemCpt=CodeSystems.GetForCurrentVersion(true).FirstOrDefault(x => x.CodeSystemName=="CPT");
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col;
			col=new GridColumn("Code System",200);
			gridMain.Columns.Add(col);
			col=new GridColumn("Current Version",100);
			gridMain.Columns.Add(col);
			col=new GridColumn("Import Status",100);
			col.IsWidthDynamic=true;
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			if(_codeSystemCpt!=null) {
				GridRow row=new GridRow();
				row.Cells.Add(_codeSystemCpt.CodeSystemName);
				row.Cells.Add(_codeSystemCpt.VersionCur);
				row.Cells.Add(_status);
				row.Tag=_codeSystemCpt;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			butImport.Enabled=(_codeSystemCpt!=null && !UpdateCodeSystemThread.IsRunning);
		}

		///<summary>Shows the status in the grid. Used for problems found before the import starts.</summary>
		private void SetStatus(string status) {
			_status=status;
			FillGrid();
		}

		private void butImport_Click(object sender,EventArgs e) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {//Do not let users import code systems when using the middle tier.
				MsgBox.Show("CodeSystemImporter","Cannot import code systems when using the middle tier.");
				return;
			}
			if(_codeSystemCpt==null) {
				return;
			}
			//CPT codes require user to choose a local file, so the file is checked and unzipped here on the main thread. Only the import runs on a thread.
			if(!MsgBox.Show("CodeSystemImporter",MsgBoxButtons.OKCancel,"CPT codes are licensed separately from the American Medical Association, "
				+"in its data file format. Most offices do not need these codes. If you have the CPT data file, click OK to browse to it."))
			{
				return;
			}
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Title=Lan.g("CodeSystemImporter","Choose CPT .zip file");
			try {
				openFileDialog.InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				openFileDialog.Filter="zip|*.zip";
				openFileDialog.RestoreDirectory=true;
				openFileDialog.Multiselect=false;
				if(openFileDialog.ShowDialog()!=DialogResult.OK) {
					return;
				}
				if(!openFileDialog.FileName.ToLower().EndsWith(".zip")) {
					SetStatus(Lan.g("CodeSystemImporter","Could not locate .zip file in specified folder."));
					return;
				}
				string versionID="";
				if(openFileDialog.FileName.ToLower().Contains("cpt-2014-data-files-download.zip")) {
					versionID="2014";
				}
				else if(openFileDialog.FileName.ToLower().Contains("cpt-2015-data-files.zip")) {
					versionID="2015";
				}
				else if(openFileDialog.FileName.ToLower().Contains("dl513216_2016.zip")) {
					versionID="2016";
				}
				else if(openFileDialog.FileName.ToLower().Contains("dl513217.zip")) {
					versionID="2017";
				}
				//Unzip the compressed file-----------------------------------------------------------------------------------------------------
				bool foundFile=false;
				string meduFileName="MEDU.txt";//MEDU stands for MEDium desciption Upper case.
				using ZipFile zipFileUnzipped=ZipFile.Read(openFileDialog.FileName);
				for(int unzipIndex=0;unzipIndex<zipFileUnzipped.Count;unzipIndex++) {//unzip/write all files to the temp directory
					ZipEntry zipEntry=zipFileUnzipped[unzipIndex];
					if(!zipEntry.FileName.ToLower().Contains("medu.txt")) {  //This file used to be called "medu.txt.txt" and is now called "medu.txt".  Uses .Contains() to catch both cases.
						continue;
					}
					meduFileName=zipEntry.FileName;
					zipEntry.Extract(PrefC.GetTempFolderPath(),ExtractExistingFileAction.OverwriteSilently);
					foundFile=true;
				}
				if(!foundFile) {
					SetStatus(Lan.g("CodeSystemImporter","MEDU.txt file not found in zip archive."));
					return;
				}
				if(versionID=="") {
					//This prompt has the chance of allowing users to "corrupt" their data and import codes for one year and identify them as codes from a
					//different year. To fix this would require a manual query. If this is a common occurance, consider automating this step based on the codes
					//detected inside the MEDU file.
					InputBox input=new InputBox("What year are these CPT codes for?");
					input.ShowDialog();
					if(input.IsDialogCancel || !Regex.IsMatch(input.StringResult,@"^\d{4}$")) {//A four digit value was not entered.
						SetStatus(Lan.g("CodeSystemImporter","CPT code year must be specified."));
						return;
					}
					versionID=input.StringResult;
				}
				UpdateCodeSystemThread.Add(ODFileUtils.CombinePaths(PrefC.GetTempFolderPath(),meduFileName),_codeSystemCpt,
					new UpdateCodeSystemThread.UpdateCodeSystemArgs(UpdateCodeSystemThread_UpdateSafe),versionID);
			}
			catch(Exception ex) {
				SetStatus(ex.Message);
				return;
			}
			//We got this far so the local file was retrieved successfully. No initial status to report.
			_status="";
			//We will re-enable the Import button when we handle the UpdateCodeSystemThread.Finished event.
			if(UpdateCodeSystemThread.StartAll()) {
				_hasImported=true;
			}
			FillGrid();
		}

		#region thread handlers
		///<summary>Call this from external thread. Invokes to main thread to avoid cross-thread collision.</summary>
		private void UpdateCodeSystemThread_FinishedSafe(object sender,EventArgs e) {
			try {
				this.BeginInvoke(new EventHandler(UpdateCodeSystemThread_FinishedUnsafe),new object[] { sender,e });
			}
			//most likely because form is no longer available to invoke to
			catch { }
		}

		///<summary>Do not call this directly from external thread. Use UpdateCodeSystemThread_FinishedSafe.</summary>
		private void UpdateCodeSystemThread_FinishedUnsafe(object sender,EventArgs e) {
			FillGrid();//Shows the new current version and enables the Import button again.
		}

		///<summary>Call this from external thread. Invokes to main thread to avoid cross-thread collision.</summary>
		private void UpdateCodeSystemThread_UpdateSafe(CodeSystem codeSystem,string status,double percentDone,bool isDone,bool isSuccess,int numUpdated) {
			try {
				this.BeginInvoke(new UpdateCodeSystemThread.UpdateCodeSystemArgs(UpdateCodeSystemThread_UpdateUnsafe),new object[] { codeSystem,status,percentDone,isDone,isSuccess,numUpdated });
			}
			//most likely because form is no longer available to invoke to
			catch { }
		}

		///<summary>Do not call this directly from external thread. Use UpdateCodeSystemThread_UpdateSafe.</summary>
		private void UpdateCodeSystemThread_UpdateUnsafe(CodeSystem codeSystem,string status,double percentDone,bool isDone,bool isSuccess,int numUpdated) {
			//This is called a lot from the import thread so don't bother with the full FillGrid. Just find our row and column and update the cell's text.
			string cellText=((int)percentDone)+"%"+" -- "+status;
			if(isDone) {
				if(isSuccess) {
					//If done==true percentDone is the number of codes that were imported, not the percent done.
					//This is done so we don't have to change the signatures of exisiting functions but can still alert the user when no codes were actually imported.
					cellText=Lan.g("CodeSystemImporter","Import complete")+"! -- "+Lan.g("CodeSystemImporter","Number of codes imported")+": "
						+Convert.ToInt32(percentDone).ToString()+" "+Lan.g("CodeSystemImporter","Number of codes updated")+": "+numUpdated;
				}
				else {
					cellText=Lan.g("CodeSystemImporter","Import failed")+"! -- "+status;
				}
			}
			_status=cellText;
			gridMain.BeginUpdate();
			for(int i=0;i<gridMain.ListGridRows.Count;i++) {
				if(gridMain.ListGridRows[i].Tag==null
					|| !(gridMain.ListGridRows[i].Tag is CodeSystem)
					|| !(((CodeSystem)gridMain.ListGridRows[i].Tag).CodeSystemName==codeSystem.CodeSystemName)) {
					continue;
				}
				gridMain.ListGridRows[i].Cells[2].Text=cellText;
			}
			gridMain.EndUpdate();  //Need to call this instead of gridMain.Invalidate() because we need text wrapping to happen if there was a long error message.
		}
		#endregion

		///<summary>Worker thread class. Imports a code system from a local file on its own thread so that the window stays responsive.</summary>
		private class UpdateCodeSystemThread {
			///<summary>Static lis of threads. All managed internally. Must always be locked by _lock when accessed!!!</summary>
			private static List<UpdateCodeSystemThread> _threads=new List<UpdateCodeSystemThread>();
			///<summary>All access of _threads member MUST BE enclosed with lock statement in order to prevent thread-lock and race conditions.</summary>
			private static object _lock=new object();
			///<summary>The code system being updated.</summary>
			private CodeSystem _codeSystem;
			///<summary>The import function will check this flag occasionally to see if it should abort prematurely.</summary>
			private bool _quit=false;
			///<summary>Function signature required to send an update.  When done==true, percentDone is the number of codes imported.</summary>
			public delegate void UpdateCodeSystemArgs(CodeSystem codeSystem,string status,double percentDone,bool done,bool success,int numUpdated);
			///<summary>Required by ctor. Used to keep main thread aware of update progress.</summary>
			private UpdateCodeSystemArgs _updateHandler;
			///<summary>Event will be fired when the final thread has finished and all threads have been cleared from the list.</summary>
			public static EventHandler Finished;
			///<summary>The file to import, which the user provided. For CPT this is the MEDU.txt file extracted from the CPT zip file.</summary>
			private string _localFilePath;
			///<summary>The version of the code system being imported.</summary>
			private string _versionID;

			///<summary>Aborts the thread. Only called by StopAll.</summary>
			private void Quit() {
				_quit=true;
			}

			///<summary>Indicates if there are still 1 or more active threads.</summary>
			public static bool IsRunning {
				get {
					lock(_lock){
						return _threads.Count>=1;
					}
				}
			}

			///<summary>Private ctor. Will only be used internally by Add.</summary>
			private UpdateCodeSystemThread(string localFilePath,CodeSystem codeSystem,UpdateCodeSystemArgs updateCodeSystemArgs,string versionID) {
				_localFilePath=localFilePath;
				_codeSystem=codeSystem;
				_updateHandler+=updateCodeSystemArgs;
				_versionID=versionID;
			}

			///<summary>Provide a nice ledgible identifier.</summary>
			public override string ToString() {
				return _codeSystem.CodeSystemName;
			}

			///<summary>Thread list manager needs this to remove threads. Required for List.Contains.</summary>
			public override bool Equals(object obj) {
				return ((UpdateCodeSystemThread)obj)._codeSystem.CodeSystemNum==_codeSystem.CodeSystemNum;
			}

			///<summary>Just to prevent a VS warning.</summary>
			public override int GetHashCode() {
				return base.GetHashCode();
			}

			///<summary>Add a thread to the queue. These threads will not be started until StartAll is called subsequent to adding all necessary threads.
			///The file at localFilePath must exist locally.</summary>
			public static void Add(string localFilePath,CodeSystem codeSystem,UpdateCodeSystemArgs updateCodeSystemArgs,string versionID) {
				UpdateCodeSystemThread updateCodeSystemThread=new UpdateCodeSystemThread(localFilePath,codeSystem,updateCodeSystemArgs,versionID);
				lock(_lock) {
					_threads.Add(updateCodeSystemThread);
				}
			}

			///<summary>Use this to start the threads once all threads have been added using Add.</summary>
			public static bool StartAll() {
				bool hasStartedAtLeastOne=false;
				lock(_lock) {
					foreach(UpdateCodeSystemThread thread in _threads) {
						Thread th=new Thread(new ThreadStart(thread.Run));
						th.Name=thread.ToString();
						th.Start();
						hasStartedAtLeastOne=true;
					}
				}
				return hasStartedAtLeastOne;
			}

			///<summary>Sets the Quit flag for all threads. Use this if early abort is desired.</summary>
			public static void StopAll() {
				lock(_lock) {
					foreach(UpdateCodeSystemThread thread in _threads) {
						thread.Quit();
					}
					_threads.Clear();
				}
			}

			///<summary>Called internally each time time a thread has completed. Will trigger the Finished event if this is the last thread to complete.</summary>
			private void Done(string status,bool isSuccess, int numCodesImported,int numCodesUpdated) {
				_updateHandler(_codeSystem,status,Convert.ToDouble(numCodesImported),true,isSuccess,numCodesUpdated);//Pass in the number of codes imported as percentDone.  This was done so can display the number of codes imported withouth having to add a new perameter to the delegate function.
				bool isFinished=false;
				lock(_lock) {
					if(_threads.Contains(this)) {
						_threads.Remove(this);
					}
					isFinished=_threads.Count<=0;
				}
				if(isFinished && Finished!=null) {
					Finished("UpdateCodeSystemThread",new EventArgs());
				}
			}

			///<summary>Update the current status of this import thread. Thread owner is required to handle this as the delegat is required in the ctor.</summary>
			private void Update(string status,int numDone,int numTotal) {
				double percentDone=0;
				//Guard against illegal division.
				if(numTotal>0) {
					percentDone=100*(numDone/(double)numTotal);
				}
				_updateHandler(_codeSystem,status,percentDone,false,true,0);
			}

			///<summary>Helper used internally.</summary>
			private void ImportProgress(int numDone,int numTotal) {
				Update(Lan.g("CodeSystemImporter","Importing"),numDone,numTotal);
			}

			///<summary>The thread function.</summary>
			private void Run() {
				string failText="";
				int numCodesImported=0;
				int numCodesUpdated=0;
				if(!ImportHelper(ref failText,ref numCodesImported,ref numCodesUpdated)) {
					Done(Lan.g("CodeSystemImporter","Error")+": "+failText,false,0,0);
					return;
				}
				//set current version=imported version
				CodeSystems.UpdateCurrentVersion(_codeSystem,_versionID);
				Done(Lan.g("CodeSystemImporter","Import Complete"),true,numCodesImported,numCodesUpdated);
			}

			///<summary>Imports codeSystem from the local file. Returns false if unsuccessful.</summary>
			private bool ImportHelper(ref string failText,ref int numCodesImported,ref int numCodesUpdated) {
				try {
					if(!File.Exists(_localFilePath)) {
						throw new Exception(Lan.g("CodeSystemImporter","Local file not found ")+": "+_localFilePath);
					}
					switch(_codeSystem.CodeSystemName) {
						case "CPT":
							CodeSystems.ImportCpt(_localFilePath,new CodeSystems.ProgressArgs(ImportProgress),ref _quit,ref numCodesImported,ref numCodesUpdated,
								_versionID);
							break;
						default:
							throw new Exception(Lan.g("CodeSystemImporter","Unsupported Code System")+": "+_codeSystem.CodeSystemName);
					}
					//Import succeded so delete the import file where necessary.
					DeleteImportFileIfNecessary();
					//We got here so everything succeeded.
					return true;
				}
				catch(Exception ex) {
					failText=ex.Message;
				}
				//We got here so something failed.
				return false;
			}

			///<summary>Delete the import file which was extracted from the zip archive. It is temporary and can be deleted.</summary>
			private void DeleteImportFileIfNecessary() {
				//Don't bother if the file isn't there.
				if(!File.Exists(_localFilePath)) {
					return;
				}
				//We got this far so assume the file is safe to delete.
				File.Delete(_localFilePath);
			}
		}

		///<summary>If there are still import threads running then prompt the user to see if they want to abort the imports prematurely.</summary>
		private void FormCodeSystemsImport_FormClosing(object sender,FormClosingEventArgs e) {
			if(!UpdateCodeSystemThread.IsRunning) { //All done, exit.
				if(_hasImported) {
					DataValid.SetInvalid(InvalidType.EhrCodes);//Update in-memory list of codes for all other workstations
				}
				return;
			}
			if(MsgBox.Show("CodeSystemImporter",MsgBoxButtons.YesNo,"Import in progress. Would you like to abort?")) {
				//User wants abort the threads.
				UpdateCodeSystemThread.StopAll();
				_hasImported=false;
				return;
			}
			//User elected to continue waiting so cancel the Close event.
			e.Cancel=true;
		}
	}

}
