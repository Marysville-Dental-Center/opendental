// Seed rows for the Open Dental 25.4 and 26.1 registries (package M2-02 in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

//Not in OpenDentBusiness.ForkMigrations: that namespace holds the 25.3.48 schema steps and Seed25_3Step, which must come first
//(HandOffIntegrationTests). The runner finds steps in any namespace.
namespace OpenDentBusiness.ForkMigrations.Seeds26_1 {
	///<summary>Rows that the 25.4 and 26.1 registries need in the database.
	///<para>1. Preference rows for the 25.4 and 26.1 preferences, with their defaults, on every database that lacks them. The names are this fork's
	///own (Open Dental does not publish preference names), so no Open Dental database has them; an existing row is never changed.</para>
	///<para>2. The 25.4 blockout permission split, only on a database that comes from before 25.4 (24.3, or an Open Dental 25.3 database adopted by
	///this fork): first the Open Dental 25.3 PhoneExtension grants move from 272 to 274, where 25.4 put that value, then every user group that holds
	///Blockouts (13, now Blockout Edit) also gets BlockoutAdd (272) and BlockoutDelete (273) with the same date limits, so nobody loses an ability.
	///This is the fork's conversion; Open Dental's own is not public.</para></summary>
	public static class Seed26_1 {
		///<summary>Databases whose source version is lower than this get the permission split.</summary>
		public static readonly Version VersionFirst25_4=new Version(25,4);
		///<summary>EnumPermType.PhoneExtension as Open Dental 25.3 stored it. 25.4 gave this number to BlockoutAdd.</summary>
		public const int PERM_PHONE_EXTENSION_25_3=272;

		///<summary>Default text of the e-mail sent when a recurring charge fails (written for this fork).</summary>
		public const string RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT="We could not process your recurring payment";
		public const string RECURRING_FAIL_EMAIL_BODY_DEFAULT="Hello [NameF],\r\n\r\nThe recurring charge of [Amount] to your card ending in [CardLast4] "
			+"on [DateCharge] did not go through. Please call [OfficeName] at [OfficePhone] to update your payment information.\r\n\r\nThank you.";

		///<summary>The 25.4 and 26.1 preferences and their defaults. The defaults keep 24.3 behavior.</summary>
		public static readonly List<(PrefName PrefName,string ValueString)> ListPrefDefaults=new List<(PrefName,string)> {
			//25.4
			(PrefName.IncomeTransfersLimitToPatPayments,"0"),
			(PrefName.RecurringChargesFailEmailEnabled,"0"),
			(PrefName.RecurringChargesFailEmailSubject,RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT),
			(PrefName.RecurringChargesFailEmailBody,RECURRING_FAIL_EMAIL_BODY_DEFAULT),
			(PrefName.UdsPlusDentalLineCodes,""),
			//26.1
			(PrefName.RepeatingChargesAllowBackdating,"1"),
			(PrefName.ImagingAllowPdfJavascript,"0"),
			(PrefName.AppointmentMakeRecallSearchesOpenings,"1"),
			(PrefName.BillingEmailType,"0"),
			(PrefName.BillingDefaultServiceDateView,"0"),
		};

		///<summary>The 25.4 upgrade grants: each new permission goes to every user group that holds the existing one.</summary>
		public static readonly List<(EnumPermType PermTypeNew,EnumPermType PermTypeFrom)> ListGrants=new List<(EnumPermType,EnumPermType)> {
			(EnumPermType.BlockoutAdd,EnumPermType.Blockouts),
			(EnumPermType.BlockoutDelete,EnumPermType.Blockouts),
		};

		///<summary>True if the version is lower than 25.4. A blank or unreadable version returns false, so an unknown database is never converted.</summary>
		public static bool IsVersionBefore25_4(string version) {
			if(string.IsNullOrWhiteSpace(version)) {
				return false;
			}
			if(!Version.TryParse(version.Trim(),out Version versionParsed)) {
				return false;
			}
			return versionParsed<VersionFirst25_4;
		}

		///<summary>Inserts every missing preference row with its default. Returns one line per row inserted.</summary>
		public static List<string> InsertMissingPrefs() {
			List<string> listChanges=new List<string>();
			for(int i=0;i<ListPrefDefaults.Count;i++) {
				if(Seed25_3.InsertPrefIfMissing(ListPrefDefaults[i].PrefName,ListPrefDefaults[i].ValueString)) {
					listChanges.Add("Inserted preference "+ListPrefDefaults[i].PrefName);
				}
			}
			return listChanges;
		}

