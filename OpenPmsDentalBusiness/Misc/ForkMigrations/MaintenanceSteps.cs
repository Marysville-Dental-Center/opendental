// Routine maintenance (docs/specs/routine-maintenance.md): the five fork-only tables and the steps that create them (levels 2026110201 to
// 2026110205), the grant of the two permissions to the Security Admin groups (2026110206) and the correction columns of maintrecord
// (2026110207). 2026110208 and 2026110209 stay free for this feature.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>The routine maintenance tables, exactly as SHOW CREATE TABLE prints them on MariaDB 10.11. Not Open Dental schema: the schema
	///verifier allows them as extra tables and ForkTables.VerifyDatabase checks them. The steps below create them empty and write no row into them,
	///so on an adopted database they change nothing else (the grant step writes grouppermission rows; see MaintPermissionGrants).</summary>
	public class MaintenanceTables:ForkTableSet {
		public const string TABLE_ITEM="maintitem";
		public const string TABLE_STEP="maintstep";
		public const string TABLE_FIELD="maintfield";
		public const string TABLE_RECORD="maintrecord";
		public const string TABLE_RECORD_VALUE="maintrecordvalue";

		public const string CREATE_ITEM=@"CREATE TABLE `maintitem` (
  `MaintItemNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ItemName` varchar(255) NOT NULL,
  `Description` text NOT NULL,
  `EquipmentNum` bigint(20) NOT NULL,
  `ScheduleType` tinyint(4) NOT NULL,
  `IntervalCount` int(11) NOT NULL,
  `IntervalUnit` tinyint(4) NOT NULL,
  `Weekdays` int(11) NOT NULL,
  `DateAnchor` date NOT NULL DEFAULT '0001-01-01',
  `LeadDays` int(11) NOT NULL,
  `UserNumAssigned` bigint(20) NOT NULL,
  `UserGroupNumAssigned` bigint(20) NOT NULL,
  `IsHidden` tinyint(4) NOT NULL,
  `SecUserNumEntry` bigint(20) NOT NULL,
  `SecDateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `SecDateTEdit` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`MaintItemNum`),
  KEY `EquipmentNum` (`EquipmentNum`),
  KEY `UserNumAssigned` (`UserNumAssigned`),
  KEY `UserGroupNumAssigned` (`UserGroupNumAssigned`),
  KEY `SecUserNumEntry` (`SecUserNumEntry`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_STEP=@"CREATE TABLE `maintstep` (
  `MaintStepNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  `Description` varchar(255) NOT NULL,
  PRIMARY KEY (`MaintStepNum`),
  KEY `MaintItemNum` (`MaintItemNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_FIELD=@"CREATE TABLE `maintfield` (
  `MaintFieldNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  `FieldName` varchar(255) NOT NULL,
  `FieldType` tinyint(4) NOT NULL,
  `Unit` varchar(255) NOT NULL,
  `HasMin` tinyint(4) NOT NULL,
  `ValueMin` double NOT NULL,
  `HasMax` tinyint(4) NOT NULL,
  `ValueMax` double NOT NULL,
  `IsRequired` tinyint(4) NOT NULL,
  PRIMARY KEY (`MaintFieldNum`),
  KEY `MaintItemNum` (`MaintItemNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_RECORD=@"CREATE TABLE `maintrecord` (
  `MaintRecordNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `DateDone` date NOT NULL DEFAULT '0001-01-01',
  `DateDue` date NOT NULL DEFAULT '0001-01-01',
  `UserNum` bigint(20) NOT NULL,
  `Note` text NOT NULL,
  `IsFlagged` tinyint(4) NOT NULL,
  `SecDateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `UserNumCorrected` bigint(20) NOT NULL,
  `DateTCorrected` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`MaintRecordNum`),
  KEY `MaintItemNum` (`MaintItemNum`),
  KEY `UserNum` (`UserNum`),
  KEY `DateDone` (`DateDone`),
  KEY `UserNumCorrected` (`UserNumCorrected`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_RECORD_VALUE=@"CREATE TABLE `maintrecordvalue` (
  `MaintRecordValueNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintRecordNum` bigint(20) NOT NULL,
  `MaintStepNum` bigint(20) NOT NULL,
  `MaintFieldNum` bigint(20) NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  `FieldType` tinyint(4) NOT NULL,
  `Label` varchar(255) NOT NULL,
  `Unit` varchar(255) NOT NULL,
  `ValueText` text NOT NULL,
  `IsFlagged` tinyint(4) NOT NULL,
  PRIMARY KEY (`MaintRecordValueNum`),
  KEY `MaintRecordNum` (`MaintRecordNum`),
  KEY `MaintStepNum` (`MaintStepNum`),
  KEY `MaintFieldNum` (`MaintFieldNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public override string Feature {
			get { return "Routine maintenance"; }
		}

		public override string Levels {
			get { return "2026110201 to 2026110205 and 2026110207"; }
		}

		public override string[] ArrayCreateTables {
			get { return new[] { CREATE_ITEM,CREATE_STEP,CREATE_FIELD,CREATE_RECORD,CREATE_RECORD_VALUE }; }
		}
	}

	///<summary>Routine maintenance: the maintenance items the office defines. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110201,"Routine maintenance: add the maintitem table")]
	public class MaintItemTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_ITEM);
		}
	}

	///<summary>Routine maintenance: the checklist steps of each item. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110202,"Routine maintenance: add the maintstep table")]
	public class MaintStepTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_STEP);
		}
	}

	///<summary>Routine maintenance: the result fields of each item. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110203,"Routine maintenance: add the maintfield table")]
	public class MaintFieldTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_FIELD);
		}
	}

	///<summary>Routine maintenance: each recorded completion. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110204,"Routine maintenance: add the maintrecord table")]
	public class MaintRecordTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_RECORD);
		}
	}

	///<summary>Routine maintenance: the checklist ticks and results of each completion. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110205,"Routine maintenance: add the maintrecordvalue table")]
	public class MaintRecordValueTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_RECORD_VALUE);
		}
	}

	///<summary>Routine maintenance (docs/specs/routine-maintenance.md, section 7): Maintenance Setup and Maintenance Record Completion for every user
	///group that has Security Admin, owner decision of 2026-11-02. Plan works out the grouppermission rows to insert without writing; the step
	///(MaintPermissionGrantStep) inserts them, and AdoptionValidation's migration-preview and adopt's row check (MaintenanceGrantPlan) use Plan too,
	///so the three never disagree.</summary>
	public static class MaintPermissionGrants {
		///<summary>The permissions granted, in the order they are inserted for each user group.</summary>
		public static readonly EnumPermType[] ArrayPermTypes={ EnumPermType.MaintenanceSetup,EnumPermType.MaintenanceRecord };

		///<summary>One grouppermission row to insert. It is written as the other grant steps write theirs (Seed25_3, Seed26_1): FKey 0, NewerDate
		///0001-01-01, NewerDays 0.</summary>
		public class Grant {
			public long UserGroupNum;
			public EnumPermType PermType;
		}

		///<summary>The rows to insert, given every grouppermission row of the database as (UserGroupNum, PermType): for each user group that has a
		///Security Admin row, in UserGroupNum order, each permission of ArrayPermTypes that the group has no row of (whatever that row's FKey or date
		///limits). Nothing for any other group, and nothing when every Security Admin group has both already.</summary>
		public static List<Grant> Plan(List<(long UserGroupNum,int PermType)> listRows) {
			List<Grant> listGrants=new List<Grant>();
			List<long> listUserGroupNumsAdmin=listRows.Where(x => x.PermType==(int)EnumPermType.SecurityAdmin).Select(x => x.UserGroupNum)
				.Distinct().OrderBy(x => x).ToList();
			HashSet<(long,int)> setExisting=new HashSet<(long,int)>(listRows.Select(x => (x.UserGroupNum,x.PermType)));
			foreach(long userGroupNum in listUserGroupNumsAdmin) {
				foreach(EnumPermType permType in ArrayPermTypes) {
					if(setExisting.Contains((userGroupNum,(int)permType))) {
						continue;
					}
					listGrants.Add(new Grant { UserGroupNum=userGroupNum,PermType=permType });
				}
			}
			return listGrants;
		}

		///<summary>Every grouppermission row of the connected database as (UserGroupNum, PermType), read straight from the table (not the cache).
		///</summary>
		public static List<(long UserGroupNum,int PermType)> ReadRows() {
			DataTable table=DataCore.GetTable("SELECT UserGroupNum,PermType FROM grouppermission");
			return table.Select().Select(x => (PIn.Long(x[0].ToString()),PIn.Int(x[1].ToString()))).ToList();
		}

		///<summary>The log line of one permission's grants, as the step logs it: "Granted MaintenanceSetup to 2 Security Admin user group(s)".</summary>
		public static string Describe(EnumPermType permType,int count) {
			return "Granted "+permType+" to "+count+" Security Admin user group(s)";
		}

		///<summary>Inserts the planned rows into the connected database. Returns one log line per permission granted (Describe); none when no row was
		///missing.</summary>
		public static List<string> Run() {
			List<Grant> listGrants=Plan(ReadRows());
			foreach(Grant grant in listGrants) {
				DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('0001-01-01',0,"
					+POut.Long(grant.UserGroupNum)+","+POut.Int((int)grant.PermType)+",0)");
			}
			return ArrayPermTypes.Select(x => (permType:x,count:listGrants.Count(y => y.PermType==x)))
				.Where(x => x.count>0)
				.Select(x => Describe(x.permType,x.count))
				.ToList();
		}
	}

	///<summary>Routine maintenance: gives Maintenance Setup and Maintenance Record Completion to every user group that has Security Admin
	///(MaintPermissionGrants), inserting only the rows that are missing and changing nothing else. Once per database: the runner runs it once, and
	///a forced rerun (RunAll) of a database whose ForkMigrationsApplied already holds this level skips it, so a grant an administrator removed since
	///is not given back. On the same rows a second run inserts nothing.</summary>
	[ForkMigration(2026110206,"Routine maintenance: grant Maintenance Setup and Maintenance Record Completion to the Security Admin groups")]
	public class MaintPermissionGrantStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(ForkMigrationRunner.GetAppliedLevels().Contains(Level)) {
				return;
			}
			foreach(string change in MaintPermissionGrants.Run()) {
				context.Log(change);
			}
		}
	}

	///<summary>Routine maintenance: who corrected a completion last, and when (maintrecord.UserNumCorrected and DateTCorrected, with a key on
	///UserNumCorrected). MaintenanceTables.CREATE_RECORD already declares them, so step 2026110204 creates the table complete and this step changes
	///nothing; it adds them to a maintrecord table that an earlier build of this unreleased feature created without them. Writes no row.</summary>
	[ForkMigration(2026110207,"Routine maintenance: add the correction columns of maintrecord")]
	public class MaintRecordCorrectionColumnsStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.AddColumnIfMissing(MaintenanceTables.TABLE_RECORD,"UserNumCorrected","bigint(20) NOT NULL","SecDateTEntry");
			context.AddColumnIfMissing(MaintenanceTables.TABLE_RECORD,"DateTCorrected","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","UserNumCorrected");
			context.EnsureIndex(MaintenanceTables.TABLE_RECORD,"UserNumCorrected","`UserNumCorrected`");
		}
	}
}
