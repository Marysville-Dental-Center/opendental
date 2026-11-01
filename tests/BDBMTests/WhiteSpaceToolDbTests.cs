// "Notes With Too Much White Space" moves from the Checks list to the Tools tab (Open Dental 25.3, also 25.2.41), on the scratch database.
// Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BDBMTests {
	[TestClass]
	public class WhiteSpaceToolDbTests {
		[TestInitialize]
		public void Init() {
			BDbmDb.Begin();
		}

		[TestMethod]
		public void NotesWithTooMuchWhiteSpace_IsNoLongerACheck() {
			MethodInfo methodInfo=typeof(DatabaseMaintenances).GetMethod(nameof(DatabaseMaintenances.NotesWithTooMuchWhiteSpace));
			Assert.IsNull(methodInfo.GetCustomAttribute(typeof(DbmMethodAttr)),"The attribute is what puts a method on the Checks list.");
			Assert.IsFalse(DatabaseMaintenances.GetMethodsForDisplay().Any(x => x.Name==nameof(DatabaseMaintenances.NotesWithTooMuchWhiteSpace)));
			Assert.IsFalse(DatabaseMaintenances.GetMethodsForDisplay(hasOnlyPatNumMethods:true)
				.Any(x => x.Name==nameof(DatabaseMaintenances.NotesWithTooMuchWhiteSpace)));
		}

		[TestMethod]
		public void NotesWithTooMuchWhiteSpace_AnOfficeRowForItIsHarmless() {
			//An office database that comes from Open Dental has a databasemaintenance row for the method; the window builds its lists from the methods
			//that carry the attribute, so the row matches nothing.
			DataCore.NonQ("INSERT INTO databasemaintenance (MethodName,IsHidden,IsOld,DateLastRun) "
				+"VALUES ('"+nameof(DatabaseMaintenances.NotesWithTooMuchWhiteSpace)+"',0,0,'0001-01-01')");
			DatabaseMaintenances.InsertMissingDBMs(DatabaseMaintenances.GetMethodsForDisplay().Select(x => x.Name).ToList());
			Assert.IsTrue(DatabaseMaintenances.GetAll().Any(x => x.MethodName==nameof(DatabaseMaintenances.NotesWithTooMuchWhiteSpace)));
			Assert.IsTrue(DatabaseMaintenances.GetAll().Any(x => x.MethodName==nameof(DatabaseMaintenances.EmployeeMissingSupervisor)));
			Assert.IsTrue(DatabaseMaintenances.GetAll().Any(x => x.MethodName==nameof(DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText)));
		}

		[TestMethod]
		public void NotesWithTooMuchWhiteSpace_ToolRunInFixModeStillTrims() {
			Patient patient=BDbmDb.CreatePatient("WhiteSpace");
			Commlog commlog=CommlogT.CreateCommlog(patient.PatNum,text:"placeholder");
			BDbmDb.SetText("commlog","Note","CommlogNum",commlog.CommlogNum,"Called patient"+new string('\t',40));
			Commlog commlogSpaces=CommlogT.CreateCommlog(patient.PatNum,text:"placeholder");
			BDbmDb.SetText("commlog","Note","CommlogNum",commlogSpaces.CommlogNum,"Left message"+new string(' ',400));
			string log=DatabaseMaintenances.NotesWithTooMuchWhiteSpace(true,DbmMode.Fix);
			StringAssert.Contains(log,"Notes with too much white space fixed:");
			Assert.AreEqual("Called patient",BDbmDb.GetText("commlog","Note","CommlogNum",commlog.CommlogNum));
			Assert.AreEqual("Left message",BDbmDb.GetText("commlog","Note","CommlogNum",commlogSpaces.CommlogNum));
			StringAssert.Contains(DatabaseMaintenances.NotesWithTooMuchWhiteSpace(true,DbmMode.Fix),"fixed: 0");
		}
	}
}
