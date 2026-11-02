// This program's own values of enums whose numbers are stored in the database (docs/port-notes/FORK-ENUMS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Where this program's own (fork-only) values of stored enums live, so that a later port of an Open Dental release, whose values count
	///up from its last one, never reuses them. Most enums: 10000 and up. Enums stored in tinyint(4) columns cannot hold 10000, so theirs count down
	///from 127 and start at 100. docs/port-notes/FORK-ENUMS.md gives each feature its range; RegistryTests (ForkEnumRangeTests) holds the code,
	///these lists and that document to each other.</summary>
	public static class ForkEnums {
		///<summary>The first fork-only value of an enum stored in a column of at least smallint size (EnumPermType: grouppermission.PermType and
		///securitylog.PermType are smallint(6)).</summary>
		public const int FIRST_VALUE=10000;
		///<summary>The lowest fork-only value of an enum stored in a tinyint(4) column (range 100 to 127).</summary>
		public const int FIRST_VALUE_TINYINT=100;
		///<summary>The highest value a tinyint(4) column holds.</summary>
		public const int LAST_VALUE_TINYINT=127;
		///<summary>The highest value a smallint(6) column holds.</summary>
		public const int LAST_VALUE_SMALLINT=32767;
		public const string FEATURE_MAINTENANCE="Routine maintenance";
		public const string FEATURE_INVENTORY="Inventory tracking";
		public const string FEATURE_SSH_HOST_KEYS="SSH host keys";
		public const string FEATURE_RELEASE_CHANNEL="Release channel";
		public const string FEATURE_PATIENT_FORMS_VIEW="Patient Forms view";
		///<summary>The enums with fork-only values that are stored in tinyint(4) columns: AlertType (alertitem.Type, alertcategorylink.AlertType,
		///alertsub.Type), FormType (alertitem.FormToOpen), DocumentMiscType (documentmisc.DocMiscType) and UserOdFkeyType (userodpref.FkeyType).
		///</summary>
		public static readonly Type[] ArrayTinyIntEnums={ typeof(AlertType),typeof(FormType),typeof(DocumentMiscType),typeof(UserOdFkeyType) };
		///<summary>Every enum that has fork-only values: EnumPermType (smallint(6) columns) and the tinyint enums.</summary>
		public static readonly Type[] ArrayEnums=new[] { typeof(EnumPermType) }.Concat(ArrayTinyIntEnums).ToArray();

		///<summary>The range of fork-only values each feature takes its values from, in every enum of the kind (IsTinyInt: the tinyint enums,
		///otherwise the enums stored in columns of at least smallint size). A value outside every range is one of ArrayEarlierValues.</summary>
		public static readonly List<ForkEnumRange> ListRanges=new List<ForkEnumRange>() {
			new ForkEnumRange(false,10000,10049,FEATURE_MAINTENANCE),
			new ForkEnumRange(false,10050,10099,FEATURE_INVENTORY),
			new ForkEnumRange(false,10100,10149,FEATURE_SSH_HOST_KEYS),
			new ForkEnumRange(true,120,127,FEATURE_MAINTENANCE),
			new ForkEnumRange(true,112,119,FEATURE_INVENTORY),
		};

		///<summary>The fork-only values that version 0.0 released before the tinyint ranges were set. A released value is never renumbered, so they
		///keep their numbers, and no feature may take them in their enum: the release channel's DocumentMiscType 100 and 101 (below every range),
		///and the Patient Forms view's UserOdFkeyType 114 (package B-EF, inside inventory tracking's tinyint range, which in UserOdFkeyType
		///therefore has 114 taken).</summary>
		public static readonly List<ForkEnumEarlierValue> ListEarlierValues=new List<ForkEnumEarlierValue>() {
			new ForkEnumEarlierValue(DocumentMiscType.ForkReleaseManifest,FEATURE_RELEASE_CHANNEL),
			new ForkEnumEarlierValue(DocumentMiscType.ForkReleaseSegment,FEATURE_RELEASE_CHANNEL),
			new ForkEnumEarlierValue(UserOdFkeyType.PatientFormsView,FEATURE_PATIENT_FORMS_VIEW),
		};

		///<summary>The first fork-only value of the enum: FIRST_VALUE_TINYINT for the tinyint enums, otherwise FIRST_VALUE.</summary>
		public static int GetFirstValue(Type enumType) {
			return ArrayTinyIntEnums.Contains(enumType) ? FIRST_VALUE_TINYINT : FIRST_VALUE;
		}

		///<summary>True for a value in the enum's fork-only range.</summary>
		public static bool IsForkOnly(Enum value) {
			return Convert.ToInt64(value)>=GetFirstValue(value.GetType());
		}

		///<summary>The feature a fork-only value belongs to: its earlier value's, else the one range of its kind that holds it. Null when there is
		///none (a value no feature reserved).</summary>
		public static string GetFeature(Enum value) {
			ForkEnumEarlierValue earlierValue=ListEarlierValues.FirstOrDefault(x => x.EnumType==value.GetType() && x.Value==Convert.ToInt64(value));
			if(earlierValue!=null) {
				return earlierValue.Feature;
			}
			bool isTinyInt=ArrayTinyIntEnums.Contains(value.GetType());
			List<ForkEnumRange> listRanges=ListRanges.Where(x => x.IsTinyInt==isTinyInt && x.Contains(Convert.ToInt64(value))).ToList();
			if(listRanges.Count!=1) {
				return null;
			}
			return listRanges[0].Feature;
		}
	}

	///<summary>One feature's range of fork-only values (docs/port-notes/FORK-ENUMS.md).</summary>
	public class ForkEnumRange {
		///<summary>True for the range in the tinyint enums, false for the one in the enums stored in at least smallint columns.</summary>
		public readonly bool IsTinyInt;
		public readonly int First;
		public readonly int Last;
		public readonly string Feature;

		public ForkEnumRange(bool isTinyInt,int first,int last,string feature) {
			IsTinyInt=isTinyInt;
			First=first;
			Last=last;
			Feature=feature;
		}

		public bool Contains(long value) {
			return value>=First && value<=Last;
		}
	}

	///<summary>A fork-only value that a release shipped before the ranges were set (ForkEnums.ListEarlierValues).</summary>
	public class ForkEnumEarlierValue {
		public readonly Type EnumType;
		public readonly long Value;
		public readonly string Feature;

		public ForkEnumEarlierValue(Enum value,string feature) {
			EnumType=value.GetType();
			Value=Convert.ToInt64(value);
			Feature=feature;
		}
	}
}
