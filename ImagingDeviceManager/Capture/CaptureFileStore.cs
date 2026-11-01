// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.Journal;

namespace ImagingDeviceManager.Capture {
	///<summary>The sidecar keys the capture session adds through TwainEnableRequest.SidecarFields and the library writes for every session image,
	///so that the quarantine window and the journal recovery (design 7.1, 7.12) can file an image where it was meant to go. The values are
	///Open Dental row numbers and text as the caller gives them; the library does not interpret them.</summary>
	public static class CaptureSidecarKeys {
		///<summary>The target mount's MountNum, or empty for a single image with no mount.</summary>
		public const string MountNum="MountNum";
		///<summary>The target slot's MountItemNum, or empty.</summary>
		public const string MountItemNum="MountItemNum";
		///<summary>The target slot's ItemOrder (its number in the mount), or empty.</summary>
		public const string SlotItemOrder="SlotItemOrder";
		///<summary>The tooth numbers of the target: the slot's, or the banner's for a single image.</summary>
		public const string ToothNumbers="ToothNumbers";
		///<summary>The image category (DefNum) the session saves into.</summary>
		public const string DocCategory="DocCategory";
		///<summary>The provider the session stamps on its documents.</summary>
		public const string ProvNum="ProvNum";
		///<summary>The imagingdevice row the session runs for ([Device.N]).</summary>
		public const string ImagingDeviceNum="ImagingDeviceNum";
		///<summary>Written by the library: the image's 1-based position in its enable, and "1" when the enable ran continuously (design 7.4).</summary>
		public const string IndexInEnable="IndexInEnable";
		public const string ContinuousEnable="ContinuousEnable";
		///<summary>"1": MountItemNum, SlotItemOrder and ToothNumbers are the target the capture session meant this image for (it sets them when it
		///places or quarantines the image). Without it they are its enable's target, which is not the image's own for a later image of a
		///continuous enable in a mount, nor for a redelivered image (placed into the target current when the operator confirms it).</summary>
		public const string SlotOfImage="SlotOfImage";
		///<summary>"1": a redelivered image (Redelivered=1) that the operator confirmed for the bound patient in the session window (design
		///7.12), so its PatNum is that patient's. Without it a redelivered image is never offered for Assign, whatever its PatNum says.</summary>
		public const string RedeliveryConfirmed="RedeliveryConfirmed";
		///<summary>Written by the library: TW_IMAGEINFO PixelType, BitsPerSample, and whether 16 bits were negotiated (IsBitDepth16InEffect).</summary>
		public const string PixelType="PixelType";
		public const string BitsPerSample="BitsPerSample";
		public const string BitDepth16InEffect="BitDepth16InEffect";
		///<summary>Written when a quarantine file or journal copy is filed or discarded (CaptureFileStore.Resolve): what happened to it.</summary>
		public const string Disposition="Disposition";
		public const string DispositionAt="DispositionAt";
		public const string DispositionComputer="DispositionComputer";
		public const string DispositionWindowsUser="DispositionWindowsUser";
	}

	///<summary>Where a capture file is.</summary>
	public enum CaptureFileKind {
		///<summary>Capture\quarantine (an unassigned capture, design 7.12).</summary>
		Quarantine,
		///<summary>Capture\journal (a recovery copy, design 7.1).</summary>
		Journal,
	}

	///<summary>What was done with a quarantine file or a journal copy.</summary>
	public enum CaptureFileDisposition {
		///<summary>Filed for the patient the session was bound to (Assign, or Recover recent captures).</summary>
		Assigned,
		///<summary>Filed for another patient chosen with the patient search (Reassign).</summary>
		Reassigned,
		///<summary>Discarded (ImageDelete).</summary>
		Discarded,
	}

	///<summary>One image file of the quarantine or the journal with what its sidecar says (design 7.12 "Contents"): the receive time, the source,
	///the computer and the Windows user for every file; for a capture-session file also the originating PatNum and patient name, the session GUID,
	///the slot, the mount and the metadata keys. A file of the CAP-1 library (phases 0-2, the legacy loop), a Test capture and a declined
	///redelivered image record no patient: IsPatientUnknown. Read-only; CaptureFileStore makes them.</summary>
	public sealed class CaptureFileEntry {
		private readonly Dictionary<string,string> _dictFields;

