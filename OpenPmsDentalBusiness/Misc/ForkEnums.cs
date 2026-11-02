// This program's own values of enums whose numbers are stored in the database (docs/port-notes/FORK-ENUMS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Where this program's own (fork-only) values of stored enums live, so that a later port of an Open Dental release, whose values count
	///up from its last one, never reuses them. Most enums: 10000 and up. Enums stored in tinyint(4) columns cannot hold 10000, so theirs count down
	///from 127 and start at 100. docs/port-notes/FORK-ENUMS.md gives each feature its range.</summary>
	public static class ForkEnums {
		///<summary>The first fork-only value of an enum stored in a column of at least smallint size (EnumPermType: grouppermission.PermType and
		///securitylog.PermType are smallint(6)).</summary>
		public const int FIRST_VALUE=10000;
		///<summary>The lowest fork-only value of an enum stored in a tinyint(4) column (range 100 to 127).</summary>
		public const int FIRST_VALUE_TINYINT=100;
		///<summary>The enums with fork-only values that are stored in tinyint(4) columns: AlertType (alertitem.Type, alertcategorylink.AlertType,
		///alertsub.Type) and FormType (alertitem.FormToOpen).</summary>
		public static readonly Type[] ArrayTinyIntEnums={ typeof(AlertType),typeof(FormType) };

		///<summary>The first fork-only value of the enum: FIRST_VALUE_TINYINT for the tinyint enums, otherwise FIRST_VALUE.</summary>
		public static int GetFirstValue(Type enumType) {
			return ArrayTinyIntEnums.Contains(enumType) ? FIRST_VALUE_TINYINT : FIRST_VALUE;
		}

		///<summary>True for a value in the enum's fork-only range.</summary>
		public static bool IsForkOnly(Enum value) {
			return Convert.ToInt64(value)>=GetFirstValue(value.GetType());
		}
	}
}
