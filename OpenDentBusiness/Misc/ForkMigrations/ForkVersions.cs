// Fork-owned version markers and the fixed version numbers of the version guard (backlog Risk R1, package M1-03).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;
using DataConnectionBase;

namespace OpenDentBusiness {
	///<summary>The fork keeps two meanings of "version" apart. The release (product) version is our own scheme ("0.0", the
	///AssemblyInformationalVersion of OpenDental.exe) and is never compared with Open Dental numbers. The compatibility version is the Open Dental
	///line our legacy conversion chain ends at (24.3.63.0). Database state lives in four fork-owned preference rows, read and written here with plain
	///SQL so they need no PrefName entry and work before the preference cache is filled:
	///ForkSchemaLevel (last applied fork migration step), ForkProgramVersion (release and build last published from the update server),
	///OdSchemaCompat (the Open Dental schema the database matches) and ForkAdoptedFrom (the adoption record).</summary>
	public class ForkVersions {
		public const string PREF_FORK_SCHEMA_LEVEL="ForkSchemaLevel";
		public const string PREF_FORK_PROGRAM_VERSION="ForkProgramVersion";
		public const string PREF_OD_SCHEMA_COMPAT="OdSchemaCompat";
		public const string PREF_FORK_ADOPTED_FROM="ForkAdoptedFrom";
		///<summary>The fork schema level written at adoption, before any fork migration step has run.</summary>
		public const string SCHEMA_LEVEL_BASELINE="0";

		///<summary>The Open Dental version line of this code: the legacy ConvertDatabases chain is frozen here. Used for DataBaseVersion and
		///ProgramVersion on databases that the fork markers do not govern yet.</summary>
		public static readonly Version CompatVersion=new Version(24,3,63,0);
		///<summary>Written to DataBaseVersion and ProgramVersion at adoption. No Open Dental release will reach it, so an Open Dental client treats
		///the database as newer than itself: it skips its conversion, finds no update files, and does not use the database.</summary>
		public static readonly Version SentinelVersion=new Version(99,0,0,0);
		///<summary>The only Open Dental versions an unadopted database may have to be adopted: our own 24.3.63 line and the office's 25.3.48. Any other
		///version is refused until a schema diff proves it equal.</summary>
		public static readonly List<Version> ListAdoptableVersions=new List<Version>() {
			new Version(24,3,63,0),
			new Version(25,3,48,0),
		};

		///<summary>True if the version is on the adoption allowlist (exact match, all four parts).</summary>
		public static bool IsAdoptable(Version version) {
			return version!=null && ListAdoptableVersions.Contains(version);
		}

		///<summary>The release version of the running program: the AssemblyInformationalVersion of the entry assembly (OpenDental.exe), e.g. "0.0".
		///Falls back to the given assembly, then to "0.0".</summary>
		public static string GetReleaseVersion(Assembly assembly=null) {
			assembly=assembly??Assembly.GetEntryAssembly();
			if(assembly!=null) {
				AssemblyInformationalVersionAttribute attribute=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
				if(attribute!=null && !string.IsNullOrWhiteSpace(attribute.InformationalVersion)) {
					return attribute.InformationalVersion.Trim();
				}
			}
			return "0.0";
		}

		///<summary>Parses a version stored in a preference. Returns null for blank or malformed values instead of throwing.</summary>
		public static Version ParseVersion(string value) {
			Version version;
			if(string.IsNullOrWhiteSpace(value) || !Version.TryParse(value.Trim(),out version)) {
				return null;
			}
			//Stored versions always have four parts; treat missing parts as 0 so 25.3.48 and 25.3.48.0 compare equal.
			return new Version(version.Major,version.Minor,Math.Max(version.Build,0),Math.Max(version.Revision,0));
		}