		internal CaptureFileEntry(CaptureFileKind kind,string imagePath,string sidecarPath,Dictionary<string,string> dictFields,DateTime lastWriteTime) {
			Kind=kind;
			ImagePath=imagePath;
			SidecarPath=sidecarPath;
			_dictFields=dictFields??new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			LastWriteTime=lastWriteTime;
		}

		public CaptureFileKind Kind { get; }
		public string ImagePath { get; }
		///<summary>The sidecar file, or null when the image has none.</summary>
		public string SidecarPath { get; }
		///<summary>The image file's last-write time, the receive time when the sidecar has none.</summary>
		public DateTime LastWriteTime { get; }
		///<summary>Every Key=Value line of the sidecar (keys ignore case), for the details view.</summary>
		public IReadOnlyDictionary<string,string> Fields => _dictFields;

		public string FileName => Path.GetFileName(ImagePath??"");
		///<summary>".bmp", ".tif", ".png" or ".dcm", lower case.</summary>
		public string Extension => Path.GetExtension(ImagePath??"").ToLowerInvariant();
		public bool HasSidecar => SidecarPath!=null;

		///<summary>The value of key, or "".</summary>
		public string Get(string key) {
			string value;
			return _dictFields.TryGetValue(key??"",out value) ? value??"" : "";
		}

		///<summary>The receive time (sidecar ReceivedAt, local), else LastWriteTime.</summary>
		public DateTime ReceivedAt {
			get {
				DateTimeOffset dateTimeOffset;
				if(DateTimeOffset.TryParseExact(Get("ReceivedAt"),"yyyy-MM-ddTHH:mm:ss.fffzzz",CultureInfo.InvariantCulture,DateTimeStyles.None,
					out dateTimeOffset))
				{
					return dateTimeOffset.LocalDateTime;
				}
				return LastWriteTime;
			}
		}

