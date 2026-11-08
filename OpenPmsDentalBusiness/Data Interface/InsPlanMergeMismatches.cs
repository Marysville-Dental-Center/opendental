// P262-CLAIMS (Open Dental 26.2, release note "When combining insurance plans, there is a new detailed warning for mismatched fields"): which
// fields of the plans being combined differ from the plan they are combined into. The fields compared, their names and the benefits line are this
// program's own (INFERRED); see docs/port-notes/P262-CLAIMS.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>One field of one plan that differs from the plan the plans are combined into. The plan's own value is lost when combined.</summary>
	public class InsPlanMismatch {
		///<summary>The insplan column, or "Benefits" for the plan's benefits.</summary>
		public string FieldName;
		///<summary>The name shown to the user, after the Edit Insurance Plan window.</summary>
		public string Label;
		///<summary>The plan whose value differs (one of the plans that will be combined into the kept plan).</summary>
		public long PlanNumOther;
		///<summary>The value of the plan that is kept, as shown to the user.</summary>
		public string ValueKept;
		///<summary>The value of the other plan, as shown to the user.</summary>
		public string ValueOther;
	}

	///<summary>Finds the fields that differ between the plan chosen in Combine Insurance Plans and each of the other plans, for the detailed warning
	///shown before combining.</summary>
	public static class InsPlanMergeMismatches {
		///<summary>FieldName of the benefits line.</summary>
		public const string FIELD_BENEFITS="Benefits";

		///<summary>One compared field: the insplan column, its name in the window, its value and how the value is shown.</summary>
		public class Field {
			public string FieldName;
			public string Label;
			public Func<InsPlan,object> FuncGetValue;
			///<summary>Shows a value to the user. Null shows it as text (true/false as Yes/No, an empty text as "(blank)").</summary>
			public Func<object,string> FuncFormat;

			public Field(string fieldName,string label,Func<InsPlan,object> funcGetValue,Func<object,string> funcFormat=null) {
				FieldName=fieldName;
				Label=label;
				FuncGetValue=funcGetValue;
				FuncFormat=funcFormat;
			}
		}

		///<summary>Every user-visible insplan column, in the order of the Edit Insurance Plan window, with the names to show. PlanNum, the security
		///columns (SecUserNumEntry, SecDateEntry, SecDateTEdit) and NumberSubscribers (not a column) are not compared.</summary>
		public static List<Field> GetFields() {
			Meth.NoCheckMiddleTierRole();
			return new List<Field>() {
				new Field(nameof(InsPlan.CarrierNum),"Carrier",x => x.CarrierNum,x => Carriers.GetName((long)x)),
				new Field(nameof(InsPlan.EmployerNum),"Employer",x => x.EmployerNum,x => Employers.GetName((long)x)),
				new Field(nameof(InsPlan.GroupName),"Group Name",x => x.GroupName),
				new Field(nameof(InsPlan.GroupNum),"Group Num",x => x.GroupNum),
				new Field(nameof(InsPlan.DivisionNo),"Div. No.",x => x.DivisionNo),
				new Field(nameof(InsPlan.TrojanID),"Trojan ID",x => x.TrojanID),
				new Field(nameof(InsPlan.RxBIN),"BIN",x => x.RxBIN),
				new Field(nameof(InsPlan.IsMedical),"Medical Insurance",x => x.IsMedical),
				new Field(nameof(InsPlan.PlanType),"Plan Type",x => x.PlanType,x => GetPlanTypeText((string)x)),
				new Field(nameof(InsPlan.FeeSched),"Fee Schedule",x => x.FeeSched,x => GetFeeSchedText((long)x)),
				new Field(nameof(InsPlan.CopayFeeSched),"Patient Co-pay Amounts",x => x.CopayFeeSched,x => GetFeeSchedText((long)x)),
				new Field(nameof(InsPlan.AllowedFeeSched),"Carrier Allowed Amounts: Out of Network",x => x.AllowedFeeSched,x => GetFeeSchedText((long)x)),
				new Field(nameof(InsPlan.ManualFeeSchedNum),"Carrier Allowed Amounts: Manual Blue Book",x => x.ManualFeeSchedNum,
					x => GetFeeSchedText((long)x)),
				new Field(nameof(InsPlan.IsBlueBookEnabled),"Use Blue Book",x => x.IsBlueBookEnabled),
				new Field(nameof(InsPlan.ClaimFormNum),"Claim Form",x => x.ClaimFormNum,x => GetClaimFormText((long)x)),
				new Field(nameof(InsPlan.FilingCode),"Filing Code",x => x.FilingCode,x => GetFilingCodeText((long)x)),
				new Field(nameof(InsPlan.FilingCodeSubtype),"Filing Code Subtype",x => x.FilingCodeSubtype,x => GetFilingCodeSubtypeText((long)x)),
				new Field(nameof(InsPlan.BillingType),"Billing Type",x => x.BillingType,x => GetBillingTypeText((long)x)),
				new Field(nameof(InsPlan.CobRule),"COB Rule",x => x.CobRule,x => ((EnumCobRule)x).ToString()),
				new Field(nameof(InsPlan.ExclusionFeeRule),"Exclusions",x => x.ExclusionFeeRule,x => ((ExclusionRule)x).GetDescription()),
				new Field(nameof(InsPlan.InsPlansZeroWriteOffsOnAnnualMaxOverride),"Write-Offs for Non-Covered Services: Annual Max",
					x => x.InsPlansZeroWriteOffsOnAnnualMaxOverride,x => GetYNText((YN)x)),
				new Field(nameof(InsPlan.InsPlansZeroWriteOffsOnFreqOrAgingOverride),"Write-Offs for Non-Covered Services: Age or Frequency Limit",
					x => x.InsPlansZeroWriteOffsOnFreqOrAgingOverride,x => GetYNText((YN)x)),
				new Field(nameof(InsPlan.HasPpoSubstWriteoffs),"PPO substitution calculate writeoffs",x => x.HasPpoSubstWriteoffs),
				new Field(nameof(InsPlan.UseAltCode),"Use Alternate Code",x => x.UseAltCode),
				new Field(nameof(InsPlan.ClaimsUseUCR),"Claims show standard fee, not billed fee",x => x.ClaimsUseUCR),
				new Field(nameof(InsPlan.ShowBaseUnits),"Claims show base units",x => x.ShowBaseUnits),
				new Field(nameof(InsPlan.CodeSubstNone),"Don't Substitute Codes",x => x.CodeSubstNone),
				new Field(nameof(InsPlan.IsHidden),"Hidden",x => x.IsHidden),
				new Field(nameof(InsPlan.HideFromVerifyList),"Don't Verify",x => x.HideFromVerifyList),
				new Field(nameof(InsPlan.MonthRenew),"Benefit Year Renew Month",x => x.MonthRenew,x => GetMonthRenewText((byte)x)),
				new Field(nameof(InsPlan.PerVisitPatAmount),"Per Visit Amounts: Patient Copay",x => x.PerVisitPatAmount,x => GetAmountText((double)x)),
				new Field(nameof(InsPlan.PerVisitInsAmount),"Per Visit Amounts: Insurance",x => x.PerVisitInsAmount,x => GetAmountText((double)x)),
				new Field(nameof(InsPlan.OrthoType),"Ortho Claim Type",x => x.OrthoType,x => ((OrthoClaimType)x).GetDescription()),
				new Field(nameof(InsPlan.OrthoAutoProcFreq),"Ortho Auto Proc Period",x => x.OrthoAutoProcFreq,x => ((OrthoAutoProcFrequency)x).ToString()),
				new Field(nameof(InsPlan.OrthoAutoProcCodeNumOverride),"Ortho Auto Proc",x => x.OrthoAutoProcCodeNumOverride,
					x => GetProcCodeText((long)x)),
				new Field(nameof(InsPlan.OrthoAutoFeeBilled),"Ortho Auto Fee",x => x.OrthoAutoFeeBilled,x => GetAmountText((double)x)),
				new Field(nameof(InsPlan.OrthoAutoClaimDaysWait),"Ortho: days to wait before the first automatic claim",x => x.OrthoAutoClaimDaysWait),
				new Field(nameof(InsPlan.CanadianPlanFlag),"Canadian: Plan Flag",x => x.CanadianPlanFlag),
				new Field(nameof(InsPlan.CanadianDiagnosticCode),"Canadian: Diagnostic Code",x => x.CanadianDiagnosticCode),
				new Field(nameof(InsPlan.CanadianInstitutionCode),"Canadian: Institution Code",x => x.CanadianInstitutionCode),
				new Field(nameof(InsPlan.DentaideCardSequence),"Canadian: Dentaide Card Sequence",x => x.DentaideCardSequence),
				new Field(nameof(InsPlan.SopCode),"SOP Code",x => x.SopCode),
				new Field(nameof(InsPlan.PlanNote),"Plan Note",x => x.PlanNote),
			};
		}

		///<summary>The fields of each other plan that differ from the kept plan, plan by plan in the order given, fields in the order of GetFields.
		///The kept plan itself is skipped wherever it is in the list. Values are shown with the fields' formats, so the caches they use (carriers,
		///employers, fee schedules, ...) must be available.</summary>
		public static List<InsPlanMismatch> GetMismatches(InsPlan insPlanKept,IEnumerable<InsPlan> listInsPlansOther) {
			Meth.NoCheckMiddleTierRole();
			return GetMismatches(insPlanKept,listInsPlansOther,GetFields());
		}

		///<summary>As GetMismatches, with the fields to compare.</summary>
		public static List<InsPlanMismatch> GetMismatches(InsPlan insPlanKept,IEnumerable<InsPlan> listInsPlansOther,List<Field> listFields) {
			List<InsPlanMismatch> listMismatches=new List<InsPlanMismatch>();
			if(insPlanKept==null || listInsPlansOther==null) {
				return listMismatches;
			}
			foreach(InsPlan insPlanOther in listInsPlansOther) {
				if(insPlanOther==null || insPlanOther.PlanNum==insPlanKept.PlanNum) {
					continue;
				}
				for(int i=0;i<listFields.Count;i++) {
					object valueKept=listFields[i].FuncGetValue(insPlanKept);
					object valueOther=listFields[i].FuncGetValue(insPlanOther);
					if(AreSame(valueKept,valueOther)) {
						continue;
					}
					listMismatches.Add(new InsPlanMismatch() {
						FieldName=listFields[i].FieldName,
						Label=listFields[i].Label,
						PlanNumOther=insPlanOther.PlanNum,
						ValueKept=Format(listFields[i],valueKept),
						ValueOther=Format(listFields[i],valueOther),
					});
				}
			}
			return listMismatches;
		}

		///<summary>True when two values of one field are the same. Texts must match exactly (a null text is an empty one); amounts are the same to
		///the cent.</summary>
		public static bool AreSame(object valueA,object valueB) {
			if(valueA is string || valueB is string) {
				return (valueA as string??"")==(valueB as string??"");
			}
			if(valueA is double doubleA && valueB is double doubleB) {
				return CompareDouble.IsEqual(doubleA,doubleB);
			}
			return Equals(valueA,valueB);
		}

		private static string Format(Field field,object value) {
			string text;
			try {
				if(field.FuncFormat!=null) {
					text=field.FuncFormat(value);
				}
				else if(value is bool isTrue) {
					text=isTrue ? "Yes" : "No";
				}
				else {
					text=Convert.ToString(value,CultureInfo.CurrentCulture);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
				text=Convert.ToString(value,CultureInfo.InvariantCulture);
			}
			if(string.IsNullOrWhiteSpace(text)) {
				return "(blank)";
			}
			return text;
		}

		#region Benefits (INFERRED: the release note names fields; combining deletes the other plans' benefits, so a difference is listed too)
		///<summary>A benefits line for each other plan whose plan-level benefits (PatPlanNum 0) are not the same set as the kept plan's. Benefits are
		///the same when every value but the keys and security columns matches, in any order. dictBenefits holds the benefits of every plan by PlanNum
		///(a plan with none may be missing).</summary>
		public static List<InsPlanMismatch> GetBenefitMismatches(InsPlan insPlanKept,IEnumerable<InsPlan> listInsPlansOther,
			Dictionary<long,List<Benefit>> dictBenefits)
		{
			List<InsPlanMismatch> listMismatches=new List<InsPlanMismatch>();
			if(insPlanKept==null || listInsPlansOther==null) {
				return listMismatches;
			}
			List<string> listKeysKept=GetBenefitKeys(GetBenefitsForPlan(dictBenefits,insPlanKept.PlanNum));
			foreach(InsPlan insPlanOther in listInsPlansOther) {
				if(insPlanOther==null || insPlanOther.PlanNum==insPlanKept.PlanNum) {
					continue;
				}
				List<string> listKeysOther=GetBenefitKeys(GetBenefitsForPlan(dictBenefits,insPlanOther.PlanNum));
				if(listKeysKept.SequenceEqual(listKeysOther)) {
					continue;
				}
				listMismatches.Add(new InsPlanMismatch() {
					FieldName=FIELD_BENEFITS,
					Label="Benefits",
					PlanNumOther=insPlanOther.PlanNum,
					ValueKept=GetBenefitCountText(listKeysKept.Count),
					ValueOther=GetBenefitCountText(listKeysOther.Count)+", "+"not the same",
				});
			}
			return listMismatches;
		}

		///<summary>The plan-level benefits (PatPlanNum 0) of these plans, by PlanNum. Plans without benefits are missing.</summary>
		public static Dictionary<long,List<Benefit>> GetBenefitsForPlans(List<long> listPlanNums) {
			Meth.NoCheckMiddleTierRole();
			Dictionary<long,List<Benefit>> dict=new Dictionary<long,List<Benefit>>();
			foreach(IGrouping<long,Benefit> group in GetPlanLevelBenefits(listPlanNums).GroupBy(x => x.PlanNum)) {
				dict[group.Key]=group.ToList();
			}
			return dict;
		}

		///<summary>The plan-level benefits (PatPlanNum 0) of these plans. Empty when no plan is given.</summary>
		public static List<Benefit> GetPlanLevelBenefits(List<long> listPlanNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Benefit>>(MethodBase.GetCurrentMethod(),listPlanNums);
			}
			if(listPlanNums==null || listPlanNums.Count==0) {
				return new List<Benefit>();
			}
			string command="SELECT * FROM benefit WHERE PatPlanNum=0 AND PlanNum IN("+string.Join(",",listPlanNums.Select(x => POut.Long(x)))+")";
			return Crud.BenefitCrud.SelectMany(command);
		}

		private static List<Benefit> GetBenefitsForPlan(Dictionary<long,List<Benefit>> dictBenefits,long planNum) {
			if(dictBenefits==null || !dictBenefits.TryGetValue(planNum,out List<Benefit> listBenefits) || listBenefits==null) {
				return new List<Benefit>();
			}
			return listBenefits.FindAll(x => x!=null && x.PatPlanNum==0);
		}

		///<summary>One text per benefit with every value that makes it what it is, sorted, so two sets can be compared in any order.</summary>
		public static List<string> GetBenefitKeys(List<Benefit> listBenefits) {
			return listBenefits.Select(x => string.Join("|",
				x.CovCatNum,(int)x.BenefitType,x.Percent,x.MonetaryAmt.ToString("F2",CultureInfo.InvariantCulture),(int)x.TimePeriod,
				(int)x.QuantityQualifier,x.Quantity,x.CodeNum,(int)x.CoverageLevel,x.CodeGroupNum,(int)x.TreatArea,x.ToothRange??""))
				.OrderBy(x => x,StringComparer.Ordinal)
				.ToList();
		}

		private static string GetBenefitCountText(int count) {
			return count==1 ? "1 benefit" : count+" benefits";
		}
		#endregion Benefits

		///<summary>How the plan is described in the warning: carrier, group number and employer, like the rows of Combine Insurance Plans.</summary>
		public static string GetPlanDescription(InsPlan insPlan) {
			Meth.NoCheckMiddleTierRole();
			if(insPlan==null) {
				return "";
			}
			List<string> listParts=new List<string>();
			listParts.Add(Carriers.GetName(insPlan.CarrierNum));
			if(!string.IsNullOrWhiteSpace(insPlan.GroupNum)) {
				listParts.Add("#"+insPlan.GroupNum);
			}
			string employer=Employers.GetName(insPlan.EmployerNum);
			if(!string.IsNullOrWhiteSpace(employer)) {
				listParts.Add(employer);
			}
			return string.Join(", ",listParts.Where(x => !string.IsNullOrWhiteSpace(x)))+" (PlanNum "+insPlan.PlanNum+")";
		}

		#region Formats
		///<summary>The Plan Type names of the Edit Insurance Plan window.</summary>
		public static string GetPlanTypeText(string planType) {
			switch(planType??"") {
				case "":
					return "Category Percentage";
				case "p":
					return "PPO Percentage";
				case "f":
					return "Medicaid or Flat Co-pay";
				case "c":
					return "Capitation";
				default:
					return planType;
			}
		}

		public static string GetMonthRenewText(byte monthRenew) {
			if(monthRenew<1 || monthRenew>12) {
				return "Calendar Year";
			}
			return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(monthRenew);
		}

		public static string GetYNText(YN yn) {
			switch(yn) {
				case YN.Yes:
					return "Yes";
				case YN.No:
					return "No";
				default:
					return "Use practice default";
			}
		}

		public static string GetAmountText(double amount) {
			return amount.ToString("F2",CultureInfo.CurrentCulture);
		}

		private static string GetFeeSchedText(long feeSchedNum) {
			if(feeSchedNum==0) {
				return "none";
			}
			return FeeScheds.GetDescription(feeSchedNum);
		}

		private static string GetClaimFormText(long claimFormNum) {
			if(claimFormNum==0) {
				return "none";
			}
			return ClaimForms.GetClaimForm(claimFormNum)?.Description??claimFormNum.ToString();
		}

		private static string GetFilingCodeText(long insFilingCodeNum) {
			if(insFilingCodeNum==0) {
				return "none";
			}
			return InsFilingCodes.GetOne(insFilingCodeNum)?.Descript??insFilingCodeNum.ToString();
		}

		private static string GetFilingCodeSubtypeText(long insFilingCodeSubtypeNum) {
			if(insFilingCodeSubtypeNum==0) {
				return "none";
			}
			return InsFilingCodeSubtypes.GetOne(insFilingCodeSubtypeNum)?.Descript??insFilingCodeSubtypeNum.ToString();
		}

		private static string GetBillingTypeText(long defNum) {
			if(defNum==0) {
				return "none";
			}
			return Defs.GetName(DefCat.BillingTypes,defNum);
		}

		private static string GetProcCodeText(long codeNum) {
			if(codeNum==0) {
				return "default";
			}
			return ProcedureCodes.GetStringProcCode(codeNum,doThrowIfMissing:false);
		}
		#endregion Formats
	}
}
