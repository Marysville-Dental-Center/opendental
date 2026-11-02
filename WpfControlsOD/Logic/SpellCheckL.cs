using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using WeCantSpell.Hunspell;

namespace WpfControls {
	///<summary>The spell checker of every ODtextBox and TextRich. Words are checked against the en_US Hunspell dictionary embedded in this assembly
	///(Resources\en_US_aff and Resources\en_US_dic) with WeCantSpell.Hunspell, a managed port of Hunspell that needs no native DLL, and against the
	///office's own words (the dictcustom table, DictCustoms). The dictionary is read once per process and shared by every box on every thread. The
	///boxes start the read on a background thread when they are created, so opening a window never waits for it.</summary>
	public class SpellCheckL {
		///<summary>The words TextRich checks: at least three letters, apostrophes or hyphens between word boundaries, so a word with a digit is never
		///checked.</summary>
		private static readonly Regex _regexWord=new Regex(@"\b[A-Za-z'-]{3,}\b");
		///<summary>The dictionary, read on first use by whichever thread gets there first; every other thread waits for that one read.</summary>
		private static readonly Lazy<WordList> _lazyWordList=new Lazy<WordList>(ReadDictionary,LazyThreadSafetyMode.ExecutionAndPublication);
		///<summary>What the dictionary said about each word as typed, so a long note looks up each distinct word once. The dictionary never changes
		///while the program runs, so the answers stay right; the office's own words are not kept here because they can be edited.</summary>
		private static readonly ConcurrentDictionary<string,bool> _dictInDictionary=new ConcurrentDictionary<string,bool>(StringComparer.Ordinal);
		///<summary>_dictInDictionary starts over when it holds this many words, so it cannot grow without end.</summary>
		private const int MAX_CACHED_WORDS=100000;
		///<summary>1 once ReadInBackground started the read.</summary>
		private static int _isReadStarted;
		private static int _countReads;
		private static string _readError="";

		///<summary>How many times this process read the dictionary: 0 before the first use, 1 after it, never more.</summary>
		public static int CountDictionaryReads {
			get {
				return _countReads;
			}
		}

		///<summary>True once the dictionary was read (or could not be read, see DictionaryReadError).</summary>
		public static bool IsDictionaryRead {
			get {
				return _lazyWordList.IsValueCreated;
			}
		}

		///<summary>Why the embedded dictionary could not be read, or "". Without it every word counts as correct, so nothing is underlined.</summary>
		public static string DictionaryReadError {
			get {
				return _readError;
			}
		}

		///<summary>Starts reading the dictionary on a thread pool thread and returns at once; only the first call in the process does anything. The
		///boxes call this when they are created, so the dictionary is ready by the time they first check spelling.</summary>
		public static void ReadInBackground() {
			if(Interlocked.Exchange(ref _isReadStarted,1)==1) {
				return;
			}
			System.Threading.Tasks.Task.Run(() => _lazyWordList.Value);
		}

		///<summary>Reads the embedded en_US dictionary. Runs once per process, through _lazyWordList. Returns null if it cannot be read.</summary>
		private static WordList ReadDictionary() {
			Interlocked.Increment(ref _countReads);
			try {
				using MemoryStream memoryStreamDic=new MemoryStream(Properties.Resources.en_US_dic);
				using MemoryStream memoryStreamAff=new MemoryStream(Properties.Resources.en_US_aff);
				return WordList.CreateFromStreams(memoryStreamDic,memoryStreamAff);
			}
			catch(Exception ex) {
				_readError=ex.Message;
				return null;
			}
		}

		///<summary>True when the dictionary has the word as typed. Hunspell is case sensitive: "Google" is correct and "google" is not, while a
		///capitalized or all upper case form of a lower case word ("Tooth", "TOOTH") is correct. Waits for the dictionary on first use. Also true
		///when the dictionary could not be read, so nothing is underlined then.</summary>
		public static bool IsInDictionary(string word) {
			if(string.IsNullOrEmpty(word)) {
				return true;
			}
			WordList wordList=_lazyWordList.Value;
			if(wordList==null) {
				return true;
			}
			if(_dictInDictionary.TryGetValue(word,out bool isInDictionary)) {
				return isInDictionary;
			}
			isInDictionary=wordList.Check(word);
			if(_dictInDictionary.Count>=MAX_CACHED_WORDS) {
				_dictInDictionary.Clear();
			}
			_dictInDictionary[word]=isInDictionary;
			return isInDictionary;
		}

		///<summary>True when the office added the word to its own words (the dictcustom table, Setup | Spell Check or "Add to Dictionary"),
		///ignoring case. Uses the DictCustoms cache; false when there is no database.</summary>
		public static bool IsCustomWord(string word) {
			if(string.IsNullOrEmpty(word) || !Db.HasDatabaseConnection()) {
				return false;
			}
			string wordLower=word.ToLower();
			return DictCustoms.GetFirstOrDefault(x => x.WordText.ToLower()==wordLower)!=null;
		}

		///<summary>True when the word is spelled correctly: in the dictionary as typed or in lower case, or one of the office's own words.</summary>
		public static bool IsCorrect(string word) {
			return IsInDictionary(word) || IsInDictionary(word.ToLower()) || IsCustomWord(word);
		}

		///<summary>The dictionary's suggestions for a misspelled word, best first (at most 15). WeCantSpell.Hunspell stops looking after a quarter
		///of a second (QueryOptions.TimeLimitSuggestGlobal), so a right click on a long word never holds up the window. Empty when there are none or
		///the dictionary could not be read.</summary>
		public static List<string> Suggest(string word) {
			if(string.IsNullOrWhiteSpace(word)) {
				return new List<string>();
			}
			WordList wordList=_lazyWordList.Value;
			if(wordList==null) {
				return new List<string>();
			}
			return wordList.Suggest(word).ToList();
		}

		///<summary>The misspelled words of the text, in order, as TextRich underlines them. Each distinct word is checked once per call (and looked
		///up in the dictionary once per process), so a long note that repeats its words is quick to check again after every change. Pass the same
		///dictCorrect to the calls of one pass over several pieces of text (TextRich checks each run of text on its own) to share those answers.
		///</summary>
		public static List<Match> GetMisspelledWords(string text,Dictionary<string,bool> dictCorrect=null) {
			List<Match> listMatches=new List<Match>();
			if(string.IsNullOrEmpty(text)) {
				return listMatches;
			}
			if(dictCorrect==null) {
				dictCorrect=new Dictionary<string,bool>(StringComparer.Ordinal);
			}
			MatchCollection matchCollection=_regexWord.Matches(text);
			for(int i=0;i<matchCollection.Count;i++) {
				string word=matchCollection[i].Value;
				if(!dictCorrect.TryGetValue(word,out bool isCorrect)) {
					isCorrect=IsCorrect(word);
					dictCorrect[word]=isCorrect;
				}
				if(!isCorrect) {
					listMatches.Add(matchCollection[i]);
				}
			}
			return listMatches;
		}

		///<summary>Adds the word to the office's own words (the dictcustom table) and signals DictCustoms, which refreshes this workstation's cache
		///at once and the other workstations' caches with their next signal.</summary>
		public static void AddCustomWord(string word) {
			if(string.IsNullOrWhiteSpace(word)) {
				return;
			}
			DictCustom dictCustom=new DictCustom();
			dictCustom.WordText=word;
			DictCustoms.Insert(dictCustom);
			DataValid.SetInvalid(InvalidType.DictCustoms);
		}
	}
}
