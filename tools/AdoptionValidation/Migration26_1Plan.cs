// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration's Open Dental 25.4 and 26.1 steps (backlog packages M2-01 and M2-02) change when adoption brings an Open Dental
	///25.3.48 database to 26.1.70, worked out before adoption and read-only. migration-preview prints it (runbook step 0.2), and adopt's row check
	///(AdoptionRowCheck) holds the adopted copy to the plan made from the original.
	///- The schema steps (2026102401 to 2026102403) add the tables and columns that the 26.1.70 reference has and the 25.3.48 reference lacks, and
	///  change the definitions of a few columns (GetSchemaChanges, from the two embedded references). New tables stay empty and new columns hold their
	///  default in every existing row (evaluation.GradeOverride: -1, its default). Of the changed definitions only two change stored values:
	///  claimpayment.Note becomes text NOT NULL, so a NULL note becomes an empty string, and insverify and insverifyhist.DateLastAssigned become
	///  datetime, so each date reads back at midnight.
	///- The seed step (Seed26_1Step, 2026102501) inserts the 25.4 and 26.1 preferences that are missing, with their defaults (Seed26_1.ListPrefDefaults),
	///  and, on a database from before 25.4 that has not had the step yet, splits the blockout permission: the Open Dental 25.3 PhoneExtension grants
	///  move from 272 to 274, then every user group with a Blockouts grant gets BlockoutAdd and BlockoutDelete with the same FKey and date limits, unless
	///  that permission is already configured (Seed26_1.RunPermissionSplit). This class repeats the step's rules on plain SQL reads, from the step's own
	///  lists and constants; the rehearsal checks that the plan equals what the step did (check M4).
	///Reads the preference names and the version and fork migration preferences, grouppermission, and whether claimpayment notes are NULL; never
	///securitylog (the step only counts its 272 rows, and the table can be large).</summary>
	public static class Migration26_1Plan {
		///<summary>Level of the seed step, for messages.</summary>
		public static long StepSeed {
			get { return new Seed26_1Step().Level; }
		}

		///<summary>The columns whose definition the 25.4 and 26.1 steps change and whose stored values change with it, as table.column. AdoptionRowCheck
		///checks each row by row; allowed-differences.adoption.json allows them as whole columns.</summary>
		public static readonly string[] ArrayValueChangingColumns={ "claimpayment.Note","insverify.DateLastAssigned","insverifyhist.DateLastAssigned" };

		///<summary>A grouppermission row as the seed step reads it.</summary>
		public class GroupPermRow {
			public long GroupPermNum;
			public long UserGroupNum;
			public int PermType;
			public long FKey;
			public DateTime NewerDate;
			public int NewerDays;

			///<summary>What a granted copy of this row must hold, apart from its new key.</summary>
			public string Describe(int permType) {
				return "user group "+UserGroupNum.ToString(CultureInfo.InvariantCulture)+", PermType "+permType.ToString(CultureInfo.InvariantCulture)
					+", FKey "+FKey.ToString(CultureInfo.InvariantCulture)+", NewerDate "+NewerDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)
					+", NewerDays "+NewerDays.ToString(CultureInfo.InvariantCulture);
			}
		}

		///<summary>One upgrade grant of Seed26_1.ListGrants.</summary>
		public class GrantPlan {
			public EnumPermType PermTypeNew;
			public EnumPermType PermTypeFrom;
			///<summary>True when the database already has a grant of PermTypeNew (after the 272 move): the step then grants nothing.</summary>
			public bool IsAlreadyConfigured;
			///<summary>The PermTypeFrom rows that are copied, one per user group and FKey, in the step's order.</summary>
			public List<GroupPermRow> ListSources=new List<GroupPermRow>();

			public int CountGranted {
				get { return IsAlreadyConfigured ? 0 : ListSources.Count; }
			}
		}

		///<summary>What the 25.4 and 26.1 steps change in the database's rows, and in its schema.</summary>
		public class Plan {
			public string Database;
			///<summary>True when the database comes from before Open Dental 25.4, judged as the seed step judges it.</summary>
			public bool IsFromBefore25_4;
			///<summary>True when the seed step already ran on this database (an adopted database): the permission split never runs twice.</summary>
			public bool IsSeedStepApplied;
			///<summary>The 25.4 and 26.1 preferences the database lacks, which the step inserts with their defaults.</summary>
			public List<string> ListPrefsInserted=new List<string>();
			///<summary>The grouppermission rows with 272 (PhoneExtension in Open Dental 25.3) that move to 274.</summary>
			public List<GroupPermRow> ListRenumbered=new List<GroupPermRow>();
			public List<GrantPlan> ListGrants=new List<GrantPlan>();
			///<summary>claimpayment rows whose Note is NULL; they become empty strings. 0 when the column is already NOT NULL.</summary>
			public long CountClaimPaymentNotesNull;
			public SchemaChanges Schema=GetSchemaChanges();

			public bool IsPermissionSplitPlanned {
				get { return IsFromBefore25_4 && !IsSeedStepApplied; }
			}

			///<summary>What migration-preview prints for the 25.4 and 26.1 steps.</summary>
			public List<string> Lines() {
				List<string> listLines=new List<string>();
				listLines.Add("Steps 2026102401 to 2026102403 (Open Dental 25.4 and 26.1 schema) bring the schema to Open Dental "+SchemaVerifier.ReferenceVersion+": "
					+Schema.ListTablesAdded.Count+" new table(s), empty ("+string.Join(", ",Schema.ListTablesAdded)+"); "+Schema.ListColumnsAdded.Count
					+" new column(s) in "+Schema.ListColumnsAdded.Select(x => x.Split('.')[0]).Distinct().Count()+" table(s), each holding its default in every "
					+"existing row (evaluation.GradeOverride -1); "+Schema.ListColumnsChanged.Count+" column definition(s) changed ("
					+string.Join(", ",Schema.ListColumnsChanged)+"), of which only claimpayment.Note (text NOT NULL: "+CountClaimPaymentNotesNull
					+" NULL note(s) become empty) and insverify and insverifyhist.DateLastAssigned (datetime: the same dates at midnight) change stored values.");
				listLines.Add("Step "+StepSeed+" (Open Dental 25.4 and 26.1 seed rows) inserts "+ListPrefsInserted.Count+" preference(s) with their defaults"
					+(ListPrefsInserted.Count==0 ? "." : ": "+string.Join(", ",ListPrefsInserted)+"."));
				if(!IsPermissionSplitPlanned) {
					listLines.Add("Step "+StepSeed+" changes no permission: "+(IsSeedStepApplied ? "it already ran on this database." : "this database does not come from "
						+"before Open Dental 25.4."));
				}
				else {
					listLines.Add("Step "+StepSeed+" splits the blockout permission (this database comes from before Open Dental 25.4): it moves "+ListRenumbered.Count
						+" Open Dental 25.3 PhoneExtension grant(s) from 272 to "+(int)EnumPermType.PhoneExtension+", then "
						+string.Join(" and ",ListGrants.Select(x => x.IsAlreadyConfigured ? x.PermTypeNew+" is already configured, so it is not granted"
							: "grants "+x.PermTypeNew+" ("+(int)x.PermTypeNew+") to "+x.ListSources.Count+" user group(s)"))
						+", with the FKey and date limits of their "+EnumPermType.Blockouts+" grant.");
				}
				listLines.Add("adopt's row check (step 4) requires exactly these changes; the adoption log names the inserted preferences and the moved and "
					+"granted permissions.");
				return listLines;
			}
		}

		///<summary>The difference between the 25.3.48 and the current (26.1.70) reference: what the 25.4 and 26.1 schema steps change.</summary>
		public class SchemaChanges {
			public List<string> ListTablesAdded=new List<string>();
			///<summary>New columns of tables both references have, as table.column.</summary>
			public List<string> ListColumnsAdded=new List<string>();
			///<summary>Columns both references have with different definitions, as table.column.</summary>
			public List<string> ListColumnsChanged=new List<string>();
		}

		private static readonly Lazy<SchemaChanges> _lazySchemaChanges=new Lazy<SchemaChanges>(ReadSchemaChanges);

		///<summary>The schema changes, from the two references embedded in OpenDentBusiness (no database). Read once.</summary>
		public static SchemaChanges GetSchemaChanges() {
			return _lazySchemaChanges.Value;
		}

		private static SchemaChanges ReadSchemaChanges() {
			SchemaSnapshot snapshotFrom=SchemaVerifier.GetReference25_3_48();
			SchemaSnapshot snapshotTo=SchemaVerifier.GetReference();
			SchemaChanges changes=new SchemaChanges();
			foreach(SchemaTable tableTo in snapshotTo.Tables.OrderBy(x => x.Name,StringComparer.Ordinal)) {
				SchemaTable tableFrom=snapshotFrom.GetTable(tableTo.Name);
				if(tableFrom==null) {
					changes.ListTablesAdded.Add(tableTo.Name);
					continue;
				}
				foreach(SchemaColumn columnTo in tableTo.Columns) {
					SchemaColumn columnFrom=tableFrom.GetColumn(columnTo.Name);
					if(columnFrom==null) {
						changes.ListColumnsAdded.Add(tableTo.Name+"."+columnTo.Name);
					}
					else if(columnFrom.Definition!=columnTo.Definition) {
						changes.ListColumnsChanged.Add(tableTo.Name+"."+columnTo.Name);
					}
				}
			}
			return changes;
		}

		///<summary>The plan for a database on this server, read with plain SQL.</summary>
		public static Plan Make(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			Plan plan=new Plan { Database=database };
			if(!HasTable(db,database,"preference")) {
				return plan;
			}
			ForkDbState state=HeadlessAdoption.ReadState(db,database);
			if(state.ForkAdoptedFrom!=null) {
				Version versionSource=Adoption.ParseAdoptedFromSource(state.ForkAdoptedFrom);
				plan.IsFromBefore25_4=versionSource!=null && versionSource<Seed26_1.VersionFirst25_4;
			}
			else {
				plan.IsFromBefore25_4=Seed26_1.IsVersionBefore25_4(state.DataBaseVersion);
			}
			string applied=Convert.ToString(db.Scalar("SELECT ValueString FROM `"+database+"`.preference WHERE PrefName="
				+SqlLiteral.Quote(ForkMigrationRunner.PrefNameApplied)+" ORDER BY PrefNum LIMIT 1"))??"";
			plan.IsSeedStepApplied=applied.Split(',').Any(x => x.Trim()==StepSeed.ToString(CultureInfo.InvariantCulture));
			HashSet<string> setPrefNames=new HashSet<string>(db.Rows("SELECT PrefName FROM `"+database+"`.preference").Select(x => Convert.ToString(x[0])),
				StringComparer.OrdinalIgnoreCase);
			plan.ListPrefsInserted=Seed26_1.ListPrefDefaults.Select(x => x.PrefName.ToString()).Where(x => !setPrefNames.Contains(x)).ToList();
			if(HasTable(db,database,"claimpayment")) {
				plan.CountClaimPaymentNotesNull=db.ScalarLong("SELECT COUNT(*) FROM `"+database+"`.claimpayment WHERE Note IS NULL");
			}
			if(!plan.IsPermissionSplitPlanned || !HasTable(db,database,"grouppermission")) {
				return plan;
			}
			plan.ListRenumbered=ReadGroupPerms(db,database,Seed26_1.PERM_PHONE_EXTENSION_25_3);
			foreach((EnumPermType permTypeNew,EnumPermType permTypeFrom) grant in Seed26_1.ListGrants) {
				GrantPlan grantPlan=new GrantPlan { PermTypeNew=grant.permTypeNew,PermTypeFrom=grant.permTypeFrom };
				//The step looks after the 272 move, so the moved rows no longer count as grants of 272.
				long countExisting=db.ScalarLong("SELECT COUNT(*) FROM `"+database+"`.grouppermission WHERE PermType="+((int)grant.permTypeNew).ToString(CultureInfo.InvariantCulture))
					-((int)grant.permTypeNew==Seed26_1.PERM_PHONE_EXTENSION_25_3 ? plan.ListRenumbered.Count : 0);
				//A grant earlier in the list of the same permission counts too (there is none today).
				countExisting+=plan.ListGrants.Where(x => x.PermTypeNew==grant.permTypeNew).Sum(x => x.CountGranted);
				grantPlan.IsAlreadyConfigured=countExisting>0;
				//One row per user group and FKey: the first by GroupPermNum, as the step reads them.
				grantPlan.ListSources=ReadGroupPerms(db,database,(int)grant.permTypeFrom)
					.GroupBy(x => (x.UserGroupNum,x.FKey)).Select(x => x.First()).ToList();
				plan.ListGrants.Add(grantPlan);
			}
			return plan;
		}

		///<summary>The grouppermission rows of one permission, ordered as the seed step reads them (UserGroupNum, then GroupPermNum).</summary>
		public static List<GroupPermRow> ReadGroupPerms(Db db,string database,int permType) {
			LocalOnly.EnsureSafeIdentifier(database);
			return db.Rows("SELECT GroupPermNum,UserGroupNum,PermType,FKey,NewerDate,NewerDays FROM `"+database+"`.grouppermission WHERE PermType="
				+permType.ToString(CultureInfo.InvariantCulture)+" ORDER BY UserGroupNum,GroupPermNum").Select(ToGroupPermRow).ToList();
		}

		public static GroupPermRow ToGroupPermRow(object[] row) {
			return new GroupPermRow {
				GroupPermNum=Convert.ToInt64(row[0],CultureInfo.InvariantCulture),
				UserGroupNum=Convert.ToInt64(row[1],CultureInfo.InvariantCulture),
				PermType=Convert.ToInt32(row[2],CultureInfo.InvariantCulture),
				FKey=Convert.ToInt64(row[3],CultureInfo.InvariantCulture),
				NewerDate=row[4] is DateTime date ? date.Date : DateTime.MinValue,
				NewerDays=Convert.ToInt32(row[5],CultureInfo.InvariantCulture),
			};
		}

		public static bool HasTable(Db db,string database,string table) {
			return db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_NAME="
				+SqlLiteral.Quote(table))>0;
		}
	}
}
