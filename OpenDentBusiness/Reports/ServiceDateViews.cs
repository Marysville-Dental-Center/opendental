// Service Date View (package M2-SDV in docs/specs/port-backlog.md; Open Dental 25.4 and 26.1 items v25_4-account-service-date-view and
// v26_1-account-service-date-view). One business method builds the service-date ledger for the Service Date View window, the Account module
// grid in service-date view and statements printed in service-date view, so the three always match.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>What a row of the service-date view is.</summary>
	public enum ServiceDateViewRowKind {
		///<summary>A procedure, or a transaction (adjustment, payment, insurance payment, write-off, payment plan charge or credit).</summary>
		Transaction,
		///<summary>The "Unallocated Trans" heading above the transactions of a date that are not attached to a procedure.</summary>
		UnallocatedHeading,
		///<summary>"Total for Date": the totals of one date of service.</summary>
		DayTotal,
		///<summary>"Family Total:" or "Patient Total:" at the end.</summary>
		OverallTotal,
		///<summary>Fork addition for date ranges (statements, the Account module's date filter): what the services before the range still owe.</summary>
		BalanceForward,
	}

	///<summary>One raw row of the service-date query (RpServiceDateView.GetCoreTable): a procedure or one transaction, dated by the service it
	///belongs to.</summary>
	public class ServiceDateViewCoreRow {
		///<summary>The type text of the 24.3 report: Proc, Adj-Att., Adj, PatPay Att., PatPay, PatPay Att. PayPlan, PatPay PayPlan, InsPay-Att.,
		///InsPay, WriteOff-Att., WriteOff, PayPlan Charge Att., PayPlan Charge, PayPlan Credit, Dynamic PayPlan Credit.</summary>
		public string TranType="";
		///<summary>Primary key of the transaction as text. Write-offs are "W" plus the ClaimProcNum.</summary>
		public string TranNum="";
		///<summary>The date of service: the procedure date, or the transaction's own date when it is not attached to a procedure. Time kept as the
		///query returns it (only dynamic payment plan credits have one).</summary>
		public DateTime DateService;
		///<summary>The transaction date. MinValue for procedures.</summary>
		public DateTime DateTrans;
		public long PatNum;
		///<summary>Patient first name, as the 24.3 report shows it.</summary>
		public string PatientName="";
		public long ProcNum;
		public string Reference="";
		public decimal Charge;
		public decimal Credit;
		public string ProvAbbr="";
		///<summary>Procedures only: adjustments, patient payments and received insurance (payments and write-offs) already applied.</summary>
		public decimal ProcCredits;
		///<summary>Procedures only: insurance still expected (estimate and write-off estimate of claims not received).</summary>
		public decimal InsCredits;
		public long ClinicNum;
		public long ProvNum;
		///<summary>PayNum of a payment split, ClaimNum of an insurance payment or write-off, PayPlanNum of a payment plan charge or credit.</summary>
		public long ParentNum;
		///<summary>Procedures only.</summary>
		public long CodeNum;
		public string ProcCode="";
		public string ToothNum="";
		public string ToothRange="";
		public string Surf="";

		public ServiceDateViewCoreRow Copy() {
			return (ServiceDateViewCoreRow)MemberwiseClone();
		}
	}

	///<summary>One row of the service-date view, as the window, the Account grid and statements show it.</summary>
	public class ServiceDateViewRow {
		public ServiceDateViewRowKind RowKind;
		///<summary>Type text of the 24.3 report (see ServiceDateViewCoreRow.TranType), "Unallocated", "Day Total", "Overall Total" or
		///"Balance Forward".</summary>
		public string TranType="";
		public string TranNum="";
		///<summary>Date of service the row is grouped under, without time. MinValue for the overall total and balance forward.</summary>
		public DateTime DateService;
		///<summary>Transaction date. MinValue for procedures and for rows that are not transactions.</summary>
		public DateTime DateTrans;
		public long PatNum;
		public string PatientName="";
		public long ProcNum;
		///<summary>The 24.3 report's text: transactions attached to a procedure are indented by two spaces.</summary>
		public string Reference="";
		public decimal Charge;
		public decimal Credit;
		public string ProvAbbr="";
		public long ProvNum;
		public long ClinicNum;
		public long ParentNum;
		public long CodeNum;
		public string ProcCode="";
		public string ToothNum="";
		public string ToothRange="";
		public string Surf="";
		///<summary>Insurance estimate still expected ("InsBal"). Procedures (in detailed view), totals and balance forward.</summary>
		public decimal InsBal;
		///<summary>Patient portion still owed ("AcctBal"): charge minus credits minus the insurance still expected.</summary>
		public decimal AcctBal;
		///<summary>True when InsBal and AcctBal are shown for this row: totals and balance forward always, procedures in detailed view.</summary>
		public bool ShowsInsAndAcctBal;
		///<summary>Remaining balance of the service, before insurance estimates (statements' Balance column): charge minus the credits applied.</summary>
		public decimal Balance;
		///<summary>True when Balance is shown: procedures, totals and balance forward.</summary>
		public bool ShowsBalance;

		public bool IsTotal {
			get { return RowKind==ServiceDateViewRowKind.DayTotal || RowKind==ServiceDateViewRowKind.OverallTotal; }
		}

		public bool IsProcedure {
			get { return RowKind==ServiceDateViewRowKind.Transaction && TranType==ServiceDateViews.TYPE_PROC; }
		}

		///<summary>True for transactions shown indented under the procedure they are attached to.</summary>
		public bool IsAttachedCredit {
			get { return RowKind==ServiceDateViewRowKind.Transaction && ServiceDateViews.IsAttachedType(TranType) && TranType!=ServiceDateViews.TYPE_PROC; }
		}

		///<summary>The numeric primary key of the transaction (TranNum without the "W" of write-offs).</summary>
		public long GetPriKey() {
			string tranNum=TranNum??"";
			if(tranNum.StartsWith("W")) {
				tranNum=tranNum.Substring(1);
			}
			long.TryParse(tranNum,NumberStyles.Integer,CultureInfo.InvariantCulture,out long priKey);
			return priKey;
		}
	}

	///<summary>The charges an invoice holds: what a service-date view invoice lists, with the credits applied to them.</summary>
	public class ServiceDateViewInvoiceItems {
		public List<long> ListProcNums=new List<long>();
		public List<long> ListAdjNums=new List<long>();
		public List<long> ListPayPlanChargeNums=new List<long>();
	}

	///<summary>Options for ServiceDateViews.Build.</summary>
	public class ServiceDateViewOptions {
		///<summary>The overall total says "Family Total:" instead of "Patient Total:".</summary>
		public bool IsFamily;
		///<summary>Insurance estimate and patient portion on every procedure row, not only on totals ("Show detailed view").</summary>
		public bool IsDetailed;
		///<summary>First date of service shown. Services before it are summed into one balance forward row. MinValue for all.</summary>
		public DateTime DateFrom=DateTime.MinValue;
		///<summary>Last date of service shown. Later services are left out. MaxValue (or any year after 2100) for all.</summary>
		public DateTime DateTo=DateTime.MaxValue;
		///<summary>Receipt: only patient payments dated within DateFrom to DateTo, each with the procedure it pays (whatever its date). No balance
		///forward.</summary>
		public bool IsReceipt;
		///<summary>Invoice: only these charges, each with the credits applied to it. No date range, no balance forward. Null when not an invoice.</summary>
		public ServiceDateViewInvoiceItems InvoiceItems;
	}

	///<summary>Builds the service-date view from the rows of RpServiceDateView.GetCoreTable. The layout follows the 24.3 Service Date View report:
	///for each date of service, the procedures with the transactions attached to them indented beneath, then the unattached transactions under
	///an "Unallocated Trans" heading, then the date's totals; an overall total at the end.</summary>
	public static class ServiceDateViews {
		public const string TYPE_PROC="Proc";
		public const string TYPE_UNALLOCATED="Unallocated";
		public const string TYPE_DAY_TOTAL="Day Total";
		public const string TYPE_OVERALL_TOTAL="Overall Total";
		public const string TYPE_BALANCE_FORWARD="Balance Forward";
		///<summary>Report text of the rows the query does not return.</summary>
		public const string REFERENCE_UNALLOCATED="Unallocated Trans";
		public const string REFERENCE_DAY_TOTAL="  Total for Date";
		public const string REFERENCE_FAMILY_TOTAL="Family Total:";
		public const string REFERENCE_PATIENT_TOTAL="Patient Total:";
		public const string REFERENCE_BALANCE_FORWARD="Balance Forward";

		///<summary>Types that belong to a procedure: the procedure itself and the transactions attached to it. Sorted first on their date.</summary>
		private static readonly string[] _arrayAttachedTypes=new[] { "Proc","Adj-Att.","PatPay Att.","WriteOff-Att.","InsPay-Att.","PayPlan Charge Att.",
			"PatPay Att. PayPlan","Unallocated" };
		///<summary>Order of types among rows that tie on everything else (the 24.3 report's FIELD() list).</summary>
		private static readonly string[] _arrayTypeOrder=new[] { "Proc","Adj-Att.","PatPay Att.","WriteOff-Att.","InsPay-Att.","PayPlan Charge Att.",
			"PatPay Att. PayPlan","Unallocated","PatPay","WriteOff","Adj","InsPay","PayPlan Credit","Dynamic PayPlan Credit","PayPlan Charge","PatPay PayPlan",
			"Day Total","Overall Total" };
		private static readonly string[] _arrayPatPayTypes=new[] { "PatPay Att.","PatPay","PatPay Att. PayPlan","PatPay PayPlan" };
		private static readonly string[] _arrayAdjTypes=new[] { "Adj-Att.","Adj" };
		private static readonly string[] _arrayPayPlanChargeTypes=new[] { "PayPlan Charge Att.","PayPlan Charge" };
		private static readonly string[] _arrayInsTypes=new[] { "InsPay-Att.","InsPay","WriteOff-Att.","WriteOff" };

		///<summary>True for the procedure and the transactions attached to one (the 24.3 report's first ordering group).</summary>
		public static bool IsAttachedType(string tranType) {
			return _arrayAttachedTypes.Contains(tranType);
		}

		public static bool IsPatPayType(string tranType) {
			return _arrayPatPayTypes.Contains(tranType);
		}

		public static bool IsAdjustmentType(string tranType) {
			return _arrayAdjTypes.Contains(tranType);
		}

		public static bool IsPayPlanChargeType(string tranType) {
			return _arrayPayPlanChargeTypes.Contains(tranType);
		}

		///<summary>Insurance payments and write-offs (attached or not). ParentNum is the ClaimNum.</summary>
		public static bool IsInsuranceType(string tranType) {
			return _arrayInsTypes.Contains(tranType);
		}

		///<summary>Payment plan credits and charges, attached or not. ParentNum is the PayPlanNum.</summary>
		public static bool IsPayPlanType(string tranType) {
			return tranType!=null && tranType.StartsWith("PayPlan") || tranType=="Dynamic PayPlan Credit";
		}

		///<summary>Reads the rows of RpServiceDateView.GetCoreTable. Works with typed columns and with text columns.</summary>
		public static List<ServiceDateViewCoreRow> ReadCoreRows(DataTable table) {
			List<ServiceDateViewCoreRow> listCoreRows=new List<ServiceDateViewCoreRow>();
			if(table==null) {
				return listCoreRows;
			}
			for(int i=0;i<table.Rows.Count;i++) {
				DataRow dataRow=table.Rows[i];
				ServiceDateViewCoreRow coreRow=new ServiceDateViewCoreRow();
				coreRow.TranType=GetString(dataRow,"Type");
				coreRow.TranNum=GetString(dataRow,"TranNum");
				coreRow.DateService=GetDate(dataRow,"ProcDate");
				coreRow.DateTrans=GetDate(dataRow,"TranDate");
				coreRow.PatNum=GetLong(dataRow,"PatNum");
				coreRow.PatientName=GetString(dataRow,"Patient");
				coreRow.ProcNum=GetLong(dataRow,"ProcNum");
				coreRow.Reference=GetString(dataRow,"Reference");
				coreRow.Charge=GetDecimal(dataRow,"Charge");
				coreRow.Credit=GetDecimal(dataRow,"Credit");
				coreRow.ProvAbbr=GetString(dataRow,"Prov");
				coreRow.ProcCredits=GetDecimal(dataRow,"ProcCredits");
				coreRow.InsCredits=GetDecimal(dataRow,"InsCredits");
				coreRow.ClinicNum=GetLong(dataRow,"ClinicNum");
				coreRow.ProvNum=GetLong(dataRow,"ProvNum");
				coreRow.ParentNum=GetLong(dataRow,"ParentNum");
				coreRow.CodeNum=GetLong(dataRow,"CodeNum");
				coreRow.ProcCode=GetString(dataRow,"ProcCode");
				coreRow.ToothNum=GetString(dataRow,"ToothNum");
				coreRow.ToothRange=GetString(dataRow,"ToothRange");
				coreRow.Surf=GetString(dataRow,"Surf");
				listCoreRows.Add(coreRow);
			}
			return listCoreRows;
		}

		///<summary>Builds the rows in display order. Pure: no database or cache access, so the window, the Account grid and statements get the
		///same rows from the same query rows.</summary>
		public static List<ServiceDateViewRow> Build(List<ServiceDateViewCoreRow> listCoreRows,ServiceDateViewOptions options) {
			options=options??new ServiceDateViewOptions();
			List<ServiceDateViewCoreRow> listRows=(listCoreRows??new List<ServiceDateViewCoreRow>()).Select(x => x.Copy()).ToList();
			bool hasDateRange=false;
			List<ServiceDateViewCoreRow> listRowsBefore=new List<ServiceDateViewCoreRow>();
			if(options.InvoiceItems!=null) {
				listRows=FilterForInvoice(listRows,options.InvoiceItems);
			}
			else if(options.IsReceipt) {
				listRows=FilterForReceipt(listRows,options.DateFrom,options.DateTo);
			}
			else {
				hasDateRange=options.DateFrom.Year>1880 || (options.DateTo.Year>1 && options.DateTo.Year<2100);
				if(hasDateRange) {
					DateTime dateFrom=options.DateFrom.Date;
					DateTime dateTo=(options.DateTo.Year>=2100 || options.DateTo.Year<=1) ? DateTime.MaxValue : options.DateTo.Date;
					listRowsBefore=listRows.FindAll(x => x.DateService.Date<dateFrom);
					listRows=listRows.FindAll(x => x.DateService.Date>=dateFrom && x.DateService.Date<=dateTo);
				}
			}
			List<ServiceDateViewRow> listResult=new List<ServiceDateViewRow>();
			ServiceDateViewRow rowBalanceForward=null;
			if(hasDateRange && listRowsBefore.Count>0) {
				rowBalanceForward=MakeBalanceForward(listRowsBefore);
				listResult.Add(rowBalanceForward);
			}
			//One row per transaction. The 24.3 report groups by transaction number, date and type, which only merges the payment plan link rows of one
			//dynamic payment plan that share a date.
			List<ServiceDateViewRow> listTransactions=listRows
				.GroupBy(x => x.TranNum+"|"+x.DateService.Date.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"|"+x.TranType)
				.Select(x => MakeTransactionRow(x.ToList(),options.IsDetailed))
				.ToList();
			foreach(IGrouping<DateTime,ServiceDateViewRow> groupDate in listTransactions.GroupBy(x => x.DateService).OrderBy(x => x.Key)) {
				List<ServiceDateViewRow> listAttached=groupDate.Where(x => IsAttachedType(x.TranType)).ToList();
				List<ServiceDateViewRow> listUnattached=groupDate.Where(x => !IsAttachedType(x.TranType)).ToList();
				listAttached.Sort(CompareTransactions);
				listUnattached.Sort(CompareTransactions);
				listResult.AddRange(listAttached);
				if(listUnattached.Count>0) {
					listResult.Add(MakeUnallocatedHeading(groupDate.Key));
					listResult.AddRange(listUnattached);
				}
				listResult.Add(MakeTotal(ServiceDateViewRowKind.DayTotal,groupDate.Key,groupDate.ToList(),listRows.FindAll(x => x.DateService.Date==groupDate.Key),
					null,options.IsFamily));
			}
			listResult.Add(MakeTotal(ServiceDateViewRowKind.OverallTotal,DateTime.MinValue,listTransactions,listRows,rowBalanceForward,options.IsFamily));
			return listResult;
		}

		///<summary>Keeps the invoice's charges and the credits applied to its procedures.</summary>
		private static List<ServiceDateViewCoreRow> FilterForInvoice(List<ServiceDateViewCoreRow> listRows,ServiceDateViewInvoiceItems invoiceItems) {
			HashSet<long> setProcNums=new HashSet<long>(invoiceItems.ListProcNums??new List<long>());
			HashSet<long> setAdjNums=new HashSet<long>(invoiceItems.ListAdjNums??new List<long>());
			HashSet<long> setPayPlanChargeNums=new HashSet<long>(invoiceItems.ListPayPlanChargeNums??new List<long>());
			return listRows.FindAll(x => {
				if(x.TranType==TYPE_PROC) {
					return setProcNums.Contains(x.ProcNum);
				}
				long priKey=ParseLong(x.TranNum);
				if(IsAdjustmentType(x.TranType) && setAdjNums.Contains(priKey)) {
					return true;
				}
				if(IsPayPlanChargeType(x.TranType) && setPayPlanChargeNums.Contains(priKey)) {
					return true;
				}
				//Credits applied to a procedure of the invoice.
				return x.ProcNum!=0 && setProcNums.Contains(x.ProcNum) && IsAttachedType(x.TranType);
			});
		}

		///<summary>Keeps the patient payments dated within the range and the procedures they pay.</summary>
		private static List<ServiceDateViewCoreRow> FilterForReceipt(List<ServiceDateViewCoreRow> listRows,DateTime dateFrom,DateTime dateTo) {
			DateTime dateMin=dateFrom.Date;
			DateTime dateMax=(dateTo.Year>=2100 || dateTo.Year<=1) ? DateTime.MaxValue : dateTo.Date;
			List<ServiceDateViewCoreRow> listPayments=listRows.FindAll(x => IsPatPayType(x.TranType) && x.DateTrans.Date>=dateMin && x.DateTrans.Date<=dateMax);
			HashSet<long> setProcNums=new HashSet<long>(listPayments.Where(x => x.ProcNum!=0).Select(x => x.ProcNum));
			return listRows.FindAll(x => listPayments.Contains(x) || (x.TranType==TYPE_PROC && setProcNums.Contains(x.ProcNum)));
		}

		private static ServiceDateViewRow MakeTransactionRow(List<ServiceDateViewCoreRow> listGroup,bool isDetailed) {
			ServiceDateViewCoreRow coreRow=listGroup[0];
			ServiceDateViewRow row=new ServiceDateViewRow();
			row.RowKind=ServiceDateViewRowKind.Transaction;
			row.TranType=coreRow.TranType;
			row.TranNum=coreRow.TranNum;
			row.DateService=coreRow.DateService.Date;
			row.DateTrans=coreRow.TranType==TYPE_PROC ? DateTime.MinValue : coreRow.DateTrans;
			row.PatNum=coreRow.PatNum;
			row.PatientName=coreRow.PatientName;
			row.ProcNum=coreRow.ProcNum;
			row.Reference=coreRow.Reference;
			if(IsAttachedType(coreRow.TranType) && coreRow.TranType!=TYPE_PROC) {
				row.Reference="  "+coreRow.Reference;
			}
			row.Charge=listGroup.Sum(x => x.Charge);
			row.Credit=listGroup.Sum(x => x.Credit);
			row.ProvAbbr=coreRow.ProvAbbr;
			row.ProvNum=coreRow.ProvNum;
			row.ClinicNum=coreRow.ClinicNum;
			row.ParentNum=coreRow.ParentNum;
			row.CodeNum=coreRow.CodeNum;
			row.ProcCode=coreRow.ProcCode;
			row.ToothNum=coreRow.ToothNum;
			row.ToothRange=coreRow.ToothRange;
			row.Surf=coreRow.Surf;
			if(coreRow.TranType==TYPE_PROC) {
				row.InsBal=coreRow.InsCredits;
				row.AcctBal=coreRow.Charge-(coreRow.ProcCredits+coreRow.InsCredits);
				row.ShowsInsAndAcctBal=isDetailed;
				row.Balance=coreRow.Charge-coreRow.ProcCredits;
				row.ShowsBalance=true;
			}
			return row;
		}

		private static ServiceDateViewRow MakeUnallocatedHeading(DateTime dateService) {
			ServiceDateViewRow row=new ServiceDateViewRow();
			row.RowKind=ServiceDateViewRowKind.UnallocatedHeading;
			row.TranType=TYPE_UNALLOCATED;
			row.DateService=dateService;
			row.Reference=REFERENCE_UNALLOCATED;
			return row;
		}

		///<summary>Totals of a date (DayTotal) or of everything shown (OverallTotal). Charge and Credit add up the transaction rows; InsBal, AcctBal
		///and Balance add up the raw rows, as the 24.3 report does, plus the balance forward for the overall total.</summary>
		private static ServiceDateViewRow MakeTotal(ServiceDateViewRowKind rowKind,DateTime dateService,List<ServiceDateViewRow> listTransactions,
			List<ServiceDateViewCoreRow> listCoreRows,ServiceDateViewRow rowBalanceForward,bool isFamily)
		{
			ServiceDateViewRow row=new ServiceDateViewRow();
			row.RowKind=rowKind;
			row.DateService=dateService;
			if(rowKind==ServiceDateViewRowKind.DayTotal) {
				row.TranType=TYPE_DAY_TOTAL;
				row.Reference=REFERENCE_DAY_TOTAL;
			}
			else {
				row.TranType=TYPE_OVERALL_TOTAL;
				row.Reference=isFamily ? REFERENCE_FAMILY_TOTAL : REFERENCE_PATIENT_TOTAL;
			}
			row.Charge=listTransactions.Sum(x => x.Charge);
			row.Credit=listTransactions.Sum(x => x.Credit);
			row.InsBal=listCoreRows.Where(x => x.TranType==TYPE_PROC).Sum(x => x.InsCredits);
			row.AcctBal=listCoreRows.Sum(x => x.Charge-x.Credit-x.InsCredits);
			row.Balance=listCoreRows.Sum(x => x.Charge-x.Credit);
			if(rowBalanceForward!=null) {
				row.InsBal+=rowBalanceForward.InsBal;
				row.AcctBal+=rowBalanceForward.AcctBal;
				row.Balance+=rowBalanceForward.Balance;
			}
			row.ShowsInsAndAcctBal=true;
			row.ShowsBalance=true;
			return row;
		}

		private static ServiceDateViewRow MakeBalanceForward(List<ServiceDateViewCoreRow> listCoreRowsBefore) {
			ServiceDateViewRow row=new ServiceDateViewRow();
			row.RowKind=ServiceDateViewRowKind.BalanceForward;
			row.TranType=TYPE_BALANCE_FORWARD;
			row.Reference=REFERENCE_BALANCE_FORWARD;
			row.InsBal=listCoreRowsBefore.Where(x => x.TranType==TYPE_PROC).Sum(x => x.InsCredits);
			row.AcctBal=listCoreRowsBefore.Sum(x => x.Charge-x.Credit-x.InsCredits);
			row.Balance=listCoreRowsBefore.Sum(x => x.Charge-x.Credit);
			row.ShowsInsAndAcctBal=true;
			row.ShowsBalance=true;
			return row;
		}

		///<summary>The 24.3 report's order within one ordering group of a date: patient first name (case-insensitive), procedure number descending (so
		///each procedure's credits follow it), transaction date (procedures first), then type. Ties are broken by transaction number so the order
		///is stable.</summary>
		private static int CompareTransactions(ServiceDateViewRow x,ServiceDateViewRow y) {
			int compare=string.Compare(x.PatientName??"",y.PatientName??"",StringComparison.OrdinalIgnoreCase);
			if(compare!=0) {
				return compare;
			}
			compare=y.ProcNum.CompareTo(x.ProcNum);
			if(compare!=0) {
				return compare;
			}
			compare=x.DateTrans.CompareTo(y.DateTrans);
			if(compare!=0) {
				return compare;
			}
			compare=GetTypeOrder(x.TranType).CompareTo(GetTypeOrder(y.TranType));
			if(compare!=0) {
				return compare;
			}
			compare=x.GetPriKey().CompareTo(y.GetPriKey());
			if(compare!=0) {
				return compare;
			}
			return string.CompareOrdinal(x.TranNum,y.TranNum);
		}

		private static int GetTypeOrder(string tranType) {
			return Array.IndexOf(_arrayTypeOrder,tranType)+1;
		}

		///<summary>The DataTable the 24.3 report returned (RpServiceDateView.GetData): Date, Trans Date, Patient, Reference, Charge, Credit, Pvdr,
		///InsBal, AcctBal, Type, PrimaryKey, PatNum, with blanks where the report had them. Amounts are written with two decimals in the current
		///culture. Also has ProcNum, ClinicNum and ParentNum.</summary>
		public static DataTable ToReportTable(List<ServiceDateViewRow> listRows) {
			DataTable table=new DataTable("ServiceDateView");
			foreach(string columnName in new[] { "Date","Trans Date","Patient","Reference","Charge","Credit","Pvdr","InsBal","AcctBal","Type","PrimaryKey",
				"PatNum","ProcNum","ClinicNum","ParentNum" })
			{
				table.Columns.Add(columnName);
			}
			foreach(ServiceDateViewRow row in listRows) {
				DataRow dataRow=table.NewRow();
				bool isTransaction=row.RowKind==ServiceDateViewRowKind.Transaction;
				//The report shows the date of service on procedures, the unallocated heading and the day total; other transactions show their own date.
				bool showsServiceDate=row.IsProcedure || row.RowKind==ServiceDateViewRowKind.UnallocatedHeading || row.RowKind==ServiceDateViewRowKind.DayTotal;
				dataRow["Date"]=showsServiceDate ? row.DateService.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) : "";
				dataRow["Trans Date"]=(isTransaction && !row.IsProcedure) ? row.DateTrans.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) : "";
				dataRow["Patient"]=isTransaction ? row.PatientName : "";
				dataRow["Reference"]=row.Reference;
				bool hasAmounts=row.RowKind!=ServiceDateViewRowKind.UnallocatedHeading && row.RowKind!=ServiceDateViewRowKind.BalanceForward;
				dataRow["Charge"]=hasAmounts ? FormatAmount(row.Charge) : "";
				dataRow["Credit"]=hasAmounts ? FormatAmount(row.Credit) : "";
				dataRow["Pvdr"]=isTransaction ? row.ProvAbbr : "";
				dataRow["InsBal"]=row.ShowsInsAndAcctBal ? FormatAmount(row.InsBal) : "";
				dataRow["AcctBal"]=row.ShowsInsAndAcctBal ? FormatAmount(row.AcctBal) : "";
				dataRow["Type"]=row.TranType;
				dataRow["PrimaryKey"]=row.TranNum;
				dataRow["PatNum"]=row.PatNum.ToString(CultureInfo.InvariantCulture);
				dataRow["ProcNum"]=row.ProcNum.ToString(CultureInfo.InvariantCulture);
				dataRow["ClinicNum"]=row.ClinicNum.ToString(CultureInfo.InvariantCulture);
				dataRow["ParentNum"]=row.ParentNum.ToString(CultureInfo.InvariantCulture);
				table.Rows.Add(dataRow);
			}
			return table;
		}

		///<summary>Two decimals, current culture, no thousands separator, so PIn.Decimal reads it back in any culture.</summary>
		public static string FormatAmount(decimal amount) {
			return Math.Round(amount,2,MidpointRounding.AwayFromZero).ToString("F2",CultureInfo.CurrentCulture);
		}

		#region Cell readers
		private static string GetString(DataRow dataRow,string columnName) {
			if(!dataRow.Table.Columns.Contains(columnName) || dataRow[columnName]==DBNull.Value) {
				return "";
			}
			return dataRow[columnName].ToString();
		}

		private static long GetLong(DataRow dataRow,string columnName) {
			if(!dataRow.Table.Columns.Contains(columnName) || dataRow[columnName]==DBNull.Value) {
				return 0;
			}
			object value=dataRow[columnName];
			if(value is string) {
				return ParseLong((string)value);
			}
			try {
				return Convert.ToInt64(value,CultureInfo.InvariantCulture);
			}
			catch(Exception) {
				return ParseLong(value.ToString());
			}
		}

		private static long ParseLong(string value) {
			long.TryParse(value??"",NumberStyles.Integer,CultureInfo.InvariantCulture,out long result);
			return result;
		}

		private static decimal GetDecimal(DataRow dataRow,string columnName) {
			if(!dataRow.Table.Columns.Contains(columnName) || dataRow[columnName]==DBNull.Value) {
				return 0;
			}
			object value=dataRow[columnName];
			if(value is string) {
				string text=(string)value;
				if(text=="") {
					return 0;
				}
				if(decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out decimal result)) {
					return result;
				}
				return PIn.Decimal(text);
			}
			try {
				return Convert.ToDecimal(value,CultureInfo.InvariantCulture);
			}
			catch(Exception) {
				return 0;
			}
		}

		private static DateTime GetDate(DataRow dataRow,string columnName) {
			if(!dataRow.Table.Columns.Contains(columnName) || dataRow[columnName]==DBNull.Value) {
				return DateTime.MinValue;
			}
			object value=dataRow[columnName];
			if(value is DateTime) {
				return (DateTime)value;
			}
			string text=value.ToString();
			if(text=="") {
				return DateTime.MinValue;
			}
			string[] arrayFormats=new[] { "yyyy-MM-dd","yyyy-MM-dd HH:mm:ss","yyyy-MM-dd H:mm:ss" };
			if(DateTime.TryParseExact(text,arrayFormats,CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime date)) {
				return date;
			}
			return PIn.DateT(text);
		}
		#endregion Cell readers
	}

	///<summary>Per-user Service Date View settings, kept in userodpref (Open Dental 25.4 remembers "Show detailed view" per user; this program
	///also remembers the Account module's view). UserOdFkeyType has no value for them and the enum is a registry of published numbers, so they
	///are stored under UserOdFkeyType.AcctProcBreakdown (13, the Account module's "Show Proc Breakdowns" default) with a Fkey other than 0:
	///Open Dental and this program keep the proc breakdown setting in the row whose Fkey is 0.</summary>
	public static class ServiceDateViewUserPrefs {
		public const UserOdFkeyType FKEY_TYPE=UserOdFkeyType.AcctProcBreakdown;
		///<summary>The proc breakdown setting's own row.</summary>
		public const long FKEY_PROC_BREAKDOWN=0;
		///<summary>"Show detailed view" (Service Date View window) and "Show details" (Account module in service-date view).</summary>
		public const long FKEY_SHOW_DETAILS=1;
		///<summary>The Account module's main grid shows the service-date view.</summary>
		public const long FKEY_ACCOUNT_VIEW=2;

		///<summary>The user's value, or defaultValue when the user has none. Reads the cache.</summary>
		public static bool GetBool(long userNum,long fkey,bool defaultValue) {
			Meth.NoCheckMiddleTierRole();
			UserOdPref userOdPref=UserOdPrefs.GetFirstOrDefault(x => x.UserNum==userNum && x.FkeyType==FKEY_TYPE && x.Fkey==fkey);
			if(userOdPref==null || userOdPref.ValueString=="") {
				return defaultValue;
			}
			return PIn.Bool(userOdPref.ValueString);
		}

		///<summary>Saves the user's value (inserts or updates the row) and refreshes the local cache. The caller signals other workstations if
		///needed.</summary>
		public static void SetBool(long userNum,long fkey,bool value) {
			Meth.NoCheckMiddleTierRole();
			UserOdPref userOdPref=UserOdPrefs.GetFirstOrDefault(x => x.UserNum==userNum && x.FkeyType==FKEY_TYPE && x.Fkey==fkey);
			if(userOdPref==null) {
				userOdPref=new UserOdPref();
				userOdPref.IsNew=true;
				userOdPref.UserNum=userNum;
				userOdPref.Fkey=fkey;
				userOdPref.FkeyType=FKEY_TYPE;
				userOdPref.ClinicNum=0;
			}
			userOdPref.ValueString=POut.Bool(value);
			UserOdPrefs.Upsert(userOdPref);
			UserOdPrefs.RefreshCache();
		}

		///<summary>The proc breakdown setting (Fkey 0). Null when the user has none.</summary>
		public static UserOdPref GetProcBreakdownPref(long userNum) {
			Meth.NoCheckMiddleTierRole();
			return UserOdPrefs.GetFirstOrDefault(x => x.UserNum==userNum && x.FkeyType==FKEY_TYPE && x.Fkey==FKEY_PROC_BREAKDOWN);
		}
	}
}
