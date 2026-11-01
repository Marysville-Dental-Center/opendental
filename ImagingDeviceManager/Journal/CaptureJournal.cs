// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.Journal {
	///<summary>The capture journal (design 7.1 "Journal"): a copy of every image a device acquisition transferred, written to
	///Capture\journal before the image goes back to the caller, with a sidecar. It is a recovery buffer of copies, not the record: today's
	///Mount/Acquire loop discards an image whose save fails, and the journal copy is then the only one. Rotation keeps the newest
	///[Twain] JournalKeep copies and removes only journal copies. A copy the library knows to be the only one (DeliveredNone: its
	///quarantine write failed) is named with OnlyCopySuffix, which rotation never removes: like the quarantine, it stays until an
	///administrator recovers it.</summary>
	internal sealed class CaptureJournal {
		///<summary>Delivered values: what happened to the image apart from this copy.</summary>
		public const string DeliveredCaller="Caller";
		public const string DeliveredQuarantine="Quarantine";
		public const string DeliveredNone="None";
		///<summary>Ends the base name of a DeliveredNone copy, so that rotation leaves it alone.</summary>
		public const string OnlyCopySuffix="-only";
		///<summary>[Twain] JournalKeep when capture.ini sets none, and in place of 0: the journal has no off switch (design 7.1, 10.6).</summary>
		public const int KeepDefault=200;
		private readonly CaptureFolders _folders;
		///<summary>The warning about JournalKeep=0 is logged once.</summary>
		private bool _isKeepWarningLogged;

		public CaptureJournal(CaptureFolders folders) {
			_folders=folders;
		}

		///<summary>Writes a copy of dib with its sidecar, then rotates. keep is [Twain] JournalKeep; 0 or less is not allowed, because the
		///journal must not be turned off, and keeps KeepDefault copies (logged once). delivered is one of the Delivered values, detail says
		///more about it (the quarantine file, or why there is none), or null. Returns the image path, or null when no copy was written;
		///problems go to log. Never throws.</summary>
		public string Write(DibImage dib,CaptureRecord record,string delivered,string detail,int keep,Action<string> log) {
			CaptureImageFile file;
			try {
				file=CaptureImageFile.FromDib(dib);
			}
			catch(Exception ex) {
				(log??(text => { }))("No journal copy: "+ex.Message);
				return null;
			}
			return Write(file,record,delivered,detail,keep,log);
		}

		///<summary>Write for any image file: a DIB (.bmp) or the 16-bit samples of a capture-session image (.tif).</summary>
		public string Write(CaptureImageFile file,CaptureRecord record,string delivered,string detail,int keep,Action<string> log) {
			log=log??(text => { });
			if(keep<=0) {
				if(!_isKeepWarningLogged) {
					_isKeepWarningLogged=true;
					log("[Twain] JournalKeep="+keep+" is not allowed: the journal cannot be turned off. The newest "+KeepDefault
						+" copies are kept.");
				}
				keep=KeepDefault;
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
				byte[] bytes=file.Bytes;
				string sha256=CaptureFiles.Sha256Hex(bytes);
				//The image file's name is known only once it is written, so the sidecar names the image by its base name.
				string errorWrite;
				string pathImage=CaptureFiles.WritePair(folder,record.ReceivedAt,bytes,
					baseName => BuildSidecar(record,delivered,detail,baseName+file.Extension,file,sha256),out errorWrite,
					delivered==DeliveredNone ? OnlyCopySuffix : "",file.Extension);
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

		///<summary>Rewrites the sidecar of the journal copy at pathImage (written by Write for file and record) with another Delivered value, for
		///an image whose fate was decided after its copy was written: the capture session quarantined it (or could not). A DeliveredNone copy is
		///renamed with OnlyCopySuffix first, so that rotation leaves it alone (a rename needs no disk space), and its sidecar is moved after it at
		///once, so the image always keeps its sidecar with the patient, session and slot; if the sidecar cannot follow, the image gets its name
		///back. The sidecar is then rewritten through a temporary file (WriteReplacing): a crash or a full disk leaves the old sidecar or the new
		///one, never an empty one; when the rewrite fails, the copy keeps its sidecar with the Delivered value from before. Returns the path the
		///image has now; problems go to log. Never throws.</summary>
		public string SetDelivered(string pathImage,CaptureImageFile file,CaptureRecord record,string delivered,string detail,Action<string> log) {
			log=log??(text => { });
			if(string.IsNullOrEmpty(pathImage) || file==null || record==null) {
				return pathImage;
			}
			string pathImageNow=pathImage;
			try {
				string folder=Path.GetDirectoryName(pathImage);
				string baseName=Path.GetFileNameWithoutExtension(pathImage);
				string pathSidecar=Path.Combine(folder,baseName+CaptureFiles.SidecarExtension);
				if(delivered==DeliveredNone && !baseName.EndsWith(OnlyCopySuffix,StringComparison.Ordinal)) {
					string baseNameNew=baseName+OnlyCopySuffix;
					string pathCandidate=Path.Combine(folder,baseNameNew+Path.GetExtension(pathImage));
					string pathSidecarNew=Path.Combine(folder,baseNameNew+CaptureFiles.SidecarExtension);
					try {
						File.Move(pathImage,pathCandidate);
						pathImageNow=pathCandidate;
					}
					catch(Exception ex) {
						log("The journal copy "+Path.GetFileName(pathImage)+" is the only one, but it could not be renamed so that rotation keeps it: "
							+ex.Message);
					}
					if(pathImageNow==pathCandidate) {
						try {
							if(File.Exists(pathSidecar)) {
								File.Move(pathSidecar,pathSidecarNew);
							}
							pathSidecar=pathSidecarNew;
						}
						catch(Exception ex) {
							//The image is never left without its sidecar: it gets its name back.
							log("The sidecar "+Path.GetFileName(pathSidecar)+" could not follow the only copy renamed to "+Path.GetFileName(pathCandidate)
								+" ("+ex.Message+"); the copy keeps its name, so rotation may remove it after "+KeepDefault+" newer copies.");
							try {
								File.Move(pathCandidate,pathImage);
								pathImageNow=pathImage;
							}
							catch(Exception exBack) {
								log("The journal copy "+Path.GetFileName(pathCandidate)+" could not get its name back: "+exBack.Message);
								pathSidecar=pathSidecarNew;
							}
						}
					}
				}
				string sha256=CaptureFiles.Sha256Hex(file.Bytes);
				WriteReplacing(pathSidecar,BuildSidecar(record,delivered,detail,Path.GetFileName(pathImageNow),file,sha256));
				return pathImageNow;
			}
			catch(Exception ex) {
				log("The sidecar of the journal copy "+Path.GetFileName(pathImageNow)+" could not be updated (Delivered="+delivered+"): "+ex.Message);
				return pathImageNow;
			}
		}

		///<summary>Writes text to path through path+".tmp", then replaces path with it (or moves it there when there is no file yet), so that a
		///crash or a full disk leaves the old file or the new one, never an empty one. Throws when it fails; the temporary file is removed.</summary>
		private static void WriteReplacing(string path,string text) {
			string pathTemp=path+".tmp";
			try {
				File.WriteAllText(pathTemp,text,new UTF8Encoding(false));
				if(File.Exists(path)) {
					File.Replace(pathTemp,path,null);
				}
				else {
					File.Move(pathTemp,path);
				}
			}
			catch {
				try {
					if(File.Exists(pathTemp)) {
						File.Delete(pathTemp);
					}
				}
				catch {
				}
				throw;
			}
		}

		private static string BuildSidecar(CaptureRecord record,string delivered,string detail,string fileImage,CaptureImageFile file,string sha256) {
			List<SidecarField> listFields=new List<SidecarField> { new SidecarField("Kind","journal copy") };
			listFields.AddRange(record.GetOriginFields());
			listFields.Add(new SidecarField("PatNum",record.PatNum>0 ? record.PatNum.ToString(CultureInfo.InvariantCulture) : ""));
			listFields.Add(new SidecarField("Delivered",delivered));
			if(!string.IsNullOrEmpty(detail)) {
				listFields.Add(new SidecarField("DeliveredDetail",detail));
			}
			listFields.AddRange(record.GetMetadataFields());
			listFields.AddRange(record.ListCaptureFields);
			listFields.AddRange(file.GetFields(fileImage,sha256));
			listFields.AddRange(record.ListSettings);
			return CaptureFiles.BuildSidecar(new[] {
				"Capture journal copy: a recovery copy, not the record. The image file is the DIB the program received, unchanged, as a .bmp file,",
				"or for a 16-bit capture the samples it received, unchanged, as a 16-bit grayscale .tif file.",
				"PatNum is empty when the caller did not pass one (the Mount/Acquire loop passes none). Delivered=Caller: the program received",
				"the image and may have stored it; Quarantine: it went to the capture quarantine instead; None: this copy is the only one,",
				"its name ends in "+OnlyCopySuffix+", and rotation never removes it.",
			},listFields);
		}

		///<summary>Keeps the keep newest journal copies in folder and removes the older ones with their sidecars. Only files named by
		///CaptureFiles without a suffix count and are removed; only copies (OnlyCopySuffix) and anything else in the folder are left alone.
		///Returns how many images it removed; error says what could not be removed. Never throws.</summary>
		public static int Rotate(string folder,int keep,out string error) {
			error="";
			int countRemoved=0;
			try {
				Dictionary<string,string> dictExtensions=new Dictionary<string,string>(StringComparer.Ordinal);
				foreach(string extension in CaptureFiles.ArrayImageExtensions) {
					foreach(string path in Directory.GetFiles(folder,"*"+extension)) {
						if(!string.Equals(Path.GetExtension(path),extension,StringComparison.OrdinalIgnoreCase)) {
							continue;//"*.tif" also matches ".tiff".
						}
						string baseNameFound=Path.GetFileNameWithoutExtension(path);
						if(CaptureFiles.IsCaptureBaseName(baseNameFound)) {
							dictExtensions[baseNameFound]=extension;
						}
					}
				}
				List<string> listBaseNames=dictExtensions.Keys.OrderByDescending(x => x,StringComparer.Ordinal).ToList();
				List<string> listErrors=new List<string>();
				foreach(string baseName in listBaseNames.Skip(Math.Max(0,keep))) {
					try {
						File.Delete(Path.Combine(folder,baseName+dictExtensions[baseName]));
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
