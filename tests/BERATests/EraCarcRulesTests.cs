// v25_2-manage-era-carc-logic: the entries of "Don't auto-process claims w/ these CARCs" (bare and GROUP-CODE entries, $0.00 adjustments, save
// validation, and the descriptions that name the matching entry). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using static BERATests.EraObjects;

namespace BERATests {
	[TestClass]
	public class EraCarcRulesTests {
		[TestInitialize]
		public void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void Parse_243DefaultList_AllBare() {
			List<EraCarcRule> listRules=EraCarcRules.Parse("97,16,252,242,50,251,B7,226,250");
			Assert.AreEqual(9,listRules.Count);
			Assert.IsTrue(listRules.All(x => x.GroupCode==""));
			CollectionAssert.AreEqual(new[] { "97","16","252","242","50","251","B7","226","250" },listRules.Select(x => x.ReasonCode).ToArray());
		}

		[TestMethod]
		public void Parse_QualifiedEntries_TrimsUpperCasesAndDropsDuplicates() {
			List<EraCarcRule> listRules=EraCarcRules.Parse(" co-97, PR - 16 ,b7,,CO-97, 97 ");
			CollectionAssert.AreEqual(new[] { "CO-97","PR-16","B7","97" },listRules.Select(x => x.ToString()).ToArray());
			Assert.AreEqual("CO",listRules[0].GroupCode);
			Assert.AreEqual("97",listRules[0].ReasonCode);
		}

		[TestMethod]
		public void Parse_BlankOrNull_Empty() {
			Assert.AreEqual(0,EraCarcRules.Parse("").Count);
			Assert.AreEqual(0,EraCarcRules.Parse(null).Count);
			Assert.AreEqual(0,EraCarcRules.Parse(" , ,").Count);
		}

		[TestMethod]
		public void Parse_UnknownShape_KeptWholeAsBareCode() {
			//An entry 24.3 accepted that is not GROUP-CODE keeps matching exactly what it matched before.
			List<EraCarcRule> listRules=EraCarcRules.Parse("XX-97");
			Assert.AreEqual(1,listRules.Count);
			Assert.AreEqual("",listRules[0].GroupCode);
			Assert.AreEqual("XX-97",listRules[0].ReasonCode);
		}

		[TestMethod]
		public void IsValid_AcceptsBareQualifiedAndBlank() {
			Assert.IsTrue(EraCarcRules.IsValid("97,16,252,242,50,251,B7,226,250",out _));
			Assert.IsTrue(EraCarcRules.IsValid("CO-97, pr-16 ,OA-23,PI-45,CR-B7",out _));
			Assert.IsTrue(EraCarcRules.IsValid("CO - 97",out _));
			Assert.IsTrue(EraCarcRules.IsValid("",out _));
			Assert.IsTrue(EraCarcRules.IsValid(" ",out _));
			Assert.IsTrue(EraCarcRules.IsValid("97,,16",out _));
		}

		[TestMethod]
		public void IsValid_RejectsBadEntries_NamesTheEntry() {
			Assert.IsFalse(EraCarcRules.IsValid("97,ABCDEF",out string error));
			StringAssert.Contains(error,"ABCDEF");
			Assert.IsFalse(EraCarcRules.IsValid("XX-97",out error));
			StringAssert.Contains(error,"XX-97");
			Assert.IsFalse(EraCarcRules.IsValid("CO-",out _));
			Assert.IsFalse(EraCarcRules.IsValid("9 7",out _));
			Assert.IsFalse(EraCarcRules.IsValid("97!",out _));
			Assert.IsFalse(EraCarcRules.IsValid("CO-97-1",out _));
		}

