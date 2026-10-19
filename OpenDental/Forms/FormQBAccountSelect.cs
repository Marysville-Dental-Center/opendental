using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDentBusiness.Qbo;

namespace OpenDental {
	public partial class FormQBAccountSelect:FormODBase {
		///<summary>List of deposit accounts from pref. for QuickBooks</summary>
		private List<string> _listDepositAccountsQB;
		///<summary>List of income accounts from pref. for QuickBooks</summary>
		private List<string> _listIncomeAccountsQB;
		///<summary>The selected account when clicking OK.  Used in FormDepositEdit to pass to QuickBooks or QuickBooks Online.</summary>
		public string DepositAccountSelected="";
		///<summary>The selected account when clicking OK.  Used in FormDepositEdit to pass to QuickBooks or QuickBooks Online.</summary>
		public string IncomeAccountSelected="";
		///<summary>The QuickBooks Online Id of the selected deposit account. Only set for QuickBooks Online. Matching by Id never confuses two accounts
		///with the same name.</summary>
		public string DepositAccountIdSelected="";
		///<summary>The QuickBooks Online Id of the selected income account. Only set for QuickBooks Online.</summary>
		public string IncomeAccountIdSelected="";
		///<summary>True if user is using QuickBooks Online instead of regular QuickBooks.</summary>
		private bool _isQuickBooksOnline;

		public FormQBAccountSelect(bool isQuickBooksOnline) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_isQuickBooksOnline=isQuickBooksOnline;
		}

		private void FormQBAccountSelect_Load(object sender,EventArgs e) {
			if(_isQuickBooksOnline) {
				FillQuickBooksOnlineAccounts();
				return;
			}
			FillQuickBooksAccounts();
			
		}

		private void FillQuickBooksAccounts() {
			_listDepositAccountsQB=Accounts.GetDepositAccountsQB();
			for(int i=0;i<_listDepositAccountsQB.Count;i++) {
				comboDepositAccount.Items.Add(_listDepositAccountsQB[i]);
			}
			comboDepositAccount.SelectedIndex=0;
			_listIncomeAccountsQB=Accounts.GetIncomeAccountsQB();
			for(int i=0;i<_listIncomeAccountsQB.Count;i++) {
				comboIncomeAccount.Items.Add(_listIncomeAccountsQB[i]);
			}
			comboIncomeAccount.SelectedIndex=0;
		}

		private void FillQuickBooksOnlineAccounts() {
			//Missing rows read as blank, so this works before anyone has opened the QuickBooks Online setup window.
			QboSettings qboSettings=QboSettings.Load();
			FillAccountsFromProgramProperty(qboSettings.DepositAccounts,comboDepositAccount);
			comboDepositAccount.SelectedIndex=0;
			FillAccountsFromProgramProperty(qboSettings.IncomeAccounts,comboIncomeAccount);
			comboIncomeAccount.SelectedIndex=0;
		}

		private void FillAccountsFromProgramProperty(string propertyValue,OpenDental.UI.ComboBox comboBox) {
			List<QuickBooksOnlineAccount> listAccounts=QuickBooksOnlineAccount.StringAccountsToList(propertyValue)
				.OrderBy(x => x.Name).ToList();
			for(int i=0;i<listAccounts.Count;i++) {
				comboBox.Items.Add(listAccounts[i].Name,listAccounts[i]);
			}
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(_isQuickBooksOnline) {
				QuickBooksOnlineAccount accountDeposit=comboDepositAccount.GetSelected<QuickBooksOnlineAccount>();
				QuickBooksOnlineAccount accountIncome=comboIncomeAccount.GetSelected<QuickBooksOnlineAccount>();
				if(accountDeposit==null || accountIncome==null) {
					MsgBox.Show(this,"Pick a deposit account and an income account. They are chosen in Accounting > Setup > QuickBooks Online.");
					return;
				}
				DepositAccountSelected=accountDeposit.Name;
				DepositAccountIdSelected=accountDeposit.Id;
				IncomeAccountSelected=accountIncome.Name;
				IncomeAccountIdSelected=accountIncome.Id;
			}
			else {
				DepositAccountSelected=_listDepositAccountsQB[comboDepositAccount.SelectedIndex];
				IncomeAccountSelected=_listIncomeAccountsQB[comboIncomeAccount.SelectedIndex];
			}
			DialogResult=DialogResult.OK;
		}

	}
}