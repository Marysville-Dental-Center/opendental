using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace OpenDentBusiness {
	///<summary>Insurance Pending Supplementals report (Open Dental 25.2, Reports > Standard > Lists). Lists every outstanding pending supplemental:
	///a claimproc that Edit Claim created for an insurance overpayment or underpayment (IsOverpay=1) and that is still NotReceived. Its expected
	///amount is InsEstTotalOverride: negative for an overpayment (the office owes the carrier a refund), positive for an underpayment (the carrier
	///owes the office more). A row disappears when its amount is cleared or set to zero (Edit Claim deletes the claimproc) or when it is turned into
	///a supplemental payment (IsOverpay=0, Status=Supplemental).</summary>
	public class RpInsPendingSupplementals {
		///<summary>Which pending supplementals the report shows.</summary>
		public enum PendingSupplementalFilter {
			All,
			///<summary>Negative amounts: the carrier overpaid.</summary>
			Overpaid,
			///<summary>Positive amounts: the carrier underpaid.</summary>
			Underpaid,
		}

		///<summary>Overpaid for a negative amount, Underpaid for a positive one, All for zero (zero amounts are never listed).</summary>
		public static PendingSupplementalFilter GetKind(double amount) {
			Meth.NoCheckMiddleTierRole();
			double amountRounded=Math.Round(amount,2);
			if(amountRounded<0) {
				return PendingSupplementalFilter.Overpaid;
			}
			if(amountRounded>0) {
				return PendingSupplementalFilter.Underpaid;
			}
			return PendingSupplementalFilter.All;
		}

		///<summary>Rows ordered by date of service and patient, with the columns ClaimProcNum, ClaimNum, PatNum, ProcNum, ProcDate, PatName, CarrierName,
		///ClinicNum, Amount and DateSent (the claim's date sent). listClinicNums limits the rows to those clinics (pass the clinics the user may see);
		///an empty list means no clinic filter, for offices without clinics. dateFrom and dateTo limit the date of service; DateTime.MinValue means no
		///limit on that side.</summary>
		public static DataTable GetTable(List<long> listClinicNums,PendingSupplementalFilter filter,DateTime dateFrom,DateTime dateTo) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),listClinicNums,filter,dateFrom,dateTo);
			}
			string query=GetQuery(listClinicNums,filter,dateFrom,dateTo);
			return ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(query));
		}

		///<summary>The SQL of the report. Public for the report tests.</summary>
		public static string GetQuery(List<long> listClinicNums,PendingSupplementalFilter filter,DateTime dateFrom,DateTime dateTo) {
			Meth.NoCheckMiddleTierRole();
			string query="SELECT claimproc.ClaimProcNum,claimproc.ClaimNum,claimproc.PatNum,claimproc.ProcNum,claimproc.ProcDate,"
				+"CONCAT(patient.LName,', ',patient.FName) PatName,COALESCE(carrier.CarrierName,'') CarrierName,claimproc.ClinicNum,"
				+"claimproc.InsEstTotalOverride Amount,COALESCE(claim.DateSent,"+POut.Date(DateTime.MinValue)+") DateSent "
				+"FROM claimproc "
				+"INNER JOIN patient ON patient.PatNum=claimproc.PatNum "
				+"LEFT JOIN claim ON claim.ClaimNum=claimproc.ClaimNum "
				+"LEFT JOIN insplan ON insplan.PlanNum=claimproc.PlanNum "
				+"LEFT JOIN carrier ON carrier.CarrierNum=insplan.CarrierNum "
				+"WHERE claimproc.IsOverpay=1 "
				+"AND claimproc.Status="+POut.Int((int)ClaimProcStatus.NotReceived)+" ";
			switch(filter) {
				case PendingSupplementalFilter.Overpaid:
					query+="AND ROUND(claimproc.InsEstTotalOverride,2)<0 ";
					break;
				case PendingSupplementalFilter.Underpaid:
					query+="AND ROUND(claimproc.InsEstTotalOverride,2)>0 ";
					break;
				default:
					query+="AND ROUND(claimproc.InsEstTotalOverride,2)!=0 ";
					break;
			}
			if(listClinicNums!=null && listClinicNums.Count>0) {
				query+="AND claimproc.ClinicNum IN ("+string.Join(",",listClinicNums.Select(x => POut.Long(x)))+") ";
			}
			if(dateFrom.Year>1880) {
				query+="AND claimproc.ProcDate>="+POut.Date(dateFrom)+" ";
			}
			if(dateTo.Year>1880) {
				query+="AND claimproc.ProcDate<="+POut.Date(dateTo)+" ";
			}
			query+="ORDER BY claimproc.ProcDate,patient.LName,patient.FName,claimproc.ClaimProcNum";
			return query;
		}
	}
}
