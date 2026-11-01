// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Save;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>The database reads and writes of the capture session apart from saving an image (RadiographImport has its own seam,
	///IRadiographStore), as a thin seam so that the launcher and the controller can be tested without a database. OdCaptureSessionStore is the
	///real one; every member is one OpenDentBusiness call. Called on the UI thread only.</summary>
	public interface ICaptureSessionStore {
		///<summary>Security.IsAuthorized(permType,suppressMessage:true): the caller shows the refusal itself.</summary>
		bool IsAuthorized(EnumPermType permType);
		///<summary>Defs.GetDef(DefCat.ImageCats,defNum) is not null (design 7.12 "Invalid category").</summary>
		bool IsCategoryValid(long defNum);
		///<summary>MountItems.GetItemsForMount: ordered by ItemOrder.</summary>
		List<MountItem> GetMountItems(long mountNum);
		///<summary>Documents.GetDocumentsForMountItems: parallel to listMountItems, null for an empty item.</summary>
		List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems);
		///<summary>RadiographProvider.GetDefaultProvNumFromDb (design 10.8).</summary>
		long GetDefaultProvNum(Patient patient);
		///<summary>Providers.GetAbbr, for the banner and the summary.</summary>
		string GetProviderAbbr(long provNum);
		///<summary>The Setup security-log entries with FKey=imagingDeviceNum (where Test capture writes its verification records, design 9.3).</summary>
		List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum);
		///<summary>SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue). Throws on failure.</summary>
		void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey);
	}

	///<summary>ICaptureSessionStore through OpenDentBusiness. Not exercised by the unit tests, which have no database.</summary>
	public sealed class OdCaptureSessionStore:ICaptureSessionStore {
		public bool IsAuthorized(EnumPermType permType) {
			return Security.IsAuthorized(permType,suppressMessage:true);
		}

		public bool IsCategoryValid(long defNum) {
			return Defs.GetDef(DefCat.ImageCats,defNum)!=null;
		}

		public List<MountItem> GetMountItems(long mountNum) {
			return MountItems.GetItemsForMount(mountNum);
		}

		public List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems) {
			return Documents.GetDocumentsForMountItems(listMountItems).ToList();
		}

		public long GetDefaultProvNum(Patient patient) {
			return RadiographProvider.GetDefaultProvNumFromDb(patient);
		}

		public string GetProviderAbbr(long provNum) {
			return provNum==0 ? "" : Providers.GetAbbr(provNum,includeHidden:true);
		}

		public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
			return SecurityLogs.Refresh(0,new List<EnumPermType> { EnumPermType.Setup },imagingDeviceNum).ToList();
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue);
		}
	}
}