		///<summary>The originating patient (sidecar PatNum), or 0 for "patient unknown".</summary>
		public long PatNum => GetLong("PatNum");
		///<summary>Design 7.12: no originating patient (CAP-1 files, Test captures, declined redelivered images, phase-0 journal copies).</summary>
		public bool IsPatientUnknown => PatNum<=0;
		///<summary>The patient's name as the session recorded it, or "patient unknown". A journal copy records no name ("").</summary>
		public string PatientName => IsPatientUnknown ? QuarantineWriter.PatientUnknown : Get("Patient");
		public Guid? SessionGuid {
			get {
				Guid guid;
				return Guid.TryParse(Get("SessionGuid"),out guid) ? guid : (Guid?)null;
			}
		}
		///<summary>The target in words: the quarantine's Slot, else the journal's Target ("Slot 3 (teeth 2,3)", "Single image").</summary>
		public string Slot {
			get {
				string slot=Get("Slot");
				return slot!="" ? slot : Get("Target");
			}
		}
		public long MountNum => GetLong(CaptureSidecarKeys.MountNum);
		///<summary>The image's position in its enable (1 when the sidecar does not say).</summary>
		public int IndexInEnable => Math.Max(1,(int)GetLong(CaptureSidecarKeys.IndexInEnable));
		public bool IsContinuousEnable => Get(CaptureSidecarKeys.ContinuousEnable)=="1";
		///<summary>MountItemNum, SlotItemOrder and ToothNumbers are this image's target (CaptureSidecarKeys.SlotOfImage): the session said so, or
		///they are its enable's and that is where the session would have placed it (the first image of an enable, every image of a per-slot
		///enable, also one drained during a stop, and every image of single-target mode). False for a later image of a continuous enable in a
		///mount, Normal or drained during its stop, and for a redelivered image, when the session did not record the target: the target moved on
		///after each image of that enable, so the slot is not known (design 7.12 Assign files it with no slot and no tooth numbers).</summary>
		public bool IsSlotKnown {
			get {
				if(Get(CaptureSidecarKeys.SlotOfImage)=="1") {
					return true;
				}
				if(IsRedelivered) {
					return false;
				}
				return !(MountNum>0 && IsContinuousEnable && IndexInEnable>1);
			}
		}
		public long MountItemNum => GetLong(CaptureSidecarKeys.MountItemNum);
		public int SlotItemOrder => (int)GetLong(CaptureSidecarKeys.SlotItemOrder);
		public string ToothNumbers => Get(CaptureSidecarKeys.ToothNumbers);
		public long DocCategory => GetLong(CaptureSidecarKeys.DocCategory);
		public long ProvNum => GetLong(CaptureSidecarKeys.ProvNum);
		public long ImagingDeviceNum => GetLong(CaptureSidecarKeys.ImagingDeviceNum);
		///<summary>The source's ProductName.</summary>
		public string Source => Get("Source");
		public string Computer => Get("Computer");
		public string WindowsUser => Get("WindowsUser");
		///<summary>The source's capture id (metadata captureId), or "" when not known.</summary>
		public string CaptureId => Get("CaptureId");
		public int? Sequence {
			get {
				int sequence;
				return int.TryParse(Get("Sequence"),NumberStyles.Integer,CultureInfo.InvariantCulture,out sequence) ? sequence : (int?)null;
			}
		}
		///<summary>The raw capture metadata (the 0x124B text), or "" (the caller parses acquiredAt from it).</summary>
		public string MetadataJson => Get("Metadata");
		///<summary>Why the file is in the quarantine.</summary>
		public string Reason => Get("Reason");
		///<summary>Journal only: Caller, Quarantine or None (CaptureJournal).</summary>
		public string Delivered => Get("Delivered");
		///<summary>A journal copy whose image went to the quarantine instead of the caller (Delivered=Quarantine): it is filed from the quarantine.</summary>
		public bool IsCopyOfQuarantined => Kind==CaptureFileKind.Journal && Delivered==CaptureJournal.DeliveredQuarantine;
		public bool IsRedelivered => Get("Redelivered")=="1";
		///<summary>A redelivered image the operator confirmed for the bound patient (CaptureSidecarKeys.RedeliveryConfirmed).</summary>
		public bool IsRedeliveryConfirmed => IsRedelivered && Get(CaptureSidecarKeys.RedeliveryConfirmed)=="1";
		public int Width => (int)GetLong("Width");
		public int Height => (int)GetLong("Height");
		public int BitsPerPixel => (int)GetLong("BitsPerPixel");
		public string Sha256 => Get("Sha256");
		///<summary>A 16-bit copy (.tif): it is filed only as DICOM, under the session's 16-bit conditions (design 7.8).</summary>
		public bool IsGray16 => Extension==Gray16Tiff.Extension;
		///<summary>The file was filed or discarded before (CaptureFileStore.Resolve wrote its Disposition): a journal copy then stays in the
		///journal until rotation removes it, and is not offered again.</summary>
		public bool IsResolved => Get(CaptureSidecarKeys.Disposition)!="";
		///<summary>Design 7.12 "Visibility": received more than CaptureNoticeText.UnassignedWarningDays days before now.</summary>
		public bool IsOverdue(DateTime now) {
			return (now-ReceivedAt).TotalDays>CaptureNoticeText.UnassignedWarningDays;
		}

		private long GetLong(string key) {
			long value;
			return long.TryParse(Get(key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value) ? value : 0;
		}
	}

	///<summary>The capture folders as the quarantine window and the journal recovery use them (design 7.1, 7.12). CaptureFileStore is the real
	///one.</summary>
	public interface ICaptureFileStore {
		///<summary>The image files under Capture\quarantine, with their sidecars, newest first.</summary>
		List<CaptureFileEntry> ListQuarantine();
		///<summary>The journal copies in Capture\journal, with their sidecars, newest first.</summary>
		List<CaptureFileEntry> ListJournal();
		///<summary>The image of entry as the session would have received it, or null with error.</summary>
		AcquiredImage ReadImage(CaptureFileEntry entry,out string error);
		///<summary>Records that entry was filed or discarded, without losing it (see CaptureFileStore.Resolve). Returns false with error when
		///nothing was changed.</summary>
		bool Resolve(CaptureFileEntry entry,CaptureFileDisposition disposition,IList<KeyValuePair<string,string>> listFields,out string pathResolved,
			out string error);
	}

