// Remembered SSH host keys (docs/specs/ssh-host-keys.md): the fork-only table sshhostkey and the step that creates it, level 2026110221 of this
// feature's range 2026110221 to 2026110229. No step writes a row: the first connection to each server stores its key (SshHostKeys.InsertFirstUse).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>The remembered SSH host keys table, exactly as SHOW CREATE TABLE prints it on MariaDB 10.11. Not Open Dental schema: the schema
	///verifier allows it as an extra table and ForkTables.VerifyDatabase checks it. The step below creates it empty and writes no row into it, so on
	///an adopted database it changes nothing else, and the adoption tools hold it to empty.</summary>
	public class SshHostKeyTables:ForkTableSet {
		public const string TABLE_HOST_KEY="sshhostkey";

		///<summary>One row per server (host and port, the unique key HostPort, which also makes two first connections at the same moment store one
		///key). Host is stored trimmed and in lower case, and the utf8mb3_general_ci collation compares it without case.</summary>
		public const string CREATE_HOST_KEY=@"CREATE TABLE `sshhostkey` (
  `SshHostKeyNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `Host` varchar(255) NOT NULL,
  `Port` int(11) NOT NULL,
  `KeyType` varchar(255) NOT NULL,
  `FingerprintSha256` varchar(255) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTStored` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `IsAccepted` tinyint(4) NOT NULL,
  `RefusedKeyType` varchar(255) NOT NULL,
  `RefusedFingerprintSha256` varchar(255) NOT NULL,
  `DateTRefused` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`SshHostKeyNum`),
  UNIQUE KEY `HostPort` (`Host`,`Port`),
  KEY `UserNum` (`UserNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public override string Feature {
			get { return "SSH host keys"; }
		}

		public override string Levels {
			get { return "2026110221"; }
		}

		public override string[] ArrayCreateTables {
			get { return new[] { CREATE_HOST_KEY }; }
		}
	}

	///<summary>Remembered SSH host keys: one stored host key per SSH server. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110221,"SSH host keys: add the sshhostkey table")]
	public class SshHostKeyTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(SshHostKeyTables.CREATE_HOST_KEY);
		}
	}
}
