// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager {
	///<summary>TWAIN scanning with the same calls the program used with the commercial EZTwain library, implemented directly
	///on the standard TWAIN Data Source Manager. All calls are synchronous and must be made from the UI thread.
	///
	///Typical single image scan: OpenDefaultSource or OpenSource, optional Set* calls, Acquire (shows the scanner dialog unless
	///SetHideUI(true), runs modally, closes the source afterwards), check LastErrorCode, use the returned image handle with the
	///DIB_* functions, then DIB_Free.
	///
	///Several images in one session (sensors): OpenSource, SetMultiTransfer(true), then Acquire repeatedly until it returns
	///IntPtr.Zero or IsDone() is true, then CloseSource.
	///
	///Multipage document to PDF: OpenDefaultSource, Set* calls, AcquireMultipageFile.
	///
	///Error codes keep their EZTwain numbers: 19 user cancelled, 38 no pages, 76 paper jam, 5 data source failure.</summary>
	public static partial class EZTwain {
		///<summary>Native transfers: the scanner hands over a whole bitmap at once. The default.</summary>
		public const int XFERMECH_NATIVE=0;
		///<summary>File transfers. Not supported.</summary>
		public const int XFERMECH_FILE=1;
		///<summary>Memory transfers: the image arrives in strips. Better for very large images.</summary>
		public const int XFERMECH_MEMORY=2;
		///<summary>Returned by DIB_XResolution when the scanner did not report a resolution.</summary>
		public const int DefaultResolution=PdfPageWriter.DefaultDpi;
		private const int JpegQualityDefault=75;

		private static TwainSession _session;
		private static List<string> _listSourceNames=new List<string>();
		private static int _idxNextSourceName;
		private static int _jpegQuality=JpegQualityDefault;
		///<summary>-1 until PDF_SetCompression is called; then the JPEG quality for PDF pages.</summary>
		private static int _pdfQuality=-1;
		///<summary>Unit tests replace the data source manager with a simulated one.</summary>
		internal static DsmLoader DsmLoaderOverride;

		internal static TwainSession Session {
			get {
				if(_session==null) {
					DsmLoader dsmLoader=DsmLoaderOverride;
					if(dsmLoader==null) {
						dsmLoader=LoadNativeDsm;
					}
					_session=new TwainSession(dsmLoader);
					ConfigureSession(_session);
				}
				return _session;
			}
		}

		private static ITwainDsm LoadNativeDsm(out string errorText) {
			return NativeTwainDsm.Load(out errorText);
		}

		///<summary>Discards all state. For unit tests: capture.ini reads as empty and no windows are shown.</summary>
		internal static void Reset(DsmLoader dsmLoader) {
			Reset(dsmLoader,null);
		}

		///<summary>Discards all state. For unit tests. host replaces capture.ini, the capture folders and the windows; null gives
		///CaptureHost.CreateInert.</summary>
		internal static void Reset(DsmLoader dsmLoader,CaptureHost host) {
			if(_session!=null) {
				_session.Dispose();
				_session=null;
			}
			DsmLoaderOverride=dsmLoader;
			_listSourceNames=new List<string>();
			_idxNextSourceName=0;
			_jpegQuality=JpegQualityDefault;
			_pdfQuality=-1;
			ResetCapture(host??CaptureHost.CreateInert());
		}

		#region Sources
		///<summary>Shows the standard "Select Source" dialog. The choice becomes the default source for OpenDefaultSource.
		///Returns false if the user cancelled or no dialog could be shown.</summary>
		public static bool SelectImageSource(IntPtr hwnd) {
			Session.ClearError();
			return Session.UserSelect(hwnd);
		}

		///<summary>Opens the default source (the one last chosen in SelectImageSource), closing any source already open. With [Twain]
		///RestrictSources=1 in capture.ini, a source not listed in [Twain] AllowedSources is refused with a notice.</summary>
		public static bool OpenDefaultSource() {
			Session.ClearError();
			CloseEnabledSourceFirst();
			ApplyCaptureConfig();
			return Session.OpenSource(null);
		}

		///<summary>Opens the source with this product name (as returned by GetNextSourceName), closing any source already open.
		///An empty name opens the default source. With [Twain] RestrictSources=1 in capture.ini, a source not listed in [Twain]
		///AllowedSources is refused with a notice.</summary>
		public static bool OpenSource(string sourceName) {
			Session.ClearError();
			CloseEnabledSourceFirst();
			ApplyCaptureConfig();
			return Session.OpenSource(sourceName);
		}

		///<summary>Closes the open source, discarding any images not yet transferred (for a source opened by name they go to the capture
		///quarantine instead), and closes the Data Source Manager. The manager's library stays loaded for the next scan.</summary>
		public static bool CloseSource() {
			CloseSourceGuarded();
			return true;
		}

		///<summary>Reads the list of installed sources. Then call GetNextSourceName until it returns false.</summary>
		public static bool GetSourceList() {
			Session.ClearError();
			_listSourceNames=new List<string>();
			_idxNextSourceName=0;
			List<TW_IDENTITY> listIdentities=Session.GetSources();
			if(listIdentities==null) {
				return false;
			}
			_listSourceNames=listIdentities.Select(x => x.ProductName??"").Where(x => x!="").Distinct().ToList();
			return true;
		}

		///<summary>Puts the next source name from GetSourceList into sourceName, replacing its contents. False at the end.</summary>
		public static bool GetNextSourceName(StringBuilder sourceName) {
			if(sourceName==null || _idxNextSourceName>=_listSourceNames.Count) {
				return false;
			}
			sourceName.Length=0;
			sourceName.Append(_listSourceNames[_idxNextSourceName]);
			_idxNextSourceName++;
			return true;
		}
		#endregion Sources

		#region Settings
		///<summary>true hides the scanner's own dialog when acquiring (the scan starts right away). Stays in effect until changed.
		///Some scanners always show their dialog.</summary>
		public static void SetHideUI(bool isHidden) {
			Session.HideUI=isHidden;
		}

		///<summary>Sets the horizontal and vertical resolution of the open source, in dots per inch.</summary>
		public static bool SetResolution(double dpi) {
			Session.ClearError();
			return Session.SetResolution(dpi);
		}

		///<summary>Sets the pixel type of the open source: 0 black and white, 1 grayscale, 2 color.</summary>
		public static bool SetPixelType(int pixelType) {
			Session.ClearError();
			return Session.SetPixelType(pixelType);
		}

		///<summary>JPEG quality, 0 to 100, used for PDF pages when PDF_SetCompression has not been called.</summary>
		public static void SetJpegQuality(int quality) {
			_jpegQuality=Math.Max(0,Math.Min(100,quality));
		}

		///<summary>Chooses how images move from the open source: XFERMECH_NATIVE or XFERMECH_MEMORY. If the source does not
		///support the choice, native transfers are used and false is returned.</summary>
		public static bool SetXferMech(int mechanism) {
			Session.ClearError();
			return Session.SetTransferMechanism(mechanism);
		}

		///<summary>true leaves the source open after each Acquire so that the remaining images of a batch can be acquired.
		///Turned off again by CloseSource.</summary>
		public static void SetMultiTransfer(bool isMultiTransfer) {
			Session.MultiTransfer=isMultiTransfer;
		}

		///<summary>Turns duplex (two sided) scanning on or off on the open source. Returns false if the source refused.</summary>
		public static bool EnableDuplex(bool isEnabled) {
			Session.ClearError();
			return Session.SetDuplex(isEnabled);
		}

		///<summary>Sets the compression of PDF pages written by AcquireMultipageFile. The first argument is ignored (callers pass a
		///window handle). quality is the JPEG quality, 0 to 100, for grayscale and color pages; 100 stores them losslessly.
		///Black and white pages are always lossless.</summary>
		public static void PDF_SetCompression(int reserved,int quality) {
			_pdfQuality=Math.Max(0,Math.Min(100,quality));
		}
		#endregion Settings

		#region Acquire
		///<summary>Acquires one image and returns its handle, or IntPtr.Zero (check LastErrorCode: 19 means the user cancelled).
		///Opens the default source if none is open. Runs modally: the top-level window containing hwnd is disabled until the
		///scan ends; for a source opened by name (OpenSource), every visible window of the calling thread is (see
		///DisableThreadWindowsDuringAcquire). Unless SetMultiTransfer(true) was called, the source is closed afterwards. Free the handle
		///with DIB_Free.</summary>
		public static IntPtr Acquire(IntPtr hwnd) {
			return Acquire(hwnd,0);
		}

		///<summary>Acquire, for a caller that knows the patient the image will be filed under. patNum goes only into the sidecar of the
		///journal copy of the image, for recovery; 0 means unknown.</summary>
		public static IntPtr Acquire(IntPtr hwnd,long patNum) {
			if(Session.State<4) {
				ApplyCaptureConfig();//Acquire opens the default source.
			}
			DibImage dib;
			if(Session.State>=4 && Session.IsOpenedByName && !Session.IsAcquiring) {
				dib=AcquireByName(hwnd,patNum);
			}
			else {
				dib=Session.Acquire(hwnd);
			}
			return DibHandleTable.Add(dib);
		}

		///<summary>Acquires every page the scanner delivers in one session (for example a whole document feeder) and writes them
		///to fileName, which must end in .pdf. Opens the default source if none is open and always closes it. Returns the number
		///of pages written. LastErrorCode is 0 on success, 19 if cancelled, 38 if no pages were scanned, 76 for a paper jam.
		///If the scan fails part way, the pages scanned before the failure are still written and LastErrorCode reports the failure.</summary>
		public static int AcquireMultipageFile(IntPtr hwnd,string fileName) {
			Session.ClearError();
			if(string.IsNullOrWhiteSpace(fileName)) {
				Session.CloseSource();
				Session.SetError(EZTwainErrorCode.EZTEC_BAD_FILENAME,"No file name was given for the scanned pages.");
				return 0;
			}
			if(!string.Equals(Path.GetExtension(fileName),".pdf",StringComparison.OrdinalIgnoreCase)) {
				Session.CloseSource();
				Session.SetError(EZTwainErrorCode.EZTEC_UNK_WRITE_FF,"Scanned pages can only be saved as PDF.");
				return 0;
			}
			if(Session.State<4) {
				ApplyCaptureConfig();//AcquireAll opens the default source.
			}
			int quality=_pdfQuality>=0 ? _pdfQuality : _jpegQuality;
			using(PdfPageWriter pdfPageWriter=new PdfPageWriter(quality)) {
				Session.AcquireAll(hwnd,dib => AddPdfPage(pdfPageWriter,dib));
				EZTwainErrorCode errorCode=Session.ErrorCode;
				if(pdfPageWriter.PageCount==0 || errorCode==EZTwainErrorCode.EZTEC_USER_CANCEL) {
					return 0;
				}
				try {
					pdfPageWriter.Save(fileName);
				}
				catch(Exception ex) {
					Session.SetError(EZTwainErrorCode.EZTEC_PDF_WRITE_ERR,"The scanned pages could not be saved to "+fileName+": "+ex.Message);
					return 0;
				}
				return pdfPageWriter.PageCount;
			}
		}

		private static void AddPdfPage(PdfPageWriter pdfPageWriter,DibImage dib) {
			try {
				pdfPageWriter.AddPage(dib);
			}
			catch(ImagingDeviceException) {
				throw;
			}
			catch(Exception ex) {
				throw new ImagingDeviceException("A scanned page could not be added to the PDF: "+ex.Message,EZTwainErrorCode.EZTEC_PDF_WRITE_ERR);
			}
		}

		///<summary>With SetMultiTransfer(true): true when the batch is finished and no more images are waiting.</summary>
		public static bool IsDone() {
			return Session.IsDone();
		}
		#endregion Acquire

		#region Images
		///<summary>Releases an image handle from Acquire. Freeing IntPtr.Zero, or a handle that was already freed, does nothing.</summary>
		public static void DIB_Free(IntPtr hdib) {
			DibHandleTable.Free(hdib);
		}

		///<summary>Creates a GDI DIB section (HBITMAP) from the image and frees the image handle. Delete the HBITMAP with
		///Twain.DeleteObject when done. Returns IntPtr.Zero on failure.</summary>
		public static IntPtr DIB_ToDibSection(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			if(dib==null) {
				return IntPtr.Zero;
			}
			try {
				IntPtr hBitmap=dib.ToHBitmap();
				DibHandleTable.Free(hdib);
				return hBitmap;
			}
			catch(ImagingDeviceException ex) {
				Session.SetError(ex.ErrorCode,ex.Message);
				return IntPtr.Zero;
			}
		}

		///<summary>Creates an independent System.Drawing.Bitmap from the image. The image handle stays valid; free it with DIB_Free.
		///Returns null on failure.</summary>
		public static Image DIB_ToImage(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			if(dib==null) {
				return null;
			}
			try {
				return dib.ToBitmap();
			}
			catch(Exception ex) {
				Session.SetError(EZTwainErrorCode.EZTEC_BAD_DIB,"The scanned image could not be converted: "+ex.Message);
				return null;
			}
		}

		///<summary>Horizontal resolution in dots per inch. DefaultResolution if the scanner did not report one, 0 for an invalid handle.</summary>
		public static int DIB_XResolution(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			if(dib==null) {
				return 0;
			}
			return dib.XResolutionDpi>0 ? dib.XResolutionDpi : DefaultResolution;
		}

		///<summary>Vertical resolution in dots per inch. DefaultResolution if the scanner did not report one, 0 for an invalid handle.</summary>
		public static int DIB_YResolution(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			if(dib==null) {
				return 0;
			}
			return dib.YResolutionDpi>0 ? dib.YResolutionDpi : DefaultResolution;
		}

		///<summary>Width in pixels, or 0 for an invalid handle.</summary>
		public static int DIB_Width(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			return dib==null ? 0 : dib.Width;
		}

		///<summary>Height in pixels, or 0 for an invalid handle.</summary>
		public static int DIB_Height(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			return dib==null ? 0 : dib.Height;
		}

		///<summary>Bits per pixel (1, 4, 8, 16, 24 or 32), or 0 for an invalid handle.</summary>
		public static int DIB_Depth(IntPtr hdib) {
			DibImage dib=GetDib(hdib);
			return dib==null ? 0 : dib.BitCount;
		}

		private static DibImage GetDib(IntPtr hdib) {
			DibImage dib=DibHandleTable.Get(hdib);
			if(dib==null) {
				Session.SetError(EZTwainErrorCode.EZTEC_BAD_DIB,"The image handle is not valid.");
			}
			return dib;
		}
		#endregion Images

		#region Errors
		///<summary>The error code of the last operation, 0 if it succeeded. See EZTwainErrorCode.</summary>
		public static int LastErrorCode() {
			return (int)Session.ErrorCode;
		}

		///<summary>A description of the last error, or "" if the last operation succeeded.</summary>
		public static string LastErrorText() {
			if(Session.ErrorText!="" || Session.ErrorCode==EZTwainErrorCode.EZTEC_NONE) {
				return Session.ErrorText;
			}
			return "Scanning error "+(int)Session.ErrorCode+" ("+Session.ErrorCode+").";
		}
		#endregion Errors
	}
}