	///<summary>Lists, reads and resolves the quarantine files and journal copies under %ProgramData%\OpenPMS-Dental\Imaging\Capture (design 7.1,
	///7.12). UI thread only, like the windows that use it; it never throws.
	///
	///What a filed or discarded quarantine file becomes (Resolve): the image and its sidecar move to Capture\quarantine-resolved, under the same
	///session folder, and the sidecar gets the disposition appended (what, when, where, by whom, the document or the reason). Nothing is deleted:
	///the security-log entry names the file and the file still exists, so an administrator can check what was filed or discarded, and a mistaken
	///Discard can be undone by hand; the folder is outside Capture\quarantine, so the file no longer counts as an unassigned capture and is not
	///listed again. There is no automatic purge of either folder (design 7.12); the runbook says when an administrator may remove resolved
	///files. A journal copy stays where it is, because rotation owns the journal: its sidecar gets the disposition, so it is not offered again,
	///and an only copy (named with CaptureJournal.OnlyCopySuffix) gets its plain name back, because once filed it is no longer the only copy and
	///rotation may remove it.</summary>
	public sealed class CaptureFileStore:ICaptureFileStore {
		///<summary>The image extensions the quarantine counts (QuarantineWriter.CountImages) and lists.</summary>
		private static readonly string[] _arrayExtensions={ ".bmp",".tif",".png",".dcm" };
		private readonly CaptureFolders _folders;
		private readonly Func<DateTime> _funcNow;

		internal CaptureFileStore(CaptureFolders folders,Func<DateTime> funcNow) {
			_folders=folders??throw new ArgumentNullException(nameof(folders));
			_funcNow=funcNow??(() => DateTime.Now);
		}

		///<summary>The folders under %ProgramData%\OpenPMS-Dental\Imaging.</summary>
		public static CaptureFileStore CreateDefault() {
			return new CaptureFileStore(CaptureFolders.CreateDefault(),() => DateTime.Now);
		}

		///<summary>For tests and tools: folderImaging stands in for the Imaging folder, and no ACL is changed.</summary>
		public static CaptureFileStore CreateForFolder(string folderImaging,Func<DateTime> funcNow=null) {
			return new CaptureFileStore(CaptureFolders.CreateForFolder(folderImaging),funcNow);
		}

		#region List
		///<summary>A file that was filed or discarded but could not be moved out (its sidecar records the disposition, see Resolve) is left out:
		///it is no longer an unassigned capture.</summary>
		public List<CaptureFileEntry> ListQuarantine() {
			return List(CaptureFileKind.Quarantine,CaptureFolders.SubfolderQuarantine,SearchOption.AllDirectories).Where(x => !x.IsResolved).ToList();
		}

		public List<CaptureFileEntry> ListJournal() {
			return List(CaptureFileKind.Journal,CaptureFolders.SubfolderJournal,SearchOption.TopDirectoryOnly);
		}

		private List<CaptureFileEntry> List(CaptureFileKind kind,string subfolder,SearchOption searchOption) {
			List<CaptureFileEntry> listEntries=new List<CaptureFileEntry>();
			try {
				string folder=_folders.GetPath(subfolder);
				if(folder==null || !Directory.Exists(folder)) {
					return listEntries;//never created to list
				}
				foreach(FileInfo fileInfo in new DirectoryInfo(folder).GetFiles("*",searchOption)) {
					if(!_arrayExtensions.Contains(fileInfo.Extension.ToLowerInvariant())) {
						continue;
					}
					CaptureFileEntry entry=ReadEntry(kind,fileInfo);
					if(entry!=null) {
						listEntries.Add(entry);
					}
				}
			}
			catch {
				//a folder that cannot be read lists what was read so far
			}
			return listEntries.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.FileName,StringComparer.Ordinal).ToList();
		}

		private static CaptureFileEntry ReadEntry(CaptureFileKind kind,FileInfo fileInfo) {
			try {
				string pathSidecar=Path.Combine(fileInfo.DirectoryName,Path.GetFileNameWithoutExtension(fileInfo.Name)+CaptureFiles.SidecarExtension);
				Dictionary<string,string> dictFields;
				if(File.Exists(pathSidecar)) {
					dictFields=CaptureFiles.ReadSidecar(pathSidecar);
				}
				else {
					pathSidecar=null;
					dictFields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
				}
				return new CaptureFileEntry(kind,fileInfo.FullName,pathSidecar,dictFields,fileInfo.LastWriteTime);
			}
			catch {
				return null;
			}
		}
		#endregion List

