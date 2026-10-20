// The per-user active filter group, kept in a local file (backlog package A-QM).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace QueryMonitorTests {
	[TestClass]
	public class ActiveGroupTests {
		private string _dir;

		[TestInitialize]
		public void Init() {
			_dir=Path.Combine(Path.GetTempPath(),"QueryMonitorTests_"+Guid.NewGuid().ToString("N"));
			QueryFilterActiveGroups.FilePathOverride=Path.Combine(_dir,"sub","QueryMonitorFilterGroups.txt");
		}

		[TestCleanup]
		public void Cleanup() {
			QueryFilterActiveGroups.FilePathOverride=null;
			try {
				Directory.Delete(_dir,true);
			}
			catch(Exception) {
			}
		}

		[TestMethod]
		public void NoFile_MeansNoActiveGroup() {
			Assert.AreEqual("",QueryFilterActiveGroups.GetActiveGroup(QueryFilterActiveGroups.GetKey("localhost","opendental",1)));
		}

		[TestMethod]
		public void SetThenGet_IsPerServerDatabaseAndUser() {
			string keyUser1=QueryFilterActiveGroups.GetKey("localhost","opendental",1);
			string keyUser2=QueryFilterActiveGroups.GetKey("localhost","opendental",2);
			string keyOtherDb=QueryFilterActiveGroups.GetKey("localhost","training",1);
			Assert.IsTrue(QueryFilterActiveGroups.SetActiveGroup(keyUser1,"Signals"));
			Assert.IsTrue(QueryFilterActiveGroups.SetActiveGroup(keyUser2,"Billing"));
			Assert.AreEqual("Signals",QueryFilterActiveGroups.GetActiveGroup(keyUser1));
			Assert.AreEqual("Billing",QueryFilterActiveGroups.GetActiveGroup(keyUser2));
			Assert.AreEqual("",QueryFilterActiveGroups.GetActiveGroup(keyOtherDb));
			Assert.AreEqual("Signals",QueryFilterActiveGroups.GetActiveGroup(QueryFilterActiveGroups.GetKey("LOCALHOST","OpenDental",1)),
				"Keys ignore case.");
		}

		[TestMethod]
		public void BlankGroup_ClearsTheSetting() {
			string key=QueryFilterActiveGroups.GetKey("localhost","opendental",1);
			QueryFilterActiveGroups.SetActiveGroup(key,"Signals");
			Assert.IsTrue(QueryFilterActiveGroups.SetActiveGroup(key,"  "));
			Assert.AreEqual("",QueryFilterActiveGroups.GetActiveGroup(key));
		}

		[TestMethod]
		public void UnreadableLines_AreIgnored() {
			string path=QueryFilterActiveGroups.GetFilePath();
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path,"garbage line\r\n\tno key\r\nlocalhost|opendental|1\tSignals\r\n");
			Assert.AreEqual("Signals",QueryFilterActiveGroups.GetActiveGroup("localhost|opendental|1"));
		}
	}
}
