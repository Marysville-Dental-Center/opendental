// M2-EST: teeth on age limitations (Open Dental 26.1) and benefit periods for treatment plan estimates as of the appointment date (25.4).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace OpenPmsDentalBusiness {
	public partial class Benefits {
		///<summary>Whether a benefit of this type and quantity qualifier may be limited to teeth (benefit.ToothRange): exclusions (Open Dental 25.3)
		///and age limitations (Open Dental 26.1). Any other benefit applies to every tooth and its Teeth box stays empty.</summary>
		public static bool CanHaveTeeth(InsBenefitType insBenefitType,BenefitQuantity benefitQuantity) {
			Meth.NoCheckMiddleTierRole();
			if(insBenefitType==InsBenefitType.Exclusions) {
				return true;
			}
			return insBenefitType==InsBenefitType.Limitations && benefitQuantity==BenefitQuantity.AgeLimit;
		}

		///<summary>Whether an age limitation applies to a procedure with these teeth (Open Dental 26.1).
		///<para>A row with teeth applies only when the procedure's tooth, or any tooth of its tooth range, is in the row's list. A procedure with no tooth
		///(mouth, quadrant, arch) never matches a row with teeth.</para>
		///<para>A row without teeth applies to every procedure, except one whose teeth match a row with teeth for the same code group, category or
		///procedure code: when both kinds exist, the row with teeth governs its teeth and the row without teeth everything else (INFERRED
		///precedence, from the 26.1 spec).</para>
		///listBenefits holds the other limitation rows to compare with (the short list of GetLimitationByCode). Exclusions never use this: they
		///never consider age.</summary>
		public static bool IsAgeLimitForTeeth(Benefit benefitAgeLimit,List<Benefit> listBenefits,string toothNum,string toothRange) {
			Meth.NoCheckMiddleTierRole();
			if(benefitAgeLimit==null) {
				return false;
			}
			if(!string.IsNullOrWhiteSpace(benefitAgeLimit.ToothRange)) {
				return Tooth.IsAnyToothInList(benefitAgeLimit.ToothRange,toothNum,toothRange);
			}
			if(listBenefits==null) {
				return true;
			}
			for(int i=0;i<listBenefits.Count;i++) {
				Benefit benefit=listBenefits[i];
				if(benefit==null || ReferenceEquals(benefit,benefitAgeLimit) || string.IsNullOrWhiteSpace(benefit.ToothRange)) {
					continue;
				}
				if(benefit.QuantityQualifier!=BenefitQuantity.AgeLimit || benefit.Quantity<=0 || !IsSameAgeLimitScope(benefit,benefitAgeLimit)) {
					continue;
				}
				if(Tooth.IsAnyToothInList(benefit.ToothRange,toothNum,toothRange)) {
					return false;//The row with teeth governs this procedure.
				}
			}
			return true;
		}

		///<summary>True when two age limitations limit the same procedures: same type, code group, category and procedure code, and both family or
		///both not family.</summary>
		private static bool IsSameAgeLimitScope(Benefit benefit,Benefit benefitOther) {
			return benefit.BenefitType==benefitOther.BenefitType
				&& benefit.CodeGroupNum==benefitOther.CodeGroupNum
				&& benefit.CovCatNum==benefitOther.CovCatNum
				&& benefit.CodeNum==benefitOther.CodeNum
				&& (benefit.CoverageLevel==BenefitCoverageLevel.Family)==(benefitOther.CoverageLevel==BenefitCoverageLevel.Family);
		}

		///<summary>The benefit period (benefit year) that contains the date, from the plan's renewal month: the same period the annual max and the
		///deductible use for history.</summary>
		public static void GetBenefitPeriod(DateTime date,InsPlan insPlan,out DateTime dateStart,out DateTime dateEnd) {
			Meth.NoCheckMiddleTierRole();
			dateStart=BenefitLogic.ComputeRenewDate(date,insPlan?.MonthRenew??0);
			dateEnd=dateStart.AddYears(1).AddDays(-1);
		}

		///<summary>Whether a loop entry (an earlier procedure of the same treatment plan or claim) counts toward the benefit period dateStart to dateEnd.
		///Entries are counted whatever their date, as before 25.4, unless they are Treatment Plan module entries estimated as of their own date
		///(ClaimProcHist.IsDateAsOf, Open Dental 25.4): those count only toward the period that contains their date, so a procedure scheduled next
		///benefit year draws on next year's max and deductible, and procedures of this year do not reduce next year's. Lifetime benefits count every
		///entry.</summary>
		public static bool IsLoopHistInPeriod(ClaimProcHist claimProcHist,Benefit benefit,DateTime dateStart,DateTime dateEnd) {
			Meth.NoCheckMiddleTierRole();
			if(claimProcHist==null || !claimProcHist.IsDateAsOf) {
				return true;
			}
			if(benefit!=null && benefit.TimePeriod==BenefitTimePeriod.Lifetime) {
				return true;
			}
			DateTime date=claimProcHist.ProcDate.Date;
			return date>=dateStart.Date && date<=dateEnd.Date;
		}
	}
}
