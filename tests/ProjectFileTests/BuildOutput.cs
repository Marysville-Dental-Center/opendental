// The rule for a test that checks a built file (package S1-T and section 3 of the .NET 10 plan, docs/specs/net10-plan.md): when the file is
// not built, the test is inconclusive on a developer's run of one assembly, but fails when OPMS_REQUIRE_BUILD_OUTPUT=1, which the build lane and
// the test runner's -RequireBuildOutput (the stage gates' Full runs) set, so a check never passes a gate by being skipped. Linked into every
// test project with such a check, as tests\ForkMigrationTests\TestDb.cs is; its own tests are in tests\ProjectFileTests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	internal static class BuildOutput {
		public const string VARIABLE_NAME="OPMS_REQUIRE_BUILD_OUTPUT";

		///<summary>True when OPMS_REQUIRE_BUILD_OUTPUT is 1 (or true) in this process.</summary>
		public static bool IsRequired {
			get { return IsRequiredValue(Environment.GetEnvironmentVariable(VARIABLE_NAME)); }
		}

		///<summary>The rule for a value of OPMS_REQUIRE_BUILD_OUTPUT: "1" or "true" (any case, spaces trimmed) requires the build output.</summary>
		public static bool IsRequiredValue(string value) {
			string trimmed=(value??"").Trim();
			return trimmed=="1" || trimmed.Equals("true",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Ends the test because a built file it checks is missing: it fails when the build output is required, otherwise it is
		///inconclusive. The message says what is not built and how to build it.</summary>
		public static void NotBuilt(string message) {
			NotBuilt(message,IsRequired);
		}

		///<summary>NotBuilt with the rule's answer given, for the rule's own tests.</summary>
		public static void NotBuilt(string message,bool isRequired) {
			if(isRequired) {
				Assert.Fail(message+" ("+VARIABLE_NAME+"=1: a missing build output fails.)");
			}
			Assert.Inconclusive(message);
		}

		///<summary>Calls NotBuilt with the message unless the file exists.</summary>
		public static void RequireFile(string path,string message) {
			if(!File.Exists(path)) {
				NotBuilt(message);
			}
		}
	}
}
