using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using CodeBase.Controls;
using ImagingDeviceManager;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>The EOB and amendment window: the EOBs of a claim payment (Insurance Payment, and Edit Claim) or of a preauthorization (Edit Claim,
	///and Enter Estimates for PreAuth), or the one file of an EHR amendment (Edit Amendment). Rebuilt in S0-6 on the current Imaging module's parts:
	///its WPF toolbar and zoom slider, PDFs in ODWebView2, printing through PrinterL, scanning with its EZTwain calls and paste through
	///ODClipboard. The file handling is EobAmendmentFiles (at the end of ImageStore.cs); the stored rows, folders and file names are those of the
	///old window. The Images module's image selector and image display are not used, because they work only on a patient's documents and mounts.
	///</summary>
	public partial class FormImages:FormODBase {
		#region Fields - Public
		///<summary>The EOBs of this claim payment. Set only one of ClaimPaymentNum, ClaimNumPreAuth and EhrAmendmentCur.</summary>
		public long ClaimPaymentNum;
		///<summary>Open Dental 25.2: the EOBs of this preauthorization (claim.ClaimNum of a PreAuth claim), stored with eobattach.ClaimNumPreAuth.</summary>
		public long ClaimNumPreAuth;
		///<summary>The amendment whose one file this window shows, scans, imports and deletes.</summary>
		public EhrAmendment EhrAmendmentCur;
		#endregion Fields - Public

		#region Fields - Private
		///<summary>The image showing, a copy that holds no lock on the stored file. Null while nothing or a PDF shows.</summary>
		private Bitmap _bitmapShowing;
		private Cursor _cursorPan;
		///<summary>The amendment as stored. Starts as EhrAmendmentCur and follows each change of its file.</summary>
		private EhrAmendment _ehrAmendment;
		private bool _isMouseDown;
		///<summary>The entries of listFiles, in the same order.</summary>
		private List<EobAmendmentFiles.FileEntry> _listFileEntries=new List<EobAmendmentFiles.FileEntry>();
		///<summary>Temp copies made for the PDF viewer (cloud storage, images in the database). Deleted when the window closes.</summary>
		private List<string> _listPathsTemp=new List<string>();
		private EobAmendmentFiles.EnumMode _mode;
		///<summary>Shows PDFs.</summary>
		private ODWebView2 _odWebView2;
		///<summary>The file the PDF viewer shows or is about to show.</summary>
		private string _odWebView2FilePath;
		private Point _pointMouseDown;
		///<summary>The pan of the image from the center of panelImage, in image pixels.</summary>
		private Point _pointTranslation;
		private Point _pointTranslationOld;
		private WpfControls.UI.ToolBar toolBarMain;
		private WpfControls.UI.ZoomSlider zoomSlider;
		#endregion Fields - Private

		#region Constructor
		public FormImages() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			toolBarMain=new WpfControls.UI.ToolBar();
			elementHostToolBarMain.Child=toolBarMain;
			zoomSlider=new WpfControls.UI.ZoomSlider();
			elementHostZoomSlider.Child=zoomSlider;
			zoomSlider.EventResetTranslation+=zoomSlider_EventResetTranslation;
			zoomSlider.EventZoomed+=zoomSlider_EventZoomed;
			zoomSlider.IsEnabled=false;
			_cursorPan=new Cursor(GetType(),"CursorPalm.cur");//The Imaging module's hand cursor.
			panelImage.Cursor=_cursorPan;
			panelImage.MouseWheel+=panelImage_MouseWheel;//here because not browsable in designer
		}
		#endregion Constructor

		#region Methods - Event Handlers
		private void FormImages_Load(object sender,EventArgs e) {
			_mode=EobAmendmentFiles.GetMode(ClaimPaymentNum,ClaimNumPreAuth,EhrAmendmentCur);
			_ehrAmendment=EhrAmendmentCur;
			float scaleZoom=LayoutManager.ScaleMyFont();
			toolBarMain.LayoutTransform=new System.Windows.Media.ScaleTransform(scaleZoom,scaleZoom);
			zoomSlider.LayoutTransform=new System.Windows.Media.ScaleTransform(scaleZoom,scaleZoom);
			LayoutToolBar();
			LayoutListMenu();
			_odWebView2=new ODWebView2();//The Imaging module's PDF viewer.
			_odWebView2.Visible=false;
			_odWebView2.DoBlockNavigation=true;
			_odWebView2.Bounds=panelImage.Bounds;
			_odWebView2.Anchor=panelImage.Anchor;
			LayoutManager.Add(_odWebView2,this);
			FillList();
			//The first file is selected when the window opens.
			if(_listFileEntries.Count>0) {
				listFiles.SetSelected(0);
			}
			ShowSelected();
		}

		private void FormImages_FormClosed(object sender,FormClosedEventArgs e) {
			ClearShowing();
			for(int i=0;i<_listPathsTemp.Count;i++) {
				EobAmendmentFiles.DeleteTempCopy(_listPathsTemp[i]);
			}
		}

		private void listFiles_SelectionChangeCommitted(object sender,EventArgs e) {
			ShowSelected();
		}

		///<summary>An EOB other than JPG, JPEG or GIF opens in its Windows program: the stored file, or a downloaded temp copy with cloud storage. With
		///images in the database it must be exported first, as in the old window.</summary>
		private void listFiles_MouseDoubleClick(object sender,MouseEventArgs e) {
			int idx=listFiles.IndexFromPoint(e.Location);
			if(idx<0 || idx>=_listFileEntries.Count) {
				return;
			}
			if(PrefC.AtoZfolderUsed==DataStorageType.InDatabase) {
				MsgBox.Show(this,"Images stored directly in database. Export file in order to open with external program.");
				return;
			}
			EobAmendmentFiles.FileEntry fileEntry=_listFileEntries[idx];
			if(!EobAmendmentFiles.CanOpenInWindowsProgram(fileEntry)) {
				return;
			}
			string filePath="";
			try {
				if(!RunWithCloudProgress(() => filePath=EobAmendmentFiles.GetLocalPath(fileEntry,out _),Lan.g(this,"Downloading Document..."))) {
					return;
				}
				//A temp copy that a Windows program opened stays in the temp folder, as in the old window.
				ODFileUtils.ProcessStart(filePath);
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}

		private void menuListPrint_Click(object sender,EventArgs e) {
			ToolBarPrint_Click(sender,e);
		}

		private void menuListDelete_Click(object sender,EventArgs e) {
			ToolBarDelete_Click(sender,e);
		}

		private void panelImage_MouseDown(object sender,MouseEventArgs e) {
			panelImage.Focus();//for the mouse wheel
			if(_bitmapShowing==null || e.Button!=MouseButtons.Left) {
				return;
			}
			_isMouseDown=true;
			_pointMouseDown=e.Location;
			_pointTranslationOld=_pointTranslation;
		}

		///<summary>Drag to pan, as in the Imaging module.</summary>
		private void panelImage_MouseMove(object sender,MouseEventArgs e) {
			if(!_isMouseDown || _bitmapShowing==null) {
				return;
			}
			float scaleTrans=100f/zoomSlider.Value;//example, 200%, 100/200=.5, indicating .5 bitmap pixels for each screen pixel
			int xTrans=(int)(_pointTranslationOld.X+(e.Location.X-_pointMouseDown.X)*scaleTrans);
			int yTrans=(int)(_pointTranslationOld.Y+(e.Location.Y-_pointMouseDown.Y)*scaleTrans);
			_pointTranslation=new Point(xTrans,yTrans);
			panelImage.Invalidate();
		}

		private void panelImage_MouseUp(object sender,MouseEventArgs e) {
			_isMouseDown=false;
		}

		private void panelImage_MouseWheel(object sender,MouseEventArgs e) {
			if(_bitmapShowing==null) {
				return;
			}
			float deltaZoom=zoomSlider.Value*(float)e.Delta/SystemInformation.MouseWheelScrollDelta/8f;//For example, -15
			zoomSlider.SetByWheel(deltaZoom);
			panelImage.Invalidate();
		}

		///<summary>The image fitted at first, then as zoomed and panned. Same drawing as the Imaging module's for a document without crop or rotation.
		///</summary>
		private void panelImage_Paint(object sender,PaintEventArgs e) {
			Graphics g=e.Graphics;
			g.Clear(Color.White);
			if(_bitmapShowing==null) {
				return;
			}
			try {
				//Center of the panel, so scaling is around its center:
				g.TranslateTransform(panelImage.Width/2,panelImage.Height/2);
				float scaleFactor=zoomSlider.Value/100f;
				g.ScaleTransform(scaleFactor,scaleFactor);
				//The user's pan is in image coordinates:
				g.TranslateTransform(_pointTranslation.X,_pointTranslation.Y);
				g.DrawImage(_bitmapShowing,-_bitmapShowing.Width/2,-_bitmapShowing.Height/2,_bitmapShowing.Width,_bitmapShowing.Height);
			}
			catch {
				//The bitmap might get replaced in the middle of a paint.
			}
		}

		private void panelImage_SizeChanged(object sender,EventArgs e) {
			if(zoomSlider==null) {
				return;//The layout manager sizes the panel before the constructor makes the zoom slider.
			}
			SetZoomSliderToFit();
			panelImage.Invalidate();
		}

		///<summary>Prints the shown image fitted inside the half-inch margins, from the margins' origin, as the old window did.</summary>
		private void printDocument_PrintPage(object sender,PrintPageEventArgs e) {
			e.HasMorePages=false;
			if(_bitmapShowing==null || _bitmapShowing.Width<1 || _bitmapShowing.Height<1) {
				return;
			}
			RectangleF rectangleFMargins=e.MarginBounds;
			float ratio=Math.Min(rectangleFMargins.Width/(float)_bitmapShowing.Width,rectangleFMargins.Height/(float)_bitmapShowing.Height);
			Graphics g=e.Graphics;
			g.InterpolationMode=InterpolationMode.HighQualityBicubic;
			g.CompositingQuality=CompositingQuality.HighQuality;
			g.SmoothingMode=SmoothingMode.HighQuality;
			g.PixelOffsetMode=PixelOffsetMode.HighQuality;
			g.DrawImage(_bitmapShowing,0,0,(int)(_bitmapShowing.Width*ratio),(int)(_bitmapShowing.Height*ratio));
		}

		private void zoomSlider_EventResetTranslation(object sender,EventArgs e) {
			_pointTranslation=new Point(0,0);
			panelImage.Invalidate();
		}

		private void zoomSlider_EventZoomed(object sender,EventArgs e) {
			//fires repeatedly while dragging
			panelImage.Invalidate();
		}
		#endregion Methods - Event Handlers

		#region Methods - Event Handlers Toolbar
		private void ToolBarClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}

		///<summary>Copies the shown image to the clipboard. Not enabled for a PDF.</summary>
		private void ToolBarCopy_Click(object sender,EventArgs e) {
			EobAmendmentFiles.FileEntry fileEntry=GetSelectedEntry();
			if(fileEntry==null) {
				MsgBox.Show(this,"Please select a document before copying");
				return;
			}
			if(_bitmapShowing==null) {
				return;
			}
			Cursor=Cursors.WaitCursor;
			try {
				Clipboard.SetImage((Bitmap)_bitmapShowing.Clone());
			}
			catch(Exception ex) {
				ex.DoNothing();
				Cursor=Cursors.Default;
				MsgBox.Show(this,"Could not copy contents to the clipboard.  Please try again.");
				return;
			}
			EobAmendmentFiles.LogCopied(fileEntry);
			Cursor=Cursors.Default;
		}

		private void ToolBarDelete_Click(object sender,EventArgs e) {
			EobAmendmentFiles.FileEntry fileEntry=GetSelectedEntry();
			if(fileEntry==null) {
				MsgBox.Show(this,"No item is currently selected");
				return;
			}
			if(fileEntry.IsEob) {
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete EOB?")) {
					return;
				}
				ClearShowing();//release the file
				EobAmendmentFiles.DeleteEob(fileEntry);//file and row
			}
			else {
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete amendment?")) {
					return;
				}
				ClearShowing();//release the file
				EobAmendmentFiles.DeleteAmendmentFile(_ehrAmendment);//the file; the row stays with an empty file name
			}
			FillList();
			ShowSelected();
		}

		///<summary>Saves a copy of the selected file under its stored name, or another name the user picks.</summary>
		private void ToolBarExport_Click(object sender,EventArgs e) {
			EobAmendmentFiles.FileEntry fileEntry=GetSelectedEntry();
			if(fileEntry==null) {
				MsgBox.Show(this,"Please select an item first.");
				return;
			}
			string fileName="";
			using SaveFileDialog saveFileDialog=new SaveFileDialog();
			saveFileDialog.Title=Lan.g(this,"Export a Document");
			saveFileDialog.FileName=fileEntry.FileName;
			saveFileDialog.DefaultExt=Path.GetExtension(fileEntry.FileName);
			if(saveFileDialog.ShowDialog()!=DialogResult.OK) {
				return;
			}
			fileName=saveFileDialog.FileName;
			if(fileName.Length<1) {
				MsgBox.Show(this,"You must enter a file name.");
				return;
			}
			try {
				if(!RunWithCloudProgress(() => EobAmendmentFiles.Export(fileEntry,fileName),Lan.g(this,"Downloading..."))) {
					return;
				}
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Unable to export file, May be in use")+": "+ex.Message+": "+fileName);
				return;
			}
			MessageBox.Show(Lan.g(this,"Successfully exported to ")+fileName);
		}

		///<summary>Several files become new EOBs; one file replaces the amendment's file.</summary>
		private void ToolBarImport_Click(object sender,EventArgs e) {
			if(!ConfirmReplaceAmendmentFile()) {
				return;
			}
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Multiselect=EobAmendmentFiles.IsImportMultiSelect(_mode);
			if(Prefs.GetContainsKey(nameof(PrefName.UseAlternateOpenFileDialogWindow)) && PrefC.GetBool(PrefName.UseAlternateOpenFileDialogWindow)) {//Hidden pref, almost always false.
				//We don't know why this makes any difference but people have mentioned this will stop some hanging issues.
				//https://stackoverflow.com/questions/6718148/windows-forms-gui-hangs-when-calling-openfiledialog-showdialog
				openFileDialog.ShowHelp=true;
			}
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				return;
			}
			List<string> listFileNames=openFileDialog.FileNames.ToList();
			if(listFileNames.Count<1) {
				return;
			}
			if(EobAmendmentFiles.IsEobMode(_mode)) {
				long eobAttachNumLastAdded=0;
				string fileNameLastAdded="";
				for(int i=0;i<listFileNames.Count;i++) {
					string fileNameImport=listFileNames[i];
					EobAttach eobAttach=null;
					try {
						if(!RunWithCloudProgress(() => eobAttach=EobAmendmentFiles.AddEob(fileNameImport,_mode,ClaimPaymentNum,ClaimNumPreAuth),
							Lan.g(this,"Uploading...")))
						{
							break;
						}
					}
					catch(Exception ex) {
						MessageBox.Show(Lan.g(this,"Unable to copy file, May be in use: ")+ex.Message+": "+fileNameImport);
						continue;
					}
					eobAttachNumLastAdded=eobAttach.EobAttachNum;
					fileNameLastAdded=fileNameImport;
				}
				FillList();
				if(eobAttachNumLastAdded==0) {
					ShowSelected();
					return;
				}
				SelectEob(eobAttachNumLastAdded,fileNameLastAdded);//The new EOB is selected.
				return;
			}
			if(_mode==EobAmendmentFiles.EnumMode.Amendment) {
				ClearShowing();//release the old file
				try {
					if(!RunWithCloudProgress(() => _ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,listFileNames[0]),
						Lan.g(this,"Uploading...")))
					{
						FillList();
						ShowSelected();
						return;
					}
				}
				catch(Exception ex) {
					MessageBox.Show(Lan.g(this,"Unable to copy file, May be in use: ")+ex.Message+": "+listFileNames[0]);
				}
				FillList();
				SelectAmendment(listFileNames[0]);
			}
		}

		///<summary>Pastes a bitmap, or one image file, from the clipboard: a new EOB, or the amendment's new file.</summary>
		private void ToolBarPaste_Click(object sender,EventArgs e) {
			if(_mode==EobAmendmentFiles.EnumMode.None) {
				return;
			}
			Bitmap bitmapPaste;
			try {
				bitmapPaste=ODClipboard.GetImage();
			}
			catch(Exception ex) {
				ex.DoNothing();
				MsgBox.Show(this,"Could not paste contents from the clipboard.  Please try again.");
				return;
			}
			string[] stringArrayFileNames=null;
			if(bitmapPaste==null) {//No image was found so look for a file path to an image instead
				try {
					stringArrayFileNames=ODClipboard.GetFileDropList();
				}
				catch(Exception ex) {
					ex.DoNothing();//the array stays null and the message below shows
				}
			}
			if(stringArrayFileNames==null && bitmapPaste==null) {
				MsgBox.Show(this,"There are no images on your clipboard.");
				return;
			}
			if(stringArrayFileNames!=null) {
				if(stringArrayFileNames.Length>1) {
					MsgBox.Show(this,"You can only paste one image at a time.");
					return;
				}
				if(stringArrayFileNames.Length==0 || !File.Exists(stringArrayFileNames[0])) {
					MsgBox.Show(this,"The file on the clipboard could not be located. It may have been moved, deleted or renamed.");
					return;
				}
				try {
					using Bitmap bitmapFromFile=new Bitmap(stringArrayFileNames[0]);
					bitmapPaste=new Bitmap(bitmapFromFile);//releases the file lock
				}
				catch {
					MsgBox.Show(this,"An invalid file type was pasted.");
					return;
				}
			}
			//S0-6: pasting onto an amendment that has a file asks and removes the old file, as scan and import do.
			if(!ConfirmReplaceAmendmentFile()) {
				bitmapPaste.Dispose();
				return;
			}
			Cursor=Cursors.WaitCursor;
			if(EobAmendmentFiles.IsEobMode(_mode)) {
				EobAttach eobAttach=null;
				try {
					if(!RunWithCloudProgress(() => eobAttach=EobAmendmentFiles.AddEob(bitmapPaste,_mode,ClaimPaymentNum,ClaimNumPreAuth),Lan.g(this,"Uploading..."))) {
						Cursor=Cursors.Default;
						bitmapPaste.Dispose();
						return;
					}
				}
				catch {
					Cursor=Cursors.Default;
					bitmapPaste.Dispose();
					MessageBox.Show(Lan.g(this,"Error saving eob."));
					return;
				}
				bitmapPaste.Dispose();
				FillList();
				SelectEob(eobAttach.EobAttachNum);
				Cursor=Cursors.Default;
				return;
			}
			ClearShowing();//release the old file
			try {
				if(!RunWithCloudProgress(() => _ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,bitmapPaste),Lan.g(this,"Uploading..."))) {
					Cursor=Cursors.Default;
					bitmapPaste.Dispose();
					FillList();
					ShowSelected();
					return;
				}
			}
			catch {
				Cursor=Cursors.Default;
				bitmapPaste.Dispose();
				MessageBox.Show(Lan.g(this,"Error saving amendment."));
				FillList();
				ShowSelected();
				return;
			}
			bitmapPaste.Dispose();
			FillList();
			SelectAmendment();
			Cursor=Cursors.Default;
		}

		///<summary>Prints the shown image through the program's printer settings and its Print Options window (Setup, Printers, the Default
		///situation), as the Imaging module does. PrinterL makes the one Printing log entry of each print. A PDF prints from the viewer's own print
		///command.</summary>
		private void ToolBarPrint_Click(object sender,EventArgs e) {
			EobAmendmentFiles.FileEntry fileEntry=GetSelectedEntry();
			if(fileEntry==null) {
				MsgBox.Show(this,"Cannot print. No document currently selected.");
				return;
			}
			if(_bitmapShowing==null) {
				return;//a PDF or another file type: the button is not enabled
			}
			try {
				PrinterL.TryPrintOrDebugClassicPreview(printDocument_PrintPage,EobAmendmentFiles.GetPrintAuditDescription(fileEntry),
					margins:new Margins(50,50,50,50),//Half-inch all around
					printoutOrigin:PrintoutOrigin.AtMargin,
					auditPatNum:EobAmendmentFiles.GetPrintAuditPatNum(fileEntry));
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"An error occurred while printing")+"\r\n"+ex.ToString());
			}
		}

		///<summary>Scans one page, as ControlImages.ToolBarScan_Click does with this computer's scan settings. It is saved as JPG at the scan quality
		///and also put on the clipboard.</summary>
		private void ToolBarScanDoc_Click(object sender,EventArgs e) {
			if(!ConfirmReplaceAmendmentFile()) {
				return;
			}
			Cursor=Cursors.WaitCursor;
			Bitmap bitmapScanned=null;
			IntPtr handleDIB=IntPtr.Zero;
			try {
				Twain.ActivateEZTwain();
			}
			catch {
				Cursor=Cursors.Default;
				MsgBox.Show(this,"EzTwain4.dll not found.  Please run the setup file in your images folder.");
				return;
			}
			if(IsDisposed) {
				return;//Possibly the form was being closed as the user clicked the scan button.
			}
			if(ComputerPrefs.LocalComputer.ScanDocSelectSource) {
				if(!EZTwain.SelectImageSource(this.Handle)) {//dialog to select source
					Cursor=Cursors.Default;
					return;//User clicked cancel.
				}
			}
			EZTwain.SetHideUI(!ComputerPrefs.LocalComputer.ScanDocShowOptions);
			if(!EZTwain.OpenDefaultSource()) {//if it can't open the scanner successfully
				Cursor=Cursors.Default;
				MsgBox.Show(this,"Default scanner could not be opened.  Check that the default scanner works from Windows Control Panel and from Windows Fax and Scan.");
				return;
			}
			EZTwain.SetResolution(ComputerPrefs.LocalComputer.ScanDocResolution);
			if(ComputerPrefs.LocalComputer.ScanDocGrayscale) {
				EZTwain.SetPixelType(1);//8-bit grayscale - only set if scanner dialog will not show
			}
			else {
				EZTwain.SetPixelType(2);//24-bit color
			}
			EZTwain.SetJpegQuality(ComputerPrefs.LocalComputer.ScanDocQuality);
			EZTwain.SetXferMech(EZTwain.XFERMECH_MEMORY);
			Cursor=Cursors.Default;
			handleDIB=EZTwain.Acquire(this.Handle);//This is where the options dialog would come up. The settings above will not populate this window.
			int errorCode=EZTwain.LastErrorCode();
			if(errorCode!=0) {
				if(errorCode==(int)EZTwainErrorCode.EZTEC_USER_CANCEL) {//19
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_JPEG_DLL) {//22
					MessageBox.Show("Missing dll\r\n\r\nRequired file EZJpeg.dll is missing.");
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_0_PAGES) {//38
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_NO_PDF) {//43
					MessageBox.Show("Missing dll\r\n\r\nRequired file EZPdf.dll is missing.");
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM) {//76
					MessageBox.Show("Paper jam\r\n\r\nPlease check the scanner document feeder and ensure there path is clear of any paper jams.");
					return;
				}
				//Other error codes do not block continuation, as in the Imaging module.
			}
			if(handleDIB==(IntPtr)0) {
				MsgBox.Show(EZTwain.LastErrorText());
				return;
			}
			double xdpi=EZTwain.DIB_XResolution(handleDIB);
			double ydpi=EZTwain.DIB_XResolution(handleDIB);
			IntPtr handleBitmap=EZTwain.DIB_ToDibSection(handleDIB);
			try {
				bitmapScanned=Bitmap.FromHbitmap(handleBitmap);//Sometimes throws 'A generic error occurred in GDI+.'
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Error scanning")+": "+ex.Message,ex);
				return;
			}
			bitmapScanned.SetResolution((float)xdpi,(float)ydpi);
			try {
				Clipboard.SetImage(bitmapScanned);//We do this because a customer requested it, and some customers probably use it.
			}
			catch {
				//Rarely, setting the clipboard image fails, in which case we should ignore the failure because most people do not use this feature.
			}
			bool isSaved=true;
			EobAttach eobAttach=null;
			if(EobAmendmentFiles.IsEobMode(_mode)) {
				try {
					isSaved=RunWithCloudProgress(() => eobAttach=EobAmendmentFiles.AddEob(bitmapScanned,_mode,ClaimPaymentNum,ClaimNumPreAuth),Lan.g(this,"Uploading..."));
				}
				catch(Exception ex) {
					isSaved=false;
					MessageBox.Show(Lan.g(this,"Error saving eob")+": "+ex.Message);
				}
			}
			else {
				ClearShowing();//release the old file
				try {
					isSaved=RunWithCloudProgress(() => _ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,bitmapScanned),Lan.g(this,"Uploading..."));
				}
				catch(Exception ex) {
					isSaved=false;
					MessageBox.Show(Lan.g(this,"Error saving amendment")+": "+ex.Message);
				}
			}
			bitmapScanned.Dispose();
			if(handleDIB!=IntPtr.Zero) {
				EZTwain.DIB_Free(handleDIB);
			}
			FillList();
			if(!isSaved) {
				ShowSelected();
				return;
			}
			if(eobAttach!=null) {
				SelectEob(eobAttach.EobAttachNum);
				return;
			}
			SelectAmendment();
		}

		///<summary>Scans several pages into one PDF, as ControlImages.ToolBarScanMulti_Click does with this computer's scan settings. The temp file is
		///deleted afterwards.</summary>
		private void ToolBarScanMulti_Click(object sender,EventArgs e) {
			if(!ConfirmReplaceAmendmentFile()) {
				return;
			}
			string tempFile=PrefC.GetRandomTempFile(".pdf");
			try {
				Twain.ActivateEZTwain();
			}
			catch {
				Cursor=Cursors.Default;
				MsgBox.Show(this,"EzTwain4.dll not found.  Please run the setup file in your images folder.");
				return;
			}
			if(IsDisposed) {
				return;//Possibly the form was being closed as the user clicked the scan button.
			}
			if(ComputerPrefs.LocalComputer.ScanDocSelectSource) {
				if(!EZTwain.SelectImageSource(this.Handle)) {
					return;//User clicked cancel.
				}
			}
			EZTwain.SetHideUI(!ComputerPrefs.LocalComputer.ScanDocShowOptions);
			EZTwain.PDF_SetCompression((int)this.Handle,(int)ComputerPrefs.LocalComputer.ScanDocQuality);
			if(!EZTwain.OpenDefaultSource()) {//if it can't open the scanner successfully
				MsgBox.Show(this,"Default scanner could not be opened.  Check that the default scanner works from Windows Control Panel and from Windows Fax and Scan.");
				Cursor=Cursors.Default;
				return;
			}
			EZTwain.EnableDuplex(ComputerPrefs.LocalComputer.ScanDocDuplex);
			if(ComputerPrefs.LocalComputer.ScanDocGrayscale) {
				EZTwain.SetPixelType(1);//8-bit grayscale
			}
			else {
				EZTwain.SetPixelType(2);//24-bit color
			}
			EZTwain.SetResolution(ComputerPrefs.LocalComputer.ScanDocResolution);
			EZTwain.AcquireMultipageFile(this.Handle,tempFile);//This is where the options dialog will come up if enabled. This will ignore and override the settings above.
			int errorCode=EZTwain.LastErrorCode();
			if(errorCode!=0) {
				if(errorCode==(int)EZTwainErrorCode.EZTEC_USER_CANCEL) {//19
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_JPEG_DLL) {//22
					MessageBox.Show("Missing dll\r\n\r\nRequired file EZJpeg.dll is missing.");
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_0_PAGES) {//38
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_NO_PDF) {//43
					MessageBox.Show("Missing dll\r\n\r\nRequired file EZPdf.dll is missing.");
					return;
				}
				else if(errorCode==(int)EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM) {//76
					MessageBox.Show("Paper jam\r\n\r\nPlease check the scanner document feeder and ensure the path is clear of any paper jams.");
					return;
				}
				MessageBox.Show(Lan.g(this,"Unable to scan. Please make sure you can scan using other software. Error: "+errorCode+" "+EZTwain.LastErrorText()));
				//Error messages do not block continuation, as in the Imaging module.
			}
			bool isSaved=true;
			EobAttach eobAttach=null;
			if(EobAmendmentFiles.IsEobMode(_mode)) {
				try {
					isSaved=RunWithCloudProgress(() => eobAttach=EobAmendmentFiles.AddEob(tempFile,_mode,ClaimPaymentNum,ClaimNumPreAuth),Lan.g(this,"Uploading..."));
				}
				catch(Exception ex) {
					isSaved=false;
					MessageBox.Show(Lan.g(this,"Unable to copy file, May be in use: ")+ex.Message+": "+tempFile);
				}
			}
			else {
				ClearShowing();//release the old file
				try {
					isSaved=RunWithCloudProgress(() => _ehrAmendment=EobAmendmentFiles.ReplaceAmendmentFile(_ehrAmendment,tempFile),Lan.g(this,"Uploading..."));
				}
				catch(Exception ex) {
					isSaved=false;
					MessageBox.Show(Lan.g(this,"Unable to copy file, May be in use: ")+ex.Message+": "+tempFile);
				}
			}
			ImageStore.TryDeleteFile(tempFile
				,actInUseException:(msg) => MsgBox.Show(msg)//Informs user when a 'file is in use' exception occurs.
			);
			FillList();
			if(!isSaved) {
				ShowSelected();
				return;
			}
			if(eobAttach!=null) {
				SelectEob(eobAttach.EobAttachNum);
				return;
			}
			SelectAmendment();
		}
		#endregion Methods - Event Handlers Toolbar

		#region Methods - Private
		///<summary>Releases the shown image and hides the PDF viewer, so the shown file can be deleted or replaced.</summary>
		private void ClearShowing() {
			_bitmapShowing?.Dispose();
			_bitmapShowing=null;
			_odWebView2FilePath=null;
			if(_odWebView2!=null) {
				_odWebView2.Visible=false;
			}
			panelImage.Visible=true;
			_pointTranslation=new Point(0,0);
			zoomSlider.IsEnabled=false;
			panelImage.Invalidate();
		}

		///<summary>Asks "This will delete your old file. Proceed?" before scan, import or paste replace the amendment's file. True to go ahead.</summary>
		private bool ConfirmReplaceAmendmentFile() {
			if(!EobAmendmentFiles.IsReplaceConfirmNeeded(_mode,_ehrAmendment)) {
				return true;
			}
			return MsgBox.Show(this,MsgBoxButtons.OKCancel,"This will delete your old file. Proceed?");
		}

		///<summary>Refills the list from the database (EOBs) or from the amendment, with nothing selected.</summary>
		private void FillList() {
			_listFileEntries=EobAmendmentFiles.GetFileEntries(_mode,ClaimPaymentNum,ClaimNumPreAuth,_ehrAmendment);
			listFiles.Items.Clear();
			for(int i=0;i<_listFileEntries.Count;i++) {
				listFiles.Items.Add(_listFileEntries[i].FileName,_listFileEntries[i]);
			}
		}

		private EobAmendmentFiles.FileEntry GetSelectedEntry() {
			int idx=listFiles.SelectedIndex;
			if(idx<0 || idx>=_listFileEntries.Count) {
				return null;
			}
			return _listFileEntries[idx];
		}

		///<summary>The list's right-click menu: Print and Delete.</summary>
		private void LayoutListMenu() {
			contextMenuStripList.Items.Clear();
			List<EobAmendmentFiles.EnumCommand> listCommands=EobAmendmentFiles.GetListMenuCommands(_mode);
			for(int i=0;i<listCommands.Count;i++) {
				switch(listCommands[i]) {
					case EobAmendmentFiles.EnumCommand.Print:
						contextMenuStripList.Items.Add(new ToolStripMenuItem(Lan.g(this,"Print"),null,menuListPrint_Click));
						break;
					case EobAmendmentFiles.EnumCommand.Delete:
						contextMenuStripList.Items.Add(new ToolStripMenuItem(Lan.g(this,"Delete"),null,menuListDelete_Click));
						break;
				}
			}
		}

		///<summary>The toolbar, from the mode's commands: Print, Delete, Scan Document, Scan Multi-Page Document, Import, Export, Copy, Paste and
		///Close.</summary>
		private void LayoutToolBar() {
			toolBarMain.Clear();
			List<EobAmendmentFiles.EnumCommand> listCommands=EobAmendmentFiles.GetCommands(_mode);
			for(int i=0;i<listCommands.Count;i++) {
				string tag=listCommands[i].ToString();
				switch(listCommands[i]) {
					case EobAmendmentFiles.EnumCommand.Print:
						toolBarMain.Add(Lan.g(this,"Print"),ToolBarPrint_Click,WpfControls.UI.EnumIcons.Print,tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.Delete:
						toolBarMain.Add(Lan.g(this,"Delete"),ToolBarDelete_Click,WpfControls.UI.EnumIcons.DeleteX,tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.ScanDoc:
						toolBarMain.AddSeparator();
						toolBarMain.Add(Lan.g(this,"Scan:"),null);
						toolBarMain.Add("",ToolBarScanDoc_Click,WpfControls.UI.EnumIcons.ImageSelectorDoc,toolTipText:Lan.g(this,"Scan Document"),tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.ScanMultiDoc:
						toolBarMain.Add("",ToolBarScanMulti_Click,WpfControls.UI.EnumIcons.ScanMulti,toolTipText:Lan.g(this,"Scan Multi-Page Document"),tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.Import:
						toolBarMain.AddSeparator();
						toolBarMain.Add(Lan.g(this,"Import"),ToolBarImport_Click,WpfControls.UI.EnumIcons.Import,toolTipText:Lan.g(this,"Import From File"),tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.Export:
						toolBarMain.Add(Lan.g(this,"Export"),ToolBarExport_Click,WpfControls.UI.EnumIcons.Export,toolTipText:Lan.g(this,"Export To File"),tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.Copy:
						toolBarMain.Add(Lan.g(this,"Copy"),ToolBarCopy_Click,WpfControls.UI.EnumIcons.Copy,toolTipText:Lan.g(this,"Copy displayed image to clipboard"),tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.Paste:
						toolBarMain.Add(Lan.g(this,"Paste"),ToolBarPaste_Click,WpfControls.UI.EnumIcons.Paste,toolTipText:Lan.g(this,"Paste From Clipboard"),tag:tag);
						break;
					case EobAmendmentFiles.EnumCommand.Close:
						toolBarMain.AddSeparator();
						toolBarMain.Add(Lan.g(this,"Close"),ToolBarClose_Click,toolTipText:Lan.g(this,"Close window"),tag:tag);
						break;
				}
			}
		}

		///<summary>Shows the PDF in the viewer, as ControlImageDisplay.LoadPdf does: the PDF JavaScript preference is applied before each navigation.
		///The file is the stored one with an A to Z folder, or a temp copy (cloud storage, images in the database).</summary>
		private async void LoadPdf(EobAmendmentFiles.FileEntry fileEntry,string localPathCloud) {
			string filePath="";
			bool isTempCopy=false;
			try {
				if(!RunWithCloudProgress(() => filePath=EobAmendmentFiles.GetLocalPath(fileEntry,out isTempCopy,localPathCloud),Lan.g(this,"Downloading..."))) {
					return;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
				MessageBox.Show(Lan.g(this,"File not found")+": "+fileEntry.FileName);
				return;
			}
			if(isTempCopy) {
				_listPathsTemp.Add(filePath);
			}
			_odWebView2FilePath=filePath;
			panelImage.Visible=false;
			_odWebView2.Visible=true;
			try {
				if(_odWebView2.CoreWebView2==null) {
					await _odWebView2.Init();//Throws exception if Microsoft WebView2 Runtime is not installed so need to have in try-catch.
				}
				if(_odWebView2FilePath!=filePath) {
					return;//Another file was selected while the viewer started.
				}
				_odWebView2.SetScriptEnabled(ImageHelper.IsPdfJavascriptAllowed());//JavaScript in PDFs only when the preference allows it, read for every PDF.
				_odWebView2.ODWebView2Navigate(filePath);
			}
			catch(Exception ex) {
				ex.DoNothing();
				//An exception can happen if they do not have Microsoft WebView2 Runtime installed.
			}
		}

		///<summary>Runs the action, inside a progress window with cloud storage, so uploads and downloads show progress, as in the Imaging module.
		///False if the user cancelled. Exceptions from the action are thrown here.</summary>
		private bool RunWithCloudProgress(Action action,string startingMessage) {
			if(!CloudStorage.IsCloudStorage) {
				action();
				return true;
			}
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=startingMessage;
			progressWin.ActionMain=action;
			progressWin.ShowDialog();
			return !progressWin.IsCancelled;
		}

		///<summary>Selects the amendment's file, if it has one, and shows it. localPathCloud is the file just imported.</summary>
		private void SelectAmendment(string localPathCloud="") {
			if(_listFileEntries.Count>0) {
				listFiles.SetSelected(0);
			}
			ShowSelected(localPathCloud);
		}

		///<summary>Selects the new EOB and shows it. localPathCloud is the file just imported.</summary>
		private void SelectEob(long eobAttachNum,string localPathCloud="") {
			int idx=_listFileEntries.FindIndex(x => x.EobAttachNum==eobAttachNum);
			if(idx>=0) {
				listFiles.SetSelected(idx);
			}
			ShowSelected(localPathCloud);
		}

		///<summary>Fits the image into panelImage, as the Imaging module's zoom slider does. Disabled while no image shows.</summary>
		private void SetZoomSliderToFit() {
			if(_bitmapShowing==null) {
				zoomSlider.IsEnabled=false;
				return;
			}
			zoomSlider.SetValueInitialFit(new System.Windows.Size(panelImage.Width,panelImage.Height),
				new System.Windows.Size(_bitmapShowing.Width,_bitmapShowing.Height),0);
			_pointTranslation=new Point(0,0);
		}

		///<summary>Print, Copy and the zoom slider for an image; Delete and Export for any selected file; scan, import, paste and close always.</summary>
		private void SetToolBarEnabled() {
			bool isSelected=GetSelectedEntry()!=null;
			bool isImage=_bitmapShowing!=null;
			toolBarMain.SetEnabled(EobAmendmentFiles.EnumCommand.Print.ToString(),isImage);
			toolBarMain.SetEnabled(EobAmendmentFiles.EnumCommand.Delete.ToString(),isSelected);
			toolBarMain.SetEnabled(EobAmendmentFiles.EnumCommand.Export.ToString(),isSelected);
			toolBarMain.SetEnabled(EobAmendmentFiles.EnumCommand.Copy.ToString(),isImage);
			for(int i=0;i<contextMenuStripList.Items.Count;i++) {
				contextMenuStripList.Items[i].Enabled=isSelected;
			}
		}

		///<summary>Shows the selected file: an image fitted, a PDF in the viewer, any other type not at all. localPathCloud is the file just imported,
		///with cloud storage, so it is not downloaded again.</summary>
		private void ShowSelected(string localPathCloud="") {
			ClearShowing();
			EobAmendmentFiles.FileEntry fileEntry=GetSelectedEntry();
			if(fileEntry!=null) {
				EobAmendmentFiles.EnumShowAs enumShowAs=EobAmendmentFiles.GetShowAs(fileEntry.FileName);
				if(enumShowAs==EobAmendmentFiles.EnumShowAs.Image) {
					Bitmap bitmap=null;
					try {
						RunWithCloudProgress(() => bitmap=EobAmendmentFiles.OpenImage(fileEntry,localPathCloud),Lan.g(this,"Downloading..."));
					}
					catch(Exception ex) {
						FriendlyException.Show(ex.Message,(ex.InnerException==null ? ex : ex.InnerException));
					}
					_bitmapShowing=bitmap;
					SetZoomSliderToFit();
				}
				else if(enumShowAs==EobAmendmentFiles.EnumShowAs.Pdf) {
					LoadPdf(fileEntry,localPathCloud);
				}
			}
			SetToolBarEnabled();
			panelImage.Invalidate();
		}
		#endregion Methods - Private
	}
}
