using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>Seed rows that Open Dental's own 24.4 to 25.3 conversions would have written, for databases that come from 24.3 (a fresh install of
	///this fork, or a 24.3.63 database upgraded by it). A database that Open Dental already converted to 25.3 (the office database) has these rows,
	///or the office's own choices instead, so nothing here runs on it.
	///<para>Package M1-02 (docs/specs/port-backlog.md). The fork migration runner (M1-01) calls Run() once, after the 25.3.48 schema steps. Every
	///step is idempotent, so a second run changes nothing.</para>
	///<para>Only names confirmed by Open Dental's published 25.3 database documentation are seeded. Report names (displayreport.InternalName)
	///and program names (program.ProgName) for 24.4 to 25.3 are not in those documents, so no displayreport or program rows are seeded here;
	///see the M1-02 report in the backlog.</para></summary>
	public static class Seed25_3 {
		///<summary>Databases whose DataBaseVersion is lower than this come from 24.3 and get the seed rows.</summary>
		public static readonly Version VersionFirstAfter24_3=new Version(24,4);

		///<summary>Preference rows with their default values. Both names are confirmed by Open Dental's 25.3 database documentation.</summary>
		public static readonly List<(PrefName PrefName,string ValueString)> ListPrefDefaults=new List<(PrefName,string)> {
			(PrefName.ApptMirrorSecondary,"0"),
			(PrefName.TaskCategoryShow,"0"),
		};

		///<summary>Upgrade grants: each new permission goes to every user group that holds the existing permission, so nobody loses an ability
		///on upgrade. EnumPermType.None as the source means every user group. Rules from the 24.4, 25.1 and 25.2 feature specs.</summary>
		public static readonly List<(EnumPermType PermTypeNew,EnumPermType PermTypeFrom)> ListGrants=new List<(EnumPermType,EnumPermType)> {
			(EnumPermType.ImageSignatureCreate,EnumPermType.ImageEdit),//24.4, docs/specs/features-24.4.md
			(EnumPermType.SignedImageEdit,EnumPermType.ImageEdit),//24.4
			(EnumPermType.BlockoutsFlagged,EnumPermType.None),//25.1: in 24.3 anyone could manage flagged blockouts.
			(EnumPermType.PayPlanUnlock,EnumPermType.PayPlanEdit),//25.1
			(EnumPermType.ProcTPEditFee,EnumPermType.ProcExistingEdit),//25.2 (our choice of source permission)
		};

		///<summary>True if the stored DataBaseVersion says the database comes from 24.3 or earlier. A blank or unreadable version returns false, so
		///an unknown database is never seeded.</summary>
		public static bool IsVersionFrom24_3(string dataBaseVersion) {
			if(string.IsNullOrWhiteSpace(dataBaseVersion)) {
				return false;
			}
			if(!Version.TryParse(dataBaseVersion.Trim(),out Version version)) {
				return false;
			}
			return version<VersionFirstAfter24_3;
		}

		///<summary>Reads DataBaseVersion straight from the database (not the cache).</summary>
		public static bool IsFrom24_3() {
			string dataBaseVersion=DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'");
			return IsVersionFrom24_3(dataBaseVersion);
		}

		///<summary>Runs the seed steps when the database comes from 24.3. Returns one line per change made, or a single line saying why it was skipped.</summary>
		public static List<string> Run() {
			if(!IsFrom24_3()) {
				return new List<string> { "Seed25_3 skipped: the database does not come from 24.3." };
			}
			return RunSteps();
		}

		///<summary>Runs every seed step without the origin check. Each step checks for itself whether it still has anything to do.</summary>
		public static List<string> RunSteps() {
			List<string> listChanges=new List<string>();
			for(int i=0;i<ListPrefDefaults.Count;i++) {
				if(InsertPrefIfMissing(ListPrefDefaults[i].PrefName,ListPrefDefaults[i].ValueString)) {
					listChanges.Add("Inserted preference "+ListPrefDefaults[i].PrefName);
				}
			}
			for(int i=0;i<ListGrants.Count;i++) {
				int count=GrantIfNeverConfigured(ListGrants[i].PermTypeNew,ListGrants[i].PermTypeFrom);
				if(count>0) {
					listChanges.Add("Granted "+ListGrants[i].PermTypeNew+" to "+count+" user group(s)");
				}
			}
			long countEForms=NormalizeRemovedEFormStatuses();
			if(countEForms>0) {
				listChanges.Add("Set "+countEForms+" eform row(s) with a removed status to Filled");
			}
			return listChanges;
		}

		///<summary>Inserts the preference row when no row with that name exists. Returns true if a row was inserted. Never changes an existing value.</summary>
		public static bool InsertPrefIfMissing(PrefName prefName,string valueString) {
			string command="SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName.ToString())+"'";
			if(PIn.Long(DataCore.GetScalar(command))>0) {
				return false;
			}
			command="INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(prefName.ToString())+"','"+POut.String(valueString)+"')";
			DataCore.NonQ(command);
			return true;
		}

		///<summary>Grants permTypeNew (FKey 0, no date limits) to every user group that holds permTypeFrom, or to every user group when
		///permTypeFrom is None. Does nothing when any grouppermission row for permTypeNew already exists: then the permission has been set up
		///already and an administrator's choices must not be overwritten. Returns the number of rows inserted.</summary>
		public static int GrantIfNeverConfigured(EnumPermType permTypeNew,EnumPermType permTypeFrom) {
			if(GroupPermissions.IsReserved(permTypeNew)) {
				throw new ArgumentException("Reserved permissions are never granted: "+permTypeNew);
			}
			string command="SELECT COUNT(*) FROM grouppermission WHERE PermType="+POut.Int((int)permTypeNew);
			if(PIn.Long(DataCore.GetScalar(command))>0) {
				return 0;
			}
			if(permTypeFrom==EnumPermType.None) {
				command="SELECT UserGroupNum FROM usergroup ORDER BY UserGroupNum";
			}
			else {
				command="SELECT DISTINCT UserGroupNum FROM grouppermission WHERE PermType="+POut.Int((int)permTypeFrom)+" ORDER BY UserGroupNum";
			}
			List<long> listUserGroupNums=DataCore.GetTable(command).Select().Select(x => PIn.Long(x[0].ToString())).Distinct().ToList();
			for(int i=0;i<listUserGroupNums.Count;i++) {
				command="INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('0001-01-01',0,"
					+POut.Long(listUserGroupNums[i])+","+POut.Int((int)permTypeNew)+",0)";
				DataCore.NonQ(command);
			}
			return listUserGroupNums.Count;
		}

		///<summary>Open Dental 25.1 and 25.2 removed eform.Status 3 (Imported) and 4 (UnmatchedToPatient). Rows that still hold them become 2
		///(Filled). Returns the number of rows changed.</summary>
		public static long NormalizeRemovedEFormStatuses() {
			string listValues=string.Join(",",EFormStatusCompat.ArrayRemovedValues.Select(x => x.ToString(CultureInfo.InvariantCulture)));
			string command="SELECT COUNT(*) FROM eform WHERE Status IN ("+listValues+")";
			long count=PIn.Long(DataCore.GetScalar(command));
			if(count==0) {
				return 0;
			}
			command="UPDATE eform SET Status="+POut.Int((int)EnumEFormStatus.Filled)+" WHERE Status IN ("+listValues+")";
			DataCore.NonQ(command);
			return count;
		}
	}
}
