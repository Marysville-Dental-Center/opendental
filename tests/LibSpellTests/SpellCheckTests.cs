// The spell checker of every ODtextBox and TextRich (WpfControls.SpellCheckL) on WeCantSpell.Hunspell 7.0.1 with the en_US dictionary embedded in
// WpfControlsOD: correct and misspelled words, case, suggestions, and a long note checked without holding up the window. No database: the
// office's own words are in CustomWordsTests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WeCantSpell.Hunspell;
using WpfControls;

namespace LibSpellTests {
	[TestClass]
	public class SpellCheckTests {
		///<summary>Words of a dental office's notes that the dictionary has, in the forms its affix rules make (plurals, past tenses, prefixes) and
		///with a capital letter at the start of a sentence or in all capitals, as typed.</summary>
		private static readonly string[] _arrayCorrect=new string[] {
			"tooth","teeth","patient","patients","appointment","appointments","scheduled","rescheduled","receive","received","dentist","dental",
			"filling","fillings","extraction","extracted","insurance","Tooth","Patient","TOOTH","PATIENT","Monday","January","isn't","doesn't",
		};

		///<summary>Misspellings as people type them.</summary>
		private static readonly string[] _arrayMisspelled=new string[] {
			"teh","recieve","apointment","patinet","pateint","denitst","fillling","extracton","insurnace","Nuirsing","Werdz","Fontz","toothh",
		};

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			//This domain has no database connection, so only the dictionary decides here.
			Assert.IsFalse(OpenPmsDentalBusiness.Db.HasDatabaseConnection(),"These tests run without a database.");
		}

		[TestMethod]
		public void EmbeddedDictionary_ReadsWithoutWarnings_AsLatin1() {
			//The aff file says SET ISO8859-1; the .NET Framework has that encoding, so the library needs no extra code page provider.
			using MemoryStream memoryStreamDic=new MemoryStream(WpfControls.Properties.Resources.en_US_dic);
			using MemoryStream memoryStreamAff=new MemoryStream(WpfControls.Properties.Resources.en_US_aff);
			WordList wordList=WordList.CreateFromStreams(memoryStreamDic,memoryStreamAff);
			Assert.AreEqual(0,wordList.Affix.Warnings.Count,string.Join("\r\n",wordList.Affix.Warnings));
			Assert.AreEqual(28591,wordList.Affix.Encoding.CodePage,"ISO-8859-1");
			//The 48,245 entries of en_US_dic (its first line is the count), as Open Dental shipped them.
			string firstLine=Encoding.ASCII.GetString(WpfControls.Properties.Resources.en_US_dic).Split('\n')[0].Trim();
			Assert.AreEqual("48245",firstLine);
			Assert.IsTrue(wordList.RootCount>=48000,"Roots read: "+wordList.RootCount);
			Assert.AreEqual("",SpellCheckL.DictionaryReadError);
		}

		[TestMethod]
		public void CorrectWords_AreInTheDictionary() {
			List<string> listWrong=_arrayCorrect.Where(x => !SpellCheckL.IsInDictionary(x) || !SpellCheckL.IsCorrect(x)).ToList();
			Assert.AreEqual(0,listWrong.Count,"Marked misspelled: "+string.Join(", ",listWrong));
			Assert.AreEqual("",SpellCheckL.DictionaryReadError);
		}

		[TestMethod]
		public void MisspelledWords_AreNotInTheDictionary() {
			List<string> listWrong=_arrayMisspelled.Where(x => SpellCheckL.IsInDictionary(x) || SpellCheckL.IsCorrect(x)).ToList();
			Assert.AreEqual(0,listWrong.Count,"Marked correct: "+string.Join(", ",listWrong));
		}

		[TestMethod]
		public void Case_TheDictionaryIsCaseSensitive_TheBoxesAlsoTryLowerCase() {
			//A proper noun only with its capital; a lower case word also capitalized or in capitals.
			Assert.IsTrue(SpellCheckL.IsInDictionary("Monday"));
			Assert.IsFalse(SpellCheckL.IsInDictionary("monday"));
			Assert.IsFalse(SpellCheckL.IsCorrect("monday"),"The lower case form of a proper noun is misspelled.");
			Assert.IsTrue(SpellCheckL.IsInDictionary("Tooth"));
			Assert.IsTrue(SpellCheckL.IsInDictionary("TOOTH"));
			//Odd capitals inside a word: Hunspell says no, the lower case retry says yes, as in both boxes before.
			Assert.IsFalse(SpellCheckL.IsInDictionary("tOOTH"));
			Assert.IsTrue(SpellCheckL.IsCorrect("tOOTH"));
		}

		[TestMethod]
		public void EmptyAndOddInput_NeverThrows() {
			Assert.IsTrue(SpellCheckL.IsInDictionary(""));
			Assert.IsTrue(SpellCheckL.IsInDictionary(null));
			Assert.AreEqual(0,SpellCheckL.Suggest("").Count);
			Assert.AreEqual(0,SpellCheckL.Suggest(null).Count);
			Assert.AreEqual(0,SpellCheckL.Suggest("   ").Count);
			Assert.AreEqual(0,SpellCheckL.GetMisspelledWords("").Count);
			Assert.AreEqual(0,SpellCheckL.GetMisspelledWords(null).Count);
			//A word longer than Hunspell looks at (100 letters) is misspelled, not an error.
			Assert.IsFalse(SpellCheckL.IsCorrect(new string('a',150)));
		}

