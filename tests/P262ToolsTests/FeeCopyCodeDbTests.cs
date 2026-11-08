// The Fee Tools' Copy Code tool (Fees.CopyFeesToCode, Open Dental 26.2 item 25) against the scratch database. Synthetic data only: new fee
// schedules, procedure codes, a clinic and a provider made for each test.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262ToolsTests {
	[TestClass]
	public class FeeCopyCodeDbTests {
		private static readonly DateTime _date2027=new DateTime(2027,1,1);
		private static readonly DateTime _date2026=new DateTime(2026,6,1);

		[TestInitialize]
		public void TestInitialize() {
			ToolsDb.Begin();
		}

		#region Helpers
		private static FeeSched NewFeeSched() {
			FeeSched feeSched=new FeeSched();
			feeSched.Description="P262 copy code sched "+ToolsDb.Next();
			feeSched.FeeSchedType=FeeScheduleType.Normal;
			feeSched.ItemOrder=2000+ToolsDb.Next();
			FeeScheds.Insert(feeSched);
			FeeScheds.RefreshCache();
			return feeSched;
		}

		private static ProcedureCode NewCode() {
			ProcedureCode procedureCode=new ProcedureCode();
			procedureCode.ProcCode="PT"+ToolsDb.Next().ToString("0000");
			procedureCode.Descript="Copy code test "+procedureCode.ProcCode;
			procedureCode.AbbrDesc=procedureCode.ProcCode;
			procedureCode.ProcCat=Defs.GetDefsForCategory(DefCat.ProcCodeCats,true).FirstOrDefault()?.DefNum??0;
			ProcedureCodes.Insert(procedureCode);
			ProcedureCodes.RefreshCache();
			return procedureCode;
		}

		private static Clinic NewClinic() {
			Clinic clinic=new Clinic();
			clinic.Abbr="PTC"+ToolsDb.Next();
			clinic.Description="Copy code test clinic";
			Clinics.Insert(clinic);
			Clinics.RefreshCache();
			return clinic;
		}

		private static Provider NewProvider() {
			Provider provider=new Provider();
			provider.Abbr="PTP"+ToolsDb.Next();
			provider.LName="Synthetic";
			provider.FName="Provider";
			Providers.Insert(provider);
			Cache.Refresh(InvalidType.Providers);
			return provider;
		}

		private static Fee InsertFee(FeeSched feeSched,ProcedureCode procedureCode,double amount,DateTime dateEffective,long clinicNum=0,long provNum=0) {
			Fee fee=new Fee();
			fee.FeeSched=feeSched.FeeSchedNum;
			fee.CodeNum=procedureCode.CodeNum;
			fee.ClinicNum=clinicNum;
			fee.ProvNum=provNum;
			fee.Amount=amount;
			fee.DateEffective=dateEffective;
			Fees.Insert(fee);
			return fee;
		}

		///<summary>The fee rows of a code in one schedule as "clinic|prov|date|amount", sorted, for comparing two codes.</summary>
		private static List<string> Describe(ProcedureCode procedureCode,FeeSched feeSched) {
			return Fees.GetAllRowsForCode(procedureCode.CodeNum,feeSched.FeeSchedNum)
				.Select(x => x.ClinicNum+"|"+x.ProvNum+"|"+(x.DateEffective.Year<1880 ? "always" : x.DateEffective.ToString("yyyy-MM-dd"))+"|"
					+x.Amount.ToString("0.00"))
				.OrderBy(x => x)
				.ToList();
		}

		private static void AssertThrowsApplicationException(Action action) {
			try {
				action();
			}
			catch(ApplicationException) {
				return;
			}
			Assert.Fail("Expected an ApplicationException.");
		}
		#endregion Helpers

		[TestMethod]
		public void CopyAll_DestinationGetsTheSourceRowsInEachScheduleTheSourceHas() {
			FeeSched feeSched1=NewFeeSched();
			FeeSched feeSched2=NewFeeSched();
			FeeSched feeSched3=NewFeeSched();
			ProcedureCode codeFrom=NewCode();
			ProcedureCode codeTo=NewCode();
			Clinic clinic=NewClinic();
			Provider provider=NewProvider();
			//Schedule 1: the source has a default, a future-dated fee, a clinic override and a blank provider override.
			InsertFee(feeSched1,codeFrom,100,Fees.DateEffectiveAlways);
			InsertFee(feeSched1,codeFrom,110,_date2027);
			InsertFee(feeSched1,codeFrom,95,Fees.DateEffectiveAlways,clinicNum:clinic.ClinicNum);
			InsertFee(feeSched1,codeFrom,-1,Fees.DateEffectiveAlways,provNum:provider.ProvNum);
			Fee feeToDefault=InsertFee(feeSched1,codeTo,80,Fees.DateEffectiveAlways);
			InsertFee(feeSched1,codeTo,85,_date2026);//the source has no such row: deleted
			Fee feeToClinic=InsertFee(feeSched1,codeTo,95,Fees.DateEffectiveAlways,clinicNum:clinic.ClinicNum);//already the same
			//Schedule 2: only the destination has a fee: not changed.
			InsertFee(feeSched2,codeTo,50,Fees.DateEffectiveAlways);
			//Schedule 3: only the source has a fee: copied.
			InsertFee(feeSched3,codeFrom,30,Fees.DateEffectiveAlways);
			FeeCodeCopyResult result=Fees.CopyFeesToCode(codeFrom.CodeNum,codeTo.CodeNum);
			Assert.IsTrue(result.CountFeeScheds>=2);
			CollectionAssert.AreEqual(Describe(codeFrom,feeSched1),Describe(codeTo,feeSched1));
			CollectionAssert.AreEqual(new List<string> { "0|0|always|50.00" },Describe(codeTo,feeSched2));
			CollectionAssert.AreEqual(new List<string> { "0|0|always|30.00" },Describe(codeTo,feeSched3));
			Assert.AreEqual(3,result.CountInserted,"2027 row, provider override, schedule 3.");
			Assert.AreEqual(1,result.CountUpdated);
			Assert.AreEqual(1,result.CountDeleted);
			Assert.AreEqual(1,result.CountUnchanged);
			Assert.IsFalse(result.IsNoChange);
			//The updated row keeps its number; the unchanged one is not touched.
			Assert.AreEqual(100,Fees.GetOneByFeeNum(feeToDefault.FeeNum).Amount);
			Assert.AreEqual(95,Fees.GetOneByFeeNum(feeToClinic.FeeNum).Amount);
			//The source is not changed.
			CollectionAssert.AreEqual(new List<string> { "0|0|always|100.00","0|0|2027-01-01|110.00","0|"+provider.ProvNum+"|always|-1.00",
				clinic.ClinicNum+"|0|always|95.00" }.OrderBy(x => x).ToList(),Describe(codeFrom,feeSched1));
		}

		[TestMethod]
		public void CopyAll_MakesTheUsualAuditEntries() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode codeFrom=NewCode();
			ProcedureCode codeTo=NewCode();
			InsertFee(feeSched,codeFrom,120,Fees.DateEffectiveAlways);
			Fee feeTo=InsertFee(feeSched,codeTo,60,Fees.DateEffectiveAlways);
			InsertFee(feeSched,codeTo,70,_date2027);
			Fees.CopyFeesToCode(codeFrom.CodeNum,codeTo.CodeNum,feeSched.FeeSchedNum);
			List<SecurityLog> listProcFeeLogs=ToolsDb.GetSecurityLogs(EnumPermType.ProcFeeEdit,codeTo.CodeNum);
			Assert.AreEqual(2,listProcFeeLogs.Count,"One update and one delete.");
			Assert.IsTrue(listProcFeeLogs.Any(x => x.LogText.Contains("Old Fee: $60.00") && x.LogText.Contains("New Fee: $120.00")
				&& x.LogText.Contains("Fee copied from "+codeFrom.ProcCode) && x.LogText.Contains(feeSched.Description)));
			Assert.IsTrue(listProcFeeLogs.Any(x => x.LogText.Contains("Old Fee: $70.00") && x.LogText.Contains("effective 2027-01-01")
				&& x.LogText.Contains("Fee deleted using the Copy Code tool")));
			Assert.AreEqual(1,ToolsDb.GetSecurityLogs(EnumPermType.LogFeeEdit,feeTo.FeeNum).Count(x => x.LogText=="Fee changed"));
		}

		[TestMethod]
		public void CopyOneSchedule_LeavesTheOtherSchedulesAlone() {
			FeeSched feeSched1=NewFeeSched();
			FeeSched feeSched2=NewFeeSched();
			ProcedureCode codeFrom=NewCode();
			ProcedureCode codeTo=NewCode();
			InsertFee(feeSched1,codeFrom,10,Fees.DateEffectiveAlways);
			InsertFee(feeSched2,codeFrom,20,Fees.DateEffectiveAlways);
			InsertFee(feeSched1,codeTo,1,Fees.DateEffectiveAlways);
			FeeCodeCopyResult result=Fees.CopyFeesToCode(codeFrom.CodeNum,codeTo.CodeNum,feeSched2.FeeSchedNum);
			Assert.AreEqual(1,result.CountFeeScheds);
			CollectionAssert.AreEqual(new List<string> { "0|0|always|1.00" },Describe(codeTo,feeSched1));
			CollectionAssert.AreEqual(new List<string> { "0|0|always|20.00" },Describe(codeTo,feeSched2));
		}

		[TestMethod]
		public void Copy_SourceWithoutFees_ChangesNothing_AndASecondCopyNeither() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode codeFrom=NewCode();
			ProcedureCode codeTo=NewCode();
			InsertFee(feeSched,codeTo,44,Fees.DateEffectiveAlways);
			FeeCodeCopyResult result=Fees.CopyFeesToCode(codeFrom.CodeNum,codeTo.CodeNum);
			Assert.AreEqual(0,result.CountFeeScheds);
			Assert.IsTrue(result.IsNoChange);
			CollectionAssert.AreEqual(new List<string> { "0|0|always|44.00" },Describe(codeTo,feeSched));
			InsertFee(feeSched,codeFrom,55,Fees.DateEffectiveAlways);
			Assert.IsFalse(Fees.CopyFeesToCode(codeFrom.CodeNum,codeTo.CodeNum).IsNoChange);
			FeeCodeCopyResult resultAgain=Fees.CopyFeesToCode(codeFrom.CodeNum,codeTo.CodeNum);
			Assert.IsTrue(resultAgain.IsNoChange);
			Assert.AreEqual(1,resultAgain.CountUnchanged);
		}

		[TestMethod]
		public void Copy_InvalidCodes_Throw() {
			ProcedureCode code=NewCode();
			AssertThrowsApplicationException(() => Fees.CopyFeesToCode(code.CodeNum,code.CodeNum));
			AssertThrowsApplicationException(() => Fees.CopyFeesToCode(0,code.CodeNum));
			AssertThrowsApplicationException(() => Fees.CopyFeesToCode(code.CodeNum,0));
		}
	}
}
