// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace ImagingCapture.UI {
	///<summary>The video image category: the VideoImageCategoryDefault preference that ControlImages saves camera photos into
	///(0 means the category selected in Imaging). It is the same preference as Setup, Imaging, "Video image category", so
	///changing it needs the Setup permission and is logged like a preference change.</summary>
	internal static class VideoCategoryPref {
		///<summary>The current value. 0 means the category selected in Imaging.</summary>
		public static long Get() {
			return PrefC.GetLong(PrefName.VideoImageCategoryDefault);
		}

		///<summary>True when the current user may change it (Setup permission). Shows no message.</summary>
		public static bool CanEdit() {
			return Security.IsAuthorized(EnumPermType.Setup,true);
		}

		///<summary>Saves defNumNew when it differs from defNumOld. Returns false, with error, when the user lacks the Setup permission
		///or the save failed; the preference is then unchanged. On a change it tells the other workstations (signal) and makes a
		///Setup security log entry.</summary>
		public static bool Save(long defNumOld,long defNumNew,ImageCategoryRule imageCategoryRule,out string error) {
			error="";
			if(defNumNew==defNumOld) {
				return true;
			}
			if(!CanEdit()) {
				error="The video image category was not changed, because that needs the Setup permission.";
				return false;
			}
			try {
				if(Prefs.UpdateLong(PrefName.VideoImageCategoryDefault,defNumNew)) {
					DataValid.SetInvalid(InvalidType.Prefs);
				}
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Video image category changed from "+GetLogName(imageCategoryRule,defNumOld)
					+" to "+GetLogName(imageCategoryRule,defNumNew)+" in the camera Learn window.");
			}
			catch(Exception ex) {
				error="The video image category could not be saved: "+ex.Message;
				return false;
			}
			return true;
		}

		private static string GetLogName(ImageCategoryRule imageCategoryRule,long defNum) {
			if(defNum==0) {
				return "(category selected in Imaging)";
			}
			ImageCategoryItem item=imageCategoryRule?.Find(defNum);
			return item==null ? "DefNum "+defNum : "\""+item.Name+"\"";
		}
	}
}
