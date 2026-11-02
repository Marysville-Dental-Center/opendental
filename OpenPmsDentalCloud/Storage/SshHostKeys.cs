using System;
using System.Collections.Concurrent;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;

namespace OpenPmsDentalCloud {
	///<summary>The host key check of every SSH connection the program makes (SFTP image storage, the TSI uploads, the Denti-Cal and Mercury
	///clearinghouses and the HL7 SFTP mode). SSH.NET itself verifies, during each key exchange, that the server holds the private key of the host
	///key it presents, and refuses the connection otherwise; that check cannot be switched off. On top of it, this class remembers the first key a
	///server presents, per host, port and key type, for the life of the process, and refuses a different key of the same type from the same
	///server: a server's key must not change while the program runs (a changed key means someone else answers). A restart trusts the server's key
	///again, as SharpSSH (StrictHostKeyChecking=no) and SSH.NET's default (no check) did before. Nothing is written to the database or disk.
	///</summary>
	public static class SshHostKeys {
		///<summary>Key "host:port|key type" (host in lower case), value: the SHA-256 fingerprint of the server's key.</summary>
		private static readonly ConcurrentDictionary<string,string> _dictFingerprints=new ConcurrentDictionary<string,string>();
		///<summary>Key "host:port": the message of the last refusal, so the caller can say why the connection failed.</summary>
		private static readonly ConcurrentDictionary<string,string> _dictRefusals=new ConcurrentDictionary<string,string>();

		///<summary>Checks every host key the client receives. Call before Connect.</summary>
		public static void Attach(BaseClient baseClient) {
			string host=baseClient.ConnectionInfo.Host;
			int port=baseClient.ConnectionInfo.Port;
			baseClient.HostKeyReceived+=(sender,hostKeyEventArgs) => {
				hostKeyEventArgs.CanTrust=CanTrust(host,port,GetKeyType(hostKeyEventArgs.HostKey),hostKeyEventArgs.FingerPrintSHA256);
			};
		}

		///<summary>True if the server's key may be trusted: the first key of its type from this host and port in this process, or the same key
		///again. False for a different key of a type already seen; the refusal is kept for GetRefusalMessage.</summary>
		public static bool CanTrust(string host,int port,string keyType,string fingerprintSha256) {
			if(string.IsNullOrEmpty(fingerprintSha256)) {
				return false;
			}
			string server=GetServerKey(host,port);
			string fingerprintKnown=_dictFingerprints.GetOrAdd(server+"|"+(keyType??""),fingerprintSha256);
			if(fingerprintKnown==fingerprintSha256) {
				string refusal;
				_dictRefusals.TryRemove(server,out refusal);
				return true;
			}
			_dictRefusals[server]="The SSH host key of "+host+":"+port+" ("+keyType+") changed while the program was running: it was SHA256:"
				+fingerprintKnown+", the server now presents SHA256:"+fingerprintSha256+". The connection was refused. If the server's key really "
				+"changed, restart the program; otherwise someone else may be answering for the server.";
			return false;
		}

		///<summary>The reason the last connection to host and port was refused for its host key, or null if it was not.</summary>
		public static string GetRefusalMessage(string host,int port) {
			string message;
			if(_dictRefusals.TryGetValue(GetServerKey(host,port),out message)) {
				return message;
			}
			return null;
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

		///<summary>If the exception is a connection the host key check refused, an exception that says so (with the original inside); otherwise the
		///exception itself.</summary>
		public static Exception Explain(Exception ex,string host,int port) {
			string message=GetRefusalMessage(host,port);
			if(message!=null && ex is SshConnectionException) {
				return new SshConnectionException(message,DisconnectReason.HostKeyNotVerifiable,ex);
			}
			return ex;
		}

		///<summary>Forgets every remembered key. Tests only.</summary>
		internal static void ForgetAll() {
			_dictFingerprints.Clear();
			_dictRefusals.Clear();
		}

		private static string GetServerKey(string host,int port) {
			return (host??"").Trim().ToLowerInvariant()+":"+port;
		}
	}
}
