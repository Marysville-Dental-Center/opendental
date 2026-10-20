using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using DataConnectionBase;

namespace OpenDentBusiness {
	///<summary>Queries for the two Daily procedure reports. Since 24.4 the old Daily Procedures report is split in two: the itemized Procedures report
	///(FormRpProcedures, GetProceduresTable) and Production by Procedure (FormRpProcSheet, GetGroupedTable). Both use the same filters and the same fee
	///rule (fee times units, less CapComplete write-offs), so their totals agree for the same filters.</summary>
	public class RpProcSheet {

		///<summary>If not using clinics then supply an empty list of clinicNums.  listClinicNums must have at least one item if using clinics. Not formatted for display</summary>
		public static DataTable GetIndividualTable(DateTime dateFrom,DateTime dateTo,List<long> listProvNums,List<long> listClinicNums,string procCode,
			bool isAnyClinicMedical,bool hasAllProvs,bool hasClinicsEnabled)
		{
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),dateFrom,dateTo,listProvNums,listClinicNums,procCode,isAnyClinicMedical,hasAllProvs,
					hasClinicsEnabled);
			}
			string query=GetIndividualQuery(dateFrom,dateTo,listProvNums,listClinicNums,procCode,isAnyClinicMedical,hasAllProvs,hasClinicsEnabled,false);
			return ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(query));
		}

		///<summary>The itemized Procedures report: one row per completed procedure, with the columns of GetIndividualTable followed by PatNum, ProcNum and
		///ClinicNum, so the report can go to the patient. If not using clinics then supply an empty list of clinicNums.</summary>
		public static DataTable GetProceduresTable(DateTime dateFrom,DateTime dateTo,List<long> listProvNums,List<long> listClinicNums,string procCode,
			bool isAnyClinicMedical,bool hasAllProvs,bool hasClinicsEnabled)
		{
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),dateFrom,dateTo,listProvNums,listClinicNums,procCode,isAnyClinicMedical,hasAllProvs,
					hasClinicsEnabled);
			}
			string query=GetIndividualQuery(dateFrom,dateTo,listProvNums,listClinicNums,procCode,isAnyClinicMedical,hasAllProvs,hasClinicsEnabled,true);
			return ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(query));
		}

		///<summary>The SQL of the itemized report. doIncludeKeys adds PatNum, ProcNum and ClinicNum as the last columns. Public for the report tests.
		///</summary>
		public static string GetIndividualQuery(DateTime dateFrom,DateTime dateTo,List<long> listProvNums,List<long> listClinicNums,string procCode,
			bool isAnyClinicMedical,bool hasAllProvs,bool hasClinicsEnabled,bool doIncludeKeys)
		{
			Meth.NoCheckMiddleTierRole();
			string query="SELECT procedurelog.ProcDate,"
			  +DbHelper.Concat("patient.LName","', '","patient.FName","' '","patient.MiddleI")+" "
			  +"AS plfname, procedurecode.ProcCode,";
			if(!isAnyClinicMedical) {
				query+="procedurelog.ToothNum,";
			}
			query+="procedurelog.Surf AS Area,procedurecode.Descript,provider.Abbr,";
			if(hasClinicsEnabled) {
				query+="COALESCE(IF(clinic.IsHidden,CONCAT(clinic.Abbr,'("+POut.String(Lans.g("FormRpProcSheet","hidden"))+")'),clinic.Abbr),\"Unassigned\") Clinic,";
			}
			query+="procedurelog.ProcFee*(procedurelog.UnitQty+procedurelog.BaseUnits)"
				+"-COALESCE(SUM(claimproc.WriteOff),0) $fee ";//if no writeoff, then subtract 0
			if(doIncludeKeys) {
				query+=",procedurelog.PatNum,procedurelog.ProcNum,procedurelog.ClinicNum ";
			}
			query+="FROM patient "
				+"INNER JOIN procedurelog ON procedurelog.PatNum=patient.PatNum "
				+"INNER JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum "
				+"INNER JOIN provider ON provider.ProvNum=procedurelog.ProvNum ";
			if(hasClinicsEnabled) {
				query+="LEFT JOIN clinic ON clinic.ClinicNum=procedurelog.ClinicNum ";
			}
			query+="LEFT JOIN claimproc ON procedurelog.ProcNum=claimproc.ProcNum "
				+"AND claimproc.Status="+POut.Int((int)ClaimProcStatus.CapComplete)+" "//only CapComplete writeoffs are subtracted here.
				+"WHERE procedurelog.ProcStatus="+POut.Int((int)ProcStat.C)+" ";
			if(!hasAllProvs) {
				query+="AND procedurelog.ProvNum IN ("+GetInList(listProvNums)+") ";
			}
			if(hasClinicsEnabled && listClinicNums.Count>0) {
				query+="AND procedurelog.ClinicNum IN ("+GetInList(listClinicNums)+") ";
			}
			if(!string.IsNullOrEmpty(procCode)) {//don't include ProcCode condition if blank, it changes the execution plan and is much slower
				query+="AND UPPER(procedurecode.ProcCode) LIKE '%"+POut.String(procCode.ToUpper())+"%' ";
			}
			query+="AND procedurelog.ProcDate >= " +POut.Date(dateFrom)+" "
				+"AND procedurelog.ProcDate <= " +POut.Date(dateTo)+" "
				+"GROUP BY procedurelog.ProcNum "
				+"ORDER BY procedurelog.ProcDate,plfname,procedurecode.ProcCode,ToothNum";
			return query;
		}

		///<summary>Production by Procedure: completed procedures grouped by procedure code, with category, quantity, average fee and total fees.</summary>
		public static DataTable GetGroupedTable(DateTime dateFrom,DateTime dateTo,List<long> listProvNums,List<long> listClinicNums,string procCode,bool hasAllProvs) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),dateFrom,dateTo,listProvNums,listClinicNums,procCode,hasAllProvs);
			}
			return ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(GetGroupedQuery(dateFrom,dateTo,listProvNums,listClinicNums,procCode,hasAllProvs,
				Prefs.HasClinicsEnabledNoCache)));
		}

		///<summary>The SQL of Production by Procedure. Public for the report tests.</summary>
		public static string GetGroupedQuery(DateTime dateFrom,DateTime dateTo,List<long> listProvNums,List<long> listClinicNums,string procCode,bool hasAllProvs,
			bool hasClinicsEnabled)
		{
			Meth.NoCheckMiddleTierRole();
			string query="SELECT procs.ItemName,procs.ProcCode,procs.Descript,COUNT(*),FORMAT(ROUND(AVG(procs.fee),2),2) $AvgFee,SUM(procs.fee) AS $TotFee "
				+"FROM ( "
				+"SELECT procedurelog.ProcFee*(procedurelog.UnitQty+procedurelog.BaseUnits) -COALESCE(SUM(claimproc.WriteOff),0) fee, "
				+"procedurecode.ProcCode,	procedurecode.Descript,	definition.ItemName, definition.ItemOrder "
				+"FROM procedurelog "
				+"INNER JOIN procedurecode ON procedurelog.CodeNum=procedurecode.CodeNum "
				+"INNER JOIN definition ON definition.DefNum=procedurecode.ProcCat "
				+"LEFT JOIN claimproc ON claimproc.ProcNum=procedurelog.ProcNum AND claimproc.Status="+POut.Int((int)ClaimProcStatus.CapComplete)+" "
				+"WHERE procedurelog.ProcStatus="+POut.Int((int)ProcStat.C)+" ";
			if(!hasAllProvs) {
				query+="AND procedurelog.ProvNum IN ("+GetInList(listProvNums)+") ";
			}
			if(hasClinicsEnabled && listClinicNums.Count>0) {
				query+="AND procedurelog.ClinicNum IN ("+GetInList(listClinicNums)+") ";
			}
			if(!string.IsNullOrEmpty(procCode)) {//don't include ProcCode condition if blank, it changes the execution plan and is much slower
				query+="AND UPPER(procedurecode.ProcCode) LIKE '%"+POut.String(procCode.ToUpper())+"%' ";
			}
			query+="AND procedurelog.ProcDate >= " +POut.Date(dateFrom)+" "
				+"AND procedurelog.ProcDate <= " +POut.Date(dateTo)+" "
				+"GROUP BY procedurelog.ProcNum ) procs "
				+"GROUP BY procs.ProcCode "
				+"ORDER BY procs.ItemOrder,procs.ProcCode";
			return query;
		}

		///<summary>A comma-separated list for an IN clause. An empty list gives -1, which matches no provider or clinic, so the
		///query returns no rows instead of failing.</summary>
		private static string GetInList(List<long> listNums) {
			if(listNums==null || listNums.Count==0) {
				return "-1";
			}
			return string.Join(",",listNums);
		}

	}
}
