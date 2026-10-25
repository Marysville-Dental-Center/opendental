using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;
using DataConnectionBase;

namespace OpenDentBusiness{
	///<summary>Query Monitor filter groups (Open Dental 24.4, table queryfilter). A group is a GroupName shared by one or more rows; it exists only
	///while it has at least one row. While a group is active in the Query Monitor, any query whose text contains one of the group's FilterTexts
	///(ignoring case) is left out of the feed and the log. Which group is active is a per-user local setting (see QueryFilterActiveGroups), because
	///Open Dental's published schema has no userodpref type for it and this fork must not add enum numbers Open Dental may use later.</summary>
	public class QueryFilters{
		///<summary>The longest FilterText and GroupName the varchar(255) columns hold.</summary>
		public const int MaxLength=255;
		///<summary>First line of an exported group file, followed by the group name. Each following non-blank line is one filter text.</summary>
		public const string ExportHeaderPrefix="Query Filter Group:";

		#region Get Methods
		///<summary>Every filter row, ordered by group name and then key.</summary>
		public static List<QueryFilter> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<QueryFilter>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM queryfilter ORDER BY GroupName,QueryFilterNum";
			return Crud.QueryFilterCrud.SelectMany(command);
		}

		///<summary>The filter rows of one group (exact name, as stored), ordered by key.</summary>
		public static List<QueryFilter> GetForGroup(string groupName) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<QueryFilter>>(MethodBase.GetCurrentMethod(),groupName);
			}
			string command="SELECT * FROM queryfilter WHERE GroupName='"+POut.String(groupName??"")+"' ORDER BY QueryFilterNum";
			return Crud.QueryFilterCrud.SelectMany(command);
		}
		#endregion Get Methods

		#region Modification Methods
		public static long Insert(QueryFilter queryFilter) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				queryFilter.QueryFilterNum=Meth.GetLong(MethodBase.GetCurrentMethod(),queryFilter);
				return queryFilter.QueryFilterNum;
			}
			return Crud.QueryFilterCrud.Insert(queryFilter);
		}

		public static void Update(QueryFilter queryFilter) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),queryFilter);
				return;
			}
			Crud.QueryFilterCrud.Update(queryFilter);
		}

		public static void Delete(long queryFilterNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),queryFilterNum);
				return;
			}
			Crud.QueryFilterCrud.Delete(queryFilterNum);
		}

		///<summary>Writes the difference between listOld (the rows as they were loaded) and listNew (the edited rows): rows in listNew with key 0 are
		///inserted, rows whose text or group changed are updated, and rows of listOld missing from listNew are deleted. Rows that another user added
		///after listOld was loaded are left alone. Returns true when anything was written. listNew should already be normalized
		///(see NormalizeForSave).</summary>
		public static bool Sync(List<QueryFilter> listNew,List<QueryFilter> listOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),listNew,listOld);
			}
			listNew=listNew??new List<QueryFilter>();
			listOld=listOld??new List<QueryFilter>();
			bool isChanged=false;
			Dictionary<long,QueryFilter> dictOld=listOld.Where(x => x.QueryFilterNum!=0).GroupBy(x => x.QueryFilterNum)
				.ToDictionary(x => x.Key,x => x.First());
			HashSet<long> hashSetKept=new HashSet<long>();
			for(int i=0;i<listNew.Count;i++) {
				QueryFilter queryFilter=listNew[i];
				if(queryFilter.QueryFilterNum==0 || !dictOld.ContainsKey(queryFilter.QueryFilterNum)) {
					Crud.QueryFilterCrud.Insert(queryFilter);
					isChanged=true;
					continue;
				}
				hashSetKept.Add(queryFilter.QueryFilterNum);
				if(Crud.QueryFilterCrud.Update(queryFilter,dictOld[queryFilter.QueryFilterNum])) {
					isChanged=true;
				}
			}
			List<long> listNumsToDelete=dictOld.Keys.Where(x => !hashSetKept.Contains(x)).ToList();
			if(listNumsToDelete.Count>0) {
				Crud.QueryFilterCrud.DeleteMany(listNumsToDelete);
				isChanged=true;
			}
			return isChanged;
		}
		#endregion Modification Methods

		#region Misc Methods
		///<summary>Cleans a filter text the way it is saved: line breaks become spaces, the ends are trimmed, and it is cut to MaxLength. Returns ""
		///for null or whitespace, which callers must reject.</summary>
		public static string CleanFilterText(string filterText) {
			if(filterText==null) {
				return "";
			}
			string text=filterText.Replace("\r\n"," ").Replace("\r"," ").Replace("\n"," ").Trim();
			if(text.Length>MaxLength) {
				text=text.Substring(0,MaxLength).Trim();
			}
			return text;
		}

		///<summary>Cleans a group name the same way as a filter text.</summary>
		public static string CleanGroupName(string groupName) {
			return CleanFilterText(groupName);
		}

		///<summary>The distinct group names in the list, sorted ignoring case. A name that differs only in case from an earlier one is not repeated.
		///</summary>
		public static List<string> GetGroupNames(List<QueryFilter> listQueryFilters) {
			return (listQueryFilters??new List<QueryFilter>()).Select(x => x.GroupName??"").Where(x => x!="")
				.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList();
		}

		///<summary>The filter texts of the named group (matched ignoring case), cleaned, without blanks or duplicates. These are what the Query Monitor
		///applies. Empty when the group name is blank or has no rows.</summary>
		public static List<string> GetFilterTextsForGroup(List<QueryFilter> listQueryFilters,string groupName) {
			if(string.IsNullOrWhiteSpace(groupName) || listQueryFilters==null) {
				return new List<string>();
			}
			return listQueryFilters.Where(x => string.Equals(x.GroupName,groupName,StringComparison.OrdinalIgnoreCase))
				.Select(x => CleanFilterText(x.FilterText)).Where(x => x!="").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		///<summary>Returns the rows to save: every text and group name cleaned, rows with a blank text or group removed, and duplicate texts within a
		///group (ignoring case) removed. Of duplicates, the first row is kept, preferring one that already has a key so it is updated rather than
		///deleted and reinserted. The objects in the returned list are the ones passed in.</summary>
		public static List<QueryFilter> NormalizeForSave(List<QueryFilter> listQueryFilters) {
			List<QueryFilter> listRet=new List<QueryFilter>();
			if(listQueryFilters==null) {
				return listRet;
			}
			Dictionary<string,QueryFilter> dictKept=new Dictionary<string,QueryFilter>(StringComparer.OrdinalIgnoreCase);
			List<QueryFilter> listOrdered=listQueryFilters.Where(x => x!=null).OrderBy(x => x.QueryFilterNum==0 ? 1 : 0).ToList();
			for(int i=0;i<listOrdered.Count;i++) {
				QueryFilter queryFilter=listOrdered[i];
				queryFilter.FilterText=CleanFilterText(queryFilter.FilterText);
				queryFilter.GroupName=CleanGroupName(queryFilter.GroupName);
				if(queryFilter.FilterText=="" || queryFilter.GroupName=="") {
					continue;
				}
				string key=queryFilter.GroupName+"\n"+queryFilter.FilterText;
				if(dictKept.ContainsKey(key)) {
					continue;
				}
				dictKept[key]=queryFilter;
			}
			//Keep the caller's order.
			for(int i=0;i<listQueryFilters.Count;i++) {
				if(listQueryFilters[i]!=null && dictKept.Values.Contains(listQueryFilters[i])) {
					listRet.Add(listQueryFilters[i]);
				}
			}
			return listRet;
		}

		///<summary>Sets GroupName to newName on every row of the group named oldName (matched ignoring case). Returns how many rows changed.</summary>
		public static int RenameGroup(List<QueryFilter> listQueryFilters,string oldName,string newName) {
			int count=0;
			if(listQueryFilters==null || string.IsNullOrWhiteSpace(oldName)) {
				return count;
			}
			newName=CleanGroupName(newName);
			for(int i=0;i<listQueryFilters.Count;i++) {
				if(string.Equals(listQueryFilters[i].GroupName,oldName,StringComparison.OrdinalIgnoreCase)) {
					listQueryFilters[i].GroupName=newName;
					count++;
				}
			}
			return count;
		}

		///<summary>Returns groupName when no existing name matches it (ignoring case); otherwise the first of "groupName (2)", "groupName (3)", ... that
		///is free. The result fits MaxLength.</summary>
		public static string GetUniqueGroupName(string groupName,IEnumerable<string> listExistingNames) {
			groupName=CleanGroupName(groupName);
			if(groupName=="") {
				groupName="Imported";
			}
			HashSet<string> hashSetNames=new HashSet<string>((listExistingNames??new List<string>()).Where(x => x!=null),StringComparer.OrdinalIgnoreCase);
			if(!hashSetNames.Contains(groupName)) {
				return groupName;
			}
			for(int i=2;;i++) {
				string suffix=" ("+i+")";
				string baseName=groupName.Length+suffix.Length>MaxLength ? groupName.Substring(0,MaxLength-suffix.Length) : groupName;
				string candidate=baseName+suffix;
				if(!hashSetNames.Contains(candidate)) {
					return candidate;
				}
			}
		}

		///<summary>The names of the groups whose rows differ between the two lists (added, removed, renamed or with a changed text). A renamed group is
		///reported under both names. Used for the security log.</summary>
		public static List<string> GetChangedGroupNames(List<QueryFilter> listNew,List<QueryFilter> listOld) {
			listNew=listNew??new List<QueryFilter>();
			listOld=listOld??new List<QueryFilter>();
			List<string> listNames=new List<string>();
			List<string> listAllGroups=GetGroupNames(listNew.Concat(listOld).ToList());
			for(int i=0;i<listAllGroups.Count;i++) {
				List<string> listTextsNew=GetGroupSignature(listNew,listAllGroups[i]);
				List<string> listTextsOld=GetGroupSignature(listOld,listAllGroups[i]);
				if(!listTextsNew.SequenceEqual(listTextsOld)) {
					listNames.Add(listAllGroups[i]);
				}
			}
			return listNames;
		}

		private static List<string> GetGroupSignature(List<QueryFilter> listQueryFilters,string groupName) {
			return listQueryFilters.Where(x => string.Equals(x.GroupName,groupName,StringComparison.OrdinalIgnoreCase))
				.Select(x => x.GroupName+"\n"+x.QueryFilterNum+"\n"+x.FilterText).OrderBy(x => x,StringComparer.Ordinal).ToList();
		}

		///<summary>The text of an export file: a header line with the group name, then one filter text per line.</summary>
		public static string ExportGroupToText(string groupName,IEnumerable<string> listFilterTexts) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(ExportHeaderPrefix+" "+CleanGroupName(groupName)+"\r\n");
			foreach(string filterText in listFilterTexts??new List<string>()) {
				string text=CleanFilterText(filterText);
				if(text=="") {
					continue;
				}
				stringBuilder.Append(text+"\r\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>Reads a file written by ExportGroupToText. The first non-blank line must be the header; each later non-blank line is one filter
		///text (cleaned, duplicates ignoring case dropped). Returns false, with an error message, when the header or the group name is missing or when
		///the file holds no filter text.</summary>
		public static bool TryParseImportText(string text,out string groupName,out List<string> listFilterTexts,out string error) {
			groupName="";
			listFilterTexts=new List<string>();
			error="";
			//A byte order mark is not whitespace to Trim, so remove it first.
			List<string> listLines=(text??"").Replace("\uFEFF","").Replace("\r\n","\n").Replace("\r","\n").Split('\n').Select(x => x.Trim())
				.Where(x => x!="").ToList();
			if(listLines.Count==0 || !listLines[0].StartsWith(ExportHeaderPrefix,StringComparison.OrdinalIgnoreCase)) {
				error="The file does not start with a '"+ExportHeaderPrefix+"' line.";
				return false;
			}
			groupName=CleanGroupName(listLines[0].Substring(ExportHeaderPrefix.Length));
			if(groupName=="") {
				error="The file does not name a group.";
				return false;
			}
			for(int i=1;i<listLines.Count;i++) {
				string filterText=CleanFilterText(listLines[i]);
				if(filterText=="" || listFilterTexts.Contains(filterText,StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				listFilterTexts.Add(filterText);
			}
			if(listFilterTexts.Count==0) {
				error="The file holds no filters.";
				return false;
			}
			return true;
		}

		///<summary>The security log text for a changed group.</summary>
		public static string GetLogText(string groupName) {
			return "Query filter group '"+groupName+"' changed.";
		}
		#endregion Misc Methods
	}

	///<summary>Remembers each user's active Query Monitor filter group on this computer, in a small text file in the Windows user's local
	///application data folder. Keyed by database server, database name and UserNum, so the same user on two databases keeps two settings. Nothing
	///leaves the computer. A missing or unreadable file means no group is active.</summary>
	public class QueryFilterActiveGroups {
		///<summary>Overrides the file location; for tests. Null uses the default location.</summary>
		public static string FilePathOverride=null;
		private static readonly object _lock=new object();

		public static string GetFilePath() {
			if(!string.IsNullOrEmpty(FilePathOverride)) {
				return FilePathOverride;
			}
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenDental","QueryMonitorFilterGroups.txt");
		}

		///<summary>The key for the current connection and user.</summary>
		public static string GetKey(long userNum) {
			return GetKey(DataConnection.GetServerName(),DataConnection.GetDatabaseName(),userNum);
		}

		public static string GetKey(string serverName,string databaseName,long userNum) {
			return ((serverName??"")+"|"+(databaseName??"")+"|"+userNum).ToLowerInvariant();
		}

		///<summary>The active group for the key, or "" when none is set.</summary>
		public static string GetActiveGroup(string key) {
			lock(_lock) {
				Dictionary<string,string> dict=Read();
				return dict.TryGetValue(key??"",out string groupName) ? groupName : "";
			}
		}

		///<summary>Sets (or, with a blank group name, clears) the active group for the key. Returns false when the file could not be written.</summary>
		public static bool SetActiveGroup(string key,string groupName) {
			lock(_lock) {
				Dictionary<string,string> dict=Read();
				groupName=QueryFilters.CleanGroupName(groupName);
				if(groupName=="") {
					dict.Remove(key??"");
				}
				else {
					dict[key??""]=groupName;
				}
				try {
					string path=GetFilePath();
					Directory.CreateDirectory(Path.GetDirectoryName(path));
					StringBuilder stringBuilder=new StringBuilder();
					foreach(KeyValuePair<string,string> pair in dict.OrderBy(x => x.Key,StringComparer.Ordinal)) {
						stringBuilder.Append(pair.Key.Replace("\t"," ")+"\t"+pair.Value.Replace("\t"," ")+"\r\n");
					}
					File.WriteAllText(path,stringBuilder.ToString(),new UTF8Encoding(false));
					return true;
				}
				catch(Exception ex) {
					ex.DoNothing();
					return false;
				}
			}
		}

		private static Dictionary<string,string> Read() {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.Ordinal);
			try {
				string path=GetFilePath();
				if(!File.Exists(path)) {
					return dict;
				}
				foreach(string line in File.ReadAllLines(path,Encoding.UTF8)) {
					int index=line.IndexOf('\t');
					if(index<=0) {
						continue;
					}
					string groupName=line.Substring(index+1).Trim();
					if(groupName!="") {
						dict[line.Substring(0,index)]=groupName;
					}
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return dict;
		}
	}
}
