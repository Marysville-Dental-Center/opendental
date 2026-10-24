using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenDentBusiness {
	///<summary></summary>
	public class CodeGroups {
		#region Cache Pattern
		private class CodeGroupCache : CacheListAbs<CodeGroup> {
			protected override List<CodeGroup> GetCacheFromDb() {
				string command="SELECT * FROM codegroup ORDER BY ItemOrder";
				return Crud.CodeGroupCrud.SelectMany(command);
			}
			protected override List<CodeGroup> TableToList(DataTable table) {
				return Crud.CodeGroupCrud.TableToList(table);
			}
			protected override CodeGroup Copy(CodeGroup codeGroup) {
				return codeGroup.Copy();
			}
			protected override DataTable ListToTable(List<CodeGroup> listCodeGroups) {
				return Crud.CodeGroupCrud.ListToTable(listCodeGroups,"CodeGroup");
			}
			protected override void FillCacheIfNeeded() {
				CodeGroups.GetTableFromCache(false);
			}
			protected override bool IsInListShort(CodeGroup codeGroup) {
				//From Open Dental 25.1, IsHidden means the group is retired. The short list holds every group that is not retired.
				return !codeGroup.IsHidden;
			}
		}

		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static CodeGroupCache _codeGroupCache=new CodeGroupCache();

		public static void ClearCache() {
			_codeGroupCache.ClearCache();
		}

		public static List<CodeGroup> GetDeepCopy(bool isShort=false) {
			return _codeGroupCache.GetDeepCopy(isShort);
		}

		public static int GetCount(bool isShort=false) {
			return _codeGroupCache.GetCount(isShort);
		}

		public static bool GetExists(Predicate<CodeGroup> match,bool isShort=false) {
			return _codeGroupCache.GetExists(match,isShort);
		}

		public static int GetFindIndex(Predicate<CodeGroup> match,bool isShort=false) {
			return _codeGroupCache.GetFindIndex(match,isShort);
		}

		public static CodeGroup GetFirst(bool isShort=false) {
			return _codeGroupCache.GetFirst(isShort);
		}

		public static CodeGroup GetFirst(Func<CodeGroup,bool> match,bool isShort=false) {
			return _codeGroupCache.GetFirst(match,isShort);
		}

		public static CodeGroup GetFirstOrDefault(Func<CodeGroup,bool> match,bool isShort=false) {
			return _codeGroupCache.GetFirstOrDefault(match,isShort);
		}

		public static CodeGroup GetLast(bool isShort=false) {
			return _codeGroupCache.GetLast(isShort);
		}

		public static CodeGroup GetLastOrDefault(Func<CodeGroup,bool> match,bool isShort=false) {
			return _codeGroupCache.GetLastOrDefault(match,isShort);
		}

		public static List<CodeGroup> GetWhere(Predicate<CodeGroup> match,bool isShort=false) {
			return _codeGroupCache.GetWhere(match,isShort);
		}

		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_codeGroupCache.FillCacheFromTable(table);
		}

		///<summary>Returns the cache in the form of a DataTable. Always refreshes the ClientMT's cache.</summary>
		///<param name="doRefreshCache">If true, will refresh the cache if MiddleTierRole is ClientDirect or ServerWeb.</param> 
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_codeGroupCache.FillCacheFromTable(table);
				return table;
			}
			return _codeGroupCache.GetTableFromCache(doRefreshCache);
		}
		#endregion Cache Pattern

		#region Methods - Get
		///<summary>Whether a procedure code is a member of a code group. Range aware: "D2391-D2394" holds every code from D2391 through D2394, and a
		///code only matches a whole entry, so "D111" is not in a group that lists "D1110". Returns false for a CodeGroupNum that is not in the cache
		///(0 when called from ComputeForOrdinal, for example).</summary>
		public static bool IsProcInCodeGroup(string procCodeString,long codeGroupNum) {
			Meth.NoCheckMiddleTierRole();
			return IsProcInCodeGroup(procCodeString,GetOne(codeGroupNum));
		}

		///<summary>Whether a procedure code is a member of the code group passed in. See ProcCodeRanges for the rules. A null group holds nothing.</summary>
		public static bool IsProcInCodeGroup(string procCodeString,CodeGroup codeGroup) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroup==null) {
				return false;
			}
			return ProcCodeRanges.Get(codeGroup.ProcCodes).Contains(procCodeString);
		}

		///<summary>From Open Dental 25.1: true when the code group is retired (codegroup.IsHidden). Retired groups stay valid on the benefits that use
		///them and in the estimates, but they are not offered for new benefits and get no rows in the Insurance Benefits window. Before 25.1 this flag
		///meant "not a row in the frequency grid"; that is now ShowInFrequency. Returns true if the codeGroupNum passed in is invalid. Returns false if
		///0 is passed in.</summary>
		public static bool IsHidden(long codeGroupNum) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroupNum==0) {
				return false;
			}
			CodeGroup codeGroup=CodeGroups.GetOne(codeGroupNum);
			if(codeGroup==null) {
				return true;//Invalid CodeGroupNum passed in or database corruption.
			}
			return codeGroup.IsHidden;
		}

		///<summary>If true, this codegroup shows in Age Limitations grid. Control of showing in Freq Lim is done separately using ShowInFrequency. Returns true if the codeGroupNum passed in is invalid. Returns false if 0 is passed in.</summary>
		public static bool IsShownInAgeLimit(long codeGroupNum) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroupNum==0) {
				return false;
			}
			CodeGroup codeGroup=GetOne(codeGroupNum);
			if(codeGroup==null) {
				return true;//Invalid CodeGroupNum passed in or database corruption.
			}
			return codeGroup.ShowInAgeLimit;
		}

		///<summary>If true, this codegroup gets a row in the frequency limitations grid (codegroup.ShowInFrequency, Open Dental 25.1). Returns true if
		///the codeGroupNum passed in is invalid. Returns false if 0 is passed in.</summary>
		public static bool IsShownInFrequency(long codeGroupNum) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroupNum==0) {
				return false;
			}
			CodeGroup codeGroup=GetOne(codeGroupNum);
			if(codeGroup==null) {
				return true;//Invalid CodeGroupNum passed in or database corruption.
			}
			return codeGroup.ShowInFrequency;
		}

		///<summary>The code groups that get a pre-filled row in the Age Limitations grid: flagged ShowInAgeLimit and not retired, in ItemOrder.</summary>
		public static List<CodeGroup> GetForAgeLimitGrid() {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => x.ShowInAgeLimit && !x.IsHidden);
		}

		///<summary>The code groups that get a pre-filled row in the frequency limitations grids: flagged ShowInFrequency and not retired, in ItemOrder.</summary>
		public static List<CodeGroup> GetForFrequencyGrid() {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => x.ShowInFrequency && !x.IsHidden);
		}

		///<summary>The code groups that get a pre-filled row in the Other Benefits grid (codegroup.ShowInOther, Open Dental 25.3): flagged and not
		///retired, in ItemOrder.</summary>
		public static List<CodeGroup> GetForOtherBenefitsGrid() {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => x.ShowInOther && !x.IsHidden);
		}

		///<summary>The code groups offered in the Edit Benefit code group list: every group that is not retired, plus the group the benefit already
		///uses even when it is retired, so that editing an old benefit never drops its group. In ItemOrder.</summary>
		public static List<CodeGroup> GetForBenefitPicker(long codeGroupNumCur) {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => !x.IsHidden || (codeGroupNumCur!=0 && x.CodeGroupNum==codeGroupNumCur));
		}

		///<summary>Whether the Insurance Benefits window shows a row for the benefit passed in. Benefits of retired code groups are kept and the
		///estimates still use them, but they get no row in any of its grids.</summary>
		public static bool IsShownInBenefitsWindow(Benefit benefit) {
			Meth.NoCheckMiddleTierRole();
			if(benefit==null || benefit.CodeGroupNum==0) {
				return true;
			}
			CodeGroup codeGroup=GetOne(benefit.CodeGroupNum);
			if(codeGroup==null) {
				return true;//Unknown group: show the row so that the user can see and fix it.
			}
			return !codeGroup.IsHidden;
		}

		///<summary>The procedure codes in the code group, ranges expanded, in ProcCode order. Listed codes that are not in the database are left out.</summary>
		public static List<ProcedureCode> GetProcedureCodes(CodeGroup codeGroup) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroup==null) {
				return new List<ProcedureCode>();
			}
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Get(codeGroup.ProcCodes);
			return ProcedureCodes.GetWhereFromList(x => procCodeRanges.Contains(x.ProcCode)).OrderBy(x => x.ProcCode).ToList();
		}

		///<summary>How many insurance plans and patient plan overrides have a benefit row that uses the code group. A code group in use can not be
		///deleted.</summary>
		public static int GetCountPlansUsing(long codeGroupNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),codeGroupNum);
			}
			if(codeGroupNum==0) {
				return 0;
			}
			string command="SELECT COUNT(DISTINCT PlanNum,PatPlanNum) FROM benefit WHERE CodeGroupNum="+POut.Long(codeGroupNum);
			return PIn.Int(Db.GetCount(command));
		}

		public static long GetCodeGroupNumForCodeGroupFixed(EnumCodeGroupFixed enumCodeGroupFixed,bool isShort=true) {
			Meth.NoCheckMiddleTierRole();
			CodeGroup codeGroup=GetFirstOrDefault(x => x.CodeGroupFixed==enumCodeGroupFixed,isShort);
			if(codeGroup==null) {
				return 0;
			}
			return codeGroup.CodeGroupNum;
		}

		public static List<long> GetCodeNums(long codeGroupNum) {
			Meth.NoCheckMiddleTierRole();
			List<long> listCodeNums=new List<long>();
			CodeGroup codeGroup=GetOne(codeGroupNum);
			if(codeGroup!=null) {
				listCodeNums=ProcedureCodes.GetCodeNumsForProcCodes(codeGroup.ProcCodes);
			}
			return listCodeNums;
		}

		///<summary>Returns the GroupName, including '(hidden)' if isHidden is true, for the CodeGroup passed in.</summary>
		public static string GetGroupName(long codeGroupNum,bool isHidden=false) {
			Meth.NoCheckMiddleTierRole();
			CodeGroup codeGroup=GetOne(codeGroupNum);
			return GetGroupName(codeGroup,isHidden:isHidden);
		}

		///<summary>Returns the GroupName, including '(hidden)' if isHidden is true, for the CodeGroup passed in.</summary>
		public static string GetGroupName(CodeGroup codeGroup,bool isHidden=false) {
			Meth.NoCheckMiddleTierRole();
			string groupName="";
			if(codeGroup==null) {
				return groupName;
			}
			groupName=codeGroup.GroupName;
			if(isHidden) {
				groupName+=" "+Lans.g("CodeGroups","(hidden)");
			}
			return groupName;
		}

		///<summary>Grab one code group from the full list of codegroups in the cache.</summary>
		public static CodeGroup GetOne(long codeGroupNum) {
			Meth.NoCheckMiddleTierRole();
			return GetFirstOrDefault(x => x.CodeGroupNum==codeGroupNum);
		}

		public static CodeGroup GetOneForCodeGroupFixed(EnumCodeGroupFixed codeGroupFixed,bool isShort=true) {
			Meth.NoCheckMiddleTierRole();
			return GetFirstOrDefault(x => x.CodeGroupFixed==codeGroupFixed,isShort);
		}

		///<summary>Gets one CodeGroup from db. Returns null if not found. Used by the API.</summary>
		public static CodeGroup GetOneFromDb(long codeGroupNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<CodeGroup>(MethodBase.GetCurrentMethod(),codeGroupNum);
			}
			if(codeGroupNum==0) {
				return null;
			}
			string command="SELECT * FROM codegroup "
				+"WHERE CodeGroupNum = '"+POut.Long(codeGroupNum)+"'";
			return Crud.CodeGroupCrud.SelectOne(command);
		}

		///<summary>Should only be called when comparing the item order of non-zero codegroup nums.</summary>
		public static int GetOrder(long codeGroupNum) {
			return GetFirst(x=>x.CodeGroupNum==codeGroupNum).ItemOrder;
		}

		///<summary>Gets a list of all codegroups from the db. Returns an empty list if none are found. Used by the API.</summary>
		public static List<CodeGroup> GetManyFromDb() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<CodeGroup>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM codegroup";
			return Crud.CodeGroupCrud.SelectMany(command);
		}

		///<summary>Used by the API to get a list of codegroups. Returns an empty list if none are found.</summary>
		public static List<CodeGroup> GetCodeGroupsForApi(int limit,int offset) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<CodeGroup>>(MethodBase.GetCurrentMethod(),limit,offset);
			}
			string command="SELECT * FROM codegroup ";
			command+="ORDER BY CodeGroupNum " //Ensure order for limit and offset
				+"LIMIT "+POut.Int(offset)+", "+POut.Int(limit);
			return Crud.CodeGroupCrud.SelectMany(command);
		}

		///<summary>Returns true if there is a code group with at least one valid procedure code associated with each EnumCodeGroupFixed. Otherwise; false.</summary>
		public static bool HasValidCodeGroupFixed(EnumCodeGroupFixed enumCodeGroupFixed) {
			Meth.NoCheckMiddleTierRole();
			CodeGroup codeGroup=GetOneForCodeGroupFixed(enumCodeGroupFixed);
			if(codeGroup==null) {
				return false;
			}
			List<long> listCodeNums=GetCodeNums(codeGroup.CodeGroupNum);
			if(listCodeNums.IsNullOrEmpty()) {
				return false;
			}
			if(listCodeNums.All(x => x==0)) {
				return false;
			}
			//There is a corresponding CodeGroup and it has at least one valid proc code.
			return true;
		}
		#endregion Methods - Get

		#region Methods - Modify
		///<summary></summary>
		public static long Insert(CodeGroup codeGroup) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				codeGroup.CodeGroupNum=Meth.GetLong(MethodBase.GetCurrentMethod(),codeGroup);
				return codeGroup.CodeGroupNum;
			}
			return Crud.CodeGroupCrud.Insert(codeGroup);
		}

		///<summary>Inserts, updates, or deletes database rows to match supplied list.  Returns true if db changes were made.</summary>
		public static bool Sync(List<CodeGroup> listCodeGroups,List<CodeGroup> listCodeGroupsOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),listCodeGroups,listCodeGroupsOld);
			}
			return Crud.CodeGroupCrud.Sync(listCodeGroups,listCodeGroupsOld);
		}

		///<summary></summary>
		public static void Update(CodeGroup codeGroup) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),codeGroup);
				return;
			}
			Crud.CodeGroupCrud.Update(codeGroup);
		}
		#endregion Methods - Modify



	}

	///<summary>A parsed list of procedure codes and code ranges, as stored in codegroup.ProcCodes from Open Dental 24.4: entries separated by
	///commas, for example "D0100-D0999,D2140-D2161,D2750". A range is two codes joined by a dash that have the same letter prefix (or none) and
	///numeric parts with the same number of digits; it holds every code with that prefix and a numeric part between the two ends, inclusive.
	///Anything else is a single code, compared whole and without regard to case, so a code with a dash in it, such as "D1110-2", is still one
	///code. A range whose start is after its end holds nothing (FormCodeGroupEdit refuses to save one). Instances are immutable and cached by text.</summary>
	public class ProcCodeRanges {
		private static readonly Regex _regexRange=new Regex(@"^([A-Za-z]*)(\d{1,9})-([A-Za-z]*)(\d{1,9})$",RegexOptions.Compiled);
		private static readonly ConcurrentDictionary<string,ProcCodeRanges> _dictionaryCache=new ConcurrentDictionary<string,ProcCodeRanges>();
		///<summary>Only to keep the cache bounded; distinct ProcCodes texts are few in practice.</summary>
		private const int CACHE_MAX=2000;
		private readonly HashSet<string> _hashSetCodes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly List<ProcCodeRange> _listRanges=new List<ProcCodeRange>();
		///<summary>The entries, trimmed, in the order they were listed. Empty entries are dropped.</summary>
		public readonly List<string> ListEntries=new List<string>();

		private ProcCodeRanges() {
		}

		///<summary>The parsed form of the text, from the cache when it was parsed before. Null and blank text give an empty list.</summary>
		public static ProcCodeRanges Get(string procCodes) {
			string key=procCodes??"";
			if(_dictionaryCache.TryGetValue(key,out ProcCodeRanges procCodeRanges)) {
				return procCodeRanges;
			}
			procCodeRanges=Parse(key);
			if(_dictionaryCache.Count>=CACHE_MAX) {
				_dictionaryCache.Clear();
			}
			_dictionaryCache[key]=procCodeRanges;
			return procCodeRanges;
		}

		///<summary>Parses the text without using the cache.</summary>
		public static ProcCodeRanges Parse(string procCodes) {
			ProcCodeRanges procCodeRanges=new ProcCodeRanges();
			if(string.IsNullOrWhiteSpace(procCodes)) {
				return procCodeRanges;
			}
			string[] arrayEntries=procCodes.Split(',');
			for(int i=0;i<arrayEntries.Length;i++) {
				string entry=arrayEntries[i].Trim();
				if(entry=="") {
					continue;
				}
				procCodeRanges.ListEntries.Add(entry);
				if(TryParseRange(entry,out ProcCodeRange procCodeRange)) {
					procCodeRanges._listRanges.Add(procCodeRange);
					continue;
				}
				procCodeRanges._hashSetCodes.Add(entry);
			}
			return procCodeRanges;
		}

		///<summary>True when the entry is shaped like a range (see the class summary), whether or not its ends are in order.</summary>
		public static bool TryParseRange(string entry,out ProcCodeRange procCodeRange) {
			procCodeRange=null;
			if(string.IsNullOrEmpty(entry)) {
				return false;
			}
			Match match=_regexRange.Match(entry.Trim());
			if(!match.Success) {
				return false;
			}
			string prefixStart=match.Groups[1].Value;
			string digitsStart=match.Groups[2].Value;
			string prefixEnd=match.Groups[3].Value;
			string digitsEnd=match.Groups[4].Value;
			if(!string.Equals(prefixStart,prefixEnd,StringComparison.OrdinalIgnoreCase) || digitsStart.Length!=digitsEnd.Length) {
				return false;//For example D1110-2, a single code that has a dash in it.
			}
			procCodeRange=new ProcCodeRange(prefixStart,long.Parse(digitsStart),long.Parse(digitsEnd),digitsStart.Length);
			return true;
		}

		///<summary>Whether the procedure code is one of the listed codes or falls inside one of the listed ranges.</summary>
		public bool Contains(string procCode) {
			if(string.IsNullOrWhiteSpace(procCode)) {
				return false;
			}
			procCode=procCode.Trim();
			if(_hashSetCodes.Contains(procCode)) {
				return true;
			}
			for(int i=0;i<_listRanges.Count;i++) {
				if(_listRanges[i].Contains(procCode)) {
					return true;
				}
			}
			return false;
		}

		///<summary>Tidies text typed into Code Group Edit: removes every space, drops empty and duplicate entries (keeping the first of each, without
		///regard to case) and keeps the order. Returns null and sets error when a range runs backwards. Codes are not required to exist.</summary>
		public static string Normalize(string procCodes,out string error) {
			error="";
			if(procCodes==null) {
				return "";
			}
			string text=Regex.Replace(procCodes,@"\s+","");
			List<string> listEntries=new List<string>();
			HashSet<string> hashSetSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] arrayEntries=text.Split(',');
			for(int i=0;i<arrayEntries.Length;i++) {
				string entry=arrayEntries[i];
				if(entry=="" || !hashSetSeen.Add(entry)) {
					continue;
				}
				if(TryParseRange(entry,out ProcCodeRange procCodeRange) && procCodeRange.Start>procCodeRange.End) {
					error=Lans.g("CodeGroups","The start of this range is after its end:")+" "+entry;
					return null;
				}
				listEntries.Add(entry);
			}
			return string.Join(",",listEntries);
		}
	}

	///<summary>One range of a ProcCodeRanges list: a letter prefix, the first and last numeric parts, and how many digits the numeric part has.</summary>
	public class ProcCodeRange {
		public readonly string Prefix;
		public readonly long Start;
		public readonly long End;
		public readonly int Digits;

		public ProcCodeRange(string prefix,long start,long end,int digits) {
			Prefix=prefix;
			Start=start;
			End=end;
			Digits=digits;
		}

		///<summary>Whether the code has this prefix, a numeric part of the same number of digits and nothing after it, between Start and End.</summary>
		public bool Contains(string procCode) {
			if(procCode==null || procCode.Length!=Prefix.Length+Digits) {
				return false;
			}
			if(!procCode.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			long number=0;
			for(int i=Prefix.Length;i<procCode.Length;i++) {
				char c=procCode[i];
				if(c<'0' || c>'9') {
					return false;
				}
				number=number*10+(c-'0');
			}
			return number>=Start && number<=End;
		}
	}
}