		[TestMethod]
		public void Suggest_OffersTheIntendedWordAmongTheFiveShown() {
			//The boxes show the first five suggestions.
			string[][] arrayCases=new[] {
				new[] { "recieve","receive" },
				new[] { "teh","the" },
				new[] { "apointment","appointment" },
				new[] { "patinet","patient" },
				new[] { "insurnace","insurance" },
				new[] { "extracton","extraction" },
				new[] { "Nuirsing","Nursing" },//the capital is kept
			};
			List<string> listProblems=new List<string>();
			foreach(string[] arrayCase in arrayCases) {
				List<string> listSuggestions=SpellCheckL.Suggest(arrayCase[0]);
				if(!listSuggestions.Take(5).Contains(arrayCase[1])) {
					listProblems.Add(arrayCase[0]+": "+string.Join(", ",listSuggestions));
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//The intended word comes first for the plain cases.
			Assert.AreEqual("receive",SpellCheckL.Suggest("recieve")[0]);
			Assert.AreEqual("appointment",SpellCheckL.Suggest("apointment")[0]);
		}

		[TestMethod]
		public void Suggest_GivesUpQuickly_OnALongNonsenseWord() {
			//A right click on such a word runs Suggest on the UI thread. WeCantSpell.Hunspell stops after 250 ms (TimeLimitSuggestGlobal) plus the
			//step in progress; 3 seconds leaves room for a busy test machine.
			string word="qzxv"+new string('k',40)+"wptrq"+new string('j',40);
			SpellCheckL.IsInDictionary("tooth");//the dictionary is read, so only Suggest is timed
			Stopwatch stopwatch=Stopwatch.StartNew();
			List<string> listSuggestions=SpellCheckL.Suggest(word);
			stopwatch.Stop();
			Assert.IsTrue(stopwatch.ElapsedMilliseconds<3000,"Suggest took "+stopwatch.ElapsedMilliseconds+" ms.");
			Assert.IsNotNull(listSuggestions);
		}

		[TestMethod]
		public void LongNote_IsCheckedWithoutHoldingUpTheWindow() {
			//A long clinical note of about 30,000 words: correct words with a misspelling in every sentence, as TextRich checks it on each change.
			string[] arraySentence=new[] { "The","patient","was","seen","for","a","recall","appointment","and","the","dentist","recommended","a",
				"filling","on","the","lower","molar","because","of","decay","noted","on","the","image" };
			StringBuilder stringBuilder=new StringBuilder();
			int countMisspelled=0;
			for(int i=0;i<1200;i++) {
				stringBuilder.Append(string.Join(" ",arraySentence));
				stringBuilder.Append(" "+_arrayMisspelled[i%_arrayMisspelled.Length]+". ");
				countMisspelled++;
				if(i%10==9) {
					stringBuilder.Append("\r\n");
				}
			}
			string note=stringBuilder.ToString();
			Assert.IsTrue(Regex.Matches(note,@"\S+").Count>30000);
			SpellCheckL.IsInDictionary("tooth");//the dictionary read is timed in DictionaryReadTests, not here
			Stopwatch stopwatch=Stopwatch.StartNew();
			List<Match> listMisspelled=SpellCheckL.GetMisspelledWords(note);
			long msFirst=stopwatch.ElapsedMilliseconds;
			//"teh" has only three letters and "Nuirsing" etc. are all at least three, so every misspelling is a word TextRich checks.
			Assert.AreEqual(countMisspelled,listMisspelled.Count);
			Assert.IsTrue(listMisspelled.All(x => _arrayMisspelled.Contains(x.Value)));
			Assert.IsTrue(listMisspelled.All(x => note.Substring(x.Index,x.Length)==x.Value),"The positions point at the words, for the underline.");
			//Typing one more letter checks the whole note again.
			stopwatch.Restart();
			List<Match> listMisspelledAgain=SpellCheckL.GetMisspelledWords(note+"s");
			long msAgain=stopwatch.ElapsedMilliseconds;
			Assert.AreEqual(countMisspelled,listMisspelledAgain.Count);
			//Generous limits for a busy test machine; the checks themselves take a few tens of milliseconds.
			Assert.IsTrue(msFirst<2000,"The first check took "+msFirst+" ms.");
			Assert.IsTrue(msAgain<1000,"The check after a change took "+msAgain+" ms.");
		}

		[TestMethod]
		public void LongNote_ManyDistinctWords_IsCheckedWithoutHoldingUpTheWindow() {
			//The worst case for the word cache: thousands of distinct words, none looked up before.
			SpellCheckL.IsInDictionary("tooth");
			StringBuilder stringBuilder=new StringBuilder();
			Random random=new Random(20261102);
			for(int i=0;i<8000;i++) {
				int length=random.Next(4,12);
				for(int j=0;j<length;j++) {
					stringBuilder.Append((char)('a'+random.Next(0,26)));
				}
				stringBuilder.Append(' ');
			}
			string note=stringBuilder.ToString();
			Stopwatch stopwatch=Stopwatch.StartNew();
			List<Match> listMisspelled=SpellCheckL.GetMisspelledWords(note);
			stopwatch.Stop();
			Assert.IsTrue(listMisspelled.Count>7000,"Random letters are mostly misspelled: "+listMisspelled.Count);
			Assert.IsTrue(stopwatch.ElapsedMilliseconds<3000,"8,000 new words took "+stopwatch.ElapsedMilliseconds+" ms.");
		}

		[TestMethod]
		public void GetMisspelledWords_ChecksTheWordsTextRichUnderlines() {
			//Words of three or more letters, apostrophes and hyphens; a word with a digit and words of one or two letters are never checked.
			List<string> listWords=SpellCheckL.GetMisspelledWords("Teh pt hs a #3 filling on 3rd molar, recieve x2 abc123 don't it's teh-end.")
				.Select(x => x.Value).ToList();
			CollectionAssert.AreEqual(new[] { "Teh","recieve","teh-end" },listWords,string.Join(", ",listWords));
		}
	}
}
