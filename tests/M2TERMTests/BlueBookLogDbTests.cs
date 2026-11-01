// The Blue Book estimate log (BlueBookEstimateData.LogText, kept as an InsBlueBookLog when the estimate is saved) names the provider fee rule
// with the 25.4 wording.
// The calculation is unchanged: the provider's fee schedule amount times the percent preference (InsBlueBookUcrFeePercent, name unchanged).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2TERMTests {
	[TestClass]
	public class BlueBookLogDbTests {
		[TestInitialize]
		public void TestInitialize() {
			TermDb.Begin();
		}

		private static BlueBookEstimateData NewProviderFeeOnly() {
			BlueBookEstimateData blueBookEstimateData=new BlueBookEstimateData();
			blueBookEstimateData.InsPlanPrimaryDental=new InsPlan { PlanNum=1,PlanType="" };
			blueBookEstimateData.ListInsBlueBookRules=new List<InsBlueBookRule> {
				new InsBlueBookRule { InsBlueBookRuleNum=1,ItemOrder=0,RuleType=InsBlueBookRuleType.ProviderFee },
			};
			return blueBookEstimateData;
		}

		private static Procedure NewProcedure(long codeNum) {
			Procedure procedure=new Procedure();
			procedure.CodeNum=codeNum;
			procedure.ProvNum=TermDb.ProvNum;
			procedure.ProcFee=300;
			procedure.UnitQty=1;
			return procedure;
		}

		[TestMethod]
		public void ProviderFeeRule_LogsStandardProviderFee() {
			BlueBookEstimateData blueBookEstimateData=NewProviderFeeOnly();
			double allowed=blueBookEstimateData.GetAllowed(NewProcedure(TermDb.CodeNumWithFee),null,true);
			Assert.AreEqual(160,allowed,0.001,"80% of the 200 fee in the provider's fee schedule.");
			Assert.AreEqual("Rule Type: Standard Provider Fee. Allowed amount: 160.00 (80% of standard provider fee).",blueBookEstimateData.LogText);
		}

		[TestMethod]
		public void ProviderFeeRule_NoFee_LogsStandardProviderFee() {
			BlueBookEstimateData blueBookEstimateData=NewProviderFeeOnly();
			double allowed=blueBookEstimateData.GetAllowed(NewProcedure(TermDb.CodeNumWithoutFee),null,true);
			Assert.AreEqual(-1,allowed,0.001);
			Assert.AreEqual("No rule applied. Last attempted Rule Type: Standard Provider Fee. No fee for code in fee schedule."
				+" Procedure's fee used as allowed amount: 300.00.",blueBookEstimateData.LogText);
			Assert.IsFalse(TermSources.RegexUcrWord.IsMatch(blueBookEstimateData.LogText));
		}
	}
}
