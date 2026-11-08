using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;
using CodeBase;

namespace OpenPmsDental{
	/// <summary>Only used from Claim Edit to "attach" an image. These are not the kind that get sent with claims.</summary>
	public partial class FormImageSelectClaimAttach:FormODBase {
		public long PatNum;
		private Patient _patient;
		private Document[] _documentArray;
		///<summary>If DialogResult==OK, then this will contain the new ClaimAttach with the filename that the file was saved under.  File will be in the EmailAttachments folder.  But ClaimNum will not be set.</summary>
		public ClaimAttach ClaimAttachNew;
		///<summary>Set to true to allow TXT files.</summary>
		public bool CanAttachTxt=false;
		///<summary>Set to true to allow DOC/DOCX files.</summary>
		public bool CanAttachDoc=false;
		/// <summary>Set to true to allow PDF files.</summary>
		public bool CanAttachPdf=false;
		private static readonly string _snipSketchURI="ms-screensketch";
		///<summary> Keeps track of how long we've been trying to kill all running Snip Tool processes </summary>
		private Stopwatch _stopwatchKillSnipToolProcesses=new Stopwatch();
		private string _titleOriginal;

		///<summary></summary>
		public FormImageSelectClaimAttach()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormImageSelect_Load(object sender,EventArgs e) {
			_patient=Patients.GetPat(PatNum);
			FillGrid();
		}

