// The Group Note window's size, remembered per user on the workstation (25.4 larger, resizable window; remembering is a fork addition). Uses a scratch
// file in the temp folder, never the real one.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2NOTESTests {
	[TestClass]
	public class LocalWindowSizesTests {
		private string _path;

		[TestInitialize]
		public void TestInitialize() {
			_path=Path.Combine(Path.GetTempPath(),"m2notes_"+Guid.NewGuid().ToString("N"),"WindowSizes.txt");
			LocalWindowSizes.PathOverride=_path;
		}

		[TestCleanup]
		public void TestCleanup() {
			LocalWindowSizes.PathOverride=null;
			try {
				Directory.Delete(Path.GetDirectoryName(_path),true);
			}
			catch(Exception) {
			}
		}

		[TestMethod]
		public void DefaultFile_UnderLocalAppData() {
			LocalWindowSizes.PathOverride=null;
			string path=LocalWindowSizes.GetPath();
			StringAssert.StartsWith(path,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
			StringAssert.EndsWith(path,Path.Combine("OpenPmsDental","WindowSizes.txt"));
		}

		[TestMethod]
		public void Remembered_PerWindowAndUser() {
			Assert.IsNull(LocalWindowSizes.Get("FormProcGroup",1),"Nothing remembered yet; the folder does not exist.");
			Assert.IsTrue(LocalWindowSizes.Set("FormProcGroup",1,1200,800));
			Assert.IsTrue(LocalWindowSizes.Set("FormProcGroup",2,1000,700));
			Assert.IsTrue(LocalWindowSizes.Set("FormOther",1,500,400));
			Assert.AreEqual(Tuple.Create(1200,800),LocalWindowSizes.Get("FormProcGroup",1));
			Assert.AreEqual(Tuple.Create(1000,700),LocalWindowSizes.Get("FormProcGroup",2));
			Assert.AreEqual(Tuple.Create(500,400),LocalWindowSizes.Get("FormOther",1));
			Assert.IsTrue(LocalWindowSizes.Set("FormProcGroup",1,1100,750));
			Assert.AreEqual(Tuple.Create(1100,750),LocalWindowSizes.Get("FormProcGroup",1),"Replaced, not added.");
			Assert.AreEqual(3,File.ReadAllLines(_path).Length);
		}

		[TestMethod]
		public void BadSizesAndBadLines_Ignored() {
			Assert.IsFalse(LocalWindowSizes.Set("FormProcGroup",1,0,800));
			Assert.IsFalse(LocalWindowSizes.Set("",1,100,100));
			Directory.CreateDirectory(Path.GetDirectoryName(_path));
			File.WriteAllText(_path,"garbage\r\nFormProcGroup\t1\tx\t2\r\nFormProcGroup\t3\t-5\t100\r\nFormProcGroup\t4\t900\t600\r\n");
			Assert.IsNull(LocalWindowSizes.Get("FormProcGroup",1));
			Assert.IsNull(LocalWindowSizes.Get("FormProcGroup",3));
			Assert.AreEqual(Tuple.Create(900,600),LocalWindowSizes.Get("FormProcGroup",4));
		}

		[TestMethod]
		public void SizeToOpen_WithinMinimumAndScreen() {
			Assert.IsNull(LocalWindowSizes.GetSizeToOpen(null,990,560,1900,1000));
			Assert.AreEqual(Tuple.Create(1200,800),LocalWindowSizes.GetSizeToOpen(Tuple.Create(1200,800),990,560,1900,1000));
			Assert.AreEqual(Tuple.Create(990,560),LocalWindowSizes.GetSizeToOpen(Tuple.Create(300,200),990,560,1900,1000),"Never below the minimum.");
			Assert.AreEqual(Tuple.Create(1900,1000),LocalWindowSizes.GetSizeToOpen(Tuple.Create(2500,1600),990,560,1900,1000),"Never larger than the screen.");
			Assert.AreEqual(Tuple.Create(990,560),LocalWindowSizes.GetSizeToOpen(Tuple.Create(2500,1600),990,560,800,500),"A screen smaller than the minimum.");
		}
	}
}
