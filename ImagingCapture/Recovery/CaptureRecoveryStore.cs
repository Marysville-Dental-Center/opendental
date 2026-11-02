// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Recovery {
	///<summary>The database reads and writes of the quarantine window and Recover recent captures apart from saving an image (RadiographImport
	///has its own seam, IRadiographStore), as a thin seam so that the filing rules can be tested without a database. OdCaptureRecoveryStore is
	///the real one; every member is one OpenPmsDentalBusiness call. UI thread only.</summary>
	public interface ICaptureRecoveryStore {
		///<summary>Security.IsAuthorized(permType,date) with no message box: false with message (the refusal, translated) when the user lacks
		///the permission or the date is before its lock date. DateTime.MinValue checks no date.</summary>
		bool IsAuthorized(EnumPermType permType,DateTime date,out string message);
		///<summary>Patients.GetPat, or null when there is no such patient.</summary>
		Patient GetPatient(long patNum);
		///<summary>The patient patNum was merged into (Patients.MergeTwoPatients archives the patient merged from and moves its mounts and
		///documents), following later merges of that patient, or 0 when patNum is not a merged patient (PatientLinks.WasPatientMerged).</summary>
		long GetMergedIntoPatNum(long patNum);
		///<summary>Mounts.GetByNum, or null when there is no such mount.</summary>
		Mount GetMount(long mountNum);
		///<summary>MountItems.GetItemsForMount: ordered by ItemOrder.</summary>
		List<MountItem> GetMountItems(long mountNum);
		///<summary>Documents.GetDocumentsForMountItems: parallel to listMountItems, null for an empty item.</summary>
		List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems);
		///<summary>Documents.GetAllWithPat: every document of the patient.</summary>
		List<Document> GetPatientDocuments(long patNum);
		///<summary>The ImageCreate security-log entries of the patient with FKey in listDocNums (the capture entries of design 7.9 and of a
		///recovery).</summary>
		List<SecurityLog> GetImageCreateLogEntries(long patNum,List<long> listDocNums);
		///<summary>Defs.GetDef(DefCat.ImageCats,defNum) is not null and not hidden.</summary>
		bool IsCategoryValid(long defNum);
		///<summary>The image categories that are not hidden, in their order (Defs.GetDefsForCategory(DefCat.ImageCats,true)).</summary>
		List<Def> GetImageCategories();
		///<summary>RadiographProvider.GetDefaultProvNumFromDb (design 10.8).</summary>
		long GetDefaultProvNum(Patient patient);
		///<summary>The imagingdevice row, or null.</summary>
		ImagingDevice GetImagingDevice(long imagingDeviceNum);
		///<summary>The Setup security-log entries with FKey=imagingDeviceNum (Test capture's verification records, design 9.3).</summary>
		List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum);
		///<summary>SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue). Throws on failure.</summary>
		void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey);
		///<summary>The Open Dental user name of the current user, for the sidecar's disposition.</summary>
		string GetUserName();
	}

	///<summary>ICaptureRecoveryStore through OpenPmsDentalBusiness. Not exercised by the unit tests, which have no database.</summary>
	public sealed class OdCaptureRecoveryStore:ICaptureRecoveryStore {
		public bool IsAuthorized(EnumPermType permType,DateTime date,out string message) {
			string messageRefused="";
			bool isAuthorized=Security.IsAuthorized(permType,date,actionNotAuthorized:text => messageRefused=text);
			message=isAuthorized ? "" : (messageRefused??"").Trim();
			if(!isAuthorized && message=="") {
				message="Not authorized for "+GroupPermissions.GetDesc(permType)+".";
			}
			return isAuthorized;
		}

		public Patient GetPatient(long patNum) {
			return patNum<=0 ? null : Patients.GetPat(patNum);
		}

		public long GetMergedIntoPatNum(long patNum) {
			long patNumInto=0;
			HashSet<long> hashSetSeen=new HashSet<long>();
			long patNumFrom=patNum;
			while(patNumFrom>0 && hashSetSeen.Add(patNumFrom)) {
				List<PatientLink> listMergeLinks=PatientLinks.GetLinks(patNumFrom,PatientLinkType.Merge);
				if(!PatientLinks.WasPatientMerged(patNumFrom,listMergeLinks)) {
					break;
				}
				long patNumLinkFrom=patNumFrom;
				patNumFrom=listMergeLinks.Where(x => x.PatNumFrom==patNumLinkFrom).OrderBy(x => x.DateTimeLink).Last().PatNumTo;//the latest merge
				patNumInto=patNumFrom;
			}
			return patNumInto;
		}

		public Mount GetMount(long mountNum) {
			if(mountNum<=0) {
				return null;
			}
			Mount mount=Mounts.GetByNum(mountNum);
			return mount==null || mount.MountNum==0 ? null : mount;//GetByNum gives a new Mount when there is none
		}

		public List<MountItem> GetMountItems(long mountNum) {
			return MountItems.GetItemsForMount(mountNum);
		}

		public List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems) {
			return Documents.GetDocumentsForMountItems(listMountItems).ToList();
		}

		public List<Document> GetPatientDocuments(long patNum) {
			return Documents.GetAllWithPat(patNum).ToList();
		}

		public List<SecurityLog> GetImageCreateLogEntries(long patNum,List<long> listDocNums) {
			if(listDocNums==null || listDocNums.Count==0) {
				return new List<SecurityLog>();
			}
			return SecurityLogs.Refresh(patNum,new List<EnumPermType> { EnumPermType.ImageCreate },listDocNums).ToList();
		}

		public bool IsCategoryValid(long defNum) {
			Def def=Defs.GetDef(DefCat.ImageCats,defNum);
			return def!=null && !def.IsHidden;
		}

		public List<Def> GetImageCategories() {
			return Defs.GetDefsForCategory(DefCat.ImageCats,isShort:true);
		}

		public long GetDefaultProvNum(Patient patient) {
			return RadiographProvider.GetDefaultProvNumFromDb(patient);
		}

		public ImagingDevice GetImagingDevice(long imagingDeviceNum) {
			return ImagingDevices.GetDeepCopy().FirstOrDefault(x => x.ImagingDeviceNum==imagingDeviceNum);
		}

		public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
			return SecurityLogs.Refresh(0,new List<EnumPermType> { EnumPermType.Setup },imagingDeviceNum).ToList();
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue);
		}

		public string GetUserName() {
			return Security.CurUser?.UserName??"";
		}
	}

	///<summary>The quarantine window's connections to the rest of the program, so that its rules can be tested with stand-ins. CreateDefault gives
	///the real ones.</summary>
	public sealed class CaptureRecoveryServices {
		public ICaptureRecoveryStore Store { get; set; }
		public IRadiographStore RadiographStore { get; set; }
		public ICaptureFileStore FileStore { get; set; }
		public Func<DateTime> FuncNow { get; set; }
		///<summary>capture.ini (LocalCaptureConfig.Load), for the 16-bit conditions of a 16-bit capture (design 7.8).</summary>
		public Func<LocalCaptureConfig> FuncLoadConfig { get; set; }
		///<summary>This computer (ODEnvironment.MachineName).</summary>
		public Func<string> FuncGetComputerName { get; set; }
		///<summary>A line for Capture\capture-log.txt (CaptureLogFile.Write).</summary>
		public Action<string> ActionWriteLog { get; set; }

		public static CaptureRecoveryServices CreateDefault() {
			return new CaptureRecoveryServices {
				Store=new OdCaptureRecoveryStore(),
				RadiographStore=new OdRadiographStore(),
				FileStore=CaptureFileStore.CreateDefault(),
				FuncNow=() => DateTime.Now,
				FuncLoadConfig=() => LocalCaptureConfig.Load(),
				FuncGetComputerName=() => ODEnvironment.MachineName,
				ActionWriteLog=text => CaptureLogFile.Write(text),
			};
		}
	}
}
