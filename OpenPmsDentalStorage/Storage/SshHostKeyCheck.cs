// Remembered SSH host keys (docs/specs/ssh-host-keys.md, section 3): the host key check of every SSH connection the program makes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.CompilerServices;
using System.Text;
using CodeBase;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;

namespace OpenPmsDentalStorage {
	///<summary>The host key check of every SSH connection the program makes (SFTP image storage, the TSI uploads, the Denti-Cal and Mercury
	///clearinghouses and the HL7 SFTP mode). SSH.NET itself verifies, during each key exchange, that the server holds the private key of the host
	///key it presents, and refuses the connection otherwise; that check cannot be switched off. On top of it, this check compares the key with the
	///one the store (ISshHostKeyStore; in the program the sshhostkey table, OpenPmsDentalBusiness.SshHostKeys.Store) holds for the server, one key
	///per host and port: the first successful connection to a server stores the key it presented (trust on first use), and every later connection
	///must present the same key, of the same type with the same SHA-256 fingerprint. Any other key is refused during the key exchange, so SSH.NET
	///never sends the user name or the password; the refused key is recorded in the store and the connection throws SshHostKeyRefusedException.
	///SSH.NET offers the same host key algorithms in the same order on every connection, so a server presents the same key type each time; a key
	///of another type is refused like a changed key, because a man in the middle could otherwise get past the check just by offering a type not
	///seen yet. One RSA key is one key under every RSA signature algorithm (GetKeyType).
	///Every SSH.NET client is attached when it is created (Attach, which needs a store) and connected through Connect, which reads the store
	///before connecting and writes it afterwards, always on the caller's thread, never on SSH.NET's.</summary>
	public class SshHostKeyCheck {
		///<summary>The check of each attached client. A client that is gone takes its check with it.</summary>
		private static readonly ConditionalWeakTable<BaseClient,SshHostKeyCheck> _tableChecks=new ConditionalWeakTable<BaseClient,SshHostKeyCheck>();
		private readonly object _lock=new object();
		private readonly ISshHostKeyStore _store;
		private readonly Action _actionConnect;
		private readonly Action _actionDisconnect;
		///<summary>The key every key exchange of the current connection must present: the stored key, or on a first use the key the first key
		///exchange presented. Null before that first key exchange of a first use.</summary>
		private SshHostKeyFingerprint _fingerprintTrusted;
		///<summary>True while the current connection is a first use (no key was stored when it began) and it has not stored its key yet.</summary>
		private bool _isFirstUse;
		///<summary>The key a key exchange of the current connection presented and the check refused. Null when none was refused.</summary>
		private SshHostKeyFingerprint _fingerprintRefused;
		///<summary>The key the refused one was compared with: _fingerprintTrusted when it was refused.</summary>
		private SshHostKeyFingerprint _fingerprintTrustedWhenRefused;
		///<summary>The server's host name, trimmed and in lower case (NormalizeHost), as the store keeps it.</summary>
		public readonly string Host;
		public readonly int Port;

		///<summary>actionConnect connects and signs in (SSH.NET raises HostKeyReceived during it, and OnHostKeyReceived must get it);
		///actionDisconnect closes the connection. Attach passes the client's Connect and Disconnect; tests pass their own.</summary>
		internal SshHostKeyCheck(string host,int port,ISshHostKeyStore store,Action actionConnect,Action actionDisconnect) {
			if(store==null) {
				throw new ArgumentNullException(nameof(store),"Every SSH connection needs a host key store.");
			}
			Host=NormalizeHost(host);
			Port=port;
			_store=store;
			_actionConnect=actionConnect;
			_actionDisconnect=actionDisconnect;
		}

		///<summary>Attaches the check to a new SSH.NET client: every host key the client receives is checked against the store's key for the
		///client's host and port. Call once, before the client connects; then connect only through Connect. Throws when store is null or the client
		///already has a check.</summary>
		public static void Attach(BaseClient baseClient,ISshHostKeyStore store) {
			SshHostKeyCheck sshHostKeyCheck=new SshHostKeyCheck(baseClient.ConnectionInfo.Host,baseClient.ConnectionInfo.Port,store,
				() => baseClient.Connect(),
				() => {
					if(baseClient.IsConnected) {
						baseClient.Disconnect();
					}
				});
			_tableChecks.Add(baseClient,sshHostKeyCheck);
			baseClient.HostKeyReceived+=(sender,hostKeyEventArgs) => sshHostKeyCheck.OnHostKeyReceived(hostKeyEventArgs);
		}

