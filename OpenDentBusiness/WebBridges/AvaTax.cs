using System;
using CodeBase;

namespace OpenDentBusiness {

	///<summary>Sales tax through Avalara's AvaTax service was only ever enabled at Open Dental HQ, for billing its own customers, and this build is never
	///HQ, so AvaTax is always disabled. The bridge to Avalara's SDK (Avalara.AvaTax.RestClient.net45.dll, which is no longer referenced or shipped) and
	///its setup window were removed. What is left keeps the callers' disabled paths as they were: every check says no, and the program properties
	///that existing adjustments may refer to are still read. The AvaTax row of the program table is kept and stays hidden in Program Links.</summary>
	public class AvaTax {

		#region Methods - Public
		///<summary>Always false. AvaTax was only ever enabled at Open Dental HQ, for billing its own customers, and this build is never HQ.</summary>
		public static bool IsEnabled() {
			return false;
		}

		///<summary>Always false: AvaTax is never enabled.</summary>
		public static bool IsTaxable(long patNum) {
			return false;
		}

		///<summary>Always false: AvaTax is never enabled, so no procedure is sent to Avalara and procedures are created without tax, as before.</summary>
		public static bool DoSendProcToAvalara(Procedure procedure,bool isSilent=false) {
			return false;
		}

		///<summary>Always false: there is no connection to Avalara.</summary>
		public static bool PingAvaTax() {
			return false;
		}

		///<summary>Always 0: AvaTax is never enabled, so nothing is taxable.</summary>
		public static decimal GetEstimate(long codeNum,long patNum,double procFee,bool hasExceptions=false) {
			return 0;
		}

		///<summary>Always false: no sales tax adjustment is ever created or changed.</summary>
		public static bool DidUpdateAdjustment(Procedure procedure,Adjustment adjustment) {
			return false;
		}

		///<summary>Always false: no sales tax return adjustment is ever created.</summary>
		public static bool DoCreateReturnAdjustment(Procedure procedure,Adjustment adjustmentLocked,Adjustment adjustmentReturn) {
			return false;
		}

		///<summary>A program property.  The adjustment type defnum to be associated with SalesTax adjustments.</summary>
		public static long GetSalesTaxAdjType() {
			return PIn.Long(ProgramProperties.GetPropVal(Programs.GetProgramNum(ProgramName.AvaTax),"Sales Tax Adjustment Type"));
		}

		///<summary>A program property.  The adjustment type defnum to be associated with SalesTax return adjustments.</summary>
		public static long GetSalesTaxReturnAdjType() {
			return PIn.Long(ProgramProperties.GetPropVal(Programs.GetProgramNum(ProgramName.AvaTax),"Sales Tax Return Adjustment Type"));
		}

		///<summary>A program property.  Returns the date after which we will consider tax adjustments "un-locked" and directly editable.
		///Example: lock date of 3/31/2019, so locked on that date, and unlocked if on 4/1/2019.</summary>
		public static DateTime GetTaxLockDate() {
			return PIn.Date(ProgramProperties.GetPropVal(Programs.GetProgramNum(ProgramName.AvaTax),"Tax Lock Date"));
		}
		#endregion Methods - Public
	}
}
