// Per-run scratch database names: several test runs share the one local MariaDB server, so no run may recreate or drop another run's database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReleaseFlowTests {
	[TestClass]
	public class ScratchDbNameTests {
		///<summary>The fixed names older versions of these tests used. Two runs at the same time collided on them.</summary>
		private static readonly string[] _arrayFixedNames=new[] { "releaseflow_adoption","releaseflow_release" };

		private static string DbNameOf(Type type) {
			FieldInfo fieldInfo=type.GetField("DB_NAME",BindingFlags.NonPublic|BindingFlags.Static);
			Assert.IsNotNull(fieldInfo,type.Name+" has no DB_NAME field.");
			return (string)fieldInfo.GetValue(null);
		}

		private static bool DatabaseExists(string name) {
			return ScratchDb.Scalar(null,"SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+name+"'")!="0";
		}

		///<summary>A name in the per-run scheme as another run (process id 1) that started at the given time would make it.</summary>
		private static string OtherRunName(DateTime dateTimeUtc,string purpose) {
			return ScratchDb.PREFIX+dateTimeUtc.ToString("yyMMddHHmmss")+"_1_"+new Random().Next(1000,10000)+"_"+purpose;
		}

		[TestMethod]
		public void RunName_IsUniqueToThisRun() {
			string name=ScratchDb.RunName("adoption");
			Assert.IsTrue(ScratchDb.IsRunName(name),name);
			StringAssert.Contains(name,"_"+Process.GetCurrentProcess().Id+"_");
			StringAssert.EndsWith(name,"_adoption");
			Assert.IsTrue(name.Length<=64,name);
			Assert.AreEqual(name,ScratchDb.RunName("adoption"),"One run uses one name per purpose.");
			ScratchDb.RequireRunName(name);
			try {
				ScratchDb.RunName(new string('x',60));
				Assert.Fail("A name over the MariaDB limit of 64 characters is refused.");
			}
			catch(ArgumentException) {
			}
		}

		[TestMethod]
		public void DbTests_UsePerRunNames_NeverTheFixedOnes() {
			string nameAdoption=DbNameOf(typeof(AdoptionDbTests));
			string nameRelease=DbNameOf(typeof(ReleaseChannelDbTests));
			foreach(string name in new[] { nameAdoption,nameRelease }) {
				Assert.IsTrue(ScratchDb.IsRunName(name),name);
				CollectionAssert.DoesNotContain(_arrayFixedNames,name);
				ScratchDb.RequireRunName(name);
			}
			Assert.AreNotEqual(nameAdoption,nameRelease);
		}

		[TestMethod]
		public void RequireRunName_RefusesFixedNamesAndOtherRunsNames() {
			foreach(string name in new[] { "releaseflow_adoption","releaseflow_release",OtherRunName(DateTime.UtcNow,"adoption"),"opendental",null }) {
				try {
					ScratchDb.RequireRunName(name);
					Assert.Fail("Not this run's name: "+name);
				}
				catch(ArgumentException) {
				}
			}
			Assert.IsFalse(ScratchDb.IsRunName("releaseflow_adoption"));
			Assert.IsFalse(ScratchDb.IsRunName("opendental"));
			Assert.IsTrue(ScratchDb.IsRunName(OtherRunName(DateTime.UtcNow,"adoption")));
		}

		[TestMethod]
		public void Create_RefusesANameOutsideThisRun_WithoutTouchingTheServer() {
			ScratchDb.RequireServer();
			string nameOther=OtherRunName(DateTime.UtcNow,"create");
			ScratchDb.Exec(null,"CREATE DATABASE `"+nameOther+"`");
			try {
				ScratchDb.Exec(nameOther,"CREATE TABLE marker (Id int NOT NULL)");
				try {
					ScratchDb.Create(nameOther,is253:false);
					Assert.Fail("Create must refuse another run's name.");
				}
				catch(ArgumentException) {
				}
				Assert.AreEqual("1",ScratchDb.Scalar(null,"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+nameOther
					+"' AND TABLE_NAME='marker'"),"The other run's database was not dropped or recreated.");
			}
			finally {
				ScratchDb.Exec(null,"DROP DATABASE IF EXISTS `"+nameOther+"`");
			}
		}

		[TestMethod]
		public void StaleDrop_KeepsRecentRunsAndOtherNames_DropsOldRuns() {
			ScratchDb.RequireServer();
			string nameRecent=OtherRunName(DateTime.UtcNow.AddMinutes(-30),"recent");
			string nameStale=OtherRunName(DateTime.UtcNow.AddHours(-13),"stale");
			string nameOutside=ScratchDb.PREFIX+"keep_"+Guid.NewGuid().ToString("N").Substring(0,12);
			foreach(string name in new[] { nameRecent,nameStale,nameOutside }) {
				ScratchDb.Exec(null,"CREATE DATABASE `"+name+"`");
			}
			try {
				ScratchDb.DropStaleRunDatabases();
				Assert.IsFalse(DatabaseExists(nameStale),"A run that started 13 hours ago is over: its leftover database is dropped.");
				Assert.IsTrue(DatabaseExists(nameRecent),"A run that started 30 minutes ago may still be testing.");
				Assert.IsTrue(DatabaseExists(nameOutside),"A name outside the per-run scheme is never dropped.");
				ScratchDb.Drop(nameOutside);
				Assert.IsTrue(DatabaseExists(nameOutside),"Drop ignores a name outside the per-run scheme.");
			}
			finally {
				foreach(string name in new[] { nameRecent,nameStale,nameOutside }) {
					ScratchDb.Exec(null,"DROP DATABASE IF EXISTS `"+name+"`");
				}
			}
		}
	}
}
