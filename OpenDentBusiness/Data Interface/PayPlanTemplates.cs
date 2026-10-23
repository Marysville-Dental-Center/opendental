using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenDentBusiness{
	///<summary></summary>
	public class PayPlanTemplates{
		#region Methods - Get
		///<summary>Gets all PayPlanTemplates from the db.</summary>
		public static List<PayPlanTemplate> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PayPlanTemplate>>(MethodBase.GetCurrentMethod());
			}
			string command = "SELECT * FROM payplantemplate";
			return Crud.PayPlanTemplateCrud.SelectMany(command);
		}

		public static List<PayPlanTemplate> GetMany(long clinicNum = 0) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PayPlanTemplate>>(MethodBase.GetCurrentMethod(),clinicNum);
			}
			string command = "SELECT * FROM payplantemplate WHERE ClinicNum = "+POut.Long(clinicNum);
			return Crud.PayPlanTemplateCrud.SelectMany(command);
		}
		#endregion Methods - Get

		#region Methods - Modify
		///<summary></summary>
		public static long Insert(PayPlanTemplate payPlanTemplate) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				payPlanTemplate.PayPlanTemplateNum=Meth.GetLong(MethodBase.GetCurrentMethod(),payPlanTemplate);
				return payPlanTemplate.PayPlanTemplateNum;
			}
			return Crud.PayPlanTemplateCrud.Insert(payPlanTemplate);
		}
		///<summary></summary>
		public static void Update(PayPlanTemplate payPlanTemplate) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),payPlanTemplate);
				return;
			}
			Crud.PayPlanTemplateCrud.Update(payPlanTemplate);
		}
		#endregion Methods - Modify

		#region Methods - Misc
		/// <summary>Gets all of the templates from the db. Convertes them into Terms objects and then returns a list of terms. </summary>
		public static PayPlanTerms ConvertTemplateToTerms(PayPlanTemplate payPlanTemplate) {
			Meth.NoCheckMiddleTierRole();
			PayPlanTerms payPlanTerms=new PayPlanTerms();
			payPlanTerms.APR=payPlanTemplate.APR;
			payPlanTerms.PeriodPayment=(decimal)payPlanTemplate.PayAmt;
			payPlanTerms.PayCount=payPlanTemplate.NumberOfPayments;//This may need to be removed.
			payPlanTerms.RoundDec=CultureInfo.CurrentCulture.NumberFormat.NumberDecimalDigits;
			payPlanTerms.Frequency=payPlanTemplate.ChargeFrequency;
			payPlanTerms.DownPayment=payPlanTemplate.DownPayment;
			payPlanTerms.DynamicPayPlanTPOption=payPlanTemplate.DynamicPayPlanTPOption;
			return payPlanTerms;
		}

		///<summary>The SheetDefNum a payment plan gets when it is made from this template: the template's sheet when it is still a PaymentPlan sheet
		///def, otherwise 0 (the default sheet).</summary>
		public static long GetSheetDefNumForPayPlan(PayPlanTemplate payPlanTemplate) {
			Meth.NoCheckMiddleTierRole();
			if(payPlanTemplate==null || !SheetDefs.IsValidPayPlanSheetDefNum(payPlanTemplate.SheetDefNum)) {
				return 0;
			}
			return payPlanTemplate.SheetDefNum;
		}

		///<summary>Copies the template's sheet choice to the payment plan (see GetSheetDefNumForPayPlan). Does not save the plan.</summary>
		public static void ApplySheetDefToPayPlan(PayPlanTemplate payPlanTemplate,PayPlan payPlan) {
			Meth.NoCheckMiddleTierRole();
			payPlan.SheetDefNum=GetSheetDefNumForPayPlan(payPlanTemplate);
		}

		///<summary>The name a payment plan sheet choice has in the audit trail: "Default" for 0, the sheet def's description, or "(deleted)" when
		///the sheet def no longer exists.</summary>
		public static string GetSheetDefNameForLog(long sheetDefNum) {
			Meth.NoCheckMiddleTierRole();
			if(sheetDefNum==0) {
				return "Default";
			}
			SheetDef sheetDef=SheetDefs.GetFirstOrDefault(x => x.SheetDefNum==sheetDefNum);
			if(sheetDef==null) {
				return "(deleted)";
			}
			return sheetDef.Description;
		}

		///<summary>24.4: logs a PayPlanEdit entry when a saved payment plan's print sheet changed ("Payment Plan sheet changed from X to Y.").
		///Nothing is logged for a new plan (its creation is logged instead) or when the sheet did not change. Returns true when an entry was made.
		///</summary>
		public static bool MakeLogEntryIfSheetChanged(PayPlan payPlan,PayPlan payPlanOld) {
			Meth.NoCheckMiddleTierRole();
			if(payPlan==null || payPlanOld==null || payPlanOld.IsNew || payPlanOld.SheetDefNum==payPlan.SheetDefNum) {
				return false;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.PayPlanEdit,payPlan.PatNum,
				"Payment Plan sheet changed from "+GetSheetDefNameForLog(payPlanOld.SheetDefNum)+" to "+GetSheetDefNameForLog(payPlan.SheetDefNum)+".",
				payPlan.PayPlanNum,DateTime.MinValue);
			return true;
		}
		#endregion Methods - Misc

		//Only pull out the methods below as you need them.  Otherwise, leave them commented out.
		//#region Methods - Get
		/////<summary></summary>
		//public static List<PayPlanTemplate> Refresh(long patNum){
		//	if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
		//		return Meth.GetObject<List<PayPlanTemplate>>(MethodBase.GetCurrentMethod(),patNum);
		//	}
		//	string command="SELECT * FROM payplantemplate WHERE PatNum = "+POut.Long(patNum);
		//	return Crud.PayPlanTemplateCrud.SelectMany(command);
		//}

		/////<summary>Gets one PayPlanTemplate from the db.</summary>
		//public static PayPlanTemplate GetOne(long payPlanTemplateNum){
		//	if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
		//		return Meth.GetObject<PayPlanTemplate>(MethodBase.GetCurrentMethod(),payPlanTemplateNum);
		//	}
		//	return Crud.PayPlanTemplateCrud.SelectOne(payPlanTemplateNum);
		//}
		//#endregion Methods - Get

		//#region Methods - Modify
		/////<summary></summary>
		//public static void Delete(long payPlanTemplateNum) {
		//	if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
		//		Meth.GetVoid(MethodBase.GetCurrentMethod(),payPlanTemplateNum);
		//		return;
		//	}
		//	Crud.PayPlanTemplateCrud.Delete(payPlanTemplateNum);
		//}
		//#endregion Methods - Modify
	}
}