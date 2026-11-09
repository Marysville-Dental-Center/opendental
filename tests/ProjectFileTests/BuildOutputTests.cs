// The rule for tests that check a built file: inconclusive when the file is not built, a failure when OPMS_REQUIRE_BUILD_OUTPUT=1.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class BuildOutputTests {
		[TestMethod]
		public void IsRequiredValue_OneOrTrue() {
			Assert.IsTrue(BuildOutput.IsRequiredValue("1"));
			Assert.IsTrue(BuildOutput.IsRequiredValue(" 1 "));
			Assert.IsTrue(BuildOutput.IsRequiredValue("true"));
			Assert.IsTrue(BuildOutput.IsRequiredValue("TRUE"));
			Assert.IsFalse(BuildOutput.IsRequiredValue(null));
			Assert.IsFalse(BuildOutput.IsRequiredValue(""));
			Assert.IsFalse(BuildOutput.IsRequiredValue("0"));
			Assert.IsFalse(BuildOutput.IsRequiredValue("false"));
			Assert.IsFalse(BuildOutput.IsRequiredValue("yes"));
		}

		[TestMethod]
		public void NotBuilt_FailsWhenRequired() {
			try {
				BuildOutput.NotBuilt(@"X\bin\Debug\X.dll is not built.",true);
			}
			catch(AssertFailedException ex) {
				StringAssert.Contains(ex.Message,@"X\bin\Debug\X.dll is not built.");
				StringAssert.Contains(ex.Message,"OPMS_REQUIRE_BUILD_OUTPUT=1");
				return;
			}
			Assert.Fail("No failure.");
		}

		[TestMethod]
		public void NotBuilt_IsInconclusiveOtherwise() {
			try {
				BuildOutput.NotBuilt(@"X\bin\Debug\X.dll is not built.",false);
			}
			catch(AssertInconclusiveException ex) {
				StringAssert.Contains(ex.Message,@"X\bin\Debug\X.dll is not built.");
				return;
			}
			Assert.Fail("Not inconclusive.");
		}

		[TestMethod]
		public void NotBuilt_FollowsTheVariable() {
			string valueBefore=Environment.GetEnvironmentVariable(BuildOutput.VARIABLE_NAME);
			try {
				foreach(string value in new[] { "1",null }) {
					Environment.SetEnvironmentVariable(BuildOutput.VARIABLE_NAME,value);
					Assert.AreEqual(value=="1",BuildOutput.IsRequired);
					Type typeExpected=value=="1" ? typeof(AssertFailedException) : typeof(AssertInconclusiveException);
					try {
						BuildOutput.RequireFile(Path.Combine(Path.GetTempPath(),"ProjectFileTests_"+Guid.NewGuid().ToString("N")+".dll"),"Not built.");
						Assert.Fail("A missing file passed.");
					}
					catch(UnitTestAssertException ex) {
						Assert.AreEqual(typeExpected,ex.GetType(),value??"(not set)");
					}
				}
				BuildOutput.RequireFile(typeof(BuildOutputTests).Assembly.Location,"The test assembly is there.");
			}
			finally {
				Environment.SetEnvironmentVariable(BuildOutput.VARIABLE_NAME,valueBefore);
			}
		}
	}
}