		///<summary>Compares two release versions of our own scheme ("0.0", "0.1", "1.0.2"). Malformed values sort lowest. Returns &lt;0, 0 or &gt;0.
		///</summary>
		public static int CompareReleaseVersions(string releaseVersionA,string releaseVersionB) {
			Version versionA=ParseVersion(releaseVersionA);
			Version versionB=ParseVersion(releaseVersionB);
			if(versionA==null && versionB==null) {
				return 0;
			}
			if(versionA==null) {
				return -1;
			}
			if(versionB==null) {
				return 1;
			}
			return versionA.CompareTo(versionB);
		}

		#region Marker rows
		///<summary>The raw value of a preference row, or null when the row does not exist. Reads the database, not the cache.</summary>
		public static string GetMarker(string prefName) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),prefName);
			}
			DataTable table=Db.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"' ORDER BY PrefNum LIMIT 1");
			if(table.Rows.Count==0) {
				return null;
			}
			return PIn.String(table.Rows[0]["ValueString"].ToString());
		}

		///<summary>Inserts or updates a preference row with plain SQL. Direct connection only. Used for the fork markers and for the version
		///preferences at adoption.</summary>
		public static void SetMarker(string prefName,string value) {
			Meth.NoCheckMiddleTierRole();
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				throw new ApplicationException("Version markers can only be written over a direct database connection.");
			}
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString,Comments) VALUES ('"+POut.String(prefName)+"','"+POut.String(value)+"','')");
			}
			else {
				Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(prefName)+"'");
			}
		}

		///<summary>Reads the version preferences and the fork markers straight from the database.</summary>
		public static ForkDbState ReadState() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ForkDbState>(MethodBase.GetCurrentMethod());
			}
			Dictionary<string,string> dictValues=new Dictionary<string,string>();
			string command="SELECT PrefName,ValueString FROM preference WHERE PrefName IN ('DataBaseVersion','ProgramVersion','"
				+PREF_FORK_SCHEMA_LEVEL+"','"+PREF_FORK_PROGRAM_VERSION+"','"+PREF_OD_SCHEMA_COMPAT+"','"+PREF_FORK_ADOPTED_FROM+"') ORDER BY PrefNum";
			DataTable table=Db.GetTable(command);
			foreach(DataRow row in table.Rows) {
				string prefName=PIn.String(row["PrefName"].ToString());
				if(!dictValues.ContainsKey(prefName)) {
					dictValues[prefName]=PIn.String(row["ValueString"].ToString());
				}
			}
			ForkDbState forkDbState=new ForkDbState();
			forkDbState.DataBaseVersion=dictValues.TryGetValue("DataBaseVersion",out string value)?value:null;
			forkDbState.ProgramVersion=dictValues.TryGetValue("ProgramVersion",out value)?value:null;
			forkDbState.ForkSchemaLevel=dictValues.TryGetValue(PREF_FORK_SCHEMA_LEVEL,out value)?value:null;
			forkDbState.ForkProgramVersion=dictValues.TryGetValue(PREF_FORK_PROGRAM_VERSION,out value)?value:null;
			forkDbState.OdSchemaCompat=dictValues.TryGetValue(PREF_OD_SCHEMA_COMPAT,out value)?value:null;
			forkDbState.ForkAdoptedFrom=dictValues.TryGetValue(PREF_FORK_ADOPTED_FROM,out value)?value:null;
			return forkDbState;
		}
		#endregion Marker rows
	}

	///<summary>The version preferences and fork markers of one database. A null field means the preference row does not exist.</summary>
	[Serializable]
	public class ForkDbState {
		public string DataBaseVersion;
		public string ProgramVersion;
		public string ForkSchemaLevel;
		public string ForkProgramVersion;
		public string OdSchemaCompat;
		public string ForkAdoptedFrom;

		///<summary>True when the fork markers govern this database (it was adopted).</summary>
		public bool IsForkMarked {
			get { return ForkSchemaLevel!=null; }
		}

		///<summary>True when an adoption was started but did not finish (ForkAdoptedFrom says "started" and there is no ForkSchemaLevel yet).</summary>
		public bool IsAdoptionIncomplete {
			get { return !IsForkMarked && ForkAdoptedFrom!=null; }
		}

		public ForkDbState Copy() {
			return (ForkDbState)MemberwiseClone();
		}
	}
}
