// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	internal static class TestUtil {
		///<summary>Runs action and returns the exception of type T it throws. Fails if it throws nothing or another type.
		///(MSTest v1 has no Assert.ThrowsException.)</summary>
		public static T AssertThrows<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+", got "+ex.GetType().Name+": "+ex.Message);
			}
			Assert.Fail("Expected "+typeof(T).Name+", nothing was thrown.");
			return null;
		}
	}
}
