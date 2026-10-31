// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>ImageCategoryRule (design 8.5): the patient picture warning by the rule of Documents.GetPatPictFromDb, with a stand-in
	///for the Defs cache. The categories are made up.</summary>
	[TestClass]
	public class ImageCategoryRuleTests {
		private List<ImageCategoryItem> _listCategories;
		private int _countLookups;
		private ImageCategoryRule _imageCategoryRule;

		[TestInitialize]
		public void Init() {
			_listCategories=new List<ImageCategoryItem>() {
				new ImageCategoryItem(10,"X-rays","X",false),
				new ImageCategoryItem(11,"Old pictures","P",true),
				new ImageCategoryItem(12,"Patient pictures","MP",false),
				new ImageCategoryItem(13,"Photos","",false),
				new ImageCategoryItem(14,"More pictures","P",false),
				new ImageCategoryItem(15,"Lower case","p",false),
				new ImageCategoryItem(16,"Hidden photos","",true),
			};
			_countLookups=0;
			_imageCategoryRule=new ImageCategoryRule(() => {
				_countLookups++;
				return _listCategories;
			});
		}

		[TestMethod]
		public void Letter_MatchedLikeDocuments() {
			Assert.IsTrue(ImageCategoryRule.CarriesPatientPictureLetter("P"));
			Assert.IsTrue(ImageCategoryRule.CarriesPatientPictureLetter("XMP"));
			Assert.IsFalse(ImageCategoryRule.CarriesPatientPictureLetter("p"),"Regex \"P\" is case-sensitive.");
			Assert.IsFalse(ImageCategoryRule.CarriesPatientPictureLetter("XMS"));
			Assert.IsFalse(ImageCategoryRule.CarriesPatientPictureLetter(""));
			Assert.IsFalse(ImageCategoryRule.CarriesPatientPictureLetter(null));
		}

		[TestMethod]
		public void PatientPictureCategory_FirstVisibleWithP() {
			//11 carries P but is hidden; Documents.GetPatPictFromDb reads the short list, so 12 is the patient picture category.
			Assert.AreEqual(12,_imageCategoryRule.GetPatientPictureDefNum());
			_listCategories.RemoveAll(x => x.DefNum==12);
			Assert.AreEqual(14,_imageCategoryRule.GetPatientPictureDefNum(),"The cache is read again on every call.");
			_listCategories.RemoveAll(x => x.DefNum==14);
			Assert.AreEqual(0,_imageCategoryRule.GetPatientPictureDefNum());
			Assert.IsTrue(_countLookups>=3);
		}

		[TestMethod]
		public void Warning_ForEveryCategoryWithP() {
			Assert.AreEqual("",_imageCategoryRule.GetWarning(0),"0 is the category selected in Imaging, which is not known here.");
			Assert.AreEqual("",_imageCategoryRule.GetWarning(10));
			Assert.AreEqual("",_imageCategoryRule.GetWarning(13));
			Assert.AreEqual("",_imageCategoryRule.GetWarning(15),"Lower case p is not the patient picture letter.");
			Assert.AreEqual("",_imageCategoryRule.GetWarning(99),"An unknown category gives no warning.");
			string warningFirst=_imageCategoryRule.GetWarning(12);
			StringAssert.Contains(warningFirst,"\"Patient pictures\" is the patient picture category");
			StringAssert.Contains(warningFirst,"becomes the patient's picture");
			string warningOther=_imageCategoryRule.GetWarning(14);
			StringAssert.Contains(warningOther,"\"More pictures\" is marked for patient pictures");
			StringAssert.Contains(warningOther,"can become the patient's picture");
			StringAssert.Contains(_imageCategoryRule.GetWarning(11),"marked for patient pictures","A hidden category with P still warns.");
			Assert.IsTrue(_imageCategoryRule.IsPatientPictureCategory(11));
			Assert.IsTrue(_imageCategoryRule.IsPatientPictureCategory(12));
			Assert.IsFalse(_imageCategoryRule.IsPatientPictureCategory(13));
			Assert.IsFalse(_imageCategoryRule.IsPatientPictureCategory(0));
		}

		[TestMethod]
		public void PickList_VisibleCategoriesAndTheHiddenCurrentOne() {
			CollectionAssert.AreEqual(new long[] { 10,12,13,14,15 },_imageCategoryRule.GetPickList(0).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 10,12,13,14,15 },_imageCategoryRule.GetPickList(13).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 10,12,13,14,15,16 },_imageCategoryRule.GetPickList(16).Select(x => x.DefNum).ToArray(),
				"A hidden current category stays in its place.");
			Assert.AreEqual("Photos",_imageCategoryRule.Find(13).Name);
			Assert.IsNull(_imageCategoryRule.Find(0));
		}

		[TestMethod]
		public void Lookup_NullListIsEmpty() {
			ImageCategoryRule imageCategoryRule=new ImageCategoryRule(() => null);
			Assert.AreEqual(0,imageCategoryRule.GetCategories().Count);
			Assert.AreEqual("",imageCategoryRule.GetWarning(12));
			TestUtil.AssertThrows<ArgumentNullException>(() => new ImageCategoryRule(null));
		}
	}
}
