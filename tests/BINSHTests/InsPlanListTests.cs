// The In Net column of Lists, Insurance Plans (Open Dental 25.3): which plan types count as in network, and the PlanType and InNet columns of
// InsPlans.GetBigList against a scratch database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BINSHTests {
	[TestClass]
	public class InsPlanListTests {
		[TestMethod]
		public void IsInNetworkPlanType_PpoFlatCopayAndCapitation() {
			Assert.IsTrue(InsPlans.IsInNetworkPlanType("p"),"PPO percentage, and PPO fixed benefit (also p).");
			Assert.IsTrue(InsPlans.IsInNetworkPlanType("f"),"Medicaid or flat copay.");
			Assert.IsTrue(InsPlans.IsInNetworkPlanType("c"),"Capitation.");
			Assert.IsFalse(InsPlans.IsInNetworkPlanType(""),"Category percentage.");
			Assert.IsFalse(InsPlans.IsInNetworkPlanType(null));
			Assert.IsTrue(InsPlans.IsInNetworkPlanType("P"));
		}

		[TestMethod]
		public void GetBigList_HasPlanTypeAndInNet() {
			BinshDb.Begin();
			string carrierName="BINSH InNet "+Guid.NewGuid().ToString("N").Substring(0,8);
			Carrier carrier=CarrierT.CreateCarrier(carrierName);
			long feeSchedNum=FeeSchedT.CreateFeeSched(FeeScheduleType.Normal,"binshinnet");
			InsPlan insPlanCat=InsPlanT.CreateInsPlan(carrier.CarrierNum,feeSched:0,groupNum:"CAT");
			InsPlan insPlanPpo=InsPlanT.CreateInsPlanPPO(carrier.CarrierNum,feeSchedNum);
			InsPlan insPlanFlat=InsPlanT.CreateInsPlanMediFlatCopay(carrier.CarrierNum,feeSchedNum);
			InsPlan insPlanCap=InsPlanT.CreateInsPlanCapitation(carrier.CarrierNum,feeSchedNum);
			DataTable table=InsPlans.GetBigList(false,"",carrierName,"","","","",true,true);
			Assert.IsTrue(table.Columns.Contains("PlanType"));
			Assert.IsTrue(table.Columns.Contains("InNet"));
			Assert.AreEqual(4,table.Rows.Count);
			string GetInNet(InsPlan insPlan) {
				return table.Select().Single(x => x["PlanNum"].ToString()==insPlan.PlanNum.ToString())["InNet"].ToString();
			}
			Assert.AreEqual("",GetInNet(insPlanCat));
			Assert.AreEqual("X",GetInNet(insPlanPpo));
			Assert.AreEqual("X",GetInNet(insPlanFlat));
			Assert.AreEqual("X",GetInNet(insPlanCap));
			Assert.AreEqual("p",table.Select().Single(x => x["PlanNum"].ToString()==insPlanPpo.PlanNum.ToString())["PlanType"].ToString());
		}
	}
}