		#region Read
		///<summary>The image as the capture session would have received it: a .bmp as its DIB, unchanged; a .tif (Gray16Tiff) as its 16-bit samples,
		///unchanged; a .png as a 24-bit DIB. The sidecar gives what the file does not: the metadata, the receive time, the source, the transfer and
		///the negotiated bit depth, the pixel type and flavor, and the target label. A .dcm is not read. Never throws.</summary>
		public AcquiredImage ReadImage(CaptureFileEntry entry,out string error) {
			error="";
			if(entry==null || string.IsNullOrEmpty(entry.ImagePath)) {
				error="No file.";
				return null;
			}
			try {
				byte[] bytes=File.ReadAllBytes(entry.ImagePath);
				AcquiredImage image=new AcquiredImage();
				switch(entry.Extension) {
					case ".bmp":
						if(!ReadBmp(bytes,image,out error)) {
							return null;
						}
						break;
					case ".tif":
						if(!ReadTif(bytes,entry,image,out error)) {
							return null;
						}
						break;
					case ".png":
						if(!ReadPng(bytes,image,out error)) {
							return null;
						}
						break;
					default:
						error="A "+entry.Extension+" file cannot be read back as a capture.";
						return null;
				}
				ApplySidecar(entry,image);
				return image;
			}
			catch(Exception ex) {
				error="The file "+entry.FileName+" could not be read: "+ex.Message;
				return null;
			}
		}

		private static bool ReadBmp(byte[] bytes,AcquiredImage image,out string error) {
			error="";
			if(bytes.Length<14+DibImage.BitmapInfoHeaderSize || bytes[0]!='B' || bytes[1]!='M') {
				error="The file is not a bitmap.";
				return false;
			}
			byte[] dib=new byte[bytes.Length-14];
			Buffer.BlockCopy(bytes,14,dib,0,dib.Length);
			DibImage dibImage=DibImage.FromPackedDib(dib);
			image.Dib=dib;
			image.Width=dibImage.Width;
			image.Height=dibImage.Height;
			image.BitsPerPixel=dibImage.BitCount;
			image.BitsPerSample=dibImage.BitCount>=24 ? 8 : dibImage.BitCount;
			image.XDpi=dibImage.XResolutionDpi;
			image.YDpi=dibImage.YResolutionDpi;
			return true;
		}

		private static bool ReadTif(byte[] bytes,CaptureFileEntry entry,AcquiredImage image,out string error) {
			int width;
			int height;
			ushort[] arraySamples;
			double xDpi;
			double yDpi;
			bool isWhiteIsZero;
			if(!Gray16Tiff.TryDecode(bytes,out width,out height,out arraySamples,out xDpi,out yDpi,out isWhiteIsZero,out error)) {
				return false;
			}
			image.Gray16=arraySamples;
			image.Width=width;
			image.Height=height;
			image.BitsPerPixel=16;
			image.BitsPerSample=16;
			image.XDpi=xDpi;
			image.YDpi=yDpi;
			image.PixelType=AcquiredImage.PixelTypeGray;
			image.PixelFlavor=isWhiteIsZero ? AcquiredImage.PixelFlavorVanilla : AcquiredImage.PixelFlavorChocolate;
			return true;
		}

		private static bool ReadPng(byte[] bytes,AcquiredImage image,out string error) {
			error="";
			using(MemoryStream memoryStream=new MemoryStream(bytes))
			using(Bitmap bitmapRead=new Bitmap(memoryStream))
			using(Bitmap bitmap=bitmapRead.Clone(new Rectangle(0,0,bitmapRead.Width,bitmapRead.Height),PixelFormat.Format24bppRgb)) {
				int width=bitmap.Width;
				int height=bitmap.Height;
				int stride=DibImage.GetStride(width,24);
				byte[] arrayPixels=new byte[stride*height];
				BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
				try {
					byte[] arrayRow=new byte[width*3];
					for(int y=0;y<height;y++) {
						Marshal.Copy(IntPtr.Add(bitmapData.Scan0,y*bitmapData.Stride),arrayRow,0,arrayRow.Length);
						Buffer.BlockCopy(arrayRow,0,arrayPixels,(height-1-y)*stride,arrayRow.Length);//a DIB is bottom-up
					}
				}
				finally {
					bitmap.UnlockBits(bitmapData);
				}
				int pelsPerMeterX=DibImage.DpiToPelsPerMeter(bitmapRead.HorizontalResolution);
				int pelsPerMeterY=DibImage.DpiToPelsPerMeter(bitmapRead.VerticalResolution);
				DibImage dibImage=DibImage.Create(width,height,24,null,arrayPixels,pelsPerMeterX,pelsPerMeterY);
				image.Dib=dibImage.Data;
				image.Width=width;
				image.Height=height;
				image.BitsPerPixel=24;
				image.BitsPerSample=8;
				image.XDpi=bitmapRead.HorizontalResolution;
				image.YDpi=bitmapRead.VerticalResolution;
			}
			return true;
		}

