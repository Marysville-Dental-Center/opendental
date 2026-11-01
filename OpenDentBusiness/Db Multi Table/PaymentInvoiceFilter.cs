using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Open Dental 26.1 behavior, written for this fork (backlog package M2-PAYW, feature v26_1-account-payment-invoice-filter): the
	///Payment window's Invoice filter on Outstanding Charges. An invoice is a statement with IsInvoice set. Its members are the procedures,
	///adjustments and payment plan charges whose StatementNum is the invoice (how this program's Account module builds invoices), plus any that a
	///stmtlink row of type Proc, Adj or PayPlanCharge attaches to it. With an invoice picked, Outstanding Charges shows only its members, so the Pay
	///button and manual splits work on just those charges.</summary>
	public class PaymentInvoiceFilter {
		///<summary>Amounts within half a cent are equal.</summary>
		private const decimal EPSILON=0.005m;

		#region Database
		///<summary>Every invoice that belongs to one of the patients (the invoice's own patient) or that holds a procedure, adjustment or payment plan
		///charge of one of them, with its members and total. Deleted procedures are not members.</summary>
		public static List<InvoiceInfo> GetInvoicesForPats(List<long> listPatNums) {
			if(listPatNums.IsNullOrEmpty()) {
				return new List<InvoiceInfo>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<InvoiceInfo>>(MethodBase.GetCurrentMethod(),listPatNums);
			}
			string patNums=string.Join(",",listPatNums.Distinct().Select(x => POut.Long(x)));
			List<long> listStatementNumsMembers=new List<long>();
			listStatementNumsMembers.AddRange(Db.GetListLong("SELECT DISTINCT StatementNum FROM procedurelog "
				+"WHERE StatementNum!=0 AND PatNum IN ("+patNums+")"));
			listStatementNumsMembers.AddRange(Db.GetListLong("SELECT DISTINCT StatementNum FROM adjustment "
				+"WHERE StatementNum!=0 AND PatNum IN ("+patNums+")"));
			listStatementNumsMembers.AddRange(Db.GetListLong("SELECT DISTINCT StatementNum FROM payplancharge "
				+"WHERE StatementNum!=0 AND (PatNum IN ("+patNums+") OR Guarantor IN ("+patNums+"))"));
			string command="SELECT * FROM statement WHERE IsInvoice=1 AND (PatNum IN ("+patNums+")";
			if(listStatementNumsMembers.Count>0) {
				command+=" OR StatementNum IN ("+string.Join(",",listStatementNumsMembers.Distinct().Select(x => POut.Long(x)))+")";
			}
			command+=")";
			List<Statement> listStatements=Crud.StatementCrud.SelectMany(command);
			if(listStatements.Count==0) {
				return new List<InvoiceInfo>();
			}
			string statementNums=string.Join(",",listStatements.Select(x => POut.Long(x.StatementNum)));
			//Members by StatementNum, then by stmtlink. Pairs of (StatementNum, FKey).
			List<(long StatementNum,long FKey)> listProcLinks=GetPairs("SELECT StatementNum,ProcNum FROM procedurelog WHERE StatementNum IN ("+statementNums+")");
			List<(long StatementNum,long FKey)> listAdjLinks=GetPairs("SELECT StatementNum,AdjNum FROM adjustment WHERE StatementNum IN ("+statementNums+")");
			List<(long StatementNum,long FKey)> listChargeLinks=GetPairs("SELECT StatementNum,PayPlanChargeNum FROM payplancharge "
				+"WHERE StatementNum IN ("+statementNums+")");
			DataTable tableStmtLinks=Db.GetTable("SELECT StatementNum,FKey,StmtLinkType FROM stmtlink WHERE StatementNum IN ("+statementNums+") "
				+"AND StmtLinkType IN ("+POut.Int((int)StmtLinkTypes.Proc)+","+POut.Int((int)StmtLinkTypes.Adj)+","+POut.Int((int)StmtLinkTypes.PayPlanCharge)+")");
			for(int i=0;i<tableStmtLinks.Rows.Count;i++) {
				long statementNum=PIn.Long(tableStmtLinks.Rows[i]["StatementNum"].ToString());
				long fKey=PIn.Long(tableStmtLinks.Rows[i]["FKey"].ToString());
				StmtLinkTypes stmtLinkType=(StmtLinkTypes)PIn.Int(tableStmtLinks.Rows[i]["StmtLinkType"].ToString());
				if(stmtLinkType==StmtLinkTypes.Proc) {
					listProcLinks.Add((statementNum,fKey));
				}
				else if(stmtLinkType==StmtLinkTypes.Adj) {
					listAdjLinks.Add((statementNum,fKey));
				}
				else {
					listChargeLinks.Add((statementNum,fKey));
				}
			}
			//Amounts of the members, for the invoice totals.
			Dictionary<long,double> dictProcAmounts=Procedures.GetManyProc(listProcLinks.Select(x => x.FKey).Distinct().ToList(),false)
				.Where(x => x.ProcStatus!=ProcStat.D)
				.ToDictionary(x => x.ProcNum,x => x.ProcFeeTotal);
			Dictionary<long,double> dictAdjAmounts=new Dictionary<long,double>();
			List<long> listAdjNums=listAdjLinks.Select(x => x.FKey).Distinct().ToList();
			if(listAdjNums.Count>0) {
				dictAdjAmounts=Adjustments.GetMany(listAdjNums).ToDictionary(x => x.AdjNum,x => x.AdjAmt);
			}
			Dictionary<long,double> dictChargeAmounts=new Dictionary<long,double>();
			List<long> listPayPlanChargeNums=listChargeLinks.Select(x => x.FKey).Distinct().ToList();
			if(listPayPlanChargeNums.Count>0) {
				dictChargeAmounts=PayPlanCharges.GetMany(listPayPlanChargeNums).ToDictionary(x => x.PayPlanChargeNum,x => x.Principal+x.Interest);
			}
			List<InvoiceInfo> listInvoiceInfos=new List<InvoiceInfo>();
			for(int i=0;i<listStatements.Count;i++) {
				long statementNum=listStatements[i].StatementNum;
				InvoiceInfo invoiceInfo=new InvoiceInfo();
				invoiceInfo.StatementNum=statementNum;
				invoiceInfo.PatNum=listStatements[i].PatNum;
				invoiceInfo.DateSent=listStatements[i].DateSent;
				invoiceInfo.ListProcNums=listProcLinks.Where(x => x.StatementNum==statementNum && dictProcAmounts.ContainsKey(x.FKey))
					.Select(x => x.FKey).Distinct().ToList();
				invoiceInfo.ListAdjNums=listAdjLinks.Where(x => x.StatementNum==statementNum && dictAdjAmounts.ContainsKey(x.FKey))
					.Select(x => x.FKey).Distinct().ToList();
				invoiceInfo.ListPayPlanChargeNums=listChargeLinks.Where(x => x.StatementNum==statementNum && dictChargeAmounts.ContainsKey(x.FKey))
					.Select(x => x.FKey).Distinct().ToList();
				invoiceInfo.InvoiceTotal=invoiceInfo.ListProcNums.Sum(x => dictProcAmounts[x])
					+invoiceInfo.ListAdjNums.Sum(x => dictAdjAmounts[x])
					+invoiceInfo.ListPayPlanChargeNums.Sum(x => dictChargeAmounts[x]);
				listInvoiceInfos.Add(invoiceInfo);
			}
			return SortNewestFirst(listInvoiceInfos);
		}

		private static List<(long StatementNum,long FKey)> GetPairs(string command) {
			List<(long StatementNum,long FKey)> listPairs=new List<(long StatementNum,long FKey)>();
			DataTable table=Db.GetTable(command);
			for(int i=0;i<table.Rows.Count;i++) {
				listPairs.Add((PIn.Long(table.Rows[i][0].ToString()),PIn.Long(table.Rows[i][1].ToString())));
			}
			return listPairs;
		}
		#endregion Database

		#region Filter
		///<summary>Newest invoice first: by date, then by statement number.</summary>
		public static List<InvoiceInfo> SortNewestFirst(List<InvoiceInfo> listInvoiceInfos) {
			Meth.NoCheckMiddleTierRole();
			return listInvoiceInfos.OrderByDescending(x => x.DateSent.Date).ThenByDescending(x => x.StatementNum).ToList();
		}

		///<summary>True when the account entry is a procedure, adjustment or payment plan charge on the invoice. Payment plan charges match by
		///PayPlanChargeNum, so both the principal and the interest entries of a charge match.</summary>
		public static bool IsMember(InvoiceInfo invoiceInfo,AccountEntry accountEntry) {
			Meth.NoCheckMiddleTierRole();
			if(invoiceInfo==null || accountEntry==null || accountEntry.Tag==null) {
				return false;
			}
			Type type=accountEntry.GetType();
			if(type==typeof(Procedure) || type==typeof(ProcExtended)) {
				return invoiceInfo.ListProcNums.Contains(accountEntry.ProcNum);
			}
			if(type==typeof(Adjustment)) {
				return invoiceInfo.ListAdjNums.Contains(accountEntry.AdjNum);
			}
			if(accountEntry.PayPlanChargeNum>0 && (type==typeof(FauxAccountEntry) || type==typeof(PayPlanCharge))) {
				return invoiceInfo.ListPayPlanChargeNums.Contains(accountEntry.PayPlanChargeNum);
			}
			return false;
		}

		///<summary>The account entries that the filter shows: all of them when invoiceInfo is null ("All"), otherwise only the invoice's members.</summary>
		public static List<AccountEntry> Filter(List<AccountEntry> listAccountEntries,InvoiceInfo invoiceInfo) {
			Meth.NoCheckMiddleTierRole();
			if(invoiceInfo==null) {
				return listAccountEntries;
			}
			return listAccountEntries.FindAll(x => IsMember(invoiceInfo,x));
		}

		///<summary>What the entry still owed before the payment being edited: its AmountEnd plus what this payment's splits (saved ones of payNum and
		///new ones) took off it.</summary>
		public static decimal GetAmountOwedBeforePayment(AccountEntry accountEntry,long payNum) {
			Meth.NoCheckMiddleTierRole();
			decimal amountThisPayment=(decimal)accountEntry.SplitCollection
				.Where(x => x.SplitNum==0 || (payNum!=0 && x.PayNum==payNum))
				.Sum(x => x.SplitAmt);
			return accountEntry.AmountEnd+amountThisPayment;
		}

		///<summary>True when at least one member still has a balance (before the payment being edited).</summary>
		public static bool HasRemainingBalance(InvoiceInfo invoiceInfo,List<AccountEntry> listAccountEntries,long payNum) {
			Meth.NoCheckMiddleTierRole();
			return listAccountEntries.Any(x => IsMember(invoiceInfo,x) && GetAmountOwedBeforePayment(x,payNum)>EPSILON);
		}

		///<summary>The invoices for the filter's drop-down: those with a remaining balance, newest first. The invoice statementNumKeep (the current
		///choice) stays listed, so paying it off in this payment does not reset the filter.</summary>
		public static List<InvoiceInfo> GetInvoicesToShow(List<InvoiceInfo> listInvoiceInfos,List<AccountEntry> listAccountEntries,long payNum,
			long statementNumKeep=0)
		{
			Meth.NoCheckMiddleTierRole();
			if(listInvoiceInfos.IsNullOrEmpty()) {
				return new List<InvoiceInfo>();
			}
			List<InvoiceInfo> listInvoiceInfosShow=listInvoiceInfos
				.FindAll(x => (statementNumKeep!=0 && x.StatementNum==statementNumKeep) || HasRemainingBalance(x,listAccountEntries,payNum));
			return SortNewestFirst(listInvoiceInfosShow);
		}

		///<summary>The drop-down text: invoice date, statement number and invoice total.</summary>
		public static string GetDisplayText(InvoiceInfo invoiceInfo) {
			Meth.NoCheckMiddleTierRole();
			return invoiceInfo.DateSent.ToShortDateString()+"  #"+invoiceInfo.StatementNum+"  "+invoiceInfo.InvoiceTotal.ToString("c");
		}
		#endregion Filter
	}

	///<summary>An invoice (statement with IsInvoice set) and its members.</summary>
	[Serializable]
	public class InvoiceInfo {
		///<summary>FK to statement.StatementNum.</summary>
		public long StatementNum;
		///<summary>The invoice's patient (statement.PatNum).</summary>
		public long PatNum;
		///<summary>statement.DateSent: the invoice date.</summary>
		public DateTime DateSent;
		///<summary>Sum of the members' amounts: procedure fees (times quantity), adjustment amounts and payment plan charge principal plus interest.</summary>
		public double InvoiceTotal;
		public List<long> ListProcNums=new List<long>();
		public List<long> ListAdjNums=new List<long>();
		public List<long> ListPayPlanChargeNums=new List<long>();
	}
}
