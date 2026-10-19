// Secret obfuscation and SecurityHash tamper hashes, built on .NET's own crypto providers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.

namespace CodeBase {
	///<summary>Keyed hashes stored in the SecurityHash tamper-detection columns (appointment, claim, claimproc, patient, payplan, paysplit):
	///HMAC-SHA256, "osh1$" prefix, fixed public key. Tamper detection only; anyone with this source can compute a valid hash.
	///See CodeBase/Crypto/README.md. OpenDentBusiness.SecurityHash decides which rows get hashed and verified.</summary>
	public static class TamperHash {
		private static readonly object _lock=new object();
		private static SaltedHash _saltedHash;

		///<summary>Prefix of every hash Compute returns. A non-empty SecurityHash without it was written by Open Dental and can't be
		///recomputed here.</summary>
		public static string Prefix {
			get {
				return SaltedHash.Prefix;
			}
		}

		///<summary>Returns the keyed hash stored in SecurityHash columns. isPatientTable selects a separate key for the patient table.
		///Thread safe. Never returns null or empty.</summary>
		public static string Compute(string unhashedText,bool isPatientTable=false) {
			lock(_lock) {
				if(_saltedHash==null) {
					_saltedHash=new SaltedHash();
				}
				return _saltedHash.Compute(unhashedText,isPatientTable);
			}
		}

		///<summary>True if hash was produced by this version of Compute (has the current prefix).</summary>
		public static bool IsCurrentFormat(string hash) {
			return SaltedHash.IsCurrentFormat(hash);
		}

		///<summary>Releases the cached hash providers after a bulk rehash. The next Compute call recreates them.</summary>
		public static void ReleaseProviders() {
			lock(_lock) {
				_saltedHash?.Dispose();
				_saltedHash=null;
			}
		}
	}
}
