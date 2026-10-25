// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChartLetterTests {
	///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
	public static class Expect {
		public static T Throws<T>(Action action,string message="") where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message+" "+message);
			}
			Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown. "+message);
			return null;
		}
	}
}
