// Code groups against a scratch database: ranges in membership and CodeNum lists (24.4), the 25.1 hidden (retired) meaning and
// ShowInFrequency, the 25.3 ShowInOther flag, and what blocks a delete. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace InsuranceTests {
	[TestClass]
	public class CodeGroupDbTests {
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			InsDb.Begin();
		}

		internal static CodeGroup NewCodeGroup(string procCodes,bool showInFrequency=false,bool showInAgeLimit=false,bool showInOther=false,
			bool isHidden=false)
		{
			CodeGroup codeGroup=new CodeGroup();
			codeGroup.GroupName="A-INS group "+(++_counter)+" "+Guid.NewGuid().ToString("N").Substring(0,6);
			codeGroup.ProcCodes=procCodes;
			codeGroup.ItemOrder=1000+_counter;
			codeGroup.ShowInFrequency=showInFrequency;
			codeGroup.ShowInAgeLimit=showInAgeLimit;
			codeGroup.ShowInOther=showInOther;
			codeGroup.IsHidden=isHidden;
			CodeGroups.Insert(codeGroup);
			CodeGroups.RefreshCache();
			return codeGroup;
		}

		[TestMethod]
		public void IsProcInCodeGroup_RangesAndCodes() {
			CodeGroup codeGroup=NewCodeGroup("Z3391-Z3394,Z3740");
			Assert.IsTrue(CodeGroups.IsProcInCodeGroup("Z3391",codeGroup.CodeGroupNum));
			Assert.IsTrue(CodeGroups.IsProcInCodeGroup("Z3393",codeGroup.CodeGroupNum));
			Assert.IsTrue(CodeGroups.IsProcInCodeGroup("Z3740",codeGroup.CodeGroupNum));
			Assert.IsFalse(CodeGroups.IsProcInCodeGroup("Z3395",codeGroup.CodeGroupNum));
			Assert.IsFalse(CodeGroups.IsProcInCodeGroup("Z374",codeGroup.CodeGroupNum),"No substring matches.");
			Assert.IsFalse(CodeGroups.IsProcInCodeGroup("Z3392",0));
			Assert.IsFalse(CodeGroups.IsProcInCodeGroup("Z3392",-5),"Unknown groups hold nothing.");
		}

		[TestMethod]
		public void GetCodeNums_ExpandsRanges() {
			List<ProcedureCode> listProcedureCodes=new[] { "Z3490","Z3491","Z3492","Z3493","Z3494","Z3495","Z3497" }
				.Select(x => ProcedureCodeT.CreateProcCode(x)).ToList();
			ProcedureCodes.RefreshCache();
			CodeGroup codeGroup=NewCodeGroup("Z3491-Z3494,Z3497");
			List<long> listCodeNums=CodeGroups.GetCodeNums(codeGroup.CodeGroupNum);
			List<long> listExpected=listProcedureCodes.Where(x => x.ProcCode!="Z3490" && x.ProcCode!="Z3495").Select(x => x.CodeNum).ToList();
			CollectionAssert.AreEquivalent(listExpected,listCodeNums);
			List<ProcedureCode> listInGroup=CodeGroups.GetProcedureCodes(codeGroup);
			CollectionAssert.AreEqual(new[] { "Z3491","Z3492","Z3493","Z3494","Z3497" },listInGroup.Select(x => x.ProcCode).ToArray());
		}

		[TestMethod]
		public void GetCodeNumsForProcCodes_LeadingZerosAndOtherPrefixes() {
			//The 24.3 version only knew D codes and dropped zeros below 100.
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z0012");
			ProcedureCodes.RefreshCache();
			CollectionAssert.Contains(ProcedureCodes.GetCodeNumsForProcCodes("Z0010-Z0019"),procedureCode.CodeNum);
		}

		[TestMethod]
		public void GridHelpers_FollowTheFlags() {
			CodeGroup codeGroupFreq=NewCodeGroup("Z3501",showInFrequency:true);
			CodeGroup codeGroupAge=NewCodeGroup("Z3502",showInAgeLimit:true);
			CodeGroup codeGroupOther=NewCodeGroup("Z3503",showInOther:true);
			CodeGroup codeGroupRetired=NewCodeGroup("Z3504",showInFrequency:true,showInAgeLimit:true,showInOther:true,isHidden:true);
			List<long> listFreq=CodeGroups.GetForFrequencyGrid().Select(x => x.CodeGroupNum).ToList();
			List<long> listAge=CodeGroups.GetForAgeLimitGrid().Select(x => x.CodeGroupNum).ToList();
			List<long> listOther=CodeGroups.GetForOtherBenefitsGrid().Select(x => x.CodeGroupNum).ToList();
			CollectionAssert.Contains(listFreq,codeGroupFreq.CodeGroupNum);
			CollectionAssert.DoesNotContain(listFreq,codeGroupAge.CodeGroupNum);
			CollectionAssert.DoesNotContain(listFreq,codeGroupRetired.CodeGroupNum,"Retired groups get no rows.");
			CollectionAssert.Contains(listAge,codeGroupAge.CodeGroupNum);
			CollectionAssert.DoesNotContain(listAge,codeGroupOther.CodeGroupNum);
			CollectionAssert.DoesNotContain(listAge,codeGroupRetired.CodeGroupNum);
			CollectionAssert.Contains(listOther,codeGroupOther.CodeGroupNum);
			CollectionAssert.DoesNotContain(listOther,codeGroupFreq.CodeGroupNum);
			CollectionAssert.DoesNotContain(listOther,codeGroupRetired.CodeGroupNum);
			//Short cache list: every group that is not retired.
			List<long> listShort=CodeGroups.GetDeepCopy(isShort:true).Select(x => x.CodeGroupNum).ToList();
			CollectionAssert.Contains(listShort,codeGroupOther.CodeGroupNum);
			CollectionAssert.DoesNotContain(listShort,codeGroupRetired.CodeGroupNum);
			//Edit Benefit offers retired groups only to the benefit that already uses one.
			CollectionAssert.DoesNotContain(CodeGroups.GetForBenefitPicker(0).Select(x => x.CodeGroupNum).ToList(),codeGroupRetired.CodeGroupNum);
			CollectionAssert.Contains(CodeGroups.GetForBenefitPicker(codeGroupRetired.CodeGroupNum).Select(x => x.CodeGroupNum).ToList(),
				codeGroupRetired.CodeGroupNum);
			Assert.IsTrue(CodeGroups.IsHidden(codeGroupRetired.CodeGroupNum));
			Assert.IsFalse(CodeGroups.IsHidden(codeGroupFreq.CodeGroupNum));
			Assert.IsTrue(CodeGroups.IsShownInFrequency(codeGroupFreq.CodeGroupNum));
			Assert.IsFalse(CodeGroups.IsShownInFrequency(codeGroupOther.CodeGroupNum));
		}

		[TestMethod]
		public void RetiredGroup_BenefitsStayValid() {
			//A retired group's benefits are kept out of the Insurance Benefits window but the estimates still use them.
			CodeGroup codeGroup=NewCodeGroup("Z3601-Z3605",isHidden:true);
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,CodeGroupNum=codeGroup.CodeGroupNum,PlanNum=777001 };
			Assert.IsFalse(CodeGroups.IsShownInBenefitsWindow(benefit));
			Assert.IsTrue(CodeGroups.IsShownInBenefitsWindow(new Benefit { BenefitType=InsBenefitType.Exclusions,CovCatNum=1 }));
			Assert.IsTrue(Benefits.IsExcluded("Z3603",new List<Benefit> { benefit },777001,0,"3",""));
			StringAssert.Contains(Benefits.GetCategoryString(benefit),"(hidden)");
		}

		[TestMethod]
		public void GetCountPlansUsing_CountsPlansNotRows() {
			CodeGroup codeGroup=NewCodeGroup("Z3701");
			Assert.AreEqual(0,CodeGroups.GetCountPlansUsing(codeGroup.CodeGroupNum));
			Benefits.Insert(new Benefit { PlanNum=880001,BenefitType=InsBenefitType.Exclusions,CodeGroupNum=codeGroup.CodeGroupNum });
			Benefits.Insert(new Benefit { PlanNum=880001,BenefitType=InsBenefitType.CoInsurance,Percent=50,CodeGroupNum=codeGroup.CodeGroupNum });
			Benefits.Insert(new Benefit { PlanNum=880002,BenefitType=InsBenefitType.Exclusions,CodeGroupNum=codeGroup.CodeGroupNum });
			Assert.AreEqual(2,CodeGroups.GetCountPlansUsing(codeGroup.CodeGroupNum));
		}

		[TestMethod]
		public void Sync_DeletesARemovedGroup() {
			CodeGroup codeGroup=NewCodeGroup("Z3801");
			List<CodeGroup> listOld=CodeGroups.GetDeepCopy();
			List<CodeGroup> listNew=listOld.Select(x => x.Copy()).Where(x => x.CodeGroupNum!=codeGroup.CodeGroupNum).ToList();
			Assert.IsTrue(CodeGroups.Sync(listNew,listOld));
			Assert.IsNull(CodeGroups.GetOneFromDb(codeGroup.CodeGroupNum));
		}

		[TestMethod]
		public void ProcCodes_RoundTripRangeText() {
			CodeGroup codeGroup=NewCodeGroup("Z0100-Z0199,Z2140-Z2161,Z2750");
			Assert.AreEqual("Z0100-Z0199,Z2140-Z2161,Z2750",CodeGroups.GetOneFromDb(codeGroup.CodeGroupNum).ProcCodes);
		}
	}
}