		[TestMethod]
		public void IsMatch_BareMatchesAnyGroup_QualifiedOnlyItsGroup() {
			EraCarcRule ruleBare=EraCarcRules.Parse("97")[0];
			EraCarcRule ruleQualified=EraCarcRules.Parse("CO-97")[0];
			Assert.IsTrue(ruleBare.IsMatch(EraAdj("CO","97",40)));
			Assert.IsTrue(ruleBare.IsMatch(EraAdj("PR","97",40)));
			Assert.IsTrue(ruleQualified.IsMatch(EraAdj("CO","97",40)));
			Assert.IsFalse(ruleQualified.IsMatch(EraAdj("PR","97",40)));
			Assert.IsFalse(ruleQualified.IsMatch(EraAdj("CO","96",40)));
		}

		[TestMethod]
		public void IsMatch_ZeroAmountNeverMatches() {
			EraCarcRule rule=EraCarcRules.Parse("97")[0];
			Assert.IsFalse(rule.IsMatch(EraAdj("CO","97",0)));
			Assert.IsTrue(rule.IsMatch(EraAdj("CO","97",-5)));//A negative adjustment still carries money.
		}

		[TestMethod]
		public void IsMatch_CaseInsensitive() {
			EraCarcRule rule=EraCarcRules.Parse("co-b7")[0];
			Assert.IsTrue(rule.IsMatch(EraAdj("CO","b7",10)));
			Assert.IsFalse(rule.IsMatch(null));
			Assert.IsFalse(rule.IsMatch(EraAdj("CO",null,10)));
		}

		[TestMethod]
		public void GetMatches_ClaimAndProcLevels_DescriptionsNameTheEntry() {
			Hx835_Proc procPaid=EraProc("Z0120",60,48,1001,EraAdj("CO","45",12));
			Hx835_Proc procDenied=EraProc("Z0220",40,0,1002,EraAdj("CO","97",40));
			Hx835_Claim claim=EraClaim(procPaid,procDenied);
			claim.ListClaimAdjustments.Add(EraAdj("PR","16",5));
			List<EraCarcMatch> listMatches=EraCarcRules.GetMatches(EraCarcRules.Parse("97,16"),claim);
			Assert.AreEqual(2,listMatches.Count);
			Assert.IsTrue(listMatches[0].IsClaimLevel);
			Assert.AreEqual("PR-16 on the claim",listMatches[0].GetDescription());
			Assert.AreSame(procDenied,listMatches[1].Proc);
			Assert.AreEqual("CO-97 on procedure Z0220",listMatches[1].GetDescription());
			Assert.AreEqual("PR-16 on the claim, CO-97 on procedure Z0220",EraCarcRules.GetDescription(listMatches));
		}

		[TestMethod]
		public void GetMatches_QualifiedEntryOnOtherGroup_NoMatch() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,48,1001,EraAdj("CO","45",12)),EraProc("Z0220",40,0,1002,EraAdj("CO","97",40)));
			Assert.AreEqual(0,EraCarcRules.GetMatches(EraCarcRules.Parse("PR-97"),claim).Count);
			Assert.AreEqual(1,EraCarcRules.GetMatches(EraCarcRules.Parse("CO-97"),claim).Count);
		}

		[TestMethod]
		public void GetMatches_NoRulesOrNullClaim_Empty() {
			Hx835_Claim claim=EraClaim(EraProc("Z0220",40,0,1002,EraAdj("CO","97",40)));
			Assert.AreEqual(0,EraCarcRules.GetMatches(new List<EraCarcRule>(),claim).Count);
			Assert.AreEqual(0,EraCarcRules.GetMatches(EraCarcRules.Parse("97"),null).Count);
			Assert.AreEqual("",EraCarcRules.GetDescription(new List<EraCarcMatch>()));
		}

		[TestMethod]
		public void GetSyntaxHelp_ExplainsGroupCodes() {
			string help=EraCarcRules.GetSyntaxHelp();
			StringAssert.Contains(help,"CO-97");
			foreach(string groupCode in EraCarcRules.ArrayGroupCodes) {
				StringAssert.Contains(help,groupCode);
			}
		}
	}
}
