// MSTest v1 has no Assert.ThrowsException.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SshHostKeyTests {
	public static class TestAssert {
		///<summary>Runs action and returns the exception it threw, which must be a T (or derive from it). Fails when it throws nothing or something
		///else.</summary>
		public static T Throws<T>(Action action,string message="") where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+", got "+ex.GetType().Name+": "+ex.Message+" "+message);
			}
			Assert.Fail("Expected "+typeof(T).Name+", nothing was thrown. "+message);
			return null;
		}
	}
}
