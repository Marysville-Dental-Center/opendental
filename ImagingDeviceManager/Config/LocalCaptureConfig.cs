// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace ImagingDeviceManager.Config {
	///<summary>Reads and writes capture.ini, the file that holds this computer's capture settings: hardware bindings and the
	///administrator-set [Twain] and [Device.N] keys. It has no database or preference access and no dependencies.
	///
	///The file is %ProgramData%\OpenPMS-Dental\Imaging\capture.ini. Reads use that file when it exists, otherwise the per-user
	///%LOCALAPPDATA%\OpenPMS-Dental\Imaging\capture.ini. Saves go to the ProgramData file, and to the per-user file only when the
	///ProgramData folder or file cannot be created or written. When this class creates the ProgramData Imaging folder, it gives
	///the local Users group Modify rights on it (inherited by its files), because Open Dental runs as an ordinary user.
	///
	///Format: [Section] headers and Key=Value lines. ';' starts a comment, on a line of its own or after a header or a value.
	///Blank lines and lines that are neither are ignored. Section and key names are case-insensitive, whitespace around names and
	///values is ignored, and when a key appears more than once in a section (or a section appears more than once), the last one
	///wins. Numbers are culture-invariant. A file that is missing, unreadable or corrupt reads as empty, so every getter returns its
	///default. No member throws: problems are reported in LastError. Save keeps every line and comment and changes only the values
	///that were Set, adding missing keys and sections.</summary>
	public sealed class LocalCaptureConfig {
		#region Constants
		public const string FileName="capture.ini";
		///<summary>The folder under %ProgramData% (and, for the fallback, under %LOCALAPPDATA%) that holds capture.ini.</summary>
		public const string FolderRelative=@"OpenPMS-Dental\Imaging";
		public const string SectionTwain="Twain";
		///<summary>Per-device sections are [Device.N], where N is imagingdevice.ImagingDeviceNum.</summary>
		public const string SectionDevicePrefix="Device.";
		///<summary>capture.ini is a few kilobytes. A larger file is not a capture.ini file and reads as corrupt.</summary>
		private const int MaxFileBytes=1024*1024;
		#endregion Constants

		#region Fields
		private readonly object _lock=new object();
		///<summary>Gives the local Users group Modify on a folder this class created. Returns "" on success, else the error.
		///Null for explicit paths, so tests never change ACLs.</summary>
		private readonly Func<string,string> _funcGrantUsersModify;
		private IniText _iniText=new IniText();
		///<summary>Section name to (key name to value), both case-insensitive. Rebuilt from _iniText after every change.</summary>
		private Dictionary<string,Dictionary<string,string>> _dictSections=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
		///<summary>Section names in the order they first appear.</summary>
		private List<string> _listSectionNames=new List<string>();
		///<summary>Values Set since the last successful Save, in order. Save applies them to the file as it is on disk then.</summary>
		private readonly List<string[]> _listPending=new List<string[]>();
		private string _lastError="";
		#endregion Fields

		#region Properties
		///<summary>The file reads prefer and saves try first. Null when none was given.</summary>
		public string PathPrimary { get; }
		///<summary>The file read when PathPrimary does not exist, and written when PathPrimary cannot be. Null when none.</summary>
		public string PathFallback { get; }
		///<summary>The file chosen for reading: PathPrimary when it exists, otherwise PathFallback when it exists, otherwise null. If
		///that file could not be read or was corrupt, the settings are empty and LastError says why.</summary>
		public string PathLoaded { get; private set; }
		///<summary>The file the last successful Save wrote. Null before that.</summary>
		public string PathSaved { get; private set; }

		///<summary>"" when the last load, Set or Save had no problem; otherwise a sentence for the user or the log. A Save that had to
		///use PathFallback succeeds and leaves a warning here.</summary>
		public string LastError {
			get {
				lock(_lock) {
					return _lastError;
				}
			}
		}

		///<summary>True when a value was Set and not yet saved.</summary>
		public bool HasUnsavedChanges {
			get {
				lock(_lock) {
					return _listPending.Count>0;
				}
			}
		}

		///<summary>%ProgramData%\OpenPMS-Dental\Imaging\capture.ini. Null if the folder cannot be determined.</summary>
		public static string DefaultPath {
			get {
				return CombineSpecialFolder(Environment.SpecialFolder.CommonApplicationData);
			}
		}

		///<summary>%LOCALAPPDATA%\OpenPMS-Dental\Imaging\capture.ini, used when the ProgramData file cannot be written. Null if the
		///folder cannot be determined.</summary>
		public static string DefaultFallbackPath {
			get {
				return CombineSpecialFolder(Environment.SpecialFolder.LocalApplicationData);
			}
		}
		#endregion Properties

		#region [Twain] settings
		//Administrator-set keys read by the TWAIN library (design section 9.3). The key name is the property name.

		///<summary>[Twain] ThreadModal, default 1.</summary>
		public bool ThreadModal {
			get {
				return GetBool(SectionTwain,"ThreadModal",true);
			}
		}

		///<summary>[Twain] AcquireTimeoutMinutes, default 0 (no timeout). Negative values read as 0.</summary>
		public int AcquireTimeoutMinutes {
			get {
				return Math.Max(0,GetInt(SectionTwain,"AcquireTimeoutMinutes",0));
			}
		}

		///<summary>[Twain] Allow16Bit, default 0.</summary>
		public bool Allow16Bit {
			get {
				return GetBool(SectionTwain,"Allow16Bit",false);
			}
		}

		///<summary>[Twain] RestrictSources, default 0.</summary>
		public bool RestrictSources {
			get {
				return GetBool(SectionTwain,"RestrictSources",false);
			}
		}

		///<summary>[Twain] AllowedSources: TWAIN ProductNames separated by |. Default empty.</summary>
		public List<string> AllowedSources {
			get {
				return GetList(SectionTwain,"AllowedSources");
			}
		}

		///<summary>[Twain] JournalKeep, default 200. Negative values read as the default.</summary>
		public int JournalKeep {
			get {
				int journalKeep=GetInt(SectionTwain,"JournalKeep",200);
				return journalKeep<0 ? 200 : journalKeep;
			}
		}

		///<summary>[Twain] DisableIsSafeSources: TWAIN ProductNames separated by |. Default empty.</summary>
		public List<string> DisableIsSafeSources {
			get {
				return GetList(SectionTwain,"DisableIsSafeSources");
			}
		}

		///<summary>[Twain] SafeDisableCap: a capability number in hex ("0x8002" or "8002"). Null when empty or invalid.</summary>
		public int? SafeDisableCap {
			get {
				return GetHex(SectionTwain,"SafeDisableCap");
			}
		}

		///<summary>[Twain] RefusedDisableCodes: condition codes in hex separated by |. Default empty. Invalid entries are skipped.</summary>
		public List<int> RefusedDisableCodes {
			get {
				return GetHexList(SectionTwain,"RefusedDisableCodes");
			}
		}
		#endregion [Twain] settings

		#region Load
		private LocalCaptureConfig(string path,string pathFallback,Func<string,string> funcGrantUsersModify) {
			PathPrimary=string.IsNullOrWhiteSpace(path) ? null : path;
			PathFallback=string.IsNullOrWhiteSpace(pathFallback) ? null : pathFallback;
			_funcGrantUsersModify=funcGrantUsersModify;
			try {
				foreach(string pathCandidate in new[] { PathPrimary,PathFallback }) {
					if(pathCandidate==null) {
						continue;
					}
					IniText iniText;
					string error;
					ReadStatus readStatus=ReadFile(pathCandidate,out iniText,out error);
					if(readStatus==ReadStatus.Missing) {
						continue;
					}
					PathLoaded=pathCandidate;
					if(readStatus==ReadStatus.Ok) {
						_iniText=iniText;
					}
					else {
						_lastError=error+" Capture uses default settings.";
					}
					break;
				}
			}
			catch(Exception ex) {
				_iniText=new IniText();
				_lastError="Could not load capture settings: "+ex.Message;
			}
			Reparse();
		}

		///<summary>Loads the default capture.ini (ProgramData, else the per-user fallback). Never throws.</summary>
		public static LocalCaptureConfig Load() {
			return new LocalCaptureConfig(DefaultPath,DefaultFallbackPath,GrantUsersModify);
		}

		///<summary>Loads the file at an explicit path, with no fallback. Never throws. A missing file gives an empty configuration
		///that Save creates.</summary>
		public static LocalCaptureConfig Load(string path) {
			return new LocalCaptureConfig(path,null,null);
		}

		///<summary>Loads with explicit primary and fallback paths, which behave like the ProgramData and per-user files, except that
		///no ACL is changed. Never throws.</summary>
		public static LocalCaptureConfig Load(string path,string pathFallback) {
			return new LocalCaptureConfig(path,pathFallback,null);
		}

		///<summary>For tests: explicit paths and a stand-in for the step that grants Users Modify on a created primary folder.</summary>
		internal static LocalCaptureConfig Load(string path,string pathFallback,Func<string,string> funcGrantUsersModify) {
			return new LocalCaptureConfig(path,pathFallback,funcGrantUsersModify);
		}
		#endregion Load

		#region Getters
		///<summary>The value, or defaultValue when the section or key is missing. A key that is present with nothing after '='
		///returns "".</summary>
		public string GetString(string section,string key,string defaultValue="") {
			string value=GetRaw(section,key);
			return value ?? defaultValue;
		}

		///<summary>True when the key is present in the section, even with an empty value.</summary>
		public bool ContainsKey(string section,string key) {
			return GetRaw(section,key)!=null;
		}

		///<summary>A decimal integer ("-5", "200"), or hex with a 0x prefix ("0x10"). defaultValue when missing, empty or invalid.</summary>
		public int GetInt(string section,string key,int defaultValue) {
			string value=GetRaw(section,key);
			if(value==null) {
				return defaultValue;
			}
			value=value.Trim();
			int result;
			if(value.StartsWith("0x",StringComparison.OrdinalIgnoreCase)) {
				return TryParseHex(value,out result) ? result : defaultValue;
			}
			if(int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out result)) {
				return result;
			}
			return defaultValue;
		}

		///<summary>1, true or yes is true; 0, false or no is false (any case). defaultValue when missing, empty or anything else.</summary>
		public bool GetBool(string section,string key,bool defaultValue) {
			string value=GetRaw(section,key);
			if(value==null) {
				return defaultValue;
			}
			value=value.Trim();
			if(value=="1" || value.Equals("true",StringComparison.OrdinalIgnoreCase) || value.Equals("yes",StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			if(value=="0" || value.Equals("false",StringComparison.OrdinalIgnoreCase) || value.Equals("no",StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			return defaultValue;
		}

		///<summary>For keys documented as hex: "0x8002" and "8002" both read as 0x8002. Null when missing, empty or invalid (including
		///values above 0x7FFFFFFF).</summary>
		public int? GetHex(string section,string key) {
			int result;
			if(TryParseHex(GetRaw(section,key),out result)) {
				return result;
			}
			return null;
		}

		///<summary>For keys documented as hex. defaultValue when missing, empty or invalid.</summary>
		public int GetHex(string section,string key,int defaultValue) {
			return GetHex(section,key) ?? defaultValue;
		}

		///<summary>Entries separated by '|', each trimmed, empty entries dropped. Empty list when missing. Never null.</summary>
		public List<string> GetList(string section,string key) {
			List<string> listEntries=new List<string>();
			string value=GetRaw(section,key);
			if(value==null) {
				return listEntries;
			}
			foreach(string entry in value.Split('|')) {
				string entryTrimmed=entry.Trim();
				if(entryTrimmed.Length>0) {
					listEntries.Add(entryTrimmed);
				}
			}
			return listEntries;
		}

		///<summary>Hex numbers separated by '|' (each "0x8001" or "8001"). Empty and invalid entries are skipped. Never null.</summary>
		public List<int> GetHexList(string section,string key) {
			List<int> listNumbers=new List<int>();
			foreach(string entry in GetList(section,key)) {
				int number;
				if(TryParseHex(entry,out number)) {
					listNumbers.Add(number);
				}
			}
			return listNumbers;
		}

		///<summary>The ImagingDeviceNum of every [Device.N] section, ascending, without duplicates. N must be a positive whole number
		///written without leading zeros, so that GetDeviceSection(N) names the same section.</summary>
		public List<long> GetDeviceNums() {
			List<long> listDeviceNums=new List<long>();
			lock(_lock) {
				foreach(string sectionName in _listSectionNames) {
					if(!sectionName.StartsWith(SectionDevicePrefix,StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					string digits=sectionName.Substring(SectionDevicePrefix.Length);
					long deviceNum;
					if(!long.TryParse(digits,NumberStyles.None,CultureInfo.InvariantCulture,out deviceNum) || deviceNum<=0
						|| deviceNum.ToString(CultureInfo.InvariantCulture)!=digits)
					{
						continue;
					}
					if(!listDeviceNums.Contains(deviceNum)) {
						listDeviceNums.Add(deviceNum);
					}
				}
			}
			listDeviceNums.Sort();
			return listDeviceNums;
		}

		///<summary>"Device.12" for ImagingDeviceNum 12.</summary>
		public static string GetDeviceSection(long imagingDeviceNum) {
			return SectionDevicePrefix+imagingDeviceNum.ToString(CultureInfo.InvariantCulture);
		}

		///<summary>Every section name in the order it first appears. Keys before the first header are in section "".</summary>
		public List<string> GetSectionNames() {
			lock(_lock) {
				return new List<string>(_listSectionNames);
			}
		}

		private string GetRaw(string section,string key) {
			if(section==null || key==null) {
				return null;
			}
			lock(_lock) {
				Dictionary<string,string> dictKeys;
				if(!_dictSections.TryGetValue(section.Trim(),out dictKeys)) {
					return null;
				}
				string value;
				if(!dictKeys.TryGetValue(key.Trim(),out value)) {
					return null;
				}
				return value;
			}
		}
		#endregion Getters

		#region Set and Save
		///<summary>Sets a value in memory; Save writes it. Section "" means keys before the first header. The value is trimmed. Returns
		///false, with LastError, when a name or the value cannot be written to an INI line: a section containing ']' or ';', a key
		///that is empty, starts with '[' or contains '=' or ';', a value containing ';', or a control character other than tab in any
		///of them (a line break; or NUL, which would make the file read as corrupt). Strings from native code, such as a camera's
		///name, may end in NUL and must be cut there before they are Set.</summary>
		public bool Set(string section,string key,string value) {
			lock(_lock) {
				_lastError="";
				string sectionTrimmed=section?.Trim();
				string keyTrimmed=key?.Trim();
				string valueTrimmed=(value ?? "").Trim();
				string error="";
				if(sectionTrimmed==null || sectionTrimmed.IndexOfAny(new[] { ']',';' })>=0 || HasControlChar(sectionTrimmed)) {
					error="Invalid capture.ini section name: "+section;
				}
				else if(string.IsNullOrEmpty(keyTrimmed) || keyTrimmed.StartsWith("[") || keyTrimmed.IndexOfAny(new[] { '=',';' })>=0
					|| HasControlChar(keyTrimmed))
				{
					error="Invalid capture.ini key name: "+key;
				}
				else if(valueTrimmed.IndexOf(';')>=0 || HasControlChar(valueTrimmed)) {
					error="A capture.ini value cannot contain ';', a line break or another control character: "+keyTrimmed;
				}
				if(error!="") {
					_lastError=error;
					return false;
				}
				ApplySet(_iniText.ListLines,sectionTrimmed,keyTrimmed,valueTrimmed);
				_listPending.Add(new[] { sectionTrimmed,keyTrimmed,valueTrimmed });
				Reparse();
				return true;
			}
		}

		///<summary>True when the text has a control character other than tab: CR, LF, NUL, the other C0 characters, DEL or a C1
		///character.</summary>
		private static bool HasControlChar(string text) {
			foreach(char c in text) {
				if(char.IsControl(c) && c!='\t') {
					return true;
				}
			}
			return false;
		}

		///<summary>Writes the values Set since the last Save. It re-reads the target file first and changes only those values, so every
		///other line and comment, and edits made by others since this file was loaded, are kept. Missing keys are added to the end of
		///their section and missing sections to the end of the file. Tries PathPrimary, then PathFallback. Returns true when a file
		///was written (or nothing needed writing); LastError then holds a warning if PathFallback had to be used. Returns false, with
		///LastError, when no file could be written; the values stay pending.</summary>
		public bool Save() {
			lock(_lock) {
				_lastError="";
				if(_listPending.Count==0) {
					return true;
				}
				List<string> listErrors=new List<string>();
				foreach(string pathTarget in new[] { PathPrimary,PathFallback }) {
					if(pathTarget==null) {
						continue;
					}
					string error;
					try {
						error=SaveTo(pathTarget,pathTarget==PathPrimary ? _funcGrantUsersModify : null);
					}
					catch(Exception ex) {
						error=ex.Message;
					}
					if(error=="") {
						if(pathTarget!=PathPrimary && PathPrimary!=null) {
							_lastError="Could not save capture settings to "+PathPrimary+" ("+listErrors[0]+"). They were saved to "+pathTarget+" instead.";
							if(FileExistsSafe(PathPrimary)) {
								_lastError+=" Settings are read from "+PathPrimary+" while it exists, so these values take effect only after an "
									+"administrator removes that file or makes it writable.";
							}
						}
						return true;
					}
					listErrors.Add(error);
				}
				if(listErrors.Count==0) {
					_lastError="No capture.ini path is set.";
				}
				else {
					_lastError="Could not save capture settings: "+string.Join(" ",listErrors);
				}
				return false;
			}
		}

		///<summary>Returns "" on success, otherwise why this target could not be written.</summary>
		private string SaveTo(string pathTarget,Func<string,string> funcGrantUsersModify) {
			string folder=Path.GetDirectoryName(Path.GetFullPath(pathTarget));
			if(!Directory.Exists(folder)) {
				try {
					Directory.CreateDirectory(folder);
				}
				catch(Exception ex) {
					return "Could not create "+folder+": "+ex.Message;
				}
				if(funcGrantUsersModify!=null) {
					string errorGrant;
					try {
						errorGrant=funcGrantUsersModify(folder) ?? "";
					}
					catch(Exception ex) {
						errorGrant=ex.Message;
					}
					if(errorGrant!="") {
						//Remove the folder so that a later save tries again to create it with the right ACL.
						try {
							Directory.Delete(folder,false);
						}
						catch {
						}
						return "Could not give the Users group access to "+folder+": "+errorGrant;
					}
				}
			}
			IniText iniTextBase;
			string errorRead;
			ReadStatus readStatus=ReadFile(pathTarget,out iniTextBase,out errorRead);
			if(readStatus==ReadStatus.Unreadable) {
				return errorRead+" It was not overwritten.";
			}
			if(readStatus==ReadStatus.Corrupt) {
				//Keep a copy of the unusable file, then write the settings in memory.
				try {
					File.Copy(pathTarget,pathTarget+".corrupt",true);
				}
				catch(Exception ex) {
					return errorRead+" A copy could not be kept, so it was not overwritten: "+ex.Message;
				}
			}
			if(readStatus!=ReadStatus.Ok) {
				//Missing or corrupt: start from the settings in memory, which hold whatever file was loaded plus the pending values.
				iniTextBase=_iniText.Clone();
			}
			IniText iniTextNew=iniTextBase.Clone();
			bool isChanged=readStatus!=ReadStatus.Ok;
			foreach(string[] arrayPending in _listPending) {
				isChanged|=ApplySet(iniTextNew.ListLines,arrayPending[0],arrayPending[1],arrayPending[2]);
			}
			if(isChanged) {
				string errorWrite=WriteFile(pathTarget,iniTextNew);
				if(errorWrite!="") {
					return "Could not write "+pathTarget+": "+errorWrite;
				}
			}
			_iniText=iniTextNew;
			_listPending.Clear();
			PathSaved=pathTarget;
			Reparse();
			return "";
		}

		///<summary>Sets key=value in the lines. Changes the line that takes effect (the last one for that key in that section), keeping
		///its spelling, spacing and comment; or adds the key after the last key line of the section's last block; or adds the
		///section and key at the end. Returns true if a line changed.</summary>
		private static bool ApplySet(List<string> listLines,string section,string key,string value) {
			string sectionCur="";
			int idxValueLine=-1;
			int idxInsert=section=="" ? 0 : -1;
			for(int i=0;i<listLines.Count;i++) {
				ParsedLine parsedLine=ParseLine(listLines[i]);
				if(parsedLine.Kind==LineKind.Section) {
					sectionCur=parsedLine.Name;
					if(SameName(sectionCur,section)) {
						idxInsert=i+1;
					}
					continue;
				}
				if(parsedLine.Kind!=LineKind.Value || !SameName(sectionCur,section)) {
					continue;
				}
				idxInsert=i+1;
				if(SameName(parsedLine.Name,key)) {
					idxValueLine=i;
				}
			}
			if(idxValueLine>=0) {
				string lineOld=listLines[idxValueLine];
				string lineNew=RewriteValue(lineOld,ParseLine(lineOld),value);
				if(lineNew==lineOld) {
					return false;
				}
				listLines[idxValueLine]=lineNew;
				return true;
			}
			string lineKey=key+"="+value;
			if(idxInsert>=0) {
				listLines.Insert(idxInsert,lineKey);
				return true;
			}
			if(listLines.Count>0 && listLines[listLines.Count-1].Trim()!="") {
				listLines.Add("");
			}
			listLines.Add("["+section+"]");
			listLines.Add(lineKey);
			return true;
		}

		///<summary>Replaces the value in a key line. When a comment follows after spaces, the comment keeps its column where it can.</summary>
		private static string RewriteValue(string line,ParsedLine parsedLine,string value) {
			string prefix=line.Substring(0,parsedLine.ValueStart);
			string suffix=line.Substring(parsedLine.ValueEnd);
			int idxComment=suffix.IndexOf(';');
			if(idxComment>0) {
				string gap=suffix.Substring(0,idxComment);
				if(gap.IndexOf('\t')<0) {
					int width=Math.Max(1,gap.Length+parsedLine.Value.Length-value.Length);
					suffix=new string(' ',width)+suffix.Substring(idxComment);
				}
			}
			return prefix+value+suffix;
		}
		#endregion Set and Save

		#region Parsing
		private enum LineKind {
			///<summary>Blank, comment, or anything else. Kept as is.</summary>
			Other,
			Section,
			Value,
		}

		private struct ParsedLine {
			public LineKind Kind;
			///<summary>Section or key name, trimmed.</summary>
			public string Name;
			///<summary>Value, trimmed, without its comment.</summary>
			public string Value;
			///<summary>Where the value starts in the line. For an empty value, just after '='.</summary>
			public int ValueStart;
			///<summary>Where the value ends in the line (exclusive).</summary>
			public int ValueEnd;
		}

		private static ParsedLine ParseLine(string line) {
			ParsedLine parsedLine=new ParsedLine();
			int idxComment=line.IndexOf(';');
			string content=idxComment<0 ? line : line.Substring(0,idxComment);
			string contentTrimmed=content.Trim();
			if(contentTrimmed.Length==0) {
				return parsedLine;
			}
			if(contentTrimmed[0]=='[') {
				parsedLine.Kind=LineKind.Section;
				int idxClose=contentTrimmed.IndexOf(']');
				//A header without ']' still starts a section, one whose name no caller uses, so the keys under it never count toward
				//the section above it.
				parsedLine.Name=idxClose<0 ? contentTrimmed : contentTrimmed.Substring(1,idxClose-1).Trim();
				return parsedLine;
			}
			int idxEquals=content.IndexOf('=');
			if(idxEquals<0) {
				return parsedLine;
			}
			string key=content.Substring(0,idxEquals).Trim();
			if(key.Length==0) {
				return parsedLine;
			}
			int start=idxEquals+1;
			int end=content.Length;
			while(end>start && char.IsWhiteSpace(content[end-1])) {
				end--;
			}
			while(start<end && char.IsWhiteSpace(content[start])) {
				start++;
			}
			if(start==end) {
				start=idxEquals+1;
				end=idxEquals+1;
			}
			parsedLine.Kind=LineKind.Value;
			parsedLine.Name=key;
			parsedLine.Value=content.Substring(start,end-start);
			parsedLine.ValueStart=start;
			parsedLine.ValueEnd=end;
			return parsedLine;
		}

		///<summary>Rebuilds the lookup from the lines. The last occurrence of a key in a section wins.</summary>
		private void Reparse() {
			Dictionary<string,Dictionary<string,string>> dictSections=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
			List<string> listSectionNames=new List<string>();
			string sectionCur="";
			foreach(string line in _iniText.ListLines) {
				ParsedLine parsedLine=ParseLine(line);
				if(parsedLine.Kind==LineKind.Other) {
					continue;
				}
				if(parsedLine.Kind==LineKind.Section) {
					sectionCur=parsedLine.Name;
				}
				Dictionary<string,string> dictKeys;
				if(!dictSections.TryGetValue(sectionCur,out dictKeys)) {
					dictKeys=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
					dictSections[sectionCur]=dictKeys;
					listSectionNames.Add(sectionCur);
				}
				if(parsedLine.Kind==LineKind.Value) {
					dictKeys[parsedLine.Name]=parsedLine.Value;
				}
			}
			_dictSections=dictSections;
			_listSectionNames=listSectionNames;
		}

		private static bool SameName(string name1,string name2) {
			return string.Equals(name1,name2,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>"0x8002", "0X8002" and "8002" all give 0x8002. Fails on empty, invalid, or above int.MaxValue.</summary>
		private static bool TryParseHex(string text,out int value) {
			value=0;
			if(text==null) {
				return false;
			}
			string digits=text.Trim();
			if(digits.StartsWith("0x",StringComparison.OrdinalIgnoreCase)) {
				digits=digits.Substring(2);
			}
			uint parsed;
			if(digits.Length==0 || !uint.TryParse(digits,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out parsed) || parsed>int.MaxValue) {
				return false;
			}
			value=(int)parsed;
			return true;
		}
		#endregion Parsing

		#region File access
		private enum ReadStatus {
			Missing,
			Ok,
			///<summary>The file exists but could not be read.</summary>
			Unreadable,
			///<summary>The file was read but is not a text file.</summary>
			Corrupt,
		}

		///<summary>The lines of a file and how to write them back the same way.</summary>
		private sealed class IniText {
			public List<string> ListLines=new List<string>();
			///<summary>65001 UTF-8, 1200 UTF-16 LE, 1201 UTF-16 BE, or 1252 for text that is not valid UTF-8 (older editors).</summary>
			public int CodePage=65001;
			public bool HasBom=false;
			public string NewLine="\r\n";

			public IniText Clone() {
				IniText iniText=(IniText)MemberwiseClone();
				iniText.ListLines=new List<string>(ListLines);
				return iniText;
			}

			public byte[] GetBytes() {
				StringBuilder stringBuilder=new StringBuilder();
				foreach(string line in ListLines) {
					stringBuilder.Append(line);
					stringBuilder.Append(NewLine);
				}
				string text=stringBuilder.ToString();
				if(CodePage==1252) {
					try {
						//Windows-1252 has no byte order mark.
						return Encoding.GetEncoding(1252,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetBytes(text);
					}
					catch(EncoderFallbackException) {
						//A value Set by the program has characters Windows-1252 lacks. Write UTF-8 instead, which keeps every character.
					}
				}
				Encoding encoding;
				byte[] bom;
				if(CodePage==1200) {
					encoding=new UnicodeEncoding(false,false);
					bom=new byte[] { 0xFF,0xFE };
				}
				else if(CodePage==1201) {
					encoding=new UnicodeEncoding(true,false);
					bom=new byte[] { 0xFE,0xFF };
				}
				else {
					encoding=new UTF8Encoding(false);
					bom=CodePage==65001 && HasBom ? new byte[] { 0xEF,0xBB,0xBF } : new byte[0];
				}
				byte[] bytesText=encoding.GetBytes(text);
				byte[] bytes=new byte[bom.Length+bytesText.Length];
				Buffer.BlockCopy(bom,0,bytes,0,bom.Length);
				Buffer.BlockCopy(bytesText,0,bytes,bom.Length,bytesText.Length);
				return bytes;
			}
		}

		private static ReadStatus ReadFile(string path,out IniText iniText,out string error) {
			iniText=null;
			error="";
			byte[] bytes;
			try {
				if(!File.Exists(path)) {
					return ReadStatus.Missing;
				}
				using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
					if(fileStream.Length>MaxFileBytes) {
						error=path+" is too large to be a capture.ini file.";
						return ReadStatus.Corrupt;
					}
					bytes=new byte[fileStream.Length];
					int countRead=0;
					while(countRead<bytes.Length) {
						int count=fileStream.Read(bytes,countRead,bytes.Length-countRead);
						if(count<=0) {
							break;
						}
						countRead+=count;
					}
					if(countRead<bytes.Length) {
						Array.Resize(ref bytes,countRead);
					}
				}
			}
			catch(Exception ex) {
				error="Could not read "+path+": "+ex.Message;
				return ReadStatus.Unreadable;
			}
			iniText=new IniText();
			string text;
			if(!TryDecode(bytes,iniText,out text)) {
				iniText=null;
				error=path+" is not a text file.";
				return ReadStatus.Corrupt;
			}
			if(text.IndexOf("\r\n",StringComparison.Ordinal)>=0) {
				iniText.NewLine="\r\n";
			}
			else if(text.IndexOf('\n')>=0) {
				iniText.NewLine="\n";
			}
			else if(text.IndexOf('\r')>=0) {
				iniText.NewLine="\r";
			}
			iniText.ListLines=SplitLines(text);
			return ReadStatus.Ok;
		}

		///<summary>Decodes by byte order mark (UTF-8, UTF-16 LE or BE), else as UTF-8, else as Windows-1252. Fails on bytes that no
		///text editor writes: a NUL character, or invalid UTF-8 after a UTF-8 byte order mark.</summary>
		private static bool TryDecode(byte[] bytes,IniText iniText,out string text) {
			text=null;
			try {
				if(bytes.Length>=2 && bytes[0]==0xFF && bytes[1]==0xFE) {
					iniText.CodePage=1200;
					iniText.HasBom=true;
					text=new UnicodeEncoding(false,true,true).GetString(bytes,2,bytes.Length-2);
				}
				else if(bytes.Length>=2 && bytes[0]==0xFE && bytes[1]==0xFF) {
					iniText.CodePage=1201;
					iniText.HasBom=true;
					text=new UnicodeEncoding(true,true,true).GetString(bytes,2,bytes.Length-2);
				}
				else if(bytes.Length>=3 && bytes[0]==0xEF && bytes[1]==0xBB && bytes[2]==0xBF) {
					iniText.CodePage=65001;
					iniText.HasBom=true;
					text=new UTF8Encoding(false,true).GetString(bytes,3,bytes.Length-3);
				}
				else {
					if(Array.IndexOf(bytes,(byte)0)>=0) {
						return false;
					}
					try {
						iniText.CodePage=65001;
						text=new UTF8Encoding(false,true).GetString(bytes);
					}
					catch(DecoderFallbackException) {
						iniText.CodePage=1252;
						text=Encoding.GetEncoding(1252).GetString(bytes);
					}
				}
			}
			catch(ArgumentException) {//DecoderFallbackException is an ArgumentException.
				return false;
			}
			return text.IndexOf('\0')<0;
		}

		private static List<string> SplitLines(string text) {
			List<string> listLines=new List<string>();
			int start=0;
			for(int i=0;i<text.Length;i++) {
				char c=text[i];
				if(c!='\r' && c!='\n') {
					continue;
				}
				listLines.Add(text.Substring(start,i-start));
				if(c=='\r' && i+1<text.Length && text[i+1]=='\n') {
					i++;
				}
				start=i+1;
			}
			if(start<text.Length) {
				listLines.Add(text.Substring(start));
			}
			return listLines;
		}

		///<summary>Writes through a temporary file in the same folder, so a crash never leaves half a file; an existing file keeps its
		///ACL (File.Replace). If the folder does not allow creating the temporary file, writes in place. A read-only file is never
		///written, since an administrator may have made it read-only on purpose. Returns "" or the error.</summary>
		private static string WriteFile(string path,IniText iniText) {
			try {
				if(File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly)!=0) {
					return "the file is read-only.";
				}
			}
			catch(Exception ex) {
				return ex.Message;
			}
			byte[] bytes=iniText.GetBytes();
			string pathTemp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
			try {
				File.WriteAllBytes(pathTemp,bytes);
				if(File.Exists(path)) {
					File.Replace(pathTemp,path,null,true);
				}
				else {
					File.Move(pathTemp,path);
				}
				return "";
			}
			catch {
				try {
					File.Delete(pathTemp);
				}
				catch {
				}
			}
			try {
				using(FileStream fileStream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read)) {
					fileStream.Write(bytes,0,bytes.Length);
				}
				return "";
			}
			catch(Exception ex) {
				return ex.Message;
			}
		}

		private static bool FileExistsSafe(string path) {
			try {
				return File.Exists(path);
			}
			catch {
				return false;
			}
		}

		private static string CombineSpecialFolder(Environment.SpecialFolder specialFolder) {
			try {
				string folder=Environment.GetFolderPath(specialFolder);
				if(string.IsNullOrEmpty(folder)) {
					return null;
				}
				return Path.Combine(folder,FolderRelative,FileName);
			}
			catch {
				return null;
			}
		}

		///<summary>Gives the local Users group (well-known SID S-1-5-32-545) Modify on the folder, inherited by its files and subfolders.
		///Returns "" on success, otherwise the error.</summary>
		internal static string GrantUsersModify(string folder) {
			try {
				DirectoryInfo directoryInfo=new DirectoryInfo(folder);
				DirectorySecurity directorySecurity=directoryInfo.GetAccessControl(AccessControlSections.Access);
				SecurityIdentifier sidUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
				directorySecurity.AddAccessRule(new FileSystemAccessRule(sidUsers,FileSystemRights.Modify,
					InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
				directoryInfo.SetAccessControl(directorySecurity);
				return "";
			}
			catch(Exception ex) {
				return ex.Message;
			}
		}
		#endregion File access
	}
}