		///<summary>The permission split for a database from before 25.4: renumbers the 25.3 PhoneExtension rows, then grants. Returns one line per
		///change. Call it once per database (Seed26_1Step does); the renumbering must not run after 272 means BlockoutAdd.</summary>
		public static List<string> RunPermissionSplit() {
			List<string> listChanges=new List<string>();
			long countRenumbered=RenumberPhoneExtension25_3();
			if(countRenumbered>0) {
				listChanges.Add("Moved "+countRenumbered+" Open Dental 25.3 PhoneExtension grant(s) from 272 to "+(int)EnumPermType.PhoneExtension);
			}
			long countLogs=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+POut.Int(PERM_PHONE_EXTENSION_25_3)));
			if(countLogs>0) {
				//Audit rows are hashed and never edited. Open Dental HQ wrote them; an office database should have none.
				listChanges.Add(countLogs+" securitylog row(s) hold 272 (PhoneExtension before 25.4); they are left unchanged");
			}
			for(int i=0;i<ListGrants.Count;i++) {
				int count=GrantIfNeverConfigured(ListGrants[i].PermTypeNew,ListGrants[i].PermTypeFrom);
				if(count>0) {
					listChanges.Add("Granted "+ListGrants[i].PermTypeNew+" to "+count+" user group(s)");
				}
			}
			return listChanges;
		}

		///<summary>Moves grouppermission rows from 272 (PhoneExtension in Open Dental 25.3) to 274 (PhoneExtension from 25.4). Returns the number of rows
		///moved. Only for a database from before 25.4, before any BlockoutAdd grant exists.</summary>
		public static long RenumberPhoneExtension25_3() {
			string command="SELECT COUNT(*) FROM grouppermission WHERE PermType="+POut.Int(PERM_PHONE_EXTENSION_25_3);
			long count=PIn.Long(DataCore.GetScalar(command));
			if(count==0) {
				return 0;
			}
			command="UPDATE grouppermission SET PermType="+POut.Int((int)EnumPermType.PhoneExtension)+" WHERE PermType="+POut.Int(PERM_PHONE_EXTENSION_25_3);
			DataCore.NonQ(command);
			return count;
		}

		///<summary>Gives permTypeNew to every user group that holds permTypeFrom, copying each source row's FKey, NewerDate and NewerDays. Does nothing
		///when any grouppermission row for permTypeNew already exists, so an administrator's choices are never overwritten. Returns the number of rows
		///inserted.</summary>
		public static int GrantIfNeverConfigured(EnumPermType permTypeNew,EnumPermType permTypeFrom) {
			if(GroupPermissions.IsReserved(permTypeNew)) {
				throw new ArgumentException("Reserved permissions are never granted: "+permTypeNew);
			}
			string command="SELECT COUNT(*) FROM grouppermission WHERE PermType="+POut.Int((int)permTypeNew);
			if(PIn.Long(DataCore.GetScalar(command))>0) {
				return 0;
			}
			command="SELECT UserGroupNum,FKey,NewerDate,NewerDays FROM grouppermission WHERE PermType="+POut.Int((int)permTypeFrom)
				+" ORDER BY UserGroupNum,GroupPermNum";
			List<(long UserGroupNum,long FKey,DateTime NewerDate,int NewerDays)> listSources=DataCore.GetTable(command).Select()
				.Select(x => (PIn.Long(x[0].ToString()),PIn.Long(x[1].ToString()),PIn.Date(x[2].ToString()),PIn.Int(x[3].ToString())))
				.GroupBy(x => (x.Item1,x.Item2))//One row per user group and FKey.
				.Select(x => x.First())
				.ToList();
			for(int i=0;i<listSources.Count;i++) {
				command="INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES("
					+POut.Date(listSources[i].NewerDate)+","+POut.Int(listSources[i].NewerDays)+","+POut.Long(listSources[i].UserGroupNum)+","
					+POut.Int((int)permTypeNew)+","+POut.Long(listSources[i].FKey)+")";
				DataCore.NonQ(command);
			}
			return listSources.Count;
		}
	}
}