		private void FillGrid(){
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g(this,"Date"),100);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Category"),120);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Description"),300);
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			GridRow row;
			_documentArray=Documents.GetAllWithPat(PatNum);
			for(int i=0;i<_documentArray.Length;i++){
				row=new GridRow();
				row.Cells.Add(_documentArray[i].DateCreated.ToString());
				row.Cells.Add(Defs.GetName(DefCat.ImageCats,_documentArray[i].DocCategory));
			  row.Cells.Add(_documentArray[i].Description);
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		///<summary>Returns an image from the clipboard. If no image is found or there was an error, then returns with a popup message.</summary>
		private Bitmap GetImageFromClipboard(bool isSilent=false, bool doShowProgressBar=true) {
			Bitmap bitmapClipboard=ODClipboard.GetImage(doShowProgressBar:doShowProgressBar);
			if(bitmapClipboard!=null || isSilent) {
				return bitmapClipboard;
			}
			string[] stringArrayFiles;
			try {
				stringArrayFiles=ODClipboard.GetFileDropList();
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return null;
			}
			if(stringArrayFiles==null) {
				MsgBox.Show(this,"Could not find a valid image or image file on the clipboard.");
				return null;
			}
			if(stringArrayFiles.Length>1) {//Show error if multiple images are in the list
				MsgBox.Show(this,"Can only paste one image from a file at a time");
				return null;
			}
			string fileName=stringArrayFiles.First();
			if(!File.Exists(fileName)) {
				MsgBox.Show(this,"The file on the clipboard could not be located. It may have been moved, deleted or renamed.");
				return null;
			}
			if(ClaimAttachDicom.IsDicomFileName(fileName)) {//CLAIM-DCM: a DICOM file is pasted as its 8-bit copy; null (after a message) if unreadable.
				return ClaimAttachDicomL.GetBitmapForFile(this,_patient,fileName);
			}
			try {
				bitmapClipboard?.Dispose();
				bitmapClipboard=new Bitmap(fileName);//Create a bitmap of the original file to avoid the transparency bug with the image class
			}
			catch(ArgumentException) {//Catch the "Parameter is not valid" error
				MsgBox.Show(this,"Invalid file type when copying image from location. Valid file types include BMP, GIF, JPEG, PNG, and TIFF.");
				return null;
			}
			return bitmapClipboard;
		}

		///<summary>Imports a document from the passed in path then calls SaveAttachment.</summary>
		private void ImportDocument(string path) {
			long defNumCategory=Defs.GetImageCat(ImageCategorySpecial.C);
				if(defNumCategory==0) {
					MsgBox.Show(this,"In Setup, Definitions, Image Categories, a category needs to be set for Claim Attachments.");
					return;
				}
				Document document;
				try {
					document=ImageStore.Import(path,defNumCategory,_patient);
				}
				catch(Exception ex) {
					MessageBox.Show(ex.Message);
					return;
				}
				SaveAttachment(document);
		}

		///<summary>Imports the passed in Bitmap, disposes the Bitmap, then calls SaveAttachment for the imported image.</summary>
		private void ImportImage(Bitmap bitmap) {
			long defNumCategory=Defs.GetImageCat(ImageCategorySpecial.C);
			if(defNumCategory==0) {
				MsgBox.Show(this,"In Setup, Definitions, Image Categories, a category needs to be set for Claim Attachments.");
				return;
			}
			Document document;
			try {
				document=ImageStore.Import(bitmap,defNumCategory,ImageType.Attachment,_patient);
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			bitmap.Dispose();
			SaveAttachment(document);
		}

		///<summary>Sets ClaimAttachNew to the passed in document and closes the form. Will show a popup message and return if there are any errors 
		///with trying to save the passed in document.</summary>
		private void SaveAttachment(Document document){
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				MsgBox.Show(this,"Error. Not using AtoZ images folder.");
				return;
			}
			string patientfolder=ImageStore.GetPatientFolder(_patient,ImageStore.GetPreferredAtoZpath());
			if(ClaimAttachDicom.IsDicomFileName(document.FileName)) {//CLAIM-DCM: an 8-bit JPEG copy is attached, never the DICOM file.
				SaveAttachmentDicom(document,patientfolder);
				return;
			}
			if(!ImageHelper.HasImageExtension(document.FileName)){
				if(CanAttachTxt && Path.GetExtension(document.FileName).ToLower()==".txt") {
				}
				else if(CanAttachDoc && Path.GetExtension(document.FileName).ToLower().In(".doc",".docx")) {
				}
				else if(CanAttachPdf && Path.GetExtension(document.FileName).ToLower()==".pdf") {
				}
				else {
					List<string> listAllowedFormats=new List<string>() { "images" };
					if(CanAttachTxt) {
						listAllowedFormats.Add("text (TXT)");
					}
					if(CanAttachDoc) {
						listAllowedFormats.Add("Microsoft Word (doc/docx)");
					}
					if(CanAttachPdf) {
						listAllowedFormats.Add("PDF");
					}
					MsgBox.Show(this,"Invalid file. Only "+string.Join(", ",listAllowedFormats)+" may be attached, no other file formats.");
					return;
				}
			}
			//CLAIM-DCM 1 (owner question, fixed on 2026-11-07): the file is read, an image with a crop, rotation, flip or brightness and contrast
			//window gets them applied (ClaimAttachImage), and the result is always written where the attachment points, in the local images folder or
			//in cloud storage alike. Before, an edited image in a local folder got an attachment row but no file, and cloud storage got the file unedited.
			string oldPath=ODFileUtils.CombinePaths(patientfolder,document.FileName);
			byte[] arrayBytesFile=null;
			if(CloudStorage.IsCloudStorage) {
				oldPath=oldPath.Replace("\\","/");
				UI.ProgressWin progressWinDownload=new UI.ProgressWin();
				progressWinDownload.StartingMessage="Downloading...";
				progressWinDownload.ActionMain=() => arrayBytesFile=CloudStorage.Download(patientfolder,document.FileName);
				progressWinDownload.ShowDialog();
				if(progressWinDownload.IsCancelled) {
					return;
				}
			}
			else {
				if(!File.Exists(oldPath)) {
					MsgBox.Show(this,"File not found: "+oldPath);
					return;
				}
				try {
					arrayBytesFile=File.ReadAllBytes(oldPath);
				}
				catch(Exception ex) {
					MessageBox.Show(ex.Message);
					return;
				}
			}
			if(arrayBytesFile==null || arrayBytesFile.Length==0) {
				MsgBox.Show(this,"File not found: "+oldPath);
				return;
			}
			byte[] arrayBytesAttach;
			try {
				arrayBytesAttach=ClaimAttachImage.BuildFileBytes(document,arrayBytesFile);
			}
			catch(Exception ex) {
				MessageBox.Show(this,Lan.g(this,"The image could not be prepared for the claim, so it was not attached.")+"\r\n"+ex.Message);
				return;
			}
			Random random=new Random();
			string newName=DateTime.Now.ToString("yyyyMMdd")+"_"+DateTime.Now.TimeOfDay.Ticks.ToString()+random.Next(1000).ToString()
				+ClaimAttachImage.GetAttachExtension(document);
			string attachPath=EmailAttaches.GetAttachPath();
			if(CloudStorage.IsCloudStorage) {
				UI.ProgressWin progressWinUpload=new UI.ProgressWin();
				progressWinUpload.StartingMessage="Uploading Image for Claim Attach...";
				progressWinUpload.ActionMain=() => CloudStorage.Upload(attachPath,newName,arrayBytesAttach);
				try {
					progressWinUpload.ShowDialog();
				}
				catch(Exception ex) {
					MessageBox.Show(this,ex.Message);
					return;
				}
				if(progressWinUpload.IsCancelled) {
					return;
				}
			}
			else {
				try {
					File.WriteAllBytes(ODFileUtils.CombinePaths(attachPath,newName),arrayBytesAttach);
				}
				catch(Exception ex) {
					MessageBox.Show(ex.Message);
					return;
				}
			}
			ClaimAttachNew=new ClaimAttach();
			ClaimAttachNew.DisplayedFileName=document.FileName;
			ClaimAttachNew.ActualFileName=newName;
			DialogResult=DialogResult.OK;
		}

		///<summary>CLAIM-DCM: attaches a DICOM document as an 8-bit JPEG copy (ClaimAttachDicom.BuildClaimAttachFile: the stored window, crop,
		///rotation and flip, read through the Imaging module's DICOM path). The copy goes into the EmailAttachments folder under a new .jpg name;
		///the DICOM file and its document are not changed. A file that cannot be read shows a message and nothing is attached.</summary>
		private void SaveAttachmentDicom(Document document,string patientFolder) {
			ClaimAttachDicomFile claimAttachDicomFile=null;
			try {
				if(CloudStorage.IsCloudStorage) {
					UI.ProgressWin progressWinDownload=new UI.ProgressWin();
					progressWinDownload.StartingMessage="Downloading...";
					progressWinDownload.ActionMain=() => claimAttachDicomFile=ClaimAttachDicom.BuildClaimAttachFile(document,patientFolder);
					progressWinDownload.ShowDialog();
					if(progressWinDownload.IsCancelled) {
						return;
					}
				}
				else {
					claimAttachDicomFile=ClaimAttachDicom.BuildClaimAttachFile(document,patientFolder);
				}
			}
			catch(ODException ex) {
				MessageBox.Show(this,ex.Message);
				return;
			}
			catch(Exception ex) {
				MessageBox.Show(this,ClaimAttachDicom.GetUnreadableMessage(document.FileName)+"\r\n"+ex.Message);
				return;
			}
			if(claimAttachDicomFile==null) {
				return;
			}
			Random random=new Random();
			string newName=DateTime.Now.ToString("yyyyMMdd")+"_"+DateTime.Now.TimeOfDay.Ticks.ToString()+random.Next(1000).ToString()
				+ClaimAttachDicom.JpegExtension;
			string attachPath=EmailAttaches.GetAttachPath();
			if(CloudStorage.IsCloudStorage) {
				UI.ProgressWin progressWin=new UI.ProgressWin();
				progressWin.StartingMessage="Uploading Image for Claim Attach...";
				progressWin.ActionMain=() => CloudStorage.Upload(attachPath,newName,claimAttachDicomFile.FileBytes);
				try {
					progressWin.ShowDialog();
				}
				catch(Exception ex) {
					MessageBox.Show(this,ex.Message);
					return;
				}
				if(progressWin.IsCancelled) {
					return;
				}
			}
			else {
				try {
					File.WriteAllBytes(ODFileUtils.CombinePaths(attachPath,newName),claimAttachDicomFile.FileBytes);
				}
				catch(Exception ex) {
					MessageBox.Show(this,ex.Message);
					return;
				}
			}
			ClaimAttachNew=new ClaimAttach();
			ClaimAttachNew.DisplayedFileName=claimAttachDicomFile.DisplayedFileName;
			ClaimAttachNew.ActualFileName=newName;
			DialogResult=DialogResult.OK;
		}

		#region Snipping tool
		private void EndSnipping() {
			timerMonitorClipboard.Stop();
			//Show the window in case it was minimized
			WindowState=FormWindowState.Normal;
			//Remove the "waiting for snip" text from the title
			Text=_titleOriginal;
			butSnipTool.Enabled=true;
			butImport.Enabled=true;
			butPasteImage.Enabled=true;
    }

		private void timerMonitorClipboard_Tick(object sender,EventArgs e) {
			timerMonitorClipboard.Stop();
			bool hasRunningProcess;
		List<Process> listProcesses=GetProcessesSnipTool();
			hasRunningProcess=listProcesses.Count>0;
			if(!hasRunningProcess) {
				WindowState=FormWindowState.Normal;
				BringToFront();
				MsgBox.Show(this,"The snipping tool was closed while waiting for a snip. Stopping snip.");
				EndSnipping();
				return;
			}
			Bitmap bitmapClipboard=GetImageFromClipboard(isSilent:true,doShowProgressBar:false);
			if(bitmapClipboard==null) {
			timerMonitorClipboard.Start();
				return;
			}
			EndSnipping();
			BringToFront();
			//Start trying to kill Snip & Sketch and Snipping Tool
			_stopwatchKillSnipToolProcesses.Restart();
			timerKillSnipToolProcesses.Start();
			ImportImage(bitmapClipboard);
		}

		///<summary>100ms. Monitor the list of running processes for Snip & Sketch and Snipping Tool, for a short duration,
		///and kill any matching processes.  Doesn't stop trying until the duration is over. </summary>
		private void timerKillSnipToolProcesses_Tick(object sender,EventArgs e) {
			List<Process> listProcesses=GetProcessesSnipTool();
			KillProcesses(listProcesses);
			if(_stopwatchKillSnipToolProcesses.Elapsed>TimeSpan.FromSeconds(3)) {
				timerKillSnipToolProcesses.Stop();
				_stopwatchKillSnipToolProcesses.Reset();
			}
		}

		///<summary> Kill all passed-in processes, ignoring any failures, and return True if any were killed. </summary>
		private bool KillProcesses(List<Process> listProcessesToKills) {
			int countProcesseskilled=0;
			for(int i=listProcessesToKills.Count-1;i>=0;i--) {//backward
				try {
					listProcessesToKills[i].Kill();
				}
				catch { }
				countProcesseskilled+=1;
			}
			return countProcesseskilled>0;
		}

		/// <summary> Return any running Snipping Tool or Snip & Sketch processes </summary>
		private List<Process> GetProcessesSnipTool() {
			string snippingToolProcess="SnippingTool";
			string snipAndSketchProcess="ScreenSketch";
			Process[] processesArrayRunnings=Process.GetProcesses();
			List<Process> listProcesses=new List<Process>();
			for(int i=0;i<processesArrayRunnings.Length;i++) {
				//Skip any processes that aren't Snipping Tool or Snip & Sketch
				if(processesArrayRunnings[i].ProcessName!=snippingToolProcess && processesArrayRunnings[i].ProcessName!=snipAndSketchProcess) {
					continue;
				}
				listProcesses.Add(processesArrayRunnings[i]);
			}
			return listProcesses;
		}

		///<summary>Returns false if a protocol handler for ms-screensketch does not exist, meaning Snip & Sketch is not installed.</summary>
		public static bool DoesSnipAndSketchExist() {
			try {
				using Microsoft.Win32.RegistryKey registryKeyScreenSketchProtocol=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(_snipSketchURI);
				return registryKeyScreenSketchProtocol!=null;
			}
			catch {
				return false;
			}
		}

		///<summary>Returns true if the Snipping Tool exe can be located.  False otherwise.</summary>
		public static bool DoesSnippingToolExist() {
			string windowsFolder=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			string snippingToolPath32=$"{windowsFolder}\\sysnative\\SnippingTool.exe";
			string snippingToolPath64=$"{windowsFolder}\\system32\\SnippingTool.exe";
			if(System.IO.File.Exists(snippingToolPath64) || System.IO.File.Exists(snippingToolPath32)) {
				return true;
			}
			return false;
		}

		///<summary>Attempts to start Snip & Sketch, then Snipping Tool if that fails. Returns true if either started, false if neither did.</summary>
		public static bool StartSnipAndSketchOrSnippingTool() {
			//Determine if the screensketch protocol is in the registry; if not, we assume Snip & Sketch is not installed.
			if(DoesSnipAndSketchExist()) {
				Process processSnipAndSketch=new Process();
				processSnipAndSketch.StartInfo.UseShellExecute=true;
				processSnipAndSketch.StartInfo.FileName=$"{_snipSketchURI}:";
				try {
					processSnipAndSketch.Start();
					return true;
				}
				catch {

				}
			}
			//Couldn't start Snip & Sketch, so try Snipping Tool next
			if(DoesSnippingToolExist()) {
				Process processSnippingTool=new Process();
				string pathToSnippingTool;
				if(!Environment.Is64BitOperatingSystem) {
					pathToSnippingTool="sysnative";
				}
				else {
					pathToSnippingTool="system32";
				}
				processSnippingTool.StartInfo.FileName=$"{Environment.GetFolderPath(Environment.SpecialFolder.Windows)}\\{pathToSnippingTool}\\SnippingTool.exe";
				try {
					processSnippingTool.Start();
				}
				catch {
					//Couldn't start either tool
					return false;
				}
				return true;
			}
			return false;
		}

		///<summary>Mimics FormClaimAttachmentDXC.StartSnipping()</summary>
		private void StartSnipping() {
			if(!ODClipboard.Clear()) {
				MsgBox.Show(this,"Couldn't access clipboard, try again.");
				return;
			}
			//If we're in the middle of trying to kill Snip Tool processes, stop for now.
			timerKillSnipToolProcesses.Stop();
			_stopwatchKillSnipToolProcesses.Reset();
			List<Process> listProcesses=GetProcessesSnipTool();
			if(KillProcesses(listProcesses)) {
				//Wait a short time before launching, since otherwise the Win32Exception "The remote procedure call failed and did not execute" can happen
				Thread.Sleep(100);
			}
			if(!StartSnipAndSketchOrSnippingTool()) {
				MsgBox.Show(this,"Neither the Snip & Sketch tool nor the Snipping Tool could be launched.  Copy an image to the clipboard, then use the Paste Image button to add it as an attachment.  If you are on a Remote Desktop connection, launch your local system's Snip & Sketch or Snipping Tool, and make snips using either of those, which will be automatically copied to the clipboard.  If neither tool is available on your system, use the Print Screen keyboard key, or other screenshot software, to copy screenshots to the clipboard.");
				return;
			}
			_titleOriginal=Text;
			Text=_titleOriginal+$" ({Lan.g(this,"Waiting For Snip")}...)";
			butSnipTool.Enabled=false;
			butImport.Enabled=false;
			butPasteImage.Enabled=false;
			//Wait half a second before minimizing, otherwise Snip & Sketch can end up behind Open Dental
			Thread.Sleep(500);
			WindowState=FormWindowState.Minimized;
			//begin monitoring the clipboard for results
			timerMonitorClipboard.Start();
		}
		#endregion

		private void butImport_Click(object sender,EventArgs e) {
			string importFilePath;
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Multiselect=false;
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				return;
			}
			importFilePath=openFileDialog.FileName;
			string selectedFilePath=importFilePath;
			if(ClaimAttachDicom.IsDicomFileName(selectedFilePath)) {
				//CLAIM-DCM: imported into the patient's images as the DICOM it is, then attached as an 8-bit copy (SaveAttachmentDicom). A file
				//that cannot be read is neither imported nor attached.
				if(ClaimAttachDicomL.CanReadFile(this,selectedFilePath)) {
					ImportDocument(selectedFilePath);
				}
				return;
			}
			try {
				Bitmap bitmap=(Bitmap)Image.FromFile(selectedFilePath);
				bitmap.Dispose();
				ImportDocument(selectedFilePath);
			}
			catch(FileNotFoundException ex) {
				FriendlyException.Show(Lan.g(this,"The selected file at")+": "+selectedFilePath+" "+Lan.g(this,"could not be found"),ex);
			}
			catch(OutOfMemoryException) {
				//Image.FromFile() will throw an OOM exception when the image format is invalid or not supported.
				//See MSDN if you have trust issues:  https://msdn.microsoft.com/en-us/library/stf701f5(v=vs.110).aspx
				string extension=Path.GetExtension(selectedFilePath);
				if(CanAttachTxt && extension.ToLower()==".txt") {
				}
				else if(CanAttachDoc && extension.ToLower().In(".doc",".docx")) {
				}
				else if(CanAttachPdf && extension.ToLower()==".pdf") {
				}
				else {
					MsgBox.Show(Lan.g(this,"The file does not have a valid format. Please try again or call support."));
					return;
				}
				ImportDocument(selectedFilePath);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"An error occurred. Please try again or call support.")+"\r\n"+ex.Message,ex);
			}
		}

		private void butPasteImage_Click(object sender,EventArgs e) {
			Bitmap bitmapPasted=GetImageFromClipboard();
			if(bitmapPasted!=null) {
				ImportImage(bitmapPasted);
			}
		}

		private void butSnipTool_Click(object sender,EventArgs e) {
			StartSnipping();
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(gridMain.GetSelectedIndex()==-1){
				return;
			}
			SaveAttachment(_documentArray[gridMain.GetSelectedIndex()]);
		}

		private void butOK_Click(object sender,System.EventArgs e) {
			if(gridMain.GetSelectedIndex()==-1){
				MsgBox.Show(this,"Please select an image first.");
				return;
			}
			SaveAttachment(_documentArray[gridMain.GetSelectedIndex()]);
		}

	}
}