		///<summary>What the sidecar says about the image (CaptureRecord and TwainCaptureSession.BuildRecord wrote it).</summary>
		private static void ApplySidecar(CaptureFileEntry entry,AcquiredImage image) {
			image.ReceivedAt=entry.ReceivedAt;
			image.SourceName=entry.Source;
			string metadata=entry.MetadataJson;
			image.MetadataJson=metadata=="" ? null : metadata;
			image.TargetLabel=entry.Slot;
			image.IsRedelivery=entry.IsRedelivered;
			double xDpi;
			if(image.XDpi<=0 && double.TryParse(entry.Get("XDpi"),NumberStyles.Float,CultureInfo.InvariantCulture,out xDpi)) {
				image.XDpi=xDpi;
			}
			double yDpi;
			if(image.YDpi<=0 && double.TryParse(entry.Get("YDpi"),NumberStyles.Float,CultureInfo.InvariantCulture,out yDpi)) {
				image.YDpi=yDpi;
			}
			int pixelType;
			if(!image.IsGray16) {
				//Not recorded by CAP-1 sidecars: -1 lets the saver decide from the pixels (RadiographSavePlanner.GetImgType).
				image.PixelType=int.TryParse(entry.Get(CaptureSidecarKeys.PixelType),NumberStyles.Integer,CultureInfo.InvariantCulture,out pixelType)
					? pixelType : -1;
			}
			int pixelFlavor;
			if(int.TryParse(entry.Get("PixelFlavor"),NumberStyles.Integer,CultureInfo.InvariantCulture,out pixelFlavor)) {
				image.PixelFlavor=pixelFlavor;
			}
			int requestedBits;
			image.RequestedBits=int.TryParse(entry.Get("RequestedBits"),NumberStyles.Integer,CultureInfo.InvariantCulture,out requestedBits)
				? requestedBits : 8;
			TwainTransferMechanism transferMechanism;
			if(Enum.TryParse(entry.Get("TransferMechanism"),out transferMechanism)) {
				image.TransferMechanism=transferMechanism;
			}
			string bitDepth16=entry.Get(CaptureSidecarKeys.BitDepth16InEffect);
			//Sidecars before CAP-4 do not record it: a 16-bit copy from a 16-bit memory enable is what the session asked for.
			image.IsBitDepth16InEffect=bitDepth16!="" ? bitDepth16=="1"
				: image.IsGray16 && image.RequestedBits==16 && image.TransferMechanism==TwainTransferMechanism.Memory;
			image.IsDeliveredAs8BitIn16BitEnable=entry.Get("DeliveredAs8BitIn16BitEnable")=="1";
			AcquiredImageKind kind;
			if(Enum.TryParse(entry.Get("ImageKind"),out kind)) {
				image.Kind=kind;
			}
			if(entry.Kind==CaptureFileKind.Quarantine) {
				image.QuarantinePath=entry.ImagePath;
			}
			else {
				image.JournalPath=entry.ImagePath;
			}
		}
		#endregion Read

