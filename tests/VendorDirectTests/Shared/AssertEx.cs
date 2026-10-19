// Test helper for every vendor-direct package: exception asserts and async test bodies under MSTest v1.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests {
	///<summary>MSTest v1 (the framework these projects use) has no Assert.ThrowsException and doesn't await async test methods reliably, so test
	///methods stay synchronous and run their async body through Run.</summary>
	public static class AssertEx {
		///<summary>Runs an async test body to completion on a pool thread, rethrowing its exception.</summary>
		public static void Run(Func<Task> funcAsync) {
			Task.Run(funcAsync).GetAwaiter().GetResult();
		}

		///<summary>Asserts that action throws T (or a subclass) and returns it.</summary>
		public static T Throws<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message);
			}
			Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown.");
			return null;
		}

		///<summary>Asserts that the task throws T (or a subclass) and returns it.</summary>
		public static async Task<T> ThrowsAsync<T>(Func<Task> funcAsync) where T:Exception {
			try {
				await funcAsync();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message);
			}
			Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown.");
			return null;
		}
	}
}
