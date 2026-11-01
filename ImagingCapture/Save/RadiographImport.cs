// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenDentBusiness;

namespace ImagingCapture.Save {
	///<summary>The database and file calls RadiographImport makes, as a thin seam so that its decisions can be tested without a database.
	///OdRadiographStore is the real one; every member is one OpenDentBusiness call.</summary>
	public interface IRadiographStore {
		///<summary>Defs.GetDef(DefCat.ImageCats,defNum) is not null.</summary>
		bool IsCategoryValid(long defNum);
		///<summary>ImageStore.Import(bytes,...): inserts the document row and writes the file (it deletes its own row when the file write
		///fails, and logs ImageCreate). Throws on failure.</summary>
		Document Import(byte[] bytes,long docCategory,ImageType imageType,Patient patient,string mimeType,string fileExtension);
		///<summary>MountItems.Insert. Throws on failure.</summary>
		void InsertMountItem(MountItem mountItem);
		///<summary>Documents.Update(document,documentOld). Throws on failure.</summary>
		void UpdateDocument(Document document,Document documentOld);
		///<summary>Deletes the document row and its file (ImageStore.DeleteDocuments). Throws on failure.</summary>
		void DeleteDocument(Document document,Patient patient);
		///<summary>MountItems.Delete. Throws on failure.</summary>
		void DeleteMountItem(MountItem mountItem);
		///<summary>Mounts.Update. Throws on failure.</summary>
		void UpdateMount(Mount mount);
		///<summary>SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue).</summary>
		void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey);
		///<summary>The Pearl gate, exactly as ControlImages.PlaceAcquiredBitmapInUI calls it: only when the category is one the office set
		///for automatic upload (Pearl.DoAutoUploadForImageCategory), then Pearl.SetupPearlForSendingSingle and SendOnThread. pngBytes is the
		///saved 8-bit image.</summary>
		void SendToPearlIfConfigured(Patient patient,Document document,byte[] pngBytes,MountItem mountItem);
	}

	///<summary>What RadiographImport.Save did.</summary>
	public sealed class RadiographSaveResult {
		///<summary>The document row, the file and its placement were all written.</summary>
		public bool IsSaved { get; set; }
		///<summary>Why not, for the red strip "Could not save: ..." (design 7.12). The caller then quarantines the image.</summary>
		public string Error { get; set; }="";
		public RadiographSavePlan Plan { get; set; }
		///<summary>The saved document, or null.</summary>
		public Document Document { get; set; }
		///<summary>The item the document went into: the target slot, or the new unmounted item. Null for a single document.</summary>
		public MountItem MountItem { get; set; }
		///<summary>A new unmounted item (ItemOrder -1) was inserted for it (no slot was free).</summary>
		public bool IsMountItemCreated { get; set; }
		///<summary>A document row was inserted and then deleted again, because a later step failed (design 7.12: MyISAM has no transaction,
		///"so the saver deletes the row when the update fails").</summary>
		public bool IsRowDeleted { get; set; }
		///<summary>Problems after the save that did not undo it (a security-log entry, the mount's provider, Pearl), and the plan's warnings.</summary>
		public List<string> ListWarnings { get; set; }=new List<string>();
	}

	///<summary>Stores one captured image for a patient through OpenDentBusiness public APIs only (design 7.8, 7.9, 7.10, 9.1, 10.8): an 8-bit
	///image as lossless PNG, a 16-bit image as DICOM when the caller allows it, with ImageStore.Import, then one Documents.Update for the
	///fields Import does not set (DateCreated, ProvNum, PrintHeading, WindowingMin/Max) and the placement of MountPlacement. The decisions
	///are RadiographSavePlanner's; this class only orders the calls and undoes a partial save. Runs on the UI thread, like every database
	///write of the session window.
	///
	///On failure nothing of the image stays in the patient's record: the document row (and its file) and a new unmounted item are deleted
	///again, and the caller quarantines the image. Images are never filed to another patient: Patient is the session's bound patient.</summary>
	public sealed class RadiographImport {
		private readonly IRadiographStore _radiographStore;
		private readonly Func<DateTime> _funcNow;

		///<summary>The real store (OpenDentBusiness).</summary>
		public RadiographImport() : this(new OdRadiographStore(),() => DateTime.Now) {
		}

		public RadiographImport(IRadiographStore radiographStore,Func<DateTime> funcNow) {
			_radiographStore=radiographStore??throw new ArgumentNullException(nameof(radiographStore));
			_funcNow=funcNow??throw new ArgumentNullException(nameof(funcNow));
		}

		///<summary>Saves request.Image. Never throws.</summary>
		public RadiographSaveResult Save(RadiographSaveRequest request) {
			RadiographSaveResult result=new RadiographSaveResult();
			if(request?.Patient==null || request.Patient.PatNum==0) {
				result.Error="The capture session has no patient.";
				return result;
			}
			if(request.Target==null) {
				result.Error="The image has no target.";
				return result;
			}
			//Design 10.6: every save uses the session's PatNum, and an image is never filed to another patient, also not through a stale mount.
			Mount mountTarget=request.Target.Mount;
			if(mountTarget!=null && mountTarget.PatNum!=request.Patient.PatNum) {
				result.Error="The target mount belongs to another patient (PatNum "+mountTarget.PatNum+", not "+request.Patient.PatNum
					+"), so the image was not saved.";
				return result;
			}
			if(mountTarget!=null && request.Target.MountItem!=null && request.Target.MountItem.MountNum!=mountTarget.MountNum) {
				result.Error="The target slot belongs to another mount (MountNum "+request.Target.MountItem.MountNum+", not "+mountTarget.MountNum
					+"), so the image was not saved.";
				return result;
			}
			bool isCategoryValid;
			try {
				isCategoryValid=request.DocCategory!=0 && _radiographStore.IsCategoryValid(request.DocCategory);
			}
			catch(Exception ex) {
				result.Error="The image category could not be checked: "+ex.Message;
				return result;
			}
			if(!isCategoryValid) {
				result.Error="Image category missing; choose a category.";//design 7.12 "Invalid category", before writing
				return result;
			}
			RadiographSavePlan plan=RadiographSavePlanner.Plan(request,_funcNow());
			result.Plan=plan;
			result.ListWarnings.AddRange(plan.ListWarnings);
			if(!plan.IsSavable) {
				result.Error=plan.Error;
				return result;
			}
			Document document;
			try {
				document=_radiographStore.Import(plan.FileBytes,request.DocCategory,plan.ImgType,request.Patient,plan.MimeType,plan.FileExtension);
			}
			catch(Exception ex) {
				result.Error=ex.Message;
				return result;
			}
			if(document==null) {
				result.Error="The image was not saved.";
				return result;
			}
			Document documentOld=document.Copy();
			ApplyFields(document,request,plan);
			MountItem mountItem=null;
			RadiographTarget target=request.Target;
			try {
				if(target.Mount!=null) {
					mountItem=target.MountItem;
					if(mountItem==null) {
						mountItem=MountPlacement.CreateUnmountedItem(target.Mount.MountNum);
						_radiographStore.InsertMountItem(mountItem);
						result.IsMountItemCreated=true;
					}
					MountPlacement.ApplyToDocument(document,mountItem,target.Mount.FlipOnAcquire);
				}
				else {
					document.ToothNumbers=(target.ToothNumbers??"").Trim();
				}
				_radiographStore.UpdateDocument(document,documentOld);
			}
			catch(Exception ex) {
				result.Error=ex.Message;
				Undo(document,result.IsMountItemCreated ? mountItem : null,request.Patient,result);
				result.IsMountItemCreated=false;
				return result;
			}
			result.IsSaved=true;
			result.Document=document;
			result.MountItem=mountItem;
			AfterSave(request,plan,document,mountItem,result);
			return result;
		}

		///<summary>The fields Import does not set (design 9.1, 10.8). Description stays empty for a slot; the session sets "Retake: ..." when it
		///moves a retake (design 7.11). Note stays for users.</summary>
		public static void ApplyFields(Document document,RadiographSaveRequest request,RadiographSavePlan plan) {
			document.DateCreated=plan.DateCreated;
			document.ProvNum=request.ProvNum;
			document.PrintHeading=plan.PrintHeading;
			document.WindowingMin=plan.WindowingMin;
			document.WindowingMax=plan.WindowingMax;
			document.ImgType=plan.ImgType;
			if(document.Description==null) {
				document.Description="";
			}
		}

		private void AfterSave(RadiographSaveRequest request,RadiographSavePlan plan,Document document,MountItem mountItem,RadiographSaveResult result) {
			long patNum=request.Patient.PatNum;
			if(plan.CaptureLogText!=null) {
				try {
					_radiographStore.MakeLogEntry(EnumPermType.ImageCreate,patNum,plan.CaptureLogText,document.DocNum);
				}
				catch(Exception ex) {
					result.ListWarnings.Add("The capture details could not be written to the security log: "+ex.Message);
				}
			}
			Mount mount=request.Target.Mount;
			if(mount!=null && mount.ProvNum!=request.ProvNum) {//design 10.8: the provider applies to mount.ProvNum too
				long provNumOld=mount.ProvNum;
				try {
					mount.ProvNum=request.ProvNum;
					_radiographStore.UpdateMount(mount);
				}
				catch(Exception ex) {
					mount.ProvNum=provNumOld;
					result.ListWarnings.Add("The mount's provider could not be set: "+ex.Message);
				}
			}
			if(plan.Format==RadiographFormat.Png8) {
				try {
					_radiographStore.SendToPearlIfConfigured(request.Patient,document,plan.FileBytes,mountItem);
				}
				catch(Exception ex) {
					result.ListWarnings.Add("The image could not be sent for analysis: "+ex.Message);
				}
			}
		}

		///<summary>Deletes what a failed save left: the document (row and file), then a new unmounted item. A delete that fails is reported in
		///Error, because the row then still exists.</summary>
		private void Undo(Document document,MountItem mountItemCreated,Patient patient,RadiographSaveResult result) {
			try {
				_radiographStore.DeleteDocument(document,patient);
				result.IsRowDeleted=true;
			}
			catch(Exception ex) {
				result.Error+=" The new document could not be deleted again ("+ex.Message+"); delete DocNum "+document.DocNum+" by hand.";
				return;//the item still holds the document
			}
			if(mountItemCreated==null) {
				return;
			}
			try {
				_radiographStore.DeleteMountItem(mountItemCreated);
			}
			catch(Exception ex) {
				result.ListWarnings.Add("An empty unmounted item was left in the mount: "+ex.Message);
			}
		}
	}

	///<summary>IRadiographStore through OpenDentBusiness. Not exercised by the unit tests, which have no database.</summary>
	public sealed class OdRadiographStore:IRadiographStore {
		public bool IsCategoryValid(long defNum) {
			return Defs.GetDef(DefCat.ImageCats,defNum)!=null;
		}

		public Document Import(byte[] bytes,long docCategory,ImageType imageType,Patient patient,string mimeType,string fileExtension) {
			return ImageStore.Import(bytes,docCategory,imageType,patient,mimeType,fileExtension);
		}

		public void InsertMountItem(MountItem mountItem) {
			MountItems.Insert(mountItem);
		}

		public void UpdateDocument(Document document,Document documentOld) {
			Documents.Update(document,documentOld);
		}

		public void DeleteDocument(Document document,Patient patient) {
			string patFolder="";
			if(PrefC.AtoZfolderUsed==DataStorageType.LocalAtoZ || CloudStorage.IsCloudStorage) {
				patFolder=ImageStore.GetPatientFolder(patient,ImageStore.GetPreferredAtoZpath());
			}
			ImageStore.DeleteDocuments(new List<Document> { document },patFolder);
		}

		public void DeleteMountItem(MountItem mountItem) {
			MountItems.Delete(mountItem);
		}

		public void UpdateMount(Mount mount) {
			Mounts.Update(mount);
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue);
		}

		public void SendToPearlIfConfigured(Patient patient,Document document,byte[] pngBytes,MountItem mountItem) {
			if(!OpenDentBusiness.Bridges.Pearl.DoAutoUploadForImageCategory(document.DocCategory)) {
				return;
			}
			using(MemoryStream memoryStream=new MemoryStream(pngBytes))
			using(Bitmap bitmap=new Bitmap(memoryStream)) {
				//SetupPearlForSendingSingle copies the bitmap, as it does for ControlImages.
				OpenDentBusiness.Bridges.Pearl pearl=OpenDentBusiness.Bridges.Pearl.SetupPearlForSendingSingle(patient,document,bitmap,mountItem:mountItem);
				pearl?.SendOnThread();
			}
		}
	}
}