		#region Resolve
		///<summary>Records that entry was filed (Assigned, Reassigned) or Discarded. listFields are more Key=Value lines for the sidecar (the
		///document, the patients, the user, the reason). A quarantine file moves to Capture\quarantine-resolved\&lt;its session folder&gt; with its
		///sidecar, which gets the disposition appended; when it cannot be moved (another program holds it, the folder is not usable), its sidecar
		///gets the disposition where it is, so it is never offered or counted again (true, with the reason in error). A journal copy keeps its
		///place, its sidecar gets the disposition, and an only copy gets its plain name back (see the class). pathResolved is where the image is
		///afterwards. Returns false with error when nothing could be recorded; a sidecar that could not be completed after the move is reported
		///in error with true.</summary>
		public bool Resolve(CaptureFileEntry entry,CaptureFileDisposition disposition,IList<KeyValuePair<string,string>> listFields,out string pathResolved,
			out string error)
		{
			pathResolved=entry?.ImagePath;
			error="";
			if(entry==null || string.IsNullOrEmpty(entry.ImagePath)) {
				error="No file.";
				return false;
			}
			try {
				if(!File.Exists(entry.ImagePath)) {
					error="The file "+entry.FileName+" is no longer there (another user may have filed it).";
					return false;
				}
				string textDisposition=BuildDisposition(entry,disposition,listFields);
				if(entry.Kind==CaptureFileKind.Journal) {
					return ResolveJournal(entry,textDisposition,out pathResolved,out error);
				}
				return ResolveQuarantine(entry,textDisposition,out pathResolved,out error);
			}
			catch(Exception ex) {
				error=ex.Message;
				return false;
			}
		}

		private bool ResolveQuarantine(CaptureFileEntry entry,string textDisposition,out string pathResolved,out string error) {
			pathResolved=entry.ImagePath;
			string folderQuarantine=_folders.GetPath(CaptureFolders.SubfolderQuarantine);
			string folderImage=Path.GetDirectoryName(entry.ImagePath);
			string relative=GetRelativeFolder(folderQuarantine,folderImage);
			if(relative==null) {
				error="The file "+entry.FileName+" is not in the capture quarantine.";
				return false;
			}
			string subfolder=relative=="" ? CaptureFolders.SubfolderQuarantineResolved : CaptureFolders.SubfolderQuarantineResolved+"\\"+relative;
			string folderResolved;
			string errorFolder;
			if(!_folders.Ensure(subfolder,out folderResolved,out errorFolder)) {
				return ResolveQuarantineInPlace(entry,textDisposition,"the folder for resolved captures is not usable. "+errorFolder,out error);
			}
			string pathImageNew=Path.Combine(folderResolved,entry.FileName);
			try {
				File.Move(entry.ImagePath,pathImageNew);//fails when the name exists; nothing is replaced
			}
			catch(Exception ex) {
				return ResolveQuarantineInPlace(entry,textDisposition,ex.Message,out error);
			}
			pathResolved=pathImageNew;
			error=errorFolder;//a warning about the folder's rule, if any
			string pathSidecarNew=Path.Combine(folderResolved,Path.GetFileNameWithoutExtension(entry.FileName)+CaptureFiles.SidecarExtension);
			try {
				if(entry.SidecarPath!=null && File.Exists(entry.SidecarPath)) {
					File.Move(entry.SidecarPath,pathSidecarNew);
				}
				File.AppendAllText(pathSidecarNew,textDisposition,new UTF8Encoding(false));
			}
			catch(Exception ex) {
				error=(error=="" ? "" : error+" ")+"The file was moved to "+pathImageNew+", but its sidecar could not be completed: "+ex.Message;
			}
			//The emptied session folder is left: a closing session of this computer may still write into it.
			return true;
		}

		///<summary>The quarantine file could not be moved out (why): its sidecar records the disposition where it is (created when it has none),
		///so the file is not listed, counted or filed again; an administrator moves it later. False when that failed too: nothing was recorded.</summary>
		private static bool ResolveQuarantineInPlace(CaptureFileEntry entry,string textDisposition,string why,out string error) {
			string pathSidecar=entry.SidecarPath??Path.Combine(Path.GetDirectoryName(entry.ImagePath),
				Path.GetFileNameWithoutExtension(entry.ImagePath)+CaptureFiles.SidecarExtension);
			try {
				File.AppendAllText(pathSidecar,textDisposition,new UTF8Encoding(false));
			}
			catch(Exception ex) {
				error="The file "+entry.FileName+" could not be moved out of the capture quarantine ("+why.Trim()+"), and its sidecar could not record "
					+"that it was filed: "+ex.Message;
				return false;
			}
			error="The file "+entry.FileName+" could not be moved out of the capture quarantine ("+why.Trim()+"); its sidecar records that it was "
				+"filed, so it is not offered again. An administrator can move it to Capture\\quarantine-resolved later.";
			return true;
		}