		///<summary>Connects and signs in the client through its check (see the instance Connect). Throws InvalidOperationException for a client that
		///was never attached, so no SSH connection can skip the check.</summary>
		public static void Connect(BaseClient baseClient) {
			if(!_tableChecks.TryGetValue(baseClient,out SshHostKeyCheck sshHostKeyCheck)) {
				throw new InvalidOperationException("The SSH connection to "+baseClient.ConnectionInfo.Host+":"+baseClient.ConnectionInfo.Port
					+" has no host key check.");
			}
			sshHostKeyCheck.Connect();
		}

		///<summary>True when the client was attached (Attach).</summary>
		public static bool IsAttached(BaseClient baseClient) {
			return _tableChecks.TryGetValue(baseClient,out SshHostKeyCheck sshHostKeyCheck);
		}

		///<summary>Connects and signs in. Before connecting, reads the server's stored key; if the store cannot be read, nothing is sent and
		///SshHostKeyRefusedException is thrown. A refused host key throws SshHostKeyRefusedException (after the refusal is recorded in the store),
		///any other failure what SSH.NET threw; then nothing is stored. After a successful connection without a stored key, stores the key the server
		///presented; if another connection stored a different key first, or the key cannot be stored, disconnects and throws
		///SshHostKeyRefusedException.</summary>
		public void Connect() {
			SshHostKeyFingerprint fingerprintStored;
			try {
				fingerprintStored=_store.GetStoredKey(Host,Port);
			}
			catch(Exception ex) {
				throw new SshHostKeyRefusedException(GetMessageStoreFailed(Host,Port,isReading:true,ex.Message),Host,Port,null,null,ex);
			}
			lock(_lock) {
				_fingerprintTrusted=fingerprintStored;
				_isFirstUse=fingerprintStored==null;
				_fingerprintRefused=null;
				_fingerprintTrustedWhenRefused=null;
			}
			try {
				_actionConnect();
			}
			catch(Exception ex) {
				SshHostKeyFingerprint fingerprintRefused;
				SshHostKeyFingerprint fingerprintCompared;
				lock(_lock) {
					fingerprintRefused=_fingerprintRefused;
					fingerprintCompared=_fingerprintTrustedWhenRefused;
				}
				if(fingerprintRefused==null) {
					throw;//Not refused for its host key: a wrong password, a network error, not an SSH server.
				}
				if(fingerprintStored!=null) {
					RecordRefused(fingerprintStored,fingerprintRefused);
				}
				throw new SshHostKeyRefusedException(GetMessageChanged(Host,Port,fingerprintCompared,fingerprintRefused,isBeforeCredentials:true),
					Host,Port,fingerprintCompared,fingerprintRefused,ex);
			}
			SshHostKeyFingerprint fingerprintPresented;
			lock(_lock) {
				if(!_isFirstUse) {
					return;
				}
				fingerprintPresented=_fingerprintTrusted;
			}
			if(fingerprintPresented==null) {
				//SSH.NET always checks a host key before it signs in, so this does not happen; never let a connection run on a key nobody saw.
				Disconnect();
				throw new SshHostKeyRefusedException("The SSH server "+Host+":"+Port+" was connected without presenting a host key. The connection was "
					+"closed.",Host,Port,null,null,null);
			}
			SshHostKeyFingerprint fingerprintNowStored;
			try {
				fingerprintNowStored=_store.StoreFirstKey(Host,Port,fingerprintPresented);
			}
			catch(Exception ex) {
				Disconnect();
				throw new SshHostKeyRefusedException(GetMessageStoreFailed(Host,Port,isReading:false,ex.Message),Host,Port,null,fingerprintPresented,ex);
			}
			if(fingerprintNowStored==null || !fingerprintNowStored.Matches(fingerprintPresented)) {
				//Another connection stored the server's first key at the same moment, and it is not the key this one was given.
				Disconnect();
				if(fingerprintNowStored!=null) {
					RecordRefused(fingerprintNowStored,fingerprintPresented);
				}
				throw new SshHostKeyRefusedException(GetMessageChanged(Host,Port,fingerprintNowStored,fingerprintPresented,isBeforeCredentials:false),
					Host,Port,fingerprintNowStored,fingerprintPresented,null);
			}
			lock(_lock) {
				_isFirstUse=false;
				_fingerprintTrusted=fingerprintNowStored;
			}
		}

		///<summary>The handler of SSH.NET's HostKeyReceived: sets CanTrust from CanTrust.</summary>
		internal void OnHostKeyReceived(HostKeyEventArgs hostKeyEventArgs) {
			hostKeyEventArgs.CanTrust=CanTrust(SshHostKeyFingerprint.FromHostKeyEvent(hostKeyEventArgs));
		}

