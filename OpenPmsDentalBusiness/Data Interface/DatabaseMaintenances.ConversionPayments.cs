// Database Maintenance tool Conversion Payment Dates (Open Dental 26.2 item 24, "New database maintenance tool to clean up incorrectly dated
// conversion payments"; package P262-TOOLS). On demand only: a Tools tab entry, never part of Run All. Written from public sources only: the
// release note, Open Dental's published 26.2 database documents (an income transfer is a payment with no payment type; paysplit.DatePay is "always in
// perfect synch with Payment date") and Open Dental's public conversion guides, which say that the income transfers made after a conversion to clean
// up the converted history are to be back dated to the date of the conversion "so they do not affect reports". How the tool recognises such a
// transfer is this program's own rule (INFERRED, docs/port-notes/P262-TOOLS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenPmsDentalBusiness {
	public partial class DatabaseMaintenances {
		///<summary>The income transfers the Conversion Payment Dates tool lists for a conversion date: payments without a payment type, $0, with
		///splits that total $0, dated after the conversion date or with no valid date (before 1880). Each row says what its splits are attached to;
		///IsConversionTransfer marks the ones that only move money of the converted history (the tool selects them). Oldest first.</summary>
		public static List<DbmConvPaymentDateRow> GetConversionPaymentDateRows(DateTime dateConversion) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DbmConvPaymentDateRow>>(MethodBase.GetCurrentMethod(),dateConversion);
			}
			dateConversion=dateConversion.Date;
			string command="SELECT payment.PayNum,payment.PatNum,payment.PayDate,payment.DateEntry,patient.LName,patient.FName "
				+"FROM payment "
				+"INNER JOIN paysplit ON paysplit.PayNum=payment.PayNum "
				+"LEFT JOIN patient ON patient.PatNum=payment.PatNum "
				+"WHERE payment.PayType=0 "
				+"AND ROUND(payment.PayAmt,2)=0 "
				+"AND (payment.PayDate>"+POut.Date(dateConversion)+" OR payment.PayDate<"+POut.Date(new DateTime(1880,1,1))+") "
				+"GROUP BY payment.PayNum "
				+"HAVING ROUND(SUM(paysplit.SplitAmt),2)=0 "
				+"ORDER BY payment.PayDate,payment.PayNum";
			DataTable table=Db.GetTable(command);
			List<DbmConvPaymentDateRow> listRows=new List<DbmConvPaymentDateRow>();
			for(int i=0;i<table.Rows.Count;i++) {
				DbmConvPaymentDateRow row=new DbmConvPaymentDateRow();
				row.PayNum=PIn.Long(table.Rows[i]["PayNum"].ToString());
				row.PatNum=PIn.Long(table.Rows[i]["PatNum"].ToString());
				row.PayDate=PIn.Date(table.Rows[i]["PayDate"].ToString());
				row.DateEntry=PIn.Date(table.Rows[i]["DateEntry"].ToString());
				row.PatientName=Patients.GetNameLF(table.Rows[i]["LName"].ToString(),table.Rows[i]["FName"].ToString(),"","");
				listRows.Add(row);
			}
			if(listRows.Count==0) {
				return listRows;
			}
			//What each split is attached to, and the dates of those entries: the procedure, the adjustment, and the split it reverses (FSplitNum).
			command="SELECT paysplit.PayNum,paysplit.PayPlanNum,paysplit.PayPlanChargeNum,paysplit.ProcNum,paysplit.AdjNum,paysplit.FSplitNum,"
					+"procedurelog.ProcDate,adjustment.AdjDate,parent.DatePay ParentDatePay "
				+"FROM paysplit "
				+"LEFT JOIN procedurelog ON procedurelog.ProcNum=paysplit.ProcNum AND paysplit.ProcNum!=0 "
				+"LEFT JOIN adjustment ON adjustment.AdjNum=paysplit.AdjNum AND paysplit.AdjNum!=0 "
				+"LEFT JOIN paysplit parent ON parent.SplitNum=paysplit.FSplitNum AND paysplit.FSplitNum!=0 "
				+"WHERE paysplit.PayNum IN("+string.Join(",",listRows.Select(x => POut.Long(x.PayNum)))+")";
			table=Db.GetTable(command);
			Dictionary<long,DbmConvPaymentDateRow> dictRows=listRows.ToDictionary(x => x.PayNum);
			for(int i=0;i<table.Rows.Count;i++) {
				DbmConvPaymentDateRow row=dictRows[PIn.Long(table.Rows[i]["PayNum"].ToString())];
				row.CountSplits++;
				ConvSplitKind convSplitKind=GetConvSplitKind(dateConversion,
					PIn.Long(table.Rows[i]["PayPlanNum"].ToString()),PIn.Long(table.Rows[i]["PayPlanChargeNum"].ToString()),
					PIn.Long(table.Rows[i]["ProcNum"].ToString()),GetNullableDate(table.Rows[i]["ProcDate"]),
					PIn.Long(table.Rows[i]["AdjNum"].ToString()),GetNullableDate(table.Rows[i]["AdjDate"]),
					PIn.Long(table.Rows[i]["FSplitNum"].ToString()),GetNullableDate(table.Rows[i]["ParentDatePay"]));
				switch(convSplitKind) {
					case ConvSplitKind.Converted:
						row.CountSplitsConverted++;
						break;
					case ConvSplitKind.AfterConversion:
						row.CountSplitsAfterConversion++;
						break;
					case ConvSplitKind.PayPlan:
						row.CountSplitsPayPlan++;
						break;
					default:
						row.CountSplitsUnattached++;
						break;
				}
			}
			return listRows;
		}

		///<summary>What one split of a listed income transfer is attached to. A split on a payment plan is PayPlan. Otherwise the dates of the
		///procedure, the adjustment and the reversed split it is attached to decide: any of them after the conversion date (or missing, for an
		///attachment whose row no longer exists) makes it AfterConversion; otherwise any of them makes it Converted; a split attached to none of
		///them (unearned or unallocated money) is Unattached.</summary>
		public static ConvSplitKind GetConvSplitKind(DateTime dateConversion,long payPlanNum,long payPlanChargeNum,long procNum,DateTime? dateProc,
			long adjNum,DateTime? dateAdj,long fSplitNum,DateTime? dateParentSplit)
		{
			Meth.NoCheckMiddleTierRole();
			if(payPlanNum!=0 || payPlanChargeNum!=0) {
				return ConvSplitKind.PayPlan;
			}
			List<DateTime?> listDates=new List<DateTime?>();
			if(procNum!=0) {
				listDates.Add(dateProc);
			}
			if(adjNum!=0) {
				listDates.Add(dateAdj);
			}
			if(fSplitNum!=0 && dateParentSplit!=null) {
				listDates.Add(dateParentSplit);//a reversed split that no longer exists says nothing about the money's date
			}
			if(listDates.Count==0) {
				return ConvSplitKind.Unattached;
			}
			if(listDates.Any(x => x==null || x.Value.Date>dateConversion.Date)) {
				return ConvSplitKind.AfterConversion;
			}
			return ConvSplitKind.Converted;
		}

		///<summary>Dates the income transfers (and every split of each) on the conversion date. Each one is checked again first: only a payment that
		///is still one the tool lists for this date is changed. Each split is saved with PaySplits.Update, because DatePay is part of the paysplit
		///SecurityHash: a split whose hash was valid is rehashed with the new date, and one whose hash was already invalid keeps its integrity warning
		///(the program's rule for every split edit). Each change gets a DBM log entry and a Payment Edit audit trail entry with the old date. Returns
		///the log text.</summary>
		public static string FixConversionPaymentDates(List<long> listPayNums,DateTime dateConversion) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),listPayNums,dateConversion);
			}
			dateConversion=dateConversion.Date;
			if(dateConversion.Year<1880) {
				throw new ApplicationException(Lans.g("FormDatabaseMaintenance","Enter a valid conversion date."));
			}
			Dictionary<long,DbmConvPaymentDateRow> dictRows=GetConversionPaymentDateRows(dateConversion).ToDictionary(x => x.PayNum);
			List<DbmLog> listDbmLogs=new List<DbmLog>();
			int countChanged=0;
			int countSkipped=0;
			foreach(long payNum in listPayNums.Distinct()) {
				if(!dictRows.TryGetValue(payNum,out DbmConvPaymentDateRow row)) {
					countSkipped++;//changed since it was listed
					continue;
				}
				Db.NonQ("UPDATE payment SET PayDate="+POut.Date(dateConversion)+" WHERE PayNum="+POut.Long(payNum));
				//Not a raw UPDATE: the split hash covers DatePay, and a conversion date inside the hashing window (the usual case, soon after go-live)
				//would otherwise leave every moved split looking tampered with.
				List<PaySplit> listPaySplits=PaySplits.GetForPayment(payNum);
				for(int i=0;i<listPaySplits.Count;i++) {
					PaySplit paySplitOld=listPaySplits[i].Copy();
					listPaySplits[i].DatePay=dateConversion;
					PaySplits.Update(listPaySplits[i],paySplitOld);
				}
				int countSplits=listPaySplits.Count;
				countChanged++;
				string oldDate=(row.PayDate.Year<1880 ? "no date" : row.PayDate.ToString("yyyy-MM-dd"));
				listDbmLogs.Add(new DbmLog(Security.CurUser.UserNum,payNum,DbmLogFKeyType.Payment,DbmLogActionType.Update,"ConversionPaymentDates",
					"Updated PayDate of the income transfer from "+oldDate+" to "+dateConversion.ToString("yyyy-MM-dd")+" (the conversion date), and the "
					+"DatePay of its "+countSplits+" split(s)."));
				SecurityLogs.MakeLogEntry(EnumPermType.PaymentEdit,row.PatNum,"Income transfer dated "+oldDate+" moved to the conversion date "
					+dateConversion.ToShortDateString()+" by the Conversion Payment Dates tool of Database Maintenance.",payNum,DateTime.MinValue);
			}
			if(listDbmLogs.Count>0) {
				DbmLogs.InsertMany(listDbmLogs);
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers dated on the conversion date")+" "+dateConversion.ToShortDateString()
				+": "+countChanged);
			if(countSkipped>0) {
				stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers skipped because they changed since they were listed")+": "
					+countSkipped);
			}
			return stringBuilder.ToString();
		}

		private static DateTime? GetNullableDate(object value) {
			if(value==null || value==DBNull.Value) {
				return null;
			}
			return PIn.Date(value.ToString());
		}
	}

	///<summary>What a split of an income transfer listed by the Conversion Payment Dates tool is attached to.</summary>
	public enum ConvSplitKind {
		///<summary>Unearned or unallocated money: no procedure, adjustment, reversed split or payment plan.</summary>
		Unattached,
		///<summary>Only entries dated on or before the conversion date.</summary>
		Converted,
		///<summary>At least one entry dated after the conversion date, or one that no longer exists.</summary>
		AfterConversion,
		///<summary>A payment plan.</summary>
		PayPlan,
	}

	///<summary>One income transfer listed by the Conversion Payment Dates tool.</summary>
	[Serializable]
	public class DbmConvPaymentDateRow {
		public long PayNum;
		public long PatNum;
		public string PatientName="";
		public DateTime PayDate;
		public DateTime DateEntry;
		public int CountSplits;
		public int CountSplitsConverted;
		public int CountSplitsAfterConversion;
		public int CountSplitsPayPlan;
		public int CountSplitsUnattached;

		///<summary>True when the transfer only moves money of the converted history: at least one split is on an entry dated on or before the
		///conversion date, and none is on a later entry or on a payment plan. Unattached splits (unearned) do not count either way.</summary>
		public bool IsConversionTransfer {
			get {
				return CountSplitsConverted>0 && CountSplitsAfterConversion==0 && CountSplitsPayPlan==0;
			}
		}
	}
}
