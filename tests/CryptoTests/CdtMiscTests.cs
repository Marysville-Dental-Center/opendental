// Tests for the open source CDT and ODCrypt libraries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CryptoTests {
	///<summary>The ADA-content and registration-key stubs in CDT.Class1.</summary>
	[TestClass]
	public class CdtMiscTests {
		[TestMethod]
		public void AdaCodeText_IsNotShipped() {
			Assert.AreEqual("",CDT.Class1.GetADAcodes());
			Assert.AreEqual("",CDT.Class1.GetProcedureCodeSettings());
			Assert.AreEqual("",CDT.Class1.GetProcedureCodeTreatAreas());
			Assert.AreEqual("",CDT.Class1.GetMandibularCodes());
			Assert.AreEqual("",CDT.Class1.GetMaxillaryCodes());
		}

		[TestMethod]
		public void ClaimFormImages_AreBlankAndIndependent() {
			Func<Image>[] arrayGetters=new Func<Image>[] {
				CDT.Class1.GetADA2006,
				CDT.Class1.GetADA2012,
				CDT.Class1.GetADA2012_J430D,
				CDT.Class1.GetADA2018_J432,
				CDT.Class1.GetADA2019_J430,
				CDT.Class1.GetADA2024_J430,
				CDT.Class1.GetDC217,
			};
			foreach(Func<Image> getter in arrayGetters) {
				Image image=getter();
				Image imageOther=getter();
				Assert.AreNotSame(image,imageOther);
				Assert.AreEqual(1,image.Width);
				Assert.AreEqual(1,image.Height);
				Assert.AreEqual(0,((Bitmap)image).GetPixel(0,0).A);
				//Callers draw at Width/HorizontalResolution*100, then may dispose.
				Assert.IsTrue(image.Width/image.HorizontalResolution*100>0);
				image.Dispose();
				Assert.AreEqual(1,imageOther.Width);
				imageOther.Dispose();
			}
		}

		[TestMethod]
		public void RegistrationKeys_AreNotEnforced() {
			Assert.AreEqual("",CDT.Class1.GenerateRandKey());
			Assert.IsTrue(CDT.Class1.ValidateKey(""));
			Assert.IsTrue(CDT.Class1.ValidateKey("ABCD-EFGH-IJKL-MNOP"));
		}
	}
}
