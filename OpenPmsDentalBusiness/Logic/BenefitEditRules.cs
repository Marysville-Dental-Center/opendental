using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Rules of the redesigned Edit Benefit window (Open Dental 25.2): which inputs each benefit type uses, the check that blocks saving a
	///value the type ignores, and the plain-language preview line. The save-time checks in Benefits.GetValidationErrors stay as a backstop.
	///Time period and coverage level are used by every type (X12 sends them with any benefit), so they are always available.</summary>
	public class BenefitEditRules {
		///<summary>The fields whose use depends on the benefit type. The others (Pat, Level, Type, Category, TimePeriod) apply to every type.</summary>
		public static readonly EnumBenefitField[] ArrayFieldsByType=new EnumBenefitField[] {
			EnumBenefitField.Percent,
			EnumBenefitField.Amount,
			EnumBenefitField.Quantity,
			EnumBenefitField.Qualifier,
			EnumBenefitField.TreatArea,
			EnumBenefitField.Teeth,
		};

		///<summary>True when a benefit of this type uses the field. Percent: coinsurance. Amount: limitations (maximums), deductibles and copayments,
		///the types Benefits.GetValidationErrors allows an amount for. Quantity and qualifier: limitations (frequency and age limits), waiting periods
		///and copayments (per visit). Treatment area: limitations. Teeth: exclusions (Open Dental 25.3); age limitations too (26.1), which depends on
		///the qualifier, so the checks of one benefit (GetIgnoredFieldErrors, GetPreviewText) use Benefits.CanHaveTeeth instead.</summary>
		public static bool IsFieldUsed(InsBenefitType insBenefitType,EnumBenefitField enumBenefitField) {
			Meth.NoCheckMiddleTierRole();
			switch(enumBenefitField) {
				case EnumBenefitField.Percent:
					return insBenefitType==InsBenefitType.CoInsurance;
				case EnumBenefitField.Amount:
					return insBenefitType.In(InsBenefitType.Limitations,InsBenefitType.Deductible,InsBenefitType.CoPayment);
				case EnumBenefitField.Quantity:
				case EnumBenefitField.Qualifier:
					return insBenefitType.In(InsBenefitType.Limitations,InsBenefitType.WaitingPeriod,InsBenefitType.CoPayment);
				case EnumBenefitField.TreatArea:
					return insBenefitType==InsBenefitType.Limitations;
				case EnumBenefitField.Teeth:
					return insBenefitType==InsBenefitType.Exclusions;
				default:
					return true;
			}
		}

		///<summary>IsFieldUsed for one benefit. Teeth also depend on the quantity qualifier: age limitations may have teeth too (Open Dental 26.1,
		///package M2-EST, Benefits.CanHaveTeeth). Every other field depends on the type only.</summary>
		private static bool IsFieldUsed(Benefit benefit,EnumBenefitField enumBenefitField) {
			if(enumBenefitField==EnumBenefitField.Teeth) {
				return Benefits.CanHaveTeeth(benefit.BenefitType,benefit.QuantityQualifier);
			}
			return IsFieldUsed(benefit.BenefitType,enumBenefitField);
		}

		///<summary>True when the benefit holds a value in the field: a percent or amount other than -1 (blank), a quantity other than 0, a qualifier
		///or treatment area other than none, or teeth.</summary>
		public static bool HasValue(Benefit benefit,EnumBenefitField enumBenefitField) {
			Meth.NoCheckMiddleTierRole();
			switch(enumBenefitField) {
				case EnumBenefitField.Percent:
					return benefit.Percent!=-1;
				case EnumBenefitField.Amount:
					return benefit.MonetaryAmt!=-1;
				case EnumBenefitField.Quantity:
					return benefit.Quantity!=0;
				case EnumBenefitField.Qualifier:
					return benefit.QuantityQualifier!=BenefitQuantity.None;
				case EnumBenefitField.TreatArea:
					return benefit.TreatArea!=TreatmentArea.None;
				case EnumBenefitField.Teeth:
					return !string.IsNullOrWhiteSpace(benefit.ToothRange);
				default:
					return false;
			}
		}

		///<summary>The name of a field as the Edit Benefit window labels it.</summary>
		public static string GetFieldName(EnumBenefitField enumBenefitField) {
			Meth.NoCheckMiddleTierRole();
			switch(enumBenefitField) {
				case EnumBenefitField.Pat:
					return Lans.g("BenefitEditRules","Patient Override");
				case EnumBenefitField.Level:
					return Lans.g("BenefitEditRules","Coverage Level");
				case EnumBenefitField.TimePeriod:
					return Lans.g("BenefitEditRules","Time Period");
				case EnumBenefitField.Qualifier:
					return Lans.g("BenefitEditRules","Qualifier");
				case EnumBenefitField.TreatArea:
					return Lans.g("BenefitEditRules","Treatment Area");
				default:
					return Lans.g("BenefitEditRules",enumBenefitField.ToString());
			}
		}

		///<summary>The type as the preview line names it, for example "Limitation".</summary>
		public static string GetTypeName(InsBenefitType insBenefitType) {
			Meth.NoCheckMiddleTierRole();
			switch(insBenefitType) {
				case InsBenefitType.ActiveCoverage:
					return Lans.g("BenefitEditRules","Active Coverage");
				case InsBenefitType.CoInsurance:
					return Lans.g("BenefitEditRules","Coinsurance");
				case InsBenefitType.Deductible:
					return Lans.g("BenefitEditRules","Deductible");
				case InsBenefitType.CoPayment:
					return Lans.g("BenefitEditRules","Copayment");
				case InsBenefitType.Exclusions:
					return Lans.g("BenefitEditRules","Exclusion");
				case InsBenefitType.Limitations:
					return Lans.g("BenefitEditRules","Limitation");
				case InsBenefitType.WaitingPeriod:
					return Lans.g("BenefitEditRules","Waiting Period");
				default:
					return insBenefitType.ToString();
			}
		}

		///<summary>The time period as the preview line names it, for example "Calendar Year". Empty for none.</summary>
		public static string GetTimePeriodName(BenefitTimePeriod benefitTimePeriod) {
			Meth.NoCheckMiddleTierRole();
			switch(benefitTimePeriod) {
				case BenefitTimePeriod.ServiceYear:
					return Lans.g("BenefitEditRules","Service Year");
				case BenefitTimePeriod.CalendarYear:
					return Lans.g("BenefitEditRules","Calendar Year");
				case BenefitTimePeriod.Lifetime:
					return Lans.g("BenefitEditRules","Lifetime");
				case BenefitTimePeriod.Years:
					return Lans.g("BenefitEditRules","Years");
				case BenefitTimePeriod.NumberInLast12Months:
					return Lans.g("BenefitEditRules","Last 12 Months");
				default:
					return "";
			}
		}

		///<summary>Values the benefit's type ignores, one error per field, naming the field. A value the benefit already had when the window opened,
		///with the same type, is allowed, so benefits saved by older versions or imported from a 271 still save unchanged. benefitLoaded is the
		///benefit as the window opened it; null checks every value.</summary>
		public static List<BenefitValidationError> GetIgnoredFieldErrors(Benefit benefit,Benefit benefitLoaded) {
			Meth.NoCheckMiddleTierRole();
			List<BenefitValidationError> listErrors=new List<BenefitValidationError>();
			for(int i=0;i<ArrayFieldsByType.Length;i++) {
				EnumBenefitField enumBenefitField=ArrayFieldsByType[i];
				if(IsFieldUsed(benefit,enumBenefitField) || !HasValue(benefit,enumBenefitField)) {
					continue;
				}
				if(benefitLoaded!=null && benefitLoaded.BenefitType==benefit.BenefitType && IsSameValue(benefit,benefitLoaded,enumBenefitField)) {
					continue;
				}
				listErrors.Add(new BenefitValidationError(enumBenefitField,GetFieldName(enumBenefitField)+" "
					+Lans.g("BenefitEditRules","is not used by")+" "+GetTypeName(benefit.BenefitType)+" "+Lans.g("BenefitEditRules","benefits.")+" "
					+Lans.g("BenefitEditRules","Clear it, or pick another type.")));
			}
			return listErrors;
		}

		private static bool IsSameValue(Benefit benefit,Benefit benefitOther,EnumBenefitField enumBenefitField) {
			switch(enumBenefitField) {
				case EnumBenefitField.Percent:
					return benefit.Percent==benefitOther.Percent;
				case EnumBenefitField.Amount:
					return CompareDouble.IsEqual(benefit.MonetaryAmt,benefitOther.MonetaryAmt);
				case EnumBenefitField.Quantity:
					return benefit.Quantity==benefitOther.Quantity;
				case EnumBenefitField.Qualifier:
					return benefit.QuantityQualifier==benefitOther.QuantityQualifier;
				case EnumBenefitField.TreatArea:
					return benefit.TreatArea==benefitOther.TreatArea;
				case EnumBenefitField.Teeth:
					return (benefit.ToothRange??"")==(benefitOther.ToothRange??"");
				default:
					return false;
			}
		}

		///<summary>The plain-language line at the bottom of the Edit Benefit window, for example
		///"Limitation: 2 per Calendar Year, Individual, Category: Preventive". Names are passed in so the line shows what the window shows:
		///categoryName for a category benefit, procCode or codeGroupName for the other two (blank for none, which reads "General"). teeth is the
		///text of the Teeth box; it shows only for types that use teeth.</summary>
		public static string GetPreviewText(Benefit benefit,string categoryName,string procCode,string codeGroupName,string teeth,bool isPat) {
			Meth.NoCheckMiddleTierRole();
			List<string> listParts=new List<string>();
			if(benefit.Percent!=-1) {
				listParts.Add(benefit.Percent.ToString()+"%");
			}
			if(benefit.MonetaryAmt!=-1) {
				listParts.Add(benefit.MonetaryAmt.ToString("c"));
			}
			string timePeriod=GetTimePeriodName(benefit.TimePeriod);
			string quantity=GetQuantityText(benefit,timePeriod,out bool isTimePeriodUsed);
			if(quantity!="") {
				listParts.Add(quantity);
			}
			if(timePeriod!="" && !isTimePeriodUsed) {
				listParts.Add(timePeriod);
			}
			if(benefit.CoverageLevel!=BenefitCoverageLevel.None) {
				listParts.Add(Lans.g("enumBenefitCoverageLevel",benefit.CoverageLevel.ToString()));
			}
			if(benefit.TreatArea!=TreatmentArea.None) {
				listParts.Add(Lans.g("BenefitEditRules","Treatment Area")+": "+Lans.g("enumTreatmentArea",benefit.TreatArea.ToString()));
			}
			if(!string.IsNullOrWhiteSpace(teeth) && IsFieldUsed(benefit,EnumBenefitField.Teeth)) {
				listParts.Add(Lans.g("BenefitEditRules","teeth")+" "+teeth.Trim());
			}
			if(!string.IsNullOrWhiteSpace(procCode)) {
				listParts.Add(Lans.g("BenefitEditRules","Code")+": "+procCode.Trim());
			}
			else if(!string.IsNullOrWhiteSpace(codeGroupName)) {
				listParts.Add(Lans.g("BenefitEditRules","Code Group")+": "+codeGroupName.Trim());
			}
			else if(!string.IsNullOrWhiteSpace(categoryName)) {
				listParts.Add(Lans.g("BenefitEditRules","Category")+": "+categoryName.Trim());
			}
			else {
				listParts.Add(Lans.g("BenefitEditRules","General"));
			}
			if(isPat) {
				listParts.Add(Lans.g("BenefitEditRules","Patient Override"));
			}
			return GetTypeName(benefit.BenefitType)+": "+string.Join(", ",listParts);
		}

		///<summary>The quantity in words: "2 per Calendar Year", "through age 18", "every 6 months", "1 visit". A waiting period reads "6 months".
		///isTimePeriodUsed is true when the text already holds the time period.</summary>
		private static string GetQuantityText(Benefit benefit,string timePeriod,out bool isTimePeriodUsed) {
			isTimePeriodUsed=false;
			string quantity=benefit.Quantity.ToString();
			if(benefit.Quantity==0) {
				if(benefit.QuantityQualifier==BenefitQuantity.None) {
					return "";
				}
				return Lans.g("enumBenefitQuantity",benefit.QuantityQualifier.ToString());
			}
			switch(benefit.QuantityQualifier) {
				case BenefitQuantity.NumberOfServices:
					if(timePeriod=="") {
						return quantity+" "+Lans.g("BenefitEditRules","services");
					}
					isTimePeriodUsed=true;
					if(benefit.TimePeriod==BenefitTimePeriod.NumberInLast12Months) {
						return quantity+" "+Lans.g("BenefitEditRules","in the")+" "+timePeriod;
					}
					return quantity+" "+Lans.g("BenefitEditRules","per")+" "+timePeriod;
				case BenefitQuantity.AgeLimit:
					return Lans.g("BenefitEditRules","through age")+" "+quantity;
				case BenefitQuantity.Visits:
					return quantity+" "+(benefit.Quantity==1 ? Lans.g("BenefitEditRules","visit") : Lans.g("BenefitEditRules","visits"));
				case BenefitQuantity.Years:
					if(benefit.BenefitType==InsBenefitType.WaitingPeriod) {
						return quantity+" "+Lans.g("BenefitEditRules","years");
					}
					return Lans.g("BenefitEditRules","every")+" "+quantity+" "+Lans.g("BenefitEditRules","years");
				case BenefitQuantity.Months:
					if(benefit.BenefitType==InsBenefitType.WaitingPeriod) {
						return quantity+" "+Lans.g("BenefitEditRules","months");
					}
					return Lans.g("BenefitEditRules","every")+" "+quantity+" "+Lans.g("BenefitEditRules","months");
				default:
					return Lans.g("BenefitEditRules","quantity")+" "+quantity;
			}
		}
	}
}
