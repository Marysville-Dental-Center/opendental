// Database Maintenance check PaymentIncomeTransferIssues (Open Dental 26.2 item 23, "New database maintenance method to fix payments with income
// transfer issues"; package P262-TOOLS). Written from the public release note and Open Dental's published 26.2 database documents only: an income
// transfer is a payment with no payment type (payment.PayType "will be 0 if this is an income transfer"), and a payment's amount "must equal the
// sum of the splits". Which problems the check finds and what Fix changes are this program's own choices (INFERRED, docs/port-notes/P262-TOOLS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenPmsDentalBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Payments with income transfer problems, in three groups:
		///1. An income transfer saved with a payment type: a $0 payment whose splits move money (at least one split is not $0) and total $0, but
		///that has a payment type. Aging, the Family Balancer and other income transfer code only treat payments without a payment type as income
		///transfers, so these move money between providers and procedures as if they were income. Fix gives them no payment type (an income
		///transfer). It leaves alone payments on a deposit, card or online payments, recurring charges and payments linked to an accounting
		///transaction, and changes no amount.
		///2. An income transfer (no payment type) with an amount other than $0. Manual fix.
		///3. An income transfer (no payment type, $0) whose splits do not total $0, so it adds money to or takes money from the account. Manual fix.
		///Every payment Fix changes gets a DBM log entry with its old payment type.</summary>
		[DbmMethodAttr(HasBreakDown=true,HasExplain=true,IsReplicationUnsafe=true)]
		public static string PaymentIncomeTransferIssues(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			List<DbmIncomeTransferRow> listRowsWithPayType=GetIncomeTransfersWithPayType();
			List<DbmIncomeTransferRow> listRowsManual=GetIncomeTransfersNotBalanced();
			List<DbmIncomeTransferRow> listRowsWithAmount=listRowsManual.FindAll(x => x.HasAmount);
			List<DbmIncomeTransferRow> listRowsNotZero=listRowsManual.FindAll(x => !x.HasAmount);
			if(listRowsWithPayType.Count==0 && listRowsManual.Count==0 && !verbose) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder();
			switch(dbmMode) {
				case DbmMode.Check:
					stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers saved with a payment type")+": "+listRowsWithPayType.Count);
					break;
				case DbmMode.Fix:
					int countFixed=FixIncomeTransfersWithPayType(listRowsWithPayType);
					stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers saved with a payment type changed to no payment type")+": "
						+countFixed);
					break;
				case DbmMode.Breakdown:
					stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers saved with a payment type (Fix removes the payment type)")+": "
						+listRowsWithPayType.Count);
					AppendIncomeTransferRows(stringBuilder,listRowsWithPayType);
					break;
			}
			stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers with an amount other than zero")+": "+listRowsWithAmount.Count);
			if(dbmMode==DbmMode.Breakdown) {
				AppendIncomeTransferRows(stringBuilder,listRowsWithAmount);
			}
			stringBuilder.AppendLine(Lans.g("FormDatabaseMaintenance","Income transfers whose splits do not total zero")+": "+listRowsNotZero.Count);
			if(dbmMode==DbmMode.Breakdown) {
				AppendIncomeTransferRows(stringBuilder,listRowsNotZero);
			}
			if(listRowsManual.Count>0) {
				if(dbmMode==DbmMode.Breakdown) {
					stringBuilder.AppendLine("   "+Lans.g("FormDatabaseMaintenance","The income transfers with an amount, or whose splits do not total zero, "
						+"need to be fixed manually."));
				}
				else {
					stringBuilder.AppendLine("   "+Lans.g("FormDatabaseMaintenance","Manual fix needed.  Double click to see a break down."));
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>Payments that are income transfers in all but the payment type (group 1 of PaymentIncomeTransferIssues), oldest first.</summary>
		public static List<DbmIncomeTransferRow> GetIncomeTransfersWithPayType() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DbmIncomeTransferRow>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT payment.PayNum,payment.PatNum,payment.PayType,payment.PayDate,payment.PayAmt,SUM(paysplit.SplitAmt) SplitTotal,"
					+"patient.LName,patient.FName "
				+"FROM payment "
				+"INNER JOIN paysplit ON paysplit.PayNum=payment.PayNum "
				+"LEFT JOIN patient ON patient.PatNum=payment.PatNum "
				+"WHERE payment.PayType!=0 "
				+"AND ROUND(payment.PayAmt,2)=0 "
				+"AND payment.DepositNum=0 "
				+"AND payment.PaymentSource=0 "//not a card or online payment
				+"AND payment.IsRecurringCC=0 "
				+"AND NOT EXISTS(SELECT * FROM transaction WHERE transaction.PayNum=payment.PayNum) "//not linked to the accounting module
				+"GROUP BY payment.PayNum "
				+"HAVING ROUND(SUM(paysplit.SplitAmt),2)=0 "
				+"AND SUM(ROUND(paysplit.SplitAmt,2)!=0)>0 "//moves money; a payment whose splits are all $0 (a declined card) is not a transfer
				+"ORDER BY payment.PayDate,payment.PayNum";
			return ToIncomeTransferRows(Db.GetTable(command));
		}

		///<summary>Income transfers (no payment type) with an amount other than $0, or whose splits do not total $0 (groups 2 and 3 of
		///PaymentIncomeTransferIssues), oldest first. A transfer without splits is left to PaymentMissingPaySplit.</summary>
		public static List<DbmIncomeTransferRow> GetIncomeTransfersNotBalanced() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DbmIncomeTransferRow>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT payment.PayNum,payment.PatNum,payment.PayType,payment.PayDate,payment.PayAmt,SUM(paysplit.SplitAmt) SplitTotal,"
					+"patient.LName,patient.FName "
				+"FROM payment "
				+"INNER JOIN paysplit ON paysplit.PayNum=payment.PayNum "
				+"LEFT JOIN patient ON patient.PatNum=payment.PatNum "
				+"WHERE payment.PayType=0 "
				+"GROUP BY payment.PayNum "
				+"HAVING ROUND(payment.PayAmt,2)!=0 OR ROUND(SUM(paysplit.SplitAmt),2)!=0 "
				+"ORDER BY payment.PayDate,payment.PayNum";
			return ToIncomeTransferRows(Db.GetTable(command));
		}

		///<summary>Gives the payments no payment type, each only while it still has the one it was found with, and logs each change. Returns the
		///number of payments changed.</summary>
		private static int FixIncomeTransfersWithPayType(List<DbmIncomeTransferRow> listRows) {
			List<DbmLog> listDbmLogs=new List<DbmLog>();
			int countFixed=0;
			for(int i=0;i<listRows.Count;i++) {
				string command="UPDATE payment SET PayType=0 WHERE PayNum="+POut.Long(listRows[i].PayNum)+" AND PayType="+POut.Long(listRows[i].PayType);
				if(Db.NonQ(command)==0) {
					continue;
				}
				countFixed++;
				listDbmLogs.Add(new DbmLog(Security.CurUser.UserNum,listRows[i].PayNum,DbmLogFKeyType.Payment,DbmLogActionType.Update,
					nameof(PaymentIncomeTransferIssues),"Updated PayType from "+listRows[i].PayType+" ("+Defs.GetName(DefCat.PaymentTypes,listRows[i].PayType)
					+") to 0 (income transfer). The payment's splits total $0; no amount was changed."));
			}
			if(listDbmLogs.Count>0) {
				DbmLogs.InsertMany(listDbmLogs);
			}
			return countFixed;
		}

		private static List<DbmIncomeTransferRow> ToIncomeTransferRows(DataTable table) {
			List<DbmIncomeTransferRow> listRows=new List<DbmIncomeTransferRow>();
			for(int i=0;i<table.Rows.Count;i++) {
				DbmIncomeTransferRow row=new DbmIncomeTransferRow();
				row.PayNum=PIn.Long(table.Rows[i]["PayNum"].ToString());
				row.PatNum=PIn.Long(table.Rows[i]["PatNum"].ToString());
				row.PayType=PIn.Long(table.Rows[i]["PayType"].ToString());
				row.PayDate=PIn.Date(table.Rows[i]["PayDate"].ToString());
				row.PayAmt=PIn.Double(table.Rows[i]["PayAmt"].ToString());
				row.SplitTotal=PIn.Double(table.Rows[i]["SplitTotal"].ToString());
				row.PatientName=Patients.GetNameLF(table.Rows[i]["LName"].ToString(),table.Rows[i]["FName"].ToString(),"","");
				listRows.Add(row);
			}
			return listRows;
		}

		private static void AppendIncomeTransferRows(StringBuilder stringBuilder,List<DbmIncomeTransferRow> listRows) {
			for(int i=0;i<listRows.Count;i++) {
				stringBuilder.AppendLine("   "+listRows[i].PatNum+"  "+listRows[i].PatientName+"  "+listRows[i].PayDate.ToShortDateString()
					+"  "+Lans.g("FormDatabaseMaintenance","Amount")+": "+listRows[i].PayAmt.ToString("c")
					+"  "+Lans.g("FormDatabaseMaintenance","Split total")+": "+listRows[i].SplitTotal.ToString("c"));
			}
		}
	}

	///<summary>One payment found by PaymentIncomeTransferIssues.</summary>
	public class DbmIncomeTransferRow {
		public long PayNum;
		public long PatNum;
		///<summary>FK to definition.DefNum, 0 for an income transfer.</summary>
		public long PayType;
		public System.DateTime PayDate;
		public double PayAmt;
		public double SplitTotal;
		public string PatientName="";

		///<summary>True when the payment amount is not $0 (rounded to cents).</summary>
		public bool HasAmount {
			get {
				return System.Math.Round(PayAmt,2)!=0;
			}
		}
	}
}
