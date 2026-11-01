// The scratch database names of these tests: unique per run, so runs from other worktrees on the same local MariaDB never drop each other's
// databases. Synthetic, empty databases only.
// V-DXC2 (fix round r1) wrote these tests for its own copy of the per-run names. port/int-5 kept the helper that was already merged
// (ScratchDb.RunName, IsRunName, DropStaleRunDatabases; see ScratchDbNameTests) and points these tests at it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReleaseFlowTests {
	[TestClass]
	public class ScratchDbTests {
		[TestMethod]
		public void ScratchName_IsUniqueToThisRun() {
			string nameAdoption=ScratchDb.RunName("adoption");
			Assert.AreEqual(nameAdoption,ScratchDb.RunName("adoption"),"One run uses one name per purpose.");
			Assert.AreNotEqual(nameAdoption,ScratchDb.RunName("release"));
			StringAssert.StartsWith(nameAdoption,ScratchDb.PREFIX);
			StringAssert.EndsWith(nameAdoption,"_adoption");
			StringAssert.Contains(nameAdoption,"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_","The process id keeps two runs started in the same second apart.");
			Assert.IsTrue(nameAdoption.Length<=64,nameAdoption);
			Assert.IsTrue(ScratchDb.IsRunName(nameAdoption),nameAdoption);
			//The fixed names older versions of these tests used, which concurrent runs shared, are no longer used and are never dropped as stale.
			Assert.AreNotEqual("releaseflow_adoption",nameAdoption);
			Assert.AreNotEqual("releaseflow_release",ScratchDb.RunName("release"));
			Assert.IsFalse(ScratchDb.IsRunName("releaseflow_adoption"));
			Assert.IsFalse(ScratchDb.IsRunName("releaseflow_release"));
		}

		[TestMethod]
		public void ScratchName_TooLong_Throws() {
			try {
				ScratchDb.RunName(new string('x',60));
				Assert.Fail("A name over the 64-character MariaDB limit must throw.");
			}
			catch(ArgumentException) {
			}
		}

		[TestMethod]
		public void DropAndCreate_LeaveOtherDatabasesAlone() {
			foreach(string name in new[] { "od_253_schema","releaseflow_adoption","mysql",null }) {
				//The kept helper's Drop ignores every name outside the per-run scheme (it never throws), and Create refuses it.
				Assert.IsFalse(ScratchDb.IsRunName(name),"Drop would ignore "+name);
				ScratchDb.Drop(name);
				try {
					ScratchDb.Create(name,is253:false);
					Assert.Fail("Create must refuse "+name);
				}
				catch(ArgumentException) {
				}
			}
		}

		[TestMethod]
		public void DropStale_DropsOnlyOldScratchDatabases() {
			ScratchDb.RequireServer();
			int random=new Random().Next(1000,10000);
			string nameStale=ScratchDb.PREFIX+DateTime.UtcNow.AddHours(-13).ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_1_"+random+"_stale";
			string nameOtherRun=ScratchDb.PREFIX+DateTime.UtcNow.AddHours(-1).ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_1_"+random+"_running";
			//Not made by RunName, like the old fixed names; unique so no other run's database is touched.
			string nameUnknown=ScratchDb.PREFIX+"legacy_"+random;
			try {
				foreach(string name in new[] { nameStale,nameOtherRun,nameUnknown }) {
					ScratchDb.Exec(null,"CREATE DATABASE IF NOT EXISTS "+name);
				}
				ScratchDb.DropStaleRunDatabases();
				Assert.IsFalse(Exists(nameStale),"A database left behind more than 12 hours ago is dropped.");
				Assert.IsTrue(Exists(nameOtherRun),"A run that may still be going keeps its database.");
				Assert.IsTrue(Exists(nameUnknown),"A name outside the naming scheme is never dropped.");
			}
			finally {
				foreach(string name in new[] { nameStale,nameOtherRun,nameUnknown }) {
					ScratchDb.Exec(null,"DROP DATABASE IF EXISTS "+name);
				}
			}
		}

		private static bool Exists(string name) {
			return ScratchDb.Scalar(null,"SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+name+"'")!="0";
		}
	}
}
