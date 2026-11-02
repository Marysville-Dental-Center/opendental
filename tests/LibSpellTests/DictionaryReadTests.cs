// The dictionary is read once per process, off the UI thread: each test runs the spell checker in a fresh AppDomain (IsolatedDomain), as a
// program that just started, and counts the reads.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibSpellTests {
	[TestClass]
	public class DictionaryReadTests {
		private static readonly string[] _arrayWords=new string[] { "tooth","teeth","patient","appointment","Monday","recieve","teh","apointment",
			"insurance","Nuirsing","dentist","denitst","filling","fillling" };
		private static readonly bool[] _arrayExpected=new bool[] { true,true,true,true,true,false,false,false,true,false,true,false,true,false };

		[TestMethod]
		public void Dictionary_IsReadOnce_WhenManyThreadsNeedItAtOnce() {
			using IsolatedDomain isolatedDomain=new IsolatedDomain();
			SpellCheckProbe probe=isolatedDomain.Probe;
			Assert.AreEqual(0,probe.CountDictionaryReads(),"Nothing reads the dictionary before it is needed.");
			Assert.IsFalse(probe.IsDictionaryRead());
			//16 threads start at once with no dictionary: one reads it, the others wait for that read.
			Assert.AreEqual("",probe.CheckFromThreads(16,_arrayWords,_arrayExpected));
			Assert.AreEqual(1,probe.CountDictionaryReads());
			Assert.IsTrue(probe.IsDictionaryRead());
			Assert.AreEqual("",probe.DictionaryReadError());
			//Later lookups, suggestions and checks of a note use the same dictionary.
			Assert.AreEqual("",probe.CheckFromThreads(4,_arrayWords,_arrayExpected));
			CollectionAssert.Contains(probe.Suggest("recieve"),"receive");
			CollectionAssert.AreEqual(new[] { "teh" },probe.GetMisspelledWords("The patient missed teh appointment."));
			Assert.AreEqual(1,probe.CountDictionaryReads());
		}

		[TestMethod]
		public void ReadInBackground_ReturnsAtOnce_AndReadsOnce() {
			using IsolatedDomain isolatedDomain=new IsolatedDomain();
			SpellCheckProbe probe=isolatedDomain.Probe;
			Assert.AreEqual(0,probe.CountDictionaryReads());
			//What 50 boxes do when a window with 50 of them opens: the calls start the read on the thread pool and return.
			double msCalls=probe.ReadInBackground(50);
			Assert.IsTrue(msCalls<250,"Creating the boxes waited "+msCalls+" ms for the spell checker.");
			//The first lookup waits for that read instead of starting another one.
			Assert.IsTrue(probe.IsInDictionary("tooth"));
			Assert.IsFalse(probe.IsInDictionary("toothh"));
			Assert.AreEqual(1,probe.CountDictionaryReads());
			probe.ReadInBackground(5);
			Assert.AreEqual(1,probe.CountDictionaryReads(),"Later calls read nothing.");
		}

		[TestMethod]
		public void Boxes_ODtextBoxAndTextRich_ShareTheOneDictionary() {
			using IsolatedDomain isolatedDomain=new IsolatedDomain();
			SpellCheckProbe probe=isolatedDomain.Probe;
			Assert.AreEqual(0,probe.CountDictionaryReads());
			//Three of each kind of box, built as a window builds them (never shown), on an STA thread.
			Assert.AreEqual("",probe.CreateBoxes(3));
			Assert.IsTrue(probe.IsInDictionary("appointment"));
			Assert.AreEqual(1,probe.CountDictionaryReads(),"Every box uses the one dictionary of the process.");
			Assert.AreEqual("",probe.CreateBoxes(2));
			Assert.AreEqual(1,probe.CountDictionaryReads());
			CollectionAssert.AreEqual(new[] { "apointment" },probe.GetMisspelledWords("Moved the apointment to Monday."));
		}

		[TestMethod]
		public void EachProcess_ReadsItsOwnDictionary() {
			//Two fresh domains stand for two programs: each reads the dictionary once.
			using IsolatedDomain isolatedDomain1=new IsolatedDomain();
			using IsolatedDomain isolatedDomain2=new IsolatedDomain();
			Assert.IsTrue(isolatedDomain1.Probe.IsInDictionary("tooth"));
			Assert.AreEqual(1,isolatedDomain1.Probe.CountDictionaryReads());
			Assert.AreEqual(0,isolatedDomain2.Probe.CountDictionaryReads());
			Assert.IsTrue(isolatedDomain2.Probe.IsInDictionary("tooth"));
			Assert.AreEqual(1,isolatedDomain2.Probe.CountDictionaryReads());
		}
	}
}
