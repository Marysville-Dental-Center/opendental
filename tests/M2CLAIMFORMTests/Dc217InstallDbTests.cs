// Installing the built-in Denti-Cal DC217_2019 claim form as one of the office's claim forms, so insurance plans can use it (backlog package
// M2-CLAIMFORM). Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CLAIMFORMTests {
	[TestClass]
	public class Dc217InstallDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ClaimFormDb.Begin();
			DeleteInstalledCopies();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(ClaimFormDb.SkipReason==null) {
				DeleteInstalledCopies();
			}
		}

		[TestMethod]
		public void InsertInternalIfMissing_InstallsOnceWithEveryItem() {
			ClaimForm claimFormInternal=Dc217.FromResource();
			long claimFormNum=ClaimForms.InsertInternalIfMissing(ClaimForms.DescriptionDentiCalDC217_2019);
			Assert.IsTrue(claimFormNum>0);
			Assert.AreEqual("1",Scalar("SELECT COUNT(*) FROM claimform WHERE Description='Denti-Cal DC217_2019'"));
			Assert.AreEqual(claimFormInternal.Items.Count.ToString(),Scalar("SELECT COUNT(*) FROM claimformitem WHERE ClaimFormNum="+claimFormNum));
			//A second call changes nothing.
			Assert.AreEqual(claimFormNum,ClaimForms.InsertInternalIfMissing(ClaimForms.DescriptionDentiCalDC217_2019));
			Assert.AreEqual("1",Scalar("SELECT COUNT(*) FROM claimform WHERE Description='Denti-Cal DC217_2019'"));
			Assert.AreEqual(claimFormInternal.Items.Count.ToString(),Scalar("SELECT COUNT(*) FROM claimformitem WHERE ClaimFormNum="+claimFormNum));
			//Loaded the way the insurance plan window lists claim forms: a visible office claim form with the shipped items and settings.
			Cache.Refresh(InvalidType.ClaimForms);
			ClaimForm claimFormInstalled=ClaimForms.GetDeepCopy(isShort:true).SingleOrDefault(x => x.ClaimFormNum==claimFormNum);
			Assert.IsNotNull(claimFormInstalled,"Selectable for insurance plans.");
			Assert.IsFalse(claimFormInstalled.IsInternal);
			Assert.AreEqual(ClaimForms.DescriptionDentiCalDC217_2019,claimFormInstalled.Description);
			Assert.IsFalse(claimFormInstalled.PrintImages);
			Assert.AreEqual(claimFormInternal.FontName,claimFormInstalled.FontName);
			Assert.AreEqual(claimFormInternal.FontSize,claimFormInstalled.FontSize);
			Assert.AreEqual(claimFormInternal.Width,claimFormInstalled.Width);
			Assert.AreEqual(claimFormInternal.Height,claimFormInstalled.Height);
			CollectionAssert.AreEquivalent(Dc217.Describe(claimFormInternal),Dc217.Describe(claimFormInstalled));
		}

		[TestMethod]
		public void InsertInternalIfMissing_LeavesTheOfficesFormAlone() {
			//An office copy with the same description, for example one the office calibrated: kept as it is.
			ClaimForm claimFormOffice=new ClaimForm { Description=ClaimForms.DescriptionDentiCalDC217_2019,FontName="Arial",FontSize=10,OffsetX=4,OffsetY=-3 };
			claimFormOffice.Items.Add(new ClaimFormItem { FieldName="PatientLastFirst",XPos=40,YPos=93,Width=225,Height=14 });
			long claimFormNumOffice=ClaimForms.Insert(claimFormOffice,includeClaimFormItems:true);
			Assert.AreEqual(claimFormNumOffice,ClaimForms.InsertInternalIfMissing(ClaimForms.DescriptionDentiCalDC217_2019));
			Assert.AreEqual("1",Scalar("SELECT COUNT(*) FROM claimform WHERE Description='Denti-Cal DC217_2019'"));
			Assert.AreEqual("1",Scalar("SELECT COUNT(*) FROM claimformitem WHERE ClaimFormNum="+claimFormNumOffice));
			Assert.AreEqual("4",Scalar("SELECT OffsetX FROM claimform WHERE ClaimFormNum="+claimFormNumOffice));
		}

		[TestMethod]
		public void InsertInternalIfMissing_UnknownFormInstallsNothing() {
			string countBefore=Scalar("SELECT COUNT(*) FROM claimform");
			Assert.AreEqual(0,ClaimForms.InsertInternalIfMissing("No such claim form"));
			Assert.AreEqual(countBefore,Scalar("SELECT COUNT(*) FROM claimform"));
		}

		[TestMethod]
		public void CopyFromClaimFormsWindow_GivesTheSameForm() {
			//Setup, Claim Forms, Copy: inserts the selected internal form with its items (FormClaimForms.butCopy_Click).
			ClaimForm claimFormInternal=Dc217.FromResource();
			long claimFormNum=ClaimForms.Insert(claimFormInternal,includeClaimFormItems:true);
			Cache.Refresh(InvalidType.ClaimForms);
			ClaimForm claimFormCopy=ClaimForms.GetClaimForm(claimFormNum);
			CollectionAssert.AreEquivalent(Dc217.Describe(Dc217.FromResource()),Dc217.Describe(claimFormCopy));
			Assert.IsFalse(claimFormCopy.PrintImages);
		}

		private static void DeleteInstalledCopies() {
			DataTable table=DataCore.GetTable("SELECT ClaimFormNum FROM claimform WHERE Description='Denti-Cal DC217_2019'");
			foreach(DataRow row in table.Rows) {
				long claimFormNum=PIn.Long(row[0].ToString());
				DataCore.NonQ("DELETE FROM claimformitem WHERE ClaimFormNum="+claimFormNum);
				DataCore.NonQ("DELETE FROM claimform WHERE ClaimFormNum="+claimFormNum);
			}
			Cache.Refresh(InvalidType.ClaimForms);
		}

		private static string Scalar(string command) {
			return DataCore.GetScalar(command);
		}
	}
}
