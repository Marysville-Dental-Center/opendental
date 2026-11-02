// Remembered SSH host keys (docs/specs/ssh-host-keys.md, section 4): the host key stored for one SSH server. A fork table; Open Dental has no
// such table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>The host key the program remembers for one SSH server (host and port; one row per server, unique key HostPort). The first successful
	///connection to the server stores it (SshHostKeys.InsertFirstUse); every later connection must present the same key, or it is refused before
	///any credentials are sent, and the refused key is recorded here (SshHostKeys.RecordRefused). A user with Security Admin accepts the refused key
	///(SshHostKeys.AcceptRefusedKey) or removes the row (SshHostKeys.Delete) in Setup, SSH Host Keys. Every change is in the audit trail under
	///EnumPermType.SshHostKeys.</summary>
	[Serializable]
	[CrudTable]
	public class SshHostKey:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SshHostKeyNum;
		///<summary>The host name or IP address as the connection names it, trimmed and in lower case (SshHostKeyCheck.NormalizeHost).</summary>
		public string Host;
		///<summary>The TCP port of the server.</summary>
		public int Port;
		///<summary>The type of the stored key, as its key blob names it: for example ssh-ed25519, ecdsa-sha2-nistp256 or ssh-rsa.</summary>
		public string KeyType;
		///<summary>The SHA-256 fingerprint of the stored key: base64 without padding, as OpenSSH prints it after "SHA256:".</summary>
		public string FingerprintSha256;
		///<summary>FK to userod.UserNum. The user whose connection stored the key, or the administrator who accepted it. 0 for a service (the HL7
		///service).</summary>
		public long UserNum;
		///<summary>When the key was stored or accepted (the database server's time).</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTStored;
		///<summary>False: stored on the first successful connection. True: accepted by an administrator in place of the key stored before.</summary>
		public bool IsAccepted;
		///<summary>The type of the last key the server presented that was refused because it is not the stored key. Empty when no key was refused
		///since this key was stored.</summary>
		public string RefusedKeyType;
		///<summary>The SHA-256 fingerprint of that refused key. Empty when none.</summary>
		public string RefusedFingerprintSha256;
		///<summary>When a connection last refused that key (the database server's time). MinValue when none.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTRefused;

		///<summary></summary>
		public SshHostKey Copy() {
			return (SshHostKey)this.MemberwiseClone();
		}

		///<summary>True when a connection refused a key of this server that an administrator may accept.</summary>
		public bool HasRefusedKey() {
			return !string.IsNullOrEmpty(RefusedFingerprintSha256);
		}
	}
}
