// v24_4-account-salestax-proc: the sales-tax procedure code setting (Preferences > Account > Adjustments). Open Dental's key for it is not public,
// so it is a non-enum preference row inserted on the first save; a database without the row reads as "not set".
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BACCTTests {
	[TestClass]
	public class SalesTaxSettingDbTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDbAssembly.ConnectOrSkip();
			SalesTaxData.EnsureCodes();
		}

		private static void DeleteRow() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+SalesTaxProcs.PREF_PROC_CODE+"'");
			Cache.Refresh(InvalidType.Prefs);
		}

		private static long CountRows() {
			return PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+SalesTaxProcs.PREF_PROC_CODE+"'"));
		}

		[TestMethod]
		public void PrefName_IsNotInTheEnum() {
			//The name is only proposed by the spec; RegistryTests keeps unconfirmed names out of PrefName.
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),SalesTaxProcs.PREF_PROC_CODE));
		}

		[TestMethod]
		public void MissingRow_ReadsAsNotSet() {
			DeleteRow();
			Assert.AreEqual(0,CountRows());
			Assert.AreEqual("",SalesTaxProcs.GetProcCodeSetting());
			Assert.AreEqual(0,SalesTaxProcs.GetCodeNum());
			Assert.IsFalse(SalesTaxProcs.IsEnabled());
			Assert.AreEqual("",SalesTaxProcs.GetProcCode());
		}

		[TestMethod]
		public void SavingBlank_WithoutRow_WritesNothing() {
			DeleteRow();
			Assert.IsFalse(SalesTaxProcs.SaveProcCodeSetting(""));
			Assert.IsFalse(SalesTaxProcs.SaveProcCodeSetting("  "));
			Assert.AreEqual(0,CountRows());
		}

		[TestMethod]
		public void FirstSave_InsertsOneRow_ThenUpdatesIt() {
			DeleteRow();
			Assert.IsTrue(SalesTaxProcs.SaveProcCodeSetting(" "+SalesTaxData.CODE_TAX+" "));
			Assert.AreEqual(1,CountRows());
			Assert.AreEqual(SalesTaxData.CODE_TAX,DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+SalesTaxProcs.PREF_PROC_CODE+"'"));
			Assert.AreEqual(SalesTaxData.CodeNum(SalesTaxData.CODE_TAX),SalesTaxProcs.GetCodeNum(),"The cache of this workstation is updated.");
			Assert.IsTrue(SalesTaxProcs.IsEnabled());
			Assert.IsFalse(SalesTaxProcs.SaveProcCodeSetting(SalesTaxData.CODE_TAX),"Same value: no change.");
			Assert.IsTrue(SalesTaxProcs.SaveProcCodeSetting(""));
			Assert.AreEqual(1,CountRows(),"Turning it off keeps the row, blank.");
			Assert.IsFalse(SalesTaxProcs.IsEnabled());
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual("",SalesTaxProcs.GetProcCodeSetting(),"What other workstations read after the Prefs signal.");
			Assert.IsTrue(SalesTaxProcs.SaveProcCodeSetting(SalesTaxData.CODE_TAX));
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual(SalesTaxData.CODE_TAX,SalesTaxProcs.GetProcCode());
		}

		[TestMethod]
		public void IsValidSetting_AllowsBlankAndExistingCodesOnly() {
			Assert.IsTrue(SalesTaxProcs.IsValidSetting(""));
			Assert.IsTrue(SalesTaxProcs.IsValidSetting(null));
			Assert.IsTrue(SalesTaxProcs.IsValidSetting(SalesTaxData.CODE_TAX));
			Assert.IsTrue(SalesTaxProcs.IsValidSetting(" "+SalesTaxData.CODE_TAX));
			Assert.IsFalse(SalesTaxProcs.IsValidSetting("NOSUCHCODE"));
		}

		[TestMethod]
		public void UnknownCode_IsNotEnabled() {
			SalesTaxProcs.UpsertPref("NOSUCHCODE");
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual("NOSUCHCODE",SalesTaxProcs.GetProcCodeSetting());
			Assert.AreEqual(0,SalesTaxProcs.GetCodeNum());
			Assert.IsFalse(SalesTaxProcs.IsEnabled(),"Quick Procs hides Sales Tax when the code does not exist.");
		}

		[TestMethod]
		public void StoredCodeNum_IsAccepted() {
			//In case the row Open Dental itself wrote under this name holds a CodeNum instead of a ProcCode.
			long codeNum=SalesTaxData.CodeNum(SalesTaxData.CODE_TAX);
			SalesTaxProcs.UpsertPref(codeNum.ToString());
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual(codeNum,SalesTaxProcs.GetCodeNum());
			Assert.AreEqual(SalesTaxData.CODE_TAX,SalesTaxProcs.GetProcCode());
			Assert.AreEqual(0,SalesTaxProcs.GetCodeNumFromSetting("987654321"),"A number that is no CodeNum.");
			Assert.AreEqual(0,SalesTaxProcs.GetCodeNumFromSetting("-5"));
			SalesTaxProcs.SaveProcCodeSetting(SalesTaxData.CODE_TAX);
		}

		[TestMethod]
		public void IsSalesTaxProc_MatchesTheConfiguredCodeOnly() {
			SalesTaxData.SetUp(10,SalesTaxData.CODE_TAX);
			Assert.IsTrue(SalesTaxProcs.IsSalesTaxProc(new Procedure { CodeNum=SalesTaxData.CodeNum(SalesTaxData.CODE_TAX) }));
			Assert.IsFalse(SalesTaxProcs.IsSalesTaxProc(new Procedure { CodeNum=SalesTaxData.CodeNum(SalesTaxData.CODE_TAXED) }));
			Assert.IsFalse(SalesTaxProcs.IsSalesTaxProc(new Procedure { CodeNum=0 }));
			Assert.IsFalse(SalesTaxProcs.IsSalesTaxProc(null));
			SalesTaxProcs.SaveProcCodeSetting("");
			Assert.IsFalse(SalesTaxProcs.IsSalesTaxProc(new Procedure { CodeNum=SalesTaxData.CodeNum(SalesTaxData.CODE_TAX) }),"Off: no code is sales tax.");
		}
	}
}
