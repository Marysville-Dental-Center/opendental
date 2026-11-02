// The office's own words (the dictcustom table, Setup | Spell Check and "Add to Dictionary") count as correct, ignoring case, next to the en_US
// dictionary. Runs on a scratch database of the local MariaDB (test_srcref_spell_..., dropped afterwards), in a fresh AppDomain so the business
// layer of the other tests never points at it. Made-up words only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibSpellTests {
	[TestClass]
	public class CustomWordsTests {
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason==null) {
				ScratchDb.DropStale();
			}
		}

		[TestMethod]
		public void CustomWords_AreCorrectIgnoringCase_AddedAtOnce_AndGoneWhenDeleted() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			string name=ScratchDb.NewName("words");
			ScratchDb.Create(name);
			try {
				using IsolatedDomain isolatedDomain=new IsolatedDomain();
				SpellCheckProbe probe=isolatedDomain.Probe;
				probe.Connect(name);
				try {
					//Words the dictionary does not have: misspelled until the office adds them.
					Assert.IsFalse(probe.IsCorrect("Zorbadent"));
					Assert.IsFalse(probe.IsCustomWord("Zorbadent"));
					Assert.IsFalse(probe.IsCorrect("Quellaprophy"));
					//Another workstation (or Setup | Spell Check) adds a word; this workstation gets the signal.
					probe.InsertCustomWordElsewhere("Quellaprophy");
					Assert.IsTrue(probe.IsCustomWord("Quellaprophy"));
					foreach(string word in new[] { "Quellaprophy","quellaprophy","QUELLAPROPHY" }) {
						Assert.IsTrue(probe.IsCorrect(word),word+": the office's words count in any case.");
					}
					//"Add to Dictionary" in a box: one row in dictcustom, the DictCustoms signal, and the word is correct at once.
					string[] arraySignals=probe.AddCustomWord("Zorbadent");
					CollectionAssert.AreEqual(new[] { "DictCustoms" },arraySignals);
					Assert.AreEqual(1,ScratchDb.CountCustomWord(name,"Zorbadent"));
					Assert.IsTrue(probe.IsCorrect("Zorbadent"));
					Assert.IsTrue(probe.IsCorrect("zorbadent"));
					//A note with both words and a misspelling: only the misspelling is underlined.
					CollectionAssert.AreEqual(new[] { "Nuirsing" },
						probe.GetMisspelledWords("Zorbadent varnish placed after Quellaprophy polish. Nuirsing staff informed."));
					//Dictionary words are unaffected, and their suggestions do not change.
					Assert.IsTrue(probe.IsCorrect("varnish"));
					CollectionAssert.Contains(probe.Suggest("recieve"),"receive");
					//Deleted in Setup | Spell Check: misspelled again, since the spell checker keeps no copy of the office's words.
					probe.DeleteCustomWordElsewhere("Quellaprophy");
					Assert.IsFalse(probe.IsCustomWord("Quellaprophy"));
					Assert.IsFalse(probe.IsCorrect("Quellaprophy"));
					Assert.AreEqual(1,probe.CountDictionaryReads());
				}
				finally {
					probe.Disconnect();
				}
			}
			finally {
				ScratchDb.Drop(name);
			}
			Assert.IsFalse(ScratchDb.Exists(name),"The scratch database is dropped.");
		}
	}
}