		///<summary>The sidecar gets the disposition first, so the copy is resolved whatever fails next. An only copy then gets its plain name back:
		///the plain-named sidecar is written before the image is renamed and the old one removed after, so the image never lists without its
		///sidecar (a copy without one would show as a "patient unknown" copy that can be filed again).</summary>
		private static bool ResolveJournal(CaptureFileEntry entry,string textDisposition,out string pathResolved,out string error) {
			pathResolved=entry.ImagePath;
			error="";
			string folder=Path.GetDirectoryName(entry.ImagePath);
			string baseName=Path.GetFileNameWithoutExtension(entry.ImagePath);
			string pathSidecar=entry.SidecarPath??Path.Combine(folder,baseName+CaptureFiles.SidecarExtension);
			File.AppendAllText(pathSidecar,textDisposition,new UTF8Encoding(false));
			if(!baseName.EndsWith(CaptureJournal.OnlyCopySuffix,StringComparison.Ordinal)) {
				return true;
			}
			string baseNamePlain=baseName.Substring(0,baseName.Length-CaptureJournal.OnlyCopySuffix.Length);
			string pathImagePlain=Path.Combine(folder,baseNamePlain+entry.Extension);
			string pathSidecarPlain=Path.Combine(folder,baseNamePlain+CaptureFiles.SidecarExtension);
			try {
				File.Copy(pathSidecar,pathSidecarPlain);//fails when the name exists; nothing is replaced
			}
			catch(Exception ex) {
				error="The journal copy keeps its name "+Path.GetFileName(pathResolved)+", so rotation will not remove it: "+ex.Message;
				return true;
			}
			try {
				File.Move(entry.ImagePath,pathImagePlain);
			}
			catch(Exception ex) {
				TryDelete(pathSidecarPlain);
				error="The journal copy keeps its name "+Path.GetFileName(pathResolved)+", so rotation will not remove it: "+ex.Message;
				return true;
			}
			pathResolved=pathImagePlain;
			if(!TryDelete(pathSidecar)) {
				error="The old sidecar "+Path.GetFileName(pathSidecar)+" of the journal copy could not be removed; it has no image any more.";
			}
			return true;
		}

		private static bool TryDelete(string path) {
			try {
				File.Delete(path);
				return true;
			}
			catch {
				return false;
			}
		}

		///<summary>The disposition block appended to a sidecar: a comment, then Disposition, its time, this computer and Windows user, then the
		///caller's fields.</summary>
		private string BuildDisposition(CaptureFileEntry entry,CaptureFileDisposition disposition,IList<KeyValuePair<string,string>> listFields) {
			DateTime now=_funcNow();
			List<SidecarField> listSidecarFields=new List<SidecarField> {
				new SidecarField(CaptureSidecarKeys.Disposition,disposition.ToString()),
				new SidecarField(CaptureSidecarKeys.DispositionAt,CaptureFiles.FormatTimestamp(now)),
				new SidecarField(CaptureSidecarKeys.DispositionComputer,CaptureRecord.GetComputerName()),
				new SidecarField(CaptureSidecarKeys.DispositionWindowsUser,CaptureRecord.GetWindowsUser()),
			};
			if(listFields!=null) {
				foreach(KeyValuePair<string,string> pair in listFields) {
					if(!string.IsNullOrWhiteSpace(pair.Key)) {
						listSidecarFields.Add(new SidecarField(pair.Key,pair.Value??""));
					}
				}
			}
			string text=CaptureFiles.BuildSidecar(new[] {
				disposition+" "+CaptureFiles.FormatTimestamp(now)+": "+(entry.Kind==CaptureFileKind.Quarantine
					? "moved out of the capture quarantine; the image and this sidecar are kept."
					: "this journal copy was filed; rotation may remove it."),
			},listSidecarFields);
			//A sidecar written before always ends its last line; one that does not gets a line break first.
			return "\r\n"+text;
		}

		///<summary>The path of folder below folderRoot ("" for the root itself), or null when it is not inside it.</summary>
		private static string GetRelativeFolder(string folderRoot,string folder) {
			if(folderRoot==null || folder==null) {
				return null;
			}
			string root=Path.GetFullPath(folderRoot).TrimEnd(Path.DirectorySeparatorChar);
			string full=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
			if(string.Equals(root,full,StringComparison.OrdinalIgnoreCase)) {
				return "";
			}
			if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			return full.Substring(root.Length+1);
		}
		#endregion Resolve
	}
}
