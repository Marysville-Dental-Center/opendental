// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.Journal {
	///<summary>The capture journal (design 7.1 "Journal"): a copy of every image a device acquisition transferred, written to
	///Capture\journal before the image goes back to the caller, with a sidecar. It is a recovery buffer of copies, not the record: today's
	///Mount/Acquire loop discards an image whose save fails, and the journal copy is then the only one. Rotation keeps the newest
	///[Twain] JournalKeep copies and removes only journal copies.</summary>
	internal sealed class CaptureJournal {
		///<summary>Delivered values: what happened to the image apart from this copy.</summary>
		public const string DeliveredCaller="Caller";
		public const string DeliveredQuarantine="Quarantine";
		public const string DeliveredNone="None";
		private readonly CaptureFolders _folders;

		public CaptureJournal(CaptureFolders folders) {
			_folders=folders;
		}

		///<summary>Writes a copy of dib with its sidecar, then rotates. keep is [Twain] JournalKeep; 0 turns the journal off. delivered is one
		///of the Delivered values, detail says more about it (the quarantine file, or why there is none), or null. Returns the image path,
		///or null when no copy was written; problems go to log. Never throws.</summary>
		public string Write(DibImage dib,CaptureRecord record,string delivered,string detail,int keep,Action<string> log) {
			log=log??(text => { });
			if(keep<=0) {
				log("No journal copy: [Twain] JournalKeep is 0.");
				return null;
			}
			try {
				string folder;
				string errorFolder;
				if(!_folders.Ensure(CaptureFolders.SubfolderJournal,out folder,out errorFolder)) {
					log("No journal copy: the journal folder is not usable. "+errorFolder);
					return null;
				}
				if(errorFolder!="") {
					log(errorFolder);
				}
				byte[] bytes=CaptureFiles.ToBmpBytes(dib);
				string sha256=CaptureFiles.Sha256Hex(bytes);
				//The image file's name is known only once it is written, so the sidecar names the image by its base name.
				string errorWrite;
				string pathImage=CaptureFiles.WritePair(folder,record.ReceivedAt,bytes,
					baseName => BuildSidecar(record,delivered,detail,baseName+CaptureFiles.ImageExtension,dib,sha256),out errorWrite);
				if(pathImage==null) {
					log("No journal copy: "+errorWrite);
					return null;
				}
				if(errorWrite!="") {
					log(errorWrite);
				}
				string errorRotate;
				Rotate(folder,keep,out errorRotate);
				if(errorRotate!="") {
					log(errorRotate);
				}
				return pathImage;
			}
			catch(Exception ex) {
				log("No journal copy: "+ex.Message);
				return null;
			}
		}

		private static string BuildSidecar(CaptureRecord record,string delivered,string detail,string fileImage,DibImage dib,string sha256) {
			List<SidecarField> listFields=new List<SidecarField> { new SidecarField("Kind","journal copy") };
			listFields.AddRange(record.GetOriginFields());
			listFields.Add(new SidecarField("PatNum",record.PatNum>0 ? record.PatNum.ToString(CultureInfo.InvariantCulture) : ""));
			listFields.Add(new SidecarField("Delivered",delivered));
			if(!string.IsNullOrEmpty(detail)) {
				listFields.Add(new SidecarField("DeliveredDetail",detail));
			}
			listFields.AddRange(record.GetMetadataFields());
			listFields.AddRange(CaptureRecord.GetImageFields(fileImage,dib,sha256));
			listFields.AddRange(record.ListSettings);
			return CaptureFiles.BuildSidecar(new[] {
				"Capture journal copy: a recovery copy, not the record. The image file is the DIB the program received, unchanged, as a .bmp file.",
				"PatNum is empty when the caller did not pass one (the Mount/Acquire loop passes none). Delivered=Caller: the program received",
				"the image and may have stored it; Quarantine: it went to the capture quarantine instead; None: this copy is the only one.",
			},listFields);
		}

		///<summary>Keeps the keep newest journal copies in folder and removes the older ones with their sidecars. Only files named by
		///CaptureFiles count and are removed; anything else in the folder is left alone. Returns how many images it removed; error says
		///what could not be removed. Never throws.</summary>
		public static int Rotate(string folder,int keep,out string error) {
			error="";
			int countRemoved=0;
			try {
				List<string> listBaseNames=Directory.GetFiles(folder,"*"+CaptureFiles.ImageExtension)
					.Select(x => Path.GetFileNameWithoutExtension(x))
					.Where(x => CaptureFiles.IsCaptureBaseName(x))
					.OrderByDescending(x => x,StringComparer.Ordinal)
					.ToList();
				List<string> listErrors=new List<string>();
				foreach(string baseName in listBaseNames.Skip(Math.Max(0,keep))) {
					try {
						File.Delete(Path.Combine(folder,baseName+CaptureFiles.ImageExtension));
						countRemoved++;
					}
					catch(Exception ex) {
						listErrors.Add(baseName+": "+ex.Message);
						continue;//Keep the sidecar of an image that stays.
					}
					try {
						File.Delete(Path.Combine(folder,baseName+CaptureFiles.SidecarExtension));
					}
					catch(Exception ex) {
						listErrors.Add(baseName+CaptureFiles.SidecarExtension+": "+ex.Message);
					}
				}
				if(listErrors.Count>0) {
					error="Journal rotation could not remove "+string.Join("; ",listErrors);
				}
			}
			catch(Exception ex) {
				error="Journal rotation failed: "+ex.Message;
			}
			return countRemoved;
		}
	}
}
