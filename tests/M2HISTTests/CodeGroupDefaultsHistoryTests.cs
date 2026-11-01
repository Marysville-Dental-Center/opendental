// Reset Code Groups (Procedure Code Tools, B-PROC) and the Open Dental 26.1 Insurance History options, without a database: the default groups
// give the rows of the 24.3 window again, and a Canadian default that takes the office's codes takes its history row with them. Synthetic data only
// (made-up Z codes for the office's groups).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2HISTTests {
	[TestClass]
	public class CodeGroupDefaultsHistoryTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		///<summary>The CDT defaults: the seven groups of the 24.3 history categories show in Insurance History with their first code (the code the
		///24.3 preference held by default), SRP with four quadrant rows; the other groups show no history row. So a reset gives the 24.3 rows back.
		///</summary>
		[TestMethod]
		public void Defaults_Cdt_GiveThe24_3InsuranceHistoryRows() {
			List<CodeGroup> listCodeGroups=CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDT);
			Assert.AreEqual(7,listCodeGroups.Count(x => x.ShowInHistory));
			Assert.IsTrue(listCodeGroups.Where(x => x.ShowInHistory).All(x => string.IsNullOrEmpty(CodeGroupHistoryOptions.GetError(x.ShowInHistory,x.HistProcCode,
				x.IsPerioFourQuads,y => true))),"Each satisfies Code Group Edit's rules.");
			Assert.IsTrue(listCodeGroups.Where(x => x.ShowInHistory).All(x => CodeGroupHistoryOptions.GetWarning(true,x.HistProcCode,x.ProcCodes)==""),
				"Each History Proc Code is one of the group's codes.");
			Assert.IsTrue(listCodeGroups.Where(x => !x.ShowInHistory).All(x => x.HistProcCode=="" && !x.IsPerioFourQuads));
			List<InsHistCategory> listInsHistCategories=InsHistCategories.GetAll(listCodeGroups);
			CollectionAssert.AreEqual(new[] { "BW","Exam","Pano/FMX","Prophy","Perio Scaling UL","Perio Scaling UR","Perio Scaling LL","Perio Scaling LR",
				"Full Debridement","Perio Maint" },listInsHistCategories.Select(x => x.Description).ToArray());
			CollectionAssert.AreEqual(new[] { "D0272","D0120","D0210","D1110","D4341","D4341","D4341","D4341","D4355","D4910" },
				listInsHistCategories.Select(x => x.HistProcCode).ToArray());
		}

		///<summary>The CDA defaults have codes only for Fluoride and Sealant, which are not history categories, so they show no history row; a default
		///that takes the codes of the office's group it replaces (CodeGroupResets.KeepCodesForEmptyDefaults) gets its history row with them.</summary>
		[TestMethod]
		public void Reset_Cda_DefaultThatKeepsTheOfficesCodesGetsItsHistoryRow() {
			Assert.AreEqual(0,CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDA).Count(x => x.ShowInHistory));
			List<CodeGroup> listCodeGroupsOffice=new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=1,GroupName="Bitewings",ProcCodes="Z0272-Z0274",ItemOrder=0,CodeGroupFixed=EnumCodeGroupFixed.BW,ShowInHistory=true,
					HistProcCode="Z0273" },
				new CodeGroup { CodeGroupNum=2,GroupName="Scaling",ProcCodes="Z4341",ItemOrder=1,CodeGroupFixed=EnumCodeGroupFixed.SRP },
			};
			CodeGroupResetPlan codeGroupResetPlan=CodeGroupResets.CreatePlan(EnumDentalCodeSystem.CDA,listCodeGroupsOffice,new Dictionary<long,int>());
			CodeGroup codeGroupBW=codeGroupResetPlan.ListCodeGroupsNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.BW);
			Assert.AreEqual("Z0272-Z0274",codeGroupBW.ProcCodes,"The office's codes are kept.");
			Assert.IsTrue(codeGroupBW.ShowInHistory);
			Assert.AreEqual("Z0272",codeGroupBW.HistProcCode,"A reset sets the default rule: the first code (the start of a range).");
			CodeGroup codeGroupSRP=codeGroupResetPlan.ListCodeGroupsNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.SRP);
			Assert.IsTrue(codeGroupSRP.ShowInHistory && codeGroupSRP.IsPerioFourQuads);
			Assert.AreEqual("Z4341",codeGroupSRP.HistProcCode);
			Assert.IsFalse(codeGroupResetPlan.ListCodeGroupsNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Exam).ShowInHistory,"No codes, no row.");
		}
	}
}
