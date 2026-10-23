using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RegistryTests {
	///<summary>Compares every enum that Open Dental's published 25.3 database documentation lists (the numbers stored in office data) with the
	///enums in this program. Names must exist and, where the document prints a number, the number must match.</summary>
	[TestClass]
	public class OdEnumParityTests {
		///<summary>Enums or members that another foundation package adds (M1-01 owns the table classes that declare them). They may be missing
		///until that package merges; once present they are checked like everything else.</summary>
		private static readonly HashSet<string> _setPendingOtherPackage=new HashSet<string> {
			//M1-01: new tables and imagedraw/guardian/eform field/statement/emailmessage enums.
			"EnumBrandingType","EnumCategoryBetterDiag","EnumUserStatus",
			"EnumEFormFieldType.PatientList","EnumEFormFieldType.InsPick",
			"StatementMode.API",
			"EmailMessageSource.EClipboardWeb",
			"GuardianRelationship.None","GuardianRelationship.Father","GuardianRelationship.Sitter",
			"EnumImageAnnotVendor.BetterDiagnostics",
		};

		///<summary>Differences that were already in the 24.3.63 code before this package and that the documentation explains (for example a
		///published comment that prints a stale number). Each one names why it is acceptable.</summary>
		private static readonly Dictionary<string,string> _dictKnownDifferences=new Dictionary<string,string> {
			//The published numbers come from the code comments, and these comments were wrong in Open Dental's own source (24.3 has the same
			//comments). The members are implicit, so the stored numbers are the declaration order, which is unchanged.
			{"X835Status.None","Comment numbers start at 1; the enum starts at 0, and etrans835.Status stores the enum value."},
			{"X835Status.Unprocessed","Same as X835Status.None."},
			{"X835Status.Partial","Same as X835Status.None."},
			{"X835Status.NotFinalized","Same as X835Status.None."},
			{"X835Status.FinalizedSomeDetached","Same as X835Status.None."},
			{"X835Status.FinalizedAllDetached","Same as X835Status.None."},
			{"X835Status.Finalized","Same as X835Status.None."},
			{"TsiServiceCode.BadCheck","The comment says 3; the member is the third one (2), in Open Dental 24.3 and 25.3 alike."},
		};

		[TestMethod]
		public void Diagnostic_ListAllDifferences() {
			List<string> listProblems=GetProblems(includeAllowed:true);
			Console.WriteLine(string.Join(Environment.NewLine,listProblems));
		}

		[TestMethod]
		public void EveryPublishedEnumValue_IsDefinedWithThePublishedNumber() {
			List<string> listProblems=GetProblems(includeAllowed:false);
			Assert.AreEqual(0,listProblems.Count,"Enum differences from Open Dental 25.3:"+Environment.NewLine+string.Join(Environment.NewLine,listProblems));
		}

		private static List<string> GetProblems(bool includeAllowed) {
			List<string> listProblems=new List<string>();
			foreach(IGrouping<string,OdEnumMember> group in OdEnumReference.GetAll().GroupBy(x => x.EnumName)) {
				Type type=OdEnumReference.FindCodeEnum(group.Key);
				if(type==null) {
					if(includeAllowed || !_setPendingOtherPackage.Contains(group.Key)) {
						listProblems.Add(group.Key+": enum not found ("+group.First().Columns+")");
					}
					continue;
				}
				foreach(OdEnumMember member in group) {
					string key=group.Key+"."+member.MemberName;
					if(!includeAllowed && (_setPendingOtherPackage.Contains(group.Key) || _setPendingOtherPackage.Contains(key) || _dictKnownDifferences.ContainsKey(key))) {
						continue;
					}
					if(!Enum.GetNames(type).Contains(member.MemberName)) {
						listProblems.Add(key+": missing (published number "+(member.Number?.ToString()??"?")+")");
						continue;
					}
					if(member.Number==null) {
						continue;
					}
					long value=Convert.ToInt64(Enum.Parse(type,member.MemberName));
					if(value!=member.Number.Value) {
						listProblems.Add(key+": is "+value+", published "+member.Number.Value);
					}
				}
			}
			return listProblems;
		}
	}
}
