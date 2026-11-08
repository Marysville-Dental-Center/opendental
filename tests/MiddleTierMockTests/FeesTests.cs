// The Middle Tier test of the legacy FeesTests (UnitTests/UnitTests/FeesTests.cs, Fees_GetByFeeSchedNumsClinicNums_MiddleTier), copied here by
// package S0-T2 of the .NET 10 plan; the original stays in UnitTests. Over the Middle Tier, Fees.GetByFeeSchedNumsClinicNums asks for 100 fee
// schedules per call (Fees.cs, there to avoid running out of memory), so 300 schedules take three round trips through OpenDentalServerMockIIS
// and must still return all 600 fees. The test keeps its name and its arrange, act and assert; only the harness changed: the s0t2_ copy of
// ScratchDb's database (ScratchDbCopy.cs) instead of the legacy unittest database and TestBase, and a mock server set per test. The arrange runs
// on the direct connection as in the legacy class; DataAction.RunMiddleTierMock runs the act as a Middle Tier client through that mock.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.WebServices;
using UnitTestsCore;

namespace MiddleTierMockTests {
	[TestClass]
	public class FeesTests {

		///<summary>This method will execute just before each test in this class.</summary>
		[TestInitialize]
		public void SetupTest() {
			ScratchDbCopy.BeginTest();
			//The legacy class ran on the direct connection with no mock server set, so RunMiddleTierMock made one that used the test's connection.
			//This mock connects to the same database with its own settings, as the other tests of this assembly do; the role stays ClientDirect.
			OpenDentalServerProxy.MockOpenDentalServerCur=ScratchDbCopy.NewMockServer();
		}

		///<summary>This method will execute after each test in this class.</summary>
		[TestCleanup]
		public void TearDownTest() {
			ScratchDbCopy.EndTest();
		}

		[ClassCleanup]
		public static void TearDownClass() {
			ScratchDbCopy.DropCopy();
		}

		///<summary>Gets fees for a bunch of fee schedules over Middle Tier.</summary>
		[TestMethod]
		public void Fees_GetByFeeSchedNumsClinicNums_MiddleTier() {
			List<long> listFeeSchedNums=new List<long>();
			long codeNum1=ProcedureCodeT.GetCodeNum("D1110");
			long codeNum2=ProcedureCodeT.GetCodeNum("D1206");
			for(int i=0;i<300;i++) {
				FeeSched feeSched=FeeSchedT.GetNewFeeSched(FeeScheduleType.Normal,"FS"+i);
				FeeT.GetNewFee(feeSched.FeeSchedNum,codeNum1,11);
				FeeT.GetNewFee(feeSched.FeeSchedNum,codeNum2,13);
				listFeeSchedNums.Add(feeSched.FeeSchedNum);
			}
			DataAction.RunMiddleTierMock(() => {
				List<FeeLim> listFees=Fees.GetByFeeSchedNumsClinicNums(listFeeSchedNums,new List<long> { 0 });
				Assert.AreEqual(600,listFees.Count);
			});
		}
	}
}
