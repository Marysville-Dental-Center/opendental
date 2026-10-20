// Shared setup for the ERX package's tests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Globalization;
using System.Threading;

namespace VendorDirectTests.Erx {
	public static class ErxTestData {
		///<summary>Lans.g returns the English text unchanged only under en-US; any other culture would look up translations in the database.</summary>
		public static void UseEnUs() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
		}
	}
}
