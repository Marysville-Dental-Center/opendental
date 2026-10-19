// QuickBooks Online (direct): an account picked for deposits, and the "Name,Id|Name,Id" format the program properties use. Replaces the type of
// the same name in the prebuilt Bridges.dll, re-implemented from how this program calls it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness.Qbo {
	///<summary>A QuickBooks Online account (bank or income). Name is what is shown and stored: the fully qualified name ("Parent:Child") for
	///accounts loaded from QuickBooks Online, so two sub-accounts with the same short name never collide. Equality is by reference, as the setup
	///window expects.</summary>
	public class QuickBooksOnlineAccount {
		///<summary>The name shown and stored. Names containing ',' or '|' can't be stored.</summary>
		public string Name { get; set; }
		///<summary>The QuickBooks Online Id.</summary>
		public string Id { get; set; }
		///<summary>"Parent:Child" from QuickBooks Online, or the stored name for accounts read from the program property.</summary>
		public string FullyQualifiedName { get; set; }
		///<summary>Set on the single element of a list returned for a failed request.</summary>
		public Exception RequestException { get; internal set; }

		public QuickBooksOnlineAccount(string name,string id) {
			Name=name??"";
			Id=id??"";
			FullyQualifiedName=Name;
		}

		///<summary>Parses "Name,Id|Name,Id". A blank string gives an empty list. A pair is split at its last ',' (the Id has none).</summary>
		public static List<QuickBooksOnlineAccount> StringAccountsToList(string accounts) {
			return QboEntityList.Parse(accounts).Select(x => new QuickBooksOnlineAccount(x.Key,x.Value)).ToList();
		}

		///<summary>Formats the list as "Name,Id|Name,Id".</summary>
		public static string ListAccountsToString(List<QuickBooksOnlineAccount> listAccounts) {
			if(listAccounts==null) {
				return "";
			}
			return QboEntityList.Format(listAccounts.Select(x => new KeyValuePair<string,string>(x.Name,x.Id)));
		}

		internal static List<QuickBooksOnlineAccount> FromException(Exception ex) {
			QuickBooksOnlineAccount account=new QuickBooksOnlineAccount("","");
			account.RequestException=ex;
			return new List<QuickBooksOnlineAccount> { account };
		}

		public override string ToString() {
			return Name;
		}
	}

	///<summary>The "Name,Id|Name,Id" format of the Deposit Accounts, Income Accounts and Class Refs program properties (unchanged from Open Dental).</summary>
	public static class QboEntityList {
		///<summary>Name/Id pairs in stored order. Blank entries are skipped. An entry without ',' becomes a name with a blank Id.</summary>
		public static List<KeyValuePair<string,string>> Parse(string value) {
			List<KeyValuePair<string,string>> listPairs=new List<KeyValuePair<string,string>>();
			if(string.IsNullOrEmpty(value)) {
				return listPairs;
			}
			string[] arrayEntries=value.Split('|');
			for(int i=0;i<arrayEntries.Length;i++) {
				if(arrayEntries[i]=="") {
					continue;
				}
				int indexComma=arrayEntries[i].LastIndexOf(',');
				if(indexComma<0) {
					listPairs.Add(new KeyValuePair<string,string>(arrayEntries[i],""));
					continue;
				}
				listPairs.Add(new KeyValuePair<string,string>(arrayEntries[i].Substring(0,indexComma),arrayEntries[i].Substring(indexComma+1)));
			}
			return listPairs;
		}

		///<summary>Formats pairs as "Name,Id|Name,Id".</summary>
		public static string Format(IEnumerable<KeyValuePair<string,string>> listPairs) {
			return string.Join("|",listPairs.Select(x => (x.Key??"")+","+(x.Value??"")));
		}

		///<summary>True if name can be stored: it has no ',' or '|'.</summary>
		public static bool IsStorableName(string name) {
			return name!=null && name.IndexOfAny(new[] { ',','|' })<0;
		}
	}
}
