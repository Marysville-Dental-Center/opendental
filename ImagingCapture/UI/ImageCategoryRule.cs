// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenPmsDentalBusiness;

namespace ImagingCapture.UI {
	///<summary>One image category (a Def of DefCat.ImageCats), with what the capture windows need from it.</summary>
	public sealed class ImageCategoryItem {
		public long DefNum { get; }
		public string Name { get; }
		///<summary>The category's usage letters (Def.ItemValue), for example "XP". Never null.</summary>
		public string UsageLetters { get; }
		public bool IsHidden { get; }

		public ImageCategoryItem(long defNum,string name,string usageLetters,bool isHidden) {
			DefNum=defNum;
			Name=name??"";
			UsageLetters=usageLetters??"";
			IsHidden=isHidden;
		}
	}

	///<summary>Which image categories hold patient pictures, by the rule Documents.GetPatPictFromDb uses (Documents.cs): a category
	///whose usage letters contain 'P' (Regex "P", so upper case only) is a patient picture category, and the first one among the
	///categories that are not hidden, in cache order, is where Open Dental looks up the patient's picture, which is the newest
	///document in it. A camera photo saved into such a category can therefore become the patient's picture, so the capture
	///windows warn about it (design 8.5): Learn for the video image category, FrmTestCapture for the X-ray category.
	///The categories come from a function, by default the Defs cache, so the rule can be tested with a stand-in.</summary>
	public sealed class ImageCategoryRule {
		///<summary>The usage letter of patient picture categories.</summary>
		public const string PatientPictureLetter="P";

		private readonly Func<List<ImageCategoryItem>> _funcGetCategories;

		///<summary>Reads the categories from the Defs cache.</summary>
		public ImageCategoryRule() : this(GetCategoriesFromDefs) {
		}

		///<summary>funcGetCategories returns every image category, hidden ones included, in cache order.</summary>
		public ImageCategoryRule(Func<List<ImageCategoryItem>> funcGetCategories) {
			_funcGetCategories=funcGetCategories??throw new ArgumentNullException(nameof(funcGetCategories));
		}

		///<summary>Every image category, hidden ones included, in cache order. Never null.</summary>
		public List<ImageCategoryItem> GetCategories() {
			return (_funcGetCategories()??new List<ImageCategoryItem>()).Where(x => x!=null).ToList();
		}

		///<summary>The categories a picker offers: those not hidden, in cache order, plus defNumSelected where it belongs in that order
		///when it is a hidden category, so the current value still shows.</summary>
		public List<ImageCategoryItem> GetPickList(long defNumSelected) {
			return GetCategories().Where(x => !x.IsHidden || (defNumSelected!=0 && x.DefNum==defNumSelected)).ToList();
		}

		///<summary>The category with this DefNum, or null (also for 0).</summary>
		public ImageCategoryItem Find(long defNum) {
			if(defNum==0) {
				return null;
			}
			return GetCategories().FirstOrDefault(x => x.DefNum==defNum);
		}

		///<summary>True when the usage letters carry the patient picture letter, matched as Documents.GetPatPictFromDb matches it.</summary>
		public static bool CarriesPatientPictureLetter(string usageLetters) {
			return Regex.IsMatch(usageLetters??"",PatientPictureLetter);
		}

		///<summary>The category Documents.GetPatPictFromDb takes the patient's picture from: the first category that is not hidden and
		///carries 'P'. 0 when there is none.</summary>
		public long GetPatientPictureDefNum() {
			ImageCategoryItem item=GetCategories().FirstOrDefault(x => !x.IsHidden && CarriesPatientPictureLetter(x.UsageLetters));
			return item?.DefNum??0;
		}

		///<summary>True when the category with this DefNum carries 'P'. False for 0 and for an unknown category.</summary>
		public bool IsPatientPictureCategory(long defNum) {
			ImageCategoryItem item=Find(defNum);
			return item!=null && CarriesPatientPictureLetter(item.UsageLetters);
		}

		///<summary>"" when photos may go into this category. Otherwise the warning to show next to the picker. 0 (no category chosen)
		///and unknown categories give "".</summary>
		public string GetWarning(long defNum) {
			return GetWarning(defNum,"photo","Photos");
		}

		///<summary>GetWarning for images of another kind, named in the text: imageSingular "X-ray" and imagePlural "X-rays" for the X-ray category
		///FrmTestCapture checks.</summary>
		public string GetWarning(long defNum,string imageSingular,string imagePlural) {
			ImageCategoryItem item=Find(defNum);
			if(item==null || !CarriesPatientPictureLetter(item.UsageLetters)) {
				return "";
			}
			if(item.DefNum==GetPatientPictureDefNum()) {
				return "\""+item.Name+"\" is the patient picture category (usage P). Every "+imageSingular+" saved in it becomes the patient's picture. "
					+"Choose a category without P.";
			}
			return "\""+item.Name+"\" is marked for patient pictures (usage P). "+imagePlural+" saved in it can become the patient's picture. "
				+"Choose a category without P.";
		}

		///<summary>The image categories from the Defs cache, hidden ones included, in cache order.</summary>
		private static List<ImageCategoryItem> GetCategoriesFromDefs() {
			return Defs.GetDefsForCategory(DefCat.ImageCats).Select(x => new ImageCategoryItem(x.DefNum,x.ItemName,x.ItemValue,x.IsHidden)).ToList();
		}
	}
}
