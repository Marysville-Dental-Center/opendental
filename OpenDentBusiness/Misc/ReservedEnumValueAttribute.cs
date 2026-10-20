using System;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Marks an enum value that this program defines only so that rows written by Open Dental 24.4 to 25.3 load and display with the right
	///name, for example values that belong to Open Dental hosted services this fork removed (Message-to-Pay, mass email, BetterDiagnostics).
	///Nothing in this program creates rows with such a value.
	///<para>It derives from IsODHQAttribute, so every list that already hides HQ-only values (GenericTools.IsODHQ, for example alert category
	///setup) hides reserved values too.</para></summary>
	[AttributeUsage(AttributeTargets.Field,AllowMultiple=false)]
	public class ReservedEnumValueAttribute:IsODHQAttribute {
		///<summary>Why the value is reserved. Not shown to users.</summary>
		public string Reason { get; }

		public ReservedEnumValueAttribute(string reason="") {
			Reason=reason;
		}

		///<summary>True if the enum value is marked [ReservedEnumValue].</summary>
		public static bool IsReserved<T>(T value) where T:Enum {
			System.Reflection.FieldInfo fieldInfo=typeof(T).GetField(value.ToString());
			if(fieldInfo==null) {
				return false;
			}
			return Attribute.IsDefined(fieldInfo,typeof(ReservedEnumValueAttribute));
		}
	}
}
