using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace UnitTests.Prefs_Tests {
	[TestClass]
	public class PrefsTests:TestBase {

		///<summary>This method will execute only once, just before any tests in this class run.</summary>
		[ClassInitialize]
		public static void SetupClass(TestContext testContext) {
		}

		///<summary>This method will execute just before each test in this class.</summary>
		[TestInitialize]
		public void SetupTest() {
		}

		///<summary>This method will execute after each test in this class.</summary>
		[TestCleanup]
		public void TearDownTest() {
		}

		///<summary>This method will execute only once, just after all tests in this class have run.</summary>
		[ClassCleanup]
		public static void TearDownClass() {
		}

		[TestMethod]
		public void Prefs_UpdateInt_BitwiseEnum() {
			//This unit test is for making sure bitwise enums stored in preferences can be stored and retrieved correctly.
			//The preference that is used is irrelevant since all preferences are technically stored as strings in the database.
			//However, we'll go ahead and use a bitwise enum preference to make the scenario as realistic as possible.
			//The ITRANS carrier import options (FormCarriers) are such a preference, saved with Prefs.UpdateInt. Until package S0-5 of the .NET 10
			//plan this test used the hosted e-mail status preference, which the fork removed with Open Dental's hosted e-mail service.
			//Get the current value of the ItransImportFields preference so that it can be reset after our testing.
			ItransImportFields itransImportFieldsOrig=PrefC.GetEnum<ItransImportFields>(PrefName.ItransImportFields);
			try {
				ItransImportFields itransImportFieldsExpected=ItransImportFields.Phone | ItransImportFields.AddMissing;
				Assert.IsTrue(itransImportFieldsExpected.HasFlag(ItransImportFields.Phone));
				Assert.IsTrue(itransImportFieldsExpected.HasFlag(ItransImportFields.AddMissing));
				//Update the enum in the database to a bitwise value.
				Prefs.UpdateInt(PrefName.ItransImportFields,(int)itransImportFieldsExpected);
				//Pull the preference value back out of the database and assert that the bitwise value was preserved.
				ItransImportFields itransImportFieldsActual=PrefC.GetEnum<ItransImportFields>(PrefName.ItransImportFields);
				Assert.IsTrue(itransImportFieldsActual.HasFlag(ItransImportFields.Phone));
				Assert.IsTrue(itransImportFieldsActual.HasFlag(ItransImportFields.AddMissing));
				Assert.IsFalse(itransImportFieldsActual.HasFlag(ItransImportFields.Address));
				Assert.AreEqual(itransImportFieldsExpected,itransImportFieldsActual);
			}
			finally {
				//Put the preference back to the way it was at the beginning of the test.
				Prefs.UpdateInt(PrefName.ItransImportFields,(int)itransImportFieldsOrig);
			}
		}

		/// <summary></summary>
		[TestMethod]
		public void PrefC_AgingAllowedToStart_AgingBeginTimeNotSet() {
			Prefs.UpdateDateT(PrefName.AgingBeginDateTime,DateTime.MinValue);
			Assert.IsTrue(PrefC.IsAgingAllowedToStart());
		}

		[TestMethod]
		public void PrefC_AgingAllowedToStart_AgingBEnginTimeIsSet_LessThan24Hours() {
			DateTime_.SetNow(() => new DateTime(2024,3,20,10,0,0));
			Prefs.UpdateDateT(PrefName.AgingBeginDateTime,new DateTime(2024,3,19,12,0,0));
			Assert.IsFalse(PrefC.IsAgingAllowedToStart());
		}

		[TestMethod]
		public void PrefC_AgingAllowedToStart_AgingBEnginTimeIsSet_Equal24Hours() {
			DateTime_.SetNow(() => new DateTime(2024,3,20,10,0,0));
			Prefs.UpdateDateT(PrefName.AgingBeginDateTime,new DateTime(2024,3,19,10,0,0));
			Assert.IsTrue(PrefC.IsAgingAllowedToStart());
		}

		[TestMethod]
		public void PrefC_AgingAllowedToStart_AgingBEnginTimeIsSet_GreaterThan24Hours() {
			DateTime_.SetNow(() => new DateTime(2024,3,20,10,0,0));
			Prefs.UpdateDateT(PrefName.AgingBeginDateTime,new DateTime(2024,3,19,8,0,0));
			Assert.IsTrue(PrefC.IsAgingAllowedToStart());
		}
		/************************************************************************************************************************************************

		///<summary>Duplicate native preferences are not allowed and should throw an exception when filling the cache.</summary>
		[TestMethod]
		public void Prefs_FillCacheFromTable_DuplicateDetected() {
			//Create a duplicate preference that is native to Open Dental.
			List<Pref> listPrefs=new List<Pref>() {
				new Pref(){ PrefName=PrefName.AccountAllowFutureDebits.ToString() },
				new Pref(){ PrefName=PrefName.AccountAllowFutureDebits.ToString() },
			};
			bool hadException=false;
			try {
				Prefs.FillCacheFromTable(OpenPmsDentalBusiness.Crud.PrefCrud.ListToTable(listPrefs));
			}
			catch(Exception e) {
				e.DoNothing();
				hadException=true;
			}
			Prefs.RefreshCache();//Put the preference cache back the way it was.
			Assert.IsTrue(hadException);//An exception should have been thrown because these duplicate preferences are native.
		}

		///<summary>Duplicate non-native preferences are allowed should not throw an exception when filling the cache.</summary>
		[TestMethod]
		public void Prefs_FillCacheFromTable_DuplicateAllowed() {
			//Create a duplicate preference that is not native to Open Dental.
			List<Pref> listPrefs=new List<Pref>() {
				new Pref(){ PrefName=MethodBase.GetCurrentMethod().Name },
				new Pref(){ PrefName=MethodBase.GetCurrentMethod().Name },
			};
			bool hadException=false;
			try {
				Prefs.FillCacheFromTable(OpenPmsDentalBusiness.Crud.PrefCrud.ListToTable(listPrefs));
			}
			catch(Exception e) {
				e.DoNothing();
				hadException=true;
			}
			Prefs.RefreshCache();//Put the preference cache back the way it was.
			Assert.IsFalse(hadException);//No exception should have been thrown because these duplicate preferences are not native.
		}

		************************************************************************************************************************************************/

	}
}
