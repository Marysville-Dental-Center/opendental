// Open Dental 26.2 (P262-GLIDEWELL): the office's Glidewell billing accounts (glidewellacct), their API keys and the providers who send to each
// account by default (provider.GlidewellAcctNum).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;
using OpenPmsDentalBusiness.VendorDirect;

namespace OpenPmsDentalBusiness {
	///<summary>The office's Glidewell accounts. Each holds the account number Glidewell bills and the API key Glidewell issued for it. The key is
	///stored encrypted with this program's own format (osd1$, SecretValue). A key another program stored (Open Dental encrypts it its own way) cannot
	///be read here and has to be entered again; it is never sent. Nothing here calls Glidewell.</summary>
	public class GlidewellAccts {
		#region Get Methods
		///<summary>Every account, by account number.</summary>
		public static List<GlidewellAcct> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<GlidewellAcct>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM glidewellacct ORDER BY AccountNumber,GlidewellAcctNum";
			return Crud.GlidewellAcctCrud.SelectMany(command);
		}

		///<summary>Null if there is no such account.</summary>
		public static GlidewellAcct GetOne(long glidewellAcctNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<GlidewellAcct>(MethodBase.GetCurrentMethod(),glidewellAcctNum);
			}
			if(glidewellAcctNum==0) {
				return null;
			}
			return Crud.GlidewellAcctCrud.SelectOne(glidewellAcctNum);
		}

		///<summary>The providers whose default Glidewell account this is (provider.GlidewellAcctNum), read from the database.</summary>
		public static List<long> GetProvNumsForAcct(long glidewellAcctNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),glidewellAcctNum);
			}
			if(glidewellAcctNum==0) {
				return new List<long>();
			}
			string command="SELECT ProvNum FROM provider WHERE GlidewellAcctNum="+POut.Long(glidewellAcctNum)+" ORDER BY ProvNum";
			return Db.GetListLong(command);
		}

		///<summary>The account a lab case of this provider goes to unless the user picks another: the provider's default account when it still exists,
		///otherwise the only account when the office has exactly one, otherwise 0 (the user picks). From the provider cache and the list given.</summary>
		public static long GetDefaultAcctNum(long provNum,List<GlidewellAcct> listGlidewellAccts) {
			Meth.NoCheckMiddleTierRole();
			if(listGlidewellAccts==null || listGlidewellAccts.Count==0) {
				return 0;
			}
			Provider provider=Providers.GetProv(provNum);
			if(provider!=null && provider.GlidewellAcctNum!=0 && listGlidewellAccts.Any(x => x.GlidewellAcctNum==provider.GlidewellAcctNum)) {
				return provider.GlidewellAcctNum;
			}
			if(listGlidewellAccts.Count==1) {
				return listGlidewellAccts[0].GlidewellAcctNum;
			}
			return 0;
		}
		#endregion Get Methods

		#region Modification Methods
		public static long Insert(GlidewellAcct glidewellAcct) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				glidewellAcct.GlidewellAcctNum=Meth.GetLong(MethodBase.GetCurrentMethod(),glidewellAcct);
				return glidewellAcct.GlidewellAcctNum;
			}
			return Crud.GlidewellAcctCrud.Insert(glidewellAcct);
		}

		public static void Update(GlidewellAcct glidewellAcct) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),glidewellAcct);
				return;
			}
			Crud.GlidewellAcctCrud.Update(glidewellAcct);
		}

		///<summary>Deletes the account and clears it as the default of every provider who had it. Throws ApplicationException, deleting nothing, when a
		///lab case was sent with the account (glidewellmsg keeps which account each case went to). The caller signals InvalidType.Providers.</summary>
		public static void Delete(long glidewellAcctNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),glidewellAcctNum);
				return;
			}
			string command="SELECT COUNT(*) FROM glidewellmsg WHERE GlidewellAcctNum="+POut.Long(glidewellAcctNum);
			if(PIn.Long(Db.GetCount(command))>0) {
				throw new ApplicationException(Lans.g("GlidewellAccts","Lab cases were sent to Glidewell with this account, so it cannot be deleted."));
			}
			command="UPDATE provider SET GlidewellAcctNum=0 WHERE GlidewellAcctNum="+POut.Long(glidewellAcctNum);
			Db.NonQ(command);
			Crud.GlidewellAcctCrud.Delete(glidewellAcctNum);
		}

		///<summary>Makes the account the default of exactly the given providers: providers not in the list that had it get 0, the providers in the list
		///get it (a provider has one default, so this replaces another account they had). Writes only rows that change. The caller signals
		///InvalidType.Providers.</summary>
		public static void SetProvidersForAcct(long glidewellAcctNum,List<long> listProvNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),glidewellAcctNum,listProvNums);
				return;
			}
			if(glidewellAcctNum==0) {
				return;
			}
			List<long> listProvNumsDistinct=(listProvNums??new List<long>()).Where(x => x>0).Distinct().ToList();
			string command="UPDATE provider SET GlidewellAcctNum=0 WHERE GlidewellAcctNum="+POut.Long(glidewellAcctNum);
			if(listProvNumsDistinct.Count>0) {
				command+=" AND ProvNum NOT IN ("+string.Join(",",listProvNumsDistinct.Select(x => POut.Long(x)))+")";
			}
			Db.NonQ(command);
			if(listProvNumsDistinct.Count==0) {
				return;
			}
			command="UPDATE provider SET GlidewellAcctNum="+POut.Long(glidewellAcctNum)+" "
				+"WHERE ProvNum IN ("+string.Join(",",listProvNumsDistinct.Select(x => POut.Long(x)))+") "
				+"AND GlidewellAcctNum!="+POut.Long(glidewellAcctNum);
			Db.NonQ(command);
		}

		///<summary>Saves the account from its setup window. A key that is not blank is encrypted into the account (blank keeps the stored key); then
		///the account number is set, the row inserted or updated, and the security log written: Setup when the account is added,
		///ManageHighSecurityProgProperties when a key was entered. When the key is refused (too long to store, or encryption failed) nothing is changed,
		///saved or logged, and the result says why. The caller checks the permission to change the key, the account number and its uniqueness, then
		///sets the providers and signals InvalidType.Providers.</summary>
		public static GlidewellApiKeyResult SaveAccount(GlidewellAcct glidewellAcct,string accountNumber,string apiKeyEntered) {
			Meth.NoCheckMiddleTierRole();
			string apiKey=(apiKeyEntered??"").Trim();
			bool isApiKeyChanged=false;
			if(apiKey!="") {
				GlidewellApiKeyResult glidewellApiKeyResult=SetApiKey(glidewellAcct,apiKey);
				if(glidewellApiKeyResult!=GlidewellApiKeyResult.Ok) {
					return glidewellApiKeyResult;
				}
				isApiKeyChanged=true;
			}
			glidewellAcct.AccountNumber=accountNumber;
			if(glidewellAcct.GlidewellAcctNum==0) {
				Insert(glidewellAcct);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Glidewell account "+accountNumber+" added.");
			}
			else {
				Update(glidewellAcct);
			}
			if(isApiKeyChanged) {
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,"The API key of Glidewell account "+accountNumber+" was altered.",
					glidewellAcct.GlidewellAcctNum,DateTime.MinValue);
			}
			return GlidewellApiKeyResult.Ok;
		}
		#endregion Modification Methods

		#region API key
		///<summary>The width of glidewellacct.ApiKey, varchar(255) in Open Dental's 26.2 database documents. The program runs MySQL with a blank
		///sql_mode, so the database would cut a longer value short without an error; SetApiKey refuses it instead.</summary>
		public const int API_KEY_STORED_MAX_LENGTH=255;
		///<summary>The longest key whose stored value fits, in UTF-8 bytes (characters, for a key of ASCII characters). The stored value is "osd1$"
		///and base64 of IV (16) + AES-CBC ciphertext (whole 16-byte blocks, always one more than the key fills) + HMAC (32): 241 characters for a
		///127-byte key, 261 for a 128-byte key.</summary>
		public const int API_KEY_MAX_LENGTH=127;

		///<summary>Encrypts the key into glidewellAcct.ApiKey (not saved; the caller saves the account). Blank clears it. Anything but Ok changes
		///nothing and the caller must refuse to save: TooLong when the stored value would be longer than glidewellacct.ApiKey holds (a key longer than
		///API_KEY_MAX_LENGTH bytes), EncryptionFailed when encryption failed.</summary>
		public static GlidewellApiKeyResult SetApiKey(GlidewellAcct glidewellAcct,string apiKeyPlainText) {
			Meth.NoCheckMiddleTierRole();
			string stored;
			if(!SecretValue.Protect((apiKeyPlainText??"").Trim(),out stored)) {
				return GlidewellApiKeyResult.EncryptionFailed;
			}
			if(stored.Length>API_KEY_STORED_MAX_LENGTH) {
				return GlidewellApiKeyResult.TooLong;
			}
			glidewellAcct.ApiKey=stored;
			return GlidewellApiKeyResult.Ok;
		}

		///<summary>Reads the stored key. A value without this program's prefix (Open Dental's ciphertext, or plain text) is never used: it reads as
		///needing re-entry (LegacyPolicy.RejectPlaintext). Never throws.</summary>
		public static SecretReadResult ReadApiKey(GlidewellAcct glidewellAcct) {
			Meth.NoCheckMiddleTierRole();
			return SecretValue.Read(glidewellAcct?.ApiKey,LegacyPolicy.RejectPlaintext);
		}

		///<summary>True when the account has an account number and a key that can be read: the office's own credentials are entered.</summary>
		public static bool IsComplete(GlidewellAcct glidewellAcct) {
			Meth.NoCheckMiddleTierRole();
			if(glidewellAcct==null || string.IsNullOrWhiteSpace(glidewellAcct.AccountNumber)) {
				return false;
			}
			return ReadApiKey(glidewellAcct).HasValue;
		}

		///<summary>What the setup lists show for the key: "Entered", "Not entered" or "Enter again" (a key that cannot be read).</summary>
		public static string GetApiKeyStatusText(GlidewellAcct glidewellAcct) {
			Meth.NoCheckMiddleTierRole();
			SecretReadResult secretReadResult=ReadApiKey(glidewellAcct);
			if(secretReadResult.HasValue) {
				return Lans.g("GlidewellAccts","Entered");
			}
			if(secretReadResult.Status==SecretReadStatus.Blank) {
				return Lans.g("GlidewellAccts","Not entered");
			}
			return Lans.g("GlidewellAccts","Enter again");
		}
		#endregion API key
	}

	///<summary>The outcome of GlidewellAccts.SetApiKey and GlidewellAccts.SaveAccount. Anything but Ok changed nothing. This program's own type, not
	///stored.</summary>
	public enum GlidewellApiKeyResult {
		///<summary>The key was encrypted into the account (and, for SaveAccount, the account was saved).</summary>
		Ok,
		///<summary>The encrypted key would be longer than glidewellacct.ApiKey holds: the key is longer than GlidewellAccts.API_KEY_MAX_LENGTH
		///bytes.</summary>
		TooLong,
		///<summary>Encryption failed.</summary>
		EncryptionFailed,
	}
}
