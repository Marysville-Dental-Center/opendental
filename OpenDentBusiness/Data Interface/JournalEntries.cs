using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using CodeBase;

namespace OpenDentBusiness{
	///<summary></summary>
	public class JournalEntries {
		///<summary>Used when displaying the splits for a transaction.</summary>
		public static List<JournalEntry> GetForTrans(long transactionNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<JournalEntry>>(MethodBase.GetCurrentMethod(),transactionNum);
			}
			string command=
				"SELECT * FROM journalentry "
				+"WHERE TransactionNum="+POut.Long(transactionNum);
			return Crud.JournalEntryCrud.SelectMany(command);
		}

		///<summary>Used to display a list of entries for one account. Even though we're passing in a dateFrom, we always get full list for assets, liabilities, and equity in order to get the running total, even if we don't show those rows.</summary>
		public static List<JournalEntry> GetForAccount(Account account,DateTime dateFrom,DateTime dateTo) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<JournalEntry>>(MethodBase.GetCurrentMethod(),account,dateFrom,dateTo);
			}
			string command;
			List<JournalEntry> listJournalEntries=new List<JournalEntry>();
			#region StartingBalanceRow
			//Get history balance and create a starting balance row.
			DateTime dateFirstOfYearFrom =new DateTime(dateFrom.Year,1,1);
			DateTime dateFirstOfYearTo=new DateTime(dateTo.Year,1,1);
			command="SELECT SUM(ROUND(CreditAmt,3)) SumCredit, "
				+"SUM(ROUND(DebitAmt,3)) SumDebit "
				+"FROM journalentry "
				+"WHERE AccountNum='"+POut.Long(account.AccountNum)+"' "
				+"AND DateDisplayed < "+POut.Date(dateFrom);
			if(account.AcctType.In(AccountType.Income,AccountType.Expense)){
				//For Income and Expense, if their dateFrom is not 1/1, then we might need a starting balance for part of a year.
				//Usually, with a 1/1 start date, this will result in no range, and a $0 starting row that doesn't show.
				command+=" AND DateDisplayed >= "+POut.Date(dateFirstOfYearFrom);
			}
			DataTable table=Db.GetTable(command);//always exactly one row
			double credit=PIn.Double(table.Rows[0]["SumCredit"].ToString());
			double debit=PIn.Double(table.Rows[0]["SumDebit"].ToString());
			double balStart = 0;
			if(Accounts.DebitIsPos(account.AcctType)) {
				balStart=debit-credit;
			}
			else {
				balStart=credit-debit;
			}
			double amtBalRE=0;
			if(account.IsRetainedEarnings){
				//Now we need an entry that's the sum all previous RE entries
				command="SELECT SUM(ROUND(CreditAmt,3))-SUM(ROUND(DebitAmt,3)) "
					+"FROM journalentry,account "
					+"WHERE journalentry.AccountNum=account.AccountNum "
					+"AND (account.AcctType='"+POut.Enum(AccountType.Income)+"' "
					+"OR account.AcctType='"+POut.Enum(AccountType.Expense)+"') "
					+"AND DateDisplayed < "+POut.Date(dateFirstOfYearFrom);
				amtBalRE=PIn.Double(Db.GetCount(command));//always a single cell
			}
			balStart+=amtBalRE;//no change for non-RE
			JournalEntry journalEntry=new JournalEntry();
			journalEntry.CheckNumber="";
			if(dateFrom.Year>1880){
				journalEntry.DateDisplayed=dateFrom.AddDays(-1);
			}
			journalEntry.Memo=Lans.g("FormJournal","(starting balance)");
			journalEntry.Splits="";
			if(Accounts.DebitIsPos(account.AcctType)) {
				if(balStart>=0){
					journalEntry.DebitAmt=balStart;
				}
				else{
					journalEntry.CreditAmt=-balStart;
				}
			}
			else{
				if(balStart>=0){
					journalEntry.CreditAmt=balStart;
				}
				else{
					journalEntry.DebitAmt=-balStart;
				}
			}
			//The debit or credit will be used later to arrive at a starting bal to show
			listJournalEntries.Add(journalEntry);
			#endregion StartingBalanceRow
			#region RetainedEarningsAutoEntries
			if(account.IsRetainedEarnings){
				//For Retained Earnings, add the auto entries for each year
				//Only show the ones in our date range.
				//RE entries prior to our date range are already included in starting bal.
				//This will normally return no rows, unless date span is greater than one year.
				//dateFrom could be empty, so 1/1/1
				command="SELECT SUM(ROUND(CreditAmt,3))-SUM(ROUND(DebitAmt,3)) AS Amount, "
					+"YEAR(journalentry.DateDisplayed) AS yearDis "
					+"FROM journalentry,account "
					+"WHERE journalentry.AccountNum=account.AccountNum "
					+"AND (account.AcctType='"+POut.Enum(AccountType.Income)+"' "
					+"OR account.AcctType='"+POut.Enum(AccountType.Expense)+"') "
					+"AND DateDisplayed < "+POut.Date(dateFirstOfYearTo)+" "
					+"AND DateDisplayed >= "+POut.Date(dateFirstOfYearFrom)+" "
					+"GROUP BY yearDis";
				table=Db.GetTable(command);
				for(int i=0;i<table.Rows.Count;i++) {
					journalEntry=new JournalEntry();
					journalEntry.CheckNumber="";
					journalEntry.Splits="";
					int year=PIn.Int(table.Rows[i]["yearDis"].ToString());
					journalEntry.DateDisplayed=new DateTime(year,12,31);
					double amount=PIn.Double(table.Rows[i]["Amount"].ToString());
					if(amount>0){
						journalEntry.CreditAmt=amount;
					}
					else{
						journalEntry.DebitAmt=-amount;
					}
					journalEntry.Memo=Lans.g("FormJournal","(auto)");
					listJournalEntries.Add(journalEntry);
				}
			}
			#endregion RetainedEarningsAutoEntries
			#region ExpenseIncomeAutoEntries
			//For income and expense accounts, if our range showing crosses any annual boundaries,
			//then we need to have an auto entry at each of those points to zero out the running balance
			if(account.AcctType.In(AccountType.Income,AccountType.Expense)){
				command="SELECT SUM(ROUND(CreditAmt,3))-SUM(ROUND(DebitAmt,3)) AS Amount, "
					+"YEAR(DateDisplayed) AS yearDis "
					+"FROM journalentry "
					+"WHERE AccountNum='"+POut.Long(account.AccountNum)+"' "
					+"AND DateDisplayed < "+POut.Date(dateFirstOfYearTo)+" "
					+"AND DateDisplayed >= "+POut.Date(dateFirstOfYearFrom)+" "
					+"GROUP BY yearDis";
				table=Db.GetTable(command);
				for(int i=0;i<table.Rows.Count;i++) {
					journalEntry=new JournalEntry();
					journalEntry.CheckNumber="";
					journalEntry.Splits="";
					int year=PIn.Int(table.Rows[i]["yearDis"].ToString());
					journalEntry.DateDisplayed=new DateTime(year,12,31);
					double amount=PIn.Double(table.Rows[i]["Amount"].ToString());
					//this math is the same for both types because the query got credits as pos.
					if(amount>0){
						journalEntry.DebitAmt=amount;
					}
					else{
						journalEntry.CreditAmt=-amount;
					}
					journalEntry.Memo=Lans.g("FormJournal","(auto)");
					listJournalEntries.Add(journalEntry);
				}
			}
			#endregion ExpenseIncomeAutoEntries
			command=
				"SELECT * FROM journalentry "
				+"WHERE AccountNum='"+POut.Long(account.AccountNum)+"' "
				+"AND DateDisplayed >= "+POut.Date(dateFrom)+" "
				+"AND DateDisplayed <= "+POut.Date(dateTo)+" "
				+"ORDER BY DateDisplayed";
			listJournalEntries.AddRange(Crud.JournalEntryCrud.SelectMany(command));
			listJournalEntries=listJournalEntries.OrderBy(x=>x.DateDisplayed)
				.ThenByDescending(x=>x.AccountNum).ToList();//this makes the auto entry come after other entries on that date
			return listJournalEntries;
		}

		///<summary>Used in reconcile window.</summary>
		public static List <JournalEntry> GetForReconcile(long accountNum,bool includeUncleared,long reconcileNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List <JournalEntry>>(MethodBase.GetCurrentMethod(),accountNum,includeUncleared,reconcileNum);
			}
			string command=
				"SELECT * FROM journalentry "
				+"WHERE AccountNum="+POut.Long(accountNum)
				+" AND (ReconcileNum="+POut.Long(reconcileNum);
			if(includeUncleared) {
				command+=" OR ReconcileNum=0)";
			}
			else {
				command+=")";
			}
			command+=" ORDER BY DateDisplayed";
			return Crud.JournalEntryCrud.SelectMany(command);
		}

		///<summary></summary>
		public static long Insert(JournalEntry journalEntry) {
			journalEntry.SecUserNumEntry=Security.CurUser.UserNum;//Before middle tier check to catch user at workstation
			journalEntry.SecUserNumEdit=Security.CurUser.UserNum;
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				journalEntry.JournalEntryNum=Meth.GetLong(MethodBase.GetCurrentMethod(),journalEntry);
				return journalEntry.JournalEntryNum;
			}
			if(journalEntry.DebitAmt<0 || journalEntry.CreditAmt<0){
				throw new ApplicationException(Lans.g("JournalEntries","Error. Credit and debit must both be positive."));
			}
			return Crud.JournalEntryCrud.Insert(journalEntry);
		}

		///<summary></summary>
		public static void Update(JournalEntry journalEntry) {
			journalEntry.SecUserNumEdit=Security.CurUser.UserNum;//Before middle tier check to catch user at workstation
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),journalEntry);
				return;
			}
			if(journalEntry.DebitAmt<0 || journalEntry.CreditAmt<0) {
				throw new ApplicationException(Lans.g("JournalEntries","Error. Credit and debit must both be positive."));
			}
			Crud.JournalEntryCrud.Update(journalEntry);
		}

		///<summary></summary>
		public static void Delete(JournalEntry journalEntry) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),journalEntry);
				return;
			}
			//This method is only used once in synch below.  Validation needs to be done, but doing it inside the loop would be dangerous.
			//So validation is done in the UI as follows:
			//1. Deleting an entire transaction is validated in business layer.
			//2. When editing a transaction attached to reconcile, simple view is blocked.
			//3. Double clicking on grid lets you change JEs not attached to reconcile.
			//4. Double clicking on grid lets you change notes even if attached to reconcile.
			string command= "DELETE FROM journalentry WHERE JournalEntryNum = "+POut.Long(journalEntry.JournalEntryNum);
			Db.NonQ(command);
		}

		///<summary>Used in FormTransactionEdit to synch database with changes user made to the journalEntry list for a transaction.  Must supply an old list for comparison.  Only the differences are saved.  Surround with try/catch, because it will thrown an exception if any entries are negative.</summary>
		public static void UpdateList(List<JournalEntry> listJournalEntriesOld,List<JournalEntry> listJournalEntriesNew) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listJournalEntriesNew.Count;i++){
				if(listJournalEntriesNew[i].DebitAmt<0 || listJournalEntriesNew[i].CreditAmt<0){
					throw new ApplicationException(Lans.g("JournalEntries","Error. Credit and debit must both be positive."));
				}
			}
			JournalEntry journalEntryNew;
			for(int i=0;i<listJournalEntriesOld.Count;i++) {//loop through the old list
				journalEntryNew=null;
				for(int j=0;j<listJournalEntriesNew.Count;j++) {
					if(listJournalEntriesNew[j]==null || listJournalEntriesNew[j].JournalEntryNum==0) {
						continue;
					}
					if(listJournalEntriesOld[i].JournalEntryNum==listJournalEntriesNew[j].JournalEntryNum) {
						journalEntryNew=listJournalEntriesNew[j];
						break;
					}
				}
				if(journalEntryNew==null) {
					//journalentry with matching journalEntryNum was not found, so it must have been deleted
					Delete((JournalEntry)listJournalEntriesOld[i]);
					continue;
				}
				//journalentry was found with matching journalEntryNum, so check for changes
				if(journalEntryNew.AccountNum != listJournalEntriesOld[i].AccountNum
					|| journalEntryNew.DateDisplayed != listJournalEntriesOld[i].DateDisplayed
					|| journalEntryNew.DebitAmt != listJournalEntriesOld[i].DebitAmt
					|| journalEntryNew.CreditAmt != listJournalEntriesOld[i].CreditAmt
					|| journalEntryNew.Memo != listJournalEntriesOld[i].Memo
					|| journalEntryNew.Splits != listJournalEntriesOld[i].Splits
					|| journalEntryNew.CheckNumber!= listJournalEntriesOld[i].CheckNumber
					|| journalEntryNew.Payee != listJournalEntriesOld[i].Payee
					|| journalEntryNew.Notes != listJournalEntriesOld[i].Notes
					|| journalEntryNew.NeedsReview != listJournalEntriesOld[i].NeedsReview)
				{
					Update(journalEntryNew);
				}
			}
			for(int i=0;i<listJournalEntriesNew.Count;i++) {//loop through the new list
				if(listJournalEntriesNew[i]==null) {
					continue;
				}
				if(listJournalEntriesNew[i].JournalEntryNum!=0) {
					continue;
				}
				//entry with journalEntryNum=0, so it's new
				Insert(listJournalEntriesNew[i]);
			}
		}

		///<summary>Called from FormTransactionEdit.</summary>
		public static bool AttachedToReconcile(List<JournalEntry> listJournalEntries){
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listJournalEntries.Count;i++){
				if(listJournalEntries[i].ReconcileNum!=0){
					return true;
				}
			}
			return false;
		}

		///<summary>Called from FormTransactionEdit.</summary>
		public static DateTime GetReconcileDate(List<JournalEntry> listJournalEntries) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listJournalEntries.Count;i++) {
				if(listJournalEntries[i].ReconcileNum!=0) {
					return Reconciles.GetOne(listJournalEntries[i].ReconcileNum).DateReconcile;
				}
			}
			return DateTime.MinValue;
		}

		///<summary>Called once from FormReconcileEdit when closing.  Saves the reconcileNum for every item in the list.</summary>
		public static void SaveList(List <JournalEntry> listJournalEntries,long reconcileNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listJournalEntries,reconcileNum);
				return;
			}
			string command="UPDATE journalentry SET ReconcileNum=0 WHERE";
			string str="";
			for(int i=0;i<listJournalEntries.Count;i++){
				if(listJournalEntries[i].ReconcileNum!=0){
					continue;
				}
				if(str!=""){
					str+=" OR";
				}
				str+=" JournalEntryNum="+POut.Long(listJournalEntries[i].JournalEntryNum);
			}
			if(str!=""){
				command+=str;
				Db.NonQ(command);
			}
			command="UPDATE journalentry SET ReconcileNum="+POut.Long(reconcileNum)+" WHERE";
			str="";
			for(int i=0;i<listJournalEntries.Count;i++) {
				if(listJournalEntries[i].ReconcileNum!=reconcileNum) {
					continue;
				}
				if(str!="") {
					str+=" OR";
				}
				str+=" JournalEntryNum="+POut.Long(listJournalEntries[i].JournalEntryNum);
			}
			if(str!=""){
				command+=str;
				Db.NonQ(command);
			}
		}

		///<Summary>Returns true if the account was used in any journal entry.</Summary>
		public static bool IsInUse(long accountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),accountNum);
			}
			string command="SELECT COUNT(*) FROM journalentry "
				+"WHERE AccountNum="+POut.Long(accountNum);
			string count=Db.GetCount(command);
			if(count=="0"){
				return false;
			}
			return true;
		}

		#region Payee, Notes, Needs Review and next check number (Open Dental 25.3 parity)
		///<summary>The check number used when an account has no purely numeric check number yet.</summary>
		public const long CHECK_NUMBER_FIRST=1001;
		///<summary>Maximum length of journalentry.Payee (varchar(255)).</summary>
		public const int PAYEE_MAX_LENGTH=255;
		///<summary>Maximum length of journalentry.Notes (text).</summary>
		public const int NOTES_MAX_LENGTH=65535;

		///<summary>The next check number for an account: one more than the highest purely numeric check number among its journal entries, or
		///CHECK_NUMBER_FIRST when there is none. Check numbers like "DEP" or "AW" are ignored. Entries of transactionNumExclude (the transaction
		///being edited) are not counted, so a transaction that already holds the highest number does not push the suggestion past itself.</summary>
		public static string GetNextCheckNumber(long accountNum,long transactionNumExclude=0) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),accountNum,transactionNumExclude);
			}
			//The REGEXP only narrows the rows; CalcNextCheckNumber does the real check, so collation or regex flavor cannot change the answer.
			string command="SELECT DISTINCT CheckNumber FROM journalentry "
				+"WHERE AccountNum="+POut.Long(accountNum)+" "
				+"AND CheckNumber REGEXP '^[[:space:]]*[0-9]+[[:space:]]*$'";
			if(transactionNumExclude!=0) {
				command+=" AND TransactionNum!="+POut.Long(transactionNumExclude);
			}
			List<string> listCheckNumbers=Db.GetListString(command);
			return CalcNextCheckNumber(listCheckNumbers);
		}

		///<summary>One more than the highest purely numeric value in the list (surrounding spaces allowed), or CHECK_NUMBER_FIRST when there is none.
		///Leading zeros of the highest number are kept, so "000123" is followed by "000124". Values too large for a long are ignored.</summary>
		public static string CalcNextCheckNumber(IEnumerable<string> listCheckNumbers) {
			Meth.NoCheckMiddleTierRole();
			long numMax=-1;
			int widthMax=0;
			foreach(string checkNumberRaw in listCheckNumbers??new List<string>()) {
				if(checkNumberRaw==null) {
					continue;
				}
				string checkNumber=checkNumberRaw.Trim();
				if(checkNumber=="" || !checkNumber.All(x => x>='0' && x<='9')) {
					continue;
				}
				long num;
				if(!long.TryParse(checkNumber,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out num)
					|| num==long.MaxValue)
				{
					continue;
				}
				if(num>numMax || (num==numMax && checkNumber.Length>widthMax)) {
					numMax=num;
					widthMax=checkNumber.Length;
				}
			}
			if(numMax<0) {
				return CHECK_NUMBER_FIRST.ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
			return (numMax+1).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(widthMax,'0');
		}

		///<summary>True if the entry should be shown for the Find Text box of Transaction History: the text appears, ignoring case, in its Memo, check
		///number, splits, Payee or Notes. An empty search matches everything.</summary>
		public static bool MatchesFindText(JournalEntry journalEntry,string findText) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(findText)) {
				return true;
			}
			string findUpper=findText.ToUpper();
			return new[] { journalEntry.Memo,journalEntry.CheckNumber,journalEntry.Splits,journalEntry.Payee,journalEntry.Notes }
				.Any(x => !string.IsNullOrEmpty(x) && x.ToUpper().Contains(findUpper));
		}

		///<summary>True when every entry has the same Memo, Payee and Notes, the fields that the simple view of Edit Transaction and the
		///"same for all" checkbox of the compound view apply to the whole transaction. True for an empty list.</summary>
		public static bool AreSharedFieldsSame(List<JournalEntry> listJournalEntries) {
			Meth.NoCheckMiddleTierRole();
			if(listJournalEntries==null || listJournalEntries.Count<2) {
				return true;
			}
			JournalEntry journalEntryFirst=listJournalEntries[0];
			return listJournalEntries.All(x => (x.Memo??"")==(journalEntryFirst.Memo??"")
				&& (x.Payee??"")==(journalEntryFirst.Payee??"")
				&& (x.Notes??"")==(journalEntryFirst.Notes??""));
		}

		///<summary>Copies Memo, Payee and Notes from the source entry to every entry in the list. Used by the "same for all" checkbox.</summary>
		public static void CopySharedFields(JournalEntry journalEntrySource,List<JournalEntry> listJournalEntries) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listJournalEntries.Count;i++) {
				listJournalEntries[i].Memo=journalEntrySource.Memo;
				listJournalEntries[i].Payee=journalEntrySource.Payee;
				listJournalEntries[i].Notes=journalEntrySource.Notes;
			}
		}

		///<summary>The Needs Review state of a transaction: true if any of its entries has the flag.</summary>
		public static bool IsAnyNeedsReview(List<JournalEntry> listJournalEntries) {
			Meth.NoCheckMiddleTierRole();
			return listJournalEntries!=null && listJournalEntries.Any(x => x.NeedsReview);
		}

		///<summary>Writes the Needs Review flag on every entry of a transaction, as the Edit Transaction checkbox does.</summary>
		public static void SetNeedsReview(List<JournalEntry> listJournalEntries,bool needsReview) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listJournalEntries.Count;i++) {
				listJournalEntries[i].NeedsReview=needsReview;
			}
		}

		///<summary>The Payee of a transaction for display and audit: the distinct non-empty payees of its entries, in entry order, joined by ", ".
		///Empty when no entry has one.</summary>
		public static string GetPayeeSummary(List<JournalEntry> listJournalEntries) {
			Meth.NoCheckMiddleTierRole();
			if(listJournalEntries==null) {
				return "";
			}
			return string.Join(", ",listJournalEntries.Select(x => x.Payee??"").Where(x => x!="").Distinct());
		}

		///<summary>Audit text for the Payee and Needs Review changes between the saved entries of a transaction and the entries about to be saved,
		///for the AccountingCreate and AccountingEdit log entries, like "Payee: 'A' -> 'B'. Needs Review: off -> on." Empty when neither changed. For
		///a new transaction pass an empty old list.</summary>
		public static string GetAuditChangeText(List<JournalEntry> listJournalEntriesOld,List<JournalEntry> listJournalEntriesNew) {
			Meth.NoCheckMiddleTierRole();
			List<string> listChanges=new List<string>();
			string payeeOld=GetPayeeSummary(listJournalEntriesOld);
			string payeeNew=GetPayeeSummary(listJournalEntriesNew);
			if(payeeOld!=payeeNew) {
				listChanges.Add(Lans.g("JournalEntries","Payee")+": '"+payeeOld+"' -> '"+payeeNew+"'");
			}
			bool needsReviewOld=IsAnyNeedsReview(listJournalEntriesOld);
			bool needsReviewNew=IsAnyNeedsReview(listJournalEntriesNew);
			if(needsReviewOld!=needsReviewNew) {
				listChanges.Add(Lans.g("JournalEntries","Needs Review")+": "+(needsReviewOld?"on":"off")+" -> "+(needsReviewNew?"on":"off"));
			}
			if(listChanges.Count==0) {
				return "";
			}
			return string.Join(". ",listChanges)+".";
		}
		#endregion Payee, Notes, Needs Review and next check number
	}
}




