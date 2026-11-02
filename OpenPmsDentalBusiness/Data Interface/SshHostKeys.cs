// Remembered SSH host keys (docs/specs/ssh-host-keys.md): the stored host key of each SSH server, and the store every SSH connection checks.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenPmsDentalStorage;

namespace OpenPmsDentalBusiness {
	///<summary>The host key the program remembers for each SSH server (sshhostkey). Every SSH connection of the program checks the server's key
	///against Store (SshHostKeyCheck in OpenPmsDentalStorage): the first successful connection stores the key (InsertFirstUse), a different key is
	///refused before any credentials are sent and recorded on the row (RecordRefused), and a user with SecurityAdmin accepts the refused key
	///(AcceptRefusedKey) or removes the row (Delete) in Setup, SSH Host Keys. Every change is in the audit trail under EnumPermType.SshHostKeys
	///(FKey the SshHostKeyNum). Not cached: each connection reads its server's row, so a change counts on every workstation from its next
	///connection.</summary>
	public class SshHostKeys {
		///<summary>The host key store of every SSH connection of the program: the sshhostkey table. Pass it to Sftp's task states, Sftp.IsConnectionValid
		///and SftpSession.</summary>
		public static readonly ISshHostKeyStore Store=new SshHostKeyStoreDb();

		#region Get Methods
		///<summary>Every stored key, by host and port.</summary>
		public static List<SshHostKey> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SshHostKey>>(MethodBase.GetCurrentMethod());
			}
			return Crud.SshHostKeyCrud.SelectMany("SELECT * FROM sshhostkey ORDER BY Host,Port");
		}

		///<summary>The row of the server (the host is compared trimmed and without case), or null when no key is stored for it.</summary>
		public static SshHostKey GetForServer(string host,int port) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SshHostKey>(MethodBase.GetCurrentMethod(),host,port);
			}
			string command="SELECT * FROM sshhostkey WHERE Host='"+POut.String(SshHostKeyCheck.NormalizeHost(host))+"' AND Port="+POut.Int(port);
			return Crud.SshHostKeyCrud.SelectOne(command);
		}

		///<summary>Null if not found.</summary>
		public static SshHostKey GetOne(long sshHostKeyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SshHostKey>(MethodBase.GetCurrentMethod(),sshHostKeyNum);
			}
			return Crud.SshHostKeyCrud.SelectOne(sshHostKeyNum);
		}
		#endregion Get Methods

		#region Modification Methods
		///<summary>Stores the key a server presented on its first successful connection, with the logged-on user (0 for a service) and the database
		///server's time, and writes the audit entry. When the server already has a row (another connection stored its key first; the unique key on
		///host and port lets only one row in), changes nothing and returns that row. Returns the server's row. Needs no permission: it is part of any
		///user's connection.</summary>
		public static SshHostKey InsertFirstUse(string host,int port,string keyType,string fingerprintSha256) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SshHostKey>(MethodBase.GetCurrentMethod(),host,port,keyType,fingerprintSha256);
			}
			host=SshHostKeyCheck.NormalizeHost(host);
			if(host=="" || string.IsNullOrEmpty(fingerprintSha256)) {
				throw new ApplicationException("An SSH host key needs a host and a fingerprint.");
			}
			SshHostKey sshHostKeyExisting=GetForServer(host,port);
			if(sshHostKeyExisting!=null) {
				return sshHostKeyExisting;
			}
			SshHostKey sshHostKey=new SshHostKey();
			sshHostKey.Host=host;
			sshHostKey.Port=port;
			sshHostKey.KeyType=keyType??"";
			sshHostKey.FingerprintSha256=fingerprintSha256;
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			sshHostKey.UserNum=Security.CurUser?.UserNum??0;
			sshHostKey.DateTStored=MiscData.GetNowDateTime();
			sshHostKey.IsAccepted=false;
			sshHostKey.RefusedKeyType="";
			sshHostKey.RefusedFingerprintSha256="";
			sshHostKey.DateTRefused=DateTime.MinValue;
			try {
				Crud.SshHostKeyCrud.Insert(sshHostKey);
			}
			catch(Exception) {
				//The unique key HostPort refused the row: another connection stored the server's key since the read above.
				SshHostKey sshHostKeyOther=GetForServer(host,port);
				if(sshHostKeyOther==null) {
					throw;
				}
				return sshHostKeyOther;
			}
			MakeLogEntry(GetLogTextStored(sshHostKey),sshHostKey.SshHostKeyNum);
			return sshHostKey;
		}

		///<summary>Records that a connection refused the key a server presented because it is not the stored key: the refused key and the database
		///server's time go on the row, where Setup, SSH Host Keys shows them. The audit entry is written only when this key is not already the
		///recorded refused key, so a service that retries every minute writes one entry per refused key. Does nothing when the server has no row or
		///the key is its stored key (an administrator accepted it meanwhile). Needs no permission: it is part of any user's connection.</summary>
		public static void RecordRefused(string host,int port,string keyType,string fingerprintSha256) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),host,port,keyType,fingerprintSha256);
				return;
			}
			keyType=keyType??"";
			fingerprintSha256=fingerprintSha256??"";
			SshHostKey sshHostKeyOld=GetForServer(host,port);
			if(sshHostKeyOld==null || IsSameKey(sshHostKeyOld.KeyType,sshHostKeyOld.FingerprintSha256,keyType,fingerprintSha256)) {
				return;
			}
			bool isNewRefusal=!IsSameKey(sshHostKeyOld.RefusedKeyType,sshHostKeyOld.RefusedFingerprintSha256,keyType,fingerprintSha256);
			SshHostKey sshHostKey=sshHostKeyOld.Copy();
			sshHostKey.RefusedKeyType=keyType;
			sshHostKey.RefusedFingerprintSha256=fingerprintSha256;
			sshHostKey.DateTRefused=MiscData.GetNowDateTime();
			Crud.SshHostKeyCrud.Update(sshHostKey,sshHostKeyOld);
			if(isNewRefusal) {
				MakeLogEntry(GetLogTextRefused(sshHostKey),sshHostKey.SshHostKeyNum);
			}
		}

		///<summary>Accepts the key a connection refused for the server: it replaces the stored key, as accepted by the logged-on administrator
		///(IsAccepted, UserNum, DateTStored the database server's time), the refused columns are cleared, and the audit entry names the old and the
		///new key. refusedKeyType and refusedFingerprintSha256 are the refused key the SSH Host Keys window showed: only that key is accepted. Throws
		///an ApplicationException, changing nothing, when the user does not have SecurityAdmin (checked here, so the rule holds through the Middle
		///Tier), the row is gone, it has no refused key, or its refused key is no longer the one shown.</summary>
		public static void AcceptRefusedKey(long sshHostKeyNum,string refusedKeyType,string refusedFingerprintSha256) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),sshHostKeyNum,refusedKeyType,refusedFingerprintSha256);
				return;
			}
			CheckSecurityAdmin();
			SshHostKey sshHostKeyOld=GetOne(sshHostKeyNum);
			if(sshHostKeyOld==null) {
				throw new ApplicationException(Lans.g("SshHostKeys","This SSH host key was not found. It may have been removed."));
			}
			if(!sshHostKeyOld.HasRefusedKey()) {
				throw new ApplicationException(Lans.g("SshHostKeys","No changed key of this server was refused, so there is no key to accept."));
			}
			if(!IsSameKey(sshHostKeyOld.RefusedKeyType,sshHostKeyOld.RefusedFingerprintSha256,refusedKeyType??"",refusedFingerprintSha256??"")) {
				throw new ApplicationException(Lans.g("SshHostKeys","A connection refused another key of this server since the list was filled. "
					+"Look at the keys again before you accept one."));
			}
			SshHostKey sshHostKey=sshHostKeyOld.Copy();
			sshHostKey.KeyType=sshHostKeyOld.RefusedKeyType;
			sshHostKey.FingerprintSha256=sshHostKeyOld.RefusedFingerprintSha256;
			sshHostKey.IsAccepted=true;
			sshHostKey.UserNum=Security.CurUser.UserNum;
			sshHostKey.DateTStored=MiscData.GetNowDateTime();
			sshHostKey.RefusedKeyType="";
			sshHostKey.RefusedFingerprintSha256="";
			sshHostKey.DateTRefused=DateTime.MinValue;
			Crud.SshHostKeyCrud.Update(sshHostKey,sshHostKeyOld);
			MakeLogEntry(GetLogTextAccepted(sshHostKeyOld,sshHostKey),sshHostKey.SshHostKeyNum);
		}

		///<summary>Removes the stored key of a server, so the next connection to it stores the key it presents then, and writes the audit entry.
		///Throws an ApplicationException, changing nothing, when the user does not have SecurityAdmin (checked here, so the rule holds through the
		///Middle Tier) or the row is gone.</summary>
		public static void Delete(long sshHostKeyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),sshHostKeyNum);
				return;
			}
			CheckSecurityAdmin();
			SshHostKey sshHostKey=GetOne(sshHostKeyNum);
			if(sshHostKey==null) {
				throw new ApplicationException(Lans.g("SshHostKeys","This SSH host key was not found. It may have been removed."));
			}
			Crud.SshHostKeyCrud.Delete(sshHostKeyNum);
			MakeLogEntry(GetLogTextRemoved(sshHostKey),sshHostKeyNum);
		}
		#endregion Modification Methods

		#region Misc Methods
		///<summary>"host:port".</summary>
		public static string GetServerText(SshHostKey sshHostKey) {
			Meth.NoCheckMiddleTierRole();
			return sshHostKey.Host+":"+sshHostKey.Port;
		}

		///<summary>The stored key: "ssh-ed25519 SHA256:...".</summary>
		public static string GetStoredKeyText(SshHostKey sshHostKey) {
			Meth.NoCheckMiddleTierRole();
			return new SshHostKeyFingerprint(sshHostKey.KeyType,sshHostKey.FingerprintSha256).ToString();
		}

		///<summary>The refused key: "ssh-ed25519 SHA256:...", or "" when none.</summary>
		public static string GetRefusedKeyText(SshHostKey sshHostKey) {
			Meth.NoCheckMiddleTierRole();
			if(!sshHostKey.HasRefusedKey()) {
				return "";
			}
			return new SshHostKeyFingerprint(sshHostKey.RefusedKeyType,sshHostKey.RefusedFingerprintSha256).ToString();
		}

		public static string GetLogTextStored(SshHostKey sshHostKey) {
			Meth.NoCheckMiddleTierRole();
			return "SSH host key stored on first use: "+GetServerText(sshHostKey)+", "+GetStoredKeyText(sshHostKey);
		}

		public static string GetLogTextRefused(SshHostKey sshHostKey) {
			Meth.NoCheckMiddleTierRole();
			return "SSH host key refused: "+GetServerText(sshHostKey)+" presented "+GetRefusedKeyText(sshHostKey)+", the stored key is "
				+GetStoredKeyText(sshHostKey);
		}

		public static string GetLogTextAccepted(SshHostKey sshHostKeyOld,SshHostKey sshHostKeyNew) {
			Meth.NoCheckMiddleTierRole();
			return "SSH host key accepted: "+GetServerText(sshHostKeyNew)+", "+GetStoredKeyText(sshHostKeyOld)+" (old) replaced by "
				+GetStoredKeyText(sshHostKeyNew)+" (new)";
		}

		public static string GetLogTextRemoved(SshHostKey sshHostKey) {
			Meth.NoCheckMiddleTierRole();
			return "SSH host key removed: "+GetServerText(sshHostKey)+", "+GetStoredKeyText(sshHostKey)
				+". The next connection stores the key the server presents.";
		}

		///<summary>Throws an ApplicationException when the logged-on user does not have SecurityAdmin.</summary>
		private static void CheckSecurityAdmin() {
			//Security.CurUser gets set on MT by the DtoProcessor so it matches the user from the client WS.
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin,true)) {
				throw new ApplicationException(Lans.g("SshHostKeys","Only a user with Security Admin can accept or remove SSH host keys."));
			}
		}

		///<summary>Ordinal: fingerprints are base64, where case matters.</summary>
		private static bool IsSameKey(string keyTypeA,string fingerprintA,string keyTypeB,string fingerprintB) {
			return string.Equals(keyTypeA,keyTypeB,StringComparison.Ordinal) && string.Equals(fingerprintA,fingerprintB,StringComparison.Ordinal);
		}

		///<summary>An audit entry under SshHostKeys. A process without a logged-on user writes none (every program that connects has one: the HL7
		///service logs on as its service user).</summary>
		private static void MakeLogEntry(string logText,long sshHostKeyNum) {
			if(Security.CurUser==null) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.SshHostKeys,0,logText,sshHostKeyNum,DateTime.MinValue);
		}
		#endregion Misc Methods

		///<summary>SshHostKeyCheck's store in the program: the sshhostkey table, through the methods above (and so through the Middle Tier).</summary>
		private class SshHostKeyStoreDb:ISshHostKeyStore {
			public SshHostKeyFingerprint GetStoredKey(string host,int port) {
				SshHostKey sshHostKey=GetForServer(host,port);
				if(sshHostKey==null) {
					return null;
				}
				return new SshHostKeyFingerprint(sshHostKey.KeyType,sshHostKey.FingerprintSha256);
			}

			public SshHostKeyFingerprint StoreFirstKey(string host,int port,SshHostKeyFingerprint fingerprint) {
				SshHostKey sshHostKey=InsertFirstUse(host,port,fingerprint.KeyType,fingerprint.Sha256);
				return new SshHostKeyFingerprint(sshHostKey.KeyType,sshHostKey.FingerprintSha256);
			}

			public void RecordRefusedKey(string host,int port,SshHostKeyFingerprint fingerprintStored,SshHostKeyFingerprint fingerprintRefused) {
				RecordRefused(host,port,fingerprintRefused.KeyType,fingerprintRefused.Sha256);
			}
		}
	}
}
