using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>The directory calls that domain (Active Directory) login needs. The real one talks to the domain named by the DomainLoginPath preference.
	///Tests replace it with a fake through DomainLogin.Directory.</summary>
	public interface IDomainDirectory {
		///<summary>True when the domain accepts this account name and password. Must check against the domain, never against the local machine.</summary>
		bool ValidateCredentials(string ldapPath,string accountName,string password);
		///<summary>The ObjectGuid of the directory entry at ldapPath, as text. Open Dental keys userod.DomainUser by it.</summary>
		string GetDomainObjectGuid(string ldapPath);
	}

	///<summary>The three preferences that domain login reads.</summary>
	public class DomainLoginSettings {
		public bool IsEnabled;
		///<summary>The DomainLoginPath preference, for example "LDAP://DC=example,DC=local".</summary>
		public string LdapPath="";
		///<summary>The DomainObjectGuid preference. Blank until the first domain login records it.</summary>
		public string DomainObjectGuid="";

		///<summary>Domain login is turned on and has a path.</summary>
		public bool IsUsable {
			get { return IsEnabled && !string.IsNullOrWhiteSpace(LdapPath); }
		}

		public static DomainLoginSettings FromPrefs() {
			return new DomainLoginSettings {
				IsEnabled=PrefC.GetBool(PrefName.DomainLoginEnabled),
				LdapPath=PrefC.GetString(PrefName.DomainLoginPath),
				DomainObjectGuid=PrefC.GetString(PrefName.DomainObjectGuid),
			};
		}
	}

	public enum DomainSwitchStatus {
		///<summary>The domain accepted the credentials and at least one Open Dental user is linked to the account. See ListUserods.</summary>
		Success,
		///<summary>Domain login is off, or has no path.</summary>
		NotEnabled,
		///<summary>Blank name or password, or the domain refused them.</summary>
		InvalidCredentials,
		///<summary>The domain accepted the credentials, but no non-hidden Open Dental user is linked to that domain account.</summary>
		NoLinkedUser,
		///<summary>Every Open Dental user linked to the account is locked by failed log on attempts.</summary>
		LockedOut,
		///<summary>The domain could not be reached or the path is not a domain path.</summary>
		DirectoryError,
	}

	///<summary>What a change-user prompt gets back from a domain log on.</summary>
	public class DomainSwitchResult {
		public DomainSwitchStatus Status;
		///<summary>The Open Dental users the domain account maps to, without hidden or locked users. More than one means the caller shows a pick list.</summary>
		public List<Userod> ListUserods=new List<Userod>();
		///<summary>The userod.DomainUser value that was looked up: "{DomainObjectGuid}\{account}".</summary>
		public string DomainUser="";
		///<summary>Set when the DomainObjectGuid preference was blank and the guid was read from the directory. The caller saves it, as startup does.</summary>
		public string DomainObjectGuidDiscovered="";
		///<summary>Text to show the user when Status is not Success.</summary>
		public string Message="";

		public bool IsSuccess {
			get { return Status==DomainSwitchStatus.Success; }
		}
	}

	///<summary>Domain (Active Directory) log on, shared by program start and the change-user prompts (procedure notes, group notes, sheet and
	///treatment plan signature boxes, ortho chart). The change-user prompts accept a domain account name and password, check them against the
	///domain at DomainLoginPath, and map the account to Open Dental users through userod.DomainUser. The password is never stored or logged.
	///Failed attempts count toward the same lockout as Open Dental passwords (5 failures within 5 minutes).</summary>
	public static class DomainLogin {
		///<summary>Replaced by tests. Null means the real Active Directory.</summary>
		public static IDomainDirectory Directory=null;

		private static IDomainDirectory GetDirectory(IDomainDirectory directory) {
			return directory??Directory??new ActiveDirectoryDomainDirectory();
		}

		///<summary>True when domain login is on and has a path. The log on window shows its domain option only then.</summary>
		public static bool IsEnabled() {
			return DomainLoginSettings.FromPrefs().IsUsable;
		}

		///<summary>Reduces what the user typed to the bare account name: "EXAMPLE\jsmith" and "jsmith@example.local" both become "jsmith".</summary>
		public static string NormalizeAccountName(string typed) {
			string account=(typed??"").Trim();
			int idxSlash=account.LastIndexOf('\\');
			if(idxSlash>=0) {
				account=account.Substring(idxSlash+1);
			}
			int idxAt=account.IndexOf('@');
			if(idxAt>=0) {
				account=account.Substring(0,idxAt);
			}
			return account.Trim();
		}

		///<summary>The userod.DomainUser value for an account: "{DomainObjectGuid}\{account}", the format FormUserEdit stores and startup matches.</summary>
		public static string BuildDomainUser(string domainObjectGuid,string accountName) {
			return (domainObjectGuid??"")+"\\"+(accountName??"");
		}

		///<summary>Splits a DomainLoginPath into the arguments of a domain PrincipalContext. Accepts "LDAP://DC=a,DC=b", "LDAP://server/DC=a,DC=b"
		///and "LDAP://a.b". The name is the server when one is given, otherwise the DC components joined with dots. Returns false when the path does not
		///name a domain, so the check can never fall back to the local machine.</summary>
		public static bool TryGetPrincipalContextArgs(string ldapPath,out string name,out string container) {
			name="";
			container="";
			string rest=(ldapPath??"").Trim();
			if(rest.StartsWith("LDAP://",StringComparison.OrdinalIgnoreCase)) {
				rest=rest.Substring("LDAP://".Length);
			}
			else if(rest.Contains("://")) {
				return false;//GC:// or other providers are not domain paths for this purpose.
			}
			if(rest=="") {
				return false;
			}
			string server="";
			int idxSlash=rest.IndexOf('/');
			if(idxSlash>=0) {
				server=rest.Substring(0,idxSlash).Trim();
				container=rest.Substring(idxSlash+1).Trim();
			}
			else if(rest.Contains("=")) {
				container=rest;
			}
			else {
				server=rest;
			}
			if(server=="" && container!="") {
				List<string> listDcs=container.Split(',')
					.Select(x => x.Trim())
					.Where(x => x.StartsWith("DC=",StringComparison.OrdinalIgnoreCase))
					.Select(x => x.Substring(3).Trim())
					.Where(x => x!="")
					.ToList();
				server=string.Join(".",listDcs);
			}
			name=server;
			return name!="";
		}

		///<summary>Checks a domain account for a change-user prompt. Does not change Security.CurUser. Hidden users are never returned. Records the attempt
		///on every linked user's lockout counters, as a failed or good log on. settings and directory default to the preferences and the real domain.</summary>
		public static DomainSwitchResult AuthenticateForSwitch(string typedName,string password,DomainLoginSettings settings=null,IDomainDirectory directory=null) {
			settings=settings??DomainLoginSettings.FromPrefs();
			DomainSwitchResult result=new DomainSwitchResult();
			if(!settings.IsUsable) {
				result.Status=DomainSwitchStatus.NotEnabled;
				result.Message=Lans.g("DomainLogin","Domain login is not turned on.");
				return result;
			}
			string account=NormalizeAccountName(typedName);
			//A blank password can be an anonymous bind, which some domains accept. Never treat that as a log on.
			if(account=="" || string.IsNullOrEmpty(password)) {
				result.Status=DomainSwitchStatus.InvalidCredentials;
				result.Message=Lans.g("DomainLogin","Enter a domain user name and password.");
				return result;
			}
			IDomainDirectory domainDirectory=GetDirectory(directory);
			string domainGuid=settings.DomainObjectGuid;
			if(string.IsNullOrWhiteSpace(domainGuid)) {
				try {
					domainGuid=domainDirectory.GetDomainObjectGuid(settings.LdapPath);
				}
				catch(Exception ex) {
					result.Status=DomainSwitchStatus.DirectoryError;
					result.Message=Lans.g("DomainLogin","The domain could not be reached:")+" "+ex.Message;
					return result;
				}
				result.DomainObjectGuidDiscovered=domainGuid??"";
			}
			result.DomainUser=BuildDomainUser(domainGuid,account);
			List<Userod> listUserodsLinked=Userods.GetUsersByDomainUserNameNoCache(result.DomainUser).Keys
				.Select(x => Userods.GetUserNoCache(x))
				.Where(x => x!=null && !x.IsHidden)
				.ToList();
			DateTime dateTimeNow=MiscData.GetNowDateTime();
			List<Userod> listUserodsOpen=listUserodsLinked.FindAll(x => !Userods.IsLockedOut(x,dateTimeNow));
			if(listUserodsLinked.Count>0 && listUserodsOpen.Count==0) {
				result.Status=DomainSwitchStatus.LockedOut;
				result.Message=Lans.g("DomainLogin","Account has been locked due to failed log in attempts."
					+"\r\nCall your security admin to unlock your account or wait at least 5 minutes.");
				return result;
			}
			bool isValid;
			try {
				isValid=domainDirectory.ValidateCredentials(settings.LdapPath,account,password);
			}
			catch(Exception ex) {
				result.Status=DomainSwitchStatus.DirectoryError;
				result.Message=Lans.g("DomainLogin","The domain could not be reached:")+" "+ex.Message;
				return result;
			}
			for(int i=0;i<listUserodsOpen.Count;i++) {
				Userods.RecordLogOnAttempt(listUserodsOpen[i].UserNum,isValid);
			}
			if(!isValid) {
				result.Status=DomainSwitchStatus.InvalidCredentials;
				result.Message=Lans.g("DomainLogin","Invalid domain user name or password.");
				return result;
			}
			if(listUserodsOpen.Count==0) {
				result.Status=DomainSwitchStatus.NoLinkedUser;
				result.Message=Lans.g("DomainLogin","No Open Dental user is linked to that domain account.");
				return result;
			}
			//Re-read so the returned users carry the lockout columns just written.
			result.ListUserods=listUserodsOpen.Select(x => Userods.GetUserNoCache(x.UserNum)??x).OrderBy(x => x.UserName).ToList();
			result.Status=DomainSwitchStatus.Success;
			return result;
		}

		///<summary>Security log text for a change of signing user. Says whether the domain checked the credentials. Never contains a password.</summary>
		public static string GetSwitchLogText(string place,Userod userodFrom,Userod userodTo,bool isDomainAuthenticated) {
			string text=place+": "+Lans.g("DomainLogin","signing user changed from")+" "+(userodFrom?.UserName??"")
				+" "+Lans.g("DomainLogin","to")+" "+(userodTo?.UserName??"");
			if(isDomainAuthenticated) {
				text+=" "+Lans.g("DomainLogin","(domain authentication)");
			}
			return text;
		}
	}

	///<summary>Domain checks through System.DirectoryServices. Used outside of tests.</summary>
	public class ActiveDirectoryDomainDirectory:IDomainDirectory {
		public bool ValidateCredentials(string ldapPath,string accountName,string password) {
			if(!DomainLogin.TryGetPrincipalContextArgs(ldapPath,out string name,out string container)) {
				throw new ApplicationException(Lans.g("DomainLogin","The domain login path does not name a domain:")+" "+ldapPath);
			}
			//ContextType.Domain always: the credentials are checked by the domain, never by this computer's local accounts.
			using PrincipalContext principalContext=new PrincipalContext(ContextType.Domain,name,string.IsNullOrEmpty(container) ? null : container);
			return principalContext.ValidateCredentials(accountName,password);
		}

		public string GetDomainObjectGuid(string ldapPath) {
			using DirectoryEntry directoryEntry=new DirectoryEntry(ldapPath);
			return directoryEntry.Guid.ToString();
		}
	}
}
