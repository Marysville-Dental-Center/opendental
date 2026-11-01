// The Blue Book rule that bases estimates on the provider's fee (Open Dental 25.3 renamed it from "UCR Fee" to "Provider Fee"; value 5 is
// unchanged): the enum, the preference that keeps its database name, and the estimate log text against a scratch database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using CodeBase;
using UnitTestsCore;

namespace BINSHTests {
	[TestClass]
	public class BlueBookProviderFeeTests {
		[TestMethod]
		public void RuleType_ValueFiveIsProviderFee() {
			Assert.AreEqual(5,(int)InsBlueBookRuleType.ProviderFee);
			StringAssert.Contains(InsBlueBookRuleType.ProviderFee.GetDescription(),"Provider Fee");
			Assert.IsFalse(InsBlueBookRuleType.ProviderFee.GetDescription().Contains("UCR"));
			Assert.IsFalse(Enum.GetNames(typeof(InsBlueBookRuleType)).Contains("UcrFee"));
		}

		[TestMethod]
		public void Preference_KeepsItsDatabaseName() {
			//The office database still holds this row; it must never be renamed.
			Assert.AreEqual("InsBlueBookUcrFeePercent",PrefName.InsBlueBookUcrFeePercent.ToString());
		}

		[TestMethod]
		public void GetAllowed_ProviderFeeRuleUsesTheProviderFeeAndSaysSo() {
			BinshDb.Begin();
			string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
			long feeSchedNum=FeeSchedT.CreateFeeSched(FeeScheduleType.Normal,"binshbb"+suffix);
			long provNum=ProviderT.CreateProvider("BB"+suffix.Substring(0,4),feeSchedNum:feeSchedNum);
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z2750");
			FeeT.CreateFee(feeSchedNum,procedureCode.CodeNum,200);
			int percentOld=PrefC.GetInt(PrefName.InsBlueBookUcrFeePercent);
			try {
				Prefs.UpdateInt(PrefName.InsBlueBookUcrFeePercent,80);
				Patient patient=PatientT.CreatePatient("bb"+suffix,priProvNum:provNum);
				Procedure procedure=ProcedureT.CreateProcedure(patient,"Z2750",ProcStat.TP,"",500,provNum:provNum);
				Carrier carrier=CarrierT.CreateCarrier("BINSH BB "+suffix);
				BlueBookEstimateData blueBookEstimateData=new BlueBookEstimateData();
				blueBookEstimateData.InsPlanPrimaryDental=InsPlanT.CreateInsPlan(carrier.CarrierNum,feeSched:0);
				blueBookEstimateData.CarrierPri=carrier;
				blueBookEstimateData.ListInsBlueBookRules=new List<InsBlueBookRule> {
					new InsBlueBookRule { InsBlueBookRuleNum=1,ItemOrder=0,RuleType=InsBlueBookRuleType.ProviderFee },
				};
				blueBookEstimateData.ListInsBlueBooks=new List<InsBlueBook>();
				double allowed=blueBookEstimateData.GetAllowed(procedure,null,true);
				Assert.AreEqual(160,allowed,0.001,"80% of the provider's fee of 200; Blue Book history is not read.");
				string logText=blueBookEstimateData.LogText;
				StringAssert.Contains(logText,"Provider Fee");
				StringAssert.Contains(logText.ToLower(),"80% of");
				StringAssert.Contains(logText.ToLower(),"provider fee)");
				Assert.IsFalse(logText.Contains("UCR"),logText);
			}
			finally {
				Prefs.UpdateInt(PrefName.InsBlueBookUcrFeePercent,percentOld);
			}
		}
	}
}