		///<summary>True when the key one key exchange presented may be trusted: the stored key, or on a first use the first key the connection was
		///given (a later key exchange of the same connection, a re-key, must present it again). Anything else, a key without a fingerprint
		///included, is remembered as refused for Connect and is not trusted.</summary>
		internal bool CanTrust(SshHostKeyFingerprint fingerprintPresented) {
			lock(_lock) {
				if(fingerprintPresented!=null && !string.IsNullOrEmpty(fingerprintPresented.Sha256)) {
					if(_fingerprintTrusted==null && _isFirstUse) {
						_fingerprintTrusted=fingerprintPresented;
						return true;
					}
					if(fingerprintPresented.Matches(_fingerprintTrusted)) {
						return true;
					}
				}
				_fingerprintRefused=fingerprintPresented??new SshHostKeyFingerprint("","");
				_fingerprintTrustedWhenRefused=_fingerprintTrusted;
				return false;
			}
		}

		private void Disconnect() {
			try {
				_actionDisconnect?.Invoke();
			}
			catch(Exception ex) {
				ex.DoNothing();//The connection may already be gone.
			}
		}

		///<summary>Records the refusal in the store. A store that cannot be written does not change the refusal.</summary>
		private void RecordRefused(SshHostKeyFingerprint fingerprintStored,SshHostKeyFingerprint fingerprintRefused) {
			try {
				_store.RecordRefusedKey(Host,Port,fingerprintStored,fingerprintRefused);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>The refusal of a host key in the exception or one of its inner exceptions (ConnectIfNeeded wraps it), or null.</summary>
		public static SshHostKeyRefusedException FindRefusal(Exception ex) {
			for(Exception exception=ex;exception!=null;exception=exception.InnerException) {
				if(exception is SshHostKeyRefusedException sshHostKeyRefusedException) {
					return sshHostKeyRefusedException;
				}
			}
			return null;
		}

		///<summary>The message of the host key refusal in the exception or one of its inner exceptions, or null when it holds none. For callers that do
		///not reference SSH.NET (an SshHostKeyRefusedException is an SshConnectionException).</summary>
		public static string GetRefusalMessage(Exception ex) {
			return FindRefusal(ex)?.Message;
		}

		///<summary>The host as the store keeps it and compares it: trimmed, in lower case.</summary>
		public static string NormalizeHost(string host) {
			return (host??"").Trim().ToLowerInvariant();
		}

		///<summary>The key type a host key blob starts with (RFC 4253 6.6: a string, e.g. "ssh-ed25519", "ssh-rsa" or "ecdsa-sha2-nistp256"). The
		///negotiated algorithm name is not used, because one RSA key is offered as rsa-sha2-512, rsa-sha2-256 and ssh-rsa.</summary>
		public static string GetKeyType(byte[] arrayHostKey) {
			if(arrayHostKey==null || arrayHostKey.Length<4) {
				return "";
			}
			int length=(arrayHostKey[0]<<24)|(arrayHostKey[1]<<16)|(arrayHostKey[2]<<8)|arrayHostKey[3];
			if(length<=0 || length>64 || arrayHostKey.Length<4+length) {
				return "";
			}
			return Encoding.ASCII.GetString(arrayHostKey,4,length);
		}

		///<summary>The message of a refused key. isBeforeCredentials: refused during the key exchange (nothing was sent); otherwise the connection
		///had signed in when the check found that another connection had stored a different key first, and was closed. fingerprintStored may be
		///null (a key exchange without a fingerprint on a first use).</summary>
		public static string GetMessageChanged(string host,int port,SshHostKeyFingerprint fingerprintStored,SshHostKeyFingerprint fingerprintPresented,
			bool isBeforeCredentials)
		{
			string server=host+":"+port;
			string presented=fingerprintPresented==null || string.IsNullOrEmpty(fingerprintPresented.Sha256) ? "a key without a fingerprint"
				: fingerprintPresented.ToString();
			string message;
			if(fingerprintStored==null) {
				message="The SSH server "+server+" presented "+presented+". The connection was refused before the user name and password were sent.";
			}
			else if(isBeforeCredentials) {
				string what=fingerprintPresented!=null && fingerprintPresented.KeyType!=fingerprintStored.KeyType ? "its key type changed"
					: "its fingerprint changed";
				message="The SSH host key of "+server+" has changed: "+what+". Stored key: "+fingerprintStored+". The server now presents: "+presented
					+". The connection was refused before the user name and password were sent.";
			}
			else {
				message="Another connection stored the SSH host key of "+server+" at the same moment, and it is not the key this connection was given. "
					+"Stored key: "+fingerprintStored+". This connection was given: "+presented+". The connection was closed.";
			}
			return message+" If the server's key really changed, a user with Security Admin can compare it with the fingerprint the server's "
				+"administrator gives and accept it in Setup, SSH Host Keys. Otherwise someone else may be answering for the server.";
		}

		///<summary>The message of a connection refused because the store could not be read (before connecting) or written (after the first
		///connection to the server).</summary>
		public static string GetMessageStoreFailed(string host,int port,bool isReading,string error) {
			if(isReading) {
				return "The stored SSH host key of "+host+":"+port+" could not be read, so the connection was refused before anything was sent to the "
					+"server: "+error;
			}
			return "The SSH host key of "+host+":"+port+" could not be stored, so the connection was closed: "+error;
		}
	}

	///<summary>A host key as the program stores and compares it: the key type its key blob names (SshHostKeyCheck.GetKeyType) and its SHA-256
	///fingerprint, base64 without padding as OpenSSH prints it after "SHA256:".</summary>
	public class SshHostKeyFingerprint {
		public readonly string KeyType;
		public readonly string Sha256;

		public SshHostKeyFingerprint(string keyType,string sha256) {
			KeyType=keyType??"";
			Sha256=sha256??"";
		}

		///<summary>The key SSH.NET hands the HostKeyReceived event.</summary>
		public static SshHostKeyFingerprint FromHostKeyEvent(HostKeyEventArgs hostKeyEventArgs) {
			return new SshHostKeyFingerprint(SshHostKeyCheck.GetKeyType(hostKeyEventArgs.HostKey),hostKeyEventArgs.FingerPrintSHA256);
		}

		///<summary>The same key type and fingerprint (ordinal: fingerprints are base64, where case matters).</summary>
		public bool Matches(SshHostKeyFingerprint other) {
			return other!=null && string.Equals(KeyType,other.KeyType,StringComparison.Ordinal) && string.Equals(Sha256,other.Sha256,StringComparison.Ordinal);
		}

		///<summary>"ssh-ed25519 SHA256:...".</summary>
		public override string ToString() {
			return KeyType+" SHA256:"+Sha256;
		}
	}

	///<summary>Where SshHostKeyCheck keeps the one host key of each SSH server (host trimmed and in lower case, and port). In the program:
	///OpenPmsDentalBusiness.SshHostKeys.Store, the sshhostkey table. Every method may throw; the check then refuses the connection.</summary>
	public interface ISshHostKeyStore {
		///<summary>The key stored for the server, or null when none is stored yet.</summary>
		SshHostKeyFingerprint GetStoredKey(string host,int port);

		///<summary>Stores the key of the server's first successful connection, unless another connection stored one first, and returns the key that
		///is stored for the server afterwards (this one, or the other connection's).</summary>
		SshHostKeyFingerprint StoreFirstKey(string host,int port,SshHostKeyFingerprint fingerprint);

		///<summary>Records that a connection refused fingerprintRefused because it is not the stored key fingerprintStored.</summary>
		void RecordRefusedKey(string host,int port,SshHostKeyFingerprint fingerprintStored,SshHostKeyFingerprint fingerprintRefused);
	}

	///<summary>A connection refused for its host key (SshHostKeyCheck): the key the server presented is not the stored key, or the stored key could
	///not be read or stored. An SshConnectionException, so callers that report SSH connection errors report it the same way; its message names the
	///server and both keys.</summary>
	public class SshHostKeyRefusedException:SshConnectionException {
		public string Host { get; }
		public int Port { get; }
		///<summary>The stored key the presented one was compared with. Null when the store could not be read or stored, or on a first use.</summary>
		public SshHostKeyFingerprint FingerprintStored { get; }
		///<summary>The key the server presented. Null when the store could not be read (nothing was sent).</summary>
		public SshHostKeyFingerprint FingerprintPresented { get; }

		public SshHostKeyRefusedException(string message,string host,int port,SshHostKeyFingerprint fingerprintStored,
			SshHostKeyFingerprint fingerprintPresented,Exception exceptionInner)
			:base(message,DisconnectReason.HostKeyNotVerifiable,exceptionInner)
		{
			Host=host;
			Port=port;
			FingerprintStored=fingerprintStored;
			FingerprintPresented=fingerprintPresented;
		}
	}
}
