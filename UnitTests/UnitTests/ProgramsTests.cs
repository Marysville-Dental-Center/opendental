using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace UnitTests.Programs_Tests {
	///<summary>Open Dental HQ used to be able to disable and delete program links remotely (HqProgram.Download and Programs.IsEnabledByHq).
	///That kill switch was removed. These tests make sure HQ state can never disable or delete a program link.</summary>
	[TestClass]
	public class ProgramsTests:TestBase {
		private static List<OpenDentBusiness.Program> _listTestProgs=new List<OpenDentBusiness.Program>();

		private static void ResetTestProgValues() {
			foreach(OpenDentBusiness.Program prog in _listTestProgs) {
				prog.IsDisabledByHq=false;
			}
		}

		[ClassInitialize]
		public static void SetUpTest(TestContext testContext) {
			_listTestProgs=Programs.GetWhere(x=> x.ProgName.In(ProgramName.PDMP.ToString(),ProgramName.DentalIntel.ToString(),ProgramName.Xcharge.ToString()));
			ResetTestProgValues();
		}

		[TestCleanup]
		public void TearDownTest() {
			ResetTestProgValues();
		}

		[TestMethod]
		public void Programs_IsEnabledByHq_IgnoresIsDisabledByHq() {
			foreach(OpenDentBusiness.Program program in _listTestProgs) {
				program.IsDisabledByHq=true;//Pretend an old database still has the HQ flag set.
				Assert.IsTrue(Programs.IsEnabledByHq(program,out string err));
				Assert.AreEqual("",err);
			}
		}

		[TestMethod]
		public void Programs_IsEnabledByHq_NeverDeletesProgram() {
			List<long> listProgramNums=_listTestProgs.Select(x => x.ProgramNum).ToList();
			foreach(OpenDentBusiness.Program program in _listTestProgs) {
				Programs.IsEnabledByHq(program,out string _);
			}
			Programs.RefreshCache();
			foreach(long programNum in listProgramNums) {
				Assert.IsNotNull(Programs.GetProgram(programNum));
			}
		}

		[TestMethod]
		public void Programs_IsEnabledByHq_NullProgram() {
			Assert.IsFalse(Programs.IsEnabledByHq((OpenDentBusiness.Program)null,out string err));
			Assert.IsFalse(string.IsNullOrEmpty(err));
		}
	}
}
