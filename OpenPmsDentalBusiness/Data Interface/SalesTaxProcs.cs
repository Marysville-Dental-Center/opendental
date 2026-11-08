// B-ACCT: sales tax posted as one procedure (Open Dental 24.4), sales tax rows last within their date in the ledger (25.1).
// Account > Quick Procs > Sales Tax adds one completed procedure with the office's sales-tax procedure code. Its fee is the tax on the highlighted
// completed procedures. It is a second way to post sales tax, next to the 24.3 sales-tax adjustments. Both use ClaimProcs.ComputeSalesTax for each
// procedure, so the two methods always give the same total.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The sales-tax procedure: its setting, the tax it carries and the rules that keep it from being taxed again. See docs/port-notes/B-ACCT.md.</summary>
	public class SalesTaxProcs {
		///<summary>The preference row that holds the sales-tax procedure code (a ProcCode). Open Dental 24.4 added this setting under this very name:
		///the setting names of the office's Open Dental 25.3.48 database include it (owner question Switch-over 1, 2026-11-07;
		///docs/port-notes/SWMAP.md), so the office's code carries over as it is. Its value format is not public, so a CodeNum is read too
		///(GetCodeNumFromSetting). The row is read and written by name, outside the PrefName enum, like the fork's other non-enum rows. It is inserted
		///the first time the office saves a code, so a database without it needs no migration and reads as "not set".</summary>
		public const string PREF_PROC_CODE="SalesTaxProcCode";

		#region Setting
		///<summary>The stored setting, trimmed. Blank when the row is missing or the cache is not loaded. Never throws.</summary>
		public static string GetProcCodeSetting() {
			Meth.NoCheckMiddleTierRole();
			try {
				if(Prefs.DictIsNull() || !Prefs.GetContainsKey(PREF_PROC_CODE)) {
					return "";
				}
				return (Prefs.GetOne(PREF_PROC_CODE).ValueString??"").Trim();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>CodeNum of the configured sales-tax procedure code, or 0 when the setting is blank or names no procedure code.</summary>
		public static long GetCodeNum() {
			Meth.NoCheckMiddleTierRole();
			return GetCodeNumFromSetting(GetProcCodeSetting());
		}

		///<summary>Resolves a stored setting. The value is read as a ProcCode. A value that is no ProcCode but is the CodeNum of an existing procedure code
		///is accepted too, in case the row Open Dental itself wrote under this name holds a CodeNum (its format is not public). Returns 0 when nothing
		///matches.</summary>
		public static long GetCodeNumFromSetting(string setting) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(setting)) {
				return 0;
			}
			string value=setting.Trim();
			long codeNum=ProcedureCodes.GetCodeNum(value);
			if(codeNum!=0) {
				return codeNum;
			}
			if(long.TryParse(value,out long codeNumStored) && codeNumStored>0) {
				return ProcedureCodes.GetProcCode(codeNumStored).CodeNum;//0 when there is no such code.
			}
			return 0;
		}

		///<summary>The ProcCode of the configured sales-tax procedure code, or blank when none is set up.</summary>
		public static string GetProcCode() {
			Meth.NoCheckMiddleTierRole();
			long codeNum=GetCodeNum();
			if(codeNum==0) {
				return "";
			}
			return ProcedureCodes.GetStringProcCode(codeNum,doThrowIfMissing:false);
		}

		///<summary>True when the office set a sales-tax procedure code that exists. Only then does Quick Procs show Sales Tax.</summary>
		public static bool IsEnabled() {
			return GetCodeNum()!=0;
		}

		///<summary>True when codeNum is the configured sales-tax procedure code.</summary>
		public static bool IsSalesTaxCodeNum(long codeNum) {
			Meth.NoCheckMiddleTierRole();
			if(codeNum==0) {
				return false;
			}
			return GetCodeNum()==codeNum;
		}

		///<summary>True when the procedure carries the configured sales-tax procedure code. Such a procedure is never taxed itself.</summary>
		public static bool IsSalesTaxProc(Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			return procedure!=null && IsSalesTaxCodeNum(procedure.CodeNum);
		}

		///<summary>A blank code is valid (it turns the feature off); otherwise the code must exist.</summary>
		public static bool IsValidSetting(string procCode) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(procCode)) {
				return true;
			}
			return ProcedureCodes.IsValidCode(procCode.Trim());
		}

		///<summary>Stores the code (trimmed), inserting the row the first time, and updates this workstation's cache. Returns true if anything changed.
		///The caller validates the code first (IsValidSetting) and signals InvalidType.Prefs so other workstations reload.</summary>
		public static bool SaveProcCodeSetting(string procCode) {
			Meth.NoCheckMiddleTierRole();
			string value=(procCode??"").Trim();
			bool hasRow=!Prefs.DictIsNull() && Prefs.GetContainsKey(PREF_PROC_CODE);
			if(GetProcCodeSetting()==value && (hasRow || value=="")) {
				return false;
			}
			UpsertPref(value);
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_PROC_CODE,ValueString=value });
			return true;
		}

		///<summary>Writes the preference row straight to the table, inserting it when it is missing.</summary>
		public static void UpsertPref(string value) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),value);
				return;
			}
			string prefName=POut.String(PREF_PROC_CODE);
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+prefName+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+prefName+"','"+POut.String(value??"")+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value??"")+"' WHERE PrefName='"+prefName+"'");
		}
		#endregion Setting

		#region Tax
		///<summary>The procedures a sales-tax procedure taxes: completed (ProcStatus C) and not sales-tax procedures themselves. Keeps the order.</summary>
		public static List<Procedure> GetTaxableProcs(List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			if(listProcedures==null) {
				return new List<Procedure>();
			}
			long codeNumSalesTax=GetCodeNum();
			return listProcedures.FindAll(x => x.ProcStatus==ProcStat.C && (codeNumSalesTax==0 || x.CodeNum!=codeNumSalesTax));
		}

		///<summary>The fee of the sales-tax procedure for these procedures: the sum, per procedure, of ClaimProcs.ComputeSalesTax with isEstimate false,
		///that is (fee - write-off) * SalesTaxPercentage / 100 rounded to cents. That is exactly what Apply Sales Tax posts as one adjustment per
		///procedure, so both methods give the same total. Pass the procedures' claim procedures so write-offs are taken off first. Does not filter:
		///call GetTaxableProcs first.</summary>
		public static double ComputeSalesTaxTotal(List<Procedure> listProcedures,List<ClaimProc> listClaimProcs) {
			Meth.NoCheckMiddleTierRole();
			decimal total=0;
			List<ClaimProc> listClaimProcsAll=listClaimProcs??new List<ClaimProc>();
			for(int i=0;i<listProcedures.Count;i++) {
				total+=(decimal)ClaimProcs.ComputeSalesTax(listProcedures[i],listClaimProcsAll,isEstimate:false);
			}
			return (double)Math.Round(total,2);
		}

		///<summary>The ProcNums, among those given, that already have a sales-tax adjustment (AdjType = the SalesTaxAdjustmentType preference). Adding a
		///sales-tax procedure for them as well would tax them twice, so the Account module warns first.</summary>
		public static List<long> GetProcNumsWithSalesTaxAdjustment(List<long> listProcNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),listProcNums);
			}
			long adjType=PrefC.GetLong(PrefName.SalesTaxAdjustmentType);
			if(listProcNums.IsNullOrEmpty() || adjType==0) {
				return new List<long>();
			}
			string command="SELECT DISTINCT ProcNum FROM adjustment WHERE AdjType="+POut.Long(adjType)+" "
				+"AND ProcNum IN ("+string.Join(",",listProcNums.Select(x => POut.Long(x)))+")";
			return Db.GetListLong(command);
		}

		///<summary>The AdjNums, among those given, whose AdjType is adjType.</summary>
		public static List<long> GetAdjNumsOfType(List<long> listAdjNums,long adjType) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),listAdjNums,adjType);
			}
			if(listAdjNums.IsNullOrEmpty() || adjType==0) {
				return new List<long>();
			}
			string command="SELECT AdjNum FROM adjustment WHERE AdjType="+POut.Long(adjType)+" "
				+"AND AdjNum IN ("+string.Join(",",listAdjNums.Select(x => POut.Long(x)))+")";
			return Db.GetListLong(command);
		}
		#endregion Tax

		#region New procedure
		///<summary>Provider of a new sales-tax procedure: the SalesTaxDefaultProvider preference, else the patient's primary provider.</summary>
		public static long GetProvNum(Patient patient) {
			Meth.NoCheckMiddleTierRole();
			long provNum=PrefC.GetLong(PrefName.SalesTaxDefaultProvider);
			if(provNum!=0) {
				return provNum;
			}
			return patient.PriProv;
		}

		///<summary>Clinic of a new sales-tax procedure: the patient's clinic, as for quick charge procedures. With clinics on and a patient who has no
		///clinic, the clinic the user is working in.</summary>
		public static long GetClinicNum(Patient patient) {
			Meth.NoCheckMiddleTierRole();
			if(patient.ClinicNum!=0 || !PrefC.HasClinicsEnabled) {
				return patient.ClinicNum;
			}
			return Clinics.ClinicNum;
		}

		///<summary>A new completed sales-tax procedure for the patient, dated today, with the tax as its fee. Not inserted: the Account module inserts it
		///and validates it the way it does quick charge procedures (insurance estimates, then the after-complete steps).</summary>
		public static Procedure CreateSalesTaxProcedure(Patient patient,ProcedureCode procedureCode,double fee,DateTime dateTimeNow) {
			Meth.NoCheckMiddleTierRole();
			Procedure procedure=new Procedure();
			procedure.PatNum=patient.PatNum;
			procedure.CodeNum=procedureCode.CodeNum;
			procedure.ProcStatus=ProcStat.C;
			procedure.ProcDate=dateTimeNow.Date;
			procedure.DateEntryC=dateTimeNow;
			procedure.DateTP=dateTimeNow;
			procedure.DateComplete=dateTimeNow;
			procedure.ToothRange="";
			procedure.ClinicNum=GetClinicNum(patient);
			procedure.PlaceService=Clinics.GetPlaceService(procedure.ClinicNum);
			if(!PrefC.GetBool(PrefName.EasyHidePublicHealth)) {
				procedure.SiteNum=patient.SiteNum;
			}
			procedure.ProvNum=GetProvNum(patient);
			procedure.MedicalCode=procedureCode.MedicalCode;
			procedure.ProcFee=fee;
			procedure.UnitQty=1;
			return procedure;
		}

		///<summary>Security log text for the AccountProcsQuickAdd entry.</summary>
		public static string GetAuditNote(string procCode,double fee,int countProcsTaxed) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("SalesTaxProcs","Sales tax procedure")+" "+procCode+" "+fee.ToString("c")+" "
				+Lans.g("SalesTaxProcs","added via Quick Procs > Sales Tax in the Account module, for")+" "+countProcsTaxed+" "
				+Lans.g("SalesTaxProcs","procedure(s)");
		}
		#endregion New procedure
	}

	///<summary>Open Dental 25.1: in the account ledger, sales tax rows go after every other row of their date, so the tax follows the work it applies
	///to. A row is sales tax when it is a procedure with the sales-tax procedure code, or an adjustment whose type is the SalesTaxAdjustmentType
	///preference. Within a date, tax rows keep the ledger's usual order among themselves; ordering across dates does not change.
	///Account rows do not carry the adjustment type, so the sales-tax AdjNums are collected before sorting.</summary>
	public class SalesTaxLedgerOrder {
		private readonly string _procCode;
		private readonly HashSet<long> _hashSetAdjNums;

		///<summary>procCode: the sales-tax ProcCode, blank for none. listAdjNumsSalesTax: the AdjNums of sales-tax adjustments.</summary>
		public SalesTaxLedgerOrder(string procCode,IEnumerable<long> listAdjNumsSalesTax) {
			_procCode=procCode??"";
			_hashSetAdjNums=new HashSet<long>(listAdjNumsSalesTax??new List<long>());
		}

		///<summary>False when no row can be sales tax; then the wrapped comparison is used unchanged.</summary>
		public bool IsActive {
			get {
				return _procCode!="" || _hashSetAdjNums.Count>0;
			}
		}

		///<summary>For GetAccount: the raw adjustment rows it read (columns AdjNum and AdjType) and the configured procedure code.</summary>
		public static SalesTaxLedgerOrder CreateFromRawAdjustments(DataTable tableRawAdj) {
			Meth.NoCheckMiddleTierRole();
			long adjType=PrefC.GetLong(PrefName.SalesTaxAdjustmentType);
			List<long> listAdjNums=new List<long>();
			if(adjType!=0 && tableRawAdj!=null && tableRawAdj.Columns.Contains("AdjNum") && tableRawAdj.Columns.Contains("AdjType")) {
				for(int i=0;i<tableRawAdj.Rows.Count;i++) {
					if(PIn.Long(tableRawAdj.Rows[i]["AdjType"].ToString())==adjType) {
						listAdjNums.Add(PIn.Long(tableRawAdj.Rows[i]["AdjNum"].ToString()));
					}
				}
			}
			return new SalesTaxLedgerOrder(SalesTaxProcs.GetProcCode(),listAdjNums);
		}

		///<summary>For account rows put together from several GetAccount calls (super family statements): looks the rows' adjustments up in the
		///database.</summary>
		public static SalesTaxLedgerOrder CreateForRows(List<DataRow> listDataRows) {
			Meth.NoCheckMiddleTierRole();
			long adjType=PrefC.GetLong(PrefName.SalesTaxAdjustmentType);
			List<long> listAdjNums=new List<long>();
			if(adjType!=0 && listDataRows!=null && listDataRows.Count>0 && listDataRows[0].Table.Columns.Contains("AdjNum")) {
				listAdjNums=listDataRows.Select(x => PIn.Long(x["AdjNum"].ToString())).Where(x => x>0).Distinct().ToList();
			}
			return new SalesTaxLedgerOrder(SalesTaxProcs.GetProcCode(),SalesTaxProcs.GetAdjNumsOfType(listAdjNums,adjType));
		}

		///<summary>True for a procedure row with the sales-tax code, or an adjustment row of a sales-tax adjustment.</summary>
		public bool IsSalesTaxRow(DataRow dataRow) {
			string procNum=dataRow["ProcNum"].ToString();
			if(procNum!="0" && procNum!="") {
				return _procCode!="" && dataRow.Table.Columns.Contains("ProcCode") && dataRow["ProcCode"].ToString()==_procCode;
			}
			long adjNum=PIn.Long(dataRow["AdjNum"].ToString());
			return adjNum!=0 && _hashSetAdjNums.Contains(adjNum);
		}

		///<summary>1 when only x is sales tax, -1 when only y is, 0 otherwise. Only meaningful for two rows of the same date.</summary>
		public int CompareSalesTaxLast(DataRow x,DataRow y) {
			bool isSalesTaxX=IsSalesTaxRow(x);
			bool isSalesTaxY=IsSalesTaxRow(y);
			if(isSalesTaxX==isSalesTaxY) {
				return 0;
			}
			return isSalesTaxX ? 1 : -1;
		}

		///<summary>Wraps a ledger comparison (AccountLineComparer, or SortRowsForStatmentPrinting with isByPatient true): rows that the comparison would
		///order by type because they share a date (and, for statement printing, a patient) put sales tax last, then the comparison decides.</summary>
		public Comparison<DataRow> Wrap(Comparison<DataRow> comparison,bool isByPatient) {
			if(!IsActive) {
				return comparison;
			}
			return (x,y) => {
				if(isByPatient) {
					string patNumX=x["PatNum"].ToString();
					string patNumY=y["PatNum"].ToString();
					if(patNumX!=patNumY && patNumX!="0" && patNumY!="0") {
						return comparison(x,y);
					}
				}
				if(((DateTime)x["DateTime"]).Date!=((DateTime)y["DateTime"]).Date) {
					return comparison(x,y);
				}
				int salesTaxOrder=CompareSalesTaxLast(x,y);
				if(salesTaxOrder!=0) {
					return salesTaxOrder;
				}
				return comparison(x,y);
			};
		}
	}
}
