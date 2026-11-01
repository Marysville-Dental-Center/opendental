// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Journal {
	///<summary>The minimal capture quarantine of phases 0-2 (design 7.1 "Quarantine in phases 0-2", 7.12): images the library received after
	///the caller could no longer take them (drained during a stop, offered after a refused disable, extra single-mode images) are written
	///to Capture\quarantine instead of being lost, each with a sidecar. The legacy loop gives the library no patient, so every sidecar says
	///"patient unknown": such a file is never filed by matching alone (design 7.12, Reassign only). There is no automatic purge.</summary>
	internal sealed class QuarantineWriter {
		public const string PatientUnknown="patient unknown";
		///<summary>Image files counted as unassigned captures: this writer's .bmp, and the formats the capture session (CAP-4) will add.</summary>
		private static readonly string[] _arrayImageExtensions={ ".bmp",".png",".dcm",".tif" };
		private readonly CaptureFolders _folders;

		public QuarantineWriter(CaptureFolders folders) {
			_folders=folders;
		}

		///<summary>Writes dib with its sidecar to Capture\quarantine. reason says why the image is here (DescribeReason). Returns the image
		///path, or null with error. A warning (the folder lacks the Users rule, or only the sidecar failed) goes to log. Never throws.</summary>
		public string Write(DibImage dib,CaptureRecord record,TwainDrainReason drainReason,TwainStopReason stopReason,Action<string> log,
			out string error)
		{
			log=log??(text => { });
			error="";
			try {
				string folder;
				string errorFolder;
				if(!_folders.Ensure(CaptureFolders.SubfolderQuarantine,out folder,out errorFolder)) {
					error="the capture quarantine folder is not usable. "+errorFolder;
					return null;
				}
				if(errorFolder!="") {
					log(errorFolder);
				}
				byte[] bytes=CaptureFiles.ToBmpBytes(dib);
				string sha256=CaptureFiles.Sha256Hex(bytes);
				string errorWrite;
				string pathImage=CaptureFiles.WritePair(folder,record.ReceivedAt,bytes,
					baseName => BuildSidecar(record,drainReason,stopReason,baseName+CaptureFiles.ImageExtension,dib,sha256),out errorWrite);
				if(pathImage==null) {
					error=errorWrite;
					return null;
				}
				if(errorWrite!="") {
					log(errorWrite);
				}
				return pathImage;
			}
			catch(Exception ex) {
				error=ex.Message;
				return null;
			}
		}

		///<summary>Writes an image of a capture session with its sidecar to Capture\quarantine\&lt;sessionGuid&gt; (design 7.12). The sidecar
		///records, beyond the usual fields, the originating PatNum, the patient's name, the session GUID and the slot; patNum 0 records "patient
		///unknown" (a declined redelivered image, whose source knows no patient). Returns the image path, or null with error. Never throws.</summary>
		public string WriteSessionCapture(CaptureImageFile file,CaptureRecord record,Guid sessionGuid,long patNum,string patientName,string slot,
			string reason,Action<string> log,out string error)
		{
			log=log??(text => { });
			error="";
			try {
				string subfolder=CaptureFolders.SubfolderQuarantine+"\\"+sessionGuid.ToString("D");
				string folder;
				string errorFolder;
				if(!_folders.Ensure(subfolder,out folder,out errorFolder)) {
					error="the capture quarantine folder is not usable. "+errorFolder;
					return null;
				}
				if(errorFolder!="") {
					log(errorFolder);
				}
				string sha256=CaptureFiles.Sha256Hex(file.Bytes);
				string errorWrite;
				string pathImage=CaptureFiles.WritePair(folder,record.ReceivedAt,file.Bytes,
					baseName => BuildSessionSidecar(record,sessionGuid,patNum,patientName,slot,reason,baseName+file.Extension,file,sha256),out errorWrite,
					"",file.Extension);
				if(pathImage==null) {
					error=errorWrite;
					return null;
				}
				if(errorWrite!="") {
					log(errorWrite);
				}
				return pathImage;
			}
			catch(Exception ex) {
				error=ex.Message;
				return null;
			}
		}

		private static string BuildSessionSidecar(CaptureRecord record,Guid sessionGuid,long patNum,string patientName,string slot,string reason,
			string fileImage,CaptureImageFile file,string sha256)
		{
			List<SidecarField> listFields=new List<SidecarField> { new SidecarField("Kind","quarantine") };
			listFields.AddRange(record.GetOriginFields());
			listFields.Add(new SidecarField("Patient",patNum>0 ? (patientName??"") : PatientUnknown));
			listFields.Add(new SidecarField("PatNum",patNum>0 ? patNum.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""));
			listFields.Add(new SidecarField("SessionGuid",sessionGuid.ToString("D")));
			listFields.Add(new SidecarField("Slot",slot??""));
			listFields.Add(new SidecarField("Reason",reason??""));
			listFields.AddRange(record.GetMetadataFields());
			listFields.AddRange(record.ListCaptureFields);
			listFields.AddRange(file.GetFields(fileImage,sha256));
			listFields.AddRange(record.ListSettings);
			return CaptureFiles.BuildSidecar(new[] {
				"Capture quarantine: an image of a capture session that was not filed. The image file is what the program received, unchanged",
				"(a .bmp of the DIB, or a 16-bit grayscale .tif of the samples). Patient and PatNum are the patient the session was bound to;",
				"\"patient unknown\" files are offered only through Reassign.",
			},listFields);
		}

		///<summary>The sidecar of a quarantine image: receive time, source ProductName, computer, Windows user, the reason and "patient
		///unknown", and CaptureId and Sequence only when known.</summary>
		public static string BuildSidecar(CaptureRecord record,TwainDrainReason drainReason,TwainStopReason stopReason,string fileImage,DibImage dib,
			string sha256)
		{
			List<SidecarField> listFields=new List<SidecarField> { new SidecarField("Kind","quarantine") };
			listFields.AddRange(record.GetOriginFields());
			listFields.Add(new SidecarField("Patient",PatientUnknown));
			listFields.Add(new SidecarField("PatNum",""));
			listFields.Add(new SidecarField("Reason",DescribeReason(drainReason,stopReason)));
			listFields.Add(new SidecarField("DrainReason",drainReason.ToString()));
			listFields.Add(new SidecarField("StopReason",stopReason.ToString()));
			listFields.AddRange(record.GetMetadataFields());
			listFields.AddRange(CaptureRecord.GetImageFields(fileImage,dib,sha256));
			return CaptureFiles.BuildSidecar(new[] {
				"Capture quarantine: an image received after the series stopped, saved instead of being lost. The image file is the DIB the",
				"program received, unchanged, as a .bmp file. The patient is unknown: recover it only as the runbook describes.",
			},listFields);
		}

		///<summary>Why an image is in the quarantine, in words for the sidecar and the administrator.</summary>
		public static string DescribeReason(TwainDrainReason drainReason,TwainStopReason stopReason) {
			switch(drainReason) {
				case TwainDrainReason.ExtraImage:
					return "The source sent more images than the one asked for (CAP_XFERCOUNT=1); this is an extra image.";
				case TwainDrainReason.RefusedDisable:
					return "The source offered it after it refused to stop (safe disable); the series had stopped: "+DescribeStop(stopReason);
				default:
					return "It was waiting to be transferred when the series stopped: "+DescribeStop(stopReason);
			}
		}

		private static string DescribeStop(TwainStopReason stopReason) {
			switch(stopReason) {
				case TwainStopReason.Cancel:
					return "Cancel.";
				case TwainStopReason.Timeout:
					return "the acquire timeout ([Twain] AcquireTimeoutMinutes).";
				case TwainStopReason.ParentDestroyed:
					return "the window that started the series was closed, for example by an automatic logoff.";
				case TwainStopReason.Quit:
					return "the program was closing.";
				case TwainStopReason.Closed:
					return "the source was closed with images still waiting.";
				case TwainStopReason.Error:
					return "an error (for example a failed transfer).";
				default:
					return "the series ended.";
			}
		}

		///<summary>How many unassigned captures are on this computer: image files anywhere under Capture\quarantine, and the last-write time
		///of the oldest. 0 when the folder does not exist (it is not created to count). Never throws.</summary>
		public static int CountImages(CaptureFolders folders,out DateTime? oldest) {
			oldest=null;
			try {
				string folder=folders.GetPath(CaptureFolders.SubfolderQuarantine);
				if(folder==null || !Directory.Exists(folder)) {
					return 0;
				}
				List<FileInfo> listFiles=new DirectoryInfo(folder).GetFiles("*",SearchOption.AllDirectories)
					.Where(x => _arrayImageExtensions.Contains(x.Extension.ToLowerInvariant()))
					.ToList();
				if(listFiles.Count>0) {
					oldest=listFiles.Min(x => x.LastWriteTime);
				}
				return listFiles.Count;
			}
			catch {
				return 0;
			}
		}
	}
}
